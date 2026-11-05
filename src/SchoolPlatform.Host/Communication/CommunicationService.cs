using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Host.Communication;

public sealed record AnnouncementRequest(string Title, string Body, string AudienceKind, Guid? AcademicYearId, Guid? AudienceId,
    bool ForStudents, bool ForGuardians, bool ForStaff, string? Version = null);
public sealed record AnnouncementView(Guid Id, string Title, string Body, string AudienceKind, Guid? AcademicYearId, Guid? AudienceId,
    string Audience, bool ForStudents, bool ForGuardians, bool ForStaff, string Status, DateTimeOffset CreatedAt, string Author,
    DateTimeOffset? PublishedAt, int Reads, bool CanEdit, string Version);
public sealed record FeedItem(Guid Id, string Title, string Body, string Audience, DateTimeOffset PublishedAt, string Author, bool Read,
    string[] Students);
public sealed record AuthoringOptions(bool CanPublishSchool, CourseView[] Courses);

/// <summary>
/// In-app announcements. School and grade notices require communication.publish; a course notice can also be written by
/// its assigned teacher. Recipients are resolved on every read from current placements, relationships with the
/// Communications faculty and teaching assignments. No message leaves the platform.
/// </summary>
public sealed class CommunicationService(SchoolDbContext db, SchoolAudit audit, TimeProvider time, CourseService courses)
{
    public async Task<AuthoringOptions> OptionsAsync(Guid userId, CancellationToken ct)
    {
        var canPublish = await HasAsync(userId, Permissions.CommunicationPublish, ct);
        return new(canPublish, await courses.MyCoursesAsync(userId, ct));
    }

    public async Task<AnnouncementView[]> AuthoredAsync(Guid userId, CancellationToken ct)
    {
        var canPublish = await HasAsync(userId, Permissions.CommunicationPublish, ct);
        var myCourses = (await courses.MyCoursesAsync(userId, ct)).Select(x => x.Id).ToArray();
        var items = await db.Announcements.AsNoTracking()
            .Where(x => canPublish || x.CreatedBy == userId || (x.AudienceKind == AudienceKinds.Course && myCourses.Contains(x.AudienceId!.Value)))
            .OrderByDescending(x => x.CreatedAt).Take(200).ToArrayAsync(ct);
        var result = new List<AnnouncementView>();
        foreach (var item in items) result.Add(await ViewAsync(item, canPublish || CanEditCourse(item, myCourses), ct));
        return result.ToArray();
    }

    public async Task<Guid> SaveAsync(Guid userId, Guid? id, AnnouncementRequest request, CancellationToken ct)
    {
        var title = (request.Title ?? "").Trim();
        var body = (request.Body ?? "").Trim();
        if (title.Length is < 1 or > 160) throw OperationException.Invalid("Escribe un título de hasta 160 caracteres.");
        if (body.Length is < 1 or > 4000) throw OperationException.Invalid("Escribe el aviso (máximo 4000 caracteres).");
        if (!request.ForStudents && !request.ForGuardians && !request.ForStaff) throw OperationException.Invalid("Elige al menos un grupo de destinatarios.");
        await AuthorizeAudienceAsync(userId, request, ct);
        Announcement announcement;
        if (id is null)
        {
            announcement = new Announcement { Id = Guid.NewGuid(), CreatedAt = time.GetUtcNow(), CreatedBy = userId };
            db.Announcements.Add(announcement);
        }
        else
        {
            announcement = await LoadEditableAsync(userId, id.Value, request.Version, ct);
            if (announcement.Status != AnnouncementStatus.Draft)
                throw new OperationException(409, "announcement-published", "Un aviso publicado no se edita; archívalo y publica uno nuevo.");
        }
        announcement.Title = title;
        announcement.Body = body;
        announcement.AudienceKind = request.AudienceKind;
        announcement.AcademicYearId = request.AudienceKind == AudienceKinds.School ? null : request.AcademicYearId;
        announcement.AudienceId = request.AudienceKind == AudienceKinds.School ? null : request.AudienceId;
        announcement.ForStudents = request.ForStudents;
        announcement.ForGuardians = request.ForGuardians;
        announcement.ForStaff = request.ForStaff && request.AudienceKind == AudienceKinds.School;
        audit.Record(id is null ? "AnnouncementCreated" : "AnnouncementUpdated", userId, "Announcement", announcement.Id, detail: request.AudienceKind);
        await db.SaveChangesAsync(ct);
        return announcement.Id;
    }

