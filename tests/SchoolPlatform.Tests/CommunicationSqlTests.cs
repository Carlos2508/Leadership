using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.Communication;
using SchoolPlatform.Host.People;

namespace SchoolPlatform.Tests;

public sealed class CommunicationSqlTests : SqlTestBase
{
    private static readonly DateOnly Start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30);
    private Guid year, grade, otherGrade, course, studentUser, outsiderUser, guardianUser, silentGuardianUser, teacherUser, otherTeacherUser;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        year = await Use<AcademicService, Guid>(x => x.CreateYearAsync(Owner, new("Ciclo avisos", Start, Start.AddDays(300)), default));
        await Use<AcademicService, Guid>(x => x.AddPeriodAsync(Owner, year, new(null, Start, Start.AddDays(60)), default));
        await Use<AcademicService>(async x => await x.SetYearStatusAsync(Owner, year, new((await x.YearAsync(year, default)).Year.Version, YearStatus.EnrollmentOpen, Reason), default));
        grade = await Use<AcademicService, Guid>(x => x.SaveGradeAsync(Owner, null, new("1S", "Primero secundaria", "Secundaria", 1), default));
        otherGrade = await Use<AcademicService, Guid>(x => x.SaveGradeAsync(Owner, null, new("2S", "Segundo secundaria", "Secundaria", 2), default));
        var subject = await Use<CourseService, Guid>(x => x.SaveSubjectAsync(Owner, null, new("ART", "Artes"), default));
        await Use<CourseService, int>(x => x.CreateCoursesAsync(Owner, year, new(grade, null, [subject]), default));
        course = (await Use<CourseService, CourseView[]>(x => x.CoursesAsync(year, grade, default))).Single().Id;

        var student = await Person("Eva", "C-1", grade);
        studentUser = await Linked(student, "synthetic-student");
        outsiderUser = await Linked(await Person("Iker", "C-2", otherGrade), "synthetic-outsider");
        guardianUser = await Guardian("Paula", student, [Faculties.Communications], "synthetic-guardian");
        silentGuardianUser = await Guardian("Raúl", student, [Faculties.AcademicRecords], "synthetic-silent");
        teacherUser = await Staff("Nadia", "synthetic-teacher", assign: true);
        otherTeacherUser = await Staff("Omar", "synthetic-other", assign: false);
    }

    private async Task<Guid> Person(string given, string code, Guid gradeId)
    {
        var id = (await Use<PeopleService, CreatedPerson>(x => x.CreateAsync(Owner, new(given, "Avisos " + code, null, null, [], code, false, true), default))).Id;
        await Use<EnrollmentService, Guid>(x => x.EnrollAsync(Owner, new(id, year, gradeId, null, Start, null), default));
        return id;
    }
    private async Task<Guid> Linked(Guid person, string username)
    {
        var user = await Account(username);
        await Use<PeopleService>(async x => await x.LinkAccountAsync(Owner, person, new((await x.GetAsync(person, default)).Version, user, Reason), default));
        return user;
    }
    private async Task<Guid> Guardian(string given, Guid student, string[] faculties, string username)
    {
        var id = (await Use<PeopleService, CreatedPerson>(x => x.CreateAsync(Owner, new(given, "Encargado", null, null, [], null, true, true), default))).Id;
        await Use<PeopleService, Guid>(x => x.CreateRelationshipAsync(Owner, new(student, id, "Mother", null, faculties, Reason), default));
        return await Linked(id, username);
    }
    private async Task<Guid> Staff(string given, string username, bool assign)
    {
        var id = (await Use<PeopleService, CreatedPerson>(x => x.CreateAsync(Owner, new(given, "Personal", null, null, [], null, false, true), default))).Id;
        await Use<PeopleService>(async x => await x.AddRoleAsync(Owner, id, new((await x.GetAsync(id, default)).Version, "staff", null, "Docente"), default));
        if (assign) await Use<CourseService, Guid>(x => x.AssignAsync(Owner, course, new(id, TeachingRoles.Lead, Start, Reason), default));
        return await Linked(id, username);
    }
    private async Task<Guid> Publish(Guid author, AnnouncementRequest request)
    {
        var id = await Use<CommunicationService, Guid>(x => x.SaveAsync(author, null, request, default));
        var version = (await Use<CommunicationService, AnnouncementView[]>(x => x.AuthoredAsync(author, default))).Single(a => a.Id == id).Version;
        await Use<CommunicationService>(x => x.SetStatusAsync(author, id, AnnouncementStatus.Published, version, default));
        return id;
    }
    private Task<FeedItem[]> Feed(Guid user) => Use<CommunicationService, FeedItem[]>(x => x.FeedAsync(user, default));

    [Fact]
    public async Task Audiences_and_the_communications_faculty_decide_who_reads_a_notice()
    {
        var families = await Publish(Owner, new("Reunión de padres", "Viernes a las 18:00.", AudienceKinds.School, null, null, false, true, false));
        var grade1 = await Publish(Owner, new("Excursión", "Primero secundaria visita el museo.", AudienceKinds.Grade, year, grade, true, false, false));
        var staffOnly = await Publish(Owner, new("Claustro", "Reunión docente.", AudienceKinds.School, null, null, false, false, true));

        var guardianFeed = await Feed(guardianUser);
        Assert.Equal([families], guardianFeed.Select(x => x.Id).ToArray());
        Assert.Equal(["Eva Avisos C-1"], guardianFeed[0].Students);
        Assert.Empty(await Feed(silentGuardianUser));
        Assert.Equal([grade1], (await Feed(studentUser)).Select(x => x.Id).ToArray());
        Assert.Empty(await Feed(outsiderUser));
        Assert.Contains(await Feed(teacherUser), x => x.Id == staffOnly);
        Assert.DoesNotContain(await Feed(studentUser), x => x.Id == staffOnly);
    }

    [Fact]
    public async Task Teachers_publish_only_in_their_courses_and_reads_are_tracked()
    {
        Assert.Equal("permission-denied", (await Assert.ThrowsAsync<OperationException>(() => Use<CommunicationService, Guid>(x =>
            x.SaveAsync(teacherUser, null, new("General", "Texto", AudienceKinds.School, null, null, true, true, false), default)))).Code);
        Assert.Equal("teaching-denied", (await Assert.ThrowsAsync<OperationException>(() => Use<CommunicationService, Guid>(x =>
            x.SaveAsync(otherTeacherUser, null, new("Artes", "Texto", AudienceKinds.Course, null, course, true, true, false), default)))).Code);

        var draft = await Use<CommunicationService, Guid>(x => x.SaveAsync(teacherUser, null,
            new("Material", "Traer témperas.", AudienceKinds.Course, null, course, true, true, false), default));
        Assert.Empty(await Feed(studentUser));
        var view = (await Use<CommunicationService, AnnouncementView[]>(x => x.AuthoredAsync(teacherUser, default))).Single();
        await Use<CommunicationService>(x => x.SetStatusAsync(teacherUser, draft, AnnouncementStatus.Published, view.Version, default));
        Assert.False(Assert.Single(await Feed(studentUser)).Read);
        Assert.Single(await Feed(guardianUser));
        await Use<CommunicationService>(x => x.MarkReadAsync(studentUser, draft, default));
        await Use<CommunicationService>(x => x.MarkReadAsync(studentUser, draft, default));
        Assert.True(Assert.Single(await Feed(studentUser)).Read);
        await Assert.ThrowsAsync<OperationException>(() => Use<CommunicationService>(x => x.MarkReadAsync(outsiderUser, draft, default)));
        view = (await Use<CommunicationService, AnnouncementView[]>(x => x.AuthoredAsync(teacherUser, default))).Single();
        Assert.Equal(1, view.Reads);
        await Assert.ThrowsAsync<OperationException>(() => Use<CommunicationService, Guid>(x => x.SaveAsync(teacherUser, draft,
            new("Cambio", "Texto", AudienceKinds.Course, null, course, true, true, false, view.Version), default)));
        await Use<CommunicationService>(x => x.SetStatusAsync(teacherUser, draft, AnnouncementStatus.Archived, view.Version, default));
        Assert.Empty(await Feed(studentUser));
    }
}
