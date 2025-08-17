using FluentAssertions;
using Moq;
using ODTE.Backtest.Config;
using ODTE.Backtest.Core;
using ODTE.Backtest.Data;
using ODTE.Backtest.Signals;
using Xunit;

namespace ODTE.Backtest.Tests.Signals;

/// <summary>
/// Comprehensive tests for RegimeScorer market condition analysis.
/// Tests opening range detection, VWAP persistence, volatility regimes, and scoring logic.
/// </summary>
public class RegimeScorerTests
{
    private readonly Mock<IMarketData> _mockMarketData;
    private readonly Mock<IEconCalendar> _mockCalendar;
    private readonly SimConfig _config;
    private readonly RegimeScorer _regimeScorer;
    private readonly DateTime _testTime = new(2024, 2, 1, 16, 0, 0, DateTimeKind.Utc); // 11:00 AM ET = 4:00 PM UTC

    public RegimeScorerTests()
    {
        _mockMarketData = new Mock<IMarketData>();
        _mockCalendar = new Mock<IEconCalendar>();

        _config = new SimConfig
        {
            Signals = new SignalsCfg
            {
                OrMinutes = 15,
                VwapWindowMinutes = 30,
                AtrPeriodBars = 20,
                EventBlockMinutesBefore = 60,
                EventBlockMinutesAfter = 15
            },
            NoNewRiskMinutesToClose = 40
        };

        _regimeScorer = new RegimeScorer(_config);
    }

    [Fact]
    public void Constructor_ValidConfig_ShouldCreateRegimeScorer()
    {
        // Arrange & Act
        var scorer = new RegimeScorer(_config);

        // Assert
        scorer.Should().NotBeNull();
    }

    [Fact]
    public void Score_NoData_ShouldReturnNeutralScore()
    {
        // Arrange
        SetupEmptyMarketData();
        _mockCalendar.Setup(x => x.NextEventAfter(_testTime)).Returns((EconEvent?)null);

        // Act
        var (score, calmRange, trendUp, trendDown) = _regimeScorer.Score(_testTime, _mockMarketData.Object, _mockCalendar.Object);

        // Assert
        score.Should().BeLessThanOrEqualTo(2); // Without data, score should be low
        calmRange.Should().BeFalse();
        trendUp.Should().BeFalse();
        trendDown.Should().BeFalse();
    }

    [Fact]
    public void Score_OpeningRangeBreakoutUp_ShouldDetectUpwardTrend()
    {
        // Arrange
        var bars = CreateTrendingUpBars();
        SetupMarketDataWithBars(bars);
        _mockMarketData.Setup(x => x.Vwap(_testTime, TimeSpan.FromMinutes(30))).Returns(100.5);
        _mockMarketData.Setup(x => x.Atr20Minutes(_testTime)).Returns(2.0);
        _mockCalendar.Setup(x => x.NextEventAfter(_testTime)).Returns((EconEvent?)null);

        // Act
        var (score, calmRange, trendUp, trendDown) = _regimeScorer.Score(_testTime, _mockMarketData.Object, _mockCalendar.Object);

        // Assert
        score.Should().BeGreaterThan(0); // Positive score for trend
        trendUp.Should().BeTrue();
        trendDown.Should().BeFalse();
        calmRange.Should().BeFalse();
    }

    [Fact]
    public void Score_OpeningRangeBreakoutDown_ShouldDetectDownwardTrend()
    {
        // Arrange
        var bars = CreateTrendingDownBars();
        SetupMarketDataWithBars(bars);
        _mockMarketData.Setup(x => x.Vwap(_testTime, TimeSpan.FromMinutes(30))).Returns(99.5);
        _mockMarketData.Setup(x => x.Atr20Minutes(_testTime)).Returns(2.0);
        _mockCalendar.Setup(x => x.NextEventAfter(_testTime)).Returns((EconEvent?)null);

        // Act
        var (score, calmRange, trendUp, trendDown) = _regimeScorer.Score(_testTime, _mockMarketData.Object, _mockCalendar.Object);

        // Assert
        score.Should().BeGreaterThan(0); // Positive score for trend
        trendUp.Should().BeFalse();
        trendDown.Should().BeTrue();
        calmRange.Should().BeFalse();
    }

