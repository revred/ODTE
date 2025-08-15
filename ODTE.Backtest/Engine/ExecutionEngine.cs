using ODTE.Backtest.Config;
using ODTE.Backtest.Core;
using ODTE.Backtest.Data;

namespace ODTE.Backtest.Engine;

/// <summary>
/// Models trade execution with realistic fill prices and exit conditions.
/// WHY: Bridges theoretical strategy signals with practical market realities.
/// 
/// EXECUTION MODELING PHILOSOPHY:
/// "In God we trust, all others bring data" - Real trading involves slippage, market impact, and timing.
/// This engine models the gap between theoretical mid-prices and actual fill prices.
/// 
/// FILL MODELING:
/// - Entry: Assume slight adverse fill (pay away from mid)
/// - Exit: Assume more adverse fill (urgency premium + wider spreads)
/// - Late session: Extra slippage due to reduced liquidity
/// - Minimum prices: Respect option tick size ($0.05)
/// 
/// STOP-LOSS MECHANISMS:
/// 1. Price-based: Exit when spread value reaches multiple of entry credit
/// 2. Delta-based: Exit when short strike delta exceeds threshold (gamma protection)
/// 
/// CRITICAL FOR 0DTE:
/// Delta-based stops are essential because gamma accelerates exponentially near expiration.
/// A 20-delta option can become 50-delta within minutes on 0DTE.
/// 
/// EXECUTION QUALITY FACTORS:
/// - Time of day: Spreads widen in final hour
/// - Volatility: Higher IV = wider spreads
/// - Market stress: Gap risk increases slippage
/// - Position size: Larger orders face more impact
/// 
/// References:
/// - Execution Quality: https://www.cboe.com/us/equities/market_statistics/execution_quality/
/// - Gamma Risk: "Dynamic Hedging" by Nassim Taleb
/// - Market Microstructure: "Trading and Exchanges" by Larry Harris
/// </summary>
public sealed class ExecutionEngine : IExecutionEngine
{
    private readonly SimConfig _cfg;
    private readonly TradeLogDatabase _tradeLogDb;
    
    public ExecutionEngine(SimConfig cfg, string logBasePath = "./TradeLogs") 
    { 
        _cfg = cfg;
        _tradeLogDb = new TradeLogDatabase(logBasePath);
    }

    /// <summary>
    /// Model entry execution with realistic slippage.
    /// WHY: Theoretical spread credits are rarely achieved in practice.
    /// 
    /// ENTRY FILL MODELING:
    /// - Start with theoretical credit from spread builder
    /// - Subtract slippage (typically 0.5 ticks = $0.025 per option)
    /// - Ensure minimum fill price (one tick = $0.05)
    /// 
    /// SLIPPAGE SOURCES:
    /// - Bid-ask spread: Pay bid for short leg, ask for long leg
    /// - Market impact: Moving prices slightly against you
    /// - Timing lag: Prices change between signal and execution
    /// - Liquidity: Less liquid strikes have wider spreads
    /// 
    /// REALISTIC EXPECTATIONS:
    /// If theoretical spread value is $0.25:
    /// - Entry fill: ~$0.22 (after 0.5 tick slippage)
    /// - This builds in conservative execution assumptions
    /// 
    /// Returns null if order cannot be filled (e.g., negative credit after slippage)
    /// </summary>
    public OpenPosition? TryEnter(SpreadOrder order)
    {
        double tick = _cfg.Slippage.TickValue;
        double entry = Math.Max(0.05, order.Credit - _cfg.Slippage.EntryHalfSpreadTicks * tick);
        return new OpenPosition(order, entry, order.Ts);
    }

