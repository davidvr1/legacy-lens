using System.Text.Json;
using LegacyLens.Core.Llm;
using LegacyLens.Core.Model;
using LegacyLens.Core.Summary;
using LegacyLens.Core.Validation;

namespace LegacyLens.Core.Migration;

/// <summary>Builds the prompts sent to the language model for a migration proposal.</summary>
public static class MigrationPromptBuilder
{
    /// <summary>Starts the line that introduces each knowledge note in the prompt. The id follows it.</summary>
    public const string SourceHeaderPrefix = "### Source id: ";

    /// <summary>Starts the repair part of a repair prompt. The eval uses it to find the repair call.</summary>
    public const string RepairMarker = "Your previous code did not compile.";

    /// <summary>Introduces the worked example in the few-shot prompt. The example is not the real file.</summary>
    public const string FewShotExampleHeader = "Example (a different file, only to show the expected style; do not copy it):";

    /// <summary>The C# reply code of the few-shot example. Its legacy input is <see cref="SummaryPromptBuilder.FewShotExampleCode"/>.</summary>
    public const string FewShotExampleCode =
        "using System.Data.Common;\n" +
        "using Microsoft.AspNetCore.Mvc;\n" +
        "using Microsoft.AspNetCore.Mvc.RazorPages;\n" +
        "\n" +
        "namespace Migrated.Pages;\n" +
        "\n" +
        "public sealed class ProductModel(DbDataSource dataSource) : PageModel\n" +
        "{\n" +
        "    public string? ProductName { get; private set; }\n" +
        "\n" +
        "    public async Task<IActionResult> OnGetAsync(int productId, CancellationToken cancellationToken)\n" +
        "    {\n" +
        "        await using var command = dataSource.CreateCommand(\"SELECT Name FROM Products WHERE Id = @id\");\n" +
        "        var parameter = command.CreateParameter();\n" +
        "        parameter.ParameterName = \"@id\";\n" +
        "        parameter.Value = productId;\n" +
        "        command.Parameters.Add(parameter);\n" +
        "        ProductName = (string?)await command.ExecuteScalarAsync(cancellationToken);\n" +
        "        return ProductName is null ? NotFound() : Page();\n" +
        "    }\n" +
        "}";

    // Relaxed escaping keeps < and > readable in the prompt; the JSON is never embedded in HTML.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };


    /// <summary>Builds the migration prompt for a file, with its line-numbered code, findings and the knowledge notes.</summary>
    public static string Build(SourceFile file, IReadOnlyList<Finding> findings, IReadOnlyList<KnowledgeChunk> chunks, PromptVariant variant = PromptVariant.ZeroShot)
    {
        var language = file.Language == SourceLanguage.ClassicAsp ? "Classic ASP (VBScript)" : "ASP.NET WebForms";
        var code = string.Join('\n', file.Lines.Select((line, index) => $"{index + 1}: {line}"));
        var findingLines = findings.Count == 0
            ? "- none"
            : string.Join('\n', findings.Select(f => $"- {f.RuleId} ({f.Severity}) line {f.Line}: {f.Title}"));
        var knowledge = chunks.Count == 0
            ? "- none"
            : string.Join('\n', chunks.Select(c => $"{SourceHeaderPrefix}{c.Id}\nTitle: {c.Title}\n{c.Content}"));
        var example = variant == PromptVariant.FewShot ? FewShotSection(chunks) : string.Empty;

        return
            "You migrate legacy source code to ASP.NET Core 10 (C# 14). " +
            "Migrate ONLY what is in the code below. Do not invent behavior that the code does not show.\n" +
            "Requirements:\n" +
            "- Return ONE C# file: a Razor Page model (PageModel) for a Classic ASP or WebForms page, " +
            "or a controller plus a service if the page is an endpoint. Include the using directives and a file-scoped namespace.\n" +
            "- Declare every record and class you use in the same file, for example a row record for query results.\n" +
            "- Use only .NET 10 base libraries and ASP.NET Core. No NuGet packages (no Dapper, no EF Core, no Microsoft.Data.SqlClient). " +
            "For data access inject System.Data.Common.DbDataSource and use DbCommand with parameters.\n" +
            "- Never build SQL by concatenation. Read secrets from IConfiguration, never hard-code them.\n" +
            "- Use async and CancellationToken for all I/O.\n" +
            "- Fix every rule finding listed below.\n" +
            "- \"code\": the full C# source text, plain C# only. It starts with the using directives. " +
            "No <%@ %> directives, no Razor or HTML markup, no file name instead of code, no top-level statements. " +
            "Inside the JSON string, escape every double quote in the code as \\\" and write each line break as \\n.\n" +
            "- A PageModel needs: using Microsoft.AspNetCore.Mvc; using Microsoft.AspNetCore.Mvc.RazorPages;. " +
            "Do not use legacy APIs: no System.Data.SqlClient, no System.Web, no Response.Write, no Server.CreateObject.\n" +
            "- \"notes\": short notes for the developer (what changed and what to check).\n" +
            "- \"sources\": the ids of the knowledge notes you used. Use only ids from the list below.\n" +
            "Reply with JSON only, matching this schema:\n" +
            MigrationSchema.Json + "\n\n" +
            example +
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
            RepairMarker + " The code was:\n" +
            previousCode + "\n\n" +
            "The compiler errors were:\n" +
            errors + "\n\n" +
            TruncationHint(previousCode) +
            "Fix the errors and reply again with the full corrected JSON.";
    }

