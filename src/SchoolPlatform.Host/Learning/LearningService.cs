using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Host.Learning;

/// <summary>PeriodId and Points (optional): what the homework is worth in a period; it becomes a graded activity of that period.</summary>
public sealed record AssignmentRequest(string Title, string? Instructions, DateTimeOffset DueAt, bool AllowLate, string? Version = null,
    Guid? PeriodId = null, decimal? Points = null);
public sealed record VersionBody(string Version);
public sealed record AssignmentView(Guid Id, Guid CourseId, string Title, string Instructions, DateTimeOffset DueAt, bool AllowLate,
    string Status, DateTimeOffset? PublishedAt, int Recipients, int Submitted, string Version, Guid? PeriodId = null, decimal? Points = null,
    int Scored = 0);
public sealed record SubmissionRow(Guid StudentId, string Name, string Code, Guid? SubmissionId, string? Text, DateTimeOffset? SubmittedAt,
    bool Late, int Attempts, string? Status, string? Feedback, string? Version, FileView[]? Files = null, decimal? Points = null,
    string? ScoreStatus = null);
public sealed record AssignmentSubmissions(AssignmentView Assignment, SubmissionRow[] Rows);
/// <summary>Points/ScoreStatus (optional) grade the homework in its activity together with the written feedback.</summary>
public sealed record FeedbackRequest(string Feedback, string Version, decimal? Points = null, string? ScoreStatus = null);
public sealed record AssignmentScoreRequest(Guid StudentId, decimal? Points, string Status);
public sealed record SubmitRequest(string Text, string? Version);
public sealed record StudentAssignment(Guid Id, string Course, string Title, string Instructions, DateTimeOffset DueAt, bool AllowLate,
    bool Closed, string? SubmissionStatus, DateTimeOffset? SubmittedAt, bool Late, string? Text, string? Feedback, string? Version, bool CanSubmit,
    Guid CourseId = default, decimal? Points = null, string? Period = null, DateTimeOffset? PublishedAt = null);

