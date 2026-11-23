using System.Text.Json;
using LegacyLens.Core.Model;

namespace LegacyLens.Core.Evaluation;

/// <summary>Reads the expected.json of an evaluation case into an <see cref="LlmEvalCase"/>.</summary>
public static class LlmEvalCaseParser
{
    /// <summary>
    /// Parses expected.json: a language and the expected facts per category. A missing category means no facts.
    /// Throws ArgumentException for an unknown language, an unknown category, an empty keyword list or an empty keyword,
    /// and JsonException for malformed JSON or a wrong JSON shape.
    /// </summary>
    public static LlmEvalCase Parse(string id, string fileName, string code, string expectedJson)
    {
        using var document = JsonDocument.Parse(expectedJson);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("expected.json must be a JSON object.");
        }

        var language = ParseLanguage(root);
        var facts = ParseFacts(root);
        return new LlmEvalCase(id, new SourceFile(fileName, language, code), facts);
    }

    private static SourceLanguage ParseLanguage(JsonElement root)
    {
        var name = root.TryGetProperty("language", out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

        return name switch
        {
            "classic-asp" => SourceLanguage.ClassicAsp,
            "webforms" => SourceLanguage.WebForms,
            _ => throw new ArgumentException($"Unknown language '{name}'. Use 'classic-asp' or 'webforms'."),
        };
    }

    private static IReadOnlyList<ExpectedFact> ParseFacts(JsonElement root)
    {
        var facts = new List<ExpectedFact>();
        if (!root.TryGetProperty("facts", out var factsElement))
        {
            return facts;
        }

        if (factsElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("facts must be a JSON object.");
        }

        foreach (var category in factsElement.EnumerateObject())
        {
            var summaryCategory = ParseCategory(category.Name);
            facts.AddRange(ParseCategoryFacts(summaryCategory, category.Value));
        }

        return facts;
    }

    private static SummaryCategory ParseCategory(string name)
    {
        return name switch
        {
            "inputs" => SummaryCategory.Inputs,
            "outputs" => SummaryCategory.Outputs,
            "databaseAccess" => SummaryCategory.DatabaseAccess,
            "includes" => SummaryCategory.Includes,
            "comObjects" => SummaryCategory.ComObjects,
            _ => throw new ArgumentException($"Unknown category '{name}'."),
        };
    }

    private static IEnumerable<ExpectedFact> ParseCategoryFacts(SummaryCategory category, JsonElement list)
    {
        if (list.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException($"The '{category}' facts must be an array of keyword arrays.");
        }

        return list.EnumerateArray().Select(keywords => new ExpectedFact(category, ParseKeywords(keywords))).ToList();
    }

    private static IReadOnlyList<string> ParseKeywords(JsonElement keywordArray)
    {
        if (keywordArray.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Each fact must be an array of keywords.");
        }

        var keywords = new List<string>();
        foreach (var element in keywordArray.EnumerateArray())
        {
            var keyword = element.ValueKind == JsonValueKind.String ? element.GetString() : null;
            if (keyword is null)
            {
                throw new JsonException("Each keyword must be a string.");
            }

            if (keyword.Split('|').Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException("A keyword must not be empty.");
            }

            keywords.Add(keyword);
        }

        if (keywords.Count == 0)
        {
            throw new ArgumentException("A fact must have at least one keyword.");
        }

        return keywords;
    }
}
