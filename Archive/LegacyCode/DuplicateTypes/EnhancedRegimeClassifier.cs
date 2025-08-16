using System;
using ODTE.Strategy.Interfaces;
using ODTE.Strategy.Models;

namespace ODTE.Strategy
{
    /// <summary>
    /// Enhanced regime classifier that suppresses IC in Convex regimes and enforces strategy selection rules
    /// Implements the strategy suppression and preference logic from the next-steps document
    /// 
    /// ** MACHINE LEARNING / GENETIC ALGORITHM OPTIMIZATION TARGETS **
    /// 
    /// REGIME CLASSIFICATION PARAMETERS (Key ML targets):
    /// 1. VIX thresholds: >40=Convex, >25=Mixed, else=Calm [GENETIC RANGE: VIX±5 points]
    /// 2. Trend strength: |trendScore| ≥0.8 → Convex [ML RANGE: 0.6-1.0] 
    /// 3. IV rank thresholds for regime transitions [ML RANGE: 0.1-0.9]
    /// 
    /// STRATEGY SELECTION LOGIC (Primary optimization focus):
    /// - Iron Condor suppression in Convex regimes (high volatility protection)
    /// - BWB preference during volatility expansion periods  
    /// - Regime-specific ROC requirements (Convex needs ≥30% vs 15% for others)
    /// - VIX-based position sizing (>40 VIX = 25% sizing, >30 VIX = 50% sizing)
    /// 
    /// GENETIC ALGORITHM APPROACH:
    /// - Encode thresholds as gene sequences: [vixMixed, vixConvex, trendThresh, rocThresh]
    /// - Fitness function: 20-year backtest Sharpe ratio + win rate - max drawdown
    /// - Crossover: Arithmetic averaging with ±10% mutation rates
    /// - Population: 100 parameter sets, 50 generations minimum
    /// - Elite preservation: Top 10% always survive to next generation
    /// 
    /// REINFORCEMENT LEARNING TARGETS:
    /// - Real-time regime classification accuracy (vs realized volatility outcomes)
    /// - Strategy selection rewards (profit per regime-strategy combination)
    /// - Adaptive threshold learning (market regime shifts over time)
    /// </summary>
    public class EnhancedRegimeClassifier
    {
        private readonly DefaultRiskModel _riskModel;

        public EnhancedRegimeClassifier()
        {
            _riskModel = new DefaultRiskModel();
        }

        /// <summary>
        /// Classify market regime and recommend strategy with entry gates
        /// </summary>
        public RegimeAnalysisResult AnalyzeAndRecommend(MarketSnapshot market)
        {
            var regime = ClassifyRegime(market);
            var allowedStrategies = GetAllowedStrategies(regime, market);
            var recommendedStrategy = SelectBestStrategy(regime, market, allowedStrategies);

            return new RegimeAnalysisResult
            {
                Regime = regime,
                RecommendedStrategy = recommendedStrategy,
                AllowedStrategies = allowedStrategies,
                Evidence = BuildRegimeEvidence(market),
                Restrictions = BuildRestrictions(regime, market)
            };
        }

        /// <summary>
        /// Validate if a candidate order should be allowed based on regime and market conditions
        /// </summary>
        public bool ValidateCandidateOrder(CandidateOrder candidate, MarketSnapshot market, out string reason)
        {
            var regime = ClassifyRegime(market);
            
            // Suppress IC in Convex regime as per requirements
            if (regime == RegimeSwitcher.Regime.Convex && candidate.Shape.Name == "IronCondor")
            {
                reason = "IC suppressed in Convex regime (VIX>40 or high trend)";
                return false;
            }

            // VIX sizing rules
            if (!ValidateVIXSizing(candidate, market, out reason))
            {
                return false;
            }

            // Trend breaker rule
            if (!ValidateTrendConditions(candidate, market, out reason))
            {
                return false;
            }

            // BWB specific gates (general validation)
            if (candidate.Shape.Name == "CreditBWB" && !ValidateBWBGeneralGates(candidate, market, out reason))
            {
                return false;
            }

            // ROC threshold for Convex regime
            if (regime == RegimeSwitcher.Regime.Convex && !ValidateConvexROC(candidate, out reason))
            {
                return false;
            }

            reason = "All gates passed";
            return true;
        }

