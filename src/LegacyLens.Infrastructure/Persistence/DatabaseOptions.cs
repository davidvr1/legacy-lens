namespace LegacyLens.Infrastructure.Persistence;

/// <summary>Settings for the database, read from the "Database" section of appsettings.json.</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>When true, pending EF Core migrations are applied before the app starts serving requests.</summary>
    public bool MigrateOnStartup { get; set; } = true;
}
