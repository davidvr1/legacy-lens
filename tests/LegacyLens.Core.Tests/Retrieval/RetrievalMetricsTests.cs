using LegacyLens.Core.Retrieval;

namespace LegacyLens.Core.Tests.Retrieval;

public sealed class RetrievalMetricsTests
{
    [Theory]
    [InlineData(new[] { "a" }, new[] { "a", "b", "c" }, 3, 1.0)]
    [InlineData(new[] { "a" }, new[] { "b", "c", "a" }, 3, 1.0)]
    [InlineData(new[] { "a" }, new[] { "b", "c", "a" }, 2, 0.0)]
    [InlineData(new[] { "a", "b" }, new[] { "a", "x", "y" }, 3, 0.5)]
    [InlineData(new[] { "a", "b" }, new[] { "b", "a" }, 2, 1.0)]
    [InlineData(new[] { "a", "b" }, new[] { "x", "y", "z" }, 3, 0.0)]
    [InlineData(new[] { "a" }, new string[0], 3, 0.0)]
    [InlineData(new[] { "a" }, new[] { "a" }, 0, 0.0)]
    public void RecallAtK(string[] expected, string[] retrieved, int k, double want)
    {
        Assert.Equal(want, RetrievalMetrics.RecallAtK(expected, retrieved, k), 10);
    }

    [Fact]
    public void RecallAtK_duplicate_retrieved_ids_count_once()
    {
        Assert.Equal(0.5, RetrievalMetrics.RecallAtK(["a", "b"], ["a", "a", "a"], 3), 10);
    }

    [Fact]
    public void RecallAtK_empty_expected_throws()
    {
        Assert.Throws<ArgumentException>(() => RetrievalMetrics.RecallAtK([], ["a"], 3));
    }

    [Theory]
    [InlineData(new[] { "a" }, new[] { "a" }, 1.0)]
    [InlineData(new[] { "a" }, new[] { "x", "a" }, 0.5)]
    [InlineData(new[] { "a" }, new[] { "x", "y", "a" }, 1.0 / 3)]
    [InlineData(new[] { "a", "b" }, new[] { "x", "b", "a" }, 0.5)]
    [InlineData(new[] { "a" }, new[] { "x", "y" }, 0.0)]
    [InlineData(new[] { "a" }, new string[0], 0.0)]
    public void ReciprocalRank(string[] expected, string[] retrieved, double want)
    {
        Assert.Equal(want, RetrievalMetrics.ReciprocalRank(expected, retrieved), 10);
    }

    [Fact]
    public void ReciprocalRank_is_not_limited_by_k()
    {
        var retrieved = Enumerable.Range(0, 9).Select(i => $"x{i}").Append("a").ToList();
        Assert.Equal(0.1, RetrievalMetrics.ReciprocalRank(["a"], retrieved), 10);
    }
}
