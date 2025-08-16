using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace ODTE.Historical.DataCollection;

/// <summary>
/// Database optimizer for handling large-scale historical data efficiently
/// Optimizes SQLite for 20+ years of market data storage and retrieval
/// </summary>
public class DatabaseOptimizer
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseOptimizer>? _logger;
    
    public DatabaseOptimizer(string databasePath, ILogger<DatabaseOptimizer>? logger = null)
    {
        _connectionString = $"Data Source={databasePath};Version=3;";
        _logger = logger;
    }
    
    /// <summary>
    /// Optimize database for bulk insert operations during data collection
    /// </summary>
    public async Task OptimizeForBulkInsertAsync()
    {
        _logger?.LogInformation("🔧 Optimizing database for bulk insert operations...");
        
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        using var transaction = connection.BeginTransaction();
        
        try
        {
            // Disable synchronous writes for speed (data integrity handled by transactions)
            await ExecuteCommandAsync(connection, "PRAGMA synchronous = OFF");
            
            // Use WAL mode for better concurrency
            await ExecuteCommandAsync(connection, "PRAGMA journal_mode = WAL");
            
            // Increase cache size to 100MB for better performance
            await ExecuteCommandAsync(connection, "PRAGMA cache_size = 25600"); // 100MB / 4KB pages
            
            // Set memory temp store
            await ExecuteCommandAsync(connection, "PRAGMA temp_store = MEMORY");
            
            // Increase page size for better I/O (must be done before any tables are created)
            await ExecuteCommandAsync(connection, "PRAGMA page_size = 8192");
            
            // Optimize for bulk operations
            await ExecuteCommandAsync(connection, "PRAGMA count_changes = OFF");
            await ExecuteCommandAsync(connection, "PRAGMA auto_vacuum = NONE");
            
            transaction.Commit();
            
            _logger?.LogInformation("✅ Database optimized for bulk insert");
            _logger?.LogInformation("   - Synchronous writes disabled");
            _logger?.LogInformation("   - WAL journal mode enabled"); 
            _logger?.LogInformation("   - Cache size increased to 100MB");
            _logger?.LogInformation("   - Page size set to 8KB");
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger?.LogError(ex, "Failed to optimize database for bulk insert");
            throw;
        }
    }
    
    /// <summary>
    /// Optimize database for querying operations after data collection
    /// </summary>
    public async Task OptimizeForQueryingAsync()
    {
        _logger?.LogInformation("🔧 Optimizing database for querying operations...");
        
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        using var transaction = connection.BeginTransaction();
        
        try
        {
            // Re-enable synchronous writes for data safety
            await ExecuteCommandAsync(connection, "PRAGMA synchronous = NORMAL");
            
            // Create comprehensive indexes for fast querying
            await CreateOptimizedIndexesAsync(connection);
            
            // Analyze tables for query optimization
            await ExecuteCommandAsync(connection, "ANALYZE");
            
            // Compact database to remove fragmentation
            await ExecuteCommandAsync(connection, "VACUUM");
            
            // Set optimal cache size for querying
            await ExecuteCommandAsync(connection, "PRAGMA cache_size = 10000"); // 40MB
            
            transaction.Commit();
            
            _logger?.LogInformation("✅ Database optimized for querying");
            _logger?.LogInformation("   - Synchronous writes re-enabled");
            _logger?.LogInformation("   - Comprehensive indexes created");
            _logger?.LogInformation("   - Database analyzed and compacted");
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger?.LogError(ex, "Failed to optimize database for querying");
            throw;
        }
    }
    
    /// <summary>
    /// Create optimized table schema for 20-year dataset
    /// </summary>
    public async Task CreateOptimizedSchemaAsync()
    {
        _logger?.LogInformation("🏗️ Creating optimized schema for 20-year dataset...");
        
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        using var transaction = connection.BeginTransaction();
        
        try
        {
            // Drop existing tables if they exist
            await ExecuteCommandAsync(connection, "DROP TABLE IF EXISTS market_data");
            await ExecuteCommandAsync(connection, "DROP TABLE IF EXISTS symbols");
            await ExecuteCommandAsync(connection, "DROP TABLE IF EXISTS data_quality");
            
            // Create symbols lookup table for normalization
            var createSymbolsTable = @"
                CREATE TABLE symbols (
                    id INTEGER PRIMARY KEY,
                    symbol TEXT UNIQUE NOT NULL,
                    name TEXT,
                    sector TEXT,
                    created_at INTEGER DEFAULT (strftime('%s', 'now'))
                )";
            
            await ExecuteCommandAsync(connection, createSymbolsTable);
            
            // Create main market data table with optimized structure
            var createMarketDataTable = @"
                CREATE TABLE market_data (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    symbol_id INTEGER NOT NULL,
                    timestamp INTEGER NOT NULL,  -- Unix timestamp for fast sorting
                    date_key INTEGER NOT NULL,   -- YYYYMMDD for fast date filtering
                    open_price INTEGER NOT NULL, -- Store as integers (price * 10000) for exact decimal math
                    high_price INTEGER NOT NULL,
                    low_price INTEGER NOT NULL,
                    close_price INTEGER NOT NULL,
                    volume INTEGER NOT NULL,
                    vwap_price INTEGER NOT NULL,
                    vix_value INTEGER,
                    data_source TEXT,
                    quality_score INTEGER DEFAULT 100,
                    created_at INTEGER DEFAULT (strftime('%s', 'now')),
                    
                    FOREIGN KEY (symbol_id) REFERENCES symbols(id),
                    
                    -- Compound primary key alternative for better performance
                    UNIQUE(symbol_id, timestamp)
                ) WITHOUT ROWID";
            
            await ExecuteCommandAsync(connection, createMarketDataTable);
            
            // Create data quality tracking table
            var createQualityTable = @"
                CREATE TABLE data_quality (
                    id INTEGER PRIMARY KEY,
                    symbol_id INTEGER NOT NULL,
                    date_key INTEGER NOT NULL,
                    completeness_score REAL,
                    anomaly_count INTEGER DEFAULT 0,
                    gap_count INTEGER DEFAULT 0,
                    validation_timestamp INTEGER DEFAULT (strftime('%s', 'now')),
                    notes TEXT,
                    
                    FOREIGN KEY (symbol_id) REFERENCES symbols(id),
                    UNIQUE(symbol_id, date_key)
                )";
            
            await ExecuteCommandAsync(connection, createQualityTable);
            
            // Insert common symbols
            await InsertCommonSymbolsAsync(connection);
            
            transaction.Commit();
            
            _logger?.LogInformation("✅ Optimized schema created");
            _logger?.LogInformation("   - Normalized symbol references");
            _logger?.LogInformation("   - Integer-based price storage for precision");
            _logger?.LogInformation("   - Optimized indexing structure");
            _logger?.LogInformation("   - Data quality tracking included");
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger?.LogError(ex, "Failed to create optimized schema");
            throw;
        }
    }
    
    /// <summary>
    /// Create comprehensive indexes for fast data access
    /// </summary>
    private async Task CreateOptimizedIndexesAsync(SqliteConnection connection)
    {
        _logger?.LogDebug("Creating optimized indexes...");
        
        var indexes = new[]
        {
            // Primary access patterns for backtesting
            "CREATE INDEX IF NOT EXISTS idx_market_data_symbol_date ON market_data(symbol_id, date_key)",
            "CREATE INDEX IF NOT EXISTS idx_market_data_timestamp ON market_data(timestamp)",
            "CREATE INDEX IF NOT EXISTS idx_market_data_date_key ON market_data(date_key)",
            
            // For date range queries
            "CREATE INDEX IF NOT EXISTS idx_market_data_symbol_timestamp ON market_data(symbol_id, timestamp)",
            
            // For data quality analysis
            "CREATE INDEX IF NOT EXISTS idx_market_data_quality ON market_data(quality_score)",
            "CREATE INDEX IF NOT EXISTS idx_data_quality_symbol_date ON data_quality(symbol_id, date_key)",
            
            // For symbol lookups
            "CREATE INDEX IF NOT EXISTS idx_symbols_symbol ON symbols(symbol)",
            
            // Covering index for common queries (includes data in index)
            "CREATE INDEX IF NOT EXISTS idx_market_data_covering ON market_data(symbol_id, date_key, close_price, volume)",
            
            // For VIX analysis
            "CREATE INDEX IF NOT EXISTS idx_market_data_vix ON market_data(vix_value) WHERE vix_value IS NOT NULL"
        };
        
        foreach (var indexSql in indexes)
        {
            await ExecuteCommandAsync(connection, indexSql);
        }
        
        _logger?.LogDebug($"Created {indexes.Length} optimized indexes");
    }
    
    /// <summary>
    /// Insert common trading symbols for normalization
    /// </summary>
    private async Task InsertCommonSymbolsAsync(SqliteConnection connection)
    {
        var symbols = new[]
        {
            ("SPY", "SPDR S&P 500 ETF", "ETF"),
            ("XSP", "SPDR S&P 500 Mini ETF", "ETF"),
            ("QQQ", "Invesco QQQ Trust", "ETF"),
            ("IWM", "iShares Russell 2000 ETF", "ETF"),
            ("VIX", "CBOE Volatility Index", "Index"),
            ("XLF", "Financial Select Sector SPDR", "ETF"),
            ("XLE", "Energy Select Sector SPDR", "ETF"),
            ("XLK", "Technology Select Sector SPDR", "ETF"),
            ("AAPL", "Apple Inc.", "Technology"),
            ("MSFT", "Microsoft Corporation", "Technology"),
            ("GOOGL", "Alphabet Inc.", "Technology"),
            ("AMZN", "Amazon.com Inc.", "Consumer Discretionary"),
            ("TSLA", "Tesla Inc.", "Consumer Discretionary"),
            ("NVDA", "NVIDIA Corporation", "Technology"),
            ("META", "Meta Platforms Inc.", "Technology")
        };
        
        foreach (var (symbol, name, sector) in symbols)
        {
            var insertSql = @"
                INSERT OR IGNORE INTO symbols (symbol, name, sector) 
                VALUES (@symbol, @name, @sector)";
            
            using var command = new SqliteCommand(insertSql, connection);
            command.Parameters.AddWithValue("@symbol", symbol);
            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@sector", sector);
            
            await command.ExecuteNonQueryAsync();
        }
        
        _logger?.LogDebug($"Inserted {symbols.Length} common symbols");
    }
    
    /// <summary>
    /// Get database performance statistics
    /// </summary>
    public async Task<DatabasePerformanceStats> GetPerformanceStatsAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var stats = new DatabasePerformanceStats();
        
        // Database size
        using (var command = new SqliteCommand("PRAGMA page_count", connection))
        {
            var pageCount = Convert.ToInt64(await command.ExecuteScalarAsync());
            
            using var pageSizeCommand = new SqliteCommand("PRAGMA page_size", connection);
            var pageSize = Convert.ToInt32(await pageSizeCommand.ExecuteScalarAsync());
            
            stats.DatabaseSizeBytes = pageCount * pageSize;
        }
        
        // Record count
        using (var command = new SqliteCommand("SELECT COUNT(*) FROM market_data", connection))
        {
            stats.TotalRecords = Convert.ToInt64(await command.ExecuteScalarAsync());
        }
        
        // Date range
        using (var command = new SqliteCommand(@"
            SELECT MIN(date_key), MAX(date_key) 
            FROM market_data", connection))
        {
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                if (!reader.IsDBNull(0))
                {
                    var minDateKey = reader.GetInt32(0);
                    var maxDateKey = reader.GetInt32(1);
                    
                    stats.EarliestDate = ParseDateKey(minDateKey);
                    stats.LatestDate = ParseDateKey(maxDateKey);
                }
            }
        }
        
        // Index statistics
        using (var command = new SqliteCommand(@"
            SELECT name FROM sqlite_master 
            WHERE type = 'index' AND tbl_name = 'market_data'", connection))
        {
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                stats.IndexCount++;
            }
        }
        
        return stats;
    }
    
    private async Task ExecuteCommandAsync(SqliteConnection connection, string sql)
    {
        using var command = new SqliteCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
    
    private DateTime ParseDateKey(int dateKey)
    {
        var year = dateKey / 10000;
        var month = (dateKey % 10000) / 100;
        var day = dateKey % 100;
        
        return new DateTime(year, month, day);
    }
}

/// <summary>
/// Database performance statistics
/// </summary>
public class DatabasePerformanceStats
{
    public long DatabaseSizeBytes { get; set; }
    public long TotalRecords { get; set; }
    public DateTime EarliestDate { get; set; }
    public DateTime LatestDate { get; set; }
    public int IndexCount { get; set; }
    
    public double DatabaseSizeMB => DatabaseSizeBytes / (1024.0 * 1024.0);
    public double DatabaseSizeGB => DatabaseSizeMB / 1024.0;
    public int TotalYears => (LatestDate - EarliestDate).Days / 365;
}