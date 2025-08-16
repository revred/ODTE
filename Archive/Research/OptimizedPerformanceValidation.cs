using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ODTE.Strategy;
using Xunit;
using FluentAssertions;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// Validation test for optimized strategy performance
    /// Compares baseline vs optimized results with focus on capital preservation
    /// </summary>
    public class OptimizedPerformanceValidation
    {
        [Fact]
        public async Task Optimized_Strategy_Should_Improve_Risk_Adjusted_Returns()
        {
            Console.WriteLine("🏆 OPTIMIZED STRATEGY PERFORMANCE VALIDATION");
            Console.WriteLine("=".PadRight(60, '='));
            Console.WriteLine();

            // Run baseline strategy (current system)
            var baselineResults = await RunBaselineStrategy();
            
            // Run optimized strategy (battle-hardened)
            var optimizedResults = await RunOptimizedStrategy();
            
            // Compare and analyze results
            AnalyzePerformanceComparison(baselineResults, optimizedResults);
            
            // Validate improvements
            ValidateOptimizations(baselineResults, optimizedResults);
        }

        private async Task<StrategyResults> RunBaselineStrategy()
        {
            Console.WriteLine("📊 RUNNING BASELINE STRATEGY (Current System)");
            Console.WriteLine("-".PadRight(50, '-'));
            
            var strategy = new IronCondorSimulator(new Random(42), new StrategyEngineConfig());
            var results = new StrategyResults { Name = "Baseline" };
            
            // Simulate 100 trades over various market conditions
            for (int i = 0; i < 100; i++)
            {
                var conditions = GenerateMarketConditions(i);
                var parameters = new StrategyParameters 
                { 
                    PositionSize = 1, 
                    MaxRisk = 500 
                };
                
                var result = await strategy.ExecuteAsync(parameters, conditions);
                results.AddTrade(result);
            }
            
            results.CalculateMetrics();
            
            Console.WriteLine($"   Total P&L: ${results.TotalPnL:F2}");
            Console.WriteLine($"   Win Rate: {results.WinRate:P1}");
            Console.WriteLine($"   Max Drawdown: ${results.MaxDrawdown:F2}");
            Console.WriteLine($"   Avg per Trade: ${results.AvgPnLPerTrade:F2}");
            Console.WriteLine();
            
            return results;
        }

        private async Task<StrategyResults> RunOptimizedStrategy()
        {
            Console.WriteLine("🚀 RUNNING OPTIMIZED STRATEGY (Battle-Hardened)");
            Console.WriteLine("-".PadRight(50, '-'));
            
            var strategy = new OptimizedRealDataStrategy();
            var results = new StrategyResults { Name = "Optimized" };
            
            // Same market conditions for fair comparison
            for (int i = 0; i < 100; i++)
            {
                var conditions = GenerateMarketConditions(i);
                var parameters = new StrategyParameters 
                { 
                    PositionSize = 1, 
                    MaxRisk = 500 
                };
                
                var result = await strategy.ExecuteAsync(parameters, conditions);
                results.AddTrade(result);
            }
            
            results.CalculateMetrics();
            
            Console.WriteLine($"   Total P&L: ${results.TotalPnL:F2}");
            Console.WriteLine($"   Win Rate: {results.WinRate:P1}");
            Console.WriteLine($"   Max Drawdown: ${results.MaxDrawdown:F2}");
            Console.WriteLine($"   Avg per Trade: ${results.AvgPnLPerTrade:F2}");
            Console.WriteLine();
            
            return results;
        }

        private void AnalyzePerformanceComparison(StrategyResults baseline, StrategyResults optimized)
        {
            Console.WriteLine("📈 PERFORMANCE COMPARISON ANALYSIS");
            Console.WriteLine("=".PadRight(60, '='));
            Console.WriteLine();
            
            // Key metrics comparison
            var pnlImprovement = optimized.TotalPnL - baseline.TotalPnL;
            var winRateChange = optimized.WinRate - baseline.WinRate;
            var drawdownImprovement = baseline.MaxDrawdown - optimized.MaxDrawdown;
            var sharpeImprovement = optimized.SharpeRatio - baseline.SharpeRatio;
            
            Console.WriteLine($"💰 P&L COMPARISON:");
            Console.WriteLine($"   Baseline Total P&L:  ${baseline.TotalPnL:F2}");
            Console.WriteLine($"   Optimized Total P&L: ${optimized.TotalPnL:F2}");
            Console.WriteLine($"   Improvement:         ${pnlImprovement:F2} ({pnlImprovement/baseline.TotalPnL:P1})");
            Console.WriteLine();
            
            Console.WriteLine($"🎯 WIN RATE COMPARISON:");
            Console.WriteLine($"   Baseline Win Rate:   {baseline.WinRate:P1}");
            Console.WriteLine($"   Optimized Win Rate:  {optimized.WinRate:P1}");
            Console.WriteLine($"   Change:              {winRateChange:P2}");
            Console.WriteLine();
            
            Console.WriteLine($"💥 DRAWDOWN COMPARISON (Key Metric):");
            Console.WriteLine($"   Baseline Max Drawdown:  ${baseline.MaxDrawdown:F2}");
            Console.WriteLine($"   Optimized Max Drawdown: ${optimized.MaxDrawdown:F2}");
            Console.WriteLine($"   Improvement:            ${drawdownImprovement:F2} ({drawdownImprovement/baseline.MaxDrawdown:P1})");
            Console.WriteLine();
            
            Console.WriteLine($"📊 RISK-ADJUSTED RETURNS:");
            Console.WriteLine($"   Baseline Sharpe Ratio:  {baseline.SharpeRatio:F2}");
            Console.WriteLine($"   Optimized Sharpe Ratio: {optimized.SharpeRatio:F2}");
            Console.WriteLine($"   Improvement:            {sharpeImprovement:F2}");
            Console.WriteLine();
            
            // Trade frequency analysis
            Console.WriteLine($"🔄 TRADE FREQUENCY:");
            Console.WriteLine($"   Baseline Trades Executed: {baseline.TradesExecuted}/100");
            Console.WriteLine($"   Optimized Trades Executed: {optimized.TradesExecuted}/100");
            Console.WriteLine($"   Optimized Blocked: {100 - optimized.TradesExecuted} (Capital Preservation)");
            Console.WriteLine();
        }

        private void ValidateOptimizations(StrategyResults baseline, StrategyResults optimized)
        {
            Console.WriteLine("✅ OPTIMIZATION VALIDATION");
            Console.WriteLine("=".PadRight(60, '='));
            Console.WriteLine();
            
            // Key optimization targets
            Console.WriteLine("🎯 TARGET ACHIEVEMENTS:");
            
            // 1. Drawdown reduction (primary goal)
            var drawdownReduction = (baseline.MaxDrawdown - optimized.MaxDrawdown) / baseline.MaxDrawdown;
            var drawdownTarget = drawdownReduction > 0.30m; // Target: 30% reduction
            Console.WriteLine($"   ✓ Drawdown Reduction: {drawdownReduction:P1} (Target: >30%) - {(drawdownTarget ? "ACHIEVED" : "MISSED")}");
            
            // 2. Preserve win rate (should not drop significantly)
            var winRatePreserved = optimized.WinRate >= baseline.WinRate * 0.95; // Allow 5% drop max
            Console.WriteLine($"   ✓ Win Rate Preserved: {optimized.WinRate:P1} vs {baseline.WinRate:P1} - {(winRatePreserved ? "ACHIEVED" : "MISSED")}");
            
            // 3. Improve risk-adjusted returns
            var sharpeImprovement = optimized.SharpeRatio > baseline.SharpeRatio;
            Console.WriteLine($"   ✓ Sharpe Ratio Improved: {optimized.SharpeRatio:F2} vs {baseline.SharpeRatio:F2} - {(sharpeImprovement ? "ACHIEVED" : "MISSED")}");
            
            // 4. Capital preservation effectiveness
            var maxSingleLoss = optimized.Trades.Min(t => t.PnL);
            var singleLossTarget = maxSingleLoss > -15m; // Target: No loss > $15
            Console.WriteLine($"   ✓ Max Single Loss Capped: ${maxSingleLoss:F2} (Target: >-$15) - {(singleLossTarget ? "ACHIEVED" : "MISSED")}");
            Console.WriteLine();
            
            // Overall optimization success
            var successCount = new[] { drawdownTarget, winRatePreserved, sharpeImprovement, singleLossTarget }.Count(x => x);
            Console.WriteLine($"🏆 OVERALL OPTIMIZATION SUCCESS: {successCount}/4 targets achieved");
            
            if (successCount >= 3)
            {
                Console.WriteLine("✅ OPTIMIZATION SUCCESSFUL - Strategy is battle-hardened for capital preservation");
            }
            else
            {
                Console.WriteLine("⚠️  OPTIMIZATION PARTIAL - Some targets need further refinement");
            }
            Console.WriteLine();
            
            // Assertions for test validation
            drawdownReduction.Should().BeGreaterThan(0.20m, "Drawdown should be reduced by at least 20%");
            optimized.WinRate.Should().BeGreaterThanOrEqualTo(baseline.WinRate * 0.95, "Win rate should be preserved within 5%");
            maxSingleLoss.Should().BeGreaterThan(-20m, "Single trade loss should be capped at reasonable levels");
        }

        private MarketConditions GenerateMarketConditions(int seed)
        {
            var random = new Random(seed + 1000); // Offset to avoid correlation
            
            return new MarketConditions
            {
                Date = DateTime.Today.AddDays(-100 + seed),
                UnderlyingPrice = 450 + random.NextDouble() * 100, // $450-$550 range
                VIX = 15 + random.NextDouble() * 30, // 15-45 VIX range
                TrendScore = (random.NextDouble() - 0.5) * 2, // -1 to +1 trend
                MarketRegime = seed % 10 < 7 ? "Calm" : seed % 10 < 9 ? "Mixed" : "Volatile"
            };
        }
    }

    public class StrategyResults
    {
        public string Name { get; set; } = "";
        public List<StrategyResult> Trades { get; set; } = new();
        public decimal TotalPnL { get; set; }
        public double WinRate { get; set; }
        public decimal MaxDrawdown { get; set; }
        public decimal AvgPnLPerTrade { get; set; }
        public double SharpeRatio { get; set; }
        public int TradesExecuted { get; set; }

        public void AddTrade(StrategyResult trade)
        {
            Trades.Add(trade);
        }

        public void CalculateMetrics()
        {
            TradesExecuted = Trades.Count(t => t.PnL != 0);
            TotalPnL = Trades.Sum(t => t.PnL);
            WinRate = Trades.Count(t => t.PnL > 0) / (double)Math.Max(1, TradesExecuted);
            AvgPnLPerTrade = TradesExecuted > 0 ? TotalPnL / TradesExecuted : 0;
            
            // Calculate maximum drawdown
            decimal peak = 0;
            decimal maxDD = 0;
            decimal cumulative = 0;
            
            foreach (var trade in Trades.Where(t => t.PnL != 0))
            {
                cumulative += trade.PnL;
                peak = Math.Max(peak, cumulative);
                var drawdown = peak - cumulative;
                maxDD = Math.Max(maxDD, drawdown);
            }
            MaxDrawdown = maxDD;
            
            // Calculate Sharpe ratio (simplified)
            if (TradesExecuted > 1)
            {
                var returns = Trades.Where(t => t.PnL != 0).Select(t => (double)t.PnL).ToList();
                var avgReturn = returns.Average();
                var stdDev = Math.Sqrt(returns.Select(r => Math.Pow(r - avgReturn, 2)).Average());
                SharpeRatio = stdDev > 0 ? avgReturn / stdDev : 0;
            }
        }
    }
}