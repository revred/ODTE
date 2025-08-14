using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ODTE.Optimization.Core;
using ODTE.Optimization.RiskManagement;

namespace ODTE.Optimization.Engine
{
    public class GeneticOptimizer : IStrategyOptimizer
    {
        private readonly Random _random = new Random();
        private readonly ReverseFibonacciRiskManager _riskManager;
        private readonly IBacktestEngine _backtestEngine;
        
        public GeneticOptimizer(IBacktestEngine backtestEngine)
        {
            _backtestEngine = backtestEngine;
            _riskManager = new ReverseFibonacciRiskManager();
        }
        
        public async Task<OptimizationResult> OptimizeAsync(
            StrategyVersion baseStrategy,
            MarketDataSet historicalData,
            OptimizationConfig config)
        {
            var result = new OptimizationResult
            {
                GenerationHistory = new Dictionary<int, GenerationStats>(),
                TopStrategies = new List<StrategyVersion>()
            };
            
            var startTime = DateTime.Now;
            
            // Initialize population
            var population = await InitializePopulationAsync(baseStrategy, config.PopulationSize);
            
            // Evolution loop
            for (int generation = 0; generation < config.MaxGenerations; generation++)
            {
                // Evaluate fitness for all individuals
                var evaluationTasks = population.Select(strategy => 
                    EvaluateStrategyAsync(strategy, historicalData));
                var evaluations = await Task.WhenAll(evaluationTasks);
                
                // Update strategy performances
                for (int i = 0; i < population.Count; i++)
                {
                    population[i].Performance = evaluations[i];
                }
                
                // Sort by fitness
                population = SortByFitness(population, config.FitnessMetric);
                
                // Record generation statistics
                var genStats = new GenerationStats
                {
                    Generation = generation,
                    BestFitness = CalculateFitness(population[0], config.FitnessMetric),
                    AverageFitness = population.Average(p => CalculateFitness(p, config.FitnessMetric)),
                    WorstFitness = CalculateFitness(population.Last(), config.FitnessMetric),
                    PopulationSize = population.Count
                };
                result.GenerationHistory[generation] = genStats;
                
                // Check for convergence
                if (generation > 10 && HasConverged(result.GenerationHistory, generation))
                {
                    break;
                }
                
                // Create next generation
                population = await CreateNextGenerationAsync(population, config, generation);
            }
            
            // Final evaluation and selection
            result.BestStrategy = population[0];
            result.TopStrategies = population.Take(10).ToList();
            result.GenerationsProcessed = result.GenerationHistory.Count;
            result.TotalStrategiesEvaluated = result.GenerationsProcessed * config.PopulationSize;
            result.OptimizationDuration = DateTime.Now - startTime;
            
            return result;
        }
        
        private async Task<List<StrategyVersion>> InitializePopulationAsync(
            StrategyVersion baseStrategy,
            int populationSize)
        {
            var population = new List<StrategyVersion> { baseStrategy };
            
            // Generate variations
            var variations = await GenerateVariationsAsync(baseStrategy, populationSize - 1);
            population.AddRange(variations);
            
            return population;
        }
        
        public async Task<List<StrategyVersion>> GenerateVariationsAsync(
            StrategyVersion parent,
            int count)
        {
            var variations = new List<StrategyVersion>();
            
            for (int i = 0; i < count; i++)
            {
                var variation = CreateMutation(parent);
                variation.Version = $"{parent.Version}.{i + 1}";
                variation.ParentVersion = parent.Version;
                variation.Generation = parent.Generation + 1;
                variations.Add(variation);
            }
            
            return await Task.FromResult(variations);
        }
        
        private StrategyVersion CreateMutation(StrategyVersion parent)
        {
            var mutated = new StrategyVersion
            {
                StrategyName = parent.StrategyName,
                CreatedAt = DateTime.Now,
                Parameters = MutateParameters(parent.Parameters)
            };
            
            return mutated;
        }
        
