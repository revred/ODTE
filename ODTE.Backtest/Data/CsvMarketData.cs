using CsvHelper;
using CsvHelper.Configuration;
using ODTE.Backtest.Core;
using System.Globalization;

namespace ODTE.Backtest.Data;

public sealed class CsvMarketData : IMarketData
{
    private readonly List<Bar> _bars;
    private readonly TimeSpan _barInt;
    private readonly string _tz;
    private readonly bool _rthOnly;

    public CsvMarketData(string path, string timezone, bool rthOnly)
    {
        _tz = timezone;
        _rthOnly = rthOnly;

        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true });

        _bars = csv.GetRecords<BarCsv>()
            .Select(r => new Bar(
                DateTime.Parse(r.ts).ToUtc(),
                r.o, r.h, r.l, r.c, r.v))
            .OrderBy(b => b.Ts)
            .ToList();

        _barInt = _bars.Count > 1 ? _bars[1].Ts - _bars[0].Ts : TimeSpan.FromMinutes(1);
    }

    public IEnumerable<Bar> GetBars(DateOnly start, DateOnly end)
        => _bars.Where(b => DateOnly.FromDateTime(b.Ts) >= start && DateOnly.FromDateTime(b.Ts) <= end)
                .Where(b => !_rthOnly || b.Ts.IsRth());

    public TimeSpan BarInterval => _barInt;

    public double Atr20Minutes(DateTime ts)
    {
        var idx = _bars.FindIndex(b => b.Ts == ts);
        if (idx < 20) return 0;

        var window = _bars.Skip(idx - 20).Take(20).ToList();
        double trSum = 0;

        for (int i = 1; i < window.Count; i++)
        {
            var prevC = window[i - 1].C;
            var cur = window[i];
            var tr = Math.Max(cur.H - cur.L, Math.Max(Math.Abs(cur.H - prevC), Math.Abs(cur.L - prevC)));
            trSum += tr;
        }

        return trSum / 19.0;
    }

    public double Vwap(DateTime now, TimeSpan window)
    {
        var from = now - window;
        var slice = _bars.Where(b => b.Ts > from && b.Ts <= now).ToList();
        double pv = slice.Sum(b => b.C * b.V);
        double v = slice.Sum(b => b.V);
        return v > 0 ? pv / v : slice.LastOrDefault()?.C ?? 0;
    }

    public double GetSpot(DateTime ts)
    {
        var bars = GetBars(DateOnly.FromDateTime(ts.Date), DateOnly.FromDateTime(ts.Date))
                      .Where(b => b.Ts <= ts).ToList();
        return bars.Count > 0 ? bars[^1].C : 0;
    }

    private sealed class BarCsv
    {
        public string ts { get; set; } = "";
        public double o { get; set; }
        public double h { get; set; }
        public double l { get; set; }
        public double c { get; set; }
        public double v { get; set; }
    }
}