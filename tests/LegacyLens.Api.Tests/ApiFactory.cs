using LegacyLens.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace LegacyLens.Api.Tests;

/// <summary>
/// Hosts the API for integration tests. The database is SQLite in memory (one open connection for the
/// factory lifetime) instead of SQL Server, and migrations are not run at startup.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:MigrateOnStartup"] = "false",
        }));

        builder.ConfigureTestServices(services =>
        {
            // EF Core 9+ keeps the provider setup in IDbContextOptionsConfiguration<T>, so remove that as well,
            // or SQL Server and SQLite would both be registered.
            services.RemoveAll<DbContextOptions<LegacyLensDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<LegacyLensDbContext>>();
            services.AddDbContext<LegacyLensDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // Open before EnsureCreated: an in-memory SQLite database lives only as long as its connection.
        _connection.Open();
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<LegacyLensDbContext>().Database.EnsureCreated();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection.Dispose();
        }

        base.Dispose(disposing);
    }
}
