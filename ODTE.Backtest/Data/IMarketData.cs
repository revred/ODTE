using ODTE.Backtest.Core;

namespace ODTE.Backtest.Data;

/// <summary>
/// Abstraction for time-series market data (bars) and derived indicators.
/// WHY: Separates data source from strategy logic, enabling easy swapping between CSV → database → live feed.
/// 
/// DESIGN PHILOSOPHY:
/// - Provides both raw bars and calculated indicators (ATR, VWAP)
/// - Indicator calculations are centralized here for consistency
/// - Interface allows dependency injection for testing
/// 
/// DATA SOURCES:
/// - CSV files (prototype, backtesting)
/// - Database (production backtesting with large datasets)
/// - Live feeds (real-time trading via APIs)
/// 
/// INDICATORS PROVIDED:
/// - ATR (Average True Range): Volatility measurement for regime detection
/// - VWAP (Volume Weighted Average Price): Trend and momentum indicator
/// - Spot price: Current underlying price for options calculations
/// 
/// References:
/// - ATR: https://www.investopedia.com/articles/trading/08/average-true-range.asp
/// - VWAP: https://www.nasdaq.com/glossary/v/vwap
/// </summary>
public interface IMarketData
{
    /// <summary>
    /// Retrieve market bars for a date range.
    /// Returns OHLCV data filtered by RTH settings if configured.
    /// Used by regime scorer for technical analysis.
    /// </summary>
    IEnumerable<Bar> GetBars(DateOnly start, DateOnly end);
    
    /// <summary>
    /// Time interval between bars (e.g., 1 minute, 5 minutes).
    /// Used for calculations that depend on bar frequency.
    /// </summary>
    TimeSpan BarInterval { get; }
    
    /// <summary>
    /// Calculate 20-period Average True Range at specific timestamp.
    /// ATR measures volatility by considering gaps between sessions.
    /// 
    /// TRUE RANGE = MAX of:
    /// 1. High - Low
    /// 2. |High - Previous Close|
    /// 3. |Low - Previous Close|
    /// 
    /// USAGE: Higher ATR = more volatile conditions, adjust strategy accordingly.
    /// THRESHOLD: ATR vs daily range comparison for regime classification.
    /// </summary>
    double Atr20Minutes(DateTime ts);
    
    /// <summary>
    /// Calculate Volume Weighted Average Price over specified time window.
    /// VWAP = Σ(Price × Volume) / Σ(Volume)
    /// 
    /// INTERPRETATION:
    /// - Price above VWAP = bullish sentiment
    /// - Price below VWAP = bearish sentiment
    /// - Slope of VWAP = trend direction
    /// 
    /// STRATEGY USE:
    /// - Determine market bias for single-sided spreads
    /// - Confirm trend persistence with price action
    /// </summary>
    double Vwap(DateTime now, TimeSpan window);
    
    /// <summary>
    /// Get current spot price of underlying at specific timestamp.
    /// Used for options pricing, strike selection, and moneyness calculations.
    /// Returns most recent close price up to the given timestamp.
    /// </summary>
    double GetSpot(DateTime ts);
}