        private StrategyParameters MutateParameters(StrategyParameters original)
        {
            var mutated = new StrategyParameters
            {
                // Mutate numeric parameters by +/- 10%
                OpeningRangeMinutes = MutateInt(original.OpeningRangeMinutes, 5, 60),
                OpeningRangeBreakoutThreshold = MutateDouble(original.OpeningRangeBreakoutThreshold, 0.1, 2.0),
                MinIVRank = MutateDouble(original.MinIVRank, 10, 80),
                MaxDelta = MutateDouble(original.MaxDelta, 0.05, 0.30),
                MinPremium = MutateDouble(original.MinPremium, 0.10, 1.00),
                StrikeOffset = MutateInt(original.StrikeOffset, 1, 20),
                StopLossPercent = MutateDouble(original.StopLossPercent, 100, 400),
                ProfitTargetPercent = MutateDouble(original.ProfitTargetPercent, 20, 100),
                DeltaExitThreshold = MutateDouble(original.DeltaExitThreshold, 0.20, 0.50),
                MaxPositionsPerSide = MutateInt(original.MaxPositionsPerSide, 1, 20),
                AllocationPerTrade = MutateDouble(original.AllocationPerTrade, 500, 5000),
                
                // Keep boolean and time parameters with occasional flip
                UseVWAPFilter = _random.NextDouble() < 0.9 ? original.UseVWAPFilter : !original.UseVWAPFilter,
                UseATRFilter = _random.NextDouble() < 0.9 ? original.UseATRFilter : !original.UseATRFilter,
                MinATR = MutateDouble(original.MinATR, 1.0, 5.0),
                MaxATR = MutateDouble(original.MaxATR, 5.0, 20.0),
                
                // Keep timing mostly the same
                EntryStartTime = original.EntryStartTime,
                EntryEndTime = original.EntryEndTime,
                ForceCloseTime = original.ForceCloseTime
            };
            
            return mutated;
        }
        
        private int MutateInt(int value, int min, int max)
        {
            var mutationRange = (max - min) * 0.1;
            var mutation = (int)(_random.NextDouble() * mutationRange * 2 - mutationRange);
            var newValue = value + mutation;
            return Math.Max(min, Math.Min(max, newValue));
        }
        
        private double MutateDouble(double value, double min, double max)
        {
            var mutationRange = (max - min) * 0.1;
            var mutation = _random.NextDouble() * mutationRange * 2 - mutationRange;
            var newValue = value + mutation;
            return Math.Max(min, Math.Min(max, newValue));
        }
        
        public async Task<PerformanceMetrics> EvaluateStrategyAsync(
            StrategyVersion strategy,
            MarketDataSet testData)
        {
            // Run backtest with Reverse Fibonacci risk management
            var backtestResult = await _backtestEngine.RunBacktestAsync(
                strategy.Parameters,
                testData,
                _riskManager);
            
            var metrics = new PerformanceMetrics
            {
                TotalPnL = backtestResult.TotalPnL,
                MaxDrawdown = backtestResult.MaxDrawdown,
                WinRate = backtestResult.WinRate,
                SharpeRatio = CalculateSharpe(backtestResult.DailyReturns),
                CalmarRatio = backtestResult.MaxDrawdown != 0 ? 
                    backtestResult.AnnualizedReturn / Math.Abs(backtestResult.MaxDrawdown) : 0,
                TotalTrades = backtestResult.TotalTrades,
                WinningDays = backtestResult.WinningDays,
                LosingDays = backtestResult.LosingDays,
                AverageDailyPnL = backtestResult.AverageDailyPnL,
                StandardDeviation = CalculateStdDev(backtestResult.DailyReturns),
                ProfitFactor = backtestResult.ProfitFactor,
                ExpectedValue = backtestResult.ExpectedValue,
                DailyPnL = backtestResult.DailyPnL
            };
            
            return metrics;
        }
        
        private double CalculateSharpe(List<double> returns)
        {
            if (returns.Count < 2) return 0;
            
            var mean = returns.Average();
            var stdDev = CalculateStdDev(returns);
            
            if (stdDev == 0) return 0;
            
            // Annualized Sharpe ratio (assuming 252 trading days)
            return mean / stdDev * Math.Sqrt(252);
        }
        
