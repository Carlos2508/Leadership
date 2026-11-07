using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Tests;

public sealed class AcademicSqlTests : SqlTestBase
{
    private static readonly DateOnly Start = new(2027, 1, 11);
    private static readonly DateOnly End = new(2027, 10, 29);

    private Task<T> Academic<T>(Func<AcademicService, Task<T>> action) => Use(action);
    private Task Academic(Func<AcademicService, Task> action) => Use(action);
    private Task<T> Enrollment<T>(Func<EnrollmentService, Task<T>> action) => Use(action);
    private Task Enrollment(Func<EnrollmentService, Task> action) => Use(action);

    private async Task<Guid> Year(string label = "2027", bool open = true, DateOnly? start = null, DateOnly? end = null)
    {
        var s = start ?? Start;
        var e = end ?? End;
        var id = await Academic(x => x.CreateYearAsync(Owner, new(label, s, e), default));
        await Academic(x => x.AddPeriodAsync(Owner, id, new(null, s, s.AddDays(60)), default));
        if (open) await Status(id, YearStatus.EnrollmentOpen);
        return id;
    }
    private async Task Status(Guid id, string status) =>
        await Academic(async x => await x.SetYearStatusAsync(Owner, id, new((await x.YearAsync(id, default)).Year.Version, status, Reason), default));
    private Task<Guid> Grade(string code, string name, int sequence = 1) =>
        Academic(x => x.SaveGradeAsync(Owner, null, new(code, name, "Primaria", sequence), default));
    private Task<Guid> Section(Guid year, Guid grade, string name, int? capacity = null) =>
        Academic(x => x.SaveSectionAsync(Owner, year, null, new(grade, name, "Matutina", capacity), default));
    private async Task<Guid> Student(string code, string given = "Ana") =>
        (await Use<PeopleService, CreatedPerson>(x => x.CreateAsync(Owner, new(given, "Prueba " + code, null, null, [], code, false, true), default))).Id;
    private Task<Guid> Enroll(Guid student, Guid year, Guid grade, Guid? section = null, DateOnly? on = null) =>
        Enrollment(x => x.EnrollAsync(Owner, new(student, year, grade, section, on ?? Start, null), default));
    private async Task<EnrollmentHistory> History(Guid student) =>
        Assert.Single(await Enrollment(x => x.StudentHistoryAsync(student, default)));

    [Fact]
    public async Task Year_lifecycle_is_forward_only_with_periods_inside_its_dates()
    {
        var first = await Academic(x => x.CreateYearAsync(Owner, new("2027", Start, End), default));
        Assert.Equal("year-overlap", (await Assert.ThrowsAsync<OperationException>(() =>
            Academic(x => x.CreateYearAsync(Owner, new("2027-B", End.AddDays(-5), End.AddDays(200)), default)))).Code);
        var second = await Academic(x => x.CreateYearAsync(Owner, new("2028", new(2028, 1, 10), new(2028, 10, 30)), default));
        var years = await Academic(x => x.YearsAsync(default));
        Assert.Single(years, y => y.IsDefault);
        Assert.True(years.Single(y => y.Id == first).IsDefault);
        await Academic(async x => await x.SetDefaultYearAsync(Owner, second, new((await x.YearAsync(second, default)).Year.Version), default));
        Assert.True((await Academic(x => x.YearsAsync(default))).Single(y => y.Id == second).IsDefault);

        Assert.Equal("periods-required", (await Assert.ThrowsAsync<OperationException>(() => Status(first, YearStatus.Active))).Code);
        await Academic(x => x.AddPeriodAsync(Owner, first, new(null, Start, Start.AddDays(60)), default));
        await Assert.ThrowsAsync<OperationException>(() => Academic(x => x.AddPeriodAsync(Owner, first, new(null, Start.AddDays(30), Start.AddDays(90)), default)));
        await Assert.ThrowsAsync<OperationException>(() => Academic(x => x.AddPeriodAsync(Owner, first, new(null, End, End.AddDays(3)), default)));
        await Academic(x => x.AddPeriodAsync(Owner, first, new("Segundo", Start.AddDays(61), Start.AddDays(120)), default));
        var detail = await Academic(x => x.YearAsync(first, default));
        Assert.Equal(["Bimestre 1", "Segundo"], detail.Periods.Select(p => p.Name).ToArray());

        await Status(first, YearStatus.Active);
        Assert.Equal("invalid-transition", (await Assert.ThrowsAsync<OperationException>(() => Status(first, YearStatus.Draft))).Code);
        Assert.Equal("year-locked", (await Assert.ThrowsAsync<OperationException>(() =>
            Academic(x => x.AddPeriodAsync(Owner, first, new(null, Start.AddDays(121), Start.AddDays(150)), default)))).Code);
        await Status(first, YearStatus.Closed);
        Assert.Equal(YearStatus.Closed, (await Academic(x => x.YearAsync(first, default))).Year.Status);
        var events = await Use<SchoolDbContext, string[]>(db => db.AuditEvents.Where(x => x.TargetId == first).Select(x => x.Action).ToArrayAsync());
        Assert.Contains("AcademicYearStatusChanged", events);
    }

