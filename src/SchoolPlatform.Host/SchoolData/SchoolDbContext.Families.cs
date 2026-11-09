using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.People;

namespace SchoolPlatform.Host.SchoolData;

public sealed partial class SchoolDbContext
{
    public DbSet<FamilyRegistration> FamilyRegistrations => Set<FamilyRegistration>();
    private static void ConfigureFamilies(ModelBuilder builder)
    {
        builder.Entity<FamilyRegistration>(e =>
        {
            e.ToTable("FamilyRegistrations", "people", t => t.HasCheckConstraint("CK_FamilyRegistration_Result", "ISJSON([ResultJson]) = 1"));
            e.HasKey(x => x.Id);
            e.Property(x => x.RequestHash).HasColumnType("binary(32)");
            e.Property(x => x.ResultJson).HasMaxLength(8000);
            e.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AcademicYear>().WithMany().HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
