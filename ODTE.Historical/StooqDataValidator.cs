// StooqDataValidator.cs — Random validation checks for Stooq data quality and performance
// Validates data integrity, query performance, and statistical correctness

using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Dapper;

namespace ODTE.Historical.Validation
{
    /// <summary>
    /// Comprehensive validation system for Stooq data quality and SQLite query performance
    /// Performs random sampling, statistical validation, and performance benchmarking
    /// </summary>
    public sealed class StooqDataValidator
    {
        private readonly string _connectionString;
        private readonly ILogger<StooqDataValidator> _logger;
        private readonly Random _random;

        public StooqDataValidator(string databasePath, ILogger<StooqDataValidator> logger)
        {
            _connectionString = $"Data Source={databasePath};Version=3;Journal Mode=WAL;Cache Size=64000;Temp Store=Memory";
            _logger = logger;
            _random = new Random();
        }

        /// <summary>
        /// Run comprehensive validation suite with random checks
        /// </summary>
        public async Task<ValidationReport> RunFullValidationAsync(CancellationToken ct = default)
        {
            _logger.LogInformation("Starting comprehensive Stooq data validation");
            var stopwatch = Stopwatch.StartNew();
            
            var report = new ValidationReport
            {
                StartTime = DateTime.UtcNow,
                ValidationId = Guid.NewGuid().ToString("N")[..8]
            };

            try
            {
                // 1. Basic connectivity and schema validation
                await ValidateSchemaAndConnectivity(report, ct);

                // 2. Random data sampling and quality checks
                await ValidateRandomDataSamples(report, ct);

                // 3. Statistical validation (OHLCV relationships)
                await ValidateStatisticalProperties(report, ct);

                // 4. Performance benchmarking
                await BenchmarkQueryPerformance(report, ct);

                // 5. Data completeness and gaps
                await ValidateDataCompleteness(report, ct);

                // 6. Cross-validation with known market events
                await ValidateMarketEvents(report, ct);

                report.OverallScore = CalculateOverallScore(report);
                report.IsValid = report.OverallScore >= 75; // 75% threshold for validity

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Validation failed with exception");
                report.Errors.Add($"Validation exception: {ex.Message}");
                report.IsValid = false;
            }
            finally
            {
                report.EndTime = DateTime.UtcNow;
                report.TotalDuration = stopwatch.Elapsed;
                _logger.LogInformation($"Validation completed in {report.TotalDuration.TotalSeconds:F2}s with score {report.OverallScore}");
            }

            return report;
        }

        /// <summary>
        /// Validate basic schema structure and connectivity
        /// </summary>
        private async Task ValidateSchemaAndConnectivity(ValidationReport report, CancellationToken ct)
        {
            var test = new ValidationTest
            {
                TestName = "Schema and Connectivity",
                Category = "Infrastructure"
            };

            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                await connection.OpenAsync(ct);

                // Check required tables exist
                var requiredTables = new[] { "underlyings", "underlying_quotes" };
                foreach (var table in requiredTables)
                {
                    var count = await connection.QuerySingleAsync<int>(
                        $"SELECT COUNT(name) FROM sqlite_master WHERE type='table' AND name='{table}'");
                    
                    if (count == 0)
                    {
                        test.Errors.Add($"Required table '{table}' not found");
                    }
                }

                // Test basic query performance
                var sw = Stopwatch.StartNew();
                var underlyingCount = await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM underlyings");
                var quoteCount = await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM underlying_quotes");
                sw.Stop();

                test.Results.Add("UnderlyingCount", underlyingCount);
                test.Results.Add("QuoteCount", quoteCount);
                test.Results.Add("BasicQueryTime", sw.ElapsedMilliseconds);

                test.Passed = test.Errors.Count == 0 && underlyingCount > 0 && quoteCount > 0;
                test.Score = test.Passed ? 100 : 0;

                _logger.LogDebug($"Schema validation: {underlyingCount} underlyings, {quoteCount} quotes");
            }
            catch (Exception ex)
            {
                test.Errors.Add($"Schema validation failed: {ex.Message}");
                test.Passed = false;
                test.Score = 0;
            }

