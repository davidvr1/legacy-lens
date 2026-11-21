using LegacyLens.Core.Model;
using LegacyLens.Core.Rules;
using LegacyLens.Core.Summary;

namespace LegacyLens.Core.Analysis;

/// <summary>Runs the rule engine first, then the summary (which receives the findings as context).</summary>
public sealed class AnalysisService(IRuleEngine ruleEngine, ISummaryService summaryService) : IAnalysisService
{
    public async Task<AnalysisResult> AnalyzeAsync(SourceFile file, CancellationToken cancellationToken)
    {
        var findings = ruleEngine.Run(file);
        var summary = await summaryService.SummarizeAsync(file, findings, cancellationToken);

        return new AnalysisResult(findings, summary.Summary, summary.Error);
    }
}
