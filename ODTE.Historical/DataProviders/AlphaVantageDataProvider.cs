using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ODTE.Historical.DataProviders;

/// <summary>
/// Alpha Vantage data provider implementation (free tier)
/// </summary>
public class AlphaVantageDataProvider : IOptionsDataProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly RateLimiter _rateLimiter;
    private ProviderHealthStatus _lastHealthStatus;
    
    public string ProviderName => "Alpha Vantage";
    public int Priority => 2; // Secondary provider
    public bool IsAvailable => _lastHealthStatus?.IsHealthy ?? true;
    
    public AlphaVantageDataProvider(string apiKey, HttpClient? httpClient = null)
    {
        _apiKey = apiKey;
        _httpClient = httpClient ?? new HttpClient { BaseAddress = new Uri("https://www.alphavantage.co/") };
        _rateLimiter = new RateLimiter(5, TimeSpan.FromMinutes(1)); // 5 requests per minute
        _lastHealthStatus = new ProviderHealthStatus { IsHealthy = true, LastCheck = DateTime.UtcNow };
    }
    
    public async Task<OptionsChainData?> GetOptionsChainAsync(
        string symbol, 
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        await _rateLimiter.WaitAsync(cancellationToken);
        
        try
        {
            // Alpha Vantage doesn't provide historical options chains directly
            // We'll get current chain and filter by expiration
            var response = await _httpClient.GetAsync(
                $"query?function=HISTORICAL_OPTIONS&symbol={symbol}&date={date:yyyy-MM-dd}&apikey={_apiKey}",
                cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                await HandleErrorResponse(response);
                return null;
            }
            
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            
            // Check for rate limit message
            if (json.Contains("Note") || json.Contains("Thank you"))
            {
                _rateLimiter.SetThrottled(TimeSpan.FromMinutes(1));
                return null;
            }
            
            var data = JsonSerializer.Deserialize<AlphaVantageOptionsResponse>(json);
            
            if (data?.Data == null)
                return null;
            
            var chainData = new OptionsChainData
            {
                Symbol = symbol,
                Date = date,
                DataSource = ProviderName,
                LastUpdated = DateTime.UtcNow,
                Calls = new List<OptionsContract>(),
                Puts = new List<OptionsContract>()
            };
            
            foreach (var option in data.Data)
            {
                var contract = new OptionsContract
                {
                    Symbol = option.ContractId ?? "",
                    Strike = option.Strike ?? 0,
                    Type = option.Type ?? "",
                    Bid = option.Bid ?? 0,
                    Ask = option.Ask ?? 0,
                    Last = option.Last ?? 0,
                    Volume = option.Volume ?? 0,
                    OpenInterest = option.OpenInterest ?? 0,
                    ImpliedVolatility = option.ImpliedVolatility ?? 0
                };
                
                if (contract.Type.ToUpper() == "CALL")
                    chainData.Calls.Add(contract);
                else if (contract.Type.ToUpper() == "PUT")
                    chainData.Puts.Add(contract);
            }
            
            // Get underlying price
            var underlyingData = await GetUnderlyingPriceAsync(symbol, date, cancellationToken);
            if (underlyingData != null)
            {
                chainData.UnderlyingPrice = underlyingData.Value;
            }
            
            return chainData;
        }
        catch (Exception ex)
        {
            RecordFailure(ex.Message);
            return null;
        }
    }
    
    public async Task<List<MarketDataBar>> GetIntradayBarsAsync(
        string symbol,
        DateTime startDate,
        DateTime endDate,
        TimeSpan? interval = null,
        CancellationToken cancellationToken = default)
    {
        await _rateLimiter.WaitAsync(cancellationToken);
        
        try
        {
            var intervalStr = interval?.TotalMinutes switch
            {
                1 => "1min",
                5 => "5min",
                15 => "15min",
                30 => "30min",
                60 => "60min",
                _ => "1min"
            };
            
            var response = await _httpClient.GetAsync(
                $"query?function=TIME_SERIES_INTRADAY&symbol={symbol}&interval={intervalStr}&outputsize=full&apikey={_apiKey}",
                cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                await HandleErrorResponse(response);
                return new List<MarketDataBar>();
            }
            
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            
            // Check for rate limit message
            if (json.Contains("Note") || json.Contains("Thank you"))
            {
                _rateLimiter.SetThrottled(TimeSpan.FromMinutes(1));
                return new List<MarketDataBar>();
            }
            
            var data = JsonSerializer.Deserialize<AlphaVantageTimeSeriesResponse>(json);
            
            if (data?.TimeSeries == null)
                return new List<MarketDataBar>();
            
            var bars = new List<MarketDataBar>();
            
            foreach (var kvp in data.TimeSeries)
            {
                if (DateTime.TryParse(kvp.Key, out var timestamp))
                {
                    if (timestamp >= startDate && timestamp <= endDate)
                    {
                        bars.Add(new MarketDataBar
                        {
                            Timestamp = timestamp,
                            Open = double.Parse(kvp.Value.Open ?? "0"),
                            High = double.Parse(kvp.Value.High ?? "0"),
                            Low = double.Parse(kvp.Value.Low ?? "0"),
                            Close = double.Parse(kvp.Value.Close ?? "0"),
                            Volume = long.Parse(kvp.Value.Volume ?? "0"),
                            VWAP = (double.Parse(kvp.Value.Open ?? "0") + double.Parse(kvp.Value.High ?? "0") + double.Parse(kvp.Value.Low ?? "0") + double.Parse(kvp.Value.Close ?? "0")) / 4
                        });
                    }
                }
            }
            
            return bars.OrderBy(b => b.Timestamp).ToList();
        }
        catch (Exception ex)
        {
            RecordFailure(ex.Message);
            return new List<MarketDataBar>();
        }
    }
    
    public async Task<ProviderHealthStatus> CheckHealthAsync()
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            // Simple quote request to check API availability
            var response = await _httpClient.GetAsync(
                $"query?function=GLOBAL_QUOTE&symbol=SPY&apikey={_apiKey}");
            
            var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            var content = await response.Content.ReadAsStringAsync();
            
            var isHealthy = response.IsSuccessStatusCode && 
                           !content.Contains("Note") && 
                           !content.Contains("Error");
            
            _lastHealthStatus = new ProviderHealthStatus
            {
                IsHealthy = isHealthy,
                LastCheck = DateTime.UtcNow,
                ResponseTimeMs = responseTime,
                ConsecutiveFailures = isHealthy ? 0 : _lastHealthStatus.ConsecutiveFailures + 1,
                ErrorMessage = isHealthy ? null : "API limit or error"
            };
        }
        catch (Exception ex)
        {
            _lastHealthStatus = new ProviderHealthStatus
            {
                IsHealthy = false,
                LastCheck = DateTime.UtcNow,
                ResponseTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds,
                ConsecutiveFailures = _lastHealthStatus.ConsecutiveFailures + 1,
                ErrorMessage = ex.Message
            };
        }
        
        return _lastHealthStatus;
    }
    
    public RateLimitStatus GetRateLimitStatus()
    {
        return _rateLimiter.GetStatus();
    }
    
    private async Task<decimal?> GetUnderlyingPriceAsync(
        string symbol,
        DateTime date,
        CancellationToken cancellationToken)
    {
        await _rateLimiter.WaitAsync(cancellationToken);
        
        var response = await _httpClient.GetAsync(
            $"query?function=TIME_SERIES_DAILY&symbol={symbol}&apikey={_apiKey}",
            cancellationToken);
        
        if (!response.IsSuccessStatusCode)
            return null;
        
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        
        if (json.Contains("Note") || json.Contains("Thank you"))
        {
            _rateLimiter.SetThrottled(TimeSpan.FromMinutes(1));
            return null;
        }
        
        var data = JsonSerializer.Deserialize<AlphaVantageDailyResponse>(json);
        
        var dateKey = date.ToString("yyyy-MM-dd");
        if (data?.TimeSeries?.ContainsKey(dateKey) == true)
        {
            return decimal.Parse(data.TimeSeries[dateKey].Close ?? "0");
        }
        
        return null;
    }
    
    private async Task HandleErrorResponse(HttpResponseMessage response)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            _rateLimiter.SetThrottled(TimeSpan.FromMinutes(1));
        }
        
        RecordFailure($"HTTP {response.StatusCode}");
    }
    
    private void RecordFailure(string error)
    {
        _lastHealthStatus = new ProviderHealthStatus
        {
            IsHealthy = false,
            LastCheck = DateTime.UtcNow,
            ConsecutiveFailures = _lastHealthStatus.ConsecutiveFailures + 1,
            ErrorMessage = error
        };
    }
}

// Response DTOs
internal class AlphaVantageOptionsResponse
{
    public List<AlphaVantageOption>? Data { get; set; }
}

internal class AlphaVantageOption
{
    public string? ContractId { get; set; }
    public string? Type { get; set; }
    public decimal? Strike { get; set; }
    public decimal? Bid { get; set; }
    public decimal? Ask { get; set; }
    public decimal? Last { get; set; }
    public int? Volume { get; set; }
    public int? OpenInterest { get; set; }
    public decimal? ImpliedVolatility { get; set; }
}

internal class AlphaVantageTimeSeriesResponse
{
    public Dictionary<string, AlphaVantageBar>? TimeSeries { get; set; }
}

internal class AlphaVantageDailyResponse
{
    public Dictionary<string, AlphaVantageBar>? TimeSeries { get; set; }
}

internal class AlphaVantageBar
{
    public string? Open { get; set; }
    public string? High { get; set; }
    public string? Low { get; set; }
    public string? Close { get; set; }
    public string? Volume { get; set; }
}