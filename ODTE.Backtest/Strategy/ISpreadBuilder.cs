using ODTE.Backtest.Core;
using ODTE.Backtest.Data;

namespace ODTE.Backtest.Strategy;

/// <summary>
/// Interface for spread order construction from strategic decisions.
/// WHY: Enables testability through mocking while maintaining clean separation of concerns.
/// 
/// CONTRACT:
/// - TryBuild(): Converts regime decision into executable spread order
/// - Returns null if no valid spread can be constructed (illiquid, poor quality, etc.)
/// - Enforces risk management guardrails during construction
/// 
/// SPREAD TYPES:
/// - Decision.Condor → Iron Condor (sell both sides)
/// - Decision.SingleSidePut → Put credit spread (bullish)
/// - Decision.SingleSideCall → Call credit spread (bearish)
/// </summary>
public interface ISpreadBuilder
{
    /// <summary>
    /// Attempt to build a spread order from strategic decision.
    /// Returns null if no suitable spread can be constructed.
    /// </summary>
    /// <param name="now">Current timestamp for quote lookup</param>
    /// <param name="decision">Strategic decision (Condor, SingleSidePut, etc.)</param>
    /// <param name="md">Market data provider</param>
    /// <param name="od">Options data provider</param>
    /// <returns>Constructed spread order or null if unavailable</returns>
    SpreadOrder? TryBuild(DateTime now, Decision decision, IMarketData md, IOptionsData od);
}