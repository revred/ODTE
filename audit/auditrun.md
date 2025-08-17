# PM212 — Institutional Audit Runbook (`auditrun.md`)

**Version:** 1.0  
**Owner:** Risk & Controls (ODTE.Strategy)  
**Purpose:** Provide a _repeatable, defensible_ audit to verify that PM212 backtest results (Jan 2005–Jul 2025) are real, risk-compliant, and institutionally credible — not overfit or “gamed”.

---

## 0) Scope & Artifacts

- **Database under test (DUT):** `PM212.sqlite3` (or equivalent path)
- **Audit tools (provided):**
  - `PM212_AuditPack.sql` — schema-agnostic SQL spot-checks
  - `pm212_audit.py` — deep institutional checks (loss guardrail, NBBO plausibility, slippage sensitivity)
- **Repo reference:** Commit on `master` that claims “PM212 Defensive Analysis suite”
- **Date window:** 2005‑01‑01 → 2025‑07‑31 (adjustable)

> If the DB lives elsewhere, replace `PM212.sqlite3` below with the correct path.

---

## 1) Preconditions

- **sqlite3** available in PATH  
- **python 3.9+** available in PATH (pandas optional; script runs without it)  
- Read access to the PM212 database file

Optional but recommended:
- `shasum` or `openssl` for hashing the DB

---

## 2) Integrity & Provenance (MANDATORY)

1. Record the Git **commit hash** and tag the database filename with it if possible.  
2. Compute and record the **SHA‑256** of the DUT:

**macOS/Linux**
```bash
shasum -a 256 PM212.sqlite3
```

**Windows (PowerShell)**
```powershell
Get-FileHash .\PM212.sqlite3 -Algorithm SHA256
```

> Save the hash + commit in the final report. Any re-run using a different file must declare the new hash.

---

## 3) Quick Start (3 commands)

> Run from the folder where the DUT and audit tools reside. Redirect outputs to persist artefacts.

**A) SQL audit (spot checks)**
```bash
sqlite3 PM212.sqlite3 < PM212_AuditPack.sql > audit_sql.md
```

**B) Python audit (institutional checks)**
```bash
python pm212_audit.py PM212.sqlite3 --start 2005-01-01 --end 2025-07-31   --out pm212_audit_report.json > audit_py_stdout.json
```

**C) Snapshot the environment**
```bash
python -V
sqlite3 -version
```

Deliverables created:
- `audit_sql.md` — tables, coverage, daily P&L, NBBO plausibility summary, duplicates
- `pm212_audit_report.json` — guardrail breaches, NBBO stats, slippage PFs
- `audit_py_stdout.json` — same JSON printed to stdout (optional)

---

## 4) Detailed Procedure & Acceptance Criteria

### 4.1 SQL Audit (PM212_AuditPack.sql)

What it does:
- Lists **tables/views** present
- **Yearly** P&L reconstruction (2005–2025)
- **Daily** P&L view (`v_daily`)
- **Reverse-Fibonacci** guardrail emulation (breach list)
- **NBBO plausibility** (if a quotes/nbbo table exists): % within [bid−$0.01, ask+$0.01], % mid-or-better
- **Capacity sanity:** top-20 days by notional proxy
- **Duplicate/overlap** detection (same option legs within ±60s)

> If your schema uses different column names, adjust the `v_trades`/`v_quotes` view column mappings at the top of the SQL pack.

**Acceptance (SQL):**
- ✅ No errors executing the script
- ✅ **0 guardrail breaches** in the recursive daily check
- ✅ NBBO check present and computed (or if absent, a documented reason + alternative evidence is provided)

### 4.2 Python Audit (pm212_audit.py)

What it adds:
- **Authoritative daily guardrail** check ($500 → $300 → $200 → $100; reset after green day)
- **NBBO plausibility** with sample outliers (if quotes available)
- **Slippage robustness**: re-computes daily results after **$0.05** and **$0.10** per-contract penalties (multiplier-aware)

**Acceptance (Python):**
- ✅ **Daily breach count = 0**
- ✅ **NBBO band coverage ≥ 98%** (trades within bid/ask ± $0.01)
- ✅ **Mid-or-better rate < 60%** for options entries (higher values may indicate execution fantasy)
- ✅ **Slippage Profit Factor** remains **> 1.30** at **$0.05** and **> 1.15** at **$0.10** per-contract penalties  
  (Adjust these floors only with desk head sign-off and historical fill studies.)

> If `quotes`/`nbbo` table is missing, NBBO tests are skipped. That’s acceptable only with documented **third-party provenance** (e.g., OPRA snapshots) or **tick replay** evidence. Otherwise, **Fail** pending data.

