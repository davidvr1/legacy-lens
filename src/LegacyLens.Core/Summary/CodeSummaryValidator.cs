using LegacyLens.Core.Model;

namespace LegacyLens.Core.Summary;

/// <summary>Checks that a model reply is a usable <see cref="CodeSummary"/>.</summary>
public static class CodeSummaryValidator
{
    /// <summary>Returns null when the summary is valid, otherwise an error message naming the bad field.</summary>
    public static string? Validate(CodeSummary? summary)
    {
        if (summary is null)
        {
            return "summary is required.";
        }

        if (string.IsNullOrWhiteSpace(summary.Purpose))
        {
            return "purpose is required.";
        }

        return ValidateList(summary.Inputs, "inputs")
            ?? ValidateList(summary.Outputs, "outputs")
            ?? ValidateList(summary.DatabaseAccess, "databaseAccess")
            ?? ValidateList(summary.Includes, "includes")
            ?? ValidateList(summary.ComObjects, "comObjects");
    }

    private static string? ValidateList(IReadOnlyList<string>? items, string fieldName)
    {
        if (items is null || items.Any(item => string.IsNullOrWhiteSpace(item)))
        {
            return $"{fieldName} must be an array of non-empty strings.";
        }

        return null;
    }
}
