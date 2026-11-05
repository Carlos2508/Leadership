using SchoolPlatform.Assessment;

namespace SchoolPlatform.Tests;

public sealed class AssessmentTests
{
    [Theory]
    [InlineData(60, 60, 60, 60, 60, SubjectOutcome.Passed)]
    [InlineData(100, 50, 50, 40, 60, SubjectOutcome.Passed)]
    [InlineData(59, 60, 60, 60, 59.75, SubjectOutcome.RequiresRecovery)]
    [InlineData(0, 0, 0, 0, 0, SubjectOutcome.RequiresRecovery)]
    public void Four_periods_determine_each_subject(decimal a, decimal b, decimal c, decimal d, decimal expected, SubjectOutcome outcome)
    {
        var result = AnnualSubjectResult.Calculate([a, b, c, d]);
        Assert.Equal(expected, result.OrdinaryScore);
        Assert.Equal(expected, result.EffectiveScore);
        Assert.Equal(outcome, result.Outcome);
    }

    [Fact]
    public void Pending_is_not_a_zero()
    {
        var result = AnnualSubjectResult.Calculate([100, null, 80, 70]);
        Assert.Null(result.EffectiveScore);
        Assert.Equal(SubjectOutcome.Pending, result.Outcome);
    }

    [Theory]
    [InlineData(80, SubjectOutcome.Passed)]
    [InlineData(40, SubjectOutcome.FailedRecovery)]
    public void Explicit_resolution_replaces_effective_score_and_preserves_ordinary(decimal score, SubjectOutcome outcome)
    {
        var resolution = new AuthorizedRecovery(score, Guid.NewGuid());
        var result = AnnualSubjectResult.Calculate([50, 50, 50, 50], resolution);
        Assert.Equal(50, result.OrdinaryScore);
        Assert.Equal(score, result.EffectiveScore);
        Assert.Equal(outcome, result.Outcome);
        Assert.Equal(resolution.ResolutionId, result.RecoveryResolutionId);
    }

    [Fact]
    public void Invalid_or_incomplete_grades_cannot_generate_a_recovery_result()
    {
        Assert.Throws<ArgumentException>(() => AnnualSubjectResult.Calculate([100, 90, 80]));
        Assert.Throws<ArgumentOutOfRangeException>(() => AnnualSubjectResult.Calculate([101, 90, 80, 70]));
        Assert.Throws<ArgumentOutOfRangeException>(() => AnnualSubjectResult.Calculate([-1, 90, 80, 70]));
        Assert.Throws<InvalidOperationException>(() => AnnualSubjectResult.Calculate([null, 50, 50, 50], new(60, Guid.NewGuid())));
        Assert.Throws<InvalidOperationException>(() => AnnualSubjectResult.Calculate([90, 90, 90, 90], new(60, Guid.NewGuid())));
        Assert.Throws<ArgumentException>(() => AnnualSubjectResult.Calculate([50, 50, 50, 50], new(60, Guid.Empty)));
    }
}
