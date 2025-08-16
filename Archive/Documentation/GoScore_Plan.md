
# ODTE — GoScore Implementation Plan (Core Trade Selector)
**Date:** 2025-08-15T05:25:32.195486Z

**Goal:** Make **GoScore** a first‑class, auditable variable that decides *go/half/skip* for each candidate **Iron Condor (IC)** or **Credit Broken‑Wing Butterfly (BWB)**, reducing loss frequency while preserving RFib loss caps and improving ROC.

---

## 1) Definition (single number, 0–100)
**GoScore** ≔ logistic‑calibrated score approximating the **probability of a profitable exit** (per our policy) **adjusted** for touch risk, liquidity, regime fit, pin alignment, pricing edge, and RFib utilization.

**Inputs**
- `PoE` – Probability that expiry lands in profit region (tent for BWB; inside shorts for IC).
- `PoT` – Max probability‑of‑touch across short legs (proxy for early adjustment risk).
- `Edge` – (NetCredit − ModelFairValue) / MaxPotentialLoss.
- `LiqScore` – 0..1 score from NBBO spread, depth, OI, and quote health.
- `RegScore` – 0..1 fit of the strategy to current regime (IVR, VIX, term slope, trend, RV/IV).
- `PinScore` – 0..1 proximity of body/shorts to gamma wall / max‑pain magnet.
- `RfibUtil` – (OpenRisk + MaxPotentialLoss)/DailyCap.

**Score (initial weights; later calibrated on 5y data)**

```
z = 1.6*PoE  − 1.0*PoT + 0.9*Edge + 0.6*LiqScore + 0.8*RegScore + 0.3*PinScore − 1.2*max(0, RfibUtil − 0.8)
GoScore = 100 * σ(z)           // σ is logistic
```

**Policy gates**
- Block if `RfibUtil ≥ 1.0`.
- Block IC when regime=Convex.
- Enter **full** if `GoScore ≥ 70`; **half** if `55 ≤ GoScore < 70`; **skip** otherwise.

---

## 2) Architecture & Interfaces (C#)

```csharp
public sealed record GoInputs(
    double PoE, double PoT, double Edge, double LiqScore,
    double RegScore, double PinScore, double RfibUtil);

public interface IGoScorer { double Compute(GoInputs x); }

public sealed class GoScorer : IGoScorer
{
    const double wPoE=1.6, wPoT=-1.0, wEdge=0.9, wLiq=0.6, wReg=0.8, wPin=0.3, wRfib=-1.2;
    static double Sigmoid(double z) => 1.0 / (1.0 + Math.Exp(-z));
    public double Compute(GoInputs x)
    {
        var rfibPenalty = Math.Max(0.0, x.RfibUtil - 0.8);
        var z = wPoE*x.PoE + wPoT*x.PoT + wEdge*x.Edge + wLiq*x.LiqScore + wReg*x.RegScore + wPin*x.PinScore + wRfib*rfibPenalty;
        return 100.0 * Sigmoid(z);
    }
}
```

Wire this into the **selector** directly before placing orders; persist all sub‑components in the ledger.

---

## 3) Computing each input (fast, deterministic)

### 3.1 PoE — Profit‑region probability
**IC:** risk‑neutral `P(K_put_short ≤ S_T ≤ K_call_short)` using boundary implied vols:
```
σ̂ = 0.5 * (IV(K_put_short) + IV(K_call_short))
z(K) = [ln(K/S) − (r−q−0.5σ̂²)T] / (σ̂√T)
PoE = Φ(z(K_call_short)) − Φ(z(K_put_short))
```

**BWB:** integrate RN density over *tent* around body. Efficient proxy:
```
PoInsideShorts = Φ(z(K_call_short)) − Φ(z(K_put_short))
PoNearBody     = Φ(z(K_body+Δn)) − Φ(z(K_body−Δn))    // Δn = narrow wing
PoE ≈ 0.5*PoInsideShorts + 0.5*PoNearBody
```
(Upgrade path: piecewise integration using the smile slice (SVI) when available.)

### 3.2 PoT — Probability of touch (path risk)
ODTE heuristic: `PoT_side ≈ clamp(2 * |Δ_short_side|, 0, 1)`; take the **max** across sides.

### 3.3 Edge — Pricing edge
`Edge = (NetCredit − ModelFairValue) / MaxPotentialLoss`; price legs with the same IV slice used in PoE.

### 3.4 LiqScore — Liquidity quality (0..1)
```
Spread = (Ask − Bid); Mid = 0.5*(Ask+Bid)
S = 1 − min(1, Spread/Mid)
DepthAdj: boost S if depth/OI ≥ thresholds; zero if quotes locked/crossed/stale.
LiqScore = clamp(S * DepthAdj, 0, 1)
```

### 3.5 RegScore — Regime agreement (0..1)
Rule map:
- Calm: IC=0.8, BWB=1.0
- Mixed: IC=0.6, BWB=0.8
- Convex: IC=0.0, BWB=0.6 (only if Δ gate & ROC pass)

Inputs: IVR bucket, VIX level, `IV(0DTE)/IV(30D)`, trend score, realized/ implied.

### 3.6 PinScore — Magnet alignment
1. Detect nearest gamma wall / max‑pain.  
2. Map normalized distance `d` (points) to `[0,1]` via `exp(−d/α)` with α≈narrow wing.

