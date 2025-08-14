using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ODTE.Backtest.Reporting;

/// <summary>
/// Tracks the state of multi-day backtest runs, enabling resume/restart capabilities
/// </summary>
public sealed class RunManifest
{
    public string RunId { get; set; } = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
    public string CfgHash { get; set; } = string.Empty;
    public string StrategyHash { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public HashSet<DateOnly> Scheduled { get; set; } = new();
    public HashSet<DateOnly> Done { get; set; } = new();
    public HashSet<DateOnly> Failed { get; set; } = new();
    public HashSet<DateOnly> Skipped { get; set; } = new();
    
    // Trinity integration fields
    public string TrinityPortfolioId { get; set; } = string.Empty;
    public string TrinityLedgerPath { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();

    public static string PathFor(string reportsDir) => Path.Combine(reportsDir, "run_manifest.json");

    public static RunManifest LoadOrCreate(string reportsDir, string cfgHash, string stratHash, IEnumerable<DateOnly> dates)
    {
        var path = PathFor(reportsDir);
        if (File.Exists(path))
        {
            try
            {
                var text = File.ReadAllText(path);
                var m = JsonSerializer.Deserialize<RunManifest>(text) ?? new RunManifest();
                
                // If configuration or strategy changed, reset the schedule
                if (m.CfgHash != cfgHash || m.StrategyHash != stratHash)
                {
                    Console.WriteLine($"Configuration or strategy changed. Creating new manifest.");
                    m = new RunManifest 
                    { 
                        CfgHash = cfgHash, 
                        StrategyHash = stratHash,
                        TrinityPortfolioId = $"ODTE_{stratHash}_{DateTime.UtcNow:yyyyMMdd}"
                    };
                    foreach (var d in dates) m.Scheduled.Add(d);
                }
                return m;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading manifest: {ex.Message}. Creating new one.");
                var m = new RunManifest 
                { 
                    CfgHash = cfgHash, 
                    StrategyHash = stratHash,
                    TrinityPortfolioId = $"ODTE_{stratHash}_{DateTime.UtcNow:yyyyMMdd}"
                };
                foreach (var d in dates) m.Scheduled.Add(d);
                return m;
            }
        }
        else
        {
            var m = new RunManifest 
            { 
                CfgHash = cfgHash, 
                StrategyHash = stratHash,
                TrinityPortfolioId = $"ODTE_{stratHash}_{DateTime.UtcNow:yyyyMMdd}"
            };
            foreach (var d in dates) m.Scheduled.Add(d);
            return m;
        }
    }

    public void Save(string reportsDir)
    {
        var path = PathFor(reportsDir);
        Directory.CreateDirectory(reportsDir);
        
        var opts = new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        // Update completion time if all scheduled days are processed
        if (Scheduled.Count > 0 && 
            Scheduled.All(d => Done.Contains(d) || Failed.Contains(d) || Skipped.Contains(d)))
        {
            CompletedAt = DateTime.UtcNow;
        }
        
        File.WriteAllText(path, JsonSerializer.Serialize(this, opts));
    }
    
    public double GetProgress()
    {
        if (Scheduled.Count == 0) return 0;
        var processed = Done.Count + Failed.Count + Skipped.Count;
        return (double)processed / Scheduled.Count * 100;
    }
    
    public (int pending, int done, int failed, int skipped) GetStats()
    {
        var pending = Scheduled.Except(Done).Except(Failed).Except(Skipped).Count();
        return (pending, Done.Count, Failed.Count, Skipped.Count);
    }
    
    public void PrintStatus()
    {
        var (pending, done, failed, skipped) = GetStats();
        Console.WriteLine($"📊 Manifest Status: {GetProgress():F1}% complete");
        Console.WriteLine($"   ✅ Done: {done} | ⏳ Pending: {pending} | ❌ Failed: {failed} | ⏭️ Skipped: {skipped}");
        Console.WriteLine($"   📁 Trinity Portfolio: {TrinityPortfolioId}");
    }
}