using Microsoft.Data.Sqlite;
using System.Collections.Concurrent;

namespace ODTE.Historical.DistributedStorage;

/// <summary>
/// Manages distributed SQLite connections with connection pooling and smart caching
/// Optimized for high-performance commodity and options data access
/// </summary>
public class DistributedDatabaseManager : IDisposable
{
    private readonly FileManager _fileManager;
    private readonly ConcurrentDictionary<string, SqliteConnection> _connectionPool;
    private readonly ConcurrentDictionary<string, DateTime> _lastAccessed;
    private readonly Timer _cleanupTimer;
    private readonly int _maxConnections;
    private readonly TimeSpan _connectionTimeout;

    public DistributedDatabaseManager(string baseDataPath = @"C:\code\ODTE\data",
        int maxConnections = 50, TimeSpan? connectionTimeout = null)
    {
        _fileManager = new FileManager(baseDataPath);
        _connectionPool = new ConcurrentDictionary<string, SqliteConnection>();
        _lastAccessed = new ConcurrentDictionary<string, DateTime>();
        _maxConnections = maxConnections;
        _connectionTimeout = connectionTimeout ?? TimeSpan.FromMinutes(30);

        // Cleanup unused connections every 5 minutes
        _cleanupTimer = new Timer(CleanupConnections, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Get commodity data for a symbol over a date range
    /// Automatically queries multiple monthly files and combines results
    /// </summary>
    public async Task<List<MarketDataBar>> GetCommodityDataAsync(string symbol, DateTime startDate, DateTime endDate,
        CommodityCategory category = CommodityCategory.Oil)
    {
        var files = _fileManager.GetCommodityFilesInRange(symbol, startDate, endDate, category);
        if (!files.Any())
        {
            Console.WriteLine($"⚠️ No commodity files found for {symbol} from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            return new List<MarketDataBar>();
        }

        Console.WriteLine($"📊 Loading {symbol} data from {files.Count} monthly files");

        // Parallel loading of multiple files
        var tasks = files.Select(async file =>
        {
            var connection = await GetConnectionAsync(file);
            return await LoadCommodityDataFromFile(connection, symbol, startDate, endDate);
        });

        var results = await Task.WhenAll(tasks);

        // Combine and sort results
        var combinedData = results.SelectMany(r => r)
            .Where(bar => bar.Timestamp >= startDate && bar.Timestamp <= endDate)
            .OrderBy(bar => bar.Timestamp)
            .ToList();

        Console.WriteLine($"✅ Loaded {combinedData.Count} data points for {symbol}");
        return combinedData;
    }

    /// <summary>
    /// Get options chain data for a specific expiration
    /// </summary>
    public async Task<OptionsChain> GetOptionsChainAsync(string symbol, DateTime expirationDate,
        CommodityCategory category = CommodityCategory.Oil)
    {
        var filePath = _fileManager.GetOptionsPath(symbol, expirationDate, category);

        if (!File.Exists(filePath))
        {
            Console.WriteLine($"⚠️ Options file not found: {filePath}");
            return new OptionsChain { Symbol = symbol, ExpirationDate = expirationDate };
        }

        var connection = await GetConnectionAsync(filePath);
        return await LoadOptionsChainFromFile(connection, symbol, expirationDate);
    }

    /// <summary>
    /// Get all available options expirations for a symbol within date range
    /// </summary>
    public async Task<List<DateTime>> GetAvailableExpirationsAsync(string symbol, DateTime startDate, DateTime endDate,
        CommodityCategory category = CommodityCategory.Oil)
    {
        var files = _fileManager.GetOptionsFilesInRange(symbol, startDate, endDate, category);
        var expirations = new List<DateTime>();

        foreach (var file in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            if (fileName.Contains("_OPT_") && fileName.Length >= 16)
            {
                var dateStr = fileName.Substring(fileName.Length - 8);
                if (DateTime.TryParseExact(dateStr, "yyyyMMdd", null,
                    System.Globalization.DateTimeStyles.None, out var expiration))
                {
                    expirations.Add(expiration);
                }
            }
        }

        return expirations.Distinct().OrderBy(d => d).ToList();
    }

    /// <summary>
    /// Store commodity data to appropriate monthly file
    /// </summary>
    public async Task StoreCommodityDataAsync(string symbol, List<MarketDataBar> data,
        CommodityCategory category = CommodityCategory.Oil)
    {
        if (!data.Any()) return;

        // Group data by month
        var monthlyGroups = data.GroupBy(bar => new { bar.Timestamp.Year, bar.Timestamp.Month });

        foreach (var group in monthlyGroups)
        {
            var monthDate = new DateTime(group.Key.Year, group.Key.Month, 1);
            var filePath = _fileManager.GetCommodityPath(symbol, monthDate, category);

            _fileManager.EnsureDirectoryExists(filePath);

            var connection = await GetConnectionAsync(filePath);
            await InitializeCommoditySchema(connection);
            await StoreCommodityDataToFile(connection, symbol, group.ToList());
        }

        Console.WriteLine($"✅ Stored {data.Count} {symbol} data points across {monthlyGroups.Count()} monthly files");
    }

    /// <summary>
    /// Store options chain data to appropriate expiration file
    /// </summary>
    public async Task StoreOptionsChainAsync(string symbol, OptionsChain optionsChain,
        CommodityCategory category = CommodityCategory.Oil)
    {
        var filePath = _fileManager.GetOptionsPath(symbol, optionsChain.ExpirationDate, category);

        _fileManager.EnsureDirectoryExists(filePath);

        var connection = await GetConnectionAsync(filePath);
        await InitializeOptionsSchema(connection);
        await StoreOptionsChainToFile(connection, optionsChain);

        Console.WriteLine($"✅ Stored {optionsChain.Options.Count} options contracts for {symbol} expiring {optionsChain.ExpirationDate:yyyy-MM-dd}");
    }

    /// <summary>
    /// Get storage statistics across all files
    /// </summary>
    public FileStorageStats GetStorageStats(string symbol, CommodityCategory category = CommodityCategory.Oil)
    {
        return _fileManager.GetStorageStats(symbol, category);
    }

    /// <summary>
    /// Get connection for a specific file with pooling
    /// </summary>
    private async Task<SqliteConnection> GetConnectionAsync(string filePath)
    {
        _lastAccessed[filePath] = DateTime.UtcNow;

        if (_connectionPool.TryGetValue(filePath, out var existingConnection))
        {
            if (existingConnection.State == System.Data.ConnectionState.Open)
            {
                return existingConnection;
            }
            else
            {
                _connectionPool.TryRemove(filePath, out _);
                existingConnection.Dispose();
            }
        }

        // Ensure we don't exceed max connections
        if (_connectionPool.Count >= _maxConnections)
        {
            CleanupOldestConnections(10);
        }

        var connectionString = $"Data Source={filePath};Cache=Shared;";
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        // SQLite optimizations
        await ExecuteNonQueryAsync(connection, "PRAGMA journal_mode=WAL");
        await ExecuteNonQueryAsync(connection, "PRAGMA synchronous=NORMAL");
        await ExecuteNonQueryAsync(connection, "PRAGMA cache_size=5000");
        await ExecuteNonQueryAsync(connection, "PRAGMA temp_store=MEMORY");

        _connectionPool[filePath] = connection;
        return connection;
    }

    private async Task<List<MarketDataBar>> LoadCommodityDataFromFile(SqliteConnection connection, string symbol,
        DateTime startDate, DateTime endDate)
    {
        var query = @"
            SELECT timestamp, open_price, high_price, low_price, close_price, volume, vwap_price
            FROM market_data m
            JOIN symbols s ON m.symbol_id = s.id
            WHERE s.symbol = @symbol 
              AND timestamp >= @startUnix 
              AND timestamp <= @endUnix
            ORDER BY timestamp";

        using var command = new SqliteCommand(query, connection);
        command.Parameters.AddWithValue("@symbol", symbol);
        command.Parameters.AddWithValue("@startUnix", ((DateTimeOffset)startDate).ToUnixTimeSeconds());
        command.Parameters.AddWithValue("@endUnix", ((DateTimeOffset)endDate).ToUnixTimeSeconds());

        var results = new List<MarketDataBar>();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            results.Add(new MarketDataBar
            {
                Timestamp = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(0)).DateTime,
                Open = (double)reader.GetInt32(1) / 10000.0,
                High = (double)reader.GetInt32(2) / 10000.0,
                Low = (double)reader.GetInt32(3) / 10000.0,
                Close = (double)reader.GetInt32(4) / 10000.0,
                Volume = reader.GetInt64(5),
                VWAP = (double)reader.GetInt32(6) / 10000.0
            });
        }

        return results;
    }

    private async Task<OptionsChain> LoadOptionsChainFromFile(SqliteConnection connection, string symbol, DateTime expirationDate)
    {
        var optionsChain = new OptionsChain
        {
            Symbol = symbol,
            ExpirationDate = expirationDate,
            Options = new List<OptionContract>()
        };

        var query = @"
            SELECT option_symbol, contract_type, strike_price, bid_price, ask_price, last_price,
                   volume, open_interest, implied_volatility, delta, gamma, theta, vega, underlying_price
            FROM options_data
            WHERE expiration_date = @expirationDate
            ORDER BY contract_type, strike_price";

        using var command = new SqliteCommand(query, connection);
        command.Parameters.AddWithValue("@expirationDate", int.Parse(expirationDate.ToString("yyyyMMdd")));

        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            optionsChain.Options.Add(new OptionContract
            {
                Symbol = reader.GetString(0),
                Type = (OptionType)reader.GetInt32(1),
                Strike = (decimal)reader.GetInt32(2) / 10000m,
                Bid = (decimal)reader.GetInt32(3) / 10000m,
                Ask = (decimal)reader.GetInt32(4) / 10000m,
                Last = (decimal)reader.GetInt32(5) / 10000m,
                Volume = reader.GetInt32(6),
                OpenInterest = reader.GetInt32(7),
                ImpliedVolatility = (decimal)reader.GetInt32(8) / 10000m,
                Delta = (decimal)reader.GetInt32(9) / 10000m,
                Gamma = (decimal)reader.GetInt32(10) / 100000m,
                Theta = (decimal)reader.GetInt32(11) / 10000m,
                Vega = (decimal)reader.GetInt32(12) / 10000m,
                UnderlyingPrice = (decimal)reader.GetInt32(13) / 10000m
            });
        }

        Console.WriteLine($"📈 Loaded {optionsChain.Options.Count} options contracts for {symbol} {expirationDate:yyyy-MM-dd}");
        return optionsChain;
    }

