using System;
using System.Threading.Tasks;
using ODTE.Strategy;
using ODTE.Historical;
using Xunit;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// Diagnostic test to understand why PM250 strategy isn't executing trades
    /// </summary>
    public class PM250_MarketDataDiagnostic
    {
        [Fact]
        public async Task Diagnose_PM250_Execution_Conditions()
        {
            Console.WriteLine("🔍 PM250 MARKET DATA DIAGNOSTIC");
            Console.WriteLine("=".PadRight(50, '='));
            
            var dataManager = new HistoricalDataManager();
            var strategy = new HighFrequencyOptimalStrategy();
            
            await dataManager.InitializeAsync();
            
            // Test with first available data point
            var testDate = new DateTime(2015, 1, 5, 10, 0, 0); // Monday at 10 AM
            
            Console.WriteLine($"\n📊 Testing market data for: {testDate:yyyy-MM-dd HH:mm}");
            
            // Get market data
            var marketData = await dataManager.GetMarketDataAsync("XSP", testDate.Date, testDate.Date.AddDays(1));
            
            if (marketData?.Any() == true)
            {
                var data = marketData.First();
                Console.WriteLine($"✅ Market data found:");
                Console.WriteLine($"   - Close: ${data.Close:F2}");
                Console.WriteLine($"   - Volume: {data.Volume:N0}");
                Console.WriteLine($"   - Timestamp: {data.Timestamp:yyyy-MM-dd HH:mm}");
                
                // Create market conditions
                var conditions = new MarketConditions
                {
                    Date = testDate,
                    UnderlyingPrice = data.Close,
                    VIX = 18.5, // Reasonable VIX level
                    TrendScore = 0.2, // Mild trend
                    MarketRegime = "Calm",
                    DaysToExpiry = 0,
                    IVRank = 0.5
                };
                
                Console.WriteLine($"\n🎯 Market Conditions Created:");
                Console.WriteLine($"   - Underlying: ${conditions.UnderlyingPrice:F2}");
                Console.WriteLine($"   - VIX: {conditions.VIX:F1}");
                Console.WriteLine($"   - Trend Score: {conditions.TrendScore:F1}");
                Console.WriteLine($"   - Market Regime: {conditions.MarketRegime}");
                Console.WriteLine($"   - Time: {conditions.Date:HH:mm}");
                
                // Test strategy execution
                var parameters = new StrategyParameters { PositionSize = 1, MaxRisk = 500 };
                var result = await strategy.ExecuteAsync(parameters, conditions);
                
                Console.WriteLine($"\n⚡ Strategy Execution Result:");
                Console.WriteLine($"   - P&L: ${result.PnL:F2}");
                Console.WriteLine($"   - Is Win: {result.IsWin}");
                Console.WriteLine($"   - Strategy: {result.StrategyName}");
                
                if (result.Metadata?.ContainsKey("BlockReason") == true)
                {
                    Console.WriteLine($"   - Block Reason: {result.Metadata["BlockReason"]}");
                }
                
                if (result.Metadata?.ContainsKey("GoScore") == true)
                {
                    Console.WriteLine($"   - GoScore: {result.Metadata["GoScore"]}");
                }
                
                // Test multiple time points
                Console.WriteLine($"\n🕐 Testing Multiple Time Points:");
                for (int hour = 9; hour <= 15; hour++)
                {
                    for (int minute = 0; minute < 60; minute += 6)
                    {
                        var testTime = new DateTime(2015, 1, 5, hour, minute, 0);
                        conditions.Date = testTime;
                        
                        var testResult = await strategy.ExecuteAsync(parameters, conditions);
                        
                        if (testResult.PnL != 0)
                        {
                            Console.WriteLine($"   ✅ TRADE EXECUTED at {testTime:HH:mm} - P&L: ${testResult.PnL:F2}");
                            break; // Found one successful trade
                        }
                        else if (testResult.Metadata?.ContainsKey("BlockReason") == true)
                        {
                            Console.WriteLine($"   ❌ {testTime:HH:mm}: {testResult.Metadata["BlockReason"]}");
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine("❌ No market data found for test date");
                
                // Show available data range
                var stats = await dataManager.GetStatsAsync();
                Console.WriteLine($"\n📈 Available Data Range:");
                Console.WriteLine($"   - Start: {stats.StartDate:yyyy-MM-dd}");
                Console.WriteLine($"   - End: {stats.EndDate:yyyy-MM-dd}");
                Console.WriteLine($"   - Total Records: {stats.TotalRecords:N0}");
            }
            
            Console.WriteLine("\n🏁 Diagnostic Complete");
        }
    }
}