    /// <summary>
    /// For code that stops in the middle: says where, why, and how to avoid the quote. Empty for complete code.
    /// The compiler errors alone (CS1513 "} expected") do not tell a small model that its code was cut off.
    /// </summary>
    private static string TruncationHint(string previousCode)
    {
        if (!TruncatedCode.IsTruncated(previousCode))
        {
            return string.Empty;
        }

        var lastLine = previousCode.TrimEnd().Split('\n').Length;
        return
            $"Your code stops in the middle of line {lastLine}. A double quote inside the code was probably not escaped, " +
            "so it ended the JSON string and the rest of the code was lost. Write every double quote inside the code as \\\". " +
            "Where the code reads a request value such as Request.Form[\"email\"], bind it as a handler parameter instead " +
            "(OnPostAsync(string email, CancellationToken cancellationToken)), which needs no quotes.\n\n";
    }

    /// <summary>Returns the note ids from the prompt, in order. Used by the Demo client to cite the notes it was given.</summary>
    public static IReadOnlyList<string> ParseSourceIds(string prompt)
    {
        return prompt.Split('\n')
            .Where(line => line.StartsWith(SourceHeaderPrefix, StringComparison.Ordinal))
            .Select(line => line[SourceHeaderPrefix.Length..].Trim())
            .ToList();
    }

    /// <summary>The worked example that comes before the real file in the few-shot prompt.</summary>
    private static string FewShotSection(IReadOnlyList<KnowledgeChunk> chunks)
    {
        var numbered = string.Join('\n', SummaryPromptBuilder.FewShotExampleCode.Split('\n')
            .Select((line, index) => $"{index + 1}: {line}"));

        return
            FewShotExampleHeader + "\n" +
            "Code:\n" +
            numbered + "\n" +
            "JSON reply:\n" +
            ExampleJson(chunks) + "\n" +
            "In the real reply, cite only ids from the Knowledge notes list.\n\n" +
            "Now the real file.\n\n";
    }

    /// <summary>
    /// The example reply. It cites the first note of the real prompt, so a model that copies the example's
    /// sources still cites a valid id (a made-up placeholder id was copied verbatim in the stage 5b eval).
    /// </summary>
    private static string ExampleJson(IReadOnlyList<KnowledgeChunk> chunks)
    {
        IReadOnlyList<string> sources = chunks.Count == 0 ? [] : [chunks[0].Id];
        return JsonSerializer.Serialize(
            new MigrationReply(
                FewShotExampleCode,
                ["The SQL query now uses a parameter instead of string concatenation.", "header.inc becomes the shared layout."],
                sources),
            JsonOptions);
    }
}
