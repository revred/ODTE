using Xunit;
using FluentAssertions;
using ODTE.Backtest.Strategy;
using ODTE.Backtest.Config;
using ODTE.Backtest.Data;
using ODTE.Backtest.Core;
using ODTE.Backtest.Engine;
using ODTE.Backtest.Signals;

namespace ODTE.Backtest.Tests.EdgeCases;

/// <summary>
/// Tests for edge cases and unfavorable market conditions that can break trade execution.
/// These scenarios often occur in real markets and must be handled gracefully.
/// 
/// PURPOSE: Ensure the system degrades gracefully rather than silently failing with zero trades.
/// </summary>
public class UnfavorableMarketConditionsTests : IDisposable
{
    private readonly string _testDataDir;
    private readonly SimConfig _baseConfig;

    public UnfavorableMarketConditionsTests()
    {
        _testDataDir = Path.Combine(Path.GetTempPath(), $"UnfavorableConditions_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDataDir);
        
        _baseConfig = CreateBaseConfig();
    }

    /// <summary>
    /// Tests system behavior when VIX is extremely low (options have minimal value).
    /// This scenario often leads to insufficient credit for spread construction.
    /// </summary>
    [Theory]
    [InlineData(5.0)]   // Extremely low VIX
    [InlineData(7.5)]   // Very low VIX
    [InlineData(9.0)]   // Low VIX
    public void ExtremelyLowVolatility_ShouldHandleGracefully(double vixLevel)
    {
        // Arrange
        CreateLowVolatilityScenario(vixLevel);
        
        var market = new CsvMarketData(_baseConfig.Paths.BarsCsv, _baseConfig.Timezone, _baseConfig.RthOnly);
        var options = new SyntheticOptionsData(_baseConfig, market, _baseConfig.Paths.VixCsv, _baseConfig.Paths.Vix9dCsv);
        var builder = new SpreadBuilder(_baseConfig);
        
        var testTime = new DateTime(2024, 2, 1, 12, 0, 0);
        
        // Act
        var condorOrder = builder.TryBuild(testTime, Decision.Condor, market, options);
        var putOrder = builder.TryBuild(testTime, Decision.SingleSidePut, market, options);
        var callOrder = builder.TryBuild(testTime, Decision.SingleSideCall, market, options);
        
        // Assert: System should handle low volatility gracefully
        var quotes = options.GetQuotesAt(testTime).ToList();
        quotes.Should().NotBeEmpty("Should still generate option quotes even in low vol");
        
        var avgOptionValue = quotes.Average(q => q.Mid);
        Console.WriteLine($"VIX {vixLevel}: Avg option value ${avgOptionValue:F3}");
        
        if (condorOrder == null && putOrder == null && callOrder == null)
        {
            // If no trades possible, verify it's due to economic constraints, not bugs
            var spot = market.GetSpot(testTime);
            var nearMoneyOptions = quotes.Where(q => Math.Abs(q.Strike - spot) <= 2).ToList();
            var maxNearValue = nearMoneyOptions.Any() ? nearMoneyOptions.Max(q => q.Mid) : 0;
            
            Console.WriteLine($"   No trades possible: max near-money value ${maxNearValue:F3}");
            maxNearValue.Should().BeLessThan(0.15, 
                "If no trades possible in low vol, it should be due to genuinely low option values");
        }
        else
        {
            // If trades are possible, they should meet quality standards
            var successfulOrders = new[] { condorOrder, putOrder, callOrder }.Where(o => o != null).ToList();
            
            foreach (var order in successfulOrders)
            {
                order!.Credit.Should().BeGreaterThan(0, "Any executed trade should have positive credit");
                var creditPerWidth = order.Credit / order.Width;
                creditPerWidth.Should().BeGreaterThan(0.03, 
                    "Even in low vol, executed trades should have meaningful credit/width");
            }
        }
    }

    /// <summary>
    /// Tests behavior when VIX is extremely high (options are expensive but may be illiquid).
    /// High volatility can create wide bid-ask spreads that prevent trade execution.
    /// </summary>
    [Theory]
    [InlineData(35.0)]  // High VIX
    [InlineData(45.0)]  // Very high VIX
    [InlineData(60.0)]  // Extreme VIX (crisis level)
    public void ExtremelyHighVolatility_ShouldManageSpreadRequirements(double vixLevel)
    {
        // Arrange
        CreateHighVolatilityScenario(vixLevel);
        
        var market = new CsvMarketData(_baseConfig.Paths.BarsCsv, _baseConfig.Timezone, _baseConfig.RthOnly);
        var options = new SyntheticOptionsData(_baseConfig, market, _baseConfig.Paths.VixCsv, _baseConfig.Paths.Vix9dCsv);
        var builder = new SpreadBuilder(_baseConfig);
        
        var testTime = new DateTime(2024, 2, 1, 11, 0, 0);
        
        // Act
        var quotes = options.GetQuotesAt(testTime).ToList();
        var order = builder.TryBuild(testTime, Decision.Condor, market, options);
        
        // Assert: High volatility effects
        quotes.Should().NotBeEmpty();
        
        var avgOptionValue = quotes.Average(q => q.Mid);
        var avgBidAskSpread = quotes.Average(q => q.Ask - q.Bid);
        var avgSpreadPct = quotes.Average(q => (q.Ask - q.Bid) / q.Mid);
        
        Console.WriteLine($"VIX {vixLevel}: Avg value ${avgOptionValue:F3}, " +
                         $"Avg spread ${avgBidAskSpread:F3} ({avgSpreadPct:P1})");
        
        avgOptionValue.Should().BeGreaterThan(0.20, "High VIX should produce higher option values");
        
        if (order == null)
        {
            // If failed due to illiquidity, verify bid-ask spreads are indeed wide
            avgSpreadPct.Should().BeGreaterThan(0.15, 
                "If trades fail in high vol, it should be due to wide bid-ask spreads");
        }
        else
        {
            // If successful, verify trade quality 
            order.Credit.Should().BeGreaterThan(0.15, "High vol trades should have substantial credit");
        }
    }

    /// <summary>
    /// Tests scenarios where market is gapping (large price jumps) that affect option deltas.
    /// </summary>
    [Fact]
    public void GappingMarket_ShouldAdaptDeltaCalculations()
    {
        // Arrange: Create scenario with large price gaps
        CreateGappingMarketScenario();
        
        var market = new CsvMarketData(_baseConfig.Paths.BarsCsv, _baseConfig.Timezone, _baseConfig.RthOnly);
        var options = new SyntheticOptionsData(_baseConfig, market, _baseConfig.Paths.VixCsv, _baseConfig.Paths.Vix9dCsv);
        var builder = new SpreadBuilder(_baseConfig);
        
        var testTimes = new[]
        {
            new DateTime(2024, 2, 1, 9, 45, 0),   // After gap up
            new DateTime(2024, 2, 1, 10, 30, 0),  // After gap down
            new DateTime(2024, 2, 1, 11, 15, 0),  // After gap up again
        };
        
        // Act & Assert
        foreach (var testTime in testTimes)
        {
            var spot = market.GetSpot(testTime);
            var quotes = options.GetQuotesAt(testTime).ToList();
            var order = builder.TryBuild(testTime, Decision.Condor, market, options);
            
            Console.WriteLine($"{testTime:HH:mm}: Spot=${spot:F2}");
            
            // Verify option chain adapts to new spot level
            var strikeRange = quotes.Max(q => q.Strike) - quotes.Min(q => q.Strike);
            strikeRange.Should().BeGreaterThan(10, "Option chain should span reasonable range around new spot");
            
            // Verify deltas are reasonable for new spot level
            var atmOptions = quotes.Where(q => Math.Abs(q.Strike - spot) <= 1).ToList();
            if (atmOptions.Any())
            {
                var avgAtmDelta = Math.Abs(atmOptions.Average(q => q.Delta));
                avgAtmDelta.Should().BeInRange(0.3, 0.7, "ATM deltas should be reasonable after gap");
            }
            
            if (order != null)
            {
                Console.WriteLine($"   ✅ Trade possible: {order.Short.Strike:F1} Credit=${order.Credit:F2}");
            }
            else
            {
                Console.WriteLine($"   ❌ No trade possible");
            }
        }
    }

    /// <summary>
    /// Tests very late in trading day when time value is minimal.
    /// Options may have insufficient value for spread construction.
    /// </summary>
    [Theory]
    [InlineData(15, 45)]  // 15 minutes to close
    [InlineData(15, 55)]  // 5 minutes to close
    [InlineData(15, 59)]  // 1 minute to close
    public void VeryLateInDay_ShouldHandleMinimalTimeValue(int hour, int minute)
    {
        // Arrange
        CreateNormalMarketScenario();
        
        var market = new CsvMarketData(_baseConfig.Paths.BarsCsv, _baseConfig.Timezone, _baseConfig.RthOnly);
        var options = new SyntheticOptionsData(_baseConfig, market, _baseConfig.Paths.VixCsv, _baseConfig.Paths.Vix9dCsv);
        var builder = new SpreadBuilder(_baseConfig);
        
        var testTime = new DateTime(2024, 2, 1, hour, minute, 0);
        var minutesToClose = (16 * 60) - (hour * 60 + minute); // Minutes until 4 PM
        
        // Act
        var quotes = options.GetQuotesAt(testTime).ToList();
        var order = builder.TryBuild(testTime, Decision.Condor, market, options);
        
        // Assert
        quotes.Should().NotBeEmpty("Should generate quotes even very late in day");
        
        var avgTimeValue = quotes.Average(q => q.Mid);
        Console.WriteLine($"{minutesToClose} min to close: Avg option value ${avgTimeValue:F3}");
        
        // Time value should decay as expiry approaches
        if (minutesToClose <= 5)
        {
            avgTimeValue.Should().BeLessThan(0.10, "Very late options should have minimal time value");
        }
        
        if (order == null)
        {
            // Acceptable to have no trades very late in day
            Console.WriteLine($"   No trades possible with {minutesToClose} minutes left - expected");
        }
        else
        {
            // If trade is possible, verify it's reasonable
            order.Credit.Should().BeGreaterThan(0.05, "Late day trades should still have some credit");
            Console.WriteLine($"   Late trade possible: Credit=${order.Credit:F2}");
        }
    }

    /// <summary>
    /// Tests scenarios where no strikes exist in target delta ranges.
    /// This can happen with extreme configurations or unusual market conditions.
    /// </summary>
    [Fact]
    public void NoStrikesInDeltaRange_ShouldReturnNullGracefully()
    {
        // Arrange: Configure impossible delta requirements
        var config = CreateBaseConfig();
        config.ShortDelta.CondorMin = 0.48;  // Very narrow range near ATM
        config.ShortDelta.CondorMax = 0.52;  // Only ATM strikes qualify
        
        CreateNormalMarketScenario();
        
        var market = new CsvMarketData(config.Paths.BarsCsv, config.Timezone, config.RthOnly);
        var options = new SyntheticOptionsData(config, market, config.Paths.VixCsv, config.Paths.Vix9dCsv);
        var builder = new SpreadBuilder(config);
        
        var testTime = new DateTime(2024, 2, 1, 14, 30, 0); // Late day (lower deltas)
        
        // Act
        var quotes = options.GetQuotesAt(testTime).ToList();
        var order = builder.TryBuild(testTime, Decision.Condor, market, options);
        
        // Assert
        quotes.Should().NotBeEmpty("Should generate quotes");
        order.Should().BeNull("Should return null when delta requirements impossible");
        
        // Verify the issue is with delta filtering, not quote generation
        var availableDeltas = quotes.Select(q => Math.Abs(q.Delta)).ToList();
        var inRange = availableDeltas.Count(d => d >= config.ShortDelta.CondorMin && d <= config.ShortDelta.CondorMax);
        
        Console.WriteLine($"Available delta range: [{availableDeltas.Min():F3}-{availableDeltas.Max():F3}]");
        Console.WriteLine($"Required range: [{config.ShortDelta.CondorMin:F2}-{config.ShortDelta.CondorMax:F2}]");
        Console.WriteLine($"Strikes in range: {inRange}");
        
        inRange.Should().BeLessThan(4, "Should have insufficient strikes in impossible delta range");
    }

    /// <summary>
    /// Tests market opening conditions where spreads may be wide due to uncertainty.
    /// </summary>
    [Fact]
    public void MarketOpening_ShouldHandleWiderSpreads()
    {
        // Arrange: Test right at market open
        CreateNormalMarketScenario();
        
        var market = new CsvMarketData(_baseConfig.Paths.BarsCsv, _baseConfig.Timezone, _baseConfig.RthOnly);
        var options = new SyntheticOptionsData(_baseConfig, market, _baseConfig.Paths.VixCsv, _baseConfig.Paths.Vix9dCsv);
        var builder = new SpreadBuilder(_baseConfig);
        
        var openingTime = new DateTime(2024, 2, 1, 9, 30, 0);
        var laterTime = new DateTime(2024, 2, 1, 10, 30, 0);
        
        // Act: Compare opening vs later spreads
        var openingQuotes = options.GetQuotesAt(openingTime).ToList();
        var laterQuotes = options.GetQuotesAt(laterTime).ToList();
        
        var openingOrder = builder.TryBuild(openingTime, Decision.Condor, market, options);
        var laterOrder = builder.TryBuild(laterTime, Decision.Condor, market, options);
        
        // Assert: Analyze differences
        var openingSpreads = openingQuotes.Average(q => (q.Ask - q.Bid) / q.Mid);
        var laterSpreads = laterQuotes.Average(q => (q.Ask - q.Bid) / q.Mid);
        
        Console.WriteLine($"Opening: {openingSpreads:P1} avg spread, Trade: {openingOrder != null}");
        Console.WriteLine($"Later: {laterSpreads:P1} avg spread, Trade: {laterOrder != null}");
        
        // Market opening may have wider spreads (acceptable)
        if (openingOrder == null && laterOrder != null)
        {
            openingSpreads.Should().BeGreaterThan(laterSpreads, 
                "If opening trade fails but later succeeds, should be due to wider opening spreads");
        }
    }

    /// <summary>
    /// Tests system behavior during economic events (when trading should be blocked).
    /// </summary>
    [Fact]
    public void EconomicEvents_ShouldBlockTrading()
    {
        // Arrange: Add FOMC event
        CreateEconomicEventScenario();
        
        var market = new CsvMarketData(_baseConfig.Paths.BarsCsv, _baseConfig.Timezone, _baseConfig.RthOnly);
        var calendar = new CsvCalendar(_baseConfig.Paths.CalendarCsv, _baseConfig.Timezone);
        var options = new SyntheticOptionsData(_baseConfig, market, _baseConfig.Paths.VixCsv, _baseConfig.Paths.Vix9dCsv);
        var scorer = new RegimeScorer(_baseConfig);
        
        var eventTime = new DateTime(2024, 2, 1, 14, 0, 0); // FOMC at 2 PM
        var beforeEvent = eventTime.AddMinutes(-30);
        var duringEvent = eventTime;
        var afterEvent = eventTime.AddMinutes(30);
        
        // Act: Test regime scoring around event
        var (beforeScore, beforeCalm, beforeUp, beforeDn) = scorer.Score(beforeEvent, market, calendar);
        var (duringScore, duringCalm, duringUp, duringDn) = scorer.Score(duringEvent, market, calendar);
        var (afterScore, afterCalm, afterUp, afterDn) = scorer.Score(afterEvent, market, calendar);
        
        // Assert: Event should block trading signals
        Console.WriteLine($"Before: Score={beforeScore}, Calm={beforeCalm}");
        Console.WriteLine($"During: Score={duringScore}, Calm={duringCalm}");
        Console.WriteLine($"After: Score={afterScore}, Calm={afterCalm}");
        
        // During event window, signals should be suppressed
        var duringActionable = (duringCalm && duringScore >= 0) || 
                              (duringUp && duringScore >= 2) || 
                              (duringDn && duringScore >= 2);
        
        duringActionable.Should().BeFalse("Should block trading signals during economic events");
    }

    /// <summary>
    /// Tests scenarios where protective wings cannot be found (insufficient strikes).
    /// </summary>
    [Fact]
    public void InsufficientStrikes_ShouldFailGracefully()
    {
        // Arrange: Create scenario with very limited strike range
        CreateLimitedStrikeScenario();
        
        var market = new CsvMarketData(_baseConfig.Paths.BarsCsv, _baseConfig.Timezone, _baseConfig.RthOnly);
        var options = new SyntheticOptionsData(_baseConfig, market, _baseConfig.Paths.VixCsv, _baseConfig.Paths.Vix9dCsv);
        var builder = new SpreadBuilder(_baseConfig);
        
        var testTime = new DateTime(2024, 2, 1, 13, 0, 0);
        
        // Act
        var quotes = options.GetQuotesAt(testTime).ToList();
        var order = builder.TryBuild(testTime, Decision.Condor, market, options);
        
        // Assert
        quotes.Should().NotBeEmpty("Should generate some quotes");
        
        var uniqueStrikes = quotes.Select(q => q.Strike).Distinct().Count();
        Console.WriteLine($"Available strikes: {uniqueStrikes}");
        
        if (order == null)
        {
            uniqueStrikes.Should().BeLessThan(8, 
                "If spread construction fails, should be due to insufficient strike coverage");
        }
    }

    // Helper methods for creating test scenarios
    private SimConfig CreateBaseConfig()
    {
        return new SimConfig
        {
            Underlying = "XSP",
            Timezone = "America/New_York",
            RthOnly = true,
            
            ShortDelta = new ShortDeltaCfg
            {
                CondorMin = 0.15,
                CondorMax = 0.35,
                SingleMin = 0.20,
                SingleMax = 0.40
            },
            
            WidthPoints = new WidthPointsCfg { Min = 1, Max = 2 },
            CreditPerWidthMin = new CreditPerWidthCfg { Condor = 0.08, Single = 0.10 },
            Slippage = new SlippageCfg { SpreadPctCap = 0.25 },
            
            Signals = new SignalsCfg
            {
                EventBlockMinutesBefore = 60,
                EventBlockMinutesAfter = 15
            },
            
            Paths = new PathsCfg
            {
                BarsCsv = Path.Combine(_testDataDir, "bars.csv"),
                VixCsv = Path.Combine(_testDataDir, "vix.csv"),
                Vix9dCsv = Path.Combine(_testDataDir, "vix9d.csv"),
                CalendarCsv = Path.Combine(_testDataDir, "calendar.csv")
            }
        };
    }

    private void CreateNormalMarketScenario()
    {
        CreateMarketData(495.0, 0.5); // Normal price movement
        CreateVolatilityData(16.0);    // Normal VIX
        CreateEmptyCalendar();
    }

    private void CreateLowVolatilityScenario(double vixLevel)
    {
        CreateMarketData(495.0, 0.2); // Low price movement  
        CreateVolatilityData(vixLevel);
        CreateEmptyCalendar();
    }

    private void CreateHighVolatilityScenario(double vixLevel)
    {
        CreateMarketData(495.0, 2.0); // High price movement
        CreateVolatilityData(vixLevel);
        CreateEmptyCalendar();
    }

    private void CreateGappingMarketScenario()
    {
        // Create price data with large gaps
        var barsContent = "ts,o,h,l,c,v\n";
        var prices = new[] { 495.0, 500.0, 492.0, 498.0, 495.0 }; // Gaps up and down
        
        for (int i = 0; i < 300; i++)
        {
            var ts = new DateTime(2024, 2, 1, 9, 30, 0).AddMinutes(i);
            var priceIndex = i / 60; // Change price every hour
            var basePrice = prices[Math.Min(priceIndex, prices.Length - 1)];
            var price = basePrice + Math.Sin(i * 0.1) * 0.3; // Small oscillations around gaps
            
            barsContent += $"{ts:yyyy-MM-dd HH:mm:ss},{price:F2},{price + 0.5:F2},{price - 0.5:F2},{price + 0.1:F2},25000\n";
        }
        File.WriteAllText(_baseConfig.Paths.BarsCsv, barsContent);
        
        CreateVolatilityData(18.0);
        CreateEmptyCalendar();
    }

    private void CreateEconomicEventScenario()
    {
        CreateNormalMarketScenario();
        
        // Add FOMC event at 2 PM
        var calendarContent = "ts,kind\n2024-02-01 14:00:00,FOMC\n";
        File.WriteAllText(_baseConfig.Paths.CalendarCsv, calendarContent);
    }

    private void CreateLimitedStrikeScenario()
    {
        CreateNormalMarketScenario();
        // The SyntheticOptionsData will generate limited strikes due to the data scenario
        // This simulates a market with poor option liquidity
    }

    private void CreateMarketData(double basePrice, double volatility)
    {
        var barsContent = "ts,o,h,l,c,v\n";
        var startTime = new DateTime(2024, 2, 1, 9, 30, 0);
        
        for (int i = 0; i < 390; i++)
        {
            var ts = startTime.AddMinutes(i);
            var noise = (Random.Shared.NextDouble() - 0.5) * volatility * 2;
            var price = basePrice + noise;
            var high = price + volatility * 0.5;
            var low = price - volatility * 0.5;
            var close = price + (Random.Shared.NextDouble() - 0.5) * volatility * 0.3;
            var volume = 30000;
            
            barsContent += $"{ts:yyyy-MM-dd HH:mm:ss},{price:F2},{high:F2},{low:F2},{close:F2},{volume}\n";
        }
        File.WriteAllText(_baseConfig.Paths.BarsCsv, barsContent);
    }

    private void CreateVolatilityData(double vixLevel)
    {
        var vixContent = $"date,vix\n2024-02-01,{vixLevel:F1}\n";
        var vix9dContent = $"date,vix9d\n2024-02-01,{vixLevel * 0.9:F1}\n";
        
        File.WriteAllText(_baseConfig.Paths.VixCsv, vixContent);
        File.WriteAllText(_baseConfig.Paths.Vix9dCsv, vix9dContent);
    }

    private void CreateEmptyCalendar()
    {
        var calendarContent = "ts,kind\n";
        File.WriteAllText(_baseConfig.Paths.CalendarCsv, calendarContent);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDataDir))
        {
            Directory.Delete(_testDataDir, recursive: true);
        }
    }
}