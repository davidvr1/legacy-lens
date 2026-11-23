using LegacyLens.Core.Model;

namespace LegacyLens.Core.Analysis;

/// <summary>Saves and reads analyses. Implementations assign the id and the creation time.</summary>
public interface IAnalysisRepository
{
    /// <summary>Saves the source file and its result, and returns the stored analysis with its new id.</summary>
    Task<StoredAnalysis> SaveAsync(SourceFile file, AnalysisResult result, CancellationToken cancellationToken);

    /// <summary>Returns up to <paramref name="limit"/> analyses, newest first, without code or full results.</summary>
    Task<IReadOnlyList<AnalysisListItem>> ListAsync(int limit, CancellationToken cancellationToken);

    /// <summary>Returns the analysis with this id, or null when there is none.</summary>
    Task<StoredAnalysis?> GetAsync(Guid id, CancellationToken cancellationToken);
}
