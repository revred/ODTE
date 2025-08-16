using System;
using System.Collections.Generic;

namespace ODTE.Strategy
{
    /// <summary>
    /// Market regime classification
    /// </summary>
    public enum MarketRegime
    {
        OPTIMAL,    // VIX < 16, stable conditions
        NORMAL,     // VIX 16-21, normal volatility
        VOLATILE,   // VIX 21-30, elevated volatility  
        CRISIS,     // VIX > 30, crisis conditions
        RECOVERY    // Post-crisis recovery phase
    }

    /// <summary>
    /// Strategy engine configuration
    /// </summary>
    public class StrategyEngineConfig
    {
        public decimal BaseCapital { get; set; } = 10000m;
        public decimal MaxPositionSize { get; set; } = 1000m;
        public decimal RiskTolerance { get; set; } = 0.02m;
        public bool EnableDualStrategy { get; set; } = true;
        public int VIXThresholdHigh { get; set; } = 21;
        public int VIXThresholdLow { get; set; } = 19;
    }

    /// <summary>
    /// Configuration for the 24-day framework
    /// </summary>
    public class FrameworkConfig
    {
        public decimal BasePositionSize { get; set; } = 1.0m;
        public decimal MaxPositionSize { get; set; } = 3.0m;
        public decimal TargetPnL { get; set; } = 6000m;
        public int FrameworkDays { get; set; } = 24;
        public decimal RiskTolerance { get; set; } = 0.02m;
        public bool EnableAmplification { get; set; } = true;
        public bool EnableSniperMode { get; set; } = true;
    }

    /// <summary>
    /// Performance trajectory for position sizing
    /// </summary>
    public enum PerformanceTrajectory
    {
        BehindTarget,
        OnTarget,
        AheadOfTarget,
        ExceedingTarget
    }

    /// <summary>
    /// Framework performance report
    /// </summary>
    public class FrameworkReport
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal TotalPnL { get; set; }
        public decimal TargetPnL { get; set; }
        public int TotalTrades { get; set; }
        public decimal WinRate { get; set; }
        public decimal MaxDrawdown { get; set; }
        public List<DailyResult> DailyResults { get; set; } = new();
        public string Summary { get; set; } = string.Empty;
    }

    /// <summary>
    /// Battle test period for stress testing
    /// </summary>
    public class BattleTestPeriod
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string PeriodName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal ExpectedMaxDrawdown { get; set; }
        public decimal ExpectedVolatility { get; set; }
        public List<string> KeyEvents { get; set; } = new();
    }

    /// <summary>
    /// Daily result for framework tracking
    /// </summary>
    public partial class DailyResult
    {
        public int DayNumber { get; set; }
        public int TradesExecuted { get; set; }
        public MarketRegime Regime { get; set; }
        public decimal PositionSize { get; set; }
        public string Strategy { get; set; } = string.Empty;
    }

    /// <summary>
    /// Strategy interface for framework compatibility
    /// </summary>
    public interface IStrategy
    {
        string Name { get; }
        string Description { get; }
        decimal Execute(MarketConditions conditions, decimal positionSize);
        bool ShouldExecute(MarketConditions conditions);
        void UpdatePerformance(decimal result);
    }

    /// <summary>
    /// Trade execution result
    /// </summary>
    public class Trade
    {
        public DateTime ExecutionTime { get; set; }
        public string Strategy { get; set; } = string.Empty;
        public decimal EntryPrice { get; set; }
        public decimal ExitPrice { get; set; }
        public decimal PnL { get; set; }
        public decimal PositionSize { get; set; }
        public MarketRegime Regime { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

}