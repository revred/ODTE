# ODTE Backtest Engine (C#) — 18‑Month Replay Scaffold (Annotated)

> This version adds **deep inline comments** and **learning links (articles/videos)** so you understand *why* each module exists and *how* it maps to the 0DTE/1DTE strategy we discussed. Links are placed in code comments and below each section.

This repo scaffold replays the last 18 months and iterates the 0DTE/1DTE strategy with:

- **Track B (Prototype)**: Synthetic options from SPX/ES + VIX/VIX9D proxies (fast + free).
- **Track A (Pro‑grade)**: Drop‑in adapters for ORATS / LiveVol / dxFeed (real intraday chains + Greeks).
- Regime scoring (OR/VWAP/ATR, event windows), credit/width filters, slippage/fee model, cash settlement, YAML config, CSV reports.

Copy files into a new folder `ODTE.Backtest` and follow README below.

---

## File tree

```
ODTE.Backtest/
  ODTE.Backtest.csproj
  Program.cs
  appsettings.yaml
  /Config/SimConfig.cs
  /Core/Types.cs
  /Core/OptionMath.cs
  /Core/Utils.cs
  /Data/IMarketData.cs
  /Data/IOptionsData.cs
  /Data/IEconCalendar.cs
  /Data/CsvMarketData.cs
  /Data/CsvCalendar.cs
  /Data/SyntheticOptionsData.cs
  /Signals/RegimeScorer.cs
  /Strategy/SpreadBuilder.cs
  /Engine/ExecutionEngine.cs
  /Engine/RiskManager.cs
  /Engine/Backtester.cs
  /Reporting/Reporter.cs
  /Docs/README.md
  /Docs/RulesCard.md
  /Samples/bars_spx_min.csv
  /Samples/vix_daily.csv
  /Samples/vix9d_daily.csv
  /Samples/calendar.csv
```

---

## ODTE.Backtest.csproj

```xml
<!-- Why: .NET 8 console app with two helper libs for config & CSV.
     CsvHelper — robust CSV parsing; YamlDotNet — human‑tweakable configs. -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="YamlDotNet" Version="13.7.1" />
    <PackageReference Include="CsvHelper" Version="30.0.1" />
  </ItemGroup>
</Project>
```

**Learn more**

