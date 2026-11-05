using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.Documents;
using SchoolPlatform.Host.Gate;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Tests;

/// <summary>Controllable clock: starts at the real time and moves only when a test advances it.</summary>
public sealed class ManualClock : TimeProvider
{
    private DateTimeOffset now = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => now;
    public void Advance(TimeSpan by) => now += by;
}

public sealed class GateSqlTests : SqlTestBase
{
    private static readonly DateOnly Start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-60);
    private readonly ManualClock clock = new();
    private readonly string storage = Path.Combine(Path.GetTempPath(), "SchoolPlatform_TestFiles_" + Guid.NewGuid().ToString("N"));
    private Guid year, grade, student, classmate, withdrawn, relationship;

    protected override Dictionary<string, string?> ExtraConfiguration => new() { ["Storage:Root"] = storage };
    protected override void ConfigureServices(IServiceCollection services) => services.AddSingleton<TimeProvider>(clock);

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        year = await Use<AcademicService, Guid>(x => x.CreateYearAsync(Owner, new("Ciclo portería", Start, Start.AddDays(300)), default));
        await Use<AcademicService>(async x => await x.SetYearStatusAsync(Owner, year, new((await x.YearAsync(year, default)).Year.Version, YearStatus.EnrollmentOpen, Reason), default));
        grade = await Use<AcademicService, Guid>(x => x.SaveGradeAsync(Owner, null, new("4P", "Cuarto primaria", "Primaria", 1), default));
        student = await Enrolled("Lara", "G-1");
        classmate = await Enrolled("Mario", "G-2");
        withdrawn = (await Use<PeopleService, CreatedPerson>(x => x.CreateAsync(Owner, new("Nico", "Portería G-3", null, null, [], "G-3", false, true), default))).Id;
        var guardian = (await Use<PeopleService, CreatedPerson>(x => x.CreateAsync(Owner, new("Paula", "Encargada", null, null, [], null, true, false), default))).Id;
        relationship = await Use<PeopleService, Guid>(x => x.CreateRelationshipAsync(Owner, new(student, guardian, "Mother", null, [Faculties.AcademicRecords], Reason), default));
        await Link(guardian, await Account("synthetic-guardian"));
    }

    public override async Task DisposeAsync()
    {
        await base.DisposeAsync();
        if (Directory.Exists(storage)) Directory.Delete(storage, recursive: true);
    }

    private async Task<Guid> Enrolled(string given, string code)
    {
        var id = (await Use<PeopleService, CreatedPerson>(x => x.CreateAsync(Owner, new(given, "Portería " + code, null, null, [], code, false, true), default))).Id;
        await Use<EnrollmentService, Guid>(x => x.EnrollAsync(Owner, new(id, year, grade, null, Start, null), default));
        return id;
    }

    private async Task Link(Guid person, Guid user) =>
        await Use<PeopleService>(async x => await x.LinkAccountAsync(Owner, person, new((await x.GetAsync(person, default)).Version, user, Reason), default));

    private async Task<string> Token(Guid id) => (await Use<CardService, CardData[]>(x => x.PrintAsync(Owner, [id], null, default)))[0].Token;

    private Task<ScanResult> Scan(string code, string mode = GateMode.Auto) =>
        Use<GateService, ScanResult>(x => x.ScanAsync(new(Owner, null, GateSource.Scanner), new(code, mode), default));

    [Fact]
    public async Task Repeated_reads_never_mark_twice_and_entry_exit_follow_the_mode()
    {
        var token = await Token(student);
        Assert.Equal(token, await Token(student));
        Assert.Equal(12, token.Length);

        var entry = await Scan(token.ToLowerInvariant());
        Assert.Equal(("Recorded", GateDirection.In), (entry.Outcome, entry.Direction));
        Assert.Equal("Lara Portería G-1", entry.Student!.Name);
        Assert.Equal("Cuarto primaria", entry.Student.Placement);

        // Scanner bounce and a second read minutes later are reported, not stored.
        Assert.Equal("Duplicate", (await Scan(token)).Outcome);
        clock.Advance(TimeSpan.FromMinutes(10));
        var again = await Scan(token);
        Assert.Equal(("Duplicate", GateDirection.In), (again.Outcome, again.Direction));
        Assert.Equal("Duplicate", (await Scan(token, GateMode.In)).Outcome);

        // Automatic mode marks the exit only after the minimum stay.
        clock.Advance(TimeSpan.FromMinutes(25));
        var exit = await Scan(token);
        Assert.Equal(("Recorded", GateDirection.Out), (exit.Outcome, exit.Direction));
        Assert.Equal("Duplicate", (await Scan(token)).Outcome);
        Assert.Equal("Duplicate", (await Scan(token, GateMode.Out)).Outcome);
        clock.Advance(TimeSpan.FromMinutes(5));
        var back = await Scan(token, GateMode.In);
        Assert.Equal(("Recorded", GateDirection.In), (back.Outcome, back.Direction));
        clock.Advance(TimeSpan.FromMinutes(1));

        // A fixed exit kiosk records an exit without a previous entry (and says so).
        var classmateToken = await Token(classmate);
        var lone = await Scan(classmateToken, GateMode.Out);
        Assert.Equal("Recorded", lone.Outcome);
        Assert.Contains("sin entrada", lone.Message);

        Assert.Equal("Unknown", (await Scan("ZZZZZZZZZZZZ")).Outcome);
        Assert.Equal("Unknown", (await Scan("x")).Outcome);
        var marks = await Use<SchoolDbContext, GateEvent[]>(db => db.GateEvents.OrderBy(x => x.OccurredAt).ToArrayAsync());
        Assert.Equal([GateDirection.In, GateDirection.Out, GateDirection.In, GateDirection.Out], marks.Select(x => x.Direction).ToArray());
        Assert.All(marks, x => Assert.Equal(Owner, x.RecordedBy));
    }

    [Fact]
    public async Task Simultaneous_reads_of_one_card_store_a_single_mark()
    {
        var token = await Token(student);
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Scan(token)));
        Assert.Equal(1, results.Count(x => x.Outcome == "Recorded"));
        Assert.Equal(5, results.Count(x => x.Outcome == "Duplicate"));
        Assert.Equal(1, await Use<SchoolDbContext, int>(db => db.GateEvents.CountAsync()));
    }

    [Fact]
    public async Task Simultaneous_card_replacements_leave_one_active_card_with_audit_for_each_replacement()
    {
        var original = await Token(student);
        var replacements = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            Use<CardService, CardView>(x => x.ReissueAsync(Owner, student, new("Reposición sintética simultánea por extravío."), default))));
        var cards = await Use<SchoolDbContext, StudentCard[]>(db => db.StudentCards.Where(x => x.StudentId == student).ToArrayAsync());
        var active = Assert.Single(cards, x => x.RevokedAt == null);
        Assert.Equal(5, cards.Length);
        Assert.Equal(4, await Use<SchoolDbContext, int>(db => db.AuditEvents.CountAsync(x => x.Action == "StudentCardReissued" && x.TargetId == student)));
        Assert.Equal("Revoked", (await Scan(original)).Outcome);
        foreach (var card in replacements.Where(x => x.Token != active.Token)) Assert.Equal("Revoked", (await Scan(card.Token)).Outcome);
        Assert.Equal("Recorded", (await Scan(active.Token)).Outcome);
    }

    [Fact]
    public async Task Reissued_cards_stop_working_and_students_without_enrollment_are_not_marked()
    {
        var old = await Token(student);
        var fresh = await Use<CardService, CardView>(x => x.ReissueAsync(Owner, student, new("Extravío reportado por la familia."), default));
        Assert.NotEqual(old, fresh.Token);
        var revoked = await Scan(old);
        Assert.Equal("Revoked", revoked.Outcome);
        Assert.Equal(student, revoked.Student!.Id);
        Assert.Equal("Recorded", (await Scan(fresh.Token)).Outcome);

        var notEnrolled = await Scan(await Token(withdrawn));
        Assert.Equal("NotEnrolled", notEnrolled.Outcome);
        Assert.Equal(1, await Use<SchoolDbContext, int>(db => db.GateEvents.CountAsync()));
        Assert.Contains(await Use<SchoolDbContext, string[]>(db => db.AuditEvents.Select(x => x.Action).ToArrayAsync()), x => x == "StudentCardReissued");
    }

    [Fact]
    public async Task Gate_permissions_manual_marks_reports_and_family_view()
    {
        var token = await Token(student);
        await Account("synthetic-gate", "gatekeeper");
        await Account("synthetic-basic");
        using var gate = await SignIn("synthetic-gate");
        using var basic = await SignIn("synthetic-basic");
        using var staff = await SignIn(OwnerName);
        using var guardian = await SignIn("synthetic-guardian");

        Assert.Equal(HttpStatusCode.Forbidden, (await Post(basic, "/api/v1/gate/scan", new { code = token, mode = "Auto" })).StatusCode);
        var scan = await Post(gate, "/api/v1/gate/scan", new { code = token, mode = "Auto" });
        Assert.Equal(HttpStatusCode.OK, scan.StatusCode);
        Assert.Equal("Recorded", (await scan.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("outcome").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await gate.GetAsync("/api/v1/gate/daily")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(gate, "/api/v1/gate/devices", new { name = "Portón" })).StatusCode);

        // Manual mark: search, reason required, audited.
        var found = await gate.GetFromJsonAsync<JsonElement[]>("/api/v1/gate/search?q=Mario");
        Assert.Equal(classmate, Assert.Single(found!).GetProperty("id").GetGuid());
        Assert.Empty((await gate.GetFromJsonAsync<JsonElement[]>("/api/v1/gate/search?q=Nico"))!);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(gate, "/api/v1/gate/manual", new { studentId = classmate, direction = "In", reason = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(gate, "/api/v1/gate/manual", new { studentId = classmate, direction = "In", reason = "Olvidó su carnet en casa." })).StatusCode);
        Assert.Contains(await Use<SchoolDbContext, string[]>(db => db.AuditEvents.Select(x => x.Action).ToArrayAsync()), x => x == "GateManualMark");
        Assert.Equal(2, (await gate.GetFromJsonAsync<JsonElement[]>("/api/v1/gate/recent"))!.Length);

        var daily = await staff.GetFromJsonAsync<JsonElement>($"/api/v1/gate/daily?gradeId={grade}");
        Assert.Equal((2, 2, 2, 0), (daily.GetProperty("expected").GetInt32(), daily.GetProperty("arrived").GetInt32(),
            daily.GetProperty("inside").GetInt32(), daily.GetProperty("absent").GetInt32()));

        // Void with reason (attendance.manage) removes the mark from reports, keeping the record.
        var markId = (await Use<SchoolDbContext, Guid>(db => db.GateEvents.Where(x => x.StudentId == classmate).Select(x => x.Id).SingleAsync()));
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(gate, $"/api/v1/gate/events/{markId}/void", new { reason = "Marca equivocada en portería." })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Post(staff, $"/api/v1/gate/events/{markId}/void", new { reason = "Marca equivocada en portería." })).StatusCode);
        daily = await staff.GetFromJsonAsync<JsonElement>($"/api/v1/gate/daily?gradeId={grade}");
        Assert.Equal(1, daily.GetProperty("absent").GetInt32());

        // Family: own student with AcademicRecords; nothing for others.
        var days = await guardian.GetFromJsonAsync<JsonElement[]>($"/api/v1/me/students/{student}/gate");
        Assert.NotNull(Assert.Single(days!).GetProperty("firstIn").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await guardian.GetAsync($"/api/v1/me/students/{classmate}/gate")).StatusCode);
        var version = (await Use<PeopleService, PersonDetail>(x => x.GetAsync(student, default))).Guardians.Single().Version;
        await Use<PeopleService>(x => x.UpdateRelationshipAsync(Owner, relationship, new(version, "Mother", [], Reason), default));
        Assert.Equal(HttpStatusCode.NotFound, (await guardian.GetAsync($"/api/v1/me/students/{student}/gate")).StatusCode);
    }

    [Fact]
    public async Task Scanner_devices_use_their_own_revocable_token()
    {
        var token = await Token(student);
        using var staff = await SignIn(OwnerName);
        var registered = await (await Post(staff, "/api/v1/gate/devices", new { name = "Teléfono portón" })).Content.ReadFromJsonAsync<JsonElement>();
        var secret = registered.GetProperty("token").GetString()!;
        Assert.StartsWith("gd_", secret);
        Assert.Contains("<svg", registered.GetProperty("pairingSvg").GetString());
        var stored = await Use<SchoolDbContext, GateDevice>(db => db.GateDevices.SingleAsync());
        Assert.Equal(GateDeviceService.Hash(secret), stored.TokenHash);
        Assert.DoesNotContain(secret, string.Join(" ", await Use<SchoolDbContext, string[]>(db => db.AuditEvents.Select(x => x.Detail + x.Reason).ToArrayAsync())));

        using var phone = App.Browser();
        phone.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        var who = await phone.GetFromJsonAsync<JsonElement>("/api/v1/gate/device/whoami");
        Assert.Equal("Teléfono portón", who.GetProperty("device").GetString());
        var first = await phone.PostAsJsonAsync("/api/v1/gate/device/scan", new { code = token, mode = "In" });
        Assert.Equal("Recorded", (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("outcome").GetString());
        var second = await phone.PostAsJsonAsync("/api/v1/gate/device/scan", new { code = token, mode = "In" });
        Assert.Equal("Duplicate", (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("outcome").GetString());
        var mark = await Use<SchoolDbContext, GateEvent>(db => db.GateEvents.SingleAsync());
        Assert.Equal((GateSource.Mobile, stored.Id, (Guid?)null), (mark.Source, mark.DeviceId!.Value, mark.RecordedBy));

        // The device token opens nothing else, and a session cookie does not open the device endpoints.
        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.GetAsync("/api/v1/session")).StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, (await phone.GetAsync("/api/v1/gate/recent")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await staff.PostAsJsonAsync("/api/v1/gate/device/scan", new { code = token })).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await Post(staff, $"/api/v1/gate/devices/{stored.Id}/revoke", new { reason = "Teléfono extraviado en portería." })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.PostAsJsonAsync("/api/v1/gate/device/scan", new { code = token })).StatusCode);
    }

    [Fact]
    public async Task Cards_print_with_barcodes_and_photos_are_private()
    {
        using var staff = await SignIn(OwnerName);
        await Account("synthetic-gate", "gatekeeper");
        await Account("synthetic-basic");
        using var gate = await SignIn("synthetic-gate");
        using var basic = await SignIn("synthetic-basic");

        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, .. Convert.FromBase64String(
            "AAAADUlIRFIAAAABAAAAAQgGAAAAHxXEiQAAAA1JREFUeNpj+M/A8B8ABQAB/6Zcm10AAAAASUVORK5CYII=")];
        var token = (await staff.GetFromJsonAsync<JsonElement>("/api/v1/antiforgery")).GetProperty("token").GetString();
        using var form = new MultipartFormDataContent { { new ByteArrayContent(png) { Headers = { ContentType = new("image/png") } }, "file", "foto.png" } };
        using var upload = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/people/{student}/photo") { Content = form };
        upload.Headers.Add("X-XSRF-TOKEN", token);
        Assert.Equal(HttpStatusCode.NoContent, (await staff.SendAsync(upload)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await gate.GetAsync($"/api/v1/people/{student}/photo")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await basic.GetAsync($"/api/v1/people/{student}/photo")).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await gate.GetAsync($"/api/v1/students/{student}/card.pdf")).StatusCode);
        var single = await staff.GetAsync($"/api/v1/students/{student}/card.pdf?layout=card");
        Assert.Equal("application/pdf", single.Content.Headers.ContentType?.MediaType);
        var card = (await staff.GetFromJsonAsync<JsonElement>($"/api/v1/students/{student}/card")).GetProperty("card").GetProperty("token").GetString()!;
        using (var document = UglyToad.PdfPig.PdfDocument.Open(await single.Content.ReadAsByteArrayAsync()))
        {
            Assert.Equal(2, document.NumberOfPages);
            var text = string.Join('\n', document.GetPages().Select(x => x.Text));
            Assert.Contains("Lara Portería G-1", text);
            Assert.Contains(card, text);
        }
        var batch = await staff.GetAsync($"/api/v1/years/{year}/cards.pdf?gradeId={grade}");
        if (Environment.GetEnvironmentVariable("SCHOOLPLATFORM_PDF_DUMP") is { Length: > 0 } dump)
        {
            await File.WriteAllBytesAsync(Path.Combine(dump, "carnet-card.pdf"), await single.Content.ReadAsByteArrayAsync());
            await File.WriteAllBytesAsync(Path.Combine(dump, "carnets-sheet.pdf"), await batch.Content.ReadAsByteArrayAsync());
        }
        using (var document = UglyToad.PdfPig.PdfDocument.Open(await batch.Content.ReadAsByteArrayAsync()))
        {
            var text = string.Join('\n', document.GetPages().Select(x => x.Text));
            Assert.Contains("Mario Portería G-2", text);
            Assert.DoesNotContain("Nico", text);
        }
        Assert.Equal(2, await Use<SchoolDbContext, int>(db => db.StudentCards.CountAsync()));

        // The printed Code 128 encodes the token (round trip through ZXing).
        var modules = Barcodes.Code128Modules(card);
        var row = new ZXing.Common.BitArray(modules.Length);
        for (var i = 0; i < modules.Length; i++) if (modules[i]) row[i] = true;
        Assert.Equal(card, new ZXing.OneD.Code128Reader().decodeRow(0, row, null).Text);
    }
}