    public async Task SetStatusAsync(Guid userId, Guid id, string status, string version, CancellationToken ct)
    {
        var announcement = await LoadEditableAsync(userId, id, version, ct);
        var valid = (announcement.Status, status) is (AnnouncementStatus.Draft, AnnouncementStatus.Published)
            or (AnnouncementStatus.Published, AnnouncementStatus.Archived) or (AnnouncementStatus.Draft, AnnouncementStatus.Archived);
        if (!valid) throw new OperationException(409, "invalid-transition", "El aviso solo avanza: borrador, publicado y archivado.");
        announcement.Status = status;
        if (status == AnnouncementStatus.Published)
        {
            announcement.PublishedAt = time.GetUtcNow();
            announcement.PublishedBy = userId;
        }
        audit.Record(status == AnnouncementStatus.Published ? "AnnouncementPublished" : "AnnouncementArchived", userId, "Announcement", id);
        await db.SaveChangesAsync(ct);
    }

    public async Task<FeedItem[]> FeedAsync(Guid userId, CancellationToken ct)
    {
        var context = await ContextAsync(userId, ct);
        if (context is null) return [];
        var published = await db.Announcements.AsNoTracking().Where(x => x.Status == AnnouncementStatus.Published)
            .OrderByDescending(x => x.PublishedAt).Take(300).ToArrayAsync(ct);
        var reads = (await db.AnnouncementReads.AsNoTracking().Where(x => x.UserId == userId).Select(x => x.AnnouncementId).ToArrayAsync(ct)).ToHashSet();
        var result = new List<FeedItem>();
        foreach (var item in published)
        {
            var students = Matches(item, context);
            if (students is null) continue;
            result.Add(new(item.Id, item.Title, item.Body, await AudienceLabelAsync(item, ct), item.PublishedAt!.Value,
                await AuthorAsync(item.PublishedBy ?? item.CreatedBy, ct), reads.Contains(item.Id), students));
        }
        return result.ToArray();
    }

    public async Task MarkReadAsync(Guid userId, Guid id, CancellationToken ct)
    {
        if (!(await FeedAsync(userId, ct)).Any(x => x.Id == id)) throw OperationException.NotFound("No encontramos ese aviso.");
        if (await db.AnnouncementReads.AnyAsync(x => x.AnnouncementId == id && x.UserId == userId, ct)) return;
        db.AnnouncementReads.Add(new AnnouncementRead { AnnouncementId = id, UserId = userId, ReadAt = time.GetUtcNow() });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { /* concurrent read receipt from another tab */ }
    }

    // ---- recipients

    private sealed record StudentContext(Guid StudentId, string Name, Guid YearId, Guid GradeId, Guid[] CourseIds);
    private sealed record ReaderContext(StudentContext? Self, StudentContext[] Wards, bool IsStaff, Guid[] TeachingCourses);

