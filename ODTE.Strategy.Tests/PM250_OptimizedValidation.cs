using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ODTE.Strategy;
using ODTE.Historical;
using Xunit;
using FluentAssertions;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// PM250 Optimized Strategy Validation
    /// 
    /// Test the optimized PM250 strategy with realistic parameters
    /// to ensure it executes trades and generates reasonable profits
    /// </summary>
    public class PM250_OptimizedValidation
    {
        [Fact]
        public async Task PM250_Optimized_Should_Execute_Trades_2015()
        {
            Console.WriteLine("🚀 PM250 OPTIMIZED STRATEGY VALIDATION");
            Console.WriteLine("=".PadRight(60, '='));
            
            var dataManager = new HistoricalDataManager();
            var optimizedStrategy = new PM250_OptimizedStrategy();
            
            await dataManager.InitializeAsync();
            
            var tradingResults = new List<StrategyResult>();
            var testPeriod = 30; // Test 30 days
            var startDate = new DateTime(2015, 1, 5);
            
            Console.WriteLine($"📊 Testing Period: {testPeriod} days starting {startDate:yyyy-MM-dd}");
            Console.WriteLine();
            
            // Test execution across different market conditions
            for (int day = 0; day < testPeriod; day++)
            {
                var currentDate = startDate.AddDays(day);
                
                // Skip weekends
                if (currentDate.DayOfWeek == DayOfWeek.Saturday || currentDate.DayOfWeek == DayOfWeek.Sunday)
                    continue;
                
                // Test multiple times per day (every 30 minutes)
                for (int hour = 9; hour <= 15; hour++)
                {
                    for (int minute = 0; minute < 60; minute += 30)
                    {
                        var testTime = new DateTime(currentDate.Year, currentDate.Month, currentDate.Day, hour, minute, 0);
                        
                        // Get market data for this day
                        var marketData = await dataManager.GetMarketDataAsync("XSP", currentDate.Date, currentDate.Date.AddDays(1));
                        
                        if (marketData?.Any() == true)
                        {
                            var data = marketData.First();
                            
                            // Create realistic market conditions
                            var conditions = CreateMarketConditions(testTime, data);
                            var parameters = new StrategyParameters { PositionSize = 1, MaxRisk = 100 };
                            
                            // Execute strategy
                            var result = await optimizedStrategy.ExecuteAsync(parameters, conditions);
                            
                            if (result.PnL != 0)
                            {
                                tradingResults.Add(result);
                                
                                if (tradingResults.Count <= 10)
                                {
                                    Console.WriteLine($"✅ Trade #{tradingResults.Count}: {testTime:yyyy-MM-dd HH:mm} - P&L: ${result.PnL:F2}");
                                    
                                    if (result.Metadata?.ContainsKey("GoScore") == true)
                                    {
                                        Console.WriteLine($"   GoScore: {result.Metadata["GoScore"]}, Target: ${result.Metadata.GetValueOrDefault("DynamicProfitTarget", "N/A")}");
                                    }
                                }
                            }
                        }
                        
                        // Stop after we get enough trades for analysis
                        if (tradingResults.Count >= 50)
                            break;
                    }
                    
                    if (tradingResults.Count >= 50)
                        break;
                }
                
                if (tradingResults.Count >= 50)
                    break;
            }
            
            // Analysis and validation
            Console.WriteLine();
            Console.WriteLine($"📈 RESULTS ANALYSIS ({tradingResults.Count} trades):");
            Console.WriteLine("-".PadRight(50, '-'));
            
            if (tradingResults.Any())
            {
                var winners = tradingResults.Where(r => r.PnL > 0).ToList();
                var losers = tradingResults.Where(r => r.PnL < 0).ToList();
                
                var totalPnL = tradingResults.Sum(r => r.PnL);
                var winRate = winners.Count / (double)tradingResults.Count;
                var avgWinner = winners.Any() ? winners.Average(r => r.PnL) : 0;
                var avgLoser = losers.Any() ? losers.Average(r => r.PnL) : 0;
                var avgTrade = tradingResults.Average(r => r.PnL);
                var maxWin = tradingResults.Max(r => r.PnL);
                var maxLoss = tradingResults.Min(r => r.PnL);
                
                Console.WriteLine($"Total P&L: ${totalPnL:F2}");
                Console.WriteLine($"Win Rate: {winRate:P1} ({winners.Count}/{tradingResults.Count})");
                Console.WriteLine($"Average Trade: ${avgTrade:F2}");
                Console.WriteLine($"Average Winner: ${avgWinner:F2}");
                Console.WriteLine($"Average Loser: ${avgLoser:F2}");
                Console.WriteLine($"Best Trade: ${maxWin:F2}");
                Console.WriteLine($"Worst Trade: ${maxLoss:F2}");
                
                // Profitability factor
                var grossProfit = winners.Sum(r => r.PnL);
                var grossLoss = Math.Abs(losers.Sum(r => r.PnL));
                var profitFactor = grossLoss > 0 ? (double)(grossProfit / grossLoss) : 0.0;
                
                Console.WriteLine($"Profit Factor: {profitFactor:F2}");
                
                // Validation assertions
                Console.WriteLine();
                Console.WriteLine("🎯 VALIDATION RESULTS:");
                Console.WriteLine("-".PadRight(30, '-'));
                
                var validations = new List<(string Test, bool Passed, string Details)>
                {
                    ("Trades Executed", tradingResults.Count >= 10, $"{tradingResults.Count} trades (target: ≥10)"),
                    ("Overall Profitability", totalPnL > 0, $"${totalPnL:F2} total P&L"),
                    ("Win Rate", winRate >= 0.60, $"{winRate:P1} (target: ≥60%)"),
                    ("Average Trade", avgTrade >= 1.0m, $"${avgTrade:F2} (target: ≥$1.00)"),
                    ("Profit Factor", profitFactor >= 1.2, $"{profitFactor:F2} (target: ≥1.2)"),
                    ("Risk Control", maxLoss >= -10.0m, $"${maxLoss:F2} max loss (limit: -$10.00)")
                };
                
                var passedCount = 0;
                foreach (var (test, passed, details) in validations)
                {
                    var status = passed ? "✅ PASS" : "❌ FAIL";
                    Console.WriteLine($"{status} {test}: {details}");
                    if (passed) passedCount++;
                }
                
                Console.WriteLine();
                Console.WriteLine($"🏆 OVERALL RESULT: {passedCount}/{validations.Count} criteria passed");
                
                if (passedCount >= 5)
                    Console.WriteLine("✅ PM250 OPTIMIZED STRATEGY: EXCELLENT PERFORMANCE");
                else if (passedCount >= 4)
                    Console.WriteLine("⚡ PM250 OPTIMIZED STRATEGY: GOOD PERFORMANCE");
                else
                    Console.WriteLine("⚠️  PM250 OPTIMIZED STRATEGY: NEEDS FURTHER TUNING");
                
                // Assert minimum requirements for test to pass
                tradingResults.Count.Should().BeGreaterThan(5, "Should execute at least 5 trades");
                totalPnL.Should().BePositive("Strategy should be profitable overall");
                winRate.Should().BeGreaterThan(0.5, "Win rate should be above 50%");
            }
            else
            {
                Console.WriteLine("❌ NO TRADES EXECUTED");
                Console.WriteLine("   Strategy still too restrictive or market data issues");
                
                // Fail the test if no trades are executed
                Assert.Fail("Optimized strategy should execute at least some trades");
            }
        }
        
        private MarketConditions CreateMarketConditions(DateTime time, MarketDataBar data)
        {
            var conditions = new MarketConditions
            {
                Date = time,
                UnderlyingPrice = data.Close,
                VIX = GenerateRealisticVIX(time),
                TrendScore = GenerateRealisticTrend(),
                MarketRegime = DetermineMarketRegime(time),
                DaysToExpiry = 0,
                IVRank = 0.5
            };
            
            return conditions;
        }
        
        private double GenerateRealisticVIX(DateTime time)
        {
            // Generate realistic VIX values based on historical patterns
            var baseVIX = 18.0; // Calm market base
            
            // Add some time-based variation
            var hourFactor = time.Hour == 9 ? 1.1 : time.Hour >= 15 ? 1.05 : 1.0;
            var dayVariation = (new Random(time.DayOfYear).NextDouble() - 0.5) * 8.0;
            
            var vix = baseVIX * hourFactor + dayVariation;
            return Math.Max(10, Math.Min(35, vix)); // Reasonable range
        }
        
        private double GenerateRealisticTrend()
        {
            var random = new Random();
            return (random.NextDouble() - 0.5) * 1.0; // -0.5 to +0.5 trend range
        }
        
        private string DetermineMarketRegime(DateTime time)
        {
            // Simulate realistic market regime distribution
            var regimes = new[] { "Calm", "Calm", "Calm", "Mixed", "Mixed", "Volatile" };
            var index = time.DayOfYear % regimes.Length;
            return regimes[index];
        }
    }
}