    [Fact]
    public async Task Enrollment_is_canonical_per_year_and_section_is_optional()
    {
        var year = await Year();
        var grade = await Grade("1P", "Primero primaria");
        var student = await Student("A-1");
        var enrollment = await Enroll(student, year, grade);
        var history = await History(student);
        var placement = Assert.Single(history.Placements);
        Assert.Null(placement.SectionId);
        Assert.Null(placement.EndsOn);
        Assert.Equal("already-enrolled", (await Assert.ThrowsAsync<OperationException>(() => Enroll(student, year, grade))).Code);
        Assert.Equal(1, await Use<SchoolDbContext, int>(db => db.Enrollments.CountAsync()));

        var other = await Grade("2P", "Segundo primaria", 2);
        var foreign = await Section(year, other, "A");
        var a2 = await Student("A-2");
        await Assert.ThrowsAsync<OperationException>(() => Enroll(a2, year, grade, foreign));
        var a3 = await Student("A-3");
        await Assert.ThrowsAsync<OperationException>(() => Enroll(a3, year, grade, null, End.AddDays(1)));

        var draft = await Year("2028", open: false, new(2028, 1, 10), new(2028, 10, 30));
        Assert.Equal("year-not-open", (await Assert.ThrowsAsync<OperationException>(() => Enroll(student, draft, grade, null, new(2028, 2, 1)))).Code);
        _ = enrollment;
    }

