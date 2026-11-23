namespace LegacyLens.Core.Evaluation;

/// <summary>The scored result of one migration proposal.</summary>
public sealed record MigrationScore(
    bool CompiledFirstAttempt,
    bool CompiledAfterRepair,
    bool SourcesValid,
    IReadOnlyList<string> CitedIds,
    IReadOnlyList<string> PromptIds);
