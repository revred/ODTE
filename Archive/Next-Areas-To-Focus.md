# PR Pack — Resumable Backtest, Sparse Scheduler & Strategy Variants

This pack contains **three self‑contained PR drafts**, each focused on one feature. You can raise them as three separate PRs in GitHub by copying only the files for that PR.

> Target repo layout matches your current ODTE project. All code assumes .NET 8 and your existing namespaces. Minimal, compile‑ready stubs are included; expand as needed.

---

## PR #1 — Five‑Year Resumable Backtest + Run Manifest

**Goal:** run **5 years** of days with **incremental results**, support `--resume`, `--invalidate`, and optional parallel **N workers**. Writes per‑day outputs as they complete and a multi‑year **ledger**.

### Files changed/added

- **ADD** `Reporting/RunManifest.cs`
- **ADD** `Engine/DayRunner.cs`
- **MOD** `Program.cs` (CLI + orchestration)
- **MOD** `Reporting/Reporter.cs` (optional: per‑strategy subfolder support)

### Code

**ADD: Reporting/RunManifest.cs**

```csharp
using System.Text.Json;

namespace ODTE.Backtest.Reporting;

public sealed class RunManifest
{
    public string RunId { get; set; } = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
    public string CfgHash { get; set; } = string.Empty;
    public string StrategyHash { get; set; } = string.Empty;
    public HashSet<DateOnly> Scheduled { get; set; } = new();
    public HashSet<DateOnly> Done { get; set; } = new();
    public HashSet<DateOnly> Failed { get; set; } = new();
    public HashSet<DateOnly> Skipped { get; set; } = new();

    public static string PathFor(string reportsDir) => System.IO.Path.Combine(reportsDir, "run_manifest.json");

    public static RunManifest LoadOrCreate(string reportsDir, string cfgHash, string stratHash, IEnumerable<DateOnly> dates)
    {
        var path = PathFor(reportsDir);
        if (File.Exists(path))
        {
            var text = File.ReadAllText(path);
            var m = JsonSerializer.Deserialize<RunManifest>(text) ?? new RunManifest();
            // If hashes changed, reset schedule
            if (m.CfgHash != cfgHash || m.StrategyHash != stratHash)
            {
                m = new RunManifest { CfgHash = cfgHash, StrategyHash = stratHash };
                foreach (var d in dates) m.Scheduled.Add(d);
            }
            return m;
        }
        else
        {
            var m = new RunManifest { CfgHash = cfgHash, StrategyHash = stratHash };
            foreach (var d in dates) m.Scheduled.Add(d);
            return m;
        }
    }

    public void Save(string reportsDir)
    {
        var path = PathFor(reportsDir);
        var opts = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(path, JsonSerializer.Serialize(this, opts));
    }
}
```

**ADD: Engine/DayRunner.cs**

```csharp
using ODTE.Backtest.Config;
using ODTE.Backtest.Core;
using ODTE.Backtest.Data;
using ODTE.Backtest.Engine;
using ODTE.Backtest.Reporting;
using ODTE.Backtest.Signals;
using ODTE.Backtest.Strategy;

namespace ODTE.Backtest.Engine;

public static class DayRunner
{
    public static (DateOnly day, bool ok) RunDay(SimConfig cfg, DateOnly day,
        IMarketData market, IOptionsData options, IEconCalendar econ,
        RegimeScorer scorer, SpreadBuilder builder, ExecutionEngine exec, RiskManager risk,
        string? strategySubdir = null)
    {
        var bt = new Backtester(cfg, market, options, econ, scorer, builder, exec, risk);
        var rep = bt.RunAsync().Result; // day-bounded data source ensures only that day is processed
        string outDir = cfg.Paths.ReportsDir;
        if (!string.IsNullOrWhiteSpace(strategySubdir)) outDir = Path.Combine(outDir, strategySubdir!);
        Directory.CreateDirectory(outDir);
        // Per-day artifacts
        Reporter.WriteTradesPerDay(cfg with { Paths = cfg.Paths with { ReportsDir = outDir } }, rep);
        Reporter.WriteDailySummaries(cfg with { Paths = cfg.Paths with { ReportsDir = outDir } }, rep);
        Reporter.WriteLedger(cfg with { Paths = cfg.Paths with { ReportsDir = outDir } }, rep);
        return (day, true);
    }
}
```

