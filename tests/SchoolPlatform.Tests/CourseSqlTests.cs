using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.People;

namespace SchoolPlatform.Tests;

public sealed class CourseSqlTests : SqlTestBase
{
    private static readonly DateOnly Start = new(2026, 1, 12);
    private Guid year, grade, sectionA, sectionB, math, science;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        year = await Use<AcademicService, Guid>(x => x.CreateYearAsync(Owner, new("2026", Start, new(2026, 10, 30)), default));
        await Use<AcademicService, Guid>(x => x.AddPeriodAsync(Owner, year, new(null, Start, Start.AddDays(60)), default));
        await Use<AcademicService>(async x => await x.SetYearStatusAsync(Owner, year, new((await x.YearAsync(year, default)).Year.Version, YearStatus.EnrollmentOpen, Reason), default));
        grade = await Use<AcademicService, Guid>(x => x.SaveGradeAsync(Owner, null, new("2B", "Segundo básico", "Básico", 1), default));
        sectionA = await Use<AcademicService, Guid>(x => x.SaveSectionAsync(Owner, year, null, new(grade, "A", null, null), default));
        sectionB = await Use<AcademicService, Guid>(x => x.SaveSectionAsync(Owner, year, null, new(grade, "B", null, null), default));
        math = await Use<CourseService, Guid>(x => x.SaveSubjectAsync(Owner, null, new("MAT", "Matemática"), default));
        science = await Use<CourseService, Guid>(x => x.SaveSubjectAsync(Owner, null, new("CIE", "Ciencias"), default));
    }

    private async Task<Guid> Student(string code, Guid section)
    {
        var id = (await Use<PeopleService, CreatedPerson>(x => x.CreateAsync(Owner, new("Est", "Curso " + code, null, null, [], code, false, true), default))).Id;
        await Use<EnrollmentService, Guid>(x => x.EnrollAsync(Owner, new(id, year, grade, section, Start, null), default));
        return id;
    }

    private async Task<Guid> Teacher(string username, string given)
    {
        var person = (await Use<PeopleService, CreatedPerson>(x => x.CreateAsync(Owner, new(given, "Docente", null, null, [], null, false, true), default))).Id;
        await Use<PeopleService>(async x => await x.AddRoleAsync(Owner, person, new((await x.GetAsync(person, default)).Version, "staff", null, "Docente"), default));
        var user = await Account(username);
        await Use<PeopleService>(async x => await x.LinkAccountAsync(Owner, person, new((await x.GetAsync(person, default)).Version, user, Reason), default));
        return person;
    }

    private async Task<CourseView> Course(Guid subject, Guid? section) =>
        (await Use<CourseService, CourseView[]>(x => x.CoursesAsync(year, grade, default))).Single(c => c.SubjectId == subject && c.SectionId == section);

    [Fact]
    public async Task Courses_are_created_once_per_scope_and_rosters_follow_dated_placements()
    {
        Assert.Equal(1, await Use<CourseService, int>(x => x.CreateCoursesAsync(Owner, year, new(grade, null, [math]), default)));
        Assert.Equal(0, await Use<CourseService, int>(x => x.CreateCoursesAsync(Owner, year, new(grade, null, [math]), default)));
        Assert.Equal("course-scope-conflict", (await Assert.ThrowsAsync<OperationException>(() =>
            Use<CourseService, int>(x => x.CreateCoursesAsync(Owner, year, new(grade, sectionA, [math]), default)))).Code);
        Assert.Equal(2, await Use<CourseService, int>(x => x.CreateCoursesAsync(Owner, year, new(grade, sectionA, [science]), default) )
            + await Use<CourseService, int>(x => x.CreateCoursesAsync(Owner, year, new(grade, sectionB, [science]), default)));

        var mover = await Student("M-1", sectionA);
        await Student("M-2", sectionB);
        var moveOn = Start.AddDays(30);
        var history = Assert.Single(await Use<EnrollmentService, EnrollmentHistory[]>(x => x.StudentHistoryAsync(mover, default)));
        await Use<EnrollmentService>(x => x.ChangePlacementAsync(Owner, history.Id, new(history.Version, grade, sectionB, moveOn, Reason), default));

        var scienceA = await Course(science, sectionA);
        var scienceB = await Course(science, sectionB);
        var wholeMath = await Course(math, null);
        async Task<Guid[]> Roster(CourseView course, DateOnly on) =>
            (await Use<CourseService, CourseRoster>(x => x.RosterAsync(course.Id, on, default))).Students.Select(s => s.StudentId).ToArray();
        Assert.Contains(mover, await Roster(scienceA, moveOn.AddDays(-1)));
        Assert.DoesNotContain(mover, await Roster(scienceA, moveOn));
        Assert.Contains(mover, await Roster(scienceB, moveOn));
        Assert.Equal(2, (await Roster(wholeMath, moveOn.AddDays(-1))).Length);
        Assert.Equal(2, (await Roster(wholeMath, moveOn)).Length);
        Assert.Equal(2, (await Course(science, sectionB)).Students);
    }

    [Fact]
    public async Task Teachers_see_only_courses_with_an_active_assignment()
    {
        await Use<CourseService, int>(x => x.CreateCoursesAsync(Owner, year, new(grade, sectionA, [math, science]), default));
        await Student("T-1", sectionA);
        var mathA = await Course(math, sectionA);
        var scienceA = await Course(science, sectionA);
        var teacher = await Teacher("synthetic-teacher", "Rosa");
        await Teacher("synthetic-other", "Luis");
        var assignment = await Use<CourseService, Guid>(x => x.AssignAsync(Owner, mathA.Id, new(teacher, TeachingRoles.Lead, Start, Reason), default));
        Assert.Equal("already-assigned", (await Assert.ThrowsAsync<OperationException>(() =>
            Use<CourseService, Guid>(x => x.AssignAsync(Owner, mathA.Id, new(teacher, TeachingRoles.Lead, Start, Reason), default)))).Code);

        using var browser = await SignIn("synthetic-teacher");
        var mine = await browser.GetFromJsonAsync<JsonElement>("/api/v1/me/courses");
        Assert.Equal(mathA.Id, Assert.Single(mine.EnumerateArray()).GetProperty("id").GetGuid());
        var roster = await browser.GetFromJsonAsync<JsonElement>($"/api/v1/courses/{mathA.Id}/roster");
        Assert.Equal(1, roster.GetProperty("students").GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync($"/api/v1/courses/{scienceA.Id}/roster")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await browser.GetAsync($"/api/v1/years/{year}/courses")).StatusCode);

        using var other = await SignIn("synthetic-other");
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/courses/{mathA.Id}/roster")).StatusCode);

        await Use<CourseService>(x => x.EndAssignmentAsync(Owner, mathA.Id, assignment, new(new DateOnly(2026, 1, 31), Reason), default));
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync($"/api/v1/courses/{mathA.Id}/roster")).StatusCode);
        Assert.Empty((await browser.GetFromJsonAsync<JsonElement>("/api/v1/me/courses")).EnumerateArray());

        using var admin = await SignIn(OwnerName);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/courses/{scienceA.Id}/roster")).StatusCode);
    }
}
