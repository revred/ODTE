using ODTE.Backtest.Core;

namespace ODTE.LiveTrading.Interfaces;

/// <summary>
/// Core brokerage interface for live options trading.
/// Provides a unified API across different brokers (IBKR, Robinhood, Schwab, etc.)
/// 
/// DEFENSIVE SECURITY DESIGN:
/// - All order validation happens at interface level
/// - Position limits enforced before broker submission
/// - Real-time risk monitoring with automatic stops
/// - Audit trail for all trading actions
/// </summary>
public interface IBroker
{
    /// <summary>
    /// Broker identification and connection status
    /// </summary>
    string BrokerName { get; }
    bool IsConnected { get; }
    DateTime LastHeartbeat { get; }
    
    /// <summary>
    /// Account and authentication
    /// </summary>
    Task<bool> ConnectAsync(BrokerCredentials credentials);
    Task DisconnectAsync();
    Task<AccountInfo> GetAccountInfoAsync();
    
    /// <summary>
    /// Market data access
    /// </summary>
    Task<IEnumerable<OptionQuote>> GetOptionChainAsync(string underlying, DateTime expiry);
    Task<double> GetSpotPriceAsync(string underlying);
    Task<MarketStatus> GetMarketStatusAsync();
    
    /// <summary>
    /// Order management
    /// </summary>
    Task<OrderResult> SubmitOrderAsync(LiveOrder order);
    Task<OrderResult> CancelOrderAsync(string orderId);
    Task<OrderResult> ModifyOrderAsync(string orderId, LiveOrder newOrder);
    
    /// <summary>
    /// Position monitoring
    /// </summary>
    Task<IEnumerable<LivePosition>> GetPositionsAsync();
    Task<IEnumerable<LiveOrder>> GetOrdersAsync(OrderStatus? status = null);
    Task<LiveOrder?> GetOrderAsync(string orderId);
    
    /// <summary>
    /// Risk management
    /// </summary>
    Task<RiskLimits> GetRiskLimitsAsync();
    Task<bool> ValidateOrderAsync(LiveOrder order);
    
    /// <summary>
    /// Events for real-time updates
    /// </summary>
    event EventHandler<PositionUpdateEventArgs> PositionUpdated;
    event EventHandler<OrderUpdateEventArgs> OrderUpdated;
    event EventHandler<MarketDataEventArgs> MarketDataUpdated;
    event EventHandler<ErrorEventArgs> ErrorOccurred;
}

/// <summary>
/// Brokerage credentials for secure authentication
/// WARNING: Never log or persist credentials in plain text
/// </summary>
public record BrokerCredentials
{
    public string Username { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public string ApiSecret { get; init; } = string.Empty;
    public string TradingPassword { get; init; } = string.Empty;
    public bool PaperTrading { get; init; } = true; // Default to paper trading for safety
    public Dictionary<string, string> ExtraParams { get; init; } = new();
}

/// <summary>
/// Account information and trading permissions
/// </summary>
public record AccountInfo
{
    public string AccountId { get; init; } = string.Empty;
    public decimal NetLiquidationValue { get; init; }
    public decimal AvailableFunds { get; init; }
    public decimal BuyingPower { get; init; }
    public decimal MaintenanceMargin { get; init; }
    public bool OptionsLevel { get; init; } // Can trade options
    public int MaxOptionsLevel { get; init; } // 0-5 options approval level
    public bool PatternDayTrader { get; init; }
    public Dictionary<string, decimal> Balances { get; init; } = new();
}

/// <summary>
/// Market status information
/// </summary>
public record MarketStatus
{
    public bool IsOpen { get; init; }
    public DateTime? NextOpen { get; init; }
    public DateTime? NextClose { get; init; }
    public string Session { get; init; } = "CLOSED"; // PRE, REGULAR, POST, CLOSED
    public Dictionary<string, bool> ProductStatus { get; init; } = new(); // Options, Stocks, etc.
}

/// <summary>
/// Live order representation for broker submission
/// </summary>
public record LiveOrder
{
    public string OrderId { get; init; } = string.Empty;
    public string ClientOrderId { get; init; } = Guid.NewGuid().ToString();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    
    // Order identification
    public string Underlying { get; init; } = string.Empty;
    public OrderType Type { get; init; }
    public OrderSide Side { get; init; }
    public int Quantity { get; init; }
    
    // Pricing
    public decimal? LimitPrice { get; init; }
    public decimal? StopPrice { get; init; }
    
    // Multi-leg support for spreads
    public List<OrderLeg> Legs { get; init; } = new();
    
    // Order management
    public OrderTimeInForce TimeInForce { get; init; } = OrderTimeInForce.Day;
    public DateTime? GoodTillDate { get; init; }
    
    // Risk parameters
    public decimal? MaxLoss { get; init; }
    public decimal? ProfitTarget { get; init; }
    
