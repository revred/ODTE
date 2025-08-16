using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ODTE.Strategy
{
    /// <summary>
    /// PM250 Genetic Algorithm Optimizer
    /// 
    /// GENETIC EVOLUTION FOR OPTIMAL PM250 PARAMETERS:
    /// - Optimizes for 2018-2019 period with strict risk controls
    /// - Maximum $2,500 drawdown constraint (HARD LIMIT)
    /// - Reverse Fibonacci capital curtailment integration
    /// - Multi-objective optimization: profit + risk + win rate
    /// - Population-based parameter evolution
    /// - NO COMPROMISE on risk mandate
    /// </summary>
    public class PM250_GeneticOptimizer
    {
        private readonly ILogger<PM250_GeneticOptimizer>? _logger;
        private readonly Random _random;
        
        // Genetic Algorithm Configuration
        private const int POPULATION_SIZE = 50;
        private const int GENERATIONS = 100;
        private const double MUTATION_RATE = 0.15;
        private const double CROSSOVER_RATE = 0.8;
        private const double ELITE_PERCENTAGE = 0.1;
        
        // Risk Control Constants
        private const decimal MAX_DRAWDOWN_LIMIT = 2500m; // ABSOLUTE CONSTRAINT
        private const double MIN_WIN_RATE = 0.75; // NO DILUTION
        private const double MIN_EXECUTION_RATE = 0.10; // Quality threshold
        
        public PM250_GeneticOptimizer(ILogger<PM250_GeneticOptimizer>? logger = null)
        {
            _logger = logger;
            _random = new Random(42); // Deterministic for reproducibility
        }
        
        public async Task<OptimizationResult> OptimizeAsync(
            DateTime startDate, 
            DateTime endDate,
            CancellationToken cancellationToken = default)
        {
            _logger?.LogInformation("🧬 Starting PM250 Genetic Algorithm Optimization");
            _logger?.LogInformation($"📅 Period: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            _logger?.LogInformation($"🛡️ Max Drawdown Limit: ${MAX_DRAWDOWN_LIMIT:N0}");
            _logger?.LogInformation($"🎯 Min Win Rate: {MIN_WIN_RATE:P0}");
            
            var result = new OptimizationResult
            {
                StartDate = startDate,
                EndDate = endDate,
                MaxDrawdownLimit = MAX_DRAWDOWN_LIMIT,
                StartTime = DateTime.UtcNow
            };
            
            try
            {
                // Step 1: Initialize population
                var population = InitializePopulation();
                _logger?.LogInformation($"👥 Initialized population: {population.Count} chromosomes");
                
                // Step 2: Evaluate initial fitness
                await EvaluatePopulationAsync(population, startDate, endDate);
                
                var bestChromosome = population.OrderByDescending(c => c.Fitness).First();
                _logger?.LogInformation($"🏆 Initial best fitness: {bestChromosome.Fitness:F4}");
                
                // Step 3: Genetic evolution
                for (int generation = 0; generation < GENERATIONS; generation++)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;
                    
                    // Selection, crossover, mutation
                    var newPopulation = EvolvePopulation(population);
                    
                    // Evaluate new generation
                    await EvaluatePopulationAsync(newPopulation, startDate, endDate);
                    
                    population = newPopulation;
                    bestChromosome = population.OrderByDescending(c => c.Fitness).First();
                    
                    if (generation % 10 == 0)
                    {
                        _logger?.LogInformation($"🧬 Generation {generation}: Best fitness = {bestChromosome.Fitness:F4}, " +
                                              $"Drawdown = ${bestChromosome.PerformanceMetrics?.MaxDrawdown:F0}");
                    }
                    
                    // Early termination if perfect solution found
                    if (bestChromosome.Fitness > 0.99 && 
                        bestChromosome.PerformanceMetrics?.MaxDrawdown <= MAX_DRAWDOWN_LIMIT)
                    {
                        _logger?.LogInformation($"✅ Optimal solution found at generation {generation}");
                        break;
                    }
                }
                
                // Step 4: Final results
                result.OptimalChromosome = bestChromosome;
                result.FinalPopulation = population;
                result.Success = true;
                result.GenerationsCompleted = GENERATIONS;
                
                _logger?.LogInformation("🎉 Genetic optimization completed successfully");
                _logger?.LogInformation($"🏆 Best parameters: {bestChromosome.GetParameterSummary()}");
                
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                _logger?.LogError(ex, "❌ Genetic optimization failed");
                return result;
            }
            finally
            {
                result.EndTime = DateTime.UtcNow;
            }
        }
        
        private List<PM250_Chromosome> InitializePopulation()
        {
            var population = new List<PM250_Chromosome>();
            
            for (int i = 0; i < POPULATION_SIZE; i++)
            {
                var chromosome = new PM250_Chromosome
                {
                    GoScoreThreshold = RandomInRange(55.0, 80.0),
                    ProfitTarget = (decimal)RandomInRange(1.5, 5.0),
                    CreditTarget = (decimal)RandomInRange(0.06, 0.12),
                    VIXSensitivity = RandomInRange(0.5, 2.0),
                    TrendTolerance = RandomInRange(0.3, 1.2),
                    RiskMultiplier = RandomInRange(0.8, 1.5),
                    TimeOfDayWeight = RandomInRange(0.5, 1.5),
                    MarketRegimeWeight = RandomInRange(0.8, 1.3),
                    VolatilityWeight = RandomInRange(0.7, 1.4),
                    MomentumWeight = RandomInRange(0.6, 1.2)
                };
                
                population.Add(chromosome);
            }
            
            return population;
        }
        
        private async Task EvaluatePopulationAsync(List<PM250_Chromosome> population, DateTime startDate, DateTime endDate)
        {
            var tasks = population.Select(async chromosome =>
            {
                try
                {
                    var metrics = await BacktestChromosomeAsync(chromosome, startDate, endDate);
                    chromosome.PerformanceMetrics = metrics;
                    chromosome.Fitness = CalculateFitness(metrics);
                }
                catch (Exception ex)
                {
                    chromosome.Fitness = 0.0; // Penalty for invalid chromosomes
                    chromosome.ErrorMessage = ex.Message;
                }
            });
            
            await Task.WhenAll(tasks);
        }
        
        private async Task<PerformanceMetrics> BacktestChromosomeAsync(PM250_Chromosome chromosome, DateTime startDate, DateTime endDate)
        {
            // Create strategy with chromosome parameters
            var strategy = new PM250_GeneticStrategy(chromosome);
            var riskManager = new ReverseFibonacciRiskManager();
            
            var metrics = new PerformanceMetrics();
            var trades = new List<TradeResult>();
            var dailyPnL = new Dictionary<DateTime, decimal>();
            
            // Simulate trading over the period
            var currentDate = startDate;
            while (currentDate <= endDate)
            {
                // Skip weekends
                if (currentDate.DayOfWeek == DayOfWeek.Saturday || currentDate.DayOfWeek == DayOfWeek.Sunday)
                {
                    currentDate = currentDate.AddDays(1);
                    continue;
                }
                
                var dayPnL = 0m;
                
                // Simulate multiple trading opportunities per day (every 30 minutes)
                for (int hour = 9; hour <= 15; hour++)
                {
                    for (int minute = 0; minute < 60; minute += 30)
                    {
                        var tradeTime = new DateTime(currentDate.Year, currentDate.Month, currentDate.Day, hour, minute, 0);
                        
                        // Create market conditions (simplified simulation)
                        var conditions = SimulateMarketConditions(tradeTime);
                        var parameters = new StrategyParameters { PositionSize = 1, MaxRisk = 100 };
                        
                        // Apply risk management
                        var adjustedSize = riskManager.CalculatePositionSize(parameters.PositionSize, 
                            trades.Select(t => new TradeExecution 
                            { 
                                ExecutionTime = t.ExecutionTime, 
                                PnL = t.PnL, 
                                Success = t.PnL > 0 
                            }).ToList());
                        
                        parameters.PositionSize = adjustedSize;
                        
                        // Execute strategy
                        var result = await strategy.ExecuteAsync(parameters, conditions);
                        
                        if (result.PnL != 0)
                        {
                            var trade = new TradeResult
                            {
                                ExecutionTime = tradeTime,
                                PnL = result.PnL,
                                IsWin = result.PnL > 0
                            };
                            
                            trades.Add(trade);
                            dayPnL += result.PnL;
                            
                            // Check drawdown constraint in real-time
                            var currentDrawdown = CalculateCurrentDrawdown(trades);
                            if (currentDrawdown > MAX_DRAWDOWN_LIMIT)
                            {
                                // Hard stop - return with penalty
                                metrics.MaxDrawdown = currentDrawdown;
                                metrics.ViolatesRiskMandates = true;
                                return metrics;
                            }
                        }
                    }
                }
                
                dailyPnL[currentDate] = dayPnL;
                currentDate = currentDate.AddDays(1);
            }
            
            // Calculate comprehensive metrics
            metrics.TotalTrades = trades.Count;
            metrics.WinningTrades = trades.Count(t => t.IsWin);
            metrics.WinRate = trades.Count > 0 ? metrics.WinningTrades / (double)trades.Count : 0;
            metrics.TotalPnL = trades.Sum(t => t.PnL);
            metrics.AvgTradeSize = trades.Count > 0 ? trades.Average(t => t.PnL) : 0;
            metrics.MaxDrawdown = CalculateMaxDrawdown(trades);
            metrics.SharpeRatio = CalculateSharpeRatio(dailyPnL.Values.ToList());
            metrics.ProfitFactor = CalculateProfitFactor(trades);
            metrics.ExecutionRate = CalculateExecutionRate(startDate, endDate, trades.Count);
            
            // Risk mandate validation
            metrics.ViolatesRiskMandates = 
                metrics.MaxDrawdown > MAX_DRAWDOWN_LIMIT ||
                metrics.WinRate < MIN_WIN_RATE ||
                metrics.ExecutionRate < MIN_EXECUTION_RATE;
            
            return metrics;
        }
        
        private double CalculateFitness(PerformanceMetrics metrics)
        {
            // STRICT FITNESS FUNCTION - NO COMPROMISE ON RISK
            if (metrics.ViolatesRiskMandates)
                return 0.0; // ZERO fitness for risk violations
            
            var fitness = 0.0;
            
            // Primary objective: Risk-adjusted returns (50% weight)
            var returnScore = Math.Max(0, (double)metrics.TotalPnL / 10000.0); // Normalize to 0-1
            fitness += returnScore * 0.5;
            
            // Win rate preservation (25% weight)
            var winRateScore = Math.Min(1.0, metrics.WinRate / MIN_WIN_RATE);
            fitness += winRateScore * 0.25;
            
            // Drawdown control (15% weight) - reward staying well below limit
            var drawdownScore = 1.0 - ((double)metrics.MaxDrawdown / (double)MAX_DRAWDOWN_LIMIT);
            fitness += Math.Max(0, drawdownScore) * 0.15;
            
            // Execution rate (10% weight)
            var executionScore = Math.Min(1.0, metrics.ExecutionRate / 0.25); // Cap at 25%
            fitness += executionScore * 0.10;
            
            return Math.Max(0.0, Math.Min(1.0, fitness));
        }
        
        private List<PM250_Chromosome> EvolvePopulation(List<PM250_Chromosome> population)
        {
            var newPopulation = new List<PM250_Chromosome>();
            
            // Elite selection (keep best chromosomes)
            var eliteCount = (int)(POPULATION_SIZE * ELITE_PERCENTAGE);
            var elites = population.OrderByDescending(c => c.Fitness).Take(eliteCount).ToList();
            newPopulation.AddRange(elites.Select(e => e.Clone()));
            
            // Generate rest through crossover and mutation
            while (newPopulation.Count < POPULATION_SIZE)
            {
                var parent1 = TournamentSelection(population);
                var parent2 = TournamentSelection(population);
                
                var (offspring1, offspring2) = Crossover(parent1, parent2);
                
                Mutate(offspring1);
                Mutate(offspring2);
                
                newPopulation.Add(offspring1);
                if (newPopulation.Count < POPULATION_SIZE)
                    newPopulation.Add(offspring2);
            }
            
            return newPopulation;
        }
        
        private PM250_Chromosome TournamentSelection(List<PM250_Chromosome> population, int tournamentSize = 3)
        {
            var tournament = new List<PM250_Chromosome>();
            for (int i = 0; i < tournamentSize; i++)
            {
                tournament.Add(population[_random.Next(population.Count)]);
            }
            return tournament.OrderByDescending(c => c.Fitness).First();
        }
        
        private (PM250_Chromosome, PM250_Chromosome) Crossover(PM250_Chromosome parent1, PM250_Chromosome parent2)
        {
            if (_random.NextDouble() > CROSSOVER_RATE)
                return (parent1.Clone(), parent2.Clone());
            
            var offspring1 = new PM250_Chromosome();
            var offspring2 = new PM250_Chromosome();
            
            // Uniform crossover
            offspring1.GoScoreThreshold = _random.NextDouble() < 0.5 ? parent1.GoScoreThreshold : parent2.GoScoreThreshold;
            offspring2.GoScoreThreshold = _random.NextDouble() < 0.5 ? parent2.GoScoreThreshold : parent1.GoScoreThreshold;
            
            offspring1.ProfitTarget = _random.NextDouble() < 0.5 ? parent1.ProfitTarget : parent2.ProfitTarget;
            offspring2.ProfitTarget = _random.NextDouble() < 0.5 ? parent2.ProfitTarget : parent1.ProfitTarget;
            
            offspring1.CreditTarget = _random.NextDouble() < 0.5 ? parent1.CreditTarget : parent2.CreditTarget;
            offspring2.CreditTarget = _random.NextDouble() < 0.5 ? parent2.CreditTarget : parent1.CreditTarget;
            
            offspring1.VIXSensitivity = _random.NextDouble() < 0.5 ? parent1.VIXSensitivity : parent2.VIXSensitivity;
            offspring2.VIXSensitivity = _random.NextDouble() < 0.5 ? parent2.VIXSensitivity : parent1.VIXSensitivity;
            
            offspring1.TrendTolerance = _random.NextDouble() < 0.5 ? parent1.TrendTolerance : parent2.TrendTolerance;
            offspring2.TrendTolerance = _random.NextDouble() < 0.5 ? parent2.TrendTolerance : parent1.TrendTolerance;
            
            offspring1.RiskMultiplier = _random.NextDouble() < 0.5 ? parent1.RiskMultiplier : parent2.RiskMultiplier;
            offspring2.RiskMultiplier = _random.NextDouble() < 0.5 ? parent2.RiskMultiplier : parent1.RiskMultiplier;
            
            offspring1.TimeOfDayWeight = _random.NextDouble() < 0.5 ? parent1.TimeOfDayWeight : parent2.TimeOfDayWeight;
            offspring2.TimeOfDayWeight = _random.NextDouble() < 0.5 ? parent2.TimeOfDayWeight : parent1.TimeOfDayWeight;
            
            offspring1.MarketRegimeWeight = _random.NextDouble() < 0.5 ? parent1.MarketRegimeWeight : parent2.MarketRegimeWeight;
            offspring2.MarketRegimeWeight = _random.NextDouble() < 0.5 ? parent2.MarketRegimeWeight : parent1.MarketRegimeWeight;
            
            offspring1.VolatilityWeight = _random.NextDouble() < 0.5 ? parent1.VolatilityWeight : parent2.VolatilityWeight;
            offspring2.VolatilityWeight = _random.NextDouble() < 0.5 ? parent2.VolatilityWeight : parent1.VolatilityWeight;
            
            offspring1.MomentumWeight = _random.NextDouble() < 0.5 ? parent1.MomentumWeight : parent2.MomentumWeight;
            offspring2.MomentumWeight = _random.NextDouble() < 0.5 ? parent2.MomentumWeight : parent1.MomentumWeight;
            
            return (offspring1, offspring2);
        }
        
        private void Mutate(PM250_Chromosome chromosome)
        {
            if (_random.NextDouble() < MUTATION_RATE)
                chromosome.GoScoreThreshold = MutateValue(chromosome.GoScoreThreshold, 55.0, 80.0);
                
            if (_random.NextDouble() < MUTATION_RATE)
                chromosome.ProfitTarget = (decimal)MutateValue((double)chromosome.ProfitTarget, 1.5, 5.0);
                
            if (_random.NextDouble() < MUTATION_RATE)
                chromosome.CreditTarget = (decimal)MutateValue((double)chromosome.CreditTarget, 0.06, 0.12);
                
            if (_random.NextDouble() < MUTATION_RATE)
                chromosome.VIXSensitivity = MutateValue(chromosome.VIXSensitivity, 0.5, 2.0);
                
            if (_random.NextDouble() < MUTATION_RATE)
                chromosome.TrendTolerance = MutateValue(chromosome.TrendTolerance, 0.3, 1.2);
                
            if (_random.NextDouble() < MUTATION_RATE)
                chromosome.RiskMultiplier = MutateValue(chromosome.RiskMultiplier, 0.8, 1.5);
                
            if (_random.NextDouble() < MUTATION_RATE)
                chromosome.TimeOfDayWeight = MutateValue(chromosome.TimeOfDayWeight, 0.5, 1.5);
                
            if (_random.NextDouble() < MUTATION_RATE)
                chromosome.MarketRegimeWeight = MutateValue(chromosome.MarketRegimeWeight, 0.8, 1.3);
                
            if (_random.NextDouble() < MUTATION_RATE)
                chromosome.VolatilityWeight = MutateValue(chromosome.VolatilityWeight, 0.7, 1.4);
                
            if (_random.NextDouble() < MUTATION_RATE)
                chromosome.MomentumWeight = MutateValue(chromosome.MomentumWeight, 0.6, 1.2);
        }
        
        private double MutateValue(double value, double min, double max)
        {
            var mutationStrength = 0.1; // 10% mutation
            var range = max - min;
            var mutation = (_random.NextDouble() - 0.5) * range * mutationStrength;
            return Math.Max(min, Math.Min(max, value + mutation));
        }
        
        private MarketConditions SimulateMarketConditions(DateTime tradeTime)
        {
            // Simplified market condition simulation for 2018-2019
            var dayOfYear = tradeTime.DayOfYear;
            var hour = tradeTime.Hour;
            
            // Simulate VIX based on historical patterns
            var baseVIX = 20.0;
            if (tradeTime.Year == 2018 && tradeTime.Month == 2) baseVIX = 35.0; // Feb 2018 spike
            if (tradeTime.Year == 2018 && tradeTime.Month == 10) baseVIX = 30.0; // Oct 2018 correction
            if (tradeTime.Year == 2019 && tradeTime.Month >= 5 && tradeTime.Month <= 8) baseVIX = 25.0; // Trade war
            
            var vixNoise = (_random.NextDouble() - 0.5) * 10.0;
            var vix = Math.Max(10, Math.Min(60, baseVIX + vixNoise));
            
            // Simulate trend and regime
            var trend = (_random.NextDouble() - 0.5) * 1.5;
            var regime = vix > 30 ? "Volatile" : vix > 20 ? "Mixed" : "Calm";
            
            return new MarketConditions
            {
                Date = tradeTime,
                UnderlyingPrice = 280.0 + (_random.NextDouble() - 0.5) * 40.0, // SPY-like prices
                VIX = vix,
                TrendScore = trend,
                MarketRegime = regime,
                DaysToExpiry = 0,
                IVRank = vix / 60.0
            };
        }
        
        private decimal CalculateCurrentDrawdown(List<TradeResult> trades)
        {
            if (!trades.Any()) return 0m;
            
            decimal peak = 0m;
            decimal maxDrawdown = 0m;
            decimal cumulative = 0m;
            
            foreach (var trade in trades.OrderBy(t => t.ExecutionTime))
            {
                cumulative += trade.PnL;
                peak = Math.Max(peak, cumulative);
                var drawdown = peak - cumulative;
                maxDrawdown = Math.Max(maxDrawdown, drawdown);
            }
            
            return maxDrawdown;
        }
        
        private decimal CalculateMaxDrawdown(List<TradeResult> trades) => CalculateCurrentDrawdown(trades);
        
        private double CalculateSharpeRatio(List<decimal> dailyReturns)
        {
            if (dailyReturns.Count < 2) return 0.0;
            
            var mean = (double)dailyReturns.Average();
            var stdDev = Math.Sqrt(dailyReturns.Sum(r => Math.Pow((double)r - mean, 2)) / (dailyReturns.Count - 1));
            
            return stdDev > 0 ? mean / stdDev * Math.Sqrt(252) : 0.0; // Annualized
        }
        
        private double CalculateProfitFactor(List<TradeResult> trades)
        {
            var grossProfit = trades.Where(t => t.PnL > 0).Sum(t => t.PnL);
            var grossLoss = Math.Abs(trades.Where(t => t.PnL < 0).Sum(t => t.PnL));
            
            return grossLoss > 0 ? (double)(grossProfit / grossLoss) : 0.0;
        }
        
        private double CalculateExecutionRate(DateTime start, DateTime end, int totalTrades)
        {
            var tradingDays = GetTradingDays(start, end);
            var maxPossibleTrades = tradingDays * 7; // ~7 opportunities per day
            return maxPossibleTrades > 0 ? totalTrades / (double)maxPossibleTrades : 0.0;
        }
        
        private int GetTradingDays(DateTime start, DateTime end)
        {
            var days = 0;
            var current = start.Date;
            while (current <= end.Date)
            {
                if (current.DayOfWeek >= DayOfWeek.Monday && current.DayOfWeek <= DayOfWeek.Friday)
                    days++;
                current = current.AddDays(1);
            }
            return days;
        }
        
        private double RandomInRange(double min, double max)
        {
            return min + (_random.NextDouble() * (max - min));
        }
    }
}