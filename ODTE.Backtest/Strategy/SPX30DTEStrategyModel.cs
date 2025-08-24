using ODTE.Backtest.Config;
using ODTE.Backtest.Data;
using ODTE.Contracts.Historical;

namespace ODTE.Backtest.Strategy;

/// <summary>
/// SPX30DTE Strategy Model - 30-day SPX options with probe system, VIX hedging, and RevFib scaling
/// WHY: Generates trading signals based on genetically optimized SPX30DTE strategy parameters
/// EXECUTION: Signals are passed to ODTE.Execution engine for realistic fills and risk management
/// </summary>
[StrategyModelName("SPX30DTE")]
public class SPX30DTEStrategyModel : IStrategyModel, IDisposable
{
    public string ModelName { get; } = "SPX30DTE";
    public string ModelVersion { get; } = "v1.0";

    private readonly StrategyConfig _strategyConfig;
    private SPX30DTEConfig _config;
    
    // State tracking
    private decimal _currentNotchLimit = 2000m; // Start at balanced notch
    private int _consecutiveProfitableDays = 0;
    private DateTime _lastTradeDate = DateTime.MinValue;
    private readonly Dictionary<string, Position> _activePositions = new();

    public SPX30DTEStrategyModel(StrategyConfig strategyConfig)
    {
        _strategyConfig = strategyConfig ?? throw new ArgumentNullException(nameof(strategyConfig));
        _config = CreateDefaultSPX30DTEConfig();
    }

    public async Task InitializeAsync(SimConfig config, IMarketData marketData, IOptionsData optionsData)
    {
        Console.WriteLine($"🎯 Initializing {ModelName} v{ModelVersion}");
        Console.WriteLine($"💰 Initial Rev Fib Notch Limit: ${_currentNotchLimit:N0}");
        Console.WriteLine($"🔧 Strategy Components: BWB + Probes + VIX Hedging + Rev Fib Scaling");
        
        // Log genetic algorithm traceability
        var parameters = _strategyConfig.OptimizationParameters;
        if (parameters != null)
        {
            Console.WriteLine($"🧬 Genetic Algorithm: {parameters.GeneticAlgorithm}");
            Console.WriteLine($"⏰ Last Optimization: {parameters.LastOptimization}");
            Console.WriteLine($"📂 Source: {parameters.OptimizationSource}");
        }

        await Task.CompletedTask;
    }

    public async Task<List<CandidateOrder>> GenerateSignalsAsync(
        DateTime timestamp,
        MarketDataBar currentBar,
        PortfolioState portfolio)
    {
        var signals = new List<CandidateOrder>();

        try
        {
            // Skip weekends and holidays
            if (!ShouldTradeToday(timestamp))
                return signals;

            // Update Rev Fib Notch based on recent performance
            UpdateRiskScaling(timestamp, portfolio);

            // 1. Generate probe signals (XSP 15DTE) - Monday: 2, Tuesday: 2, Wednesday: 1
            var probeSignals = GenerateProbeSignals(timestamp, currentBar, portfolio);
            signals.AddRange(probeSignals);

            // 2. Generate BWB signals (SPX 30DTE) - only with probe confirmation
            var bwbSignals = GenerateBWBSignals(timestamp, currentBar, portfolio);
            signals.AddRange(bwbSignals);

            // 3. Generate VIX hedge signals (50DTE spreads)
            var hedgeSignals = GenerateVIXHedgeSignals(timestamp, currentBar, portfolio);
            signals.AddRange(hedgeSignals);

            // Apply Rev Fib Notch position sizing to all signals
            foreach (var signal in signals)
            {
                ApplyPositionSizing(signal, portfolio);
            }

            if (signals.Any())
            {
                Console.WriteLine($"📊 {timestamp:yyyy-MM-dd}: Generated {signals.Count} signals " +
                    $"(Probes: {probeSignals.Count}, BWB: {bwbSignals.Count}, Hedges: {hedgeSignals.Count})");
            }

            return signals;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error generating SPX30DTE signals for {timestamp:yyyy-MM-dd}: {ex.Message}");
            return new List<CandidateOrder>();
        }
    }

