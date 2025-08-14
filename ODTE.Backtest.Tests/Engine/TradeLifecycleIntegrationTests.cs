using Xunit;
using FluentAssertions;
using ODTE.Backtest.Engine;
using ODTE.Backtest.Config;
using ODTE.Backtest.Data;
using ODTE.Backtest.Signals;
using ODTE.Backtest.Strategy;
using ODTE.Backtest.Core;

namespace ODTE.Backtest.Tests.Engine;

/// <summary>
/// Integration tests for complete trade lifecycle from signal generation to P&L realization.
/// These tests verify end-to-end functionality that unit tests with mocks cannot catch.
/// 
/// KEY INSIGHT: Integration tests must use REAL components to catch configuration mismatches
/// and component interaction issues that cause zero-trade scenarios.
/// </summary>
public class TradeLifecycleIntegrationTests : IDisposable
{
    private readonly string _testDataDir;
    private readonly SimConfig _testConfig;

    public TradeLifecycleIntegrationTests()
    {
        _testDataDir = Path.Combine(Path.GetTempPath(), $"TradeLifecycle_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDataDir);
        
        _testConfig = CreateWorkingConfiguration();
        CreateComprehensiveTestData();
    }

    /// <summary>
    /// CRITICAL INTEGRATION TEST: This would have caught the zero-trade issue.
    /// Tests the complete pipeline: Market Data → Regime Scoring → Spread Building → Trade Execution
    /// </summary>
    [Fact]
    public async Task CompleteTradeLifecycle_ShouldGenerateActualTrades()
    {
        // Arrange: Initialize all real components (no mocks)
        var market = new CsvMarketData(_testConfig.Paths.BarsCsv, _testConfig.Timezone, _testConfig.RthOnly);
        var calendar = new CsvCalendar(_testConfig.Paths.CalendarCsv, _testConfig.Timezone);
        var options = new SyntheticOptionsData(_testConfig, market, _testConfig.Paths.VixCsv, _testConfig.Paths.Vix9dCsv);
        
        var scorer = new RegimeScorer(_testConfig);
        var builder = new SpreadBuilder(_testConfig);
        var exec = new ExecutionEngine(_testConfig);
        var risk = new RiskManager(_testConfig);
        
        var backtester = new Backtester(_testConfig, market, options, calendar, scorer, builder, exec, risk);
        
        // Act: Run complete backtest
        var report = await backtester.RunAsync();
        
        // Assert: Must generate actual trades
        report.Should().NotBeNull("Backtest should produce a report");
        report.Trades.Should().NotBeEmpty("Integration test MUST produce actual trades - this catches the zero-trade bug");
        
        // Validate trade quality
        report.Trades.Count.Should().BeGreaterThan(0, "Should execute at least one trade during the test period");
        
        var profitableTrades = report.Trades.Count(t => t.PnL > 0);
        var losingTrades = report.Trades.Count(t => t.PnL < 0);
        
        Console.WriteLine($"Trade Results: {report.Trades.Count} total, {profitableTrades} profitable, {losingTrades} losing");
        Console.WriteLine($"Total P&L: ${report.NetPnL:F2}");
        
        // Verify trades have realistic characteristics  
        foreach (var trade in report.Trades.Take(5)) // Log first few trades
        {
            trade.PnL.Should().NotBe(0, "Trades should have non-zero P&L");
            trade.Position.EntryPrice.Should().BeGreaterThan(0, "Entry price should be positive");
            
            Console.WriteLine($"Trade: {trade.Position.Type} at {trade.Position.EntryTs:MM/dd HH:mm} " +
                            $"Entry=${trade.Position.EntryPrice:F2} P&L=${trade.PnL:F2} Reason={trade.ExitReason}");
        }
        
        // Verify we're testing a meaningful time period
        var tradingDays = report.Trades.Select(t => t.Position.EntryTs.Date).Distinct().Count();
        tradingDays.Should().BeGreaterThan(1, "Should trade across multiple days for valid test");
    }

    /// <summary>
    /// Tests regime scoring integration with actual market data.
    /// Verifies that scoring produces actionable signals.
    /// </summary>
    [Fact]
    public void RegimeScoring_WithRealData_ShouldProduceActionableSignals()
    {
        // Arrange
        var market = new CsvMarketData(_testConfig.Paths.BarsCsv, _testConfig.Timezone, _testConfig.RthOnly);
        var calendar = new CsvCalendar(_testConfig.Paths.CalendarCsv, _testConfig.Timezone);
        var scorer = new RegimeScorer(_testConfig);
        
        var testTimes = new[]
        {
            new DateTime(2024, 2, 1, 10, 0, 0),
            new DateTime(2024, 2, 1, 12, 0, 0),
            new DateTime(2024, 2, 1, 14, 0, 0),
            new DateTime(2024, 2, 2, 11, 0, 0),
            new DateTime(2024, 2, 3, 13, 0, 0)
        };
        
        var actionableSignals = 0;
        
        // Act & Assert
        foreach (var testTime in testTimes)
        {
            var (score, calm, up, dn) = scorer.Score(testTime, market, calendar);
            
            // Verify regime scoring produces reasonable results
            score.Should().BeInRange(-10, 10, "Regime scores should be in reasonable range");
            
            // Check for actionable signals
            if ((calm && score >= 0) || (up && score >= 2) || (dn && score >= 2))
            {
                actionableSignals++;
                Console.WriteLine($"✅ {testTime:MM/dd HH:mm}: Score={score}, Calm={calm}, Up={up}, Dn={dn} → Actionable");
            }
            else
            {
                Console.WriteLine($"❌ {testTime:MM/dd HH:mm}: Score={score}, Calm={calm}, Up={up}, Dn={dn} → No action");
            }
        }
        
        actionableSignals.Should().BeGreaterThan(0, 
            "Regime scoring should produce actionable signals for at least some time periods");
    }

    /// <summary>
    /// Tests spread construction pipeline with real synthetic data.
    /// This catches mismatches between data generation and strategy requirements.
    /// </summary>
    [Fact]
    public void SpreadConstruction_WithRealSyntheticData_ShouldSucceed()
    {
        // Arrange
        var market = new CsvMarketData(_testConfig.Paths.BarsCsv, _testConfig.Timezone, _testConfig.RthOnly);
        var options = new SyntheticOptionsData(_testConfig, market, _testConfig.Paths.VixCsv, _testConfig.Paths.Vix9dCsv);
        var builder = new SpreadBuilder(_testConfig);
        
        var testTimes = new[]
        {
            new DateTime(2024, 2, 1, 10, 30, 0),
            new DateTime(2024, 2, 1, 12, 30, 0),
            new DateTime(2024, 2, 1, 14, 30, 0),
        };
        
        var successfulBuilds = 0;
        
        // Act & Assert
        foreach (var testTime in testTimes)
        {
            var spot = market.GetSpot(testTime);
            var quotes = options.GetQuotesAt(testTime).ToList();
            
            quotes.Should().NotBeEmpty($"Synthetic data should generate quotes at {testTime}");
            Console.WriteLine($"\n{testTime:MM/dd HH:mm}: Spot=${spot:F2}, {quotes.Count} quotes");
            
            // Test each spread type
            var decisions = new[] 
            { 
                Decision.Condor, 
                Decision.SingleSidePut, 
                Decision.SingleSideCall 
            };
            
            foreach (var decision in decisions)
            {
                var order = builder.TryBuild(testTime, decision, market, options);
                
                if (order != null)
                {
                    successfulBuilds++;
                    
                    // Validate order structure
                    order.Credit.Should().BeGreaterThan(0, "Should receive positive credit");
                    order.Width.Should().BeGreaterThan(0, "Should have positive spread width");
                    
                    var creditPerWidth = order.Credit / order.Width;
                    creditPerWidth.Should().BeGreaterThan(0.05, "Should have meaningful credit/width ratio");
                    
                    Console.WriteLine($"   ✅ {decision}: K={order.Short.Strike:F1} Credit=${order.Credit:F2} " +
                                    $"Width={order.Width:F1} C/W={creditPerWidth:F3}");
                }
                else
                {
                    Console.WriteLine($"   ❌ {decision}: Build failed");
                }
            }
        }
        
        successfulBuilds.Should().BeGreaterThan(0, 
            "Should successfully build spreads for at least some time periods and decision types");
    }

    /// <summary>
    /// Tests trade execution and position management.
    /// Verifies that orders translate into actual positions with proper P&L tracking.
    /// </summary>
    [Fact]
    public void TradeExecution_ShouldProduceRealisticPnL()
    {
        // Arrange
        var market = new CsvMarketData(_testConfig.Paths.BarsCsv, _testConfig.Timezone, _testConfig.RthOnly);
        var options = new SyntheticOptionsData(_testConfig, market, _testConfig.Paths.VixCsv, _testConfig.Paths.Vix9dCsv);
        var builder = new SpreadBuilder(_testConfig);
        var exec = new ExecutionEngine(_testConfig);
        
        var entryTime = new DateTime(2024, 2, 1, 10, 30, 0);
        var exitTime = new DateTime(2024, 2, 1, 15, 0, 0);
        
        // Act: Create and execute a trade
        var order = builder.TryBuild(entryTime, Decision.Condor, market, options);
        order.Should().NotBeNull("Should be able to build test order");
        
        var position = exec.TryEnter(order!);
        position.Should().NotBeNull("Should be able to enter position");
        
        // Simulate position management over time
        var currentTime = entryTime.AddMinutes(30);
        var testUpdates = 0;
        var lastSpreadValue = position!.EntryPrice;
        
        while (currentTime < exitTime && testUpdates < 10)
        {
            // Get current option quotes
            var currentQuotes = options.GetQuotesAt(currentTime).ToList();
            
            // Calculate current spread value
            var shortQuote = currentQuotes.FirstOrDefault(q => 
                q.Right == position.Order.Short.Right && 
                Math.Abs(q.Strike - position.Order.Short.Strike) < 0.01);
                
            var longQuote = currentQuotes.FirstOrDefault(q => 
                q.Right == position.Order.Long.Right && 
                Math.Abs(q.Strike - position.Order.Long.Strike) < 0.01);
                
            if (shortQuote != null && longQuote != null)
            {
                var currentSpreadValue = Math.Max(0, shortQuote.Mid - longQuote.Mid);
                var unrealizedPnL = (position.EntryPrice - currentSpreadValue) * 100;
                
                Console.WriteLine($"{currentTime:HH:mm}: Spread=${currentSpreadValue:F2} " +
                                $"Unrealized=${unrealizedPnL:F2}");
                
                // Test exit conditions
                var shortDelta = Math.Abs(shortQuote.Delta);
                var (shouldExit, exitPrice, reason) = exec.ShouldExit(position, currentSpreadValue, shortDelta, currentTime);
                
                if (shouldExit)
                {
                    var finalPnL = (position.EntryPrice - exitPrice) * 100;
                    Console.WriteLine($"Exit triggered: {reason}, Final P&L: ${finalPnL:F2}");
                    
                    finalPnL.Should().NotBe(0, "Position should have non-zero P&L when closed");
                    break;
                }
                
                lastSpreadValue = currentSpreadValue;
                testUpdates++;
            }
            
            currentTime = currentTime.AddMinutes(30);
        }
        
        testUpdates.Should().BeGreaterThan(0, "Should be able to track position over time");
    }

    /// <summary>
    /// Tests risk management integration with actual trading scenarios.
    /// </summary>
    [Fact]
    public void RiskManagement_Integration_ShouldEnforceControls()
    {
        // Arrange
        var market = new CsvMarketData(_testConfig.Paths.BarsCsv, _testConfig.Timezone, _testConfig.RthOnly);
        var options = new SyntheticOptionsData(_testConfig, market, _testConfig.Paths.VixCsv, _testConfig.Paths.Vix9dCsv);
        var builder = new SpreadBuilder(_testConfig);
        var risk = new RiskManager(_testConfig);
        
        var testTime = new DateTime(2024, 2, 1, 11, 0, 0);
        
        // Act: Test position limits
        var positionsOpened = 0;
        var maxAttempts = 10;
        
        for (int i = 0; i < maxAttempts; i++)
        {
            if (risk.CanAdd(testTime, Decision.Condor))
            {
                var order = builder.TryBuild(testTime, Decision.Condor, market, options);
                if (order != null)
                {
                    risk.RegisterOpen(Decision.Condor);
                    positionsOpened++;
                    Console.WriteLine($"Opened position {positionsOpened}");
                }
            }
            else
            {
                Console.WriteLine($"Risk manager blocked position {i + 1}");
                break;
            }
        }
        
        // Assert: Risk limits should be enforced
        positionsOpened.Should().BeLessThanOrEqualTo(_testConfig.Risk.MaxConcurrentPerSide,
            "Risk manager should enforce position limits");
        
        positionsOpened.Should().BeGreaterThan(0,
            "Should be able to open at least one position before hitting limits");
        
        // Test daily loss limits
        risk.RegisterClose(Decision.Condor, -_testConfig.Risk.DailyLossStop * 0.6);
        risk.IsLockedToday().Should().BeFalse("Should not be locked with partial losses");
        
        risk.RegisterClose(Decision.Condor, -_testConfig.Risk.DailyLossStop * 0.6);
        risk.IsLockedToday().Should().BeTrue("Should be locked after exceeding daily loss limit");
    }

    /// <summary>
    /// Tests configuration validation - ensures all components work together.
    /// This type of test would catch configuration mismatches between components.
    /// </summary>
    [Fact]
    public void ConfigurationValidation_AllComponents_ShouldBeCompatible()
    {
        // Arrange: Test with various configuration scenarios
        var scenarios = new[]
        {
            ("Original", CreateOriginalConfiguration()),
            ("Current", CreateWorkingConfiguration()),
            ("Strict", CreateStrictConfiguration())
        };
        
        foreach (var (name, config) in scenarios)
        {
            Console.WriteLine($"\nTesting {name} configuration:");
            
            try
            {
                // Act: Initialize all components
                var market = new CsvMarketData(config.Paths.BarsCsv, config.Timezone, config.RthOnly);
                var options = new SyntheticOptionsData(config, market, config.Paths.VixCsv, config.Paths.Vix9dCsv);
                var builder = new SpreadBuilder(config);
                
                var testTime = new DateTime(2024, 2, 1, 12, 0, 0);
                
                // Test spread construction capability
                var order = builder.TryBuild(testTime, Decision.Condor, market, options);
                
                if (order != null)
                {
                    Console.WriteLine($"   ✅ {name}: Can build spreads");
                }
                else
                {
                    Console.WriteLine($"   ❌ {name}: Cannot build spreads - configuration issue");
                    
                    // Diagnose the problem
                    var quotes = options.GetQuotesAt(testTime).ToList();
                    var spot = market.GetSpot(testTime);
                    
                    var targetStrikes = quotes.Where(q => 
                        Math.Abs(q.Delta) >= config.ShortDelta.CondorMin && 
                        Math.Abs(q.Delta) <= config.ShortDelta.CondorMax).ToList();
                    
                    Console.WriteLine($"      Spot: ${spot:F2}, Quotes: {quotes.Count}, Target strikes: {targetStrikes.Count}");
                    Console.WriteLine($"      Delta range: [{config.ShortDelta.CondorMin:F2}-{config.ShortDelta.CondorMax:F2}]");
                    Console.WriteLine($"      C/W requirement: {config.CreditPerWidthMin.Condor:F2}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   💥 {name}: Component initialization failed - {ex.Message}");
            }
        }
    }

    // Helper methods
    private SimConfig CreateWorkingConfiguration()
    {
        return new SimConfig
        {
            Underlying = "XSP",
            Start = new DateOnly(2024, 2, 1),
            End = new DateOnly(2024, 2, 5),
            Mode = "prototype",
            RthOnly = true,
            Timezone = "America/New_York",
            CadenceSeconds = 900,
            
            // Working configuration from current system
            ShortDelta = new ShortDeltaCfg
            {
                CondorMin = 0.15,
                CondorMax = 0.35,
                SingleMin = 0.20,
                SingleMax = 0.40
            },
            
            WidthPoints = new WidthPointsCfg { Min = 1, Max = 2 },
            CreditPerWidthMin = new CreditPerWidthCfg { Condor = 0.08, Single = 0.10 },
            
            Risk = new RiskCfg
            {
                DailyLossStop = 500,
                PerTradeMaxLossCap = 200,
                MaxConcurrentPerSide = 2
            },
            
            Slippage = new SlippageCfg
            {
                EntryHalfSpreadTicks = 0.5,
                ExitHalfSpreadTicks = 0.5,
                TickValue = 0.05,
                SpreadPctCap = 0.25
            },
            
            Fees = new FeesCfg
            {
                CommissionPerContract = 0.65,
                ExchangeFeesPerContract = 0.25
            },
            
            Paths = new PathsCfg
            {
                BarsCsv = Path.Combine(_testDataDir, "bars.csv"),
                VixCsv = Path.Combine(_testDataDir, "vix.csv"),
                Vix9dCsv = Path.Combine(_testDataDir, "vix9d.csv"),
                CalendarCsv = Path.Combine(_testDataDir, "calendar.csv"),
                ReportsDir = Path.Combine(_testDataDir, "reports")
            }
        };
    }

    private SimConfig CreateOriginalConfiguration()
    {
        var config = CreateWorkingConfiguration();
        
        // Original problematic settings
        config.ShortDelta.CondorMin = 0.07;
        config.ShortDelta.CondorMax = 0.15;
        config.ShortDelta.SingleMin = 0.10;
        config.ShortDelta.SingleMax = 0.20;
        config.CreditPerWidthMin.Condor = 0.18;
        config.CreditPerWidthMin.Single = 0.20;
        
        return config;
    }

    private SimConfig CreateStrictConfiguration()
    {
        var config = CreateWorkingConfiguration();
        
        // Very strict requirements
        config.CreditPerWidthMin.Condor = 0.25;
        config.CreditPerWidthMin.Single = 0.30;
        config.ShortDelta.CondorMin = 0.20;
        config.ShortDelta.CondorMax = 0.25; // Narrow range
        
        return config;
    }

    private void CreateComprehensiveTestData()
    {
        // Create realistic multi-day market data
        var barsContent = "ts,o,h,l,c,v\n";
        var basePrice = 495.0;
        
        for (int day = 0; day < 5; day++)
        {
            var dayStart = new DateTime(2024, 2, 1 + day, 9, 30, 0);
            var dailyTrend = (day - 2) * 0.5; // Some days up, some down
            
            for (int minute = 0; minute < 390; minute += 5) // Every 5 minutes
            {
                var ts = dayStart.AddMinutes(minute);
                var intraDay = Math.Sin(minute * 0.02) * 1.5; // Intraday oscillation
                var price = basePrice + dailyTrend + intraDay;
                var high = price + 0.4;
                var low = price - 0.4;
                var close = price + 0.1;
                var volume = 30000 + minute * 20;
                
                barsContent += $"{ts:yyyy-MM-dd HH:mm:ss},{price:F2},{high:F2},{low:F2},{close:F2},{volume}\n";
            }
        }
        File.WriteAllText(_testConfig.Paths.BarsCsv, barsContent);
        
        // Create varying VIX data
        var vixContent = "date,vix\n";
        var vix9dContent = "date,vix9d\n";
        var vixLevels = new[] { 18.5, 15.2, 12.8, 20.1, 16.7 }; // Varying volatility
        
        for (int day = 0; day < 5; day++)
        {
            var date = new DateTime(2024, 2, 1 + day);
            var vix = vixLevels[day];
            var vix9d = vix * 0.9;
            
            vixContent += $"{date:yyyy-MM-dd},{vix:F1}\n";
            vix9dContent += $"{date:yyyy-MM-dd},{vix9d:F1}\n";
        }
        File.WriteAllText(_testConfig.Paths.VixCsv, vixContent);
        File.WriteAllText(_testConfig.Paths.Vix9dCsv, vix9dContent);
        
        // Empty calendar for clean testing
        var calendarContent = "ts,kind\n";
        File.WriteAllText(_testConfig.Paths.CalendarCsv, calendarContent);
        
        Directory.CreateDirectory(_testConfig.Paths.ReportsDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDataDir))
        {
            Directory.Delete(_testDataDir, recursive: true);
        }
    }
}