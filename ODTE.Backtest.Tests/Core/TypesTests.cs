using FluentAssertions;
using ODTE.Backtest.Core;
using Xunit;

namespace ODTE.Backtest.Tests.Core;

/// <summary>
/// Comprehensive tests for core data types and records.
/// Ensures immutability, equality, and business logic correctness.
/// </summary>
public class TypesTests
{
    [Fact]
    public void Bar_Constructor_ShouldCreateValidBar()
    {
        // Arrange
        var timestamp = new DateTime(2024, 2, 1, 10, 30, 0);
        var open = 100.0;
        var high = 102.0;
        var low = 99.0;
        var close = 101.5;
        var volume = 10000.0;

        // Act
        var bar = new Bar(timestamp, open, high, low, close, volume);

        // Assert
        bar.Ts.Should().Be(timestamp);
        bar.O.Should().Be(open);
        bar.H.Should().Be(high);
        bar.L.Should().Be(low);
        bar.C.Should().Be(close);
        bar.V.Should().Be(volume);
    }

    [Fact]
    public void Bar_Equality_ShouldWorkCorrectly()
    {
        // Arrange
        var timestamp = new DateTime(2024, 2, 1, 10, 30, 0);
        var bar1 = new Bar(timestamp, 100, 102, 99, 101.5, 10000);
        var bar2 = new Bar(timestamp, 100, 102, 99, 101.5, 10000);
        var bar3 = new Bar(timestamp, 100, 102, 99, 101.0, 10000); // Different close

        // Act & Assert
        bar1.Should().Be(bar2);
        bar1.Should().NotBe(bar3);
        bar1.GetHashCode().Should().Be(bar2.GetHashCode());
    }

    [Theory]
    [InlineData(100.0, 102.0, 99.0, 101.5)] // Valid bar
    [InlineData(50.0, 50.0, 50.0, 50.0)]    // All same prices
    public void Bar_ValidPrices_ShouldBeAccepted(double open, double high, double low, double close)
    {
        // Arrange & Act
        var bar = new Bar(DateTime.Now, open, high, low, close, 1000);

        // Assert
        bar.H.Should().BeGreaterThanOrEqualTo(Math.Max(open, close));
        bar.L.Should().BeLessThanOrEqualTo(Math.Min(open, close));
    }

    [Fact]
    public void EconEvent_Constructor_ShouldCreateValidEvent()
    {
        // Arrange
        var timestamp = new DateTime(2024, 2, 1, 14, 0, 0);
        var kind = "FOMC";

        // Act
        var econEvent = new EconEvent(timestamp, kind);

        // Assert
        econEvent.Ts.Should().Be(timestamp);
        econEvent.Kind.Should().Be(kind);
    }

    [Fact]
    public void OptionQuote_Constructor_ShouldCreateValidQuote()
    {
        // Arrange
        var timestamp = new DateTime(2024, 2, 1, 10, 30, 0);
        var expiry = DateOnly.FromDateTime(new DateTime(2024, 2, 1));
        var strike = 100.0;
        var right = Right.Call;
        var bid = 1.50;
        var ask = 1.60;
        var mid = 1.55;
        var delta = 0.50;
        var iv = 0.20;

        // Act
        var quote = new OptionQuote(timestamp, expiry, strike, right, bid, ask, mid, delta, iv);

        // Assert
        quote.Ts.Should().Be(timestamp);
        quote.Expiry.Should().Be(expiry);
        quote.Strike.Should().Be(strike);
        quote.Right.Should().Be(right);
        quote.Bid.Should().Be(bid);
        quote.Ask.Should().Be(ask);
        quote.Mid.Should().Be(mid);
        quote.Delta.Should().Be(delta);
        quote.Iv.Should().Be(iv);
        quote.IV.Should().Be(iv); // Backward compatibility property
    }

    [Theory]
    [InlineData(Right.Call, 0.10, 0.15, 0.125)] // Call with positive delta
    [InlineData(Right.Put, -0.15, -0.10, -0.125)] // Put with negative delta
    public void OptionQuote_BidAskMid_ShouldBeConsistent(Right right, double bid, double ask, double expectedMid)
    {
        // Arrange
        var expiry = DateOnly.FromDateTime(DateTime.Today);
        var quote = new OptionQuote(DateTime.Now, expiry, 100, right, bid, ask, expectedMid, 0.15, 0.20);

        // Act & Assert
        quote.Bid.Should().BeLessThanOrEqualTo(quote.Ask);
        quote.Mid.Should().BeApproximately((bid + ask) / 2, 0.001);
    }

    [Fact]
    public void SpreadLeg_Constructor_ShouldCreateValidLeg()
    {
        // Arrange
        var expiry = DateOnly.FromDateTime(DateTime.Today);
        var strike = 100.0;
        var right = Right.Put;
        var ratio = -1; // Short

        // Act
        var leg = new SpreadLeg(expiry, strike, right, ratio);

        // Assert
        leg.Expiry.Should().Be(expiry);
        leg.Strike.Should().Be(strike);
        leg.Right.Should().Be(right);
        leg.Ratio.Should().Be(ratio);
    }

