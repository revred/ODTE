using System;
using System.Collections.Generic;
using System.Linq;

namespace ODTE.Strategy
{
    /// <summary>
    /// Manages correlation budget to prevent excessive concurrent exposure
    /// Implements rho-weighted exposure calculation as specified in ScaleHighWithManagedRisk
    /// Ensures total correlation-adjusted risk stays within defined limits
    /// </summary>
    public class CorrelationBudgetManager : ICorrelationBudgetManager
    {
        private readonly ICorrelationDataProvider _correlationProvider;
        private readonly decimal _maxRhoWeightedExposure;
        private readonly Dictionary<string, decimal> _betaCache;
        private readonly Dictionary<(string, string), decimal> _correlationCache;
        private DateTime _lastCacheUpdate;
        private readonly TimeSpan _cacheValidityPeriod = TimeSpan.FromHours(4);

        public CorrelationBudgetManager(ICorrelationDataProvider correlationProvider, decimal maxRhoWeightedExposure = 1.0m)
        {
            _correlationProvider = correlationProvider ?? throw new ArgumentNullException(nameof(correlationProvider));
            _maxRhoWeightedExposure = maxRhoWeightedExposure;
            _betaCache = new Dictionary<string, decimal>();
            _correlationCache = new Dictionary<(string, string), decimal>();
            _lastCacheUpdate = DateTime.MinValue;
        }

        /// <summary>
        /// Calculate current rho-weighted exposure across all open positions
        /// </summary>
        public decimal CalculateCurrentRhoWeightedExposure(List<Position> positions)
        {
            if (positions == null || !positions.Any())
                return 0m;

            RefreshCacheIfNeeded();

            decimal totalRhoWeightedExposure = 0m;

            foreach (var position in positions)
            {
                var dailyCap = GetDailyCapForPosition(position);
                var exposureFraction = position.MaxLoss / dailyCap;
                var correlationWeight = CalculateCorrelationWeight(position, positions);
                
                var rhoWeightedContribution = exposureFraction * correlationWeight;
                totalRhoWeightedExposure += rhoWeightedContribution;

                LogCorrelationCalculation(position, exposureFraction, correlationWeight, rhoWeightedContribution);
            }

            return totalRhoWeightedExposure;
        }

        /// <summary>
        /// Calculate rho-weighted exposure after adding a new position
        /// </summary>
        public decimal CalculateRhoWeightedExposureAfter(List<Position> currentPositions, TradeSetup setup, decimal positionSize)
        {
            // Create hypothetical position from trade setup
            var newPosition = CreateHypotheticalPosition(setup, positionSize);
            
            // Calculate exposure with new position added
            var positionsWithNew = new List<Position>(currentPositions) { newPosition };
            
            return CalculateCurrentRhoWeightedExposure(positionsWithNew);
        }

        /// <summary>
        /// Check if adding a new position would violate correlation budget
        /// </summary>
        public bool WouldViolateCorrelationBudget(List<Position> currentPositions, TradeSetup setup, decimal positionSize)
        {
            var exposureAfter = CalculateRhoWeightedExposureAfter(currentPositions, setup, positionSize);
            return exposureAfter > _maxRhoWeightedExposure;
        }

        /// <summary>
        /// Get maximum position size that wouldn't violate correlation budget
        /// </summary>
        public decimal GetMaxPositionSizeForCorrelationBudget(List<Position> currentPositions, TradeSetup setup, decimal dailyCap)
        {
            RefreshCacheIfNeeded();

            // Binary search for maximum safe position size
            decimal minSize = 0m;
            decimal maxSize = 10m; // Start with reasonable upper bound
            decimal precision = 0.1m;

            while (maxSize - minSize > precision)
            {
                decimal testSize = (minSize + maxSize) / 2m;
                
                if (WouldViolateCorrelationBudget(currentPositions, setup, testSize))
                {
                    maxSize = testSize;
                }
                else
                {
                    minSize = testSize;
                }
            }

            return Math.Floor(minSize); // Return integer position size
        }

        /// <summary>
        /// Calculate correlation weight for a position relative to other positions
        /// Uses max of beta to SPY and maximum pairwise correlation
        /// </summary>
        private decimal CalculateCorrelationWeight(Position position, List<Position> allPositions)
        {
            var betaToSPY = GetBetaToSPY(position.Symbol);
            var maxPairwiseCorrelation = GetMaxPairwiseCorrelation(position, allPositions);
            
            // Weight is max of absolute beta and max pairwise correlation
            return Math.Max(Math.Abs(betaToSPY), maxPairwiseCorrelation);
        }

