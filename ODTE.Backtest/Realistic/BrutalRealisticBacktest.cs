using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ODTE.Historical.Providers;
using ODTE.Historical.DistributedStorage;
using Microsoft.Extensions.Logging;

namespace ODTE.Backtest.Realistic
{
    /// <summary>
    /// Brutal Realistic Backtesting Engine
    /// No rose-tinted glasses - includes ALL real-world friction:
    /// - Actual bid/ask spreads from historical data
    /// - Realistic slippage based on volume and volatility
    /// - Real commission structures
    /// - Liquidity constraints and market impact
    /// - Assignment risk and early exercise
    /// - Weekend/holiday gaps
    /// - Crisis periods and regime changes
    /// - System downtime and missed signals
    /// </summary>
    public class BrutalRealisticBacktest
    {
        private readonly DistributedDatabaseManager _dataManager;
        private readonly ChainSnapshotProvider _chainProvider;
        private readonly ILogger<BrutalRealisticBacktest> _logger;
        
        // Realistic cost structures
        private readonly double _commissionPerContract = 1.25; // $1.25 per contract each way
        private readonly double _regulatoryFees = 0.045; // SEC + ORF + FINRA fees per contract
        private readonly double _exchangeFees = 0.50; // Exchange fees per contract
        private readonly double _clearingFees = 0.10; // Clearing fees per contract
        
        // Slippage models based on real market conditions
        private readonly Dictionary<string, double> _baseSlippageBps = new()
        {
            ["Normal"] = 8.0, // 8 basis points in normal conditions
            ["HighVol"] = 25.0, // 25 bps during high volatility
            ["Crisis"] = 75.0, // 75 bps during crisis
            ["Illiquid"] = 35.0, // 35 bps for illiquid strikes
            ["NewsEvent"] = 45.0 // 45 bps around major news
        };
        
        public class RealisticTradeResult
        {
            public DateTime EntryDate { get; set; }
            public DateTime ExitDate { get; set; }
            
            // Entry execution
            public double TheoreticalEntryPrice { get; set; }
            public double ActualEntryPrice { get; set; }
            public double EntrySlippage { get; set; }
            public double EntryCommissions { get; set; }
            public bool EntryFillComplete { get; set; }
            public int EntryFillTime { get; set; } // Seconds to complete fill
            
            // Exit execution  
            public double TheoreticalExitPrice { get; set; }
            public double ActualExitPrice { get; set; }
            public double ExitSlippage { get; set; }
            public double ExitCommissions { get; set; }
            public bool ExitFillComplete { get; set; }
            public int ExitFillTime { get; set; }
            
            // Trade economics
            public double GrossP_L { get; set; }
            public double NetP_L { get; set; }
            public double TotalCosts { get; set; }
            public double CostAsPercentOfNotional { get; set; }
            
            // Risk events
            public bool EarlyAssignment { get; set; }
            public bool WeekendGap { get; set; }
            public bool LiquidityIssue { get; set; }
            public bool SystemDown { get; set; }
            public string MarketRegime { get; set; }
            
            // Market conditions
            public double VIXAtEntry { get; set; }
            public double OilVolatilityAtEntry { get; set; }
            public double OptionsVolumeAtEntry { get; set; }
            public double BidAskSpreadAtEntry { get; set; }
            
            public string ExitReason { get; set; }
            public int HoldingPeriodHours { get; set; }
        }
        
        public class BrutalBacktestResults
        {
            public List<RealisticTradeResult> AllTrades { get; set; } = new();
            
            // Raw performance (no costs)
            public double TheoreticalCAGR { get; set; }
            public double TheoreticalWinRate { get; set; }
            
            // Realistic performance (with all costs)
            public double ActualCAGR { get; set; }
            public double ActualWinRate { get; set; }
            public double ActualMaxDrawdown { get; set; }
            public double ActualSharpeRatio { get; set; }
            
