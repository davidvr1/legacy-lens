namespace LegacyLens.Core.Evaluation.Rules;

/// <summary>Counts per rule and in total over all cases, plus the differences of each case.</summary>
public sealed record RuleEvalResult(
    int Cases,
    IReadOnlyList<RuleCounts> PerRule,
    RuleCounts Total,
    IReadOnlyList<RuleCaseResult> PerCase);
