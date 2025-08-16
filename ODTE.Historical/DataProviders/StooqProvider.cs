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
    /// Stooq.com data provider - reliable free historical data source
    /// No API key required, excellent for US ETFs and indices
    /// Format: https://stooq.com/q/d/l/?s=SPY&i=d
    /// </summary>
    public class StooqProvider : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly SemaphoreSlim _rateLimiter;
        private const int MAX_REQUESTS_PER_MINUTE = 30; // Conservative rate limiting
        
        public StooqProvider()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
            _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
            _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
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
        /// Download historical data for a symbol from Stooq
        /// Stooq returns ALL historical data available, then we filter by date range
        /// </summary>
        public async Task<List<HistoricalDataBar>> GetHistoricalDataAsync(
            string symbol,
            DateTime startDate,
            DateTime endDate)
        {
            await _rateLimiter.WaitAsync();
            
            // Retry logic with exponential backoff
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    // Clean symbol for Stooq format
                    var stooqSymbol = ConvertToStooqSymbol(symbol);
                    
                    // Stooq URL format: https://stooq.com/q/d/l/?s=SYMBOL&i=d
                    var url = $"https://stooq.com/q/d/l/?s={stooqSymbol}&i=d";
                    
                    Console.WriteLine($"🔄 Downloading {symbol} from Stooq (attempt {attempt})...");
                    
                    var response = await _httpClient.GetStringAsync(url);
                    var bars = ParseStooqCsv(response, symbol, startDate, endDate);
                    
                    if (bars.Count > 0)
                    {
                        Console.WriteLine($"✅ Downloaded {bars.Count} bars from Stooq for {symbol}");
                        return bars;
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ Stooq returned no data for {symbol} in date range");
                        return bars;
                    }
                }
                catch (HttpRequestException ex) when (ex.Message.Contains("404") || ex.Message.Contains("Not Found"))
                {
                    Console.WriteLine($"⚠️ Symbol {symbol} not found on Stooq");
                    return new List<HistoricalDataBar>();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Stooq error for {symbol} (attempt {attempt}/3): {ex.Message}");
                    if (attempt < 3)
                    {
                        var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt) * 2); // 4s, 8s, 16s
                        Console.WriteLine($"   Waiting {delay.TotalSeconds}s before retry...");
                        await Task.Delay(delay);
                    }
                }
            }
            
            Console.WriteLine($"💔 Stooq failed for {symbol} after 3 attempts");
            return new List<HistoricalDataBar>();
        }
        
        /// <summary>
        /// Convert symbol to Stooq format
        /// US stocks require .US suffix, indices are different
        /// </summary>
        private string ConvertToStooqSymbol(string symbol)
        {
            // Handle common symbol conversions
            return symbol switch
            {
                "^VIX" => "^VIX",          // VIX index (keep ^ for indices)
                "^SPX" => "^SPX",          // S&P 500 index
                "^DJI" => "^DJI",          // Dow Jones index
                "^IXIC" => "^IXIC",        // NASDAQ index
                "SPY" => "SPY.US",         // US ETFs need .US suffix
                "QQQ" => "QQQ.US",         // PowerShares QQQ
                "IWM" => "IWM.US",         // iShares Russell 2000
                "XSP" => "XSP.TO",         // Canadian ETF (Toronto exchange)
                _ => symbol.Contains("^") ? symbol : $"{symbol.ToUpper()}.US" // Add .US for US stocks
            };
        }
        
        /// <summary>
        /// Parse Stooq CSV format and filter by date range
        /// Stooq CSV format: Date,Open,High,Low,Close,Volume
        /// </summary>
        private List<HistoricalDataBar> ParseStooqCsv(string csvData, string symbol, DateTime startDate, DateTime endDate)
        {
            var bars = new List<HistoricalDataBar>();
            var lines = csvData.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            if (lines.Length <= 1)
            {
                Console.WriteLine($"⚠️ No data in Stooq response for {symbol}");
                return bars;
            }
            
            // Skip header line (Date,Open,High,Low,Close,Volume)
            for (int i = 1; i < lines.Length; i++)
            {
                try
                {
                    var parts = lines[i].Split(',');
                    if (parts.Length >= 6)
                    {
                        var dateStr = parts[0].Trim();
                        var openStr = parts[1].Trim();
                        var highStr = parts[2].Trim();
                        var lowStr = parts[3].Trim();
                        var closeStr = parts[4].Trim();
                        var volumeStr = parts[5].Trim();
                        
                        // Parse date (Stooq uses YYYY-MM-DD format)
                        if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) &&
                            date >= startDate && date <= endDate &&
                            double.TryParse(openStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var open) &&
                            double.TryParse(highStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var high) &&
                            double.TryParse(lowStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var low) &&
                            double.TryParse(closeStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var close) &&
                            long.TryParse(volumeStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var volume))
                        {
                            bars.Add(new HistoricalDataBar
                            {
                                Timestamp = date,
                                Open = open,
                                High = high,
                                Low = low,
                                Close = close,
                                Volume = volume,
                                AdjustedClose = close, // Stooq doesn't provide adjusted close, use close
                                VWAP = (high + low + close) / 3 // Approximation
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Error parsing Stooq line {i} for {symbol}: {ex.Message}");
                }
            }
            
            // Sort by date (oldest first)
            bars.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
            
            Console.WriteLine($"✅ Parsed {bars.Count} Stooq bars for {symbol} in date range {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            return bars;
        }
        
        /// <summary>
        /// Download data in chunks to manage memory and rate limits
        /// Note: Stooq returns full history, so we download once and filter by chunks
        /// </summary>
        public async Task<List<HistoricalDataBar>> GetHistoricalDataChunkedAsync(
            string symbol,
            DateTime startDate,
            DateTime endDate,
            TimeSpan chunkSize,
            IProgress<DataAcquisitionProgress>? progress = null)
        {
            // For Stooq, we get all data at once and filter
            var allBars = await GetHistoricalDataAsync(symbol, startDate, endDate);
            
            // Report progress
            progress?.Report(new DataAcquisitionProgress
            {
                Symbol = symbol,
                StartDate = startDate,
                EndDate = endDate,
                CurrentDate = endDate,
                ProgressPercent = 100.0,
                RecordsProcessed = allBars.Count,
                Status = $"Downloaded {allBars.Count} bars from Stooq"
            });
            
            // Small delay to be respectful
            await Task.Delay(1000);
            
            return allBars;
        }
        
        public void Dispose()
        {
            _httpClient?.Dispose();
            _rateLimiter?.Dispose();
        }
    }
}