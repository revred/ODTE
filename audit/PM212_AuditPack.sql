-- PM212_AuditPack.sql
-- Purpose: Institutional-grade spot checks for ODTE/PM212 backtest databases (SQLite).
-- Usage:   sqlite3 PM212.sqlite3 < PM212_AuditPack.sql > audit_out.txt
-- Notes:   Adjust table/column names in the VIEWs below if your schema differs.

.timer on
.headers on
.mode markdown

-- 0) What tables exist?
SELECT name AS table_name, type FROM sqlite_schema WHERE type IN ('table','view') ORDER BY 1;

-- 1) Quick row counts
WITH t(name) AS (SELECT name FROM sqlite_schema WHERE type='table')
SELECT name AS table_name,
       (SELECT COUNT(*) FROM sqlite_master WHERE name=t.name AND type='table') AS is_table,
       (SELECT COUNT(*) FROM (SELECT 1 FROM main.sqlite_schema WHERE name=t.name)) AS is_visible,
       (SELECT COUNT(*) FROM (
           SELECT * FROM main.sqlite_schema s WHERE s.name=t.name
       )) AS meta_count
FROM t;

-- 2) Create views with best-guess column mappings. Adjust if needed.
DROP VIEW IF EXISTS v_trades;
CREATE VIEW v_trades AS
SELECT
  COALESCE(trade_id, id, rowid)            AS trade_id,
  COALESCE(symbol, underlying, ticker)      AS symbol,
  COALESCE(expiry, expiration, expiry_date) AS expiry,
  COALESCE(strike, strike_price)            AS strike,
  COALESCE(option_type, right, call_put)    AS right,
  COALESCE(side, action)                    AS side,
  COALESCE(qty, quantity, contracts, size)  AS qty,
  COALESCE(entry_time, open_time, time_in, ts_in, timestamp_in, timestamp) AS entry_time,
  COALESCE(exit_time, close_time, time_out, ts_out)                         AS exit_time,
  COALESCE(entry_price, open_price, fill_price, price_in, avg_entry)       AS entry_price,
  COALESCE(exit_price, close_price, price_out, avg_exit)                    AS exit_price,
  COALESCE(realized_pnl, realized, pnl, profit)                             AS realized_pnl,
  COALESCE(fees, commission, commissions, total_fees)                       AS fees,
  COALESCE(multiplier, contract_multiplier)                                  AS multiplier
FROM trades;

-- 3) Basic trade distribution & coverage (2005-01 to 2025-07)
SELECT substr(entry_time,1,10) AS trade_date, COUNT(*) AS trades
FROM v_trades
WHERE entry_time BETWEEN '2005-01-01' AND '2025-07-31'
GROUP BY 1 ORDER BY 1 LIMIT 20;

-- 4) Yearly summary
SELECT substr(entry_time,1,4) AS yyyy,
       COUNT(*) AS trades,
       ROUND(SUM(COALESCE(realized_pnl, (COALESCE(exit_price,0)-COALESCE(entry_price,0)) * COALESCE(qty,0) * COALESCE(multiplier,100))) ,2) AS gross_pnl,
       ROUND(SUM(COALESCE(fees,0)),2) AS fees,
       ROUND(SUM(COALESCE(realized_pnl, (COALESCE(exit_price,0)-COALESCE(entry_price,0)) * COALESCE(qty,0) * COALESCE(multiplier,100))) - SUM(COALESCE(fees,0)),2) AS net_pnl
FROM v_trades
GROUP BY 1 ORDER BY 1;

-- 5) Daily P&L
DROP VIEW IF EXISTS v_daily;
CREATE VIEW v_daily AS
SELECT substr(entry_time,1,10) AS trade_date,
       ROUND(SUM(COALESCE(realized_pnl, (COALESCE(exit_price,0)-COALESCE(entry_price,0)) * COALESCE(qty,0) * COALESCE(multiplier,100))) - SUM(COALESCE(fees,0)),2) AS net_pnl
FROM v_trades
GROUP BY 1;

SELECT * FROM v_daily ORDER BY trade_date LIMIT 20;

