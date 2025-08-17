namespace ODTE.Execution.Models;

/// <summary>
/// Execution profile defining how aggressive/conservative fill simulation should be.
/// Maps directly to YAML configuration in realFillSimulationUpgrade.md specification.
/// </summary>
public record ExecutionProfile
{
    public string Name { get; init; } = "";
    public int LatencyMs { get; init; } = 250;
    public decimal MaxTobParticipation { get; init; } = 0.05m;
    
    public SlippageSettings SlippageFloor { get; init; } = new();
    public SizePenaltySettings SizePenalty { get; init; } = new();
    public int AdverseSelectionBps { get; init; } = 25;
    public MidFillSettings MidFill { get; init; } = new();
    public EventOverrideSettings EventOverrides { get; init; } = new();
    
    /// <summary>
    /// Get pre-configured conservative profile for institutional compliance
    /// </summary>
    public static ExecutionProfile Conservative => new()
    {
        Name = "conservative",
        LatencyMs = 250,
        MaxTobParticipation = 0.05m,
        SlippageFloor = new SlippageSettings
        {
            PerContract = 0.02m,
            PctOfSpread = 0.10m
        },
        AdverseSelectionBps = 25,
        MidFill = new MidFillSettings
        {
            PWhenSpreadLeq20c = new Dictionary<string, decimal>
            {
                ["conservative"] = 0.00m,
                ["base"] = 0.15m,
                ["optimistic"] = 0.30m
            },
            POtherwise = new Dictionary<string, decimal>
            {
                ["conservative"] = 0.00m,
                ["base"] = 0.05m,
                ["optimistic"] = 0.15m
            }
        }
    };
    
    /// <summary>
    /// Get pre-configured base profile for research
    /// </summary>
    public static ExecutionProfile Base => Conservative with 
    { 
        Name = "base",
        LatencyMs = 200,
        MaxTobParticipation = 0.08m
    };
    
    /// <summary>
    /// Get pre-configured optimistic profile for sensitivity analysis only
    /// </summary>
    public static ExecutionProfile Optimistic => Base with 
    { 
        Name = "optimistic",
        LatencyMs = 150,
        MaxTobParticipation = 0.12m
    };
}

public record SlippageSettings
{
    public decimal PerContract { get; init; } = 0.02m;
    public decimal PctOfSpread { get; init; } = 0.10m;
}

public record SizePenaltySettings
{
    public int BpPerExtraTobMultiple { get; init; } = 8;
}

public record MidFillSettings
{
    public Dictionary<string, decimal> PWhenSpreadLeq20c { get; init; } = new();
    public Dictionary<string, decimal> POtherwise { get; init; } = new();
    
    /// <summary>
    /// Get mid-fill probability for current profile and spread
    /// </summary>
    public decimal GetMidFillProbability(string profileName, decimal spreadCents)
    {
        var probabilities = spreadCents <= 20m ? PWhenSpreadLeq20c : POtherwise;
        return probabilities.GetValueOrDefault(profileName, 0m);
    }
}

public record EventOverrideSettings
{
    public int OpenCloseWindowMinutes { get; init; } = 10;
    public bool SetMidProbabilityToZero { get; init; } = true;
    public Dictionary<string, EventOverride> SpecificEvents { get; init; } = new();
}

public record EventOverride
{
    public decimal MidAcceptanceMultiplier { get; init; } = 1.0m;
    public decimal SpreadMultiplier { get; init; } = 1.0m;
    public decimal LatencyMultiplier { get; init; } = 1.0m;
}