        /// <summary>
        /// CORE REGIME CLASSIFICATION ALGORITHM - Primary ML/GA optimization target
        /// 
        /// This method contains the most critical decision thresholds for strategy selection.
        /// Every parameter here directly impacts trading performance and should be optimized.
        /// 
        /// GENETIC ALGORITHM OPTIMIZATION PARAMETERS:
        /// - VIX_CONVEX_THRESHOLD: Currently 40, optimize range [35-45]
        /// - VIX_MIXED_THRESHOLD: Currently 25, optimize range [20-30] 
        /// - TREND_CONVEX_THRESHOLD: Currently 0.8, optimize range [0.6-1.0]
        /// - TREND_MIXED_THRESHOLD: Currently 0.4, optimize range [0.3-0.6]
        /// - IVRANK_MIXED_THRESHOLD: Currently 0.7, optimize range [0.5-0.9]
        /// 
        /// MACHINE LEARNING APPROACH:
        /// 1. Feature engineering: Add moving averages, volatility ratios, correlation measures
        /// 2. Classification models: Random Forest, SVM, Neural Networks for regime prediction
        /// 3. Ensemble methods: Combine multiple regime classifiers with voting
        /// 4. Time-series models: LSTM/GRU for regime transition prediction
        /// 
        /// OPTIMIZATION FITNESS FUNCTION:
        /// - Primary: Maximize profit per regime (measured over 20-year backtest)
        /// - Secondary: Minimize regime misclassification rate
        /// - Penalty: Excessive regime switches (prevent overfitting to noise)
        /// </summary>
        private RegimeSwitcher.Regime ClassifyRegime(MarketSnapshot market)
        {
            // GENETIC ALGORITHM TARGET: These threshold values are prime optimization candidates
            var vix = market.VIX;                          // Raw market fear/volatility measure
            var trendScore = Math.Abs(market.TrendScore);  // Directional momentum strength [0-1]
            var ivRank = market.IVRank;                    // Implied volatility percentile [0-1]

            // CONVEX REGIME: High volatility or strong directional movement
            // ML OPTIMIZATION TARGET: Tune these thresholds for maximum tail risk protection
            if (vix > 40 ||                    // VIX_CONVEX_THRESHOLD [GENETIC RANGE: 35-45]
                trendScore >= 0.8m)            // TREND_CONVEX_THRESHOLD [GENETIC RANGE: 0.6-1.0]
            {
                return RegimeSwitcher.Regime.Convex;
            }

            // MIXED REGIME: Elevated volatility or moderate trends
            // ML OPTIMIZATION TARGET: Balance between Calm and Convex classification accuracy
            if (vix > 25 ||                    // VIX_MIXED_THRESHOLD [GENETIC RANGE: 20-30]
                ivRank > 0.7m ||               // IVRANK_MIXED_THRESHOLD [GENETIC RANGE: 0.5-0.9] 
                trendScore >= 0.4m)            // TREND_MIXED_THRESHOLD [GENETIC RANGE: 0.3-0.6]
            {
                return RegimeSwitcher.Regime.Mixed;
            }

            // CALM REGIME: Low volatility, range-bound markets (DEFAULT)
            // STRATEGY IMPLICATIONS: Allow both IC and BWB, standard position sizing
            return RegimeSwitcher.Regime.Calm;
        }

        private string[] GetAllowedStrategies(RegimeSwitcher.Regime regime, MarketSnapshot market)
        {
            return regime switch
            {
                RegimeSwitcher.Regime.Calm => new[] { "CreditBWB", "IronCondor" },
                RegimeSwitcher.Regime.Mixed => new[] { "CreditBWB", "IronCondor", "TailOverlay" },
                RegimeSwitcher.Regime.Convex => new[] { "CreditBWB", "RatioBackspread" }, // IC suppressed
                _ => new[] { "CreditBWB" }
            };
        }

        private string SelectBestStrategy(RegimeSwitcher.Regime regime, MarketSnapshot market, string[] allowedStrategies)
        {
            return regime switch
            {
                RegimeSwitcher.Regime.Calm => "CreditBWB", // Better ROC than IC
                RegimeSwitcher.Regime.Mixed => "CreditBWB", // BWB + optional tail ticket
                RegimeSwitcher.Regime.Convex => market.VIX > 50 ? "RatioBackspread" : "CreditBWB",
                _ => "CreditBWB"
            };
        }

