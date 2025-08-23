using System;
using FluentAssertions;
using ODTE.Contracts.Strategy;
using Xunit;

namespace ODTE.Contracts.Tests.Strategy
{
    public class YearlyPerformanceTests
    {
        [Fact]
        public void Constructor_DefaultConstructor_ShouldCreateEmptyYearlyPerformance()
        {
            // Arrange & Act
            var performance = new YearlyPerformance();
            
            // Assert
            performance.Year.Should().Be(0);
            performance.TotalPnL.Should().Be(0);
            performance.MaxDrawdown.Should().Be(0);
            performance.WinRate.Should().Be(0);
            performance.TotalTrades.Should().Be(0);
            performance.SharpeRatio.Should().Be(0);
        }

        [Fact]
        public void Year_SetValue_ShouldUpdateYear()
        {
            // Arrange
            var performance = new YearlyPerformance();
            
            // Act
            performance.Year = 2023;
            
            // Assert
            performance.Year.Should().Be(2023);
        }

        [Fact]
        public void TotalPnL_SetPositiveValue_ShouldUpdateTotalPnL()
        {
            // Arrange
            var performance = new YearlyPerformance();
            
            // Act
            performance.TotalPnL = 125000m;
            
            // Assert
            performance.TotalPnL.Should().Be(125000m);
        }

        [Fact]
        public void TotalPnL_SetNegativeValue_ShouldUpdateTotalPnL()
        {
            // Arrange
            var performance = new YearlyPerformance();
            
            // Act
            performance.TotalPnL = -25000m;
            
            // Assert
            performance.TotalPnL.Should().Be(-25000m);
        }

        [Fact]
        public void MaxDrawdown_SetValue_ShouldUpdateMaxDrawdown()
        {
            // Arrange
            var performance = new YearlyPerformance();
            
            // Act
            performance.MaxDrawdown = -0.15m; // -15% drawdown
            
            // Assert
            performance.MaxDrawdown.Should().Be(-0.15m);
        }

        [Fact]
        public void WinRate_SetValue_ShouldUpdateWinRate()
        {
            // Arrange
            var performance = new YearlyPerformance();
            
            // Act
            performance.WinRate = 0.75; // 75% win rate
            
            // Assert
            performance.WinRate.Should().Be(0.75);
        }

        [Fact]
        public void TotalTrades_SetValue_ShouldUpdateTotalTrades()
        {
            // Arrange
            var performance = new YearlyPerformance();
            
            // Act
            performance.TotalTrades = 250;
            
            // Assert
            performance.TotalTrades.Should().Be(250);
        }

        [Fact]
        public void SharpeRatio_SetValue_ShouldUpdateSharpeRatio()
        {
            // Arrange
            var performance = new YearlyPerformance();
            
            // Act
            performance.SharpeRatio = 1.85;
            
            // Assert
            performance.SharpeRatio.Should().Be(1.85);
        }

        [Fact]
        public void ToString_WithProfitableYear_ShouldReturnFormattedString()
        {
            // Arrange
            var performance = new YearlyPerformance
            {
                Year = 2023,
                TotalPnL = 125000m,
                TotalTrades = 250,
                WinRate = 0.75
            };
            
            // Act
            var result = performance.ToString();
            
            // Assert
            result.Should().Be("Year 2023: PnL=$125,000.00, Trades=250, WinRate=75.0%");
        }

        [Fact]
        public void ToString_WithLosingYear_ShouldReturnFormattedString()
        {
            // Arrange
            var performance = new YearlyPerformance
            {
                Year = 2022,
                TotalPnL = -25000m,
                TotalTrades = 180,
                WinRate = 0.45
            };
            
            // Act
            var result = performance.ToString();
            
            // Assert
            result.Should().Be("Year 2022: PnL=-$25,000.00, Trades=180, WinRate=45.0%");
        }

        [Fact]
        public void ToString_WithZeroValues_ShouldReturnFormattedString()
        {
            // Arrange
            var performance = new YearlyPerformance
            {
                Year = 2021,
                TotalPnL = 0m,
                TotalTrades = 0,
                WinRate = 0
            };
            
            // Act
            var result = performance.ToString();
            
            // Assert
            result.Should().Be("Year 2021: PnL=$0.00, Trades=0, WinRate=0.0%");
        }