            report.Tests.Add(test);
        }

        /// <summary>
        /// Random sampling of data for quality validation
        /// </summary>
        private async Task ValidateRandomDataSamples(ValidationReport report, CancellationToken ct)
        {
            var test = new ValidationTest
            {
                TestName = "Random Data Sampling",
                Category = "Data Quality"
            };

            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                await connection.OpenAsync(ct);

                // Get random sample of quotes for validation
                var sampleSize = 100;
                var totalQuotes = await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM underlying_quotes");
                
                if (totalQuotes < sampleSize)
                {
                    test.Errors.Add($"Insufficient data: {totalQuotes} quotes, need at least {sampleSize}");
                    test.Passed = false;
                    test.Score = 0;
                    report.Tests.Add(test);
                    return;
                }

                // Random sampling using TABLESAMPLE alternative for SQLite
                var samples = await connection.QueryAsync<QuoteSample>(@"
                    SELECT uq.underlying_id, uq.timestamp, uq.open, uq.high, uq.low, uq.close, uq.volume,
                           u.symbol
                    FROM underlying_quotes uq
                    JOIN underlyings u ON u.id = uq.underlying_id
                    WHERE uq.id IN (
                        SELECT id FROM underlying_quotes 
                        ORDER BY RANDOM() 
                        LIMIT @SampleSize
                    )", new { SampleSize = sampleSize });

                var validSamples = 0;
                var priceErrors = 0;
                var volumeErrors = 0;

                foreach (var sample in samples)
                {
                    var isValid = true;

                    // Validate OHLC relationships
                    if (sample.High < Math.Max(sample.Open, sample.Close) || 
                        sample.Low > Math.Min(sample.Open, sample.Close))
                    {
                        priceErrors++;
                        isValid = false;
                    }

                    // Validate positive prices
                    if (sample.Open <= 0 || sample.High <= 0 || sample.Low <= 0 || sample.Close <= 0)
                    {
                        priceErrors++;
                        isValid = false;
                    }

                    // Validate reasonable volume (allow zero for some instruments)
                    if (sample.Volume < 0)
                    {
                        volumeErrors++;
                        isValid = false;
                    }

                    if (isValid) validSamples++;
                }

                var validityRate = (double)validSamples / samples.Count() * 100;

                test.Results.Add("SampleSize", samples.Count());
                test.Results.Add("ValidSamples", validSamples);
                test.Results.Add("ValidityRate", validityRate);
                test.Results.Add("PriceErrors", priceErrors);
                test.Results.Add("VolumeErrors", volumeErrors);

                test.Passed = validityRate >= 95; // 95% validity threshold
                test.Score = (int)Math.Min(validityRate, 100);

                _logger.LogDebug($"Random sampling: {validityRate:F1}% valid, {priceErrors} price errors, {volumeErrors} volume errors");
            }
            catch (Exception ex)
            {
                test.Errors.Add($"Random sampling failed: {ex.Message}");
                test.Passed = false;
                test.Score = 0;
            }

            report.Tests.Add(test);
        }

