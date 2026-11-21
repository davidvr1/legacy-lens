namespace LegacyLens.Core.Summary;

/// <summary>The JSON Schema the language model must follow for a <see cref="Model.CodeSummary"/>.</summary>
public static class CodeSummarySchema
{
    public const string Json = """
        {
          "type": "object",
          "properties": {
            "purpose": { "type": "string" },
            "inputs": { "type": "array", "items": { "type": "string" } },
            "outputs": { "type": "array", "items": { "type": "string" } },
            "databaseAccess": { "type": "array", "items": { "type": "string" } },
            "includes": { "type": "array", "items": { "type": "string" } },
            "comObjects": { "type": "array", "items": { "type": "string" } }
          },
          "required": ["purpose", "inputs", "outputs", "databaseAccess", "includes", "comObjects"],
          "additionalProperties": false
        }
        """;
}