---

## 5) Red‑Flag Forensics (run if anything looks “too good”)

1. **Look‑ahead leakage canary**  
   Recompute signals using only data up to `entry_time`. Material P&L drift ⇒ leakage.
2. **Timestamp sanity**  
   Ensure trade timestamps align with available quotes to the second. Suspicious clustering at extreme candles ⇒ investigate.
3. **“Impossible price” fills**  
   Any entry well **outside** NBBO band (beyond tolerance) must be rare and explainable (crossed markets, stale quotes). Investigate samples listed in JSON.
4. **Assignment/Expiry realism**  
   Check early exercise around **ex‑dividend** and expiry behaviors for ITM legs.
5. **Parameter fragility**  
   Nudge key thresholds ±5–10%. Robust edges should **not collapse**.
6. **Capacity/Scaling**  
   Compare daily notional vs market volume in traded strikes. Strategy must be scalable within desk limits.

**Disposition:** Any unexplained red flag ⇒ **Fail (Remediate & Re‑test)**.

---

## 6) Risk Doctrine Enforcement (Pre‑Trade Gate)

Even if audits pass, execution must refuse orders that can breach daily loss limits:

**Guardrail (Reverse Fibonacci)**
- Start of day limit = **$500**
- 1st red day → next day limit **$300**
- 2nd consecutive red → **$200**
- 3rd+ consecutive red → **$100**
- Any green day resets to **$500**

**Pre‑trade rule (must‑pass):**
```
if (realized_loss_today + MaxLoss(order)) > allowed_daily_limit:
    REJECT(order)
```
Also enforce **MaxLoss per structure** (credit spreads, flies, ICs) before submission.

---

## 7) Reporting Package (what to file)

1. **Cover sheet** (fill & paste into change record)
   - Commit: `xxxxxxxx` (hash)
   - DB SHA‑256: `xxxxxxxx`
   - Window: `2005-01-01` → `2025-07-31`
   - Tools: `PM212_AuditPack.sql` v1.0, `pm212_audit.py` v1.0
   - Environment: `python -V`, `sqlite3 -version`

2. **Results** (paste key figures)
   - Guardrail breaches: `0` (Required)
   - NBBO coverage: `xx.xx%` (≥ 98%)
   - Mid-or-better: `xx.xx%` (< 60%)
   - Profit Factor with $0.05 slip: `x.xx` (≥ 1.30)
   - Profit Factor with $0.10 slip: `x.xx` (≥ 1.15)

3. **Artefacts**
   - `audit_sql.md`
   - `pm212_audit_report.json`
   - `audit_py_stdout.json` (optional)

4. **Disposition**
   - ✅ **Approved** / ❌ **Rejected**  
   - Notes / remediation if rejected

**Approvals:**  
- Quant Lead: __________________ Date: ________  
- Risk Head: __________________ Date: ________  
- Execution Lead: _____________ Date: ________

---

## 8) Troubleshooting

- **SQL view mapping errors** → Edit `v_trades`/`v_quotes` COALESCE lists in `PM212_AuditPack.sql`
- **Python couldn’t find columns** → Adjust autodetect lists at top of `pm212_audit.py`
- **NBBO table missing** → Provide alternate evidence (OPRA excerpts / tick replay) or mark **Fail pending data**

---

## 9) Appendix

### 9.1 Windows vs macOS/Linux Commands
- PowerShell replaces `\` paths and uses `Get-FileHash` for SHA‑256; otherwise commands are equivalent.

### 9.2 Reproducibility
- Re-run full backtest from clean commit with **seed pinned**. **Trade‑by‑trade** outputs must match the forensic DB. Any drift ⇒ investigate nondeterminism.

### 9.3 Schema Notes
- If your trades, quotes, or multiplier fields differ, update the COALESCE maps at the top of `PM212_AuditPack.sql` and the autodetect candidates in `pm212_audit.py`.

---

## 10) Final Checklist (tick all)

- [ ] DB SHA‑256 recorded and matches reported file  
- [ ] SQL audit completed without error (`audit_sql.md` saved)  
- [ ] Python audit completed (`pm212_audit_report.json` saved)  
- [ ] Guardrail breaches = **0**  
- [ ] NBBO coverage ≥ **98%**  
- [ ] Mid‑or‑better < **60%**  
- [ ] Slippage PF ≥ **1.30** @ $0.05 and ≥ **1.15** @ $0.10  
- [ ] Red‑flag forensics reviewed; none outstanding  
- [ ] Risk gate enforced in execution path  
- [ ] Approvals signed

---

**End of Runbook**  
Contact: Risk & Controls — ODTE.Strategy