        private double CalculateStdDev(List<double> values)
        {
            if (values.Count < 2) return 0;
            
            var mean = values.Average();
            var sumOfSquares = values.Sum(v => Math.Pow(v - mean, 2));
            return Math.Sqrt(sumOfSquares / (values.Count - 1));
        }
        
        private List<StrategyVersion> SortByFitness(
            List<StrategyVersion> population,
            FitnessFunction fitnessMetric)
        {
            return population.OrderByDescending(p => CalculateFitness(p, fitnessMetric)).ToList();
        }
        
        private double CalculateFitness(StrategyVersion strategy, FitnessFunction metric)
        {
            if (strategy.Performance == null) return double.MinValue;
            
            return metric switch
            {
                FitnessFunction.TotalPnL => strategy.Performance.TotalPnL,
                FitnessFunction.SharpeRatio => strategy.Performance.SharpeRatio,
                FitnessFunction.CalmarRatio => strategy.Performance.CalmarRatio,
                FitnessFunction.ProfitFactor => strategy.Performance.ProfitFactor,
                FitnessFunction.Combined => CalculateCombinedFitness(strategy.Performance),
                _ => strategy.Performance.TotalPnL
            };
        }
        
        private double CalculateCombinedFitness(PerformanceMetrics metrics)
        {
            // Weighted combination of multiple metrics
            var fitness = 0.0;
            
            fitness += metrics.TotalPnL * 0.3;                    // 30% weight on total P&L
            fitness += metrics.SharpeRatio * 1000 * 0.25;         // 25% weight on Sharpe
            fitness += metrics.WinRate * 100 * 0.20;              // 20% weight on win rate
            fitness += (1 / (1 + metrics.MaxDrawdown)) * 1000 * 0.15; // 15% weight on drawdown control
            fitness += metrics.ProfitFactor * 100 * 0.10;         // 10% weight on profit factor
            
            return fitness;
        }
        
        private bool HasConverged(Dictionary<int, GenerationStats> history, int currentGen)
        {
            if (currentGen < 10) return false;
            
            // Check if best fitness hasn't improved significantly in last 5 generations
            var recentGens = Enumerable.Range(currentGen - 4, 5)
                .Select(g => history[g].BestFitness)
                .ToList();
            
            var variance = CalculateStdDev(recentGens);
            return variance < 0.01; // Less than 1% variation
        }
        
        private async Task<List<StrategyVersion>> CreateNextGenerationAsync(
            List<StrategyVersion> currentPopulation,
            OptimizationConfig config,
            int generation)
        {
            var nextGen = new List<StrategyVersion>();
            
            // Elite selection - keep top performers
            var eliteCount = (int)(currentPopulation.Count * config.EliteRatio);
            nextGen.AddRange(currentPopulation.Take(eliteCount));
            
            // Fill rest with offspring
            while (nextGen.Count < config.PopulationSize)
            {
                // Tournament selection for parents
                var parent1 = TournamentSelect(currentPopulation);
                var parent2 = TournamentSelect(currentPopulation);
                
                // Crossover
                if (_random.NextDouble() < config.CrossoverRate)
                {
                    var offspring = Crossover(parent1, parent2);
                    offspring.Generation = generation + 1;
                    offspring.Version = $"G{generation + 1}.{nextGen.Count}";
                    nextGen.Add(offspring);
                }
                else
                {
                    // Direct mutation of better parent
                    var mutated = CreateMutation(parent1);
                    mutated.Generation = generation + 1;
                    mutated.Version = $"G{generation + 1}.{nextGen.Count}";
                    nextGen.Add(mutated);
                }
            }
            
            // Apply adaptive mutation
            if (config.UseAdaptiveMutation)
            {
                var mutationRate = CalculateAdaptiveMutationRate(generation, config.MaxGenerations);
                await ApplyAdaptiveMutationAsync(nextGen.Skip(eliteCount).ToList(), mutationRate);
            }
            
            return nextGen;
        }
        