    // Status tracking
    public OrderStatus Status { get; init; }
    public string? RejectReason { get; init; }
    public DateTime? FilledAt { get; init; }
    public decimal? FilledPrice { get; init; }
    public int FilledQuantity { get; init; }
}

/// <summary>
/// Individual leg of a multi-leg options order (spreads, condors, etc.)
/// </summary>
public record OrderLeg
{
    public string Symbol { get; init; } = string.Empty; // Option symbol (OCC format)
    public DateTime Expiry { get; init; }
    public decimal Strike { get; init; }
    public Right Right { get; init; }
    public OrderSide Side { get; init; } // Buy or Sell
    public int Ratio { get; init; } = 1; // Number of contracts
}

/// <summary>
/// Live position tracking
/// </summary>
public record LivePosition
{
    public string PositionId { get; init; } = string.Empty;
    public string Underlying { get; init; } = string.Empty;
    public DateTime OpenedAt { get; init; }
    
    // Position details
    public List<PositionLeg> Legs { get; init; } = new();
    public decimal NetCredit { get; init; }
    public decimal CurrentValue { get; init; }
    public decimal UnrealizedPnL { get; init; }
    public decimal RealizedPnL { get; init; }
    
    // Risk metrics
    public decimal Delta { get; init; }
    public decimal Gamma { get; init; }
    public decimal Theta { get; init; }
    public decimal Vega { get; init; }
    
    // Management
    public bool IsOpen { get; init; } = true;
    public DateTime? ClosedAt { get; init; }
    public string? CloseReason { get; init; }
}

/// <summary>
/// Individual position leg details
/// </summary>
public record PositionLeg
{
    public string Symbol { get; init; } = string.Empty;
    public DateTime Expiry { get; init; }
    public decimal Strike { get; init; }
    public Right Right { get; init; }
    public int Quantity { get; init; } // Positive = long, negative = short
    public decimal AveragePrice { get; init; }
    public decimal CurrentPrice { get; init; }
}

/// <summary>
/// Order execution result
/// </summary>
public record OrderResult
{
    public bool Success { get; init; }
    public string OrderId { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public OrderStatus Status { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public decimal? FilledPrice { get; init; }
    public int FilledQuantity { get; init; }
    public List<string> Warnings { get; init; } = new();
}

/// <summary>
/// Risk limits and trading constraints
/// </summary>
public record RiskLimits
{
    public decimal MaxOrderValue { get; init; }
    public decimal DailyLossLimit { get; init; }
    public int MaxPositions { get; init; }
    public decimal MaxDelta { get; init; }
    public decimal MaxGamma { get; init; }
    public bool AllowNakedOptions { get; init; }
    public Dictionary<string, decimal> ProductLimits { get; init; } = new();
}

// Enums for order management
public enum OrderType
{
    Market,
    Limit,
    Stop,
    StopLimit,
    Spread, // Multi-leg spread order
    Condor,
    Butterfly
}

public enum OrderSide
{
    Buy,
    Sell,
    BuyToOpen,
    SellToOpen,
    BuyToClose,
    SellToClose
}

public enum OrderStatus
{
    Pending,
    Submitted,
    PartiallyFilled,
    Filled,
    Cancelled,
    Rejected,
    Expired
}

public enum OrderTimeInForce
{
    Day,      // Good for trading day
    GTC,      // Good till cancelled  
    IOC,      // Immediate or cancel
    FOK,      // Fill or kill
    GTD       // Good till date
}

// Event argument classes
public class PositionUpdateEventArgs : EventArgs
{
    public LivePosition Position { get; }
    public string UpdateType { get; } // OPENED, MODIFIED, CLOSED
    
    public PositionUpdateEventArgs(LivePosition position, string updateType)
    {
        Position = position;
        UpdateType = updateType;
    }
}

public class OrderUpdateEventArgs : EventArgs
{
    public LiveOrder Order { get; }
    public OrderStatus PreviousStatus { get; }
    
    public OrderUpdateEventArgs(LiveOrder order, OrderStatus previousStatus)
    {
        Order = order;
        PreviousStatus = previousStatus;
    }
}

public class MarketDataEventArgs : EventArgs
{
    public string Symbol { get; }
    public decimal Price { get; }
    public DateTime Timestamp { get; }
    
    public MarketDataEventArgs(string symbol, decimal price, DateTime timestamp)
    {
        Symbol = symbol;
        Price = price;
        Timestamp = timestamp;
    }
}

public class ErrorEventArgs : EventArgs
{
    public string Message { get; }
    public Exception? Exception { get; }
    public string Severity { get; } // INFO, WARNING, ERROR, CRITICAL
    
    public ErrorEventArgs(string message, Exception? exception = null, string severity = "ERROR")
    {
        Message = message;
        Exception = exception;
        Severity = severity;
    }
}