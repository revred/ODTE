using Microsoft.Data.Sqlite;
using ODTE.Backtest.Core;
using System.Data;

namespace ODTE.Backtest.Data;

/// <summary>
/// SQLite database for trade logging and forensics analysis.
/// WHY: Persistent storage for ML analysis, compliance reporting, and performance tracking.
/// 
/// DESIGN PRINCIPLES:
/// - One database per trading day for performance and organization
/// - Optimized for time-series analysis and forensics queries
/// - Supports both real-time logging and batch analytics
/// - Schema designed for JSON export to ML pipelines
/// 
/// DATABASE STRUCTURE:
/// - trade_logs: Core trade execution data
/// - daily_summary: Aggregated performance metrics
/// - market_conditions: Environment data for correlation analysis
/// 
/// USAGE PATTERNS:
/// 1. Real-time: LogTrade() during execution
/// 2. Analytics: GetTradesByDate(), GetLosers()
/// 3. Forensics: Cluster analysis for pattern recognition
/// 4. Reporting: Daily/weekly performance summaries
/// 
/// Reference: Code Review Summary - Storage & Logging (SQLite + JSONL)
/// </summary>
public class TradeLogDatabase : IDisposable
{
    private readonly string _basePath;
    private readonly Dictionary<DateOnly, SqliteConnection> _connections = new();
    private bool _disposed = false;

    public TradeLogDatabase(string basePath = "./TradeLogs")
    {
        _basePath = basePath;
        Directory.CreateDirectory(_basePath);
    }

