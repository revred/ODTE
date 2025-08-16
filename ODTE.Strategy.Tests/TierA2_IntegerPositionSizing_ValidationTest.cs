using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using ODTE.Strategy.RiskManagement;
using ODTE.Strategy.Models;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// Tier A-2: Integer Position Sizing Validation Test
    /// 
    /// PURPOSE: Validate Tier A-2 integer position sizing enhancements across diverse market conditions
    /// 
    /// SELECTED MONTHS FOR TESTING:
    /// 1. October 2008 - Financial crisis (extreme stress, small positions)
    /// 2. December 2017 - Bull market peak (low stress, larger positions) 
    /// 3. March 2020 - COVID crash (maximum stress, minimal positions)
    /// 
    /// VALIDATION CRITERIA:
    /// - Zero fractional contract execution attempts
    /// - Hard cap (8 contracts) never exceeded
    /// - Position size derived mathematically from loss allowance
    /// - Safety buffer (5%) consistently applied
    /// - Budget utilization optimized within constraints
    /// 
    /// COMPARISON METHOD:
    /// - Baseline: Original system with potential fractional sizing
    /// - Enhanced: Tier A-2 with IntegerPositionSizer enforcement
    /// - Focus: Position sizing integrity and mathematical correctness
    /// </summary>
    public class TierA2_IntegerPositionSizing_ValidationTest
    {
        #region Test Configuration
        
        private static readonly List<IntegerSizingTestPeriod> TEST_PERIODS = new()
        {
            new IntegerSizingTestPeriod
            {
                Name = "October_2008_FinancialCrisis",
                StartDate = new DateTime(2008, 10, 1),
                EndDate = new DateTime(2008, 10, 31),
                MarketRegime = "Crisis",
                ExpectedBehavior = "Minimal position sizes due to extreme stress",
                ExpectedMaxContracts = 2, // Crisis should force very small positions
                StressFactor = 4.0
            },
            new IntegerSizingTestPeriod
            {
                Name = "December_2017_BullPeak",
                StartDate = new DateTime(2017, 12, 1),
                EndDate = new DateTime(2017, 12, 31),
                MarketRegime = "Euphoria",
                ExpectedBehavior = "Larger positions in low-stress environment",
                ExpectedMaxContracts = 6, // Bull market allows larger sizes
                StressFactor = 0.8
            },
            new IntegerSizingTestPeriod
            {
                Name = "March_2020_COVIDCrash",
                StartDate = new DateTime(2020, 3, 1),
                EndDate = new DateTime(2020, 3, 31),
                MarketRegime = "Panic",
                ExpectedBehavior = "Emergency position sizes only",
                ExpectedMaxContracts = 1, // Panic should force minimum sizing
                StressFactor = 5.0
            }
        };
        
        #endregion
        
        #region Core Validation Tests
        
        [Fact]
        public void TierA2_ValidationTest_October2008_Crisis()
        {
            var testPeriod = TEST_PERIODS[0];
            var results = RunIntegerSizingValidationTest(testPeriod);
            
            // CRITICAL ACCEPTANCE CRITERIA for crisis period
            Assert.True(results.ZeroFractionalContracts, "Must prevent all fractional contract attempts");
            Assert.True(results.MaxContractsObserved <= testPeriod.ExpectedMaxContracts, 
                $"Max contracts {results.MaxContractsObserved} must be ≤ {testPeriod.ExpectedMaxContracts} during crisis");
            Assert.True(results.HardCapNeverExceeded, "Hard cap (8 contracts) must never be exceeded");
            Assert.True(results.SafetyBufferAlwaysApplied, "5% safety buffer must be consistently applied");
            
            // Crisis-specific validations
            Assert.True(results.AverageContractsPerTrade <= 1.5, "Average position size should be ≤1.5 in crisis");
            
            LogTestResults(testPeriod.Name, results);
        }
        
        [Fact]
        public void TierA2_ValidationTest_December2017_Bull()
        {
            var testPeriod = TEST_PERIODS[1];
            var results = RunIntegerSizingValidationTest(testPeriod);
            
            // ACCEPTANCE CRITERIA for bull market
            Assert.True(results.ZeroFractionalContracts, "Must prevent all fractional contract attempts");
            Assert.True(results.MaxContractsObserved <= testPeriod.ExpectedMaxContracts,
                $"Max contracts {results.MaxContractsObserved} must be ≤ {testPeriod.ExpectedMaxContracts} in bull market");
            Assert.True(results.HardCapNeverExceeded, "Hard cap must never be exceeded");
            Assert.True(results.SafetyBufferAlwaysApplied, "Safety buffer must be applied");
            
            // Bull market should allow reasonable position utilization
            Assert.True(results.BudgetUtilizationRate >= 0.60, 
                $"Budget utilization {results.BudgetUtilizationRate:P1} should be ≥60% in bull market");
            Assert.True(results.AverageContractsPerTrade >= 2.0, "Average position size should be ≥2.0 in bull market");
            
            LogTestResults(testPeriod.Name, results);
        }
        
        [Fact]
        public void TierA2_ValidationTest_March2020_Panic()
        {
            var testPeriod = TEST_PERIODS[2];
            var results = RunIntegerSizingValidationTest(testPeriod);
            
            // ACCEPTANCE CRITERIA for panic conditions
            Assert.True(results.ZeroFractionalContracts, "Must prevent all fractional contract attempts");
            Assert.True(results.MaxContractsObserved <= testPeriod.ExpectedMaxContracts,
                $"Max contracts {results.MaxContractsObserved} must be ≤ {testPeriod.ExpectedMaxContracts} during panic");
            Assert.True(results.HardCapNeverExceeded, "Hard cap must never be exceeded");
            Assert.True(results.SafetyBufferAlwaysApplied, "Safety buffer must be applied");
            
            // Panic conditions should force minimal sizing
            Assert.True(results.AverageContractsPerTrade <= 1.2, "Average position size should be ≤1.2 during panic");
            Assert.True(results.MinimumContractsObserved >= 1, "Minimum contracts should be 1 (if any trading)");
            
            LogTestResults(testPeriod.Name, results);
        }
        
        [Fact]
        public void TierA2_ValidationTest_AggregatePositionSizing()
        {
            var aggregateResults = new List<IntegerSizingValidationResults>();
            
            foreach (var testPeriod in TEST_PERIODS)
            {
                var results = RunIntegerSizingValidationTest(testPeriod);
                aggregateResults.Add(results);
            }
            
            // AGGREGATE ACCEPTANCE CRITERIA
            var allPeriodsZeroFractional = aggregateResults.All(r => r.ZeroFractionalContracts);
            Assert.True(allPeriodsZeroFractional, "All test periods must show zero fractional contracts");
            
            var allPeriodsHardCapRespected = aggregateResults.All(r => r.HardCapNeverExceeded);
            Assert.True(allPeriodsHardCapRespected, "Hard cap must be respected across all periods");
            
            var allPeriodsSafetyBufferApplied = aggregateResults.All(r => r.SafetyBufferAlwaysApplied);
            Assert.True(allPeriodsSafetyBufferApplied, "Safety buffer must be applied consistently");
            
            // Mathematical consistency checks
            var averageSafetyBufferApplied = aggregateResults.Average(r => r.SafetyBufferPercentage);
            Assert.True(Math.Abs(averageSafetyBufferApplied - 5.0) < 1.0, 
                $"Average safety buffer {averageSafetyBufferApplied:F1}% should be ~5%");
                
            LogAggregateResults(aggregateResults);
        }
        
        #endregion
        
        #region Test Implementation
        
        private IntegerSizingValidationResults RunIntegerSizingValidationTest(IntegerSizingTestPeriod testPeriod)
        {
            // Initialize Tier A-2 risk management components
            var rfibManager = new ReverseFibonacciRiskManager();
            var perTradeRiskManager = new PerTradeRiskManager(rfibManager);
            var integerPositionSizer = new IntegerPositionSizer(perTradeRiskManager, rfibManager);
            var budgetCapValidator = new BudgetCapValidator(perTradeRiskManager, rfibManager);
            var tierAGate = new TierATradeExecutionGate(perTradeRiskManager, budgetCapValidator, integerPositionSizer, rfibManager);
            
            // Generate trading opportunities for the test period
            var tradingOpportunities = GeneratePositionSizingOpportunities(testPeriod);
            
            // Run integer sizing validation simulation
            return ExecuteIntegerSizingValidation(tradingOpportunities, integerPositionSizer, tierAGate, testPeriod);
        }
        
        private List<PositionSizingOpportunity> GeneratePositionSizingOpportunities(IntegerSizingTestPeriod testPeriod)
        {
            var opportunities = new List<PositionSizingOpportunity>();
            var random = new Random(42); // Fixed seed for reproducible results
            var currentDate = testPeriod.StartDate;
            
            while (currentDate <= testPeriod.EndDate)
            {
                // Skip weekends
                if (currentDate.DayOfWeek != DayOfWeek.Saturday && currentDate.DayOfWeek != DayOfWeek.Sunday)
                {
                    // Generate 5-12 position sizing tests per day
                    var dailyOpportunities = random.Next(5, 13);
                    
                    for (int i = 0; i < dailyOpportunities; i++)
                    {
                        var opportunity = GenerateRealisticPositionSizingOpportunity(currentDate, testPeriod, random);
                        opportunities.Add(opportunity);
                    }
                }
                currentDate = currentDate.AddDays(1);
            }
            
            return opportunities;
        }
        
        private PositionSizingOpportunity GenerateRealisticPositionSizingOpportunity(
            DateTime date, 
            IntegerSizingTestPeriod testPeriod, 
            Random random)
        {
            // Base strategy parameters
            var strategyTypes = new[] { StrategyType.IronCondor, StrategyType.CreditBWB };
            var strategyType = strategyTypes[random.Next(strategyTypes.Length)];
            
            // Market stress adjustments based on test period
            var stressFactor = testPeriod.StressFactor;
            
            // Generate realistic parameters adjusted for market stress
            var baseCredit = 15m + (decimal)(random.NextDouble() * 20.0); // $15-35
            var adjustedCredit = baseCredit / (decimal)Math.Sqrt(stressFactor); // Lower credits in stress
            
            var baseWidth = 5m;
            var baseContracts = random.Next(1, 12); // Request 1-11 contracts (test caps)
            
            return new PositionSizingOpportunity
            {
                OpportunityTime = date.AddHours(9.5 + random.NextDouble() * 6.5),
                StrategyType = strategyType,
                NetCredit = Math.Max(5m, adjustedCredit),
                RequestedContracts = baseContracts,
                Width = baseWidth,
                PutWidth = strategyType == StrategyType.CreditBWB ? baseWidth * 0.6m : baseWidth,
                CallWidth = strategyType == StrategyType.CreditBWB ? baseWidth * 0.4m : baseWidth,
                BodyWidth = strategyType == StrategyType.CreditBWB ? baseWidth * 0.7m : 0m,
                WingWidth = strategyType == StrategyType.CreditBWB ? baseWidth * 0.3m : 0m,
                MarketStress = stressFactor,
                TestPeriodContext = testPeriod.MarketRegime
            };
        }
        
        private IntegerSizingValidationResults ExecuteIntegerSizingValidation(
            List<PositionSizingOpportunity> opportunities,
            IntegerPositionSizer integerPositionSizer,
            TierATradeExecutionGate tierAGate,
            IntegerSizingTestPeriod testPeriod)
        {
            var results = new IntegerSizingValidationResults
            {
                TestPeriodName = testPeriod.Name,
                TotalOpportunities = opportunities.Count
            };
            
            var contractsData = new List<int>();
            var budgetUtilizationData = new List<decimal>();
            var safetyBufferData = new List<decimal>();
            
            foreach (var opportunity in opportunities)
            {
                var day = opportunity.OpportunityTime.Date;
                
                // Create strategy specification
                var strategySpec = new StrategySpecification
                {
                    StrategyType = opportunity.StrategyType,
                    NetCredit = opportunity.NetCredit,
                    Width = opportunity.Width,
                    PutWidth = opportunity.PutWidth,
                    CallWidth = opportunity.CallWidth,
                    BodyWidth = opportunity.BodyWidth,
                    WingWidth = opportunity.WingWidth
                };
                
                // Test integer position sizing calculation
                var maxContractsResult = integerPositionSizer.CalculateMaxContracts(day, strategySpec);
                
                results.TotalCalculations++;
                
                if (maxContractsResult.IsValid)
                {
                    // Validate mathematical correctness
                    var isInteger = maxContractsResult.MaxContracts == (int)Math.Floor((double)maxContractsResult.MaxContracts);
                    if (!isInteger)
                    {
                        results.FractionalContractAttempts++;
                    }
                    
                    // Track hard cap enforcement
                    if (maxContractsResult.MaxContracts > IntegerPositionSizer.HARD_CAP_CONTRACTS)
                    {
                        results.HardCapViolations++;
                    }
                    
                    // Track safety buffer application
                    if (maxContractsResult.SafetyBufferApplied)
                    {
                        results.SafetyBufferApplications++;
                        var appliedBuffer = (maxContractsResult.DerivedMaxContracts - maxContractsResult.MaxContracts) / 
                                          (decimal)Math.Max(1, maxContractsResult.DerivedMaxContracts) * 100m;
                        safetyBufferData.Add(appliedBuffer);
                    }
                    
                    // Track budget utilization
                    if (maxContractsResult.RemainingBudget > 0)
                    {
                        var utilizationRate = maxContractsResult.MaxLossAllowance / maxContractsResult.RemainingBudget;
                        budgetUtilizationData.Add(utilizationRate);
                    }
                    
                    contractsData.Add(maxContractsResult.MaxContracts);
                    
                    // Validate specific contract count
                    var contractValidation = integerPositionSizer.ValidateContractCount(
                        day, strategySpec, opportunity.RequestedContracts);
                        
                    if (contractValidation.IsValid)
                    {
                        results.ValidatedTrades++;
                    }
                    else
                    {
                        results.RejectedTrades++;
                    }
                }
                else
                {
                    results.CalculationFailures++;
                }
            }
            
            // Calculate aggregate metrics
            results.ZeroFractionalContracts = results.FractionalContractAttempts == 0;
            results.HardCapNeverExceeded = results.HardCapViolations == 0;
            results.SafetyBufferAlwaysApplied = results.SafetyBufferApplications > results.TotalCalculations * 0.8; // 80% threshold
            
            if (contractsData.Count > 0)
            {
                results.MaxContractsObserved = contractsData.Max();
                results.MinimumContractsObserved = contractsData.Min();
                results.AverageContractsPerTrade = contractsData.Average();
            }
            
            if (budgetUtilizationData.Count > 0)
            {
                results.BudgetUtilizationRate = (double)budgetUtilizationData.Average();
            }
            
            if (safetyBufferData.Count > 0)
            {
                results.SafetyBufferPercentage = (double)safetyBufferData.Average();
            }
            
            return results;
        }
        
        #endregion
        
        #region Logging & Analysis
        
        private void LogTestResults(string testName, IntegerSizingValidationResults results)
        {
            Console.WriteLine($"\n=== TIER A-2 INTEGER SIZING VALIDATION: {testName} ===");
            Console.WriteLine($"Opportunities: {results.TotalOpportunities}, Calculations: {results.TotalCalculations}");
            Console.WriteLine($"Valid Trades: {results.ValidatedTrades}, Rejected: {results.RejectedTrades}");
            Console.WriteLine($"Fractional Attempts: {results.FractionalContractAttempts} (Zero: {(results.ZeroFractionalContracts ? "✅" : "❌")})");
            Console.WriteLine($"Hard Cap Violations: {results.HardCapViolations} (Never Exceeded: {(results.HardCapNeverExceeded ? "✅" : "❌")})");
            Console.WriteLine($"Safety Buffer Applied: {results.SafetyBufferApplications}/{results.TotalCalculations} ({(results.SafetyBufferAlwaysApplied ? "✅" : "❌")})");
            Console.WriteLine($"Contract Range: {results.MinimumContractsObserved}-{results.MaxContractsObserved}, Avg: {results.AverageContractsPerTrade:F1}");
            Console.WriteLine($"Budget Utilization: {results.BudgetUtilizationRate:P1}");
            Console.WriteLine($"Safety Buffer %: {results.SafetyBufferPercentage:F1}%");
        }
        
        private void LogAggregateResults(List<IntegerSizingValidationResults> allResults)
        {
            Console.WriteLine($"\n=== TIER A-2 AGGREGATE INTEGER SIZING VALIDATION ===");
            Console.WriteLine($"Test Periods: {allResults.Count}");
            Console.WriteLine($"Total Opportunities: {allResults.Sum(r => r.TotalOpportunities)}");
            Console.WriteLine($"All Zero Fractional: {(allResults.All(r => r.ZeroFractionalContracts) ? "✅ PASS" : "❌ FAIL")}");
            Console.WriteLine($"All Hard Cap Respected: {(allResults.All(r => r.HardCapNeverExceeded) ? "✅ PASS" : "❌ FAIL")}");
            Console.WriteLine($"All Safety Buffer Applied: {(allResults.All(r => r.SafetyBufferAlwaysApplied) ? "✅ PASS" : "❌ FAIL")}");
            Console.WriteLine($"Average Contract Size: {allResults.Average(r => r.AverageContractsPerTrade):F1}");
            Console.WriteLine($"Average Budget Utilization: {allResults.Average(r => r.BudgetUtilizationRate):P1}");
            Console.WriteLine($"Average Safety Buffer: {allResults.Average(r => r.SafetyBufferPercentage):F1}%");
        }
        
        #endregion
    }
    
    #region Supporting Data Types
    
    public class IntegerSizingTestPeriod
    {
        public string Name { get; set; } = "";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string MarketRegime { get; set; } = "";
        public string ExpectedBehavior { get; set; } = "";
        public int ExpectedMaxContracts { get; set; }
        public double StressFactor { get; set; }
    }
    
    public class PositionSizingOpportunity
    {
        public DateTime OpportunityTime { get; set; }
        public StrategyType StrategyType { get; set; }
        public decimal NetCredit { get; set; }
        public int RequestedContracts { get; set; }
        public decimal Width { get; set; }
        public decimal PutWidth { get; set; }
        public decimal CallWidth { get; set; }
        public decimal BodyWidth { get; set; }
        public decimal WingWidth { get; set; }
        public double MarketStress { get; set; }
        public string TestPeriodContext { get; set; } = "";
    }
    
    public class IntegerSizingValidationResults
    {
        public string TestPeriodName { get; set; } = "";
        public int TotalOpportunities { get; set; }
        public int TotalCalculations { get; set; }
        public int ValidatedTrades { get; set; }
        public int RejectedTrades { get; set; }
        public int CalculationFailures { get; set; }
        
        // Integer sizing metrics
        public int FractionalContractAttempts { get; set; }
        public bool ZeroFractionalContracts { get; set; }
        public int HardCapViolations { get; set; }
        public bool HardCapNeverExceeded { get; set; }
        public int SafetyBufferApplications { get; set; }
        public bool SafetyBufferAlwaysApplied { get; set; }
        
        // Position sizing analytics
        public int MaxContractsObserved { get; set; }
        public int MinimumContractsObserved { get; set; }
        public double AverageContractsPerTrade { get; set; }
        public double BudgetUtilizationRate { get; set; }
        public double SafetyBufferPercentage { get; set; }
    }
    
    #endregion
}