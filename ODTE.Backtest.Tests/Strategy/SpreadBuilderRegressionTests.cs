using Xunit;
using FluentAssertions;
using ODTE.Backtest.Strategy;
using ODTE.Backtest.Config;
using ODTE.Backtest.Data;
using ODTE.Backtest.Core;
using Moq;

namespace ODTE.Backtest.Tests.Strategy;

/// <summary>
/// REGRESSION TESTS for SpreadBuilder that would have caught the zero-trade issue.
/// These tests use REAL synthetic data instead of mocks to validate end-to-end functionality.
/// 
/// KEY LESSON: Integration with real data components is essential for catching configuration mismatches.
/// </summary>
public class SpreadBuilderRegressionTests : IDisposable
{
    private readonly string _testDataDir;
    private readonly SimConfig _testConfig;
    private readonly SpreadBuilder _builder;
    private readonly CsvMarketData _marketData;
    private readonly SyntheticOptionsData _optionsData;

    public SpreadBuilderRegressionTests()
    {
        _testDataDir = Path.Combine(Path.GetTempPath(), $"SpreadBuilderRegression_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDataDir);
        
        // Use REALISTIC configuration that should generate trades
        _testConfig = CreateRealisticConfig();
        
        // Create test data files with realistic market scenarios
        CreateRealisticTestData();
        
        _builder = new SpreadBuilder(_testConfig);
        _marketData = new CsvMarketData(_testConfig.Paths.BarsCsv, _testConfig.Timezone, _testConfig.RthOnly);
        _optionsData = new SyntheticOptionsData(_testConfig, _marketData, 
            _testConfig.Paths.VixCsv, _testConfig.Paths.Vix9dCsv);
    }

    /// <summary>
    /// CRITICAL REGRESSION TEST: This would have caught the zero-trade issue.
    /// Tests that synthetic data + SpreadBuilder actually produces executable trades.
    /// </summary>
    [Fact]
    public void TryBuild_WithRealSyntheticData_ShouldProduceTrades()
    {
        // Arrange: Use real synthetic data generation
        var testTime = new DateTime(2024, 2, 1, 10, 30, 0);
        var quotes = _optionsData.GetQuotesAt(testTime).ToList();
        
        quotes.Should().NotBeEmpty("Synthetic data should generate quotes");
        
        // Act: Try to build each type of spread
        var condorOrder = _builder.TryBuild(testTime, Decision.Condor, _marketData, _optionsData);
        var putOrder = _builder.TryBuild(testTime, Decision.SingleSidePut, _marketData, _optionsData);
        var callOrder = _builder.TryBuild(testTime, Decision.SingleSideCall, _marketData, _optionsData);
        
        // Assert: At least one spread type should be buildable
        var successfulOrders = new[] { condorOrder, putOrder, callOrder }.Where(o => o != null).ToList();
        successfulOrders.Should().NotBeEmpty(
            "Real synthetic data should enable at least one type of spread construction. " +
            "If this fails, there's a mismatch between synthetic data generation and strategy requirements.");
        
        // Verify any successful order meets quality criteria
        foreach (var order in successfulOrders)
        {
            order!.Credit.Should().BeGreaterThan(0, "Credit spreads should receive positive premium");
            order.Width.Should().BeGreaterThan(0, "Spreads should have positive width");
            
            var creditPerWidth = order.Credit / order.Width;
            creditPerWidth.Should().BeGreaterThan(0.05, "Credit/width ratio should be meaningful for 0DTE");
        }
    }

    /// <summary>
    /// Tests configuration boundary conditions that can break trade generation.
    /// </summary>
    [Theory]
    [InlineData(0.01, 0.05)]  // Very tight delta range
    [InlineData(0.05, 0.10)]  // Low delta range  
    [InlineData(0.10, 0.20)]  // Medium delta range
    [InlineData(0.20, 0.40)]  // High delta range
    public void TryBuild_WithVariousDeltaRanges_ShouldAdaptToConfiguration(double deltaMin, double deltaMax)
    {
        // Arrange: Modify configuration delta ranges
        var config = CreateRealisticConfig();
        config.ShortDelta.CondorMin = deltaMin;
        config.ShortDelta.CondorMax = deltaMax;
        config.ShortDelta.SingleMin = deltaMin;
        config.ShortDelta.SingleMax = deltaMax;
        
        var builder = new SpreadBuilder(config);
        var testTime = new DateTime(2024, 2, 1, 10, 30, 0);
        
        // Act: Try to build with modified configuration
        var order = builder.TryBuild(testTime, Decision.Condor, _marketData, _optionsData);
        
        // Assert: Document which delta ranges work with current synthetic data
        if (order != null)
        {
            // Verify the order uses deltas within specified range
            var shortDelta = Math.Abs(order.Short.Delta);
            shortDelta.Should().BeInRange(deltaMin, deltaMax, 
                $"Short delta should be within configured range [{deltaMin:F2}-{deltaMax:F2}]");
            
            // Output for analysis (visible in test results)
            Console.WriteLine($"✅ Delta range [{deltaMin:F2}-{deltaMax:F2}] produced trade: " +
                            $"K={order.Short.Strike:F1} Δ={shortDelta:F3} Credit={order.Credit:F2}");
        }
        else
        {
            Console.WriteLine($"❌ Delta range [{deltaMin:F2}-{deltaMax:F2}] failed to produce trades");
        }
    }

    /// <summary>
    /// Tests credit/width requirements that can prevent trade execution.
    /// </summary>
    [Theory]
    [InlineData(0.05)]  // Very lenient (5%)
    [InlineData(0.10)]  // Moderate (10%) 
    [InlineData(0.18)]  // Original strict (18%)
    [InlineData(0.25)]  // Very strict (25%)
    public void TryBuild_WithVariousCreditRequirements_ShouldRespectLimits(double minCreditPerWidth)
    {
        // Arrange: Modify credit requirements
        var config = CreateRealisticConfig();
        config.CreditPerWidthMin.Condor = minCreditPerWidth;
        config.CreditPerWidthMin.Single = minCreditPerWidth;
        
        var builder = new SpreadBuilder(config);
        var testTime = new DateTime(2024, 2, 1, 10, 30, 0);
        
        // Act
        var order = builder.TryBuild(testTime, Decision.Condor, _marketData, _optionsData);
        
        // Assert
        if (order != null)
        {
            var actualRatio = order.Credit / order.Width;
            actualRatio.Should().BeGreaterThanOrEqualTo(minCreditPerWidth,
                $"Executed trade should meet minimum C/W requirement of {minCreditPerWidth:F2}");
                
            Console.WriteLine($"✅ C/W requirement {minCreditPerWidth:F2} produced trade with ratio {actualRatio:F3}");
        }
        else
        {
            Console.WriteLine($"❌ C/W requirement {minCreditPerWidth:F2} too strict - no trades possible");
        }
    }

    /// <summary>
    /// Tests edge cases where option values are insufficient for spread construction.
    /// </summary>
    [Fact]
    public void TryBuild_WithLowVolatilityPeriod_ShouldHandleGracefully()
    {
        // Arrange: Create scenario with very low VIX (low option values)
        CreateLowVolatilityTestData();
        
        var lowVolOptions = new SyntheticOptionsData(_testConfig, _marketData, 
            _testConfig.Paths.VixCsv, _testConfig.Paths.Vix9dCsv);
        
        var testTime = new DateTime(2024, 2, 1, 10, 30, 0);
        
        // Act
        var order = _builder.TryBuild(testTime, Decision.Condor, _marketData, lowVolOptions);
        
        // Assert: Should either produce valid trade or fail gracefully
        if (order != null)
        {
            order.Credit.Should().BeGreaterThan(0, "If trade is executed, it should have positive credit");
            order.Width.Should().BeGreaterThan(0, "If trade is executed, it should have positive width");
        }
        else
        {
            // Document why no trade was possible for analysis
            var quotes = lowVolOptions.GetQuotesAt(testTime).ToList();
            var atmValues = quotes.Where(q => Math.Abs(q.Strike - _marketData.GetSpot(testTime)) <= 2)
                                 .Select(q => q.Mid).ToList();
            
            Console.WriteLine($"Low volatility scenario: no trades possible. ATM option values: " +
                            $"Min={atmValues.Min():F3} Max={atmValues.Max():F3} Avg={atmValues.Average():F3}");
        }
    }

    /// <summary>
    /// Tests that protective wing selection works when delta-filtered strikes are insufficient.
    /// This would have caught the "same strike for short and long" bug.
    /// </summary>
    [Fact]
    public void TryBuild_ProtectiveWings_ShouldUseDifferentStrikes()
    {
        // Arrange
        var testTime = new DateTime(2024, 2, 1, 10, 30, 0);
        
        // Act
        var order = _builder.TryBuild(testTime, Decision.Condor, _marketData, _optionsData);
        
        // Assert
        if (order != null)
        {
            // Verify spread structure makes sense
            order.Short.Strike.Should().NotBe(order.Long.Strike, 
                "Short and long strikes must be different for a valid spread");
            
            if (order.Short.Right == Right.Put)
            {
                order.Short.Strike.Should().BeGreaterThan(order.Long.Strike,
                    "Put credit spread: short strike (higher) > long strike (lower)");
            }
            else
            {
                order.Short.Strike.Should().BeLessThan(order.Long.Strike,
                    "Call credit spread: short strike (lower) < long strike (higher)");
            }
            
            var actualWidth = Math.Abs(order.Short.Strike - order.Long.Strike);
            actualWidth.Should().BeInRange(_testConfig.WidthPoints.Min, _testConfig.WidthPoints.Max,
                "Spread width should be within configured limits");
        }
    }

    /// <summary>
    /// Tests market conditions where no strikes are available in target delta ranges.
    /// </summary>
    [Fact]
    public void TryBuild_WithExtremeDeltaRequirements_ShouldReturnNull()
    {
        // Arrange: Configure impossible delta requirements
        var config = CreateRealisticConfig();
        config.ShortDelta.CondorMin = 0.45;  // Very high deltas (close to ATM)
        config.ShortDelta.CondorMax = 0.55;  // Narrow range
        
        var builder = new SpreadBuilder(config);
        var testTime = new DateTime(2024, 2, 1, 15, 30, 0); // Late in day (low time value)
        
        // Act
        var order = builder.TryBuild(testTime, Decision.Condor, _marketData, _optionsData);
        
        // Assert: Should gracefully return null when requirements can't be met
        order.Should().BeNull("Should return null when delta requirements are impossible to meet");
        
        // Verify the synthetic data actually has quotes (problem is with requirements)
        var quotes = _optionsData.GetQuotesAt(testTime).ToList();
        quotes.Should().NotBeEmpty("Synthetic data should still generate quotes");
        
        var availableDeltas = quotes.Select(q => Math.Abs(q.Delta)).ToList();
        var maxDelta = availableDeltas.Max();
        var minDelta = availableDeltas.Min();
        
        Console.WriteLine($"Available delta range: [{minDelta:F3}-{maxDelta:F3}], " +
                         $"Required: [{config.ShortDelta.CondorMin:F2}-{config.ShortDelta.CondorMax:F2}]");
    }

    /// <summary>
    /// Tests complete failure scenarios where synthetic data is invalid.
    /// </summary>
    [Fact]
    public void TryBuild_WithCorruptedSyntheticData_ShouldHandleGracefully()
    {
        // Arrange: Create scenario with invalid market data
        var testTime = new DateTime(2024, 2, 1, 10, 30, 0);
        
        // Mock corrupted market data (zero spot price)
        var mockMarketData = new Mock<IMarketData>();
        mockMarketData.Setup(m => m.GetSpot(testTime)).Returns(0);
        
        // Act
        var order = _builder.TryBuild(testTime, Decision.Condor, mockMarketData.Object, _optionsData);
        
        // Assert
        order.Should().BeNull("Should handle corrupted market data gracefully");
    }

    // Helper methods
    private SimConfig CreateRealisticConfig()
    {
        return new SimConfig
        {
            Underlying = "XSP",
            Timezone = "America/New_York",
            
            // IMPORTANT: Use delta ranges that work with 0DTE synthetic data
            ShortDelta = new ShortDeltaCfg
            {
                CondorMin = 0.15,   // Higher than original 0.07
                CondorMax = 0.35,   // Higher than original 0.15  
                SingleMin = 0.20,   // Higher than original 0.10
                SingleMax = 0.40    // Higher than original 0.20
            },
            
            WidthPoints = new WidthPointsCfg { Min = 1, Max = 2 },
            
            // IMPORTANT: Use achievable credit requirements for 0DTE
            CreditPerWidthMin = new CreditPerWidthCfg 
            { 
                Condor = 0.08,  // Lower than original 0.18
                Single = 0.10   // Lower than original 0.20
            },
            
            Slippage = new SlippageCfg { SpreadPctCap = 0.25 },
            
            Paths = new PathsCfg
            {
                BarsCsv = Path.Combine(_testDataDir, "bars.csv"),
                VixCsv = Path.Combine(_testDataDir, "vix.csv"),
                Vix9dCsv = Path.Combine(_testDataDir, "vix9d.csv"),
                CalendarCsv = Path.Combine(_testDataDir, "calendar.csv")
            }
        };
    }

    private void CreateRealisticTestData()
    {
        // Create realistic SPY-like minute bars
        var barsContent = "ts,o,h,l,c,v\n";
        var basePrice = 495.0; // XSP-level pricing
        var startTime = new DateTime(2024, 2, 1, 9, 30, 0);
        
        for (int i = 0; i < 300; i++) // 5 hours of trading
        {
            var ts = startTime.AddMinutes(i);
            var price = basePrice + Math.Sin(i * 0.02) * 2 + (i * 0.01); // Gradual trend with noise
            var high = price + 0.5;
            var low = price - 0.5;
            var close = price + 0.2;
            var volume = 50000;
            
            barsContent += $"{ts:yyyy-MM-dd HH:mm:ss},{price:F2},{high:F2},{low:F2},{close:F2},{volume}\n";
        }
        File.WriteAllText(_testConfig.Paths.BarsCsv, barsContent);
        
        // Create realistic VIX data (moderate volatility)
        var vixContent = "date,vix\n2024-02-01,16.5\n";
        File.WriteAllText(_testConfig.Paths.VixCsv, vixContent);
        
        var vix9dContent = "date,vix9d\n2024-02-01,15.2\n";  
        File.WriteAllText(_testConfig.Paths.Vix9dCsv, vix9dContent);
        
        var calendarContent = "ts,kind\n"; // No events
        File.WriteAllText(_testConfig.Paths.CalendarCsv, calendarContent);
    }

    private void CreateLowVolatilityTestData()
    {
        // Create very low VIX scenario  
        var vixContent = "date,vix\n2024-02-01,8.5\n";  // Extremely low
        File.WriteAllText(_testConfig.Paths.VixCsv, vixContent);
        
        var vix9dContent = "date,vix9d\n2024-02-01,7.8\n";
        File.WriteAllText(_testConfig.Paths.Vix9dCsv, vix9dContent);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDataDir))
        {
            Directory.Delete(_testDataDir, recursive: true);
        }
    }
}