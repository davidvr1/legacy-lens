using LegacyLens.Core.Model;
using LegacyLens.Core.Retrieval;
using LegacyLens.Core.Validation;

namespace LegacyLens.Core.Tests.Migration;

public sealed class FakeRetriever(IReadOnlyList<KnowledgeChunk> chunks, Exception? toThrow = null) : IRetriever
{
    public List<(string Query, int TopK)> Calls { get; } = [];

    public Task<IReadOnlyList<KnowledgeChunk>> SearchAsync(string query, int topK, CancellationToken cancellationToken)
    {
        Calls.Add((query, topK));
        if (toThrow is not null)
        {
            throw toThrow;
        }

        return Task.FromResult(chunks);
    }
}

/// <summary>Returns queued results in order; the last one repeats when the queue runs out.</summary>
public sealed class FakeCodeValidator(params CodeValidationResult[] results) : ICodeValidator
{
    private int _next;

    public List<string> Codes { get; } = [];

    public Task<CodeValidationResult> ValidateAsync(string code, CancellationToken cancellationToken)
    {
        Codes.Add(code);
        var result = results[Math.Min(_next, results.Length - 1)];
        _next++;
        return Task.FromResult(result);
    }
}
