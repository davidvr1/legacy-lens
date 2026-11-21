using LegacyLens.Core.Model;

namespace LegacyLens.Core.Summary;

/// <summary>Produces an AI-generated summary of a source file.</summary>
public interface ISummaryService
{
    Task<SummaryResult> SummarizeAsync(SourceFile file, IReadOnlyList<Finding> findings, CancellationToken cancellationToken);
}
