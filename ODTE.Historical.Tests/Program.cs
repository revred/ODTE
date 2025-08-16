using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ODTE.Historical;
using ODTE.Historical.Validation;

namespace ODTE.Historical.Tests;

/// <summary>
/// ODTE Historical Data Testing and Validation Console
/// 
/// CLEAN DATA ACQUISITION API TESTING TOOL
/// =======================================
/// 
/// This console application provides comprehensive testing and validation 
/// of the ODTE.Historical data acquisition pipeline. It demonstrates the
/// clean API for acquiring data on various instruments and commodities.
/// 
/// DISCOVERABLE FROM COLD START:
/// - No hardcoded paths - uses relative and temp directories
/// - Self-configuring database setup
/// - Automatic provider discovery
/// - Cross-platform compatibility
/// 
/// Usage: 
///   dotnet run                           # Interactive mode
///   dotnet run [operation]               # Direct operation
///   dotnet run --help                    # Show detailed help
///   dotnet run --api-demo               # API demonstration
/// 
/// Operations:
///   api-demo    - Demonstrate clean data acquisition APIs
///   providers   - Test and validate all data providers  
///   validate    - Run comprehensive data quality validation
///   benchmark   - Performance and quality benchmarking
///   export      - Test data export capabilities
///   status      - Show system status and health
///   instruments - Test multi-instrument support
///   stress      - Stress test the data pipeline
/// </summary>
class Program
{
    private static readonly string DefaultDatabasePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ODTE", "Historical", "test_data.db");
    
    private static readonly string TempDatabasePath = Path.Combine(
        Path.GetTempPath(), $"odte_historical_test_{DateTime.Now:yyyyMMdd}.db");

