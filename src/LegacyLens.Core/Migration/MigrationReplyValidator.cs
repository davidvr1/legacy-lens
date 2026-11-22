namespace LegacyLens.Core.Migration;

/// <summary>Checks that a model reply is a usable <see cref="MigrationReply"/>.</summary>
public static class MigrationReplyValidator
{
    /// <summary>Returns null when the reply is valid, otherwise an error message naming the bad field.</summary>
    public static string? Validate(MigrationReply? reply)
    {
        if (reply is null)
        {
            return "migration is required.";
        }

        if (string.IsNullOrWhiteSpace(reply.Code))
        {
            return "code is required.";
        }

        return ValidateList(reply.Notes, "notes")
            ?? ValidateList(reply.Sources, "sources");
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
