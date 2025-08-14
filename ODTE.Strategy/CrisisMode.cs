using System;

namespace ODTE.Strategy;

/// <summary>
/// Crisis Mode Detection and Classification System
/// 
/// CRITICAL PURPOSE: Enable framework to trade profitably during market stress
/// ROOT CAUSE FIX: Framework currently shuts down when regime confidence <0.6
/// SOLUTION: Detect crisis conditions and activate specialized strategies
/// </summary>
public enum CrisisMode
{
    /// <summary>Normal market conditions - standard strategies apply</summary>
    Normal,
    
    /// <summary>Elevated stress - increase position sizing and use defensive strategies</summary>
    Elevated,
    
    /// <summary>High stress - crisis-specific strategies, volatility expansion plays</summary>
    Crisis,
    
    /// <summary>Extreme stress - emergency protocols, tail hedge activation</summary>
    BlackSwan
}

/// <summary>
/// Real-time crisis detection and classification engine
/// </summary>
public class CrisisDetector
{
    private readonly TimeSpan _detectionWindow = TimeSpan.FromDays(3);
    
    /// <summary>
    /// Detect current crisis mode based on market conditions
    /// </summary>
    public CrisisMode DetectCrisisMode(MarketConditions conditions, MarketRegime regime)
    {
        var crisisScore = 0;
        
        // VIX-based crisis indicators
        if (regime.VIX > 80) crisisScore += 4; // Black swan territory
        else if (regime.VIX > 50) crisisScore += 3; // Crisis territory
        else if (regime.VIX > 35) crisisScore += 2; // Elevated stress
        else if (regime.VIX > 25) crisisScore += 1; // Mild stress
        
        // Regime confidence breakdown
        if (regime.Confidence < 0.2m) crisisScore += 3; // Complete uncertainty
        else if (regime.Confidence < 0.4m) crisisScore += 2; // High uncertainty
        else if (regime.Confidence < 0.6m) crisisScore += 1; // Moderate uncertainty
        
        // IV Rank explosion
        if (conditions.IVRank > 95) crisisScore += 2;
        else if (conditions.IVRank > 85) crisisScore += 1;
        
        // RSI extremes (oversold bounces in crisis)
        if (conditions.RSI < 20) crisisScore += 1; // Extreme oversold
        else if (conditions.RSI > 80) crisisScore += 1; // Extreme overbought
        
        // VIX contango breakdown (backwardation in crisis)
        if (conditions.VIXContango < -5) crisisScore += 2;
        else if (conditions.VIXContango < 0) crisisScore += 1;
        
        // Momentum divergence (often occurs at crisis peaks)
        if (Math.Abs(conditions.MomentumDivergence) > 0.7) crisisScore += 1;
        
        return crisisScore switch
        {
            >= 8 => CrisisMode.BlackSwan,
            >= 5 => CrisisMode.Crisis,
            >= 3 => CrisisMode.Elevated,
            _ => CrisisMode.Normal
        };
    }
    
    /// <summary>
    /// Check for rapid VIX spike (crisis acceleration signal)
    /// </summary>
    public bool IsVIXSpiking(double currentVIX, double[] recentVIX)
    {
        if (recentVIX.Length < 3) return false;
        
        // Check for >50% VIX increase in 3 days or less
        var baseVIX = recentVIX.Take(recentVIX.Length - 1).Average();
        var spikeRatio = currentVIX / baseVIX;
        
        return currentVIX > 50 && spikeRatio > 1.5;
    }
    
    /// <summary>
    /// Get crisis mode description and recommended actions
    /// </summary>
    public CrisisInfo GetCrisisInfo(CrisisMode mode)
    {
        return mode switch
        {
            CrisisMode.BlackSwan => new CrisisInfo
            {
                Description = "Black Swan Event - Extreme market stress",
                RecommendedAction = "Emergency protocols: Tail hedging, volatility expansion",
                PositionSizeMultiplier = 0.5m, // Reduce size due to extreme risk
                MinConfidenceOverride = 0.1m // Trade even with very low confidence
            },
            CrisisMode.Crisis => new CrisisInfo
            {
                Description = "Market Crisis - High stress environment",
                RecommendedAction = "Crisis strategies: Long volatility, mean reversion",
                PositionSizeMultiplier = 0.75m,
                MinConfidenceOverride = 0.3m
            },
            CrisisMode.Elevated => new CrisisInfo
            {
                Description = "Elevated Stress - Increased market volatility",
                RecommendedAction = "Defensive strategies: Reduce exposure, increase hedging",
                PositionSizeMultiplier = 0.9m,
                MinConfidenceOverride = 0.5m
            },
            _ => new CrisisInfo
            {
                Description = "Normal Market - Standard operations",
                RecommendedAction = "Standard strategies based on regime analysis",
                PositionSizeMultiplier = 1.0m,
                MinConfidenceOverride = 0.6m // Normal confidence threshold
            }
        };
    }
}

/// <summary>
/// Crisis information and recommendations
/// </summary>
public class CrisisInfo
{
    public string Description { get; set; } = "";
    public string RecommendedAction { get; set; } = "";
    public decimal PositionSizeMultiplier { get; set; } = 1.0m;
    public decimal MinConfidenceOverride { get; set; } = 0.6m;
}