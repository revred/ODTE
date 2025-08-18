# CDTE Weekly Engine — ODTE Implementation Spec
**Version:** 1.0  
**Audience:** ODTE maintainers (Strategy, Historical, Execution, Backtest, UI)  
**Goal:** Implement a couple‑days‑to‑expiry (CDTE) weekly SPX/XSP engine that runs **live-like** on **real historical data** (20+ years), avoiding synthetic assumptions for strikes, slippage, or fills. All selections and fills must be derived from the dataset *available at the decision timestamp*.

---

## 0) Scope & Non‑Negotiables
- **Underlyings:** `SPXW` (PM‑settled weeklys) and `XSP` (mini‑SPX, cash‑settled). Default to XSP for finer risk steps; SPXW for larger notional.
- **Weekly cycle:** Enter **Monday**, manage **Wednesday**, exit into **Thursday/Friday** expiries; **flatten all** by Friday close.
- **Defined risk only:** All structures must cap risk at order entry (verticals, iron condors/flies, BWBs). **No naked short legs.**
- **No synthetic slippage:** Use recorded **NBBO bid/ask** (and where available last trade prints) from the historical dataset at the exact quote time. Orders are **marketable‑limit** against the recorded book. Unfilled orders are **missed**—no “assumed mid fills” if the market never crossed our limit.
- **No look‑ahead bias:** At each decision point (Mon entry, Wed manage, Thu/Fri exit), only access data with timestamps **≤ decision timestamp**.
- **Time discipline:** All timestamps handled in **ET** for trading logic; persisted with UTC in storage logs.
- **Risk bounds:** Per‑ticket max loss ≤ **$800**. (Use XSP units to fine‑tune; SPXW spreads typically 5 or 10 wide). Suggested starter grid: XSP $1–$3 widths; SPXW 5 wide with size = 0–1 depending on risk budget.
- **Reproducible:** Same inputs → same orders. All randomness must be seeded and logged.

---

## 1) Data Dependencies (Real, 20+ Years)
Your SQLite (or columnar) store is the source of truth. The engine **must not** synthesize strikes, IVs, fills, or slippage.

### 1.1 Required Tables (canonical shapes)
- **`UnderlyingPrices`**
  - `ts_utc`, `symbol`, `last`, `bid`, `ask`
  - Index: `(symbol, ts_utc)`
- **`OptionChains`**
  - `ts_utc`, `underlying`, `expiry`, `right`(C/P), `strike`, `bid`, `ask`, `last`, `iv`, `delta`, `gamma`, `theta`, `vega`, `oi`, `volume`
  - Index: `(underlying, expiry, ts_utc)` and `(underlying, ts_utc, right, delta)`
- **`SessionCalendar`**
  - Trading sessions, half‑days, holidays; fields: `date`, `open_et`, `close_et`, `product`
- **`EconEvents`** *(optional but recommended)*
  - `ts_et`, `event_type` (FOMC/CPI/NFP/etc), `severity` (1–3)
- **`SymbologyMap`**
  - Handle changes in SPX/XSP symbols over time; fields for vendor symbology to canonical.

### 1.2 Access Helpers
- **Chain snapshot** loader for a target `ts_et` rounded to next available quote (e.g., Monday **10:00:00 ET** → nearest ts ≥ 10:00:00).
- **Greeks view**: delta‑sorted view for fast strike picking.
- **NBBO** helper returning `(bestBid, bestAsk)` at ts for each leg.

### 1.3 Example Queries
```sql
-- Underlying spot at decision time (nearest at-or-after)
SELECT * FROM UnderlyingPrices
WHERE symbol='SPX' AND ts_utc >= :ts_utc
ORDER BY ts_utc ASC
LIMIT 1;

-- Option legs by delta bucket for Thursday expiry
SELECT strike, right, bid, ask, iv, delta
FROM OptionChains
WHERE underlying='SPX' AND expiry=:thu_exp AND ts_utc=:ts_utc
  AND right='P' AND delta BETWEEN -0.35 AND -0.15
ORDER BY ABS(delta+0.25) ASC;
```

---

## 2) Weekly Workflow (Deterministic)
### 2.1 Monday Entry (T‑3/T‑4)
- **Decision time:** 10:00 ET (avoid open noise; configurable).
- **Create two positions:**
  1) **Core** → **Thursday** expiry
  2) **Carry** → **Friday** expiry
- **Structure by regime (from real data at 10:00 ET):**
  - **Low IV (front IV < 15)** → **Broken Wing Butterfly (BWB)** around spot on the **put side** (cheaper convexity).  
  - **Mid IV (15–22)** → **Iron Condor** bracketing the **1‑day IV move**.  
  - **High IV (> 22)** or “event proximity” (see §3) → **Iron Fly with wings** or **tight IC** for faster theta.
