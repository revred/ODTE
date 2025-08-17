namespace ODTE.Strategy;

/// <summary>
/// VIX Spike Detection System
/// 
/// PURPOSE: Detect rapid volatility expansions that signal crisis conditions
/// TRIGGER CONDITIONS:
/// - VIX >50 within 3 days or less
/// - >50% VIX increase in 1-3 day period
/// - VIX acceleration patterns (second derivative positive)
/// 
/// HISTORICAL EXAMPLES:
/// - COVID-19: VIX 12→82 in 5 days (Feb 2020)
/// - China Devaluation: VIX 13→53 in 2 days (Aug 2015)
/// - October 2018: VIX 12→50 in 3 days
/// </summary>
public class VIXSpikeDetector
{
    private readonly Queue<VIXDataPoint> _vixHistory;
    private readonly int _maxHistoryDays;

    public VIXSpikeDetector(int maxHistoryDays = 10)
    {
        _maxHistoryDays = maxHistoryDays;
        _vixHistory = new Queue<VIXDataPoint>();
    }

    /// <summary>
    /// Add new VIX data point and check for spike conditions
    /// </summary>
    public VIXSpikeResult DetectSpike(DateTime date, double vixLevel)
    {
        // Add new data point
        var dataPoint = new VIXDataPoint
        {
            Date = date,
            VIX = vixLevel,
            Timestamp = DateTime.Now
        };

        _vixHistory.Enqueue(dataPoint);

        // Maintain history size
        while (_vixHistory.Count > _maxHistoryDays)
        {
            _vixHistory.Dequeue();
        }

        // Need at least 2 points for spike detection
        if (_vixHistory.Count < 2)
        {
            return new VIXSpikeResult
            {
                IsSpike = false,
                SeverityLevel = SpikeSeverity.None,
                Description = "Insufficient data for spike detection"
            };
        }

        var currentVIX = vixLevel;
        var historyArray = _vixHistory.ToArray();

        // Check various spike patterns
        var absoluteSpike = CheckAbsoluteSpike(currentVIX);
        var rapidIncrease = CheckRapidIncrease(historyArray);
        var acceleration = CheckVIXAcceleration(historyArray);

        // Determine overall spike status
        var spikeResult = DetermineSpikeResult(absoluteSpike, rapidIncrease, acceleration, currentVIX, historyArray);

        if (spikeResult.IsSpike)
        {
            Console.WriteLine($"🚨 VIX SPIKE DETECTED: {spikeResult.Description}");
        }

        return spikeResult;
    }

    /// <summary>
    /// Check if VIX is above absolute spike threshold
    /// </summary>
    private SpikeResult CheckAbsoluteSpike(double currentVIX)
    {
        return currentVIX switch
        {
            > 80 => new SpikeResult { IsTriggered = true, Severity = SpikeSeverity.BlackSwan, Description = $"VIX {currentVIX:F1} - Black Swan territory" },
            > 50 => new SpikeResult { IsTriggered = true, Severity = SpikeSeverity.Extreme, Description = $"VIX {currentVIX:F1} - Extreme spike" },
            > 35 => new SpikeResult { IsTriggered = true, Severity = SpikeSeverity.High, Description = $"VIX {currentVIX:F1} - High volatility spike" },
            > 25 => new SpikeResult { IsTriggered = true, Severity = SpikeSeverity.Moderate, Description = $"VIX {currentVIX:F1} - Moderate spike" },
            _ => new SpikeResult { IsTriggered = false, Severity = SpikeSeverity.None, Description = "VIX below spike threshold" }
        };
    }

    /// <summary>
    /// Check for rapid VIX increase over 1-3 days
    /// </summary>
    private SpikeResult CheckRapidIncrease(VIXDataPoint[] history)
    {
        if (history.Length < 2) return new SpikeResult { IsTriggered = false };

        var current = history.Last().VIX;

        // Check 1-day increase
        var oneDayAgo = history[history.Length - 2].VIX;
        var oneDayIncrease = (current - oneDayAgo) / oneDayAgo;

        if (oneDayIncrease > 0.5 && current > 35) // >50% increase and above 35
        {
            return new SpikeResult
            {
                IsTriggered = true,
                Severity = SpikeSeverity.Extreme,
                Description = $"1-day VIX spike: {oneDayAgo:F1}→{current:F1} (+{oneDayIncrease:P0})"
            };
        }

        // Check 2-day increase if we have enough data
        if (history.Length >= 3)
        {
            var twoDayAgo = history[history.Length - 3].VIX;
            var twoDayIncrease = (current - twoDayAgo) / twoDayAgo;

            if (twoDayIncrease > 0.75 && current > 40) // >75% increase over 2 days
            {
                return new SpikeResult
                {
                    IsTriggered = true,
                    Severity = SpikeSeverity.Extreme,
                    Description = $"2-day VIX spike: {twoDayAgo:F1}→{current:F1} (+{twoDayIncrease:P0})"
                };
            }
        }

        // Check 3-day increase if we have enough data
        if (history.Length >= 4)
        {
            var threeDayAgo = history[history.Length - 4].VIX;
            var threeDayIncrease = (current - threeDayAgo) / threeDayAgo;

            if (threeDayIncrease > 1.0 && current > 45) // >100% increase over 3 days
            {
                return new SpikeResult
                {
                    IsTriggered = true,
                    Severity = SpikeSeverity.BlackSwan,
                    Description = $"3-day VIX explosion: {threeDayAgo:F1}→{current:F1} (+{threeDayIncrease:P0})"
                };
            }
        }

        return new SpikeResult { IsTriggered = false };
    }

