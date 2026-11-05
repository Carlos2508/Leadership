using System.Security.Claims;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.People;

namespace SchoolPlatform.Host.Finance;

public static class FinanceEndpoints
{
    public static void MapFinance(this WebApplication app)
    {
        var read = app.MapGroup("/api/v1/finance").RequireAuthorization(Permissions.FinanceRead).AddEndpointFilter<ApiWriteFilter>();
        read.MapGet("/years/{yearId:guid}/setup", (Guid yearId, FinanceSetupService service, CancellationToken ct) => service.SetupAsync(yearId, ct));
        read.MapGet("/concepts", (FinanceSetupService service, CancellationToken ct) => service.ConceptsAsync(ct));
        read.MapGet("/students/{id:guid}/statement", (Guid id, Guid? yearId, ChargeService service, CancellationToken ct) =>
            service.StatementAsync(id, yearId, ct));
        read.MapGet("/payments", (DateOnly? from, DateOnly? to, PaymentService service, CancellationToken ct) => service.RecentAsync(from, to, ct));
        read.MapGet("/payments/{id:guid}", (Guid id, PaymentService service, CancellationToken ct) => service.ReceiptAsync(id, ct));
        read.MapGet("/assets/{sha}", async (string sha, PaymentService service, CancellationToken ct) =>
            await service.AssetAsync(sha, ct) is { } asset ? Results.File(asset.Content, asset.ContentType) : Results.NotFound());
        read.MapGet("/cutoffs/{id:guid}/report", (Guid id, Guid? gradeId, string? status, ClearanceService service, CancellationToken ct) =>
            service.ReportAsync(id, gradeId, status, ct));
        read.MapGet("/cutoffs/{id:guid}/students/{studentId:guid}", (Guid id, Guid studentId, ClearanceService service, CancellationToken ct) =>
            service.CheckAsync(studentId, id, ct));

        var charges = app.MapGroup("/api/v1/finance").RequireAuthorization(Permissions.FinanceCharges).AddEndpointFilter<ApiWriteFilter>();
        charges.MapPost("/concepts", async (ConceptRequest request, ClaimsPrincipal user, FinanceSetupService service, CancellationToken ct) =>
            Results.Ok(new { id = await service.SaveConceptAsync(Actor(user), null, request, ct) }));
        charges.MapPost("/concepts/{id:guid}", async (Guid id, ConceptRequest request, ClaimsPrincipal user, FinanceSetupService service, CancellationToken ct) =>
        {
            await service.SaveConceptAsync(Actor(user), id, request, ct);
            return Results.NoContent();
        });
        charges.MapPost("/years/{yearId:guid}/periods", async (Guid yearId, BillingPeriodRequest request, ClaimsPrincipal user, FinanceSetupService service, CancellationToken ct) =>
            Results.Ok(new { id = await service.AddPeriodAsync(Actor(user), yearId, request, ct) }));
        charges.MapPost("/periods/{id:guid}", async (Guid id, BillingPeriodRequest request, ClaimsPrincipal user, FinanceSetupService service, CancellationToken ct) =>
        {
            await service.UpdatePeriodAsync(Actor(user), id, request, ct);
            return Results.NoContent();
        });
        charges.MapPost("/years/{yearId:guid}/cutoffs", async (Guid yearId, CutoffRequest request, ClaimsPrincipal user, FinanceSetupService service, CancellationToken ct) =>
            Results.Ok(new { id = await service.AddCutoffAsync(Actor(user), yearId, request, ct) }));
        charges.MapPost("/charges/preview", (GenerationRequest request, ClaimsPrincipal user, ChargeService service, CancellationToken ct) =>
            service.PreviewAsync(Actor(user), request, ct));
        charges.MapPost("/charges/generate", (GenerationRequest request, ClaimsPrincipal user, ChargeService service, CancellationToken ct) =>
            service.GenerateAsync(Actor(user), request, ct));
        charges.MapPost("/charges", async (SingleChargeRequest request, ClaimsPrincipal user, ChargeService service, CancellationToken ct) =>
            Results.Ok(new { id = await service.CreateSingleAsync(Actor(user), request, ct) }));
        charges.MapPost("/charges/{id:guid}/adjustments", async (Guid id, AdjustmentRequest request, ClaimsPrincipal user, ChargeService service, CancellationToken ct) =>
        {
            await service.AdjustAsync(Actor(user), id, request, ct);
            return Results.NoContent();
        });
        charges.MapPost("/charges/{id:guid}/void", async (Guid id, VoidRequest request, ClaimsPrincipal user, ChargeService service, CancellationToken ct) =>
        {
            await service.VoidAsync(Actor(user), id, request, ct);
            return Results.NoContent();
        });

        app.MapGroup("/api/v1/finance").RequireAuthorization(Permissions.FinanceCollect).AddEndpointFilter<ApiWriteFilter>()
            .MapPost("/payments", (PaymentRequest request, ClaimsPrincipal user, PaymentService service, CancellationToken ct) =>
                service.RecordAsync(Actor(user), request, ct));
        app.MapGroup("/api/v1/finance").RequireAuthorization(Permissions.FinanceReverse).AddEndpointFilter<ApiWriteFilter>()
            .MapPost("/payments/{id:guid}/reverse", async (Guid id, ReverseRequest request, ClaimsPrincipal user, PaymentService service, CancellationToken ct) =>
            {
                await service.ReverseAsync(Actor(user), id, request, ct);
                return Results.NoContent();
            });

        // Self-service: a guardian with the Finance faculty sees the statement and receipts of that student.
        // The student's own access to financial detail is a pending institutional policy, so it is not granted here.
        var me = app.MapGroup("/api/v1/me/students/{studentId:guid}").RequireAuthorization("ActiveAccount").AddEndpointFilter<ApiWriteFilter>();
        me.MapGet("/statement", async (Guid studentId, ClaimsPrincipal user, PeopleService people, ChargeService service, CancellationToken ct) =>
            await FinanceAccess(people, user, studentId, ct) ? Results.Ok(await service.StatementAsync(studentId, null, ct)) : Results.NotFound());
        me.MapGet("/receipts/{paymentId:guid}", async (Guid studentId, Guid paymentId, ClaimsPrincipal user, PeopleService people,
            PaymentService service, CancellationToken ct) =>
            await FinanceAccess(people, user, studentId, ct) && await service.PaymentTouchesStudentAsync(paymentId, studentId, ct)
                ? Results.Ok(await service.ReceiptAsync(paymentId, ct)) : Results.NotFound());
        me.MapGet("/receipt-assets/{sha}", async (Guid studentId, string sha, ClaimsPrincipal user, PeopleService people,
            PaymentService service, CancellationToken ct) =>
            await FinanceAccess(people, user, studentId, ct) && await service.AssetAsync(sha, ct) is { } asset
                ? Results.File(asset.Content, asset.ContentType) : Results.NotFound());
    }

    private static async Task<bool> FinanceAccess(PeopleService people, ClaimsPrincipal user, Guid studentId, CancellationToken ct) =>
        await people.StudentAccessAsync(Actor(user), studentId, ct) is { IsSelf: false } access && access.Faculties.Contains(Faculties.Finance);

    private static Guid Actor(ClaimsPrincipal user) => PeopleEndpoints.Actor(user);
}
