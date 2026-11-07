namespace SchoolPlatform.Host.Chat;

// A private, student-scoped teacher <-> student/guardian pair. No arbitrary participants or groups.
public sealed class ChatConversation
{
    public Guid Id { get; set; }
    public Guid AcademicYearId { get; set; }
    public Guid StudentId { get; set; }
    public Guid TeacherId { get; set; }
    public Guid MemberId { get; set; }
    public long LastSequence { get; set; }
    public long TeacherReadSequence { get; set; }
    public long MemberReadSequence { get; set; }
    public long TeacherDeliveredSequence { get; set; }
    public long MemberDeliveredSequence { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ChatMessage
{
    public Guid ConversationId { get; set; }
    public long Sequence { get; set; }
    public Guid SenderId { get; set; }
    public Guid ClientMessageId { get; set; }
    public string Body { get; set; } = "";
    public DateTimeOffset SentAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}

// Ephemeral foreground leases, scoped to one signed-in account and one browser tab/app process.
public sealed class ChatPresence
{
    public Guid UserId { get; set; }
    public Guid ClientId { get; set; }
    public long Version { get; set; }
    public string SecurityStamp { get; set; } = "";
    public DateTimeOffset? LastActiveAt { get; set; }
    public DateTimeOffset? ActiveUntil { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed record StartChat(Guid StudentId, Guid TeacherId, Guid MemberId);
public sealed record SendChat(Guid ClientMessageId, string Body);
public sealed record ReadChat(long ThroughSequence);
public sealed record ChatDelivery(Guid AcademicYearId, Guid ConversationId, long ThroughSequence);
public sealed record ChatActivity(Guid ClientId, long Version, bool Active, ChatDelivery[]? Deliveries = null);
public sealed record ChatReceipt(long Sequence, DateTimeOffset? DeliveredAt, DateTimeOffset? ReadAt);
public sealed record ChatContact(Guid StudentId, Guid TeacherId, Guid MemberId, string Name, string Role, string Student);
public sealed class ChatThread
{
    public Guid Id { get; init; }
    public Guid AcademicYearId { get; init; }
    public string Year { get; init; } = "";
    public string Name { get; init; } = "";
    public string Role { get; init; } = "";
    public string Student { get; init; } = "";
    public string Preview { get; init; } = "";
    public DateTimeOffset UpdatedAt { get; init; }
    public int Unread { get; init; }
    public long LastSequence { get; init; }
    public long PeerReadSequence { get; init; }
    public long PeerDeliveredSequence { get; init; }
    public long DeliveredSequence { get; init; }
    public DateTimeOffset ServerNow { get; init; }
    public DateTimeOffset? PeerActiveUntil { get; init; }
    public DateTimeOffset? PeerLastActiveAt { get; init; }
    public bool CanSend { get; init; }
}
public sealed record ChatMessageView(long Sequence, Guid ClientMessageId, string Body, DateTimeOffset SentAt, bool Mine,
    DateTimeOffset? DeliveredAt = null, DateTimeOffset? ReadAt = null);
public sealed record ChatPage(ChatThread Conversation, ChatMessageView[] Items, bool HasMore, ChatReceipt[] Receipts);
public sealed record ChatSummary(bool Available, int Unread, int Conversations, string Revision, ChatDelivery[] PendingDeliveries);
