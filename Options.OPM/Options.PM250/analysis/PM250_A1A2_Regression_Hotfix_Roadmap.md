# PM250 — A1/A2 Regression Hotfix Roadmap (Global, Non‑Overfit, Actionable Specs)

**Repo:** `revred/ODTE`  
**Context:** After enabling
- **A1**: Per‑trade max‑loss tied to RFib daily budget, and
- **A2**: Integer sizing + hard contract cap,

the engine produced **zero trades** in multiple months. This spec unfreezes utilization **without** violating RFib guardrails and keeps all changes **feature‑flagged** so you can A/B them safely across all regimes.

---

## Objectives

1. **Capital preservation first:** Never exceed RFib daily/weekly loss caps.  
2. **Restore utilization:** Prevent “no-trade” months caused by over-tight caps.  
3. **Lift average profit/trade** without increasing tail risk or slippage.  
4. **Keep it testable and reversible:** Each change is a small switch with clear logs.

---

## Phase 0 — Triage & Correctness Checklist (Do this once)

Before applying hotfixes, verify the implementation of A1/A2:

- **T0.1 Per-trade cap formula**
  - **Expected:** `perTradeCap = f × remainingDailyRFibBudget` where `remainingDailyRFibBudget = dailyRFibCap − openWorstCaseLoss − realizedLossToday`.
  - **Anti-patterns:** Using weekly cap; forgetting realized losses; allowing negative remaining.

- **T0.2 Units for max loss per contract**
  - **Expected:** `maxLossPerContract = (width − expectedCredit) × contractMultiplier` (e.g., `×100` for index options).
  - **Anti-patterns:** Using notional/margin; wrong multiplier.

- **T0.3 Order of operations for sizing**
  - **Expected:** `maxContractsByRisk = floor(perTradeCap / maxLossPerContract)` → `contracts = min(maxContractsByRisk, hardCap)` → **floor to integer**.
  - **Anti-patterns:** Rounding too early; reversing min/floor; deriving 0 contracts from premature rounding.

- **T0.4 Fallback policy**
  - **Expected:** If `contracts == 0`, **attempt narrower width once** (e.g., 1‑pt) or micro underlier **before reject**.
  - **Anti-patterns:** Immediate reject → zero‑trade months.

- **T0.5 Logging**
  - **Expected:** Always log `remainingBudget, perTradeCap, width, expectedCredit, maxLossPerContract, derivedContracts, reasonCode` on reject.

---

## Phase 1 — Safety‑Preserving Unfreeze (Same‑Day Hotfixes)

> All hotfixes below **respect absolute RFib caps**. They only reshape gating at tight budgets so at least **one safe unit** can trade.

### H1. Probe 1‑Lot Rule
**What:** If there are **no open positions** and a candidate passes non‑risk filters, allow **1 contract** when  
`maxLossPerContract ≤ remainingDailyRFibBudget` even if it exceeds the `f × remaining` fraction.

**Why:** Prevents total inactivity while keeping loss ≤ daily cap.

**Feature Flag:** `Risk.ProbeTradeEnabled` (bool), `Risk.ProbeTradeOnlyWhenNoOpen` (bool).

**Example Config Snippet**
```json
{
  "Risk": {
    "RFibDailyCaps": [500, 300, 200, 100],
    "PerTradeFraction": { "default": 0.40, "lowCapThreshold": 150, "lowCapFraction": 0.80 },
    "ProbeTradeEnabled": true,
    "ProbeTradeOnlyWhenNoOpen": true
  }
}
```

**Example C# (pseudocode, drop‑in for pre‑order checks)**
```csharp
decimal dailyCap = rfib.CurrentDailyLimit;
decimal remaining = Math.Max(0m, dailyCap - portfolio.OpenPositions.Sum(p => p.MaxLossAtEntry) - pnl.RealizedLossToday);
decimal f = dailyCap <= 150m ? 0.80m : 0.40m;
decimal perTradeCap = f * remaining;

decimal maxLossPerContract = (candidate.Width - candidate.ExpectedCredit) * 100m;
int maxContractsByRisk = (int)Math.Floor(perTradeCap / maxLossPerContract);
int contracts = Math.Min(maxContractsByRisk, config.HardContractCap);

if (contracts < 1 && config.Risk.ProbeTradeEnabled) {
    bool noOpen = portfolio.OpenPositions.Count == 0;
    if ((!config.Risk.ProbeTradeOnlyWhenNoOpen || noOpen) && maxLossPerContract <= remaining)
        contracts = 1;
}
if (contracts < 1) {
    logger.Info("Reject: PerTradeCap",
        new { remaining, perTradeCap, candidate.Width, candidate.ExpectedCredit, maxLossPerContract, maxContractsByRisk });
    return Decision.Reject("PerTradeCap");
}
```

