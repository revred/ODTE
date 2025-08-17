namespace ODTE.Historical;

/// <summary>
/// High-level API for managing ODTE historical data with smart caching and export capabilities
/// </summary>
public class HistoricalDataManager : IDisposable
{
    private readonly TimeSeriesDatabase _database;
    private readonly string _databasePath;

    public HistoricalDataManager(string databasePath = @"C:\code\ODTE\Data\ODTE_TimeSeries_5Y.db")
    {
        _databasePath = databasePath;
        _database = new TimeSeriesDatabase(databasePath);
    }

    /// <summary>
    /// Initialize or upgrade the database schema
    /// </summary>
    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();

        // Create the database if it doesn't exist
        if (!File.Exists(_databasePath))
        {
            Console.WriteLine("🏗️ Creating new time series database...");
            await ConsolidateFromParquetAsync();
        }
        else
        {
            var stats = await _database.GetStatsAsync();
            Console.WriteLine($"📊 Database loaded: {stats.TotalRecords:N0} records, {stats.DatabaseSizeMB:N1} MB");
        }
    }

    /// <summary>
    /// Import all Parquet files into the time series database
    /// </summary>
    public async Task<ImportResult> ConsolidateFromParquetAsync(
        string sourceDirectory = @"C:\code\ODTE\Data\Historical\XSP")
    {
        Console.WriteLine("🔄 Consolidating Parquet files into time series database...");

        var progress = new Progress<ImportProgress>(p =>
        {
            Console.WriteLine($"📈 [{p.FilesProcessed}/{p.TotalFiles}] " +
                            $"({p.ProgressPercentage:N1}%) {p.CurrentFile} - " +
                            $"{p.RecordsImported:N0} records");
        });

        var result = await _database.ImportFromParquetAsync(sourceDirectory, progress);

        if (result.Success)
        {
            var stats = await _database.GetStatsAsync();
            Console.WriteLine();
            Console.WriteLine("✅ CONSOLIDATION SUCCESSFUL");
            Console.WriteLine($"📊 Records: {stats.TotalRecords:N0}");
            Console.WriteLine($"📅 Date Range: {stats.StartDate:yyyy-MM-dd} to {stats.EndDate:yyyy-MM-dd}");
            Console.WriteLine($"💾 Database Size: {stats.DatabaseSizeMB:N1} MB");
            Console.WriteLine($"🗜️ Compression: {stats.CompressionRatio:N1}x");
            Console.WriteLine($"📁 Database Path: {_databasePath}");
        }
        else
        {
            Console.WriteLine($"❌ CONSOLIDATION FAILED: {result.ErrorMessage}");
        }

        return result;
    }

    /// <summary>
    /// Export data range to various formats
    /// </summary>
    public async Task<ExportResult> ExportRangeAsync(
        DateTime startDate,
        DateTime endDate,
        string outputPath,
        ExportFormat format = ExportFormat.CSV,
        string symbol = "XSP",
        ExportOptions? options = null)
    {
        options ??= new ExportOptions();

        Console.WriteLine($"📤 Exporting {symbol} data from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
        Console.WriteLine($"   Format: {format}, Output: {outputPath}");

        if (options.SampleToInterval.HasValue)
        {
            Console.WriteLine($"   Downsampling to: {options.SampleToInterval.Value.TotalMinutes} minute intervals");
        }

        var result = await _database.ExportRangeAsync(
            startDate, endDate, outputPath, format, symbol, options.SampleToInterval);

        if (result.Success)
        {
            var fileSizeMB = result.FileSizeBytes / (1024.0 * 1024.0);
            Console.WriteLine($"✅ Export completed: {result.RecordsExported:N0} records, {fileSizeMB:N2} MB");
        }
        else
        {
            Console.WriteLine($"❌ Export failed: {result.ErrorMessage}");
        }

        return result;
    }

    /// <summary>
    /// Get fast range data for backtesting
    /// </summary>
    public async Task<List<MarketDataBar>> GetBacktestDataAsync(
        DateTime startDate,
        DateTime endDate,
        string symbol = "XSP")
    {
        return await _database.GetRangeAsync(startDate, endDate, symbol);
    }

    /// <summary>
    /// Get downsampled data for visualization/analysis
    /// </summary>
    public async Task<List<MarketDataBar>> GetSampledDataAsync(
        DateTime startDate,
        DateTime endDate,
        TimeSpan sampleInterval,
        string symbol = "XSP")
    {
        return await _database.GetRangeAsync(startDate, endDate, symbol, sampleInterval);
    }

    /// <summary>
    /// Get database health and statistics
    /// </summary>
    public async Task<DatabaseStats> GetStatsAsync()
    {
        return await _database.GetStatsAsync();
    }

    /// <summary>
    /// Get market data for a specific symbol and date range
    /// </summary>
    public async Task<List<MarketDataBar>> GetMarketDataAsync(
        string symbol,
        DateTime startDate,
        DateTime endDate)
    {
        return await _database.GetRangeAsync(startDate, endDate, symbol);
    }



    // TODO: Query actual symbols from database
    static List<string> _symbols = new List<string> { "XSP", "SPY", "QQQ", "IWM" };

    /// <summary>
    /// Get list of available symbols in the database
    /// </summary>
    public async Task<List<string>> GetAvailableSymbolsAsync()
    {
        await Task.Delay(0); // Simulate async delay for testing purposes
        // For now, return default symbols
        return _symbols;
    }

    /// <summary>
    /// Export prebuilt datasets for common use cases
    /// </summary>
    public async Task<BatchExportResult> ExportCommonDatasetsAsync(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var result = new BatchExportResult();
        var stats = await _database.GetStatsAsync();

        Console.WriteLine("📦 Exporting common datasets...");

        // 1. Full dataset (CSV for backtesting)
        var fullExport = await ExportRangeAsync(
            stats.StartDate, stats.EndDate,
            Path.Combine(outputDirectory, "XSP_Full_Dataset.csv"),
            ExportFormat.CSV);
        result.Exports.Add("Full Dataset", fullExport);

        // 2. Recent year (high resolution)
        var recentStart = stats.EndDate.AddYears(-1);
        var recentExport = await ExportRangeAsync(
            recentStart, stats.EndDate,
            Path.Combine(outputDirectory, "XSP_Recent_Year.json"),
            ExportFormat.JSON);
        result.Exports.Add("Recent Year", recentExport);

        // 3. Daily samples (for visualization)
        var dailyExport = await ExportRangeAsync(
            stats.StartDate, stats.EndDate,
            Path.Combine(outputDirectory, "XSP_Daily_Samples.csv"),
            ExportFormat.CSV,
            options: new ExportOptions { SampleToInterval = TimeSpan.FromHours(6.5) }); // Daily close
        result.Exports.Add("Daily Samples", dailyExport);

        // 4. Monthly samples (for long-term analysis)
        var monthlyExport = await ExportRangeAsync(
            stats.StartDate, stats.EndDate,
            Path.Combine(outputDirectory, "XSP_Monthly_Samples.csv"),
            ExportFormat.CSV,
            options: new ExportOptions { SampleToInterval = TimeSpan.FromDays(22) }); // ~Monthly
        result.Exports.Add("Monthly Samples", monthlyExport);

        result.Success = result.Exports.Values.All(e => e.Success);
        result.TotalRecords = result.Exports.Values.Sum(e => e.RecordsExported);
        result.TotalSizeMB = result.Exports.Values.Sum(e => e.FileSizeBytes / (1024.0 * 1024.0));

        Console.WriteLine($"✅ Batch export completed: {result.Exports.Count} datasets, {result.TotalSizeMB:N1} MB total");

        return result;
    }

    public void Dispose()
    {
        _database?.Dispose();
    }
}

/// <summary>
/// Export configuration options
/// </summary>
public class ExportOptions
{
    /// <summary>Downsample to specified interval (e.g., hourly, daily)</summary>
    public TimeSpan? SampleToInterval { get; set; }

    /// <summary>Include trading calendar metadata</summary>
    public bool IncludeMetadata { get; set; } = true;

    /// <summary>Compress output file</summary>
    public bool Compress { get; set; } = false;
}

/// <summary>
/// Result of batch export operation
/// </summary>
public class BatchExportResult
{
    public bool Success { get; set; }
    public Dictionary<string, ExportResult> Exports { get; set; } = new();
    public int TotalRecords { get; set; }
    public double TotalSizeMB { get; set; }
}