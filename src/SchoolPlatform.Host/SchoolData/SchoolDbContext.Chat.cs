using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Chat;
using SchoolPlatform.Host.People;

namespace SchoolPlatform.Host.SchoolData;

public sealed partial class SchoolDbContext
{
    public DbSet<ChatConversation> ChatConversations => Set<ChatConversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<ChatPresence> ChatPresences => Set<ChatPresence>();

    private static void ConfigureChat(ModelBuilder builder)
    {
        builder.Entity<ChatConversation>(e =>
        {
            e.ToTable("Conversations", "chat", t =>
            {
                t.HasCheckConstraint("CK_Chat_Distinct", "[TeacherId] <> [MemberId]");
                t.HasCheckConstraint("CK_Chat_Sequences", "[LastSequence] >= 0 AND [TeacherReadSequence] >= 0 AND [MemberReadSequence] >= 0 AND [TeacherReadSequence] <= [LastSequence] AND [MemberReadSequence] <= [LastSequence]");
                t.HasCheckConstraint("CK_Chat_Delivery", "[TeacherDeliveredSequence] >= [TeacherReadSequence] AND [MemberDeliveredSequence] >= [MemberReadSequence] AND [TeacherDeliveredSequence] <= [LastSequence] AND [MemberDeliveredSequence] <= [LastSequence]");
            });
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.AcademicYearId, x.StudentId, x.TeacherId, x.MemberId }).IsUnique();
            e.HasIndex(x => new { x.TeacherId, x.UpdatedAt });
            e.HasIndex(x => new { x.MemberId, x.UpdatedAt });
            e.HasOne<AcademicYear>().WithMany().HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<StudentProfile>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<StaffProfile>().WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Person>().WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<ChatMessage>(e =>
        {
            e.ToTable("Messages", "chat", t =>
            {
                t.HasCheckConstraint("CK_ChatMessage_Sequence", "[Sequence] > 0");
                t.HasCheckConstraint("CK_ChatMessage_Body", "LEN(LTRIM(RTRIM([Body]))) > 0");
                t.HasCheckConstraint("CK_ChatMessage_Receipts", "([DeliveredAt] IS NULL OR [DeliveredAt] >= [SentAt]) AND ([ReadAt] IS NULL OR ([DeliveredAt] IS NOT NULL AND [ReadAt] >= [DeliveredAt]))");
            });
            e.HasKey(x => new { x.ConversationId, x.Sequence });
            e.HasIndex(x => new { x.ConversationId, x.SenderId, x.ClientMessageId }).IsUnique();
            e.Property(x => x.Body).HasMaxLength(4000);
            e.HasOne<ChatConversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Person>().WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<ChatPresence>(e =>
        {
            e.ToTable("Presence", "chat", t => t.HasCheckConstraint("CK_ChatPresence_Version", "[Version] > 0"));
            e.HasKey(x => new { x.UserId, x.ClientId });
            e.Property(x => x.SecurityStamp).HasMaxLength(256);
            e.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.UserId, x.ActiveUntil });
        });
    }
}
