using System.Globalization;
using LegacyLens.Core.Evaluation;
using LegacyLens.Core.Llm;
using LegacyLens.Core.Model;
using LegacyLens.Core.Retrieval;

namespace LegacyLens.Evals;

/// <summary>One grid cell: the settings that were run together.</summary>
internal sealed record LlmConfig(string Model, double Temperature, double TopP, PromptVariant Variant, int TopK, Bm25Parameters Bm25);

/// <summary>The result of one case in one run. The seconds are the summary and the migration time.</summary>
internal sealed record LlmCaseReport(
    string CaseId,
    SummaryScore? Summary,
    string? SummaryError,
    double SummarySeconds,
    MigrationScore? Migration,
    string? MigrationError,
    double MigrationSeconds,
    CodeSummary? StatedSummary,
    MigrationProposal? Proposal,
    MigrationTrace? MigrationTrace)
{
    public static LlmCaseReport From(LlmCaseResult result)
    {
        return new LlmCaseReport(
            result.CaseId,
            result.Summary,
            result.SummaryError,
            result.SummaryDuration.TotalSeconds,
            result.Migration,
            result.MigrationError,
            result.MigrationDuration.TotalSeconds,
            result.StatedSummary,
            result.Proposal,
            result.MigrationTrace);
    }
}

/// <summary>The metrics of one run of a config (every case once), and the case results behind them.</summary>
internal sealed record LlmRunReport(int Run, LlmRunMetrics Metrics, IReadOnlyList<LlmCaseReport> Cases);

/// <summary>Mean and sample spread over the runs of a config. A metric is null when its task was not run.</summary>
internal sealed record LlmSpread(
    MeanSpread? FactRecall,
    MeanSpread? HallucinationRate,
    MeanSpread? CompileFirstAttempt,
    MeanSpread? CompileAfterRepair,
    MeanSpread? SourcesValid,
    MeanSpread MeanCaseSeconds,
    MeanSpread Errors);

/// <summary>The runs of one config, its mean and spread, and whether the means meet the targets.</summary>
internal sealed record LlmConfigReport(LlmConfig Config, IReadOnlyList<LlmRunReport> Runs, LlmSpread Spread, bool MeetsTargets)
{
    public static LlmConfigReport From(LlmConfig config, IReadOnlyList<LlmRunReport> runs)
    {
        var metrics = runs.Select(run => run.Metrics).ToList();
        var spread = new LlmSpread(
            SpreadOf(metrics.Select(m => m.FactRecall)),
            SpreadOf(metrics.Select(m => m.HallucinationRate)),
            SpreadOf(metrics.Select(m => m.CompileFirstAttempt)),
            SpreadOf(metrics.Select(m => m.CompileAfterRepair)),
            SpreadOf(metrics.Select(m => m.SourcesValid)),
            MeanSpread.Of(metrics.Select(m => m.MeanCaseSeconds)),
            MeanSpread.Of(metrics.Select(m => (double)m.Errors)));

        var meetsTargets = LlmMetrics.MeetsTargets(
            spread.FactRecall?.Mean,
            spread.HallucinationRate?.Mean,
            spread.CompileAfterRepair?.Mean);

        return new LlmConfigReport(config, runs, spread, meetsTargets);
    }

    private static MeanSpread? SpreadOf(IEnumerable<double?> values)
    {
        var present = values.OfType<double>().ToList();
        return present.Count == 0 ? null : MeanSpread.Of(present);
    }
}

/// <summary>The whole report that is written to evals/results: the options and every completed config.</summary>
internal sealed record LlmEvalReport(
    DateTimeOffset StartedAt,
    LlmEvalArguments Options,
    IReadOnlyList<string> Cases,
    IReadOnlyList<LlmConfigReport> Configs);

/// <summary>Renders the Markdown table that is printed and written next to the JSON report.</summary>
internal static class LlmReport
{
    private const string Header =
        "| Model | Temp | TopP | Prompt | TopK | k1/b | Fact recall | Halluc. | Compile 1st | Compile after repair | Sources valid | s/case | Targets |";

    private const string Separator = "|---|---|---|---|---|---|---|---|---|---|---|---|---|";

    public static string Markdown(IReadOnlyList<LlmConfigReport> configs)
    {
        var lines = new List<string> { Header, Separator };
        lines.AddRange(configs.Select(Row));
        return string.Join('\n', lines) + '\n';
    }

    public static string VariantName(PromptVariant variant) => variant == PromptVariant.FewShot ? "few-shot" : "zero-shot";

    private static string Row(LlmConfigReport report)
    {
        var config = report.Config;
        var spread = report.Spread;
        var cells = new[]
        {
            config.Model,
            Number(config.Temperature),
            Number(config.TopP),
            VariantName(config.Variant),
            config.TopK.ToString(CultureInfo.InvariantCulture),
            $"{Number(config.Bm25.K1)}/{Number(config.Bm25.B)}",
            Spread(spread.FactRecall),
            Spread(spread.HallucinationRate),
            Spread(spread.CompileFirstAttempt),
            Spread(spread.CompileAfterRepair),
            Spread(spread.SourcesValid),
            Spread(spread.MeanCaseSeconds),
            report.MeetsTargets ? "✓" : "✗",
        };

        return "| " + string.Join(" | ", cells) + " |";
    }

    private static string Spread(MeanSpread? spread)
    {
        return spread is null
            ? "–"
            : $"{spread.Mean.ToString("F2", CultureInfo.InvariantCulture)} ± {spread.StdDev.ToString("F2", CultureInfo.InvariantCulture)}";
    }

    private static string Number(double value) => value.ToString(CultureInfo.InvariantCulture);
}
