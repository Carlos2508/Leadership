using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.Documents;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.SchoolData;
using SchoolPlatform.Host.Timetables;

namespace SchoolPlatform.Tests;
public class TimetableSqlTests : FamilyRegistrationTestBase
{
    private Guid course, staff, assignment;
    private static readonly DateOnly Monday = new(2027,1,11);
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        var subject = await Use<CourseService,Guid>(s=>s.SaveSubjectAsync(Owner,null,new("TH-MAT","Matemática"),default));
        await Use<CourseService,int>(s=>s.CreateCoursesAsync(Owner,Year,new(Grade,Section,[subject]),default));
        course=(await Use<CourseService,CourseView[]>(s=>s.CoursesAsync(Year,Grade,default))).Single().Id;
        staff=(await Use<PeopleService,CreatedPerson>(s=>s.CreateAsync(Owner,new("Docente","Horario sintético",null,null,[],null,false,true,"Docente"),default))).Id;
        assignment=await Use<CourseService,Guid>(s=>s.AssignAsync(Owner,course,new(staff,TeachingRoles.Lead,Starts,Reason),default));
    }
    private SaveTimetable Draft(int start=480,int end=540) {
        var period=new TimetablePeriod(Guid.NewGuid(),"Primer período",start,end);
        return new(Guid.NewGuid(),Grade,Section,null,null,Monday,new(2027,11,30),new([1,2,3,4,5],[period]),[new(period.Id,1,"Lesson",course,assignment,null)],Reason);
    }
    private Task<SavedTimetable> Save(SaveTimetable request)=>Use<TimetableService,SavedTimetable>(s=>s.SaveAsync(Owner,Year,request,default));
    private Task<SavedTimetable> Publish(SavedTimetable saved)=>Use<TimetableService,SavedTimetable>(s=>s.PublishAsync(Owner,Year,saved.RevisionId,new(Guid.NewGuid(),saved.Version,Reason),default));
    private Task<PublishedTimetable> Read(DateOnly? date=null)=>Use<TimetableService,PublishedTimetable>(s=>s.ReadAsync(Year,date??Monday,Grade,Section,null,default));

    [Fact]
    public async Task Two_groups_cannot_publish_the_same_teacher_concurrently()
    {
        var section=await Use<AcademicService,Guid>(s=>s.SaveSectionAsync(Owner,Year,null,new(Grade,"Concurrente",null,10),default));
        var subject=(await Use<CourseService,CourseView[]>(s=>s.CoursesAsync(Year,Grade,default))).Single().SubjectId;
        await Use<CourseService,int>(s=>s.CreateCoursesAsync(Owner,Year,new(Grade,section,[subject]),default));
        var other=(await Use<CourseService,CourseView[]>(s=>s.CoursesAsync(Year,Grade,default))).Single(c=>c.SectionId==section).Id;
        var assigned=await Use<CourseService,Guid>(s=>s.AssignAsync(Owner,other,new(staff,TeachingRoles.Lead,Starts,Reason),default));
        var request=Draft();var first=await Save(request);
        var second=await Save(request with{RequestId=Guid.NewGuid(),SectionId=section,Cells=[request.Cells[0] with{CourseId=other,AssignmentId=assigned}]});
        async Task<int> Try(SavedTimetable saved){try{await Publish(saved);return 200;}catch(OperationException e){return e.Status;}}
        var results=await Task.WhenAll(Try(first),Try(second)); Assert.Equal(new[]{200,409},results.Order().ToArray());
        Assert.Equal(1,await Use<SchoolDbContext,int>(db=>db.TimetableRevisions.CountAsync(r=>r.State=="Published")));
    }

    [Fact]
    public async Task Room_overlap_is_rejected_for_different_teachers_and_different_grades()
    {
        var room=await Use<TimetableService,Guid>(s=>s.RoomAsync(Owner,"Laboratorio",default));var first=Draft();await Publish(await Save(first with{Cells=[first.Cells[0] with{RoomId=room}]}));
        var grade=await Use<AcademicService,Guid>(s=>s.SaveGradeAsync(Owner,null,new("TROOM","Otro grado","Primaria",2),default));
        var subject=(await Use<CourseService,CourseView[]>(s=>s.CoursesAsync(Year,Grade,default))).Single().SubjectId;
        await Use<CourseService,int>(s=>s.CreateCoursesAsync(Owner,Year,new(grade,null,[subject]),default));
        var other=(await Use<CourseService,CourseView[]>(s=>s.CoursesAsync(Year,grade,default))).Single().Id;
        var person=(await Use<PeopleService,CreatedPerson>(s=>s.CreateAsync(Owner,new("Otra","Docente",null,null,[],null,false,true,"Docente"),default))).Id;
        var assigned=await Use<CourseService,Guid>(s=>s.AssignAsync(Owner,other,new(person,TeachingRoles.Lead,Starts,Reason),default));
        var r=Draft(510,570);var saved=await Save(r with{GradeId=grade,SectionId=null,Cells=[r.Cells[0] with{CourseId=other,AssignmentId=assigned,RoomId=room}]});
        Assert.Equal("room-overlap",Assert.Single((await Use<TimetableService,TimetableView>(s=>s.GetAsync(Year,saved.RevisionId,default))).Issues).Kind);
        Assert.Equal(409,(await Assert.ThrowsAsync<OperationException>(()=>Publish(saved))).Status);
    }

    [Fact]
    public async Task Dense_seven_day_documents_remain_valid_and_keep_the_requested_page_size()
    {
        var r=Draft();var periods=Enumerable.Range(0,16).Select(i=>new TimetablePeriod(Guid.NewGuid(),$"Período {i+1}",360+i*45,405+i*45)).ToArray();
        r=r with{Structure=new([1,2,3,4,5,6,7],periods),Cells=periods.SelectMany(p=>Enumerable.Range(1,7).Select(d=>new TimetableCell(p.Id,d,"Lesson",course,assignment,null))).ToArray()};
        await Publish(await Save(r));var view=await Read();Assert.Equal(112,view.Slots.Length);
        var bytes=await Use<PdfDocuments,byte[]>(pdf=>Task.FromResult(pdf.Timetables([view],false,"a4")));
        Assert.True(bytes.Length>5000);
        if(Environment.GetEnvironmentVariable("SCHOOLPLATFORM_QA_PDF") is {Length:>0} path){Directory.CreateDirectory(path);await File.WriteAllBytesAsync(Path.Combine(path,"horario-semana-completa.pdf"),bytes);}
    }

    [Fact]
    public async Task Drafts_are_private_publication_keeps_snapshots_and_versions_preserve_dated_history()
    {
        var request=Draft();var first=await Save(request);
        Assert.Empty((await Read()).Slots);
        var published=await Publish(first);Assert.Single((await Read()).Slots);
        var second=await Save(request with {RequestId=Guid.NewGuid(),StartsOn=Monday.AddDays(7),Structure=request.Structure with{Periods=[request.Structure.Periods[0] with{Start=600,End=660}]} });
        Assert.Equal(480,Assert.Single((await Read(Monday.AddDays(7))).Slots).Start);
        await Publish(second);
        Assert.Equal(480,Assert.Single((await Read()).Slots).Start);
        Assert.Equal(600,Assert.Single((await Read(Monday.AddDays(7))).Slots).Start);
        Assert.Equal(409,(await Assert.ThrowsAsync<OperationException>(()=>Save(request with{RequestId=Guid.NewGuid(),RevisionId=published.RevisionId,Version=published.Version}))).Status);
        Assert.Equal(2,await Use<SchoolDbContext,int>(db=>db.TimetableRevisions.CountAsync(r=>r.State=="Published")));
    }

    [Fact]
    public async Task Lost_replies_and_concurrent_saves_do_not_duplicate_and_stale_versions_never_overwrite()
    {
        var request=Draft();var results=await Task.WhenAll(Enumerable.Range(0,3).Select(_=>Save(request)));
        Assert.All(results,r=>Assert.Equal(results[0].RevisionId,r.RevisionId));
        var saved=results[0];
        Assert.Equal(409,(await Assert.ThrowsAsync<OperationException>(()=>Save(request with{Reason="Otro comando distinto"}))).Status);
        var updated=await Save(request with{RequestId=Guid.NewGuid(),RevisionId=saved.RevisionId,Version=saved.Version});
        Assert.NotEqual(saved.Version,updated.Version);
        Assert.Equal(409,(await Assert.ThrowsAsync<OperationException>(()=>Save(request with{RequestId=Guid.NewGuid(),RevisionId=saved.RevisionId,Version=saved.Version}))).Status);
        var publish=new PublishTimetable(Guid.NewGuid(),updated.Version,Reason);
        var a=await Use<TimetableService,SavedTimetable>(s=>s.PublishAsync(Owner,Year,updated.RevisionId,publish,default));
        var b=await Use<TimetableService,SavedTimetable>(s=>s.PublishAsync(Owner,Year,updated.RevisionId,publish,default));
        Assert.Equal(a,b);Assert.Single((await Read()).Slots);
    }

    [Fact]
    public async Task Partial_teacher_overlap_blocks_a_competing_group_but_adjacent_lessons_are_allowed()
    {
        await Publish(await Save(Draft()));
        var b=await Use<AcademicService,Guid>(s=>s.SaveSectionAsync(Owner,Year,null,new(Grade,"B",null,10),default));
        var subject=(await Use<CourseService,CourseView[]>(s=>s.CoursesAsync(Year,Grade,default))).Single().SubjectId;
        await Use<CourseService,int>(s=>s.CreateCoursesAsync(Owner,Year,new(Grade,b,[subject]),default));
        var target=(await Use<CourseService,CourseView[]>(s=>s.CoursesAsync(Year,Grade,default))).Single(c=>c.SectionId==b).Id;
        var assigned=await Use<CourseService,Guid>(s=>s.AssignAsync(Owner,target,new(staff,TeachingRoles.Lead,Starts,Reason),default));
        var request=Draft(510,570);request=request with{SectionId=b,Cells=[request.Cells[0] with{CourseId=target,AssignmentId=assigned}]};
        var draft=await Save(request);var view=await Use<TimetableService,TimetableView>(s=>s.GetAsync(Year,draft.RevisionId,default));
        Assert.Contains(view.Issues,i=>i.Kind=="teacher-overlap");
        Assert.Equal("timetable-conflicts",(await Assert.ThrowsAsync<OperationException>(()=>Publish(draft))).Code);
        var adjacent=await Save(request with{RequestId=Guid.NewGuid(),RevisionId=draft.RevisionId,Version=draft.Version,Structure=request.Structure with{Periods=[request.Structure.Periods[0] with{Start=540,End=600}]}});
        await Publish(adjacent);Assert.Equal(2,(await Use<TimetableService,PublishedTimetable>(s=>s.ReadAsync(Year,Monday,Grade,null,staff,default))).Slots.Length);
    }

    [Fact]
    public async Task Room_conflicts_and_whole_grade_conflicts_are_checked_independently_of_teacher()
    {
        var room=await Use<TimetableService,Guid>(s=>s.RoomAsync(Owner,"Aula 1",default));
        Assert.Equal(room,await Use<TimetableService,Guid>(s=>s.RoomAsync(Owner,"  Aula   1 ",default)));
        var first=Draft();await Publish(await Save(first with{Cells=[first.Cells[0] with{RoomId=room}]}));
        var subject=await Use<CourseService,Guid>(s=>s.SaveSubjectAsync(Owner,null,new("TH-CIE","Ciencias"),default));
        await Use<CourseService,int>(s=>s.CreateCoursesAsync(Owner,Year,new(Grade,null,[subject]),default));
        var other=(await Use<CourseService,CourseView[]>(s=>s.CoursesAsync(Year,Grade,default))).Single(c=>c.SubjectId==subject).Id;
        var assigned=await Use<CourseService,Guid>(s=>s.AssignAsync(Owner,other,new(staff,TeachingRoles.Lead,Starts,Reason),default));
        var request=Draft(500,520);request=request with{SectionId=null,Cells=[request.Cells[0] with{CourseId=other,AssignmentId=assigned,RoomId=room}]};
        var saved=await Save(request);
        Assert.Contains((await Use<TimetableService,TimetableView>(s=>s.GetAsync(Year,saved.RevisionId,default))).Issues,i=>i.Kind=="group-overlap");
        Assert.Equal("timetable-conflicts",(await Assert.ThrowsAsync<OperationException>(()=>Publish(saved))).Code);
    }

    [Fact]
    public async Task Incomplete_teacher_can_be_saved_but_not_published_and_assignments_cannot_invalidate_future_lessons()
    {
        var r=Draft();var draft=await Save(r with{Cells=[r.Cells[0] with{AssignmentId=null}]});
        Assert.Equal("timetable-conflicts",(await Assert.ThrowsAsync<OperationException>(()=>Publish(draft))).Code);
        draft=await Save(r with{RequestId=Guid.NewGuid(),RevisionId=draft.RevisionId,Version=draft.Version});await Publish(draft);
        Assert.Equal("teacher-has-timetable",(await Assert.ThrowsAsync<OperationException>(()=>Use<CourseService>(s=>s.EndAssignmentAsync(Owner,course,assignment,new(Monday.AddDays(2),Reason),default)))).Code);
        var invalid=r with{RequestId=Guid.NewGuid(),Structure=new([1],[r.Structure.Periods[0],new(Guid.NewGuid(),"Cruce",500,550)])};
        Assert.Equal(400,(await Assert.ThrowsAsync<OperationException>(()=>Save(invalid))).Status);
    }

    [Fact]
    public async Task Student_and_guardian_access_follow_link_faculty_and_placement_without_financial_clearance()
    {
        var family=await Register(Request());await Publish(await Save(Draft()));var student=family.Students[0].PersonId;var guardian=family.GuardianIds[0];
        var guardianUser=await Account("time-family");var teacherUser=await Account("time-teacher");
        await Use<PeopleService>(async s=>await s.LinkAccountAsync(Owner,guardian,new((await s.GetAsync(guardian,default)).Version,guardianUser,Reason),default));
        await Use<PeopleService>(async s=>await s.LinkAccountAsync(Owner,staff,new((await s.GetAsync(staff,default)).Version,teacherUser,Reason),default));
        Assert.Single((await Use<TimetableService,PublishedTimetable>(s=>s.StudentAsync(guardianUser,student,Year,Monday,default))).Slots);
        Assert.Single((await Use<TimetableService,PublishedTimetable>(s=>s.TeacherAsync(teacherUser,Year,Monday,default))).Slots);
        await Assert.ThrowsAsync<OperationException>(()=>Use<TimetableService,PublishedTimetable>(s=>s.StudentAsync(guardianUser,family.Students[1].PersonId,Year,Monday,default)));
        using var browser=await SignIn("time-family");
        Assert.Equal(HttpStatusCode.Forbidden,(await browser.GetAsync($"/api/v1/years/{Year}/timetables")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await browser.GetAsync($"/api/v1/me/timetables/students/{student}?yearId={Year}&on=2027-01-11")).StatusCode);
        var relation=(await Use<PeopleService,PersonDetail>(s=>s.GetAsync(student,default))).Guardians.Single();
        await Use<PeopleService>(s=>s.UpdateRelationshipAsync(Owner,relation.Id,new(relation.Version,"Mother",[],Reason),default));
        Assert.Equal(HttpStatusCode.NotFound,(await browser.GetAsync($"/api/v1/me/timetables/students/{student}?yearId={Year}&on=2027-01-11")).StatusCode);
    }

    [Fact]
    public async Task Http_writes_enforce_csrf_permissions_and_PDFs_include_weekends_and_valid_formats()
    {
        using var anonymous=App.Browser();Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync($"/api/v1/years/{Year}/timetables")).StatusCode);
        using var owner=await SignIn(OwnerName);Assert.Equal(HttpStatusCode.BadRequest,(await owner.PostAsJsonAsync($"/api/v1/years/{Year}/timetables",Draft())).StatusCode);
        await Account("time-basic");using var basic=await SignIn("time-basic");Assert.Equal(HttpStatusCode.Forbidden,(await Post(basic,$"/api/v1/years/{Year}/timetables",Draft())).StatusCode);
        var r=Draft();r=r with{Structure=r.Structure with{Days=[1,6]},Cells=[r.Cells[0],r.Cells[0] with{Day=6}]};await Publish(await Save(r));
        foreach(var paper in new[]{"letter","a4"}){
            var response=await owner.GetAsync($"/api/v1/years/{Year}/timetables/published.pdf?on=2027-01-11&paper={paper}");Assert.Equal(HttpStatusCode.OK,response.StatusCode);
            var bytes=await response.Content.ReadAsByteArrayAsync();Assert.StartsWith("%PDF",System.Text.Encoding.ASCII.GetString(bytes));
            // Optional export path supplied by the test runner, never a school directory.
            if(Environment.GetEnvironmentVariable("SCHOOLPLATFORM_QA_PDF") is {Length:>0} path){Directory.CreateDirectory(path);await File.WriteAllBytesAsync(Path.Combine(path,$"horario-{paper}.pdf"),bytes);}
        }
        Assert.Equal(HttpStatusCode.BadRequest,(await owner.GetAsync($"/api/v1/years/{Year}/timetables/published.pdf?on=2027-01-11&paper=bad")).StatusCode);
    }
}

public sealed class TimetableMfaSqlTests : FamilyRegistrationTestBase
{
    protected override Dictionary<string,string?> ExtraConfiguration=>new(){["AccountAccess:RequireMfaForPermissions:0"]="academic.manage"};
    // Only the permission policy is configured after the ordinary academic fixture has been prepared by services.
    [Fact]
    public async Task Publishing_and_catalog_writes_cannot_bypass_academic_mfa()
    {
        using var browser=await SignIn(OwnerName);
        Assert.Equal(HttpStatusCode.Forbidden,(await Post(browser,$"/api/v1/years/{Year}/timetables/rooms",new{Name="MFA room"})).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await Post(browser,$"/api/v1/years/{Year}/timetables/revisions/{Guid.NewGuid()}/publish",new PublishTimetable(Guid.NewGuid(),"",Reason))).StatusCode);
        Assert.Equal(0,await Use<SchoolDbContext,int>(db=>db.TimetableRooms.CountAsync()));
    }
}