    private async Task InitializeCommoditySchema(SqliteConnection connection)
    {
        await ExecuteNonQueryAsync(connection, @"
            CREATE TABLE IF NOT EXISTS symbols (
                id INTEGER PRIMARY KEY,
                symbol TEXT UNIQUE NOT NULL,
                description TEXT
            )");

        await ExecuteNonQueryAsync(connection, @"
            CREATE TABLE IF NOT EXISTS market_data (
                id INTEGER PRIMARY KEY,
                timestamp INTEGER NOT NULL,
                symbol_id INTEGER NOT NULL,
                open_price INTEGER NOT NULL,
                high_price INTEGER NOT NULL,  
                low_price INTEGER NOT NULL,
                close_price INTEGER NOT NULL,
                volume INTEGER NOT NULL,
                vwap_price INTEGER NOT NULL,
                UNIQUE(timestamp, symbol_id)
            )");

        await ExecuteNonQueryAsync(connection, @"
            CREATE INDEX IF NOT EXISTS idx_market_data_timestamp 
            ON market_data(timestamp)");
    }

    private async Task InitializeOptionsSchema(SqliteConnection connection)
    {
        await ExecuteNonQueryAsync(connection, @"
            CREATE TABLE IF NOT EXISTS options_data (
                id INTEGER PRIMARY KEY,
                timestamp INTEGER NOT NULL,
                underlying_symbol TEXT NOT NULL,
                option_symbol TEXT NOT NULL,
                contract_type INTEGER NOT NULL,
                strike_price INTEGER NOT NULL,
                expiration_date INTEGER NOT NULL,
                days_to_expiration INTEGER NOT NULL,
                bid_price INTEGER NOT NULL,
                ask_price INTEGER NOT NULL,
                last_price INTEGER NOT NULL,
                volume INTEGER NOT NULL,
                open_interest INTEGER NOT NULL,
                implied_volatility INTEGER NOT NULL,
                delta INTEGER NOT NULL,
                gamma INTEGER NOT NULL,
                theta INTEGER NOT NULL,
                vega INTEGER NOT NULL,
                underlying_price INTEGER NOT NULL,
                UNIQUE(option_symbol, timestamp)
            )");

        await ExecuteNonQueryAsync(connection, @"
            CREATE INDEX IF NOT EXISTS idx_options_expiration_strike 
            ON options_data(expiration_date, strike_price)");
    }

    private async Task StoreCommodityDataToFile(SqliteConnection connection, string symbol, List<MarketDataBar> data)
    {
        // Get or create symbol ID
        var symbolId = await GetOrCreateSymbolId(connection, symbol);

        using var transaction = connection.BeginTransaction();

        var insertQuery = @"
            INSERT OR REPLACE INTO market_data 
            (timestamp, symbol_id, open_price, high_price, low_price, close_price, volume, vwap_price)
            VALUES (@timestamp, @symbolId, @open, @high, @low, @close, @volume, @vwap)";

        foreach (var bar in data)
        {
            using var command = new SqliteCommand(insertQuery, connection, transaction);
            command.Parameters.AddWithValue("@timestamp", ((DateTimeOffset)bar.Timestamp).ToUnixTimeSeconds());
            command.Parameters.AddWithValue("@symbolId", symbolId);
            command.Parameters.AddWithValue("@open", (int)(bar.Open * 10000));
            command.Parameters.AddWithValue("@high", (int)(bar.High * 10000));
            command.Parameters.AddWithValue("@low", (int)(bar.Low * 10000));
            command.Parameters.AddWithValue("@close", (int)(bar.Close * 10000));
            command.Parameters.AddWithValue("@volume", bar.Volume);
            command.Parameters.AddWithValue("@vwap", (int)(bar.VWAP * 10000));

            await command.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    private async Task StoreOptionsChainToFile(SqliteConnection connection, OptionsChain optionsChain)
    {
        using var transaction = connection.BeginTransaction();

        var insertQuery = @"
            INSERT OR REPLACE INTO options_data 
            (timestamp, underlying_symbol, option_symbol, contract_type, strike_price, expiration_date, days_to_expiration,
             bid_price, ask_price, last_price, volume, open_interest, implied_volatility, 
             delta, gamma, theta, vega, underlying_price)
            VALUES (@timestamp, @underlying, @option, @type, @strike, @expiration, @dte,
                    @bid, @ask, @last, @volume, @oi, @iv, @delta, @gamma, @theta, @vega, @underlyingPrice)";

        var timestamp = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds();
        var expirationInt = int.Parse(optionsChain.ExpirationDate.ToString("yyyyMMdd"));

        foreach (var option in optionsChain.Options)
        {
            using var command = new SqliteCommand(insertQuery, connection, transaction);
            command.Parameters.AddWithValue("@timestamp", timestamp);
            command.Parameters.AddWithValue("@underlying", optionsChain.Symbol);
            command.Parameters.AddWithValue("@option", option.Symbol);
            command.Parameters.AddWithValue("@type", (int)option.Type);
            command.Parameters.AddWithValue("@strike", (int)(option.Strike * 10000));
            command.Parameters.AddWithValue("@expiration", expirationInt);
            command.Parameters.AddWithValue("@dte", (optionsChain.ExpirationDate - DateTime.Today).Days);
            command.Parameters.AddWithValue("@bid", (int)(option.Bid * 10000));
            command.Parameters.AddWithValue("@ask", (int)(option.Ask * 10000));
            command.Parameters.AddWithValue("@last", (int)(option.Last * 10000));
            command.Parameters.AddWithValue("@volume", option.Volume);
            command.Parameters.AddWithValue("@oi", option.OpenInterest);
            command.Parameters.AddWithValue("@iv", (int)(option.ImpliedVolatility * 10000));
            command.Parameters.AddWithValue("@delta", (int)(option.Delta * 10000));
            command.Parameters.AddWithValue("@gamma", (int)(option.Gamma * 100000));
            command.Parameters.AddWithValue("@theta", (int)(option.Theta * 10000));
            command.Parameters.AddWithValue("@vega", (int)(option.Vega * 10000));
            command.Parameters.AddWithValue("@underlyingPrice", (int)(option.UnderlyingPrice * 10000));

            await command.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    private async Task<int> GetOrCreateSymbolId(SqliteConnection connection, string symbol)
    {
        var selectQuery = "SELECT id FROM symbols WHERE symbol = @symbol";
        using var selectCommand = new SqliteCommand(selectQuery, connection);
        selectCommand.Parameters.AddWithValue("@symbol", symbol);

        var result = await selectCommand.ExecuteScalarAsync();
        if (result != null)
        {
            return Convert.ToInt32(result);
        }

        var insertQuery = "INSERT INTO symbols (symbol) VALUES (@symbol); SELECT last_insert_rowid()";
        using var insertCommand = new SqliteCommand(insertQuery, connection);
        insertCommand.Parameters.AddWithValue("@symbol", symbol);

        result = await insertCommand.ExecuteScalarAsync();
        return Convert.ToInt32(result!);
    }

    private async Task ExecuteNonQueryAsync(SqliteConnection connection, string sql)
    {
        using var command = new SqliteCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private void CleanupConnections(object? state)
    {
        CleanupOldestConnections(_maxConnections / 4); // Clean 25% of connections
    }

    private void CleanupOldestConnections(int count)
    {
        var oldConnections = _lastAccessed
            .OrderBy(kvp => kvp.Value)
            .Where(kvp => DateTime.UtcNow - kvp.Value > _connectionTimeout)
            .Take(count)
            .ToList();

        foreach (var old in oldConnections)
        {
            if (_connectionPool.TryRemove(old.Key, out var connection))
            {
                connection.Dispose();
                _lastAccessed.TryRemove(old.Key, out _);
            }
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();

        foreach (var connection in _connectionPool.Values)
        {
            connection.Dispose();
        }

        _connectionPool.Clear();
        _lastAccessed.Clear();
    }
}