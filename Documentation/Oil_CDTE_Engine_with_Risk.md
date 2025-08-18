# Oil CDTE Weekly Engine — Unified Spec + Risk Management (Spread‑Width Rule)
**Version:** 1.0  
**Audience:** ODTE maintainers (Strategy · Execution · Historical · Backtest · UI)  
**Goal:** Ship a **mechanical, range‑bound, short‑volatility weekly engine** for **oil options** that runs **live‑like** on **real historical data (20+ yrs)**. All orders are priced/fill‑checked against the recorded **NBBO** at the decision timestamp — **no synthetic slippage or mid‑fill assumptions**.

---

## 0) Scope & Non‑Negotiables
- **Underlyings**
  - **CL** — NYMEX WTI crude oil **futures options** (weeklys supported; exercises into futures).
  - **USO** — United States Oil **ETF options** (equity microstructure; American‑style).
- **Weekly cadence**: Enter **Monday**, manage **Wednesday**, exit into **Thursday/Friday**; **flatten all** by Friday (per product calendar).
- **Structures**: **Iron Condor (IC)** default; **Iron Fly (IF)** in high IV; **debit/credit verticals** for defensive conversion. **Defined‑risk only** (no naked legs).
- **No look‑ahead**: At each decision time, only use chain/underlying data with timestamps **≤ decision timestamp**.
- **Real fills**: Marketable‑limit orders versus **historical NBBO**. Order is filled only if the historical book **touches/crosses** our limit within a bounded window; otherwise **NotFilled**.
- **Risk cap**: Per‑ticket **max loss ≤ $800**. Weekly allocation ≤ **6%** of equity.
- **Reproducibility**: Any randomness (e.g., aggressiveness step) must be **seeded and logged**.

---

## 1) Deterministic Wing Rule (Thumb Rule)
- **Wing width = DTE × $2** (per side).  
- **Special 0DTE**: **$0.50** wings per side.

**Examples**  
- Mon→Thu: 3 DTE ⇒ **±$6** wings.  
- Mon→Fri: 4 DTE ⇒ **±$8** wings.  
- Wed roll to Fri: 2 DTE ⇒ **±$4** wings.  
- Fri 0DTE lotto (optional): **±$0.50** wings.

This rule eliminates discretionary wing setting and keeps risk naturally scaled by time.

---

## 2) Data Interfaces (Real, Backtestable)
### 2.1 Tables
- **UnderlyingPrices**(ts_utc, symbol, last, bid, ask)
- **OptionChains**(ts_utc, underlying, expiry, right, strike, bid, ask, last, iv, delta, gamma, theta, vega, oi, volume)
- **SessionCalendar**(date, open_et, close_et, early_close_et, product)
- **EconEvents** *(optional)*: EIA inventories, OPEC, Fed, major geopolitics
- **SymbologyMap**: vendor→canonical (CL roots; USO OCC changes)

### 2.2 Providers
- `ChainSnapshotProvider`: first snapshot **≥ decision time** (ET).
- `SessionCalendarProvider`: trading hours, early closes, product cutoffs.
- `EconEventProvider`: event proximity flags.
- *(Optional)* `DividendEventProvider` for USO (generic ETF ex‑div logic).

---

## 3) Weekly Workflow
### 3.1 Monday Entry — **10:00 ET**
Create **two** positions using the wing rule:
1) **Core (Thu expiry)** — **Iron Condor** centered around spot.  
2) **Carry (Fri expiry)** — **Iron Condor**, or **Iron Fly** if IV elevated.

**Short strike placement (mechanical):**
- Compute **Expected Move (EM)** from **ATM IV** at 10:00 ET (no future knowledge).
- **Short call = Spot + 1×EM**, **short put = Spot − 1×EM**.
- **Wings** by **Width(DTE)** (nearest listed strikes).  
- **High IV weeks** (config threshold): prefer **IF** at ATM, wings = Width(DTE).

