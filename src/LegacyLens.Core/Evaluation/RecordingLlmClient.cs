using LegacyLens.Core.Llm;

namespace LegacyLens.Core.Evaluation;

/// <summary>Wraps an <see cref="ILlmClient"/> and records every call. The exception of a failed call is rethrown unchanged.</summary>
public sealed class RecordingLlmClient(ILlmClient inner) : ILlmClient
{
    private readonly List<LlmCall> calls = [];

    /// <summary>Every call made so far, in order.</summary>
    public IReadOnlyList<LlmCall> Calls => calls;

    public async Task<T> CompleteJsonAsync<T>(string prompt, string jsonSchema, CancellationToken cancellationToken)
    {
        T reply;
        try
        {
            reply = await inner.CompleteJsonAsync<T>(prompt, jsonSchema, cancellationToken);
        }
        catch
        {
            calls.Add(new LlmCall(prompt, null));
            throw;
        }

        calls.Add(new LlmCall(prompt, reply));
        return reply;
    }
}
