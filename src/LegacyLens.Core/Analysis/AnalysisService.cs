using LegacyLens.Core.Migration;
using LegacyLens.Core.Model;
using LegacyLens.Core.Rules;
using LegacyLens.Core.Summary;

namespace LegacyLens.Core.Analysis;

/// <summary>
/// Runs the rule engine first, then the summary and the migration in parallel (both receive the findings
/// as context and do not depend on each other), then saves the result.
/// </summary>
public sealed class AnalysisService(
    IRuleEngine ruleEngine,
    ISummaryService summaryService,
    IMigrationService migrationService,
    IAnalysisRepository repository) : IAnalysisService
{
    public async Task<StoredAnalysis> AnalyzeAsync(SourceFile file, CancellationToken cancellationToken)
    {
        var findings = ruleEngine.Run(file);

        var summaryTask = summaryService.SummarizeAsync(file, findings, cancellationToken);
        var migrationTask = migrationService.ProposeAsync(file, findings, cancellationToken);
        await Task.WhenAll(summaryTask, migrationTask);

        var summary = await summaryTask;
        var migration = await migrationTask;

        var result = new AnalysisResult(findings, summary.Summary, summary.Error, migration.Proposal, migration.Error);
        return await repository.SaveAsync(file, result, cancellationToken);
    }
}
