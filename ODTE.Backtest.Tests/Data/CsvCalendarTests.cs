using FluentAssertions;
using ODTE.Backtest.Core;
using ODTE.Backtest.Data;
using Xunit;
using System.IO;

namespace ODTE.Backtest.Tests.Data;

/// <summary>
/// Comprehensive tests for CSV-based economic calendar data provider.
/// Tests event loading, filtering, and economic event blocking logic.
/// </summary>
public class CsvCalendarTests : IDisposable
{
    private readonly string _testDataDir;
    private readonly string _testCsvPath;

    public CsvCalendarTests()
    {
        _testDataDir = Path.Combine(Path.GetTempPath(), $"ODTETest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDataDir);
        _testCsvPath = Path.Combine(_testDataDir, "test_calendar.csv");
        CreateTestCalendarFile();
    }

    [Fact]
    public void Constructor_ValidCsvFile_ShouldLoadEventsCorrectly()
    {
        // Arrange & Act
        var calendar = new CsvCalendar(_testCsvPath, "America/New_York");

        // Assert
        calendar.Should().NotBeNull();
    }

    [Fact]
    public void NextEventAfter_FutureEvent_ShouldReturnCorrectEvent()
    {
        // Arrange
        var calendar = new CsvCalendar(_testCsvPath, "America/New_York");
        var queryTime = new DateTime(2024, 2, 1, 13, 0, 0); // Before FOMC at 2 PM

        // Act
        var nextEvent = calendar.NextEventAfter(queryTime);

        // Assert
        nextEvent.Should().NotBeNull();
        nextEvent!.Kind.Should().Be("FOMC");
        nextEvent.Ts.Should().BeAfter(queryTime);
    }

    [Fact]
    public void NextEventAfter_NoFutureEvents_ShouldReturnNull()
    {
        // Arrange
        var calendar = new CsvCalendar(_testCsvPath, "America/New_York");
        var queryTime = new DateTime(2024, 2, 5, 18, 0, 0); // After all test events

        // Act
        var nextEvent = calendar.NextEventAfter(queryTime);

        // Assert
        nextEvent.Should().BeNull();
    }

    [Fact]
    public void NextEventAfter_ExactEventTime_ShouldReturnSameEvent()
    {
        // Arrange
        var calendar = new CsvCalendar(_testCsvPath, "America/New_York");
        var eventTime = new DateTime(2024, 2, 1, 14, 0, 0); // FOMC time

        // Act
        var nextEvent = calendar.NextEventAfter(eventTime);

        // Assert
        nextEvent.Should().NotBeNull();
        nextEvent!.Kind.Should().Be("FOMC");
        nextEvent.Ts.Should().Be(eventTime);
    }

    [Fact]
    public void GetEvents_SpecificDateRange_ShouldReturnFilteredEvents()
    {
        // Arrange
        var calendar = new CsvCalendar(_testCsvPath, "America/New_York");
        var startDate = DateOnly.FromDateTime(new DateTime(2024, 2, 1));
        var endDate = DateOnly.FromDateTime(new DateTime(2024, 2, 1));

        // Act
        var events = calendar.GetEvents(startDate, endDate);

        // Assert
        events.Should().NotBeEmpty();
        events.Should().AllSatisfy(evt =>
        {
            var eventDate = DateOnly.FromDateTime(evt.Ts);
            eventDate.Should().BeOnOrAfter(startDate);
            eventDate.Should().BeOnOrBefore(endDate);
        });
    }

    [Fact]
    public void GetEvents_MultiDayRange_ShouldReturnEventsFromAllDays()
    {
        // Arrange
        var calendar = new CsvCalendar(_testCsvPath, "America/New_York");
        var startDate = DateOnly.FromDateTime(new DateTime(2024, 2, 1));
        var endDate = DateOnly.FromDateTime(new DateTime(2024, 2, 3));

        // Act
        var events = calendar.GetEvents(startDate, endDate);

        // Assert
        events.Should().NotBeEmpty();
        events.Should().HaveCount(3); // FOMC, CPI, NFP from test data
        
        // Should be ordered by timestamp
        for (int i = 1; i < events.Count; i++)
        {
            events[i].Ts.Should().BeOnOrAfter(events[i - 1].Ts);
        }
    }

    [Fact]
    public void GetEvents_NoEventsInRange_ShouldReturnEmptyList()
    {
        // Arrange
        var calendar = new CsvCalendar(_testCsvPath, "America/New_York");
        var startDate = DateOnly.FromDateTime(new DateTime(2024, 1, 1));
        var endDate = DateOnly.FromDateTime(new DateTime(2024, 1, 31));

        // Act
        var events = calendar.GetEvents(startDate, endDate);

        // Assert
        events.Should().BeEmpty();
    }

    [Theory]
    [InlineData("FOMC", "2024-02-01 14:00:00")]
    [InlineData("CPI", "2024-02-02 08:30:00")]
    [InlineData("NFP", "2024-02-03 08:30:00")]
    public void GetEvents_ShouldContainSpecificEvents(string expectedKind, string expectedTimestamp)
    {
        // Arrange
        var calendar = new CsvCalendar(_testCsvPath, "America/New_York");
        var startDate = DateOnly.FromDateTime(new DateTime(2024, 2, 1));
        var endDate = DateOnly.FromDateTime(new DateTime(2024, 2, 3));
        var expectedTime = DateTime.Parse(expectedTimestamp);

        // Act
        var events = calendar.GetEvents(startDate, endDate);

        // Assert
        events.Should().Contain(evt => 
            evt.Kind == expectedKind && 
            Math.Abs((evt.Ts - expectedTime).TotalMinutes) < 1); // Allow 1 minute tolerance for UTC conversion
    }

    [Fact]
    public void Constructor_EmptyCalendarFile_ShouldHandleGracefully()
    {
        // Arrange
        var emptyPath = Path.Combine(_testDataDir, "empty_calendar.csv");
        File.WriteAllText(emptyPath, "ts,kind\n"); // Header only

        // Act
        var calendar = new CsvCalendar(emptyPath, "America/New_York");
        var nextEvent = calendar.NextEventAfter(DateTime.Now);
        var events = calendar.GetEvents(DateOnly.MinValue, DateOnly.MaxValue);

        // Assert
        nextEvent.Should().BeNull();
        events.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_InvalidFile_ShouldThrowException()
    {
        // Arrange
        var invalidPath = Path.Combine(_testDataDir, "nonexistent_calendar.csv");

        // Act & Assert
        var act = () => new CsvCalendar(invalidPath, "America/New_York");
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void GetEvents_EdgeDates_ShouldIncludeBoundaryEvents()
    {
        // Arrange
        var calendar = new CsvCalendar(_testCsvPath, "America/New_York");
        var startDate = DateOnly.FromDateTime(new DateTime(2024, 2, 1));
        var endDate = DateOnly.FromDateTime(new DateTime(2024, 2, 1));

        // Act
        var events = calendar.GetEvents(startDate, endDate);

        // Assert
        events.Should().Contain(evt => evt.Kind == "FOMC");
    }

    [Fact]
    public void EventOrdering_ShouldBeChronological()
    {
        // Arrange
        CreateUnorderedCalendarFile(); // Create file with events in random order
        var unorderedPath = Path.Combine(_testDataDir, "unordered_calendar.csv");
        var calendar = new CsvCalendar(unorderedPath, "America/New_York");

        // Act
        var events = calendar.GetEvents(DateOnly.MinValue, DateOnly.MaxValue);

        // Assert
        events.Should().NotBeEmpty();
        for (int i = 1; i < events.Count; i++)
        {
            events[i].Ts.Should().BeOnOrAfter(events[i - 1].Ts);
        }
    }

    [Theory]
    [InlineData("America/New_York")]
    [InlineData("Europe/London")]
    [InlineData("UTC")]
    public void Constructor_DifferentTimezones_ShouldAcceptTimezone(string timezone)
    {
        // Act & Assert
        var act = () => new CsvCalendar(_testCsvPath, timezone);
        act.Should().NotThrow();
    }

    [Fact]
    public void EventScheduling_ShouldFollowTypicalSchedule()
    {
        // Arrange
        var calendar = new CsvCalendar(_testCsvPath, "America/New_York");
        var events = calendar.GetEvents(DateOnly.MinValue, DateOnly.MaxValue);

        // Act & Assert
        var fomcEvent = events.FirstOrDefault(e => e.Kind == "FOMC");
        var cpiEvent = events.FirstOrDefault(e => e.Kind == "CPI");
        var nfpEvent = events.FirstOrDefault(e => e.Kind == "NFP");

        // FOMC typically at 2 PM ET
        fomcEvent?.Ts.Hour.Should().Be(14);
        
        // CPI typically at 8:30 AM ET
        cpiEvent?.Ts.Hour.Should().Be(8);
        cpiEvent?.Ts.Minute.Should().Be(30);
        
        // NFP typically at 8:30 AM ET
        nfpEvent?.Ts.Hour.Should().Be(8);
        nfpEvent?.Ts.Minute.Should().Be(30);
    }

    [Fact]
    public void HighImpactEvents_ShouldBeIncluded()
    {
        // Arrange
        var calendar = new CsvCalendar(_testCsvPath, "America/New_York");
        var events = calendar.GetEvents(DateOnly.MinValue, DateOnly.MaxValue);

        // Act
        var eventKinds = events.Select(e => e.Kind).ToHashSet();

        // Assert - Should include major market-moving events
        eventKinds.Should().Contain("FOMC");  // Federal Reserve announcements
        eventKinds.Should().Contain("CPI");   // Inflation data
        eventKinds.Should().Contain("NFP");   // Employment data
    }

    private void CreateTestCalendarFile()
    {
        var csvContent = "ts,kind\n";
        
        // Add some typical high-impact economic events
        csvContent += "2024-02-01 14:00:00,FOMC\n";      // Fed announcement
        csvContent += "2024-02-02 08:30:00,CPI\n";       // Inflation data
        csvContent += "2024-02-03 08:30:00,NFP\n";       // Jobs report
        
        File.WriteAllText(_testCsvPath, csvContent);
    }

    private void CreateUnorderedCalendarFile()
    {
        var csvContent = "ts,kind\n";
        
        // Add events in non-chronological order
        csvContent += "2024-02-03 08:30:00,NFP\n";       // Jobs report (last)
        csvContent += "2024-02-01 14:00:00,FOMC\n";      // Fed announcement (first)
        csvContent += "2024-02-02 08:30:00,CPI\n";       // Inflation data (middle)
        csvContent += "2024-02-01 10:00:00,PPI\n";       // Earlier same day
        
        File.WriteAllText(Path.Combine(_testDataDir, "unordered_calendar.csv"), csvContent);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDataDir))
        {
            Directory.Delete(_testDataDir, true);
        }
    }
}