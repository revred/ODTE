# Synthetic Market Strategy — 4‑Week Build Plan (15‑minute Task Log)

**Objective:** Build a **hybrid historical + synthetic market** that makes the app “believe” it’s trading live, validate the ODTE strategy end‑to‑end, then flip to **IBKR Paper** (and later live) with guardrails (**≤ \$500** daily loss cap, per‑trade max loss ≤ \~\$200).

> This log is written so an engineering agent can execute it step‑by‑step. Each block is \~**15 minutes**. Every block ends with **Verification** (commands + expected artifacts). Adjust pacing by merging adjacent blocks if needed.

---

## Repo layout (target)

```
ODTE/
  ODTE.Backtest/                # existing backtest engine
  Synth/                        # NEW: synthetic market & scenarios
  LiveLike/                     # NEW: replay clock & fake broker
  data/                         # parquet/csv store (SPY, VIX, VIX9D, calendar)
  ml/                           # simple models + walk-forward
  tools/                        # downloaders, validators
  .github/workflows/dotnet.yml  # CI (already drafted)
```

---

## Global prerequisites (one‑time)

- .NET 8 SDK installed
- Python 3.11+ (for data tooling) + `pip install polars pyarrow pandas numpy scikit-learn` (if using Python path)
- GitHub Actions enabled on repo
- IBKR **TWS (paper)** installed; API enabled (localhost, 7497); paper login

**Verification (10 min)**

- `dotnet --info` shows .NET 8
- `git ls-files` includes `ODTE.Backtest.csproj`
- TWS opens; `Edit → Global Configuration → API` shows sockets enabled

---

# Week 1 — Data & Day Archetypes

### W1‑D1 — Bootstrap foldering & data schema (90 min)

**[00–15]** Create folders & schema stubs

- Add `data/spy_1m/.keep`, `data/vix/.keep`, `data/vix9d/.keep`, `data/calendar/.keep`
- Add `tools/schema.md` describing columns

**[15–30]** Add downloader stubs (choose one source)

- `tools/download_spy_1m.py` (Alpaca/Tiingo/Kibot adapters; CLI flags `--from --to --dest`)
- `tools/download_vix.py` (FRED VIX) & `tools/download_vix9d.py` (Yahoo ^VIX9D)

**[30–45]** Add validator

- `tools/validate_intraday.py` (checks missing minutes, RTH coverage, monotone timestamps)

**[45–60]** Sample calendar

- `data/calendar/calendar.csv` with CPI/FOMC/NFP UTC stamps (headers only if needed)

**[60–75]** CI smoke

- Extend `.github/workflows/dotnet.yml` with a **Data Lint** step calling the validators on sample files

**[75–90] Verification**

- Run: `python tools/download_spy_1m.py --from 2024-01-01 --to 2024-01-05 --dest data/spy_1m/`
- Run: `python tools/download_vix.py --dest data/vix/` & `python tools/download_vix9d.py --dest data/vix9d/`
- Run: `python tools/validate_intraday.py data/spy_1m/2024-01-02.parquet`
- Expect: Parquet files exist; validator prints **OK**; CI passes **Data Lint**

---

### W1‑D2 — Feature maker (intraday) (90 min)

**[00–15]** Create `tools/feature_maker.py`

- Inputs: SPY 1‑min Parquet for a day → Outputs `features/DATE.parquet`

**[15–30]** Compute OR(15m), VWAP(30m), ATR(20), session clock, 5/15‑min momentum

**[30–45]** Join daily VIX & VIX9D onto every row

**[45–60]** Write unit tests (`tools/tests/test_features.py`) for OR/VWAP/ATR correctness on toy data

**[60–75]** Wire Makefile shortcuts

- `make features FROM=2024-01-01 TO=2024-01-31`

**[75–90] Verification**

- Run: `make features FROM=2024-01-02 TO=2024-01-03`
- Inspect sample rows: `python - <<<'import polars as pl;print(pl.scan_parquet("features/2024-01-02.parquet").head(5).collect())'`
- Tests: `pytest tools/tests -q` → **all passed**

---

### W1‑D3 — Day archetypes (clustering) (90 min)

**[00–15]** `Synth/DayArchetypeClassifier.cs` scaffold

- Loads per‑day aggregates (OR range, range/ATR, VWAP side %)

**[15–30]** Implement k‑means (or call Python via `ml/day_cluster.py`) to label days: `calm_range, trend_up, trend_dn, fakeout, event_spike_fade`

**[30–45]** Persist labels → `data/archetypes/labels.csv` (`date, archetype`)

