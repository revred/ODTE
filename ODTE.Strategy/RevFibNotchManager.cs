using System;
using System.Collections.Generic;
using System.Linq;

namespace ODTE.Strategy
{
    /// <summary>
    /// RevFibNotch Risk Management System
    /// Proportional risk adjustment based on P&L magnitude:
    /// - Losses: Immediate rightward movement (increase safety)
    /// - Profits: Leftward movement (increase allocation) after sustained performance
    /// - Movement proportional to magnitude of loss/profit
    /// </summary>
    public class RevFibNotchManager
    {
        private readonly decimal[] _rFibLimits = { 1280m, 500m, 300m, 200m, 100m, 50m };
        private int _currentNotchIndex;
        private readonly List<RevFibNotchDailyResult> _recentDays;
        private readonly RevFibNotchConfiguration _config;
        
        // Notch movement thresholds
        private readonly Dictionary<int, decimal> _lossNotchThresholds;
        private readonly Dictionary<int, decimal> _profitNotchThresholds;

        public RevFibNotchManager(RevFibNotchConfiguration config = null)
        {
            _config = config ?? new RevFibNotchConfiguration();
            _currentNotchIndex = 2; // Start at $300 (middle position)
            _recentDays = new List<RevFibNotchDailyResult>();
            
            // Initialize thresholds based on current RFib level
            _lossNotchThresholds = InitializeLossThresholds();
            _profitNotchThresholds = InitializeProfitThresholds();
        }

        /// <summary>
        /// Process end-of-day P&L and adjust RFib position accordingly
        /// OPTIMIZED: Enhanced with win rate monitoring and immediate protection
        /// </summary>
        public RevFibNotchAdjustment ProcessDailyPnL(decimal dailyPnL, DateTime date, decimal dailyWinRate = 0m)
        {
            var dailyResult = new RevFibNotchDailyResult
            {
                Date = date,
                PnL = dailyPnL,
                RFibLevelBefore = _currentNotchIndex
            };

            // OPTIMIZED: Check immediate protection trigger first
            if (dailyPnL <= _config.ProtectiveTriggerLoss)
            {
                var protectiveAdjustment = new RevFibNotchMovementDecision
                {
                    NotchMovement = 2, // Immediate 2-notch protection
                    Reason = $"IMMEDIATE_PROTECTION_{dailyPnL:C}"
                };
                ApplyNotchMovement(protectiveAdjustment);
            }
            
            // OPTIMIZED: Check win rate threshold
            if (dailyWinRate > 0 && dailyWinRate < _config.WinRateThreshold)
            {
                var winRateAdjustment = new RevFibNotchMovementDecision
                {
                    NotchMovement = 1, // Scale down for poor win rate
                    Reason = $"LOW_WINRATE_{dailyWinRate:P1}"
                };
                ApplyNotchMovement(winRateAdjustment);
            }
            
            // Calculate normal notch movement based on P&L
            var adjustment = CalculateNotchMovement(dailyPnL);
            
            // Apply movement
            var oldIndex = _currentNotchIndex;
            var oldLimit = _rFibLimits[_currentNotchIndex];
            
            ApplyNotchMovement(adjustment);
            
            dailyResult.RFibLevelAfter = _currentNotchIndex;
            dailyResult.NotchMovement = adjustment.NotchMovement;
            dailyResult.MovementReason = adjustment.Reason;
            
            // Update recent days history
            _recentDays.Add(dailyResult);
            if (_recentDays.Count > _config.MaxHistoryDays)
            {
                _recentDays.RemoveAt(0);
            }

            var result = new RevFibNotchAdjustment
            {
                Date = date,
                DailyPnL = dailyPnL,
                OldNotchIndex = oldIndex,
                NewNotchIndex = _currentNotchIndex,
                OldRFibLimit = oldLimit,
                NewRFibLimit = _rFibLimits[_currentNotchIndex],
                NotchMovement = adjustment.NotchMovement,
                Reason = adjustment.Reason,
                ConsecutiveProfitDays = CountConsecutiveProfitDays(),
                RecentDrawdown = CalculateRecentDrawdown()
            };

            LogNotchAdjustment(result);
            return result;
        }

