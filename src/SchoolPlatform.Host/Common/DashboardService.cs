using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Branding;
using SchoolPlatform.Host.Finance;
using SchoolPlatform.Host.Gate;
using SchoolPlatform.Host.Grades;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.Learning;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Host.Common;

public sealed record DashboardYear(Guid Id, string Label, string Status, bool IsDefault);
public sealed record GradeCount(string Grade, int Count);
public sealed record DashboardAcademic(string Year, int Enrolled, string? NextPeriod, int Recorded, int Expected,
    int Courses, int UnassignedCourses, GradeCount[] ByGrade, PublicationView[] Periods);
public sealed record CollectionDay(DateOnly Date, decimal Amount);
public sealed record DashboardReceipt(Guid Id, long Number, DateOnly Date, decimal Applied);
public sealed record DashboardFinance(decimal Pending, int StudentsWithBalance, int PaymentsToday, decimal CollectedToday,
    string Currency, decimal Overdue, decimal DueToday, decimal Future, int ChargeCount, int EnrollmentsWithoutCharges,
    CollectionDay[] LastSevenDays, DashboardReceipt[] RecentReceipts);
public sealed record DashboardGate(int Expected, int Arrived, int Inside, int Left);
public sealed record DashboardTask(string Course, string Title, DateTimeOffset DueAt, bool Overdue);
public sealed record DashboardStudentCourse(Guid Id, string Subject, string Grade, string? Section);
public sealed record DashboardStudent(Guid Id, string Name, string Code, bool CanAcademic, bool CanFinance, bool? Enrolled,
    int? PendingTasks, DashboardTask[] Tasks, decimal? Balance, int? Charges, bool IsSelf, int? Courses, DashboardStudentCourse[] CourseItems);
public sealed record DashboardTeaching(Guid Id, string Subject, string Grade, string? Section, int Students,
    int PendingReviews, int AttendanceRecorded, DashboardTask[] Upcoming);
public sealed record DashboardAccess(int PendingActivation, int Suspended);
public sealed record Dashboard(DateTimeOffset AsOf, DateOnly On, DashboardYear[] Years, Guid? YearId,
    DashboardAcademic? Academic, DashboardFinance? Finance, DashboardGate? Gate, int? People,
    DashboardTeaching[] Teaching, DashboardStudent[] Students, DashboardAccess? Access);

