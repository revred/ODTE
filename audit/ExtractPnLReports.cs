using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Data.Sqlite;

namespace AuditPnLExtraction
{
    public class ExtractPnLReports
    {
        public static void Main(string[] args)
        {
            var dbPath = "PM212_Trading_Ledger_2005_2025.db";
            
            Console.WriteLine("📊 PM212 P&L EXTRACTION FOR INSTITUTIONAL AUDIT");
            Console.WriteLine("🎯 Generating Daily, Monthly, and Yearly P&L Reports");
            Console.WriteLine("=" + new string('=', 65));
            
            if (!File.Exists(dbPath))
            {
                Console.WriteLine($"❌ Database not found: {dbPath}");
                return;
            }
            
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();
            
            // Extract and generate P&L reports
            ExtractDailyPnL(connection);
            ExtractMonthlyPnL(connection);
            ExtractYearlyPnL(connection);
            ExtractCumulativePnL(connection);
            
            Console.WriteLine("\n🏆 P&L EXTRACTION COMPLETE");
            Console.WriteLine("✅ All CSV files generated for institutional audit package");
        }
        
        static void ExtractDailyPnL(SqliteConnection connection)
        {
            Console.WriteLine("\n📅 Extracting Daily P&L...");
            
            var sql = @"
                SELECT 
                    DATE(entry_date) as trade_date,
                    COUNT(*) as trades_count,
                    SUM(actual_pnl) as daily_pnl,
                    SUM(commissions_paid) as daily_commissions,
                    SUM(actual_pnl + commissions_paid) as gross_pnl,
                    SUM(CASE WHEN was_profit = 1 THEN 1 ELSE 0 END) as winning_trades,
                    ROUND(SUM(CASE WHEN was_profit = 1 THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) as win_rate,
                    MIN(actual_pnl) as worst_trade,
                    MAX(actual_pnl) as best_trade,
                    AVG(actual_pnl) as avg_trade_pnl,
                    market_regime,
                    ROUND(AVG(vix_entry), 1) as avg_vix,
                    ROUND(AVG(underlying_entry_price), 2) as avg_spx_price
                FROM trades 
                GROUP BY DATE(entry_date), market_regime
                ORDER BY trade_date";
            
            var csvContent = new StringBuilder();
            csvContent.AppendLine("Date,Trades_Count,Daily_PnL,Daily_Commissions,Gross_PnL,Winning_Trades,Win_Rate_Pct,Worst_Trade,Best_Trade,Avg_Trade_PnL,Market_Regime,Avg_VIX,Avg_SPX_Price");
            
            using var command = new SqliteCommand(sql, connection);
            using var reader = command.ExecuteReader();
            
            int totalDays = 0;
            decimal totalPnL = 0;
            
            while (reader.Read())
            {
                csvContent.AppendLine($"{reader["trade_date"]}," +
                                    $"{reader["trades_count"]}," +
                                    $"{reader["daily_pnl"]:F2}," +
                                    $"{reader["daily_commissions"]:F2}," +
                                    $"{reader["gross_pnl"]:F2}," +
                                    $"{reader["winning_trades"]}," +
                                    $"{reader["win_rate"]}," +
                                    $"{reader["worst_trade"]:F2}," +
                                    $"{reader["best_trade"]:F2}," +
                                    $"{reader["avg_trade_pnl"]:F2}," +
                                    $"\"{reader["market_regime"]}\"," +
                                    $"{reader["avg_vix"]}," +
                                    $"{reader["avg_spx_price"]}");
                
                totalDays++;
                totalPnL += Convert.ToDecimal(reader["daily_pnl"]);
            }
            
            File.WriteAllText("PM212_Daily_PnL_Report.csv", csvContent.ToString());
            Console.WriteLine($"✅ Generated PM212_Daily_PnL_Report.csv ({totalDays} trading days, ${totalPnL:N2} total P&L)");
        }
        
        static void ExtractMonthlyPnL(SqliteConnection connection)
        {
            Console.WriteLine("\n📅 Extracting Monthly P&L...");
            
            var sql = @"
                SELECT 
                    month,
                    COUNT(*) as trades_count,
                    SUM(actual_pnl) as monthly_pnl,
                    SUM(commissions_paid) as monthly_commissions,
                    SUM(actual_pnl + commissions_paid) as gross_pnl,
                    SUM(CASE WHEN was_profit = 1 THEN 1 ELSE 0 END) as winning_trades,
                    ROUND(SUM(CASE WHEN was_profit = 1 THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) as win_rate,
                    MIN(actual_pnl) as worst_trade,
                    MAX(actual_pnl) as best_trade,
                    AVG(actual_pnl) as avg_trade_pnl,
                    GROUP_CONCAT(DISTINCT market_regime) as market_regimes,
                    ROUND(AVG(vix_entry), 1) as avg_vix,
                    ROUND(MIN(underlying_entry_price), 2) as month_low_spx,
                    ROUND(MAX(underlying_entry_price), 2) as month_high_spx,
                    COUNT(DISTINCT DATE(entry_date)) as trading_days
                FROM trades 
                GROUP BY month
                ORDER BY month";
            
            var csvContent = new StringBuilder();
            csvContent.AppendLine("Month,Trades_Count,Monthly_PnL,Monthly_Commissions,Gross_PnL,Winning_Trades,Win_Rate_Pct,Worst_Trade,Best_Trade,Avg_Trade_PnL,Market_Regimes,Avg_VIX,Month_Low_SPX,Month_High_SPX,Trading_Days");
            
            using var command = new SqliteCommand(sql, connection);
            using var reader = command.ExecuteReader();
            
            int totalMonths = 0;
            decimal totalPnL = 0;
            decimal runningBalance = 0;
            
            while (reader.Read())
            {
                var monthlyPnL = Convert.ToDecimal(reader["monthly_pnl"]);
                runningBalance += monthlyPnL;
                
                csvContent.AppendLine($"{reader["month"]}," +
                                    $"{reader["trades_count"]}," +
                                    $"{reader["monthly_pnl"]:F2}," +
                                    $"{reader["monthly_commissions"]:F2}," +
                                    $"{reader["gross_pnl"]:F2}," +
                                    $"{reader["winning_trades"]}," +
                                    $"{reader["win_rate"]}," +
                                    $"{reader["worst_trade"]:F2}," +
                                    $"{reader["best_trade"]:F2}," +
                                    $"{reader["avg_trade_pnl"]:F2}," +
                                    $"\"{reader["market_regimes"]}\"," +
                                    $"{reader["avg_vix"]}," +
                                    $"{reader["month_low_spx"]}," +
                                    $"{reader["month_high_spx"]}," +
                                    $"{reader["trading_days"]}");
                
                totalMonths++;
                totalPnL += monthlyPnL;
            }
            
            File.WriteAllText("PM212_Monthly_PnL_Report.csv", csvContent.ToString());
            Console.WriteLine($"✅ Generated PM212_Monthly_PnL_Report.csv ({totalMonths} months, ${totalPnL:N2} total P&L)");
        }
        
        static void ExtractYearlyPnL(SqliteConnection connection)
        {
            Console.WriteLine("\n📅 Extracting Yearly P&L...");
            
            var sql = @"
                SELECT 
                    SUBSTR(month, 1, 4) as year,
                    COUNT(*) as trades_count,
                    SUM(actual_pnl) as yearly_pnl,
                    SUM(commissions_paid) as yearly_commissions,
                    SUM(actual_pnl + commissions_paid) as gross_pnl,
                    SUM(CASE WHEN was_profit = 1 THEN 1 ELSE 0 END) as winning_trades,
                    ROUND(SUM(CASE WHEN was_profit = 1 THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) as win_rate,
                    MIN(actual_pnl) as worst_trade,
                    MAX(actual_pnl) as best_trade,
                    AVG(actual_pnl) as avg_trade_pnl,
                    GROUP_CONCAT(DISTINCT market_regime) as market_regimes,
                    ROUND(AVG(vix_entry), 1) as avg_vix,
                    ROUND(MIN(underlying_entry_price), 2) as year_low_spx,
                    ROUND(MAX(underlying_entry_price), 2) as year_high_spx,
                    COUNT(DISTINCT month) as months_traded,
                    COUNT(DISTINCT DATE(entry_date)) as trading_days,
                    ROUND(AVG(rev_fib_level), 1) as avg_risk_level
                FROM trades 
                GROUP BY SUBSTR(month, 1, 4)
                ORDER BY year";
            
            var csvContent = new StringBuilder();
            csvContent.AppendLine("Year,Trades_Count,Yearly_PnL,Yearly_Commissions,Gross_PnL,Winning_Trades,Win_Rate_Pct,Worst_Trade,Best_Trade,Avg_Trade_PnL,Market_Regimes,Avg_VIX,Year_Low_SPX,Year_High_SPX,Months_Traded,Trading_Days,Avg_Risk_Level");
            
            using var command = new SqliteCommand(sql, connection);
            using var reader = command.ExecuteReader();
            
            int totalYears = 0;
            decimal totalPnL = 0;
            
            while (reader.Read())
            {
                var yearlyPnL = Convert.ToDecimal(reader["yearly_pnl"]);
                
                csvContent.AppendLine($"{reader["year"]}," +
                                    $"{reader["trades_count"]}," +
                                    $"{reader["yearly_pnl"]:F2}," +
                                    $"{reader["yearly_commissions"]:F2}," +
                                    $"{reader["gross_pnl"]:F2}," +
                                    $"{reader["winning_trades"]}," +
                                    $"{reader["win_rate"]}," +
                                    $"{reader["worst_trade"]:F2}," +
                                    $"{reader["best_trade"]:F2}," +
                                    $"{reader["avg_trade_pnl"]:F2}," +
                                    $"\"{reader["market_regimes"]}\"," +
                                    $"{reader["avg_vix"]}," +
                                    $"{reader["year_low_spx"]}," +
                                    $"{reader["year_high_spx"]}," +
                                    $"{reader["months_traded"]}," +
                                    $"{reader["trading_days"]}," +
                                    $"{reader["avg_risk_level"]}");
                
                totalYears++;
                totalPnL += yearlyPnL;
            }
            
            File.WriteAllText("PM212_Yearly_PnL_Report.csv", csvContent.ToString());
            Console.WriteLine($"✅ Generated PM212_Yearly_PnL_Report.csv ({totalYears} years, ${totalPnL:N2} total P&L)");
        }
        
        static void ExtractCumulativePnL(SqliteConnection connection)
        {
            Console.WriteLine("\n📈 Extracting Cumulative P&L Performance...");
            
            var sql = @"
                SELECT 
                    trade_id,
                    month,
                    DATE(entry_date) as trade_date,
                    strategy,
                    actual_pnl,
                    commissions_paid,
                    market_regime,
                    vix_entry,
                    underlying_entry_price,
                    was_profit,
                    rev_fib_level,
                    rev_fib_limit,
                    position_size,
                    exit_reason
                FROM trades 
                ORDER BY entry_date, trade_id";
            
            var csvContent = new StringBuilder();
            csvContent.AppendLine("Trade_ID,Month,Trade_Date,Strategy,Trade_PnL,Commissions,Cumulative_PnL,Market_Regime,VIX,SPX_Price,Was_Profit,Risk_Level,Risk_Limit,Position_Size,Exit_Reason");
            
            using var command = new SqliteCommand(sql, connection);
            using var reader = command.ExecuteReader();
            
            decimal cumulativePnL = 0;
            int tradeCount = 0;
            
            while (reader.Read())
            {
                var tradePnL = Convert.ToDecimal(reader["actual_pnl"]);
                cumulativePnL += tradePnL;
                tradeCount++;
                
                csvContent.AppendLine($"{reader["trade_id"]}," +
                                    $"{reader["month"]}," +
                                    $"{reader["trade_date"]}," +
                                    $"\"{reader["strategy"]}\"," +
                                    $"{reader["actual_pnl"]:F2}," +
                                    $"{reader["commissions_paid"]:F2}," +
                                    $"{cumulativePnL:F2}," +
                                    $"\"{reader["market_regime"]}\"," +
                                    $"{reader["vix_entry"]:F1}," +
                                    $"{reader["underlying_entry_price"]:F2}," +
                                    $"{(Convert.ToInt32(reader["was_profit"]) == 1 ? "TRUE" : "FALSE")}," +
                                    $"{reader["rev_fib_level"]}," +
                                    $"{reader["rev_fib_limit"]:F0}," +
                                    $"{reader["position_size"]:F2}," +
                                    $"\"{reader["exit_reason"]}\"");
            }
            
            File.WriteAllText("PM212_Cumulative_PnL_Report.csv", csvContent.ToString());
            Console.WriteLine($"✅ Generated PM212_Cumulative_PnL_Report.csv ({tradeCount} trades, ${cumulativePnL:N2} final P&L)");
        }
    }
}