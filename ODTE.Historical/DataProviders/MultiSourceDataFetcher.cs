using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ODTE.Historical.DataProviders;

/// <summary>
/// Multi-source data fetcher with automatic failover and load balancing
/// </summary>
public class MultiSourceDataFetcher : IDisposable
{
    private readonly List<IOptionsDataProvider> _providers;
    private readonly ILogger<MultiSourceDataFetcher>? _logger;
    private readonly SemaphoreSlim _semaphore;
    private readonly Dictionary<string, ProviderMetrics> _providerMetrics;
    private readonly DataCache _cache;
    
    public MultiSourceDataFetcher(ILogger<MultiSourceDataFetcher>? logger = null)
    {
        _logger = logger;
        _providers = new List<IOptionsDataProvider>();
        _semaphore = new SemaphoreSlim(1, 1);
        _providerMetrics = new Dictionary<string, ProviderMetrics>();
        _cache = new DataCache(TimeSpan.FromMinutes(15)); // 15-minute cache
    }
    
    /// <summary>
    /// Add a data provider to the pool
    /// </summary>
    public void AddProvider(IOptionsDataProvider provider)
    {
        _providers.Add(provider);
        _providerMetrics[provider.ProviderName] = new ProviderMetrics();
        _logger?.LogInformation($"Added provider: {provider.ProviderName} (Priority: {provider.Priority})");
    }
    