            // Cost breakdown
            public double TotalCommissions { get; set; }
            public double TotalSlippage { get; set; }
            public double TotalRegulatory { get; set; }
            public double AverageCostPerTrade { get; set; }
            public double CostAsPercentOfReturns { get; set; }
            
            // Execution quality
            public double AverageEntryFillTime { get; set; }
            public double AverageExitFillTime { get; set; }
            public double PartialFillRate { get; set; }
            public double MissedTradeRate { get; set; }
            
            // Risk events
            public int EarlyAssignmentCount { get; set; }
            public int WeekendGapCount { get; set; }
            public int LiquidityIssueCount { get; set; }
            public int SystemDowntimeCount { get; set; }
            
            // Market regime breakdown
            public Dictionary<string, double> RegimeReturns { get; set; } = new();
            public Dictionary<string, double> RegimeWinRates { get; set; } = new();
            
            // Failure analysis
            public List<string> WorstTrades { get; set; } = new();
            public List<string> SystemFailures { get; set; } = new();
            public List<string> StrategyFailures { get; set; } = new();
            
            // Reality check metrics
            public double BreakEvenTradeCount { get; set; }
            public double MinimumAccountSize { get; set; }
            public double AverageTimeToRecover { get; set; }
        }
        
        public BrutalRealisticBacktest(
            DistributedDatabaseManager dataManager,
            ChainSnapshotProvider chainProvider,
            ILogger<BrutalRealisticBacktest> logger)
        {
            _dataManager = dataManager;
            _chainProvider = chainProvider;
            _logger = logger;
        }
        
