using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Host.Learning;

public sealed record AssignmentRequest(string Title, string? Instructions, DateTimeOffset DueAt, bool AllowLate, string? Version = null);
public sealed record VersionBody(string Version);
public sealed record AssignmentView(Guid Id, Guid CourseId, string Title, string Instructions, DateTimeOffset DueAt, bool AllowLate,
    string Status, DateTimeOffset? PublishedAt, int Recipients, int Submitted, string Version);
public sealed record SubmissionRow(Guid StudentId, string Name, string Code, Guid? SubmissionId, string? Text, DateTimeOffset? SubmittedAt,
    bool Late, int Attempts, string? Status, string? Feedback, string? Version);
public sealed record AssignmentSubmissions(AssignmentView Assignment, SubmissionRow[] Rows);
public sealed record FeedbackRequest(string Feedback, string Version);
public sealed record SubmitRequest(string Text, string? Version);
public sealed record StudentAssignment(Guid Id, string Course, string Title, string Instructions, DateTimeOffset DueAt, bool AllowLate,
    bool Closed, string? SubmissionStatus, DateTimeOffset? SubmittedAt, bool Late, string? Text, string? Feedback, string? Version, bool CanSubmit);

/// <summary>
/// Course activities and text submissions. Teaching scope comes from assignments; the student submits with their own account.
/// Guardians with the AcademicRecords faculty follow status and feedback. Activities are never blocked by financial clearance.
/// </summary>
public sealed class LearningService(SchoolDbContext db, SchoolAudit audit, TimeProvider time, CourseService courses, PeopleService people)
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
        return new(await ViewAsync(assignment, ct), recipients.Select(r => submissions.TryGetValue(r.Id, out var s)
            ? new SubmissionRow(r.Id, r.Name, r.Code, s.Id, s.Text, s.SubmittedAt, s.SubmittedAt > assignment.DueAt, s.Attempts, s.Status,
                s.Feedback, Versions.Encode(s.Version))
            : new SubmissionRow(r.Id, r.Name, r.Code, null, null, null, false, 0, null, null, null)).ToArray());
    }

    public async Task FeedbackAsync(Guid userId, Guid courseId, Guid submissionId, FeedbackRequest request, CancellationToken ct)
    {
        if (!await courses.CanTeachAsync(userId, courseId, ct)) throw Denied();
        var feedback = (request.Feedback ?? "").Trim();
        if (feedback.Length is < 1 or > 2000) throw OperationException.Invalid("Escribe la retroalimentación (máximo 2000 caracteres).");
        var submission = await db.Submissions.SingleOrDefaultAsync(x => x.Id == submissionId &&
            db.Assignments.Any(a => a.Id == x.AssignmentId && a.CourseId == courseId), ct) ?? throw OperationException.NotFound("No encontramos esa entrega.");
        if (!submission.Version.AsSpan().SequenceEqual(Versions.Decode(request.Version))) throw OperationException.Changed();
        submission.Feedback = feedback;
        submission.FeedbackAt = time.GetUtcNow();
        submission.FeedbackBy = userId;
        submission.Status = SubmissionStatus.Reviewed;
        audit.Record("SubmissionReviewed", userId, "Assignment", submission.AssignmentId);
        await db.SaveChangesAsync(ct);
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
                                select new { Assignment = a, Subject = s.Name }).ToArrayAsync(ct);
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
                s is null ? null : Versions.Encode(s.Version), access.IsSelf && open));
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
        return new(x.Id, x.CourseId, x.Title, x.Instructions, x.DueAt, x.AllowLate, x.Status, x.PublishedAt, recipients.Length, submitted,
            Versions.Encode(x.Version));
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
