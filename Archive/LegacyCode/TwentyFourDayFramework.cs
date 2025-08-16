using System;
using System.Collections.Generic;
using System.Linq;

namespace ODTE.Strategy;

/// <summary>
/// 24-Day Maximum Reward Trading Framework
/// 
/// OBJECTIVE: Earn $6000 over 24 trading days with intelligent contingency management
/// 
/// FRAMEWORK DESIGN:
/// - Days 1-18: Base earnings phase ($250/day average = $4500 target)
/// - Days 19-24: Acceleration phase if behind OR risk doubling if ahead
/// - Contingency: Switch to "sniper mode" if $6000 target appears unlikely by day 12
/// - Risk amplification: Double position sizes for final 6 days if $6000 achieved by day 18
/// 
/// STRATEGIC PILLARS:
/// 1. Adaptive Position Sizing based on performance trajectory
/// 2. Market Regime Detection for battle selection
/// 3. Multi-Strategy Arsenal deployment
/// 4. Risk Management with performance-based adjustments
/// 5. Psychological discipline through systematic framework
/// 
/// ACADEMIC FOUNDATION:
/// - Kelly Criterion for optimal position sizing
/// - Behavioral finance principles for systematic decision making
/// - Options Greeks optimization for risk-adjusted returns
/// - Statistical arbitrage concepts for consistent edge
/// </summary>
public class TwentyFourDayFramework
{
    public const decimal TARGET_PROFIT = 6000m;
    public const int FRAMEWORK_DAYS = 24;
    public const int SNIPER_ACTIVATION_DAY = 12;
    public const int ACCELERATION_DAY = 18;
    
    private readonly List<DayResult> _dayResults = new();
    public readonly FrameworkConfig _config;
    private readonly List<IStrategy> _strategies;
    private readonly MarketRegimeAnalyzer _regimeAnalyzer;
    private readonly PositionSizingEngine _positionSizing;
    
    // === CRISIS DETECTION SYSTEMS ===
    private readonly CrisisDetector _crisisDetector;
    private readonly VIXSpikeDetector _vixSpikeDetector;
    private readonly GapDetector _gapDetector;
    private readonly CircuitBreakerDetector _circuitBreakerDetector;
    private readonly NewsBasedCrisisTriggers _newsAnalyzer;
    private readonly EmergencyStopLoss _emergencyStopLoss;
    private readonly CrisisPositionSizing _crisisSizing;

    public TwentyFourDayFramework(FrameworkConfig config)
    {
        _config = config;
        _regimeAnalyzer = new MarketRegimeAnalyzer();
        _positionSizing = new PositionSizingEngine(config);
        
        // === INITIALIZE CRISIS DETECTION SYSTEMS ===
        _crisisDetector = new CrisisDetector();
        _vixSpikeDetector = new VIXSpikeDetector();
        _gapDetector = new GapDetector();
        _circuitBreakerDetector = new CircuitBreakerDetector();
        _newsAnalyzer = new NewsBasedCrisisTriggers();
        _emergencyStopLoss = new EmergencyStopLoss(config.MaxDailyLoss);
        _crisisSizing = new CrisisPositionSizing();
        
        // Initialize ENHANCED multi-strategy arsenal with crisis strategies
        _strategies = new List<IStrategy>
        {
            new BlackSwanStrategy(),      // NEW: Crisis and extreme volatility strategy
            new VolatilityExpansionStrategy(), // NEW: Volatility expansion plays
            new GhostStrategy(),          // Ultra-conservative, high win rate (85%+)
            new PrecisionStrategy(),      // Surgical strikes on perfect setups (70% win rate)
            new SniperStrategy(),         // High-conviction, larger positions (60% win rate, 3:1 RR)
            new VolatilityCrusher(),      // Volatility selling in calm conditions (80% win rate)
            new RegimeAdaptive()          // Dynamic strategy based on market conditions
        };
    }

