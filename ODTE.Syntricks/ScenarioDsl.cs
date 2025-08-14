using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ODTE.Syntricks;

/// <summary>
/// Scenario configuration loaded from YAML
/// </summary>
public class ScenarioConfig
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;
    
    [YamlMember(Alias = "description")]
    public string Description { get; set; } = string.Empty;
    
    [YamlMember(Alias = "archetype")]
    public string Archetype { get; set; } = "calm_range";
    
    [YamlMember(Alias = "replay_speed")]
    public double ReplaySpeed { get; set; } = 1.0;
    
    [YamlMember(Alias = "duration_minutes")]
    public int DurationMinutes { get; set; } = 390; // Full trading day
    
    [YamlMember(Alias = "seed")]
    public int Seed { get; set; } = 42;
    
    [YamlMember(Alias = "events")]
    public List<EventConfig> Events { get; set; } = new();
    
    [YamlMember(Alias = "microstructure")]
    public MicrostructureConfig Microstructure { get; set; } = new();
    
    [YamlMember(Alias = "adversarial")]
    public bool Adversarial { get; set; } = false;
}

/// <summary>
/// Event injection configuration (CPI, FOMC, etc.)
/// </summary>
public class EventConfig
{
    [YamlMember(Alias = "time")]
    public string Time { get; set; } = "14:30"; // UTC time
    
    [YamlMember(Alias = "type")]
    public string Type { get; set; } = "CPI";
    
    [YamlMember(Alias = "impact")]
    public EventImpact Impact { get; set; } = new();
}

/// <summary>
/// Event impact parameters
/// </summary>
public class EventImpact
{
    [YamlMember(Alias = "price_jump_sigma")]
    public double PriceJumpSigma { get; set; } = 2.0;
    
    [YamlMember(Alias = "iv_jump_pct")]
    public double IvJumpPct { get; set; } = 20.0;
    
    [YamlMember(Alias = "spread_widen_bps")]
    public double SpreadWidenBps { get; set; } = 50.0;
    
    [YamlMember(Alias = "duration_minutes")]
    public int DurationMinutes { get; set; } = 30;
}

/// <summary>
/// Microstructure configuration
/// </summary>
public class MicrostructureConfig
{
    [YamlMember(Alias = "u_shaped_volume")]
    public bool UShapedVolume { get; set; } = true;
    
    [YamlMember(Alias = "lunch_lull")]
    public bool LunchLull { get; set; } = true;
    
    [YamlMember(Alias = "late_session_widening")]
    public bool LateSessionWidening { get; set; } = true;
    
    [YamlMember(Alias = "lunch_start")]
    public string LunchStart { get; set; } = "17:00"; // 12:00 ET = 17:00 UTC
    
    [YamlMember(Alias = "lunch_end")]
    public string LunchEnd { get; set; } = "18:00"; // 13:00 ET = 18:00 UTC
}

/// <summary>
/// Scenario DSL parser and loader
/// </summary>
public static class ScenarioDsl
{
    /// <summary>
    /// Load scenario configuration from YAML file
    /// </summary>
    public static ScenarioConfig LoadFromFile(string yamlPath)
    {
        if (!File.Exists(yamlPath))
            throw new FileNotFoundException($"Scenario file not found: {yamlPath}");

        var yaml = File.ReadAllText(yamlPath);
        return LoadFromYaml(yaml);
    }

    /// <summary>
    /// Load scenario configuration from YAML string
    /// </summary>
    public static ScenarioConfig LoadFromYaml(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        return deserializer.Deserialize<ScenarioConfig>(yaml);
    }

    /// <summary>
    /// Create a market stream from scenario configuration
    /// </summary>
    public static async Task<IMarketStream> CreateStreamAsync(ScenarioConfig config, IBlockBootstrap? bootstrap = null)
    {
        IMarketStream stream;

        if (bootstrap != null)
        {
            // Use block bootstrap for synthetic data
            stream = new BootstrapMarketStream(bootstrap, config.Archetype, config.Seed);
        }
        else
        {
            // Use historical replay (placeholder - would load actual historical data)
            var placeholderTicks = GeneratePlaceholderTicks(config);
            stream = new HistoricalMarketStream(placeholderTicks, TimeSpan.FromMinutes(1));
        }

        stream.ReplaySpeed = config.ReplaySpeed;
        
        return stream;
    }

    /// <summary>
    /// Generate placeholder ticks for testing (replace with real historical data)
    /// </summary>
    private static IEnumerable<SpotTick> GeneratePlaceholderTicks(ScenarioConfig config)
    {
        var random = new Random(config.Seed);
        var startTime = DateTime.UtcNow.Date.AddHours(14).AddMinutes(30); // 9:30 ET
        var basePrice = 400.0; // SPY around $400
        var currentPrice = basePrice;

        for (int i = 0; i < config.DurationMinutes; i++)
        {
            var timestamp = startTime.AddMinutes(i);
            
            // Simple random walk for testing
            var change = (random.NextDouble() - 0.5) * 2.0; // +/- $1 max per minute
            currentPrice = Math.Max(currentPrice + change, basePrice * 0.95); // Floor at 5% down
            currentPrice = Math.Min(currentPrice, basePrice * 1.05); // Cap at 5% up

            var high = currentPrice + random.NextDouble() * 0.5;
            var low = currentPrice - random.NextDouble() * 0.5;
            var volume = (long)(1000000 + random.NextDouble() * 2000000); // 1-3M volume

            yield return new SpotTick
            {
                Timestamp = timestamp,
                Open = currentPrice,
                High = high,
                Low = low,
                Close = currentPrice,
                Volume = volume,
                Vwap = currentPrice + (random.NextDouble() - 0.5) * 0.1,
                Atr = 2.5 + random.NextDouble() * 1.0, // $2.5-3.5 ATR
                SessionPct = (double)i / config.DurationMinutes
            };
        }
    }
}