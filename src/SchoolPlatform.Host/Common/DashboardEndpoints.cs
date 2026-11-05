using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Gate;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Host.Common;

public sealed record DashboardAcademic(string Year, int Enrolled, string? NextPeriod, int Recorded, int Expected);
public sealed record DashboardFinance(decimal Pending, int StudentsWithBalance, int PaymentsToday, decimal CollectedToday, string Currency);
public sealed record DashboardGate(int Expected, int Arrived, int Inside, int Left);
/// <summary>Only the sections the account may read are filled; the rest are null.</summary>
public sealed record Dashboard(DashboardAcademic? Academic, DashboardFinance? Finance, DashboardGate? Gate, int? People);

/// <summary>"Today at school" figures for the home page, computed from SQL on each read and filtered by permission.</summary>
public static class DashboardEndpoints
{
    public static void MapDashboard(this WebApplication app) =>
        app.MapGroup("/api/v1").RequireAuthorization("ActiveAccount").AddEndpointFilter<ApiWriteFilter>()
            .MapGet("/dashboard", async (ClaimsPrincipal user, SchoolDbContext db, CourseServiceClock clock, GateService gate,
                SchoolPlatform.Host.Grades.GradeService grades, Microsoft.Extensions.Options.IOptions<SchoolPlatform.Host.Branding.InstitutionOptions> school,
                CancellationToken ct) =>
            {
                var actor = PeopleEndpoints.Actor(user);
                var granted = (await db.Permissions.AsNoTracking().Where(x => x.UserId == actor).Select(x => x.Permission).ToArrayAsync(ct)).ToHashSet();
                var today = clock.Today();
                DashboardAcademic? academic = null;
                if (granted.Contains(Permissions.AcademicRead) &&
                    await db.Years.AsNoTracking().Where(x => x.IsDefault).Select(x => new { x.Id, x.Label }).FirstOrDefaultAsync(ct) is { } year)
                {
                    var enrolled = await db.Enrollments.CountAsync(x => x.AcademicYearId == year.Id && x.Status == EnrollmentStatus.Active, ct);
                    var next = (await grades.PublicationsAsync(year.Id, ct)).FirstOrDefault(x => !x.Published);
                    academic = new(year.Label, enrolled, next?.Name, next?.Recorded ?? 0, next?.Expected ?? 0);
                }
                DashboardFinance? finance = null;
                if (granted.Contains(Permissions.FinanceRead))
                {
                    var balances = await db.Charges.AsNoTracking().Where(x => x.VoidedAt == null)
                        .Select(x => new
                        {
                            x.StudentId,
                            Balance = x.Amount - (db.ChargeAdjustments.Where(a => a.ChargeId == x.Id).Sum(a => (decimal?)a.Amount) ?? 0)
                                - (db.PaymentAllocations.Where(a => a.ChargeId == x.Id && db.Payments.Any(p => p.Id == a.PaymentId && p.ReversedAt == null))
                                    .Sum(a => (decimal?)a.Amount) ?? 0)
                        }).Where(x => x.Balance > 0).ToArrayAsync(ct);
                    var paid = await db.Payments.AsNoTracking().Where(x => x.ReceivedOn == today && x.ReversedAt == null)
                        .Select(x => x.Amount).ToArrayAsync(ct);
                    finance = new(balances.Sum(x => x.Balance), balances.Select(x => x.StudentId).Distinct().Count(), paid.Length, paid.Sum(),
                        school.Value.Locale.Currency);
                }
                DashboardGate? gateSummary = null;
                if (granted.Contains(Permissions.AttendanceRead))
                {
                    var daily = await gate.DailyAsync(today, null, null, ct);
                    gateSummary = new(daily.Expected, daily.Arrived, daily.Inside, daily.Left);
                }
                int? people = granted.Contains(Permissions.PeopleRead) ? await db.Persons.CountAsync(x => x.Status == PersonStatus.Active, ct) : null;
                return new Dashboard(academic, finance, gateSummary, people);
            });
}
