
#!/usr/bin/env python3
"""
PM212 Pre-Paper Audit Agent
Purpose: One-shot, deterministic audit for SQLite trade DBs before paper trading.
Outputs: Markdown report, JSON summary, CSV anomalies.
Usage:
  python pm212_prepaper_agent.py --db PM212.sqlite3 [--config pm212_agent_config.yaml]
"""
import argparse, sqlite3, sys, os, json, math, statistics as stats
from datetime import datetime, date
from collections import defaultdict

try:
    import yaml
except Exception:
    yaml = None

def load_config(path):
    default = {
        'date_range': {'start': '2005-01-01', 'end': '2025-07-31'},
        'thresholds': {
            'nbbo_within_pct': 98.0,
            'mid_rate_max_pct': 60.0,
            'pf_5c_min': 1.30,
            'pf_10c_min': 1.15,
            'guardrail_breaches_max': 0
        },
        'execution': {
            'tolerance': 0.01,
            'slippage_penalty_cents': [0.05, 0.10]
        }
    }
    if path and yaml:
        with open(path, 'r') as f:
            user_cfg = yaml.safe_load(f) or {}
        # shallow merge
        for k,v in user_cfg.items():
            if isinstance(v, dict) and k in default:
                default[k].update(v)
            else:
                default[k]=v
    return default

def detect_table(cur, patterns):
    cur.execute("SELECT name FROM sqlite_schema WHERE type='table'")
    names = [r[0] for r in cur.fetchall()]
    for pat in patterns:
        for n in names:
            if pat.lower() in n.lower():
                return n
    return None

def candidate_cols(cur, table):
    cur.execute(f"PRAGMA table_info({table})")
    return [r[1] for r in cur.fetchall()]

def pick(cols, candidates):
    low = {c.lower(): c for c in cols}
    for c in candidates:
        if c.lower() in low:
            return low[c.lower()]
    return None

def to_dt(s):
    if s is None: return None
    if isinstance(s,(int,float)):
        try:
            return datetime.utcfromtimestamp(int(s))
        except: return None
    s=str(s)
    for fmt in ('%Y-%m-%d %H:%M:%S','%Y-%m-%dT%H:%M:%S','%Y-%m-%d %H:%M','%Y-%m-%d'):
        try:
            return datetime.strptime(s[:19], fmt)
        except: pass
    return None

