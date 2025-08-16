using System;
using System.Collections.Generic;
using System.Linq;

namespace ODTE.Strategy
{
    /// <summary>
    /// Enhanced Reverse Fibonacci Risk Management System (Tier A-1.3 Enhancement)
    /// 
    /// INTELLIGENT CURTAILMENT STRATEGY:
    /// - Progressive position reduction after consecutive losses
    /// - Fibonacci-based scaling: 100% -> 62% -> 38% -> 23% -> 15%
    /// - Immediate reset on profitable day
    /// - Maximum drawdown protection
    /// - Volume-optimized risk scaling for high-frequency trading
    /// 
    /// NEW TIER A ENHANCEMENTS:
    /// - RemainingDailyRFibBudget tracking for per-trade risk validation
    /// - Dynamic daily budget limits based on consecutive loss count
    /// - Real-time budget consumption monitoring
    /// - Integration with PerTradeRiskManager for budget validation
    /// </summary>
    public class ReverseFibonacciRiskManager
    {
        private readonly List<decimal> _fibonacciLevels;
        private readonly Dictionary<DateTime, decimal> _dailyPnL;
        private readonly Dictionary<DateTime, decimal> _dailyBudgetUsed;
        private readonly List<TradeExecution> _consecutiveLossTracker;
        
        // Enhanced risk management configuration
        private const decimal BASE_POSITION_SIZE = 1.0m;
        private const decimal MIN_POSITION_SIZE = 0.15m; // 15% minimum
        private const decimal MAX_DAILY_DRAWDOWN = 75.0m;
        private const decimal MAX_WEEKLY_DRAWDOWN = 300.0m;
        private const int MAX_CONSECUTIVE_LOSSES = 5;
        
        // NEW: Dynamic daily budget limits (Tier A enhancement)
        private readonly decimal[] DAILY_BUDGET_LIMITS = { 625.0m, 385.0m, 240.0m, 150.0m };
        private int _consecutiveLossDays = 0;

        public ReverseFibonacciRiskManager()
        {
            // Reverse Fibonacci sequence for position scaling
            _fibonacciLevels = new List<decimal>
            {
                1.00m,  // Day 0: Full position (100%)
                0.62m,  // Day 1: 62% (Golden ratio reduction)
                0.38m,  // Day 2: 38% (Further reduction)
                0.23m,  // Day 3: 23% (Conservative)
                0.15m   // Day 4+: 15% (Minimum viable)
            };
            
            _dailyPnL = new Dictionary<DateTime, decimal>();
            _dailyBudgetUsed = new Dictionary<DateTime, decimal>();
            _consecutiveLossTracker = new List<TradeExecution>();
        }
        
        #region Tier A Enhancement Methods - Budget Tracking
        
        /// <summary>
        /// Get remaining daily budget for risk validation (Tier A-1.3)
        /// </summary>
        /// <param name="tradingDay">Trading day to check</param>
        /// <returns>Remaining budget amount available for new trades</returns>
        public decimal GetRemainingDailyBudget(DateTime tradingDay)
        {
            var dayKey = tradingDay.Date;
            var dailyLimit = GetDailyBudgetLimit(tradingDay);
            var usedBudget = _dailyBudgetUsed.ContainsKey(dayKey) ? _dailyBudgetUsed[dayKey] : 0m;
            
            return Math.Max(0m, dailyLimit - usedBudget);
        }
        
        /// <summary>
        /// Get current daily budget limit based on consecutive loss streak
        /// </summary>
        /// <param name="tradingDay">Trading day to check</param>
        /// <returns>Daily budget limit in dollars</returns>
        public decimal GetDailyBudgetLimit(DateTime tradingDay)
        {
            var levelIndex = Math.Min(_consecutiveLossDays, DAILY_BUDGET_LIMITS.Length - 1);
            return DAILY_BUDGET_LIMITS[levelIndex];
        }
        
