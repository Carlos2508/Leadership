using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Attendance;
using SchoolPlatform.Host.People;

namespace SchoolPlatform.Host.SchoolData;

public sealed partial class SchoolDbContext
{
    public DbSet<AttendanceSession> AttendanceSessions => Set<AttendanceSession>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();

    partial void ConfigureAttendanceModel(ModelBuilder builder)
    {
        builder.Entity<AttendanceSession>(entity =>
        {
            entity.ToTable("Sessions", "attendance");
            entity.HasKey(x => x.Id);
            entity.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.CourseId, x.Date }).IsUnique();
        });
        builder.Entity<AttendanceRecord>(entity =>
        {
            entity.ToTable("Records", "attendance", table =>
                table.HasCheckConstraint("CK_Records_Status", "[Status] IN ('Present','Absent','Late','Excused')"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.Note).HasMaxLength(200);
            entity.Property(x => x.Version).IsRowVersion();
            entity.HasOne<AttendanceSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StudentProfile>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.SessionId, x.StudentId }).IsUnique();
            entity.HasIndex(x => x.StudentId);
        });
    }
}
