using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ODTE.Optimization.Core
{
    public interface IStrategyOptimizer
    {
        Task<OptimizationResult> OptimizeAsync(
            StrategyVersion baseStrategy,
            MarketDataSet historicalData,
            OptimizationConfig config);
        
        Task<List<StrategyVersion>> GenerateVariationsAsync(
            StrategyVersion parent,
            int populationSize);
        
        Task<PerformanceMetrics> EvaluateStrategyAsync(
            StrategyVersion strategy,
            MarketDataSet testData);
    }

    public class StrategyVersion
    {
        public string StrategyName { get; set; }
        public string Version { get; set; }
        public DateTime CreatedAt { get; set; }
        public StrategyParameters Parameters { get; set; }
        public PerformanceMetrics Performance { get; set; }
        public string ParentVersion { get; set; }
        public int Generation { get; set; }
    }

    public class StrategyParameters
    {
        // Opening range parameters
        public int OpeningRangeMinutes { get; set; } = 15;
        public double OpeningRangeBreakoutThreshold { get; set; } = 0.5;
        
        // Entry criteria
        public double MinIVRank { get; set; } = 30;
        public double MaxDelta { get; set; } = 0.16;
        public double MinPremium { get; set; } = 0.20;
        public int StrikeOffset { get; set; } = 5;
        
        // Risk management
        public double StopLossPercent { get; set; } = 200;
        public double ProfitTargetPercent { get; set; } = 50;
        public double DeltaExitThreshold { get; set; } = 0.33;
        
        // Position sizing
        public int MaxPositionsPerSide { get; set; } = 10;
        public double AllocationPerTrade { get; set; } = 1000;
        
        // Timing
        public TimeSpan EntryStartTime { get; set; } = new TimeSpan(14, 30, 0);
        public TimeSpan EntryEndTime { get; set; } = new TimeSpan(17, 0, 0);
        public TimeSpan ForceCloseTime { get; set; } = new TimeSpan(20, 45, 0);
        
        // Market regime filters
        public bool UseVWAPFilter { get; set; } = true;
        public bool UseATRFilter { get; set; } = true;
        public double MinATR { get; set; } = 2.0;
        public double MaxATR { get; set; } = 10.0;
    }

    public class PerformanceMetrics
    {
        public double TotalPnL { get; set; }
        public double MaxDrawdown { get; set; }
        public double WinRate { get; set; }
        public double SharpeRatio { get; set; }
        public double CalmarRatio { get; set; }
        public int TotalTrades { get; set; }
        public int WinningDays { get; set; }
        public int LosingDays { get; set; }
        public double AverageDailyPnL { get; set; }
        public double StandardDeviation { get; set; }
        public double ProfitFactor { get; set; }
        public double ExpectedValue { get; set; }
        public Dictionary<DateTime, double> DailyPnL { get; set; }
    }

    public class OptimizationResult
    {
        public StrategyVersion BestStrategy { get; set; }
        public List<StrategyVersion> TopStrategies { get; set; }
        public int GenerationsProcessed { get; set; }
        public int TotalStrategiesEvaluated { get; set; }
        public TimeSpan OptimizationDuration { get; set; }
        public Dictionary<int, GenerationStats> GenerationHistory { get; set; }
    }

    public class GenerationStats
    {
        public int Generation { get; set; }
        public double BestFitness { get; set; }
        public double AverageFitness { get; set; }
        public double WorstFitness { get; set; }
        public int PopulationSize { get; set; }
    }

    public class OptimizationConfig
    {
        public int MaxGenerations { get; set; } = 100;
        public int PopulationSize { get; set; } = 50;
        public double MutationRate { get; set; } = 0.1;
        public double CrossoverRate { get; set; } = 0.7;
        public double EliteRatio { get; set; } = 0.1;
        public FitnessFunction FitnessMetric { get; set; } = FitnessFunction.SharpeRatio;
        public bool UseAdaptiveMutation { get; set; } = true;
        public int MaxParallelEvaluations { get; set; } = 8;
    }

    public enum FitnessFunction
    {
        TotalPnL,
        SharpeRatio,
        CalmarRatio,
        ProfitFactor,
        Combined
    }

    public class MarketDataSet
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string DataPath { get; set; }
        public DataFormat Format { get; set; }
        public List<string> Symbols { get; set; }
    }

    public enum DataFormat
    {
        CSV,
        Parquet,
        Binary
    }
}