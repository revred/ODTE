using ODTE.Backtest.Config;
using System.Reflection;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ODTE.Backtest.Strategy;

/// <summary>
/// Factory for creating strategy models from YAML configuration
/// WHY: Enables dynamic loading of strategy models without hard-coding specific backtester classes
/// </summary>
public static class StrategyModelFactory
{
    private static readonly Dictionary<string, Type> RegisteredModels = new();
    private static bool _initialized = false;

    /// <summary>
    /// Register all available strategy models from loaded assemblies
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;

        Console.WriteLine("🔍 Scanning for strategy models...");

        // Scan all loaded assemblies for strategy model implementations
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        var modelTypes = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IStrategyModel).IsAssignableFrom(t))
            .ToList();

        foreach (var modelType in modelTypes)
        {
            // Use model name from attribute or class name
            var modelName = GetModelName(modelType);
            RegisteredModels[modelName.ToUpperInvariant()] = modelType;
            Console.WriteLine($"✅ Registered model: {modelName} ({modelType.Name})");
        }

        _initialized = true;
        Console.WriteLine($"📊 Total models registered: {RegisteredModels.Count}");
    }

    /// <summary>
    /// Create strategy model instance from YAML configuration file
    /// </summary>
    public static async Task<IStrategyModel> CreateFromConfigAsync(string configPath)
    {
        if (!_initialized) Initialize();

        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Configuration file not found: {configPath}");
        }

        Console.WriteLine($"📋 Loading strategy configuration: {configPath}");

        try
        {
            // Load YAML configuration
            var yaml = await File.ReadAllTextAsync(configPath);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var config = deserializer.Deserialize<StrategyConfig>(yaml);

            if (string.IsNullOrEmpty(config.ModelName))
            {
                throw new InvalidOperationException("Configuration missing required 'model_name' field");
            }

            if (string.IsNullOrEmpty(config.ModelVersion))
            {
                throw new InvalidOperationException("Configuration missing required 'model_version' field");
            }

            Console.WriteLine($"🎯 Model: {config.ModelName} v{config.ModelVersion}");
            Console.WriteLine($"📅 Period: {config.Start} to {config.End}");
            Console.WriteLine($"📈 Underlying: {config.Underlying}");

            // Validate optimization traceability
            if (config.OptimizationParameters != null)
            {
                Console.WriteLine($"🧬 Genetic Algorithm: {config.OptimizationParameters.GeneticAlgorithm}");
                Console.WriteLine($"⏰ Last Optimization: {config.OptimizationParameters.LastOptimization}");
                
                if (!string.IsNullOrEmpty(config.OptimizationParameters.OptimizationSource))
                {
                    Console.WriteLine($"📂 Source: {config.OptimizationParameters.OptimizationSource}");
                }

                if (config.OptimizationParameters.StrategyComponents?.Count > 0)
                {
                    Console.WriteLine($"🔧 Components: {string.Join(", ", config.OptimizationParameters.StrategyComponents)}");
                }
            }

            // Create model instance
            var model = CreateModelInstance(config.ModelName, config);

            // Validate configuration compatibility
            var simConfig = ConvertToSimConfig(config);
            model.ValidateConfiguration(simConfig);

            Console.WriteLine($"✅ Strategy model created: {model.ModelName} v{model.ModelVersion}");
            
            return model;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create strategy model from {configPath}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Create strategy model instance by name
    /// </summary>
    public static IStrategyModel CreateModelInstance(string modelName, StrategyConfig config)
    {
        if (!RegisteredModels.TryGetValue(modelName.ToUpperInvariant(), out var modelType))
        {
            var available = string.Join(", ", RegisteredModels.Keys);
            throw new ArgumentException($"Unknown strategy model: '{modelName}'. Available models: {available}");
        }

        try
        {
            // Create instance with configuration parameter
            var instance = Activator.CreateInstance(modelType, config);
            if (instance is IStrategyModel model)
            {
                return model;
            }
            
            throw new InvalidOperationException($"Model {modelType.Name} does not implement IStrategyModel");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create instance of {modelType.Name}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Get list of all available strategy models
    /// </summary>
    public static List<string> GetAvailableModels()
    {
        if (!_initialized) Initialize();
        return RegisteredModels.Keys.ToList();
    }

    /// <summary>
    /// Convert strategy configuration to SimConfig for backtest engine
    /// </summary>
    private static SimConfig ConvertToSimConfig(StrategyConfig strategyConfig)
    {
        return new SimConfig
        {
            Start = DateOnly.ParseExact(strategyConfig.Start, "yyyy-MM-dd"),
            End = DateOnly.ParseExact(strategyConfig.End, "yyyy-MM-dd"),
            Underlying = strategyConfig.Underlying,
            Mode = strategyConfig.Mode,
            RthOnly = strategyConfig.RthOnly,
            Timezone = strategyConfig.Timezone,
            CadenceSeconds = strategyConfig.CadenceSeconds,
            NoNewRiskMinutesToClose = strategyConfig.NoNewRiskMinutesToClose,
            Slippage = strategyConfig.Slippage != null ? new SlippageCfg
            {
                EntryHalfSpreadTicks = strategyConfig.Slippage.EntryHalfSpreadTicks,
                ExitHalfSpreadTicks = strategyConfig.Slippage.ExitHalfSpreadTicks,
                LateSessionExtraTicks = strategyConfig.Slippage.LateSessionExtraTicks,
                TickValue = strategyConfig.Slippage.TickValue,
                SpreadPctCap = strategyConfig.Slippage.SpreadPctCap
            } : new SlippageCfg(),
            Fees = strategyConfig.Fees != null ? new FeesCfg
            {
                CommissionPerContract = strategyConfig.Fees.CommissionPerContract,
                ExchangeFeesPerContract = strategyConfig.Fees.ExchangeFeesPerContract
            } : new FeesCfg(),
            Paths = strategyConfig.Paths != null ? new PathsCfg
            {
                BarsCsv = strategyConfig.Paths.BarsCsv,
                VixCsv = strategyConfig.Paths.VixCsv,
                Vix9dCsv = strategyConfig.Paths.Vix9dCsv,
                CalendarCsv = strategyConfig.Paths.CalendarCsv,
                ReportsDir = strategyConfig.Paths.ReportsDir
            } : new PathsCfg()
        };
    }

    /// <summary>
    /// Extract model name from type
    /// </summary>
    private static string GetModelName(Type modelType)
    {
        // Look for ModelNameAttribute first
        var nameAttr = modelType.GetCustomAttribute<StrategyModelNameAttribute>();
        if (nameAttr != null && !string.IsNullOrEmpty(nameAttr.Name))
        {
            return nameAttr.Name;
        }

        // Fall back to class name, removing common suffixes
        var name = modelType.Name;
        var suffixes = new[] { "Model", "Strategy", "Engine", "System" };
        
        foreach (var suffix in suffixes)
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^suffix.Length];
                break;
            }
        }

        return name;
    }
}

