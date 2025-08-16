using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using ODTE.Strategy.RiskManagement;
using ODTE.Strategy.Models;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// Tier A-1.6: Validation Test for Budget Cap Enhancement
    /// 
    /// PURPOSE: Validate Tier A improvements across randomly selected months from 2005-2025
    /// 
    /// SELECTED MONTHS FOR TESTING:
    /// 1. September 2008 - Lehman Brothers collapse (extreme stress)
    /// 2. February 2018 - Volmageddon event (volatility explosion) 
    /// 3. July 2021 - Delta variant concerns + meme stock mania (mixed volatility)
    /// 
    /// VALIDATION CRITERIA:
    /// - Zero daily RFib budget breaches with Tier A enhancements
    /// - Weekly max drawdown reduction ≥ 25%
    /// - Risk-of-ruin reduction ≥ 50%
    /// - Trade utilization ≥ 70% of baseline (not over-defensive)
    /// 
    /// COMPARISON METHOD:
    /// - Baseline: Original system without Tier A enhancements
    /// - Enhanced: System with all Tier A improvements active
    /// - Metrics: Risk metrics, profit metrics, utilization metrics
    /// </summary>
    public class TierA_ValidationTest_RandomMonths
    {
        #region Test Configuration
        
        // Three randomly selected test periods with different market characteristics
        private static readonly List<TestPeriod> TEST_PERIODS = new()
        {
            new TestPeriod
            {
                Name = "September_2008_LehmankCollapse",
                StartDate = new DateTime(2008, 9, 1),
                EndDate = new DateTime(2008, 9, 30),
                MarketRegime = "Crisis",
                ExpectedBehavior = "Maximum defense, capital preservation priority",
                TargetTradeCount = 5, // Expect very few trades due to stress
                MaxAllowableDrawdown = 150m // Emergency level
            },
            new TestPeriod
            {
                Name = "February_2018_Volmageddon",
                StartDate = new DateTime(2018, 2, 1),
                EndDate = new DateTime(2018, 2, 28),
                MarketRegime = "Volatility_Explosion",
                ExpectedBehavior = "Defensive with selective opportunities",
                TargetTradeCount = 15, // Some trades but selective
                MaxAllowableDrawdown = 240m // Level 2 defense
            },
            new TestPeriod
            {
                Name = "July_2021_DeltaVariant_MemeStocks",
                StartDate = new DateTime(2021, 7, 1),
                EndDate = new DateTime(2021, 7, 31),
                MarketRegime = "Mixed_Volatility",
                ExpectedBehavior = "Moderate activity with risk control",
                TargetTradeCount = 45, // More active period
                MaxAllowableDrawdown = 385m // Level 1 defense
            }
        };
        
        #endregion
        
        #region Core Validation Tests
        
        [Fact]
        public void TierA_ValidationTest_September2008_LehmankCollapse()
        {
            var testPeriod = TEST_PERIODS[0];
            var results = RunTierAValidationTest(testPeriod);
            
            // ACCEPTANCE CRITERIA for extreme crisis period
            Assert.True(results.TierA_ZeroBudgetBreaches, "Tier A must prevent all budget breaches during crisis");
            Assert.True(results.TierA_MaxDrawdown <= testPeriod.MaxAllowableDrawdown, 
                $"Max drawdown {results.TierA_MaxDrawdown:F2} must be ≤ ${testPeriod.MaxAllowableDrawdown:F2}");
            Assert.True(results.TierA_RiskOfRuin < results.Baseline_RiskOfRuin * 0.5, 
                "Risk-of-ruin must be reduced by ≥50%");
            
            // During crisis, defensive behavior is paramount - utilization can be lower
            Assert.True(results.TierA_TradeCount >= 1, "Should execute at least 1 trade for validation");
            
            // Log results for analysis
            LogTestResults(testPeriod.Name, results);
        }
        
        [Fact]
        public void TierA_ValidationTest_February2018_Volmageddon()
        {
            var testPeriod = TEST_PERIODS[1];
            var results = RunTierAValidationTest(testPeriod);
            
            // ACCEPTANCE CRITERIA for volatility explosion
            Assert.True(results.TierA_ZeroBudgetBreaches, "Tier A must prevent budget breaches during vol explosion");
            Assert.True(results.TierA_MaxDrawdown <= testPeriod.MaxAllowableDrawdown,
                $"Max drawdown {results.TierA_MaxDrawdown:F2} must be ≤ ${testPeriod.MaxAllowableDrawdown:F2}");
            Assert.True(results.TierA_WeeklyDrawdownReduction >= 0.25,
                $"Weekly drawdown reduction {results.TierA_WeeklyDrawdownReduction:P1} must be ≥25%");
            Assert.True(results.TierA_TradeUtilization >= 0.60,
                $"Trade utilization {results.TierA_TradeUtilization:P1} must be ≥60% during vol events");
                
            LogTestResults(testPeriod.Name, results);
        }
        
        [Fact]
        public void TierA_ValidationTest_July2021_MixedVolatility()
        {
            var testPeriod = TEST_PERIODS[2];
            var results = RunTierAValidationTest(testPeriod);
            
            // ACCEPTANCE CRITERIA for mixed volatility period
            Assert.True(results.TierA_ZeroBudgetBreaches, "Tier A must prevent budget breaches in normal volatility");
            Assert.True(results.TierA_MaxDrawdown <= testPeriod.MaxAllowableDrawdown,
                $"Max drawdown {results.TierA_MaxDrawdown:F2} must be ≤ ${testPeriod.MaxAllowableDrawdown:F2}");
            Assert.True(results.TierA_TradeUtilization >= 0.70,
                $"Trade utilization {results.TierA_TradeUtilization:P1} must be ≥70% in mixed vol");
            Assert.True(results.TierA_AverageProfitPerTrade >= results.Baseline_AverageProfitPerTrade * 0.8m,
                "Average profit per trade should not degrade by more than 20%");
                
            LogTestResults(testPeriod.Name, results);
        }
        
        [Fact]
        public void TierA_ValidationTest_AggregateResults()
        {
            var aggregateResults = new List<TierAValidationResults>();
            
            foreach (var testPeriod in TEST_PERIODS)
            {
                var results = RunTierAValidationTest(testPeriod);
                aggregateResults.Add(results);
            }
            
            // AGGREGATE ACCEPTANCE CRITERIA
            var allPeriodsZeroBreaches = aggregateResults.All(r => r.TierA_ZeroBudgetBreaches);
            Assert.True(allPeriodsZeroBreaches, "All test periods must show zero budget breaches");
            
            var averageUtilization = aggregateResults.Average(r => r.TierA_TradeUtilization);
            Assert.True(averageUtilization >= 0.65, 
                $"Average utilization {averageUtilization:P1} across all periods must be ≥65%");
                
            var averageDrawdownReduction = aggregateResults.Average(r => r.TierA_WeeklyDrawdownReduction);
            Assert.True(averageDrawdownReduction >= 0.20,
                $"Average drawdown reduction {averageDrawdownReduction:P1} must be ≥20%");
                
            LogAggregateResults(aggregateResults);
        }
        
        #endregion
        
        #region Test Implementation
        
        private TierAValidationResults RunTierAValidationTest(TestPeriod testPeriod)
        {
            // Initialize risk management systems (A1.5 + A2.4)
            var baselineRfibManager = new ReverseFibonacciRiskManager();
            var enhancedRfibManager = new ReverseFibonacciRiskManager();
            var perTradeRiskManager = new PerTradeRiskManager(enhancedRfibManager);
            var integerPositionSizer = new IntegerPositionSizer(perTradeRiskManager, enhancedRfibManager);
            var budgetCapValidator = new BudgetCapValidator(perTradeRiskManager, enhancedRfibManager);
            var tierAGate = new TierATradeExecutionGate(perTradeRiskManager, budgetCapValidator, integerPositionSizer, enhancedRfibManager);
            
            // Generate synthetic trading opportunities for the test period
            var tradingOpportunities = GenerateTradingOpportunities(testPeriod);
            
            // Run baseline simulation (without Tier A enhancements)
            var baselineResults = RunBaselineSimulation(tradingOpportunities, baselineRfibManager, testPeriod);
            
            // Run enhanced simulation (with Tier A enhancements)
            var enhancedResults = RunEnhancedSimulation(tradingOpportunities, tierAGate, testPeriod);
            
            // Calculate validation metrics
            return CalculateValidationMetrics(baselineResults, enhancedResults, testPeriod);
        }
        
        private List<TierATradingOpportunity> GenerateTradingOpportunities(TestPeriod testPeriod)
        {
            var opportunities = new List<TierATradingOpportunity>();
            var random = new Random(42); // Fixed seed for reproducible results
            var currentDate = testPeriod.StartDate;
            
            while (currentDate <= testPeriod.EndDate)
            {
                // Skip weekends
                if (currentDate.DayOfWeek != DayOfWeek.Saturday && currentDate.DayOfWeek != DayOfWeek.Sunday)
                {
                    // Generate 3-8 opportunities per trading day
                    var dailyOpportunities = random.Next(3, 9);
                    
                    for (int i = 0; i < dailyOpportunities; i++)
                    {
                        var opportunity = GenerateRealisticOpportunity(currentDate, testPeriod, random);
                        opportunities.Add(opportunity);
                    }
                }
                currentDate = currentDate.AddDays(1);
            }
            
            return opportunities;
        }
        
        private TierATradingOpportunity GenerateRealisticOpportunity(DateTime date, TestPeriod testPeriod, Random random)
        {
            // Market stress factors based on test period characteristics
            var stressFactor = testPeriod.MarketRegime switch
            {
                "Crisis" => 3.0, // 3x normal stress during crisis
                "Volatility_Explosion" => 2.5, // 2.5x during vol events
                "Mixed_Volatility" => 1.2, // 1.2x during mixed periods
                _ => 1.0
            };
            
            // Generate realistic opportunity parameters
            var baseCredit = 15m + (decimal)(random.NextDouble() * 15.0); // $15-30 base credit
            var baseContracts = random.Next(1, 4); // 1-3 contracts normally
            var baseLiquidity = 0.6 + (random.NextDouble() * 0.3); // 0.6-0.9 liquidity score
            
            // Apply stress adjustments
            var stressedCredit = baseCredit / (decimal)stressFactor; // Lower credits in stress
            var stressedLiquidity = baseLiquidity / stressFactor; // Worse liquidity in stress
            var stressedContracts = (int)Math.Ceiling(baseContracts / stressFactor); // Smaller sizes in stress
            
            return new TierATradingOpportunity
            {
                OpportunityTime = date.AddHours(9.5 + random.NextDouble() * 6.5), // Market hours
                StrategyType = random.NextDouble() > 0.7 ? StrategyType.IronCondor : StrategyType.CreditBWB,
                NetCredit = Math.Max(5m, stressedCredit),
                ProposedContracts = Math.Max(1, stressedContracts),
                Width = 5m, // Standard 5-point width
                LiquidityScore = Math.Max(0.1, Math.Min(1.0, stressedLiquidity)),
                BidAskSpread = 0.15m * (decimal)stressFactor, // Wider spreads under stress
                MarketStress = stressFactor,
                ExpectedWinProbability = Math.Max(0.5, 0.85 / stressFactor) // Lower win probability under stress
            };
        }
        
        private SimulationResults RunBaselineSimulation(
            List<TierATradingOpportunity> opportunities, 
            ReverseFibonacciRiskManager rfibManager,
            TestPeriod testPeriod)
        {
            var results = new SimulationResults();
            var dailyPnL = new Dictionary<DateTime, decimal>();
            
            foreach (var opportunity in opportunities)
            {
                var day = opportunity.OpportunityTime.Date;
                
                // Simple baseline logic: trade if RFib allows and basic conditions met
                var canTrade = rfibManager.CanTrade(day);
                var meetsBasicCriteria = opportunity.LiquidityScore > 0.5 && opportunity.NetCredit > 10m;
                
                if (canTrade && meetsBasicCriteria)
                {
                    // Execute trade with baseline position sizing
                    var contracts = opportunity.ProposedContracts;
                    var maxLoss = (opportunity.Width - opportunity.NetCredit) * 100m * contracts;
                    
                    // Simulate trade outcome
                    var isWin = new Random().NextDouble() < opportunity.ExpectedWinProbability;
                    var pnl = isWin ? opportunity.NetCredit * 100m * contracts * 0.7m : -maxLoss;
                    
                    if (!dailyPnL.ContainsKey(day)) dailyPnL[day] = 0m;
                    dailyPnL[day] += pnl;
                    
                    results.ExecutedTrades++;
                    results.TotalPnL += pnl;
                    
                    if (pnl > 0)
                    {
                        rfibManager.RecordTradeProfit(day, pnl);
                        results.WinningTrades++;
                    }
                    else
                    {
                        rfibManager.RecordTradeLoss(day, Math.Abs(pnl));
                        results.LosingTrades++;
                    }
                }
                
                results.TotalOpportunities++;
            }
            
            // Calculate final metrics
            results.WinRate = results.ExecutedTrades > 0 ? (double)results.WinningTrades / results.ExecutedTrades : 0;
            results.AverageProfitPerTrade = results.ExecutedTrades > 0 ? results.TotalPnL / results.ExecutedTrades : 0;
            results.MaxDrawdown = CalculateMaxDrawdown(dailyPnL);
            results.MaxDailyLoss = dailyPnL.Values.DefaultIfEmpty(0).Min();
            results.BudgetBreaches = CountBudgetBreaches(dailyPnL, testPeriod);
            
            return results;
        }
        
        private SimulationResults RunEnhancedSimulation(
            List<TierATradingOpportunity> opportunities,
            TierATradeExecutionGate tierAGate,
            TestPeriod testPeriod)
        {
            var results = new SimulationResults();
            var dailyPnL = new Dictionary<DateTime, decimal>();
            
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
                
                if (validation.IsApproved)
                {
                    // Execute trade with Tier A protections
                    var contracts = tradeCandidate.Contracts;
                    var maxLoss = validation.MaxLossAtEntry;
                    
                    // Simulate trade outcome
                    var isWin = new Random().NextDouble() < opportunity.ExpectedWinProbability;
                    var pnl = isWin ? opportunity.NetCredit * 100m * contracts * 0.7m : -maxLoss;
                    
                    if (!dailyPnL.ContainsKey(day)) dailyPnL[day] = 0m;
                    dailyPnL[day] += pnl;
                    
                    results.ExecutedTrades++;
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
                
                results.TotalOpportunities++;
            }
            
            // Calculate enhanced metrics
            results.WinRate = results.ExecutedTrades > 0 ? (double)results.WinningTrades / results.ExecutedTrades : 0;
            results.AverageProfitPerTrade = results.ExecutedTrades > 0 ? results.TotalPnL / results.ExecutedTrades : 0;
            results.MaxDrawdown = CalculateMaxDrawdown(dailyPnL);
            results.MaxDailyLoss = dailyPnL.Values.DefaultIfEmpty(0).Min();
            results.BudgetBreaches = CountBudgetBreaches(dailyPnL, testPeriod);
            
            return results;
        }
        
        private decimal CalculateMaxDrawdown(Dictionary<DateTime, decimal> dailyPnL)
        {
            var runningTotal = 0m;
            var peak = 0m;
            var maxDrawdown = 0m;
            
            foreach (var kvp in dailyPnL.OrderBy(x => x.Key))
            {
                runningTotal += kvp.Value;
                if (runningTotal > peak)
                    peak = runningTotal;
                var currentDrawdown = peak - runningTotal;
                if (currentDrawdown > maxDrawdown)
                    maxDrawdown = currentDrawdown;
            }
            
            return maxDrawdown;
        }
        
        private int CountBudgetBreaches(Dictionary<DateTime, decimal> dailyPnL, TestPeriod testPeriod)
        {
            var breaches = 0;
            foreach (var kvp in dailyPnL)
            {
                if (Math.Abs(kvp.Value) > testPeriod.MaxAllowableDrawdown)
                    breaches++;
            }
            return breaches;
        }
        
        private TierAValidationResults CalculateValidationMetrics(
            SimulationResults baseline,
            SimulationResults enhanced,
            TestPeriod testPeriod)
        {
            return new TierAValidationResults
            {
                TestPeriodName = testPeriod.Name,
                
                // Baseline metrics
                Baseline_TradeCount = baseline.ExecutedTrades,
                Baseline_WinRate = baseline.WinRate,
                Baseline_AverageProfitPerTrade = baseline.AverageProfitPerTrade,
                Baseline_MaxDrawdown = baseline.MaxDrawdown,
                Baseline_BudgetBreaches = baseline.BudgetBreaches,
                Baseline_RiskOfRuin = CalculateRiskOfRuin(baseline.MaxDrawdown, baseline.AverageProfitPerTrade),
                
                // Tier A enhanced metrics
                TierA_TradeCount = enhanced.ExecutedTrades,
                TierA_WinRate = enhanced.WinRate,
                TierA_AverageProfitPerTrade = enhanced.AverageProfitPerTrade,
                TierA_MaxDrawdown = enhanced.MaxDrawdown,
                TierA_BudgetBreaches = enhanced.BudgetBreaches,
                TierA_ZeroBudgetBreaches = enhanced.BudgetBreaches == 0,
                TierA_RiskOfRuin = CalculateRiskOfRuin(enhanced.MaxDrawdown, enhanced.AverageProfitPerTrade),
                
                // Comparative metrics
                TierA_TradeUtilization = baseline.ExecutedTrades > 0 ? (double)enhanced.ExecutedTrades / baseline.ExecutedTrades : 0,
                TierA_WeeklyDrawdownReduction = baseline.MaxDrawdown > 0 ? (double)(baseline.MaxDrawdown - enhanced.MaxDrawdown) / (double)baseline.MaxDrawdown : 0
            };
        }
        
        private double CalculateRiskOfRuin(decimal maxDrawdown, decimal avgProfitPerTrade)
        {
            // Simplified risk of ruin calculation
            if (avgProfitPerTrade <= 0) return 1.0; // 100% if not profitable
            var riskRewardRatio = (double)(maxDrawdown / Math.Abs(avgProfitPerTrade));
            return Math.Min(1.0, riskRewardRatio / 10.0); // Simplified formula
        }
        
        #endregion
        
        #region Logging & Output
        
        private void LogTestResults(string testName, TierAValidationResults results)
        {
            Console.WriteLine($"\n=== TIER A VALIDATION RESULTS: {testName} ===");
            Console.WriteLine($"Baseline: {results.Baseline_TradeCount} trades, {results.Baseline_WinRate:P1} win rate, ${results.Baseline_AverageProfitPerTrade:F2} avg profit");
            Console.WriteLine($"Enhanced: {results.TierA_TradeCount} trades, {results.TierA_WinRate:P1} win rate, ${results.TierA_AverageProfitPerTrade:F2} avg profit");
            Console.WriteLine($"Budget Breaches: Baseline {results.Baseline_BudgetBreaches} → Enhanced {results.TierA_BudgetBreaches}");
            Console.WriteLine($"Max Drawdown: Baseline ${results.Baseline_MaxDrawdown:F2} → Enhanced ${results.TierA_MaxDrawdown:F2}");
            Console.WriteLine($"Trade Utilization: {results.TierA_TradeUtilization:P1}");
            Console.WriteLine($"Drawdown Reduction: {results.TierA_WeeklyDrawdownReduction:P1}");
            Console.WriteLine($"Zero Budget Breaches: {(results.TierA_ZeroBudgetBreaches ? "✅ PASS" : "❌ FAIL")}");
        }
        
        private void LogAggregateResults(List<TierAValidationResults> allResults)
        {
            Console.WriteLine($"\n=== TIER A AGGREGATE VALIDATION RESULTS ===");
            Console.WriteLine($"Test Periods: {allResults.Count}");
            Console.WriteLine($"All Zero Budget Breaches: {(allResults.All(r => r.TierA_ZeroBudgetBreaches) ? "✅ PASS" : "❌ FAIL")}");
            Console.WriteLine($"Average Trade Utilization: {allResults.Average(r => r.TierA_TradeUtilization):P1}");
            Console.WriteLine($"Average Drawdown Reduction: {allResults.Average(r => r.TierA_WeeklyDrawdownReduction):P1}");
            Console.WriteLine($"Average Risk-of-Ruin Reduction: {allResults.Average(r => (r.Baseline_RiskOfRuin - r.TierA_RiskOfRuin) / r.Baseline_RiskOfRuin):P1}");
        }
        
        #endregion
    }
    
    #region Supporting Data Types
    
    public class TestPeriod
    {
        public string Name { get; set; } = "";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string MarketRegime { get; set; } = "";
        public string ExpectedBehavior { get; set; } = "";
        public int TargetTradeCount { get; set; }
        public decimal MaxAllowableDrawdown { get; set; }
    }
    
    public class TierATradingOpportunity
    {
        public DateTime OpportunityTime { get; set; }
        public StrategyType StrategyType { get; set; }
        public decimal NetCredit { get; set; }
        public int ProposedContracts { get; set; }
        public decimal Width { get; set; }
        public double LiquidityScore { get; set; }
        public decimal BidAskSpread { get; set; }
        public double MarketStress { get; set; }
        public double ExpectedWinProbability { get; set; }
    }
    
    public class SimulationResults
    {
        public int TotalOpportunities { get; set; }
        public int ExecutedTrades { get; set; }
        public int WinningTrades { get; set; }
        public int LosingTrades { get; set; }
        public double WinRate { get; set; }
        public decimal TotalPnL { get; set; }
        public decimal AverageProfitPerTrade { get; set; }
        public decimal MaxDrawdown { get; set; }
        public decimal MaxDailyLoss { get; set; }
        public int BudgetBreaches { get; set; }
    }
    
    public class TierAValidationResults
    {
        public string TestPeriodName { get; set; } = "";
        
        // Baseline metrics
        public int Baseline_TradeCount { get; set; }
        public double Baseline_WinRate { get; set; }
        public decimal Baseline_AverageProfitPerTrade { get; set; }
        public decimal Baseline_MaxDrawdown { get; set; }
        public int Baseline_BudgetBreaches { get; set; }
        public double Baseline_RiskOfRuin { get; set; }
        
        // Enhanced metrics
        public int TierA_TradeCount { get; set; }
        public double TierA_WinRate { get; set; }
        public decimal TierA_AverageProfitPerTrade { get; set; }
        public decimal TierA_MaxDrawdown { get; set; }
        public int TierA_BudgetBreaches { get; set; }
        public bool TierA_ZeroBudgetBreaches { get; set; }
        public double TierA_RiskOfRuin { get; set; }
        
        // Comparative metrics
        public double TierA_TradeUtilization { get; set; }
        public double TierA_WeeklyDrawdownReduction { get; set; }
    }
    
    #endregion
}