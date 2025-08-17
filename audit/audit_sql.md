

Error executing statement: SELECT * FROM v_daily ORDER BY trade_date LIMIT 20
Error: SQLite Error 1: 'no such column: id'.

Error executing statement: for a definitive check use the Python script (pm212_audit.py).
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
SELECT * FROM r WHERE breach=1 LIMIT 50
Error: SQLite Error 1: 'near "for": syntax error'.


