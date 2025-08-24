using ODTE.Contracts.Data;

namespace ODTE.Contracts.Historical
{
    /// <summary>
    /// Historical data provider interface
    /// </summary>
    public interface IDataProvider
    {
        Task<List<MarketDataBar>> GetHistoricalBarsAsync(string symbol, DateTime start, DateTime end);
        Task<ChainSnapshot> GetOptionsChainAsync(string symbol, DateTime date, DateTime expiration);
        Task<List<ChainSnapshot>> GetMultiExpirationChainAsync(string symbol, DateTime date);
    }

    /// <summary>
    /// Distributed database manager interface
    /// </summary>
    public interface IDistributedDatabaseManager
    {
        Task<bool> StoreOptionsDataAsync(string symbol, DateTime date, ChainSnapshot data);
        Task<ChainSnapshot?> RetrieveOptionsDataAsync(string symbol, DateTime date, DateTime expiration);
        Task<List<MarketDataBar>> GetMarketDataAsync(string symbol, DateTime start, DateTime end);
        Task<DatabaseStatistics> GetStorageStatisticsAsync();
    }

    /// <summary>
    /// Market data bar (OHLCV)
    /// </summary>
    public class MarketDataBar
    {
        public DateTime Timestamp { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public long Volume { get; set; }
        public decimal VWAP { get; set; }
    }

    /// <summary>
    /// Database storage statistics
    /// </summary>
    public class DatabaseStatistics
    {
        public int TotalRecords { get; set; }
        public long StorageSize { get; set; }
        public DateTime EarliestDate { get; set; }
        public DateTime LatestDate { get; set; }
        public List<string> AvailableSymbols { get; set; } = new();
    }
}