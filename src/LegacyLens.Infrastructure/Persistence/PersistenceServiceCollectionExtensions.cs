using LegacyLens.Core.Analysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LegacyLens.Infrastructure.Persistence;

/// <summary>Registers the SQL Server context, the analysis repository and the database options.</summary>
public static class PersistenceServiceCollectionExtensions
{
    /// <summary>Name of the connection string in <c>ConnectionStrings</c>.</summary>
    public const string ConnectionStringName = "LegacyLens";

    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>().Bind(configuration.GetSection(DatabaseOptions.SectionName));

        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"The connection string '{ConnectionStringName}' is missing. Set ConnectionStrings:{ConnectionStringName} in configuration.");
        }

        services.AddDbContext<LegacyLensDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IAnalysisRepository, EfAnalysisRepository>();
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
