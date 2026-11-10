using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Grades;
using SchoolPlatform.Host.Learning;
using SchoolPlatform.Host.People;

namespace SchoolPlatform.Host.SchoolData;

public sealed partial class SchoolDbContext
{
    public DbSet<GradeActivity> GradeActivities => Set<GradeActivity>();
    public DbSet<ActivityScore> ActivityScores => Set<ActivityScore>();
    public DbSet<ActivityScoreRevision> ActivityScoreRevisions => Set<ActivityScoreRevision>();
    public DbSet<ActivityTemplate> ActivityTemplates => Set<ActivityTemplate>();

    private static void ConfigureActivities(ModelBuilder builder)
    {
        builder.Entity<GradeActivity>(entity =>
        {
            entity.ToTable("Activities", "assessment", table =>
                table.HasCheckConstraint("CK_Activities_Points", "[MaxPoints] > 0 AND [MaxPoints] <= 100"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(120);
            entity.Property(x => x.MaxPoints).HasPrecision(5, 2);
            entity.Property(x => x.Version).IsRowVersion();
            entity.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AcademicPeriod>().WithMany().HasForeignKey(x => x.PeriodId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Assignment>().WithMany().HasForeignKey(x => x.AssignmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.CourseId, x.PeriodId, x.Sequence });
            entity.Property(x => x.RemoveReason).HasMaxLength(500);
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.RemovedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.AssignmentId).IsUnique().HasFilter("[AssignmentId] IS NOT NULL AND [RemovedAt] IS NULL");
        });
        builder.Entity<ActivityScore>(entity =>
        {
            entity.ToTable("ActivityScores", "assessment", table =>
            {
                table.HasCheckConstraint("CK_ActivityScores_Status", "[Status] IN ('Scored','Missing','Excused')");
                table.HasCheckConstraint("CK_ActivityScores_Value",
                    "([Status] = 'Scored' AND [Points] IS NOT NULL AND [Points] >= 0) OR ([Status] <> 'Scored' AND [Points] IS NULL)");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Points).HasPrecision(5, 2);
            entity.Property(x => x.Status).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.Version).IsRowVersion();
            entity.HasOne<GradeActivity>().WithMany().HasForeignKey(x => x.ActivityId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StudentProfile>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.ActivityId, x.StudentId }).IsUnique();
            entity.HasIndex(x => x.StudentId);
        });
        builder.Entity<ActivityScoreRevision>(entity =>
        {
            entity.ToTable("ActivityScoreRevisions", "assessment");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PreviousPoints).HasPrecision(5, 2);
            entity.Property(x => x.NewPoints).HasPrecision(5, 2);
            entity.Property(x => x.PreviousStatus).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.NewStatus).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.Reason).HasMaxLength(500);
            entity.HasOne<ActivityScore>().WithMany().HasForeignKey(x => x.ScoreId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.ChangedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.ScoreId, x.ChangedAt });
        });
        builder.Entity<ActivityTemplate>(entity =>
        {
            entity.ToTable("ActivityTemplates", "assessment");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(80);
            entity.Property(x => x.ItemsJson).HasMaxLength(4000);
            entity.Property(x => x.Version).IsRowVersion();
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.Name).IsUnique();
        });
    }
}
