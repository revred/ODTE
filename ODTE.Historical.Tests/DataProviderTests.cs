using ODTE.Historical.DataProviders;
using Xunit;

namespace ODTE.Historical.Tests;

public class DataProviderTests
{
    [Fact]
    public void RateLimiter_ShouldRespectLimits()
    {
        // Arrange
        var rateLimiter = new RateLimiter(2); // 2 requests per minute
        var startTime = DateTime.UtcNow;

        // Act & Assert
        var task1 = rateLimiter.WaitAsync();
        var task2 = rateLimiter.WaitAsync();

        Assert.True(task1.IsCompleted, "First request should be immediate");
        Assert.True(task2.IsCompleted, "Second request should be immediate");

        // Third request should be delayed
        var task3 = rateLimiter.WaitAsync();
        Assert.False(task3.IsCompleted, "Third request should be delayed");

        var status = rateLimiter.GetStatus();
        Assert.Equal(0, status.RequestsRemaining);
        Assert.Equal(2, status.RequestsPerMinute);
    }

    [Fact]
    public void RateLimiter_ShouldHandleThrottling()
    {
        // Arrange
        var rateLimiter = new RateLimiter(5);

        // Act
        rateLimiter.SetThrottled(TimeSpan.FromSeconds(1));
        var status = rateLimiter.GetStatus();

        // Assert
        Assert.True(status.IsThrottled);
        Assert.NotNull(status.RetryAfter);
        Assert.True(status.RetryAfter.Value.TotalSeconds > 0);
    }

    [Fact]
    public void DataCache_ShouldStoreAndRetrieve()
    {
        // Arrange
        using var cache = new DataCache(TimeSpan.FromMinutes(5));
        var testData = "test data";
        var key = "test_key";

        // Act
        cache.Set(key, testData);
        var retrieved = cache.Get<string>(key);

        // Assert
        Assert.Equal(testData, retrieved);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public Task DataCache_ShouldExpireItems()
    {
        // Arrange
        using var cache = new DataCache(TimeSpan.FromMilliseconds(50));
        var testData = "test data";
        var key = "test_key";

        // Act
        cache.Set(key, testData);

        // Wait for expiration
        Task.Delay(100);

        var retrieved = cache.Get<string>(key);

        // Assert
        Assert.Null(retrieved);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task MultiSourceDataFetcher_ShouldHandleNoProviders()
    {
        // Arrange
        using var fetcher = new MultiSourceDataFetcher();

        // Act
        var result = await fetcher.GetIntradayBarsAsync("SPY", DateTime.Today, DateTime.Today.AddDays(1));

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Any());
    }

    [Fact]
    public async Task MultiSourceDataFetcher_ShouldReportProviderStatus()
    {
        // Arrange
        using var fetcher = new MultiSourceDataFetcher();

        // Add a mock provider
        var mockProvider = new MockDataProvider();
        fetcher.AddProvider(mockProvider);

        // Act
        var statuses = await fetcher.GetProviderStatusAsync();

        // Assert
        Assert.Single(statuses);
        Assert.Equal(mockProvider.ProviderName, statuses[0].ProviderName);
        Assert.Equal(mockProvider.Priority, statuses[0].Priority);
    }

    [Fact]
    public async Task EnhancedHistoricalDataFetcher_ShouldInitializeWithoutAPIKeys()
    {
        // Arrange & Act
        using var fetcher = new EnhancedHistoricalDataFetcher();
        var statuses = await fetcher.GetProviderStatusAsync();

        // Assert
        Assert.NotNull(statuses);
        // Should work even with no providers (would use fallback mechanisms)
    }

    [Fact]
    public async Task EnhancedHistoricalDataFetcher_ShouldValidateDataQuality()
    {
        // Arrange
        using var fetcher = new EnhancedHistoricalDataFetcher();

        // Act
        var report = await fetcher.ValidateDataQualityAsync("SPY", DateTime.Today.AddDays(-1));

        // Assert
        Assert.NotNull(report);
        Assert.Equal("SPY", report.Symbol);
        Assert.True(report.ValidationTime > DateTime.MinValue);
    }

    [Fact]
    public void OptionsContract_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var contract = new OptionsContract();

        // Assert
        Assert.Equal(string.Empty, contract.Symbol);
        Assert.Equal(0, contract.Strike);
        Assert.Equal(0, contract.Volume);
        Assert.Equal(0, contract.OpenInterest);
    }

    [Fact]
    public void OptionsChainData_ShouldInitializeWithEmptyLists()
    {
        // Arrange & Act
        var chainData = new OptionsChainData();

        // Assert
        Assert.NotNull(chainData.Calls);
        Assert.NotNull(chainData.Puts);
        Assert.Empty(chainData.Calls);
        Assert.Empty(chainData.Puts);
    }
}

/// <summary>
/// Mock data provider for testing
/// </summary>
internal class MockDataProvider : IOptionsDataProvider
{
    public string ProviderName => "Mock Provider";
    public int Priority => 99;
    public bool IsAvailable => true;

    public Task<OptionsChainData?> GetOptionsChainAsync(string symbol, DateTime date, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<OptionsChainData?>(new OptionsChainData
        {
            Symbol = symbol,
            Date = date,
            DataSource = ProviderName,
            UnderlyingPrice = 100m,
            Calls = new List<OptionsContract>
            {
                new OptionsContract
                {
                    Symbol = $"{symbol}_CALL_100",
                    Strike = 100,
                    Type = "CALL",
                    Bid = 1.00m,
                    Ask = 1.05m,
                    Volume = 100
                }
            },
            Puts = new List<OptionsContract>
            {
                new OptionsContract
                {
                    Symbol = $"{symbol}_PUT_100",
                    Strike = 100,
                    Type = "PUT",
                    Bid = 0.95m,
                    Ask = 1.00m,
                    Volume = 50
                }
            }
        });
    }

    public Task<List<MarketDataBar>> GetIntradayBarsAsync(string symbol, DateTime startDate, DateTime endDate, TimeSpan? interval = null, CancellationToken cancellationToken = default)
    {
        var bars = new List<MarketDataBar>
        {
            new MarketDataBar
            {
                Timestamp = startDate,
                Open = 100.0,
                High = 101.0,
                Low = 99.0,
                Close = 100.5,
                Volume = 1000000,
                VWAP = 100.125
            }
        };

        return Task.FromResult(bars);
    }

    public Task<ProviderHealthStatus> CheckHealthAsync()
    {
        return Task.FromResult(new ProviderHealthStatus
        {
            IsHealthy = true,
            LastCheck = DateTime.UtcNow,
            ResponseTimeMs = 50,
            ConsecutiveFailures = 0
        });
    }

    public RateLimitStatus GetRateLimitStatus()
    {
        return new RateLimitStatus
        {
            RequestsRemaining = 100,
            RequestsPerMinute = 100,
            ResetTime = DateTime.UtcNow.AddMinutes(1),
            IsThrottled = false
        };
    }
}