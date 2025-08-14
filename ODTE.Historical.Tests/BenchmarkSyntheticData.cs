// BenchmarkSyntheticData.cs — Command-line tool for testing synthetic data generator
// Tests OptionsDataGenerator against real SQLite market data as benchmark

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ODTE.Historical.Validation;

namespace ODTE.Historical.Tests
{
    /// <summary>
    /// Command-line utility for benchmarking synthetic options data generator
    /// Usage: dotnet run benchmark [database_path]
    /// </summary>
    public static class SyntheticDataBenchmarkTool
    {
        public static async Task<int> RunBenchmarkToolAsync(string[] args)
        {
            try
            {
                var databasePath = args.Length > 0 ? args[0] : "../data/ODTE_TimeSeries_5Y.db";
                var logger = CreateLogger();

                Console.WriteLine("🧬 ODTE Synthetic Data Benchmark Tool");
                Console.WriteLine($"Database: {databasePath}");
                Console.WriteLine("Testing OptionsDataGenerator against real market data...");
                Console.WriteLine();

                if (!File.Exists(databasePath))
                {
                    Console.WriteLine($"❌ Database file not found: {databasePath}");
                    return 1;
                }

                var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

                var benchmark = new SyntheticDataBenchmark(databasePath, logger);
                var result = await benchmark.RunBenchmarkAsync();

                // Display comprehensive results
                Console.WriteLine("═══════════════════════════════════════");
                Console.WriteLine($"📊 Synthetic Data Benchmark Report");
                Console.WriteLine($"Benchmark ID: {result.BenchmarkId}");
                Console.WriteLine($"Duration: {result.Duration.TotalSeconds:F2} seconds");
                Console.WriteLine($"Overall Score: {result.OverallScore:F1}/100");
                Console.WriteLine($"Status: {(result.IsAcceptable ? "✅ ACCEPTABLE" : "❌ NEEDS IMPROVEMENT")}");
                Console.WriteLine();

                // Statistical Tests
                Console.WriteLine("📈 Statistical Test Results:");
                Console.WriteLine($"  Mean Difference Score: {result.StatisticalTests.MeanDifferenceScore:F1}/100");
                Console.WriteLine($"    • Historical Mean: {result.StatisticalTests.HistoricalMean:F6}");
                Console.WriteLine($"    • Synthetic Mean: {result.StatisticalTests.SyntheticMean:F6}");
                Console.WriteLine();
                
                Console.WriteLine($"  Volatility Match Score: {result.StatisticalTests.VolatilityMatchScore:F1}/100");
                Console.WriteLine($"    • Historical Std: {result.StatisticalTests.HistoricalStdDev:F6}");
                Console.WriteLine($"    • Synthetic Std: {result.StatisticalTests.SyntheticStdDev:F6}");
                Console.WriteLine();

                Console.WriteLine($"  Skewness Match Score: {result.StatisticalTests.SkewnessMatchScore:F1}/100");
                Console.WriteLine($"    • Historical: {result.StatisticalTests.HistoricalSkewness:F3}");
                Console.WriteLine($"    • Synthetic: {result.StatisticalTests.SyntheticSkewness:F3}");
                Console.WriteLine();

                Console.WriteLine($"  Kurtosis Match Score: {result.StatisticalTests.KurtosisMatchScore:F1}/100");
                Console.WriteLine($"    • Historical: {result.StatisticalTests.HistoricalKurtosis:F3}");
                Console.WriteLine($"    • Synthetic: {result.StatisticalTests.SyntheticKurtosis:F3}");
                Console.WriteLine();

                // Volatility Analysis
                Console.WriteLine("📊 Volatility Analysis:");
                Console.WriteLine($"  Volatility Clustering Score: {result.VolatilityAnalysis.VolClusteringScore:F1}/100");
                Console.WriteLine($"  Mean Reversion Score: {result.VolatilityAnalysis.VolMeanReversionScore:F1}/100");
                Console.WriteLine($"  Historical Avg Vol: {result.VolatilityAnalysis.HistoricalAvgVol:F3}");
                Console.WriteLine($"  Synthetic Avg Vol: {result.VolatilityAnalysis.SyntheticAvgVol:F3}");
                Console.WriteLine($"  Vol Correlation: {result.VolatilityAnalysis.VolatilityCorrelation:F3}");
                Console.WriteLine();

                // Distribution Tests
                Console.WriteLine("📋 Distribution Tests:");
                Console.WriteLine($"  Kolmogorov-Smirnov Score: {result.DistributionTests.KolmogorovSmirnovScore:F1}/100");
                Console.WriteLine($"  Tail Risk Score: {result.DistributionTests.TailRiskScore:F1}/100");
                Console.WriteLine($"  VaR Accuracy: {result.DistributionTests.VaRAccuracy:F1}/100");
                Console.WriteLine($"  Normality Test Score: {result.DistributionTests.JarqueBeraScore:F1}/100");
                Console.WriteLine();

                // Regime Tests
                Console.WriteLine("🎭 Market Regime Tests:");
                Console.WriteLine($"  Trend Detection Score: {result.RegimeTests.TrendDetectionScore:F1}/100");
                Console.WriteLine($"  Volatility Regime Score: {result.RegimeTests.VolatilityRegimeScore:F1}/100");
                Console.WriteLine($"  Crisis Detection Score: {result.RegimeTests.CrisisDetectionScore:F1}/100");
                Console.WriteLine($"  Mean Reversion Score: {result.RegimeTests.MeanReversionScore:F1}/100");
                Console.WriteLine();

                // Quality Assessment
                if (result.OverallScore >= 85)
                {
                    Console.WriteLine("🎯 ASSESSMENT: EXCELLENT");
                    Console.WriteLine("  ✅ Synthetic data quality exceeds industry standards");
                    Console.WriteLine("  ✅ Ready for production backtesting");
                    Console.WriteLine("  ✅ Statistical properties match real market data");
                }
                else if (result.OverallScore >= 75)
                {
                    Console.WriteLine("🎯 ASSESSMENT: GOOD");
                    Console.WriteLine("  ✅ Synthetic data quality is acceptable for production");
                    Console.WriteLine("  ⚠️  Minor improvements could enhance realism");
                    Console.WriteLine("  ✅ Safe for strategy validation");
                }
                else if (result.OverallScore >= 60)
                {
                    Console.WriteLine("🎯 ASSESSMENT: NEEDS IMPROVEMENT");
                    Console.WriteLine("  ⚠️  Synthetic data has significant deviations");
                    Console.WriteLine("  ⚠️  Risk of overfit strategies in backtesting");
                    Console.WriteLine("  🔧 Calibration adjustments recommended");
                }
                else
                {
                    Console.WriteLine("🎯 ASSESSMENT: POOR");
                    Console.WriteLine("  ❌ Synthetic data does not match market reality");
                    Console.WriteLine("  ❌ Not suitable for strategy validation");
                    Console.WriteLine("  🚨 Major model revision required");
                }

                Console.WriteLine();
                Console.WriteLine("💡 RECOMMENDATIONS:");
                
                if (result.StatisticalTests.VolatilityMatchScore < 70)
                {
                    Console.WriteLine("  • Adjust volatility surface calibration parameters");
                }
                
                if (result.DistributionTests.TailRiskScore < 70)
                {
                    Console.WriteLine("  • Enhance jump-diffusion model for tail events");
                }
                
                if (result.RegimeTests.VolatilityRegimeScore < 70)
                {
                    Console.WriteLine("  • Improve market regime detection sensitivity");
                }
                
                if (result.VolatilityAnalysis.VolClusteringScore < 70)
                {
                    Console.WriteLine("  • Implement GARCH-style volatility clustering");
                }

                Console.WriteLine();
                Console.WriteLine("📁 For detailed analysis, see the validation framework in:");
                Console.WriteLine("   • SyntheticDataBenchmark.cs");
                Console.WriteLine("   • OptionsDataGenerator.cs");
                Console.WriteLine("   • OPTIONS_DATA_QUALITY_RESEARCH.md");

                return result.IsAcceptable ? 0 : 1;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("\n🛑 Benchmark cancelled by user");
                return 130;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Benchmark failed: {ex.Message}");
                return 1;
            }
        }

        private static ILogger<SyntheticDataBenchmark> CreateLogger()
        {
            var factory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Information)
                       .AddConsole(options => 
                       {
                           options.LogToStandardErrorThreshold = LogLevel.Warning;
                       });
            });

            return factory.CreateLogger<SyntheticDataBenchmark>();
        }
    }
}