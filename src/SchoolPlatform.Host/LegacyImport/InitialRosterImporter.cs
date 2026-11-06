using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Branding;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.SchoolData;
using static SchoolPlatform.Host.LegacyImport.RosterValidation;

namespace SchoolPlatform.Host.LegacyImport;

public sealed class InitialRosterImporter(SchoolDbContext db, IdentityDbContext identity, UserManager<ApplicationUser> users,
    RosterAccountProvisioner accounts, SchoolAudit audit, IOptions<InstitutionOptions> institution,
    IOptions<AccountAccessOptions> access, TimeProvider time)
{
    public async Task<RosterReport> RunAsync(byte[] content, string mode, string? expectedPlanSha256,
        RosterOperator credentials, CancellationToken ct = default) =>
        await RunCoreAsync(content, mode, expectedPlanSha256, () => OperatorAsync(credentials, ct), ct);

    // Only the authenticated, resource-authorized local assistant calls this; actor ID comes from its cookie, never its body.
    internal Task<RosterReport> RunForAccountAsync(byte[] content, string mode, string? expectedPlanSha256, Guid actorId, CancellationToken ct) =>
        RunCoreAsync(content, mode, expectedPlanSha256, () => ActiveOperatorAsync(actorId, ct), ct);

    private async Task<RosterReport> RunCoreAsync(byte[] content, string mode, string? expectedPlanSha256,
        Func<Task<ApplicationUser>> authorize, CancellationToken ct)
    {
        Require(mode is "plan" or "apply" or "reconcile", "invalid-mode");
        var file = RosterJson.Read(content);
        Validate(file, institution.Value.SchoolId, DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime));
        Require(!(await db.Database.GetPendingMigrationsAsync(ct)).Any() &&
            !(await identity.Database.GetPendingMigrationsAsync(ct)).Any(), "pending-migrations");
        Require(db.Database.GetDbConnection().ConnectionString == identity.Database.GetDbConnection().ConnectionString, "different-databases");
        // The UserManager's scoped Identity context must enlist in School's physical connection and transaction.
        identity.Database.SetDbConnection(db.Database.GetDbConnection(), contextOwnsConnection: false);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await identity.Database.UseTransactionAsync(transaction.GetDbTransaction(), ct);
        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                DECLARE @result int;
                EXEC @result = sp_getapplock @Resource=N'SchoolPlatform.AccountAccess',
                    @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=30000;
                IF @result < 0 THROW 51000, 'Account operation busy', 1;
                """, ct);
            await EnrollmentWriteScope.LockAsync(db, ct);
            var actor = await authorize();
            var sha = Hash(content);
            var previous = await db.RosterBatches.AsNoTracking().SingleOrDefaultAsync(b => b.Id == file.Manifest.BatchId, ct);
            if (previous is not null)
            {
                Require(previous.FileSha256 == sha, "batch-content-changed");
                Require(mode != "apply" || string.Equals(previous.PlanSha256, expectedPlanSha256, StringComparison.OrdinalIgnoreCase), "plan-changed");
                var saved = JsonSerializer.Deserialize<RosterReport>(previous.ResultJson, RosterJson.Options)!;
                var observed = await ReconcileAsync(file, saved, ct);
                await transaction.CommitAsync(ct);
                return observed with { AlreadyApplied = true };
            }
            Require(mode != "reconcile", "batch-not-applied");
            var year = await ValidateDestinationAsync(file, ct);
            var stamp = await DestinationStampAsync(file, actor, sha, ct);
            var report = new RosterReport(file.Manifest.BatchId, year.Id, year.Label,
                db.Database.GetDbConnection().DataSource, db.Database.GetDbConnection().Database, sha, stamp,
                file.Enrollments.Length, file.People.Count(p => p.Profiles.Contains("guardian")), file.Relationships.Length,
                file.People.Length, file.Candidates.Count(c => c.Decision == "exclude"), false, false, [], []);
            if (mode == "plan") { await transaction.CommitAsync(ct); return report; }
            Require(Sha(expectedPlanSha256) && string.Equals(stamp, expectedPlanSha256, StringComparison.OrdinalIgnoreCase), "plan-changed");
            var now = time.GetUtcNow();
            var items = new Dictionary<string, RosterItemResult>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in file.People)
            {
                var userId = await accounts.CreatePendingAsync(p.PendingUsername, file.AccountPreparation.RoleKey, ct);
                var person = new Person
                {
                    Id = Guid.NewGuid(), GivenNames = Clean(p.GivenNames), FamilyNames = Clean(p.FamilyNames),
                    BirthDate = p.BirthDate is null ? null : DateOnly.ParseExact(p.BirthDate, "yyyy-MM-dd"),
                    Status = PersonStatus.Active, CreatedAt = now, UpdatedAt = now
                };
                if (p.Profiles.Contains("student")) person.Student = new() { PersonId = person.Id, StudentCode = p.StudentCode!, CreatedAt = now };
                if (p.Profiles.Contains("guardian")) person.Guardian = new() { PersonId = person.Id, CreatedAt = now };
                foreach (var c in p.Contacts) person.Contacts.Add(new()
                { Id = Guid.NewGuid(), PersonId = person.Id, Kind = c.Kind, Value = c.Value, Label = "Legado revisado", IsPrimary = true });
                db.Persons.Add(person);
                db.AccountLinks.Add(new() { Id = Guid.NewGuid(), PersonId = person.Id, UserId = userId, LinkedAt = now, LinkedBy = actor.Id });
                items.Add(p.Key, new(p.Key, person.Id, userId, null));
                audit.Record("ImportedAccountPrepared", actor.Id, "Person", person.Id);
            }
            foreach (var e in file.Enrollments)
            {
                var item = items[e.StudentKey];
                var enrollment = new Enrollment
                {
                    Id = Guid.NewGuid(), StudentId = item.PersonId, AcademicYearId = year.Id, EnrolledOn = e.EnrolledOn,
                    Status = EnrollmentStatus.Active, CreatedAt = now, UpdatedAt = now, CreatedBy = actor.Id
                };
                enrollment.Placements.Add(new() { Id = Guid.NewGuid(), EnrollmentId = enrollment.Id, GradeLevelId = e.GradeLevelId,
                    SectionId = e.SectionId, StartsOn = e.EnrolledOn, Reason = "Matrícula inicial desde padrón revisado", CreatedAt = now, CreatedBy = actor.Id });
                db.Enrollments.Add(enrollment);
                items[e.StudentKey] = item with { EnrollmentId = enrollment.Id };
            }
            foreach (var r in file.Relationships)
                db.Relationships.Add(new() { Id = Guid.NewGuid(), StudentId = items[r.StudentKey].PersonId, GuardianId = items[r.GuardianKey].PersonId,
                    Kind = r.Kind, StartsOn = r.StartsOn, CreatedAt = now, CreatedBy = actor.Id });
            report = report with { Applied = true, Items = items.Values.OrderBy(x => x.Key, StringComparer.Ordinal).ToArray() };
            db.RosterBatches.Add(new() { Id = file.Manifest.BatchId, FileSha256 = sha, PlanSha256 = stamp, Source = file.Manifest.Source,
                AcademicYearId = year.Id, ActorId = actor.Id, AppliedAt = now, ResultJson = JsonSerializer.Serialize(report, RosterJson.Options) });
            foreach (var alias in file.LegacyAliases)
                db.RosterSourceAliases.Add(new() { Source = file.Manifest.Source, Entity = alias.Entity, LegacyId = alias.LegacyId,
                    Discriminator = alias.Discriminator, PersonId = items[alias.PersonKey].PersonId, BatchId = file.Manifest.BatchId });
            audit.Record("InitialRosterImported", actor.Id, "ImportBatch", file.Manifest.BatchId,
                "Padrón inicial revisado; cuentas sin activar.", $"{report.Students}:{report.Guardians}:{report.Accounts}:{sha[..12]}");
            await db.SaveChangesAsync(ct);
            // Verify initial invariants before commit too. An unexpected result rolls back Identity AND School.
            var checkedReport = await ReconcileAsync(file, report, ct);
            Require(checkedReport.Observations.Length == 0, "initial-reconciliation-failed");
            await transaction.CommitAsync(ct);
            return report;
        }
        finally
        {
            await identity.Database.UseTransactionAsync(null, CancellationToken.None);
            // A failed transaction must not leak staged users/entities into a later operation in the same scope.
            db.ChangeTracker.Clear();
            identity.ChangeTracker.Clear();
        }
    }

    private async Task<ApplicationUser> OperatorAsync(RosterOperator c, CancellationToken ct)
    {
        var actor = await users.FindByNameAsync(c.Username);
        Require(actor is { IsActive: true, PasswordHash: not null } && !await users.IsLockedOutAsync(actor) &&
            c.Password.Length is > 0 and <= 4096 && users.PasswordHasher.VerifyHashedPassword(actor, actor.PasswordHash, c.Password) != PasswordVerificationResult.Failed, "invalid-operator");
        await audit.RequireAsync(actor!.Id, ct, Permissions.PeopleManage, Permissions.EnrollmentManage, Permissions.ManageAccounts);
        var requiresMfa = actor.TwoFactorEnabled || access.Value.RequireMfaForPermissions.Intersect(
            new[] { Permissions.PeopleManage, Permissions.EnrollmentManage, Permissions.ManageAccounts }).Any();
        if (requiresMfa)
            Require(actor.TwoFactorEnabled && !string.IsNullOrWhiteSpace(c.SecondFactorCode) &&
                await users.VerifyTwoFactorTokenAsync(actor, users.Options.Tokens.AuthenticatorTokenProvider, c.SecondFactorCode), "operator-mfa-required");
        return actor;
    }

    internal async Task<ApplicationUser> ActiveOperatorAsync(Guid id, CancellationToken ct)
    {
        var actor = await identity.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id && u.IsActive && u.PasswordHash != null, ct);
        Require(actor is not null, "invalid-operator");
        await audit.RequireAsync(id, ct, Permissions.PeopleManage, Permissions.EnrollmentManage, Permissions.ManageAccounts);
        var requiresMfa = access.Value.RequireMfaForPermissions.Intersect(new[]
            { Permissions.PeopleManage, Permissions.EnrollmentManage, Permissions.ManageAccounts }).Any();
        Require(!requiresMfa || actor.TwoFactorEnabled, "operator-mfa-required");
        return actor;
    }

    private async Task<AcademicYear> ValidateDestinationAsync(RosterFile file, CancellationToken ct)
    {
        var m = file.Manifest;
        var year = await db.Years.AsNoTracking().SingleOrDefaultAsync(y => y.Id == m.TargetYearId, ct);
        Require(year is not null && year.Label == m.TargetYearLabel && YearStatus.AcceptsEnrollment(year.Status), "year-not-open-or-mismatch");
        Require(await identity.AccessRoles.AnyAsync(r => r.Key == file.AccountPreparation.RoleKey && r.IsActive, ct) &&
            !await identity.AccessRolePermissions.AnyAsync(r => r.RoleKey == file.AccountPreparation.RoleKey, ct), "unsafe-account-role");
        var grades = await db.Grades.AsNoTracking().ToDictionaryAsync(x => x.Id, ct);
        var sections = await db.Sections.AsNoTracking().ToDictionaryAsync(x => x.Id, ct);
        foreach (var e in file.Enrollments)
        {
            Require(e.EnrolledOn >= year!.StartsOn && e.EnrolledOn <= year.EndsOn && grades.TryGetValue(e.GradeLevelId, out var grade) && grade.IsActive,
                "invalid-enrollment-date-or-grade");
            Require(e.SectionId is null || sections.TryGetValue(e.SectionId.Value, out var section) &&
                section.AcademicYearId == year.Id && section.GradeLevelId == e.GradeLevelId, "section-year-grade-mismatch");
        }
        foreach (var group in file.Enrollments.Where(e => e.SectionId is not null).GroupBy(e => e.SectionId!.Value))
        {
            var section = sections[group.Key];
            var occupied = await db.Placements.CountAsync(p => p.SectionId == section.Id && p.EndsOn == null &&
                db.Enrollments.Any(e => e.Id == p.EnrollmentId && e.Status == EnrollmentStatus.Active), ct);
            Require(section.Capacity is null || occupied + group.Count() <= section.Capacity.Value, "section-full");
        }
        var existingPeople = await db.Persons.AsNoTracking().Select(p => new { p.GivenNames, p.FamilyNames, p.BirthDate }).ToArrayAsync(ct);
        foreach (var p in file.People)
        {
            Require(await users.FindByNameAsync(p.PendingUsername) is null, "username-collision");
            if (p.StudentCode is not null) Require(!await db.Students.AnyAsync(s => s.StudentCode == p.StudentCode, ct), "code-collision");
            var birth = p.BirthDate is null ? (DateOnly?)null : DateOnly.ParseExact(p.BirthDate, "yyyy-MM-dd");
            bool Same(string g, string f, DateOnly? b) => string.Equals(Clean(g), Clean(p.GivenNames), StringComparison.InvariantCultureIgnoreCase) &&
                string.Equals(Clean(f), Clean(p.FamilyNames), StringComparison.InvariantCultureIgnoreCase) && (birth is null || b is null || b == birth);
            var possibleDuplicate = existingPeople.Any(x => Same(x.GivenNames, x.FamilyNames, x.BirthDate)) ||
                file.People.Any(x => x.Key != p.Key && Same(x.GivenNames, x.FamilyNames, x.BirthDate is null ? null : DateOnly.ParseExact(x.BirthDate, "yyyy-MM-dd")));
            Require(!possibleDuplicate || p.DistinctPersonConfirmed, "possible-duplicate-review");
        }
        foreach (var alias in file.LegacyAliases)
        {
            Require(!await db.RosterSourceAliases.AnyAsync(x => x.Source == m.Source && x.Entity == alias.Entity &&
                x.LegacyId == alias.LegacyId && x.Discriminator == alias.Discriminator, ct), "alias-already-imported");
            var oldEntity = alias.Entity == "student" ? "student" : "guardian";
            Require(!await db.LegacyIdentities.AnyAsync(x => x.Source == m.Source && x.Entity == oldEntity && x.LegacyId == alias.LegacyId, ct), "legacy-v1-already-imported");
        }
        return year!;
    }

    private async Task<string> DestinationStampAsync(RosterFile file, ApplicationUser actor, string sha, CancellationToken ct)
    {
        // Fingerprint committed rows, not @@DBTS: SQL can consume rowversions in a rolled-back transaction.
        // Order every projection explicitly; SQL's physical row order is not stable across writes/rollbacks.
        var years = await db.Years.AsNoTracking().Where(x => x.Id == file.Manifest.TargetYearId).OrderBy(x => x.Id).Select(x => new { x.Id, x.Version }).ToArrayAsync(ct);
        var grades = await db.Grades.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.Version }).ToArrayAsync(ct);
        var sections = await db.Sections.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.Version }).ToArrayAsync(ct);
        var people = await db.Persons.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.Version }).ToArrayAsync(ct);
        var codes = await db.Students.AsNoTracking().OrderBy(x => x.PersonId).Select(x => new { x.PersonId, x.StudentCode }).ToArrayAsync(ct);
        var enrollments = await db.Enrollments.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.Version }).ToArrayAsync(ct);
        var placements = await db.Placements.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.EnrollmentId, x.SectionId, x.EndsOn }).ToArrayAsync(ct);
        var roles = await identity.AccessRoles.AsNoTracking().OrderBy(x => x.Key).Select(x => new { x.Key, x.Version }).ToArrayAsync(ct);
        var userStamps = await identity.Users.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.ConcurrencyStamp, x.SecurityStamp, x.IsActive }).ToArrayAsync(ct);
        var permissions = await identity.AccountPermissions.AsNoTracking().OrderBy(x => x.UserId).ThenBy(x => x.Permission).ToArrayAsync(ct);
        var rolePermissions = await identity.AccessRolePermissions.AsNoTracking().OrderBy(x => x.RoleKey).ThenBy(x => x.Permission).ToArrayAsync(ct);
        return Hash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            sha, db.Database.GetDbConnection().DataSource, db.Database.GetDbConnection().Database,
            actor.Id, actor.SecurityStamp, years, grades, sections, people, codes, enrollments, placements, roles,
            userStamps, permissions, rolePermissions, file.Manifest.TargetYearId
        })));
    }

    private async Task<RosterReport> ReconcileAsync(RosterFile file, RosterReport report, CancellationToken ct)
    {
        var observations = new List<string>();
        if (!await db.Years.AnyAsync(y => y.Id == report.YearId && y.Label == file.Manifest.TargetYearLabel, ct)) observations.Add("year-changed");
        foreach (var item in report.Items)
        {
            var p = file.People.Single(p => p.Key == item.Key);
            var person = await db.Persons.AsNoTracking().SingleOrDefaultAsync(x => x.Id == item.PersonId, ct);
            if (person is null || person.Status != PersonStatus.Active || person.GivenNames != Clean(p.GivenNames) || person.FamilyNames != Clean(p.FamilyNames) ||
                person.BirthDate != (p.BirthDate is null ? (DateOnly?)null : DateOnly.ParseExact(p.BirthDate, "yyyy-MM-dd"))) observations.Add($"{item.Key}:person-changed");
            if (p.StudentCode is not null && !await db.Students.AnyAsync(s => s.PersonId == item.PersonId && s.StudentCode == p.StudentCode, ct)) observations.Add($"{item.Key}:student-changed");
            if (p.Profiles.Contains("guardian") && !await db.Guardians.AnyAsync(s => s.PersonId == item.PersonId, ct)) observations.Add($"{item.Key}:guardian-changed");
            if (!await db.AccountLinks.AnyAsync(l => l.PersonId == item.PersonId && l.UserId == item.UserId && l.UnlinkedAt == null, ct)) observations.Add($"{item.Key}:account-link-changed");
            var account = await identity.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == item.UserId, ct);
            if (account is null || account.UserName != p.PendingUsername) observations.Add($"{item.Key}:account-changed");
            else if (account.IsActive || account.PasswordHash is not null || await identity.AccessGrants.AnyAsync(g => g.UserId == item.UserId, ct)) observations.Add($"{item.Key}:activation-started");
            if (await identity.AccountPermissions.AnyAsync(a => a.UserId == item.UserId, ct) ||
                !await identity.AccountRoleAssignments.AnyAsync(a => a.UserId == item.UserId && a.RoleKey == file.AccountPreparation.RoleKey, ct)) observations.Add($"{item.Key}:access-changed");
            if (item.EnrollmentId is { } id)
            {
                var expected = file.Enrollments.Single(e => e.StudentKey == item.Key);
                if (!await db.Enrollments.AnyAsync(e => e.Id == id && e.StudentId == item.PersonId && e.AcademicYearId == report.YearId &&
                    e.Status == EnrollmentStatus.Active && e.EnrolledOn == expected.EnrolledOn, ct) ||
                    !await db.Placements.AnyAsync(e => e.EnrollmentId == id && e.GradeLevelId == expected.GradeLevelId &&
                        e.SectionId == expected.SectionId && e.StartsOn == expected.EnrolledOn && e.EndsOn == null, ct)) observations.Add($"{item.Key}:enrollment-changed");
            }
            var actualContacts = await db.PersonContacts.AsNoTracking().Where(c => c.PersonId == item.PersonId).Select(c => new RosterContact(c.Kind, c.Value)).ToArrayAsync(ct);
            if (!actualContacts.OrderBy(c => c.Kind).SequenceEqual(p.Contacts.OrderBy(c => c.Kind))) observations.Add($"{item.Key}:contacts-changed");
        }
        var items = report.Items.ToDictionary(x => x.Key);
        foreach (var r in file.Relationships)
        {
            var s = items[r.StudentKey].PersonId;
            var g = items[r.GuardianKey].PersonId;
            var relationship = await db.Relationships.AsNoTracking().SingleOrDefaultAsync(x => x.StudentId == s && x.GuardianId == g && x.EndedAt == null, ct);
            if (relationship is null || relationship.Kind != r.Kind || relationship.StartsOn != r.StartsOn) observations.Add($"{r.StudentKey}/{r.GuardianKey}:relationship-changed");
            else if (await db.RelationshipFaculties.AnyAsync(f => f.RelationshipId == relationship.Id && f.RevokedAt == null, ct)) observations.Add($"{r.StudentKey}/{r.GuardianKey}:faculties-granted");
        }
        foreach (var a in file.LegacyAliases)
            if (!await db.RosterSourceAliases.AnyAsync(x => x.Source == file.Manifest.Source && x.Entity == a.Entity && x.LegacyId == a.LegacyId &&
                x.Discriminator == a.Discriminator && x.PersonId == items[a.PersonKey].PersonId && x.BatchId == file.Manifest.BatchId, ct)) observations.Add("alias-changed");
        return report with { Observations = observations.Distinct().ToArray() };
    }

    private static string Hash(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));
}
