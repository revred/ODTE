using System;

namespace ODTE.Strategy;

/// <summary>
/// Dynamic Position Sizing Engine for 24-Day Framework
/// 
/// OBJECTIVE: Intelligently scale position sizes based on:
/// 1. Performance trajectory relative to $6000 target
/// 2. Market regime and volatility conditions
/// 3. Framework phase (base/sniper/amplification)
/// 4. Risk management constraints
/// 
/// MATHEMATICAL FOUNDATION:
/// - Kelly Criterion for optimal position sizing
/// - Volatility scaling using realized/implied vol ratios
/// - Performance-based multipliers with risk controls
/// - Drawdown-adjusted sizing for capital preservation
/// 
/// KEY FEATURES:
/// - Conservative base sizing with performance amplification
/// - Sniper mode: Precision trades with concentrated risk
/// - Risk amplification: Double sizing when target achieved early
/// - Volatility-adjusted scaling for changing market conditions
/// - Maximum position limits for risk control
/// </summary>
public class PositionSizingEngine
{
    private readonly FrameworkConfig _config;
    private string _lastSizingRationale = "";

    public PositionSizingEngine(FrameworkConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Calculate optimal position size based on framework state and market conditions
    /// </summary>
    public decimal CalculatePositionSize(
        int dayNumber, 
        PerformanceTrajectory trajectory, 
        decimal currentPnL, 
        MarketRegime regime)
    {
        // Start with base position size
        var baseSize = _config.BasePositionSize;
        
        // Apply trajectory-based multiplier
        var trajectoryMultiplier = GetTrajectoryMultiplier(trajectory, dayNumber, currentPnL);
        
        // Apply market regime adjustment
        var regimeMultiplier = GetRegimeMultiplier(regime);
        
        // Apply volatility scaling
        var volatilityMultiplier = GetVolatilityMultiplier(regime.VIX);
        
        // Apply Kelly Criterion scaling based on expected edge
        var kellyMultiplier = GetKellyMultiplier(regime, trajectory);
        
        // Calculate raw position size
        var rawSize = baseSize * trajectoryMultiplier * regimeMultiplier * volatilityMultiplier * kellyMultiplier;
        
        // Apply risk controls
        var finalSize = ApplyRiskControls(rawSize, currentPnL, dayNumber);
        
        // Store rationale for reporting
        _lastSizingRationale = BuildSizingRationale(
            trajectoryMultiplier, regimeMultiplier, volatilityMultiplier, 
            kellyMultiplier, rawSize, finalSize);
        
        return finalSize;
    }

    /// <summary>
    /// Get position size multiplier based on performance trajectory
    /// </summary>
    private decimal GetTrajectoryMultiplier(PerformanceTrajectory trajectory, int dayNumber, decimal currentPnL)
    {
        var progressRatio = currentPnL / TwentyFourDayFramework.TARGET_PROFIT;
        var timeRatio = (decimal)dayNumber / TwentyFourDayFramework.FRAMEWORK_DAYS;
        
        return trajectory switch
        {
            // Sniper mode: High-conviction concentrated bets
            PerformanceTrajectory.SniperModeRequired => 1.5m,
            
            // Risk amplification: Double position sizes when target achieved early
            PerformanceTrajectory.RiskAmplification when _config.EnableRiskAmplification => 2.0m,
            
            // Recovery mode: Aggressive sizing to catch up
            PerformanceTrajectory.RecoveryMode => 1.3m,
            
            // Final push: Escalated sizing for final days
            PerformanceTrajectory.FinalPush when dayNumber > 20 => 1.4m,
            
            // Ahead of schedule: Slightly more aggressive
            PerformanceTrajectory.AheadOfSchedule => 1.1m,
            
            // Base operations: Conservative sizing
            _ => 1.0m
        };
    }

    /// <summary>
    /// Get position size multiplier based on market regime
    /// </summary>
    private decimal GetRegimeMultiplier(MarketRegime regime)
    {
        var baseMultiplier = regime.RegimeType switch
        {
            RegimeType.LowVolatility when regime.Confidence > 0.8m => 1.2m,  // High confidence, favorable
            RegimeType.Ranging when regime.Confidence > 0.7m => 1.15m,       // Range-bound, predictable
            RegimeType.Trending when regime.TrendStrength > 0.6 => 1.1m,     // Clear trend
            RegimeType.HighVolatility => 0.7m,                               // High vol, reduce size
            RegimeType.Uncertain => 0.8m,                                    // Uncertain, conservative
            _ => 1.0m
        };

        // Reduce sizing if regime confidence is low
        if (regime.Confidence < 0.6m)
            baseMultiplier *= 0.8m;

        return baseMultiplier;
    }

    /// <summary>
    /// Get position size multiplier based on VIX level
    /// </summary>
    private decimal GetVolatilityMultiplier(decimal vix)
    {
        return vix switch
        {
            < 15m => 1.2m,    // Very low vol - can size up
            < 20m => 1.1m,    // Low vol - slightly higher size
            < 30m => 1.0m,    // Normal vol - base size
            < 40m => 0.8m,    // High vol - reduce size
            _ => 0.6m         // Extreme vol - significantly reduce
        };
    }

    /// <summary>
    /// Apply Kelly Criterion for optimal position sizing based on edge
    /// </summary>
    private decimal GetKellyMultiplier(MarketRegime regime, PerformanceTrajectory trajectory)
    {
        // Estimate win rate and average win/loss based on regime and strategy
        var estimatedWinRate = regime.RegimeType switch
        {
            RegimeType.LowVolatility => 0.75,   // High win rate in calm markets
            RegimeType.Ranging => 0.70,         // Good for condor strategies
            RegimeType.Trending => 0.60,        // Moderate win rate
            RegimeType.HighVolatility => 0.50,  // Challenging conditions
            _ => 0.55
        };

        var estimatedWinLossRatio = trajectory switch
        {
            PerformanceTrajectory.SniperModeRequired => 3.0,  // High reward/risk in sniper mode
            PerformanceTrajectory.RiskAmplification => 1.5,   // Conservative when doubling
            _ => 2.0  // Standard reward/risk ratio
        };

        // Kelly formula: f = (bp - q) / b
        // where b = odds received (win/loss ratio), p = win probability, q = loss probability
        var kellyFraction = (estimatedWinLossRatio * estimatedWinRate - (1 - estimatedWinRate)) / estimatedWinLossRatio;
        
        // Apply Kelly but with conservative scaling (max 25% of Kelly suggestion)
        var kellyMultiplier = Math.Max(0.5, Math.Min(1.25, 1.0 + kellyFraction * 0.25));

        return (decimal)kellyMultiplier;
    }

    /// <summary>
    /// Apply final risk controls to position size
    /// </summary>
    private decimal ApplyRiskControls(decimal rawSize, decimal currentPnL, int dayNumber)
    {
        // Maximum position size limit
        var sizeControlled = Math.Min(rawSize, _config.MaxPositionSize);
        
        // Reduce sizing if approaching max loss limits
        var maxAllowedLoss = _config.MaxDailyLoss;
        if (currentPnL < -maxAllowedLoss * 3) // If down 3x daily limit
        {
            sizeControlled *= 0.6m; // Significantly reduce sizing
        }
        else if (currentPnL < -maxAllowedLoss)
        {
            sizeControlled *= 0.8m; // Moderately reduce sizing
        }

        // Late-stage protection: More conservative in final days if behind
        if (dayNumber > 20 && currentPnL < TwentyFourDayFramework.TARGET_PROFIT * 0.8m)
        {
            sizeControlled = Math.Min(sizeControlled, _config.BasePositionSize * 1.1m);
        }

        // Minimum position size floor
        return Math.Max(sizeControlled, _config.BasePositionSize * 0.5m);
    }

    /// <summary>
    /// Build detailed rationale for position sizing decision
    /// </summary>
    private string BuildSizingRationale(
        decimal trajectoryMultiplier, 
        decimal regimeMultiplier, 
        decimal volatilityMultiplier,
        decimal kellyMultiplier,
        decimal rawSize,
        decimal finalSize)
    {
        var components = new[]
        {
            $"Trajectory: {trajectoryMultiplier:F2}x",
            $"Regime: {regimeMultiplier:F2}x", 
            $"Volatility: {volatilityMultiplier:F2}x",
            $"Kelly: {kellyMultiplier:F2}x"
        };

        var rationale = string.Join(" | ", components);
        
        if (finalSize != rawSize)
        {
            rationale += $" → Risk Control: ${finalSize:F0} (from ${rawSize:F0})";
        }

        return rationale;
    }

    /// <summary>
    /// Get the last sizing rationale for reporting
    /// </summary>
    public string GetSizingRationale() => _lastSizingRationale;

    /// <summary>
    /// Calculate position size for specific strategy and trade setup
    /// </summary>
    public decimal CalculateTradeSize(
        decimal frameworkPositionSize, 
        IStrategy strategy, 
        double tradeConfidence,
        decimal accountEquity)
    {
        // Base trade size from framework position sizing
        var baseTradeSize = frameworkPositionSize;
        
        // Adjust for strategy characteristics
        var strategyMultiplier = strategy switch
        {
            SniperStrategy => 1.5m,      // Concentrated bets
            GhostStrategy => 0.8m,       // Conservative sizing
            VolatilityCrusher => 1.2m,   // Can size up in low vol
            _ => 1.0m
        };
        
        // Adjust for trade confidence
        var confidenceMultiplier = (decimal)(0.7 + (tradeConfidence - 0.5) * 0.6); // 0.7x to 1.3x
        
        // Apply risk of ruin protection (max 2% of account per trade)
        var maxRiskSize = accountEquity * 0.02m;
        
        var finalTradeSize = Math.Min(
            baseTradeSize * strategyMultiplier * confidenceMultiplier,
            maxRiskSize
        );
        
        return Math.Max(finalTradeSize, frameworkPositionSize * 0.3m); // Minimum 30% of framework size
    }
}

/// <summary>
/// Market regime data for position sizing decisions
/// </summary>
public class MarketRegime
{
    public RegimeType RegimeType { get; set; }
    public decimal Confidence { get; set; }
    public decimal VIX { get; set; }
    public double TrendStrength { get; set; }
    public bool HasMajorEvent { get; set; }
}

/// <summary>
/// Market regime classifications
/// </summary>
public enum RegimeType
{
    LowVolatility,
    HighVolatility,
    Trending,
    Ranging,
    Uncertain
}