    /// <summary>
    /// Execute daily trading decision with framework intelligence
    /// </summary>
    public async Task<DayResult> ExecuteTradingDay(DateTime tradingDay, MarketConditions conditions)
    {
        var dayNumber = _dayResults.Count + 1;
        var currentPnL = _dayResults.Sum(r => r.PnL);
        
        // === PERFORMANCE TRAJECTORY ANALYSIS ===
        var trajectory = AnalyzePerformanceTrajectory(dayNumber, currentPnL);
        Console.WriteLine($"\n📊 Day {dayNumber}/24 - Trajectory: {trajectory}");
        Console.WriteLine($"💰 Current P&L: ${currentPnL:F2} | Target: ${TARGET_PROFIT:F2}");
        
        // === MARKET REGIME DETECTION ===
        var regimeTypeStr = await _regimeAnalyzer.ClassifyMarketRegimeAsync(conditions);
        var regimeType = regimeTypeStr switch 
        {
            "calm" => RegimeType.LowVolatility,
            "mixed" => RegimeType.Ranging,
            "convex" => RegimeType.HighVolatility,
            _ => RegimeType.LowVolatility
        };
        var regime = new MarketRegime 
        { 
            RegimeType = regimeType, 
            Confidence = 85.0m, 
            VIX = (decimal)conditions.VIX,
            TrendStrength = Math.Abs(conditions.TrendScore),
            HasMajorEvent = conditions.VIX > 30
        };
        Console.WriteLine($"🎯 Market Regime: {regime.RegimeType} (Confidence: {regime.Confidence:F1}%)");
        
        // === CRISIS DETECTION ANALYSIS ===
        var crisisMode = _crisisDetector.DetectCrisisMode(conditions, regime);
        var vixSpike = _vixSpikeDetector.DetectSpike(tradingDay, (double)regime.VIX);
        var gapResult = _gapDetector.DetectGap(tradingDay, 100m, 100m, 100m, 100m); // Simplified for demo
        
        if (crisisMode != CrisisMode.Normal || vixSpike.IsSpike)
        {
            Console.WriteLine($"🚨 CRISIS MODE: {crisisMode} | VIX Spike: {vixSpike.IsSpike}");
            if (vixSpike.IsSpike) Console.WriteLine($"   VIX Spike: {vixSpike.Description}");
        }
        
        // === STRATEGY SELECTION ===
        var selectedStrategy = SelectOptimalStrategy(dayNumber, trajectory, regime, currentPnL);
        Console.WriteLine($"⚡ Strategy: {selectedStrategy.Name} | Expected Edge: {selectedStrategy.ExpectedEdge:F1}%");
        
        // === DYNAMIC POSITION SIZING ===
        var positionSize = _positionSizing.CalculatePositionSize(dayNumber, trajectory, currentPnL, regime);
        Console.WriteLine($"📈 Position Size: ${positionSize:F2} ({_positionSizing.GetSizingRationale()})");
        
        // === ENHANCED BATTLE SELECTION WITH CRISIS OVERRIDE ===
        var battleDecision = EvaluateBattleSelection(regime, selectedStrategy, dayNumber, crisisMode, vixSpike);
        if (!battleDecision.ShouldTrade)
        {
            Console.WriteLine($"🛑 NO-GO: {battleDecision.Reason}");
            var noTradeResult = new DayResult
            {
                Day = dayNumber,
                Date = tradingDay,
                Strategy = "No Trade",
                PnL = 0,
                Trades = 0,
                Reason = battleDecision.Reason
            };
            _dayResults.Add(noTradeResult);
            return noTradeResult;
        }
        
        // === EXECUTE TRADING STRATEGY ===
        Console.WriteLine($"✅ GO: {battleDecision.Reason}");
        var trades = await selectedStrategy.ExecuteTrading(conditions, positionSize, regime);
        
        var dayResult = new DayResult
        {
            Day = dayNumber,
            Date = tradingDay,
            Strategy = selectedStrategy.Name,
            PnL = trades.Sum(t => t.ExpectedPnL), // Use ExpectedPnL from strategy
            Trades = trades.Count,
            WinRate = trades.Count > 0 ? trades.Count(t => t.ExpectedPnL > 0) / (double)trades.Count * 100 : 0,
            MaxDrawdown = trades.Count > 0 ? trades.Min(t => t.ExpectedPnL) : 0,
            Regime = regime.RegimeType.ToString(),
            PositionSize = positionSize
        };
        
        _dayResults.Add(dayResult);
        
        // === FRAMEWORK PROGRESSION ANALYSIS ===
        AnalyzeFrameworkProgression(dayResult);
        
        return dayResult;
    }

