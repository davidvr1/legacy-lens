using LegacyLens.Core.Model;

namespace LegacyLens.Core.Summary;

/// <summary>Builds the prompts sent to the language model for a code summary.</summary>
public static class SummaryPromptBuilder
{
    /// <summary>Builds the summary prompt for a file, including its line-numbered code and rule findings.</summary>
    public static string Build(SourceFile file, IReadOnlyList<Finding> findings)
    {
        var language = file.Language == SourceLanguage.ClassicAsp ? "Classic ASP (VBScript)" : "ASP.NET WebForms";
        var code = string.Join('\n', file.Lines.Select((line, index) => $"{index + 1}: {line}"));
        var findingLines = findings.Count == 0
            ? "- none"
            : string.Join('\n', findings.Select(f => $"- {f.RuleId} ({f.Severity}) line {f.Line}: {f.Title}"));

        return
            "You document legacy source code. Describe ONLY what is in the code below. " +
            "Do not guess and do not invent behavior that the code does not show. " +
            "Reply with JSON only, matching this schema:\n" +
            CodeSummarySchema.Json + "\n\n" +
            $"File name: {file.FileName}\n" +
            $"Language: {language}\n\n" +
            "Code (each line is prefixed with its line number):\n" +
            code + "\n\n" +
            "Rule findings from a deterministic scanner (context only):\n" +
            findingLines + "\n";
    }

    /// <summary>Builds the retry prompt after an invalid reply, quoting the error.</summary>
    public static string BuildRetry(string originalPrompt, string error)
    {
        return
            originalPrompt + "\n" +
            "Your previous reply was invalid and could not be used. The error was:\n" +
            error + "\n\n" +
            "Reply again with valid JSON only, matching the schema above. Do not write any text outside the JSON.";
    }
}
