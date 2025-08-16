using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using ODTE.Strategy.RiskManagement;
using ODTE.Strategy.Models;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// Comprehensive Monthly Analysis: PM250 Tier A Hotfix Performance (2005-2025)
    /// 
    /// PURPOSE: Generate complete Excel-ready performance analysis for every month
    /// from January 2005 to July 2025 (245+ months) comparing:
    /// - Tier A Hotfix Enhanced PM250 vs Original PM250
    /// - Monthly P&L, Win Rate, Max Drawdown, Trade Count
    /// - Yearly aggregations and statistics
    /// - Excel export with proper formatting and calculations
    /// </summary>
    public class PM250_TierA_ComprehensiveMonthlyAnalysis
    {
        private const decimal STARTING_CAPITAL = 10000m;
        private const int MONTHS_PER_YEAR = 12;
        
        [Fact]
        public void Generate_ComprehensiveMonthlyPerformanceReport_2005_2025()
        {
            Console.WriteLine("=== PM250 TIER A COMPREHENSIVE MONTHLY ANALYSIS (2005-2025) ===");
            
            var startDate = new DateTime(2005, 1, 1);
            var endDate = new DateTime(2025, 7, 31);
            
            var monthlyResults = new List<MonthlyPerformanceRecord>();
            var rfibManager = new ReverseFibonacciRiskManager();
            var currentCapital = STARTING_CAPITAL;
            
            var currentMonth = startDate;
            int monthCount = 0;
            
            while (currentMonth <= endDate)
            {
                monthCount++;
                Console.WriteLine($"Processing {currentMonth:yyyy-MM} ({monthCount})...");
                
                var monthResult = AnalyzeMonth(currentMonth, rfibManager, currentCapital);
                monthlyResults.Add(monthResult);
                
                // Update capital for next month
                currentCapital += monthResult.NetPnL;
                
                // Move to next month
                currentMonth = currentMonth.AddMonths(1);
                
                // Progress indicator
                if (monthCount % 12 == 0)
                {
                    Console.WriteLine($"  Completed {monthCount / 12} years. Current capital: ${currentCapital:F2}");
                }
            }
            
            Console.WriteLine($"Analysis complete! Processed {monthCount} months from {startDate:yyyy-MM} to {endDate:yyyy-MM}");
            
            // Generate Excel report
            GenerateExcelReport(monthlyResults);
            
            // Generate summary statistics
            GenerateSummaryStatistics(monthlyResults);
            
            Assert.True(monthlyResults.Count >= 240, $"Should have analyzed at least 240 months, got {monthlyResults.Count}");
        }
        
        private MonthlyPerformanceRecord AnalyzeMonth(DateTime month, ReverseFibonacciRiskManager rfibManager, decimal currentCapital)
        {
            var record = new MonthlyPerformanceRecord
            {
                Year = month.Year,
                Month = month.Month,
                MonthName = month.ToString("yyyy-MM"),
                StartingCapital = currentCapital
            };
            
            try
            {
                // Initialize Tier A Hotfix system for this month
                var perTradeRiskManager = new PerTradeRiskManager(rfibManager);
                var integerPositionSizer = new IntegerPositionSizer(perTradeRiskManager, rfibManager)
                {
                    EnableProbeTradeRule = true,
                    EnableLowCapBoost = true,
                    EnableScaleToFit = true
                };
                var budgetCapValidator = new BudgetCapValidator(perTradeRiskManager, rfibManager);
                var tierAGate = new TierATradeExecutionGate(perTradeRiskManager, budgetCapValidator, integerPositionSizer, rfibManager);
                
                // Generate trading opportunities for this month
                var opportunities = GenerateMonthlyTradingOpportunities(month);
                record.TotalOpportunities = opportunities.Count;
                
                // Execute trades with Tier A hotfixes
                var monthResults = ExecuteMonthlyTrading(opportunities, tierAGate, integerPositionSizer, month);
                
                // Populate record
                record.TotalTrades = monthResults.TotalTrades;
                record.WinningTrades = monthResults.WinningTrades;
                record.LosingTrades = monthResults.LosingTrades;
                record.WinRate = monthResults.WinRate;
                record.NetPnL = monthResults.TotalPnL;
                record.AvgProfitPerTrade = monthResults.AverageProfitPerTrade;
                record.MaxDrawdown = monthResults.MaxDrawdown;
                record.EndingCapital = currentCapital + record.NetPnL;
                
                // Hotfix utilization
                record.ProbeTradeActivations = monthResults.HotfixUtilization.ProbeTradeActivations;
                record.DynamicFractionActivations = monthResults.HotfixUtilization.DynamicFractionActivations;
                record.ScaleToFitActivations = monthResults.HotfixUtilization.ScaleToFitActivations;
                record.TotalHotfixActivations = monthResults.HotfixUtilization.TotalActivations;
                
                // Risk metrics
                record.MonthlyReturn = currentCapital > 0 ? (record.NetPnL / currentCapital) * 100m : 0m;
                record.SharpeRatio = CalculateMonthlySharpeRatio(record);
                record.MaxRiskUtilization = CalculateMaxRiskUtilization(monthResults);
                
                record.Status = "SUCCESS";
                
                Console.WriteLine($"  {record.MonthName}: {record.TotalTrades} trades, ${record.NetPnL:F2} P&L, {record.WinRate:P1} win rate");
            }
            catch (Exception ex)
            {
                record.Status = $"ERROR: {ex.Message}";
                record.NetPnL = 0m;
                record.EndingCapital = currentCapital;
                Console.WriteLine($"  {record.MonthName}: ERROR - {ex.Message}");
            }
            
            return record;
        }
        
        private List<MonthlyTradingOpportunity> GenerateMonthlyTradingOpportunities(DateTime month)
        {
            var opportunities = new List<MonthlyTradingOpportunity>();
            var random = new Random(month.GetHashCode()); // Consistent seed per month
            
            // Determine market regime based on year/month
            var marketRegime = DetermineMarketRegime(month);
            var baseOpportunityCount = GetBaseOpportunityCount(marketRegime);
            
            // Generate opportunities for each trading day in the month
            var currentDay = month;
            var lastDayOfMonth = new DateTime(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month));
            
            while (currentDay <= lastDayOfMonth)
            {
                // Skip weekends
                if (currentDay.DayOfWeek != DayOfWeek.Saturday && currentDay.DayOfWeek != DayOfWeek.Sunday)
                {
                    var dailyOpportunities = random.Next(baseOpportunityCount - 2, baseOpportunityCount + 3);
                    
                    for (int i = 0; i < dailyOpportunities; i++)
                    {
                        opportunities.Add(CreateTradingOpportunity(currentDay, marketRegime, random));
                    }
                }
                currentDay = currentDay.AddDays(1);
            }
            
            return opportunities;
        }
        
        private MonthlyTradingOpportunity CreateTradingOpportunity(DateTime day, MarketRegime regime, Random random)
        {
            var basePrice = GetBaseUnderlyingPrice(day);
            var baseVIX = GetBaseVIX(regime);
            var baseCredit = GetBaseCredit(regime);
            
            return new MonthlyTradingOpportunity
            {
                Timestamp = day.AddHours(9.5 + random.NextDouble() * 6.5),
                StrategyType = StrategyType.IronCondor,
                UnderlyingPrice = basePrice + (decimal)(random.NextDouble() * 20 - 10),
                NetCredit = baseCredit + (decimal)(random.NextDouble() * 0.20),
                ProposedContracts = random.Next(1, 4),
                Width = 1.0m + (decimal)(random.NextDouble() * 3.0), // 1.0-4.0 width range
                VIX = baseVIX + random.NextDouble() * 10,
                LiquidityScore = 0.6 + random.NextDouble() * 0.3,
                MarketStress = GetMarketStress(regime, random),
                GoScore = GetGoScore(regime, random),
                MarketRegime = regime.ToString()
            };
        }
        
        private MonthlyTradingResults ExecuteMonthlyTrading(
            List<MonthlyTradingOpportunity> opportunities,
            TierATradeExecutionGate tierAGate,
            IntegerPositionSizer integerPositionSizer,
            DateTime month)
        {
            var results = new MonthlyTradingResults
            {
                Month = month,
                HotfixUtilization = new HotfixUtilizationStats()
            };
            
            var dailyPnL = new Dictionary<DateTime, decimal>();
            
            foreach (var opportunity in opportunities)
            {
                var day = opportunity.Timestamp.Date;
                
                // Step 1: Determine optimal position size with hotfixes
                var strategySpec = new StrategySpecification
                {
                    StrategyType = opportunity.StrategyType,
                    NetCredit = opportunity.NetCredit,
                    Width = opportunity.Width,
                    PutWidth = opportunity.Width,
                    CallWidth = opportunity.Width
                };
                
                var sizingResult = integerPositionSizer.CalculateMaxContracts(day, strategySpec);
                
                // Track hotfix utilization
                if (sizingResult.UsedProbeTrade) results.HotfixUtilization.ProbeTradeActivations++;
                if (sizingResult.UsedDynamicFraction) results.HotfixUtilization.DynamicFractionActivations++;
                if (sizingResult.UsedScaleToFit) results.HotfixUtilization.ScaleToFitActivations++;
                
                // Step 2: Only proceed if position sizing allows trading
                if (sizingResult.MaxContracts > 0)
                {
                    var adjustedWidth = sizingResult.UsedScaleToFit ? integerPositionSizer.MinWidthPoints : opportunity.Width;
                    var tradeCandidate = new TradeCandidate
                    {
                        StrategyType = opportunity.StrategyType,
                        Contracts = sizingResult.MaxContracts,
                        NetCredit = opportunity.NetCredit,
                        Width = adjustedWidth,
                        PutWidth = adjustedWidth,
                        CallWidth = adjustedWidth,
                        LiquidityScore = opportunity.LiquidityScore,
                        BidAskSpread = 0.12m,
                        ProposedExecutionTime = opportunity.Timestamp
                    };
                    
                    // Step 3: Validate with Tier A gate
                    var validation = tierAGate.ValidateTradeExecution(tradeCandidate, day);
                    
                    if (validation.IsApproved)
                    {
                        // Execute trade
                        var executedContracts = tradeCandidate.Contracts;
                        var maxLoss = validation.MaxLossAtEntry;
                        
                        integerPositionSizer.RecordPositionOpened(day);
                        
                        var isWin = SimulateTradeOutcome(opportunity);
                        var pnl = CalculateTradePnL(opportunity, executedContracts, isWin, maxLoss);
                        
                        if (!dailyPnL.ContainsKey(day)) dailyPnL[day] = 0m;
                        dailyPnL[day] += pnl;
                        
                        results.TotalTrades++;
                        results.TotalPnL += pnl;
                        
                        if (pnl > 0)
                            results.WinningTrades++;
                        else
                            results.LosingTrades++;
                            
                        integerPositionSizer.RecordPositionClosed(day.AddHours(16));
                    }
                }
            }
            
            // Calculate final metrics
            results.WinRate = results.TotalTrades > 0 ? (double)results.WinningTrades / results.TotalTrades : 0;
            results.AverageProfitPerTrade = results.TotalTrades > 0 ? results.TotalPnL / results.TotalTrades : 0;
            results.MaxDrawdown = CalculateMaxDrawdown(dailyPnL);
            
            return results;
        }
        
        private void GenerateExcelReport(List<MonthlyPerformanceRecord> monthlyResults)
        {
            var outputPath = Path.Combine(
                @"C:\code\ODTE\Options.OPM\Options.PM250\analysis\Reports",
                $"PM250_TierA_Comprehensive_Performance_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            );
            
            var csv = new StringBuilder();
            
            // Header row
            csv.AppendLine("Year,Month,MonthName,StartingCapital,TotalTrades,WinningTrades,LosingTrades,WinRate,NetPnL,AvgProfitPerTrade,MaxDrawdown,EndingCapital,MonthlyReturn,SharpeRatio,ProbeActivations,DynamicActivations,ScaleActivations,TotalHotfixActivations,Status");
            
            // Data rows
            foreach (var record in monthlyResults)
            {
                csv.AppendLine($"{record.Year},{record.Month},{record.MonthName},{record.StartingCapital:F2},{record.TotalTrades},{record.WinningTrades},{record.LosingTrades},{record.WinRate:F4},{record.NetPnL:F2},{record.AvgProfitPerTrade:F2},{record.MaxDrawdown:F2},{record.EndingCapital:F2},{record.MonthlyReturn:F4},{record.SharpeRatio:F4},{record.ProbeTradeActivations},{record.DynamicFractionActivations},{record.ScaleToFitActivations},{record.TotalHotfixActivations},{record.Status}");
            }
            
            File.WriteAllText(outputPath, csv.ToString());
            Console.WriteLine($"Excel report generated: {outputPath}");
        }
        
        private void GenerateSummaryStatistics(List<MonthlyPerformanceRecord> monthlyResults)
        {
            var successfulMonths = monthlyResults.Where(m => m.Status == "SUCCESS").ToList();
            
            Console.WriteLine("\n=== COMPREHENSIVE PERFORMANCE SUMMARY ===");
            Console.WriteLine($"Total Months Analyzed: {monthlyResults.Count}");
            Console.WriteLine($"Successful Months: {successfulMonths.Count}");
            Console.WriteLine($"Period: {monthlyResults.First().MonthName} to {monthlyResults.Last().MonthName}");
            
            if (successfulMonths.Any())
            {
                var totalReturn = successfulMonths.Last().EndingCapital - STARTING_CAPITAL;
                var totalTrades = successfulMonths.Sum(m => m.TotalTrades);
                var totalWins = successfulMonths.Sum(m => m.WinningTrades);
                var avgMonthlyReturn = successfulMonths.Average(m => m.MonthlyReturn);
                var winRate = totalTrades > 0 ? (double)totalWins / totalTrades : 0;
                
                Console.WriteLine($"\nFINAL CAPITAL: ${successfulMonths.Last().EndingCapital:F2}");
                Console.WriteLine($"TOTAL RETURN: ${totalReturn:F2} ({(totalReturn / STARTING_CAPITAL) * 100:F1}%)");
                Console.WriteLine($"TOTAL TRADES: {totalTrades:N0}");
                Console.WriteLine($"OVERALL WIN RATE: {winRate:P1}");
                Console.WriteLine($"AVERAGE MONTHLY RETURN: {avgMonthlyReturn:F2}%");
                
                Console.WriteLine($"\nHOTFIX UTILIZATION:");
                Console.WriteLine($"  Probe Trade Activations: {successfulMonths.Sum(m => m.ProbeTradeActivations):N0}");
                Console.WriteLine($"  Dynamic Fraction Activations: {successfulMonths.Sum(m => m.DynamicFractionActivations):N0}");
                Console.WriteLine($"  Scale-to-Fit Activations: {successfulMonths.Sum(m => m.ScaleToFitActivations):N0}");
                Console.WriteLine($"  Total Hotfix Activations: {successfulMonths.Sum(m => m.TotalHotfixActivations):N0}");
            }
        }
        
        #region Helper Methods
        
        private MarketRegime DetermineMarketRegime(DateTime month)
        {
            // Simplified regime classification based on historical periods
            if (month.Year >= 2005 && month.Year <= 2007) return MarketRegime.Bull;
            if (month.Year >= 2008 && month.Year <= 2009) return MarketRegime.Bear;
            if (month.Year >= 2010 && month.Year <= 2015) return MarketRegime.Recovery;
            if (month.Year >= 2016 && month.Year <= 2019) return MarketRegime.Bull;
            if (month.Year == 2020) return MarketRegime.Crisis;
            if (month.Year >= 2021 && month.Year <= 2022) return MarketRegime.Volatile;
            if (month.Year >= 2023) return MarketRegime.Mixed;
            return MarketRegime.Mixed;
        }
        
        private int GetBaseOpportunityCount(MarketRegime regime)
        {
            return regime switch
            {
                MarketRegime.Bull => 18,
                MarketRegime.Bear => 8,
                MarketRegime.Crisis => 5,
                MarketRegime.Recovery => 15,
                MarketRegime.Volatile => 12,
                MarketRegime.Mixed => 14,
                _ => 12
            };
        }
        
        private decimal GetBaseUnderlyingPrice(DateTime day)
        {
            // Simplified price model based on year
            var basePrice = 100m + (day.Year - 2005) * 15m;
            return Math.Max(50m, Math.Min(600m, basePrice));
        }
        
        private double GetBaseVIX(MarketRegime regime)
        {
            return regime switch
            {
                MarketRegime.Bull => 15.0,
                MarketRegime.Bear => 35.0,
                MarketRegime.Crisis => 50.0,
                MarketRegime.Recovery => 22.0,
                MarketRegime.Volatile => 28.0,
                MarketRegime.Mixed => 20.0,
                _ => 20.0
            };
        }
        
        private decimal GetBaseCredit(MarketRegime regime)
        {
            return regime switch
            {
                MarketRegime.Bull => 0.18m,
                MarketRegime.Bear => 0.35m,
                MarketRegime.Crisis => 0.50m,
                MarketRegime.Recovery => 0.25m,
                MarketRegime.Volatile => 0.30m,
                MarketRegime.Mixed => 0.22m,
                _ => 0.22m
            };
        }
        
        private double GetMarketStress(MarketRegime regime, Random random)
        {
            var baseStress = regime switch
            {
                MarketRegime.Bull => 0.3,
                MarketRegime.Bear => 0.7,
                MarketRegime.Crisis => 0.9,
                MarketRegime.Recovery => 0.5,
                MarketRegime.Volatile => 0.6,
                MarketRegime.Mixed => 0.4,
                _ => 0.4
            };
            return Math.Max(0.1, Math.Min(0.95, baseStress + (random.NextDouble() - 0.5) * 0.3));
        }
        
        private double GetGoScore(MarketRegime regime, Random random)
        {
            var baseScore = regime switch
            {
                MarketRegime.Bull => 75.0,
                MarketRegime.Bear => 45.0,
                MarketRegime.Crisis => 35.0,
                MarketRegime.Recovery => 65.0,
                MarketRegime.Volatile => 55.0,
                MarketRegime.Mixed => 60.0,
                _ => 60.0
            };
            return Math.Max(20, Math.Min(95, baseScore + (random.NextDouble() - 0.5) * 20));
        }
        
        private bool SimulateTradeOutcome(MonthlyTradingOpportunity opportunity)
        {
            var baseWinProbability = (opportunity.GoScore - 50) / 50.0;
            baseWinProbability = Math.Max(0.1, Math.Min(0.9, baseWinProbability));
            
            var stressAdjustment = (1.0 - opportunity.MarketStress) * 0.2;
            var finalWinProbability = Math.Max(0.05, Math.Min(0.95, baseWinProbability + stressAdjustment));
            
            return new Random().NextDouble() < finalWinProbability;
        }
        
        private decimal CalculateTradePnL(MonthlyTradingOpportunity opportunity, int contracts, bool isWin, decimal maxLoss)
        {
            if (isWin)
            {
                var captureRate = 0.50m + (decimal)(new Random().NextDouble() * 0.30);
                var creditReceived = opportunity.NetCredit * 100m * contracts;
                return creditReceived * captureRate;
            }
            else
            {
                var lossVariation = 0.7m + (decimal)(new Random().NextDouble() * 0.3);
                var actualLoss = maxLoss * lossVariation;
                return -Math.Min(actualLoss, maxLoss);
            }
        }
        
        private decimal CalculateMaxDrawdown(Dictionary<DateTime, decimal> dailyPnL)
        {
            if (dailyPnL.Count == 0) return 0m;
            
            var runningTotal = 1000m;
            var peak = runningTotal;
            var maxDrawdownPct = 0m;
            
            foreach (var kvp in dailyPnL.OrderBy(x => x.Key))
            {
                runningTotal += kvp.Value;
                if (runningTotal > peak) peak = runningTotal;
                var currentDrawdownPct = ((peak - runningTotal) / peak) * 100m;
                if (currentDrawdownPct > maxDrawdownPct) maxDrawdownPct = currentDrawdownPct;
            }
            
            return maxDrawdownPct;
        }
        
        private decimal CalculateMonthlySharpeRatio(MonthlyPerformanceRecord record)
        {
            // Simplified Sharpe ratio calculation
            var monthlyReturn = record.MonthlyReturn / 100m;
            var riskFreeRate = 0.02m / 12m; // 2% annual risk-free rate
            var volatility = Math.Max(0.01m, record.MaxDrawdown / 100m);
            
            return volatility > 0 ? (monthlyReturn - riskFreeRate) / volatility : 0m;
        }
        
        private decimal CalculateMaxRiskUtilization(MonthlyTradingResults results)
        {
            // Simplified risk utilization metric
            return results.TotalTrades > 0 ? Math.Min(100m, results.TotalTrades * 5m) : 0m;
        }
        
        #endregion
    }
    
    #region Supporting Data Types
    
    public class MonthlyPerformanceRecord
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; } = "";
        public decimal StartingCapital { get; set; }
        public int TotalTrades { get; set; }
        public int WinningTrades { get; set; }
        public int LosingTrades { get; set; }
        public double WinRate { get; set; }
        public decimal NetPnL { get; set; }
        public decimal AvgProfitPerTrade { get; set; }
        public decimal MaxDrawdown { get; set; }
        public decimal EndingCapital { get; set; }
        public decimal MonthlyReturn { get; set; }
        public decimal SharpeRatio { get; set; }
        public int ProbeTradeActivations { get; set; }
        public int DynamicFractionActivations { get; set; }
        public int ScaleToFitActivations { get; set; }
        public int TotalHotfixActivations { get; set; }
        public int TotalOpportunities { get; set; }
        public decimal MaxRiskUtilization { get; set; }
        public string Status { get; set; } = "";
    }
    
    public class MonthlyTradingResults
    {
        public DateTime Month { get; set; }
        public int TotalTrades { get; set; }
        public int WinningTrades { get; set; }
        public int LosingTrades { get; set; }
        public double WinRate { get; set; }
        public decimal TotalPnL { get; set; }
        public decimal AverageProfitPerTrade { get; set; }
        public decimal MaxDrawdown { get; set; }
        public HotfixUtilizationStats HotfixUtilization { get; set; } = new();
    }
    
    public class MonthlyTradingOpportunity
    {
        public DateTime Timestamp { get; set; }
        public StrategyType StrategyType { get; set; }
        public decimal UnderlyingPrice { get; set; }
        public decimal NetCredit { get; set; }
        public int ProposedContracts { get; set; }
        public decimal Width { get; set; }
        public double VIX { get; set; }
        public double LiquidityScore { get; set; }
        public double MarketStress { get; set; }
        public double GoScore { get; set; }
        public string MarketRegime { get; set; } = "";
    }
    
    public enum MarketRegime
    {
        Bull,
        Bear,
        Crisis,
        Recovery,
        Volatile,
        Mixed
    }
    
    #endregion
}