using Xunit;
using FluentAssertions;
using Moq;
using ODTE.Backtest.Strategy;
using ODTE.Backtest.Config;
using ODTE.Backtest.Data;
using ODTE.Backtest.Core;

namespace ODTE.Backtest.Tests.Strategy;

/// <summary>
/// Tests for the SpreadBuilder component that constructs option spreads.
/// Validates strike selection, credit/width ratios, and spread construction logic.
/// </summary>
public class SpreadBuilderTests
{
    private readonly SimConfig _testConfig;
    private readonly SpreadBuilder _builder;
    private readonly Mock<IMarketData> _mockMarketData;
    private readonly Mock<IOptionsData> _mockOptionsData;

    public SpreadBuilderTests()
    {
        _testConfig = new SimConfig
        {
            Underlying = "XSP",
            ShortDelta = new DeltaConfig
            {
                CondorMin = 0.07,
                CondorMax = 0.15,
                SingleMin = 0.10,
                SingleMax = 0.20
            },
            WidthPoints = new WidthConfig
            {
                Min = 1,
                Max = 2
            },
            CreditPerWidthMin = new CreditConfig
            {
                Condor = 0.18,
                Single = 0.20
            },
            Slippage = new SlippageConfig
            {
                SpreadPctCap = 0.25
            }
        };
        
        _builder = new SpreadBuilder(_testConfig);
        _mockMarketData = new Mock<IMarketData>();
        _mockOptionsData = new Mock<IOptionsData>();
    }

    [Fact]
    public void TryBuild_IronCondor_ShouldConstructValidSpread()
    {
        // Arrange: Setup for iron condor construction
        var now = new DateTime(2024, 2, 1, 10, 0, 0);
        var expiry = new DateTime(2024, 2, 1, 16, 0, 0);
        double spot = 100;
        
        _mockMarketData.Setup(m => m.GetSpot(now)).Returns(spot);
        _mockOptionsData.Setup(o => o.TodayExpiry(now)).Returns(expiry);
        
        // Create option chain with suitable strikes for iron condor
        var quotes = CreateIronCondorQuotes(spot, expiry);
        _mockOptionsData.Setup(o => o.GetQuotesAt(now)).Returns(quotes);
        
        var decision = Decision.CondorGo();
        
        // Act
        var order = _builder.TryBuild(now, decision, _mockMarketData.Object, _mockOptionsData.Object);
        
        // Assert
        order.Should().NotBeNull("Should construct iron condor with suitable quotes");
        order!.Type.Should().Be(PositionType.IronCondor);
        
        // Verify put spread
        order.Short.Right.Should().Be(Right.Put, "Short leg of put spread");
        order.Long.Right.Should().Be(Right.Put, "Long leg of put spread");
        order.Short.Strike.Should().BeGreaterThan(order.Long.Strike, "Put spread: short strike > long strike");
        
        // Verify call spread
        order.Short2.Should().NotBeNull("Iron condor needs call spread");
        order.Long2.Should().NotBeNull("Iron condor needs call spread");
        order.Short2!.Right.Should().Be(Right.Call, "Short leg of call spread");
        order.Long2!.Right.Should().Be(Right.Call, "Long leg of call spread");
        order.Short2.Strike.Should().BeLessThan(order.Long2.Strike, "Call spread: short strike < long strike");
        
        // Verify credit received
        order.NetCredit.Should().BeGreaterThan(0, "Should receive net credit");
        
        // Verify width constraints
        var putWidth = order.Short.Strike - order.Long.Strike;
        var callWidth = order.Long2!.Strike - order.Short2!.Strike;
        putWidth.Should().BeGreaterThanOrEqualTo(_testConfig.WidthPoints.Min);
        putWidth.Should().BeLessThanOrEqualTo(_testConfig.WidthPoints.Max);
        callWidth.Should().BeGreaterThanOrEqualTo(_testConfig.WidthPoints.Min);
        callWidth.Should().BeLessThanOrEqualTo(_testConfig.WidthPoints.Max);
    }

