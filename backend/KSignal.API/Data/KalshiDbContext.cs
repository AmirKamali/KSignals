using KSignal.API.Models;
using Microsoft.EntityFrameworkCore;

namespace KSignal.API.Data;

public class KalshiDbContext : DbContext
{
    public KalshiDbContext(DbContextOptions<KalshiDbContext> options) : base(options)
    {
    }

    public DbSet<MarketCategory> MarketCategories => Set<MarketCategory>();
    public DbSet<MarketCache> Markets => Set<MarketCache>();

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

        var market = modelBuilder.Entity<MarketCache>();
        market.ToTable("Markets");
        market.HasKey(e => e.TickerId);
        market.HasIndex(e => e.SeriesTicker);
        market.Property(e => e.TickerId).HasMaxLength(255).IsRequired();
        market.Property(e => e.SeriesTicker).HasMaxLength(255).IsRequired();
        market.Property(e => e.Title);
        market.Property(e => e.Subtitle);
        market.Property(e => e.Volume);
        market.Property(e => e.Volume24h);
        market.Property(e => e.CreatedTime);
        market.Property(e => e.ExpirationTime);
        market.Property(e => e.CloseTime);
        market.Property(e => e.LatestExpirationTime);
        market.Property(e => e.OpenTime);
        market.Property(e => e.Status).HasMaxLength(64);
        market.Property(e => e.YesBid);
        market.Property(e => e.YesBidDollars);
        market.Property(e => e.YesAsk);
        market.Property(e => e.YesAskDollars);
        market.Property(e => e.NoBid);
        market.Property(e => e.NoBidDollars);
        market.Property(e => e.NoAsk);
        market.Property(e => e.NoAskDollars);
        market.Property(e => e.LastPrice);
        market.Property(e => e.LastPriceDollars);
        market.Property(e => e.PreviousYesBid);
        market.Property(e => e.PreviousYesBidDollars);
        market.Property(e => e.PreviousYesAsk);
        market.Property(e => e.PreviousYesAskDollars);
        market.Property(e => e.PreviousPrice);
        market.Property(e => e.PreviousPriceDollars);
        market.Property(e => e.Liquidity);
        market.Property(e => e.LiquidityDollars);
        market.Property(e => e.SettlementValue);
        market.Property(e => e.SettlementValueDollars);
        market.Property(e => e.NotionalValue);
        market.Property(e => e.NotionalValueDollars);
        market.Property(e => e.JsonResponse);
        market.Property(e => e.LastUpdate).IsRequired();
    }
}
