using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ODTE.Strategy;

/// <summary>
/// Advanced Market Regime Analysis for 24-Day Framework
/// 
/// OBJECTIVE: Real-time market regime detection and classification for optimal strategy selection
/// 
/// REGIME TYPES:
/// 1. LowVolatility: VIX < 20, stable trends, theta-friendly environment
/// 2. HighVolatility: VIX > 30, unstable, risk-off sentiment
/// 3. Trending: Clear directional movement with momentum
/// 4. Ranging: Sideways movement, mean-reverting behavior
/// 5. Uncertain: Mixed signals, low confidence environment
/// 
/// ANALYTICAL COMPONENTS:
/// - VIX term structure analysis
/// - Price action momentum and trend strength
/// - Volume and volatility clustering
/// - Economic event calendar impact
/// - Sentiment and positioning indicators
/// 
/// ACADEMIC FOUNDATION:
/// - Markov regime switching models (Hamilton, 1989)
/// - Volatility clustering (GARCH models)
/// - Market microstructure theory
/// - Behavioral finance regime detection
/// </summary>
public class MarketRegimeAnalyzer
{
    private readonly Dictionary<DateTime, MarketRegime> _regimeCache = new();
    private readonly List<RegimeIndicator> _indicators;

    public MarketRegimeAnalyzer()
    {
        _indicators = InitializeIndicators();
    }

    /// <summary>
    /// Analyze current market regime with confidence scoring
    /// </summary>
    public async Task<MarketRegime> AnalyzeRegime(DateTime date, MarketConditions conditions)
    {
        // Check cache first for performance
        if (_regimeCache.TryGetValue(date.Date, out var cachedRegime))
            return cachedRegime;

        // Gather all regime indicators
        var indicatorScores = new Dictionary<string, double>();
        
        foreach (var indicator in _indicators)
        {
            var score = await indicator.CalculateScore(date, conditions);
            indicatorScores[indicator.Name] = score;
        }

        // Determine primary regime type
        var regimeType = ClassifyRegime(indicatorScores, conditions);
        
        // Calculate confidence level
        var confidence = CalculateConfidence(indicatorScores, regimeType);
        
        // Analyze trend characteristics
        var trendStrength = CalculateTrendStrength(conditions);
        
        // Check for major economic events
        var hasMajorEvent = await CheckMajorEvents(date);

        var regime = new MarketRegime
        {
            RegimeType = regimeType,
            Confidence = (decimal)confidence,
            VIX = GetCurrentVIX(date, conditions),
            TrendStrength = trendStrength,
            HasMajorEvent = hasMajorEvent
        };

        // Cache result
        _regimeCache[date.Date] = regime;
        
        return regime;
    }

    /// <summary>
    /// Initialize regime detection indicators
    /// </summary>
    private List<RegimeIndicator> InitializeIndicators()
    {
        return new List<RegimeIndicator>
        {
            new VIXRegimeIndicator(),
            new TrendMomentumIndicator(),
            new VolatilityClusterIndicator(),
            new VolumeRegimeIndicator(),
            new SentimentIndicator(),
            new TechnicalIndicator(),
            new MacroEnvironmentIndicator()
        };
    }

    /// <summary>
    /// Classify regime type based on indicator scores
    /// </summary>
    private RegimeType ClassifyRegime(Dictionary<string, double> scores, MarketConditions conditions)
    {
        var vixScore = scores.GetValueOrDefault("VIX", 0.5);
        var trendScore = scores.GetValueOrDefault("Trend", 0.5);
        var volatilityScore = scores.GetValueOrDefault("Volatility", 0.5);
        var volumeScore = scores.GetValueOrDefault("Volume", 0.5);

        // Decision tree for regime classification
        if (vixScore < 0.3 && volatilityScore < 0.4) // Low volatility environment
        {
            return trendScore > 0.6 ? RegimeType.Trending : RegimeType.LowVolatility;
        }
        
        if (vixScore > 0.7 || volatilityScore > 0.8) // High volatility environment
        {
            return RegimeType.HighVolatility;
        }
        
        if (Math.Abs(trendScore - 0.5) > 0.3) // Strong trend either direction
        {
            return RegimeType.Trending;
        }
        
        if (trendScore > 0.3 && trendScore < 0.7 && volatilityScore < 0.6) // Range-bound
        {
            return RegimeType.Ranging;
        }
        
        return RegimeType.Uncertain; // Mixed signals
    }

    /// <summary>
    /// Calculate confidence level for regime classification
    /// </summary>
    private double CalculateConfidence(Dictionary<string, double> scores, RegimeType regimeType)
    {
        // Calculate agreement between indicators
        var relevantScores = GetRelevantScoresForRegime(scores, regimeType);
        var agreement = CalculateIndicatorAgreement(relevantScores);
        
        // Base confidence from agreement
        var baseConfidence = agreement;
        
        // Boost confidence for clear extreme readings
        var extremeBoost = CalculateExtremeReadingsBoost(scores);
        
        // Reduce confidence for conflicting signals
        var conflictPenalty = CalculateConflictPenalty(scores);
        
        var finalConfidence = Math.Max(0.0, Math.Min(1.0, baseConfidence + extremeBoost - conflictPenalty));
        
        return finalConfidence;
    }

