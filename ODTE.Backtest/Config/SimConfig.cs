namespace ODTE.Backtest.Config;

/// <summary>
/// Centralized configuration for the 0DTE/1DTE backtest simulation.
/// WHY: Provides strongly-typed, data-driven strategy configuration that can be tuned via YAML.
/// This design allows parameter optimization and grid-search without recompilation.
/// 
/// DESIGN PHILOSOPHY: 
/// - Delta bands & credit/width floors gate entries
/// - Stops are both price-based (k×credit) and risk-based (Δ breach)
/// - On 0DTE, gamma amplifies small moves near short strike; Δ-based exits help avoid "death by gamma"
/// 
/// REFERENCES:
/// - Black-Scholes Model & Greeks: https://en.wikipedia.org/wiki/Black%E2%80%93Scholes_model
/// - SPX/SPXW PM Settlement: https://www.cboe.com/tradable_products/sp_500/spx_options/specifications/
/// - XSP Mini-SPX (1/10th size): https://www.cboe.com/tradable_products/sp_500/mini_spx_options/
/// </summary>
public sealed class SimConfig
{
    /// <summary>
    /// Underlying symbol to trade. XSP is Mini-SPX (1/10th SPX size).
    /// XSP ADVANTAGES:
    /// - Cash-settled (no assignment risk)
    /// - European-style (no early exercise)
    /// - PM settlement (4:00 PM ET)
    /// - Smaller size for risk management ($500 per point vs $5000 for SPX)
    /// Reference: https://www.cboe.com/tradable_products/sp_500/mini_spx_options/
    /// </summary>
    public string Underlying { get; set; } = "XSP";
    
    public DateOnly Start { get; set; }
    public DateOnly End { get; set; }
    
    /// <summary>
    /// Data mode: "prototype" uses synthetic options, "pro" requires vendor adapter.
    /// Prototype mode generates options from SPX spot + VIX/VIX9D proxies.
    /// Pro mode would connect to ORATS/LiveVol/dxFeed for real intraday chains.
    /// </summary>
    public string Mode { get; set; } = "prototype";
    
    /// <summary>
    /// Regular Trading Hours only. RTH = 9:30 AM - 4:00 PM ET.
    /// Avoids overnight noise and focuses on liquid market hours.
    /// </summary>
    public bool RthOnly { get; set; } = true;
    
    public string Timezone { get; set; } = "Europe/London";

    /// <summary>
    /// Decision cadence in seconds (900 = 15 minutes).
    /// How often to evaluate market conditions and potentially enter new trades.
    /// Can be throttled based on realized volatility (see ThrottleCfg).
    /// </summary>
    public int CadenceSeconds { get; set; } = 900;
    
    /// <summary>
    /// No new risk in final N minutes before close (gamma hour protection).
    /// The final hour (3-4 PM ET) sees accelerated gamma on 0DTE options.
    /// Reference: "Gamma Risk in Weekly Options" - Cboe Research
    /// </summary>
    public int NoNewRiskMinutesToClose { get; set; } = 40; 

    public ShortDeltaCfg ShortDelta { get; set; } = new();
    public WidthPointsCfg WidthPoints { get; set; } = new();
    public CreditPerWidthCfg CreditPerWidthMin { get; set; } = new();

    public StopsCfg Stops { get; set; } = new();
    public RiskCfg Risk { get; set; } = new();
    public SlippageCfg Slippage { get; set; } = new();
    public FeesCfg Fees { get; set; } = new();
    public SignalsCfg Signals { get; set; } = new();
    public ThrottleCfg Throttle { get; set; } = new();
    public PathsCfg Paths { get; set; } = new();
}

