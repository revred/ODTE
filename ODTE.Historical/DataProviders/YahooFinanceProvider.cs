using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ODTE.Historical.DataProviders
{
    /// <summary>
    /// Yahoo Finance API provider for authentic historical market data
    /// Free tier with no API key required - perfect for historical data acquisition
    /// </summary>
    public class YahooFinanceProvider : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly SemaphoreSlim _rateLimiter;
        private const int MAX_REQUESTS_PER_MINUTE = 30; // More conservative rate limiting
        
        public YahooFinanceProvider()
        {
            _httpClient = new HttpClient();
            // Enhanced headers to avoid 401 errors
            _httpClient.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
            _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
            _httpClient.DefaultRequestHeaders.Add("DNT", "1");
            _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            _httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
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
        /// </summary>
        public async Task<List<HistoricalDataBar>> GetHistoricalDataAsync(
            string symbol, 
            DateTime startDate, 
            DateTime endDate,
            string interval = "1d")
        {
            await _rateLimiter.WaitAsync();
            
            // Retry logic with exponential backoff
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    // Yahoo Finance uses Unix timestamps
                    var startUnix = ((DateTimeOffset)startDate).ToUnixTimeSeconds();
                    var endUnix = ((DateTimeOffset)endDate).ToUnixTimeSeconds();
                    
                    // Yahoo Finance historical data URL
                    var url = $"https://query1.finance.yahoo.com/v7/finance/download/{symbol}" +
                             $"?period1={startUnix}&period2={endUnix}&interval={interval}" +
                             $"&events=history&includeAdjustedClose=true";
                    
                    Console.WriteLine($"🔄 Downloading {symbol} data: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd} (attempt {attempt})");
                    
                    var response = await _httpClient.GetStringAsync(url);
                    return ParseYahooFinanceCsv(response, symbol);
                }
                catch (HttpRequestException ex) when (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
                {
                    Console.WriteLine($"⚠️ Yahoo Finance API access denied (attempt {attempt}/3): {ex.Message}");
                    if (attempt < 3)
                    {
                        var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt) * 5); // 10s, 20s, 40s
                        Console.WriteLine($"   Waiting {delay.TotalSeconds}s before retry...");
                        await Task.Delay(delay);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error downloading {symbol}: {ex.Message}");
                    if (attempt < 3)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2 * attempt));
                    }
                }
            }
            
            Console.WriteLine($"💡 Yahoo Finance failed for {symbol}. Consider alternative data sources.");
            return new List<HistoricalDataBar>();
        }
        
        /// <summary>
        /// Parse Yahoo Finance CSV format into our data structure
        /// </summary>
        private List<HistoricalDataBar> ParseYahooFinanceCsv(string csvData, string symbol)
        {
            var bars = new List<HistoricalDataBar>();
            var lines = csvData.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            // Skip header line
            for (int i = 1; i < lines.Length; i++)
            {
                var fields = lines[i].Split(',');
                if (fields.Length < 6) continue;
                
                try
                {
                    var date = DateTime.Parse(fields[0], CultureInfo.InvariantCulture);
                    var open = double.Parse(fields[1], CultureInfo.InvariantCulture);
                    var high = double.Parse(fields[2], CultureInfo.InvariantCulture);
                    var low = double.Parse(fields[3], CultureInfo.InvariantCulture);
                    var close = double.Parse(fields[4], CultureInfo.InvariantCulture);
                    var volume = long.Parse(fields[5], CultureInfo.InvariantCulture);
                    var adjClose = fields.Length > 6 ? double.Parse(fields[6], CultureInfo.InvariantCulture) : close;
                    
                    bars.Add(new HistoricalDataBar
                    {
                        Symbol = symbol,
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
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Error parsing line {i}: {ex.Message}");
                }
            }
            
            Console.WriteLine($"✅ Parsed {bars.Count} bars for {symbol}");
            return bars;
        }
        
        /// <summary>
        /// Download data in chunks to manage memory and rate limits
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
                    RecordsProcessed = allBars.Count,
                    Status = $"Downloaded {chunkBars.Count} bars for {currentDate:yyyy-MM-dd} to {chunkEnd:yyyy-MM-dd}"
                });
                
                currentDate = chunkEnd.AddDays(1);
                
                // Add delay between chunks to be respectful to Yahoo Finance
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
            
            return allBars;
        }
        
        public void Dispose()
        {
            _httpClient?.Dispose();
            _rateLimiter?.Dispose();
        }
    }
    
    /// <summary>
    /// Historical data bar structure
    /// </summary>
    public class HistoricalDataBar
    {
        public string Symbol { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public long Volume { get; set; }
        public double AdjustedClose { get; set; }
        public double VWAP { get; set; }
    }
    
    /// <summary>
    /// Progress tracking for data acquisition
    /// </summary>
    public class DataAcquisitionProgress
    {
        public string Symbol { get; set; } = "";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime CurrentDate { get; set; }
        public double ProgressPercent { get; set; }
        public int RecordsProcessed { get; set; }
        public string Status { get; set; } = "";
    }
}