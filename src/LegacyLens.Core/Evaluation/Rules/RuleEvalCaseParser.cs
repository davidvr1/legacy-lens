using System.Text.Json;
using LegacyLens.Core.Model;

namespace LegacyLens.Core.Evaluation.Rules;

/// <summary>Reads the expected.json of a rule eval case into a <see cref="RuleEvalCase"/>.</summary>
public static class RuleEvalCaseParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Parses expected.json: a language and the expected findings (rule id and line). Throws <see cref="JsonException"/>
    /// when the JSON is malformed, the language is missing or unknown, the findings are missing, or a finding is incomplete.
    /// </summary>
    public static RuleEvalCase Parse(string id, string code, string expectedJson)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(expectedJson);

        var document = JsonSerializer.Deserialize<ExpectedFile>(expectedJson, JsonOptions)
            ?? throw new JsonException("expected.json must be a JSON object.");

        var language = ParseLanguage(document.Language);
        if (document.Findings is null)
        {
            throw new JsonException("expected.json must have a 'findings' array (it may be empty).");
        }

        var findings = document.Findings.Select(ParseFinding).ToList();
        return new RuleEvalCase(id, language, code, findings);
    }

    private static SourceLanguage ParseLanguage(string? name)
    {
        return name switch
        {
            "classic-asp" => SourceLanguage.ClassicAsp,
            "webforms" => SourceLanguage.WebForms,
            null => throw new JsonException("expected.json must have a 'language' ('classic-asp' or 'webforms')."),
            _ => throw new JsonException($"Unknown language '{name}'. Use 'classic-asp' or 'webforms'."),
        };
    }

    private static ExpectedRuleFinding ParseFinding(FindingJson? finding)
    {
        if (finding is null)
        {
            throw new JsonException("Each finding must be a JSON object with 'ruleId' and 'line'.");
        }

        if (string.IsNullOrWhiteSpace(finding.RuleId))
        {
            throw new JsonException("Every finding needs a non-blank 'ruleId'.");
        }

        if (finding.Line < 1)
        {
            throw new JsonException($"The line of finding '{finding.RuleId}' must be 1 or more.");
        }

        return new ExpectedRuleFinding(finding.RuleId, finding.Line);
    }

    private sealed record ExpectedFile(string? Language, List<FindingJson?>? Findings);

    private sealed record FindingJson(string? RuleId, int Line);
}