    /// <summary>
    /// Fetch options chain with automatic failover
    /// </summary>
    public async Task<OptionsChainData?> GetOptionsChainAsync(
        string symbol,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        // Check cache first
        var cacheKey = $"options_{symbol}_{date:yyyy-MM-dd}";
        var cached = _cache.Get<OptionsChainData>(cacheKey);
        if (cached != null)
        {
            _logger?.LogDebug($"Cache hit for {cacheKey}");
            return cached;
        }
        
        // Get available providers sorted by priority and health
        var availableProviders = await GetAvailableProvidersAsync();
        
        if (!availableProviders.Any())
        {
            _logger?.LogError("No available data providers");
            return null;
        }
        
        // Try each provider in order
        foreach (var provider in availableProviders)
        {
            try
            {
                _logger?.LogInformation($"Fetching options chain from {provider.ProviderName}");
                
                var startTime = DateTime.UtcNow;
                var data = await provider.GetOptionsChainAsync(symbol, date, cancellationToken);
                
                if (data != null)
                {
                    // Record success metrics
                    RecordSuccess(provider.ProviderName, DateTime.UtcNow - startTime);
                    
                    // Validate data quality
                    if (ValidateOptionsData(data))
                    {
                        // Cache the result
                        _cache.Set(cacheKey, data);
                        
                        _logger?.LogInformation($"Successfully fetched options chain from {provider.ProviderName}");
                        return data;
                    }
                    else
                    {
                        _logger?.LogWarning($"Data validation failed for {provider.ProviderName}");
                        RecordFailure(provider.ProviderName, "Data validation failed");
                    }
                }
                else
                {
                    RecordFailure(provider.ProviderName, "No data returned");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error fetching from {provider.ProviderName}");
                RecordFailure(provider.ProviderName, ex.Message);
            }
        }
        
        _logger?.LogError($"Failed to fetch options chain from all providers");
        return null;
    }
    
    /// <summary>
    /// Fetch intraday bars with automatic failover
    /// </summary>
    public async Task<List<MarketDataBar>> GetIntradayBarsAsync(
        string symbol,
        DateTime startDate,
        DateTime endDate,
        TimeSpan? interval = null,
        CancellationToken cancellationToken = default)
    {
        // Check cache first
        var cacheKey = $"bars_{symbol}_{startDate:yyyy-MM-dd}_{endDate:yyyy-MM-dd}_{interval?.TotalMinutes ?? 1}";
        var cached = _cache.Get<List<MarketDataBar>>(cacheKey);
        if (cached != null)
        {
            _logger?.LogDebug($"Cache hit for {cacheKey}");
            return cached;
        }
        
        // Get available providers
        var availableProviders = await GetAvailableProvidersAsync();
        
        if (!availableProviders.Any())
        {
            _logger?.LogError("No available data providers");
            return new List<MarketDataBar>();
        }
        
        // Try each provider in order
        foreach (var provider in availableProviders)
        {
            try
            {
                _logger?.LogInformation($"Fetching intraday bars from {provider.ProviderName}");
                
                var startTime = DateTime.UtcNow;
                var data = await provider.GetIntradayBarsAsync(symbol, startDate, endDate, interval, cancellationToken);
                
                if (data != null && data.Any())
                {
                    // Record success metrics
                    RecordSuccess(provider.ProviderName, DateTime.UtcNow - startTime);
                    
                    // Validate data quality
                    if (ValidateBarsData(data))
                    {
                        // Cache the result
                        _cache.Set(cacheKey, data);
                        
                        _logger?.LogInformation($"Successfully fetched {data.Count} bars from {provider.ProviderName}");
                        return data;
                    }
                    else
                    {
                        _logger?.LogWarning($"Data validation failed for {provider.ProviderName}");
                        RecordFailure(provider.ProviderName, "Data validation failed");
                    }
                }
                else
                {
                    RecordFailure(provider.ProviderName, "No data returned");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error fetching from {provider.ProviderName}");
                RecordFailure(provider.ProviderName, ex.Message);
            }
        }
        
        _logger?.LogError($"Failed to fetch intraday bars from all providers");
        return new List<MarketDataBar>();
    }
    
    /// <summary>
    /// Fetch data from multiple providers in parallel and consolidate
    /// </summary>
    public async Task<ConsolidatedData> GetConsolidatedDataAsync(
        string symbol,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task<(string Provider, OptionsChainData? Data)>>();
        
        foreach (var provider in _providers.Where(p => p.IsAvailable))
        {
            tasks.Add(FetchFromProviderAsync(provider, symbol, date, cancellationToken));
        }
        
        var results = await Task.WhenAll(tasks);
        
        // Consolidate results
        var validResults = results
            .Where(r => r.Data != null && ValidateOptionsData(r.Data))
            .ToList();
        
        if (!validResults.Any())
        {
            return new ConsolidatedData
            {
                Success = false,
                ErrorMessage = "No valid data from any provider"
            };
        }
        
        // Use consensus approach - take median values
        var consolidated = ConsolidateOptionsData(validResults.Select(r => r.Data!).ToList());
        
        return new ConsolidatedData
        {
            Success = true,
            Data = consolidated,
            Sources = validResults.Select(r => r.Provider).ToList(),
            Timestamp = DateTime.UtcNow
        };
    }
    
    /// <summary>
    /// Get provider health status
    /// </summary>
    public async Task<List<ProviderStatus>> GetProviderStatusAsync()
    {
        var statuses = new List<ProviderStatus>();
        
        foreach (var provider in _providers)
        {
            var health = await provider.CheckHealthAsync();
            var rateLimit = provider.GetRateLimitStatus();
            var metrics = _providerMetrics.GetValueOrDefault(provider.ProviderName);
            
            statuses.Add(new ProviderStatus
            {
                ProviderName = provider.ProviderName,
                Priority = provider.Priority,
                IsHealthy = health.IsHealthy,
                LastHealthCheck = health.LastCheck,
                RequestsRemaining = rateLimit.RequestsRemaining,
                IsThrottled = rateLimit.IsThrottled,
                SuccessRate = metrics?.GetSuccessRate() ?? 0,
                AverageResponseTime = metrics?.GetAverageResponseTime() ?? 0,
                TotalRequests = metrics?.TotalRequests ?? 0,
                ConsecutiveFailures = health.ConsecutiveFailures
            });
        }
        
        return statuses.OrderBy(s => s.Priority).ToList();
    }
    
    private async Task<List<IOptionsDataProvider>> GetAvailableProvidersAsync()
    {
        var available = new List<(IOptionsDataProvider Provider, double Score)>();
        
        foreach (var provider in _providers)
        {
            if (!provider.IsAvailable)
                continue;
            
            var rateLimit = provider.GetRateLimitStatus();
            if (rateLimit.IsThrottled)
                continue;
            
            var metrics = _providerMetrics.GetValueOrDefault(provider.ProviderName);
            
            // Calculate provider score (lower is better)
            double score = provider.Priority * 100; // Base score from priority
            
            if (metrics != null)
            {
                // Adjust score based on performance
                score -= metrics.GetSuccessRate() * 50; // Reward success
                score += metrics.ConsecutiveFailures * 20; // Penalize failures
                score += metrics.GetAverageResponseTime() / 100; // Penalize slow responses
            }
            
            available.Add((provider, score));
        }
        
        // Sort by score (lower is better)
        return available
            .OrderBy(x => x.Score)
            .Select(x => x.Provider)
            .ToList();
    }
    
    private async Task<(string Provider, OptionsChainData? Data)> FetchFromProviderAsync(
        IOptionsDataProvider provider,
        string symbol,
        DateTime date,
        CancellationToken cancellationToken)
    {
        try
        {
            var data = await provider.GetOptionsChainAsync(symbol, date, cancellationToken);
            return (provider.ProviderName, data);
        }
        catch
        {
            return (provider.ProviderName, null);
        }
    }
    
    private bool ValidateOptionsData(OptionsChainData data)
    {
        // Basic validation rules
        if (data.Calls.Count == 0 && data.Puts.Count == 0)
            return false;
        
        if (data.UnderlyingPrice <= 0)
            return false;
        
        // Check for reasonable bid/ask spreads
        foreach (var option in data.Calls.Concat(data.Puts))
        {
            if (option.Ask > 0 && option.Bid > 0)
            {
                var spread = (option.Ask - option.Bid) / option.Ask;
                if (spread > 0.5m) // More than 50% spread is suspicious
                    return false;
            }
        }
        
        return true;
    }
    
    private bool ValidateBarsData(List<MarketDataBar> bars)
    {
        if (!bars.Any())
            return false;
        
        // Check for reasonable price movements
        foreach (var bar in bars)
        {
            if (bar.High < bar.Low)
                return false;
            
            if (bar.Open <= 0 || bar.Close <= 0)
                return false;
            
            // Check for extreme price movements (>20% in a bar)
            var maxPrice = Math.Max(bar.High, Math.Max(bar.Open, bar.Close));
            var minPrice = Math.Min(bar.Low, Math.Min(bar.Open, bar.Close));
            
            if (minPrice > 0 && (maxPrice - minPrice) / (double)minPrice > 0.2)
                return false;
        }
        
        return true;
    }
    
    private OptionsChainData ConsolidateOptionsData(List<OptionsChainData> dataList)
    {
        // Take median values for pricing
        var consolidated = new OptionsChainData
        {
            Symbol = dataList.First().Symbol,
            Date = dataList.First().Date,
            UnderlyingPrice = GetMedian(dataList.Select(d => d.UnderlyingPrice)),
            DataSource = "Consolidated",
            LastUpdated = DateTime.UtcNow,
            Calls = new List<OptionsContract>(),
            Puts = new List<OptionsContract>()
        };
        
        // Group options by strike
        var callsByStrike = dataList
            .SelectMany(d => d.Calls)
            .GroupBy(c => c.Strike);
        
        foreach (var group in callsByStrike)
        {
            var options = group.ToList();
            consolidated.Calls.Add(new OptionsContract
            {
                Strike = group.Key,
                Type = "CALL",
                Bid = GetMedian(options.Select(o => o.Bid)),
                Ask = GetMedian(options.Select(o => o.Ask)),
                Last = GetMedian(options.Select(o => o.Last)),
                Volume = (int)options.Average(o => o.Volume),
                OpenInterest = (int)options.Average(o => o.OpenInterest),
                ImpliedVolatility = GetMedian(options.Select(o => o.ImpliedVolatility))
            });
        }
        
        var putsByStrike = dataList
            .SelectMany(d => d.Puts)
            .GroupBy(p => p.Strike);
        
        foreach (var group in putsByStrike)
        {
            var options = group.ToList();
            consolidated.Puts.Add(new OptionsContract
            {
                Strike = group.Key,
                Type = "PUT",
                Bid = GetMedian(options.Select(o => o.Bid)),
                Ask = GetMedian(options.Select(o => o.Ask)),
                Last = GetMedian(options.Select(o => o.Last)),
                Volume = (int)options.Average(o => o.Volume),
                OpenInterest = (int)options.Average(o => o.OpenInterest),
                ImpliedVolatility = GetMedian(options.Select(o => o.ImpliedVolatility))
            });
        }
        
        return consolidated;
    }
    
    private decimal GetMedian(IEnumerable<decimal> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        if (!sorted.Any())
            return 0;
        
        int middle = sorted.Count / 2;
        
        if (sorted.Count % 2 == 0)
            return (sorted[middle - 1] + sorted[middle]) / 2;
        
        return sorted[middle];
    }
    
    private void RecordSuccess(string providerName, TimeSpan responseTime)
    {
        if (_providerMetrics.TryGetValue(providerName, out var metrics))
        {
            metrics.RecordSuccess(responseTime);
        }
    }
    
    private void RecordFailure(string providerName, string error)
    {
        if (_providerMetrics.TryGetValue(providerName, out var metrics))
        {
            metrics.RecordFailure(error);
        }
    }
    
    public void Dispose()
    {
        _semaphore?.Dispose();
        _cache?.Dispose();
    }
}

/// <summary>
/// Provider performance metrics
/// </summary>
internal class ProviderMetrics
{
    private readonly Queue<RequestMetric> _recentRequests = new();
    private readonly TimeSpan _metricWindow = TimeSpan.FromHours(1);
    
    public int TotalRequests { get; private set; }
    public int SuccessfulRequests { get; private set; }
    public int FailedRequests { get; private set; }
    public int ConsecutiveFailures { get; private set; }
    public DateTime LastRequest { get; private set; }
    
    public void RecordSuccess(TimeSpan responseTime)
    {
        TotalRequests++;
        SuccessfulRequests++;
        ConsecutiveFailures = 0;
        LastRequest = DateTime.UtcNow;
        
        _recentRequests.Enqueue(new RequestMetric
        {
            Timestamp = DateTime.UtcNow,
            Success = true,
            ResponseTime = responseTime
        });
        
        CleanupOldMetrics();
    }
    
    public void RecordFailure(string error)
    {
        TotalRequests++;
        FailedRequests++;
        ConsecutiveFailures++;
        LastRequest = DateTime.UtcNow;
        
        _recentRequests.Enqueue(new RequestMetric
        {
            Timestamp = DateTime.UtcNow,
            Success = false,
            Error = error
        });
        
        CleanupOldMetrics();
    }
    
    public double GetSuccessRate()
    {
        if (TotalRequests == 0)
            return 1.0;
        
        return (double)SuccessfulRequests / TotalRequests;
    }
    
    public double GetAverageResponseTime()
    {
        var recentSuccesses = _recentRequests
            .Where(r => r.Success && r.ResponseTime.HasValue)
            .ToList();
        
        if (!recentSuccesses.Any())
            return 0;
        
        return recentSuccesses.Average(r => r.ResponseTime!.Value.TotalMilliseconds);
    }
    
    private void CleanupOldMetrics()
    {
        var cutoff = DateTime.UtcNow - _metricWindow;
        
        while (_recentRequests.Count > 0 && _recentRequests.Peek().Timestamp < cutoff)
        {
            _recentRequests.Dequeue();
        }
    }
    
    private class RequestMetric
    {
        public DateTime Timestamp { get; set; }
        public bool Success { get; set; }
        public TimeSpan? ResponseTime { get; set; }
        public string? Error { get; set; }
    }
}

/// <summary>
/// Consolidated data result
/// </summary>
public class ConsolidatedData
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public OptionsChainData? Data { get; set; }
    public List<string> Sources { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Provider status information
/// </summary>
public class ProviderStatus
{
    public string ProviderName { get; set; } = "";
    public int Priority { get; set; }
    public bool IsHealthy { get; set; }
    public DateTime LastHealthCheck { get; set; }
    public int RequestsRemaining { get; set; }
    public bool IsThrottled { get; set; }
    public double SuccessRate { get; set; }
    public double AverageResponseTime { get; set; }
    public int TotalRequests { get; set; }
    public int ConsecutiveFailures { get; set; }
}