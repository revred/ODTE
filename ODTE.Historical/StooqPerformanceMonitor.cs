// StooqPerformanceMonitor.cs — Continuous performance monitoring and alerting
// Tracks query performance trends, data quality degradation, and system health

using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Dapper;

namespace ODTE.Historical.Monitoring
{
    /// <summary>
    /// Continuous performance monitoring system for Stooq data operations
    /// Provides real-time health checks, performance trending, and alerting
    /// </summary>
    public sealed class StooqPerformanceMonitor
    {
        private readonly string _connectionString;
        private readonly ILogger<StooqPerformanceMonitor> _logger;
        private readonly Timer? _monitoringTimer;
        private readonly List<PerformanceSnapshot> _performanceHistory = new();
        private readonly object _lockObject = new();

        // Performance thresholds (configurable)
        public int QueryTimeoutMs { get; set; } = 5000;
        public int SlowQueryThresholdMs { get; set; } = 1000;
        public double DataQualityThreshold { get; set; } = 0.95; // 95%
        public int MaxHistoryPoints { get; set; } = 100;

        public StooqPerformanceMonitor(string databasePath, ILogger<StooqPerformanceMonitor> logger)
        {
            _connectionString = $"Data Source={databasePath};Version=3;Journal Mode=WAL;Cache Size=64000;Temp Store=Memory";
            _logger = logger;

            // Start monitoring every 5 minutes
            _monitoringTimer = new Timer(async _ => await MonitorPerformanceAsync(), null, 
                TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// Get current system health status
        /// </summary>
        public async Task<HealthStatus> GetHealthStatusAsync(CancellationToken ct = default)
        {
            var health = new HealthStatus { CheckTime = DateTime.UtcNow };

            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                await connection.OpenAsync(ct);

                // Quick connectivity test
                var sw = Stopwatch.StartNew();
                var recordCount = await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM underlying_quotes");
                sw.Stop();

                health.IsHealthy = sw.ElapsedMilliseconds < QueryTimeoutMs && recordCount > 0;
                health.ResponseTimeMs = sw.ElapsedMilliseconds;
                health.TotalRecords = recordCount;

                // Check database size
                var dbInfo = await connection.QuerySingleOrDefaultAsync<DatabaseInfo>(@"
                    SELECT 
                        page_count * page_size as database_size,
                        freelist_count * page_size as free_space
                    FROM pragma_page_count(), pragma_page_size(), pragma_freelist_count()");

                if (dbInfo != null)
                {
                    health.DatabaseSizeMB = dbInfo.DatabaseSize / 1024.0 / 1024.0;
                    health.FreeSpaceMB = dbInfo.FreeSpace / 1024.0 / 1024.0;
                }

                // Check for recent data
                var latestTimestamp = await connection.QuerySingleOrDefaultAsync<long?>(
                    "SELECT MAX(timestamp) FROM underlying_quotes");

                if (latestTimestamp.HasValue)
                {
                    var latestDate = DateTimeOffset.FromUnixTimeMilliseconds(latestTimestamp.Value / 1000).DateTime;
                    health.LatestDataAge = DateTime.UtcNow - latestDate;
                    health.IsDataStale = health.LatestDataAge > TimeSpan.FromDays(7);
                }

                _logger.LogDebug($"Health check: {(health.IsHealthy ? "HEALTHY" : "UNHEALTHY")} - {health.ResponseTimeMs}ms");
            }
            catch (Exception ex)
            {
                health.IsHealthy = false;
                health.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Health check failed");
            }

            return health;
        }

        /// <summary>
        /// Run performance benchmark suite
        /// </summary>
        public async Task<PerformanceBenchmark> RunBenchmarkAsync(CancellationToken ct = default)
        {
            var benchmark = new PerformanceBenchmark { StartTime = DateTime.UtcNow };

            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                await connection.OpenAsync(ct);

                // Benchmark common query patterns
                await BenchmarkBasicQueries(connection, benchmark);
                await BenchmarkAnalyticalQueries(connection, benchmark);
                await BenchmarkRandomAccess(connection, benchmark);

                benchmark.EndTime = DateTime.UtcNow;
                benchmark.TotalDuration = benchmark.EndTime - benchmark.StartTime;
                benchmark.OverallScore = CalculateBenchmarkScore(benchmark);

                _logger.LogInformation($"Benchmark completed: {benchmark.OverallScore}% score in {benchmark.TotalDuration.TotalSeconds:F2}s");
            }
            catch (Exception ex)
            {
                benchmark.ErrorMessage = ex.Message;
                benchmark.OverallScore = 0;
                _logger.LogError(ex, "Benchmark failed");
            }

            return benchmark;
        }

        /// <summary>
        /// Test random data access patterns for performance validation
        /// </summary>
        public async Task<RandomAccessTest> TestRandomAccessAsync(int testCount = 50, CancellationToken ct = default)
        {
            var test = new RandomAccessTest 
            { 
                StartTime = DateTime.UtcNow,
                TestCount = testCount
            };

            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                await connection.OpenAsync(ct);

                // Get total record count for random sampling
                var totalRecords = await connection.QuerySingleAsync<long>("SELECT COUNT(*) FROM underlying_quotes");
                var random = new Random();
                var accessTimes = new List<long>();

                for (int i = 0; i < testCount; i++)
                {
                    // Generate random offset
                    var randomOffset = random.NextInt64(0, Math.Max(1, totalRecords - 1));

                    var sw = Stopwatch.StartNew();
                    
                    // Random access query
                    var sample = await connection.QuerySingleOrDefaultAsync(@"
                        SELECT underlying_id, timestamp, close, volume
                        FROM underlying_quotes
                        LIMIT 1 OFFSET @Offset", new { Offset = randomOffset });

                    sw.Stop();
                    accessTimes.Add(sw.ElapsedMilliseconds);

                    if (sample == null)
                    {
                        test.FailedAccesses++;
                    }
                    else
                    {
                        test.SuccessfulAccesses++;
                    }
                }

                test.AverageAccessTimeMs = accessTimes.Average();
                test.MaxAccessTimeMs = accessTimes.Max();
                test.MinAccessTimeMs = accessTimes.Min();
                test.MedianAccessTimeMs = accessTimes.OrderBy(x => x).Skip(accessTimes.Count / 2).FirstOrDefault();

                test.SuccessRate = test.SuccessfulAccesses / (double)testCount;
                test.IsAcceptable = test.AverageAccessTimeMs < SlowQueryThresholdMs && test.SuccessRate >= 0.95;

                test.EndTime = DateTime.UtcNow;

                _logger.LogDebug($"Random access test: {test.AverageAccessTimeMs:F1}ms avg, {test.SuccessRate:P1} success rate");
            }
            catch (Exception ex)
            {
                test.ErrorMessage = ex.Message;
                test.IsAcceptable = false;
                _logger.LogError(ex, "Random access test failed");
            }

            return test;
        }

        /// <summary>
        /// Get performance history and trends
        /// </summary>
        public PerformanceTrends GetPerformanceTrends()
        {
            lock (_lockObject)
            {
                if (_performanceHistory.Count < 2)
                {
                    return new PerformanceTrends
                    {
                        HasSufficientData = false,
                        Message = "Insufficient historical data for trend analysis"
                    };
                }

                var recent = _performanceHistory.TakeLast(10).ToArray();
                var older = _performanceHistory.Take(_performanceHistory.Count - 10).TakeLast(10).ToArray();

                var trends = new PerformanceTrends
                {
                    HasSufficientData = true,
                    DataPoints = _performanceHistory.Count,
                    TimeSpan = _performanceHistory.Last().Timestamp - _performanceHistory.First().Timestamp
                };

                if (older.Any() && recent.Any())
                {
                    trends.QueryTimeTrend = recent.Average(r => r.AverageQueryTime) - older.Average(o => o.AverageQueryTime);
                    trends.DataQualityTrend = recent.Average(r => r.DataQuality) - older.Average(o => o.DataQuality);
                    trends.ErrorRateTrend = recent.Average(r => r.ErrorRate) - older.Average(o => o.ErrorRate);

                    // Determine overall trend
                    var negativeIndicators = 0;
                    if (trends.QueryTimeTrend > 100) negativeIndicators++; // Query time getting worse
                    if (trends.DataQualityTrend < -0.05) negativeIndicators++; // Quality degrading
                    if (trends.ErrorRateTrend > 0.02) negativeIndicators++; // More errors

                    trends.OverallTrend = negativeIndicators switch
                    {
                        0 => "IMPROVING",
                        1 => "STABLE", 
                        _ => "DEGRADING"
                    };
                }

                return trends;
            }
        }

        /// <summary>
        /// Periodic performance monitoring (called by timer)
        /// </summary>
        private async Task MonitorPerformanceAsync()
        {
            try
            {
                var snapshot = new PerformanceSnapshot { Timestamp = DateTime.UtcNow };

                // Quick health check
                var health = await GetHealthStatusAsync();
                snapshot.IsHealthy = health.IsHealthy;
                snapshot.ResponseTime = health.ResponseTimeMs;

                // Quick benchmark
                using var connection = new SQLiteConnection(_connectionString);
                await connection.OpenAsync();

                var sw = Stopwatch.StartNew();
                var sampleQuery = await connection.QuerySingleOrDefaultAsync("SELECT AVG(close) FROM underlying_quotes LIMIT 1000");
                sw.Stop();

                snapshot.AverageQueryTime = sw.ElapsedMilliseconds;

                // Data quality spot check
                var qualityCheck = await connection.QuerySingleOrDefaultAsync<QualityCheck>(@"
                    SELECT 
                        COUNT(*) as total_records,
                        SUM(CASE WHEN close > 0 AND high >= close AND low <= close THEN 1 ELSE 0 END) as valid_records
                    FROM underlying_quotes 
                    WHERE timestamp >= @CutoffTime
                    LIMIT 1000", new { CutoffTime = ((DateTimeOffset)DateTime.UtcNow.AddDays(-1)).ToUnixTimeMilliseconds() * 1000 });

                if (qualityCheck != null && qualityCheck.TotalRecords > 0)
                {
                    snapshot.DataQuality = qualityCheck.ValidRecords / (double)qualityCheck.TotalRecords;
                }

                snapshot.ErrorRate = snapshot.IsHealthy ? 0.0 : 1.0;

                // Store snapshot
                lock (_lockObject)
                {
                    _performanceHistory.Add(snapshot);
                    if (_performanceHistory.Count > MaxHistoryPoints)
                    {
                        _performanceHistory.RemoveAt(0);
                    }
                }

                // Check for alerts
                CheckForAlerts(snapshot);

                _logger.LogDebug($"Performance snapshot: {snapshot.AverageQueryTime}ms, quality {snapshot.DataQuality:P1}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Performance monitoring failed");
            }
        }

        private async Task BenchmarkBasicQueries(SQLiteConnection connection, PerformanceBenchmark benchmark)
        {
            var queries = new Dictionary<string, string>
            {
                ["Count"] = "SELECT COUNT(*) FROM underlying_quotes",
                ["RecentData"] = "SELECT * FROM underlying_quotes ORDER BY timestamp DESC LIMIT 100",
                ["SymbolLookup"] = "SELECT u.symbol, COUNT(*) FROM underlying_quotes uq JOIN underlyings u ON u.id = uq.underlying_id GROUP BY u.symbol"
            };

            foreach (var (name, query) in queries)
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    await connection.QueryAsync(query);
                    sw.Stop();
                    benchmark.BasicQueryTimes[name] = sw.ElapsedMilliseconds;
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    benchmark.BasicQueryTimes[name] = -1; // Error indicator
                    benchmark.ErrorMessages.Add($"{name}: {ex.Message}");
                }
            }
        }

