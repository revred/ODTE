using System;
using System.Collections.Generic;
using System.Linq;

namespace ODTE.Strategy
{
    /// <summary>
    /// PM250 Scaling Engine with RevFibNotch Risk Management
    /// Integrates the conservative RevFibNotch system with dual-strategy scaling
    /// Provides proportional risk adjustment while maintaining scaling potential
    /// </summary>
    public class PM250_RevFibNotch_ScalingEngine
    {
        private readonly RevFibNotchManager _revFibNotchManager;
        private readonly IPositiveProbeDetector _probeDetector;
        private readonly ICorrelationBudgetManager _correlationManager;
        private readonly IQualityEntryFilter _qualityFilter;
        private readonly IDualLaneExitManager _exitManager;
        
        private RevFibNotchScalingConfiguration _config;
        private SessionStats _currentSession;
        private EscalationLevel _currentLevel;
        private DateTime _cooldownUntil;
        private List<Position> _openPositions;

        public PM250_RevFibNotch_ScalingEngine(
            RevFibNotchManager revFibNotchManager,
            IPositiveProbeDetector probeDetector,
            ICorrelationBudgetManager correlationManager,
            IQualityEntryFilter qualityFilter,
            IDualLaneExitManager exitManager)
        {
            _revFibNotchManager = revFibNotchManager ?? throw new ArgumentNullException(nameof(revFibNotchManager));
            _probeDetector = probeDetector ?? throw new ArgumentNullException(nameof(probeDetector));
            _correlationManager = correlationManager ?? throw new ArgumentNullException(nameof(correlationManager));
            _qualityFilter = qualityFilter ?? throw new ArgumentNullException(nameof(qualityFilter));
            _exitManager = exitManager ?? throw new ArgumentNullException(nameof(exitManager));
            
            _openPositions = new List<Position>();
            _currentLevel = EscalationLevel.Level0;
            _cooldownUntil = DateTime.MinValue;
            
            // Initialize with conservative scaling configuration
            _config = CreateRevFibNotchScalingConfiguration();
        }

        /// <summary>
        /// Process end-of-day P&L and update RevFibNotch position
        /// </summary>
        public RevFibNotchScalingDailyResult ProcessEndOfDay(decimal dailyPnL, DateTime date)
        {
            // Update RevFibNotch position based on P&L
            var notchAdjustment = _revFibNotchManager.ProcessDailyPnL(dailyPnL, date);
            
            // Update session statistics
            UpdateSessionForNewDay(dailyPnL, date);
            
            // Determine scaling phase based on current RFib level
            var scalingPhase = DetermineScalingPhase(_revFibNotchManager.CurrentRFibLimit);
            
            // Update configuration for new scaling phase if changed
            if (scalingPhase != _config.CurrentPhase)
            {
                UpdateConfigurationForPhase(scalingPhase);
            }

            return new RevFibNotchScalingDailyResult
            {
                Date = date,
                DailyPnL = dailyPnL,
                NotchAdjustment = notchAdjustment,
                CurrentRFibLimit = _revFibNotchManager.CurrentRFibLimit,
                ScalingPhase = scalingPhase,
                EscalationLevel = _currentLevel,
                ConfigurationChanged = scalingPhase != _config.CurrentPhase
            };
        }

        /// <summary>
        /// Process a trading opportunity with RevFibNotch constraints
        /// </summary>
        public RevFibNotchTradeDecision ProcessTradeOpportunity(TradeSetup setup)
        {
            UpdateSessionStats();
            
            // Get current RevFibNotch limit
            var currentRFibLimit = _revFibNotchManager.CurrentRFibLimit;
            
            // Core risk management - absolute constraints
            if (!ValidateAbsoluteConstraints(setup, currentRFibLimit))
            {
                return RevFibNotchTradeDecision.Reject("RevFibNotch constraints violated", currentRFibLimit);
            }

            // Determine trade lane (Probe vs Quality/Punch)
            var tradeLane = DetermineTradeLane(setup);
            
            // Check escalation level and permissions
            var escalationLevel = ComputeEscalationLevel();
            
            // Calculate position sizing with RevFibNotch constraints
            var positionSize = CalculatePositionSize(setup, tradeLane, escalationLevel, currentRFibLimit);
            
            if (positionSize <= 0)
            {
                return RevFibNotchTradeDecision.Reject("Insufficient position size within RevFibNotch limits", currentRFibLimit);
            }

            // Final validations
            if (!ValidateCorrelationBudget(setup, positionSize) || 
                !ValidateQualityRequirements(setup, tradeLane))
            {
                return RevFibNotchTradeDecision.Reject("Quality or correlation constraints", currentRFibLimit);
            }

            return new RevFibNotchTradeDecision
            {
                Action = TradeAction.Execute,
                Lane = tradeLane,
                PositionSize = positionSize,
                EscalationLevel = escalationLevel,
                CurrentRFibLimit = currentRFibLimit,
                NotchPosition = _revFibNotchManager.CurrentNotchIndex,
                ReasonCode = $"OK_{tradeLane}_{escalationLevel}_NOTCH{_revFibNotchManager.CurrentNotchIndex}",
                ExpectedCredit = setup.ExpectedCredit,
                MaxLoss = (setup.Width - setup.ExpectedCredit) * positionSize * 100m
            };
        }