    /// <summary>
    /// Check for VIX acceleration (increasing rate of change)
    /// </summary>
    private SpikeResult CheckVIXAcceleration(VIXDataPoint[] history)
    {
        if (history.Length < 3) return new SpikeResult { IsTriggered = false };

        // Calculate daily changes
        var changes = new List<double>();
        for (int i = 1; i < history.Length; i++)
        {
            changes.Add(history[i].VIX - history[i - 1].VIX);
        }

        // Check if changes are accelerating (getting larger)
        if (changes.Count >= 2)
        {
            var recentChange = changes.Last();
            var previousChange = changes[changes.Count - 2];

            var acceleration = recentChange - previousChange;

            if (acceleration > 5 && recentChange > 8 && history.Last().VIX > 30)
            {
                return new SpikeResult
                {
                    IsTriggered = true,
                    Severity = SpikeSeverity.High,
                    Description = $"VIX acceleration: +{previousChange:F1} then +{recentChange:F1} (accel: +{acceleration:F1})"
                };
            }
        }

        return new SpikeResult { IsTriggered = false };
    }

    /// <summary>
    /// Determine overall spike result from individual checks
    /// </summary>
    private VIXSpikeResult DetermineSpikeResult(SpikeResult absolute, SpikeResult rapid, SpikeResult accel, double currentVIX, VIXDataPoint[] history)
    {
        var triggeredResults = new[] { absolute, rapid, accel }.Where(r => r.IsTriggered).ToArray();

        if (!triggeredResults.Any())
        {
            return new VIXSpikeResult
            {
                IsSpike = false,
                SeverityLevel = SpikeSeverity.None,
                Description = $"VIX {currentVIX:F1} - Normal conditions"
            };
        }

        // Use highest severity level
        var maxSeverity = triggeredResults.Max(r => r.Severity);
        var primaryResult = triggeredResults.First(r => r.Severity == maxSeverity);

        // Add context from other triggered results
        var additionalContext = "";
        if (triggeredResults.Length > 1)
        {
            additionalContext = " | Multiple spike patterns detected";
        }

        return new VIXSpikeResult
        {
            IsSpike = true,
            SeverityLevel = maxSeverity,
            Description = primaryResult.Description + additionalContext,
            TriggeredPatterns = triggeredResults.Length,
            RecommendedAction = GetRecommendedAction(maxSeverity),
            ConfidenceOverride = GetConfidenceOverride(maxSeverity)
        };
    }

    /// <summary>
    /// Get recommended action based on spike severity
    /// </summary>
    private string GetRecommendedAction(SpikeSeverity severity)
    {
        return severity switch
        {
            SpikeSeverity.BlackSwan => "IMMEDIATE: Activate Black Swan strategy, tail hedge positions",
            SpikeSeverity.Extreme => "URGENT: Crisis mode activation, volatility expansion trades",
            SpikeSeverity.High => "Elevated stress protocols, increase position sizing for vol trades",
            SpikeSeverity.Moderate => "Monitor closely, prepare for crisis mode escalation",
            _ => "Continue normal operations"
        };
    }

    /// <summary>
    /// Get confidence threshold override for spike conditions
    /// </summary>
    private decimal GetConfidenceOverride(SpikeSeverity severity)
    {
        return severity switch
        {
            SpikeSeverity.BlackSwan => 0.1m, // Trade even with 10% confidence
            SpikeSeverity.Extreme => 0.2m,   // Trade even with 20% confidence
            SpikeSeverity.High => 0.3m,      // Trade even with 30% confidence
            SpikeSeverity.Moderate => 0.4m,  // Trade even with 40% confidence
            _ => 0.6m                         // Normal 60% confidence threshold
        };
    }
}

/// <summary>
/// VIX data point for historical tracking
/// </summary>
public class VIXDataPoint
{
    public DateTime Date { get; set; }
    public double VIX { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Individual spike detection result
/// </summary>
public class SpikeResult
{
    public bool IsTriggered { get; set; }
    public SpikeSeverity Severity { get; set; }
    public string Description { get; set; } = "";
}

/// <summary>
/// Comprehensive VIX spike detection result
/// </summary>
public class VIXSpikeResult
{
    public bool IsSpike { get; set; }
    public SpikeSeverity SeverityLevel { get; set; }
    public string Description { get; set; } = "";
    public int TriggeredPatterns { get; set; }
    public string RecommendedAction { get; set; } = "";
    public decimal ConfidenceOverride { get; set; } = 0.6m;
}

/// <summary>
/// Spike severity levels
/// </summary>
public enum SpikeSeverity
{
    None = 0,
    Moderate = 1,
    High = 2,
    Extreme = 3,
    BlackSwan = 4
}