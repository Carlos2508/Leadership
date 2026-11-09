using System.Security.Claims;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.IdentityAccess;

namespace SchoolPlatform.Host.People;

public static class PeopleEndpoints
{
    public static void MapPeople(this WebApplication app)
    {
        app.MapGroup("/api/v1/people/families")
            .RequireAuthorization(Permissions.PeopleRead, Permissions.PeopleManage, Permissions.AcademicRead, Permissions.EnrollmentManage)
            .AddEndpointFilter<ApiWriteFilter>()
            .MapPost("", (RegisterFamilyRequest request, ClaimsPrincipal actor, FamilyRegistrationService service, CancellationToken ct) => service.RegisterAsync(Actor(actor), request, ct));
        var read = app.MapGroup("/api/v1/people").RequireAuthorization(Permissions.PeopleRead).AddEndpointFilter<ApiWriteFilter>();
        read.MapGet("/catalog", () => Results.Ok(new { relationshipKinds = RelationshipKinds.All, faculties = Faculties.All }));
        read.MapGet("", (string? search, string? role, string? status, int? page, PeopleService service, CancellationToken ct) =>
            service.ListAsync(search, role, status, page ?? 1, ct));
        read.MapGet("/{id:guid}", (Guid id, PeopleService service, CancellationToken ct) => service.GetAsync(id, ct));

        var manage = app.MapGroup("/api/v1/people").RequireAuthorization(Permissions.PeopleManage).AddEndpointFilter<ApiWriteFilter>();
        manage.MapPost("", async (CreatePersonRequest request, ClaimsPrincipal actor, PeopleService service, CancellationToken ct) =>
            Results.Ok(await service.CreateAsync(Actor(actor), request, ct)));
        manage.MapPost("/{id:guid}", async (Guid id, UpdatePersonRequest request, ClaimsPrincipal actor, PeopleService service, CancellationToken ct) =>
        {
            await service.UpdateAsync(Actor(actor), id, request, ct);
            return Results.NoContent();
        });
        manage.MapPost("/{id:guid}/roles", async (Guid id, AddRoleRequest request, ClaimsPrincipal actor, PeopleService service, CancellationToken ct) =>
        {
            await service.AddRoleAsync(Actor(actor), id, request, ct);
            return Results.NoContent();
        });
        manage.MapPost("/{id:guid}/status", async (Guid id, PersonStatusRequest request, ClaimsPrincipal actor, PeopleService service, CancellationToken ct) =>
        {
            await service.SetStatusAsync(Actor(actor), id, request, ct);
            return Results.NoContent();
        });

        var relationships = app.MapGroup("/api/v1/relationships").RequireAuthorization(Permissions.PeopleManage)
            .AddEndpointFilter<ApiWriteFilter>();
        relationships.MapPost("", async (CreateRelationshipRequest request, ClaimsPrincipal actor, PeopleService service, CancellationToken ct) =>
            Results.Ok(new { id = await service.CreateRelationshipAsync(Actor(actor), request, ct) }));
        relationships.MapPost("/{id:guid}", async (Guid id, UpdateRelationshipRequest request, ClaimsPrincipal actor, PeopleService service, CancellationToken ct) =>
        {
            await service.UpdateRelationshipAsync(Actor(actor), id, request, ct);
            return Results.NoContent();
        });
        relationships.MapPost("/{id:guid}/end", async (Guid id, EndRelationshipRequest request, ClaimsPrincipal actor, PeopleService service, CancellationToken ct) =>
        {
            await service.EndRelationshipAsync(Actor(actor), id, request, ct);
            return Results.NoContent();
        });

        // Linking an account to a person grants that account the person's portal context: requires both permissions.
        var links = app.MapGroup("/api/v1/people").RequireAuthorization(Permissions.PeopleManage, Permissions.ManageAccounts)
            .AddEndpointFilter<ApiWriteFilter>();
        links.MapGet("/linkable-accounts", (string? search, PeopleService service, CancellationToken ct) =>
            service.LinkableAccountsAsync(search, ct));
        links.MapPost("/{id:guid}/account", async (Guid id, LinkAccountRequest request, ClaimsPrincipal actor, PeopleService service, CancellationToken ct) =>
        {
            await service.LinkAccountAsync(Actor(actor), id, request, ct);
            return Results.NoContent();
        });
        links.MapPost("/{id:guid}/account/unlink", async (Guid id, UnlinkAccountRequest request, ClaimsPrincipal actor, PeopleService service, CancellationToken ct) =>
        {
            await service.UnlinkAccountAsync(Actor(actor), id, request, ct);
            return Results.NoContent();
        });

        var me = app.MapGroup("/api/v1/me").RequireAuthorization("ActiveAccount").AddEndpointFilter<ApiWriteFilter>();
        me.MapGet("", async (ClaimsPrincipal user, PeopleService service, Academic.EnrollmentService enrollments, CancellationToken ct) =>
        {
            var profile = await service.MeAsync(Actor(user), ct);
            if (profile.Person is null) return Results.Ok(new { person = (object?)null, placement = (object?)null, students = Array.Empty<object>() });
            // Placement (year, grade, section) is shown to the student and to guardians holding the academic faculty only.
            var students = new List<object>();
            foreach (var student in profile.Students)
                students.Add(new
                {
                    student.Id, student.DisplayName, student.StudentCode, student.Relationship, student.Faculties,
                    placement = student.Faculties.Contains(Faculties.AcademicRecords) ? await enrollments.CurrentAsync(student.Id, ct) : null
                });
            var own = profile.Person.StudentCode is null ? null : await enrollments.CurrentAsync(profile.Person.Id, ct);
            return Results.Ok(new { person = profile.Person, placement = own, students });
        });
        me.MapGet("/students/{id:guid}", async (Guid id, ClaimsPrincipal user, PeopleService service, Academic.EnrollmentService enrollments, CancellationToken ct) =>
        {
            var access = await service.StudentAccessAsync(Actor(user), id, ct);
            if (access is null) return Results.NotFound();
            var profile = await service.MeAsync(Actor(user), ct);
            var student = access.IsSelf
                ? new MyStudent(id, profile.Person!.DisplayName, profile.Person.StudentCode!, "Self", [])
                : profile.Students.Single(x => x.Id == id);
            var placement = access.Has(Faculties.AcademicRecords) ? await enrollments.CurrentAsync(id, ct) : null;
            return Results.Ok(new { student, access.IsSelf, access.Faculties, placement });
        });
    }

    internal static Guid Actor(ClaimsPrincipal actor) => Guid.Parse(actor.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
