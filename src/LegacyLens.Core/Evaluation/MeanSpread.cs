namespace LegacyLens.Core.Evaluation;

/// <summary>The mean and sample standard deviation of a metric over several runs.</summary>
public sealed record MeanSpread(double Mean, double StdDev, int Count)
{
    /// <summary>Sample standard deviation (n - 1); StdDev 0 when Count is below 2; an empty input gives (0, 0, 0).</summary>
    public static MeanSpread Of(IEnumerable<double> values)
    {
        var list = values.ToList();
        if (list.Count == 0)
        {
            return new MeanSpread(0, 0, 0);
        }

        var mean = list.Average();
        if (list.Count < 2)
        {
            return new MeanSpread(mean, 0, list.Count);
        }

        var variance = list.Sum(value => (value - mean) * (value - mean)) / (list.Count - 1);
        return new MeanSpread(mean, Math.Sqrt(variance), list.Count);
    }
}
