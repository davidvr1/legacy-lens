namespace LegacyLens.Core.Retrieval;

/// <summary>Okapi BM25 tuning values. K1 controls term frequency saturation, B controls length normalization.</summary>
public sealed record Bm25Parameters(double K1 = 1.2, double B = 0.75);
