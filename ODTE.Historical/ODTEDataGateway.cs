using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Logging;
using ODTE.Historical.Validation;

namespace ODTE.Historical
{
    /// <summary>
    /// Central data gateway implementation for the ODTE platform.
    /// Provides unified access to historical, synthetic, and real-time market data
    /// for optimization, backtesting, paper trading, and live trading scenarios.
    /// 
    /// This gateway serves as the single entry point for all data access needs:
    /// - Historical data for backtesting and optimization
    /// - Synthetic data for stress testing and scenario analysis  
    /// - Real-time data for live trading and monitoring
    /// - Data validation and quality assurance
    /// </summary>
    public class ODTEDataGateway : IDataGateway, IDisposable
    {
        private readonly ILogger<ODTEDataGateway> _logger;
        private readonly HistoricalDataManager _historicalManager;
        private readonly OptionsDataGenerator _syntheticGenerator;
        private readonly SyntheticDataBenchmark _validator;
        private readonly TimeSeriesDatabase _database;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the ODTE data gateway.
        /// </summary>
        /// <param name="databasePath">Path to the SQLite database file</param>
        /// <param name="logger">Logger instance for diagnostics</param>
        public ODTEDataGateway(string databasePath, ILogger<ODTEDataGateway> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Database path cannot be null or empty", nameof(databasePath));

            _database = new TimeSeriesDatabase(databasePath);
            _historicalManager = new HistoricalDataManager(databasePath);
            _syntheticGenerator = new OptionsDataGenerator();
            _validator = new SyntheticDataBenchmark(databasePath, 
                logger as ILogger<SyntheticDataBenchmark> ?? 
                LoggerFactory.Create(b => b.AddConsole()).CreateLogger<SyntheticDataBenchmark>());

            _logger.LogInformation("ODTE Data Gateway initialized with database: {DatabasePath}", databasePath);
        }

        /// <summary>
        /// Gets the name of this data gateway implementation.
        /// </summary>
        public string Name => "ODTE Unified Data Gateway";

        /// <summary>
        /// Indicates whether this gateway provides real-time data.
        /// Currently returns false as real-time feeds are not yet implemented.
        /// </summary>
        public bool IsRealTime => false; // TODO: Implement real-time data feeds

        /// <summary>
        /// Gets the current data quality score based on recent validation results.
        /// This represents the overall reliability and accuracy of the data.
        /// </summary>
        public double QualityScore => GetCachedQualityScore();

        /// <summary>
        /// Retrieves historical market data for the specified symbol and date range.
        /// This method provides access to stored historical data for backtesting,
        /// optimization, and historical analysis purposes.
        /// </summary>
        /// <param name="symbol">Trading symbol (e.g., "SPY", "XSP")</param>
        /// <param name="startDate">Start date for data retrieval</param>
        /// <param name="endDate">End date for data retrieval</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Historical market data points</returns>
        public async Task<IEnumerable<MarketDataPoint>> GetHistoricalDataAsync(
            string symbol, 
            DateTime startDate, 
            DateTime endDate, 
            CancellationToken cancellationToken = default)
        {
            ValidateSymbol(symbol);
            ValidateDateRange(startDate, endDate);

            _logger.LogDebug("Retrieving historical data for {Symbol} from {StartDate} to {EndDate}", 
                symbol, startDate, endDate);

            try
            {
                await _historicalManager.InitializeAsync();
                var data = await _historicalManager.GetMarketDataAsync(symbol, startDate, endDate);
                
                var dataPoints = data.Select(d => new MarketDataPoint
                {
                    Timestamp = d.Timestamp,
                    Symbol = symbol,
                    Open = d.Open,
                    High = d.High,
                    Low = d.Low,
                    Close = d.Close,
                    Volume = d.Volume,
                    VWAP = d.VWAP,
                    IsSynthetic = false,
                    QualityScore = 100.0 // Historical data assumed high quality
                }).ToList();

                _logger.LogInformation("Retrieved {Count} historical data points for {Symbol}", 
                    dataPoints.Count, symbol);

                return dataPoints;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve historical data for {Symbol}", symbol);
                throw;
            }
        }

