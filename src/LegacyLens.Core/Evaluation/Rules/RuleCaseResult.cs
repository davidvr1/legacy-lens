namespace LegacyLens.Core.Evaluation.Rules;

/// <summary>The differences between the expected and the actual findings of one case.</summary>
/// <param name="CaseId">The id of the eval case.</param>
/// <param name="Missed">Expected findings that were not reported (false negatives).</param>
/// <param name="Unexpected">Reported findings that were not expected (false positives).</param>
public sealed record RuleCaseResult(
    string CaseId,
    IReadOnlyList<ExpectedRuleFinding> Missed,
    IReadOnlyList<ExpectedRuleFinding> Unexpected);