        /// <summary>
        /// Record actual trade loss for budget tracking
        /// </summary>
        /// <param name="tradingDay">Trading day</param>
        /// <param name="lossAmount">Actual loss amount (positive number)</param>
        public void RecordTradeLoss(DateTime tradingDay, decimal lossAmount)
        {
            var dayKey = tradingDay.Date;
            
            if (!_dailyBudgetUsed.ContainsKey(dayKey))
            {
                _dailyBudgetUsed[dayKey] = 0m;
            }
            
            _dailyBudgetUsed[dayKey] += Math.Abs(lossAmount);
            
            // Update daily P&L tracking
            if (!_dailyPnL.ContainsKey(dayKey))
            {
                _dailyPnL[dayKey] = 0m;
            }
            _dailyPnL[dayKey] -= Math.Abs(lossAmount);
        }
        
        /// <summary>
        /// Record profitable trade for budget and streak tracking
        /// </summary>
        /// <param name="tradingDay">Trading day</param>
        /// <param name="profitAmount">Profit amount (positive number)</param>
        public void RecordTradeProfit(DateTime tradingDay, decimal profitAmount)
        {
            var dayKey = tradingDay.Date;
            
            // Update daily P&L tracking
            if (!_dailyPnL.ContainsKey(dayKey))
            {
                _dailyPnL[dayKey] = 0m;
            }
            _dailyPnL[dayKey] += Math.Abs(profitAmount);
            
            // Check if this makes the day profitable and reset streak
            if (_dailyPnL[dayKey] > 0m)
            {
                _consecutiveLossDays = 0; // Reset on profitable day
            }
        }
        
        /// <summary>
        /// Check if trading is allowed based on budget and limits
        /// </summary>
        /// <param name="tradingDay">Trading day to check</param>
        /// <returns>True if trading is allowed</returns>
        public bool CanTrade(DateTime tradingDay)
        {
            var remainingBudget = GetRemainingDailyBudget(tradingDay);
            return remainingBudget > 10m; // Minimum $10 buffer for micro trades
        }
        
        /// <summary>
        /// Update consecutive loss day count (called at end of day)
        /// </summary>
        /// <param name="tradingDay">Trading day that ended</param>
        public void UpdateDayEndStatus(DateTime tradingDay)
        {
            var dayKey = tradingDay.Date;
            var dayPnL = _dailyPnL.ContainsKey(dayKey) ? _dailyPnL[dayKey] : 0m;
            
            if (dayPnL < 0m)
            {
                _consecutiveLossDays++;
            }
            else if (dayPnL > 0m)
            {
                _consecutiveLossDays = 0; // Reset on profitable day
            }
            
            // Cap at maximum levels
            _consecutiveLossDays = Math.Min(_consecutiveLossDays, DAILY_BUDGET_LIMITS.Length - 1);
        }
        
        /// <summary>
        /// Get current risk management status
        /// </summary>
        /// <param name="tradingDay">Trading day to analyze</param>
        /// <returns>Risk status summary</returns>
        public RiskStatus GetRiskStatus(DateTime tradingDay)
        {
            var dailyLimit = GetDailyBudgetLimit(tradingDay);
            var remainingBudget = GetRemainingDailyBudget(tradingDay);
            var budgetUtilization = dailyLimit > 0 ? (double)((dailyLimit - remainingBudget) / dailyLimit) : 0;
            
            return new RiskStatus
            {
                TradingDay = tradingDay,
                ConsecutiveLossDays = _consecutiveLossDays,
                DailyBudgetLimit = dailyLimit,
                RemainingBudget = remainingBudget,
                BudgetUtilization = budgetUtilization,
                CanTrade = CanTrade(tradingDay),
                RiskLevel = GetRiskLevel(budgetUtilization, _consecutiveLossDays)
            };
        }
        
