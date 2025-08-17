using ODTE.Strategy.Models;

namespace ODTE.Strategy
{
    /// <summary>
    /// Reverse Fibonacci Risk Manager that enforces daily loss limits with MPL integration
    /// Implements the adaptive position sizing based on consecutive loss days
    /// </summary>
    public class RFibRiskManager
    {
        private readonly decimal[] _dailyLimits = { 500m, 300m, 200m, 100m };
        private int _consecutiveLossDays;
        private decimal _currentDayRiskUsed;
        private decimal _currentDayPnL;
        private DateTime _currentTradingDay;
        private readonly List<RFibDayRecord> _dayHistory;

        public RFibRiskManager()
        {
            _consecutiveLossDays = 0;
            _currentDayRiskUsed = 0;
            _currentDayPnL = 0;
            _currentTradingDay = DateTime.Today;
            _dayHistory = new List<RFibDayRecord>();
        }

        /// <summary>
        /// Get current daily loss limit based on consecutive loss days
        /// </summary>
        public decimal CurrentDailyLimit => _dailyLimits[Math.Min(_consecutiveLossDays, _dailyLimits.Length - 1)];

        /// <summary>
        /// Get remaining risk capacity for the current day
        /// </summary>
        public decimal RemainingCapacity => Math.Max(0, CurrentDailyLimit - _currentDayRiskUsed);

        /// <summary>
        /// Validate if a candidate order is allowed under RFib rules
        /// </summary>
        public RFibValidationResult ValidateOrder(CandidateOrder candidate)
        {
            var currentLimit = CurrentDailyLimit;
            var wouldExceedLimit = (_currentDayRiskUsed + candidate.MaxPotentialLoss) > currentLimit;

            if (wouldExceedLimit)
            {
                return new RFibValidationResult
                {
                    IsAllowed = false,
                    Reason = $"Order MPL ${candidate.MaxPotentialLoss} would exceed daily limit ${currentLimit} (used: ${_currentDayRiskUsed})",
                    DailyLimit = currentLimit,
                    CurrentUsage = _currentDayRiskUsed,
                    RemainingCapacity = RemainingCapacity,
                    UtilizationAfterOrder = (_currentDayRiskUsed + candidate.MaxPotentialLoss) / currentLimit
                };
            }

            // Calculate post-order utilization
            var utilizationAfterOrder = (_currentDayRiskUsed + candidate.MaxPotentialLoss) / currentLimit;

            // Warning at 90% utilization
            var warningLevel = utilizationAfterOrder >= 0.90m;

            return new RFibValidationResult
            {
                IsAllowed = true,
                Reason = warningLevel ? "Order allowed but approaching daily limit" : "Order within daily limits",
                DailyLimit = currentLimit,
                CurrentUsage = _currentDayRiskUsed,
                RemainingCapacity = RemainingCapacity - candidate.MaxPotentialLoss,
                UtilizationAfterOrder = utilizationAfterOrder,
                WarningLevel = warningLevel
            };
        }

        /// <summary>
        /// Calculate maximum position size for a given strategy MPL
        /// </summary>
        public int CalculateMaxPositionSize(decimal mplPerContract)
        {
            if (mplPerContract <= 0) return 0;

            var remainingCapacity = RemainingCapacity;
            return (int)Math.Floor(remainingCapacity / mplPerContract);
        }

        /// <summary>
        /// Record a strategy execution and update risk usage
        /// </summary>
        public void RecordExecution(StrategyResult result)
        {
            EnsureCurrentDay();

            // Add MPL to risk usage when opening position
            if (result.ExitReason != "Day end" && result.ExitReason != "Stop loss")
            {
                _currentDayRiskUsed += result.MaxPotentialLoss;
            }

            // Track daily P&L
            _currentDayPnL += result.PnL;

            // Update utilization in result
            result.RfibUtilization = _currentDayRiskUsed / CurrentDailyLimit;
        }

        /// <summary>
        /// Start a new trading day and update consecutive loss tracking
        /// </summary>
        public void StartNewTradingDay(DateTime tradingDay)
        {
            // Record previous day if it had activity AND we're actually changing days
            if ((_currentDayPnL != 0 || _currentDayRiskUsed > 0) && tradingDay.Date != _currentTradingDay.Date)
            {
                var dayRecord = new RFibDayRecord
                {
                    Date = _currentTradingDay,
                    PnL = _currentDayPnL,
                    RiskUsed = _currentDayRiskUsed,
                    DailyLimit = CurrentDailyLimit,
                    ConsecutiveLossDays = _consecutiveLossDays
                };
                _dayHistory.Add(dayRecord);

                // Update consecutive loss tracking
                if (_currentDayPnL > 0)
                {
                    _consecutiveLossDays = 0; // Reset on profitable day
                }
                else if (_currentDayPnL < 0)
                {
                    _consecutiveLossDays++;
                }
            }

            // Reset for new day only if actually changing days
            if (tradingDay.Date != _currentTradingDay.Date)
            {
                _currentTradingDay = tradingDay;
                _currentDayRiskUsed = 0;
                _currentDayPnL = 0;
            }
        }

        /// <summary>
        /// Force reset consecutive loss days (for testing or emergency reset)
        /// </summary>
        public void ResetConsecutiveLosses()
        {
            _consecutiveLossDays = 0;
        }

        /// <summary>
        /// Get current risk manager status
        /// </summary>
        public RFibStatus GetStatus()
        {
            return new RFibStatus
            {
                CurrentDay = _currentTradingDay,
                ConsecutiveLossDays = _consecutiveLossDays,
                DailyLimit = CurrentDailyLimit,
                RiskUsed = _currentDayRiskUsed,
                RemainingCapacity = RemainingCapacity,
                CurrentUtilization = _currentDayRiskUsed / CurrentDailyLimit,
                DayPnL = _currentDayPnL,
                TotalDaysTracked = _dayHistory.Count
            };
        }

        /// <summary>
        /// Get historical day records
        /// </summary>
        public IReadOnlyList<RFibDayRecord> GetDayHistory() => _dayHistory.AsReadOnly();

        private void EnsureCurrentDay()
        {
            var today = DateTime.Today;
            if (_currentTradingDay.Date != today)
            {
                StartNewTradingDay(today);
            }
        }
    }

    /// <summary>
    /// Result of RFib validation for a candidate order
    /// </summary>
    public class RFibValidationResult
    {
        public bool IsAllowed { get; set; }
        public string Reason { get; set; } = "";
        public decimal DailyLimit { get; set; }
        public decimal CurrentUsage { get; set; }
        public decimal RemainingCapacity { get; set; }
        public decimal UtilizationAfterOrder { get; set; }
        public bool WarningLevel { get; set; }
    }

    /// <summary>
    /// Current status of the RFib risk manager
    /// </summary>
    public class RFibStatus
    {
        public DateTime CurrentDay { get; set; }
        public int ConsecutiveLossDays { get; set; }
        public decimal DailyLimit { get; set; }
        public decimal RiskUsed { get; set; }
        public decimal RemainingCapacity { get; set; }
        public decimal CurrentUtilization { get; set; }
        public decimal DayPnL { get; set; }
        public int TotalDaysTracked { get; set; }
    }

    /// <summary>
    /// Historical record of a trading day under RFib management
    /// </summary>
    public class RFibDayRecord
    {
        public DateTime Date { get; set; }
        public decimal PnL { get; set; }
        public decimal RiskUsed { get; set; }
        public decimal DailyLimit { get; set; }
        public int ConsecutiveLossDays { get; set; }
        public decimal Utilization => RiskUsed / DailyLimit;
    }
}