        /// <summary>
        /// Calculate required notch movement based on P&L magnitude
        /// </summary>
        private RevFibNotchMovementDecision CalculateNotchMovement(decimal dailyPnL)
        {
            if (dailyPnL < 0)
            {
                // Loss: Immediate rightward movement (more conservative)
                return CalculateLossMovement(Math.Abs(dailyPnL));
            }
            else if (dailyPnL > 0)
            {
                // Profit: Potential leftward movement (more aggressive) if sustained
                return CalculateProfitMovement(dailyPnL);
            }
            else
            {
                // Breakeven: No movement
                return new RevFibNotchMovementDecision
                {
                    NotchMovement = 0,
                    Reason = "BREAKEVEN"
                };
            }
        }

        /// <summary>
        /// Calculate rightward movement based on loss magnitude
        /// </summary>
        private RevFibNotchMovementDecision CalculateLossMovement(decimal lossAmount)
        {
            var currentLimit = _rFibLimits[_currentNotchIndex];
            var lossPercentage = lossAmount / currentLimit;

            // OPTIMIZED: More sensitive loss thresholds with scaling sensitivity
            decimal adjustedLossPercentage = lossPercentage * _config.ScalingSensitivity;
            
            int notchMovement = adjustedLossPercentage switch
            {
                >= 0.60m => 3, // OPTIMIZED: Catastrophic loss (was 0.80m)
                >= 0.35m => 2, // OPTIMIZED: Major loss (was 0.50m)  
                >= 0.15m => 1, // OPTIMIZED: Significant loss (was 0.25m)
                >= 0.06m => 1, // OPTIMIZED: Mild loss (was 0.10m)
                _ => 0          // Minimal loss: No movement
            };

            var reason = adjustedLossPercentage switch
            {
                >= 0.60m => "CATASTROPHIC_LOSS_OPTIMIZED",
                >= 0.35m => "MAJOR_LOSS_OPTIMIZED",
                >= 0.15m => "SIGNIFICANT_LOSS_OPTIMIZED", 
                >= 0.06m => "MILD_LOSS_OPTIMIZED",
                _ => "MINIMAL_LOSS"
            };

            return new RevFibNotchMovementDecision
            {
                NotchMovement = notchMovement, // Positive = rightward (more conservative)
                Reason = $"{reason}_{lossPercentage:P1}"
            };
        }

        /// <summary>
        /// Calculate leftward movement based on profit magnitude and consistency
        /// </summary>
        private RevFibNotchMovementDecision CalculateProfitMovement(decimal profitAmount)
        {
            var currentLimit = _rFibLimits[_currentNotchIndex];
            var profitPercentage = profitAmount / currentLimit;
            var consecutiveProfitDays = CountConsecutiveProfitDays() + 1; // Include today

            // Major profit: Immediate leftward movement
            if (profitPercentage >= _config.MajorProfitThreshold)
            {
                return new RevFibNotchMovementDecision
                {
                    NotchMovement = -1, // Negative = leftward (more aggressive)
                    Reason = $"MAJOR_PROFIT_{profitPercentage:P1}"
                };
            }

            // Sustained mild profit: Requires consecutive days
            if (profitPercentage >= _config.MildProfitThreshold && 
                consecutiveProfitDays >= _config.RequiredConsecutiveProfitDays)
            {
                return new RevFibNotchMovementDecision
                {
                    NotchMovement = -1,
                    Reason = $"SUSTAINED_PROFIT_{consecutiveProfitDays}DAYS_{profitPercentage:P1}"
                };
            }

            // Insufficient for movement
            return new RevFibNotchMovementDecision
            {
                NotchMovement = 0,
                Reason = $"PROFIT_INSUFFICIENT_{profitPercentage:P1}_{consecutiveProfitDays}DAYS"
            };
        }

