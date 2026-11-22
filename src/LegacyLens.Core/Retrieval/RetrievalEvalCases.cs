using System.Text.Json;

namespace LegacyLens.Core.Retrieval;

/// <summary>Reads retrieval eval cases from JSON: an array of { "id", "query", "expected": [...] }.</summary>
public static class RetrievalEvalCases
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Throws <see cref="JsonException"/> when the text is not a valid array of complete cases.</summary>
    public static IReadOnlyList<RetrievalEvalCase> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        var cases = JsonSerializer.Deserialize<List<RetrievalEvalCase>>(json, JsonOptions)
            ?? throw new JsonException("Expected a JSON array of retrieval eval cases.");

        foreach (var evalCase in cases)
        {
            if (evalCase is null || evalCase.Id is null || evalCase.Query is null || evalCase.Expected is null || evalCase.Expected.Count == 0)
            {
                throw new JsonException("Every retrieval eval case needs an id, a query and at least one expected id.");
            }
        }

        return cases;
    }
}
