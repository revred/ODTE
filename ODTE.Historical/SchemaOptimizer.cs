using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace ODTE.Historical
{
    /// <summary>
    /// Schema optimization utilities for the 10-year dataset expansion
    /// Ensures optimal performance with 2.6M+ records instead of current 520K records
    /// </summary>
    public class SchemaOptimizer
    {
        private readonly SqliteConnection _connection;
        
        public SchemaOptimizer(SqliteConnection connection)
        {
            _connection = connection;
        }
        
        /// <summary>
        /// Optimize schema for 10-year dataset (2015-2025)
        /// Expected growth: 520KB → ~1MB (2.6M records)
        /// </summary>
        public async Task OptimizeFor10YearDatasetAsync()
        {
            Console.WriteLine("🔧 Optimizing schema for 10-year dataset...");
            
            await OptimizeIndicesAsync();
            await CreatePartitionedViewsAsync();
            await UpdateStatisticsAsync();
            await OptimizeCacheSettingsAsync();
            
            Console.WriteLine("✅ Schema optimization completed for 10-year dataset");
        }
        
        /// <summary>
        /// Create optimized indices for large dataset queries
        /// </summary>
        private async Task OptimizeIndicesAsync()
        {
            Console.WriteLine("📊 Creating optimized indices for 10-year dataset...");
            
            // Primary time-based index (already exists, but let's ensure it's optimal)
            await ExecuteNonQueryAsync(@"
                CREATE UNIQUE INDEX IF NOT EXISTS idx_market_data_time_symbol 
                ON market_data(timestamp, symbol_id)");
            
            // Secondary index for symbol-based queries
            await ExecuteNonQueryAsync(@"
                CREATE INDEX IF NOT EXISTS idx_market_data_symbol_time 
                ON market_data(symbol_id, timestamp)");
            
            // Date-based index for business day queries  
            await ExecuteNonQueryAsync(@"
                CREATE INDEX IF NOT EXISTS idx_market_data_date
                ON market_data(timestamp DESC)");
            
            // Volume analysis index
            await ExecuteNonQueryAsync(@"
                CREATE INDEX IF NOT EXISTS idx_market_data_volume
                ON market_data(symbol_id, volume DESC) 
                WHERE volume > 1000000");
            
            // Price range analysis index
            await ExecuteNonQueryAsync(@"
                CREATE INDEX IF NOT EXISTS idx_market_data_price_range
                ON market_data(symbol_id, high_price - low_price DESC)");
        }
        
        /// <summary>
        /// Create partitioned views for common time ranges
        /// </summary>
        private async Task CreatePartitionedViewsAsync()
        {
            Console.WriteLine("🗂️ Creating partitioned views for common queries...");
            
            // Recent data view (last 2 years - most common queries)
            var twoYearsAgo = ((DateTimeOffset)DateTime.Now.AddYears(-2)).ToUnixTimeSeconds();
            await ExecuteNonQueryAsync($@"
                CREATE VIEW IF NOT EXISTS market_data_recent AS
                SELECT * FROM market_data 
                WHERE timestamp >= {twoYearsAgo}");
            
            // Historical data view (2015-2020 pre-COVID)
            var covid = ((DateTimeOffset)new DateTime(2020, 3, 1)).ToUnixTimeSeconds();
            var startHistorical = ((DateTimeOffset)new DateTime(2015, 1, 1)).ToUnixTimeSeconds();
            await ExecuteNonQueryAsync($@"
                CREATE VIEW IF NOT EXISTS market_data_historical AS
                SELECT * FROM market_data 
                WHERE timestamp >= {startHistorical} AND timestamp < {covid}");
            
            // Crisis periods view (2020-2022 COVID/inflation era)
            var endCrisis = ((DateTimeOffset)new DateTime(2023, 1, 1)).ToUnixTimeSeconds();
            await ExecuteNonQueryAsync($@"
                CREATE VIEW IF NOT EXISTS market_data_crisis AS
                SELECT * FROM market_data 
                WHERE timestamp >= {covid} AND timestamp < {endCrisis}");
            
            // Daily aggregation view for backtesting
            await ExecuteNonQueryAsync(@"
                CREATE VIEW IF NOT EXISTS daily_market_data AS
                SELECT 
                    symbol_id,
                    DATE(timestamp, 'unixepoch') as date,
                    MIN(open_price) as day_open,
                    MAX(high_price) as day_high,
                    MIN(low_price) as day_low,
                    MAX(close_price) as day_close,
                    SUM(volume) as day_volume,
                    AVG(vwap_price) as day_vwap
                FROM market_data
                GROUP BY symbol_id, DATE(timestamp, 'unixepoch')");
        }
        
        /// <summary>
        /// Update database statistics for query optimizer
        /// </summary>
        private async Task UpdateStatisticsAsync()
        {
            Console.WriteLine("📈 Updating database statistics...");
            
            // Analyze all tables for optimal query planning
            await ExecuteNonQueryAsync("ANALYZE market_data");
            await ExecuteNonQueryAsync("ANALYZE symbols");
            await ExecuteNonQueryAsync("ANALYZE trading_calendar");
            await ExecuteNonQueryAsync("ANALYZE metadata");
        }
        
        /// <summary>
        /// Optimize cache settings for 10-year dataset
        /// </summary>
        private async Task OptimizeCacheSettingsAsync()
        {
            Console.WriteLine("⚡ Optimizing cache settings for large dataset...");
            
            // Increase cache size for 10-year dataset (from 10MB to 50MB)
            await ExecuteNonQueryAsync("PRAGMA cache_size=50000");
            
            // Increase memory map size (from 256MB to 512MB)
            await ExecuteNonQueryAsync("PRAGMA mmap_size=536870912");
            
            // Optimize for read-heavy workloads
            await ExecuteNonQueryAsync("PRAGMA query_only=0");
            await ExecuteNonQueryAsync("PRAGMA read_uncommitted=1");
            
            // Enable automatic vacuum for large datasets
            await ExecuteNonQueryAsync("PRAGMA auto_vacuum=INCREMENTAL");
        }
        
        /// <summary>
        /// Validate schema is ready for 10-year expansion
        /// </summary>
        public async Task<SchemaValidationResult> ValidateSchemaAsync()
        {
            var result = new SchemaValidationResult();
            
            // Check existing indices
            var indexCount = await ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name LIKE 'idx_%'");
            result.IndexCount = indexCount;
            result.HasOptimalIndices = indexCount >= 5;
            
            // Check cache settings
            var cacheSize = await ExecuteScalarAsync<int>("PRAGMA cache_size");
            result.CacheSize = cacheSize;
            result.HasOptimalCache = cacheSize >= 50000;
            
            // Check database size
            var pageCount = await ExecuteScalarAsync<int>("PRAGMA page_count");
            var pageSize = await ExecuteScalarAsync<int>("PRAGMA page_size");
            result.DatabaseSizeMB = (pageCount * pageSize) / (1024.0 * 1024.0);
            
            // Estimate capacity for 10-year dataset
            result.EstimatedSizeAfterExpansion = result.DatabaseSizeMB * 2.0; // Double the size
            result.HasSufficientCapacity = result.EstimatedSizeAfterExpansion < 100; // Under 100MB is fine
            
            return result;
        }
        
        private async Task ExecuteNonQueryAsync(string sql)
        {
            using var command = new SqliteCommand(sql, _connection);
            await command.ExecuteNonQueryAsync();
        }
        
        private async Task<T> ExecuteScalarAsync<T>(string sql)
        {
            using var command = new SqliteCommand(sql, _connection);
            var result = await command.ExecuteScalarAsync();
            return (T)Convert.ChangeType(result, typeof(T));
        }
    }
    
    /// <summary>
    /// Results of schema validation for 10-year dataset
    /// </summary>
    public class SchemaValidationResult
    {
        public int IndexCount { get; set; }
        public bool HasOptimalIndices { get; set; }
        public int CacheSize { get; set; }
        public bool HasOptimalCache { get; set; }
        public double DatabaseSizeMB { get; set; }
        public double EstimatedSizeAfterExpansion { get; set; }
        public bool HasSufficientCapacity { get; set; }
        
        public bool IsOptimizedFor10Years => 
            HasOptimalIndices && HasOptimalCache && HasSufficientCapacity;
    }
}