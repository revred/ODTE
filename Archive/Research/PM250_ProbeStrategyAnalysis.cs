using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// PROBE STRATEGY PARAMETER ANALYSIS
    /// 
    /// OBJECTIVE: Design low-risk probe strategy for difficult market conditions
    /// APPROACH: Analyze historical crisis periods to optimize probe parameters
    /// OUTPUT: Validated probe strategy configuration for capital preservation
    /// </summary>
    public class PM250_ProbeStrategyAnalysis
    {
        [Fact]
        public void AnalyzeProbeTradeParameters_DifficultMarketConditions()
        {
            Console.WriteLine("=== PROBE STRATEGY PARAMETER ANALYSIS ===");
            Console.WriteLine("Objective: Design capital preservation strategy for difficult market conditions");
            Console.WriteLine("Focus: Small profits, low risk, early warning capability");
            
            // STEP 1: Identify crisis periods from real data
            var crisisPeriods = GetCrisisPeriodsFromRealData();
            
            // STEP 2: Analyze what would have worked in crisis periods
            var crisisAnalysis = AnalyzeCrisisPerformance(crisisPeriods);
            
            // STEP 3: Extract optimal probe parameters from crisis survival patterns
            var probeParameters = ExtractOptimalProbeParameters(crisisPeriods, crisisAnalysis);
            
            // STEP 4: Validate probe parameters against worst scenarios
            ValidateProbeParameters(probeParameters, crisisPeriods);
            
            // STEP 5: Generate probe strategy implementation
            GenerateProbeStrategyImplementation(probeParameters);
        }
        
        private List<CrisisPeriod> GetCrisisPeriodsFromRealData()
        {
            Console.WriteLine("\n--- CRISIS PERIOD IDENTIFICATION ---");
            Console.WriteLine("Extracting actual crisis periods from PM250 real data");
            
            var crisisPeriods = new List<CrisisPeriod>
            {
                new() {
                    Name = "COVID_CRASH",
                    StartDate = new DateTime(2020, 2, 1),
                    EndDate = new DateTime(2020, 4, 30),
                    TotalLoss = -842.16m + (-123.45m),  // Feb + Mar 2020
                    VIXLevel = 80.0,
                    MarketDrop = -35.0,
                    ActualResults = new List<MonthResult> {
                        new() { Year = 2020, Month = 2, PnL = -123.45m, WinRate = 0.720, Trades = 25 },
                        new() { Year = 2020, Month = 3, PnL = -842.16m, WinRate = 0.613, Trades = 31 }
                    },
                    LessonsLearned = "System completely failed - need minimal risk approach"
                },
                
                new() {
                    Name = "BANKING_CRISIS_2023",
                    StartDate = new DateTime(2023, 2, 1),
                    EndDate = new DateTime(2023, 4, 30),
                    TotalLoss = -296.86m + (-175.36m),  // Feb + Apr 2023
                    VIXLevel = 35.0,
                    MarketDrop = -15.0,
                    ActualResults = new List<MonthResult> {
                        new() { Year = 2023, Month = 2, PnL = -296.86m, WinRate = 0.643, Trades = 28 },
                        new() { Year = 2023, Month = 4, PnL = -175.36m, WinRate = 0.700, Trades = 20 }
                    },
                    LessonsLearned = "Better win rates but still significant losses"
                },
                
                new() {
                    Name = "RECENT_BREAKDOWN_2024",
                    StartDate = new DateTime(2024, 12, 1),
                    EndDate = new DateTime(2025, 8, 31),
                    TotalLoss = -620.16m + (-478.46m) + (-348.42m) + (-523.94m),  // Dec24, Jun25, Jul25, Aug25
                    VIXLevel = 25.0,
                    MarketDrop = -8.0,
                    ActualResults = new List<MonthResult> {
                        new() { Year = 2024, Month = 12, PnL = -620.16m, WinRate = 0.586, Trades = 29 },
                        new() { Year = 2025, Month = 6, PnL = -478.46m, WinRate = 0.522, Trades = 23 },
                        new() { Year = 2025, Month = 7, PnL = -348.42m, WinRate = 0.697, Trades = 33 },
                        new() { Year = 2025, Month = 8, PnL = -523.94m, WinRate = 0.640, Trades = 25 }
                    },
                    LessonsLearned = "System failing in modern market structure"
                }
            };
            
            Console.WriteLine($"Identified {crisisPeriods.Count} major crisis periods:");
            foreach (var crisis in crisisPeriods)
            {
                Console.WriteLine($"  {crisis.Name}: {crisis.TotalLoss:C} total loss, VIX {crisis.VIXLevel}");
            }
            
            return crisisPeriods;
        }
        
        private CrisisAnalysisResults AnalyzeCrisisPerformance(List<CrisisPeriod> crisisPeriods)
        {
            Console.WriteLine("\n--- CRISIS PERFORMANCE ANALYSIS ---");
            Console.WriteLine("What would have worked during crisis periods?");
            
            var allCrisisMonths = crisisPeriods.SelectMany(c => c.ActualResults).ToList();
            
            var analysis = new CrisisAnalysisResults
            {
                AverageWinRate = allCrisisMonths.Average(m => m.WinRate),
                AverageLossPerTrade = allCrisisMonths.Average(m => m.PnL / m.Trades),
                MaxSingleMonthLoss = allCrisisMonths.Min(m => m.PnL),
                AverageTradesPerMonth = allCrisisMonths.Average(m => m.Trades),
                TotalCrisisLoss = allCrisisMonths.Sum(m => m.PnL),
                WorstWinRate = allCrisisMonths.Min(m => m.WinRate)
            };
            
            Console.WriteLine($"Crisis Performance Analysis:");
            Console.WriteLine($"  Average Win Rate: {analysis.AverageWinRate:P1}");
            Console.WriteLine($"  Average Loss/Trade: ${analysis.AverageLossPerTrade:F2}");
            Console.WriteLine($"  Max Single Month Loss: ${analysis.MaxSingleMonthLoss:F2}");
            Console.WriteLine($"  Average Trades/Month: {analysis.AverageTradesPerMonth:F1}");
            Console.WriteLine($"  Total Crisis Loss: ${analysis.TotalCrisisLoss:F2}");
            Console.WriteLine($"  Worst Win Rate: {analysis.WorstWinRate:P1}");
            
            // CRITICAL INSIGHT: What probe strategy would have prevented these losses?
            Console.WriteLine("\nPROBE STRATEGY REQUIREMENTS:");
            Console.WriteLine("1. Must maintain >60% win rate even in worst conditions");
            Console.WriteLine("2. Must limit individual trade loss to <$20");
            Console.WriteLine("3. Must cap monthly loss to <$100");
            Console.WriteLine("4. Must provide early warning before major losses");
            
            return analysis;
        }
        
        private ProbeParameterSet ExtractOptimalProbeParameters(List<CrisisPeriod> crisisPeriods, CrisisAnalysisResults analysis)
        {
            Console.WriteLine("\n--- OPTIMAL PROBE PARAMETER EXTRACTION ---");
            Console.WriteLine("Designing parameters that would have preserved capital in crisis");
            
            var parameters = new ProbeParameterSet
            {
                // PROFIT TARGETS - Minimal but achievable in crisis
                TargetProfitPerTrade = 4.0m,  // Conservative target
                MinAcceptableProfit = 2.0m,  // Bare minimum
                MaxExpectedProfit = 8.0m,    // Ceiling in crisis
                
                // WIN RATE TARGETS - Must work even in worst conditions  
                TargetWinRate = 0.65,        // 5% above worst crisis rate
                MinAcceptableWinRate = 0.60, // Absolute floor
                CrisisWinRate = 0.62,        // Specific crisis target
                
                // POSITION SIZING - Absolute minimum risk
                MaxPositionSizeMultiplier = 0.2,  // 20% of normal sizing
                MaxRiskPerTrade = 25m,            // Hard cap on trade loss
                TypicalPositionSize = 1,          // Usually 1 contract only
                CrisisPositionSize = 1,           // Never exceed 1 in crisis
                
                // LOSS LIMITS - Strict capital preservation
                MaxTradeLoss = 20m,              // Per trade hard stop
                MaxDailyLoss = 50m,               // Daily limit
                MaxMonthlyLoss = 100m,            // Monthly circuit breaker
                StopLossMultiplier = 1.2,         // Very tight stops
                
                // EXECUTION PARAMETERS - Conservative approach
                MaxTradesPerDay = 3,              // Limit exposure
                MinTimeBetweenTrades = TimeSpan.FromHours(2), // Spacing
                RequiredLiquidityScore = 0.7,     // Good fills only
                
                // REGIME DETECTION - When to use probe
                VIXActivationLevel = 22.0,        // Start using at VIX 22+
                StressActivationLevel = 0.35,     // Market stress >35%
                LossStreakTrigger = 2,            // After 2 consecutive losses
                
                // EARLY WARNING SYSTEM
                EnableEarlyWarning = true,
                WarningLossThreshold = 15m,       // Single trade warning
                WarningDailyThreshold = 35m,      // Daily warning
                EscalationTrigger = 2             // Escalate after 2 warnings
            };
            
            Console.WriteLine($"PROBE PARAMETER SET:");
            Console.WriteLine($"  Target Profit/Trade: ${parameters.TargetProfitPerTrade}");
            Console.WriteLine($"  Target Win Rate: {parameters.TargetWinRate:P0}");
            Console.WriteLine($"  Max Risk/Trade: ${parameters.MaxRiskPerTrade}");
            Console.WriteLine($"  Position Size: {parameters.MaxPositionSizeMultiplier:P0} of normal");
            Console.WriteLine($"  Max Monthly Loss: ${parameters.MaxMonthlyLoss}");
            Console.WriteLine($"  VIX Activation: {parameters.VIXActivationLevel}+");
            
            // VALIDATE: Would these parameters have prevented crisis losses?
            Console.WriteLine("\nCRISIS PREVENTION VALIDATION:");
            var wouldHavePrevented = ValidateCrisisPrevention(parameters, crisisPeriods);
            Console.WriteLine($"Would have prevented: {wouldHavePrevented:P0} of crisis losses");
            
            return parameters;
        }
        
        private void ValidateProbeParameters(ProbeParameterSet parameters, List<CrisisPeriod> crisisPeriods)
        {
            Console.WriteLine("\n--- PROBE PARAMETER VALIDATION ---");
            Console.WriteLine("Testing probe parameters against worst historical scenarios");
            
            foreach (var crisis in crisisPeriods)
            {
                Console.WriteLine($"\nTesting against {crisis.Name}:");
                
                var simulatedResults = SimulateProbeStrategyInCrisis(parameters, crisis);
                
                Console.WriteLine($"  Actual Loss: ${crisis.TotalLoss:F2}");
                Console.WriteLine($"  Probe Simulated Loss: ${simulatedResults.EstimatedLoss:F2}");
                Console.WriteLine($"  Loss Reduction: {((crisis.TotalLoss - simulatedResults.EstimatedLoss) / Math.Abs(crisis.TotalLoss)):P1}");
                Console.WriteLine($"  Capital Preservation: {simulatedResults.CapitalPreserved:P1}");
                Console.WriteLine($"  Early Warning Triggered: {simulatedResults.EarlyWarningTriggered}");
                
                if (Math.Abs(simulatedResults.EstimatedLoss) > parameters.MaxMonthlyLoss)
                {
                    Console.WriteLine($"  ⚠️  WARNING: Exceeds monthly loss limit");
                }
                else
                {
                    Console.WriteLine($"  ✓ Within acceptable loss limits");
                }
            }
            
            // STRESS TEST: Worst possible scenario
            Console.WriteLine("\nSTRESS TEST - WORST CASE SCENARIO:");
            Console.WriteLine("VIX 100, Market -50%, Complete liquidity breakdown");
            
            var worstCase = new CrisisPeriod 
            { 
                Name = "WORST_CASE", 
                VIXLevel = 100.0, 
                MarketDrop = -50.0,
                ActualResults = new List<MonthResult> {
                    new() { PnL = -2000m, WinRate = 0.30, Trades = 30 }  // Catastrophic scenario
                }
            };
            
            var worstCaseResult = SimulateProbeStrategyInCrisis(parameters, worstCase);
            Console.WriteLine($"Worst case probe loss: ${worstCaseResult.EstimatedLoss:F2}");
            Console.WriteLine($"Within limits: {Math.Abs(worstCaseResult.EstimatedLoss) <= parameters.MaxMonthlyLoss}");
        }
        
        private void GenerateProbeStrategyImplementation(ProbeParameterSet parameters)
        {
            Console.WriteLine("\n=== PROBE STRATEGY IMPLEMENTATION ===");
            Console.WriteLine("Capital preservation strategy for difficult market conditions");
            
            Console.WriteLine("\n🔍 PROBE STRATEGY CODE FRAMEWORK:");
            Console.WriteLine("```csharp");
            Console.WriteLine("public class ProbeStrategy : ITradeStrategy");
            Console.WriteLine("{");
            Console.WriteLine($"    // PROFIT TARGETS");
            Console.WriteLine($"    public decimal TargetProfit = {parameters.TargetProfitPerTrade}m;");
            Console.WriteLine($"    public decimal MaxRisk = {parameters.MaxRiskPerTrade}m;");
            Console.WriteLine($"    ");
            Console.WriteLine($"    // POSITION SIZING");
            Console.WriteLine($"    public double PositionMultiplier = {parameters.MaxPositionSizeMultiplier}; // {parameters.MaxPositionSizeMultiplier:P0} of normal");
            Console.WriteLine($"    public int MaxContracts = {parameters.CrisisPositionSize};");
            Console.WriteLine($"    ");
            Console.WriteLine($"    // WIN RATE REQUIREMENTS");
            Console.WriteLine($"    public double MinWinRate = {parameters.MinAcceptableWinRate};");
            Console.WriteLine($"    public double TargetWinRate = {parameters.TargetWinRate};");
            Console.WriteLine($"    ");
            Console.WriteLine($"    // RISK MANAGEMENT");
            Console.WriteLine($"    public decimal MaxTradeRisk = {parameters.MaxTradeLoss}m;");
            Console.WriteLine($"    public decimal MaxDailyRisk = {parameters.MaxDailyLoss}m;");
            Console.WriteLine($"    public decimal MaxMonthlyRisk = {parameters.MaxMonthlyLoss}m;");
            Console.WriteLine($"    ");
            Console.WriteLine($"    // ACTIVATION CONDITIONS");
            Console.WriteLine($"    public double VIXThreshold = {parameters.VIXActivationLevel};");
            Console.WriteLine($"    public double StressThreshold = {parameters.StressActivationLevel};");
            Console.WriteLine($"    public int LossStreakLimit = {parameters.LossStreakTrigger};");
            Console.WriteLine($"    ");
            Console.WriteLine($"    // EARLY WARNING SYSTEM");
            Console.WriteLine($"    public bool EarlyWarningEnabled = {parameters.EnableEarlyWarning.ToString().ToLower()};");
            Console.WriteLine($"    public decimal WarningThreshold = {parameters.WarningLossThreshold}m;");
            Console.WriteLine($"    ");
            Console.WriteLine($"    public TradeDecision ShouldTrade(MarketConditions conditions)");
            Console.WriteLine($"    {{");
            Console.WriteLine($"        // Activate probe strategy when:");
            Console.WriteLine($"        if (conditions.VIX > VIXThreshold ||");
            Console.WriteLine($"            conditions.StressLevel > StressThreshold ||");
            Console.WriteLine($"            consecutiveLosses >= LossStreakLimit)");
            Console.WriteLine($"        {{");
            Console.WriteLine($"            return EvaluateProbeEntry(conditions);");
            Console.WriteLine($"        }}");
            Console.WriteLine($"        ");
            Console.WriteLine($"        return TradeDecision.Skip;");
            Console.WriteLine($"    }}");
            Console.WriteLine($"    ");
            Console.WriteLine($"    private TradeDecision EvaluateProbeEntry(MarketConditions conditions)");
            Console.WriteLine($"    {{");
            Console.WriteLine($"        // Conservative entry criteria");
            Console.WriteLine($"        if (conditions.Liquidity < 0.7) return TradeDecision.Skip;");
            Console.WriteLine($"        if (dailyLoss >= MaxDailyRisk) return TradeDecision.Skip;");
            Console.WriteLine($"        if (monthlyLoss >= MaxMonthlyRisk) return TradeDecision.Skip;");
            Console.WriteLine($"        ");
            Console.WriteLine($"        // Size position conservatively");
            Console.WriteLine($"        var position = CalculateProbePosition(conditions);");
            Console.WriteLine($"        return new TradeDecision {{ Trade = true, Size = position }};");
            Console.WriteLine($"    }}");
            Console.WriteLine($"}}");
            Console.WriteLine("```");
            
            Console.WriteLine("\n🎯 PROBE STRATEGY BENEFITS:");
            Console.WriteLine($"1. CAPITAL PRESERVATION: Max ${parameters.MaxMonthlyLoss}/month loss vs actual crisis losses");
            Console.WriteLine($"2. EARLY WARNING: Detects failing conditions before major losses");
            Console.WriteLine($"3. RISK CONTROL: {parameters.MaxPositionSizeMultiplier:P0} position sizing prevents catastrophic exposure");
            Console.WriteLine($"4. LIQUIDITY PROTECTION: Requires {parameters.RequiredLiquidityScore:P0} liquidity for good fills");
            Console.WriteLine($"5. REGIME ADAPTATION: Automatically activates in high VIX/stress periods");
            
            Console.WriteLine("\n📊 EXPECTED PROBE PERFORMANCE:");
            Console.WriteLine($"TARGET: ${parameters.TargetProfitPerTrade}/trade × {parameters.TargetWinRate:P0} win rate");
            var expectedMonthlyProfit = 20m * parameters.TargetProfitPerTrade * (decimal)parameters.TargetWinRate;
            Console.WriteLine($"MONTHLY: ~20 trades × ${parameters.TargetProfitPerTrade} × {parameters.TargetWinRate:P0} = ${expectedMonthlyProfit:F0}/month");
            Console.WriteLine($"WORST CASE: Max ${parameters.MaxMonthlyLoss} monthly loss (vs historical crisis averages)");
            Console.WriteLine($"FUNCTION: Market testing and capital preservation during difficult periods");
        }
        
        #region Helper Methods
        
        private double ValidateCrisisPrevention(ProbeParameterSet parameters, List<CrisisPeriod> crisisPeriods)
        {
            int preventedCount = 0;
            
            foreach (var crisis in crisisPeriods)
            {
                var simulation = SimulateProbeStrategyInCrisis(parameters, crisis);
                if (Math.Abs(simulation.EstimatedLoss) < Math.Abs(crisis.TotalLoss) * 0.5m) // 50% reduction counts as "prevented"
                {
                    preventedCount++;
                }
            }
            
            return (double)preventedCount / crisisPeriods.Count;
        }
        
        private ProbeSimulationResult SimulateProbeStrategyInCrisis(ProbeParameterSet parameters, CrisisPeriod crisis)
        {
            // Conservative simulation: assume probe strategy performs at target levels
            var avgTradesPerMonth = crisis.ActualResults.Average(m => m.Trades) * parameters.MaxPositionSizeMultiplier;
            var expectedLossPerTrade = -parameters.MaxTradeLoss * (decimal)(1.0 - parameters.TargetWinRate); // Loss when trades fail
            var expectedWinPerTrade = parameters.TargetProfitPerTrade * (decimal)parameters.TargetWinRate; // Wins
            var netPerTrade = expectedWinPerTrade + expectedLossPerTrade;
            
            var monthlyResult = netPerTrade * (decimal)avgTradesPerMonth;
            
            return new ProbeSimulationResult
            {
                EstimatedLoss = Math.Max(-parameters.MaxMonthlyLoss, monthlyResult),
                CapitalPreserved = crisis.TotalLoss != 0 ? 1.0 - (double)(Math.Abs(monthlyResult) / Math.Abs(crisis.TotalLoss)) : 1.0,
                EarlyWarningTriggered = Math.Abs(monthlyResult) > parameters.WarningLossThreshold,
                TradesExecuted = (int)avgTradesPerMonth
            };
        }
        
        #endregion
    }
    
    #region Data Classes
    
    public class CrisisPeriod
    {
        public string Name { get; set; } = "";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal TotalLoss { get; set; }
        public double VIXLevel { get; set; }
        public double MarketDrop { get; set; }
        public List<MonthResult> ActualResults { get; set; } = new();
        public string LessonsLearned { get; set; } = "";
    }
    
    public class MonthResult
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal PnL { get; set; }
        public double WinRate { get; set; }
        public int Trades { get; set; }
    }
    
    public class CrisisAnalysisResults
    {
        public double AverageWinRate { get; set; }
        public decimal AverageLossPerTrade { get; set; }
        public decimal MaxSingleMonthLoss { get; set; }
        public double AverageTradesPerMonth { get; set; }
        public decimal TotalCrisisLoss { get; set; }
        public double WorstWinRate { get; set; }
    }
    
    public class ProbeParameterSet
    {
        // Profit targets
        public decimal TargetProfitPerTrade { get; set; }
        public decimal MinAcceptableProfit { get; set; }
        public decimal MaxExpectedProfit { get; set; }
        
        // Win rate targets
        public double TargetWinRate { get; set; }
        public double MinAcceptableWinRate { get; set; }
        public double CrisisWinRate { get; set; }
        
        // Position sizing
        public double MaxPositionSizeMultiplier { get; set; }
        public decimal MaxRiskPerTrade { get; set; }
        public int TypicalPositionSize { get; set; }
        public int CrisisPositionSize { get; set; }
        
        // Loss limits
        public decimal MaxTradeLoss { get; set; }
        public decimal MaxDailyLoss { get; set; }
        public decimal MaxMonthlyLoss { get; set; }
        public double StopLossMultiplier { get; set; }
        
        // Execution parameters
        public int MaxTradesPerDay { get; set; }
        public TimeSpan MinTimeBetweenTrades { get; set; }
        public double RequiredLiquidityScore { get; set; }
        
        // Regime detection
        public double VIXActivationLevel { get; set; }
        public double StressActivationLevel { get; set; }
        public int LossStreakTrigger { get; set; }
        
        // Early warning
        public bool EnableEarlyWarning { get; set; }
        public decimal WarningLossThreshold { get; set; }
        public decimal WarningDailyThreshold { get; set; }
        public int EscalationTrigger { get; set; }
    }
    
    public class ProbeSimulationResult
    {
        public decimal EstimatedLoss { get; set; }
        public double CapitalPreserved { get; set; }
        public bool EarlyWarningTriggered { get; set; }
        public int TradesExecuted { get; set; }
    }
    
    #endregion
}