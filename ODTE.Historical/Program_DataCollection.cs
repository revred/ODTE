using Microsoft.Extensions.Logging;
using ODTE.Historical.DataCollection;

namespace ODTE.Historical;

/// <summary>
/// Main program for comprehensive 20-year data collection (2005-2025)
/// Run this to collect all historical data and optimize SQLite for backtesting
/// </summary>
public class Program_DataCollection
{
    private static readonly string DatabasePath = @"C:\code\ODTE\Data\ODTE_TimeSeries_20Y.db";
    private static readonly string LogPath = @"C:\code\ODTE\Data\Logs";

    public static async Task Main(string[] args)
    {
        // Set up comprehensive logging
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole(options =>
            {
                options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
            });
            builder.AddFilter("System", LogLevel.Warning);
            builder.AddFilter("Microsoft", LogLevel.Warning);
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var logger = loggerFactory.CreateLogger<Program_DataCollection>();

        try
        {
            await RunDataCollectionAsync(args, logger);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "💥 Critical error in data collection");
            Environment.Exit(1);
        }
    }

    private static async Task RunDataCollectionAsync(string[] args, ILogger logger)
    {
        logger.LogInformation("🚀 ODTE 20-Year Historical Data Collection Starting");
        logger.LogInformation("==================================================");
        logger.LogInformation($"📅 Target: 2005-2025 (20 years of market data)");
        logger.LogInformation($"🗄️  Database: {DatabasePath}");
        logger.LogInformation($"💾 Estimated final size: ~50GB");
        logger.LogInformation("");

        // Check for command line arguments
        var mode = args.Length > 0 ? args[0].ToLower() : "full";

        switch (mode)
        {
            case "full":
                await RunFullDataCollectionAsync(logger);
                break;
            case "resume":
                await RunResumeDataCollectionAsync(logger);
                break;
            case "validate":
                await RunValidationOnlyAsync(logger);
                break;
            case "test":
                await RunTestCollectionAsync(logger);
                break;
            case "optimize":
                await RunDatabaseOptimizationAsync(logger);
                break;
            default:
                logger.LogError($"Unknown mode: {mode}");
                ShowUsage();
                return;
        }

        logger.LogInformation("🎉 Data collection program completed!");
    }

    /// <summary>
    /// Run complete 20-year data collection
    /// </summary>
    private static async Task RunFullDataCollectionAsync(ILogger logger)
    {
        logger.LogInformation("🎯 Mode: FULL DATA COLLECTION (2005-2025)");
        logger.LogInformation("⚠️  This will take several hours and use significant API quota");
        logger.LogInformation("⚠️  Ensure you have valid API keys set in environment variables");
        logger.LogInformation("");

        // Confirm before starting
        if (!ConfirmOperation("Start full 20-year data collection?"))
        {
            logger.LogInformation("Operation cancelled by user");
            return;
        }

        var collectorLogger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ComprehensiveDataCollector>();
        var optimizerLogger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<DatabaseOptimizer>();
        var validatorLogger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<DataValidationEngine>();

        // Step 1: Database Setup and Optimization
        logger.LogInformation("🔧 Step 1: Database Setup and Optimization");
        var optimizer = new DatabaseOptimizer(DatabasePath, optimizerLogger);

        await optimizer.CreateOptimizedSchemaAsync();
        await optimizer.OptimizeForBulkInsertAsync();

        var initialStats = await optimizer.GetPerformanceStatsAsync();
        logger.LogInformation($"📊 Initial database: {initialStats.TotalRecords:N0} records, {initialStats.DatabaseSizeMB:N1} MB");

        // Step 2: Data Collection
        logger.LogInformation("📈 Step 2: Comprehensive Data Collection");
        logger.LogInformation("This will collect data for all major symbols across 20 years...");

        using var collector = new ComprehensiveDataCollector(DatabasePath, logger: collectorLogger);
        using var cts = new CancellationTokenSource();

        // Handle Ctrl+C gracefully
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            logger.LogWarning("🛑 Cancellation requested... finishing current batch");
            cts.Cancel();
        };

        var collectionResult = await collector.CollectFullHistoricalDataAsync(cts.Token);

        if (collectionResult.Success)
        {
            logger.LogInformation($"✅ Data collection completed successfully!");
            logger.LogInformation($"   Duration: {collectionResult.Duration.TotalHours:F1} hours");
            logger.LogInformation($"   Days processed: {collectionResult.TotalDaysProcessed:N0}");
            logger.LogInformation($"   Success rate: {collectionResult.OverallSuccessRate:P2}");
            logger.LogInformation($"   API calls made: {collectionResult.TotalApiCalls:N0}");
        }
        else
        {
            logger.LogError($"❌ Data collection failed: {collectionResult.ErrorMessage}");
        }

        // Step 3: Data Validation
        logger.LogInformation("🔍 Step 3: Data Quality Validation");

        var database = new TimeSeriesDatabase(DatabasePath);
        var validator = new DataValidationEngine(database, validatorLogger);

        var symbols = new[] { "SPY", "XSP", "QQQ", "IWM", "VIX" }; // Key symbols for validation
        var validationResult = await validator.ValidateCollectedDataAsync(
            new DateTime(2005, 1, 1),
            new DateTime(2025, 12, 31),
            symbols.ToList());

        logger.LogInformation($"📊 Validation Results:");
        logger.LogInformation($"   Overall quality score: {validationResult.OverallQualityScore:P1}");
        logger.LogInformation($"   Total records validated: {validationResult.TotalRecords:N0}");
        logger.LogInformation($"   Data gaps found: {validationResult.TotalGaps}");
        logger.LogInformation($"   Anomalies detected: {validationResult.TotalAnomalies}");

        // Step 4: Final Database Optimization
        logger.LogInformation("⚡ Step 4: Final Database Optimization");

        await optimizer.OptimizeForQueryingAsync();

        var finalStats = await optimizer.GetPerformanceStatsAsync();
        logger.LogInformation($"📊 Final database statistics:");
        logger.LogInformation($"   Total records: {finalStats.TotalRecords:N0}");
        logger.LogInformation($"   Database size: {finalStats.DatabaseSizeGB:N2} GB");
        logger.LogInformation($"   Date range: {finalStats.EarliestDate:yyyy-MM-dd} to {finalStats.LatestDate:yyyy-MM-dd}");
        logger.LogInformation($"   Years covered: {finalStats.TotalYears}");
        logger.LogInformation($"   Indexes created: {finalStats.IndexCount}");

        // Step 5: Backtest Readiness Check
        logger.LogInformation("🎯 Step 5: Backtest Readiness Verification");

        var backtestData = await database.GetRangeAsync(
            new DateTime(2020, 1, 1),
            new DateTime(2020, 12, 31),
            "SPY");

        if (backtestData.Count > 0)
        {
            logger.LogInformation($"✅ Backtest verification successful!");
            logger.LogInformation($"   Sample query returned {backtestData.Count:N0} records for SPY in 2020");
            logger.LogInformation($"   Database is ready for ODTE backtesting!");
        }
        else
        {
            logger.LogWarning($"⚠️  Backtest verification failed - no sample data retrieved");
        }

        database.Dispose();
    }

    /// <summary>
    /// Resume data collection from previous progress
    /// </summary>
    private static async Task RunResumeDataCollectionAsync(ILogger logger)
    {
        logger.LogInformation("🔄 Mode: RESUME DATA COLLECTION");
        logger.LogInformation("Continuing from previous progress...");

        var collectorLogger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ComprehensiveDataCollector>();

        using var collector = new ComprehensiveDataCollector(DatabasePath, logger: collectorLogger);
        using var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            logger.LogWarning("🛑 Cancellation requested... finishing current batch");
            cts.Cancel();
        };

        var result = await collector.ResumeDataCollectionAsync(cts.Token);

        if (result.Success)
        {
            logger.LogInformation($"✅ Resume completed: {result.TotalDaysProcessed:N0} additional days processed");
        }
        else
        {
            logger.LogError($"❌ Resume failed: {result.ErrorMessage}");
        }
    }

    /// <summary>
    /// Run validation only on existing data
    /// </summary>
    private static async Task RunValidationOnlyAsync(ILogger logger)
    {
        logger.LogInformation("🔍 Mode: VALIDATION ONLY");
        logger.LogInformation("Validating existing database...");

        var database = new TimeSeriesDatabase(DatabasePath);
        var validatorLogger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<DataValidationEngine>();
        var validator = new DataValidationEngine(database, validatorLogger);

        var symbols = new[] { "SPY", "XSP", "QQQ", "IWM", "VIX", "AAPL", "MSFT" };
        var result = await validator.ValidateCollectedDataAsync(
            new DateTime(2005, 1, 1),
            new DateTime(2025, 12, 31),
            symbols.ToList());

        // Display detailed validation results
        logger.LogInformation($"📊 Detailed Validation Results:");

        foreach (var symbolResult in result.SymbolValidations)
        {
            var symbol = symbolResult.Key;
            var validation = symbolResult.Value;

            logger.LogInformation($"");
            logger.LogInformation($"📈 {symbol}:");
            logger.LogInformation($"   Records: {validation.TotalRecords:N0}");
            logger.LogInformation($"   Quality Score: {validation.QualityScore:P1}");
            logger.LogInformation($"   Completeness: {validation.DataCompleteness:P1}");
            logger.LogInformation($"   Gaps: {validation.Gaps.Count}");
            logger.LogInformation($"   Anomalies: {validation.Anomalies.Count}");

            if (validation.Gaps.Any())
            {
                logger.LogWarning($"   ⚠️  Data Gaps:");
                foreach (var gap in validation.Gaps.Take(3))
                {
                    logger.LogWarning($"     - {gap.StartDate:yyyy-MM-dd} to {gap.EndDate:yyyy-MM-dd}: {gap.Description}");
                }
            }

            if (validation.Anomalies.Any())
            {
                logger.LogWarning($"   ⚠️  Anomalies:");
                foreach (var anomaly in validation.Anomalies.Take(3))
                {
                    logger.LogWarning($"     - {anomaly.Date:yyyy-MM-dd}: {anomaly.Description}");
                }
            }
        }

        database.Dispose();
    }

    /// <summary>
    /// Run test collection for a small date range
    /// </summary>
    private static async Task RunTestCollectionAsync(ILogger logger)
    {
        logger.LogInformation("🧪 Mode: TEST COLLECTION");
        logger.LogInformation("Running test collection for recent 30 days...");

        var collectorLogger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ComprehensiveDataCollector>();

        using var collector = new ComprehensiveDataCollector(DatabasePath, logger: collectorLogger);

        var testSymbols = new List<string> { "SPY", "QQQ" };
        var startDate = DateTime.Today.AddDays(-30);
        var endDate = DateTime.Today.AddDays(-1);

        var result = await collector.CollectDateRangeAsync(startDate, endDate, testSymbols);

        if (result.Success)
        {
            logger.LogInformation($"✅ Test collection successful:");
            logger.LogInformation($"   Duration: {result.Duration.TotalSeconds:F1} seconds");
            logger.LogInformation($"   Days processed: {result.TotalDaysProcessed}");
            logger.LogInformation($"   Success rate: {result.OverallSuccessRate:P2}");
        }
        else
        {
            logger.LogError($"❌ Test collection failed: {result.ErrorMessage}");
        }
    }

    /// <summary>
    /// Run database optimization only
    /// </summary>
    private static async Task RunDatabaseOptimizationAsync(ILogger logger)
    {
        logger.LogInformation("⚡ Mode: DATABASE OPTIMIZATION");
        logger.LogInformation("Optimizing existing database for querying...");

        var optimizerLogger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<DatabaseOptimizer>();
        var optimizer = new DatabaseOptimizer(DatabasePath, optimizerLogger);

        var beforeStats = await optimizer.GetPerformanceStatsAsync();
        logger.LogInformation($"📊 Before optimization: {beforeStats.DatabaseSizeMB:N1} MB, {beforeStats.IndexCount} indexes");

        await optimizer.OptimizeForQueryingAsync();

        var afterStats = await optimizer.GetPerformanceStatsAsync();
        logger.LogInformation($"📊 After optimization: {afterStats.DatabaseSizeMB:N1} MB, {afterStats.IndexCount} indexes");

        var sizeDiff = afterStats.DatabaseSizeMB - beforeStats.DatabaseSizeMB;
        logger.LogInformation($"📈 Size change: {sizeDiff:+0.0;-0.0;0} MB");
    }

    private static bool ConfirmOperation(string message)
    {
        Console.WriteLine($"❓ {message} (y/N): ");
        var response = Console.ReadLine()?.Trim().ToLower();
        return response == "y" || response == "yes";
    }

    private static void ShowUsage()
    {
        Console.WriteLine("Usage: ODTE.Historical.exe [mode]");
        Console.WriteLine();
        Console.WriteLine("Modes:");
        Console.WriteLine("  full     - Complete 20-year data collection (default)");
        Console.WriteLine("  resume   - Resume from previous progress");
        Console.WriteLine("  validate - Validate existing data only");
        Console.WriteLine("  test     - Test collection with recent 30 days");
        Console.WriteLine("  optimize - Optimize database for querying only");
        Console.WriteLine();
        Console.WriteLine("Environment Variables Required:");
        Console.WriteLine("  POLYGON_API_KEY      - Polygon.io API key");
        Console.WriteLine("  ALPHA_VANTAGE_API_KEY - Alpha Vantage API key");
        Console.WriteLine("  TWELVE_DATA_API_KEY  - Twelve Data API key");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  ODTE.Historical.exe full");
        Console.WriteLine("  ODTE.Historical.exe test");
        Console.WriteLine("  ODTE.Historical.exe validate");
    }
}