using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Branding;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Host.Chat;

public sealed partial class ChatService(SchoolDbContext db, SchoolAudit audit, TimeProvider time, IOptions<InstitutionOptions> institution)
{
    public async Task<object> YearsAsync(Guid actor, CancellationToken ct) => await Pairs(actor)
        .Select(x => new { Id = x.YearId, Label = x.Year, x.Status }).Distinct().OrderByDescending(x => x.Label).ToArrayAsync(ct);
    // Every list/read/write resolves this SQL scope again. Account names/role names never grant chat access.
    private sealed class Pair
    {
        public Guid YearId { get; init; }
        public string Year { get; init; } = "";
        public string Status { get; init; } = "";
        public Guid StudentId { get; init; }
        public string Student { get; init; } = "";
        public Guid TeacherId { get; init; }
        public string Teacher { get; init; } = "";
        public Guid MemberId { get; init; }
        public string Member { get; init; } = "";
        public Guid TeacherAccount { get; init; }
        public Guid MemberAccount { get; init; }
    }

    private IQueryable<Pair> Pairs(Guid actor, Guid? yearId = null)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(),
            TimeZoneInfo.FindSystemTimeZoneById(institution.Value.Locale.TimeZone)).DateTime);
        var roster = from y in db.Years
                     where (yearId == null || y.Id == yearId) && y.Status != YearStatus.Draft
                     let date = today < y.StartsOn ? y.StartsOn : today > y.EndsOn ? y.EndsOn : today
                     from c in db.Courses where c.AcademicYearId == y.Id
                     from a in db.TeachingAssignments where a.CourseId == c.Id && a.StartsOn <= date && (a.EndsOn == null || a.EndsOn >= date)
                     from p in db.Placements where p.GradeLevelId == c.GradeLevelId && (c.SectionId == null || p.SectionId == c.SectionId) &&
                         p.StartsOn <= date && (p.EndsOn == null || p.EndsOn > date)
                     from e in db.Enrollments where e.Id == p.EnrollmentId && e.AcademicYearId == y.Id &&
                         e.Status != EnrollmentStatus.Cancelled && e.EnrolledOn <= date && (e.WithdrawnOn == null || e.WithdrawnOn > date)
                     from student in db.Persons where student.Id == e.StudentId && student.Status == PersonStatus.Active
                     from teacher in db.Persons where teacher.Id == a.StaffId && teacher.Status == PersonStatus.Active
                     select new { y, e.StudentId, Student = (student.PreferredName ?? student.GivenNames) + " " + student.FamilyNames,
                         TeacherId = teacher.Id, Teacher = (teacher.PreferredName ?? teacher.GivenNames) + " " + teacher.FamilyNames, date };
        var members = roster.Select(x => new { x.y.Id, Year = x.y.Label, x.y.Status, x.StudentId, x.Student, x.TeacherId, x.Teacher,
            MemberId = x.StudentId, Member = x.Student });
        var families = from r in roster
                       from relationship in db.Relationships where relationship.StudentId == r.StudentId && relationship.EndedAt == null && relationship.StartsOn <= today &&
                           db.RelationshipFaculties.Any(f => f.RelationshipId == relationship.Id && f.Faculty == Faculties.Communications && f.RevokedAt == null)
                       from member in db.Persons where member.Id == relationship.GuardianId && member.Status == PersonStatus.Active
                       select new { r.y.Id, Year = r.y.Label, r.y.Status, r.StudentId, r.Student, r.TeacherId, r.Teacher,
                           MemberId = member.Id, Member = (member.PreferredName ?? member.GivenNames) + " " + member.FamilyNames };
        return (from r in members.Union(families)
                where r.TeacherId != r.MemberId
                from t in db.AccountLinks where t.PersonId == r.TeacherId && t.UnlinkedAt == null
                from m in db.AccountLinks where m.PersonId == r.MemberId && m.UnlinkedAt == null
                where (t.UserId == actor || m.UserId == actor) &&
                    db.Accounts.Any(u => u.Id == t.UserId && u.IsActive && u.PasswordHash != null) &&
                    db.Accounts.Any(u => u.Id == m.UserId && u.IsActive && u.PasswordHash != null)
                select new Pair { YearId = r.Id, Year = r.Year, Status = r.Status, StudentId = r.StudentId, Student = r.Student,
                    TeacherId = r.TeacherId, Teacher = r.Teacher, MemberId = r.MemberId, Member = r.Member,
                    TeacherAccount = t.UserId, MemberAccount = m.UserId }).Distinct();
    }

    public async Task<PagedResult<ChatContact>> ContactsAsync(Guid actor, Guid yearId, string? search, int page, CancellationToken ct)
    {
        page = Math.Max(1, page);
        var query = Pairs(actor, yearId);
        var term = search?.Trim() ?? "";
        if (term.Length > 100) throw OperationException.Invalid("La búsqueda admite hasta 100 caracteres.");
        if (term.Length > 0) query = query.Where(x => x.Student.Contains(term) ||
            (x.TeacherAccount == actor ? x.Member : x.Teacher).Contains(term));
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.Student).ThenBy(x => x.Teacher).ThenBy(x => x.Member).Skip((page - 1) * 40).Take(40).ToArrayAsync(ct);
        return new(rows.Select(x => new ChatContact(x.StudentId, x.TeacherId, x.MemberId,
            x.TeacherAccount == actor ? x.Member : x.Teacher,
            x.TeacherAccount != actor ? "Docente" : x.MemberId == x.StudentId ? "Estudiante" : "Encargado", x.Student)).ToArray(), total, page, 40);
    }

    private IQueryable<ChatThread> Threads(Guid actor, Guid? year = null)
    {
        var now = time.GetUtcNow();
        return
        from c in db.ChatConversations.AsNoTracking()
        join p in Pairs(actor, year) on new { Y = c.AcademicYearId, S = c.StudentId, T = c.TeacherId, M = c.MemberId }
            equals new { Y = p.YearId, S = p.StudentId, T = p.TeacherId, M = p.MemberId }
        let mine = p.TeacherAccount == actor ? c.TeacherId : c.MemberId
        let read = p.TeacherAccount == actor ? c.TeacherReadSequence : c.MemberReadSequence
        let peer = p.TeacherAccount == actor ? p.MemberAccount : p.TeacherAccount
        select new ChatThread { Id = c.Id, AcademicYearId = c.AcademicYearId, Year = p.Year, Name = p.TeacherAccount == actor ? p.Member : p.Teacher,
            Role = p.TeacherAccount != actor ? "Docente" : c.MemberId == c.StudentId ? "Estudiante" : "Encargado", Student = p.Student,
            Preview = db.ChatMessages.Where(m => m.ConversationId == c.Id).OrderByDescending(m => m.Sequence).Select(m => m.Body.Length > 120 ? m.Body.Substring(0, 120) : m.Body).FirstOrDefault() ?? "",
            UpdatedAt = c.UpdatedAt, Unread = db.ChatMessages.Count(m => m.ConversationId == c.Id && m.SenderId != mine && m.Sequence > read),
            LastSequence = c.LastSequence, PeerReadSequence = p.TeacherAccount == actor ? c.MemberReadSequence : c.TeacherReadSequence,
            PeerDeliveredSequence = p.TeacherAccount == actor ? c.MemberDeliveredSequence : c.TeacherDeliveredSequence,
            DeliveredSequence = p.TeacherAccount == actor ? c.TeacherDeliveredSequence : c.MemberDeliveredSequence,
            ServerNow = now,
            PeerActiveUntil = db.ChatPresences.Where(s => s.UserId == peer &&
                db.Accounts.Any(a => a.Id == peer && a.SecurityStamp == s.SecurityStamp)).Max(s => s.ActiveUntil),
            PeerLastActiveAt = db.ChatPresences.Where(s => s.UserId == peer).Max(s => s.LastActiveAt),
            CanSend = p.Status == YearStatus.EnrollmentOpen || p.Status == YearStatus.Active };
    }

    public async Task<ChatThread[]> ListAsync(Guid actor, Guid year, CancellationToken ct) =>
        await Threads(actor, year).OrderByDescending(x => x.UpdatedAt).Take(200).ToArrayAsync(ct);

    public async Task<PagedResult<ChatThread>> BrowseAsync(Guid actor, Guid year, string? search, int page, CancellationToken ct)
    {
        var term = search?.Trim() ?? "";
        if (term.Length > 100 || page is < 1 or > 100000) throw OperationException.Invalid("La búsqueda de conversaciones no es válida.");
        var query = Threads(actor, year);
        if (term.Length > 0) query = query.Where(x => x.Name.Contains(term) || x.Student.Contains(term));
        return new(await query.OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id).Skip((page - 1) * 40).Take(40).ToArrayAsync(ct), await query.CountAsync(ct), page, 40);
    }

    public async Task<ChatSummary> SummaryAsync(Guid actor, CancellationToken ct)
    {
        var person = await db.AccountLinks.Where(x => x.UserId == actor && x.UnlinkedAt == null).Select(x => (Guid?)x.PersonId).SingleOrDefaultAsync(ct);
        if (person is null) return new(false, 0, 0, "", []);
        var unread = await Threads(actor).Where(x => x.Unread > 0).Select(x => new { x.Id, x.Unread,
            Incoming = db.ChatMessages.Where(m => m.ConversationId == x.Id && m.SenderId != person).Max(m => (long?)m.Sequence) ?? 0 }).ToArrayAsync(ct);
        var deliveries = await Threads(actor).Where(t => db.ChatMessages.Any(m => m.ConversationId == t.Id && m.SenderId != person && m.Sequence > t.DeliveredSequence))
            .OrderBy(t => t.UpdatedAt).Take(100).Select(t => new ChatDelivery(t.AcademicYearId, t.Id,
                db.ChatMessages.Where(m => m.ConversationId == t.Id && m.SenderId != person).Max(m => m.Sequence))).ToArrayAsync(ct);
        var revision = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
            string.Join('|', unread.OrderBy(x => x.Id).Select(x => $"{x.Id:N}:{x.Incoming}")))));
        return new(await Pairs(actor).AnyAsync(ct), unread.Sum(x => x.Unread), unread.Length, revision, deliveries);
    }

    public async Task<Guid> StartAsync(Guid actor, Guid year, StartChat request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await LockAsync($"start:{year:N}:{request.StudentId:N}:{request.TeacherId:N}:{request.MemberId:N}", ct);
        var pair = await Pairs(actor, year).FirstOrDefaultAsync(x => x.StudentId == request.StudentId && x.TeacherId == request.TeacherId && x.MemberId == request.MemberId, ct)
            ?? throw Denied();
        if (pair.Status != YearStatus.Active && pair.Status != YearStatus.EnrollmentOpen) throw OperationException.Invalid("El ciclo está cerrado. Puedes consultar las conversaciones existentes.");
        var existing = await db.ChatConversations.SingleOrDefaultAsync(x => x.AcademicYearId == year && x.StudentId == request.StudentId &&
            x.TeacherId == request.TeacherId && x.MemberId == request.MemberId, ct);
        if (existing is not null) { await tx.CommitAsync(ct); return existing.Id; }
        var conversation = new ChatConversation { Id = Guid.NewGuid(), AcademicYearId = year, StudentId = request.StudentId,
            TeacherId = request.TeacherId, MemberId = request.MemberId, CreatedAt = time.GetUtcNow(), UpdatedAt = time.GetUtcNow() };
        db.ChatConversations.Add(conversation);
        audit.Record("ChatStarted", actor, "Chat", conversation.Id);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return conversation.Id;
    }

    public async Task<ChatPage> MessagesAsync(Guid actor, Guid year, Guid id, long? before, long? after, CancellationToken ct,
        long knownRead = 0, long knownDelivered = 0, long receiptFrom = 1)
    {
        if (before is <= 0 || after is < 0 || before is not null && after is not null || knownRead < 0 || knownDelivered < 0 || receiptFrom < 1)
            throw OperationException.Invalid("El cursor de mensajes no es válido.");
        var thread = await Threads(actor, year).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Denied();
        var person = await ActorPerson(actor, ct);
        var query = db.ChatMessages.AsNoTracking().Where(x => x.ConversationId == id && (before == null || x.Sequence < before) && (after == null || x.Sequence > after));
        var rows = after is null ? await query.OrderByDescending(x => x.Sequence).Take(51).ToArrayAsync(ct)
            : await query.OrderBy(x => x.Sequence).Take(51).ToArrayAsync(ct);
        var receipts = after is null ? [] : await db.ChatMessages.AsNoTracking().Where(m => m.ConversationId == id && m.SenderId == person &&
            m.Sequence >= receiptFrom && m.Sequence <= after &&
            ((m.ReadAt != null && m.Sequence > knownRead) || (m.DeliveredAt != null && m.Sequence > knownDelivered)))
            .OrderBy(m => m.Sequence).Select(m => new ChatReceipt(m.Sequence, m.DeliveredAt, m.ReadAt)).ToArrayAsync(ct);
        var sequences = rows.Select(r => r.Sequence).ToArray();
        var files = await db.ChatAttachments.AsNoTracking().Where(a => a.ConversationId == id && sequences.Contains(a.Sequence))
            .ToDictionaryAsync(a => a.Sequence, a => new ChatAttachmentView(a.Id, a.FileName, a.ContentType, a.Size), ct);
        return new(thread, rows.Take(50).OrderBy(x => x.Sequence).Select(x => View(x, person) with { Attachment = files.GetValueOrDefault(x.Sequence) }).ToArray(),
            rows.Length > 50, receipts);
    }

    /// <summary>Whether the account takes part in the conversation (attachment downloads use the same rule as messages).</summary>
    public async Task<bool> CanReadAsync(Guid actor, Guid year, Guid id, CancellationToken ct) =>
        await Threads(actor, year).AnyAsync(x => x.Id == id, ct);

    /// <summary>
    /// Sends a message carrying one file, with the same authorization, lock and retry rules as text messages. Without a caption
    /// the message text names the file, so older clients still show it. Returns whether this attempt stored the file.
    /// </summary>
    internal async Task<(ChatMessageView View, bool Stored)> SendWithAttachmentAsync(Guid actor, Guid year, Guid id, Guid clientMessageId, string? caption,
        ChatAttachment attachment, CancellationToken ct)
    {
        var text = (caption ?? "").Replace("\r\n", "\n").Trim();
        var body = text.Length > 0 ? text : "📎 " + attachment.FileName;
        if (clientMessageId == Guid.Empty || body.Length > 4000 || body.Any(c => char.IsControl(c) && c != '\n' && c != '\t'))
            throw OperationException.Invalid("El mensaje del archivo admite hasta 4000 caracteres.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await LockAsync(id.ToString("N"), ct);
        var thread = await Threads(actor, year).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Denied();
        if (!thread.CanSend) throw OperationException.Invalid("El ciclo está cerrado. Esta conversación es de consulta.");
        var person = await ActorPerson(actor, ct);
        var duplicate = await db.ChatMessages.SingleOrDefaultAsync(x => x.ConversationId == id && x.SenderId == person && x.ClientMessageId == clientMessageId, ct);
        if (duplicate is not null)
        {
            var previous = await db.ChatAttachments.AsNoTracking().SingleOrDefaultAsync(a => a.ConversationId == id && a.Sequence == duplicate.Sequence, ct);
            if (previous is null || previous.Sha256 != attachment.Sha256) throw new OperationException(409, "message-key-reused", "Ese intento ya pertenece a otro mensaje.");
            await tx.CommitAsync(ct);
            return (View(duplicate, person) with { Attachment = new(previous.Id, previous.FileName, previous.ContentType, previous.Size) }, false);
        }
        var conversation = await db.ChatConversations.SingleAsync(x => x.Id == id, ct);
        var now = time.GetUtcNow();
        var message = new ChatMessage { ConversationId = id, Sequence = ++conversation.LastSequence, SenderId = person, ClientMessageId = clientMessageId, Body = body, SentAt = now };
        conversation.UpdatedAt = now;
        db.ChatMessages.Add(message);
        attachment.ConversationId = id;
        attachment.Sequence = message.Sequence;
        attachment.UploadedAt = now;
        attachment.UploadedBy = actor;
        db.ChatAttachments.Add(attachment);
        audit.Record("ChatAttachmentSent", actor, "Chat", id, detail: $"{message.Sequence}:{Path.GetExtension(attachment.FileName).ToLowerInvariant()}");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return (View(message, person) with { Attachment = new(attachment.Id, attachment.FileName, attachment.ContentType, attachment.Size) }, true);
    }

    public async Task<ChatMessageView> SendAsync(Guid actor, Guid year, Guid id, SendChat request, CancellationToken ct)
    {
        var body = (request.Body ?? "").Replace("\r\n", "\n").Trim();
        if (request.ClientMessageId == Guid.Empty || body.Length is < 1 or > 4000 || body.Any(c => char.IsControl(c) && c != '\n' && c != '\t'))
            throw OperationException.Invalid("Escribe un mensaje de 1 a 4000 caracteres.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await LockAsync(id.ToString("N"), ct);
        var thread = await Threads(actor, year).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Denied();
        if (!thread.CanSend) throw OperationException.Invalid("El ciclo está cerrado. Esta conversación es de consulta.");
        var person = await ActorPerson(actor, ct);
        var duplicate = await db.ChatMessages.SingleOrDefaultAsync(x => x.ConversationId == id && x.SenderId == person && x.ClientMessageId == request.ClientMessageId, ct);
        if (duplicate is not null)
        {
            if (duplicate.Body != body) throw new OperationException(409, "message-key-reused", "Ese intento ya pertenece a otro mensaje.");
            await tx.CommitAsync(ct);
            return View(duplicate, person);
        }
        var conversation = await db.ChatConversations.SingleAsync(x => x.Id == id, ct);
        var message = new ChatMessage { ConversationId = id, Sequence = ++conversation.LastSequence, SenderId = person,
            ClientMessageId = request.ClientMessageId, Body = body, SentAt = time.GetUtcNow() };
        conversation.UpdatedAt = message.SentAt;
        db.ChatMessages.Add(message);
        audit.Record("ChatMessageSent", actor, "Chat", id, detail: message.Sequence.ToString());
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return View(message, person);
    }

    public Task ReadAsync(Guid actor, Guid year, Guid id, long through, CancellationToken ct) => ReceiptAsync(actor, year, id, through, true, ct);
    public Task DeliveredAsync(Guid actor, Guid year, Guid id, long through, CancellationToken ct) => ReceiptAsync(actor, year, id, through, false, ct);

    private async Task<Guid> ActorPerson(Guid actor, CancellationToken ct) =>
        await db.AccountLinks.Where(x => x.UserId == actor && x.UnlinkedAt == null).Select(x => x.PersonId).SingleAsync(ct);
    private Task LockAsync(string key, CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource={"SchoolPlatform.Chat:" + key}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000; IF @result < 0 THROW 51000, 'Chat lock unavailable', 1;", ct);
    private static OperationException Denied() => OperationException.NotFound("La conversación no está disponible o ya no tienes acceso.");
    private static ChatMessageView View(ChatMessage m, Guid person) => new(m.Sequence, m.ClientMessageId, m.Body, m.SentAt, m.SenderId == person, m.DeliveredAt, m.ReadAt);
}
