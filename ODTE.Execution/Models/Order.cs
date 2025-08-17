namespace ODTE.Execution.Models;

/// <summary>
/// Represents an order to be executed with complete market context.
/// </summary>
public record Order
{
    public string OrderId { get; init; } = "";
    public string Symbol { get; init; } = "";
    public DateTime ExpirationDate { get; init; }
    public decimal Strike { get; init; }
    public OptionType OptionType { get; init; }
    public OrderSide Side { get; init; }
    public int Quantity { get; init; }
    public decimal? LimitPrice { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public OrderType OrderType { get; init; } = OrderType.Market;
    public TimeInForce TimeInForce { get; init; } = TimeInForce.Day;

    /// <summary>
    /// Estimated notional value for market impact calculations
    /// </summary>
    public decimal NotionalValue { get; init; }

    /// <summary>
    /// Strategy context for execution optimization
    /// </summary>
    public string StrategyId { get; init; } = "";

    /// <summary>
    /// Parent strategy type (e.g., IronCondor, CreditSpread)
    /// </summary>
    public string StrategyType { get; init; } = "";
}

/// <summary>
/// Multi-leg spread order containing multiple individual option orders
/// </summary>
public record SpreadOrder
{
    public string SpreadOrderId { get; init; } = "";
    public List<Order> Legs { get; init; } = new();
    public decimal NetCredit { get; init; }
    public decimal MaxRisk { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string StrategyType { get; init; } = "";
    public SpreadType SpreadType { get; init; }
}

public enum OptionType
{
    Call,
    Put
}

public enum OrderSide
{
    Buy,
    Sell
}

public enum OrderType
{
    Market,
    Limit,
    MarketOnClose
}

public enum TimeInForce
{
    Day,
    IOC,  // Immediate or Cancel
    FOK,  // Fill or Kill
    GTC   // Good Till Cancel
}

public enum SpreadType
{
    CreditSpread,
    DebitSpread,
    IronCondor,
    ButterflySpread,
    Straddle,
    Strangle,
    Custom
}