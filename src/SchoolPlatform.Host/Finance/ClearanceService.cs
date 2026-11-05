using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Host.Finance;

/// <summary>
/// Academic clearance against a cutoff chosen by the school. Evaluated from confirmed SQL data on every call:
/// required = obligations of clearance concepts (recurring ones in billing periods 1..N of the cutoff, one-off ones due
/// up to the last required period). Missing calendar or missing obligations yield PendingFinancialSetup, never "clear".
/// </summary>
public sealed class ClearanceService(SchoolDbContext db)
{
    public async Task<ClearanceResult> CheckAsync(Guid studentId, Guid cutoffId, CancellationToken ct)
    {
        var cutoff = await Cutoff(cutoffId, ct);
        var report = await EvaluateAsync(cutoff, [studentId], ct);
        return report.Single();
    }

    public async Task<ClearanceReport> ReportAsync(Guid cutoffId, Guid? gradeId, string? status, CancellationToken ct)
    {
        var cutoff = await Cutoff(cutoffId, ct);
        var enrolled = await (from e in db.Enrollments.AsNoTracking()
                              where e.AcademicYearId == cutoff.AcademicYearId && e.Status != EnrollmentStatus.Cancelled
                              select e.StudentId).ToArrayAsync(ct);
        var results = await EvaluateAsync(cutoff, enrolled, ct);
        var info = await (from e in db.Enrollments.AsNoTracking()
                          where e.AcademicYearId == cutoff.AcademicYearId && e.Status != EnrollmentStatus.Cancelled
                          join person in db.Persons on e.StudentId equals person.Id
                          join student in db.Students on e.StudentId equals student.PersonId
                          select new
                          {
                              e.StudentId, person.GivenNames, person.FamilyNames, person.PreferredName, student.StudentCode,
                              Placement = db.Placements.Where(p => p.EnrollmentId == e.Id).OrderBy(p => p.EndsOn == null ? 0 : 1)
                                  .ThenByDescending(p => p.StartsOn).Select(p => new
                                  {
                                      p.GradeLevelId,
                                      Grade = db.Grades.Where(g => g.Id == p.GradeLevelId).Select(g => g.Name).First(),
                                      Section = db.Sections.Where(s => s.Id == p.SectionId).Select(s => s.Name).FirstOrDefault()
                                  }).FirstOrDefault()
                          }).ToDictionaryAsync(x => x.StudentId, ct);
        var rows = results.Select(r =>
        {
            var x = info[r.StudentId];
            return new { Row = new ClearanceRow(r.StudentId, PeopleService.Display(x.GivenNames, x.FamilyNames, x.PreferredName), x.StudentCode,
                x.Placement?.Grade, x.Placement?.Section, r.Status, r.Pending.Sum(p => p.Balance), r.Missing.Length), Grade = x.Placement?.GradeLevelId };
        }).Where(x => gradeId == null || x.Grade == gradeId).Select(x => x.Row).ToArray();
        var filtered = rows.Where(x => string.IsNullOrEmpty(status) || status == "all" || x.Status == status)
            .OrderBy(x => x.Status == ClearanceStatus.Clear).ThenBy(x => x.StudentName).ToArray();
        return new(cutoff.Id, cutoff.Name, rows.Count(x => x.Status == ClearanceStatus.Clear), rows.Count(x => x.Status == ClearanceStatus.Blocked),
            rows.Count(x => x.Status == ClearanceStatus.PendingFinancialSetup), filtered);
    }

    private async Task<ClearanceCutoff> Cutoff(Guid id, CancellationToken ct) =>
        await db.ClearanceCutoffs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw OperationException.NotFound("No encontramos ese corte.");

    private async Task<ClearanceResult[]> EvaluateAsync(ClearanceCutoff cutoff, Guid[] studentIds, CancellationToken ct)
    {
        var periods = await db.BillingPeriods.AsNoTracking().Where(x => x.AcademicYearId == cutoff.AcademicYearId && x.Sequence <= cutoff.ThroughSequence)
            .OrderBy(x => x.Sequence).ToArrayAsync(ct);
        var calendarComplete = periods.Length == cutoff.ThroughSequence;
        var lastDue = periods.Length == 0 ? (DateOnly?)null : periods[^1].DueOn;
        var concepts = await db.FeeConcepts.AsNoTracking().Where(x => x.CountsForClearance).ToDictionaryAsync(x => x.Id, ct);
        var recurring = concepts.Values.Any(x => x.IsRecurring);
        var conceptIds = concepts.Keys.ToArray();
        var enrollments = await db.Enrollments.AsNoTracking()
            .Where(x => x.AcademicYearId == cutoff.AcademicYearId && studentIds.Contains(x.StudentId) && x.Status != EnrollmentStatus.Cancelled)
            .ToDictionaryAsync(x => x.StudentId, x => x.Id, ct);
        var enrollmentIds = enrollments.Values.ToArray();
        var periodIds = periods.Select(x => x.Id).ToArray();
        var charges = await db.Charges.AsNoTracking()
            .Where(x => enrollmentIds.Contains(x.EnrollmentId) && x.VoidedAt == null && conceptIds.Contains(x.ConceptId) &&
                ((x.BillingPeriodId != null && periodIds.Contains(x.BillingPeriodId.Value)) ||
                 (x.BillingPeriodId == null && lastDue != null && x.DueOn <= lastDue)))
            .Select(x => new { x.Id, x.EnrollmentId, x.BillingPeriodId, x.Description, x.DueOn }).ToArrayAsync(ct);
        var balances = await ChargeService.Balances(db, charges.Select(x => x.Id).ToArray(), ct);

        return studentIds.Select(studentId =>
        {
            if (!enrollments.TryGetValue(studentId, out var enrollmentId))
                return new ClearanceResult(studentId, cutoff.Id, cutoff.Name, ClearanceStatus.NotEnrolled, [], []);
            var own = charges.Where(x => x.EnrollmentId == enrollmentId).ToArray();
            var missing = new List<string>();
            if (!calendarComplete) missing.Add("Calendario de cuotas incompleto para este corte");
            if (!recurring) missing.Add("Ningún concepto recurrente cuenta para solvencia");
            // Every required billing period must have at least one clearance obligation issued (or waived through an adjustment).
            else missing.AddRange(periods.Where(p => own.All(c => c.BillingPeriodId != p.Id)).Select(p => p.Label));
            var pending = own.Where(x => balances[x.Id] > 0)
                .Select(x => new PendingItem(x.Id, x.Description, balances[x.Id], x.DueOn)).OrderBy(x => x.DueOn).ToArray();
            var status = missing.Count > 0 ? ClearanceStatus.PendingFinancialSetup : pending.Length > 0 ? ClearanceStatus.Blocked : ClearanceStatus.Clear;
            return new ClearanceResult(studentId, cutoff.Id, cutoff.Name, status, pending, missing.ToArray());
        }).ToArray();
    }
}
