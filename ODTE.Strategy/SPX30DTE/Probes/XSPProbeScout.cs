using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ODTE.Backtest.Core;
using ODTE.Backtest.Data;
using ODTE.Execution.Engine;
using ODTE.Execution.Models;
using ODTE.Historical.DistributedStorage;
using ODTE.Historical.Models;

namespace ODTE.Strategy.SPX30DTE.Probes
{
    public interface IProbeScout
    {
        Task<ProbeSignal> AnalyzeMarketMood(DateTime date);
        ProbeSentiment GetSentiment();
        Task<List<ProbeEntry>> GenerateProbeEntries(DateTime date, int count);
        void UpdateProbePerformance(string probeId, decimal pnl, bool isWin);
        ProbePerformanceMetrics GetPerformanceMetrics();
    }

    public class XSPProbeScout : IProbeScout
    {
        private readonly DistributedDatabaseManager _dataManager;
        private readonly RealisticFillEngine _fillEngine;
        private readonly ProbeConfiguration _config;
        private readonly Queue<ProbeResult> _recentProbes;
        private readonly Dictionary<string, ProbePosition> _activeProbes;
        
        private const int SENTIMENT_LOOKBACK = 10; // Days
        
        public XSPProbeScout(
            DistributedDatabaseManager dataManager,
            RealisticFillEngine fillEngine,
            ProbeConfiguration config)
        {
            _dataManager = dataManager;
            _fillEngine = fillEngine;
            _config = config;
            _recentProbes = new Queue<ProbeResult>();
            _activeProbes = new Dictionary<string, ProbePosition>();
        }

        public async Task<ProbeSignal> AnalyzeMarketMood(DateTime date)
        {
            // Get XSP options chain
            var xspChain = await _dataManager.GetOptionsChain("XSP", date);
            if (xspChain == null || !xspChain.Any())
            {
                return ProbeSignal.CreateInsufficient("No XSP data available");
            }

            // Get underlying price
            var xspPrice = await _dataManager.GetUnderlyingPrice("XSP", date);
            
            // Calculate market metrics
            var ivRank = CalculateIVRank(xspChain);
            var putCallRatio = CalculatePutCallRatio(xspChain);
            var skew = CalculateSkew(xspChain);
            
            // Analyze recent probe performance
            var recentWinRate = CalculateRecentWinRate();
            var avgProbeReturn = CalculateAverageReturn();
            
            // Generate signal based on multiple factors
            var signal = new ProbeSignal
            {
                Date = date,
                UnderlyingPrice = xspPrice,
                IVRank = ivRank,
                PutCallRatio = putCallRatio,
                Skew = skew,
                RecentWinRate = recentWinRate,
                AverageReturn = avgProbeReturn,
                Sentiment = DetermineSentiment(ivRank, putCallRatio, skew, recentWinRate),
                Strength = CalculateSignalStrength(ivRank, recentWinRate, skew),
                RecommendedProbes = CalculateRecommendedProbeCount(ivRank, recentWinRate)
            };

            // Check for volatility spikes
            if (ivRank > 80)
            {
                signal.Warnings.Add("High IV Rank - Consider reducing probe size");
            }
            
            if (recentWinRate < 0.40m)
            {
                signal.Warnings.Add("Low probe win rate - Market conditions unfavorable");
            }

            return signal;
        }

        public ProbeSentiment GetSentiment()
        {
            if (_recentProbes.Count < 5)
                return ProbeSentiment.Insufficient;

            var recentWinRate = CalculateRecentWinRate();
            var volatility = CalculateRecentVolatility();
            
            if (volatility > 30)
                return ProbeSentiment.Volatile;
                
            if (recentWinRate >= _config.WinRateThreshold)
                return ProbeSentiment.Bullish;
                
            if (recentWinRate < 0.40m)
                return ProbeSentiment.Bearish;
                
            return ProbeSentiment.Neutral;
        }

