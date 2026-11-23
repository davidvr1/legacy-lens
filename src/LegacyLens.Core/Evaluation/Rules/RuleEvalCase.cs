using LegacyLens.Core.Model;

namespace LegacyLens.Core.Evaluation.Rules;

/// <summary>One rule eval case: a legacy file and the rule findings the rule engine should report on it.</summary>
public sealed record RuleEvalCase(string Id, SourceLanguage Language, string Code, IReadOnlyList<ExpectedRuleFinding> Expected);