    public async Task<List<CandidateOrder>> ManagePositionsAsync(
        DateTime timestamp,
        MarketDataBar currentBar,
        PortfolioState portfolio)
    {
        var managementSignals = new List<CandidateOrder>();

        try
        {
            // Check each position for exit conditions
            foreach (var position in portfolio.OpenPositions)
            {
                var exitSignal = EvaluatePositionExit(timestamp, position);
                if (exitSignal != null)
                {
                    managementSignals.Add(exitSignal);
                }
            }

            // Emergency risk controls
            if (ShouldActivateEmergencyStop(portfolio))
            {
                Console.WriteLine($"🚨 Emergency stop activated - closing all positions");
                foreach (var position in portfolio.OpenPositions)
                {
                    managementSignals.Add(CreateEmergencyExitOrder(position));
                }
            }

            return managementSignals;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error managing SPX30DTE positions for {timestamp:yyyy-MM-dd}: {ex.Message}");
            return new List<CandidateOrder>();
        }
    }

    public Dictionary<string, object> GetModelParameters()
    {
        return new Dictionary<string, object>
        {
            ["model_name"] = ModelName,
            ["model_version"] = ModelVersion,
            ["genetic_algorithm"] = _strategyConfig.OptimizationParameters?.GeneticAlgorithm ?? "16_mutation_tournament",
            ["last_optimization"] = _strategyConfig.OptimizationParameters?.LastOptimization ?? "2025-08-15",
            ["optimization_source"] = _strategyConfig.OptimizationParameters?.OptimizationSource ?? "SPX30DTEConfig.cs",
            
            // Current state
            ["current_notch_limit"] = _currentNotchLimit,
            ["consecutive_profitable_days"] = _consecutiveProfitableDays,
            
            // Configuration parameters (from genetic algorithm optimization)
            ["bwb_target_dte"] = _config.SPXCore.TargetDTE,
            ["bwb_target_credit"] = _config.SPXCore.TargetCredit,
            ["bwb_max_positions"] = _config.SPXCore.MaxPositions,
            ["probe_target_dte"] = _config.XSPProbe.TargetDTE,
            ["probe_win_rate_threshold"] = _config.XSPProbe.WinRateThreshold,
            ["hedge_target_dte"] = _config.VIXHedge.TargetDTE,
            ["rev_fib_notch_limits"] = _config.RiskScale.NotchLimits,
            ["emergency_stop_loss"] = _config.RiskScale.EmergencyStopLoss
        };
    }

    public void ValidateConfiguration(SimConfig config)
    {
        // Validate underlying
        if (!config.Underlying.Equals("SPX", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"SPX30DTE model requires SPX underlying, got: {config.Underlying}");
        }

        // Validate minimum period (need at least 30 days for 30DTE strategy)
        var dateRange = config.End.ToDateTime(TimeOnly.MinValue) - config.Start.ToDateTime(TimeOnly.MinValue);
        if (dateRange.Days < 30)
        {
            throw new InvalidOperationException($"SPX30DTE requires at least 30 days, got: {dateRange.Days} days");
        }

        Console.WriteLine("✅ SPX30DTE configuration validation passed");
    }

    public void Dispose()
    {
        // Cleanup any resources
    }

    // Private helper methods
    private bool ShouldTradeToday(DateTime timestamp)
    {
        // Skip weekends
        if (timestamp.DayOfWeek == DayOfWeek.Saturday || timestamp.DayOfWeek == DayOfWeek.Sunday)
            return false;

        // Skip if too close to market close (no new risk in final hour)
        var marketClose = timestamp.Date.AddHours(16); // 4 PM ET
        if (timestamp > marketClose.AddMinutes(-60))
            return false;

        return true;
    }

    private void UpdateRiskScaling(DateTime timestamp, PortfolioState portfolio)
    {
        // Rev Fib Notch scaling based on daily P&L performance
        if (timestamp.Date != _lastTradeDate)
        {
            var dailyPnL = portfolio.UnrealizedPnL; // Simplified - would calculate actual daily P&L
            var lossThreshold = _currentNotchLimit * 0.15m; // 15% loss triggers downgrade
            
            if (dailyPnL < -lossThreshold)
            {
                // Downgrade notch immediately on significant loss
                if (_currentNotchLimit > 400m) // Don't go below survival level
                {
                    _currentNotchLimit = _config.RiskScale.NotchLimits[GetNextLowerNotchIndex()];
                    Console.WriteLine($"📉 Rev Fib Notch downgraded to ${_currentNotchLimit:N0} due to ${dailyPnL:N0} loss");
                }
                _consecutiveProfitableDays = 0;
            }
            else if (dailyPnL > 0)
            {
                // Track profitable days for potential upgrade
                _consecutiveProfitableDays++;
                if (_consecutiveProfitableDays >= 10 && _currentNotchLimit < 5000m)
                {
                    _currentNotchLimit = _config.RiskScale.NotchLimits[GetNextHigherNotchIndex()];
                    Console.WriteLine($"📈 Rev Fib Notch upgraded to ${_currentNotchLimit:N0} after {_consecutiveProfitableDays} profitable days");
                    _consecutiveProfitableDays = 0;
                }
            }
            else
            {
                _consecutiveProfitableDays = 0;
            }

            _lastTradeDate = timestamp.Date;
        }
    }

