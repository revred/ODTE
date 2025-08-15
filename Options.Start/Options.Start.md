# ODTE.Blazor UX Structure & Model Performance Pages
*Exported: 2025-08-15 (Europe/London)*

This document defines the **Blazor Web App / PWA** structure for ODTE, including routes, components, and detailed UX for **Active/Verified Model** pages. Each model page renders **20 years** of trading performance with a **GitHub-style contribution calendar** for each year. Cells encode profitability, holidays/weekends, and no-trade days; executed-trade days show an overlay marker.

---

## 1) App Architecture (Blazor Web App / PWA)

**Project:** `Options.Start`  
**Layouts:** `MainLayout` (KPI bar + left rail), `PageLayout` (page toolbar)  
**Routing root:** `/` → Dashboard; features under `/trade/*`, `/risk/*`, `/reports/*`, `/data/*`, `/models/*`  
**State:** Flux/Fluxor-style store + SignalR hubs for quotes/quality/risk  
**Core services:** `IMarketData`, `IOptionsGreeks`, `IRiskGate`, `IBacktest`, `IBroker`, `IQualityReport`, `IEventBus`, `IModelCatalog`, `IModelPerf`

```
/Options.Start
  /Pages               // Razor pages (.razor)
  /Components          // Reusable UI components
  /Services            // Interfaces + DI registrations
  /State               // Store, actions, reducers
  /Assets              // Icons, css, etc.
```

---

