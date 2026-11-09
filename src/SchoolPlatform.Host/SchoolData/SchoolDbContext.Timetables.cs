using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Timetables;

namespace SchoolPlatform.Host.SchoolData;
public sealed partial class SchoolDbContext
{
    public DbSet<TimetablePlan> TimetablePlans => Set<TimetablePlan>();
    public DbSet<TimetableRevision> TimetableRevisions => Set<TimetableRevision>();
    public DbSet<TimetableEntry> TimetableEntries => Set<TimetableEntry>();
    public DbSet<TimetableRoom> TimetableRooms => Set<TimetableRoom>();
    public DbSet<TimetableTemplate> TimetableTemplates => Set<TimetableTemplate>();
    public DbSet<TimetableOperation> TimetableOperations => Set<TimetableOperation>();
    private static void ConfigureTimetables(ModelBuilder b)
    {
        b.Entity<TimetablePlan>(e => {
            e.ToTable("Plans", "timetable"); e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.AcademicYearId, x.GradeLevelId, x.SectionId }).IsUnique().HasFilter(null);
            e.HasOne<AcademicYear>().WithMany().HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<GradeLevel>().WithMany().HasForeignKey(x => x.GradeLevelId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Section>().WithMany().HasForeignKey(x => x.SectionId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<TimetableRevision>(e => {
            e.ToTable("Revisions", "timetable", t => {
                t.HasCheckConstraint("CK_TimetableRevision_State", "[State] IN ('Draft','Published') AND ([State] = 'Draft' AND [PublishedAt] IS NULL OR [State] = 'Published' AND [PublishedAt] IS NOT NULL)");
                t.HasCheckConstraint("CK_TimetableRevision_Dates", "[UntilExclusive] >= [StartsOn] AND ISJSON([StructureJson]) = 1");
            }); e.HasKey(x => x.Id); e.Property(x => x.State).HasMaxLength(12).IsUnicode(false); e.Property(x => x.Version).IsRowVersion();
            e.HasIndex(x => x.PlanId).IsUnique().HasFilter("[State] = 'Draft'");
            e.HasIndex(x => new { x.PlanId, x.StartsOn });
            e.HasOne<TimetablePlan>().WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<TimetableEntry>(e => {
            e.ToTable("Entries", "timetable", t => {
                t.HasCheckConstraint("CK_TimetableEntry_Time", "[Day] BETWEEN 1 AND 7 AND [Start] >= 0 AND [End] <= 1440 AND [Start] < [End]");
                t.HasCheckConstraint("CK_TimetableEntry_Kind", "([Kind] = 'Lesson' AND [CourseId] IS NOT NULL) OR ([Kind] = 'Break' AND [CourseId] IS NULL AND [AssignmentId] IS NULL AND [StaffId] IS NULL AND [RoomId] IS NULL)");
            }); e.HasKey(x => x.Id); e.Property(x => x.Kind).HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.Subject).HasMaxLength(80); e.Property(x => x.Teacher).HasMaxLength(220); e.Property(x => x.Room).HasMaxLength(80);
            e.HasIndex(x => new { x.RevisionId, x.PeriodId, x.Day }).IsUnique();
            e.HasIndex(x => new { x.Day, x.Start, x.End });
            e.HasOne<TimetableRevision>().WithMany().HasForeignKey(x => x.RevisionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<TeachingAssignment>().WithMany().HasForeignKey(x => x.AssignmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<SchoolPlatform.Host.People.StaffProfile>().WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<TimetableRoom>().WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<TimetableRoom>(e => { e.ToTable("Rooms", "timetable"); e.HasKey(x => x.Id); e.Property(x => x.Name).HasMaxLength(80); e.HasIndex(x => x.Name).IsUnique(); });
        b.Entity<TimetableTemplate>(e => { e.ToTable("Templates", "timetable", t => t.HasCheckConstraint("CK_TimetableTemplate_Json", "ISJSON([StructureJson]) = 1")); e.HasKey(x => x.Id); e.Property(x => x.Name).HasMaxLength(80); e.HasIndex(x => x.Name).IsUnique(); });
        b.Entity<TimetableOperation>(e => { e.ToTable("Operations", "timetable", t => t.HasCheckConstraint("CK_TimetableOperation_Json", "ISJSON([ResultJson]) = 1")); e.HasKey(x => x.Id); e.Property(x => x.Hash).HasColumnType("binary(32)"); e.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict); });
    }
}
