using LegacyLens.Core.Retrieval;

namespace LegacyLens.Core.Tests.Retrieval;

/// <summary>Deterministic embedding client. The vector of a text comes from a function; every call is recorded.</summary>
public sealed class FakeEmbeddingClient(Func<string, float[]> vectorFor) : IEmbeddingClient
{
    public List<IReadOnlyList<string>> Calls { get; } = [];

    /// <summary>When greater than zero, the next calls throw this exception and decrement the counter.</summary>
    public int FailuresRemaining { get; set; }

    public Exception? Failure { get; set; }

    public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        lock (Calls)
        {
            Calls.Add(texts.ToList());
        }

        if (FailuresRemaining > 0)
        {
            FailuresRemaining--;
            throw Failure ?? new LegacyLens.Core.Llm.LlmException("fake failure");
        }

        IReadOnlyList<float[]> vectors = texts.Select(vectorFor).ToList();
        return Task.FromResult(vectors);
    }
}
