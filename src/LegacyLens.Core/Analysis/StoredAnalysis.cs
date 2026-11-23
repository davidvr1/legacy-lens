using LegacyLens.Core.Model;

namespace LegacyLens.Core.Analysis;

/// <summary>A saved analysis: the source file, its result, and when it was saved.</summary>
public sealed record StoredAnalysis(
    Guid Id,
    string FileName,
    SourceLanguage Language,
    string Code,
    DateTimeOffset CreatedAt,
    AnalysisResult Result);
