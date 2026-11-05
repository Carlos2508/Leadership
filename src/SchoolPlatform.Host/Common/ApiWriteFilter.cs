using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;

namespace SchoolPlatform.Host.Common;

/// <summary>Validates antiforgery on writes and maps expected business failures to problem responses.</summary>
public sealed class ApiWriteFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            // Scanner devices authenticate with a bearer token only (no cookies), so cross-site request forgery does not apply.
            if (!HttpMethods.IsGet(context.HttpContext.Request.Method) &&
                context.HttpContext.User.Identity?.AuthenticationType != SchoolPlatform.Host.Gate.GateDeviceAuthenticationHandler.SchemeName)
                await antiforgery.ValidateRequestAsync(context.HttpContext);
            return await next(context);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.Problem(statusCode: 400, title: "El formulario venció. Recarga la página e intenta nuevamente.",
                extensions: new Dictionary<string, object?> { ["code"] = "invalid-csrf" });
        }
        catch (OperationException exception)
        {
            return Results.Problem(statusCode: exception.Status, title: exception.Message,
                extensions: new Dictionary<string, object?> { ["code"] = exception.Code });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Problem(statusCode: 409, title: "Los datos cambiaron. Recarga la página antes de continuar.",
                extensions: new Dictionary<string, object?> { ["code"] = "resource-changed" });
        }
        catch (DbUpdateException exception) when (exception.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        {
            return Results.Problem(statusCode: 409, title: "Ya existe un registro con esos datos. Revisa la información e intenta nuevamente.",
                extensions: new Dictionary<string, object?> { ["code"] = "duplicate" });
        }
        catch (Microsoft.Data.SqlClient.SqlException exception) when (exception.Number is 51000 or 1205)
        {
            return Results.Problem(statusCode: 409, title: "Otra operación está en curso. Espera unos segundos e intenta nuevamente.");
        }
    }
}
