using System;
using System.Linq;
using FluentAssertions;
using ODTE.Contracts.Data;
using Xunit;

namespace ODTE.Contracts.Tests.Data
{
    public class DateRangeTests
    {
        private readonly DateTime _testStartDate = new DateTime(2023, 1, 1);
        private readonly DateTime _testEndDate = new DateTime(2023, 12, 31);

        [Fact]
        public void Constructor_DefaultConstructor_ShouldCreateEmptyDateRange()
        {
            // Arrange & Act
            var dateRange = new DateRange();
            
            // Assert
            dateRange.StartDate.Should().Be(default(DateTime));
            dateRange.EndDate.Should().Be(default(DateTime));
        }

        [Fact]
        public void StartDate_SetValue_ShouldUpdateStartDateAndStartAlias()
        {
            // Arrange
            var dateRange = new DateRange();
            
            // Act
            dateRange.StartDate = _testStartDate;
            
            // Assert
            dateRange.StartDate.Should().Be(_testStartDate);
            dateRange.Start.Should().Be(_testStartDate);
        }

        [Fact]
        public void Start_SetValue_ShouldUpdateStartDateAndStartAlias()
        {
            // Arrange
            var dateRange = new DateRange();
            
            // Act
            dateRange.Start = _testStartDate;
            
            // Assert
            dateRange.Start.Should().Be(_testStartDate);
            dateRange.StartDate.Should().Be(_testStartDate);
        }

        [Fact]
        public void EndDate_SetValue_ShouldUpdateEndDateAndEndAlias()
        {
            // Arrange
            var dateRange = new DateRange();
            
            // Act
            dateRange.EndDate = _testEndDate;
            
            // Assert
            dateRange.EndDate.Should().Be(_testEndDate);
            dateRange.End.Should().Be(_testEndDate);
        }

        [Fact]
        public void End_SetValue_ShouldUpdateEndDateAndEndAlias()
        {
            // Arrange
            var dateRange = new DateRange();
            
            // Act
            dateRange.End = _testEndDate;
            
            // Assert
            dateRange.End.Should().Be(_testEndDate);
            dateRange.EndDate.Should().Be(_testEndDate);
        }

        [Fact]
        public void Days_WithValidRange_ShouldReturnCorrectDayCount()
        {
            // Arrange
            var dateRange = new DateRange
            {
                StartDate = new DateTime(2023, 1, 1),
                EndDate = new DateTime(2023, 1, 10)
            };
            
            // Act
            var days = dateRange.Days;
            
            // Assert
            days.Should().Be(10); // 10 days including both start and end dates
        }

        [Fact]
        public void Days_WithSameStartAndEndDate_ShouldReturnOne()
        {
            // Arrange
            var singleDate = new DateTime(2023, 1, 1);
            var dateRange = new DateRange
            {
                StartDate = singleDate,
                EndDate = singleDate
            };
            
            // Act
            var days = dateRange.Days;
            
            // Assert
            days.Should().Be(1);
        }

        [Fact]
        public void Days_WithEndDateBeforeStartDate_ShouldReturnNegativeValue()
        {
            // Arrange
            var dateRange = new DateRange
            {
                StartDate = new DateTime(2023, 1, 10),
                EndDate = new DateTime(2023, 1, 1)
            };
            
            // Act
            var days = dateRange.Days;
            
            // Assert
            days.Should().Be(-8); // -9 days + 1 = -8
        }

        [Fact]
        public void Contains_DateWithinRange_ShouldReturnTrue()
        {
            // Arrange
            var dateRange = new DateRange
            {
                StartDate = new DateTime(2023, 1, 1),
                EndDate = new DateTime(2023, 12, 31)
            };
            var testDate = new DateTime(2023, 6, 15);
            
            // Act
            var contains = dateRange.Contains(testDate);
            
            // Assert
            contains.Should().BeTrue();
        }

