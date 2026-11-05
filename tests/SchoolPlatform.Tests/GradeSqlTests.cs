using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.Finance;
using SchoolPlatform.Host.Grades;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Tests;

public sealed class GradeSqlTests : SqlTestBase
{
    private static readonly DateOnly Start = new(2026, 1, 12);
    private Guid year, period, grade, course, student, outsider, cutoff, relationship, teacherUser, otherTeacherUser;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        year = await Use<AcademicService, Guid>(x => x.CreateYearAsync(Owner, new("2026", Start, new(2026, 10, 30)), default));
        period = await Use<AcademicService, Guid>(x => x.AddPeriodAsync(Owner, year, new(null, Start, Start.AddDays(60)), default));
        await Use<AcademicService>(async x => await x.SetYearStatusAsync(Owner, year, new((await x.YearAsync(year, default)).Year.Version, YearStatus.EnrollmentOpen, Reason), default));
        grade = await Use<AcademicService, Guid>(x => x.SaveGradeAsync(Owner, null, new("3B", "Tercero básico", "Básico", 1), default));
        var other = await Use<AcademicService, Guid>(x => x.SaveGradeAsync(Owner, null, new("1P", "Primero primaria", "Primaria", 2), default));
        var subject = await Use<CourseService, Guid>(x => x.SaveSubjectAsync(Owner, null, new("MAT", "Matemática"), default));
        await Use<CourseService, int>(x => x.CreateCoursesAsync(Owner, year, new(grade, null, [subject]), default));
        course = (await Use<CourseService, CourseView[]>(x => x.CoursesAsync(year, grade, default))).Single().Id;

        student = await Person("Nora", "N-1", grade);
        outsider = await Person("Otto", "N-2", other);
        teacherUser = await TeacherWithCourse("synthetic-teacher", "Rita", assign: true);
        otherTeacherUser = await TeacherWithCourse("synthetic-other", "Iván", assign: false);

        var guardian = (await Use<PeopleService, CreatedPerson>(x => x.CreateAsync(Owner, new("Gloria", "Encargada", null, null, [], null, true, false), default))).Id;
        relationship = await Use<PeopleService, Guid>(x => x.CreateRelationshipAsync(Owner, new(student, guardian, "Mother", null, [], Reason), default));
        var guardianUser = await Account("synthetic-guardian");
        await Link(guardian, guardianUser);
        await Link(student, await Account("synthetic-student"));

