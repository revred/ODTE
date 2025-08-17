namespace ODTE.Execution.Models;

/// <summary>
/// Current market state used for execution modeling calibration.
/// Combines time-of-day, volatility regime, and event risk factors.
/// </summary>
public record MarketState
{
    public DateTime Timestamp { get; init; }
    public decimal VIX { get; init; }
    public VIXRegime VIXRegime { get; init; }
    public TimeOfDayRegime TimeRegime { get; init; }
    public List<string> ActiveEvents { get; init; } = new();
    public decimal UnderlyingPrice { get; init; }
    public int DaysToExpiry { get; init; }

    /// <summary>
    /// Market stress indicator (0.0 = calm, 1.0 = extreme stress)
    /// </summary>
    public decimal StressLevel { get; init; }

    /// <summary>
    /// Volatility term structure slope
    /// </summary>
    public decimal TermStructureSlope { get; init; }

    /// <summary>
    /// Put/call skew indicator
    /// </summary>
    public decimal PutCallSkew { get; init; }

    /// <summary>
    /// Options volume rank (percentile)
    /// </summary>
    public decimal VolumeRank { get; init; }

    /// <summary>
    /// Check if we're in a high-risk event window
    /// </summary>
    public bool IsEventRisk => ActiveEvents.Any() || IsOpenCloseWindow;

    /// <summary>
    /// Check if we're near market open/close
    /// </summary>
    public bool IsOpenCloseWindow
    {
        get
        {
            var marketOpen = Timestamp.Date.AddHours(9.5); // 9:30 AM ET
            var marketClose = Timestamp.Date.AddHours(16);  // 4:00 PM ET

            return Math.Abs((Timestamp - marketOpen).TotalMinutes) <= 10 ||
                   Math.Abs((Timestamp - marketClose).TotalMinutes) <= 10;
        }
    }

    /// <summary>
    /// Calculate adjusted spread multiplier based on market conditions
    /// </summary>
    public decimal GetSpreadMultiplier()
    {
        var multiplier = 1.0m;

        // VIX regime adjustments
        multiplier *= VIXRegime switch
        {
            VIXRegime.Low => 0.9m,
            VIXRegime.Normal => 1.0m,
            VIXRegime.High => 1.3m,
            VIXRegime.Extreme => 2.0m,
            _ => 1.0m
        };

        // Time-of-day adjustments
        multiplier *= TimeRegime switch
        {
            TimeOfDayRegime.PreMarket => 2.0m,
            TimeOfDayRegime.Open => 1.5m,
            TimeOfDayRegime.MidDay => 1.0m,
            TimeOfDayRegime.LateDay => 1.2m,
            TimeOfDayRegime.Close => 1.8m,
            TimeOfDayRegime.PostMarket => 2.5m,
            _ => 1.0m
        };

        // Event risk adjustments
        if (IsEventRisk)
            multiplier *= 1.5m;

        return multiplier;
    }
}

public enum VIXRegime
{
    Low,        // VIX < 14
    Normal,     // VIX 14-20
    High,       // VIX 20-30
    Extreme     // VIX > 30
}

public enum TimeOfDayRegime
{
    PreMarket,   // Before 9:30 AM
    Open,        // 9:30-10:30 AM
    MidDay,      // 10:30 AM-3:00 PM
    LateDay,     // 3:00-3:50 PM
    Close,       // 3:50-4:00 PM
    PostMarket   // After 4:00 PM
}

/// <summary>
/// Factory for creating MarketState from current conditions
/// </summary>
public static class MarketStateFactory
{
    public static MarketState Create(DateTime timestamp, decimal vix, decimal underlyingPrice, int daysToExpiry)
    {
        return new MarketState
        {
            Timestamp = timestamp,
            VIX = vix,
            VIXRegime = ClassifyVIXRegime(vix),
            TimeRegime = ClassifyTimeRegime(timestamp),
            UnderlyingPrice = underlyingPrice,
            DaysToExpiry = daysToExpiry,
            StressLevel = CalculateStressLevel(vix),
            ActiveEvents = DetectActiveEvents(timestamp)
        };
    }

    private static VIXRegime ClassifyVIXRegime(decimal vix) => vix switch
    {
        < 14 => VIXRegime.Low,
        < 20 => VIXRegime.Normal,
        < 30 => VIXRegime.High,
        _ => VIXRegime.Extreme
    };

    private static TimeOfDayRegime ClassifyTimeRegime(DateTime timestamp)
    {
        var timeOfDay = timestamp.TimeOfDay;
        var marketOpen = TimeSpan.FromHours(9.5);   // 9:30 AM
        var marketClose = TimeSpan.FromHours(16);   // 4:00 PM

        if (timeOfDay < TimeSpan.FromHours(9.5))
            return TimeOfDayRegime.PreMarket;
        else if (timeOfDay < TimeSpan.FromHours(10.5))
            return TimeOfDayRegime.Open;
        else if (timeOfDay < TimeSpan.FromHours(15))
            return TimeOfDayRegime.MidDay;
        else if (timeOfDay < TimeSpan.FromHours(15.83)) // 3:50 PM
            return TimeOfDayRegime.LateDay;
        else if (timeOfDay < TimeSpan.FromHours(16))
            return TimeOfDayRegime.Close;
        else
            return TimeOfDayRegime.PostMarket;
    }

    private static decimal CalculateStressLevel(decimal vix)
    {
        // Normalize VIX to 0-1 stress scale
        return Math.Min(1.0m, Math.Max(0.0m, (vix - 10m) / 50m));
    }

    private static List<string> DetectActiveEvents(DateTime timestamp)
    {
        var events = new List<string>();

        // Add FOMC, CPI, OPEX detection logic here
        // For now, return empty list

        return events;
    }
}