        public async Task<List<ProbeEntry>> GenerateProbeEntries(DateTime date, int count)
        {
            var entries = new List<ProbeEntry>();
            var xspChain = await _dataManager.GetOptionsChain("XSP", date);
            var xspPrice = await _dataManager.GetUnderlyingPrice("XSP", date);
            
            // Find optimal expiration dates (10-20 DTE)
            var targetExpirations = xspChain
                .Where(o => o.DTE >= _config.MinDTE && o.DTE <= _config.MaxDTE)
                .Select(o => o.Expiration)
                .Distinct()
                .OrderBy(e => e)
                .Take(count)
                .ToList();

            foreach (var expiration in targetExpirations)
            {
                var dte = (expiration - date).Days;
                var chainForExpiry = xspChain.Where(o => o.Expiration == expiration).ToList();
                
                // Find put spread at target delta
                var shortStrike = FindStrikeAtDelta(chainForExpiry, _config.DeltaTarget, true);
                var longStrike = shortStrike - _config.SpreadWidth;
                
                // Get quotes for the spread
                var shortPut = chainForExpiry.FirstOrDefault(o => 
                    o.Strike == shortStrike && o.OptionType == "PUT");
                var longPut = chainForExpiry.FirstOrDefault(o => 
                    o.Strike == longStrike && o.OptionType == "PUT");
                
                if (shortPut != null && longPut != null)
                {
                    var credit = shortPut.Bid - longPut.Ask;
                    var risk = _config.SpreadWidth * 100 - credit;
                    
                    // Validate credit meets minimum requirements
                    if (credit >= _config.MinCredit)
                    {
                        entries.Add(new ProbeEntry
                        {
                            Symbol = "XSP",
                            Expiration = expiration,
                            ShortStrike = shortStrike,
                            LongStrike = longStrike,
                            Credit = credit,
                            Risk = risk,
                            Quantity = 1
                        });
                    }
                }
            }

            return entries;
        }

        public void UpdateProbePerformance(string probeId, decimal pnl, bool isWin)
        {
            var result = new ProbeResult
            {
                ProbeId = probeId,
                Date = DateTime.Now,
                PnL = pnl,
                IsWin = isWin,
                ReturnPercent = _activeProbes.ContainsKey(probeId) 
                    ? pnl / _activeProbes[probeId].Risk 
                    : 0
            };
            
            _recentProbes.Enqueue(result);
            
            // Keep only recent results
            while (_recentProbes.Count > 50)
            {
                _recentProbes.Dequeue();
            }
            
            // Remove from active probes
            if (_activeProbes.ContainsKey(probeId))
            {
                _activeProbes.Remove(probeId);
            }
        }

        public ProbePerformanceMetrics GetPerformanceMetrics()
        {
            if (!_recentProbes.Any())
            {
                return new ProbePerformanceMetrics();
            }

            var wins = _recentProbes.Count(p => p.IsWin);
            var total = _recentProbes.Count;
            
            return new ProbePerformanceMetrics
            {
                TotalProbes = total,
                WinCount = wins,
                LossCount = total - wins,
                WinRate = total > 0 ? (decimal)wins / total : 0,
                AveragePnL = _recentProbes.Average(p => p.PnL),
                TotalPnL = _recentProbes.Sum(p => p.PnL),
                AverageReturn = _recentProbes.Average(p => p.ReturnPercent),
                MaxWin = _recentProbes.Any() ? _recentProbes.Max(p => p.PnL) : 0,
                MaxLoss = _recentProbes.Any() ? _recentProbes.Min(p => p.PnL) : 0,
                ConsecutiveWins = CalculateConsecutiveWins(),
                ConsecutiveLosses = CalculateConsecutiveLosses(),
                SentimentScore = CalculateSentimentScore()
            };
        }

