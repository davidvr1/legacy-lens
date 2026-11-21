using LegacyLens.Core.Model;

namespace LegacyLens.Core.Analysis;

/// <summary>Rule findings plus an optional AI summary. Findings are present even when the summary failed.</summary>
public sealed record AnalysisResult(IReadOnlyList<Finding> Findings, CodeSummary? Summary, string? SummaryError);
