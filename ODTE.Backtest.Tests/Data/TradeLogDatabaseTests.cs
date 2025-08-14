using FluentAssertions;
using ODTE.Backtest.Core;
using ODTE.Backtest.Data;
using Xunit;

namespace ODTE.Backtest.Tests.Data;

public class TradeLogDatabaseTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly TradeLogDatabase _database;

    public TradeLogDatabaseTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"TestTradeLogs_{Guid.NewGuid()}");
        _database = new TradeLogDatabase(_testDbPath);
    }

    [Fact]
    public async Task LogTradeAsync_ValidTrade_ShouldPersistToDatabase()
    {
        // Arrange
        var tradeLog = new TradeLog(
            Timestamp: DateTime.UtcNow,
            Symbol: "XSP",
            Expiry: DateOnly.FromDateTime(DateTime.Today),
            Right: Right.Put,
            Strike: 530.0m,
            Type: SpreadType.CreditSpread,
            MaxLoss: 150.0m,
            ExitPnL: -75.0m,
            ExitReason: "Stop loss",
            MarketRegime: "Trending Down"
        );

        // Act
        await _database.LogTradeAsync(tradeLog);

        // Assert - Retrieve and verify the trade was persisted
        var trades = await _database.GetTradesByDateAsync(DateOnly.FromDateTime(DateTime.Today));
        trades.Should().HaveCount(1);
        
        var persistedTrade = trades[0];
        persistedTrade.Symbol.Should().Be("XSP");
        persistedTrade.Strike.Should().Be(530.0m);
        persistedTrade.Type.Should().Be(SpreadType.CreditSpread);
        persistedTrade.ExitPnL.Should().Be(-75.0m);
        persistedTrade.ExitReason.Should().Be("Stop loss");
        persistedTrade.MarketRegime.Should().Be("Trending Down");
    }

    [Fact]
    public async Task GetLosingTradesAsync_MultipleTrades_ShouldFilterLosers()
    {
        // Arrange
        var date = DateOnly.FromDateTime(DateTime.Today);
        var winningTrade = new TradeLog(DateTime.UtcNow, "XSP", date, Right.Put, 530m, SpreadType.CreditSpread, 100m, 25m, "Profitable exit", "Calm");
        var losingTrade = new TradeLog(DateTime.UtcNow, "XSP", date, Right.Call, 535m, SpreadType.CreditSpread, 100m, -75m, "Stop loss", "Volatile");
        
        await _database.LogTradeAsync(winningTrade);
        await _database.LogTradeAsync(losingTrade);

        // Act
        var losers = await _database.GetLosingTradesAsync(date);

        // Assert
        losers.Should().HaveCount(1);
        losers[0].ExitPnL.Should().Be(-75m);
        losers[0].ExitReason.Should().Be("Stop loss");
    }

    [Fact]
    public async Task GetDailySummaryAsync_MultipleTrades_ShouldCalculateMetrics()
    {
        // Arrange
        var date = DateOnly.FromDateTime(DateTime.Today);
        var trades = new[]
        {
            new TradeLog(DateTime.UtcNow, "XSP", date, Right.Put, 530m, SpreadType.CreditSpread, 100m, 25m, "Win", "Calm"),
            new TradeLog(DateTime.UtcNow, "XSP", date, Right.Call, 535m, SpreadType.CreditSpread, 150m, -75m, "Loss", "Volatile"),
            new TradeLog(DateTime.UtcNow, "XSP", date, Right.Put, 525m, SpreadType.CreditSpread, 100m, 40m, "Win", "Calm")
        };

        foreach (var trade in trades)
        {
            await _database.LogTradeAsync(trade);
        }

        // Act
        var summary = await _database.GetDailySummaryAsync(date);

        // Assert
        summary.TotalTrades.Should().Be(3);
        summary.WinningTrades.Should().Be(2);
        summary.WinRate.Should().BeApproximately(2.0 / 3.0, 0.01);
        summary.TotalPnL.Should().Be(-10m); // 25 - 75 + 40 = -10
        summary.WorstTrade.Should().Be(-75m);
        summary.BestTrade.Should().Be(40m);
        summary.TotalRiskDeployed.Should().Be(350m); // 100 + 150 + 100
    }

    public void Dispose()
    {
        _database?.Dispose();
        
        // Give a moment for connections to fully close
        Task.Delay(100).Wait();
        
        try
        {
            if (Directory.Exists(_testDbPath))
            {
                Directory.Delete(_testDbPath, true);
            }
        }
        catch (IOException)
        {
            // Ignore cleanup errors in tests
        }
    }
}