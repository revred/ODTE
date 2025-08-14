using FluentAssertions;
using Moq;
using ODTE.Backtest.Config;
using ODTE.Backtest.Core;
using ODTE.Backtest.Data;
using ODTE.Backtest.Strategy;
using Xunit;

namespace ODTE.Backtest.Tests.Strategy;

/// <summary>
/// Comprehensive tests for SpreadBuilder option spread construction logic.
/// Tests single-sided spreads, iron condors, and quality filters.
/// </summary>
public class SpreadBuilderTests
{
    private readonly Mock<IMarketData> _mockMarketData;
    private readonly Mock<IOptionsData> _mockOptionsData;
    private readonly SimConfig _config;
    private readonly SpreadBuilder _spreadBuilder;
    private readonly DateTime _testTime = new(2024, 2, 1, 10, 0, 0);
    private readonly DateOnly _testExpiry = DateOnly.FromDateTime(new DateTime(2024, 2, 1));

    public SpreadBuilderTests()
    {
        _mockMarketData = new Mock<IMarketData>();
        _mockOptionsData = new Mock<IOptionsData>();
        
        _config = new SimConfig
        {
            Underlying = "XSP",
            ShortDelta = new ShortDeltaCfg
            {
                CondorMin = 0.07,
                CondorMax = 0.15,
                SingleMin = 0.10,
                SingleMax = 0.20
            },
            WidthPoints = new WidthPointsCfg
            {
                Min = 1,
                Max = 2
            },
            CreditPerWidthMin = new CreditPerWidthCfg
            {
                Condor = 0.15, // More forgiving for tests
                Single = 0.15  // More forgiving for tests
            },
            Slippage = new SlippageCfg
            {
                SpreadPctCap = 0.25
            }
        };
        
        _spreadBuilder = new SpreadBuilder(_config);
    }

    [Fact]
    public void Constructor_ValidConfig_ShouldCreateSpreadBuilder()
    {
        // Arrange & Act
        var builder = new SpreadBuilder(_config);

        // Assert
        builder.Should().NotBeNull();
    }

