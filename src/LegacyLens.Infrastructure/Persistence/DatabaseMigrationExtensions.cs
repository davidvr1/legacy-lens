using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LegacyLens.Infrastructure.Persistence;

/// <summary>Applies EF Core migrations at startup.</summary>
public static class DatabaseMigrationExtensions
{
    /// <summary>Applies pending migrations when <see cref="DatabaseOptions.MigrateOnStartup"/> is true. Does nothing otherwise.</summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        if (!services.GetRequiredService<IOptions<DatabaseOptions>>().Value.MigrateOnStartup)
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LegacyLensDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }
}
