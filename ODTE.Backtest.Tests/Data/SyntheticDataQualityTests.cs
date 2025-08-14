using Xunit;
using FluentAssertions;
using ODTE.Backtest.Data;
using ODTE.Backtest.Config;
using ODTE.Backtest.Core;

namespace ODTE.Backtest.Tests.Data;

/// <summary>
/// Quality validation tests for synthetic options data generation.
/// These tests ensure synthetic data meets the requirements for strategy execution.
/// 
/// KEY INSIGHT: Testing data properties isn't enough - must test USABILITY for trading strategies.
/// </summary>
public class SyntheticDataQualityTests : IDisposable
{
    private readonly string _testDataDir;
    private readonly SimConfig _testConfig;
    private readonly CsvMarketData _marketData;
    private readonly SyntheticOptionsData _syntheticData;

    public SyntheticDataQualityTests()
    {
        _testDataDir = Path.Combine(Path.GetTempPath(), $"SyntheticQuality_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDataDir);
        
        _testConfig = CreateTestConfig();
        CreateTestDataFiles();
        
        _marketData = new CsvMarketData(_testConfig.Paths.BarsCsv, _testConfig.Timezone, _testConfig.RthOnly);
        _syntheticData = new SyntheticOptionsData(_testConfig, _marketData, 
            _testConfig.Paths.VixCsv, _testConfig.Paths.Vix9dCsv);
    }

    /// <summary>
    /// CRITICAL TEST: Validates that synthetic data generates sufficient strikes for spread construction.
    /// This would have caught the issue where only 1-2 strikes were in target delta ranges.
    /// </summary>
    [Theory]
    [InlineData(0.07, 0.15)]  // Original condor deltas
    [InlineData(0.10, 0.20)]  // Original single deltas
    [InlineData(0.15, 0.35)]  // Current working deltas
    public void GetQuotesAt_ShouldProvideAdequateStrikeDensity(double deltaMin, double deltaMax)
    {
        // Arrange
        var testTime = new DateTime(2024, 2, 1, 10, 30, 0);
        var spot = _marketData.GetSpot(testTime);
        
        // Act
        var quotes = _syntheticData.GetQuotesAt(testTime).ToList();
        
        // Assert: Sufficient strike density for spread construction
        var putsInRange = quotes.Where(q => q.Right == Right.Put && 
                                           Math.Abs(q.Delta) >= deltaMin && 
                                           Math.Abs(q.Delta) <= deltaMax).ToList();
        
        var callsInRange = quotes.Where(q => q.Right == Right.Call && 
                                            Math.Abs(q.Delta) >= deltaMin && 
                                            Math.Abs(q.Delta) <= deltaMax).ToList();
        
        // Need at least 2 strikes per side for spread construction
        putsInRange.Should().HaveCountGreaterThanOrEqualTo(2, 
            $"Need at least 2 put strikes in delta range [{deltaMin:F2}-{deltaMax:F2}] for spread construction");
        
        callsInRange.Should().HaveCountGreaterThanOrEqualTo(2,
            $"Need at least 2 call strikes in delta range [{deltaMin:F2}-{deltaMax:F2}] for spread construction");
        
        // Strikes should be reasonably spaced (not all clustered)
        if (putsInRange.Count >= 2)
        {
            var putStrikes = putsInRange.Select(q => q.Strike).OrderBy(k => k).ToList();
            var minPutSpacing = putStrikes.Zip(putStrikes.Skip(1), (a, b) => b - a).Min();
            minPutSpacing.Should().BeGreaterThan(0.5, "Put strikes should have reasonable spacing");
        }
        
        // Log results for analysis
        Console.WriteLine($"Delta [{deltaMin:F2}-{deltaMax:F2}]: {putsInRange.Count} puts, {callsInRange.Count} calls around spot {spot:F1}");
    }

    /// <summary>
    /// Tests that synthetic options have sufficient value to support credit spread construction.
    /// This would have caught the $0.05 minimum bid issue.
    /// </summary>
    [Theory]
    [InlineData(10, 0)]   // Morning (6+ hours to expiry)
    [InlineData(12, 0)]   // Midday (4 hours to expiry)
    [InlineData(14, 0)]   // Afternoon (2 hours to expiry)
    [InlineData(15, 30)]  // Late day (30 min to expiry)
    public void GetQuotesAt_ShouldGenerateMeaningfulOptionValues(int hour, int minute)
    {
        // Arrange
        var testTime = new DateTime(2024, 2, 1, hour, minute, 0);
        var spot = _marketData.GetSpot(testTime);
        
        // Act
        var quotes = _syntheticData.GetQuotesAt(testTime).ToList();
        
        // Assert: Options should have meaningful values based on time to expiry
        var timeToExpiry = (new DateTime(2024, 2, 1, 16, 0, 0) - testTime).TotalHours;
        var expectedMinValue = timeToExpiry > 1 ? 0.10 : 0.05; // Higher values with more time
        
        // Near-money options should have reasonable values
        var nearMoneyOptions = quotes.Where(q => Math.Abs(q.Strike - spot) <= 3).ToList();
        nearMoneyOptions.Should().NotBeEmpty("Should have near-money options");
        
        var meaningfulValueOptions = nearMoneyOptions.Where(q => q.Mid >= expectedMinValue).ToList();
        var meaningfulPercentage = (double)meaningfulValueOptions.Count / nearMoneyOptions.Count;
        
        meaningfulPercentage.Should().BeGreaterThan(0.3, 
            $"At least 30% of near-money options should have value >= ${expectedMinValue:F2} " +
            $"with {timeToExpiry:F1} hours to expiry");
        
        // Check for spread construction viability
        var avgNearMoneyValue = nearMoneyOptions.Average(q => q.Mid);
        Console.WriteLine($"Time {hour:D2}:{minute:D2} (T={timeToExpiry:F1}h): " +
                         $"Avg near-money value ${avgNearMoneyValue:F3}, " +
                         $"{meaningfulPercentage:P0} above ${expectedMinValue:F2}");
    }

    /// <summary>
    /// Tests bid-ask spread realism for credit spread feasibility.
    /// Wide spreads can make positive credit impossible.
    /// </summary>
    [Fact]
    public void GetQuotesAt_BidAskSpreads_ShouldEnableCreditSpreads()
    {
        // Arrange
        var testTime = new DateTime(2024, 2, 1, 11, 0, 0);
        var spot = _marketData.GetSpot(testTime);
        
        // Act
        var quotes = _syntheticData.GetQuotesAt(testTime).ToList();
        
        // Assert: Test actual credit spread construction
        var puts = quotes.Where(q => q.Right == Right.Put).OrderByDescending(q => q.Strike).ToList();
        var calls = quotes.Where(q => q.Right == Right.Call).OrderBy(q => q.Strike).ToList();
        
        // Try to construct 1-point put credit spread
        if (puts.Count >= 2)
        {
            for (int i = 0; i < puts.Count - 1; i++)
            {
                var shortPut = puts[i];    // Higher strike (sell)
                var longPut = puts[i + 1]; // Lower strike (buy)
                
                var strikeGap = shortPut.Strike - longPut.Strike;
                if (strikeGap >= 0.8 && strikeGap <= 2.0) // Reasonable spread width
                {
                    var credit = shortPut.Bid - longPut.Ask;
                    if (credit > 0)
                    {
                        var creditPerWidth = credit / strikeGap;
                        Console.WriteLine($"✅ Put spread possible: {shortPut.Strike:F1}/{longPut.Strike:F1} " +
                                        $"Credit=${credit:F2} Width={strikeGap:F1} C/W={creditPerWidth:F3}");
                        
                        // At least one viable spread should exist
                        creditPerWidth.Should().BeGreaterThan(0.05, 
                            "Should be possible to construct put spreads with meaningful credit/width ratio");
                        return; // Found viable spread
                    }
                }
            }
            
            Assert.Fail("No viable put credit spreads found in synthetic data");
        }
    }

    /// <summary>
    /// Tests delta distribution to ensure realistic option Greeks.
    /// </summary>
    [Fact] 
    public void GetQuotesAt_DeltaDistribution_ShouldBeRealistic()
    {
        // Arrange
        var testTime = new DateTime(2024, 2, 1, 12, 0, 0);
        var spot = _marketData.GetSpot(testTime);
        
        // Act
        var quotes = _syntheticData.GetQuotesAt(testTime).ToList();
        
        // Assert: Delta distribution should follow expected patterns
        var puts = quotes.Where(q => q.Right == Right.Put).ToList();
        var calls = quotes.Where(q => q.Right == Right.Call).ToList();
        
        puts.Should().NotBeEmpty();
        calls.Should().NotBeEmpty();
        
        // ATM options should have ~0.5 delta
        var atmPuts = puts.Where(q => Math.Abs(q.Strike - spot) <= 1).ToList();
        var atmCalls = calls.Where(q => Math.Abs(q.Strike - spot) <= 1).ToList();
        
        if (atmPuts.Any())
        {
            var avgAtmPutDelta = Math.Abs(atmPuts.Average(q => q.Delta));
            avgAtmPutDelta.Should().BeInRange(0.3, 0.7, "ATM put deltas should be near 0.5");
        }
        
        if (atmCalls.Any())
        {
            var avgAtmCallDelta = atmCalls.Average(q => q.Delta);
            avgAtmCallDelta.Should().BeInRange(0.3, 0.7, "ATM call deltas should be near 0.5");
        }
        
        // OTM options should have lower deltas
        var otmPuts = puts.Where(q => q.Strike < spot - 5).ToList();
        var otmCalls = calls.Where(q => q.Strike > spot + 5).ToList();
        
        if (otmPuts.Any())
        {
            var maxOtmPutDelta = Math.Abs(otmPuts.Max(q => q.Delta));
            maxOtmPutDelta.Should().BeLessThan(0.3, "Far OTM puts should have low deltas");
        }
        
        if (otmCalls.Any())
        {
            var maxOtmCallDelta = otmCalls.Max(q => q.Delta);
            maxOtmCallDelta.Should().BeLessThan(0.3, "Far OTM calls should have low deltas");
        }
    }

    /// <summary>
    /// Tests volatility scaling effects on option values.
    /// Critical for ensuring synthetic data responds appropriately to VIX changes.
    /// </summary>
    [Theory]
    [InlineData(10)]  // Low VIX
    [InlineData(16)]  // Normal VIX 
    [InlineData(25)]  // High VIX
    [InlineData(35)]  // Very high VIX
    public void GetQuotesAt_VolatilityScaling_ShouldAffectValues(double vixLevel)
    {
        // Arrange: Create specific VIX scenario
        var vixContent = $"date,vix\n2024-02-01,{vixLevel:F1}\n";
        var vix9dContent = $"date,vix9d\n2024-02-01,{vixLevel * 0.9:F1}\n";
        
        File.WriteAllText(_testConfig.Paths.VixCsv, vixContent);
        File.WriteAllText(_testConfig.Paths.Vix9dCsv, vix9dContent);
        
        var volData = new SyntheticOptionsData(_testConfig, _marketData, 
            _testConfig.Paths.VixCsv, _testConfig.Paths.Vix9dCsv);
        
        var testTime = new DateTime(2024, 2, 1, 11, 0, 0);
        var spot = _marketData.GetSpot(testTime);
        
        // Act
        var quotes = volData.GetQuotesAt(testTime).ToList();
        
        // Assert: Option values should scale with volatility
        var nearMoneyOptions = quotes.Where(q => Math.Abs(q.Strike - spot) <= 2).ToList();
        nearMoneyOptions.Should().NotBeEmpty();
        
        var avgOptionValue = nearMoneyOptions.Average(q => q.Mid);
        var avgImpliedVol = nearMoneyOptions.Average(q => q.IV);
        
        // Expected relationship: higher VIX → higher option values
        Console.WriteLine($"VIX {vixLevel}: Avg option value ${avgOptionValue:F3}, Avg IV {avgImpliedVol:F3}");
        
        // Basic sanity checks
        avgOptionValue.Should().BeGreaterThan(0.05, "Options should have meaningful value even in low vol");
        avgImpliedVol.Should().BeGreaterThan(0.05, "Implied volatility should be reasonable");
        
        if (vixLevel >= 25) // High vol environment
        {
            avgOptionValue.Should().BeGreaterThan(0.20, "High VIX should produce higher option values");
        }
        
        if (vixLevel <= 12) // Low vol environment  
        {
            var viableSpreadCount = 0;
            var puts = quotes.Where(q => q.Right == Right.Put).OrderByDescending(q => q.Strike).ToList();
            
            for (int i = 0; i < Math.Min(puts.Count - 1, 10); i++)
            {
                var credit = puts[i].Bid - puts[i + 1].Ask;
                if (credit > 0.05) viableSpreadCount++;
            }
            
            if (viableSpreadCount == 0)
            {
                Console.WriteLine($"⚠️  VIX {vixLevel} may be too low for viable spread construction");
            }
        }
    }

    /// <summary>
    /// Tests strike grid coverage and spacing for spread construction requirements.
    /// </summary>
    [Fact]
    public void GetQuotesAt_StrikeGrid_ShouldSupportVariousSpreadWidths()
    {
        // Arrange
        var testTime = new DateTime(2024, 2, 1, 13, 0, 0);
        var spot = _marketData.GetSpot(testTime);
        
        // Act
        var quotes = _syntheticData.GetQuotesAt(testTime).ToList();
        var strikes = quotes.Select(q => q.Strike).Distinct().OrderBy(k => k).ToList();
        
        // Assert: Strike grid should support 1-2 point spreads
        strikes.Should().HaveCountGreaterThanOrEqualTo(10, "Should have adequate strike coverage");
        
        // Check spacing around ATM
        var atmStrikes = strikes.Where(k => Math.Abs(k - spot) <= 5).ToList();
        atmStrikes.Should().HaveCountGreaterThanOrEqualTo(5, "Should have dense strikes around ATM");
        
        // Verify 1-point spacing is available
        var onePointSpreads = 0;
        var twoPointSpreads = 0;
        
        for (int i = 0; i < atmStrikes.Count - 1; i++)
        {
            var gap = atmStrikes[i + 1] - atmStrikes[i];
            if (Math.Abs(gap - 1.0) < 0.1) onePointSpreads++;
            if (Math.Abs(gap - 2.0) < 0.1) twoPointSpreads++;
        }
        
        (onePointSpreads + twoPointSpreads).Should().BeGreaterThan(0, 
            "Should have strikes spaced for 1-point or 2-point spreads");
        
        Console.WriteLine($"Strike grid around {spot:F1}: {onePointSpreads} 1-point gaps, {twoPointSpreads} 2-point gaps");
    }

    /// <summary>
    /// Integration test: validates that synthetic data enables end-to-end strategy execution.
    /// </summary>
    [Fact]
    public void GetQuotesAt_StrategyCompatibility_ShouldEnableTrading()
    {
        // Arrange: Test multiple time points throughout the day
        var testTimes = new[]
        {
            new DateTime(2024, 2, 1, 10, 0, 0),  // Morning
            new DateTime(2024, 2, 1, 12, 0, 0),  // Midday
            new DateTime(2024, 2, 1, 14, 0, 0),  // Afternoon
        };
        
        var successfulTimes = 0;
        
        foreach (var testTime in testTimes)
        {
            // Act
            var quotes = _syntheticData.GetQuotesAt(testTime).ToList();
            var spot = _marketData.GetSpot(testTime);
            
            // Simulate strategy requirements
            var condorPuts = quotes.Where(q => q.Right == Right.Put && 
                                          Math.Abs(q.Delta) >= 0.15 && 
                                          Math.Abs(q.Delta) <= 0.35).ToList();
            
            var condorCalls = quotes.Where(q => q.Right == Right.Call && 
                                           Math.Abs(q.Delta) >= 0.15 && 
                                           Math.Abs(q.Delta) <= 0.35).ToList();
            
            // Check if viable condor is possible
            if (condorPuts.Count >= 2 && condorCalls.Count >= 2)
            {
                var shortPut = condorPuts.OrderBy(q => Math.Abs(q.Delta)).First();
                var shortCall = condorCalls.OrderBy(q => Math.Abs(q.Delta)).First();
                
                // Find protective wings
                var longPut = quotes.Where(q => q.Right == Right.Put)
                                   .OrderBy(q => Math.Abs(q.Strike - (shortPut.Strike - 1)))
                                   .FirstOrDefault();
                                   
                var longCall = quotes.Where(q => q.Right == Right.Call)
                                    .OrderBy(q => Math.Abs(q.Strike - (shortCall.Strike + 1)))
                                    .FirstOrDefault();
                
                if (longPut != null && longCall != null)
                {
                    var putCredit = shortPut.Bid - longPut.Ask;
                    var callCredit = shortCall.Bid - longCall.Ask;
                    var totalCredit = putCredit + callCredit;
                    
                    if (totalCredit > 0.08) // 8% C/W for 1-point spreads
                    {
                        successfulTimes++;
                        Console.WriteLine($"✅ {testTime:HH:mm}: Viable condor possible, total credit ${totalCredit:F2}");
                    }
                    else
                    {
                        Console.WriteLine($"❌ {testTime:HH:mm}: Insufficient credit ${totalCredit:F2}");
                    }
                }
                else
                {
                    Console.WriteLine($"❌ {testTime:HH:mm}: No protective wings available");
                }
            }
            else
            {
                Console.WriteLine($"❌ {testTime:HH:mm}: Insufficient strikes in delta range (puts:{condorPuts.Count}, calls:{condorCalls.Count})");
            }
        }
        
        // Assert: Should be successful at least some of the time
        successfulTimes.Should().BeGreaterThan(0, 
            "Synthetic data should enable strategy execution at least some times during the day");
    }

    // Helper methods
    private SimConfig CreateTestConfig()
    {
        return new SimConfig
        {
            Underlying = "XSP",
            Timezone = "America/New_York",
            RthOnly = true,
            
            Paths = new PathsCfg
            {
                BarsCsv = Path.Combine(_testDataDir, "bars.csv"),
                VixCsv = Path.Combine(_testDataDir, "vix.csv"),
                Vix9dCsv = Path.Combine(_testDataDir, "vix9d.csv"),
                CalendarCsv = Path.Combine(_testDataDir, "calendar.csv")
            }
        };
    }

    private void CreateTestDataFiles()
    {
        // Create realistic intraday price movement
        var barsContent = "ts,o,h,l,c,v\n";
        var basePrice = 495.0;
        var startTime = new DateTime(2024, 2, 1, 9, 30, 0);
        
        for (int i = 0; i < 390; i++) // Full trading day
        {
            var ts = startTime.AddMinutes(i);
            var price = basePrice + Math.Sin(i * 0.01) * 1.5; // Smooth price movement
            var high = price + 0.3;
            var low = price - 0.3;
            var close = price + 0.1;
            var volume = 25000;
            
            barsContent += $"{ts:yyyy-MM-dd HH:mm:ss},{price:F2},{high:F2},{low:F2},{close:F2},{volume}\n";
        }
        File.WriteAllText(_testConfig.Paths.BarsCsv, barsContent);
        
        // Default VIX data (will be overridden by specific tests)
        var vixContent = "date,vix\n2024-02-01,16.0\n";
        File.WriteAllText(_testConfig.Paths.VixCsv, vixContent);
        
        var vix9dContent = "date,vix9d\n2024-02-01,15.0\n";
        File.WriteAllText(_testConfig.Paths.Vix9dCsv, vix9dContent);
        
        var calendarContent = "ts,kind\n";
        File.WriteAllText(_testConfig.Paths.CalendarCsv, calendarContent);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDataDir))
        {
            Directory.Delete(_testDataDir, recursive: true);
        }
    }
}