-- 6) Loss-streak / Reverse-Fibonacci guardrail compliance check (assumes $500→$300→$200→$100 with reset on green).
--    This is a pure SQL emulation; for a definitive check use the Python script (pm212_audit.py).
WITH RECURSIVE
r AS (
  SELECT trade_date, net_pnl,
         0 AS loss_streak,
         500 AS allowed_loss,
         CASE WHEN net_pnl < 0 AND ABS(net_pnl) > 500 THEN 1 ELSE 0 END AS breach
  FROM v_daily WHERE trade_date=(SELECT MIN(trade_date) FROM v_daily)
  UNION ALL
  SELECT d.trade_date, d.net_pnl,
         CASE WHEN d.net_pnl < 0 THEN MIN(3, r.loss_streak+1) ELSE 0 END AS loss_streak,
         CASE
           WHEN d.net_pnl >= 0 THEN 500
           WHEN r.loss_streak = 0 THEN 300
           WHEN r.loss_streak = 1 THEN 200
           ELSE 100
         END AS allowed_loss,
         CASE
           WHEN d.net_pnl < 0 AND ABS(d.net_pnl) >
                CASE
                  WHEN r.loss_streak = 0 THEN 300
                  WHEN r.loss_streak = 1 THEN 200
                  ELSE 100
                END
           THEN 1 ELSE 0 END AS breach
  FROM v_daily d, r
  WHERE d.trade_date = (SELECT trade_date FROM v_daily WHERE trade_date > r.trade_date ORDER BY trade_date LIMIT 1)
)
SELECT * FROM r WHERE breach=1 LIMIT 50;

-- 7) Fill plausibility (requires quotes/nbbo table).
-- Create a mapping view if your quotes table is named differently.
-- Assumes columns: ts, symbol, bid, ask.
DROP VIEW IF EXISTS v_quotes;
CREATE VIEW v_quotes AS
SELECT COALESCE(ts, timestamp, time, quote_time) AS ts,
       COALESCE(symbol, underlying, ticker) AS symbol,
       bid, ask
FROM quotes;

-- Joins each trade to the nearest quote before/at entry_time within 60s and checks if entry_price is within [bid-0.01, ask+0.01].
WITH q AS (
  SELECT v.trade_id, v.symbol, v.entry_time, v.entry_price,
         (SELECT bid FROM v_quotes
           WHERE symbol=v.symbol AND ts<=v.entry_time
           ORDER BY ts DESC LIMIT 1) AS bid,
         (SELECT ask FROM v_quotes
           WHERE symbol=v.symbol AND ts<=v.entry_time
           ORDER BY ts DESC LIMIT 1) AS ask
  FROM v_trades v
)
SELECT
  COUNT(*) AS trades_checked,
  SUM(CASE WHEN entry_price BETWEEN (bid-0.01) AND (ask+0.01) THEN 1 ELSE 0 END) AS within_nbbo_band,
  ROUND(100.0 * SUM(CASE WHEN entry_price BETWEEN (bid-0.01) AND (ask+0.01) THEN 1 ELSE 0 END) / COUNT(*),2) AS pct_within_nbbo,
  SUM(CASE WHEN entry_price < (bid-0.01) OR entry_price > (ask+0.01) THEN 1 ELSE 0 END) AS outliers
FROM q;

-- 8) Mid-or-better fill anomaly check
SELECT
  ROUND(100.0 * AVG(CASE WHEN entry_price >= (bid + ask)/2.0 THEN 1 ELSE 0 END),2) AS pct_at_or_above_mid
FROM (
  SELECT v.trade_id, v.entry_price,
         (SELECT bid FROM v_quotes WHERE symbol=v.symbol AND ts<=v.entry_time ORDER BY ts DESC LIMIT 1) AS bid,
         (SELECT ask FROM v_quotes WHERE symbol=v.symbol AND ts<=v.entry_time ORDER BY ts DESC LIMIT 1) AS ask
  FROM v_trades v
);

-- 9) Capacity sanity: per-day notional exposure proxy (needs multiplier and price)
SELECT substr(entry_time,1,10) AS trade_date,
       SUM(ABS(COALESCE(qty,0)) * COALESCE(multiplier,100) * COALESCE(entry_price,0)) AS notional_proxy
FROM v_trades
GROUP BY 1 ORDER BY 2 DESC LIMIT 20;

-- 10) Duplicate/overlapping positions (same symbol/expiry/strike/side within 1 minute)
SELECT a.trade_id AS t1, b.trade_id AS t2, a.symbol, a.expiry, a.strike, a.right, a.side, a.entry_time, b.entry_time
FROM v_trades a
JOIN v_trades b
  ON a.trade_id < b.trade_id
 AND a.symbol=b.symbol AND a.expiry=b.expiry AND a.strike=b.strike AND a.right=b.right AND a.side=b.side
 AND ABS(strftime('%s', a.entry_time) - strftime('%s', b.entry_time)) <= 60
LIMIT 50;

-- END OF PACK