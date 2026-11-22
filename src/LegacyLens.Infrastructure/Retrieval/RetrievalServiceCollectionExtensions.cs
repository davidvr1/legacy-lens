using LegacyLens.Core.Retrieval;
using LegacyLens.Infrastructure.Llm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LegacyLens.Infrastructure.Retrieval;

/// <summary>Registers the knowledge base and the retriever chosen by Retrieval:Mode.</summary>
public static class RetrievalServiceCollectionExtensions
{
    /// <summary>Name of the HttpClient used for Ollama embedding calls.</summary>
    public const string EmbeddingHttpClientName = "OllamaEmbeddings";

    public static IServiceCollection AddRetrieval(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RetrievalOptions>().Bind(configuration.GetSection(RetrievalOptions.SectionName));
        services.AddOptions<LlmOptions>().Bind(configuration.GetSection(LlmOptions.SectionName));

        var mode = configuration[$"{RetrievalOptions.SectionName}:Mode"] ?? "Bm25";

        services.AddSingleton<IKnowledgeBase, FileKnowledgeBase>();
        services.AddSingleton<Bm25Retriever>(sp =>
        {
            var retrieval = sp.GetRequiredService<IOptions<RetrievalOptions>>().Value;
            return new Bm25Retriever(sp.GetRequiredService<IKnowledgeBase>(), new Bm25Parameters(retrieval.K1, retrieval.B));
        });

        if (string.Equals(mode, "Bm25", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IRetriever>(sp => sp.GetRequiredService<Bm25Retriever>());
        }
        else if (string.Equals(mode, "Embedding", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mode, "Hybrid", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient(EmbeddingHttpClientName, (sp, client) =>
            {
                var llmOptions = sp.GetRequiredService<IOptions<LlmOptions>>().Value;
                client.BaseAddress = new Uri(llmOptions.OllamaUrl.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(llmOptions.TimeoutSeconds);
            });

            // Singletons, so the chunk vectors are computed once per process and not once per request.
            // The client holds one HttpClient for the process lifetime; that is fine for a local Ollama server
            // (handler rotation only matters for changing DNS).
            services.AddSingleton<IEmbeddingClient>(sp => new OllamaEmbeddingClient(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(EmbeddingHttpClientName),
                sp.GetRequiredService<IOptions<RetrievalOptions>>()));
            services.AddSingleton<EmbeddingRetriever>();

            if (string.Equals(mode, "Embedding", StringComparison.OrdinalIgnoreCase))
            {
                services.AddSingleton<IRetriever>(sp => sp.GetRequiredService<EmbeddingRetriever>());
            }
            else
            {
                services.AddSingleton<HybridRetriever>();
                services.AddSingleton<IRetriever>(sp => sp.GetRequiredService<HybridRetriever>());
            }
        }
        else
        {
            throw new InvalidOperationException($"Unknown Retrieval:Mode '{mode}'. Use 'Bm25', 'Embedding' or 'Hybrid'.");
        }

        return services;
    }
}
