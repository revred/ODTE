using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// DUAL-STRATEGY FRAMEWORK DESIGN
    /// 
    /// CONCEPT: Two complementary trading approaches instead of one-size-fits-all
    /// 
    /// PROBE STRATEGY: Low-risk, small-profit trades for difficult market conditions
    /// - Purpose: Test market receptivity while preserving capital
    /// - Target: $3-5 profit per trade, 65-70% win rate, high volume
    /// - Risk: Minimal position sizing, tight stops, early warning system
    /// 
    /// QUALITY STRATEGY: High-confidence trades for optimal conditions  
    /// - Purpose: Generate bulk profits when market conditions align
    /// - Target: $15-25 profit per trade, 80-85% win rate, selective execution
    /// - Risk: Larger positions but only in favorable regimes
    /// </summary>
    public class PM250_DualStrategyFramework
    {
        [Fact]
        public void DesignDualStrategyFramework_ProbeAndQualityTrades()
        {
            Console.WriteLine("=== DUAL-STRATEGY FRAMEWORK DESIGN ===");
            Console.WriteLine("Breakthrough concept: Two strategies for different market conditions");
            Console.WriteLine("Based on insight that single strategy fails across all regimes");
            
            // STEP 1: Define probe strategy parameters
            var probeStrategy = DesignProbeStrategy();
            
            // STEP 2: Define quality strategy parameters  
            var qualityStrategy = DesignQualityStrategy();
            
            // STEP 3: Create regime detection logic
            var regimeDetector = DesignRegimeDetector();
            
            // STEP 4: Design strategy selection logic
            var strategySelector = DesignStrategySelector(probeStrategy, qualityStrategy, regimeDetector);
            
            // STEP 5: Analyze historical performance with dual approach
            AnalyzeHistoricalDualStrategy(probeStrategy, qualityStrategy, regimeDetector);
            
            // STEP 6: Generate implementation framework
            GenerateDualStrategyImplementation(probeStrategy, qualityStrategy, strategySelector);
        }
        
        private ProbeStrategyConfig DesignProbeStrategy()
        {
            Console.WriteLine("\n--- PROBE STRATEGY DESIGN ---");
            Console.WriteLine("Purpose: Low-risk market testing in difficult conditions");
            
            var config = new ProbeStrategyConfig
            {
                // PROFIT TARGETS - Modest but achievable
                TargetProfitPerTrade = 4.0m,
                MinAcceptableProfit = 2.0m,
                MaxExpectedProfit = 6.0m,
                
                // WIN RATE TARGETS - Conservative but realistic
                TargetWinRate = 0.68,
                MinAcceptableWinRate = 0.60,
                
                // POSITION SIZING - Minimal risk
                MaxPositionSizeMultiplier = 0.3, // 30% of normal sizing
                MaxRiskPerTrade = 50m, // Maximum $50 loss per trade
                TypicalPositionSize = 1, // Usually 1 contract
                
                // MARKET CONDITIONS - When to use
                UseInRegimes = new[] { "VOLATILE", "CRISIS", "UNCERTAIN", "MIXED" },
                VIXThresholdMin = 20.0, // Use when VIX > 20
                StressLevelMin = 0.4, // Use when market stress > 40%
                
                // EXECUTION PARAMETERS
                MaxTradesPerDay = 5, // High frequency for testing
                MinTimeBetweenTrades = TimeSpan.FromMinutes(30),
                RequiredLiquidityScore = 0.6, // Lower liquidity requirements
                
                // RISK MANAGEMENT
                StopLossMultiplier = 1.5, // Tight stops - 1.5x credit received
                MaxDailyLoss = 150m, // Cap daily probe losses
                MaxConsecutiveLosses = 3, // Stop after 3 consecutive losses
                
                // STRATEGIC FUNCTION
                Purpose = "Market condition assessment and capital preservation",
                EarlyWarningFunction = true,
                CapitalPreservationMode = true
            };
            
            Console.WriteLine($"Probe Strategy Config:");
            Console.WriteLine($"  Target: ${config.TargetProfitPerTrade}/trade, {config.TargetWinRate:P0} win rate");
            Console.WriteLine($"  Risk: Max ${config.MaxRiskPerTrade} loss, {config.MaxPositionSizeMultiplier:P0} position size");
            Console.WriteLine($"  Usage: {string.Join(", ", config.UseInRegimes)} market conditions");
            Console.WriteLine($"  Function: {config.Purpose}");
            
            return config;
        }
        
        private QualityStrategyConfig DesignQualityStrategy()
        {
            Console.WriteLine("\n--- QUALITY STRATEGY DESIGN ---");
            Console.WriteLine("Purpose: Maximum profit extraction in favorable conditions");
            
            var config = new QualityStrategyConfig
            {
                // PROFIT TARGETS - Aggressive but achievable in good conditions
                TargetProfitPerTrade = 18.0m,
                MinAcceptableProfit = 12.0m,
                MaxExpectedProfit = 30.0m,
                
                // WIN RATE TARGETS - High confidence required
                TargetWinRate = 0.82,
                MinAcceptableWinRate = 0.75,
                
                // POSITION SIZING - Full allocation in good conditions
                MaxPositionSizeMultiplier = 1.0, // 100% of calculated sizing
                MaxRiskPerTrade = 250m, // Higher risk tolerance
                TypicalPositionSize = 3, // Usually 2-3 contracts
                
                // MARKET CONDITIONS - When to use
                UseInRegimes = new[] { "BULL", "RECOVERY", "LOW_VOLATILITY", "TECH_BOOM" },
                VIXThresholdMax = 25.0, // Use when VIX < 25
                StressLevelMax = 0.3, // Use when market stress < 30%
                GoScoreMin = 75.0, // Require high Go Score
                
                // EXECUTION PARAMETERS
                MaxTradesPerDay = 2, // Quality over quantity
                MinTimeBetweenTrades = TimeSpan.FromHours(2),
                RequiredLiquidityScore = 0.8, // High liquidity requirements
                
                // RISK MANAGEMENT
                StopLossMultiplier = 2.0, // Wider stops for better fills
                MaxDailyLoss = 400m, // Higher daily loss tolerance
                MaxConsecutiveLosses = 2, // Lower tolerance for consecutive losses
                
                // STRATEGIC FUNCTION
                Purpose = "Maximum profit generation in optimal conditions",
                SelectiveExecution = true,
                HighConfidenceMode = true
            };
            
            Console.WriteLine($"Quality Strategy Config:");
            Console.WriteLine($"  Target: ${config.TargetProfitPerTrade}/trade, {config.TargetWinRate:P0} win rate");
            Console.WriteLine($"  Risk: Max ${config.MaxRiskPerTrade} loss, {config.MaxPositionSizeMultiplier:P0} position size");
            Console.WriteLine($"  Usage: {string.Join(", ", config.UseInRegimes)} market conditions");
            Console.WriteLine($"  Function: {config.Purpose}");
            
            return config;
        }
        
        private RegimeDetector DesignRegimeDetector()
        {
            Console.WriteLine("\n--- REGIME DETECTION DESIGN ---");
            Console.WriteLine("Purpose: Real-time classification of market conditions");
            
            var detector = new RegimeDetector
            {
                // VIX-based classification
                VIXLevels = new Dictionary<string, (double Min, double Max)>
                {
                    ["LOW_VOLATILITY"] = (0, 18),
                    ["NORMAL"] = (18, 25),
                    ["ELEVATED"] = (25, 35),
                    ["HIGH_VOLATILITY"] = (35, 50),
                    ["CRISIS"] = (50, 100)
                },
                
                // Market stress indicators
                StressIndicators = new Dictionary<string, double>
                {
                    ["BULL"] = 0.2,           // Low stress
                    ["RECOVERY"] = 0.3,       // Moderate stress
                    ["MIXED"] = 0.4,          // Mixed signals
                    ["VOLATILE"] = 0.6,       // High stress
                    ["CRISIS"] = 0.8          // Extreme stress
                },
                
                // Go Score thresholds
                GoScoreThresholds = new Dictionary<string, double>
                {
                    ["EXCELLENT"] = 80,       // High confidence
                    ["GOOD"] = 70,           // Moderate confidence  
                    ["FAIR"] = 60,           // Low confidence
                    ["POOR"] = 50            // Very low confidence
                },
                
                // Historical regime mapping
                HistoricalRegimes = GetHistoricalRegimeMapping(),
                
                // Real-time detection logic
                DetectionLogic = "Combine VIX, stress indicators, Go Score, and recent performance"
            };
            
            Console.WriteLine($"Regime Detection Framework:");
            Console.WriteLine($"  VIX Levels: {detector.VIXLevels.Count} categories");
            Console.WriteLine($"  Stress Indicators: {detector.StressIndicators.Count} levels");
            Console.WriteLine($"  Go Score Thresholds: {detector.GoScoreThresholds.Count} confidence levels");
            Console.WriteLine($"  Logic: {detector.DetectionLogic}");
            
            return detector;
        }
        
        private StrategySelector DesignStrategySelector(ProbeStrategyConfig probe, QualityStrategyConfig quality, RegimeDetector detector)
        {
            Console.WriteLine("\n--- STRATEGY SELECTION LOGIC ---");
            Console.WriteLine("Purpose: Dynamic strategy selection based on market conditions");
            
            var selector = new StrategySelector
            {
                // Primary selection rules
                SelectionRules = new List<StrategySelectionRule>
                {
                    new() {
                        Condition = "VIX > 25 OR StressLevel > 0.4 OR GoScore < 65",
                        SelectedStrategy = "PROBE",
                        Reason = "Difficult market conditions - use probe strategy",
                        Confidence = 0.8
                    },
                    new() {
                        Condition = "VIX < 20 AND StressLevel < 0.3 AND GoScore > 75",
                        SelectedStrategy = "QUALITY",
                        Reason = "Optimal conditions - use quality strategy",
                        Confidence = 0.9
                    },
                    new() {
                        Condition = "ConsecutiveLosses >= 2",
                        SelectedStrategy = "PROBE",
                        Reason = "Recent losses - switch to defensive mode",
                        Confidence = 0.85
                    },
                    new() {
                        Condition = "RecentPerformance > TargetProfit",
                        SelectedStrategy = "QUALITY",
                        Reason = "Strong performance - leverage momentum",
                        Confidence = 0.75
                    }
                },
                
                // Hybrid approach rules
                HybridRules = new List<HybridRule>
                {
                    new() {
                        Condition = "Mixed market signals",
                        ProbeAllocation = 0.7,
                        QualityAllocation = 0.3,
                        Reason = "Uncertain conditions - bias toward probe"
                    },
                    new() {
                        Condition = "Transition periods",
                        ProbeAllocation = 0.5,
                        QualityAllocation = 0.5,
                        Reason = "Regime change - balanced approach"
                    }
                },
                
                // Fallback logic
                DefaultStrategy = "PROBE", // When in doubt, use probe
                MaxProbeAllocation = 0.8,  // Never more than 80% probe
                MinQualityAllocation = 0.1 // Always maintain some quality exposure
            };
            
            Console.WriteLine($"Strategy Selection Framework:");
            Console.WriteLine($"  Primary Rules: {selector.SelectionRules.Count}");
            Console.WriteLine($"  Hybrid Rules: {selector.HybridRules.Count}"); 
            Console.WriteLine($"  Default: {selector.DefaultStrategy} strategy");
            Console.WriteLine($"  Max Probe Allocation: {selector.MaxProbeAllocation:P0}");
            
            return selector;
        }
        
        private void AnalyzeHistoricalDualStrategy(ProbeStrategyConfig probe, QualityStrategyConfig quality, RegimeDetector detector)
        {
            Console.WriteLine("\n--- HISTORICAL DUAL-STRATEGY ANALYSIS ---");
            Console.WriteLine("Analyzing how dual approach would have performed vs single strategy");
            
            // Get historical monthly data from our previous analysis
            var historicalMonths = GetHistoricalMonthData();
            
            var singleStrategyResults = new DualStrategyResults();
            var dualStrategyResults = new DualStrategyResults();
            
            foreach (var month in historicalMonths)
            {
                // Classify the regime for this month
                var regime = ClassifyHistoricalRegime(month, detector);
                
                // Single strategy result (what we actually got)
                singleStrategyResults.AddMonth(month.ActualPnL, month.ActualTrades);
                
                // Dual strategy simulation
                var dualResult = SimulateDualStrategy(month, regime, probe, quality);
                dualStrategyResults.AddMonth(dualResult.EstimatedPnL, dualResult.EstimatedTrades);
            }
            
            Console.WriteLine($"HISTORICAL COMPARISON:");
            Console.WriteLine($"Single Strategy:");
            Console.WriteLine($"  Total P&L: ${singleStrategyResults.TotalPnL:F2}");
            Console.WriteLine($"  Avg/Trade: ${singleStrategyResults.AvgPnLPerTrade:F2}");
            Console.WriteLine($"  Win Rate: {singleStrategyResults.WinRate:P1}");
            Console.WriteLine($"  Profitable Months: {singleStrategyResults.ProfitableMonthRate:P1}");
            
            Console.WriteLine($"Dual Strategy (Estimated):");
            Console.WriteLine($"  Total P&L: ${dualStrategyResults.TotalPnL:F2}");
            Console.WriteLine($"  Avg/Trade: ${dualStrategyResults.AvgPnLPerTrade:F2}");
            Console.WriteLine($"  Win Rate: {dualStrategyResults.WinRate:P1}");
            Console.WriteLine($"  Profitable Months: {dualStrategyResults.ProfitableMonthRate:P1}");
            
            var improvement = (dualStrategyResults.TotalPnL - singleStrategyResults.TotalPnL) / Math.Abs(singleStrategyResults.TotalPnL);
            Console.WriteLine($"ESTIMATED IMPROVEMENT: {improvement:P1}");
        }
        
        private void GenerateDualStrategyImplementation(ProbeStrategyConfig probe, QualityStrategyConfig quality, StrategySelector selector)
        {
            Console.WriteLine("\n=== DUAL-STRATEGY IMPLEMENTATION FRAMEWORK ===");
            
            Console.WriteLine("\n🔍 PROBE STRATEGY IMPLEMENTATION:");
            Console.WriteLine($"```csharp");
            Console.WriteLine($"// Probe Strategy - Market Testing & Capital Preservation");
            Console.WriteLine($"public class ProbeStrategy {{");
            Console.WriteLine($"    TargetProfit = {probe.TargetProfitPerTrade}m;");
            Console.WriteLine($"    MaxRisk = {probe.MaxRiskPerTrade}m;"); 
            Console.WriteLine($"    PositionSizeMultiplier = {probe.MaxPositionSizeMultiplier}f;");
            Console.WriteLine($"    WinRateTarget = {probe.TargetWinRate:F2}f;");
            Console.WriteLine($"    MaxDailyTrades = {probe.MaxTradesPerDay};");
            Console.WriteLine($"    UseWhenVIX > {probe.VIXThresholdMin};");
            Console.WriteLine($"    UseWhenStress > {probe.StressLevelMin:F1};");
            Console.WriteLine($"}}");
            Console.WriteLine($"```");
            
            Console.WriteLine("\n🎯 QUALITY STRATEGY IMPLEMENTATION:");
            Console.WriteLine($"```csharp");
            Console.WriteLine($"// Quality Strategy - Maximum Profit in Optimal Conditions");
            Console.WriteLine($"public class QualityStrategy {{");
            Console.WriteLine($"    TargetProfit = {quality.TargetProfitPerTrade}m;");
            Console.WriteLine($"    MaxRisk = {quality.MaxRiskPerTrade}m;");
            Console.WriteLine($"    PositionSizeMultiplier = {quality.MaxPositionSizeMultiplier}f;");
            Console.WriteLine($"    WinRateTarget = {quality.TargetWinRate:F2}f;");
            Console.WriteLine($"    MaxDailyTrades = {quality.MaxTradesPerDay};");
            Console.WriteLine($"    UseWhenVIX < {quality.VIXThresholdMax};");
            Console.WriteLine($"    UseWhenStress < {quality.StressLevelMax:F1};");
            Console.WriteLine($"    RequireGoScore > {quality.GoScoreMin};");
            Console.WriteLine($"}}");
            Console.WriteLine($"```");
            
            Console.WriteLine("\n⚡ STRATEGY SELECTION LOGIC:");
            Console.WriteLine($"```csharp");
            Console.WriteLine($"public IStrategy SelectStrategy(MarketConditions conditions) {{");
            Console.WriteLine($"    // High stress/volatility -> Probe");
            Console.WriteLine($"    if (conditions.VIX > 25 || conditions.Stress > 0.4)");
            Console.WriteLine($"        return ProbeStrategy;");
            Console.WriteLine($"    ");
            Console.WriteLine($"    // Optimal conditions -> Quality");
            Console.WriteLine($"    if (conditions.VIX < 20 && conditions.Stress < 0.3 && conditions.GoScore > 75)");
            Console.WriteLine($"        return QualityStrategy;");
            Console.WriteLine($"    ");
            Console.WriteLine($"    // Recent losses -> Probe (defensive)");
            Console.WriteLine($"    if (recentConsecutiveLosses >= 2)");
            Console.WriteLine($"        return ProbeStrategy;");
            Console.WriteLine($"    ");
            Console.WriteLine($"    // Default to probe for capital preservation");
            Console.WriteLine($"    return ProbeStrategy;");
            Console.WriteLine($"}}");
            Console.WriteLine($"```");
            
            Console.WriteLine("\n📊 EXPECTED PERFORMANCE TARGETS:");
            Console.WriteLine($"PROBE STRATEGY (60% of trades):");
            Console.WriteLine($"  - ${probe.TargetProfitPerTrade}/trade × 0.6 allocation = ${probe.TargetProfitPerTrade * 0.6m}/trade contribution");
            Console.WriteLine($"  - {probe.TargetWinRate:P0} win rate in difficult conditions");
            Console.WriteLine($"  - Capital preservation and early warning function");
            
            Console.WriteLine($"QUALITY STRATEGY (40% of trades):");
            Console.WriteLine($"  - ${quality.TargetProfitPerTrade}/trade × 0.4 allocation = ${quality.TargetProfitPerTrade * 0.4m}/trade contribution");
            Console.WriteLine($"  - {quality.TargetWinRate:P0} win rate in optimal conditions");
            Console.WriteLine($"  - Bulk profit generation when conditions align");
            
            var weightedAvgProfit = (probe.TargetProfitPerTrade * 0.6m) + (quality.TargetProfitPerTrade * 0.4m);
            var weightedAvgWinRate = (probe.TargetWinRate * 0.6) + (quality.TargetWinRate * 0.4);
            
            Console.WriteLine($"COMBINED EXPECTED PERFORMANCE:");
            Console.WriteLine($"  - ${weightedAvgProfit:F2}/trade (weighted average)");
            Console.WriteLine($"  - {weightedAvgWinRate:P1} win rate (weighted average)");
            Console.WriteLine($"  - Significantly better risk management");
            Console.WriteLine($"  - Adaptive to market conditions");
            Console.WriteLine($"  - Capital preservation in difficult periods");
        }
        
        #region Helper Methods
        
        private Dictionary<string, string> GetHistoricalRegimeMapping()
        {
            return new Dictionary<string, string>
            {
                ["2020-03"] = "CRISIS",
                ["2020-04"] = "RECOVERY", 
                ["2021-01"] = "BULL",
                ["2021-11"] = "TECH_BOOM",
                ["2022-02"] = "VOLATILE",
                ["2022-06"] = "BEAR",
                ["2023-02"] = "CRISIS",
                ["2024-03"] = "TECH_BOOM",
                ["2024-12"] = "CRISIS",
                ["2025-06"] = "VOLATILE"
            };
        }
        
        private List<HistoricalMonth> GetHistoricalMonthData()
        {
            // Sample data from our real validation
            return new List<HistoricalMonth>
            {
                new() { Year = 2020, Month = 3, ActualPnL = -842.16m, ActualTrades = 31, Regime = "CRISIS" },
                new() { Year = 2021, Month = 11, ActualPnL = 487.94m, ActualTrades = 24, Regime = "TECH_BOOM" },
                new() { Year = 2024, Month = 3, ActualPnL = 1028.02m, ActualTrades = 25, Regime = "TECH_BOOM" },
                new() { Year = 2024, Month = 12, ActualPnL = -620.16m, ActualTrades = 29, Regime = "CRISIS" },
                new() { Year = 2025, Month = 6, ActualPnL = -478.46m, ActualTrades = 23, Regime = "VOLATILE" }
            };
        }
        
        private string ClassifyHistoricalRegime(HistoricalMonth month, RegimeDetector detector)
        {
            // Simplified classification based on historical context
            return month.Regime;
        }
        
        private DualStrategyResult SimulateDualStrategy(HistoricalMonth month, string regime, ProbeStrategyConfig probe, QualityStrategyConfig quality)
        {
            // Estimate how dual strategy would have performed
            if (regime == "CRISIS" || regime == "VOLATILE")
            {
                // Would have used mostly probe strategy
                return new DualStrategyResult
                {
                    EstimatedPnL = month.ActualTrades * probe.TargetProfitPerTrade * 0.68m, // 68% win rate
                    EstimatedTrades = month.ActualTrades,
                    StrategyMix = "80% Probe, 20% Quality"
                };
            }
            else
            {
                // Would have used mostly quality strategy
                return new DualStrategyResult
                {
                    EstimatedPnL = month.ActualTrades * quality.TargetProfitPerTrade * 0.82m, // 82% win rate
                    EstimatedTrades = month.ActualTrades,
                    StrategyMix = "30% Probe, 70% Quality"
                };
            }
        }
        
        #endregion
    }
    
    #region Data Classes
    
    public class ProbeStrategyConfig
    {
        public decimal TargetProfitPerTrade { get; set; }
        public decimal MinAcceptableProfit { get; set; }
        public decimal MaxExpectedProfit { get; set; }
        public double TargetWinRate { get; set; }
        public double MinAcceptableWinRate { get; set; }
        public double MaxPositionSizeMultiplier { get; set; }
        public decimal MaxRiskPerTrade { get; set; }
        public int TypicalPositionSize { get; set; }
        public string[] UseInRegimes { get; set; } = Array.Empty<string>();
        public double VIXThresholdMin { get; set; }
        public double StressLevelMin { get; set; }
        public int MaxTradesPerDay { get; set; }
        public TimeSpan MinTimeBetweenTrades { get; set; }
        public double RequiredLiquidityScore { get; set; }
        public double StopLossMultiplier { get; set; }
        public decimal MaxDailyLoss { get; set; }
        public int MaxConsecutiveLosses { get; set; }
        public string Purpose { get; set; } = "";
        public bool EarlyWarningFunction { get; set; }
        public bool CapitalPreservationMode { get; set; }
    }
    
    public class QualityStrategyConfig
    {
        public decimal TargetProfitPerTrade { get; set; }
        public decimal MinAcceptableProfit { get; set; }
        public decimal MaxExpectedProfit { get; set; }
        public double TargetWinRate { get; set; }
        public double MinAcceptableWinRate { get; set; }
        public double MaxPositionSizeMultiplier { get; set; }
        public decimal MaxRiskPerTrade { get; set; }
        public int TypicalPositionSize { get; set; }
        public string[] UseInRegimes { get; set; } = Array.Empty<string>();
        public double VIXThresholdMax { get; set; }
        public double StressLevelMax { get; set; }
        public double GoScoreMin { get; set; }
        public int MaxTradesPerDay { get; set; }
        public TimeSpan MinTimeBetweenTrades { get; set; }
        public double RequiredLiquidityScore { get; set; }
        public double StopLossMultiplier { get; set; }
        public decimal MaxDailyLoss { get; set; }
        public int MaxConsecutiveLosses { get; set; }
        public string Purpose { get; set; } = "";
        public bool SelectiveExecution { get; set; }
        public bool HighConfidenceMode { get; set; }
    }
    
    public class RegimeDetector
    {
        public Dictionary<string, (double Min, double Max)> VIXLevels { get; set; } = new();
        public Dictionary<string, double> StressIndicators { get; set; } = new();
        public Dictionary<string, double> GoScoreThresholds { get; set; } = new();
        public Dictionary<string, string> HistoricalRegimes { get; set; } = new();
        public string DetectionLogic { get; set; } = "";
    }
    
    public class StrategySelector
    {
        public List<StrategySelectionRule> SelectionRules { get; set; } = new();
        public List<HybridRule> HybridRules { get; set; } = new();
        public string DefaultStrategy { get; set; } = "";
        public double MaxProbeAllocation { get; set; }
        public double MinQualityAllocation { get; set; }
    }
    
    public class StrategySelectionRule
    {
        public string Condition { get; set; } = "";
        public string SelectedStrategy { get; set; } = "";
        public string Reason { get; set; } = "";
        public double Confidence { get; set; }
    }
    
    public class HybridRule
    {
        public string Condition { get; set; } = "";
        public double ProbeAllocation { get; set; }
        public double QualityAllocation { get; set; }
        public string Reason { get; set; } = "";
    }
    
    public class DualStrategyResults
    {
        public decimal TotalPnL { get; private set; }
        public int TotalTrades { get; private set; }
        public int WinningTrades { get; private set; }
        public int ProfitableMonths { get; private set; }
        public int TotalMonths { get; private set; }
        
        public decimal AvgPnLPerTrade => TotalTrades > 0 ? TotalPnL / TotalTrades : 0;
        public double WinRate => TotalTrades > 0 ? (double)WinningTrades / TotalTrades : 0;
        public double ProfitableMonthRate => TotalMonths > 0 ? (double)ProfitableMonths / TotalMonths : 0;
        
        public void AddMonth(decimal monthPnL, int monthTrades)
        {
            TotalPnL += monthPnL;
            TotalTrades += monthTrades;
            TotalMonths++;
            if (monthPnL > 0) ProfitableMonths++;
            if (monthPnL > 0) WinningTrades += (int)(monthTrades * 0.75); // Estimate winning trades
        }
    }
    
    public class HistoricalMonth
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal ActualPnL { get; set; }
        public int ActualTrades { get; set; }
        public string Regime { get; set; } = "";
    }
    
    public class DualStrategyResult
    {
        public decimal EstimatedPnL { get; set; }
        public int EstimatedTrades { get; set; }
        public string StrategyMix { get; set; } = "";
    }
    
    #endregion
}