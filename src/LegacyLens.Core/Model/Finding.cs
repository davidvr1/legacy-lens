namespace LegacyLens.Core.Model;

/// <summary>One risk found in a source file. <see cref="Line"/> is 1-based.</summary>
public sealed record Finding(
    string RuleId,
    Severity Severity,
    int Line,
    string Title,
    string Explanation,
    string Fix);
