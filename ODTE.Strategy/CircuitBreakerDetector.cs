using System;
using System.Collections.Generic;
using System.Linq;

namespace ODTE.Strategy;

/// <summary>
/// Circuit Breaker Detection and Response System
/// 
/// PURPOSE: Detect circuit breaker triggers and market halts
/// CIRCUIT BREAKER LEVELS (S&P 500):
/// - Level 1: 7% decline before 3:25 PM EST (15-minute halt)
/// - Level 2: 13% decline before 3:25 PM EST (15-minute halt)  
/// - Level 3: 20% decline (trading halted for remainder of day)
/// 
/// HISTORICAL TRIGGERS:
/// - COVID-19: March 9, 12, 16, 18, 2020 (multiple Level 1 & 2)
/// - Black Monday 1987: Would have triggered Level 3 (-22.6%)
/// - China Devaluation 2015: Nearly triggered Level 1
/// 
/// TRADING IMPLICATIONS:
/// - All options positions frozen during halt
/// - Massive volatility expansion upon reopening
/// - Gap risk on halt resumption
/// - Need immediate crisis strategy activation
/// </summary>
public class CircuitBreakerDetector
{
    private readonly List<CircuitBreakerEvent> _eventHistory;
    private readonly TimeZoneInfo _eastCoastTime;
    private decimal _marketOpenPrice;
    private bool _marketOpenSet;
    
    public CircuitBreakerDetector()
    {
        _eventHistory = new List<CircuitBreakerEvent>();
        _eastCoastTime = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        _marketOpenSet = false;
    }
    
    /// <summary>
    /// Set market opening price for circuit breaker calculations
    /// </summary>
    public void SetMarketOpen(decimal openPrice, DateTime openTime)
    {
        _marketOpenPrice = openPrice;
        _marketOpenSet = true;
        
        Console.WriteLine($"📊 Market Open Set: ${openPrice:F2} at {openTime:HH:mm} EST");
    }
    
    /// <summary>
    /// Check for circuit breaker conditions
    /// </summary>
    public CircuitBreakerResult CheckCircuitBreaker(DateTime currentTime, decimal currentPrice, decimal volumeSpike = 1.0m)
    {
        if (!_marketOpenSet)
        {
            return new CircuitBreakerResult
            {
                IsTriggered = false,
                Level = CircuitBreakerLevel.None,
                Message = "Market open price not set"
            };
        }
        
        // Calculate percentage decline from market open
        var declinePercentage = (_marketOpenPrice - currentPrice) / _marketOpenPrice * 100;
        
        // Convert to Eastern Time for circuit breaker rules
        var estTime = TimeZoneInfo.ConvertTime(currentTime, _eastCoastTime);
        var cutoffTime = new DateTime(estTime.Year, estTime.Month, estTime.Day, 15, 25, 0); // 3:25 PM EST
        var isBeforeCutoff = estTime < cutoffTime;
        
        // Determine circuit breaker level
        var level = DetermineCircuitBreakerLevel(declinePercentage, isBeforeCutoff);
        var wouldTrigger = level != CircuitBreakerLevel.None;
        
        // Check for near-trigger conditions (within 0.5% of circuit breaker)
        var nearTrigger = CheckNearTriggerConditions(declinePercentage, isBeforeCutoff);
        
        var result = new CircuitBreakerResult
        {
            IsTriggered = wouldTrigger,
            Level = level,
            DeclinePercentage = declinePercentage,
            TimeToBreaker = CalculateTimeToBreaker(declinePercentage),
            IsNearTrigger = nearTrigger,
            Message = GenerateMessage(level, declinePercentage, isBeforeCutoff, nearTrigger),
            RecommendedAction = GetRecommendedAction(level, nearTrigger, declinePercentage),
            TradingHaltDuration = GetHaltDuration(level, isBeforeCutoff),
            MarketReopenEstimate = estTime.Add(GetHaltDuration(level, isBeforeCutoff))
        };
        
        // Log circuit breaker events
        if (wouldTrigger || nearTrigger)
        {
            LogCircuitBreakerEvent(result, currentTime);
            Console.WriteLine($"⚡ CIRCUIT BREAKER ALERT: {result.Message}");
            Console.WriteLine($"   Decline: {declinePercentage:F2}% | Action: {result.RecommendedAction}");
        }
        
        return result;
    }
    
