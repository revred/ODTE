using System;
using System.Collections.Generic;
using System.Linq;

namespace ODTE.Strategy
{
    /// <summary>
    /// Optimal Condition Detection System
    /// 
    /// SOPHISTICATED MARKET ANALYSIS:
    /// - Multi-factor condition scoring for optimal trade entry
    /// - Real-time market regime classification
    /// - Volatility and liquidity assessment
    /// - Time-of-day optimization patterns
    /// - Historical pattern recognition for high-probability setups
    /// </summary>
    public class OptimalConditionDetector
    {
        private readonly Dictionary<string, double> _conditionWeights;
        private readonly List<MarketPattern> _historicalPatterns;
        private readonly Random _random;

        // Optimal condition thresholds
        private const double EXCELLENT_THRESHOLD = 85.0;
        private const double FAVORABLE_THRESHOLD = 70.0;
        private const double ADEQUATE_THRESHOLD = 55.0;
        private const double POOR_THRESHOLD = 40.0;

        public OptimalConditionDetector()
        {
            _conditionWeights = InitializeConditionWeights();
            _historicalPatterns = new List<MarketPattern>();
            _random = new Random();
        }

        public OptimalConditions AnalyzeConditions(MarketConditions conditions)
        {
            try
            {
                // Step 1: Calculate individual condition scores
                var scores = CalculateConditionScores(conditions);
                
                // Step 2: Apply weighted composite scoring
                var compositeScore = CalculateCompositeScore(scores);
                
                // Step 3: Apply time-based optimizations
                var timeAdjustedScore = ApplyTimingOptimizations(compositeScore, conditions);
                
                // Step 4: Historical pattern enhancement
                var finalScore = ApplyHistoricalPatternBoost(timeAdjustedScore, conditions);
                
                // Step 5: Classify quality level
                var quality = ClassifyQualityLevel(finalScore);
                
                return new OptimalConditions
                {
                    Quality = quality,
                    Score = finalScore,
                    Reason = GenerateQualityReason(quality, scores, conditions),
                    ComponentScores = scores
                };
            }
            catch (Exception ex)
            {
                return new OptimalConditions
                {
                    Quality = OptimalQuality.Poor,
                    Score = 0,
                    Reason = $"Analysis error: {ex.Message}"
                };
            }
        }

        private Dictionary<string, double> CalculateConditionScores(MarketConditions conditions)
        {
            var scores = new Dictionary<string, double>();

            // 1. Volatility Assessment (25% weight)
            scores["Volatility"] = AnalyzeVolatilityConditions(conditions);
            
            // 2. Market Regime Suitability (20% weight)
            scores["MarketRegime"] = AnalyzeMarketRegime(conditions);
            
            // 3. Trend Stability (15% weight)
            scores["TrendStability"] = AnalyzeTrendStability(conditions);
            
            // 4. Liquidity Conditions (15% weight)
            scores["Liquidity"] = AnalyzeLiquidityConditions(conditions);
            
            // 5. Time-of-Day Favorability (10% weight)
            scores["TimeOfDay"] = AnalyzeTimeOptimality(conditions);
            
            // 6. Options-Specific Factors (10% weight)
            scores["OptionsFactors"] = AnalyzeOptionsSpecificFactors(conditions);
            
            // 7. Risk Environment (5% weight)
            scores["RiskEnvironment"] = AnalyzeRiskEnvironment(conditions);

            return scores;
        }

        private double AnalyzeVolatilityConditions(MarketConditions conditions)
        {
            var vix = conditions.VIX;
            
            // Optimal VIX ranges for 0DTE strategies
            if (vix >= 15 && vix <= 25)
                return 90.0; // Sweet spot: enough premium, manageable risk
            else if (vix >= 12 && vix < 15)
                return 75.0; // Low vol: less premium but very stable
            else if (vix > 25 && vix <= 35)
                return 70.0; // Elevated vol: good premium, higher risk
            else if (vix > 35 && vix <= 45)
                return 45.0; // High vol: great premium, significant risk
            else if (vix > 45)
                return 20.0; // Extreme vol: dangerous conditions
            else
                return 30.0; // Ultra-low vol: insufficient premium
        }

