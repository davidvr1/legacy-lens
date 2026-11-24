namespace LegacyLens.Core.Migration;

/// <summary>
/// Detects code that stops in the middle. The usual cause: the model wrote a double quote inside the code without
/// escaping it, the quote ended the JSON string, and the rest of the code was lost (for example <c>Email = Request.Form[</c>).
/// </summary>
public static class TruncatedCode
{
    private const string OpenEndings = "[(,=+.";

    /// <summary>
    /// True when the code has more '{' than '}' or its last character opens an expression. Braces are counted on the raw
    /// text, so a lone brace inside a string literal can mislead it; balanced ones such as <c>$"{x}"</c> do not.
    /// </summary>
    public static bool IsTruncated(string code)
    {
        var trimmed = code.TrimEnd();
        if (trimmed.Length == 0)
        {
            return false;
        }

        var depth = 0;
        foreach (var ch in trimmed)
        {
            if (ch == '{')
            {
                depth++;
            }
            else if (ch == '}')
            {
                depth--;
            }
        }

        return depth > 0 || OpenEndings.Contains(trimmed[^1]);
    }
}
