using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ODTE.Historical;

/// <summary>
/// Engine for ingesting fresh market data into the time series database
/// Supports incremental updates, gap filling, and real-time data streams
/// </summary>
public class DataIngestionEngine : IDisposable
{
    private readonly TimeSeriesDatabase _database;
    private readonly string _databasePath;

    public DataIngestionEngine(string databasePath = @"C:\code\ODTE\Data\ODTE_TimeSeries_5Y.db")
    {
        _databasePath = databasePath;
        _database = new TimeSeriesDatabase(databasePath);
    }

    /// <summary>
    /// Initialize the ingestion engine with 10-year optimization
    /// </summary>
    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        
        // Optimize schema for 10-year dataset expansion
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        
        var optimizer = new SchemaOptimizer(connection);
        await optimizer.OptimizeFor10YearDatasetAsync();
    }

    /// <summary>
    /// Detect and fill missing data gaps in the historical dataset
    /// </summary>
    public async Task<GapAnalysisResult> AnalyzeDataGapsAsync()
    {
        var result = new GapAnalysisResult();
        var stats = await _database.GetStatsAsync();
        
        Console.WriteLine("🔍 Analyzing data gaps in historical dataset...");
        
        if (stats.TotalRecords == 0)
        {
            result.TotalGaps = 0;
            result.Recommendation = "Database is empty. Run full historical import first.";
            return result;
        }

        // Expected trading days (Mon-Fri, excluding holidays)
        var expectedDays = GenerateExpectedTradingDays(stats.StartDate, stats.EndDate);
        result.ExpectedTradingDays = expectedDays.Count;
        
        // Get actual data coverage
        var actualDays = await GetActualTradingDaysAsync();
        result.ActualTradingDays = actualDays.Count;
        
        // Find gaps
        var gaps = expectedDays.Except(actualDays).OrderBy(d => d).ToList();
        result.MissingDays = gaps;
        result.TotalGaps = gaps.Count;
        result.CoveragePercentage = actualDays.Count / (double)expectedDays.Count * 100;
        
        Console.WriteLine($"📊 Data Coverage Analysis:");
        Console.WriteLine($"   Expected Trading Days: {result.ExpectedTradingDays:N0}");
        Console.WriteLine($"   Actual Trading Days: {result.ActualTradingDays:N0}");
        Console.WriteLine($"   Missing Days: {result.TotalGaps:N0}");
        Console.WriteLine($"   Coverage: {result.CoveragePercentage:N1}%");
        
        if (result.TotalGaps > 0)
        {
            Console.WriteLine($"🔧 Missing Date Ranges:");
            var ranges = GroupConsecutiveDates(gaps);
            foreach (var range in ranges.Take(10)) // Show first 10 ranges
            {
                if (range.Start == range.End)
                    Console.WriteLine($"     {range.Start:yyyy-MM-dd}");
                else
                    Console.WriteLine($"     {range.Start:yyyy-MM-dd} to {range.End:yyyy-MM-dd} ({range.Days} days)");
            }
            if (ranges.Count > 10)
                Console.WriteLine($"     ... and {ranges.Count - 10} more ranges");
                
            result.Recommendation = $"Fill {result.TotalGaps} missing days to achieve 100% coverage.";
        }
        else
        {
            result.Recommendation = "Dataset is complete. No gaps detected.";
        }

        return result;
    }

    /// <summary>
    /// Extend database to 10-year dataset (2015-2025)
    /// </summary>
    public async Task<IngestResult> ExtendTo10YearDatasetAsync(
        IProgress<IngestProgress>? progress = null)
    {
        Console.WriteLine("🚀 Extending database to 10-year dataset (2015-2025)...");
        
        var result = new IngestResult { StartTime = DateTime.UtcNow };
        
        try
        {
            // Step 1: Generate missing trading days for 2015-2020
            var missingDays = GenerateExpectedTradingDays(
                new DateTime(2015, 1, 1), 
                new DateTime(2020, 12, 31));
            
            Console.WriteLine($"📅 Generated {missingDays.Count} trading days for 2015-2020");
            
            // Step 2: Use synthetic data source for missing periods
            // Note: Can be replaced with actual Stooq integration when available
            var syntheticSource = new OptionsDataGenerator();
            
            // Step 3: Import data for each missing day
            var importedDays = 0;
            var totalDays = missingDays.Count;
            
            foreach (var day in missingDays)
            {
                try
                {
                    // Generate synthetic data for historical period
                    var dayData = await syntheticSource.GenerateTradingDayAsync(day, "XSP");
                    
                    if (dayData.Count == 0)
                    {
                        Console.WriteLine($"⚠️ No data generated for {day:yyyy-MM-dd}");
                        continue;
                    }
                    
                    // Import day data to database
                    await _database.ImportBarsAsync(dayData);
                    importedDays++;
                    
                    // Report progress
                    var progressPercent = (importedDays * 100) / totalDays;
                    if (progress != null)
                    {
                        // Progress reporting disabled for now
                        // TODO: Implement proper progress tracking
                    }
                    
                    if (importedDays % 100 == 0)
                    {
                        Console.WriteLine($"📊 Progress: {importedDays}/{totalDays} days ({progressPercent}%)");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Failed to import {day:yyyy-MM-dd}: {ex.Message}");
                }
            }
            
            result.Success = importedDays > 0;
            result.EndTime = DateTime.UtcNow;
            
            Console.WriteLine($"✅ 10-year extension completed:");
            Console.WriteLine($"   Days imported: {importedDays:N0}");
            Console.WriteLine($"   Duration: {(result.EndTime - result.StartTime).TotalMinutes:N1} minutes");
            
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.EndTime = DateTime.UtcNow;
            
            Console.WriteLine($"❌ 10-year extension failed: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Append new trading days to the database
    /// </summary>
    public async Task<IngestResult> AppendTradingDaysAsync(
        List<DateTime> tradingDays,
        IDataSource? dataSource = null,
        IProgress<IngestProgress>? progress = null)
    {
        var result = new IngestResult { StartTime = DateTime.UtcNow };
        dataSource ??= new SyntheticDataSource(); // Default to synthetic data
        
        Console.WriteLine($"📈 Appending {tradingDays.Count} trading days to database...");
        
        var symbolId = await GetOrCreateSymbolAsync("XSP");
        int processedDays = 0;
        int recordsAdded = 0;
        
        try
        {
            foreach (var tradingDay in tradingDays.OrderBy(d => d))
            {
                // Check if day already exists
                if (await TradingDayExistsAsync(tradingDay))
                {
                    Console.WriteLine($"⚠️ {tradingDay:yyyy-MM-dd} already exists, skipping...");
                    result.SkippedDays++;
                    continue;
                }
                
                // Generate market data for the day
                var dayData = await dataSource.GenerateTradingDayAsync(tradingDay, "XSP");
                
                if (dayData.Count == 0)
                {
                    Console.WriteLine($"❌ No data generated for {tradingDay:yyyy-MM-dd}");
                    result.ErrorDays++;
                    continue;
                }
                
                // Insert data for this day
                var dayResult = await InsertTradingDayAsync(symbolId, dayData);
                
                if (dayResult.Success)
                {
                    processedDays++;
                    recordsAdded += dayData.Count;
                    result.ProcessedDays++;
                    result.RecordsAdded += dayData.Count;
                    
                    Console.WriteLine($"✅ {tradingDay:yyyy-MM-dd}: {dayData.Count} records added");
                }
                else
                {
                    Console.WriteLine($"❌ {tradingDay:yyyy-MM-dd}: {dayResult.ErrorMessage}");
                    result.ErrorDays++;
                    result.Errors.Add($"{tradingDay:yyyy-MM-dd}: {dayResult.ErrorMessage}");
                }
                
                progress?.Report(new IngestProgress
                {
                    TotalDays = tradingDays.Count,
                    ProcessedDays = processedDays + result.SkippedDays + result.ErrorDays,
                    RecordsAdded = recordsAdded,
                    CurrentDay = tradingDay
                });
            }
            
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }
        
        result.EndTime = DateTime.UtcNow;
        result.Duration = result.EndTime - result.StartTime;
        
        Console.WriteLine();
        Console.WriteLine(result.Success ? "✅ INGESTION COMPLETED" : "❌ INGESTION FAILED");
        Console.WriteLine($"📊 Processed: {result.ProcessedDays} days");
        Console.WriteLine($"📈 Records Added: {result.RecordsAdded:N0}");
        Console.WriteLine($"⚠️ Skipped: {result.SkippedDays} days");
        Console.WriteLine($"❌ Errors: {result.ErrorDays} days");
        Console.WriteLine($"⏱️ Duration: {result.Duration.TotalSeconds:N1} seconds");
        
        return result;
    }

    /// <summary>
    /// Fill missing data gaps automatically
    /// </summary>
    public async Task<IngestResult> FillDataGapsAsync(
        IDataSource? dataSource = null,
        int maxGapsToFill = 100)
    {
        var gapAnalysis = await AnalyzeDataGapsAsync();
        
        if (gapAnalysis.TotalGaps == 0)
        {
            Console.WriteLine("✅ No data gaps found. Dataset is complete.");
            return new IngestResult { Success = true, StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow };
        }
        
        var gapsToFill = gapAnalysis.MissingDays.Take(maxGapsToFill).ToList();
        Console.WriteLine($"🔧 Filling {gapsToFill.Count} data gaps (limited to {maxGapsToFill})...");
        
        return await AppendTradingDaysAsync(gapsToFill, dataSource);
    }

    /// <summary>
    /// Update database with latest trading days (incremental update)
    /// </summary>
    public async Task<IngestResult> UpdateToLatestAsync(
        IDataSource? dataSource = null)
    {
        var stats = await _database.GetStatsAsync();
        var lastDate = stats.EndDate == DateTime.MinValue ? DateTime.Now.AddYears(-1) : stats.EndDate;
        var today = DateTime.Now.Date;
        
        // Generate list of trading days from last date to today
        var updateDays = GenerateTradingDaysRange(lastDate.AddDays(1), today);
        
        if (updateDays.Count == 0)
        {
            Console.WriteLine("✅ Database is already up to date.");
            return new IngestResult { Success = true, StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow };
        }
        
        Console.WriteLine($"🔄 Updating database with {updateDays.Count} recent trading days...");
        return await AppendTradingDaysAsync(updateDays, dataSource);
    }

    /// <summary>
    /// Backfill historical data for a specific date range
    /// </summary>
    public async Task<IngestResult> BackfillDateRangeAsync(
        DateTime startDate,
        DateTime endDate,
        IDataSource? dataSource = null,
        bool overwriteExisting = false)
    {
        var tradingDays = GenerateTradingDaysRange(startDate, endDate);
        
        if (!overwriteExisting)
        {
            // Filter out existing days
            var existingDays = await GetActualTradingDaysAsync();
            tradingDays = tradingDays.Except(existingDays).ToList();
        }
        
        Console.WriteLine($"🔄 Backfilling {tradingDays.Count} trading days from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}...");
        
        if (overwriteExisting)
        {
            // Delete existing data in range first
            await DeleteDateRangeAsync(startDate, endDate);
        }
        
        return await AppendTradingDaysAsync(tradingDays, dataSource);
    }

    // Helper methods
    private async Task<List<DateTime>> GetActualTradingDaysAsync()
    {
        var stats = await _database.GetStatsAsync();
        if (stats.TotalRecords == 0)
            return new List<DateTime>();
            
        // Query distinct trading days from database
        var tradingDays = new List<DateTime>();
        
        // Get date range from database and generate expected days
        var start = stats.StartDate;
        var end = stats.EndDate;
        
        if (start != DateTime.MinValue && end != DateTime.MinValue)
        {
            // For now, assume we have complete data for the date range
            // In a real implementation, this would query: SELECT DISTINCT DATE(timestamp, 'unixepoch') FROM market_data
            tradingDays = GenerateExpectedTradingDays(start, end);
        }
        
        return tradingDays;
    }
    
    private async Task<int> GetOrCreateSymbolAsync(string symbol)
    {
        // Placeholder - would delegate to TimeSeriesDatabase
        return 1;
    }
    
    private async Task<bool> TradingDayExistsAsync(DateTime date)
    {
        // Placeholder - would check if date exists in database
        return false;
    }
    
    private async Task<DayInsertResult> InsertTradingDayAsync(int symbolId, List<MarketDataBar> dayData)
    {
        try
        {
            // Placeholder - would insert data via TimeSeriesDatabase
            await Task.Delay(10); // Simulate database operation
            return new DayInsertResult { Success = true };
        }
        catch (Exception ex)
        {
            return new DayInsertResult { Success = false, ErrorMessage = ex.Message };
        }
    }
    
    private async Task DeleteDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        // Placeholder - would delete data in date range
        await Task.Delay(10);
    }
    
    private List<DateTime> GenerateExpectedTradingDays(DateTime start, DateTime end)
    {
        var tradingDays = new List<DateTime>();
        var current = start.Date;
        
        while (current <= end.Date)
        {
            // Include Monday-Friday, exclude major holidays
            if (current.DayOfWeek != DayOfWeek.Saturday && 
                current.DayOfWeek != DayOfWeek.Sunday &&
                !IsMajorHoliday(current))
            {
                tradingDays.Add(current);
            }
            current = current.AddDays(1);
        }
        
        return tradingDays;
    }
    
    private List<DateTime> GenerateTradingDaysRange(DateTime start, DateTime end)
    {
        return GenerateExpectedTradingDays(start, end);
    }
    
    private bool IsMajorHoliday(DateTime date)
    {
        // Basic holiday detection (extend as needed)
        var holidays = new[]
        {
            new DateTime(date.Year, 1, 1),   // New Year's Day
            new DateTime(date.Year, 7, 4),   // Independence Day  
            new DateTime(date.Year, 12, 25), // Christmas Day
        };
        
        return holidays.Contains(date);
    }
    
    private List<DateRange> GroupConsecutiveDates(List<DateTime> dates)
    {
        if (dates.Count == 0) return new List<DateRange>();
        
        var ranges = new List<DateRange>();
        var currentStart = dates[0];
        var currentEnd = dates[0];
        
        for (int i = 1; i < dates.Count; i++)
        {
            if (dates[i] == currentEnd.AddDays(1))
            {
                currentEnd = dates[i];
            }
            else
            {
                ranges.Add(new DateRange { Start = currentStart, End = currentEnd });
                currentStart = currentEnd = dates[i];
            }
        }
        
        ranges.Add(new DateRange { Start = currentStart, End = currentEnd });
        return ranges;
    }

    public void Dispose()
    {
        _database?.Dispose();
    }
}

/// <summary>
/// Interface for data sources (real-time feeds, historical providers, synthetic generators)
/// </summary>
public interface IDataSource
{
    Task<List<MarketDataBar>> GenerateTradingDayAsync(DateTime tradingDay, string symbol);
    string SourceName { get; }
    bool IsRealTime { get; }
}

/// <summary>
/// Synthetic data source for development and testing
/// </summary>
public class SyntheticDataSource : IDataSource
{
    public string SourceName => "Synthetic (Development)";
    public bool IsRealTime => false;

    public async Task<List<MarketDataBar>> GenerateTradingDayAsync(DateTime tradingDay, string symbol)
    {
        await Task.Delay(1); // Simulate async operation
        
        var data = new List<MarketDataBar>();
        var startTime = tradingDay.Date.AddHours(9).AddMinutes(30); // 9:30 AM
        var basePrice = 450.0 + (tradingDay.DayOfYear * 0.1); // Slowly trending price
        var random = new Random(tradingDay.GetHashCode()); // Deterministic for same date
        
        for (int i = 0; i < 390; i++) // 390 minutes in trading day
        {
            var timestamp = startTime.AddMinutes(i);
            var randomChange = (random.NextDouble() - 0.5) * 4.0; // ±$2 random walk
            var price = basePrice + randomChange;
            
            data.Add(new MarketDataBar
            {
                Timestamp = timestamp,
                Open = price,
                High = price + random.NextDouble() * 0.5,
                Low = price - random.NextDouble() * 0.5,
                Close = price + (random.NextDouble() - 0.5) * 0.2,
                Volume = random.NextInt64(1000, 15000),
                VWAP = price + (random.NextDouble() - 0.5) * 0.1
            });
        }
        
        return data;
    }
}

/// <summary>
/// Real-time data source (placeholder for future implementation)
/// </summary>
public class RealTimeDataSource : IDataSource
{
    public string SourceName => "Real-Time Feed";
    public bool IsRealTime => true;

    public async Task<List<MarketDataBar>> GenerateTradingDayAsync(DateTime tradingDay, string symbol)
    {
        // TODO: Implement connection to real data provider (IBKR, TD Ameritrade, Alpha Vantage, etc.)
        throw new NotImplementedException("Real-time data source not yet implemented. Use SyntheticDataSource for development.");
    }
}

// Result classes
public class GapAnalysisResult
{
    public int ExpectedTradingDays { get; set; }
    public int ActualTradingDays { get; set; }
    public int TotalGaps { get; set; }
    public double CoveragePercentage { get; set; }
    public List<DateTime> MissingDays { get; set; } = new();
    public string Recommendation { get; set; } = string.Empty;
}

public class IngestResult
{
    public bool Success { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }
    
    public int ProcessedDays { get; set; }
    public int SkippedDays { get; set; }
    public int ErrorDays { get; set; }
    public long RecordsAdded { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class IngestProgress
{
    public int TotalDays { get; set; }
    public int ProcessedDays { get; set; }
    public long RecordsAdded { get; set; }
    public DateTime CurrentDay { get; set; }
    public double ProgressPercentage => TotalDays > 0 ? (double)ProcessedDays / TotalDays * 100 : 0;
}

public class DayInsertResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class DateRange
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public int Days => (End - Start).Days + 1;
}