    [Fact]
    public void Score_CalmRangeConditions_ShouldDetectRangeMarket()
    {
        // Arrange
        var bars = CreateRangeBoundBars();
        SetupMarketDataWithBars(bars);
        _mockMarketData.Setup(x => x.Vwap(_testTime, TimeSpan.FromMinutes(30))).Returns(100.0);
        _mockMarketData.Setup(x => x.Atr20Minutes(_testTime)).Returns(3.0); // ATR higher than range
        _mockCalendar.Setup(x => x.NextEventAfter(_testTime)).Returns((EconEvent?)null);

        // Act
        var (score, calmRange, trendUp, trendDown) = _regimeScorer.Score(_testTime, _mockMarketData.Object, _mockCalendar.Object);

        // Assert
        calmRange.Should().BeTrue();
        trendUp.Should().BeFalse();
        trendDown.Should().BeFalse();
        score.Should().BeGreaterThan(0); // Calm conditions get bonus points
    }

    [Fact]
    public void Score_VwapPersistenceHigh_ShouldIncreaseScore()
    {
        // Arrange
        var bars = CreateVwapPersistentBars();
        SetupMarketDataWithBars(bars);
        _mockMarketData.Setup(x => x.Vwap(_testTime, TimeSpan.FromMinutes(30))).Returns(99.5); // Below current price
        _mockMarketData.Setup(x => x.Atr20Minutes(_testTime)).Returns(2.0);
        _mockCalendar.Setup(x => x.NextEventAfter(_testTime)).Returns((EconEvent?)null);

        // Act
        var (score, _, _, _) = _regimeScorer.Score(_testTime, _mockMarketData.Object, _mockCalendar.Object);

        // Assert
        score.Should().BeGreaterThan(1); // VWAP persistence should add points
    }

    [Fact]
    public void Score_EventProximity_ShouldReduceScore()
    {
        // Arrange
        var bars = CreateTrendingUpBars();
        SetupMarketDataWithBars(bars);
        _mockMarketData.Setup(x => x.Vwap(_testTime, TimeSpan.FromMinutes(30))).Returns(100.5);
        _mockMarketData.Setup(x => x.Atr20Minutes(_testTime)).Returns(2.0);

        // Event in 30 minutes (within EventBlockMinutesBefore = 60)
        var upcomingEvent = new EconEvent(_testTime.AddMinutes(30), "FOMC");
        _mockCalendar.Setup(x => x.NextEventAfter(_testTime)).Returns(upcomingEvent);

        // Act
        var (score, _, _, _) = _regimeScorer.Score(_testTime, _mockMarketData.Object, _mockCalendar.Object);

        // Assert
        score.Should().BeLessThan(2); // Event proximity should reduce score
    }

    [Fact]
    public void Score_GammaHour_ShouldSignificantlyReduceScore()
    {
        // Arrange - 30 minutes to close (within NoNewRiskMinutesToClose = 40)
        var lateTime = new DateTime(2024, 2, 1, 15, 30, 0); // 3:30 PM, close at 4:00 PM

        var bars = CreateTrendingUpBars();
        SetupMarketDataWithBars(bars);
        _mockMarketData.Setup(x => x.Vwap(lateTime, TimeSpan.FromMinutes(30))).Returns(100.5);
        _mockMarketData.Setup(x => x.Atr20Minutes(lateTime)).Returns(2.0);
        _mockCalendar.Setup(x => x.NextEventAfter(lateTime)).Returns((EconEvent?)null);

        // Act
        var (score, _, _, _) = _regimeScorer.Score(lateTime, _mockMarketData.Object, _mockCalendar.Object);

        // Assert
        score.Should().BeLessThan(0); // Gamma hour should heavily penalize score
    }