    private List<CandidateOrder> GenerateProbeSignals(DateTime timestamp, MarketDataBar currentBar, PortfolioState portfolio)
    {
        var signals = new List<CandidateOrder>();
        
        // XSP probe schedule: Monday: 2, Tuesday: 2, Wednesday: 1, Thu/Fri: 0
        var probesAllowed = timestamp.DayOfWeek switch
        {
            DayOfWeek.Monday => 2,
            DayOfWeek.Tuesday => 2,
            DayOfWeek.Wednesday => 1,
            _ => 0
        };

        if (probesAllowed > 0)
        {
            var activeProbes = portfolio.OpenPositions.Count(p => p.StrategyType == "PROBE");
            if (activeProbes < _config.XSPProbe.MaxActiveProbes)
            {
                // Generate probe signal - ODTE.Execution will handle the actual options chain and strikes
                var probeSignal = new CandidateOrder
                {
                    OrderId = $"PROBE_{timestamp:yyyyMMdd}_{Guid.NewGuid().ToString()[..8]}",
                    Symbol = "XSP",
                    StrategyType = "PROBE",
                    OrderType = OrderType.Spread,
                    ExpectedCredit = _config.XSPProbe.TargetCredit,
                    MaxRisk = _config.XSPProbe.MaxRisk,
                    EntryReason = $"Daily probe signal - {timestamp.DayOfWeek}",
                    ExpirationDate = timestamp.AddDays(_config.XSPProbe.TargetDTE),
                    Metadata = new Dictionary<string, object>
                    {
                        ["delta_target"] = _config.XSPProbe.DeltaTarget,
                        ["spread_width"] = _config.XSPProbe.SpreadWidth,
                        ["profit_target"] = _config.XSPProbe.ProfitTarget
                    }
                };
                signals.Add(probeSignal);
            }
        }

        return signals;
    }

    private List<CandidateOrder> GenerateBWBSignals(DateTime timestamp, MarketDataBar currentBar, PortfolioState portfolio)
    {
        var signals = new List<CandidateOrder>();
        
        // Only enter BWB if we have probe confirmation
        if (!HasProbeConfirmation(portfolio))
            return signals;

        var activeBWBs = portfolio.OpenPositions.Count(p => p.StrategyType == "BWB");
        if (activeBWBs < _config.SPXCore.MaxPositions)
        {
            // Generate BWB signal - ODTE.Execution will handle strike selection and pricing
            var bwbSignal = new CandidateOrder
            {
                OrderId = $"BWB_{timestamp:yyyyMMdd}_{Guid.NewGuid().ToString()[..8]}",
                Symbol = "SPX",
                StrategyType = "BWB",
                OrderType = OrderType.Spread,
                ExpectedCredit = _config.SPXCore.TargetCredit,
                MaxRisk = _config.SPXCore.MaxRisk,
                EntryReason = "BWB signal with probe confirmation",
                ExpirationDate = timestamp.AddDays(_config.SPXCore.TargetDTE),
                Metadata = new Dictionary<string, object>
                {
                    ["wing_width_points"] = _config.SPXCore.WingWidthPoints,
                    ["short_strike_offset"] = _config.SPXCore.ShortStrikeOffset,
                    ["long_upper_offset"] = _config.SPXCore.LongUpperOffset,
                    ["long_lower_offset"] = _config.SPXCore.LongLowerOffset,
                    ["profit_target"] = _config.SPXCore.ProfitTarget,
                    ["delta_threshold"] = _config.SPXCore.DeltaThreshold
                }
            };
            signals.Add(bwbSignal);
        }

        return signals;
    }

