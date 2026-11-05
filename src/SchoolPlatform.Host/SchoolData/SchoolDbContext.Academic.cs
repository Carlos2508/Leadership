using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.People;

namespace SchoolPlatform.Host.SchoolData;

public sealed partial class SchoolDbContext
{
    public DbSet<AcademicYear> Years => Set<AcademicYear>();
    public DbSet<AcademicPeriod> Periods => Set<AcademicPeriod>();
    public DbSet<GradeLevel> Grades => Set<GradeLevel>();
    public DbSet<Section> Sections => Set<Section>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<EnrollmentPlacement> Placements => Set<EnrollmentPlacement>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<TeachingAssignment> TeachingAssignments => Set<TeachingAssignment>();

    partial void ConfigureAcademicModel(ModelBuilder builder)
    {
        builder.Entity<AcademicYear>(entity =>
        {
            entity.ToTable("Years", "academic", table =>
            {
                table.HasCheckConstraint("CK_Years_Dates", "[EndsOn] > [StartsOn]");
                table.HasCheckConstraint("CK_Years_Status", "[Status] IN ('Draft','EnrollmentOpen','Active','Closed')");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Label).HasMaxLength(40);
            entity.Property(x => x.Status).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.Version).IsRowVersion();
            entity.HasIndex(x => x.Label).IsUnique();
            entity.HasIndex(x => x.IsDefault).IsUnique().HasFilter("[IsDefault] = 1");
            entity.HasMany(x => x.Periods).WithOne().HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<AcademicPeriod>(entity =>
        {
            entity.ToTable("Periods", "academic", table =>
            {
                table.HasCheckConstraint("CK_Periods_Dates", "[EndsOn] >= [StartsOn]");
                table.HasCheckConstraint("CK_Periods_Sequence", "[Sequence] BETWEEN 1 AND 12");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(60);
            entity.HasIndex(x => new { x.AcademicYearId, x.Sequence }).IsUnique();
        });
        builder.Entity<GradeLevel>(entity =>
        {
            entity.ToTable("Grades", "academic");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.Name).HasMaxLength(80);
            entity.Property(x => x.Stage).HasMaxLength(60);
            entity.Property(x => x.Version).IsRowVersion();
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.Name).IsUnique();
        });
        builder.Entity<Section>(entity =>
        {
            entity.ToTable("Sections", "academic", table =>
                table.HasCheckConstraint("CK_Sections_Capacity", "[Capacity] IS NULL OR [Capacity] BETWEEN 1 AND 500"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(20);
            entity.Property(x => x.Shift).HasMaxLength(40);
            entity.Property(x => x.Version).IsRowVersion();
            entity.HasOne<AcademicYear>().WithMany().HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<GradeLevel>().WithMany().HasForeignKey(x => x.GradeLevelId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.AcademicYearId, x.GradeLevelId, x.Name }).IsUnique();
        });
        builder.Entity<Enrollment>(entity =>
        {
            entity.ToTable("Enrollments", "academic", table =>
            {
                table.HasCheckConstraint("CK_Enrollments_Status", "[Status] IN ('Active','Withdrawn','Cancelled','Completed')");
                table.HasCheckConstraint("CK_Enrollments_Withdrawal",
                    "([Status] = 'Withdrawn' AND [WithdrawnOn] IS NOT NULL) OR ([Status] <> 'Withdrawn' AND [WithdrawnOn] IS NULL)");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.Version).IsRowVersion();
            entity.HasOne<StudentProfile>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AcademicYear>().WithMany().HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.StudentId, x.AcademicYearId }).IsUnique();
            entity.HasIndex(x => new { x.AcademicYearId, x.Status });
            entity.HasMany(x => x.Placements).WithOne().HasForeignKey(x => x.EnrollmentId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<EnrollmentPlacement>(entity =>
        {
            entity.ToTable("EnrollmentPlacements", "academic", table =>
                table.HasCheckConstraint("CK_EnrollmentPlacements_Dates", "[EndsOn] IS NULL OR [EndsOn] >= [StartsOn]"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Reason).HasMaxLength(500);
            entity.HasOne<GradeLevel>().WithMany().HasForeignKey(x => x.GradeLevelId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Section>().WithMany().HasForeignKey(x => x.SectionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            // EndsOn is exclusive. EndsOn = StartsOn records a same-day correction/withdrawal (empty interval kept as history).
            // At most one open placement per enrollment; overlaps are also checked in the service under the enrollment version.
            entity.HasIndex(x => x.EnrollmentId).IsUnique().HasFilter("[EndsOn] IS NULL");
            entity.HasIndex(x => new { x.SectionId, x.EndsOn });
            entity.HasIndex(x => new { x.GradeLevelId, x.EndsOn });
        });
        builder.Entity<Subject>(entity =>
        {
            entity.ToTable("Subjects", "academic");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.Name).HasMaxLength(80);
            entity.Property(x => x.Version).IsRowVersion();
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.Name).IsUnique();
        });
        builder.Entity<Course>(entity =>
        {
            entity.ToTable("Courses", "academic");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Version).IsRowVersion();
            entity.HasOne<AcademicYear>().WithMany().HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<GradeLevel>().WithMany().HasForeignKey(x => x.GradeLevelId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Section>().WithMany().HasForeignKey(x => x.SectionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.AcademicYearId, x.GradeLevelId, x.SubjectId }).IsUnique().HasFilter("[SectionId] IS NULL");
            entity.HasIndex(x => new { x.AcademicYearId, x.GradeLevelId, x.SectionId, x.SubjectId }).IsUnique().HasFilter("[SectionId] IS NOT NULL");
        });
        builder.Entity<TeachingAssignment>(entity =>
        {
            entity.ToTable("TeachingAssignments", "academic", table =>
            {
                table.HasCheckConstraint("CK_TeachingAssignments_Role", "[Role] IN ('Lead','Assistant')");
                table.HasCheckConstraint("CK_TeachingAssignments_Dates", "[EndsOn] IS NULL OR [EndsOn] >= [StartsOn]");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Role).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.EndReason).HasMaxLength(500);
            entity.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StaffProfile>().WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.CourseId, x.StaffId }).IsUnique().HasFilter("[EndsOn] IS NULL");
            entity.HasIndex(x => new { x.StaffId, x.EndsOn });
        });
    }
}
