using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ODTE.Optimization.Core;
using ODTE.Optimization.Data;
using ODTE.Optimization.Engine;
using ODTE.Optimization.ML;
using ODTE.Optimization.Reporting;
using ODTE.Optimization.RiskManagement;

namespace ODTE.Optimization
{
    /// <summary>
    /// Main optimization pipeline that orchestrates the entire strategy optimization process
    /// with 5-year data, genetic algorithms, ML learning, and Reverse Fibonacci risk management
    /// </summary>
    public class OptimizationPipeline
    {
        private readonly HistoricalDataFetcher _dataFetcher;
        private readonly GeneticOptimizer _geneticOptimizer;
        private readonly StrategyLearner _strategyLearner;
        private readonly VersionedPnLReporter _reporter;
        private readonly ReverseFibonacciRiskManager _riskManager;
        private readonly IBacktestEngine _backtestEngine;
        
        public OptimizationPipeline(IBacktestEngine backtestEngine)
        {
            _dataFetcher = new HistoricalDataFetcher();
            _geneticOptimizer = new GeneticOptimizer(backtestEngine);
            _strategyLearner = new StrategyLearner();
            _reporter = new VersionedPnLReporter();
            _riskManager = new ReverseFibonacciRiskManager();
            _backtestEngine = backtestEngine;
        }
        
