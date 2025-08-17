namespace ODTE.Backtest.Core;

/// <summary>
/// Strategy decision types for different market regimes.
/// WHY: Separates signal generation from execution logic.
/// - NoGo: Don't trade (risk-off, event window, daily loss limit hit)
/// - Condor: Range-bound market, sell volatility on both sides
/// - SingleSidePut: Bullish bias, sell put spreads
/// - SingleSideCall: Bearish bias, sell call spreads
/// </summary>
public enum Decision { NoGo, Condor, SingleSidePut, SingleSideCall }

/// <summary>
/// Position type classification for different option strategies.
/// Used for tracking and risk management purposes.
/// </summary>
public enum PositionType { IronCondor, PutSpread, CallSpread, Other }

/// <summary>
/// Option right: Call or Put.
/// Call = right to buy underlying at strike
/// Put = right to sell underlying at strike
/// Reference: https://www.investopedia.com/terms/o/option.asp
/// </summary>
public enum Right { Call, Put }

/// <summary>
/// Market data bar (OHLCV) representing price action over a time interval.
/// Standard format used across financial data providers.
/// Ts = Timestamp, O/H/L/C = Open/High/Low/Close, V = Volume
/// </summary>
public record Bar(DateTime Ts, double O, double H, double L, double C, double V);

/// <summary>
/// Economic calendar event that could impact markets.
/// Examples: FOMC announcements, CPI releases, NFP data
/// Strategy uses these to avoid trading during high-impact windows
/// Reference: https://www.investopedia.com/terms/e/economic-calendar.asp
/// </summary>
public record EconEvent(DateTime Ts, string Kind);

/// <summary>
/// Single option quote with pricing and Greeks.
/// WHY: Encapsulates all data needed for option valuation and risk assessment.
/// 
/// COMPONENTS EXPLAINED:
/// - Bid/Ask: Market quotes (what you can sell/buy for)
/// - Mid: (Bid + Ask) / 2, theoretical "fair" price
/// - Delta: Sensitivity to underlying price (0-1 for calls, -1-0 for puts)
/// - IV: Implied Volatility, market's expectation of future price movement
/// 
/// USAGE: Strategy filters options by delta bands, values by mid, and checks liquidity via bid-ask spread.
/// Reference: https://www.investopedia.com/terms/o/option-chain.asp
/// </summary>
public record OptionQuote(
    DateTime Ts,           // Quote timestamp
    DateOnly Expiry,       // Expiration date (0DTE = same day)
    double Strike,         // Strike price
    Right Right,           // Call or Put
    double Bid,            // Highest price buyer will pay
    double Ask,            // Lowest price seller will accept
    double Mid,            // (Bid + Ask) / 2
    double Delta,          // Price sensitivity to underlying
    double Iv              // Implied volatility (annualized %)
)
{
    // Backward compatibility property
    public double IV => Iv;
}

/// <summary>
/// Individual leg of a spread strategy.
/// Ratio: +1 = long (buy), -1 = short (sell)
/// Example: Credit spread = Short high-delta option + Long low-delta option
/// </summary>
public record SpreadLeg(DateOnly Expiry, double Strike, Right Right, int Ratio);

