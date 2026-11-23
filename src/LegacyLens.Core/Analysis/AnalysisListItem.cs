using LegacyLens.Core.Model;

namespace LegacyLens.Core.Analysis;

/// <summary>One row of the analysis history: enough to show a list without loading the code or the full result.</summary>
public sealed record AnalysisListItem(
    Guid Id,
    string FileName,
    SourceLanguage Language,
    DateTimeOffset CreatedAt,
    FindingCounts FindingCounts);