    [Fact]
    public void TryBuild_PutCreditSpread_ShouldConstructBullishSpread()
    {
        // Arrange: Setup for put credit spread (bullish)
        var now = new DateTime(2024, 2, 1, 10, 0, 0);
        var expiry = new DateTime(2024, 2, 1, 16, 0, 0);
        double spot = 100;
        
        _mockMarketData.Setup(m => m.GetSpot(now)).Returns(spot);
        _mockOptionsData.Setup(o => o.TodayExpiry(now)).Returns(expiry);
        
        var quotes = CreatePutSpreadQuotes(spot, expiry);
        _mockOptionsData.Setup(o => o.GetQuotesAt(now)).Returns(quotes);
        
        var decision = Decision.PutSpreadGo();
        
        // Act
        var order = _builder.TryBuild(now, decision, _mockMarketData.Object, _mockOptionsData.Object);
        
        // Assert
        order.Should().NotBeNull("Should construct put spread with suitable quotes");
        order!.Type.Should().Be(PositionType.PutSpread);
        
        // Verify it's a credit spread (sell higher strike, buy lower strike)
        order.Short.Right.Should().Be(Right.Put);
        order.Long.Right.Should().Be(Right.Put);
        order.Short.Strike.Should().BeGreaterThan(order.Long.Strike, 
            "Put credit spread: sell higher strike, buy lower strike");
        
        // Verify net credit
        order.NetCredit.Should().BeGreaterThan(0, "Credit spread should receive premium");
        
        // Verify delta constraints
        Math.Abs(order.Short.Delta).Should().BeGreaterThanOrEqualTo(_testConfig.ShortDelta.SingleMin);
        Math.Abs(order.Short.Delta).Should().BeLessThanOrEqualTo(_testConfig.ShortDelta.SingleMax);
    }

    [Fact]
    public void TryBuild_CallCreditSpread_ShouldConstructBearishSpread()
    {
        // Arrange: Setup for call credit spread (bearish)
        var now = new DateTime(2024, 2, 1, 10, 0, 0);
        var expiry = new DateTime(2024, 2, 1, 16, 0, 0);
        double spot = 100;
        
        _mockMarketData.Setup(m => m.GetSpot(now)).Returns(spot);
        _mockOptionsData.Setup(o => o.TodayExpiry(now)).Returns(expiry);
        
        var quotes = CreateCallSpreadQuotes(spot, expiry);
        _mockOptionsData.Setup(o => o.GetQuotesAt(now)).Returns(quotes);
        
        var decision = Decision.CallSpreadGo();
        
        // Act
        var order = _builder.TryBuild(now, decision, _mockMarketData.Object, _mockOptionsData.Object);
        
        // Assert
        order.Should().NotBeNull("Should construct call spread with suitable quotes");
        order!.Type.Should().Be(PositionType.CallSpread);
        
        // Verify it's a credit spread (sell lower strike, buy higher strike)
        order.Short.Right.Should().Be(Right.Call);
        order.Long.Right.Should().Be(Right.Call);
        order.Short.Strike.Should().BeLessThan(order.Long.Strike, 
            "Call credit spread: sell lower strike, buy higher strike");
        
        // Verify net credit
        order.NetCredit.Should().BeGreaterThan(0, "Credit spread should receive premium");
    }

    [Fact]
    public void TryBuild_WithInsufficientCredit_ShouldReturnNull()
    {
        // Arrange: Setup quotes with poor credit/width ratio
        var now = new DateTime(2024, 2, 1, 10, 0, 0);
        var expiry = new DateTime(2024, 2, 1, 16, 0, 0);
        double spot = 100;
        
        _mockMarketData.Setup(m => m.GetSpot(now)).Returns(spot);
        _mockOptionsData.Setup(o => o.TodayExpiry(now)).Returns(expiry);
        
        // Create quotes with very low premium (insufficient credit)
        var quotes = CreateLowPremiumQuotes(spot, expiry);
        _mockOptionsData.Setup(o => o.GetQuotesAt(now)).Returns(quotes);
        
        var decision = Decision.PutSpreadGo();
        
        // Act
        var order = _builder.TryBuild(now, decision, _mockMarketData.Object, _mockOptionsData.Object);
        
        // Assert
        order.Should().BeNull("Should reject spread with insufficient credit/width ratio");
    }