**MOD: Program.cs (CLI + resume/invalidate)**

```csharp
// Pseudocode-ish but compile-ready minimal CLI
using System.Security.Cryptography;
using System.Text;
using ODTE.Backtest.Config;
using ODTE.Backtest.Data;
using ODTE.Backtest.Reporting;
using ODTE.Backtest.Engine;
using ODTE.Backtest.Signals;
using ODTE.Backtest.Strategy;

partial class Program
{
    private static string HashConfig(SimConfig cfg)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(cfg))));

    private static IEnumerable<DateOnly> DateRange(DateOnly start, DateOnly end)
    { for (var d = start; d <= end; d = d.AddDays(1)) yield return d; }

    static int Main(string[] args)
    {
        string cfgPath = args.FirstOrDefault(a => a.EndsWith(".yaml")) ?? "appsettings.yaml";
        var cfg = LoadConfig(cfgPath);
        var from = cfg.Start; var to = cfg.End; bool resume = args.Contains("--resume");
        int workers = int.TryParse(ArgVal(args, "--max-workers"), out var w) ? Math.Max(1, w) : 1;
        string invalidate = ArgVal(args, "--invalidate") ?? string.Empty; // "all" | "since=YYYY-MM-DD"

        // Build data providers (per your current design)
        IMarketData market = new CsvMarketData(cfg.Paths.BarsCsv, cfg.Timezone, cfg.RthOnly);
        IOptionsData options = cfg.Mode.Equals("prototype", StringComparison.OrdinalIgnoreCase)
            ? new SyntheticOptionsData(cfg, market, cfg.Paths.VixCsv, cfg.Paths.Vix9dCsv)
            : throw new NotImplementedException();
        IEconCalendar econ = new CsvCalendar(cfg.Paths.CalendarCsv, cfg.Timezone);

        var scorer = new RegimeScorer(cfg);
        var builder = new SpreadBuilder(cfg);
        var exec = new ExecutionEngine(cfg);
        var risk = new RiskManager(cfg);

        // Manifest
        var allDays = DateRange(from, to).ToList();
        var cfgHash = HashConfig(cfg); var stratHash = "baseline"; // can swap per strategy later
        var manifest = RunManifest.LoadOrCreate(cfg.Paths.ReportsDir, cfgHash, stratHash, allDays);

        if (!string.IsNullOrEmpty(invalidate))
        {
            if (invalidate.Equals("all", StringComparison.OrdinalIgnoreCase)) { manifest.Done.Clear(); manifest.Failed.Clear(); }
            else if (invalidate.StartsWith("since="))
            {
                var s = DateOnly.Parse(invalidate[6..]);
                manifest.Done.RemoveWhere(d => d >= s);
                manifest.Failed.RemoveWhere(d => d >= s);
            }
            manifest.Save(cfg.Paths.ReportsDir); return 0;
        }

        var work = resume ? manifest.Scheduled.Except(manifest.Done).Except(manifest.Failed).OrderBy(d=>d).ToList()
                          : allDays;

        // Simple single-thread loop (expand to Parallel later)
        foreach (var day in work)
        {
            try
            {
                var (d, ok) = DayRunner.RunDay(cfg, day, market, options, econ, scorer, builder, exec, risk, strategySubdir: null);
                if (ok) manifest.Done.Add(d); else manifest.Failed.Add(d);
                manifest.Save(cfg.Paths.ReportsDir);
                Console.WriteLine($"Finished {d:yyyy-MM-dd}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAIL {day:yyyy-MM-dd} → {ex.Message}");
                manifest.Failed.Add(day);
                manifest.Save(cfg.Paths.ReportsDir);
            }
        }
        return 0;
    }

    static string? ArgVal(string[] a, string key)
    { var i = Array.IndexOf(a, key); return i>=0 && i+1<a.Length ? a[i+1] : null; }
}
```

