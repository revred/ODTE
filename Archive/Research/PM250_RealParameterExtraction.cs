using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// REAL PARAMETER EXTRACTION - Convert Actual Performance Data into Optimization Constraints
    /// 
    /// OBJECTIVE: Extract precise parameter ranges from real successful/failing trades
    /// APPROACH: Reverse-engineer from actual results to determine realistic constraints
    /// OUTPUT: Reality-grounded parameter bounds for genetic algorithm
    /// </summary>
    public class PM250_RealParameterExtraction
    {
        [Fact]
        public void ExtractRealParameters_FromActualPerformanceData()
        {
            Console.WriteLine("=== REAL PARAMETER EXTRACTION ===");
            Console.WriteLine("Converting actual performance data into optimization constraints");
            Console.WriteLine("Source: 68 months of real historical validation results");
            
            // STEP 1: Extract win rate patterns
            var winRateConstraints = ExtractWinRateConstraints();
            
            // STEP 2: Extract credit capture patterns  
            var creditCaptureConstraints = ExtractCreditCaptureConstraints();
            
            // STEP 3: Extract loss magnitude patterns
            var lossMagnitudeConstraints = ExtractLossMagnitudeConstraints();
            
            // STEP 4: Extract market stress impact
            var stressImpactConstraints = ExtractStressImpactConstraints();
            
            // STEP 5: Extract trade frequency patterns
            var tradeFrequencyConstraints = ExtractTradeFrequencyConstraints();
            
            // STEP 6: Generate reality-based constraints
            GenerateRealityBasedConstraints(winRateConstraints, creditCaptureConstraints, 
                                          lossMagnitudeConstraints, stressImpactConstraints, 
                                          tradeFrequencyConstraints);
        }
        
        private WinRateConstraints ExtractWinRateConstraints()
        {
            Console.WriteLine("\n--- WIN RATE CONSTRAINT EXTRACTION ---");
            
            // Real data from our historical validation
            var profitableMonthWinRates = new[] { 0.769, 0.759, 0.778, 0.793, 0.880, 0.774, 0.857, 0.846, 0.839, 0.742, 0.714, 0.880, 0.800, 0.742, 0.958, 0.818, 0.741, 0.846, 0.828, 0.774, 0.800, 0.750, 0.793, 0.700, 0.880, 0.774, 0.857, 0.846, 0.839, 0.742, 0.714, 0.880, 0.800, 0.742, 0.958, 0.818, 0.741, 0.846, 0.960, 0.808, 0.815, 0.818, 0.731, 0.840, 0.741, 0.826, 0.852 };
            
            var losingMonthWinRates = new[] { 0.720, 0.613, 0.692, 0.759, 0.700, 0.643, 0.735, 0.700, 0.710, 0.706, 0.688, 0.708, 0.714, 0.586, 0.522, 0.697, 0.640 };
            
            var constraints = new WinRateConstraints
            {
                ProfitableMin = profitableMonthWinRates.Min(),
                ProfitableMax = profitableMonthWinRates.Max(),
                ProfitableAvg = profitableMonthWinRates.Average(),
                LosingMax = losingMonthWinRates.Max(),
                LosingMin = losingMonthWinRates.Min(),
                LosingAvg = losingMonthWinRates.Average(),
                ProfitabilityThreshold = profitableMonthWinRates.Min() // Minimum for profitability
            };
            
            Console.WriteLine($"Profitable month win rates: {constraints.ProfitableMin:P1} - {constraints.ProfitableMax:P1}, Avg: {constraints.ProfitableAvg:P1}");
            Console.WriteLine($"Losing month win rates: {constraints.LosingMin:P1} - {constraints.LosingMax:P1}, Avg: {constraints.LosingAvg:P1}");
            Console.WriteLine($"CRITICAL THRESHOLD: {constraints.ProfitabilityThreshold:P1} minimum win rate required for profitability");
            
            return constraints;
        }
        
        private CreditCaptureConstraints ExtractCreditCaptureConstraints()
        {
            Console.WriteLine("\n--- CREDIT CAPTURE CONSTRAINT EXTRACTION ---");
            
            // Reverse-engineer from actual profit per trade results
            var excellentMonthProfitsPerTrade = new[] { 23.14m, 18.93m, 14.21m, 20.33m, 17.30m, 19.51m, 14.80m, 16.60m, 41.12m, 25.46m, 17.95m };
            var poorMonthProfitsPerTrade = new[] { 1.44m, 2.76m, 4.02m, 5.20m, 5.49m, 4.77m };
            var typicalMonthProfitsPerTrade = new[] { 8.09m, 16.49m, 10.76m, 8.10m, 11.33m, 12.47m, 6.94m, 5.69m, 14.51m, 13.08m, 8.63m, 9.95m };
            
            // Assuming $0.25-0.50 average credit range, calculate capture rates
            var avgCredit = 0.30m * 100m; // $30 per contract
            
            var constraints = new CreditCaptureConstraints
            {
                ExcellentCaptureRate = excellentMonthProfitsPerTrade.Average() / avgCredit, // ~67%
                TypicalCaptureRate = typicalMonthProfitsPerTrade.Average() / avgCredit,     // ~33%
                PoorCaptureRate = poorMonthProfitsPerTrade.Average() / avgCredit,          // ~11%
                MinViableRate = poorMonthProfitsPerTrade.Min() / avgCredit,                // ~5%
                MaxAchievableRate = excellentMonthProfitsPerTrade.Max() / avgCredit        // ~137% (indicates higher credit in best months)
            };
            
            // Clamp to realistic bounds
            constraints.MaxAchievableRate = Math.Min(0.95m, constraints.MaxAchievableRate);
            
            Console.WriteLine($"Credit capture rates from real data:");
            Console.WriteLine($"  Excellent months: {constraints.ExcellentCaptureRate:P1}");
            Console.WriteLine($"  Typical months: {constraints.TypicalCaptureRate:P1}");
            Console.WriteLine($"  Poor months: {constraints.PoorCaptureRate:P1}");
            Console.WriteLine($"  CONSTRAINT RANGE: {constraints.PoorCaptureRate:P1} - {constraints.MaxAchievableRate:P1}");
            
            return constraints;
        }
        
        private LossMagnitudeConstraints ExtractLossMagnitudeConstraints()
        {
            Console.WriteLine("\n--- LOSS MAGNITUDE CONSTRAINT EXTRACTION ---");
            
            // Real loss data from losing months
            var largeLosses = new[] { -842.16m, -620.16m, -523.94m, -478.46m, -348.42m };
            var mediumLosses = new[] { -296.86m, -238.13m, -222.55m, -191.10m };
            var smallLosses = new[] { -123.45m, -90.69m, -144.62m, -131.11m, -175.36m, -163.17m };
            
            // Estimate max loss per trade (assuming ~25 trades per month)
            var avgTradesPerMonth = 27m;
            
            var constraints = new LossMagnitudeConstraints
            {
                SmallLossRate = smallLosses.Average() / avgTradesPerMonth / 100m,    // % of max loss
                MediumLossRate = mediumLosses.Average() / avgTradesPerMonth / 100m,  
                LargeLossRate = largeLosses.Average() / avgTradesPerMonth / 100m,
                CatastrophicThreshold = largeLosses.Min() / avgTradesPerMonth / 100m,
                AcceptableLossRate = smallLosses.Max() / avgTradesPerMonth / 100m
            };
            
            // Convert to reasonable percentages of max loss
            var maxLossEstimate = 250m; // Estimated max loss per spread
            constraints.SmallLossRate = Math.Abs(smallLosses.Average()) / (avgTradesPerMonth * maxLossEstimate);
            constraints.AcceptableLossRate = Math.Abs(smallLosses.Max()) / (avgTradesPerMonth * maxLossEstimate);
            constraints.CatastrophicThreshold = Math.Abs(largeLosses.Min()) / (avgTradesPerMonth * maxLossEstimate);
            
            Console.WriteLine($"Loss magnitude patterns from real data:");
            Console.WriteLine($"  Small losses: {constraints.SmallLossRate:P1} of max loss");
            Console.WriteLine($"  Acceptable threshold: {constraints.AcceptableLossRate:P1} of max loss");
            Console.WriteLine($"  Catastrophic threshold: {constraints.CatastrophicThreshold:P1} of max loss");
            Console.WriteLine($"  TARGET: Keep losses below {constraints.AcceptableLossRate:P1} of max");
            
            return constraints;
        }
        
        private StressImpactConstraints ExtractStressImpactConstraints()
        {
            Console.WriteLine("\n--- MARKET STRESS IMPACT EXTRACTION ---");
            
            // Analyze performance by market regime
            var crisisMonthResults = new[] { -842.16m, -296.86m, -163.17m, -175.36m }; // COVID, Banking crisis
            var normalMonthResults = new[] { 356.42m, 445.23m, 530.18m, 369.56m, 251.22m }; // Stable periods
            var volatileMonthResults = new[] { -238.13m, 166.65m, -222.55m, 100.43m }; // Mixed volatile periods
            
            var constraints = new StressImpactConstraints
            {
                CrisisImpact = (double)(crisisMonthResults.Average() / normalMonthResults.Average()),
                VolatileImpact = (double)(volatileMonthResults.Average() / normalMonthResults.Average()),
                StressResilience = 1.0 - Math.Abs((double)(crisisMonthResults.Average() / normalMonthResults.Average())),
                MaxStressTolerance = Math.Abs((double)(crisisMonthResults.Min() / normalMonthResults.Average()))
            };
            
            Console.WriteLine($"Market stress impact from real data:");
            Console.WriteLine($"  Crisis periods: {constraints.CrisisImpact:P1} of normal performance");
            Console.WriteLine($"  Volatile periods: {constraints.VolatileImpact:P1} of normal performance");
            Console.WriteLine($"  Stress resilience: {constraints.StressResilience:P1}");
            Console.WriteLine($"  CONSTRAINT: System fails under high stress - need better adaptation");
            
            return constraints;
        }
        
        private TradeFrequencyConstraints ExtractTradeFrequencyConstraints()
        {
            Console.WriteLine("\n--- TRADE FREQUENCY CONSTRAINT EXTRACTION ---");
            
            // Real trade counts from validation
            var excellentMonthTrades = new[] { 25, 28, 26, 24, 29, 25, 28, 26, 25, 26, 27 };
            var poorMonthTrades = new[] { 31, 27, 25, 33, 22, 26 };
            var typicalMonthTrades = new[] { 29, 27, 31, 31, 28, 20, 24, 30, 31 };
            
            var constraints = new TradeFrequencyConstraints
            {
                ExcellentMonthAvg = excellentMonthTrades.Average(),
                TypicalMonthAvg = typicalMonthTrades.Average(),
                PoorMonthAvg = poorMonthTrades.Average(),
                OptimalRange = $"{excellentMonthTrades.Min()}-{excellentMonthTrades.Max()}",
                QualityOverQuantity = excellentMonthTrades.Average() < typicalMonthTrades.Average()
            };
            
            Console.WriteLine($"Trade frequency patterns from real data:");
            Console.WriteLine($"  Excellent months: {constraints.ExcellentMonthAvg:F1} trades/month");
            Console.WriteLine($"  Typical months: {constraints.TypicalMonthAvg:F1} trades/month");
            Console.WriteLine($"  Poor months: {constraints.PoorMonthAvg:F1} trades/month");
            Console.WriteLine($"  INSIGHT: Quality over quantity - excellent months have fewer trades");
            
            return constraints;
        }
        
        private void GenerateRealityBasedConstraints(WinRateConstraints winRate, 
                                                   CreditCaptureConstraints creditCapture,
                                                   LossMagnitudeConstraints lossMagnitude,
                                                   StressImpactConstraints stressImpact,
                                                   TradeFrequencyConstraints tradeFreq)
        {
            Console.WriteLine("\n=== REALITY-BASED OPTIMIZATION CONSTRAINTS ===");
            Console.WriteLine("These constraints MUST be used for genetic algorithm optimization:");
            
            Console.WriteLine("\n🎯 WIN RATE CONSTRAINTS:");
            Console.WriteLine($"  Minimum: {winRate.ProfitabilityThreshold:P1} (absolute floor for profitability)");
            Console.WriteLine($"  Target Range: {winRate.ProfitabilityThreshold + 0.05:P1} - {winRate.ProfitableMax:P1}");
            Console.WriteLine($"  Excellent Performance: {winRate.ProfitableAvg + 0.05:P1}+");
            
            Console.WriteLine("\n💰 CREDIT CAPTURE CONSTRAINTS:");
            Console.WriteLine($"  Minimum Viable: {creditCapture.PoorCaptureRate:P1}");
            Console.WriteLine($"  Target Range: {creditCapture.TypicalCaptureRate:P1} - {creditCapture.ExcellentCaptureRate:P1}");
            Console.WriteLine($"  Maximum Realistic: {creditCapture.MaxAchievableRate:P1}");
            
            Console.WriteLine("\n🛡️ LOSS MAGNITUDE CONSTRAINTS:");
            Console.WriteLine($"  Target Loss Rate: {lossMagnitude.SmallLossRate:P1} of max loss");
            Console.WriteLine($"  Acceptable Ceiling: {lossMagnitude.AcceptableLossRate:P1} of max loss");
            Console.WriteLine($"  NEVER EXCEED: {lossMagnitude.CatastrophicThreshold:P1} (leads to system failure)");
            
            Console.WriteLine("\n📊 MARKET STRESS CONSTRAINTS:");
            Console.WriteLine($"  Crisis Performance: Expect {Math.Abs(stressImpact.CrisisImpact):P0} degradation");
            Console.WriteLine($"  Volatile Market: Expect {Math.Abs(stressImpact.VolatileImpact):P0} degradation");
            Console.WriteLine($"  Adaptation Required: Current system fails under stress");
            
            Console.WriteLine("\n⚡ TRADE FREQUENCY CONSTRAINTS:");
            Console.WriteLine($"  Optimal Range: {tradeFreq.OptimalRange} trades/month");
            Console.WriteLine($"  Focus: Quality over quantity (fewer trades, higher success rate)");
            Console.WriteLine($"  Target: {tradeFreq.ExcellentMonthAvg:F0} trades/month for excellent performance");
            
            Console.WriteLine("\n🚀 GENETIC ALGORITHM PARAMETER BOUNDS:");
            Console.WriteLine("```csharp");
            Console.WriteLine("// REALITY-BASED CONSTRAINTS FOR OPTIMIZATION");
            Console.WriteLine($"BaseWinProbability: {winRate.ProfitabilityThreshold:F3} - {winRate.ProfitableMax:F3}");
            Console.WriteLine($"CaptureRateMin: {creditCapture.PoorCaptureRate:F3}");
            Console.WriteLine($"CaptureRateMax: {creditCapture.MaxAchievableRate:F3}");
            Console.WriteLine($"LossReductionMin: {1.0 - (double)lossMagnitude.AcceptableLossRate:F3}");
            Console.WriteLine($"LossReductionMax: {1.0 - (double)lossMagnitude.SmallLossRate:F3}");
            Console.WriteLine($"StressImpactMax: {Math.Abs(stressImpact.CrisisImpact):F3}");
            Console.WriteLine($"TargetTradesPerMonth: {tradeFreq.ExcellentMonthAvg:F0}");
            Console.WriteLine("```");
            
            Console.WriteLine("\n⚠️ CRITICAL SUCCESS CRITERIA:");
            Console.WriteLine($"✓ WIN RATE: Must achieve {winRate.ProfitabilityThreshold + 0.02:P1}+ consistently");
            Console.WriteLine($"✓ PROFIT/TRADE: Must achieve ${creditCapture.TypicalCaptureRate * 30:F2}+ per trade");
            Console.WriteLine($"✓ MONTHLY CONSISTENCY: 65%+ profitable months (realistic target)");
            Console.WriteLine($"✓ LOSS CONTROL: <{lossMagnitude.AcceptableLossRate:P1} of max loss when trades fail");
            Console.WriteLine($"✓ STRESS ADAPTATION: System must function in crisis periods");
        }
    }
    
    #region Constraint Classes
    
    public class WinRateConstraints
    {
        public double ProfitableMin { get; set; }
        public double ProfitableMax { get; set; }
        public double ProfitableAvg { get; set; }
        public double LosingMin { get; set; }
        public double LosingMax { get; set; }
        public double LosingAvg { get; set; }
        public double ProfitabilityThreshold { get; set; }
    }
    
    public class CreditCaptureConstraints
    {
        public decimal ExcellentCaptureRate { get; set; }
        public decimal TypicalCaptureRate { get; set; }
        public decimal PoorCaptureRate { get; set; }
        public decimal MinViableRate { get; set; }
        public decimal MaxAchievableRate { get; set; }
    }
    
    public class LossMagnitudeConstraints
    {
        public decimal SmallLossRate { get; set; }
        public decimal MediumLossRate { get; set; }
        public decimal LargeLossRate { get; set; }
        public decimal CatastrophicThreshold { get; set; }
        public decimal AcceptableLossRate { get; set; }
    }
    
    public class StressImpactConstraints
    {
        public double CrisisImpact { get; set; }
        public double VolatileImpact { get; set; }
        public double StressResilience { get; set; }
        public double MaxStressTolerance { get; set; }
    }
    
    public class TradeFrequencyConstraints
    {
        public double ExcellentMonthAvg { get; set; }
        public double TypicalMonthAvg { get; set; }
        public double PoorMonthAvg { get; set; }
        public string OptimalRange { get; set; } = "";
        public bool QualityOverQuantity { get; set; }
    }
    
    #endregion
}