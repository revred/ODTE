using ODTE.Backtest.Data;

namespace ODTE.Backtest.Signals;

/// <summary>
/// Interface for market regime scoring and classification.
/// WHY: Enables testability through mocking while maintaining clean separation of concerns.
/// 
/// CONTRACT:
/// - Score(): Core regime analysis returning (score, regime_flags)
/// - Score > 2: Favorable conditions for directional strategies
/// - Score 0-1: Neutral, consider range strategies  
/// - Score ≤ -1: Risk-off, no new positions
/// 
/// REGIME FLAGS:
/// - calmRange: Low volatility + no trend (favor iron condors)
/// - trendBiasUp: Bullish trend detected (favor put credit spreads)
/// - trendBiasDown: Bearish trend detected (favor call credit spreads)
/// </summary>
public interface IRegimeScorer
{
    /// <summary>
    /// Calculate market regime score and directional bias.
    /// Returns (score, calm_range_flag, bullish_trend_flag, bearish_trend_flag).
    /// </summary>
    /// <param name="now">Current timestamp for analysis</param>
    /// <param name="md">Market data provider</param>
    /// <param name="cal">Economic calendar for event proximity</param>
    /// <returns>Tuple of (score, calmRange, trendBiasUp, trendBiasDown)</returns>
    (int score, bool calmRange, bool trendBiasUp, bool trendBiasDown) Score(
        DateTime now, 
        IMarketData md, 
        IEconCalendar cal);
}