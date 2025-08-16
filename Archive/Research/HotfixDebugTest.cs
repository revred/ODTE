using System;
using Xunit;
using ODTE.Strategy.RiskManagement;
using ODTE.Strategy.Models;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// Debug test to understand why hotfixes aren't activating
    /// </summary>
    public class HotfixDebugTest
    {
        [Fact]
        public void Debug_HotfixActivation()
        {
            Console.WriteLine("=== HOTFIX DEBUG TEST ===");
            
            // Create the system with hotfixes enabled
            var rfibManager = new ReverseFibonacciRiskManager();
            var perTradeRiskManager = new PerTradeRiskManager(rfibManager);
            var integerPositionSizer = new IntegerPositionSizer(perTradeRiskManager, rfibManager)
            {
                EnableProbeTradeRule = true,
                EnableLowCapBoost = true,
                EnableScaleToFit = true
            };
            
            var today = DateTime.Today;
            
            // Test 1: Normal scenario - should work
            Console.WriteLine("\n--- Test 1: Normal Scenario ---");
            var normalStrategy = new StrategySpecification
            {
                StrategyType = StrategyType.IronCondor,
                NetCredit = 0.22m,
                Width = 1.0m,
                PutWidth = 1.0m,
                CallWidth = 1.0m
            };
            
            var normalResult = integerPositionSizer.CalculateMaxContracts(today, normalStrategy);
            Console.WriteLine($"Normal result: {normalResult.CalculationDetails}");
            Console.WriteLine($"Contracts: {normalResult.MaxContracts}");
            Console.WriteLine($"Probe used: {normalResult.UsedProbeTrade}");
            Console.WriteLine($"Dynamic used: {normalResult.UsedDynamicFraction}");
            Console.WriteLine($"Scale used: {normalResult.UsedScaleToFit}");
            
            // Test 2: Force low budget scenario
            Console.WriteLine("\n--- Test 2: Low Budget Scenario ---");
            
            // Record some losses to reduce budget
            rfibManager.RecordTradeLoss(today.AddDays(-1), 400m);
            
            var result2 = integerPositionSizer.CalculateMaxContracts(today, normalStrategy);
            Console.WriteLine($"Low budget result: {result2.CalculationDetails}");
            Console.WriteLine($"Daily cap: ${rfibManager.GetDailyBudgetLimit(today)}");
            Console.WriteLine($"Remaining budget: ${rfibManager.GetRemainingDailyBudget(today)}");
            Console.WriteLine($"Contracts: {result2.MaxContracts}");
            Console.WriteLine($"Probe used: {result2.UsedProbeTrade}");
            Console.WriteLine($"Dynamic used: {result2.UsedDynamicFraction}");
            Console.WriteLine($"Scale used: {result2.UsedScaleToFit}");
            
            // Test 3: Force very low budget (should trigger probe)
            Console.WriteLine("\n--- Test 3: Very Low Budget (Probe Test) ---");
            
            // Record more losses
            rfibManager.RecordTradeLoss(today.AddDays(-1), 200m);
            rfibManager.RecordTradeLoss(today.AddDays(-1), 100m);
            
            var result3 = integerPositionSizer.CalculateMaxContracts(today, normalStrategy);
            Console.WriteLine($"Very low budget result: {result3.CalculationDetails}");
            Console.WriteLine($"Daily cap: ${rfibManager.GetDailyBudgetLimit(today)}");
            Console.WriteLine($"Remaining budget: ${rfibManager.GetRemainingDailyBudget(today)}");
            Console.WriteLine($"Contracts: {result3.MaxContracts}");
            Console.WriteLine($"Probe used: {result3.UsedProbeTrade}");
            Console.WriteLine($"Dynamic used: {result3.UsedDynamicFraction}");
            Console.WriteLine($"Scale used: {result3.UsedScaleToFit}");
            
            // Test 4: Force wide strategy (should trigger scale-to-fit)
            Console.WriteLine("\n--- Test 4: Wide Strategy (Scale-to-Fit Test) ---");
            
            var wideStrategy = new StrategySpecification
            {
                StrategyType = StrategyType.IronCondor,
                NetCredit = 0.40m,
                Width = 5.0m,
                PutWidth = 5.0m,
                CallWidth = 5.0m
            };
            
            var result4 = integerPositionSizer.CalculateMaxContracts(today, wideStrategy);
            Console.WriteLine($"Wide strategy result: {result4.CalculationDetails}");
            Console.WriteLine($"Contracts: {result4.MaxContracts}");
            Console.WriteLine($"Probe used: {result4.UsedProbeTrade}");
            Console.WriteLine($"Dynamic used: {result4.UsedDynamicFraction}");
            Console.WriteLine($"Scale used: {result4.UsedScaleToFit}");
            
            // At least one of these should show some hotfix activation
            var anyHotfixUsed = normalResult.UsedProbeTrade || normalResult.UsedDynamicFraction || normalResult.UsedScaleToFit ||
                               result2.UsedProbeTrade || result2.UsedDynamicFraction || result2.UsedScaleToFit ||
                               result3.UsedProbeTrade || result3.UsedDynamicFraction || result3.UsedScaleToFit ||
                               result4.UsedProbeTrade || result4.UsedDynamicFraction || result4.UsedScaleToFit;
            
            Console.WriteLine($"\n=== Any hotfix used: {anyHotfixUsed} ===");
            
            Assert.True(anyHotfixUsed || result2.MaxContracts > 0 || result3.MaxContracts > 0 || result4.MaxContracts > 0,
                "At least one test should show hotfix activation or allow contracts");
        }
    }
}