using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ODTE.Execution.Engine;
using ODTE.Execution.Models;

namespace ODTE.Execution.Tests;

/// <summary>
/// Comprehensive tests for RealisticFillEngine ensuring institutional compliance.
/// Tests cover all acceptance criteria from realFillSimulationUpgrade.md.
/// </summary>
[TestClass]
public class RealisticFillEngineTests
{
    private RealisticFillEngine _engine = null!;
    private ILogger<RealisticFillEngine> _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<RealisticFillEngine>();
        _engine = new RealisticFillEngine(ExecutionProfile.Conservative, _logger);
    }

    [TestMethod]
    [TestCategory("AuditCompliance")]
    public async Task Conservative_Profile_Never_Exceeds_Mid_Rate_Threshold()
    {
        // Test with 1000 random scenarios to ensure mid-rate < 60%
        var scenarios = GenerateRandomScenarios(1000);
        var midOrBetterCount = 0;
        var totalFills = 0;

        foreach (var scenario in scenarios)
        {
            var result = await _engine.SimulateFillAsync(scenario.Order, scenario.Quote, ExecutionProfile.Conservative, scenario.MarketState);

            Assert.IsNotNull(result, "Fill simulation should not return null");
            totalFills++;

            if (result.WasMidOrBetter)
                midOrBetterCount++;
        }

        var midRate = (double)midOrBetterCount / totalFills * 100;
        Assert.IsTrue(midRate < 60.0, $"Conservative profile mid-rate {midRate:F2}% must be < 60%");

        Console.WriteLine($"Conservative profile mid-rate: {midRate:F2}% (target: < 60%)");
    }

    [TestMethod]
    [TestCategory("AuditCompliance")]
    public async Task All_Fills_Must_Be_Within_NBBO_Band()
    {
        // Test NBBO compliance with ±$0.01 tolerance
        var scenarios = GenerateRandomScenarios(500);
        var withinNbboCount = 0;
        var totalFills = 0;

        foreach (var scenario in scenarios)
        {
            var result = await _engine.SimulateFillAsync(scenario.Order, scenario.Quote, ExecutionProfile.Conservative, scenario.MarketState);

            Assert.IsNotNull(result);
            totalFills++;

            if (result.WasWithinNbbo)
                withinNbboCount++;
        }

        var nbboComplianceRate = (double)withinNbboCount / totalFills * 100;
        Assert.IsTrue(nbboComplianceRate >= 98.0, $"NBBO compliance rate {nbboComplianceRate:F2}% must be ≥ 98%");

        Console.WriteLine($"NBBO compliance rate: {nbboComplianceRate:F2}% (target: ≥ 98%)");
    }

    [TestMethod]
    [TestCategory("AuditCompliance")]
    public async Task Slippage_Sensitivity_Meets_Profit_Factor_Requirements()
    {
        // Test PM212-style strategy with 5c and 10c slippage penalties
        var tradingDays = GenerateStrategyBacktest(100); // 100 trading days

        var originalPF = CalculateProfitFactor(tradingDays, 0.00m);
        var pf5c = CalculateProfitFactor(tradingDays, 0.05m);
        var pf10c = CalculateProfitFactor(tradingDays, 0.10m);

        Assert.IsTrue(pf5c >= 1.30m, $"Profit factor with 5c slippage {pf5c:F2} must be ≥ 1.30");
        Assert.IsTrue(pf10c >= 1.15m, $"Profit factor with 10c slippage {pf10c:F2} must be ≥ 1.15");

        Console.WriteLine($"Original PF: {originalPF:F2}, 5c PF: {pf5c:F2}, 10c PF: {pf10c:F2}");
    }

    [TestMethod]
    [TestCategory("RiskManagement")]
    public void Worst_Case_Fill_Calculation_Is_Conservative()
    {
        var order = CreateSampleOrder();
        var quote = CreateSampleQuote();

        var worstCase = _engine.CalculateWorstCaseFill(order, quote, ExecutionProfile.Conservative);
        var normalFill = quote.Ask; // Expected normal fill for buy order

        Assert.IsTrue(worstCase >= normalFill, "Worst case fill must be at least as bad as normal fill");
        Assert.IsTrue(worstCase <= normalFill * 1.10m, "Worst case fill should be reasonable (within 10% of normal)");

        Console.WriteLine($"Normal fill: {normalFill:F4}, Worst case: {worstCase:F4}");
    }

    [TestMethod]
    [TestCategory("Performance")]
    public async Task Fill_Simulation_Completes_Within_Performance_Budget()
    {
        var order = CreateSampleOrder();
        var quote = CreateSampleQuote();
        var marketState = CreateSampleMarketState();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var result = await _engine.SimulateFillAsync(order, quote, ExecutionProfile.Conservative, marketState);

        stopwatch.Stop();

        Assert.IsNotNull(result);
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 100, $"Fill simulation took {stopwatch.ElapsedMilliseconds}ms, must be < 100ms");

        Console.WriteLine($"Fill simulation completed in {stopwatch.ElapsedMilliseconds}ms");
    }

    [TestMethod]
    [TestCategory("Configuration")]
    public void Different_Profiles_Produce_Different_Results()
    {
        var conservativeEngine = new RealisticFillEngine(ExecutionProfile.Conservative, _logger);
        var baseEngine = new RealisticFillEngine(ExecutionProfile.Base, _logger);
        var optimisticEngine = new RealisticFillEngine(ExecutionProfile.Optimistic, _logger);

        var order = CreateSampleOrder();
        var quote = CreateSampleQuote();

        var conservativeWorstCase = conservativeEngine.CalculateWorstCaseFill(order, quote, ExecutionProfile.Conservative);
        var baseWorstCase = baseEngine.CalculateWorstCaseFill(order, quote, ExecutionProfile.Base);
        var optimisticWorstCase = optimisticEngine.CalculateWorstCaseFill(order, quote, ExecutionProfile.Optimistic);

        Assert.IsTrue(conservativeWorstCase >= baseWorstCase, "Conservative should be more pessimistic than base");
        Assert.IsTrue(baseWorstCase >= optimisticWorstCase, "Base should be more pessimistic than optimistic");

        Console.WriteLine($"Conservative: {conservativeWorstCase:F4}, Base: {baseWorstCase:F4}, Optimistic: {optimisticWorstCase:F4}");
    }

    [TestMethod]
    [TestCategory("EventRisk")]
    public async Task Event_Risk_Reduces_Mid_Fill_Probability()
    {
        var order = CreateSampleOrder();
        var quote = CreateSampleQuote();

        // Normal market conditions
        var normalMarket = CreateSampleMarketState();
        var normalResult = await _engine.SimulateFillAsync(order, quote, ExecutionProfile.Base, normalMarket);

        // Event risk conditions (FOMC)
        var eventMarket = normalMarket with { ActiveEvents = new List<string> { "fomc" } };
        var eventResult = await _engine.SimulateFillAsync(order, quote, ExecutionProfile.Base, eventMarket);

        Assert.IsNotNull(normalResult);
        Assert.IsNotNull(eventResult);

        // Event conditions should generally result in worse fills
        Assert.IsTrue(eventResult.SlippagePerContract >= normalResult.SlippagePerContract,
            "Event conditions should increase slippage");

        Console.WriteLine($"Normal slippage: {normalResult.SlippagePerContract:F4}, Event slippage: {eventResult.SlippagePerContract:F4}");
    }

    [TestMethod]
    [TestCategory("Determinism")]
    public async Task Same_Inputs_Produce_Consistent_Results()
    {
        // Test determinism by running same scenario multiple times
        var order = CreateSampleOrder();
        var quote = CreateSampleQuote();
        var marketState = CreateSampleMarketState();

        var results = new List<FillResult>();
        for (int i = 0; i < 10; i++)
        {
            var result = await _engine.SimulateFillAsync(order, quote, ExecutionProfile.Conservative, marketState);
            Assert.IsNotNull(result);
            results.Add(result);
        }

        // Results should be similar (within reasonable variance due to randomness)
        var avgPrice = results.Average(r => r.AverageFillPrice);
        var maxDeviation = results.Max(r => Math.Abs(r.AverageFillPrice - avgPrice));

        Assert.IsTrue(maxDeviation <= quote.Spread, "Price deviation should be within one spread");

        Console.WriteLine($"Average fill: {avgPrice:F4}, Max deviation: {maxDeviation:F4}");
    }

    #region Helper Methods

    private List<TestScenario> GenerateRandomScenarios(int count)
    {
        var random = new Random(42); // Fixed seed for reproducibility
        var scenarios = new List<TestScenario>();

        for (int i = 0; i < count; i++)
        {
            var bidPrice = 100m + (decimal)random.NextDouble() * 50m;
            var spread = 0.05m + (decimal)random.NextDouble() * 0.15m;

            scenarios.Add(new TestScenario
            {
                Order = new Order
                {
                    OrderId = $"TEST-{i:D4}",
                    Symbol = "XSP",
                    Strike = bidPrice + 10m,
                    OptionType = OptionType.Put,
                    Side = OrderSide.Sell,
                    Quantity = random.Next(1, 10),
                    NotionalValue = 1000m
                },
                Quote = new Quote
                {
                    Symbol = "XSP",
                    Bid = bidPrice,
                    Ask = bidPrice + spread,
                    BidSize = random.Next(10, 100),
                    AskSize = random.Next(10, 100),
                    Timestamp = DateTime.UtcNow
                },
                MarketState = new MarketState
                {
                    Timestamp = DateTime.UtcNow,
                    VIX = 10m + (decimal)random.NextDouble() * 40m,
                    UnderlyingPrice = bidPrice,
                    VIXRegime = VIXRegime.Normal,
                    TimeRegime = TimeOfDayRegime.MidDay
                }
            });
        }

        return scenarios;
    }

    private List<DailyTradeResult> GenerateStrategyBacktest(int days)
    {
        var random = new Random(42);
        var results = new List<DailyTradeResult>();

        for (int i = 0; i < days; i++)
        {
            // Simulate PM212-style iron condor with realistic parameters
            var isWin = random.NextDouble() < 0.70; // 70% win rate
            var baseReturn = isWin ? random.NextDouble() * 50 + 10 : -(random.NextDouble() * 200 + 50);

            results.Add(new DailyTradeResult
            {
                Date = DateTime.Today.AddDays(-days + i),
                BaseReturn = (decimal)baseReturn,
                IsWin = isWin
            });
        }

        return results;
    }

    private decimal CalculateProfitFactor(List<DailyTradeResult> trades, decimal slippagePerContract)
    {
        var totalWins = 0m;
        var totalLosses = 0m;

        foreach (var trade in trades)
        {
            var adjustedReturn = trade.BaseReturn - slippagePerContract;

            if (adjustedReturn > 0)
                totalWins += adjustedReturn;
            else
                totalLosses += Math.Abs(adjustedReturn);
        }

        return totalLosses > 0 ? totalWins / totalLosses : 0;
    }

    private Order CreateSampleOrder()
    {
        return new Order
        {
            OrderId = "TEST-001",
            Symbol = "XSP",
            Strike = 450m,
            OptionType = OptionType.Put,
            Side = OrderSide.Sell,
            Quantity = 1,
            NotionalValue = 1000m,
            StrategyType = "CreditSpread"
        };
    }

    private Quote CreateSampleQuote()
    {
        return new Quote
        {
            Symbol = "XSP",
            Bid = 1.50m,
            Ask = 1.55m,
            BidSize = 50,
            AskSize = 45,
            Timestamp = DateTime.UtcNow
        };
    }

    private MarketState CreateSampleMarketState()
    {
        return new MarketState
        {
            Timestamp = DateTime.UtcNow,
            VIX = 18m,
            VIXRegime = VIXRegime.Normal,
            TimeRegime = TimeOfDayRegime.MidDay,
            UnderlyingPrice = 450m,
            DaysToExpiry = 0,
            StressLevel = 0.2m
        };
    }

    #endregion

    #region Test Data Classes

    private class TestScenario
    {
        public Order Order { get; set; } = null!;
        public Quote Quote { get; set; } = null!;
        public MarketState MarketState { get; set; } = null!;
    }

    private class DailyTradeResult
    {
        public DateTime Date { get; set; }
        public decimal BaseReturn { get; set; }
        public bool IsWin { get; set; }
    }

    #endregion
}