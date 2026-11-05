using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Learning;
using SchoolPlatform.Host.People;

namespace SchoolPlatform.Host.SchoolData;

public sealed partial class SchoolDbContext
{
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<Submission> Submissions => Set<Submission>();

    partial void ConfigureLearningModel(ModelBuilder builder)
    {
        builder.Entity<Assignment>(entity =>
        {
            entity.ToTable("Assignments", "learning", table =>
                table.HasCheckConstraint("CK_Assignments_Status", "[Status] IN ('Draft','Published','Closed')"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(160);
            entity.Property(x => x.Instructions).HasMaxLength(4000);
            entity.Property(x => x.Status).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.Version).IsRowVersion();
            entity.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.CourseId, x.Status, x.DueAt });
        });
        builder.Entity<Submission>(entity =>
        {
            entity.ToTable("Submissions", "learning", table =>
                table.HasCheckConstraint("CK_Submissions_Status", "[Status] IN ('Submitted','Reviewed')"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Text).HasMaxLength(8000);
            entity.Property(x => x.Status).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.Feedback).HasMaxLength(2000);
            entity.Property(x => x.Version).IsRowVersion();
            entity.HasOne<Assignment>().WithMany().HasForeignKey(x => x.AssignmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StudentProfile>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.SubmittedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.FeedbackBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.AssignmentId, x.StudentId }).IsUnique();
        });
    }
}
