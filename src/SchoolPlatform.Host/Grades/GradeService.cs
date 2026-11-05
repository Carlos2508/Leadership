using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.Finance;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Host.Grades;

public sealed record ScoreEntry(Guid StudentId, decimal? Score, string Status, string? Version);
public sealed record SaveScoresRequest(Guid PeriodId, ScoreEntry[] Entries, string? Reason);
public sealed record SavedScore(Guid StudentId, decimal? Score, string Status, string Version);
public sealed record SaveScoresResult(int Changed, SavedScore[] Rows);
public sealed record SheetRow(Guid StudentId, string Name, string Code, decimal? Score, string? Status, string? Version);
public sealed record SheetPeriod(Guid Id, int Sequence, string Name, DateOnly StartsOn, DateOnly EndsOn, bool Published);
public sealed record GradeSheet(CourseView Course, SheetPeriod Period, SheetPeriod[] Periods, bool CanEdit, bool RequiresReason, SheetRow[] Rows);
public sealed record PublicationView(Guid PeriodId, int Sequence, string Name, DateOnly StartsOn, DateOnly EndsOn, bool Published,
    DateTimeOffset? PublishedAt, string? Cutoff, int Expected, int Recorded);
public sealed record PublishRequest(Guid PeriodId, Guid CutoffId, string Note);
public sealed record MyGrade(string Subject, decimal? Score, string Status);
public sealed record MyGradesPeriod(Guid PeriodId, int Sequence, string Name, string Status, MyGrade[] Grades, PendingItem[] Pending);
public sealed record MyGrades(Guid StudentId, string? Year, MyGradesPeriod[] Periods);

