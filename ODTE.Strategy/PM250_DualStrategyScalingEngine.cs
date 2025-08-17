namespace ODTE.Strategy
{
    /// <summary>
    /// PM250 Dual Strategy Scaling Engine - Implements ScaleHighWithManagedRisk framework
    /// Provides systematic 10x scaling from $284.66 baseline to $2,847 monthly target
    /// Maintains risk management integrity while progressively increasing position sizing
    /// </summary>
    public class PM250_DualStrategyScalingEngine
    {
        private readonly IReverseFibonacciRiskManager _riskManager;
        private readonly IPositiveProbeDetector _probeDetector;
        private readonly ICorrelationBudgetManager _correlationManager;
        private readonly IQualityEntryFilter _qualityFilter;
        private readonly IDualLaneExitManager _exitManager;

        private ScalingConfiguration _config;
        private SessionStats _currentSession;
        private EscalationLevel _currentLevel;
        private DateTime _cooldownUntil;
        private List<Position> _openPositions;

        public PM250_DualStrategyScalingEngine(
            IReverseFibonacciRiskManager riskManager,
            IPositiveProbeDetector probeDetector,
            ICorrelationBudgetManager correlationManager,
            IQualityEntryFilter qualityFilter,
            IDualLaneExitManager exitManager)
        {
            _riskManager = riskManager ?? throw new ArgumentNullException(nameof(riskManager));
            _probeDetector = probeDetector ?? throw new ArgumentNullException(nameof(probeDetector));
            _correlationManager = correlationManager ?? throw new ArgumentNullException(nameof(correlationManager));
            _qualityFilter = qualityFilter ?? throw new ArgumentNullException(nameof(qualityFilter));
            _exitManager = exitManager ?? throw new ArgumentNullException(nameof(exitManager));

            _openPositions = new List<Position>();
            _currentLevel = EscalationLevel.Level0;
            _cooldownUntil = DateTime.MinValue;

            // Initialize with Phase 1 configuration
            _config = CreatePhase1Configuration();
        }

        /// <summary>
        /// Configure the engine for a specific scaling phase
        /// </summary>
        public void ConfigureForPhase(ScalingPhase phase)
        {
            _config = phase switch
            {
                ScalingPhase.Foundation => CreatePhase1Configuration(),
                ScalingPhase.Escalation => CreatePhase2Configuration(),
                ScalingPhase.Quality => CreatePhase3Configuration(),
                ScalingPhase.Maximum => CreatePhase4Configuration(),
                _ => throw new ArgumentException($"Unknown scaling phase: {phase}")
            };

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Configured for {phase} phase: Target={_config.MonthlyTarget:C}, RFib={string.Join("/", _config.RFibLimits.Select(x => x.ToString("C0")))}");
        }

        /// <summary>
        /// Process a trading opportunity and determine if/how to execute
        /// </summary>
        public ScalingTradeDecision ProcessTradeOpportunity(TradeSetup setup)
        {
            UpdateSessionStats();

            // Core risk management - absolute constraints
            if (!ValidateAbsoluteConstraints(setup))
            {
                return ScalingTradeDecision.Reject("Absolute constraints violated");
            }

            // Determine trade lane (Probe vs Quality/Punch)
            var tradeLane = DetermineTradeLane(setup);

            // Check escalation level and permissions
            var escalationLevel = ComputeEscalationLevel();

            // Calculate position sizing
            var positionSize = CalculatePositionSize(setup, tradeLane, escalationLevel);

            if (positionSize <= 0)
            {
                return ScalingTradeDecision.Reject("Insufficient position size");
            }

            // Final validations
            if (!ValidateCorrelationBudget(setup, positionSize) ||
                !ValidateQualityRequirements(setup, tradeLane))
            {
                return ScalingTradeDecision.Reject("Quality or correlation constraints");
            }

            return new ScalingTradeDecision
            {
                Action = TradeAction.Execute,
                Lane = tradeLane,
                PositionSize = positionSize,
                EscalationLevel = escalationLevel,
                ReasonCode = $"OK_{tradeLane}_{escalationLevel}",
                ExpectedCredit = setup.ExpectedCredit,
                MaxLoss = setup.Width - setup.ExpectedCredit
            };
        }

        /// <summary>
        /// Update session statistics and manage escalation state
        /// </summary>
        private void UpdateSessionStats()
        {
            // Update current session statistics
            _currentSession = CalculateSessionStats();

            // Check for auto de-escalation conditions
            CheckAutoDeescalation();

            // Update escalation level
            _currentLevel = ComputeEscalationLevel();
        }

        /// <summary>
        /// Validate absolute risk constraints that can never be violated
        /// </summary>
        private bool ValidateAbsoluteConstraints(TradeSetup setup)
        {
            var remainingBudget = CalculateRemainingBudget();
            var maxLossPerContract = (setup.Width - setup.ExpectedCredit) * 100m;

            // Must fit within remaining budget
            if (maxLossPerContract > remainingBudget)
            {
                LogDecision("RFIB_CAP_HIT", setup, 0, remainingBudget);
                return false;
            }

            // Weekly RFib constraint
            if (_riskManager.WouldViolateWeeklyLimit(maxLossPerContract))
            {
                LogDecision("WEEKLY_RFIB_HIT", setup, 0, remainingBudget);
                return false;
            }

            // Event blackout periods
            if (IsInEventBlackout())
            {
                LogDecision("EVENT_BLACKOUT", setup, 0, remainingBudget);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Determine whether this should be a Probe or Quality/Punch trade
        /// </summary>
        private TradeLane DetermineTradeLane(TradeSetup setup)
        {
            // Always allow Probe trades for sampling edge
            if (setup.Width <= 1.0m || !_config.EscalationEnabled)
            {
                return TradeLane.Probe;
            }

            // Quality/Punch lane only available when positive-probe conditions met
            if (_probeDetector.IsGreenlit(_currentSession))
            {
                return TradeLane.Quality;
            }

            return TradeLane.Probe;
        }

        /// <summary>
        /// Compute current escalation level based on P&L and conditions
        /// </summary>
        private EscalationLevel ComputeEscalationLevel()
        {
            if (!_config.EscalationEnabled ||
                DateTime.Now < _cooldownUntil ||
                !_probeDetector.IsGreenlit(_currentSession))
            {
                return EscalationLevel.Level0;
            }

            var dailyCap = _riskManager.GetCurrentDailyLimit();

            // Level 2: High P&L cushion + Quality punch trades profitable
            if (_currentSession.RealizedDayPnL >= 0.60m * dailyCap &&
                _currentSession.Last3PunchPnL >= 0)
            {
                return EscalationLevel.Level2;
            }

            // Level 1: Basic P&L cushion met
            if (_currentSession.RealizedDayPnL >= 0.30m * dailyCap)
            {
                return EscalationLevel.Level1;
            }

            return EscalationLevel.Level0;
        }

        /// <summary>
        /// Calculate position size based on lane, escalation level, and constraints
        /// </summary>
        private decimal CalculatePositionSize(TradeSetup setup, TradeLane lane, EscalationLevel level)
        {
            var remainingBudget = CalculateRemainingBudget();
            var maxLossPerContract = (setup.Width - setup.ExpectedCredit) * 100m;

            // Get fraction based on lane and escalation level
            var fraction = GetCapitalFraction(lane, level);

            // Per-trade cap calculation
            var perTradeCap = fraction * remainingBudget;

            // Additional constraint for Quality lane: max 50% of realized P&L
            if (lane == TradeLane.Quality)
            {
                var pnlConstraint = 0.50m * Math.Max(0, _currentSession.RealizedDayPnL);
                perTradeCap = Math.Min(perTradeCap, pnlConstraint);
            }

            // Calculate contracts
            var contractsByRisk = (int)Math.Floor(perTradeCap / maxLossPerContract);
            var contracts = Math.Min(contractsByRisk, _config.HardContractCap);

            // Probe 1-lot rule: if probe trade fits in absolute budget, allow 1 contract
            if (contracts < 1 && lane == TradeLane.Probe && maxLossPerContract <= remainingBudget)
            {
                contracts = 1;
            }

            LogDecision(
                $"{lane}_{level}",
                setup,
                contracts,
                remainingBudget,
                fraction,
                perTradeCap
            );

            return contracts;
        }

        /// <summary>
        /// Get capital allocation fraction based on lane and escalation level
        /// </summary>
        private decimal GetCapitalFraction(TradeLane lane, EscalationLevel level)
        {
            if (lane == TradeLane.Probe)
            {
                return _config.ProbeCapitalFraction;
            }

            // Quality/Punch lane fractions by escalation level
            return level switch
            {
                EscalationLevel.Level0 => _config.ProbeCapitalFraction, // Fallback to probe
                EscalationLevel.Level1 => _config.QualityCapitalFractionL1,
                EscalationLevel.Level2 => _config.QualityCapitalFractionL2,
                _ => _config.ProbeCapitalFraction
            };
        }

        /// <summary>
        /// Validate correlation budget constraints for concurrent positions
        /// </summary>
        private bool ValidateCorrelationBudget(TradeSetup setup, decimal positionSize)
        {
            if (!_config.CorrelationBudgetEnabled)
                return true;

            var rhoWeightedExposureAfter = _correlationManager.CalculateRhoWeightedExposureAfter(
                _openPositions, setup, positionSize);

            if (rhoWeightedExposureAfter > _config.MaxRhoWeightedExposure)
            {
                LogDecision("RHO_BUDGET_EXCEEDED", setup, positionSize, 0);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validate quality requirements for Quality lane trades
        /// </summary>
        private bool ValidateQualityRequirements(TradeSetup setup, TradeLane lane)
        {
            if (lane == TradeLane.Probe)
                return true; // Probe trades have relaxed requirements

            if (!_qualityFilter.IsHighQuality(setup))
            {
                LogDecision("QUALITY_FAIL", setup, 0, 0);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Check for auto de-escalation conditions
        /// </summary>
        private void CheckAutoDeescalation()
        {
            var dailyCap = _riskManager.GetCurrentDailyLimit();

            // Drop level if P&L falls below half of last escalation trigger
            if (_currentSession.RealizedDayPnL < 0.5m * GetLastEscalationTrigger(_currentLevel))
            {
                if (_currentLevel > EscalationLevel.Level0)
                {
                    _currentLevel--;
                    LogDecision("AUTO_DEESCALATE_PNL", null, 0, 0);
                }
            }

            // Cooldown after consecutive Quality lane losses
            if (_currentSession.ConsecutivePunchLosses >= 2)
            {
                _currentLevel = EscalationLevel.Level0;
                _cooldownUntil = DateTime.Now.AddMinutes(_config.CooldownMinutes);
                LogDecision("AUTO_DEESCALATE_LOSSES", null, 0, 0);
            }

            // Correlation exposure exceeded
            var currentRhoExposure = _correlationManager.CalculateCurrentRhoWeightedExposure(_openPositions);
            if (currentRhoExposure > _config.MaxRhoWeightedExposure)
            {
                _currentLevel = EscalationLevel.Level0;
                _cooldownUntil = DateTime.Now.AddMinutes(_config.CooldownMinutes);
                LogDecision("AUTO_DEESCALATE_CORRELATION", null, 0, 0);
            }
        }

        /// <summary>
        /// Calculate remaining budget for new positions
        /// </summary>
        private decimal CalculateRemainingBudget()
        {
            var dailyCap = _riskManager.GetCurrentDailyLimit();
            var openMaxLoss = _openPositions.Sum(p => p.MaxLoss);
            var realizedLossToday = Math.Min(0, _currentSession.RealizedDayPnL);

            return Math.Max(0, dailyCap - openMaxLoss - Math.Abs(realizedLossToday));
        }

        /// <summary>
        /// Calculate current session statistics
        /// </summary>
        private SessionStats CalculateSessionStats()
        {
            // This would be implemented to track actual session statistics
            // For now, return placeholder values
            return new SessionStats
            {
                DailyCap = _riskManager.GetCurrentDailyLimit(),
                RealizedDayPnL = 0m, // Would be calculated from closed positions
                ProbeCount = 0,
                ProbeWinRate = 0.75m,
                LiquidityScore = 0.80m,
                Last3PunchPnL = 0m,
                ConsecutivePunchLosses = 0,
                InEventBlackout = IsInEventBlackout()
            };
        }

        /// <summary>
        /// Check if currently in event blackout period
        /// </summary>
        private bool IsInEventBlackout()
        {
            // Implementation would check economic calendar and market events
            return false;
        }

        /// <summary>
        /// Get P&L trigger level for the specified escalation level
        /// </summary>
        private decimal GetLastEscalationTrigger(EscalationLevel level)
        {
            var dailyCap = _riskManager.GetCurrentDailyLimit();
            return level switch
            {
                EscalationLevel.Level1 => 0.30m * dailyCap,
                EscalationLevel.Level2 => 0.60m * dailyCap,
                _ => 0m
            };
        }

        /// <summary>
        /// Log trading decision with detailed context
        /// </summary>
        private void LogDecision(string reasonCode, TradeSetup setup, decimal contracts, decimal remainingBudget,
            decimal fraction = 0, decimal perTradeCap = 0)
        {
            var lane = setup != null ? DetermineTradeLane(setup) : TradeLane.Unknown;
            var level = _currentLevel;
            var dailyCap = _riskManager.GetCurrentDailyLimit();
            var realizedPnL = _currentSession?.RealizedDayPnL ?? 0;
            var expectedCredit = setup?.ExpectedCredit ?? 0;
            var width = setup?.Width ?? 0;
            var maxLossPerContract = setup != null ? (setup.Width - setup.ExpectedCredit) * 100m : 0;

            var logMessage = $"Decision: {lane} L{(int)level} Cap:{dailyCap:C} Rem:{remainingBudget:C} " +
                           $"Frac:{fraction:P1} PerTrade:{perTradeCap:C} PnL:{realizedPnL:C} " +
                           $"Credit:{expectedCredit:C} Width:{width:F1} MaxLoss:{maxLossPerContract:C} " +
                           $"Contracts:{contracts} Reason:{reasonCode}";

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {logMessage}");
        }

        // Configuration factory methods

        private ScalingConfiguration CreatePhase1Configuration()
        {
            return new ScalingConfiguration
            {
                Phase = ScalingPhase.Foundation,
                MonthlyTarget = 569m, // 2x baseline
                RFibLimits = new[] { 1000m, 600m, 400m, 200m }, // 2x original
                ProbeCapitalFraction = 0.40m,
                QualityCapitalFractionL1 = 0.55m,
                QualityCapitalFractionL2 = 0.55m, // Same as L1 in Phase 1
                MaxConcurrentPositions = 2,
                EscalationEnabled = false, // Not yet active
                CorrelationBudgetEnabled = true,
                MaxRhoWeightedExposure = 1.0m,
                HardContractCap = 5,
                CooldownMinutes = 60
            };
        }

        private ScalingConfiguration CreatePhase2Configuration()
        {
            return new ScalingConfiguration
            {
                Phase = ScalingPhase.Escalation,
                MonthlyTarget = 1139m, // 4x baseline
                RFibLimits = new[] { 1500m, 900m, 600m, 300m }, // 3x original
                ProbeCapitalFraction = 0.40m,
                QualityCapitalFractionL1 = 0.55m,
                QualityCapitalFractionL2 = 0.65m,
                MaxConcurrentPositions = 3,
                EscalationEnabled = true, // Escalation system active
                CorrelationBudgetEnabled = true,
                MaxRhoWeightedExposure = 1.0m,
                HardContractCap = 5,
                CooldownMinutes = 60
            };
        }

        private ScalingConfiguration CreatePhase3Configuration()
        {
            return new ScalingConfiguration
            {
                Phase = ScalingPhase.Quality,
                MonthlyTarget = 1708m, // 6x baseline
                RFibLimits = new[] { 2000m, 1200m, 800m, 400m }, // 4x original
                ProbeCapitalFraction = 0.40m,
                QualityCapitalFractionL1 = 0.55m,
                QualityCapitalFractionL2 = 0.65m,
                MaxConcurrentPositions = 3,
                EscalationEnabled = true,
                CorrelationBudgetEnabled = true,
                MaxRhoWeightedExposure = 1.0m,
                HardContractCap = 5,
                CooldownMinutes = 60,
                QualityEnhancementEnabled = true // Enhanced quality requirements
            };
        }

        private ScalingConfiguration CreatePhase4Configuration()
        {
            return new ScalingConfiguration
            {
                Phase = ScalingPhase.Maximum,
                MonthlyTarget = 2847m, // 10x baseline
                RFibLimits = new[] { 3000m, 1800m, 1200m, 600m }, // 6x original
                ProbeCapitalFraction = 0.40m,
                QualityCapitalFractionL1 = 0.55m,
                QualityCapitalFractionL2 = 0.65m,
                MaxConcurrentPositions = 4, // Maximum concurrency
                EscalationEnabled = true,
                CorrelationBudgetEnabled = true,
                MaxRhoWeightedExposure = 1.0m,
                HardContractCap = 5,
                CooldownMinutes = 60,
                QualityEnhancementEnabled = true,
                DynamicSizingEnabled = true // Dynamic position sizing
            };
        }
    }

    // Supporting Types and Interfaces

    public enum ScalingPhase
    {
        Foundation = 1,  // 2x scaling
        Escalation = 2,  // 4x scaling  
        Quality = 3,     // 6x scaling
        Maximum = 4      // 10x scaling
    }

    public enum TradeLane
    {
        Unknown,
        Probe,   // Capital preservation, tight spreads
        Quality  // Profit maximization, wider spreads
    }

    public enum EscalationLevel
    {
        Level0 = 0, // Baseline
        Level1 = 1, // Greenlight-1
        Level2 = 2  // Greenlight-2
    }

    public enum TradeAction
    {
        Execute,
        Reject
    }

    public class ScalingConfiguration
    {
        public ScalingPhase Phase { get; set; }
        public decimal MonthlyTarget { get; set; }
        public decimal[] RFibLimits { get; set; }
        public decimal ProbeCapitalFraction { get; set; }
        public decimal QualityCapitalFractionL1 { get; set; }
        public decimal QualityCapitalFractionL2 { get; set; }
        public int MaxConcurrentPositions { get; set; }
        public bool EscalationEnabled { get; set; }
        public bool CorrelationBudgetEnabled { get; set; }
        public decimal MaxRhoWeightedExposure { get; set; }
        public int HardContractCap { get; set; }
        public int CooldownMinutes { get; set; }
        public bool QualityEnhancementEnabled { get; set; }
        public bool DynamicSizingEnabled { get; set; }
    }

    public class TradeSetup
    {
        public string Symbol { get; set; }
        public decimal Width { get; set; }
        public decimal ExpectedCredit { get; set; }
        public DateTime EntryTime { get; set; }
        public decimal LiquidityScore { get; set; }
        public decimal BidAskSpread { get; set; }
        public decimal IVRank { get; set; }
        public decimal GoScore { get; set; }
    }

    public class ScalingTradeDecision
    {
        public TradeAction Action { get; set; }
        public TradeLane Lane { get; set; }
        public decimal PositionSize { get; set; }
        public EscalationLevel EscalationLevel { get; set; }
        public string ReasonCode { get; set; }
        public decimal ExpectedCredit { get; set; }
        public decimal MaxLoss { get; set; }

        public static ScalingTradeDecision Reject(string reason)
        {
            return new ScalingTradeDecision
            {
                Action = TradeAction.Reject,
                ReasonCode = reason,
                PositionSize = 0
            };
        }
    }

    public class SessionStats
    {
        public decimal DailyCap { get; set; }
        public decimal RealizedDayPnL { get; set; }
        public int ProbeCount { get; set; }
        public decimal ProbeWinRate { get; set; }
        public decimal LiquidityScore { get; set; }
        public decimal Last3PunchPnL { get; set; }
        public int ConsecutivePunchLosses { get; set; }
        public bool InEventBlackout { get; set; }
    }

    public class Position
    {
        public string Symbol { get; set; }
        public decimal MaxLoss { get; set; }
        public decimal BetaToSPY { get; set; }
        public decimal MaxPairwiseCorrelation { get; set; }
        public TradeLane Lane { get; set; }
        public DateTime EntryTime { get; set; }
    }

    // Interfaces for dependency injection

    public interface IReverseFibonacciRiskManager
    {
        decimal GetCurrentDailyLimit();
        bool WouldViolateWeeklyLimit(decimal additionalRisk);
    }

    public interface IPositiveProbeDetector
    {
        bool IsGreenlit(SessionStats session);
    }

    public interface ICorrelationBudgetManager
    {
        decimal CalculateCurrentRhoWeightedExposure(List<Position> positions);
        decimal CalculateRhoWeightedExposureAfter(List<Position> positions, TradeSetup setup, decimal positionSize);
    }

    public interface IQualityEntryFilter
    {
        bool IsHighQuality(TradeSetup setup);
    }

    public interface IDualLaneExitManager
    {
        void ManageProbeExit(Position position);
        void ManagePunchExit(Position position);
    }
}