**[45–60]** Add CLI: `dotnet run --classify-days --from 2024-01-01 --to 2025-07-31`

**[60–75]** Unit test: small synthetic input → stable cluster assignment

**[75–90] Verification**

- Run classifier on one month
- Inspect `data/archetypes/labels.csv` rows; counts per archetype look reasonable

---

# Week 2 — Synthetic Market (HYSIM) v1

### W2‑D1 — Scenario DSL & market stream skeleton (120 min)

**[00–15]** Add `Synth/ScenarioDsl.cs` (parse YAML)

**[15–30]** Add `Synth/MarketStream.cs`

- Interface `IMarketStream: IAsyncEnumerable<SpotTick>`
- `SpotTick { DateTime Ts; double O,H,L,C,V; double Vwap; double Atr; }`

**[30–45]** Add CLI: `dotnet run --scenario ./Synth/scenarios/calm_range.yaml --replay 5x`

**[45–60]** Seed first scenario YAML (`calm_range.yaml`)

**[60–75]** Integrate historical blocks: create `Synth/BlockBootstrap.cs` (takes archetype → yields 5–15 min stitched segments)

**[75–90]** Verification

- Run scenario; log first 50 ticks to `./out/stream.log`
- Expect: timestamps monotone; VWAP defined after window; ATR non‑zero

**[90–105]** Add `tools/check_stream_stats.py` to compute range, OR, VWAP flips

**[105–120] Verification**

- Run checker; stats fall within calm‑range bounds (document expected thresholds in code)

---

### W2‑D2 — Microstructure & IV surface (120 min)

**[00–15]** `Synth/Microstructure.cs` — enforce U‑shaped volume; lunch lull; late‑session widening

**[15–30]** Hook microstructure into MarketStream (volume shaping + last‑hour flags)

**[30–45]** `Synth/IvSurface.cs` — base IV from VIX/VIX9D; put‑skew; call‑wing softness; time‑to‑close widening

**[45–60]** Implement `GetQuotesAt(ts)` returning \~±1–10% strikes with (bid/ask/mid/Δ/IV)

**[60–75]** Wire to existing `IOptionsData` so **Backtester** can accept `IOptionsData` from **HYSIM**

**[75–90]** Verification

- Hook Backtester to HYSIM via flag `--synthetic-source hysim`
- Run one day; confirm trades & quotes populate; CSV `trades.csv` not empty

**[90–105]** Add test: credit/width floors applied; last‑hour quotes wider than mid‑day

**[105–120] Verification**

- Unit tests green; inspection shows later half‑spreads > earlier (as expected)

---

### W2‑D3 — Event injector & replay clock (120 min)

**[00–15]** `Synth/EventInjector.cs` — CPI/FOMC templates: price jump σ, IV jump %, spread widen bps

**[15–30]** Scenario `event_spike_fade.yaml` using injector at 13:30 UTC

**[30–45]** `LiveLike/ReplayClock.cs` — wall‑clock (1×) & accelerated (e.g., 10×) modes; emits ticks on schedule

**[45–60]** `LiveLike/PaperBrokerFake.cs` — fills based on (mid ± n ticks), cancel/replace policy, expire OTM free

**[60–75]** Verification

- Replay **event** scenario at 5×; observe stop‑outs increase near event windows

**[75–90]** Add **Suggestion Matrix** logging in replay: CSV of decisions each 15 min

**[90–105]** Verification

- Matrix file exists; counts match decision windows; no entries in last‑hour window

**[105–120]** Smoke E2E (one calm day, one event day) → Net P&L reasonable, no day < −\$500

---

# Week 3 — Calibration & Adversarial

### W3‑D1 — Spread model calibration (90 min)

**[00–15]** Write `tools/calibrate_spreads.py` to fit half‑spread \~ a + b*IV + c*(late\_session)

**[15–30]** Fit using last 30 real days (or your best estimates); save `Synth/calibration.json`

**[30–45]** Load calibration into `IvSurface` & `Microstructure`

**[45–60]** Verification

- Regression R² printed; parameters non‑pathological; late session widening observed

**[60–75]** E2E: replay 5 days; compare historical vs HYSIM credit distributions (KS/AD test)

**[75–90]** Verification

- KS p‑value > 0.05 on matched regimes; if not, adjust knobs & repeat

---

### W3‑D2 — Adversarial scenarios (90 min)

**[00–15]** `Synth/Adversary.cs` — tiny search over (gap, IV path, VWAP flips) to maximize expected loss under constraints

**[15–30]** Hook adversary into Scenario DSL (`risk_stress.adversarial: true`)

**[30–45]** Run 20 adversarial days; log worst 5

