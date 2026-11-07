using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Host.Academic;

public sealed record CopyCycleRequest(Guid SourceYearId, bool Periods = true, bool Teachers = true);
public sealed record CopyCycleResult(int Periods, int Sections, int Courses, int Skipped, int Teachers);

/// <summary>
/// Prepares a new year in one step from a previous one: same number of periods (split over the new dates), sections,
/// courses (grades/subjects) and, optionally, the teachers still assigned at the end of the source year. Each part is
/// idempotent (existing items are kept, nothing is duplicated), so repeating the copy after a partial setup is safe.
/// </summary>
public sealed class CycleSetupService(SchoolDbContext db, SchoolAudit audit, AcademicService academic, CourseService courses)
{
    public async Task<CopyCycleResult> CopyAsync(Guid actor, Guid yearId, CopyCycleRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.AcademicManage);
        if (request.SourceYearId == yearId) throw OperationException.Invalid("Elige otro ciclo como origen.");
        var year = await db.Years.AsNoTracking().SingleOrDefaultAsync(x => x.Id == yearId, ct) ?? throw OperationException.NotFound("No encontramos ese ciclo.");
        if (year.Status == YearStatus.Closed) throw new OperationException(409, "year-closed", "El ciclo está cerrado.");
        if (!await db.Years.AnyAsync(x => x.Id == request.SourceYearId, ct)) throw OperationException.NotFound("No encontramos el ciclo de origen.");

        var periods = 0;
        if (request.Periods && YearStatus.AllowsCalendarChanges(year.Status) && !await db.Periods.AnyAsync(x => x.AcademicYearId == yearId, ct))
        {
            var count = await db.Periods.CountAsync(x => x.AcademicYearId == request.SourceYearId, ct);
            if (count > 0)
                periods = await academic.GeneratePeriodsAsync(actor, yearId, new GeneratePeriodsRequest(Versions.Encode(year.Version), count), ct);
        }
        var sections = await academic.CopySectionsAsync(actor, yearId, new CopyFromYearRequest(request.SourceYearId), ct);
        var copied = await courses.CopyCoursesAsync(actor, yearId, new CopyCoursesRequest(request.SourceYearId, request.Teachers), ct);
        audit.Record("CycleCopied", actor, "AcademicYear", yearId, detail: $"{periods}/{sections}/{copied.Created}/{copied.Teachers}");
        await db.SaveChangesAsync(ct);
        return new(periods, sections, copied.Created, copied.Skipped, copied.Teachers);
    }
}
