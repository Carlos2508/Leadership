using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Tests;

public abstract class FamilyRegistrationTestBase : SqlTestBase
{
    protected Guid Year, Grade, Section;
    protected static readonly DateOnly Starts = new(2027, 1, 10);
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        Year = await Use<AcademicService, Guid>(s => s.CreateYearAsync(Owner, new("Familias 2027", Starts, new(2027, 11, 30)), default));
        await Use<AcademicService>(async s => await s.SetYearStatusAsync(Owner, Year, new((await s.YearAsync(Year, default)).Year.Version, YearStatus.EnrollmentOpen, Reason), default));
        Grade = await Use<AcademicService, Guid>(s => s.SaveGradeAsync(Owner, null, new("FAM", "Grado de prueba", "Primaria", 1), default));
        Section = await Use<AcademicService, Guid>(s => s.SaveSectionAsync(Owner, Year, null, new(Grade, "A", null, 10), default));
    }
    protected static FamilyPersonInput NewPerson(string given, string? code = null) => new(null, null, given, "Familia sintética", null, null, null, code);
    protected RegisterFamilyRequest Request() => new(Guid.NewGuid(), Year, Starts,
        [new(NewPerson("Madre"))],
        [new(NewPerson("Ana", "FAM-1"), Grade, Section, [new(0, "Mother", [Faculties.AcademicRecords])]),
         new(NewPerson("Luis", "FAM-2"), Grade, Section, [new(0, "Mother", [])])], Reason);
    protected Task<RegisteredFamily> Register(RegisterFamilyRequest request) => Use<FamilyRegistrationService, RegisteredFamily>(s => s.RegisterAsync(Owner, request, default));
}

public sealed class FamilyRegistrationSqlTests : FamilyRegistrationTestBase
{
    [Fact]
    public async Task One_family_saves_people_links_and_enrollments_atomically_without_creating_accounts_or_implicit_faculties()
    {
        var request = Request(); var result = await Register(request);
        Assert.Equal(2, result.Students.Length); Assert.Single(result.GuardianIds);
        await Use<SchoolDbContext>(async db =>
        {
            Assert.Equal(3, await db.Persons.CountAsync()); Assert.Equal(2, await db.Enrollments.CountAsync(e => e.AcademicYearId == Year));
            Assert.Equal(2, await db.Relationships.CountAsync()); Assert.Equal(Faculties.AcademicRecords, (await db.RelationshipFaculties.SingleAsync()).Faculty);
            Assert.Equal(1, await db.Accounts.CountAsync()); Assert.Empty(await db.AccountLinks.ToListAsync());
            var receipt = await db.FamilyRegistrations.SingleAsync(); Assert.DoesNotContain("Familia sintética", receipt.ResultJson);
            Assert.Single(await db.AuditEvents.Where(a => a.Action == "FamilyRegistered").ToArrayAsync());
        });
        var replay = await Register(request);
        Assert.Equal(result.Students.Select(s => s.PersonId), replay.Students.Select(s => s.PersonId));
        Assert.Equal(409, (await Assert.ThrowsAsync<OperationException>(() => Register(request with { Reason = "Otro motivo de registro" }))).Status);
        Assert.Equal(3, await Use<SchoolDbContext, int>(db => db.Persons.CountAsync()));
    }