    private List<CandidateOrder> GenerateVIXHedgeSignals(DateTime timestamp, MarketDataBar currentBar, PortfolioState portfolio)
    {
        var signals = new List<CandidateOrder>();
        
        // Determine required hedge count based on total portfolio exposure
        var totalExposure = portfolio.OpenPositions.Sum(p => p.MaxRisk);
        var activeHedges = portfolio.OpenPositions.Count(p => p.StrategyType == "HEDGE");
        
        var requiredHedges = _config.VIXHedge.BaseHedgeCount;
        if (totalExposure >= 10000m) requiredHedges = 2;
        if (totalExposure >= 15000m) requiredHedges = 3;
        if (totalExposure >= 20000m) requiredHedges = 4;

        if (activeHedges < requiredHedges)
        {
            var hedgeSignal = new CandidateOrder
            {
                OrderId = $"HEDGE_{timestamp:yyyyMMdd}_{Guid.NewGuid().ToString()[..8]}",
                Symbol = "VIX",
                StrategyType = "HEDGE",
                OrderType = OrderType.Spread,
                ExpectedCredit = -_config.VIXHedge.MaxCostPerHedge, // Hedges cost money
                MaxRisk = _config.VIXHedge.MaxCostPerHedge,
                EntryReason = $"VIX hedge for ${totalExposure:N0} exposure",
                ExpirationDate = timestamp.AddDays(_config.VIXHedge.TargetDTE),
                Metadata = new Dictionary<string, object>
                {
                    ["long_strike"] = _config.VIXHedge.LongStrike,
                    ["short_strike"] = _config.VIXHedge.ShortStrike,
                    ["max_payoff"] = _config.VIXHedge.MaxPayoff
                }
            };
            signals.Add(hedgeSignal);
        }

        return signals;
    }

    private bool HasProbeConfirmation(PortfolioState portfolio)
    {
        // Check recent probe win rate (last 10 trading days)
        var recentProbes = portfolio.OpenPositions
            .Where(p => p.StrategyType == "PROBE" && 
                       (DateTime.Now - p.EntryDate).Days <= 10)
            .ToList();

        if (recentProbes.Count < 2) // Need minimum 2 probe wins
            return false;

        var winningProbes = recentProbes.Count(p => p.UnrealizedPnL > 0);
        var winRate = recentProbes.Count > 0 ? (decimal)winningProbes / recentProbes.Count : 0;

        return winRate >= _config.XSPProbe.WinRateThreshold;
    }

    private void ApplyPositionSizing(CandidateOrder signal, PortfolioState portfolio)
    {
        // Scale position size based on Rev Fib Notch limit
        if (signal.MaxRisk > _currentNotchLimit)
        {
            var scaleFactor = _currentNotchLimit / signal.MaxRisk;
            signal.MaxRisk = _currentNotchLimit;
            signal.ExpectedCredit *= scaleFactor;
            
            signal.Metadata["notch_limit"] = _currentNotchLimit;
            signal.Metadata["scale_factor"] = scaleFactor;
        }
    }

    private CandidateOrder? EvaluatePositionExit(DateTime timestamp, Position position)
    {
        // Check profit target
        var profitTarget = position.StrategyType switch
        {
            "BWB" => _config.SPXCore.ProfitTarget,
            "PROBE" => _config.XSPProbe.ProfitTarget,
            _ => 0.65m
        };

        if (position.UnrealizedPnL > 0 && position.UnrealizedPnL >= position.MaxRisk * profitTarget)
        {
            return CreateExitOrder(position, "PROFIT_TARGET");
        }

        // Check stop loss (2x credit received)
        var stopLossMultiple = position.StrategyType switch
        {
            "BWB" => _config.SPXCore.StopLoss,
            _ => 2.0m
        };

        var entryCredit = (decimal)(position.Metadata.TryGetValue("entry_credit", out var credit) ? credit : 0);
        if (position.UnrealizedPnL < -(entryCredit * stopLossMultiple))
        {
            return CreateExitOrder(position, "STOP_LOSS");
        }

        // Check DTE-based forced exit
        var daysToExpiration = (position.ExpirationDate - timestamp).Days;
        var forcedExitDTE = position.StrategyType switch
        {
            "BWB" => _config.SPXCore.ForcedExitDTE,
            "PROBE" => _config.XSPProbe.ForcedExitDTE,
            _ => 5
        };

        if (daysToExpiration <= forcedExitDTE)
        {
            return CreateExitOrder(position, "DTE_EXPIRY");
        }

        return null;
    }

    private bool ShouldActivateEmergencyStop(PortfolioState portfolio)
    {
        var totalLoss = portfolio.UnrealizedPnL + portfolio.RealizedPnL;
        var emergencyThreshold = portfolio.AccountValue * _config.RiskScale.EmergencyStopLoss;
        
        return totalLoss < -emergencyThreshold;
    }

    private CandidateOrder CreateExitOrder(Position position, string exitReason)
    {
        return new CandidateOrder
        {
            OrderId = $"EXIT_{position.PositionId}_{DateTime.Now:HHmmss}",
            Symbol = position.Symbol,
            StrategyType = "EXIT",
            OrderType = OrderType.Market,
            EntryReason = exitReason,
            Metadata = new Dictionary<string, object>
            {
                ["original_position_id"] = position.PositionId,
                ["exit_reason"] = exitReason
            }
        };
    }

