using ODTE.Backtest.Config;
using ODTE.Backtest.Core;
using ODTE.Backtest.Data;
using ODTE.Backtest.Signals;
using ODTE.Backtest.Strategy;

namespace ODTE.Backtest.Engine;

public sealed class Backtester
{
    private readonly SimConfig _cfg; 
    private readonly IMarketData _md; 
    private readonly IOptionsData _od; 
    private readonly IEconCalendar _cal;
    private readonly RegimeScorer _scorer; 
    private readonly SpreadBuilder _builder; 
    private readonly ExecutionEngine _exec; 
    private readonly RiskManager _risk;

    public Backtester(
        SimConfig cfg, 
        IMarketData md, 
        IOptionsData od, 
        IEconCalendar cal, 
        RegimeScorer scorer, 
        SpreadBuilder builder, 
        ExecutionEngine exec, 
        RiskManager risk)
    { 
        _cfg=cfg; _md=md; _od=od; _cal=cal; 
        _scorer=scorer; _builder=builder; _exec=exec; _risk=risk; 
    }

    public Task<RunReport> RunAsync()
    {
        var report = new RunReport();
        var bars = _md.GetBars(_cfg.Start, _cfg.End).ToList();
        var active = new List<OpenPosition>();
        DateTime? lastDecision = null;

        foreach (var bar in bars.Where(b => InSession(b.Ts)))
        {
            // 1) Update open positions
            for (int i=active.Count-1; i>=0; i--)
            {
                var pos = active[i];
                var quotes = _od.GetQuotesAt(bar.Ts)
                    .Where(q => q.Expiry == _od.TodayExpiry(bar.Ts))
                    .ToList();
                
                double shortMid = quotes
                    .Where(q => q.Right==pos.Order.Short.Right && 
                           Math.Abs(q.Strike - pos.Order.Short.Strike) < 1e-6)
                    .Select(q=>q.Mid)
                    .FirstOrDefault();
                
                double longMid = quotes
                    .Where(q => q.Right==pos.Order.Long.Right && 
                           Math.Abs(q.Strike - pos.Order.Long.Strike) < 1e-6)
                    .Select(q=>q.Mid)
                    .FirstOrDefault();
                
                double spreadVal = Math.Max(0, shortMid - longMid);
                
                double shortDelta = quotes
                    .Where(q => q.Right==pos.Order.Short.Right && 
                           Math.Abs(q.Strike - pos.Order.Short.Strike) < 1e-6)
                    .Select(q=>q.Delta)
                    .FirstOrDefault();

                var (exit, price, reason) = _exec.ShouldExit(pos, spreadVal, shortDelta, bar.Ts);
                if (exit)
                {
                    pos.Closed = true; 
                    pos.ExitPrice = price; 
                    pos.ExitTs = bar.Ts; 
                    pos.ExitReason = reason;
                    
                    double fees = 2 * (_cfg.Fees.CommissionPerContract + _cfg.Fees.ExchangeFeesPerContract); 
                    double pnl = (pos.EntryPrice - price) * 100 - fees; 
                    
                    report.Trades.Add(new TradeResult(
                        pos, pnl, fees, 
                        spreadVal - pos.EntryPrice, 
                        pos.EntryPrice - spreadVal));
                    
                    _risk.RegisterClose(pos.Order.Type, pnl);
                    active.RemoveAt(i);
                }
            }

            // 2) Expiry cash settlement for safe positions
            if (IsPmClose(bar.Ts))
            {
                for (int i=active.Count-1; i>=0; i--)
                {
                    var pos = active[i];
                    double price = 0.0; 
                    pos.Closed = true; 
                    pos.ExitPrice = price; 
                    pos.ExitTs = bar.Ts; 
                    pos.ExitReason = "PM cash settlement";
                    
                    double fees = _cfg.Fees.CommissionPerContract + _cfg.Fees.ExchangeFeesPerContract; 
                    double pnl = (pos.EntryPrice - price) * 100 - fees;
                    
                    report.Trades.Add(new TradeResult(
                        pos, pnl, fees, 0, pos.EntryPrice));
                    
                    _risk.RegisterClose(pos.Order.Type, pnl);
                    active.RemoveAt(i);
                }
            }

            // 3) New decision at cadence
            if (ShouldDecide(bar.Ts, lastDecision))
            {
                lastDecision = bar.Ts;
                var (score, calm, up, dn) = _scorer.Score(bar.Ts, _md, _cal);
                
                Decision d = Decision.NoGo;
                if (score <= -1) 
                    d = Decision.NoGo;
                else if (calm && score >= 0) 
                    d = Decision.Condor;
                else if (up && score >= 2) 
                    d = Decision.SingleSideCall;
                else if (dn && score >= 2) 
                    d = Decision.SingleSidePut;

                if (d != Decision.NoGo && _risk.CanAdd(bar.Ts, d))
                {
                    var order = _builder.TryBuild(bar.Ts, d, _md, _od);
                    if (order != null)
                    {
                        var pos = _exec.TryEnter(order);
                        if (pos != null)
                        {
                            active.Add(pos);
                            _risk.RegisterOpen(d);
                        }
                    }
                }
            }
        }

        CalculateMetrics(report);
        return Task.FromResult(report);
    }

    private bool InSession(DateTime ts)
    {
        var t = ts.TimeOfDay;
        var sessionStart = new TimeSpan(14, 30, 0); 
        var sessionEnd = new TimeSpan(21, 0, 0);   
        return t >= sessionStart && t <= sessionEnd;
    }

    private bool IsPmClose(DateTime ts)
    {
        var t = ts.TimeOfDay;
        return t >= new TimeSpan(20, 59, 0) && t <= new TimeSpan(21, 1, 0);
    }

    private bool ShouldDecide(DateTime ts, DateTime? lastDecision)
    {
        if (lastDecision == null) return true;
        return (ts - lastDecision.Value).TotalSeconds >= _cfg.CadenceSeconds;
    }

    private void CalculateMetrics(RunReport report)
    {
        if (report.Trades.Count == 0) return;

        var dailyPnL = report.Trades
            .GroupBy(t => DateOnly.FromDateTime(t.Pos.EntryTs))
            .Select(g => g.Sum(t => t.PnL))
            .ToList();

        if (dailyPnL.Count > 0)
        {
            double avgDaily = dailyPnL.Average();
            double stdDaily = Math.Sqrt(dailyPnL.Select(p => Math.Pow(p - avgDaily, 2)).Average());
            report.Sharpe = stdDaily > 0 ? (avgDaily / stdDaily) * Math.Sqrt(252) : 0;
        }

        double cumPnL = 0;
        double peak = 0;
        double maxDD = 0;

        foreach (var trade in report.Trades.OrderBy(t => t.Pos.EntryTs))
        {
            cumPnL += trade.PnL;
            peak = Math.Max(peak, cumPnL);
            double dd = peak - cumPnL;
            maxDD = Math.Max(maxDD, dd);
        }

        report.MaxDrawdown = maxDD;
    }
}