namespace SchoolPlatform.Assessment;

public enum SubjectOutcome { Pending, Passed, RequiresRecovery, FailedRecovery }

// No rounding or recovery publication policy is assumed. Resolution is an explicit, approved input.
public sealed record AuthorizedRecovery(decimal Score, Guid ResolutionId);

public sealed record AnnualSubjectResult(decimal? OrdinaryScore, decimal? EffectiveScore, SubjectOutcome Outcome, Guid? RecoveryResolutionId)
{
    public static AnnualSubjectResult Calculate(IReadOnlyList<decimal?> periods, AuthorizedRecovery? recovery = null)
    {
        ArgumentNullException.ThrowIfNull(periods);
        if (periods.Count != 4) throw new ArgumentException("Se requieren exactamente cuatro bimestres.", nameof(periods));
        if (periods.Any(x => x is < 0 or > 100)) throw new ArgumentOutOfRangeException(nameof(periods));
        if (periods.Any(x => x is null))
        {
            if (recovery is not null) throw new InvalidOperationException("No puede resolver recuperación con bimestres pendientes.");
            return new(null, null, SubjectOutcome.Pending, null);
        }
        var ordinary = periods.Sum(x => x!.Value) / 4m;
        if (recovery is null)
            return new(ordinary, ordinary, ordinary >= 60m ? SubjectOutcome.Passed : SubjectOutcome.RequiresRecovery, null);
        if (ordinary >= 60m) throw new InvalidOperationException("La materia no requiere recuperación.");
        if (recovery.Score is < 0 or > 100 || recovery.ResolutionId == Guid.Empty)
            throw new ArgumentException("La recuperación requiere nota válida y resolución autorizada.", nameof(recovery));
        return new(ordinary, recovery.Score, recovery.Score >= 60m ? SubjectOutcome.Passed : SubjectOutcome.FailedRecovery, recovery.ResolutionId);
    }
}
