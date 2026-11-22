namespace LegacyLens.Core.Retrieval;

/// <summary>Recall@k and reciprocal rank for one query.</summary>
public static class RetrievalMetrics
{
    /// <summary>|expected ∩ first k retrieved| / |expected|. Throws <see cref="ArgumentException"/> when nothing is expected.</summary>
    public static double RecallAtK(IReadOnlyCollection<string> expectedIds, IReadOnlyList<string> retrievedIds, int k)
    {
        ArgumentNullException.ThrowIfNull(expectedIds);
        ArgumentNullException.ThrowIfNull(retrievedIds);

        var expected = new HashSet<string>(expectedIds, StringComparer.Ordinal);
        if (expected.Count == 0)
        {
            throw new ArgumentException("At least one expected id is required.", nameof(expectedIds));
        }

        var found = retrievedIds
            .Take(k)
            .Where(expected.Contains)
            .Distinct(StringComparer.Ordinal)
            .Count();

        return (double)found / expected.Count;
    }

    /// <summary>1 / (1-based rank of the first retrieved id that is expected), or 0 when none is.</summary>
    public static double ReciprocalRank(IReadOnlyCollection<string> expectedIds, IReadOnlyList<string> retrievedIds)
    {
        ArgumentNullException.ThrowIfNull(expectedIds);
        ArgumentNullException.ThrowIfNull(retrievedIds);

        var expected = new HashSet<string>(expectedIds, StringComparer.Ordinal);
        for (var i = 0; i < retrievedIds.Count; i++)
        {
            if (expected.Contains(retrievedIds[i]))
            {
                return 1.0 / (i + 1);
            }
        }

        return 0;
    }
}