        /// <summary>
        /// Determine scaling phase based on current RFib level
        /// </summary>
        private RevFibNotchScalingPhase DetermineScalingPhase(decimal rFibLimit)
        {
            return rFibLimit switch
            {
                1250m => RevFibNotchScalingPhase.Maximum,      // Most aggressive
                800m => RevFibNotchScalingPhase.Aggressive,    // High scaling
                500m => RevFibNotchScalingPhase.Balanced,      // Balanced approach
                300m => RevFibNotchScalingPhase.Conservative,  // Reduced scaling
                200m => RevFibNotchScalingPhase.Defensive,     // Minimal scaling
                100m => RevFibNotchScalingPhase.Survival,      // Capital preservation only
                _ => RevFibNotchScalingPhase.Balanced
            };
        }

        /// <summary>
        /// Update configuration for new scaling phase
        /// </summary>
        private void UpdateConfigurationForPhase(RevFibNotchScalingPhase phase)
        {
            var oldPhase = _config.CurrentPhase;
            _config = CreateConfigurationForPhase(phase);
            
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] REVFIBNOTCH SCALING PHASE CHANGE:");
            Console.WriteLine($"  Phase: {oldPhase} → {phase}");
            Console.WriteLine($"  RFib: {_revFibNotchManager.CurrentRFibLimit:C}");
            Console.WriteLine($"  Probe Fraction: {_config.ProbeCapitalFraction:P1}");
            Console.WriteLine($"  Quality Fraction L1: {_config.QualityCapitalFractionL1:P1}");
            Console.WriteLine($"  Max Concurrent: {_config.MaxConcurrentPositions}");
        }

