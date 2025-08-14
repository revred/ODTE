using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace ODTE.Historical
{
    /// <summary>
    /// Base interface for market data sources in the ODTE platform.
    /// Provides standardized access to market data from various providers.
    /// </summary>
    public interface IMarketDataSource
    {
        /// <summary>
        /// Gets the name of this data source provider.
        /// </summary>
        string SourceName { get; }

        /// <summary>
        /// Indicates whether this source provides real-time data.
        /// </summary>
        bool IsRealTime { get; }

        /// <summary>
        /// Gets the data quality score for this source (0-100).
        /// </summary>
        double QualityScore { get; }

        /// <summary>
        /// Retrieves market data for the specified symbol and time range.
        /// </summary>
        /// <param name="symbol">Trading symbol</param>
        /// <param name="startDate">Start date for data retrieval</param>
        /// <param name="endDate">End date for data retrieval</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Market data bars for the specified range</returns>
        Task<List<MarketDataBar>> GetMarketDataAsync(
            string symbol, 
            DateTime startDate, 
            DateTime endDate, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets current market data for the specified symbol.
        /// </summary>
        /// <param name="symbol">Trading symbol</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Current market data bar</returns>
        Task<MarketDataBar?> GetCurrentDataAsync(
            string symbol, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates the health and connectivity of this data source.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the source is healthy and accessible</returns>
        Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Base implementation of a market data source with common functionality.
    /// </summary>
    public abstract class MarketDataSourceBase : IMarketDataSource
    {
        protected readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the MarketDataSourceBase class.
        /// </summary>
        /// <param name="logger">Logger instance for diagnostics</param>
        protected MarketDataSourceBase(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the name of this data source provider.
        /// </summary>
        public abstract string SourceName { get; }

        /// <summary>
        /// Indicates whether this source provides real-time data.
        /// </summary>
        public abstract bool IsRealTime { get; }

        /// <summary>
        /// Gets the data quality score for this source (0-100).
        /// </summary>
        public virtual double QualityScore => 85.0;

        /// <summary>
        /// Retrieves market data for the specified symbol and time range.
        /// </summary>
        public abstract Task<List<MarketDataBar>> GetMarketDataAsync(
            string symbol, 
            DateTime startDate, 
            DateTime endDate, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets current market data for the specified symbol.
        /// </summary>
        public virtual async Task<MarketDataBar?> GetCurrentDataAsync(
            string symbol, 
            CancellationToken cancellationToken = default)
        {
            // Default implementation: get the most recent historical data
            var endDate = DateTime.Now;
            var startDate = endDate.AddDays(-1);
            
            var data = await GetMarketDataAsync(symbol, startDate, endDate, cancellationToken);
            return data.Count > 0 ? data[^1] : null;
        }

        /// <summary>
        /// Validates the health and connectivity of this data source.
        /// </summary>
        public virtual Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Health check for {SourceName}: OK", SourceName);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed for {SourceName}", SourceName);
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Validates input parameters for market data requests.
        /// </summary>
        /// <param name="symbol">Trading symbol</param>
        /// <param name="startDate">Start date</param>
        /// <param name="endDate">End date</param>
        protected virtual void ValidateParameters(string symbol, DateTime startDate, DateTime endDate)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                throw new ArgumentException("Symbol cannot be null or empty", nameof(symbol));

            if (startDate > endDate)
                throw new ArgumentException("Start date cannot be after end date");

            if (endDate > DateTime.Now.AddDays(1))
                throw new ArgumentException("End date cannot be in the future");
        }
    }

    /// <summary>
    /// Historical market data source that reads from the ODTE time series database.
    /// </summary>
    public class HistoricalMarketDataSource : MarketDataSourceBase
    {
        private readonly TimeSeriesDatabase _database;

        /// <summary>
        /// Initializes a new instance of the HistoricalMarketDataSource class.
        /// </summary>
        /// <param name="databasePath">Path to the SQLite database file</param>
        /// <param name="logger">Logger instance</param>
        public HistoricalMarketDataSource(string databasePath, ILogger<HistoricalMarketDataSource> logger)
            : base(logger)
        {
            _database = new TimeSeriesDatabase(databasePath);
        }

        /// <summary>
        /// Gets the name of this data source provider.
        /// </summary>
        public override string SourceName => "ODTE Historical Database";

        /// <summary>
        /// Indicates whether this source provides real-time data.
        /// </summary>
        public override bool IsRealTime => false;

        /// <summary>
        /// Gets the data quality score for this source (0-100).
        /// </summary>
        public override double QualityScore => 95.0; // High quality for historical data

        /// <summary>
        /// Retrieves market data for the specified symbol and time range.
        /// </summary>
        public override async Task<List<MarketDataBar>> GetMarketDataAsync(
            string symbol, 
            DateTime startDate, 
            DateTime endDate, 
            CancellationToken cancellationToken = default)
        {
            ValidateParameters(symbol, startDate, endDate);

            try
            {
                _logger.LogDebug("Retrieving historical data for {Symbol} from {StartDate} to {EndDate}", 
                    symbol, startDate, endDate);

                await _database.InitializeAsync();
                var data = await _database.GetRangeAsync(startDate, endDate, symbol);

                _logger.LogInformation("Retrieved {Count} data points for {Symbol}", data.Count, symbol);
                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve historical data for {Symbol}", symbol);
                throw;
            }
        }

        /// <summary>
        /// Validates the health and connectivity of this data source.
        /// </summary>
        public override async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _database.InitializeAsync();
                var stats = await _database.GetStatsAsync();
                
                _logger.LogDebug("Database health check: {Records} records, {Size} MB", 
                    stats.TotalRecords, stats.DatabaseSizeMB);
                
                return stats.TotalRecords > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database health check failed");
                return false;
            }
        }
    }

    /// <summary>
    /// Synthetic market data source that generates realistic market data for testing.
    /// </summary>
    public class SyntheticMarketDataSource : MarketDataSourceBase
    {
        private readonly OptionsDataGenerator _generator;

        /// <summary>
        /// Initializes a new instance of the SyntheticMarketDataSource class.
        /// </summary>
        /// <param name="logger">Logger instance</param>
        public SyntheticMarketDataSource(ILogger<SyntheticMarketDataSource> logger)
            : base(logger)
        {
            _generator = new OptionsDataGenerator();
        }

        /// <summary>
        /// Gets the name of this data source provider.
        /// </summary>
        public override string SourceName => "ODTE Synthetic Data Generator";

        /// <summary>
        /// Indicates whether this source provides real-time data.
        /// </summary>
        public override bool IsRealTime => false;

        /// <summary>
        /// Gets the data quality score for this source (0-100).
        /// </summary>
        public override double QualityScore => 76.9; // Based on academic validation

        /// <summary>
        /// Retrieves market data for the specified symbol and time range.
        /// </summary>
        public override async Task<List<MarketDataBar>> GetMarketDataAsync(
            string symbol, 
            DateTime startDate, 
            DateTime endDate, 
            CancellationToken cancellationToken = default)
        {
            ValidateParameters(symbol, startDate, endDate);

            try
            {
                _logger.LogDebug("Generating synthetic data for {Symbol} from {StartDate} to {EndDate}", 
                    symbol, startDate, endDate);

                var allData = new List<MarketDataBar>();
                var currentDate = startDate.Date;

                while (currentDate <= endDate.Date)
                {
                    // Skip weekends
                    if (currentDate.DayOfWeek != DayOfWeek.Saturday && 
                        currentDate.DayOfWeek != DayOfWeek.Sunday)
                    {
                        var dayData = await _generator.GenerateTradingDayAsync(currentDate, symbol);
                        allData.AddRange(dayData);
                    }

                    currentDate = currentDate.AddDays(1);
                }

                _logger.LogInformation("Generated {Count} synthetic data points for {Symbol}", 
                    allData.Count, symbol);

                return allData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate synthetic data for {Symbol}", symbol);
                throw;
            }
        }
    }

    /// <summary>
    /// Market data source that combines multiple sources with fallback logic.
    /// </summary>
    public class CompositeMarketDataSource : MarketDataSourceBase
    {
        private readonly List<IMarketDataSource> _sources;

        /// <summary>
        /// Initializes a new instance of the CompositeMarketDataSource class.
        /// </summary>
        /// <param name="sources">List of data sources in priority order</param>
        /// <param name="logger">Logger instance</param>
        public CompositeMarketDataSource(List<IMarketDataSource> sources, ILogger<CompositeMarketDataSource> logger)
            : base(logger)
        {
            _sources = sources ?? throw new ArgumentNullException(nameof(sources));
            
            if (_sources.Count == 0)
                throw new ArgumentException("At least one data source must be provided", nameof(sources));
        }

        /// <summary>
        /// Gets the name of this data source provider.
        /// </summary>
        public override string SourceName => $"Composite ({_sources.Count} sources)";

        /// <summary>
        /// Indicates whether this source provides real-time data.
        /// </summary>
        public override bool IsRealTime => _sources.Any(s => s.IsRealTime);

        /// <summary>
        /// Gets the data quality score for this source (0-100).
        /// </summary>
        public override double QualityScore => _sources.Max(s => s.QualityScore);

        /// <summary>
        /// Retrieves market data for the specified symbol and time range.
        /// </summary>
        public override async Task<List<MarketDataBar>> GetMarketDataAsync(
            string symbol, 
            DateTime startDate, 
            DateTime endDate, 
            CancellationToken cancellationToken = default)
        {
            ValidateParameters(symbol, startDate, endDate);

            foreach (var source in _sources)
            {
                try
                {
                    _logger.LogDebug("Trying data source: {SourceName}", source.SourceName);
                    
                    var data = await source.GetMarketDataAsync(symbol, startDate, endDate, cancellationToken);
                    
                    if (data.Count > 0)
                    {
                        _logger.LogInformation("Successfully retrieved {Count} data points from {SourceName}", 
                            data.Count, source.SourceName);
                        return data;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Data source {SourceName} failed, trying next", source.SourceName);
                }
            }

            throw new InvalidOperationException($"All data sources failed for {symbol}");
        }

        /// <summary>
        /// Gets current market data for the specified symbol.
        /// </summary>
        public override async Task<MarketDataBar?> GetCurrentDataAsync(
            string symbol, 
            CancellationToken cancellationToken = default)
        {
            // Try real-time sources first
            foreach (var source in _sources.Where(s => s.IsRealTime))
            {
                try
                {
                    var data = await source.GetCurrentDataAsync(symbol, cancellationToken);
                    if (data != null)
                    {
                        _logger.LogDebug("Got current data from {SourceName}", source.SourceName);
                        return data;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Real-time source {SourceName} failed", source.SourceName);
                }
            }

            // Fallback to historical sources
            return await base.GetCurrentDataAsync(symbol, cancellationToken);
        }

        /// <summary>
        /// Validates the health and connectivity of this data source.
        /// </summary>
        public override async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            var healthyCount = 0;
            
            foreach (var source in _sources)
            {
                try
                {
                    if (await source.HealthCheckAsync(cancellationToken))
                    {
                        healthyCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Health check failed for {SourceName}", source.SourceName);
                }
            }

            var isHealthy = healthyCount > 0;
            _logger.LogInformation("Composite health check: {Healthy}/{Total} sources healthy", 
                healthyCount, _sources.Count);

            return isHealthy;
        }
    }
}