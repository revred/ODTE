using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ODTE.Historical.DataProviders;

/// <summary>
/// Twelve Data provider implementation (free tier available)
/// </summary>
public class TwelveDataProvider : IOptionsDataProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly RateLimiter _rateLimiter;
    private ProviderHealthStatus _lastHealthStatus;
    
    public string ProviderName => "Twelve Data";
    public int Priority => 3; // Tertiary provider
    public bool IsAvailable => _lastHealthStatus?.IsHealthy ?? true;
    
    public TwelveDataProvider(string apiKey, HttpClient? httpClient = null)
    {
        _apiKey = apiKey;
        _httpClient = httpClient ?? new HttpClient { BaseAddress = new Uri("https://api.twelvedata.com/") };
        _rateLimiter = new RateLimiter(8, TimeSpan.FromMinutes(1)); // 8 requests per minute for free tier
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
            // Twelve Data provides options chain endpoint
            var response = await _httpClient.GetAsync(
                $"options/chain?symbol={symbol}&expiration_date={date:yyyy-MM-dd}&apikey={_apiKey}",
                cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                await HandleErrorResponse(response);
                return null;
            }
            
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var data = JsonSerializer.Deserialize<TwelveDataOptionsResponse>(json);
            
            if (data?.Status != "ok" || data.Data == null)
                return null;
            
            var chainData = new OptionsChainData
            {
                Symbol = symbol,
                Date = date,
                DataSource = ProviderName,
                LastUpdated = DateTime.UtcNow,
                UnderlyingPrice = data.Meta?.UnderlyingPrice ?? 0,
                Calls = new List<OptionsContract>(),
                Puts = new List<OptionsContract>()
            };
            
            foreach (var expiration in data.Data)
            {
                if (expiration.Calls != null)
                {
                    chainData.Calls.AddRange(expiration.Calls.Select(c => new OptionsContract
                    {
                        Symbol = c.ContractSymbol ?? "",
                        Strike = c.Strike ?? 0,
                        Type = "CALL",
                        ExpirationDate = DateTime.Parse(expiration.ExpirationDate ?? date.ToString()),
                        Bid = c.Bid ?? 0,
                        Ask = c.Ask ?? 0,
                        Last = c.Last ?? 0,
                        Volume = c.Volume ?? 0,
                        OpenInterest = c.OpenInterest ?? 0,
                        ImpliedVolatility = c.ImpliedVolatility ?? 0,
                        Delta = c.Greeks?.Delta ?? 0,
                        Gamma = c.Greeks?.Gamma ?? 0,
                        Theta = c.Greeks?.Theta ?? 0,
                        Vega = c.Greeks?.Vega ?? 0
                    }));
                }
                
                if (expiration.Puts != null)
                {
                    chainData.Puts.AddRange(expiration.Puts.Select(p => new OptionsContract
                    {
                        Symbol = p.ContractSymbol ?? "",
                        Strike = p.Strike ?? 0,
                        Type = "PUT",
                        ExpirationDate = DateTime.Parse(expiration.ExpirationDate ?? date.ToString()),
                        Bid = p.Bid ?? 0,
                        Ask = p.Ask ?? 0,
                        Last = p.Last ?? 0,
                        Volume = p.Volume ?? 0,
                        OpenInterest = p.OpenInterest ?? 0,
                        ImpliedVolatility = p.ImpliedVolatility ?? 0,
                        Delta = p.Greeks?.Delta ?? 0,
                        Gamma = p.Greeks?.Gamma ?? 0,
                        Theta = p.Greeks?.Theta ?? 0,
                        Vega = p.Greeks?.Vega ?? 0
                    }));
                }
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
                60 => "1h",
                240 => "4h",
                _ => "1min"
            };
            
            var response = await _httpClient.GetAsync(
                $"time_series?symbol={symbol}&interval={intervalStr}" +
                $"&start_date={startDate:yyyy-MM-dd HH:mm:ss}&end_date={endDate:yyyy-MM-dd HH:mm:ss}" +
                $"&apikey={_apiKey}",
                cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                await HandleErrorResponse(response);
                return new List<MarketDataBar>();
            }
            
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var data = JsonSerializer.Deserialize<TwelveDataTimeSeriesResponse>(json);
            
            if (data?.Status != "ok" || data.Values == null)
                return new List<MarketDataBar>();
            
            return data.Values.Select(v => new MarketDataBar
            {
                Timestamp = DateTime.Parse(v.Datetime ?? ""),
                Open = double.Parse(v.Open ?? "0"),
                High = double.Parse(v.High ?? "0"),
                Low = double.Parse(v.Low ?? "0"),
                Close = double.Parse(v.Close ?? "0"),
                Volume = long.Parse(v.Volume ?? "0"),
                VWAP = (double.Parse(v.Open ?? "0") + double.Parse(v.High ?? "0") + double.Parse(v.Low ?? "0") + double.Parse(v.Close ?? "0")) / 4
            }).OrderBy(b => b.Timestamp).ToList();
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
            var response = await _httpClient.GetAsync($"api_usage?apikey={_apiKey}");
            var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            
            var content = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<TwelveDataUsageResponse>(content);
            
            var isHealthy = response.IsSuccessStatusCode && data?.Status == "ok";
            
            _lastHealthStatus = new ProviderHealthStatus
            {
                IsHealthy = isHealthy,
                LastCheck = DateTime.UtcNow,
                ResponseTimeMs = responseTime,
                ConsecutiveFailures = isHealthy ? 0 : _lastHealthStatus.ConsecutiveFailures + 1,
                ErrorMessage = isHealthy ? null : data?.Message ?? "Unknown error"
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
    
    private async Task HandleErrorResponse(HttpResponseMessage response)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            _rateLimiter.SetThrottled(TimeSpan.FromMinutes(1));
        }
        
        var content = await response.Content.ReadAsStringAsync();
        RecordFailure($"HTTP {response.StatusCode}: {content}");
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
internal class TwelveDataOptionsResponse
{
    public string? Status { get; set; }
    public TwelveDataOptionsMeta? Meta { get; set; }
    public List<TwelveDataOptionsExpiration>? Data { get; set; }
}

internal class TwelveDataOptionsMeta
{
    public decimal? UnderlyingPrice { get; set; }
}

internal class TwelveDataOptionsExpiration
{
    public string? ExpirationDate { get; set; }
    public List<TwelveDataOption>? Calls { get; set; }
    public List<TwelveDataOption>? Puts { get; set; }
}

internal class TwelveDataOption
{
    public string? ContractSymbol { get; set; }
    public decimal? Strike { get; set; }
    public decimal? Bid { get; set; }
    public decimal? Ask { get; set; }
    public decimal? Last { get; set; }
    public int? Volume { get; set; }
    public int? OpenInterest { get; set; }
    public decimal? ImpliedVolatility { get; set; }
    public TwelveDataGreeks? Greeks { get; set; }
}

internal class TwelveDataGreeks
{
    public decimal? Delta { get; set; }
    public decimal? Gamma { get; set; }
    public decimal? Theta { get; set; }
    public decimal? Vega { get; set; }
}

internal class TwelveDataTimeSeriesResponse
{
    public string? Status { get; set; }
    public List<TwelveDataBar>? Values { get; set; }
}

internal class TwelveDataBar
{
    public string? Datetime { get; set; }
    public string? Open { get; set; }
    public string? High { get; set; }
    public string? Low { get; set; }
    public string? Close { get; set; }
    public string? Volume { get; set; }
}

internal class TwelveDataUsageResponse
{
    public string? Status { get; set; }
    public string? Message { get; set; }
    public int? DailyUsage { get; set; }
    public int? MonthlyUsage { get; set; }
}