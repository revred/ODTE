using System;
using System.Collections.Generic;
using System.Linq;

namespace ODTE.Strategy
{
    /// <summary>
    /// Battle-Hardened Capital Preservation Engine
    /// Optimized based on real performance data showing $430 profit on 284 trades
    /// with 92.6% win rate but concerning $59.86 max drawdown
    /// 
    /// REAL DATA ANALYSIS INSIGHTS:
    /// - Current system: $1.52 per trade average, but max loss $26.37 is too high
    /// - Win rate 92.6% is excellent, but drawdown $59.86 represents 13.8% of profits
    /// - Need to cut tail risk while preserving high win rate
    /// </summary>
    public class BattleHardenedCapitalPreservation
    {
        private readonly List<decimal> _fibonacciLevels;
        private readonly Dictionary<string, decimal> _dailyLosses;
        private readonly Queue<decimal> _recentPerformance;
        private int _consecutiveLossingDays;
        private decimal _cumulativeDrawdown;
        private decimal _peakAccountValue;
        
        // REAL DATA OPTIMIZED PARAMETERS - BALANCED APPROACH
        private readonly decimal _maxSingleTradeRisk = 18.00m; // Reduced from observed $26.37 max loss but not too conservative
        private readonly decimal _dailyDrawdownLimit = 40.00m; // Reduced from observed $59.86 but allows trading
        private readonly decimal _emergencyStopLoss = 15.00m; // Hard stop before major losses
        
        public BattleHardenedCapitalPreservation()
        {
            // Balanced Fibonacci curtailment - preserve high win rate while controlling risk
            _fibonacciLevels = new List<decimal> 
            { 
                500m,  // Day 0: Start at normal level  
                350m,  // Day 1: Moderate reduction after first loss
                250m,  // Day 2: More conservative
                150m,  // Day 3: Significantly reduced
                100m,  // Day 4: Conservative position
                75m,   // Day 5: Minimal risk
                50m,   // Day 6+: Emergency level
            };
            
            _dailyLosses = new Dictionary<string, decimal>();
            _recentPerformance = new Queue<decimal>();
            _consecutiveLossingDays = 0;
            _cumulativeDrawdown = 0m;
            _peakAccountValue = 0m;
        }

        /// <summary>
        /// Get battle-hardened daily risk limit based on real performance analysis
        /// </summary>
        public decimal GetDailyRiskLimit(decimal accountSize, MarketConditions conditions)
        {
            // Base Fibonacci level
            var fibIndex = Math.Min(_consecutiveLossingDays, _fibonacciLevels.Count - 1);
            var baseLimit = _fibonacciLevels[fibIndex];
            
            // Real data shows we need VIX-based scaling
            var vixMultiplier = CalculateVixMultiplier(conditions.VIX);
            
            // Trend risk adjustment (real data shows trend days had larger losses)
            var trendAdjustment = CalculateTrendRiskAdjustment(conditions.TrendScore);
            
            // Recent performance adjustment
            var performanceAdjustment = CalculatePerformanceAdjustment();
            
            // Apply all adjustments
            var adjustedLimit = baseLimit * vixMultiplier * trendAdjustment * performanceAdjustment;
            
            // Absolute safety caps based on real data analysis - more balanced
            adjustedLimit = Math.Min(adjustedLimit, accountSize * 0.025m); // Never more than 2.5% of account (less restrictive)
            adjustedLimit = Math.Min(adjustedLimit, _dailyDrawdownLimit);
            
            // Emergency stop if in severe drawdown
            if (_cumulativeDrawdown > accountSize * 0.12m) // 12% drawdown triggers emergency mode (more tolerant)
            {
                adjustedLimit = Math.Min(adjustedLimit, 50m); // Higher emergency minimum
            }
            
            return Math.Max(adjustedLimit, 10m); // Minimum viable trade size
        }

        /// <summary>
        /// VIX multiplier based on real data correlation analysis
        /// </summary>
        private decimal CalculateVixMultiplier(double vix)
        {
            // Real data shows higher losses on high VIX days
            return vix switch
            {
                < 15 => 1.2m,   // Low vol - can be slightly more aggressive
                < 20 => 1.0m,   // Normal vol - standard sizing
                < 25 => 0.8m,   // Elevated vol - reduce size
                < 30 => 0.6m,   // High vol - significant reduction
                < 40 => 0.4m,   // Crisis vol - major reduction
                >= 40 => 0.25m, // Extreme vol - emergency only
                _ => 1.0m       // Default case
            };
        }

        /// <summary>
        /// Trend risk adjustment based on real data analysis
        /// </summary>
        private decimal CalculateTrendRiskAdjustment(double trendScore)
        {
            var absTrend = Math.Abs(trendScore);
            
            // Strong trends caused some of the larger losses in real data
            return absTrend switch
            {
                < 0.2 => 1.0m,   // Calm market - normal sizing
                < 0.4 => 0.9m,   // Mild trend - slight reduction
                < 0.6 => 0.75m,  // Moderate trend - reduce size
                < 0.8 => 0.6m,   // Strong trend - significant reduction
                >= 0.8 => 0.4m,  // Extreme trend - major reduction
                _ => 1.0m        // Default case
            };
        }

