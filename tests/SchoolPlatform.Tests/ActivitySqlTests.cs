using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Attendance;
using SchoolPlatform.Host.Chat;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.Grades;
using SchoolPlatform.Host.Learning;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Tests;

/// <summary>Activities with points (period result = sum), homework worth points, templates and the student calendar.</summary>
public sealed class ActivitySqlTests : FamilyRegistrationTestBase
{
    private Guid course, period, ana, luis;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        period = await Use<AcademicService, Guid>(s => s.AddPeriodAsync(Owner, Year, new("Bimestre 1", Starts, new(2027, 3, 20)), default));
        var subject = await Use<CourseService, Guid>(s => s.SaveSubjectAsync(Owner, null, new("ACT-MAT", "Matemática"), default));
        await Use<CourseService, int>(s => s.CreateCoursesAsync(Owner, Year, new(Grade, Section, [subject]), default));
        course = (await Use<CourseService, CourseView[]>(s => s.CoursesAsync(Year, Grade, default))).Single().Id;
        var family = await Register(Request());
        ana = family.Students[0].PersonId;
        luis = family.Students[1].PersonId;
    }

    private Task<Guid> Activity(string title, decimal points) =>
        Use<ActivityService, Guid>(s => s.CreateAsync(Owner, course, new(period, title, points, null, null), default));
    private Task<int> Score(params ActivityScoreEntry[] entries) =>
        Use<ActivityService, int>(s => s.SaveScoresAsync(Owner, course, new(period, entries, null), default));
    private Task<ActivityBook> Book() => Use<ActivityService, ActivityBook>(async s => (await s.BookAsync(Owner, course, period, default))!);
    private Task<PeriodScore?> Result(Guid student) =>
        Use<SchoolDbContext, PeriodScore?>(db => db.PeriodScores.AsNoTracking().SingleOrDefaultAsync(x => x.StudentId == student && x.CourseId == course && x.PeriodId == period));

    [Fact]
    public async Task Points_of_activities_add_up_to_the_period_result_with_history()
    {
        var homework = await Activity("Tarea 1", 20);
        var exam = await Activity("Examen", 30);
        Assert.Equal(2, await Score(new(homework, ana, 18, ScoreStatus.Scored, null), new(exam, ana, 25.5m, ScoreStatus.Scored, null),
            new(homework, luis, null, ScoreStatus.Missing, null)));
        Assert.Equal((43.5m, ScoreStatus.Scored), ((await Result(ana))!.Score, (await Result(ana))!.Status));
        Assert.Equal((0m, ScoreStatus.Scored), ((await Result(luis))!.Score, (await Result(luis))!.Status)); // no entregó = 0

        // A correction keeps the previous value in the activity history and in the period history.
        var book = await Book();
        var cell = book.Rows.Single(r => r.StudentId == ana).Scores[exam];
        await Score(new ActivityScoreEntry(exam, ana, 28, ScoreStatus.Scored, cell.Version));
        Assert.Equal(46m, (await Result(ana))!.Score);
        Assert.Equal(2, await Use<SchoolDbContext, int>(db => db.ActivityScoreRevisions.CountAsync(r => db.ActivityScores.Any(s => s.Id == r.ScoreId && s.StudentId == ana && s.ActivityId == exam))));
        Assert.True(await Use<SchoolDbContext, int>(db => db.ScoreRevisions.CountAsync()) >= 3);

        // Excused everywhere means excused; a stale version never overwrites.
        await Assert.ThrowsAsync<OperationException>(() => Score(new ActivityScoreEntry(exam, ana, 10, ScoreStatus.Scored, cell.Version)));
        Assert.Equal(46m, (await Result(ana))!.Score);

        // Limits: points above the activity value, totals above 100, typed period results while activities exist.
        var luisVersion = (await Book()).Rows.Single(r => r.StudentId == luis).Scores[homework].Version;
        Assert.Equal(400, (await Assert.ThrowsAsync<OperationException>(() => Score(new ActivityScoreEntry(homework, luis, 21, ScoreStatus.Scored, luisVersion)))).Status);
        Assert.Equal(400, (await Assert.ThrowsAsync<OperationException>(() => Activity("Proyecto", 50.01m))).Status);
        var manual = await Assert.ThrowsAsync<OperationException>(() => Use<GradeService, SaveScoresResult>(s =>
            s.SaveAsync(Owner, course, new(period, [new(ana, 90, ScoreStatus.Scored, null)], null), default)));
        Assert.Equal("activity-graded", manual.Code);
    }

    [Fact]
    public async Task Removing_a_scored_activity_needs_a_reason_keeps_its_points_and_recalculates()
    {
        var a = await Activity("Laboratorio", 40);
        var b = await Activity("Quiz", 10);
        await Score(new(a, ana, 30, ScoreStatus.Scored, null), new(b, ana, 9, ScoreStatus.Scored, null));
        Assert.Equal(39m, (await Result(ana))!.Score);
        var version = (await Book()).Activities.Single(x => x.Id == a).Version;
        Assert.Equal(400, (await Assert.ThrowsAsync<OperationException>(() =>
            Use<ActivityService>(s => s.RemoveAsync(Owner, course, a, new(version, null), default)))).Status);
        await Use<ActivityService>(s => s.RemoveAsync(Owner, course, a, new(version, "Laboratorio repetido por error de captura."), default));
        Assert.Equal(9m, (await Result(ana))!.Score);
        Assert.Single((await Book()).Activities);
        Assert.Equal(2, await Use<SchoolDbContext, int>(db => db.ActivityScores.CountAsync())); // points stay for the record
    }

    [Fact]
    public async Task Homework_worth_points_becomes_an_activity_and_its_review_scores_the_period()
    {
        var due = new DateTimeOffset(2027, 2, 10, 23, 0, 0, TimeSpan.FromHours(-6));
        var outside = await Assert.ThrowsAsync<OperationException>(() => Use<LearningService, Guid>(s =>
            s.SaveAsync(Owner, course, null, new("Tarea fuera del ciclo", "", new DateTimeOffset(2026, 11, 10, 23, 0, 0, TimeSpan.Zero), false, null, period, 10), default)));
        Assert.Contains("dentro del ciclo", outside.Message);
        var assignment = await Use<LearningService, Guid>(s => s.SaveAsync(Owner, course, null, new("Fracciones", "Ejercicios 1 a 10", due, true, null, period, 15), default));
        var activity = Assert.Single((await Book()).Activities);
        Assert.Equal((assignment, 15m), (activity.AssignmentId!.Value, activity.MaxPoints));

        // Scoring a student (no submission needed for «no entregó») writes the period result.
        await Use<ActivityService>(s => s.ScoreAssignmentAsync(Owner, course, assignment, ana, 12, ScoreStatus.Scored, default));
        await Use<ActivityService>(s => s.ScoreAssignmentAsync(Owner, course, assignment, luis, null, ScoreStatus.Missing, default));
        Assert.Equal(12m, (await Result(ana))!.Score);
        Assert.Equal(0m, (await Result(luis))!.Score);

        // Points of a scored homework cannot be removed, and its activity cannot be removed from the gradebook.
        var version = await Use<SchoolDbContext, byte[]>(db => db.Assignments.Where(x => x.Id == assignment).Select(x => x.Version).SingleAsync());
        Assert.Equal("assignment-scored", (await Assert.ThrowsAsync<OperationException>(() => Use<LearningService, Guid>(s =>
            s.SaveAsync(Owner, course, assignment, new("Fracciones", "Ejercicios 1 a 10", due, true, Versions.Encode(version)), default)))).Code);
        Assert.Equal("linked-assignment", (await Assert.ThrowsAsync<OperationException>(() =>
            Use<ActivityService>(s => s.RemoveAsync(Owner, course, activity.Id, new(activity.Version, "Quitar la tarea calificada."), default)))).Code);
    }

    [Fact]
    public async Task Templates_validate_their_total_and_apply_once_per_period()
    {
        Assert.Equal(400, (await Assert.ThrowsAsync<OperationException>(() => Use<ActivityService, Guid>(s =>
            s.SaveTemplateAsync(Owner, null, new("Excedida", [new("A", 60), new("B", 50)], true, null, Reason), default)))).Status);
        var id = await Use<ActivityService, Guid>(s => s.SaveTemplateAsync(Owner, null,
            new("Actividades por bimestre", [new("Tarea 1", 10), new("Tarea 2", 10), new("Laboratorio", 20), new("Examen", 60)], true, null, Reason), default));
        Assert.Equal(4, await Use<ActivityService, int>(s => s.ApplyTemplateAsync(Owner, course, new(period, id), default)));
        Assert.Equal(0, await Use<ActivityService, int>(s => s.ApplyTemplateAsync(Owner, course, new(period, id), default)));
        var book = await Book();
        Assert.Equal(100m, book.TotalPoints);
        Assert.Contains(book.Templates, t => t.Id == id && t.Total == 100);
        Assert.Contains(await Use<SchoolDbContext, string[]>(db => db.AuditEvents.Select(x => x.Action).ToArrayAsync()), x => x == "ActivityTemplateCreated");
    }

    [Fact]
    public async Task Attendance_calendar_requires_access_and_a_short_range()
    {
        var guardianUser = await Account("act-family");
        var guardian = (await Use<SchoolDbContext, Guid>(db => db.Relationships.Where(r => r.StudentId == ana).Select(r => r.GuardianId).FirstAsync()));
        await Use<PeopleService>(async s => await s.LinkAccountAsync(Owner, guardian, new((await s.GetAsync(guardian, default)).Version, guardianUser, Reason), default));
        // Ana's link has AcademicRecords; Luis's does not.
        var calendar = await Use<AttendanceService, AttendanceCalendar?>(s => s.CalendarAsync(guardianUser, ana, new(2027, 2, 1), new(2027, 2, 28), default));
        Assert.NotNull(calendar);
        Assert.Empty(calendar!.Days);
        Assert.Null(await Use<AttendanceService, AttendanceCalendar?>(s => s.CalendarAsync(guardianUser, luis, new(2027, 2, 1), new(2027, 2, 28), default)));
        Assert.Equal(400, (await Assert.ThrowsAsync<OperationException>(() =>
            Use<AttendanceService, AttendanceCalendar?>(s => s.CalendarAsync(guardianUser, ana, new(2027, 1, 1), new(2027, 6, 1), default)))).Status);
    }

    [Fact]
    public async Task Chat_attachment_limit_is_set_by_administration_with_a_reason()
    {
        Assert.Equal(PlatformSettingsService.DefaultChatAttachmentMb, await Use<PlatformSettingsService, int>(s => s.ChatAttachmentMaxMbAsync(default)));
        Assert.Equal(400, (await Assert.ThrowsAsync<OperationException>(() => Use<PlatformSettingsService>(s =>
            s.SaveAsync(Owner, new(PlatformSettingsService.ChatAttachmentLimitMb + 1, null, Reason), default)))).Status);
        await Use<PlatformSettingsService>(s => s.SaveAsync(Owner, new(8, null, Reason), default));
        Assert.Equal(8, await Use<PlatformSettingsService, int>(s => s.ChatAttachmentMaxMbAsync(default)));
        var basic = await Account("act-basic");
        Assert.Equal(403, (await Assert.ThrowsAsync<OperationException>(() => Use<PlatformSettingsService>(s =>
            s.SaveAsync(basic, new(3, null, Reason), default)))).Status);
        Assert.Contains(await Use<SchoolDbContext, string[]>(db => db.AuditEvents.Select(x => x.Action).ToArrayAsync()), x => x == "PlatformSettingChanged");
    }
}