    [Fact]
    public async Task Placement_changes_keep_dated_history_and_withdrawal_can_be_reversed()
    {
        var year = await Year();
        var grade = await Grade("3P", "Tercero primaria");
        var a = await Section(year, grade, "a");
        var b = await Section(year, grade, "B");
        var student = await Student("H-1");
        var id = await Enroll(student, year, grade, a);
        var version = (await History(student)).Version;
        var moveOn = Start.AddDays(40);
        await Enrollment(x => x.ChangePlacementAsync(Owner, id, new(version, grade, b, moveOn, Reason), default));
        Assert.Equal("resource-changed", (await Assert.ThrowsAsync<OperationException>(() =>
            Enrollment(x => x.ChangePlacementAsync(Owner, id, new(version, grade, a, moveOn, Reason), default)))).Code);

        var history = await History(student);
        Assert.Equal(2, history.Placements.Length);
        Assert.Equal(("A", (DateOnly?)moveOn), (history.Placements[0].Section, history.Placements[0].EndsOn));
        Assert.Equal(("B", (DateOnly?)null), (history.Placements[1].Section, history.Placements[1].EndsOn));
        var roster = await Enrollment(x => x.ListAsync(year, null, b, null, null, 1, default));
        Assert.Equal(id, Assert.Single(roster.Items).Id);
        Assert.Empty((await Enrollment(x => x.ListAsync(year, null, a, null, null, 1, default))).Items);
        // Directory and cashier searches show the current grade and section to tell homonyms apart.
        async Task<string?> Shown() => Assert.Single((await Use<PeopleService, PagedResult<PersonSummary>>(x =>
            x.ListAsync("H-1", "student", null, 1, default))).Items).Placement;
        Assert.Equal("Tercero primaria · B", await Shown());

        await Assert.ThrowsAsync<OperationException>(() => Enrollment(x =>
            x.WithdrawAsync(Owner, id, new(history.Version, moveOn.AddDays(-1), Reason), default)));
        var withdrawOn = moveOn.AddDays(20);
        await Enrollment(x => x.WithdrawAsync(Owner, id, new(history.Version, withdrawOn, Reason), default));
        history = await History(student);
        Assert.Equal(EnrollmentStatus.Withdrawn, history.Status);
        Assert.Null(await Shown());
        Assert.All(history.Placements, p => Assert.NotNull(p.EndsOn));
        await Assert.ThrowsAsync<OperationException>(() => Enrollment(x =>
            x.ReactivateAsync(Owner, id, new(history.Version, grade, b, withdrawOn.AddDays(-1), Reason), default)));
        await Enrollment(x => x.ReactivateAsync(Owner, id, new(history.Version, grade, null, withdrawOn.AddDays(10), Reason), default));
        history = await History(student);
        Assert.Equal(EnrollmentStatus.Active, history.Status);
        Assert.Equal(3, history.Placements.Length);
        Assert.Null(history.Placements[2].SectionId);
        var actions = await Use<SchoolDbContext, string[]>(db => db.AuditEvents.Where(x => x.TargetId == id).Select(x => x.Action).ToArrayAsync());
        Assert.Equal(["EnrollmentPlacementChanged", "StudentEnrolled", "StudentReenrolled", "StudentWithdrawn"], actions.Order().ToArray());
    }

    [Fact]
    public async Task Section_capacity_and_concurrent_moves_are_enforced()
    {
        var year = await Year();
        var grade = await Grade("4P", "Cuarto primaria");
        var small = await Section(year, grade, "A", capacity: 1);
        var other = await Section(year, grade, "B");
        await Enroll(await Student("C-1"), year, grade, small);
        Assert.Equal("section-full", (await Assert.ThrowsAsync<OperationException>(async () => await Enroll(await Student("C-2"), year, grade, small))).Code);

        var student = await Student("C-3");
        var id = await Enroll(student, year, grade, other);
        var version = (await History(student)).Version;
        async Task<bool> Move(Guid? section)
        {
            try
            {
                await Enrollment(x => x.ChangePlacementAsync(Owner, id, new(version, grade, section, Start.AddDays(5), Reason), default));
                return true;
            }
            catch (Exception error) when (error is OperationException or DbUpdateConcurrencyException or DbUpdateException) { return false; }
        }
        var results = await Task.WhenAll(Move(null), Move(null));
        Assert.Single(results, x => x);
        Assert.Equal(1, await Use<SchoolDbContext, int>(db => db.Placements.CountAsync(p => p.EnrollmentId == id && p.EndsOn == null)));
    }

