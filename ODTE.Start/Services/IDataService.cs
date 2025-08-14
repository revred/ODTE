namespace ODTE.Start.Services;

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
        return new DataSummary
        {
            TotalFiles = 12,
            TotalSizeMB = 1250.5,
            LastUpdated = DateTime.UtcNow.AddHours(-6),
            DataRange = new DateRange
            {
                Start = new DateTime(2020, 8, 14),
                End = new DateTime(2025, 8, 14)
            },
            Formats = new Dictionary<string, int>
            {
                ["Parquet"] = 8,
                ["CSV"] = 4
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
        return new MarketDataInfo
        {
            Symbol = "XSP",
            TotalBars = 504660,
            TotalTradingDays = 1294,
            DataCompression = "10x vs CSV",
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