        [Fact]
        public void Contains_DateEqualToStartDate_ShouldReturnTrue()
        {
            // Arrange
            var startDate = new DateTime(2023, 1, 1);
            var dateRange = new DateRange
            {
                StartDate = startDate,
                EndDate = new DateTime(2023, 12, 31)
            };
            
            // Act
            var contains = dateRange.Contains(startDate);
            
            // Assert
            contains.Should().BeTrue();
        }

        [Fact]
        public void Contains_DateEqualToEndDate_ShouldReturnTrue()
        {
            // Arrange
            var endDate = new DateTime(2023, 12, 31);
            var dateRange = new DateRange
            {
                StartDate = new DateTime(2023, 1, 1),
                EndDate = endDate
            };
            
            // Act
            var contains = dateRange.Contains(endDate);
            
            // Assert
            contains.Should().BeTrue();
        }

        [Fact]
        public void Contains_DateBeforeStartDate_ShouldReturnFalse()
        {
            // Arrange
            var dateRange = new DateRange
            {
                StartDate = new DateTime(2023, 1, 1),
                EndDate = new DateTime(2023, 12, 31)
            };
            var testDate = new DateTime(2022, 12, 31);
            
            // Act
            var contains = dateRange.Contains(testDate);
            
            // Assert
            contains.Should().BeFalse();
        }

        [Fact]
        public void Contains_DateAfterEndDate_ShouldReturnFalse()
        {
            // Arrange
            var dateRange = new DateRange
            {
                StartDate = new DateTime(2023, 1, 1),
                EndDate = new DateTime(2023, 12, 31)
            };
            var testDate = new DateTime(2024, 1, 1);
            
            // Act
            var contains = dateRange.Contains(testDate);
            
            // Assert
            contains.Should().BeFalse();
        }

        [Fact]
        public void GetTradingDays_WithWeekdaysOnly_ShouldExcludeWeekends()
        {
            // Arrange
            var dateRange = new DateRange
            {
                StartDate = new DateTime(2023, 1, 1), // Sunday
                EndDate = new DateTime(2023, 1, 7)   // Saturday
            };
            
            // Act
            var tradingDays = dateRange.GetTradingDays().ToList();
            
            // Assert
            tradingDays.Should().HaveCount(5); // Monday through Friday
            tradingDays.Should().NotContain(new DateTime(2023, 1, 1)); // Sunday
            tradingDays.Should().NotContain(new DateTime(2023, 1, 7)); // Saturday
        }

        [Fact]
        public void GetTradingDays_WithSingleWeekday_ShouldReturnSingleDay()
        {
            // Arrange
            var monday = new DateTime(2023, 1, 2); // Monday
            var dateRange = new DateRange
            {
                StartDate = monday,
                EndDate = monday
            };
            
            // Act
            var tradingDays = dateRange.GetTradingDays().ToList();
            
            // Assert
            tradingDays.Should().HaveCount(1);
            tradingDays[0].Should().Be(monday);
        }

        [Fact]
        public void GetTradingDays_WithWeekendsOnly_ShouldReturnEmptyList()
        {
            // Arrange
            var dateRange = new DateRange
            {
                StartDate = new DateTime(2023, 1, 7), // Saturday
                EndDate = new DateTime(2023, 1, 8)   // Sunday
            };
            
            // Act
            var tradingDays = dateRange.GetTradingDays().ToList();
            
            // Assert
            tradingDays.Should().BeEmpty();
        }

        [Fact]
        public void GetTradingDays_WithEndDateBeforeStartDate_ShouldReturnEmptyList()
        {
            // Arrange
            var dateRange = new DateRange
            {
                StartDate = new DateTime(2023, 1, 10),
                EndDate = new DateTime(2023, 1, 1)
            };
            
            // Act
            var tradingDays = dateRange.GetTradingDays().ToList();
            
            // Assert
            tradingDays.Should().BeEmpty();
        }

        [Fact]
        public void IsValid_WithValidRange_ShouldReturnTrue()
        {
            // Arrange
            var dateRange = new DateRange
            {
                StartDate = new DateTime(2023, 1, 1),
                EndDate = new DateTime(2023, 12, 31)
            };
            
            // Act
            var isValid = dateRange.IsValid;
            
            // Assert
            isValid.Should().BeTrue();
        }

