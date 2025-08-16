using System;

namespace ODTE.Strategy;

/// <summary>
/// Emergency Stop-Loss System
/// Implements circuit breaker for portfolio protection
/// </summary>
public class EmergencyStopLoss
{
    private decimal _dailyLossLimit;
    private decimal _currentDayLoss = 0m;
    
    public EmergencyStopLoss(decimal dailyLossLimit)
    {
        _dailyLossLimit = dailyLossLimit;
    }
    
    public bool ShouldTriggerStopLoss(decimal currentPnL, CrisisMode crisisMode)
    {
        _currentDayLoss = Math.Abs(Math.Min(0, currentPnL));
        
        var adjustedLimit = crisisMode switch
        {
            CrisisMode.BlackSwan => _dailyLossLimit * 2.0m, // Allow larger losses in extreme events
            CrisisMode.Crisis => _dailyLossLimit * 1.5m,    // Moderate increase
            _ => _dailyLossLimit                             // Normal limit
        };
        
        return _currentDayLoss > adjustedLimit;
    }
}