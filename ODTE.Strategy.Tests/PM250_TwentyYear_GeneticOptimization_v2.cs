using System;
using System.Threading.Tasks;
using ODTE.Strategy;
using Xunit;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// Comprehensive 20-year genetic optimization of PM250 strategy
    /// Target: $15+ average trade earnings with tight drawdown control
    /// Period: 2005-2025 across all market conditions
    /// Features: Reverse Fibonacci risk management + persistent parameter storage
    /// </summary>
    public class PM250_TwentyYear_GeneticOptimization_v2
    {
        [Fact]
        public async Task Execute_TwentyYear_Genetic_Optimization_With_Persistence()
        {
            Console.WriteLine("🧬 PM250 TWENTY-YEAR GENETIC OPTIMIZATION");
            Console.WriteLine("=".PadRight(60, '='));
            Console.WriteLine("Evolving PM250 strategy for 2005-2025 market conditions");
            Console.WriteLine("🎯 Target: $15+ average trade earnings with capital preservation");
            Console.WriteLine("🔄 Reverse Fibonacci risk curtailment system included");
            Console.WriteLine("💾 Persistent parameter storage across optimization sessions");
            Console.WriteLine();
            
            // Initialize genetic optimizer with 20-year dataset
            var optimizer = new PM250_GeneticOptimizer_v2(
                startDate: new DateTime(2005, 1, 1),
                endDate: new DateTime(2025, 8, 15),
                modelPath: @"C:\code\ODTE\PM250_OptimalWeights_TwentyYear.json"
            );
            
            Console.WriteLine("📊 OPTIMIZATION SPECIFICATIONS:");
            Console.WriteLine("   🎯 Target Trade P&L: $15.00+ per trade");
            Console.WriteLine("   📉 Maximum Drawdown: 10.0% (tight capital preservation)");
            Console.WriteLine("   ✅ Minimum Win Rate: 70.0% (high consistency)");
            Console.WriteLine("   📈 Minimum Sharpe: 1.5 (strong risk-adjusted returns)");
            Console.WriteLine("   👥 Population Size: 200 chromosomes");
            Console.WriteLine("   🔄 Max Generations: 100 (adaptive early termination)");
            Console.WriteLine("   🧬 Parameters: 23 optimized variables");
            Console.WriteLine();
            
            Console.WriteLine("🎯 CRITICAL MARKET PERIODS FOR EVOLUTION:");
            Console.WriteLine("   2007-2009: Financial Crisis (stress testing)");
            Console.WriteLine("   2010-2012: QE Recovery (adaptation to intervention)");
            Console.WriteLine("   2013-2015: Taper Tantrum Era (policy uncertainty)");
            Console.WriteLine("   2016-2018: Trump Rally + Volmageddon (regime shifts)");
            Console.WriteLine("   2020: COVID Crash/Recovery (extreme volatility)");
            Console.WriteLine("   2022-2023: Rate Hiking Cycle (tightening environment)");
            Console.WriteLine("   2024-2025: Current Market (validation period)");
            Console.WriteLine();
            
            Console.WriteLine("🧬 GENETIC ALGORITHM PARAMETERS:");
            Console.WriteLine("   Core PM250: ShortDelta, WidthPoints, CreditRatio, StopMultiple");
            Console.WriteLine("   GoScore Optimization: Base threshold + VIX/trend adjustments");
            Console.WriteLine("   Risk Management: Position sizing, drawdown reduction, recovery");
            Console.WriteLine("   Market Adaptation: Bull/bear factors, volatility response");
            Console.WriteLine("   Time Optimization: Opening/closing bias, Friday reduction");
            Console.WriteLine("   Reverse Fibonacci: 4-level risk curtailment system");
            Console.WriteLine();
            
            try
            {
                Console.WriteLine("🚀 STARTING 20-YEAR GENETIC EVOLUTION...");
                Console.WriteLine("This process will optimize PM250 across all major market conditions");
                Console.WriteLine("Expected duration: 2-6 hours depending on system performance");
                Console.WriteLine("Progress will be reported every 5 generations");
                Console.WriteLine();
                
                var progress = new Progress<OptimizationProgress>(p =>
                {
                    if (p.Generation % 5 == 0 || p.Generation >= 95)
                    {
                        Console.WriteLine($"📈 Generation {p.Generation}/100:");
                        Console.WriteLine($"   🏆 Best Fitness: {p.BestFitness:F3}");
                        Console.WriteLine($"   💰 Avg Trade P&L: ${p.BestTradeProfit:F2}");
                        Console.WriteLine($"   ✅ Win Rate: {p.BestWinRate:F1}%");
                        Console.WriteLine($"   📉 Max Drawdown: {p.BestDrawdown:F1}%");
                        Console.WriteLine($"   📊 Sharpe Ratio: {p.BestSharpe:F2}");
                        
                        // Target achievement status
                        var profitTarget = p.BestTradeProfit >= 15.0m ? "✅" : "❌";
                        var drawdownTarget = p.BestDrawdown <= 10.0 ? "✅" : "❌";
                        var winRateTarget = p.BestWinRate >= 70.0 ? "✅" : "❌";
                        var sharpeTarget = p.BestSharpe >= 1.5 ? "✅" : "❌";
                        
                        Console.WriteLine($"   Targets: {profitTarget}P&L {drawdownTarget}DD {winRateTarget}WR {sharpeTarget}Sharpe");
                        Console.WriteLine();
                    }
                });
                
                // Execute the genetic optimization
                var result = await optimizer.OptimizeAsync(progress);
                
                Console.WriteLine();
                Console.WriteLine("🏆 TWENTY-YEAR OPTIMIZATION COMPLETED");
                Console.WriteLine("=".PadRight(50, '='));
                Console.WriteLine($"Optimization Status: {(result.Success ? "✅ SUCCESS" : "❌ FAILED")}");
                Console.WriteLine($"Duration: {result.Duration.TotalHours:F1} hours");
                Console.WriteLine($"Generations Completed: {result.GenerationsCompleted}");
                Console.WriteLine($"Total Strategies Tested: {result.TotalStrategiesTested:N0}");
                Console.WriteLine();
                
                if (result.Success && result.BestStrategy != null)
                {
                    var best = result.BestStrategy;
                    var perf = best.Performance;
                    
                    Console.WriteLine("🎯 OPTIMAL STRATEGY DISCOVERED:");
                    Console.WriteLine("-".PadRight(40, '-'));
                    Console.WriteLine($"Average Trade P&L: ${perf.AverageTradeProfit:F2}");
                    Console.WriteLine($"Total Trades: {perf.TotalTrades:N0}");
                    Console.WriteLine($"Win Rate: {perf.WinRate:F1}%");
                    Console.WriteLine($"Total P&L: ${perf.TotalProfitLoss:F2}");
                    Console.WriteLine($"Maximum Drawdown: {perf.MaxDrawdown:F1}%");
                    Console.WriteLine($"Sharpe Ratio: {perf.SharpeRatio:F2}");
                    Console.WriteLine($"Calmar Ratio: {perf.CalmarRatio:F2}");
                    Console.WriteLine();
                    
                    Console.WriteLine("🔧 KEY OPTIMAL PARAMETERS:");
                    Console.WriteLine("-".PadRight(30, '-'));
                    var parameters = best.Parameters;
                    Console.WriteLine($"Short Delta: {parameters.GetValueOrDefault("ShortDelta", 0):F3}");
                    Console.WriteLine($"Width Points: {parameters.GetValueOrDefault("WidthPoints", 0):F2}");
                    Console.WriteLine($"Credit Ratio: {parameters.GetValueOrDefault("CreditRatio", 0):F3}");
                    Console.WriteLine($"Stop Multiple: {parameters.GetValueOrDefault("StopMultiple", 0):F2}");
                    Console.WriteLine($"GoScore Base: {parameters.GetValueOrDefault("GoScoreBase", 0):F1}");
                    Console.WriteLine($"Bull Market Aggression: {parameters.GetValueOrDefault("BullMarketAggression", 0):F2}");
                    Console.WriteLine($"Bear Market Defense: {parameters.GetValueOrDefault("BearMarketDefense", 0):F2}");
                    Console.WriteLine();
                    
                    Console.WriteLine("🛡️ REVERSE FIBONACCI RISK LEVELS:");
                    Console.WriteLine("-".PadRight(35, '-'));
                    Console.WriteLine($"Level 1 (Initial): ${parameters.GetValueOrDefault("FibLevel1", 0):F0}");
                    Console.WriteLine($"Level 2 (First Loss): ${parameters.GetValueOrDefault("FibLevel2", 0):F0}");
                    Console.WriteLine($"Level 3 (Second Loss): ${parameters.GetValueOrDefault("FibLevel3", 0):F0}");
                    Console.WriteLine($"Level 4 (Max Defense): ${parameters.GetValueOrDefault("FibLevel4", 0):F0}");
                    Console.WriteLine($"Reset Profit Threshold: ${parameters.GetValueOrDefault("FibResetProfit", 0):F0}");
                    Console.WriteLine();
                    
                    // Check target achievement
                    var meetsProfit = perf.AverageTradeProfit >= 15.0m;
                    var meetsDrawdown = perf.MaxDrawdown <= 10.0;
                    var meetsWinRate = perf.WinRate >= 70.0;
                    var meetsSharpe = perf.SharpeRatio >= 1.5;
                    var allTargetsMet = meetsProfit && meetsDrawdown && meetsWinRate && meetsSharpe;
                    
                    Console.WriteLine("✅ TARGET ACHIEVEMENT ANALYSIS:");
                    Console.WriteLine("-".PadRight(35, '-'));
                    Console.WriteLine($"$15+ Trade Target: {(meetsProfit ? "✅ ACHIEVED" : "❌ MISSED")} (${perf.AverageTradeProfit:F2})");
                    Console.WriteLine($"10% Drawdown Limit: {(meetsDrawdown ? "✅ ACHIEVED" : "❌ EXCEEDED")} ({perf.MaxDrawdown:F1}%)");
                    Console.WriteLine($"70% Win Rate Target: {(meetsWinRate ? "✅ ACHIEVED" : "❌ MISSED")} ({perf.WinRate:F1}%)");
                    Console.WriteLine($"1.5 Sharpe Target: {(meetsSharpe ? "✅ ACHIEVED" : "❌ MISSED")} ({perf.SharpeRatio:F2})");
                    Console.WriteLine();
                    
                    if (allTargetsMet)
                    {
                        Console.WriteLine("🎉 COMPLETE OPTIMIZATION SUCCESS!");
                        Console.WriteLine("================================");
                        Console.WriteLine("✅ PM250 successfully evolved for 20-year market conditions");
                        Console.WriteLine("✅ All performance targets achieved simultaneously");
                        Console.WriteLine("✅ Capital preservation maintained across all market regimes");
                        Console.WriteLine("✅ Reverse Fibonacci risk management optimally calibrated");
                        Console.WriteLine("✅ Strategy parameters saved for production deployment");
                        Console.WriteLine();
                        Console.WriteLine("🚀 READY FOR PRODUCTION:");
                        Console.WriteLine("1. Deploy optimized parameters to live PM250 strategy");
                        Console.WriteLine("2. Implement reverse Fibonacci risk management");
                        Console.WriteLine("3. Begin paper trading with battle-tested parameters");
                        Console.WriteLine("4. Monitor performance across live market conditions");
                        Console.WriteLine("5. Parameters automatically saved for future sessions");
                    }
                    else
                    {
                        Console.WriteLine("⚠️ PARTIAL OPTIMIZATION SUCCESS");
                        Console.WriteLine("Some targets achieved, others need refinement:");
                        
                        if (!meetsProfit)
                        {
                            Console.WriteLine($"• Trade profit target missed by ${15.0m - perf.AverageTradeProfit:F2}");
                            Console.WriteLine("  Consider increasing position sizes or improving entry timing");
                        }
                        
                        if (!meetsDrawdown)
                        {
                            Console.WriteLine($"• Drawdown exceeded by {perf.MaxDrawdown - 10.0:F1}%");
                            Console.WriteLine("  Consider tighter stop losses or position size reduction");
                        }
                        
                        if (!meetsWinRate)
                        {
                            Console.WriteLine($"• Win rate missed by {70.0 - perf.WinRate:F1}%");
                            Console.WriteLine("  Consider more selective entry criteria or wider spreads");
                        }
                        
                        if (!meetsSharpe)
                        {
                            Console.WriteLine($"• Sharpe ratio missed by {1.5 - perf.SharpeRatio:F2}");
                            Console.WriteLine("  Consider risk management improvements or volatility filtering");
                        }
                        
                        Console.WriteLine();
                        Console.WriteLine("💡 RECOMMENDATIONS:");
                        Console.WriteLine("1. Run additional optimization cycles with adjusted parameters");
                        Console.WriteLine("2. Consider relaxing some targets if others are strongly achieved");
                        Console.WriteLine("3. Analyze market period performance for specific weaknesses");
                        Console.WriteLine("4. Current parameters saved as best-effort solution");
                    }
                    
                    Console.WriteLine();
                    Console.WriteLine($"💾 PERSISTENCE: Optimal parameters saved to model file");
                    Console.WriteLine("   Future optimization sessions will start from these parameters");
                    Console.WriteLine("   Model can be loaded for production deployment");
                }
                else
                {
                    Console.WriteLine("❌ OPTIMIZATION FAILED");
                    Console.WriteLine("No viable strategy found meeting minimum requirements");
                    Console.WriteLine();
                    Console.WriteLine("💡 TROUBLESHOOTING SUGGESTIONS:");
                    Console.WriteLine("1. Check 20-year market data availability and quality");
                    Console.WriteLine("2. Verify backtest engine is properly configured");
                    Console.WriteLine("3. Consider relaxing minimum performance constraints");
                    Console.WriteLine("4. Increase population size or generation count");
                    Console.WriteLine("5. Review parameter ranges for realistic bounds");
                }
                
                // Final validation
                Assert.True(result.GenerationsCompleted > 0, "Should complete at least one generation");
                Assert.True(result.TotalStrategiesTested > 0, "Should test at least some strategies");
                
                if (result.Success)
                {
                    Assert.NotNull(result.BestStrategy);
                    Assert.True(result.BestStrategy.Performance.TotalTrades > 0, "Best strategy should have trades");
                    
                    Console.WriteLine("✅ All optimization validations passed");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ GENETIC OPTIMIZATION ERROR: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                
                Console.WriteLine();
                Console.WriteLine("🔧 DIAGNOSTIC INFORMATION:");
                Console.WriteLine("1. Check that PM250_GeneticOptimizer_v2 is properly implemented");
                Console.WriteLine("2. Verify 20-year market data is available in the database");
                Console.WriteLine("3. Ensure sufficient system resources for optimization");
                Console.WriteLine("4. Check that all required dependencies are loaded");
                
                throw;
            }
        }
    }
}