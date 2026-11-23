using System.Text.Json;
using LegacyLens.Core.Llm;
using LegacyLens.Core.Model;

namespace LegacyLens.Core.Summary;

/// <summary>Builds the prompts sent to the language model for a code summary.</summary>
public static class SummaryPromptBuilder
{
    /// <summary>Introduces the worked example in the few-shot prompt. The example is not the real file.</summary>
    public const string FewShotExampleHeader = "Example (a different file, only to show the expected style; do not copy its facts):";

    /// <summary>The raw (un-numbered) legacy code of the few-shot example. The migration prompt reuses it.</summary>
    public const string FewShotExampleCode =
        "<!--#include file=\"header.inc\"-->\n" +
        "<%\n" +
        "Dim id, rs\n" +
        "id = Request.QueryString(\"productId\")\n" +
        "Set rs = Server.CreateObject(\"ADODB.Recordset\")\n" +
        "rs.Open \"SELECT Name, Price FROM Products WHERE Id = \" & id, Application(\"Db\")\n" +
        "Response.Write \"<h1>\" & rs(\"Name\") & \"</h1>\"\n" +
        "%>";

    // Relaxed escaping keeps < and > readable in the prompt; the JSON is never embedded in HTML.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly string FewShotExampleJson = JsonSerializer.Serialize(
        new CodeSummary(
            "Shows the name of one product, looked up by the productId query string value.",
            ["QueryString: productId", "Application: Db"],
            ["HTML heading with the product name"],
            ["SELECT Products (Name, Price by Id)"],
            ["header.inc"],
            ["ADODB.Recordset"]),
        JsonOptions);

    /// <summary>Builds the summary prompt for a file, including its line-numbered code and rule findings.</summary>
    public static string Build(SourceFile file, IReadOnlyList<Finding> findings, PromptVariant variant = PromptVariant.ZeroShot)
    {
        var language = file.Language == SourceLanguage.ClassicAsp ? "Classic ASP (VBScript)" : "ASP.NET WebForms";
        var code = NumberLines(file.Lines);
        var findingLines = findings.Count == 0
            ? "- none"
            : string.Join('\n', findings.Select(f => $"- {f.RuleId} ({f.Severity}) line {f.Line}: {f.Title}"));
        var example = variant == PromptVariant.FewShot ? FewShotSection() : string.Empty;

        return
            "You document legacy source code. Describe ONLY what is in the code below. " +
            "Do not guess and do not invent behavior that the code does not show.\n" +
            "Fields:\n" +
            "- inputs: every value the code reads, as \"<source>: <name>\". <source> is one of QueryString, Form, Session, " +
            "Cookie, Application, Config or Control; <name> is the exact key used in the code.\n" +
            "- outputs: what the page writes, shows, sends, saves or redirects to (name the target page or file).\n" +
            "- databaseAccess: each SQL statement, as \"<operation> <table>\".\n" +
            "- Only list names that appear in the code below.\n" +
            "- includes: included file paths. comObjects: ProgIDs passed to Server.CreateObject. Use [] when there are none.\n" +
            "Reply with JSON only, matching this schema:\n" +
            CodeSummarySchema.Json + "\n\n" +
            example +
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

    /// <summary>The worked example that comes before the real file in the few-shot prompt.</summary>
    private static string FewShotSection()
    {
        return
            FewShotExampleHeader + "\n" +
            "Code:\n" +
            NumberLines(FewShotExampleCode.Split('\n')) + "\n" +
            "JSON reply:\n" +
            FewShotExampleJson + "\n\n" +
            "Now the real file.\n\n";
    }

    /// <summary>Prefixes each line with its 1-based line number.</summary>
    private static string NumberLines(IEnumerable<string> lines)
    {
        return string.Join('\n', lines.Select((line, index) => $"{index + 1}: {line}"));
    }
}