    /// <summary>
    /// Log trade execution to daily database.
    /// Creates new database file per day for performance and organization.
    /// </summary>
    /// <param name="tradeLog">Trade execution data</param>
    public async Task LogTradeAsync(TradeLog tradeLog)
    {
        var tradeDate = DateOnly.FromDateTime(tradeLog.Timestamp);
        var connection = await GetConnectionAsync(tradeDate);

        const string sql = @"
            INSERT INTO trade_logs (
                timestamp, symbol, expiry, right, strike, spread_type,
                max_loss, exit_pnl, exit_reason, market_regime,
                json_data
            ) VALUES (
                @timestamp, @symbol, @expiry, @right, @strike, @spread_type,
                @max_loss, @exit_pnl, @exit_reason, @market_regime,
                @json_data
            )";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@timestamp", tradeLog.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        command.Parameters.AddWithValue("@symbol", tradeLog.Symbol);
        command.Parameters.AddWithValue("@expiry", tradeLog.Expiry.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("@right", tradeLog.Right.ToString());
        command.Parameters.AddWithValue("@strike", tradeLog.Strike);
        command.Parameters.AddWithValue("@spread_type", tradeLog.Type.ToString());
        command.Parameters.AddWithValue("@max_loss", tradeLog.MaxLoss);
        command.Parameters.AddWithValue("@exit_pnl", tradeLog.ExitPnL);
        command.Parameters.AddWithValue("@exit_reason", tradeLog.ExitReason);
        command.Parameters.AddWithValue("@market_regime", tradeLog.MarketRegime);
        command.Parameters.AddWithValue("@json_data", tradeLog.ToJson());

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Retrieve all trades for a specific date.
    /// Useful for daily performance analysis and forensics.
    /// </summary>
    public async Task<List<TradeLog>> GetTradesByDateAsync(DateOnly date)
    {
        var connection = await GetConnectionAsync(date);

        const string sql = @"
            SELECT timestamp, symbol, expiry, right, strike, spread_type,
                   max_loss, exit_pnl, exit_reason, market_regime
            FROM trade_logs 
            ORDER BY timestamp";

        using var command = new SqliteCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync();

        var trades = new List<TradeLog>();
        while (await reader.ReadAsync())
        {
            trades.Add(new TradeLog(
                Timestamp: DateTime.Parse(reader.GetString("timestamp")),
                Symbol: reader.GetString("symbol"),
                Expiry: DateOnly.Parse(reader.GetString("expiry")),
                Right: Enum.Parse<Right>(reader.GetString("right")),
                Strike: reader.GetDecimal("strike"),
                Type: Enum.Parse<SpreadType>(reader.GetString("spread_type")),
                MaxLoss: reader.GetDecimal("max_loss"),
                ExitPnL: reader.GetDecimal("exit_pnl"),
                ExitReason: reader.GetString("exit_reason"),
                MarketRegime: reader.GetString("market_regime")
            ));
        }

        return trades;
    }

    /// <summary>
    /// Get losing trades for forensics analysis.
    /// Filters to negative P&L trades for ML pattern recognition.
    /// </summary>
    public async Task<List<TradeLog>> GetLosingTradesAsync(DateOnly date, decimal minLoss = -1m)
    {
        var connection = await GetConnectionAsync(date);

        const string sql = @"
            SELECT timestamp, symbol, expiry, right, strike, spread_type,
                   max_loss, exit_pnl, exit_reason, market_regime
            FROM trade_logs 
            WHERE exit_pnl <= @min_loss
            ORDER BY exit_pnl ASC";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@min_loss", minLoss);
        using var reader = await command.ExecuteReaderAsync();

        var trades = new List<TradeLog>();
        while (await reader.ReadAsync())
        {
            trades.Add(new TradeLog(
                Timestamp: DateTime.Parse(reader.GetString("timestamp")),
                Symbol: reader.GetString("symbol"),
                Expiry: DateOnly.Parse(reader.GetString("expiry")),
                Right: Enum.Parse<Right>(reader.GetString("right")),
                Strike: reader.GetDecimal("strike"),
                Type: Enum.Parse<SpreadType>(reader.GetString("spread_type")),
                MaxLoss: reader.GetDecimal("max_loss"),
                ExitPnL: reader.GetDecimal("exit_pnl"),
                ExitReason: reader.GetString("exit_reason"),
                MarketRegime: reader.GetString("market_regime")
            ));
        }

        return trades;
    }

    /// <summary>
    /// Generate daily performance summary for reporting.
    /// Aggregates all trades for the day into key metrics.
    /// </summary>
    public async Task<DailyTradingSummary> GetDailySummaryAsync(DateOnly date)
    {
        var connection = await GetConnectionAsync(date);

        const string sql = @"
            SELECT 
                COUNT(*) as total_trades,
                SUM(CASE WHEN exit_pnl > 0 THEN 1 ELSE 0 END) as winning_trades,
                SUM(exit_pnl) as total_pnl,
                AVG(exit_pnl) as avg_pnl,
                MIN(exit_pnl) as worst_trade,
                MAX(exit_pnl) as best_trade,
                SUM(max_loss) as total_risk_deployed
            FROM trade_logs";

        using var command = new SqliteCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            var totalTrades = reader.GetInt32("total_trades");
            var winningTrades = reader.GetInt32("winning_trades");

            return new DailyTradingSummary(
                Date: date,
                TotalTrades: totalTrades,
                WinningTrades: winningTrades,
                WinRate: totalTrades > 0 ? (double)winningTrades / totalTrades : 0,
                TotalPnL: reader.IsDBNull("total_pnl") ? 0 : reader.GetDecimal("total_pnl"),
                AvgPnL: reader.IsDBNull("avg_pnl") ? 0 : reader.GetDecimal("avg_pnl"),
                WorstTrade: reader.IsDBNull("worst_trade") ? 0 : reader.GetDecimal("worst_trade"),
                BestTrade: reader.IsDBNull("best_trade") ? 0 : reader.GetDecimal("best_trade"),
                TotalRiskDeployed: reader.IsDBNull("total_risk_deployed") ? 0 : reader.GetDecimal("total_risk_deployed")
            );
        }

        return new DailyTradingSummary(date, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    /// <summary>
    /// Export trades to JSONL format for ML pipelines.
    /// Creates one JSON object per line for streaming analytics.
    /// </summary>
    public async Task ExportToJsonlAsync(DateOnly date, string filePath)
    {
        var trades = await GetTradesByDateAsync(date);

        using var writer = new StreamWriter(filePath);
        foreach (var trade in trades)
        {
            await writer.WriteLineAsync(trade.ToJson());
        }
    }

    /// <summary>
    /// Get connection for specific trading day, creating database if needed.
    /// Each day gets its own SQLite file for performance and organization.
    /// </summary>
    private async Task<SqliteConnection> GetConnectionAsync(DateOnly date)
    {
        if (_connections.TryGetValue(date, out var existingConnection))
        {
            return existingConnection;
        }

        var dbPath = Path.Combine(_basePath, $"trades_{date:yyyy-MM-dd}.db");
        var connectionString = $"Data Source={dbPath}";

        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        // Create tables if they don't exist
        await CreateTablesAsync(connection);

        _connections[date] = connection;
        return connection;
    }

    /// <summary>
    /// Create database schema optimized for trade logging and analytics.
    /// </summary>
    private async Task CreateTablesAsync(SqliteConnection connection)
    {
        const string createTradeLogsTable = @"
            CREATE TABLE IF NOT EXISTS trade_logs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp TEXT NOT NULL,
                symbol TEXT NOT NULL,
                expiry TEXT NOT NULL,
                right TEXT NOT NULL,
                strike DECIMAL(10,2) NOT NULL,
                spread_type TEXT NOT NULL,
                max_loss DECIMAL(10,2) NOT NULL,
                exit_pnl DECIMAL(10,2) NOT NULL,
                exit_reason TEXT NOT NULL,
                market_regime TEXT NOT NULL,
                json_data TEXT NOT NULL,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP
            )";

        const string createIndexes = @"
            CREATE INDEX IF NOT EXISTS idx_trade_logs_timestamp ON trade_logs(timestamp);
            CREATE INDEX IF NOT EXISTS idx_trade_logs_symbol ON trade_logs(symbol);
            CREATE INDEX IF NOT EXISTS idx_trade_logs_pnl ON trade_logs(exit_pnl);
            CREATE INDEX IF NOT EXISTS idx_trade_logs_regime ON trade_logs(market_regime);
        ";

        using var command = new SqliteCommand(createTradeLogsTable, connection);
        await command.ExecuteNonQueryAsync();

        using var indexCommand = new SqliteCommand(createIndexes, connection);
        await indexCommand.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Cleanup and close all database connections.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var connection in _connections.Values)
            {
                connection?.Dispose();
            }
            _connections.Clear();
            _disposed = true;
        }
    }
}

/// <summary>
/// Daily trading performance summary for reporting and analysis.
/// </summary>
public record DailyTradingSummary(
    DateOnly Date,
    int TotalTrades,
    int WinningTrades,
    double WinRate,
    decimal TotalPnL,
    decimal AvgPnL,
    decimal WorstTrade,
    decimal BestTrade,
    decimal TotalRiskDeployed
);