    [Fact]
    public async Task Self_service_shows_placement_only_to_the_student_or_academic_faculty()
    {
        var year = await Year();
        var grade = await Grade("5P", "Quinto primaria");
        var student = await Student("S-1", "Lucía");
        await Enroll(student, year, grade);
        var guardian = (await Use<PeopleService, CreatedPerson>(x => x.CreateAsync(Owner, new("Marta", "Encargada", null, null, [], null, true, false), default))).Id;
        var relationship = await Use<PeopleService, Guid>(x => x.CreateRelationshipAsync(Owner, new(student, guardian, "Mother", null, [], Reason), default));
        var guardianUser = await Account("synthetic-guardian");
        var studentUser = await Account("synthetic-student");
        await Use<PeopleService>(async x => await x.LinkAccountAsync(Owner, guardian, new((await x.GetAsync(guardian, default)).Version, guardianUser, Reason), default));
        await Use<PeopleService>(async x => await x.LinkAccountAsync(Owner, student, new((await x.GetAsync(student, default)).Version, studentUser, Reason), default));

        using var guardianBrowser = await SignIn("synthetic-guardian");
        var view = await guardianBrowser.GetFromJsonAsync<JsonElement>($"/api/v1/me/students/{student}");
        Assert.Equal(JsonValueKind.Null, view.GetProperty("placement").ValueKind);
        var version = (await Use<PeopleService, PersonDetail>(x => x.GetAsync(student, default))).Guardians.Single().Version;
        await Use<PeopleService>(x => x.UpdateRelationshipAsync(Owner, relationship, new(version, "Mother", [Faculties.AcademicRecords], Reason), default));
        view = await guardianBrowser.GetFromJsonAsync<JsonElement>($"/api/v1/me/students/{student}");
        Assert.Equal("Quinto primaria", view.GetProperty("placement").GetProperty("grade").GetString());
        var home = await guardianBrowser.GetFromJsonAsync<JsonElement>("/api/v1/me");
        Assert.Equal("2027", home.GetProperty("students")[0].GetProperty("placement").GetProperty("year").GetString());

        using var studentBrowser = await SignIn("synthetic-student");
        var own = await studentBrowser.GetFromJsonAsync<JsonElement>("/api/v1/me");
        Assert.Equal("Quinto primaria", own.GetProperty("placement").GetProperty("grade").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await studentBrowser.GetAsync($"/api/v1/years/{year}/enrollments")).StatusCode);
    }

    [Fact]
    public async Task Academic_and_enrollment_permissions_are_separate()
    {
        var year = await Year();
        var grade = await Grade("6P", "Sexto primaria");
        var student = await Student("P-1");
        await Account("synthetic-registrar", "registrar");
        await Account("synthetic-coordinator", "academic-coordinator");

        using var registrar = await SignIn("synthetic-registrar");
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(registrar, "/api/v1/academic/years",
            new YearRequest("2030", new(2030, 1, 1), new(2030, 10, 1)))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(registrar, "/api/v1/enrollments",
            new EnrollRequest(student, year, grade, null, Start, null))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await registrar.GetAsync($"/api/v1/years/{year}/enrollments?search=P-1")).StatusCode);

        using var coordinator = await SignIn("synthetic-coordinator");
        Assert.Equal(HttpStatusCode.OK, (await Post(coordinator, "/api/v1/academic/years",
            new YearRequest("2030", new(2030, 1, 1), new(2030, 10, 1)))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(coordinator, "/api/v1/enrollments",
            new EnrollRequest(await Student("P-2"), year, grade, null, Start, null))).StatusCode);
        var page = await coordinator.GetFromJsonAsync<JsonElement>($"/api/v1/years/{year}/enrollments");
        Assert.Equal(1, page.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Malformed_request_bodies_are_client_errors_not_server_errors()
    {
        var year = await Year(open: false);
        using var browser = await SignIn(OwnerName);
        var response = await Post(browser, $"/api/v1/academic/years/{year}/periods", new { name = (string?)null, startsOn = "", endsOn = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid-request", problem.GetProperty("code").GetString());
        Assert.DoesNotContain("DateOnly", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Readiness_reports_a_migrated_database_without_details()
    {
        var response = await App.Browser().GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Audit_can_be_filtered_by_area()
    {
        await Year();
        using var admin = await SignIn(OwnerName);
        var academic = await admin.GetFromJsonAsync<JsonElement>("/api/v1/audit?area=academic");
        Assert.True(academic.GetProperty("total").GetInt32() >= 3);
        Assert.All(academic.GetProperty("items").EnumerateArray(), x => Assert.Equal("AcademicYear", x.GetProperty("targetType").GetString()));
        var access = await admin.GetFromJsonAsync<JsonElement>("/api/v1/audit?area=access");
        Assert.All(access.GetProperty("items").EnumerateArray(), x => Assert.Equal(JsonValueKind.Null, x.GetProperty("targetType").ValueKind));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/audit?area=unknown")).StatusCode);
    }
}
