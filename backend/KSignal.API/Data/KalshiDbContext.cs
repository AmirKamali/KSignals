using KSignal.API.Models;
using Microsoft.EntityFrameworkCore;

namespace KSignal.API.Data;

public class KalshiDbContext : DbContext
{
    public KalshiDbContext(DbContextOptions<KalshiDbContext> options) : base(options)
    {
    }

    public DbSet<MarketCategory> MarketCategories => Set<MarketCategory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var entity = modelBuilder.Entity<MarketCategory>();
        entity.ToTable("MarketCategories");
        entity.HasKey(e => e.SeriesId);
        entity.HasIndex(e => e.SeriesId).HasDatabaseName("idx_series_id");

        entity.Property(e => e.SeriesId).HasMaxLength(255).IsRequired();
        entity.Property(e => e.Category).HasMaxLength(255);
        entity.Property(e => e.Tags);
        entity.Property(e => e.Ticker).HasMaxLength(255);
        entity.Property(e => e.Title);
        entity.Property(e => e.Frequency).HasMaxLength(255);
        entity.Property(e => e.JsonResponse);
        entity.Property(e => e.LastUpdate).IsRequired();
    }
}