        private double AnalyzeMarketRegime(MarketConditions conditions)
        {
            switch (conditions.MarketRegime.ToLower())
            {
                case "calm":
                    return 95.0; // Ideal for consistent 0DTE strategies
                case "mixed":
                    return 75.0; // Good with proper adjustments
                case "trending":
                    return 65.0; // Manageable with directional bias
                case "volatile":
                    return 45.0; // Challenging but tradeable
                case "crisis":
                    return 15.0; // High risk, avoid unless exceptional setup
                default:
                    return 50.0; // Unknown regime, moderate caution
            }
        }

        private double AnalyzeTrendStability(MarketConditions conditions)
        {
            var trendScore = Math.Abs(conditions.TrendScore);
            
            // Favor moderate trends for iron condor strategies
            if (trendScore <= 0.3)
                return 90.0; // Very stable, ideal for neutral strategies
            else if (trendScore <= 0.5)
                return 80.0; // Mild trend, good for adjusted strategies
            else if (trendScore <= 0.7)
                return 65.0; // Moderate trend, requires bias adjustment
            else if (trendScore <= 1.0)
                return 45.0; // Strong trend, challenging for neutral strategies
            else
                return 25.0; // Extreme trend, high directional risk
        }

        private double AnalyzeLiquidityConditions(MarketConditions conditions)
        {
            // Estimate liquidity based on time and market conditions
            var hour = conditions.Date.Hour;
            var minute = conditions.Date.Minute;
            
            var timeScore = 50.0;
            
            // Peak liquidity hours
            if ((hour >= 9 && hour <= 11) || (hour >= 13 && hour <= 15))
                timeScore = 90.0;
            else if ((hour >= 11 && hour < 13) || hour == 15)
                timeScore = 75.0;
            else
                timeScore = 40.0; // Low liquidity periods
            
            // VIX impact on liquidity
            var vixAdjustment = conditions.VIX > 30 ? 0.9 : 1.0; // High vol reduces effective liquidity
            
            // Market regime impact
            var regimeAdjustment = conditions.MarketRegime == "Crisis" ? 0.7 : 1.0;
            
            return timeScore * vixAdjustment * regimeAdjustment;
        }

        private double AnalyzeTimeOptimality(MarketConditions conditions)
        {
            var hour = conditions.Date.Hour;
            var minute = conditions.Date.Minute;
            var dayOfWeek = conditions.Date.DayOfWeek;
            
            var score = 50.0;
            
            // Optimal hours for 0DTE trading
            if (hour == 10 || hour == 14)
                score = 95.0; // Peak performance hours
            else if (hour == 9 || hour == 11 || hour == 13 || hour == 15)
                score = 80.0; // Good performance hours
            else if (hour >= 9 && hour <= 15)
                score = 65.0; // Acceptable hours
            else
                score = 20.0; // Poor timing
            
            // Minute-level optimization (6-minute alignment)
            if (minute % 6 == 0)
                score *= 1.1; // Boost for 6-minute intervals
            
            // Day of week adjustments
            if (dayOfWeek == DayOfWeek.Tuesday || dayOfWeek == DayOfWeek.Wednesday || dayOfWeek == DayOfWeek.Thursday)
                score *= 1.05; // Mid-week optimal
            else if (dayOfWeek == DayOfWeek.Monday || dayOfWeek == DayOfWeek.Friday)
                score *= 0.95; // Start/end week adjustments
            
            return Math.Min(score, 100.0);
        }

