using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PM250RadicalOptimizer
{
    /// <summary>
    /// PM250 RADICAL GENETIC ALGORITHM BREAKTHROUGH OPTIMIZER
    /// 100 generations with radical mutations to discover outlier configurations with exceptional potential
    /// Extreme parameter exploration beyond conservative bounds for breakthrough discovery
    /// </summary>
    public class PM250_Radical_Genetic_Breakthrough
    {
        private const int POPULATION_SIZE = 200;
        private const int GENERATIONS = 100;
        private const decimal MUTATION_RATE = 0.35m; // RADICAL: 35% mutation rate
        private const decimal RADICAL_MUTATION_CHANCE = 0.20m; // 20% chance of radical mutation
        private const decimal CURRENT_BEST_FITNESS = 56.12m; // Breakthrough threshold
        
        private Random _random;
        private List<RadicalChromosome> _population;
        private List<RadicalChromosome> _breakthroughCandidates;
        private decimal _bestFitness;
        private RadicalChromosome _bestChromosome;

        public static void Main(string[] args)
        {
            Console.WriteLine("🧬 PM250 RADICAL GENETIC BREAKTHROUGH OPTIMIZER");
            Console.WriteLine("===============================================");
            Console.WriteLine($"Radical Evolution Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"Population Size: {POPULATION_SIZE}");
            Console.WriteLine($"Generations: {GENERATIONS}");
            Console.WriteLine($"Mutation Rate: {MUTATION_RATE:P0} (RADICAL)");
            Console.WriteLine($"Breakthrough Threshold: {CURRENT_BEST_FITNESS:F2}");
            Console.WriteLine();
            
            var optimizer = new PM250_Radical_Genetic_Breakthrough();
            optimizer.RunRadicalOptimization();
        }

        public void RunRadicalOptimization()
        {
            _random = new Random(DateTime.Now.Millisecond);
            _breakthroughCandidates = new List<RadicalChromosome>();
            _bestFitness = 0m;
            
            InitializeRadicalPopulation();
            
            Console.WriteLine("🚀 STARTING RADICAL GENETIC EVOLUTION");
            Console.WriteLine("====================================");
            
            for (int generation = 1; generation <= GENERATIONS; generation++)
            {
                EvaluatePopulationFitness();
                DisplayGenerationProgress(generation);
                DetectBreakthroughCandidates(generation);
                
                if (generation < GENERATIONS)
                {
                    var nextGeneration = CreateNextGenerationRadical();
                    _population = nextGeneration;
                }
            }
            
            AnalyzeRadicalResults();
            ExportBreakthroughCandidates();
            
            Console.WriteLine("\n🏆 RADICAL OPTIMIZATION COMPLETE");
            Console.WriteLine("=================================");
            DisplayFinalBreakthroughResults();
        }

        private void InitializeRadicalPopulation()
        {
            Console.WriteLine("🎯 Initializing radical population with extreme parameter exploration...");
            
            _population = new List<RadicalChromosome>();
            
            for (int i = 0; i < POPULATION_SIZE; i++)
            {
                var chromosome = new RadicalChromosome
                {
                    Id = Guid.NewGuid(),
                    Generation = 0,
                    
                    // Core RevFibNotch parameters with EXTREME ranges
                    RFibLimit1 = GenerateRadicalDecimal(800m, 2000m), // Extreme high limit
                    RFibLimit2 = GenerateRadicalDecimal(300m, 800m),
                    RFibLimit3 = GenerateRadicalDecimal(150m, 500m),
                    RFibLimit4 = GenerateRadicalDecimal(75m, 300m),
                    RFibLimit5 = GenerateRadicalDecimal(25m, 150m),
                    RFibLimit6 = GenerateRadicalDecimal(10m, 75m),
                    
                    // Enhanced thresholds with extreme ranges
                    WinRateThreshold = GenerateRadicalDecimal(0.55m, 0.85m), // 55% to 85%
                    ProtectiveTriggerLoss = GenerateRadicalDecimal(-150m, -25m), // Extreme protection
                    ScalingSensitivity = GenerateRadicalDecimal(0.5m, 4.0m), // 0.5x to 4.0x
                    
                    // Advanced reaction parameters (NEW)
                    MovementAgility = GenerateRadicalDecimal(0.8m, 3.0m), // Extreme agility
                    LossReactionSpeed = GenerateRadicalDecimal(0.5m, 3.5m), // Ultra-fast loss reaction
                    ProfitReactionSpeed = GenerateRadicalDecimal(0.5m, 2.5m), // Controlled profit reaction
                    
                    // Market regime multipliers with extreme ranges
                    CrisisMultiplier = GenerateRadicalDecimal(0.10m, 0.60m), // 10% to 60% in crisis
                    VolatileMultiplier = GenerateRadicalDecimal(0.60m, 1.20m), // 60% to 120% in volatile
                    BullMultiplier = GenerateRadicalDecimal(0.90m, 1.30m), // 90% to 130% in bull
                    
                    // NEW: Revolutionary parameters for breakthrough discovery
                    CrisisRecoverySpeed = GenerateRadicalDecimal(0.3m, 2.5m), // Crisis recovery agility
                    VolatilityAdaptation = GenerateRadicalDecimal(0.4m, 2.0m), // Volatility response
                    TrendFollowingStrength = GenerateRadicalDecimal(0.2m, 1.8m), // Trend following power
                    MeanReversionBias = GenerateRadicalDecimal(0.1m, 1.5m), // Mean reversion tendency
                    SeasonalityWeight = GenerateRadicalDecimal(0.0m, 1.2m), // Seasonal adjustment
                    CorrelationSensitivity = GenerateRadicalDecimal(0.0m, 1.0m), // Market correlation response
                    InnovationFactor = GenerateRadicalDecimal(0.0m, 0.5m), // Innovation bonus
                    
                    // Multi-objective weights for radical exploration
                    CapitalPreservationWeight = GenerateRadicalDecimal(0.20m, 0.80m),
                    SharpeRatioWeight = GenerateRadicalDecimal(0.10m, 0.50m),
                    ConsistencyWeight = GenerateRadicalDecimal(0.05m, 0.40m),
                    DrawdownProtectionWeight = GenerateRadicalDecimal(0.15m, 0.60m)
                };
                
                // Innovation bonus for extreme configurations
                if (IsExtremeConfiguration(chromosome))
                {
                    chromosome.InnovationBonus = GenerateRadicalDecimal(0.0m, 2.0m);
                    chromosome.IsExtremeConfig = true;
                }
                
                _population.Add(chromosome);
            }
            
            Console.WriteLine($"✓ Generated {POPULATION_SIZE} radical chromosomes with extreme parameter ranges");
        }

        private bool IsExtremeConfiguration(RadicalChromosome chromosome)
        {
            // Detect configurations that push boundaries
            return chromosome.ScalingSensitivity > 3.0m ||
                   chromosome.CrisisMultiplier < 0.20m ||
                   chromosome.MovementAgility > 2.5m ||
                   chromosome.LossReactionSpeed > 3.0m ||
                   chromosome.TrendFollowingStrength > 1.5m;
        }

        private decimal GenerateRadicalDecimal(decimal min, decimal max)
        {
            var range = max - min;
            var randomValue = (decimal)_random.NextDouble();
            return min + (randomValue * range);
        }

        private void EvaluatePopulationFitness()
        {
            foreach (var chromosome in _population)
            {
                chromosome.Fitness = CalculateRadicalFitness(chromosome);
            }
            
            // Track best performer
            var generationBest = _population.OrderByDescending(c => c.Fitness).First();
            if (generationBest.Fitness > _bestFitness)
            {
                _bestFitness = generationBest.Fitness;
                _bestChromosome = generationBest.Clone();
            }
        }

        private decimal CalculateRadicalFitness(RadicalChromosome chromosome)
        {
            // Multi-objective fitness with innovation bonuses
            decimal baseFitness = 0m;
            
            // Core performance metrics (70% of fitness)
            decimal capitalPreservation = EvaluateCapitalPreservation(chromosome) * chromosome.CapitalPreservationWeight;
            decimal sharpeRatio = EvaluateSharpeRatio(chromosome) * chromosome.SharpeRatioWeight;
            decimal consistency = EvaluateConsistency(chromosome) * chromosome.ConsistencyWeight;
            decimal drawdownProtection = EvaluateDrawdownProtection(chromosome) * chromosome.DrawdownProtectionWeight;
            
            baseFitness = (capitalPreservation + sharpeRatio + consistency + drawdownProtection) * 0.70m;
            
            // Innovation metrics (20% of fitness)
            decimal innovationScore = EvaluateInnovationPotential(chromosome) * 0.20m;
            
            // Extreme configuration bonus (10% of fitness)
            decimal extremeBonus = chromosome.IsExtremeConfig ? chromosome.InnovationBonus * 0.10m : 0m;
            
            // Crisis survival bonus
            decimal crisisSurvival = EvaluateCrisisSurvival(chromosome) * 0.15m;
            
            // Volatility adaptation bonus
            decimal volatilityAdaptation = EvaluateVolatilityAdaptation(chromosome) * 0.10m;
            
            var totalFitness = baseFitness + innovationScore + extremeBonus + crisisSurvival + volatilityAdaptation;
            
            // Breakthrough detection: Add significant bonus for configurations exceeding current best
            if (totalFitness > CURRENT_BEST_FITNESS)
            {
                totalFitness += (totalFitness - CURRENT_BEST_FITNESS) * 2.0m; // 200% breakthrough bonus
                chromosome.IsBreakthrough = true;
            }
            
            return Math.Max(0m, Math.Min(100m, totalFitness));
        }

        private decimal EvaluateCapitalPreservation(RadicalChromosome chromosome)
        {
            // Evaluate how well this configuration preserves capital during losses
            var protectionScore = 40m;
            
            // Faster protection trigger = better preservation
            if (chromosome.ProtectiveTriggerLoss > -50m) protectionScore += 15m;
            if (chromosome.ProtectiveTriggerLoss > -75m) protectionScore += 10m;
            
            // Higher scaling sensitivity = faster reaction
            if (chromosome.ScalingSensitivity > 2.0m) protectionScore += 10m;
            if (chromosome.ScalingSensitivity > 2.5m) protectionScore += 5m;
            
            // Crisis protection multiplier
            if (chromosome.CrisisMultiplier < 0.35m) protectionScore += 15m;
            if (chromosome.CrisisMultiplier < 0.25m) protectionScore += 10m;
            
            return Math.Min(80m, protectionScore);
        }

        private decimal EvaluateSharpeRatio(RadicalChromosome chromosome)
        {
            // Estimate Sharpe ratio based on parameter balance
            var sharpeScore = 30m;
            
            // Balanced win rate threshold
            if (chromosome.WinRateThreshold >= 0.68m && chromosome.WinRateThreshold <= 0.75m) sharpeScore += 15m;
            
            // Optimal scaling sensitivity
            if (chromosome.ScalingSensitivity >= 1.8m && chromosome.ScalingSensitivity <= 2.8m) sharpeScore += 10m;
            
            // Movement agility balance
            if (chromosome.MovementAgility >= 1.5m && chromosome.MovementAgility <= 2.2m) sharpeScore += 10m;
            
            return Math.Min(65m, sharpeScore);
        }

        private decimal EvaluateConsistency(RadicalChromosome chromosome)
        {
            // Evaluate parameter stability and consistency
            var consistencyScore = 25m;
            
            // Stable reaction speeds
            var reactionBalance = Math.Abs(chromosome.LossReactionSpeed - chromosome.ProfitReactionSpeed);
            if (reactionBalance < 0.5m) consistencyScore += 15m;
            else if (reactionBalance < 1.0m) consistencyScore += 10m;
            
            // Balanced regime multipliers
            var regimeRange = chromosome.BullMultiplier - chromosome.CrisisMultiplier;
            if (regimeRange >= 0.5m && regimeRange <= 1.0m) consistencyScore += 10m;
            
            return Math.Min(50m, consistencyScore);
        }

        private decimal EvaluateDrawdownProtection(RadicalChromosome chromosome)
        {
            // Evaluate maximum drawdown protection capabilities
            var protectionScore = 20m;
            
            // Ultra-fast loss reaction
            if (chromosome.LossReactionSpeed > 2.5m) protectionScore += 20m;
            if (chromosome.LossReactionSpeed > 3.0m) protectionScore += 10m;
            
            // Crisis recovery capabilities
            if (chromosome.CrisisRecoverySpeed > 1.5m) protectionScore += 15m;
            if (chromosome.CrisisRecoverySpeed > 2.0m) protectionScore += 10m;
            
            return Math.Min(75m, protectionScore);
        }

        private decimal EvaluateInnovationPotential(RadicalChromosome chromosome)
        {
            // Reward truly innovative parameter combinations
            var innovationScore = 10m;
            
            // Revolutionary trend following
            if (chromosome.TrendFollowingStrength > 1.4m) innovationScore += 15m;
            
            // Advanced volatility adaptation
            if (chromosome.VolatilityAdaptation > 1.5m) innovationScore += 10m;
            
            // High correlation sensitivity
            if (chromosome.CorrelationSensitivity > 0.7m) innovationScore += 8m;
            
            // Seasonality integration
            if (chromosome.SeasonalityWeight > 0.8m) innovationScore += 7m;
            
            return Math.Min(50m, innovationScore);
        }

        private decimal EvaluateCrisisSurvival(RadicalChromosome chromosome)
        {
            // Evaluate crisis period survival capabilities
            var survivalScore = 5m;
            
            // Ultra-conservative crisis positioning
            if (chromosome.CrisisMultiplier < 0.20m) survivalScore += 25m;
            if (chromosome.CrisisMultiplier < 0.15m) survivalScore += 15m;
            
            // Fast crisis recovery
            if (chromosome.CrisisRecoverySpeed > 2.0m) survivalScore += 20m;
            
            return Math.Min(65m, survivalScore);
        }

        private decimal EvaluateVolatilityAdaptation(RadicalChromosome chromosome)
        {
            // Evaluate adaptation to different volatility regimes
            var adaptationScore = 8m;
            
            // Strong volatility response
            if (chromosome.VolatilityAdaptation > 1.3m) adaptationScore += 20m;
            
            // Balanced volatile market multiplier
            if (chromosome.VolatileMultiplier >= 0.8m && chromosome.VolatileMultiplier <= 1.0m) adaptationScore += 15m;
            
            return Math.Min(43m, adaptationScore);
        }

        private void DetectBreakthroughCandidates(int generation)
        {
            var breakthroughs = _population.Where(c => c.Fitness > CURRENT_BEST_FITNESS || c.IsBreakthrough).ToList();
            
            foreach (var breakthrough in breakthroughs)
            {
                breakthrough.Generation = generation;
                breakthrough.DiscoveryTime = DateTime.Now;
                _breakthroughCandidates.Add(breakthrough.Clone());
                
                Console.WriteLine($"🚀 BREAKTHROUGH DETECTED (Gen {generation}): Fitness {breakthrough.Fitness:F2}");
            }
        }

        private List<RadicalChromosome> CreateNextGenerationRadical()
        {
            var nextGeneration = new List<RadicalChromosome>();
            
            // Sort by fitness
            var sortedPopulation = _population.OrderByDescending(c => c.Fitness).ToList();
            
            // Elitism: Keep top 10% unchanged
            int eliteCount = (int)(POPULATION_SIZE * 0.10);
            for (int i = 0; i < eliteCount; i++)
            {
                nextGeneration.Add(sortedPopulation[i].Clone());
            }
            
            // Generate rest through radical breeding
            while (nextGeneration.Count < POPULATION_SIZE)
            {
                var parent1 = SelectParentByFitness(sortedPopulation);
                var parent2 = SelectParentByFitness(sortedPopulation);
                
                var offspring = RadicalCrossover(parent1, parent2);
                offspring = RadicalMutation(offspring);
                
                nextGeneration.Add(offspring);
            }
            
            return nextGeneration;
        }

        private RadicalChromosome SelectParentByFitness(List<RadicalChromosome> sortedPopulation)
        {
            // Tournament selection with bias toward high fitness
            int tournamentSize = 5;
            var tournament = new List<RadicalChromosome>();
            
            for (int i = 0; i < tournamentSize; i++)
            {
                int index = _random.Next(sortedPopulation.Count);
                tournament.Add(sortedPopulation[index]);
            }
            
            return tournament.OrderByDescending(c => c.Fitness).First();
        }

        private RadicalChromosome RadicalCrossover(RadicalChromosome parent1, RadicalChromosome parent2)
        {
            var offspring = new RadicalChromosome { Id = Guid.NewGuid() };
            
            // Radical crossover: mix parameters with 60% chance from better parent
            var betterParent = parent1.Fitness > parent2.Fitness ? parent1 : parent2;
            var worseParent = parent1.Fitness > parent2.Fitness ? parent2 : parent1;
            
            offspring.RFibLimit1 = _random.NextDouble() < 0.6 ? betterParent.RFibLimit1 : worseParent.RFibLimit1;
            offspring.RFibLimit2 = _random.NextDouble() < 0.6 ? betterParent.RFibLimit2 : worseParent.RFibLimit2;
            offspring.RFibLimit3 = _random.NextDouble() < 0.6 ? betterParent.RFibLimit3 : worseParent.RFibLimit3;
            offspring.RFibLimit4 = _random.NextDouble() < 0.6 ? betterParent.RFibLimit4 : worseParent.RFibLimit4;
            offspring.RFibLimit5 = _random.NextDouble() < 0.6 ? betterParent.RFibLimit5 : worseParent.RFibLimit5;
            offspring.RFibLimit6 = _random.NextDouble() < 0.6 ? betterParent.RFibLimit6 : worseParent.RFibLimit6;
            
            offspring.WinRateThreshold = _random.NextDouble() < 0.6 ? betterParent.WinRateThreshold : worseParent.WinRateThreshold;
            offspring.ProtectiveTriggerLoss = _random.NextDouble() < 0.6 ? betterParent.ProtectiveTriggerLoss : worseParent.ProtectiveTriggerLoss;
            offspring.ScalingSensitivity = _random.NextDouble() < 0.6 ? betterParent.ScalingSensitivity : worseParent.ScalingSensitivity;
            
            offspring.MovementAgility = _random.NextDouble() < 0.6 ? betterParent.MovementAgility : worseParent.MovementAgility;
            offspring.LossReactionSpeed = _random.NextDouble() < 0.6 ? betterParent.LossReactionSpeed : worseParent.LossReactionSpeed;
            offspring.ProfitReactionSpeed = _random.NextDouble() < 0.6 ? betterParent.ProfitReactionSpeed : worseParent.ProfitReactionSpeed;
            
            offspring.CrisisMultiplier = _random.NextDouble() < 0.6 ? betterParent.CrisisMultiplier : worseParent.CrisisMultiplier;
            offspring.VolatileMultiplier = _random.NextDouble() < 0.6 ? betterParent.VolatileMultiplier : worseParent.VolatileMultiplier;
            offspring.BullMultiplier = _random.NextDouble() < 0.6 ? betterParent.BullMultiplier : worseParent.BullMultiplier;
            
            // Revolutionary parameters
            offspring.CrisisRecoverySpeed = _random.NextDouble() < 0.6 ? betterParent.CrisisRecoverySpeed : worseParent.CrisisRecoverySpeed;
            offspring.VolatilityAdaptation = _random.NextDouble() < 0.6 ? betterParent.VolatilityAdaptation : worseParent.VolatilityAdaptation;
            offspring.TrendFollowingStrength = _random.NextDouble() < 0.6 ? betterParent.TrendFollowingStrength : worseParent.TrendFollowingStrength;
            offspring.MeanReversionBias = _random.NextDouble() < 0.6 ? betterParent.MeanReversionBias : worseParent.MeanReversionBias;
            offspring.SeasonalityWeight = _random.NextDouble() < 0.6 ? betterParent.SeasonalityWeight : worseParent.SeasonalityWeight;
            offspring.CorrelationSensitivity = _random.NextDouble() < 0.6 ? betterParent.CorrelationSensitivity : worseParent.CorrelationSensitivity;
            offspring.InnovationFactor = _random.NextDouble() < 0.6 ? betterParent.InnovationFactor : worseParent.InnovationFactor;
            
            // Multi-objective weights
            offspring.CapitalPreservationWeight = _random.NextDouble() < 0.6 ? betterParent.CapitalPreservationWeight : worseParent.CapitalPreservationWeight;
            offspring.SharpeRatioWeight = _random.NextDouble() < 0.6 ? betterParent.SharpeRatioWeight : worseParent.SharpeRatioWeight;
            offspring.ConsistencyWeight = _random.NextDouble() < 0.6 ? betterParent.ConsistencyWeight : worseParent.ConsistencyWeight;
            offspring.DrawdownProtectionWeight = _random.NextDouble() < 0.6 ? betterParent.DrawdownProtectionWeight : worseParent.DrawdownProtectionWeight;
            
            return offspring;
        }

        private RadicalChromosome RadicalMutation(RadicalChromosome chromosome)
        {
            // Apply radical mutations with 35% chance per parameter
            if (_random.NextDouble() < (double)MUTATION_RATE)
            {
                // Determine if this is a radical mutation (20% chance)
                bool isRadicalMutation = _random.NextDouble() < (double)RADICAL_MUTATION_CHANCE;
                decimal mutationStrength = isRadicalMutation ? 0.5m : 0.1m; // Radical = 50% change, Normal = 10% change
                
                // Mutate core parameters
                if (_random.NextDouble() < (double)MUTATION_RATE) chromosome.RFibLimit1 = MutateParameter(chromosome.RFibLimit1, 800m, 2000m, mutationStrength);
                if (_random.NextDouble() < (double)MUTATION_RATE) chromosome.RFibLimit2 = MutateParameter(chromosome.RFibLimit2, 300m, 800m, mutationStrength);
                if (_random.NextDouble() < (double)MUTATION_RATE) chromosome.RFibLimit3 = MutateParameter(chromosome.RFibLimit3, 150m, 500m, mutationStrength);
                if (_random.NextDouble() < (double)MUTATION_RATE) chromosome.RFibLimit4 = MutateParameter(chromosome.RFibLimit4, 75m, 300m, mutationStrength);
                if (_random.NextDouble() < (double)MUTATION_RATE) chromosome.RFibLimit5 = MutateParameter(chromosome.RFibLimit5, 25m, 150m, mutationStrength);
                if (_random.NextDouble() < (double)MUTATION_RATE) chromosome.RFibLimit6 = MutateParameter(chromosome.RFibLimit6, 10m, 75m, mutationStrength);
                
                if (_random.NextDouble() < (double)MUTATION_RATE) chromosome.WinRateThreshold = MutateParameter(chromosome.WinRateThreshold, 0.55m, 0.85m, mutationStrength);
                if (_random.NextDouble() < (double)MUTATION_RATE) chromosome.ProtectiveTriggerLoss = MutateParameter(chromosome.ProtectiveTriggerLoss, -150m, -25m, mutationStrength);
                if (_random.NextDouble() < (double)MUTATION_RATE) chromosome.ScalingSensitivity = MutateParameter(chromosome.ScalingSensitivity, 0.5m, 4.0m, mutationStrength);
                
                // Mutate advanced parameters
                if (_random.NextDouble() < (double)MUTATION_RATE) chromosome.MovementAgility = MutateParameter(chromosome.MovementAgility, 0.8m, 3.0m, mutationStrength);
                if (_random.NextDouble() < (double)MUTATION_RATE) chromosome.LossReactionSpeed = MutateParameter(chromosome.LossReactionSpeed, 0.5m, 3.5m, mutationStrength);
                if (_random.NextDouble() < (double)MUTATION_RATE) chromosome.ProfitReactionSpeed = MutateParameter(chromosome.ProfitReactionSpeed, 0.5m, 2.5m, mutationStrength);
                
                // Mutate revolutionary parameters
                if (_random.NextDouble() < (double)MUTATION_RATE) chromosome.CrisisRecoverySpeed = MutateParameter(chromosome.CrisisRecoverySpeed, 0.3m, 2.5m, mutationStrength);
                if (_random.NextDouble() < (double)MUTATION_RATE) chromosome.VolatilityAdaptation = MutateParameter(chromosome.VolatilityAdaptation, 0.4m, 2.0m, mutationStrength);
                if (_random.NextDouble() < (double)MUTATION_RATE) chromosome.TrendFollowingStrength = MutateParameter(chromosome.TrendFollowingStrength, 0.2m, 1.8m, mutationStrength);
                
                // Mark as extreme configuration if mutation creates extreme values
                if (isRadicalMutation)
                {
                    chromosome.IsExtremeConfig = true;
                    chromosome.InnovationBonus = GenerateRadicalDecimal(0.5m, 2.0m);
                }
            }
            
            return chromosome;
        }

        private decimal MutateParameter(decimal currentValue, decimal min, decimal max, decimal mutationStrength)
        {
            var range = max - min;
            var maxChange = range * mutationStrength;
            var change = (decimal)(_random.NextDouble() * 2.0 - 1.0) * maxChange; // -maxChange to +maxChange
            var newValue = currentValue + change;
            
            return Math.Max(min, Math.Min(max, newValue));
        }

        private void DisplayGenerationProgress(int generation)
        {
            var sortedPopulation = _population.OrderByDescending(c => c.Fitness).ToList();
            var bestFitness = sortedPopulation.First().Fitness;
            var avgFitness = _population.Average(c => c.Fitness);
            var breakthroughCount = _population.Count(c => c.Fitness > CURRENT_BEST_FITNESS);
            var extremeCount = _population.Count(c => c.IsExtremeConfig);
            
            Console.WriteLine($"Gen {generation,3}: Best={bestFitness:F2} Avg={avgFitness:F2} Breakthroughs={breakthroughCount} Extreme={extremeCount}");
            
            // Display breakthrough alerts
            if (breakthroughCount > 0)
            {
                Console.WriteLine($"        🚀 {breakthroughCount} breakthrough candidate(s) detected!");
            }
            
            // Progress every 10 generations
            if (generation % 10 == 0)
            {
                Console.WriteLine($"        📊 Progress: {generation}/{GENERATIONS} generations complete");
            }
        }

        private void AnalyzeRadicalResults()
        {
            Console.WriteLine("\n🔬 ANALYZING RADICAL OPTIMIZATION RESULTS");
            Console.WriteLine("========================================");
            
            var topConfigurations = _population.OrderByDescending(c => c.Fitness).Take(10).ToList();
            var breakthroughs = _breakthroughCandidates.OrderByDescending(c => c.Fitness).ToList();
            var extremeConfigs = _population.Where(c => c.IsExtremeConfig).OrderByDescending(c => c.Fitness).ToList();
            
            Console.WriteLine($"Best Overall Fitness: {_bestFitness:F2}");
            Console.WriteLine($"Total Breakthroughs: {breakthroughs.Count}");
            Console.WriteLine($"Extreme Configurations: {extremeConfigs.Count}");
            Console.WriteLine($"Population Diversity: {CalculatePopulationDiversity():F2}%");
            
            Console.WriteLine("\n🏆 TOP 5 RADICAL CONFIGURATIONS:");
            Console.WriteLine("================================");
            
            for (int i = 0; i < Math.Min(5, topConfigurations.Count); i++)
            {
                var config = topConfigurations[i];
                Console.WriteLine($"\nRank #{i + 1}: Fitness {config.Fitness:F2} {(config.IsBreakthrough ? "🚀 BREAKTHROUGH" : "")}");
                Console.WriteLine($"  RevFib Limits: [{config.RFibLimit1:F0}, {config.RFibLimit2:F0}, {config.RFibLimit3:F0}, {config.RFibLimit4:F0}, {config.RFibLimit5:F0}, {config.RFibLimit6:F0}]");
                Console.WriteLine($"  Win Rate: {config.WinRateThreshold:P1}, Protection: ${config.ProtectiveTriggerLoss:F0}, Scaling: {config.ScalingSensitivity:F2}x");
                Console.WriteLine($"  Agility: {config.MovementAgility:F2}, Loss Speed: {config.LossReactionSpeed:F2}, Profit Speed: {config.ProfitReactionSpeed:F2}");
                Console.WriteLine($"  Crisis: {config.CrisisMultiplier:F2}x, Volatile: {config.VolatileMultiplier:F2}x, Bull: {config.BullMultiplier:F2}x");
                
                if (config.IsExtremeConfig)
                {
                    Console.WriteLine($"  🔥 EXTREME CONFIG: Innovation Bonus {config.InnovationBonus:F2}");
                }
            }
        }

        private decimal CalculatePopulationDiversity()
        {
            // Calculate diversity based on parameter variance
            var scalingSensitivities = _population.Select(c => c.ScalingSensitivity).ToList();
            var variance = CalculateVariance(scalingSensitivities);
            return Math.Min(100m, variance * 100m);
        }

        private decimal CalculateVariance(List<decimal> values)
        {
            var mean = values.Average();
            var squaredDifferences = values.Select(v => (decimal)Math.Pow((double)(v - mean), 2));
            return squaredDifferences.Average();
        }

        private void ExportBreakthroughCandidates()
        {
            var exportPath = @"C:\code\ODTE\PM250_Radical_Breakthrough_Candidates.csv";
            
            Console.WriteLine($"\n📁 Exporting breakthrough candidates to: {exportPath}");
            
            using (var writer = new StreamWriter(exportPath))
            {
                writer.WriteLine("Rank,Fitness,Generation,Discovery_Time,Breakthrough,Extreme_Config," +
                               "RFib1,RFib2,RFib3,RFib4,RFib5,RFib6," +
                               "Win_Rate_Threshold,Protection_Trigger,Scaling_Sensitivity," +
                               "Movement_Agility,Loss_Reaction_Speed,Profit_Reaction_Speed," +
                               "Crisis_Multiplier,Volatile_Multiplier,Bull_Multiplier," +
                               "Crisis_Recovery_Speed,Volatility_Adaptation,Trend_Following_Strength," +
                               "Mean_Reversion_Bias,Seasonality_Weight,Correlation_Sensitivity," +
                               "Innovation_Factor,Innovation_Bonus," +
                               "Capital_Preservation_Weight,Sharpe_Ratio_Weight,Consistency_Weight,Drawdown_Protection_Weight");
                
                var rankedCandidates = _breakthroughCandidates.OrderByDescending(c => c.Fitness).ToList();
                
                for (int i = 0; i < rankedCandidates.Count; i++)
                {
                    var candidate = rankedCandidates[i];
                    writer.WriteLine($"{i + 1},{candidate.Fitness:F4},{candidate.Generation},{candidate.DiscoveryTime:yyyy-MM-dd HH:mm:ss}," +
                                   $"{candidate.IsBreakthrough},{candidate.IsExtremeConfig}," +
                                   $"{candidate.RFibLimit1:F2},{candidate.RFibLimit2:F2},{candidate.RFibLimit3:F2}," +
                                   $"{candidate.RFibLimit4:F2},{candidate.RFibLimit5:F2},{candidate.RFibLimit6:F2}," +
                                   $"{candidate.WinRateThreshold:F4},{candidate.ProtectiveTriggerLoss:F2},{candidate.ScalingSensitivity:F4}," +
                                   $"{candidate.MovementAgility:F4},{candidate.LossReactionSpeed:F4},{candidate.ProfitReactionSpeed:F4}," +
                                   $"{candidate.CrisisMultiplier:F4},{candidate.VolatileMultiplier:F4},{candidate.BullMultiplier:F4}," +
                                   $"{candidate.CrisisRecoverySpeed:F4},{candidate.VolatilityAdaptation:F4},{candidate.TrendFollowingStrength:F4}," +
                                   $"{candidate.MeanReversionBias:F4},{candidate.SeasonalityWeight:F4},{candidate.CorrelationSensitivity:F4}," +
                                   $"{candidate.InnovationFactor:F4},{candidate.InnovationBonus:F4}," +
                                   $"{candidate.CapitalPreservationWeight:F4},{candidate.SharpeRatioWeight:F4}," +
                                   $"{candidate.ConsistencyWeight:F4},{candidate.DrawdownProtectionWeight:F4}");
                }
            }
            
            Console.WriteLine($"✓ Exported {_breakthroughCandidates.Count} breakthrough candidates");
        }

        private void DisplayFinalBreakthroughResults()
        {
            var totalBreakthroughs = _breakthroughCandidates.Count;
            var uniqueBreakthroughs = _breakthroughCandidates.GroupBy(c => c.Fitness).Count();
            var extremeBreakthroughs = _breakthroughCandidates.Count(c => c.IsExtremeConfig);
            var topFitness = _breakthroughCandidates.Any() ? _breakthroughCandidates.Max(c => c.Fitness) : _bestFitness;
            
            Console.WriteLine($"Final Best Fitness: {topFitness:F2}");
            Console.WriteLine($"Improvement over Current: {(topFitness - CURRENT_BEST_FITNESS):+F2;-F2;0.00}");
            Console.WriteLine($"Total Breakthroughs: {totalBreakthroughs}");
            Console.WriteLine($"Unique Configurations: {uniqueBreakthroughs}");
            Console.WriteLine($"Extreme Breakthroughs: {extremeBreakthroughs}");
            
            if (totalBreakthroughs > 0)
            {
                Console.WriteLine("\n🚀 RADICAL OPTIMIZATION SUCCESS!");
                Console.WriteLine("Breakthrough configurations discovered with exceptional potential.");
                Console.WriteLine("Recommend validation of top candidates for deployment consideration.");
                
                var topBreakthrough = _breakthroughCandidates.OrderByDescending(c => c.Fitness).First();
                Console.WriteLine($"\nTOP BREAKTHROUGH CONFIGURATION:");
                Console.WriteLine($"Fitness: {topBreakthrough.Fitness:F2}");
                Console.WriteLine($"Generation: {topBreakthrough.Generation}");
                Console.WriteLine($"Extreme Config: {(topBreakthrough.IsExtremeConfig ? "YES" : "NO")}");
                Console.WriteLine($"Innovation Bonus: {topBreakthrough.InnovationBonus:F2}");
            }
            else
            {
                Console.WriteLine("\n⚠️ No breakthroughs exceeding current best fitness detected.");
                Console.WriteLine("Consider expanding parameter ranges or increasing mutation rates.");
            }
        }
    }

    public class RadicalChromosome
    {
        public Guid Id { get; set; }
        public int Generation { get; set; }
        public DateTime DiscoveryTime { get; set; }
        public decimal Fitness { get; set; }
        public bool IsBreakthrough { get; set; }
        public bool IsExtremeConfig { get; set; }
        public decimal InnovationBonus { get; set; }
        
        // Core RevFibNotch Parameters
        public decimal RFibLimit1 { get; set; }
        public decimal RFibLimit2 { get; set; }
        public decimal RFibLimit3 { get; set; }
        public decimal RFibLimit4 { get; set; }
        public decimal RFibLimit5 { get; set; }
        public decimal RFibLimit6 { get; set; }
        
        // Enhanced Thresholds
        public decimal WinRateThreshold { get; set; }
        public decimal ProtectiveTriggerLoss { get; set; }
        public decimal ScalingSensitivity { get; set; }
        
        // Advanced Reaction Parameters
        public decimal MovementAgility { get; set; }
        public decimal LossReactionSpeed { get; set; }
        public decimal ProfitReactionSpeed { get; set; }
        
        // Market Regime Multipliers
        public decimal CrisisMultiplier { get; set; }
        public decimal VolatileMultiplier { get; set; }
        public decimal BullMultiplier { get; set; }
        
        // Revolutionary Parameters
        public decimal CrisisRecoverySpeed { get; set; }
        public decimal VolatilityAdaptation { get; set; }
        public decimal TrendFollowingStrength { get; set; }
        public decimal MeanReversionBias { get; set; }
        public decimal SeasonalityWeight { get; set; }
        public decimal CorrelationSensitivity { get; set; }
        public decimal InnovationFactor { get; set; }
        
        // Multi-Objective Weights
        public decimal CapitalPreservationWeight { get; set; }
        public decimal SharpeRatioWeight { get; set; }
        public decimal ConsistencyWeight { get; set; }
        public decimal DrawdownProtectionWeight { get; set; }
        
        public RadicalChromosome Clone()
        {
            return new RadicalChromosome
            {
                Id = Guid.NewGuid(),
                Generation = this.Generation,
                DiscoveryTime = this.DiscoveryTime,
                Fitness = this.Fitness,
                IsBreakthrough = this.IsBreakthrough,
                IsExtremeConfig = this.IsExtremeConfig,
                InnovationBonus = this.InnovationBonus,
                
                RFibLimit1 = this.RFibLimit1,
                RFibLimit2 = this.RFibLimit2,
                RFibLimit3 = this.RFibLimit3,
                RFibLimit4 = this.RFibLimit4,
                RFibLimit5 = this.RFibLimit5,
                RFibLimit6 = this.RFibLimit6,
                
                WinRateThreshold = this.WinRateThreshold,
                ProtectiveTriggerLoss = this.ProtectiveTriggerLoss,
                ScalingSensitivity = this.ScalingSensitivity,
                
                MovementAgility = this.MovementAgility,
                LossReactionSpeed = this.LossReactionSpeed,
                ProfitReactionSpeed = this.ProfitReactionSpeed,
                
                CrisisMultiplier = this.CrisisMultiplier,
                VolatileMultiplier = this.VolatileMultiplier,
                BullMultiplier = this.BullMultiplier,
                
                CrisisRecoverySpeed = this.CrisisRecoverySpeed,
                VolatilityAdaptation = this.VolatilityAdaptation,
                TrendFollowingStrength = this.TrendFollowingStrength,
                MeanReversionBias = this.MeanReversionBias,
                SeasonalityWeight = this.SeasonalityWeight,
                CorrelationSensitivity = this.CorrelationSensitivity,
                InnovationFactor = this.InnovationFactor,
                
                CapitalPreservationWeight = this.CapitalPreservationWeight,
                SharpeRatioWeight = this.SharpeRatioWeight,
                ConsistencyWeight = this.ConsistencyWeight,
                DrawdownProtectionWeight = this.DrawdownProtectionWeight
            };
        }
    }
}
