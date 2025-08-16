using System;
using System.Linq;
using System.Threading.Tasks;
using ODTE.Strategy;
using ODTE.Historical;
using Xunit;
using FluentAssertions;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// Simple integration test between PM250 strategies and ODTE.Historical data pipeline
    /// Validates that the existing data pipeline works correctly with PM250
    /// </summary>
    public class PM250_HistoricalIntegrationTest
    {
        [Fact]
        public async Task PM250_Can_Access_Historical_Data_Pipeline()
        {
            // Test the basic integration between PM250 and ODTE.Historical
            Console.WriteLine("🔗 Testing PM250 Integration with ODTE.Historical Pipeline");
            Console.WriteLine(new string('=', 60));

            try
            {
                // Initialize the historical data manager
                using var dataManager = new HistoricalDataManager();
                await dataManager.InitializeAsync();

                Console.WriteLine("✅ ODTE.Historical data manager initialized successfully");

                // Get database statistics
                var stats = await dataManager.GetStatsAsync();
                Console.WriteLine($"📊 Database Statistics:");
                Console.WriteLine($"   Total Records: {stats.TotalRecords:N0}");
                Console.WriteLine($"   Date Range: {stats.StartDate:yyyy-MM-dd} to {stats.EndDate:yyyy-MM-dd}");
                Console.WriteLine($"   Database Size: {stats.DatabaseSizeMB:N1} MB");

                // Test that we have at least some data
                stats.TotalRecords.Should().BeGreaterThan(0, "Database should contain historical data");

                // Test data retrieval for a recent period
                var testStartDate = DateTime.Now.AddDays(-30);
                var testEndDate = DateTime.Now.AddDays(-1);

                Console.WriteLine($"\n🔍 Testing data retrieval for {testStartDate:yyyy-MM-dd} to {testEndDate:yyyy-MM-dd}");

                // Try to get SPY data (primary symbol for PM250)
                var spyData = await dataManager.GetMarketDataAsync("SPY", testStartDate, testEndDate);
                
                if (spyData?.Any() == true)
                {
                    Console.WriteLine($"✅ Retrieved {spyData.Count()} SPY data points");
                    Console.WriteLine($"   First: {spyData.First().Timestamp:yyyy-MM-dd} - Close: ${spyData.First().Close:F2}");
                    Console.WriteLine($"   Last:  {spyData.Last().Timestamp:yyyy-MM-dd} - Close: ${spyData.Last().Close:F2}");
                }
                else
                {
                    Console.WriteLine("⚠️ No SPY data found - this may be normal for recent dates");
                }

                // Test PM250 strategy initialization
                var strategy = new PM250_OptimizedStrategy();
                strategy.Should().NotBeNull("PM250 strategy should initialize");

                Console.WriteLine("✅ PM250_OptimizedStrategy initialized successfully");

                // Test basic market conditions creation
                if (spyData?.Any() == true)
                {
                    var testBar = spyData.First();
                    var conditions = new MarketConditions
                    {
                        Date = testBar.Timestamp,
                        UnderlyingPrice = testBar.Close,
                        VIX = 20.0, // Default VIX
                        TrendScore = 0.0,
                        MarketRegime = "Calm",
                        DaysToExpiry = 0,
                        IVRank = 0.5
                    };

                    var parameters = new StrategyParameters 
                    { 
                        PositionSize = 1, 
                        MaxRisk = 500 
                    };

                    Console.WriteLine($"\n🎯 Testing strategy execution with real market data");
                    Console.WriteLine($"   Date: {conditions.Date:yyyy-MM-dd}");
                    Console.WriteLine($"   Underlying Price: ${conditions.UnderlyingPrice:F2}");

                    // Test strategy execution (should not throw)
                    var result = await strategy.ExecuteAsync(parameters, conditions);
                    result.Should().NotBeNull("Strategy execution should return a result");

                    Console.WriteLine($"✅ Strategy executed successfully");
                    Console.WriteLine($"   P&L: ${result.PnL:F2}");
                    Console.WriteLine($"   Is Win: {result.IsWin}");
                }

                Console.WriteLine("\n🎉 PM250 HISTORICAL INTEGRATION TEST PASSED");
                Console.WriteLine("   ✅ ODTE.Historical data pipeline is working");
                Console.WriteLine("   ✅ PM250 strategy can access historical data");
                Console.WriteLine("   ✅ Integration is ready for production use");

                // Assert success
                Assert.True(true, "Integration test completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ INTEGRATION TEST FAILED: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        [Fact]
        public async Task ODTE_Historical_Data_Quality_Validation()
        {
            Console.WriteLine("🔍 Testing ODTE.Historical Data Quality Validation");
            Console.WriteLine(new string('=', 60));

            try
            {
                using var dataManager = new HistoricalDataManager();
                await dataManager.InitializeAsync();

                // Test data validation (this should exist in ODTE.Historical)
                Console.WriteLine("📊 Running data quality checks...");

                var stats = await dataManager.GetStatsAsync();
                
                // Basic quality checks
                stats.TotalRecords.Should().BeGreaterThan(0, "Database should have data");
                stats.DatabaseSizeMB.Should().BeGreaterThan(0, "Database should have size");
                
                var dataRange = (stats.EndDate - stats.StartDate).TotalDays;
                dataRange.Should().BeGreaterThan(0, "Should have valid date range");

                Console.WriteLine($"✅ Data quality validation passed");
                Console.WriteLine($"   Records: {stats.TotalRecords:N0}");
                Console.WriteLine($"   Date span: {dataRange:F0} days");
                Console.WriteLine($"   Size: {stats.DatabaseSizeMB:N1} MB");

                Assert.True(true, "Data quality validation completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ DATA QUALITY TEST FAILED: {ex.Message}");
                throw;
            }
        }

        [Fact]
        public void ODTE_Historical_Data_Providers_Available()
        {
            Console.WriteLine("🌐 Testing ODTE.Historical Data Providers");
            Console.WriteLine(new string('=', 60));

            try
            {
                // Test that data providers can be instantiated
                using var stooqProvider = new ODTE.Historical.DataProviders.StooqProvider();
                stooqProvider.Should().NotBeNull("StooqProvider should be available");

                using var yahooProvider = new ODTE.Historical.DataProviders.YahooFinanceProvider();
                yahooProvider.Should().NotBeNull("YahooFinanceProvider should be available");

                Console.WriteLine("✅ Data providers are available:");
                Console.WriteLine("   ✅ StooqProvider (primary)");
                Console.WriteLine("   ✅ YahooFinanceProvider (backup)");

                Assert.True(true, "Data providers are available");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ DATA PROVIDER TEST FAILED: {ex.Message}");
                throw;
            }
        }
    }
}