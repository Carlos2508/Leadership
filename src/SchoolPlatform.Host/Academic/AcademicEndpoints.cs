using System.Security.Claims;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.People;

namespace SchoolPlatform.Host.Academic;

public static class AcademicEndpoints
{
    public static void MapAcademic(this WebApplication app)
    {
        var read = app.MapGroup("/api/v1/academic").RequireAuthorization(Permissions.AcademicRead).AddEndpointFilter<ApiWriteFilter>();
        read.MapGet("/years", (AcademicService service, CancellationToken ct) => service.YearsAsync(ct));
        read.MapGet("/years/{id:guid}", (Guid id, AcademicService service, CancellationToken ct) => service.YearAsync(id, ct));
        read.MapGet("/grades", (AcademicService service, CancellationToken ct) => service.GradesAsync(ct));

        var manage = app.MapGroup("/api/v1/academic").RequireAuthorization(Permissions.AcademicManage).AddEndpointFilter<ApiWriteFilter>();
        manage.MapPost("/years", async (YearRequest request, ClaimsPrincipal user, AcademicService service, CancellationToken ct) =>
            Results.Ok(new { id = await service.CreateYearAsync(Actor(user), request, ct) }));
        manage.MapPost("/years/{id:guid}", async (Guid id, YearRequest request, ClaimsPrincipal user, AcademicService service, CancellationToken ct) =>
        {
            await service.UpdateYearAsync(Actor(user), id, request, ct);
            return Results.NoContent();
        });
        manage.MapPost("/years/{id:guid}/status", async (Guid id, YearStatusRequest request, ClaimsPrincipal user, AcademicService service, CancellationToken ct) =>
        {
            await service.SetYearStatusAsync(Actor(user), id, request, ct);
            return Results.NoContent();
        });
        manage.MapPost("/years/{id:guid}/default", async (Guid id, VersionRequest request, ClaimsPrincipal user, AcademicService service, CancellationToken ct) =>
        {
            await service.SetDefaultYearAsync(Actor(user), id, request, ct);
            return Results.NoContent();
        });
        manage.MapPost("/years/{id:guid}/periods", async (Guid id, PeriodRequest request, ClaimsPrincipal user, AcademicService service, CancellationToken ct) =>
            Results.Ok(new { id = await service.AddPeriodAsync(Actor(user), id, request, ct) }));
        manage.MapPost("/years/{id:guid}/periods/generate", async (Guid id, GeneratePeriodsRequest request, ClaimsPrincipal user, AcademicService service, CancellationToken ct) =>
            Results.Ok(new { created = await service.GeneratePeriodsAsync(Actor(user), id, request, ct) }));
        manage.MapPost("/years/{id:guid}/sections/copy", async (Guid id, CopyFromYearRequest request, ClaimsPrincipal user, AcademicService service, CancellationToken ct) =>
            Results.Ok(new { created = await service.CopySectionsAsync(Actor(user), id, request, ct) }));
        manage.MapPost("/years/{id:guid}/periods/remove-last", async (Guid id, VersionRequest request, ClaimsPrincipal user, AcademicService service, CancellationToken ct) =>
        {
            await service.RemoveLastPeriodAsync(Actor(user), id, request, ct);
            return Results.NoContent();
        });
        manage.MapPost("/periods/{id:guid}", async (Guid id, PeriodRequest request, ClaimsPrincipal user, AcademicService service, CancellationToken ct) =>
        {
            await service.UpdatePeriodAsync(Actor(user), id, request, ct);
            return Results.NoContent();
        });
        manage.MapPost("/grades", async (GradeRequest request, ClaimsPrincipal user, AcademicService service, CancellationToken ct) =>
            Results.Ok(new { id = await service.SaveGradeAsync(Actor(user), null, request, ct) }));
        manage.MapPost("/grades/{id:guid}", async (Guid id, GradeRequest request, ClaimsPrincipal user, AcademicService service, CancellationToken ct) =>
        {
            await service.SaveGradeAsync(Actor(user), id, request, ct);
            return Results.NoContent();
        });
        manage.MapPost("/years/{id:guid}/sections", async (Guid id, SectionRequest request, ClaimsPrincipal user, AcademicService service, CancellationToken ct) =>
            Results.Ok(new { id = await service.SaveSectionAsync(Actor(user), id, null, request, ct) }));
        manage.MapPost("/years/{yearId:guid}/sections/{id:guid}", async (Guid yearId, Guid id, SectionRequest request, ClaimsPrincipal user, AcademicService service, CancellationToken ct) =>
        {
            await service.SaveSectionAsync(Actor(user), yearId, id, request, ct);
            return Results.NoContent();
        });
        manage.MapPost("/years/{yearId:guid}/sections/{id:guid}/remove", async (Guid yearId, Guid id, VersionRequest request, ClaimsPrincipal user, AcademicService service, CancellationToken ct) =>
        {
            await service.RemoveSectionAsync(Actor(user), yearId, id, request, ct);
            return Results.NoContent();
        });

        var enrollmentRead = app.MapGroup("/api/v1").RequireAuthorization(Permissions.AcademicRead).AddEndpointFilter<ApiWriteFilter>();
        enrollmentRead.MapGet("/years/{yearId:guid}/enrollments", (Guid yearId, Guid? gradeId, Guid? sectionId, string? status,
                string? search, int? page, EnrollmentService service, CancellationToken ct) =>
            service.ListAsync(yearId, gradeId, sectionId, status, search, page ?? 1, ct));
        enrollmentRead.MapGet("/years/{yearId:guid}/enrollments/candidates", (Guid yearId, string? search, Guid? fromYearId, Guid? fromGradeId,
                Guid? fromSectionId, BulkEnrollmentService service, CancellationToken ct) =>
            service.CandidatesAsync(yearId, search, fromYearId, fromGradeId, fromSectionId, ct));
        enrollmentRead.MapGet("/people/{id:guid}/enrollments", (Guid id, EnrollmentService service, CancellationToken ct) =>
            service.StudentHistoryAsync(id, ct));

        var enrollment = app.MapGroup("/api/v1/enrollments").RequireAuthorization(Permissions.EnrollmentManage).AddEndpointFilter<ApiWriteFilter>();
        enrollment.MapPost("", async (EnrollRequest request, ClaimsPrincipal user, EnrollmentService service, CancellationToken ct) =>
            Results.Ok(new { id = await service.EnrollAsync(Actor(user), request, ct) }));
        enrollment.MapPost("/bulk", async (BulkEnrollRequest request, ClaimsPrincipal user, BulkEnrollmentService service, CancellationToken ct) =>
            Results.Ok(await service.EnrollManyAsync(Actor(user), request, ct)));
        enrollment.MapPost("/import", async (Guid yearId, bool? apply, IFormFile file, ClaimsPrincipal user, BulkEnrollmentService service,
            CancellationToken ct) =>
        {
            if (file.Length is 0 or > 5 * 1024 * 1024) throw OperationException.Invalid("Sube un archivo .xlsx o .csv de hasta 5 MB.");
            await using var stream = file.OpenReadStream();
            return Results.Ok(await service.ImportAsync(Actor(user), yearId, stream, file.FileName, apply == true, ct));
        }).WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(6 * 1024 * 1024));
        enrollment.MapGet("/import/template", () => Results.File(
            System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(
                "Nombres;Apellidos;Código;Fecha de nacimiento;Grado;Sección\r\nAna Lucía;Pérez López;;15/03/2018;1P;A\r\n")).ToArray(),
            "text/csv; charset=utf-8", "plantilla-inscripcion.csv"));
        enrollment.MapPost("/{id:guid}/placement", async (Guid id, PlacementRequest request, ClaimsPrincipal user, EnrollmentService service, CancellationToken ct) =>
        {
            await service.ChangePlacementAsync(Actor(user), id, request, ct);
            return Results.NoContent();
        });
        enrollment.MapPost("/{id:guid}/withdraw", async (Guid id, WithdrawRequest request, ClaimsPrincipal user, EnrollmentService service, CancellationToken ct) =>
        {
            await service.WithdrawAsync(Actor(user), id, request, ct);
            return Results.NoContent();
        });
        enrollment.MapPost("/{id:guid}/reactivate", async (Guid id, PlacementRequest request, ClaimsPrincipal user, EnrollmentService service, CancellationToken ct) =>
        {
            await service.ReactivateAsync(Actor(user), id, request, ct);
            return Results.NoContent();
        });
    }

    public static void MapCourses(this WebApplication app)
    {
        var read = app.MapGroup("/api/v1").RequireAuthorization(Permissions.AcademicRead).AddEndpointFilter<ApiWriteFilter>();
        read.MapGet("/academic/subjects", (CourseService service, CancellationToken ct) => service.SubjectsAsync(ct));
        read.MapGet("/years/{yearId:guid}/courses", (Guid yearId, Guid? gradeId, CourseService service, CancellationToken ct) =>
            service.CoursesAsync(yearId, gradeId, ct));
        read.MapGet("/academic/staff", (string? search, CourseService service, CancellationToken ct) => service.StaffAsync(search, ct));

        var manage = app.MapGroup("/api/v1").RequireAuthorization(Permissions.AcademicManage).AddEndpointFilter<ApiWriteFilter>();
        manage.MapPost("/academic/subjects", async (SubjectRequest request, ClaimsPrincipal user, CourseService service, CancellationToken ct) =>
            Results.Ok(new { id = await service.SaveSubjectAsync(Actor(user), null, request, ct) }));
        manage.MapPost("/academic/subjects/{id:guid}", async (Guid id, SubjectRequest request, ClaimsPrincipal user, CourseService service, CancellationToken ct) =>
        {
            await service.SaveSubjectAsync(Actor(user), id, request, ct);
            return Results.NoContent();
        });
        manage.MapPost("/years/{yearId:guid}/courses", async (Guid yearId, CreateCoursesRequest request, ClaimsPrincipal user, CourseService service, CancellationToken ct) =>
            Results.Ok(new { created = await service.CreateCoursesAsync(Actor(user), yearId, request, ct) }));
        manage.MapPost("/years/{yearId:guid}/courses/copy", async (Guid yearId, CopyCoursesRequest request, ClaimsPrincipal user, CourseService service, CancellationToken ct) =>
            Results.Ok(await service.CopyCoursesAsync(Actor(user), yearId, request, ct)));
        manage.MapPost("/years/{yearId:guid}/copy-cycle", async (Guid yearId, CopyCycleRequest request, ClaimsPrincipal user, CycleSetupService service,
                CancellationToken ct) => Results.Ok(await service.CopyAsync(Actor(user), yearId, request, ct)));
        manage.MapPost("/academic/teachers/assign-many", async (AssignManyRequest request, ClaimsPrincipal user, CourseService service,
                CancellationToken ct) => Results.Ok(await service.AssignManyAsync(Actor(user), request, ct)));
        manage.MapPost("/courses/{id:guid}/teachers", async (Guid id, AssignTeacherRequest request, ClaimsPrincipal user, CourseService service, CancellationToken ct) =>
            Results.Ok(new { id = await service.AssignAsync(Actor(user), id, request, ct) }));
        manage.MapPost("/courses/{id:guid}/teachers/{assignmentId:guid}/end", async (Guid id, Guid assignmentId, EndAssignmentRequest request,
            ClaimsPrincipal user, CourseService service, CancellationToken ct) =>
        {
            await service.EndAssignmentAsync(Actor(user), id, assignmentId, request, ct);
            return Results.NoContent();
        });

        // Roster: academic staff, or the teacher with an assignment active today (resource check, not a menu flag).
        var scoped = app.MapGroup("/api/v1").RequireAuthorization("ActiveAccount").AddEndpointFilter<ApiWriteFilter>();
        scoped.MapGet("/courses/{id:guid}/roster", async (Guid id, DateOnly? on, ClaimsPrincipal user, CourseService service, CancellationToken ct) =>
            await service.CanViewAsync(Actor(user), id, ct) ? Results.Ok(await service.RosterAsync(id, on, ct)) : Results.NotFound());
        scoped.MapGet("/me/courses", (ClaimsPrincipal user, CourseService service, CancellationToken ct) => service.MyCoursesAsync(Actor(user), ct));
    }

    private static Guid Actor(ClaimsPrincipal user) => PeopleEndpoints.Actor(user);
}