    [Fact]
    public void TryBuild_NoQuotesAvailable_ShouldReturnNull()
    {
        // Arrange
        _mockOptionsData.Setup(x => x.TodayExpiry(_testTime)).Returns(_testExpiry);
        _mockOptionsData.Setup(x => x.GetQuotesAt(_testTime)).Returns(new List<OptionQuote>());

        // Act
        var result = _spreadBuilder.TryBuild(_testTime, Decision.Condor, _mockMarketData.Object, _mockOptionsData.Object);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void TryBuild_InvalidSpotPrice_ShouldReturnNull()
    {
        // Arrange
        var quotes = CreateTestQuotes();
        _mockOptionsData.Setup(x => x.TodayExpiry(_testTime)).Returns(_testExpiry);
        _mockOptionsData.Setup(x => x.GetQuotesAt(_testTime)).Returns(quotes);
        _mockMarketData.Setup(x => x.GetSpot(_testTime)).Returns(0); // Invalid spot

        // Act
        var result = _spreadBuilder.TryBuild(_testTime, Decision.SingleSidePut, _mockMarketData.Object, _mockOptionsData.Object);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(Decision.SingleSidePut)]
    [InlineData(Decision.SingleSideCall)]
    [InlineData(Decision.Condor)]
    public void TryBuild_ValidConditions_ShouldReturnSpreadOrder(Decision decision)
    {
        // Arrange
        var quotes = CreateTestQuotes();
        _mockOptionsData.Setup(x => x.TodayExpiry(_testTime)).Returns(_testExpiry);
        _mockOptionsData.Setup(x => x.GetQuotesAt(_testTime)).Returns(quotes);
        _mockMarketData.Setup(x => x.GetSpot(_testTime)).Returns(100.0);

        // Act
        var result = _spreadBuilder.TryBuild(_testTime, decision, _mockMarketData.Object, _mockOptionsData.Object);

        // Assert
        result.Should().NotBeNull();
        result!.Ts.Should().Be(_testTime);
        result.Underlying.Should().Be("XSP");
        result.Type.Should().Be(decision);
        result.NetCredit.Should().BeGreaterThan(0);
        result.Width.Should().BeGreaterThan(0);
        result.CreditPerWidth.Should().BeGreaterThan(0);
    }

    [Fact]
    public void TryBuild_SingleSidePut_ShouldCreatePutSpread()
    {
        // Arrange
        var quotes = CreateTestQuotes();
        _mockOptionsData.Setup(x => x.TodayExpiry(_testTime)).Returns(_testExpiry);
        _mockOptionsData.Setup(x => x.GetQuotesAt(_testTime)).Returns(quotes);
        _mockMarketData.Setup(x => x.GetSpot(_testTime)).Returns(100.0);

        // Act
        var result = _spreadBuilder.TryBuild(_testTime, Decision.SingleSidePut, _mockMarketData.Object, _mockOptionsData.Object);

        // Assert
        result.Should().NotBeNull();
        result!.Short.Right.Should().Be(Right.Put);
        result.Long.Right.Should().Be(Right.Put);
        result.Short.Ratio.Should().Be(-1); // Sell short leg
        result.Long.Ratio.Should().Be(1);   // Buy long leg
        result.Short.Strike.Should().BeGreaterThan(result.Long.Strike); // Put spread: short higher, long lower
    }

    [Fact]
    public void TryBuild_SingleSideCall_ShouldCreateCallSpread()
    {
        // Arrange
        var quotes = CreateTestQuotes();
        _mockOptionsData.Setup(x => x.TodayExpiry(_testTime)).Returns(_testExpiry);
        _mockOptionsData.Setup(x => x.GetQuotesAt(_testTime)).Returns(quotes);
        _mockMarketData.Setup(x => x.GetSpot(_testTime)).Returns(100.0);

        // Act
        var result = _spreadBuilder.TryBuild(_testTime, Decision.SingleSideCall, _mockMarketData.Object, _mockOptionsData.Object);

        // Assert
        result.Should().NotBeNull();
        result!.Short.Right.Should().Be(Right.Call);
        result.Long.Right.Should().Be(Right.Call);
        result.Short.Ratio.Should().Be(-1); // Sell short leg
        result.Long.Ratio.Should().Be(1);   // Buy long leg
        result.Long.Strike.Should().BeGreaterThan(result.Short.Strike); // Call spread: long higher, short lower
    }

    [Fact]
    public void TryBuild_Condor_ShouldCreateIronCondor()
    {
        // Arrange
        var quotes = CreateTestQuotes();
        _mockOptionsData.Setup(x => x.TodayExpiry(_testTime)).Returns(_testExpiry);
        _mockOptionsData.Setup(x => x.GetQuotesAt(_testTime)).Returns(quotes);
        _mockMarketData.Setup(x => x.GetSpot(_testTime)).Returns(100.0);

        // Act
        var result = _spreadBuilder.TryBuild(_testTime, Decision.Condor, _mockMarketData.Object, _mockOptionsData.Object);

        // Assert
        result.Should().NotBeNull();
        result!.Type.Should().Be(Decision.Condor);
        result.Short.Right.Should().Be(Right.Put); // Simplified condor returns put spread
        result.Long.Right.Should().Be(Right.Put);
        result.Width.Should().Be(_config.WidthPoints.Min);
    }

    [Fact]
    public void TryBuild_InsufficientCredit_ShouldReturnNull()
    {
        // Arrange - Create quotes with very low credit
        var quotes = CreateLowCreditQuotes();
        _mockOptionsData.Setup(x => x.TodayExpiry(_testTime)).Returns(_testExpiry);
        _mockOptionsData.Setup(x => x.GetQuotesAt(_testTime)).Returns(quotes);
        _mockMarketData.Setup(x => x.GetSpot(_testTime)).Returns(100.0);

        // Act
        var result = _spreadBuilder.TryBuild(_testTime, Decision.SingleSidePut, _mockMarketData.Object, _mockOptionsData.Object);

        // Assert
        result.Should().BeNull(); // Insufficient credit/width ratio
    }

    [Fact]
    public void TryBuild_IlliquidOptions_ShouldReturnNull()
    {
        // Arrange - Create quotes with wide bid-ask spreads
        var quotes = CreateIlliquidQuotes();
        _mockOptionsData.Setup(x => x.TodayExpiry(_testTime)).Returns(_testExpiry);
        _mockOptionsData.Setup(x => x.GetQuotesAt(_testTime)).Returns(quotes);
        _mockMarketData.Setup(x => x.GetSpot(_testTime)).Returns(100.0);

        // Act
        var result = _spreadBuilder.TryBuild(_testTime, Decision.SingleSideCall, _mockMarketData.Object, _mockOptionsData.Object);

        // Assert
        result.Should().BeNull(); // Too illiquid
    }

    [Fact]
    public void TryBuild_NoStrikesInDeltaRange_ShouldReturnNull()
    {
        // Arrange - Create quotes outside delta criteria
        var quotes = CreateOutOfRangeQuotes();
        _mockOptionsData.Setup(x => x.TodayExpiry(_testTime)).Returns(_testExpiry);
        _mockOptionsData.Setup(x => x.GetQuotesAt(_testTime)).Returns(quotes);
        _mockMarketData.Setup(x => x.GetSpot(_testTime)).Returns(100.0);

        // Act
        var result = _spreadBuilder.TryBuild(_testTime, Decision.SingleSidePut, _mockMarketData.Object, _mockOptionsData.Object);

        // Assert
        result.Should().BeNull(); // No strikes in delta range
    }

    [Theory]
    [InlineData(0.05, 0.30, 0.15, 0.25)] // Valid ranges
    [InlineData(0.10, 0.20, 0.15, 0.25)] // Tighter ranges
    public void TryBuild_DifferentDeltaConfigurations_ShouldRespectConfig(
        double condorMin, double condorMax, double singleMin, double singleMax)
    {
        // Arrange
        _config.ShortDelta.CondorMin = condorMin;
        _config.ShortDelta.CondorMax = condorMax;
        _config.ShortDelta.SingleMin = singleMin;
        _config.ShortDelta.SingleMax = singleMax;

        var quotes = CreateTestQuotes();
        _mockOptionsData.Setup(x => x.TodayExpiry(_testTime)).Returns(_testExpiry);
        _mockOptionsData.Setup(x => x.GetQuotesAt(_testTime)).Returns(quotes);
        _mockMarketData.Setup(x => x.GetSpot(_testTime)).Returns(100.0);

        // Act
        var putSpread = _spreadBuilder.TryBuild(_testTime, Decision.SingleSidePut, _mockMarketData.Object, _mockOptionsData.Object);
        var condor = _spreadBuilder.TryBuild(_testTime, Decision.Condor, _mockMarketData.Object, _mockOptionsData.Object);

        // Assert - Should respect different delta criteria
        if (putSpread != null)
        {
            // Short delta should be within single range for put spread
            var shortDelta = Math.Abs(GetDeltaForStrike(quotes, putSpread.Short.Strike, putSpread.Short.Right));
            shortDelta.Should().BeInRange(singleMin, singleMax);
        }

        if (condor != null)
        {
            // Short delta should be within condor range
            var shortDelta = Math.Abs(GetDeltaForStrike(quotes, condor.Short.Strike, condor.Short.Right));
            shortDelta.Should().BeInRange(condorMin, condorMax);
        }
    }

    [Theory]
    [InlineData(1, 3)] // Min width 1
    [InlineData(2, 5)] // Min width 2
    public void TryBuild_DifferentWidthConfigurations_ShouldRespectConfig(int minWidth, int maxWidth)
    {
        // Arrange
        _config.WidthPoints.Min = minWidth;
        _config.WidthPoints.Max = maxWidth;

        var quotes = CreateTestQuotes();
        _mockOptionsData.Setup(x => x.TodayExpiry(_testTime)).Returns(_testExpiry);
        _mockOptionsData.Setup(x => x.GetQuotesAt(_testTime)).Returns(quotes);
        _mockMarketData.Setup(x => x.GetSpot(_testTime)).Returns(100.0);

        // Act
        var result = _spreadBuilder.TryBuild(_testTime, Decision.SingleSidePut, _mockMarketData.Object, _mockOptionsData.Object);

        // Assert
        if (result != null)
        {
            result.Width.Should().BeGreaterThanOrEqualTo(minWidth);
            result.Width.Should().BeLessThanOrEqualTo(maxWidth);
        }
    }

    [Fact]
    public void TryBuild_QualityMetrics_ShouldMeetCriteria()
    {
        // Arrange
        var quotes = CreateTestQuotes();
        _mockOptionsData.Setup(x => x.TodayExpiry(_testTime)).Returns(_testExpiry);
        _mockOptionsData.Setup(x => x.GetQuotesAt(_testTime)).Returns(quotes);
        _mockMarketData.Setup(x => x.GetSpot(_testTime)).Returns(100.0);

        // Act
        var result = _spreadBuilder.TryBuild(_testTime, Decision.SingleSidePut, _mockMarketData.Object, _mockOptionsData.Object);

        // Assert
        result.Should().NotBeNull();
        result!.CreditPerWidth.Should().BeGreaterThanOrEqualTo(_config.CreditPerWidthMin.Single);
        result.NetCredit.Should().BeGreaterThan(0);
        result.Width.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData(Decision.NoGo)]
    [InlineData((Decision)99)] // Invalid enum value
    public void TryBuild_InvalidDecision_ShouldReturnNull(Decision decision)
    {
        // Arrange
        var quotes = CreateTestQuotes();
        _mockOptionsData.Setup(x => x.TodayExpiry(_testTime)).Returns(_testExpiry);
        _mockOptionsData.Setup(x => x.GetQuotesAt(_testTime)).Returns(quotes);
        _mockMarketData.Setup(x => x.GetSpot(_testTime)).Returns(100.0);

        // Act
        var result = _spreadBuilder.TryBuild(_testTime, decision, _mockMarketData.Object, _mockOptionsData.Object);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void TryBuild_OnlyCallQuotes_CondorShouldReturnNull()
    {
        // Arrange - Only call quotes available
        var quotes = CreateTestQuotes().Where(q => q.Right == Right.Call).ToList();
        _mockOptionsData.Setup(x => x.TodayExpiry(_testTime)).Returns(_testExpiry);
        _mockOptionsData.Setup(x => x.GetQuotesAt(_testTime)).Returns(quotes);
        _mockMarketData.Setup(x => x.GetSpot(_testTime)).Returns(100.0);

        // Act
        var result = _spreadBuilder.TryBuild(_testTime, Decision.Condor, _mockMarketData.Object, _mockOptionsData.Object);

        // Assert
        result.Should().BeNull(); // Condor needs both puts and calls
    }

    [Fact]
    public void TryBuild_SpreadOrderProperties_ShouldBeCorrect()
    {
        // Arrange
        var quotes = CreateTestQuotes();
        _mockOptionsData.Setup(x => x.TodayExpiry(_testTime)).Returns(_testExpiry);
        _mockOptionsData.Setup(x => x.GetQuotesAt(_testTime)).Returns(quotes);
        _mockMarketData.Setup(x => x.GetSpot(_testTime)).Returns(100.0);

        // Act
        var result = _spreadBuilder.TryBuild(_testTime, Decision.SingleSidePut, _mockMarketData.Object, _mockOptionsData.Object);

        // Assert
        result.Should().NotBeNull();
        result!.Ts.Should().Be(_testTime);
        result.Underlying.Should().Be(_config.Underlying);
        result.Short.Expiry.Should().Be(_testExpiry);
        result.Long.Expiry.Should().Be(_testExpiry);
        
        // SpreadOrder backward compatibility properties
        result.PositionType.Should().Be(PositionType.PutSpread); // Based on SingleSidePut decision
        result.NetCredit.Should().Be(result.Credit); // Backward compatibility
        result.Credit.Should().BeGreaterThan(0);
    }

    private List<OptionQuote> CreateTestQuotes()
    {
        var quotes = new List<OptionQuote>();

        // Create specific quotes that should work with our config
        // Put spread: sell 99 put (delta ~0.15), buy 98 put (delta ~0.12) 
        // Credit = short bid - long ask = 0.80 - 0.30 = 0.50, Width = 1.0, C/W = 0.50 > 0.15 ✓
        quotes.Add(new OptionQuote(_testTime, _testExpiry, 99.0, Right.Put, 0.80, 0.90, 0.85, -0.15, 0.20));
        quotes.Add(new OptionQuote(_testTime, _testExpiry, 98.0, Right.Put, 0.25, 0.30, 0.275, -0.12, 0.20));
        quotes.Add(new OptionQuote(_testTime, _testExpiry, 97.0, Right.Put, 0.15, 0.20, 0.175, -0.10, 0.20));

        // Call spread: sell 101 call (delta ~0.15), buy 102 call (delta ~0.12)
        // Credit = short bid - long ask = 0.80 - 0.30 = 0.50, Width = 1.0, C/W = 0.50 > 0.15 ✓
        quotes.Add(new OptionQuote(_testTime, _testExpiry, 101.0, Right.Call, 0.80, 0.90, 0.85, 0.15, 0.20));
        quotes.Add(new OptionQuote(_testTime, _testExpiry, 102.0, Right.Call, 0.25, 0.30, 0.275, 0.12, 0.20));
        quotes.Add(new OptionQuote(_testTime, _testExpiry, 103.0, Right.Call, 0.15, 0.20, 0.175, 0.10, 0.20));

        // Add more strikes for condor
        quotes.Add(new OptionQuote(_testTime, _testExpiry, 100.0, Right.Put, 1.00, 1.10, 1.05, -0.18, 0.20));
        quotes.Add(new OptionQuote(_testTime, _testExpiry, 100.0, Right.Call, 1.00, 1.10, 1.05, 0.18, 0.20));

        return quotes;
    }

    private List<OptionQuote> CreateLowCreditQuotes()
    {
        var quotes = new List<OptionQuote>();

        // Create quotes with very low credit potential
        for (double strike = 95.0; strike <= 105.0; strike += 0.5)
        {
            double delta = (100.0 - strike) * 0.02 + 0.10;
            quotes.Add(new OptionQuote(
                _testTime, _testExpiry, strike, Right.Put,
                0.05, 0.10, 0.075, -Math.Abs(delta), 0.20));
        }

        return quotes;
    }

    private List<OptionQuote> CreateIlliquidQuotes()
    {
        var quotes = new List<OptionQuote>();

        // Create quotes with very wide spreads
        for (double strike = 95.0; strike <= 105.0; strike += 0.5)
        {
            double delta = (100.0 - strike) * 0.02 + 0.10;
            quotes.Add(new OptionQuote(
                _testTime, _testExpiry, strike, Right.Put,
                1.00, 2.00, 1.50, -Math.Abs(delta), 0.20));
        }

        return quotes;
    }

    private List<OptionQuote> CreateOutOfRangeQuotes()
    {
        var quotes = new List<OptionQuote>();

        // Create quotes with deltas outside configured ranges
        for (double strike = 95.0; strike <= 105.0; strike += 0.5)
        {
            double delta = 0.05; // Too low for single range (0.10-0.20)
            quotes.Add(new OptionQuote(
                _testTime, _testExpiry, strike, Right.Put,
                1.20, 1.30, 1.25, -delta, 0.20));
        }

        return quotes;
    }

    private double GetDeltaForStrike(List<OptionQuote> quotes, double strike, Right right)
    {
        return quotes.FirstOrDefault(q => q.Strike == strike && q.Right == right)?.Delta ?? 0.0;
    }
}