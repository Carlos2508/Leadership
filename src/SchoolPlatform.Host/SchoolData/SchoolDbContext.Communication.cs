using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Communication;

namespace SchoolPlatform.Host.SchoolData;

public sealed partial class SchoolDbContext
{
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<AnnouncementRead> AnnouncementReads => Set<AnnouncementRead>();

    partial void ConfigureCommunicationModel(ModelBuilder builder)
    {
        builder.Entity<Announcement>(entity =>
        {
            entity.ToTable("Announcements", "communication", table =>
            {
                table.HasCheckConstraint("CK_Announcements_Status", "[Status] IN ('Draft','Published','Archived')");
                table.HasCheckConstraint("CK_Announcements_Audience", "[AudienceKind] IN ('School','Grade','Course')");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(160);
            entity.Property(x => x.Body).HasMaxLength(4000);
            entity.Property(x => x.AudienceKind).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.Status).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.Version).IsRowVersion();
            entity.HasOne<AcademicYear>().WithMany().HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.PublishedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.Status, x.PublishedAt });
        });
        builder.Entity<AnnouncementRead>(entity =>
        {
            entity.ToTable("AnnouncementReads", "communication");
            entity.HasKey(x => new { x.AnnouncementId, x.UserId });
            entity.HasOne<Announcement>().WithMany().HasForeignKey(x => x.AnnouncementId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
