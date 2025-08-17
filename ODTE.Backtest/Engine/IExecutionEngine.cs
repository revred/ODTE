using ODTE.Backtest.Core;

namespace ODTE.Backtest.Engine;

/// <summary>
/// Interface for trade execution and position lifecycle management.
/// WHY: Enables testability through mocking while maintaining clean separation of concerns.
/// 
/// CONTRACT:
/// - TryEnter(): Models realistic entry execution with slippage
/// - ShouldExit(): Evaluates stop-loss and exit conditions
/// - LogTradeClosureAsync(): Records trade for forensics and analytics
/// 
/// EXECUTION MODELING:
/// - Entry fills include adverse slippage from theoretical mid-prices
/// - Exit conditions include both price-based and delta-based stops
/// - Gamma protection essential for 0DTE options near expiration
/// </summary>
public interface IExecutionEngine : IDisposable
{
    /// <summary>
    /// Attempt to enter position with realistic slippage modeling.
    /// Returns null if order cannot be filled.
    /// </summary>
    /// <param name="order">Spread order to execute</param>
    /// <returns>Open position or null if unfillable</returns>
    OpenPosition? TryEnter(SpreadOrder order);

    /// <summary>
    /// Evaluate exit conditions for open position.
    /// Returns (should_exit, exit_price, reason) tuple.
    /// </summary>
    /// <param name="pos">Open position to evaluate</param>
    /// <param name="currentSpreadValue">Current market value of the spread</param>
    /// <param name="shortStrikeDelta">Delta of the short strike (gamma protection)</param>
    /// <param name="now">Current timestamp</param>
    /// <returns>Exit decision with price and reason</returns>
    (bool exit, double exitPrice, string reason) ShouldExit(
        OpenPosition pos,
        double currentSpreadValue,
        double shortStrikeDelta,
        DateTime now);

    /// <summary>
    /// Log trade closure for forensics and pattern analysis.
    /// </summary>
    /// <param name="position">Closed position details</param>
    /// <param name="exitPrice">Actual exit price achieved</param>
    /// <param name="exitReason">Why trade was closed</param>
    /// <param name="marketRegime">Current market conditions</param>
    Task LogTradeClosureAsync(OpenPosition position, double exitPrice, string exitReason, string marketRegime = "unknown");
}