using FluentAssertions;
using ODTE.Historical.DistributedStorage;
using Xunit;

namespace ODTE.Historical.Tests;

/// <summary>
/// Test distributed SQLite storage system for commodities and options
/// Validates performance, file organization, and data integrity
/// </summary>
public class DistributedStorageTests : IDisposable
{
    private readonly string _testDataPath;
    private readonly DistributedDatabaseManager _dbManager;

    public DistributedStorageTests()
    {
        _testDataPath = Path.Combine(Path.GetTempPath(), $"distributed_test_{Guid.NewGuid()}");
        _dbManager = new DistributedDatabaseManager(_testDataPath);
    }

    [Fact]
    public async Task FileManager_ShouldGenerateCorrectPaths()
    {
        // Arrange
        var fileManager = new FileManager(_testDataPath);
        var testDate = new DateTime(2024, 1, 15);
        var expirationDate = new DateTime(2024, 1, 19);

        // Act
        var commodityPath = fileManager.GetCommodityPath("USO", testDate);
        var optionsPath = fileManager.GetOptionsPath("USO", expirationDate);

        // Assert
        commodityPath.Should().EndWith(@"commodities\oil\2024\01\USO_202401.db");
        optionsPath.Should().EndWith(@"options\oil\USO\2024\01\USO_OPT_20240119.db");

        Console.WriteLine($"✅ Commodity Path: {commodityPath}");
        Console.WriteLine($"✅ Options Path: {optionsPath}");
    }

    [Fact]
    public async Task DistributedStorage_ShouldStoreAndRetrieveCommodityData()
    {
        // Arrange
        var testData = GenerateTestCommodityData("USO", new DateTime(2024, 1, 1), 20);

        // Act - Store data
        await _dbManager.StoreCommodityDataAsync("USO", testData);

        // Retrieve data
        var retrievedData = await _dbManager.GetCommodityDataAsync("USO",
            new DateTime(2024, 1, 1), new DateTime(2024, 1, 31));

        // Assert
        retrievedData.Should().HaveCount(20);
        // Data integrity validated by count and order
        retrievedData.Should().BeInAscendingOrder(d => d.Timestamp);

        Console.WriteLine($"✅ Stored and retrieved {retrievedData.Count} USO data points");
        Console.WriteLine($"   Price range: ${retrievedData.Min(d => d.Low):F2} - ${retrievedData.Max(d => d.High):F2}");
    }

    [Fact]
    public async Task DistributedStorage_ShouldHandleMultipleMonths()
    {
        // Arrange - Generate data spanning 3 months
        var jan2024Data = GenerateTestCommodityData("USO", new DateTime(2024, 1, 1), 20);
        var feb2024Data = GenerateTestCommodityData("USO", new DateTime(2024, 2, 1), 19);
        var mar2024Data = GenerateTestCommodityData("USO", new DateTime(2024, 3, 1), 21);

        var allData = jan2024Data.Concat(feb2024Data).Concat(mar2024Data).ToList();

        // Act
        await _dbManager.StoreCommodityDataAsync("USO", allData);

        // Query across multiple months
        var retrievedData = await _dbManager.GetCommodityDataAsync("USO",
            new DateTime(2024, 1, 1), new DateTime(2024, 3, 31));

        // Assert
        retrievedData.Should().HaveCount(60);

        // Should have data from 3 separate monthly files
        var months = retrievedData.Select(d => d.Timestamp.Month).Distinct().ToList();
        months.Should().HaveCount(3);
        months.Should().Contain(new[] { 1, 2, 3 });

        Console.WriteLine($"✅ Successfully queried {retrievedData.Count} records across {months.Count} monthly files");
    }