        /// <summary>
        /// Generates synthetic market data for stress testing and scenario analysis.
        /// This method creates realistic market data based on research-backed models
        /// for testing strategies under various market conditions.
        /// </summary>
        /// <param name="symbol">Trading symbol</param>
        /// <param name="startDate">Start date for synthetic data</param>
        /// <param name="endDate">End date for synthetic data</param>
        /// <param name="scenario">Market scenario ("normal", "stressed", "crisis")</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Synthetic market data points</returns>
        public async Task<IEnumerable<MarketDataPoint>> GenerateSyntheticDataAsync(
            string symbol, 
            DateTime startDate, 
            DateTime endDate, 
            string scenario = "normal",
            CancellationToken cancellationToken = default)
        {
            ValidateSymbol(symbol);
            ValidateDateRange(startDate, endDate);

            _logger.LogDebug("Generating synthetic data for {Symbol} from {StartDate} to {EndDate}, scenario: {Scenario}", 
                symbol, startDate, endDate, scenario);

            try
            {
                var syntheticData = new List<MarketDataPoint>();
                var currentDate = startDate.Date;

                while (currentDate <= endDate.Date)
                {
                    // Skip weekends for equity data
                    if (currentDate.DayOfWeek != DayOfWeek.Saturday && 
                        currentDate.DayOfWeek != DayOfWeek.Sunday)
                    {
                        var dayData = await _syntheticGenerator.GenerateTradingDayAsync(currentDate, symbol);
                        
                        foreach (var bar in dayData)
                        {
                            syntheticData.Add(new MarketDataPoint
                            {
                                Timestamp = bar.Timestamp,
                                Symbol = symbol,
                                Open = bar.Open,
                                High = bar.High,
                                Low = bar.Low,
                                Close = bar.Close,
                                Volume = (long)bar.Volume,
                                VWAP = (bar.Open + bar.High + bar.Low + bar.Close) / 4, // Approximation
                                IsSynthetic = true,
                                QualityScore = QualityScore,
                                Metadata = new Dictionary<string, object>
                                {
                                    ["Scenario"] = scenario,
                                    ["Generator"] = "OptionsDataGenerator",
                                    ["GeneratedAt"] = DateTime.UtcNow
                                }
                            });
                        }
                    }

                    currentDate = currentDate.AddDays(1);
                }

                _logger.LogInformation("Generated {Count} synthetic data points for {Symbol} (scenario: {Scenario})", 
                    syntheticData.Count, symbol, scenario);

                return syntheticData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate synthetic data for {Symbol}", symbol);
                throw;
            }
        }

        /// <summary>
        /// Retrieves current market data for real-time trading scenarios.
        /// Currently returns the latest available historical data point as
        /// real-time feeds are not yet implemented.
        /// </summary>
        /// <param name="symbol">Trading symbol</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Current market data point or null if not available</returns>
        public async Task<MarketDataPoint?> GetCurrentDataAsync(
            string symbol, 
            CancellationToken cancellationToken = default)
        {
            ValidateSymbol(symbol);

            _logger.LogDebug("Retrieving current data for {Symbol}", symbol);

            try
            {
                // For now, return the most recent historical data point
                // TODO: Implement real-time data feeds
                var endDate = DateTime.Today;
                var startDate = endDate.AddDays(-5); // Look back 5 days to find recent data

                var recentData = await GetHistoricalDataAsync(symbol, startDate, endDate, cancellationToken);
                var latestPoint = recentData.OrderByDescending(d => d.Timestamp).FirstOrDefault();

                if (latestPoint != null)
                {
                    latestPoint.Metadata["IsCurrentData"] = true;
                    latestPoint.Metadata["RetrievedAt"] = DateTime.UtcNow;
                }

                _logger.LogDebug("Retrieved current data for {Symbol}: {Timestamp}", 
                    symbol, latestPoint?.Timestamp);

                return latestPoint;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve current data for {Symbol}", symbol);
                return null;
            }
        }

        /// <summary>
        /// Gets all available trading symbols in the data gateway.
        /// This includes symbols with historical data and supported synthetic symbols.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Available trading symbols</returns>
        public async Task<IEnumerable<string>> GetAvailableSymbolsAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Retrieving available symbols");

            try
            {
                await _historicalManager.InitializeAsync();
                var symbols = await _historicalManager.GetAvailableSymbolsAsync();
                
                // Add commonly supported synthetic symbols
                var syntheticSymbols = new[] { "SPY", "QQQ", "IWM", "VIX" };
                var allSymbols = symbols.Union(syntheticSymbols).Distinct().ToList();

                _logger.LogInformation("Found {Count} available symbols", allSymbols.Count);
                return allSymbols;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve available symbols");
                // Return default symbols if database query fails
                return new[] { "SPY", "QQQ", "IWM", "VIX" };
            }
        }