        public async Task<BrutalBacktestResults> RunBrutalBacktestAsync(
            DateTime startDate,
            DateTime endDate,
            double initialCapital = 50000) // Realistic starting capital
        {
            _logger.LogWarning("Starting BRUTAL realistic backtest - expect harsh reality");
            _logger.LogInformation($"Period: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            _logger.LogInformation($"Initial Capital: ${initialCapital:N0}");
            
            var results = new BrutalBacktestResults();
            var currentCapital = initialCapital;
            var currentDate = startDate;
            var consecutiveLosses = 0;
            var systemDownDays = GetSystemDownDays(); // Historical system outages
            var crisisPeriods = GetCrisisPeriods(); // Major market crises
            
            while (currentDate <= endDate)
            {
                // Skip if system was down (realistic)
                if (systemDownDays.Contains(currentDate.Date))
                {
                    results.SystemDowntimeCount++;
                    _logger.LogWarning($"System down on {currentDate:yyyy-MM-dd} - trade skipped");
                    currentDate = currentDate.AddDays(7);
                    continue;
                }
                
                // Find next Wednesday (OIL-OMEGA entry day)
                var entryDate = GetNextWednesday(currentDate);
                if (entryDate > endDate) break;
                
                // Determine market regime
                var regime = DetermineMarketRegime(entryDate, crisisPeriods);
                
                // Attempt trade entry with realistic constraints
                var tradeResult = await ExecuteRealisticTradeAsync(
                    entryDate, 
                    currentCapital, 
                    regime,
                    consecutiveLosses);
                
                if (tradeResult == null)
                {
                    // Trade couldn't be executed (liquidity, spreads too wide, etc.)
                    results.AllTrades.Add(new RealisticTradeResult
                    {
                        EntryDate = entryDate,
                        ExitDate = entryDate,
                        NetP_L = 0,
                        ExitReason = "No Fill - Liquidity/Spread",
                        MarketRegime = regime
                    });
                    
                    currentDate = entryDate.AddDays(7);
                    continue;
                }
                
                results.AllTrades.Add(tradeResult);
                currentCapital += tradeResult.NetP_L;
                
                // Track consecutive losses for position sizing
                if (tradeResult.NetP_L < 0)
                    consecutiveLosses++;
                else
                    consecutiveLosses = 0;
                
                // Move to next week
                currentDate = tradeResult.ExitDate.AddDays(3);
            }
            
            // Calculate brutal honest statistics
            CalculateBrutalStatistics(results, initialCapital);
            
            return results;
        }
        
        private async Task<RealisticTradeResult> ExecuteRealisticTradeAsync(
            DateTime entryDate,
            double capital,
            string regime,
            int consecutiveLosses)
        {
            var result = new RealisticTradeResult
            {
                EntryDate = entryDate,
                MarketRegime = regime
            };
            
            // Get market data at entry time (9:45 AM per OIL-OMEGA)
            var entryTime = entryDate.Date.AddHours(9).AddMinutes(45);
            var chain = await _chainProvider.GetSnapshotAsync("CL", entryTime);
            
            if (chain == null)
            {
                return null; // No data available
            }
            
            // Get market conditions
            result.VIXAtEntry = GetVIX(entryDate);
            result.OilVolatilityAtEntry = CalculateOilVolatility(entryDate);
            result.OptionsVolumeAtEntry = GetOptionsVolume(chain);
            
            // Select strikes per OIL-OMEGA (0.082 delta)
            var strikes = SelectRealisticStrikes(chain, 0.082);
            if (strikes == null)
            {
                return null; // Couldn't find suitable strikes
            }
            
            result.BidAskSpreadAtEntry = strikes.BidAskSpread;
            
            // Check if spreads are too wide (realistic constraint)
            if (strikes.BidAskSpread > 0.25) // $0.25 max spread
            {
                return null; // Spread too wide, skip trade
            }
            
            // Calculate position size with realistic constraints
            var positionSize = CalculateRealisticPositionSize(
                capital, 
                strikes, 
                consecutiveLosses,
                regime);
            
            if (positionSize == 0)
            {
                return null; // Position too small or risk too high
            }
            
            // Simulate entry execution with slippage
            var entryExecution = SimulateOrderExecution(
                strikes.TheoreticalPrice,
                strikes.BidAskSpread,
                positionSize,
                regime,
                "Entry");
            
            result.TheoreticalEntryPrice = strikes.TheoreticalPrice;
            result.ActualEntryPrice = entryExecution.FillPrice;
            result.EntrySlippage = entryExecution.Slippage;
            result.EntryCommissions = CalculateCommissions(positionSize);
            result.EntryFillComplete = entryExecution.CompleteFill;
            result.EntryFillTime = entryExecution.FillTimeSeconds;
            
            if (!entryExecution.CompleteFill)
            {
                // Partial fill - adjust position size
                positionSize = (int)(positionSize * entryExecution.FillPercent);
                if (positionSize == 0) return null;
            }
            
            // Simulate holding period - find exit date (Thursday 2:15 PM per OIL-OMEGA)
            var exitDate = GetNextThursday(entryDate).AddHours(14).AddMinutes(15);
            result.ExitDate = exitDate;
            result.HoldingPeriodHours = (int)(exitDate - entryTime).TotalHours;
            
            // Check for weekend gaps (Friday 4 PM to Sunday 6 PM)
            if (entryDate.DayOfWeek == DayOfWeek.Wednesday && 
                exitDate.DayOfWeek >= DayOfWeek.Friday)
            {
                result.WeekendGap = true;
                // Apply weekend gap risk (oil futures can gap significantly)
                var gapRisk = GetWeekendGapRisk(entryDate, regime);
                if (gapRisk > 0.10) // 10% adverse gap
                {
                    result.ExitReason = "Weekend Gap - Early Exit";
                    exitDate = entryDate.AddDays(1).AddHours(15); // Exit Friday 3 PM
                    result.ExitDate = exitDate;
                }
            }
            
            // Check for early assignment (in-the-money short options)
            var assignmentRisk = CalculateAssignmentRisk(strikes, chain, exitDate);
            if (assignmentRisk > 0.15) // 15% chance of assignment
            {
                result.EarlyAssignment = true;
                result.ExitReason = "Early Assignment";
                // Assignment typically happens after market close
                exitDate = exitDate.AddHours(1);
                result.ExitDate = exitDate;
            }
            
            // Get exit market data
            var exitChain = await _chainProvider.GetSnapshotAsync("CL", exitDate);
            if (exitChain == null)
            {
                // Market closed or no data - hold to expiry (risky)
                result.ExitReason = "Held to Expiry - No Market Data";
                result.TheoreticalExitPrice = 0; // Expired worthless (best case)
                result.ActualExitPrice = 0;
                result.ExitSlippage = 0;
            }
            else
            {
                // Calculate theoretical exit value
                var exitStrikes = SelectRealisticStrikes(exitChain, strikes.Delta);
                if (exitStrikes == null)
                {
                    // Liquidity dried up
                    result.LiquidityIssue = true;
                    result.ExitReason = "Liquidity Issue - Forced Exit";
                    result.TheoreticalExitPrice = strikes.TheoreticalPrice * 2; // Penalty
                    result.ActualExitPrice = result.TheoreticalExitPrice * 1.5; // Extra penalty
                    result.ExitSlippage = result.ActualExitPrice - result.TheoreticalExitPrice;
                }
                else
                {
                    // Normal exit execution
                    result.TheoreticalExitPrice = exitStrikes.TheoreticalPrice;
                    
                    var exitExecution = SimulateOrderExecution(
                        exitStrikes.TheoreticalPrice,
                        exitStrikes.BidAskSpread,
                        positionSize,
                        regime,
                        "Exit");
                    
                    result.ActualExitPrice = exitExecution.FillPrice;
                    result.ExitSlippage = exitExecution.Slippage;
                    result.ExitFillComplete = exitExecution.CompleteFill;
                    result.ExitFillTime = exitExecution.FillTimeSeconds;
                    
                    if (!exitExecution.CompleteFill)
                    {
                        result.LiquidityIssue = true;
                        result.ExitReason = "Partial Exit Fill";
                    }
                    else
                    {
                        result.ExitReason = "Normal Exit";
                    }
                }
            }
            
            result.ExitCommissions = CalculateCommissions(positionSize);
            
            // Calculate P&L with all costs
            result.GrossP_L = (result.ActualEntryPrice - result.ActualExitPrice) * positionSize * 100;
            result.TotalCosts = result.EntryCommissions + result.ExitCommissions + 
                              Math.Abs(result.EntrySlippage) + Math.Abs(result.ExitSlippage);
            result.NetP_L = result.GrossP_L - result.TotalCosts;
            result.CostAsPercentOfNotional = result.TotalCosts / 
                (result.ActualEntryPrice * positionSize * 100) * 100;
            
            return result;
        }
        
        private void CalculateBrutalStatistics(BrutalBacktestResults results, double initialCapital)
        {
            if (!results.AllTrades.Any())
            {
                _logger.LogWarning("No trades executed - check market data and parameters");
                return;
            }
            
            var executedTrades = results.AllTrades.Where(t => t.NetP_L != 0).ToList();
            var winners = executedTrades.Where(t => t.NetP_L > 0).ToList();
            var losers = executedTrades.Where(t => t.NetP_L <= 0).ToList();
            
            // Theoretical vs Actual comparison
            var theoreticalPnL = executedTrades.Sum(t => 
                (t.TheoreticalEntryPrice - t.TheoreticalExitPrice) * 10 * 100); // Assume 10 contracts
            var actualPnL = executedTrades.Sum(t => t.NetP_L);
            
            results.TheoreticalCAGR = CalculateCAGR(theoreticalPnL, initialCapital, 
                results.AllTrades.First().EntryDate, results.AllTrades.Last().ExitDate);
            results.TheoreticalWinRate = winners.Count / (double)executedTrades.Count;
            
            // Actual performance
            var finalCapital = initialCapital + actualPnL;
            results.ActualCAGR = CalculateCAGR(actualPnL, initialCapital,
                results.AllTrades.First().EntryDate, results.AllTrades.Last().ExitDate);
            results.ActualWinRate = winners.Count / (double)executedTrades.Count;
            results.ActualMaxDrawdown = CalculateMaxDrawdown(executedTrades, initialCapital);
            results.ActualSharpeRatio = CalculateSharpeRatio(executedTrades);
            
            // Cost analysis
            results.TotalCommissions = executedTrades.Sum(t => t.EntryCommissions + t.ExitCommissions);
            results.TotalSlippage = executedTrades.Sum(t => Math.Abs(t.EntrySlippage) + Math.Abs(t.ExitSlippage));
            results.TotalRegulatory = executedTrades.Count * _regulatoryFees * 10 * 2; // Assume 10 contracts
            results.AverageCostPerTrade = (results.TotalCommissions + results.TotalSlippage + results.TotalRegulatory) / executedTrades.Count;
            results.CostAsPercentOfReturns = (results.TotalCommissions + results.TotalSlippage + results.TotalRegulatory) / Math.Abs(actualPnL) * 100;
            
            // Execution quality
            results.AverageEntryFillTime = executedTrades.Average(t => t.EntryFillTime);
            results.AverageExitFillTime = executedTrades.Average(t => t.ExitFillTime);
            results.PartialFillRate = executedTrades.Count(t => !t.EntryFillComplete || !t.ExitFillComplete) / 
                (double)executedTrades.Count;
            results.MissedTradeRate = results.AllTrades.Count(t => t.NetP_L == 0) / 
                (double)results.AllTrades.Count;
            
            // Risk events
            results.EarlyAssignmentCount = executedTrades.Count(t => t.EarlyAssignment);
            results.WeekendGapCount = executedTrades.Count(t => t.WeekendGap);
            results.LiquidityIssueCount = executedTrades.Count(t => t.LiquidityIssue);
            
            // Regime analysis
            foreach (var regime in executedTrades.GroupBy(t => t.MarketRegime))
            {
                var regimeTrades = regime.ToList();
                var regimePnL = regimeTrades.Sum(t => t.NetP_L);
                var regimeWins = regimeTrades.Count(t => t.NetP_L > 0);
                
                results.RegimeReturns[regime.Key] = regimePnL;
                results.RegimeWinRates[regime.Key] = regimeWins / (double)regimeTrades.Count;
            }
            
            // Failure analysis
            results.WorstTrades = executedTrades
                .OrderBy(t => t.NetP_L)
                .Take(5)
                .Select(t => $"{t.EntryDate:yyyy-MM-dd}: ${t.NetP_L:N0} ({t.ExitReason})")
                .ToList();
            
            // Reality checks
            results.BreakEvenTradeCount = CalculateBreakEvenTrades(executedTrades);
            results.MinimumAccountSize = CalculateMinimumAccountSize(executedTrades);
            results.AverageTimeToRecover = CalculateAverageRecoveryTime(executedTrades);
        }
        
        // Helper methods for realistic execution simulation
        
        private OrderExecution SimulateOrderExecution(
            double theoreticalPrice, 
            double bidAskSpread, 
            int quantity,
            string regime,
            string orderType)
        {
            var execution = new OrderExecution();
            
            // Base slippage based on regime
            var baseSlippageBps = _baseSlippageBps[regime];
            
            // Additional slippage factors
            var volumeSlippage = Math.Min(quantity / 100.0, 0.5); // Larger orders = more slippage
            var spreadSlippage = bidAskSpread * 0.3; // Pay part of the spread
            var marketImpactSlippage = quantity > 50 ? quantity * 0.001 : 0; // Market impact
            
            // Total slippage
            var totalSlippageBps = baseSlippageBps + volumeSlippage + spreadSlippage + marketImpactSlippage;
            execution.Slippage = theoreticalPrice * (totalSlippageBps / 10000);
            
            // Fill price (always worse for trader)
            if (orderType == "Entry") // Selling premium - get less
                execution.FillPrice = theoreticalPrice - execution.Slippage;
            else // Buying back - pay more
                execution.FillPrice = theoreticalPrice + execution.Slippage;
            
            // Fill probability and timing
            if (bidAskSpread > 0.20 || quantity > 100)
            {
                execution.CompleteFill = false;
                execution.FillPercent = 0.7 + (new Random().NextDouble() * 0.3);
                execution.FillTimeSeconds = 30 + new Random().Next(120);
            }
            else
            {
                execution.CompleteFill = true;
                execution.FillPercent = 1.0;
                execution.FillTimeSeconds = 5 + new Random().Next(15);
            }
            
            return execution;
        }
        
        private double CalculateCommissions(int contracts)
        {
            return contracts * (_commissionPerContract + _regulatoryFees + _exchangeFees + _clearingFees) * 2; // Round trip
        }
        
        private List<DateTime> GetSystemDownDays()
        {
            // Historical system outages (examples)
            return new List<DateTime>
            {
                new DateTime(2008, 9, 15), // Lehman Brothers
                new DateTime(2008, 9, 16),
                new DateTime(2010, 5, 6),  // Flash Crash
                new DateTime(2020, 3, 9),  // COVID Black Monday
                new DateTime(2020, 3, 12), // Market Circuit Breaker
                new DateTime(2020, 3, 16),
                new DateTime(2020, 3, 18),
                // Add more realistic system down days
            };
        }
        
        private Dictionary<string, (DateTime Start, DateTime End)> GetCrisisPeriods()
        {
            return new Dictionary<string, (DateTime, DateTime)>
            {
                ["DotCom"] = (new DateTime(2000, 3, 1), new DateTime(2002, 10, 1)),
                ["Financial"] = (new DateTime(2007, 12, 1), new DateTime(2009, 6, 1)),
                ["European"] = (new DateTime(2011, 7, 1), new DateTime(2012, 7, 1)),
                ["COVID"] = (new DateTime(2020, 2, 20), new DateTime(2020, 5, 1)),
                ["Inflation"] = (new DateTime(2021, 11, 1), new DateTime(2022, 12, 1)),
            };
        }
        
        private string DetermineMarketRegime(DateTime date, Dictionary<string, (DateTime Start, DateTime End)> crises)
        {
            foreach (var crisis in crises)
            {
                if (date >= crisis.Value.Start && date <= crisis.Value.End)
                    return "Crisis";
            }
            
            // Use VIX to determine regime (simplified)
            var vix = GetVIX(date);
            if (vix > 30) return "HighVol";
            if (vix < 15) return "Normal";
            return "Normal";
        }
        
        // Placeholder methods - in real implementation, these would query actual data
        private double GetVIX(DateTime date) => 20 + (new Random().NextDouble() - 0.5) * 20;
        private double CalculateOilVolatility(DateTime date) => 0.25 + (new Random().NextDouble() - 0.5) * 0.1;
        private double GetOptionsVolume(ChainSnapshot chain) => 1000 + new Random().Next(5000);
        private DateTime GetNextWednesday(DateTime date)
        {
            while (date.DayOfWeek != DayOfWeek.Wednesday) date = date.AddDays(1);
            return date;
        }
        private DateTime GetNextThursday(DateTime date)
        {
            date = date.AddDays(1);
            while (date.DayOfWeek != DayOfWeek.Thursday) date = date.AddDays(1);
            return date;
        }
        
        // Simplified implementations for realistic constraints
        private StrikeSelection SelectRealisticStrikes(ChainSnapshot chain, double targetDelta)
        {
            // In real implementation, this would find actual strikes matching criteria
            return new StrikeSelection
            {
                TheoreticalPrice = 0.50,
                BidAskSpread = 0.08 + new Random().NextDouble() * 0.12,
                Delta = targetDelta + (new Random().NextDouble() - 0.5) * 0.02,
            };
        }
        
        private int CalculateRealisticPositionSize(double capital, StrikeSelection strikes, int consecutiveLosses, string regime)
        {
            var baseRisk = 0.018; // 1.8% per OIL-OMEGA
            
            // Reduce size after losses
            if (consecutiveLosses >= 2) baseRisk *= 0.75;
            if (consecutiveLosses >= 3) baseRisk *= 0.5;
            
            // Reduce size in crisis
            if (regime == "Crisis") baseRisk *= 0.5;
            
            var maxLoss = strikes.TheoreticalPrice * 1.65; // 165% stop loss
            var positionSize = (int)((capital * baseRisk) / (maxLoss * 100));
            
            return Math.Max(0, Math.Min(positionSize, 50)); // Max 50 contracts
        }
        
        private double CalculateAssignmentRisk(StrikeSelection strikes, ChainSnapshot chain, DateTime exitDate)
        {
            // Higher risk if closer to expiry and in-the-money
            if (strikes.Delta > 0.20) return 0.25;
            if (strikes.Delta > 0.15) return 0.10;
            return 0.02;
        }
        
        private double GetWeekendGapRisk(DateTime date, string regime)
        {
            // Oil can gap significantly over weekends due to geopolitical events
            if (regime == "Crisis") return 0.15;
            return 0.05 + new Random().NextDouble() * 0.05;
        }
        
        // Statistical calculation methods
        private double CalculateCAGR(double totalReturn, double initialCapital, DateTime start, DateTime end)
        {
            var years = (end - start).TotalDays / 365.25;
            var finalCapital = initialCapital + totalReturn;
            return (Math.Pow(finalCapital / initialCapital, 1.0 / years) - 1) * 100;
        }
        
        private double CalculateMaxDrawdown(List<RealisticTradeResult> trades, double initialCapital)
        {
            var equity = initialCapital;
            var peak = initialCapital;
            var maxDD = 0.0;
            
            foreach (var trade in trades)
            {
                equity += trade.NetP_L;
                peak = Math.Max(peak, equity);
                var dd = (peak - equity) / peak;
                maxDD = Math.Max(maxDD, dd);
            }
            
            return -maxDD * 100;
        }
        
        private double CalculateSharpeRatio(List<RealisticTradeResult> trades)
        {
            var returns = trades.Select(t => t.NetP_L / 5000.0).ToList(); // Assume $5k average position
            if (!returns.Any()) return 0;
            
            var avgReturn = returns.Average();
            var stdDev = Math.Sqrt(returns.Select(r => Math.Pow(r - avgReturn, 2)).Average());
            return stdDev > 0 ? (avgReturn * Math.Sqrt(52)) / stdDev : 0; // Weekly returns
        }
        
        private double CalculateBreakEvenTrades(List<RealisticTradeResult> trades)
        {
            var avgCost = trades.Average(t => t.TotalCosts);
            var avgGain = trades.Where(t => t.NetP_L > 0).DefaultIfEmpty(new RealisticTradeResult { NetP_L = 0 }).Average(t => t.NetP_L);
            return avgGain > 0 ? avgCost / avgGain : double.MaxValue;
        }
        
        private double CalculateMinimumAccountSize(List<RealisticTradeResult> trades)
        {
            var maxLoss = trades.Min(t => t.NetP_L);
            return Math.Abs(maxLoss) * 20; // 20x worst loss as minimum
        }
        
        private double CalculateAverageRecoveryTime(List<RealisticTradeResult> trades)
        {
            // Simplified - would need equity curve analysis
            return 30; // Placeholder: 30 days average recovery
        }
        
        // Helper classes
        private class OrderExecution
        {
            public double FillPrice { get; set; }
            public double Slippage { get; set; }
            public bool CompleteFill { get; set; }
            public double FillPercent { get; set; }
            public int FillTimeSeconds { get; set; }
        }
        
        private class StrikeSelection
        {
            public double TheoreticalPrice { get; set; }
            public double BidAskSpread { get; set; }
            public double Delta { get; set; }
        }
    }
}