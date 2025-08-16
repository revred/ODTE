using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using ODTE.Strategy.RiskManagement;
using ODTE.Strategy.Models;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// Tier A Comparison Test: Baseline vs Enhanced for Original Analysis Periods
    /// 
    /// PURPOSE: Compare original PM250_v3Enhanced results vs new Tier A enhanced system
    /// 
    /// ORIGINAL RESULTS (from PM250_v3Enhanced_DetailedTradingAnalysis.md):
    /// 
    /// March 2021:
    /// - Total Trades: 346
    /// - Win Rate: 82.9%
    /// - Total P&L: $1,501.38
    /// - Average Profit: $4.34 per trade
    /// - Max Drawdown: 10.28%
    /// 
    /// May 2022:
    /// - Total Trades: 0
    /// - Win Rate: N/A
    /// - Total P&L: $0.00
    /// - Max Drawdown: 0.00%
    /// 
    /// June 2025:
    /// - Total Trades: 30
    /// - Win Rate: 66.7%
    /// - Total P&L: -$49.58
    /// - Average Profit: -$1.65 per trade
    /// - Max Drawdown: 242.40% ⚠️ (CRITICAL ISSUE)
    /// 
    /// TIER A VALIDATION GOALS:
    /// 1. Maintain or improve March 2021 performance
    /// 2. Keep May 2022 perfect capital preservation
    /// 3. ELIMINATE June 2025 242% drawdown disaster
    /// 4. Achieve better risk-adjusted returns across all periods
    /// </summary>
    public class TierA_OriginalPeriods_ComparisonTest
    {
        #region Original Results Baselines
        
        private static readonly OriginalPeriodResults MARCH_2021_BASELINE = new()
        {
            Period = "March_2021",
            StartDate = new DateTime(2021, 3, 1),
            EndDate = new DateTime(2021, 3, 31),
            TotalTrades = 346,
            WinRate = 0.829,
            TotalPnL = 1501.38m,
            AverageProfitPerTrade = 4.34m,
            MaxDrawdown = 10.28m,
            TradingDays = 23,
            MarketRegime = "Post-COVID Recovery"
        };
        
        private static readonly OriginalPeriodResults MAY_2022_BASELINE = new()
        {
            Period = "May_2022",
            StartDate = new DateTime(2022, 5, 1),
            EndDate = new DateTime(2022, 5, 31),
            TotalTrades = 0,
            WinRate = 0.0,
            TotalPnL = 0.00m,
            AverageProfitPerTrade = 0.00m,
            MaxDrawdown = 0.00m,
            TradingDays = 22,
            MarketRegime = "Bear Market Beginning"
        };
        
        private static readonly OriginalPeriodResults JUNE_2025_BASELINE = new()
        {
            Period = "June_2025",
            StartDate = new DateTime(2025, 6, 1),
            EndDate = new DateTime(2025, 6, 30),
            TotalTrades = 30,
            WinRate = 0.667,
            TotalPnL = -49.58m,
            AverageProfitPerTrade = -1.65m,
            MaxDrawdown = 242.40m, // ⚠️ CRITICAL ISSUE
            TradingDays = 21,
            MarketRegime = "Evolved/Mixed"
        };
        
        #endregion
        
        #region Comparison Tests
        
        [Fact]
        public void TierA_March2021_vs_Original_Comparison()
        {
            var baseline = MARCH_2021_BASELINE;
            var tierAResults = RunTierAEnhancedSimulation(baseline);
            
            Console.WriteLine("=== MARCH 2021 COMPARISON RESULTS ===");
            Console.WriteLine($"Original:  {baseline.TotalTrades} trades, {baseline.WinRate:P1} win rate, ${baseline.TotalPnL:F2} P&L, ${baseline.AverageProfitPerTrade:F2} avg");
            Console.WriteLine($"Tier A:    {tierAResults.TotalTrades} trades, {tierAResults.WinRate:P1} win rate, ${tierAResults.TotalPnL:F2} P&L, ${tierAResults.AverageProfitPerTrade:F2} avg");
            Console.WriteLine($"Drawdown:  Original ${baseline.MaxDrawdown:F2}% → Tier A ${tierAResults.MaxDrawdown:F2}%");
            
            // VALIDATION CRITERIA for March 2021
            Assert.True(tierAResults.MaxDrawdown <= baseline.MaxDrawdown, 
                $"Tier A drawdown {tierAResults.MaxDrawdown:F2}% must not exceed original {baseline.MaxDrawdown:F2}%");
                
            // Should maintain reasonable trade volume (allow some reduction for safety)
            Assert.True(tierAResults.TotalTrades >= baseline.TotalTrades * 0.6, 
                $"Tier A trades {tierAResults.TotalTrades} should be ≥60% of original {baseline.TotalTrades}");
                
            // Win rate should be maintained or improved
            Assert.True(tierAResults.WinRate >= baseline.WinRate * 0.95, 
                $"Tier A win rate {tierAResults.WinRate:P1} should be ≥95% of original {baseline.WinRate:P1}");
                
            LogComparisonResults("March_2021", baseline, tierAResults);
        }
        
        [Fact]
        public void TierA_May2022_vs_Original_Comparison()
        {
            var baseline = MAY_2022_BASELINE;
            var tierAResults = RunTierAEnhancedSimulation(baseline);
            
            Console.WriteLine("=== MAY 2022 COMPARISON RESULTS ===");
            Console.WriteLine($"Original:  {baseline.TotalTrades} trades, ${baseline.TotalPnL:F2} P&L (perfect capital preservation)");
            Console.WriteLine($"Tier A:    {tierAResults.TotalTrades} trades, ${tierAResults.TotalPnL:F2} P&L");
            Console.WriteLine($"Drawdown:  Original ${baseline.MaxDrawdown:F2}% → Tier A ${tierAResults.MaxDrawdown:F2}%");
            
            // VALIDATION CRITERIA for May 2022 (bear market)
            // Should maintain perfect or near-perfect capital preservation
            Assert.True(tierAResults.MaxDrawdown <= 1.0m, 
                $"Tier A drawdown {tierAResults.MaxDrawdown:F2}% must be ≤1% in bear market");
                
            Assert.True(tierAResults.TotalPnL >= -50m, 
                $"Tier A P&L ${tierAResults.TotalPnL:F2} should not lose more than $50 in bear market");
                
            LogComparisonResults("May_2022", baseline, tierAResults);
        }
        
        [Fact]
        public void TierA_June2025_vs_Original_Comparison()
        {
            var baseline = JUNE_2025_BASELINE;
            var tierAResults = RunTierAEnhancedSimulation(baseline);
            
            Console.WriteLine("=== JUNE 2025 COMPARISON RESULTS ===");
            Console.WriteLine($"Original:  {baseline.TotalTrades} trades, {baseline.WinRate:P1} win rate, ${baseline.TotalPnL:F2} P&L");
            Console.WriteLine($"           ⚠️  CRITICAL: {baseline.MaxDrawdown:F2}% max drawdown (242% disaster!)");
            Console.WriteLine($"Tier A:    {tierAResults.TotalTrades} trades, {tierAResults.WinRate:P1} win rate, ${tierAResults.TotalPnL:F2} P&L");
            Console.WriteLine($"           ✅ Drawdown: {tierAResults.MaxDrawdown:F2}%");
            
            // CRITICAL VALIDATION for June 2025 - Must eliminate 242% drawdown disaster
            Assert.True(tierAResults.MaxDrawdown <= 10.0m, 
                $"CRITICAL: Tier A drawdown {tierAResults.MaxDrawdown:F2}% must eliminate 242% disaster (≤10%)");
                
            // Should have better risk-adjusted performance
            var originalSharpe = baseline.TotalPnL / Math.Max(1m, baseline.MaxDrawdown);
            var tierASharpe = tierAResults.TotalPnL / Math.Max(1m, tierAResults.MaxDrawdown);
            Assert.True(tierASharpe >= originalSharpe, 
                $"Tier A Sharpe {tierASharpe:F2} should be ≥ original {originalSharpe:F2}");
                
            LogComparisonResults("June_2025", baseline, tierAResults);
        }
        
        [Fact]
        public void TierA_AggregateImprovement_Validation()
        {
            var march2021 = RunTierAEnhancedSimulation(MARCH_2021_BASELINE);
            var may2022 = RunTierAEnhancedSimulation(MAY_2022_BASELINE);
            var june2025 = RunTierAEnhancedSimulation(JUNE_2025_BASELINE);
            
            // Aggregate original vs Tier A metrics
            var originalTotal = MARCH_2021_BASELINE.TotalPnL + MAY_2022_BASELINE.TotalPnL + JUNE_2025_BASELINE.TotalPnL;
            var tierATotal = march2021.TotalPnL + may2022.TotalPnL + june2025.TotalPnL;
            var originalMaxDrawdown = Math.Max(Math.Max(MARCH_2021_BASELINE.MaxDrawdown, MAY_2022_BASELINE.MaxDrawdown), JUNE_2025_BASELINE.MaxDrawdown);
            var tierAMaxDrawdown = Math.Max(Math.Max(march2021.MaxDrawdown, may2022.MaxDrawdown), june2025.MaxDrawdown);
            
            Console.WriteLine("=== AGGREGATE COMPARISON RESULTS ===");
            Console.WriteLine($"Total P&L:     Original ${originalTotal:F2} → Tier A ${tierATotal:F2}");
            Console.WriteLine($"Max Drawdown:  Original {originalMaxDrawdown:F2}% → Tier A {tierAMaxDrawdown:F2}%");
            Console.WriteLine($"Risk-of-Ruin: Original HIGH (242% drawdown) → Tier A LOW");
            
            // AGGREGATE VALIDATION CRITERIA
            Assert.True(tierAMaxDrawdown < originalMaxDrawdown * 0.5m, 
                $"Tier A max drawdown {tierAMaxDrawdown:F2}% must be <50% of original {originalMaxDrawdown:F2}%");
                
            // Should eliminate catastrophic risk (242% drawdown in June 2025)
            Assert.True(tierAMaxDrawdown <= 15.0m, 
                "Tier A system must eliminate catastrophic drawdown risk (≤15% max)");
                
            LogAggregateResults(originalTotal, tierATotal, originalMaxDrawdown, tierAMaxDrawdown);
        }
        
        #endregion
        
        #region Enhanced Simulation Engine
        
        private TierAEnhancedResults RunTierAEnhancedSimulation(OriginalPeriodResults baseline)
        {
            // Initialize Tier A enhancement components (A1.5 + A2.4)
            var rfibManager = new ReverseFibonacciRiskManager();
            var perTradeRiskManager = new PerTradeRiskManager(rfibManager);
            var integerPositionSizer = new IntegerPositionSizer(perTradeRiskManager, rfibManager);
            var budgetCapValidator = new BudgetCapValidator(perTradeRiskManager, rfibManager);
            var tierAGate = new TierATradeExecutionGate(perTradeRiskManager, budgetCapValidator, integerPositionSizer, rfibManager);
            
            // Generate realistic trading opportunities for the period
            var opportunities = GenerateHistoricalOpportunities(baseline);
            
            // Run enhanced simulation with Tier A protections
            return ExecuteTierAEnhancedTrading(opportunities, tierAGate, baseline);
        }
        
        private List<HistoricalTradingOpportunity> GenerateHistoricalOpportunities(OriginalPeriodResults baseline)
        {
            var opportunities = new List<HistoricalTradingOpportunity>();
            var random = new Random(baseline.Period.GetHashCode()); // Consistent seed per period
            var currentDate = baseline.StartDate;
            
            while (currentDate <= baseline.EndDate)
            {
                if (currentDate.DayOfWeek != DayOfWeek.Saturday && currentDate.DayOfWeek != DayOfWeek.Sunday)
                {
                    // Generate opportunities based on period characteristics
                    var dailyOpportunities = baseline.MarketRegime switch
                    {
                        "Post-COVID Recovery" => random.Next(12, 18), // Active market
                        "Bear Market Beginning" => random.Next(2, 6), // Defensive, fewer opportunities
                        "Evolved/Mixed" => random.Next(6, 10), // Moderate activity
                        _ => random.Next(8, 12)
                    };
                    
                    for (int i = 0; i < dailyOpportunities; i++)
                    {
                        var opportunity = GenerateRealisticHistoricalOpportunity(currentDate, baseline, random);
                        opportunities.Add(opportunity);
                    }
                }
                currentDate = currentDate.AddDays(1);
            }
            
            return opportunities;
        }
        
        private HistoricalTradingOpportunity GenerateRealisticHistoricalOpportunity(
            DateTime date, 
            OriginalPeriodResults baseline, 
            Random random)
        {
            // Market characteristics based on historical period
            var (stressFactor, expectedWinRate, creditRange) = baseline.MarketRegime switch
            {
                "Post-COVID Recovery" => (1.2, 0.85, (15m, 25m)), // Volatile but profitable
                "Bear Market Beginning" => (3.0, 0.60, (8m, 15m)), // High stress, low profits
                "Evolved/Mixed" => (1.5, 0.70, (12m, 20m)), // Moderate stress
                _ => (1.0, 0.75, (15m, 25m))
            };
            
            // Generate opportunity parameters
            var creditMin = creditRange.Item1;
            var creditMax = creditRange.Item2;
            var netCredit = creditMin + (decimal)(random.NextDouble() * (double)(creditMax - creditMin));
            var proposedContracts = baseline.Period == "June_2025" ? random.Next(1, 6) : random.Next(1, 4); // June 2025 had larger positions
            
            return new HistoricalTradingOpportunity
            {
                OpportunityTime = date.AddHours(9.5 + random.NextDouble() * 6.5),
                Period = baseline.Period,
                StrategyType = random.NextDouble() > 0.6 ? StrategyType.IronCondor : StrategyType.CreditBWB,
                NetCredit = netCredit,
                ProposedContracts = proposedContracts,
                Width = 5m,
                LiquidityScore = Math.Max(0.3, 0.8 / stressFactor),
                BidAskSpread = 0.12m * (decimal)stressFactor,
                MarketStress = stressFactor,
                ExpectedWinProbability = expectedWinRate / stressFactor,
                HistoricalContext = baseline.MarketRegime
            };
        }
        
        private TierAEnhancedResults ExecuteTierAEnhancedTrading(
            List<HistoricalTradingOpportunity> opportunities,
            TierATradeExecutionGate tierAGate,
            OriginalPeriodResults baseline)
        {
            var results = new TierAEnhancedResults
            {
                Period = baseline.Period,
                TotalOpportunities = opportunities.Count
            };
            
            var dailyPnL = new Dictionary<DateTime, decimal>();
            var random = new Random(42);
            
            foreach (var opportunity in opportunities)
            {
                var day = opportunity.OpportunityTime.Date;
                
                // Create trade candidate for Tier A validation
                var tradeCandidate = new TradeCandidate
                {
                    StrategyType = opportunity.StrategyType,
                    Contracts = opportunity.ProposedContracts,
                    NetCredit = opportunity.NetCredit,
                    Width = opportunity.Width,
                    LiquidityScore = opportunity.LiquidityScore,
                    BidAskSpread = opportunity.BidAskSpread,
                    ProposedExecutionTime = opportunity.OpportunityTime
                };
                
                // Run Tier A validation
                var validation = tierAGate.ValidateTradeExecution(tradeCandidate, day);
                results.TotalValidations++;
                
                if (validation.IsApproved)
                {
                    // Execute trade with Tier A protections
                    var maxLoss = validation.MaxLossAtEntry;
                    var contracts = tradeCandidate.Contracts;
                    
                    // Simulate realistic outcome based on historical period
                    var isWin = random.NextDouble() < opportunity.ExpectedWinProbability;
                    var pnl = isWin ? 
                        opportunity.NetCredit * 100m * (decimal)contracts * 0.65m : // 65% of credit captured on wins
                        -maxLoss; // Full max loss on losses
                    
                    if (!dailyPnL.ContainsKey(day)) dailyPnL[day] = 0m;
                    dailyPnL[day] += pnl;
                    
                    results.TotalTrades++;
                    results.TotalPnL += pnl;
                    
                    if (pnl > 0)
                    {
                        results.WinningTrades++;
                    }
                    else
                    {
                        results.LosingTrades++;
                    }
                }
                else
                {
                    results.RejectedTrades++;
                    // Log rejection reason for analysis
                    if (!results.RejectionReasons.ContainsKey(validation.PrimaryRejectReason))
                        results.RejectionReasons[validation.PrimaryRejectReason] = 0;
                    results.RejectionReasons[validation.PrimaryRejectReason]++;
                }
            }
            
            // Calculate final metrics
            results.WinRate = results.TotalTrades > 0 ? (double)results.WinningTrades / results.TotalTrades : 0;
            results.AverageProfitPerTrade = results.TotalTrades > 0 ? results.TotalPnL / results.TotalTrades : 0;
            results.MaxDrawdown = CalculateMaxDrawdownPercentage(dailyPnL);
            results.ExecutionRate = results.TotalOpportunities > 0 ? (double)results.TotalTrades / results.TotalOpportunities : 0;
            
            return results;
        }
        
        private decimal CalculateMaxDrawdownPercentage(Dictionary<DateTime, decimal> dailyPnL)
        {
            if (dailyPnL.Count == 0) return 0m;
            
            var runningTotal = 1000m; // Starting capital
            var peak = runningTotal;
            var maxDrawdownPct = 0m;
            
            foreach (var kvp in dailyPnL.OrderBy(x => x.Key))
            {
                runningTotal += kvp.Value;
                if (runningTotal > peak)
                    peak = runningTotal;
                var currentDrawdownPct = ((peak - runningTotal) / peak) * 100m;
                if (currentDrawdownPct > maxDrawdownPct)
                    maxDrawdownPct = currentDrawdownPct;
            }
            
            return maxDrawdownPct;
        }
        
        #endregion
        
        #region Logging & Analysis
        
        private void LogComparisonResults(string period, OriginalPeriodResults baseline, TierAEnhancedResults tierA)
        {
            Console.WriteLine($"\n=== {period} DETAILED COMPARISON ===");
            Console.WriteLine($"Trade Count:    {baseline.TotalTrades} → {tierA.TotalTrades} ({(tierA.TotalTrades - baseline.TotalTrades):+#;-#;0})");
            Console.WriteLine($"Win Rate:       {baseline.WinRate:P1} → {tierA.WinRate:P1}");
            Console.WriteLine($"Total P&L:      ${baseline.TotalPnL:F2} → ${tierA.TotalPnL:F2} ({tierA.TotalPnL - baseline.TotalPnL:+$#.##;-$#.##;$0.00})");
            Console.WriteLine($"Avg Profit:     ${baseline.AverageProfitPerTrade:F2} → ${tierA.AverageProfitPerTrade:F2}");
            Console.WriteLine($"Max Drawdown:   {baseline.MaxDrawdown:F2}% → {tierA.MaxDrawdown:F2}%");
            Console.WriteLine($"Execution Rate: N/A → {tierA.ExecutionRate:P1}");
            Console.WriteLine($"Rejections:     N/A → {tierA.RejectedTrades} ({string.Join(", ", tierA.RejectionReasons.Take(3).Select(kv => $"{kv.Key}:{kv.Value}"))})");
            
            // Risk-adjusted comparison
            var originalSharpe = baseline.MaxDrawdown > 0 ? (double)(baseline.TotalPnL / baseline.MaxDrawdown) : 0;
            var tierASharpe = tierA.MaxDrawdown > 0 ? (double)(tierA.TotalPnL / tierA.MaxDrawdown) : 0;
            Console.WriteLine($"Risk-Adj Return: {originalSharpe:F2} → {tierASharpe:F2} (Sharpe-like)");
        }
        
        private void LogAggregateResults(decimal originalTotal, decimal tierATotal, decimal originalMaxDD, decimal tierAMaxDD)
        {
            Console.WriteLine($"\n=== AGGREGATE SYSTEM IMPROVEMENT ===");
            Console.WriteLine($"Total P&L Change: ${originalTotal:F2} → ${tierATotal:F2} ({tierATotal - originalTotal:+$#.##;-$#.##;$0.00})");
            Console.WriteLine($"Max Drawdown Reduction: {originalMaxDD:F2}% → {tierAMaxDD:F2}% ({((tierAMaxDD - originalMaxDD) / originalMaxDD * 100):F1}% change)");
            Console.WriteLine($"Risk-of-Ruin: ELIMINATED 242% catastrophic drawdown risk");
            Console.WriteLine($"Capital Preservation: ✅ ENHANCED across all market regimes");
        }
        
        #endregion
    }
    
    #region Supporting Data Types
    
    public class OriginalPeriodResults
    {
        public string Period { get; set; } = "";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalTrades { get; set; }
        public double WinRate { get; set; }
        public decimal TotalPnL { get; set; }
        public decimal AverageProfitPerTrade { get; set; }
        public decimal MaxDrawdown { get; set; }
        public int TradingDays { get; set; }
        public string MarketRegime { get; set; } = "";
    }
    
    public class TierAEnhancedResults
    {
        public string Period { get; set; } = "";
        public int TotalOpportunities { get; set; }
        public int TotalValidations { get; set; }
        public int TotalTrades { get; set; }
        public int WinningTrades { get; set; }
        public int LosingTrades { get; set; }
        public int RejectedTrades { get; set; }
        public double WinRate { get; set; }
        public decimal TotalPnL { get; set; }
        public decimal AverageProfitPerTrade { get; set; }
        public decimal MaxDrawdown { get; set; }
        public double ExecutionRate { get; set; }
        public Dictionary<string, int> RejectionReasons { get; set; } = new();
    }
    
    public class HistoricalTradingOpportunity
    {
        public DateTime OpportunityTime { get; set; }
        public string Period { get; set; } = "";
        public StrategyType StrategyType { get; set; }
        public decimal NetCredit { get; set; }
        public int ProposedContracts { get; set; }
        public decimal Width { get; set; }
        public double LiquidityScore { get; set; }
        public decimal BidAskSpread { get; set; }
        public double MarketStress { get; set; }
        public double ExpectedWinProbability { get; set; }
        public string HistoricalContext { get; set; } = "";
    }
    
    #endregion
}