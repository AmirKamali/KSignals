using KSignal.API.Models;
using Microsoft.EntityFrameworkCore;

namespace KSignal.API.Data;

    public class KalshiDbContext : DbContext
    {
        public KalshiDbContext(DbContextOptions<KalshiDbContext> options) : base(options)
        {
        }

        public DbSet<MarketCategory> MarketCategories => Set<MarketCategory>();
        public DbSet<MarketSnapshot> MarketSnapshots => Set<MarketSnapshot>();
        public DbSet<TagsCategory> TagsCategories => Set<TagsCategory>();
        public DbSet<User> Users => Set<User>();
        public DbSet<MarketSeries> MarketSeries => Set<MarketSeries>();
        public DbSet<MarketEvent> MarketEvents => Set<MarketEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var entity = modelBuilder.Entity<MarketCategory>();
        entity.ToTable("market_categories");
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

        var marketSnapshot = modelBuilder.Entity<MarketSnapshot>();
        marketSnapshot.ToTable("market_snapshots");
        marketSnapshot.HasKey(e => e.MarketSnapshotID);
        marketSnapshot.HasIndex(e => e.Ticker);
        marketSnapshot.HasIndex(e => e.EventTicker);
        marketSnapshot.Property(e => e.Ticker).HasMaxLength(255).IsRequired();
        marketSnapshot.Property(e => e.EventTicker).HasMaxLength(255).IsRequired();
        marketSnapshot.Property(e => e.MarketType).HasMaxLength(50).IsRequired();
        marketSnapshot.Property(e => e.YesSubTitle);
        marketSnapshot.Property(e => e.NoSubTitle);
        marketSnapshot.Property(e => e.CreatedTime).IsRequired();
        marketSnapshot.Property(e => e.OpenTime).IsRequired();
        marketSnapshot.Property(e => e.CloseTime).IsRequired();
        marketSnapshot.Property(e => e.ExpectedExpirationTime);
        marketSnapshot.Property(e => e.LatestExpirationTime).IsRequired();
        marketSnapshot.Property(e => e.SettlementTimerSeconds).IsRequired();
        marketSnapshot.Property(e => e.Status).HasMaxLength(64).IsRequired();
        marketSnapshot.Property(e => e.ResponsePriceUnits).HasMaxLength(50).IsRequired();
        marketSnapshot.Property(e => e.YesBid).IsRequired();
        marketSnapshot.Property(e => e.YesBidDollars).IsRequired();
        marketSnapshot.Property(e => e.YesAsk).IsRequired();
        marketSnapshot.Property(e => e.YesAskDollars).IsRequired();
        marketSnapshot.Property(e => e.NoBid).IsRequired();
        marketSnapshot.Property(e => e.NoBidDollars).IsRequired();
        marketSnapshot.Property(e => e.NoAsk).IsRequired();
        marketSnapshot.Property(e => e.NoAskDollars).IsRequired();
        marketSnapshot.Property(e => e.LastPrice).IsRequired();
        marketSnapshot.Property(e => e.LastPriceDollars).IsRequired();
        marketSnapshot.Property(e => e.Volume).IsRequired();
        marketSnapshot.Property(e => e.Volume24h).IsRequired();
        marketSnapshot.Property(e => e.Result).HasMaxLength(10).IsRequired();
        marketSnapshot.Property(e => e.CanCloseEarly).IsRequired();
        marketSnapshot.Property(e => e.OpenInterest).IsRequired();
        marketSnapshot.Property(e => e.NotionalValue).IsRequired();
        marketSnapshot.Property(e => e.NotionalValueDollars).IsRequired();
        marketSnapshot.Property(e => e.PreviousYesBid).IsRequired();
        marketSnapshot.Property(e => e.PreviousYesBidDollars).IsRequired();
        marketSnapshot.Property(e => e.PreviousYesAsk).IsRequired();
        marketSnapshot.Property(e => e.PreviousYesAskDollars).IsRequired();
        marketSnapshot.Property(e => e.PreviousPrice).IsRequired();
        marketSnapshot.Property(e => e.PreviousPriceDollars).IsRequired();
        marketSnapshot.Property(e => e.Liquidity).IsRequired();
        marketSnapshot.Property(e => e.LiquidityDollars).IsRequired();
        marketSnapshot.Property(e => e.SettlementValue);
        marketSnapshot.Property(e => e.SettlementValueDollars);
        marketSnapshot.Property(e => e.ExpirationValue).IsRequired();
        marketSnapshot.Property(e => e.FeeWaiverExpirationTime);
        marketSnapshot.Property(e => e.EarlyCloseCondition);
        marketSnapshot.Property(e => e.TickSize).IsRequired();
        marketSnapshot.Property(e => e.StrikeType).HasMaxLength(50);
        marketSnapshot.Property(e => e.FloorStrike);
        marketSnapshot.Property(e => e.CapStrike);
        marketSnapshot.Property(e => e.FunctionalStrike);
        marketSnapshot.Property(e => e.CustomStrike);
        marketSnapshot.Property(e => e.RulesPrimary).IsRequired();
        marketSnapshot.Property(e => e.RulesSecondary).IsRequired();
        marketSnapshot.Property(e => e.MveCollectionTicker).HasMaxLength(255);
        marketSnapshot.Property(e => e.MveSelectedLegs);
        marketSnapshot.Property(e => e.PrimaryParticipantKey).HasMaxLength(255);
        marketSnapshot.Property(e => e.PriceLevelStructure).IsRequired();
        marketSnapshot.Property(e => e.PriceRanges);
        marketSnapshot.Property(e => e.GenerateDate).IsRequired();

        var tagsCategory = modelBuilder.Entity<TagsCategory>();
        tagsCategory.ToTable("TagsCategories");
        tagsCategory.HasKey(e => e.Id);
        // ClickHouse doesn't support RETURNING clause, so generate values client-side
        tagsCategory.Property(e => e.Id).ValueGeneratedNever();
        tagsCategory.HasIndex(e => new { e.Category, e.Tag }).IsUnique();
        tagsCategory.Property(e => e.Category).HasMaxLength(255).IsRequired();
        tagsCategory.Property(e => e.Tag).HasMaxLength(255).IsRequired();
        tagsCategory.Property(e => e.LastUpdate).IsRequired();
        // Don't use HasDefaultValue as it triggers RETURNING clause in ClickHouse
        tagsCategory.Property(e => e.IsDeleted).IsRequired();

        var user = modelBuilder.Entity<User>();
        user.ToTable("Users");
        user.HasKey(e => e.Id);
        user.HasIndex(e => e.FirebaseId).IsUnique();
        user.Property(e => e.FirebaseId).HasMaxLength(255).IsRequired();
        user.Property(e => e.Username).HasMaxLength(255);
        user.Property(e => e.FirstName).HasMaxLength(255);
        user.Property(e => e.LastName).HasMaxLength(255);
        user.Property(e => e.Email).HasMaxLength(255);
        user.Property(e => e.IsComnEmailOn).HasDefaultValue(false);
        user.Property(e => e.CreatedAt).IsRequired();
        user.Property(e => e.UpdatedAt).IsRequired();

        var marketSeries = modelBuilder.Entity<MarketSeries>();
        marketSeries.ToTable("market_series");
        marketSeries.HasKey(e => e.Ticker);
        marketSeries.Property(e => e.Ticker).HasMaxLength(255).IsRequired();
        marketSeries.Property(e => e.Frequency).HasMaxLength(255).IsRequired();
        marketSeries.Property(e => e.Title).IsRequired();
        marketSeries.Property(e => e.Category).HasMaxLength(255).IsRequired();
        marketSeries.HasIndex(e => e.Category).HasDatabaseName("idx_market_series_category");
        marketSeries.Property(e => e.Tags);
        marketSeries.HasIndex(e => e.Tags).HasDatabaseName("idx_market_series_tags");
        marketSeries.Property(e => e.SettlementSources);
        marketSeries.Property(e => e.ContractUrl);
        marketSeries.Property(e => e.ContractTermsUrl);
        marketSeries.Property(e => e.ProductMetadata);
        marketSeries.Property(e => e.FeeType).HasMaxLength(50).IsRequired();
        marketSeries.Property(e => e.FeeMultiplier).IsRequired();
        marketSeries.Property(e => e.AdditionalProhibitions);
        marketSeries.Property(e => e.LastUpdate).IsRequired();
        marketSeries.Property(e => e.IsDeleted).IsRequired();

        var marketEvent = modelBuilder.Entity<MarketEvent>();
        marketEvent.ToTable("market_events");
        marketEvent.HasKey(e => e.EventTicker);
        marketEvent.Property(e => e.EventTicker).HasMaxLength(255).IsRequired();
        marketEvent.Property(e => e.SeriesTicker).HasMaxLength(255).IsRequired();
        marketEvent.HasIndex(e => e.SeriesTicker).HasDatabaseName("idx_market_events_series_ticker");
        marketEvent.Property(e => e.SubTitle).IsRequired();
        marketEvent.Property(e => e.Title).IsRequired();
        marketEvent.Property(e => e.CollateralReturnType).HasMaxLength(50).IsRequired();
        marketEvent.Property(e => e.MutuallyExclusive).IsRequired();
        marketEvent.Property(e => e.Category).HasMaxLength(255).IsRequired();
        marketEvent.HasIndex(e => e.Category).HasDatabaseName("idx_market_events_category");
        marketEvent.Property(e => e.StrikeDate);
        marketEvent.Property(e => e.StrikePeriod).HasMaxLength(50);
        marketEvent.Property(e => e.AvailableOnBrokers).IsRequired();
        marketEvent.Property(e => e.ProductMetadata);
        marketEvent.Property(e => e.LastUpdate).IsRequired();
        marketEvent.Property(e => e.IsDeleted).IsRequired();
    }
}
