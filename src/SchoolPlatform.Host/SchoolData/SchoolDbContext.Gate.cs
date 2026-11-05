using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Gate;
using SchoolPlatform.Host.People;

namespace SchoolPlatform.Host.SchoolData;

public sealed partial class SchoolDbContext
{
    public DbSet<StudentCard> StudentCards => Set<StudentCard>();
    public DbSet<PersonPhoto> PersonPhotos => Set<PersonPhoto>();
    public DbSet<GateEvent> GateEvents => Set<GateEvent>();
    public DbSet<GateDevice> GateDevices => Set<GateDevice>();

    partial void ConfigureGateModel(ModelBuilder builder)
    {
        builder.Entity<StudentCard>(entity =>
        {
            entity.ToTable("StudentCards", "people");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Token).HasMaxLength(16).IsUnicode(false);
            entity.Property(x => x.RevokeReason).HasMaxLength(500);
            entity.HasIndex(x => x.Token).IsUnique();
            entity.HasIndex(x => x.StudentId).IsUnique().HasFilter("[RevokedAt] IS NULL").HasDatabaseName("UX_StudentCards_ActivePerStudent");
            entity.HasOne<StudentProfile>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.IssuedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.RevokedBy).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<PersonPhoto>(entity =>
        {
            entity.ToTable("Photos", "people", table => table.HasCheckConstraint("CK_Photos_Type", "[ContentType] IN ('image/jpeg','image/png')"));
            entity.HasKey(x => x.PersonId);
            entity.Property(x => x.StorageKey).HasMaxLength(80).IsUnicode(false);
            entity.Property(x => x.ContentType).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.Sha256).HasMaxLength(64).IsUnicode(false);
            entity.HasOne<Person>().WithOne().HasForeignKey<PersonPhoto>(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.UploadedBy).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<GateEvent>(entity =>
        {
            entity.ToTable("GateEvents", "attendance", table =>
            {
                table.HasCheckConstraint("CK_GateEvents_Direction", "[Direction] IN ('In','Out')");
                table.HasCheckConstraint("CK_GateEvents_Source", "[Source] IN ('Scanner','Manual','Mobile')");
                table.HasCheckConstraint("CK_GateEvents_Actor", "[RecordedBy] IS NOT NULL OR [DeviceId] IS NOT NULL");
                table.HasCheckConstraint("CK_GateEvents_Void", "([VoidedAt] IS NULL AND [VoidedBy] IS NULL AND [VoidReason] IS NULL) OR ([VoidedAt] IS NOT NULL AND [VoidedBy] IS NOT NULL AND [VoidReason] IS NOT NULL)");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Direction).HasMaxLength(3).IsUnicode(false);
            entity.Property(x => x.Source).HasMaxLength(10).IsUnicode(false);
            entity.Property(x => x.Note).HasMaxLength(500);
            entity.Property(x => x.VoidReason).HasMaxLength(500);
            entity.HasOne<StudentProfile>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StudentCard>().WithMany().HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<GateDevice>().WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.RecordedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.VoidedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.LocalDate, x.StudentId });
            entity.HasIndex(x => new { x.StudentId, x.OccurredAt });
            entity.HasIndex(x => x.OccurredAt);
        });
        builder.Entity<GateDevice>(entity =>
        {
            entity.ToTable("GateDevices", "attendance");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(80);
            entity.Property(x => x.TokenHash).HasMaxLength(64).IsUnicode(false);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.RevokedBy).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
