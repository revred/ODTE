using ODTE.Backtest.Config;
using ODTE.Backtest.Core;
using ODTE.Backtest.Data;

namespace ODTE.Backtest.Strategy;

/// <summary>
/// Translates regime decisions into concrete, risk-defined spread structures.
/// WHY: Converts high-level strategy signals into executable multi-leg option orders.
/// 
/// SPREAD CONSTRUCTION PHILOSOPHY:
/// All strategies are credit spreads (collect premium, defined risk):
/// - Iron Condor: Sell both call and put spreads (range-bound market)
/// - Put Credit Spread: Sell put, buy put at lower strike (bullish bias)
/// - Call Credit Spread: Sell call, buy call at higher strike (bearish bias)
/// 
/// RISK MANAGEMENT GUARDRAILS:
/// 1. Delta Bands: Only trade strikes within configured delta ranges
/// 2. Credit/Width Ratios: Ensure minimum premium collection per dollar of risk
/// 3. Liquidity Filters: Avoid wide bid-ask spreads (illiquid options)
/// 4. Width Constraints: Keep spreads narrow (1-2 points) for manageable risk
/// 
/// STRIKE SELECTION METHODOLOGY:
/// - Short leg: Primary income generator (higher delta, closer to ATM)
/// - Long leg: Protective wing (lower delta, further OTM)
/// - Width: Distance between strikes (max loss = width - credit)
/// - Credit: Net premium received (short premium - long premium)
/// 
/// XSP ADVANTAGES FOR SMALL ACCOUNTS:
/// - 1 point width = $100 max loss (vs $1000 for SPX)
/// - Easier position sizing for retail accounts
/// - Same cash settlement and liquidity benefits as SPX
/// 
/// QUALITY FILTERS:
/// - Credit/width ≥ 18-20% (avoid "selling pennies")
/// - Bid-ask spread ≤ 25% of credit (avoid illiquid strikes)
/// - Strike availability and proper spacing
/// 
/// References:
/// - Credit Spreads: https://www.investopedia.com/terms/c/creditspread.asp
/// - Iron Condor: https://www.investopedia.com/terms/i/ironcondor.asp
/// - XSP Cash Settlement: https://www.cboe.com/tradable_products/sp_500/mini_spx_options/cash_settlement/
/// - Options Risk: https://www.theocc.com/getmedia/a151a9ae-d784-4a15-bdeb-23a029f50b70/riskstoc.pdf
/// </summary>
public sealed class SpreadBuilder
{
    private readonly SimConfig _cfg;
    
    public SpreadBuilder(SimConfig cfg) 
    { 
        _cfg = cfg; 
    }