**MOD (optional): Reporting/Reporter.cs** — support per‑strategy subfolder by honoring `cfg.Paths.ReportsDir` (already done in your latest version). No change needed if you pass subdir via `DayRunner` as above.

### How to use

```bash
# 5-year window
sed -i '' 's/^start:.*/start: 2020-08-13/; s/^end:.*/end: 2025-08-12/' appsettings.yaml

# First run (fresh manifest)
dotnet run --project ODTE.Backtest -- backtest

# Resume remaining days later
dotnet run --project ODTE.Backtest -- backtest --resume

# Invalidate since a date
dotnet run --project ODTE.Backtest -- invalidate since=2024-01-01
```

---

## PR #2 — Sparse Day Scheduler + Day Tags (Calm/Volatile/Fed/etc.)

**Goal:** run days in a **coverage-first** order, and label each day with **archetype**, **vol bucket**, and **event tags**. Ledger is enriched with tags for quick diagnosis.

### Files changed/added

- **ADD** `Synth/DayTags.cs` (labels)
- **ADD** `Engine/SparseScheduler.cs`
- **MOD** `Program.cs` (use scheduler when `--scheduler sparse`)
- **MOD** `Reporting/Reporter.cs` (join tags into `ledger.csv`)

### Code

**ADD: Synth/DayTags.cs**

```csharp
namespace ODTE.Backtest.Synth;

public sealed record DayTag(DateOnly Date, string Archetype, string VolBucket, string EventTags);

public static class DayTags
{
    // In production, load from data/archetypes/labels.csv + calendar; here, simple heuristics
    public static Dictionary<DateOnly, DayTag> Load(string pathCsv)
    {
        var dict = new Dictionary<DateOnly, DayTag>();
        if (!File.Exists(pathCsv)) return dict;
        foreach (var line in File.ReadAllLines(pathCsv).Skip(1))
        {
            var parts = line.Split(',');
            var d = DateOnly.Parse(parts[0]);
            dict[d] = new DayTag(d, parts[1], parts[2], parts[3]);
        }
        return dict;
    }
}
```

**ADD: Engine/SparseScheduler.cs**

```csharp
using ODTE.Backtest.Synth;

namespace ODTE.Backtest.Engine;

public static class SparseScheduler
{
    // Weighted round-robin across (archetype, vol_bucket, event/no_event)
    public static List<DateOnly> Order(IEnumerable<DateOnly> days, Dictionary<DateOnly, DayTag> tags)
    {
        var buckets = days.GroupBy(d => Key(tags, d)).ToDictionary(g => g.Key, g => new Queue<DateOnly>(g.OrderBy(x=>x)));
        var weights = new Dictionary<string,int>{{"calm_range|calm|noevent",4},{"trend_up|normal|noevent",3},{"trend_dn|normal|noevent",3},{"*|volatile|*",2},{"*|*|event",2}};

        var outList = new List<DateOnly>();
        while (buckets.Values.Any(q => q.Count>0))
        {
            foreach (var (k, q) in buckets.ToArray())
            {
                if (q.Count==0) continue;
                var w = WeightFor(weights, k);
                for (int i=0;i<w && q.Count>0;i++) outList.Add(q.Dequeue());
            }
        }
        return outList;
    }

    private static string Key(Dictionary<DateOnly, DayTag> tags, DateOnly d)
    {
        if (!tags.TryGetValue(d, out var t)) return "unknown|normal|noevent";
        var ev = string.IsNullOrWhiteSpace(t.EventTags)?"noevent":"event";
        return $"{t.Archetype}|{t.VolBucket}|{ev}".ToLowerInvariant();
    }
    private static int WeightFor(Dictionary<string,int> weights, string key)
    {
        if (weights.TryGetValue(key, out var w)) return w;
        foreach (var kv in weights) // wildcard: poor man’s matcher
        {
            var parts = kv.Key.Split('|'); var k2 = key.Split('|');
            if ((parts[0]=="*"||parts[0]==k2[0]) && (parts[1]=="*"||parts[1]==k2[1]) && (parts[2]=="*"||parts[2]==k2[2])) return kv.Value;
        }
        return 1;
    }
}
```

