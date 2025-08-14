using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ODTE.Optimization.Core;

namespace ODTE.Optimization.ML
{
    public class StrategyLearner
    {
        private readonly List<LearningRecord> _learningHistory;
        private readonly FeatureExtractor _featureExtractor;
        private readonly PatternRecognizer _patternRecognizer;
        private readonly StrategyPredictor _predictor;
        
        public StrategyLearner()
        {
            _learningHistory = new List<LearningRecord>();
            _featureExtractor = new FeatureExtractor();
            _patternRecognizer = new PatternRecognizer();
            _predictor = new StrategyPredictor();
        }
        
        public async Task<StrategyVersion> ImproveStrategyAsync(
            StrategyVersion currentStrategy,
            List<TradeResult> historicalTrades,
            MarketContext marketContext)
        {
            // Extract features from historical performance
            var features = _featureExtractor.ExtractFeatures(historicalTrades, marketContext);
            
            // Identify patterns in winning vs losing trades
            var patterns = _patternRecognizer.IdentifyPatterns(features);
            
            // Learn from patterns to suggest improvements
            var improvements = await LearnFromPatternsAsync(patterns, currentStrategy);
            
            // Apply improvements to create new strategy version
            var improvedStrategy = ApplyImprovements(currentStrategy, improvements);
            
            // Record learning for future reference
            RecordLearning(currentStrategy, improvedStrategy, patterns);
            
            return improvedStrategy;
        }
        
        private async Task<StrategyImprovements> LearnFromPatternsAsync(
            TradingPatterns patterns,
            StrategyVersion currentStrategy)
        {
            var improvements = new StrategyImprovements();
            
            // Analyze time-of-day patterns
            if (patterns.TimePatterns.Any())
            {
                var bestTimes = patterns.TimePatterns
                    .Where(t => t.WinRate > 0.6)
                    .OrderByDescending(t => t.ExpectedValue)
                    .ToList();
                
                if (bestTimes.Any())
                {
                    improvements.SuggestedEntryStart = bestTimes.First().StartTime;
                    improvements.SuggestedEntryEnd = bestTimes.First().EndTime;
                }
            }
            
            // Analyze volatility patterns
            if (patterns.VolatilityPatterns.Any())
            {
                var optimalVol = patterns.VolatilityPatterns
                    .OrderByDescending(v => v.ProfitFactor)
                    .First();
                
                improvements.SuggestedMinATR = optimalVol.MinATR;
                improvements.SuggestedMaxATR = optimalVol.MaxATR;
            }
            
            // Analyze strike selection patterns
            if (patterns.StrikePatterns.Any())
            {
                var bestStrikes = patterns.StrikePatterns
                    .Where(s => s.WinRate > currentStrategy.Performance?.WinRate)
                    .OrderByDescending(s => s.ExpectedValue)
                    .FirstOrDefault();
                
                if (bestStrikes != null)
                {
                    improvements.SuggestedDelta = bestStrikes.AverageDelta;
                    improvements.SuggestedStrikeOffset = bestStrikes.OptimalOffset;
                }
            }
            
            // Analyze exit patterns
            if (patterns.ExitPatterns.Any())
            {
                var optimalExits = AnalyzeExitPatterns(patterns.ExitPatterns);
                improvements.SuggestedStopLoss = optimalExits.StopLoss;
                improvements.SuggestedProfitTarget = optimalExits.ProfitTarget;
            }
            
            // ML-based predictions
            var mlPrediction = await _predictor.PredictOptimalParametersAsync(
                currentStrategy.Parameters,
                patterns,
                _learningHistory);
            
            improvements.MLSuggestedParameters = mlPrediction;
            
            return improvements;
        }
        
