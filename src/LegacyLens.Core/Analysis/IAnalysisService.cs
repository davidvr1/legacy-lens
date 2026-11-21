using LegacyLens.Core.Model;

namespace LegacyLens.Core.Analysis;

/// <summary>Runs the rules and the AI summary for one source file.</summary>
public interface IAnalysisService
{
    Task<AnalysisResult> AnalyzeAsync(SourceFile file, CancellationToken cancellationToken);
}
