using KSignal.API.Models;
using Microsoft.EntityFrameworkCore;

namespace KSignal.API.Data;

    public class KalshiDbContext : DbContext
    {
        public KalshiDbContext(DbContextOptions<KalshiDbContext> options) : base(options)
        {
        }

        public DbSet<MarketSnapshot> MarketSnapshots => Set<MarketSnapshot>();
        public DbSet<TagsCategory> TagsCategories => Set<TagsCategory>();
        public DbSet<User> Users => Set<User>();
        public DbSet<MarketSeries> MarketSeries => Set<MarketSeries>();
        public DbSet<MarketEvent> MarketEvents => Set<MarketEvent>();
        public DbSet<MarketHighPriority> MarketHighPriorities => Set<MarketHighPriority>();
        public DbSet<OrderbookSnapshot> OrderbookSnapshots => Set<OrderbookSnapshot>();
        public DbSet<OrderbookEvent> OrderbookEvents => Set<OrderbookEvent>();
        public DbSet<MarketCandlestickData> MarketCandlesticks => Set<MarketCandlestickData>();
        public DbSet<AnalyticsMarketFeature> AnalyticsMarketFeatures => Set<AnalyticsMarketFeature>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var marketSnapshot = modelBuilder.Entity<MarketSnapshot>();
        marketSnapshot.ToTable("market_snapshots");
        marketSnapshot.HasKey(e => e.MarketSnapshotID);
        marketSnapshot.Property(e => e.MarketSnapshotID).ValueGeneratedOnAdd();
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

        var marketHighPriority = modelBuilder.Entity<MarketHighPriority>();
        marketHighPriority.ToTable("market_highpriority");
        marketHighPriority.HasKey(e => e.TickerId);
        marketHighPriority.Property(e => e.TickerId).HasMaxLength(255).IsRequired();
        marketHighPriority.Property(e => e.Priority).IsRequired();
        marketHighPriority.HasIndex(e => e.Priority).HasDatabaseName("idx_market_highpriority_priority");
        marketHighPriority.Property(e => e.FetchCandlesticks).IsRequired();
        marketHighPriority.Property(e => e.FetchOrderbook).IsRequired();
        marketHighPriority.Property(e => e.ProcessAnalyticsL1).IsRequired();
        marketHighPriority.Property(e => e.ProcessAnalyticsL2).IsRequired();
        marketHighPriority.Property(e => e.ProcessAnalyticsL3).IsRequired();
        marketHighPriority.Property(e => e.LastUpdate).IsRequired();

        var orderbookSnapshot = modelBuilder.Entity<OrderbookSnapshot>();
        orderbookSnapshot.ToTable("orderbook_snapshots");
        orderbookSnapshot.HasKey(e => e.Id);
        orderbookSnapshot.Property(e => e.Id).ValueGeneratedOnAdd();
        orderbookSnapshot.Property(e => e.MarketId).HasMaxLength(255).IsRequired();
        orderbookSnapshot.HasIndex(e => e.MarketId).HasDatabaseName("idx_orderbook_snapshots_market_id");
        orderbookSnapshot.HasIndex(e => e.CapturedAt).HasDatabaseName("idx_orderbook_snapshots_captured_at");
        orderbookSnapshot.Property(e => e.CapturedAt).IsRequired();
        orderbookSnapshot.Property(e => e.YesLevels);
        orderbookSnapshot.Property(e => e.NoLevels);
        orderbookSnapshot.Property(e => e.YesDollars);
        orderbookSnapshot.Property(e => e.NoDollars);
        orderbookSnapshot.Property(e => e.BestYes);
        orderbookSnapshot.Property(e => e.BestNo);
        orderbookSnapshot.Property(e => e.Spread);
        orderbookSnapshot.Property(e => e.TotalYesLiquidity).IsRequired();
        orderbookSnapshot.Property(e => e.TotalNoLiquidity).IsRequired();

        var orderbookEvent = modelBuilder.Entity<OrderbookEvent>();
        orderbookEvent.ToTable("orderbook_events");
        orderbookEvent.HasKey(e => e.Id);
        orderbookEvent.Property(e => e.Id).ValueGeneratedOnAdd();
        orderbookEvent.Property(e => e.MarketId).HasMaxLength(255).IsRequired();
        orderbookEvent.HasIndex(e => e.MarketId).HasDatabaseName("idx_orderbook_events_market_id");
        orderbookEvent.HasIndex(e => e.EventTime).HasDatabaseName("idx_orderbook_events_event_time");
        orderbookEvent.Property(e => e.EventTime).IsRequired();
        orderbookEvent.Property(e => e.Side).HasMaxLength(10).IsRequired();
        orderbookEvent.Property(e => e.Price).IsRequired();
        orderbookEvent.Property(e => e.Size).IsRequired();
        orderbookEvent.Property(e => e.EventType).HasMaxLength(20).IsRequired();

        var marketCandlestick = modelBuilder.Entity<MarketCandlestickData>();
        marketCandlestick.ToTable("market_candlesticks");
        marketCandlestick.HasKey(e => e.Id);
        marketCandlestick.Property(e => e.Id).ValueGeneratedOnAdd();
        marketCandlestick.Property(e => e.Ticker).HasMaxLength(255).IsRequired();
        marketCandlestick.HasIndex(e => e.Ticker).HasDatabaseName("idx_market_candlesticks_ticker");
        marketCandlestick.Property(e => e.SeriesTicker).HasMaxLength(255).IsRequired();
        marketCandlestick.HasIndex(e => e.SeriesTicker).HasDatabaseName("idx_market_candlesticks_series_ticker");
        marketCandlestick.Property(e => e.PeriodInterval).IsRequired();
        marketCandlestick.Property(e => e.EndPeriodTs).IsRequired();
        marketCandlestick.Property(e => e.EndPeriodTime).IsRequired();
        marketCandlestick.HasIndex(e => e.EndPeriodTime).HasDatabaseName("idx_market_candlesticks_end_period_time");
        // Yes Bid OHLC
        marketCandlestick.Property(e => e.YesBidOpen).IsRequired();
        marketCandlestick.Property(e => e.YesBidLow).IsRequired();
        marketCandlestick.Property(e => e.YesBidHigh).IsRequired();
        marketCandlestick.Property(e => e.YesBidClose).IsRequired();
        marketCandlestick.Property(e => e.YesBidOpenDollars).IsRequired();
        marketCandlestick.Property(e => e.YesBidLowDollars).IsRequired();
        marketCandlestick.Property(e => e.YesBidHighDollars).IsRequired();
        marketCandlestick.Property(e => e.YesBidCloseDollars).IsRequired();
        // Yes Ask OHLC
        marketCandlestick.Property(e => e.YesAskOpen).IsRequired();
        marketCandlestick.Property(e => e.YesAskLow).IsRequired();
        marketCandlestick.Property(e => e.YesAskHigh).IsRequired();
        marketCandlestick.Property(e => e.YesAskClose).IsRequired();
        marketCandlestick.Property(e => e.YesAskOpenDollars).IsRequired();
        marketCandlestick.Property(e => e.YesAskLowDollars).IsRequired();
        marketCandlestick.Property(e => e.YesAskHighDollars).IsRequired();
        marketCandlestick.Property(e => e.YesAskCloseDollars).IsRequired();
        // Price OHLC (nullable)
        marketCandlestick.Property(e => e.PriceOpen);
        marketCandlestick.Property(e => e.PriceLow);
        marketCandlestick.Property(e => e.PriceHigh);
        marketCandlestick.Property(e => e.PriceClose);
        marketCandlestick.Property(e => e.PriceMean);
        marketCandlestick.Property(e => e.PricePrevious);
        marketCandlestick.Property(e => e.PriceOpenDollars);
        marketCandlestick.Property(e => e.PriceLowDollars);
        marketCandlestick.Property(e => e.PriceHighDollars);
        marketCandlestick.Property(e => e.PriceCloseDollars);
        marketCandlestick.Property(e => e.PriceMeanDollars);
        marketCandlestick.Property(e => e.PricePreviousDollars);
        // Volume and open interest
        marketCandlestick.Property(e => e.Volume).IsRequired();
        marketCandlestick.Property(e => e.OpenInterest).IsRequired();
        marketCandlestick.Property(e => e.FetchedAt).IsRequired();

        // Analytics Market Features configuration
        var analyticsFeature = modelBuilder.Entity<AnalyticsMarketFeature>();
        analyticsFeature.ToTable("analytics_market_features");
        analyticsFeature.HasKey(e => e.FeatureId);
        analyticsFeature.Property(e => e.FeatureId).ValueGeneratedOnAdd();
        analyticsFeature.Property(e => e.Ticker).HasMaxLength(255).IsRequired();
        analyticsFeature.HasIndex(e => e.Ticker).HasDatabaseName("idx_analytics_market_features_ticker");
        analyticsFeature.Property(e => e.SeriesId).HasMaxLength(255).IsRequired();
        analyticsFeature.Property(e => e.EventTicker).HasMaxLength(255).IsRequired();
        analyticsFeature.Property(e => e.FeatureTime).IsRequired();
        analyticsFeature.HasIndex(e => e.FeatureTime).HasDatabaseName("idx_analytics_market_features_feature_time");
        // Time structure
        analyticsFeature.Property(e => e.TimeToCloseSeconds).IsRequired();
        analyticsFeature.Property(e => e.TimeToExpirationSeconds).IsRequired();
        // Prices in probability space
        analyticsFeature.Property(e => e.YesBidProb).IsRequired();
        analyticsFeature.Property(e => e.YesAskProb).IsRequired();
        analyticsFeature.Property(e => e.NoBidProb).IsRequired();
        analyticsFeature.Property(e => e.NoAskProb).IsRequired();
        analyticsFeature.Property(e => e.MidProb).IsRequired();
        analyticsFeature.Property(e => e.ImpliedProbYes).IsRequired();
        // Volatility / returns
        analyticsFeature.Property(e => e.Return1h).IsRequired();
        analyticsFeature.Property(e => e.Return24h).IsRequired();
        analyticsFeature.Property(e => e.Volatility1h).IsRequired();
        analyticsFeature.Property(e => e.Volatility24h).IsRequired();
        // Liquidity
        analyticsFeature.Property(e => e.BidAskSpread).IsRequired();
        analyticsFeature.Property(e => e.TopOfBookLiquidityYes).IsRequired();
        analyticsFeature.Property(e => e.TopOfBookLiquidityNo).IsRequired();
        analyticsFeature.Property(e => e.TotalLiquidityYes).IsRequired();
        analyticsFeature.Property(e => e.TotalLiquidityNo).IsRequired();
        analyticsFeature.Property(e => e.OrderbookImbalance).IsRequired();
        // Volume / activity
        analyticsFeature.Property(e => e.Volume1h).IsRequired();
        analyticsFeature.Property(e => e.Volume24h).IsRequired();
        analyticsFeature.Property(e => e.OpenInterest).IsRequired();
        analyticsFeature.Property(e => e.Notional1h).IsRequired();
        analyticsFeature.Property(e => e.Notional24h).IsRequired();
        // Categorical
        analyticsFeature.Property(e => e.Category).HasMaxLength(255).IsRequired();
        analyticsFeature.Property(e => e.MarketType).HasMaxLength(50).IsRequired();
        analyticsFeature.Property(e => e.Status).HasMaxLength(64).IsRequired();
        // External probability
        analyticsFeature.Property(e => e.FactualProbabilityYes).IsRequired();
        analyticsFeature.Property(e => e.MispriceScore).IsRequired();
        analyticsFeature.Property(e => e.GeneratedAt).IsRequired();
    }
}
