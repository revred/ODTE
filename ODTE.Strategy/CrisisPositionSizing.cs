using System;

namespace ODTE.Strategy;

/// <summary>
/// Crisis Position Sizing Engine
/// Adjusts position sizes based on crisis conditions
/// </summary>
public class CrisisPositionSizing
{
    public decimal CalculateCrisisAdjustedSize(decimal baseSize, CrisisMode crisisMode, decimal vix)
    {
        var multiplier = crisisMode switch
        {
            CrisisMode.BlackSwan => 0.4m, // Reduce size significantly
            CrisisMode.Crisis => 0.6m,    // Moderate reduction
            CrisisMode.Elevated => 0.8m,  // Small reduction
            _ => 1.0m                      // Normal size
        };
        
        // Additional VIX-based adjustment
        if (vix > 50) multiplier *= 0.8m;
        if (vix > 70) multiplier *= 0.7m;
        
        return baseSize * multiplier;
    }
}