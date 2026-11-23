using LegacyLens.Core.Model;

namespace LegacyLens.Core.Evaluation;

/// <summary>
/// Scores a summary against the expected facts of a case. A stated item that no expected fact of its category
/// supports is a hallucination, unless it is grounded in the code.
/// </summary>
public static class SummaryScorer
{
    /// <summary>Words too common to show that a stated item is grounded in the code.</summary>
    public static readonly IReadOnlySet<string> GenericWords = new HashSet<string>(
    [
        "the", "and", "for", "with", "from", "into", "via", "using", "used", "uses", "not", "all", "any", "each",
        "when", "then", "else", "this", "that", "its", "are", "was", "has", "have", "user", "users", "page", "pages",
        "data", "value", "values", "field", "fields", "name", "names", "parameter", "parameters", "param", "input",
        "inputs", "output", "outputs", "query", "querystring", "form", "request", "response", "session", "cookie",
        "cookies", "server", "application", "variable", "variables", "string", "html", "text", "message", "write",
        "writes", "written", "display", "displays", "shown", "show", "shows", "redirect", "redirects", "database",
        "table", "tables", "record", "records", "recordset", "row", "rows", "column", "columns", "select", "insert",
        "update", "delete", "sql", "statement", "connection", "command", "execute", "executes", "read", "reads",
        "object", "objects", "com", "include", "includes", "file", "files", "result", "results", "list", "error",
        "errors", "current", "new", "none",
    ], StringComparer.OrdinalIgnoreCase);

    /// <summary>Stated items that mean "nothing" are ignored and not counted.</summary>
    private static readonly IReadOnlySet<string> NothingWords = new HashSet<string>(
        ["none", "n/a", "na", "-", "nothing", "no"], StringComparer.OrdinalIgnoreCase);

    /// <summary>Scores the summary. Purpose is not scored; every item of the other five lists is.</summary>
    public static SummaryScore Score(CodeSummary summary, IReadOnlyList<ExpectedFact> facts, string code)
    {
        var items = StatedItems(summary);
        var missed = facts.Where(fact => !items.Any(item => IsMatch(fact, item))).ToList();
        var hallucinations = items
            .Where(item => !IsSupported(item, facts) && !IsGrounded(item.Text, code))
            .Select(item => item.Text)
            .ToList();

        return new SummaryScore(
            facts.Count,
            facts.Count - missed.Count,
            items.Count,
            hallucinations.Count,
            missed,
            hallucinations);
    }

    private static List<StatedItem> StatedItems(CodeSummary summary)
    {
        var items = new List<StatedItem>();
        AddItems(items, SummaryCategory.Inputs, summary.Inputs);
        AddItems(items, SummaryCategory.Outputs, summary.Outputs);
        AddItems(items, SummaryCategory.DatabaseAccess, summary.DatabaseAccess);
        AddItems(items, SummaryCategory.Includes, summary.Includes);
        AddItems(items, SummaryCategory.ComObjects, summary.ComObjects);
        return items;
    }

    private static void AddItems(List<StatedItem> items, SummaryCategory category, IEnumerable<string> texts)
    {
        foreach (var text in texts)
        {
            var trimmed = text.Trim();
            if (!IsNothing(trimmed))
            {
                items.Add(new StatedItem(category, trimmed));
            }
        }
    }

    private static bool IsNothing(string text)
    {
        // "Control: None" states nothing, the same as "None".
        var colon = text.LastIndexOf(':');
        var key = (colon >= 0 ? text[(colon + 1)..] : text).Trim().TrimEnd('.');
        return key.Length == 0 || NothingWords.Contains(key);
    }

    private static bool IsMatch(ExpectedFact fact, StatedItem item)
    {
        return fact.Category == item.Category && fact.Matches(item.Text);
    }

    private static bool IsSupported(StatedItem item, IReadOnlyList<ExpectedFact> facts)
    {
        return facts.Any(fact => IsMatch(fact, item));
    }

    /// <summary>True when the item has no specific tokens (too vague to check), or one of its tokens occurs in the code.</summary>
    private static bool IsGrounded(string text, string code)
    {
        var tokens = SpecificTokens(text);
        return tokens.Count == 0 || tokens.Any(token => code.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Splits the text on every char that is not a letter, digit, '_' or '.', and keeps the specific tokens.</summary>
    private static List<string> SpecificTokens(string text)
    {
        var cleaned = new string(text.Select(c => IsTokenChar(c) ? c : ' ').ToArray());
        return cleaned
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim('.'))
            .Where(token => token.Length >= 3 && !GenericWords.Contains(token))
            .ToList();
    }

    private static bool IsTokenChar(char c)
    {
        return char.IsLetterOrDigit(c) || c == '_' || c == '.';
    }

    /// <summary>A non-ignored item from one of the scored lists, with the category it was stated under.</summary>
    private sealed record StatedItem(SummaryCategory Category, string Text);
}
