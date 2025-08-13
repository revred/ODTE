using Xunit;
using FluentAssertions;
using ODTE.Backtest.Engine;
using ODTE.Backtest.Config;
using ODTE.Backtest.Data;
using ODTE.Backtest.Signals;
using ODTE.Backtest.Strategy;
using ODTE.Backtest.Core;
using System.IO;

namespace ODTE.Backtest.Tests.Engine;

/// <summary>
/// Integration tests for the complete backtesting engine.
/// Tests the full workflow from data loading through trade execution to reporting.
/// </summary>
public class BacktesterIntegrationTests : IDisposable
{
    private readonly string _testDataDir;
    private readonly SimConfig _testConfig;

    public BacktesterIntegrationTests()
    {
        // Create temporary test data directory
        _testDataDir = Path.Combine(Path.GetTempPath(), $"ODTETest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDataDir);
        
        // Setup test configuration
        _testConfig = CreateTestConfig();
        
        // Create sample test data files
        CreateTestDataFiles();
    }

    [Fact]
    public async Task RunAsync_WithValidData_ShouldExecuteTrades()
    {
        // Arrange: Initialize all components
        var market = new CsvMarketData(_testConfig.Paths.BarsCsv, _testConfig.Timezone, _testConfig.RthOnly);
        var calendar = new CsvCalendar(_testConfig.Paths.CalendarCsv, _testConfig.Timezone);
        var options = new SyntheticOptionsData(_testConfig, market, _testConfig.Paths.VixCsv, _testConfig.Paths.Vix9dCsv);
        
        var scorer = new RegimeScorer(_testConfig);
        var builder = new SpreadBuilder(_testConfig);
        var exec = new ExecutionEngine(_testConfig);
        var risk = new RiskManager(_testConfig);
        
        var backtester = new Backtester(_testConfig, market, options, calendar, scorer, builder, exec, risk);
        
        // Act
        var report = await backtester.RunAsync();
        
        // Assert
        report.Should().NotBeNull("Backtest should produce a report");
        report.Trades.Should().NotBeNull("Report should contain trades list");
        report.NetPnL.Should().NotBe(0, "Should have non-zero P&L after running");
    }

    [Fact]
    public async Task RunAsync_ShouldRespectRiskLimits()
    {
        // Arrange: Configure strict risk limits
        _testConfig.Risk.MaxConcurrentPerSide = 1;
        _testConfig.Risk.DailyLossStop = 100;
        
        var market = new CsvMarketData(_testConfig.Paths.BarsCsv, _testConfig.Timezone, _testConfig.RthOnly);
        var calendar = new CsvCalendar(_testConfig.Paths.CalendarCsv, _testConfig.Timezone);
        var options = new SyntheticOptionsData(_testConfig, market, _testConfig.Paths.VixCsv, _testConfig.Paths.Vix9dCsv);
        
        var scorer = new RegimeScorer(_testConfig);
        var builder = new SpreadBuilder(_testConfig);
        var exec = new ExecutionEngine(_testConfig);
        var risk = new RiskManager(_testConfig);
        
        var backtester = new Backtester(_testConfig, market, options, calendar, scorer, builder, exec, risk);
        
        // Act
        var report = await backtester.RunAsync();
        
        // Assert
        // Verify risk limits were respected
        var maxConcurrentPositions = CalculateMaxConcurrentPositions(report.Trades);
        maxConcurrentPositions.Should().BeLessThanOrEqualTo(_testConfig.Risk.MaxConcurrentPerSide * 2,
            "Should not exceed max concurrent positions limit");
        
        var dailyLosses = CalculateDailyLosses(report.Trades);
        dailyLosses.All(loss => loss >= -_testConfig.Risk.DailyLossStop).Should().BeTrue(
            "Daily losses should not exceed configured stop");
    }

    [Fact]
    public async Task RunAsync_ShouldHandleStopLosses()
    {
        // Arrange
        _testConfig.Stops.CreditMultiple = 2.0; // Exit at 2x credit loss
        _testConfig.Stops.DeltaBreach = 0.30;   // Exit if delta exceeds 30
        
        var market = new CsvMarketData(_testConfig.Paths.BarsCsv, _testConfig.Timezone, _testConfig.RthOnly);
        var calendar = new CsvCalendar(_testConfig.Paths.CalendarCsv, _testConfig.Timezone);
        var options = new SyntheticOptionsData(_testConfig, market, _testConfig.Paths.VixCsv, _testConfig.Paths.Vix9dCsv);
        
        var scorer = new RegimeScorer(_testConfig);
        var builder = new SpreadBuilder(_testConfig);
        var exec = new ExecutionEngine(_testConfig);
        var risk = new RiskManager(_testConfig);
        
        var backtester = new Backtester(_testConfig, market, options, calendar, scorer, builder, exec, risk);
        
        // Act
        var report = await backtester.RunAsync();
        
        // Assert
        var stoppedTrades = report.Trades.Where(t => t.ExitReason.Contains("stop") || 
                                                     t.ExitReason.Contains("delta")).ToList();
        
        if (stoppedTrades.Any())
        {
            // Verify stop losses were executed properly
            foreach (var trade in stoppedTrades)
            {
                if (trade.ExitReason.Contains("stop"))
                {
                    var maxLoss = trade.Position.EntryPrice * _testConfig.Stops.CreditMultiple * 100;
                    Math.Abs(trade.PnL).Should().BeLessThanOrEqualTo(maxLoss + 10, // Allow for slippage
                        "Stop loss should limit losses to configured multiple");
                }
            }
        }
    }

    [Fact]
    public async Task RunAsync_WithEconomicEvents_ShouldBlockTrades()
    {
        // Arrange: Add economic event to calendar
        var eventTime = _testConfig.Start.AddDays(1).AddHours(14); // 2 PM on second day
        UpdateCalendarWithEvent(eventTime, "FOMC");
        
        var market = new CsvMarketData(_testConfig.Paths.BarsCsv, _testConfig.Timezone, _testConfig.RthOnly);
        var calendar = new CsvCalendar(_testConfig.Paths.CalendarCsv, _testConfig.Timezone);
        var options = new SyntheticOptionsData(_testConfig, market, _testConfig.Paths.VixCsv, _testConfig.Paths.Vix9dCsv);
        
        var scorer = new RegimeScorer(_testConfig);
        var builder = new SpreadBuilder(_testConfig);
        var exec = new ExecutionEngine(_testConfig);
        var risk = new RiskManager(_testConfig);
        
        var backtester = new Backtester(_testConfig, market, options, calendar, scorer, builder, exec, risk);
        
        // Act
        var report = await backtester.RunAsync();
        
        // Assert
        // Check that no trades were opened near the event window
        var blockStart = eventTime.AddMinutes(-_testConfig.Signals.EventBlockMinutesBefore);
        var blockEnd = eventTime.AddMinutes(_testConfig.Signals.EventBlockMinutesAfter);
        
        var tradesNearEvent = report.Trades
            .Where(t => t.Position.EntryTs >= blockStart && t.Position.EntryTs <= blockEnd)
            .ToList();
        
        tradesNearEvent.Should().BeEmpty("No trades should be opened during event blocking window");
    }

    [Fact]
    public void ExecutionEngine_ShouldModelSlippage()
    {
        // Arrange
        var exec = new ExecutionEngine(_testConfig);
        var order = new SpreadOrder
        {
            Type = PositionType.PutSpread,
            Short = new OptionQuote { Strike = 98, Right = Right.Put, Bid = 1.00, Ask = 1.10, Mid = 1.05 },
            Long = new OptionQuote { Strike = 97, Right = Right.Put, Bid = 0.60, Ask = 0.70, Mid = 0.65 },
            NetCredit = 0.40
        };
        
        var entryTime = new DateTime(2024, 2, 1, 10, 0, 0);
        
        // Act
        var fillPrice = exec.GetFillPrice(order, entryTime, isEntry: true);
        
        // Assert
        fillPrice.Should().BeLessThan(order.NetCredit, "Slippage should reduce credit received");
        fillPrice.Should().BeGreaterThan(0, "Fill price should be positive for credit spread");
        
        var expectedSlippage = _testConfig.Slippage.EntryHalfSpreadTicks * _testConfig.Slippage.TickValue * 2;
        var actualSlippage = order.NetCredit - fillPrice;
        actualSlippage.Should().BeApproximately(expectedSlippage, 0.01, 
            "Slippage should match configured ticks");
    }

    [Fact]
    public void RiskManager_ShouldEnforcePositionLimits()
    {
        // Arrange
        var risk = new RiskManager(_testConfig);
        
        // Open maximum allowed positions
        for (int i = 0; i < _testConfig.Risk.MaxConcurrentPerSide; i++)
        {
            risk.RegisterOpen(PositionType.PutSpread);
        }
        
        // Act
        bool canOpenAnother = risk.CanOpen(PositionType.PutSpread);
        
        // Assert
        canOpenAnother.Should().BeFalse("Should not allow opening beyond position limit");
        
        // Close one position
        risk.RegisterClose(PositionType.PutSpread, pnl: 50);
        
        // Now should be able to open
        bool canOpenAfterClose = risk.CanOpen(PositionType.PutSpread);
        canOpenAfterClose.Should().BeTrue("Should allow opening after closing a position");
    }

    [Fact]
    public void RiskManager_ShouldTriggerDailyStopLoss()
    {
        // Arrange
        var risk = new RiskManager(_testConfig);
        
        // Register losses approaching daily limit
        risk.RegisterClose(PositionType.PutSpread, pnl: -_testConfig.Risk.DailyLossStop * 0.8);
        
        // Act & Assert: Should still allow trading
        risk.IsLockedToday().Should().BeFalse("Should allow trading before hitting stop");
        
        // Register loss that exceeds limit
        risk.RegisterClose(PositionType.CallSpread, pnl: -_testConfig.Risk.DailyLossStop * 0.3);
        
        // Should now be locked
        risk.IsLockedToday().Should().BeTrue("Should lock trading after exceeding daily loss limit");
    }

    // Helper methods
    private SimConfig CreateTestConfig()
    {
        return new SimConfig
        {
            Underlying = "XSP",
            Start = new DateTime(2024, 2, 1),
            End = new DateTime(2024, 2, 5),
            Mode = "prototype",
            RthOnly = true,
            Timezone = "America/New_York",
            CadenceSeconds = 900,
            NoNewRiskMinutesToClose = 40,
            
            ShortDelta = new DeltaConfig
            {
                CondorMin = 0.07,
                CondorMax = 0.15,
                SingleMin = 0.10,
                SingleMax = 0.20
            },
            
            WidthPoints = new WidthConfig { Min = 1, Max = 2 },
            CreditPerWidthMin = new CreditConfig { Condor = 0.18, Single = 0.20 },
            
            Stops = new StopsConfig
            {
                CreditMultiple = 2.2,
                DeltaBreach = 0.33
            },
            
            Risk = new RiskConfig
            {
                DailyLossStop = 500,
                PerTradeMaxLossCap = 200,
                MaxConcurrentPerSide = 2
            },
            
            Slippage = new SlippageConfig
            {
                EntryHalfSpreadTicks = 0.5,
                ExitHalfSpreadTicks = 0.5,
                LateSessionExtraTicks = 0.5,
                TickValue = 0.05,
                SpreadPctCap = 0.25
            },
            
            Fees = new FeesConfig
            {
                CommissionPerContract = 0.65,
                ExchangeFeesPerContract = 0.25
            },
            
            Signals = new SignalsConfig
            {
                OrMinutes = 15,
                VwapWindowMinutes = 30,
                AtrPeriodBars = 20,
                EventBlockMinutesBefore = 60,
                EventBlockMinutesAfter = 15
            },
            
            Paths = new PathsConfig
            {
                BarsCsv = Path.Combine(_testDataDir, "bars.csv"),
                VixCsv = Path.Combine(_testDataDir, "vix.csv"),
                Vix9dCsv = Path.Combine(_testDataDir, "vix9d.csv"),
                CalendarCsv = Path.Combine(_testDataDir, "calendar.csv"),
                ReportsDir = Path.Combine(_testDataDir, "reports")
            }
        };
    }

    private void CreateTestDataFiles()
    {
        // Create bars data
        var barsContent = "ts,o,h,l,c,v\n";
        var basePrice = 100.0;
        var startDate = _testConfig.Start;
        
        for (int day = 0; day < 5; day++)
        {
            var currentDate = startDate.AddDays(day);
            for (int minute = 0; minute < 390; minute += 5) // Market hours: 9:30 AM - 4:00 PM
            {
                var ts = currentDate.AddHours(9).AddMinutes(30 + minute);
                var price = basePrice + Math.Sin(minute * 0.1) * 2 + day * 0.5;
                var high = price + 0.2;
                var low = price - 0.2;
                var close = price + 0.1;
                var volume = 10000 + minute * 10;
                
                barsContent += $"{ts:yyyy-MM-dd HH:mm:ss},{price:F2},{high:F2},{low:F2},{close:F2},{volume}\n";
            }
        }
        File.WriteAllText(_testConfig.Paths.BarsCsv, barsContent);
        
        // Create VIX data
        var vixContent = "date,vix\n";
        for (int day = 0; day < 5; day++)
        {
            var date = startDate.AddDays(day);
            var vixValue = 15 + day * 0.5;
            vixContent += $"{date:yyyy-MM-dd},{vixValue:F2}\n";
        }
        File.WriteAllText(_testConfig.Paths.VixCsv, vixContent);
        
        // Create VIX9D data
        var vix9dContent = "date,vix9d\n";
        for (int day = 0; day < 5; day++)
        {
            var date = startDate.AddDays(day);
            var vix9dValue = 14 + day * 0.4;
            vix9dContent += $"{date:yyyy-MM-dd},{vix9dValue:F2}\n";
        }
        File.WriteAllText(_testConfig.Paths.Vix9dCsv, vix9dContent);
        
        // Create empty calendar (no events initially)
        var calendarContent = "ts,kind\n";
        File.WriteAllText(_testConfig.Paths.CalendarCsv, calendarContent);
        
        // Create reports directory
        Directory.CreateDirectory(_testConfig.Paths.ReportsDir);
    }

    private void UpdateCalendarWithEvent(DateTime eventTime, string eventKind)
    {
        var calendarContent = $"ts,kind\n{eventTime:yyyy-MM-dd HH:mm:ss},{eventKind}\n";
        File.WriteAllText(_testConfig.Paths.CalendarCsv, calendarContent);
    }

    private int CalculateMaxConcurrentPositions(List<TradeResult> trades)
    {
        if (!trades.Any()) return 0;
        
        var events = new List<(DateTime time, int change)>();
        foreach (var trade in trades)
        {
            events.Add((trade.Position.EntryTs, 1));
            events.Add((trade.Position.ExitTs, -1));
        }
        
        events = events.OrderBy(e => e.time).ToList();
        
        int current = 0;
        int max = 0;
        foreach (var (_, change) in events)
        {
            current += change;
            max = Math.Max(max, current);
        }
        
        return max;
    }

    private List<double> CalculateDailyLosses(List<TradeResult> trades)
    {
        var dailyPnL = trades
            .GroupBy(t => t.Position.ExitTs.Date)
            .Select(g => g.Sum(t => t.PnL))
            .ToList();
        
        return dailyPnL;
    }

    public void Dispose()
    {
        // Clean up test data directory
        if (Directory.Exists(_testDataDir))
        {
            Directory.Delete(_testDataDir, recursive: true);
        }
    }
}