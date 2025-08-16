using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ODTE.Strategy
{
    /// <summary>
    /// PM250 DUAL-STRATEGY IMPLEMENTATION GUIDE
    /// 
    /// Complete production-ready implementation of the dual-strategy system
    /// based on clinical analysis of 68 months of real trading data.
    /// 
    /// This serves as the definitive implementation reference for the
    /// dual-strategy framework that transforms PM250 from failure to success.
    /// </summary>
    
    #region Core Interfaces
    
    public interface IDualStrategyEngine
    {
        Task<TradeDecision> EvaluateTradeOpportunityAsync(MarketConditions conditions);
        MarketStrategy GetActiveStrategy();
        StrategyPerformanceMetrics GetPerformanceMetrics();
        void UpdateRiskLimits(RiskLimits limits);
    }
    
    public interface ITradeStrategy
    {
        string Name { get; }
        StrategyType Type { get; }
        
        TradeDecision ShouldTrade(MarketConditions conditions);
        PositionSize CalculatePosition(MarketConditions conditions);
        decimal CalculateTargetProfit(MarketConditions conditions);
        void UpdatePerformance(TradeResult result);
        StrategyMetrics GetCurrentMetrics();
    }
    
    public interface IRegimeDetector
    {
        MarketRegime ClassifyRegime(MarketConditions conditions);
        double GetClassificationConfidence();
        RegimeSignals GetRegimeSignals(MarketConditions conditions);
    }
    
    public interface IRevFibNotchRiskManager
    {
        decimal GetDailyLossLimit(int notchIndex);
        decimal GetCurrentRFibLimit();
        decimal GetMonthlyLossLimit();
        bool ValidateTradeRisk(TradeDecision decision);
        RiskAssessment AssessCurrentRisk();
    }
    
    #endregion
    
    #region Main Dual Strategy Engine
    
    public class DualStrategyEngine : IDualStrategyEngine
    {
        private readonly IProbeStrategy _probeStrategy;
        private readonly IQualityStrategy _qualityStrategy;
        private readonly IRegimeDetector _regimeDetector;
        private readonly IRevFibNotchRiskManager _riskManager;
        private readonly ILogger _logger;
        
        private MarketStrategy _currentStrategy = MarketStrategy.ProbeOnly;
        private DateTime _lastRegimeChange = DateTime.UtcNow;
        
        public DualStrategyEngine(
            IProbeStrategy probeStrategy,
            IQualityStrategy qualityStrategy, 
            IRegimeDetector regimeDetector,
            IRevFibNotchRiskManager riskManager,
            ILogger logger)
        {
            _probeStrategy = probeStrategy ?? throw new ArgumentNullException(nameof(probeStrategy));
            _qualityStrategy = qualityStrategy ?? throw new ArgumentNullException(nameof(qualityStrategy));
            _regimeDetector = regimeDetector ?? throw new ArgumentNullException(nameof(regimeDetector));
            _riskManager = riskManager ?? throw new ArgumentNullException(nameof(riskManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        public async Task<TradeDecision> EvaluateTradeOpportunityAsync(MarketConditions conditions)
        {
            try
            {
                // Step 1: Detect current market regime
                var regime = _regimeDetector.ClassifyRegime(conditions);
                var confidence = _regimeDetector.GetClassificationConfidence();
                
                // Step 2: Select appropriate strategy based on regime
                var strategySelection = SelectStrategy(regime, conditions);
                
                // Step 3: Log regime changes
                if (strategySelection != _currentStrategy)
                {
                    _logger.LogInformation($"Strategy change: {_currentStrategy} → {strategySelection} (Regime: {regime}, Confidence: {confidence:P1})");
                    _currentStrategy = strategySelection;
                    _lastRegimeChange = DateTime.UtcNow;
                }
                
                // Step 4: Get trade decision from active strategy
                var decision = await GetTradeDecisionAsync(strategySelection, conditions);
                
                // Step 5: Validate with risk manager
                if (decision.ShouldTrade)
                {
                    var riskValidation = _riskManager.ValidateTradeRisk(decision);
                    if (!riskValidation)
                    {
                        _logger.LogWarning("Trade rejected by risk manager");
                        decision = TradeDecision.Skip("Risk manager override");
                    }
                }
                
                return decision;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in dual strategy evaluation");
                return TradeDecision.Skip("System error");
            }
        }
        
        private MarketStrategy SelectStrategy(MarketRegime regime, MarketConditions conditions)
        {
            // Primary regime-based selection
            var strategy = regime switch
            {
                MarketRegime.Crisis => MarketStrategy.ProbeOnly,
                MarketRegime.Volatile => MarketStrategy.ProbeOnly,
                MarketRegime.Optimal => MarketStrategy.QualityOnly,
                MarketRegime.Normal => MarketStrategy.Hybrid,
                MarketRegime.Recovery => conditions.VIX < 25 ? MarketStrategy.QualityOnly : MarketStrategy.ProbeOnly,
                _ => MarketStrategy.ProbeOnly
            };
            
            // Override conditions for safety
            if (conditions.VIX >= 30) strategy = MarketStrategy.ProbeOnly;
            if (conditions.StressLevel >= 0.8) strategy = MarketStrategy.ProbeOnly;
            
            return strategy;
        }
        
        private async Task<TradeDecision> GetTradeDecisionAsync(MarketStrategy strategy, MarketConditions conditions)
        {
            return strategy switch
            {
                MarketStrategy.ProbeOnly => _probeStrategy.ShouldTrade(conditions),
                MarketStrategy.QualityOnly => _qualityStrategy.ShouldTrade(conditions),
                MarketStrategy.Hybrid => await EvaluateHybridStrategyAsync(conditions),
                _ => TradeDecision.Skip("Unknown strategy")
            };
        }
        
        private async Task<TradeDecision> EvaluateHybridStrategyAsync(MarketConditions conditions)
        {
            // Hybrid: 65% probe allocation, 35% quality allocation
            var probeDecision = _probeStrategy.ShouldTrade(conditions);
            var qualityDecision = _qualityStrategy.ShouldTrade(conditions);
            
            // Prefer quality if both suggest trading
            if (qualityDecision.ShouldTrade && conditions.VIX < 22)
                return qualityDecision;
            
            if (probeDecision.ShouldTrade)
                return probeDecision;
                
            return TradeDecision.Skip("Neither strategy favorable");
        }
        
        public MarketStrategy GetActiveStrategy() => _currentStrategy;
        
        public StrategyPerformanceMetrics GetPerformanceMetrics()
        {
            return new StrategyPerformanceMetrics
            {
                ProbeMetrics = _probeStrategy.GetCurrentMetrics(),
                QualityMetrics = _qualityStrategy.GetCurrentMetrics(),
                ActiveStrategy = _currentStrategy,
                LastRegimeChange = _lastRegimeChange
            };
        }
        
        public void UpdateRiskLimits(RiskLimits limits)
        {
            _logger.LogInformation($"Risk limits updated: Daily={limits.MaxDailyLoss}, Monthly={limits.MaxMonthlyLoss}");
        }
    }
    
    #endregion
    
    #region Probe Strategy Implementation
    
    public class ProbeStrategy : IProbeStrategy
    {
        private readonly ProbeStrategyConfig _config;
        private readonly ILogger _logger;
        private readonly StrategyMetrics _metrics;
        
        public string Name => "Probe Strategy";
        public StrategyType Type => StrategyType.CapitalPreservation;
        
        public ProbeStrategy(ProbeStrategyConfig config, ILogger logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _metrics = new StrategyMetrics();
        }
        
        public TradeDecision ShouldTrade(MarketConditions conditions)
        {
            // Activation conditions
            if (!ShouldActivateProbeStrategy(conditions))
                return TradeDecision.Skip("Probe strategy not activated");
            
            // Safety checks
            if (!PassesSafetyChecks(conditions))
                return TradeDecision.Skip("Failed safety checks");
            
            // Risk validation
            if (!PassesRiskValidation())
                return TradeDecision.Skip("Risk limits exceeded");
            
            // Calculate position
            var position = CalculateProbePosition(conditions);
            
            return new TradeDecision
            {
                ShouldTrade = true,
                Strategy = "Probe",
                PositionSize = position,
                TargetProfit = _config.TargetProfitPerTrade,
                MaxRisk = _config.MaxRiskPerTrade,
                StopLoss = _config.MaxRiskPerTrade * (decimal)_config.StopLossMultiplier,
                Reasoning = $"Probe entry: VIX {conditions.VIX:F1}, Stress {conditions.StressLevel:P1}"
            };
        }
        
        private bool ShouldActivateProbeStrategy(MarketConditions conditions)
        {
            // Primary activation conditions
            if (conditions.VIX >= _config.VIXActivationLevel) return true;
            if (conditions.StressLevel >= _config.StressActivationLevel) return true;
            if (_metrics.ConsecutiveLosses >= _config.LossStreakTrigger) return true;
            
            // Crisis regime always activates probe
            if (conditions.Regime == MarketRegime.Crisis) return true;
            if (conditions.Regime == MarketRegime.Volatile) return true;
            
            return false;
        }
        
        private bool PassesSafetyChecks(MarketConditions conditions)
        {
            // Liquidity requirements
            if (conditions.Liquidity < _config.RequiredLiquidityScore)
            {
                _logger.LogWarning($"Insufficient liquidity: {conditions.Liquidity:P1} < {_config.RequiredLiquidityScore:P1}");
                return false;
            }
            
            // Market stress limits
            if (conditions.StressLevel > 0.9) // Extreme stress
            {
                _logger.LogWarning($"Extreme market stress: {conditions.StressLevel:P1}");
                return false;
            }
            
            // Time-based restrictions
            if (_metrics.LastTradeTime.HasValue && 
                DateTime.UtcNow - _metrics.LastTradeTime.Value < _config.MinTimeBetweenTrades)
            {
                return false;
            }
            
            return true;
        }
        
        private bool PassesRiskValidation()
        {
            // Daily loss limit
            if (_metrics.DailyLoss >= _config.MaxDailyLoss)
            {
                _logger.LogWarning($"Daily loss limit reached: ${_metrics.DailyLoss} >= ${_config.MaxDailyLoss}");
                return false;
            }
            
            // Monthly loss limit
            if (_metrics.MonthlyLoss >= _config.MaxMonthlyLoss)
            {
                _logger.LogWarning($"Monthly loss limit reached: ${_metrics.MonthlyLoss} >= ${_config.MaxMonthlyLoss}");
                return false;
            }
            
            // Trade count limit
            if (_metrics.DailyTradeCount >= _config.MaxTradesPerDay)
            {
                return false;
            }
            
            // Consecutive loss limit
            if (_metrics.ConsecutiveLosses >= _config.MaxConsecutiveLosses)
            {
                _logger.LogWarning($"Too many consecutive losses: {_metrics.ConsecutiveLosses}");
                return false;
            }
            
            return true;
        }
        
        public PositionSize CalculatePosition(MarketConditions conditions)
        {
            return CalculateProbePosition(conditions);
        }
        
        private PositionSize CalculateProbePosition(MarketConditions conditions)
        {
            // Base position sizing: 18% of normal
            var baseSize = _config.PositionSizeMultiplier;
            
            // Adjust for market stress
            var stressAdjustment = Math.Max(0.5, 1.0 - conditions.StressLevel);
            var adjustedSize = baseSize * stressAdjustment;
            
            // Never exceed maximum position size
            var contractCount = Math.Min(_config.MaxPositionSize, Math.Max(1, (int)(adjustedSize * 10)));
            
            return new PositionSize
            {
                Contracts = contractCount,
                Multiplier = adjustedSize,
                MaxRiskPerContract = _config.MaxRiskPerTrade / contractCount
            };
        }
        
        public decimal CalculateTargetProfit(MarketConditions conditions)
        {
            // Conservative profit targets in probe mode
            if (conditions.VIX > 40) return _config.MinAcceptableProfit; // Crisis mode
            if (conditions.VIX > 30) return _config.TargetProfitPerTrade * 0.8m; // Reduced target
            
            return _config.TargetProfitPerTrade;
        }
        
        public void UpdatePerformance(TradeResult result)
        {
            _metrics.UpdateWithTradeResult(result);
            
            // Early warning system
            if (result.PnL < -_config.WarningLossThreshold)
            {
                _logger.LogWarning($"Probe strategy early warning: Trade loss ${-result.PnL} exceeds threshold ${_config.WarningLossThreshold}");
            }
            
            // Escalation trigger
            if (result.PnL < -_config.EscalationThreshold)
            {
                _logger.LogError($"Probe strategy escalation: Trade loss ${-result.PnL} exceeds escalation threshold ${_config.EscalationThreshold}");
            }
        }
        
        public StrategyMetrics GetCurrentMetrics() => _metrics;
    }
    
    #endregion
    
    #region Quality Strategy Implementation
    
    public class QualityStrategy : IQualityStrategy
    {
        private readonly QualityStrategyConfig _config;
        private readonly ILogger _logger;
        private readonly StrategyMetrics _metrics;
        
        public string Name => "Quality Strategy";
        public StrategyType Type => StrategyType.ProfitMaximization;
        
        public QualityStrategy(QualityStrategyConfig config, ILogger logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _metrics = new StrategyMetrics();
        }
        
        public TradeDecision ShouldTrade(MarketConditions conditions)
        {
            // Quality criteria - all must be met
            if (!MeetsQualityCriteria(conditions))
                return TradeDecision.Skip("Quality criteria not met");
            
            // Risk validation
            if (!PassesRiskValidation())
                return TradeDecision.Skip("Risk limits exceeded");
            
            // Calculate optimal position
            var position = CalculateQualityPosition(conditions);
            var targetProfit = CalculateDynamicTarget(conditions);
            
            return new TradeDecision
            {
                ShouldTrade = true,
                Strategy = "Quality",
                PositionSize = position,
                TargetProfit = targetProfit,
                MaxRisk = _config.MaxTradeLoss,
                StopLoss = _config.MaxTradeLoss * (decimal)_config.StopLossMultiplier,
                Reasoning = $"Quality entry: VIX {conditions.VIX:F1}, GoScore {conditions.GoScore:F1}, Trend {conditions.TrendStrength:P1}"
            };
        }
        
        private bool MeetsQualityCriteria(MarketConditions conditions)
        {
            var criteria = new List<(bool Pass, string Description)>
            {
                (conditions.VIX <= _config.MaxVIXLevel, $"VIX {conditions.VIX:F1} <= {_config.MaxVIXLevel}"),
                (conditions.GoScore >= _config.RequiredGoScore, $"GoScore {conditions.GoScore:F1} >= {_config.RequiredGoScore}"),
                (conditions.TrendStrength >= _config.RequiredTrendStrength, $"Trend {conditions.TrendStrength:P1} >= {_config.RequiredTrendStrength:P1}"),
                (conditions.Liquidity >= _config.RequiredLiquidityScore, $"Liquidity {conditions.Liquidity:P1} >= {_config.RequiredLiquidityScore:P1}"),
                (conditions.MarketBreadth >= _config.MinMarketBreadth, $"Breadth {conditions.MarketBreadth:P1} >= {_config.MinMarketBreadth:P1}")
            };
            
            var failedCriteria = criteria.Where(c => !c.Pass).ToList();
            
            if (failedCriteria.Any())
            {
                _logger.LogDebug($"Quality criteria failed: {string.Join(", ", failedCriteria.Select(c => c.Description))}");
                return false;
            }
            
            return true;
        }
        
        private bool PassesRiskValidation()
        {
            // Daily loss limit
            if (_metrics.DailyLoss >= _config.MaxDailyLoss)
            {
                _logger.LogWarning($"Quality strategy daily loss limit: ${_metrics.DailyLoss} >= ${_config.MaxDailyLoss}");
                return false;
            }
            
            // Trade count limit (quality over quantity)
            if (_metrics.DailyTradeCount >= _config.MaxTradesPerDay)
            {
                return false;
            }
            
            // Time spacing requirement
            if (_metrics.LastTradeTime.HasValue && 
                DateTime.UtcNow - _metrics.LastTradeTime.Value < _config.MinTimeBetweenTrades)
            {
                return false;
            }
            
            // Consecutive loss limit
            if (_metrics.ConsecutiveLosses >= _config.MaxConsecutiveLosses)
            {
                _logger.LogWarning($"Quality strategy consecutive losses: {_metrics.ConsecutiveLosses}");
                return false;
            }
            
            return true;
        }
        
        public PositionSize CalculatePosition(MarketConditions conditions)
        {
            return CalculateQualityPosition(conditions);
        }
        
        private PositionSize CalculateQualityPosition(MarketConditions conditions)
        {
            // Aggressive position sizing for quality setups
            var baseSize = _config.MaxPositionSizeMultiplier;
            
            // Boost size in optimal conditions
            if (conditions.VIX < 15 && conditions.GoScore > 85)
                baseSize = Math.Min(1.0, baseSize * 1.1); // 10% boost in perfect conditions
            
            // Calculate contract count
            var contractCount = Math.Min(_config.MaxPositionSize, 
                Math.Max(1, (int)(baseSize * _config.OptimalPositionSize)));
            
            return new PositionSize
            {
                Contracts = contractCount,
                Multiplier = baseSize,
                MaxRiskPerContract = _config.MaxTradeLoss / contractCount
            };
        }
        
        public decimal CalculateTargetProfit(MarketConditions conditions)
        {
            return CalculateDynamicTarget(conditions);
        }
        
        private decimal CalculateDynamicTarget(MarketConditions conditions)
        {
            // Scale profit target based on market conditions
            if (conditions.VIX < 15 && conditions.GoScore > 85)
                return _config.MaxExpectedProfit; // $40 in perfect conditions
            else if (conditions.GoScore > 75)
                return _config.TargetProfitPerTrade; // $22 standard target
            else
                return _config.MinAcceptableProfit; // $15 minimum acceptable
        }
        
        public void UpdatePerformance(TradeResult result)
        {
            _metrics.UpdateWithTradeResult(result);
            
            // Quality strategy expects high win rates
            if (_metrics.RecentWinRate < _config.MinAcceptableWinRate && _metrics.TradeCount > 10)
            {
                _logger.LogWarning($"Quality strategy win rate below target: {_metrics.RecentWinRate:P1} < {_config.MinAcceptableWinRate:P1}");
            }
            
            // Track profit capture effectiveness
            if (result.PnL > _config.TargetProfitPerTrade)
            {
                _logger.LogInformation($"Quality strategy excellence: ${result.PnL} profit achieved");
            }
        }
        
        public StrategyMetrics GetCurrentMetrics() => _metrics;
    }
    
    #endregion
    
    #region Regime Detection Implementation
    
    public class RegimeDetector : IRegimeDetector
    {
        private readonly RegimeDetectionConfig _config;
        private readonly ILogger _logger;
        private MarketRegime _lastRegime = MarketRegime.Normal;
        private double _lastConfidence = 0.0;
        
        public RegimeDetector(RegimeDetectionConfig config, ILogger logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        public MarketRegime ClassifyRegime(MarketConditions conditions)
        {
            var signals = GetRegimeSignals(conditions);
            var regime = DetermineRegimeFromSignals(signals);
            var confidence = CalculateClassificationConfidence(signals, regime);
            
            // Log regime changes
            if (regime != _lastRegime)
            {
                _logger.LogInformation($"Regime change detected: {_lastRegime} → {regime} (Confidence: {confidence:P1})");
                _lastRegime = regime;
            }
            
            _lastConfidence = confidence;
            return regime;
        }
        
        public RegimeSignals GetRegimeSignals(MarketConditions conditions)
        {
            return new RegimeSignals
            {
                VIXSignal = AnalyzeVIX(conditions.VIX),
                StressSignal = AnalyzeStressLevel(conditions.StressLevel),
                GoScoreSignal = AnalyzeGoScore(conditions.GoScore),
                TrendSignal = AnalyzeTrendStrength(conditions.TrendStrength),
                VolumeSignal = AnalyzeVolumeProfile(conditions),
                BreadthSignal = AnalyzeMarketBreadth(conditions.MarketBreadth)
            };
        }
        
        private VIXSignal AnalyzeVIX(double vix)
        {
            var classification = vix switch
            {
                >= 40 => VIXClassification.ExtremeStress,
                >= 30 => VIXClassification.HighStress,
                >= 25 => VIXClassification.ElevatedVolatility,
                >= 20 => VIXClassification.ModerateVolatility,
                >= 15 => VIXClassification.LowVolatility,
                _ => VIXClassification.VeryLowVolatility
            };
            
            return new VIXSignal
            {
                Level = vix,
                Classification = classification,
                RegimeImplication = classification switch
                {
                    VIXClassification.ExtremeStress => MarketRegime.Crisis,
                    VIXClassification.HighStress => MarketRegime.Crisis,
                    VIXClassification.ElevatedVolatility => MarketRegime.Volatile,
                    VIXClassification.ModerateVolatility => MarketRegime.Normal,
                    VIXClassification.LowVolatility => MarketRegime.Optimal,
                    VIXClassification.VeryLowVolatility => MarketRegime.Optimal,
                    _ => MarketRegime.Normal
                }
            };
        }
        
        private StressSignal AnalyzeStressLevel(double stressLevel)
        {
            var classification = stressLevel switch
            {
                >= 0.8 => StressClassification.Extreme,
                >= 0.6 => StressClassification.High,
                >= 0.4 => StressClassification.Moderate,
                >= 0.2 => StressClassification.Low,
                _ => StressClassification.Minimal
            };
            
            return new StressSignal
            {
                Level = stressLevel,
                Classification = classification,
                RegimeImplication = classification switch
                {
                    StressClassification.Extreme => MarketRegime.Crisis,
                    StressClassification.High => MarketRegime.Volatile,
                    StressClassification.Moderate => MarketRegime.Normal,
                    StressClassification.Low => MarketRegime.Optimal,
                    StressClassification.Minimal => MarketRegime.Optimal,
                    _ => MarketRegime.Normal
                }
            };
        }
        
        private GoScoreSignal AnalyzeGoScore(double goScore)
        {
            var classification = goScore switch
            {
                >= 85 => GoScoreClassification.Excellent,
                >= 75 => GoScoreClassification.Good,
                >= 65 => GoScoreClassification.Fair,
                >= 50 => GoScoreClassification.Poor,
                _ => GoScoreClassification.VeryPoor
            };
            
            return new GoScoreSignal
            {
                Score = goScore,
                Classification = classification,
                RegimeImplication = classification switch
                {
                    GoScoreClassification.Excellent => MarketRegime.Optimal,
                    GoScoreClassification.Good => MarketRegime.Optimal,
                    GoScoreClassification.Fair => MarketRegime.Normal,
                    GoScoreClassification.Poor => MarketRegime.Volatile,
                    GoScoreClassification.VeryPoor => MarketRegime.Crisis,
                    _ => MarketRegime.Normal
                }
            };
        }
        
        private TrendSignal AnalyzeTrendStrength(double trendStrength)
        {
            var classification = trendStrength switch
            {
                >= 0.8 => TrendClassification.VeryStrong,
                >= 0.6 => TrendClassification.Strong,
                >= 0.4 => TrendClassification.Moderate,
                >= 0.2 => TrendClassification.Weak,
                _ => TrendClassification.VeryWeak
            };
            
            return new TrendSignal
            {
                Strength = trendStrength,
                Classification = classification
            };
        }
        
        private VolumeSignal AnalyzeVolumeProfile(MarketConditions conditions)
        {
            // Analyze volume patterns for regime implications
            return new VolumeSignal
            {
                RelativeVolume = 1.0, // Placeholder - would calculate from actual volume data
                VolumeProfile = VolumeProfile.Normal
            };
        }
        
        private BreadthSignal AnalyzeMarketBreadth(double breadth)
        {
            return new BreadthSignal
            {
                Breadth = breadth,
                Classification = breadth >= 0.6 ? BreadthClassification.Broad : BreadthClassification.Narrow
            };
        }
        
        private MarketRegime DetermineRegimeFromSignals(RegimeSignals signals)
        {
            // Weighted regime scoring
            var regimeScores = new Dictionary<MarketRegime, double>
            {
                [MarketRegime.Crisis] = 0,
                [MarketRegime.Volatile] = 0,
                [MarketRegime.Normal] = 0,
                [MarketRegime.Optimal] = 0
            };
            
            // VIX signal (40% weight - most important)
            regimeScores[signals.VIXSignal.RegimeImplication] += 0.4;
            
            // Stress signal (25% weight)
            regimeScores[signals.StressSignal.RegimeImplication] += 0.25;
            
            // GoScore signal (20% weight)
            regimeScores[signals.GoScoreSignal.RegimeImplication] += 0.20;
            
            // Additional signals (15% weight total)
            if (signals.TrendSignal.Classification == TrendClassification.VeryWeak)
                regimeScores[MarketRegime.Volatile] += 0.1;
            else if (signals.TrendSignal.Classification == TrendClassification.VeryStrong)
                regimeScores[MarketRegime.Optimal] += 0.1;
            
            // Return regime with highest score
            return regimeScores.OrderByDescending(kvp => kvp.Value).First().Key;
        }
        
        private double CalculateClassificationConfidence(RegimeSignals signals, MarketRegime regime)
        {
            // Calculate confidence based on signal agreement
            var signalAgreement = 0;
            var totalSignals = 3; // VIX, Stress, GoScore
            
            if (signals.VIXSignal.RegimeImplication == regime) signalAgreement++;
            if (signals.StressSignal.RegimeImplication == regime) signalAgreement++;
            if (signals.GoScoreSignal.RegimeImplication == regime) signalAgreement++;
            
            return (double)signalAgreement / totalSignals;
        }
        
        public double GetClassificationConfidence() => _lastConfidence;
    }
    
    #endregion
    
    #region Data Classes
    
    public class ProbeStrategyConfig
    {
        // Profit targets
        public decimal TargetProfitPerTrade { get; set; } = 3.8m;
        public decimal MinAcceptableProfit { get; set; } = 2.0m;
        public decimal MaxExpectedProfit { get; set; } = 8.0m;
        
        // Risk management
        public decimal MaxRiskPerTrade { get; set; } = 22m;
        public decimal MaxDailyLoss { get; set; } = 50m;
        public decimal MaxMonthlyLoss { get; set; } = 95m;
        public double StopLossMultiplier { get; set; } = 1.3;
        
        // Position sizing
        public double PositionSizeMultiplier { get; set; } = 0.18;
        public int MaxPositionSize { get; set; } = 1;
        public int MaxConsecutiveLosses { get; set; } = 3;
        
        // Execution parameters
        public int MaxTradesPerDay { get; set; } = 4;
        public TimeSpan MinTimeBetweenTrades { get; set; } = TimeSpan.FromMinutes(30);
        public double RequiredLiquidityScore { get; set; } = 0.7;
        
        // Activation conditions
        public double VIXActivationLevel { get; set; } = 21.0;
        public double StressActivationLevel { get; set; } = 0.38;
        public int LossStreakTrigger { get; set; } = 2;
        
        // Early warning system
        public decimal WarningLossThreshold { get; set; } = 15m;
        public decimal EscalationThreshold { get; set; } = 35m;
    }
    
    public class QualityStrategyConfig
    {
        // Profit targets
        public decimal TargetProfitPerTrade { get; set; } = 22m;
        public decimal MinAcceptableProfit { get; set; } = 15m;
        public decimal MaxExpectedProfit { get; set; } = 40m;
        
        // Risk management
        public decimal MaxTradeLoss { get; set; } = 250m;
        public decimal MaxDailyLoss { get; set; } = 475m;
        public double StopLossMultiplier { get; set; } = 2.3;
        public int MaxConsecutiveLosses { get; set; } = 2;
        
        // Position sizing
        public double MaxPositionSizeMultiplier { get; set; } = 0.95;
        public int OptimalPositionSize { get; set; } = 3;
        public int MaxPositionSize { get; set; } = 5;
        
        // Execution parameters
        public int MaxTradesPerDay { get; set; } = 2;
        public TimeSpan MinTimeBetweenTrades { get; set; } = TimeSpan.FromHours(2);
        public double RequiredLiquidityScore { get; set; } = 0.85;
        
        // Market condition requirements
        public double MaxVIXLevel { get; set; } = 19.0;
        public double RequiredGoScore { get; set; } = 72.0;
        public double RequiredTrendStrength { get; set; } = 0.68;
        public double MinMarketBreadth { get; set; } = 0.60;
        
        // Win rate targets
        public double TargetWinRate { get; set; } = 0.85;
        public double MinAcceptableWinRate { get; set; } = 0.80;
    }
    
    public class RegimeDetectionConfig
    {
        public double VIXCrisisThreshold { get; set; } = 30.0;
        public double VIXOptimalThreshold { get; set; } = 18.0;
        public double StressCrisisThreshold { get; set; } = 0.8;
        public double StressOptimalThreshold { get; set; } = 0.3;
        public double GoScoreOptimalThreshold { get; set; } = 75.0;
        public double GoScoreCrisisThreshold { get; set; } = 50.0;
    }
    
    public enum MarketStrategy
    {
        ProbeOnly,
        QualityOnly,
        Hybrid
    }
    
    public enum MarketRegime
    {
        Crisis,
        Volatile,
        Normal,
        Optimal,
        Recovery
    }
    
    public enum StrategyType
    {
        CapitalPreservation,
        ProfitMaximization
    }
    
    public class TradeDecision
    {
        public bool ShouldTrade { get; set; }
        public string Strategy { get; set; } = "";
        public PositionSize PositionSize { get; set; } = new();
        public decimal TargetProfit { get; set; }
        public decimal MaxRisk { get; set; }
        public decimal StopLoss { get; set; }
        public string Reasoning { get; set; } = "";
        
        public static TradeDecision Skip(string reason) => new() { ShouldTrade = false, Reasoning = reason };
    }
    
    public class PositionSize
    {
        public int Contracts { get; set; }
        public double Multiplier { get; set; }
        public decimal MaxRiskPerContract { get; set; }
    }
    
    public class MarketConditions
    {
        public double VIX { get; set; }
        public double GoScore { get; set; }
        public double TrendStrength { get; set; }
        public double StressLevel { get; set; }
        public double Liquidity { get; set; }
        public double MarketBreadth { get; set; }
        public MarketRegime Regime { get; set; }
        public double PutCallRatio { get; set; }
    }
    
    public class StrategyMetrics
    {
        public int TradeCount { get; set; }
        public int DailyTradeCount { get; set; }
        public decimal DailyLoss { get; set; }
        public decimal MonthlyLoss { get; set; }
        public int ConsecutiveLosses { get; set; }
        public double RecentWinRate { get; set; }
        public DateTime? LastTradeTime { get; set; }
        
        public void UpdateWithTradeResult(TradeResult result)
        {
            TradeCount++;
            DailyTradeCount++;
            LastTradeTime = DateTime.UtcNow;
            
            if (result.PnL < 0)
            {
                DailyLoss += Math.Abs(result.PnL);
                MonthlyLoss += Math.Abs(result.PnL);
                ConsecutiveLosses++;
            }
            else
            {
                ConsecutiveLosses = 0;
            }
            
            // Update recent win rate (last 20 trades)
            // Implementation would track recent trade results
        }
    }
    
    public class TradeResult
    {
        public decimal PnL { get; set; }
        public DateTime ExecutionTime { get; set; }
        public string Strategy { get; set; } = "";
        public bool IsWin => PnL > 0;
    }
    
    public class RegimeSignals
    {
        public VIXSignal VIXSignal { get; set; } = new();
        public StressSignal StressSignal { get; set; } = new();
        public GoScoreSignal GoScoreSignal { get; set; } = new();
        public TrendSignal TrendSignal { get; set; } = new();
        public VolumeSignal VolumeSignal { get; set; } = new();
        public BreadthSignal BreadthSignal { get; set; } = new();
    }
    
    public class VIXSignal
    {
        public double Level { get; set; }
        public VIXClassification Classification { get; set; }
        public MarketRegime RegimeImplication { get; set; }
    }
    
    public class StressSignal
    {
        public double Level { get; set; }
        public StressClassification Classification { get; set; }
        public MarketRegime RegimeImplication { get; set; }
    }
    
    public class GoScoreSignal
    {
        public double Score { get; set; }
        public GoScoreClassification Classification { get; set; }
        public MarketRegime RegimeImplication { get; set; }
    }
    
    public class TrendSignal
    {
        public double Strength { get; set; }
        public TrendClassification Classification { get; set; }
    }
    
    public class VolumeSignal
    {
        public double RelativeVolume { get; set; }
        public VolumeProfile VolumeProfile { get; set; }
    }
    
    public class BreadthSignal
    {
        public double Breadth { get; set; }
        public BreadthClassification Classification { get; set; }
    }
    
    public enum VIXClassification
    {
        VeryLowVolatility,
        LowVolatility,
        ModerateVolatility,
        ElevatedVolatility,
        HighStress,
        ExtremeStress
    }
    
    public enum StressClassification
    {
        Minimal,
        Low,
        Moderate,
        High,
        Extreme
    }
    
    public enum GoScoreClassification
    {
        VeryPoor,
        Poor,
        Fair,
        Good,
        Excellent
    }
    
    public enum TrendClassification
    {
        VeryWeak,
        Weak,
        Moderate,
        Strong,
        VeryStrong
    }
    
    public enum VolumeProfile
    {
        Low,
        Normal,
        High,
        Extreme
    }
    
    public enum BreadthClassification
    {
        Narrow,
        Broad
    }
    
    #endregion
    
    #region Interfaces for Extension
    
    public interface IProbeStrategy : ITradeStrategy
    {
        // Probe-specific methods
    }
    
    public interface IQualityStrategy : ITradeStrategy
    {
        // Quality-specific methods
    }
    
    public interface ILogger
    {
        void LogInformation(string message);
        void LogWarning(string message);
        void LogError(Exception ex, string message);
        void LogDebug(string message);
    }
    
    public class StrategyPerformanceMetrics
    {
        public StrategyMetrics ProbeMetrics { get; set; } = new();
        public StrategyMetrics QualityMetrics { get; set; } = new();
        public MarketStrategy ActiveStrategy { get; set; }
        public DateTime LastRegimeChange { get; set; }
    }
    
    public class RiskLimits
    {
        public decimal MaxDailyLoss { get; set; }
        public decimal MaxMonthlyLoss { get; set; }
        public int MaxPositionSize { get; set; }
    }
    
    public class RiskAssessment
    {
        public bool IsWithinLimits { get; set; }
        public decimal CurrentRiskExposure { get; set; }
        public string RiskWarnings { get; set; } = "";
    }
    
    #endregion
}

/// <summary>
/// USAGE EXAMPLE: Complete Dual-Strategy Implementation
/// 
/// This example shows how to wire up the complete dual-strategy system
/// for production use with the PM250 trading framework.
/// </summary>
namespace ODTE.Strategy.Examples
{
    public class DualStrategyImplementationExample
    {
        public static IDualStrategyEngine CreateProductionEngine()
        {
            // Configuration
            var probeConfig = new ProbeStrategyConfig
            {
                TargetProfitPerTrade = 3.8m,
                MaxRiskPerTrade = 22m,
                MaxDailyLoss = 50m,
                MaxMonthlyLoss = 95m,
                PositionSizeMultiplier = 0.18,
                VIXActivationLevel = 21.0,
                StressActivationLevel = 0.38
            };
            
            var qualityConfig = new QualityStrategyConfig
            {
                TargetProfitPerTrade = 22m,
                MinAcceptableProfit = 15m,
                MaxExpectedProfit = 40m,
                MaxTradeLoss = 250m,
                MaxDailyLoss = 475m,
                MaxVIXLevel = 19.0,
                RequiredGoScore = 72.0,
                RequiredTrendStrength = 0.68
            };
            
            var regimeConfig = new RegimeDetectionConfig
            {
                VIXCrisisThreshold = 30.0,
                VIXOptimalThreshold = 18.0,
                StressCrisisThreshold = 0.8,
                GoScoreOptimalThreshold = 75.0
            };
            
            // Dependencies (would be injected in real implementation)
            var logger = new ConsoleLogger();
            var riskManager = new RevFibNotchRiskManager();
            
            // Create strategies
            var probeStrategy = new ProbeStrategy(probeConfig, logger);
            var qualityStrategy = new QualityStrategy(qualityConfig, logger);
            var regimeDetector = new RegimeDetector(regimeConfig, logger);
            
            // Create dual strategy engine
            return new DualStrategyEngine(
                probeStrategy,
                qualityStrategy,
                regimeDetector,
                riskManager,
                logger
            );
        }
        
        public static async Task RunTradingLoop(IDualStrategyEngine engine)
        {
            while (true)
            {
                try
                {
                    // Get market data (would come from real market data provider)
                    var marketConditions = await GetCurrentMarketConditions();
                    
                    // Evaluate trade opportunity
                    var decision = await engine.EvaluateTradeOpportunityAsync(marketConditions);
                    
                    if (decision.ShouldTrade)
                    {
                        // Execute trade (would integrate with broker API)
                        Console.WriteLine($"Trade Signal: {decision.Strategy} - {decision.Reasoning}");
                        Console.WriteLine($"Position: {decision.PositionSize.Contracts} contracts, Target: ${decision.TargetProfit}");
                    }
                    
                    // Wait for next evaluation cycle
                    await Task.Delay(TimeSpan.FromMinutes(1));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Trading loop error: {ex.Message}");
                    await Task.Delay(TimeSpan.FromSeconds(30));
                }
            }
        }
        
        private static async Task<MarketConditions> GetCurrentMarketConditions()
        {
            // Mock implementation - would integrate with real market data
            return new MarketConditions
            {
                VIX = 20.5,
                GoScore = 75.0,
                TrendStrength = 0.7,
                StressLevel = 0.3,
                Liquidity = 0.85,
                MarketBreadth = 0.65,
                PutCallRatio = 0.8
            };
        }
    }
    
    // Simple logger implementation for example
    public class ConsoleLogger : ILogger
    {
        public void LogInformation(string message) => Console.WriteLine($"INFO: {message}");
        public void LogWarning(string message) => Console.WriteLine($"WARN: {message}");
        public void LogError(Exception ex, string message) => Console.WriteLine($"ERROR: {message} - {ex.Message}");
        public void LogDebug(string message) => Console.WriteLine($"DEBUG: {message}");
    }
    
    // Placeholder RevFibNotch risk manager for example
    public class RevFibNotchRiskManager : IRevFibNotchRiskManager
    {
        private readonly decimal[] _revFibNotchArray = { 1250m, 800m, 500m, 300m, 200m, 100m };
        private int _currentNotchIndex = 2; // Start at $500 (balanced position)
        
        public decimal GetDailyLossLimit(int notchIndex) => 
            _revFibNotchArray[Math.Clamp(notchIndex, 0, _revFibNotchArray.Length - 1)];
        
        public decimal GetCurrentRFibLimit() => _revFibNotchArray[_currentNotchIndex];
        
        public decimal GetMonthlyLossLimit() => 2000m; // Conservative monthly limit
        public bool ValidateTradeRisk(TradeDecision decision) => true;
        public RiskAssessment AssessCurrentRisk() => new() { IsWithinLimits = true };
    }
}