        private StrategyVersion ApplyImprovements(
            StrategyVersion current,
            StrategyImprovements improvements)
        {
            var improved = new StrategyVersion
            {
                StrategyName = current.StrategyName,
                Version = GenerateNewVersion(current.Version),
                CreatedAt = DateTime.Now,
                ParentVersion = current.Version,
                Generation = current.Generation + 1,
                Parameters = new StrategyParameters
                {
                    // Apply time improvements
                    EntryStartTime = improvements.SuggestedEntryStart ?? current.Parameters.EntryStartTime,
                    EntryEndTime = improvements.SuggestedEntryEnd ?? current.Parameters.EntryEndTime,
                    
                    // Apply volatility improvements
                    MinATR = improvements.SuggestedMinATR ?? current.Parameters.MinATR,
                    MaxATR = improvements.SuggestedMaxATR ?? current.Parameters.MaxATR,
                    UseATRFilter = improvements.SuggestedMinATR.HasValue,
                    
                    // Apply strike selection improvements
                    MaxDelta = improvements.SuggestedDelta ?? current.Parameters.MaxDelta,
                    StrikeOffset = improvements.SuggestedStrikeOffset ?? current.Parameters.StrikeOffset,
                    
                    // Apply exit improvements
                    StopLossPercent = improvements.SuggestedStopLoss ?? current.Parameters.StopLossPercent,
                    ProfitTargetPercent = improvements.SuggestedProfitTarget ?? current.Parameters.ProfitTargetPercent,
                    
                    // Apply ML suggestions if confidence is high
                    MinPremium = improvements.MLSuggestedParameters?.MinPremium ?? current.Parameters.MinPremium,
                    MinIVRank = improvements.MLSuggestedParameters?.MinIVRank ?? current.Parameters.MinIVRank,
                    DeltaExitThreshold = improvements.MLSuggestedParameters?.DeltaExit ?? current.Parameters.DeltaExitThreshold,
                    
                    // Keep other parameters
                    OpeningRangeMinutes = current.Parameters.OpeningRangeMinutes,
                    OpeningRangeBreakoutThreshold = current.Parameters.OpeningRangeBreakoutThreshold,
                    MaxPositionsPerSide = current.Parameters.MaxPositionsPerSide,
                    AllocationPerTrade = current.Parameters.AllocationPerTrade,
                    ForceCloseTime = current.Parameters.ForceCloseTime,
                    UseVWAPFilter = current.Parameters.UseVWAPFilter
                }
            };
            
            return improved;
        }
        
        private string GenerateNewVersion(string currentVersion)
        {
            // Parse current version (e.g., "1.2.3" -> "1.2.4")
            var parts = currentVersion.Split('.');
            if (parts.Length >= 3)
            {
                if (int.TryParse(parts[2], out int patch))
                {
                    parts[2] = (patch + 1).ToString();
                    return string.Join(".", parts);
                }
            }
            
            return $"{currentVersion}.ML{DateTime.Now:yyyyMMddHHmm}";
        }
        
        private void RecordLearning(
            StrategyVersion original,
            StrategyVersion improved,
            TradingPatterns patterns)
        {
            _learningHistory.Add(new LearningRecord
            {
                Timestamp = DateTime.Now,
                OriginalVersion = original.Version,
                ImprovedVersion = improved.Version,
                PatternsIdentified = patterns,
                ImprovementReason = GenerateImprovementReason(patterns)
            });
        }
        
        private string GenerateImprovementReason(TradingPatterns patterns)
        {
            var reasons = new List<string>();
            
            if (patterns.TimePatterns.Any(t => t.WinRate > 0.6))
                reasons.Add("Optimized entry times based on historical performance");
            
            if (patterns.VolatilityPatterns.Any())
                reasons.Add("Adjusted volatility filters for market conditions");
            
            if (patterns.StrikePatterns.Any())
                reasons.Add("Refined strike selection criteria");
            
            if (patterns.ExitPatterns.Any())
                reasons.Add("Improved exit strategies");
            
            return string.Join("; ", reasons);
        }
        
        private OptimalExits AnalyzeExitPatterns(List<ExitPattern> patterns)
        {
            // Group by exit reason and analyze performance
            var stopLossExits = patterns.Where(p => p.ExitReason == "StopLoss").ToList();
            var profitTargetExits = patterns.Where(p => p.ExitReason == "ProfitTarget").ToList();
            var deltaExits = patterns.Where(p => p.ExitReason.Contains("Delta")).ToList();
            
            // Find optimal stop loss
            double optimalStopLoss = 200; // Default
            if (stopLossExits.Any())
            {
                // Find the stop loss level that minimizes average loss
                var avgLoss = stopLossExits.Average(e => e.PnL);
                if (avgLoss < -150)
                    optimalStopLoss = 150; // Tighter stop
                else if (avgLoss < -250)
                    optimalStopLoss = 300; // Wider stop
            }
            
            // Find optimal profit target
            double optimalProfitTarget = 50; // Default
            if (profitTargetExits.Any())
            {
                var avgProfit = profitTargetExits.Average(e => e.PnL);
                if (avgProfit > 40)
                    optimalProfitTarget = 60; // Higher target
                else if (avgProfit < 30)
                    optimalProfitTarget = 40; // Lower target
            }
            
            return new OptimalExits
            {
                StopLoss = optimalStopLoss,
                ProfitTarget = optimalProfitTarget
            };
        }
    }
    
