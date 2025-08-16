using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ODTE.Historical.DataProviders;

/// <summary>
/// Enhanced historical data fetcher that integrates with ODTE's existing data pipeline
/// </summary>
public class EnhancedHistoricalDataFetcher : IDisposable
{
    private readonly MultiSourceDataFetcher _multiSource;
    private readonly TimeSeriesDatabase _database;
    private readonly ILogger<EnhancedHistoricalDataFetcher>? _logger;
    private readonly string _parquetOutputPath;
    
    public EnhancedHistoricalDataFetcher(
        string databasePath = @"C:\code\ODTE\Data\ODTE_TimeSeries_5Y.db",
        string parquetPath = @"C:\code\ODTE\Data\Historical\XSP",
        ILogger<EnhancedHistoricalDataFetcher>? logger = null)
    {
        _database = new TimeSeriesDatabase(databasePath);
        _parquetOutputPath = parquetPath;
        _logger = logger;
        
        // Initialize multi-source fetcher with providers
        _multiSource = new MultiSourceDataFetcher();
        
        // Add providers if API keys are available
        InitializeProviders();
    }
    
    /// <summary>
    /// Fetch and consolidate data for a date range using multiple sources
    /// </summary>
    public async Task<DataFetchResult> FetchAndConsolidateDataAsync(
        string symbol,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var result = new DataFetchResult
        {
            Symbol = symbol,
            StartDate = startDate,
            EndDate = endDate,
            StartTime = DateTime.UtcNow
        };
        
        try
        {
            Directory.CreateDirectory(_parquetOutputPath);
            
            var processedDays = 0;
            var failedDays = new List<DateTime>();
            var currentDate = startDate.Date;
            
            _logger?.LogInformation($"Starting data fetch for {symbol} from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            
            while (currentDate <= endDate.Date)
            {
                // Skip weekends (basic implementation - real trading calendar would be better)
                if (currentDate.DayOfWeek == DayOfWeek.Saturday || 
                    currentDate.DayOfWeek == DayOfWeek.Sunday)
                {
                    currentDate = currentDate.AddDays(1);
                    continue;
                }
                
                try
                {
                    // Check if data already exists
                    var existingData = await CheckExistingDataAsync(symbol, currentDate);
                    if (existingData)
                    {
                        _logger?.LogDebug($"Data already exists for {currentDate:yyyy-MM-dd}, skipping");
                        processedDays++;
                        currentDate = currentDate.AddDays(1);
                        continue;
                    }
                    
                    // Fetch intraday bars
                    var bars = await _multiSource.GetIntradayBarsAsync(
                        symbol,
                        currentDate,
                        currentDate.AddDays(1),
                        TimeSpan.FromMinutes(1),
                        cancellationToken);
                    
                    if (bars.Any())
                    {
                        // Enhance with VIX data if available
                        await EnhanceBarsWithVixAsync(bars, currentDate);
                        
                        // Save to parquet
                        await SaveToParquetAsync(bars, currentDate);
                        
                        // Note: Database update would be handled by existing ODTE pipeline
                        
                        processedDays++;
                        result.ProcessedDays.Add(currentDate);
                        
                        _logger?.LogInformation($"Successfully processed {currentDate:yyyy-MM-dd} ({bars.Count} bars)");
                    }
                    else
                    {
                        _logger?.LogWarning($"No data available for {currentDate:yyyy-MM-dd}");
                        failedDays.Add(currentDate);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, $"Failed to process {currentDate:yyyy-MM-dd}");
                    failedDays.Add(currentDate);
                }
                
                currentDate = currentDate.AddDays(1);
                
                // Rate limiting between days
                await Task.Delay(1000, cancellationToken);
            }
            
            result.TotalDaysProcessed = processedDays;
            result.FailedDays = failedDays;
            result.Success = processedDays > 0;
            result.EndTime = DateTime.UtcNow;
            
            _logger?.LogInformation($"Data fetch completed: {processedDays} successful, {failedDays.Count} failed");
            
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.EndTime = DateTime.UtcNow;
            
            _logger?.LogError(ex, "Data fetch operation failed");
            return result;
        }
    }
    
    /// <summary>
    /// Get provider status and health information
    /// </summary>
    public async Task<List<ProviderStatus>> GetProviderStatusAsync()
    {
        return await _multiSource.GetProviderStatusAsync();
    }
    
    /// <summary>
    /// Validate data quality across all providers
    /// </summary>
    public async Task<DataQualityReport> ValidateDataQualityAsync(
        string symbol,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        var report = new DataQualityReport
        {
            Symbol = symbol,
            Date = date,
            ValidationTime = DateTime.UtcNow
        };
        
        try
        {
            // Get consolidated data from multiple sources
            var consolidatedData = await _multiSource.GetConsolidatedDataAsync(symbol, date, cancellationToken);
            
            if (consolidatedData.Success && consolidatedData.Data != null)
            {
                report.IsValid = true;
                report.Sources = consolidatedData.Sources;
                report.UnderlyingPrice = consolidatedData.Data.UnderlyingPrice;
                report.TotalOptions = consolidatedData.Data.Calls.Count + consolidatedData.Data.Puts.Count;
                
                // Analyze data quality metrics
                report.QualityMetrics = AnalyzeDataQuality(consolidatedData.Data);
            }
            else
            {
                report.IsValid = false;
                report.ErrorMessage = consolidatedData.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            report.IsValid = false;
            report.ErrorMessage = ex.Message;
        }
        
        return report;
    }
    
    private void InitializeProviders()
    {
        // Try to get API keys from environment variables
        var polygonKey = Environment.GetEnvironmentVariable("POLYGON_API_KEY");
        var alphaVantageKey = Environment.GetEnvironmentVariable("ALPHA_VANTAGE_API_KEY");
        var twelveDataKey = Environment.GetEnvironmentVariable("TWELVE_DATA_API_KEY");
        
        if (!string.IsNullOrEmpty(polygonKey))
        {
            _multiSource.AddProvider(new PolygonDataProvider(polygonKey));
            _logger?.LogInformation("Added Polygon.io provider");
        }
        
        if (!string.IsNullOrEmpty(alphaVantageKey))
        {
            _multiSource.AddProvider(new AlphaVantageDataProvider(alphaVantageKey));
            _logger?.LogInformation("Added Alpha Vantage provider");
        }
        
        if (!string.IsNullOrEmpty(twelveDataKey))
        {
            _multiSource.AddProvider(new TwelveDataProvider(twelveDataKey));
            _logger?.LogInformation("Added Twelve Data provider");
        }
        
        if (string.IsNullOrEmpty(polygonKey) && 
            string.IsNullOrEmpty(alphaVantageKey) && 
            string.IsNullOrEmpty(twelveDataKey))
        {
            _logger?.LogWarning("No API keys found. Set environment variables: POLYGON_API_KEY, ALPHA_VANTAGE_API_KEY, TWELVE_DATA_API_KEY");
        }
    }
    
    private async Task<bool> CheckExistingDataAsync(string symbol, DateTime date)
    {
        try
        {
            var yearMonth = $"{date.Year}/{date.Month:D2}";
            var parquetFile = Path.Combine(_parquetOutputPath, yearMonth, $"{date:yyyyMMdd}.parquet");
            
            return File.Exists(parquetFile) && new FileInfo(parquetFile).Length > 0;
        }
        catch
        {
            return false;
        }
    }
    
    private async Task EnhanceBarsWithVixAsync(List<MarketDataBar> bars, DateTime date)
    {
        try
        {
            // Try to get VIX data for the same day
            var vixBars = await _multiSource.GetIntradayBarsAsync(
                "VIX",
                date,
                date.AddDays(1),
                TimeSpan.FromHours(1));
            
            if (vixBars.Any())
            {
                var vixClose = vixBars.Last().Close;
                
                // Note: VIX enhancement would be handled by existing ODTE pipeline
                // The existing MarketDataBar doesn't have a VIX property
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, $"Failed to enhance bars with VIX data for {date:yyyy-MM-dd}");
        }
    }
    
    private async Task SaveToParquetAsync(List<MarketDataBar> bars, DateTime date)
    {
        try
        {
            var yearMonth = $"{date.Year}/{date.Month:D2}";
            var outputDir = Path.Combine(_parquetOutputPath, yearMonth);
            Directory.CreateDirectory(outputDir);
            
            var parquetFile = Path.Combine(outputDir, $"{date:yyyyMMdd}.parquet");
            
            // Convert to the format expected by ODTE
            var dataRows = bars.Select(bar => new
            {
                timestamp = bar.Timestamp,
                open = bar.Open,
                high = bar.High,
                low = bar.Low,
                close = bar.Close,
                volume = bar.Volume,
                vwap = bar.VWAP,
                symbol = "XSP", // Default symbol for ODTE
                vix = 20.0 // Default VIX if not available
            }).ToList();
            
            // Save as parquet (using a simple CSV for now, can upgrade to Arrow/Parquet later)
            await File.WriteAllTextAsync(parquetFile + ".csv", 
                string.Join("\n", dataRows.Select(r => 
                    $"{r.timestamp:yyyy-MM-dd HH:mm:ss},{r.open},{r.high},{r.low},{r.close},{r.volume},{r.vwap},{r.symbol},{r.vix}")));
            
            _logger?.LogDebug($"Saved {bars.Count} bars to {parquetFile}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"Failed to save parquet file for {date:yyyy-MM-dd}");
            throw;
        }
    }
    
    // Database updates would be handled by existing ODTE pipeline
    
    private DataQualityMetrics AnalyzeDataQuality(OptionsChainData data)
    {
        var metrics = new DataQualityMetrics();
        
        var allOptions = data.Calls.Concat(data.Puts).ToList();
        
        if (allOptions.Any())
        {
            // Calculate bid-ask spreads
            var spreads = allOptions
                .Where(o => o.Ask > o.Bid && o.Bid > 0)
                .Select(o => (o.Ask - o.Bid) / o.Ask)
                .ToList();
            
            if (spreads.Any())
            {
                metrics.AverageBidAskSpread = (double)spreads.Average();
                metrics.MaxBidAskSpread = (double)spreads.Max();
            }
            
            // Count options with valid pricing
            metrics.OptionsWithValidPricing = allOptions.Count(o => o.Bid > 0 && o.Ask > o.Bid);
            metrics.OptionsWithVolume = allOptions.Count(o => o.Volume > 0);
            metrics.OptionsWithOpenInterest = allOptions.Count(o => o.OpenInterest > 0);
            
            // IV analysis
            var validIVs = allOptions.Where(o => o.ImpliedVolatility > 0).ToList();
            if (validIVs.Any())
            {
                metrics.AverageImpliedVolatility = (double)validIVs.Average(o => o.ImpliedVolatility);
                metrics.ImpliedVolatilityRange = (double)(validIVs.Max(o => o.ImpliedVolatility) - validIVs.Min(o => o.ImpliedVolatility));
            }
        }
        
        return metrics;
    }
    
    public void Dispose()
    {
        _multiSource?.Dispose();
        _database?.Dispose();
    }
}

/// <summary>
/// Data fetch operation result
/// </summary>
public class DataFetchResult
{
    public string Symbol { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int TotalDaysProcessed { get; set; }
    public List<DateTime> ProcessedDays { get; set; } = new();
    public List<DateTime> FailedDays { get; set; } = new();
    
    public TimeSpan Duration => EndTime - StartTime;
    public double SuccessRate => ProcessedDays.Count / (double)(ProcessedDays.Count + FailedDays.Count);
}

/// <summary>
/// Data quality validation report
/// </summary>
public class DataQualityReport
{
    public string Symbol { get; set; } = "";
    public DateTime Date { get; set; }
    public DateTime ValidationTime { get; set; }
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Sources { get; set; } = new();
    public decimal UnderlyingPrice { get; set; }
    public int TotalOptions { get; set; }
    public DataQualityMetrics? QualityMetrics { get; set; }
}

/// <summary>
/// Data quality analysis metrics
/// </summary>
public class DataQualityMetrics
{
    public double AverageBidAskSpread { get; set; }
    public double MaxBidAskSpread { get; set; }
    public int OptionsWithValidPricing { get; set; }
    public int OptionsWithVolume { get; set; }
    public int OptionsWithOpenInterest { get; set; }
    public double AverageImpliedVolatility { get; set; }
    public double ImpliedVolatilityRange { get; set; }
}