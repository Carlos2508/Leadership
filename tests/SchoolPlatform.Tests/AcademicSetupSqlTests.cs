using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.People;

namespace SchoolPlatform.Tests;

/// <summary>Shortcuts for preparing a new year: generated periods and sections/courses/teachers copied from another year.</summary>
public sealed class AcademicSetupSqlTests : SqlTestBase
{
    private static readonly DateOnly Start = new(2026, 1, 12);
    private Guid source, grade, oldGrade, sectionA, sectionB, math, science, art;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        source = await Use<AcademicService, Guid>(x => x.CreateYearAsync(Owner, new("2026", Start, new(2026, 10, 30)), default));
        grade = await Use<AcademicService, Guid>(x => x.SaveGradeAsync(Owner, null, new("1B", "Primero básico", "Básico", 1), default));
        oldGrade = await Use<AcademicService, Guid>(x => x.SaveGradeAsync(Owner, null, new("ANT", "Grado anterior", "Básico", 2), default));
        sectionA = await Use<AcademicService, Guid>(x => x.SaveSectionAsync(Owner, source, null, new(grade, "A", "Matutina", 30), default));
        sectionB = await Use<AcademicService, Guid>(x => x.SaveSectionAsync(Owner, source, null, new(grade, "B", null, null), default));
        await Use<AcademicService, Guid>(x => x.SaveSectionAsync(Owner, source, null, new(oldGrade, "A", null, null), default));
        math = await Use<CourseService, Guid>(x => x.SaveSubjectAsync(Owner, null, new("MAT", "Matemática"), default));
        science = await Use<CourseService, Guid>(x => x.SaveSubjectAsync(Owner, null, new("CIE", "Ciencias"), default));
        art = await Use<CourseService, Guid>(x => x.SaveSubjectAsync(Owner, null, new("ART", "Arte"), default));
    }

    private async Task<Guid> Teacher(string username, string given)
    {
        var person = (await Use<PeopleService, CreatedPerson>(x => x.CreateAsync(Owner, new(given, "Docente", null, null, [], null, false, true), default))).Id;
        await Use<PeopleService>(async x => await x.AddRoleAsync(Owner, person, new((await x.GetAsync(person, default)).Version, "staff", null, "Docente"), default));
        var user = await Account(username);
        await Use<PeopleService>(async x => await x.LinkAccountAsync(Owner, person, new((await x.GetAsync(person, default)).Version, user, Reason), default));
        return person;
    }

    private Task<string> Version(Guid year) => Use<AcademicService, string>(async x => (await x.YearAsync(year, default)).Year.Version);

    [Fact]
    public async Task Periods_are_generated_contiguously_once_and_only_by_academic_managers()
    {
        var count = await Use<AcademicService, int>(async x => await x.GeneratePeriodsAsync(Owner, source, new(await Version(source), 4), default));
        Assert.Equal(4, count);
        var periods = (await Use<AcademicService, YearDetail>(x => x.YearAsync(source, default))).Periods;
        Assert.Equal([1, 2, 3, 4], periods.Select(p => p.Sequence));
        Assert.Equal(Start, periods[0].StartsOn);
        Assert.Equal(new DateOnly(2026, 10, 30), periods[^1].EndsOn);
        for (var i = 1; i < periods.Length; i++) Assert.Equal(periods[i - 1].EndsOn.AddDays(1), periods[i].StartsOn);
        Assert.All(periods, p => Assert.False(string.IsNullOrWhiteSpace(p.Name)));
        Assert.True(periods.Max(p => p.EndsOn.DayNumber - p.StartsOn.DayNumber) - periods.Min(p => p.EndsOn.DayNumber - p.StartsOn.DayNumber) <= 1);

        Assert.Equal("periods-exist", (await Assert.ThrowsAsync<OperationException>(() =>
            Use<AcademicService, int>(async x => await x.GeneratePeriodsAsync(Owner, source, new(await Version(source), 4), default)))).Code);

        await Account("synthetic-reader", "registrar");
        using var reader = await SignIn("synthetic-reader");
        var target = await Use<AcademicService, Guid>(x => x.CreateYearAsync(Owner, new("2027", new(2027, 1, 11), new(2027, 10, 29)), default));
        var denied = await Post(reader, $"/api/v1/academic/years/{target}/periods/generate", new { version = await Version(target), count = 4 });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Empty((await Use<AcademicService, YearDetail>(x => x.YearAsync(target, default))).Periods);
    }

    [Fact]
    public async Task A_new_year_copies_sections_courses_and_current_teachers_from_the_previous_one()
    {
        await Use<CourseService, int>(x => x.CreateCoursesAsync(Owner, source, new(grade, null, [math]), default));
        await Use<CourseService, int>(x => x.CreateCoursesAsync(Owner, source, new(grade, sectionA, [science]), default));
        await Use<CourseService, int>(x => x.CreateCoursesAsync(Owner, source, new(grade, sectionB, [science, art]), default));
        var courses = await Use<CourseService, CourseView[]>(x => x.CoursesAsync(source, grade, default));
        var rosa = await Teacher("synthetic-rosa", "Rosa");
        var luis = await Teacher("synthetic-luis", "Luis");
        var mathCourse = courses.Single(c => c.SubjectId == math);
        await Use<CourseService, Guid>(x => x.AssignAsync(Owner, mathCourse.Id, new(rosa, TeachingRoles.Lead, Start, Reason), default));
        var ended = await Use<CourseService, Guid>(x => x.AssignAsync(Owner, courses.Single(c => c.SubjectId == science && c.SectionId == sectionA).Id,
            new(luis, TeachingRoles.Lead, Start, Reason), default));
        await Use<CourseService>(x => x.EndAssignmentAsync(Owner, courses.Single(c => c.SubjectId == science && c.SectionId == sectionA).Id, ended,
            new(new DateOnly(2026, 5, 1), Reason), default));
        // The old grade is retired: its sections are not carried over.
        await Use<AcademicService, Guid>(async x => await x.SaveGradeAsync(Owner, oldGrade,
            new("ANT", "Grado anterior", "Básico", 2, false, (await x.GradesAsync(default)).Single(g => g.Id == oldGrade).Version), default));
        // Art is no longer taught: courses of inactive subjects are skipped.
        await Use<CourseService, Guid>(async x => await x.SaveSubjectAsync(Owner, art,
            new("ART", "Arte", false, (await x.SubjectsAsync(default)).Single(s => s.Id == art).Version), default));

        var target = await Use<AcademicService, Guid>(x => x.CreateYearAsync(Owner, new("2027", new(2027, 1, 11), new(2027, 10, 29)), default));
        using var admin = await SignIn(OwnerName);
        var sections = await Post(admin, $"/api/v1/academic/years/{target}/sections/copy", new { sourceYearId = source });
        Assert.Equal(HttpStatusCode.OK, sections.StatusCode);
        Assert.Equal(2, (await sections.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("created").GetInt32());
        var copiedSections = (await Use<AcademicService, YearDetail>(x => x.YearAsync(target, default))).Sections;
        Assert.Equal(["A", "B"], copiedSections.Select(s => s.Name).Order());
        Assert.All(copiedSections, s => Assert.Equal(grade, s.GradeLevelId));
        Assert.Equal(30, copiedSections.Single(s => s.Name == "A").Capacity);
        Assert.Equal(0, await Use<AcademicService, int>(x => x.CopySectionsAsync(Owner, target, new(source), default)));

        var copy = await Post(admin, $"/api/v1/years/{target}/courses/copy", new { sourceYearId = source, teachers = true });
        Assert.Equal(HttpStatusCode.OK, copy.StatusCode);
        var result = await copy.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, result.GetProperty("created").GetInt32());
        Assert.Equal(1, result.GetProperty("skipped").GetInt32());
        Assert.Equal(1, result.GetProperty("teachers").GetInt32());

        var copied = await Use<CourseService, CourseView[]>(x => x.CoursesAsync(target, null, default));
        Assert.Equal(3, copied.Length);
        var newMath = copied.Single(c => c.SubjectId == math);
        Assert.Null(newMath.SectionId);
        Assert.Equal(rosa, Assert.Single(newMath.Teachers).StaffId);
        Assert.Equal(new DateOnly(2027, 1, 11), newMath.Teachers[0].StartsOn);
        Assert.All(copied.Where(c => c.SubjectId == science), c => Assert.Empty(c.Teachers));
        Assert.Equal(copiedSections.Select(s => s.Id).Order(), copied.Where(c => c.SubjectId == science).Select(c => c.SectionId!.Value).Order());

        var again = await Use<CourseService, CopyCoursesResult>(x => x.CopyCoursesAsync(Owner, target, new(source, true), default));
        Assert.Equal(0, again.Created);
        Assert.Equal(0, again.Teachers);

        using var teacher = await SignIn("synthetic-rosa");
        var mine = await teacher.GetFromJsonAsync<JsonElement>("/api/v1/me/courses");
        Assert.Contains(mine.EnumerateArray(), c => c.GetProperty("id").GetGuid() == newMath.Id);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Post(teacher, $"/api/v1/years/{target}/courses/copy", new { sourceYearId = source, teachers = false })).StatusCode);
    }
}