    [Fact]
    public async Task DistributedStorage_ShouldStoreAndRetrieveOptionsData()
    {
        // Arrange
        var expirationDate = new DateTime(2024, 1, 19);
        var optionsChain = GenerateTestOptionsChain("USO", expirationDate, 80m);

        // Act
        await _dbManager.StoreOptionsChainAsync("USO", optionsChain);
        var retrievedChain = await _dbManager.GetOptionsChainAsync("USO", expirationDate);

        // Assert
        retrievedChain.Should().NotBeNull();
        retrievedChain.Symbol.Should().Be("USO");
        retrievedChain.ExpirationDate.Should().Be(expirationDate);
        retrievedChain.Options.Should().HaveCountGreaterThan(0);

        var calls = retrievedChain.Calls;
        var puts = retrievedChain.Puts;

        calls.Should().HaveCountGreaterThan(0);
        puts.Should().HaveCountGreaterThan(0);

        Console.WriteLine($"✅ Options Chain: {calls.Count} calls, {puts.Count} puts");
        Console.WriteLine($"   Strike range: ${retrievedChain.Options.Min(o => o.Strike)} - ${retrievedChain.Options.Max(o => o.Strike)}");
    }

    [Fact]
    public async Task DistributedStorage_ShouldHandleMultipleExpirations()
    {
        // Arrange
        var symbol = "USO";
        var expirations = new[]
        {
            new DateTime(2024, 1, 19),
            new DateTime(2024, 2, 16),
            new DateTime(2024, 3, 15)
        };

        // Store multiple options chains
        foreach (var expiration in expirations)
        {
            var chain = GenerateTestOptionsChain(symbol, expiration, 80m);
            await _dbManager.StoreOptionsChainAsync(symbol, chain);
        }

        // Act
        var availableExpirations = await _dbManager.GetAvailableExpirationsAsync(symbol,
            new DateTime(2024, 1, 1), new DateTime(2024, 3, 31));

        // Assert
        availableExpirations.Should().HaveCount(3);
        availableExpirations.Should().BeInAscendingOrder();

        foreach (var expiration in expirations)
        {
            availableExpirations.Should().Contain(expiration);
        }

        Console.WriteLine($"✅ Found {availableExpirations.Count} available expirations for {symbol}");
        foreach (var exp in availableExpirations)
        {
            Console.WriteLine($"   📅 {exp:yyyy-MM-dd}");
        }
    }

    [Fact]
    public async Task DistributedStorage_ShouldProvideStorageStatistics()
    {
        // Arrange
        var symbol = "USO";
        var commodityData = GenerateTestCommodityData(symbol, new DateTime(2024, 1, 1), 30);
        var optionsChain = GenerateTestOptionsChain(symbol, new DateTime(2024, 1, 19), 80m);

        // Act
        await _dbManager.StoreCommodityDataAsync(symbol, commodityData);
        await _dbManager.StoreOptionsChainAsync(symbol, optionsChain);

        var stats = _dbManager.GetStorageStats(symbol);

        // Assert
        stats.Should().NotBeNull();
        stats.Symbol.Should().Be(symbol);
        stats.CommodityFiles.Should().BeGreaterThan(0);
        stats.OptionsFiles.Should().BeGreaterThan(0);
        stats.TotalStorageBytes.Should().BeGreaterThan(0);

        Console.WriteLine($"✅ Storage Stats for {symbol}:");
        Console.WriteLine($"   📁 Commodity Files: {stats.CommodityFiles}");
        Console.WriteLine($"   📁 Options Files: {stats.OptionsFiles}");
        Console.WriteLine($"   💾 Total Storage: {stats.TotalStorageMB:F2} MB");
        Console.WriteLine($"   📊 Total Files: {stats.TotalFiles}");
    }

