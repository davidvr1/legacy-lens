namespace LegacyLens.Core.Evaluation;

/// <summary>The scored result of one summary against the expected facts of a case.</summary>
public sealed record SummaryScore(
    int ExpectedFacts,
    int MatchedFacts,
    int StatedItems,
    int HallucinatedItems,
    IReadOnlyList<ExpectedFact> MissedFacts,
    IReadOnlyList<string> Hallucinations)
{
    /// <summary>Share of expected facts that were matched. 1 when there are no expected facts.</summary>
    public double FactRecall => ExpectedFacts == 0 ? 1 : (double)MatchedFacts / ExpectedFacts;

    /// <summary>Share of stated items that are hallucinations. 0 when nothing was stated.</summary>
    public double HallucinationRate => StatedItems == 0 ? 0 : (double)HallucinatedItems / StatedItems;
}
