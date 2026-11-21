using LegacyLens.Core.Llm;

namespace LegacyLens.Core.Tests.Summary;

/// <summary>Hand-written fake: each queued item is a reply (object) or an Exception to throw.</summary>
public sealed class FakeLlmClient : ILlmClient
{
    private readonly Queue<object?> _responses = new();

    public List<string> Prompts { get; } = [];

    public FakeLlmClient Enqueue(object? replyOrException)
    {
        _responses.Enqueue(replyOrException);
        return this;
    }

    public Task<T> CompleteJsonAsync<T>(string prompt, string jsonSchema, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Prompts.Add(prompt);
        var next = _responses.Dequeue();
        if (next is Exception ex)
        {
            throw ex;
        }

        return Task.FromResult((T)next!);
    }
}
