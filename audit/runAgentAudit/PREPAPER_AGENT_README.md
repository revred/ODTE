# PM212 Pre‑Paper Audit Agent — README

This package performs a **one‑shot, deterministic** audit of a PM212 SQLite database before any paper trading. It enforces institutional thresholds and produces a decision: **APPROVE** or **REJECT** with reasons.

## Files
- `pm212_prepaper_agent.py` — main script (Python 3.9+)
- `pm212_agent_config.yaml` — thresholds & execution settings (optional override)

## Usage
```bash
python pm212_prepaper_agent.py --db PM212.sqlite3 --config pm212_agent_config.yaml --outdir prepaper_out
```

### Outputs
- `prepaper_out/pm212_prepaper_summary.json` — machine‑readable summary & decision
- `prepaper_out/pm212_prepaper_report.md` — human‑readable report
- `prepaper_out/breaches.csv` — (if any) Reverse‑Fib guardrail breaches
- `prepaper_out/nbbo_outliers_sample.csv` — (if any) NBBO outlier sample

### What it checks
- **Reverse‑Fibonacci daily loss guardrail** breaches (must be zero)
- **NBBO plausibility**: % fills within [bid−$0.01, ask+$0.01] and % mid‑or‑better
- **Slippage robustness**: Profit Factor under $0.05/$0.10 per‑contract penalties
- Final **decision** based on thresholds in the YAML

> If your database uses different column names, the script auto‑detects common names. If it still fails, adjust your schema or extend the detection lists in the script.

## Notes
- Provide a `quotes`/`nbbo` table for full plausibility checks. Without it, NBBO tests are skipped and decision may default to REJECT depending on policy.
- The script is **deterministic** and suitable for CI. Store the JSON alongside commit hashes and DB SHA‑256 for full provenance.