/// <summary>
/// Complete spread order ready for execution.
/// WHY: Encapsulates all components of a multi-leg options strategy.
/// 
/// CREDIT SPREADS EXPLAINED:
/// - Short leg: Sell higher-premium option (closer to ATM)
/// - Long leg: Buy lower-premium option (further OTM) as protection
/// - Credit: Net premium received (Short premium - Long premium)
/// - Width: Difference between strikes (determines max loss)
/// - Max profit = Credit received
/// - Max loss = Width - Credit
/// 
/// Example: Sell 4950 Put, Buy 4949 Put for $0.25 credit
/// - Max profit: $25 (if SPX > 4950 at expiry)
/// - Max loss: $75 (if SPX < 4949 at expiry)
/// 
/// Reference: https://www.investopedia.com/terms/c/creditspread.asp
/// </summary>
public record SpreadOrder(
    DateTime Ts,           // Order timestamp
    string Underlying,     // XSP, SPX, etc.
    double Credit,         // Net premium received
    double Width,          // Strike width (max loss = width - credit)
    double CreditPerWidth, // Credit/Width ratio (quality filter)
    Decision Type,         // Strategy type that created this order
    SpreadLeg Short,       // Short leg (sell)
    SpreadLeg Long         // Long leg (buy, protection)
)
{
    // Compatibility properties for existing test code
    public double NetCredit => Credit;
    public PositionType PositionType => Type switch
    {
        Decision.Condor => PositionType.IronCondor,
        Decision.SingleSidePut => PositionType.PutSpread,
        Decision.SingleSideCall => PositionType.CallSpread,
        _ => PositionType.Other
    };

    // For iron condor strategies (4-leg), these would be the call spread legs
    public SpreadLeg? Short2 { get; init; }
    public SpreadLeg? Long2 { get; init; }
};

/// <summary>
/// Trade execution record.
/// Tracks actual fill price vs theoretical order price.
/// Accounts for slippage, market impact, and timing differences.
/// </summary>
public record Fill(DateTime Ts, double Price, string Reason);

/// <summary>
/// Active position in the portfolio.
/// WHY: Tracks lifecycle from entry to exit with all relevant data for P&L calculation.
/// 
/// POSITION STATES:
/// 1. Open: Just entered, monitoring for exit conditions
/// 2. Closed: Exited, ready for P&L calculation
/// 
/// EXIT TRIGGERS:
/// - Stop loss: Price moves against us
/// - Target profit: Price moves in our favor
/// - Time decay: Natural expiration (0DTE PM settlement)
/// - Risk management: Daily loss limits, position limits
/// </summary>
public record OpenPosition(SpreadOrder Order, double EntryPrice, DateTime EntryTs)
{
    /// <summary>Position closed flag</summary>
    public bool Closed { get; set; }

    /// <summary>Exit price (null if still open)</summary>
    public double? ExitPrice { get; set; }

    /// <summary>Exit timestamp (null if still open)</summary>
    public DateTime? ExitTs { get; set; }

    /// <summary>Exit reason: "Stop", "Target", "Expiry", "Risk Limit", etc.</summary>
    public string ExitReason { get; set; } = string.Empty;
}

/// <summary>
/// Complete trade result with P&L and performance metrics.
/// WHY: Standardized format for backtesting analysis and reporting.
/// 
/// P&L CALCULATION (Credit Spreads):
/// - Entry: Receive credit (positive cash flow)
/// - Exit: Pay to close spread (negative cash flow)
/// - P&L = (Entry Credit - Exit Debit) × 100 - Fees
/// - For XSP: Multiply by $100 per point
/// 
/// PERFORMANCE METRICS:
/// - MAE: Maximum Adverse Excursion (worst point during trade)
/// - MFE: Maximum Favorable Excursion (best point during trade)
/// These help optimize stop/target levels
/// 
/// Reference: "Trade Performance Metrics" - Van Tharp Institute
/// </summary>
public record TradeResult(
    OpenPosition Pos,               // Full position details
    double PnL,                     // Profit/Loss after fees
    double Fees,                    // Total transaction costs
    double MaxAdverseExcursion,     // Worst unrealized loss during trade
    double MaxFavorableExcursion    // Best unrealized profit during trade
)
{
    // Compatibility properties for existing test code
    public OpenPosition Position => Pos;
    public string ExitReason => Pos.ExitReason;
};

