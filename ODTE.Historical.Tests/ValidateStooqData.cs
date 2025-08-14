// ValidateStooqData.cs — Console application for running Stooq data validation
// Provides command-line interface for data quality checks and performance monitoring

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using ODTE.Historical.Validation;
using ODTE.Historical.Monitoring;

namespace ODTE.Historical.Tests
{
    /// <summary>
    /// Command-line utility for validating Stooq data quality and performance
    /// Usage: dotnet run ValidateStooqData [database_path] [options]
    /// </summary>
    public static class StooqDataValidationTool
    {
        public static async Task<int> RunValidationToolAsync(string[] args)
        {
            try
            {
                var options = ParseArguments(args);
                var logger = CreateLogger(options.Verbose);

                Console.WriteLine("🧬 ODTE Stooq Data Validation Tool");
                Console.WriteLine($"Database: {options.DatabasePath}");
                Console.WriteLine($"Mode: {options.Mode}");
                Console.WriteLine();

                if (!File.Exists(options.DatabasePath))
                {
                    Console.WriteLine($"❌ Database file not found: {options.DatabasePath}");
                    return 1;
                }

                var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

                var exitCode = options.Mode switch
                {
                    "validate" => await RunValidationAsync(options.DatabasePath, logger, cts.Token),
                    "monitor" => await RunMonitoringAsync(options.DatabasePath, logger, cts.Token),
                    "benchmark" => await RunBenchmarkAsync(options.DatabasePath, logger, cts.Token),
                    "health" => await CheckHealthAsync(options.DatabasePath, logger, cts.Token),
                    _ => ShowUsage()
                };

                return exitCode;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("\n🛑 Operation cancelled by user");
                return 130; // Standard exit code for SIGINT
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Unexpected error: {ex.Message}");
                return 1;
            }
        }

        private static async Task<int> RunValidationAsync(string databasePath, ILogger logger, CancellationToken ct)
        {
            Console.WriteLine("🔍 Starting comprehensive data validation...");
            Console.WriteLine();

            var validator = new StooqDataValidator(databasePath, (ILogger<StooqDataValidator>)logger);
            var report = await validator.RunFullValidationAsync(ct);

            // Display results
            Console.WriteLine($"📊 Validation Report (ID: {report.ValidationId})");
            Console.WriteLine($"Duration: {report.TotalDuration.TotalSeconds:F2} seconds");
            Console.WriteLine($"Overall Score: {report.OverallScore}/100");
            Console.WriteLine($"Status: {(report.IsValid ? "✅ VALID" : "❌ INVALID")}");
            Console.WriteLine();

            // Test results by category
            var categories = new Dictionary<string, List<ValidationTest>>();
            foreach (var test in report.Tests)
            {
                if (!categories.ContainsKey(test.Category))
                    categories[test.Category] = new List<ValidationTest>();
                categories[test.Category].Add(test);
            }

            foreach (var (category, tests) in categories)
            {
                Console.WriteLine($"📁 {category}:");
                foreach (var test in tests)
                {
                    var status = test.Passed ? "✅" : "❌";
                    Console.WriteLine($"  {status} {test.TestName}: {test.Score}/100");
                    
                    if (test.Errors.Count > 0)
                    {
                        foreach (var error in test.Errors)
                            Console.WriteLine($"    ⚠️  {error}");
                    }

                    // Show key results
                    foreach (var (key, value) in test.Results.Take(3))
                    {
                        if (value is double d)
                            Console.WriteLine($"    • {key}: {d:F2}");
                        else if (value is int i)
                            Console.WriteLine($"    • {key}: {i:N0}");
                        else if (!key.Contains("Details") && !key.Contains("Data"))
                            Console.WriteLine($"    • {key}: {value}");
                    }
                }
                Console.WriteLine();
            }

            if (report.Errors.Count > 0)
            {
                Console.WriteLine("⚠️  Global Errors:");
                foreach (var error in report.Errors)
                    Console.WriteLine($"  • {error}");
                Console.WriteLine();
            }

            return report.IsValid ? 0 : 1;
        }

