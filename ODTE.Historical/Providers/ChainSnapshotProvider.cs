using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ODTE.Historical.Providers
{
    public enum OptionRight
    {
        Call,
        Put
    }

    public sealed class ChainSnapshotProvider
    {
        private readonly IHistoricalDataSource _dataSource;
        private readonly ILogger<ChainSnapshotProvider> _logger;
        private readonly Dictionary<string, ChainSnapshot> _cache;
        private readonly object _cacheLock = new();

        public ChainSnapshotProvider(IHistoricalDataSource dataSource, ILogger<ChainSnapshotProvider> logger)
        {
            _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = new Dictionary<string, ChainSnapshot>();
        }

        public async Task<ChainSnapshot> GetSnapshotAtDecisionTime(
            string underlying, 
            DateTime decisionTimeEt, 
            ProductCalendar calendar)
        {
            try
            {
                var cacheKey = $"{underlying}_{decisionTimeEt:yyyyMMdd_HHmmss}";
                
                lock (_cacheLock)
                {
                    if (_cache.TryGetValue(cacheKey, out var cachedSnapshot))
                    {
                        _logger.LogDebug("Using cached chain snapshot for {Underlying} at {DecisionTime}", 
                            underlying, decisionTimeEt);
                        return cachedSnapshot;
                    }
                }

                _logger.LogDebug("Fetching chain snapshot for {Underlying} at {DecisionTime}", 
                    underlying, decisionTimeEt);

                var underlyingPrice = await GetUnderlyingPrice(underlying, decisionTimeEt);
                var optionChain = await GetOptionsChain(underlying, decisionTimeEt);
                var marketData = await GetMarketData(underlying, decisionTimeEt);

                var snapshot = new ChainSnapshot
                {
                    Underlying = underlying,
                    Timestamp = decisionTimeEt,
                    UnderlyingPrice = underlyingPrice,
                    OptionsChain = optionChain.ToArray(),
                    Calendar = calendar,
                    MarketData = marketData,
                    VixLevel = await GetVixLevel(decisionTimeEt),
                    AtmImpliedVolatility = CalculateAtmImpliedVolatility(optionChain, underlyingPrice)
                };

                lock (_cacheLock)
                {
                    _cache[cacheKey] = snapshot;
                }

                _logger.LogInformation("Retrieved chain snapshot for {Underlying}: {OptionCount} options, spot ${Spot:F2}, ATM IV {AtmIv:F1}%",
                    underlying, optionChain.Count(), underlyingPrice, snapshot.AtmImpliedVolatility * 100);

                return snapshot;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get chain snapshot for {Underlying} at {DecisionTime}", 
                    underlying, decisionTimeEt);
                throw;
            }
        }

        public async Task<ChainSnapshot[]> GetSnapshotsForWeek(
            string underlying,
            DateTime mondayDecisionTime,
            DateTime wednesdayDecisionTime,
            DateTime exitTime,
            ProductCalendar calendar)
        {
            var snapshots = new List<ChainSnapshot>();

            var snapshotTimes = new[]
            {
                ("Monday", mondayDecisionTime),
                ("Wednesday", wednesdayDecisionTime),
                ("Exit", exitTime)
            };

            foreach (var (label, timestamp) in snapshotTimes)
            {
                try
                {
                    var snapshot = await GetSnapshotAtDecisionTime(underlying, timestamp, calendar);
                    snapshot.Label = label;
                    snapshots.Add(snapshot);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get {Label} snapshot for {Underlying} at {Timestamp}", 
                        label, underlying, timestamp);
                }
            }

            return snapshots.ToArray();
        }

        private async Task<double> GetUnderlyingPrice(string underlying, DateTime timestamp)
        {
            var priceData = await _dataSource.GetUnderlyingPrices(underlying, timestamp, timestamp.AddMinutes(1));
            var pricePoint = priceData.FirstOrDefault(p => p.Timestamp <= timestamp);
            
            if (pricePoint == null)
            {
                throw new DataNotFoundException($"No underlying price found for {underlying} at {timestamp}");
            }

            return pricePoint.Price;
        }

        private async Task<IEnumerable<OptionQuote>> GetOptionsChain(string underlying, DateTime timestamp)
        {
            var chainData = await _dataSource.GetOptionsChain(underlying, timestamp);
            
            return chainData
                .Where(opt => opt.Timestamp <= timestamp)
                .Where(opt => HasValidQuote(opt))
                .Where(opt => IsReasonableStrike(opt, underlying))
                .OrderBy(opt => opt.Expiry)
                .ThenBy(opt => opt.Right)
                .ThenBy(opt => opt.Strike);
        }

        private async Task<MarketDataSnapshot> GetMarketData(string underlying, DateTime timestamp)
        {
            var marketData = await _dataSource.GetMarketData(underlying, timestamp);
            
            return new MarketDataSnapshot
            {
                Timestamp = timestamp,
                Volume = marketData.Volume,
                OpenInterest = marketData.OpenInterest,
                High = marketData.High,
                Low = marketData.Low,
                Close = marketData.Close,
                Vwap = marketData.Vwap,
                ImpliedVolatility30 = marketData.ImpliedVolatility30,
                HistoricalVolatility20 = marketData.HistoricalVolatility20
            };
        }

        private async Task<double> GetVixLevel(DateTime timestamp)
        {
            try
            {
                var vixData = await _dataSource.GetVixData(timestamp);
                return vixData?.Level ?? 20.0; // Default VIX if unavailable
            }
            catch
            {
                return 20.0; // Default fallback
            }
        }

        private double CalculateAtmImpliedVolatility(IEnumerable<OptionQuote> optionChain, double underlyingPrice)
        {
            var nearestExpiry = optionChain
                .Where(opt => opt.Expiry > DateTime.Today)
                .GroupBy(opt => opt.Expiry)
                .OrderBy(g => g.Key)
                .FirstOrDefault();

            if (nearestExpiry == null)
                return 0.25; // Default 25% IV

            var atmOptions = nearestExpiry
                .Where(opt => Math.Abs(opt.Strike - underlyingPrice) < underlyingPrice * 0.05) // Within 5% of ATM
                .Where(opt => opt.ImpliedVolatility > 0)
                .ToArray();

            if (!atmOptions.Any())
                return 0.25;

            return atmOptions.Average(opt => opt.ImpliedVolatility);
        }

        private bool HasValidQuote(OptionQuote option)
        {
            return option.Bid > 0 && 
                   option.Ask > option.Bid && 
                   option.Ask < option.Bid * 5 && // Spread not too wide
                   option.ImpliedVolatility > 0 &&
                   option.ImpliedVolatility < 3.0; // IV under 300%
        }

        private bool IsReasonableStrike(OptionQuote option, string underlying)
        {
            // Filter out strikes that are too far OTM or have unrealistic prices
            var underlyingPrice = 75.0; // Will be replaced with actual price lookup
            var maxOtmPercent = underlying.StartsWith("CL") ? 0.50 : 0.30; // 50% for oil, 30% for ETFs
            
            var otmPercent = Math.Abs(option.Strike - underlyingPrice) / underlyingPrice;
            return otmPercent <= maxOtmPercent;
        }

        public async Task<OptionQuote[]> GetNearestStrikes(
            string underlying, 
            double targetStrike, 
            OptionRight right, 
            DateTime expiry,
            DateTime timestamp,
            int count = 5)
        {
            var chainData = await GetOptionsChain(underlying, timestamp);
            
            return chainData
                .Where(opt => opt.Right == right && opt.Expiry.Date == expiry.Date)
                .OrderBy(opt => Math.Abs(opt.Strike - targetStrike))
                .Take(count)
                .ToArray();
        }

        public async Task<double[]> GetAvailableStrikes(
            string underlying, 
            DateTime expiry, 
            DateTime timestamp)
        {
            var chainData = await GetOptionsChain(underlying, timestamp);
            
            return chainData
                .Where(opt => opt.Expiry.Date == expiry.Date)
                .Select(opt => opt.Strike)
                .Distinct()
                .OrderBy(strike => strike)
                .ToArray();
        }

        public async Task<ExpirationInfo[]> GetAvailableExpirations(
            string underlying, 
            DateTime timestamp,
            int maxDte = 45)
        {
            var chainData = await GetOptionsChain(underlying, timestamp);
            var cutoffDate = timestamp.AddDays(maxDte);
            
            return chainData
                .Where(opt => opt.Expiry > timestamp && opt.Expiry <= cutoffDate)
                .GroupBy(opt => opt.Expiry.Date)
                .Select(g => new ExpirationInfo
                {
                    Expiry = g.Key,
                    DTE = (g.Key - timestamp.Date).Days,
                    OptionCount = g.Count(),
                    HasWeekly = IsWeeklyExpiration(g.Key),
                    LiquidityScore = CalculateLiquidityScore(g)
                })
                .OrderBy(exp => exp.Expiry)
                .ToArray();
        }

        private bool IsWeeklyExpiration(DateTime expiry)
        {
            // Weekly expirations are typically Monday, Wednesday, Friday
            return expiry.DayOfWeek is DayOfWeek.Monday or DayOfWeek.Wednesday or DayOfWeek.Friday;
        }

        private double CalculateLiquidityScore(IGrouping<DateTime, OptionQuote> expiryGroup)
        {
            var validOptions = expiryGroup.Where(HasValidQuote).ToArray();
            if (!validOptions.Any()) return 0;

            var avgVolume = validOptions.Average(opt => opt.Volume);
            var avgOpenInterest = validOptions.Average(opt => opt.OpenInterest);
            var tightSpreads = validOptions.Count(opt => (opt.Ask - opt.Bid) / opt.Ask < 0.10);
            var spreadQuality = (double)tightSpreads / validOptions.Length;

            return (Math.Log(avgVolume + 1) + Math.Log(avgOpenInterest + 1)) * spreadQuality;
        }

        public void ClearCache()
        {
            lock (_cacheLock)
            {
                _cache.Clear();
                _logger.LogInformation("Chain snapshot cache cleared");
            }
        }

        public void ClearCacheOlderThan(TimeSpan age)
        {
            var cutoffTime = DateTime.Now.Subtract(age);
            
            lock (_cacheLock)
            {
                var oldKeys = _cache
                    .Where(kvp => kvp.Value.Timestamp < cutoffTime)
                    .Select(kvp => kvp.Key)
                    .ToArray();

                foreach (var key in oldKeys)
                {
                    _cache.Remove(key);
                }

                _logger.LogInformation("Removed {Count} old entries from chain snapshot cache", oldKeys.Length);
            }
        }
    }

    public sealed class ChainSnapshot
    {
        public string Underlying { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public double UnderlyingPrice { get; set; }
        public OptionQuote[] OptionsChain { get; set; } = Array.Empty<OptionQuote>();
        public ProductCalendar Calendar { get; set; } = new();
        public MarketDataSnapshot MarketData { get; set; } = new();
        public double VixLevel { get; set; }
        public double AtmImpliedVolatility { get; set; }
        public string Label { get; set; } = "";

        public double GetAtmImpliedVolatility() => AtmImpliedVolatility;
        
        public Func<double, double> GetNearestStrike => strike => 
        {
            var increment = Underlying.StartsWith("CL") ? 0.5 : 0.5;
            return Math.Round(strike / increment) * increment;
        };
        
        public bool HasZeroDteOptions() => 
            OptionsChain.Any(opt => opt.Expiry.Date == Timestamp.Date);

        public OptionQuote[] GetOptionsForExpiry(DateTime expiry) =>
            OptionsChain.Where(opt => opt.Expiry.Date == expiry.Date).ToArray();

        public double[] GetStrikesForExpiry(DateTime expiry) =>
            GetOptionsForExpiry(expiry)
                .Select(opt => opt.Strike)
                .Distinct()
                .OrderBy(s => s)
                .ToArray();
    }

    public sealed class OptionQuote
    {
        public DateTime Timestamp { get; set; }
        public string Underlying { get; set; } = "";
        public DateTime Expiry { get; set; }
        public OptionRight Right { get; set; }
        public double Strike { get; set; }
        public double Bid { get; set; }
        public double Ask { get; set; }
        public double Last { get; set; }
        public double ImpliedVolatility { get; set; }
        public double Delta { get; set; }
        public double Gamma { get; set; }
        public double Theta { get; set; }
        public double Vega { get; set; }
        public int Volume { get; set; }
        public int OpenInterest { get; set; }
    }

    public sealed class MarketDataSnapshot
    {
        public DateTime Timestamp { get; set; }
        public int Volume { get; set; }
        public int OpenInterest { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public double Vwap { get; set; }
        public double ImpliedVolatility30 { get; set; }
        public double HistoricalVolatility20 { get; set; }
    }

    public sealed class ExpirationInfo
    {
        public DateTime Expiry { get; set; }
        public int DTE { get; set; }
        public int OptionCount { get; set; }
        public bool HasWeekly { get; set; }
        public double LiquidityScore { get; set; }
    }

    public sealed class UnderlyingPrice
    {
        public DateTime Timestamp { get; set; }
        public double Price { get; set; }
    }

    public sealed class VixData
    {
        public DateTime Timestamp { get; set; }
        public double Level { get; set; }
    }

    // ProductCalendar moved to SessionCalendarProvider.cs to avoid duplication

    public interface IHistoricalDataSource
    {
        Task<IEnumerable<UnderlyingPrice>> GetUnderlyingPrices(string symbol, DateTime start, DateTime end);
        Task<IEnumerable<OptionQuote>> GetOptionsChain(string underlying, DateTime timestamp);
        Task<MarketDataSnapshot> GetMarketData(string underlying, DateTime timestamp);
        Task<VixData?> GetVixData(DateTime timestamp);
    }

    public interface ILogger<T>
    {
        void LogDebug(string message, params object[] args);
        void LogInformation(string message, params object[] args);
        void LogWarning(Exception ex, string message, params object[] args);
        void LogError(Exception ex, string message, params object[] args);
    }

    public class DataNotFoundException : Exception
    {
        public DataNotFoundException(string message) : base(message) { }
    }
}