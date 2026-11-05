using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Tests;

[Trait("Category", "SqlServer")]
public sealed class PeopleSqlTests : IAsyncLifetime
{
    private readonly string database = "SchoolPlatform_Test_" + Guid.NewGuid().ToString("N");
    private TestApplication app = null!;
    private Guid owner;
    private const string OwnerName = "synthetic-owner";
    private const string Password = "Synthetic-Only!12345";
    private const string Reason = "Verificación institucional sintética de prueba.";

    public async Task InitializeAsync()
    {
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("SCHOOLPLATFORM_TEST_SQL") ??
            "Server=localhost;Integrated Security=true;Encrypt=true;TrustServerCertificate=true") { InitialCatalog = database };
        app = new TestApplication(configuration: new() { ["ConnectionStrings:SchoolDatabase"] = connection.ConnectionString });
        await app.MigrateOwnedDatabaseAsync(database);
        await using var scope = app.Services.CreateAsyncScope();
        owner = await scope.ServiceProvider.GetRequiredService<AccountAccessService>().ProvisionOwnerAsync(OwnerName, Password);
        // The owner starts with account/audit management only; grant the institutional profile for these tests.
        var version = await Identity(db => db.Users.Where(x => x.Id == owner).Select(x => x.ConcurrencyStamp!).SingleAsync());
        await Access(x => x.ChangeProfileAsync(owner, owner, new(version, Reason, "administrator")));
    }

    public async Task DisposeAsync()
    {
        if (app is null) return;
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        if (!database.StartsWith("SchoolPlatform_Test_", StringComparison.Ordinal) || db.Database.GetDbConnection().Database != database)
            throw new InvalidOperationException("Refusing to delete a database not owned by this test.");
        await db.Database.EnsureDeletedAsync();
        await app.DisposeAsync();
    }

    private async Task<T> People<T>(Func<PeopleService, Task<T>> action)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<PeopleService>());
    }
    private async Task People(Func<PeopleService, Task> action)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<PeopleService>());
    }
    private async Task Access(Func<AccountAccessService, Task> action)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<AccountAccessService>());
    }
    private async Task<T> Identity<T>(Func<IdentityDbContext, Task<T>> action)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IdentityDbContext>());
    }
    private async Task<T> School<T>(Func<SchoolDbContext, Task<T>> action)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<SchoolDbContext>());
    }

    private Task<CreatedPerson> Create(string given, string family, string? code = null, bool guardian = false, bool confirm = false) =>
        People(x => x.CreateAsync(owner, new(given, family, null, new DateOnly(2014, 3, 9),
            [new(ContactKinds.Phone, "+502 5555-0000", "Casa", true)], code, guardian, confirm), default));
    private Task<PersonDetail> Detail(Guid id) => People(x => x.GetAsync(id, default));
    private async Task<Guid> Link(Guid student, Guid guardian, params string[] faculties) =>
        await People(x => x.CreateRelationshipAsync(owner, new(student, guardian, "Mother", null, faculties, Reason), default));

    private async Task<Guid> Account(string username, string profile = "basic")
    {
        await using var scope = app.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<AccountAccessService>();
        var ticket = await service.InviteAsync(owner, new(username, profile, Reason));
        await service.RedeemAsync(ticket.Code, Password);
        return ticket.UserId;
    }
    private async Task LinkAccount(Guid person, Guid user) =>
        await People(async x => await x.LinkAccountAsync(owner, person, new((await Detail(person)).Version, user, Reason), default));

    private async Task<HttpClient> SignIn(string username)
    {
        var browser = app.Browser();
        var html = await browser.GetStringAsync("/Identity/Account/Login");
        var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        var response = await browser.PostAsync("/Identity/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["Input.Username"] = username, ["Input.Password"] = Password, ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return browser;
    }
    private static async Task<HttpResponseMessage> Post(HttpClient browser, string path, object body)
    {
        var token = (await browser.GetFromJsonAsync<JsonElement>("/api/v1/antiforgery")).GetProperty("token").GetString();
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-XSRF-TOKEN", token);
        return await browser.SendAsync(request);
    }

    [Fact]
    public async Task Person_roles_relationships_and_audit_are_stored_separately_from_accounts()
    {
        var student = await Create("Ana Lucía", "Pérez Gómez", code: "est-001");
        var guardian = await Create("María", "Gómez", guardian: true);
        var relationship = await Link(student.Id, guardian.Id, Faculties.AcademicRecords, Faculties.Pickup);

        var detail = await Detail(student.Id);
        Assert.Equal("EST-001", detail.StudentCode);
        Assert.Null(detail.Account);
        var view = Assert.Single(detail.Guardians);
        Assert.Equal(guardian.Id, view.PersonId);
        Assert.Equal([Faculties.AcademicRecords, Faculties.Pickup], view.Faculties);
        Assert.Equal(student.Id, Assert.Single((await Detail(guardian.Id)).Students).PersonId);
        Assert.Equal(0, await Identity(db => db.Users.CountAsync(x => x.Id != owner)));

        var events = await Identity(db => db.AuditEvents.Where(x => x.TargetType != null).ToArrayAsync());
        Assert.Contains(events, x => x.Action == "PersonCreated" && x.TargetId == student.Id && x.TargetType == "Person");
        Assert.Contains(events, x => x.Action == "GuardianLinked" && x.TargetId == relationship && x.Reason == Reason);
        Assert.DoesNotContain("Pérez", JsonSerializer.Serialize(events));
    }

    [Fact]
    public async Task Duplicates_require_confirmation_and_student_codes_are_unique()
    {
        await Create("Luis", "Ramírez", code: "A1");
        var duplicate = await Assert.ThrowsAsync<OperationException>(() => Create("Luis", "Ramírez"));
        Assert.Equal("possible-duplicate", duplicate.Code);
        await Create("Luis", "Ramírez", confirm: true);
        Assert.Equal("student-code-taken", (await Assert.ThrowsAsync<OperationException>(() => Create("Otro", "Nombre", code: "a1"))).Code);
        Assert.Equal(2, await School(db => db.Persons.CountAsync(x => x.FamilyNames == "Ramírez")));
        Assert.Equal(400, (await Assert.ThrowsAsync<OperationException>(() => People(x => x.CreateAsync(owner,
            new("Ana", "López", null, null, [new(ContactKinds.Email, "no-es-correo", null, false)], null, false, false), default)))).Status);
    }

    [Fact]
    public async Task Faculty_history_is_preserved_and_ending_revokes_access_without_deleting_history()
    {
        var student = await Create("Sofía", "Méndez", code: "S-10");
        var guardian = await Create("Carlos", "Méndez", guardian: true);
        var id = await Link(student.Id, guardian.Id, Faculties.Finance);
        var view = Assert.Single((await Detail(student.Id)).Guardians);
        await People(x => x.UpdateRelationshipAsync(owner, id, new(view.Version, "Father", [Faculties.AcademicRecords], Reason), default));
        await Assert.ThrowsAsync<OperationException>(() => People(x =>
            x.UpdateRelationshipAsync(owner, id, new(view.Version, "Father", [], Reason), default)));
        view = Assert.Single((await Detail(student.Id)).Guardians);
        Assert.Equal("Father", view.Kind);
        Assert.Equal([Faculties.AcademicRecords], view.Faculties);
        await People(x => x.EndRelationshipAsync(owner, id, new(view.Version, Reason), default));
        var rows = await School(db => db.RelationshipFaculties.Where(x => x.RelationshipId == id).ToArrayAsync());
        Assert.Equal(2, rows.Length);
        Assert.All(rows, x => Assert.NotNull(x.RevokedAt));
        Assert.Equal("relationship-exists", (await Assert.ThrowsAsync<OperationException>(async () =>
        {
            var again = await Link(student.Id, guardian.Id);
            await Link(student.Id, guardian.Id);
            _ = again;
        })).Code);
        Assert.Equal(2, (await Detail(student.Id)).Guardians.Length);
    }

    [Fact]
    public async Task Stale_versions_are_rejected_and_archived_people_cannot_be_linked()
    {
        var person = await Create("Elena", "Castillo", code: "C-1");
        var stale = (await Detail(person.Id)).Version;
        await People(x => x.UpdateAsync(owner, person.Id, new(stale, "Elena María", "Castillo", null, null, []), default));
        Assert.Equal("resource-changed", (await Assert.ThrowsAsync<OperationException>(() => People(x =>
            x.UpdateAsync(owner, person.Id, new(stale, "Otro", "Castillo", null, null, []), default)))).Code);
        var guardian = await Create("Jorge", "Castillo", guardian: true);
        await Link(person.Id, guardian.Id);
        Assert.Equal("active-relationships", (await Assert.ThrowsAsync<OperationException>(async () => await People(async x =>
            await x.SetStatusAsync(owner, guardian.Id, new((await Detail(guardian.Id)).Version, PersonStatus.Archived, Reason), default)))).Code);
        var other = await Create("Pablo", "Castillo", code: "C-2");
        await People(async x => await x.SetStatusAsync(owner, other.Id, new((await Detail(other.Id)).Version, PersonStatus.Archived, Reason), default));
        await Assert.ThrowsAsync<OperationException>(() => Link(other.Id, guardian.Id));
        Assert.Equal("Elena María Castillo", (await Detail(person.Id)).DisplayName);
    }

    [Fact]
    public async Task Linked_accounts_see_only_their_own_or_authorized_students()
    {
        var student = await Create("Diego", "Arriola", code: "D-1");
        var sibling = await Create("Paula", "Arriola", code: "D-2");
        var stranger = await Create("Marco", "Solís", code: "D-3");
        var guardian = await Create("Rosa", "Arriola", guardian: true);
        var relationship = await Link(student.Id, guardian.Id, Faculties.AcademicRecords);
        await Link(sibling.Id, guardian.Id);

        var studentUser = await Account("synthetic-student");
        var guardianUser = await Account("synthetic-guardian");
        await LinkAccount(student.Id, studentUser);
        await LinkAccount(guardian.Id, guardianUser);
        Assert.Equal("account-linked", (await Assert.ThrowsAsync<OperationException>(() => LinkAccount(stranger.Id, studentUser))).Code);

        using var studentBrowser = await SignIn("synthetic-student");
        var me = await studentBrowser.GetFromJsonAsync<JsonElement>("/api/v1/me");
        Assert.Equal("D-1", me.GetProperty("person").GetProperty("studentCode").GetString());
        Assert.Equal(HttpStatusCode.OK, (await studentBrowser.GetAsync($"/api/v1/me/students/{student.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await studentBrowser.GetAsync($"/api/v1/me/students/{sibling.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await studentBrowser.GetAsync("/api/v1/people")).StatusCode);

        using var guardianBrowser = await SignIn("synthetic-guardian");
        var guardianView = await guardianBrowser.GetFromJsonAsync<JsonElement>("/api/v1/me");
        Assert.Equal(2, guardianView.GetProperty("students").GetArrayLength());
        var access = await guardianBrowser.GetFromJsonAsync<JsonElement>($"/api/v1/me/students/{student.Id}");
        Assert.Equal(Faculties.AcademicRecords, access.GetProperty("faculties")[0].GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await guardianBrowser.GetAsync($"/api/v1/me/students/{stranger.Id}")).StatusCode);

        var version = Assert.Single((await Detail(student.Id)).Guardians).Version;
        await People(x => x.EndRelationshipAsync(owner, relationship, new(version, Reason), default));
        Assert.Equal(HttpStatusCode.NotFound, (await guardianBrowser.GetAsync($"/api/v1/me/students/{student.Id}")).StatusCode);

        await People(async x => await x.UnlinkAccountAsync(owner, student.Id, new((await Detail(student.Id)).Version, Reason), default));
        Assert.Equal(JsonValueKind.Null, (await studentBrowser.GetFromJsonAsync<JsonElement>("/api/v1/me")).GetProperty("person").ValueKind);
        Assert.Equal(2, await School(db => db.AccountLinks.CountAsync()));
    }

    [Fact]
    public async Task People_permissions_are_enforced_by_server_policies()
    {
        await Account("synthetic-reader", "people-reader");
        await Account("synthetic-registrar", "registrar");
        var person = await Create("Lucía", "Herrera", code: "H-1");

        using var reader = await SignIn("synthetic-reader");
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync("/api/v1/people?search=Herrera")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(reader, "/api/v1/people",
            new CreatePersonRequest("X", "Y", null, null, [], null, false, false))).StatusCode);

        using var registrar = await SignIn("synthetic-registrar");
        Assert.Equal(HttpStatusCode.BadRequest, (await registrar.PostAsJsonAsync("/api/v1/people",
            new CreatePersonRequest("X", "Y", null, null, [], null, false, false))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(registrar, "/api/v1/people",
            new CreatePersonRequest("Nuevo", "Registro", null, null, [], "R-9", false, false))).StatusCode);
        var user = await Account("synthetic-target");
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(registrar, $"/api/v1/people/{person.Id}/account",
            new LinkAccountRequest((await Detail(person.Id)).Version, user, Reason))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await registrar.GetAsync("/api/v1/people/linkable-accounts")).StatusCode);

        var page = await reader.GetFromJsonAsync<JsonElement>("/api/v1/people?search=h-1");
        Assert.Equal(1, page.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Browser().GetAsync("/api/v1/people")).StatusCode);
    }
}