        private static async Task<int> RunMonitoringAsync(string databasePath, ILogger logger, CancellationToken ct)
        {
            Console.WriteLine("📈 Starting performance monitoring...");
            Console.WriteLine("Press Ctrl+C to stop monitoring");
            Console.WriteLine();

            var monitor = new StooqPerformanceMonitor(databasePath, (ILogger<StooqPerformanceMonitor>)logger);

            var monitoringTask = Task.Run(async () =>
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        var health = await monitor.GetHealthStatusAsync(ct);
                        var trends = monitor.GetPerformanceTrends();

                        Console.Clear();
                        Console.WriteLine($"📊 Performance Dashboard - {DateTime.Now:HH:mm:ss}");
                        Console.WriteLine("═══════════════════════════════════════");
                        Console.WriteLine();

                        // Health status
                        var healthIcon = health.IsHealthy ? "✅" : "❌";
                        Console.WriteLine($"{healthIcon} System Health: {(health.IsHealthy ? "HEALTHY" : "UNHEALTHY")}");
                        Console.WriteLine($"   Response Time: {health.ResponseTimeMs}ms");
                        Console.WriteLine($"   Total Records: {health.TotalRecords:N0}");
                        Console.WriteLine($"   Database Size: {health.DatabaseSizeMB:F1} MB");
                        Console.WriteLine($"   Data Age: {health.LatestDataAge.TotalDays:F1} days");
                        Console.WriteLine();

                        // Trends
                        if (trends.HasSufficientData)
                        {
                            var trendIcon = trends.OverallTrend switch
                            {
                                "IMPROVING" => "📈",
                                "DEGRADING" => "📉",
                                _ => "📊"
                            };

                            Console.WriteLine($"{trendIcon} Performance Trends ({trends.DataPoints} data points):");
                            Console.WriteLine($"   Overall: {trends.OverallTrend}");
                            Console.WriteLine($"   Query Time: {trends.QueryTimeTrend:+0.0;-0.0;0.0}ms trend");
                            Console.WriteLine($"   Data Quality: {trends.DataQualityTrend:+0.00%;-0.00%;0.00%} trend");
                            Console.WriteLine($"   Error Rate: {trends.ErrorRateTrend:+0.00%;-0.00%;0.00%} trend");
                        }
                        else
                        {
                            Console.WriteLine("📊 Performance Trends: Collecting data...");
                        }

                        Console.WriteLine();
                        Console.WriteLine("Press Ctrl+C to exit monitoring");

                        await Task.Delay(TimeSpan.FromSeconds(30), ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️  Monitoring error: {ex.Message}");
                        await Task.Delay(TimeSpan.FromMinutes(1), ct);
                    }
                }
            }, ct);

            await monitoringTask;
            monitor.Dispose();

