using ODTE.Backtest.Core;

namespace ODTE.Backtest.Engine;

/// <summary>
/// Interface for risk management and position limits.
/// WHY: Enables testability through mocking while maintaining clean separation of concerns.
/// 
/// CONTRACT:
/// - CanAdd(): Pre-trade risk validation (time, limits, position counts)
/// - CanAddOrder(): Per-order risk validation with Fibonacci budget enforcement
/// - RegisterOpen()/RegisterClose(): Position lifecycle tracking
/// 
/// RISK SYSTEMS:
/// - Daily loss limits with Reverse Fibonacci scaling
/// - Position count limits per strategy type
/// - Time-based restrictions (gamma hour protection)
/// - Event proximity blocking
/// </summary>
public interface IRiskManager
{
    /// <summary>
    /// Check if new position can be added based on time and position limits.
    /// </summary>
    /// <param name="now">Current timestamp</param>
    /// <param name="d">Strategy decision type</param>
    /// <returns>True if position can be added</returns>
    bool CanAdd(DateTime now, Decision d);

    /// <summary>
    /// Validate specific order against Fibonacci risk budget.
    /// </summary>
    /// <param name="order">Spread order to validate</param>
    /// <returns>True if order fits within risk budget</returns>
    bool CanAddOrder(SpreadOrder order);

    /// <summary>
    /// Register position opening for tracking and limits.
    /// </summary>
    /// <param name="d">Strategy decision type</param>
    void RegisterOpen(Decision d);

    /// <summary>
    /// Register position closing with P&L for budget tracking.
    /// </summary>
    /// <param name="d">Strategy decision type</param>
    /// <param name="pnl">Profit/loss from closed position</param>
    void RegisterClose(Decision d, double pnl);
}