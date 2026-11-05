using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.Finance;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Tests;

public sealed class FinanceSqlTests : SqlTestBase
{
    private static readonly DateOnly Start = new(2026, 1, 12);
    private Guid year, grade, tuition, cutoff;
    private Guid[] periods = [];

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        year = await Use<AcademicService, Guid>(x => x.CreateYearAsync(Owner, new("2026", Start, new(2026, 10, 30)), default));
        await Use<AcademicService, Guid>(x => x.AddPeriodAsync(Owner, year, new(null, Start, Start.AddDays(60)), default));
        await Use<AcademicService>(async x => await x.SetYearStatusAsync(Owner, year, new((await x.YearAsync(year, default)).Year.Version, YearStatus.EnrollmentOpen, Reason), default));
        grade = await Use<AcademicService, Guid>(x => x.SaveGradeAsync(Owner, null, new("1B", "Primero básico", "Básico", 1), default));
        tuition = await Use<FinanceSetupService, Guid>(x => x.SaveConceptAsync(Owner, null, new("COL", "Colegiatura", true, true), default));
        var list = new List<Guid>();
        for (var i = 1; i <= 3; i++)
        {
            var due = new DateOnly(2026, i + 1, 5);
            list.Add(await Use<FinanceSetupService, Guid>(x => x.AddPeriodAsync(Owner, year, new($"Cuota {i}", due), default)));
        }
        periods = list.ToArray();
        cutoff = await Use<FinanceSetupService, Guid>(x => x.AddCutoffAsync(Owner, year, new("Primer bimestre", 2), default));
    }

    private async Task<(Guid Student, Guid Enrollment)> Enrolled(string code, string given = "Ana")
    {
        var student = (await Use<PeopleService, CreatedPerson>(x => x.CreateAsync(Owner, new(given, "Finanzas " + code, null, null, [], code, false, true), default))).Id;
        var enrollment = await Use<EnrollmentService, Guid>(x => x.EnrollAsync(Owner, new(student, year, grade, null, Start, null), default));
        return (student, enrollment);
    }

    private async Task<GenerationResult> Generate(Guid period, decimal amount = 500m)
    {
        var request = new GenerationRequest(year, tuition, period, 1, amount, null, null, [grade], null, null);
        var preview = await Use<ChargeService, GenerationPreview>(x => x.PreviewAsync(Owner, request, default));
        return await Use<ChargeService, GenerationResult>(x => x.GenerateAsync(Owner, request with { PreviewHash = preview.PreviewHash }, default));
    }

    private Task<Statement> Statement(Guid student) => Use<ChargeService, Statement>(x => x.StatementAsync(student, year, default));
    private Task<ClearanceResult> Clearance(Guid student) => Use<ClearanceService, ClearanceResult>(x => x.CheckAsync(student, cutoff, default));
    private Task<ReceiptView> Pay(string key, params (Guid Charge, decimal Amount)[] lines) =>
        Use<PaymentService, ReceiptView>(x => x.RecordAsync(Owner, new(key, "Encargado sintético", "Cash", null, Start.AddDays(10), null,
            lines.Sum(l => l.Amount), lines.Select(l => new AllocationRequest(l.Charge, l.Amount)).ToArray()), default));
    private static string Key() => Guid.NewGuid().ToString("N");

    [Fact]
    public async Task Bulk_generation_requires_its_preview_and_never_duplicates_an_obligation()
    {
        await Enrolled("F-1");
        await Enrolled("F-2");
        var request = new GenerationRequest(year, tuition, periods[0], 1, 450m, null, null, [grade], null, null);
        var preview = await Use<ChargeService, GenerationPreview>(x => x.PreviewAsync(Owner, request, default));
        Assert.Equal((2, 0, 900m), (preview.ToCreate, preview.Existing, preview.Total));
        await Enrolled("F-3");
        Assert.Equal("preview-changed", (await Assert.ThrowsAsync<OperationException>(() =>
            Use<ChargeService, GenerationResult>(x => x.GenerateAsync(Owner, request with { PreviewHash = preview.PreviewHash }, default)))).Code);
        Assert.Equal(3, (await Generate(periods[0], 450m)).Created);
        var again = await Generate(periods[0], 450m);
        Assert.Equal((0, 3), (again.Created, again.Existing));
        Assert.Equal(3, await Use<SchoolDbContext, int>(db => db.Charges.CountAsync()));
        await Assert.ThrowsAsync<OperationException>(() => Use<ChargeService, Guid>(x => x.CreateSingleAsync(Owner,
            new(Guid.Empty, tuition, periods[0], 1, 10.555m, null, null), default)));
    }

    [Fact]
    public async Task Payments_apply_exactly_issue_numbered_receipts_and_are_idempotent()
    {
        var (student, _) = await Enrolled("P-1");
        await Generate(periods[0]);
        await Generate(periods[1]);
        var charges = (await Statement(student)).Charges;
        Assert.Equal(1000m, (await Statement(student)).TotalBalance);
        // The received amount must be fully applied; overpayment is not kept as unmanaged credit.
        await Assert.ThrowsAsync<OperationException>(() => Use<PaymentService, ReceiptView>(x => x.RecordAsync(Owner,
            new(Key(), "Pagador", "Cash", null, Start, null, 600m, [new(charges[0].Id, 500m)]), default)));
        await Assert.ThrowsAsync<OperationException>(() => Pay(Key(), (charges[0].Id, 600m)));

        var key = Key();
        var first = await Pay(key, (charges[0].Id, 500m), (charges[1].Id, 200m));
        var retry = await Pay(key, (charges[0].Id, 500m), (charges[1].Id, 200m));
        Assert.Equal(first.Receipt.Number, retry.Receipt.Number);
        Assert.Equal(1, await Use<SchoolDbContext, int>(db => db.Payments.CountAsync()));
        Assert.Equal(2, first.Receipt.Lines.Length);
        Assert.False(string.IsNullOrEmpty(first.Receipt.InstitutionName));
        var second = await Pay(Key(), (charges[1].Id, 300m));
        Assert.Equal(first.Receipt.Number + 1, second.Receipt.Number);
        Assert.Equal(1, await Use<SchoolDbContext, int>(db => db.DocumentAssets.CountAsync()));
        var statement = await Statement(student);
        Assert.Equal(0m, statement.TotalBalance);
        Assert.All(statement.Charges, x => Assert.Equal("Paid", x.Status));
        Assert.Equal("charge-paid", (await Assert.ThrowsAsync<OperationException>(() =>
            Use<ChargeService>(x => x.VoidAsync(Owner, statement.Charges[0].Id, new(statement.Charges[0].Version, Reason), default)))).Code);
    }

    [Fact]
    public async Task Clearance_requires_every_cutoff_obligation_and_reacts_to_waivers_and_reversals()
    {
        var (paid, _) = await Enrolled("C-1");
        var (owing, _) = await Enrolled("C-2");
        var (waived, _) = await Enrolled("C-3");
        var (lateEntry, lateEnrollment) = await Enrolled("C-4");
        var outsider = (await Use<PeopleService, CreatedPerson>(x => x.CreateAsync(Owner, new("Sin", "Matrícula", null, null, [], "C-5", false, true), default))).Id;

        // Nothing issued yet: an empty balance is not clearance.
        Assert.Equal(ClearanceStatus.PendingFinancialSetup, (await Clearance(paid)).Status);
        await Generate(periods[0]);
        await Generate(periods[1]);
        // The school decides how a late entry is handled: here it voids the first installment, leaving the period without obligation.
        var lateCharge = (await Statement(lateEntry)).Charges[0];
        await Use<ChargeService>(x => x.VoidAsync(Owner, lateCharge.Id, new(lateCharge.Version, Reason), default));

        var paidCharges = (await Statement(paid)).Charges;
        var receipt = await Pay(Key(), (paidCharges[0].Id, 500m), (paidCharges[1].Id, 500m));
        var owingCharges = (await Statement(owing)).Charges;
        await Pay(Key(), (owingCharges[0].Id, 500m), (owingCharges[1].Id, 499m));
        foreach (var charge in (await Statement(waived)).Charges)
            await Use<ChargeService>(x => x.AdjustAsync(Owner, charge.Id, new(charge.Version, AdjustmentKinds.Waiver, 500m, Reason), default));

        Assert.Equal(ClearanceStatus.Clear, (await Clearance(paid)).Status);
        var blocked = await Clearance(owing);
        Assert.Equal(ClearanceStatus.Blocked, blocked.Status);
        Assert.Equal(1m, Assert.Single(blocked.Pending).Balance);
        Assert.Equal(ClearanceStatus.Clear, (await Clearance(waived)).Status);
        var late = await Clearance(lateEntry);
        Assert.Equal(ClearanceStatus.PendingFinancialSetup, late.Status);
        Assert.Equal(["Cuota 1"], late.Missing);
        Assert.Equal(ClearanceStatus.NotEnrolled, (await Clearance(outsider)).Status);

        var report = await Use<ClearanceService, ClearanceReport>(x => x.ReportAsync(cutoff, null, null, default));
        Assert.Equal((2, 1, 1), (report.Clear, report.Blocked, report.PendingSetup));

        var version = (await Statement(paid)).Payments.Single().Version;
        await Use<PaymentService>(x => x.ReverseAsync(Owner, receipt.PaymentId, new(version, Reason), default));
        Assert.Equal(ClearanceStatus.Blocked, (await Clearance(paid)).Status);
        Assert.True((await Use<PaymentService, ReceiptView>(x => x.ReceiptAsync(receipt.PaymentId, default))).Reversed);
        _ = lateEnrollment;
    }

    [Fact]
    public async Task Concurrent_payments_cannot_overpay_the_same_obligation()
    {
        var (student, _) = await Enrolled("R-1");
        await Generate(periods[0]);
        var charge = (await Statement(student)).Charges.Single();
        async Task<bool> Attempt()
        {
            try { await Pay(Key(), (charge.Id, 500m)); return true; }
            catch (OperationException error) when (error.Code == "allocation-exceeds-balance") { return false; }
        }
        Assert.Single(await Task.WhenAll(Attempt(), Attempt(), Attempt()), x => x);
        Assert.Equal(0m, (await Statement(student)).TotalBalance);
    }

    [Fact]
    public async Task Finance_permissions_and_guardian_faculty_are_enforced()
    {
        var (student, _) = await Enrolled("G-1", "Sofía");
        await Generate(periods[0]);
        var guardian = (await Use<PeopleService, CreatedPerson>(x => x.CreateAsync(Owner, new("Laura", "Encargada", null, null, [], null, true, false), default))).Id;
        var relationship = await Use<PeopleService, Guid>(x => x.CreateRelationshipAsync(Owner, new(student, guardian, "Mother", null, [], Reason), default));
        var guardianUser = await Account("synthetic-guardian");
        await Use<PeopleService>(async x => await x.LinkAccountAsync(Owner, guardian, new((await x.GetAsync(guardian, default)).Version, guardianUser, Reason), default));
        var studentUser = await Account("synthetic-student");
        await Use<PeopleService>(async x => await x.LinkAccountAsync(Owner, student, new((await x.GetAsync(student, default)).Version, studentUser, Reason), default));
        await Account("synthetic-cashier", "cashier");
        var charge = (await Statement(student)).Charges.Single();

        using var cashier = await SignIn("synthetic-cashier");
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(cashier, "/api/v1/finance/charges/preview",
            new GenerationRequest(year, tuition, periods[1], 1, 500m, null, null, null, null, null))).StatusCode);
        var paid = await Post(cashier, "/api/v1/finance/payments", new PaymentRequest(Key(), "Laura Encargada", "Transfer", "REF-1",
            Start.AddDays(3), null, 500m, [new(charge.Id, 500m)]));
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        var receipt = await paid.Content.ReadFromJsonAsync<JsonElement>();
        var paymentId = receipt.GetProperty("paymentId").GetGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(cashier, $"/api/v1/finance/payments/{paymentId}/reverse",
            new ReverseRequest("AAAAAAAAAAA=", Reason))).StatusCode);
        var sha = receipt.GetProperty("receipt").GetProperty("logoSha256").GetString();
        Assert.Equal(HttpStatusCode.OK, (await cashier.GetAsync($"/api/v1/finance/assets/{sha}")).StatusCode);

        using var guardianBrowser = await SignIn("synthetic-guardian");
        Assert.Equal(HttpStatusCode.NotFound, (await guardianBrowser.GetAsync($"/api/v1/me/students/{student}/statement")).StatusCode);
        var version = (await Use<PeopleService, PersonDetail>(x => x.GetAsync(student, default))).Guardians.Single().Version;
        await Use<PeopleService>(x => x.UpdateRelationshipAsync(Owner, relationship, new(version, "Mother", [Faculties.Finance], Reason), default));
        var statement = await guardianBrowser.GetFromJsonAsync<JsonElement>($"/api/v1/me/students/{student}/statement");
        Assert.Equal(0m, statement.GetProperty("totalBalance").GetDecimal());
        Assert.Equal(HttpStatusCode.OK, (await guardianBrowser.GetAsync($"/api/v1/me/students/{student}/receipts/{paymentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await guardianBrowser.GetAsync($"/api/v1/finance/students/{student}/statement")).StatusCode);

        using var studentBrowser = await SignIn("synthetic-student");
        Assert.Equal(HttpStatusCode.NotFound, (await studentBrowser.GetAsync($"/api/v1/me/students/{student}/statement")).StatusCode);
    }
}