**Fill policy (historical NBBO):**
- Place **marketable‑limit** at touch ± aggressiveness step **[0.25, 0.40, 0.50]** of quote spread.
- Fill only if NBBO **touches/crosses** the limit within **FillWindow (30s)** and the **opposite side** does not move beyond **MaxAdverseTick (1 tick)**. Multi‑leg fills require synchronicity within the window. Otherwise **NotFilled**.

### 3.2 Wednesday Manage — **12:30 ET**
- **Take Profit**: if **Core (Thu)** captured **≥70%** of max profit ⇒ **close Core**, keep Carry.
- **Neutral** (|PnL| < **15%** ticket risk) ⇒ **Roll Core→Fri**:
  - Re‑center shorts at spot ± 1×EM (from 12:30 ET snapshot).
  - New wings = **Width(2 DTE) = ±$4**.
  - **Roll debit ≤ 25%** of original ticket risk; else **do not roll**.
- **Loss** (≥ **50%** ticket risk) ⇒ **close Core & Carry**; re‑enter **smaller** Fri IC using **±$4** wings.

### 3.3 Thursday/Friday Exit
- **Force‑exit** all positions **≥ 45 min** before product close (calendar‑driven).  
- Optional **0DTE** lotto on Friday: IC/IF with **±$0.50 wings**, size ≤ **0.5×** base.

---

## 4) Risk & Assignment Guardrails (Deterministic)
These rules keep you out of assignment/futures delivery and away from max‑loss tails.

### 4.1 Assignment/Avoid Delivery
- **CL (futures options)**: If `DTE ≤ 1` **and** any short leg is **ITM** or `|Δshort| ≥ 0.30` ⇒ **forbid hold** into final session; **roll out & away** or **close by T‑1**.
- **USO (ETF options)**: If **ex‑div within T‑1** and short calls **ITM** or **extrinsic ≤ $0.02** ⇒ **close/roll up & over** (prevent early assignment).
- **All instruments**: Every short must have a farther‑OTM long **until short is closed** (always defined risk).

### 4.2 Pin Risk (Expiry Day)
- If `|spot − shortStrike| ≤ pin_band_usd` (default **$0.10**) ⇒ **force flatten** that spread (no “hope” trades).

### 4.3 Gamma Brake
- If `|portfolioGammaUsdPer$| > gamma_max_usd_per_1` (default **$2,500** per $1 move) ⇒ **ReduceSize** / take profit / **convert to narrower debit verticals**.

### 4.4 Delta Guard
- If any **short leg** `|Δ| > 0.30`:
  - If **roll cost ≤ roll_budget_cap** (≤ **25%** ticket risk) ⇒ **RollOutAndAway** (same wing rule).
  - Else **ConvertToDebitVertical** or **Close**.

### 4.5 Roll Budget
- Thu→Fri **net additional debit ≤ 25%** of ticket risk, otherwise **no roll**.

### 4.6 Exit Window
- **Force exit** at `session_close − exit_buffer_min` (default **45 min**). Calendar‑driven (holidays/half‑days honored).

### 4.7 Event Controls
- If **EIA/OPEC/Fed** within **T‑2**:
  - Prefer **IF** (same wings) or **reduced size**.
  - Take‑profit **earlier** on Wed (config flag).

---

## 5) Config (YAML — drop‑in)
```yaml
oil_cdte:
  monday_decision_et: "10:00:00"
  wednesday_decision_et: "12:30:00"
  exit_cutoff_buffer_min: 45

  risk_cap_usd: 800
  weekly_cap_pct: 6
  take_profit_core_pct: 0.70
  max_drawdown_pct: 0.50
  neutral_band_pct: 0.15
  roll_debit_cap_pct_of_risk: 0.25
  iv_high_threshold_pct: 30

  width_rule:
    per_day_usd: 2.0
    zero_dte_usd: 0.5

  delta_targets:
    ic_short_abs: 0.18
    vert_short_abs: 0.25

  fill_policy:
    type: "marketable_limit"
    window_sec: 30
    max_adverse_tick: 1
    aggressiveness_steps: [0.25, 0.40, 0.50]

oil_risk_guardrails:
  pin_band_usd: 0.10
  delta_guard_abs: 0.30
  gamma_max_usd_per_1: 2500
  roll_debit_cap_pct_of_risk: 0.25
  exit_buffer_min: 45

  delta_itm_guard: 0.30
  extrinsic_min: 0.02

  event_guard:
    enable: true
    eia_opec_within_tminus_days: 2
    prefer_iron_fly: true
    early_take_profit: true
```