        /// <summary>
        /// Statistical validation of market data properties
        /// </summary>
        private async Task ValidateStatisticalProperties(ValidationReport report, CancellationToken ct)
        {
            var test = new ValidationTest
            {
                TestName = "Statistical Properties",
                Category = "Market Data"
            };

            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                await connection.OpenAsync(ct);

                // Get major symbols for statistical analysis
                var majorSymbols = new[] { "SPY", "QQQ", "IWM", "VIX" };
                var statisticsResults = new Dictionary<string, SymbolStatistics>();

                foreach (var symbol in majorSymbols)
                {
                    var stats = await connection.QuerySingleOrDefaultAsync<RawStatistics>(@"
                        SELECT 
                            COUNT(*) as RecordCount,
                            AVG(close) as AvgClose,
                            AVG((high - low) / close) as AvgTrueRangeRatio,
                            AVG(volume) as AvgVolume,
                            MIN(timestamp) as MinTimestamp,
                            MAX(timestamp) as MaxTimestamp
                        FROM underlying_quotes uq
                        JOIN underlyings u ON u.id = uq.underlying_id
                        WHERE u.symbol = @Symbol AND close > 0", new { Symbol = symbol });

                    if (stats != null && stats.RecordCount > 0)
                    {
                        // Calculate additional statistics
                        var returns = await connection.QueryAsync<double>(@"
                            SELECT 
                                (close - LAG(close) OVER (ORDER BY timestamp)) / LAG(close) OVER (ORDER BY timestamp) as return_rate
                            FROM underlying_quotes uq
                            JOIN underlyings u ON u.id = uq.underlying_id
                            WHERE u.symbol = @Symbol AND close > 0
                            ORDER BY timestamp
                            LIMIT 1000", new { Symbol = symbol });

                        var validReturns = returns.Where(r => !double.IsNaN(r) && !double.IsInfinity(r)).ToArray();
                        var volatility = validReturns.Any() ? validReturns.StandardDeviation() * Math.Sqrt(252) : 0;

                        statisticsResults[symbol] = new SymbolStatistics
                        {
                            Symbol = symbol,
                            RecordCount = stats.RecordCount,
                            AvgPrice = stats.AvgClose,
                            AnnualizedVolatility = volatility,
                            AvgTrueRangeRatio = stats.AvgTrueRangeRatio,
                            AvgVolume = stats.AvgVolume,
                            DataSpanDays = (FromMicroseconds(stats.MaxTimestamp) - FromMicroseconds(stats.MinTimestamp)).TotalDays
                        };
                    }
                }

                // Validate statistical properties
                var validSymbols = 0;
                var totalSymbols = statisticsResults.Count;

                foreach (var (symbol, stats) in statisticsResults)
                {
                    var isValid = true;

                    // Validate reasonable price ranges
                    if (symbol == "SPY" && (stats.AvgPrice < 50 || stats.AvgPrice > 800))
                        isValid = false;
                    else if (symbol == "VIX" && (stats.AvgPrice < 5 || stats.AvgPrice > 200))
                        isValid = false;

                    // Validate reasonable volatility (annualized)
                    if (stats.AnnualizedVolatility < 0.05 || stats.AnnualizedVolatility > 5.0)
                        isValid = false;

                    // Validate sufficient data points
                    if (stats.RecordCount < 100)
                        isValid = false;

                    if (isValid) validSymbols++;

                    _logger.LogDebug($"{symbol}: {stats.RecordCount} records, avg=${stats.AvgPrice:F2}, vol={stats.AnnualizedVolatility:F1}%");
                }

                test.Results.Add("TotalSymbolsAnalyzed", totalSymbols);
                test.Results.Add("ValidSymbols", validSymbols);
                test.Results.Add("StatisticsData", System.Text.Json.JsonSerializer.Serialize(statisticsResults));

                test.Passed = validSymbols == totalSymbols && totalSymbols > 0;
                test.Score = totalSymbols > 0 ? (validSymbols * 100 / totalSymbols) : 0;

                _logger.LogDebug($"Statistical validation: {validSymbols}/{totalSymbols} symbols valid");
            }
            catch (Exception ex)
            {
                test.Errors.Add($"Statistical validation failed: {ex.Message}");
                test.Passed = false;
                test.Score = 0;
            }

            report.Tests.Add(test);
        }

        /// <summary>
        /// Performance benchmarking of common queries
        /// </summary>
        private async Task BenchmarkQueryPerformance(ValidationReport report, CancellationToken ct)
        {
            var test = new ValidationTest
            {
                TestName = "Query Performance Benchmark",
                Category = "Performance"
            };

            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                await connection.OpenAsync(ct);

                var benchmarks = new Dictionary<string, long>();

                // Benchmark 1: Simple count query
                var sw = Stopwatch.StartNew();
                await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM underlying_quotes");
                sw.Stop();
                benchmarks["SimpleCount"] = sw.ElapsedMilliseconds;

                // Benchmark 2: Index-based lookup
                sw.Restart();
                await connection.QueryAsync("SELECT * FROM underlying_quotes WHERE underlying_id = 1 ORDER BY timestamp DESC LIMIT 100");
                sw.Stop();
                benchmarks["IndexLookup"] = sw.ElapsedMilliseconds;

                // Benchmark 3: Date range query
                sw.Restart();
                var cutoff = ((DateTimeOffset)DateTime.UtcNow.AddDays(-30)).ToUnixTimeMilliseconds() * 1000;
                await connection.QueryAsync("SELECT * FROM underlying_quotes WHERE timestamp >= @Cutoff LIMIT 1000", new { Cutoff = cutoff });
                sw.Stop();
                benchmarks["DateRangeQuery"] = sw.ElapsedMilliseconds;

                // Benchmark 4: Join query
                sw.Restart();
                await connection.QueryAsync(@"
                    SELECT u.symbol, COUNT(*) 
                    FROM underlying_quotes uq 
                    JOIN underlyings u ON u.id = uq.underlying_id 
                    GROUP BY u.symbol 
                    ORDER BY COUNT(*) DESC 
                    LIMIT 10");
                sw.Stop();
                benchmarks["JoinAggregation"] = sw.ElapsedMilliseconds;

                // Benchmark 5: Complex analytical query
                sw.Restart();
                await connection.QueryAsync(@"
                    SELECT 
                        u.symbol,
                        AVG(close) as avg_close,
                        AVG((high - low) / close) as avg_range_pct
                    FROM underlying_quotes uq
                    JOIN underlyings u ON u.id = uq.underlying_id
                    WHERE timestamp >= @RecentCutoff
                    GROUP BY u.symbol
                    HAVING COUNT(*) > 10
                    ORDER BY avg_close DESC", new { RecentCutoff = cutoff });
                sw.Stop();
                benchmarks["ComplexAnalytical"] = sw.ElapsedMilliseconds;

                // Evaluate performance
                var performanceScore = 100;
                var performanceIssues = new List<string>();

                foreach (var (queryType, elapsed) in benchmarks)
                {
                    var threshold = queryType switch
                    {
                        "SimpleCount" => 50,        // Should be under 50ms
                        "IndexLookup" => 100,       // Should be under 100ms
                        "DateRangeQuery" => 200,    // Should be under 200ms
                        "JoinAggregation" => 300,   // Should be under 300ms
                        "ComplexAnalytical" => 500, // Should be under 500ms
                        _ => 1000
                    };

                    if (elapsed > threshold)
                    {
                        performanceScore -= 20;
                        performanceIssues.Add($"{queryType}: {elapsed}ms (threshold: {threshold}ms)");
                    }

                    test.Results.Add($"{queryType}Ms", elapsed);
                }

                test.Results.Add("PerformanceIssues", string.Join("; ", performanceIssues));
                test.Passed = performanceScore >= 60; // At least 60% performance score
                test.Score = Math.Max(0, performanceScore);

                _logger.LogDebug($"Performance benchmark: {test.Score}% score, issues: {string.Join(", ", performanceIssues)}");
            }
            catch (Exception ex)
            {
                test.Errors.Add($"Performance benchmark failed: {ex.Message}");
                test.Passed = false;
                test.Score = 0;
            }

            report.Tests.Add(test);
        }

        /// <summary>
        /// Validate data completeness and identify gaps
        /// </summary>
        private async Task ValidateDataCompleteness(ValidationReport report, CancellationToken ct)
        {
            var test = new ValidationTest
            {
                TestName = "Data Completeness",
                Category = "Data Quality"
            };

            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                await connection.OpenAsync(ct);

                // Check for major gaps in data
                var symbols = await connection.QueryAsync<string>("SELECT symbol FROM underlyings ORDER BY symbol");
                var completenessResults = new Dictionary<string, CompletenessInfo>();

                foreach (var symbol in symbols.Take(5)) // Check top 5 symbols
                {
                    var dataInfo = await connection.QuerySingleOrDefaultAsync<CompletenessInfo>(@"
                        SELECT 
                            COUNT(*) as RecordCount,
                            MIN(timestamp) as FirstTimestamp,
                            MAX(timestamp) as LastTimestamp
                        FROM underlying_quotes uq
                        JOIN underlyings u ON u.id = uq.underlying_id
                        WHERE u.symbol = @Symbol", new { Symbol = symbol });

                    if (dataInfo != null && dataInfo.RecordCount > 0)
                    {
                        var spanDays = (FromMicroseconds(dataInfo.LastTimestamp) - FromMicroseconds(dataInfo.FirstTimestamp)).TotalDays;
                        var expectedRecords = Math.Max(1, (int)(spanDays / 7) * 5); // Assume 5 trading days per week
                        var completenessRatio = Math.Min(1.0, dataInfo.RecordCount / (double)expectedRecords);

                        dataInfo.CompletenessRatio = completenessRatio;
                        dataInfo.ExpectedRecords = expectedRecords;
                        completenessResults[symbol] = dataInfo;
                    }
                }

                var avgCompleteness = completenessResults.Values.Average(c => c.CompletenessRatio);
                var minCompleteness = completenessResults.Values.Min(c => c.CompletenessRatio);

                test.Results.Add("SymbolsAnalyzed", completenessResults.Count);
                test.Results.Add("AverageCompleteness", avgCompleteness);
                test.Results.Add("MinimumCompleteness", minCompleteness);
                test.Results.Add("CompletenessDetails", System.Text.Json.JsonSerializer.Serialize(completenessResults));

                test.Passed = avgCompleteness >= 0.8 && minCompleteness >= 0.6; // 80% avg, 60% min
                test.Score = (int)(avgCompleteness * 100);

                _logger.LogDebug($"Data completeness: {avgCompleteness:P1} average, {minCompleteness:P1} minimum");
            }
            catch (Exception ex)
            {
                test.Errors.Add($"Completeness validation failed: {ex.Message}");
                test.Passed = false;
                test.Score = 0;
            }

            report.Tests.Add(test);
        }

        /// <summary>
        /// Validate against known market events (basic sanity checks)
        /// </summary>
        private async Task ValidateMarketEvents(ValidationReport report, CancellationToken ct)
        {
            var test = new ValidationTest
            {
                TestName = "Market Events Validation",
                Category = "Market Data"
            };

            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                await connection.OpenAsync(ct);

                // Check for VIX spike during known events (if VIX data exists)
                var vixSpikes = await connection.QueryAsync<MarketEventCheck>(@"
                    SELECT 
                        DATE(timestamp / 1000000, 'unixepoch') as trade_date,
                        close as vix_close,
                        high as vix_high
                    FROM underlying_quotes uq
                    JOIN underlyings u ON u.id = uq.underlying_id
                    WHERE u.symbol = 'VIX' 
                      AND close > 30  -- VIX spike threshold
                    ORDER BY timestamp DESC
                    LIMIT 10");

                // Check for reasonable SPY price progression over time
                var spyProgression = await connection.QueryAsync<MarketEventCheck>(@"
                    SELECT 
                        DATE(timestamp / 1000000, 'unixepoch') as trade_date,
                        close as spy_close
                    FROM underlying_quotes uq
                    JOIN underlyings u ON u.id = uq.underlying_id
                    WHERE u.symbol = 'SPY'
                    ORDER BY timestamp DESC
                    LIMIT 252");  // Approximately 1 year of trading days

                var eventScore = 100;
                var eventIssues = new List<string>();

                // Validate VIX behavior
                if (vixSpikes.Any())
                {
                    var maxVix = vixSpikes.Max(v => v.VixClose ?? v.SpyClose ?? 0);
                    if (maxVix < 20)
                    {
                        eventIssues.Add("No significant VIX spikes found (max < 20)");
                        eventScore -= 20;
                    }
                    test.Results.Add("MaxVixLevel", maxVix);
                    test.Results.Add("VixSpikesCount", vixSpikes.Count());
                }

                // Validate SPY price continuity
                if (spyProgression.Any())
                {
                    var spyPrices = spyProgression.Where(s => s.SpyClose.HasValue)
                                                 .Select(s => s.SpyClose!.Value)
                                                 .ToArray();
                    
                    if (spyPrices.Any())
                    {
                        var maxPrice = spyPrices.Max();
                        var minPrice = spyPrices.Min();
                        var priceRange = maxPrice - minPrice;
                        var avgPrice = spyPrices.Average();

                        // Check for reasonable SPY price range
                        if (minPrice < 50 || maxPrice > 800)
                        {
                            eventIssues.Add($"Unusual SPY price range: ${minPrice:F2} - ${maxPrice:F2}");
                            eventScore -= 20;
                        }

                        test.Results.Add("SpyPriceRange", $"${minPrice:F2} - ${maxPrice:F2}");
                        test.Results.Add("SpyAvgPrice", avgPrice);
                        test.Results.Add("SpyDataPoints", spyPrices.Length);
                    }
                }

                test.Results.Add("EventIssues", string.Join("; ", eventIssues));
                test.Passed = eventScore >= 60;
                test.Score = eventScore;

                _logger.LogDebug($"Market events validation: {eventScore}% score, issues: {string.Join(", ", eventIssues)}");
            }
            catch (Exception ex)
            {
                test.Errors.Add($"Market events validation failed: {ex.Message}");
                test.Passed = false;
                test.Score = 0;
            }

            report.Tests.Add(test);
        }

        /// <summary>
        /// Calculate overall validation score
        /// </summary>
        private int CalculateOverallScore(ValidationReport report)
        {
            if (!report.Tests.Any()) return 0;

            // Weighted scoring
            var weights = new Dictionary<string, double>
            {
                ["Infrastructure"] = 0.2,
                ["Data Quality"] = 0.4,
                ["Performance"] = 0.2,
                ["Market Data"] = 0.2
            };

            var totalScore = 0.0;
            var totalWeight = 0.0;

            foreach (var test in report.Tests)
            {
                var weight = weights.GetValueOrDefault(test.Category, 1.0);
                totalScore += test.Score * weight;
                totalWeight += weight;
            }

            return totalWeight > 0 ? (int)(totalScore / totalWeight) : 0;
        }

        private static DateTime FromMicroseconds(long microseconds)
            => DateTimeOffset.FromUnixTimeMilliseconds(microseconds / 1000).DateTime;
    }

    // ============================================================================
    // DATA STRUCTURES
    // ============================================================================

    public class ValidationReport
    {
        public string ValidationId { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public List<ValidationTest> Tests { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public int OverallScore { get; set; }
        public bool IsValid { get; set; }
    }

    public class ValidationTest
    {
        public string TestName { get; set; } = "";
        public string Category { get; set; } = "";
        public bool Passed { get; set; }
        public int Score { get; set; } // 0-100
        public Dictionary<string, object> Results { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    public class QuoteSample
    {
        public int UnderlyingId { get; set; }
        public long Timestamp { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public long Volume { get; set; }
        public string Symbol { get; set; } = "";
    }

    public class RawStatistics
    {
        public int RecordCount { get; set; }
        public double AvgClose { get; set; }
        public double AvgTrueRangeRatio { get; set; }
        public double AvgVolume { get; set; }
        public long MinTimestamp { get; set; }
        public long MaxTimestamp { get; set; }
    }

    public class SymbolStatistics
    {
        public string Symbol { get; set; } = "";
        public int RecordCount { get; set; }
        public double AvgPrice { get; set; }
        public double AnnualizedVolatility { get; set; }
        public double AvgTrueRangeRatio { get; set; }
        public double AvgVolume { get; set; }
        public double DataSpanDays { get; set; }
    }

    public class CompletenessInfo
    {
        public int RecordCount { get; set; }
        public long FirstTimestamp { get; set; }
        public long LastTimestamp { get; set; }
        public double CompletenessRatio { get; set; }
        public int ExpectedRecords { get; set; }
    }

    public class MarketEventCheck
    {
        public string TradeDate { get; set; } = "";
        public double? VixClose { get; set; }
        public double? SpyClose { get; set; }
    }

    // Extension method for standard deviation calculation
    public static class MathExtensions
    {
        public static double StandardDeviation(this IEnumerable<double> values)
        {
            var array = values.ToArray();
            if (array.Length == 0) return 0;
            
            var mean = array.Average();
            var sumSquaredDeviations = array.Sum(x => Math.Pow(x - mean, 2));
            return Math.Sqrt(sumSquaredDeviations / array.Length);
        }
    }
}