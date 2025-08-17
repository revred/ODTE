using ODTE.Execution.Models;

namespace ODTE.Execution.Interfaces;

/// <summary>
/// Core interface for realistic fill simulation with market microstructure awareness.
/// Replaces optimistic/idealized fills with calibrated execution friction.
/// </summary>
public interface IFillEngine
{
    /// <summary>
    /// Simulate realistic fill for an order given market conditions.
    /// Returns null if order cannot be filled (e.g., insufficient liquidity).
    /// </summary>
    /// <param name="order">Order to execute</param>
    /// <param name="quote">Current market quote (bid/ask/size)</param>
    /// <param name="profile">Execution profile (conservative/base/optimistic)</param>
    /// <param name="marketState">Current market conditions (VIX, time-of-day, etc.)</param>
    /// <returns>Fill result with realistic pricing and diagnostics</returns>
    Task<FillResult?> SimulateFillAsync(Order order, Quote quote, ExecutionProfile profile, MarketState marketState);

    /// <summary>
    /// Calculate worst-case fill price for risk management purposes.
    /// Used by RiskGate to ensure orders don't breach daily limits.
    /// </summary>
    /// <param name="order">Order to evaluate</param>
    /// <param name="quote">Current market quote</param>
    /// <param name="profile">Execution profile</param>
    /// <returns>Worst-case fill price including all penalties</returns>
    decimal CalculateWorstCaseFill(Order order, Quote quote, ExecutionProfile profile);

    /// <summary>
    /// Get current execution profile being used.
    /// </summary>
    ExecutionProfile CurrentProfile { get; }

    /// <summary>
    /// Get daily execution metrics for audit compliance.
    /// </summary>
    ExecutionMetrics GetDailyMetrics(DateTime date);
}