using LegacyLens.Core.Model;
using Microsoft.EntityFrameworkCore;

namespace LegacyLens.Infrastructure.Persistence;

/// <summary>EF Core context for saved analyses and their findings.</summary>
public sealed class LegacyLensDbContext(DbContextOptions<LegacyLensDbContext> options) : DbContext(options)
{
    public DbSet<AnalysisEntity> Analyses => Set<AnalysisEntity>();

    public DbSet<FindingEntity> Findings => Set<FindingEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AnalysisEntity>(entity =>
        {
            entity.ToTable("Analyses");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.FileName).HasMaxLength(260).IsRequired();
            entity.Property(a => a.Language).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(a => a.CreatedAt);
            entity.HasMany(a => a.Findings)
                .WithOne()
                .HasForeignKey(f => f.AnalysisId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FindingEntity>(entity =>
        {
            entity.ToTable("Findings");
            entity.HasKey(f => f.Id);
            entity.Property(f => f.RuleId).HasMaxLength(20).IsRequired();
            entity.Property(f => f.Severity).HasConversion<string>().HasMaxLength(10);
            entity.Property(f => f.Source).HasMaxLength(10).IsRequired();
        });
    }
}
