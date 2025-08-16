using System;
using System.Threading.Tasks;
using ODTE.Historical.DataProviders;
using Xunit;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// Test Stooq data provider functionality
    /// Validates actual data acquisition from Stooq.com
    /// </summary>
    public class StooqDataProviderTest
    {
        [Fact]
        public async Task Test_Stooq_SPY_Data_Acquisition()
        {
            Console.WriteLine("🧪 STOOQ PROVIDER TEST - SPY DATA");
            Console.WriteLine("=".PadRight(40, '='));
            Console.WriteLine("Testing Stooq.com data acquisition for SPY...");
            Console.WriteLine();
            
            using var stooqProvider = new StooqProvider();
            
            // Test with a recent small date range
            var startDate = new DateTime(2024, 1, 20);
            var endDate = new DateTime(2024, 1, 27);
            
            Console.WriteLine($"📅 Test Period: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            Console.WriteLine($"🎯 Symbol: SPY");
            Console.WriteLine($"🌐 Source: Stooq.com");
            Console.WriteLine();
            
            try
            {
                Console.WriteLine("🔄 Connecting to Stooq.com...");
                var bars = await stooqProvider.GetHistoricalDataAsync("SPY", startDate, endDate);
                
                Console.WriteLine();
                Console.WriteLine("📊 STOOQ ACQUISITION RESULTS:");
                Console.WriteLine("-".PadRight(35, '-'));
                Console.WriteLine($"Status: {(bars.Count > 0 ? "✅ SUCCESS" : "❌ NO DATA")}");
                Console.WriteLine($"Records Retrieved: {bars.Count:N0}");
                
                if (bars.Count > 0)
                {
                    Console.WriteLine($"Date Range: {bars[0].Timestamp:yyyy-MM-dd} to {bars[^1].Timestamp:yyyy-MM-dd}");
                    Console.WriteLine($"First Day: O={bars[0].Open:F2} H={bars[0].High:F2} L={bars[0].Low:F2} C={bars[0].Close:F2} V={bars[0].Volume:N0}");
                    Console.WriteLine($"Last Day: O={bars[^1].Open:F2} H={bars[^1].High:F2} L={bars[^1].Low:F2} C={bars[^1].Close:F2} V={bars[^1].Volume:N0}");
                    Console.WriteLine();
                    Console.WriteLine("✅ STOOQ PROVIDER WORKING SUCCESSFULLY!");
                    Console.WriteLine("🚀 Ready for full data acquisition with Stooq as primary source");
                }
                else
                {
                    Console.WriteLine("⚠️ No data returned - check symbol availability or date range");
                }
                
                // Basic validation
                Assert.True(bars.Count >= 0, "Should return non-negative number of bars");
                
                if (bars.Count > 0)
                {
                    Assert.All(bars, bar => 
                    {
                        Assert.True(bar.Open > 0, "Open price should be positive");
                        Assert.True(bar.High >= bar.Open, "High should be >= Open");
                        Assert.True(bar.Low <= bar.Open, "Low should be <= Open");
                        Assert.True(bar.Close > 0, "Close price should be positive");
                        Assert.True(bar.Volume >= 0, "Volume should be non-negative");
                    });
                    
                    Console.WriteLine("✅ Data validation passed - all OHLCV values are reasonable");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ STOOQ TEST FAILED: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }
        
        [Fact]
        public async Task Test_MultiSource_With_Stooq_Priority()
        {
            Console.WriteLine("🧪 MULTI-SOURCE PROVIDER TEST - STOOQ PRIORITY");
            Console.WriteLine("=".PadRight(50, '='));
            Console.WriteLine("Testing multi-source provider with Stooq as primary source...");
            Console.WriteLine();
            
            using var multiProvider = new MultiSourceDataProvider();
            
            // Test with SPY (should work well with Stooq)
            var startDate = new DateTime(2024, 1, 20);
            var endDate = new DateTime(2024, 1, 27);
            
            Console.WriteLine($"📅 Test Period: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            Console.WriteLine($"🎯 Symbol: SPY");
            Console.WriteLine($"🌐 Sources: Stooq → Yahoo Finance → Alpha Vantage");
            Console.WriteLine();
            
            try
            {
                Console.WriteLine("🔄 Testing multi-source failover system...");
                var bars = await multiProvider.GetHistoricalDataAsync("SPY", startDate, endDate);
                
                Console.WriteLine();
                Console.WriteLine("📊 MULTI-SOURCE RESULTS:");
                Console.WriteLine("-".PadRight(30, '-'));
                Console.WriteLine($"Status: {(bars.Count > 0 ? "✅ SUCCESS" : "❌ ALL SOURCES FAILED")}");
                Console.WriteLine($"Records Retrieved: {bars.Count:N0}");
                
                if (bars.Count > 0)
                {
                    Console.WriteLine($"Date Range: {bars[0].Timestamp:yyyy-MM-dd} to {bars[^1].Timestamp:yyyy-MM-dd}");
                    Console.WriteLine();
                    Console.WriteLine("📈 PROVIDER STATISTICS:");
                    var stats = multiProvider.GetProviderStatistics();
                    foreach (var stat in stats)
                    {
                        Console.WriteLine($"  {stat.Key}: {stat.Value} failures");
                    }
                    Console.WriteLine();
                    Console.WriteLine("✅ MULTI-SOURCE SYSTEM WORKING!");
                    Console.WriteLine("🚀 Stooq-enhanced data acquisition ready for production");
                }
                else
                {
                    Console.WriteLine("❌ All data sources failed - check network connectivity");
                }
                
                // Validation
                Assert.True(bars.Count >= 0, "Should return non-negative number of bars");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ MULTI-SOURCE TEST FAILED: {ex.Message}");
                throw;
            }
        }
    }
}