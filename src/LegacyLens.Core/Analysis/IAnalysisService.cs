using LegacyLens.Core.Model;

namespace LegacyLens.Core.Analysis;

/// <summary>Runs the rules, the AI summary and the AI migration for one source file, then saves the result.</summary>
public interface IAnalysisService
{
    Task<StoredAnalysis> AnalyzeAsync(SourceFile file, CancellationToken cancellationToken);
}
