using LegacyLens.Core.Llm;
using LegacyLens.Infrastructure.Llm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LegacyLens.Api.Tests;

public sealed class AddLlmTests
{
    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection().AddLlm(config).BuildServiceProvider();
    }

    [Theory]
    [InlineData("Demo")]
    [InlineData("demo")]
    public void Demo_provider_gives_DemoLlmClient(string provider)
    {
        using var sp = Build(new() { ["Llm:Provider"] = provider });
        Assert.IsType<DemoLlmClient>(sp.GetRequiredService<ILlmClient>());
    }

    [Fact]
    public void Missing_provider_defaults_to_demo()
    {
        using var sp = Build([]);
        Assert.IsType<DemoLlmClient>(sp.GetRequiredService<ILlmClient>());
    }

    [Fact]
    public void Ollama_provider_gives_OllamaLlmClient()
    {
        using var sp = Build(new() { ["Llm:Provider"] = "Ollama", ["Llm:OllamaUrl"] = "http://example.invalid:1234/" });
        Assert.IsType<OllamaLlmClient>(sp.GetRequiredService<ILlmClient>());
    }

    [Fact]
    public void Unknown_provider_throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Build(new() { ["Llm:Provider"] = "Bogus" }));
        Assert.Contains("Bogus", ex.Message);
    }
}