        /// <summary>
        /// Get maximum pairwise correlation between this position and all others
        /// </summary>
        private decimal GetMaxPairwiseCorrelation(Position position, List<Position> allPositions)
        {
            decimal maxCorrelation = 0m;

            foreach (var otherPosition in allPositions)
            {
                if (otherPosition.Symbol == position.Symbol)
                    continue;

                var correlation = Math.Abs(GetCorrelationBetween(position.Symbol, otherPosition.Symbol));
                maxCorrelation = Math.Max(maxCorrelation, correlation);
            }

            return maxCorrelation;
        }

        /// <summary>
        /// Get beta to SPY for a given symbol
        /// </summary>
        private decimal GetBetaToSPY(string symbol)
        {
            if (symbol == "SPY" || symbol == "SPX")
                return 1.0m;

            if (_betaCache.TryGetValue(symbol, out decimal cachedBeta))
                return cachedBeta;

            var beta = _correlationProvider.GetBetaToSPY(symbol);
            _betaCache[symbol] = beta;
            
            return beta;
        }

        /// <summary>
        /// Get correlation coefficient between two symbols
        /// </summary>
        private decimal GetCorrelationBetween(string symbol1, string symbol2)
        {
            if (symbol1 == symbol2)
                return 1.0m;

            // Ensure consistent ordering for cache key
            var key = string.CompareOrdinal(symbol1, symbol2) < 0 ? (symbol1, symbol2) : (symbol2, symbol1);

            if (_correlationCache.TryGetValue(key, out decimal cachedCorrelation))
                return cachedCorrelation;

            var correlation = _correlationProvider.GetCorrelationBetween(symbol1, symbol2);
            _correlationCache[key] = correlation;
            
            return correlation;
        }

        /// <summary>
        /// Create hypothetical position for calculation purposes
        /// </summary>
        private Position CreateHypotheticalPosition(TradeSetup setup, decimal positionSize)
        {
            var maxLossPerContract = (setup.Width - setup.ExpectedCredit) * 100m;
            var totalMaxLoss = maxLossPerContract * positionSize;

            return new Position
            {
                Symbol = setup.Symbol,
                MaxLoss = totalMaxLoss,
                BetaToSPY = GetBetaToSPY(setup.Symbol),
                MaxPairwiseCorrelation = 0m, // Will be calculated in context
                Lane = TradeLane.Quality, // Assume Quality for conservative estimation
                EntryTime = setup.EntryTime
            };
        }

        /// <summary>
        /// Get daily cap relevant for the position (may vary by strategy phase)
        /// </summary>
        private decimal GetDailyCapForPosition(Position position)
        {
            // This would typically come from the risk manager
            // For now, return a default based on position entry time
            return 1000m; // Placeholder - should be injected from risk manager
        }

        /// <summary>
        /// Refresh correlation and beta data cache if stale
        /// </summary>
        private void RefreshCacheIfNeeded()
        {
            if (DateTime.Now - _lastCacheUpdate > _cacheValidityPeriod)
            {
                _betaCache.Clear();
                _correlationCache.Clear();
                _lastCacheUpdate = DateTime.Now;
                
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Correlation cache refreshed");
            }
        }

