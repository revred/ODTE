using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ODTE.Historical.DataProviders;

namespace ODTE.Historical.Examples;

/// <summary>
/// Example demonstrating how to use the multi-source data fetcher
/// </summary>
public class MultiSourceDataFetcherExample
{
    public static async Task RunExample()
    {
        Console.WriteLine("🚀 ODTE Multi-Source Data Fetcher Example");
        Console.WriteLine("==========================================");
        
        // Set up logging
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        
        var logger = loggerFactory.CreateLogger<EnhancedHistoricalDataFetcher>();
        
        // Initialize the enhanced data fetcher
        using var fetcher = new EnhancedHistoricalDataFetcher(logger: logger);
        
        Console.WriteLine("\n📊 Checking Provider Status...");
        var providers = await fetcher.GetProviderStatusAsync();
        
        if (providers.Count == 0)
        {
            Console.WriteLine("⚠️  No data providers configured!");
            Console.WriteLine("   Set environment variables:");
            Console.WriteLine("   - POLYGON_API_KEY=your_key_here");
            Console.WriteLine("   - ALPHA_VANTAGE_API_KEY=your_key_here");
            Console.WriteLine("   - TWELVE_DATA_API_KEY=your_key_here");
            Console.WriteLine();
            Console.WriteLine("🔧 Running with mock data for demonstration...");
            await RunMockExample();
            return;
        }
        
        foreach (var provider in providers)
        {
            var status = provider.IsHealthy ? "✅ Healthy" : "❌ Unhealthy";
            Console.WriteLine($"   {provider.ProviderName}: {status} " +
                            $"({provider.RequestsRemaining} requests remaining)");
        }
        
        Console.WriteLine("\n📈 Fetching market data...");
        
        // Example 1: Get data quality report
        var qualityReport = await fetcher.ValidateDataQualityAsync("SPY", DateTime.Today.AddDays(-1));
        if (qualityReport.IsValid)
        {
            Console.WriteLine($"✅ Data Quality Report for {qualityReport.Symbol}:");
            Console.WriteLine($"   Sources: {string.Join(", ", qualityReport.Sources)}");
            Console.WriteLine($"   Underlying Price: ${qualityReport.UnderlyingPrice:F2}");
            Console.WriteLine($"   Options Available: {qualityReport.TotalOptions}");
            
            if (qualityReport.QualityMetrics != null)
            {
                Console.WriteLine($"   Avg Bid-Ask Spread: {qualityReport.QualityMetrics.AverageBidAskSpread:P2}");
                Console.WriteLine($"   Avg Implied Vol: {qualityReport.QualityMetrics.AverageImpliedVolatility:P1}");
            }
        }
        
        // Example 2: Fetch historical data for multiple days
        Console.WriteLine("\n📥 Fetching historical data range...");
        
        var result = await fetcher.FetchAndConsolidateDataAsync(
            "SPY", 
            DateTime.Today.AddDays(-5), 
            DateTime.Today.AddDays(-1));
        
        if (result.Success)
        {
            Console.WriteLine($"✅ Data fetch completed successfully!");
            Console.WriteLine($"   Duration: {result.Duration.TotalSeconds:F1} seconds");
            Console.WriteLine($"   Success Rate: {result.SuccessRate:P1}");
            Console.WriteLine($"   Days Processed: {result.TotalDaysProcessed}");
            Console.WriteLine($"   Failed Days: {result.FailedDays.Count}");
            
            if (result.FailedDays.Any())
            {
                Console.WriteLine("   Failed dates:");
                foreach (var failedDate in result.FailedDays)
                {
                    Console.WriteLine($"     - {failedDate:yyyy-MM-dd}");
                }
            }
        }
        else
        {
            Console.WriteLine($"❌ Data fetch failed: {result.ErrorMessage}");
        }
        
        Console.WriteLine("\n🔄 Final provider status:");
        var finalProviders = await fetcher.GetProviderStatusAsync();
        foreach (var provider in finalProviders)
        {
            Console.WriteLine($"   {provider.ProviderName}:");
            Console.WriteLine($"     Health: {(provider.IsHealthy ? "✅" : "❌")}");
            Console.WriteLine($"     Success Rate: {provider.SuccessRate:P1}");
            Console.WriteLine($"     Total Requests: {provider.TotalRequests}");
            Console.WriteLine($"     Avg Response Time: {provider.AverageResponseTime:F0}ms");
        }
        
        Console.WriteLine("\n🎉 Example completed!");
    }
    
