using LegacyLens.Core.Model;
using LegacyLens.Core.Validation;

namespace LegacyLens.Core.Migration;

/// <summary>Builds the prompts sent to the language model for a migration proposal.</summary>
public static class MigrationPromptBuilder
{
    /// <summary>Starts the line that introduces each knowledge note in the prompt. The id follows it.</summary>
    public const string SourceHeaderPrefix = "### Source id: ";

    /// <summary>Builds the migration prompt for a file, with its line-numbered code, findings and the knowledge notes.</summary>
    public static string Build(SourceFile file, IReadOnlyList<Finding> findings, IReadOnlyList<KnowledgeChunk> chunks)
    {
        var language = file.Language == SourceLanguage.ClassicAsp ? "Classic ASP (VBScript)" : "ASP.NET WebForms";
        var code = string.Join('\n', file.Lines.Select((line, index) => $"{index + 1}: {line}"));
        var findingLines = findings.Count == 0
            ? "- none"
            : string.Join('\n', findings.Select(f => $"- {f.RuleId} ({f.Severity}) line {f.Line}: {f.Title}"));
        var knowledge = chunks.Count == 0
            ? "- none"
            : string.Join('\n', chunks.Select(c => $"{SourceHeaderPrefix}{c.Id}\nTitle: {c.Title}\n{c.Content}"));

        return
            "You migrate legacy source code to ASP.NET Core 8 (C# 12). " +
            "Migrate ONLY what is in the code below. Do not invent behavior that the code does not show.\n" +
            "Requirements:\n" +
            "- Return ONE C# file: a Razor Page model (PageModel) for a Classic ASP or WebForms page, " +
            "or a controller plus a service if the page is an endpoint. Include the using directives and a file-scoped namespace.\n" +
            "- Use only .NET 8 base libraries and ASP.NET Core. No NuGet packages (no Dapper, no EF Core, no Microsoft.Data.SqlClient). " +
            "For data access inject System.Data.Common.DbDataSource and use DbCommand with parameters.\n" +
            "- Never build SQL by concatenation. Read secrets from IConfiguration, never hard-code them.\n" +
            "- Use async and CancellationToken for all I/O.\n" +
            "- Fix every rule finding listed below.\n" +
            "- \"notes\": short notes for the developer (what changed and what to check).\n" +
            "- \"sources\": the ids of the knowledge notes you used. Use only ids from the list below.\n" +
            "Reply with JSON only, matching this schema:\n" +
            MigrationSchema.Json + "\n\n" +
            $"File name: {file.FileName}\n" +
            $"Language: {language}\n\n" +
            "Code (each line is prefixed with its line number):\n" +
            code + "\n\n" +
            "Rule findings from a deterministic scanner (must all be fixed):\n" +
            findingLines + "\n\n" +
            "Knowledge notes:\n" +
            knowledge + "\n";
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

    /// <summary>Builds the repair prompt after the code did not compile, quoting the code and the compiler errors.</summary>
    public static string BuildRepair(string originalPrompt, string previousCode, IReadOnlyList<CodeDiagnostic> diagnostics)
    {
        var errors = string.Join('\n', diagnostics.Select(d => $"- line {d.Line}: {d.Id} {d.Message}"));

        return
            originalPrompt + "\n" +
            "Your previous code did not compile. The code was:\n" +
            previousCode + "\n\n" +
            "The compiler errors were:\n" +
            errors + "\n\n" +
            "Fix the errors and reply again with the full corrected JSON.";
    }

    /// <summary>Returns the note ids from the prompt, in order. Used by the Demo client to cite the notes it was given.</summary>
    public static IReadOnlyList<string> ParseSourceIds(string prompt)
    {
        return prompt.Split('\n')
            .Where(line => line.StartsWith(SourceHeaderPrefix, StringComparison.Ordinal))
            .Select(line => line[SourceHeaderPrefix.Length..].Trim())
            .ToList();
    }
}