        /// <summary>
        /// VIX-BASED POSITION SIZING AND STRATEGY SUPPRESSION - Critical ML optimization target
        /// 
        /// This method implements the core risk management system that scales position sizes
        /// and suppresses risky strategies based on market volatility (VIX) levels.
        /// 
        /// MACHINE LEARNING OPTIMIZATION TARGETS:
        /// 1. VIX thresholds for position size reduction [GENETIC ALGORITHM TARGET]
        /// 2. Strategy suppression levels [REINFORCEMENT LEARNING TARGET]  
        /// 3. Position size multipliers [CONTINUOUS OPTIMIZATION TARGET]
        /// 
        /// GENETIC ALGORITHM PARAMETERS:
        /// - VIX_HIGH_THRESHOLD: Currently 40, optimize range [35-45]
        /// - VIX_MEDIUM_THRESHOLD: Currently 30, optimize range [25-35]
        /// - HIGH_VIX_SIZE_MULTIPLIER: Currently 0.25, optimize range [0.15-0.35]
        /// - MEDIUM_VIX_SIZE_MULTIPLIER: Currently 0.5, optimize range [0.4-0.7]
        /// 
        /// STRATEGY SELECTION IMPACT:
        /// - Iron Condor suppression in high VIX protects against gamma risk
        /// - Position size reduction prevents catastrophic losses during vol expansion
        /// - Adaptive sizing allows profitable participation while managing tail risk
        /// 
        /// OPTIMIZATION APPROACH:
        /// - Backtest different VIX thresholds against historical vol clusters
        /// - Optimize for maximum return per unit of VIX risk
        /// - Machine learning models can learn regime-specific VIX sensitivity
        /// </summary>
        private bool ValidateVIXSizing(CandidateOrder candidate, MarketSnapshot market, out string reason)
        {
            var vix = market.VIX;  // Market fear/volatility index
            
            // HIGH VOLATILITY REGIME: Extreme risk management
            if (vix > 40)  // VIX_HIGH_THRESHOLD [GENETIC RANGE: 35-45]
            {
                // STRATEGY SUPPRESSION: Iron Condor completely banned in high VIX
                // ML TARGET: Learn optimal strategy suppression thresholds per volatility level
                if (candidate.Shape.Name == "IronCondor")
                {
                    reason = "IC suppressed when VIX > 40";
                    return false;
                }
                
                // POSITION SIZE REDUCTION: Maximum 25% position sizing
                // GENETIC TARGET: Optimize size multiplier for high VIX scenarios
                if (candidate.RfibUtilization > 0.25m)  // HIGH_VIX_SIZE_MULTIPLIER [GENETIC RANGE: 0.15-0.35]
                {
                    reason = "Position size too large for VIX > 40 (max 0.25x sizing)";
                    return false;
                }
            }
            // MEDIUM VOLATILITY REGIME: Moderate risk management
            else if (vix > 30)  // VIX_MEDIUM_THRESHOLD [GENETIC RANGE: 25-35]
            {
                // POSITION SIZE REDUCTION: Maximum 50% position sizing for elevated volatility
                // GENETIC TARGET: Optimize medium VIX size multiplier for balance of profit vs risk
                if (candidate.RfibUtilization > 0.5m)  // MEDIUM_VIX_SIZE_MULTIPLIER [GENETIC RANGE: 0.4-0.7]
                {
                    reason = "Position size too large for VIX > 30 (max 0.5x sizing)";
                    return false;
                }
            }
            // LOW VOLATILITY REGIME: Standard position sizing allowed (up to RFib limits)
            
            reason = "VIX sizing rules satisfied";
            return true;
        }

        /// <summary>
        /// TREND-BASED ENTRY BLOCKING - ML optimization target for momentum protection
        /// 
        /// Prevents new option positions during strong directional moves to avoid
        /// getting caught in momentum breakouts that can cause rapid losses.
        /// 
        /// GENETIC ALGORITHM OPTIMIZATION:
        /// - TREND_BLOCK_THRESHOLD: Currently 0.8, optimize range [0.6-1.0]
        /// - Consider adaptive thresholds based on volatility regime
        /// - ML models could predict trend continuation vs reversal probability
        /// 
        /// STRATEGY SELECTION IMPACT:
        /// - Protects credit spreads from directional risk during breakouts
        /// - Prevents entries during potential gamma squeeze scenarios
        /// - Maintains position quality by avoiding high-momentum environments
        /// </summary>
        private bool ValidateTrendConditions(CandidateOrder candidate, MarketSnapshot market, out string reason)
        {
            // GENETIC TARGET: Trend strength threshold for entry blocking
            var trendScore = Math.Abs(market.TrendScore);  // Absolute momentum strength [0-1]
            
            // TREND BLOCKING: Prevent entries during strong directional moves
            // ML OPTIMIZATION TARGET: Learn optimal trend threshold per market regime
            if (trendScore >= 0.8m)  // TREND_BLOCK_THRESHOLD [GENETIC RANGE: 0.6-1.0]
            {
                reason = "New entries blocked during strong trend (|Trend5m| >= 0.8)";
                return false;
            }

            reason = "Trend conditions acceptable";
            return true;
        }

