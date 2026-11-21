namespace LegacyLens.Core.Llm;

/// <summary>Talks to a language model. Implementations live in the Infrastructure project.</summary>
public interface ILlmClient
{
    /// <summary>
    /// Sends the prompt, asks for JSON matching <paramref name="jsonSchema"/>, and deserializes the reply to <typeparamref name="T"/>.
    /// Throws <see cref="LlmInvalidResponseException"/> when the reply is not valid JSON for T (or deserializes to null).
    /// Throws <see cref="LlmException"/> when the model cannot be reached, returns an HTTP error, or times out.
    /// Throws <see cref="OperationCanceledException"/> when the cancellation token is cancelled.
    /// </summary>
    Task<T> CompleteJsonAsync<T>(string prompt, string jsonSchema, CancellationToken cancellationToken);
}