        private async Task BenchmarkAnalyticalQueries(SQLiteConnection connection, PerformanceBenchmark benchmark)
        {
            var queries = new Dictionary<string, string>
            {
                ["MovingAverage"] = @"
                    SELECT AVG(close) OVER (ORDER BY timestamp ROWS BETWEEN 19 PRECEDING AND CURRENT ROW) as ma20
                    FROM underlying_quotes 
                    WHERE underlying_id = 1 
                    ORDER BY timestamp DESC 
                    LIMIT 100",
                ["Volatility"] = @"
                    SELECT 
                        symbol,
                        STDEV((high - low) / close) as volatility
                    FROM underlying_quotes uq
                    JOIN underlyings u ON u.id = uq.underlying_id
                    WHERE timestamp >= @RecentTime
                    GROUP BY symbol",
                ["PriceRanges"] = @"
                    SELECT 
                        symbol,
                        MIN(close) as min_price,
                        MAX(close) as max_price,
                        AVG(volume) as avg_volume
                    FROM underlying_quotes uq
                    JOIN underlyings u ON u.id = uq.underlying_id
                    GROUP BY symbol
                    ORDER BY avg_volume DESC"
            };

            var recentTime = ((DateTimeOffset)DateTime.UtcNow.AddDays(-30)).ToUnixTimeMilliseconds() * 1000;

            foreach (var (name, query) in queries)
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    await connection.QueryAsync(query, new { RecentTime = recentTime });
                    sw.Stop();
                    benchmark.AnalyticalQueryTimes[name] = sw.ElapsedMilliseconds;
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    benchmark.AnalyticalQueryTimes[name] = -1; // Error indicator
                    benchmark.ErrorMessages.Add($"{name}: {ex.Message}");
                }
            }
        }

        private async Task BenchmarkRandomAccess(SQLiteConnection connection, PerformanceBenchmark benchmark)
        {
            var random = new Random();
            var accessTimes = new List<long>();

            for (int i = 0; i < 20; i++)
            {
                var randomId = random.Next(1, 10); // Assume we have underlying IDs 1-10
                
                var sw = Stopwatch.StartNew();
                try
                {
                    await connection.QuerySingleOrDefaultAsync(
                        "SELECT * FROM underlying_quotes WHERE underlying_id = @Id ORDER BY RANDOM() LIMIT 1",
                        new { Id = randomId });
                    sw.Stop();
                    accessTimes.Add(sw.ElapsedMilliseconds);
                }
                catch
                {
                    sw.Stop();
                    accessTimes.Add(-1); // Error
                }
            }

            var validTimes = accessTimes.Where(t => t >= 0).ToArray();
            benchmark.RandomAccessAvgMs = validTimes.Any() ? validTimes.Average() : -1;
            benchmark.RandomAccessMaxMs = validTimes.Any() ? validTimes.Max() : -1;
        }

        private int CalculateBenchmarkScore(PerformanceBenchmark benchmark)
        {
            var score = 100;

            // Penalize slow basic queries
            foreach (var time in benchmark.BasicQueryTimes.Values)
            {
                if (time > SlowQueryThresholdMs) score -= 10;
                if (time < 0) score -= 20; // Error
            }

            // Penalize slow analytical queries
            foreach (var time in benchmark.AnalyticalQueryTimes.Values)
            {
                if (time > SlowQueryThresholdMs * 2) score -= 15;
                if (time < 0) score -= 25; // Error
            }

            // Penalize slow random access
            if (benchmark.RandomAccessAvgMs > SlowQueryThresholdMs) score -= 20;
            if (benchmark.RandomAccessAvgMs < 0) score -= 30;

            return Math.Max(0, score);
        }

        private void CheckForAlerts(PerformanceSnapshot snapshot)
        {
            var alerts = new List<string>();

            if (!snapshot.IsHealthy)
                alerts.Add("DATABASE_UNHEALTHY");

            if (snapshot.AverageQueryTime > SlowQueryThresholdMs)
                alerts.Add($"SLOW_QUERIES_{snapshot.AverageQueryTime}ms");

            if (snapshot.DataQuality < DataQualityThreshold)
                alerts.Add($"DATA_QUALITY_LOW_{snapshot.DataQuality:P1}");

            if (alerts.Any())
            {
                _logger.LogWarning($"Performance alerts: {string.Join(", ", alerts)}");
            }
        }

        public void Dispose()
        {
            _monitoringTimer?.Dispose();
        }
    }

    // ============================================================================
    // DATA STRUCTURES  
    // ============================================================================

    public class HealthStatus
    {
        public DateTime CheckTime { get; set; }
        public bool IsHealthy { get; set; }
        public long ResponseTimeMs { get; set; }
        public int TotalRecords { get; set; }
        public double DatabaseSizeMB { get; set; }
        public double FreeSpaceMB { get; set; }
        public TimeSpan LatestDataAge { get; set; }
        public bool IsDataStale { get; set; }
        public string ErrorMessage { get; set; } = "";
    }

    public class PerformanceBenchmark
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public Dictionary<string, long> BasicQueryTimes { get; set; } = new();
        public Dictionary<string, long> AnalyticalQueryTimes { get; set; } = new();
        public double RandomAccessAvgMs { get; set; }
        public long RandomAccessMaxMs { get; set; }
        public List<string> ErrorMessages { get; set; } = new();
        public int OverallScore { get; set; }
        public string ErrorMessage { get; set; } = "";
    }

    public class RandomAccessTest
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int TestCount { get; set; }
        public int SuccessfulAccesses { get; set; }
        public int FailedAccesses { get; set; }
        public double SuccessRate { get; set; }
        public double AverageAccessTimeMs { get; set; }
        public long MaxAccessTimeMs { get; set; }
        public long MinAccessTimeMs { get; set; }
        public long MedianAccessTimeMs { get; set; }
        public bool IsAcceptable { get; set; }
        public string ErrorMessage { get; set; } = "";
    }

    public class PerformanceSnapshot
    {
        public DateTime Timestamp { get; set; }
        public bool IsHealthy { get; set; }
        public long ResponseTime { get; set; }
        public double AverageQueryTime { get; set; }
        public double DataQuality { get; set; }
        public double ErrorRate { get; set; }
    }

    public class PerformanceTrends
    {
        public bool HasSufficientData { get; set; }
        public string Message { get; set; } = "";
        public int DataPoints { get; set; }
        public TimeSpan TimeSpan { get; set; }
        public double QueryTimeTrend { get; set; } // Positive = getting slower
        public double DataQualityTrend { get; set; } // Negative = getting worse
        public double ErrorRateTrend { get; set; } // Positive = more errors
        public string OverallTrend { get; set; } = "UNKNOWN"; // IMPROVING, STABLE, DEGRADING
    }

    public class DatabaseInfo
    {
        public long DatabaseSize { get; set; }
        public long FreeSpace { get; set; }
    }

    public class QualityCheck
    {
        public int TotalRecords { get; set; }
        public int ValidRecords { get; set; }
    }
}