    /// <summary>
    /// Analyze current performance trajectory and determine framework phase
    /// </summary>
    private PerformanceTrajectory AnalyzePerformanceTrajectory(int dayNumber, decimal currentPnL)
    {
        var expectedPnL = (TARGET_PROFIT / FRAMEWORK_DAYS) * dayNumber; // Linear expectation: $250/day
        var pnlRatio = currentPnL / expectedPnL;
        
        return dayNumber switch
        {
            <= SNIPER_ACTIVATION_DAY when pnlRatio < 0.5m => PerformanceTrajectory.SniperModeRequired,
            <= SNIPER_ACTIVATION_DAY when pnlRatio >= 0.8m => PerformanceTrajectory.OnTrack,
            <= ACCELERATION_DAY when pnlRatio >= 1.0m => PerformanceTrajectory.AheadOfSchedule,
            <= ACCELERATION_DAY when pnlRatio < 0.7m => PerformanceTrajectory.RecoveryMode,
            > ACCELERATION_DAY when currentPnL >= TARGET_PROFIT => PerformanceTrajectory.RiskAmplification,
            > ACCELERATION_DAY => PerformanceTrajectory.FinalPush,
            _ => PerformanceTrajectory.BaseEarningsPhase
        };
    }

    /// <summary>
    /// Select optimal strategy based on framework phase and market conditions
    /// </summary>
    private IStrategy SelectOptimalStrategy(int dayNumber, PerformanceTrajectory trajectory, MarketRegime regime, decimal currentPnL)
    {
        // === ENHANCED STRATEGY SELECTION WITH CRISIS AWARENESS ===
        var crisisMode = _crisisDetector.DetectCrisisMode(new MarketConditions(), regime);
        
        // Crisis strategies take priority
        if (crisisMode == CrisisMode.BlackSwan && regime.VIX > 80)
        {
            return _strategies.OfType<BlackSwanStrategy>().First();
        }
        
        if (crisisMode >= CrisisMode.Crisis && regime.VIX > 35)
        {
            return _strategies.OfType<VolatilityExpansionStrategy>().First();
        }
        
        // Original trajectory-based logic
        return trajectory switch
        {
            PerformanceTrajectory.SniperModeRequired => _strategies.OfType<SniperStrategy>().First(),
            PerformanceTrajectory.RiskAmplification => _strategies.OfType<VolatilityCrusher>().First(),
            PerformanceTrajectory.RecoveryMode => _strategies.OfType<PrecisionStrategy>().First(),
            PerformanceTrajectory.FinalPush => _strategies.OfType<RegimeAdaptive>().First(),
            _ => SelectRegimeBasedStrategy(regime, crisisMode)
        };
    }

    /// <summary>
    /// ENHANCED: Select strategy based on market regime AND crisis conditions
    /// </summary>
    private IStrategy SelectRegimeBasedStrategy(MarketRegime regime, CrisisMode crisisMode)
    {
        // Crisis conditions override normal regime logic
        if (crisisMode >= CrisisMode.Elevated)
        {
            return regime.VIX switch
            {
                > 50 => _strategies.OfType<BlackSwanStrategy>().First(),
                > 35 => _strategies.OfType<VolatilityExpansionStrategy>().First(),
                _ => _strategies.OfType<GhostStrategy>().First()
            };
        }
        
        // Normal regime-based selection
        return regime.RegimeType switch
        {
            RegimeType.LowVolatility when regime.TrendStrength < 0.3 => _strategies.OfType<VolatilityCrusher>().First(),
            RegimeType.HighVolatility when regime.Confidence > 0.8m => _strategies.OfType<GhostStrategy>().First(),
            RegimeType.Trending when regime.TrendStrength > 0.7 => _strategies.OfType<SniperStrategy>().First(),
            RegimeType.Ranging when regime.Confidence > 0.7m => _strategies.OfType<VolatilityCrusher>().First(),
            _ => _strategies.OfType<PrecisionStrategy>().First() // Default to precision
        };
    }

