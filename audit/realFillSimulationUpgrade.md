# realFillSimulationUpgrade.md
**Version:** 1.0  
**Owner:** ODTE.Strategy — Risk, Execution & Research  
**Target Model(s):** PM212 + future ODTE models  
**Instrument Focus:** XSP (Mini‑SPX, 1/10th SPX), European‑style, cash‑settled

---

## 1) Purpose
Replace optimistic/idealized fills with a calibrated, **market‑microstructure aware** execution simulator so that backtests, paper/live, and audits align under realistic frictions: spreads, latency, partial fills, adverse selection, and capacity limits.

**Goal:** Models must be profitable under **Conservative** execution. “Optimistic” is allowed only for *upper‑bound* sensitivity, never for scoring or risk gating.

---

## 2) Architectural Overview
```
Signal Engine ──► Order Intents ──► RealisticFillEngine ──► Filled Trades ──► P&L
                          ▲                  │
                          │                  ├─ Uses quotes/NBBO, ToB size, profile, latency, VIX bin, time-of-day
                      RiskGate ◄─────────────┘
```

- **RealisticFillEngine**: Injectable component with pluggable profiles (conservative/base/optimistic).
- **RiskGate**: Uses **worst‑case fill** (touch + slip + penalties) for Reverse‑Fibonacci daily cap checks.
- **Audit Hooks**: Emit `mid_rate`, `pct_within_nbbo`, `slip_PF_5c`, `slip_PF_10c`, and “breach” events.

---

## 3) Execution Profiles (config‑driven)
Declare in `execution_config.yaml` and load at runtime. Research runners **must default to `conservative`**.

```yaml
execution:
  profile: conservative      # conservative | base | optimistic
  latency_ms: 250            # simulated decision→exchange latency
  max_tob_participation: 0.05  # ≤ 5% of Top‑of‑Book size per child order
  slippage_floor:
    per_contract: 0.02       # $0.02/contract minimum slippage
    pct_of_spread: 0.10      # +10% of prevailing spread
  size_penalty:
    bp_per_extra_tob_multiple: 8  # +8 bps of spread for each extra ToB multiple consumed
  adverse_selection_bps: 25   # add 25 bps of spread when quote ticks against you during latency
  mid_fill:
    p_when_spread_leq_20c:
      conservative: 0.00
      base: 0.15
      optimistic: 0.30
    p_otherwise:
      conservative: 0.00
      base: 0.05
      optimistic: 0.15
  event_overrides:
    open_close_window_minutes: 10   # within ±10m of open/close
    set_mid_probability_to_zero: true
```

**Rule:** If a backtest isn’t profitable under **conservative**, it **fails**.

---

## 4) Fill Simulation Algorithm (per order)

**Inputs:** order `o`, quote snapshot `q0` (bid/ask/mid, ToB size), profile `cfg`, market state `mkt` (time‑of‑day, VIX bin).  
**Outputs:** one or more child fills `{price, size, ts}`.

1. **Compute spread** `S = max(0, ask − bid)` and **ToB size** `Q` (use proxy if exact size missing).
2. **Split sizing** to enforce participation: `child_size ≤ cfg.max_tob_participation * Q` (create K children).
3. For each child:
   - Decide **attempt@mid**: `Bernoulli(P_mid)` where `P_mid` depends on `S`, time‑of‑day, VIX, and `cfg`.
   - **Latency**: wait `L ~ Normal(cfg.latency_ms, 50ms)`; fetch `q1` (re‑check NBBO).
   - If **mid attempt**:
     - If `q1` unchanged/improved and `rand < P_mid`: **fill at mid(q1)**.
     - Else **reprice to touch** (buy@ask, sell@bid).
   - If **touch**: set `px = ask` (buy) or `bid` (sell).
   - **Slippage floor**: `slip_floor = max(cfg.slippage_floor.per_contract, cfg.slippage_floor.pct_of_spread * S)`.
   - **Adverse selection**: if best side moved against the order during `L`, add `cfg.adverse_selection_bps/10000 * S` to `px` (worse).
   - **Size penalty**: if child uses multiple ToB, add `(multiples-1) * cfg.size_penalty.bp_per_extra_tob_multiple/10000 * S`.
   - Record child fill.
4. The order’s **avg fill** is the size‑weighted average of child fills.
5. Emit fill diagnostics: intended vs achieved price, mid attempt status, latency, penalties applied.

**Price formula summary (buy example):**
```
px_fill = ask(q1)
        + slip_floor
        + adverse_bps * S
        + size_bps * S
(If mid accepted: px_fill = mid(q1) without touch terms)
```

---

## 5) Calibration Plan (XSP Microstructure)

1. **Data grid**: build bins by **time‑of‑day** (e.g., 5‑min buckets) × **VIX** (e.g., [<14, 14–20, 20–30, >30]).
2. For each bin, compute from paper/live or high‑quality tick data:
   - Median **spread** `S` (in $).
   - **P(mid acceptance)** for small passive limits at mid.
   - **Adverse selection**: average unfavorable move (in bps of S) within 250–500 ms after order time.