    /// <summary>
    /// Attempt to build a spread order based on regime decision and current market conditions.
    /// Returns null if no suitable spread can be constructed with current quotes.
    /// 
    /// CONSTRUCTION PROCESS:
    /// 1. Filter option chain to today's expiry (0DTE)
    /// 2. Check for adequate spot price data
    /// 3. Apply decision-specific logic (condor vs single-sided)
    /// 4. Validate credit/width ratios and liquidity
    /// 5. Return complete SpreadOrder or null if unsuitable
    /// 
    /// QUALITY CHECKS:
    /// - Strike availability within delta bands
    /// - Minimum credit/width ratios met
    /// - Bid-ask spreads not excessively wide
    /// - Proper protective wing spacing
    /// 
    /// FAILURE MODES:
    /// - No options quotes available (market closed, data issues)
    /// - No strikes within delta criteria (extreme market moves)
    /// - Insufficient credit for risk taken (low volatility)
    /// - Wide bid-ask spreads (illiquid strikes)
    /// </summary>
    public SpreadOrder? TryBuild(DateTime now, Decision decision, IMarketData md, IOptionsData od)
    {
        var exp = od.TodayExpiry(now);
        var quotes = od.GetQuotesAt(now).Where(q => q.Expiry == exp).ToList();
        if (!quotes.Any()) 
        {
            Console.WriteLine($"      DEBUG: No quotes available for {exp}");
            return null;
        }

        double spot = md.GetSpot(now);
        if (spot <= 0) 
        {
            Console.WriteLine($"      DEBUG: Invalid spot price: {spot}");
            return null;
        }

        // DEBUG: Show available option deltas
        Console.WriteLine($"      DEBUG: Spot={spot:F2}, Available quotes: {quotes.Count}");
        foreach (var q in quotes.OrderBy(q => q.Right).ThenBy(q => q.Strike))
        {
            Console.WriteLine($"        {q.Right} K={q.Strike:F1} Δ={Math.Abs(q.Delta):F3} Mid={q.Mid:F2} Credit={q.Bid:F2}");
        }

        // === LOCAL HELPER FUNCTIONS ===
        
        /// <summary>
        /// Select short and long strikes for single-sided spread.
        /// Picks closest delta in range for short leg, appropriate wing for long leg.
        /// </summary>
        (OptionQuote? shortQ, OptionQuote? longQ) pickSingle(Right r, double dMin, double dMax)
        {
            // Filter to strikes within delta criteria, order by delta (closest to threshold first)
            var side = quotes.Where(q => q.Right == r && Math.Abs(q.Delta) >= dMin && Math.Abs(q.Delta) <= dMax)
                             .OrderBy(q => Math.Abs(q.Delta))
                             .ToList();
            
            Console.WriteLine($"      DEBUG: pickSingle {r} delta [{dMin:F2}-{dMax:F2}]: found {side.Count} candidates");
            
            var sh = side.FirstOrDefault(); 
            if (sh is null) 
            {
                Console.WriteLine($"      DEBUG: No short strike found in delta range [{dMin:F2}-{dMax:F2}]");
                return (null, null);
            }
            
            // Calculate target strike for protective long leg
            double targetK = r == Right.Put 
                ? sh.Strike - _cfg.WidthPoints.Min  // Put spread: long leg below short
                : sh.Strike + _cfg.WidthPoints.Min; // Call spread: long leg above short
            
            // Find closest available strike to target width
            var lg = side.OrderBy(q => Math.Abs(q.Strike - targetK)).FirstOrDefault();
            return (sh, lg);
        }

        /// <summary>
        /// Validate spread components and create order if criteria met.
        /// Applies credit/width and liquidity filters.
        /// </summary>
        SpreadOrder? make(OptionQuote? sh, OptionQuote? lg, Decision t)
        {
            if (sh is null || lg is null) 
            {
                Console.WriteLine($"      DEBUG: make() failed - sh={sh?.Strike:F1} lg={lg?.Strike:F1}");
                return null;
            }
            
            double width = Math.Abs(lg.Strike - sh.Strike);
            double credit = Math.Max(0, (sh.Bid - lg.Ask));  // Net credit received
            double cpw = width > 0 ? credit/width : 0;
            
            Console.WriteLine($"      DEBUG: make() - Width={width:F1} Credit={credit:F2} C/W={cpw:F3} (need {_cfg.CreditPerWidthMin.Single:F3})");
            Console.WriteLine($"      DEBUG: Short: K={sh.Strike:F1} Bid={sh.Bid:F2} Ask={sh.Ask:F2}");
            Console.WriteLine($"      DEBUG: Long:  K={lg.Strike:F1} Bid={lg.Bid:F2} Ask={lg.Ask:F2}");
            
            // Quality filters
            if (cpw < _cfg.CreditPerWidthMin.Single) 
            {
                Console.WriteLine($"      DEBUG: Insufficient credit/width: {cpw:F3} < {_cfg.CreditPerWidthMin.Single:F3}");
                return null;  // Insufficient credit for risk
            }
            if ((sh.Ask - sh.Bid) > _cfg.Slippage.SpreadPctCap * Math.Max(credit, 0.10)) 
            {
                Console.WriteLine($"      DEBUG: Too illiquid: spread {sh.Ask - sh.Bid:F2} > {_cfg.Slippage.SpreadPctCap * Math.Max(credit, 0.10):F2}");
                return null;  // Too illiquid
            }
            
            return new SpreadOrder(
                now, 
                _cfg.Underlying, 
                credit, 
                width, 
                cpw, 
                t,
                new SpreadLeg(exp, sh.Strike, sh.Right, -1),  // Short leg (sell)
                new SpreadLeg(exp, lg.Strike, lg.Right, +1)); // Long leg (buy protection)
        }

        // === MAIN ROUTING LOGIC ===
        return decision switch
        {
            Decision.SingleSidePut => pickSinglePut(),
            Decision.SingleSideCall => pickSingleCall(),
            Decision.Condor => BuildCondor(now, quotes, exp),
            _ => null
        };
        
        /// <summary>Put credit spread: Sell put, buy put at lower strike (bullish bias)</summary>
        SpreadOrder? pickSinglePut()
        {
            var (sh, lg) = pickSingle(Right.Put, _cfg.ShortDelta.SingleMin, _cfg.ShortDelta.SingleMax);
            return make(sh, lg, Decision.SingleSidePut);
        }
        
        /// <summary>Call credit spread: Sell call, buy call at higher strike (bearish bias)</summary>
        SpreadOrder? pickSingleCall()
        {
            var (sh, lg) = pickSingle(Right.Call, _cfg.ShortDelta.SingleMin, _cfg.ShortDelta.SingleMax);
            return make(sh, lg, Decision.SingleSideCall);
        }
    }