---

### H2. Dynamic Fraction **f** at Tight RFib Caps
**What:** Use `f = 0.80` when `dailyRFibCap ≤ 150`, else `0.40`.

**Why:** A 1‑pt defined‑risk vertical often needs ~$70–$90 risk; `0.4 × 100 = 40` blocks even a single‑lot.

**Feature Flag:** `Risk.EnableLowCapBoost` (bool).

**C# Hook (integrates with H1)**
```csharp
decimal f = config.Risk.EnableLowCapBoost && dailyCap <= config.Risk.PerTradeFraction.lowCapThreshold
    ? config.Risk.PerTradeFraction.lowCapFraction
    : config.Risk.PerTradeFraction.@default;
```

---

### H3. Scale‑to‑Fit (Narrow‑Once Fallback)
**What:** If `contracts == 0`, retry **once** with the **narrowest allowed width** (e.g., 1‑pt) or a micro underlier; recompute `maxLossPerContract` and `contracts`.

**Why:** Keeps a path for safe trades under tight caps; still bounded by absolute daily cap.

**Feature Flag:** `Sizing.ScaleToFitOnce` (bool), `Sizing.MinWidthPoints` (decimal).

**C# Sketch**
```csharp
if (contracts < 1 && config.Sizing.ScaleToFitOnce && candidate.Width > config.Sizing.MinWidthPoints) {
    candidate.Width = config.Sizing.MinWidthPoints;
    candidate.ExpectedCredit = pricing.EstimateCredit(candidate); // adjust for new width
    maxLossPerContract = (candidate.Width - candidate.ExpectedCredit) * 100m;
    maxContractsByRisk = (int)Math.Floor(perTradeCap / maxLossPerContract);
    contracts = Math.Min(maxContractsByRisk, config.HardContractCap);

    // Allow probe 1-lot if still zero and it fits absolute remaining
    if (contracts < 1 && config.Risk.ProbeTradeEnabled && maxLossPerContract <= remaining) contracts = 1;
}
```

---

### H4. Audit Logging (Mandatory)
**What to log on every reject/accept:**  
`timestamp, symbol, side, width, expectedCredit, maxLossPerContract, dailyCap, remainingBudget, perTradeFraction, perTradeCap, derivedContracts, hardCap, reasonCode`

**Log Format Example (JSON)**
```json
{
  "t": "2025-08-16T14:01:23Z",
  "sym": "XSP",
  "side": "short_put_spread",
  "width": 1.00,
  "expectedCredit": 0.22,
  "maxLossPerContract": 78,
  "dailyCap": 100,
  "remainingBudget": 100,
  "perTradeFraction": 0.80,
  "perTradeCap": 80,
  "derivedContracts": 1,
  "hardCap": 5,
  "decision": "ACCEPT"
}
```

---

## Phase 2 — Correctness Hardening

### C1. Units Sanity Tests
- **Test:** Given (width=1.00, credit=0.22) → `maxLossPerContract=78` (×100).  
- **Edge:** credit ≥ width must be rejected.  
- **Automate:** Unit tests in risk module; assert ranges by product class.

### C2. Remaining Budget Calculation
- **Invariant:** `remaining = dailyCap − openWorstCase − realizedLossToday; remaining ≥ 0`.  
- **Test:** Intraday sequence with open/close trades; ensure monotonic non‑increase; floor at zero.

### C3. Sizing Order of Operations
- **Test:** Property‑based tests across random caps/widths/credits to ensure `contracts ≥ 0`, integer, and `contracts ≤ hardCap`.

---

## Phase 3 — Controlled Utilization Lift (Optional, Safe)

