using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using ODTE.Backtest.Core;
using ODTE.Backtest.Config;

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
    /// Creates a coarse strike grid around current spot price with realistic skew and spreads.
    /// 
    /// STRIKE GRID GENERATION:
    /// - Covers ±1% to ±10% from ATM in 1% increments
    /// - Provides 20 total strikes (10 puts + 10 calls)
    /// - Realistic for liquid SPX/XSP option chains
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
    /// LIMITATIONS:
    /// - No support levels or technical strike clustering
    /// - Linear skew vs real convex smile
    /// - No liquidity holes at extreme strikes
    /// </summary>
    public IEnumerable<OptionQuote> GetQuotesAt(DateTime ts)
    {
        var S = _md.GetSpot(ts);
        if (S <= 0) yield break;
        
        var exp = TodayExpiry(ts);
        double T = Math.Max((exp.ToDateTime(new TimeOnly(21,0)) - ts).TotalDays/365.0, 0.0005);
        var (ivS, ivL) = GetIvProxies(ts);
        double baseIv = Math.Max(0.05, Math.Min(0.80, ivS/100.0)); // Convert % to decimal, clamp

        // Generate strikes at 1%, 2%, ..., 10% from ATM
        foreach (var pct in Enumerable.Range(1, 10))
        {
            double kPut = S*(1- pct/100.0);   // OTM puts below spot
            double kCall = S*(1+ pct/100.0);  // OTM calls above spot
            
            // Apply volatility skew (equity index characteristic)
            double skewPut = baseIv * (1 + 0.10*pct/10.0);  // Puts: +10% IV at 10% OTM
            double skewCall = baseIv * (1 - 0.05*pct/10.0); // Calls: -5% IV at 10% OTM

            // Calculate theoretical values using Black-Scholes
            var dPut = OptionMath.Delta(S, kPut, 0.00, 0.00, skewPut, T, Right.Put);
            var pPut = OptionMath.Price(S, kPut, 0.00, 0.00, skewPut, T, Right.Put);
            var dCall = OptionMath.Delta(S, kCall, 0.00, 0.00, skewCall, T, Right.Call);
            var pCall = OptionMath.Price(S, kCall, 0.00, 0.00, skewCall, T, Right.Call);

            // Generate quotes for both puts and calls at this moneyness level
            foreach (var (right, K, d, mid, iv) in new[]
            { 
                (Right.Put, kPut, dPut, pPut, skewPut), 
                (Right.Call, kCall, dCall, pCall, skewCall) 
            })
            {
                var (bid, ask) = QuoteFromMid(mid, baseIv, ts);
                yield return new OptionQuote(ts, exp, K, right, bid, ask, (bid+ask)/2.0, d, iv);
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
        double pct = 0.10 + 0.5 * baseIv; 
        var minsToClose = (TodayExpiry(ts).ToDateTime(new TimeOnly(21,0)) - ts).TotalMinutes;
        
        if (minsToClose < 40) pct += 0.10;
        pct = Math.Min(pct, 0.40);
        
        double half = mid * pct / 2.0;
        double tick = 0.05; 
        double bid = Math.Max(0.05, Math.Floor((mid - half)/tick)*tick);
        double ask = Math.Ceiling((mid + half)/tick)*tick;
        
        return (bid, ask);
    }

    private static List<(DateOnly d, double val)> Load(string path, string valCol)
    {
        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture){ HasHeaderRecord = true });
        
        if (valCol == "vix")
            return csv.GetRecords<VixRow>().Select(r => (DateOnly.Parse(r.date), double.Parse(r.vix))).ToList();
        else
            return csv.GetRecords<Vix9dRow>().Select(r => (DateOnly.Parse(r.date), double.Parse(r.vix9d))).ToList();
    }

    private sealed class VixRow { public string date { get; set; } = ""; public string vix { get; set; } = ""; }
    private sealed class Vix9dRow { public string date { get; set; } = ""; public string vix9d { get; set; } = ""; }
}