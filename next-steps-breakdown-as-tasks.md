
# ODTE — Code Review & Action Plan (BWB / Iron Condor)  
**Date:** 2025-08-15T04:47:35.612707Z

> Scope: Review current ODTE strategy implementation (IC → BWB focus), add **MaxPotentialLoss** as a first-class metric, expand tests, and tighten regime-driven trade selection under the Reverse‑Fibonacci (RFib) loss policy.

---

## 1) Executive Summary

- **What’s working**
  - Regime-aware selection and IC baseline are in place; RFib cap prevents large daily losses.
  - 5‑year IC backtest shows **75% win‑rate** with stable max daily draw (**−$343**) and manageable loss streaks.
  - Logging and dashboards exist for high-level performance views.

- **Gaps to close (P0)**
  1. **MaxPotentialLoss** not computed as a **first-class metric** at order construction and logged into the ledger.  
  2. **IC→BWB morph rules** and BWB construction gates to be formalized and unit-tested.  
  3. **Regime switcher** must *suppress IC* in Convex (high-vol/trend) and *prefer BWB or convex overlay*.  
  4. **Ledger/metrics** missing RFib utilization, expected shortfall, and per-trade ROC (credit ÷ max loss).

- **Outcome target:** Same RFib cap, but improved **ROC** and **tail monetization** via BWB; reduce volatile-regime losses; prepare for incremental tail overlays later.

---

## 2) Design Reviews & Recommendations

### 2.1 Strategy Interfaces (P0)
Introduce clear contracts for strategy geometry, pricing, risk, and execution:

```csharp
public interface IStrategyShape
{
    string Name { get; }               // "IronCondor", "CreditBWB"
    ExerciseStyle Style { get; }       // European/American (for pricing)
    IReadOnlyList<OptionLeg> Legs { get; }
}

public interface IRiskModel
{
    decimal MaxPotentialLoss(IStrategyShape shape, decimal netCredit, int multiplier = 100);
    decimal MarginRequired(IStrategyShape shape);
    RiskProfile Profile(IStrategyShape shape); // Δ, Γ, Vega targets, stress kinks
}

public interface IEntryGate
{
    bool Allow(MarketSnapshot mkt, StrategyInputs inp, out string reason);
}

// Use this at order build-time:
public sealed record CandidateOrder(
    IStrategyShape Shape,
    decimal NetCredit,
    decimal MaxPotentialLoss,
    decimal Roc,                       // NetCredit / MaxPotentialLoss
    decimal RfibUtilization,           // (OpenRisk + MaxPotentialLoss) / DailyCap
    string  Reason                     // classifier/gates transcript
);
```

**Why:** Makes **MaxPotentialLoss** and **ROC** first-class, so sizing, gates, and RFib checks can be deterministic and testable.

---

### 2.2 MaxPotentialLoss Formulas (P0)

**Iron Condor (symmetric, per contract):**
```csharp
// Call-side loss if price >> short call:
LossCall  = (CallWingWidth  - NetCredit) * multiplier;

// Put-side loss if price << short put:
LossPut   = (PutWingWidth   - NetCredit) * multiplier;

MaxPotentialLossIC = Math.Max(LossCall, LossPut);
```

**Credit Broken‑Wing Butterfly (single-tail emphasis):**
```csharp
// Narrow wing distance: |Body - NearWing|
// Far wing distance:    |FarWing - Body|
// Single-tail risk on the "broken" side:
MaxPotentialLossBWB = ((FarWing - NarrowWing) * multiplier) - (NetCredit * multiplier);

// If geometry yields negative loss (rare), clamp to zero.
MaxPotentialLossBWB = Math.Max(0m, MaxPotentialLossBWB);
```
> **Notes:**  
> • Use `decimal` for prices/Greeks aggregation to avoid FP drift in ledger math.  
> • Respect contract multipliers: 100 (equities), 10 (minis), 1 (micros).  
> • Include fees: `MaxPotentialLossAfterFees = MaxPotentialLoss + EstCommission + SlippageReserve` (policy‑defined).

---

### 2.3 Geometry Gates & Defaults (P0)

**IC**
- Symmetric wings (20–40 pts SPX). `credit/width ≤ 1/3`  
- Entry Δ ≈ 0 per contract (±2), both tails capped.

**Credit BWB**
- Body at **15–25Δ**; **narrow** 5–10 pts; **far** = (3–4) × narrow.  
- Net credit **≥ 20–35%** of narrow width.  
- |net‑Δ| ≤ 3 per contract at entry.

**Common**
- **VIX sizing rule:** VIX>30 ⇒ 0.5× size; VIX>40 ⇒ 0.25×; suppress IC when VIX>40.  
- **Trend breaker:** |Trend5m| ≥ 0.8 ⇒ block new entries (cooldown).

---

### 2.4 Classifier (Regime Switcher) (P0)

- **Calm:** Prefer **BWB** (income with better ROC than IC).  
- **Mixed:** BWB + (optional) cheap tail ticket on risk side.  
- **Convex:** Suppress IC; allow BWB only if ROC ≥ threshold and delta small; else ratio backspread (future module).

Logging: persist *regime evidence* (IVR, VIX, term slope, trend score, realized/ implied).

---

### 2.5 Ledger & Telemetry (P0)

Add the following fields to **every candidate & filled order**:
- `MaxPotentialLoss` (post‑fees), `Roc`, `RfibUtilization`, `MarginAtEntry`  
- `BodyDelta`, `GammaAtBody`, `CreditToWidth`, `LiquidityFlags`  
- `Regime`, `ReasonCodes` (why allowed/denied), `DecisionTimeMs`

