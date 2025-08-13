using Xunit;
using FluentAssertions;
using Moq;
using ODTE.Backtest.Signals;
using ODTE.Backtest.Config;
using ODTE.Backtest.Data;
using ODTE.Backtest.Core;

namespace ODTE.Backtest.Tests.Signals;

/// <summary>
/// Tests for the RegimeScorer component that classifies market conditions.
/// Validates opening range breakouts, VWAP persistence, and volatility regimes.
/// </summary>
public class RegimeScorerTests
{
    private readonly SimConfig _testConfig;
    private readonly RegimeScorer _scorer;
    private readonly Mock<IMarketData> _mockMarketData;
    private readonly Mock<IEconCalendar> _mockCalendar;

    public RegimeScorerTests()
    {
        // Setup test configuration with typical parameters
        _testConfig = new SimConfig
        {
            Signals = new SignalsConfig
            {
                OrMinutes = 15,
                VwapWindowMinutes = 30,
                AtrPeriodBars = 20,
                EventBlockMinutesBefore = 60,
                EventBlockMinutesAfter = 15
            }
        };
        
        _scorer = new RegimeScorer(_testConfig);
        _mockMarketData = new Mock<IMarketData>();
        _mockCalendar = new Mock<IEconCalendar>();
    }

    [Fact]
    public void Score_WithOpeningRangeBreakout_ShouldDetectTrendBias()
    {
        // Arrange: Setup market data showing upward breakout from opening range
        var testDate = new DateTime(2024, 2, 1, 10, 30, 0);
        var sessionStart = new DateTime(2024, 2, 1, 9, 30, 0);
        
        var bars = new List<Bar>
        {
            // Opening range (9:30-9:45) - establish range
            new Bar { Ts = sessionStart, O = 100, H = 101, L = 99, C = 100.5, V = 1000 },
            new Bar { Ts = sessionStart.AddMinutes(5), O = 100.5, H = 101, L = 100, C = 100.8, V = 1000 },
            new Bar { Ts = sessionStart.AddMinutes(10), O = 100.8, H = 101, L = 100.5, C = 100.7, V = 1000 },
            new Bar { Ts = sessionStart.AddMinutes(15), O = 100.7, H = 101, L = 100.5, C = 100.9, V = 1000 },
            
            // Breakout above opening range
            new Bar { Ts = sessionStart.AddMinutes(20), O = 100.9, H = 102, L = 100.8, C = 101.8, V = 1500 },
            new Bar { Ts = sessionStart.AddMinutes(25), O = 101.8, H = 102.5, L = 101.5, C = 102.2, V = 2000 },
            new Bar { Ts = sessionStart.AddMinutes(30), O = 102.2, H = 102.8, L = 102, C = 102.5, V = 1800 },
            
            // Continue holding above range
            new Bar { Ts = sessionStart.AddMinutes(35), O = 102.5, H = 103, L = 102.3, C = 102.8, V = 1600 },
            new Bar { Ts = sessionStart.AddMinutes(40), O = 102.8, H = 103.2, L = 102.5, C = 103, V = 1700 },
            new Bar { Ts = sessionStart.AddMinutes(45), O = 103, H = 103.5, L = 102.8, C = 103.3, V = 1500 },
            new Bar { Ts = sessionStart.AddMinutes(50), O = 103.3, H = 103.8, L = 103, C = 103.5, V = 1400 },
            new Bar { Ts = sessionStart.AddMinutes(55), O = 103.5, H = 104, L = 103.3, C = 103.8, V = 1600 },
            new Bar { Ts = sessionStart.AddMinutes(60), O = 103.8, H = 104.2, L = 103.5, C = 104, V = 1800 }
        };
        
        _mockMarketData.Setup(m => m.GetBars(It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .Returns(bars);
        
        // No economic events
        _mockCalendar.Setup(c => c.GetEvents(It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .Returns(new List<EconEvent>());
        
        // Act
        var (score, calmRange, trendBiasUp, trendBiasDown) = _scorer.Score(testDate.AddMinutes(30), _mockMarketData.Object, _mockCalendar.Object);
        
        // Assert
        score.Should().BeGreaterThan(0, "Breakout above opening range should produce positive score");
        trendBiasUp.Should().BeTrue("Upward breakout should set bullish bias");
        trendBiasDown.Should().BeFalse("Upward breakout should not set bearish bias");
        calmRange.Should().BeFalse("Trending market should not be classified as calm range");
    }

    [Fact]
    public void Score_WithVWAPPersistence_ShouldIncreaseScore()
    {
        // Arrange: Price consistently above VWAP
        var testDate = new DateTime(2024, 2, 1, 11, 0, 0);
        var sessionStart = new DateTime(2024, 2, 1, 9, 30, 0);
        
        // Create bars with price above VWAP (simplified: VWAP ≈ average price weighted by volume)
        var bars = CreateTrendingBars(sessionStart, basePrice: 100, trend: 0.1, periods: 20);
        
        _mockMarketData.Setup(m => m.GetBars(It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .Returns(bars);
        
        _mockCalendar.Setup(c => c.GetEvents(It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .Returns(new List<EconEvent>());
        
        // Act
        var (score, _, _, _) = _scorer.Score(testDate, _mockMarketData.Object, _mockCalendar.Object);
        
        // Assert
        score.Should().BeGreaterThan(0, "VWAP persistence should contribute positive score");
    }

    [Fact]
    public void Score_NearEconomicEvent_ShouldReturnNegativeScore()
    {
        // Arrange: FOMC event scheduled close to current time
        var testDate = new DateTime(2024, 2, 1, 13, 30, 0); // 1:30 PM
        var fomcTime = new DateTime(2024, 2, 1, 14, 0, 0);  // 2:00 PM (30 min away)
        
        var bars = CreateNormalBars(new DateTime(2024, 2, 1, 9, 30, 0), 100, 20);
        
        _mockMarketData.Setup(m => m.GetBars(It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .Returns(bars);
        
        // Setup FOMC event within blocking window
        _mockCalendar.Setup(c => c.GetEvents(It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .Returns(new List<EconEvent> 
            { 
                new EconEvent { Ts = fomcTime, Kind = "FOMC" } 
            });
        
        // Act
        var (score, _, _, _) = _scorer.Score(testDate, _mockMarketData.Object, _mockCalendar.Object);
        
        // Assert
        score.Should().BeLessThan(0, "Score should be negative when near economic event");
    }

    [Fact]
    public void Score_InLateSession_ShouldPenalizeScore()
    {
        // Arrange: Late afternoon trading (gamma hour)
        var testDate = new DateTime(2024, 2, 1, 15, 30, 0); // 3:30 PM (30 min to close)
        
        var bars = CreateNormalBars(new DateTime(2024, 2, 1, 9, 30, 0), 100, 40);
        
        _mockMarketData.Setup(m => m.GetBars(It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .Returns(bars);
        
        _mockCalendar.Setup(c => c.GetEvents(It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .Returns(new List<EconEvent>());
        
        // Act
        var (score, _, _, _) = _scorer.Score(testDate, _mockMarketData.Object, _mockCalendar.Object);
        
        // Assert
        score.Should().BeLessThanOrEqualTo(-1, "Late session should heavily penalize score");
    }

    [Fact]
    public void Score_WithCalmVolatility_ShouldIdentifyRangeBoundMarket()
    {
        // Arrange: Low volatility, no breakouts - ideal for iron condors
        var testDate = new DateTime(2024, 2, 1, 11, 0, 0);
        var sessionStart = new DateTime(2024, 2, 1, 9, 30, 0);
        
        // Create bars with minimal movement (range-bound)
        var bars = CreateRangeBoundBars(sessionStart, centerPrice: 100, range: 0.5, periods: 20);
        
        _mockMarketData.Setup(m => m.GetBars(It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .Returns(bars);
        
        _mockCalendar.Setup(c => c.GetEvents(It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .Returns(new List<EconEvent>());
        
        // Act
        var (score, calmRange, trendBiasUp, trendBiasDown) = _scorer.Score(testDate, _mockMarketData.Object, _mockCalendar.Object);
        
        // Assert
        calmRange.Should().BeTrue("Low volatility should indicate calm range");
        trendBiasUp.Should().BeFalse("No trend bias in range-bound market");
        trendBiasDown.Should().BeFalse("No trend bias in range-bound market");
        score.Should().BeGreaterThanOrEqualTo(0, "Calm conditions should not penalize score");
    }

    [Fact]
    public void Score_WithDownwardBreakout_ShouldDetectBearishBias()
    {
        // Arrange: Setup market data showing downward breakout
        var testDate = new DateTime(2024, 2, 1, 10, 30, 0);
        var sessionStart = new DateTime(2024, 2, 1, 9, 30, 0);
        
        var bars = new List<Bar>
        {
            // Opening range
            new Bar { Ts = sessionStart, O = 100, H = 101, L = 99, C = 99.5, V = 1000 },
            new Bar { Ts = sessionStart.AddMinutes(5), O = 99.5, H = 100.5, L = 99, C = 99.2, V = 1000 },
            new Bar { Ts = sessionStart.AddMinutes(10), O = 99.2, H = 100, L = 99, C = 99.3, V = 1000 },
            new Bar { Ts = sessionStart.AddMinutes(15), O = 99.3, H = 100, L = 99, C = 99.1, V = 1000 },
            
            // Breakout below opening range
            new Bar { Ts = sessionStart.AddMinutes(20), O = 99.1, H = 99.2, L = 98.5, C = 98.6, V = 1500 },
            new Bar { Ts = sessionStart.AddMinutes(25), O = 98.6, H = 98.8, L = 98, C = 98.2, V = 2000 },
            new Bar { Ts = sessionStart.AddMinutes(30), O = 98.2, H = 98.5, L = 97.8, C = 98, V = 1800 },
            
            // Continue below range
            new Bar { Ts = sessionStart.AddMinutes(35), O = 98, H = 98.3, L = 97.5, C = 97.7, V = 1600 },
            new Bar { Ts = sessionStart.AddMinutes(40), O = 97.7, H = 98, L = 97.3, C = 97.5, V = 1700 },
            new Bar { Ts = sessionStart.AddMinutes(45), O = 97.5, H = 97.8, L = 97.2, C = 97.3, V = 1500 },
            new Bar { Ts = sessionStart.AddMinutes(50), O = 97.3, H = 97.6, L = 97, C = 97.2, V = 1400 },
            new Bar { Ts = sessionStart.AddMinutes(55), O = 97.2, H = 97.5, L = 96.8, C = 97, V = 1600 },
            new Bar { Ts = sessionStart.AddMinutes(60), O = 97, H = 97.3, L = 96.7, C = 96.8, V = 1800 }
        };
        
        _mockMarketData.Setup(m => m.GetBars(It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .Returns(bars);
        
        _mockCalendar.Setup(c => c.GetEvents(It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .Returns(new List<EconEvent>());
        
        // Act
        var (score, calmRange, trendBiasUp, trendBiasDown) = _scorer.Score(testDate.AddMinutes(30), _mockMarketData.Object, _mockCalendar.Object);
        
        // Assert
        score.Should().BeGreaterThan(0, "Breakout below opening range should still produce positive score for trading");
        trendBiasDown.Should().BeTrue("Downward breakout should set bearish bias");
        trendBiasUp.Should().BeFalse("Downward breakout should not set bullish bias");
    }

    [Fact]
    public void Score_WithVolatilityExpansion_ShouldAdjustRegime()
    {
        // Arrange: High volatility day (large daily range vs ATR)
        var testDate = new DateTime(2024, 2, 1, 11, 0, 0);
        var sessionStart = new DateTime(2024, 2, 1, 9, 30, 0);
        
        // Create volatile bars with large swings
        var bars = CreateVolatileBars(sessionStart, basePrice: 100, volatility: 2.0, periods: 20);
        
        _mockMarketData.Setup(m => m.GetBars(It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .Returns(bars);
        
        _mockCalendar.Setup(c => c.GetEvents(It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .Returns(new List<EconEvent>());
        
        // Act
        var (score, calmRange, _, _) = _scorer.Score(testDate, _mockMarketData.Object, _mockCalendar.Object);
        
        // Assert
        calmRange.Should().BeFalse("High volatility should not indicate calm range");
        // Note: High volatility might increase score if it supports trend trading
    }

    [Fact]
    public void Score_WithMultipleNegativeFactors_ShouldBlockTrading()
    {
        // Arrange: Combine multiple negative factors
        var testDate = new DateTime(2024, 2, 1, 15, 45, 0); // Very late session
        var nfpTime = new DateTime(2024, 2, 1, 16, 30, 0);  // NFP event upcoming
        
        var bars = CreateNormalBars(new DateTime(2024, 2, 1, 9, 30, 0), 100, 40);
        
        _mockMarketData.Setup(m => m.GetBars(It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .Returns(bars);
        
        _mockCalendar.Setup(c => c.GetEvents(It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .Returns(new List<EconEvent> 
            { 
                new EconEvent { Ts = nfpTime, Kind = "NFP" } 
            });
        
        // Act
        var (score, _, _, _) = _scorer.Score(testDate, _mockMarketData.Object, _mockCalendar.Object);
        
        // Assert
        score.Should().BeLessThan(-3, "Multiple risk factors should produce very negative score");
    }

    // Helper methods to create test data
    private List<Bar> CreateNormalBars(DateTime start, double basePrice, int count)
    {
        var bars = new List<Bar>();
        var random = new Random(42);
        
        for (int i = 0; i < count; i++)
        {
            double noise = (random.NextDouble() - 0.5) * 0.5;
            double open = basePrice + noise;
            double close = open + (random.NextDouble() - 0.5) * 0.3;
            double high = Math.Max(open, close) + random.NextDouble() * 0.2;
            double low = Math.Min(open, close) - random.NextDouble() * 0.2;
            
            bars.Add(new Bar
            {
                Ts = start.AddMinutes(i * 5),
                O = open,
                H = high,
                L = low,
                C = close,
                V = 1000 + random.Next(500)
            });
        }
        
        return bars;
    }

    private List<Bar> CreateTrendingBars(DateTime start, double basePrice, double trend, int count)
    {
        var bars = new List<Bar>();
        double currentPrice = basePrice;
        
        for (int i = 0; i < count; i++)
        {
            currentPrice += trend;
            double open = currentPrice;
            double close = currentPrice + trend * 0.5;
            
            bars.Add(new Bar
            {
                Ts = start.AddMinutes(i * 5),
                O = open,
                H = close + 0.1,
                L = open - 0.05,
                C = close,
                V = 1000 + i * 50
            });
        }
        
        return bars;
    }

    private List<Bar> CreateRangeBoundBars(DateTime start, double centerPrice, double range, int count)
    {
        var bars = new List<Bar>();
        var random = new Random(42);
        
        for (int i = 0; i < count; i++)
        {
            double oscillation = Math.Sin(i * 0.5) * range;
            double open = centerPrice + oscillation;
            double close = centerPrice + oscillation * 0.8;
            
            bars.Add(new Bar
            {
                Ts = start.AddMinutes(i * 5),
                O = open,
                H = Math.Max(open, close) + range * 0.1,
                L = Math.Min(open, close) - range * 0.1,
                C = close,
                V = 1000
            });
        }
        
        return bars;
    }

    private List<Bar> CreateVolatileBars(DateTime start, double basePrice, double volatility, int count)
    {
        var bars = new List<Bar>();
        var random = new Random(42);
        
        for (int i = 0; i < count; i++)
        {
            double swing = (random.NextDouble() - 0.5) * volatility * 2;
            double open = basePrice + swing;
            double close = basePrice + (random.NextDouble() - 0.5) * volatility * 2;
            
            bars.Add(new Bar
            {
                Ts = start.AddMinutes(i * 5),
                O = open,
                H = Math.Max(open, close) + volatility,
                L = Math.Min(open, close) - volatility,
                C = close,
                V = 1500 + random.Next(1000)
            });
        }
        
        return bars;
    }
}