    /// <summary>
    /// Build Iron Condor: simultaneous put and call credit spreads.
    /// WHY: Range-bound strategy that profits from time decay and low volatility.
    /// 
    /// IRON CONDOR STRUCTURE:
    /// - Sell Put Spread: Short put + Long put (lower strike)
    /// - Sell Call Spread: Short call + Long call (higher strike)
    /// - Collect credit on both sides
    /// - Profit if underlying stays between short strikes
    /// 
    /// DELTA SELECTION:
    /// Uses lower delta bands (7-15) for wider OTM placement
    /// - Lower probability of assignment
    /// - Higher probability of profit (both sides expire OTM)
    /// - Lower credit but better risk-adjusted returns
    /// 
    /// RISK PROFILE:
    /// - Max profit: Total credit received (if expires between short strikes)
    /// - Max loss: Width - Credit (if underlying moves beyond long strikes)
    /// - Breakevens: Short strikes ± credit received
    /// 
    /// SIMPLIFIED IMPLEMENTATION:
    /// Current version only returns put spread portion for simplicity.
    /// Full condor would require 4-leg order management complexity.
    /// 
    /// ENHANCEMENT OPPORTUNITIES:
    /// - True 4-leg iron condor construction
    /// - Asymmetric width selection based on skew
    /// - Dynamic delta adjustment based on market conditions
    /// </summary>
    private SpreadOrder? BuildCondor(DateTime now, List<OptionQuote> quotes, DateOnly exp)
    {
        // Filter puts and calls within condor delta criteria
        var puts = quotes.Where(q => q.Right==Right.Put && 
                Math.Abs(q.Delta)>=_cfg.ShortDelta.CondorMin && 
                Math.Abs(q.Delta)<=_cfg.ShortDelta.CondorMax)
            .OrderBy(q => Math.Abs(q.Delta))
            .ToList();
            
        var calls= quotes.Where(q => q.Right==Right.Call && 
                Math.Abs(q.Delta)>=_cfg.ShortDelta.CondorMin && 
                Math.Abs(q.Delta)<=_cfg.ShortDelta.CondorMax)
            .OrderBy(q => Math.Abs(q.Delta))
            .ToList();
        
        Console.WriteLine($"      DEBUG: BuildCondor delta [{_cfg.ShortDelta.CondorMin:F2}-{_cfg.ShortDelta.CondorMax:F2}]: found {puts.Count} puts, {calls.Count} calls");
        
        if (!puts.Any() || !calls.Any()) 
        {
            Console.WriteLine($"      DEBUG: BuildCondor failed - insufficient quotes (puts:{puts.Count}, calls:{calls.Count})");
            return null;
        }

        // Select strikes closest to delta criteria
        var sp = puts.First();   // Short put (primary income)
        var sc = calls.First();  // Short call (primary income)
        
        Console.WriteLine($"      DEBUG: Selected short strikes - Put K={sp.Strike:F1} Δ={Math.Abs(sp.Delta):F3}, Call K={sc.Strike:F1} Δ={Math.Abs(sc.Delta):F3}");
        
        // Find protective wings at target width from FULL option chain (not just delta-filtered)
        var allPuts = quotes.Where(q => q.Right == Right.Put).ToList();
        var allCalls = quotes.Where(q => q.Right == Right.Call).ToList();
        
        var lp = allPuts.OrderBy(q => Math.Abs(q.Strike - (sp.Strike - _cfg.WidthPoints.Min))).FirstOrDefault();
        var lc = allCalls.OrderBy(q => Math.Abs(q.Strike - (sc.Strike + _cfg.WidthPoints.Min))).FirstOrDefault();
        
        if (lp is null || lc is null) 
        {
            Console.WriteLine($"      DEBUG: BuildCondor failed - no protective wings (lp={lp?.Strike:F1}, lc={lc?.Strike:F1})");
            return null;
        }
        
        Console.WriteLine($"      DEBUG: Protective wings - Put K={lp.Strike:F1}, Call K={lc.Strike:F1}, Width={_cfg.WidthPoints.Min}");
        
        // Calculate combined credit (put spread + call spread)
        double width = _cfg.WidthPoints.Min; 
        double credit = Math.Max(0, (sp.Bid - lp.Ask) + (sc.Bid - lc.Ask));
        double cpw = width > 0 ? credit/width : 0;
        
        Console.WriteLine($"      DEBUG: Condor credit calc - Width={width:F1} Credit={credit:F2} C/W={cpw:F3} (need {_cfg.CreditPerWidthMin.Condor:F3})");
        Console.WriteLine($"      DEBUG: Put spread: {sp.Strike:F1} Bid={sp.Bid:F2} - {lp.Strike:F1} Ask={lp.Ask:F2} = {sp.Bid - lp.Ask:F2}");
        Console.WriteLine($"      DEBUG: Call spread: {sc.Strike:F1} Bid={sc.Bid:F2} - {lc.Strike:F1} Ask={lc.Ask:F2} = {sc.Bid - lc.Ask:F2}");
        
        // Apply quality filters
        if (cpw < _cfg.CreditPerWidthMin.Condor) 
        {
            Console.WriteLine($"      DEBUG: Insufficient condor credit/width: {cpw:F3} < {_cfg.CreditPerWidthMin.Condor:F3}");
            return null;  // Insufficient credit
        }
        if ((sp.Ask - sp.Bid) > _cfg.Slippage.SpreadPctCap * Math.Max(credit, 0.10)) 
        {
            Console.WriteLine($"      DEBUG: Too illiquid: spread {sp.Ask - sp.Bid:F2} > {_cfg.Slippage.SpreadPctCap * Math.Max(credit, 0.10):F2}");
            return null;  // Illiquid
        }

        // SIMPLIFIED: Return put spread only for prototype
        // TODO: Implement true 4-leg condor order management
        return new SpreadOrder(
            now, 
            _cfg.Underlying, 
            credit, 
            width, 
            cpw, 
            Decision.Condor,
            new SpreadLeg(exp, sp.Strike, Right.Put, -1),   // Short put
            new SpreadLeg(exp, lp.Strike, Right.Put, +1));  // Long put protection
    }
}