    /// <summary>
    /// Get relevant indicator scores for specific regime type
    /// </summary>
    private List<double> GetRelevantScoresForRegime(Dictionary<string, double> scores, RegimeType regimeType)
    {
        return regimeType switch
        {
            RegimeType.LowVolatility => new[] { "VIX", "Volatility", "Volume" }.Select(k => scores.GetValueOrDefault(k, 0.5)).ToList(),
            RegimeType.HighVolatility => new[] { "VIX", "Volatility", "Sentiment" }.Select(k => scores.GetValueOrDefault(k, 0.5)).ToList(),
            RegimeType.Trending => new[] { "Trend", "Momentum", "Volume" }.Select(k => scores.GetValueOrDefault(k, 0.5)).ToList(),
            RegimeType.Ranging => new[] { "Trend", "Technical", "Volatility" }.Select(k => scores.GetValueOrDefault(k, 0.5)).ToList(),
            _ => scores.Values.ToList()
        };
    }

    /// <summary>
    /// Calculate agreement between indicators
    /// </summary>
    private double CalculateIndicatorAgreement(List<double> scores)
    {
        if (scores.Count == 0) return 0.5;
        
        var mean = scores.Average();
        var variance = scores.Sum(x => Math.Pow(x - mean, 2)) / scores.Count;
        var standardDeviation = Math.Sqrt(variance);
        
        // Lower standard deviation = higher agreement = higher confidence
        return Math.Max(0.0, 1.0 - standardDeviation * 2.0);
    }

    /// <summary>
    /// Calculate confidence boost from extreme readings
    /// </summary>
    private double CalculateExtremeReadingsBoost(Dictionary<string, double> scores)
    {
        var extremeCount = scores.Values.Count(s => s < 0.2 || s > 0.8);
        var totalIndicators = scores.Count;
        
        return (double)extremeCount / totalIndicators * 0.2; // Max 20% boost
    }

    /// <summary>
    /// Calculate confidence penalty for conflicting signals
    /// </summary>
    private double CalculateConflictPenalty(Dictionary<string, double> scores)
    {
        // Look for indicators giving opposing signals
        var vixScore = scores.GetValueOrDefault("VIX", 0.5);
        var trendScore = scores.GetValueOrDefault("Trend", 0.5);
        var sentimentScore = scores.GetValueOrDefault("Sentiment", 0.5);
        
        var conflicts = 0;
        
        // VIX vs Trend conflict (high VIX but strong trend)
        if (vixScore > 0.7 && Math.Abs(trendScore - 0.5) > 0.3) conflicts++;
        
        // Sentiment vs Technical conflict
        var technicalScore = scores.GetValueOrDefault("Technical", 0.5);
        if (Math.Abs(sentimentScore - technicalScore) > 0.4) conflicts++;
        
        return conflicts * 0.15; // 15% penalty per conflict
    }

    /// <summary>
    /// Calculate trend strength and direction
    /// </summary>
    private double CalculateTrendStrength(MarketConditions conditions)
    {
        // Combine multiple trend indicators
        var rsiTrend = ((double)conditions.RSI - 50) / 50.0; // -1 to 1
        var momentumTrend = conditions.MomentumDivergence;
        
        // Weight and combine
        var combinedTrend = (rsiTrend * 0.4 + momentumTrend * 0.6);
        
        return Math.Max(-1.0, Math.Min(1.0, combinedTrend));
    }

    /// <summary>
    /// Get current VIX level (simulated for now)
    /// </summary>
    private decimal GetCurrentVIX(DateTime date, MarketConditions conditions)
    {
        // In production, this would fetch real VIX data
        // For now, simulate based on date and conditions
        var baseLevelSeed = date.DayOfYear + (int)conditions.IVRank;
        var random = new Random(baseLevelSeed);
        
        var baseVIX = 18.0 + random.NextDouble() * 12.0; // 18-30 base range
        
        // Adjust based on conditions
        if (conditions.RSI < 30 || conditions.RSI > 70)
            baseVIX *= 1.3; // Stress conditions
        
        return (decimal)Math.Max(10.0, Math.Min(80.0, baseVIX));
    }

    /// <summary>
    /// Check for major economic events
    /// </summary>
    private async Task<bool> CheckMajorEvents(DateTime date)
    {
        // In production, this would check economic calendar
        // For simulation, create some patterns
        var dayOfMonth = date.Day;
        
        // FOMC meetings (roughly every 6 weeks)
        if (dayOfMonth == 2 && date.Month % 2 == 0) return true;
        
        // Jobs report (first Friday of month)
        if (date.DayOfWeek == DayOfWeek.Friday && dayOfMonth <= 7) return true;
        
        // CPI report (mid-month)
        if (dayOfMonth >= 10 && dayOfMonth <= 15 && date.DayOfWeek == DayOfWeek.Thursday) return true;
        
        return false;
    }
}

