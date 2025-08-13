using ODTE.Backtest.Config;
using ODTE.Backtest.Core;
using ODTE.Backtest.Data;

namespace ODTE.Backtest.Signals;

/// <summary>
/// Market regime classification engine using multiple technical signals.
/// WHY: Converts raw market data into actionable Go/No-Go decisions and strategy bias.
/// 
/// REGIME DETECTION PHILOSOPHY:
/// Different market conditions favor different options strategies:
/// - Range-bound: Iron condors (sell volatility on both sides)
/// - Trending up: Put credit spreads (bullish bias)
/// - Trending down: Call credit spreads (bearish bias)
/// - High volatility: Avoid new positions (gamma risk)
/// - Event windows: Risk-off (unpredictable moves)
/// 
/// SIGNALS INTEGRATED:
/// 1. Opening Range (OR): First 15-min high/low, breakout = trend bias
/// 2. VWAP Persistence: Price above/below volume-weighted average
/// 3. ATR vs Daily Range: Volatility expansion/contraction
/// 4. Economic Calendar: Event proximity blocking
/// 5. Time-to-Close: Gamma hour protection
/// 
/// SCORING METHODOLOGY:
/// Additive point system where higher scores = more favorable conditions
/// - Positive points: Trend persistence, calm volatility, OR breakouts
/// - Negative points: Event proximity, late session, mixed signals
/// 
/// OUTPUT INTERPRETATION:
/// - Score ≤ -1: No-Go (risk-off)
/// - Score 0-1 + calm: Iron condor
/// - Score 2+ + trend up: Put credit spreads
/// - Score 2+ + trend down: Call credit spreads
/// 
/// References:
/// - Opening Range: https://thepaxgroup.org/the-opening-range/
/// - VWAP Trading: https://www.investopedia.com/terms/v/vwap.asp
/// - ATR Volatility: https://www.investopedia.com/articles/trading/08/average-true-range.asp
/// - Market Microstructure: "Trading and Exchanges" by Larry Harris
/// </summary>
public sealed class RegimeScorer
{
    private readonly SimConfig _cfg;
    
    public RegimeScorer(SimConfig cfg) 
    { 
        _cfg = cfg; 
    }

    /// <summary>
    /// Calculate market regime score and directional bias.
    /// Returns (score, calm_range_flag, bullish_trend_flag, bearish_trend_flag).
    /// 
    /// SIGNAL PROCESSING PIPELINE:
    /// 1. Opening Range Analysis: Detect trend initiation
    /// 2. VWAP Persistence: Measure trend continuation
    /// 3. Volatility Regime: Compare daily range vs historical ATR
    /// 4. Event Proximity: Block trades near macro announcements
    /// 5. Time Gates: Avoid gamma hour risk
    /// 
    /// SCORING BREAKDOWN:
    /// +2: Strong trend (OR breakout + hold)
    /// +1-2: VWAP persistence (70%+ bars on one side = +2, 50%+ = +1)
    /// +1: Signal alignment (VWAP slope matches OR direction)
    /// +2: Calm conditions (daily range ≤ 0.8× ATR)
    /// +2: Expansion conditions (daily range ≥ 1.0× ATR) - favors trend trades
    /// -2: Event proximity (within configured window)
    /// -3: Late session (gamma hour protection)
    /// 
    /// REGIME CLASSIFICATION:
    /// - Calm Range: Low volatility + no trend breakout (favor condors)
    /// - Trend Up: OR break higher + VWAP persistence above (favor put spreads)
    /// - Trend Down: OR break lower + VWAP persistence below (favor call spreads)
    /// 
    /// THRESHOLD INTERPRETATION:
    /// Score ≤ -1: Risk-off, no new positions
    /// Score 0-1: Neutral conditions, consider range strategies
    /// Score 2+: Favorable conditions for directional strategies
    /// </summary>
    public (int score, bool calmRange, bool trendBiasUp, bool trendBiasDown) Score(
        DateTime now, 
        IMarketData md, 
        IEconCalendar cal)
    {
        // === 1. OPENING RANGE ANALYSIS ===
        var orMins = _cfg.Signals.OrMinutes;
        var sessionStart = now.SessionStart();
        var orEnd = sessionStart.AddMinutes(orMins);
        
        var bars = md.GetBars(DateOnly.FromDateTime(now.Date), DateOnly.FromDateTime(now.Date))
            .Where(b => b.Ts >= sessionStart && b.Ts <= now)
            .ToList();
        
        // Calculate OR high/low from first N minutes
        double orHigh = bars.Where(b => b.Ts <= orEnd).Select(b => b.H).DefaultIfEmpty().Max();
        double orLow  = bars.Where(b => b.Ts <= orEnd).Select(b => b.L).DefaultIfEmpty().Min();
        
        // Check for breakout and persistence
        bool orBreakUp = bars.LastOrDefault()?.C > orHigh;
        bool orBreakDn = bars.LastOrDefault()?.C < orLow;
        bool orHolds = orBreakUp || orBreakDn;  // Any breakout = trend signal

        // === 2. VWAP PERSISTENCE ANALYSIS ===
        var vwap = md.Vwap(now, TimeSpan.FromMinutes(_cfg.Signals.VwapWindowMinutes));
        var last30 = bars.Where(b => b.Ts > now.AddMinutes(-_cfg.Signals.VwapWindowMinutes)).ToList();
        int above = last30.Count(b => b.C >= vwap);
        double sidePct = last30.Count>0 ? (double)above / last30.Count : 0.5;
        bool vwapSlopeUp = last30.Count>1 && last30.Last().C > last30.First().C;

        // === 3. VOLATILITY REGIME ANALYSIS ===
        double dayRange = bars.Count>0 ? (bars.Max(b=>b.H) - bars.Min(b=>b.L)) : 0;
        double atr = md.Atr20Minutes(now);
        double rngVsAtr = atr>0 ? dayRange/atr : 0.5;

        // === 4. EVENT PROXIMITY CHECK ===
        var nextEvt = cal.NextEventAfter(now);
        int minsToEvent = nextEvt is null ? int.MaxValue : (int)(nextEvt.Ts - now).TotalMinutes;

        // === 5. SCORE CALCULATION ===
        int score = 0;
        
        // Trend signals
        if (orHolds) score += 2;  // Strong trend initiation signal
        
        // VWAP persistence
        score += sidePct >= 0.7 ? 2 : sidePct >= 0.5 ? 1 : 0;
        
        // Signal alignment bonus
        if (vwapSlopeUp == orBreakUp) score += 1;  // VWAP slope confirms OR direction
        
        // Volatility regime bonuses
        score += (rngVsAtr <= 0.8 ? 2 : 0);  // Calm conditions favor premium selling
        score += (rngVsAtr >= 1.0 ? 2 : 0);  // Expansion favors directional trades
        
        // Risk gates (negative points)
        if (minsToEvent < _cfg.Signals.EventBlockMinutesBefore) score -= 2;  // Event proximity
        
        var minsToClose = (now.SessionEnd() - now).TotalMinutes;
        if (minsToClose < _cfg.NoNewRiskMinutesToClose) score -= 3;  // Gamma hour

        // === 6. REGIME CLASSIFICATION ===
        bool calmRange = rngVsAtr <= 0.8 && !orHolds;  // Quiet + no breakout = range
        bool trendUp = orBreakUp && sidePct>=0.6;       // Breakout up + VWAP persistence
        bool trendDn = orBreakDn && (1-sidePct)>=0.6;   // Breakout down + VWAP persistence
        
        return (score, calmRange, trendUp, trendDn);
    }
}