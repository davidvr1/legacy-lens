namespace LegacyLens.Core.Evaluation;

/// <summary>The lists of a <see cref="LegacyLens.Core.Model.CodeSummary"/> that are scored against expected facts.</summary>
public enum SummaryCategory
{
    Inputs,
    Outputs,
    DatabaseAccess,
    Includes,
    ComObjects,
}
