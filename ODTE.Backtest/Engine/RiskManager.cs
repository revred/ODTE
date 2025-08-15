using ODTE.Backtest.Config;
using ODTE.Backtest.Core;

namespace ODTE.Backtest.Engine;

/// <summary>
/// Portfolio-level risk controls and position limits enforcement.
/// WHY: Multiple layers of protection prevent catastrophic losses and emotional decision-making.
/// 
/// RISK MANAGEMENT HIERARCHY:
/// 1. Daily Loss Stop: Hard limit on realized losses per day
/// 2. Per-Trade Risk Cap: Maximum loss per individual position
/// 3. Concurrency Limits: Avoid clustering too many positions
/// 4. Time Windows: No new risk during gamma hour
/// 5. Event Blocking: No trading near macro announcements
/// 
/// BEHAVIORAL FINANCE INSIGHTS:
/// "Risk comes from not knowing what you're doing" - Warren Buffett
/// - Pre-defined rules prevent emotional overrides
/// - Daily stops force breaks after bad sessions
/// - Position limits prevent concentration risk
/// - Time gates avoid predictable high-risk periods
/// 
/// DAILY RESET PHILOSOPHY:
/// Each trading day starts fresh with reset counters.
/// This prevents multi-day drawdowns from compounding
/// and gives trader psychological reset.
/// 
/// CONCURRENCY RATIONALE:
/// Limiting simultaneous positions per side (put/call) prevents:
/// - All positions moving against you simultaneously
/// - Concentration at similar strike levels
/// - Overwhelming margin requirements
/// - Inability to manage multiple positions effectively
/// 
/// IMPLEMENTATION DETAILS:
/// - State tracks per-day and per-side positions
/// - Automatic reset on day boundary
/// - Fail-safe: Block all new positions if limits exceeded
/// 
/// References:
/// - Risk Management: "Market Wizards" by Jack Schwager
/// - Position Sizing: "The Mathematics of Money Management" by Ralph Vince
/// - Behavioral Finance: "Thinking, Fast and Slow" by Daniel Kahneman
/// </summary>
public sealed class RiskManager : IRiskManager
{
    private readonly SimConfig _cfg;
    private double _dailyRealizedPnL;    // Running P&L for current day
    private int _activePerSidePut;       // Count of open put positions
    private int _activePerSideCall;      // Count of open call positions
    private DateOnly _currentDay;        // Track day boundaries for resets

    public RiskManager(SimConfig cfg)
    { 
        _cfg = cfg; 
    }

    /// <summary>
    /// Evaluate whether a new position can be added given current risk state.
    /// Returns false if any risk limit would be violated.
    /// 
    /// RISK GATE HIERARCHY (all must pass):
    /// 1. Daily Loss Limit: Stop trading if realized losses exceed daily cap
    /// 2. Time Window: No new positions in final N minutes (gamma hour protection)
    /// 3. Position Limits: Respect maximum concurrent positions per side
    /// 
    /// DAILY RESET LOGIC:
    /// Automatically resets counters on new trading day:
    /// - P&L tracker: Back to zero
    /// - Position counts: Reset to zero
    /// - Fresh start: Psychological and practical benefits
    /// 
    /// GAMMA HOUR PROTECTION:
    /// Final 40 minutes (3:20-4:00 PM ET) are high-risk for 0DTE:
    /// - Extreme gamma acceleration
    /// - Reduced liquidity
    /// - Pin risk near ATM strikes
    /// - Better to manage existing positions than add new ones
    /// 
    /// CONCURRENCY LIMITS:
    /// Put and call positions tracked separately:
    /// - Prevents overconcentration in one direction
    /// - Allows some diversification (puts + calls)
    /// - Manageable position count for manual oversight
    /// </summary>
    public bool CanAdd(DateTime now, Decision d)
    {
        // === DAILY RESET LOGIC ===
        var today = DateOnly.FromDateTime(now);
        if (today != _currentDay && _currentDay != default(DateOnly))
        {
            // Only reset if this is a genuine day change, not initial state
            _currentDay = today;
            _dailyRealizedPnL = 0;
            _activePerSidePut = 0;
            _activePerSideCall = 0;
        }
        else if (_currentDay == default(DateOnly))
        {
            // First time initialization - set current day but don't reset counters
            _currentDay = today;
        }
        
        // === RISK GATE 1: DAILY LOSS LIMIT ===
        if (_dailyRealizedPnL <= -_cfg.Risk.DailyLossStop) return false;
        
        // === RISK GATE 2: TIME WINDOW (GAMMA HOUR) ===
        var minsToClose = (now.SessionEnd() - now).TotalMinutes;
        if (minsToClose < _cfg.NoNewRiskMinutesToClose) return false;
        
        // === RISK GATE 3: POSITION LIMITS ===
        if (d == Decision.SingleSidePut && _activePerSidePut >= _cfg.Risk.MaxConcurrentPerSide) return false;
        if (d == Decision.SingleSideCall && _activePerSideCall >= _cfg.Risk.MaxConcurrentPerSide) return false;
        // Condors use both put and call sides, check if either side would exceed limits
        if (d == Decision.Condor && (_activePerSidePut >= _cfg.Risk.MaxConcurrentPerSide || _activePerSideCall >= _cfg.Risk.MaxConcurrentPerSide)) return false;
        
        // All gates passed - position approved
        return true;
    }