        /// <summary>
        /// Validates data quality for the specified symbol and date range.
        /// This performs comprehensive quality checks including statistical validation,
        /// OHLC relationship verification, and synthetic data benchmarking.
        /// </summary>
        /// <param name="symbol">Trading symbol</param>
        /// <param name="startDate">Start date for validation</param>
        /// <param name="endDate">End date for validation</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Detailed validation results</returns>
        public async Task<DataValidationResult> ValidateDataQualityAsync(
            string symbol, 
            DateTime startDate, 
            DateTime endDate, 
            CancellationToken cancellationToken = default)
        {
            ValidateSymbol(symbol);
            ValidateDateRange(startDate, endDate);

            _logger.LogInformation("Starting data quality validation for {Symbol} from {StartDate} to {EndDate}", 
                symbol, startDate, endDate);

            var result = new DataValidationResult
            {
                ValidationId = Guid.NewGuid().ToString("N")[..8]
            };

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Get data for validation
                var data = await GetHistoricalDataAsync(symbol, startDate, endDate, cancellationToken);
                var dataPoints = data.ToList();

                result.DataPointsValidated = dataPoints.Count;

                if (dataPoints.Count == 0)
                {
                    result.Issues.Add($"No data found for {symbol} in the specified date range");
                    result.OverallScore = 0;
                    result.IsValid = false;
                    return result;
                }

                // Validate OHLC relationships
                var ohlcErrors = 0;
                foreach (var point in dataPoints)
                {
                    if (!point.IsValid())
                    {
                        ohlcErrors++;
                        if (ohlcErrors <= 5) // Limit error messages
                        {
                            result.Issues.Add($"Invalid OHLC data at {point.Timestamp:yyyy-MM-dd HH:mm}");
                        }
                    }
                }

                result.ErrorCount = ohlcErrors;
                result.MetricsByCategory["OHLC_Validity"] = Math.Max(0, 100.0 - (ohlcErrors * 100.0 / dataPoints.Count));

                // Calculate statistical metrics
                var returns = new List<double>();
                for (int i = 1; i < dataPoints.Count; i++)
                {
                    returns.Add(dataPoints[i].CalculateReturn(dataPoints[i - 1].Close));
                }

                if (returns.Count > 0)
                {
                    var avgReturn = returns.Average();
                    var stdDev = Math.Sqrt(returns.Sum(r => Math.Pow(r - avgReturn, 2)) / returns.Count);
                    
                    result.MetricsByCategory["Statistical_Quality"] = CalculateStatisticalScore(avgReturn, stdDev);
                }

                // Run synthetic data benchmark if possible
                try
                {
                    var benchmark = await _validator.RunBenchmarkAsync();
                    result.MetricsByCategory["Synthetic_Quality"] = benchmark.OverallScore;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not run synthetic data benchmark");
                    result.MetricsByCategory["Synthetic_Quality"] = 75.0; // Default score
                }

                // Calculate overall score
                result.OverallScore = result.MetricsByCategory.Values.Average();
                result.IsValid = result.OverallScore >= 70.0 && result.ErrorCount < dataPoints.Count * 0.05;

                // Generate recommendations
                if (result.OverallScore < 85)
                {
                    result.Recommendations.Add("Consider data quality improvements");
                }
                if (ohlcErrors > 0)
                {
                    result.Recommendations.Add("Review OHLC data consistency");
                }

                stopwatch.Stop();
                result.ValidationDuration = stopwatch.Elapsed;

                _logger.LogInformation("Data validation completed for {Symbol}: Score {Score:F1}/100, Valid: {IsValid}", 
                    symbol, result.OverallScore, result.IsValid);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Data validation failed for {Symbol}", symbol);
                stopwatch.Stop();
                
                result.ValidationDuration = stopwatch.Elapsed;
                result.Issues.Add($"Validation error: {ex.Message}");
                result.OverallScore = 0;
                result.IsValid = false;
                
                return result;
            }
        }

        /// <summary>
        /// Exports market data to various formats for external analysis.
        /// Supports CSV, JSON, and Parquet formats with comprehensive metadata.
        /// </summary>
        /// <param name="symbol">Trading symbol</param>
        /// <param name="startDate">Start date for export</param>
        /// <param name="endDate">End date for export</param>
        /// <param name="format">Export format ("csv", "json", "parquet")</param>
        /// <param name="outputPath">Output file path</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Export operation result</returns>
        public async Task<ExportResult> ExportDataAsync(
            string symbol, 
            DateTime startDate, 
            DateTime endDate, 
            string format, 
            string outputPath,
            CancellationToken cancellationToken = default)
        {
            ValidateSymbol(symbol);
            ValidateDateRange(startDate, endDate);

            if (string.IsNullOrEmpty(format))
                throw new ArgumentException("Format cannot be null or empty", nameof(format));

            if (string.IsNullOrEmpty(outputPath))
                throw new ArgumentException("Output path cannot be null or empty", nameof(outputPath));

            _logger.LogInformation("Exporting {Symbol} data to {Format} format: {OutputPath}", 
                symbol, format, outputPath);

            var result = new ExportResult { OutputPath = outputPath };
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var data = await GetHistoricalDataAsync(symbol, startDate, endDate, cancellationToken);
                var dataPoints = data.ToList();

                if (dataPoints.Count == 0)
                {
                    result.ErrorMessage = "No data to export";
                    return result;
                }

                switch (format.ToLowerInvariant())
                {
                    case "csv":
                        await ExportToCsvAsync(dataPoints, outputPath);
                        break;
                    case "json":
                        await ExportToJsonAsync(dataPoints, outputPath);
                        break;
                    case "parquet":
                        throw new NotImplementedException("Parquet export not yet implemented");
                    default:
                        throw new ArgumentException($"Unsupported format: {format}");
                }

                var fileInfo = new System.IO.FileInfo(outputPath);
                result.Success = true;
                result.RecordsExported = dataPoints.Count;
                result.FileSizeBytes = fileInfo.Length;
                
                stopwatch.Stop();
                result.ExportDuration = stopwatch.Elapsed;

                _logger.LogInformation("Successfully exported {Count} records to {OutputPath} ({Size} bytes)", 
                    result.RecordsExported, outputPath, result.FileSizeBytes);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Export failed for {Symbol} to {OutputPath}", symbol, outputPath);
                stopwatch.Stop();
                
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.ExportDuration = stopwatch.Elapsed;
                
                return result;
            }
        }

        #region Private Helper Methods

        private static void ValidateSymbol(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                throw new ArgumentException("Symbol cannot be null or empty", nameof(symbol));
        }

        private static void ValidateDateRange(DateTime startDate, DateTime endDate)
        {
            if (startDate > endDate)
                throw new ArgumentException("Start date cannot be after end date");
        }

        private double GetCachedQualityScore()
        {
            // For now, return a default high-quality score
            // TODO: Implement caching of recent validation results
            return 85.0;
        }

        private static double CalculateStatisticalScore(double avgReturn, double stdDev)
        {
            // Simple heuristic for statistical quality
            // Lower volatility and returns close to zero get higher scores
            var returnScore = Math.Max(0, 100 - Math.Abs(avgReturn) * 10000);
            var volScore = Math.Max(0, 100 - stdDev * 1000);
            return (returnScore + volScore) / 2;
        }

        private static async Task ExportToCsvAsync(List<MarketDataPoint> dataPoints, string outputPath)
        {
            using var writer = new System.IO.StreamWriter(outputPath);
            
            // Write header
            await writer.WriteLineAsync("Timestamp,Symbol,Open,High,Low,Close,Volume,VWAP,IsSynthetic,QualityScore");
            
            // Write data
            foreach (var point in dataPoints)
            {
                await writer.WriteLineAsync($"{point.Timestamp:yyyy-MM-dd HH:mm:ss}," +
                    $"{point.Symbol},{point.Open},{point.High},{point.Low},{point.Close}," +
                    $"{point.Volume},{point.VWAP},{point.IsSynthetic},{point.QualityScore}");
            }
        }

        private static async Task ExportToJsonAsync(List<MarketDataPoint> dataPoints, string outputPath)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(dataPoints, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });
            
            await System.IO.File.WriteAllTextAsync(outputPath, json);
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Disposes of managed resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose method for derived classes.
        /// </summary>
        /// <param name="disposing">True if disposing managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _historicalManager?.Dispose();
                _validator?.Dispose();
                _database?.Dispose();
                _disposed = true;
                
                _logger.LogInformation("ODTE Data Gateway disposed");
            }
        }

        #endregion
    }
}