using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ODTE.Historical.DataProviders
{
    /// <summary>
    /// Alpha Vantage API provider as fallback for Yahoo Finance
    /// Requires free API key but provides reliable historical data
    /// </summary>
    public class AlphaVantageProvider : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly SemaphoreSlim _rateLimiter;
        private readonly string _apiKey;
        private const int MAX_REQUESTS_PER_MINUTE = 5; // Alpha Vantage free tier limit
        
        public AlphaVantageProvider(string apiKey = "demo")
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", 
                "ODTE-DataAcquisition/1.0 Educational Research");
            _apiKey = apiKey;
            _rateLimiter = new SemaphoreSlim(MAX_REQUESTS_PER_MINUTE, MAX_REQUESTS_PER_MINUTE);
            
            // Reset rate limiter every minute
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromMinutes(1));
                    _rateLimiter.Release(MAX_REQUESTS_PER_MINUTE - _rateLimiter.CurrentCount);
                }
            });
        }
        
        /// <summary>
        /// Download historical data for a symbol and date range
        /// Note: Alpha Vantage returns full historical data, not date-ranged
        /// </summary>
        public async Task<List<HistoricalDataBar>> GetHistoricalDataAsync(
            string symbol,
            DateTime startDate,
            DateTime endDate)
        {
            await _rateLimiter.WaitAsync();
            
            try
            {
                // Alpha Vantage API URL for daily adjusted data
                var url = $"https://www.alphavantage.co/query?" +
                         $"function=TIME_SERIES_DAILY_ADJUSTED&symbol={symbol}" +
                         $"&outputsize=full&apikey={_apiKey}";
                
                Console.WriteLine($"🔄 Downloading {symbol} from Alpha Vantage (full history)...");
                
                var response = await _httpClient.GetStringAsync(url);
                var bars = ParseAlphaVantageJson(response, symbol, startDate, endDate);
                
                Console.WriteLine($"✅ Downloaded {bars.Count} bars for {symbol} in date range");
                return bars;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Alpha Vantage error for {symbol}: {ex.Message}");
                return new List<HistoricalDataBar>();
            }
        }
        
        /// <summary>
        /// Parse Alpha Vantage JSON format and filter by date range
        /// </summary>
        private List<HistoricalDataBar> ParseAlphaVantageJson(
            string jsonData, 
            string symbol, 
            DateTime startDate, 
            DateTime endDate)
        {
            var bars = new List<HistoricalDataBar>();
            
            try
            {
                using var document = JsonDocument.Parse(jsonData);
                var root = document.RootElement;
                
                // Check for API errors
                if (root.TryGetProperty("Error Message", out _))
                {
                    Console.WriteLine($"⚠️ Alpha Vantage API error for {symbol}");
                    return bars;
                }
                
                if (root.TryGetProperty("Note", out _))
                {
                    Console.WriteLine($"⚠️ Alpha Vantage rate limit hit for {symbol}");
                    return bars;
                }
                
                // Get time series data
                if (!root.TryGetProperty("Time Series (Daily)", out var timeSeries))
                {
                    Console.WriteLine($"⚠️ No time series data found for {symbol}");
                    return bars;
                }
                
                foreach (var dayData in timeSeries.EnumerateObject())
                {
                    if (DateTime.TryParse(dayData.Name, out var date) &&
                        date >= startDate && date <= endDate)
                    {
                        var values = dayData.Value;
                        
                        if (values.TryGetProperty("1. open", out var openElement) &&
                            values.TryGetProperty("2. high", out var highElement) &&
                            values.TryGetProperty("3. low", out var lowElement) &&
                            values.TryGetProperty("4. close", out var closeElement) &&
                            values.TryGetProperty("6. volume", out var volumeElement) &&
                            values.TryGetProperty("5. adjusted close", out var adjCloseElement))
                        {
                            if (double.TryParse(openElement.GetString(), out var open) &&
                                double.TryParse(highElement.GetString(), out var high) &&
                                double.TryParse(lowElement.GetString(), out var low) &&
                                double.TryParse(closeElement.GetString(), out var close) &&
                                long.TryParse(volumeElement.GetString(), out var volume) &&
                                double.TryParse(adjCloseElement.GetString(), out var adjClose))
                            {
                                bars.Add(new HistoricalDataBar
                                {
                                    Timestamp = date,
                                    Open = open,
                                    High = high,
                                    Low = low,
                                    Close = close,
                                    Volume = volume,
                                    AdjustedClose = adjClose,
                                    VWAP = (high + low + close) / 3 // Approximation
                                });
                            }
                        }
                    }
                }
                
                // Sort by date (oldest first)
                bars.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
                
                Console.WriteLine($"✅ Parsed {bars.Count} Alpha Vantage bars for {symbol}");
                return bars;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error parsing Alpha Vantage data for {symbol}: {ex.Message}");
                return bars;
            }
        }
        
        public void Dispose()
        {
            _httpClient?.Dispose();
            _rateLimiter?.Dispose();
        }
    }
}