        private decimal CalculateIVRank(List<OptionsQuote> chain)
        {
            if (!chain.Any()) return 50;
            
            var avgIV = chain.Average(o => o.ImpliedVolatility);
            // Simplified IV rank calculation (would need historical IV data for real calculation)
            return Math.Min(100, Math.Max(0, avgIV * 100));
        }

        private decimal CalculatePutCallRatio(List<OptionsQuote> chain)
        {
            var puts = chain.Where(o => o.OptionType == "PUT").Sum(o => o.Volume);
            var calls = chain.Where(o => o.OptionType == "CALL").Sum(o => o.Volume);
            
            if (calls == 0) return 1;
            return puts / (decimal)calls;
        }

        private decimal CalculateSkew(List<OptionsQuote> chain)
        {
            var atmOptions = chain.Where(o => Math.Abs(o.Delta) > 0.45m && Math.Abs(o.Delta) < 0.55m).ToList();
            var otmPuts = chain.Where(o => o.OptionType == "PUT" && o.Delta > -0.25m && o.Delta < -0.15m).ToList();
            
            if (!atmOptions.Any() || !otmPuts.Any()) return 0;
            
            var atmIV = atmOptions.Average(o => o.ImpliedVolatility);
            var otmIV = otmPuts.Average(o => o.ImpliedVolatility);
            
            return otmIV - atmIV;
        }

        private decimal CalculateRecentWinRate()
        {
            if (!_recentProbes.Any()) return 0.5m;
            
            var recent = _recentProbes.TakeLast(SENTIMENT_LOOKBACK).ToList();
            if (!recent.Any()) return 0.5m;
            
            return (decimal)recent.Count(p => p.IsWin) / recent.Count;
        }

        private decimal CalculateAverageReturn()
        {
            if (!_recentProbes.Any()) return 0;
            
            var recent = _recentProbes.TakeLast(SENTIMENT_LOOKBACK).ToList();
            if (!recent.Any()) return 0;
            
            return recent.Average(p => p.ReturnPercent);
        }

        private decimal CalculateRecentVolatility()
        {
            if (_recentProbes.Count < 5) return 20;
            
            var returns = _recentProbes.Select(p => p.ReturnPercent).ToList();
            var mean = returns.Average();
            var variance = returns.Select(r => Math.Pow((double)(r - mean), 2)).Average();
            
            return (decimal)Math.Sqrt(variance) * 100;
        }

        private ProbeSentiment DetermineSentiment(decimal ivRank, decimal putCallRatio, decimal skew, decimal winRate)
        {
            if (ivRank > 80 || putCallRatio > 2)
                return ProbeSentiment.Volatile;
                
            if (winRate >= _config.WinRateThreshold && ivRank < 50 && putCallRatio < 1.2m)
                return ProbeSentiment.Bullish;
                
            if (winRate < 0.40m || skew > 10)
                return ProbeSentiment.Bearish;
                
            if (winRate >= 0.45m && winRate < _config.WinRateThreshold)
                return ProbeSentiment.Neutral;
                
            return ProbeSentiment.Insufficient;
        }

        private decimal CalculateSignalStrength(decimal ivRank, decimal winRate, decimal skew)
        {
            var strength = 50m; // Base strength
            
            // Adjust for IV rank (lower is better for selling premium)
            if (ivRank < 30) strength += 20;
            else if (ivRank > 70) strength -= 20;
            
            // Adjust for win rate
            strength += (winRate - 0.5m) * 100;
            
            // Adjust for skew
            if (Math.Abs(skew) < 5) strength += 10;
            else if (Math.Abs(skew) > 15) strength -= 20;
            
            return Math.Min(100, Math.Max(0, strength));
        }

        private int CalculateRecommendedProbeCount(decimal ivRank, decimal winRate)
        {
            var baseCount = 2;
            
            if (winRate > 0.65m && ivRank < 40)
                return baseCount + 2; // 4 probes in favorable conditions
            
            if (winRate > 0.55m && ivRank < 60)
                return baseCount + 1; // 3 probes in normal conditions
                
            if (winRate < 0.45m || ivRank > 70)
                return Math.Max(1, baseCount - 1); // 1 probe in poor conditions
                
            return baseCount;
        }

