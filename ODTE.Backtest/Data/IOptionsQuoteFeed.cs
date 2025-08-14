using ODTE.Backtest.Core;

namespace ODTE.Backtest.Data;

/// <summary>
/// Interface for options quote data providers.
/// WHY: Abstracts data source to support multiple vendors (OPRA, Polygon, etc.)
/// 
/// DESIGN PRINCIPLES:
/// - Vendor-agnostic: Works with any OPRA-grade data provider
/// - Async-first: Supports real-time and historical data fetching
/// - Strongly-typed: Returns structured OptionQuote records
/// - Error-tolerant: Graceful handling of missing data or connectivity issues
/// 
/// OPRA INTEGRATION:
/// Implementations should provide NBBO (National Best Bid/Offer) quotes
/// consolidated across all US options exchanges as the default data source.
/// This ensures pricing accuracy and regulatory compliance.
/// 
/// USAGE PATTERNS:
/// 1. GetChainAsync(): Bulk fetch for strategy evaluation
/// 2. GetQuoteAsync(): Individual quote for specific strike/expiry
/// 3. Support for both real-time and historical data modes
/// 
/// Reference: Code Review Summary - OPRA NBBO + Last Sale as Default
/// </summary>
public interface IOptionsQuoteFeed
{
    /// <summary>
    /// Retrieve complete options chain for a given symbol and expiry.
    /// Returns all available strikes with current market quotes.
    /// 
    /// PERFORMANCE NOTES:
    /// - Implementations should cache recent quotes to reduce API calls
    /// - Support bulk fetching for efficiency
    /// - Return empty list if no data available (don't throw)
    /// 
    /// NBBO REQUIREMENT:
    /// Quotes must represent NBBO (National Best Bid/Offer) across exchanges
    /// to ensure accurate pricing for strategy evaluation.
    /// </summary>
    /// <param name="symbol">Underlying symbol (e.g., "XSP", "SPY")</param>
    /// <param name="expiry">Option expiration date</param>
    /// <param name="asOf">Quote timestamp (UTC)</param>
    /// <returns>List of option quotes for all available strikes</returns>
    Task<List<OptionQuote>> GetChainAsync(string symbol, DateOnly expiry, DateTime asOf);

    /// <summary>
    /// Retrieve specific option quote for exact strike and expiry.
    /// More efficient than fetching entire chain when only one quote needed.
    /// 
    /// FALLBACK BEHAVIOR:
    /// Returns null if specific quote not available rather than throwing.
    /// Allows strategy to gracefully handle missing data.
    /// </summary>
    /// <param name="symbol">Underlying symbol</param>
    /// <param name="expiry">Option expiration date</param>
    /// <param name="strike">Strike price</param>
    /// <param name="right">Call or Put</param>
    /// <param name="asOf">Quote timestamp (UTC)</param>
    /// <returns>Option quote if available, null otherwise</returns>
    Task<OptionQuote?> GetQuoteAsync(string symbol, DateOnly expiry, decimal strike, Right right, DateTime asOf);

    /// <summary>
    /// Health check for data connection and quality.
    /// Implementations should verify:
    /// - API connectivity
    /// - Quote freshness (not stale)
    /// - NBBO compliance
    /// - No-arbitrage conditions
    /// </summary>
    /// <returns>True if data feed is healthy and reliable</returns>
    Task<bool> IsHealthyAsync();

    /// <summary>
    /// Get data quality metrics for monitoring and alerting.
    /// Used by validation framework to ensure data meets thresholds.
    /// 
    /// QUALITY METRICS:
    /// - Quote freshness (time since last update)
    /// - Spread percentage (bid-ask spread / mid)
    /// - Coverage percentage (available strikes vs expected)
    /// - No-arbitrage violations count
    /// </summary>
    /// <param name="symbol">Symbol to check</param>
    /// <param name="expiry">Expiry to check</param>
    /// <returns>Quality metrics for validation</returns>
    Task<DataQualityMetrics> GetQualityMetricsAsync(string symbol, DateOnly expiry);
}

/// <summary>
/// Data quality metrics for options quote validation.
/// Used to ensure data meets production trading standards.
/// </summary>
public record DataQualityMetrics(
    DateTime LastUpdate,        // When quotes were last refreshed
    double AvgSpreadPercent,   // Average bid-ask spread as % of mid
    double CoveragePercent,    // % of expected strikes available
    int ArbitrageViolations,   // Count of no-arbitrage violations
    bool PassesThreshold       // Overall pass/fail for quality gate
);