- **Strike selection (no synthesis):**
  - **Primary rule: delta‑targeted** using recorded **Greeks** at 10:00 ET.
    - BWB put body ~ **Δ −0.30**; long wing Δ ~ −0.10; far wing chosen to hit target max loss ≤ $800.
    - IC short legs **|Δ| ~ 0.15–0.20**; wings set by risk cap.
    - IF body at **ATM**; wings at risk cap.
  - **Fallback:** If Greeks missing, compute **expected move** from **ATM IV** at 10:00 ET and map Δ targets via Black‑Scholes using **that IV only** (no future knowledge).
- **Order type & fill:**
  - Price = **marketable‑limit at or beyond NBBO** to seek fill (e.g., for credit structures place at `bestBid + α * spread`, α ∈ [0.25..0.5], seeded).
  - If the book **never crosses** our limit within a **30‑second window** (configurable) → **missed trade**; log as `NotFilled` (do not assume mid).

### 2.2 Wednesday Manage (12:30 ET)
On a fresh chain snapshot at **12:30 ET**:
- **P&L calc:** use NBBO mid (or conservative: bestBid for exits of credit positions).
- **Decision tree:**
  1) **Take‑Profit:** If **Core (Thu)** position ≥ **70%** of its **max planned profit** → **close Core**, keep Carry.
  2) **Neutral:** |PnL| < `NeutralBand` (e.g., < 15% of risk) → **roll Core to Friday** using **current Δ targets**; keep defined risk; **net roll debit ≤ 25%** of original net credit/debit cap.
  3) **Loss:** If drawdown ≥ **50%** of ticket risk → **close Core & Carry**, re‑enter **new Carry (Fri)** using **cheaper** convexity (smaller width / further strikes) to limit weekly bleed.
- **All transforms re‑price from real quotes at 12:30 ET**; unfilled rolls are counted as **NotFilled** and **aborted** (no fabricated fill).

### 2.3 Thursday/Friday Exit Discipline
- **Force‑exit times:**  
  - **XSP**: by **15:00 CT** on expiry day (dataset cut‑off aware).  
  - **SPXW**: by **15:15 CT** (regular close), or earlier per session calendar.
- **No overnight/next‑week carry.** If unfilled at first attempt, increase aggressiveness one step (tighter to NBBO), then **abandon** if still not filled—log explicitly.

---

## 3) Regime & Event Filters (From Real History)
- **Regime:** computed from **front IV**, **term structure slope** (e.g., 9‑day vs 30‑day IV), and **spot realized σ** trailing 5 days—all at decision time.
- **Event proximity:** If **FOMC/CPI/NFP** within **T‑2** sessions, reduce position widths (risk), broaden wings on IC/IF, and prefer **early take‑profits** on Wed.
- **Deterministic classifier:** Given inputs at 10:00 ET, the regime label is unique and fully logged.

---

## 4) Position Sizing & Risk
- **Per‑ticket risk cap:** ≤ **$800** absolute.  
- **Sizing ladder (default):**
  - **Win previous week:** size resets to base (e.g., 1x XSP BWB or 1x SPXW 5‑wide).  
  - **Loss previous week:** size **halves** (or switch to XSP equivalent).
- **Gamma brake:** If |Gamma| of Core exceeds threshold on **Thu 10:00 ET**, reduce exposure (take profits / narrow wings).

---

## 5) Fills From Historical NBBO (No Synthetic Slippage)
- **Entry/Exit/Adjust orders** are marketable‑limit against recorded NBBO at decision time.  
- **Execution simulator rules:**
  1) A limit order **fills** only if the historical **bestBid/bestAsk** **touches or improves** our limit within `FillWindow` (default 30s), *and* the opposite side remains within `MaxAdverseTick` (default 1 tick) during that window.  
  2) If **Last Trade** exists between our limit and the opposite NBBO, count as filled at our limit (never better).  
  3) Multi‑leg orders: require **ALL legs** to satisfy (1) within the window **on the same timestamp** bucket; otherwise **no fill**. (If your infra supports legging, enable deterministic **one‑leg‑at‑a‑time** with bounded slippage = real NBBO, never synthetic.)  
  4) **Partial fills** are allowed in XSP; size remainder may retry once with a more aggressive limit, else **missed**.
- **All misses are first‑class results.** We **do not** substitute mid or modeled slippage.

---

## 6) Strategy Menu (All Derived From Real Chain)
At Monday 10:00 ET, pick one of the following **by regime**:

