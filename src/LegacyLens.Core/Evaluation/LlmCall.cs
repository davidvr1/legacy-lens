namespace LegacyLens.Core.Evaluation;

/// <summary>One call made through a <see cref="RecordingLlmClient"/>. Reply is null when the call threw.</summary>
public sealed record LlmCall(string Prompt, object? Reply);
