using LegacyLens.Core.Model;

namespace LegacyLens.Core.Evaluation;

/// <summary>Scores a migration proposal: whether it compiled, and whether its cited sources were in the prompt.</summary>
public static class MigrationScorer
{
    /// <summary>
    /// CompiledFirstAttempt = proposal.CompilesOk && !proposal.Repaired.
    /// CompiledAfterRepair = proposal.CompilesOk.
    /// SourcesValid = citedIds is not empty and every cited id is in promptIds (case-insensitive).
    /// </summary>
    public static MigrationScore Score(MigrationProposal proposal, IReadOnlyList<string> citedIds, IReadOnlyList<string> promptIds)
    {
        var sourcesValid = citedIds.Count > 0 && citedIds.All(id => promptIds.Any(promptId => string.Equals(promptId, id, StringComparison.OrdinalIgnoreCase)));

        return new MigrationScore(
            proposal.CompilesOk && !proposal.Repaired,
            proposal.CompilesOk,
            sourcesValid,
            citedIds,
            promptIds);
    }
}