### 3.7 RfibUtil — Capital pressure
`RfibUtil = (OpenRisk + MaxPotentialLoss)/DailyCap`. (Uses your MPL functions already specified.)

---

## 4) Integration points

1. **Selector pipeline**
```
Build candidate shape -> Price & Greeks -> MPL -> GoInputs -> GoScore
-> Gate: RFib & regime -> Size: full/half/skip -> Route IOC/Smart
```

2. **Ledger/schema additions**
Add columns to Fills & Decisions:
`GoScore, PoE, PoT, Edge, LiqScore, RegScore, PinScore, RfibUtil, RegimeEvidenceJson`

3. **Config**
Weights, thresholds (70/55), α for PinScore, PoT scaling, IV sources — all in policy JSON.

4. **UI**
Add a gauge + breakdown bars; tooltip shows components and reasons (auditable decisions).

---

## 5) Calibration (turn score into real win‑probability)

**Dataset:** 5‑year backtest + live paper fills. Label `y=1` if trade met exit policy with positive P&L (after fees).  
**Model:** logistic regression (or isotonic on top of linear z) using components as features.  
**Schedule:** re‑fit weekly; keep last 8 models for rollback; store calibration in `GoScoreCalib.json`.

**Acceptance:** AUC ≥ 0.70 on out‑of‑sample; Brier score improves vs naive POP; monotonic calibration (reliability curve).

---

## 6) Tests (add 80 unit/integration tests)

- **Math sanity (PoE/PoT/Edge)**: boundary cases, ATM/OTM extremes, σ→0, T→0.  
- **Liquidity scoring**: tight vs wide spreads; crossed markets → LiqScore=0.  
- **Regime gating**: IC blocked in Convex; BWB allowed only with ROC≥min & Δ gate.  
- **RFib enforcement**: RfibUtil ≥1.0 → skip regardless of score.  
- **Decision tiers**: 3 fixtures causing GoScore around 50/60/80 → hit skip/half/full.  
- **Serialization**: ledger writes all fields; round‑trip.  
- **Determinism**: same snapshot → identical GoScore; parallel vs serial equality.  
- **Syntricks**: stress scenarios yield **lower** GoScore unless convex module active.

---

## 7) CI/CD & rollout

**PR‑1 (Core):** GoScorer class, GoInputs calculators, ledger fields, baseline weights, 40 unit tests.  
**PR‑2 (Selector):** Integrate GoScore gates; UI widget; 20 integration tests.  
**PR‑3 (Calibration):** Offline trainer + loader; shadow‑mode calibration for 2 weeks; 20 tests.  
**PR‑4 (Hygiene):** Docs, dashboards (reliability curve, AUC, Brier), alerts.

**Kill‑switch:** feature flag `UseGoScore`; can fall back to previous policy instantly.

---

## 8) Decision policy (final wiring)

```
if (RfibUtil >= 1.0)        -> SKIP (reason: RFib)
if (Regime == Convex && IC) -> SKIP (reason: Regime)
score = GoScore(inputs)
if (score >= 70)            -> FULL size
else if (score >= 55)       -> HALF size
else                        -> SKIP
```

**Sizing uses** RFib cap and MPL as before; GoScore only gates participation.

---

## 9) Telemetry & KPIs

- **Reliability** (calibration plot): predicted vs realized win rate by decile.  
- **Uplift**: loss‑trade rate before/after GoScore gate (target: −30% or better).  
- **ROC**: average per‑trade ROC improvement (target: +20% in Calm).  
- **Bucket P&L**: volatile/trend buckets turn less negative or positive.  
- **Policy drift watchdog**: alert if average GoScore deviates >2σ from trailing 30‑day mean.

---

## 10) Edge cases & fallbacks

- Missing IV or stale quotes → LiqScore=0, PoE via fallback σ (deny unless manual override).  
- Discrete divs or AM/PM settlement → ensure TTM & forward pricing consistent.  
- Extremely wide markets → auto‑skip (min LiqScore policy).

---

## 11) Snippets for PoE/PoT (self‑contained)

```csharp
static double Phi(double x) => 0.5 * (1.0 + Math.Erf(x / Math.Sqrt(2.0)));
static double Z(double K, double S, double r, double q, double sigma, double T)
    => (Math.Log(K/S) - (r - q - 0.5*sigma*sigma)*T) / (sigma*Math.Sqrt(T));

double PoE_IC(double S, double r, double q, double T, double ivPut, double Kp, double ivCall, double Kc)
{
    var sigma = 0.5*(ivPut + ivCall);
    return Math.Max(0, Phi(Z(Kc,S,r,q,sigma,T)) - Phi(Z(Kp,S,r,q,sigma,T)));
}

double PoT_FromDelta(double deltaAbs) => Math.Min(1.0, 2.0 * deltaAbs); // clamp
```

---

## 12) Acceptance for rollout

- Backtest with GoScore gate shows **reduced loss % by ≥30%** and **system profit ≥ 1.5×** in Calm, **improved** in Volatile.  
- No increase in worst‑day loss; RFib breaches remain **zero**.  
- Shadow‑mode live trial for 10 trading days before full enable.

---

**Next step:** Merge **PR‑1** (core scorer + calculators + ledger fields) and run the augmented tests; start shadow scoring in backtests to calibrate weights.