/// <summary>
/// Delta configuration for short strikes selection.
/// DELTA PRIMER: Delta represents the rate of change of option price relative to underlying.
/// - 0.10 delta ≈ 10% probability of finishing ITM at expiration
/// - Further OTM (lower delta) = safer but less credit
/// - Closer ATM (higher delta) = more credit but higher risk
/// Reference: Greeks Overview - https://www.investopedia.com/terms/g/greeks.asp
/// </summary>
public sealed class ShortDeltaCfg 
{ 
    /// <summary>Iron Condor: Use 7-15 delta for calmer markets (further OTM)</summary>
    public double CondorMin { get; set; } = 0.07; 
    public double CondorMax { get; set; } = 0.15;
    
    /// <summary>Single-sided spreads: Use 10-20 delta for trending markets (slightly closer)</summary>
    public double SingleMin { get; set; } = 0.10; 
    public double SingleMax { get; set; } = 0.20; 
}

/// <summary>
/// Strike width configuration in points.
/// For XSP: 1 point = $100 per spread (vs SPX: 1 point = $1000)
/// Narrow spreads (1-2 pts) keep max loss manageable (~$100-200 per spread)
/// </summary>
public sealed class WidthPointsCfg 
{ 
    public int Min { get; set; } = 1; 
    public int Max { get; set; } = 2; 
}

/// <summary>
/// Minimum credit-to-width ratio filters.
/// RATIONALE: Avoid selling "pennies in front of steamroller"
/// - 0.18 for condors = collect at least 18% of max risk
/// - 0.20 for singles = slightly higher threshold for directional risk
/// Example: $1 wide spread needs $0.20 credit minimum
/// </summary>
public sealed class CreditPerWidthCfg 
{ 
    public double Condor { get; set; } = 0.18; 
    public double Single { get; set; } = 0.20; 
}

/// <summary>
/// Stop-loss configuration for open positions.
/// CRITICAL FOR 0DTE: Gamma accelerates exponentially near expiration.
/// - Price stop: Exit when spread value reaches 2.2x entry credit
/// - Delta stop: Exit when short strike delta exceeds 0.33 (entering "gamma danger zone")
/// Reference: "Managing Gamma Risk" - https://www.optionseducation.org/
/// </summary>
public sealed class StopsCfg 
{ 
    /// <summary>Exit when spread trades at 2.2x credit received (loss of 1.2x credit)</summary>
    public double CreditMultiple { get; set; } = 2.2;
    
    /// <summary>Exit when short strike delta > 0.33 (gamma acceleration risk)</summary>
    public double DeltaBreach { get; set; } = 0.33; 
}

/// <summary>
/// Portfolio-level risk management.
/// PHILOSOPHY: Multiple layers of protection prevent catastrophic losses.
/// - Daily stop: Hard limit on realized losses per day
/// - Per-trade cap: Maximum risk per position
/// - Concurrency limits: Avoid clustering risk at similar strikes
/// </summary>
public sealed class RiskCfg 
{ 
    /// <summary>Stop trading for the day after $500 in realized losses</summary>
    public double DailyLossStop { get; set; } = 500;
    
    /// <summary>Maximum loss per trade (affects position sizing)</summary>
    public double PerTradeMaxLossCap { get; set; } = 200;
    
    /// <summary>Max concurrent positions per side (put/call) to avoid concentration</summary>
    public int MaxConcurrentPerSide { get; set; } = 2; 
}

/// <summary>
/// Slippage and market impact modeling.
/// REALITY CHECK: You rarely get mid-price fills, especially on 0DTE.
/// - Entry: Pay away from mid to get filled
/// - Exit: Pay more when closing (urgency premium)
/// - Late session: Wider spreads in final hour
/// Reference: "Execution Quality in Options" - https://www.cboe.com/
/// </summary>
public sealed class SlippageCfg 
{ 
    /// <summary>Ticks to subtract from theoretical credit on entry</summary>
    public double EntryHalfSpreadTicks { get; set; } = 0.5;
    
    /// <summary>Ticks to add to spread value on exit (urgency cost)</summary>
    public double ExitHalfSpreadTicks { get; set; } = 0.5;
    
