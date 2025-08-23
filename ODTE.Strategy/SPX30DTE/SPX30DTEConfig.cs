using System;
using System.Collections.Generic;

namespace ODTE.Strategy.SPX30DTE
{
    public class SPX30DTEConfig
    {
        public BWBConfiguration SPXCore { get; set; } = new();
        public ProbeConfiguration XSPProbe { get; set; } = new();
        public HedgeConfiguration VIXHedge { get; set; } = new();
        public SynchronizationRules Synchronization { get; set; } = new();
        public SPX30DTERevFibNotchScale RiskScale { get; set; } = new();
        
        public decimal MaxPortfolioRisk { get; set; } = 0.25m; // 25% max risk
        public decimal MonthlyIncomeTarget { get; set; } = 2500m; // $2,500 target
        public decimal MaxDrawdownLimit { get; set; } = 5000m; // $5k at -5% SPX
    }

    public class BWBConfiguration
    {
        public int TargetDTE { get; set; } = 30;
        public int MinDTE { get; set; } = 25;
        public int MaxDTE { get; set; } = 35;
        
        public decimal WingWidthPoints { get; set; } = 50;
        public decimal ShortStrikeOffset { get; set; } = 350; // Points below spot
        public decimal LongUpperOffset { get; set; } = 450;  // Points below spot
        public decimal LongLowerOffset { get; set; } = 300;  // Points below spot
        
        public decimal TargetCredit { get; set; } = 800m;
        public decimal MinCredit { get; set; } = 700m;
        public decimal MaxRisk { get; set; } = 4200m;
        
        public int MaxPositions { get; set; } = 4;
        public decimal ProfitTarget { get; set; } = 0.65m; // 65% of max profit
        public decimal StopLoss { get; set; } = 2.0m; // 2x credit received
        
        public int ForcedExitDTE { get; set; } = 10;
        public decimal DeltaThreshold { get; set; } = 0.15m; // Max delta exposure
        
        public bool RequireProbeConfirmation { get; set; } = true;
        public int MinProbeWins { get; set; } = 2; // Need 2 probe wins before entry
    }

    public class ProbeConfiguration
    {
        public int TargetDTE { get; set; } = 15;
        public int MinDTE { get; set; } = 10;
        public int MaxDTE { get; set; } = 20;
        
        public decimal SpreadWidth { get; set; } = 5; // XSP points
        public decimal DeltaTarget { get; set; } = 0.20m; // 20 delta short
        
        public decimal TargetCredit { get; set; } = 65m;
        public decimal MinCredit { get; set; } = 60m;
        public decimal MaxRisk { get; set; } = 435m;
        
        public int ProbesPerWeek { get; set; } = 5;
        public int MaxActiveProbes { get; set; } = 20;
        
        public decimal ProfitTarget { get; set; } = 0.65m; // 65% of max profit
        public int ForcedExitDTE { get; set; } = 5;
        
        public Dictionary<DayOfWeek, int> DailyProbeSchedule { get; set; } = new()
        {
            { DayOfWeek.Monday, 2 },
            { DayOfWeek.Tuesday, 2 },
            { DayOfWeek.Wednesday, 1 },
            { DayOfWeek.Thursday, 0 },
            { DayOfWeek.Friday, 0 }
        };
        
        public decimal WinRateThreshold { get; set; } = 0.60m; // Min win rate for bullish signal
        public int LookbackPeriod { get; set; } = 10; // Days to evaluate probe performance
    }

    public class HedgeConfiguration
    {
        public int TargetDTE { get; set; } = 50;
        public int MinDTE { get; set; } = 45;
        public int MaxDTE { get; set; } = 60;
        
        public decimal LongStrike { get; set; } = 20; // VIX level
        public decimal ShortStrike { get; set; } = 30; // VIX level
        public decimal SpreadWidth { get; set; } = 10;
        
        public decimal MaxCostPerHedge { get; set; } = 50m;
        public decimal MaxPayoff { get; set; } = 1000m;
        
        public int BaseHedgeCount { get; set; } = 2; // Always maintain 2
        public int MaxHedgeCount { get; set; } = 4;
        
        public decimal VIXSpikeThreshold { get; set; } = 3; // Points increase for partial close
        public decimal PartialClosePercent { get; set; } = 0.50m; // Close 50% on spike
        
        public Dictionary<decimal, int> ExposureBasedHedges { get; set; } = new()
        {
            { 10000m, 2 },  // $10k exposure = 2 hedges
            { 15000m, 3 },  // $15k exposure = 3 hedges
            { 20000m, 4 }   // $20k exposure = 4 hedges
        };
        
        public decimal HedgeCostBudget { get; set; } = 0.02m; // 2% of exposure for hedges
    }

    public class SynchronizationRules
    {
        public bool ProbesLeadCore { get; set; } = true;
        public int ProbeLeadDays { get; set; } = 2;
        