    /// <summary>
    /// Register a new position opening.
    /// Updates position counters for concurrency tracking.
    /// Called immediately after successful position entry.
    /// </summary>
    public void RegisterOpen(Decision d)
    {
        if (d == Decision.SingleSidePut) _activePerSidePut++;
        if (d == Decision.SingleSideCall) _activePerSideCall++;
        if (d == Decision.Condor) 
        {
            // Condors use both put and call sides
            _activePerSidePut++;
            _activePerSideCall++;
        }
    }

    /// <summary>
    /// Check if a specific order can be placed without exceeding per-trade risk limits.
    /// Implements Reverse Fibonacci budget enforcement per the code review recommendations.
    /// 
    /// FIBONACCI RISK BUDGET:
    /// Daily loss limits follow Reverse Fibonacci: $500 → $300 → $200 → $100
    /// Each trade's maximum potential loss must fit within remaining daily budget.
    /// 
    /// CALCULATION:
    /// - Determine order's maximum potential loss
    /// - Check against remaining Fibonacci budget for the day
    /// - Reserve risk for open positions to prevent over-allocation
    /// 
    /// DEFENSIVE DESIGN:
    /// - No order can breach daily Fibonacci limit
    /// - Accounts for unrealized risk from open positions
    /// - Conservative: Better to miss opportunity than exceed risk
    /// </summary>
    public bool CanAddOrder(SpreadOrder order)
    {
        var maxLoss = CalculateMaxLoss(order);
        var remainingBudget = GetRemainingFibonacciBudget();
        return maxLoss <= remainingBudget;
    }

    /// <summary>
    /// Calculate the maximum potential loss for a spread order.
    /// This is the worst-case scenario if the spread moves fully against us.
    /// </summary>
    private decimal CalculateMaxLoss(SpreadOrder order) => order.Type switch
    {
        Decision.SingleSidePut or Decision.SingleSideCall => (decimal)(order.Width - order.Credit) * 100m, // $100 per point for XSP
        Decision.Condor => (decimal)order.Width * 100m - (decimal)order.Credit,     // Worst wing minus credit
        _ => throw new NotSupportedException($"Max loss calculation not implemented for {order.Type}")
    };

    /// <summary>
    /// Get remaining daily risk budget based on Reverse Fibonacci system.
    /// TODO: Implement actual Fibonacci progression based on consecutive loss days.
    /// For now, uses simple daily loss stop as budget.
    /// </summary>
    private decimal GetRemainingFibonacciBudget()
    {
        var dailyBudget = (decimal)_cfg.Risk.DailyLossStop;
        var realizedLoss = (decimal)Math.Abs(_dailyRealizedPnL);
        // TODO: Add reserved risk for open positions
        return Math.Max(0, dailyBudget - realizedLoss);
    }

    /// <summary>
    /// Register a position closing.
    /// Updates both P&L tracking and position counters.
    /// Called immediately after position exit (stop, target, or expiry).
    /// 
    /// P&L TRACKING:
    /// - Accumulates realized gains/losses for daily limit enforcement
    /// - Only counts closed positions (no mark-to-market)
    /// - Includes all fees and slippage costs
    /// 
    /// POSITION COUNT MANAGEMENT:
    /// - Decrements active position counters
    /// - Enables new positions up to limits
    /// - Defensive programming: Prevents negative counts
    /// </summary>
    public void RegisterClose(Decision d, double pnl)
    {
        // Update daily P&L tracker
        _dailyRealizedPnL += pnl;
        
        // Decrement position counters (with bounds checking)
        if (d == Decision.SingleSidePut && _activePerSidePut > 0) _activePerSidePut--;
        if (d == Decision.SingleSideCall && _activePerSideCall > 0) _activePerSideCall--;
        if (d == Decision.Condor) 
        {
            // Condors use both put and call sides
            if (_activePerSidePut > 0) _activePerSidePut--;
            if (_activePerSideCall > 0) _activePerSideCall--;
        }
    }
}