### U1. Adaptive Credit Floor
**Rule:** `MinCredit = max(α·width, β·IVRank·width)` (start α=0.18, β=0.04).  
**Why:** Improves average profit/trade across IV regimes without extra tail risk.  
**Flags:** `Entries.EnableAdaptiveCreditFloor` (bool), `Entries.AlphaWidth`, `Entries.BetaIV`.

**Config Example**
```json
{
  "Entries": {
    "EnableAdaptiveCreditFloor": true,
    "AlphaWidth": 0.18,
    "BetaIV": 0.04
  }
}
```

### U2. Microstructure‑Aware Entry Windows
**Rule:** Permit entries only in windows with known tighter spreads (post‑OR, pre‑close), unless `liquidityScore ≥ highWatermark`.  
**KPI:** Slippage (bps) median & p90 ↓; cancel‑to‑fill ratio ↓; win‑rate flat.

---

## Feature Flags (Summary)

| Flag | Default | Scope | Notes |
|---|---:|---|---|
| `Risk.ProbeTradeEnabled` | `true` | Phase 1 | Allow 1‑lot if absolute remaining cap fits |
| `Risk.ProbeTradeOnlyWhenNoOpen` | `true` | Phase 1 | Probe only when flat |
| `Risk.EnableLowCapBoost` | `true` | Phase 1 | Use higher `f` at small daily caps |
| `Sizing.ScaleToFitOnce` | `true` | Phase 1 | Narrow width once to fit cap |
| `Sizing.MinWidthPoints` | `1.00` | Phase 1 | Narrowest allowed spread |
| `Entries.EnableAdaptiveCreditFloor` | `false` | Phase 3 | Turn on once Phase 1 is stable |

---

## Acceptance Criteria (Global, Regime‑Stratified)

- **Risk:** Zero RFib breaches. Max daily/weekly loss ≤ configured caps. **ES95 ≤ baseline**.  
- **Utilization:** No month is “0 trades” unless stress/liquidity alone blocks it. Executed opportunity rate **≥ 70%** of baseline.  
- **Return:** **Avg profit/trade ≥ +20%** vs. pre‑A1/A2 baseline with similar win‑rate.  
- **Costs:** Slippage median and p90 **not worse** than baseline.

---

## Test Protocol (Anti‑Overfit)

1. **Walk‑forward:** Optimize thresholds on trailing 3y; test next quarter; roll.  
2. **Regime bins:** IV quartiles, trend up/down, event vs non‑event. Report PF, Sharpe, ES95 per bin.  
3. **Stress (Syntricks):** Gap moves, IV spikes, liquidity droughts. Assert: no guardrail breach; graceful degradation.

---

## Rollout & Rollback

- **Rollout order:** H1 → H2 → H3 → H4 → C1/C2/C3 → U1 → U2.  
- **Rollback:** Disable `ProbeTradeEnabled`, set `EnableLowCapBoost=false`, `ScaleToFitOnce=false`; system reverts to A1/A2 behavior.  
- **Observability:** Keep shadow metrics & reason‑codes to isolate contribution per flag.

---

## Appendix — Reference Helpers

**Helper: Compute Remaining Budget**
```csharp
decimal RemainingDailyBudget(decimal dailyCap, IEnumerable<Position> openPositions, decimal realizedLossToday) {
    decimal openWorst = openPositions.Sum(p => p.MaxLossAtEntry);
    return Math.Max(0m, dailyCap - openWorst - realizedLossToday);
}
```

**Helper: Max Loss Per Contract**
```csharp
decimal MaxLossPerContract(OrderIdea idea) {
    return (idea.Width - idea.ExpectedCredit) * 100m; // ensure multiplier is correct for the product
}
```

**Helper: Derive Contracts**
```csharp
int DeriveContracts(decimal perTradeCap, decimal maxLossPerContract, int hardCap) {
    if (maxLossPerContract <= 0m) return 0;
    int byRisk = (int)Math.Floor(perTradeCap / maxLossPerContract);
    return Math.Max(0, Math.Min(byRisk, hardCap));
}
```

**Example: Reason‑coded Reject**
```csharp
return Decision.Reject("PerTradeCap")
    .WithMeta(new {
        remaining, perTradeCap, candidate = new { candidate.Width, candidate.ExpectedCredit },
        maxLossPerContract, maxContractsByRisk, hardCap = config.HardContractCap
    });
```