    /// <summary>
    /// ENHANCED: Evaluate whether to engage in battle with CRISIS OVERRIDE capability
    /// </summary>
    private BattleDecision EvaluateBattleSelection(MarketRegime regime, IStrategy strategy, int dayNumber, CrisisMode crisisMode, VIXSpikeResult vixSpike)
    {
        // === CRISIS MODE OVERRIDE LOGIC ===
        // This fixes the root cause: framework shutting down during market stress
        if (crisisMode != CrisisMode.Normal || vixSpike.IsSpike)
        {
            Console.WriteLine($"🚨 CRISIS OVERRIDE ACTIVATED - Trading despite low confidence");
            
            // Override confidence requirements in crisis
            var requiredConfidence = crisisMode switch
            {
                CrisisMode.BlackSwan => 0.1m,  // Trade with ANY confidence in black swan
                CrisisMode.Crisis => 0.2m,     // Very low confidence required
                CrisisMode.Elevated => 0.3m,   // Moderately low confidence
                _ => 0.6m                       // Normal confidence
            };
            
            if (regime.Confidence >= requiredConfidence)
            {
                // Prefer crisis-specific strategies
                if (strategy is BlackSwanStrategy || strategy is VolatilityExpansionStrategy)
                {
                    return BattleDecision.Go($"CRISIS STRATEGY: {strategy.Name} activated for {crisisMode}");
                }
                
                return BattleDecision.Go($"Crisis override: {strategy.Name} with {regime.Confidence:F1}% confidence");
            }
        }
        
        // === NORMAL MODE LOGIC (Existing) ===
        // High-confidence regime required for normal trading
        if (regime.Confidence < 0.6m)
            return BattleDecision.NoGo($"Low regime confidence ({regime.Confidence:F1}%) - waiting for clarity");
        
        // Strategy-specific edge requirements
        if (strategy.ExpectedEdge < 0.1)
            return BattleDecision.NoGo("Insufficient statistical edge");
        
        // VIX-based battle selection (UPDATED: Allow crisis strategies in high VIX)
        if (regime.VIX > 35m && !(strategy is GhostStrategy || strategy is BlackSwanStrategy || strategy is VolatilityExpansionStrategy))
            return BattleDecision.NoGo("High VIX - only crisis strategies authorized");
        
        // Economic events filter
        if (regime.HasMajorEvent && dayNumber > 18) // More conservative in final phase
            return BattleDecision.NoGo("Major economic event - avoiding late-phase risk");
        
        return BattleDecision.Go($"{strategy.Name} optimal for {regime.RegimeType}");
    }

    /// <summary>
    /// Analyze framework progression and provide strategic insights
    /// </summary>
    private void AnalyzeFrameworkProgression(DayResult dayResult)
    {
        var totalPnL = _dayResults.Sum(r => r.PnL);
        var averageDailyPnL = totalPnL / _dayResults.Count;
        var winRate = _dayResults.Count(r => r.PnL > 0) / (double)_dayResults.Count * 100;
        
        Console.WriteLine($"\n📈 Framework Metrics:");
        Console.WriteLine($"   Total P&L: ${totalPnL:F2} | Average Daily: ${averageDailyPnL:F2}");
        Console.WriteLine($"   Win Rate: {winRate:F1}% | Days Remaining: {FRAMEWORK_DAYS - _dayResults.Count}");
        Console.WriteLine($"   Target Progress: {(totalPnL / TARGET_PROFIT * 100):F1}%");
        
        // Performance trajectory analysis
        var remainingDays = FRAMEWORK_DAYS - _dayResults.Count;
        var requiredDailyAverage = remainingDays > 0 ? (TARGET_PROFIT - totalPnL) / remainingDays : 0;
        Console.WriteLine($"   Required Daily Average: ${requiredDailyAverage:F2}");
        
        if (requiredDailyAverage > 400)
            Console.WriteLine($"   ⚠️  HIGH PRESSURE: Need ${requiredDailyAverage:F2}/day - consider strategy escalation");
        else if (requiredDailyAverage < 150)
            Console.WriteLine($"   ✅ ON CRUISE: Need ${requiredDailyAverage:F2}/day - maintain discipline");
        
        // Provide strategic recommendations
        ProvideStrategicRecommendations(totalPnL, _dayResults.Count);
    }

    /// <summary>
    /// Provide strategic recommendations based on current performance
    /// </summary>
    private void ProvideStrategicRecommendations(decimal currentPnL, int daysCompleted)
    {
        var progressRatio = currentPnL / TARGET_PROFIT;
        var timeRatio = (decimal)daysCompleted / FRAMEWORK_DAYS;
        
        if (progressRatio / timeRatio > 1.2m && daysCompleted >= ACCELERATION_DAY)
        {
            Console.WriteLine("🚀 RECOMMENDATION: Target achieved early - implement risk amplification protocol");
        }
        else if (progressRatio / timeRatio < 0.5m && daysCompleted >= SNIPER_ACTIVATION_DAY)
        {
            Console.WriteLine("🎯 RECOMMENDATION: Activate sniper mode - focus on high-conviction trades");
        }
        else if (progressRatio / timeRatio < 0.8m && daysCompleted > 15)
        {
            Console.WriteLine("⚡ RECOMMENDATION: Enter recovery mode - precision strategy with increased position sizing");
        }
        else
        {
            Console.WriteLine("📊 RECOMMENDATION: Continue current strategy - framework on track");
        }
    }

