using System;
using System.Threading.Tasks;
using ODTE.Strategy;
using ODTE.Historical;
using Xunit;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// Simple validation test for PM250 Optimized Strategy
    /// </summary>
    public class PM250_SimpleValidation
    {
        [Fact]
        public async Task Test_PM250_Optimized_Single_Trade()
        {
            Console.WriteLine("🔍 PM250 OPTIMIZED - SINGLE TRADE TEST");
            Console.WriteLine("=".PadRight(50, '='));
            
            var strategy = new PM250_OptimizedStrategy();
            
            // Create very favorable conditions
            var conditions = new MarketConditions
            {
                Date = new DateTime(2015, 1, 5, 10, 0, 0), // Monday 10 AM
                UnderlyingPrice = 450.0,
                VIX = 18.0, // Good VIX level
                TrendScore = 0.1, // Very mild trend
                MarketRegime = "Calm", // Best regime
                DaysToExpiry = 0,
                IVRank = 0.5
            };
            
            var parameters = new StrategyParameters { PositionSize = 1, MaxRisk = 100 };
            
            Console.WriteLine($"📊 Test Conditions:");
            Console.WriteLine($"   - Time: {conditions.Date:yyyy-MM-dd HH:mm}");
            Console.WriteLine($"   - Underlying: ${conditions.UnderlyingPrice:F2}");
            Console.WriteLine($"   - VIX: {conditions.VIX:F1}");
            Console.WriteLine($"   - Trend: {conditions.TrendScore:F1}");
            Console.WriteLine($"   - Regime: {conditions.MarketRegime}");
            
            // Execute strategy
            var result = await strategy.ExecuteAsync(parameters, conditions);
            
            Console.WriteLine($"\n⚡ Execution Result:");
            Console.WriteLine($"   - P&L: ${result.PnL:F2}");
            Console.WriteLine($"   - Is Win: {result.IsWin}");
            Console.WriteLine($"   - Strategy: {result.StrategyName}");
            
            if (result.Metadata != null)
            {
                foreach (var metadata in result.Metadata)
                {
                    Console.WriteLine($"   - {metadata.Key}: {metadata.Value}");
                }
            }
            
            if (result.PnL > 0)
            {
                Console.WriteLine("✅ SUCCESS: Trade executed!");
            }
            else
            {
                Console.WriteLine("❌ BLOCKED: No trade executed");
            }
        }
    }
}