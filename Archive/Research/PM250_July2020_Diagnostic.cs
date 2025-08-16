using System;
using System.Threading.Tasks;
using ODTE.Strategy;
using ODTE.Historical;
using Xunit;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// Diagnostic test to understand why PM250 genetic strategy isn't executing in July 2020
    /// </summary>
    public class PM250_July2020_Diagnostic
    {
        [Fact]
        public async Task Diagnose_PM250_Genetic_July_2020_Blocking()
        {
            Console.WriteLine("🔍 PM250 GENETIC STRATEGY - JULY 2020 DIAGNOSTIC");
            Console.WriteLine("=".PadRight(60, '='));
            
            // Create optimized chromosome
            var optimizedChromosome = new PM250_Chromosome
            {
                GoScoreThreshold = 60.9,
                ProfitTarget = 2.40m,
                CreditTarget = 0.092m,
                VIXSensitivity = 0.93,
                TrendTolerance = 0.65,
                RiskMultiplier = 0.93,
                TimeOfDayWeight = 1.15,
                MarketRegimeWeight = 1.25,
                VolatilityWeight = 1.20,
                MomentumWeight = 0.90
            };
            
            var strategy = new PM250_GeneticStrategy(optimizedChromosome);
            var dataManager = new HistoricalDataManager();
            
            await dataManager.InitializeAsync();
            
            // Test a specific July 2020 date
            var testDate = new DateTime(2020, 7, 15, 10, 0, 0); // Mid-month, 10 AM
            
            Console.WriteLine($"🧪 Testing specific moment: {testDate:yyyy-MM-dd HH:mm}");
            Console.WriteLine();
            
            // Get market data
            var marketData = await dataManager.GetMarketDataAsync("XSP", testDate.Date, testDate.Date.AddDays(1));
            
            if (marketData?.Any() == true)
            {
                var data = marketData.First();
                Console.WriteLine($"📊 Market Data Found:");
                Console.WriteLine($"   Close: ${data.Close:F2}");
                Console.WriteLine($"   Volume: {data.Volume:N0}");
                Console.WriteLine();
                
                // Test multiple scenarios with different market conditions
                var scenarios = new[]
                {
                    new { VIX = 25.0, Trend = 0.2, Regime = "Calm", Name = "Favorable July 2020" },
                    new { VIX = 20.0, Trend = 0.1, Regime = "Calm", Name = "Very Calm" },
                    new { VIX = 30.0, Trend = 0.3, Regime = "Mixed", Name = "Moderate Vol" },
                    new { VIX = 15.0, Trend = 0.0, Regime = "Calm", Name = "Ultra Low Vol" }
                };
                
                Console.WriteLine("🎯 TESTING MULTIPLE SCENARIOS:");
                Console.WriteLine("-".PadRight(50, '-'));
                
                foreach (var scenario in scenarios)
                {
                    var conditions = new MarketConditions
                    {
                        Date = testDate,
                        UnderlyingPrice = data.Close,
                        VIX = scenario.VIX,
                        TrendScore = scenario.Trend,
                        MarketRegime = scenario.Regime,
                        DaysToExpiry = 0,
                        IVRank = 0.5
                    };
                    
                    var parameters = new StrategyParameters { PositionSize = 1, MaxRisk = 93 }; // Apply risk multiplier
                    
                    Console.WriteLine($"\n📋 Scenario: {scenario.Name}");
                    Console.WriteLine($"   VIX: {scenario.VIX}, Trend: {scenario.Trend}, Regime: {scenario.Regime}");
                    
                    var result = await strategy.ExecuteAsync(parameters, conditions);
                    
                    Console.WriteLine($"   Result: P&L = ${result.PnL:F2}");
                    
                    if (result.Metadata?.ContainsKey("BlockReason") == true)
                    {
                        Console.WriteLine($"   🚫 Block Reason: {result.Metadata["BlockReason"]}");
                    }
                    
                    if (result.Metadata?.ContainsKey("GoScore") == true)
                    {
                        Console.WriteLine($"   GoScore: {result.Metadata.GetValueOrDefault("GoScore", "N/A")}");
                    }
                    
                    if (result.PnL > 0)
                    {
                        Console.WriteLine($"   ✅ TRADE EXECUTED!");
                        break; // Found working scenario
                    }
                }
                
                Console.WriteLine();
                Console.WriteLine("🔧 PARAMETER ANALYSIS:");
                Console.WriteLine("-".PadRight(30, '-'));
                Console.WriteLine($"Current Genetic Parameters:");
                Console.WriteLine($"  GoScore Threshold: {optimizedChromosome.GoScoreThreshold:F1}");
                Console.WriteLine($"  Profit Target: ${optimizedChromosome.ProfitTarget:F2}");
                Console.WriteLine($"  Credit Target: {optimizedChromosome.CreditTarget:P1}");
                Console.WriteLine();
                
                Console.WriteLine("🎯 POTENTIAL SOLUTIONS:");
                Console.WriteLine("-".PadRight(30, '-'));
                Console.WriteLine("1. Lower GoScore threshold to 55-58 for July 2020");
                Console.WriteLine("2. Reduce profit target to $1.50-2.00 for COVID markets");
                Console.WriteLine("3. Adjust credit target to 6-8% for lower premiums");
                Console.WriteLine("4. Increase VIX sensitivity for COVID volatility patterns");
                
            }
            else
            {
                Console.WriteLine("❌ No market data found for July 2020");
            }
        }
        
        [Fact]
        public async Task Test_PM250_July2020_With_Adjusted_Parameters()
        {
            Console.WriteLine("🔧 PM250 JULY 2020 - ADJUSTED PARAMETERS TEST");
            Console.WriteLine("=".PadRight(60, '='));
            
            // Create adjusted chromosome specifically for July 2020 COVID conditions
            var adjustedChromosome = new PM250_Chromosome
            {
                GoScoreThreshold = 55.0,     // Lower for COVID markets
                ProfitTarget = 1.80m,        // More realistic for volatile period
                CreditTarget = 0.075m,       // Lower credit target (7.5%)
                VIXSensitivity = 1.2,        // Higher sensitivity to COVID volatility
                TrendTolerance = 0.8,        // More tolerant of trend changes
                RiskMultiplier = 0.90,       // Conservative sizing
                TimeOfDayWeight = 1.0,       // Standard timing
                MarketRegimeWeight = 1.1,    // Moderate regime awareness
                VolatilityWeight = 1.3,      // High volatility focus
                MomentumWeight = 0.8         // Lower momentum dependence
            };
            
            Console.WriteLine("🧬 ADJUSTED PARAMETERS FOR JULY 2020:");
            Console.WriteLine("-".PadRight(40, '-'));
            Console.WriteLine($"   GoScore Threshold: {adjustedChromosome.GoScoreThreshold:F1} (was 60.9)");
            Console.WriteLine($"   Profit Target: ${adjustedChromosome.ProfitTarget:F2} (was $2.40)");
            Console.WriteLine($"   Credit Target: {adjustedChromosome.CreditTarget:P1} (was 9.2%)");
            Console.WriteLine($"   VIX Sensitivity: {adjustedChromosome.VIXSensitivity:F2} (was 0.93)");
            Console.WriteLine();
            
            var strategy = new PM250_GeneticStrategy(adjustedChromosome);
            var dataManager = new HistoricalDataManager();
            await dataManager.InitializeAsync();
            
            // Quick test on a few July 2020 dates
            var testDates = new[]
            {
                new DateTime(2020, 7, 6, 10, 0, 0),   // Monday
                new DateTime(2020, 7, 15, 14, 0, 0),  // Wednesday
                new DateTime(2020, 7, 24, 11, 0, 0)   // Friday
            };
            
            var successfulTrades = 0;
            
            Console.WriteLine("⚡ QUICK EXECUTION TEST:");
            Console.WriteLine("-".PadRight(30, '-'));
            
            foreach (var testDate in testDates)
            {
                try
                {
                    var marketData = await dataManager.GetMarketDataAsync("XSP", testDate.Date, testDate.Date.AddDays(1));
                    
                    if (marketData?.Any() == true)
                    {
                        var data = marketData.First();
                        
                        var conditions = new MarketConditions
                        {
                            Date = testDate,
                            UnderlyingPrice = data.Close,
                            VIX = 26.0, // Typical July 2020 VIX
                            TrendScore = 0.3, // Mild uptrend during tech rally
                            MarketRegime = "Mixed",
                            DaysToExpiry = 0,
                            IVRank = 0.6
                        };
                        
                        var parameters = new StrategyParameters { PositionSize = 1, MaxRisk = 90 };
                        var result = await strategy.ExecuteAsync(parameters, conditions);
                        
                        if (result.PnL > 0)
                        {
                            successfulTrades++;
                            Console.WriteLine($"   ✅ {testDate:MM-dd HH:mm}: P&L = ${result.PnL:F2}");
                        }
                        else if (result.Metadata?.ContainsKey("BlockReason") == true)
                        {
                            Console.WriteLine($"   ❌ {testDate:MM-dd HH:mm}: {result.Metadata["BlockReason"]}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ⚠️ {testDate:MM-dd HH:mm}: Error - {ex.Message}");
                }
            }
            
            Console.WriteLine();
            Console.WriteLine($"📊 Quick Test Results: {successfulTrades}/{testDates.Length} trades executed");
            
            if (successfulTrades > 0)
            {
                Console.WriteLine("✅ ADJUSTED PARAMETERS SHOW PROMISE");
                Console.WriteLine("   Recommend running full July 2020 backtest with adjusted parameters");
            }
            else
            {
                Console.WriteLine("⚠️ PARAMETERS STILL NEED FURTHER ADJUSTMENT");
                Console.WriteLine("   Consider even more aggressive parameter relaxation for COVID markets");
            }
        }
    }
}