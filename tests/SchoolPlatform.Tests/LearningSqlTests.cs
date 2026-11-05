using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.Learning;
using SchoolPlatform.Host.People;

namespace SchoolPlatform.Tests;

public sealed class LearningSqlTests : SqlTestBase
{
    // The year spans "today" so publication dates fall inside it.
    private static readonly DateOnly Start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-90);
    private Guid year, grade, course, student, leaver, relationship, teacherUser, otherUser;
    private readonly string storage = Path.Combine(Path.GetTempPath(), "SchoolPlatform_TestFiles_" + Guid.NewGuid().ToString("N"));
    protected override Dictionary<string, string?> ExtraConfiguration => new() { ["Storage:Root"] = storage };

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        year = await Use<AcademicService, Guid>(x => x.CreateYearAsync(Owner, new("Ciclo actual", Start, Start.AddDays(300)), default));
        await Use<AcademicService, Guid>(x => x.AddPeriodAsync(Owner, year, new(null, Start, Start.AddDays(60)), default));
        await Use<AcademicService>(async x => await x.SetYearStatusAsync(Owner, year, new((await x.YearAsync(year, default)).Year.Version, YearStatus.EnrollmentOpen, Reason), default));
        grade = await Use<AcademicService, Guid>(x => x.SaveGradeAsync(Owner, null, new("4B", "Cuarto bachillerato", "Diversificado", 1), default));
        var subject = await Use<CourseService, Guid>(x => x.SaveSubjectAsync(Owner, null, new("FIS", "Física"), default));
        await Use<CourseService, int>(x => x.CreateCoursesAsync(Owner, year, new(grade, null, [subject]), default));
        course = (await Use<CourseService, CourseView[]>(x => x.CoursesAsync(year, grade, default))).Single().Id;
        student = await Enrolled("Lía", "L-1");
        leaver = await Enrolled("Mario", "L-2");
        var history = Assert.Single(await Use<EnrollmentService, EnrollmentHistory[]>(x => x.StudentHistoryAsync(leaver, default)));
        await Use<EnrollmentService>(x => x.WithdrawAsync(Owner, history.Id, new(history.Version, Start.AddDays(10), Reason), default));

        teacherUser = await Staff("synthetic-teacher", "Ana", assign: true);
        otherUser = await Staff("synthetic-other", "Beto", assign: false);
        var guardian = (await Use<PeopleService, CreatedPerson>(x => x.CreateAsync(Owner, new("Clara", "Encargada", null, null, [], null, true, false), default))).Id;
        relationship = await Use<PeopleService, Guid>(x => x.CreateRelationshipAsync(Owner, new(student, guardian, "Mother", null, [], Reason), default));
        await Link(guardian, await Account("synthetic-guardian"));
        await Link(student, await Account("synthetic-student"));
    }

    private async Task<Guid> Enrolled(string given, string code)
    {
        var id = (await Use<PeopleService, CreatedPerson>(x => x.CreateAsync(Owner, new(given, "Tareas " + code, null, null, [], code, false, true), default))).Id;
        await Use<EnrollmentService, Guid>(x => x.EnrollAsync(Owner, new(id, year, grade, null, Start, null), default));
        return id;
    }
    private async Task Link(Guid person, Guid user) =>
        await Use<PeopleService>(async x => await x.LinkAccountAsync(Owner, person, new((await x.GetAsync(person, default)).Version, user, Reason), default));
    private async Task<Guid> Staff(string username, string given, bool assign)
    {
        var person = (await Use<PeopleService, CreatedPerson>(x => x.CreateAsync(Owner, new(given, "Docente", null, null, [], null, false, true), default))).Id;
        await Use<PeopleService>(async x => await x.AddRoleAsync(Owner, person, new((await x.GetAsync(person, default)).Version, "staff", null, "Docente"), default));
        var user = await Account(username);
        await Link(person, user);
        if (assign) await Use<CourseService, Guid>(x => x.AssignAsync(Owner, course, new(person, TeachingRoles.Lead, Start, Reason), default));
        return user;
    }
    private async Task<Guid> Published(DateTimeOffset due, bool allowLate = false)
    {
        var id = await Use<LearningService, Guid>(x => x.SaveAsync(teacherUser, course, null, new("Laboratorio", "Describe el experimento.", due, allowLate), default));
        var version = (await Use<LearningService, AssignmentView[]?>(x => x.CourseAssignmentsAsync(teacherUser, course, default)))!.Single(a => a.Id == id).Version;
        await Use<LearningService>(x => x.SetStatusAsync(teacherUser, course, id, AssignmentStatus.Published, new(version), default));
        return id;
    }
    private async Task<JsonElement> Mine(HttpClient client) =>
        await client.GetFromJsonAsync<JsonElement>($"/api/v1/me/students/{student}/assignments");

    [Fact]
    public async Task Students_submit_their_own_work_and_guardians_follow_status_with_the_faculty()
    {
        var draft = await Use<LearningService, Guid>(x => x.SaveAsync(teacherUser, course, null, new("Borrador", null, DateTimeOffset.UtcNow.AddDays(3), false), default));
        Assert.Equal("teaching-denied", (await Assert.ThrowsAsync<OperationException>(() =>
            Use<LearningService, Guid>(x => x.SaveAsync(otherUser, course, null, new("Ajena", null, DateTimeOffset.UtcNow.AddDays(3), false), default)))).Code);
        using var self = await SignIn("synthetic-student");
        Assert.Empty((await Mine(self)).EnumerateArray());

        var id = await Published(DateTimeOffset.UtcNow.AddDays(7));
        var item = Assert.Single((await Mine(self)).EnumerateArray());
        Assert.True(item.GetProperty("canSubmit").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, (await Post(self, $"/api/v1/me/assignments/{id}/submission", new SubmitRequest("Primera versión", null))).StatusCode);
        var version = (await Mine(self))[0].GetProperty("version").GetString();
        Assert.Equal(HttpStatusCode.NoContent, (await Post(self, $"/api/v1/me/assignments/{id}/submission", new SubmitRequest("Versión corregida", version))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(self, $"/api/v1/me/assignments/{draft}/submission", new SubmitRequest("No visible", null))).StatusCode);

        using var guardian = await SignIn("synthetic-guardian");
        Assert.Equal(HttpStatusCode.NotFound, (await guardian.GetAsync($"/api/v1/me/students/{student}/assignments")).StatusCode);
        var relVersion = (await Use<PeopleService, PersonDetail>(x => x.GetAsync(student, default))).Guardians.Single().Version;
        await Use<PeopleService>(x => x.UpdateRelationshipAsync(Owner, relationship, new(relVersion, "Mother", [Faculties.AcademicRecords], Reason), default));
        var seen = (await Mine(guardian))[0];
        Assert.Equal("Submitted", seen.GetProperty("submissionStatus").GetString());
        Assert.Equal(JsonValueKind.Null, seen.GetProperty("text").ValueKind);
        Assert.False(seen.GetProperty("canSubmit").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, (await Post(guardian, $"/api/v1/me/assignments/{id}/submission", new SubmitRequest("Por el estudiante", null))).StatusCode);

        var submissions = (await Use<LearningService, AssignmentSubmissions?>(x => x.SubmissionsAsync(teacherUser, course, id, default)))!;
        var row = Assert.Single(submissions.Rows);
        Assert.Equal(("Versión corregida", 2), (row.Text, row.Attempts));
        Assert.Null(await Use<LearningService, AssignmentSubmissions?>(x => x.SubmissionsAsync(otherUser, course, id, default)));
        await Use<LearningService>(x => x.FeedbackAsync(teacherUser, course, row.SubmissionId!.Value, new("Buen análisis.", row.Version!), default));
        var reviewed = (await Mine(self))[0];
        Assert.Equal("Buen análisis.", reviewed.GetProperty("feedback").GetString());
        Assert.False(reviewed.GetProperty("canSubmit").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await Post(self, $"/api/v1/me/assignments/{id}/submission",
            new SubmitRequest("Otra vez", reviewed.GetProperty("version").GetString()))).StatusCode);
    }

    [Fact]
    public async Task Due_dates_late_policy_closing_and_audience_are_enforced()
    {
        var strict = await Published(DateTimeOffset.UtcNow.AddMinutes(-5));
        var lenient = await Published(DateTimeOffset.UtcNow.AddMinutes(-5), allowLate: true);
        using var self = await SignIn("synthetic-student");
        Assert.Equal(HttpStatusCode.Conflict, (await Post(self, $"/api/v1/me/assignments/{strict}/submission", new SubmitRequest("Tarde", null))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Post(self, $"/api/v1/me/assignments/{lenient}/submission", new SubmitRequest("Tarde", null))).StatusCode);
        var rows = (await Use<LearningService, AssignmentSubmissions?>(x => x.SubmissionsAsync(teacherUser, course, lenient, default)))!.Rows;
        Assert.True(Assert.Single(rows).Late);
        Assert.DoesNotContain(rows, x => x.StudentId == leaver);

        var version = (await Use<LearningService, AssignmentView[]?>(x => x.CourseAssignmentsAsync(teacherUser, course, default)))!.Single(a => a.Id == lenient).Version;
        await Use<LearningService>(x => x.SetStatusAsync(teacherUser, course, lenient, AssignmentStatus.Closed, new(version), default));
        Assert.Equal(HttpStatusCode.Conflict, (await Post(self, $"/api/v1/me/assignments/{lenient}/submission",
            new SubmitRequest("Después del cierre", (await Mine(self)).EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == lenient).GetProperty("version").GetString()))).StatusCode);
    }

    private static async Task<HttpResponseMessage> Upload(HttpClient client, Guid assignment, string name, byte[] content)
    {
        var token = (await client.GetFromJsonAsync<JsonElement>("/api/v1/antiforgery")).GetProperty("token").GetString();
        using var form = new MultipartFormDataContent { { new ByteArrayContent(content), "file", name } };
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/me/assignments/{assignment}/files") { Content = form };
        request.Headers.Add("X-XSRF-TOKEN", token);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Private_files_are_type_checked_and_downloaded_only_by_the_student_or_course_staff()
    {
        try
        {
            var id = await Published(DateTimeOffset.UtcNow.AddDays(5));
            using var self = await SignIn("synthetic-student");
            var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3 };
            Assert.Equal(HttpStatusCode.NotFound, (await Upload(self, id, "foto.png", png)).StatusCode);
            await Post(self, $"/api/v1/me/assignments/{id}/submission", new SubmitRequest("Adjunto mi evidencia.", null));
            Assert.Equal(HttpStatusCode.BadRequest, (await Upload(self, id, "virus.exe", [0x4D, 0x5A, 0x90])).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Upload(self, id, "renombrado.pdf", [0x4D, 0x5A, 0x90])).StatusCode);
            var uploaded = await Upload(self, id, "../../foto final.png", png);
            Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
            var file = await uploaded.Content.ReadFromJsonAsync<JsonElement>();
            var fileId = file.GetProperty("id").GetGuid();
            Assert.Equal("foto final.png", file.GetProperty("fileName").GetString());
            Assert.Single(Directory.GetFiles(storage, "*", SearchOption.AllDirectories));

            var download = await self.GetAsync($"/api/v1/files/{fileId}");
            Assert.Equal(HttpStatusCode.OK, download.StatusCode);
            Assert.Equal(png, await download.Content.ReadAsByteArrayAsync());
            Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
            var row = (await Use<LearningService, AssignmentSubmissions?>(x => x.SubmissionsAsync(teacherUser, course, id, default)))!.Rows.Single();
            Assert.Equal(fileId, Assert.Single(row.Files!).Id);

            using var guardian = await SignIn("synthetic-guardian");
            Assert.Equal(HttpStatusCode.NotFound, (await guardian.GetAsync($"/api/v1/files/{fileId}")).StatusCode);
            var other = await SignIn("synthetic-other");
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/files/{fileId}")).StatusCode);
            var teacher = await SignIn("synthetic-teacher");
            Assert.Equal(HttpStatusCode.OK, (await teacher.GetAsync($"/api/v1/files/{fileId}")).StatusCode);

            Assert.Equal(HttpStatusCode.NoContent, (await Post(self, $"/api/v1/me/assignments/{id}/files/{fileId}/remove", new { })).StatusCode);
            Assert.Empty(await self.GetFromJsonAsync<JsonElement[]>($"/api/v1/me/assignments/{id}/files") ?? []);
            Assert.Equal(HttpStatusCode.NotFound, (await teacher.GetAsync($"/api/v1/files/{fileId}")).StatusCode);
        }
        finally
        {
            if (Directory.Exists(storage)) Directory.Delete(storage, recursive: true);
        }
    }
}