### 6.1 Neutral Theta — Iron Condor (IC)
- Shorts: Δ ≈ ±0.15–0.20; Wings sized to meet risk ≤ $800.
- Target: 30–50% of max profit by Wed; 70% TP auto‑close rule for Core.

### 6.2 Directional Lean — Credit Vertical (Bear Call or Bull Put)
- Short leg Δ ≈ 0.25; long leg width chosen to cap risk.
- Prefer when spot trend and term structure slope agree.

### 6.3 Convex Catcher — Broken Wing Butterfly (BWB, Put skew preferred)
- Body Δ ≈ −0.30; near wing ≈ −0.15; far wing placed to keep net debit small & risk ≤ cap.
- Works best in low‑IV and mild down‑drift weeks.

*(All strikes must come directly from the recorded chain; if a target Δ does not exist, choose the nearest available Δ with lowest |Δtarget − Δ|.)*

---

## 7) Deterministic Wednesday Roll
- **Re‑center** using the same Δ targets from §6 at **12:30 ET** chain.
- **Rule:** Maintain defined risk; **net new debit ≤ 0.25 ×** original ticket risk; otherwise **no roll**.
- **Trending weeks:** collapse the threatened side to a **vertical** (keep risk ≤ cap).  
- **Chop weeks:** widen IC wings modestly if premium supports it (from the real chain).

---

## 8) Backtest Runner (Sparse, Resume‑able, Live‑Like)
- **SparseDayRunner:** Iterate the 20‑year sample in a **coverage‑maximizing** order (mixture of calm/volatile/event weeks) so you see signal quickly.
- **Checkpointing:** Persist week‑level state to resume; invalidation strategy on config change.
- **Intraday points:** Only the three decision snapshots are mandatory (Mon 10:00 ET, Wed 12:30 ET, Exit window on Thu/Fri). If you have intraday granularity, allow optional “Gamma Brake” checks Thursday 10:00 ET.
- **Outputs per week:**
  - Orders attempted (legs, limits, NBBO context, fill yes/no).  
  - P&L (mark method: mid or conservative bestBid for closing shorts; configurable).  
  - Miss log (why a trade didn’t fill).  
  - Regime & event labels.  
  - Risk trace (max loss at entry, realized drawdown).

---

## 9) Repository Structure Additions
```
ODTE.Strategy/
  CDTE/
    CDTEStrategy.cs
    CDTEConfig.cs
    CDTERollRules.cs
    CDTESignalPack.cs
    README.md

ODTE.Execution/
  HistoricalFill/
    NbboFillEngine.cs
    MultiLegMatcher.cs
    ExecutionPolicy.cs    # marketable-limit, windows, aggressiveness steps

ODTE.Historical/
  Providers/
    ChainSnapshotProvider.cs
    TermStructureProvider.cs
    EconEventProvider.cs
    SessionCalendarProvider.cs

ODTE.Backtest/
  Scenarios/CDTE/
    MondayToThuFriHarness.cs
    SparseDayRunner.cs
    Assertions.cs

ODTE.Start/Pages/
  cdte-weekly.razor
  components/
    CDTEHeatmap.razor
    WednesdayDecisionCard.razor

audit/
  CDTE_AUDITRUN.md
  sample_week_exports/
```

---

## 10) C# API Sketches (Concise)
```csharp
public sealed class CDTEStrategy : IStrategy
{
    public Task<PlannedOrders> EnterMondayAsync(
        ChainSnapshot snapshot10Et, CDTEConfig cfg);

    public Task<DecisionPlan> ManageWednesdayAsync(
        PortfolioState state, ChainSnapshot snapshot1230Et, CDTEConfig cfg);

    public Task<ExitReport> ExitWeekAsync(
        PortfolioState state, ChainSnapshot exitWindow, CDTEConfig cfg);
}
```

**Strike picking (delta‑first, fallback EM):**
```csharp
public static StrikeSelection PickByDelta(ChainSnapshot s, double targetDelta, OptionRight right)
    => s.For(right).OrderBy(k => Math.Abs(k.Delta - targetDelta)).First();

public static (double emPct, double atmIv) ExpectedMovePct(ChainSnapshot s, int dte)
{
    var atm = s.Atm();
    var atmIv = atm.Iv;
    var em = atmIv * Math.Sqrt(dte / 365.0);
    return (em, atmIv);
}
```

**Historical NBBO fill (marketable‑limit):**
```csharp
public FillResult TryFill(SpreadOrder o, NbboBook book, TimeSpan window, int maxAdverseTick)
{
    // 1) Limit must be marketable vs NBBO at t0
    if (!o.IsMarketable(book.At(o.T0))) return FillResult.NotMarketable;
    // 2) Within window, NBBO must cross or touch our limit; multi-leg synch required
    var crossed = book.Crosses(o, window, maxAdverseTick);
    return crossed ? FillResult.FilledAt(o.Limit) : FillResult.NotFilled;
}
```

