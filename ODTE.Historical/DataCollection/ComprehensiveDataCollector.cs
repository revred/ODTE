using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ODTE.Historical.DataProviders;

namespace ODTE.Historical.DataCollection;

/// <summary>
/// Comprehensive data collector for 20-year historical market data (2005-2025)
/// Optimized for large-scale data collection with progress tracking and resumption
/// </summary>
public class ComprehensiveDataCollector : IDisposable
{
    private readonly EnhancedHistoricalDataFetcher _dataFetcher;
    private readonly TimeSeriesDatabase _database;
    private readonly ILogger<ComprehensiveDataCollector>? _logger;
    private readonly string _progressFile;
    private readonly string _reportFile;
    private CollectionProgress _progress;
    
    public ComprehensiveDataCollector(
        string databasePath = @"C:\code\ODTE\Data\ODTE_TimeSeries_20Y.db",
        string progressPath = @"C:\code\ODTE\Data\collection_progress.json",
        string reportPath = @"C:\code\ODTE\Data\collection_report.json",
        ILogger<ComprehensiveDataCollector>? logger = null)
    {
        _logger = logger;
        _progressFile = progressPath;
        _reportFile = reportPath;
        
        // Initialize database with optimizations for large datasets
        _database = new TimeSeriesDatabase(databasePath);
        _dataFetcher = new EnhancedHistoricalDataFetcher(databasePath);
        
        // Load existing progress or create new
        _progress = LoadProgress();
        
        _logger?.LogInformation($"Initialized ComprehensiveDataCollector");
        _logger?.LogInformation($"Database: {databasePath}");
        _logger?.LogInformation($"Progress: {_progress.CompletedDays.Count:N0} days already collected");
    }
    