    [Theory]
    [InlineData(1, "Long")]
    [InlineData(-1, "Short")]
    [InlineData(2, "Long (2x)")]
    public void SpreadLeg_Ratio_ShouldIndicatePosition(int ratio, string expectedDescription)
    {
        // Arrange
        var leg = new SpreadLeg(DateOnly.FromDateTime(DateTime.Today), 100, Right.Call, ratio);

        // Act
        var isLong = ratio > 0;
        var isShort = ratio < 0;

        // Assert
        if (expectedDescription.Contains("Long"))
            isLong.Should().BeTrue();
        if (expectedDescription.Contains("Short"))
            isShort.Should().BeTrue();
    }

    [Fact]
    public void SpreadOrder_Constructor_ShouldCreateValidOrder()
    {
        // Arrange
        var timestamp = new DateTime(2024, 2, 1, 10, 30, 0);
        var underlying = "XSP";
        var credit = 0.25;
        var width = 1.0;
        var creditPerWidth = 0.25;
        var type = Decision.SingleSidePut;
        var shortLeg = new SpreadLeg(DateOnly.FromDateTime(DateTime.Today), 99, Right.Put, -1);
        var longLeg = new SpreadLeg(DateOnly.FromDateTime(DateTime.Today), 98, Right.Put, 1);

        // Act
        var order = new SpreadOrder(timestamp, underlying, credit, width, creditPerWidth, type, shortLeg, longLeg);

        // Assert
        order.Ts.Should().Be(timestamp);
        order.Underlying.Should().Be(underlying);
        order.Credit.Should().Be(credit);
        order.Width.Should().Be(width);
        order.CreditPerWidth.Should().Be(creditPerWidth);
        order.Type.Should().Be(type);
        order.Short.Should().Be(shortLeg);
        order.Long.Should().Be(longLeg);
        order.NetCredit.Should().Be(credit); // Compatibility property
    }

    [Fact]
    public void SpreadOrder_PositionType_ShouldMapFromDecision()
    {
        // Arrange
        var timestamp = DateTime.Now;
        var shortLeg = new SpreadLeg(DateOnly.FromDateTime(DateTime.Today), 99, Right.Put, -1);
        var longLeg = new SpreadLeg(DateOnly.FromDateTime(DateTime.Today), 98, Right.Put, 1);

        var condorOrder = new SpreadOrder(timestamp, "XSP", 0.25, 1.0, 0.25, Decision.Condor, shortLeg, longLeg);
        var putOrder = new SpreadOrder(timestamp, "XSP", 0.25, 1.0, 0.25, Decision.SingleSidePut, shortLeg, longLeg);
        var callOrder = new SpreadOrder(timestamp, "XSP", 0.25, 1.0, 0.25, Decision.SingleSideCall, shortLeg, longLeg);
        var noGoOrder = new SpreadOrder(timestamp, "XSP", 0.25, 1.0, 0.25, Decision.NoGo, shortLeg, longLeg);

        // Act & Assert
        condorOrder.PositionType.Should().Be(PositionType.IronCondor);
        putOrder.PositionType.Should().Be(PositionType.PutSpread);
        callOrder.PositionType.Should().Be(PositionType.CallSpread);
        noGoOrder.PositionType.Should().Be(PositionType.Other);
    }

    [Fact]
    public void SpreadOrder_IronCondor_ShouldHaveCallSpreadLegs()
    {
        // Arrange
        var timestamp = DateTime.Now;
        var putShort = new SpreadLeg(DateOnly.FromDateTime(DateTime.Today), 99, Right.Put, -1);
        var putLong = new SpreadLeg(DateOnly.FromDateTime(DateTime.Today), 98, Right.Put, 1);
        var callShort = new SpreadLeg(DateOnly.FromDateTime(DateTime.Today), 101, Right.Call, -1);
        var callLong = new SpreadLeg(DateOnly.FromDateTime(DateTime.Today), 102, Right.Call, 1);

        // Act
        var order = new SpreadOrder(timestamp, "XSP", 0.25, 1.0, 0.25, Decision.Condor, putShort, putLong)
        {
            Short2 = callShort,
            Long2 = callLong
        };

        // Assert
        order.Short2.Should().NotBeNull();
        order.Long2.Should().NotBeNull();
        order.Short2!.Right.Should().Be(Right.Call);
        order.Long2!.Right.Should().Be(Right.Call);
        order.Short2.Ratio.Should().Be(-1);
        order.Long2.Ratio.Should().Be(1);
    }

