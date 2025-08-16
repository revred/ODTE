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
    /// PM250 Live Trading Simulation with Real 2015-2016 Data
    /// Uses the trading-enabled strategy with realistic thresholds
    /// </summary>
    public class PM250_LiveTradingSimulation
    {
        private readonly PM250_TradingEnabledStrategy _pm250Strategy;
        private readonly HistoricalDataManager _dataManager;
        private readonly List<string> _tradingLog;

        public PM250_LiveTradingSimulation()
        {
            _pm250Strategy = new PM250_TradingEnabledStrategy();
            _dataManager = new HistoricalDataManager();
            _tradingLog = new List<string>();
        }

        [Fact]
        public async Task PM250_Execute_Live_Trading_Simulation_2015_2016()
        {
            LogTrade("🚀 PM250 LIVE TRADING SIMULATION - 2015-2016 REAL DATA");
            LogTrade("=".PadRight(70, '='));
            LogTrade("Strategy: PM250 Trading-Enabled (Realistic Thresholds)");
            LogTrade("Period: 2015-2016 Historical Data");
            LogTrade("Target: Generate actual trades and P&L results");
            LogTrade("");

            try
            {
                // Initialize data
                await _dataManager.InitializeAsync();
                var stats = await _dataManager.GetStatsAsync();
                
                LogTrade($"📊 Historical Data Available:");
                LogTrade($"   Records: {stats.TotalRecords:N0}");
                LogTrade($"   Date Range: {stats.StartDate:yyyy-MM-dd} to {stats.EndDate:yyyy-MM-dd}");
                LogTrade($"   Database Size: {stats.DatabaseSizeMB:N1} MB");
                LogTrade("");

                // Execute focused trading simulation (first month of 2015 for speed)
                var simulationStart = new DateTime(2015, 1, 5, 9, 30, 0);
                var simulationEnd = new DateTime(2015, 2, 28, 16, 0, 0);
                
                LogTrade($"⚡ EXECUTING FOCUSED TRADING SIMULATION");
                LogTrade($"   Period: {simulationStart:yyyy-MM-dd} to {simulationEnd:yyyy-MM-dd}");
                LogTrade($"   Time Step: 15 minutes (for performance)");
                LogTrade("");

                var results = new List<StrategyResult>();
                var currentTime = simulationStart;
                var executedTrades = 0;
                var totalOpportunities = 0;
                var weeklyTrades = 0;
                var currentWeekStart = GetWeekStart(currentTime);

                LogTrade("   🎯 TRADE EXECUTION LOG:");
                LogTrade("   " + "-".PadRight(50, '-'));

                while (currentTime <= simulationEnd)
                {
                    // Reset weekly counter
                    if (GetWeekStart(currentTime) != currentWeekStart)
                    {
                        LogTrade($"   Week {currentWeekStart:yyyy-MM-dd}: {weeklyTrades} trades executed");
                        weeklyTrades = 0;
                        currentWeekStart = GetWeekStart(currentTime);
                    }

                    // Only trade during market hours on weekdays
                    if (IsWithinTradingHours(currentTime))
                    {
                        totalOpportunities++;
                        
                        try
                        {
                            var marketData = await GetMarketDataForTime(currentTime);
                            if (marketData != null)
                            {
                                var conditions = CreateMarketConditionsFromData(marketData, currentTime);
                                var parameters = new StrategyParameters { PositionSize = 1, MaxRisk = 500 };

                                var result = await _pm250Strategy.ExecuteAsync(parameters, conditions);
                                
                                if (result.PnL != 0) // Trade executed
                                {
                                    results.Add(result);
                                    executedTrades++;
                                    weeklyTrades++;
                                    
                                    var tradeType = result.IsWin ? "WIN" : "LOSS";
                                    LogTrade($"   #{executedTrades:D3} {currentTime:MM-dd HH:mm} {tradeType} ${result.PnL:+0.00;-0.00} | GoScore: {result.Metadata.GetValueOrDefault("GoScore", "N/A")} | VIX: {conditions.VIX:F1}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogTrade($"   ERROR at {currentTime:yyyy-MM-dd HH:mm}: {ex.Message}");
                        }
                    }

                    // Move forward by 15 minutes for performance
                    currentTime = currentTime.AddMinutes(15);
                }

                LogTrade($"   Final Week: {weeklyTrades} trades");
                LogTrade("");

                // Generate comprehensive results
                GenerateTradingResults(results, totalOpportunities, simulationStart, simulationEnd);

                LogTrade("✅ PM250 LIVE TRADING SIMULATION COMPLETED SUCCESSFULLY");
            }
            catch (Exception ex)
            {
                LogTrade($"❌ SIMULATION FAILED: {ex.Message}");
                LogTrade($"Stack Trace: {ex.StackTrace}");
                throw;
            }
            finally
            {
                OutputTradingLog();
            }
        }

        private void GenerateTradingResults(List<StrategyResult> results, int totalOpportunities, DateTime start, DateTime end)
        {
            LogTrade("📈 PM250 LIVE TRADING RESULTS");
            LogTrade("=".PadRight(70, '='));
            
            var executedTrades = results.Where(r => r.PnL != 0).ToList();
            var winners = executedTrades.Where(r => r.IsWin).ToList();
            var losers = executedTrades.Where(r => !r.IsWin).ToList();
            
            var totalPnL = executedTrades.Sum(r => r.PnL);
            var winRate = executedTrades.Count > 0 ? winners.Count / (double)executedTrades.Count : 0;
            var avgWinner = winners.Count > 0 ? winners.Average(r => r.PnL) : 0;
            var avgLoser = losers.Count > 0 ? losers.Average(r => r.PnL) : 0;
            var executionRate = totalOpportunities > 0 ? executedTrades.Count / (double)totalOpportunities : 0;

            LogTrade("");
            LogTrade("💰 PROFITABILITY ANALYSIS:");
            LogTrade($"   Total P&L: ${totalPnL:N2}");
            LogTrade($"   Total Trades: {executedTrades.Count:N0}");
            LogTrade($"   Average per Trade: ${(executedTrades.Count > 0 ? totalPnL / executedTrades.Count : 0):F2}");
            LogTrade($"   Execution Rate: {executionRate:P1} ({executedTrades.Count}/{totalOpportunities})");
            LogTrade("");
            
            LogTrade("🎯 WIN/LOSS STATISTICS:");
            LogTrade($"   Win Rate: {winRate:P1} ({winners.Count}/{executedTrades.Count})");
            LogTrade($"   Average Winner: ${avgWinner:F2}");
            LogTrade($"   Average Loser: ${avgLoser:F2}");
            LogTrade($"   Largest Winner: ${(winners.Count > 0 ? winners.Max(r => r.PnL) : 0):F2}");
            LogTrade($"   Largest Loser: ${(losers.Count > 0 ? losers.Min(r => r.PnL) : 0):F2}");
            LogTrade("");

            LogTrade("📅 TIME-BASED PERFORMANCE:");
            LogTrade($"   Trading Period: {start:yyyy-MM-dd} to {end:yyyy-MM-dd}");
            LogTrade($"   Calendar Days: {(end - start).TotalDays:F0}");
            LogTrade($"   Total Opportunities: {totalOpportunities:N0}");
            LogTrade("");

            // Profit factor
            var grossProfit = winners.Sum(r => r.PnL);
            var grossLoss = Math.Abs(losers.Sum(r => r.PnL));
            var profitFactor = grossLoss > 0 ? grossProfit / grossLoss : 0;
            
            LogTrade("🏆 PERFORMANCE METRICS:");
            LogTrade($"   Profit Factor: {profitFactor:F2}");
            LogTrade($"   Risk/Reward: {(avgLoser != 0 ? Math.Abs(avgWinner / avgLoser) : 0):F2}:1");
            LogTrade($"   Strategy: {_pm250Strategy.Name}");
            LogTrade($"   Expected Win Rate: {_pm250Strategy.ExpectedWinRate:P0}");
            LogTrade("");

            // Performance validation
            var meetsTargets = winRate >= 0.70 && totalPnL > 0 && executionRate >= 0.05;
            LogTrade($"🎯 PM250 PERFORMANCE: {(meetsTargets ? "✅ EXCELLENT" : "⚠️ NEEDS TUNING")}");
            if (meetsTargets)
            {
                LogTrade("   PM250 successfully demonstrates profitable trading on real 2015 data!");
                LogTrade("   Ready for full 2015-2016 simulation and live deployment.");
            }
        }

        // Helper methods (same as before)
        private bool IsWithinTradingHours(DateTime time)
        {
            var timeOfDay = time.TimeOfDay;
            var isWeekday = time.DayOfWeek >= DayOfWeek.Monday && time.DayOfWeek <= DayOfWeek.Friday;
            var isDuringHours = timeOfDay >= new TimeSpan(9, 30, 0) && timeOfDay <= new TimeSpan(16, 0, 0);
            return isWeekday && isDuringHours;
        }

        private async Task<MarketDataBar?> GetMarketDataForTime(DateTime time)
        {
            try
            {
                var data = await _dataManager.GetMarketDataAsync("XSP", time.Date, time.Date.AddDays(1));
                return data.FirstOrDefault(d => Math.Abs((d.Timestamp - time).TotalMinutes) < 30);
            }
            catch
            {
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
                IVRank = 0.5
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

        private DateTime GetWeekStart(DateTime date)
        {
            var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.AddDays(-1 * diff).Date;
        }

        private void LogTrade(string message)
        {
            _tradingLog.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
        }

        private void OutputTradingLog()
        {
            Console.WriteLine("");
            Console.WriteLine("🔍 PM250 LIVE TRADING SIMULATION LOG:");
            Console.WriteLine("=".PadRight(70, '='));
            
            foreach (var logEntry in _tradingLog)
            {
                Console.WriteLine(logEntry);
            }
            
            Console.WriteLine("=".PadRight(70, '='));
            Console.WriteLine($"Total log entries: {_tradingLog.Count}");
            Console.WriteLine("");
        }
    }
}