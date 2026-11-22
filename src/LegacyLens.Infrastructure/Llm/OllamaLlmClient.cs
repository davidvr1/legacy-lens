using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using LegacyLens.Core.Llm;
using Microsoft.Extensions.Options;

namespace LegacyLens.Infrastructure.Llm;

/// <summary>Calls a local Ollama server (POST api/generate) and reads the JSON reply.</summary>
public sealed class OllamaLlmClient(HttpClient httpClient, IOptions<LlmOptions> options) : ILlmClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<T> CompleteJsonAsync<T>(string prompt, string jsonSchema, CancellationToken cancellationToken)
    {
        var request = new GenerateRequest(
            options.Value.Model,
            prompt,
            false,
            JsonNode.Parse(jsonSchema),
            new GenerateOptions(options.Value.Temperature, options.Value.TopP, options.Value.NumCtx));

        GenerateResponse? envelope;
        try
        {
            using var response = await httpClient.PostAsJsonAsync("api/generate", request, JsonOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new LlmException($"Ollama returned HTTP {(int)response.StatusCode}.");
            }

            envelope = await response.Content.ReadFromJsonAsync<GenerateResponse>(JsonOptions, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient.Timeout fired; the caller did not cancel.
            throw new LlmException("The language model timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new LlmException("The language model could not be reached.", ex);
        }
        catch (JsonException ex)
        {
            throw new LlmInvalidResponseException(ex.Message, ex);
        }

        if (envelope?.Response is null)
        {
            throw new LlmInvalidResponseException("The model reply has no response text.");
        }

        try
        {
            var result = JsonSerializer.Deserialize<T>(envelope.Response, JsonOptions);
            if (result is null)
            {
                throw new LlmInvalidResponseException("The model reply is empty.");
            }

            return result;
        }
        catch (JsonException ex)
        {
            throw new LlmInvalidResponseException(ex.Message, ex);
        }
    }

    private sealed record GenerateRequest(string Model, string Prompt, bool Stream, JsonNode? Format, GenerateOptions Options);

    private sealed record GenerateOptions(
        double Temperature,
        [property: JsonPropertyName("top_p")] double TopP,
        [property: JsonPropertyName("num_ctx")] int NumCtx);

    private sealed record GenerateResponse(string? Response);
}