/// <summary>Chat attachment rules: real content signatures and ZIP contents, without a database.</summary>
public sealed class ChatAttachmentRuleTests
{
    private const long Max = 5 * 1024 * 1024;

    private static byte[] Zip(params string[] names)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            foreach (var name in names)
            {
                using var entry = zip.CreateEntry(name).Open();
                entry.Write("contenido"u8);
            }
        return stream.ToArray();
    }

    [Fact]
    public void Documents_images_and_clean_zips_are_accepted()
    {
        Assert.Equal("application/pdf", ChatAttachmentRules.Validate("guia.pdf", "%PDF-1.7 synthetic"u8.ToArray(), Max));
        Assert.Equal("application/zip", ChatAttachmentRules.Validate("tareas.zip", Zip("lectura.pdf", "notas.docx"), Max));
        Assert.Equal("text/csv", ChatAttachmentRules.Validate("lista.csv", "a,b\n1,2"u8.ToArray(), Max));
    }

    [Theory]
    [InlineData("programa.exe")]
    [InlineData("script.bat")]
    [InlineData("macro.docm")]
    public void Programs_and_macros_are_rejected_by_extension(string name) =>
        Assert.Equal(400, Assert.Throws<OperationException>(() => ChatAttachmentRules.Validate(name, "MZ"u8.ToArray(), Max)).Status);

    [Fact]
    public void Renamed_programs_zip_bombs_and_dangerous_zip_contents_are_rejected()
    {
        Assert.Contains("no corresponde", Assert.Throws<OperationException>(() => ChatAttachmentRules.Validate("factura.pdf", "MZ\u0090"u8.ToArray(), Max)).Message);
        Assert.Contains("no permitido", Assert.Throws<OperationException>(() => ChatAttachmentRules.Validate("fotos.zip", Zip("foto.png", "instalar.exe"), Max)).Message);
        Assert.Contains("no permitido", Assert.Throws<OperationException>(() => ChatAttachmentRules.Validate("x.zip", Zip("carpeta/run.js"), Max)).Message);
        Assert.Contains("MB", Assert.Throws<OperationException>(() => ChatAttachmentRules.Validate("grande.pdf", new byte[6 * 1024 * 1024], Max)).Message);
        Assert.Contains("binarios", Assert.Throws<OperationException>(() => ChatAttachmentRules.Validate("nota.txt", [0x41, 0x00, 0x42], Max)).Message);
    }
}
