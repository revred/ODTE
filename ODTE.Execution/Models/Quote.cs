namespace ODTE.Execution.Models;

/// <summary>
/// Market quote with bid/ask prices and size information for execution simulation.
/// </summary>
public record Quote
{
    public string Symbol { get; init; } = "";
    public DateTime Timestamp { get; init; }
    public decimal Bid { get; init; }
    public decimal Ask { get; init; }
    public int BidSize { get; init; }
    public int AskSize { get; init; }
    public decimal Last { get; init; }
    public int LastSize { get; init; }

    /// <summary>
    /// Mid-point of bid/ask spread
    /// </summary>
    public decimal Mid => (Bid + Ask) / 2m;

    /// <summary>
    /// Bid-ask spread in dollars
    /// </summary>
    public decimal Spread => Ask - Bid;

    /// <summary>
    /// Spread as percentage of mid price
    /// </summary>
    public decimal SpreadBps => Spread / Mid * 10000m;

    /// <summary>
    /// Top-of-book size (minimum of bid/ask size)
    /// </summary>
    public int TopOfBookSize => Math.Min(BidSize, AskSize);

    /// <summary>
    /// Implied volatility if available
    /// </summary>
    public decimal? ImpliedVolatility { get; init; }

    /// <summary>
    /// Greeks if available
    /// </summary>
    public Greeks? Greeks { get; init; }
}

/// <summary>
/// Option Greeks for risk and execution modeling
/// </summary>
public record Greeks
{
    public decimal Delta { get; init; }
    public decimal Gamma { get; init; }
    public decimal Theta { get; init; }
    public decimal Vega { get; init; }
    public decimal Rho { get; init; }
}

/// <summary>
/// Quote book with multiple price levels for advanced execution modeling
/// </summary>
public record QuoteBook
{
    public string Symbol { get; init; } = "";
    public DateTime Timestamp { get; init; }
    public List<PriceLevel> Bids { get; init; } = new();
    public List<PriceLevel> Asks { get; init; } = new();

    /// <summary>
    /// Best bid/offer from the book
    /// </summary>
    public Quote? BestBidOffer => Bids.Count > 0 && Asks.Count > 0
        ? new Quote
        {
            Symbol = Symbol,
            Timestamp = Timestamp,
            Bid = Bids[0].Price,
            Ask = Asks[0].Price,
            BidSize = Bids[0].Size,
            AskSize = Asks[0].Size
        }
        : null;
}

/// <summary>
/// Individual price level in order book
/// </summary>
public record PriceLevel
{
    public decimal Price { get; init; }
    public int Size { get; init; }
    public int OrderCount { get; init; }
}