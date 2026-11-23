namespace LegacyLens.Core.Evaluation.Rules;

/// <summary>A rule finding that an eval case expects: the rule id and the 1-based line.</summary>
public sealed record ExpectedRuleFinding(string RuleId, int Line);
