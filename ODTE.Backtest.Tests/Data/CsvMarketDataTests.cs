using FluentAssertions;
using ODTE.Backtest.Core;
using ODTE.Backtest.Data;
using Xunit;

namespace ODTE.Backtest.Tests.Data;

/// <summary>
/// Comprehensive tests for CSV-based market data provider.
/// Tests data loading, filtering, and technical indicator calculations.
/// </summary>
public class CsvMarketDataTests : IDisposable
{
    private readonly string _testDataDir;
    private readonly string _testCsvPath;

    public CsvMarketDataTests()
    {
        _testDataDir = Path.Combine(Path.GetTempPath(), $"ODTETest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDataDir);
        _testCsvPath = Path.Combine(_testDataDir, "test_bars.csv");
        CreateTestCsvFile();
    }

    [Fact]
    public void Constructor_ValidCsvFile_ShouldLoadDataCorrectly()
    {
        // Arrange & Act
        var marketData = new CsvMarketData(_testCsvPath, "America/New_York", false);

        // Assert
        marketData.Should().NotBeNull();
        marketData.BarInterval.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void GetBars_SpecificDateRange_ShouldReturnFilteredBars()
    {
        // Arrange
        var marketData = new CsvMarketData(_testCsvPath, "America/New_York", false);
        var startDate = DateOnly.FromDateTime(new DateTime(2024, 2, 1));
        var endDate = DateOnly.FromDateTime(new DateTime(2024, 2, 1));

        // Act
        var bars = marketData.GetBars(startDate, endDate).ToList();

        // Assert
        bars.Should().NotBeEmpty();
        bars.Should().AllSatisfy(bar =>
        {
            var barDate = DateOnly.FromDateTime(bar.Ts);
            barDate.Should().BeOnOrAfter(startDate);
            barDate.Should().BeOnOrBefore(endDate);
        });
    }

    [Fact]
    public void GetBars_RthOnlyEnabled_ShouldFilterToRegularTradingHours()
    {
        // Arrange
        CreateExtendedHoursCsvFile(); // Include pre-market and after-hours data
        var extendedCsvPath = Path.Combine(_testDataDir, "extended_bars.csv");
        var marketData = new CsvMarketData(extendedCsvPath, "America/New_York", true);
        var startDate = DateOnly.FromDateTime(new DateTime(2024, 2, 1));
        var endDate = DateOnly.FromDateTime(new DateTime(2024, 2, 1));

        // Act
        var bars = marketData.GetBars(startDate, endDate).ToList();

        // Assert
        bars.Should().NotBeEmpty();
        bars.Should().AllSatisfy(bar =>
        {
            // Verify bars are within RTH by using the same IsRth logic
            bar.Ts.IsRth().Should().BeTrue("Bar should be within Regular Trading Hours (9:30 AM - 4:00 PM ET)");
        });
    }

    [Fact]
    public void GetBars_RthOnlyDisabled_ShouldIncludeExtendedHours()
    {
        // Arrange
        CreateExtendedHoursCsvFile();
        var extendedCsvPath = Path.Combine(_testDataDir, "extended_bars.csv");
        var marketData = new CsvMarketData(extendedCsvPath, "America/New_York", false);
        var startDate = DateOnly.FromDateTime(new DateTime(2024, 2, 1));
        var endDate = DateOnly.FromDateTime(new DateTime(2024, 2, 1));

        // Act
        var bars = marketData.GetBars(startDate, endDate).ToList();

        // Assert
        bars.Should().NotBeEmpty();

        // Should include pre-market and after-hours bars
        var hasPreMarket = bars.Any(bar => bar.Ts.Hour < 9 || (bar.Ts.Hour == 9 && bar.Ts.Minute < 30));
        var hasAfterHours = bars.Any(bar => bar.Ts.Hour >= 16);

        (hasPreMarket || hasAfterHours).Should().BeTrue();
    }

    [Fact]
    public void GetSpot_ValidTimestamp_ShouldReturnClosePrice()
    {
        // Arrange
        var marketData = new CsvMarketData(_testCsvPath, "America/New_York", false);
        var testTime = new DateTime(2024, 2, 1, 10, 0, 0);

        // Act
        var spot = marketData.GetSpot(testTime);

        // Assert
        spot.Should().BeGreaterThan(0);
        spot.Should().BeInRange(99, 102); // Based on test data
    }

    [Fact]
    public void GetSpot_TimestampBeforeData_ShouldHandleGracefully()
    {
        // Arrange
        var marketData = new CsvMarketData(_testCsvPath, "America/New_York", false);
        var earlyTime = new DateTime(2024, 1, 1, 9, 30, 0);

        // Act
        var spot = marketData.GetSpot(earlyTime);

        // Assert
        spot.Should().BeGreaterThanOrEqualTo(0); // Should not crash
    }

    [Fact]
    public void Atr20Minutes_SufficientData_ShouldCalculateAtr()
    {
        // Arrange
        var marketData = new CsvMarketData(_testCsvPath, "America/New_York", false);
        var testTime = new DateTime(2024, 2, 1, 11, 30, 0); // Late enough to have 20 bars before

        // Act
        var atr = marketData.Atr20Minutes(testTime);

        // Assert
        atr.Should().BeGreaterThan(0);
        atr.Should().BeLessThan(10); // Reasonable ATR for normal market conditions
    }

    [Fact]
    public void Atr20Minutes_InsufficientData_ShouldReturnZero()
    {
        // Arrange
        var marketData = new CsvMarketData(_testCsvPath, "America/New_York", false);
        var earlyTime = new DateTime(2024, 2, 1, 9, 30, 0); // First bar

        // Act
        var atr = marketData.Atr20Minutes(earlyTime);

        // Assert
        atr.Should().Be(0);
    }

    [Fact]
    public void Vwap_ValidWindow_ShouldCalculateVwap()
    {
        // Arrange
        var marketData = new CsvMarketData(_testCsvPath, "America/New_York", false);
        var testTime = new DateTime(2024, 2, 1, 11, 0, 0);
        var window = TimeSpan.FromMinutes(30);

        // Act
        var vwap = marketData.Vwap(testTime, window);

        // Assert
        vwap.Should().BeGreaterThan(0);
        vwap.Should().BeInRange(99, 103); // Should be close to prices in test data (relaxed range)
    }

    [Fact]
    public void Vwap_EmptyWindow_ShouldHandleGracefully()
    {
        // Arrange
        var marketData = new CsvMarketData(_testCsvPath, "America/New_York", false);
        var testTime = new DateTime(2024, 1, 1, 9, 30, 0); // Before any data
        var window = TimeSpan.FromMinutes(30);

        // Act
        var vwap = marketData.Vwap(testTime, window);

        // Assert
        vwap.Should().BeGreaterThanOrEqualTo(0); // Should not crash
    }

    [Fact]
    public void BarInterval_ShouldCalculateFromActualBars()
    {
        // Arrange & Act
        var marketData = new CsvMarketData(_testCsvPath, "America/New_York", false);

        // Assert
        marketData.BarInterval.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void Constructor_InvalidFile_ShouldThrowException()
    {
        // Arrange
        var invalidPath = Path.Combine(_testDataDir, "nonexistent.csv");

        // Act & Assert
        var act = () => new CsvMarketData(invalidPath, "America/New_York", false);
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void Constructor_EmptyFile_ShouldHandleGracefully()
    {
        // Arrange
        var emptyPath = Path.Combine(_testDataDir, "empty.csv");
        File.WriteAllText(emptyPath, "ts,o,h,l,c,v\n"); // Header only

        // Act
        var marketData = new CsvMarketData(emptyPath, "America/New_York", false);
        var bars = marketData.GetBars(DateOnly.MinValue, DateOnly.MaxValue);

        // Assert
        bars.Should().BeEmpty();
        marketData.BarInterval.Should().Be(TimeSpan.FromMinutes(1)); // Default fallback
    }

    [Fact]
    public void GetBars_DataShouldBeOrderedByTimestamp()
    {
        // Arrange
        CreateUnorderedCsvFile(); // Create file with unordered timestamps
        var unorderedPath = Path.Combine(_testDataDir, "unordered_bars.csv");
        var marketData = new CsvMarketData(unorderedPath, "America/New_York", false);

        // Act
        var bars = marketData.GetBars(DateOnly.MinValue, DateOnly.MaxValue).ToList();

        // Assert
        bars.Should().NotBeEmpty();
        for (int i = 1; i < bars.Count; i++)
        {
            bars[i].Ts.Should().BeOnOrAfter(bars[i - 1].Ts);
        }
    }

    [Theory]
    [InlineData("America/New_York")]
    [InlineData("Europe/London")]
    [InlineData("UTC")]
    public void Constructor_DifferentTimezones_ShouldAcceptTimezone(string timezone)
    {
        // Act & Assert
        var act = () => new CsvMarketData(_testCsvPath, timezone, false);
        act.Should().NotThrow();
    }

    private void CreateTestCsvFile()
    {
        var csvContent = "ts,o,h,l,c,v\n";
        var baseDate = new DateTime(2024, 2, 1, 9, 30, 0);
        var basePrice = 100.0;

        for (int i = 0; i < 100; i++)
        {
            var timestamp = baseDate.AddMinutes(i * 5);
            var price = basePrice + Math.Sin(i * 0.1) * 2; // Create realistic price movement
            var open = price;
            var high = price + 0.5;
            var low = price - 0.5;
            var close = price + 0.2;
            var volume = 10000 + i * 100;

            csvContent += $"{timestamp:yyyy-MM-dd HH:mm:ss},{open:F2},{high:F2},{low:F2},{close:F2},{volume}\n";
        }

        File.WriteAllText(_testCsvPath, csvContent);
    }

    private void CreateExtendedHoursCsvFile()
    {
        var csvContent = "ts,o,h,l,c,v\n";
        var baseDate = new DateTime(2024, 2, 1, 4, 0, 0); // Start at 4 AM
        var basePrice = 100.0;

        // Create bars from 4 AM to 8 PM (including pre-market and after-hours)
        for (int i = 0; i < 200; i++)
        {
            var timestamp = baseDate.AddMinutes(i * 5);
            var price = basePrice + Math.Sin(i * 0.1) * 2;
            var open = price;
            var high = price + 0.5;
            var low = price - 0.5;
            var close = price + 0.2;
            var volume = timestamp.Hour >= 9 && timestamp.Hour < 16 ? 10000 : 2000; // Lower volume outside RTH

            csvContent += $"{timestamp:yyyy-MM-dd HH:mm:ss},{open:F2},{high:F2},{low:F2},{close:F2},{volume}\n";
        }

        File.WriteAllText(Path.Combine(_testDataDir, "extended_bars.csv"), csvContent);
    }

    private void CreateUnorderedCsvFile()
    {
        var csvContent = "ts,o,h,l,c,v\n";
        var timestamps = new List<DateTime>();
        var baseDate = new DateTime(2024, 2, 1, 9, 30, 0);

        // Create unordered timestamps
        for (int i = 0; i < 20; i++)
        {
            timestamps.Add(baseDate.AddMinutes(i * 5));
        }

        // Shuffle the timestamps
        var random = new Random(42);
        for (int i = timestamps.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (timestamps[i], timestamps[j]) = (timestamps[j], timestamps[i]);
        }

        // Write CSV with unordered timestamps
        for (int i = 0; i < timestamps.Count; i++)
        {
            var timestamp = timestamps[i];
            var price = 100.0 + i * 0.1;
            csvContent += $"{timestamp:yyyy-MM-dd HH:mm:ss},{price:F2},{price + 0.5:F2},{price - 0.5:F2},{price + 0.2:F2},10000\n";
        }

        File.WriteAllText(Path.Combine(_testDataDir, "unordered_bars.csv"), csvContent);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDataDir))
        {
            Directory.Delete(_testDataDir, true);
        }
    }
}