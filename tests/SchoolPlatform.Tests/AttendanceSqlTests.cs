using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Attendance;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.People;

namespace SchoolPlatform.Tests;

public sealed class AttendanceSqlTests : SqlTestBase
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly DateOnly Start = Today.AddDays(-60);
    private Guid year, course, student, outsider, relationship, teacherUser;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        year = await Use<AcademicService, Guid>(x => x.CreateYearAsync(Owner, new("Ciclo asistencia", Start, Start.AddDays(300)), default));
        await Use<AcademicService, Guid>(x => x.AddPeriodAsync(Owner, year, new(null, Start, Start.AddDays(60)), default));
        await Use<AcademicService>(async x => await x.SetYearStatusAsync(Owner, year, new((await x.YearAsync(year, default)).Year.Version, YearStatus.EnrollmentOpen, Reason), default));
        var grade = await Use<AcademicService, Guid>(x => x.SaveGradeAsync(Owner, null, new("5P", "Quinto primaria", "Primaria", 1), default));
        var other = await Use<AcademicService, Guid>(x => x.SaveGradeAsync(Owner, null, new("6P", "Sexto primaria", "Primaria", 2), default));
        var subject = await Use<CourseService, Guid>(x => x.SaveSubjectAsync(Owner, null, new("CN", "Ciencias naturales"), default));
        await Use<CourseService, int>(x => x.CreateCoursesAsync(Owner, year, new(grade, null, [subject]), default));
        course = (await Use<CourseService, CourseView[]>(x => x.CoursesAsync(year, grade, default))).Single().Id;
        student = await Enrolled("Ada", "A-1", grade);
        outsider = await Enrolled("Leo", "A-2", other);
        var teacher = (await Use<PeopleService, CreatedPerson>(x => x.CreateAsync(Owner, new("Sara", "Docente", null, null, [], null, false, true), default))).Id;
        await Use<PeopleService>(async x => await x.AddRoleAsync(Owner, teacher, new((await x.GetAsync(teacher, default)).Version, "staff", null, "Docente"), default));
        teacherUser = await Account("synthetic-teacher");
        await Link(teacher, teacherUser);
        await Use<CourseService, Guid>(x => x.AssignAsync(Owner, course, new(teacher, TeachingRoles.Lead, Start, Reason), default));
        var guardian = (await Use<PeopleService, CreatedPerson>(x => x.CreateAsync(Owner, new("Rosa", "Encargada", null, null, [], null, true, false), default))).Id;
        relationship = await Use<PeopleService, Guid>(x => x.CreateRelationshipAsync(Owner, new(student, guardian, "Mother", null, [Faculties.AcademicRecords], Reason), default));
        await Link(guardian, await Account("synthetic-guardian"));
    }

    private async Task<Guid> Enrolled(string given, string code, Guid grade)
    {
        var id = (await Use<PeopleService, CreatedPerson>(x => x.CreateAsync(Owner, new(given, "Asistencia " + code, null, null, [], code, false, true), default))).Id;
        await Use<EnrollmentService, Guid>(x => x.EnrollAsync(Owner, new(id, year, grade, null, Start, null), default));
        return id;
    }
    private async Task Link(Guid person, Guid user) =>
        await Use<PeopleService>(async x => await x.LinkAccountAsync(Owner, person, new((await x.GetAsync(person, default)).Version, user, Reason), default));
    private Task<int> Save(DateOnly date, string status, string? version = null, Guid? who = null) =>
        Use<AttendanceService, int>(x => x.SaveAsync(teacherUser, course, new(date, [new(who ?? student, status, null, version)]), default));

    [Fact]
    public async Task Teacher_records_one_session_per_date_with_versions_and_limits()
    {
        Assert.Equal(1, await Save(Today.AddDays(-1), AttendanceStatus.Absent));
        Assert.Equal("resource-changed", (await Assert.ThrowsAsync<OperationException>(() => Save(Today.AddDays(-1), AttendanceStatus.Present))).Code);
        var sheet = (await Use<AttendanceService, AttendanceSheet?>(x => x.SheetAsync(teacherUser, course, Today.AddDays(-1), default)))!;
        var row = Assert.Single(sheet.Rows);
        Assert.Equal(AttendanceStatus.Absent, row.Status);
        Assert.Equal(1, await Save(Today.AddDays(-1), AttendanceStatus.Excused, row.Version));
        await Assert.ThrowsAsync<OperationException>(() => Save(Today.AddDays(1), AttendanceStatus.Present));
        await Assert.ThrowsAsync<OperationException>(() => Save(Start.AddDays(-1), AttendanceStatus.Present));
        await Assert.ThrowsAsync<OperationException>(() => Save(Today, AttendanceStatus.Present, who: outsider));
        await Assert.ThrowsAsync<OperationException>(() => Use<AttendanceService, int>(x =>
            x.SaveAsync(Guid.NewGuid(), course, new(Today, [new(student, AttendanceStatus.Present, null, null)]), default)));
    }

    [Fact]
    public async Task Families_with_the_faculty_see_a_summary_of_incidents()
    {
        await Save(Today.AddDays(-3), AttendanceStatus.Present);
        await Save(Today.AddDays(-2), AttendanceStatus.Late);
        await Save(Today.AddDays(-1), AttendanceStatus.Absent);
        using var guardian = await SignIn("synthetic-guardian");
        var summary = await guardian.GetFromJsonAsync<JsonElement>($"/api/v1/me/students/{student}/attendance");
        Assert.Equal((1, 1, 1), (summary.GetProperty("present").GetInt32(), summary.GetProperty("late").GetInt32(), summary.GetProperty("absent").GetInt32()));
        Assert.Equal(2, summary.GetProperty("incidents").GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await guardian.GetAsync($"/api/v1/me/students/{outsider}/attendance")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await guardian.GetAsync($"/api/v1/courses/{course}/attendance")).StatusCode);

        var version = (await Use<PeopleService, PersonDetail>(x => x.GetAsync(student, default))).Guardians.Single().Version;
        await Use<PeopleService>(x => x.UpdateRelationshipAsync(Owner, relationship, new(version, "Mother", [], Reason), default));
        Assert.Equal(HttpStatusCode.NotFound, (await guardian.GetAsync($"/api/v1/me/students/{student}/attendance")).StatusCode);
    }
}
