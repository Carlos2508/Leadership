using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.LegacyImport;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Tests;

public sealed class MigrationAssistantSqlTests : SqlTestBase
{
    private readonly string storage = Path.Combine(Path.GetTempPath(), "SchoolPlatform_Assistant_" + Guid.NewGuid().ToString("N"));
    private readonly SyntheticSource source = new();
    private readonly SyntheticBackup backup = new();
    private Guid year;
    private Guid grade;
    protected override Dictionary<string, string?> ExtraConfiguration => new()
    { ["MigrationAssistant:Enabled"] = "true", ["MigrationAssistant:StorageRoot"] = storage };
    protected override void ConfigureServices(IServiceCollection services)
    { services.AddSingleton<IAssistantSource>(source); services.AddSingleton<IAssistantBackup>(backup); }
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        year = await Use<AcademicService, Guid>(s => s.CreateYearAsync(Owner, new("2027", new(2027,1,11), new(2027,10,29)), default));
        await Use<AcademicService>(async s => await s.SetYearStatusAsync(Owner, year, new((await s.YearAsync(year, default)).Year.Version, YearStatus.EnrollmentOpen, Reason), default));
        grade = await Use<AcademicService, Guid>(s => s.SaveGradeAsync(Owner, null, new("P5", "Quinto sintético", "Primaria", 5), default));
    }
    public override async Task DisposeAsync()
    {
        await base.DisposeAsync();
        var path = Path.GetFullPath(storage);
        if (path.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) &&
            System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(path), "^SchoolPlatform_Assistant_[a-f0-9]{32}$") && Directory.Exists(path)) Directory.Delete(path, true);
    }

    private async Task<AssistantReview> Review(HttpClient browser)
    {
        var response = await Post(browser, "/api/v1/migration-assistant/read", new AssistantReadRequest("42", year, "two-surnames", null));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var review = (await response.Content.ReadFromJsonAsync<AssistantReview>())!;
        return review with { Confirmed = true, Students = review.Students.Select(s => s with { GradeId = grade, BirthDate = null }).ToArray() };
    }

    [Fact]
    public async Task Authenticated_operator_reviews_plans_imports_and_reconciles_without_a_password_in_the_body()
    {
        using var browser = await SignIn(OwnerName);
        var catalog = await browser.GetFromJsonAsync<AssistantCatalog>("/api/v1/migration-assistant/catalog");
        Assert.False(catalog!.NeedsPreparation);
        Assert.Contains(catalog.Years, y => y.Id == year);
        var sourceResponse = await Post(browser, "/api/v1/migration-assistant/source", new AssistantInspectRequest(null));
        Assert.Equal(HttpStatusCode.OK, sourceResponse.StatusCode);
        var review = await Review(browser);
        Assert.Equal("Alumna Uno", review.Students[0].GivenNames);
        Assert.Equal("Ejemplo Sintético", review.Students[0].FamilyNames);
        Assert.Equal("000001", review.Students[0].Code);
        Assert.Equal(0, await Use<SchoolDbContext,int>(d => d.Persons.CountAsync()));
        var response = await Post(browser, "/api/v1/migration-assistant/plan", new AssistantPlanRequest(review));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var plan = (await response.Content.ReadFromJsonAsync<RosterReport>())!;
        Assert.Equal(0, backup.Calls);
        var applied = await Post(browser, "/api/v1/migration-assistant/apply", new AssistantApplyRequest(review.RunId, plan.PlanSha256));
        Assert.Equal(HttpStatusCode.OK, applied.StatusCode);
        var result = (await applied.Content.ReadFromJsonAsync<AssistantResult>())!;
        Assert.True(result.BackupVerified);
        Assert.Equal(1, backup.Calls);
        Assert.Equal((3,2,4,5), (result.Report.Students,result.Report.Guardians,result.Report.Relationships,result.Report.Accounts));
        Assert.Empty(result.Report.Observations);
        var again = await Post(browser, "/api/v1/migration-assistant/apply", new AssistantApplyRequest(review.RunId, plan.PlanSha256));
        Assert.True((await again.Content.ReadFromJsonAsync<AssistantResult>())!.Report.AlreadyApplied);
        Assert.Equal(1, backup.Calls);
        Assert.Equal(5, await Use<SchoolDbContext,int>(d => d.Persons.CountAsync()));
        var recent = await browser.GetFromJsonAsync<AssistantReview[]>("/api/v1/migration-assistant/recent");
        Assert.Single(recent!);
        Assert.DoesNotContain(Password, await File.ReadAllTextAsync(Path.Combine(storage, Owner.ToString("N"), review.RunId.ToString("N"),"state.json")));
    }

    [Fact]
    public async Task Backup_failure_or_changed_plan_never_starts_import_and_draft_edits_invalidate_old_plan()
    {
        using var browser = await SignIn(OwnerName);
        var review = await Review(browser);
        var plan = (await (await Post(browser,"/api/v1/migration-assistant/plan",new AssistantPlanRequest(review))).Content.ReadFromJsonAsync<RosterReport>())!;
        backup.Fail = true;
        Assert.Equal(HttpStatusCode.Conflict, (await Post(browser,"/api/v1/migration-assistant/apply",new AssistantApplyRequest(review.RunId,plan.PlanSha256))).StatusCode);
        Assert.Equal(0, await Use<SchoolDbContext,int>(d => d.Persons.CountAsync()));
        backup.Fail = false;
        var saved = await Post(browser,"/api/v1/migration-assistant/draft",new AssistantPlanRequest(review));
        Assert.False((await saved.Content.ReadFromJsonAsync<AssistantReview>())!.Confirmed);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(browser,"/api/v1/migration-assistant/apply",new AssistantApplyRequest(review.RunId,plan.PlanSha256))).StatusCode);
        Assert.Equal(1, backup.Calls); // Only the failed backup attempt; obsolete plan never runs another backup/import.
    }

    [Fact]
    public async Task Anonymous_unprivileged_csrf_and_other_operators_drafts_are_rejected()
    {
        using var anonymous = App.Browser();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/migration-assistant/catalog")).StatusCode);
        await Account("assistant-basic");
        using var basic = await SignIn("assistant-basic");
        Assert.Equal(HttpStatusCode.Forbidden, (await basic.GetAsync("/api/v1/migration-assistant/catalog")).StatusCode);
        using var owner = await SignIn(OwnerName);
        var review = await Review(owner);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/v1/migration-assistant/plan",new AssistantPlanRequest(review))).StatusCode);
        await Account("assistant-second", "administrator");
        using var second = await SignIn("assistant-second");
        Assert.Empty((await second.GetFromJsonAsync<AssistantReview[]>("/api/v1/migration-assistant/recent"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(second,"/api/v1/migration-assistant/plan",new AssistantPlanRequest(review))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(second,"/api/v1/migration-assistant/reconcile/"+review.RunId,new{})).StatusCode);
    }

    [Fact]
    public async Task Review_is_explicit_and_existing_or_unknown_sources_do_not_create_fallback_data()
    {
        using var browser = await SignIn(OwnerName);
        var review = await Review(browser);
        var rejected = await Post(browser,"/api/v1/migration-assistant/plan",new AssistantPlanRequest(review with { Confirmed=false }));
        Assert.Equal(HttpStatusCode.Conflict,rejected.StatusCode);
        Assert.Contains("review-required",await rejected.Content.ReadAsStringAsync());
        source.Fail = true;
        Assert.Equal(HttpStatusCode.BadRequest,(await Post(browser,"/api/v1/migration-assistant/read",new AssistantReadRequest("42",year,"two-surnames",null))).StatusCode);
        Assert.Single((await browser.GetFromJsonAsync<AssistantReview[]>("/api/v1/migration-assistant/recent"))!);
        Assert.Equal(0, await Use<SchoolDbContext,int>(d => d.Persons.CountAsync()));
    }

    [Fact]
    public async Task Explicitly_shared_guardian_across_old_family_records_gets_one_account()
    {
        var raw = SyntheticSource.Raw();
        raw = raw with { Students = raw.Students.Select(s => s.LegacyStudentId=="2" ? s with { FamilyLegacyId="10" } : s).ToArray(),
            Families=[..raw.Families, raw.Families[0] with { LegacyFamilyId="10" }] };
        var review = AssistantProposal.Propose(raw,year,"2027",new(2027,1,11),"two-surnames");
        review = review with { Confirmed=true, Students=review.Students.Select(s=>s with {GradeId=grade,BirthDate=null}).ToArray(),
            Families=review.Families.Select(f=>f.LegacyId=="10" ? f with {Adults=f.Adults.Select(a=>a with {SameAsKey=a.Kind=="Father"?"family-9-father":"family-9-mother"}).ToArray()} :f).ToArray() };
        var package = AssistantProposal.Package(raw,JsonSerializer.SerializeToUtf8Bytes(raw),review,
            App.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<SchoolPlatform.Host.Branding.InstitutionOptions>>().Value.SchoolId,"basic");
        Assert.Equal(5,package.People.Length);
        Assert.Equal(2,package.People.Count(p=>p.Profiles.Contains("guardian")));
        Assert.Equal(4,package.LegacyAliases.Count(a=>a.Entity=="parents"));
        Assert.Equal(4,package.Relationships.Length);
    }

    [Fact]
    public async Task Real_sql_backup_is_verified_before_an_import_can_start()
    {
        string? path=null;
        var catalog = await Use<SchoolDbContext,string>(d=>Task.FromResult(d.Database.GetDbConnection().Database));
        try
        {
            path=await Use<SchoolDbContext,string>(d=>new AssistantBackup(d).CreateVerifiedAsync(default));
            Assert.StartsWith(catalog+"_migration_",Path.GetFileName(path));
            // SQL may run in a container/on another host: verify the server's file, not the client's filesystem.
            await Use<SchoolDbContext>(async db =>
            {
                await using var connection = new Microsoft.Data.SqlClient.SqlConnection(db.Database.GetConnectionString());
                await connection.OpenAsync();
                await using var command = new Microsoft.Data.SqlClient.SqlCommand("SELECT file_exists FROM sys.dm_os_file_exists(@path)",connection);
                command.Parameters.AddWithValue("@path",path);
                Assert.Equal(1,Convert.ToInt32(await command.ExecuteScalarAsync()));
            });
        }
        finally
        {
            var fileName = path?.Replace('\\','/').Split('/').Last();
            if(path is not null && System.Text.RegularExpressions.Regex.IsMatch(fileName!,"^"+catalog+"_migration_[a-f0-9]{32}\\.bak$"))
                await Use<SchoolDbContext>(async db =>
                {
                    await using var connection = new Microsoft.Data.SqlClient.SqlConnection(db.Database.GetConnectionString());
                    await connection.OpenAsync();
                    await using var command = new Microsoft.Data.SqlClient.SqlCommand("EXEC master.dbo.xp_delete_file 0, @path",connection);
                    command.Parameters.AddWithValue("@path",path);
                    await command.ExecuteNonQueryAsync();
                });
        }
    }

    [Fact]
    public async Task Two_browser_tabs_apply_one_batch_with_one_backup()
    {
        using var browser=await SignIn(OwnerName);
        var review=await Review(browser);
        var plan=(await (await Post(browser,"/api/v1/migration-assistant/plan",new AssistantPlanRequest(review))).Content.ReadFromJsonAsync<RosterReport>())!;
        var responses=await Task.WhenAll(Post(browser,"/api/v1/migration-assistant/apply",new AssistantApplyRequest(review.RunId,plan.PlanSha256)),
            Post(browser,"/api/v1/migration-assistant/apply",new AssistantApplyRequest(review.RunId,plan.PlanSha256)));
        Assert.All(responses,r=>Assert.Equal(HttpStatusCode.OK,r.StatusCode));
        Assert.Equal(1,backup.Calls);
        Assert.Equal(5,await Use<SchoolDbContext,int>(d=>d.Persons.CountAsync()));
    }

    [Fact]
    public async Task Local_assistant_prepares_only_its_additive_schema_and_regular_host_does_not_expose_it()
    {
        var connection=await Use<SchoolDbContext,string>(async d=>
        {
            Assert.Matches("^SchoolPlatform_Test_[a-f0-9]{32}$",d.Database.GetDbConnection().Database);
            var migrator=d.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
            await migrator.MigrateAsync("20261106055933_ReceiptNumbersNoCache");
            return d.Database.GetConnectionString()!;
        });
        using var browser=await SignIn(OwnerName);
        // A later chat migration is also pending: the assistant must refuse a general installation upgrade.
        Assert.Equal(HttpStatusCode.Conflict,(await browser.GetAsync("/api/v1/migration-assistant/catalog")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await Post(browser,"/api/v1/migration-assistant/prepare",new{})).StatusCode);
        await Use<SchoolDbContext>(async d=>
        {
            Assert.Matches("^SchoolPlatform_Test_[a-f0-9]{32}$",d.Database.GetDbConnection().Database);
            await d.Database.MigrateAsync(); // Explicit installation upgrade, owned fixture only.
            // Simulate only the independent roster extension missing while all other modules are current.
            await d.Database.ExecuteSqlRawAsync("DROP TABLE [people].[RosterSourceAliases]; DROP TABLE [people].[RosterBatches]; DELETE FROM [school].[__EFMigrationsHistory] WHERE [MigrationId]='20261106202522_InitialRosterImport';");
        });
        Assert.True((await browser.GetFromJsonAsync<AssistantCatalog>("/api/v1/migration-assistant/catalog"))!.NeedsPreparation);
        Assert.Equal(HttpStatusCode.OK,(await Post(browser,"/api/v1/migration-assistant/prepare",new{})).StatusCode);
        Assert.False((await browser.GetFromJsonAsync<AssistantCatalog>("/api/v1/migration-assistant/catalog"))!.NeedsPreparation);
        await using var normal=new TestApplication(configuration:new() { ["ConnectionStrings:SchoolDatabase"]=connection });
        using var standard=normal.Browser();
        Assert.Equal(HttpStatusCode.NotFound,(await standard.GetAsync("/api/v1/migration-assistant/catalog")).StatusCode);
    }

    private sealed class SyntheticBackup : IAssistantBackup
    {
        public int Calls; public bool Fail;
        public Task<string> CreateVerifiedAsync(CancellationToken ct)
        { Calls++; if(Fail) throw new OperationException(409,"backup-unavailable","Respaldo sintético no disponible."); return Task.FromResult("synthetic-backup.bak"); }
    }
    private sealed class SyntheticSource : IAssistantSource
    {
        public bool Fail;
        public Task<AssistantInspect> InspectAsync(AssistantSourceConnection? connection,CancellationToken ct)=>Task.FromResult(new AssistantInspect([new("42","2026",3)]));
        public async Task ReadAsync(string yearId,string outputFile,AssistantSourceConnection? connection,CancellationToken ct)
        { if(Fail) throw OperationException.Invalid("Origen sintético no disponible."); await File.WriteAllBytesAsync(outputFile,JsonSerializer.SerializeToUtf8Bytes(Raw(),AssistantJson.Options),ct); }
        public static RawRosterExport Raw()=>new(1,"raw-roster-export-needs-review","sys-escolar","42","2026",DateTimeOffset.UtcNow,
            [new("1","Alumna Uno Ejemplo Sintético",null,"000001",null,null,null,"9","4",null),new("2","Alumno Dos Ejemplo Sintético",null,"000002",null,null,null,"9","4",null),new("3","Alumna Tres Caso Sintético",null,"000003",null,null,null,"404","4",null)],
            [new("9","42","Adulto Uno Ejemplo Sintético","Adulto Uno Ejemplo Sintético","Adulta Dos Caso Sintético",null,null)],
            [new("4","Cuarto sintético",4)]);
    }
}
