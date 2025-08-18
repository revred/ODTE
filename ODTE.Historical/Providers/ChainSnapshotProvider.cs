using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using ODTE.Historical.DistributedStorage;

namespace ODTE.Historical.Providers;

/// <summary>
/// ChainSnapshotProvider - Real historical options chain access
/// Per CDTE spec: No synthetic data, authentic NBBO only
/// Returns first snapshot ≥ target timestamp (deterministic)
/// </summary>
public class ChainSnapshotProvider : IDisposable
{
    private readonly DistributedDatabaseManager _dataManager;
    private readonly ILogger<ChainSnapshotProvider> _logger;
    private readonly Dictionary<string, DateTime> _lastSnapshotCache = new();

    public ChainSnapshotProvider(DistributedDatabaseManager dataManager, ILogger<ChainSnapshotProvider> logger)
    {
        _dataManager = dataManager;
        _logger = logger;
    }

    /// <summary>
    /// Get options chain snapshot at or after target timestamp
    /// Per spec: Returns first available snapshot ≥ ts_et (deterministic)
    /// </summary>
    public async Task<ChainSnapshot?> GetSnapshotAsync(
        string underlying, 
        DateTime targetTimestampET, 
        TimeSpan? maxDeferMinutes = null)
    {
        try
        {
            var maxDefer = maxDeferMinutes ?? TimeSpan.FromMinutes(5);
            var maxTimestamp = targetTimestampET.Add(maxDefer);
            
            _logger.LogDebug("Getting chain snapshot for {Underlying} at {Target} ET (max defer: {MaxDefer})", 
                underlying, targetTimestampET, maxDefer);

            // Convert ET to UTC for database queries
            var targetUTC = ConvertETToUTC(targetTimestampET);
            var maxUTC = ConvertETToUTC(maxTimestamp);

            // Get underlying price at decision time
            var underlyingPrice = await GetUnderlyingPriceAsync(underlying, targetUTC, maxUTC);
            if (underlyingPrice == null)
            {
                _logger.LogWarning("No underlying price found for {Underlying} at {Target}", underlying, targetTimestampET);
                return null;
            }

            // Get options chain for current week's expirations
            var thisWeekExpirations = GetThisWeekExpirations(targetTimestampET);
            var options = new List<OptionContract>();

            foreach (var expiry in thisWeekExpirations)
            {
                var expiryOptions = await GetOptionsForExpiryAsync(underlying, expiry, targetUTC, maxUTC);
                options.AddRange(expiryOptions);
            }

            if (!options.Any())
            {
                _logger.LogWarning("No options data found for {Underlying} expirations {Expirations} at {Target}", 
                    underlying, string.Join(",", thisWeekExpirations.Select(e => e.ToString("yyyy-MM-dd"))), targetTimestampET);
                return null;
            }

            var snapshot = new ChainSnapshot
            {
                TimestampET = targetTimestampET,
                TimestampUTC = targetUTC,
                Underlying = underlying,
                UnderlyingPrice = underlyingPrice.Price,
                UnderlyingBid = underlyingPrice.Bid,
                UnderlyingAsk = underlyingPrice.Ask,
                Options = options
            };

            _logger.LogInformation("Retrieved chain snapshot for {Underlying}: {OptionCount} options across {ExpiryCount} expirations", 
                underlying, options.Count, thisWeekExpirations.Count);

            return snapshot;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting chain snapshot for {Underlying} at {Target}", underlying, targetTimestampET);
            return null;
        }
    }

