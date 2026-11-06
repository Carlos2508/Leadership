using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.Finance;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.Learning;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Tests;

public sealed class DashboardSqlTests : SqlTestBase
{
    private DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Guatemala").DateTime);
    private Task<Dashboard> Read(Guid? actor = null, Guid? year = null) => Use<DashboardService, Dashboard>(s =>
        s.ReadAsync(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, (actor ?? Owner).ToString())], "Test")), year, default));
    private async Task<Guid> Year(int number)
    {
        var start = new DateOnly(number, 1, 1);
        var id = await Use<AcademicService, Guid>(x => x.CreateYearAsync(Owner, new(number.ToString(), start, new(number, 12, 31)), default));
        await Use<AcademicService, Guid>(x => x.AddPeriodAsync(Owner, id, new(null, start, start.AddDays(60)), default));
        await Use<AcademicService>(async x => await x.SetYearStatusAsync(Owner, id, new((await x.YearAsync(id, default)).Year.Version, YearStatus.EnrollmentOpen, Reason), default));
        return id;
    }
    private async Task<Guid> Person(string name, string? code = null, bool guardian = false, string? position = null) =>
        (await Use<PeopleService, CreatedPerson>(x => x.CreateAsync(Owner, new(name, "Dashboard sintético", null, null, [], code, guardian, true, position), default))).Id;
    private Task<Guid> Grade() => Use<AcademicService, Guid>(x => x.SaveGradeAsync(Owner, null, new("DASH", "Grado sintético", "", 1), default));
    private Task<Guid> Enroll(Guid person, Guid year, Guid grade, int number) => Use<EnrollmentService, Guid>(x =>
        x.EnrollAsync(Owner, new(person, year, grade, null, new(number, 1, 1), null), default));
    private Task Link(Guid person, Guid account) => Use<PeopleService>(async x =>
        await x.LinkAccountAsync(Owner, person, new((await x.GetAsync(person, default)).Version, account, Reason), default));

    [Fact]
    public async Task Pending_activations_are_not_reported_as_suspended_accounts()
    {
        var invitation = await Use<AccountAccessService, IssueCodeResult>(x => x.InviteAsync(Owner, new("pending-dashboard", "basic", Reason)));
        Assert.Equal(new DashboardAccess(1, 0), (await Read()).Access);
        await Use<AccountAccessService>(x => x.RedeemAsync(invitation.Code, Password));
        var version = await Use<IdentityDbContext, string>(db => db.Users.Where(u => u.Id == invitation.UserId).Select(u => u.ConcurrencyStamp!).SingleAsync());
        await Use<AccountAccessService>(x => x.SetActiveAsync(Owner, invitation.UserId, new(version, Reason, IsActive: false)));
        Assert.Equal(new DashboardAccess(0, 1), (await Read()).Access);
    }

    [Fact]
    public async Task Money_is_attributed_by_cycle_allocations_with_adjustments_reversals_and_due_dates()
    {
        var current = await Year(Today.Year);
        var previous = await Year(Today.Year - 1);
        var grade = await Grade();
        var person = await Person("Estudiante", "DASH-1");
        var enrollment = await Enroll(person, current, grade, Today.Year);
        var oldEnrollment = await Enroll(person, previous, grade, Today.Year - 1);
        await Enroll(await Person("Sin cargos", "DASH-2"), current, grade, Today.Year);
        var concept = await Use<FinanceSetupService, Guid>(x => x.SaveConceptAsync(Owner, null, new("DASH", "Obligación sintética", false, false), default));
        Task<Guid> Charge(Guid e, int occurrence, decimal amount, DateOnly due) => Use<ChargeService, Guid>(x =>
            x.CreateSingleAsync(Owner, new(e, concept, null, occurrence, amount, due, "Cargo de prueba"), default));
        var overdue = await Charge(enrollment, 1, 200, Today.AddDays(-1));
        await Charge(enrollment, 2, 100, Today.AddDays(1));
        await Charge(enrollment, 3, 40, Today);
        var old = await Charge(oldEnrollment, 1, 300, new(Today.Year - 1, 12, 31));
        var version = await Use<SchoolDbContext, string>(async db => Versions.Encode((await db.Charges.SingleAsync(x => x.Id == overdue)).Version));
        await Use<ChargeService>(x => x.AdjustAsync(Owner, overdue, new(version, AdjustmentKinds.Discount, 20, Reason), default));
        var paid = await Use<PaymentService, ReceiptView>(x => x.RecordAsync(Owner,
            new(Guid.NewGuid().ToString("N"), "Pagador sintético", "Cash", null, Today, null, 120, [new(overdue, 50), new(old, 70)]), default));
        var reversed = await Use<PaymentService, ReceiptView>(x => x.RecordAsync(Owner,
            new(Guid.NewGuid().ToString("N"), "Pagador sintético", "Cash", null, Today, null, 30, [new(overdue, 30)]), default));
        var paymentVersion = await Use<SchoolDbContext, string>(async db => Versions.Encode((await db.Payments.SingleAsync(x => x.Id == reversed.PaymentId)).Version));
        await Use<PaymentService>(x => x.ReverseAsync(Owner, reversed.PaymentId, new(paymentVersion, Reason), default));
        var result = await Read(year: current);
        Assert.Equal(current, result.YearId);
        var finance = Assert.IsType<DashboardFinance>(result.Finance);
        Assert.Equal((270m, 130m, 40m, 100m), (finance.Pending, finance.Overdue, finance.DueToday, finance.Future));
        Assert.Equal((50m, 1, 1), (finance.CollectedToday, finance.PaymentsToday, finance.EnrollmentsWithoutCharges));
        Assert.Equal(7, finance.LastSevenDays.Length);
        Assert.Equal(50m, finance.LastSevenDays.Sum(x => x.Amount));
        Assert.Equal((paid.PaymentId, 50m), (Assert.Single(finance.RecentReceipts).Id, finance.RecentReceipts[0].Applied));
        Assert.Equal(2, result.Academic!.Enrolled);
        Assert.Equal(2, Assert.Single(result.Academic.ByGrade).Count);
        var oldResult = await Read(year: previous);
        Assert.Equal((230m, 70m), (oldResult.Finance!.Pending, oldResult.Finance.CollectedToday));
        using var anonymous = App.Browser();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/dashboard")).StatusCode);
    }

    [Fact]
    public async Task Personal_summary_rechecks_faculties_and_teacher_assignments_without_exposing_grades()
    {
        var year = await Year(Today.Year);
        var grade = await Grade();
        var student = await Person("Académico", "DASH-A");
        var financeOnly = await Person("Finanzas", "DASH-F");
        await Enroll(student, year, grade, Today.Year);
        await Enroll(financeOnly, year, grade, Today.Year);
        var guardian = await Person("Encargado", guardian: true);
        var familyUser = await Account("dashboard-family");
        await Link(guardian, familyUser);
        var relationship = await Use<PeopleService, Guid>(x => x.CreateRelationshipAsync(Owner, new(student, guardian, "Other", null, [Faculties.AcademicRecords], Reason), default));
        await Use<PeopleService, Guid>(x => x.CreateRelationshipAsync(Owner, new(financeOnly, guardian, "Other", null, [Faculties.Finance], Reason), default));
        var teacher = await Person("Docente", position: "Docente");
        var teacherUser = await Account("dashboard-teacher");
        await Link(teacher, teacherUser);
        var course = Guid.NewGuid();
        await Use<SchoolDbContext>(async db =>
        {
            var subject = new Subject { Id = Guid.NewGuid(), Code = "DASH", Name = "Curso autorizado" };
            db.Subjects.Add(subject);
            db.Courses.Add(new Course { Id = course, AcademicYearId = year, GradeLevelId = grade, SubjectId = subject.Id });
            db.TeachingAssignments.Add(new TeachingAssignment { Id = Guid.NewGuid(), CourseId = course, StaffId = teacher, StartsOn = Today.AddDays(-1), CreatedBy = Owner });
            var assignment = new Assignment { Id = Guid.NewGuid(), CourseId = course, Title = "Tarea autorizada", DueAt = DateTimeOffset.UtcNow.AddDays(1), PublishedAt = DateTimeOffset.UtcNow.AddDays(-1), CreatedBy = Owner, Status = AssignmentStatus.Published };
            db.Assignments.Add(assignment);
            db.Submissions.Add(new Submission { Id = Guid.NewGuid(), AssignmentId = assignment.Id, StudentId = financeOnly, Text = "Entrega sintética", SubmittedBy = Owner, Status = SubmissionStatus.Submitted });
            await db.SaveChangesAsync();
        });
        var family = await Read(familyUser, year);
        Assert.Null(family.Academic);
        Assert.Null(family.Finance);
        Assert.Null(family.Access);
        var academicChild = family.Students.Single(x => x.Id == student);
        Assert.Equal(1, academicChild.PendingTasks);
        Assert.Equal("Tarea autorizada", Assert.Single(academicChild.Tasks).Title);
        Assert.Null(academicChild.Balance);
        var limitedChild = family.Students.Single(x => x.Id == financeOnly);
        Assert.Null(limitedChild.PendingTasks);
        Assert.Empty(limitedChild.Tasks);
        Assert.Equal(0m, limitedChild.Balance);
        Assert.Equal(0, limitedChild.Charges);
        Assert.DoesNotContain("score", JsonSerializer.Serialize(family), StringComparison.OrdinalIgnoreCase);
        var own = Assert.Single((await Read(teacherUser, year)).Teaching);
        Assert.Equal((course, 1), (own.Id, own.PendingReviews));
        var detail = await Use<PeopleService, PersonDetail>(x => x.GetAsync(guardian, default));
        await Use<PeopleService>(x => x.UpdateRelationshipAsync(Owner, relationship,
            new(detail.Students.Single(x => x.Id == relationship).Version, "Other", [], Reason), default));
        var revoked = (await Read(familyUser, year)).Students.Single(x => x.Id == student);
        Assert.False(revoked.CanAcademic);
        Assert.Null(revoked.PendingTasks);
        Assert.Empty(revoked.Tasks);
        var basic = await Account("dashboard-outsider");
        await Assert.ThrowsAsync<OperationException>(() => Read(basic, year));
        Assert.Empty((await Read(basic)).Years);
    }
}

public sealed class DashboardMfaSqlTests : SqlTestBase
{
    protected override Dictionary<string, string?> ExtraConfiguration => new()
    { ["AccountAccess:RequireMfaForPermissions:0"] = Permissions.FinanceRead };

    [Fact]
    public async Task Financial_summary_respects_the_effective_mfa_requirement()
    {
        await Use<AcademicService, Guid>(x => x.CreateYearAsync(Owner, new("2026", new(2026, 1, 1), new(2026, 12, 31)), default));
        using var browser = await SignIn(OwnerName);
        var response = await browser.GetAsync("/api/v1/dashboard");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(JsonValueKind.Null, data.GetProperty("finance").ValueKind);
        Assert.Equal(JsonValueKind.Object, data.GetProperty("academic").ValueKind);
    }
}
