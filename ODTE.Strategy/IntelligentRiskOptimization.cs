using ODTE.Strategy.GoScore;

namespace ODTE.Strategy
{
    /// <summary>
    /// Intelligent Risk Optimization - PROFITABLE capital preservation
    /// 
    /// REAL DATA ANALYSIS INSIGHTS:
    /// - Current: $430.52 profit (284 trades) = $1.52 avg per trade
    /// - Target: $10-50 per trade (6-33x improvement needed)
    /// - Problem: Max drawdown $59.86, max loss $26.37
    /// - Solution: Better trade selection + smarter sizing, NOT blocking everything
    /// 
    /// STRATEGY: Use GoScore for quality filtering + intelligent position sizing
    /// </summary>
    public class IntelligentRiskOptimization
    {
        private readonly GoScorer _goScorer;
        private readonly List<decimal> _recentPerformance;
        private readonly Dictionary<string, decimal> _dailyPnL;
        private int _consecutiveLossingDays;

        // INTELLIGENT PARAMETERS (not overly conservative)
        private readonly decimal _targetProfitPerTrade = 15.0m; // Target $15 per trade (10x current)
        private readonly decimal _maxSingleLoss = 18.0m; // Reduce from $26.37 but allow reasonable losses
        private readonly decimal _maxDailyDrawdown = 35.0m; // Reduce from $59.86 but allow trading
        private readonly double _minGoScoreThreshold = 60.0; // Use GoScore for quality filtering

        public IntelligentRiskOptimization()
        {
            var policy = LoadOptimizedGoScorePolicy();
            _goScorer = new GoScorer(policy);
            _recentPerformance = new List<decimal>();
            _dailyPnL = new Dictionary<string, decimal>();
            _consecutiveLossingDays = 0;
        }

        /// <summary>
        /// Intelligent trade evaluation - use GoScore for quality, sizing for risk management
        /// </summary>
        public TradeDecision EvaluateTrade(MarketConditions conditions, StrategyParameters parameters)
        {
            // Step 1: Use GoScore for trade quality assessment
            var goInputs = ConvertToGoInputs(conditions, parameters);
            var strategyKind = SelectStrategyKind(conditions);
            var regime = ClassifyRegime(conditions);

            var goScoreBreakdown = _goScorer.GetBreakdown(goInputs, strategyKind, regime);

            // Step 2: Quality-based filtering (not conservative blocking)
            if (goScoreBreakdown.FinalScore < _minGoScoreThreshold)
            {
                return new TradeDecision
                {
                    ShouldTrade = false,
                    Reason = $"GoScore {goScoreBreakdown.FinalScore:F1} below threshold {_minGoScoreThreshold}",
                    RecommendedSize = 0
                };
            }

            // Step 3: Intelligent position sizing (profit-focused)
            var baseSize = CalculateIntelligentPositionSize(conditions, goScoreBreakdown.FinalScore);
            var riskAdjustedSize = ApplyRiskManagement(baseSize, conditions);

            // Step 4: Profitability enhancement
            var enhancedSize = ApplyProfitabilityEnhancement(riskAdjustedSize, goScoreBreakdown.FinalScore);

            return new TradeDecision
            {
                ShouldTrade = true,
                RecommendedSize = enhancedSize,
                GoScore = goScoreBreakdown.FinalScore,
                ExpectedProfit = enhancedSize * _targetProfitPerTrade,
                MaxRisk = Math.Min(enhancedSize * _maxSingleLoss, _maxDailyDrawdown),
                Reason = $"High quality trade (GoScore: {goScoreBreakdown.FinalScore:F1})"
            };
        }

        /// <summary>
        /// Calculate position size focused on profit enhancement, not conservative blocking
        /// </summary>
        private decimal CalculateIntelligentPositionSize(MarketConditions conditions, double goScore)
        {
            // Base size starts at 1.0, enhanced based on opportunity quality
            var baseSize = 1.0m;

            // GoScore enhancement - reward high quality setups
            var qualityMultiplier = goScore > 80 ? 2.0m :  // Excellent setups - double size
                                  goScore > 70 ? 1.5m :  // Good setups - 50% more
                                  1.0m;                  // Acceptable setups - normal

            // Market condition enhancement - take advantage of good conditions
            var marketMultiplier = 1.0m;

            // Low volatility + calm conditions = bigger size (higher win probability)
            if (conditions.VIX < 20 && Math.Abs(conditions.TrendScore) < 0.3)
                marketMultiplier = 1.3m;

            // High IV rank = bigger size (better premium collection)
            if (GetIVRank(conditions) > 70)
                marketMultiplier *= 1.2m;

            var intelligentSize = baseSize * qualityMultiplier * marketMultiplier;

            // Cap at reasonable maximum (not overly conservative)
            return Math.Min(intelligentSize, 3.0m);
        }

        /// <summary>
        /// Apply smart risk management without destroying profitability
        /// </summary>
        private decimal ApplyRiskManagement(decimal baseSize, MarketConditions conditions)
        {
            var adjustedSize = baseSize;

            // Recent performance adjustment (not Fibonacci destruction)
            if (_consecutiveLossingDays > 0)
            {
                var reductionFactor = _consecutiveLossingDays switch
                {
                    1 => 0.85m,  // 15% reduction after 1 loss day
                    2 => 0.70m,  // 30% reduction after 2 loss days  
                    3 => 0.55m,  // 45% reduction after 3 loss days
                    _ => 0.40m   // 60% reduction for 4+ (still allows trading)
                };
                adjustedSize *= reductionFactor;
            }

            // Volatility adjustment (smart, not fearful)
            if (conditions.VIX > 35)
                adjustedSize *= 0.7m;  // Reduce size in high vol, don't stop trading
            else if (conditions.VIX < 15)
                adjustedSize *= 1.2m;  // Increase size in low vol opportunity

            // Trend strength adjustment
            if (Math.Abs(conditions.TrendScore) > 0.8)
                adjustedSize *= 0.8m;  // Reduce in strong trends, don't block

            return Math.Max(adjustedSize, 0.3m); // Minimum viable size, not zero
        }

