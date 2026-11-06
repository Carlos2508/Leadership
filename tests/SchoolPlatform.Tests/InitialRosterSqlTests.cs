using System.Net;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Branding;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.LegacyImport;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Tests;

public sealed class InitialRosterSqlTests : SqlTestBase
{
    private Guid year;
    private Guid grade;
    private static readonly DateOnly Start = new(2027, 1, 11);
    private readonly FailRosterSave failure = new();

    protected override void ConfigureServices(IServiceCollection services) =>
        services.AddDbContext<SchoolDbContext>(o => o.AddInterceptors(failure));

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        year = await Use<AcademicService, Guid>(s => s.CreateYearAsync(Owner, new("2027", Start, new(2027, 10, 29)), default));
        await Use<AcademicService>(async s => await s.SetYearStatusAsync(Owner, year,
            new((await s.YearAsync(year, default)).Year.Version, YearStatus.EnrollmentOpen, Reason), default));
        grade = await Use<AcademicService, Guid>(s => s.SaveGradeAsync(Owner, null, new("SYN-5", "Quinto sintético", "Primaria", 5), default));
    }

    private RosterFile File()
    {
        var school = App.Services.GetRequiredService<IOptions<InstitutionOptions>>().Value.SchoolId;
        var people = new[]
        {
            new RosterPerson("student-1", "Alumna Uno", "Ejemplo Sintético", null, "SYN-001", ["student"], [new("Email", "uno@example.test")], "e-syn-001", false),
            new RosterPerson("student-2", "Alumno Dos", "Ejemplo Sintético", null, "SYN-002", ["student"], [], "e-syn-002", false),
            new RosterPerson("student-3", "Alumna Tres", "Caso Sintético", null, "SYN-003", ["student"], [], "e-syn-003", false),
            new RosterPerson("guardian-1", "Adulto Uno", "Ejemplo Sintético", null, null, ["guardian"], [], "p-syn-001", false),
            new RosterPerson("guardian-2", "Adulta Dos", "Caso Sintético", null, null, ["guardian"], [], "p-syn-002", false)
        };
        return new(2, new(Guid.NewGuid(), school, "synthetic-legacy", "2026", "2026", DateTimeOffset.UtcNow,
            new string('a', 64), "initial-students-guardians-enrollments-pending-accounts", year, "2027"),
            [new("s1", "p1", "include", "student-1", Reason), new("s2", "p1", "include", "student-2", Reason),
                new("s3", null, "include", "student-3", Reason), new("s4", null, "exclude", null, Reason)],
            [new("p1", "include", ["guardian-1", "guardian-2"], Reason)], people,
            [new("student", "s1", "person", "student-1"), new("student", "s2", "person", "student-2"), new("student", "s3", "person", "student-3"),
                new("parents", "p1", "adult-1", "guardian-1"), new("parents", "p1", "adult-2", "guardian-2")],
            [new("student-1", "guardian-1", "Father", Start, [], true), new("student-1", "guardian-2", "Mother", Start, [], true),
                new("student-2", "guardian-1", "Father", Start, [], true)],
            [new("student-1", year, grade, null, Start, true), new("student-2", year, grade, null, Start, true), new("student-3", year, grade, null, Start, true)],
            new("pending-without-secrets", "basic", true, false, null, false, false));
    }

    private static byte[] Bytes(RosterFile file) => JsonSerializer.SerializeToUtf8Bytes(file, RosterJson.Options);
    private Task<RosterReport> Run(RosterFile file, string mode = "plan", string? plan = null, RosterOperator? actor = null) =>
        Raw(Bytes(file), mode, plan, actor);
    private Task<RosterReport> Raw(byte[] content, string mode = "plan", string? plan = null, RosterOperator? actor = null) =>
        Use<InitialRosterImporter, RosterReport>(s => s.RunAsync(content, mode, plan, actor ?? new(OwnerName, Password)));
    private async Task<RosterReport> Apply(RosterFile f)
    {
        var plan = await Run(f);
        return await Run(f, "apply", plan.PlanSha256);
    }
    private async Task Fails(RosterFile f, string code, string mode = "plan", string? plan = null, RosterOperator? actor = null) =>
        Assert.Equal(code, (await Assert.ThrowsAsync<RosterImportException>(() => Run(f, mode, plan, actor))).Code);

    [Fact]
    public async Task Plan_is_read_only_apply_imports_year_families_and_pending_accounts_and_reconciles()
    {
        var f = File();
        var before = await Use<SchoolDbContext, int>(d => d.AuditEvents.CountAsync());
        var plan = await Run(f);
        Assert.False(plan.Applied);
        Assert.Equal((3, 2, 3, 5, 1), (plan.Students, plan.Guardians, plan.Relationships, plan.Accounts, plan.Excluded));
        Assert.Equal(0, await Use<SchoolDbContext, int>(d => d.Persons.CountAsync()));
        Assert.Equal(1, await Use<IdentityDbContext, int>(d => d.Users.CountAsync()));
        Assert.Equal(before, await Use<SchoolDbContext, int>(d => d.AuditEvents.CountAsync()));
        Assert.Equal(plan.PlanSha256, (await Run(f)).PlanSha256);
        var applied = await Run(f, "apply", plan.PlanSha256);
        Assert.True(applied.Applied);
        Assert.Equal(5, applied.Items.Length);
        Assert.Empty((await Run(f, "reconcile")).Observations);
        await Use<SchoolDbContext>(async d =>
        {
            Assert.Equal(3, await d.Students.CountAsync());
            Assert.Equal(2, await d.Guardians.CountAsync());
            Assert.Equal(3, await d.Relationships.CountAsync());
            Assert.Empty(await d.RelationshipFaculties.ToArrayAsync());
            Assert.Equal(3, await d.Enrollments.CountAsync(e => e.AcademicYearId == year && e.EnrolledOn == Start));
            Assert.Equal(3, await d.Placements.CountAsync(e => e.GradeLevelId == grade && e.SectionId == null));
            Assert.Equal(5, await d.AccountLinks.CountAsync());
            Assert.Equal(5, await d.RosterSourceAliases.CountAsync());
            Assert.Equal(0, await d.GradePublications.CountAsync());
            Assert.Equal(0, await d.Charges.CountAsync());
            Assert.Equal(0, await d.Payments.CountAsync());
            Assert.Equal(0, await d.Assignments.CountAsync());
            Assert.Equal(0, await d.AttendanceRecords.CountAsync());
            Assert.Equal(0, await d.StudentCards.CountAsync());
            Assert.Equal(0, await d.GateDevices.CountAsync());
        });
        await Use<IdentityDbContext>(async d =>
        {
            Assert.Equal(5, await d.Users.CountAsync(u => u.Id != Owner && !u.IsActive && u.PasswordHash == null && !u.TwoFactorEnabled));
            Assert.Empty(await d.AccessGrants.ToArrayAsync());
            Assert.Equal(0, await d.AccountPermissions.CountAsync(p => p.UserId != Owner));
            Assert.True(await d.Users.AnyAsync(u => u.Id == Owner && u.IsActive));
        });
        var audit = await Use<SchoolDbContext, string[]>(d => d.AuditEvents.Select(e => e.Detail + e.Reason).ToArrayAsync());
        Assert.DoesNotContain(audit, s => s.Contains("Alumna") || s.Contains("example.test") || s.Contains(Password));
        var again = await Run(f, "apply", plan.PlanSha256);
        Assert.True(again.AlreadyApplied);
        Assert.Equal(applied.Items, again.Items);
        Assert.Equal(5, await Use<SchoolDbContext, int>(d => d.Persons.CountAsync()));
    }

    [Fact]
    public async Task All_invalid_rows_block_the_entire_batch_instead_of_partially_importing()
    {
        var f = File();
        var cases = new (RosterFile File, string Code)[]
        {
            (f with { FormatVersion = 3 }, "unsupported-format-or-scope"),
            (f with { Manifest = f.Manifest with { SchoolId = Guid.NewGuid() } }, "wrong-installation"),
            (f with { Manifest = f.Manifest with { SourceSha256 = "bad" } }, "invalid-manifest"),
            (f with { People = [f.People[0], .. f.People] }, "duplicate-person-or-username"),
            (f with { People = [f.People[0] with { GivenNames = "" }, .. f.People.Skip(1)] }, "names-review"),
            (f with { People = [f.People[0] with { BirthDate = "2027-99-99" }, .. f.People.Skip(1)] }, "birth-review"),
            (f with { People = [f.People[0] with { BirthDate = "2999-01-01" }, .. f.People.Skip(1)] }, "birth-review"),
            (f with { People = [f.People[0] with { StudentCode = "bad code" }, .. f.People.Skip(1)] }, "code-review"),
            (f with { People = [f.People[0] with { Contacts = [new("Email", "wrong")] }, .. f.People.Skip(1)] }, "contact-review"),
            (f with { People = [f.People[0] with { Contacts = [new("Phone", "123")] }, .. f.People.Skip(1)] }, "contact-review"),
            (f with { People = [f.People[0] with { Contacts = [new("Email", "a@example.test"), new("Email", "b@example.test")] }, .. f.People.Skip(1)] }, "contact-review"),
            (f with { People = [f.People[0] with { Profiles = ["staff"] }, .. f.People.Skip(1)] }, "invalid-profiles"),
            (f with { Candidates = [f.Candidates[0] with { Decision = "pending" }, .. f.Candidates.Skip(1)] }, "unresolved-candidate"),
            (f with { Candidates = [f.Candidates[0], .. f.Candidates] }, "duplicate-candidate"),
            (f with { LegacyAliases = [.. f.LegacyAliases.Skip(1)] }, "candidate-alias-required"),
            (f with { LegacyAliases = [.. f.LegacyAliases, f.LegacyAliases[0]] }, "duplicate-alias"),
            (f with { Families = [] }, "family-decision-required"),
            (f with { Families = [f.Families[0] with { Decision = "pending" }] }, "family-review"),
            (f with { Relationships = [f.Relationships[0] with { Faculties = ["AcademicRecords"] }, .. f.Relationships.Skip(1)] }, "relationship-review"),
            (f with { Relationships = [f.Relationships[0] with { Reviewed = false }, .. f.Relationships.Skip(1)] }, "relationship-review"),
            (f with { Relationships = [.. f.Relationships, f.Relationships[0]] }, "duplicate-relationship"),
            (f with { Enrollments = [.. f.Enrollments.Skip(1)] }, "enrollment-count-mismatch"),
            (f with { Enrollments = [f.Enrollments[0] with { AcademicYearId = Guid.NewGuid() }, .. f.Enrollments.Skip(1)] }, "enrollment-review"),
            (f with { AccountPreparation = f.AccountPreparation with { IsActive = true } }, "unsafe-account-preparation"),
            (f with { AccountPreparation = f.AccountPreparation with { IssueActivationCodes = true } }, "unsafe-account-preparation"),
            (f with { AccountPreparation = f.AccountPreparation with { PasswordHash = "forbidden" } }, "unsafe-account-preparation")
        };
        foreach (var invalid in cases) await Fails(invalid.File, invalid.Code);
        Assert.Equal(0, await Use<SchoolDbContext, int>(d => d.Persons.CountAsync()));
        Assert.Equal(1, await Use<IdentityDbContext, int>(d => d.Users.CountAsync()));
    }

    [Fact]
    public async Task Strict_json_rejects_unknown_missing_null_and_duplicate_properties_and_bad_modes()
    {
        var json = Encoding.UTF8.GetString(Bytes(File()));
        foreach (var text in new[] { json.Replace("\"formatVersion\":2", "\"formatVersion\":2,\"grades\":[]"),
            json.Replace("\"formatVersion\":2,", ""), json.Replace("\"formatVersion\":2", "\"formatVersion\":2,\"formatVersion\":2"),
            json.Replace("\"families\":[", "\"families\":[null,"), "{}", "null", "[]" })
            await Assert.ThrowsAsync<RosterImportException>(() => Raw(Encoding.UTF8.GetBytes(text)));
        await Fails(File(), "invalid-mode", "aplly");
        var bomPlan = await Raw(new byte[] { 0xef, 0xbb, 0xbf }.Concat(Bytes(File())).ToArray());
        Assert.Equal(3, bomPlan.Students);
    }

    [Fact]
    public async Task Destination_rejects_wrong_year_dates_grade_section_and_privileged_roles()
    {
        var f = File();
        await Fails(f with { Manifest = f.Manifest with { TargetYearLabel = "2028" } }, "year-not-open-or-mismatch");
        await Fails(f with { Enrollments = [f.Enrollments[0] with { EnrolledOn = Start.AddDays(-1) }, .. f.Enrollments.Skip(1)] }, "invalid-enrollment-date-or-grade");
        await Fails(f with { Enrollments = [f.Enrollments[0] with { GradeLevelId = Guid.NewGuid() }, .. f.Enrollments.Skip(1)] }, "invalid-enrollment-date-or-grade");
        await Fails(f with { Enrollments = [f.Enrollments[0] with { SectionId = Guid.NewGuid() }, .. f.Enrollments.Skip(1)] }, "section-year-grade-mismatch");
        await Fails(f with { AccountPreparation = f.AccountPreparation with { RoleKey = "administrator" } }, "unsafe-account-role");
        await Use<AcademicService, Guid>(s => s.AddPeriodAsync(Owner, year, new(null, Start, Start.AddDays(60)), default));
        await Use<AcademicService>(async s => await s.SetYearStatusAsync(Owner, year,
            new((await s.YearAsync(year, default)).Year.Version, YearStatus.Closed, Reason), default));
        await Fails(f, "year-not-open-or-mismatch");
    }

    [Fact]
    public async Task Operator_password_permissions_and_required_mfa_are_checked()
    {
        var f = File();
        await Fails(f, "invalid-operator", actor: new(OwnerName, "wrong"));
        await Fails(f, "invalid-operator", actor: new("does-not-exist", Password));
        await Account("synthetic-unprivileged");
        var denied = await Assert.ThrowsAsync<OperationException>(() => Run(f, actor: new("synthetic-unprivileged", Password)));
        Assert.Equal(403, denied.Status);
        App.Services.GetRequiredService<IOptions<AccountAccessOptions>>().Value.RequireMfaForPermissions = [Permissions.ManageAccounts];
        await Fails(f, "operator-mfa-required");
        Assert.Equal(0, await Use<SchoolDbContext, int>(d => d.RosterBatches.CountAsync()));
    }

    [Fact]
    public async Task Hash_changes_and_changed_destination_require_a_new_reviewed_plan()
    {
        var f = File();
        var p = await Run(f);
        await Fails(f, "plan-changed", "apply", new string('0', 64));
        await Fails(f with { Candidates = [f.Candidates[0] with { Reason = Reason + " revisado" }, .. f.Candidates.Skip(1)] }, "plan-changed", "apply", p.PlanSha256);
        await Use<SchoolDbContext>(async d => { var g = await d.Grades.SingleAsync(); g.Stage = "Cambio sintético"; await d.SaveChangesAsync(); });
        await Fails(f, "plan-changed", "apply", p.PlanSha256);
        var applied = await Apply(f);
        await Fails(f with { People = [f.People[0] with { GivenNames = "Otro" }, .. f.People.Skip(1)] }, "batch-content-changed", "apply", applied.PlanSha256);
    }

    [Fact]
    public async Task Failure_after_identity_inserts_rolls_back_everything_and_same_scope_can_retry()
    {
        var f = File();
        await using var scope = App.Services.CreateAsyncScope();
        var importer = scope.ServiceProvider.GetRequiredService<InitialRosterImporter>();
        var plan = await importer.RunAsync(Bytes(f), "plan", null, new(OwnerName, Password));
        failure.Enabled = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => importer.RunAsync(Bytes(f), "apply", plan.PlanSha256, new(OwnerName, Password)));
        Assert.Equal(1, await Use<IdentityDbContext, int>(d => d.Users.CountAsync()));
        Assert.Equal(0, await Use<SchoolDbContext, int>(d => d.Persons.CountAsync()));
        Assert.Equal(0, await Use<SchoolDbContext, int>(d => d.RosterBatches.CountAsync()));
        // A retry on the same service scope must use clean contexts after the rollback.
        var replanned = await importer.RunAsync(Bytes(f), "plan", null, new(OwnerName, Password));
        Assert.True((await importer.RunAsync(Bytes(f), "apply", replanned.PlanSha256, new(OwnerName, Password))).Applied);
    }

    [Fact]
    public async Task Concurrent_applications_create_one_batch_only()
    {
        var f = File();
        var p = await Run(f);
        var results = await Task.WhenAll(Run(f, "apply", p.PlanSha256), Run(f, "apply", p.PlanSha256));
        Assert.Single(results, r => r.AlreadyApplied);
        Assert.Equal(5, await Use<SchoolDbContext, int>(d => d.Persons.CountAsync()));
        Assert.Equal(1, await Use<SchoolDbContext, int>(d => d.RosterBatches.CountAsync()));
    }

    [Fact]
    public async Task Pending_logins_fail_and_individual_activation_and_retries_preserve_the_enabled_account()
    {
        var f = File();
        var applied = await Apply(f);
        var pending = await Use<SignInFlow, SignInOutcome>(s => s.PasswordAsync("e-syn-001", Password, default));
        Assert.Equal(SignInOutcome.Failed, pending);
        var item = applied.Items.Single(i => i.Key == "student-1");
        var version = await Use<IdentityDbContext, string>(d => d.Users.Where(u => u.Id == item.UserId).Select(u => u.ConcurrencyStamp!).SingleAsync());
        var ticket = await Use<AccountAccessService, IssueCodeResult>(s => s.IssueCodeAsync(Owner, item.UserId, new(version, AccessPurpose.Activation, Reason, true)));
        await Use<AccountAccessService>(s => s.RedeemAsync(ticket.Code, Password));
        var browser = await SignIn("e-syn-001");
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/v1/me")).StatusCode);
        var other = applied.Items.Single(i => i.Key == "student-2").PersonId;
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync($"/api/v1/me/students/{other}")).StatusCode);
        var again = await Run(f, "apply", applied.PlanSha256);
        Assert.Contains("student-1:activation-started", again.Observations);
        Assert.True(await Use<IdentityDbContext, bool>(d => d.Users.AnyAsync(u => u.Id == item.UserId && u.IsActive && u.PasswordHash != null)));
        Assert.Equal(4, await Use<IdentityDbContext, int>(d => d.Users.CountAsync(u => u.Id != Owner && !u.IsActive && u.PasswordHash == null)));
    }

    [Fact]
    public async Task Existing_codes_aliases_and_unreviewed_name_duplicates_are_never_merged()
    {
        var f = File();
        await Apply(f);
        await Fails(f with { Manifest = f.Manifest with { BatchId = Guid.NewGuid() } }, "username-collision");
        var another = f with { Manifest = f.Manifest with { BatchId = Guid.NewGuid() },
            People = f.People.Select(p => p with { PendingUsername = p.PendingUsername + "-new" }).ToArray() };
        await Fails(another, "code-collision");
        another = another with { People = another.People.Select(p => p with { StudentCode = p.StudentCode is null ? null : p.StudentCode + "-NEW" }).ToArray() };
        await Fails(another, "possible-duplicate-review");
        another = another with { People = another.People.Select(p => p with { DistinctPersonConfirmed = true }).ToArray() };
        await Fails(another, "alias-already-imported");
        Assert.Equal(5, await Use<SchoolDbContext, int>(d => d.Persons.CountAsync()));
    }

    [Fact]
    public async Task Capacity_counts_the_entire_batch_and_competing_web_enrollment_cannot_overbook()
    {
        var section = await Use<AcademicService, Guid>(s => s.SaveSectionAsync(Owner, year, null, new(grade, "A", null, 1), default));
        var f = File();
        await Fails(f with { Enrollments = f.Enrollments.Select(e => e with { SectionId = section }).ToArray() }, "section-full");
        var person = await Use<PeopleService, CreatedPerson>(s => s.CreateAsync(Owner, new("Otro", "Estudiante sintético", null, null, [], "WEB-SYN", false, true), default));
        f = f with { People = [f.People[0]], Candidates = [f.Candidates[0] with { FamilyLegacyId = null }], Families = [],
            Relationships = [], LegacyAliases = [f.LegacyAliases[0]], Enrollments = [f.Enrollments[0] with { SectionId = section }] };
        var plan = await Run(f);
        async Task<bool> Import()
        {
            try { await Run(f, "apply", plan.PlanSha256); return true; }
            catch (RosterImportException e) when (e.Code is "section-full" or "plan-changed") { return false; }
        }
        async Task<bool> Web()
        {
            try { await Use<EnrollmentService, Guid>(s => s.EnrollAsync(Owner, new(person.Id, year, grade, section, Start, Reason), default)); return true; }
            catch (OperationException e) when (e.Code == "section-full") { return false; }
        }
        var results = await Task.WhenAll(Import(), Web());
        Assert.Single(results, x => x);
        Assert.Equal(1, await Use<SchoolDbContext, int>(d => d.Placements.CountAsync(p => p.SectionId == section && p.EndsOn == null)));
    }

    [Fact]
    public async Task Confirmed_missing_family_and_combined_student_guardian_profile_do_not_create_extra_accounts()
    {
        var f = File();
        f = f with
        {
            Candidates = f.Candidates.Select(c => c.PersonKey == "student-3" ? c with { FamilyLegacyId = "p404" } : c).ToArray(),
            Families = [f.Families[0] with { PersonKeys = ["student-3", "guardian-2"] }, new("p404", "exclude", [], Reason)],
            People = f.People.Where(p => p.Key != "guardian-1").Select(p => p.Key == "student-3" ? p with { Profiles = ["student", "guardian"] } : p).ToArray(),
            LegacyAliases = f.LegacyAliases.Select(a => a.PersonKey == "guardian-1" ? a with { PersonKey = "student-3" } : a).ToArray(),
            Relationships = f.Relationships.Select(r => r.GuardianKey == "guardian-1" ? r with { GuardianKey = "student-3", Kind = "Sibling" } : r).ToArray()
        };
        var result = await Apply(f);
        Assert.Equal(4, result.Accounts);
        Assert.Equal(3, result.Students);
        Assert.Equal(2, result.Guardians);
        var combined = result.Items.Single(i => i.Key == "student-3");
        Assert.True(await Use<SchoolDbContext, bool>(d => d.Students.AnyAsync(s => s.PersonId == combined.PersonId)));
        Assert.True(await Use<SchoolDbContext, bool>(d => d.Guardians.AnyAsync(s => s.PersonId == combined.PersonId)));
        Assert.Equal(1, await Use<SchoolDbContext, int>(d => d.AccountLinks.CountAsync(l => l.PersonId == combined.PersonId)));
    }

    [Fact]
    public async Task Enrolled_operator_requires_a_valid_totp_for_plan_and_apply()
    {
        var f = File();
        var key = await Use<UserManager<ApplicationUser>, string>(async u =>
        {
            var owner = (await u.FindByIdAsync(Owner.ToString()))!;
            Assert.True((await u.ResetAuthenticatorKeyAsync(owner)).Succeeded);
            Assert.True((await u.SetTwoFactorEnabledAsync(owner, true)).Succeeded);
            return (await u.GetAuthenticatorKeyAsync(owner))!;
        });
        await Fails(f, "operator-mfa-required");
        await Fails(f, "operator-mfa-required", actor: new(OwnerName, Password, "bad-code"));
        var p = await Run(f, actor: new(OwnerName, Password, MfaSqlTests.Totp(key)));
        var result = await Run(f, "apply", p.PlanSha256, new(OwnerName, Password, MfaSqlTests.Totp(key)));
        Assert.True(result.Applied);
    }

    [Fact]
    public async Task Roster_of_150_students_is_imported_without_creating_any_other_records()
    {
        var f = File();
        var people = Enumerable.Range(1, 150).Select(i => new RosterPerson($"student-{i}", $"Alumno {i}", "Carga Sintética", null,
            $"BULK-{i:D4}", ["student"], [], $"bulk-{i:D4}", false)).ToArray();
        f = f with { People = people, Families = [], Relationships = [],
            Candidates = people.Select((p,i) => new RosterCandidate($"s{i}", null, "include", p.Key, Reason)).ToArray(),
            LegacyAliases = people.Select((p,i) => new RosterAlias("student", $"s{i}", "person", p.Key)).ToArray(),
            Enrollments = people.Select(p => new RosterEnrollment(p.Key, year, grade, null, Start, true)).ToArray() };
        var result = await Apply(f);
        Assert.Equal(150, result.Accounts);
        Assert.Equal(150, result.Items.Length);
        Assert.Empty((await Run(f, "reconcile")).Observations);
    }

    [Fact]
    public async Task Actual_cli_plans_applies_reconciles_and_returns_safe_nonzero_errors()
    {
        var f = File();
        var directory = Path.Combine(Path.GetTempPath(), "SchoolPlatform_RosterCli_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var input = Path.Combine(directory, "review.json");
        try
        {
            await System.IO.File.WriteAllBytesAsync(input, Bytes(f));
            var connection = await Use<SchoolDbContext, string>(d => Task.FromResult(d.Database.GetConnectionString()!));
            var catalog = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection).InitialCatalog;
            Assert.Matches("^SchoolPlatform_Test_[a-f0-9]{32}$", catalog);
            async Task<(int ExitCode, string Output, string Error)> Cli(string mode, string? plan = null, string? expectedDatabase = null)
            {
                var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    WorkingDirectory = App.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>().ContentRootPath };
                start.ArgumentList.Add(typeof(InitialRosterImporter).Assembly.Location);
                start.ArgumentList.Add("--import-roster");
                start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
                start.Environment["ConnectionStrings__SchoolDatabase"] = connection;
                start.Environment["Logging__LogLevel__Default"] = "None";
                start.Environment["Roster__File"] = input;
                start.Environment["Roster__Actor"] = OwnerName;
                start.Environment["Roster__Mode"] = mode;
                start.Environment["Roster__PlanSha256"] = plan ?? "";
                start.Environment["Roster__Database"] = expectedDatabase ?? catalog;
                start.Environment["ROSTER_OPERATOR_PASSWORD"] = Password;
                using var process = Process.Start(start)!;
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(45));
                return (process.ExitCode, await output, await error);
            }
            var planned = await Cli("plan");
            Assert.Equal(0, planned.ExitCode);
            var plan = JsonSerializer.Deserialize<RosterReport>(planned.Output, RosterJson.Options)!;
            var applied = await Cli("apply", plan.PlanSha256);
            Assert.Equal(0, applied.ExitCode);
            Assert.True(JsonSerializer.Deserialize<RosterReport>(applied.Output, RosterJson.Options)!.Applied);
            var reconciled = await Cli("reconcile");
            Assert.Equal(0, reconciled.ExitCode);
            Assert.DoesNotContain("Alumna", applied.Output);
            Assert.DoesNotContain(Password, planned.Output + applied.Output + reconciled.Output);
            var invalid = await Cli("aplly");
            Assert.Equal(1, invalid.ExitCode);
            Assert.Contains("invalid-mode", invalid.Error);
            Assert.DoesNotContain(" at ", invalid.Error);
            var wrongDatabase = await Cli("plan", expectedDatabase: "SchoolPlatform_WrongCatalog");
            Assert.Equal(1, wrongDatabase.ExitCode);
            Assert.Contains("target-database-confirmation-required", wrongDatabase.Error);
        }
        finally { System.IO.File.Delete(input); Directory.Delete(directory); }
    }

    [Fact]
    public async Task Legacy_importer_cannot_silently_accept_v2_or_reimport_its_source()
    {
        var f = File();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Use<LegacyPeopleImporter, ImportPlan>(s => s.RunAsync(Bytes(f), false, null, null, default)));
        await Apply(f);
        var legacy = new StagingFile(new(f.Manifest.Source, f.Manifest.SchoolId, "students-guardians", DateTimeOffset.UtcNow), [], []);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(legacy, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Use<LegacyPeopleImporter, ImportPlan>(s => s.RunAsync(bytes, false, null, null, default)));
        Assert.Equal(5, await Use<SchoolDbContext, int>(d => d.Persons.CountAsync()));
    }

    private sealed class FailRosterSave : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (Enabled && eventData.Context!.ChangeTracker.Entries<RosterSourceAlias>().Any())
            {
                Enabled = false;
                throw new InvalidOperationException("Synthetic failure after IAM writes");
            }
            return ValueTask.FromResult(result);
        }
    }
}
