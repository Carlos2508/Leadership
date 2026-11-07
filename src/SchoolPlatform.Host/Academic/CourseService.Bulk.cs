using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.People;

namespace SchoolPlatform.Host.Academic;

public sealed record AssignManyRequest(Guid StaffId, string Role, Guid[] CourseIds, DateOnly? StartsOn, string Reason);
public sealed record AssignManyResult(int Assigned, int AlreadyAssigned);

public sealed partial class CourseService
{
    /// <summary>
    /// Assigns one staff person to several courses (any grades/sections of the same year) in one transaction.
    /// Courses where the person is already assigned are counted and skipped, so the operation can be repeated safely.
    /// </summary>
    public async Task<AssignManyResult> AssignManyAsync(Guid actor, AssignManyRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.AcademicManage);
        var reason = Reasons.Require(request.Reason);
        if (request.Role is not (TeachingRoles.Lead or TeachingRoles.Assistant)) throw OperationException.Invalid("Selecciona la función docente.");
        var ids = (request.CourseIds ?? []).Distinct().ToArray();
        if (ids.Length is 0 or > 200) throw OperationException.Invalid("Selecciona entre 1 y 200 cursos.");
        var selected = await db.Courses.AsNoTracking().Where(x => ids.Contains(x.Id)).ToArrayAsync(ct);
        if (selected.Length != ids.Length) throw CourseNotFound();
        var yearIds = selected.Select(x => x.AcademicYearId).Distinct().ToArray();
        if (yearIds.Length != 1) throw OperationException.Invalid("Selecciona cursos de un mismo ciclo.");
        var year = await db.Years.AsNoTracking().SingleAsync(x => x.Id == yearIds[0], ct);
        if (year.Status == YearStatus.Closed) throw new OperationException(409, "year-closed", "El ciclo está cerrado.");
        if (!await db.Staff.AnyAsync(s => s.PersonId == request.StaffId && db.Persons.Any(p => p.Id == s.PersonId && p.Status == PersonStatus.Active), ct))
            throw OperationException.Invalid("Selecciona a una persona activa con perfil de personal.");
        var starts = request.StartsOn ?? Clamp(Today(), year);
        if (starts < year.StartsOn || starts > year.EndsOn) throw OperationException.Invalid("La fecha debe estar dentro del ciclo.");
        var already = await db.TeachingAssignments.AsNoTracking()
            .Where(x => ids.Contains(x.CourseId) && x.StaffId == request.StaffId && x.EndsOn == null).Select(x => x.CourseId).ToArrayAsync(ct);
        var assigned = 0;
        foreach (var course in selected.Where(c => !already.Contains(c.Id)))
        {
            db.TeachingAssignments.Add(new TeachingAssignment
            {
                Id = Guid.NewGuid(), CourseId = course.Id, StaffId = request.StaffId, Role = request.Role, StartsOn = starts,
                CreatedAt = time.GetUtcNow(), CreatedBy = actor
            });
            audit.Record("TeacherAssigned", actor, "Course", course.Id, reason, request.Role);
            assigned++;
        }
        if (assigned > 0) await db.SaveChangesAsync(ct);
        return new(assigned, already.Length);
    }
}
