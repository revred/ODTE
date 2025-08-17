namespace ODTE.Historical.DataProviders;

/// <summary>
/// Multi-source data provider that tries Stooq first, then Yahoo Finance, then Alpha Vantage
/// Implements intelligent failover for robust data acquisition
/// </summary>
public class MultiSourceDataProvider : IDisposable
{
    private readonly StooqProvider _stooqProvider;
    private readonly YahooFinanceProvider _yahooProvider;
    private readonly AlphaVantageProvider _alphaVantageProvider;
    private readonly Dictionary<string, int> _providerFailures;

    public MultiSourceDataProvider(string alphaVantageApiKey = "demo")
    {
        _stooqProvider = new StooqProvider();
        _yahooProvider = new YahooFinanceProvider();
        _alphaVantageProvider = new AlphaVantageProvider(alphaVantageApiKey);
        _providerFailures = new Dictionary<string, int>();
    }

    /// <summary>
    /// Get historical data with intelligent provider selection
    /// </summary>
    public async Task<List<HistoricalDataBar>> GetHistoricalDataAsync(
        string symbol,
        DateTime startDate,
        DateTime endDate)
    {
        var providers = GetProviderOrder(symbol);

        foreach (var provider in providers)
        {
            try
            {
                List<HistoricalDataBar>? bars = null;

                switch (provider)
                {
                    case "Stooq":
                        Console.WriteLine($"🔄 Trying Stooq for {symbol}...");
                        bars = await _stooqProvider.GetHistoricalDataAsync(symbol, startDate, endDate);
                        break;

                    case "Yahoo":
                        Console.WriteLine($"🔄 Trying Yahoo Finance for {symbol}...");
                        bars = await _yahooProvider.GetHistoricalDataAsync(symbol, startDate, endDate);
                        break;

                    case "AlphaVantage":
                        Console.WriteLine($"🔄 Trying Alpha Vantage for {symbol}...");
                        bars = await _alphaVantageProvider.GetHistoricalDataAsync(symbol, startDate, endDate);
                        break;
                }

                if (bars != null && bars.Count > 0)
                {
                    Console.WriteLine($"✅ Successfully retrieved {bars.Count} bars from {provider}");
                    ResetFailureCount(provider);
                    return bars;
                }
                else
                {
                    Console.WriteLine($"⚠️ {provider} returned no data for {symbol}");
                    IncrementFailureCount(provider);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ {provider} failed for {symbol}: {ex.Message}");
                IncrementFailureCount(provider);
            }
        }

        Console.WriteLine($"💔 All data sources failed for {symbol} ({startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd})");
        return new List<HistoricalDataBar>();
    }

    /// <summary>
    /// Get chunked historical data with intelligent provider selection
    /// </summary>
    public async Task<List<HistoricalDataBar>> GetHistoricalDataChunkedAsync(
        string symbol,
        DateTime startDate,
        DateTime endDate,
        TimeSpan chunkSize,
        IProgress<DataAcquisitionProgress>? progress = null)
    {
        var allBars = new List<HistoricalDataBar>();
        var currentDate = startDate;
        var totalDays = (endDate - startDate).TotalDays;
        var processedDays = 0.0;

        while (currentDate < endDate)
        {
            var chunkEnd = currentDate.Add(chunkSize);
            if (chunkEnd > endDate) chunkEnd = endDate;

            var chunkBars = await GetHistoricalDataAsync(symbol, currentDate, chunkEnd);
            allBars.AddRange(chunkBars);

            processedDays += (chunkEnd - currentDate).TotalDays;
            var progressPercent = (processedDays / totalDays) * 100;

            progress?.Report(new DataAcquisitionProgress
            {
                Symbol = symbol,
                StartDate = startDate,
                EndDate = endDate,
                CurrentDate = chunkEnd,
                ProgressPercent = progressPercent,
                RecordsProcessed = chunkBars.Count,
                Status = $"Downloaded {chunkBars.Count} bars for {currentDate:yyyy-MM-dd} to {chunkEnd:yyyy-MM-dd}"
            });

            currentDate = chunkEnd.AddDays(1);

            // Small delay between chunks to be respectful
            await Task.Delay(1000);
        }

        // Remove duplicates and sort by date
        allBars = allBars
            .GroupBy(b => b.Timestamp.Date)
            .Select(g => g.First())
            .OrderBy(b => b.Timestamp)
            .ToList();

        return allBars;
    }

    /// <summary>
    /// Determine provider order based on historical success rates
    /// </summary>
    private List<string> GetProviderOrder(string symbol)
    {
        var providers = new[]
        {
            ("Stooq", GetFailureCount("Stooq")),
            ("Yahoo", GetFailureCount("Yahoo")),
            ("AlphaVantage", GetFailureCount("AlphaVantage"))
        };

        // Sort by failure count (ascending) - most reliable first
        return providers
            .OrderBy(p => p.Item2)
            .Select(p => p.Item1)
            .ToList();
    }

    private void IncrementFailureCount(string provider)
    {
        _providerFailures[provider] = _providerFailures.GetValueOrDefault(provider, 0) + 1;
    }

    private void ResetFailureCount(string provider)
    {
        if (_providerFailures.ContainsKey(provider))
            _providerFailures[provider] = 0;
    }

    private int GetFailureCount(string provider)
    {
        return _providerFailures.GetValueOrDefault(provider, 0);
    }

    /// <summary>
    /// Get current provider statistics
    /// </summary>
    public Dictionary<string, int> GetProviderStatistics()
    {
        return new Dictionary<string, int>(_providerFailures);
    }

    public void Dispose()
    {
        _stooqProvider?.Dispose();
        _yahooProvider?.Dispose();
        _alphaVantageProvider?.Dispose();
    }
}