These unlock deterministic tests and dashboards.

---

## 3) Concrete Code Inserts

### 3.1 RiskModel implementation (C#) (P0)

```csharp
public static class RiskModel
{
    public static decimal MaxPotentialLossIC(
        decimal netCredit, decimal putWing, decimal callWing, int multiplier = 100)
    {
        // per‑contract
        var callLoss = (callWing - netCredit) * multiplier;
        var putLoss  = (putWing  - netCredit) * multiplier;
        return Math.Max(callLoss, putLoss);
    }

    public static decimal MaxPotentialLossBwb(
        decimal netCredit, decimal narrowWing, decimal farWing, int multiplier = 100)
    {
        var core = ((farWing - narrowWing) * multiplier) - (netCredit * multiplier);
        return Math.Max(0m, core);
    }
}
```

**Integration (order build):**
```csharp
var mpl = shape.Name switch
{
    "IronCondor" => RiskModel.MaxPotentialLossIC(netCredit, putWing, callWing, multiplier),
    "CreditBWB"  => RiskModel.MaxPotentialLossBwb(netCredit, narrowWing, farWing, multiplier),
    _            => throw new NotSupportedException(shape.Name)
};

var candidate = new CandidateOrder(shape, netCredit, mpl,
    Roc: mpl > 0 ? netCredit * multiplier / mpl : 0m,
    RfibUtilization: (TodayOpenRisk + mpl) / DailyCap,
    Reason: classifierTranscript);
```

---

### 3.2 xUnit tests for MPL (P0)
- IC: various `credit/width` pairs; verify `MaxPotentialLossIC` equals worse side.  
- BWB: (narrow, far) = (10, 30/40); ensure non‑negative; ROC improves as far increases until credit gate fails.  
- Multiplier variants: 100/10/1.  
- Fees: add fixed + per‑leg and assert `MPLAfterFees ≥ MPL`.

---

## 4) Test Expansion (add to the existing 500)

**Add 120 focused tests** (brings suite to 620):

- **MPL Computation (32 tests)**  
  - IC symmetric widths {{20, 30, 40}} × credits grid; BWB narrow {{5,7,10}} × far {{3,3.5,4}}×narrow.  
  - Negative/pathological inputs clamped; multiplier coverage.

- **RFib Enforcement (20)**  
  - Candidate rejected if `OpenRisk + MPL > DailyCap`; rollover to next session resets allowance.

- **ROC & Sizing (20)**  
  - `contracts = floor(DailyCap / MPL)`, zero when <1.  
  - ROC threshold gate blocks thin credits.

- **Classifier Suppression (16)**  
  - IC blocked when VIX>40; BWB allowed only if ROC≥min and Δ within gate in Convex.

- **Morph IC→BWB (16)**  
  - When short leg hits 30–35Δ, morph reduces MPL and improves ROC; audit entry verified.

- **Ledger Assertions (16)**  
  - All new fields persisted; schema migration validated; dashboards read new columns.

> CSV template can be regenerated from the earlier tool; include these tests under suites: **Risk/MPL**, **RFib**, **ROC**, **Classifier**, **Morph**, **Ledger**.

---

## 5) Data & Schema Migration (P0)

- Add columns to **Trades** and **Ledger** tables:  
  `MaxPotentialLoss`, `Roc`, `RfibUtilization`, `MarginAtEntry`, `BodyDelta`, `GammaAtBody`, `CreditToWidth`, `ReasonCodes`, `Regime`.  
- Backfill batch: compute MPL for historical fills (IC assumptions) to unlock comparative dashboards.

---

## 6) Dashboards & Alerts (P1)

- New widgets: **MPL vs Realized Loss** (scatter), **ROC distribution**, **RfibUtilization heatmap**.  
- Alerts: block if *any* candidate would push `RfibUtilization > 1.0`; warn at ≥ 0.9.

---

## 7) CI/CD, Determinism, and Syntricks (P1)

- **CI gates:** Fail build if any **P0 test** fails or **Syntricks** emits NaN.  
- **Determinism:** Seed control for MC/pricers; parallel vs serial parity tests.  
- **Syntricks runs:** FlashCrash, Volmageddon, GammaSqueeze—assert MPL not exceeded by modeled payout; position trimmed per policy.

---

## 8) PR Plan (merge in small, safe steps)

- **PR‑1 (P0):** RiskModel + MPL wiring, schema migration, basic unit tests.  
- **PR‑2 (P0):** Strategy gates (IC/BWB), classifier suppression, ROC sizing, ledger fields & logging.  
- **PR‑3 (P1):** IC→BWB morph + tests, dashboards, Syntricks assertions.  
- **PR‑4 (P2):** Tail extender module (behind feature flag), additional scenarios, documentation.

---

## 9) Acceptance Criteria

- No candidate can be sent to the broker without a computed **MaxPotentialLoss** and **RfibUtilization ≤ 1.0**.  
- Backtests re-run show:  
  - **Volatile bucket P&L improves** (less negative vs IC‑only baseline).  
  - **Calm bucket ROC ≥ 1.5×** baseline IC.  
  - Worst‑day loss **≤ current worst (−$343)**.

---

## 10) Appendix — Quick Examples

**IC example:** wings 30/30, credit 8.00, multiplier 100 → MPL = max(30−8, 30−8)*100 = **$2,200**.  
**BWB example:** narrow 10, far 40, credit 3.00 → MPL = ((40−10)×100) − 300 = **$2,700**.

---

**Next Step:** Implement PR‑1 (RiskModel + MPL wiring) and run the augmented 620‑test suite.
