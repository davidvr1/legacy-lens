namespace LegacyLens.Core.Evaluation.Rules;

/// <summary>True positives, false positives and false negatives of one rule ("ALL" for the total).</summary>
public sealed record RuleCounts(string RuleId, int TruePositives, int FalsePositives, int FalseNegatives)
{
    /// <summary>TP / (TP + FP), or null when the rule reported nothing.</summary>
    public double? Precision => TruePositives + FalsePositives == 0
        ? null
        : (double)TruePositives / (TruePositives + FalsePositives);

    /// <summary>TP / (TP + FN), or null when no finding was expected.</summary>
    public double? Recall => TruePositives + FalseNegatives == 0
        ? null
        : (double)TruePositives / (TruePositives + FalseNegatives);
}