        public decimal MinProbeWinRate { get; set; } = 0.60m;
        public int MinConsecutiveProbeWins { get; set; } = 2;
        
        public decimal MaxCorrelatedRisk { get; set; } = 0.30m; // 30% correlated positions
        
        public bool AutoFreezeOnDrawdown { get; set; } = true;
        public decimal DrawdownFreezeThreshold { get; set; } = -0.03m; // -3% intraday
        
        public bool ScaleWithVolatility { get; set; } = true;
        public decimal HighVolThreshold { get; set; } = 25; // VIX > 25
        public decimal VolatilityScaleFactor { get; set; } = 0.50m; // Reduce size by 50%
        
        public Dictionary<string, decimal> RiskAllocation { get; set; } = new()
        {
            { "SPX_CORE", 0.60m },   // 60% to SPX BWBs
            { "XSP_PROBE", 0.30m },  // 30% to XSP probes
            { "VIX_HEDGE", 0.10m }   // 10% to VIX hedges
        };
    }

    public class SPX30DTERevFibNotchScale
    {
        public decimal[] NotchLimits { get; set; } = new[]
        {
            5000m,   // Maximum (after 3+ profitable months)
            3200m,   // Aggressive (2 profitable weeks)
            2000m,   // Balanced (starting position)
            1200m,   // Conservative (mild loss > 10%)
            800m,    // Defensive (major loss > 30%)
            400m     // Survival (catastrophic loss > 50%)
        };
        
        public int CurrentNotchIndex { get; set; } = 2; // Start at Balanced
        
        public int DaysForUpgrade { get; set; } = 10; // Need 10 profitable days
        public decimal LossPercentForDowngrade { get; set; } = 0.15m; // 15% day loss
        
        public bool RequireConsecutiveWinsForUpgrade { get; set; } = true;
        public int ConsecutiveWinsRequired { get; set; } = 5;
        
        public decimal EmergencyStopLoss { get; set; } = 0.25m; // 25% portfolio loss
        public bool AutoShutdownOnEmergency { get; set; } = true;
    }

    public enum ProbeSentiment
    {
        Bullish,
        Neutral,
        Bearish,
        Volatile,
        Insufficient // Not enough data
    }

    public class ExecutionPlan
    {
        public DateTime ExecutionDate { get; set; }
        public List<ProbeEntry> ProbeEntries { get; set; } = new();
        public BWBEntry CoreEntry { get; set; }
        public List<HedgeAdjustment> HedgeAdjustments { get; set; } = new();
        public List<PositionExit> ScheduledExits { get; set; } = new();
        public decimal EstimatedCapitalRequired { get; set; }
        public PortfolioGreeks ProjectedGreeks { get; set; }
    }

    public class ProbeEntry
    {
        public string Symbol { get; set; } = "XSP";
        public DateTime Expiration { get; set; }
        public decimal ShortStrike { get; set; }
        public decimal LongStrike { get; set; }
        public decimal Credit { get; set; }
        public decimal Risk { get; set; }
        public int Quantity { get; set; }
    }

    public class BWBEntry
    {
        public string Symbol { get; set; } = "SPX";
        public DateTime Expiration { get; set; }
        public decimal LongLowerStrike { get; set; }
        public decimal ShortStrike { get; set; }
        public decimal LongUpperStrike { get; set; }
        public int[] Quantities { get; set; } = { 1, -2, 1 };
        public decimal Credit { get; set; }
        public decimal MaxRisk { get; set; }
    }

    public class HedgeAdjustment
    {
        public string Action { get; set; } // "ADD", "CLOSE", "PARTIAL_CLOSE"
        public string Symbol { get; set; } = "VIX";
        public DateTime Expiration { get; set; }
        public decimal LongStrike { get; set; }
        public decimal ShortStrike { get; set; }
        public decimal Cost { get; set; }
        public int Quantity { get; set; }
        public string Reason { get; set; }
    }

    public class PositionExit
    {
        public string PositionId { get; set; }
        public string Reason { get; set; } // "PROFIT_TARGET", "DTE_EXPIRY", "STOP_LOSS"
        public decimal ExpectedProfit { get; set; }
        public DateTime ExitTime { get; set; }
    }

    public class PortfolioGreeks
    {
        public decimal NetDelta { get; set; }
        public decimal NetGamma { get; set; }
        public decimal NetTheta { get; set; }
        public decimal NetVega { get; set; }
        public decimal NetRho { get; set; }
        
        public decimal DeltaAdjustedExposure { get; set; }
        public decimal GammaRisk { get; set; }
        public decimal DailyThetaDecay { get; set; }
        public decimal VegaExposure { get; set; }
        
        public Dictionary<string, decimal> ComponentGreeks { get; set; } = new();
    }
}