    public class FeatureExtractor
    {
        public TradingFeatures ExtractFeatures(List<TradeResult> trades, MarketContext context)
        {
            return new TradingFeatures
            {
                TimeOfDayDistribution = ExtractTimeDistribution(trades),
                VolatilityAtEntry = ExtractVolatilityFeatures(trades, context),
                StrikeSelectionMetrics = ExtractStrikeMetrics(trades),
                ExitReasonDistribution = ExtractExitDistribution(trades),
                MarketRegimeIndicators = ExtractMarketRegime(trades, context)
            };
        }
        
        private Dictionary<int, double> ExtractTimeDistribution(List<TradeResult> trades)
        {
            return trades.GroupBy(t => t.EntryTime.Hour)
                .ToDictionary(g => g.Key, g => g.Average(t => t.PnL));
        }
        
        private List<double> ExtractVolatilityFeatures(List<TradeResult> trades, MarketContext context)
        {
            return trades.Select(t => context.GetATRAtTime(t.EntryTime)).ToList();
        }
        
        private StrikeMetrics ExtractStrikeMetrics(List<TradeResult> trades)
        {
            return new StrikeMetrics
            {
                AverageDelta = trades.Average(t => t.EntryDelta),
                AverageStrikeDistance = trades.Average(t => t.StrikeDistance),
                WinRateByDelta = trades.GroupBy(t => Math.Round(t.EntryDelta, 2))
                    .ToDictionary(g => g.Key, g => g.Count(t => t.PnL > 0) / (double)g.Count())
            };
        }
        
        private Dictionary<string, int> ExtractExitDistribution(List<TradeResult> trades)
        {
            return trades.GroupBy(t => t.ExitReason)
                .ToDictionary(g => g.Key, g => g.Count());
        }
        
        private MarketRegimeData ExtractMarketRegime(List<TradeResult> trades, MarketContext context)
        {
            return new MarketRegimeData
            {
                TrendingDays = context.TrendingDays,
                RangeBoundDays = context.RangeBoundDays,
                HighVolatilityDays = context.HighVolatilityDays
            };
        }
    }
    
    public class PatternRecognizer
    {
        public TradingPatterns IdentifyPatterns(TradingFeatures features)
        {
            return new TradingPatterns
            {
                TimePatterns = IdentifyTimePatterns(features.TimeOfDayDistribution),
                VolatilityPatterns = IdentifyVolatilityPatterns(features.VolatilityAtEntry),
                StrikePatterns = IdentifyStrikePatterns(features.StrikeSelectionMetrics),
                ExitPatterns = IdentifyExitPatterns(features.ExitReasonDistribution)
            };
        }
        
        private List<TimePattern> IdentifyTimePatterns(Dictionary<int, double> distribution)
        {
            var patterns = new List<TimePattern>();
            
            foreach (var kvp in distribution.OrderByDescending(k => k.Value))
            {
                patterns.Add(new TimePattern
                {
                    StartTime = new TimeSpan(kvp.Key, 0, 0),
                    EndTime = new TimeSpan(kvp.Key + 1, 0, 0),
                    WinRate = kvp.Value > 0 ? 0.6 : 0.4, // Simplified
                    ExpectedValue = kvp.Value
                });
            }
            
            return patterns;
        }
        
        private List<VolatilityPattern> IdentifyVolatilityPatterns(List<double> volatilities)
        {
            if (!volatilities.Any()) return new List<VolatilityPattern>();
            
            var min = volatilities.Min();
            var max = volatilities.Max();
            var avg = volatilities.Average();
            
            return new List<VolatilityPattern>
            {
                new VolatilityPattern
                {
                    MinATR = min,
                    MaxATR = avg,
                    ProfitFactor = 1.5 // Placeholder
                },
                new VolatilityPattern
                {
                    MinATR = avg,
                    MaxATR = max,
                    ProfitFactor = 1.2 // Placeholder
                }
            };
        }
        
        private List<StrikePattern> IdentifyStrikePatterns(StrikeMetrics metrics)
        {
            return metrics.WinRateByDelta
                .Select(kvp => new StrikePattern
                {
                    AverageDelta = kvp.Key,
                    OptimalOffset = (int)(kvp.Key * 100),
                    WinRate = kvp.Value,
                    ExpectedValue = kvp.Value * 50 - (1 - kvp.Value) * 100 // Simplified EV
                })
                .OrderByDescending(p => p.ExpectedValue)
                .ToList();
        }
        
        private List<ExitPattern> IdentifyExitPatterns(Dictionary<string, int> distribution)
        {
            return distribution.Select(kvp => new ExitPattern
            {
                ExitReason = kvp.Key,
                Frequency = kvp.Value,
                PnL = 0 // Would need actual PnL data per exit type
            }).ToList();
        }
    }
    