    [Fact]
    public async Task Concurrent_retries_return_the_same_family_without_duplicate_people_or_enrollment()
    {
        var request = Request();
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Register(request)));
        Assert.All(results, r => Assert.Equal(results[0].GuardianIds, r.GuardianIds));
        Assert.Equal(3, await Use<SchoolDbContext, int>(db => db.Persons.CountAsync()));
        Assert.Equal(1, await Use<SchoolDbContext, int>(db => db.FamilyRegistrations.CountAsync()));
    }

    [Fact]
    public async Task Capacity_failure_on_second_child_rolls_back_first_child_people_links_and_audit()
    {
        await Use<AcademicService, Guid>(async s => await s.SaveSectionAsync(Owner, Year, Section,
            new(Grade, "A", null, 1, (await s.YearAsync(Year, default)).Sections.Single().Version), default));
        var before = await Use<SchoolDbContext, int>(db => db.AuditEvents.CountAsync());
        Assert.Equal("section-full", (await Assert.ThrowsAsync<OperationException>(() => Register(Request()))).Code);
        await Use<SchoolDbContext>(async db =>
        {
            Assert.Empty(await db.Persons.ToArrayAsync()); Assert.Empty(await db.Enrollments.ToArrayAsync());
            Assert.Empty(await db.Relationships.ToArrayAsync()); Assert.Empty(await db.FamilyRegistrations.ToArrayAsync());
            Assert.Equal(before, await db.AuditEvents.CountAsync());
        });
    }

    [Fact]
    public async Task Existing_people_and_relationships_are_reused_without_changing_their_data_or_permissions()
    {
        var guardian = (await Use<PeopleService, CreatedPerson>(s => s.CreateAsync(Owner, new("Madre", "Existente", null, null, [], null, true, true), default))).Id;
        var student = (await Use<PeopleService, CreatedPerson>(s => s.CreateAsync(Owner, new("Ana", "Existente", null, null, [], "EX-1", false, true), default))).Id;
        var relationship = await Use<PeopleService, Guid>(s => s.CreateRelationshipAsync(Owner, new(student, guardian, "Mother", null, [Faculties.Finance], Reason), default));
        var parentDetail = await Use<PeopleService, PersonDetail>(s => s.GetAsync(guardian, default));
        var studentDetail = await Use<PeopleService, PersonDetail>(s => s.GetAsync(student, default));
        var request = new RegisterFamilyRequest(Guid.NewGuid(), Year, Starts, [new(new(guardian, parentDetail.Version, null, null, null, null, null, null))],
            [new(new(student, studentDetail.Version, null, null, null, null, null, null), Grade, Section, [new(0, "Mother", [Faculties.Finance])])], Reason);
        var result = await Register(request); Assert.Equal(relationship, Assert.Single(result.Students[0].RelationshipIds));
        Assert.Equal(2, await Use<SchoolDbContext, int>(db => db.Persons.CountAsync()));
        Assert.Equal(parentDetail.Version, (await Use<PeopleService, PersonDetail>(s => s.GetAsync(guardian, default))).Version);
        var again = request with { RequestId = Guid.NewGuid() };
        Assert.Equal("already-enrolled", (await Assert.ThrowsAsync<OperationException>(() => Register(again))).Code);
    }

    [Fact]
    public async Task Duplicate_people_or_student_codes_require_review_and_do_not_leave_half_a_family()
    {
        var request = Request();
        request = request with { Students = [request.Students[0], request.Students[1] with { Person = request.Students[1].Person with { StudentCode = "FAM-1" } }] };
        await Assert.ThrowsAsync<OperationException>(() => Register(request));
        Assert.Equal(0, await Use<SchoolDbContext, int>(db => db.Persons.CountAsync()));
        request = Request();
        request = request with { Students = [request.Students[0], request.Students[1] with { Person = request.Students[1].Person with { GivenNames = "Ana" } }] };
        var conflict = await Assert.ThrowsAsync<OperationException>(() => Register(request));
        Assert.Equal("possible-duplicate", conflict.Code); Assert.StartsWith("Estudiante 2:", conflict.Message);
        Assert.Equal(0, await Use<SchoolDbContext, int>(db => db.Persons.CountAsync()));
    }

    [Fact]
    public async Task A_student_can_be_registered_without_guardians_and_invalid_payloads_do_not_write()
    {
        var request = Request();
        Assert.Equal(400, (await Assert.ThrowsAsync<OperationException>(() => Register(request with { Guardians = [null!] }))).Status);
        Assert.Equal(400, (await Assert.ThrowsAsync<OperationException>(() => Register(request with { EnrolledOn = Starts.AddDays(-1) }))).Status);
        var alone = request with { Guardians = [], Students = [request.Students[0] with { Guardians = [] }] };
        var result = await Register(alone); Assert.Empty(result.GuardianIds); Assert.Empty(result.Students[0].RelationshipIds);
    }

    [Fact]
    public async Task Http_enforces_active_session_all_permissions_csrf_and_keeps_request_retries_safe()
    {
        using var anonymous = App.Browser();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/v1/people/families", Request())).StatusCode);
        var key = await Use<AccessRoleService, string>(s => s.SaveAsync(Owner, null, new("Fichas sin matrícula", "Prueba", [Permissions.PeopleRead, Permissions.PeopleManage, Permissions.AcademicRead], true, Reason), default));
        await Account("family-no-enroll", key); using var limited = await SignIn("family-no-enroll");
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(limited, "/api/v1/people/families", Request())).StatusCode);
        using var browser = await SignIn(OwnerName); var request = Request();
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsJsonAsync("/api/v1/people/families", request)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(browser, "/api/v1/people/families", request)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(browser, "/api/v1/people/families", request)).StatusCode);
        Assert.Equal(2, await Use<SchoolDbContext, int>(db => db.Enrollments.CountAsync()));
    }
}

public sealed class FamilyRegistrationMfaSqlTests : FamilyRegistrationTestBase
{
    protected override Dictionary<string, string?> ExtraConfiguration => new() { ["AccountAccess:RequireMfaForPermissions:0"] = Permissions.EnrollmentManage };
    [Fact]
    public async Task Family_registration_cannot_bypass_the_enrollment_mfa_policy()
    {
        using var browser = await SignIn(OwnerName);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(browser, "/api/v1/people/families", Request())).StatusCode);
        Assert.Equal(0, await Use<SchoolDbContext, int>(db => db.Persons.CountAsync()));
    }
}