    /// <summary>
    /// Evaluate exit conditions for open position.
    /// Returns (should_exit, exit_price, reason) tuple.
    /// 
    /// EXIT TRIGGERS:
    /// 1. Price Stop: Spread value ≥ 2.2x entry credit (default)
    ///    - Protects against adverse moves
    ///    - Limits losses to ~1.2x credit received
    /// 
    /// 2. Delta Stop: Short strike delta ≥ 0.33 (default)
    ///    - Critical gamma protection for 0DTE
    ///    - Prevents exponential losses as strike approaches ATM
    /// 
    /// DELTA BREACH EXPLANATION:
    /// On 0DTE, gamma (rate of delta change) becomes extreme near ATM.
    /// Example: 20-delta put can become 80-delta put in minutes if underlying drops.
    /// Delta stop cuts losses before gamma acceleration overwhelms position.
    /// 
    /// EXIT SLIPPAGE:
    /// - Add exit slippage to current spread value
    /// - Accounts for urgency and wider spreads when closing
    /// - Typically higher than entry slippage (need to get out fast)
    /// 
    /// ENHANCEMENT OPPORTUNITIES:
    /// - Time-based stops (e.g., close within 2 hours of expiry)
    /// - Profit targets (e.g., close at 50% of max profit)
    /// - Volatility-based adjustments
    /// - Risk-based position sizing adjustments
    /// </summary>
    public (bool exit, double exitPrice, string reason) ShouldExit(
        OpenPosition pos, 
        double currentSpreadValue, 
        double shortStrikeDelta, 
        DateTime now)
    {
        double stopVal = pos.EntryPrice * _cfg.Stops.CreditMultiple;
        
        // Price-based stop: Spread has moved against us
        if (currentSpreadValue >= stopVal) 
            return (true, 
                    currentSpreadValue + _cfg.Slippage.ExitHalfSpreadTicks * _cfg.Slippage.TickValue, 
                    $"Stop credit x{_cfg.Stops.CreditMultiple}");
        
        // Delta-based stop: Gamma protection for 0DTE
        if (Math.Abs(shortStrikeDelta) >= _cfg.Stops.DeltaBreach) 
            return (true, 
                    currentSpreadValue + _cfg.Slippage.ExitHalfSpreadTicks * _cfg.Slippage.TickValue, 
                    $"Delta>{_cfg.Stops.DeltaBreach}");
        
        // No exit conditions met
        return (false, 0, "");
    }

    /// <summary>
    /// Log trade closure for forensics and pattern analysis.
    /// Implements structured logging per code review recommendations.
    /// 
    /// FORENSICS PIPELINE:
    /// - Captures market conditions at trade closure
    /// - Enables ML-based pattern recognition
    /// - Supports Syntricks replay for loss analysis
    /// - Feeds nightly clustering of losing trades
    /// 
    /// LOG FORMAT:
    /// JSONL format for easy ingestion into analytics pipelines
    /// Compatible with time-series databases and ML frameworks
    /// </summary>
    /// <param name="position">Closed position details</param>
    /// <param name="exitPrice">Actual exit price achieved</param>
    /// <param name="exitReason">Why trade was closed</param>
    /// <param name="marketRegime">Current market conditions</param>
    public async Task LogTradeClosureAsync(OpenPosition position, double exitPrice, string exitReason, string marketRegime = "unknown")
    {
        try
        {
            var pnl = (decimal)(exitPrice - position.EntryPrice);
            var maxLoss = CalculateMaxLoss(position.Order);

            var tradeLog = new TradeLog(
                Timestamp: DateTime.UtcNow,
                Symbol: position.Order.Underlying,
                Expiry: position.Order.Short.Expiry,
                Right: position.Order.Short.Right, // Primary leg right
                Strike: (decimal)position.Order.Short.Strike,
                Type: GetSpreadType(position.Order.Type),
                MaxLoss: maxLoss,
                ExitPnL: pnl,
                ExitReason: exitReason,
                MarketRegime: marketRegime
            );

            // Log to SQLite database for analytics and forensics
            await _tradeLogDb.LogTradeAsync(tradeLog);
            
            // Also log to console for immediate feedback
            Console.WriteLine($"TRADE_LOG: {tradeLog.ToJson()}");
        }
        catch (Exception ex)
        {
            // Defensive: Never let logging break execution
            Console.WriteLine($"Trade logging failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Calculate maximum potential loss for position risk tracking.
    /// Mirrors RiskManager calculation for consistency.
    /// </summary>
    private decimal CalculateMaxLoss(SpreadOrder order) => order.Type switch
    {
        Decision.SingleSidePut or Decision.SingleSideCall => 
            (decimal)(order.Width - order.Credit) * 100m, // Credit spread
        Decision.Condor => 
            (decimal)order.Width * 100m - (decimal)order.Credit, // Iron condor
        _ => 0m
    };

    /// <summary>
    /// Convert Decision enum to SpreadType for logging consistency.
    /// </summary>
    private SpreadType GetSpreadType(Decision decision) => decision switch
    {
        Decision.SingleSidePut or Decision.SingleSideCall => SpreadType.CreditSpread,
        Decision.Condor => SpreadType.IronCondor,
        _ => SpreadType.Other
    };

    /// <summary>
    /// Cleanup resources including database connections.
    /// </summary>
    public void Dispose()
    {
        _tradeLogDb?.Dispose();
    }
}