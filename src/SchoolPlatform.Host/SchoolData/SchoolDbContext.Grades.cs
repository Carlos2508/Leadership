using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Finance;
using SchoolPlatform.Host.Grades;
using SchoolPlatform.Host.People;

namespace SchoolPlatform.Host.SchoolData;

public sealed partial class SchoolDbContext
{
    public DbSet<PeriodScore> PeriodScores => Set<PeriodScore>();
    public DbSet<ScoreRevision> ScoreRevisions => Set<ScoreRevision>();
    public DbSet<GradePublication> GradePublications => Set<GradePublication>();

    partial void ConfigureGradesModel(ModelBuilder builder)
    {
        builder.Entity<PeriodScore>(entity =>
        {
            entity.ToTable("PeriodScores", "assessment", table =>
            {
                table.HasCheckConstraint("CK_PeriodScores_Status", "[Status] IN ('Scored','Missing','Excused')");
                table.HasCheckConstraint("CK_PeriodScores_Value",
                    "([Status] = 'Scored' AND [Score] IS NOT NULL AND [Score] BETWEEN 0 AND 100) OR ([Status] <> 'Scored' AND [Score] IS NULL)");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Score).HasPrecision(7, 3);
            entity.Property(x => x.Status).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.Version).IsRowVersion();
            entity.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AcademicPeriod>().WithMany().HasForeignKey(x => x.PeriodId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StudentProfile>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.CourseId, x.PeriodId, x.StudentId }).IsUnique();
            entity.HasIndex(x => new { x.StudentId, x.PeriodId });
        });
        builder.Entity<ScoreRevision>(entity =>
        {
            entity.ToTable("ScoreRevisions", "assessment");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PreviousScore).HasPrecision(7, 3);
            entity.Property(x => x.NewScore).HasPrecision(7, 3);
            entity.Property(x => x.PreviousStatus).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.NewStatus).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.Reason).HasMaxLength(500);
            entity.HasOne<PeriodScore>().WithMany().HasForeignKey(x => x.ScoreId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.ChangedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.ScoreId, x.ChangedAt });
        });
        builder.Entity<GradePublication>(entity =>
        {
            entity.ToTable("Publications", "assessment");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Note).HasMaxLength(500);
            entity.HasOne<AcademicYear>().WithMany().HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AcademicPeriod>().WithMany().HasForeignKey(x => x.PeriodId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ClearanceCutoff>().WithMany().HasForeignKey(x => x.CutoffId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.PublishedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.PeriodId).IsUnique();
        });
    }
}
