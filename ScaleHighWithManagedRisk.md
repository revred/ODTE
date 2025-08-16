# ScaleHighWithManagedRisk — Dual Probe & Punch Capital Allocation (Actionable Spec)

**Goal:** Scale profits **aggressively when conditions are favorable** without scaling downside risk in lockstep.  
**Principle:** **Capital preservation first** (RFib caps unchanged). **Punch only when probes are green and a P&L cushion exists.**

---

## 1) Definitions & Guardrails

- **RFib Daily Cap (`D_cap`)**: Max allowed **worst-case** loss for the day (e.g., 500/300/200/100). **Unchanged.**
- **Remaining Budget (`B_rem`)**: `D_cap − Σ MaxLoss(open) − RealizedLossToday`, floored at 0.
- **Per-Trade Fraction (`f`)**: Fraction of `B_rem` allowed for a new position’s *worst-case* loss.
- **Probe Lane (P):** Low-risk, small width (≤ 1.0 pt), standard f (0.30–0.40), used to sample edge.
- **Punch Lane (Q):** Higher allocation **within the same RFib cap**; enabled **only** when probes are positive and a P&L cushion is present.
- **Correlation Budget (`ρ-budget`)**: Exposure clamp using correlation-adjusted sums; target ≤ 1.0.

> **Invariant:** For any set of open trades **Σ MaxLoss(open) ≤ D_cap** and weekly RFib cap still applies. No exceptions.

---

## 2) Positive-Probe Criteria (Greenlight Conditions)

Enable Punch lane **only if ALL** are true within the current session:
1. **Sample Size:** `N_probe_executed ≥ k` (default `k=3`).
2. **Realized Probe Win-Rate:** `WR_probe ≥ r_min` (default `r_min = 0.60`), *or* **GoScore** avg ≥ threshold (e.g., ≥ 65).
3. **P&L Cushion:** `RealizedDayPnL ≥ C_min`, where `C_min = 0.30 × D_cap` (dynamic with the chosen `D_cap`).
4. **Microstructure Healthy:** Median quoted spread ≤ threshold & liquidity score ≥ 0.72.
5. **Event Safety:** No imminent high-impact event inside blackout window.

If any fails → **Punch disabled** (Probe continues).

---

## 3) Punch Escalation Ladder (PnL-Locked)

Escalation is **P&L-locked** and **bounded by RFib**:

- **Level 0 (Baseline):** `f = 0.40` (or 0.30 in high stress), `MaxConcurrent = 1` (per underlying window). Runner **off**.
- **Level 1 (Greenlight-1):**
  - **Entry:** Positive-Probe met and `RealizedDayPnL ≥ 0.30 × D_cap`.
  - **Allocation:** `f_Q = 0.55` for Q-lane tickets (P-lane remains 0.40).
  - **Concurrency:** Allow +1 additional **independent** ticket (new underlying or time-slice).
  - **Runner:** TP1=60%, RunnerTarget=90%, RunnerSize=50% (QUALITY setups only).
- **Level 2 (Greenlight-2):**
  - **Entry:** `RealizedDayPnL ≥ 0.60 × D_cap` **and** Q-lane last 3 realized trades not net-loss.
  - **Allocation:** `f_Q = 0.65`.
  - **Concurrency:** +1 independent ticket (ρ-budget permitting).
  - **Credit Floor:** +10–20% over baseline (QUALITY only).

> **Absolute Constraint:** For each new Q ticket, `MaxLossAtEntry ≤ min(f_Q × B_rem, 0.5 × RealizedDayPnL)` and **Σ MaxLoss(open) ≤ D_cap**.

**Auto De-escalation / Cooldown:**  
- If `RealizedDayPnL` drops below **0.5 × last escalator trigger** → drop down one level.  
- If **two consecutive Q-lane losers** or **ρ-weighted exposure > 1.0** → return to Level 0 for 60 minutes or until a new Positive-Probe is achieved.

