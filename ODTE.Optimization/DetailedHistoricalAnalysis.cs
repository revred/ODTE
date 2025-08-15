using System;
using System.Collections.Generic;
using System.Linq;
using ODTE.Optimization.Core;

namespace ODTE.Optimization
{
    /// <summary>
    /// Deep dive analysis of the optimized strategy performance over 5 years
    /// Tracks every win, loss, and failure pattern to identify learning points
    /// </summary>
    public class DetailedHistoricalAnalysis
    {
        public class TradeResult
        {
            public DateTime Date { get; set; }
            public double PnL { get; set; }
            public bool IsWin { get; set; }
            public string MarketRegime { get; set; } = "";
            public double VIX { get; set; }
            public string FailureReason { get; set; } = "";
            public double LossSize { get; set; }
            public int ConsecutiveLosses { get; set; }
            public bool TriggeredRiskLimit { get; set; }
            public double DailyPnL { get; set; }
            public int TradesThisDay { get; set; }
        }

        public class FailureAnalysis
        {
            public string Category { get; set; } = "";
            public int Frequency { get; set; }
            public double TotalLoss { get; set; }
            public double AverageLoss { get; set; }
            public double MaxLoss { get; set; }
            public List<DateTime> OccurrenceDates { get; set; } = new();
            public string Pattern { get; set; } = "";
        }

        public class DetailedResults
        {
            public List<TradeResult> AllTrades { get; set; } = new();
            public List<FailureAnalysis> FailureCategories { get; set; } = new();
            public Dictionary<string, double> MarketRegimePerformance { get; set; } = new();
            public Dictionary<int, double> LossStreakAnalysis { get; set; } = new();
            public Dictionary<string, int> FailurePatterns { get; set; } = new();
            public List<DateTime> WorstDrawdownPeriods { get; set; } = new();
            public double WorstSingleDayLoss { get; set; }
            public double LongestDrawdownDays { get; set; }
            public double MaxConsecutiveLosses { get; set; }
            public double RecoveryTimeAfterLosses { get; set; }
        }

        public DetailedResults RunComprehensiveAnalysis()
        {
            Console.WriteLine("================================================================================");
            Console.WriteLine("COMPREHENSIVE 5-YEAR ODTE STRATEGY FAILURE ANALYSIS");
            Console.WriteLine("Analyzing every trade, loss pattern, and market condition over 1,294 days");
            Console.WriteLine("================================================================================");

            var results = new DetailedResults();
            var random = new Random(42); // Consistent seed for reproducible analysis
            
            // Simulate 5 years of detailed trading (1,294 trading days)
            var startDate = new DateTime(2019, 1, 2); // Start of 5-year period
            var currentDate = startDate;
            var consecutiveLosses = 0;
            var peak = 5000.0;
            var equity = 5000.0;
            var currentDrawdown = 0.0;
            var drawdownStartDate = DateTime.MinValue;
            
            // Track daily loss limits (Reverse Fibonacci)
            var dailyLossLimits = new[] { 500.0, 300.0, 200.0, 100.0 };
            var consecutiveLossDays = 0;

            for (int day = 0; day < 1294; day++)
            {
                // Skip weekends
                while (currentDate.DayOfWeek == DayOfWeek.Saturday || 
                       currentDate.DayOfWeek == DayOfWeek.Sunday)
                {
                    currentDate = currentDate.AddDays(1);
                }

                var dailyResult = SimulateDetailedTradingDay(currentDate, consecutiveLossDays, random);
                results.AllTrades.AddRange(dailyResult.Trades);

                // Update equity and drawdown tracking
                var dailyPnL = dailyResult.Trades.Sum(t => t.PnL);
                equity += dailyPnL;
                
                if (equity > peak)
                {
                    peak = equity;
                    if (currentDrawdown < 0) // End of drawdown
                    {
                        var drawdownDays = (currentDate - drawdownStartDate).TotalDays;
                        if (drawdownDays > results.LongestDrawdownDays)
                        {
                            results.LongestDrawdownDays = drawdownDays;
                        }
                    }
                    currentDrawdown = 0;
                }
                else
                {
                    var newDrawdown = equity - peak;
                    if (currentDrawdown == 0) // Start of new drawdown
                    {
                        drawdownStartDate = currentDate;
                    }
                    currentDrawdown = newDrawdown;
                    
                    if (newDrawdown < -1000) // Significant drawdown
                    {
                        results.WorstDrawdownPeriods.Add(currentDate);
                    }
                }

                // Track consecutive losses
                if (dailyPnL < 0)
                {
                    consecutiveLossDays++;
                    consecutiveLosses++;
                    if (consecutiveLosses > results.MaxConsecutiveLosses)
                    {
                        results.MaxConsecutiveLosses = consecutiveLosses;
                    }
                }
                else if (dailyPnL > 0)
                {
                    consecutiveLossDays = 0;
                    consecutiveLosses = 0;
                }

                // Track worst single day
                if (dailyPnL < results.WorstSingleDayLoss)
                {
                    results.WorstSingleDayLoss = dailyPnL;
                }

                currentDate = currentDate.AddDays(1);
            }

            // Analyze failure patterns
            AnalyzeFailurePatterns(results);
            AnalyzeMarketRegimePerformance(results);
            AnalyzeLossStreaks(results);
            GenerateFailureCategories(results);

            // Print comprehensive analysis
            PrintDetailedAnalysis(results);

            return results;
        }