    private async Task<ReaderContext?> ContextAsync(Guid userId, CancellationToken ct)
    {
        var personId = await db.AccountLinks.AsNoTracking().Where(l => l.UserId == userId && l.UnlinkedAt == null)
            .Where(l => db.Persons.Any(p => p.Id == l.PersonId && p.Status == PersonStatus.Active))
            .Select(l => (Guid?)l.PersonId).SingleOrDefaultAsync(ct);
        if (personId is null) return null;
        var isStudent = await db.Students.AnyAsync(x => x.PersonId == personId, ct);
        var isStaff = await db.Staff.AnyAsync(x => x.PersonId == personId, ct);
        var wards = await db.Relationships.AsNoTracking()
            .Where(r => r.GuardianId == personId && r.EndedAt == null &&
                r.Faculties.Any(f => f.RevokedAt == null && f.Faculty == Faculties.Communications))
            .Select(r => r.StudentId).ToArrayAsync(ct);
        var self = isStudent ? await StudentContextAsync(personId.Value, ct) : null;
        var wardContexts = new List<StudentContext>();
        foreach (var ward in wards)
            if (await StudentContextAsync(ward, ct) is { } c) wardContexts.Add(c);
        var teaching = isStaff ? (await courses.MyCoursesAsync(userId, ct)).Select(x => x.Id).ToArray() : [];
        return new(self, wardContexts.ToArray(), isStaff, teaching);
    }

    private async Task<StudentContext?> StudentContextAsync(Guid studentId, CancellationToken ct)
    {
        var row = await (from e in db.Enrollments.AsNoTracking()
                         where e.StudentId == studentId && e.Status == EnrollmentStatus.Active
                         join p in db.Placements on e.Id equals p.EnrollmentId
                         where p.EndsOn == null
                         join y in db.Years on e.AcademicYearId equals y.Id
                         join person in db.Persons on e.StudentId equals person.Id
                         orderby y.IsDefault descending, y.StartsOn descending
                         select new { e.AcademicYearId, p.GradeLevelId, p.SectionId, person.GivenNames, person.FamilyNames, person.PreferredName })
            .FirstOrDefaultAsync(ct);
        if (row is null) return null;
        var courseIds = await db.Courses.AsNoTracking().Where(c => c.AcademicYearId == row.AcademicYearId && c.GradeLevelId == row.GradeLevelId &&
            (c.SectionId == null || c.SectionId == row.SectionId)).Select(c => c.Id).ToArrayAsync(ct);
        return new(studentId, PeopleService.Display(row.GivenNames, row.FamilyNames, row.PreferredName), row.AcademicYearId, row.GradeLevelId, courseIds);
    }

    /// <summary>Null when the reader is not a recipient; otherwise the related students' names (empty for personal notices).</summary>
    private static string[]? Matches(Announcement item, ReaderContext reader)
    {
        bool Covers(StudentContext s) => item.AudienceKind switch
        {
            AudienceKinds.School => true,
            AudienceKinds.Grade => item.AcademicYearId == s.YearId && item.AudienceId == s.GradeId,
            AudienceKinds.Course => item.AudienceId is { } course && s.CourseIds.Contains(course),
            _ => false
        };
        var names = new List<string>();
        var matched = false;
        if (item.ForStudents && reader.Self is { } self && Covers(self)) matched = true;
        if (item.ForGuardians)
            foreach (var ward in reader.Wards.Where(Covers))
            {
                matched = true;
                names.Add(ward.Name);
            }
        if (item.ForStaff && reader.IsStaff && item.AudienceKind == AudienceKinds.School) matched = true;
        if (item.AudienceKind == AudienceKinds.Course && item.AudienceId is { } id && reader.TeachingCourses.Contains(id)) matched = true;
        return matched ? names.Distinct().ToArray() : null;
    }

    // ---- helpers

