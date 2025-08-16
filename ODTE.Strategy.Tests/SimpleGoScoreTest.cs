using System;
using System.Linq;
using ODTE.Strategy;
using ODTE.Strategy.GoScore;
using Xunit;
using FluentAssertions;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// Simple demonstration of GoScore optimization for battle selection
    /// This test shows how GoScore can improve trading performance through intelligent trade selection
    /// </summary>
    public class SimpleGoScoreTest
    {
        [Fact]
        public void GoScore_DemonstratesBattleSelectionOptimization()
        {
            Console.WriteLine("===============================================================================");
            Console.WriteLine("GOSCORE BATTLE SELECTION OPTIMIZATION DEMONSTRATION");
            Console.WriteLine("===============================================================================");
            Console.WriteLine("This test demonstrates how GoScore improves performance by picking better battles");
            Console.WriteLine();

            // Initialize GoScore system
            var goPolicy = GoPolicy.Load(@"C:\code\ODTE\ODTE.GoScoreTests\GoScore.policy.json");
            var goScorer = new GoScorer(goPolicy);

            Console.WriteLine("📊 Testing GoScore decision making for different market scenarios:");
            Console.WriteLine();

            // Test scenario 1: High-quality trade (calm market, good conditions)
            var highQualityInputs = new GoInputs(
                PoE: 0.85,      // High probability of expiring profitable
                PoT: 0.05,      // Low tail risk
                Edge: 0.15,     // Positive mathematical edge
                LiqScore: 0.8,  // Good liquidity
                RegScore: 0.9,  // Perfect regime fit
                PinScore: 0.8,  // Low pin risk
                RfibUtil: 0.3   // Conservative position sizing
            );

            var highQualityBreakdown = goScorer.GetBreakdown(
                highQualityInputs, 
                StrategyKind.CreditBwb, 
                ODTE.Strategy.GoScore.Regime.Calm
            );

            Console.WriteLine($"🟢 HIGH QUALITY TRADE:");
            Console.WriteLine($"   PoE: {highQualityInputs.PoE:P1} | PoT: {highQualityInputs.PoT:P1} | Edge: {highQualityInputs.Edge:+0.0%}");
            Console.WriteLine($"   GoScore: {highQualityBreakdown.FinalScore:F1} → Decision: {highQualityBreakdown.Decision}");
            Console.WriteLine();

            // Test scenario 2: Poor-quality trade (volatile market, bad conditions)
            var poorQualityInputs = new GoInputs(
                PoE: 0.45,      // Low probability of expiring profitable
                PoT: 0.35,      // High tail risk
                Edge: -0.10,    // Negative mathematical edge
                LiqScore: 0.4,  // Poor liquidity
                RegScore: 0.3,  // Bad regime fit
                PinScore: 0.5,  // High pin risk
                RfibUtil: 0.8   // Aggressive position sizing
            );

            var poorQualityBreakdown = goScorer.GetBreakdown(
                poorQualityInputs, 
                StrategyKind.IronCondor, 
                ODTE.Strategy.GoScore.Regime.Convex
            );

            Console.WriteLine($"🔴 POOR QUALITY TRADE:");
            Console.WriteLine($"   PoE: {poorQualityInputs.PoE:P1} | PoT: {poorQualityInputs.PoT:P1} | Edge: {poorQualityInputs.Edge:+0.0%}");
            Console.WriteLine($"   GoScore: {poorQualityBreakdown.FinalScore:F1} → Decision: {poorQualityBreakdown.Decision}");
            Console.WriteLine();

            // Test scenario 3: Marginal trade (mixed signals)
            var marginalInputs = new GoInputs(
                PoE: 0.65,      // Moderate probability
                PoT: 0.15,      // Moderate tail risk
                Edge: 0.02,     // Small positive edge
                LiqScore: 0.7,  // Decent liquidity
                RegScore: 0.6,  // Moderate regime fit
                PinScore: 0.7,  // Moderate pin risk
                RfibUtil: 0.5   // Balanced position sizing
            );

            var marginalBreakdown = goScorer.GetBreakdown(
                marginalInputs, 
                StrategyKind.CreditBwb, 
                ODTE.Strategy.GoScore.Regime.Mixed
            );

            Console.WriteLine($"🟡 MARGINAL TRADE:");
            Console.WriteLine($"   PoE: {marginalInputs.PoE:P1} | PoT: {marginalInputs.PoT:P1} | Edge: {marginalInputs.Edge:+0.0%}");
            Console.WriteLine($"   GoScore: {marginalBreakdown.FinalScore:F1} → Decision: {marginalBreakdown.Decision}");
            Console.WriteLine();

            // Demonstrate battle selection optimization
            Console.WriteLine("🎯 BATTLE SELECTION OPTIMIZATION:");
            Console.WriteLine("═══════════════════════════════════");

            var fullTrades = 0;
            var halfTrades = 0;
            var skippedTrades = 0;

            // Count decisions
            if (highQualityBreakdown.Decision == Decision.Full) fullTrades++;
            else if (highQualityBreakdown.Decision == Decision.Half) halfTrades++;
            else skippedTrades++;

            if (poorQualityBreakdown.Decision == Decision.Full) fullTrades++;
            else if (poorQualityBreakdown.Decision == Decision.Half) halfTrades++;
            else skippedTrades++;

            if (marginalBreakdown.Decision == Decision.Full) fullTrades++;
            else if (marginalBreakdown.Decision == Decision.Half) halfTrades++;
            else skippedTrades++;

            var selectivityRate = (double)skippedTrades / 3;

            Console.WriteLine($"Out of 3 potential trades:");
            Console.WriteLine($"   ✅ Full Position: {fullTrades} trades");
            Console.WriteLine($"   ⚡ Half Position: {halfTrades} trades");
            Console.WriteLine($"   ❌ Skipped: {skippedTrades} trades");
            Console.WriteLine($"   📊 Selectivity Rate: {selectivityRate:P1}");
            Console.WriteLine();

            Console.WriteLine("🧬 GENETIC ALGORITHM OPTIMIZATION OPPORTUNITIES:");
            Console.WriteLine("═════════════════════════════════════════════════");
            Console.WriteLine("1. Weight optimization: wPoE, wPoT, wEdge, wLiq, wReg, wPin, wRfib");
            Console.WriteLine("2. Decision thresholds: Full ≥70, Half ≥55");
            Console.WriteLine("3. Regime-specific adjustments");
            Console.WriteLine("4. Input calculation improvements");
            Console.WriteLine();

            Console.WriteLine("🎯 PERFORMANCE IMPACT:");
            Console.WriteLine("═══════════════════");
            Console.WriteLine("• Intelligent trade selection reduces capital at risk");
            Console.WriteLine("• Higher win rates through quality filtering");
            Console.WriteLine("• Better risk-adjusted returns via selectivity");
            Console.WriteLine("• Adaptive position sizing based on confidence");
            Console.WriteLine();

            Console.WriteLine("===============================================================================");
            Console.WriteLine("GOSCORE DEMONSTRATION COMPLETED");
            Console.WriteLine("===============================================================================");

            // Assertions to validate the optimization concept
            highQualityBreakdown.FinalScore.Should().BeGreaterThan(70, "High quality trades should score above Full threshold");
            poorQualityBreakdown.FinalScore.Should().BeLessThan(60, "Poor quality trades should score lower than good trades");
            
            // Demonstrate selectivity is working
            selectivityRate.Should().BeGreaterThan(0, "GoScore should skip some trades for intelligent battle selection");
            
            Console.WriteLine("✅ All GoScore optimization demonstrations passed!");
        }
    }
}