- CsvHelper quickstart: [https://joshclose.github.io/CsvHelper/](https://joshclose.github.io/CsvHelper/)
- YAML in .NET: [https://github.com/aaubry/YamlDotNet](https://github.com/aaubry/YamlDotNet)

---

## appsettings.yaml (sample)

```yaml
# Why: All knobs in one place so you can grid‑search later.
# Key ideas: keep per‑trade risk small, respect event windows, throttle by realized vol.

underlying: XSP             # or SPX (XSP is 1/10th SPX; cash‑settled, European)
start: 2024-02-01
end: 2025-07-31
mode: prototype             # prototype | pro
rth_only: true
timezone: Europe/London

cadence_seconds: 900        # decisions every 15m in backtest (tighten when confident)
no_new_risk_minutes_to_close: 40  # gamma hour rule

# strike/credit/width defaults (tunable per regime)
short_delta:
  condor_min: 0.07          # calmer → further OTM
  condor_max: 0.15
  single_min: 0.10          # trend days → slightly closer OTM
  single_max: 0.20
width_points:
  min: 1
  max: 2
credit_per_width_min:
  condor: 0.18              # don’t sell pennies
  single: 0.20

stops:
  credit_multiple: 2.2      # exit when spread value >= 2.2x entry credit
  delta_breach: 0.33        # or when short‑strike delta breaches ~33∆ (gamma risk)

risk:
  daily_loss_stop: 500      # ties to your $500/day cap
  per_trade_max_loss_cap: 200
  max_concurrent_per_side: 2

slippage:
  entry_half_spread_ticks: 0.5
  exit_half_spread_ticks: 0.5
  late_session_extra_ticks: 0.5
  tick_value: 0.05         # $0.05 per option tick
  spread_pct_cap: 0.25     # quoted spread ≤ 25% of credit

fees:
  commission_per_contract: 0.65
  exchange_fees_per_contract: 0.25

signals:
  or_minutes: 15            # Opening Range length
  vwap_window_minutes: 30   # VWAP regime window
  atr_period_bars: 20       # ATR(20) on minute bars
  calm_iv_condition: short_leq_30d  # VIX9D <= VIX → calmer near‑term vol
  event_block_minutes_before: 60
  event_block_minutes_after: 15

throttle:
  rv_high_cadence_seconds: 1800  # slow down when tape is wild
  rv_low_cadence_seconds: 600    # speed up when tape is calm

paths:
  bars_csv: ./Samples/bars_spx_min.csv
  vix_csv: ./Samples/vix_daily.csv
  vix9d_csv: ./Samples/vix9d_daily.csv
  calendar_csv: ./Samples/calendar.csv
  reports_dir: ./Reports
```

**Learn more**

- SPX/SPXW specs (PM settlement, cash): [https://www.cboe.com/tradable\_products/sp\_500/spx\_options/specifications/](https://www.cboe.com/tradable_products/sp_500/spx_options/specifications/)
- XSP (Mini‑SPX) basics (cash‑settled, European): [https://www.cboe.com/tradable\_products/sp\_500/mini\_spx\_options/](https://www.cboe.com/tradable_products/sp_500/mini_spx_options/)
- XSP advantages (cash settlement, no early exercise): [https://www.cboe.com/tradable\_products/sp\_500/mini\_spx\_options/cash\_settlement/](https://www.cboe.com/tradable_products/sp_500/mini_spx_options/cash_settlement/)
- OCC options risk booklet (ODD): [https://www.theocc.com/getmedia/a151a9ae-d784-4a15-bdeb-23a029f50b70/riskstoc.pdf](https://www.theocc.com/getmedia/a151a9ae-d784-4a15-bdeb-23a029f50b70/riskstoc.pdf)

---

## Program.cs

```csharp
// WHY: Composition root. Loads YAML, wires data providers + engines, runs the backtest, writes reports.
// You can swap the options data source by changing SimConfig.Mode (prototype → pro) and plugging an adapter.
//
// Helpful references:
// - IBKR TWS API (for live/paper later): https://interactivebrokers.github.io/tws-api/classIBApi_1_1EClientSocket.html
// - Client Portal (REST/WebSocket): https://www.interactivebrokers.com/campus/ibkr-api-page/cpapi-v1/

using ODTE.Backtest.Config;
using ODTE.Backtest.Engine;
using ODTE.Backtest.Reporting;
using ODTE.Backtest.Data;
using ODTE.Backtest.Signals;
using ODTE.Backtest.Strategy;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ODTE.Backtest;

public class Program
{
    public static async Task Main(string[] args)
    {
        string cfgPath = args.FirstOrDefault(a => a.EndsWith(".yaml")) ?? "appsettings.yaml";
        var cfg = LoadConfig(cfgPath);

        Directory.CreateDirectory(cfg.Paths.ReportsDir);

        // Data providers (swap here)
        IMarketData market = new CsvMarketData(cfg.Paths.BarsCsv, cfg.Timezone, cfg.RthOnly);
        IEconCalendar econ = new CsvCalendar(cfg.Paths.CalendarCsv, cfg.Timezone);
        IOptionsData options = cfg.Mode.Equals("prototype", StringComparison.OrdinalIgnoreCase)
            ? new SyntheticOptionsData(cfg, market, cfg.Paths.VixCsv, cfg.Paths.Vix9dCsv)
            : throw new NotImplementedException("Pro‑grade adapter: plug ORATS/LiveVol/dxFeed here.");

        // Engines
        var scorer = new RegimeScorer(cfg);    // OR/VWAP/ATR/event → Go/No‑Go
        var builder = new SpreadBuilder(cfg);  // Converts decisions into candidate spreads
        var exec = new ExecutionEngine(cfg);   // Entry/exit price modelling & stops
        var risk = new RiskManager(cfg);       // Daily hard stop, concurrency caps, time windows
        var backtester = new Backtester(cfg, market, options, econ, scorer, builder, exec, risk);

        var report = await backtester.RunAsync();
        Reporter.WriteSummary(cfg, report);
        Reporter.WriteTrades(cfg, report);

        Console.WriteLine("
Done. Reports at: " + cfg.Paths.ReportsDir);
    }

    static SimConfig LoadConfig(string path)
    {
        var yaml = File.ReadAllText(path);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        return deserializer.Deserialize<SimConfig>(yaml);
    }
}
```

**Learn more**

- Data vendors for *pro* mode:
  - ORATS 1‑minute intraday options: [https://orats.com/one-minute-data](https://orats.com/one-minute-data)
  - ORATS Intraday Data API: [https://orats.com/intraday-data-api](https://orats.com/intraday-data-api)
  - Cboe DataShop (LiveVol): [https://datashop.cboe.com/](https://datashop.cboe.com/)
  - dxFeed historical/replay: [https://www.livevol.com/stock-options-analysis-data/](https://www.livevol.com/stock-options-analysis-data/)

---

## /Config/SimConfig.cs

```csharp
// WHY: Centralized, strongly‑typed config so the strategy is data‑driven and tunable.
// Design: Delta bands & credit/width floors gate entries; stops are both price‑based (k×credit) and risk‑based (Δ breach).
// Rationale: On 0DTE, gamma amplifies small moves near your short strike; Δ‑based exits help avoid “death by gamma”.
// GAMMA/DELTA primer: Black–Scholes model + greeks overview → https://en.wikipedia.org/wiki/Black%E2%80%93Scholes_model

namespace ODTE.Backtest.Config;

public sealed class SimConfig
{
    public string Underlying { get; set; } = "XSP";    // Mini‑SPX for small, repeatable risk sizing
    public DateOnly Start { get; set; }
    public DateOnly End { get; set; }
    public string Mode { get; set; } = "prototype";    // swap to pro when wiring a vendor adapter
    public bool RthOnly { get; set; } = true;           // RTH avoids overnight noise
    public string Timezone { get; set; } = "Europe/London";

    public int CadenceSeconds { get; set; } = 900;      // decision cadence; throttle by regime
    public int NoNewRiskMinutesToClose { get; set; } = 40; // gamma‑hour guardrail

    public ShortDeltaCfg ShortDelta { get; set; } = new();
    public WidthPointsCfg WidthPoints { get; set; } = new();
    public CreditPerWidthCfg CreditPerWidthMin { get; set; } = new();

    public StopsCfg Stops { get; set; } = new();
    public RiskCfg Risk { get; set; } = new();
    public SlippageCfg Slippage { get; set; } = new();
    public FeesCfg Fees { get; set; } = new();
    public SignalsCfg Signals { get; set; } = new();
    public ThrottleCfg Throttle { get; set; } = new();
    public PathsCfg Paths { get; set; } = new();
}

public sealed class ShortDeltaCfg { public double CondorMin { get; set; } = 0.07; public double CondorMax { get; set; } = 0.15; public double SingleMin { get; set; } = 0.10; public double SingleMax { get; set; } = 0.20; }
public sealed class WidthPointsCfg { public int Min { get; set; } = 1; public int Max { get; set; } = 2; }
public sealed class CreditPerWidthCfg { public double Condor { get; set; } = 0.18; public double Single { get; set; } = 0.20; }

public sealed class StopsCfg { public double CreditMultiple { get; set; } = 2.2; public double DeltaBreach { get; set; } = 0.33; }
public sealed class RiskCfg { public double DailyLossStop { get; set; } = 500; public double PerTradeMaxLossCap { get; set; } = 200; public int MaxConcurrentPerSide { get; set; } = 2; }
public sealed class SlippageCfg { public double EntryHalfSpreadTicks { get; set; } = 0.5; public double ExitHalfSpreadTicks { get; set; } = 0.5; public double LateSessionExtraTicks { get; set; } = 0.5; public double TickValue { get; set; } = 0.05; public double SpreadPctCap { get; set; } = 0.25; }
public sealed class FeesCfg { public double CommissionPerContract { get; set; } = 0.65; public double ExchangeFeesPerContract { get; set; } = 0.25; }

public sealed class SignalsCfg
{
    public int OrMinutes { get; set; } = 15; public int VwapWindowMinutes { get; set; } = 30; public int AtrPeriodBars { get; set; } = 20; public string CalmIvCondition { get; set; } = "short_leq_30d";
    public int EventBlockMinutesBefore { get; set; } = 60; public int EventBlockMinutesAfter { get; set; } = 15;
}

public sealed class ThrottleCfg { public int RvHighCadenceSeconds { get; set; } = 1800; public int RvLowCadenceSeconds { get; set; } = 600; }

public sealed class PathsCfg
{ public string BarsCsv { get; set; } = "./Samples/bars_spx_min.csv"; public string VixCsv { get; set; } = "./Samples/vix_daily.csv"; public string Vix9dCsv { get; set; } = "./Samples/vix9d_daily.csv"; public string CalendarCsv { get; set; } = "./Samples/calendar.csv"; public string ReportsDir { get; set; } = "./Reports"; }
```

**Learn more**

- XSP basics (mini, cash‑settled, European): [https://www.cboe.com/tradable\_products/sp\_500/mini\_spx\_options/](https://www.cboe.com/tradable_products/sp_500/mini_spx_options/)
- SPXW PM settlement mechanics: [https://www.cboe.com/tradable\_products/sp\_500/spx\_options/specifications/](https://www.cboe.com/tradable_products/sp_500/spx_options/specifications/)
- Greeks refresher (∆/Γ): [https://en.wikipedia.org/wiki/Black%E2%80%93Scholes\_model](https://en.wikipedia.org/wiki/Black%E2%80%93Scholes_model)

---

## /Core/Types.cs

```csharp
// WHY: Small immutable records keep the sim transparent.
// Decision — scanner output → what to build.
// OptionQuote — single snapshot of a strike; Mid is used for pricing in the prototype.

namespace ODTE.Backtest.Core;

public enum Decision { NoGo, Condor, SingleSidePut, SingleSideCall }
public enum Right { Call, Put }

public record Bar(DateTime Ts, double O, double H, double L, double C, double V);
public record EconEvent(DateTime Ts, string Kind);

public record OptionQuote(DateTime Ts, DateOnly Expiry, double Strike, Right Right, double Bid, double Ask, double Mid, double Delta, double Iv);

public record SpreadLeg(DateOnly Expiry, double Strike, Right Right, int Ratio);
public record SpreadOrder(DateTime Ts, string Underlying, double Credit, double Width, double CreditPerWidth, Decision Type,
    SpreadLeg Short, SpreadLeg Long);

public record Fill(DateTime Ts, double Price, string Reason);

public record OpenPosition(SpreadOrder Order, double EntryPrice, DateTime EntryTs)
{
    public bool Closed { get; set; }
    public double? ExitPrice { get; set; }
    public DateTime? ExitTs { get; set; }
    public string ExitReason { get; set; } = string.Empty;
}

public record TradeResult(OpenPosition Pos, double PnL, double Fees, double MaxAdverseExcursion, double MaxFavorableExcursion);

public sealed class RunReport
{
    public List<TradeResult> Trades { get; } = new();
    public double GrossPnL => Trades.Sum(t => t.PnL);
    public double Fees => Trades.Sum(t => t.Fees);
    public double NetPnL => GrossPnL - Fees;
}
```

---

## /Core/OptionMath.cs

```csharp
// WHY: Minimal Black‑Scholes helpers for the prototype.
// We need Delta & a fair‑value Price to synthesize quotes when we don’t have vendor Greeks.
// Limitations: Assumes European options, constant vol, continuous yield (q). Good enough for gating, not for production pricing.
// References:
// - Black–Scholes model (math & greeks): https://en.wikipedia.org/wiki/Black%E2%80%93Scholes_model

using System;

namespace ODTE.Backtest.Core;

public static class OptionMath
{
    public static double D1(double S, double K, double r, double q, double sigma, double T)
        => (Math.Log(S/K) + (r - q + 0.5*sigma*sigma)*T) / (sigma*Math.Sqrt(T));
    public static double D2(double d1, double sigma, double T) => d1 - sigma*Math.Sqrt(T);

    public static double Nd(double x) => 0.5 * (1.0 + Erf(x / Math.Sqrt(2.0))); // CDF
    public static double nd(double x) => Math.Exp(-0.5*x*x) / Math.Sqrt(2*Math.PI); // PDF

    public static double Delta(double S, double K, double r, double q, double sigma, double T, Right right)
    {
        var d1 = D1(S,K,r,q,sigma,T);
        return right == Right.Call ? Math.Exp(-q*T)*Nd(d1) : -Math.Exp(-q*T)*Nd(-d1);
    }

    public static double Price(double S, double K, double r, double q, double sigma, double T, Right right)
    {
        var d1 = D1(S,K,r,q,sigma,T); var d2 = D2(d1, sigma, T);
        if (right == Right.Call)
            return Math.Exp(-q*T)*S*Nd(d1) - Math.Exp(-r*T)*K*Nd(d2);
        else
            return Math.Exp(-r*T)*K*Nd(-d2) - Math.Exp(-q*T)*S*Nd(-d1);
    }

    // Numerical erf — simple, fast; sufficient for this backtest.
    private static double Erf(double x)
    {
        double t = 1.0/(1.0+0.5*Math.Abs(x));
        double tau = t*Math.Exp(-x*x - 1.26551223 + t*(1.00002368 + t*(0.37409196 + t*(0.09678418 + t*(-0.18628806 + t*(0.27886807 + t*(-1.13520398 + t*(1.48851587 + t*(-0.82215223 + t*0.17087277)))))))));
        return x>=0 ? 1.0 - tau : tau - 1.0;
    }
}
```

---

## /Core/Utils.cs

```csharp
namespace ODTE.Backtest.Core;

public static class Utils
{
    // Simple clamp helper; used in a few places when we cap ratios.
    public static double Clamp(this double v, double lo, double hi) => Math.Max(lo, Math.Min(hi, v));
}
```

---

## /Data/IMarketData.cs

```csharp
// WHY: Abstracts time‑series bars (SPX/ES) so we can swap CSV → database → live feed.
// Provides ATR and VWAP used by the regime scorer.
// References:
// - ATR explainer: https://www.investopedia.com/articles/trading/08/average-true-range.asp
// - VWAP explainer: https://www.nasdaq.com/glossary/v/vwap

using ODTE.Backtest.Core;

namespace ODTE.Backtest.Data;

public interface IMarketData
{
    IEnumerable<Bar> GetBars(DateOnly start, DateOnly end);
    TimeSpan BarInterval { get; }
    double Atr20Minutes(DateTime ts);
    double Vwap(DateTime now, TimeSpan window);
}
```

---

## /Data/IOptionsData.cs

```csharp
// WHY: Abstraction over the options chain. Prototype uses a synthetic surface; pro mode uses vendor quotes/Greeks.
// TodayExpiry(ts) returns same‑day PM expiry to mimic SPXW/XSP dailies.
// References:
// - SPXW PM settlement & dailies: https://www.cboe.com/tradable_products/sp_500/spx_options/specifications/
// - XSP overview: https://www.cboe.com/tradable_products/sp_500/mini_spx_options/

using ODTE.Backtest.Core;

namespace ODTE.Backtest.Data;

public interface IOptionsData
{
    IEnumerable<OptionQuote> GetQuotesAt(DateTime ts); // for today’s expiry (and 1DTE if needed)
    DateOnly TodayExpiry(DateTime ts);
    (double shortIv, double thirtyIv) GetIvProxies(DateTime ts);
}
```

---

## /Data/IEconCalendar.cs

```csharp
// WHY: Strategy blocks entries near macro prints (CPI/FOMC/NFP). Simple CSV adapter here; swap to API if needed.

using ODTE.Backtest.Core;

namespace ODTE.Backtest.Data;

public interface IEconCalendar
{
    EconEvent? NextEventAfter(DateTime ts);
}
```

---

## /Data/CsvMarketData.cs

```csharp
// WHY: Load minute bars from CSV and compute ATR/VWAP needed by the regime scorer.
// NOTE: RTH logic here is simplified; for production, use real timezone/DST handling (e.g., NodaTime) and exchange calendars.
// VWAP is price‑volume weighted average over the requested window.
// Learn: VWAP — https://www.investopedia.com/terms/v/vwap.asp

using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using ODTE.Backtest.Core;

namespace ODTE.Backtest.Data;

public sealed class CsvMarketData : IMarketData
{
    private readonly List<Bar> _bars;
    private readonly TimeSpan _barInt;
    private readonly string _tz;
    private readonly bool _rthOnly;

    public CsvMarketData(string path, string timezone, bool rthOnly)
    {
        _tz = timezone; _rthOnly = rthOnly;
        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true });
        _bars = csv.GetRecords<BarCsv>().Select(r => new Bar(
            DateTime.SpecifyKind(DateTime.Parse(r.ts), DateTimeKind.Utc), r.o, r.h, r.l, r.c, r.v)).OrderBy(b=>b.Ts).ToList();
        _barInt = _bars.Count > 1 ? _bars[1].Ts - _bars[0].Ts : TimeSpan.FromMinutes(1);
    }

    public IEnumerable<Bar> GetBars(DateOnly start, DateOnly end)
        => _bars.Where(b => DateOnly.FromDateTime(b.Ts) >= start && DateOnly.FromDateTime(b.Ts) <= end)
                .Where(b => !_rthOnly || IsRth(b.Ts));

    public TimeSpan BarInterval => _barInt;

    public double Atr20Minutes(DateTime ts)
    {
        // Simple ATR on minute bars over last 20 bars
        var idx = _bars.FindIndex(b => b.Ts == ts);
        if (idx < 20) return 0;
        var window = _bars.Skip(idx-20).Take(20).ToList();
        double trSum = 0;
        for (int i=1; i<window.Count; i++)
        {
            var prevC = window[i-1].C; var cur = window[i];
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

    private static bool IsRth(DateTime tsUtc)
    {
        // US RTH (ET 9:30–16:00). For a quick prototype we offset UTC→ET by −5h.
        // WARNING: This ignores DST. Replace with a proper calendar in production.
        var et = tsUtc.AddHours(-5);
        var t = et.TimeOfDay;
        return t >= new TimeSpan(9,30,0) && t <= new TimeSpan(16,0,0);
    }

    private sealed class BarCsv { public string ts { get; set; } = ""; public double o { get; set; } public double h { get; set; } public double l { get; set; } public double c { get; set; } public double v { get; set; } }
}
```

---

## /Data/CsvCalendar.cs

```csharp
// WHY: Minimal macro calendar adapter so the sim can block entries near prints.
// Upgrade path: wire an API for FOMC/CPI/NFP releases.

using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using ODTE.Backtest.Core;

namespace ODTE.Backtest.Data;

public sealed class CsvCalendar : IEconCalendar
{
    private readonly List<EconEvent> _evts;
    public CsvCalendar(string path, string timezone)
    {
        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture){ HasHeaderRecord = true });
        _evts = csv.GetRecords<Row>().Select(r => new EconEvent(DateTime.SpecifyKind(DateTime.Parse(r.ts), DateTimeKind.Utc), r.kind)).ToList();
    }
    public EconEvent? NextEventAfter(DateTime ts) => _evts.Where(e => e.Ts >= ts).OrderBy(e => e.Ts).FirstOrDefault();

    private sealed class Row { public string ts { get; set; } = ""; public string kind { get; set; } = ""; }
}
```

---

## /Data/SyntheticOptionsData.cs

```csharp
// WHY: Cheap/fast way to test scanner & risk rules without paying for intraday options data yet.
// Method: Use SPX spot to generate a strike grid; use VIX9D (short) and VIX (30‑day) to proxy implied vols.
// Smile: Put skew > Call skew to mimic equity index surfaces.
// IMPORTANT: This is a proxy. For production backtests, swap to ORATS/LiveVol/dxFeed adapters.
// References:
// - VIX (30‑day IV proxy): https://www.cboe.com/tradable_products/vix/
// - VIX9D (short‑dated IV): https://www.cboe.com/us/indices/dashboard/vix9d/
// - SPXW/XSP PM cash settlement: https://www.cboe.com/tradable_products/sp_500/spx_options/specifications/

using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using ODTE.Backtest.Core;
using ODTE.Backtest.Config;

namespace ODTE.Backtest.Data;

public sealed class SyntheticOptionsData : IOptionsData
{
    private readonly SimConfig _cfg; private readonly IMarketData _md;
    private readonly List<(DateOnly d, double vix)> _vix; private readonly List<(DateOnly d, double vix9d)> _vix9d;
    public SyntheticOptionsData(SimConfig cfg, IMarketData md, string vixPath, string vix9dPath)
    {
        _cfg = cfg; _md = md;
        _vix = Load(vixPath); _vix9d = Load(vix9dPath);
    }

    public IEnumerable<OptionQuote> GetQuotesAt(DateTime ts)
    {
        // Build a coarse smile around spot S with +/-1..10% strikes.
        var S = Spot(ts);
        if (S <= 0) yield break;
        var exp = TodayExpiry(ts);
        double T = Math.Max((exp.ToDateTime(new TimeOnly(21,0)) - ts).TotalDays/365.0, 0.0005);
        var (ivS, ivL) = GetIvProxies(ts);
        double baseIv = Math.Max(0.05, Math.Min(0.80, ivS/100.0)); // convert % → decimal

        foreach (var pct in Enumerable.Range(1, 10))
        {
            double kPut = S*(1- pct/100.0); double kCall = S*(1+ pct/100.0);
            double skewPut = baseIv * (1 + 0.10*pct/10.0); // equity put‑smile
            double skewCall = baseIv * (1 - 0.05*pct/10.0);

            var dPut = OptionMath.Delta(S, kPut, 0.00, 0.00, skewPut, T, Right.Put);
            var pPut = OptionMath.Price(S, kPut, 0.00, 0.00, skewPut, T, Right.Put);
            var dCall = OptionMath.Delta(S, kCall, 0.00, 0.00, skewCall, T, Right.Call);
            var pCall = OptionMath.Price(S, kCall, 0.00, 0.00, skewCall, T, Right.Call);

            foreach (var (right, K, d, mid) in new[]{ (Right.Put, kPut, dPut, pPut), (Right.Call, kCall, dCall, pCall) })
            {
                var (bid, ask) = QuoteFromMid(mid, baseIv, ts);
                yield return new OptionQuote(ts, exp, K, right, bid, ask, (bid+ask)/2.0, d, right==Right.Put?skewPut:skewCall);
            }
        }
    }

    public DateOnly TodayExpiry(DateTime ts)
    {
        // Daily PM‑settled: assume same‑day expiry on trading days
        return DateOnly.FromDateTime(ts);
    }

    public (double shortIv, double thirtyIv) GetIvProxies(DateTime ts)
    {
        var d = DateOnly.FromDateTime(ts);
        double vix = _vix.FirstOrDefault(x => x.d == d).vix;
        double vix9 = _vix9d.FirstOrDefault(x => x.d == d).vix9d;
        if (vix == 0) vix = _vix.LastOrDefault(x => x.d <= d).vix;
        if (vix9 == 0) vix9 = _vix9d.LastOrDefault(x => x.d <= d).vix9d;
        return (vix9<=0? vix : vix9, vix);
    }

    private (double bid, double ask) QuoteFromMid(double mid, double baseIv, DateTime ts)
    {
        // Model: wider spreads when vol is higher and into the close; capped by config.
        double pct = 0.10 + 0.5 * baseIv; // 10% + vol component
        var minsToClose = (TodayExpiry(ts).ToDateTime(new TimeOnly(21,0)) - ts).TotalMinutes;
        if (minsToClose < 40) pct += 0.10;
        pct = Math.Min(pct, 0.40);
        double half = mid * pct / 2.0;
        double tick = 0.05; // option tick size
        double bid = Math.Max(0.05, Math.Floor((mid - half)/tick)*tick);
        double ask = Math.Ceiling((mid + half)/tick)*tick;
        return (bid, ask);
    }

    private static List<(DateOnly d,double vix)> Load(string path)
    {
        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture){ HasHeaderRecord = true });
        return csv.GetRecords<Row>().Select(r => (DateOnly.Parse(r.date), double.Parse(r.vix))).ToList();
    }

    private double Spot(DateTime ts)
    {
        // Use latest close from market bars up to ts (prototype). In pro mode, you’d query a price stream.
        var bars = _md.GetBars(DateOnly.FromDateTime(ts.Date), DateOnly.FromDateTime(ts.Date))
                      .Where(b => b.Ts <= ts).ToList();
        return bars.Count>0 ? bars[^1].C : 0;
    }

    private sealed class Row { public string date { get; set; } = ""; public string vix { get; set; } = ""; }
}
```

---

## /Signals/RegimeScorer.cs

```csharp
// WHY: Convert tape context into a numeric “Go/No‑Go” score and bias (range vs trend).
// Signals used:
//  • Opening Range (OR) hold/fail → trend tilt
//  • % bars on one side of VWAP + VWAP slope → trend persistence
//  • Day’s realized range vs ATR(20) → calm vs expansion
//  • Macro proximity + time‑to‑close → risk gates
// References:
//  - VWAP (concept/usage): https://www.investopedia.com/terms/v/vwap.asp
//  - Opening Range idea (educational): https://thepaxgroup.org/the-opening-range/
//  - ATR refresher: https://www.investopedia.com/articles/trading/08/average-true-range.asp

using ODTE.Backtest.Config;
using ODTE.Backtest.Core;
using ODTE.Backtest.Data;

namespace ODTE.Backtest.Signals;

public sealed class RegimeScorer
{
    private readonly SimConfig _cfg;
    public RegimeScorer(SimConfig cfg) { _cfg = cfg; }

    public (int score, bool calmRange, bool trendBiasUp, bool trendBiasDown) Score(DateTime now, IMarketData md, IEconCalendar cal)
    {
        // Opening Range window (first N minutes after RTH open)
        var orMins = _cfg.Signals.OrMinutes;
        var sessionStart = SessionStart(now);
        var orEnd = sessionStart.AddMinutes(orMins);
        var bars = md.GetBars(DateOnly.FromDateTime(now.Date), DateOnly.FromDateTime(now.Date)).Where(b => b.Ts >= sessionStart && b.Ts <= now).ToList();
        double orHigh = bars.Where(b => b.Ts <= orEnd).Select(b => b.H).DefaultIfEmpty().Max();
        double orLow  = bars.Where(b => b.Ts <= orEnd).Select(b => b.L).DefaultIfEmpty().Min();
        bool orBreakUp = bars.LastOrDefault()?.C > orHigh;
        bool orBreakDn = bars.LastOrDefault()?.C < orLow;
        bool orHolds = orBreakUp || orBreakDn;  // simple persistence test

        // VWAP regime
        var vwap = md.Vwap(now, TimeSpan.FromMinutes(_cfg.Signals.VwapWindowMinutes));
        var last30 = bars.Where(b => b.Ts > now.AddMinutes(-_cfg.Signals.VwapWindowMinutes)).ToList();
        int above = last30.Count(b => b.C >= vwap);
        double sidePct = last30.Count>0 ? (double)above / last30.Count : 0.5;
        bool vwapSlopeUp = last30.Count>1 && last30.Last().C > last30.First().C;

        // Range vs ATR (calm vs expansion)
        double dayRange = (bars.Count>0 ? (bars.Max(b=>b.H) - bars.Min(b=>b.L)) : 0);
        double atr = md.Atr20Minutes(now);
        double rngVsAtr = atr>0 ? dayRange/atr : 0.5;

        // Event proximity & late session guardrail
        var nextEvt = cal.NextEventAfter(now);
        int minsToEvent = nextEvt is null ? int.MaxValue : (int)(nextEvt.Value.Ts - now).TotalMinutes;

        int score = 0;
        if (orHolds) score += 2; // trend‑friendly
        score += sidePct >= 0.7 ? 2 : sidePct >= 0.5 ? 1 : 0;
        if (vwapSlopeUp == (orBreakUp)) score += 1; // slope agrees with OR direction
        score += (rngVsAtr <= 0.8 ? 2 : 0); // calm → condor
        score += (rngVsAtr >= 1.0 ? 2 : 0); // expansion → trend
        if (minsToEvent < _cfg.Signals.EventBlockMinutesBefore) score -= 2;
        var minsToClose = (SessionEnd(now) - now).TotalMinutes;
        if (minsToClose < _cfg.NoNewRiskMinutesToClose) score -= 3;

        bool calmRange = rngVsAtr <= 0.8 && !orHolds; // range vibe
        bool trendUp = orBreakUp && sidePct>=0.6;
        bool trendDn = orBreakDn && (1-sidePct)>=0.6;
        return (score, calmRange, trendUp, trendDn);
    }

    private static DateTime SessionStart(DateTime tsUtc) => new DateTime(tsUtc.Year, tsUtc.Month, tsUtc.Day, 14, 30, 0, DateTimeKind.Utc); // 9:30 ET
    private static DateTime SessionEnd(DateTime tsUtc)   => new DateTime(tsUtc.Year, tsUtc.Month, tsUtc.Day, 21, 0, 0, DateTimeKind.Utc);  // 16:00 ET
}
```

---

## /Strategy/SpreadBuilder.cs

```csharp
// WHY: Translate decisions into concrete, risk‑defined structures with guardrails (Δ bands, credit/width floors, quote‑health).
// We pick narrow XSP spreads (1–2 pts) to keep per‑trade worst‑case ≤ ~$200.
// Guards: credit/width must be decent; quoted spread can’t be too wide; long wing is farther OTM.
// References:
// - Cash settlement & European style (assignment risk reduced): https://www.cboe.com/tradable_products/sp_500/mini_spx_options/

using ODTE.Backtest.Config;
using ODTE.Backtest.Core;
using ODTE.Backtest.Data;

namespace ODTE.Backtest.Strategy;

public sealed class SpreadBuilder
{
    private readonly SimConfig _cfg;
    public SpreadBuilder(SimConfig cfg) { _cfg = cfg; }

    public SpreadOrder? TryBuild(DateTime now, Decision decision, IMarketData md, IOptionsData od)
    {
        var exp = od.TodayExpiry(now);
        var quotes = od.GetQuotesAt(now).Where(q => q.Expiry == exp).ToList();
        if (!quotes.Any()) return null;

        double spot = md.GetBars(DateOnly.FromDateTime(now.Date), DateOnly.FromDateTime(now.Date)).Where(b=>b.Ts<=now).LastOrDefault()?.C ?? 0;
        if (spot <= 0) return null;

        (OptionQuote? shortQ, OptionQuote? longQ) pickSingle(Right r, double dMin, double dMax)
        {
            var side = quotes.Where(q => q.Right == r && Math.Abs(q.Delta) >= dMin && Math.Abs(q.Delta) <= dMax)
                             .OrderBy(q => Math.Abs(q.Delta)).ToList();
            var sh = side.FirstOrDefault(); if (sh is null) return (null, null);
            // Long wing farther OTM by width points
            double targetK = r==Right.Put ? sh.Strike - _cfg.WidthPoints.Min : sh.Strike + _cfg.WidthPoints.Min;
            var lg = side.OrderBy(q => Math.Abs(q.Strike - targetK)).FirstOrDefault();
            return (sh, lg);
        }

        SpreadOrder? make(OptionQuote? sh, OptionQuote? lg, Decision t)
        {
            if (sh is null || lg is null) return null;
            double width = Math.Abs(lg.Strike - sh.Strike);
            double credit = Math.Max(0, (sh.Bid - lg.Ask));
            double cpw = width>0? credit/width : 0;
            if (cpw < (_cfg.CreditPerWidthMin.Single)) return null;     // paid enough?
            if ((sh.Ask - sh.Bid) > _cfg.Slippage.SpreadPctCap * Math.Max(credit, 0.10)) return null; // avoid illiquid quotes
            return new SpreadOrder(now, _cfg.Underlying, credit, width, cpw, t,
                new SpreadLeg(exp, sh.Strike, sh.Right, -1), new SpreadLeg(exp, lg.Strike, lg.Right, +1));
        }

        return decision switch
        {
            Decision.SingleSidePut => { var (sh,lg) = pickSingle(Right.Put, _cfg.ShortDelta.SingleMin, _cfg.ShortDelta.SingleMax); return make(sh, lg, decision); },
            Decision.SingleSideCall => { var (sh,lg) = pickSingle(Right.Call,_cfg.ShortDelta.SingleMin, _cfg.ShortDelta.SingleMax); return make(sh, lg, decision); },
            Decision.Condor => BuildCondor(now, quotes, exp),
            _ => null
        };
    }

    private SpreadOrder? BuildCondor(DateTime now, List<OptionQuote> quotes, DateOnly exp)
    {
        var puts = quotes.Where(q => q.Right==Right.Put && Math.Abs(q.Delta)>=_cfg.ShortDelta.CondorMin && Math.Abs(q.Delta)<=_cfg.ShortDelta.CondorMax)
                         .OrderBy(q => Math.Abs(q.Delta)).ToList();
        var calls= quotes.Where(q => q.Right==Right.Call&& Math.Abs(q.Delta)>=_cfg.ShortDelta.CondorMin && Math.Abs(q.Delta)<=_cfg.ShortDelta.CondorMax)
                         .OrderBy(q => Math.Abs(q.Delta)).ToList();
        if (!puts.Any() || !calls.Any()) return null;

        var sp = puts.First(); var sc = calls.First();
        var lp = puts.OrderBy(q => Math.Abs(q.Strike - (sp.Strike - _cfg.WidthPoints.Min))).FirstOrDefault();
        var lc = calls.OrderBy(q => Math.Abs(q.Strike - (sc.Strike + _cfg.WidthPoints.Min))).FirstOrDefault();
        if (lp is null || lc is null) return null;
        double width = _cfg.WidthPoints.Min; // symmetric for simplicity
        double credit = Math.Max(0, (sp.Bid - lp.Ask) + (sc.Bid - lc.Ask));
        double cpw = width>0? credit/width : 0;
        if (cpw < _cfg.CreditPerWidthMin.Condor) return null;
        if ((sp.Ask - sp.Bid) > _cfg.Slippage.SpreadPctCap * Math.Max(credit, 0.10)) return null;

        return new SpreadOrder(now, _cfg.Underlying, credit, width, cpw, Decision.Condor,
            new SpreadLeg(exp, sp.Strike, Right.Put, -1), new SpreadLeg(exp, lp.Strike, Right.Put, +1));
    }
}
```

---

## /Engine/ExecutionEngine.cs

```csharp
// WHY: Model fills and attach exits that reflect 0DTE realities: you rarely get mid, and exits cost more.
// Stops:
//  • Price multiple — spread value ≥ k×entry credit (captures adverse move or IV spike)
//  • Delta breach — short strike ∆ beyond threshold (Gamma danger)
// Fill model: entry at (credit − half‑spread ticks). Exit: pay up (mid + half‑spread ticks).

using ODTE.Backtest.Config;
using ODTE.Backtest.Core;

namespace ODTE.Backtest.Engine;

public sealed class ExecutionEngine
{
    private readonly SimConfig _cfg;
    public ExecutionEngine(SimConfig cfg) { _cfg = cfg; }

    public OpenPosition? TryEnter(SpreadOrder order)
    {
        double tick = _cfg.Slippage.TickValue;
        double entry = Math.Max(0.05, order.Credit - _cfg.Slippage.EntryHalfSpreadTicks * tick);
        return new OpenPosition(order, entry, order.Ts);
    }

    public (bool exit, double exitPrice, string reason) ShouldExit(OpenPosition pos, double currentSpreadValue, double shortStrikeDelta, DateTime now)
    {
        double stopVal = pos.EntryPrice * _cfg.Stops.CreditMultiple;
        if (currentSpreadValue >= stopVal) return (true, currentSpreadValue + _cfg.Slippage.ExitHalfSpreadTicks * _cfg.Slippage.TickValue, $"Stop credit x{_cfg.Stops.CreditMultiple}");
        if (Math.Abs(shortStrikeDelta) >= _cfg.Stops.DeltaBreach) return (true, currentSpreadValue + _cfg.Slippage.ExitHalfSpreadTicks * _cfg.Slippage.TickValue, $"Delta>{_cfg.Stops.DeltaBreach}");
        return (false, 0, "");
    }
}
```

---

## /Engine/RiskManager.cs

```csharp
// WHY: Enforce non‑negotiables at the portfolio level.
//  • Daily hard stop → stop adding risk when Net PnL ≤ −cap
//  • Time guard → no new risk in final X minutes
//  • Concurrency caps per side → avoid clustering at one level

using ODTE.Backtest.Config;
using ODTE.Backtest.Core;

namespace ODTE.Backtest.Engine;

public sealed class RiskManager
{
    private readonly SimConfig _cfg;
    private double _realizedPnL;
    private int _activePerSidePut;
    private int _activePerSideCall;

    public RiskManager(SimConfig cfg){ _cfg = cfg; }

    public bool CanAdd(DateTime now, Decision d)
    {
        if (_realizedPnL <= -_cfg.Risk.DailyLossStop) return false;
        var minsToClose = (new DateTime(now.Year, now.Month, now.Day, 21,0,0, DateTimeKind.Utc) - now).TotalMinutes;
        if (minsToClose < _cfg.NoNewRiskMinutesToClose) return false;
        if (d == Decision.SingleSidePut && _activePerSidePut >= _cfg.Risk.MaxConcurrentPerSide) return false;
        if (d == Decision.SingleSideCall && _activePerSideCall >= _cfg.Risk.MaxConcurrentPerSide) return false;
        return true;
    }

    public void RegisterOpen(Decision d)
    {
        if (d==Decision.SingleSidePut) _activePerSidePut++;
        if (d==Decision.SingleSideCall) _activePerSideCall++;
    }

    public void RegisterClose(Decision d, double pnl)
    {
        _realizedPnL += pnl;
        if (d==Decision.SingleSidePut && _activePerSidePut>0) _activePerSidePut--;
        if (d==Decision.SingleSideCall && _activePerSideCall>0) _activePerSideCall--;
    }
}
```

---

## /Engine/Backtester.cs

```csharp
// WHY: Orchestrates the simulation minute‑by‑minute: manage opens, handle expiry, make new decisions at cadence.
// Cash settlement: Index options (SPXW/XSP) settle to cash at PM close; letting safe spreads expire avoids exit costs.
// References:
// - SPXW/XSP PM cash settlement details: https://www.cboe.com/tradable_products/sp_500/spx_options/specifications/

using ODTE.Backtest.Config;
using ODTE.Backtest.Core;
using ODTE.Backtest.Data;
using ODTE.Backtest.Signals;
using ODTE.Backtest.Strategy;

namespace ODTE.Backtest.Engine;

public sealed class Backtester
{
    private readonly SimConfig _cfg; private readonly IMarketData _md; private readonly IOptionsData _od; private readonly IEconCalendar _cal;
    private readonly RegimeScorer _scorer; private readonly SpreadBuilder _builder; private readonly ExecutionEngine _exec; private readonly RiskManager _risk;

    public Backtester(SimConfig cfg, IMarketData md, IOptionsData od, IEconCalendar cal, RegimeScorer scorer, SpreadBuilder builder, ExecutionEngine exec, RiskManager risk)
    { _cfg=cfg; _md=md; _od=od; _cal=cal; _scorer=scorer; _builder=builder; _exec=exec; _risk=risk; }

    public Task<RunReport> RunAsync()
    {
        var report = new RunReport();
        var bars = _md.GetBars(_cfg.Start, _cfg.End).ToList();
        var active = new List<OpenPosition>();

        foreach (var bar in bars.Where(b => InSession(b.Ts)))
        {
            // 1) Update open positions
            for (int i=active.Count-1; i>=0; i--)
            {
                var pos = active[i];
                // Get mid values for current spread to evaluate exits
                var quotes = _od.GetQuotesAt(bar.Ts).Where(q => q.Expiry == _od.TodayExpiry(bar.Ts)).ToList();
                double shortMid = quotes.Where(q => q.Right==pos.Order.Short.Right && Math.Abs(q.Strike - pos.Order.Short.Strike) < 1e-6).Select(q=>q.Mid).FirstOrDefault();
                double longMid  = quotes.Where(q => q.Right==pos.Order.Long.Right  && Math.Abs(q.Strike - pos.Order.Long.Strike)  < 1e-6).Select(q=>q.Mid).FirstOrDefault();
                double spreadVal = Math.Max(0, shortMid - longMid);
                double shortDelta = quotes.Where(q => q.Right==pos.Order.Short.Right && Math.Abs(q.Strike - pos.Order.Short.Strike) < 1e-6).Select(q=>q.Delta).FirstOrDefault();

                var (exit, price, reason) = _exec.ShouldExit(pos, spreadVal, shortDelta, bar.Ts);
                if (exit)
                {
                    pos.Closed = true; pos.ExitPrice = price; pos.ExitTs = bar.Ts; pos.ExitReason = reason;
                    double fees = 2 * (_cfg.Fees.CommissionPerContract + _cfg.Fees.ExchangeFeesPerContract); // two legs
                    double pnl = (pos.EntryPrice - price) * 100 - fees; // credit spread: higher exit price is worse
                    report.Trades.Add(new TradeResult(pos, pnl, fees, spreadVal - pos.EntryPrice, pos.EntryPrice - spreadVal));
                    _risk.RegisterClose(pos.Order.Type, pnl);
                    active.RemoveAt(i);
                }
            }

            // 2) Expiry cash settlement for safe positions (worth ~0)
            if (IsPmClose(bar.Ts))
            {
                for (int i=active.Count-1; i>=0; i--)
                {
                    var pos = active[i];
                    double price = 0.0; // simplified; enhance by computing intrinsic for near‑ATM spreads
                    pos.Closed = true; pos.ExitPrice = price; pos.ExitTs = bar.Ts; pos.ExitReason = "PM cash settlement";
                    double fees = 0; // let‑expire → no exit commissions
                    double pnl = (pos.EntryPrice - price) * 100 - fees;
                    report.Trades.Add(new TradeResult(pos, pnl, fees, 0, pos.EntryPrice));
                    _risk.RegisterClose(pos.Order.Type, pnl);
                    active.RemoveAt(i);
                }
            }

            // 3) New decision at cadence
            if (ShouldDecide(bar.Ts))
            {
                var (score, calm, up, dn) = _scorer.Score(bar.Ts, _md, _cal);
                Decision d = Decision.NoGo;
                if (score <= -1) d = Decision.NoGo;
                else if (calm && score >= 0) d = Decision.
```