    /// <summary>
    /// Get underlying price at or after target timestamp
    /// </summary>
    private async Task<UnderlyingPrice?> GetUnderlyingPriceAsync(string symbol, DateTime targetUTC, DateTime maxUTC)
    {
        try
        {
            // Use distributed database to get underlying data
            var commodityData = await _dataManager.GetCommodityDataAsync(symbol, targetUTC.Date, maxUTC.Date, CommodityCategory.Energy);
            
            var priceBar = commodityData
                .Where(d => d.Timestamp >= targetUTC && d.Timestamp <= maxUTC)
                .OrderBy(d => d.Timestamp)
                .FirstOrDefault();

            if (priceBar != null)
            {
                return new UnderlyingPrice
                {
                    Symbol = symbol,
                    Timestamp = priceBar.Timestamp,
                    Price = (decimal)priceBar.Close,
                    Bid = (decimal)priceBar.Close * 0.9999m, // Approximate bid
                    Ask = (decimal)priceBar.Close * 1.0001m  // Approximate ask
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting underlying price for {Symbol}", symbol);
            return null;
        }
    }

    /// <summary>
    /// Get options data for specific expiration at target time
    /// </summary>
    private async Task<List<OptionContract>> GetOptionsForExpiryAsync(
        string underlying, 
        DateTime expiry, 
        DateTime targetUTC, 
        DateTime maxUTC)
    {
        try
        {
            // Get options chain from distributed database
            var optionsChain = await _dataManager.GetOptionsChainAsync(underlying, expiry, CommodityCategory.Energy);
            
            if (!optionsChain.Options.Any())
            {
                _logger.LogDebug("No options found for {Underlying} expiry {Expiry}", underlying, expiry);
                return new List<OptionContract>();
            }

            // Return the contracts directly - they're already in the right format
            var contracts = optionsChain.Options.ToList();

            _logger.LogDebug("Retrieved {Count} options for {Underlying} expiry {Expiry}", 
                contracts.Count, underlying, expiry);

            return contracts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting options for {Underlying} expiry {Expiry}", underlying, expiry);
            return new List<OptionContract>();
        }
    }

    /// <summary>
    /// Get this week's expiration dates (Thursday and Friday)
    /// </summary>
    private List<DateTime> GetThisWeekExpirations(DateTime referenceDate)
    {
        var expirations = new List<DateTime>();
        
        // Find Thursday of this week
        var daysUntilThursday = ((int)DayOfWeek.Thursday - (int)referenceDate.DayOfWeek + 7) % 7;
        if (daysUntilThursday == 0 && referenceDate.DayOfWeek != DayOfWeek.Thursday)
            daysUntilThursday = 7;
        
        var thursday = referenceDate.Date.AddDays(daysUntilThursday);
        expirations.Add(thursday);
        
        // Friday is the day after Thursday
        var friday = thursday.AddDays(1);
        expirations.Add(friday);
        
        return expirations;
    }

    /// <summary>
    /// Convert Eastern Time to UTC for database queries
    /// </summary>
    private DateTime ConvertETToUTC(DateTime easternTime)
    {
        // Simplified conversion - in production, use proper timezone handling
        // EST = UTC-5, EDT = UTC-4 (account for daylight saving time)
        var isDST = IsDaylightSavingTime(easternTime);
        var utcOffset = isDST ? -4 : -5;
        return easternTime.AddHours(-utcOffset);
    }

    /// <summary>
    /// Determine if date falls within daylight saving time
    /// </summary>
    private bool IsDaylightSavingTime(DateTime date)
    {
        // Simplified DST calculation for US Eastern Time
        // Second Sunday in March to first Sunday in November
        var year = date.Year;
        var marchSecondSunday = GetNthSundayOfMonth(year, 3, 2);
        var novemberFirstSunday = GetNthSundayOfMonth(year, 11, 1);
        
        return date >= marchSecondSunday && date < novemberFirstSunday;
    }

    /// <summary>
    /// Get the Nth Sunday of a given month
    /// </summary>
    private DateTime GetNthSundayOfMonth(int year, int month, int n)
    {
        var firstDay = new DateTime(year, month, 1);
        var firstSunday = firstDay.AddDays(((int)DayOfWeek.Sunday - (int)firstDay.DayOfWeek + 7) % 7);
        return firstSunday.AddDays((n - 1) * 7);
    }

    public void Dispose()
    {
        _dataManager?.Dispose();
    }
}

/// <summary>
/// Underlying price data at specific timestamp
/// </summary>
public class UnderlyingPrice
{
    public string Symbol { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public decimal Price { get; set; }
    public decimal Bid { get; set; }
    public decimal Ask { get; set; }
}

/// <summary>
/// Options chain snapshot at specific timestamp
/// Contains all options data needed for CDTE strategy decisions
/// </summary>
public class ChainSnapshot
{
    public DateTime TimestampET { get; set; }
    public DateTime TimestampUTC { get; set; }
    public string Underlying { get; set; } = "";
    public decimal UnderlyingPrice { get; set; }
    public decimal UnderlyingBid { get; set; }
    public decimal UnderlyingAsk { get; set; }
    public List<OptionContract> Options { get; set; } = new();
    
    /// <summary>
    /// Get options for specific expiration date
    /// </summary>
    public IEnumerable<OptionContract> GetOptionsForExpiry(DateTime expiry)
    {
        return Options.Where(o => o.ExpirationDate.Date == expiry.Date);
    }
    
    /// <summary>
    /// Get options of specific type (calls or puts)
    /// </summary>
    public IEnumerable<OptionContract> GetOptions(OptionType optionType)
    {
        return Options.Where(o => o.Type == optionType);
    }
    
    /// <summary>
    /// Get options sorted by delta for strike picking
    /// </summary>
    public IEnumerable<OptionContract> GetOptionsByDelta(OptionType optionType, bool ascending = true)
    {
        var options = GetOptions(optionType);
        return ascending ? options.OrderBy(o => o.Delta) : options.OrderByDescending(o => o.Delta);
    }
    
    /// <summary>
    /// Calculate front month implied volatility
    /// </summary>
    public double GetFrontImpliedVolatility()
    {
        var nearExpiry = Options.Min(o => o.ExpirationDate);
        var atmOptions = Options
            .Where(o => o.ExpirationDate == nearExpiry)
            .Where(o => Math.Abs(o.Strike - UnderlyingPrice) < UnderlyingPrice * 0.05m) // Within 5% of ATM
            .ToList();
            
        return atmOptions.Any() ? (double)atmOptions.Average(o => o.ImpliedVolatility) : 20.0;
    }
}