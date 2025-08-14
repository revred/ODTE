using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using ODTE.Historical;

namespace ODTE.Historical.Tests
{
    /// <summary>
    /// Unit tests for OptionsDataGenerator
    /// Validates synthetic data generation functionality
    /// </summary>
    public class OptionsDataGeneratorTests
    {
        private readonly OptionsDataGenerator _generator;

        public OptionsDataGeneratorTests()
        {
            _generator = new OptionsDataGenerator();
        }

        [Fact]
        public void SourceName_ShouldReturnCorrectName()
        {
            // Act
            var sourceName = _generator.SourceName;

            // Assert
            sourceName.Should().Be("Advanced Synthetic (Research-Based)");
        }

        [Fact]
        public void IsRealTime_ShouldReturnFalse()
        {
            // Act
            var isRealTime = _generator.IsRealTime;

            // Assert
            isRealTime.Should().BeFalse();
        }

        [Fact]
        public async Task GenerateTradingDayAsync_ShouldReturnValidData()
        {
            // Arrange
            var tradingDay = new DateTime(2024, 8, 15); // Thursday
            var symbol = "SPY";

            // Act
            var result = await _generator.GenerateTradingDayAsync(tradingDay, symbol);

            // Assert
            result.Should().NotBeNull();
            result.Should().NotBeEmpty();
            result.Count.Should().BeGreaterThan(0);
            
            // Check that we get approximately 390 minutes of trading data (6.5 hours)
            result.Count.Should().BeCloseTo(390, 50);
        }

        [Fact]
        public async Task GenerateTradingDayAsync_ShouldHaveRealisticPrices()
        {
            // Arrange
            var tradingDay = new DateTime(2024, 8, 15);
            var symbol = "SPY";

            // Act
            var result = await _generator.GenerateTradingDayAsync(tradingDay, symbol);

            // Assert
            foreach (var bar in result)
            {
                // Basic OHLC validation
                bar.High.Should().BeGreaterOrEqualTo(Math.Max(bar.Open, bar.Close));
                bar.Low.Should().BeLessOrEqualTo(Math.Min(bar.Open, bar.Close));
                
                // Reasonable price range for SPY
                bar.Close.Should().BeInRange(100, 600);
                
                // Positive volume
                bar.Volume.Should().BeGreaterThan(0);
            }
        }

        [Fact]
        public async Task GenerateTradingDayAsync_ShouldHaveSequentialTimestamps()
        {
            // Arrange
            var tradingDay = new DateTime(2024, 8, 15);
            var symbol = "SPY";

            // Act
            var result = await _generator.GenerateTradingDayAsync(tradingDay, symbol);

            // Assert
            for (int i = 1; i < result.Count; i++)
            {
                result[i].Timestamp.Should().BeAfter(result[i-1].Timestamp);
            }
        }

        [Theory]
        [InlineData("SPY")]
        [InlineData("QQQ")]
        [InlineData("IWM")]
        public async Task GenerateTradingDayAsync_ShouldWorkForDifferentSymbols(string symbol)
        {
            // Arrange
            var tradingDay = new DateTime(2024, 8, 15);

            // Act
            var result = await _generator.GenerateTradingDayAsync(tradingDay, symbol);

            // Assert
            result.Should().NotBeNull();
            result.Should().NotBeEmpty();
        }

        [Fact]
        public async Task GenerateTradingDayAsync_ShouldStartAtMarketOpen()
        {
            // Arrange
            var tradingDay = new DateTime(2024, 8, 15);
            var symbol = "SPY";

            // Act
            var result = await _generator.GenerateTradingDayAsync(tradingDay, symbol);

            // Assert
            var firstBar = result.First();
            firstBar.Timestamp.Hour.Should().Be(9);
            firstBar.Timestamp.Minute.Should().Be(30);
        }

        [Fact]
        public async Task GenerateTradingDayAsync_WeekendDay_ShouldStillGenerate()
        {
            // Arrange
            var saturday = new DateTime(2024, 8, 17); // Saturday
            var symbol = "SPY";

            // Act
            var result = await _generator.GenerateTradingDayAsync(saturday, symbol);

            // Assert
            // Should still generate data even for non-trading days (for testing purposes)
            result.Should().NotBeNull();
            result.Should().NotBeEmpty();
        }
    }
}