        [Theory]
        [InlineData(2020, 50000, 100, 0.60, "Year 2020: PnL=$50,000.00, Trades=100, WinRate=60.0%")]
        [InlineData(2021, -10000, 75, 0.40, "Year 2021: PnL=-$10,000.00, Trades=75, WinRate=40.0%")]
        [InlineData(2022, 0, 0, 0, "Year 2022: PnL=$0.00, Trades=0, WinRate=0.0%")]
        [InlineData(2023, 999999.99, 1000, 1.0, "Year 2023: PnL=$999,999.99, Trades=1000, WinRate=100.0%")]
        public void ToString_WithVariousValues_ShouldReturnCorrectFormat(int year, decimal pnl, int trades, double winRate, string expected)
        {
            // Arrange
            var performance = new YearlyPerformance
            {
                Year = year,
                TotalPnL = pnl,
                TotalTrades = trades,
                WinRate = winRate
            };
            
            // Act
            var result = performance.ToString();
            
            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public void IYearlyPerformance_Interface_ShouldBeImplementedCorrectly()
        {
            // Arrange
            IYearlyPerformance performance = new YearlyPerformance();
            
            // Act & Assert
            performance.Year = 2023;
            performance.TotalPnL = 100000m;
            performance.MaxDrawdown = -0.20m;
            performance.WinRate = 0.70;
            performance.TotalTrades = 200;
            performance.SharpeRatio = 1.50;
            
            performance.Year.Should().Be(2023);
            performance.TotalPnL.Should().Be(100000m);
            performance.MaxDrawdown.Should().Be(-0.20m);
            performance.WinRate.Should().Be(0.70);
            performance.TotalTrades.Should().Be(200);
            performance.SharpeRatio.Should().Be(1.50);
        }

        [Theory]
        [InlineData(0.0, 0.0)]
        [InlineData(0.5, 0.5)]
        [InlineData(1.0, 1.0)]
        [InlineData(-0.5, -0.5)]
        [InlineData(2.5, 2.5)]
        public void WinRate_WithVariousValues_ShouldAcceptAllRanges(double inputWinRate, double expectedWinRate)
        {
            // Arrange
            var performance = new YearlyPerformance();
            
            // Act
            performance.WinRate = inputWinRate;
            
            // Assert
            performance.WinRate.Should().Be(expectedWinRate);
        }

        [Theory]
        [InlineData(-1000000, -1000000)]
        [InlineData(0, 0)]
        [InlineData(1000000, 1000000)]
        [InlineData(123.45, 123.45)]
        [InlineData(-67.89, -67.89)]
        public void TotalPnL_WithVariousValues_ShouldAcceptAllRanges(double inputPnL, double expectedPnL)
        {
            // Arrange
            var performance = new YearlyPerformance();
            var decimalPnL = (decimal)inputPnL;
            
            // Act
            performance.TotalPnL = decimalPnL;
            
            // Assert
            performance.TotalPnL.Should().Be((decimal)expectedPnL);
        }

        [Theory]
        [InlineData(-3.0, -3.0)]
        [InlineData(-1.0, -1.0)]
        [InlineData(0.0, 0.0)]
        [InlineData(1.5, 1.5)]
        [InlineData(5.0, 5.0)]
        public void SharpeRatio_WithVariousValues_ShouldAcceptAllRanges(double inputSharpe, double expectedSharpe)
        {
            // Arrange
            var performance = new YearlyPerformance();
            
            // Act
            performance.SharpeRatio = inputSharpe;
            
            // Assert
            performance.SharpeRatio.Should().Be(expectedSharpe);
        }

        [Theory]
        [InlineData(1900)]
        [InlineData(2000)]
        [InlineData(2023)]
        [InlineData(2050)]
        [InlineData(-1)]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        public void Year_WithVariousValues_ShouldAcceptAllRanges(int inputYear)
        {
            // Arrange
            var performance = new YearlyPerformance();
            
            // Act
            performance.Year = inputYear;
            
            // Assert
            performance.Year.Should().Be(inputYear);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(1000)]
        [InlineData(int.MaxValue)]
        public void TotalTrades_WithVariousValues_ShouldAcceptAllRanges(int inputTrades)
        {
            // Arrange
            var performance = new YearlyPerformance();
            
            // Act
            performance.TotalTrades = inputTrades;
            
            // Assert
            performance.TotalTrades.Should().Be(inputTrades);
        }
    }
}