/// <summary>Operational read model. Effective permissions include MFA; personal data uses current resource access.
/// No grade values, clearance conclusions or fictional substitutes are returned.</summary>
public sealed class DashboardService(SchoolDbContext db, IAuthorizationService authorization, CourseServiceClock clock,
    TimeProvider time, IOptions<InstitutionOptions> school, CourseService courses, GradeService grades,
    GateService gate, PeopleService people, LearningService learning, ChargeService charges)
{
    public async Task<Dashboard> ReadAsync(ClaimsPrincipal user, Guid? requestedYear, CancellationToken ct)
    {
        var actor = PeopleEndpoints.Actor(user);
        async Task<bool> Can(string permission) => (await authorization.AuthorizeAsync(user, permission)).Succeeded;
        var academicRead = await Can(Permissions.AcademicRead);
        var financeRead = await Can(Permissions.FinanceRead);
        var attendanceRead = await Can(Permissions.AttendanceRead);
        var peopleRead = await Can(Permissions.PeopleRead);
        var accessRead = await Can(Permissions.ManageAccounts);
        var today = clock.Today();
        var now = time.GetUtcNow();
        var profile = await people.MeAsync(actor, ct);
        var ownCourses = profile.Person?.IsStaff == true ? await courses.MyCoursesAsync(actor, ct) : [];
        var personalIds = profile.Students.Where(x => x.Faculties.Contains(Faculties.AcademicRecords) || x.Faculties.Contains(Faculties.Finance))
            .Select(x => x.Id).ToList();
        if (profile.Person?.StudentCode is not null) personalIds.Add(profile.Person.Id);
        var courseYearIds = ownCourses.Select(x => x.AcademicYearId).Distinct().ToArray();
        var yearRows = await db.Years.AsNoTracking().Where(y => academicRead || financeRead || courseYearIds.Contains(y.Id) ||
                db.Enrollments.Any(e => e.AcademicYearId == y.Id && e.Status != EnrollmentStatus.Cancelled && personalIds.Contains(e.StudentId)))
            .OrderByDescending(y => y.StartsOn).ToArrayAsync(ct);
        var selected = requestedYear is { } id
            ? yearRows.SingleOrDefault(y => y.Id == id) ?? throw OperationException.NotFound("No encontramos ese ciclo o no tienes acceso a su resumen.")
            : yearRows.FirstOrDefault(y => y.IsDefault) ?? yearRows.FirstOrDefault();
        var yearId = selected?.Id;
        DashboardAcademic? academic = null;
        if (academicRead && selected is not null)
        {
            var enrollment = db.Enrollments.AsNoTracking().Where(e => e.AcademicYearId == yearId && e.Status == EnrollmentStatus.Active);
            var enrolled = await enrollment.CountAsync(ct);
            var byGrade = await (from e in enrollment
                                 join p in db.Placements on e.Id equals p.EnrollmentId
                                 where p.EndsOn == null
                                 join g in db.Grades on p.GradeLevelId equals g.Id
                                 group e by new { g.Id, g.Name, g.Sequence } into g
                                 orderby g.Key.Sequence, g.Key.Name
                                 select new GradeCount(g.Key.Name, g.Count())).ToArrayAsync(ct);
            var yearCourses = await db.Courses.AsNoTracking().Where(c => c.AcademicYearId == yearId).Select(c => c.Id).ToArrayAsync(ct);
            var unassigned = await db.Courses.CountAsync(c => c.AcademicYearId == yearId && !db.TeachingAssignments.Any(t =>
                t.CourseId == c.Id && (t.EndsOn == null || t.EndsOn >= today)), ct);
            var periods = await grades.PublicationsAsync(selected.Id, ct);
            var next = periods.FirstOrDefault(x => !x.Published && x.StartsOn <= today);
            academic = new(selected.Label, enrolled, next?.Name, next?.Recorded ?? 0, next?.Expected ?? 0,
                yearCourses.Length, unassigned, byGrade, periods);
        }
        var finance = financeRead && selected is not null ? await FinanceAsync(selected.Id, today, ct) : null;
        DashboardGate? gateSummary = null;
        if (attendanceRead)
        {
            var daily = await gate.DailyAsync(today, null, null, ct);
            gateSummary = new(daily.Expected, daily.Arrived, daily.Inside, daily.Left);
        }
        var teaching = new List<DashboardTeaching>();
        foreach (var course in ownCourses.Where(x => x.AcademicYearId == yearId))
        {
            var reviews = await (from s in db.Submissions.AsNoTracking()
                                 join a in db.Assignments on s.AssignmentId equals a.Id
                                 where a.CourseId == course.Id && s.Status == SubmissionStatus.Submitted
                                 select s.Id).CountAsync(ct);
            var attendance = await (from r in db.AttendanceRecords.AsNoTracking()
                                    join s in db.AttendanceSessions on r.SessionId equals s.Id
                                    where s.CourseId == course.Id && s.Date == today
                                    select r.Id).CountAsync(ct);
            var upcoming = await db.Assignments.AsNoTracking().Where(a => a.CourseId == course.Id &&
                    a.Status == AssignmentStatus.Published && a.DueAt >= now && a.DueAt <= now.AddDays(7))
                .OrderBy(a => a.DueAt).Take(3).Select(a => new DashboardTask(course.Subject, a.Title, a.DueAt, false)).ToArrayAsync(ct);
            teaching.Add(new(course.Id, course.Subject, course.Grade, course.Section, course.Students, reviews, attendance, upcoming));
        }
        var students = new List<DashboardStudent>();
        var studentProfiles = profile.Students.ToList();
        if (profile.Person?.StudentCode is { } code)
            studentProfiles.Insert(0, new(profile.Person.Id, profile.Person.DisplayName, code, "Self", []));
        foreach (var student in studentProfiles.DistinctBy(x => x.Id))
        {
            var access = await people.StudentAccessAsync(actor, student.Id, ct);
            if (access is null) continue;
            var canAcademic = access.Has(Faculties.AcademicRecords);
            var canFinance = !access.IsSelf && access.Faculties.Contains(Faculties.Finance);
            bool? enrolled = (canAcademic || canFinance) && yearId is not null
                ? await db.Enrollments.AnyAsync(e => e.StudentId == student.Id && e.AcademicYearId == yearId && e.Status != EnrollmentStatus.Cancelled, ct) : null;
            DashboardTask[] tasks = [];
            int? pendingTasks = null;
            int? courseCount = null;
            DashboardStudentCourse[] courseItems = [];
            if (canAcademic && enrolled == true)
            {
                courseItems = await db.Courses.AsNoTracking().Where(c => c.AcademicYearId == yearId &&
                    db.Placements.Any(p => p.GradeLevelId == c.GradeLevelId && (c.SectionId == null || p.SectionId == c.SectionId) &&
                        p.EndsOn == null && db.Enrollments.Any(e => e.Id == p.EnrollmentId && e.StudentId == student.Id &&
                            e.AcademicYearId == yearId && e.Status == EnrollmentStatus.Active)))
                    .OrderBy(c => db.Subjects.Where(s => s.Id == c.SubjectId).Select(s => s.Name).First())
                    .Select(c => new DashboardStudentCourse(c.Id, db.Subjects.Where(s => s.Id == c.SubjectId).Select(s => s.Name).First(),
                        db.Grades.Where(g => g.Id == c.GradeLevelId).Select(g => g.Name).First(),
                        db.Sections.Where(s => s.Id == c.SectionId).Select(s => s.Name).FirstOrDefault()))
                    .ToArrayAsync(ct);
                courseCount = courseItems.Length;
                var allowed = await learning.StudentAssignmentsAsync(actor, student.Id, ct) ?? [];
                var activityIds = await (from a in db.Assignments.AsNoTracking()
                                         join c in db.Courses on a.CourseId equals c.Id
                                         where c.AcademicYearId == yearId select a.Id).ToArrayAsync(ct);
                var pending = allowed.Where(a => activityIds.Contains(a.Id) && !a.Closed && a.SubmissionStatus == null &&
                    (a.DueAt >= now || a.AllowLate)).OrderBy(a => a.DueAt).ToArray();
                pendingTasks = pending.Length;
                tasks = pending.Select(a => new DashboardTask(a.Course, a.Title, a.DueAt, a.DueAt < now)).ToArray();
            }
            Statement? statement = canFinance && enrolled == true ? await charges.StatementAsync(student.Id, yearId, ct) : null;
            students.Add(new(student.Id, student.DisplayName, student.StudentCode, canAcademic, canFinance, enrolled,
                pendingTasks, tasks, statement?.TotalBalance, statement?.Charges.Count(c => c.Status != "Voided"), access.IsSelf, courseCount, courseItems));
        }
        DashboardAccess? accountSummary = accessRead ? new(
            await db.Accounts.CountAsync(a => a.PasswordHash == null, ct),
            await db.Accounts.CountAsync(a => a.PasswordHash != null && !a.IsActive, ct)) : null;
        return new(now, today, yearRows.Select(y => new DashboardYear(y.Id, y.Label, y.Status, y.IsDefault)).ToArray(), yearId,
            academic, finance, gateSummary, peopleRead ? await db.Persons.CountAsync(p => p.Status == PersonStatus.Active, ct) : null,
            teaching.ToArray(), students.ToArray(), accountSummary);
    }

    private async Task<DashboardFinance> FinanceAsync(Guid yearId, DateOnly today, CancellationToken ct)
    {
        var balances = await db.Charges.AsNoTracking().Where(x => x.AcademicYearId == yearId && x.VoidedAt == null)
            .Select(x => new
            {
                x.StudentId, x.DueOn,
                Balance = x.Amount - (db.ChargeAdjustments.Where(a => a.ChargeId == x.Id).Sum(a => (decimal?)a.Amount) ?? 0)
                    - (db.PaymentAllocations.Where(a => a.ChargeId == x.Id && db.Payments.Any(p => p.Id == a.PaymentId && p.ReversedAt == null))
                        .Sum(a => (decimal?)a.Amount) ?? 0)
            }).ToArrayAsync(ct);
        // Attribute only explicit allocations to their own cycle; a mixed-cycle receipt must never be counted twice.
        var payments = from a in db.PaymentAllocations.AsNoTracking()
                       join p in db.Payments on a.PaymentId equals p.Id
                       join c in db.Charges on a.ChargeId equals c.Id
                       where c.AcademicYearId == yearId && c.VoidedAt == null && p.ReversedAt == null
                       group a by new { p.Id, p.ReceiptNumber, p.ReceivedOn, p.RecordedAt } into g
                       select new { g.Key.Id, g.Key.ReceiptNumber, g.Key.ReceivedOn, g.Key.RecordedAt, Amount = g.Sum(a => a.Amount) };
        var week = await payments.Where(p => p.ReceivedOn >= today.AddDays(-6) && p.ReceivedOn <= today).ToArrayAsync(ct);
        var recent = await payments.OrderByDescending(p => p.RecordedAt).ThenByDescending(p => p.ReceiptNumber).Take(5)
            .Select(p => new DashboardReceipt(p.Id, p.ReceiptNumber, p.ReceivedOn, p.Amount)).ToArrayAsync(ct);
        var withoutCharges = await db.Enrollments.CountAsync(e => e.AcademicYearId == yearId && e.Status == EnrollmentStatus.Active &&
            !db.Charges.Any(c => c.EnrollmentId == e.Id && c.VoidedAt == null), ct);
        var unpaid = balances.Where(b => b.Balance > 0).ToArray();
        return new(unpaid.Sum(b => b.Balance), unpaid.Select(b => b.StudentId).Distinct().Count(),
            week.Count(p => p.ReceivedOn == today), week.Where(p => p.ReceivedOn == today).Sum(p => p.Amount), school.Value.Locale.Currency,
            unpaid.Where(b => b.DueOn < today).Sum(b => b.Balance), unpaid.Where(b => b.DueOn == today).Sum(b => b.Balance),
            unpaid.Where(b => b.DueOn > today).Sum(b => b.Balance), balances.Length, withoutCharges,
            Enumerable.Range(-6, 7).Select(offset => new CollectionDay(today.AddDays(offset), week.Where(p => p.ReceivedOn == today.AddDays(offset)).Sum(p => p.Amount))).ToArray(), recent);
    }
}
