using FluentAssertions;
using Kalshi.Api;
using Kalshi.Api.Model;
using KSignal.API.Data;
using KSignal.API.Messaging;
using KSignal.API.Services;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KSignal.API.Tests.Services;

/// <summary>
/// Comprehensive unit tests for SynchronizationService
/// Testing all methods, parameters, conditions, and edge cases
/// Note: Tests focus on mockable dependencies (Redis, MassTransit, validation logic)
/// Full API integration tests would require TestContainers for Kalshi API simulation
/// </summary>
public class SynchronizationServiceTests
{
    private readonly KalshiClient _kalshiClient;
    private readonly Mock<KalshiDbContext> _mockDbContext;
    private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
    private readonly Mock<ILockService> _mockLockService;
    private readonly Mock<ILogger<SynchronizationService>> _mockLogger;
    private readonly SynchronizationService _service;

    public SynchronizationServiceTests()
    {
        // Create real KalshiClient (API calls won't be made in these unit tests)
        _kalshiClient = new KalshiClient();

        // Create DbContext mock with in-memory options
        var options = new DbContextOptionsBuilder<KalshiDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _mockDbContext = new Mock<KalshiDbContext>(options);

        _mockPublishEndpoint = new Mock<IPublishEndpoint>();
        _mockLockService = new Mock<ILockService>();
        _mockLogger = new Mock<ILogger<SynchronizationService>>();

        // Create service instance
        _service = new SynchronizationService(
            _kalshiClient,
            _mockDbContext.Object,
            _mockPublishEndpoint.Object,
            _mockLockService.Object,
            _mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullKalshiClient_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new SynchronizationService(
            null!,
            _mockDbContext.Object,
            _mockPublishEndpoint.Object,
            _mockLockService.Object,
            _mockLogger.Object));

        ex.ParamName.Should().Be("kalshiClient");
    }