/// <summary>
/// Period scores per course, with revisions; publication per period tied to a clearance cutoff; and the family/student
/// view, which requires the AcademicRecords faculty (or being the student) and clearance for each publication on every read.
/// Only captured period scores are published: annual averages and rounding remain internal until the policy is approved.
/// </summary>
public sealed class GradeService(SchoolDbContext db, SchoolAudit audit, TimeProvider time, CourseService courses,
    ClearanceService clearance, PeopleService people)
{
    public async Task<GradeSheet?> SheetAsync(Guid userId, Guid courseId, Guid? periodId, CancellationToken ct)
    {
        if (!await courses.CanViewAsync(userId, courseId, ct)) return null;
        var course = await courses.CourseAsync(courseId, ct);
        var periods = await Periods(course.AcademicYearId, ct);
        if (periods.Length == 0) throw new OperationException(409, "periods-required", "El ciclo no tiene períodos definidos.");
        var period = periodId is null ? periods.FirstOrDefault(x => !x.Published) ?? periods[^1] : periods.SingleOrDefault(x => x.Id == periodId)
            ?? throw OperationException.NotFound("No encontramos ese período en el ciclo del curso.");
        var canCapture = await CanCaptureAsync(userId, courseId, ct);
        var isManager = await IsManagerAsync(userId, ct);
        var students = await StudentsAsync(course, period, ct);
        var ids = students.Select(x => x.Id).ToArray();
        var scores = await db.PeriodScores.AsNoTracking().Where(x => x.CourseId == courseId && x.PeriodId == period.Id && ids.Contains(x.StudentId))
            .ToDictionaryAsync(x => x.StudentId, ct);
        return new(course, period, periods, period.Published ? isManager : canCapture, period.Published,
            students.Select(s => scores.TryGetValue(s.Id, out var score)
                ? new SheetRow(s.Id, s.Name, s.Code, score.Score, score.Status, Versions.Encode(score.Version))
                : new SheetRow(s.Id, s.Name, s.Code, null, null, null)).ToArray());
    }

    public async Task<SaveScoresResult> SaveAsync(Guid userId, Guid courseId, SaveScoresRequest request, CancellationToken ct)
    {
        if (!await courses.CanViewAsync(userId, courseId, ct)) throw OperationException.NotFound("No encontramos ese curso.");
        var course = await courses.CourseAsync(courseId, ct);
        var period = (await Periods(course.AcademicYearId, ct)).SingleOrDefault(x => x.Id == request.PeriodId)
            ?? throw OperationException.NotFound("No encontramos ese período en el ciclo del curso.");
        string reason;
        if (period.Published)
        {
            // Published results change only through an explicit correction by academic management, with a reason.
            if (!await IsManagerAsync(userId, ct))
                throw new OperationException(403, "period-published", "El período ya fue publicado. Solicita una corrección a coordinación académica.");
            reason = Reasons.Require(request.Reason);
        }
        else
        {
            if (!await CanCaptureAsync(userId, courseId, ct)) throw new OperationException(403, "capture-denied", "No tienes asignado este curso para capturar notas.");
            reason = request.Reason?.Trim() is { Length: > 0 } text ? text[..Math.Min(text.Length, 500)] : "Captura";
        }
        var entries = request.Entries ?? [];
        if (entries.Length is 0 or > 200 || entries.Select(x => x.StudentId).Distinct().Count() != entries.Length)
            throw OperationException.Invalid("Envía una fila por estudiante.");
        foreach (var entry in entries)
        {
            if (!ScoreStatus.All.Contains(entry.Status)) throw OperationException.Invalid("Estado de nota inválido.");
            if (entry.Status == ScoreStatus.Scored && (entry.Score is null or < 0 or > 100 || decimal.Round(entry.Score.Value, 2) != entry.Score))
                throw OperationException.Invalid("Cada nota debe estar entre 0 y 100, con hasta dos decimales.");
            if (entry.Status != ScoreStatus.Scored && entry.Score is not null)
                throw OperationException.Invalid("Solo una nota registrada lleva valor numérico.");
        }
        var allowed = (await StudentsAsync(course, period, ct)).Select(x => x.Id).ToHashSet();
        if (entries.Any(x => !allowed.Contains(x.StudentId)))
            throw OperationException.Invalid("Un estudiante no pertenecía al curso durante ese período.");
        var ids = entries.Select(x => x.StudentId).ToArray();
        var current = await db.PeriodScores.Where(x => x.CourseId == courseId && x.PeriodId == period.Id && ids.Contains(x.StudentId))
            .ToDictionaryAsync(x => x.StudentId, ct);
        var now = time.GetUtcNow();
        var changed = 0;
        var touched = new List<PeriodScore>(entries.Length);
        foreach (var entry in entries)
        {
            if (current.TryGetValue(entry.StudentId, out var score))
            {
                if (!score.Version.AsSpan().SequenceEqual(Versions.Decode(entry.Version))) throw OperationException.Changed();
                touched.Add(score);
                if (score.Score == entry.Score && score.Status == entry.Status) continue;
                db.ScoreRevisions.Add(new ScoreRevision
                {
                    Id = Guid.NewGuid(), ScoreId = score.Id, PreviousScore = score.Score, PreviousStatus = score.Status,
                    NewScore = entry.Score, NewStatus = entry.Status, Reason = reason, ChangedAt = now, ChangedBy = userId
                });
                score.Score = entry.Score;
                score.Status = entry.Status;
                score.UpdatedAt = now;
                score.UpdatedBy = userId;
            }
            else
            {
                if (!string.IsNullOrEmpty(entry.Version)) throw OperationException.Changed();
                score = new PeriodScore
                {
                    Id = Guid.NewGuid(), CourseId = courseId, PeriodId = period.Id, StudentId = entry.StudentId, Score = entry.Score,
                    Status = entry.Status, UpdatedAt = now, UpdatedBy = userId
                };
                db.PeriodScores.Add(score);
                touched.Add(score);
                db.ScoreRevisions.Add(new ScoreRevision
                {
                    Id = Guid.NewGuid(), ScoreId = score.Id, NewScore = entry.Score, NewStatus = entry.Status, Reason = reason,
                    ChangedAt = now, ChangedBy = userId
                });
            }
            changed++;
        }
        if (changed > 0)
        {
            audit.Record(period.Published ? "PublishedScoresCorrected" : "ScoresRecorded", userId, "Course", courseId,
                period.Published ? reason : "", $"{period.Sequence}:{changed}");
            await db.SaveChangesAsync(ct);
        }
        // New row versions let the sheet keep autosaving without reloading (and without losing what is being typed).
        return new(changed, touched.Select(x => new SavedScore(x.StudentId, x.Score, x.Status, Versions.Encode(x.Version))).ToArray());
    }

    public async Task<PublicationView[]> PublicationsAsync(Guid yearId, CancellationToken ct)
    {
        var periods = await Periods(yearId, ct);
        var yearCourses = await courses.CoursesAsync(yearId, null, ct);
        var publications = await db.GradePublications.AsNoTracking().Where(x => x.AcademicYearId == yearId)
            .Join(db.ClearanceCutoffs, p => p.CutoffId, c => c.Id, (p, c) => new { p.PeriodId, p.PublishedAt, Cutoff = c.Name })
            .ToDictionaryAsync(x => x.PeriodId, ct);
        var result = new List<PublicationView>();
        foreach (var period in periods)
        {
            var (expected, recorded) = await CompletenessAsync(yearCourses, period, ct);
            publications.TryGetValue(period.Id, out var publication);
            result.Add(new(period.Id, period.Sequence, period.Name, period.StartsOn, period.EndsOn, period.Published,
                publication?.PublishedAt, publication?.Cutoff, expected, recorded));
        }
        return result.ToArray();
    }

    public async Task<object[]> CutoffsAsync(Guid yearId, CancellationToken ct) =>
        await db.ClearanceCutoffs.AsNoTracking().Where(x => x.AcademicYearId == yearId).OrderBy(x => x.ThroughSequence)
            .Select(x => (object)new { x.Id, x.Name, x.ThroughSequence }).ToArrayAsync(ct);

    public async Task PublishAsync(Guid actor, Guid yearId, PublishRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.AcademicManage);
        var note = (request.Note ?? "").Trim();
        if (note.Length > 500) throw OperationException.Invalid("La nota admite como máximo 500 caracteres.");
        var period = (await Periods(yearId, ct)).SingleOrDefault(x => x.Id == request.PeriodId)
            ?? throw OperationException.NotFound("No encontramos ese período.");
        if (period.Published) throw new OperationException(409, "already-published", "Ese período ya fue publicado.");
        if (!await db.ClearanceCutoffs.AnyAsync(x => x.Id == request.CutoffId && x.AcademicYearId == yearId, ct))
            throw OperationException.Invalid("Selecciona un corte de solvencia del mismo ciclo.");
        var (expected, recorded) = await CompletenessAsync(await courses.CoursesAsync(yearId, null, ct), period, ct);
        if (expected == 0) throw new OperationException(409, "nothing-to-publish", "No hay estudiantes en cursos durante ese período.");
        if (recorded < expected)
            throw new OperationException(409, "incomplete", $"Faltan {expected - recorded} notas por registrar (o marcar como pendiente/exonerado) antes de publicar.");
        db.GradePublications.Add(new GradePublication
        {
            Id = Guid.NewGuid(), AcademicYearId = yearId, PeriodId = period.Id, CutoffId = request.CutoffId, PublishedAt = time.GetUtcNow(),
            PublishedBy = actor, Note = note
        });
        audit.Record("GradesPublished", actor, "AcademicYear", yearId, note, $"{period.Sequence}");
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Family/student view. Returns null when the account has no academic access to the student (respond 404).</summary>
    public async Task<MyGrades?> MyGradesAsync(Guid userId, Guid studentId, CancellationToken ct)
    {
        var access = await people.StudentAccessAsync(userId, studentId, ct);
        if (access is null || !access.Has(Faculties.AcademicRecords)) return null;
        var canSeeFinance = !access.IsSelf && access.Faculties.Contains(Faculties.Finance);
        var enrollment = await db.Enrollments.AsNoTracking().Where(x => x.StudentId == studentId && x.Status != EnrollmentStatus.Cancelled)
            .Join(db.Years, e => e.AcademicYearId, y => y.Id, (e, y) => new { e.AcademicYearId, y.Label, y.IsDefault, y.StartsOn })
            .OrderByDescending(x => x.IsDefault).ThenByDescending(x => x.StartsOn).FirstOrDefaultAsync(ct);
        if (enrollment is null) return new(studentId, null, []);
        var publications = await db.GradePublications.AsNoTracking().Where(x => x.AcademicYearId == enrollment.AcademicYearId)
            .Join(db.Periods, p => p.PeriodId, period => period.Id, (p, period) => new { p.PeriodId, p.CutoffId, period.Sequence, period.Name })
            .OrderBy(x => x.Sequence).ToArrayAsync(ct);
        var result = new List<MyGradesPeriod>();
        foreach (var publication in publications)
        {
            // Clearance is evaluated from confirmed SQL data on every read; nothing about blocked results is returned.
            var check = await clearance.CheckAsync(studentId, publication.CutoffId, ct);
            if (check.Status != ClearanceStatus.Clear)
            {
                result.Add(new(publication.PeriodId, publication.Sequence, publication.Name,
                    check.Status == ClearanceStatus.PendingFinancialSetup ? "PendingFinancialSetup" : "Blocked", [],
                    canSeeFinance ? check.Pending : []));
                continue;
            }
            var grades = await db.PeriodScores.AsNoTracking().Where(x => x.StudentId == studentId && x.PeriodId == publication.PeriodId)
                .Join(db.Courses, s => s.CourseId, c => c.Id, (s, c) => new { s.Score, s.Status, c.SubjectId })
                .Join(db.Subjects, x => x.SubjectId, subject => subject.Id, (x, subject) => new { Subject = subject.Name, x.Score, x.Status })
                .OrderBy(x => x.Subject).ToArrayAsync(ct);
            result.Add(new(publication.PeriodId, publication.Sequence, publication.Name, "Available",
                grades.Select(x => new MyGrade(x.Subject, x.Score, x.Status)).ToArray(), []));
        }
        return new(studentId, enrollment.Label, result.ToArray());
    }

    // ---- helpers

    private async Task<SheetPeriod[]> Periods(Guid yearId, CancellationToken ct) =>
        await db.Periods.AsNoTracking().Where(x => x.AcademicYearId == yearId).OrderBy(x => x.Sequence)
            .Select(x => new SheetPeriod(x.Id, x.Sequence, x.Name, x.StartsOn, x.EndsOn, db.GradePublications.Any(p => p.PeriodId == x.Id)))
            .ToArrayAsync(ct);

    private async Task<bool> IsManagerAsync(Guid userId, CancellationToken ct) =>
        await db.Permissions.AnyAsync(x => x.UserId == userId && x.Permission == Permissions.AcademicManage, ct) &&
        await db.Accounts.AnyAsync(x => x.Id == userId && x.IsActive, ct);

    private async Task<bool> CanCaptureAsync(Guid userId, Guid courseId, CancellationToken ct) =>
        await IsManagerAsync(userId, ct) || (await courses.MyCoursesAsync(userId, ct)).Any(x => x.Id == courseId);

    private sealed record CourseStudent(Guid Id, string Name, string Code);

    // Students who belonged to the course at any moment of the period (dated placements; empty intervals excluded).
    private async Task<CourseStudent[]> StudentsAsync(CourseView course, SheetPeriod period, CancellationToken ct) =>
        (await (from p in db.Placements.AsNoTracking()
                join e in db.Enrollments on p.EnrollmentId equals e.Id
                where e.AcademicYearId == course.AcademicYearId && e.Status != EnrollmentStatus.Cancelled &&
                      p.GradeLevelId == course.GradeLevelId && (course.SectionId == null || p.SectionId == course.SectionId) &&
                      p.StartsOn <= period.EndsOn && (p.EndsOn == null || (p.EndsOn > period.StartsOn && p.EndsOn > p.StartsOn))
                join person in db.Persons on e.StudentId equals person.Id
                join student in db.Students on e.StudentId equals student.PersonId
                select new { e.StudentId, person.GivenNames, person.FamilyNames, person.PreferredName, student.StudentCode })
            .Distinct().ToArrayAsync(ct))
        .OrderBy(x => x.FamilyNames).ThenBy(x => x.GivenNames)
        .Select(x => new CourseStudent(x.StudentId, PeopleService.Display(x.GivenNames, x.FamilyNames, x.PreferredName), x.StudentCode)).ToArray();

    private async Task<(int Expected, int Recorded)> CompletenessAsync(CourseView[] yearCourses, SheetPeriod period, CancellationToken ct)
    {
        var expected = 0;
        var recorded = 0;
        foreach (var course in yearCourses)
        {
            var ids = (await StudentsAsync(course, period, ct)).Select(x => x.Id).ToArray();
            expected += ids.Length;
            recorded += await db.PeriodScores.CountAsync(x => x.CourseId == course.Id && x.PeriodId == period.Id && ids.Contains(x.StudentId), ct);
        }
        return (expected, recorded);
    }
}
