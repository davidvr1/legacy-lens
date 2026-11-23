using LegacyLens.Core.Evaluation;

namespace LegacyLens.Core.Tests.Evaluation;

public sealed class LlmMetricsTests
{
    private static SummaryScore Sum(int expected, int matched, int stated, int halluc)
        => new(expected, matched, stated, halluc, [], []);

    private static MigrationScore Mig(bool first, bool after, bool sources)
        => new(first, after, sources, [], []);

    private static LlmCaseResult Result(
        SummaryScore? summary = null,
        string? summaryError = null,
        MigrationScore? migration = null,
        string? migrationError = null,
        double summarySeconds = 0,
        double migrationSeconds = 0)
        => new("c", summary, summaryError, TimeSpan.FromSeconds(summarySeconds), migration, migrationError, TimeSpan.FromSeconds(migrationSeconds));

    // MeanSpread

    [Fact]
    public void MeanSpread_empty()
        => Assert.Equal(new MeanSpread(0, 0, 0), MeanSpread.Of([]));

    [Fact]
    public void MeanSpread_single_value_has_zero_deviation()
        => Assert.Equal(new MeanSpread(0.7, 0, 1), MeanSpread.Of([0.7]));

    [Fact]
    public void MeanSpread_uses_sample_standard_deviation()
    {
        var s = MeanSpread.Of([1, 2, 3]);
        Assert.Equal(2, s.Mean, 10);
        Assert.Equal(1, s.StdDev, 10);
        Assert.Equal(3, s.Count);

        var t = MeanSpread.Of([2, 4, 4, 4, 5, 5, 7, 9]);
        Assert.Equal(5, t.Mean, 10);
        Assert.Equal(Math.Sqrt(32.0 / 7.0), t.StdDev, 10);
    }

    [Fact]
    public void MeanSpread_identical_values_have_zero_deviation()
        => Assert.Equal(0, MeanSpread.Of([0.5, 0.5, 0.5]).StdDev, 10);

    [Fact]
    public void MeanSpread_two_values()
    {
        var s = MeanSpread.Of([0, 1]);
        Assert.Equal(0.5, s.Mean, 10);
        Assert.Equal(Math.Sqrt(0.5), s.StdDev, 10);
    }

    // ForRun

    [Fact]
    public void ForRun_empty_gives_nulls_and_zero()
    {
        var m = LlmMetrics.ForRun([]);
        Assert.Null(m.FactRecall);
        Assert.Null(m.HallucinationRate);
        Assert.Null(m.CompileFirstAttempt);
        Assert.Null(m.CompileAfterRepair);
        Assert.Null(m.SourcesValid);
        Assert.Equal(0, m.MeanCaseSeconds);
        Assert.Equal(0, m.Errors);
    }

    [Fact]
    public void ForRun_summary_only_leaves_migration_metrics_null()
    {
        var m = LlmMetrics.ForRun([Result(Sum(2, 2, 2, 0)), Result(Sum(2, 1, 2, 1))]);
        Assert.Equal(0.75, m.FactRecall!.Value, 10);
        Assert.Equal(0.25, m.HallucinationRate!.Value, 10);
        Assert.Null(m.CompileFirstAttempt);
        Assert.Null(m.CompileAfterRepair);
        Assert.Null(m.SourcesValid);
    }

    [Fact]
    public void ForRun_migration_only_leaves_summary_metrics_null()
    {
        var m = LlmMetrics.ForRun([Result(migration: Mig(true, true, true)), Result(migration: Mig(false, true, false))]);
        Assert.Null(m.FactRecall);
        Assert.Null(m.HallucinationRate);
        Assert.Equal(0.5, m.CompileFirstAttempt!.Value, 10);
        Assert.Equal(1.0, m.CompileAfterRepair!.Value, 10);
        Assert.Equal(0.5, m.SourcesValid!.Value, 10);
    }

    [Fact]
    public void ForRun_summary_error_counts_as_recall_zero_and_is_left_out_of_hallucination_mean()
    {
        var m = LlmMetrics.ForRun([Result(Sum(1, 1, 2, 1)), Result(summaryError: "boom")]);
        Assert.Equal(0.5, m.FactRecall!.Value, 10);
        Assert.Equal(0.5, m.HallucinationRate!.Value, 10);
        Assert.Equal(1, m.Errors);
    }

