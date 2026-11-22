namespace LegacyLens.Core.Migration;

/// <summary>The JSON Schema the language model must follow for a <see cref="MigrationReply"/>.</summary>
public static class MigrationSchema
{
    public const string Json = """
        {
          "type": "object",
          "properties": {
            "code": { "type": "string" },
            "notes": { "type": "array", "items": { "type": "string" } },
            "sources": { "type": "array", "items": { "type": "string" } }
          },
          "required": ["code", "notes", "sources"],
          "additionalProperties": false
        }
        """;
}
