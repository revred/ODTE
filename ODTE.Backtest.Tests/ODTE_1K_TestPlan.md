# Core 1000 Tests — Options Trading System

Generated: 2025-08-15T01:51:18.793998Z

This plan enumerates 20 suites totalling **1,000** tests. Each test has a stable ID (OT-0001…OT-1000).

## Suites & Counts

- **Pricing Engines** — 105
- **Greeks Validation** — 110
- **Implied Vol Solver** — 50
- **Put-Call Parity & Bounds** — 30
- **Vol Surface & Term Structure** — 90
- **Dividends & Carry** — 30
- **Interest Rates & Curves** — 30
- **Exercise Styles (Am/EU)** — 40
- **Calendar & Expiry Handling** — 35
- **Instrument Specs** — 25
- **Market Data & Ingestion** — 45
- **Slippage & Liquidity** — 35
- **Risk Limits & Capital Preservation** — 60
- **Backtest Determinism** — 25
- **Strategy Logic & Regimes** — 45
- **Stress Tests (Syntricks)** — 60
- **PnL Attribution & Greek Drift** — 25
- **Portfolio & Margin** — 40
- **Logging, Audit & Compliance** — 20
- **Performance & Numerical Stability** — 100

## Conventions
- Tolerance defaults: price=1e-6 (normalized), greeks=1e-5; override per test when noted.
- Expected results state *properties* (e.g., parity, monotonicity) and *quantitative checks* (e.g., epsilon).
- Any breach of **Reverse Fibonacci** risk caps is a **P0** failure.

## Sample Assertions

```text
Put-Call Parity (EU): C - P = F - K*DF  (|lhs - rhs| < 1e-6)
Early Exercise: AmericanCall(no-div) ≈ EuropeanCall; AmericanPut ≥ EuropeanPut
Monotonicity: ∂Price/∂IV ≥ 0; ∂Price/∂K ≤ 0 (calls)
Greeks Cross-Check: Analytic ≈ FiniteDiff (bump=1e-4)
IV Solver: root-finder converges; returns NaN if price < intrinsic or violates bounds
Risk Cap: DailyRealizedLoss ≤ Cap; new orders rejected after breach
Syntricks: No NaNs/inf; positions trimmed; logs contain scenario markers
Determinism: Same seed ⇒ identical pathwise PnL; parallel=serial within epsilon
```

## File Format
See `ODTE_1K_Tests.csv` with columns: TestID, Suite, Category, Subcategory, Description, Preconditions, Input/Parameters, Expected Result, Priority, Tags, References.
