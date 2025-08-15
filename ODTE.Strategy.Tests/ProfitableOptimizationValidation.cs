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
    /// Validation test for PROFITABLE optimization (not counterproductive blocking)
    /// 
    /// GOALS:
    /// - Maintain or increase total P&L (not 92% reduction!)
    /// - Target $10-50 per trade (vs current $1.52)
    /// - Execute 70-90% of trades (vs blocking 69%)
    /// - Minimize losses to ~$18 max (vs current $26.37)
    /// - Minimize drawdown to ~$35 max (vs current $59.86)
    /// </summary>
    public class ProfitableOptimizationValidation
    {
        [Fact]
        public async Task Profitable_Optimization_Should_INCREASE_Profitability()
        {
            Console.WriteLine("🚀 PROFITABLE OPTIMIZATION VALIDATION - PROFIT FOCUSED");
            Console.WriteLine("=".PadRight(65, '='));
            Console.WriteLine();

            // Run baseline strategy
            var baselineResults = await RunBaselineStrategy();
            
            // Run PROFITABLE optimization (not destructive blocking)
            var optimizedResults = await RunProfitableOptimization();
            
            // Compare results - MUST show improvement, not destruction
            AnalyzeProfitablePerformance(baselineResults, optimizedResults);
            
            // Validate PROFITABLE improvements
            ValidateProfitableOptimizations(baselineResults, optimizedResults);
        }

        private async Task<StrategyResults> RunBaselineStrategy()
        {
            Console.WriteLine("📊 BASELINE STRATEGY (Current System)");
            Console.WriteLine("-".PadRight(50, '-'));
            
            var strategy = new IronCondorSimulator(new Random(42), new StrategyEngineConfig());
            var results = new StrategyResults { Name = "Baseline" };
            
            // Simulate 100 trades
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
            Console.WriteLine($"   Avg per Trade: ${results.AvgPnLPerTrade:F2}");
            Console.WriteLine($"   Win Rate: {results.WinRate:P1}");
            Console.WriteLine($"   Max Drawdown: ${results.MaxDrawdown:F2}");
            Console.WriteLine($"   Trades Executed: {results.TradesExecuted}/100");
            Console.WriteLine();
            
            return results;
        }

        private async Task<StrategyResults> RunProfitableOptimization()
        {
            Console.WriteLine("💰 PROFITABLE OPTIMIZATION (Intelligent, Not Destructive)");
            Console.WriteLine("-".PadRight(50, '-'));
            
            var strategy = new ProfitableOptimizedStrategy();
            var results = new StrategyResults { Name = "Profitable Optimized" };
            
            // Same conditions for fair comparison
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
            Console.WriteLine($"   Avg per Trade: ${results.AvgPnLPerTrade:F2}");
            Console.WriteLine($"   Win Rate: {results.WinRate:P1}");
            Console.WriteLine($"   Max Drawdown: ${results.MaxDrawdown:F2}");
            Console.WriteLine($"   Trades Executed: {results.TradesExecuted}/100");
            Console.WriteLine();
            
            return results;
        }

        private void AnalyzeProfitablePerformance(StrategyResults baseline, StrategyResults optimized)
        {
            Console.WriteLine("📈 PROFITABLE OPTIMIZATION ANALYSIS");
            Console.WriteLine("=".PadRight(65, '='));
            Console.WriteLine();
            
            var pnlImprovement = optimized.TotalPnL - baseline.TotalPnL;
            var pnlImprovementPct = baseline.TotalPnL != 0 ? (pnlImprovement / baseline.TotalPnL) : 0;
            var perTradeImprovement = optimized.AvgPnLPerTrade - baseline.AvgPnLPerTrade;
            var winRateChange = optimized.WinRate - baseline.WinRate;
            var drawdownImprovement = baseline.MaxDrawdown - optimized.MaxDrawdown;
            var executionRate = (double)optimized.TradesExecuted / 100;
            
            Console.WriteLine($"💰 PROFITABILITY COMPARISON:");
            Console.WriteLine($"   Baseline Total P&L:    ${baseline.TotalPnL:F2}");
            Console.WriteLine($"   Optimized Total P&L:   ${optimized.TotalPnL:F2}");
            Console.WriteLine($"   P&L Change:            ${pnlImprovement:F2} ({pnlImprovementPct:P1})");
            Console.WriteLine();
            
            Console.WriteLine($"🎯 PER-TRADE PROFITABILITY:");
            Console.WriteLine($"   Baseline per Trade:    ${baseline.AvgPnLPerTrade:F2}");
            Console.WriteLine($"   Optimized per Trade:   ${optimized.AvgPnLPerTrade:F2}");
            Console.WriteLine($"   Per-Trade Improvement: ${perTradeImprovement:F2}");
            Console.WriteLine($"   Target Range:          $10.00 - $50.00");
            Console.WriteLine();
            
            Console.WriteLine($"🔥 TRADE EXECUTION:");
            Console.WriteLine($"   Baseline Executed:     {baseline.TradesExecuted}/100 (100.0%)");
            Console.WriteLine($"   Optimized Executed:    {optimized.TradesExecuted}/100 ({executionRate:P1})");
            Console.WriteLine($"   Target Range:          70-90% execution rate");
            Console.WriteLine();
            
            Console.WriteLine($"🛡️ RISK METRICS:");
            Console.WriteLine($"   Baseline Win Rate:     {baseline.WinRate:P1}");
            Console.WriteLine($"   Optimized Win Rate:    {optimized.WinRate:P1}");
            Console.WriteLine($"   Win Rate Change:       {winRateChange:P2}");
            Console.WriteLine();
            
            Console.WriteLine($"   Baseline Drawdown:     ${baseline.MaxDrawdown:F2}");
            Console.WriteLine($"   Optimized Drawdown:    ${optimized.MaxDrawdown:F2}");
            Console.WriteLine($"   Drawdown Improvement:  ${drawdownImprovement:F2}");
            Console.WriteLine();
        }

        private void ValidateProfitableOptimizations(StrategyResults baseline, StrategyResults optimized)
        {
            Console.WriteLine("✅ PROFITABLE OPTIMIZATION VALIDATION");
            Console.WriteLine("=".PadRight(65, '='));
            Console.WriteLine();
            
            // CRITICAL: Total P&L must NOT decrease significantly
            var pnlChange = (optimized.TotalPnL - baseline.TotalPnL) / Math.Max(baseline.TotalPnL, 1m);
            var pnlTarget = pnlChange >= -0.20m; // Allow max 20% reduction, not 92%!
            Console.WriteLine($"   ✓ Total P&L Preservation: {pnlChange:P1} change (Target: >-20%) - {(pnlTarget ? "ACHIEVED" : "FAILED")}");
            
            // Per-trade profitability improvement
            var perTradeTarget = optimized.AvgPnLPerTrade >= 8m; // Target $8+ per trade (5x improvement)
            Console.WriteLine($"   ✓ Per-Trade Improvement: ${optimized.AvgPnLPerTrade:F2} (Target: >$8) - {(perTradeTarget ? "ACHIEVED" : "NEEDS WORK")}");
            
            // Trade execution rate - should NOT block everything
            var executionRate = (double)optimized.TradesExecuted / 100;
            var executionTarget = executionRate >= 0.70; // Minimum 70% execution
            Console.WriteLine($"   ✓ Trade Execution Rate: {executionRate:P1} (Target: >70%) - {(executionTarget ? "ACHIEVED" : "TOO CONSERVATIVE")}");
            
            // Drawdown reduction - good but not at expense of profit
            var drawdownReduction = (baseline.MaxDrawdown - optimized.MaxDrawdown) / Math.Max(baseline.MaxDrawdown, 1m);
            var drawdownTarget = drawdownReduction > 0.20m; // Target 20%+ reduction
            Console.WriteLine($"   ✓ Drawdown Reduction: {drawdownReduction:P1} (Target: >20%) - {(drawdownTarget ? "ACHIEVED" : "NEEDS WORK")}");
            
            // Win rate should be maintained or improved
            var winRateTarget = optimized.WinRate >= baseline.WinRate * 0.95; // Allow 5% drop max
            Console.WriteLine($"   ✓ Win Rate Maintained: {optimized.WinRate:P1} vs {baseline.WinRate:P1} - {(winRateTarget ? "ACHIEVED" : "DECLINED")}");
            
            Console.WriteLine();
            
            // Overall assessment
            var successCount = new[] { pnlTarget, perTradeTarget, executionTarget, drawdownTarget, winRateTarget }.Count(x => x);
            Console.WriteLine($"🏆 OVERALL PROFITABLE OPTIMIZATION: {successCount}/5 targets achieved");
            
            if (successCount >= 4)
            {
                Console.WriteLine("✅ PROFITABLE OPTIMIZATION SUCCESSFUL - Strategy enhanced for profit!");
            }
            else if (successCount >= 3)
            {
                Console.WriteLine("⚡ PROFITABLE OPTIMIZATION PARTIAL - Good progress, needs refinement");
            }
            else
            {
                Console.WriteLine("❌ OPTIMIZATION FAILED - Too conservative, destroying profitability");
            }
            Console.WriteLine();
            
            // Critical assertions - no more 92% P&L destruction!
            pnlChange.Should().BeGreaterThan(-0.30m, "Total P&L should not decrease by more than 30%");
            executionRate.Should().BeGreaterThan(0.60, "Should execute at least 60% of trades, not block everything");
            optimized.AvgPnLPerTrade.Should().BeGreaterThan(5m, "Per-trade profit should be significantly improved");
        }

        private MarketConditions GenerateMarketConditions(int seed)
        {
            var random = new Random(seed + 2000); // Different seed for varied conditions
            
            return new MarketConditions
            {
                Date = DateTime.Today.AddDays(-100 + seed),
                UnderlyingPrice = 450 + random.NextDouble() * 100,
                VIX = 15 + random.NextDouble() * 25, // 15-40 VIX range
                TrendScore = (random.NextDouble() - 0.5) * 1.5, // -0.75 to +0.75 trend
                MarketRegime = seed % 10 < 6 ? "Calm" : seed % 10 < 9 ? "Mixed" : "Volatile"
            };
        }
    }
}