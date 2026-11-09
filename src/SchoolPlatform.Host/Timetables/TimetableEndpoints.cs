using System.Security.Claims;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.Documents;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.People;

namespace SchoolPlatform.Host.Timetables;
public sealed record TimetableRoomRequest(string Name);
public sealed record TimetableTemplateRequest(string Name, TimetableStructure Structure);
public static class TimetableEndpoints
{
    public static void MapTimetables(this WebApplication app)
    {
        var read = app.MapGroup("/api/v1/years/{year:guid}/timetables").RequireAuthorization(Permissions.AcademicRead).AddEndpointFilter<ApiWriteFilter>();
        read.MapGet("", (Guid year, TimetableService s, CancellationToken ct) => s.ListAsync(year, ct));
        read.MapGet("/catalog", (Guid year, TimetableService s, CancellationToken ct) => s.CatalogAsync(year, ct));
        read.MapGet("/revisions/{id:guid}", (Guid year, Guid id, TimetableService s, CancellationToken ct) => s.GetAsync(year, id, ct));
        read.MapGet("/published", (Guid year, DateOnly on, Guid? gradeId, Guid? sectionId, Guid? staffId, TimetableService s, CancellationToken ct) => s.ReadAsync(year, on, gradeId, sectionId, staffId, ct));
        read.MapGet("/published.pdf", async (Guid year, DateOnly on, Guid? gradeId, Guid? sectionId, Guid? staffId, string? paper, TimetableService s, PdfDocuments pdf, CancellationToken ct) =>
            DocumentEndpoints.Pdf(pdf.Timetables([await s.ReadAsync(year, on, gradeId, sectionId, staffId, ct)], staffId == null && sectionId == null, paper), "horarios.pdf"));
        var write = app.MapGroup("/api/v1/years/{year:guid}/timetables").RequireAuthorization(Permissions.AcademicManage).AddEndpointFilter<ApiWriteFilter>();
        write.MapPost("", (Guid year, SaveTimetable request, ClaimsPrincipal actor, TimetableService s, CancellationToken ct) => s.SaveAsync(PeopleEndpoints.Actor(actor), year, request, ct));
        write.MapPost("/revisions/{id:guid}/publish", (Guid year, Guid id, PublishTimetable request, ClaimsPrincipal actor, TimetableService s, CancellationToken ct) => s.PublishAsync(PeopleEndpoints.Actor(actor), year, id, request, ct));
        write.MapPost("/rooms", async (TimetableRoomRequest request, ClaimsPrincipal actor, TimetableService s, CancellationToken ct) => Results.Ok(new { id = await s.RoomAsync(PeopleEndpoints.Actor(actor), request.Name, ct) }));
        write.MapPost("/templates", async (TimetableTemplateRequest request, ClaimsPrincipal actor, TimetableService s, CancellationToken ct) => Results.Ok(new { id = await s.TemplateAsync(PeopleEndpoints.Actor(actor), request.Name, request.Structure, ct) }));
        var self = app.MapGroup("/api/v1/me/timetables").RequireAuthorization("ActiveAccount").AddEndpointFilter<ApiWriteFilter>();
        self.MapGet("/years", (ClaimsPrincipal actor, TimetableService s, CancellationToken ct) => s.SelfYearsAsync(PeopleEndpoints.Actor(actor), ct));
        self.MapGet("/teacher", (Guid yearId, DateOnly on, ClaimsPrincipal actor, TimetableService s, CancellationToken ct) => s.TeacherAsync(PeopleEndpoints.Actor(actor), yearId, on, ct));
        self.MapGet("/students/{id:guid}", (Guid id, Guid yearId, DateOnly on, ClaimsPrincipal actor, TimetableService s, CancellationToken ct) => s.StudentAsync(PeopleEndpoints.Actor(actor), id, yearId, on, ct));
        self.MapGet("/teacher.pdf", async (Guid yearId, DateOnly on, string? paper, ClaimsPrincipal actor, TimetableService s, PdfDocuments pdf, CancellationToken ct) =>
            DocumentEndpoints.Pdf(pdf.Timetables([await s.TeacherAsync(PeopleEndpoints.Actor(actor), yearId, on, ct)], false, paper), "mi-horario.pdf"));
        self.MapGet("/students/{id:guid}.pdf", async (Guid id, Guid yearId, DateOnly on, string? paper, ClaimsPrincipal actor, TimetableService s, PdfDocuments pdf, CancellationToken ct) =>
            DocumentEndpoints.Pdf(pdf.Timetables([await s.StudentAsync(PeopleEndpoints.Actor(actor), id, yearId, on, ct)], false, paper), "horario-estudiante.pdf"));
    }
}