    [Theory]
    [InlineData(0.5)] // Range 50% of ATR (calm)
    [InlineData(0.8)] // Range 80% of ATR (calm threshold)
    [InlineData(1.2)] // Range 120% of ATR (expansion)
    public void Score_DifferentVolatilityRegimes_ShouldAdjustScore(double rangeToAtrRatio)
    {
        // Arrange
        var atr = 2.0;
        var dailyRange = atr * rangeToAtrRatio;
        var bars = CreateBarsWithRange(dailyRange);

        SetupMarketDataWithBars(bars);
        _mockMarketData.Setup(x => x.Vwap(_testTime, TimeSpan.FromMinutes(30))).Returns(100.0);
        _mockMarketData.Setup(x => x.Atr20Minutes(_testTime)).Returns(atr);
        _mockCalendar.Setup(x => x.NextEventAfter(_testTime)).Returns((EconEvent?)null);

        // Act
        var (score, calmRange, _, _) = _regimeScorer.Score(_testTime, _mockMarketData.Object, _mockCalendar.Object);

        // Assert
        if (rangeToAtrRatio <= 0.8)
        {
            score.Should().BeGreaterThan(1); // Calm conditions get bonus
            calmRange.Should().BeTrue();
        }
        else if (rangeToAtrRatio >= 1.0)
        {
            score.Should().BeGreaterThan(1); // Expansion also gets bonus
        }
    }

    [Fact]
    public void Score_SignalAlignment_ShouldProvideBonus()
    {
        // Arrange - OR breaks up and VWAP slope is also up
        var bars = CreateAlignedSignalsBars(); // OR break up + VWAP slope up
        SetupMarketDataWithBars(bars);
        _mockMarketData.Setup(x => x.Vwap(_testTime, TimeSpan.FromMinutes(30))).Returns(100.2);
        _mockMarketData.Setup(x => x.Atr20Minutes(_testTime)).Returns(2.0);
        _mockCalendar.Setup(x => x.NextEventAfter(_testTime)).Returns((EconEvent?)null);

        // Act
        var (score, _, _, _) = _regimeScorer.Score(_testTime, _mockMarketData.Object, _mockCalendar.Object);

        // Assert
        score.Should().BeGreaterThan(3); // Should get alignment bonus
    }

    [Fact]
    public void Score_MultipleNegativeFactors_ShouldCompoundReduction()
    {
        // Arrange - Event proximity AND gamma hour
        var lateTime = new DateTime(2024, 2, 1, 15, 30, 0);
        var upcomingEvent = new EconEvent(lateTime.AddMinutes(30), "NFP");

        var bars = CreateTrendingUpBars();
        SetupMarketDataWithBars(bars);
        _mockMarketData.Setup(x => x.Vwap(lateTime, TimeSpan.FromMinutes(30))).Returns(100.5);
        _mockMarketData.Setup(x => x.Atr20Minutes(lateTime)).Returns(2.0);
        _mockCalendar.Setup(x => x.NextEventAfter(lateTime)).Returns(upcomingEvent);

        // Act
        var (score, _, _, _) = _regimeScorer.Score(lateTime, _mockMarketData.Object, _mockCalendar.Object);

        // Assert
        score.Should().BeLessThan(-2); // Multiple negative factors should compound
    }

    [Theory]
    [InlineData(0.3, false)] // 30% above VWAP - not persistent enough
    [InlineData(0.5, false)] // 50% above VWAP - borderline
    [InlineData(0.7, true)]  // 70% above VWAP - persistent trend up
    [InlineData(0.9, true)]  // 90% above VWAP - strong trend up
    public void Score_VwapPersistenceLevels_ShouldAffectTrendDetection(double aboveVwapRatio, bool expectedTrend)
    {
        // Arrange
        var bars = CreateVwapBarsWithRatio(aboveVwapRatio);
        SetupMarketDataWithBars(bars);
        _mockMarketData.Setup(x => x.Vwap(_testTime, TimeSpan.FromMinutes(30))).Returns(99.8);
        _mockMarketData.Setup(x => x.Atr20Minutes(_testTime)).Returns(2.0);
        _mockCalendar.Setup(x => x.NextEventAfter(_testTime)).Returns((EconEvent?)null);

        // Act
        var (_, _, trendUp, trendDown) = _regimeScorer.Score(_testTime, _mockMarketData.Object, _mockCalendar.Object);

        // Assert
        if (expectedTrend)
        {
            (trendUp || trendDown).Should().BeTrue();
        }
        else
        {
            trendUp.Should().BeFalse();
            trendDown.Should().BeFalse();
        }
    }

