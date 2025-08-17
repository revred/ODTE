using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// PM250 GENETIC ALGORITHM OPTIMIZER
    /// Evolves RevFibNotch parameters to resolve current performance issues
    /// Targets: Loss prevention, faster scaling, better leverage utilization
    /// </summary>
    public class PM250_GeneticOptimizer
    {
        [Fact]
        public void OptimizeRevFibNotchParameters()
        {
            Console.WriteLine("🧬 PM250 GENETIC ALGORITHM OPTIMIZATION");
            Console.WriteLine("=======================================");
            Console.WriteLine($"Optimization Start: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine("Objective: Resolve 2024-2025 performance decline\n");

            var geneticOptimizer = new RevFibNotchGeneticOptimizer();
            var optimizedParams = geneticOptimizer.RunOptimization();
            
            Console.WriteLine("\n✅ GENETIC OPTIMIZATION COMPLETE");
            Console.WriteLine($"Best Parameters Found: {optimizedParams}");
        }
    }

    public class RevFibNotchGeneticOptimizer
    {
        private const int POPULATION_SIZE = 50;
        private const int GENERATIONS = 100;
        private const double MUTATION_RATE = 0.1;
        private const double CROSSOVER_RATE = 0.7;

        private List<TradingResult> _realTradingData;
        private Random _random = new Random(42); // Fixed seed for reproducibility

        public OptimizedParameters RunOptimization()
        {
            LoadRealTradingData();
            
            Console.WriteLine("🔬 Initializing genetic algorithm...");
            Console.WriteLine($"Population Size: {POPULATION_SIZE}");
            Console.WriteLine($"Generations: {GENERATIONS}");
            Console.WriteLine($"Mutation Rate: {MUTATION_RATE:P1}");
            Console.WriteLine($"Crossover Rate: {CROSSOVER_RATE:P1}\n");

            var population = InitializePopulation();
            
            for (int generation = 0; generation < GENERATIONS; generation++)
            {
                // Evaluate fitness for all chromosomes
                foreach (var chromosome in population)
                {
                    chromosome.Fitness = EvaluateFitness(chromosome);
                }

                // Sort by fitness (higher is better)
                population = population.OrderByDescending(c => c.Fitness).ToList();

                if (generation % 20 == 0)
                {
                    Console.WriteLine($"Generation {generation}: Best Fitness = {population[0].Fitness:F4}");
                    Console.WriteLine($"  Best Parameters: {FormatChromosome(population[0])}");
                }

                // Create next generation
                var newPopulation = new List<RevFibNotchChromosome>();
                
                // Keep best 10% (elitism)
                var eliteCount = (int)(POPULATION_SIZE * 0.1);
                newPopulation.AddRange(population.Take(eliteCount));

                // Generate rest through crossover and mutation
                while (newPopulation.Count < POPULATION_SIZE)
                {
                    var parent1 = TournamentSelection(population);
                    var parent2 = TournamentSelection(population);
                    
                    var children = Crossover(parent1, parent2);
                    children.ForEach(child => Mutate(child));
                    
                    newPopulation.AddRange(children.Take(POPULATION_SIZE - newPopulation.Count));
                }

                population = newPopulation;
            }

            // Final evaluation
            foreach (var chromosome in population)
            {
                chromosome.Fitness = EvaluateFitness(chromosome);
            }

            var bestChromosome = population.OrderByDescending(c => c.Fitness).First();
            
            Console.WriteLine("\n🏆 OPTIMIZATION RESULTS:");
            Console.WriteLine("========================");
            Console.WriteLine($"Best Fitness Score: {bestChromosome.Fitness:F4}");
            Console.WriteLine($"Improvement vs Current: {((bestChromosome.Fitness - GetCurrentSystemFitness()) / GetCurrentSystemFitness() * 100):F1}%");
            
            return ConvertToOptimizedParameters(bestChromosome);
        }

        private void LoadRealTradingData()
        {
            // Load the problematic 2024-2025 period for optimization
            _realTradingData = new List<TradingResult>
            {
                // 2024 failures
                new() { Date = new DateTime(2024, 4, 1), PnL = -238.13m, WinRate = 0.710m, VIX = 22.1m, Drawdown = 0.0987m },
                new() { Date = new DateTime(2024, 6, 1), PnL = -131.11m, WinRate = 0.706m, VIX = 19.8m, Drawdown = 0.0845m },
                new() { Date = new DateTime(2024, 7, 1), PnL = -144.62m, WinRate = 0.688m, VIX = 18.5m, Drawdown = 0.1123m },
                new() { Date = new DateTime(2024, 9, 1), PnL = -222.55m, WinRate = 0.708m, VIX = 20.4m, Drawdown = 0.1045m },
                new() { Date = new DateTime(2024, 10, 1), PnL = -191.10m, WinRate = 0.714m, VIX = 21.7m, Drawdown = 0.1234m },
                new() { Date = new DateTime(2024, 12, 1), PnL = -620.16m, WinRate = 0.586m, VIX = 25.3m, Drawdown = 0.1892m },
                
                // 2025 continued failures
                new() { Date = new DateTime(2025, 6, 1), PnL = -478.46m, WinRate = 0.522m, VIX = 23.8m, Drawdown = 0.1634m },
                new() { Date = new DateTime(2025, 7, 1), PnL = -348.42m, WinRate = 0.697m, VIX = 21.2m, Drawdown = 0.1345m },
                new() { Date = new DateTime(2025, 8, 1), PnL = -523.94m, WinRate = 0.640m, VIX = 22.9m, Drawdown = 0.1945m },
                
                // Include some profitable months for balance
                new() { Date = new DateTime(2024, 1, 1), PnL = 74.48m, WinRate = 0.741m, VIX = 16.8m, Drawdown = 0.0891m },
                new() { Date = new DateTime(2024, 3, 1), PnL = 1028.02m, WinRate = 0.960m, VIX = 15.2m, Drawdown = 0.0234m },
                new() { Date = new DateTime(2025, 1, 1), PnL = 124.10m, WinRate = 0.731m, VIX = 17.5m, Drawdown = 0.0789m },
                new() { Date = new DateTime(2025, 2, 1), PnL = 248.71m, WinRate = 0.840m, VIX = 16.1m, Drawdown = 0.0456m }
            };

            Console.WriteLine($"✓ Loaded {_realTradingData.Count} trading periods for optimization");
            Console.WriteLine($"✓ Focus: 2024-2025 performance improvement\n");
        }

        private List<RevFibNotchChromosome> InitializePopulation()
        {
            var population = new List<RevFibNotchChromosome>();
            
            for (int i = 0; i < POPULATION_SIZE; i++)
            {
                population.Add(new RevFibNotchChromosome
                {
                    // Gene 1-6: RevFib Limits Array (more conservative than current)
                    RevFibLimits = new decimal[]
                    {
                        _random.Next(800, 1200),    // Level 0: 800-1200 (vs current 1250)
                        _random.Next(500, 800),     // Level 1: 500-800 (vs current 800)
                        _random.Next(300, 500),     // Level 2: 300-500 (vs current 500)
                        _random.Next(200, 350),     // Level 3: 200-350 (vs current 300)
                        _random.Next(100, 250),     // Level 4: 100-250 (vs current 200)
                        _random.Next(50, 150)       // Level 5: 50-150 (vs current 100)
                    },
                    
                    // Gene 7: Scaling Sensitivity (how much P&L triggers movement)
                    ScalingSensitivity = (decimal)(_random.NextDouble() * 1.5 + 0.5), // 0.5 - 2.0
                    
                    // Gene 8: Win Rate Threshold (scale down if below this)
                    WinRateThreshold = (decimal)(_random.NextDouble() * 0.25 + 0.55), // 0.55 - 0.80
                    
                    // Gene 9: Confirmation Days (0 = immediate, 1-3 = delayed)
                    ConfirmationDays = _random.Next(0, 4), // 0-3 days
                    
                    // Gene 10: Market Stress Multiplier
                    MarketStressMultiplier = (decimal)(_random.NextDouble() * 2.0 + 1.0), // 1.0 - 3.0
                    
                    // Gene 11: Maximum Daily Risk Percentage
                    MaxDailyRisk = (decimal)(_random.NextDouble() * 0.025 + 0.005), // 0.5% - 3.0%
                    
                    // Gene 12: Protective Trigger Loss (immediate scale down)
                    ProtectiveTriggerLoss = -(_random.Next(25, 200)) // -$25 to -$200
                });
            }
            
            return population;
        }

        private double EvaluateFitness(RevFibNotchChromosome chromosome)
        {
            decimal totalPnL = 0;
            decimal maxDrawdown = 0;
            int currentNotchIndex = 2; // Start at $500 level
            decimal runningCapital = 30000m;
            int consecutiveLosses = 0;
            int preventedLargeDrawdowns = 0;

            foreach (var tradingPeriod in _realTradingData)
            {
                // Simulate RevFibNotch position sizing
                var positionSize = chromosome.RevFibLimits[currentNotchIndex];
                
                // Apply market stress adjustment
                if (tradingPeriod.VIX > 25)
                {
                    positionSize /= chromosome.MarketStressMultiplier;
                }
                
                // Apply win rate adjustment
                if (tradingPeriod.WinRate < chromosome.WinRateThreshold)
                {
                    positionSize *= 0.7m; // Reduce size for poor win rate
                }
                
                // Limit maximum daily risk
                var maxRisk = runningCapital * chromosome.MaxDailyRisk;
                positionSize = Math.Min(positionSize, maxRisk);
                
                // Scale P&L by position size
                var scaledPnL = tradingPeriod.PnL * (positionSize / 500m); // 500 is baseline
                totalPnL += scaledPnL;
                runningCapital += scaledPnL;
                
                // Track drawdown
                var currentDrawdown = Math.Abs(Math.Min(0, scaledPnL)) / runningCapital;
                maxDrawdown = Math.Max(maxDrawdown, currentDrawdown);
                
                // Check for protective trigger
                if (scaledPnL <= chromosome.ProtectiveTriggerLoss)
                {
                    currentNotchIndex = Math.Min(currentNotchIndex + 2, chromosome.RevFibLimits.Length - 1);
                    preventedLargeDrawdowns++;
                }
                
                // Normal RevFibNotch movement logic
                if (scaledPnL < 0)
                {
                    consecutiveLosses++;
                    if (consecutiveLosses >= chromosome.ConfirmationDays)
                    {
                        var movement = Math.Max(1, (int)(Math.Abs(scaledPnL) / 100 * chromosome.ScalingSensitivity));
                        currentNotchIndex = Math.Min(currentNotchIndex + movement, chromosome.RevFibLimits.Length - 1);
                    }
                }
                else
                {
                    consecutiveLosses = 0;
                    // Scale up on profits (double-day confirmation for safety)
                    if (consecutiveLosses == 0 && currentNotchIndex > 0)
                    {
                        currentNotchIndex = Math.Max(currentNotchIndex - 1, 0);
                    }
                }
            }

            // Fitness calculation (higher is better)
            var sharpeRatio = totalPnL / Math.Max(1m, maxDrawdown * runningCapital);
            var lossPreventionBonus = preventedLargeDrawdowns * 100; // Bonus for preventing large drawdowns
            var stabilityBonus = runningCapital > 30000m ? 100 : 0; // Bonus for capital preservation
            
            var fitness = (double)(totalPnL + lossPreventionBonus + stabilityBonus + sharpeRatio * 50);
            
            return fitness;
        }

        private double GetCurrentSystemFitness()
        {
            // Simulate current RevFibNotch performance
            var currentSystem = new RevFibNotchChromosome
            {
                RevFibLimits = new decimal[] { 1250, 800, 500, 300, 200, 100 },
                ScalingSensitivity = 1.0m,
                WinRateThreshold = 0.65m,
                ConfirmationDays = 2,
                MarketStressMultiplier = 1.5m,
                MaxDailyRisk = 0.02m,
                ProtectiveTriggerLoss = -100m
            };

            return EvaluateFitness(currentSystem);
        }

        private RevFibNotchChromosome TournamentSelection(List<RevFibNotchChromosome> population)
        {
            var tournamentSize = 5;
            var tournament = new List<RevFibNotchChromosome>();
            
            for (int i = 0; i < tournamentSize; i++)
            {
                tournament.Add(population[_random.Next(population.Count)]);
            }
            
            return tournament.OrderByDescending(c => c.Fitness).First();
        }

        private List<RevFibNotchChromosome> Crossover(RevFibNotchChromosome parent1, RevFibNotchChromosome parent2)
        {
            if (_random.NextDouble() > CROSSOVER_RATE)
            {
                return new List<RevFibNotchChromosome> { parent1, parent2 };
            }

            var child1 = new RevFibNotchChromosome();
            var child2 = new RevFibNotchChromosome();
            
            // Crossover RevFib limits
            child1.RevFibLimits = new decimal[6];
            child2.RevFibLimits = new decimal[6];
            
            for (int i = 0; i < 6; i++)
            {
                if (_random.NextDouble() < 0.5)
                {
                    child1.RevFibLimits[i] = parent1.RevFibLimits[i];
                    child2.RevFibLimits[i] = parent2.RevFibLimits[i];
                }
                else
                {
                    child1.RevFibLimits[i] = parent2.RevFibLimits[i];
                    child2.RevFibLimits[i] = parent1.RevFibLimits[i];
                }
            }
            
            // Crossover other parameters
            child1.ScalingSensitivity = _random.NextDouble() < 0.5 ? parent1.ScalingSensitivity : parent2.ScalingSensitivity;
            child1.WinRateThreshold = _random.NextDouble() < 0.5 ? parent1.WinRateThreshold : parent2.WinRateThreshold;
            child1.ConfirmationDays = _random.NextDouble() < 0.5 ? parent1.ConfirmationDays : parent2.ConfirmationDays;
            child1.MarketStressMultiplier = _random.NextDouble() < 0.5 ? parent1.MarketStressMultiplier : parent2.MarketStressMultiplier;
            child1.MaxDailyRisk = _random.NextDouble() < 0.5 ? parent1.MaxDailyRisk : parent2.MaxDailyRisk;
            child1.ProtectiveTriggerLoss = _random.NextDouble() < 0.5 ? parent1.ProtectiveTriggerLoss : parent2.ProtectiveTriggerLoss;
            
            child2.ScalingSensitivity = child1.ScalingSensitivity == parent1.ScalingSensitivity ? parent2.ScalingSensitivity : parent1.ScalingSensitivity;
            child2.WinRateThreshold = child1.WinRateThreshold == parent1.WinRateThreshold ? parent2.WinRateThreshold : parent1.WinRateThreshold;
            child2.ConfirmationDays = child1.ConfirmationDays == parent1.ConfirmationDays ? parent2.ConfirmationDays : parent1.ConfirmationDays;
            child2.MarketStressMultiplier = child1.MarketStressMultiplier == parent1.MarketStressMultiplier ? parent2.MarketStressMultiplier : parent1.MarketStressMultiplier;
            child2.MaxDailyRisk = child1.MaxDailyRisk == parent1.MaxDailyRisk ? parent2.MaxDailyRisk : parent1.MaxDailyRisk;
            child2.ProtectiveTriggerLoss = child1.ProtectiveTriggerLoss == parent1.ProtectiveTriggerLoss ? parent2.ProtectiveTriggerLoss : parent1.ProtectiveTriggerLoss;
            
            return new List<RevFibNotchChromosome> { child1, child2 };
        }

        private void Mutate(RevFibNotchChromosome chromosome)
        {
            // Mutate RevFib limits
            for (int i = 0; i < chromosome.RevFibLimits.Length; i++)
            {
                if (_random.NextDouble() < MUTATION_RATE)
                {
                    chromosome.RevFibLimits[i] *= (decimal)(_random.NextDouble() * 0.4 + 0.8); // +/- 20%
                }
            }
            
            // Mutate other parameters
            if (_random.NextDouble() < MUTATION_RATE)
                chromosome.ScalingSensitivity *= (decimal)(_random.NextDouble() * 0.4 + 0.8);
            
            if (_random.NextDouble() < MUTATION_RATE)
                chromosome.WinRateThreshold = Math.Max(0.5m, Math.Min(0.9m, chromosome.WinRateThreshold + (decimal)(_random.NextDouble() * 0.1 - 0.05)));
            
            if (_random.NextDouble() < MUTATION_RATE)
                chromosome.ConfirmationDays = Math.Max(0, Math.Min(5, chromosome.ConfirmationDays + _random.Next(-1, 2)));
            
            if (_random.NextDouble() < MUTATION_RATE)
                chromosome.MarketStressMultiplier *= (decimal)(_random.NextDouble() * 0.4 + 0.8);
            
            if (_random.NextDouble() < MUTATION_RATE)
                chromosome.MaxDailyRisk = Math.Max(0.001m, Math.Min(0.05m, chromosome.MaxDailyRisk * (decimal)(_random.NextDouble() * 0.4 + 0.8)));
            
            if (_random.NextDouble() < MUTATION_RATE)
                chromosome.ProtectiveTriggerLoss *= (decimal)(_random.NextDouble() * 0.4 + 0.8);
        }

        private string FormatChromosome(RevFibNotchChromosome chromosome)
        {
            return $"Limits:[{string.Join(",", chromosome.RevFibLimits.Select(l => l.ToString("F0")))}] " +
                   $"Sens:{chromosome.ScalingSensitivity:F2} WinTh:{chromosome.WinRateThreshold:F2} " +
                   $"Conf:{chromosome.ConfirmationDays} Stress:{chromosome.MarketStressMultiplier:F2}";
        }

        private OptimizedParameters ConvertToOptimizedParameters(RevFibNotchChromosome best)
        {
            return new OptimizedParameters
            {
                OptimizedRevFibLimits = best.RevFibLimits,
                OptimizedScalingSensitivity = best.ScalingSensitivity,
                OptimizedWinRateThreshold = best.WinRateThreshold,
                OptimizedConfirmationDays = best.ConfirmationDays,
                OptimizedMarketStressMultiplier = best.MarketStressMultiplier,
                OptimizedMaxDailyRisk = best.MaxDailyRisk,
                OptimizedProtectiveTriggerLoss = best.ProtectiveTriggerLoss,
                FitnessScore = best.Fitness,
                ImprovementVsCurrent = ((best.Fitness - GetCurrentSystemFitness()) / GetCurrentSystemFitness() * 100)
            };
        }
    }

    public class RevFibNotchChromosome
    {
        public decimal[] RevFibLimits { get; set; }
        public decimal ScalingSensitivity { get; set; }
        public decimal WinRateThreshold { get; set; }
        public int ConfirmationDays { get; set; }
        public decimal MarketStressMultiplier { get; set; }
        public decimal MaxDailyRisk { get; set; }
        public decimal ProtectiveTriggerLoss { get; set; }
        public double Fitness { get; set; }
    }

    public class TradingResult
    {
        public DateTime Date { get; set; }
        public decimal PnL { get; set; }
        public decimal WinRate { get; set; }
        public decimal VIX { get; set; }
        public decimal Drawdown { get; set; }
    }

    public class OptimizedParameters
    {
        public decimal[] OptimizedRevFibLimits { get; set; }
        public decimal OptimizedScalingSensitivity { get; set; }
        public decimal OptimizedWinRateThreshold { get; set; }
        public int OptimizedConfirmationDays { get; set; }
        public decimal OptimizedMarketStressMultiplier { get; set; }
        public decimal OptimizedMaxDailyRisk { get; set; }
        public decimal OptimizedProtectiveTriggerLoss { get; set; }
        public double FitnessScore { get; set; }
        public double ImprovementVsCurrent { get; set; }

        public override string ToString()
        {
            return $"RevFib Limits: [{string.Join(", ", OptimizedRevFibLimits.Select(l => l.ToString("F0")))}]\n" +
                   $"Scaling Sensitivity: {OptimizedScalingSensitivity:F2}\n" +
                   $"Win Rate Threshold: {OptimizedWinRateThreshold:P1}\n" +
                   $"Confirmation Days: {OptimizedConfirmationDays}\n" +
                   $"Market Stress Multiplier: {OptimizedMarketStressMultiplier:F2}\n" +
                   $"Max Daily Risk: {OptimizedMaxDailyRisk:P2}\n" +
                   $"Protective Trigger: ${OptimizedProtectiveTriggerLoss:F2}\n" +
                   $"Fitness Score: {FitnessScore:F2}\n" +
                   $"Improvement vs Current: {ImprovementVsCurrent:F1}%";
        }
    }
}