    [Fact]
    public void TryBuild_WithWideBidAskSpread_ShouldReturnNull()
    {
        // Arrange: Setup quotes with excessively wide bid-ask spreads
        var now = new DateTime(2024, 2, 1, 10, 0, 0);
        var expiry = new DateTime(2024, 2, 1, 16, 0, 0);
        double spot = 100;
        
        _mockMarketData.Setup(m => m.GetSpot(now)).Returns(spot);
        _mockOptionsData.Setup(o => o.TodayExpiry(now)).Returns(expiry);
        
        // Create illiquid quotes with wide spreads
        var quotes = CreateIlliquidQuotes(spot, expiry);
        _mockOptionsData.Setup(o => o.GetQuotesAt(now)).Returns(quotes);
        
        var decision = Decision.CondorGo();
        
        // Act
        var order = _builder.TryBuild(now, decision, _mockMarketData.Object, _mockOptionsData.Object);
        
        // Assert
        order.Should().BeNull("Should reject spread with excessive bid-ask spread");
    }

    [Fact]
    public void TryBuild_WithNoStrikesInDeltaRange_ShouldReturnNull()
    {
        // Arrange: No strikes within configured delta range
        var now = new DateTime(2024, 2, 1, 10, 0, 0);
        var expiry = new DateTime(2024, 2, 1, 16, 0, 0);
        double spot = 100;
        
        _mockMarketData.Setup(m => m.GetSpot(now)).Returns(spot);
        _mockOptionsData.Setup(o => o.TodayExpiry(now)).Returns(expiry);
        
        // Create quotes with deltas outside configured range
        var quotes = CreateExtremeDelataQuotes(spot, expiry);
        _mockOptionsData.Setup(o => o.GetQuotesAt(now)).Returns(quotes);
        
        var decision = Decision.PutSpreadGo();
        
        // Act
        var order = _builder.TryBuild(now, decision, _mockMarketData.Object, _mockOptionsData.Object);
        
        // Assert
        order.Should().BeNull("Should return null when no strikes match delta criteria");
    }

    [Fact]
    public void TryBuild_ValidatesMinimumCreditPerWidth()
    {
        // Arrange
        var now = new DateTime(2024, 2, 1, 10, 0, 0);
        var expiry = new DateTime(2024, 2, 1, 16, 0, 0);
        double spot = 100;
        
        _mockMarketData.Setup(m => m.GetSpot(now)).Returns(spot);
        _mockOptionsData.Setup(o => o.TodayExpiry(now)).Returns(expiry);
        
        // Create quotes that will produce specific credit/width ratio
        var quotes = new List<OptionQuote>
        {
            // Put options for testing credit/width validation
            new OptionQuote { Strike = 98, Right = Right.Put, Expiry = expiry, 
                Bid = 0.50, Ask = 0.60, Mid = 0.55, Delta = -0.15, IV = 0.20 },
            new OptionQuote { Strike = 97, Right = Right.Put, Expiry = expiry, 
                Bid = 0.35, Ask = 0.45, Mid = 0.40, Delta = -0.10, IV = 0.20 }
        };
        
        _mockOptionsData.Setup(o => o.GetQuotesAt(now)).Returns(quotes);
        
        var decision = Decision.PutSpreadGo();
        
        // Act
        var order = _builder.TryBuild(now, decision, _mockMarketData.Object, _mockOptionsData.Object);
        
        // Assert
        if (order != null)
        {
            var width = order.Short.Strike - order.Long.Strike;
            var creditRatio = order.NetCredit / width;
            creditRatio.Should().BeGreaterThanOrEqualTo(_testConfig.CreditPerWidthMin.Single,
                "Credit/width ratio should meet minimum threshold");
        }
    }

    [Fact]
    public void TryBuild_WithNoGoDecision_ShouldReturnNull()
    {
        // Arrange
        var now = new DateTime(2024, 2, 1, 10, 0, 0);
        var decision = Decision.NoGo();
        
        // Act
        var order = _builder.TryBuild(now, decision, _mockMarketData.Object, _mockOptionsData.Object);
        
        // Assert
        order.Should().BeNull("NoGo decision should not produce any orders");
    }