**[45–60]** Verification

- Confirm daily P&L floor still ≥ −\$500; else tighten Δ bands, credit floors, or concurrency caps

**[60–75]** Capture new rules in `Docs/RulesCard.md` deltas

**[75–90]** Commit & CI passes

---

### W3‑D3 — ML risk gate (90 min)

**[00–15]** `ml/labels.py` — derive label per decision slot: stop‑hit (1/0), win size

**[15–30]** Train logistic/GBT for `P(stop | features)` with rolling windows

**[30–45]** Integrate in Backtester: suppress entry if `P(stop)*loss > 0.5*expected_gain`

**[45–60]** Verification

- Backtest over 3 months; win rate ≥ 75%, no day < −\$500

**[60–75]** Add `ml/report.ipynb` charts (calibration curve, feature importances)

**[75–90]** CI artifact upload of reports (Actions → Artifacts)

---

# Week 4 — Paper Trading & Readiness

### W4‑D1 — LiveRunner & IBKR paper dry‑run (120 min)

**[00–15]** `LiveLike/LiveRunner.cs` — scheduler every 15 min; renders Suggestion Matrix; calls `IBroker` if **trade** mode

**[15–30]** Wire `mode: live_ib` + safety: kill‑switch at −\$500; no‑new‑risk last 60 min

**[30–45]** Connect to TWS paper; place **dummy** orders read‑only (no transmit) to validate contract resolution

**[45–60]** Verification

- Logs show connected; contractIds resolved; no exceptions

**[60–75]** Enable tiny‑size XSP orders (qty=1) in **paper**

**[75–90]** Verification

- Fills received; PnL loop reconciles with our model; expiries behave as expected

**[90–105]** Failure drills

- Kill TWS mid‑session → app pauses; reconnect resumes

**[105–120]** Verification

- Kill‑switch triggers at synthetic −\$500; Suggestion Matrix shows no entries after 15:00 ET

---

## Continuous Verification Matrix (attach to every PR)

- **Unit tests**: features, spread builder gates, stop logic
- **Property tests**: no new entries in last‑hour; no order that makes worst‑case portfolio risk > \$500
- **Distribution checks**: KS/AD between HYSIM vs last‑month real for credits, spreads, stop frequency
- **E2E replay**: calm day + event day + adversarial day → Net ≥ 0 on calm; bounded drawdown on others
- **Paper smoke**: connect → contractIds → place → fill → settle → PnL file written

Each CI run uploads:

- `Reports/summary.txt`, `Reports/trades.csv`
- `out/stream_stats.json`, `ml/metrics.json`, `ml/plots.zip`

---

## Definition of Done (DoD)

1. `dotnet run -- scenario ./Synth/scenarios/calm_range.yaml --replay 5x` produces trades and passes gates.
2. `dotnet run -- backtest --from 2024-02-01 --to 2025-07-31 --synthetic-source hysim` shows **≥ 75% win** and **no day < −\$500** over held‑out months.
3. `mode: live_ib` connects to **paper**, places 1‑lot XSP with correct contracts, and reconciles PnL.
4. CI green with artifacts attached.

---

## Quick Command Sheet (paste‑and‑go)

```bash
# Data backfill (SPY 1m, VIX, VIX9D)
python tools/download_spy_1m.py --from 2024-01-01 --to 2025-07-31 --dest data/spy_1m/
python tools/download_vix.py --dest data/vix/
python tools/download_vix9d.py --dest data/vix9d/
python tools/validate_intraday.py data/spy_1m/2024-02-01.parquet

# Features
make features FROM=2024-02-01 TO=2024-02-28

# Classify archetypes
 dotnet run --project ODTE.Backtest -- classify-days --from 2024-02-01 --to 2025-07-31

# HYSIM replay (calm day, 5x speed)
 dotnet run --project ODTE.Backtest -- scenario ./Synth/scenarios/calm_range.yaml --replay 5x

# Backtest over 18 months
 dotnet run --project ODTE.Backtest -- backtest --from 2024-02-01 --to 2025-07-31 --synthetic-source hysim

# Paper trading (XSP qty=1, 15m cadence)
 dotnet run --project ODTE.Backtest -- mode live_ib --qty 1 --cadence 900
```

---

### Notes

- Keep **per‑trade worst‑case ≤ \$200** and **daily loss stop = \$500** enforced at order‑time to guarantee risk bounds.
- You can substitute real intraday options feeds later; `IOptionsData` makes it a drop‑in.
- The **adversarial pass** is essential: it finds failure modes in calm/trend days you won’t see with random replays.