        private double AnalyzeOptionsSpecificFactors(MarketConditions conditions)
        {
            var score = 50.0;
            
            // Implied volatility considerations
            var vix = conditions.VIX;
            if (vix >= 18 && vix <= 28)
                score += 30.0; // Good IV for premium collection
            else if (vix >= 15 && vix < 18)
                score += 20.0; // Adequate IV
            else if (vix > 28 && vix <= 35)
                score += 15.0; // High IV but manageable
            else
                score += 5.0; // Suboptimal IV environment
            
            // Time decay favorability (0DTE specific)
            var hour = conditions.Date.Hour;
            if (hour >= 14)
                score += 15.0; // Accelerated theta decay in final hours
            else if (hour >= 11)
                score += 10.0; // Good theta decay
            else
                score += 5.0; // Moderate theta impact
            
            // Underlying price stability
            var trendStability = 1.0 - Math.Min(Math.Abs(conditions.TrendScore), 1.0);
            score += trendStability * 20.0; // Reward stability for neutral strategies
            
            return Math.Min(score, 100.0);
        }

        private double AnalyzeRiskEnvironment(MarketConditions conditions)
        {
            var score = 70.0; // Base moderate score
            
            // Economic event risk (simplified)
            var hour = conditions.Date.Hour;
            if (hour == 10 && conditions.Date.Minute == 0) // Potential economic release
                score -= 20.0;
            
            // Market close risk
            if (hour >= 15 && hour <= 16)
                score -= 10.0; // Elevated pin risk near close
            
            // Volatility risk
            if (conditions.VIX > 35)
                score -= 25.0; // High volatility environment
            else if (conditions.VIX < 12)
                score -= 15.0; // Ultra-low vol (complacency risk)
            
            // Regime-specific risks
            switch (conditions.MarketRegime.ToLower())
            {
                case "crisis":
                    score -= 40.0;
                    break;
                case "volatile":
                    score -= 20.0;
                    break;
                case "calm":
                    score += 15.0;
                    break;
            }
            
            return Math.Max(score, 0.0);
        }

        private double CalculateCompositeScore(Dictionary<string, double> scores)
        {
            var weightedScore = 0.0;
            
            foreach (var score in scores)
            {
                if (_conditionWeights.ContainsKey(score.Key))
                {
                    weightedScore += score.Value * _conditionWeights[score.Key];
                }
            }
            
            return Math.Round(weightedScore, 1);
        }

        private double ApplyTimingOptimizations(double baseScore, MarketConditions conditions)
        {
            var adjustedScore = baseScore;
            
            // Time-of-day momentum
            var hour = conditions.Date.Hour;
            if (hour == 10 || hour == 14) // Optimal execution windows
                adjustedScore *= 1.05;
            else if (hour == 9 || hour == 15) // Good execution windows
                adjustedScore *= 1.02;
            
            // Day-of-week patterns
            var dayOfWeek = conditions.Date.DayOfWeek;
            if (dayOfWeek == DayOfWeek.Wednesday) // Mid-week stability
                adjustedScore *= 1.03;
            else if (dayOfWeek == DayOfWeek.Friday) // Expiration day effects
                adjustedScore *= 0.98;
            
            return Math.Min(adjustedScore, 100.0);
        }

        private double ApplyHistoricalPatternBoost(double baseScore, MarketConditions conditions)
        {
            // Simplified pattern recognition
            var patternBoost = 0.0;
            
            // Look for similar historical conditions
            var similarPatterns = _historicalPatterns
                .Where(p => Math.Abs(p.VIX - conditions.VIX) < 5 &&
                           p.MarketRegime == conditions.MarketRegime)
                .ToList();
            
            if (similarPatterns.Any())
            {
                var avgSuccess = similarPatterns.Average(p => p.SuccessRate);
                if (avgSuccess > 0.85)
                    patternBoost = 5.0; // Boost for historically successful patterns
                else if (avgSuccess < 0.60)
                    patternBoost = -5.0; // Penalty for historically poor patterns
            }
            
            return Math.Max(0.0, Math.Min(100.0, baseScore + patternBoost));
        }

