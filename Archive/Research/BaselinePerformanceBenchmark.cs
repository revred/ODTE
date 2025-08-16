using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using ODTE.Strategy;
using Xunit;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// Benchmark current strategy performance on 20-year real data BEFORE GoScore implementation
    /// Establishes baseline metrics that GoScore must beat:
    /// - Target: Reduce loss frequency by 30%+
    /// - Target: Improve ROC by 20%+ in Calm regime
    /// - Target: Maintain zero RFib breaches
    /// </summary>
    public class BaselinePerformanceBenchmark
    {
        [Fact]
        public void Benchmark_Current_Strategy_Performance_20Year_Real_Data()
        {
            Console.WriteLine("====================================================================");
            Console.WriteLine("BASELINE PERFORMANCE BENCHMARK - 20-YEAR REAL DATA (PRE-GOSCORE)");
            Console.WriteLine("====================================================================");
            Console.WriteLine("Purpose: Establish baseline metrics that GoScore implementation must beat");
            Console.WriteLine("Dataset: Real SPY/VIX data 2005-2020 (4,027 trading days)");
            Console.WriteLine("Strategies: Current regime-based selection without GoScore gating");
            Console.WriteLine();

            var realDataAnalyzer = new RealDataRegimeSwitcher();
            
            // Run full 20-year analysis to get baseline performance
            Console.WriteLine("Running baseline 20-year analysis...");
            var baselineResult = realDataAnalyzer.RunRealDataAnalysis(
                new DateTime(2005, 1, 1), 
                new DateTime(2020, 12, 31)
            );
            
            // Calculate detailed baseline metrics
            var baselineMetrics = CalculateBaselineMetrics(baselineResult);
            
            DisplayBaselineResults(baselineMetrics, baselineResult);
            
            // Save baseline for GoScore comparison
            SaveBaselineForComparison(baselineMetrics);
            
            Console.WriteLine();
            Console.WriteLine("=== BASELINE BENCHMARK COMPLETED ===");
            Console.WriteLine("Next: Implement GoScore and validate improvement targets:");
            Console.WriteLine("- Loss frequency reduction: ≥30%");  
            Console.WriteLine("- ROC improvement (Calm): ≥20%");
            Console.WriteLine("- Zero RFib breaches maintained");
        }
        
        private BaselineMetrics CalculateBaselineMetrics(RegimeSwitchingAnalysisResult result)
        {
            var totalTrades = result.Periods.Count;
            var losingTrades = result.Periods.Count(p => p.ReturnPercentage < 0);
            var winningTrades = result.Periods.Count(p => p.ReturnPercentage > 0);
            
            var lossFrequency = (double)losingTrades / totalTrades;
            var winRate = (double)winningTrades / totalTrades;
            
            // Calculate ROC by regime (approximated from period returns)
            var calmPeriods = result.Periods.Where(p => p.DominantRegime.ToString() == "Calm");
            var mixedPeriods = result.Periods.Where(p => p.DominantRegime.ToString() == "Mixed");
            var convexPeriods = result.Periods.Where(p => p.DominantRegime.ToString() == "Convex");
            
            var avgReturnCalm = calmPeriods.Any() ? calmPeriods.Average(p => p.ReturnPercentage) : 0;
            var avgReturnMixed = mixedPeriods.Any() ? mixedPeriods.Average(p => p.ReturnPercentage) : 0;
            var avgReturnConvex = convexPeriods.Any() ? convexPeriods.Average(p => p.ReturnPercentage) : 0;
            
            // Calculate volatility and risk metrics
            var returns = result.Periods.Select(p => p.ReturnPercentage).ToList();
            var volatility = CalculateVolatility(returns);
            var maxDrawdown = CalculateMaxDrawdown(returns);
            var sharpeRatio = result.AverageReturn / volatility;
            
            return new BaselineMetrics
            {
                TotalPeriods = totalTrades,
                WinRate = winRate,
                LossFrequency = lossFrequency,
                AverageReturn = result.AverageReturn,
                TotalReturn = result.TotalReturn,
                BestReturn = result.BestPeriodReturn,
                WorstReturn = result.WorstPeriodReturn,
                
                // Regime-specific performance
                CalmPeriods = calmPeriods.Count(),
                MixedPeriods = mixedPeriods.Count(),
                ConvexPeriods = convexPeriods.Count(),
                
                AvgReturnCalm = avgReturnCalm,
                AvgReturnMixed = avgReturnMixed,
                AvgReturnConvex = avgReturnConvex,
                
                // Risk metrics
                Volatility = volatility,
                MaxDrawdown = maxDrawdown,
                SharpeRatio = sharpeRatio,
                
                // Performance by regime from ledger - sum from actual results
                CalmPnL = calmPeriods.Sum(p => p.CurrentCapital - p.StartingCapital),
                MixedPnL = mixedPeriods.Sum(p => p.CurrentCapital - p.StartingCapital),
                ConvexPnL = convexPeriods.Sum(p => p.CurrentCapital - p.StartingCapital)
            };
        }
        
        private void DisplayBaselineResults(BaselineMetrics metrics, RegimeSwitchingAnalysisResult result)
        {
            Console.WriteLine();
            Console.WriteLine("BASELINE PERFORMANCE METRICS (20-YEAR REAL DATA)");
            Console.WriteLine("=================================================");
            
            Console.WriteLine($"📊 OVERALL PERFORMANCE:");
            Console.WriteLine($"   Total 24-day periods: {metrics.TotalPeriods:N0}");
            Console.WriteLine($"   Win rate: {metrics.WinRate:P1}");
            Console.WriteLine($"   Loss frequency: {metrics.LossFrequency:P1} ⬅️ TARGET: Reduce by 30%");
            Console.WriteLine($"   Average return/period: {metrics.AverageReturn:F2}%");
            Console.WriteLine($"   Total compound return: {metrics.TotalReturn:F2}%");
            Console.WriteLine($"   Best period: {metrics.BestReturn:F2}%");
            Console.WriteLine($"   Worst period: {metrics.WorstReturn:F2}%");
            
            Console.WriteLine();
            Console.WriteLine($"📈 REGIME BREAKDOWN:");
            Console.WriteLine($"   Calm periods: {metrics.CalmPeriods:N0} (avg: {metrics.AvgReturnCalm:F2}%) ⬅️ TARGET: +20% ROC");
            Console.WriteLine($"   Mixed periods: {metrics.MixedPeriods:N0} (avg: {metrics.AvgReturnMixed:F2}%)");
            Console.WriteLine($"   Convex periods: {metrics.ConvexPeriods:N0} (avg: {metrics.AvgReturnConvex:F2}%)");
            
            Console.WriteLine();
            Console.WriteLine($"⚖️ RISK METRICS:");
            Console.WriteLine($"   Volatility: {metrics.Volatility:F2}%");
            Console.WriteLine($"   Max drawdown: {metrics.MaxDrawdown:F2}%");
            Console.WriteLine($"   Sharpe ratio: {metrics.SharpeRatio:F2}");
            
            Console.WriteLine();
            Console.WriteLine($"💰 P&L BY REGIME:");
            Console.WriteLine($"   Calm: ${metrics.CalmPnL:N0}");
            Console.WriteLine($"   Mixed: ${metrics.MixedPnL:N0}");
            Console.WriteLine($"   Convex: ${metrics.ConvexPnL:N0}");
            
            Console.WriteLine();
            Console.WriteLine($"🎯 GOSCORE IMPROVEMENT TARGETS:");
            Console.WriteLine($"   Loss frequency: {metrics.LossFrequency:P1} → {metrics.LossFrequency * 0.7:P1} (30% reduction)");
            Console.WriteLine($"   Calm ROC: {metrics.AvgReturnCalm:F2}% → {metrics.AvgReturnCalm * 1.2:F2}% (20% improvement)");
            Console.WriteLine($"   RFib breaches: 0 → 0 (maintain zero)");
        }
        
        private void SaveBaselineForComparison(BaselineMetrics metrics)
        {
            var baselineFile = @"C:\code\ODTE\data\exports\baseline_performance_20yr.json";
            
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(metrics, new System.Text.Json.JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                
                File.WriteAllText(baselineFile, json);
                Console.WriteLine($"✅ Baseline metrics saved to: {baselineFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Could not save baseline: {ex.Message}");
            }
        }
        
        private double CalculateVolatility(List<double> returns)
        {
            if (!returns.Any()) return 0;
            
            var mean = returns.Average();
            var variance = returns.Sum(r => Math.Pow(r - mean, 2)) / returns.Count;
            return Math.Sqrt(variance);
        }
        
        private double CalculateMaxDrawdown(List<double> returns)
        {
            if (!returns.Any()) return 0;
            
            var cumulative = 1.0;
            var peak = 1.0;
            var maxDrawdown = 0.0;
            
            foreach (var ret in returns)
            {
                cumulative *= (1 + ret / 100);
                peak = Math.Max(peak, cumulative);
                var drawdown = (peak - cumulative) / peak;
                maxDrawdown = Math.Max(maxDrawdown, drawdown);
            }
            
            return maxDrawdown * 100;
        }
        
        public class BaselineMetrics
        {
            public int TotalPeriods { get; set; }
            public double WinRate { get; set; }
            public double LossFrequency { get; set; }
            public double AverageReturn { get; set; }
            public double TotalReturn { get; set; }
            public double BestReturn { get; set; }
            public double WorstReturn { get; set; }
            
            public int CalmPeriods { get; set; }
            public int MixedPeriods { get; set; }
            public int ConvexPeriods { get; set; }
            
            public double AvgReturnCalm { get; set; }
            public double AvgReturnMixed { get; set; }
            public double AvgReturnConvex { get; set; }
            
            public double Volatility { get; set; }
            public double MaxDrawdown { get; set; }
            public double SharpeRatio { get; set; }
            
            public double CalmPnL { get; set; }
            public double MixedPnL { get; set; }
            public double ConvexPnL { get; set; }
            
            public DateTime BenchmarkDate { get; set; } = DateTime.UtcNow;
            public string DatasetDescription { get; set; } = "20-year real SPY/VIX data (2005-2020)";
        }
    }
}