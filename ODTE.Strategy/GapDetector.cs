using System;
using System.Collections.Generic;
using System.Linq;

namespace ODTE.Strategy;

/// <summary>
/// Gap Opening Detection System
/// 
/// PURPOSE: Detect overnight gaps that signal crisis events or earnings surprises
/// HISTORICAL IMPACT:
/// - China Devaluation: -4.2% gap opening (Aug 10, 2015)
/// - COVID-19 Peak: -7.6% gap down (March 16, 2020)  
/// - Fed Rate Surprise: -2.9% gap (Dec 19, 2018)
/// 
/// STRATEGY IMPACT:
/// - Gap downs >2%: Iron Condors immediately underwater
/// - Gap ups >2%: Put spreads at risk
/// - Need immediate position adjustment or hedging
/// </summary>
public class GapDetector
{
    private readonly Queue<PriceDataPoint> _priceHistory;
    private readonly int _maxHistoryDays;
    
    public GapDetector(int maxHistoryDays = 10)
    {
        _maxHistoryDays = maxHistoryDays;
        _priceHistory = new Queue<PriceDataPoint>();
    }
    
    /// <summary>
    /// Detect gap opening and classify severity
    /// </summary>
    public GapResult DetectGap(DateTime date, decimal openPrice, decimal previousClose, decimal highPrice, decimal lowPrice)
    {
        // Calculate gap percentage
        var gapPercentage = (openPrice - previousClose) / previousClose * 100;
        
        // Add to history
        var dataPoint = new PriceDataPoint
        {
            Date = date,
            Open = openPrice,
            High = highPrice,
            Low = lowPrice,
            Close = previousClose, // Previous close for gap calculation
            GapPercentage = gapPercentage
        };
        
        _priceHistory.Enqueue(dataPoint);
        while (_priceHistory.Count > _maxHistoryDays)
        {
            _priceHistory.Dequeue();
        }
        
        // Classify gap severity
        var gapSeverity = ClassifyGapSeverity(gapPercentage);
        var gapDirection = gapPercentage > 0 ? GapDirection.Up : GapDirection.Down;
        
        // Check for gap patterns
        var isGapAndRun = CheckGapAndRun(openPrice, highPrice, lowPrice, gapDirection);
        var isGapFill = CheckPotentialGapFill(gapPercentage, openPrice, previousClose);
        var isBreakaway = CheckBreakawayPattern(gapPercentage);
        
        var result = new GapResult
        {
            IsGap = Math.Abs(gapPercentage) > 0.5m, // >0.5% considered a gap
            GapPercentage = gapPercentage,
            Direction = gapDirection,
            Severity = gapSeverity,
            PatternType = DeterminePatternType(gapPercentage, isGapAndRun, isGapFill, isBreakaway),
            RecommendedAction = GetRecommendedAction(gapSeverity, gapDirection),
            PositionAdjustment = GetPositionAdjustment(gapSeverity, gapDirection),
            ConfidenceOverride = GetConfidenceOverride(gapSeverity)
        };
        
        if (result.IsGap && Math.Abs(gapPercentage) > 1.0m)
        {
            Console.WriteLine($"📊 GAP DETECTED: {gapPercentage:F2}% {gapDirection} - {result.PatternType}");
            Console.WriteLine($"   Severity: {gapSeverity} | Action: {result.RecommendedAction}");
        }
        
        return result;
    }
    
    /// <summary>
    /// Classify gap severity based on percentage
    /// </summary>
    private GapSeverity ClassifyGapSeverity(decimal gapPercentage)
    {
        var absGap = Math.Abs(gapPercentage);
        
        return absGap switch
        {
            > 5.0m => GapSeverity.BlackSwan,    // >5% gap - circuit breaker territory
            > 3.0m => GapSeverity.Extreme,     // >3% gap - major event
            > 2.0m => GapSeverity.High,        // >2% gap - significant event
            > 1.0m => GapSeverity.Moderate,    // >1% gap - notable event
            > 0.5m => GapSeverity.Minor,       // >0.5% gap - minor event
            _ => GapSeverity.None               // <0.5% not considered a gap
        };
    }
    
    /// <summary>
    /// Check for gap-and-run pattern (gap continues in same direction)
    /// </summary>
    private bool CheckGapAndRun(decimal open, decimal high, decimal low, GapDirection direction)
    {
        if (direction == GapDirection.Up)
        {
            // Gap up and continue higher: close near high
            return high > open * 1.005m; // Continued higher by >0.5%
        }
        else
        {
            // Gap down and continue lower: close near low
            return low < open * 0.995m; // Continued lower by >0.5%
        }
    }
    
    /// <summary>
    /// Check potential for gap fill (return to previous close)
    /// </summary>
    private bool CheckPotentialGapFill(decimal gapPercentage, decimal open, decimal previousClose)
    {
        // Small gaps more likely to fill
        var absGap = Math.Abs(gapPercentage);
        
        if (absGap < 1.0m) return true;  // Small gaps often fill
        if (absGap < 2.0m) return false; // Medium gaps may or may not fill
        return false; // Large gaps rarely fill same day
    }
    
    /// <summary>
    /// Check for breakaway gap pattern (strong momentum continuation)
    /// </summary>
    private bool CheckBreakawayPattern(decimal gapPercentage)
    {
        var absGap = Math.Abs(gapPercentage);
        
        // Breakaway gaps are typically >2% with strong volume
        return absGap > 2.0m;
    }
    