        /// <summary>
        /// Apply calculated notch movement with boundary checks
        /// </summary>
        private void ApplyNotchMovement(RevFibNotchMovementDecision decision)
        {
            if (decision.NotchMovement == 0) return;

            var newIndex = _currentNotchIndex + decision.NotchMovement;
            
            // Enforce boundaries
            newIndex = Math.Max(0, Math.Min(_rFibLimits.Length - 1, newIndex));
            
            _currentNotchIndex = newIndex;
        }

        /// <summary>
        /// Count consecutive profitable days leading up to today
        /// </summary>
        private int CountConsecutiveProfitDays()
        {
            int count = 0;
            for (int i = _recentDays.Count - 1; i >= 0; i--)
            {
                if (_recentDays[i].PnL > 0)
                    count++;
                else
                    break;
            }
            return count;
        }

        /// <summary>
        /// Calculate recent maximum drawdown
        /// </summary>
        private decimal CalculateRecentDrawdown()
        {
            if (_recentDays.Count < 2) return 0m;

            decimal peak = 0m;
            decimal maxDrawdown = 0m;
            decimal runningPnL = 0m;

            foreach (var day in _recentDays.TakeLast(_config.DrawdownLookbackDays))
            {
                runningPnL += day.PnL;
                peak = Math.Max(peak, runningPnL);
                var drawdown = peak - runningPnL;
                maxDrawdown = Math.Max(maxDrawdown, drawdown);
            }

            return maxDrawdown;
        }

        /// <summary>
        /// Initialize loss thresholds for notch movement
        /// </summary>
        private Dictionary<int, decimal> InitializeLossThresholds()
        {
            return new Dictionary<int, decimal>
            {
                { 1, 0.10m }, // 1 notch: 10%+ loss
                { 2, 0.25m }, // 2 notches: 25%+ loss
                { 3, 0.50m }  // 3 notches: 50%+ loss
            };
        }

        /// <summary>
        /// Initialize profit thresholds for notch movement
        /// </summary>
        private Dictionary<int, decimal> InitializeProfitThresholds()
        {
            return new Dictionary<int, decimal>
            {
                { 1, 0.20m }  // 1 notch left: 20%+ profit (major) or sustained mild
            };
        }

        /// <summary>
        /// Log notch adjustment for monitoring and analysis
        /// </summary>
        private void LogNotchAdjustment(RevFibNotchAdjustment adjustment)
        {
            if (adjustment.NotchMovement != 0)
            {
                Console.WriteLine($"[{adjustment.Date:yyyy-MM-dd}] NOTCH-RFIB ADJUSTMENT:");
                Console.WriteLine($"  P&L: {adjustment.DailyPnL:C} | Movement: {adjustment.NotchMovement:+0;-0;0} notches");
                Console.WriteLine($"  RFib: {adjustment.OldRFibLimit:C} → {adjustment.NewRFibLimit:C}");
                Console.WriteLine($"  Reason: {adjustment.Reason}");
                Console.WriteLine($"  Consecutive Profit Days: {adjustment.ConsecutiveProfitDays}");
                Console.WriteLine($"  Recent Drawdown: {adjustment.RecentDrawdown:C}");
            }
        }

        // Public Properties and Methods

        public decimal CurrentRFibLimit => _rFibLimits[_currentNotchIndex];
        public int CurrentNotchIndex => _currentNotchIndex;
        public decimal[] AllRFibLimits => _rFibLimits.ToArray();
        public int ConsecutiveProfitDays => CountConsecutiveProfitDays();
        public decimal RecentDrawdown => CalculateRecentDrawdown();

        /// <summary>
        /// Get detailed status for monitoring
        /// </summary>
        public RevFibNotchStatus GetStatus()
        {
            return new RevFibNotchStatus
            {
                CurrentLimit = CurrentRFibLimit,
                CurrentNotchIndex = _currentNotchIndex,
                NotchPosition = $"{_currentNotchIndex + 1}/{_rFibLimits.Length}",
                ConsecutiveProfitDays = CountConsecutiveProfitDays(),
                RecentDrawdown = CalculateRecentDrawdown(),
                DaysInCurrentPosition = GetDaysInCurrentPosition(),
                AllLimits = _rFibLimits.ToArray(),
                RecentHistory = _recentDays.TakeLast(5).ToList()
            };
        }