    [Fact]
    public void Score_ScoreComponents_ShouldBeAdditive()
    {
        // Arrange - Create ideal conditions for maximum score
        var bars = CreateIdealConditionsBars(); // OR break + VWAP persistence + calm range
        SetupMarketDataWithBars(bars);
        _mockMarketData.Setup(x => x.Vwap(_testTime, TimeSpan.FromMinutes(30))).Returns(100.2);
        _mockMarketData.Setup(x => x.Atr20Minutes(_testTime)).Returns(4.0); // High ATR for calm range calculation
        _mockCalendar.Setup(x => x.NextEventAfter(_testTime)).Returns((EconEvent?)null);

        // Act
        var (score, _, _, _) = _regimeScorer.Score(_testTime, _mockMarketData.Object, _mockCalendar.Object);

        // Assert
        score.Should().BeGreaterThan(5); // Multiple positive factors should add up
    }

    private void SetupEmptyMarketData()
    {
        var emptyBars = new List<Bar>();
        _mockMarketData.Setup(x => x.GetBars(It.IsAny<DateOnly>(), It.IsAny<DateOnly>())).Returns(emptyBars);
        _mockMarketData.Setup(x => x.Vwap(_testTime, It.IsAny<TimeSpan>())).Returns(100.0);
        _mockMarketData.Setup(x => x.Atr20Minutes(_testTime)).Returns(2.0);
    }

    private void SetupMarketDataWithBars(List<Bar> bars)
    {
        _mockMarketData.Setup(x => x.GetBars(It.IsAny<DateOnly>(), It.IsAny<DateOnly>())).Returns(bars);
    }

    private List<Bar> CreateTrendingUpBars()
    {
        var bars = new List<Bar>();
        var sessionStart = new DateTime(2024, 2, 1, 14, 30, 0, DateTimeKind.Utc); // 9:30 AM ET = 2:30 PM UTC

        // Opening range: 99.5 - 100.5
        for (int i = 0; i < 3; i++) // First 15 minutes
        {
            var time = sessionStart.AddMinutes(i * 5);
            bars.Add(new Bar(time, 99.5 + i * 0.2, 100.0 + i * 0.2, 99.3 + i * 0.2, 99.8 + i * 0.2, 10000));
        }

        // Breakout higher and continuation
        for (int i = 3; i < 30; i++) // Rest of session until test time
        {
            var time = sessionStart.AddMinutes(i * 5);
            var price = 100.5 + (i - 3) * 0.05; // Trending higher
            bars.Add(new Bar(time, price - 0.1, price + 0.2, price - 0.2, price, 10000));
        }

        return bars;
    }

    private List<Bar> CreateTrendingDownBars()
    {
        var bars = new List<Bar>();
        var sessionStart = new DateTime(2024, 2, 1, 14, 30, 0, DateTimeKind.Utc); // 9:30 AM ET = 2:30 PM UTC

        // Opening range: 99.5 - 100.5
        for (int i = 0; i < 3; i++)
        {
            var time = sessionStart.AddMinutes(i * 5);
            bars.Add(new Bar(time, 100.5 - i * 0.2, 100.8 - i * 0.2, 99.8 - i * 0.2, 100.2 - i * 0.2, 10000));
        }

        // Breakout lower and continuation
        for (int i = 3; i < 30; i++)
        {
            var time = sessionStart.AddMinutes(i * 5);
            var price = 99.5 - (i - 3) * 0.05; // Trending lower
            bars.Add(new Bar(time, price + 0.1, price + 0.2, price - 0.2, price, 10000));
        }

        return bars;
    }

    private List<Bar> CreateRangeBoundBars()
    {
        var bars = new List<Bar>();
        var sessionStart = new DateTime(2024, 2, 1, 14, 30, 0, DateTimeKind.Utc); // 9:30 AM ET = 2:30 PM UTC

        // Stays within opening range - no breakout
        for (int i = 0; i < 30; i++)
        {
            var time = sessionStart.AddMinutes(i * 5);
            var price = 100.0 + Math.Sin(i * 0.3) * 0.3; // Oscillating within range
            bars.Add(new Bar(time, price - 0.1, price + 0.1, price - 0.15, price, 10000));
        }

        return bars;
    }