    private CandidateOrder CreateEmergencyExitOrder(Position position)
    {
        return CreateExitOrder(position, "EMERGENCY_STOP");
    }

    private int GetNextLowerNotchIndex()
    {
        for (int i = 0; i < _config.RiskScale.NotchLimits.Length; i++)
        {
            if (_config.RiskScale.NotchLimits[i] == _currentNotchLimit && i < _config.RiskScale.NotchLimits.Length - 1)
                return i + 1;
        }
        return _config.RiskScale.NotchLimits.Length - 1; // Return survival level
    }

    private int GetNextHigherNotchIndex()
    {
        for (int i = 0; i < _config.RiskScale.NotchLimits.Length; i++)
        {
            if (_config.RiskScale.NotchLimits[i] == _currentNotchLimit && i > 0)
                return i - 1;
        }
        return 0; // Return maximum level
    }

    private SPX30DTEConfig CreateDefaultSPX30DTEConfig()
    {
        // Create default configuration matching genetic algorithm optimization
        return new SPX30DTEConfig
        {
            SPXCore = new BWBConfiguration
            {
                TargetDTE = 30,
                TargetCredit = 800m,
                MaxRisk = 4200m,
                MaxPositions = 4,
                ProfitTarget = 0.65m,
                StopLoss = 2.0m,
                ForcedExitDTE = 10,
                DeltaThreshold = 0.15m,
                WingWidthPoints = 50,
                ShortStrikeOffset = 350,
                LongUpperOffset = 450,
                LongLowerOffset = 300
            },
            XSPProbe = new ProbeConfiguration
            {
                TargetDTE = 15,
                TargetCredit = 65m,
                MaxRisk = 435m,
                MaxActiveProbes = 20,
                ProfitTarget = 0.65m,
                ForcedExitDTE = 5,
                WinRateThreshold = 0.60m,
                DeltaTarget = 0.20m,
                SpreadWidth = 5
            },
            VIXHedge = new HedgeConfiguration
            {
                TargetDTE = 50,
                BaseHedgeCount = 2,
                MaxCostPerHedge = 50m,
                MaxPayoff = 1000m,
                LongStrike = 20,
                ShortStrike = 30
            },
            RiskScale = new SPX30DTERevFibNotchScale
            {
                NotchLimits = new[] { 5000m, 3200m, 2000m, 1200m, 800m, 400m },
                EmergencyStopLoss = 0.25m
            }
        };
    }
}

// Configuration classes (simplified for demonstration)
public class SPX30DTEConfig
{
    public BWBConfiguration SPXCore { get; set; } = new();
    public ProbeConfiguration XSPProbe { get; set; } = new();
    public HedgeConfiguration VIXHedge { get; set; } = new();
    public SPX30DTERevFibNotchScale RiskScale { get; set; } = new();
}

public class BWBConfiguration
{
    public int TargetDTE { get; set; }
    public decimal TargetCredit { get; set; }
    public decimal MaxRisk { get; set; }
    public int MaxPositions { get; set; }
    public decimal ProfitTarget { get; set; }
    public decimal StopLoss { get; set; }
    public int ForcedExitDTE { get; set; }
    public decimal DeltaThreshold { get; set; }
    public decimal WingWidthPoints { get; set; }
    public decimal ShortStrikeOffset { get; set; }
    public decimal LongUpperOffset { get; set; }
    public decimal LongLowerOffset { get; set; }
}

public class ProbeConfiguration
{
    public int TargetDTE { get; set; }
    public decimal TargetCredit { get; set; }
    public decimal MaxRisk { get; set; }
    public int MaxActiveProbes { get; set; }
    public decimal ProfitTarget { get; set; }
    public int ForcedExitDTE { get; set; }
    public decimal WinRateThreshold { get; set; }
    public decimal DeltaTarget { get; set; }
    public decimal SpreadWidth { get; set; }
}

public class HedgeConfiguration
{
    public int TargetDTE { get; set; }
    public int BaseHedgeCount { get; set; }
    public decimal MaxCostPerHedge { get; set; }
    public decimal MaxPayoff { get; set; }
    public decimal LongStrike { get; set; }
    public decimal ShortStrike { get; set; }
}

public class SPX30DTERevFibNotchScale
{
    public decimal[] NotchLimits { get; set; } = Array.Empty<decimal>();
    public decimal EmergencyStopLoss { get; set; }
}