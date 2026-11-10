using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Host.Grades;

public sealed record ActivityView(Guid Id, string Title, decimal MaxPoints, int Sequence, Guid? AssignmentId, int Scored, string Version);
public sealed record ActivityCell(decimal? Points, string Status, string Version);
public sealed record ActivityRow(Guid StudentId, string Name, string Code, Dictionary<Guid, ActivityCell> Scores, decimal? Total,
    string? TotalStatus, decimal? PeriodScore, string? PeriodStatus, string? PeriodVersion);
public sealed record ActivityTemplateView(Guid Id, string Name, TemplateItem[] Items, decimal Total, bool IsActive, string Version);
public sealed record ActivityBook(CourseView Course, SheetPeriod Period, SheetPeriod[] Periods, bool CanEdit, bool RequiresReason,
    decimal TotalPoints, ActivityView[] Activities, ActivityRow[] Rows, ActivityTemplateView[] Templates);
public sealed record SaveActivityRequest(Guid PeriodId, string Title, decimal MaxPoints, string? Version, string? Reason);
public sealed record RemoveActivityRequest(string Version, string? Reason);
public sealed record ActivityScoreEntry(Guid ActivityId, Guid StudentId, decimal? Points, string Status, string? Version);
public sealed record SaveActivityScoresRequest(Guid PeriodId, ActivityScoreEntry[] Entries, string? Reason);
public sealed record ApplyTemplateRequest(Guid PeriodId, Guid TemplateId);
public sealed record SaveTemplateRequest(string Name, TemplateItem[] Items, bool IsActive, string? Version, string Reason);

/// <summary>
/// Activities with points per course and period (the period result is the sum of the points obtained), institutional
/// activity templates and the link between a homework and its activity. When a course and period have activities, the
/// period score in <see cref="PeriodScore"/> is derived here (with its revision) so publication, report cards and the
/// clearance check keep working unchanged. Writes run serializable under a per-course applock. Before publication the
/// assigned teacher or academic management edits; after publication only academic management, with a reason.
/// </summary>
public sealed class ActivityService(SchoolDbContext db, SchoolAudit audit, TimeProvider time, CourseService courses)
{
    private const decimal PeriodMax = 100m;

    public async Task<ActivityBook?> BookAsync(Guid userId, Guid courseId, Guid? periodId, CancellationToken ct)
    {
        if (!await courses.CanViewAsync(userId, courseId, ct)) return null;
        var course = await courses.CourseAsync(courseId, ct);
        var periods = await PeriodsAsync(course.AcademicYearId, ct);
        if (periods.Length == 0) throw new OperationException(409, "periods-required", "El ciclo no tiene períodos definidos.");
        var period = periodId is null ? periods.FirstOrDefault(x => !x.Published) ?? periods[^1]
            : periods.SingleOrDefault(x => x.Id == periodId) ?? throw OperationException.NotFound("No encontramos ese período en el ciclo del curso.");
        var canCapture = await courses.CanTeachAsync(userId, courseId, ct);
        var isManager = await IsManagerAsync(userId, ct);
        var activities = await db.GradeActivities.AsNoTracking().Where(x => x.RemovedAt == null && x.CourseId == courseId && x.PeriodId == period.Id)
            .OrderBy(x => x.Sequence).ThenBy(x => x.CreatedAt).ToArrayAsync(ct);
        var activityIds = activities.Select(x => x.Id).ToArray();
        var students = await courses.StudentsBetweenAsync(courseId, period.StartsOn, period.EndsOn, ct);
        var studentIds = students.Select(x => x.Id).ToArray();
        var scores = await db.ActivityScores.AsNoTracking().Where(x => activityIds.Contains(x.ActivityId) && studentIds.Contains(x.StudentId))
            .ToArrayAsync(ct);
        var periodScores = await db.PeriodScores.AsNoTracking().Where(x => x.CourseId == courseId && x.PeriodId == period.Id && studentIds.Contains(x.StudentId))
            .ToDictionaryAsync(x => x.StudentId, ct);
        var byStudent = scores.ToLookup(x => x.StudentId);
        var templates = await TemplatesAsync(activeOnly: true, ct);
        return new(course, period, periods, period.Published ? isManager : canCapture, period.Published, activities.Sum(x => x.MaxPoints),
            activities.Select(a => new ActivityView(a.Id, a.Title, a.MaxPoints, a.Sequence, a.AssignmentId, scores.Count(s => s.ActivityId == a.Id),
                Versions.Encode(a.Version))).ToArray(),
            students.Select(s =>
            {
                var own = byStudent[s.Id].ToArray();
                var (total, status) = Sum(own);
                periodScores.TryGetValue(s.Id, out var p);
                return new ActivityRow(s.Id, s.Name, s.Code, own.ToDictionary(x => x.ActivityId, x => new ActivityCell(x.Points, x.Status, Versions.Encode(x.Version))),
                    total, status, p?.Score, p?.Status, p is null ? null : Versions.Encode(p.Version));
            }).ToArray(),
            templates);
    }

