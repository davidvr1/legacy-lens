namespace LegacyLens.Core.Model;

/// <summary>An AI-generated description of what a legacy file does. Only describes the code, never a verdict.</summary>
public sealed record CodeSummary(
    string Purpose,
    IReadOnlyList<string> Inputs,
    IReadOnlyList<string> Outputs,
    IReadOnlyList<string> DatabaseAccess,
    IReadOnlyList<string> Includes,
    IReadOnlyList<string> ComObjects);