    // Helper methods to create test option chains
    private List<OptionQuote> CreateIronCondorQuotes(double spot, DateTime expiry)
    {
        return new List<OptionQuote>
        {
            // Put options for put spread
            new OptionQuote { Strike = spot - 3, Right = Right.Put, Expiry = expiry, 
                Bid = 0.70, Ask = 0.80, Mid = 0.75, Delta = -0.12, IV = 0.20 },
            new OptionQuote { Strike = spot - 4, Right = Right.Put, Expiry = expiry, 
                Bid = 0.45, Ask = 0.55, Mid = 0.50, Delta = -0.08, IV = 0.20 },
            
            // Call options for call spread
            new OptionQuote { Strike = spot + 3, Right = Right.Call, Expiry = expiry, 
                Bid = 0.70, Ask = 0.80, Mid = 0.75, Delta = 0.12, IV = 0.20 },
            new OptionQuote { Strike = spot + 4, Right = Right.Call, Expiry = expiry, 
                Bid = 0.45, Ask = 0.55, Mid = 0.50, Delta = 0.08, IV = 0.20 }
        };
    }

    private List<OptionQuote> CreatePutSpreadQuotes(double spot, DateTime expiry)
    {
        return new List<OptionQuote>
        {
            new OptionQuote { Strike = spot - 2, Right = Right.Put, Expiry = expiry, 
                Bid = 0.90, Ask = 1.00, Mid = 0.95, Delta = -0.15, IV = 0.22 },
            new OptionQuote { Strike = spot - 3, Right = Right.Put, Expiry = expiry, 
                Bid = 0.60, Ask = 0.70, Mid = 0.65, Delta = -0.10, IV = 0.21 },
            new OptionQuote { Strike = spot - 4, Right = Right.Put, Expiry = expiry, 
                Bid = 0.40, Ask = 0.50, Mid = 0.45, Delta = -0.07, IV = 0.20 }
        };
    }

    private List<OptionQuote> CreateCallSpreadQuotes(double spot, DateTime expiry)
    {
        return new List<OptionQuote>
        {
            new OptionQuote { Strike = spot + 2, Right = Right.Call, Expiry = expiry, 
                Bid = 0.90, Ask = 1.00, Mid = 0.95, Delta = 0.15, IV = 0.22 },
            new OptionQuote { Strike = spot + 3, Right = Right.Call, Expiry = expiry, 
                Bid = 0.60, Ask = 0.70, Mid = 0.65, Delta = 0.10, IV = 0.21 },
            new OptionQuote { Strike = spot + 4, Right = Right.Call, Expiry = expiry, 
                Bid = 0.40, Ask = 0.50, Mid = 0.45, Delta = 0.07, IV = 0.20 }
        };
    }

    private List<OptionQuote> CreateLowPremiumQuotes(double spot, DateTime expiry)
    {
        // Very low premiums that won't meet credit/width requirements
        return new List<OptionQuote>
        {
            new OptionQuote { Strike = spot - 2, Right = Right.Put, Expiry = expiry, 
                Bid = 0.10, Ask = 0.15, Mid = 0.125, Delta = -0.15, IV = 0.10 },
            new OptionQuote { Strike = spot - 3, Right = Right.Put, Expiry = expiry, 
                Bid = 0.05, Ask = 0.10, Mid = 0.075, Delta = -0.10, IV = 0.10 }
        };
    }

    private List<OptionQuote> CreateIlliquidQuotes(double spot, DateTime expiry)
    {
        // Wide bid-ask spreads indicating illiquidity
        return new List<OptionQuote>
        {
            new OptionQuote { Strike = spot - 2, Right = Right.Put, Expiry = expiry, 
                Bid = 0.50, Ask = 1.50, Mid = 1.00, Delta = -0.15, IV = 0.20 },
            new OptionQuote { Strike = spot - 3, Right = Right.Put, Expiry = expiry, 
                Bid = 0.30, Ask = 1.20, Mid = 0.75, Delta = -0.10, IV = 0.20 }
        };
    }

    private List<OptionQuote> CreateExtremeDelataQuotes(double spot, DateTime expiry)
    {
        // All deltas outside configured range
        return new List<OptionQuote>
        {
            new OptionQuote { Strike = spot - 10, Right = Right.Put, Expiry = expiry, 
                Bid = 0.01, Ask = 0.02, Mid = 0.015, Delta = -0.01, IV = 0.15 },
            new OptionQuote { Strike = spot - 0.5, Right = Right.Put, Expiry = expiry, 
                Bid = 4.00, Ask = 4.10, Mid = 4.05, Delta = -0.95, IV = 0.30 }
        };
    }
}