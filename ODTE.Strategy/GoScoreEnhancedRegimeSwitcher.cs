using System;
using System.Collections.Generic;
using System.Linq;
using ODTE.Strategy.GoScore;

namespace ODTE.Strategy
{
    /// <summary>
    /// GoScore-Enhanced Regime Switcher: Intelligent Battle Selection
    /// 
    /// This enhanced version of RegimeSwitcher integrates the GoScore framework to make
    /// intelligent decisions about which market opportunities to pursue vs skip.
    /// 
    /// KEY ENHANCEMENT: Instead of trading every day, GoScore evaluates each potential
    /// trade and only executes when the probability of success meets our thresholds:
    /// - Score ≥70: Full position (aggressive)  
    /// - Score 55-69: Half position (cautious)
    /// - Score <55: Skip trade (preserve capital)
    /// 
    /// OPTIMIZATION TARGET: This class demonstrates how ML/GA can optimize:
    /// 1. GoScore weight parameters for better trade selection
    /// 2. Decision thresholds for different market regimes
    /// 3. Position sizing based on confidence levels
    /// 4. Regime-specific scoring adjustments
    /// </summary>
    public class GoScoreEnhancedRegimeSwitcher : RegimeSwitcher
    {
        private readonly GoScorer _goScorer;
        private readonly GoPolicy _policy;
        private readonly Random _random;
        
        // Trade selection statistics for optimization feedback
        public int TotalOpportunities { get; private set; }
        public int FullPositions { get; private set; }
        public int HalfPositions { get; private set; }
        public int SkippedTrades { get; private set; }
        public double SelectivityRate => TotalOpportunities > 0 ? (double)SkippedTrades / TotalOpportunities : 0;
        
        public GoScoreEnhancedRegimeSwitcher(GoScorer goScorer, GoPolicy policy, Random? random = null)
        {
            _goScorer = goScorer;
            _policy = policy;
            _random = random ?? new Random();
        }

        /// <summary>
        /// Enhanced daily strategy execution with GoScore battle selection
        /// 
        /// OPTIMIZATION OPPORTUNITY: This method contains the core logic for intelligent
        /// trade selection that can be tuned via genetic algorithms or machine learning
        /// </summary>
        protected virtual DailyResult ExecuteDailyStrategyWithGoScore(DateTime date, TwentyFourDayPeriod period)
        {
            var result = new DailyResult { Date = date };

            // Step 1: Generate market conditions (same as baseline)
            result.Conditions = GenerateMarketConditions(date);

            // Step 2: Classify market regime (same as baseline)  
            result.DetectedRegime = ClassifyRegime(result.Conditions);

            // Step 3: Get potential strategy parameters (same as baseline)
            result.StrategyUsed = GetStrategyParameters(result.DetectedRegime, result.Conditions);

            // Step 4: NEW - GoScore Evaluation: Should we take this trade?
            var goScoreInputs = CalculateGoScoreInputs(result.StrategyUsed, result.Conditions, period);
            var goScoreBreakdown = _goScorer.GetBreakdown(
                goScoreInputs, 
                MapToGoScoreStrategy(result.StrategyUsed),
                MapToGoScoreRegime(result.DetectedRegime)
            );

            // Step 5: Make intelligent execution decision based on GoScore
            TotalOpportunities++;
            var executionDecision = goScoreBreakdown.Decision;
            
            // Update statistics and get position size multiplier
            double positionSizeMultiplier;
            switch (executionDecision)
            {
                case Decision.Full:
                    FullPositions++;
                    positionSizeMultiplier = 1.0;
                    break;
                case Decision.Half:
                    HalfPositions++;
                    positionSizeMultiplier = 0.5;
                    break;
                case Decision.Skip:
                    SkippedTrades++;
                    positionSizeMultiplier = 0.0;
                    break;
                default:
                    positionSizeMultiplier = 0.0;
                    break;
            }

            // Step 6: Execute strategy with GoScore-determined position sizing
            if (positionSizeMultiplier > 0)
            {
                // Execute the trade with appropriate position sizing
                var basePnL = SimulateStrategyExecution(result.StrategyUsed, result.Conditions, period);
                result.DailyPnL = basePnL * positionSizeMultiplier;
                result.ExecutionSummary = $"GoScore: {goScoreBreakdown.FinalScore:F1} → {executionDecision} " +
                                        $"({positionSizeMultiplier:P0} position) → P&L: ${result.DailyPnL:F0}";
            }
            else
            {
                // Skip the trade - preserve capital
                result.DailyPnL = 0;
                result.ExecutionSummary = $"GoScore: {goScoreBreakdown.FinalScore:F1} → SKIP " +
                                        $"(Reason: Low score)";
            }

            // Track GoScore decision data for optimization analysis
            result.GoScoreData = new GoScoreDecisionData
            {
                Score = goScoreBreakdown.FinalScore,
                Decision = executionDecision,
                Components = new Dictionary<string, double>(), // Will be populated by GoScorer
                Inputs = goScoreInputs
            };

            // Update cumulative metrics (same as baseline)
            var cumulativePnL = period.CurrentCapital + result.DailyPnL - period.StartingCapital;
            result.CumulativePnL = cumulativePnL;
            result.DrawdownFromPeak = Math.Min(0, (period.CurrentCapital + result.DailyPnL) - period.Peak);

            return result;
        }

