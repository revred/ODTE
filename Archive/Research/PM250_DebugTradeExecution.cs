using System;
using System.Threading.Tasks;
using ODTE.Strategy;
using ODTE.Historical;
using Xunit;
using FluentAssertions;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// Debug PM250 trade execution to identify why trades aren't executing
    /// Analyze blocking conditions and adjust thresholds for real market data
    /// </summary>
    public class PM250_DebugTradeExecution
    {
        [Fact]
        public async Task Debug_PM250_Trade_Execution_Conditions()
        {
            Console.WriteLine("🔍 DEBUGGING PM250 TRADE EXECUTION CONDITIONS");
            Console.WriteLine("=" + new string('=', 60));
            
            var strategy = new HighFrequencyOptimalStrategy();
            var dataManager = new HistoricalDataManager();
            await dataManager.InitializeAsync();
            
            Console.WriteLine("📊 Testing sample market conditions from 2015...");
            
            // Test with sample 2015 market data
            var testDate = new DateTime(2015, 1, 5, 10, 0, 0); // First Monday, 10 AM
            var marketData = await GetSampleMarketData(dataManager, testDate);
            
            if (marketData != null)
            {
                var conditions = CreateMarketConditionsFromData(marketData, testDate);
                var parameters = new StrategyParameters { PositionSize = 1, MaxRisk = 500 };
                
                Console.WriteLine("\n🔬 MARKET CONDITIONS ANALYSIS:");
                Console.WriteLine($"   Date: {conditions.Date:yyyy-MM-dd HH:mm}");
                Console.WriteLine($"   Underlying Price: ${conditions.UnderlyingPrice:F2}");
                Console.WriteLine($"   VIX: {conditions.VIX:F1}");
                Console.WriteLine($"   Trend Score: {conditions.TrendScore:F2}");
                Console.WriteLine($"   Market Regime: {conditions.MarketRegime}");
                Console.WriteLine($"   Days to Expiry: {conditions.DaysToExpiry}");
                
                // Execute with detailed logging
                Console.WriteLine("\n⚡ EXECUTING PM250 STRATEGY WITH DETAILED ANALYSIS:");
                
                // Test the strategy execution
                var result = await strategy.ExecuteAsync(parameters, conditions);
                
                Console.WriteLine($"\n📊 STRATEGY EXECUTION RESULT:");
                Console.WriteLine($"   Strategy Name: {result.StrategyName}");
                Console.WriteLine($"   P&L: ${result.PnL:F2}");
                Console.WriteLine($"   Credit Received: ${result.CreditReceived:F2}");
                Console.WriteLine($"   Is Win: {result.IsWin}");
                Console.WriteLine($"   Metadata: {result.Metadata}");
                
                // Analyze why trade might be blocked
                await AnalyzeBlockingConditions(conditions);
            }
            else
            {
                Console.WriteLine("❌ Could not retrieve market data for analysis");
            }
        }
        
        private async Task<MarketDataBar?> GetSampleMarketData(HistoricalDataManager dataManager, DateTime targetDate)
        {
            try
            {
                var data = await dataManager.GetMarketDataAsync("XSP", targetDate.Date, targetDate.Date.AddDays(1));
                return data.FirstOrDefault(d => Math.Abs((d.Timestamp - targetDate).TotalMinutes) < 60);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error retrieving market data: {ex.Message}");
                return null;
            }
        }
        
        private MarketConditions CreateMarketConditionsFromData(MarketDataBar data, DateTime time)
        {
            return new MarketConditions
            {
                Date = time,
                UnderlyingPrice = data.Close,
                VIX = EstimateVIX(data),
                TrendScore = CalculateTrend(data),
                MarketRegime = ClassifyRegime(data),
                DaysToExpiry = 0,
                IVRank = 0.5,
                RealizedVolatility = 0.15
            };
        }
        
        private double EstimateVIX(MarketDataBar data)
        {
            var range = (data.High - data.Low) / data.Close;
            return Math.Max(10, Math.Min(50, 15 + range * 150));
        }
        
        private double CalculateTrend(MarketDataBar data)
        {
            var mid = (data.High + data.Low) / 2;
            return Math.Max(-1, Math.Min(1, (data.Close - mid) / mid * 8));
        }
        
        private string ClassifyRegime(MarketDataBar data)
        {
            var vix = EstimateVIX(data);
            return vix > 30 ? "Volatile" : vix > 20 ? "Mixed" : "Calm";
        }
        
        private async Task AnalyzeBlockingConditions(MarketConditions conditions)
        {
            Console.WriteLine("\n🚫 ANALYZING POTENTIAL BLOCKING CONDITIONS:");
            
            // 1. Time validation
            var hour = conditions.Date.Hour;
            var isValidHour = (hour >= 9 && hour <= 11) || (hour >= 13 && hour <= 15);
            Console.WriteLine($"   ✅ Trading Hours: {conditions.Date.Hour}:00 - {(isValidHour ? "VALID" : "BLOCKED")}");
            
            // 2. VIX analysis
            Console.WriteLine($"   📊 VIX Analysis: {conditions.VIX:F1}");
            Console.WriteLine($"      - Extreme volatility (>45): {(conditions.VIX > 45 ? "BLOCKED" : "OK")}");
            Console.WriteLine($"      - Optimal range (15-25): {(conditions.VIX >= 15 && conditions.VIX <= 25 ? "OPTIMAL" : "SUBOPTIMAL")}");
            
            // 3. GoScore calculation simulation
            var baseScore = 50.0;
            var vixScore = conditions.VIX >= 15 && conditions.VIX <= 25 ? 85.0 : 
                          conditions.VIX < 15 ? 70.0 : 
                          conditions.VIX <= 35 ? 75.0 : 45.0;
            baseScore += (vixScore - 50) * 0.3;
            
            var regimeScore = conditions.MarketRegime switch
            {
                "Calm" => 85.0,
                "Mixed" => 70.0,
                "Volatile" => 60.0,
                _ => 45.0
            };
            baseScore += (regimeScore - 50) * 0.25;
            
            var trendScore = Math.Abs(conditions.TrendScore) < 0.3 ? 85.0 :
                           Math.Abs(conditions.TrendScore) < 0.7 ? 70.0 : 60.0;
            baseScore += (trendScore - 50) * 0.20;
            
            Console.WriteLine($"   🎯 GoScore Calculation:");
            Console.WriteLine($"      - VIX Score: {vixScore:F1} (weight: 30%)");
            Console.WriteLine($"      - Regime Score: {regimeScore:F1} (weight: 25%)");
            Console.WriteLine($"      - Trend Score: {trendScore:F1} (weight: 20%)");
            Console.WriteLine($"      - Estimated Total: {baseScore:F1}");
            Console.WriteLine($"      - Threshold: 75.0 - {(baseScore >= 75.0 ? "PASS" : "BLOCKED")}");
            
            // 4. Market regime check
            var regimeOk = conditions.MarketRegime != "Crisis" && 
                          !(conditions.MarketRegime == "Volatile" && Math.Abs(conditions.TrendScore) > 1.2);
            Console.WriteLine($"   🏛️ Market Regime: {conditions.MarketRegime} - {(regimeOk ? "ACCEPTABLE" : "BLOCKED")}");
            
            // 5. Recommendations
            Console.WriteLine("\n💡 RECOMMENDATIONS:");
            if (baseScore < 75.0)
            {
                Console.WriteLine($"   🔧 LOWER GoScore threshold from 75.0 to {Math.Max(50.0, baseScore - 5):F1}");
            }
            if (!isValidHour)
            {
                Console.WriteLine("   🕐 EXPAND trading hours or test during valid hours");
            }
            if (conditions.VIX > 35)
            {
                Console.WriteLine("   📈 Test with calmer market periods (VIX < 35)");
            }
        }
    }
}