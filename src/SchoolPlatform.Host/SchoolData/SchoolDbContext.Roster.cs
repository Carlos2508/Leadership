using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.LegacyImport;
using SchoolPlatform.Host.People;

namespace SchoolPlatform.Host.SchoolData;

public sealed partial class SchoolDbContext
{
    public DbSet<RosterBatch> RosterBatches => Set<RosterBatch>();
    public DbSet<RosterSourceAlias> RosterSourceAliases => Set<RosterSourceAlias>();

    private static void ConfigureRoster(ModelBuilder builder)
    {
        builder.Entity<RosterBatch>(e =>
        {
            e.ToTable("RosterBatches", "people");
            e.HasKey(x => x.Id);
            e.Property(x => x.Source).HasMaxLength(40).IsUnicode(false);
            e.Property(x => x.FileSha256).HasMaxLength(64).IsUnicode(false);
            e.Property(x => x.PlanSha256).HasMaxLength(64).IsUnicode(false);
            e.HasOne<Academic.AcademicYear>().WithMany().HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<RosterSourceAlias>(e =>
        {
            e.ToTable("RosterSourceAliases", "people");
            e.HasKey(x => new { x.Source, x.Entity, x.LegacyId, x.Discriminator });
            e.Property(x => x.Source).HasMaxLength(40).IsUnicode(false);
            e.Property(x => x.Entity).HasMaxLength(20).IsUnicode(false);
            e.Property(x => x.LegacyId).HasMaxLength(60).IsUnicode(false);
            e.Property(x => x.Discriminator).HasMaxLength(40).IsUnicode(false);
            e.HasOne<Person>().WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<RosterBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
