using Microsoft.AspNetCore.Diagnostics;

namespace SchoolPlatform.Host.Common;

/// <summary>
/// Request binding failures (malformed JSON, empty dates, wrong types) are client errors: answer 400 with a stable code
/// instead of the generic 500 produced by the exception handler. Details are not echoed to the client.
/// </summary>
public sealed class BadRequestExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499; // Client closed request; nothing is written.
            return true;
        }
        if (exception is not BadHttpRequestException bad) return false;
        context.Response.StatusCode = bad.StatusCode is >= 400 and < 500 ? bad.StatusCode : StatusCodes.Status400BadRequest;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails =
            {
                Status = context.Response.StatusCode,
                Title = "Revisa el formulario: hay campos vacíos o con un formato no válido.",
                Extensions = { ["code"] = "invalid-request" }
            }
        });
    }
}