    /// <summary>
    /// Determine overall gap pattern type
    /// </summary>
    private GapPattern DeterminePatternType(decimal gapPercentage, bool isGapAndRun, bool isGapFill, bool isBreakaway)
    {
        if (isBreakaway) return GapPattern.Breakaway;
        if (isGapAndRun) return GapPattern.GapAndRun;
        if (isGapFill) return GapPattern.GapFill;
        
        return Math.Abs(gapPercentage) switch
        {
            > 3.0m => GapPattern.Crisis,
            > 1.0m => GapPattern.Event,
            _ => GapPattern.Normal
        };
    }
    
    /// <summary>
    /// Get recommended action based on gap characteristics
    /// </summary>
    private string GetRecommendedAction(GapSeverity severity, GapDirection direction)
    {
        return severity switch
        {
            GapSeverity.BlackSwan => $"EMERGENCY: Activate crisis protocols, immediate hedging required",
            GapSeverity.Extreme => $"URGENT: Gap {direction} >3% - adjust all positions, crisis mode",
            GapSeverity.High => $"Gap {direction} >2% - hedge existing positions, reduce new exposure",
            GapSeverity.Moderate => $"Gap {direction} >1% - monitor positions closely, prepare adjustments",
            GapSeverity.Minor => $"Small gap {direction} - watch for fill or continuation",
            _ => "Normal opening - continue standard operations"
        };
    }
    
    /// <summary>
    /// Get position adjustment recommendations
    /// </summary>
    private string GetPositionAdjustment(GapSeverity severity, GapDirection direction)
    {
        return severity switch
        {
            GapSeverity.BlackSwan => "Close all positions, switch to Black Swan strategy",
            GapSeverity.Extreme => direction == GapDirection.Down ? "Hedge with protective puts, reduce short vol" : "Hedge with protective calls, reduce short call exposure",
            GapSeverity.High => direction == GapDirection.Down ? "Add put protection, close credit spreads" : "Add call protection, close put spreads",
            GapSeverity.Moderate => "Monitor delta exposure, prepare for adjustments",
            _ => "No immediate adjustment needed"
        };
    }
    
    /// <summary>
    /// Get confidence threshold override for gap conditions
    /// </summary>
    private decimal GetConfidenceOverride(GapSeverity severity)
    {
        return severity switch
        {
            GapSeverity.BlackSwan => 0.1m, // Trade with any confidence in crisis
            GapSeverity.Extreme => 0.2m,   // Lower threshold for extreme gaps
            GapSeverity.High => 0.3m,      // Moderately lower threshold
            GapSeverity.Moderate => 0.4m,  // Slightly lower threshold
            _ => 0.6m                       // Normal threshold
        };
    }
    
    /// <summary>
    /// Get gap statistics from recent history
    /// </summary>
    public GapStatistics GetGapStatistics()
    {
        if (!_priceHistory.Any()) return new GapStatistics();
        
        var gaps = _priceHistory.Where(p => Math.Abs(p.GapPercentage) > 0.5m).ToArray();
        var recentGaps = gaps.TakeLast(5).ToArray();
        
        return new GapStatistics
        {
            TotalGaps = gaps.Length,
            RecentGapCount = recentGaps.Length,
            AverageGapSize = gaps.Any() ? gaps.Average(g => Math.Abs(g.GapPercentage)) : 0,
            LargestGap = gaps.Any() ? gaps.Max(g => Math.Abs(g.GapPercentage)) : 0,
            GapFrequency = gaps.Length / (double)Math.Max(_priceHistory.Count, 1) * 100
        };
    }
}

/// <summary>
/// Price data point for gap calculation
/// </summary>
public class PriceDataPoint
{
    public DateTime Date { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal GapPercentage { get; set; }
}

/// <summary>
/// Gap detection result
/// </summary>
public class GapResult
{
    public bool IsGap { get; set; }
    public decimal GapPercentage { get; set; }
    public GapDirection Direction { get; set; }
    public GapSeverity Severity { get; set; }
    public GapPattern PatternType { get; set; }
    public string RecommendedAction { get; set; } = "";
    public string PositionAdjustment { get; set; } = "";
    public decimal ConfidenceOverride { get; set; } = 0.6m;
}

/// <summary>
/// Gap statistics summary
/// </summary>
public class GapStatistics
{
    public int TotalGaps { get; set; }
    public int RecentGapCount { get; set; }
    public decimal AverageGapSize { get; set; }
    public decimal LargestGap { get; set; }
    public double GapFrequency { get; set; }
}

/// <summary>
/// Gap direction
/// </summary>
public enum GapDirection
{
    Up,
    Down
}

/// <summary>
/// Gap severity levels
/// </summary>
public enum GapSeverity
{
    None,
    Minor,
    Moderate,
    High,
    Extreme,
    BlackSwan
}

/// <summary>
/// Gap pattern types
/// </summary>
public enum GapPattern
{
    Normal,     // Small gap, likely to fill
    Event,      // Medium gap from news/earnings
    GapFill,    // Gap likely to fill during day
    GapAndRun,  // Gap continues in same direction
    Breakaway,  // Strong momentum gap
    Crisis      // Crisis-driven gap
}