---

## 4) Sizing & Concurrency Rules

### 4.1 Per-Trade Sizing (applies to both lanes)
```
maxLoss_per_contract = (width − expectedCredit) × multiplier(=100)
perTradeCap = f_lane × B_rem
contracts_by_risk = floor(perTradeCap / maxLoss_per_contract)
contracts = min(contracts_by_risk, HardCap)
if contracts < 1 and ProbeEnabled and maxLoss_per_contract ≤ B_rem ⇒ contracts = 1  (Probe 1-lot rule)
```

### 4.2 Concurrency & ρ-Budget
Maintain a rolling correlation matrix (e.g., vs SPY and pairwise). Define **ρ-weighted exposure**:
```
ρ_weighted_exposure = Σ_i ( MaxLoss_i / D_cap ) × w_i
w_i = max( |β_SPY_i|, max_j |ρ_ij| )
Constraint: ρ_weighted_exposure ≤ 1.0
```
- New Q-lane entries **blocked** if the constraint would be exceeded.
- Independent bets: prefer different underlyings and time windows (post-OR vs pre-close).

---

## 5) Entry Economics (Quality Focus)

- **Adaptive Credit Floor (QUALITY):** `MinCredit ≥ max(0.20 × width, 0.04 × IVRank × width)`.
- **Microstructure Gate:** `liquidityScore ≥ 0.72`, quoted spread ≤ 80th percentile of last 30 mins, limit orders at mid ± ticks.
- **Windows:** Prefer post-OR consolidation and late-session decay windows; allow off-window only if liquidity at high watermark.

---

## 6) Exit Management

- **Probe Lane:** TP at 40–50% of max; no runner; strict time-stop if <10% by T−90 mins.
- **Punch Lane (QUALITY only):** TP1 at 60%; leave 50% runner to 85–90% or time-stop. Tighten stop multiple for narrow/low-IV (1.7× credit).

---

## 7) Feature Flags & Config (Suggested)

```json
{
  "Risk": {
    "RFibDailyCaps": [500, 300, 200, 100],
    "WeeklyKillMultiple": 3.0,
    "PerTradeFraction": { "Probe": 0.40, "PunchL1": 0.55, "PunchL2": 0.65 },
    "ProbeOneLotIfFitsAbsolute": true,
    "CorrelationBudget": { "Enable": true, "MaxRhoWeightedExposure": 1.0 }
  },
  "Escalation": {
    "Enable": true,
    "ProbeMinCount": 3,
    "ProbeMinWinRate": 0.60,
    "GoScoreThreshold": 65,
    "CushionL1": 0.30,  // × D_cap
    "CushionL2": 0.60,  // × D_cap
    "AddedRiskMaxFracRemaining": 0.40,
    "AddedRiskMaxFracRealized": 0.50,
    "AutoDeescalateOnCushionLoss": true,
    "CooldownMinutes": 60
  },
  "Entries": {
    "MinLiquidityScore": 0.72,
    "AdaptiveCreditFloor": { "Enable": true, "AlphaWidth": 0.20, "BetaIV": 0.04 },
    "QualityWindows": ["postOR: +15m..+90m", "preClose: -120m..-20m"]
  },
  "Exits": {
    "Probe": { "TakeProfit": 0.45, "TimeStopCutoffMinutesToClose": 90 },
    "Punch": { "TP1": 0.60, "RunnerTarget": 0.90, "RunnerFraction": 0.50, "StopMultipleNarrowLowIV": 1.7 }
  },
  "Sizing": {
    "HardContractCap": 5,
    "ScaleToFitOnce": true,
    "MinWidthPoints": 1.00
  }
}
```

---

## 8) Pseudocode Hooks (C#-style)