    /// <summary>
    /// Collect all market data from 2005 to 2025 with comprehensive coverage
    /// </summary>
    public async Task<DataCollectionResult> CollectFullHistoricalDataAsync(
        CancellationToken cancellationToken = default)
    {
        var result = new DataCollectionResult
        {
            StartTime = DateTime.UtcNow,
            TargetStartDate = new DateTime(2005, 1, 1),
            TargetEndDate = new DateTime(2025, 12, 31)
        };
        
        try
        {
            _logger?.LogInformation("🚀 Starting comprehensive 20-year data collection (2005-2025)");
            _logger?.LogInformation($"📊 Target: {(result.TargetEndDate - result.TargetStartDate).TotalDays:N0} calendar days");
            
            // Initialize database with optimizations
            await OptimizeDatabaseForBulkInsert();
            
            // Define symbols to collect (focusing on key indices and ETFs)
            var symbols = GetTargetSymbols();
            _logger?.LogInformation($"🎯 Collecting data for {symbols.Count} symbols");
            
            foreach (var symbol in symbols)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                
                _logger?.LogInformation($"📈 Processing {symbol}...");
                
                var symbolResult = await CollectSymbolDataAsync(symbol, result.TargetStartDate, result.TargetEndDate, cancellationToken);
                result.SymbolResults[symbol] = symbolResult;
                
                // Update overall progress
                result.TotalDaysProcessed += symbolResult.DaysProcessed;
                result.TotalDaysFailed += symbolResult.DaysFailed;
                result.TotalApiCalls += symbolResult.ApiCallsMade;
                
                // Save progress after each symbol
                await SaveProgressAsync();
                
                _logger?.LogInformation($"✅ {symbol} completed: {symbolResult.DaysProcessed:N0} days, " +
                                      $"{symbolResult.SuccessRate:P1} success rate");
                
                // Rate limiting between symbols
                await Task.Delay(5000, cancellationToken);
            }
            
            // Final database optimization
            await OptimizeDatabaseForQuerying();
            
            result.EndTime = DateTime.UtcNow;
            result.Success = result.TotalDaysProcessed > 0;
            result.OverallSuccessRate = result.TotalDaysProcessed / (double)(result.TotalDaysProcessed + result.TotalDaysFailed);
            
            // Generate comprehensive report
            await GenerateComprehensiveReportAsync(result);
            
            _logger?.LogInformation($"🎉 Data collection completed!");
            _logger?.LogInformation($"📊 Total: {result.TotalDaysProcessed:N0} days processed in {result.Duration.TotalHours:F1} hours");
            _logger?.LogInformation($"📈 Success rate: {result.OverallSuccessRate:P2}");
            
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.EndTime = DateTime.UtcNow;
            
            _logger?.LogError(ex, "❌ Data collection failed");
            return result;
        }
    }
    
    /// <summary>
    /// Resume data collection from where it left off
    /// </summary>
    public async Task<DataCollectionResult> ResumeDataCollectionAsync(
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("🔄 Resuming data collection from previous progress...");
        
        // Determine what still needs to be collected
        var symbols = GetTargetSymbols();
        var incompleteSymbols = symbols.Where(s => !_progress.CompletedSymbols.Contains(s)).ToList();
        
        if (!incompleteSymbols.Any())
        {
            _logger?.LogInformation("✅ All symbols already completed!");
            return new DataCollectionResult { Success = true, TotalDaysProcessed = _progress.CompletedDays.Count };
        }
        
        _logger?.LogInformation($"📊 {incompleteSymbols.Count} symbols remaining: {string.Join(", ", incompleteSymbols)}");
        
        // Continue with incomplete symbols
        return await CollectFullHistoricalDataAsync(cancellationToken);
    }
    
    /// <summary>
    /// Collect specific date range for testing or catch-up
    /// </summary>
    public async Task<DataCollectionResult> CollectDateRangeAsync(
        DateTime startDate,
        DateTime endDate,
        List<string>? symbols = null,
        CancellationToken cancellationToken = default)
    {
        symbols ??= GetTargetSymbols();
        
        var result = new DataCollectionResult
        {
            StartTime = DateTime.UtcNow,
            TargetStartDate = startDate,
            TargetEndDate = endDate
        };
        
        _logger?.LogInformation($"📅 Collecting data from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
        _logger?.LogInformation($"🎯 Symbols: {string.Join(", ", symbols)}");
        
        foreach (var symbol in symbols)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            
            var symbolResult = await CollectSymbolDataAsync(symbol, startDate, endDate, cancellationToken);
            result.SymbolResults[symbol] = symbolResult;
            
            result.TotalDaysProcessed += symbolResult.DaysProcessed;
            result.TotalDaysFailed += symbolResult.DaysFailed;
            result.TotalApiCalls += symbolResult.ApiCallsMade;
        }
        
        result.EndTime = DateTime.UtcNow;
        result.Success = result.TotalDaysProcessed > 0;
        result.OverallSuccessRate = result.TotalDaysProcessed / (double)(result.TotalDaysProcessed + result.TotalDaysFailed);
        
        return result;
    }
    
    private async Task<SymbolCollectionResult> CollectSymbolDataAsync(
        string symbol,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        var result = new SymbolCollectionResult
        {
            Symbol = symbol,
            StartTime = DateTime.UtcNow
        };
        
        // Split into manageable chunks (monthly batches to handle rate limits)
        var batches = GenerateMonthlyBatches(startDate, endDate);
        
        _logger?.LogInformation($"   📦 Processing {batches.Count} monthly batches for {symbol}");
        
        foreach (var batch in batches)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            
            // Skip if already processed
            var batchKey = $"{symbol}_{batch.Start:yyyy-MM}";
            if (_progress.CompletedBatches.Contains(batchKey))
            {
                _logger?.LogDebug($"   ⏭️  Skipping {batchKey} (already completed)");
                continue;
            }
            
            try
            {
                _logger?.LogDebug($"   📊 Processing {symbol} {batch.Start:yyyy-MM-dd} to {batch.End:yyyy-MM-dd}");
                
                var batchResult = await _dataFetcher.FetchAndConsolidateDataAsync(
                    symbol, batch.Start, batch.End, cancellationToken);
                
                if (batchResult.Success)
                {
                    result.DaysProcessed += batchResult.TotalDaysProcessed;
                    result.ApiCallsMade += batchResult.TotalDaysProcessed; // Approximate
                    
                    // Mark batch as completed
                    _progress.CompletedBatches.Add(batchKey);
                    
                    // Update completed days
                    foreach (var day in batchResult.ProcessedDays)
                    {
                        _progress.CompletedDays.Add($"{symbol}_{day:yyyy-MM-dd}");
                    }
                    
                    _logger?.LogDebug($"   ✅ {batchKey}: {batchResult.TotalDaysProcessed} days");
                }
                else
                {
                    result.DaysFailed += batchResult.FailedDays.Count;
                    result.Errors.Add($"{batchKey}: {batchResult.ErrorMessage}");
                    
                    _logger?.LogWarning($"   ❌ {batchKey} failed: {batchResult.ErrorMessage}");
                }
                
                // Rate limiting between batches
                await Task.Delay(2000, cancellationToken);
                
                // Periodic progress save (every 10 batches)
                if ((result.DaysProcessed + result.DaysFailed) % 10 == 0)
                {
                    await SaveProgressAsync();
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"{batchKey}: {ex.Message}");
                _logger?.LogError(ex, $"   ❌ Error processing {batchKey}");
            }
        }
        
        // Mark symbol as completed if we processed all batches
        var totalBatches = batches.Count;
        var completedBatches = batches.Count(b => _progress.CompletedBatches.Contains($"{symbol}_{b.Start:yyyy-MM}"));
        
        if (completedBatches == totalBatches)
        {
            _progress.CompletedSymbols.Add(symbol);
        }
        
        result.EndTime = DateTime.UtcNow;
        result.SuccessRate = result.DaysProcessed / (double)Math.Max(1, result.DaysProcessed + result.DaysFailed);
        
        return result;
    }
    
    private List<DateRange> GenerateMonthlyBatches(DateTime startDate, DateTime endDate)
    {
        var batches = new List<DateRange>();
        var current = new DateTime(startDate.Year, startDate.Month, 1);
        
        while (current <= endDate)
        {
            var batchEnd = current.AddMonths(1).AddDays(-1);
            if (batchEnd > endDate)
                batchEnd = endDate;
            
            batches.Add(new DateRange { Start = current, End = batchEnd });
            current = current.AddMonths(1);
        }
        
        return batches;
    }
    
    private List<string> GetTargetSymbols()
    {
        // Focus on most important symbols for options trading
        return new List<string>
        {
            // Major indices and ETFs (primary focus)
            "SPY", "XSP", "QQQ", "IWM", "VIX",
            
            // Sector ETFs
            "XLF", "XLE", "XLK", "XLV", "XLI", "XLP", "XLU", "XLB", "XLRE",
            
            // Major individual stocks (high volume options)
            "AAPL", "MSFT", "GOOGL", "AMZN", "TSLA", "NVDA", "META",
            
            // Additional important ETFs
            "GLD", "SLV", "TLT", "EEM", "FXI", "EFA"
        };
    }
    
    private async Task OptimizeDatabaseForBulkInsert()
    {
        _logger?.LogInformation("🔧 Optimizing database for bulk insert operations...");
        
        // These would be implemented in the TimeSeriesDatabase class
        // For now, we'll use the existing initialization
        await _database.InitializeAsync();
        
        _logger?.LogInformation("✅ Database optimization completed");
    }
    
    private async Task OptimizeDatabaseForQuerying()
    {
        _logger?.LogInformation("🔧 Optimizing database for querying...");
        
        // Final optimization for querying performance
        // This would include creating indexes, updating statistics, etc.
        var stats = await _database.GetStatsAsync();
        
        _logger?.LogInformation($"📊 Final database stats:");
        _logger?.LogInformation($"   Records: {stats.TotalRecords:N0}");
        _logger?.LogInformation($"   Size: {stats.DatabaseSizeMB:N1} MB");
        _logger?.LogInformation($"   Date range: {stats.StartDate:yyyy-MM-dd} to {stats.EndDate:yyyy-MM-dd}");
    }
    
    private CollectionProgress LoadProgress()
    {
        try
        {
            if (File.Exists(_progressFile))
            {
                var json = File.ReadAllText(_progressFile);
                var progress = System.Text.Json.JsonSerializer.Deserialize<CollectionProgress>(json);
                return progress ?? new CollectionProgress();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load progress file, starting fresh");
        }
        
        return new CollectionProgress();
    }
    
    private async Task SaveProgressAsync()
    {
        try
        {
            _progress.LastUpdated = DateTime.UtcNow;
            var json = System.Text.Json.JsonSerializer.Serialize(_progress, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            Directory.CreateDirectory(Path.GetDirectoryName(_progressFile) ?? "");
            await File.WriteAllTextAsync(_progressFile, json);
            
            _logger?.LogDebug($"💾 Progress saved: {_progress.CompletedDays.Count:N0} days completed");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to save progress");
        }
    }
    
    private async Task GenerateComprehensiveReportAsync(DataCollectionResult result)
    {
        try
        {
            var report = new
            {
                GeneratedAt = DateTime.UtcNow,
                DataCollection = new
                {
                    result.Success,
                    result.StartTime,
                    result.EndTime,
                    DurationHours = result.Duration.TotalHours,
                    result.TotalDaysProcessed,
                    result.TotalDaysFailed,
                    result.TotalApiCalls,
                    result.OverallSuccessRate
                },
                SymbolBreakdown = result.SymbolResults.Select(kvp => new
                {
                    Symbol = kvp.Key,
                    DaysProcessed = kvp.Value.DaysProcessed,
                    DaysFailed = kvp.Value.DaysFailed,
                    SuccessRate = kvp.Value.SuccessRate,
                    ErrorCount = kvp.Value.Errors.Count,
                    FirstError = kvp.Value.Errors.FirstOrDefault()
                }).ToList(),
                DatabaseStats = await _database.GetStatsAsync()
            };
            
            var json = System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            Directory.CreateDirectory(Path.GetDirectoryName(_reportFile) ?? "");
            await File.WriteAllTextAsync(_reportFile, json);
            
            _logger?.LogInformation($"📋 Comprehensive report saved to: {_reportFile}");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to generate report");
        }
    }
    
    public void Dispose()
    {
        SaveProgressAsync().Wait();
        _dataFetcher?.Dispose();
        _database?.Dispose();
    }
}

/// <summary>
/// Progress tracking for resumable data collection
/// </summary>
public class CollectionProgress
{
    public HashSet<string> CompletedDays { get; set; } = new();
    public HashSet<string> CompletedBatches { get; set; } = new();
    public HashSet<string> CompletedSymbols { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Overall data collection result
/// </summary>
public class DataCollectionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public DateTime TargetStartDate { get; set; }
    public DateTime TargetEndDate { get; set; }
    
    public int TotalDaysProcessed { get; set; }
    public int TotalDaysFailed { get; set; }
    public int TotalApiCalls { get; set; }
    public double OverallSuccessRate { get; set; }
    
    public Dictionary<string, SymbolCollectionResult> SymbolResults { get; set; } = new();
    
    public TimeSpan Duration => EndTime - StartTime;
}

/// <summary>
/// Collection result for individual symbol
/// </summary>
public class SymbolCollectionResult
{
    public string Symbol { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    
    public int DaysProcessed { get; set; }
    public int DaysFailed { get; set; }
    public int ApiCallsMade { get; set; }
    public double SuccessRate { get; set; }
    
    public List<string> Errors { get; set; } = new();
    
    public TimeSpan Duration => EndTime - StartTime;
}

/// <summary>
/// Date range for batch processing
/// </summary>
internal class DateRange
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
}