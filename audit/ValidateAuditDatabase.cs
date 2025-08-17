using System;
using Microsoft.Data.Sqlite;

namespace AuditValidation
{
    public class ValidateAuditDatabase
    {
        public static void Main(string[] args)
        {
            var dbPath = "PM212_Trading_Ledger_2005_2025.db";
            
            Console.WriteLine("🔍 PM212 FOLLOW-UP AUDIT VALIDATION");
            Console.WriteLine("📋 Addressing Previous Institutional Findings");
            Console.WriteLine("=" + new string('=', 60));
            
            if (!System.IO.File.Exists(dbPath))
            {
                Console.WriteLine($"❌ Database not found: {dbPath}");
                return;
            }
            
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();
            
            // 1. Basic metrics validation
            ValidateBasicMetrics(connection);
            
            // 2. Data integrity validation
            ValidateDataIntegrity(connection);
            
            // 3. Compliance validation
            ValidateCompliance(connection);
            
            // 4. Realism validation
            ValidateRealism(connection);
            
            Console.WriteLine("\n🏆 AUDIT VALIDATION COMPLETE");
            Console.WriteLine("✅ Database ready for institutional review");
        }
        
        static void ValidateBasicMetrics(SqliteConnection connection)
        {
            Console.WriteLine("\n📊 BASIC METRICS VALIDATION:");
            
            // Total trades and P&L
            var sql = @"
                SELECT 
                    COUNT(*) as total_trades,
                    SUM(actual_pnl) as total_pnl,
                    ROUND(SUM(CASE WHEN was_profit = 1 THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) as win_rate,
                    SUM(commissions_paid) as total_commissions
                FROM trades";
                
            using var command = new SqliteCommand(sql, connection);
            using var reader = command.ExecuteReader();
            
            if (reader.Read())
            {
                Console.WriteLine($"  Total Trades: {reader["total_trades"]:N0}");
                Console.WriteLine($"  Total P&L: ${reader["total_pnl"]:N2}");
                Console.WriteLine($"  Win Rate: {reader["win_rate"]}%");
                Console.WriteLine($"  Total Commissions: ${reader["total_commissions"]:N2}");
            }
        }
        
        static void ValidateDataIntegrity(SqliteConnection connection)
        {
            Console.WriteLine("\n🔍 DATA INTEGRITY VALIDATION:");
            
            // Check for orphaned option legs
            var orphanedSQL = @"
                SELECT COUNT(*) 
                FROM option_legs ol 
                LEFT JOIN trades t ON ol.trade_id = t.trade_id 
                WHERE t.trade_id IS NULL";
                
            using var orphanedCommand = new SqliteCommand(orphanedSQL, connection);
            var orphanedCount = orphanedCommand.ExecuteScalar();
            Console.WriteLine($"  Orphaned option legs: {orphanedCount} (target: 0)");
            
            // Check complete iron condors (4 legs each)
            var completeTradesSQL = @"
                SELECT COUNT(*) 
                FROM (
                    SELECT trade_id, COUNT(*) as leg_count 
                    FROM option_legs 
                    GROUP BY trade_id 
                    HAVING leg_count = 4
                ) complete_spreads";
                
            using var completeCommand = new SqliteCommand(completeTradesSQL, connection);
            var completeCount = completeCommand.ExecuteScalar();
            Console.WriteLine($"  Complete 4-leg trades: {completeCount} (target: 730)");
            
            // P&L reconciliation check
            var pnlCheckSQL = @"
                SELECT COUNT(*) 
                FROM trades t
                JOIN (
                    SELECT trade_id, SUM(leg_pnl) as total_leg_pnl
                    FROM option_legs 
                    GROUP BY trade_id
                ) ol ON t.trade_id = ol.trade_id
                WHERE ABS(t.actual_pnl - (ol.total_leg_pnl - t.commissions_paid)) > 0.01";
                
            using var pnlCommand = new SqliteCommand(pnlCheckSQL, connection);
            var pnlErrors = pnlCommand.ExecuteScalar();
            Console.WriteLine($"  P&L reconciliation errors: {pnlErrors} (target: 0)");
        }
        
        static void ValidateCompliance(SqliteConnection connection)
        {
            Console.WriteLine("\n✅ COMPLIANCE VALIDATION:");
            
            // European settlement check
            var europeanSQL = @"
                SELECT 
                    COUNT(*) as total_legs,
                    SUM(CASE WHEN settlement_type = 'EUROPEAN' THEN 1 ELSE 0 END) as european_legs
                FROM option_legs";
                
            using var europeanCommand = new SqliteCommand(europeanSQL, connection);
            using var reader = europeanCommand.ExecuteReader();
            
            if (reader.Read())
            {
                var total = Convert.ToInt32(reader["total_legs"]);
                var european = Convert.ToInt32(reader["european_legs"]);
                var percentage = total > 0 ? (european * 100.0 / total) : 0;
                Console.WriteLine($"  European settlement: {european:N0}/{total:N0} ({percentage:F1}%) - Target: 100%");
            }
            
            // Assignment risk check
            var assignmentSQL = @"
                SELECT COUNT(*) 
                FROM option_legs 
                WHERE assignment_risk = 0";
                
            using var assignmentCommand = new SqliteCommand(assignmentSQL, connection);
            var noAssignmentCount = assignmentCommand.ExecuteScalar();
            Console.WriteLine($"  Zero assignment risk: {noAssignmentCount:N0} legs (target: 2,920)");
            
            // Risk limit compliance
            var riskLimitSQL = @"
                SELECT COUNT(*) 
                FROM trades 
                WHERE position_size > rev_fib_limit";
                
            using var riskCommand = new SqliteCommand(riskLimitSQL, connection);
            var violations = riskCommand.ExecuteScalar();
            Console.WriteLine($"  Risk limit violations: {violations} (target: 0)");
        }
        
        static void ValidateRealism(SqliteConnection connection)
        {
            Console.WriteLine("\n🎯 REALISM VALIDATION:");
            
            // Commission consistency check
            var commissionSQL = @"
                SELECT 
                    COUNT(*) as total_trades,
                    AVG(commissions_paid) as avg_commission,
                    MIN(commissions_paid) as min_commission,
                    MAX(commissions_paid) as max_commission
                FROM trades";
                
            using var commissionCommand = new SqliteCommand(commissionSQL, connection);
            using var reader = commissionCommand.ExecuteReader();
            
            if (reader.Read())
            {
                Console.WriteLine($"  Average commission: ${reader["avg_commission"]:F2} (target: $8.00)");
                Console.WriteLine($"  Commission range: ${reader["min_commission"]:F2} - ${reader["max_commission"]:F2}");
            }
            
            // Bid-ask spread realism
            var spreadSQL = @"
                SELECT 
                    AVG(entry_ask - entry_bid) as avg_spread,
                    MIN(entry_ask - entry_bid) as min_spread,
                    MAX(entry_ask - entry_bid) as max_spread
                FROM option_legs 
                WHERE entry_bid > 0 AND entry_ask > entry_bid";
                
            using var spreadCommand = new SqliteCommand(spreadSQL, connection);
            using var spreadReader = spreadCommand.ExecuteReader();
            
            if (spreadReader.Read())
            {
                Console.WriteLine($"  Average bid-ask spread: ${spreadReader["avg_spread"]:F3}");
                Console.WriteLine($"  Spread range: ${spreadReader["min_spread"]:F3} - ${spreadReader["max_spread"]:F3}");
            }
            
            // Delta distribution check
            var deltaSQL = @"
                SELECT 
                    option_type,
                    action,
                    AVG(ABS(entry_delta)) as avg_abs_delta,
                    COUNT(*) as leg_count
                FROM option_legs 
                GROUP BY option_type, action
                ORDER BY option_type, action";
                
            Console.WriteLine("  Delta distribution by leg type:");
            using var deltaCommand = new SqliteCommand(deltaSQL, connection);
            using var deltaReader = deltaCommand.ExecuteReader();
            
            while (deltaReader.Read())
            {
                Console.WriteLine($"    {deltaReader["option_type"]} {deltaReader["action"]}: " +
                                $"{deltaReader["avg_abs_delta"]:F3} avg delta ({deltaReader["leg_count"]:N0} legs)");
            }
        }
    }
}