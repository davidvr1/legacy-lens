namespace LegacyLens.Core.Retrieval;

/// <summary>Turns texts into embedding vectors. Implementations live in the Infrastructure project.</summary>
public interface IEmbeddingClient
{
    /// <summary>Returns one vector per text, in the same order. Throws <see cref="LegacyLens.Core.Llm.LlmException"/> on failure.</summary>
    Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken);
}
