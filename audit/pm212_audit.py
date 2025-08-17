
#!/usr/bin/env python3
import argparse, sqlite3, sys, json, datetime as dt, re, math
from collections import defaultdict, Counter
try:
    import pandas as pd
except ImportError:
    pd = None

def detect_table(cur, patterns):
    cur.execute("SELECT name FROM sqlite_schema WHERE type='table'")
    names = [r[0].lower() for r in cur.fetchall()]
    for pat in patterns:
        for n in names:
            if re.search(pat, n):
                return n
    return None

def candidate_cols(cur, table):
    cur.execute(f"PRAGMA table_info({table})")
    cols = [r[1] for r in cur.fetchall()]
    return {c.lower(): c for c in cols}

def pick(colmap, candidates, default=None):
    for c in candidates:
        if c in colmap:
            return colmap[c]
    return default

def fetch_df(cur, sql, params=None):
    if pd is None:
        return None
    import pandas as _pd
    return _pd.read_sql_query(sql, cur.connection, params=params or {})

def main():
    ap = argparse.ArgumentParser(description='PM212 Auditor — institutional sanity checks for ODTE backtests (SQLite).')
    ap.add_argument('db', help='Path to SQLite database (e.g., PM212.sqlite3)')
    ap.add_argument('--start', default='2005-01-01', help='Start date (YYYY-MM-DD)')
    ap.add_argument('--end', default='2025-07-31', help='End date (YYYY-MM-DD)')
    ap.add_argument('--tolerance', type=float, default=0.01, help='Price tolerance for NBBO checks')
    ap.add_argument('--out', default='pm212_audit_report.json', help='Output JSON summary file')
    args = ap.parse_args()

    con = sqlite3.connect(args.db)
    con.row_factory = sqlite3.Row
    cur = con.cursor()

    # Detect key tables
    trades_tbl = detect_table(cur, [r'trade', r'fills?', r'positions?'])
    quotes_tbl = detect_table(cur, [r'nbbo', r'quote', r'bestbidask', r'book'])
    bars_tbl   = detect_table(cur, [r'bar', r'ohlcv', r'prices?', r'underlying'])

    if not trades_tbl:
        print('ERROR: Could not find a trades table.', file=sys.stderr)
        sys.exit(2)

    tcols = candidate_cols(cur, trades_tbl)
    # Map likely columns
    c_trade_id   = pick(tcols, ['trade_id','id','rowid'])
    c_symbol     = pick(tcols, ['symbol','underlying','ticker'])
    c_expiry     = pick(tcols, ['expiry','expiration','expiry_date'])
    c_strike     = pick(tcols, ['strike','strike_price'])
    c_right      = pick(tcols, ['option_type','right','call_put'])
    c_side       = pick(tcols, ['side','action'])
    c_qty        = pick(tcols, ['qty','quantity','contracts','size'])
    c_entry_t    = pick(tcols, ['entry_time','open_time','time_in','ts_in','timestamp_in','timestamp'])
    c_exit_t     = pick(tcols, ['exit_time','close_time','time_out','ts_out'])
    c_entry_px   = pick(tcols, ['entry_price','open_price','fill_price','price_in','avg_entry','price'])
    c_exit_px    = pick(tcols, ['exit_price','close_price','price_out','avg_exit'])
    c_realized   = pick(tcols, ['realized_pnl','realized','pnl','profit'])
    c_fees       = pick(tcols, ['fees','commission','commissions','total_fees'])
    c_mult       = pick(tcols, ['multiplier','contract_multiplier'])

    # Pull trades
    sel = [c_trade_id, c_symbol, c_expiry, c_strike, c_right, c_side, c_qty, c_entry_t, c_exit_t, c_entry_px, c_exit_px, c_realized, c_fees, c_mult]
    cols = ','.join([c for c in sel if c])
    cur.execute(f"SELECT {cols} FROM {trades_tbl}")
    rows = cur.fetchall()

    def to_date(s):
        if s is None: return None
        if isinstance(s, (int,float)):
            # seconds since epoch
            try:
                return dt.datetime.utcfromtimestamp(int(s))
            except Exception:
                return None
        s = str(s)
        for fmt in ('%Y-%m-%d %H:%M:%S', '%Y-%m-%dT%H:%M:%S', '%Y-%m-%d', '%Y/%m/%d %H:%M:%S', '%Y-%m-%d %H:%M'):
            try:
                return dt.datetime.strptime(s[:19], fmt)
            except Exception:
                continue
        return None

    trades = []
    for r in rows:
        d = {k: r[k] for k in r.keys()}
        d['entry_dt'] = to_date(d.get(c_entry_t))
        d['exit_dt']  = to_date(d.get(c_exit_t))
        d['entry_px'] = float(d.get(c_entry_px) or 0)
        d['exit_px']  = float(d.get(c_exit_px) or 0)
        d['qty']      = float(d.get(c_qty) or 0)
        d['fees']     = float(d.get(c_fees) or 0)
        d['mult']     = float(d.get(c_mult) or 100)
        d['realized'] = d.get(c_realized)
        trades.append(d)

    # Daily P&L and Reverse-Fibonacci guardrail
    day_pnl = defaultdict(float)
    for t in trades:
        if t['entry_dt'] is None: continue
        day = t['entry_dt'].date()
        realized = t['realized']
        if realized is None:
            realized = (t['exit_px'] - t['entry_px']) * t['qty'] * t['mult']
        day_pnl[day] += (realized - t['fees'])

    daily = sorted(day_pnl.items())
    breaches = []
    loss_streak = 0
    for day, pnl in daily:
        if pnl >= 0:
            loss_streak = 0
            allowed = 500
        else:
            allowed = 300 if loss_streak==0 else 200 if loss_streak==1 else 100
            if abs(pnl) > allowed + 1e-6:
                breaches.append({'date': str(day), 'net_pnl': round(pnl,2), 'loss_streak_at_open': loss_streak, 'allowed_loss': allowed})
            loss_streak = min(3, loss_streak+1)

    # NBBO plausibility if quotes available
    nbbo_summary = None
    if quotes_tbl:
        qcols = candidate_cols(cur, quotes_tbl)
        c_q_ts   = pick(qcols, ['ts','timestamp','time','quote_time'])
        c_q_sym  = pick(qcols, ['symbol','underlying','ticker'])
        c_q_bid  = pick(qcols, ['bid','best_bid'])
        c_q_ask  = pick(qcols, ['ask','best_ask'])
        if all([c_q_ts, c_q_sym, c_q_bid, c_q_ask, c_symbol, c_entry_t, c_entry_px]):
            # Build an index of last quote per symbol per minute to keep it light
            cur.execute(f"SELECT {c_q_sym} sym, {c_q_ts} ts, {c_q_bid} bid, {c_q_ask} ask FROM {quotes_tbl}")
            qrows = cur.fetchall()
            quotes = defaultdict(list)
            for q in qrows:
                quotes[q['sym']].append((q['ts'], q['bid'], q['ask']))
            # Sort each list by ts
            for s in quotes:
                quotes[s].sort(key=lambda x: str(x[0]))

            def nearest(sym, ts):
                arr = quotes.get(sym)
                if not arr: return (None, None)
                # binary search by string compare (timestamps assumed sortable as strings)
                lo, hi = 0, len(arr)-1
                best = None
                while lo <= hi:
                    mid = (lo+hi)//2
                    if str(arr[mid][0]) <= str(ts):
                        best = arr[mid]
                        lo = mid+1
                    else:
                        hi = mid-1
                if best is None: return (None, None)
                return (float(best[1] or 0), float(best[2] or 0))

            checked = 0
            within = 0
            mid_or_better = 0
            outliers = []
            for r in rows:
                sym = r.get(c_symbol)
                ts  = r.get(c_entry_t)
                px  = r.get(c_entry_px)
                if sym is None or ts is None or px is None: continue
                bid, ask = nearest(sym, ts)
                if bid is None or ask is None: continue
                checked += 1
                if (px >= bid - args.tolerance) and (px <= ask + args.tolerance):
                    within += 1
                else:
                    outliers.append({'symbol': sym, 'time': str(ts), 'price': float(px), 'bid': bid, 'ask': ask})
                if px >= (bid+ask)/2.0: mid_or_better += 1
            nbbo_summary = {
                'trades_checked': checked,
                'within_nbbo_band': within,
                'pct_within_nbbo': round(100.0*within/checked,2) if checked else None,
                'pct_at_or_above_mid': round(100.0*mid_or_better/checked,2) if checked else None,
                'sample_outliers': outliers[:20]
            }

    # Slippage robustness: penalize 0.05, 0.10 per contract and recompute PF
    def pnl_with_slip(per_contract):
        day_p = defaultdict(float)
        wins = losses = 0
        gross_win = gross_loss = 0.0
        for t in trades:
            if t['entry_dt'] is None: continue
            day = t['entry_dt'].date()
            realized = t['realized']
            if realized is None:
                realized = (t['exit_px'] - t['entry_px']) * t['qty'] * t['mult']
            slip = per_contract * abs(t['qty']) * t['mult']
            r = (realized - t['fees'] - slip)
            day_p[day] += r
            if r > 0:
                wins += 1; gross_win += r
            elif r < 0:
                losses += 1; gross_loss += abs(r)
        pf = (gross_win / gross_loss) if gross_loss>0 else None
        return {'profit_factor': round(pf,2) if pf else None,
                'wins': wins, 'losses': losses,
                'total_days': len(day_p), 'net_sum': round(sum(day_p.values()),2)}

    slip05 = pnl_with_slip(0.05)
    slip10 = pnl_with_slip(0.10)

    summary = {
        'db': args.db,
        'trades_table': trades_tbl,
        'quotes_table': quotes_tbl,
        'bars_table': bars_tbl,
        'date_range': {'start': args.start, 'end': args.end},
        'daily_breach_count': len(breaches),
        'breach_samples': breaches[:20],
        'nbbo_summary': nbbo_summary,
        'slippage_sensitivity': {'$0.05': slip05, '$0.10': slip10},
        'notes': [
            'Adjust table/column mappings if auto-detection picks the wrong ones.',
            'If quotes_table is None, NBBO checks were skipped.',
            'Profit factor is computed on daily aggregates after slippage penalties.'
        ]
    }

    with open(args.out, 'w') as f:
        json.dump(summary, f, indent=2)
    print(json.dumps(summary, indent=2))

if __name__ == '__main__':
    main()