        /// <summary>
        /// Validate absolute risk constraints with current RevFibNotch limit
        /// </summary>
        private bool ValidateAbsoluteConstraints(TradeSetup setup, decimal currentRFibLimit)
        {
            var remainingBudget = CalculateRemainingBudget(currentRFibLimit);
            var maxLossPerContract = (setup.Width - setup.ExpectedCredit) * 100m;
            
            // Must fit within RevFibNotch budget
            if (maxLossPerContract > remainingBudget)
            {
                LogDecision("REVFIBNOTCH_CAP_HIT", setup, 0, remainingBudget, currentRFibLimit);
                return false;
            }

            // Event blackout periods
            if (IsInEventBlackout())
            {
                LogDecision("EVENT_BLACKOUT", setup, 0, remainingBudget, currentRFibLimit);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Calculate position size with RevFibNotch constraints
        /// </summary>
        private decimal CalculatePositionSize(TradeSetup setup, TradeLane lane, EscalationLevel level, decimal rFibLimit)
        {
            var remainingBudget = CalculateRemainingBudget(rFibLimit);
            var maxLossPerContract = (setup.Width - setup.ExpectedCredit) * 100m;
            
            // Get fraction based on lane, escalation level, and current phase
            var fraction = GetCapitalFraction(lane, level);
            
            // Apply RevFibNotch scaling multiplier
            var notchMultiplier = GetNotchScalingMultiplier(_config.CurrentPhase);
            var adjustedFraction = fraction * notchMultiplier;
            
            // Per-trade cap calculation
            var perTradeCap = adjustedFraction * remainingBudget;
            
            // Additional constraint for Quality lane: max 50% of realized P&L
            if (lane == TradeLane.Quality && _currentSession?.RealizedDayPnL > 0)
            {
                var pnlConstraint = 0.50m * _currentSession.RealizedDayPnL;
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
                $"{lane}_{level}_NOTCH{_revFibNotchManager.CurrentNotchIndex}",
                setup,
                contracts,
                remainingBudget,
                rFibLimit,
                adjustedFraction,
                perTradeCap,
                notchMultiplier
            );

            return contracts;
        }

        /// <summary>
        /// Get scaling multiplier based on RevFibNotch phase
        /// </summary>
        private decimal GetNotchScalingMultiplier(RevFibNotchScalingPhase phase)
        {
            return phase switch
            {
                RevFibNotchScalingPhase.Maximum => 1.50m,      // 150% of base allocation
                RevFibNotchScalingPhase.Aggressive => 1.25m,   // 125% of base allocation
                RevFibNotchScalingPhase.Balanced => 1.00m,     // 100% of base allocation
                RevFibNotchScalingPhase.Conservative => 0.80m, // 80% of base allocation
                RevFibNotchScalingPhase.Defensive => 0.60m,    // 60% of base allocation
                RevFibNotchScalingPhase.Survival => 0.40m,     // 40% of base allocation (capital preservation)
                _ => 1.00m
            };
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
        /// Calculate remaining budget with current RevFibNotch limit
        /// </summary>
        private decimal CalculateRemainingBudget(decimal rFibLimit)
        {
            var openMaxLoss = _openPositions.Sum(p => p.MaxLoss);
            var realizedLossToday = Math.Min(0, _currentSession?.RealizedDayPnL ?? 0);
            
            return Math.Max(0, rFibLimit - openMaxLoss - Math.Abs(realizedLossToday));
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
        /// Update session for new trading day
        /// </summary>
        private void UpdateSessionForNewDay(decimal dailyPnL, DateTime date)
        {
            // Reset daily statistics
            _currentSession = new SessionStats
            {
                DailyCap = _revFibNotchManager.CurrentRFibLimit,
                RealizedDayPnL = 0m, // Reset for new day
                ProbeCount = 0,
                ProbeWinRate = 0.75m, // Use historical average
                LiquidityScore = 0.80m,
                Last3PunchPnL = 0m,
                ConsecutivePunchLosses = 0,
                InEventBlackout = IsInEventBlackout()
            };
            
            // Reset escalation level for new day
            _currentLevel = EscalationLevel.Level0;
            _cooldownUntil = DateTime.MinValue;
            
            // Clear open positions for new day
            _openPositions.Clear();
        }

        // Delegate methods to original implementation

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

        private EscalationLevel ComputeEscalationLevel()
        {
            if (!_config.EscalationEnabled || 
                DateTime.Now < _cooldownUntil ||
                !_probeDetector.IsGreenlit(_currentSession))
            {
                return EscalationLevel.Level0;
            }

            var dailyCap = _revFibNotchManager.CurrentRFibLimit;
            
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

        private void CheckAutoDeescalation()
        {
            var dailyCap = _revFibNotchManager.CurrentRFibLimit;
            
            // Drop level if P&L falls below half of last escalation trigger
            if (_currentSession.RealizedDayPnL < 0.5m * GetLastEscalationTrigger(_currentLevel))
            {
                if (_currentLevel > EscalationLevel.Level0)
                {
                    _currentLevel--;
                    LogDecision("AUTO_DEESCALATE_PNL", null, 0, 0, dailyCap);
                }
            }
            
            // Cooldown after consecutive Quality lane losses
            if (_currentSession.ConsecutivePunchLosses >= 2)
            {
                _currentLevel = EscalationLevel.Level0;
                _cooldownUntil = DateTime.Now.AddMinutes(_config.CooldownMinutes);
                LogDecision("AUTO_DEESCALATE_LOSSES", null, 0, 0, dailyCap);
            }
        }

        private decimal GetLastEscalationTrigger(EscalationLevel level)
        {
            var dailyCap = _revFibNotchManager.CurrentRFibLimit;
            return level switch
            {
                EscalationLevel.Level1 => 0.30m * dailyCap,
                EscalationLevel.Level2 => 0.60m * dailyCap,
                _ => 0m
            };
        }

        private bool ValidateCorrelationBudget(TradeSetup setup, decimal positionSize)
        {
            if (!_config.CorrelationBudgetEnabled)
                return true;

            var rhoWeightedExposureAfter = _correlationManager.CalculateRhoWeightedExposureAfter(_openPositions, setup, positionSize);
            return rhoWeightedExposureAfter <= _config.MaxRhoWeightedExposure;
        }

        private bool ValidateQualityRequirements(TradeSetup setup, TradeLane lane)
        {
            if (lane == TradeLane.Probe)
                return true; // Probe trades have relaxed requirements

            return _qualityFilter.IsHighQuality(setup);
        }

        private bool IsInEventBlackout()
        {
            // Implementation would check economic calendar and market events
            return false;
        }

        private SessionStats CalculateSessionStats()
        {
            // This would be implemented to track actual session statistics
            return new SessionStats
            {
                DailyCap = _revFibNotchManager.CurrentRFibLimit,
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
        /// Log trading decision with RevFibNotch context
        /// </summary>
        private void LogDecision(string reasonCode, TradeSetup setup, decimal contracts, decimal remainingBudget, 
            decimal rFibLimit, decimal fraction = 0, decimal perTradeCap = 0, decimal notchMultiplier = 1.0m)
        {
            var lane = setup != null ? DetermineTradeLane(setup) : TradeLane.Unknown;
            var level = _currentLevel;
            var notchIndex = _revFibNotchManager.CurrentNotchIndex;
            var phase = _config.CurrentPhase;
            var realizedPnL = _currentSession?.RealizedDayPnL ?? 0;
            var expectedCredit = setup?.ExpectedCredit ?? 0;
            var width = setup?.Width ?? 0;
            var maxLossPerContract = setup != null ? (setup.Width - setup.ExpectedCredit) * 100m : 0;
            
            var logMessage = $"RevFibNotch Decision: {lane} L{(int)level} Phase:{phase} " +
                           $"Notch:{notchIndex} RFib:{rFibLimit:C} Rem:{remainingBudget:C} " +
                           $"Frac:{fraction:P1} Mult:{notchMultiplier:F2} PerTrade:{perTradeCap:C} " +
                           $"PnL:{realizedPnL:C} Contracts:{contracts} Reason:{reasonCode}";
            
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {logMessage}");
        }

        // Configuration factory methods

        private RevFibNotchScalingConfiguration CreateRevFibNotchScalingConfiguration()
        {
            return CreateConfigurationForPhase(RevFibNotchScalingPhase.Balanced);
        }

        private RevFibNotchScalingConfiguration CreateConfigurationForPhase(RevFibNotchScalingPhase phase)
        {
            return phase switch
            {
                RevFibNotchScalingPhase.Survival => new RevFibNotchScalingConfiguration
                {
                    CurrentPhase = phase,
                    ProbeCapitalFraction = 0.30m,
                    QualityCapitalFractionL1 = 0.30m, // No scaling in survival mode
                    QualityCapitalFractionL2 = 0.30m,
                    MaxConcurrentPositions = 1,
                    EscalationEnabled = false, // Disable escalation
                    CorrelationBudgetEnabled = true,
                    MaxRhoWeightedExposure = 0.8m, // More conservative
                    HardContractCap = 3,
                    CooldownMinutes = 120 // Longer cooldown
                },
                
                RevFibNotchScalingPhase.Defensive => new RevFibNotchScalingConfiguration
                {
                    CurrentPhase = phase,
                    ProbeCapitalFraction = 0.35m,
                    QualityCapitalFractionL1 = 0.45m,
                    QualityCapitalFractionL2 = 0.50m,
                    MaxConcurrentPositions = 2,
                    EscalationEnabled = true,
                    CorrelationBudgetEnabled = true,
                    MaxRhoWeightedExposure = 0.9m,
                    HardContractCap = 4,
                    CooldownMinutes = 90
                },
                
                RevFibNotchScalingPhase.Conservative => new RevFibNotchScalingConfiguration
                {
                    CurrentPhase = phase,
                    ProbeCapitalFraction = 0.40m,
                    QualityCapitalFractionL1 = 0.50m,
                    QualityCapitalFractionL2 = 0.55m,
                    MaxConcurrentPositions = 2,
                    EscalationEnabled = true,
                    CorrelationBudgetEnabled = true,
                    MaxRhoWeightedExposure = 1.0m,
                    HardContractCap = 5,
                    CooldownMinutes = 60
                },
                
                RevFibNotchScalingPhase.Balanced => new RevFibNotchScalingConfiguration
                {
                    CurrentPhase = phase,
                    ProbeCapitalFraction = 0.40m,
                    QualityCapitalFractionL1 = 0.55m,
                    QualityCapitalFractionL2 = 0.65m,
                    MaxConcurrentPositions = 3,
                    EscalationEnabled = true,
                    CorrelationBudgetEnabled = true,
                    MaxRhoWeightedExposure = 1.0m,
                    HardContractCap = 5,
                    CooldownMinutes = 60
                },
                
                RevFibNotchScalingPhase.Aggressive => new RevFibNotchScalingConfiguration
                {
                    CurrentPhase = phase,
                    ProbeCapitalFraction = 0.45m,
                    QualityCapitalFractionL1 = 0.60m,
                    QualityCapitalFractionL2 = 0.70m,
                    MaxConcurrentPositions = 3,
                    EscalationEnabled = true,
                    CorrelationBudgetEnabled = true,
                    MaxRhoWeightedExposure = 1.0m,
                    HardContractCap = 6,
                    CooldownMinutes = 45
                },
                
                RevFibNotchScalingPhase.Maximum => new RevFibNotchScalingConfiguration
                {
                    CurrentPhase = phase,
                    ProbeCapitalFraction = 0.50m,
                    QualityCapitalFractionL1 = 0.65m,
                    QualityCapitalFractionL2 = 0.75m,
                    MaxConcurrentPositions = 4,
                    EscalationEnabled = true,
                    CorrelationBudgetEnabled = true,
                    MaxRhoWeightedExposure = 1.0m,
                    HardContractCap = 7,
                    CooldownMinutes = 30
                },
                
                _ => CreateConfigurationForPhase(RevFibNotchScalingPhase.Balanced)
            };
        }

        // Public properties for monitoring

        public decimal CurrentRFibLimit => _revFibNotchManager.CurrentRFibLimit;
        public int CurrentNotchIndex => _revFibNotchManager.CurrentNotchIndex;
        public RevFibNotchScalingPhase CurrentPhase => _config.CurrentPhase;
        public EscalationLevel CurrentEscalationLevel => _currentLevel;
        public RevFibNotchStatus RevFibNotchStatus => _revFibNotchManager.GetStatus();
    }

    // Supporting Types

    public enum RevFibNotchScalingPhase
    {
        Survival = 0,     // $100 - Capital preservation only
        Defensive = 1,    // $200 - Minimal scaling
        Conservative = 2, // $300 - Reduced scaling
        Balanced = 3,     // $500 - Standard scaling
        Aggressive = 4,   // $800 - High scaling
        Maximum = 5       // $1250 - Maximum scaling
    }

    public class RevFibNotchScalingConfiguration
    {
        public RevFibNotchScalingPhase CurrentPhase { get; set; }
        public decimal ProbeCapitalFraction { get; set; }
        public decimal QualityCapitalFractionL1 { get; set; }
        public decimal QualityCapitalFractionL2 { get; set; }
        public int MaxConcurrentPositions { get; set; }
        public bool EscalationEnabled { get; set; }
        public bool CorrelationBudgetEnabled { get; set; }
        public decimal MaxRhoWeightedExposure { get; set; }
        public int HardContractCap { get; set; }
        public int CooldownMinutes { get; set; }
    }

    public class RevFibNotchTradeDecision
    {
        public TradeAction Action { get; set; }
        public TradeLane Lane { get; set; }
        public decimal PositionSize { get; set; }
        public EscalationLevel EscalationLevel { get; set; }
        public decimal CurrentRFibLimit { get; set; }
        public int NotchPosition { get; set; }
        public string ReasonCode { get; set; } = string.Empty;
        public decimal ExpectedCredit { get; set; }
        public decimal MaxLoss { get; set; }
        
        public static RevFibNotchTradeDecision Reject(string reason, decimal rFibLimit)
        {
            return new RevFibNotchTradeDecision
            {
                Action = TradeAction.Reject,
                ReasonCode = reason,
                PositionSize = 0,
                CurrentRFibLimit = rFibLimit
            };
        }
    }

    public class RevFibNotchScalingDailyResult
    {
        public DateTime Date { get; set; }
        public decimal DailyPnL { get; set; }
        public RevFibNotchAdjustment NotchAdjustment { get; set; }
        public decimal CurrentRFibLimit { get; set; }
        public RevFibNotchScalingPhase ScalingPhase { get; set; }
        public EscalationLevel EscalationLevel { get; set; }
        public bool ConfigurationChanged { get; set; }
    }
}