def profit_factor(series):
    wins = sum(x for x in series if x>0)
    losses = abs(sum(x for x in series if x<0))
    return (wins / losses) if losses>0 else None

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--db', required=True)
    ap.add_argument('--config', default='pm212_agent_config.yaml')
    ap.add_argument('--outdir', default='pm212_prepaper_out')
    args = ap.parse_args()

    cfg = load_config(args.config if os.path.exists(args.config) else None)
    os.makedirs(args.outdir, exist_ok=True)

    con = sqlite3.connect(args.db)
    con.row_factory = sqlite3.Row
    cur = con.cursor()

    trades_tbl = detect_table(cur, ['trade','fills','positions'])
    quotes_tbl = detect_table(cur, ['nbbo','quote','bestbidask','book'])
    if not trades_tbl:
        print('FATAL: trades table not found')
        sys.exit(2)

    tcols = candidate_cols(cur, trades_tbl)
    c_id    = pick(tcols, ['trade_id','id','rowid'])
    c_sym   = pick(tcols, ['symbol','underlying','ticker'])
    c_exp   = pick(tcols, ['expiry','expiration','expiry_date'])
    c_strk  = pick(tcols, ['strike','strike_price'])
    c_right = pick(tcols, ['option_type','right','call_put'])
    c_side  = pick(tcols, ['side','action'])
    c_qty   = pick(tcols, ['qty','quantity','contracts','size'])
    c_tin   = pick(tcols, ['entry_time','open_time','time_in','ts_in','timestamp_in','timestamp'])
    c_tout  = pick(tcols, ['exit_time','close_time','time_out','ts_out'])
    c_pin   = pick(tcols, ['entry_price','open_price','fill_price','price_in','avg_entry','price'])
    c_pout  = pick(tcols, ['exit_price','close_price','price_out','avg_exit'])
    c_real  = pick(tcols, ['realized_pnl','realized','pnl','profit'])
    c_fees  = pick(tcols, ['fees','commission','commissions','total_fees'])
    c_mult  = pick(tcols, ['multiplier','contract_multiplier'])

    sel = ','.join([c for c in [c_id,c_sym,c_exp,c_strk,c_right,c_side,c_qty,c_tin,c_tout,c_pin,c_pout,c_real,c_fees,c_mult] if c])
    cur.execute(f"SELECT {sel} FROM {trades_tbl}")
    rows = cur.fetchall()

    # Build daily pnl
    daily = defaultdict(float)
    for r in rows:
        entry_dt = to_dt(r[c_tin]) if c_tin else None
        if not entry_dt: continue
        day = entry_dt.date()
        qty = float(r[c_qty] or 0) if c_qty else 0.0
        mult = float(r[c_mult] or 100) if c_mult else 100.0
        pin = float(r[c_pin] or 0) if c_pin else 0.0
        pout= float(r[c_pout] or 0) if c_pout else 0.0
        fees= float(r[c_fees] or 0) if c_fees else 0.0
        realized = r[c_real] if c_real in r.keys() else None
        if realized is None:
            realized = (pout - pin) * qty * mult
        else:
            try: realized = float(realized)
            except: realized = 0.0
        daily[day] += (realized - fees)

    # Guardrail check
    start_loss, fib = 500.0, [300.0,200.0,100.0]
    loss_streak = 0
    guardrail_breaches = []
    for d in sorted(daily.keys()):
        pnl = daily[d]
        if pnl >= 0:
            loss_streak = 0
            allowed = start_loss
        else:
            allowed = fib[min(loss_streak,2)]
            if abs(pnl) > allowed + 1e-9:
                guardrail_breaches.append({'date': str(d), 'net_pnl': round(pnl,2), 'allowed': allowed, 'streak': loss_streak})
            loss_streak = min(3, loss_streak+1)

    # NBBO plausibility if quotes exist
    nbbo_stats = None
    nbbo_outliers = []
    if quotes_tbl:
        qcols = candidate_cols(cur, quotes_tbl)
        c_q_ts = pick(qcols, ['ts','timestamp','time','quote_time'])
        c_q_sym= pick(qcols, ['symbol','underlying','ticker'])
        c_bid  = pick(qcols, ['bid','best_bid'])
        c_ask  = pick(qcols, ['ask','best_ask'])
        if c_q_ts and c_q_sym and c_bid and c_ask and c_sym and c_tin and c_pin:
            # Build simple last-known map per symbol by string time key
            cur.execute(f"SELECT {c_q_sym} sym, {c_q_ts} ts, {c_bid} bid, {c_ask} ask FROM {quotes_tbl}")
            qrows = cur.fetchall()
            symmap = defaultdict(list)
            for q in qrows:
                symmap[q['sym']].append((str(q['ts']), float(q['bid'] or 0), float(q['ask'] or 0)))
            for s in symmap: symmap[s].sort(key=lambda x: x[0])

            def last_before(sym, t):
                arr = symmap.get(sym, [])
                lo, hi, best = 0, len(arr)-1, None
                key = str(t)
                while lo <= hi:
                    mid = (lo+hi)//2
                    if arr[mid][0] <= key:
                        best = arr[mid]; lo = mid+1
                    else:
                        hi = mid-1
                return best

            checked = within = mid_or_better = 0
            tol = float(cfg['execution']['tolerance'])
            for r in rows:
                sym = r[c_sym] if c_sym else None
                ts = r[c_tin] if c_tin else None
                px = float(r[c_pin] or 0) if c_pin else None
                if not (sym and ts and px is not None):
                    continue
                q = last_before(sym, ts)
                if not q: continue
                bid, ask = q[1], q[2]
                checked += 1
                if (px >= bid - tol) and (px <= ask + tol):
                    within += 1
                else:
                    nbbo_outliers.append({'symbol': sym, 'time': str(ts), 'price': px, 'bid': bid, 'ask': ask})
                if ask >= bid:
                    mid = (bid+ask)/2.0
                    if px >= mid: mid_or_better += 1
            nbbo_stats = {
                'checked': checked,
                'within': within,
                'pct_within': round(100.0*within/checked,2) if checked else None,
                'pct_mid_or_better': round(100.0*mid_or_better/checked,2) if checked else None
            }

    # Slippage stress
    def pf_with_slip(cents):
        day = defaultdict(float)
        for r in rows:
            entry_dt = to_dt(r[c_tin]) if c_tin else None
            if not entry_dt: continue
            qty = float(r[c_qty] or 0) if c_qty else 0.0
            mult = float(r[c_mult] or 100) if c_mult else 100.0
            pin = float(r[c_pin] or 0) if c_pin else 0.0
            pout= float(r[c_pout] or 0) if c_pout else 0.0
            fees= float(r[c_fees] or 0) if c_fees else 0.0
            realized = r[c_real] if c_real in r.keys() else None
            if realized is None:
                realized = (pout - pin) * qty * mult
            else:
                try: realized = float(realized)
                except: realized = 0.0
            slip = cents * abs(qty) * mult
            day[entry_dt.date()] += (realized - fees - slip)
        series = [v for _,v in sorted(day.items())]
        pf = profit_factor(series)
        return {'pf': round(pf,2) if pf else None, 'net_sum': round(sum(series),2)}

    slip_5 = pf_with_slip(0.05)
    slip_10= pf_with_slip(0.10)

    # Decision
    th = cfg['thresholds']
    decision = 'APPROVE'
    reasons = []
    if len(guardrail_breaches) > th['guardrail_breaches_max']:
        decision = 'REJECT'; reasons.append('Guardrail breaches present')
    if nbbo_stats:
        if nbbo_stats['pct_within'] is not None and nbbo_stats['pct_within'] < th['nbbo_within_pct']:
            decision = 'REJECT'; reasons.append('NBBO coverage below threshold')
        if nbbo_stats['pct_mid_or_better'] is not None and nbbo_stats['pct_mid_or_better'] > th['mid_rate_max_pct']:
            decision = 'REJECT'; reasons.append('Mid-or-better rate too high (unrealistic fills)')
    if slip_5['pf'] is not None and slip_5['pf'] < th['pf_5c_min']:
        decision = 'REJECT'; reasons.append('PF under $0.05 slippage below threshold')
    if slip_10['pf'] is not None and slip_10['pf'] < th['pf_10c_min']:
        decision = 'REJECT'; reasons.append('PF under $0.10 slippage below threshold')

    summary = {
        'db': args.db,
        'trades_table': trades_tbl,
        'quotes_table': quotes_tbl,
        'date_generated': datetime.utcnow().isoformat()+ 'Z',
        'thresholds': th,
        'guardrail_breach_count': len(guardrail_breaches),
        'nbbo_stats': nbbo_stats,
        'slippage_pf': {'5c': slip_5, '10c': slip_10},
        'decision': decision,
        'reasons': reasons
    }

    json_path = os.path.join(args.outdir, 'pm212_prepaper_summary.json')
    with open(json_path, 'w') as f:
        json.dump(summary, f, indent=2)

    if guardrail_breaches:
        with open(os.path.join(args.outdir, 'breaches.csv'), 'w') as f:
            f.write('date,net_pnl,allowed,streak\n')
            for b in guardrail_breaches:
                f.write(f"{b['date']},{b['net_pnl']},{b['allowed']},{b['streak']}\n")

    if nbbo_stats and len(nbbo_outliers)>0:
        with open(os.path.join(args.outdir, 'nbbo_outliers_sample.csv'), 'w') as f:
            f.write('symbol,time,price,bid,ask\n')
            for o in nbbo_outliers[:500]:
                f.write(f"{o['symbol']},{o['time']},{o['price']},{o['bid']},{o['ask']}\n")

    md = []
    md.append(f"# PM212 Pre‑Paper Audit — {os.path.basename(args.db)}")
    md.append("")
    md.append(f"**Decision:** {decision}")
    if reasons:
        md.append("**Reasons:** " + "; ".join(reasons))
    md.append("")
    md.append("## Summary")
    md.append("```")
    md.append(json.dumps(summary, indent=2))
    md.append("```")
    if guardrail_breaches:
        md.append("")
        md.append(f"Guardrail breaches: {len(guardrail_breaches)} (see breaches.csv)")
    if nbbo_stats and nbbo_outliers:
        md.append(f"NBBO outliers sample: {min(len(nbbo_outliers),500)} rows in nbbo_outliers_sample.csv")

    with open(os.path.join(args.outdir, 'pm212_prepaper_report.md'), 'w') as f:
        f.write('\n'.join(md))

    print(json.dumps(summary, indent=2))

if __name__ == '__main__':
    main()
