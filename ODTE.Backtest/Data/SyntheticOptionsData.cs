using CsvHelper;
using CsvHelper.Configuration;
using ODTE.Backtest.Config;
using ODTE.Backtest.Core;
using System.Globalization;

namespace ODTE.Backtest.Data;

/// <summary>
/// Prototype implementation: generates synthetic option quotes from underlying + volatility proxies.
/// WHY: Enables strategy testing without expensive real-time options data during development.
/// 
/// SYNTHETIC GENERATION METHOD:
/// 1. Use SPX spot price to generate strike grid (±1% to ±10% from ATM)
/// 2. Apply VIX9D/VIX as implied volatility proxies
/// 3. Calculate fair values using Black-Scholes model
/// 4. Add equity index skew (puts > calls IV)
/// 5. Generate bid-ask spreads based on volatility and time-to-close
/// 
/// ADVANTAGES:
/// - Free: No data vendor costs during development
/// - Fast: No API latency or rate limits
/// - Consistent: Repeatable backtests with same data
/// - Educational: Understand how IV affects option pricing
/// 
/// LIMITATIONS:
/// - No real market microstructure (liquidity, market makers)
/// - Simplified volatility surface (no term structure/skew complexity)
/// - Perfect Greeks (real world has bid-ask on Greeks too)
/// - No pin risk or early exercise modeling
/// 
/// UPGRADE PATH TO PRODUCTION:
/// Replace with ORATS/LiveVol/dxFeed adapters that provide:
/// - Real intraday option quotes
/// - Actual market-derived Greeks
/// - True liquidity metrics
/// - Historical volatility surfaces
/// 
/// References:
/// - VIX as IV proxy: https://www.cboe.com/tradable_products/vix/
/// - VIX9D short-term: https://www.cboe.com/us/indices/dashboard/vix9d/
/// - SPXW specifications: https://www.cboe.com/tradable_products/sp_500/spx_options/specifications/
/// - Professional data: https://orats.com/intraday-data-api
/// </summary>
public sealed class SyntheticOptionsData : IOptionsData
{
    private readonly SimConfig _cfg;
    private readonly IMarketData _md;
    private readonly List<(DateOnly d, double vix)> _vix;
    private readonly List<(DateOnly d, double vix9d)> _vix9d;

    public SyntheticOptionsData(SimConfig cfg, IMarketData md, string vixPath, string vix9dPath)
    {
        _cfg = cfg;
        _md = md;
        _vix = Load(vixPath, "vix");
        _vix9d = Load(vix9dPath, "vix9d");
    }

    /// <summary>
    /// Generate synthetic option quotes for 0DTE expiry.
    /// Creates a fine strike grid around current spot price with realistic deltas for strategy requirements.
    /// 
    /// STRIKE GRID GENERATION:
    /// - Fine 1-point increments from ATM-15 to ATM+15 (30 total strikes)
    /// - Ensures delta ranges match strategy requirements (0.07-0.20)
    /// - Provides proper strike spacing for 1-2 point width spreads
    /// 
    /// VOLATILITY SKEW MODELING:
    /// - Put skew: Higher IV for OTM puts (fear premium)
    /// - Call skew: Lower IV for OTM calls (less demand)
    /// - Based on typical equity index volatility smile
    /// 
    /// BID-ASK SPREAD MODELING:
    /// - Wider spreads when volatility is high
    /// - Extra width in final hour before expiration
    /// - Respects minimum tick size ($0.05)
    /// 
    /// STRATEGY OPTIMIZATION:
    /// - Designed for XSP 1-point spreads
    /// - Delta ranges support both single spreads (0.10-0.20) and condors (0.07-0.15)
    /// - Provides sufficient strike density for spread construction
    /// </summary>
    public IEnumerable<OptionQuote> GetQuotesAt(DateTime ts)
    {
        var S = _md.GetSpot(ts);
        if (S <= 0) yield break;

        // Adjust for XSP vs SPX pricing - convert to XSP if needed
        if (S > 1000) S = S / 10.0; // Convert SPX to XSP if data is SPX-level

        var exp = TodayExpiry(ts);
        double T = Math.Max((exp.ToDateTime(new TimeOnly(21, 0)) - ts).TotalDays / 365.0, 0.0005);
        var (ivS, ivL) = GetIvProxies(ts);
        double baseIv = Math.Max(0.05, Math.Min(0.80, ivS / 100.0)); // Convert % to decimal, clamp

        // Generate fine strike grid: 1-point increments around ATM
        double atmStrike = Math.Round(S);

        for (int offset = -15; offset <= 15; offset++)
        {
            double K = atmStrike + offset;
            if (K <= 0) continue;

            // Calculate moneyness for skew adjustment
            double moneyness = Math.Abs(K - S) / S;

            // Generate both put and call for each strike
            foreach (var right in new[] { Right.Put, Right.Call })
            {
                // Apply volatility skew based on moneyness and option type
                double skewAdjust = right == Right.Put
                    ? 1 + (moneyness * 2.0)  // Puts: higher IV for OTM (K < S)
                    : 1 + (moneyness * 1.0); // Calls: moderate IV increase for OTM (K > S)

                double iv = baseIv * skewAdjust;
                iv = Math.Max(0.05, Math.Min(1.0, iv)); // Clamp IV to reasonable range

                // Calculate theoretical values using Black-Scholes
                var delta = OptionMath.Delta(S, K, 0.00, 0.00, iv, T, right);
                var price = OptionMath.Price(S, K, 0.00, 0.00, iv, T, right);

                // Ensure minimum option value for liquidity
                price = Math.Max(0.05, price);

                var (bid, ask) = QuoteFromMid(price, baseIv, ts);
                yield return new OptionQuote(ts, exp, K, right, bid, ask, (bid + ask) / 2.0, delta, iv);
            }
        }
    }