    private static async Task RunMockExample()
    {
        Console.WriteLine("\n🧪 Mock Data Example:");
        
        // Create a multi-source fetcher with mock provider
        using var fetcher = new MultiSourceDataFetcher();
        fetcher.AddProvider(new MockDataProvider());
        
        // Test intraday bars
        var bars = await fetcher.GetIntradayBarsAsync(
            "SPY", 
            DateTime.Today.AddDays(-1), 
            DateTime.Today);
        
        Console.WriteLine($"✅ Retrieved {bars.Count} mock data bars");
        foreach (var bar in bars)
        {
            Console.WriteLine($"   {bar.Timestamp:HH:mm:ss} O:{bar.Open:F2} H:{bar.High:F2} " +
                            $"L:{bar.Low:F2} C:{bar.Close:F2} V:{bar.Volume:N0}");
        }
        
        // Test options chain
        var optionsData = await fetcher.GetOptionsChainAsync("SPY", DateTime.Today);
        if (optionsData != null)
        {
            Console.WriteLine($"✅ Retrieved options chain:");
            Console.WriteLine($"   Underlying: ${optionsData.UnderlyingPrice:F2}");
            Console.WriteLine($"   Calls: {optionsData.Calls.Count}, Puts: {optionsData.Puts.Count}");
        }
    }
}

/// <summary>
/// Simple mock data provider for demonstration
/// </summary>
internal class MockDataProvider : IOptionsDataProvider
{
    public string ProviderName => "Mock Provider (Demo)";
    public int Priority => 1;
    public bool IsAvailable => true;
    
    public Task<OptionsChainData?> GetOptionsChainAsync(string symbol, DateTime date, CancellationToken cancellationToken = default)
    {
        var chainData = new OptionsChainData
        {
            Symbol = symbol,
            Date = date,
            DataSource = ProviderName,
            UnderlyingPrice = 150m + (decimal)(new Random().NextDouble() * 10 - 5), // $145-$155
            Calls = GenerateOptions(symbol, "CALL", 5),
            Puts = GenerateOptions(symbol, "PUT", 5)
        };
        
        return Task.FromResult<OptionsChainData?>(chainData);
    }
    
    public Task<List<MarketDataBar>> GetIntradayBarsAsync(string symbol, DateTime startDate, DateTime endDate, TimeSpan? interval = null, CancellationToken cancellationToken = default)
    {
        var bars = new List<MarketDataBar>();
        var random = new Random();
        var currentTime = startDate.Date.AddHours(9.5); // 9:30 AM
        var endTime = startDate.Date.AddHours(16); // 4:00 PM
        var price = 150.0 + (random.NextDouble() * 10 - 5); // Start around $150
        
        while (currentTime < endTime)
        {
            var change = (random.NextDouble() - 0.5) * 0.5; // Small random moves
            price += change;
            
            var high = price + Math.Abs(random.NextGaussian()) * 0.2;
            var low = price - Math.Abs(random.NextGaussian()) * 0.2;
            var close = low + (high - low) * random.NextDouble();
            
            bars.Add(new MarketDataBar
            {
                Timestamp = currentTime,
                Open = price,
                High = high,
                Low = low,
                Close = close,
                Volume = random.Next(100000, 1000000),
                VWAP = (price + high + low + close) / 4
            });
            
            price = close;
            currentTime = currentTime.AddMinutes(1);
        }
        
        return Task.FromResult(bars);
    }
    
    public Task<ProviderHealthStatus> CheckHealthAsync()
    {
        return Task.FromResult(new ProviderHealthStatus
        {
            IsHealthy = true,
            LastCheck = DateTime.UtcNow,
            ResponseTimeMs = 10,
            ConsecutiveFailures = 0
        });
    }
    
    public RateLimitStatus GetRateLimitStatus()
    {
        return new RateLimitStatus
        {
            RequestsRemaining = 1000,
            RequestsPerMinute = 1000,
            ResetTime = DateTime.UtcNow.AddMinutes(1),
            IsThrottled = false
        };
    }
    
    private List<OptionsContract> GenerateOptions(string symbol, string type, int count)
    {
        var options = new List<OptionsContract>();
        var random = new Random();
        var basePrice = 150m;
        
        for (int i = 0; i < count; i++)
        {
            var strike = basePrice + (i - 2) * 5; // Strikes from $140 to $160
            var isITM = (type == "CALL" && strike < basePrice) || (type == "PUT" && strike > basePrice);
            
            options.Add(new OptionsContract
            {
                Symbol = $"{symbol}_{type}_{strike:F0}",
                Strike = strike,
                Type = type,
                ExpirationDate = DateTime.Today.AddDays(7), // Weekly expiration
                Bid = (decimal)(isITM ? 2 + random.NextDouble() * 3 : 0.5 + random.NextDouble() * 2),
                Ask = 0, // Will be set based on bid
                Volume = random.Next(10, 1000),
                OpenInterest = random.Next(100, 5000),
                ImpliedVolatility = (decimal)(0.15 + random.NextDouble() * 0.3) // 15%-45% IV
            });
            
            // Set ask based on bid with realistic spread
            var lastOption = options.Last();
            lastOption.Ask = lastOption.Bid * 1.05m; // 5% spread
            lastOption.Last = (lastOption.Bid + lastOption.Ask) / 2;
        }
        
        return options;
    }
}

// Extension method for Gaussian random numbers
internal static class RandomExtensions
{
    public static double NextGaussian(this Random random)
    {
        var u1 = 1.0 - random.NextDouble();
        var u2 = 1.0 - random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }
}