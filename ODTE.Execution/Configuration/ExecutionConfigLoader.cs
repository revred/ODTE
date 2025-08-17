using ODTE.Execution.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Microsoft.Extensions.Logging;

namespace ODTE.Execution.Configuration;

/// <summary>
/// Loads execution profiles from YAML configuration files.
/// Supports hot-reloading for calibration updates without deployment.
/// </summary>
public class ExecutionConfigLoader
{
    private readonly ILogger<ExecutionConfigLoader> _logger;
    private readonly IDeserializer _deserializer;
    
    public ExecutionConfigLoader(ILogger<ExecutionConfigLoader>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ExecutionConfigLoader>.Instance;
        
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>
    /// Load execution profile from YAML file.
    /// </summary>
    public async Task<ExecutionProfile> LoadProfileAsync(string configPath, string profileName = "conservative")
    {
        try
        {
            _logger.LogDebug("Loading execution profile {ProfileName} from {ConfigPath}", profileName, configPath);
            
            if (!File.Exists(configPath))
            {
                _logger.LogWarning("Config file not found: {ConfigPath}, using default profile", configPath);
                return GetDefaultProfile(profileName);
            }
            
            var yaml = await File.ReadAllTextAsync(configPath);
            var config = _deserializer.Deserialize<ExecutionConfig>(yaml);
            
            if (!config.Profiles.TryGetValue(profileName, out var profileConfig))
            {
                _logger.LogWarning("Profile {ProfileName} not found in config, using default", profileName);
                return GetDefaultProfile(profileName);
            }
            
            var profile = MapToExecutionProfile(profileConfig, profileName);
            _logger.LogInformation("Loaded execution profile {ProfileName} successfully", profileName);
            
            return profile;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading execution config from {ConfigPath}", configPath);
            return GetDefaultProfile(profileName);
        }
    }

    /// <summary>
    /// Load calibration data from YAML file.
    /// </summary>
    public async Task<CalibrationData> LoadCalibrationAsync(string calibrationPath)
    {
        try
        {
            _logger.LogDebug("Loading calibration data from {CalibrationPath}", calibrationPath);
            
            if (!File.Exists(calibrationPath))
            {
                _logger.LogWarning("Calibration file not found: {CalibrationPath}, using defaults", calibrationPath);
                return GetDefaultCalibration();
            }
            
            var yaml = await File.ReadAllTextAsync(calibrationPath);
            var calibration = _deserializer.Deserialize<CalibrationData>(yaml);
            
            _logger.LogInformation("Loaded calibration data for {Instrument} dated {CalibrationDate}", 
                calibration.Instrument, calibration.CalibrationDate);
                
            return calibration;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading calibration data from {CalibrationPath}", calibrationPath);
            return GetDefaultCalibration();
        }
    }

    /// <summary>
    /// Save execution profile to YAML file.
    /// </summary>
    public async Task SaveProfileAsync(ExecutionProfile profile, string configPath)
    {
        try
        {
            var config = new ExecutionConfig
            {
                Profiles = new Dictionary<string, ProfileConfig>
                {
                    [profile.Name] = MapFromExecutionProfile(profile)
                }
            };
            
            var serializer = new SerializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();
                
            var yaml = serializer.Serialize(config);
            await File.WriteAllTextAsync(configPath, yaml);
            
            _logger.LogInformation("Saved execution profile {ProfileName} to {ConfigPath}", profile.Name, configPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving execution config to {ConfigPath}", configPath);
            throw;
        }
    }

    /// <summary>
    /// Get default execution profile when config is unavailable.
    /// </summary>
    private ExecutionProfile GetDefaultProfile(string profileName) => profileName.ToLower() switch
    {
        "base" => ExecutionProfile.Base,
        "optimistic" => ExecutionProfile.Optimistic,
        _ => ExecutionProfile.Conservative
    };

    /// <summary>
    /// Get default calibration data when unavailable.
    /// </summary>
    private CalibrationData GetDefaultCalibration() => new()
    {
        Instrument = "XSP",
        CalibrationDate = DateTime.UtcNow.Date,
        TimeOfDayBins = new Dictionary<string, VIXBins>
        {
            ["09:30-10:00"] = new VIXBins
            {
                Low = new MarketConditionStats { MedianSpread = 0.05m, MidAcceptance = 0.12m },
                Normal = new MarketConditionStats { MedianSpread = 0.08m, MidAcceptance = 0.08m },
                High = new MarketConditionStats { MedianSpread = 0.15m, MidAcceptance = 0.03m }
            },
            ["10:00-15:30"] = new VIXBins
            {
                Low = new MarketConditionStats { MedianSpread = 0.03m, MidAcceptance = 0.18m },
                Normal = new MarketConditionStats { MedianSpread = 0.05m, MidAcceptance = 0.12m },
                High = new MarketConditionStats { MedianSpread = 0.12m, MidAcceptance = 0.05m }
            }
        },
        EventOverrides = new Dictionary<string, EventOverride>
        {
            ["fomc"] = new EventOverride { MidAcceptanceMultiplier = 0.0m, SpreadMultiplier = 2.0m },
            ["opex"] = new EventOverride { MidAcceptanceMultiplier = 0.5m, SpreadMultiplier = 1.3m }
        }
    };

    /// <summary>
    /// Map YAML config to ExecutionProfile object.
    /// </summary>
    private ExecutionProfile MapToExecutionProfile(ProfileConfig config, string name)
    {
        return new ExecutionProfile
        {
            Name = name,
            LatencyMs = config.LatencyMs,
            MaxTobParticipation = config.MaxTobParticipation,
            SlippageFloor = new SlippageSettings
            {
                PerContract = config.SlippageFloor.PerContract,
                PctOfSpread = config.SlippageFloor.PctOfSpread
            },
            SizePenalty = new SizePenaltySettings
            {
                BpPerExtraTobMultiple = config.SizePenalty.BpPerExtraTobMultiple
            },
            AdverseSelectionBps = config.AdverseSelectionBps,
            MidFill = new MidFillSettings
            {
                PWhenSpreadLeq20c = config.MidFill.PWhenSpreadLeq20c,
                POtherwise = config.MidFill.POtherwise
            },
            EventOverrides = new EventOverrideSettings
            {
                OpenCloseWindowMinutes = config.EventOverrides.OpenCloseWindowMinutes,
                SetMidProbabilityToZero = config.EventOverrides.SetMidProbabilityToZero,
                SpecificEvents = config.EventOverrides.SpecificEvents ?? new Dictionary<string, EventOverride>()
            }
        };
    }

    /// <summary>
    /// Map ExecutionProfile to YAML config.
    /// </summary>
    private ProfileConfig MapFromExecutionProfile(ExecutionProfile profile)
    {
        return new ProfileConfig
        {
            LatencyMs = profile.LatencyMs,
            MaxTobParticipation = profile.MaxTobParticipation,
            SlippageFloor = new SlippageFloorConfig
            {
                PerContract = profile.SlippageFloor.PerContract,
                PctOfSpread = profile.SlippageFloor.PctOfSpread
            },
            SizePenalty = new SizePenaltyConfig
            {
                BpPerExtraTobMultiple = profile.SizePenalty.BpPerExtraTobMultiple
            },
            AdverseSelectionBps = profile.AdverseSelectionBps,
            MidFill = new MidFillConfig
            {
                PWhenSpreadLeq20c = profile.MidFill.PWhenSpreadLeq20c,
                POtherwise = profile.MidFill.POtherwise
            },
            EventOverrides = new EventOverrideConfig
            {
                OpenCloseWindowMinutes = profile.EventOverrides.OpenCloseWindowMinutes,
                SetMidProbabilityToZero = profile.EventOverrides.SetMidProbabilityToZero,
                SpecificEvents = profile.EventOverrides.SpecificEvents
            }
        };
    }
}

/// <summary>
/// Root YAML configuration structure.
/// </summary>
public class ExecutionConfig
{
    public Dictionary<string, ProfileConfig> Profiles { get; set; } = new();
}

/// <summary>
/// Individual profile configuration.
/// </summary>
public class ProfileConfig
{
    public int LatencyMs { get; set; } = 250;
    public decimal MaxTobParticipation { get; set; } = 0.05m;
    public SlippageFloorConfig SlippageFloor { get; set; } = new();
    public SizePenaltyConfig SizePenalty { get; set; } = new();
    public int AdverseSelectionBps { get; set; } = 25;
    public MidFillConfig MidFill { get; set; } = new();
    public EventOverrideConfig EventOverrides { get; set; } = new();
}

public class SlippageFloorConfig
{
    public decimal PerContract { get; set; } = 0.02m;
    public decimal PctOfSpread { get; set; } = 0.10m;
}

public class SizePenaltyConfig
{
    public int BpPerExtraTobMultiple { get; set; } = 8;
}

public class MidFillConfig
{
    public Dictionary<string, decimal> PWhenSpreadLeq20c { get; set; } = new();
    public Dictionary<string, decimal> POtherwise { get; set; } = new();
}

public class EventOverrideConfig
{
    public int OpenCloseWindowMinutes { get; set; } = 10;
    public bool SetMidProbabilityToZero { get; set; } = true;
    public Dictionary<string, EventOverride> SpecificEvents { get; set; } = new();
}

/// <summary>
/// Calibration data for market microstructure parameters.
/// </summary>
public class CalibrationData
{
    public string Instrument { get; set; } = "";
    public DateTime CalibrationDate { get; set; }
    public Dictionary<string, VIXBins> TimeOfDayBins { get; set; } = new();
    public Dictionary<string, EventOverride> EventOverrides { get; set; } = new();
}

public class VIXBins
{
    public MarketConditionStats Low { get; set; } = new();
    public MarketConditionStats Normal { get; set; } = new();
    public MarketConditionStats High { get; set; } = new();
}

public class MarketConditionStats
{
    public decimal MedianSpread { get; set; }
    public decimal MidAcceptance { get; set; }
    public decimal AdverseSelectionBps { get; set; }
    public int SampleSize { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}