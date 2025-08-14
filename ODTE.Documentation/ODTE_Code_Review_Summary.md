# ODTE.Strategy — Expert Code Review Summary (Aug 14, 2025)

## Executive Summary
ODTE.Strategy has solid building blocks (backtester, optimizer, Syntricks) and a strong **capital-preservation ethos**. However, the system currently relies on **synthetic options data**, and the **hard pre-trade loss cap** is described at a **daily** level rather than enforced **per order**. To meet your end goals, you must: (1) make **OPRA NBBO + last sale** the default truth for options pricing, (2) enforce a **per-trade worst-case loss** gate tied to your **Reverse-Fibonacci** daily envelope, and (3) formalize **loss-run forensics** with **Syntricks replay** and ML-based “skip/allow” gating. This document consolidates findings, gaps, and a concrete implementation path.

---

## End Goals (Restated)
1. **Capital preservation first, then allocation.** No trade is allowed if its **max potential loss** would breach the **Reverse-Fibonacci** loss calculus (e.g., 500 → 300 → 200 → 100).
2. **Data fidelity:** Options evaluation, greeks, IV and fills should be referenced to **OPRA NBBO + last sale** (or a vendor delivering OPRA-consolidated data).
3. **Loss-run forensics:** Every losing trade is analyzed for **root cause**, replicated in **Syntricks**, and patterns are learned to pre-empt similar scenarios.
4. **Scalable validation:** A daily **data-quality gate** compares synthetic (if used for stress) vs OPRA-grade data and invalidates backtests that fail thresholds.
5. **Transparent, modular architecture:** Clear interfaces for market data, risk checks, logs, and reports with automated CI checks.

---

## Current State (Observed)
- **Core engines:** Backtester, genetic optimizer, and Syntricks are described as integrated and functioning.
- **Risk:** Reverse-Fibonacci **daily** loss limits are documented; **per-trade** guard not yet enforced at order-entry.
- **Market data:** Underlying via OHLCV (e.g., SPY/XSP). Options are **synthetic** with “realistic greeks.”
- **Execution:** Broker hookups / live or paper trading are WIP; Blazor PWA for monitoring exists/roadmapped.
- **Forensics:** “Learn from losers” intent exists, but the logging schema + ML loop + Syntricks replay are not yet concrete.

---

## Key Gaps
1. **Per-Order Risk Gate:** Missing hard **pre-trade** check to block any order whose worst-case loss breaches remaining daily Fibonacci budget.
2. **OPRA-Grade Options Feed:** Lacks OPRA NBBO + last sale as the **default** data source for quotes and tape verification.
3. **Forensics Pipeline:** No standardized **loss-run log schema**, clustering/replay process, or ML classifier to flag **skip/allow** conditions.
4. **Data Quality Harness:** No systematic daily comparison (MID/IV/Greeks) of synthetic vs OPRA-grade references; no parity/no‑arb checks.
5. **Auditability:** Need consistent JSONL logs + SQLite tables so every report is reproducible.

---

## Recommendations (Actionable)
1. **Adopt OPRA-grade reference:** Use a vendor that exposes OPRA NBBO + last sale (e.g., Polygon/DataBento for dev). Switch model inputs (mid, IV, greeks) to NBBO references.
2. **Implement Per-Trade Guardrail:** Enforce worst-case loss checks pre‑order, budgeted under the Fibonacci envelope, counting reserved risk on open positions.
3. **Stand Up Forensics Loop:** Emit structured JSONL on every close; nightly clustering of losers; Syntricks replay; promote ML rules only with out‑of‑sample validation.
4. **Daily Data-Quality Gate:** Heatmaps and thresholds for MID/IV/Greeks MAPE, parity violations, staleness, and coverage. Invalidate bad days.
5. **Defensive Defaults:** Fills = **NBBO mid ± slippage model**; synthetic options only for **stress**/counterfactuals (not truth).

---

## “OPRA NBBO + Last Sale as Default” — Definition
- **NBBO** (National Best Bid/Offer) = consolidated best bid/ask across all US options exchanges.
- **Last sale** = consolidated trades (“the tape”).  
- **Default:** Pricing, IV, greeks, and slippage modeling derive from NBBO mid; last sale validates the tape and liquidity. Exchange-direct feeds can be added later for depth/speed; NBBO remains pricing truth for validation.

---

## Interfaces & Guardrails (Proposed)

### Market Data Interfaces
```csharp
public interface IOptionsQuoteFeed {
    Task<OptionChain> GetChainAsync(string root, DateOnly expiry, DateTime asOfUtc);
}

public interface IUnderlyingFeed {
    Task<Bar> GetBarAsync(string symbol, DateTime asOfUtc);
}
```

### Pre-Trade Hard Cap (Reverse-Fibonacci)
```csharp
bool EnforceRiskCaps(PlannedOrder o, DayRiskState s)
{
    decimal maxLoss = o switch
    {
        CreditSpread cs => (cs.Width - cs.Credit) * cs.Multiplier,
        Butterfly bf   => (bf.WidestWing - bf.NetCredit) * bf.Multiplier,
        _              => throw new NotSupportedException("Undefined risk instrument")
    };

    decimal dailyCap = s.CurrentFibLossCap;   // e.g., 500 → 300 → 200 → 100
    decimal remainingToday = dailyCap - s.RealizedLossToday - s.ReservedRiskForOpenPositions;
    decimal perTradeBudget = Math.Max(0, Math.Floor(remainingToday / Math.Max(1, s.MaxNewTradesAllowed)));

    return maxLoss <= perTradeBudget;
}
```