## 2) Global Layout & Navigation

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ Top KPI: [Cash] [Fib Budget] [Open Risk] [PnL] [Data Quality] [ML Gate]     │
├───────────────┬──────────────────────────────────────────────────────────────┤
│ Left Rail     │ Page Content (Page toolbar at top)                           │
│ [Dashboard]   │                                                              │
│ [Weekly]      │                                                              │
│ [Prospects]   │                                                              │
│ [Status]      │                                                              │
│ [Now]         │                                                              │
│ [Loss Leads]  │                                                              │
│ [Profit Leads]│                                                              │
│ [Summary]     │                                                              │
│ ─────────────  │                                                              │
│ [Data Quality] │                                                              │
│ [Risk Console] │                                                              │
│ [Broker]       │                                                              │
│ [Models]       │  ← NEW: Active/Verified models directory                     │
│ [Settings]     │                                                              │
└───────────────┴──────────────────────────────────────────────────────────────┘
```

**Top KPI bar components:** `CashChip`, `FibonacciGauge`, `OpenRiskMeter`, `PnLMini`, `DataQualityBadge`, `MLGateBadge`, `ConnectionStatus`.  
**Left rail:** collapsible; `1..9` hotkeys jump to main pages.

---

## 3) Pages (Summary)

| # | Page | Route | Objective | Key Components |
|---|------|-------|-----------|----------------|
| 0 | Dashboard | `/` | Readiness & constraints | `RegimeCard`, `BudgetCard`, `QualityCard`, `IvSurfaceHeatmap`, `EventTicker` |
| 1 | Weekly | `/weekly` | Sparse scheduling & coverage | `WeekCalendar`, `SparseSelector`, `RunQueue`, `DayCard` |
| 2 | Prospects | `/trade/prospects` | Scan & stage entries; **risk-gate** | `ChainTable`, `StrategyBuilder`, `RiskPreview`, `PreTradeGate` |
| 3 | Status | `/status` | Positions, Greeks, alerts | `PositionsGrid`, `RiskTotals`, `AlertCenter` |
| 4 | Now | `/now` | Immediate actions & what-if | `DecisionCard`, `WhatIf`, `ExecutePanel`, `GateBanner` |
| 5 | Loss Leads | `/loss-leads` | Clusters, **Syntricks replay**, rules | `ClusterList`, `ClusterDetail`, `SyntricksReplay`, `RuleProposal` |
| 6 | Profit Leads | `/profit-leads` | Cohorts & positive filters | `CohortGrid`, `AttributionChart`, `PromotePattern` |
| 7 | Summary | `/summary` | Exec weekly & prior | `KpiRow`, `PnlChart`, `Heatmaps`, `Changelog`, `ExportButtons` |
| 8 | Data Quality | `/data/quality` | OPRA parity, IV/PnL MAPE gate | `QualityTiles`, `SurfaceCompare`, `QualityBanner` |
| 9 | Risk Console | `/risk/console` | Fibonacci budget/kill switch | `BudgetLadder`, `ReservationsTable`, `KillSwitch` |
| 10 | Broker | `/broker` | Paper/live plumbing | `ConnectionCard`, `DryRun`, `AuditLog` |
| 11 | **Models** | `/models` | Directory of **Active/Verified** models | `ModelCard`, `FilterBar`, `ModelStatusBadge` |
| 12 | **Model Details** | `/models/{modelId}` | **20-year** performance grids (GitHub-style) | `YearHeatmap`, `Legend`, `PerfTiles`, `TradeList`, `ExportButtons` |

---

## 4) Models Directory (Active/Verified)
**Route:** `/models`

**Objective:** List all **Active** and **Verified** models with status, brief performance, and links.

**UX Layout:**
```
[Filter: Status (Active/Verified), Asset, DTE, Strategy] [Search]
┌────────────────────────┬────────────────────────┬────────────────────────────┐
│ ModelCard (Verified)   │ ModelCard (Active)     │ ModelCard (Active)         │
│ Name • Strategy • DTE  │ Name • Strategy • DTE  │ Name • Strategy • DTE      │
│ 1Y mini heatmap        │ 1Y mini heatmap        │ 1Y mini heatmap            │
│ [Open] [Compare]       │ [Open] [Compare]       │ [Open] [Compare]           │
└────────────────────────┴────────────────────────┴────────────────────────────┘
```

**Components:**  
- `ModelCard` (title, badges, last-12-month mini heatmap, quick KPIs)  
- `ModelStatusBadge` (`Active`, `Verified` with tooltips on verification criteria)  
- `Compare` adds models to `/models/compare?ids=...` (optional).

---

## 5) Model Details Page (20-Year Performance)
**Route:** `/models/{modelId}`

**Objective:** Present **20 years** of day-level performance in **GitHub-style yearly heatmaps**, with overlays for trade execution and clear semantics for weekends/holidays/no-trade.

### 5.1 Layout
```
[ModelHeader]  Name • Strategy • DTE • StatusBadge(Verified/Active) • Tags
[PerfTiles]    KPIs: Total PnL, CAGR, Max DD, Win rate, Avg RR, NbboQuality%
[Legend]       Colors + markers (see below)
[Controls]     Year range selector • Aggregation • Thresholds • Export
[Heatmaps]     20x YearHeatmap (or paged 5-at-a-time): 2006 … 2025
[TradeList]    Selected day details (executed trades, entries, exits, PnL, notes)
```

### 5.2 Data Semantics
- **Timeframe:** 20 full calendar years (e.g., 2006–2025).  
- **Resolution of opportunity:** **Every 10 minutes** during US regular session for the relevant instrument.  
- **Trading day aggregation:** per-day PnL is the sum of outcomes across 10-minute **opportunity windows** (see 5.3) with the model’s execution rules.  
- **Execution marking:** if at least one trade **executed** on a day, the cell shows an **overlay marker** (●).  
- **No-trade day:** shows color for net PnL = 0 (neutral gray) unless it’s *holiday/weekend* (dark gray).  
- **Holidays/weekends:** explicitly annotated via calendar; these cells are **non-interactive**.

### 5.3 Opportunity Windows (10-Minute)
- **Session:** Default US RTH (e.g., 09:30–16:00 ET) → **6.5 hours = 39 windows/day**.  
- **Window index:** `0..38` where `0 = 09:30–09:39`, `1 = 09:40–09:49`, …, `38 = 15:50–15:59`.  
- **Mapping to PnL:** model evaluates each window; if rules trigger, the trade’s PnL contributes to the day’s cell total.  
- **Optional display:** per-day hover shows a tiny sparkline or histogram of the 39-window outcomes.

### 5.4 Color & Markers (Legend)
- **Profit shades (green):** map daily PnL percentile to light→dark green.  
- **Loss shades (red):** map negative PnL magnitude to light→dark red.  
- **No-trade (neutral):** mid-gray.  
- **Holiday/weekend:** dark slate gray with diagonal hatch.  
- **Executed trade present:** **●** (dot) overlay at top-right corner of the cell.  
- **Data-quality fail (NBBO parity fail):** yellow border around the cell (hover shows reason).

Example (approximate hex):
```
Loss:   - High  #8B0000 | Mid  #CD5C5C | Light #F4A6A6
Neutral:        #C0C0C0
Profit: + Light #A8E6A1 | Mid  #5BCB5E | High  #1E7F31
Holiday/WE:     #2F3640 (hatch)
Exec Marker:    black ●
Quality Fail:   2px border: #FFC107
```

### 5.5 Interaction
- **Hover:** shows tooltip with Date, Total PnL, #Trades, Win/Loss, Spread%, NBBO quality flags, **first 3 trades** summaries.  
- **Click:** populates **TradeList** panel with full detail (legs, credits/debits, maxLoss, greeks, rule reasons).  
- **Zoom controls:** page through 5 years at a time, or enable vertical scroll with sticky header.  
- **Export:** per-year PNG/SVG snapshot + CSV of the underlying day metrics.

### 5.6 Components (Blazor)
- `YearHeatmap` → renders a 53×7 grid for each calendar year (ISO weeks).  
- `Legend` → static mapping with live thresholds.  
- `PerfTiles` → KPIs, recomputed on filter changes.  
- `TradeList` → virtualized table for intraday/executions.  
- `QualityBadge` → summarises OPRA parity compliance for the filtered range.

---

## 6) Data Model & Queries (SQLite-first)

### 6.1 Tables (suggested)
- `model_catalog(model_id TEXT PK, name, strategy, dte, status, verified_on, tags)`  
- `market_calendar(date DATE PK, is_trading_day BOOL, is_holiday BOOL, holiday_name TEXT)`  
- `model_day_perf(model_id, date, pnl REAL, trades INT, executed BOOL, nbbo_quality_score REAL, PRIMARY KEY(model_id, date))`  
- `model_intraday_perf(model_id, date, window INT, pnl REAL, executed BOOL, features JSON, PRIMARY KEY(model_id,date,window))`

### 6.2 Example Queries
**Fetch 20-year day cells for a model:**
```sql
SELECT d.date,
       COALESCE(p.pnl, 0) AS pnl,
       COALESCE(p.trades, 0) AS trades,
       COALESCE(p.executed, 0) AS executed,
       COALESCE(p.nbbo_quality_score, 1.0) AS nbbo_q
