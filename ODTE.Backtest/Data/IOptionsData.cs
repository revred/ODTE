using ODTE.Backtest.Core;

namespace ODTE.Backtest.Data;

/// <summary>
/// Abstraction over options chain data and volatility surface.
/// WHY: Separates options data source from strategy logic, enabling prototype → production migration.
/// 
/// PROTOTYPE MODE: Synthetic options generated from SPX spot + VIX/VIX9D proxies
/// PRODUCTION MODE: Real intraday chains from ORATS/LiveVol/dxFeed with actual Greeks
/// 
/// CRITICAL FOR 0DTE TRADING:
/// - Accurate Greeks (especially Delta) for strike selection
/// - Real-time quotes for entry/exit decisions
/// - Implied volatility for regime assessment
/// - Liquidity metrics (bid-ask spreads) for execution quality
/// 
/// 0DTE OPTIONS CHARACTERISTICS:
/// - Extreme time decay (theta burn)
/// - High gamma (accelerating delta)
/// - Wide bid-ask spreads near expiration
/// - Limited strike availability
/// - PM cash settlement for SPX/XSP
/// 
/// References:
/// - SPXW/XSP Dailies: https://www.cboe.com/tradable_products/sp_500/spx_options/specifications/
/// - PM Settlement: https://www.cboe.com/tradable_products/sp_500/mini_spx_options/cash_settlement/
/// - Options Data Vendors: https://orats.com/one-minute-data
/// </summary>
public interface IOptionsData
{
    /// <summary>
    /// Get option quotes for today's expiry (0DTE) at specific timestamp.
    /// Returns complete option chain with bids, asks, mid prices, and Greeks.
    /// 
    /// STRIKE SELECTION STRATEGY:
    /// - Filter by delta bands (7-15 for condors, 10-20 for singles)
    /// - Check liquidity via bid-ask spread width
    /// - Ensure adequate width for protective long legs
    /// 
    /// DATA QUALITY REQUIREMENTS:
    /// - Real-time or near-real-time quotes
    /// - Accurate Greeks (calculated or vendor-provided)
    /// - Proper handling of early/late session illiquidity
    /// </summary>
    IEnumerable<OptionQuote> GetQuotesAt(DateTime ts);

    /// <summary>
    /// Determine expiry date for "today's" options (0DTE trading).
    /// SPX/XSP daily options expire on the same trading day with PM settlement.
    /// 
    /// EXPIRY MECHANICS:
    /// - Settlement time: 4:00 PM ET (market close)
    /// - Cash settlement: No physical delivery, automatic exercise
    /// - European style: No early exercise risk
    /// - Final prices based on closing auction
    /// 
    /// TRADING IMPLICATIONS:
    /// - Positions can be held to expiration safely
    /// - No assignment risk in cash-settled index options
    /// - Final hour (3-4 PM) shows extreme gamma effects
    /// </summary>
    DateOnly TodayExpiry(DateTime ts);

    /// <summary>
    /// Get implied volatility proxies for regime detection.
    /// Returns (short-term IV, 30-day IV) typically from VIX9D and VIX.
    /// 
    /// IV TERM STRUCTURE ANALYSIS:
    /// - VIX9D > VIX: Near-term stress (avoid new positions)
    /// - VIX9D < VIX: Calm near-term conditions (favorable for premium selling)
    /// - Both rising: General market stress
    /// - Both falling: Complacency conditions
    /// 
    /// STRATEGY INTEGRATION:
    /// - High IV: Better credit collection but higher gamma risk
    /// - Low IV: Lower credits but more predictable behavior
    /// - IV skew: Puts typically trade at higher IV than calls (equity fear premium)
    /// 
    /// References:
    /// - VIX Methodology: https://www.cboe.com/tradable_products/vix/
    /// - VIX9D: https://www.cboe.com/us/indices/dashboard/vix9d/
    /// </summary>
    (double shortIv, double thirtyIv) GetIvProxies(DateTime ts);
}