        /// <summary>
        /// Log correlation calculation details for debugging and monitoring
        /// </summary>
        private void LogCorrelationCalculation(Position position, decimal exposureFraction, decimal correlationWeight, decimal rhoWeightedContribution)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Correlation Calc: {position.Symbol} " +
                            $"Exposure:{exposureFraction:P2} Weight:{correlationWeight:F3} " +
                            $"Contribution:{rhoWeightedContribution:F3} MaxLoss:{position.MaxLoss:C}");
        }

        /// <summary>
        /// Get detailed correlation budget analysis for monitoring
        /// </summary>
        public CorrelationBudgetAnalysis GetDetailedAnalysis(List<Position> positions)
        {
            RefreshCacheIfNeeded();

            var analysis = new CorrelationBudgetAnalysis
            {
                TotalPositions = positions.Count,
                TotalRhoWeightedExposure = CalculateCurrentRhoWeightedExposure(positions),
                MaxRhoWeightedExposure = _maxRhoWeightedExposure,
                RemainingCapacity = _maxRhoWeightedExposure - CalculateCurrentRhoWeightedExposure(positions),
                PositionDetails = new List<PositionCorrelationDetail>()
            };

            foreach (var position in positions)
            {
                var dailyCap = GetDailyCapForPosition(position);
                var exposureFraction = position.MaxLoss / dailyCap;
                var correlationWeight = CalculateCorrelationWeight(position, positions);
                var rhoContribution = exposureFraction * correlationWeight;

                analysis.PositionDetails.Add(new PositionCorrelationDetail
                {
                    Symbol = position.Symbol,
                    MaxLoss = position.MaxLoss,
                    ExposureFraction = exposureFraction,
                    BetaToSPY = GetBetaToSPY(position.Symbol),
                    MaxPairwiseCorrelation = GetMaxPairwiseCorrelation(position, positions),
                    CorrelationWeight = correlationWeight,
                    RhoWeightedContribution = rhoContribution
                });
            }

            analysis.IsOverBudget = analysis.TotalRhoWeightedExposure > _maxRhoWeightedExposure;
            analysis.UtilizationPercent = analysis.TotalRhoWeightedExposure / _maxRhoWeightedExposure;

            return analysis;
        }
    }

    /// <summary>
    /// Interface for correlation data provider (market data, historical analysis, etc.)
    /// </summary>
    public interface ICorrelationDataProvider
    {
        decimal GetBetaToSPY(string symbol);
        decimal GetCorrelationBetween(string symbol1, string symbol2);
        DateTime GetLastDataUpdate(string symbol);
    }

    /// <summary>
    /// Detailed correlation budget analysis for monitoring and debugging
    /// </summary>
    public class CorrelationBudgetAnalysis
    {
        public int TotalPositions { get; set; }
        public decimal TotalRhoWeightedExposure { get; set; }
        public decimal MaxRhoWeightedExposure { get; set; }
        public decimal RemainingCapacity { get; set; }
        public decimal UtilizationPercent { get; set; }
        public bool IsOverBudget { get; set; }
        public List<PositionCorrelationDetail> PositionDetails { get; set; } = new();
    }

    /// <summary>
    /// Correlation details for individual position
    /// </summary>
    public class PositionCorrelationDetail
    {
        public string Symbol { get; set; }
        public decimal MaxLoss { get; set; }
        public decimal ExposureFraction { get; set; }
        public decimal BetaToSPY { get; set; }
        public decimal MaxPairwiseCorrelation { get; set; }
        public decimal CorrelationWeight { get; set; }
        public decimal RhoWeightedContribution { get; set; }
    }

    /// <summary>
    /// Mock correlation data provider for testing and development
    /// </summary>
    public class MockCorrelationDataProvider : ICorrelationDataProvider
    {
        private readonly Dictionary<string, decimal> _betaData;
        private readonly Dictionary<(string, string), decimal> _correlationData;

        public MockCorrelationDataProvider()
        {
            // Initialize with common market correlations
            _betaData = new Dictionary<string, decimal>
            {
                { "SPY", 1.00m },
                { "SPX", 1.00m },
                { "QQQ", 1.15m },
                { "IWM", 1.25m },
                { "XSP", 1.00m },
                { "AAPL", 1.10m },
                { "TSLA", 1.80m },
                { "MSFT", 0.95m },
                { "GOOGL", 1.05m },
                { "NVDA", 1.60m }
            };

            _correlationData = new Dictionary<(string, string), decimal>
            {
                { ("SPY", "QQQ"), 0.85m },
                { ("SPY", "IWM"), 0.75m },
                { ("SPY", "XSP"), 0.99m },
                { ("QQQ", "IWM"), 0.65m },
                { ("AAPL", "MSFT"), 0.70m },
                { ("AAPL", "GOOGL"), 0.65m },
                { ("TSLA", "NVDA"), 0.60m },
                { ("SPY", "AAPL"), 0.80m },
                { ("SPY", "TSLA"), 0.45m },
                { ("QQQ", "AAPL"), 0.90m }
            };
        }

        public decimal GetBetaToSPY(string symbol)
        {
            return _betaData.TryGetValue(symbol, out decimal beta) ? beta : 1.0m;
        }

        public decimal GetCorrelationBetween(string symbol1, string symbol2)
        {
            if (symbol1 == symbol2) return 1.0m;

            var key = string.CompareOrdinal(symbol1, symbol2) < 0 ? (symbol1, symbol2) : (symbol2, symbol1);
            return _correlationData.TryGetValue(key, out decimal correlation) ? correlation : 0.30m; // Default 30% correlation
        }

        public DateTime GetLastDataUpdate(string symbol)
        {
            return DateTime.Now.AddMinutes(-5); // Mock recent update
        }
    }
}