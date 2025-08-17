namespace ODTE.Historical
{
    /// <summary>
    /// Central gateway interface for accessing all types of market data in the ODTE platform.
    /// Provides unified access to historical, real-time, and synthetic data for optimization,
    /// practice trading, and simulation.
    /// </summary>
    public interface IDataGateway
    {
        /// <summary>
        /// Gets the name of the data gateway implementation.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Indicates whether this gateway provides real-time data.
        /// </summary>
        bool IsRealTime { get; }

        /// <summary>
        /// Gets the data quality score (0-100) for this gateway.
        /// Higher scores indicate better data quality and reliability.
        /// </summary>
        double QualityScore { get; }

        /// <summary>
        /// Retrieves historical market data for the specified symbol and date range.
        /// Used for backtesting, optimization, and historical analysis.
        /// </summary>
        /// <param name="symbol">The trading symbol (e.g., "SPY", "XSP")</param>
        /// <param name="startDate">Start date for data retrieval</param>
        /// <param name="endDate">End date for data retrieval</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Collection of market data points</returns>
        Task<IEnumerable<MarketDataPoint>> GetHistoricalDataAsync(
            string symbol,
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates synthetic market data for the specified parameters.
        /// Used for stress testing, scenario analysis, and data augmentation.
        /// </summary>
        /// <param name="symbol">The trading symbol</param>
        /// <param name="startDate">Start date for synthetic data generation</param>
        /// <param name="endDate">End date for synthetic data generation</param>
        /// <param name="scenario">Market scenario to simulate (e.g., "normal", "stressed", "crisis")</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Collection of synthetic market data points</returns>
        Task<IEnumerable<MarketDataPoint>> GenerateSyntheticDataAsync(
            string symbol,
            DateTime startDate,
            DateTime endDate,
            string scenario = "normal",
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves current market data for live trading and real-time analysis.
        /// </summary>
        /// <param name="symbol">The trading symbol</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Current market data point</returns>
        Task<MarketDataPoint?> GetCurrentDataAsync(
            string symbol,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a list of all available symbols in this data gateway.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Collection of available trading symbols</returns>
        Task<IEnumerable<string>> GetAvailableSymbolsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates the data quality for the specified symbol and date range.
        /// Used for ensuring data integrity before optimization or trading.
        /// </summary>
        /// <param name="symbol">The trading symbol</param>
        /// <param name="startDate">Start date for validation</param>
        /// <param name="endDate">End date for validation</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Data validation result with quality metrics</returns>
        Task<DataValidationResult> ValidateDataQualityAsync(
            string symbol,
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Exports market data to the specified format for external analysis.
        /// Supports CSV, JSON, and Parquet formats.
        /// </summary>
        /// <param name="symbol">The trading symbol</param>
        /// <param name="startDate">Start date for export</param>
        /// <param name="endDate">End date for export</param>
        /// <param name="format">Export format ("csv", "json", "parquet")</param>
        /// <param name="outputPath">Output file path</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Export operation result</returns>
        Task<ExportResult> ExportDataAsync(
            string symbol,
            DateTime startDate,
            DateTime endDate,
            string format,
            string outputPath,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents a single market data point with comprehensive trading information.
    /// </summary>
    public class MarketDataPoint
    {
        /// <summary>
        /// Timestamp of the data point in UTC.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Trading symbol (e.g., "SPY", "XSP").
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Opening price for the time period.
        /// </summary>
        public double Open { get; set; }

        /// <summary>
        /// Highest price during the time period.
        /// </summary>
        public double High { get; set; }

        /// <summary>
        /// Lowest price during the time period.
        /// </summary>
        public double Low { get; set; }

        /// <summary>
        /// Closing price for the time period.
        /// </summary>
        public double Close { get; set; }

        /// <summary>
        /// Trading volume for the time period.
        /// </summary>
        public long Volume { get; set; }

        /// <summary>
        /// Volume-weighted average price (VWAP) if available.
        /// </summary>
        public double? VWAP { get; set; }

        /// <summary>
        /// Current bid price (for real-time data).
        /// </summary>
        public double? Bid { get; set; }

        /// <summary>
        /// Current ask price (for real-time data).
        /// </summary>
        public double? Ask { get; set; }

        /// <summary>
        /// Indicates whether this is synthetic or real market data.
        /// </summary>
        public bool IsSynthetic { get; set; }

        /// <summary>
        /// Data quality score for this specific point (0-100).
        /// </summary>
        public double QualityScore { get; set; } = 100.0;

        /// <summary>
        /// Additional metadata about the data point.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Validates the OHLC relationships and data integrity.
        /// </summary>
        /// <returns>True if data point is valid, false otherwise</returns>
        public bool IsValid()
        {
            return Open > 0 && High > 0 && Low > 0 && Close > 0 &&
                   High >= Math.Max(Open, Close) &&
                   Low <= Math.Min(Open, Close) &&
                   Volume >= 0;
        }

        /// <summary>
        /// Calculates the price return for this data point.
        /// </summary>
        /// <param name="previousClose">Previous period's closing price</param>
        /// <returns>Return as a decimal (e.g., 0.01 for 1% return)</returns>
        public double CalculateReturn(double previousClose)
        {
            return previousClose > 0 ? (Close - previousClose) / previousClose : 0.0;
        }
    }

    /// <summary>
    /// Result of data validation operations with detailed quality metrics.
    /// </summary>
    public class DataValidationResult
    {
        /// <summary>
        /// Unique identifier for this validation run.
        /// </summary>
        public string ValidationId { get; set; } = Guid.NewGuid().ToString("N")[..8];

        /// <summary>
        /// Overall data quality score (0-100).
        /// </summary>
        public double OverallScore { get; set; }

        /// <summary>
        /// Indicates whether the data meets quality standards.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Number of data points validated.
        /// </summary>
        public int DataPointsValidated { get; set; }

        /// <summary>
        /// Number of data points with validation errors.
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// Duration of the validation process.
        /// </summary>
        public TimeSpan ValidationDuration { get; set; }

        /// <summary>
        /// Detailed validation metrics by category.
        /// </summary>
        public Dictionary<string, double> MetricsByCategory { get; set; } = new();

        /// <summary>
        /// Validation errors and warnings.
        /// </summary>
        public List<string> Issues { get; set; } = new();

        /// <summary>
        /// Recommendations for improving data quality.
        /// </summary>
        public List<string> Recommendations { get; set; } = new();
    }

    /// <summary>
    /// Result of data export operations.
    /// </summary>
    public class ExportResult
    {
        /// <summary>
        /// Indicates whether the export was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Path to the exported file.
        /// </summary>
        public string OutputPath { get; set; } = string.Empty;

        /// <summary>
        /// Number of records exported.
        /// </summary>
        public int RecordsExported { get; set; }

        /// <summary>
        /// Size of the exported file in bytes.
        /// </summary>
        public long FileSizeBytes { get; set; }

        /// <summary>
        /// Duration of the export process.
        /// </summary>
        public TimeSpan ExportDuration { get; set; }

        /// <summary>
        /// Error message if export failed.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}