        private StrategyVersion TournamentSelect(List<StrategyVersion> population)
        {
            var tournamentSize = 3;
            var tournament = new List<StrategyVersion>();
            
            for (int i = 0; i < tournamentSize; i++)
            {
                tournament.Add(population[_random.Next(population.Count)]);
            }
            
            return tournament.OrderByDescending(s => 
                CalculateFitness(s, FitnessFunction.Combined)).First();
        }
        
        private StrategyVersion Crossover(StrategyVersion parent1, StrategyVersion parent2)
        {
            var offspring = new StrategyVersion
            {
                StrategyName = parent1.StrategyName,
                CreatedAt = DateTime.Now,
                Parameters = new StrategyParameters
                {
                    // Mix parameters from both parents
                    OpeningRangeMinutes = _random.NextDouble() < 0.5 ? 
                        parent1.Parameters.OpeningRangeMinutes : parent2.Parameters.OpeningRangeMinutes,
                    MinIVRank = (parent1.Parameters.MinIVRank + parent2.Parameters.MinIVRank) / 2,
                    MaxDelta = _random.NextDouble() < 0.5 ? 
                        parent1.Parameters.MaxDelta : parent2.Parameters.MaxDelta,
                    MinPremium = (parent1.Parameters.MinPremium + parent2.Parameters.MinPremium) / 2,
                    StrikeOffset = _random.NextDouble() < 0.5 ? 
                        parent1.Parameters.StrikeOffset : parent2.Parameters.StrikeOffset,
                    StopLossPercent = (parent1.Parameters.StopLossPercent + parent2.Parameters.StopLossPercent) / 2,
                    ProfitTargetPercent = _random.NextDouble() < 0.5 ? 
                        parent1.Parameters.ProfitTargetPercent : parent2.Parameters.ProfitTargetPercent,
                    DeltaExitThreshold = (parent1.Parameters.DeltaExitThreshold + parent2.Parameters.DeltaExitThreshold) / 2,
                    MaxPositionsPerSide = _random.NextDouble() < 0.5 ? 
                        parent1.Parameters.MaxPositionsPerSide : parent2.Parameters.MaxPositionsPerSide,
                    UseVWAPFilter = _random.NextDouble() < 0.5 ? 
                        parent1.Parameters.UseVWAPFilter : parent2.Parameters.UseVWAPFilter,
                    UseATRFilter = _random.NextDouble() < 0.5 ? 
                        parent1.Parameters.UseATRFilter : parent2.Parameters.UseATRFilter
                }
            };
            
            return offspring;
        }
        
        private double CalculateAdaptiveMutationRate(int generation, int maxGenerations)
        {
            // Start with higher mutation, decrease over time
            var progress = (double)generation / maxGenerations;
            return 0.2 * (1 - progress) + 0.01; // From 20% to 1%
        }
        
        private async Task ApplyAdaptiveMutationAsync(
            List<StrategyVersion> strategies,
            double mutationRate)
        {
            foreach (var strategy in strategies)
            {
                if (_random.NextDouble() < mutationRate)
                {
                    strategy.Parameters = MutateParameters(strategy.Parameters);
                }
            }
            
            await Task.CompletedTask;
        }
    }
    
    // Placeholder interface for backtest engine
    public interface IBacktestEngine
    {
        Task<BacktestResult> RunBacktestAsync(
            StrategyParameters parameters,
            MarketDataSet data,
            ReverseFibonacciRiskManager riskManager);
    }
    
    public class BacktestResult
    {
        public double TotalPnL { get; set; }
        public double MaxDrawdown { get; set; }
        public double WinRate { get; set; }
        public int TotalTrades { get; set; }
        public int WinningDays { get; set; }
        public int LosingDays { get; set; }
        public double AverageDailyPnL { get; set; }
        public double AnnualizedReturn { get; set; }
        public double ProfitFactor { get; set; }
        public double ExpectedValue { get; set; }
        public List<double> DailyReturns { get; set; }
        public Dictionary<DateTime, double> DailyPnL { get; set; }
    }
}