        /// <summary>
        /// Performance-based adjustment using recent results
        /// </summary>
        private decimal CalculatePerformanceAdjustment()
        {
            if (_recentPerformance.Count < 5) return 1.0m;
            
            var recentTotal = _recentPerformance.Sum();
            var recentAverage = recentTotal / _recentPerformance.Count;
            
            // If recent performance is poor, reduce risk
            if (recentAverage < -5m) return 0.6m;  // Recent losses - reduce significantly
            if (recentAverage < 0m) return 0.8m;   // Recent flat - reduce moderately
            if (recentAverage > 5m) return 1.1m;   // Recent gains - slight increase
            
            return 1.0m; // Neutral
        }

        /// <summary>
        /// Check if individual trade should be blocked based on real data insights
        /// </summary>
        public bool ShouldBlockTrade(decimal proposedRisk, MarketConditions conditions, decimal accountSize)
        {
            // Block if single trade risk exceeds our battle-hardened limit
            if (proposedRisk > _maxSingleTradeRisk) return true;
            
            // Block if daily limit would be exceeded
            var todayKey = DateTime.Today.ToString("yyyy-MM-dd");
            var todayLosses = _dailyLosses.GetValueOrDefault(todayKey, 0m);
            if (todayLosses + proposedRisk > GetDailyRiskLimit(accountSize, conditions)) return true;
            
            // Block if in emergency drawdown mode - more tolerant
            if (_cumulativeDrawdown > accountSize * 0.15m) return true; // 15% vs 10%
            
            // Block if VIX is extremely high - more tolerant
            if (conditions.VIX > 55) return true; // 55 vs 45
            
            // Block if extreme trend conditions - more tolerant  
            if (Math.Abs(conditions.TrendScore) > 1.2) return true; // Allow more trend
            
            return false;
        }

        /// <summary>
        /// Record trade result and update internal state
        /// </summary>
        public void RecordTradeResult(decimal pnl, DateTime tradeDate)
        {
            var dateKey = tradeDate.ToString("yyyy-MM-dd");
            
            // Update daily tracking
            if (pnl < 0)
            {
                _dailyLosses[dateKey] = _dailyLosses.GetValueOrDefault(dateKey, 0m) + Math.Abs(pnl);
            }
            
            // Update recent performance queue
            _recentPerformance.Enqueue(pnl);
            if (_recentPerformance.Count > 10) _recentPerformance.Dequeue();
            
            // Update consecutive losing days
            if (GetDailyPnL(dateKey) < 0)
            {
                _consecutiveLossingDays++;
            }
            else if (GetDailyPnL(dateKey) > 0)
            {
                _consecutiveLossingDays = 0; // Reset on profitable day
            }
            
            // Update drawdown tracking
            var currentAccountValue = _peakAccountValue + _recentPerformance.Sum();
            if (currentAccountValue > _peakAccountValue)
            {
                _peakAccountValue = currentAccountValue;
                _cumulativeDrawdown = 0;
            }
            else
            {
                _cumulativeDrawdown = _peakAccountValue - currentAccountValue;
            }
        }

        /// <summary>
        /// Get current Fibonacci level for transparency
        /// </summary>
        public (int Level, decimal Limit) GetCurrentFibonacciState()
        {
            var level = Math.Min(_consecutiveLossingDays, _fibonacciLevels.Count - 1);
            return (level, _fibonacciLevels[level]);
        }

        /// <summary>
        /// Get comprehensive risk status for monitoring
        /// </summary>
        public CapitalPreservationStatus GetRiskStatus(decimal accountSize)
        {
            return new CapitalPreservationStatus
            {
                ConsecutiveLossingDays = _consecutiveLossingDays,
                CurrentFibonacciLevel = GetCurrentFibonacciState().Level,
                CurrentDailyLimit = _fibonacciLevels[GetCurrentFibonacciState().Level],
                CumulativeDrawdown = _cumulativeDrawdown,
                DrawdownPercentage = (double)(_cumulativeDrawdown / accountSize * 100),
                IsEmergencyMode = _cumulativeDrawdown > accountSize * 0.08m,
                RecentPerformanceAverage = _recentPerformance.Count > 0 ? _recentPerformance.Average() : 0m
            };
        }

        private decimal GetDailyPnL(string dateKey)
        {
            // This would need to be calculated from actual trade records
            // For now, simplified logic
            return _dailyLosses.GetValueOrDefault(dateKey, 0m) > 0 ? -_dailyLosses[dateKey] : 5m;
        }
    }

    /// <summary>
    /// Status report for capital preservation monitoring
    /// </summary>
    public class CapitalPreservationStatus
    {
        public int ConsecutiveLossingDays { get; set; }
        public int CurrentFibonacciLevel { get; set; }
        public decimal CurrentDailyLimit { get; set; }
        public decimal CumulativeDrawdown { get; set; }
        public double DrawdownPercentage { get; set; }
        public bool IsEmergencyMode { get; set; }
        public decimal RecentPerformanceAverage { get; set; }
    }
}