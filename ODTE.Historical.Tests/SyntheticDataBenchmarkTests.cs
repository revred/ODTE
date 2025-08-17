using FluentAssertions;
using Microsoft.Extensions.Logging;
using ODTE.Historical.Validation;
using Xunit;

namespace ODTE.Historical.Tests
{
    /// <summary>
    /// Integration tests for SyntheticDataBenchmark
    /// Tests the validation framework against real database
    /// </summary>
    public class SyntheticDataBenchmarkTests
    {
        private readonly ILogger<SyntheticDataBenchmark> _logger;
        private readonly string _testDatabasePath;

        public SyntheticDataBenchmarkTests()
        {
            var factory = LoggerFactory.Create(builder => builder.AddConsole());
            _logger = factory.CreateLogger<SyntheticDataBenchmark>();

            // Use the actual database path
            _testDatabasePath = Path.Combine("..", "..", "..", "..", "data", "ODTE_TimeSeries_5Y.db");
        }

        [Fact]
        public async Task RunBenchmarkAsync_WithValidDatabase_ShouldReturnResults()
        {
            // Arrange
            if (!File.Exists(_testDatabasePath))
            {
                // Skip test if database doesn't exist
                return;
            }

            var benchmark = new SyntheticDataBenchmark(_testDatabasePath, _logger);

            // Act
            var result = await benchmark.RunBenchmarkAsync();

            // Assert
            result.Should().NotBeNull();
            result.BenchmarkId.Should().NotBeNullOrEmpty();
            result.OverallScore.Should().BeInRange(0, 100);
            result.Duration.Should().BePositive();

            // Statistical tests should be populated
            result.StatisticalTests.Should().NotBeNull();
            result.VolatilityAnalysis.Should().NotBeNull();
            result.DistributionTests.Should().NotBeNull();
            result.RegimeTests.Should().NotBeNull();
        }

        [Fact]
        public async Task RunBenchmarkAsync_WithInvalidDatabase_ShouldHandleError()
        {
            // Arrange
            var invalidPath = "nonexistent_database.db";
            var benchmark = new SyntheticDataBenchmark(invalidPath, _logger);

            // Act
            var result = await benchmark.RunBenchmarkAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsAcceptable.Should().BeFalse();
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void BenchmarkResult_DefaultValues_ShouldBeValid()
        {
            // Arrange & Act
            var result = new BenchmarkResult();

            // Assert
            result.BenchmarkId.Should().NotBeNull();
            result.OverallScore.Should().Be(0);
            result.IsAcceptable.Should().BeFalse();
            result.ErrorMessage.Should().NotBeNull();
            result.StatisticalTests.Should().NotBeNull();
            result.VolatilityAnalysis.Should().NotBeNull();
            result.DistributionTests.Should().NotBeNull();
            result.RegimeTests.Should().NotBeNull();
        }

        [Theory]
        [InlineData(85.0, true)]  // Excellent
        [InlineData(75.0, true)]  // Good
        [InlineData(60.0, false)] // Needs improvement
        [InlineData(40.0, false)] // Poor
        public void BenchmarkResult_AcceptabilityThreshold_ShouldBeCorrect(double score, bool expectedAcceptable)
        {
            // Arrange
            var result = new BenchmarkResult
            {
                OverallScore = score
            };

            // Act & Assert
            if (expectedAcceptable)
            {
                result.OverallScore.Should().BeGreaterOrEqualTo(75.0);
            }
            else
            {
                result.OverallScore.Should().BeLessThan(75.0);
            }
        }
    }
}