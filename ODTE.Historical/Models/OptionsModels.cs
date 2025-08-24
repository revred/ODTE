namespace ODTE.Historical;

/// <summary>
/// Represents a complete options chain for a specific underlying and expiration
/// </summary>
public class OptionsChain
{
    public string Symbol { get; set; } = "";
    public DateTime ExpirationDate { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public decimal UnderlyingPrice { get; set; }
    public List<OptionContract> Options { get; set; } = new();

    /// <summary>
    /// Get all call options in the chain
    /// </summary>
    public List<OptionContract> Calls => Options.Where(o => o.Type == OptionType.Call).ToList();

    /// <summary>
    /// Get all put options in the chain
    /// </summary>
    public List<OptionContract> Puts => Options.Where(o => o.Type == OptionType.Put).ToList();

    /// <summary>
    /// Get option contract by strike and type
    /// </summary>
    public OptionContract? GetOption(decimal strike, OptionType type)
    {
        return Options.FirstOrDefault(o => o.Strike == strike && o.Type == type);
    }

    /// <summary>
    /// Get strikes within a delta range
    /// </summary>
    public List<decimal> GetStrikesInDeltaRange(decimal minDelta, decimal maxDelta, OptionType type)
    {
        return Options
            .Where(o => o.Type == type && Math.Abs(o.Delta) >= minDelta && Math.Abs(o.Delta) <= maxDelta)
            .Select(o => o.Strike)
            .OrderBy(s => s)
            .ToList();
    }

    /// <summary>
    /// Calculate total volume and open interest
    /// </summary>
    public (long TotalVolume, long TotalOpenInterest) GetTotals()
    {
        var totalVolume = Options.Sum(o => o.Volume);
        var totalOpenInterest = Options.Sum(o => o.OpenInterest);
        return (totalVolume, totalOpenInterest);
    }
}

/// <summary>
/// Individual option contract data
/// </summary>
public class OptionContract
{
    public string Symbol { get; set; } = "";  // Full option symbol (USO240119C00012000)
    public OptionType Type { get; set; }
    public decimal Strike { get; set; }
    public DateTime ExpirationDate { get; set; }
    public int DaysToExpiration { get; set; }

    // Pricing
    public decimal Bid { get; set; }
    public decimal Ask { get; set; }
    public decimal Last { get; set; }
    public decimal Mark => (Bid + Ask) / 2;
    public decimal Spread => Ask - Bid;

    // Volume and Interest
    public long Volume { get; set; }
    public long OpenInterest { get; set; }

    // Greeks
    public decimal ImpliedVolatility { get; set; }
    public decimal Delta { get; set; }
    public decimal Gamma { get; set; }
    public decimal Theta { get; set; }
    public decimal Vega { get; set; }

    // Underlying
    public decimal UnderlyingPrice { get; set; }
    public decimal Moneyness => UnderlyingPrice / Strike;
    public bool IsInTheMoney => Type == OptionType.Call ? UnderlyingPrice > Strike : UnderlyingPrice < Strike;
    public bool IsOutOfTheMoney => !IsInTheMoney;

    /// <summary>
    /// Calculate intrinsic value
    /// </summary>
    public decimal IntrinsicValue
    {
        get
        {
            return Type == OptionType.Call
                ? Math.Max(0, UnderlyingPrice - Strike)
                : Math.Max(0, Strike - UnderlyingPrice);
        }
    }

    /// <summary>
    /// Calculate time value
    /// </summary>
    public decimal TimeValue => Math.Max(0, Mark - IntrinsicValue);

    /// <summary>
    /// Check if option has sufficient liquidity
    /// </summary>
    public bool HasSufficientLiquidity(decimal maxSpreadPct = 0.20m, long minVolume = 10, long minOpenInterest = 100)
    {
        var spreadPct = Spread / Mark;
        return spreadPct <= maxSpreadPct &&
               (Volume >= minVolume || OpenInterest >= minOpenInterest);
    }
}

/// <summary>
/// Option contract type
/// </summary>
public enum OptionType
{
    Call = 0,
    Put = 1
}


/// <summary>
/// Options market data for strategy analysis
/// </summary>
public class OptionsMarketData
{
    public string UnderlyingSymbol { get; set; } = "";
    public DateTime Date { get; set; }
    public decimal UnderlyingPrice { get; set; }

    // Volume and Interest Metrics
    public long TotalCallVolume { get; set; }
    public long TotalPutVolume { get; set; }
    public decimal PutCallVolumeRatio => TotalCallVolume > 0 ? (decimal)TotalPutVolume / TotalCallVolume : 0;

    public long TotalCallOpenInterest { get; set; }
    public long TotalPutOpenInterest { get; set; }
    public decimal PutCallOpenInterestRatio => TotalCallOpenInterest > 0 ? (decimal)TotalPutOpenInterest / TotalCallOpenInterest : 0;

    // Volatility Metrics
    public decimal ImpliedVolatility30Day { get; set; }
    public decimal HistoricalVolatility30Day { get; set; }
    public decimal VolatilitySkew { get; set; }

    // Flow Analysis
    public decimal NetCallDelta { get; set; }
    public decimal NetPutDelta { get; set; }
    public decimal NetGamma { get; set; }
    public decimal NetTheta { get; set; }

    /// <summary>
    /// Calculate market sentiment based on options flow
    /// </summary>
    public MarketSentiment CalculateSentiment()
    {
        var putCallRatio = PutCallVolumeRatio;

        if (putCallRatio < 0.7m) return MarketSentiment.Bullish;
        if (putCallRatio > 1.3m) return MarketSentiment.Bearish;
        return MarketSentiment.Neutral;
    }
}

/// <summary>
/// Market sentiment based on options flow
/// </summary>
public enum MarketSentiment
{
    Bullish,
    Neutral,
    Bearish
}

/// <summary>
/// Options trading strategy result
/// </summary>
public class OptionsStrategyResult
{
    public string StrategyName { get; set; } = "";
    public List<OptionContract> Legs { get; set; } = new();
    public decimal NetPremium { get; set; }  // Positive = credit, Negative = debit
    public decimal MaxProfit { get; set; }
    public decimal MaxLoss { get; set; }
    public decimal BreakevenLower { get; set; }
    public decimal BreakevenUpper { get; set; }
    public decimal ProfitProbability { get; set; }

    /// <summary>
    /// Calculate strategy P&L at expiration for given underlying price
    /// </summary>
    public decimal CalculatePnLAtExpiration(decimal underlyingPrice)
    {
        decimal pnl = NetPremium; // Start with premium received/paid

        foreach (var leg in Legs)
        {
            var intrinsicValue = leg.Type == OptionType.Call
                ? Math.Max(0, underlyingPrice - leg.Strike)
                : Math.Max(0, leg.Strike - underlyingPrice);

            // Assume we're short the option if we received premium, long if we paid
            var position = NetPremium > 0 ? -1 : 1;
            pnl += position * intrinsicValue;
        }

        return pnl;
    }
}