    private async Task AuthorizeAudienceAsync(Guid userId, AnnouncementRequest request, CancellationToken ct)
    {
        switch (request.AudienceKind)
        {
            case AudienceKinds.School:
                await audit.RequireAsync(userId, ct, Permissions.CommunicationPublish);
                break;
            case AudienceKinds.Grade:
                await audit.RequireAsync(userId, ct, Permissions.CommunicationPublish);
                if (request.AcademicYearId is null || request.AudienceId is null ||
                    !await db.Years.AnyAsync(y => y.Id == request.AcademicYearId, ct) || !await db.Grades.AnyAsync(g => g.Id == request.AudienceId, ct))
                    throw OperationException.Invalid("Selecciona el ciclo y el grado.");
                break;
            case AudienceKinds.Course:
                if (request.AudienceId is null) throw OperationException.Invalid("Selecciona el curso.");
                var yearId = await db.Courses.Where(c => c.Id == request.AudienceId).Select(c => (Guid?)c.AcademicYearId).SingleOrDefaultAsync(ct)
                    ?? throw OperationException.Invalid("Selecciona un curso existente.");
                if (request.AcademicYearId is not null && request.AcademicYearId != yearId) throw OperationException.Invalid("El curso no pertenece a ese ciclo.");
                if (!await HasAsync(userId, Permissions.CommunicationPublish, ct) && !await courses.CanTeachAsync(userId, request.AudienceId.Value, ct))
                    throw new OperationException(403, "teaching-denied", "Solo puedes publicar avisos en tus cursos.");
                break;
            default:
                throw OperationException.Invalid("Selecciona la audiencia del aviso.");
        }
    }

    private async Task<Announcement> LoadEditableAsync(Guid userId, Guid id, string? version, CancellationToken ct)
    {
        var announcement = await db.Announcements.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw OperationException.NotFound("No encontramos ese aviso.");
        if (!announcement.Version.AsSpan().SequenceEqual(Versions.Decode(version))) throw OperationException.Changed();
        var canPublish = await HasAsync(userId, Permissions.CommunicationPublish, ct);
        var canCourse = announcement.AudienceKind == AudienceKinds.Course && announcement.AudienceId is { } course &&
            await courses.CanTeachAsync(userId, course, ct);
        if (!canPublish && !canCourse) throw new OperationException(403, "permission-denied", "No puedes modificar este aviso.");
        return announcement;
    }

    private static bool CanEditCourse(Announcement item, Guid[] myCourses) =>
        item.AudienceKind == AudienceKinds.Course && item.AudienceId is { } id && myCourses.Contains(id);

    private async Task<bool> HasAsync(Guid userId, string permission, CancellationToken ct) =>
        await db.Permissions.AnyAsync(x => x.UserId == userId && x.Permission == permission, ct) &&
        await db.Accounts.AnyAsync(x => x.Id == userId && x.IsActive, ct);

    private async Task<AnnouncementView> ViewAsync(Announcement x, bool canEdit, CancellationToken ct) =>
        new(x.Id, x.Title, x.Body, x.AudienceKind, x.AcademicYearId, x.AudienceId, await AudienceLabelAsync(x, ct), x.ForStudents, x.ForGuardians,
            x.ForStaff, x.Status, x.CreatedAt, await AuthorAsync(x.CreatedBy, ct), x.PublishedAt,
            await db.AnnouncementReads.CountAsync(r => r.AnnouncementId == x.Id, ct), canEdit, Versions.Encode(x.Version));

    private async Task<string> AudienceLabelAsync(Announcement x, CancellationToken ct) => x.AudienceKind switch
    {
        AudienceKinds.Grade => await db.Grades.Where(g => g.Id == x.AudienceId).Select(g => g.Name).FirstOrDefaultAsync(ct) ?? "Grado",
        AudienceKinds.Course => await (from c in db.Courses
                                       where c.Id == x.AudienceId
                                       join s in db.Subjects on c.SubjectId equals s.Id
                                       join g in db.Grades on c.GradeLevelId equals g.Id
                                       select s.Name + " · " + g.Name).FirstOrDefaultAsync(ct) ?? "Curso",
        _ => "Todo el colegio"
    };

    // Shows the author's person name when the account represents someone; otherwise a neutral institutional label.
    private async Task<string> AuthorAsync(Guid userId, CancellationToken ct) =>
        await (from l in db.AccountLinks
               where l.UserId == userId && l.UnlinkedAt == null
               join p in db.Persons on l.PersonId equals p.Id
               select (p.PreferredName ?? p.GivenNames) + " " + p.FamilyNames).FirstOrDefaultAsync(ct)
        ?? "Administración del colegio"; // internal account names are never shown to families
}
