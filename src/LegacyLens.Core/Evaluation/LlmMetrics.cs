using LegacyLens.Core.Model;

namespace LegacyLens.Core.Evaluation;

/// <summary>Turns the case results of one run into metrics, and checks them against the targets.</summary>
public static class LlmMetrics
{
    public const double TargetFactRecall = 0.9;

    public const double TargetHallucinationRate = 0.05;

    public const double TargetCompileAfterRepair = 0.8;

    /// <summary>
    /// Summary: mean over cases where the summary was run. A case with SummaryError counts as fact recall 0 and is left
    /// out of the hallucination mean (hallucination null when no case has a summary score).
    /// Migration: share over cases where migration was run; a case with MigrationError counts as false for all three.
    /// MeanCaseSeconds: mean of (SummaryDuration + MigrationDuration) in seconds. Errors: SummaryError + MigrationError count.
    /// </summary>
    public static LlmRunMetrics ForRun(IReadOnlyList<LlmCaseResult> results)
    {
        var summaryRuns = results.Where(r => r.Summary is not null || r.SummaryError is not null).ToList();
        var migrationRuns = results.Where(r => r.Migration is not null || r.MigrationError is not null).ToList();
        var scores = summaryRuns.Select(r => r.Summary).OfType<SummaryScore>().ToList();

        return new LlmRunMetrics(
            FactRecall: Mean(summaryRuns.Select(r => r.Summary?.FactRecall ?? 0)),
            HallucinationRate: Mean(scores.Select(s => s.HallucinationRate)),
            CompileFirstAttempt: Share(migrationRuns, r => r.Migration?.CompiledFirstAttempt == true),
            CompileAfterRepair: Share(migrationRuns, r => r.Migration?.CompiledAfterRepair == true),
            SourcesValid: Share(migrationRuns, r => r.Migration?.SourcesValid == true),
            MeanCaseSeconds: results.Count == 0 ? 0 : results.Average(r => (r.SummaryDuration + r.MigrationDuration).TotalSeconds),
            Errors: results.Count(r => r.SummaryError is not null) + results.Count(r => r.MigrationError is not null));
    }

    /// <summary>True when recall >= 0.9, hallucination <= 0.05 and compile after repair >= 0.8. A null value is not checked.</summary>
    public static bool MeetsTargets(double? factRecall, double? hallucinationRate, double? compileAfterRepair)
    {
        return (factRecall is null || factRecall >= TargetFactRecall)
            && (hallucinationRate is null || hallucinationRate <= TargetHallucinationRate)
            && (compileAfterRepair is null || compileAfterRepair >= TargetCompileAfterRepair);
    }

    private static double? Mean(IEnumerable<double> values)
    {
        var list = values.ToList();
        return list.Count == 0 ? null : list.Average();
    }

    private static double? Share(IReadOnlyList<LlmCaseResult> runs, Func<LlmCaseResult, bool> predicate)
    {
        return runs.Count == 0 ? null : (double)runs.Count(predicate) / runs.Count;
    }
}
