using System.Net.Http.Json;
using System.Text.Json;
using LegacyLens.Core.Llm;
using LegacyLens.Core.Retrieval;
using Microsoft.Extensions.Options;

namespace LegacyLens.Infrastructure.Retrieval;

/// <summary>Calls a local Ollama server (POST api/embed) for embedding vectors.</summary>
public sealed class OllamaEmbeddingClient(HttpClient httpClient, IOptions<RetrievalOptions> options) : IEmbeddingClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(texts);
        if (texts.Count == 0)
        {
            return Array.Empty<float[]>();
        }

        var request = new EmbedRequest(options.Value.EmbeddingModel, texts);

        EmbedResponse? envelope;
        try
        {
            using var response = await httpClient.PostAsJsonAsync("api/embed", request, JsonOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new LlmException($"Ollama returned HTTP {(int)response.StatusCode}.");
            }

            envelope = await response.Content.ReadFromJsonAsync<EmbedResponse>(JsonOptions, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient.Timeout fired; the caller did not cancel.
            throw new LlmException("The embedding model timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new LlmException("The embedding model could not be reached.", ex);
        }
        catch (JsonException ex)
        {
            throw new LlmInvalidResponseException(ex.Message, ex);
        }

        if (envelope?.Embeddings is null || envelope.Embeddings.Count != texts.Count)
        {
            throw new LlmInvalidResponseException("The embedding reply does not have one vector per text.");
        }

        if (envelope.Embeddings.Any(vector => vector is null || vector.Length == 0))
        {
            throw new LlmInvalidResponseException("The embedding reply contains an empty vector.");
        }

        return envelope.Embeddings;
    }

    private sealed record EmbedRequest(string Model, IReadOnlyList<string> Input);

    private sealed record EmbedResponse(List<float[]>? Embeddings);
}
