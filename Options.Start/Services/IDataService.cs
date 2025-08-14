namespace Options.Start.Services;

public interface IDataService
{
    Task<DataSummary> GetDataSummaryAsync();
    Task<List<DataFile>> GetDataFilesAsync();
    Task<bool> RefreshDataAsync();
    Task<MarketDataInfo> GetMarketDataInfoAsync();
    Task<bool> GenerateHistoricalDataAsync(DateTime startDate, DateTime endDate);
}

public class DataService : IDataService
{
    public async Task<DataSummary> GetDataSummaryAsync()
    {
        await Task.Delay(100);
        
        // Load actual metadata from consolidated data
        var metadataPath = @"C:\code\ODTE\Data\XSP_Master_5Y_Index.metadata.json";
        if (File.Exists(metadataPath))
        {
            try
            {
                var jsonText = await File.ReadAllTextAsync(metadataPath);
                var metadata = System.Text.Json.JsonSerializer.Deserialize<ConsolidatedMetadata>(jsonText);
                
                if (metadata != null)
                {
                    return new DataSummary
                    {
                        TotalFiles = metadata.total_files,
                        TotalSizeMB = metadata.data_summary.total_size_mb,
                        LastUpdated = DateTime.Parse(metadata.consolidation_timestamp),
                        DataRange = new DateRange
                        {
                            Start = DateTime.Parse(metadata.date_range.start),
                            End = DateTime.Parse(metadata.date_range.end)
                        },
                        Formats = new Dictionary<string, int>
                        {
                            ["Parquet"] = metadata.total_files
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load consolidated metadata: {ex.Message}");
            }
        }
        
        // Fallback to hardcoded values
        return new DataSummary
        {
            TotalFiles = 1294,
            TotalSizeMB = 17.79,
            LastUpdated = DateTime.UtcNow,
            DataRange = new DateRange
            {
                Start = new DateTime(2021, 1, 4),
                End = new DateTime(2025, 12, 31)
            },
            Formats = new Dictionary<string, int>
            {
                ["Parquet"] = 1294
            }
        };
    }

    public async Task<List<DataFile>> GetDataFilesAsync()
    {
        await Task.Delay(100);
        return new List<DataFile>
        {
            new DataFile
            {
                Name = "XSP_Historical_5Y.parquet",
                Path = "Data/Historical/XSP/",
                Format = "Parquet",
                SizeMB = 847.2,
                LastModified = DateTime.UtcNow.AddDays(-1),
                RecordCount = 504660
            },
            new DataFile
            {
                Name = "SPY_2024.csv", 
                Path = "Data/rawData/csv/",
                Format = "CSV",
                SizeMB = 125.8,
                LastModified = DateTime.UtcNow.AddDays(-3),
                RecordCount = 98340
            },
            new DataFile
            {
                Name = "VIX_2024.csv",
                Path = "Data/rawData/csv/",
                Format = "CSV", 
                SizeMB = 45.2,
                LastModified = DateTime.UtcNow.AddDays(-3),
                RecordCount = 32780
            }
        };
    }

    public async Task<bool> RefreshDataAsync()
    {
        await Task.Delay(2000); // Simulate data refresh
        return true;
    }

    public async Task<MarketDataInfo> GetMarketDataInfoAsync()
    {
        await Task.Delay(100);
        
        // Load actual metadata from consolidated data
        var metadataPath = @"C:\code\ODTE\Data\XSP_Master_5Y_Index.metadata.json";
        if (File.Exists(metadataPath))
        {
            try
            {
                var jsonText = await File.ReadAllTextAsync(metadataPath);
                var metadata = System.Text.Json.JsonSerializer.Deserialize<ConsolidatedMetadata>(jsonText);
                
                if (metadata != null)
                {
                    var endDate = DateTime.Parse(metadata.date_range.end);
                    return new MarketDataInfo
                    {
                        Symbol = metadata.file_structure.schema.symbol,
                        TotalBars = metadata.data_summary.estimated_total_records,
                        TotalTradingDays = metadata.date_range.trading_days,
                        DataCompression = "Parquet format (10x vs CSV)",
                        AvgBarsPerDay = metadata.data_summary.estimated_total_records / metadata.date_range.trading_days,
                        LastBarTime = endDate.Date.AddHours(16), // Market close
                        MarketHours = "9:30 AM - 4:00 PM EST"
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load consolidated metadata: {ex.Message}");
            }
        }
        
        // Fallback values
        return new MarketDataInfo
        {
            Symbol = "XSP",
            TotalBars = 504660,
            TotalTradingDays = 1294,
            DataCompression = "Parquet format (10x vs CSV)",
            AvgBarsPerDay = 390,
            LastBarTime = DateTime.UtcNow.AddDays(-1).Date.AddHours(16), // Market close
            MarketHours = "9:30 AM - 4:00 PM EST"
        };
    }

    public async Task<bool> GenerateHistoricalDataAsync(DateTime startDate, DateTime endDate)
    {
        await Task.Delay(5000); // Simulate data generation
        return true;
    }
}

public class DataSummary
{
    public int TotalFiles { get; set; }
    public double TotalSizeMB { get; set; }
    public DateTime LastUpdated { get; set; }
    public DateRange DataRange { get; set; } = new();
    public Dictionary<string, int> Formats { get; set; } = new();
}

public class DataFile
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public double SizeMB { get; set; }
    public DateTime LastModified { get; set; }
    public long RecordCount { get; set; }
}

public class DateRange
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
}

public class MarketDataInfo
{
    public string Symbol { get; set; } = string.Empty;
    public long TotalBars { get; set; }
    public int TotalTradingDays { get; set; }
    public string DataCompression { get; set; } = string.Empty;
    public int AvgBarsPerDay { get; set; }
    public DateTime LastBarTime { get; set; }
    public string MarketHours { get; set; } = string.Empty;
}

// Consolidated metadata classes for deserializing the consolidated data metadata
public class ConsolidatedMetadata
{
    public string consolidation_timestamp { get; set; } = string.Empty;
    public string source_directory { get; set; } = string.Empty;
    public int total_files { get; set; }
    public ConsolidatedDateRange date_range { get; set; } = new();
    public ConsolidatedDataSummary data_summary { get; set; } = new();
    public ConsolidatedFileStructure file_structure { get; set; } = new();
    public ConsolidatedQualityIndicators quality_indicators { get; set; } = new();
}

public class ConsolidatedDateRange
{
    public string start { get; set; } = string.Empty;
    public string end { get; set; } = string.Empty;
    public int trading_days { get; set; }
    public int calendar_days { get; set; }
}

public class ConsolidatedDataSummary
{
    public long total_size_bytes { get; set; }
    public double total_size_mb { get; set; }
    public double avg_file_size_bytes { get; set; }
    public int estimated_total_records { get; set; }
}

public class ConsolidatedFileStructure
{
    public string format { get; set; } = string.Empty;
    public ConsolidatedSchema schema { get; set; } = new();
}

public class ConsolidatedSchema
{
    public string[] fields { get; set; } = Array.Empty<string>();
    public string symbol { get; set; } = string.Empty;
    public string timeframe { get; set; } = string.Empty;
    public string timezone { get; set; } = string.Empty;
}

public class ConsolidatedQualityIndicators
{
    public int expected_files { get; set; }
    public int actual_files { get; set; }
    public double coverage_percentage { get; set; }
    public string data_completeness { get; set; } = string.Empty;
}