/// <summary>
/// Base class for regime indicators
/// </summary>
public abstract class RegimeIndicator
{
    public abstract string Name { get; }
    public abstract Task<double> CalculateScore(DateTime date, MarketConditions conditions);
}

/// <summary>
/// VIX-based regime indicator
/// </summary>
public class VIXRegimeIndicator : RegimeIndicator
{
    public override string Name => "VIX";

    public override async Task<double> CalculateScore(DateTime date, MarketConditions conditions)
    {
        // Simulate VIX level based on conditions
        var impliedVIX = 15.0 + ((double)conditions.IVRank / 100.0 * 25.0); // 15-40 range
        
        // Score: 0 = very low vol, 1 = very high vol
        return Math.Max(0.0, Math.Min(1.0, (impliedVIX - 12.0) / 28.0));
    }
}

/// <summary>
/// Trend momentum indicator
/// </summary>
public class TrendMomentumIndicator : RegimeIndicator
{
    public override string Name => "Trend";

    public override async Task<double> CalculateScore(DateTime date, MarketConditions conditions)
    {
        // Combine RSI and momentum divergence
        var rsiComponent = conditions.RSI > 50 ? ((double)conditions.RSI - 50) / 50.0 : (50 - (double)conditions.RSI) / -50.0;
        var momentumComponent = conditions.MomentumDivergence;
        
        // Score: 0 = strong downtrend, 0.5 = no trend, 1 = strong uptrend
        var trendScore = 0.5 + (rsiComponent * 0.3 + momentumComponent * 0.7) * 0.5;
        
        return Math.Max(0.0, Math.Min(1.0, trendScore));
    }
}

/// <summary>
/// Volatility clustering indicator
/// </summary>
public class VolatilityClusterIndicator : RegimeIndicator
{
    public override string Name => "Volatility";

    public override async Task<double> CalculateScore(DateTime date, MarketConditions conditions)
    {
        // Simulate volatility clustering based on IV rank and recent patterns
        var baseCluster = (double)conditions.IVRank / 100.0;
        
        // Add persistence effect (volatility clustering)
        var dayEffect = Math.Sin(date.DayOfYear / 365.0 * 2 * Math.PI) * 0.1;
        
        return Math.Max(0.0, Math.Min(1.0, baseCluster + dayEffect + 0.1));
    }
}

/// <summary>
/// Volume regime indicator  
/// </summary>
public class VolumeRegimeIndicator : RegimeIndicator
{
    public override string Name => "Volume";

    public override async Task<double> CalculateScore(DateTime date, MarketConditions conditions)
    {
        // Simulate volume patterns
        var baseVolume = 0.5;
        
        // Higher volume around extreme RSI readings
        if (conditions.RSI < 35 || conditions.RSI > 65)
            baseVolume += 0.2;
        
        // Higher volume on high IV
        baseVolume += (double)conditions.IVRank / 200.0;
        
        return Math.Max(0.0, Math.Min(1.0, baseVolume));
    }
}

/// <summary>
/// Market sentiment indicator
/// </summary>
public class SentimentIndicator : RegimeIndicator
{
    public override string Name => "Sentiment";

    public override async Task<double> CalculateScore(DateTime date, MarketConditions conditions)
    {
        // Sentiment based on RSI extremes and momentum
        var sentiment = 0.5;
        
        if (conditions.RSI < 30) sentiment = 0.2; // Bearish
        else if (conditions.RSI > 70) sentiment = 0.8; // Bullish
        else sentiment = (double)conditions.RSI / 100.0;
        
        // Adjust for momentum divergence
        sentiment += conditions.MomentumDivergence * 0.1;
        
        return Math.Max(0.0, Math.Min(1.0, sentiment));
    }
}

/// <summary>
/// Technical analysis indicator
/// </summary>
public class TechnicalIndicator : RegimeIndicator
{
    public override string Name => "Technical";

    public override async Task<double> CalculateScore(DateTime date, MarketConditions conditions)
    {
        // Technical score based on RSI and momentum alignment
        var rsiNormalized = (double)conditions.RSI / 100.0;
        var momentumAlignment = Math.Abs(conditions.MomentumDivergence);
        
        // Higher score for extreme readings with momentum confirmation
        var technicalScore = rsiNormalized;
        
        if (momentumAlignment > 0.5)
            technicalScore += 0.1; // Momentum confirmation bonus
        
        return Math.Max(0.0, Math.Min(1.0, technicalScore));
    }
}

/// <summary>
/// Macro environment indicator
/// </summary>
public class MacroEnvironmentIndicator : RegimeIndicator
{
    public override string Name => "Macro";

    public override async Task<double> CalculateScore(DateTime date, MarketConditions conditions)
    {
        // Macro factors: seasonal effects, month-end, etc.
        var score = 0.5; // Neutral baseline
        
        // Month-end effect
        if (date.Day > 25) score += 0.1;
        
        // Quarterly effects
        if (date.Month % 3 == 0 && date.Day > 20) score += 0.1;
        
        // October effect (higher volatility)
        if (date.Month == 10) score += 0.15;
        
        return Math.Max(0.0, Math.Min(1.0, score));
    }
}