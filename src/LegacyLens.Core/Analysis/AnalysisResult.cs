using LegacyLens.Core.Model;

namespace LegacyLens.Core.Analysis;

/// <summary>
/// Rule findings plus an optional AI summary and an optional AI migration proposal.
/// Findings are present even when the summary or the migration failed.
/// </summary>
public sealed record AnalysisResult(
    IReadOnlyList<Finding> Findings,
    CodeSummary? Summary,
    string? SummaryError,
    MigrationProposal? Migration,
    string? MigrationError);