    [Fact]
    public async Task DistributedStorage_ShouldSupportParallelAccess()
    {
        // Arrange
        var symbols = new[] { "USO", "UCO", "SCO" };
        var testDate = new DateTime(2024, 1, 1);

        // Store data for multiple symbols
        foreach (var symbol in symbols)
        {
            var data = GenerateTestCommodityData(symbol, testDate, 20);
            await _dbManager.StoreCommodityDataAsync(symbol, data);
        }

        // Act - Parallel retrieval
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var tasks = symbols.Select(async symbol =>
        {
            return await _dbManager.GetCommodityDataAsync(symbol, testDate, testDate.AddDays(30));
        });

        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        results.Should().HaveCount(3);
        foreach (var result in results)
        {
            result.Should().HaveCount(20);
        }

        Console.WriteLine($"✅ Parallel access to {symbols.Length} symbols completed in {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"   Average: {stopwatch.ElapsedMilliseconds / (double)symbols.Length:F1}ms per symbol");
    }

    private List<MarketDataBar> GenerateTestCommodityData(string symbol, DateTime startDate, int days)
    {
        var data = new List<MarketDataBar>();
        var random = new Random(42);
        var price = 80m; // Starting oil price

        for (int i = 0; i < days; i++)
        {
            var date = startDate.AddDays(i);

            // Skip weekends
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                continue;

            // Simulate price movement
            var change = (decimal)(random.NextDouble() - 0.5) * 2; // +/- $1 max change
            price += change;
            price = Math.Max(50m, Math.Min(120m, price)); // Keep in reasonable range

            var volume = random.Next(1000000, 5000000);

            data.Add(new MarketDataBar
            {
                Timestamp = date,
                Open = (double)(price + (decimal)(random.NextDouble() - 0.5) * 0.5m),
                High = (double)(price + (decimal)random.NextDouble() * 1m),
                Low = (double)(price - (decimal)random.NextDouble() * 1m),
                Close = (double)price,
                Volume = volume,
                VWAP = (double)(price + (decimal)(random.NextDouble() - 0.5) * 0.2m)
            });
        }

        return data;
    }

    private OptionsChain GenerateTestOptionsChain(string symbol, DateTime expirationDate, decimal underlyingPrice)
    {
        var chain = new OptionsChain
        {
            Symbol = symbol,
            ExpirationDate = expirationDate,
            UnderlyingPrice = underlyingPrice,
            Options = new List<OptionContract>()
        };

        var random = new Random(42);
        var strikes = new List<decimal>();

        // Generate strikes around current price
        for (decimal strike = underlyingPrice - 10m; strike <= underlyingPrice + 10m; strike += 1m)
        {
            strikes.Add(strike);
        }

        foreach (var strike in strikes)
        {
            var dte = (expirationDate - DateTime.Today).Days;

            // Generate call option
            var call = new OptionContract
            {
                Symbol = $"{symbol}{expirationDate:yyMMdd}C{strike:00000000}",
                Type = OptionType.Call,
                Strike = strike,
                ExpirationDate = expirationDate,
                DaysToExpiration = dte,
                UnderlyingPrice = underlyingPrice
            };

            // Simple option pricing (mock)
            var callIntrinsic = Math.Max(0, underlyingPrice - strike);
            var callTimeValue = Math.Max(0.05m, (decimal)random.NextDouble() * 2m);
            call.Last = callIntrinsic + callTimeValue;
            call.Bid = call.Last - 0.05m;
            call.Ask = call.Last + 0.05m;
            call.Volume = random.Next(0, 1000);
            call.OpenInterest = random.Next(100, 5000);
            call.ImpliedVolatility = 0.30m + (decimal)(random.NextDouble() - 0.5) * 0.10m;
            call.Delta = Math.Max(0, Math.Min(1, 0.5m + (underlyingPrice - strike) / 20m));
            call.Gamma = 0.05m;
            call.Theta = -0.02m;
            call.Vega = 0.10m;

            chain.Options.Add(call);

            // Generate put option
            var put = new OptionContract
            {
                Symbol = $"{symbol}{expirationDate:yyMMdd}P{strike:00000000}",
                Type = OptionType.Put,
                Strike = strike,
                ExpirationDate = expirationDate,
                DaysToExpiration = dte,
                UnderlyingPrice = underlyingPrice
            };

            var putIntrinsic = Math.Max(0, strike - underlyingPrice);
            var putTimeValue = Math.Max(0.05m, (decimal)random.NextDouble() * 2m);
            put.Last = putIntrinsic + putTimeValue;
            put.Bid = put.Last - 0.05m;
            put.Ask = put.Last + 0.05m;
            put.Volume = random.Next(0, 1000);
            put.OpenInterest = random.Next(100, 5000);
            put.ImpliedVolatility = 0.30m + (decimal)(random.NextDouble() - 0.5) * 0.10m;
            put.Delta = Math.Min(0, Math.Max(-1, -0.5m + (underlyingPrice - strike) / 20m));
            put.Gamma = 0.05m;
            put.Theta = -0.02m;
            put.Vega = 0.10m;

            chain.Options.Add(put);
        }

        return chain;
    }

    public void Dispose()
    {
        _dbManager?.Dispose();

        if (Directory.Exists(_testDataPath))
        {
            Directory.Delete(_testDataPath, true);
        }
    }
}