using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Tests;

public sealed class CourseQuerySqlTests : FamilyRegistrationTestBase
{
    private readonly Reads reads = new();
    protected override void ConfigureServices(IServiceCollection services) =>
        services.AddDbContext<SchoolDbContext>(o => o.AddInterceptors(reads));

    [Fact]
    public async Task Course_queries_stay_constant_as_subjects_grow_and_counts_respect_grade_section_and_year()
    {
        await Register(Request());
        var sectionB = await Use<AcademicService, Guid>(s => s.SaveSectionAsync(Owner, Year, null, new(Grade, "B", null, 10), default));
        var subject = await Use<CourseService, Guid>(s => s.SaveSubjectAsync(Owner, null, new("Q-0", "Materia 0"), default));
        await Use<CourseService, int>(s => s.CreateCoursesAsync(Owner, Year, new(Grade, null, [subject]), default));
        reads.Count = 0;
        Assert.Equal(2, Assert.Single(await Use<CourseService, CourseView[]>(s => s.CoursesAsync(Year, null, default))).Students);
        var one = reads.Count;
        for (var i = 1; i <= 8; i++)
        {
            var next = await Use<CourseService, Guid>(s => s.SaveSubjectAsync(Owner, null, new($"Q-{i}", $"Materia {i}"), default));
            await Use<CourseService, int>(s => s.CreateCoursesAsync(Owner, Year, new(Grade, i % 2 == 0 ? Section : sectionB, [next]), default));
        }
        reads.Count = 0;
        var courses = await Use<CourseService, CourseView[]>(s => s.CoursesAsync(Year, null, default));
        Assert.Equal(one, reads.Count); Assert.InRange(reads.Count, 1, 5); Assert.Equal(9, courses.Length);
        Assert.All(courses, c => Assert.Equal(c.SectionId == sectionB ? 0 : 2, c.Students));
        Assert.Empty(await Use<CourseService, CourseView[]>(s => s.CoursesAsync(Guid.NewGuid(), null, default)));
    }

    private sealed class Reads : DbCommandInterceptor
    {
        public int Count;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Count);
            return ValueTask.FromResult(result);
        }
    }
}
