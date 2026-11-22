using LegacyLens.Core.Analysis;
using LegacyLens.Core.Llm;
using LegacyLens.Core.Migration;
using LegacyLens.Core.Summary;
using LegacyLens.Core.Validation;
using LegacyLens.Infrastructure.Retrieval;
using LegacyLens.Infrastructure.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LegacyLens.Infrastructure.Llm;

/// <summary>Registers the language model client, the summary, migration and analysis services, and the code validator.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLlm(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<LlmOptions>().Bind(configuration.GetSection(LlmOptions.SectionName));
        services.AddOptions<RetrievalOptions>().Bind(configuration.GetSection(RetrievalOptions.SectionName));

        var provider = configuration[$"{LlmOptions.SectionName}:Provider"] ?? "Demo";
        if (string.Equals(provider, "Demo", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<ILlmClient, DemoLlmClient>();
        }
        else if (string.Equals(provider, "Ollama", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<ILlmClient, OllamaLlmClient>((sp, client) =>
            {
                var llmOptions = sp.GetRequiredService<IOptions<LlmOptions>>().Value;
                client.BaseAddress = new Uri(llmOptions.OllamaUrl.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(llmOptions.TimeoutSeconds);
            });
        }
        else
        {
            throw new InvalidOperationException($"Unknown Llm:Provider '{provider}'. Use 'Demo' or 'Ollama'.");
        }

        services.AddSingleton<ICodeValidator, RoslynCodeValidator>();
        services.AddSingleton(sp => new MigrationSettings(sp.GetRequiredService<IOptions<RetrievalOptions>>().Value.TopK));

        services.AddScoped<ISummaryService, SummaryService>();
        services.AddScoped<IMigrationService, MigrationService>();
        services.AddScoped<IAnalysisService, AnalysisService>();

        return services;
    }
}