    [Fact]
    public void OpenPosition_Constructor_ShouldCreateValidPosition()
    {
        // Arrange
        var order = CreateTestSpreadOrder();
        var entryPrice = 0.20;
        var entryTs = new DateTime(2024, 2, 1, 10, 30, 0);

        // Act
        var position = new OpenPosition(order, entryPrice, entryTs);

        // Assert
        position.Order.Should().Be(order);
        position.EntryPrice.Should().Be(entryPrice);
        position.EntryTs.Should().Be(entryTs);
        position.Closed.Should().BeFalse();
        position.ExitPrice.Should().BeNull();
        position.ExitTs.Should().BeNull();
        position.ExitReason.Should().BeEmpty();
    }

    [Fact]
    public void OpenPosition_Close_ShouldUpdateExitFields()
    {
        // Arrange
        var position = new OpenPosition(CreateTestSpreadOrder(), 0.20, DateTime.Now);
        var exitPrice = 0.10;
        var exitTs = DateTime.Now.AddHours(1);
        var exitReason = "Target";

        // Act
        position.Closed = true;
        position.ExitPrice = exitPrice;
        position.ExitTs = exitTs;
        position.ExitReason = exitReason;

        // Assert
        position.Closed.Should().BeTrue();
        position.ExitPrice.Should().Be(exitPrice);
        position.ExitTs.Should().Be(exitTs);
        position.ExitReason.Should().Be(exitReason);
    }

    [Fact]
    public void TradeResult_Constructor_ShouldCreateValidResult()
    {
        // Arrange
        var position = new OpenPosition(CreateTestSpreadOrder(), 0.20, DateTime.Now)
        {
            Closed = true,
            ExitPrice = 0.10,
            ExitTs = DateTime.Now.AddHours(1),
            ExitReason = "Target"
        };
        var pnl = 50.0;
        var fees = 3.60;
        var mae = -10.0;
        var mfe = 60.0;

        // Act
        var result = new TradeResult(position, pnl, fees, mae, mfe);

        // Assert
        result.Pos.Should().Be(position);
        result.PnL.Should().Be(pnl);
        result.Fees.Should().Be(fees);
        result.MaxAdverseExcursion.Should().Be(mae);
        result.MaxFavorableExcursion.Should().Be(mfe);
        result.Position.Should().Be(position); // Compatibility property
        result.ExitReason.Should().Be("Target"); // Compatibility property
    }

    [Fact]
    public void RunReport_Properties_ShouldCalculateCorrectly()
    {
        // Arrange
        var report = new RunReport();

        // Add some test trades
        var position1 = new OpenPosition(CreateTestSpreadOrder(), 0.20, DateTime.Now)
        {
            Closed = true,
            ExitReason = "Target"
        };
        var position2 = new OpenPosition(CreateTestSpreadOrder(), 0.25, DateTime.Now)
        {
            Closed = true,
            ExitReason = "Stop"
        };

        var trade1 = new TradeResult(position1, 50.0, 3.60, -5.0, 50.0);  // Winner
        var trade2 = new TradeResult(position2, -30.0, 3.60, -30.0, 10.0); // Loser

        // Act
        report.Trades.Add(trade1);
        report.Trades.Add(trade2);

        // Assert
        report.GrossPnL.Should().Be(20.0); // 50 - 30
        report.Fees.Should().Be(7.20); // 3.60 + 3.60
        report.NetPnL.Should().Be(12.8); // 20 - 7.20
        report.WinCount.Should().Be(1);
        report.LossCount.Should().Be(1);
        report.WinRate.Should().Be(0.5);
        report.AvgWin.Should().Be(50.0);
        report.AvgLoss.Should().Be(-30.0);
    }

    [Theory]
    [InlineData(Decision.NoGo)]
    [InlineData(Decision.Condor)]
    [InlineData(Decision.SingleSidePut)]
    [InlineData(Decision.SingleSideCall)]
    public void Decision_AllValues_ShouldBeValid(Decision decision)
    {
        // Act & Assert
        Enum.IsDefined(typeof(Decision), decision).Should().BeTrue();
    }

    [Theory]
    [InlineData(PositionType.IronCondor)]
    [InlineData(PositionType.PutSpread)]
    [InlineData(PositionType.CallSpread)]
    [InlineData(PositionType.Other)]
    public void PositionType_AllValues_ShouldBeValid(PositionType positionType)
    {
        // Act & Assert
        Enum.IsDefined(typeof(PositionType), positionType).Should().BeTrue();
    }

    [Theory]
    [InlineData(Right.Call)]
    [InlineData(Right.Put)]
    public void Right_AllValues_ShouldBeValid(Right right)
    {
        // Act & Assert
        Enum.IsDefined(typeof(Right), right).Should().BeTrue();
    }

    private static SpreadOrder CreateTestSpreadOrder()
    {
        var shortLeg = new SpreadLeg(DateOnly.FromDateTime(DateTime.Today), 99, Right.Put, -1);
        var longLeg = new SpreadLeg(DateOnly.FromDateTime(DateTime.Today), 98, Right.Put, 1);
        return new SpreadOrder(DateTime.Now, "XSP", 0.25, 1.0, 0.25, Decision.SingleSidePut, shortLeg, longLeg);
    }
}