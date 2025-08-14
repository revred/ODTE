using System;
using System.Threading.Tasks;
using ODTE.Optimization;
using ODTE.Optimization.Core;
using ODTE.Optimization.Engine;
using ODTE.Optimization.RiskManagement;

namespace ODTE.Optimization
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("ODTE Strategy Optimization System");
            Console.WriteLine("Version 1.0.0");
            Console.WriteLine("=" .PadRight(60, '='));
            Console.WriteLine();
            Console.WriteLine("This system will:");
            Console.WriteLine("1. Fetch 5 years of historical market data");
            Console.WriteLine("2. Optimize trading strategies using genetic algorithms");
            Console.WriteLine("3. Apply machine learning improvements");
            Console.WriteLine("4. Implement Reverse Fibonacci risk management");
            Console.WriteLine("5. Generate comprehensive P&L reports");
            Console.WriteLine();
            
            // Use command line arguments or defaults for non-interactive mode
            var strategyName = args.Length > 0 ? args[0] : "ODTE_IronCondor";
            int maxIterations = args.Length > 1 && int.TryParse(args[1], out var iter) ? iter : 5;
            
            Console.WriteLine();
            Console.WriteLine("Starting optimization with:");
            Console.WriteLine($"  Strategy: {strategyName}");
            Console.WriteLine($"  Max Iterations: {maxIterations}");
            Console.WriteLine($"  Risk Management: Reverse Fibonacci");
            Console.WriteLine($"  Initial Max Loss: $500/day");
            Console.WriteLine();
            
            Console.WriteLine("Starting optimization automatically in non-interactive mode...");
            
            try
            {
                // Create backtest engine adapter
                var backtestEngine = new BacktestEngineAdapter();
                
                // Create and run optimization pipeline
                var pipeline = new OptimizationPipeline(backtestEngine);
                var result = await pipeline.RunFullOptimizationAsync(strategyName, maxIterations);
                
                if (result.Success)
                {
                    Console.WriteLine("\nOptimization completed successfully!");
                    Console.WriteLine($"Best strategy saved as: {result.BestStrategy?.Version}");
                    Console.WriteLine("\nReports available at: C:\\code\\ODTE\\ODTE.Optimization\\Reports");
                }
                else
                {
                    Console.WriteLine($"\nOptimization failed: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nFatal error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            
            Console.WriteLine("\nOptimization run completed.");
        }
    }
    
    /// <summary>
    /// Adapter to connect optimization engine with existing backtest infrastructure
    /// </summary>
    public class BacktestEngineAdapter : IBacktestEngine
    {
        public async Task<BacktestResult> RunBacktestAsync(
            StrategyParameters parameters,
            MarketDataSet data,
            ReverseFibonacciRiskManager riskManager)
        {
            // This would integrate with the existing ODTE.Backtest engine
            // For now, returning simulated results
            
            var random = new Random();
            var tradingDays = 250 * 5; // 5 years
            var dailyPnL = new Dictionary<DateTime, double>();
            var dailyReturns = new List<double>();
            
            double totalPnL = 0;
            double maxDrawdown = 0;
            double peak = 0;
            int winningDays = 0;
            int losingDays = 0;
            
            var currentDate = data.StartDate;
            
            for (int i = 0; i < tradingDays; i++)
            {
                // Skip weekends
                while (currentDate.DayOfWeek == DayOfWeek.Saturday || 
                       currentDate.DayOfWeek == DayOfWeek.Sunday)
                {
                    currentDate = currentDate.AddDays(1);
                }
                
                // Start new trading day
                riskManager.StartNewDay(currentDate);
                
                // Simulate daily P&L based on strategy parameters
                double winProb = 0.45 + (parameters.MinIVRank / 200.0); // Higher IV = better win rate
                winProb += parameters.UseVWAPFilter ? 0.05 : 0;
                winProb += parameters.UseATRFilter ? 0.05 : 0;
                winProb = Math.Min(0.65, winProb); // Cap at 65% win rate
                
                double dayPnL = 0;
                int dayTrades = random.Next(1, 4); // 1-3 trades per day
                
                for (int t = 0; t < dayTrades; t++)
                {
                    if (random.NextDouble() < winProb)
                    {
                        // Winning trade
                        dayPnL += parameters.AllocationPerTrade * (parameters.ProfitTargetPercent / 100.0);
                    }
                    else
                    {
                        // Losing trade
                        dayPnL -= parameters.AllocationPerTrade * (parameters.StopLossPercent / 100.0);
                    }
                    
                    // Check risk limits
                    if (!riskManager.ValidatePosition(dayPnL))
                    {
                        break; // Stop trading for the day
                    }
                }
                
                // Update risk manager
                riskManager.UpdateDailyPnL(dayPnL);
                
                // Record results
                dailyPnL[currentDate] = dayPnL;
                dailyReturns.Add(dayPnL);
                totalPnL += dayPnL;
                
                if (dayPnL > 0) winningDays++;
                else if (dayPnL < 0) losingDays++;
                
                // Track drawdown
                peak = Math.Max(peak, totalPnL);
                var drawdown = totalPnL - peak;
                maxDrawdown = Math.Min(maxDrawdown, drawdown);
                
                currentDate = currentDate.AddDays(1);
            }
            
            // Calculate metrics
            var avgDaily = dailyReturns.Average();
            var stdDev = Math.Sqrt(dailyReturns.Sum(r => Math.Pow(r - avgDaily, 2)) / dailyReturns.Count);
            
            return new BacktestResult
            {
                TotalPnL = totalPnL,
                MaxDrawdown = maxDrawdown,
                WinRate = (double)winningDays / (winningDays + losingDays),
                TotalTrades = tradingDays * 2, // Approximate
                WinningDays = winningDays,
                LosingDays = losingDays,
                AverageDailyPnL = avgDaily,
                AnnualizedReturn = avgDaily * 252,
                ProfitFactor = winningDays > 0 && losingDays > 0 ? 
                    (winningDays * 50.0) / (losingDays * 100.0) : 0,
                ExpectedValue = avgDaily,
                DailyReturns = dailyReturns,
                DailyPnL = dailyPnL
            };
        }
    }
}