        /// <summary>
        /// Calculate GoScore inputs for trade evaluation
        /// 
        /// ML/GA OPTIMIZATION TARGET: These input calculations can be enhanced via:
        /// - Feature engineering (additional market indicators)
        /// - Learned weightings for different input components  
        /// - Regime-specific input adjustments
        /// - Historical performance feedback loops
        /// </summary>
        private GoInputs CalculateGoScoreInputs(StrategyParameters strategy, MarketConditions conditions, TwentyFourDayPeriod period)
        {
            // Probability of Expiring Profitable (PoE) - based on strategy and market conditions
            var poe = CalculatePoE(strategy, conditions);
            
            // Probability of Tail Event (PoT) - tail risk assessment
            var pot = CalculatePot(strategy, conditions);
            
            // Expected Edge - mathematical advantage estimation
            var edge = CalculateEdge(strategy, conditions);
            
            // Liquidity Score - market microstructure quality
            var liqScore = CalculateLiquidityScore(conditions);
            
            // Regime Score - how well strategy fits current regime
            var regScore = CalculateRegimeScore(strategy, conditions);
            
            // Pin Score - options expiry pin risk
            var pinScore = CalculatePinScore(conditions);
            
            // RFib Utilization - position sizing vs daily risk budget
            var rfibUtil = CalculateRfibUtilization(strategy, period);

            return new GoInputs(poe, pot, edge, liqScore, regScore, pinScore, rfibUtil);
        }

        /// <summary>
        /// Calculate Probability of Expiring Profitable
        /// 
        /// GENETIC ALGORITHM TARGET: This calculation can be optimized via ML models
        /// that learn from historical outcomes vs predicted probabilities
        /// </summary>
        private double CalculatePoE(StrategyParameters strategy, MarketConditions conditions)
        {
            // Base probability from volatility regime and strategy type
            var basePoE = conditions.IVR switch
            {
                < 0.2 => 0.65,  // Low IV = lower credit but higher win rate
                < 0.5 => 0.75,  // Medium IV = balanced
                < 0.8 => 0.82,  // High IV = higher credit, good for sellers
                _ => 0.78       // Very high IV = some risk of vol expansion
            };

            // Adjust for trend conditions (credit spreads hate strong trends)
            var trendPenalty = Math.Abs(conditions.TrendScore) * 0.15;
            basePoE -= trendPenalty;

            // Strategy-specific adjustments
            if (strategy.UseTailExtender) basePoE += 0.05; // Tail protection
            if (strategy.UseOppositeIncome) basePoE += 0.03; // Additional income

            return Math.Max(0.3, Math.Min(0.95, basePoE));
        }

        /// <summary>
        /// Calculate Probability of Tail Event
        /// 
        /// ML OPTIMIZATION TARGET: Learn tail risk patterns from historical data
        /// </summary>
        private double CalculatePot(StrategyParameters strategy, MarketConditions conditions)
        {
            // Base tail risk from market volatility
            var basePot = conditions.VIX switch
            {
                < 20 => 0.05,   // Low vol = low tail risk
                < 30 => 0.12,   // Medium vol = moderate tail risk  
                < 40 => 0.25,   // High vol = elevated tail risk
                _ => 0.45       // Extreme vol = high tail risk
            };

            // Trend amplifies tail risk (momentum can cause breakouts)
            var trendAmplifier = 1.0 + Math.Abs(conditions.TrendScore) * 0.8;
            basePot *= trendAmplifier;

            // Term structure warning (backwardation = stressed markets)
            if (conditions.TermSlope < 0.85) basePot *= 1.3;

            return Math.Max(0.01, Math.Min(0.8, basePot));
        }

        /// <summary>
        /// Calculate mathematical edge of the strategy
        /// </summary>
        private double CalculateEdge(StrategyParameters strategy, MarketConditions conditions)
        {
            // Positive edge when we're collecting credit in appropriate conditions
            var ivRankEdge = (conditions.IVR - 0.5) * 0.2; // Positive when IV > 50th percentile
            
            // Realized vs implied edge (sell when IV > RV)
            var volEdge = (1.0 - conditions.RealizedVsImplied) * 0.15;
            
            // Strategy fit edge
            var strategyEdge = strategy.CreditMin > 0.15 ? 0.05 : -0.02; // Favor decent credit

            return ivRankEdge + volEdge + strategyEdge;
        }