    public async Task<Guid> CreateAsync(Guid userId, Guid courseId, SaveActivityRequest request, CancellationToken ct)
    {
        await using var tx = await BeginAsync(courseId, ct);
        var (_, period, reason) = await AuthorizeAsync(userId, courseId, request.PeriodId, request.Reason, ct);
        var title = Title(request.Title);
        var points = Points(request.MaxPoints);
        var existing = await db.GradeActivities.Where(x => x.RemovedAt == null && x.CourseId == courseId && x.PeriodId == period.Id).ToArrayAsync(ct);
        EnsureTotal(existing.Sum(x => x.MaxPoints) + points);
        var activity = new GradeActivity
        {
            Id = Guid.NewGuid(), CourseId = courseId, PeriodId = period.Id, Title = title, MaxPoints = points,
            Sequence = existing.Length == 0 ? 1 : existing.Max(x => x.Sequence) + 1, CreatedAt = time.GetUtcNow(), CreatedBy = userId
        };
        db.GradeActivities.Add(activity);
        audit.Record("GradeActivityCreated", userId, "Course", courseId, period.Published ? reason : "", $"{period.Sequence}:{points}");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return activity.Id;
    }

    public async Task UpdateAsync(Guid userId, Guid courseId, Guid activityId, SaveActivityRequest request, CancellationToken ct)
    {
        await using var tx = await BeginAsync(courseId, ct);
        var activity = await LoadAsync(courseId, activityId, request.Version, ct);
        var (_, period, reason) = await AuthorizeAsync(userId, courseId, activity.PeriodId, request.Reason, ct);
        var title = Title(request.Title);
        var points = Points(request.MaxPoints);
        var others = await db.GradeActivities.Where(x => x.RemovedAt == null && x.CourseId == courseId && x.PeriodId == period.Id && x.Id != activityId).SumAsync(x => x.MaxPoints, ct);
        EnsureTotal(others + points);
        if (points < activity.MaxPoints && await db.ActivityScores.AnyAsync(x => x.ActivityId == activityId && x.Points > points, ct))
            throw new OperationException(409, "points-below-scores", "Hay estudiantes con más puntos que el nuevo valor. Corrige primero esos punteos.");
        activity.Title = title;
        activity.MaxPoints = points;
        audit.Record("GradeActivityUpdated", userId, "Course", courseId, period.Published ? reason : "", $"{period.Sequence}:{points}");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>
    /// Removes an activity from the period. It is marked as removed (points and revisions are kept for the record) and the
    /// period result of the students who had points is recalculated. Removing a scored activity requires a reason.
    /// </summary>
    public async Task RemoveAsync(Guid userId, Guid courseId, Guid activityId, RemoveActivityRequest request, CancellationToken ct)
    {
        await using var tx = await BeginAsync(courseId, ct);
        var activity = await LoadAsync(courseId, activityId, request.Version, ct);
        var (_, period, reason) = await AuthorizeAsync(userId, courseId, activity.PeriodId, request.Reason, ct);
        if (activity.AssignmentId is not null)
            throw new OperationException(409, "linked-assignment", "Esta actividad pertenece a una tarea. Quita el punteo desde la tarea.");
        var students = await db.ActivityScores.Where(x => x.ActivityId == activityId).Select(x => x.StudentId).ToArrayAsync(ct);
        if (students.Length > 0) reason = Reasons.Require(request.Reason);
        activity.RemovedAt = time.GetUtcNow();
        activity.RemovedBy = userId;
        activity.RemoveReason = reason;
        await db.SaveChangesAsync(ct);
        audit.Record("GradeActivityRemoved", userId, "Course", courseId, reason, $"{period.Sequence}:{activity.Title}:{students.Length}");
        await SyncAsync(userId, courseId, period, students, $"Actividad quitada: {reason}", ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<int> SaveScoresAsync(Guid userId, Guid courseId, SaveActivityScoresRequest request, CancellationToken ct)
    {
        await using var tx = await BeginAsync(courseId, ct);
        var (course, period, reason) = await AuthorizeAsync(userId, courseId, request.PeriodId, request.Reason, ct);
        var entries = request.Entries ?? [];
        if (entries.Length is 0 or > 4000 || entries.Select(x => (x.ActivityId, x.StudentId)).Distinct().Count() != entries.Length)
            throw OperationException.Invalid("Envía un punteo por estudiante y actividad.");
        var activities = await db.GradeActivities.AsNoTracking().Where(x => x.RemovedAt == null && x.CourseId == courseId && x.PeriodId == period.Id)
            .ToDictionaryAsync(x => x.Id, ct);
        foreach (var entry in entries)
        {
            if (!activities.TryGetValue(entry.ActivityId, out var activity)) throw OperationException.Invalid("Una actividad no pertenece al curso y período.");
            Validate(entry.Status, entry.Points, activity);
        }
        var allowed = (await courses.StudentsBetweenAsync(courseId, period.StartsOn, period.EndsOn, ct)).Select(x => x.Id).ToHashSet();
        if (entries.Any(x => !allowed.Contains(x.StudentId))) throw OperationException.Invalid("Un estudiante no pertenecía al curso durante ese período.");
        var ids = entries.Select(x => x.ActivityId).Distinct().ToArray();
        var current = await db.ActivityScores.Where(x => ids.Contains(x.ActivityId)).ToDictionaryAsync(x => (x.ActivityId, x.StudentId), ct);
        var now = time.GetUtcNow();
        var touched = new HashSet<Guid>();
        foreach (var entry in entries)
        {
            current.TryGetValue((entry.ActivityId, entry.StudentId), out var score);
            if (score is not null && !score.Version.AsSpan().SequenceEqual(Versions.Decode(entry.Version))) throw OperationException.Changed();
            if (score is null && !string.IsNullOrEmpty(entry.Version)) throw OperationException.Changed();
            if (Write(score, entry.ActivityId, entry.StudentId, entry.Points, entry.Status, reason, userId, now)) touched.Add(entry.StudentId);
        }
        if (touched.Count == 0) return 0;
        await db.SaveChangesAsync(ct);
        audit.Record(period.Published ? "PublishedActivityScoresCorrected" : "ActivityScoresRecorded", userId, "Course", courseId,
            period.Published ? reason : "", $"{period.Sequence}:{touched.Count}");
        await SyncAsync(userId, courseId, period, touched.ToArray(), period.Published ? reason : "Punteo de actividades", ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return touched.Count;
    }

    public async Task<int> ApplyTemplateAsync(Guid userId, Guid courseId, ApplyTemplateRequest request, CancellationToken ct)
    {
        await using var tx = await BeginAsync(courseId, ct);
        var (_, period, _) = await AuthorizeAsync(userId, courseId, request.PeriodId, null, ct);
        var template = await db.ActivityTemplates.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.TemplateId && x.IsActive, ct)
            ?? throw OperationException.NotFound("No encontramos esa plantilla.");
        var items = Items(template.ItemsJson);
        var existing = await db.GradeActivities.Where(x => x.RemovedAt == null && x.CourseId == courseId && x.PeriodId == period.Id).ToArrayAsync(ct);
        var titles = existing.Select(x => x.Title).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = items.Where(x => !titles.Contains(x.Title)).ToArray();
        EnsureTotal(existing.Sum(x => x.MaxPoints) + added.Sum(x => x.Points));
        var sequence = existing.Length == 0 ? 0 : existing.Max(x => x.Sequence);
        foreach (var item in added)
            db.GradeActivities.Add(new GradeActivity
            {
                Id = Guid.NewGuid(), CourseId = courseId, PeriodId = period.Id, Title = item.Title, MaxPoints = item.Points,
                Sequence = ++sequence, CreatedAt = time.GetUtcNow(), CreatedBy = userId
            });
        audit.Record("GradeActivityTemplateApplied", userId, "Course", courseId, "", $"{period.Sequence}:{template.Name}:{added.Length}");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return added.Length;
    }

    // ---- homework link (called by LearningService inside its own save)

    /// <summary>
    /// Sets the points a homework is worth in a period: creates, updates or removes its linked activity. Removing the points
    /// of a homework that already has scores is refused.
    /// </summary>
    public async Task LinkAssignmentAsync(Guid userId, Guid courseId, Guid assignmentId, string title, Guid? periodId, decimal? points, CancellationToken ct)
    {
        var activity = await db.GradeActivities.SingleOrDefaultAsync(x => x.RemovedAt == null && x.AssignmentId == assignmentId, ct);
        if (points is null || periodId is null)
        {
            if (activity is null) return;
            if (await db.ActivityScores.AnyAsync(x => x.ActivityId == activity.Id, ct))
                throw new OperationException(409, "assignment-scored", "La tarea ya tiene punteos registrados; no se puede quitar su valor.");
            activity.RemovedAt = time.GetUtcNow();
            activity.RemovedBy = userId;
            activity.RemoveReason = "Tarea sin punteo";
            return;
        }
        var (_, period, _) = await AuthorizeAsync(userId, courseId, periodId.Value, null, ct);
        var value = Points(points.Value);
        var others = await db.GradeActivities.Where(x => x.RemovedAt == null && x.CourseId == courseId && x.PeriodId == period.Id && x.Id != (activity == null ? Guid.Empty : activity.Id))
            .ToArrayAsync(ct);
        EnsureTotal(others.Sum(x => x.MaxPoints) + value);
        var label = Title(title.Length > 120 ? title[..120] : title);
        if (activity is null)
        {
            db.GradeActivities.Add(new GradeActivity
            {
                Id = Guid.NewGuid(), CourseId = courseId, PeriodId = period.Id, Title = label, MaxPoints = value, AssignmentId = assignmentId,
                Sequence = others.Length == 0 ? 1 : others.Max(x => x.Sequence) + 1, CreatedAt = time.GetUtcNow(), CreatedBy = userId
            });
            return;
        }
        var scored = await db.ActivityScores.AnyAsync(x => x.ActivityId == activity.Id, ct);
        if (activity.PeriodId != period.Id && scored)
            throw new OperationException(409, "assignment-scored", "La tarea ya tiene punteos en otro período; no se puede mover.");
        if (value < activity.MaxPoints && await db.ActivityScores.AnyAsync(x => x.ActivityId == activity.Id && x.Points > value, ct))
            throw new OperationException(409, "points-below-scores", "Hay estudiantes con más puntos que el nuevo valor de la tarea.");
        activity.PeriodId = period.Id;
        activity.Title = label;
        activity.MaxPoints = value;
    }

    /// <summary>Points of one student in a homework (from the review of a submission or the gradebook), with revision.</summary>
    public async Task ScoreAssignmentAsync(Guid userId, Guid courseId, Guid assignmentId, Guid studentId, decimal? points, string status, CancellationToken ct)
    {
        var activity = await db.GradeActivities.AsNoTracking().SingleOrDefaultAsync(x => x.RemovedAt == null && x.AssignmentId == assignmentId && x.CourseId == courseId, ct)
            ?? throw OperationException.Invalid("Asigna un punteo a la tarea antes de calificarla.");
        await using var tx = await BeginAsync(courseId, ct);
        var (_, period, reason) = await AuthorizeAsync(userId, courseId, activity.PeriodId, null, ct);
        Validate(status, points, activity);
        if (!(await courses.StudentsBetweenAsync(courseId, period.StartsOn, period.EndsOn, ct)).Any(x => x.Id == studentId))
            throw OperationException.Invalid("El estudiante no pertenecía al curso durante ese período.");
        var score = await db.ActivityScores.SingleOrDefaultAsync(x => x.ActivityId == activity.Id && x.StudentId == studentId, ct);
        if (!Write(score, activity.Id, studentId, points, status, "Calificación de la tarea", userId, time.GetUtcNow())) { await tx.CommitAsync(ct); return; }
        await db.SaveChangesAsync(ct);
        audit.Record("ActivityScoresRecorded", userId, "Course", courseId, "", $"{period.Sequence}:1");
        await SyncAsync(userId, courseId, period, [studentId], "Calificación de la tarea", ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    // ---- templates (academic management)

    public async Task<ActivityTemplateView[]> TemplatesAsync(bool activeOnly, CancellationToken ct) =>
        (await db.ActivityTemplates.AsNoTracking().Where(x => !activeOnly || x.IsActive).OrderBy(x => x.Name).ToArrayAsync(ct))
            .Select(x => { var items = Items(x.ItemsJson); return new ActivityTemplateView(x.Id, x.Name, items, items.Sum(i => i.Points), x.IsActive, Versions.Encode(x.Version)); })
            .ToArray();

    public async Task<Guid> SaveTemplateAsync(Guid userId, Guid? id, SaveTemplateRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(userId, ct, Permissions.AcademicManage);
        var reason = Reasons.Require(request.Reason);
        var name = (request.Name ?? "").Trim();
        if (name.Length is < 2 or > 80) throw OperationException.Invalid("Escribe un nombre de hasta 80 caracteres.");
        var items = (request.Items ?? []).Select(x => new TemplateItem(Title(x.Title), Points(x.Points))).ToArray();
        if (items.Length is 0 or > 20) throw OperationException.Invalid("Agrega entre 1 y 20 actividades.");
        if (items.Select(x => x.Title).Distinct(StringComparer.OrdinalIgnoreCase).Count() != items.Length)
            throw OperationException.Invalid("Cada actividad de la plantilla debe tener un nombre distinto.");
        EnsureTotal(items.Sum(x => x.Points));
        ActivityTemplate template;
        if (id is null)
        {
            if (await db.ActivityTemplates.AnyAsync(x => x.Name == name, ct)) throw new OperationException(409, "template-exists", "Ya existe una plantilla con ese nombre.");
            template = new ActivityTemplate { Id = Guid.NewGuid() };
            db.ActivityTemplates.Add(template);
        }
        else
        {
            template = await db.ActivityTemplates.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw OperationException.NotFound("No encontramos esa plantilla.");
            if (!template.Version.AsSpan().SequenceEqual(Versions.Decode(request.Version))) throw OperationException.Changed();
            if (await db.ActivityTemplates.AnyAsync(x => x.Name == name && x.Id != id, ct)) throw new OperationException(409, "template-exists", "Ya existe una plantilla con ese nombre.");
        }
        template.Name = name;
        template.ItemsJson = JsonSerializer.Serialize(items);
        template.IsActive = request.IsActive;
        template.UpdatedAt = time.GetUtcNow();
        template.UpdatedBy = userId;
        audit.Record(id is null ? "ActivityTemplateCreated" : "ActivityTemplateUpdated", userId, "ActivityTemplate", template.Id, reason);
        await db.SaveChangesAsync(ct);
        return template.Id;
    }

    // ---- helpers

    /// <summary>Sum of points: Missing counts as zero, Excused is left out; all excused means Excused; nothing recorded means no total.</summary>
    internal static (decimal? Total, string? Status) Sum(IReadOnlyCollection<ActivityScore> scores)
    {
        if (scores.Count == 0) return (null, null);
        if (scores.All(x => x.Status == ScoreStatus.Excused)) return (null, ScoreStatus.Excused);
        return (scores.Where(x => x.Status == ScoreStatus.Scored).Sum(x => x.Points ?? 0), ScoreStatus.Scored);
    }

    /// <summary>Writes the period result of the given students from their activity points (only when the course and period has activities).</summary>
    private async Task SyncAsync(Guid userId, Guid courseId, SheetPeriod period, Guid[] studentIds, string reason, CancellationToken ct)
    {
        if (studentIds.Length == 0) return;
        var activityIds = await db.GradeActivities.Where(x => x.RemovedAt == null && x.CourseId == courseId && x.PeriodId == period.Id).Select(x => x.Id).ToArrayAsync(ct);
        var scores = (await db.ActivityScores.Where(x => activityIds.Contains(x.ActivityId) && studentIds.Contains(x.StudentId)).ToArrayAsync(ct))
            .ToLookup(x => x.StudentId);
        var current = await db.PeriodScores.Where(x => x.CourseId == courseId && x.PeriodId == period.Id && studentIds.Contains(x.StudentId))
            .ToDictionaryAsync(x => x.StudentId, ct);
        var now = time.GetUtcNow();
        var text = reason.Length > 500 ? reason[..500] : reason;
        foreach (var studentId in studentIds)
        {
            var (total, status) = Sum(scores[studentId].ToArray());
            current.TryGetValue(studentId, out var score);
            if (status is null)
            {
                // No activity points left: the result is not recorded any more (kept as pending so history is not lost).
                if (score is null || score.Status == ScoreStatus.Missing) continue;
                status = ScoreStatus.Missing;
            }
            var value = status == ScoreStatus.Scored ? Math.Min(PeriodMax, total ?? 0) : (decimal?)null;
            if (score is null)
            {
                score = new PeriodScore
                {
                    Id = Guid.NewGuid(), CourseId = courseId, PeriodId = period.Id, StudentId = studentId, Score = value, Status = status,
                    UpdatedAt = now, UpdatedBy = userId
                };
                db.PeriodScores.Add(score);
                db.ScoreRevisions.Add(new ScoreRevision { Id = Guid.NewGuid(), ScoreId = score.Id, NewScore = value, NewStatus = status, Reason = text, ChangedAt = now, ChangedBy = userId });
                continue;
            }
            if (score.Score == value && score.Status == status) continue;
            db.ScoreRevisions.Add(new ScoreRevision
            {
                Id = Guid.NewGuid(), ScoreId = score.Id, PreviousScore = score.Score, PreviousStatus = score.Status, NewScore = value,
                NewStatus = status, Reason = text, ChangedAt = now, ChangedBy = userId
            });
            score.Score = value;
            score.Status = status;
            score.UpdatedAt = now;
            score.UpdatedBy = userId;
        }
    }

    private bool Write(ActivityScore? score, Guid activityId, Guid studentId, decimal? points, string status, string reason, Guid userId, DateTimeOffset now)
    {
        var value = status == ScoreStatus.Scored ? points : null;
        if (score is null)
        {
            score = new ActivityScore { Id = Guid.NewGuid(), ActivityId = activityId, StudentId = studentId, Points = value, Status = status, UpdatedAt = now, UpdatedBy = userId };
            db.ActivityScores.Add(score);
            db.ActivityScoreRevisions.Add(new ActivityScoreRevision { Id = Guid.NewGuid(), ScoreId = score.Id, NewPoints = value, NewStatus = status, Reason = reason, ChangedAt = now, ChangedBy = userId });
            return true;
        }
        if (score.Points == value && score.Status == status) return false;
        db.ActivityScoreRevisions.Add(new ActivityScoreRevision
        {
            Id = Guid.NewGuid(), ScoreId = score.Id, PreviousPoints = score.Points, PreviousStatus = score.Status, NewPoints = value,
            NewStatus = status, Reason = reason, ChangedAt = now, ChangedBy = userId
        });
        score.Points = value;
        score.Status = status;
        score.UpdatedAt = now;
        score.UpdatedBy = userId;
        return true;
    }

    private static void Validate(string status, decimal? points, GradeActivity activity)
    {
        if (!ScoreStatus.All.Contains(status)) throw OperationException.Invalid("Estado de punteo inválido.");
        if (status == ScoreStatus.Scored && (points is null or < 0 || points > activity.MaxPoints || decimal.Round(points.Value, 2) != points))
            throw OperationException.Invalid($"«{activity.Title}» admite de 0 a {activity.MaxPoints:0.##} puntos, con hasta dos decimales.");
        if (status != ScoreStatus.Scored && points is not null) throw OperationException.Invalid("Solo un punteo registrado lleva valor.");
    }

    private async Task<(CourseView Course, SheetPeriod Period, string Reason)> AuthorizeAsync(Guid userId, Guid courseId, Guid periodId, string? requestReason,
        CancellationToken ct)
    {
        if (!await courses.CanViewAsync(userId, courseId, ct)) throw OperationException.NotFound("No encontramos ese curso.");
        var course = await courses.CourseAsync(courseId, ct);
        var period = (await PeriodsAsync(course.AcademicYearId, ct)).SingleOrDefault(x => x.Id == periodId)
            ?? throw OperationException.NotFound("No encontramos ese período en el ciclo del curso.");
        if (period.Published)
        {
            if (!await IsManagerAsync(userId, ct))
                throw new OperationException(403, "period-published", "El período ya fue publicado. Solicita una corrección a coordinación académica.");
            return (course, period, Reasons.Require(requestReason));
        }
        if (!await courses.CanTeachAsync(userId, courseId, ct)) throw new OperationException(403, "capture-denied", "No tienes asignado este curso para calificar.");
        return (course, period, requestReason?.Trim() is { Length: > 0 } text ? text[..Math.Min(text.Length, 500)] : "Captura");
    }

    /// <summary>Serializable transaction plus the per-course gradebook lock (also used by homework saves that set points).</summary>
    internal async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginAsync(Guid courseId, CancellationToken ct)
    {
        var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @r int;
            EXEC @r = sp_getapplock @Resource={0}, @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=15000;
            IF @r < 0 THROW 51000, 'Gradebook busy', 1;
            """, ["SchoolPlatform.Gradebook." + courseId], ct);
        return tx;
    }

    private async Task<GradeActivity> LoadAsync(Guid courseId, Guid activityId, string? version, CancellationToken ct)
    {
        var activity = await db.GradeActivities.SingleOrDefaultAsync(x => x.RemovedAt == null && x.Id == activityId && x.CourseId == courseId, ct)
            ?? throw OperationException.NotFound("No encontramos esa actividad.");
        if (!activity.Version.AsSpan().SequenceEqual(Versions.Decode(version))) throw OperationException.Changed();
        return activity;
    }

    private async Task<SheetPeriod[]> PeriodsAsync(Guid yearId, CancellationToken ct) =>
        await db.Periods.AsNoTracking().Where(x => x.AcademicYearId == yearId).OrderBy(x => x.Sequence)
            .Select(x => new SheetPeriod(x.Id, x.Sequence, x.Name, x.StartsOn, x.EndsOn, db.GradePublications.Any(p => p.PeriodId == x.Id)))
            .ToArrayAsync(ct);

    private async Task<bool> IsManagerAsync(Guid userId, CancellationToken ct) =>
        await db.Permissions.AnyAsync(x => x.UserId == userId && x.Permission == Permissions.AcademicManage, ct) &&
        await db.Accounts.AnyAsync(x => x.Id == userId && x.IsActive, ct);

    private static string Title(string? value)
    {
        var title = (value ?? "").Trim();
        return title.Length is >= 1 and <= 120 ? title : throw OperationException.Invalid("Escribe el nombre de la actividad (hasta 120 caracteres).");
    }

    private static decimal Points(decimal value) =>
        value is > 0 and <= PeriodMax && decimal.Round(value, 2) == value ? value
            : throw OperationException.Invalid("El punteo de una actividad va de 0.01 a 100, con hasta dos decimales.");

    private static void EnsureTotal(decimal total)
    {
        if (total > PeriodMax)
            throw OperationException.Invalid($"Las actividades del período suman {total:0.##} puntos; el máximo es {PeriodMax:0}.");
    }

    private static TemplateItem[] Items(string json) => JsonSerializer.Deserialize<TemplateItem[]>(json) ?? [];
}