        private RiskLevel GetRiskLevel(double budgetUtilization, int consecutiveLossDays)
        {
            if (consecutiveLossDays >= 3 || budgetUtilization > 0.8)
                return RiskLevel.High;
            else if (consecutiveLossDays >= 2 || budgetUtilization > 0.6)
                return RiskLevel.Medium;
            else if (consecutiveLossDays >= 1 || budgetUtilization > 0.3)
                return RiskLevel.Low;
            else
                return RiskLevel.Minimal;
        }
        
        #endregion

        public decimal CalculatePositionSize(decimal baseSize, List<TradeExecution> recentTrades)
        {
            try
            {
                // Step 1: Update tracking with recent trades
                UpdateTrackingData(recentTrades);

                // Step 2: Check for immediate risk blocks
                var riskCheck = AssessImmediateRisk(recentTrades);
                if (riskCheck.ShouldBlock)
                    return 0m;

                // Step 3: Calculate consecutive loss curtailment
                var lossAdjustment = CalculateLossCurtailment(recentTrades);

                // Step 4: Apply daily drawdown protection
                var drawdownAdjustment = CalculateDrawdownProtection(recentTrades);

                // Step 5: Apply weekly risk scaling
                var weeklyAdjustment = CalculateWeeklyRiskScaling(recentTrades);

                // Step 6: Volume optimization for high-frequency trading
                var volumeAdjustment = CalculateVolumeOptimization(recentTrades);

                // Step 7: Combine all adjustments
                var finalAdjustment = Math.Min(lossAdjustment, drawdownAdjustment);
                finalAdjustment = Math.Min(finalAdjustment, weeklyAdjustment);
                finalAdjustment *= volumeAdjustment;

                // Apply minimum position size floor
                var adjustedSize = Math.Max(baseSize * finalAdjustment, baseSize * MIN_POSITION_SIZE);

                return Math.Round(adjustedSize, 3);
            }
            catch (Exception ex)
            {
                // Fail-safe: return minimum position on error
                return baseSize * MIN_POSITION_SIZE;
            }
        }

        private void UpdateTrackingData(List<TradeExecution> recentTrades)
        {
            // Update daily P&L tracking
            _dailyPnL.Clear();
            var dailyGroups = recentTrades
                .Where(t => t.ExecutionTime >= DateTime.Today.AddDays(-30)) // Last 30 days
                .GroupBy(t => t.ExecutionTime.Date);

            foreach (var day in dailyGroups)
            {
                _dailyPnL[day.Key] = day.Sum(t => t.PnL);
            }

            // Update consecutive loss tracking
            _consecutiveLossTracker.Clear();
            var orderedTrades = recentTrades.OrderByDescending(t => t.ExecutionTime).ToList();
            
            foreach (var trade in orderedTrades)
            {
                if (trade.PnL < 0)
                    _consecutiveLossTracker.Add(trade);
                else
                    break; // Stop at first profitable trade
            }
        }

        private RiskBlock AssessImmediateRisk(List<TradeExecution> recentTrades)
        {
            var today = DateTime.Today;
            
            // Check daily drawdown limit
            if (_dailyPnL.ContainsKey(today))
            {
                var todayLoss = Math.Abs(_dailyPnL[today]);
                if (todayLoss > MAX_DAILY_DRAWDOWN)
                {
                    return new RiskBlock
                    {
                        ShouldBlock = true,
                        Reason = $"Daily drawdown limit exceeded: ${todayLoss:F2} > ${MAX_DAILY_DRAWDOWN:F2}"
                    };
                }
            }

            // Check excessive consecutive losses
            if (_consecutiveLossTracker.Count > MAX_CONSECUTIVE_LOSSES)
            {
                return new RiskBlock
                {
                    ShouldBlock = true,
                    Reason = $"Excessive consecutive losses: {_consecutiveLossTracker.Count} > {MAX_CONSECUTIVE_LOSSES}"
                };
            }

            // Check weekly drawdown limit
            var weekStart = today.AddDays(-(int)today.DayOfWeek);
            var weeklyLoss = _dailyPnL
                .Where(kvp => kvp.Key >= weekStart && kvp.Value < 0)
                .Sum(kvp => Math.Abs(kvp.Value));
            
            if (weeklyLoss > MAX_WEEKLY_DRAWDOWN)
            {
                return new RiskBlock
                {
                    ShouldBlock = true,
                    Reason = $"Weekly drawdown limit exceeded: ${weeklyLoss:F2} > ${MAX_WEEKLY_DRAWDOWN:F2}"
                };
            }

            return new RiskBlock { ShouldBlock = false };
        }