---

## 6) Public API & Strategy Hooks (C# Stubs)
### 6.1 Strategy Surface
```csharp
public sealed class OilCDTEStrategy : IStrategy
{
    public Task<PlannedOrders> EnterMondayAsync(ChainSnapshot oil10Et, OilCDTEConfig cfg);
    public Task<DecisionPlan> ManageWednesdayAsync(PortfolioState state, ChainSnapshot oil1230Et, OilCDTEConfig cfg);
    public Task<ExitReport>    ExitWeekAsync(PortfolioState state, ChainSnapshot exitWindow, OilCDTEConfig cfg);
}
```

### 6.2 Risk Modules
```csharp
public enum GuardAction { None, Close, RollOutAndAway, ConvertToDebitVertical, ReduceSize }
public record ActionPlan(GuardAction Action, string Reason, object? Payload = null);

public static class AssignmentRiskChecks
{
    public static ActionPlan PreTradeGate(PositionPlan plan, ProductCalendar cal, RiskConfig cfg);
    public static ActionPlan PreCloseGate(PortfolioState state, MarketSnapshot snap, ProductCalendar cal, RiskConfig cfg);
}

public static class PinRiskMonitor
{
    public static ActionPlan Check(PortfolioState state, double spot, RiskConfig cfg);
}

public static class GammaBrake
{
    public static ActionPlan Evaluate(GreeksAggregate g, RiskConfig cfg);
}

public static class DeltaGuard
{
    public static ActionPlan Evaluate(PortfolioState state, ChainSnapshot snap, RiskConfig cfg);
}

public static class RollBudgetEnforcer
{
    public static bool AllowRoll(double proposedDebit, double ticketRisk, RiskConfig cfg);
}

public static class ExitWindowEnforcer
{
    public static ActionPlan Check(ProductCalendar cal, DateTimeOffset nowEt, RiskConfig cfg);
}
```

### 6.3 Strategy Wiring (example)
```csharp
// Monday
var preTrade = AssignmentRiskChecks.PreTradeGate(plan, calendar, cfg.Risk);
if (preTrade.Action != GuardAction.None) return Apply(preTrade); // shrink/switch/abort deterministically

// Wednesday
var guards = new Func<ActionPlan>[] {
    () => GammaBrake.Evaluate(greeks, cfg.Risk),
    () => DeltaGuard.Evaluate(state, snapshot, cfg.Risk),
    () => ExitWindowEnforcer.Check(calendar, nowEt, cfg.Risk),
    () => PinRiskMonitor.Check(state, spot, cfg.Risk),
    () => AssignmentRiskChecks.PreCloseGate(state, market, calendar, cfg.Risk)
};
foreach (var g in guards)
{
    var plan = g();
    if (plan.Action != GuardAction.None) return Apply(plan);
}

// Then apply CDTE decision tree (TP / Roll / Close) subject to RollBudgetEnforcer
if (decision == Decision.Roll && !RollBudgetEnforcer.AllowRoll(proposedDebit, ticketRisk, cfg.Risk))
    decision = Decision.Close;
```

### 6.4 IC Builder (Wing Rule)
```csharp
public static class OilStrikes
{
    public static double WingWidth(int dte) => dte == 0 ? 0.5 : dte * 2.0;

    public static IronCondor BuildIC(double spot, int dte, double em, Func<double,double> nearest)
    {
        var w = WingWidth(dte);
        var sc = nearest(spot + em);
        var lc = nearest(sc + w);
        var sp = nearest(spot - em);
        var lp = nearest(sp - w);
        return new IronCondor(sc, lc, sp, lp);
    }
}
```