**MOD: Program.cs** (use sparse scheduler when requested)

```csharp
// after manifest/work list build
var tags = ODTE.Backtest.Synth.DayTags.Load(System.IO.Path.Combine(cfg.Paths.ReportsDir, "day_tags.csv"));
var sparse = args.Contains("--scheduler") && (ArgVal(args,"--scheduler")=="sparse");
if (sparse)
{
    work = SparseScheduler.Order(work, tags);
}
```

**MOD: Reporting/Reporter.cs** (enrich ledger with tags)

```csharp
// After computing daily aggregates, join tags if available
var tagPath = Path.Combine(cfg.Paths.ReportsDir, "day_tags.csv");
var tagMap = new Dictionary<DateOnly,(string a,string v,string e)>();
if (File.Exists(tagPath))
{
    foreach (var line in File.ReadAllLines(tagPath).Skip(1))
    { var p = line.Split(','); var d = DateOnly.Parse(p[0]); tagMap[d] = (p[1],p[2],p[3]); }
}
// When writing csv fields for ledger, append archetype, vol_bucket, event_tags if present
```

**ADD: Reports/day\_tags.csv (format)**

```
date,archetype,vol_bucket,event_tags
2024-02-01,calm_range,calm,
2024-02-13,event_spike_fade,volatile,fed,cpi
```

### How to use

```bash
# Generate tags (from your classifier & calendar tooling) then run sparse
cp tools/out/day_tags.csv Reports/day_tags.csv

dotnet run --project ODTE.Backtest -- backtest --scheduler sparse --resume
```

---

## PR #3 — Strategy Registry + LHC\_Prof Variant (A/B capable)

**Goal:** allow **multiple strategies** in one run, isolate outputs per strategy, and add the **LHC\_Prof** variant as a first alternative.

### Files changed/added

- **ADD** `Strategy/IStrategy.cs`
- **ADD** `Strategy/StrategyRegistry.cs`
- **ADD** `Strategy/LhcProfStrategy.cs`
- **MOD** `Program.cs` (parse `--strategies`, route per strategy, per‑strategy Reports subdir)

### Code

**ADD: Strategy/IStrategy.cs**

```csharp
using ODTE.Backtest.Core;
using ODTE.Backtest.Data;
using ODTE.Backtest.Signals;

namespace ODTE.Backtest.Strategy;

public interface IStrategy
{
    string Name { get; }
    string ParamsToJson();
    Decision Decide(DateTime now, RegimeScorer scorer, IMarketData md, IEconCalendar cal);
}
```

**ADD: Strategy/StrategyRegistry.cs**

```csharp
namespace ODTE.Backtest.Strategy;

public sealed class StrategyRegistry
{
    private readonly Dictionary<string, IStrategy> _map = new(StringComparer.OrdinalIgnoreCase);
    public void Register(IStrategy s) => _map[s.Name] = s;
    public IStrategy Resolve(string name) => _map[name];
    public IEnumerable<IStrategy> All(IEnumerable<string> names)
    { foreach (var n in names) if (_map.ContainsKey(n)) yield return _map[n]; }
}
```

**ADD: Strategy/LhcProfStrategy.cs**

