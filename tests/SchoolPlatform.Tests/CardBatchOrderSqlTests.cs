using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Gate;
using SchoolPlatform.Host.People;

namespace SchoolPlatform.Tests;

/// <summary>Cards of a whole grade print in classroom order: section, then family and given names.</summary>
public sealed class CardBatchOrderSqlTests : SqlTestBase
{
    [Fact]
    public async Task Grade_cards_are_ordered_by_section_and_name()
    {
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30);
        var year = await Use<AcademicService, Guid>(x => x.CreateYearAsync(Owner, new("Carnets", start, start.AddDays(300)), default));
        await Use<AcademicService>(async x => await x.SetYearStatusAsync(Owner, year,
            new((await x.YearAsync(year, default)).Year.Version, YearStatus.EnrollmentOpen, Reason), default));
        var grade = await Use<AcademicService, Guid>(x => x.SaveGradeAsync(Owner, null, new("4P", "Cuarto primaria", "Primaria", 1), default));
        var a = await Use<AcademicService, Guid>(x => x.SaveSectionAsync(Owner, year, null, new(grade, "A", null, null), default));
        var b = await Use<AcademicService, Guid>(x => x.SaveSectionAsync(Owner, year, null, new(grade, "B", null, null), default));
        var expected = new List<Guid>();
        // Created out of order on purpose.
        foreach (var (given, family, section, code) in new[]
                 {
                     ("Zoe", "Barrios", b, "C-1"), ("Ana", "Méndez", a, "C-2"), ("Luis", "Álvarez", b, "C-3"),
                     ("Rosa", "Álvarez", a, "C-4"), ("Ana", "Álvarez", a, "C-5"),
                 })
        {
            var id = (await Use<PeopleService, CreatedPerson>(x => x.CreateAsync(Owner, new(given, family, null, null, [], code, false, true), default))).Id;
            await Use<EnrollmentService, Guid>(x => x.EnrollAsync(Owner, new(id, year, grade, section, start, null), default));
            expected.Add(id);
        }
        var (ids, _) = await Use<CardService, (Guid[], DateOnly)>(x => x.BatchAsync(new(year, grade, null), default));
        // A: Ana Álvarez, Rosa Álvarez, Ana Méndez · B: Luis Álvarez, Zoe Barrios
        Assert.Equal([expected[4], expected[3], expected[1], expected[2], expected[0]], ids);
    }
}
