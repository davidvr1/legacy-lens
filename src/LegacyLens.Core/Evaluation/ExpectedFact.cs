namespace LegacyLens.Core.Evaluation;

/// <summary>
/// A fact that a correct summary should state. Each keyword may hold alternatives separated by '|'.
/// A fact matches a stated item when every keyword (any one of its alternatives) occurs in the item as a
/// case-insensitive substring.
/// </summary>
public sealed record ExpectedFact(SummaryCategory Category, IReadOnlyList<string> Keywords)
{
    /// <summary>Returns true when the stated item contains every keyword, in any one of its alternatives.</summary>
    public bool Matches(string item)
    {
        return Keywords.All(keyword =>
            keyword.Split('|').Any(alternative => item.Contains(alternative.Trim(), StringComparison.OrdinalIgnoreCase)));
    }
}