```csharp
using ODTE.Backtest.Config;
using ODTE.Backtest.Core;
using ODTE.Backtest.Data;
using ODTE.Backtest.Signals;

namespace ODTE.Backtest.Strategy;

public sealed class LhcProfStrategy : IStrategy
{
    public string Name => "lhc_prof";
    public record Params(double ShortDeltaMin=0.10, double ShortDeltaMax=0.20, int Width=2, double CpwMin=0.20,
                         double StopMultiple=2.2, double DeltaBreach=0.33, int NoNewMins=60, bool BlockEvents=true);
    private readonly Params P;
    private readonly SimConfig _cfg;
    public LhcProfStrategy(SimConfig cfg, Params p){ P=p; _cfg=cfg; }

    public string ParamsToJson() => System.Text.Json.JsonSerializer.Serialize(P);

    public Decision Decide(DateTime now, RegimeScorer scorer, IMarketData md, IEconCalendar cal)
    {
        var (score, calm, up, dn) = scorer.Score(now, md, cal);
        var minsToClose = (new DateTime(now.Year,now.Month,now.Day,21,0,0,DateTimeKind.Utc)-now).TotalMinutes;
        if (minsToClose < P.NoNewMins) return Decision.NoGo;
        var ne = cal.NextEventAfter(now); if (P.BlockEvents && ne!=null && (ne.Value.Ts - now).TotalMinutes < 60) return Decision.NoGo;
        if (calm && score>=0) return Decision.Condor;
        if (up && score>=4) return Decision.SingleSidePut;
        if (dn && score>=4) return Decision.SingleSideCall;
        return Decision.NoGo;
    }
}
```

**MOD: Program.cs** (multi‑strategy loop)

```csharp
// Parse strategies from CLI: --strategies baseline,lhc_prof
var stratArg = ArgVal(args, "--strategies");
var stratNames = string.IsNullOrWhiteSpace(stratArg) ? new[]{"baseline"} : stratArg.Split(',');
var registry = new StrategyRegistry();
registry.Register(new BaselineStrategy(cfg)); // implement BaselineStrategy thin wrapper around your existing Decide logic
registry.Register(new LhcProfStrategy(cfg, new LhcProfStrategy.Params()));

foreach (var strat in registry.All(stratNames))
{
    Console.WriteLine($"Running strategy: {strat.Name}");
    // Reuse manifest but separate output subdir per strategy
    var manifest = RunManifest.LoadOrCreate(Path.Combine(cfg.Paths.ReportsDir, strat.Name), HashConfig(cfg), strat.Name, allDays);
    var work = resume ? manifest.Scheduled.Except(manifest.Done).Except(manifest.Failed).OrderBy(d=>d).ToList() : allDays;
    if (sparse) work = SparseScheduler.Order(work, tags);

    foreach (var day in work)
    {
        try
        {
            var (d, ok) = DayRunner.RunDay(cfg, day, market, options, econ, scorer, builder, exec, risk, strategySubdir: strat.Name);
            if (ok) manifest.Done.Add(d); else manifest.Failed.Add(d);
            manifest.Save(Path.Combine(cfg.Paths.ReportsDir, strat.Name));
        }
        catch (Exception ex) { Console.WriteLine($"[{strat.Name}] FAIL {day:yyyy-MM-dd}: {ex.Message}"); manifest.Failed.Add(day); manifest.Save(Path.Combine(cfg.Paths.ReportsDir, strat.Name)); }
    }
}
```

### How to use

```bash
# A/B both strategies over a sparse schedule with resume
 dotnet run --project ODTE.Backtest -- backtest \
   --from 2020-08-13 --to 2025-08-12 \
   --strategies baseline,lhc_prof \
   --scheduler sparse --resume
```

---

## Verification & CI

- Each PR compiles independently.
- CI uploads `Reports/**/ledger.csv` when present.
- Manual spot tests:
  - **PR1:** `--invalidate since=YYYY-MM-DD`, `--resume` yields immediate continuation; `run_manifest.json` evolves.
  - **PR2:** `--scheduler sparse` shows a shuffled cross‑regime order; `ledger.csv` includes tags.
  - **PR3:** outputs go under `Reports/baseline/` and `Reports/lhc_prof/` with separate ledgers for fast comparison.

---

## Notes

- Keep per‑trade worst‑case ≤ \~\$200 and the daily kill‑switch at −\$500.
- When you later plug real intraday options (ORATS/LiveVol), these abstractions remain unchanged.