        [Fact]
        public void IsValid_WithEqualDates_ShouldReturnTrue()
        {
            // Arrange
            var sameDate = new DateTime(2023, 1, 1);
            var dateRange = new DateRange
            {
                StartDate = sameDate,
                EndDate = sameDate
            };
            
            // Act
            var isValid = dateRange.IsValid;
            
            // Assert
            isValid.Should().BeTrue();
        }

        [Fact]
        public void IsValid_WithEndDateBeforeStartDate_ShouldReturnFalse()
        {
            // Arrange
            var dateRange = new DateRange
            {
                StartDate = new DateTime(2023, 12, 31),
                EndDate = new DateTime(2023, 1, 1)
            };
            
            // Act
            var isValid = dateRange.IsValid;
            
            // Assert
            isValid.Should().BeFalse();
        }

        [Fact]
        public void ToString_WithValidRange_ShouldReturnFormattedString()
        {
            // Arrange
            var dateRange = new DateRange
            {
                StartDate = new DateTime(2023, 1, 1),
                EndDate = new DateTime(2023, 1, 10)
            };
            
            // Act
            var result = dateRange.ToString();
            
            // Assert
            result.Should().Be("2023-01-01 to 2023-01-10 (10 days)");
        }

        [Fact]
        public void ToString_WithSingleDay_ShouldReturnFormattedString()
        {
            // Arrange
            var singleDate = new DateTime(2023, 1, 1);
            var dateRange = new DateRange
            {
                StartDate = singleDate,
                EndDate = singleDate
            };
            
            // Act
            var result = dateRange.ToString();
            
            // Assert
            result.Should().Be("2023-01-01 to 2023-01-01 (1 days)");
        }

        [Fact]
        public void ToString_WithInvalidRange_ShouldStillReturnFormattedString()
        {
            // Arrange
            var dateRange = new DateRange
            {
                StartDate = new DateTime(2023, 1, 10),
                EndDate = new DateTime(2023, 1, 1)
            };
            
            // Act
            var result = dateRange.ToString();
            
            // Assert
            result.Should().Be("2023-01-10 to 2023-01-01 (-8 days)");
        }

        [Theory]
        [InlineData("2023-01-01", "2023-01-31", 31)]
        [InlineData("2023-02-01", "2023-02-28", 28)]
        [InlineData("2024-02-01", "2024-02-29", 29)] // Leap year
        [InlineData("2023-01-01", "2023-01-01", 1)]
        public void Days_WithVariousRanges_ShouldReturnCorrectCount(string startDateStr, string endDateStr, int expectedDays)
        {
            // Arrange
            var dateRange = new DateRange
            {
                StartDate = DateTime.Parse(startDateStr),
                EndDate = DateTime.Parse(endDateStr)
            };
            
            // Act
            var days = dateRange.Days;
            
            // Assert
            days.Should().Be(expectedDays);
        }

        [Theory]
        [InlineData("2023-01-01", "2023-01-07", 5)] // Sunday to Saturday = 5 weekdays
        [InlineData("2023-01-02", "2023-01-06", 5)] // Monday to Friday = 5 weekdays
        [InlineData("2023-01-03", "2023-01-03", 1)] // Single Tuesday = 1 weekday
        [InlineData("2023-01-07", "2023-01-08", 0)] // Saturday to Sunday = 0 weekdays
        public void GetTradingDays_WithVariousRanges_ShouldReturnCorrectCount(string startDateStr, string endDateStr, int expectedTradingDays)
        {
            // Arrange
            var dateRange = new DateRange
            {
                StartDate = DateTime.Parse(startDateStr),
                EndDate = DateTime.Parse(endDateStr)
            };
            
            // Act
            var tradingDays = dateRange.GetTradingDays().ToList();
            
            // Assert
            tradingDays.Should().HaveCount(expectedTradingDays);
        }
    }
}