        private decimal FindStrikeAtDelta(List<OptionsQuote> chain, decimal targetDelta, bool isPut)
        {
            var options = chain
                .Where(o => o.OptionType == (isPut ? "PUT" : "CALL"))
                .OrderBy(o => Math.Abs(Math.Abs(o.Delta) - targetDelta))
                .ToList();
                
            return options.FirstOrDefault()?.Strike ?? 0;
        }

        private int CalculateConsecutiveWins()
        {
            if (!_recentProbes.Any()) return 0;
            
            var consecutive = 0;
            foreach (var probe in _recentProbes.Reverse())
            {
                if (probe.IsWin) consecutive++;
                else break;
            }
            
            return consecutive;
        }

        private int CalculateConsecutiveLosses()
        {
            if (!_recentProbes.Any()) return 0;
            
            var consecutive = 0;
            foreach (var probe in _recentProbes.Reverse())
            {
                if (!probe.IsWin) consecutive++;
                else break;
            }
            
            return consecutive;
        }

        private decimal CalculateSentimentScore()
        {
            var metrics = GetPerformanceMetrics();
            
            // Score from -100 to +100
            var score = 0m;
            
            // Win rate component (40% weight)
            score += (metrics.WinRate - 0.5m) * 80;
            
            // Return component (40% weight)
            score += Math.Min(40, Math.Max(-40, metrics.AverageReturn * 200));
            
            // Streak component (20% weight)
            if (metrics.ConsecutiveWins > 3) score += 20;
            else if (metrics.ConsecutiveLosses > 3) score -= 20;
            
            return Math.Min(100, Math.Max(-100, score));
        }
    }

    public class ProbeSignal
    {
        public DateTime Date { get; set; }
        public decimal UnderlyingPrice { get; set; }
        public decimal IVRank { get; set; }
        public decimal PutCallRatio { get; set; }
        public decimal Skew { get; set; }
        public decimal RecentWinRate { get; set; }
        public decimal AverageReturn { get; set; }
        public ProbeSentiment Sentiment { get; set; }
        public decimal Strength { get; set; }
        public int RecommendedProbes { get; set; }
        public List<string> Warnings { get; set; } = new();
        
        public static ProbeSignal CreateInsufficient(string reason)
        {
            return new ProbeSignal
            {
                Sentiment = ProbeSentiment.Insufficient,
                Warnings = new List<string> { reason }
            };
        }
    }

    public class ProbeResult
    {
        public string ProbeId { get; set; }
        public DateTime Date { get; set; }
        public decimal PnL { get; set; }
        public bool IsWin { get; set; }
        public decimal ReturnPercent { get; set; }
    }

    public class ProbePosition
    {
        public string ProbeId { get; set; }
        public DateTime EntryDate { get; set; }
        public DateTime Expiration { get; set; }
        public decimal ShortStrike { get; set; }
        public decimal LongStrike { get; set; }
        public decimal Credit { get; set; }
        public decimal Risk { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal UnrealizedPnL { get; set; }
        public int DTE { get; set; }
    }

    public class ProbePerformanceMetrics
    {
        public int TotalProbes { get; set; }
        public int WinCount { get; set; }
        public int LossCount { get; set; }
        public decimal WinRate { get; set; }
        public decimal AveragePnL { get; set; }
        public decimal TotalPnL { get; set; }
        public decimal AverageReturn { get; set; }
        public decimal MaxWin { get; set; }
        public decimal MaxLoss { get; set; }
        public int ConsecutiveWins { get; set; }
        public int ConsecutiveLosses { get; set; }
        public decimal SentimentScore { get; set; }
    }
}