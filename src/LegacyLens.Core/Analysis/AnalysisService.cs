using LegacyLens.Core.Migration;
using LegacyLens.Core.Model;
using LegacyLens.Core.Rules;
using LegacyLens.Core.Summary;

namespace LegacyLens.Core.Analysis;

/// <summary>Runs the rule engine first, then the summary and the migration (both receive the findings as context), then saves the result.</summary>
public sealed class AnalysisService(
    IRuleEngine ruleEngine,
    ISummaryService summaryService,
    IMigrationService migrationService,
    IAnalysisRepository repository) : IAnalysisService
{
    public async Task<StoredAnalysis> AnalyzeAsync(SourceFile file, CancellationToken cancellationToken)
    {
        var findings = ruleEngine.Run(file);
        var summary = await summaryService.SummarizeAsync(file, findings, cancellationToken);
        var migration = await migrationService.ProposeAsync(file, findings, cancellationToken);

        var result = new AnalysisResult(findings, summary.Summary, summary.Error, migration.Proposal, migration.Error);
        return await repository.SaveAsync(file, result, cancellationToken);
    }
}