    /// <summary>
    /// Determine circuit breaker level based on decline percentage
    /// </summary>
    private CircuitBreakerLevel DetermineCircuitBreakerLevel(decimal decline, bool isBeforeCutoff)
    {
        if (decline >= 20m)
        {
            return CircuitBreakerLevel.Level3; // Always triggers regardless of time
        }
        
        if (!isBeforeCutoff) // After 3:25 PM EST, only Level 3 can trigger
        {
            return CircuitBreakerLevel.None;
        }
        
        return decline switch
        {
            >= 13m => CircuitBreakerLevel.Level2, // 13% decline
            >= 7m => CircuitBreakerLevel.Level1,  // 7% decline
            _ => CircuitBreakerLevel.None
        };
    }
    
    /// <summary>
    /// Check for near-trigger conditions (within 0.5% of circuit breaker)
    /// </summary>
    private bool CheckNearTriggerConditions(decimal decline, bool isBeforeCutoff)
    {
        if (decline >= 19.5m) return true; // Near Level 3
        
        if (isBeforeCutoff)
        {
            if (decline >= 12.5m) return true; // Near Level 2
            if (decline >= 6.5m) return true;  // Near Level 1
        }
        
        return false;
    }
    
    /// <summary>
    /// Calculate percentage points to next circuit breaker
    /// </summary>
    private decimal CalculateTimeToBreaker(decimal currentDecline)
    {
        if (currentDecline >= 20m) return 0; // Already at Level 3
        if (currentDecline >= 13m) return 20m - currentDecline; // To Level 3
        if (currentDecline >= 7m) return 13m - currentDecline;  // To Level 2
        return 7m - currentDecline; // To Level 1
    }
    
    /// <summary>
    /// Generate descriptive message for circuit breaker status
    /// </summary>
    private string GenerateMessage(CircuitBreakerLevel level, decimal decline, bool isBeforeCutoff, bool nearTrigger)
    {
        return level switch
        {
            CircuitBreakerLevel.Level3 => $"LEVEL 3 TRIGGERED: {decline:F2}% decline - Trading halted for day",
            CircuitBreakerLevel.Level2 => $"LEVEL 2 TRIGGERED: {decline:F2}% decline - 15-minute halt",
            CircuitBreakerLevel.Level1 => $"LEVEL 1 TRIGGERED: {decline:F2}% decline - 15-minute halt",
            _ when nearTrigger => $"NEAR TRIGGER: {decline:F2}% decline - Circuit breaker imminent",
            _ => $"Market decline: {decline:F2}% - Monitoring for circuit breakers"
        };
    }
    
    /// <summary>
    /// Get recommended action based on circuit breaker conditions
    /// </summary>
    private string GetRecommendedAction(CircuitBreakerLevel level, bool nearTrigger, decimal decline)
    {
        return level switch
        {
            CircuitBreakerLevel.Level3 => "EMERGENCY: All trading halted - prepare for next day gap",
            CircuitBreakerLevel.Level2 => "CRISIS: 15-min halt - activate Black Swan strategy immediately",
            CircuitBreakerLevel.Level1 => "URGENT: 15-min halt - switch to crisis mode, hedge all positions",
            _ when nearTrigger => "PREPARE: Circuit breaker imminent - reduce exposure, prepare hedges",
            _ when decline > 5m => "MONITOR: Significant decline - prepare for crisis protocols",
            _ => "Normal monitoring - continue operations"
        };
    }
    
