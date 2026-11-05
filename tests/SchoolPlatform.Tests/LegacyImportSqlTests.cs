using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SchoolPlatform.Host.Branding;
using SchoolPlatform.Host.LegacyImport;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Tests;

public sealed class LegacyImportSqlTests : SqlTestBase
{
    // Synthetic staging only: invented names and codes, never data from the legacy system.
    private byte[] Staging(Guid? schoolId = null)
    {
        var school = schoolId ?? App.Services.GetRequiredService<IOptions<InstitutionOptions>>().Value.SchoolId;
        var file = new
        {
            manifest = new { source = "sys-escolar", schoolId = school, scope = "students-guardians", exportedAt = "2026-11-01T10:00:00Z" },
            students = new object[]
            {
                new { legacyId = "s1", givenNames = "Ana", familyNames = "Prueba Uno", birthDate = "2014-05-02", studentCode = "LEG-1", parentLegacyId = "p1", phone = "+502 5555-0001", email = (string?)null },
                new { legacyId = "s2", givenNames = "Beto", familyNames = "Prueba Uno", birthDate = (string?)null, studentCode = "LEG-2", parentLegacyId = "p1", phone = (string?)null, email = (string?)null },
                new { legacyId = "s3", givenNames = "Ciro", familyNames = "Prueba Dos", birthDate = "2013-01-01", studentCode = "LEG-3", parentLegacyId = "p404", phone = (string?)null, email = (string?)null },
                new { legacyId = "s4", givenNames = (string?)null, familyNames = "Sin Nombre", birthDate = (string?)null, studentCode = "LEG-4", parentLegacyId = (string?)null, phone = (string?)null, email = (string?)null },
                new { legacyId = "s5", givenNames = "Dina", familyNames = "Prueba Tres", birthDate = "1890-01-01", studentCode = "LEG-5", parentLegacyId = (string?)null, phone = (string?)null, email = (string?)null },
                new { legacyId = "s6", givenNames = "Eva", familyNames = "Prueba Cuatro", birthDate = (string?)null, studentCode = "LEG-1", parentLegacyId = (string?)null, phone = (string?)null, email = (string?)null },
            },
            guardians = new object[]
            {
                new { legacyId = "p1", givenNames = "Gloria", familyNames = "Prueba Uno", phone = "+502 5555-0100", email = "gloria@example.test", relationship = "Mother" },
                new { legacyId = "p9", givenNames = "Hugo", familyNames = "Sin Hijos", phone = (string?)null, email = (string?)null, relationship = (string?)null },
            }
        };
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(file));
    }

    private Task<ImportPlan> Run(byte[] content, bool apply, string? sha = null) =>
        Use<LegacyPeopleImporter, ImportPlan>(x => x.RunAsync(content, apply, sha, apply ? OwnerName : null, default));

    [Fact]
    public async Task Plan_reports_incidents_and_apply_is_exact_idempotent_and_grants_no_access()
    {
        var content = Staging();
        var plan = await Run(content, apply: false);
        Assert.False(plan.Applied);
        Assert.Equal((3, 1, 2), (plan.StudentsToCreate, plan.GuardiansToCreate, plan.RelationshipsToCreate));
        var codes = plan.Incidents.Select(x => $"{x.LegacyId}:{x.Code}").ToHashSet();
        Assert.Contains("s3:orphan-parent", codes);
        Assert.Contains("s4:names-review", codes);
        Assert.Contains("s5:birth-review", codes);
        Assert.Contains("s6:code-collision", codes);
        Assert.Contains("p9:no-student", codes);
        Assert.Equal(0, await Use<SchoolDbContext, int>(db => db.Persons.CountAsync()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Run(content, apply: true, sha: new string('0', 64)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Run(Staging(Guid.NewGuid()), apply: false));

        var applied = await Run(content, apply: true, sha: plan.Sha256);
        Assert.True(applied.Applied);
        Assert.Equal(4, await Use<SchoolDbContext, int>(db => db.Persons.CountAsync()));
        var relationships = await Use<SchoolDbContext, int>(db => db.Relationships.CountAsync());
        Assert.Equal(2, relationships);
        Assert.Equal(0, await Use<SchoolDbContext, int>(db => db.RelationshipFaculties.CountAsync()));
        Assert.Equal(0, await Use<SchoolDbContext, int>(db => db.AccountLinks.CountAsync()));
        Assert.Null(await Use<SchoolDbContext, DateOnly?>(db => db.Persons.Where(p => p.GivenNames == "Beto").Select(p => p.BirthDate).SingleAsync()));

        var again = await Run(content, apply: true, sha: plan.Sha256);
        Assert.Equal((0, 0, 0, 4), (again.StudentsToCreate, again.GuardiansToCreate, again.RelationshipsToCreate, again.AlreadyImported));
        Assert.Equal(4, await Use<SchoolDbContext, int>(db => db.Persons.CountAsync()));
        Assert.Equal(4, await Use<SchoolDbContext, int>(db => db.LegacyIdentities.CountAsync()));
        Assert.Contains(await Use<SchoolDbContext, string[]>(db => db.AuditEvents.Select(x => x.Action).ToArrayAsync()), x => x == "LegacyPeopleImported");
    }
}
