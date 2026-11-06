using System.Net;
using System.Security.Claims;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.IdentityAccess;

namespace SchoolPlatform.Host.LegacyImport;

public static class MigrationAssistantEndpoints
{
    public static void MapMigrationAssistant(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("MigrationAssistant:Enabled")) return;
        var group = app.MapGroup("/api/v1/migration-assistant")
            .RequireAuthorization("ActiveAccount", Permissions.PeopleManage, Permissions.EnrollmentManage, Permissions.ManageAccounts)
            .AddEndpointFilter<LocalAssistantFilter>().AddEndpointFilter<ApiWriteFilter>();
        static Guid Actor(ClaimsPrincipal actor) => Guid.Parse(actor.FindFirstValue(ClaimTypes.NameIdentifier)!);
        group.MapGet("/catalog", (ClaimsPrincipal actor, MigrationAssistantService service, CancellationToken ct) => service.CatalogAsync(Actor(actor), ct));
        group.MapPost("/prepare", (ClaimsPrincipal actor, MigrationAssistantService service, CancellationToken ct) => service.PrepareAsync(Actor(actor), ct));
        group.MapPost("/source", (AssistantInspectRequest request, ClaimsPrincipal actor, MigrationAssistantService service, CancellationToken ct) => service.InspectAsync(Actor(actor), request.Connection, ct));
        group.MapPost("/read", (AssistantReadRequest request, ClaimsPrincipal actor, MigrationAssistantService service, CancellationToken ct) => service.ReadAsync(Actor(actor), request, ct));
        group.MapGet("/recent", (ClaimsPrincipal actor, MigrationAssistantService service, CancellationToken ct) => service.RecentAsync(Actor(actor), ct));
        group.MapPost("/draft", (AssistantPlanRequest request, ClaimsPrincipal actor, MigrationAssistantService service, CancellationToken ct) => service.SaveDraftAsync(Actor(actor), request.Review, ct));
        group.MapPost("/year", async (YearRequest request, ClaimsPrincipal actor, MigrationAssistantService service, CancellationToken ct) =>
            Results.Ok(new { id = await service.CreateYearAsync(Actor(actor), request, ct) })).RequireAuthorization(Permissions.AcademicManage);
        group.MapPost("/grades/{run:guid}", async (Guid run, ClaimsPrincipal actor, MigrationAssistantService service, CancellationToken ct) =>
        { await service.CreateGradesAsync(Actor(actor), run, ct); return Results.NoContent(); }).RequireAuthorization(Permissions.AcademicManage);
        group.MapPost("/plan", (AssistantPlanRequest request, ClaimsPrincipal actor, MigrationAssistantService service, CancellationToken ct) => service.PlanAsync(Actor(actor), request.Review, ct));
        group.MapPost("/apply", (AssistantApplyRequest request, ClaimsPrincipal actor, MigrationAssistantService service, CancellationToken ct) => service.ApplyAsync(Actor(actor), request, ct));
        group.MapPost("/reconcile/{run:guid}", (Guid run, ClaimsPrincipal actor, MigrationAssistantService service, CancellationToken ct) => service.ReconcileAsync(Actor(actor), run, ct));
    }
}

public sealed class LocalAssistantFilter(IWebHostEnvironment environment) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var remote = context.HttpContext.Connection.RemoteIpAddress;
        if (remote is null ? !environment.IsDevelopment() : !IPAddress.IsLoopback(remote)) return Results.NotFound();
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        try { return await next(context); }
        catch (RosterImportException exception)
        {
            var message = exception.Code switch
            {
                "grade-mapping-required" => "Elige el grado de destino de los estudiantes incluidos.",
                "names-review" => "Completa nombres y apellidos de las personas señaladas.",
                "birth-review" => "Corrige las fechas de nacimiento inválidas o déjalas vacías.",
                "code-collision" or "duplicate-student-code" => "Hay códigos de estudiante repetidos. Revisa los señalados; no se fusionan personas automáticamente.",
                "possible-duplicate-review" => "Hay posibles personas duplicadas. Revisa los nombres coincidentes y confirma solo si son personas distintas.",
                "section-full" => "Una sección no tiene cupo para todo el grupo. Cambia la sección o déjala sin asignar.",
                "plan-changed" => "La revisión o el destino cambiaron. Pulsa Comprobar nuevamente antes de importar.",
                "alias-already-imported" or "legacy-v1-already-imported" => "Ya hay estudiantes de este origen importados. No se crearon duplicados; revisa el resultado anterior.",
                "review-required" => "Confirma el padrón de estudiantes y encargados antes de comprobarlo.",
                _ => "Revisa nombres, códigos, familias y ciclo. Hay datos que necesitan corrección antes de importar."
            };
            return Results.Problem(statusCode: 409, title: message, extensions: new Dictionary<string, object?> { ["code"] = exception.Code });
        }
        catch (OperationException) { throw; }
        catch (Microsoft.AspNetCore.Antiforgery.AntiforgeryValidationException) { throw; }
        catch (Exception)
        {
            // Never serialize a PHP/PDO/SQL/file exception that might contain a secret or a person's values.
            return Results.Problem(statusCode: 400, title: "No se pudo completar este paso. Reintenta o corrige la conexión; no se aplicó una carga parcial.");
        }
    }
}
