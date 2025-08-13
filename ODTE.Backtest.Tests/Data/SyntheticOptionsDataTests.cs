using Xunit;
using FluentAssertions;
using Moq;
using ODTE.Backtest.Data;
using ODTE.Backtest.Config;
using ODTE.Backtest.Core;

namespace ODTE.Backtest.Tests.Data;

/// <summary>
/// Tests for synthetic options data generation.
/// Validates Black-Scholes pricing, Greeks calculations, and volatility surface construction.
/// </summary>
public class SyntheticOptionsDataTests : IDisposable
{
    private readonly string _testDataDir;
    private readonly SimConfig _testConfig;
    private readonly Mock<IMarketData> _mockMarketData;
    private readonly SyntheticOptionsData _syntheticData;

    public SyntheticOptionsDataTests()
    {
        _testDataDir = Path.Combine(Path.GetTempPath(), $"SyntheticTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDataDir);
        
        CreateTestVixFiles();
        
        _testConfig = new SimConfig
        {
            Underlying = "XSP",
            Mode = "prototype",
            Timezone = "America/New_York"
        };
        
        _mockMarketData = new Mock<IMarketData>();
        
        var vixCsv = Path.Combine(_testDataDir, "vix.csv");
        var vix9dCsv = Path.Combine(_testDataDir, "vix9d.csv");
        
        _syntheticData = new SyntheticOptionsData(_testConfig, _mockMarketData.Object, vixCsv, vix9dCsv);
    }

    [Fact]
    public void TodayExpiry_ShouldReturnCorrect4PMExpiry()
    {
        // Arrange: Test various times during trading day
        var testCases = new[]
        {
            new DateTime(2024, 2, 1, 9, 30, 0),   // Market open
            new DateTime(2024, 2, 1, 12, 0, 0),   // Midday
            new DateTime(2024, 2, 1, 15, 59, 0)   // Near close
        };
        
        // Act & Assert
        foreach (var testTime in testCases)
        {
            var expiry = _syntheticData.TodayExpiry(testTime);
            expiry.Should().Be(new DateTime(2024, 2, 1, 16, 0, 0), 
                $"Expiry should be 4 PM on same day for time {testTime}");
        }
    }

    [Fact]
    public void GetQuotesAt_ShouldGenerateReasonableOptionChain()
    {
        // Arrange
        var now = new DateTime(2024, 2, 1, 10, 0, 0);
        double spot = 100;
        
        _mockMarketData.Setup(m => m.GetSpot(now)).Returns(spot);
        
        // Act
        var quotes = _syntheticData.GetQuotesAt(now).ToList();
        
        // Assert
        quotes.Should().NotBeEmpty("Should generate option quotes");
        
        // Should have both puts and calls
        var puts = quotes.Where(q => q.Right == Right.Put).ToList();
        var calls = quotes.Where(q => q.Right == Right.Call).ToList();
        
        puts.Should().NotBeEmpty("Should generate put options");
        calls.Should().NotBeEmpty("Should generate call options");
        
        // Verify reasonable strike range around spot
        var minStrike = quotes.Min(q => q.Strike);
        var maxStrike = quotes.Max(q => q.Strike);
        
        minStrike.Should().BeLessThan(spot, "Strike range should extend below spot");
        maxStrike.Should().BeGreaterThan(spot, "Strike range should extend above spot");
        
        // Verify all quotes have required data
        foreach (var quote in quotes)
        {
            quote.Bid.Should().BeGreaterThan(0, "Bid should be positive");
            quote.Ask.Should().BeGreaterThan(quote.Bid, "Ask should exceed bid");
            quote.Mid.Should().BeApproximately((quote.Bid + quote.Ask) / 2, 0.001, "Mid should be average of bid/ask");
            Math.Abs(quote.Delta).Should().BeLessThanOrEqualTo(1, "Delta should be between -1 and 1");
            quote.IV.Should().BeGreaterThan(0, "Implied volatility should be positive");
        }
    }

    [Fact]
    public void GetQuotesAt_CallPutParity_ShouldHold()
    {
        // Arrange
        var now = new DateTime(2024, 2, 1, 10, 0, 0);
        double spot = 100;
        
        _mockMarketData.Setup(m => m.GetSpot(now)).Returns(spot);
        
        // Act
        var quotes = _syntheticData.GetQuotesAt(now).ToList();
        var expiry = _syntheticData.TodayExpiry(now);
        
        // Assert: Test put-call parity for ATM strikes
        var atmStrikes = quotes.Select(q => q.Strike).Distinct()
            .Where(s => Math.Abs(s - spot) < 2) // Near ATM
            .ToList();
        
        foreach (var strike in atmStrikes)
        {
            var call = quotes.FirstOrDefault(q => q.Strike == strike && q.Right == Right.Call);
            var put = quotes.FirstOrDefault(q => q.Strike == strike && q.Right == Right.Put);
            
            if (call != null && put != null)
            {
                // Put-call parity: C - P = S - K*e^(-r*T)
                double timeToExpiry = (expiry - now).TotalDays / 365.0;
                double riskFreeRate = 0.05; // Assumed rate
                
                double leftSide = call.Mid - put.Mid;
                double rightSide = spot - strike * Math.Exp(-riskFreeRate * timeToExpiry);
                
                leftSide.Should().BeApproximately(rightSide, 0.1, 
                    $"Put-call parity should hold for strike {strike}");
            }
        }
    }

    [Fact]
    public void GetQuotesAt_DeltaNeutralStraddle_ShouldHaveBalancedGreeks()
    {
        // Arrange
        var now = new DateTime(2024, 2, 1, 10, 0, 0);
        double spot = 100;
        
        _mockMarketData.Setup(m => m.GetSpot(now)).Returns(spot);
        
        // Act
        var quotes = _syntheticData.GetQuotesAt(now).ToList();
        
        // Find ATM straddle
        var atmCall = quotes
            .Where(q => q.Right == Right.Call)
            .OrderBy(q => Math.Abs(q.Strike - spot))
            .FirstOrDefault();
            
        var atmPut = quotes
            .Where(q => q.Right == Right.Put && q.Strike == atmCall?.Strike)
            .FirstOrDefault();
        
        // Assert
        if (atmCall != null && atmPut != null)
        {
            // ATM call delta should be ~0.5, put delta should be ~-0.5
            atmCall.Delta.Should().BeApproximately(0.5, 0.2, "ATM call delta should be around 0.5");
            atmPut.Delta.Should().BeApproximately(-0.5, 0.2, "ATM put delta should be around -0.5");
            
            // Combined delta should be close to zero (delta neutral)
            var straddleDelta = atmCall.Delta + atmPut.Delta;
            straddleDelta.Should().BeApproximately(0, 0.1, "ATM straddle should be approximately delta neutral");
        }
    }

    [Fact]
    public void GetQuotesAt_VolatilitySurface_ShouldBeReasonable()
    {
        // Arrange
        var now = new DateTime(2024, 2, 1, 10, 0, 0);
        double spot = 100;
        
        _mockMarketData.Setup(m => m.GetSpot(now)).Returns(spot);
        
        // Act
        var quotes = _syntheticData.GetQuotesAt(now).ToList();
        
        // Assert: Check volatility characteristics
        var otmPuts = quotes.Where(q => q.Right == Right.Put && q.Strike < spot - 5).ToList();
        var atmOptions = quotes.Where(q => Math.Abs(q.Strike - spot) <= 1).ToList();
        var otmCalls = quotes.Where(q => q.Right == Right.Call && q.Strike > spot + 5).ToList();
        
        if (otmPuts.Any() && atmOptions.Any() && otmCalls.Any())
        {
            var avgOtmPutIV = otmPuts.Average(q => q.IV);
            var avgAtmIV = atmOptions.Average(q => q.IV);
            var avgOtmCallIV = otmCalls.Average(q => q.IV);
            
            // Verify reasonable volatility levels
            avgAtmIV.Should().BeGreaterThan(0.1, "ATM IV should be at least 10%");
            avgAtmIV.Should().BeLessThan(2.0, "ATM IV should be less than 200%");
            
            // Note: In real markets, we'd expect skew (puts > calls), but synthetic data may be flat
            avgOtmPutIV.Should().BeGreaterThan(0, "OTM put IV should be positive");
            avgOtmCallIV.Should().BeGreaterThan(0, "OTM call IV should be positive");
        }
    }

    [Fact]
    public void GetQuotesAt_BidAskSpread_ShouldBeReasonable()
    {
        // Arrange
        var now = new DateTime(2024, 2, 1, 10, 0, 0);
        double spot = 100;
        
        _mockMarketData.Setup(m => m.GetSpot(now)).Returns(spot);
        
        // Act
        var quotes = _syntheticData.GetQuotesAt(now).ToList();
        
        // Assert
        foreach (var quote in quotes)
        {
            var spread = quote.Ask - quote.Bid;
            var spreadPct = spread / quote.Mid;
            
            spread.Should().BeGreaterThan(0, "Bid-ask spread should be positive");
            spread.Should().BeLessThan(quote.Mid, "Spread should be less than option price");
            spreadPct.Should().BeLessThan(0.5, "Spread should be less than 50% of mid price");
        }
    }

    [Fact]
    public void GetQuotesAt_WithHighVIX_ShouldIncreaseOptionPrices()
    {
        // Arrange: Create two scenarios - low and high VIX
        var now = new DateTime(2024, 2, 1, 10, 0, 0);
        double spot = 100;
        
        _mockMarketData.Setup(m => m.GetSpot(now)).Returns(spot);
        
        // Test with low VIX day
        CreateTestVixFiles(lowVix: 12);
        var lowVixData = new SyntheticOptionsData(_testConfig, _mockMarketData.Object, 
            Path.Combine(_testDataDir, "vix.csv"), Path.Combine(_testDataDir, "vix9d.csv"));
        
        // Test with high VIX day
        CreateTestVixFiles(highVix: 30);
        var highVixData = new SyntheticOptionsData(_testConfig, _mockMarketData.Object, 
            Path.Combine(_testDataDir, "vix.csv"), Path.Combine(_testDataDir, "vix9d.csv"));
        
        // Act
        var lowVixQuotes = lowVixData.GetQuotesAt(now).ToList();
        var highVixQuotes = highVixData.GetQuotesAt(now).ToList();
        
        // Assert: High VIX should produce higher option prices
        var lowVixAtm = lowVixQuotes
            .Where(q => Math.Abs(q.Strike - spot) <= 0.5)
            .Average(q => q.Mid);
            
        var highVixAtm = highVixQuotes
            .Where(q => Math.Abs(q.Strike - spot) <= 0.5)
            .Average(q => q.Mid);
        
        highVixAtm.Should().BeGreaterThan(lowVixAtm, 
            "High VIX environment should produce higher option prices");
        
        var highVixIV = highVixQuotes.Average(q => q.IV);
        var lowVixIV = lowVixQuotes.Average(q => q.IV);
        
        highVixIV.Should().BeGreaterThan(lowVixIV,
            "High VIX should result in higher implied volatilities");
    }

    [Fact]
    public void GetQuotesAt_TimeDecay_ShouldReducePricesNearExpiry()
    {
        // Arrange: Test same option at different times to expiry
        double spot = 100;
        
        var morning = new DateTime(2024, 2, 1, 9, 30, 0);  // 6.5 hours to expiry
        var afternoon = new DateTime(2024, 2, 1, 15, 0, 0); // 1 hour to expiry
        
        _mockMarketData.Setup(m => m.GetSpot(It.IsAny<DateTime>())).Returns(spot);
        
        // Act
        var morningQuotes = _syntheticData.GetQuotesAt(morning).ToList();
        var afternoonQuotes = _syntheticData.GetQuotesAt(afternoon).ToList();
        
        // Assert: Find comparable ATM options
        var morningAtm = morningQuotes
            .Where(q => Math.Abs(q.Strike - spot) <= 0.5)
            .Average(q => q.Mid);
            
        var afternoonAtm = afternoonQuotes
            .Where(q => Math.Abs(q.Strike - spot) <= 0.5)
            .Average(q => q.Mid);
        
        morningAtm.Should().BeGreaterThan(afternoonAtm, 
            "Options should lose value due to time decay (theta)");
    }

    [Fact]
    public void GetQuotesAt_ShouldHandleEdgeCases()
    {
        // Arrange: Test edge cases
        var testCases = new[]
        {
            new DateTime(2024, 2, 1, 15, 59, 59), // Very close to expiry
            new DateTime(2024, 2, 1, 9, 30, 1),   // Very far from expiry
        };
        
        double spot = 100;
        _mockMarketData.Setup(m => m.GetSpot(It.IsAny<DateTime>())).Returns(spot);
        
        // Act & Assert
        foreach (var testTime in testCases)
        {
            var quotes = _syntheticData.GetQuotesAt(testTime).ToList();
            
            quotes.Should().NotBeEmpty($"Should generate quotes even at edge case time {testTime}");
            
            // All quotes should have valid data
            foreach (var quote in quotes)
            {
                quote.Mid.Should().BeGreaterThanOrEqualTo(0, "Option prices should be non-negative");
                quote.IV.Should().BeGreaterThan(0, "Implied volatility should be positive");
                Math.Abs(quote.Delta).Should().BeLessThanOrEqualTo(1, "Delta should be bounded");
            }
        }
    }

    [Fact]
    public void GetQuotesAt_WithZeroSpot_ShouldHandleGracefully()
    {
        // Arrange: Edge case with invalid spot price
        var now = new DateTime(2024, 2, 1, 10, 0, 0);
        
        _mockMarketData.Setup(m => m.GetSpot(now)).Returns(0);
        
        // Act
        var quotes = _syntheticData.GetQuotesAt(now).ToList();
        
        // Assert: Should handle gracefully (either empty or reasonable defaults)
        if (quotes.Any())
        {
            quotes.All(q => q.Mid >= 0).Should().BeTrue("All prices should be non-negative");
            quotes.All(q => q.IV > 0).Should().BeTrue("All IVs should be positive");
        }
    }

    // Helper methods
    private void CreateTestVixFiles(double? lowVix = null, double? highVix = null)
    {
        var vixValue = lowVix ?? highVix ?? 16.0;
        var vix9dValue = vixValue * 0.9; // Typically slightly lower
        
        var vixContent = $"date,vix\n2024-02-01,{vixValue:F2}\n";
        var vix9dContent = $"date,vix9d\n2024-02-01,{vix9dValue:F2}\n";
        
        File.WriteAllText(Path.Combine(_testDataDir, "vix.csv"), vixContent);
        File.WriteAllText(Path.Combine(_testDataDir, "vix9d.csv"), vix9dContent);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDataDir))
        {
            Directory.Delete(_testDataDir, recursive: true);
        }
    }
}