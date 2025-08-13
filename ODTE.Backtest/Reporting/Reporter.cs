using System.Text;
using ODTE.Backtest.Config;
using ODTE.Backtest.Core;

namespace ODTE.Backtest.Reporting;

/// <summary>
/// Performance reporting and trade analysis utilities.
/// WHY: Transforms raw backtest results into actionable insights for strategy evaluation.
/// 
/// REPORTING PHILOSOPHY:
/// "What gets measured gets managed" - Peter Drucker
/// Comprehensive metrics help identify:
/// - Strategy strengths and weaknesses
/// - Optimal parameter ranges
/// - Risk-adjusted performance quality
/// - Areas for improvement
/// 
/// DUAL OUTPUT FORMAT:
/// 1. Summary Report: High-level metrics for quick assessment
/// 2. Trade Log: Detailed CSV for in-depth analysis
/// 
/// KEY METRICS PROVIDED:
/// - Win Rate: Percentage of profitable trades
/// - Risk-Adjusted Returns: Sharpe ratio, max drawdown
/// - Trade Distribution: Performance by strategy type
/// - P&L Attribution: Gross vs net (fees matter!)
/// 
/// ANALYSIS WORKFLOW:
/// 1. Run backtest → Generate reports
/// 2. Review summary for overall performance
/// 3. Analyze trade CSV in Excel/Python for patterns
/// 4. Adjust parameters and re-test
/// 5. Compare multiple scenarios
/// 
/// ENHANCEMENT OPPORTUNITIES:
/// - Risk metrics (VaR, Expected Shortfall)
/// - Regime-specific performance breakdown
/// - Rolling performance windows
/// - Statistical significance testing
/// - Benchmark comparisons (buy-and-hold SPX)
/// 
/// References:
/// - Performance Metrics: https://www.investopedia.com/terms/s/sharperatio.asp
/// - Backtesting Best Practices: "Quantitative Portfolio Management" by Chincarini & Kim
/// </summary>
public static class Reporter
{
    /// <summary>
    /// Generate comprehensive performance summary report.
    /// WHY: Provides at-a-glance assessment of strategy performance across key dimensions.
    /// 
    /// REPORT SECTIONS:
    /// 1. Test Configuration: Period, underlying, mode
    /// 2. Core Performance: Trade count, win rate, avg win/loss
    /// 3. P&L Attribution: Gross, fees, net (reveals cost impact)
    /// 4. Risk Metrics: Sharpe ratio, max drawdown
    /// 5. Strategy Breakdown: Performance by trade type
    /// 
    /// METRIC INTERPRETATION GUIDE:
    /// 
    /// WIN RATE:
    /// - 60-80% typical for premium selling strategies
    /// - Higher isn't always better (may indicate insufficient risk)
    /// 
    /// AVG WIN vs AVG LOSS:
    /// - Premium selling: Many small wins, few large losses
    /// - Key ratio: Avg Win × Win Rate vs |Avg Loss| × Loss Rate
    /// 
    /// SHARPE RATIO:
    /// - > 1.0: Good risk-adjusted returns
    /// - > 2.0: Excellent (rare in options strategies)
    /// - Annualized measure (accounts for volatility)
    /// 
    /// MAX DRAWDOWN:
    /// - Psychological importance: Can you handle worst loss?
    /// - < 20% of account: Generally sustainable
    /// - Consider leverage and position sizing
    /// 
    /// FEES IMPACT:
    /// - Often 10-20% of gross profits in options trading
    /// - Compare gross vs net to assess cost efficiency
    /// - Higher frequency = higher fee drag
    /// </summary>
    public static void WriteSummary(SimConfig cfg, RunReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== ODTE BACKTEST SUMMARY ===");
        sb.AppendLine($"Period: {cfg.Start:yyyy-MM-dd} to {cfg.End:yyyy-MM-dd}");
        sb.AppendLine($"Underlying: {cfg.Underlying}");
        sb.AppendLine($"Mode: {cfg.Mode}");
        sb.AppendLine();
        
        sb.AppendLine("=== CORE PERFORMANCE ===");
        sb.AppendLine($"Total Trades: {report.Trades.Count}");
        sb.AppendLine($"Win Rate: {report.WinRate:P2}");
        sb.AppendLine($"Wins: {report.WinCount} | Losses: {report.LossCount}");
        sb.AppendLine($"Avg Win: ${report.AvgWin:F2}");
        sb.AppendLine($"Avg Loss: ${report.AvgLoss:F2}");
        
        // Calculate profit factor (key metric for trading strategies)
        double profitFactor = report.LossCount > 0 && report.AvgLoss < 0 
            ? (report.AvgWin * report.WinCount) / (Math.Abs(report.AvgLoss) * report.LossCount)
            : double.PositiveInfinity;
        sb.AppendLine($"Profit Factor: {(profitFactor == double.PositiveInfinity ? "∞" : profitFactor.ToString("F2"))}");
        sb.AppendLine();
        
        sb.AppendLine("=== P&L ATTRIBUTION ===");
        sb.AppendLine($"Gross PnL: ${report.GrossPnL:F2}");
        sb.AppendLine($"Total Fees: ${report.Fees:F2}");
        sb.AppendLine($"Net PnL: ${report.NetPnL:F2}");
        double feeRatio = report.GrossPnL > 0 ? (report.Fees / report.GrossPnL) : 0;
        sb.AppendLine($"Fee Drag: {feeRatio:P1} of gross profits");
        sb.AppendLine();
        
        sb.AppendLine("=== RISK METRICS ===");
        sb.AppendLine($"Sharpe Ratio: {report.Sharpe:F2}");
        sb.AppendLine($"Max Drawdown: ${report.MaxDrawdown:F2}");
        
        // Add some context for metric interpretation
        string sharpeAssessment = report.Sharpe switch
        {
            > 2.0 => "Excellent",
            > 1.0 => "Good", 
            > 0.5 => "Fair",
            _ => "Poor"
        };
        sb.AppendLine($"Sharpe Assessment: {sharpeAssessment}");
        sb.AppendLine();
        
        sb.AppendLine("=== STRATEGY BREAKDOWN ===");
        var byType = report.Trades.GroupBy(t => t.Pos.Order.Type);
        foreach (var g in byType)
        {
            var trades = g.ToList();
            var typeWinRate = trades.Count > 0 ? (double)trades.Count(t => t.PnL > 0) / trades.Count : 0;
            sb.AppendLine($"{g.Key}: {trades.Count} trades, " +
                         $"Net PnL: ${trades.Sum(t => t.PnL):F2}, " +
                         $"Win Rate: {typeWinRate:P2}");
        }
        sb.AppendLine();
        
        // Add footer with generation timestamp
        sb.AppendLine($"Report generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("Detailed trade log available in CSV format.");

        var summaryPath = Path.Combine(cfg.Paths.ReportsDir, $"summary_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        File.WriteAllText(summaryPath, sb.ToString());
        Console.WriteLine(sb.ToString());
    }

    /// <summary>
    /// Export detailed trade log to CSV for advanced analysis.
    /// WHY: Enables deep-dive analysis in Excel, Python, R, or other tools.
    /// 
    /// CSV SCHEMA:
    /// - EntryTime: When position was opened
    /// - ExitTime: When position was closed
    /// - Type: Strategy type (Condor, SingleSidePut, SingleSideCall)
    /// - ShortStrike: Strike price of short (sold) option
    /// - LongStrike: Strike price of long (protection) option
    /// - EntryPrice: Actual credit received (after slippage)
    /// - ExitPrice: Actual debit paid to close (after slippage)
    /// - PnL: Net profit/loss including all costs
    /// - Fees: Total transaction costs (commissions + exchange fees)
    /// - ExitReason: Why position closed (stop, delta breach, expiry, etc.)
    /// 
    /// ANALYSIS OPPORTUNITIES:
    /// 
    /// TRADE TIMING:
    /// - Entry time patterns (morning vs afternoon performance)
    /// - Hold time distribution (most profitable hold periods)
    /// - Day-of-week effects
    /// 
    /// STRIKE ANALYSIS:
    /// - Moneyness at entry vs success rate
    /// - Strike clustering and support/resistance levels
    /// - Width optimization (1-point vs 2-point spreads)
    /// 
    /// EXIT ANALYSIS:
    /// - Exit reason distribution (what causes most losses?)
    /// - Time to stop vs time to profit
    /// - Delta breach effectiveness
    /// 
    /// STRATEGY COMPARISON:
    /// - Condor vs single-sided performance
    /// - Bull vs bear market performance
    /// - Volatility regime effectiveness
    /// 
    /// SUGGESTED EXCEL ANALYSIS:
    /// 1. Pivot tables by strategy type and exit reason
    /// 2. Scatter plots: Entry time vs P&L
    /// 3. Histograms: P&L distribution
    /// 4. Time series: Cumulative P&L progression
    /// 5. Correlation analysis: VIX level vs trade performance
    /// </summary>
    public static void WriteTrades(SimConfig cfg, RunReport report)
    {
        var csvPath = Path.Combine(cfg.Paths.ReportsDir, $"trades_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        
        using var writer = new StreamWriter(csvPath);
        
        // Write header with detailed column descriptions
        writer.WriteLine("EntryTime,ExitTime,Type,ShortStrike,LongStrike,Width,EntryPrice,ExitPrice,PnL,Fees,MAE,MFE,ExitReason");
        
        foreach (var trade in report.Trades.OrderBy(t => t.Pos.EntryTs))
        {
            double width = Math.Abs(trade.Pos.Order.Long.Strike - trade.Pos.Order.Short.Strike);
            
            writer.WriteLine($"{trade.Pos.EntryTs:yyyy-MM-dd HH:mm:ss}," +
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
        
        Console.WriteLine($"Trade details written to: {csvPath}");
        Console.WriteLine($"Total records: {report.Trades.Count}");
    }
}