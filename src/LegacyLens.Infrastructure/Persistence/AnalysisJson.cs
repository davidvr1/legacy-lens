using System.Text.Json;

namespace LegacyLens.Infrastructure.Persistence;

/// <summary>Serializes the summary and migration results into the JSON columns of <c>Analyses</c>.</summary>
internal static class AnalysisJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Read<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options) ?? throw new InvalidOperationException($"Stored {typeof(T).Name} JSON is empty.");
}