3. Seed defaults conservatively; update monthly with **paper/live shadow** (tiny size).  
4. Maintain **profile files** per venue/instrument:
   - `xsp_execution_calibration.yaml`
   - Include event overrides (FOMC/CPI/quad‑witching → `P(mid)=0`, widened spread multipliers).

---

## 6) Test Harness & Metrics

### 6.1 Monte Carlo per-signal
For each signal, simulate **N=200–1000** execution paths:
- Outputs: median fill, P25/P05 fills, daily P&L distribution → **use P05** for safety budgeting.
- Approve a regime only if **P05 P&L** passes the desk’s hurdle.

### 6.2 Daily Summary & Audit Metrics (must log)
- `mid_rate` (share of units filled ≥ mid) — **target < 60%**
- `pct_within_nbbo_band` ([bid−$0.01, ask+$0.01]) — **target ≥ 98%**
- `slip_PF_5c`, `slip_PF_10c` — PF under extra $0.05/$0.10 per‑contract penalties  
  **targets:** PF ≥ **1.30** (5c) and ≥ **1.15** (10c).
- `avg_latency_ms`, `avg_adverse_bps`, `avg_size_bps` (for diagnostics).

### 6.3 Event Day Regimes
- Set `P(mid)=0`, increase `slippage_floor.pct_of_spread` by +5–10pp, and apply stricter ToB participation.
- Separate daily reports for event and non‑event days.

---

## 7) Integration Points (C#)

### 7.1 Interface & Engine
```csharp
public interface IFillEngine
{
    FillResult SimulateFill(Order o, Quote q, ExecConfig cfg, MarketState mkt);
}

public sealed class RealisticFillEngine : IFillEngine { /* see implementation notes */ }

public sealed record ExecConfig( /* values mapped from YAML */ );
public sealed record Fill(decimal Price, int Size, DateTime Ts);
public sealed record FillResult(IReadOnlyList<Fill> Children);
```

### 7.2 RiskGate (Reverse‑Fibonacci)
Before sending any order:
```csharp
var worstCasePx = o.IsBuy ? q.Ask : q.Bid;
var worstCaseLoss = MaxLossStructure(o, worstCasePx) 
                    + SlipFloor(cfg, q) 
                    + SizePenalty(cfg, o, q) 
                    + AdverseSelectionWorstCase(cfg, q);
if (RealizedLossToday + worstCaseLoss > AllowedDailyLossFib(currentStreak))
    Reject(o);
```
- **AllowedDailyLossFib:** $500 → $300 → $200 → $100 (reset after green day).

### 7.3 CI Enforcement
- Default profile = **conservative** in tests.
- Unit test fails if any run reports `mid_rate > 0.60`, `pct_within_nbbo < 0.98`, or `PF_5c/10c` below thresholds.

---

## 8) Rollout Plan

1. **Phase 0 — Code & Config**  
   - Implement `RealisticFillEngine` + YAML loader.  
   - Wire RiskGate worst‑case logic.
2. **Phase 1 — Backtest Re‑baseline**  
   - Re-run PM212 under **conservative**; store full metrics; attach to audit.
3. **Phase 2 — Paper “Shadow” (Tiny Size)**  
   - 4 weeks: collect real fills; update calibration YAML.
4. **Phase 3 — Base Profile Unlocked**  
   - If thresholds pass with updated calibration, allow `base` for research sensitivity only.
5. **Phase 4 — Production**  
   - Keep default **conservative** for scoring; CI blocks optimistic scoring.

---

## 9) Acceptance Criteria (Go/No‑Go)

- ✅ **Guardrail breaches = 0** (audit)  
- ✅ `pct_within_nbbo ≥ 98%` and `mid_rate < 60%`  
- ✅ `slip_PF_5c ≥ 1.30` and `slip_PF_10c ≥ 1.15`  
- ✅ Paper/live shadow confirms calibration (no >5 pp drift in NBBO/mid metrics)  
- ✅ RiskGate blocks orders that would breach the daily Reverse‑Fib limit using worst‑case fills

---

## 10) Appendices

### 10.1 Definitions
- **NBBO band**: price must lie within `[bid−$0.01, ask+$0.01]` at the time of fill check.
- **Spread (S)**: `ask − bid`.
- **bps of spread**: `(bps/10000) * S`.
- **ToB participation**: fraction of top‑of‑book size your child order may be.

### 10.2 Missing Data Fallbacks
- If ToB size is unknown, set a conservative default (e.g., 25% of median depth) and **tighten** participation further.
- If quotes missing for a timestamp, degrade to touch fills with extra `adverse_selection_bps` and log a warning.

### 10.3 Determinism
- Pin RNG seed per run; log it in the audit. Re-runs must reproduce identical Monte Carlo paths for the same seed.

---

**End of Document — realFillSimulationUpgrade.md**