        /// <summary>
        /// Get number of days in current RFib position
        /// </summary>
        private int GetDaysInCurrentPosition()
        {
            int days = 0;
            for (int i = _recentDays.Count - 1; i >= 0; i--)
            {
                if (_recentDays[i].RFibLevelAfter == _currentNotchIndex)
                    days++;
                else
                    break;
            }
            return days;
        }

        /// <summary>
        /// Reset to specific notch position (for testing or emergency)
        /// </summary>
        public void ResetToNotch(int notchIndex, string reason = "MANUAL_RESET")
        {
            if (notchIndex < 0 || notchIndex >= _rFibLimits.Length)
                throw new ArgumentOutOfRangeException(nameof(notchIndex));

            var oldIndex = _currentNotchIndex;
            _currentNotchIndex = notchIndex;

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd}] NOTCH-RFIB MANUAL RESET:");
            Console.WriteLine($"  Position: {oldIndex} → {notchIndex}");
            Console.WriteLine($"  RFib: {_rFibLimits[oldIndex]:C} → {_rFibLimits[notchIndex]:C}");
            Console.WriteLine($"  Reason: {reason}");
        }
    }

    // Supporting Types

    public class RevFibNotchConfiguration
    {
        public int RequiredConsecutiveProfitDays { get; set; } = 1; // OPTIMIZED: Faster scaling up
        public decimal MildProfitThreshold { get; set; } = 0.08m; // OPTIMIZED: 8% profit threshold (more sensitive)
        public decimal MajorProfitThreshold { get; set; } = 0.25m; // OPTIMIZED: 25% profit for immediate upgrade
        public int MaxHistoryDays { get; set; } = 30;
        public int DrawdownLookbackDays { get; set; } = 10;
        public decimal WinRateThreshold { get; set; } = 0.71m; // ULTRA-OPTIMIZED: Scale down if win rate <71%
        public decimal ProtectiveTriggerLoss { get; set; } = -75m; // ULTRA-OPTIMIZED: Immediate protection at -$75
        public decimal ScalingSensitivity { get; set; } = 2.26m; // ULTRA-OPTIMIZED: 2.26x faster scaling
    }

    public class RevFibNotchDailyResult
    {
        public DateTime Date { get; set; }
        public decimal PnL { get; set; }
        public int RFibLevelBefore { get; set; }
        public int RFibLevelAfter { get; set; }
        public int NotchMovement { get; set; }
        public string MovementReason { get; set; } = string.Empty;
    }

    public class RevFibNotchMovementDecision
    {
        public int NotchMovement { get; set; } // Positive = right (conservative), Negative = left (aggressive)
        public string Reason { get; set; } = string.Empty;
    }

    public class RevFibNotchAdjustment
    {
        public DateTime Date { get; set; }
        public decimal DailyPnL { get; set; }
        public int OldNotchIndex { get; set; }
        public int NewNotchIndex { get; set; }
        public decimal OldRFibLimit { get; set; }
        public decimal NewRFibLimit { get; set; }
        public int NotchMovement { get; set; }
        public string Reason { get; set; } = string.Empty;
        public int ConsecutiveProfitDays { get; set; }
        public decimal RecentDrawdown { get; set; }
    }

    public class RevFibNotchStatus
    {
        public decimal CurrentLimit { get; set; }
        public int CurrentNotchIndex { get; set; }
        public string NotchPosition { get; set; } = string.Empty;
        public int ConsecutiveProfitDays { get; set; }
        public decimal RecentDrawdown { get; set; }
        public int DaysInCurrentPosition { get; set; }
        public decimal[] AllLimits { get; set; } = Array.Empty<decimal>();
        public List<RevFibNotchDailyResult> RecentHistory { get; set; } = new();
    }
}