        private DailyTradingResult SimulateDetailedTradingDay(DateTime date, int consecutiveLossDays, Random random)
        {
            var result = new DailyTradingResult { Trades = new List<TradeResult>() };
            var dailyLossLimits = new[] { 500.0, 300.0, 200.0, 100.0 };
            var currentLimit = dailyLossLimits[Math.Min(consecutiveLossDays, 3)];
            
            var dayPnL = 0.0;
            var tradesPlaced = 0;
            var maxTradesPerDay = 8; // Realistic trading frequency

            // Determine market regime for the day
            var marketRegime = DetermineMarketRegime(date, random);
            var vix = SimulateVIX(marketRegime, random);

            while (tradesPlaced < maxTradesPerDay && Math.Abs(dayPnL) < currentLimit)
            {
                var trade = SimulateDetailedTrade(date, marketRegime, vix, tradesPlaced, random);
                trade.DailyPnL = dayPnL + trade.PnL;
                trade.TradesThisDay = tradesPlaced + 1;
                
                result.Trades.Add(trade);
                dayPnL += trade.PnL;
                tradesPlaced++;

                // Stop if we hit daily limit
                if (dayPnL <= -currentLimit)
                {
                    trade.TriggeredRiskLimit = true;
                    break;
                }

                // Realistic exit probability
                if (tradesPlaced >= 3 && random.NextDouble() > 0.7)
                    break;
            }

            return result;
        }

        private TradeResult SimulateDetailedTrade(DateTime date, string regime, double vix, int tradeNumber, Random random)
        {
            var trade = new TradeResult
            {
                Date = date,
                MarketRegime = regime,
                VIX = vix
            };

            // Use Credit BWB + Convex Tail Overlay simulation
            var bwbEngine = new CreditBWBEngine(random);
            var convexOverlay = new ConvexTailOverlay(random);
            var parameters = new StrategyParameters(); // Default parameters for simulation
            
            var bwbResult = bwbEngine.SimulateCreditBWB(date, regime, vix, parameters);

            // Apply Convex Tail Overlay when conditions warrant
            var overlayConditions = ConvexTailOverlay.GenerateMarketConditions(regime, random);
            overlayConditions.VIX = vix;
            
            var marketMove = SimulateMarketMove(regime, random);
            var overlayResult = convexOverlay.ApplyConvexOverlay(
                bwbResult.PnL, 
                bwbResult.Structure, 
                overlayConditions, 
                marketMove);

            // Map failure reasons to analysis categories (include overlay information)
            var failureReason = bwbResult.ExitReason;
            if (overlayResult.TotalPnL <= 0) // Use total P&L to determine win/loss
            {
                var overlayInfo = overlayResult.OverlayActivated ? $" + {overlayResult.OverlayType}" : "";
                failureReason = bwbResult.ExitReason switch
                {
                    "Pin failure - limited loss" => $"BWB Pin failure - limited loss{overlayInfo}",
                    "Partial trend breach" => $"BWB Trend breach - partial{overlayInfo}",
                    "Full trend breach" => $"BWB Trend breach - full{overlayInfo}",
                    "Small volatility breach" => $"BWB Volatility breach - small{overlayInfo}",
                    "Medium volatility breach" => $"BWB Volatility breach - medium{overlayInfo}", 
                    "Large volatility breach" => $"BWB Volatility breach - large{overlayInfo}",
                    _ => $"BWB Unknown failure{overlayInfo}"
                };
            }
            else if (overlayResult.OverlayActivated && overlayResult.OverlayPnL > 0)
            {
                failureReason = $"Convex Tail Success - {overlayResult.OverlayType}";
            }

            trade.PnL = overlayResult.TotalPnL;
            trade.IsWin = overlayResult.TotalPnL > 0;
            trade.FailureReason = failureReason;
            trade.LossSize = overlayResult.TotalPnL < 0 ? Math.Abs(overlayResult.TotalPnL) : 0;

            return trade;
        }
        
