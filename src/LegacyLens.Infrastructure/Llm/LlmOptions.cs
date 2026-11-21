namespace LegacyLens.Infrastructure.Llm;

/// <summary>Settings for the language model, read from the "Llm" section of appsettings.json.</summary>
public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    /// <summary>"Demo" (canned answers, no network) or "Ollama" (local model).</summary>
    public string Provider { get; set; } = "Demo";

    public string Model { get; set; } = "qwen2.5-coder:7b";

    public string OllamaUrl { get; set; } = "http://localhost:11434";

    public int TimeoutSeconds { get; set; } = 120;
}
