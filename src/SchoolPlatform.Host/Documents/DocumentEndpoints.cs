using System.Security.Claims;
using System.Text;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.Finance;
using SchoolPlatform.Host.Grades;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.People;

namespace SchoolPlatform.Host.Documents;

/// <summary>
/// Printable PDFs. Each endpoint applies the same authorization as the screen that shows the data; responses are
/// never cached (/api is no-store) and open inline in the browser.
/// </summary>
public static class DocumentEndpoints
{
    public static void MapDocuments(this WebApplication app)
    {
        app.MapGroup("/api/v1/finance").RequireAuthorization(Permissions.FinanceRead).AddEndpointFilter<ApiWriteFilter>()
            .MapGet("/payments/{id:guid}/receipt.pdf", async (Guid id, PaymentService payments, PdfDocuments pdf, CancellationToken ct) =>
                await ReceiptPdf(id, payments, pdf, true, ct));

        var scoped = app.MapGroup("/api/v1").RequireAuthorization("ActiveAccount").AddEndpointFilter<ApiWriteFilter>();
        scoped.MapGet("/me/students/{studentId:guid}/receipts/{paymentId:guid}/receipt.pdf", async (Guid studentId, Guid paymentId,
            ClaimsPrincipal user, PeopleService people, PaymentService payments, PdfDocuments pdf, CancellationToken ct) =>
            await people.StudentAccessAsync(Actor(user), studentId, ct) is { IsSelf: false } access && access.Faculties.Contains(Faculties.Finance) &&
            await payments.PaymentTouchesStudentAsync(paymentId, studentId, ct)
                ? await ReceiptPdf(paymentId, payments, pdf, false, ct) : Results.NotFound());
        scoped.MapGet("/me/students/{studentId:guid}/grades.pdf", async (Guid studentId, ClaimsPrincipal user, GradeReportService reports,
            PdfDocuments pdf, CancellationToken ct) =>
            await reports.FamilyReportAsync(Actor(user), studentId, ct) is { } report
                ? Pdf(pdf.GradeReports([report]), $"notas-{report.StudentCode}.pdf") : Results.NotFound());
        scoped.MapGet("/courses/{courseId:guid}/grades.pdf", async (Guid courseId, Guid? periodId, ClaimsPrincipal user,
            GradeReportService reports, PdfDocuments pdf, CancellationToken ct) =>
            await reports.CourseSheetAsync(Actor(user), courseId, periodId, ct) is { } sheet
                ? Pdf(pdf.CourseSheet(sheet), "libreta.pdf") : Results.NotFound());

        app.MapGroup("/api/v1").RequireAuthorization(Permissions.AcademicRead).AddEndpointFilter<ApiWriteFilter>()
            .MapGet("/years/{yearId:guid}/periods/{periodId:guid}/report-cards.pdf", async (Guid yearId, Guid periodId, Guid gradeId,
                Guid? sectionId, GradeReportService reports, PdfDocuments pdf, CancellationToken ct) =>
            {
                var all = await reports.PeriodReportsAsync(yearId, periodId, gradeId, sectionId, ct);
                return all.Length == 0
                    ? Results.Problem(statusCode: 404, title: "No hay estudiantes en ese grado o sección durante el período.")
                    : Pdf(pdf.GradeReports(all), "informes.pdf");
            });
    }

    private static async Task<IResult> ReceiptPdf(Guid paymentId, PaymentService payments, PdfDocuments pdf, bool staff, CancellationToken ct)
    {
        var view = await payments.ReceiptAsync(paymentId, ct);
        var logo = view.Receipt.LogoSha256.Length > 0 ? await payments.AssetAsync(view.Receipt.LogoSha256, ct) : null;
        return Pdf(pdf.Receipt(view, logo?.Content, logo?.ContentType, staff), $"recibo-{view.Receipt.Number}.pdf");
    }

    /// <summary>Inline PDF with an ASCII file name (the browser offers it when saving).</summary>
    public static IResult Pdf(byte[] content, string name) => new InlinePdf(content, name);

    private sealed class InlinePdf(byte[] content, string name) : IResult
    {
        public async Task ExecuteAsync(HttpContext context)
        {
            var safe = new StringBuilder();
            foreach (var c in name) safe.Append(char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_' ? c : '-');
            context.Response.ContentType = "application/pdf";
            context.Response.Headers.ContentDisposition = $"inline; filename=\"{safe}\"";
            context.Response.ContentLength = content.Length;
            await context.Response.Body.WriteAsync(content, context.RequestAborted);
        }
    }

    private static Guid Actor(ClaimsPrincipal user) => PeopleEndpoints.Actor(user);
}
