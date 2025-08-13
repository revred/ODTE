using ODTE.Backtest.Core;

namespace ODTE.Backtest.Data;

/// <summary>
/// Economic calendar interface for event-driven risk management.
/// WHY: Major economic announcements cause volatility spikes that can quickly overwhelm 0DTE positions.
/// Strategy blocks new entries near high-impact events and may close existing positions.
/// 
/// HIGH-IMPACT EVENTS FOR OPTIONS TRADING:
/// - FOMC Announcements: Interest rate decisions, policy statements
/// - CPI/PCE: Inflation data affecting Fed policy expectations
/// - NFP: Employment data indicating economic health
/// - GDP: Quarterly growth figures
/// - Earnings (for single-name options): Company-specific but affects sector/index
/// 
/// RISK MANAGEMENT APPROACH:
/// - No new positions 60+ minutes before events
/// - Resume trading 15+ minutes after (allow initial reaction to settle)
/// - Consider closing existing positions if near expiration
/// - Monitor VIX term structure for stress signals
/// 
/// EVENT WINDOW STRATEGY:
/// Before: Volatility compression as market waits
/// During: Explosive moves as positions unwind
/// After: Continued volatility as participants reposition
/// 
/// References:
/// - Economic Calendar: https://www.investopedia.com/terms/e/economic-calendar.asp
/// - FOMC Impact: https://www.federalreserve.gov/monetarypolicy/fomccalendars.htm
/// - High-Impact Data: https://www.bls.gov/schedule/news_release/
/// </summary>
public interface IEconCalendar
{
    /// <summary>
    /// Find the next economic event after the given timestamp.
    /// Used to calculate time-to-event for position entry gating.
    /// 
    /// IMPLEMENTATION CONSIDERATIONS:
    /// - Events should be pre-filtered for high/medium impact only
    /// - Include time zones (events often announced at specific ET times)
    /// - Handle revised event times and cancellations
    /// - Consider market holidays when events may be delayed
    /// 
    /// USAGE PATTERN:
    /// 1. Check upcoming event before each trade decision
    /// 2. Calculate minutes until event
    /// 3. Block trades if within configured window
    /// 4. Resume after event + buffer period
    /// 
    /// Returns null if no events found in reasonable future timeframe.
    /// </summary>
    EconEvent? NextEventAfter(DateTime ts);
}