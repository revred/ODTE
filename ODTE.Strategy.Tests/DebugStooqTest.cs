using System;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// Debug test to check Stooq API response format
    /// </summary>
    public class DebugStooqTest
    {
        [Fact]
        public async Task Debug_Stooq_Raw_Response()
        {
            Console.WriteLine("🔍 DEBUG: Stooq Raw Response - Multiple Symbols");
            Console.WriteLine("=".PadRight(50, '='));
            
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36");
            
            // Test multiple symbols and URL formats
            var testSymbols = new[]
            {
                ("SPY", "https://stooq.com/q/d/l/?s=SPY&i=d"),
                ("SPY.US", "https://stooq.com/q/d/l/?s=SPY.US&i=d"),
                ("AAPL", "https://stooq.com/q/d/l/?s=AAPL&i=d"),
                ("AAPL.US", "https://stooq.com/q/d/l/?s=AAPL.US&i=d"),
                ("^SPX", "https://stooq.com/q/d/l/?s=^SPX&i=d"),
                ("SPX", "https://stooq.com/q/d/l/?s=SPX&i=d")
            };
            
            foreach (var (symbol, url) in testSymbols)
            {
                Console.WriteLine($"🔍 Testing {symbol}:");
                Console.WriteLine($"🌐 URL: {url}");
                
                try
                {
                    var response = await httpClient.GetStringAsync(url);
                    
                    Console.WriteLine($"📊 Response Length: {response.Length} characters");
                    Console.WriteLine($"📝 Response: {response.Substring(0, Math.Min(100, response.Length))}{(response.Length > 100 ? "..." : "")}");
                    
                    // Check if it contains expected CSV headers
                    if (response.Contains("Date") && response.Contains("Open"))
                    {
                        Console.WriteLine("✅ Valid CSV data found!");
                        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        Console.WriteLine($"📈 Total lines: {lines.Length}");
                        Console.WriteLine($"📋 Header: {lines[0]}");
                        if (lines.Length > 1) Console.WriteLine($"📋 Sample: {lines[1]}");
                        break; // Found working format, stop testing
                    }
                    else if (response.Contains("No data"))
                    {
                        Console.WriteLine("❌ No data available");
                    }
                    else
                    {
                        Console.WriteLine("⚠️ Unexpected response format");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error: {ex.Message}");
                }
                
                Console.WriteLine();
                await Task.Delay(1000); // Be respectful to Stooq
            }
        }
    }
}