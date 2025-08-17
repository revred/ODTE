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
    private readonly IRegimeScorer _scorer;
    private readonly ISpreadBuilder _builder;
    private readonly IExecutionEngine _exec;
    private readonly IRiskManager _risk;

    public Backtester(
        SimConfig cfg,
        IMarketData md,
        IOptionsData od,
        IEconCalendar cal,
        IRegimeScorer scorer,
        ISpreadBuilder builder,
        IExecutionEngine exec,
        IRiskManager risk)
    {
        _cfg = cfg; _md = md; _od = od; _cal = cal;
        _scorer = scorer; _builder = builder; _exec = exec; _risk = risk;
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
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var pos = active[i];
                var quotes = _od.GetQuotesAt(bar.Ts)
                    .Where(q => q.Expiry == _od.TodayExpiry(bar.Ts))
                    .ToList();

                double shortMid = quotes
                    .Where(q => q.Right == pos.Order.Short.Right &&
                           Math.Abs(q.Strike - pos.Order.Short.Strike) < 1e-6)
                    .Select(q => q.Mid)
                    .FirstOrDefault();

                double longMid = quotes
                    .Where(q => q.Right == pos.Order.Long.Right &&
                           Math.Abs(q.Strike - pos.Order.Long.Strike) < 1e-6)
                    .Select(q => q.Mid)
                    .FirstOrDefault();

                double spreadVal = Math.Max(0, shortMid - longMid);

                double shortDelta = quotes
                    .Where(q => q.Right == pos.Order.Short.Right &&
                           Math.Abs(q.Strike - pos.Order.Short.Strike) < 1e-6)
                    .Select(q => q.Delta)
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
                for (int i = active.Count - 1; i >= 0; i--)
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

                // DEBUG: Log regime scoring
                Console.WriteLine($"🎯 {bar.Ts:yyyy-MM-dd HH:mm} | Score: {score}, Calm: {calm}, Up: {up}, Dn: {dn}");

                Decision d = Decision.NoGo;
                if (score <= -1)
                {
                    d = Decision.NoGo;
                    Console.WriteLine($"   ❌ NoGo - Score too low: {score}");
                }
                else if (calm && score >= 0)
                {
                    d = Decision.Condor;
                    Console.WriteLine($"   🎪 Condor - Calm market, score: {score}");
                }
                else if (up && score >= 2)
                {
                    d = Decision.SingleSideCall;
                    Console.WriteLine($"   📈 Call spread - Uptrend, score: {score}");
                }
                else if (dn && score >= 2)
                {
                    d = Decision.SingleSidePut;
                    Console.WriteLine($"   📉 Put spread - Downtrend, score: {score}");
                }
                else
                {
                    Console.WriteLine($"   ⏸️ NoGo - No clear signal, score: {score}");
                }

                if (d != Decision.NoGo)
                {
                    if (!_risk.CanAdd(bar.Ts, d))
                    {
                        Console.WriteLine($"   🚫 Risk blocked trade");
                    }
                    else
                    {
                        var order = _builder.TryBuild(bar.Ts, d, _md, _od);
                        if (order == null)
                        {
                            Console.WriteLine($"   ❌ Order build failed");
                        }
                        else
                        {
                            var pos = _exec.TryEnter(order);
                            if (pos == null)
                            {
                                Console.WriteLine($"   ❌ Execution failed");
                            }
                            else
                            {
                                Console.WriteLine($"   ✅ Trade executed!");
                                active.Add(pos);
                                _risk.RegisterOpen(d);
                            }
                        }
                    }
                }
            }
        }

        // Force-close any remaining active positions at end of simulation (0DTE expiry)
        if (active.Count > 0)
        {
            DateTime finalTime = bars.LastOrDefault()?.Ts ?? _cfg.End.ToDateTime(new TimeOnly(21, 0, 0));

            foreach (var pos in active.Where(p => !p.Closed))
            {
                // For 0DTE options, assume they expire worthless if not closed
                pos.Closed = true;
                pos.ExitPrice = 0.01; // Minimal value - options expire worthless 
                pos.ExitTs = finalTime;
                pos.ExitReason = "Expiry - 0DTE options expire worthless";

                double fees = 2.0 * ((double)_cfg.Fees.CommissionPerContract + (double)_cfg.Fees.ExchangeFeesPerContract);
                double pnl = (pos.EntryPrice - pos.ExitPrice.Value) * 100 - fees;

                report.Trades.Add(new TradeResult(
                    pos, pnl, fees,
                    0.01 - pos.EntryPrice,  // MAE - assume minimum adverse movement
                    pos.EntryPrice - 0.01   // MFE - maximum favorable was entry price
                ));

                // Register position closure with risk manager to free up position slots
                _risk.RegisterClose(pos.Order.Type, pnl);

                Console.WriteLine($"   📅 Position force-closed at expiry: P&L ${pnl:F2}");
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