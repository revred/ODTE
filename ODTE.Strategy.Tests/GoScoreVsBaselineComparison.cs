using System;
using System.Collections.Generic;
using System.Linq;
using ODTE.Strategy;
using ODTE.Strategy.GoScore;
using Xunit;
using FluentAssertions;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// GoScore vs Baseline Comparison: The REAL performance test
    /// 
    /// This compares GoScore against the baseline "91% favourable strategy" 
    /// to see if intelligent battle selection actually improves outcomes
    /// vs just trading every day with the standard strategy.
    /// </summary>
    public class GoScoreVsBaselineComparison
    {
        private readonly GoScorer _goScorer;
        private readonly GoPolicy _goPolicy;
        private readonly Random _random = new Random(42);

        public GoScoreVsBaselineComparison()
        {
            _goPolicy = GoPolicy.Load(@"C:\code\ODTE\ODTE.GoScoreTests\GoScore.policy.json");
            _goScorer = new GoScorer(_goPolicy);
        }

        [Fact]
        public void CompareGoScoreVsBaseline_91PercentStrategy()
        {
            Console.WriteLine("===============================================================================");
            Console.WriteLine("GOSCORE VS BASELINE: THE REAL PERFORMANCE TEST");
            Console.WriteLine("===============================================================================");
            Console.WriteLine("Comparing GoScore intelligent selection vs baseline 91% favourable strategy");
            Console.WriteLine();

            // Test period: 1 year of trading
            var startDate = new DateTime(2023, 1, 1);
            var endDate = new DateTime(2023, 12, 31);
            var tradingDays = GetTradingDays(startDate, endDate);
            
            Console.WriteLine($"📅 Test Period: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            Console.WriteLine($"📊 Trading Days: {tradingDays.Count}");
            Console.WriteLine();

            // Run baseline strategy (91% win rate, trade every day)
            var baselineResults = RunBaselineStrategy(tradingDays);
            
            // Run GoScore strategy (intelligent selection)
            var goScoreResults = RunGoScoreStrategy(tradingDays);
            
            // Compare results
            CompareStrategies(baselineResults, goScoreResults, tradingDays.Count);
        }

        private BaselineResults RunBaselineStrategy(List<DateTime> tradingDays)
        {
            Console.WriteLine("🔄 BASELINE STRATEGY: Trade Every Day (91% Win Rate)");
            Console.WriteLine("═══════════════════════════════════════════════════");
            
            var results = new BaselineResults();
            var totalPnL = 0.0;
            var wins = 0;
            var losses = 0;
            var maxDrawdown = 0.0;
            var peak = 0.0;
            var dailyPnLs = new List<double>();

            foreach (var date in tradingDays)
            {
                // Baseline: 91% win rate as mentioned
                var isWin = _random.NextDouble() < 0.91;
                
                double dailyPnL;
                if (isWin)
                {
                    // Win: typical credit collection
                    dailyPnL = 20 + _random.NextDouble() * 30; // $20-50 profit
                    wins++;
                }
                else
                {
                    // Loss: typical max loss
                    dailyPnL = -(100 + _random.NextDouble() * 200); // $100-300 loss
                    losses++;
                }
                
                totalPnL += dailyPnL;
                dailyPnLs.Add(dailyPnL);
                
                // Track drawdown
                peak = Math.Max(peak, totalPnL);
                var currentDrawdown = totalPnL - peak;
                maxDrawdown = Math.Min(maxDrawdown, currentDrawdown);
            }

            results.TotalPnL = totalPnL;
            results.WinRate = (double)wins / tradingDays.Count;
            results.MaxDrawdown = maxDrawdown;
            results.TotalTrades = tradingDays.Count;
            results.WinningTrades = wins;
            results.LosingTrades = losses;
            results.DailyPnLs = dailyPnLs;

            Console.WriteLine($"   Total P&L: ${results.TotalPnL:F0}");
            Console.WriteLine($"   Win Rate: {results.WinRate:P1}");
            Console.WriteLine($"   Total Trades: {results.TotalTrades}");
            Console.WriteLine($"   Winning Trades: {results.WinningTrades}");
            Console.WriteLine($"   Losing Trades: {results.LosingTrades}");
            Console.WriteLine($"   Max Drawdown: ${results.MaxDrawdown:F0}");
            Console.WriteLine();

            return results;
        }

        private GoScoreResults RunGoScoreStrategy(List<DateTime> tradingDays)
        {
            Console.WriteLine("🧠 GOSCORE STRATEGY: Intelligent Battle Selection");
            Console.WriteLine("═══════════════════════════════════════════════");
            
            var results = new GoScoreResults();
            var totalPnL = 0.0;
            var wins = 0;
            var losses = 0;
            var maxDrawdown = 0.0;
            var peak = 0.0;
            var fullPositions = 0;
            var halfPositions = 0;
            var skippedTrades = 0;
            var dailyPnLs = new List<double>();

            foreach (var date in tradingDays)
            {
                // Generate market conditions
                var conditions = GetMarketConditions(date);
                var regime = ClassifyRegime(conditions);
                var strategy = GetStrategyForRegime(regime);
                
                // Calculate GoScore
                var goInputs = CalculateGoScoreInputs(conditions, strategy);
                var breakdown = _goScorer.GetBreakdown(
                    goInputs,
                    strategy.Type,
                    MapRegime(regime)
                );
                
                double dailyPnL = 0;
                
                // Execute based on GoScore decision
                switch (breakdown.Decision)
                {
                    case ODTE.Strategy.GoScore.Decision.Full:
                        fullPositions++;
                        dailyPnL = SimulateTradeOutcome(conditions, 1.0);
                        break;
                        
                    case ODTE.Strategy.GoScore.Decision.Half:
                        halfPositions++;
                        dailyPnL = SimulateTradeOutcome(conditions, 0.5);
                        break;
                        
                    case ODTE.Strategy.GoScore.Decision.Skip:
                        skippedTrades++;
                        dailyPnL = 0; // No trade, no P&L
                        break;
                }
                
                if (dailyPnL > 0) wins++;
                else if (dailyPnL < 0) losses++;
                
                totalPnL += dailyPnL;
                dailyPnLs.Add(dailyPnL);
                
                // Track drawdown
                peak = Math.Max(peak, totalPnL);
                var currentDrawdown = totalPnL - peak;
                maxDrawdown = Math.Min(maxDrawdown, currentDrawdown);
            }

            results.TotalPnL = totalPnL;
            results.WinRate = wins > 0 ? (double)wins / (wins + losses) : 0;
            results.MaxDrawdown = maxDrawdown;
            results.TotalOpportunities = tradingDays.Count;
            results.FullPositions = fullPositions;
            results.HalfPositions = halfPositions;
            results.SkippedTrades = skippedTrades;
            results.ActualTrades = fullPositions + halfPositions;
            results.WinningTrades = wins;
            results.LosingTrades = losses;
            results.DailyPnLs = dailyPnLs;
            results.SelectivityRate = (double)skippedTrades / tradingDays.Count;

            Console.WriteLine($"   Total P&L: ${results.TotalPnL:F0}");
            Console.WriteLine($"   Win Rate: {results.WinRate:P1} (of executed trades)");
            Console.WriteLine($"   Total Opportunities: {results.TotalOpportunities}");
            Console.WriteLine($"   Full Positions: {results.FullPositions}");
            Console.WriteLine($"   Half Positions: {results.HalfPositions}");
            Console.WriteLine($"   Skipped Trades: {results.SkippedTrades}");
            Console.WriteLine($"   Selectivity Rate: {results.SelectivityRate:P1}");
            Console.WriteLine($"   Max Drawdown: ${results.MaxDrawdown:F0}");
            Console.WriteLine();

            return results;
        }

        private void CompareStrategies(BaselineResults baseline, GoScoreResults goScore, int totalDays)
        {
            Console.WriteLine("⚔️ STRATEGY COMPARISON");
            Console.WriteLine("═══════════════════");
            
            var pnlImprovement = goScore.TotalPnL - baseline.TotalPnL;
            var pnlImprovementPct = baseline.TotalPnL != 0 ? (pnlImprovement / Math.Abs(baseline.TotalPnL)) * 100 : 0;
            
            var drawdownImprovement = Math.Abs(goScore.MaxDrawdown) - Math.Abs(baseline.MaxDrawdown);
            var drawdownImprovementPct = baseline.MaxDrawdown != 0 ? (drawdownImprovement / Math.Abs(baseline.MaxDrawdown)) * 100 : 0;

            Console.WriteLine($"📊 PERFORMANCE METRICS:");
            Console.WriteLine($"                     BASELINE    GOSCORE     DIFFERENCE");
            Console.WriteLine($"   Total P&L:        ${baseline.TotalPnL:F0}       ${goScore.TotalPnL:F0}       ${pnlImprovement:+0;-0;0} ({pnlImprovementPct:+0.0;-0.0;0.0}%)");
            Console.WriteLine($"   Max Drawdown:     ${baseline.MaxDrawdown:F0}      ${goScore.MaxDrawdown:F0}      ${-drawdownImprovement:+0;-0;0} ({-drawdownImprovementPct:+0.0;-0.0;0.0}%)");
            Console.WriteLine($"   Win Rate:         {baseline.WinRate:P1}       {goScore.WinRate:P1}       {(goScore.WinRate - baseline.WinRate)*100:+0.0;-0.0;0.0}%");
            Console.WriteLine($"   Total Trades:     {baseline.TotalTrades}         {goScore.ActualTrades}         {goScore.ActualTrades - baseline.TotalTrades}");
            Console.WriteLine();
            
            Console.WriteLine($"🎯 GOSCORE SELECTIVITY:");
            Console.WriteLine($"   Opportunities:    {goScore.TotalOpportunities}");
            Console.WriteLine($"   Executed:         {goScore.ActualTrades} ({100.0 * goScore.ActualTrades / goScore.TotalOpportunities:F1}%)");
            Console.WriteLine($"   Skipped:          {goScore.SkippedTrades} ({goScore.SelectivityRate:P1})");
            Console.WriteLine();
            
            // Calculate risk-adjusted metrics
            var baselineSharpe = CalculateSharpeRatio(baseline.DailyPnLs);
            var goScoreSharpe = CalculateSharpeRatio(goScore.DailyPnLs);
            
            Console.WriteLine($"📈 RISK-ADJUSTED PERFORMANCE:");
            Console.WriteLine($"   Baseline Sharpe:  {baselineSharpe:F2}");
            Console.WriteLine($"   GoScore Sharpe:   {goScoreSharpe:F2}");
            Console.WriteLine($"   Sharpe Improvement: {goScoreSharpe - baselineSharpe:+0.00;-0.00;0.00}");
            Console.WriteLine();
            
            Console.WriteLine($"💡 KEY INSIGHTS:");
            
            if (pnlImprovement > 0)
            {
                Console.WriteLine($"   ✅ GoScore IMPROVED total returns by ${pnlImprovement:F0} ({pnlImprovementPct:+0.0}%)");
            }
            else
            {
                Console.WriteLine($"   ❌ GoScore REDUCED total returns by ${Math.Abs(pnlImprovement):F0} ({Math.Abs(pnlImprovementPct):0.0}%)");
            }
            
            if (drawdownImprovement < 0)
            {
                Console.WriteLine($"   ✅ GoScore REDUCED max drawdown by ${Math.Abs(drawdownImprovement):F0} ({Math.Abs(drawdownImprovementPct):0.0}%)");
            }
            else
            {
                Console.WriteLine($"   ❌ GoScore INCREASED max drawdown by ${drawdownImprovement:F0} ({drawdownImprovementPct:0.0}%)");
            }
            
            if (goScoreSharpe > baselineSharpe)
            {
                Console.WriteLine($"   ✅ GoScore has BETTER risk-adjusted returns (Sharpe: {goScoreSharpe:F2} vs {baselineSharpe:F2})");
            }
            else
            {
                Console.WriteLine($"   ❌ GoScore has WORSE risk-adjusted returns (Sharpe: {goScoreSharpe:F2} vs {baselineSharpe:F2})");
            }
            
            var tradesReduction = baseline.TotalTrades - goScore.ActualTrades;
            Console.WriteLine($"   📊 GoScore executed {tradesReduction} fewer trades ({100.0 * tradesReduction / baseline.TotalTrades:F1}% reduction)");
            
            Console.WriteLine();
            Console.WriteLine("===============================================================================");
            Console.WriteLine("COMPARISON COMPLETE - IS GOSCORE WORTH THE COMPLEXITY?");
            Console.WriteLine("===============================================================================");
        }

        // Helper methods (same as previous analysis)
        private List<DateTime> GetTradingDays(DateTime start, DateTime end)
        {
            var days = new List<DateTime>();
            var current = start;
            
            while (current <= end)
            {
                if (current.DayOfWeek != DayOfWeek.Saturday && 
                    current.DayOfWeek != DayOfWeek.Sunday)
                {
                    days.Add(current);
                }
                current = current.AddDays(1);
            }
            
            return days;
        }

        private TestMarketConditions GetMarketConditions(DateTime date)
        {
            var dayOfYear = date.DayOfYear;
            var vix = 15 + Math.Sin(dayOfYear * 0.1) * 10 + _random.NextDouble() * 15;
            
            return new TestMarketConditions
            {
                Date = date,
                VIX = vix,
                IVRank = (_random.NextDouble() + Math.Sin(dayOfYear * 0.05)) / 2,
                TrendScore = Math.Sin(dayOfYear * 0.02) * 0.8,
                TermSlope = 0.9 + _random.NextDouble() * 0.3,
                RealizedVolatility = vix * (0.8 + _random.NextDouble() * 0.4),
                ImpliedVolatility = vix
            };
        }

        private string ClassifyRegime(TestMarketConditions conditions)
        {
            if (conditions.VIX > 40 || Math.Abs(conditions.TrendScore) >= 0.8)
                return "Convex";
            else if (conditions.VIX > 25)
                return "Mixed";
            else
                return "Calm";
        }

        private StrategySpec GetStrategyForRegime(string regime)
        {
            return regime switch
            {
                "Calm" => new StrategySpec { Type = StrategyKind.CreditBwb, CreditTarget = 0.25 },
                "Mixed" => new StrategySpec { Type = StrategyKind.CreditBwb, CreditTarget = 0.30 },
                "Convex" => new StrategySpec { Type = StrategyKind.IronCondor, CreditTarget = 0.20 },
                _ => new StrategySpec { Type = StrategyKind.CreditBwb, CreditTarget = 0.25 }
            };
        }

        private GoInputs CalculateGoScoreInputs(TestMarketConditions conditions, StrategySpec strategy)
        {
            var basePoE = 0.65 + conditions.IVRank * 0.2 - Math.Abs(conditions.TrendScore) * 0.15;
            var poT = Math.Min(0.8, conditions.VIX / 100.0 + Math.Abs(conditions.TrendScore) * 0.3);
            var edge = (conditions.IVRank - 0.5) * 0.2 + (strategy.CreditTarget - 0.2) * 0.5;
            var liqScore = conditions.VIX < 50 ? 0.8 : 0.6;
            var regScore = 0.8;
            var pinScore = conditions.VIX < 15 ? 0.6 : 0.8;
            var rfibUtil = strategy.CreditTarget * 0.8;

            return new GoInputs(
                PoE: Math.Max(0.2, Math.Min(0.95, basePoE)),
                PoT: Math.Max(0.01, Math.Min(0.8, poT)),
                Edge: Math.Max(-0.3, Math.Min(0.3, edge)),
                LiqScore: liqScore,
                RegScore: regScore,
                PinScore: pinScore,
                RfibUtil: Math.Max(0.1, Math.Min(0.9, rfibUtil))
            );
        }

        private double SimulateTradeOutcome(TestMarketConditions conditions, double positionSize)
        {
            // Use the same win rate logic as baseline but with market condition adjustments
            var baseWinRate = 0.91; // Same as baseline
            
            // Adjust for market conditions (GoScore should pick better conditions)
            if (conditions.VIX > 30) baseWinRate -= 0.05;
            if (Math.Abs(conditions.TrendScore) > 0.5) baseWinRate -= 0.08;
            if (conditions.IVRank > 0.7) baseWinRate += 0.03;
            
            var isWin = _random.NextDouble() < baseWinRate;
            
            double basePnL;
            if (isWin)
            {
                basePnL = 20 + _random.NextDouble() * 30; // $20-50 profit
            }
            else
            {
                basePnL = -(100 + _random.NextDouble() * 200); // $100-300 loss
            }
            
            return basePnL * positionSize;
        }

        private ODTE.Strategy.GoScore.Regime MapRegime(string regime)
        {
            return regime switch
            {
                "Calm" => ODTE.Strategy.GoScore.Regime.Calm,
                "Mixed" => ODTE.Strategy.GoScore.Regime.Mixed,
                "Convex" => ODTE.Strategy.GoScore.Regime.Convex,
                _ => ODTE.Strategy.GoScore.Regime.Calm
            };
        }

        private double CalculateSharpeRatio(List<double> returns)
        {
            if (returns.Count < 2) return 0;
            
            var avgReturn = returns.Average();
            var stdDev = Math.Sqrt(returns.Sum(r => Math.Pow(r - avgReturn, 2)) / (returns.Count - 1));
            
            return stdDev > 0 ? avgReturn / stdDev : 0;
        }
    }

    public class BaselineResults
    {
        public double TotalPnL { get; set; }
        public double WinRate { get; set; }
        public double MaxDrawdown { get; set; }
        public int TotalTrades { get; set; }
        public int WinningTrades { get; set; }
        public int LosingTrades { get; set; }
        public List<double> DailyPnLs { get; set; } = new();
    }

    public class GoScoreResults
    {
        public double TotalPnL { get; set; }
        public double WinRate { get; set; }
        public double MaxDrawdown { get; set; }
        public int TotalOpportunities { get; set; }
        public int FullPositions { get; set; }
        public int HalfPositions { get; set; }
        public int SkippedTrades { get; set; }
        public int ActualTrades { get; set; }
        public int WinningTrades { get; set; }
        public int LosingTrades { get; set; }
        public double SelectivityRate { get; set; }
        public List<double> DailyPnLs { get; set; } = new();
    }

}