    [Fact]
    public void Constructor_WithNullDbContext_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new SynchronizationService(
            _kalshiClient,
            null!,
            _mockPublishEndpoint.Object,
            _mockLockService.Object,
            _mockLogger.Object));

        ex.ParamName.Should().Be("dbContext");
    }

    [Fact]
    public void Constructor_WithNullPublishEndpoint_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new SynchronizationService(
            _kalshiClient,
            _mockDbContext.Object,
            null!,
            _mockLockService.Object,
            _mockLogger.Object));

        ex.ParamName.Should().Be("publishEndpoint");
    }

    [Fact]
    public void Constructor_WithNullLockService_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new SynchronizationService(
            _kalshiClient,
            _mockDbContext.Object,
            _mockPublishEndpoint.Object,
            null!,
            _mockLogger.Object));

        ex.ParamName.Should().Be("lockService");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new SynchronizationService(
            _kalshiClient,
            _mockDbContext.Object,
            _mockPublishEndpoint.Object,
            _mockLockService.Object,
            null!));

        ex.ParamName.Should().Be("logger");
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange, Act & Assert
        _service.Should().NotBeNull();
    }

    #endregion

    #region EnqueueMarketSyncAsync Tests

    [Fact]
    public async Task EnqueueMarketSyncAsync_WhenLockCannotBeAcquired_ThrowsInvalidOperationException()
    {
        // Arrange
        _mockLockService
            .Setup(x => x.AcquireWithCounterAsync(
                "sync:market-snapshots:lock",
                "sync:market-snapshots:pending",
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var act = async () => await _service.EnqueueMarketSyncAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already in progress*");
    }

    [Fact]
    public async Task EnqueueMarketSyncAsync_WithSpecificMarketTickerId_EnqueuesOnlyOneMessage()
    {
        // Arrange
        var tickerId = "TEST-MARKET-123";
        _mockLockService
            .Setup(x => x.AcquireWithCounterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockLockService
            .Setup(x => x.IncrementJobCounterAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _service.EnqueueMarketSyncAsync(marketTickerId: tickerId);

        // Assert
        _mockPublishEndpoint.Verify(x => x.Publish(
            It.Is<SynchronizeMarketData>(m => m.MarketTickerId == tickerId),
            It.IsAny<CancellationToken>()), Times.Once);
        _mockLockService.Verify(x => x.IncrementJobCounterAsync(
            "sync:market-snapshots:pending", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnqueueMarketSyncAsync_WithFilters_EnqueuesFilteredMessage()
    {
        // Arrange
        long minTs = 1000000;
        long maxTs = 2000000;
        string status = "active";

        _mockLockService
            .Setup(x => x.AcquireWithCounterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockLockService
            .Setup(x => x.IncrementJobCounterAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _service.EnqueueMarketSyncAsync(minCreatedTs: minTs, maxCreatedTs: maxTs, status: status);

        // Assert
        _mockPublishEndpoint.Verify(x => x.Publish(
            It.Is<SynchronizeMarketData>(m =>
                m.MinCreatedTs == minTs &&
                m.MaxCreatedTs == maxTs &&
                m.Status == status),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null, 2000000L, null)]
    [InlineData(1000000L, null, null)]
    [InlineData(null, null, "active")]
    public async Task EnqueueMarketSyncAsync_WithAnyFilter_EnqueuesSingleMessage(long? minTs, long? maxTs, string? status)
    {
        // Arrange
        _mockLockService
            .Setup(x => x.AcquireWithCounterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockLockService
            .Setup(x => x.IncrementJobCounterAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _service.EnqueueMarketSyncAsync(minCreatedTs: minTs, maxCreatedTs: maxTs, status: status);

        // Assert
        _mockPublishEndpoint.Verify(x => x.Publish(
            It.IsAny<SynchronizeMarketData>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnqueueMarketSyncAsync_OnException_ReleasesLockAndResetsCounter()
    {
        // Arrange
        _mockLockService
            .Setup(x => x.AcquireWithCounterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockPublishEndpoint
            .Setup(x => x.Publish(It.IsAny<SynchronizeMarketData>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var act = async () => await _service.EnqueueMarketSyncAsync(marketTickerId: "TEST");

        // Assert
        await act.Should().ThrowAsync<Exception>();
        _mockLockService.Verify(x => x.ReleaseWithCounterAsync(
            "sync:market-snapshots:lock",
            "sync:market-snapshots:pending",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnqueueMarketSyncAsync_InitializesCounterToZero()
    {
        // Arrange
        _mockLockService
            .Setup(x => x.AcquireWithCounterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockLockService
            .Setup(x => x.IncrementJobCounterAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _service.EnqueueMarketSyncAsync(marketTickerId: "TEST");

        // Assert
        _mockLockService.Verify(x => x.AcquireWithCounterAsync(
            "sync:market-snapshots:lock",
            "sync:market-snapshots:pending",
            It.IsAny<TimeSpan>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnqueueMarketSyncAsync_AcquiresLockWith30MinuteExpiration()
    {
        // Arrange
        _mockLockService
            .Setup(x => x.AcquireWithCounterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockLockService
            .Setup(x => x.IncrementJobCounterAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _service.EnqueueMarketSyncAsync(marketTickerId: "TEST");

        // Assert
        _mockLockService.Verify(x => x.AcquireWithCounterAsync(
            "sync:market-snapshots:lock",
            "sync:market-snapshots:pending",
            TimeSpan.FromMinutes(30),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region EnqueueTagsCategoriesSyncAsync Tests

    [Fact]
    public async Task EnqueueTagsCategoriesSyncAsync_PublishesMessage()
    {
        // Act
        await _service.EnqueueTagsCategoriesSyncAsync();

        // Assert
        _mockPublishEndpoint.Verify(x => x.Publish(
            It.IsAny<SynchronizeTagsCategories>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnqueueTagsCategoriesSyncAsync_OnException_RethrowsException()
    {
        // Arrange
        _mockPublishEndpoint
            .Setup(x => x.Publish(It.IsAny<SynchronizeTagsCategories>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var act = async () => await _service.EnqueueTagsCategoriesSyncAsync();

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("Test exception");
    }

    [Fact]
    public async Task EnqueueTagsCategoriesSyncAsync_WithCancellationToken_PropagatesToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var token = cts.Token;

        // Act
        await _service.EnqueueTagsCategoriesSyncAsync(token);

        // Assert
        _mockPublishEndpoint.Verify(x => x.Publish(
            It.IsAny<SynchronizeTagsCategories>(),
            token), Times.Once);
    }

    #endregion

    #region EnqueueSeriesSyncAsync Tests

    [Fact]
    public async Task EnqueueSeriesSyncAsync_PublishesMessage()
    {
        // Act
        await _service.EnqueueSeriesSyncAsync();

        // Assert
        _mockPublishEndpoint.Verify(x => x.Publish(
            It.IsAny<SynchronizeSeries>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnqueueSeriesSyncAsync_OnException_RethrowsException()
    {
        // Arrange
        _mockPublishEndpoint
            .Setup(x => x.Publish(It.IsAny<SynchronizeSeries>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Network error"));

        // Act
        var act = async () => await _service.EnqueueSeriesSyncAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Network error");
    }

    #endregion

    #region EnqueueEventsSyncAsync Tests

    // Note: EnqueueEventsSyncAsync requires complex DbContext setup with async queries
    // These tests are better suited for integration tests with TestContainers
    // The method interacts directly with EF Core's DbSet which is difficult to mock properly

    #endregion

    #region EnqueueEventDetailSyncAsync Tests

    [Fact]
    public async Task EnqueueEventDetailSyncAsync_WithValidTicker_PublishesMessage()
    {
        // Arrange
        var eventTicker = "EVENT-TEST-123";

        // Act
        await _service.EnqueueEventDetailSyncAsync(eventTicker);

        // Assert
        _mockPublishEndpoint.Verify(x => x.Publish(
            It.Is<SynchronizeEventDetail>(e => e.EventTicker == eventTicker),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task EnqueueEventDetailSyncAsync_WithInvalidTicker_ThrowsArgumentException(string? eventTicker)
    {
        // Act
        var act = async () => await _service.EnqueueEventDetailSyncAsync(eventTicker!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("eventTicker");
    }

    [Fact]
    public async Task EnqueueEventDetailSyncAsync_OnPublishError_RethrowsException()
    {
        // Arrange
        var eventTicker = "EVENT-TEST";
        _mockPublishEndpoint
            .Setup(x => x.Publish(It.IsAny<SynchronizeEventDetail>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("MassTransit timeout"));

        // Act
        var act = async () => await _service.EnqueueEventDetailSyncAsync(eventTicker);

        // Assert
        await act.Should().ThrowAsync<TimeoutException>();
    }

    #endregion

    #region EnqueueOrderbookSyncAsync Tests

    [Fact]
    public async Task EnqueueOrderbookSyncAsync_PublishesMessage()
    {
        // Act
        await _service.EnqueueOrderbookSyncAsync();

        // Assert
        _mockPublishEndpoint.Verify(x => x.Publish(
            It.IsAny<SynchronizeOrderbook>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnqueueOrderbookSyncAsync_OnException_RethrowsException()
    {
        // Arrange
        _mockPublishEndpoint
            .Setup(x => x.Publish(It.IsAny<SynchronizeOrderbook>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Publish failed"));

        // Act
        var act = async () => await _service.EnqueueOrderbookSyncAsync();

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("Publish failed");
    }

    #endregion

    #region EnqueueCandlesticksSyncAsync Tests

    [Fact]
    public async Task EnqueueCandlesticksSyncAsync_PublishesMessage()
    {
        // Act
        await _service.EnqueueCandlesticksSyncAsync();

        // Assert
        _mockPublishEndpoint.Verify(x => x.Publish(
            It.IsAny<SynchronizeCandlesticks>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnqueueCandlesticksSyncAsync_OnException_RethrowsException()
    {
        // Arrange
        _mockPublishEndpoint
            .Setup(x => x.Publish(It.IsAny<SynchronizeCandlesticks>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Queue error"));

        // Act
        var act = async () => await _service.EnqueueCandlesticksSyncAsync();

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("Queue error");
    }

    #endregion
}