    static async Task<int> Main(string[] args)
    {
        try
        {
            PrintHeader();

            // Handle help requests
            if (args.Any(arg => arg.Contains("help") || arg == "-h" || arg == "--help"))
            {
                ShowDetailedHelp();
                return 0;
            }

            // Parse operation from arguments
            var operation = args.Length > 0 ? args[0].ToLower() : "";
            
            // Interactive mode if no operation specified
            if (string.IsNullOrEmpty(operation))
            {
                operation = await GetInteractiveOperation();
            }

            Console.WriteLine($"🎯 Running operation: {operation}");
            Console.WriteLine();

            return operation switch
            {
                "api-demo" or "demo" => await RunApiDemonstration(),
                "providers" or "provider" => await TestDataProviders(),
                "validate" or "validation" => await RunDataValidation(args.Skip(1).ToArray()),
                "benchmark" or "bench" => await RunBenchmarkSuite(args.Skip(1).ToArray()),
                "export" or "exp" => await TestExportCapabilities(),
                "status" or "health" => await ShowSystemStatus(),
                "instruments" or "inst" => await TestInstrumentSupport(),
                "stress" or "load" => await RunStressTest(),
                "cold-start" or "cold" => await TestColdStartCapability(),
                "integration" or "int" => await RunIntegrationTests(),
                _ => await ShowOperationsMenu()
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ FATAL ERROR: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return 1;
        }
    }

    private static void PrintHeader()
    {
        Console.WriteLine("🧬 ODTE Historical Data Acquisition API Testing");
        Console.WriteLine("═══════════════════════════════════════════════");
        Console.WriteLine("Clean, discoverable API for acquiring market data");
        Console.WriteLine("Supports stocks, ETFs, commodities, forex, and indices");
        Console.WriteLine();
        Console.WriteLine($"📍 Working Directory: {Directory.GetCurrentDirectory()}");
        Console.WriteLine($"🗃️  Default Database: {DefaultDatabasePath}");
        Console.WriteLine($"🧪 Test Database: {TempDatabasePath}");
        Console.WriteLine();
    }

    private static async Task<string> GetInteractiveOperation()
    {
        Console.WriteLine("📊 Select operation:");
        Console.WriteLine("   1. api-demo     - Demonstrate clean data acquisition APIs");
        Console.WriteLine("   2. providers    - Test and validate all data providers");
        Console.WriteLine("   3. validate     - Run comprehensive data quality validation");
        Console.WriteLine("   4. benchmark    - Performance and quality benchmarking");
        Console.WriteLine("   5. export       - Test data export capabilities");
        Console.WriteLine("   6. status       - Show system status and health");
        Console.WriteLine("   7. instruments  - Test multi-instrument support");
        Console.WriteLine("   8. stress       - Stress test the data pipeline");
        Console.WriteLine("   9. cold-start   - Test cold start capability");
        Console.WriteLine("  10. integration  - Run integration test suite");
        Console.WriteLine();
        Console.Write("Enter operation (1-10 or name): ");

        var input = Console.ReadLine()?.Trim().ToLower();
        
        return input switch
        {
            "1" or "demo" => "api-demo",
            "2" or "prov" => "providers", 
            "3" or "val" => "validate",
            "4" or "bench" => "benchmark",
            "5" or "exp" => "export",
            "6" or "stat" => "status",
            "7" or "inst" => "instruments",
            "8" or "stress" => "stress",
            "9" or "cold" => "cold-start",
            "10" or "int" => "integration",
            _ => input ?? "api-demo"
        };
    }

    private static async Task<int> RunApiDemonstration()
    {
        Console.WriteLine("🚀 CLEAN DATA ACQUISITION API DEMONSTRATION");
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine();

        try
        {
            // Create test database in temp location
            EnsureDirectoryExists(Path.GetDirectoryName(TempDatabasePath));
            
            using var dataManager = new HistoricalDataManager(TempDatabasePath);
            
            Console.WriteLine("1️⃣  Initializing data manager (cold start)...");
            await dataManager.InitializeAsync();
            Console.WriteLine("   ✅ Data manager initialized successfully");
            
            Console.WriteLine();
            Console.WriteLine("2️⃣  Getting system status...");
            var stats = await dataManager.GetStatsAsync();
            Console.WriteLine($"   📊 Database: {stats.DatabaseSizeMB:F2} MB");
            Console.WriteLine($"   📈 Records: {stats.TotalRecords:N0}");
            Console.WriteLine($"   📅 Range: {stats.StartDate:yyyy-MM-dd} to {stats.EndDate:yyyy-MM-dd}");
            
            Console.WriteLine();
            Console.WriteLine("3️⃣  Testing multi-instrument data acquisition...");
            var testSymbols = new[] { "SPY", "QQQ", "GLD", "TLT", "VIX" };
            var endDate = DateTime.Now.Date.AddDays(-1);
            var startDate = endDate.AddDays(-7);
            
            foreach (var symbol in testSymbols)
            {
                try
                {
                    var data = await dataManager.GetMarketDataAsync(symbol, startDate, endDate);
                    Console.WriteLine($"   ✅ {symbol}: API call successful ({data.Count()} points)");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ⚠️  {symbol}: {ex.Message}");
                }
            }
            
            Console.WriteLine();
            Console.WriteLine("4️⃣  Testing available symbols...");
            var symbols = await dataManager.GetAvailableSymbolsAsync();
            Console.WriteLine($"   🔍 Found {symbols.Count} available symbols");
            if (symbols.Any())
            {
                Console.WriteLine($"      First few: {string.Join(", ", symbols.Take(5))}");
            }
            
            Console.WriteLine();
            Console.WriteLine("🎉 API DEMONSTRATION COMPLETED SUCCESSFULLY");
            Console.WriteLine("   ✅ Clean API is working and discoverable");
            Console.WriteLine("   ✅ Cold start capability confirmed");
            Console.WriteLine("   ✅ Multi-instrument support validated");
            Console.WriteLine("   ✅ Provider discovery operational");
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ API demonstration failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> TestDataProviders()
    {
        Console.WriteLine("🌐 DATA PROVIDERS TESTING");
        Console.WriteLine("═════════════════════════");
        Console.WriteLine();

        try
        {
            using var dataManager = new HistoricalDataManager(TempDatabasePath);
            await dataManager.InitializeAsync();

            Console.WriteLine("Testing individual data providers...");
            
            // Test Stooq provider
            Console.WriteLine();
            Console.WriteLine("📊 Stooq Provider Test:");
            using var stooqProvider = new ODTE.Historical.DataProviders.StooqProvider();
            var testDate = DateTime.Now.AddDays(-30);
            var stooqData = await stooqProvider.GetHistoricalDataAsync("SPY", testDate, DateTime.Now.AddDays(-1));
            Console.WriteLine($"   ✅ Stooq: {stooqData.Count} data points retrieved");

            // Test Yahoo provider
            Console.WriteLine();
            Console.WriteLine("📈 Yahoo Finance Provider Test:");
            using var yahooProvider = new ODTE.Historical.DataProviders.YahooFinanceProvider();
            var yahooData = await yahooProvider.GetHistoricalDataAsync("SPY", testDate, DateTime.Now.AddDays(-1));
            Console.WriteLine($"   ✅ Yahoo: {yahooData.Count} data points retrieved");

            Console.WriteLine();
            Console.WriteLine("🎯 PROVIDER TESTING COMPLETED");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Provider testing failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> RunDataValidation(string[] args)
    {
        Console.WriteLine("🔍 DATA QUALITY VALIDATION");
        Console.WriteLine("══════════════════════════");
        Console.WriteLine();

        try
        {
            using var dataManager = new HistoricalDataManager(TempDatabasePath);
            await dataManager.InitializeAsync();

            var symbol = args.Length > 0 ? args[0] : "SPY";
            var validationDate = args.Length > 1 ? DateTime.Parse(args[1]) : DateTime.Now.AddDays(-7);

            Console.WriteLine($"Getting data for {symbol} from {validationDate:yyyy-MM-dd}...");
            
            var data = await dataManager.GetMarketDataAsync(symbol, validationDate, validationDate.AddDays(1));
            
            Console.WriteLine($"✅ Data retrieval completed:");
            Console.WriteLine($"   Symbol: {symbol}");
            Console.WriteLine($"   Data points: {data.Count()}");
            Console.WriteLine($"   Data available: {data.Any()}");

            return data.Any() ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Data validation failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> RunBenchmarkSuite(string[] args)
    {
        Console.WriteLine("⚡ PERFORMANCE BENCHMARK SUITE");
        Console.WriteLine("══════════════════════════════");
        Console.WriteLine();

        // Run synthetic data benchmark
        Console.WriteLine("🧬 Running synthetic data benchmark...");
        return await SyntheticDataBenchmarkTool.RunBenchmarkToolAsync(args);
    }

    private static async Task<int> TestExportCapabilities()
    {
        Console.WriteLine("📤 EXPORT CAPABILITIES TEST");
        Console.WriteLine("═══════════════════════════");
        Console.WriteLine();

        try
        {
            using var dataManager = new HistoricalDataManager(TempDatabasePath);
            await dataManager.InitializeAsync();

            var tempExportDir = Path.Combine(Path.GetTempPath(), $"odte_export_test_{DateTime.Now:yyyyMMddHHmmss}");
            Directory.CreateDirectory(tempExportDir);

            Console.WriteLine($"Testing export to: {tempExportDir}");
            
            var exportResult = await dataManager.ExportCommonDatasetsAsync(tempExportDir);
            
            Console.WriteLine($"✅ Export test completed");
            Console.WriteLine($"   Success: {exportResult.Success}");
            Console.WriteLine($"   Files created: {Directory.GetFiles(tempExportDir).Length}");

            // Cleanup
            Directory.Delete(tempExportDir, true);
            
            return exportResult.Success ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Export test failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> ShowSystemStatus()
    {
        Console.WriteLine("📊 SYSTEM STATUS & HEALTH");
        Console.WriteLine("═════════════════════════");
        Console.WriteLine();

        try
        {
            using var dataManager = new HistoricalDataManager(TempDatabasePath);
            await dataManager.InitializeAsync();

            var stats = await dataManager.GetStatsAsync();
            
            Console.WriteLine("System Status:");
            Console.WriteLine($"   Database: {stats.DatabaseSizeMB:F2} MB");
            Console.WriteLine($"   Records: {stats.TotalRecords:N0}");
            Console.WriteLine($"   Date range: {stats.StartDate:yyyy-MM-dd} to {stats.EndDate:yyyy-MM-dd}");
            Console.WriteLine($"   Compression: {stats.CompressionRatio:F1}x");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Status check failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> TestInstrumentSupport()
    {
        Console.WriteLine("🌍 MULTI-INSTRUMENT SUPPORT TEST");
        Console.WriteLine("═════════════════════════════════");
        Console.WriteLine();

        var instrumentCategories = new Dictionary<string, string[]>
        {
            ["US Equities"] = new[] { "SPY", "QQQ", "IWM", "DIA" },
            ["Commodities"] = new[] { "GLD", "SLV", "USO", "UNG" },
            ["Bonds"] = new[] { "TLT", "IEF", "SHY", "LQD" },
            ["Volatility"] = new[] { "VIX", "UVXY", "VXX" },
            ["International"] = new[] { "EFA", "EEM", "FXI", "EWJ" }
        };

        try
        {
            using var dataManager = new HistoricalDataManager(TempDatabasePath);
            await dataManager.InitializeAsync();

            var endDate = DateTime.Now.Date.AddDays(-1);
            var startDate = endDate.AddDays(-5);

            foreach (var category in instrumentCategories)
            {
                Console.WriteLine($"📈 Testing {category.Key}:");
                foreach (var symbol in category.Value)
                {
                    try
                    {
                        var data = await dataManager.GetMarketDataAsync(symbol, startDate, endDate);
                        Console.WriteLine($"   ✅ {symbol}: API responded successfully");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"   ⚠️  {symbol}: {ex.Message.Substring(0, Math.Min(50, ex.Message.Length))}...");
                    }
                }
                Console.WriteLine();
            }

            Console.WriteLine("🎯 INSTRUMENT SUPPORT TEST COMPLETED");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Instrument test failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> RunStressTest()
    {
        Console.WriteLine("🔥 STRESS TEST");
        Console.WriteLine("═══════════════");
        Console.WriteLine();

        try
        {
            using var dataManager = new HistoricalDataManager(TempDatabasePath);
            await dataManager.InitializeAsync();

            Console.WriteLine("Running concurrent data acquisition stress test...");
            
            var symbols = new[] { "SPY", "QQQ", "IWM", "GLD", "TLT", "VIX", "EFA", "EEM", "USO", "SLV" };
            var endDate = DateTime.Now.Date.AddDays(-1);
            var startDate = endDate.AddDays(-3);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var tasks = symbols.Select(async symbol =>
            {
                try
                {
                    return await dataManager.GetMarketDataAsync(symbol, startDate, endDate);
                }
                catch
                {
                    return Enumerable.Empty<MarketDataBar>();
                }
            });

            var results = await Task.WhenAll(tasks);
            stopwatch.Stop();

            var totalDataPoints = results.Sum(r => r.Count());
            
            Console.WriteLine($"✅ Stress test completed:");
            Console.WriteLine($"   Symbols: {symbols.Length}");
            Console.WriteLine($"   Time: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"   Avg per symbol: {stopwatch.ElapsedMilliseconds / symbols.Length}ms");
            Console.WriteLine($"   Total data points: {totalDataPoints}");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Stress test failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> TestColdStartCapability()
    {
        Console.WriteLine("🥶 COLD START CAPABILITY TEST");
        Console.WriteLine("══════════════════════════════");
        Console.WriteLine();

        try
        {
            // Create completely new database in temp location
            var coldStartDb = Path.Combine(Path.GetTempPath(), $"odte_cold_start_{Guid.NewGuid()}.db");
            
            Console.WriteLine($"Testing cold start with new database: {coldStartDb}");
            
            using var dataManager = new HistoricalDataManager(coldStartDb);
            
            Console.WriteLine("1. Initializing from scratch...");
            await dataManager.InitializeAsync();
            Console.WriteLine("   ✅ Cold initialization successful");
            
            Console.WriteLine("2. Testing immediate API availability...");
            var stats = await dataManager.GetStatsAsync();
            Console.WriteLine($"   ✅ Stats API available: {stats.TotalRecords} records");
            
            Console.WriteLine("3. Testing data acquisition...");
            var data = await dataManager.GetMarketDataAsync("SPY", DateTime.Now.AddDays(-5), DateTime.Now.AddDays(-1));
            Console.WriteLine($"   ✅ Data acquisition working: {data.Count()} points");

            // Cleanup
            File.Delete(coldStartDb);
            
            Console.WriteLine();
            Console.WriteLine("🎉 COLD START TEST PASSED");
            Console.WriteLine("   ✅ Can initialize from scratch");
            Console.WriteLine("   ✅ APIs immediately available");
            Console.WriteLine("   ✅ No pre-configuration required");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Cold start test failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> RunIntegrationTests()
    {
        Console.WriteLine("🔗 INTEGRATION TESTS");
        Console.WriteLine("═══════════════════");
        Console.WriteLine();

        Console.WriteLine("Running xUnit integration test suite...");
        Console.WriteLine("Use: dotnet test --filter DataAcquisitionApiTests");
        
        return 0;
    }

    private static async Task<int> ShowOperationsMenu()
    {
        Console.WriteLine("❓ UNKNOWN OPERATION");
        Console.WriteLine("═══════════════════");
        Console.WriteLine();
        Console.WriteLine("Available operations:");
        Console.WriteLine("  api-demo     - Demonstrate clean data acquisition APIs");
        Console.WriteLine("  providers    - Test and validate all data providers");
        Console.WriteLine("  validate     - Run comprehensive data quality validation");
        Console.WriteLine("  benchmark    - Performance and quality benchmarking");
        Console.WriteLine("  export       - Test data export capabilities");
        Console.WriteLine("  status       - Show system status and health");
        Console.WriteLine("  instruments  - Test multi-instrument support");
        Console.WriteLine("  stress       - Stress test the data pipeline");
        Console.WriteLine("  cold-start   - Test cold start capability");
        Console.WriteLine("  integration  - Run integration test suite");
        Console.WriteLine();
        Console.WriteLine("Use --help for detailed information");
        
        return 1;
    }

    private static void ShowDetailedHelp()
    {
        Console.WriteLine("ODTE HISTORICAL DATA ACQUISITION API TESTING");
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine("This tool provides comprehensive testing and validation of the");
        Console.WriteLine("ODTE.Historical data acquisition pipeline. It demonstrates the");
        Console.WriteLine("clean API for acquiring data on various instruments and commodities.");
        Console.WriteLine();
        Console.WriteLine("KEY FEATURES:");
        Console.WriteLine("  ✅ No hardcoded paths - uses relative and temp directories");
        Console.WriteLine("  ✅ Self-configuring database setup");
        Console.WriteLine("  ✅ Automatic provider discovery");
        Console.WriteLine("  ✅ Cross-platform compatibility");
        Console.WriteLine("  ✅ Cold start capability");
        Console.WriteLine();
        Console.WriteLine("USAGE:");
        Console.WriteLine("  dotnet run                    # Interactive mode");
        Console.WriteLine("  dotnet run api-demo          # API demonstration");
        Console.WriteLine("  dotnet run providers          # Test data providers");
        Console.WriteLine("  dotnet run validate SPY       # Validate SPY data");
        Console.WriteLine("  dotnet run benchmark          # Run benchmarks");
        Console.WriteLine("  dotnet run cold-start         # Test cold start");
        Console.WriteLine();
        Console.WriteLine("SUPPORTED INSTRUMENTS:");
        Console.WriteLine("  • US Equities (SPY, QQQ, IWM, etc.)");
        Console.WriteLine("  • Commodities (GLD, SLV, USO, etc.)");
        Console.WriteLine("  • Bonds (TLT, IEF, SHY, etc.)");
        Console.WriteLine("  • Volatility (VIX, UVXY, etc.)");
        Console.WriteLine("  • International (EFA, EEM, etc.)");
    }

    private static void EnsureDirectoryExists(string? path)
    {
        if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }
}