```csharp
bool PositiveProbe(SessionStats s) {
    return s.Probe.Count >= cfg.Escalation.ProbeMinCount
        && (s.Probe.WinRate >= cfg.Escalation.ProbeMinWinRate || s.Probe.AvgGoScore >= cfg.Escalation.GoScoreThreshold)
        && s.RealizedDayPnL >= cfg.Escalation.CushionL1 * s.DailyCap
        && s.Microstructure.Healthy
        && !s.Events.InBlackout;
}

EscalationLevel ComputeLevel(SessionStats s) {
    if (!cfg.Escalation.Enable || !PositiveProbe(s)) return L0;
    if (s.RealizedDayPnL >= cfg.Escalation.CushionL2 * s.DailyCap && s.Punch.Last3PnL >= 0) return L2;
    return L1;
}

bool CanAddPunchTrade(TradeIdea idea, SessionStats s, EscalationLevel lvl) {
    decimal B_rem = RemainingBudget(s);
    decimal f = lvl == L2 ? cfg.Risk.PerTradeFraction.PunchL2 : cfg.Risk.PerTradeFraction.PunchL1;
    decimal perTradeCap = Math.Min(f * B_rem, cfg.Escalation.AddedRiskMaxFracRealized * s.RealizedDayPnL);

    decimal maxLossPerContract = (idea.Width - idea.ExpectedCredit) * 100m;
    int contracts = DeriveContracts(perTradeCap, maxLossPerContract, cfg.Sizing.HardContractCap);

    return contracts >= 1
        && RhoWeightedExposureAfter(idea, contracts, s) <= cfg.Risk.CorrelationBudget.MaxRhoWeightedExposure;
}
```

---

## 9) Validation Protocol (Global, Not Overfit)

1. **Walk-Forward (3y→qtr) with Feature Flags:** Toggle Punch escalator on/off; compare **CAGR, Sortino, PF**, **Max DD**, **ES95**.
2. **Regime Bins:** VIX (<20, 20–25, 25–30, 30–40, ≥40), trend (up/down), event vs non-event.
   - Expect **higher net P&L** in <20 & 20–25 bins, **no worse tails** elsewhere.
3. **Intraday Metrics:** Concurrency, ρ-exposure, slippage bps median/p90; ensure **costs not worse**.
4. **Stress Replays (Syntricks):** Gaps, IV spikes, liquidity droughts. Assert **no RFib breach**; escalator self-throttles.

**Acceptance Gates:**
- Day/week **RFib never breached**.
- **Avg $/trade** ↑ ≥ 15% in QUALITY bins; **win-rate** stable.
- **Max DD** ≤ baseline; **ES95** ≤ baseline.
- **Sharpe/Sortino** ≥ baseline across out-of-sample quarters.

---

## 10) Rollout Order & Rollback

**Order:** (1) Positive-Probe gate → (2) Punch L1 (f=0.55) → (3) Runner exits (QUALITY) → (4) Punch L2 (f=0.65) → (5) Correlation budget clamp.  
**Rollback:** Disable `Escalation.Enable` (reverts to Probe-only). Feature flags allow partial rollback (e.g., disable L2 only).

---

## 11) Observability & Reason Codes

Log per decision: `lane, level, D_cap, B_rem, f, perTradeCap, RealizedDayPnL, rhoExposureBefore/After, expectedCredit, width, maxLossPerContract, contracts, reasonCode`.

Example reason codes: `POS_PROBE_FAIL`, `NO_CUSHION`, `RFIB_CAP_HIT`, `RHO_BUDGET_EXCEEDED`, `MICROSTRUCTURE_FAIL`, `EVENT_BLACKOUT`, `OK_PUNCH_L1`, `OK_PUNCH_L2`.

---

**Summary:** This spec keeps **absolute losses bounded** by RFib while letting you **add more independent, higher-quality tickets** when **probes prove the edge** and **realized P&L provides a cushion**. The escalator is **self-limiting**, **correlation-aware**, and fully **feature-flagged** for safe, incremental rollout.
