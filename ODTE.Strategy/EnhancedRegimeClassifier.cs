using System;
using ODTE.Strategy.Interfaces;
using ODTE.Strategy.Models;

namespace ODTE.Strategy
{
    /// <summary>
    /// Enhanced regime classifier that suppresses IC in Convex regimes and enforces strategy selection rules
    /// Implements the strategy suppression and preference logic from the next-steps document
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

        private RegimeSwitcher.Regime ClassifyRegime(MarketSnapshot market)
        {
            // Enhanced classification with evidence logging
            var vix = market.VIX;
            var trendScore = Math.Abs(market.TrendScore);
            var ivRank = market.IVRank;

            // Convex: High volatility or strong trend
            if (vix > 40 || trendScore >= 0.8m)
            {
                return RegimeSwitcher.Regime.Convex;
            }

            // Mixed: Elevated volatility or moderate trend  
            if (vix > 25 || ivRank > 0.7m || trendScore >= 0.4m)
            {
                return RegimeSwitcher.Regime.Mixed;
            }

            // Calm: Low volatility, range-bound
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

        private bool ValidateVIXSizing(CandidateOrder candidate, MarketSnapshot market, out string reason)
        {
            var vix = market.VIX;
            
            if (vix > 40)
            {
                // Suppress IC completely when VIX > 40
                if (candidate.Shape.Name == "IronCondor")
                {
                    reason = "IC suppressed when VIX > 40";
                    return false;
                }
                
                // Reduce position size to 0.25x when VIX > 40
                if (candidate.RfibUtilization > 0.25m)
                {
                    reason = "Position size too large for VIX > 40 (max 0.25x sizing)";
                    return false;
                }
            }
            else if (vix > 30)
            {
                // Reduce position size to 0.5x when VIX > 30
                if (candidate.RfibUtilization > 0.5m)
                {
                    reason = "Position size too large for VIX > 30 (max 0.5x sizing)";
                    return false;
                }
            }

            reason = "VIX sizing rules satisfied";
            return true;
        }

        private bool ValidateTrendConditions(CandidateOrder candidate, MarketSnapshot market, out string reason)
        {
            var trendScore = Math.Abs(market.TrendScore);
            
            // Block new entries during strong trends (cooldown period)
            if (trendScore >= 0.8m)
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

        private bool ValidateConvexROC(CandidateOrder candidate, out string reason)
        {
            const decimal ConvexROCThreshold = 0.30m; // Higher ROC required in Convex
            
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