        var concept = await Use<FinanceSetupService, Guid>(x => x.SaveConceptAsync(Owner, null, new("COL", "Colegiatura", true, true), default));
        var billing = await Use<FinanceSetupService, Guid>(x => x.AddPeriodAsync(Owner, year, new("Cuota 1", new(2026, 2, 5)), default));
        cutoff = await Use<FinanceSetupService, Guid>(x => x.AddCutoffAsync(Owner, year, new("Primer bimestre", 1), default));
        var request = new GenerationRequest(year, concept, billing, 1, 400m, null, null, null, null, null);
        var preview = await Use<ChargeService, GenerationPreview>(x => x.PreviewAsync(Owner, request, default));
        await Use<ChargeService, GenerationResult>(x => x.GenerateAsync(Owner, request with { PreviewHash = preview.PreviewHash }, default));
    }

    private async Task<Guid> Person(string given, string code, Guid grade)
    {
        var id = (await Use<PeopleService, CreatedPerson>(x => x.CreateAsync(Owner, new(given, "Notas " + code, null, null, [], code, false, true), default))).Id;
        await Use<EnrollmentService, Guid>(x => x.EnrollAsync(Owner, new(id, year, grade, null, Start, null), default));
        return id;
    }

    private async Task Link(Guid person, Guid user) =>
        await Use<PeopleService>(async x => await x.LinkAccountAsync(Owner, person, new((await x.GetAsync(person, default)).Version, user, Reason), default));

    private async Task<Guid> TeacherWithCourse(string username, string given, bool assign)
    {
        var person = (await Use<PeopleService, CreatedPerson>(x => x.CreateAsync(Owner, new(given, "Docente", null, null, [], null, false, true), default))).Id;
        await Use<PeopleService>(async x => await x.AddRoleAsync(Owner, person, new((await x.GetAsync(person, default)).Version, "staff", null, "Docente"), default));
        var user = await Account(username);
        await Link(person, user);
        if (assign) await Use<CourseService, Guid>(x => x.AssignAsync(Owner, course, new(person, TeachingRoles.Lead, Start, Reason), default));
        return user;
    }

    private Task<int> Capture(Guid user, decimal? score, string status = ScoreStatus.Scored, string? version = null, string? reason = null) =>
        Use<GradeService, int>(async x => (await x.SaveAsync(user, course, new(period, [new(student, score, status, version)], reason), default)).Changed);
    private async Task<string?> Version() =>
        (await Use<GradeService, GradeSheet?>(x => x.SheetAsync(Owner, course, period, default)))!.Rows.Single(r => r.StudentId == student).Version;
    private Task Publish() => Use<GradeService>(x => x.PublishAsync(Owner, year, new(period, cutoff, "Entrega del primer período"), default));

    [Fact]
    public async Task Only_the_assigned_teacher_captures_and_every_change_keeps_a_revision()
    {
        var sheet = await Use<GradeService, GradeSheet?>(x => x.SheetAsync(teacherUser, course, null, default));
        Assert.NotNull(sheet);
        Assert.True(sheet.CanEdit);
        Assert.Equal([student], sheet.Rows.Select(x => x.StudentId).ToArray());
        Assert.Null(await Use<GradeService, GradeSheet?>(x => x.SheetAsync(otherTeacherUser, course, null, default)));
        await Assert.ThrowsAsync<OperationException>(() => Capture(otherTeacherUser, 80));
        await Assert.ThrowsAsync<OperationException>(() => Capture(teacherUser, 100.5m));
        await Assert.ThrowsAsync<OperationException>(() => Capture(teacherUser, 70.555m));
        await Assert.ThrowsAsync<OperationException>(() => Use<GradeService, SaveScoresResult>(x =>
            x.SaveAsync(teacherUser, course, new(period, [new(outsider, 90, ScoreStatus.Scored, null)], null), default)));

        Assert.Equal(1, await Capture(teacherUser, 0));
        Assert.Equal("resource-changed", (await Assert.ThrowsAsync<OperationException>(() => Capture(teacherUser, 75))).Code);
        Assert.Equal(1, await Capture(teacherUser, 75.5m, version: await Version()));
        Assert.Equal(0, await Capture(teacherUser, 75.5m, version: await Version()));
        var revisions = await Use<SchoolDbContext, ScoreRevision[]>(db => db.ScoreRevisions.OrderBy(x => x.ChangedAt).ToArrayAsync());
        Assert.Equal(2, revisions.Length);
        Assert.Equal((0m, 75.5m), (revisions[1].PreviousScore!.Value, revisions[1].NewScore!.Value));
    }

    [Fact]
    public async Task Publication_requires_complete_capture_and_locks_teacher_edits()
    {
        Assert.Equal("incomplete", (await Assert.ThrowsAsync<OperationException>(Publish)).Code);
        await Capture(teacherUser, 88);
        await Publish();
        Assert.Equal("already-published", (await Assert.ThrowsAsync<OperationException>(Publish)).Code);
        var locked = await Assert.ThrowsAsync<OperationException>(async () => await Capture(teacherUser, 90, version: await Version()));
        Assert.Equal("period-published", locked.Code);
        await Assert.ThrowsAsync<OperationException>(async () => await Capture(Owner, 90, version: await Version()));
        await Capture(Owner, 90, version: await Version(), reason: "Corrección autorizada por coordinación académica.");
        Assert.Contains(await Use<SchoolDbContext, string[]>(db => db.AuditEvents.Select(x => x.Action).ToArrayAsync()), x => x == "PublishedScoresCorrected");
        var publication = Assert.Single(await Use<GradeService, PublicationView[]>(x => x.PublicationsAsync(year, default)));
        Assert.True(publication.Published);
        Assert.Equal((1, 1), (publication.Expected, publication.Recorded));
    }

    private async Task<JsonElement> Grades(HttpClient client)
    {
        var response = await client.GetAsync($"/api/v1/me/students/{student}/grades");
        if (!response.IsSuccessStatusCode) Assert.Fail($"{(int)response.StatusCode}: {string.Join(" || ", Errors)}");
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Families_see_published_scores_only_with_the_faculty_and_clearance_on_every_read()
    {
        await Capture(teacherUser, 93);
        using var guardian = await SignIn("synthetic-guardian");
        using var self = await SignIn("synthetic-student");
        Assert.Equal(HttpStatusCode.NotFound, (await guardian.GetAsync($"/api/v1/me/students/{student}/grades")).StatusCode);
        Assert.Empty((await Grades(self)).GetProperty("periods").EnumerateArray());

        await Publish();
        var version = (await Use<PeopleService, PersonDetail>(x => x.GetAsync(student, default))).Guardians.Single().Version;
        await Use<PeopleService>(x => x.UpdateRelationshipAsync(Owner, relationship, new(version, "Mother", [Faculties.AcademicRecords], Reason), default));
        var blocked = (await Grades(guardian)).GetProperty("periods")[0];
        Assert.Equal("Blocked", blocked.GetProperty("status").GetString());
        Assert.Empty(blocked.GetProperty("grades").EnumerateArray());
        Assert.Empty(blocked.GetProperty("pending").EnumerateArray());

        var charge = (await Use<ChargeService, Statement>(x => x.StatementAsync(student, year, default))).Charges.Single();
        var receipt = await Use<PaymentService, ReceiptView>(x => x.RecordAsync(Owner, new(Guid.NewGuid().ToString("N"), "Gloria Encargada", "Cash", null,
            Start.AddDays(5), null, 400m, [new(charge.Id, 400m)]), default));
        var available = (await Grades(guardian)).GetProperty("periods")[0];
        Assert.Equal("Available", available.GetProperty("status").GetString());
        Assert.Equal(93m, available.GetProperty("grades")[0].GetProperty("score").GetDecimal());
        Assert.Equal("Available", (await Grades(self)).GetProperty("periods")[0].GetProperty("status").GetString());

        var paymentVersion = (await Use<ChargeService, Statement>(x => x.StatementAsync(student, year, default))).Payments.Single().Version;
        await Use<PaymentService>(x => x.ReverseAsync(Owner, receipt.PaymentId, new(paymentVersion, Reason), default));
        Assert.Equal("Blocked", (await Grades(guardian)).GetProperty("periods")[0].GetProperty("status").GetString());
    }

    private static async Task<string> PdfText(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("inline", response.Content.Headers.ContentDisposition?.ToString());
        var bytes = await response.Content.ReadAsByteArrayAsync();
        // Optional visual review: SCHOOLPLATFORM_PDF_DUMP=<folder> keeps a copy of every generated document.
        if (Environment.GetEnvironmentVariable("SCHOOLPLATFORM_PDF_DUMP") is { Length: > 0 } dump)
            await File.WriteAllBytesAsync(Path.Combine(dump, $"{DateTime.Now:HHmmssfff}-{response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')}"), bytes);
        using var document = UglyToad.PdfPig.PdfDocument.Open(bytes);
        return string.Join('\n', document.GetPages().Select(x => x.Text));
    }

    [Fact]
    public async Task Printed_documents_follow_the_same_access_and_clearance_rules()
    {
        await Capture(teacherUser, 93);
        using var staff = await SignIn(OwnerName);
        using var guardian = await SignIn("synthetic-guardian");
        using var teacher = await SignIn("synthetic-teacher");
        using var other = await SignIn("synthetic-other");
        var cards = $"/api/v1/years/{year}/periods/{period}/report-cards.pdf?gradeId={grade}";
        Assert.Equal(HttpStatusCode.Conflict, (await staff.GetAsync(cards)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.GetAsync(cards)).StatusCode);

        // The teacher's working sheet: only for those who can see the course.
        Assert.Contains("Notas N-1", await PdfText(teacher, $"/api/v1/courses/{course}/grades.pdf"));
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/courses/{course}/grades.pdf")).StatusCode);

        await Publish();
        var version = (await Use<PeopleService, PersonDetail>(x => x.GetAsync(student, default))).Guardians.Single().Version;
        await Use<PeopleService>(x => x.UpdateRelationshipAsync(Owner, relationship, new(version, "Mother",
            [Faculties.AcademicRecords, Faculties.Finance], Reason), default));

        // Not cleared: neither the family copy nor the staff report card prints the score.
        var family = await PdfText(guardian, $"/api/v1/me/students/{student}/grades.pdf");
        Assert.Contains("no disponibles", family);
        Assert.DoesNotContain("93", family);
        var withheld = await PdfText(staff, cards);
        Assert.Contains("Informe retenido", withheld);
        Assert.DoesNotContain("93", withheld);
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.GetAsync($"/api/v1/me/students/{student}/grades.pdf")).StatusCode);

        var charge = (await Use<ChargeService, Statement>(x => x.StatementAsync(student, year, default))).Charges.Single();
        var receipt = await Use<PaymentService, ReceiptView>(x => x.RecordAsync(Owner, new(Guid.NewGuid().ToString("N"), "Gloria Encargada", "Cash", null,
            Start.AddDays(5), null, 400m, [new(charge.Id, 400m)]), default));
        Assert.Contains("93", await PdfText(guardian, $"/api/v1/me/students/{student}/grades.pdf"));
        var printed = await PdfText(staff, cards);
        Assert.Contains("93", printed);
        Assert.Contains("Matemática", printed);

        var receiptText = await PdfText(staff, $"/api/v1/finance/payments/{receipt.PaymentId}/receipt.pdf");
        Assert.Contains("Recibo de pago", receiptText);
        Assert.Contains(OwnerName, receiptText);
        Assert.Contains("Gloria Encargada", receiptText);
        Assert.Contains($"No. {receipt.Receipt.Number}", receiptText);
        var familyReceipt = await PdfText(guardian, $"/api/v1/me/students/{student}/receipts/{receipt.PaymentId}/receipt.pdf");
        Assert.Contains("Gloria Encargada", familyReceipt);
        Assert.DoesNotContain(OwnerName, familyReceipt);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.GetAsync($"/api/v1/finance/payments/{receipt.PaymentId}/receipt.pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.GetAsync($"/api/v1/me/students/{student}/receipts/{receipt.PaymentId}/receipt.pdf")).StatusCode);

        var paymentVersion = (await Use<ChargeService, Statement>(x => x.StatementAsync(student, year, default))).Payments.Single().Version;
        await Use<PaymentService>(x => x.ReverseAsync(Owner, receipt.PaymentId, new(paymentVersion, Reason), default));
        Assert.Contains("ANULADO", await PdfText(staff, $"/api/v1/finance/payments/{receipt.PaymentId}/receipt.pdf"));
        Assert.DoesNotContain("93", await PdfText(guardian, $"/api/v1/me/students/{student}/grades.pdf"));
        Assert.Empty(Errors);
    }
}