        /// <summary>
        /// Calculate liquidity quality score
        /// </summary>
        private double CalculateLiquidityScore(MarketConditions conditions)
        {
            // Simple model - in real trading would use bid-ask spreads, volume, etc.
            var vixLiquidity = conditions.VIX < 50 ? 0.8 : 0.6; // Lower liquidity in crisis
            var timeLiquidity = 0.85; // Assume reasonable liquidity for 0DTE options
            
            return Math.Min(vixLiquidity, timeLiquidity);
        }

        /// <summary>
        /// Calculate how well strategy fits the regime
        /// </summary>
        private double CalculateRegimeScore(StrategyParameters strategy, MarketConditions conditions)
        {
            var regime = ClassifyRegime(conditions);
            
            return regime switch
            {
                Regime.Calm => strategy.UseTailExtender ? 0.7 : 0.9, // BWB perfect for calm
                Regime.Mixed => 0.8, // Most strategies work in mixed
                Regime.Convex => strategy.UseTailExtender ? 0.9 : 0.6, // Need protection in convex
                _ => 0.7
            };
        }

        /// <summary>
        /// Calculate pin risk (options gravitating to strikes)
        /// </summary>
        private double CalculatePinScore(MarketConditions conditions)
        {
            // Lower scores = higher pin risk = worse for credit spreads
            // Pin risk higher on expiry day with low volatility
            var volPinFactor = conditions.VIX < 15 ? 0.6 : 0.8;
            
            return volPinFactor;
        }

        /// <summary>
        /// Calculate RFib utilization (position sizing vs daily risk budget)
        /// </summary>
        private double CalculateRfibUtilization(StrategyParameters strategy, TwentyFourDayPeriod period)
        {
            // Simplified model - in real system would track actual RFib usage
            var baseUtilization = strategy.CreditMin * 0.8; // Higher credit = more capital at risk
            
            // Reduce if we've had recent losses
            var recentDrawdown = Math.Abs(period.MaxDrawdown) / period.StartingCapital;
            if (recentDrawdown > 0.05) baseUtilization *= 0.7; // Reduce after drawdown
            
            return Math.Max(0.1, Math.Min(0.9, baseUtilization));
        }

        /// <summary>
        /// Map strategy parameters to GoScore strategy type
        /// </summary>
        private StrategyKind MapToGoScoreStrategy(StrategyParameters strategy)
        {
            return strategy.UseTailExtender ? StrategyKind.CreditBwb : StrategyKind.IronCondor;
        }

        /// <summary>
        /// Map regime to GoScore regime type
        /// </summary>
        private ODTE.Strategy.GoScore.Regime MapToGoScoreRegime(Regime regime)
        {
            return regime switch
            {
                Regime.Calm => ODTE.Strategy.GoScore.Regime.Calm,
                Regime.Mixed => ODTE.Strategy.GoScore.Regime.Mixed,
                Regime.Convex => ODTE.Strategy.GoScore.Regime.Convex,
                _ => ODTE.Strategy.GoScore.Regime.Calm
            };
        }

        /// <summary>
        /// Override the base method to use GoScore-enhanced execution
        /// </summary>
        protected virtual MarketConditions GenerateMarketConditions(DateTime date)
        {
            // Use base implementation for now - in real system would integrate with data feeds
            var random = new Random(date.GetHashCode());
            
            return new MarketConditions
            {
                Date = date,
                VIX = 15 + random.NextDouble() * 30, // 15-45 VIX range
                IVR = random.NextDouble(), // 0-1 IV Rank
                TermSlope = 0.8 + random.NextDouble() * 0.4, // 0.8-1.2 term structure
                TrendScore = (random.NextDouble() - 0.5) * 2, // -1 to +1
                RealizedVsImplied = 0.7 + random.NextDouble() * 0.8, // 0.7-1.5
            };
        }
    }

    /// <summary>
    /// Extended daily result with GoScore decision data for optimization analysis
    /// </summary>
    public class GoScoreDecisionData
    {
        public double Score { get; set; }
        public Decision Decision { get; set; }
        public Dictionary<string, double> Components { get; set; } = new();
        public required GoInputs Inputs { get; set; }
    }
}

// Extension to DailyResult to include GoScore data
namespace ODTE.Strategy
{
    public partial class RegimeSwitcher
    {
        public partial class DailyResult
        {
            public GoScoreDecisionData? GoScoreData { get; set; }
        }
    }
}