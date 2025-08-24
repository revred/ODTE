namespace ODTE.Strategy.SPX30DTE.Risk
{
    /// <summary>
    /// Enhanced RevFibNotch system specifically designed for 30DTE SPX strategies
    /// Handles higher capital allocation with controlled risk escalation
    /// Optimized for minimizing drawdown while allowing capital growth
    /// </summary>
    public class SPX30DTERevFibNotchManager : RevFibNotchManager
    {
        // Enhanced scale for 30DTE strategies with higher capital requirements
        private readonly decimal[] SPX_30DTE_NOTCH_LIMITS = new[]
        {
            8000m,    // Level 6 - Maximum (after sustained profitability 3+ months)
            5000m,    // Level 5 - Aggressive (after 6+ consecutive profitable weeks)
            3200m,    // Level 4 - Growth (after 4+ consecutive profitable weeks)
            2000m,    // Level 3 - Balanced (starting position - target level)
            1200m,    // Level 2 - Conservative (after 15%+ monthly loss)
            800m,     // Level 1 - Defensive (after 25%+ monthly loss)
            400m      // Level 0 - Survival (after 40%+ monthly loss or emergency)
        };

        private readonly SPX30DTENotchConfig _config;
        private readonly List<TradingDayResult> _tradingHistory;
        private readonly Dictionary<DateTime, decimal> _monthlyReturns;
        private int _currentNotchIndex;
        private DateTime _lastNotchChange;
        private int _consecutiveProfitableDays;
        private int _consecutiveProfitableWeeks;
        private decimal _monthToDatePnL;
        private decimal _peakMonthValue;

        public SPX30DTERevFibNotchManager(SPX30DTENotchConfig config = null)
        {
            _config = config ?? GetDefaultConfig();
            _tradingHistory = new List<TradingDayResult>();
            _monthlyReturns = new Dictionary<DateTime, decimal>();
            _currentNotchIndex = 3; // Start at Balanced level ($2000)
            _lastNotchChange = DateTime.Now;
            _peakMonthValue = SPX_30DTE_NOTCH_LIMITS[_currentNotchIndex];
        }

        public override decimal GetCurrentNotchLimit()
        {
            return SPX_30DTE_NOTCH_LIMITS[_currentNotchIndex];
        }

        public SPX30DTENotchLevel GetCurrentNotchLevel()
        {
            return (SPX30DTENotchLevel)_currentNotchIndex;
        }

        public override decimal UpdateNotchAfterTrade(decimal tradePnL, decimal totalPortfolioValue)
        {
            var today = DateTime.Now.Date;

            // Record trade result
            var dayResult = new TradingDayResult
            {
                Date = today,
                PnL = tradePnL,
                PortfolioValue = totalPortfolioValue,
                NotchLimitAtTime = GetCurrentNotchLimit(),
                NotchLevel = GetCurrentNotchLevel()
            };

            _tradingHistory.Add(dayResult);
            _monthToDatePnL += tradePnL;

            // Update consecutive tracking
            if (tradePnL > 0)
            {
                _consecutiveProfitableDays++;
            }
            else
            {
                _consecutiveProfitableDays = 0;
            }

            // Check for notch changes
            CheckForNotchAdjustment(dayResult);

            // Monthly reset tracking
            if (IsNewMonth(today))
            {
                ProcessMonthlyReset(today);
            }

            return GetCurrentNotchLimit();
        }

        public NotchAnalysis GetNotchAnalysis()
        {
            var analysis = new NotchAnalysis
            {
                CurrentLevel = GetCurrentNotchLevel(),
                CurrentLimit = GetCurrentNotchLimit(),
                DaysAtCurrentLevel = (DateTime.Now.Date - _lastNotchChange.Date).Days,
                ConsecutiveProfitableDays = _consecutiveProfitableDays,
                ConsecutiveProfitableWeeks = _consecutiveProfitableWeeks,
                MonthToDatePnL = _monthToDatePnL,
                MonthToDateReturn = _peakMonthValue > 0 ? _monthToDatePnL / _peakMonthValue : 0
            };

            // Calculate potential movements
            analysis.CanMoveUp = CanUpgrade();
            analysis.RiskOfDowngrade = CalculateDowngradeRisk();
            analysis.DaysUntilPossibleUpgrade = CalculateDaysUntilUpgrade();
            analysis.ProtectiveStopLevel = CalculateProtectiveStopLevel();

            // Performance metrics
            if (_tradingHistory.Count >= 30)
            {
                var last30Days = _tradingHistory.TakeLast(30).ToList();
                analysis.Last30DayWinRate = (decimal)last30Days.Count(d => d.PnL > 0) / 30;
                analysis.Last30DayAvgReturn = last30Days.Average(d => d.PnL / d.PortfolioValue);
                analysis.Last30DayMaxDrawdown = CalculateMaxDrawdown(last30Days);
            }

            return analysis;
        }

        public EmergencyProtocol CheckEmergencyConditions(decimal currentPortfolioValue)
        {
            var protocol = new EmergencyProtocol { IsTriggered = false };

            // Calculate various loss metrics
            var monthLoss = _peakMonthValue > 0 ? (_peakMonthValue - currentPortfolioValue) / _peakMonthValue : 0;
            var dayLoss = _tradingHistory.LastOrDefault()?.PnL ?? 0;
            var dayLossPercent = currentPortfolioValue > 0 ? dayLoss / currentPortfolioValue : 0;

            // Emergency triggers
            if (monthLoss >= _config.EmergencyMonthLossThreshold)
            {
                protocol.IsTriggered = true;
                protocol.Reason = $"Monthly loss {monthLoss:P2} exceeds emergency threshold {_config.EmergencyMonthLossThreshold:P2}";
                protocol.RecommendedAction = EmergencyAction.ImmediateStop;
                protocol.NewNotchLevel = SPX30DTENotchLevel.Survival;
            }
            else if (dayLossPercent <= -_config.EmergencyDayLossThreshold)
            {
                protocol.IsTriggered = true;
                protocol.Reason = $"Daily loss {dayLossPercent:P2} exceeds emergency threshold {_config.EmergencyDayLossThreshold:P2}";
                protocol.RecommendedAction = EmergencyAction.FreezeTradingToday;
                protocol.NewNotchLevel = (SPX30DTENotchLevel)Math.Max(0, _currentNotchIndex - 2);
            }
            else if (_consecutiveProfitableDays < -5) // 5+ consecutive losing days
            {
                protocol.IsTriggered = true;
                protocol.Reason = "5+ consecutive losing days detected";
                protocol.RecommendedAction = EmergencyAction.ReducePositionSizing;
                protocol.NewNotchLevel = (SPX30DTENotchLevel)Math.Max(0, _currentNotchIndex - 1);
            }

            return protocol;
        }

        private void CheckForNotchAdjustment(TradingDayResult dayResult)
        {
            var daysSinceLastChange = (DateTime.Now.Date - _lastNotchChange.Date).Days;

            // Upgrade conditions (more stringent for higher capital)
            if (CanUpgrade() && ShouldUpgrade(dayResult, daysSinceLastChange))
            {
                UpgradeNotch();
            }
            // Downgrade conditions (immediate for risk control)
            else if (ShouldDowngrade(dayResult))
            {
                DowngradeNotch(dayResult);
            }
        }

        private bool CanUpgrade()
        {
            return _currentNotchIndex < SPX_30DTE_NOTCH_LIMITS.Length - 1;
        }

        private bool ShouldUpgrade(TradingDayResult dayResult, int daysSinceLastChange)
        {
            // Minimum time requirements
            if (daysSinceLastChange < _config.MinDaysForUpgrade)
                return false;

            var currentLevel = (SPX30DTENotchLevel)_currentNotchIndex;

            switch (currentLevel)
            {
                case SPX30DTENotchLevel.Survival:
                case SPX30DTENotchLevel.Defensive:
                    // Need 10+ profitable days and positive month
                    return _consecutiveProfitableDays >= 10 && _monthToDatePnL > 0;

                case SPX30DTENotchLevel.Conservative:
                    // Need 15+ profitable days and +5% month
                    return _consecutiveProfitableDays >= 15 &&
                           _monthToDatePnL / _peakMonthValue >= 0.05m;

                case SPX30DTENotchLevel.Balanced:
                    // Need 20+ profitable days and +8% month
                    return _consecutiveProfitableDays >= 20 &&
                           _monthToDatePnL / _peakMonthValue >= 0.08m &&
                           _consecutiveProfitableWeeks >= 4;

                case SPX30DTENotchLevel.Growth:
                    // Need 25+ profitable days, +12% month, and 6+ profitable weeks
                    return _consecutiveProfitableDays >= 25 &&
                           _monthToDatePnL / _peakMonthValue >= 0.12m &&
                           _consecutiveProfitableWeeks >= 6;

                case SPX30DTENotchLevel.Aggressive:
                    // Need exceptional performance for maximum level
                    return _consecutiveProfitableDays >= 30 &&
                           _monthToDatePnL / _peakMonthValue >= 0.15m &&
                           _consecutiveProfitableWeeks >= 8 &&
                           GetLast90DayWinRate() >= 0.70m;

                default:
                    return false;
            }
        }

        private bool ShouldDowngrade(TradingDayResult dayResult)
        {
            var monthLossPercent = _peakMonthValue > 0 ?
                (_peakMonthValue - dayResult.PortfolioValue) / _peakMonthValue : 0;

            var dayLossPercent = dayResult.PortfolioValue > 0 ?
                dayResult.PnL / dayResult.PortfolioValue : 0;

            // Immediate downgrade triggers
            if (monthLossPercent >= _config.MonthlyLossDowngradeThreshold)
                return true;

            if (dayLossPercent <= -_config.DailyLossDowngradeThreshold)
                return true;

            // Consecutive loss trigger
            var recentLosses = _tradingHistory.TakeLast(5).Count(d => d.PnL < 0);
            if (recentLosses >= 4) // 4 out of last 5 days are losses
                return true;

            return false;
        }

        private void UpgradeNotch()
        {
            if (!CanUpgrade()) return;

            var oldLevel = (SPX30DTENotchLevel)_currentNotchIndex;
            _currentNotchIndex++;
            var newLevel = (SPX30DTENotchLevel)_currentNotchIndex;

            _lastNotchChange = DateTime.Now;

            LogNotchChange(oldLevel, newLevel, "UPGRADE", "Performance criteria met");
        }

        private void DowngradeNotch(TradingDayResult dayResult)
        {
            if (_currentNotchIndex <= 0) return;

            var oldLevel = (SPX30DTENotchLevel)_currentNotchIndex;

            // Determine downgrade severity
            var monthLossPercent = _peakMonthValue > 0 ?
                (_peakMonthValue - dayResult.PortfolioValue) / _peakMonthValue : 0;

            if (monthLossPercent >= 0.25m) // 25%+ loss - drop 2 levels
            {
                _currentNotchIndex = Math.Max(0, _currentNotchIndex - 2);
            }
            else if (monthLossPercent >= 0.15m) // 15%+ loss - drop 1 level
            {
                _currentNotchIndex = Math.Max(0, _currentNotchIndex - 1);
            }
            else // Standard downgrade
            {
                _currentNotchIndex = Math.Max(0, _currentNotchIndex - 1);
            }

            var newLevel = (SPX30DTENotchLevel)_currentNotchIndex;

            _lastNotchChange = DateTime.Now;
            _consecutiveProfitableDays = 0; // Reset on downgrade

            LogNotchChange(oldLevel, newLevel, "DOWNGRADE",
                $"Month loss: {monthLossPercent:P2}, Day PnL: {dayResult.PnL:C}");
        }

        private void ProcessMonthlyReset(DateTime newMonthStart)
        {
            // Store previous month's performance
            var previousMonth = newMonthStart.AddMonths(-1);
            var monthReturn = _peakMonthValue > 0 ? _monthToDatePnL / _peakMonthValue : 0;
            _monthlyReturns[previousMonth] = monthReturn;

            // Update weekly consecutive tracking
            if (_monthToDatePnL > 0)
            {
                _consecutiveProfitableWeeks++;
            }
            else
            {
                _consecutiveProfitableWeeks = 0;
            }

            // Reset monthly tracking
            _monthToDatePnL = 0;
            _peakMonthValue = GetCurrentNotchLimit();

            // Clean old history (keep 6 months)
            var cutoffDate = DateTime.Now.AddMonths(-6);
            _tradingHistory.RemoveAll(h => h.Date < cutoffDate);

            var oldMonths = _monthlyReturns.Keys.Where(k => k < cutoffDate).ToList();
            foreach (var oldMonth in oldMonths)
            {
                _monthlyReturns.Remove(oldMonth);
            }
        }

        private bool IsNewMonth(DateTime date)
        {
            return !_tradingHistory.Any() ||
                   _tradingHistory.Last().Date.Month != date.Month ||
                   _tradingHistory.Last().Date.Year != date.Year;
        }

        private decimal CalculateDowngradeRisk()
        {
            if (_monthToDatePnL >= 0) return 0;

            var currentLossPercent = _peakMonthValue > 0 ?
                Math.Abs(_monthToDatePnL) / _peakMonthValue : 0;

            var remainingBuffer = _config.MonthlyLossDowngradeThreshold - currentLossPercent;

            // Return risk as percentage (0-100)
            if (remainingBuffer <= 0) return 100m; // Already at downgrade threshold
            if (remainingBuffer >= 0.10m) return 0m; // Safe buffer

            return (1 - remainingBuffer / 0.10m) * 100m;
        }

        private int CalculateDaysUntilUpgrade()
        {
            var daysSinceLastChange = (DateTime.Now.Date - _lastNotchChange.Date).Days;
            var minDaysRequired = _config.MinDaysForUpgrade;

            return Math.Max(0, minDaysRequired - daysSinceLastChange);
        }

        private decimal CalculateProtectiveStopLevel()
        {
            var currentValue = _peakMonthValue + _monthToDatePnL;
            var stopLossPercent = _config.ProtectiveStopLossPercent;

            return currentValue * (1 - stopLossPercent);
        }

        private decimal CalculateMaxDrawdown(List<TradingDayResult> period)
        {
            if (period.Count < 2) return 0;

            var peak = period.First().PortfolioValue;
            var maxDrawdown = 0m;

            foreach (var day in period)
            {
                if (day.PortfolioValue > peak)
                    peak = day.PortfolioValue;

                var drawdown = (peak - day.PortfolioValue) / peak;
                if (drawdown > maxDrawdown)
                    maxDrawdown = drawdown;
            }

            return maxDrawdown;
        }

        private decimal GetLast90DayWinRate()
        {
            var last90Days = _tradingHistory.Where(d => d.Date >= DateTime.Now.AddDays(-90)).ToList();
            if (!last90Days.Any()) return 0.5m;

            return (decimal)last90Days.Count(d => d.PnL > 0) / last90Days.Count;
        }

        private void LogNotchChange(SPX30DTENotchLevel oldLevel, SPX30DTENotchLevel newLevel,
                                  string action, string reason)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] NOTCH {action}: " +
                             $"{oldLevel} ({SPX_30DTE_NOTCH_LIMITS[(int)oldLevel]:C}) → " +
                             $"{newLevel} ({SPX_30DTE_NOTCH_LIMITS[(int)newLevel]:C}) - {reason}");
        }

        private SPX30DTENotchConfig GetDefaultConfig()
        {
            return new SPX30DTENotchConfig
            {
                MinDaysForUpgrade = 14,                    // 2 weeks minimum between upgrades
                MonthlyLossDowngradeThreshold = 0.15m,     // 15% monthly loss triggers downgrade
                DailyLossDowngradeThreshold = 0.05m,       // 5% daily loss triggers downgrade
                EmergencyMonthLossThreshold = 0.30m,       // 30% monthly loss = emergency stop
                EmergencyDayLossThreshold = 0.10m,         // 10% daily loss = emergency protocols
                ProtectiveStopLossPercent = 0.08m,         // 8% protective stop from peak
                MinConsecutiveDaysForUpgrade = 10,         // Minimum profitable days for upgrade
                MaxConsecutiveLossesAllowed = 5            // Max consecutive losses before action
            };
        }

        // Public method to force emergency downgrade
        public void TriggerEmergencyDowngrade(string reason)
        {
            var oldLevel = (SPX30DTENotchLevel)_currentNotchIndex;
            _currentNotchIndex = Math.Max(0, _currentNotchIndex - 2); // Drop 2 levels
            var newLevel = (SPX30DTENotchLevel)_currentNotchIndex;

            _lastNotchChange = DateTime.Now;
            _consecutiveProfitableDays = 0;

            LogNotchChange(oldLevel, newLevel, "EMERGENCY_DOWNGRADE", reason);
        }

        // Method to get position sizing recommendation
        public PositionSizingRecommendation GetPositionSizingRecommendation(decimal tradeRisk)
        {
            var currentLimit = GetCurrentNotchLimit();
            var recommendedPositions = Math.Floor(currentLimit / tradeRisk);

            var recommendation = new PositionSizingRecommendation
            {
                MaxPositions = (int)recommendedPositions,
                CapitalAllocated = currentLimit,
                RiskPerPosition = tradeRisk,
                NotchLevel = GetCurrentNotchLevel(),
                ConfidenceLevel = CalculateConfidenceLevel()
            };

            // Apply safety restrictions
            var analysis = GetNotchAnalysis();
            if (analysis.RiskOfDowngrade > 50m)
            {
                recommendation.MaxPositions = Math.Max(1, recommendation.MaxPositions / 2);
                recommendation.Warning = "High downgrade risk - reducing position sizing";
            }

            if (_consecutiveProfitableDays < 0) // In losing streak
            {
                recommendation.MaxPositions = Math.Max(1, recommendation.MaxPositions * 2 / 3);
                recommendation.Warning = "Currently in losing streak - conservative sizing";
            }

            return recommendation;
        }

        private decimal CalculateConfidenceLevel()
        {
            var factors = new List<decimal>();

            // Win rate factor
            if (_tradingHistory.Count >= 20)
            {
                var recentWinRate = (decimal)_tradingHistory.TakeLast(20).Count(d => d.PnL > 0) / 20;
                factors.Add(recentWinRate * 100);
            }

            // Consecutive performance factor
            if (_consecutiveProfitableDays > 10)
                factors.Add(Math.Min(100, _consecutiveProfitableDays * 3));
            else if (_consecutiveProfitableDays < -3)
                factors.Add(Math.Max(0, 50 + _consecutiveProfitableDays * 10));
            else
                factors.Add(60);

            // Monthly performance factor
            var monthReturnPercent = _peakMonthValue > 0 ? _monthToDatePnL / _peakMonthValue : 0;
            if (monthReturnPercent > 0.05m) factors.Add(80);
            else if (monthReturnPercent > 0) factors.Add(60);
            else if (monthReturnPercent > -0.05m) factors.Add(40);
            else factors.Add(20);

            return factors.Any() ? factors.Average() : 50m;
        }
    }

    // Supporting classes and enums
    public enum SPX30DTENotchLevel
    {
        Survival = 0,      // $400 - Emergency mode
        Defensive = 1,     // $800 - Major losses
        Conservative = 2,  // $1200 - Mild losses
        Balanced = 3,      // $2000 - Starting/target level
        Growth = 4,        // $3200 - Good performance
        Aggressive = 5,    // $5000 - Strong performance
        Maximum = 6        // $8000 - Exceptional performance
    }

    public class SPX30DTENotchConfig
    {
        public int MinDaysForUpgrade { get; set; }
        public decimal MonthlyLossDowngradeThreshold { get; set; }
        public decimal DailyLossDowngradeThreshold { get; set; }
        public decimal EmergencyMonthLossThreshold { get; set; }
        public decimal EmergencyDayLossThreshold { get; set; }
        public decimal ProtectiveStopLossPercent { get; set; }
        public int MinConsecutiveDaysForUpgrade { get; set; }
        public int MaxConsecutiveLossesAllowed { get; set; }
    }

    public class TradingDayResult
    {
        public DateTime Date { get; set; }
        public decimal PnL { get; set; }
        public decimal PortfolioValue { get; set; }
        public decimal NotchLimitAtTime { get; set; }
        public SPX30DTENotchLevel NotchLevel { get; set; }
    }

    public class NotchAnalysis
    {
        public SPX30DTENotchLevel CurrentLevel { get; set; }
        public decimal CurrentLimit { get; set; }
        public int DaysAtCurrentLevel { get; set; }
        public int ConsecutiveProfitableDays { get; set; }
        public int ConsecutiveProfitableWeeks { get; set; }
        public decimal MonthToDatePnL { get; set; }
        public decimal MonthToDateReturn { get; set; }
        public bool CanMoveUp { get; set; }
        public decimal RiskOfDowngrade { get; set; }
        public int DaysUntilPossibleUpgrade { get; set; }
        public decimal ProtectiveStopLevel { get; set; }
        public decimal Last30DayWinRate { get; set; }
        public decimal Last30DayAvgReturn { get; set; }
        public decimal Last30DayMaxDrawdown { get; set; }
    }

    public class EmergencyProtocol
    {
        public bool IsTriggered { get; set; }
        public string Reason { get; set; }
        public EmergencyAction RecommendedAction { get; set; }
        public SPX30DTENotchLevel NewNotchLevel { get; set; }
    }

    public enum EmergencyAction
    {
        None,
        ReducePositionSizing,
        FreezeTradingToday,
        ImmediateStop
    }

    public class PositionSizingRecommendation
    {
        public int MaxPositions { get; set; }
        public decimal CapitalAllocated { get; set; }
        public decimal RiskPerPosition { get; set; }
        public SPX30DTENotchLevel NotchLevel { get; set; }
        public decimal ConfidenceLevel { get; set; }
        public string Warning { get; set; }
    }
}