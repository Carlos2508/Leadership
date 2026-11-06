using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Chat;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Tests;

public sealed class ChatSqlTests : SqlTestBase
{
    private static readonly DateOnly Start = new(2027, 1, 10);
    private Guid year, grade, a, b, course, student, sibling, teacher, parent, studentAccount, teacherAccount, parentAccount, relationship;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        year = await Use<AcademicService, Guid>(s => s.CreateYearAsync(Owner, new("Chat 2027", Start, new(2027, 11, 20)), default));
        await Use<AcademicService, Guid>(s => s.AddPeriodAsync(Owner, year, new(null, Start, Start.AddDays(60)), default));
        await Use<AcademicService>(async s => await s.SetYearStatusAsync(Owner, year, new((await s.YearAsync(year, default)).Year.Version, YearStatus.EnrollmentOpen, Reason), default));
        grade = await Use<AcademicService, Guid>(s => s.SaveGradeAsync(Owner, null, new("CHAT", "Grado sintético", "Primaria", 1), default));
        a = await Use<AcademicService, Guid>(s => s.SaveSectionAsync(Owner, year, null, new(grade, "A", null, null), default));
        b = await Use<AcademicService, Guid>(s => s.SaveSectionAsync(Owner, year, null, new(grade, "B", null, null), default));
        student = await Person("Estudiante", "CHAT-1");
        sibling = await Person("Otro estudiante", "CHAT-2");
        teacher = await Person("Docente");
        parent = await Person("Encargado", guardian: true);
        await Use<PeopleService>(async s => await s.AddRoleAsync(Owner, teacher, new((await s.GetAsync(teacher, default)).Version, "staff", null, "Docente"), default));
        studentAccount = await LinkedAccount(student, "chat-student");
        teacherAccount = await LinkedAccount(teacher, "chat-teacher");
        parentAccount = await LinkedAccount(parent, "chat-parent");
        await LinkedAccount(sibling, "chat-other");
        await Use<EnrollmentService, Guid>(s => s.EnrollAsync(Owner, new(student, year, grade, a, Start, null), default));
        await Use<EnrollmentService, Guid>(s => s.EnrollAsync(Owner, new(sibling, year, grade, b, Start, null), default));
        var subject = await Use<CourseService, Guid>(s => s.SaveSubjectAsync(Owner, null, new("CHT", "Materia sintética"), default));
        await Use<CourseService, int>(s => s.CreateCoursesAsync(Owner, year, new(grade, a, [subject]), default));
        course = (await Use<CourseService, CourseView[]>(s => s.CoursesAsync(year, null, default))).Single().Id;
        await Use<CourseService, Guid>(s => s.AssignAsync(Owner, course, new(teacher, TeachingRoles.Lead, Start, Reason), default));
        relationship = await Use<PeopleService, Guid>(s => s.CreateRelationshipAsync(Owner, new(student, parent, "Mother", new(2026, 1, 1), [Faculties.Communications], Reason), default));
    }

    private async Task<Guid> Person(string name, string? code = null, bool guardian = false) =>
        (await Use<PeopleService, CreatedPerson>(s => s.CreateAsync(Owner, new(name, "Ejemplo sintético", null, null, [], code, guardian, true), default))).Id;
    private async Task<Guid> LinkedAccount(Guid person, string username)
    {
        var user = await Account(username);
        await Use<PeopleService>(async s => await s.LinkAccountAsync(Owner, person, new((await s.GetAsync(person, default)).Version, user, Reason), default));
        return user;
    }
    private Task<Guid> StartChat(Guid actor, Guid? member = null) => Use<ChatService, Guid>(s => s.StartAsync(actor, year, new(student, teacher, member ?? student), default));

    [Fact]
    public async Task Only_assigned_teachers_students_and_authorized_guardians_can_discover_or_start_pairs()
    {
        Assert.Single((await Use<ChatService, PagedResult<ChatContact>>(s => s.ContactsAsync(studentAccount, year, null, 1, default))).Items);
        Assert.Single((await Use<ChatService, PagedResult<ChatContact>>(s => s.ContactsAsync(parentAccount, year, null, 1, default))).Items);
        Assert.Equal(2, (await Use<ChatService, PagedResult<ChatContact>>(s => s.ContactsAsync(teacherAccount, year, null, 1, default))).Total);
        Assert.False((await Use<ChatService, ChatSummary>(s => s.SummaryAsync(Owner, default))).Available); // administrator is not a participant
        foreach (var request in new[] { new StartChat(student, sibling, student), new StartChat(student, teacher, sibling), new StartChat(sibling, teacher, sibling), new StartChat(student, teacher, teacher) })
            Assert.Equal(404, (await Assert.ThrowsAsync<OperationException>(() => Use<ChatService, Guid>(s => s.StartAsync(studentAccount, year, request, default)))).Status);
        var first = await StartChat(studentAccount);
        Assert.Equal(first, await StartChat(teacherAccount));
        Assert.NotEqual(first, await StartChat(parentAccount, parent));
    }

    [Fact]
    public async Task Messages_are_durable_idempotent_ordered_and_reads_never_mark_unseen_messages()
    {
        var id = await StartChat(studentAccount);
        var key = Guid.NewGuid();
        var sent = await Use<ChatService, ChatMessageView>(s => s.SendAsync(studentAccount, year, id, new(key, "Hola\n¿Podemos revisar la tarea?"), default));
        Assert.Equal(sent, await Use<ChatService, ChatMessageView>(s => s.SendAsync(studentAccount, year, id, new(key, sent.Body), default)));
        Assert.Equal(1, (await Use<ChatService, ChatSummary>(s => s.SummaryAsync(teacherAccount, default))).Unread);
        Assert.Equal(0, (await Use<ChatService, ChatSummary>(s => s.SummaryAsync(parentAccount, default))).Unread);
        await Use<ChatService>(s => s.ReadAsync(teacherAccount, year, id, 1, default));
        await Use<ChatService>(s => s.ReadAsync(teacherAccount, year, id, 0, default));
        var reply = await Use<ChatService, ChatMessageView>(s => s.SendAsync(teacherAccount, year, id, new(Guid.NewGuid(), "Sí, mañana."), default));
        Assert.Equal(2, reply.Sequence);
        var page = await Use<ChatService, ChatPage>(s => s.MessagesAsync(studentAccount, year, id, null, null, default));
        Assert.Equal(1, page.Conversation.PeerReadSequence);
        Assert.Equal(new[] { true, false }, page.Items.Select(m => m.Mine));
        Assert.Equal(1, page.Conversation.Unread);
        Assert.Equal(409, (await Assert.ThrowsAsync<OperationException>(() => Use<ChatService, ChatMessageView>(s => s.SendAsync(studentAccount, year, id, new(key, "Cambio"), default)))).Status);
        Assert.Equal(400, (await Assert.ThrowsAsync<OperationException>(() => Use<ChatService>(s => s.ReadAsync(studentAccount, year, id, 3, default)))).Status);
    }

    [Fact]
    public async Task Ending_faculty_or_enrollment_immediately_removes_history_unread_and_send_access()
    {
        var id = await StartChat(parentAccount, parent);
        await Use<ChatService, ChatMessageView>(s => s.SendAsync(teacherAccount, year, id, new(Guid.NewGuid(), "Mensaje privado"), default));
        await Use<SchoolDbContext>(async db => { (await db.RelationshipFaculties.SingleAsync(f => f.RelationshipId == relationship)).RevokedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(); });
        Assert.False((await Use<ChatService, ChatSummary>(s => s.SummaryAsync(parentAccount, default))).Available);
        Assert.Empty(await Use<ChatService, ChatThread[]>(s => s.ListAsync(teacherAccount, year, default)));
        Assert.Equal(404, (await Assert.ThrowsAsync<OperationException>(() => Use<ChatService, ChatPage>(s => s.MessagesAsync(parentAccount, year, id, null, null, default)))).Status);
        var own = await StartChat(studentAccount);
        await Use<EnrollmentService>(async s => { var e = (await s.StudentHistoryAsync(student, default)).Single(); await s.WithdrawAsync(Owner, e.Id, new(e.Version, Start, Reason), default); });
        Assert.Equal(404, (await Assert.ThrowsAsync<OperationException>(() => Use<ChatService, ChatMessageView>(s => s.SendAsync(teacherAccount, year, own, new(Guid.NewGuid(), "No permitido"), default)))).Status);
    }

    [Fact]
    public async Task Concurrent_starts_and_retries_create_one_pair_and_one_message_per_client_attempt()
    {
        var ids = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => StartChat(studentAccount)));
        Assert.Single(ids.Distinct());
        var key = Guid.NewGuid();
        var messages = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Use<ChatService, ChatMessageView>(s => s.SendAsync(studentAccount, year, ids[0], new(key, "Un solo mensaje"), default))));
        Assert.All(messages, m => Assert.Equal(1, m.Sequence));
        var different = await Task.WhenAll(Enumerable.Range(0, 6).Select(i => Use<ChatService, ChatMessageView>(s => s.SendAsync(teacherAccount, year, ids[0], new(Guid.NewGuid(), "Respuesta " + i), default))));
        Assert.Equal(6, different.Select(m => m.Sequence).Distinct().Count());
    }

    [Fact]
    public async Task History_cursors_and_validation_do_not_lose_or_duplicate_messages()
    {
        var id = await StartChat(studentAccount);
        for (var i = 1; i <= 55; i++) await Use<ChatService, ChatMessageView>(s => s.SendAsync(studentAccount, year, id, new(Guid.NewGuid(), "Mensaje " + i), default));
        var page = await Use<ChatService, ChatPage>(s => s.MessagesAsync(teacherAccount, year, id, null, null, default));
        Assert.True(page.HasMore);
        Assert.Equal(6, page.Items.First().Sequence);
        var older = await Use<ChatService, ChatPage>(s => s.MessagesAsync(teacherAccount, year, id, 6, null, default));
        Assert.Equal(5, older.Items.Length);
        Assert.False(older.HasMore);
        Assert.Equal(55, (await Use<ChatService, ChatSummary>(s => s.SummaryAsync(teacherAccount, default))).Unread);
        foreach (var text in new[] { "   ", new string('a', 4001), "a\0b" })
            Assert.Equal(400, (await Assert.ThrowsAsync<OperationException>(() => Use<ChatService, ChatMessageView>(s => s.SendAsync(studentAccount, year, id, new(Guid.NewGuid(), text), default)))).Status);
    }

    [Fact]
    public async Task Conversation_search_is_scoped_and_own_sends_do_not_create_new_incoming_notifications()
    {
        var id = await StartChat(studentAccount);
        await StartChat(parentAccount, parent);
        var all = await Use<ChatService, PagedResult<ChatThread>>(s => s.BrowseAsync(teacherAccount, year, null, 1, default));
        Assert.Equal(2, all.Total);
        Assert.Single((await Use<ChatService, PagedResult<ChatThread>>(s => s.BrowseAsync(teacherAccount, year, "Encargado", 1, default))).Items);
        Assert.Empty((await Use<ChatService, PagedResult<ChatThread>>(s => s.BrowseAsync(studentAccount, year, "Encargado", 1, default))).Items);
        Assert.Empty((await Use<ChatService, PagedResult<ChatThread>>(s => s.BrowseAsync(teacherAccount, year, null, 2, default))).Items);
        await Use<ChatService, ChatMessageView>(s => s.SendAsync(studentAccount, year, id, new(Guid.NewGuid(), "Consulta pendiente"), default));
        var before = await Use<ChatService, ChatSummary>(s => s.SummaryAsync(teacherAccount, default));
        await Use<ChatService, ChatMessageView>(s => s.SendAsync(teacherAccount, year, id, new(Guid.NewGuid(), "Respuesta"), default));
        var after = await Use<ChatService, ChatSummary>(s => s.SummaryAsync(teacherAccount, default));
        Assert.Equal(before.Revision, after.Revision);
        Assert.Equal(1, after.Unread);
    }

    [Fact]
    public async Task A_scheduled_future_withdrawal_keeps_the_current_dated_course_scope_until_it_takes_effect()
    {
        var id = await StartChat(studentAccount);
        await Use<EnrollmentService>(async s => { var e = (await s.StudentHistoryAsync(student, default)).Single();
            await s.WithdrawAsync(Owner, e.Id, new(e.Version, Start.AddDays(40), Reason), default); });
        Assert.Single((await Use<ChatService, PagedResult<ChatContact>>(s => s.ContactsAsync(studentAccount, year, null, 1, default))).Items);
        Assert.Equal(id, (await Use<ChatService, ChatPage>(s => s.MessagesAsync(studentAccount, year, id, null, null, default))).Conversation.Id);
    }

    [Fact]
    public async Task Http_requires_active_session_csrf_and_resource_scope_without_admin_bypass()
    {
        var id = await StartChat(studentAccount);
        var path = $"/api/v1/years/{year}/chat/conversations/{id}/messages";
        using var anonymous = App.Browser();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
        using var outsider = await SignIn("chat-other");
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync(path)).StatusCode);
        using var browser = await SignIn("chat-student");
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsJsonAsync(path, new SendChat(Guid.NewGuid(), "Hola"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(browser, path, new SendChat(Guid.NewGuid(), "Hola"))).StatusCode);
        using var owner = await SignIn("synthetic-owner");
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(path)).StatusCode);
    }
}
