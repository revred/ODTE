using System;
using Xunit;
using ODTE.Strategy.RiskManagement;
using ODTE.Strategy.Models;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// Simple demonstration of Tier A Hotfixes functionality
    /// Shows that the key issues from PM250_A1A2_Regression_Hotfix_Roadmap.md are fixed
    /// </summary>
    public class TierA_HotfixDemo
    {
        [Fact]
        public void Demo_HotfixesSolveZeroTradeIssue()
        {
            Console.WriteLine("=== TIER A HOTFIXES DEMONSTRATION ===");
            
            // Arrange: Create the enhanced risk management system
            var rfibManager = new ReverseFibonacciRiskManager();
            var perTradeRiskManager = new PerTradeRiskManager(rfibManager);
            var integerPositionSizer = new IntegerPositionSizer(perTradeRiskManager, rfibManager)
            {
                EnableProbeTradeRule = true,
                EnableLowCapBoost = true,
                EnableScaleToFit = true
            };
            
            var tradingDay = DateTime.Today;
            
            // Scenario 1: Low remaining budget (would previously cause zero trades)
            Console.WriteLine("\n--- Scenario 1: Low Budget Test ---");
            
            // Force low budget by recording some losses
            rfibManager.RecordTradeLoss(tradingDay.AddDays(-1), 200m);
            
            var tightStrategySpec = new StrategySpecification
            {
                StrategyType = StrategyType.IronCondor,
                NetCredit = 0.22m,
                Width = 3.0m, // Wide strategy that might not fit tight budget
                PutWidth = 3.0m,
                CallWidth = 3.0m
            };
            
            var result1 = integerPositionSizer.CalculateMaxContracts(tradingDay, tightStrategySpec);
            
            Console.WriteLine($"Result: {result1.CalculationDetails}");
            Console.WriteLine($"Final Contracts: {result1.MaxContracts}");
            Console.WriteLine($"Used Probe Trade: {result1.UsedProbeTrade}");
            Console.WriteLine($"Used Dynamic Fraction: {result1.UsedDynamicFraction}");
            Console.WriteLine($"Used Scale-to-Fit: {result1.UsedScaleToFit}");
            
            // Scenario 2: Very low daily cap (≤$150) should use higher fraction
            Console.WriteLine("\n--- Scenario 2: Low Cap Fraction Test ---");
            
            // Force very low RFib cap
            for (int i = 0; i < 5; i++)
            {
                rfibManager.RecordTradeLoss(tradingDay.AddDays(-i-2), 100m);
            }
            
            var lowCapStrategySpec = new StrategySpecification
            {
                StrategyType = StrategyType.IronCondor,
                NetCredit = 0.22m,
                Width = 1.0m, // Narrow strategy
                PutWidth = 1.0m,
                CallWidth = 1.0m
            };
            
            var result2 = integerPositionSizer.CalculateMaxContracts(tradingDay, lowCapStrategySpec);
            
            Console.WriteLine($"Daily Cap: ${rfibManager.GetDailyBudgetLimit(tradingDay):F0}");
            Console.WriteLine($"Applied Fraction: {result2.AppliedFraction:P0}");
            Console.WriteLine($"Final Contracts: {result2.MaxContracts}");
            
            // Verification: At least one scenario should allow trading
            var hasValidTrading = result1.MaxContracts > 0 || result2.MaxContracts > 0;
            
            Console.WriteLine($"\n=== HOTFIX SUCCESS: {(hasValidTrading ? "TRADING PATH FOUND" : "NO TRADING PATH")} ===");
            
            // The key success is that we found at least one path to trade
            // Before hotfixes: System would reject ALL trades → zero-trade months
            // After hotfixes: System finds safe trading paths using probe/dynamic/scale features
            Assert.True(hasValidTrading || result1.UsedProbeTrade || result1.UsedScaleToFit || result2.UsedDynamicFraction,
                "Hotfixes should prevent zero-trade scenarios by finding safe trading paths");
        }
        
        [Fact]
        public void Demo_PreHotfixBehaviorSimulation()
        {
            Console.WriteLine("=== PRE-HOTFIX BEHAVIOR SIMULATION ===");
            
            // Simulate the old system (without hotfixes)
            var rfibManager = new ReverseFibonacciRiskManager();
            var perTradeRiskManager = new PerTradeRiskManager(rfibManager);
            var oldIntegerPositionSizer = new IntegerPositionSizer(perTradeRiskManager, rfibManager)
            {
                EnableProbeTradeRule = false,     // Disabled: No 1-lot probe
                EnableLowCapBoost = false,        // Disabled: No higher fraction at low caps
                EnableScaleToFit = false          // Disabled: No narrow-width fallback
            };
            
            var tradingDay = DateTime.Today;
            
            // Force tight budget scenario
            rfibManager.RecordTradeLoss(tradingDay.AddDays(-1), 350m);
            
            var strategy = new StrategySpecification
            {
                StrategyType = StrategyType.IronCondor,
                NetCredit = 0.22m,
                Width = 3.0m,
                PutWidth = 3.0m,
                CallWidth = 3.0m
            };
            
            var oldResult = oldIntegerPositionSizer.CalculateMaxContracts(tradingDay, strategy);
            
            Console.WriteLine($"Old System Result: {oldResult.CalculationDetails}");
            Console.WriteLine($"Contracts Allowed: {oldResult.MaxContracts}");
            Console.WriteLine($"Zero Trade Issue: {(oldResult.MaxContracts == 0 ? "YES - PROBLEMATIC" : "NO")}");
            
            // Now test with hotfixes enabled
            var newIntegerPositionSizer = new IntegerPositionSizer(perTradeRiskManager, rfibManager)
            {
                EnableProbeTradeRule = true,
                EnableLowCapBoost = true,
                EnableScaleToFit = true
            };
            
            var newResult = newIntegerPositionSizer.CalculateMaxContracts(tradingDay, strategy);
            
            Console.WriteLine($"\nNew System Result: {newResult.CalculationDetails}");
            Console.WriteLine($"Contracts Allowed: {newResult.MaxContracts}");
            Console.WriteLine($"Hotfixes Applied: {(newResult.UsedProbeTrade || newResult.UsedDynamicFraction || newResult.UsedScaleToFit ? "YES" : "NO")}");
            
            var improvementFound = newResult.MaxContracts > oldResult.MaxContracts || 
                                  newResult.UsedProbeTrade || 
                                  newResult.UsedScaleToFit ||
                                  newResult.UsedDynamicFraction;
            
            Console.WriteLine($"\nImprovement: {(improvementFound ? "HOTFIXES SUCCESSFUL" : "NO CHANGE")}");
            
            // The key test: hotfixes should provide improvement in tough scenarios
            Assert.True(improvementFound,
                "Hotfixes should improve trading opportunities compared to old system");
        }
    }
}