---

## 11) Config (YAML)
```yaml
cdte:
  monday_decision_et: "10:00:00"
  wednesday_decision_et: "12:30:00"
  exit_cutoff_buffer_min: 20

  risk_cap_usd: 800
  take_profit_core_pct: 0.70
  max_drawdown_pct: 0.50
  neutral_band_pct: 0.15
  roll_debit_cap_pct_of_risk: 0.25

  delta_targets:
    ic_short_abs: 0.18
    vert_short_abs: 0.25
    bwb_body_put: -0.30
    bwb_near_put: -0.15

  regime_bands_iv:
    low: 15
    high: 22

  fill_policy:
    type: "marketable_limit"
    window_sec: 30
    max_adverse_tick: 1
    aggressiveness_steps: [0.25, 0.40, 0.50]  # fraction of spread toward the touch
```

---

## 12) Acceptance Criteria (Per Module)
- **ChainSnapshotProvider**: Given (underlying, expiry, ts_et) returns the *first* snapshot ≥ ts_et. Deterministic.
- **NbboFillEngine**: Unit tests cover: not marketable, crossed within window, multi‑leg mismatch, partials (XSP), retry aggressiveness.
- **CDTEStrategy**: Golden‑week tests reproduce exact legs and P&L with a frozen dataset slice.
- **SparseDayRunner**: Resume after interruption; invalidation re‑runs only affected weeks.
- **UI**: Heatmap displays weekly P&L; decision card reproduces Wed actions on replay.
- **Audit**: Week folder contains: orders.json, fills.json, miss_log.json, pnl.csv, regime.json, config.yml, and a SHA256 manifest.

---

## 13) Issue Checklist (Paste into GitHub)
- [ ] **Historical**: Implement `ChainSnapshotProvider` + tests  
- [ ] **Execution**: Implement `NbboFillEngine` (marketable‑limit, multi‑leg) + tests  
- [ ] **Strategy**: Implement `CDTEStrategy` (enter/manage/exit) + golden‑week tests  
- [ ] **Strategy**: Implement `CDTERollRules` (Thu→Fri) deterministic transforms + tests  
- [ ] **Backtest**: `MondayToThuFriHarness` + `SparseDayRunner` + resume & invalidation  
- [ ] **UI**: `/cdte-weekly` dashboard + `CDTEHeatmap` + `WednesdayDecisionCard`  
- [ ] **Ops**: `CDTE_AUDITRUN.md` runbook + sample month export  
- [ ] **Docs**: `ODTE/Strategy/CDTE/README.md` developer guide  

---

## 14) Edge Cases & Policies
- **Holidays/Half‑days:** Drive exit times from `SessionCalendar` per product; never rely on constants.
- **Chain sparsity:** If target Δ missing, choose nearest |Δtarget − Δ|; if wings can’t meet risk cap, **skip ticket** (log reason).
- **Large gaps (Mon→Wed):** If underlying moves > 1.5× Monday EM, switch to **vertical** rather than roll the entire complex (control risk).
- **Data gaps:** If no snapshot available at decision time, **defer** to next available minute (bounded by `MaxDeferMin`), else **skip**.
- **Mark method:** Default use **mid** for P&L; optionally **conservative** (credit exits at bid). Must be consistent across backtests.

---

## 15) KPIs for Validation
- **Weekly win rate**, **avg weekly P&L per $ risk**, **max weekly drawdown**, **percent of missed orders**, **fill latency** (seconds), **regime coverage** (low/mid/high IV weeks), **Sharpe/Sortino** on weekly series.

---

## 16) Minimal Developer Runbook
1. `cd ODTE.Backtest && dotnet test` (ensures providers & fill engine green).  
2. `dotnet run --project ODTE.Backtest --scenario CDTE --from 2005-01-03 --to 2025-07-31`  
3. Inspect `/audit/sample_week_exports/YYYY‑WW/` for artifacts and SHA manifest.  
4. Open `/Start/cdte-weekly` to visualize P&L heatmap and decision tree on replay.

---

### Notes
- This spec **assumes** the presence of SPX/XSP historical option chains with **NBBO** (bid/ask) and **Greeks**. If Greeks are missing, compute Greeks using the **same snapshot IV** per‑leg and the day’s risk‑free rate proxy from your dataset (no forward peeking).
- All *random* aggressiveness choices must use a seeded RNG; seed persisted in weekly audit to guarantee reproducibility.

---

**End of Spec**
