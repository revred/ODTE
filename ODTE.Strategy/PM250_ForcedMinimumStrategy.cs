using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ODTE.Strategy
{
    /// <summary>
    /// PM250 with Forced Minimum Trading Strategy
    /// 
    /// CRITICAL REQUIREMENTS ADDRESSED:
    /// 1. Force minimum 1 trade per hour when Reverse Fibonacci not naturally triggered
    /// 2. Strict loss and drawdown controls even for forced trades
    /// 3. Clear assessment of unfavorable conditions and suboptimal GoScores
    /// 4. Comprehensive data collection for all market scenarios
    /// 
    /// PURPOSE: Understand strategy performance across ALL market conditions,
    /// not just optimal ones, to build robust models that work in any environment.
    /// </summary>
    public class PM250_ForcedMinimumStrategy
    {
        private readonly ReverseFibonacciRiskManager _riskManager;
        private readonly List<TradeExecution> _recentTrades;
        private readonly Dictionary<DateTime, int> _hourlyTradeCount;
        private readonly Random _random;

        // Strategy properties
        public string Name => "PM250 Forced Minimum Trading";
        public double ExpectedEdge => 0.75; // Lower due to forced suboptimal trades
        public double ExpectedWinRate => 0.70; // More conservative with forced trades

        // Core thresholds
        private const int MAX_TRADES_PER_WEEK = 250;
        private const int MAX_TRADES_PER_DAY = 50;
        private const int MIN_SEPARATION_MINUTES = 6;
        
        // Forced trading parameters
        private const int FORCED_TRADE_HOUR_THRESHOLD = 60; // Minutes without trade = force one
        private const double FORCED_TRADE_GOSCORE_MINIMUM = 25.0; // Even lower for forced trades
        private const decimal FORCED_TRADE_MAX_RISK = 8.0m; // Strict risk limit for forced trades
        private const decimal FORCED_TRADE_TARGET_CREDIT = 0.08m; // Lower target for bad conditions
        
        // Absolute risk limits (NEVER exceed these)
        private const decimal ABSOLUTE_MAX_SINGLE_LOSS = 12.0m;
        private const decimal ABSOLUTE_MAX_DAILY_DRAWDOWN = 60.0m;
        private const decimal ABSOLUTE_MAX_HOURLY_LOSS = 20.0m;

        public PM250_ForcedMinimumStrategy()
        {
            _riskManager = new ReverseFibonacciRiskManager();
            _recentTrades = new List<TradeExecution>();
            _hourlyTradeCount = new Dictionary<DateTime, int>();
            _random = new Random();
        }

        public async Task<StrategyResult> ExecuteAsync(StrategyParameters parameters, MarketConditions conditions)
        {
            try
            {
                var currentHour = new DateTime(conditions.Date.Year, conditions.Date.Month, conditions.Date.Day, conditions.Date.Hour, 0, 0);
                
                // Step 1: Check if we need forced trading due to inactivity
                var needsForcedTrade = RequiresForcedTrade(currentHour);
                var isOptimalConditions = !needsForcedTrade;

                // Step 2: Basic validation (still applies to forced trades)
                if (!IsValidTradeOpportunity(conditions, needsForcedTrade))
                    return CreateBlockedResult("Trade limits or timing restrictions");

                // Step 3: Risk assessment with forced trade considerations
                var riskAssessment = await AssessRiskWithForcedTrading(conditions, needsForcedTrade);
                if (!riskAssessment.IsAcceptable)
                    return CreateBlockedResult($"Risk block: {riskAssessment.Reason}");

                // Step 4: GoScore calculation with forced trade adjustments
                var goScore = CalculateAdjustedGoScore(conditions);
                var minimumScore = needsForcedTrade ? FORCED_TRADE_GOSCORE_MINIMUM : 50.0;
                
                if (goScore < minimumScore && !needsForcedTrade)
                    return CreateBlockedResult($"GoScore {goScore:F1} below threshold {minimumScore}");

                // Step 5: Execute trade with appropriate risk sizing
                var adjustedSize = _riskManager.CalculatePositionSize(parameters.PositionSize, _recentTrades);
                var result = await ExecuteTradeWithForcedLogic(conditions, goScore, adjustedSize, needsForcedTrade);

                // Step 6: Record trade and update hourly tracking
                RecordTradeExecution(result, conditions, needsForcedTrade);
                UpdateHourlyTradeTracking(currentHour);

                return result;
            }
            catch (Exception ex)
            {
                return CreateErrorResult($"PM250 Forced strategy error: {ex.Message}");
            }
        }

        private bool RequiresForcedTrade(DateTime currentHour)
        {
            // Check if we've had any trades in the last hour
            var hourAgo = currentHour.AddHours(-1);
            var recentTrades = _recentTrades.Where(t => t.ExecutionTime >= hourAgo).ToList();
            
            // Check if current hour has any trades
            var currentHourTrades = _hourlyTradeCount.GetValueOrDefault(currentHour, 0);
            
            // Force trade if:
            // 1. No trades in last hour AND
            // 2. No trades in current hour AND  
            // 3. We're in trading hours
            var inTradingHours = currentHour.Hour >= 9 && currentHour.Hour <= 15;
            
            return inTradingHours && recentTrades.Count == 0 && currentHourTrades == 0;
        }

        private bool IsValidTradeOpportunity(MarketConditions conditions, bool isForcedTrade)
        {
            // Daily/weekly limits (apply even to forced trades)
            var todaysTrades = _recentTrades.Count(t => t.ExecutionTime.Date == conditions.Date.Date);
            if (todaysTrades >= MAX_TRADES_PER_DAY) return false;

            var weekStart = conditions.Date.AddDays(-(int)conditions.Date.DayOfWeek);
            var weekTrades = _recentTrades.Count(t => t.ExecutionTime >= weekStart);
            if (weekTrades >= MAX_TRADES_PER_WEEK) return false;

            // Separation check (relaxed for forced trades)
            var lastTrade = _recentTrades.LastOrDefault();
            if (lastTrade != null)
            {
                var timeSince = conditions.Date - lastTrade.ExecutionTime;
                var minSeparation = isForcedTrade ? 3 : MIN_SEPARATION_MINUTES; // Shorter for forced
                if (timeSince.TotalMinutes < minSeparation) return false;
            }

            // Trading hours
            var hour = conditions.Date.Hour;
            return hour >= 9 && hour <= 15;
        }

        private async Task<RiskAssessment> AssessRiskWithForcedTrading(MarketConditions conditions, bool isForcedTrade)
        {
            var assessment = new RiskAssessment { IsAcceptable = true };

            // ABSOLUTE risk limits (never violated, even for forced trades)
            
            // 1. Daily drawdown check
            var todaysLoss = _recentTrades
                .Where(t => t.ExecutionTime.Date == conditions.Date.Date && t.PnL < 0)
                .Sum(t => t.PnL);
            
            if (Math.Abs(todaysLoss) > ABSOLUTE_MAX_DAILY_DRAWDOWN)
            {
                assessment.IsAcceptable = false;
                assessment.Reason = $"Absolute daily limit: ${Math.Abs(todaysLoss):F2}";
                return assessment;
            }

            // 2. Hourly loss check (prevents rapid deterioration)
            var currentHour = new DateTime(conditions.Date.Year, conditions.Date.Month, conditions.Date.Day, conditions.Date.Hour, 0, 0);
            var hourlyLoss = _recentTrades
                .Where(t => t.ExecutionTime >= currentHour && t.PnL < 0)
                .Sum(t => t.PnL);
                
            if (Math.Abs(hourlyLoss) > ABSOLUTE_MAX_HOURLY_LOSS)
            {
                assessment.IsAcceptable = false;
                assessment.Reason = $"Hourly loss limit: ${Math.Abs(hourlyLoss):F2}";
                return assessment;
            }

            // 3. Extreme market conditions (even forced trades have limits)
            if (conditions.VIX > 80) // Extreme panic
            {
                assessment.IsAcceptable = false;
                assessment.Reason = $"Extreme panic conditions: VIX {conditions.VIX:F1}";
                return assessment;
            }

            // 4. Consecutive loss protection (more lenient for forced trades)
            var consecutiveLosses = _recentTrades.TakeLast(10).Count(t => t.PnL < 0);
            var maxConsecutive = isForcedTrade ? 8 : 5; // Allow more for forced trades
            
            if (consecutiveLosses >= maxConsecutive)
            {
                assessment.IsAcceptable = false;
                assessment.Reason = $"Consecutive losses: {consecutiveLosses}/{maxConsecutive}";
                return assessment;
            }

            return assessment;
        }

        private double CalculateAdjustedGoScore(MarketConditions conditions)
        {
            var baseScore = 40.0; // Lower base for comprehensive testing

            // VIX contribution (accept wider ranges)
            var vixScore = conditions.VIX >= 8 && conditions.VIX <= 50 ? 70.0 : 
                          conditions.VIX <= 70 ? 60.0 : 45.0;
            baseScore += (vixScore - 40) * 0.25;

            // Market regime (accept all regimes)
            var regimeScore = conditions.MarketRegime switch
            {
                "Calm" => 80.0,
                "Mixed" => 70.0,
                "Volatile" => 60.0,
                "Crisis" => 45.0,
                _ => 40.0
            };
            baseScore += (regimeScore - 40) * 0.20;

            // Trend acceptance (test all trend conditions)
            var trendScore = Math.Abs(conditions.TrendScore) < 0.7 ? 75.0 :
                           Math.Abs(conditions.TrendScore) < 1.2 ? 65.0 : 55.0;
            baseScore += (trendScore - 40) * 0.15;

            return Math.Max(0, Math.Min(100, baseScore));
        }

        private async Task<StrategyResult> ExecuteTradeWithForcedLogic(MarketConditions conditions, double goScore, decimal positionSize, bool isForcedTrade)
        {
            // Adjust trade parameters based on whether this is forced
            decimal baseCredit, maxRisk;
            string tradeReason;
            
            if (isForcedTrade)
            {
                // Forced trade: smaller size, lower targets, strict risk
                baseCredit = FORCED_TRADE_TARGET_CREDIT;
                maxRisk = Math.Min(FORCED_TRADE_MAX_RISK, ABSOLUTE_MAX_SINGLE_LOSS);
                tradeReason = "FORCED_MINIMUM";
                positionSize *= 0.5m; // Half size for forced trades
            }
            else
            {
                // Regular trade: normal parameters
                baseCredit = 0.15m;
                maxRisk = Math.Min(18.0m, ABSOLUTE_MAX_SINGLE_LOSS);
                tradeReason = "OPTIMAL";
            }

            // GoScore adjustment (smaller impact for forced trades)
            var goScoreMultiplier = 1.0m + ((decimal)goScore - 50m) / 300m; // Smaller adjustment
            var creditReceived = baseCredit * goScoreMultiplier * positionSize;
            var adjustedMaxRisk = maxRisk * positionSize;
            
            // Win probability calculation (lower for forced trades)
            var baseProbability = isForcedTrade ? 0.65 : 0.78; // Lower win rate for forced
            var winProbability = CalculateWinProbability(conditions, goScore, baseProbability);
            var isWin = _random.NextDouble() < winProbability;
            
            decimal pnl;
            if (isWin)
            {
                // Win: keep percentage of credit
                var keepPercentage = isForcedTrade ? 0.60m : 0.75m; // Keep less on forced trades
                pnl = creditReceived * keepPercentage;
            }
            else
            {
                // Loss: capped at risk limit
                pnl = -Math.Min(adjustedMaxRisk, ABSOLUTE_MAX_SINGLE_LOSS);
            }

            return new StrategyResult
            {
                StrategyName = Name,
                PnL = pnl,
                CreditReceived = creditReceived,
                IsWin = isWin,
                Metadata = new Dictionary<string, object>
                {
                    ["GoScore"] = goScore,
                    ["WinProbability"] = winProbability,
                    ["PositionSize"] = positionSize,
                    ["MarketRegime"] = conditions.MarketRegime,
                    ["VIX"] = conditions.VIX,
                    ["TrendScore"] = conditions.TrendScore,
                    ["TradeReason"] = tradeReason,
                    ["IsForcedTrade"] = isForcedTrade,
                    ["MaxRisk"] = adjustedMaxRisk,
                    ["ExecutionTime"] = conditions.Date.ToString("yyyy-MM-dd HH:mm")
                }
            };
        }

        private double CalculateWinProbability(MarketConditions conditions, double goScore, double baseProbability)
        {
            // GoScore adjustment
            var goScoreAdjustment = (goScore - 50.0) / 400.0; // Smaller impact
            
            // VIX adjustment
            var vixAdjustment = conditions.VIX < 15 ? 0.03 :
                               conditions.VIX < 25 ? 0.02 :
                               conditions.VIX < 35 ? -0.02 :
                               conditions.VIX < 50 ? -0.05 :
                               -0.12; // Harsh penalty for extreme VIX

            // Regime adjustment  
            var regimeAdjustment = conditions.MarketRegime switch
            {
                "Calm" => 0.05,
                "Mixed" => 0.01,
                "Volatile" => -0.03,
                "Crisis" => -0.08,
                _ => -0.10
            };

            // Trend adjustment
            var trendPenalty = Math.Abs(conditions.TrendScore) > 1.0 ? -0.04 : 0.0;

            var finalProbability = baseProbability + goScoreAdjustment + vixAdjustment + regimeAdjustment + trendPenalty;
            return Math.Max(0.50, Math.Min(0.92, finalProbability)); // Clamp between 50%-92%
        }

        private void RecordTradeExecution(StrategyResult result, MarketConditions conditions, bool isForcedTrade)
        {
            _recentTrades.Add(new TradeExecution
            {
                ExecutionTime = conditions.Date,
                PnL = result.PnL,
                Success = result.IsWin,
                Strategy = result.StrategyName + (isForcedTrade ? "_FORCED" : "_OPTIMAL")
            });

            // Keep only recent trades (last 200 for better analysis)
            if (_recentTrades.Count > 200)
            {
                _recentTrades.RemoveRange(0, _recentTrades.Count - 200);
            }
        }

        private void UpdateHourlyTradeTracking(DateTime currentHour)
        {
            _hourlyTradeCount[currentHour] = _hourlyTradeCount.GetValueOrDefault(currentHour, 0) + 1;
            
            // Clean old hourly data (keep last 48 hours)
            var cutoff = currentHour.AddHours(-48);
            var keysToRemove = _hourlyTradeCount.Keys.Where(k => k < cutoff).ToList();
            foreach (var key in keysToRemove)
            {
                _hourlyTradeCount.Remove(key);
            }
        }

        private StrategyResult CreateBlockedResult(string reason)
        {
            return new StrategyResult
            {
                StrategyName = Name,
                PnL = 0,
                CreditReceived = 0,
                IsWin = false,
                Metadata = new Dictionary<string, object> 
                { 
                    ["BlockReason"] = reason,
                    ["IsForcedTrade"] = false
                }
            };
        }

        private StrategyResult CreateErrorResult(string error)
        {
            return new StrategyResult
            {
                StrategyName = Name,
                PnL = 0,
                CreditReceived = 0,
                IsWin = false,
                Metadata = new Dictionary<string, object> 
                { 
                    ["Error"] = error,
                    ["IsForcedTrade"] = false
                }
            };
        }
    }
}