        private OptimalQuality ClassifyQualityLevel(double score)
        {
            if (score >= EXCELLENT_THRESHOLD)
                return OptimalQuality.Exceptional;
            else if (score >= FAVORABLE_THRESHOLD)
                return OptimalQuality.Optimal;
            else if (score >= ADEQUATE_THRESHOLD)
                return OptimalQuality.Favorable;
            else if (score >= POOR_THRESHOLD)
                return OptimalQuality.Adequate;
            else
                return OptimalQuality.Poor;
        }

        private string GenerateQualityReason(OptimalQuality quality, Dictionary<string, double> scores, MarketConditions conditions)
        {
            var topFactors = scores.OrderByDescending(s => s.Value).Take(3).ToList();
            var bottomFactors = scores.OrderBy(s => s.Value).Take(2).ToList();
            
            switch (quality)
            {
                case OptimalQuality.Exceptional:
                    return $"Exceptional conditions: {topFactors[0].Key}({topFactors[0].Value:F0}), {topFactors[1].Key}({topFactors[1].Value:F0}), VIX={conditions.VIX:F1}";
                
                case OptimalQuality.Optimal:
                    return $"Optimal setup: {topFactors[0].Key}({topFactors[0].Value:F0}), {topFactors[1].Key}({topFactors[1].Value:F0}), regime={conditions.MarketRegime}";
                
                case OptimalQuality.Favorable:
                    return $"Favorable conditions: {topFactors[0].Key}({topFactors[0].Value:F0}), minor concerns in {bottomFactors[0].Key}";
                
                case OptimalQuality.Adequate:
                    return $"Adequate setup: {topFactors[0].Key}({topFactors[0].Value:F0}), watch {bottomFactors[0].Key}({bottomFactors[0].Value:F0})";
                
                case OptimalQuality.Poor:
                    return $"Poor conditions: {bottomFactors[0].Key}({bottomFactors[0].Value:F0}), {bottomFactors[1].Key}({bottomFactors[1].Value:F0})";
                
                default:
                    return "Unknown quality classification";
            }
        }

        private Dictionary<string, double> InitializeConditionWeights()
        {
            return new Dictionary<string, double>
            {
                { "Volatility", 0.25 },        // 25% - Most important for 0DTE
                { "MarketRegime", 0.20 },      // 20% - Critical for strategy selection
                { "TrendStability", 0.15 },    // 15% - Important for neutral strategies
                { "Liquidity", 0.15 },         // 15% - Execution quality
                { "TimeOfDay", 0.10 },         // 10% - Timing optimization
                { "OptionsFactors", 0.10 },    // 10% - Options-specific considerations
                { "RiskEnvironment", 0.05 }    // 5% - Overall risk assessment
            };
        }

        public void AddHistoricalPattern(MarketConditions conditions, bool wasSuccessful)
        {
            var pattern = new MarketPattern
            {
                VIX = conditions.VIX,
                MarketRegime = conditions.MarketRegime,
                TrendScore = conditions.TrendScore,
                TimeOfDay = conditions.Date.TimeOfDay,
                WasSuccessful = wasSuccessful,
                Timestamp = conditions.Date
            };
            
            _historicalPatterns.Add(pattern);
            
            // Maintain rolling window of patterns
            if (_historicalPatterns.Count > 1000)
            {
                _historicalPatterns.RemoveRange(0, 200);
            }
            
            // Update success rates
            UpdatePatternSuccessRates();
        }

        private void UpdatePatternSuccessRates()
        {
            var regimeGroups = _historicalPatterns.GroupBy(p => p.MarketRegime);
            
            foreach (var group in regimeGroups)
            {
                var successRate = group.Count(p => p.WasSuccessful) / (double)group.Count();
                
                foreach (var pattern in group)
                {
                    pattern.SuccessRate = successRate;
                }
            }
        }
    }

    // Supporting classes
    public class MarketPattern
    {
        public double VIX { get; set; }
        public string MarketRegime { get; set; } = "";
        public double TrendScore { get; set; }
        public TimeSpan TimeOfDay { get; set; }
        public bool WasSuccessful { get; set; }
        public DateTime Timestamp { get; set; }
        public double SuccessRate { get; set; }
    }

}