    private List<Bar> CreateVwapPersistentBars()
    {
        var bars = new List<Bar>();
        var sessionStart = new DateTime(2024, 2, 1, 14, 30, 0, DateTimeKind.Utc); // 9:30 AM ET = 2:30 PM UTC

        // Most bars above VWAP level (99.5)
        for (int i = 0; i < 30; i++)
        {
            var time = sessionStart.AddMinutes(i * 5);
            var price = i < 25 ? 100.0 + i * 0.02 : 99.3; // Most bars above, few below
            bars.Add(new Bar(time, price - 0.1, price + 0.1, price - 0.15, price, 10000));
        }

        return bars;
    }

    private List<Bar> CreateBarsWithRange(double dailyRange)
    {
        var bars = new List<Bar>();
        var sessionStart = new DateTime(2024, 2, 1, 14, 30, 0, DateTimeKind.Utc); // 9:30 AM ET = 2:30 PM UTC
        var low = 100.0 - dailyRange / 2;
        var high = 100.0 + dailyRange / 2;

        for (int i = 0; i < 30; i++)
        {
            var time = sessionStart.AddMinutes(i * 5);
            var price = low + (high - low) * (i / 29.0); // Linear progression through range
            bars.Add(new Bar(time, price - 0.05, Math.Min(price + 0.05, high), Math.Max(price - 0.05, low), price, 10000));
        }

        return bars;
    }

    private List<Bar> CreateAlignedSignalsBars()
    {
        var bars = new List<Bar>();
        var sessionStart = new DateTime(2024, 2, 1, 14, 30, 0, DateTimeKind.Utc); // 9:30 AM ET = 2:30 PM UTC

        // OR break up AND upward price progression (aligned signals)
        for (int i = 0; i < 3; i++) // Opening range
        {
            var time = sessionStart.AddMinutes(i * 5);
            bars.Add(new Bar(time, 99.8, 100.2, 99.6, 100.0, 10000));
        }

        for (int i = 3; i < 30; i++) // Breakout higher with consistent uptrend
        {
            var time = sessionStart.AddMinutes(i * 5);
            var price = 100.2 + (i - 3) * 0.03; // Steady uptrend
            bars.Add(new Bar(time, price - 0.05, price + 0.05, price - 0.1, price, 10000));
        }

        return bars;
    }

    private List<Bar> CreateVwapBarsWithRatio(double aboveVwapRatio)
    {
        var bars = new List<Bar>();
        var sessionStart = new DateTime(2024, 2, 1, 14, 30, 0, DateTimeKind.Utc); // 9:30 AM ET = 2:30 PM UTC
        int totalBars = 30;
        int barsAboveVwap = (int)(totalBars * aboveVwapRatio);

        // OR breakout up first
        for (int i = 0; i < 3; i++)
        {
            var time = sessionStart.AddMinutes(i * 5);
            bars.Add(new Bar(time, 99.8, 100.3, 99.6, 100.1, 10000));
        }

        // Then create bars with specified VWAP ratio
        for (int i = 3; i < totalBars; i++)
        {
            var time = sessionStart.AddMinutes(i * 5);
            var price = i < (3 + barsAboveVwap) ? 100.0 : 99.6; // Above or below VWAP (99.8)
            bars.Add(new Bar(time, price - 0.05, price + 0.05, price - 0.1, price, 10000));
        }

        return bars;
    }

    private List<Bar> CreateIdealConditionsBars()
    {
        var bars = new List<Bar>();
        var sessionStart = new DateTime(2024, 2, 1, 14, 30, 0, DateTimeKind.Utc); // 9:30 AM ET = 2:30 PM UTC

        // Perfect conditions: OR break up, VWAP persistence, calm range
        for (int i = 0; i < 3; i++) // Opening range
        {
            var time = sessionStart.AddMinutes(i * 5);
            bars.Add(new Bar(time, 100.0, 100.2, 99.8, 100.0, 10000));
        }

        // Breakout and persistent trend with limited daily range
        for (int i = 3; i < 30; i++)
        {
            var time = sessionStart.AddMinutes(i * 5);
            var price = 100.2 + (i - 3) * 0.01; // Gentle uptrend for calm conditions
            bars.Add(new Bar(time, price - 0.02, price + 0.02, price - 0.03, price, 10000));
        }

        return bars;
    }
}