    /// <summary>Additional slippage in gamma hour (3-4 PM ET)</summary>
    public double LateSessionExtraTicks { get; set; } = 0.5;
    
    /// <summary>Option tick size ($0.05 for most index options)</summary>
    public double TickValue { get; set; } = 0.05;
    
    /// <summary>Max acceptable bid-ask spread as % of credit</summary>
    public double SpreadPctCap { get; set; } = 0.25; 
}

/// <summary>
/// Transaction cost modeling.
/// TYPICAL RETAIL COSTS (per contract):
/// - Commission: $0.65 per contract
/// - Exchange fees: $0.25 per contract
/// - Total: $0.90 per contract, $1.80 per spread (2 legs)
/// Note: Some brokers offer better rates for high volume
/// </summary>
public sealed class FeesCfg 
{ 
    public double CommissionPerContract { get; set; } = 0.65; 
    public double ExchangeFeesPerContract { get; set; } = 0.25; 
}

/// <summary>
/// Market regime signal configuration.
/// SIGNALS USED:
/// - Opening Range (OR): First 15 min high/low, breakout indicates trend
/// - VWAP: Volume-weighted average, price persistence indicator
/// - ATR: Average True Range, volatility measure
/// - Event blocking: Avoid trading near macro announcements
/// References:
/// - OR Trading: https://www.investopedia.com/terms/o/opening-range.asp
/// - VWAP: https://www.investopedia.com/terms/v/vwap.asp
/// - ATR: https://www.investopedia.com/articles/trading/08/average-true-range.asp
/// </summary>
public sealed class SignalsCfg
{
    /// <summary>Opening Range period in minutes after market open</summary>
    public int OrMinutes { get; set; } = 15;
    
    /// <summary>VWAP calculation window for regime detection</summary>
    public int VwapWindowMinutes { get; set; } = 30;
    
    /// <summary>ATR period for volatility measurement</summary>
    public int AtrPeriodBars { get; set; } = 20;
    
    /// <summary>IV condition: "short_leq_30d" means VIX9D <= VIX (calmer near-term)</summary>
    public string CalmIvCondition { get; set; } = "short_leq_30d";
    
    /// <summary>Block new trades N minutes before economic events (CPI/FOMC/NFP)</summary>
    public int EventBlockMinutesBefore { get; set; } = 60;
    
    /// <summary>Resume trading N minutes after events</summary>
    public int EventBlockMinutesAfter { get; set; } = 15;
}

/// <summary>
/// Adaptive cadence based on realized volatility.
/// CONCEPT: Trade less frequently when market is volatile.
/// - High RV: Slow down decisions (30 min)
/// - Low RV: Speed up decisions (10 min)
/// This reduces whipsaws in volatile conditions
/// </summary>
public sealed class ThrottleCfg 
{ 
    /// <summary>Cadence when realized vol is high (1800s = 30 min)</summary>
    public int RvHighCadenceSeconds { get; set; } = 1800;
    
    /// <summary>Cadence when realized vol is low (600s = 10 min)</summary>
    public int RvLowCadenceSeconds { get; set; } = 600; 
}

/// <summary>
/// File paths for data inputs and outputs.
/// Sample data format requirements:
/// - bars_csv: ts,o,h,l,c,v (minute bars)
/// - vix_csv: date,vix (daily VIX values)
/// - vix9d_csv: date,vix9d (9-day VIX)
/// - calendar_csv: ts,kind (economic events)
/// </summary>
public sealed class PathsCfg
{ 
    public string BarsCsv { get; set; } = "./Samples/bars_spx_min.csv"; 
    public string VixCsv { get; set; } = "./Samples/vix_daily.csv"; 
    public string Vix9dCsv { get; set; } = "./Samples/vix9d_daily.csv"; 
    public string CalendarCsv { get; set; } = "./Samples/calendar.csv"; 
    public string ReportsDir { get; set; } = "./Reports"; 
}