    [Fact]
    public void ForRun_only_summary_errors_gives_recall_zero_and_null_hallucination()
    {
        var m = LlmMetrics.ForRun([Result(summaryError: "a"), Result(summaryError: "b")]);
        Assert.Equal(0.0, m.FactRecall!.Value);
        Assert.Null(m.HallucinationRate);
        Assert.Equal(2, m.Errors);
    }

    [Fact]
    public void ForRun_migration_error_counts_as_false_for_all_three()
    {
        var m = LlmMetrics.ForRun([Result(migration: Mig(true, true, true)), Result(migrationError: "x"), Result(migration: Mig(false, true, true))]);
        Assert.Equal(1.0 / 3, m.CompileFirstAttempt!.Value, 10);
        Assert.Equal(2.0 / 3, m.CompileAfterRepair!.Value, 10);
        Assert.Equal(2.0 / 3, m.SourcesValid!.Value, 10);
        Assert.Equal(1, m.Errors);
    }

    [Fact]
    public void ForRun_all_migration_errors_gives_zero_not_null()
    {
        var m = LlmMetrics.ForRun([Result(migrationError: "x")]);
        Assert.Equal(0.0, m.CompileAfterRepair!.Value);
        Assert.Equal(0.0, m.CompileFirstAttempt!.Value);
        Assert.Equal(0.0, m.SourcesValid!.Value);
    }

    [Fact]
    public void ForRun_mixed_cases_only_average_cases_that_ran_each_task()
    {
        var m = LlmMetrics.ForRun([Result(Sum(1, 1, 0, 0)), Result(migration: Mig(true, true, true))]);
        Assert.Equal(1.0, m.FactRecall!.Value);
        Assert.Equal(1.0, m.CompileAfterRepair!.Value);
    }

    [Fact]
    public void ForRun_mean_case_seconds_is_mean_of_summary_plus_migration()
    {
        var m = LlmMetrics.ForRun([Result(Sum(1, 1, 0, 0), summarySeconds: 2, migrationSeconds: 4), Result(Sum(1, 1, 0, 0), summarySeconds: 1, migrationSeconds: 1)]);
        Assert.Equal(4.0, m.MeanCaseSeconds, 10);
    }

    [Fact]
    public void ForRun_errors_sum_summary_and_migration_errors()
    {
        var m = LlmMetrics.ForRun([Result(summaryError: "a", migrationError: "b"), Result(migrationError: "c"), Result(Sum(1, 1, 0, 0))]);
        Assert.Equal(3, m.Errors);
    }

    // MeetsTargets

    [Theory]
    [InlineData(0.9, 0.05, 0.8, true)]
    [InlineData(1.0, 0.0, 1.0, true)]
    [InlineData(0.89, 0.05, 0.8, false)]
    [InlineData(0.9, 0.06, 0.8, false)]
    [InlineData(0.9, 0.05, 0.79, false)]
    public void MeetsTargets_boundaries(double recall, double halluc, double compile, bool expected)
        => Assert.Equal(expected, LlmMetrics.MeetsTargets(recall, halluc, compile));

    [Fact]
    public void MeetsTargets_null_values_are_not_checked()
    {
        Assert.True(LlmMetrics.MeetsTargets(null, null, null));
        Assert.True(LlmMetrics.MeetsTargets(0.95, 0.0, null));
        Assert.True(LlmMetrics.MeetsTargets(null, null, 0.9));
        Assert.False(LlmMetrics.MeetsTargets(null, 0.5, null));
        Assert.False(LlmMetrics.MeetsTargets(0.1, null, null));
        Assert.False(LlmMetrics.MeetsTargets(null, null, 0.1));
    }

    [Fact]
    public void Target_constants_match_contract()
    {
        Assert.Equal(0.9, LlmMetrics.TargetFactRecall);
        Assert.Equal(0.05, LlmMetrics.TargetHallucinationRate);
        Assert.Equal(0.8, LlmMetrics.TargetCompileAfterRepair);
    }
}