        private decimal CalculateLossCurtailment(List<TradeExecution> recentTrades)
        {
            // Check for recent profitable performance (reset condition)
            var lastTrade = recentTrades.LastOrDefault();
            if (lastTrade?.PnL > 0)
                return 1.0m; // Reset to full position on profit

            // Check for profitable day (daily reset condition)
            var today = DateTime.Today;
            if (_dailyPnL.ContainsKey(today) && _dailyPnL[today] > 0)
                return 1.0m; // Reset to full position on profitable day

            // Calculate consecutive losing days
            var consecutiveLossDays = 0;
            var currentDate = today;
            
            for (int i = 0; i < 10; i++) // Check last 10 days
            {
                if (_dailyPnL.ContainsKey(currentDate) && _dailyPnL[currentDate] < 0)
                    consecutiveLossDays++;
                else
                    break;
                    
                currentDate = currentDate.AddDays(-1);
            }

            // Apply Fibonacci curtailment based on consecutive loss days
            var levelIndex = Math.Min(consecutiveLossDays, _fibonacciLevels.Count - 1);
            return _fibonacciLevels[levelIndex];
        }

        private decimal CalculateDrawdownProtection(List<TradeExecution> recentTrades)
        {
            var today = DateTime.Today;
            
            if (!_dailyPnL.ContainsKey(today))
                return 1.0m; // No trades today yet
            
            var todayPnL = _dailyPnL[today];
            
            // Progressive scaling based on daily drawdown
            if (todayPnL >= 0)
                return 1.0m; // Profitable day, no scaling
            
            var dailyLoss = Math.Abs(todayPnL);
            var lossRatio = dailyLoss / MAX_DAILY_DRAWDOWN;
            
            if (lossRatio <= 0.25m)
                return 1.0m;    // 0-25% of daily limit: full position
            else if (lossRatio <= 0.50m)
                return 0.8m;    // 25-50% of daily limit: 80% position
            else if (lossRatio <= 0.75m)
                return 0.6m;    // 50-75% of daily limit: 60% position
            else
                return 0.4m;    // 75-100% of daily limit: 40% position
        }

        private decimal CalculateWeeklyRiskScaling(List<TradeExecution> recentTrades)
        {
            var weekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
            var weeklyPnL = _dailyPnL
                .Where(kvp => kvp.Key >= weekStart)
                .Sum(kvp => kvp.Value);
            
            if (weeklyPnL >= 0)
                return 1.0m; // Profitable week, no scaling
            
            var weeklyLoss = Math.Abs(weeklyPnL);
            var weeklyLossRatio = weeklyLoss / MAX_WEEKLY_DRAWDOWN;
            
            if (weeklyLossRatio <= 0.33m)
                return 1.0m;    // 0-33% of weekly limit: full position
            else if (weeklyLossRatio <= 0.66m)
                return 0.7m;    // 33-66% of weekly limit: 70% position
            else
                return 0.5m;    // 66-100% of weekly limit: 50% position
        }

        private decimal CalculateVolumeOptimization(List<TradeExecution> recentTrades)
        {
            // High-frequency trading volume optimization
            var recentPerformance = recentTrades.TakeLast(20).ToList();
            
            if (!recentPerformance.Any())
                return 1.0m;
            
            var winRate = recentPerformance.Count(t => t.PnL > 0) / (double)recentPerformance.Count;
            var avgPnL = recentPerformance.Average(t => t.PnL);
            
            // Scale up during excellent performance
            if (winRate >= 0.95 && avgPnL > 20m)
                return 1.2m;    // 20% increase for exceptional performance
            else if (winRate >= 0.90 && avgPnL > 15m)
                return 1.1m;    // 10% increase for strong performance
            else if (winRate >= 0.80 && avgPnL > 10m)
                return 1.0m;    // Normal position for good performance
            else if (winRate >= 0.70)
                return 0.9m;    // 10% reduction for adequate performance
            else
                return 0.8m;    // 20% reduction for poor performance
        }

