using System;
using System.Threading.Tasks;
using ODTE.Historical;
using Xunit;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// Quick test to check what real historical data is actually available
    /// </summary>
    public class DataAvailabilityCheck
    {
        [Fact]
        public async Task Check_Available_Real_Data()
        {
            Console.WriteLine("🔍 CHECKING AVAILABLE REAL DATA");
            Console.WriteLine("================================");

            var dataManager = new HistoricalDataManager();
            await dataManager.InitializeAsync();
            
            var stats = await dataManager.GetStatsAsync();
            
            Console.WriteLine($"📊 Database Statistics:");
            Console.WriteLine($"   Total Records: {stats.TotalRecords:N0}");
            Console.WriteLine($"   Date Range: {stats.StartDate:yyyy-MM-dd} to {stats.EndDate:yyyy-MM-dd}");
            Console.WriteLine($"   Database Size: {stats.DatabaseSizeMB:N1} MB");
            Console.WriteLine($"   Trading Days: ~{stats.TradingDays}");
            Console.WriteLine();

            // Test with available date range
            var startDate = stats.StartDate;
            var endDate = stats.EndDate > startDate.AddDays(5) ? startDate.AddDays(5) : stats.EndDate;
            
            Console.WriteLine($"📅 Testing with available dates: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            
            var marketData = await dataManager.GetMarketDataAsync("XSP", startDate, endDate);
            
            Console.WriteLine($"📈 Retrieved {marketData.Count:N0} market data points");
            if (marketData.Count > 0)
            {
                Console.WriteLine($"   First record: {marketData[0].Timestamp:yyyy-MM-dd HH:mm:ss} - Close: ${marketData[0].Close:F2}");
                Console.WriteLine($"   Last record:  {marketData[^1].Timestamp:yyyy-MM-dd HH:mm:ss} - Close: ${marketData[^1].Close:F2}");
            }
            
            Console.WriteLine();
            Console.WriteLine("✅ Data availability check complete");
            
            dataManager.Dispose();
        }
    }
}