using System.Data;
using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Common;

namespace SchoolPlatform.Host.Chat;

public sealed partial class ChatService
{
    public async Task ActivityAsync(Guid actor, string stamp, ChatActivity request, CancellationToken ct)
    {
        if (request.ClientId == Guid.Empty || request.Version is < 1 or > 9007199254740991 || (request.Deliveries?.Length ?? 0) > 100 ||
            request.Deliveries?.Any(r => r is null || r.AcademicYearId == Guid.Empty || r.ConversationId == Guid.Empty || r.ThroughSequence < 0) == true)
            throw OperationException.Invalid("La actualización de actividad no es válida.");
        await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct))
        {
            await LockAsync("presence:" + actor.ToString("N"), ct);
            if (string.IsNullOrEmpty(stamp) || !await db.Accounts.AnyAsync(a => a.Id == actor && a.IsActive && a.PasswordHash != null && a.SecurityStamp == stamp, ct))
                throw new OperationException(401, "session-expired", "Tu sesión venció. Vuelve a ingresar.");
            var now = time.GetUtcNow();
            var slot = await db.ChatPresences.SingleOrDefaultAsync(p => p.UserId == actor && p.ClientId == request.ClientId, ct);
            if (slot is null)
            {
                if (await db.ChatPresences.CountAsync(p => p.UserId == actor, ct) >= 32)
                {
                    var oldest = await db.ChatPresences.Where(p => p.UserId == actor && (p.ActiveUntil == null || p.ActiveUntil <= now))
                        .OrderBy(p => p.UpdatedAt).FirstOrDefaultAsync(ct);
                    if (oldest is null) throw new OperationException(429, "presence-sessions", "Hay demasiadas sesiones activas. Intenta de nuevo en un momento.");
                    db.ChatPresences.Remove(oldest);
                }
                db.ChatPresences.Add(slot = new() { UserId = actor, ClientId = request.ClientId });
            }
            if (request.Version > slot.Version)
            {
                // A late active request must not undo a newer background/lock event from the same client.
                if (request.Active || slot.ActiveUntil > now) slot.LastActiveAt = now;
                slot.ActiveUntil = request.Active ? now.AddSeconds(45) : null;
                slot.SecurityStamp = stamp;
                slot.Version = request.Version;
                slot.UpdatedAt = now;
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        foreach (var receipt in (request.Deliveries ?? []).DistinctBy(r => r.ConversationId).OrderBy(r => r.ConversationId))
        {
            try { await DeliveredAsync(actor, receipt.AcademicYearId, receipt.ConversationId, receipt.ThroughSequence, ct); }
            catch (OperationException e) when (e.Status is 400 or 404) { /* Revocation between inbox fetch and acknowledgment is normal. */ }
        }
    }

    private async Task ReceiptAsync(Guid actor, Guid year, Guid id, long through, bool read, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await LockAsync(id.ToString("N"), ct);
        if (!await Threads(actor, year).AnyAsync(x => x.Id == id, ct)) throw Denied();
        var person = await ActorPerson(actor, ct);
        var conversation = await db.ChatConversations.SingleAsync(x => x.Id == id, ct);
        if (through < 0 || through > conversation.LastSequence) throw OperationException.Invalid("El mensaje confirmado no existe.");
        var teacher = person == conversation.TeacherId;
        var oldDelivered = teacher ? conversation.TeacherDeliveredSequence : conversation.MemberDeliveredSequence;
        var oldRead = teacher ? conversation.TeacherReadSequence : conversation.MemberReadSequence;
        var now = time.GetUtcNow();
        if (through > oldDelivered)
        {
            await db.ChatMessages.Where(m => m.ConversationId == id && m.SenderId != person && m.Sequence > oldDelivered && m.Sequence <= through)
                .ExecuteUpdateAsync(setters => setters.SetProperty(m => m.DeliveredAt, m => m.DeliveredAt ?? (m.SentAt > now ? m.SentAt : now)), ct);
            if (teacher) conversation.TeacherDeliveredSequence = through;
            else conversation.MemberDeliveredSequence = through;
        }
        if (read && through > oldRead)
        {
            await db.ChatMessages.Where(m => m.ConversationId == id && m.SenderId != person && m.Sequence > oldRead && m.Sequence <= through)
                .ExecuteUpdateAsync(setters => setters.SetProperty(m => m.ReadAt, m => m.ReadAt ?? (m.DeliveredAt > now ? m.DeliveredAt : now)), ct);
            if (teacher) conversation.TeacherReadSequence = through;
            else conversation.MemberReadSequence = through;
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