/// <summary>
/// Course activities and text submissions. Teaching scope comes from assignments; the student submits with their own account.
/// Guardians with the AcademicRecords faculty follow status and feedback. Activities are never blocked by financial clearance.
/// </summary>
public sealed class LearningService(SchoolDbContext db, SchoolAudit audit, TimeProvider time, CourseService courses, PeopleService people,
    Grades.ActivityService activities)
{
    public async Task<AssignmentView[]?> CourseAssignmentsAsync(Guid userId, Guid courseId, CancellationToken ct)
    {
        if (!await courses.CanViewAsync(userId, courseId, ct)) return null;
        var items = await db.Assignments.AsNoTracking().Where(x => x.CourseId == courseId).OrderByDescending(x => x.DueAt).ToArrayAsync(ct);
        var views = new List<AssignmentView>();
        foreach (var item in items) views.Add(await ViewAsync(item, ct));
        return views.ToArray();
    }

    public async Task<Guid> SaveAsync(Guid userId, Guid courseId, Guid? id, AssignmentRequest request, CancellationToken ct)
    {
        if (!await courses.CanTeachAsync(userId, courseId, ct)) throw Denied();
        var title = (request.Title ?? "").Trim();
        var instructions = (request.Instructions ?? "").Trim();
        if (title.Length is < 1 or > 160) throw OperationException.Invalid("Escribe un título de hasta 160 caracteres.");
        if (instructions.Length > 4000) throw OperationException.Invalid("Las instrucciones admiten hasta 4000 caracteres.");
        if (request.DueAt < time.GetUtcNow().AddYears(-1) || request.DueAt > time.GetUtcNow().AddYears(100))
            throw OperationException.Invalid("La fecha de entrega no es válida.");
        // A homework reaches the students placed in the course between publication and due date: a due date outside the
        // course's cycle would reach nobody.
        var year = await db.Courses.AsNoTracking().Where(c => c.Id == courseId).Join(db.Years, c => c.AcademicYearId, y => y.Id, (c, y) => y).SingleAsync(ct);
        var due = courses.LocalDate(request.DueAt);
        if (due < year.StartsOn || due > year.EndsOn)
            throw OperationException.Invalid($"La fecha de entrega debe estar dentro del ciclo del curso (del {year.StartsOn:dd/MM/yyyy} al {year.EndsOn:dd/MM/yyyy}).");
        if ((request.Points is null) != (request.PeriodId is null))
            throw OperationException.Invalid("Para calificar la tarea indica el período y su punteo.");
        await using var tx = await activities.BeginAsync(courseId, ct);
        Assignment assignment;
        if (id is null)
        {
            assignment = new Assignment { Id = Guid.NewGuid(), CourseId = courseId, CreatedAt = time.GetUtcNow(), CreatedBy = userId };
            db.Assignments.Add(assignment);
        }
        else
        {
            assignment = await Load(id.Value, courseId, request.Version, ct);
            if (assignment.Status == AssignmentStatus.Closed) throw new OperationException(409, "assignment-closed", "La actividad está cerrada.");
        }
        assignment.Title = title;
        assignment.Instructions = instructions;
        assignment.DueAt = request.DueAt;
        assignment.AllowLate = request.AllowLate;
        audit.Record(id is null ? "AssignmentCreated" : "AssignmentUpdated", userId, "Assignment", assignment.Id);
        await db.SaveChangesAsync(ct);
        await activities.LinkAssignmentAsync(userId, courseId, assignment.Id, title, request.PeriodId, request.Points, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return assignment.Id;
    }

    public async Task SetStatusAsync(Guid userId, Guid courseId, Guid id, string status, VersionBody request, CancellationToken ct)
    {
        if (!await courses.CanTeachAsync(userId, courseId, ct)) throw Denied();
        var assignment = await Load(id, courseId, request.Version, ct);
        var valid = (assignment.Status, status) is (AssignmentStatus.Draft, AssignmentStatus.Published) or (AssignmentStatus.Published, AssignmentStatus.Closed);
        if (!valid) throw new OperationException(409, "invalid-transition", "La actividad solo avanza de borrador a publicada y de publicada a cerrada.");
        assignment.Status = status;
        if (status == AssignmentStatus.Published) assignment.PublishedAt = time.GetUtcNow();
        audit.Record(status == AssignmentStatus.Published ? "AssignmentPublished" : "AssignmentClosed", userId, "Assignment", id);
        await db.SaveChangesAsync(ct);
    }

    public async Task<AssignmentSubmissions?> SubmissionsAsync(Guid userId, Guid courseId, Guid id, CancellationToken ct)
    {
        if (!await courses.CanViewAsync(userId, courseId, ct)) return null;
        var assignment = await db.Assignments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.CourseId == courseId, ct);
        if (assignment is null) return null;
        var recipients = await RecipientsAsync(assignment, ct);
        var submissions = await db.Submissions.AsNoTracking().Where(x => x.AssignmentId == id).ToDictionaryAsync(x => x.StudentId, ct);
        var submissionIds = submissions.Values.Select(x => x.Id).ToArray();
        var files = (await db.SubmissionFiles.AsNoTracking().Where(f => submissionIds.Contains(f.SubmissionId) && f.DeletedAt == null)
            .OrderBy(f => f.UploadedAt).ToArrayAsync(ct))
            .ToLookup(f => f.SubmissionId, f => new FileView(f.Id, f.FileName, f.ContentType, f.Size, f.UploadedAt));
        var points = await (from a in db.GradeActivities.AsNoTracking()
                            where a.AssignmentId == id && a.RemovedAt == null
                            join sc in db.ActivityScores on a.Id equals sc.ActivityId
                            select new { sc.StudentId, sc.Points, sc.Status }).ToDictionaryAsync(x => x.StudentId, ct);
        return new(await ViewAsync(assignment, ct), recipients.Select(r =>
        {
            points.TryGetValue(r.Id, out var p);
            return submissions.TryGetValue(r.Id, out var s)
                ? new SubmissionRow(r.Id, r.Name, r.Code, s.Id, s.Text, s.SubmittedAt, s.SubmittedAt > assignment.DueAt, s.Attempts, s.Status,
                    s.Feedback, Versions.Encode(s.Version), files[s.Id].ToArray(), p?.Points, p?.Status)
                : new SubmissionRow(r.Id, r.Name, r.Code, null, null, null, false, 0, null, null, null, null, p?.Points, p?.Status);
        }).ToArray());
    }

    public async Task FeedbackAsync(Guid userId, Guid courseId, Guid submissionId, FeedbackRequest request, CancellationToken ct)
    {
        if (!await courses.CanTeachAsync(userId, courseId, ct)) throw Denied();
        var feedback = (request.Feedback ?? "").Trim();
        // A comment is required unless the review is a score (points alone are a valid review).
        if (feedback.Length > 2000 || (feedback.Length == 0 && request.ScoreStatus is null))
            throw OperationException.Invalid("Escribe la retroalimentación (máximo 2000 caracteres).");
        var submission = await db.Submissions.SingleOrDefaultAsync(x => x.Id == submissionId &&
            db.Assignments.Any(a => a.Id == x.AssignmentId && a.CourseId == courseId), ct) ?? throw OperationException.NotFound("No encontramos esa entrega.");
        if (!submission.Version.AsSpan().SequenceEqual(Versions.Decode(request.Version))) throw OperationException.Changed();
        submission.Feedback = feedback.Length > 0 ? feedback : submission.Feedback;
        submission.FeedbackAt = time.GetUtcNow();
        submission.FeedbackBy = userId;
        submission.Status = SubmissionStatus.Reviewed;
        audit.Record("SubmissionReviewed", userId, "Assignment", submission.AssignmentId);
        await db.SaveChangesAsync(ct);
        if (request.ScoreStatus is { } status)
            await activities.ScoreAssignmentAsync(userId, courseId, submission.AssignmentId, submission.StudentId, request.Points, status, ct);
    }

    /// <summary>Points of a student in a homework, also without a submission (for example "no entregó").</summary>
    public async Task ScoreAsync(Guid userId, Guid courseId, Guid assignmentId, AssignmentScoreRequest request, CancellationToken ct)
    {
        if (!await courses.CanTeachAsync(userId, courseId, ct)) throw Denied();
        var assignment = await db.Assignments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == assignmentId && x.CourseId == courseId, ct) ?? throw NotFound();
        if (assignment.Status == AssignmentStatus.Draft) throw new OperationException(409, "assignment-draft", "Publica la tarea antes de calificarla.");
        await activities.ScoreAssignmentAsync(userId, courseId, assignmentId, request.StudentId, request.Points, request.Status, ct);
    }

    /// <summary>Activities of a student, for the student or a guardian with the AcademicRecords faculty; null means no access.</summary>
    public async Task<StudentAssignment[]?> StudentAssignmentsAsync(Guid userId, Guid studentId, CancellationToken ct)
    {
        var access = await people.StudentAccessAsync(userId, studentId, ct);
        if (access is null || !access.Has(Faculties.AcademicRecords)) return null;
        var candidates = await (from a in db.Assignments.AsNoTracking()
                                where a.Status != AssignmentStatus.Draft
                                join c in db.Courses on a.CourseId equals c.Id
                                join e in db.Enrollments on c.AcademicYearId equals e.AcademicYearId
                                where e.StudentId == studentId && e.Status != Academic.EnrollmentStatus.Cancelled
                                join s in db.Subjects on c.SubjectId equals s.Id
                                select new
                                {
                                    Assignment = a, Subject = s.Name,
                                    Worth = db.GradeActivities.Where(g => g.AssignmentId == a.Id && g.RemovedAt == null)
                                        .Select(g => new { g.MaxPoints, Period = db.Periods.Where(p => p.Id == g.PeriodId).Select(p => p.Name).FirstOrDefault() })
                                        .FirstOrDefault()
                                }).ToArrayAsync(ct);
        var submissions = await db.Submissions.AsNoTracking().Where(x => x.StudentId == studentId).ToDictionaryAsync(x => x.AssignmentId, ct);
        var now = time.GetUtcNow();
        var result = new List<StudentAssignment>();
        foreach (var item in candidates.OrderBy(x => x.Assignment.DueAt))
        {
            if (!(await RecipientsAsync(item.Assignment, ct)).Any(r => r.Id == studentId)) continue;
            submissions.TryGetValue(item.Assignment.Id, out var s);
            var open = item.Assignment.Status == AssignmentStatus.Published && (now <= item.Assignment.DueAt || item.Assignment.AllowLate) &&
                s?.Status != SubmissionStatus.Reviewed;
            result.Add(new(item.Assignment.Id, item.Subject, item.Assignment.Title, item.Assignment.Instructions, item.Assignment.DueAt,
                item.Assignment.AllowLate, item.Assignment.Status == AssignmentStatus.Closed, s?.Status, s?.SubmittedAt,
                s is not null && s.SubmittedAt > item.Assignment.DueAt, access.IsSelf ? s?.Text : null, s?.Feedback,
                s is null ? null : Versions.Encode(s.Version), access.IsSelf && open, item.Assignment.CourseId, item.Worth?.MaxPoints,
                item.Worth?.Period, item.Assignment.PublishedAt));
        }
        return result.ToArray();
    }

    public async Task SubmitAsync(Guid userId, Guid assignmentId, SubmitRequest request, CancellationToken ct)
    {
        var text = (request.Text ?? "").Trim();
        if (text.Length is < 1 or > 8000) throw OperationException.Invalid("Escribe tu entrega (máximo 8000 caracteres).");
        var assignment = await db.Assignments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == assignmentId, ct) ?? throw NotFound();
        var studentId = await db.AccountLinks.AsNoTracking().Where(l => l.UserId == userId && l.UnlinkedAt == null)
            .Select(l => (Guid?)l.PersonId).SingleOrDefaultAsync(ct);
        // Only the student's own account submits; guardians and staff cannot submit on the student's behalf.
        if (studentId is null || await people.StudentAccessAsync(userId, studentId.Value, ct) is not { IsSelf: true }) throw NotFound();
        if (assignment.Status == AssignmentStatus.Draft || !(await RecipientsAsync(assignment, ct)).Any(r => r.Id == studentId)) throw NotFound();
        var now = time.GetUtcNow();
        if (assignment.Status == AssignmentStatus.Closed) throw new OperationException(409, "assignment-closed", "La actividad ya cerró.");
        if (now > assignment.DueAt && !assignment.AllowLate)
            throw new OperationException(409, "past-due", "La fecha de entrega ya pasó y la actividad no acepta entregas tardías.");
        var submission = await db.Submissions.SingleOrDefaultAsync(x => x.AssignmentId == assignmentId && x.StudentId == studentId, ct);
        if (submission is null)
        {
            if (!string.IsNullOrEmpty(request.Version)) throw OperationException.Changed();
            db.Submissions.Add(new Submission
            {
                Id = Guid.NewGuid(), AssignmentId = assignmentId, StudentId = studentId.Value, Text = text, SubmittedAt = now,
                SubmittedBy = userId, Attempts = 1
            });
        }
        else
        {
            if (!submission.Version.AsSpan().SequenceEqual(Versions.Decode(request.Version))) throw OperationException.Changed();
            if (submission.Status == SubmissionStatus.Reviewed)
                throw new OperationException(409, "already-reviewed", "Tu entrega ya fue revisada; consulta con tu docente.");
            submission.Text = text;
            submission.SubmittedAt = now;
            submission.Attempts++;
        }
        audit.Record("AssignmentSubmitted", userId, "Assignment", assignmentId);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>The caller's own submission to an assignment, when the caller is that student.</summary>
    public async Task<Guid?> OwnSubmissionIdAsync(Guid userId, Guid assignmentId, CancellationToken ct) =>
        await (from l in db.AccountLinks
               where l.UserId == userId && l.UnlinkedAt == null
               join s in db.Submissions on l.PersonId equals s.StudentId
               where s.AssignmentId == assignmentId
               select (Guid?)s.Id).SingleOrDefaultAsync(ct);

    // ---- helpers

    private async Task<(Guid Id, string Name, string Code)[]> RecipientsAsync(Assignment assignment, CancellationToken ct)
    {
        if (assignment.PublishedAt is null) return [];
        var from = courses.LocalDate(assignment.PublishedAt.Value);
        var to = courses.LocalDate(assignment.DueAt);
        return await courses.StudentsBetweenAsync(assignment.CourseId, from < to ? from : to, to, ct);
    }

    private async Task<AssignmentView> ViewAsync(Assignment x, CancellationToken ct)
    {
        var recipients = (await RecipientsAsync(x, ct)).Select(r => r.Id).ToArray();
        var submitted = await db.Submissions.CountAsync(s => s.AssignmentId == x.Id && recipients.Contains(s.StudentId), ct);
        var activity = await db.GradeActivities.AsNoTracking().Where(a => a.AssignmentId == x.Id && a.RemovedAt == null)
            .Select(a => new { a.PeriodId, a.MaxPoints, Scored = db.ActivityScores.Count(s => s.ActivityId == a.Id) }).FirstOrDefaultAsync(ct);
        return new(x.Id, x.CourseId, x.Title, x.Instructions, x.DueAt, x.AllowLate, x.Status, x.PublishedAt, recipients.Length, submitted,
            Versions.Encode(x.Version), activity?.PeriodId, activity?.MaxPoints, activity?.Scored ?? 0);
    }

    private async Task<Assignment> Load(Guid id, Guid courseId, string? version, CancellationToken ct)
    {
        var assignment = await db.Assignments.SingleOrDefaultAsync(x => x.Id == id && x.CourseId == courseId, ct) ?? throw NotFound();
        if (!assignment.Version.AsSpan().SequenceEqual(Versions.Decode(version))) throw OperationException.Changed();
        return assignment;
    }

    private static OperationException Denied() => new(403, "teaching-denied", "No tienes asignado este curso.");
    private static OperationException NotFound() => OperationException.NotFound("No encontramos esa actividad.");
}