### NBBO → IV & Comparison Metrics
```csharp
ImpliedVolResult ToIv(OptionQuote q, double spot, double r, double tYears)
{
    double mid = 0.5 * (q.Bid + q.Ask);
    return IvSolver.Solve(mid, spot, q.Strike, tYears, q.Right, r);
}

Evaluation Compare(OptionQuote synth, OptionQuote nbbo)
{
    double midSynth = 0.5 * (synth.Bid + synth.Ask);
    double midReal  = 0.5 * (nbbo.Bid + nbbo.Ask);
    return new Evaluation {
        MidMae = Math.Abs(midSynth - midReal),
        MidMape = Math.Abs(midSynth - midReal) / Math.Max(1e-4, midReal),
        SpreadPct = (nbbo.Ask - nbbo.Bid) / Math.Max(1e-4, midReal),
        // add IV/Greeks deltas once IVs computed
    };
}
```

---

## Storage & Logging (SQLite + JSONL)

### Tables
- `options_quotes(t, root, expiry, strike, right, bid, ask, last, exchange, conditions)`  
- `options_greeks(t, root, expiry, strike, right, iv_mid, delta_mid, gamma_mid, vega_mid, theta_mid)`  
- `eval_metrics(t, root, expiry, metric, value)`  
- `trades(t_open, t_close, symbol, expiry, right, strike, type, width, credit, maxLoss, exitPnL, exitReason)`

### Loss-Run JSONL Example
```json
{"t":"2025-08-14T14:31:02Z","symbol":"XSP","expiry":"2025-08-14",
 "right":"P","strike":530,"type":"credit_spread","width":2,"credit":0.58,
 "maxLoss":142,"exitPnL":-96,"exitReason":"delta_breach",
 "env":{"vix":15.8,"vix9d":14.2,"termSlope":-1.6,
        "vwapDev":-0.9,"orBreak":"down","atrPct":1.1,
        "fomcWindow":false,"regime":"trend_down"},
 "syntheticReplay":{"scenario":"fed_whipsaw","seed":91327}
}
```

---

## Data-Quality Harness (Synthetic vs OPRA)
**Per expiry × strike × timestamp:**  
- **Pricing:** MAE/MAPE of **mid**; **spread%**; staleness.  
- **IV Surface:** Solve IV from NBBO mid, compute MAE/MAPE vs synthetic IV.  
- **Greeks:** Finite-difference greeks from real-IV vs synthetic greeks.  
- **No-arbitrage:** Call-put parity, monotonicity in strike, butterfly positivity.  
- **Coverage:** % of OPRA series present in your chain.  

**Daily Report:** heatmaps by moneyness × tenor; league tables; **pass/fail** gate that blocks backtests that exceed thresholds.

---

## ML Loop & Syntricks Replay
1. **Nightly clustering** of losing trades by environment features (vol regime, term structure, momentum, event windows).  
2. **Syntricks replay** of each cluster’s representative scenarios (`scenario/seed`), confirming reproducibility.  
3. **Classifier** (e.g., gradient-boosted trees) trained to output **skip/allow** for fresh setups.  
4. **Guarded promotion**: new rules flagged and tested out-of-sample before production enablement.

---

## Roadmap as PRs
**PR-1 — OPRA Integration & Quality Gate**
- Add `IOptionsQuoteFeed` + `PolygonOptionsFeed` (or vendor of choice).
- Create SQLite schema, ingestion jobs, and **daily data-quality report** with pass/fail.
- Switch IV/Greeks to NBBO mid; document slippage model.

**PR-2 — Risk Guardrails & Auditability**
- Implement **per-trade Fibonacci** pre‑order guard.
- Reserve risk for open positions; block new orders when budget is saturated.
- Standardize JSONL logs + daily audit report (PnL by setup; max drawdown; VaR-lite).

**PR-3 — Loss Forensics, Syntricks Replay, ML**
- Emit rich loss logs, nightly clustering, Syntricks replay.
- First-pass classifier for **skip/allow**; wire into pre‑trade checks as a soft veto (override requires explicit flag).

---

## Acceptance Criteria (Definition of Done)
- **Data Truth:** All pricing/IV/greeks derive from **OPRA NBBO** (or equivalent vendor). Synthetic options are used only for stress tests.  
- **Risk Hard Stop:** No order can be queued if **maxLoss** exceeds **remaining Fibonacci budget** after accounting for open-risk reservations.  
- **Quality Gate:** Backtests/trading sessions **fail fast** if daily data-quality thresholds are breached.  
- **Forensics:** Every loser has a log entry; clusters are replayed in Syntricks; classifier recommendations are reported and versioned.  
- **Reports:** Nightly (and on-demand) HTML/MD reports for data-quality, risk utilization, PnL attribution, and ML recommendations.

---

## Notes & Defaults
- **Fills:** NBBO mid ± configurable slippage by spread% and quote conditions.  
- **Calendaring:** US market holidays/events calendar required for labeling (FOMC, CPI, NFP).  
- **Replay Seeds:** Store `scenario/seed` with each trade to enable deterministic Syntricks replays.  
- **CI Hooks:** Unit tests for IV solver edge cases; no‑arb checks must pass for any generated chain before use.

---

### Quick Win Checklist
- [ ] Vendor wired: `PolygonOptionsFeed` or equivalent OPRA-grade source
- [ ] Switch pricing/IV/greeks to NBBO mid
- [ ] Implement per-trade Fibonacci guard
- [ ] JSONL trade logs + nightly forensics job
- [ ] Data-quality report with pass/fail & heatmaps
- [ ] Syntricks replay job + first ML classifier (skip/allow)

---

*Prepared by: Code Review (ODTE.Strategy)*  
*Date: 2025‑08‑14 (Europe/London)*
