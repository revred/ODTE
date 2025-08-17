using Microsoft.Extensions.Logging;
using ODTE.Historical.DataCollection;

namespace ODTE.Historical.Examples;

/// <summary>
/// Quick start demonstration of the 20-year data collection system
/// </summary>
public class QuickStartDemo
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("🚀 ODTE 20-Year Data Collection - Quick Start Demo");
        Console.WriteLine("==================================================");
        Console.WriteLine();

        // Set up basic logging
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        var logger = loggerFactory.CreateLogger<QuickStartDemo>();

        // Show what we're going to do
        logger.LogInformation("This demo will:");
        logger.LogInformation("1. 🧪 Run a test collection (30 days, 2 symbols)");
        logger.LogInformation("2. 🔍 Validate the collected data");
        logger.LogInformation("3. ⚡ Optimize the database for backtesting");
        logger.LogInformation("");

        Console.Write("Press Enter to continue or Ctrl+C to exit...");
        Console.ReadLine();

        try
        {
            // Step 1: Test Collection
            logger.LogInformation("🧪 Step 1: Running test collection...");
            await RunTestCollection(logger);

            // Step 2: Data Validation
            logger.LogInformation("🔍 Step 2: Validating collected data...");
            await RunDataValidation(logger);

            // Step 3: Database Optimization
            logger.LogInformation("⚡ Step 3: Optimizing database...");
            await RunDatabaseOptimization(logger);

            // Final Status
            logger.LogInformation("🎉 Demo completed successfully!");
            logger.LogInformation("");
            logger.LogInformation("Next steps:");
            logger.LogInformation("  - Set API keys: $env:POLYGON_API_KEY='your_key'");
            logger.LogInformation("  - Run full collection: .\\setup_and_run_collection.ps1 -Mode full");
            logger.LogInformation("  - Start backtesting with your ODTE strategies!");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Demo failed");
        }

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }

    private static async Task RunTestCollection(ILogger logger)
    {
        using var collector = new ComprehensiveDataCollector(
            @"C:\code\ODTE\Data\ODTE_Demo.db");

        var result = await collector.CollectDateRangeAsync(
            DateTime.Today.AddDays(-30),
            DateTime.Today.AddDays(-1),
            new List<string> { "SPY", "QQQ" });

        if (result.Success)
        {
            logger.LogInformation($"✅ Test collection successful:");
            logger.LogInformation($"   Days processed: {result.TotalDaysProcessed}");
            logger.LogInformation($"   Success rate: {result.OverallSuccessRate:P1}");
            logger.LogInformation($"   Duration: {result.Duration.TotalSeconds:F1}s");
        }
        else
        {
            logger.LogWarning($"⚠️  Test collection had issues: {result.ErrorMessage}");
            logger.LogInformation($"   This is normal without API keys - using mock data");
        }
    }

    private static async Task RunDataValidation(ILogger logger)
    {
        var database = new TimeSeriesDatabase(@"C:\code\ODTE\Data\ODTE_Demo.db");
        await database.InitializeAsync();

        var validator = new DataValidationEngine(database);

        var report = await validator.ValidateCollectedDataAsync(
            DateTime.Today.AddDays(-30),
            DateTime.Today.AddDays(-1),
            new List<string> { "SPY", "QQQ" });

        logger.LogInformation($"📊 Validation Results:");
        logger.LogInformation($"   Overall quality: {report.OverallQualityScore:P1}");
        logger.LogInformation($"   Records validated: {report.TotalRecords:N0}");
        logger.LogInformation($"   Data gaps: {report.TotalGaps}");
        logger.LogInformation($"   Anomalies: {report.TotalAnomalies}");

        database.Dispose();
    }

    private static async Task RunDatabaseOptimization(ILogger logger)
    {
        var optimizer = new DatabaseOptimizer(@"C:\code\ODTE\Data\ODTE_Demo.db");

        var beforeStats = await optimizer.GetPerformanceStatsAsync();
        logger.LogInformation($"📊 Before optimization: {beforeStats.DatabaseSizeMB:N1} MB");

        await optimizer.OptimizeForQueryingAsync();

        var afterStats = await optimizer.GetPerformanceStatsAsync();
        logger.LogInformation($"📊 After optimization: {afterStats.DatabaseSizeMB:N1} MB");
        logger.LogInformation($"   Records: {afterStats.TotalRecords:N0}");
        logger.LogInformation($"   Indexes: {afterStats.IndexCount}");
    }
}