    /// <summary>
    /// Get expected halt duration
    /// </summary>
    private TimeSpan GetHaltDuration(CircuitBreakerLevel level, bool isBeforeCutoff)
    {
        return level switch
        {
            CircuitBreakerLevel.Level3 => TimeSpan.FromHours(24), // Rest of day + overnight
            CircuitBreakerLevel.Level2 when isBeforeCutoff => TimeSpan.FromMinutes(15),
            CircuitBreakerLevel.Level1 when isBeforeCutoff => TimeSpan.FromMinutes(15),
            _ => TimeSpan.Zero
        };
    }
    
    /// <summary>
    /// Log circuit breaker event for historical analysis
    /// </summary>
    private void LogCircuitBreakerEvent(CircuitBreakerResult result, DateTime eventTime)
    {
        var cbEvent = new CircuitBreakerEvent
        {
            Timestamp = eventTime,
            Level = result.Level,
            DeclinePercentage = result.DeclinePercentage,
            WasTriggered = result.IsTriggered,
            Message = result.Message
        };
        
        _eventHistory.Add(cbEvent);
        
        // Keep only last 100 events
        if (_eventHistory.Count > 100)
        {
            _eventHistory.RemoveAt(0);
        }
    }
    
    /// <summary>
    /// Get circuit breaker statistics
    /// </summary>
    public CircuitBreakerStatistics GetStatistics()
    {
        var triggeredEvents = _eventHistory.Where(e => e.WasTriggered).ToArray();
        
        return new CircuitBreakerStatistics
        {
            TotalEvents = _eventHistory.Count,
            Level1Triggers = triggeredEvents.Count(e => e.Level == CircuitBreakerLevel.Level1),
            Level2Triggers = triggeredEvents.Count(e => e.Level == CircuitBreakerLevel.Level2),
            Level3Triggers = triggeredEvents.Count(e => e.Level == CircuitBreakerLevel.Level3),
            AverageDeclineAtTrigger = triggeredEvents.Any() ? triggeredEvents.Average(e => e.DeclinePercentage) : 0,
            MostRecentTrigger = triggeredEvents.LastOrDefault()?.Timestamp,
            DaysSinceLastTrigger = triggeredEvents.Any() ? 
                (DateTime.Now - triggeredEvents.Last().Timestamp).TotalDays : 
                double.MaxValue
        };
    }
}

/// <summary>
/// Circuit breaker event record
/// </summary>
public class CircuitBreakerEvent
{
    public DateTime Timestamp { get; set; }
    public CircuitBreakerLevel Level { get; set; }
    public decimal DeclinePercentage { get; set; }
    public bool WasTriggered { get; set; }
    public string Message { get; set; } = "";
}

/// <summary>
/// Circuit breaker detection result
/// </summary>
public class CircuitBreakerResult
{
    public bool IsTriggered { get; set; }
    public CircuitBreakerLevel Level { get; set; }
    public decimal DeclinePercentage { get; set; }
    public decimal TimeToBreaker { get; set; }
    public bool IsNearTrigger { get; set; }
    public string Message { get; set; } = "";
    public string RecommendedAction { get; set; } = "";
    public TimeSpan TradingHaltDuration { get; set; }
    public DateTime MarketReopenEstimate { get; set; }
}

/// <summary>
/// Circuit breaker statistics
/// </summary>
public class CircuitBreakerStatistics
{
    public int TotalEvents { get; set; }
    public int Level1Triggers { get; set; }
    public int Level2Triggers { get; set; }
    public int Level3Triggers { get; set; }
    public decimal AverageDeclineAtTrigger { get; set; }
    public DateTime? MostRecentTrigger { get; set; }
    public double DaysSinceLastTrigger { get; set; }
}

/// <summary>
/// Circuit breaker levels
/// </summary>
public enum CircuitBreakerLevel
{
    None,
    Level1,    // 7% decline
    Level2,    // 13% decline  
    Level3     // 20% decline
}