using LegacyLens.Core.Retrieval;
using LegacyLens.Infrastructure.Retrieval;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LegacyLens.Api.Tests;

public sealed class AddRetrievalTests
{
    private static ServiceProvider Build(Dictionary<string, string?> settings, ServiceProviderOptions? options = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection().AddRetrieval(config);
        return options is null ? services.BuildServiceProvider() : services.BuildServiceProvider(options);
    }

    [Theory]
    [InlineData("Bm25")]
    [InlineData("bm25")]
    [InlineData("BM25")]
    public void Bm25_mode_gives_Bm25Retriever_case_insensitive(string mode)
    {
        using var sp = Build(new() { ["Retrieval:Mode"] = mode });
        Assert.IsType<Bm25Retriever>(sp.GetRequiredService<IRetriever>());
    }

    [Fact]
    public void Missing_mode_defaults_to_Bm25()
    {
        using var sp = Build([]);
        Assert.IsType<Bm25Retriever>(sp.GetRequiredService<IRetriever>());
        Assert.Same(sp.GetRequiredService<Bm25Retriever>(), sp.GetRequiredService<IRetriever>());
    }

    [Fact]
    public void Knowledge_base_is_FileKnowledgeBase_singleton()
    {
        using var sp = Build([]);
        Assert.IsType<FileKnowledgeBase>(sp.GetRequiredService<IKnowledgeBase>());
        Assert.Same(sp.GetRequiredService<IKnowledgeBase>(), sp.GetRequiredService<IKnowledgeBase>());
    }

    [Theory]
    [InlineData("Embedding")]
    [InlineData("embedding")]
    [InlineData("EMBEDDING")]
    public void Embedding_mode_gives_singleton_EmbeddingRetriever(string mode)
    {
        using var sp = Build(new() { ["Retrieval:Mode"] = mode });

        var first = sp.GetRequiredService<IRetriever>();
        var second = sp.GetRequiredService<IRetriever>();

        Assert.IsType<EmbeddingRetriever>(first);
        Assert.Same(first, second);
        Assert.IsType<OllamaEmbeddingClient>(sp.GetRequiredService<IEmbeddingClient>());
        Assert.Same(sp.GetRequiredService<IEmbeddingClient>(), sp.GetRequiredService<IEmbeddingClient>());
    }

    [Theory]
    [InlineData("Hybrid")]
    [InlineData("hybrid")]
    [InlineData("HYBRID")]
    public void Hybrid_mode_gives_singleton_HybridRetriever(string mode)
    {
        using var sp = Build(new() { ["Retrieval:Mode"] = mode });

        var first = sp.GetRequiredService<IRetriever>();
        var second = sp.GetRequiredService<IRetriever>();

        Assert.IsType<HybridRetriever>(first);
        Assert.Same(first, second);
        Assert.Same(sp.GetRequiredService<EmbeddingRetriever>(), sp.GetRequiredService<EmbeddingRetriever>());
        Assert.Same(sp.GetRequiredService<Bm25Retriever>(), sp.GetRequiredService<Bm25Retriever>());
    }

    [Fact]
    public void Singletons_resolve_from_a_scope_with_validation_on()
    {
        using var sp = Build(
            new() { ["Retrieval:Mode"] = "Hybrid" },
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = sp.CreateScope();

        Assert.Same(sp.GetRequiredService<IRetriever>(), scope.ServiceProvider.GetRequiredService<IRetriever>());
    }

    [Fact]
    public void Embedding_http_client_uses_Llm_url_with_trailing_slash_and_timeout()
    {
        using var sp = Build(new()
        {
            ["Retrieval:Mode"] = "Embedding",
            ["Llm:OllamaUrl"] = "http://example.invalid:1234",
            ["Llm:TimeoutSeconds"] = "42",
        });

        var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient(RetrievalServiceCollectionExtensions.EmbeddingHttpClientName);

        Assert.Equal(new Uri("http://example.invalid:1234/"), client.BaseAddress);
        Assert.Equal(TimeSpan.FromSeconds(42), client.Timeout);
    }

    [Fact]
    public void Embedding_http_client_defaults()
    {
        using var sp = Build(new() { ["Retrieval:Mode"] = "Embedding" });

        var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient(RetrievalServiceCollectionExtensions.EmbeddingHttpClientName);

        Assert.Equal(new Uri("http://localhost:11434/"), client.BaseAddress);
        Assert.Equal(TimeSpan.FromSeconds(120), client.Timeout);
    }

    [Fact]
    public void Unknown_mode_throws_with_documented_message()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Build(new() { ["Retrieval:Mode"] = "Bogus" }));
        Assert.Equal("Unknown Retrieval:Mode 'Bogus'. Use 'Bm25', 'Embedding' or 'Hybrid'.", ex.Message);
    }

    [Fact]
    public void RetrievalOptions_have_documented_defaults()
    {
        var o = new RetrievalOptions();
        Assert.Equal("Retrieval", RetrievalOptions.SectionName);
        Assert.Equal("Bm25", o.Mode);
        Assert.Equal("knowledge", o.KnowledgePath);
        Assert.Equal(3, o.TopK);
        Assert.Equal(1.2, o.K1);
        Assert.Equal(0.75, o.B);
        Assert.Equal("nomic-embed-text", o.EmbeddingModel);
    }
}