/// <summary>
/// Attribute to specify the name for a strategy model
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class StrategyModelNameAttribute : Attribute
{
    public string Name { get; }

    public StrategyModelNameAttribute(string name)
    {
        Name = name;
    }
}

/// <summary>
/// Configuration structure loaded from YAML files
/// </summary>
public class StrategyConfig
{
    public string ModelName { get; set; } = string.Empty;
    public string ModelVersion { get; set; } = string.Empty;
    public OptimizationParameters? OptimizationParameters { get; set; }
    public string Underlying { get; set; } = string.Empty;
    public string Start { get; set; } = string.Empty;
    public string End { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public bool RthOnly { get; set; } = true;
    public string Timezone { get; set; } = "America/New_York";
    public int CadenceSeconds { get; set; } = 3600;
    public int NoNewRiskMinutesToClose { get; set; } = 60;
    public SlippageCfg? Slippage { get; set; }
    public FeesCfg? Fees { get; set; }
    public PathsCfg? Paths { get; set; }
    public Dictionary<string, object> StrategySpecific { get; set; } = new();
}

/// <summary>
/// Optimization parameters for traceability
/// </summary>
public class OptimizationParameters
{
    public string GeneticAlgorithm { get; set; } = string.Empty;
    public string LastOptimization { get; set; } = string.Empty;
    public string? OptimizationSource { get; set; }
    public string? TournamentDemo { get; set; }
    public int TargetDte { get; set; }
    public List<string> StrategyComponents { get; set; } = new();
}