FROM market_calendar d
LEFT JOIN model_day_perf p
  ON p.date = d.date AND p.model_id = @modelId
WHERE d.date BETWEEN @from AND @to
ORDER BY d.date;
```

**Compute day aggregation from 10-min windows (ETL step):**
```sql
INSERT INTO model_day_perf (model_id, date, pnl, trades, executed, nbbo_quality_score)
SELECT model_id, date,
       SUM(pnl) AS pnl,
       SUM(CASE WHEN executed THEN 1 ELSE 0 END) AS trades,
       MAX(CASE WHEN executed THEN 1 ELSE 0 END) AS executed,
       MIN(json_extract(features,'$.nbbo_q')) AS nbbo_quality_score
FROM model_intraday_perf
WHERE model_id=@modelId
  AND date BETWEEN @from AND @to
GROUP BY model_id, date
ON CONFLICT(model_id,date) DO UPDATE SET
  pnl=excluded.pnl, trades=excluded.trades, executed=excluded.executed,
  nbbo_quality_score=excluded.nbbo_quality_score;
```

---

## 7) Razor Structure (Model Pages)

### 7.1 Directory page
`/Pages/Models.razor`
```razor
@page "/models"
<PageTitle>Models</PageTitle>

<ModelFilterBar @bind-Filter="Filter" OnSearch="LoadModels" />
<div class="grid grid-cols-3 gap-4">
  @foreach (var m in Models)
  {
    <ModelCard Model="m" />
  }
</div>
```

### 7.2 Detail page
`/Pages/ModelDetail.razor`
```razor
@page "/models/{ModelId}"
@inject IModelPerf Perf
@inject IModelCatalog Catalog

@if (Model is null) { <p>Loading…</p> }
else {
  <ModelHeader Model="Model" />
  <PerfTiles Data="Summary" />
  <Legend />

  <HeatmapControls @bind-From="From" @bind-To="To" OnChange="Reload" />

  <div class="year-heatmaps">
    @foreach (var y in Years)
    {
      <YearHeatmap Year="y" Cells="GetCells(y)" OnSelect="SelectDay" />
    }
  </div>

  <TradeList Items="SelectedDayTrades" />
  <ExportButtons OnExportPng="ExportPng" OnExportCsv="ExportCsv" />
}
```

---

## 8) Quality & Risk Integration

- **Data Quality Gate:** Cells with `nbbo_quality_score < threshold` render a **yellow border** and contribute to a **YearQuality%** metric displayed in `PerfTiles`. The Prospects/Now pages read the same gate to enable/disable actions.  
- **Risk Visibility:** Top KPI bar mirrors the **Reverse-Fibonacci** bracket and **Open Risk** so users know, at a glance, whether the model is operating within budget.

---

## 9) Accessibility & Theming
- **WCAG contrast** for reds/greens; add **pattern fills** for color-blind safety.  
- **Keyboard navigation** across heatmap cells; tooltips open on focus.  
- **Responsive** layout: 5-year stacks per row (desktop), 1-year per row (mobile).  

---

## 10) Export & Sharing
- **Exports:** per-year PNG/SVG snapshot + CSV.  
- **Shareable deep links:** `/models/{modelId}?from=2010-01-01&to=2025-12-31`.  
- **Auditability:** CSV includes day PnL, #trades, executed flag, quality score, and model version hash.

---

## 11) Acceptance Criteria (DoD)
- **Directory** lists only **Active/Verified** models with status badges.  
- **Detail page** renders **20 years** of day-level heatmaps, with hover tooltips and click-through trade details.  
- **Semantics** for **profit/loss/no-trade/holiday/weekend/executed** are visually distinct and consistent.  
- **Quality overlay** (yellow border) appears when NBBO parity falls below threshold.  
- **Exports** (PNG/SVG/CSV) work for any selected range.  
- **Performance:** heatmap virtualization keeps interactions < 50ms per hover on modern hardware.

---

### Appendix: Daily Color Mapping
Let `pnl_day` be total PnL per day. Compute percentiles P10, P50, P90 on the selected range:  
- `pnl_day <= 0`: interpolate red from 0 to P10 (cap at 2×|P10|).  
- `0 < pnl_day < P90`: interpolate green from near-neutral to strong.  
- `pnl_day >= P90`: strongest green.  
- `executed == false && is_trading_day == true`: neutral gray.  
- `is_holiday || !is_trading_day`: dark gray + hatch.