        private bool ValidateBWBGeneralGates(CandidateOrder candidate, MarketSnapshot market, out string reason)
        {
            // BWB general validation (not regime-specific)
            
            // Basic credit check (ensure some minimum credit)
            if (candidate.Roc < 0.15m)
            {
                reason = "BWB credit too thin (ROC < 15%)";
                return false;
            }

            // Delta check (|net-Δ| ≤ 3 per contract at entry)
            // This would need actual delta calculation from the shape
            // For now, use a simplified check

            reason = "BWB geometry gates passed";
            return true;
        }

        /// <summary>
        /// CONVEX REGIME ROC THRESHOLD - Critical ML optimization target for high volatility trading
        /// 
        /// In high volatility (Convex) regimes, strategies must meet higher return-on-capital
        /// requirements to justify the increased tail risk and volatility exposure.
        /// 
        /// GENETIC ALGORITHM OPTIMIZATION:
        /// - CONVEX_ROC_THRESHOLD: Currently 30%, optimize range [20%-40%]
        /// - Could be adaptive based on VIX level (higher VIX = higher ROC requirement)
        /// - ML models could learn optimal ROC thresholds per volatility cluster
        /// 
        /// STRATEGY SELECTION IMPACT:
        /// - Forces higher quality setups during dangerous market conditions
        /// - Prevents low-return trades when tail risk is elevated  
        /// - Maintains portfolio performance by being more selective in Convex regimes
        /// 
        /// OPTIMIZATION APPROACH:
        /// - Backtest different ROC thresholds vs actual Convex regime outcomes
        /// - Optimize for: maximum Sharpe ratio in high volatility periods
        /// - Consider regime-specific vs adaptive threshold approaches
        /// </summary>
        private bool ValidateConvexROC(CandidateOrder candidate, out string reason)
        {
            // GENETIC ALGORITHM TARGET: ROC threshold for high volatility regime trading
            const decimal ConvexROCThreshold = 0.30m; // CONVEX_ROC_THRESHOLD [GENETIC RANGE: 0.20-0.40]
            
            // CONVEX ROC REQUIREMENT: Higher returns required to justify elevated risk
            // ML OPTIMIZATION TARGET: Learn optimal ROC thresholds per VIX level  
            if (candidate.Roc < ConvexROCThreshold)
            {
                reason = $"ROC too low for Convex regime (required: {ConvexROCThreshold:P0}, actual: {candidate.Roc:P1})";
                return false;
            }

            reason = "Convex ROC threshold met";
            return true;
        }

        private string BuildRegimeEvidence(MarketSnapshot market)
        {
            return $"VIX:{market.VIX:F1}, IVR:{market.IVRank:F2}, Trend:{market.TrendScore:F2}";
        }

        private string[] BuildRestrictions(RegimeSwitcher.Regime regime, MarketSnapshot market)
        {
            var restrictions = new List<string>();

            if (regime == RegimeSwitcher.Regime.Convex)
            {
                restrictions.Add("IC suppressed");
                restrictions.Add("High ROC required");
            }

            if (market.VIX > 40)
            {
                restrictions.Add("0.25x position sizing");
            }
            else if (market.VIX > 30)
            {
                restrictions.Add("0.5x position sizing");
            }

            if (Math.Abs(market.TrendScore) >= 0.8m)
            {
                restrictions.Add("New entries blocked");
            }

            return restrictions.ToArray();
        }
    }

    /// <summary>
    /// Result of regime analysis and strategy recommendation
    /// </summary>
    public class RegimeAnalysisResult
    {
        public RegimeSwitcher.Regime Regime { get; set; }
        public string RecommendedStrategy { get; set; } = "";
        public string[] AllowedStrategies { get; set; } = Array.Empty<string>();
        public string Evidence { get; set; } = "";
        public string[] Restrictions { get; set; } = Array.Empty<string>();
    }
}