        private double SimulateMarketMove(string marketRegime, Random random)
        {
            // Simulate daily market moves for convex overlay testing
            return marketRegime switch
            {
                "Volatile" => (random.NextDouble() - 0.5) * 0.08,  // ±4% moves
                "Trending" => (random.NextDouble() < 0.5 ? -1 : 1) * (0.005 + random.NextDouble() * 0.025), // Directional 0.5-3%
                "Calm" => (random.NextDouble() - 0.5) * 0.02,      // ±1% moves
                _ => (random.NextDouble() - 0.5) * 0.02
            };
        }

        private string DetermineMarketRegime(DateTime date, Random random)
        {
            // Simulate realistic market regime distribution
            // Add bias for known historical events
            
            if (IsVolatileEvent(date))
            {
                return "Volatile";
            }

            var regime = random.NextDouble();
            if (regime < 0.15) return "Volatile";
            else if (regime < 0.25) return "Trending"; 
            else return "Calm";
        }

        private bool IsVolatileEvent(DateTime date)
        {
            // Major market events that would cause volatility
            var volatileEvents = new[]
            {
                new DateTime(2020, 3, 16), // COVID crash
                new DateTime(2020, 3, 23), // COVID bottom
                new DateTime(2022, 6, 13), // CPI shock
                new DateTime(2023, 3, 13), // SVB collapse
                new DateTime(2021, 1, 27), // GameStop squeeze
                new DateTime(2022, 2, 24), // Russia invasion
            };

            return volatileEvents.Any(v => Math.Abs((date - v).TotalDays) < 3);
        }

        private double SimulateVIX(string regime, Random random)
        {
            return regime switch
            {
                "Volatile" => 30 + random.NextDouble() * 50, // 30-80 VIX
                "Trending" => 20 + random.NextDouble() * 20, // 20-40 VIX
                "Calm" => 12 + random.NextDouble() * 15,     // 12-27 VIX
                _ => 20
            };
        }

        private void AnalyzeFailurePatterns(DetailedResults results)
        {
            var losses = results.AllTrades.Where(t => !t.IsWin).ToList();
            
            foreach (var loss in losses)
            {
                if (!results.FailurePatterns.ContainsKey(loss.FailureReason))
                {
                    results.FailurePatterns[loss.FailureReason] = 0;
                }
                results.FailurePatterns[loss.FailureReason]++;
            }
        }

        private void AnalyzeMarketRegimePerformance(DetailedResults results)
        {
            var regimes = results.AllTrades.GroupBy(t => t.MarketRegime);
            
            foreach (var regime in regimes)
            {
                var totalPnL = regime.Sum(t => t.PnL);
                results.MarketRegimePerformance[regime.Key] = totalPnL;
            }
        }

        private void AnalyzeLossStreaks(DetailedResults results)
        {
            var consecutiveLosses = 0;
            
            foreach (var trade in results.AllTrades.OrderBy(t => t.Date))
            {
                if (!trade.IsWin)
                {
                    consecutiveLosses++;
                }
                else
                {
                    if (consecutiveLosses > 0)
                    {
                        if (!results.LossStreakAnalysis.ContainsKey(consecutiveLosses))
                        {
                            results.LossStreakAnalysis[consecutiveLosses] = 0;
                        }
                        results.LossStreakAnalysis[consecutiveLosses]++;
                    }
                    consecutiveLosses = 0;
                }
            }
        }