    public DateOnly TodayExpiry(DateTime ts)
        => DateOnly.FromDateTime(ts);

    public (double shortIv, double thirtyIv) GetIvProxies(DateTime ts)
    {
        var d = DateOnly.FromDateTime(ts);
        double vix = _vix.FirstOrDefault(x => x.d == d).vix;
        double vix9 = _vix9d.FirstOrDefault(x => x.d == d).vix9d;

        if (vix == 0) vix = _vix.LastOrDefault(x => x.d <= d).vix;
        if (vix9 == 0) vix9 = _vix9d.LastOrDefault(x => x.d <= d).vix9d;

        return (vix9 <= 0 ? vix : vix9, vix);
    }

    private (double bid, double ask) QuoteFromMid(double mid, double baseIv, DateTime ts)
    {
        double tick = 0.05;

        // For 0DTE options, use tighter spreads and realistic pricing
        // Higher value options get tighter percentage spreads
        double spreadPct;
        if (mid >= 1.0)
            spreadPct = 0.05;  // 5% spread for ITM/ATM options
        else if (mid >= 0.25)
            spreadPct = 0.10;  // 10% spread for near-money options  
        else
            spreadPct = 0.20;  // 20% spread for far OTM options

        // Widen spreads in final hour
        var minsToClose = (TodayExpiry(ts).ToDateTime(new TimeOnly(21, 0)) - ts).TotalMinutes;
        if (minsToClose < 40) spreadPct *= 1.5;

        double half = mid * spreadPct / 2.0;

        // Calculate bid/ask with proper minimum values
        double rawBid = mid - half;
        double rawAsk = mid + half;

        // For very low value options, allow bids to go to zero but maintain minimum ask
        double bid = rawBid <= 0.05 ? 0.05 : Math.Floor(rawBid / tick) * tick;
        double ask = Math.Max(bid + tick, Math.Ceiling(rawAsk / tick) * tick);

        // Ensure ask is always at least one tick above bid
        if (ask <= bid) ask = bid + tick;

        return (bid, ask);
    }

    private static List<(DateOnly d, double val)> Load(string path, string valCol)
    {
        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true });

        if (valCol == "vix")
            return csv.GetRecords<VixRow>().Select(r => (DateOnly.Parse(r.date), double.Parse(r.vix))).ToList();
        else
            return csv.GetRecords<Vix9dRow>().Select(r => (DateOnly.Parse(r.date), double.Parse(r.vix9d))).ToList();
    }

    private sealed class VixRow { public string date { get; set; } = ""; public string vix { get; set; } = ""; }
    private sealed class Vix9dRow { public string date { get; set; } = ""; public string vix9d { get; set; } = ""; }
}