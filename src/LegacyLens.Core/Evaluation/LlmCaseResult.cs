using LegacyLens.Core.Model;

namespace LegacyLens.Core.Evaluation;

/// <summary>
/// The result of one case in one run. A task that was not run has null scores and no error, and a zero duration.
/// A task that ran has either a score or an error. StatedSummary and Proposal keep the model output behind the scores.
/// </summary>
public sealed record LlmCaseResult(
    string CaseId,
    SummaryScore? Summary,
    string? SummaryError,
    TimeSpan SummaryDuration,
    MigrationScore? Migration,
    string? MigrationError,
    TimeSpan MigrationDuration,
    CodeSummary? StatedSummary = null,
    MigrationProposal? Proposal = null);