        private void GenerateFailureCategories(DetailedResults results)
        {
            var failureGroups = results.AllTrades
                .Where(t => !t.IsWin)
                .GroupBy(t => t.FailureReason)
                .Select(g => new FailureAnalysis
                {
                    Category = g.Key,
                    Frequency = g.Count(),
                    TotalLoss = g.Sum(t => Math.Abs(t.PnL)),
                    AverageLoss = g.Average(t => Math.Abs(t.PnL)),
                    MaxLoss = g.Max(t => Math.Abs(t.PnL)),
                    OccurrenceDates = g.Select(t => t.Date).ToList(),
                    Pattern = DeterminePattern(g.ToList())
                })
                .OrderByDescending(f => f.TotalLoss)
                .ToList();

            results.FailureCategories = failureGroups;
        }

        private string DeterminePattern(List<TradeResult> trades)
        {
            if (trades.Count < 5) return "Isolated incidents";
            
            var avgVIX = trades.Average(t => t.VIX);
            if (avgVIX > 40) return "High volatility clustering";
            
            var dateSpread = (trades.Max(t => t.Date) - trades.Min(t => t.Date)).TotalDays;
            if (dateSpread < 30) return "Clustered in time";
            
            return "Distributed pattern";
        }

        private void PrintDetailedAnalysis(DetailedResults results)
        {
            var totalTrades = results.AllTrades.Count;
            var wins = results.AllTrades.Count(t => t.IsWin);
            var losses = totalTrades - wins;
            var totalPnL = results.AllTrades.Sum(t => t.PnL);
            var totalLossAmount = results.AllTrades.Where(t => !t.IsWin).Sum(t => Math.Abs(t.PnL));

            Console.WriteLine($"\n🔍 DEEP DIVE ANALYSIS RESULTS:");
            Console.WriteLine($"Total Trades: {totalTrades:N0}");
            Console.WriteLine($"Winners: {wins:N0} ({(double)wins/totalTrades:P1})");
            Console.WriteLine($"Losers: {losses:N0} ({(double)losses/totalTrades:P1})");
            Console.WriteLine($"Total P&L: ${totalPnL:N0}");
            Console.WriteLine($"Total Loss Amount: ${totalLossAmount:N0}");
            Console.WriteLine($"Worst Single Day: ${results.WorstSingleDayLoss:N0}");
            Console.WriteLine($"Max Consecutive Losses: {results.MaxConsecutiveLosses}");
            Console.WriteLine($"Longest Drawdown: {results.LongestDrawdownDays:N0} days");

            Console.WriteLine($"\n📊 FAILURE CATEGORIES (Top 5):");
            foreach (var failure in results.FailureCategories.Take(5))
            {
                Console.WriteLine($"  {failure.Category}:");
                Console.WriteLine($"    Frequency: {failure.Frequency} times");
                Console.WriteLine($"    Total Loss: ${failure.TotalLoss:N0}");
                Console.WriteLine($"    Avg Loss: ${failure.AverageLoss:F0}");
                Console.WriteLine($"    Max Loss: ${failure.MaxLoss:F0}");
                Console.WriteLine($"    Pattern: {failure.Pattern}");
                Console.WriteLine();
            }

            Console.WriteLine($"\n🌊 MARKET REGIME PERFORMANCE:");
            foreach (var regime in results.MarketRegimePerformance.OrderByDescending(r => r.Value))
            {
                Console.WriteLine($"  {regime.Key}: ${regime.Value:N0}");
            }

            Console.WriteLine($"\n📉 LOSS STREAK ANALYSIS:");
            foreach (var streak in results.LossStreakAnalysis.OrderByDescending(s => s.Key))
            {
                Console.WriteLine($"  {streak.Key} consecutive losses: {streak.Value} occurrences");
            }

            Console.WriteLine($"\n⚠️ WORST DRAWDOWN PERIODS:");
            foreach (var period in results.WorstDrawdownPeriods.Take(5))
            {
                Console.WriteLine($"  {period:yyyy-MM-dd}");
            }
        }

        private class DailyTradingResult
        {
            public List<TradeResult> Trades { get; set; } = new();
        }
    }
}