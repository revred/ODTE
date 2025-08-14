using System;
using System.IO;
using System.Threading.Tasks;
using ODTE.Backtest.Config;
using ODTE.Backtest.Core;
using ODTE.Backtest.Data;
using ODTE.Backtest.Engine;
using ODTE.Backtest.Reporting;
using ODTE.Backtest.Signals;
using ODTE.Backtest.Strategy;

namespace ODTE.Backtest.Engine;

/// <summary>
/// Executes single-day backtests with Trinity-compatible output structure
/// </summary>
public static class DayRunner
{
    /// <summary>
    /// Run a single day's backtest with complete isolation
    /// </summary>
    public static async Task<(DateOnly day, bool ok, DayResult result)> RunDayAsync(
        SimConfig cfg, 
        DateOnly day,
        IMarketData market, 
        IOptionsData options, 
        IEconCalendar econ,
        RegimeScorer scorer, 
        SpreadBuilder builder, 
        ExecutionEngine exec, 
        RiskManager risk,
        string? strategySubdir = null)
    {
        try
        {
            Console.WriteLine($"🔄 Processing {day:yyyy-MM-dd}...");
            
            // Create day-bounded backtest instance
            var bt = new Backtester(cfg, market, options, econ, scorer, builder, exec, risk);
            var report = await bt.RunAsync();
            
            // Determine output directory structure for Trinity integration
            string outDir = cfg.Paths.ReportsDir;
            
            // Trinity-compatible structure: Reports/YYYY/MM/strategy_name/
            var yearDir = Path.Combine(outDir, day.Year.ToString());
            var monthDir = Path.Combine(yearDir, day.Month.ToString("00"));
            
            if (!string.IsNullOrWhiteSpace(strategySubdir))
            {
                outDir = Path.Combine(monthDir, strategySubdir);
            }
            else
            {
                outDir = Path.Combine(monthDir, "baseline");
            }
            
            Directory.CreateDirectory(outDir);
            
            // Write day-specific artifacts
            // Note: SimConfig is not a record, so we can't use 'with' syntax
            // Just use the outDir directly in the reporter methods
            
            // Per-day files with date suffix for Trinity ingestion
            var daySuffix = $"_{day:yyyyMMdd}";
            
            // Write trades detail
            await WriteDayTradesAsync(cfg, report, day, outDir);
            
            // Write daily summary
            await WriteDaySummaryAsync(cfg, report, day, outDir);
            
            // Append to monthly ledger (Trinity main input)
            await AppendToMonthlyLedgerAsync(cfg, report, day, outDir);
            
            // Create day result for aggregation
            var result = new DayResult
            {
                Date = day,
                TradeCount = report.Trades.Count,
                PnL = report.NetPnL,
                MaxDrawdown = report.MaxDrawdown,
                WinRate = report.WinRate,
                AvgWin = report.AvgWin,
                AvgLoss = report.AvgLoss,
                Sharpe = report.Sharpe,
                Success = true
            };
            
            Console.WriteLine($"✅ {day:yyyy-MM-dd}: {report.Trades.Count} trades, P&L: ${report.NetPnL:N2}");
            
            return (day, true, result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ {day:yyyy-MM-dd}: {ex.Message}");
            return (day, false, new DayResult { Date = day, Success = false, ErrorMessage = ex.Message });
        }
    }
    
    private static async Task WriteDayTradesAsync(SimConfig cfg, RunReport report, DateOnly day, string outDir)
    {
        var tradesPath = Path.Combine(outDir, $"trades_{day:yyyyMMdd}.csv");
        
        if (report.Trades.Count == 0)
        {
            // Create empty file with headers for consistency
            await File.WriteAllTextAsync(tradesPath, "EntryTime,ExitTime,Type,ShortStrike,LongStrike,Width,EntryPrice,ExitPrice,PnL,Fees,MAE,MFE,ExitReason\n");
            return;
        }
        
        using var writer = new StreamWriter(tradesPath);
        
        // Write header
        await writer.WriteLineAsync("EntryTime,ExitTime,Type,ShortStrike,LongStrike,Width,EntryPrice,ExitPrice,PnL,Fees,MAE,MFE,ExitReason");
        
        // Write trade details for this specific day
        foreach (var trade in report.Trades.OrderBy(t => t.Pos.EntryTs))
        {
            double width = Math.Abs(trade.Pos.Order.Long.Strike - trade.Pos.Order.Short.Strike);
            
            await writer.WriteLineAsync($"{trade.Pos.EntryTs:yyyy-MM-dd HH:mm:ss}," +
                                       $"{trade.Pos.ExitTs:yyyy-MM-dd HH:mm:ss}," +
                                       $"{trade.Pos.Order.Type}," +
                                       $"{trade.Pos.Order.Short.Strike:F2}," +
                                       $"{trade.Pos.Order.Long.Strike:F2}," +
                                       $"{width:F0}," +
                                       $"{trade.Pos.EntryPrice:F2}," +
                                       $"{trade.Pos.ExitPrice:F2}," +
                                       $"{trade.PnL:F2}," +
                                       $"{trade.Fees:F2}," +
                                       $"{trade.MaxAdverseExcursion:F2}," +
                                       $"{trade.MaxFavorableExcursion:F2}," +
                                       $"\"{trade.Pos.ExitReason}\"");
        }
    }
    
    private static async Task WriteDaySummaryAsync(SimConfig cfg, RunReport report, DateOnly day, string outDir)
    {
        var summaryPath = Path.Combine(outDir, $"summary_{day:yyyyMMdd}.json");
        var summary = new
        {
            Date = day.ToString("yyyy-MM-dd"),
            Trades = report.Trades.Count,
            PnL = report.NetPnL,
            MaxDrawdown = report.MaxDrawdown,
            WinRate = report.WinRate,
            Sharpe = report.Sharpe,
            CalmarRatio = 0, // TODO: Calculate if needed
            AvgDailyPnL = report.NetPnL, // Single day
            StdDailyPnL = 0 // Single day
        };
        
        var json = System.Text.Json.JsonSerializer.Serialize(summary, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(summaryPath, json);
    }
    
    private static async Task AppendToMonthlyLedgerAsync(SimConfig cfg, RunReport report, DateOnly day, string outDir)
    {
        // Trinity-compatible ledger format
        var ledgerPath = Path.Combine(outDir, $"ledger_{day.Year}{day.Month:00}.csv");
        var isNewFile = !File.Exists(ledgerPath);
        
        using var writer = new StreamWriter(ledgerPath, append: true);
        
        if (isNewFile)
        {
            // Trinity-compatible header
            await writer.WriteLineAsync("date,strategy,underlying,trades,gross_pnl,fees,net_pnl,win_rate,max_dd,sharpe,calmar,cum_pnl");
        }
        
        var strategyName = Path.GetFileName(outDir) ?? "baseline";
        var line = $"{day:yyyy-MM-dd},{strategyName},{cfg.Underlying}," +
                   $"{report.Trades.Count},{report.GrossPnL:F2},{report.Fees:F2}," +
                   $"{report.NetPnL:F2},{report.WinRate * 100:F2}," +
                   $"{report.MaxDrawdown:F2},{report.Sharpe:F2},{0:F2}," +
                   $"{report.NetPnL:F2}";
        
        await writer.WriteLineAsync(line);
    }
}

/// <summary>
/// Result of a single day's backtest for aggregation
/// </summary>
public class DayResult
{
    public DateOnly Date { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int TradeCount { get; set; }
    public double PnL { get; set; }
    public double MaxDrawdown { get; set; }
    public double WinRate { get; set; }
    public double AvgWin { get; set; }
    public double AvgLoss { get; set; }
    public double Sharpe { get; set; }
}