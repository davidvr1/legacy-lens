namespace LegacyLens.Core.Evaluation;

/// <summary>One call made through a <see cref="RecordingLlmClient"/>. Reply is null and Error holds the message when the call threw.</summary>
public sealed record LlmCall(string Prompt, object? Reply, string? Error = null);