        public RiskMetrics GetCurrentRiskMetrics(List<TradeExecution> recentTrades)
        {
            UpdateTrackingData(recentTrades);
            
            var today = DateTime.Today;
            var weekStart = today.AddDays(-(int)today.DayOfWeek);
            
            var dailyPnL = _dailyPnL.GetValueOrDefault(today, 0m);
            var weeklyPnL = _dailyPnL
                .Where(kvp => kvp.Key >= weekStart)
                .Sum(kvp => kvp.Value);
            
            return new RiskMetrics
            {
                ConsecutiveLossDays = CalculateConsecutiveLossDays(),
                ConsecutiveLossTrades = _consecutiveLossTracker.Count,
                DailyPnL = dailyPnL,
                WeeklyPnL = weeklyPnL,
                DailyDrawdownUsed = Math.Abs(Math.Min(dailyPnL, 0m)) / MAX_DAILY_DRAWDOWN,
                WeeklyDrawdownUsed = Math.Abs(Math.Min(weeklyPnL, 0m)) / MAX_WEEKLY_DRAWDOWN,
                CurrentPositionScale = CalculatePositionSize(BASE_POSITION_SIZE, recentTrades) / BASE_POSITION_SIZE
            };
        }

        private int CalculateConsecutiveLossDays()
        {
            var consecutiveDays = 0;
            var currentDate = DateTime.Today;
            
            for (int i = 0; i < 10; i++)
            {
                if (_dailyPnL.ContainsKey(currentDate) && _dailyPnL[currentDate] < 0)
                    consecutiveDays++;
                else
                    break;
                    
                currentDate = currentDate.AddDays(-1);
            }
            
            return consecutiveDays;
        }
    }

    // Supporting classes
    public class RiskBlock
    {
        public bool ShouldBlock { get; set; }
        public string Reason { get; set; } = "";
    }

    public class RiskMetrics
    {
        public int ConsecutiveLossDays { get; set; }
        public int ConsecutiveLossTrades { get; set; }
        public decimal DailyPnL { get; set; }
        public decimal WeeklyPnL { get; set; }
        public decimal DailyDrawdownUsed { get; set; }
        public decimal WeeklyDrawdownUsed { get; set; }
        public decimal CurrentPositionScale { get; set; }
    }
    
    /// <summary>
    /// Risk management status for Tier A enhancements
    /// </summary>
    public class RiskStatus
    {
        public DateTime TradingDay { get; set; }
        public int ConsecutiveLossDays { get; set; }
        public decimal DailyBudgetLimit { get; set; }
        public decimal RemainingBudget { get; set; }
        public double BudgetUtilization { get; set; }
        public bool CanTrade { get; set; }
        public RiskLevel RiskLevel { get; set; }
        
        public string GetSummary()
        {
            return $"Day {TradingDay:MM/dd}: Risk Level {RiskLevel}, " +
                   $"Budget: ${RemainingBudget:F0}/${DailyBudgetLimit:F0} " +
                   $"({BudgetUtilization:P1} used), " +
                   $"Loss Streak: {ConsecutiveLossDays} days, " +
                   $"Trading: {(CanTrade ? "ALLOWED" : "BLOCKED")}";
        }
    }
    
    /// <summary>
    /// Risk level classification
    /// </summary>
    public enum RiskLevel
    {
        Minimal,    // 0 loss days, <30% budget used
        Low,        // 1 loss day, 30-60% budget used
        Medium,     // 2 loss days, 60-80% budget used
        High        // 3+ loss days, >80% budget used
    }
}