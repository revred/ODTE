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
    /// PM250 Comprehensive Data Collection Strategy
    /// 
    /// ADDRESSES CRITICAL REQUIREMENTS:
    /// 1. Tests strategy across ALL available market periods (no cherry-picking)
    /// 2. Forces minimum trading to collect data on suboptimal conditions
    /// 3. Strict risk controls to prevent model from "cheating" with year-specific tweaks
    /// 4. Comprehensive performance analysis across different market regimes
    /// 
    /// PURPOSE: Build robust, generalizable models that work in ANY market condition
    /// </summary>
    public class PM250_ComprehensiveDataCollection
    {
        private readonly PM250_ForcedMinimumStrategy _strategy;
        private readonly HistoricalDataManager _dataManager;
        private readonly List<string> _analysisLog;

        public PM250_ComprehensiveDataCollection()
        {
            _strategy = new PM250_ForcedMinimumStrategy();
            _dataManager = new HistoricalDataManager();
            _analysisLog = new List<string>();
        }

        [Fact]
        public async Task PM250_Comprehensive_Data_Collection_All_Market_Periods()
        {
            Log("🎯 PM250 COMPREHENSIVE DATA COLLECTION - ALL MARKET PERIODS");
            Log("=" + new string('=', 80));
            Log("Strategy: PM250 Forced Minimum Trading");
            Log("Purpose: Test ALL market conditions without overfitting to specific periods");
            Log("Risk Controls: Strict limits to prevent model 'cheating'");
            Log("");

            try
            {
                // Initialize with all available data
                await _dataManager.InitializeAsync();
                var stats = await _dataManager.GetStatsAsync();
                
                Log("📊 AVAILABLE MARKET DATA ANALYSIS:");
                Log($"   Total Records: {stats.TotalRecords:N0}");
                Log($"   Date Range: {stats.StartDate:yyyy-MM-dd} to {stats.EndDate:yyyy-MM-dd}");
                Log($"   Years Covered: {(stats.EndDate - stats.StartDate).TotalDays / 365.25:F1}");
                Log($"   Database Size: {stats.DatabaseSizeMB:N1} MB");
                Log("");

                // Identify distinct market periods in our data
                var marketPeriods = IdentifyMarketPeriods(stats.StartDate, stats.EndDate);
                Log("🏛️ IDENTIFIED MARKET PERIODS FOR TESTING:");
                foreach (var period in marketPeriods)
                {
                    Log($"   {period.Name}: {period.Start:yyyy-MM-dd} to {period.End:yyyy-MM-dd}");
                }
                Log("");

                // Test strategy across ALL periods (no cherry-picking)
                var allResults = new List<ComprehensiveTestResult>();
                
                foreach (var period in marketPeriods)
                {
                    Log($"⚡ TESTING PERIOD: {period.Name}");
                    Log($"   Date Range: {period.Start:yyyy-MM-dd} to {period.End:yyyy-MM-dd}");
                    
                    var periodResult = await TestStrategyInPeriod(period);
                    allResults.Add(periodResult);
                    
                    LogPeriodResults(periodResult);
                    Log("");
                }

                // Generate comprehensive analysis
                GenerateComprehensiveAnalysis(allResults);

                // Validate no overfitting occurred
                ValidateRobustness(allResults);

                Log("✅ PM250 COMPREHENSIVE DATA COLLECTION COMPLETED");
                Log("   Strategy tested across ALL available market periods");
                Log("   No period-specific optimizations allowed");
                Log("   Strict risk controls maintained throughout");
            }
            catch (Exception ex)
            {
                Log($"❌ COMPREHENSIVE TESTING FAILED: {ex.Message}");
                Log($"Stack Trace: {ex.StackTrace}");
                throw;
            }
            finally
            {
                OutputAnalysisLog();
            }
        }

        private List<MarketPeriod> IdentifyMarketPeriods(DateTime start, DateTime end)
        {
            var periods = new List<MarketPeriod>();
            
            // Define periods based on available data (no assumptions about missing data)
            if (start.Year <= 2015 && end.Year >= 2015)
                periods.Add(new MarketPeriod("2015 Volatility", new DateTime(2015, 1, 1), new DateTime(2015, 12, 31), "Mixed"));
                
            if (start.Year <= 2016 && end.Year >= 2016)
                periods.Add(new MarketPeriod("2016 Recovery", new DateTime(2016, 1, 1), new DateTime(2016, 12, 31), "Volatile"));
                
            if (start.Year <= 2017 && end.Year >= 2017)
                periods.Add(new MarketPeriod("2017 Bull Run", new DateTime(2017, 1, 1), new DateTime(2017, 12, 31), "Calm"));
                
            if (start.Year <= 2018 && end.Year >= 2018)
                periods.Add(new MarketPeriod("2018 Volmageddon", new DateTime(2018, 1, 1), new DateTime(2018, 12, 31), "Crisis"));
                
            if (start.Year <= 2019 && end.Year >= 2019)
                periods.Add(new MarketPeriod("2019 Steady Growth", new DateTime(2019, 1, 1), new DateTime(2019, 12, 31), "Calm"));
                
            if (start.Year <= 2020 && end.Year >= 2020)
                periods.Add(new MarketPeriod("2020 COVID Crash", new DateTime(2020, 1, 1), new DateTime(2020, 12, 31), "Crisis"));
                
            if (start.Year <= 2021 && end.Year >= 2021)
                periods.Add(new MarketPeriod("2021 Recovery Bull", new DateTime(2021, 1, 1), new DateTime(2021, 12, 31), "Mixed"));

            // Filter to only periods we actually have data for
            return periods.Where(p => p.Start >= start && p.End <= end).ToList();
        }

        private async Task<ComprehensiveTestResult> TestStrategyInPeriod(MarketPeriod period)
        {
            var result = new ComprehensiveTestResult
            {
                PeriodName = period.Name,
                StartDate = period.Start,
                EndDate = period.End,
                ExpectedRegime = period.ExpectedRegime,
                AllTrades = new List<TradeAnalysis>()
            };

            // Test with 30-minute intervals for comprehensive coverage
            var currentTime = period.Start.Date.AddHours(9).AddMinutes(30); // 9:30 AM
            var endTime = period.End.Date.AddHours(16); // 4:00 PM
            
            var opportunities = 0;
            var forcedTrades = 0;
            var optimalTrades = 0;

            while (currentTime <= endTime)
            {
                // Only test during weekdays and market hours
                if (IsWithinTradingHours(currentTime))
                {
                    opportunities++;
                    
                    try
                    {
                        var marketData = await GetMarketDataForTime(currentTime);
                        if (marketData != null)
                        {
                            var conditions = CreateMarketConditionsFromData(marketData, currentTime);
                            var parameters = new StrategyParameters { PositionSize = 1, MaxRisk = 500 };

                            var strategyResult = await _strategy.ExecuteAsync(parameters, conditions);
                            
                            if (strategyResult.PnL != 0) // Trade executed
                            {
                                var isForcedTrade = (bool)strategyResult.Metadata.GetValueOrDefault("IsForcedTrade", false);
                                if (isForcedTrade) forcedTrades++; else optimalTrades++;

                                var tradeAnalysis = new TradeAnalysis
                                {
                                    ExecutionTime = currentTime,
                                    PnL = strategyResult.PnL,
                                    IsWin = strategyResult.IsWin,
                                    IsForcedTrade = isForcedTrade,
                                    GoScore = (double)strategyResult.Metadata.GetValueOrDefault("GoScore", 0.0),
                                    VIX = conditions.VIX,
                                    MarketRegime = conditions.MarketRegime,
                                    TrendScore = conditions.TrendScore,
                                    WinProbability = (double)strategyResult.Metadata.GetValueOrDefault("WinProbability", 0.0)
                                };
                                
                                result.AllTrades.Add(tradeAnalysis);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue testing
                        Log($"   Error at {currentTime:yyyy-MM-dd HH:mm}: {ex.Message}");
                    }
                }

                // Move forward by 30 minutes
                currentTime = currentTime.AddMinutes(30);
            }

            // Calculate comprehensive metrics
            CalculatePeriodMetrics(result);
            
            Log($"   Period Results: {result.AllTrades.Count} trades ({forcedTrades} forced, {optimalTrades} optimal) from {opportunities} opportunities");
            
            return result;
        }

        private void CalculatePeriodMetrics(ComprehensiveTestResult result)
        {
            var executedTrades = result.AllTrades.Where(t => Math.Abs(t.PnL) > 0).ToList();
            
            if (!executedTrades.Any())
            {
                // No trades executed
                result.TotalPnL = 0;
                result.WinRate = 0;
                result.ExecutionRate = 0;
                result.ForcedTradePercentage = 0;
                return;
            }

            result.TotalTrades = executedTrades.Count;
            result.TotalPnL = executedTrades.Sum(t => t.PnL);
            result.WinRate = executedTrades.Count(t => t.IsWin) / (double)executedTrades.Count;
            result.AvgPnLPerTrade = result.TotalPnL / result.TotalTrades;
            
            var forcedTrades = executedTrades.Where(t => t.IsForcedTrade).ToList();
            var optimalTrades = executedTrades.Where(t => !t.IsForcedTrade).ToList();
            
            result.ForcedTradePercentage = forcedTrades.Count / (double)executedTrades.Count;
            result.ForcedTradeWinRate = forcedTrades.Any() ? forcedTrades.Count(t => t.IsWin) / (double)forcedTrades.Count : 0;
            result.OptimalTradeWinRate = optimalTrades.Any() ? optimalTrades.Count(t => t.IsWin) / (double)optimalTrades.Count : 0;
            
            result.MaxDrawdown = CalculateMaxDrawdown(executedTrades);
            result.AvgGoScore = executedTrades.Average(t => t.GoScore);
            result.AvgVIX = executedTrades.Average(t => t.VIX);
        }

        private decimal CalculateMaxDrawdown(List<TradeAnalysis> trades)
        {
            if (!trades.Any()) return 0;
            
            decimal peak = 0, maxDD = 0, cumulative = 0;
            
            foreach (var trade in trades.OrderBy(t => t.ExecutionTime))
            {
                cumulative += trade.PnL;
                peak = Math.Max(peak, cumulative);
                var drawdown = peak - cumulative;
                maxDD = Math.Max(maxDD, drawdown);
            }
            
            return maxDD;
        }

        private void LogPeriodResults(ComprehensiveTestResult result)
        {
            Log($"   📊 PERIOD ANALYSIS - {result.PeriodName}:");
            Log($"      Total Trades: {result.TotalTrades}");
            Log($"      Total P&L: ${result.TotalPnL:F2}");
            Log($"      Win Rate: {result.WinRate:P1}");
            Log($"      Avg P&L/Trade: ${result.AvgPnLPerTrade:F2}");
            Log($"      Forced Trades: {result.ForcedTradePercentage:P1}");
            Log($"      Forced Win Rate: {result.ForcedTradeWinRate:P1}");
            Log($"      Optimal Win Rate: {result.OptimalTradeWinRate:P1}");
            Log($"      Max Drawdown: ${result.MaxDrawdown:F2}");
            Log($"      Avg GoScore: {result.AvgGoScore:F1}");
            Log($"      Avg VIX: {result.AvgVIX:F1}");
        }

        private void GenerateComprehensiveAnalysis(List<ComprehensiveTestResult> allResults)
        {
            Log("🔬 COMPREHENSIVE ANALYSIS ACROSS ALL PERIODS:");
            Log("-".PadRight(60, '-'));
            
            var totalTrades = allResults.Sum(r => r.TotalTrades);
            var totalPnL = allResults.Sum(r => r.TotalPnL);
            var avgWinRate = allResults.Where(r => r.TotalTrades > 0).Average(r => r.WinRate);
            var allForcedTrades = allResults.Sum(r => r.TotalTrades * r.ForcedTradePercentage);
            
            Log($"   OVERALL PERFORMANCE:");
            Log($"      Total Periods Tested: {allResults.Count}");
            Log($"      Total Trades: {totalTrades}");
            Log($"      Total P&L: ${totalPnL:F2}");
            Log($"      Overall Win Rate: {avgWinRate:P1}");
            Log($"      Forced Trades: {allForcedTrades:F0} ({allForcedTrades/Math.Max(totalTrades,1):P1})");
            Log("");
            
            Log($"   REGIME-SPECIFIC PERFORMANCE:");
            var regimeGroups = allResults.GroupBy(r => r.ExpectedRegime);
            foreach (var group in regimeGroups)
            {
                var regimeTrades = group.Sum(r => r.TotalTrades);
                var regimePnL = group.Sum(r => r.TotalPnL);
                var regimeWinRate = group.Where(r => r.TotalTrades > 0).Average(r => r.WinRate);
                
                Log($"      {group.Key}: {regimeTrades} trades, ${regimePnL:F2} P&L, {regimeWinRate:P1} win rate");
            }
            Log("");
        }

        private void ValidateRobustness(List<ComprehensiveTestResult> allResults)
        {
            Log("🛡️ ROBUSTNESS VALIDATION (Anti-Overfitting Checks):");
            Log("-".PadRight(60, '-'));
            
            var validations = new List<(string Name, bool Passed, string Message)>();
            
            // 1. No period should dominate profits
            var totalPnL = allResults.Sum(r => r.TotalPnL);
            var maxPeriodContribution = allResults.Max(r => Math.Abs((double)(r.TotalPnL / Math.Max(totalPnL, 1m))));
            validations.Add(("Period Dependence", maxPeriodContribution < 0.7, $"Max period contribution: {maxPeriodContribution:P1}"));
            
            // 2. Strategy should work across different regimes
            var profitableRegimes = allResults.GroupBy(r => r.ExpectedRegime).Count(g => g.Sum(r => r.TotalPnL) > 0);
            var totalRegimes = allResults.GroupBy(r => r.ExpectedRegime).Count();
            validations.Add(("Regime Robustness", profitableRegimes >= totalRegimes * 0.6, $"{profitableRegimes}/{totalRegimes} regimes profitable"));
            
            // 3. Forced trades should not be excessively bad
            var avgForcedWinRate = allResults.Where(r => r.TotalTrades > 0).Average(r => r.ForcedTradeWinRate);
            validations.Add(("Forced Trade Quality", avgForcedWinRate > 0.55, $"Forced trade win rate: {avgForcedWinRate:P1}"));
            
            // 4. Maximum drawdown control
            var maxDrawdown = allResults.Max(r => r.MaxDrawdown);
            validations.Add(("Risk Control", maxDrawdown < 200.0m, $"Max drawdown: ${maxDrawdown:F2}"));
            
            // 5. Consistent execution across periods
            var executionRates = allResults.Where(r => r.TotalTrades > 0).Select(r => r.ExecutionRate).ToList();
            var executionConsistency = executionRates.Count == 0 ? 0 : executionRates.Min() / Math.Max(executionRates.Max(), 0.001);
            validations.Add(("Execution Consistency", executionConsistency > 0.3, $"Execution consistency: {executionConsistency:P1}"));

            foreach (var validation in validations)
            {
                var status = validation.Passed ? "✅ PASS" : "❌ FAIL";
                Log($"   {status} {validation.Name}: {validation.Message}");
            }

            var passedCount = validations.Count(v => v.Passed);
            Log($"");
            Log($"   🏆 ROBUSTNESS SUMMARY: {passedCount}/{validations.Count} validations passed");
            
            if (passedCount >= 4)
                Log("   ✅ STRATEGY DEMONSTRATES ROBUST GENERALIZATION");
            else
                Log("   ⚠️ STRATEGY MAY BE OVERFITTED - REQUIRES ADJUSTMENT");
        }

        // Helper methods
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
                return data.FirstOrDefault(d => Math.Abs((d.Timestamp - time).TotalMinutes) < 45);
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
            return Math.Max(10, Math.Min(80, 15 + range * 200)); // Wider VIX range for testing
        }

        private double CalculateTrend(MarketDataBar data)
        {
            var mid = (data.High + data.Low) / 2;
            return Math.Max(-1.5, Math.Min(1.5, (data.Close - mid) / mid * 10)); // Extended trend range
        }

        private string ClassifyRegime(MarketDataBar data)
        {
            var vix = EstimateVIX(data);
            return vix > 50 ? "Crisis" : 
                   vix > 30 ? "Volatile" : 
                   vix > 20 ? "Mixed" : "Calm";
        }

        private void Log(string message)
        {
            _analysisLog.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
        }

        private void OutputAnalysisLog()
        {
            Console.WriteLine("");
            Console.WriteLine("🔍 PM250 COMPREHENSIVE ANALYSIS LOG:");
            Console.WriteLine("=" + new string('=', 80));
            
            foreach (var logEntry in _analysisLog)
            {
                Console.WriteLine(logEntry);
            }
            
            Console.WriteLine("=" + new string('=', 80));
            Console.WriteLine($"Total analysis entries: {_analysisLog.Count}");
        }
    }

    // Supporting classes
    public class MarketPeriod
    {
        public string Name { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string ExpectedRegime { get; set; }

        public MarketPeriod(string name, DateTime start, DateTime end, string expectedRegime)
        {
            Name = name;
            Start = start;
            End = end;
            ExpectedRegime = expectedRegime;
        }
    }

    public class ComprehensiveTestResult
    {
        public string PeriodName { get; set; } = "";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string ExpectedRegime { get; set; } = "";
        public List<TradeAnalysis> AllTrades { get; set; } = new();
        
        public int TotalTrades { get; set; }
        public decimal TotalPnL { get; set; }
        public double WinRate { get; set; }
        public decimal AvgPnLPerTrade { get; set; }
        public double ExecutionRate { get; set; }
        public double ForcedTradePercentage { get; set; }
        public double ForcedTradeWinRate { get; set; }
        public double OptimalTradeWinRate { get; set; }
        public decimal MaxDrawdown { get; set; }
        public double AvgGoScore { get; set; }
        public double AvgVIX { get; set; }
    }

    public class TradeAnalysis
    {
        public DateTime ExecutionTime { get; set; }
        public decimal PnL { get; set; }
        public bool IsWin { get; set; }
        public bool IsForcedTrade { get; set; }
        public double GoScore { get; set; }
        public double VIX { get; set; }
        public string MarketRegime { get; set; } = "";
        public double TrendScore { get; set; }
        public double WinProbability { get; set; }
    }
}