    public class StrategyPredictor
    {
        public async Task<MLParameters> PredictOptimalParametersAsync(
            StrategyParameters current,
            TradingPatterns patterns,
            List<LearningRecord> history)
        {
            // Simple prediction logic - in reality would use ML model
            var prediction = new MLParameters
            {
                MinPremium = current.MinPremium,
                MinIVRank = current.MinIVRank,
                DeltaExit = current.DeltaExitThreshold
            };
            
            // Adjust based on patterns
            if (patterns.TimePatterns.Any(t => t.WinRate > 0.7))
            {
                prediction.MinPremium *= 0.9; // Lower premium requirement in good times
            }
            
            if (patterns.VolatilityPatterns.Any(v => v.ProfitFactor > 2))
            {
                prediction.MinIVRank *= 0.85; // Lower IV requirement when profitable
            }
            
            return await Task.FromResult(prediction);
        }
    }
    
    // Supporting classes
    public class LearningRecord
    {
        public DateTime Timestamp { get; set; }
        public string OriginalVersion { get; set; }
        public string ImprovedVersion { get; set; }
        public TradingPatterns PatternsIdentified { get; set; }
        public string ImprovementReason { get; set; }
    }
    
    public class TradeResult
    {
        public DateTime EntryTime { get; set; }
        public DateTime ExitTime { get; set; }
        public double PnL { get; set; }
        public double EntryDelta { get; set; }
        public double StrikeDistance { get; set; }
        public string ExitReason { get; set; }
    }
    
    public class MarketContext
    {
        public int TrendingDays { get; set; }
        public int RangeBoundDays { get; set; }
        public int HighVolatilityDays { get; set; }
        
        public double GetATRAtTime(DateTime time)
        {
            // Placeholder - would fetch actual ATR
            return 5.0 + new Random(time.GetHashCode()).NextDouble() * 10;
        }
    }
    
    public class TradingFeatures
    {
        public Dictionary<int, double> TimeOfDayDistribution { get; set; }
        public List<double> VolatilityAtEntry { get; set; }
        public StrikeMetrics StrikeSelectionMetrics { get; set; }
        public Dictionary<string, int> ExitReasonDistribution { get; set; }
        public MarketRegimeData MarketRegimeIndicators { get; set; }
    }
    
    public class StrikeMetrics
    {
        public double AverageDelta { get; set; }
        public double AverageStrikeDistance { get; set; }
        public Dictionary<double, double> WinRateByDelta { get; set; }
    }
    
    public class MarketRegimeData
    {
        public int TrendingDays { get; set; }
        public int RangeBoundDays { get; set; }
        public int HighVolatilityDays { get; set; }
    }
    
    public class TradingPatterns
    {
        public List<TimePattern> TimePatterns { get; set; }
        public List<VolatilityPattern> VolatilityPatterns { get; set; }
        public List<StrikePattern> StrikePatterns { get; set; }
        public List<ExitPattern> ExitPatterns { get; set; }
    }
    
    public class TimePattern
    {
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public double WinRate { get; set; }
        public double ExpectedValue { get; set; }
    }
    
    public class VolatilityPattern
    {
        public double MinATR { get; set; }
        public double MaxATR { get; set; }
        public double ProfitFactor { get; set; }
    }
    
    public class StrikePattern
    {
        public double AverageDelta { get; set; }
        public int OptimalOffset { get; set; }
        public double WinRate { get; set; }
        public double ExpectedValue { get; set; }
    }
    
    public class ExitPattern
    {
        public string ExitReason { get; set; }
        public int Frequency { get; set; }
        public double PnL { get; set; }
    }
    
    public class StrategyImprovements
    {
        public TimeSpan? SuggestedEntryStart { get; set; }
        public TimeSpan? SuggestedEntryEnd { get; set; }
        public double? SuggestedMinATR { get; set; }
        public double? SuggestedMaxATR { get; set; }
        public double? SuggestedDelta { get; set; }
        public int? SuggestedStrikeOffset { get; set; }
        public double? SuggestedStopLoss { get; set; }
        public double? SuggestedProfitTarget { get; set; }
        public MLParameters MLSuggestedParameters { get; set; }
    }
    
    public class MLParameters
    {
        public double MinPremium { get; set; }
        public double MinIVRank { get; set; }
        public double DeltaExit { get; set; }
    }
    
    public class OptimalExits
    {
        public double StopLoss { get; set; }
        public double ProfitTarget { get; set; }
    }
}