    /// <summary>
    /// Generate comprehensive framework report
    /// </summary>
    public FrameworkReport GenerateReport()
    {
        var totalPnL = _dayResults.Sum(r => r.PnL);
        var winRate = _dayResults.Count(r => r.PnL > 0) / (double)_dayResults.Count * 100;
        var winningResults = _dayResults.Where(r => r.PnL > 0).ToList();
        var losingResults = _dayResults.Where(r => r.PnL < 0).ToList();
        
        var averageWin = winningResults.Any() ? winningResults.Average(r => r.PnL) : 0;
        var averageLoss = losingResults.Any() ? losingResults.Average(r => r.PnL) : 0;
        var maxDrawdown = _dayResults.Min(r => r.MaxDrawdown);
        
        return new FrameworkReport
        {
            TotalPnL = totalPnL,
            TargetAchievement = totalPnL / TARGET_PROFIT * 100,
            WinRate = winRate,
            AverageWin = averageWin,
            AverageLoss = averageLoss,
            ProfitFactor = Math.Abs(averageWin / averageLoss),
            MaxDrawdown = maxDrawdown,
            DaysCompleted = _dayResults.Count,
            StrategyDistribution = _dayResults.GroupBy(r => r.Strategy)
                .ToDictionary(g => g.Key, g => g.Count()),
            DayResults = _dayResults.ToList()
        };
    }
}

/// <summary>
/// Framework configuration parameters
/// </summary>
public class FrameworkConfig
{
    public decimal MaxDailyLoss { get; set; } = 300m;
    public decimal BasePositionSize { get; set; } = 1000m;
    public decimal MaxPositionSize { get; set; } = 2000m;
    public decimal VolatilityThreshold { get; set; } = 25m;
    public bool EnableRiskAmplification { get; set; } = true;
    public bool EnableSniperMode { get; set; } = true;
}

/// <summary>
/// Performance trajectory classifications
/// </summary>
public enum PerformanceTrajectory
{
    BaseEarningsPhase,      // Normal operation, on track
    OnTrack,                // Meeting expectations
    AheadOfSchedule,        // Exceeding expectations
    SniperModeRequired,     // Behind schedule, need precision
    RecoveryMode,           // Significant catch-up required
    RiskAmplification,      // Target achieved early, amplify risk
    FinalPush               // Final days, maximum effort
}

/// <summary>
/// Daily trading result
/// </summary>
public class DayResult
{
    public int Day { get; set; }
    public DateTime Date { get; set; }
    public string Strategy { get; set; } = "";
    public decimal PnL { get; set; }
    public int Trades { get; set; }
    public double WinRate { get; set; }
    public decimal MaxDrawdown { get; set; }
    public string Regime { get; set; } = "";
    public decimal PositionSize { get; set; }
    public string Reason { get; set; } = "";
}

/// <summary>
/// Battle selection decision
/// </summary>
public class BattleDecision
{
    public bool ShouldTrade { get; set; }
    public string Reason { get; set; } = "";
    
    public static BattleDecision Go(string reason) => new() { ShouldTrade = true, Reason = reason };
    public static BattleDecision NoGo(string reason) => new() { ShouldTrade = false, Reason = reason };
}

/// <summary>
/// Comprehensive framework performance report
/// </summary>
public class FrameworkReport
{
    public decimal TotalPnL { get; set; }
    public decimal TargetAchievement { get; set; }
    public double WinRate { get; set; }
    public decimal AverageWin { get; set; }
    public decimal AverageLoss { get; set; }
    public decimal ProfitFactor { get; set; }
    public decimal MaxDrawdown { get; set; }
    public int DaysCompleted { get; set; }
    public Dictionary<string, int> StrategyDistribution { get; set; } = new();
    public List<DayResult> DayResults { get; set; } = new();
}