/// <summary>
/// Comprehensive backtest performance report.
/// WHY: Aggregates all trades into portfolio-level metrics for strategy evaluation.
/// 
/// KEY METRICS EXPLAINED:
/// - Win Rate: % of profitable trades (quality over quantity)
/// - Avg Win/Loss: Size of typical winners vs losers
/// - Sharpe Ratio: Risk-adjusted returns (return per unit of volatility)
/// - Max Drawdown: Worst peak-to-trough loss (psychological important)
/// 
/// INTERPRETATION:
/// - Good win rate: 60-80% for premium selling strategies
/// - Profit factor: (Avg Win × Win Count) / (|Avg Loss| × Loss Count) > 1.5
/// - Sharpe > 1.0 considered good, > 2.0 excellent
/// - Max DD < 20% of account for sustainable trading
/// 
/// References:
/// - Performance Metrics: https://www.investopedia.com/terms/s/sharperatio.asp
/// - Drawdown Analysis: https://www.investopedia.com/terms/d/drawdown.asp
/// </summary>
public sealed class RunReport
{
    /// <summary>All completed trades in chronological order</summary>
    public List<TradeResult> Trades { get; } = new();

    /// <summary>Total P&L before fees</summary>
    public double GrossPnL => Trades.Sum(t => t.PnL);

    /// <summary>Total transaction costs</summary>
    public double Fees => Trades.Sum(t => t.Fees);

    /// <summary>Net P&L after all costs (the number that matters)</summary>
    public double NetPnL => GrossPnL - Fees;

    /// <summary>Number of profitable trades</summary>
    public int WinCount => Trades.Count(t => t.PnL > 0);

    /// <summary>Number of losing trades</summary>
    public int LossCount => Trades.Count(t => t.PnL < 0);

    /// <summary>Win rate as percentage (0.0 - 1.0)</summary>
    public double WinRate => Trades.Count > 0 ? (double)WinCount / Trades.Count : 0;

    /// <summary>Average profit per winning trade</summary>
    public double AvgWin => WinCount > 0 ? Trades.Where(t => t.PnL > 0).Average(t => t.PnL) : 0;

    /// <summary>Average loss per losing trade (negative number)</summary>
    public double AvgLoss => LossCount > 0 ? Trades.Where(t => t.PnL < 0).Average(t => t.PnL) : 0;

    /// <summary>Sharpe ratio: risk-adjusted return measure</summary>
    public double Sharpe { get; set; }

    /// <summary>Maximum drawdown: worst peak-to-trough loss</summary>
    public double MaxDrawdown { get; set; }
}

/// <summary>
/// Structured trade logging for loss forensics and pattern analysis.
/// WHY: Enables ML-based learning from losing trades as per code review recommendations.
/// 
/// FORENSICS PIPELINE:
/// 1. Every trade closure generates a log entry
/// 2. Nightly clustering groups similar losing patterns
/// 3. Syntricks replay validates scenarios
/// 4. ML classifier learns skip/allow rules
/// 
/// MARKET ENVIRONMENT CAPTURE:
/// - Tracks key market conditions at trade entry
/// - Enables correlation analysis with trade outcomes
/// - Supports regime-aware strategy adjustments
/// 
/// JSON FORMAT:
/// Designed for easy ingestion into analytics pipelines
/// Compatible with standard logging frameworks and time-series databases
/// 
/// Reference: Code Review Summary - Loss Forensics Pipeline
/// </summary>
public record TradeLog(
    DateTime Timestamp,      // Trade closure time (UTC)
    string Symbol,          // Underlying symbol (e.g., "XSP")
    DateOnly Expiry,        // Option expiration date
    Right Right,            // Put or Call
    decimal Strike,         // Strike price
    SpreadType Type,        // Strategy type (CreditSpread, IronCondor)
    decimal MaxLoss,        // Maximum potential loss at entry
    decimal ExitPnL,        // Actual realized P&L
    string ExitReason,      // Why trade was closed ("Stop", "Target", "Expiry")
    string MarketRegime     // Market conditions ("trending", "ranging", "volatile")
)
{
    /// <summary>JSON serialization helper for logging pipelines</summary>
    public string ToJson() => System.Text.Json.JsonSerializer.Serialize(this);
};

/// <summary>
/// Spread type classification for different option strategies.
/// Used for risk calculation and performance analysis.
/// </summary>
public enum SpreadType
{
    CreditSpread,    // Single-sided put or call spread
    IronCondor,      // Double-sided condor spread  
    Butterfly,       // Butterfly spread (future)
    Straddle,        // Long/short straddle (future)
    Other            // Undefined strategy type
}