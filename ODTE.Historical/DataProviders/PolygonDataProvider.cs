using System.Text.Json;

namespace ODTE.Historical.DataProviders;

/// <summary>
/// Polygon.io data provider implementation
/// </summary>
public class PolygonDataProvider : IOptionsDataProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly RateLimiter _rateLimiter;
    private ProviderHealthStatus _lastHealthStatus;

    public string ProviderName => "Polygon.io";
    public int Priority => 1; // Primary provider
    public bool IsAvailable => _lastHealthStatus?.IsHealthy ?? true;

    public PolygonDataProvider(string apiKey, HttpClient? httpClient = null)
    {
        _apiKey = apiKey;
        _httpClient = httpClient ?? new HttpClient { BaseAddress = new Uri("https://api.polygon.io/") };
        _rateLimiter = new RateLimiter(5); // 5 requests per minute for free tier
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
            var dateStr = date.ToString("yyyy-MM-dd");
            var response = await _httpClient.GetAsync(
                $"v3/reference/options/contracts?underlying_ticker={symbol}&expiration_date={dateStr}&apiKey={_apiKey}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                await HandleErrorResponse(response);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var data = JsonSerializer.Deserialize<PolygonOptionsResponse>(json);

            if (data?.Results == null || !data.Results.Any())
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

            // Get quotes for each contract
            foreach (var contract in data.Results)
            {
                var optionData = await GetOptionQuoteAsync(contract.Ticker, date, cancellationToken);
                if (optionData != null)
                {
                    if (contract.ContractType?.ToUpper() == "CALL")
                        chainData.Calls.Add(optionData);
                    else if (contract.ContractType?.ToUpper() == "PUT")
                        chainData.Puts.Add(optionData);
                }
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
            throw;
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
            var multiplier = interval?.TotalMinutes ?? 1;
            var from = startDate.ToString("yyyy-MM-dd");
            var to = endDate.ToString("yyyy-MM-dd");

            var response = await _httpClient.GetAsync(
                $"v2/aggs/ticker/{symbol}/range/{multiplier}/minute/{from}/{to}?apiKey={_apiKey}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                await HandleErrorResponse(response);
                return new List<MarketDataBar>();
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var data = JsonSerializer.Deserialize<PolygonAggregatesResponse>(json);

            if (data?.Results == null)
                return new List<MarketDataBar>();

            return data.Results.Select(r => new MarketDataBar
            {
                Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(r.Timestamp).DateTime,
                Open = r.Open,
                High = r.High,
                Low = r.Low,
                Close = r.Close,
                Volume = r.Volume,
                VWAP = (r.Open + r.High + r.Low + r.Close) / 4
            }).ToList();
        }
        catch (Exception ex)
        {
            RecordFailure(ex.Message);
            throw;
        }
    }

    public async Task<ProviderHealthStatus> CheckHealthAsync()
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var response = await _httpClient.GetAsync($"v1/marketstatus/now?apiKey={_apiKey}");
            var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

            _lastHealthStatus = new ProviderHealthStatus
            {
                IsHealthy = response.IsSuccessStatusCode,
                LastCheck = DateTime.UtcNow,
                ResponseTimeMs = responseTime,
                ConsecutiveFailures = response.IsSuccessStatusCode ? 0 : _lastHealthStatus.ConsecutiveFailures + 1,
                ErrorMessage = response.IsSuccessStatusCode ? null : $"HTTP {response.StatusCode}"
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

    private async Task<OptionsContract?> GetOptionQuoteAsync(
        string optionSymbol,
        DateTime date,
        CancellationToken cancellationToken)
    {
        await _rateLimiter.WaitAsync(cancellationToken);

        var dateStr = date.ToString("yyyy-MM-dd");
        var response = await _httpClient.GetAsync(
            $"v1/open-close/{optionSymbol}/{dateStr}?apiKey={_apiKey}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var data = JsonSerializer.Deserialize<PolygonQuoteResponse>(json);

        if (data == null)
            return null;

        return new OptionsContract
        {
            Symbol = optionSymbol,
            Bid = (decimal)(data.Bid ?? 0),
            Ask = (decimal)(data.Ask ?? 0),
            Last = (decimal)(data.Last ?? 0),
            Volume = data.Volume ?? 0
        };
    }

    private async Task<decimal?> GetUnderlyingPriceAsync(
        string symbol,
        DateTime date,
        CancellationToken cancellationToken)
    {
        await _rateLimiter.WaitAsync(cancellationToken);

        var dateStr = date.ToString("yyyy-MM-dd");
        var response = await _httpClient.GetAsync(
            $"v1/open-close/{symbol}/{dateStr}?apiKey={_apiKey}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var data = JsonSerializer.Deserialize<PolygonQuoteResponse>(json);

        return (decimal?)(data?.Close ?? 0);
    }

    private async Task HandleErrorResponse(HttpResponseMessage response)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            var retryAfter = response.Headers.RetryAfter?.Delta;
            _rateLimiter.SetThrottled(retryAfter ?? TimeSpan.FromMinutes(1));
        }

        RecordFailure($"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
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
internal class PolygonOptionsResponse
{
    public List<PolygonOptionsContract>? Results { get; set; }
}

internal class PolygonOptionsContract
{
    public string? Ticker { get; set; }
    public string? ContractType { get; set; }
    public decimal? StrikePrice { get; set; }
    public DateTime? ExpirationDate { get; set; }
}

internal class PolygonAggregatesResponse
{
    public List<PolygonAggregate>? Results { get; set; }
}

internal class PolygonAggregate
{
    public long Timestamp { get; set; }
    public double Open { get; set; }
    public double High { get; set; }
    public double Low { get; set; }
    public double Close { get; set; }
    public long Volume { get; set; }
}

internal class PolygonQuoteResponse
{
    public double? Open { get; set; }
    public double? High { get; set; }
    public double? Low { get; set; }
    public double? Close { get; set; }
    public double? Bid { get; set; }
    public double? Ask { get; set; }
    public double? Last { get; set; }
    public int? Volume { get; set; }
}