        public async Task<OptimizationRunResult> RunFullOptimizationAsync(
            string strategyName = "ODTE_IronCondor",
            int maxIterations = 10)
        {
            Console.WriteLine("Starting ODTE Strategy Optimization Pipeline");
            Console.WriteLine("=" .PadRight(60, '='));
            
            var result = new OptimizationRunResult
            {
                StartTime = DateTime.Now,
                StrategyName = strategyName
            };
            
            try
            {
                // Step 1: Fetch 5 years of historical data
                Console.WriteLine("\nStep 1: Fetching 5 years of historical data...");
                var dataResult = await _dataFetcher.FetchFiveYearDataAsync("XSP");
                
                var marketData = new MarketDataSet
                {
                    StartDate = dataResult.StartDate,
                    EndDate = dataResult.EndDate,
                    DataPath = dataResult.DataPath,
                    Format = Core.DataFormat.Parquet,
                    Symbols = new List<string> { "XSP" }
                };
                
                Console.WriteLine($"Data fetched: {dataResult.TotalDays} days, {dataResult.TotalBars:N0} bars");
                result.DataFetched = true;
                result.TotalDataDays = dataResult.TotalDays;
                
                // Step 2: Initialize base strategy
                Console.WriteLine("\nStep 2: Initializing base strategy...");
                var baseStrategy = CreateBaseStrategy(strategyName);
                result.Strategies.Add(baseStrategy);
                
                // Step 3: Run optimization iterations
                Console.WriteLine("\nStep 3: Running optimization iterations...");
                var currentBest = baseStrategy;
                
                for (int iteration = 1; iteration <= maxIterations; iteration++)
                {
                    Console.WriteLine($"\n--- Iteration {iteration}/{maxIterations} ---");
                    
                    // Step 3a: Genetic Algorithm Optimization
                    Console.WriteLine("Running genetic algorithm optimization...");
                    var geneticConfig = new OptimizationConfig
                    {
                        MaxGenerations = 50,
                        PopulationSize = 30,
                        MutationRate = 0.1,
                        CrossoverRate = 0.7,
                        EliteRatio = 0.1,
                        FitnessMetric = FitnessFunction.Combined,
                        UseAdaptiveMutation = true
                    };
                    
                    var geneticResult = await _geneticOptimizer.OptimizeAsync(
                        currentBest,
                        marketData,
                        geneticConfig);
                    
                    Console.WriteLine($"Genetic optimization complete: {geneticResult.TotalStrategiesEvaluated} strategies evaluated");
                    Console.WriteLine($"Best fitness: {geneticResult.BestStrategy.Performance?.SharpeRatio:F3}");
                    
                    // Step 3b: ML-based improvement
                    Console.WriteLine("Applying machine learning improvements...");
                    
                    // Run backtest to get trade results
                    var backtestResult = await _backtestEngine.RunBacktestAsync(
                        geneticResult.BestStrategy.Parameters,
                        marketData,
                        _riskManager);
                    
                    var trades = ConvertToTradeResults(backtestResult);
                    var marketContext = new MarketContext
                    {
                        TrendingDays = CalculateTrendingDays(marketData),
                        RangeBoundDays = CalculateRangeBoundDays(marketData),
                        HighVolatilityDays = CalculateHighVolatilityDays(marketData)
                    };
                    
                    var mlImproved = await _strategyLearner.ImproveStrategyAsync(
                        geneticResult.BestStrategy,
                        trades,
                        marketContext);
                    
                    mlImproved.Version = $"{strategyName}_v{iteration}.0";
                    Console.WriteLine($"ML improvements applied: {mlImproved.Version}");
                    
                    // Step 3c: Evaluate with Reverse Fibonacci Risk Management
                    Console.WriteLine("Evaluating with Reverse Fibonacci risk management...");
                    var performance = await _geneticOptimizer.EvaluateStrategyAsync(
                        mlImproved,
                        marketData);
                    
                    mlImproved.Performance = performance;
                    
                    // Step 3d: Generate reports
                    Console.WriteLine("Generating performance reports...");
                    await _reporter.GenerateReportAsync(
                        mlImproved,
                        performance,
                        _riskManager.GetAnalytics());
                    
                    // Step 3e: Check convergence
                    if (HasConverged(currentBest, mlImproved))
                    {
                        Console.WriteLine("\nOptimization has converged - stopping early");
                        result.ConvergedAtIteration = iteration;
                        break;
                    }
                    
                    // Update current best
                    if (IsBetterStrategy(mlImproved, currentBest))
                    {
                        currentBest = mlImproved;
                        result.Strategies.Add(mlImproved);
                        Console.WriteLine($"New best strategy: P&L=${performance.TotalPnL:N2}, Sharpe={performance.SharpeRatio:F3}");
                    }
                    else
                    {
                        Console.WriteLine("No improvement in this iteration");
                    }
                    
                    // Display iteration summary
                    DisplayIterationSummary(iteration, mlImproved, performance);
                }
                
                // Step 4: Generate final reports
                Console.WriteLine("\nStep 4: Generating final reports...");
                await _reporter.GenerateMasterReportAsync();
                
                // Step 5: Save optimization results
                Console.WriteLine("\nStep 5: Saving optimization results...");
                await SaveOptimizationResultsAsync(result, currentBest);
                
                result.BestStrategy = currentBest;
                result.EndTime = DateTime.Now;
                result.Success = true;
                
                // Display final summary
                DisplayFinalSummary(result);
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError during optimization: {ex.Message}");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }
            
            return result;
        }
        
        private StrategyVersion CreateBaseStrategy(string strategyName)
        {
            return new StrategyVersion
            {
                StrategyName = strategyName,
                Version = "1.0.0",
                CreatedAt = DateTime.Now,
                Generation = 0,
                Parameters = new StrategyParameters
                {
                    OpeningRangeMinutes = 15,
                    OpeningRangeBreakoutThreshold = 0.5,
                    MinIVRank = 30,
                    MaxDelta = 0.16,
                    MinPremium = 0.20,
                    StrikeOffset = 5,
                    StopLossPercent = 200,
                    ProfitTargetPercent = 50,
                    DeltaExitThreshold = 0.33,
                    MaxPositionsPerSide = 10,
                    AllocationPerTrade = 1000,
                    EntryStartTime = new TimeSpan(14, 30, 0),
                    EntryEndTime = new TimeSpan(17, 0, 0),
                    ForceCloseTime = new TimeSpan(20, 45, 0),
                    UseVWAPFilter = true,
                    UseATRFilter = true,
                    MinATR = 2.0,
                    MaxATR = 10.0
                }
            };
        }
        
        private List<TradeResult> ConvertToTradeResults(BacktestResult backtest)
        {
            // Convert backtest results to trade results for ML analysis
            var trades = new List<TradeResult>();
            
            if (backtest.DailyPnL != null)
            {
                foreach (var (date, pnl) in backtest.DailyPnL)
                {
                    trades.Add(new TradeResult
                    {
                        EntryTime = date,
                        ExitTime = date.AddHours(6),
                        PnL = pnl,
                        EntryDelta = 0.16, // Placeholder
                        StrikeDistance = 5,
                        ExitReason = pnl > 0 ? "ProfitTarget" : "StopLoss"
                    });
                }
            }
            
            return trades;
        }
        
        private int CalculateTrendingDays(MarketDataSet data)
        {
            // Simplified calculation - would analyze actual data
            return 50;
        }
        
        private int CalculateRangeBoundDays(MarketDataSet data)
        {
            // Simplified calculation
            return 150;
        }
        
        private int CalculateHighVolatilityDays(MarketDataSet data)
        {
            // Simplified calculation
            return 30;
        }
        
        private bool HasConverged(StrategyVersion previous, StrategyVersion current)
        {
            if (previous.Performance == null || current.Performance == null)
                return false;
            
            // Check if improvement is less than 1%
            var improvementRatio = Math.Abs(
                (current.Performance.SharpeRatio - previous.Performance.SharpeRatio) / 
                Math.Max(0.01, Math.Abs(previous.Performance.SharpeRatio)));
            
            return improvementRatio < 0.01;
        }
        
        private bool IsBetterStrategy(StrategyVersion candidate, StrategyVersion current)
        {
            if (current.Performance == null) return true;
            if (candidate.Performance == null) return false;
            
            // Compare using multiple metrics
            int betterCount = 0;
            
            if (candidate.Performance.SharpeRatio > current.Performance.SharpeRatio)
                betterCount++;
            
            if (candidate.Performance.TotalPnL > current.Performance.TotalPnL)
                betterCount++;
            
            if (candidate.Performance.MaxDrawdown > current.Performance.MaxDrawdown) // Less negative
                betterCount++;
            
            if (candidate.Performance.WinRate > current.Performance.WinRate)
                betterCount++;
            
            return betterCount >= 3; // Better in at least 3 out of 4 metrics
        }
        
        private void DisplayIterationSummary(int iteration, StrategyVersion strategy, PerformanceMetrics performance)
        {
            Console.WriteLine("\n" + "-" .PadRight(40, '-'));
            Console.WriteLine($"Iteration {iteration} Summary:");
            Console.WriteLine($"  Version:      {strategy.Version}");
            Console.WriteLine($"  Total P&L:    ${performance.TotalPnL:N2}");
            Console.WriteLine($"  Win Rate:     {performance.WinRate:P1}");
            Console.WriteLine($"  Sharpe:       {performance.SharpeRatio:F3}");
            Console.WriteLine($"  Max Drawdown: ${performance.MaxDrawdown:N2}");
            Console.WriteLine($"  Total Trades: {performance.TotalTrades}");
        }
        
        private void DisplayFinalSummary(OptimizationRunResult result)
        {
            Console.WriteLine("\n" + "=" .PadRight(60, '='));
            Console.WriteLine("OPTIMIZATION COMPLETE");
            Console.WriteLine("=" .PadRight(60, '='));
            
            if (result.BestStrategy != null && result.BestStrategy.Performance != null)
            {
                Console.WriteLine($"\nBest Strategy: {result.BestStrategy.Version}");
                Console.WriteLine($"Total P&L:     ${result.BestStrategy.Performance.TotalPnL:N2}");
                Console.WriteLine($"Sharpe Ratio:  {result.BestStrategy.Performance.SharpeRatio:F3}");
                Console.WriteLine($"Calmar Ratio:  {result.BestStrategy.Performance.CalmarRatio:F3}");
                Console.WriteLine($"Win Rate:      {result.BestStrategy.Performance.WinRate:P1}");
                Console.WriteLine($"Max Drawdown:  ${result.BestStrategy.Performance.MaxDrawdown:N2}");
                Console.WriteLine($"Total Trades:  {result.BestStrategy.Performance.TotalTrades}");
            }
            
            Console.WriteLine($"\nStrategies Evaluated: {result.Strategies.Count}");
            Console.WriteLine($"Data Days Processed:  {result.TotalDataDays}");
            Console.WriteLine($"Optimization Time:    {result.EndTime - result.StartTime:hh\\:mm\\:ss}");
            
            if (result.ConvergedAtIteration.HasValue)
            {
                Console.WriteLine($"Converged at:         Iteration {result.ConvergedAtIteration}");
            }
            
            Console.WriteLine($"\nReports saved to: C:\\code\\ODTE\\Reports\\Optimization");
        }
        
        private async Task SaveOptimizationResultsAsync(OptimizationRunResult result, StrategyVersion bestStrategy)
        {
            var resultsPath = Path.Combine(@"C:\code\ODTE\Reports\Optimization", 
                $"optimization_result_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                OptimizationRun = new
                {
                    StartTime = result.StartTime,
                    EndTime = result.EndTime,
                    Duration = (result.EndTime - result.StartTime).TotalMinutes,
                    Success = result.Success,
                    ConvergedAtIteration = result.ConvergedAtIteration,
                    TotalDataDays = result.TotalDataDays,
                    StrategiesEvaluated = result.Strategies.Count
                },
                BestStrategy = new
                {
                    Version = bestStrategy?.Version,
                    TotalPnL = bestStrategy?.Performance?.TotalPnL,
                    SharpeRatio = bestStrategy?.Performance?.SharpeRatio,
                    CalmarRatio = bestStrategy?.Performance?.CalmarRatio,
                    WinRate = bestStrategy?.Performance?.WinRate,
                    MaxDrawdown = bestStrategy?.Performance?.MaxDrawdown,
                    TotalTrades = bestStrategy?.Performance?.TotalTrades
                },
                Parameters = bestStrategy?.Parameters
            }, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            await File.WriteAllTextAsync(resultsPath, json);
        }
    }
    
    public class OptimizationRunResult
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string StrategyName { get; set; }
        public List<StrategyVersion> Strategies { get; set; } = new List<StrategyVersion>();
        public StrategyVersion BestStrategy { get; set; }
        public bool DataFetched { get; set; }
        public int TotalDataDays { get; set; }
        public int? ConvergedAtIteration { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }
}