        /// <summary>
        /// Enhance position sizing for profitability (the real goal)
        /// </summary>
        private decimal ApplyProfitabilityEnhancement(decimal riskAdjustedSize, double goScore)
        {
            var enhancedSize = riskAdjustedSize;

            // Profit momentum enhancement - scale up when doing well
            if (_recentPerformance.Count >= 5)
            {
                var recentAvg = _recentPerformance.TakeLast(5).Average();
                if (recentAvg > 10m) // If recent performance is strong
                    enhancedSize *= 1.3m; // Scale up to capture more profit
            }

            // Exceptional opportunity enhancement
            if (goScore > 85) // Truly exceptional setups
                enhancedSize *= 1.4m; // Aggressive sizing for best opportunities

            // Market regime enhancement
            var todaysPnL = GetTodaysPnL();
            if (todaysPnL > 0 && todaysPnL < _maxDailyDrawdown * 0.5m) // Room to grow
                enhancedSize *= 1.1m;

            return enhancedSize;
        }

        /// <summary>
        /// Smart trade blocking - only block genuinely bad situations
        /// </summary>
        public bool ShouldBlockTrade(decimal proposedRisk, MarketConditions conditions)
        {
            // Block only truly dangerous situations

            // Single trade risk too high
            if (proposedRisk > _maxSingleLoss)
                return true;

            // Daily P&L already at limit
            var todaysPnL = GetTodaysPnL();
            if (todaysPnL < -_maxDailyDrawdown)
                return true;

            // Extreme market conditions only
            if (conditions.VIX > 60) // Only block in truly extreme vol
                return true;

            if (Math.Abs(conditions.TrendScore) > 1.5) // Only block in impossible trends
                return true;

            // Otherwise, trade with appropriate sizing
            return false;
        }

        public void RecordTradeResult(decimal pnl, DateTime tradeDate)
        {
            _recentPerformance.Add(pnl);
            if (_recentPerformance.Count > 20)
                _recentPerformance.RemoveAt(0); // Keep last 20 trades

            var dateKey = tradeDate.ToString("yyyy-MM-dd");
            _dailyPnL[dateKey] = _dailyPnL.GetValueOrDefault(dateKey, 0m) + pnl;

            // Update consecutive losing days
            if (_dailyPnL[dateKey] < 0)
                _consecutiveLossingDays++;
            else if (_dailyPnL[dateKey] > 0)
                _consecutiveLossingDays = 0;
        }

        private decimal GetTodaysPnL()
        {
            var today = DateTime.Today.ToString("yyyy-MM-dd");
            return _dailyPnL.GetValueOrDefault(today, 0m);
        }

        private double GetIVRank(MarketConditions conditions)
        {
            // Estimate IV rank from VIX (simplified)
            return Math.Min(100, Math.Max(0, (conditions.VIX - 10) * 2));
        }

        private GoInputs ConvertToGoInputs(MarketConditions conditions, StrategyParameters parameters)
        {
            // Convert market conditions to GoScore inputs
            var poE = Math.Max(0.3, 0.8 - (conditions.VIX / 100.0)); // Higher in low vol
            var poT = Math.Min(0.6, conditions.VIX / 50.0); // Higher in high vol
            var edge = (GetIVRank(conditions) - 50) / 100.0; // Positive when IV rank > 50
            var liqScore = Math.Max(0.5, 1.0 - (conditions.VIX / 100.0)); // Better in low vol
            var regScore = poE; // Simplified
            var pinScore = 0.7; // Simplified
            var rfibUtil = Math.Max(0.0, Math.Min(1.0, _consecutiveLossingDays / 5.0));

            return new GoInputs(poE, poT, edge, liqScore, regScore, pinScore, rfibUtil);
        }

        private StrategyKind SelectStrategyKind(MarketConditions conditions)
        {
            return conditions.VIX > 25 ? StrategyKind.IronCondor : StrategyKind.CreditBwb;
        }

        private ODTE.Strategy.GoScore.Regime ClassifyRegime(MarketConditions conditions)
        {
            if (conditions.VIX > 30) return ODTE.Strategy.GoScore.Regime.Convex;
            if (conditions.VIX > 20) return ODTE.Strategy.GoScore.Regime.Mixed;
            return ODTE.Strategy.GoScore.Regime.Calm;
        }

        private GoPolicy LoadOptimizedGoScorePolicy()
        {
            // Load optimized policy with profitable thresholds
            return new GoPolicy
            {
                Weights = new Weights(wPoE: 1.8, wPoT: -0.8, wEdge: 1.2, wLiq: 0.7, wReg: 0.9, wPin: 0.4, wRfib: -0.9),
                Thresholds = new Thresholds(full: 70.0, half: 60.0, minLiqScore: 0.4)
            };
        }
    }

    public class TradeDecision
    {
        public bool ShouldTrade { get; set; }
        public decimal RecommendedSize { get; set; }
        public double GoScore { get; set; }
        public decimal ExpectedProfit { get; set; }
        public decimal MaxRisk { get; set; }
        public string Reason { get; set; } = "";
    }
}