            return 0;
        }

        private static async Task<int> RunBenchmarkAsync(string databasePath, ILogger logger, CancellationToken ct)
        {
            Console.WriteLine("⚡ Running performance benchmark...");
            Console.WriteLine();

            var monitor = new StooqPerformanceMonitor(databasePath, (ILogger<StooqPerformanceMonitor>)logger);
            
            // Run benchmark
            var benchmark = await monitor.RunBenchmarkAsync(ct);
            
            Console.WriteLine($"📊 Benchmark Results");
            Console.WriteLine($"Duration: {benchmark.TotalDuration.TotalSeconds:F2} seconds");
            Console.WriteLine($"Overall Score: {benchmark.OverallScore}/100");
            Console.WriteLine();

            // Basic queries
            Console.WriteLine("🔍 Basic Query Performance:");
            foreach (var (name, time) in benchmark.BasicQueryTimes)
            {
                var status = time < 0 ? "❌" : (time > 1000 ? "⚠️ " : "✅");
                var timeStr = time < 0 ? "ERROR" : $"{time}ms";
                Console.WriteLine($"  {status} {name}: {timeStr}");
            }
            Console.WriteLine();

            // Analytical queries
            Console.WriteLine("📈 Analytical Query Performance:");
            foreach (var (name, time) in benchmark.AnalyticalQueryTimes)
            {
                var status = time < 0 ? "❌" : (time > 2000 ? "⚠️ " : "✅");
                var timeStr = time < 0 ? "ERROR" : $"{time}ms";
                Console.WriteLine($"  {status} {name}: {timeStr}");
            }
            Console.WriteLine();

            // Random access
            Console.WriteLine("🎲 Random Access Performance:");
            if (benchmark.RandomAccessAvgMs >= 0)
            {
                var status = benchmark.RandomAccessAvgMs > 1000 ? "⚠️ " : "✅";
                Console.WriteLine($"  {status} Average: {benchmark.RandomAccessAvgMs:F1}ms");
                Console.WriteLine($"  📊 Max: {benchmark.RandomAccessMaxMs}ms");
            }
            else
            {
                Console.WriteLine("  ❌ Random access test failed");
            }
            Console.WriteLine();

            // Run random access test
            Console.WriteLine("🎯 Running detailed random access test...");
            var randomTest = await monitor.TestRandomAccessAsync(100, ct);
            
            var accessStatus = randomTest.IsAcceptable ? "✅" : "❌";
            Console.WriteLine($"  {accessStatus} Success Rate: {randomTest.SuccessRate:P1}");
            Console.WriteLine($"  📊 Average: {randomTest.AverageAccessTimeMs:F1}ms");
            Console.WriteLine($"  📊 Median: {randomTest.MedianAccessTimeMs}ms");
            Console.WriteLine($"  📊 Range: {randomTest.MinAccessTimeMs}ms - {randomTest.MaxAccessTimeMs}ms");

            if (benchmark.ErrorMessages.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("⚠️  Errors encountered:");
                foreach (var error in benchmark.ErrorMessages)
                    Console.WriteLine($"  • {error}");
            }

            return benchmark.OverallScore >= 60 ? 0 : 1;
        }

        private static async Task<int> CheckHealthAsync(string databasePath, ILogger logger, CancellationToken ct)
        {
            Console.WriteLine("🩺 Checking system health...");

            var monitor = new StooqPerformanceMonitor(databasePath, (ILogger<StooqPerformanceMonitor>)logger);
            var health = await monitor.GetHealthStatusAsync(ct);

            var status = health.IsHealthy ? "✅ HEALTHY" : "❌ UNHEALTHY";
            Console.WriteLine($"Status: {status}");
            Console.WriteLine($"Response Time: {health.ResponseTimeMs}ms");
            Console.WriteLine($"Total Records: {health.TotalRecords:N0}");
            Console.WriteLine($"Database Size: {health.DatabaseSizeMB:F1} MB");
            Console.WriteLine($"Free Space: {health.FreeSpaceMB:F1} MB");
            Console.WriteLine($"Latest Data Age: {health.LatestDataAge.TotalDays:F1} days");
            
            if (health.IsDataStale)
                Console.WriteLine("⚠️  Data appears stale (>7 days old)");

            if (!string.IsNullOrEmpty(health.ErrorMessage))
                Console.WriteLine($"Error: {health.ErrorMessage}");

            return health.IsHealthy ? 0 : 1;
        }

        private static ValidationOptions ParseArguments(string[] args)
        {
            var options = new ValidationOptions();

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "--help":
                    case "-h":
                        ShowUsage();
                        Environment.Exit(0);
                        break;
                    case "--verbose":
                    case "-v":
                        options.Verbose = true;
                        break;
                    case "--mode":
                    case "-m":
                        if (i + 1 < args.Length)
                            options.Mode = args[++i].ToLowerInvariant();
                        break;
                    default:
                        if (!args[i].StartsWith("-") && string.IsNullOrEmpty(options.DatabasePath))
                            options.DatabasePath = args[i];
                        break;
                }
            }

            // Default database path
            if (string.IsNullOrEmpty(options.DatabasePath))
                options.DatabasePath = Path.Combine(Directory.GetCurrentDirectory(), "market_data.db");

            return options;
        }

        private static ILogger CreateLogger(bool verbose)
        {
            var factory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Information)
                       .AddConsole(options => 
                       {
                           options.LogToStandardErrorThreshold = LogLevel.Warning;
                       });
            });

            return factory.CreateLogger("StooqValidator");
        }

        private static int ShowUsage()
        {
            Console.WriteLine("🧬 ODTE Stooq Data Validation Tool");
            Console.WriteLine();
            Console.WriteLine("Usage: dotnet run [database_path] [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --mode, -m <mode>     Validation mode: validate, monitor, benchmark, health");
            Console.WriteLine("  --verbose, -v         Enable verbose logging");
            Console.WriteLine("  --help, -h           Show this help message");
            Console.WriteLine();
            Console.WriteLine("Modes:");
            Console.WriteLine("  validate    Run comprehensive data validation tests");
            Console.WriteLine("  monitor     Continuous performance monitoring");
            Console.WriteLine("  benchmark   Run performance benchmark suite");
            Console.WriteLine("  health      Quick health check");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  dotnet run market_data.db --mode validate");
            Console.WriteLine("  dotnet run --mode monitor --verbose");
            Console.WriteLine("  dotnet run /path/to/db.sqlite --mode benchmark");

            return 0;
        }
    }

    public class ValidationOptions
    {
        public string DatabasePath { get; set; } = "";
        public string Mode { get; set; } = "validate";
        public bool Verbose { get; set; } = false;
    }
}