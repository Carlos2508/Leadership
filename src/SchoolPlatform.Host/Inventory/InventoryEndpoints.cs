using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.People;

namespace SchoolPlatform.Host.Inventory;

public static class InventoryEndpoints
{
    public static void MapInventory(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/inventory").RequireAuthorization(Permissions.InventoryRead).AddEndpointFilter<ApiWriteFilter>();
        group.MapGet("/summary", (ClaimsPrincipal user, InventoryService service, CancellationToken ct) => service.SummaryAsync(PeopleEndpoints.Actor(user), ct));
        group.MapGet("/products", (string? search, string? status, bool? low, int? page, ClaimsPrincipal user, InventoryService service, CancellationToken ct) =>
            service.ProductsAsync(PeopleEndpoints.Actor(user), search, status, low ?? false, page ?? 1, ct));
        group.MapGet("/products/{id:guid}", (Guid id, ClaimsPrincipal user, InventoryService service, CancellationToken ct) => service.ProductAsync(PeopleEndpoints.Actor(user), id, ct));
        group.MapGet("/products/{id:guid}/movements", (Guid id, int? page, ClaimsPrincipal user, InventoryService service, CancellationToken ct) => service.MovementsAsync(PeopleEndpoints.Actor(user), id, page ?? 1, ct));
        group.MapGet("/export", async (string? search, string? status, bool? low, ClaimsPrincipal user, InventoryService service, CancellationToken ct) =>
            Results.File(await service.ExportAsync(PeopleEndpoints.Actor(user), search, status, low ?? false, ct), "text/csv; charset=utf-8", "inventario-bodega.csv"));
        group.MapPost("/products", async (CreateInventoryProduct request, ClaimsPrincipal user, IAuthorizationService authorization, InventoryService service, CancellationToken ct) =>
        {
            if (request.InitialQuantity > 0 && !(await authorization.AuthorizeAsync(user, null, Permissions.InventoryMove)).Succeeded) return Results.Forbid();
            return Results.Ok(await service.CreateAsync(PeopleEndpoints.Actor(user), request, ct));
        })
            .RequireAuthorization(Permissions.InventoryManage);
        group.MapPost("/products/{id:guid}", (Guid id, UpdateInventoryProduct request, ClaimsPrincipal user, InventoryService service, CancellationToken ct) => service.UpdateAsync(PeopleEndpoints.Actor(user), id, request, ct))
            .RequireAuthorization(Permissions.InventoryManage);
        group.MapPost("/products/{id:guid}/movements", (Guid id, MoveInventoryStock request, ClaimsPrincipal user, InventoryService service, CancellationToken ct) =>
            request.Kind is "In" or "Out" ? service.MoveAsync(PeopleEndpoints.Actor(user), id, request, ct) : throw OperationException.Invalid("Este comando requiere una entrada o salida."))
            .RequireAuthorization(Permissions.InventoryMove);
        group.MapPost("/products/{id:guid}/counts", (Guid id, MoveInventoryStock request, ClaimsPrincipal user, InventoryService service, CancellationToken ct) =>
            request.Kind == "Count" ? service.MoveAsync(PeopleEndpoints.Actor(user), id, request, ct) : throw OperationException.Invalid("Este comando requiere un conteo físico."))
            .RequireAuthorization(Permissions.InventoryManage);
    }
}