---

## 7) Execution (Historical NBBO — No Synthetic Slippage)
- **Marketable‑limit** at decision time.  
- **Fill** only if historical NBBO **touches/crosses** the limit within **30s**; multi‑leg synch required.  
- **Last trade** between our limit and opposite NBBO counts as **filled at our limit** (never better).  
- **Partials** allowed for USO; CL defaults atomic (unless policy allows legging with deterministic bounds).  
- Misses are first‑class results and logged.

---

## 8) Acceptance Tests
- **Deterministic strikes**: Given (spot, ATM IV, DTE) ⇒ unique IC/IF with Width(DTE).  
- **No synthetic fills**: Orders only fill when NBBO criteria met; otherwise NotFilled.  
- **Risk enforced**: Ticket risk ≤ $800 at entry; weekly cap obeyed.  
- **Assignment blocks**: CL DTE≤1 ITM ⇒ `PreCloseGate=Close`; USO ex‑div + ITM/low extrinsic ⇒ `PreCloseGate=Close`.  
- **Pin‑risk**: spot within pin band ⇒ `Check=Close`.  
- **Gamma/Delta**: breaches trigger appropriate `ActionPlan`.  
- **Roll budget**: proposedDebit > 0.25× risk ⇒ roll denied.  
- **Reproducible weekly P&L** on a frozen slice.

---

## 9) Minimal Runbook
1. Configure `oil_cdte` and `oil_risk_guardrails` blocks (YAML above).  
2. Wire risk modules into **Enter**, **Manage**, and **Exit** phases (see §6.3).  
3. Backtest:  
   ```bash
   dotnet run --project ODTE.Backtest --scenario CDTE.Oil --from 2005-01-03 --to 2025-07-31
   ```
4. Inspect `/audit/.../YYYY‑WW/`: `orders.json`, `fills.json`, `miss_log.json`, `pnl.csv`, `regime.json`, `config.yml`, SHA256 manifest.  
5. UI: open `/Start/cdte-oil-weekly` (heatmap + Wednesday decision card).

---

## 10) Repo Structure (Additions)
```
ODTE.Strategy/
  CDTE.Oil/
    OilCDTEStrategy.cs
    OilCDTEConfig.cs
    OilCDTERollRules.cs
    OilSignals.cs
    Risk/ (AssignmentRiskChecks.cs, PinRiskMonitor.cs, GammaBrake.cs, DeltaGuard.cs, RollBudgetEnforcer.cs, ExitWindowEnforcer.cs)
    README.md

ODTE.Execution/
  HistoricalFill/
    NbboFillEngine.cs
    FuturesOptionQuirks.cs
    EquityOptionQuirks.cs

ODTE.Historical/
  Providers/
    ChainSnapshotProvider.cs
    SessionCalendarProvider.cs
    EconEventProvider.cs
    DividendEventProvider.cs (optional)

ODTE.Backtest/
  Scenarios/CDTE.Oil/
    OilMondayToFriHarness.cs
    SparseDayRunner.cs
    Assertions.cs

ODTE.Start/Pages/
  cdte-oil-weekly.razor
  components/ (OilCDTEHeatmap.razor, OilWednesdayDecisionCard.razor)

audit/
  OIL_CDTE_AUDITRUN.md
  sample_week_exports/
```

---

### Notes
- When delta targets are unavailable at listed strikes, choose **nearest |Δtarget − Δ|**. If wings cannot meet the **$800 risk cap**, reduce size or **switch instrument** (USO instead of CL).  
- On **large moves (> 2× EM)** between Mon and Wed, prefer **conversion to debit verticals** on the threatened side rather than widening risk.  
- Maintain consistent **P&L marking** (default mid; conservative: bid for short‑credit exits) across backtests for fair comparisons.

**End of Unified Spec**
