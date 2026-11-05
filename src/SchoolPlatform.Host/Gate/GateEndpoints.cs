using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.Documents;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Host.Gate;

public static class GateEndpoints
{
    public static void MapGate(this WebApplication app)
    {
        // Gate kiosk (signed-in account). The service re-checks attendance.gate in SQL inside each write.
        var kiosk = app.MapGroup("/api/v1/gate").RequireAuthorization(Permissions.AttendanceGate).AddEndpointFilter<ApiWriteFilter>();
        kiosk.MapPost("/scan", (ScanRequest request, ClaimsPrincipal user, GateService service, CancellationToken ct) =>
            service.ScanAsync(new(Actor(user), null, GateSource.Scanner), request, ct));
        kiosk.MapPost("/manual", (ManualMarkRequest request, ClaimsPrincipal user, GateService service, CancellationToken ct) =>
            service.ManualAsync(Actor(user), request, ct));
        kiosk.MapGet("/search", (string? q, GateService service, CancellationToken ct) => service.SearchAsync(q, ct));
        kiosk.MapGet("/recent", (GateService service, CancellationToken ct) => service.RecentAsync(20, ct));

        var reports = app.MapGroup("/api/v1/gate").RequireAuthorization(Permissions.AttendanceRead).AddEndpointFilter<ApiWriteFilter>();
        reports.MapGet("/daily", (DateOnly? date, Guid? gradeId, Guid? sectionId, GateService service, CourseServiceClock clock, CancellationToken ct) =>
            service.DailyAsync(date ?? clock.Today(), gradeId, sectionId, ct));
        reports.MapGet("/students/{id:guid}/events", (Guid id, DateOnly? from, DateOnly? to, GateService service, CourseServiceClock clock,
            CancellationToken ct) => service.StudentEventsAsync(id, from ?? clock.Today().AddDays(-30), to ?? clock.Today(), ct));

        var manage = app.MapGroup("/api/v1/gate").RequireAuthorization(Permissions.AttendanceManage).AddEndpointFilter<ApiWriteFilter>();
        manage.MapPost("/events/{id:guid}/void", async (Guid id, VoidMarkRequest request, ClaimsPrincipal user, GateService service, CancellationToken ct) =>
        {
            await service.VoidAsync(Actor(user), id, request, ct);
            return Results.NoContent();
        });
        manage.MapGet("/devices", (GateDeviceService service, CancellationToken ct) => service.ListAsync(ct));
        manage.MapPost("/devices", (DeviceRequest request, ClaimsPrincipal user, HttpContext context, GateDeviceService service,
            IOptions<GateOptions> options, CancellationToken ct) =>
            service.RegisterAsync(Actor(user), request,
                options.Value.PublicUrl.Length > 0 ? options.Value.PublicUrl.TrimEnd('/') : $"{context.Request.Scheme}://{context.Request.Host}", ct));
        manage.MapPost("/devices/{id:guid}/revoke", async (Guid id, VoidMarkRequest request, ClaimsPrincipal user, GateDeviceService service,
            CancellationToken ct) =>
        {
            await service.RevokeAsync(Actor(user), id, request, ct);
            return Results.NoContent();
        });

        // Scanner devices (bearer token, no cookies): only these two endpoints accept the device scheme.
        var device = app.MapGroup("/api/v1/gate/device").RequireAuthorization("GateDevice").AddEndpointFilter<ApiWriteFilter>()
            .RequireRateLimiting("gate-device");
        device.MapGet("/whoami", (ClaimsPrincipal user, IOptions<SchoolPlatform.Host.Branding.InstitutionOptions> school) =>
            Results.Ok(new { device = user.Identity!.Name, school = school.Value.DisplayName }));
        device.MapPost("/scan", (ScanRequest request, ClaimsPrincipal user, GateService service, CancellationToken ct) =>
            service.ScanAsync(new(null, Guid.Parse(user.FindFirstValue(GateDeviceAuthenticationHandler.DeviceClaim)!), GateSource.Mobile), request, ct));

        // Family/student: own marks with the AcademicRecords faculty; 404 otherwise.
        var scoped = app.MapGroup("/api/v1").RequireAuthorization("ActiveAccount").AddEndpointFilter<ApiWriteFilter>();
        scoped.MapGet("/me/students/{id:guid}/gate", async (Guid id, ClaimsPrincipal user, GateService service, PeopleService people, CancellationToken ct) =>
            await service.MyDaysAsync(people, Actor(user), id, ct) is { } days ? Results.Ok(days) : Results.NotFound());
        // Photo: gate operators and people readers, or the student/guardian with academic access.
        scoped.MapGet("/people/{id:guid}/photo", async (Guid id, ClaimsPrincipal user, SchoolDbContext db, PeopleService people, CardService cards,
            CancellationToken ct) =>
        {
            var actor = Actor(user);
            var staff = await db.Permissions.AnyAsync(x => x.UserId == actor &&
                (x.Permission == Permissions.AttendanceGate || x.Permission == Permissions.PeopleRead), ct);
            if (!staff && !(await people.StudentAccessAsync(actor, id, ct) is { } access && access.Has(Faculties.AcademicRecords)))
                return Results.NotFound();
            return await cards.PhotoAsync(id, ct) is { } photo ? Results.File(photo.Path, photo.ContentType) : Results.NotFound();
        });

        // ID cards: issuing/printing by people.manage; the current token is visible to people.read.
        app.MapGroup("/api/v1").RequireAuthorization(Permissions.PeopleRead)
            .MapGet("/people/{id:guid}/photo-status", async (Guid id, CardService cards, CancellationToken ct) =>
                Results.Ok(new { hasPhoto = await cards.PhotoAsync(id, ct) is not null }));
        app.MapGroup("/api/v1").RequireAuthorization(Permissions.PeopleRead).AddEndpointFilter<ApiWriteFilter>()
            .MapGet("/students/{id:guid}/card", async (Guid id, CardService cards, CancellationToken ct) =>
                Results.Ok(new { card = await cards.CurrentAsync(id, ct) }));
        var issuing = app.MapGroup("/api/v1").RequireAuthorization(Permissions.PeopleManage).AddEndpointFilter<ApiWriteFilter>();
        issuing.MapPost("/students/{id:guid}/card/reissue", (Guid id, ReissueRequest request, ClaimsPrincipal user, CardService cards, CancellationToken ct) =>
            cards.ReissueAsync(Actor(user), id, request, ct));
        issuing.MapGet("/students/{id:guid}/card.pdf", async (Guid id, string? layout, ClaimsPrincipal user, CardService cards, PdfDocuments pdf,
            CancellationToken ct) =>
        {
            var data = await cards.PrintAsync(Actor(user), [id], null, ct);
            return DocumentEndpoints.Pdf(pdf.Cards(data, Layout(layout)), $"carnet-{data[0].StudentCode}.pdf");
        });
        issuing.MapGet("/years/{yearId:guid}/cards.pdf", async (Guid yearId, Guid gradeId, Guid? sectionId, string? layout, ClaimsPrincipal user,
            CardService cards, PdfDocuments pdf, CancellationToken ct) =>
        {
            var (ids, asOf) = await cards.BatchAsync(new(yearId, gradeId, sectionId), ct);
            if (ids.Length == 0) return Results.Problem(statusCode: 404, title: "No hay estudiantes activos en ese grado o sección.");
            return DocumentEndpoints.Pdf(pdf.Cards(await cards.PrintAsync(Actor(user), ids, asOf, ct), Layout(layout)), "carnets.pdf");
        });
        issuing.MapPost("/people/{id:guid}/photo", async (Guid id, IFormFile file, ClaimsPrincipal user, CardService cards, CancellationToken ct) =>
        {
            await cards.UploadPhotoAsync(Actor(user), id, file, ct);
            return Results.NoContent();
        }).WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(3 * 1024 * 1024));
        issuing.MapPost("/people/{id:guid}/photo/remove", async (Guid id, ClaimsPrincipal user, CardService cards, CancellationToken ct) =>
        {
            await cards.RemovePhotoAsync(Actor(user), id, ct);
            return Results.NoContent();
        });
    }

    private static string Layout(string? layout) => layout == "card" ? "card" : "sheet";

    private static Guid Actor(ClaimsPrincipal user) => PeopleEndpoints.Actor(user);
}

/// <summary>Today's date in the institution time zone, for endpoint defaults.</summary>
public sealed class CourseServiceClock(SchoolPlatform.Host.Academic.CourseService courses, TimeProvider time)
{
    public DateOnly Today() => courses.LocalDate(time.GetUtcNow());
}
