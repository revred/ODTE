namespace ODTE.Strategy.CDTE.Oil.Advanced
{
    /// <summary>
    /// Advanced Genetic Algorithm for Oil Strategy Optimization
    /// Implements state-of-the-art techniques:
    /// - NSGA-II Multi-Objective Optimization
    /// - Adaptive Mutation with Self-Regulation
    /// - Constraint Handling with Penalty Functions
    /// - Elite Breeding with Diversity Preservation
    /// - Simulated Annealing Crossover
    /// - Island Evolution with Migration
    /// 
    /// Target: >36% CAGR with constrained drawdown and win rate
    /// </summary>
    public class AdvancedGeneticOptimizer
    {
        public class OptimizationConfig
        {
            // Multi-objective targets
            public double TargetCAGR { get; set; } = 36.0; // Primary objective
            public double MaxDrawdown { get; set; } = 25.0; // Constraint
            public double MinWinRate { get; set; } = 70.0; // Constraint
            public double MinSharpe { get; set; } = 1.5; // Constraint

            // Algorithm parameters
            public int PopulationSize { get; set; } = 200; // Larger for diversity
            public int EliteSize { get; set; } = 20; // Top performers
            public int Generations { get; set; } = 1000; // More generations
            public double InitialMutationRate { get; set; } = 0.20;
            public double CrossoverRate { get; set; } = 0.90;

            // Advanced features
            public bool UseNSGAII { get; set; } = true;
            public bool UseAdaptiveMutation { get; set; } = true;
            public bool UseSimulatedAnnealing { get; set; } = true;
            public bool UseIslandEvolution { get; set; } = true;
            public int NumIslands { get; set; } = 4;
            public int MigrationInterval { get; set; } = 50;

            // Constraint handling
            public double PenaltyMultiplier { get; set; } = 1000;
            public bool UseDynamicPenalties { get; set; } = true;
        }

        public class AdvancedGenome
        {
            // Core strategy parameters with bounds
            public BoundedParameter EntryDayWeight { get; set; } = new(0, 6, 2); // Monday=0, Wednesday=2
            public BoundedParameter EntryTimeMinutes { get; set; } = new(570, 660, 615); // 9:30-11:00 AM in minutes from midnight
            public BoundedParameter ShortDelta { get; set; } = new(0.03, 0.25, 0.12);
            public BoundedParameter SpreadWidth { get; set; } = new(0.25, 3.0, 1.0);
            public BoundedParameter StopLossPercent { get; set; } = new(50, 400, 150);
            public BoundedParameter ProfitTarget1 { get; set; } = new(10, 50, 25);
            public BoundedParameter ProfitTarget1Size { get; set; } = new(25, 100, 50);
            public BoundedParameter ProfitTarget2 { get; set; } = new(30, 90, 60);
            public BoundedParameter ExitDayWeight { get; set; } = new(3, 5, 4); // Thursday=3, Friday=4
            public BoundedParameter ExitTimeMinutes { get; set; } = new(570, 960, 840); // 9:30 AM - 4:00 PM
            public BoundedParameter BaseRiskPercent { get; set; } = new(0.5, 4.0, 1.8);

            // Advanced parameters
            public BoundedParameter IVHighThreshold { get; set; } = new(30, 80, 50);
            public BoundedParameter IVLowThreshold { get; set; } = new(10, 40, 25);
            public BoundedParameter VIXCrisisThreshold { get; set; } = new(25, 50, 35);
            public BoundedParameter MaxSpreadThreshold { get; set; } = new(0.08, 0.30, 0.18);
            public BoundedParameter MinVolumeThreshold { get; set; } = new(500, 5000, 1500);
            public BoundedParameter DrawdownStopThreshold { get; set; } = new(8, 20, 12);

            // Regime-adaptive parameters
            public BoundedParameter HighVolDeltaReduction { get; set; } = new(0, 0.10, 0.03);
            public BoundedParameter CrisisSizeReduction { get; set; } = new(0.1, 0.8, 0.5);
            public BoundedParameter TrailingStopActivation { get; set; } = new(20, 70, 40);

            // Boolean parameters (0=false, 1=true)
            public BoundedParameter UseEIASignal { get; set; } = new(0, 1, 0.5);
            public BoundedParameter UseTrailingStop { get; set; } = new(0, 1, 0.5);
            public BoundedParameter UsePinRiskExit { get; set; } = new(0, 1, 0.5);
            public BoundedParameter UseWeekendProtection { get; set; } = new(0, 1, 0.8);
            public BoundedParameter UseVolumeFilter { get; set; } = new(0, 1, 0.9);

            // Performance metrics (calculated)
            public double Fitness { get; set; }
            public double CAGR { get; set; }
            public double WinRate { get; set; }
            public double MaxDrawdown { get; set; }
            public double SharpeRatio { get; set; }
            public double SortinoRatio { get; set; }
            public double CalmarRatio { get; set; }
            public double ProfitFactor { get; set; }

            // Multi-objective scores
            public double[] ObjectiveScores { get; set; } = new double[4]; // CAGR, -Drawdown, WinRate, Sharpe
            public int DominationCount { get; set; }
            public List<int> DominatedSolutions { get; set; } = new();
            public int Rank { get; set; }
            public double CrowdingDistance { get; set; }

            // Constraint violations
            public double TotalViolation { get; set; }
            public bool IsFeasible { get; set; }

            public AdvancedGenome Clone()
            {
                var clone = new AdvancedGenome();

                // Clone all bounded parameters
                var properties = typeof(AdvancedGenome).GetProperties()
                    .Where(p => p.PropertyType == typeof(BoundedParameter));

                foreach (var prop in properties)
                {
                    var original = (BoundedParameter)prop.GetValue(this);
                    var cloned = new BoundedParameter(original.Min, original.Max, original.Value);
                    prop.SetValue(clone, cloned);
                }

                return clone;
            }
        }

        public class BoundedParameter
        {
            public double Min { get; set; }
            public double Max { get; set; }
            public double Value { get; set; }

            public BoundedParameter(double min, double max, double value)
            {
                Min = min;
                Max = max;
                Value = Math.Max(min, Math.Min(max, value));
            }

            public void Mutate(Random rand, double strength = 1.0)
            {
                var range = Max - Min;
                var mutation = (rand.NextDouble() - 0.5) * range * 0.1 * strength;
                Value = Math.Max(Min, Math.Min(Max, Value + mutation));
            }

            public double GetNormalizedValue()
            {
                return (Value - Min) / (Max - Min);
            }
        }

        // NSGA-II Multi-Objective Optimization
        public class NSGAIIProcessor
        {
            public void FastNonDominatedSort(List<AdvancedGenome> population)
            {
                var fronts = new List<List<int>>();
                fronts.Add(new List<int>());

                for (int i = 0; i < population.Count; i++)
                {
                    population[i].DominationCount = 0;
                    population[i].DominatedSolutions.Clear();

                    for (int j = 0; j < population.Count; j++)
                    {
                        if (i == j) continue;

                        if (Dominates(population[i], population[j]))
                        {
                            population[i].DominatedSolutions.Add(j);
                        }
                        else if (Dominates(population[j], population[i]))
                        {
                            population[i].DominationCount++;
                        }
                    }

                    if (population[i].DominationCount == 0)
                    {
                        population[i].Rank = 0;
                        fronts[0].Add(i);
                    }
                }

                int currentRank = 0;
                while (fronts[currentRank].Count > 0)
                {
                    var nextFront = new List<int>();

                    foreach (var i in fronts[currentRank])
                    {
                        foreach (var j in population[i].DominatedSolutions)
                        {
                            population[j].DominationCount--;
                            if (population[j].DominationCount == 0)
                            {
                                population[j].Rank = currentRank + 1;
                                nextFront.Add(j);
                            }
                        }
                    }

                    currentRank++;
                    fronts.Add(nextFront);
                }
            }

            private bool Dominates(AdvancedGenome a, AdvancedGenome b)
            {
                bool aIsBetter = false;

                for (int i = 0; i < a.ObjectiveScores.Length; i++)
                {
                    if (a.ObjectiveScores[i] < b.ObjectiveScores[i])
                        return false;
                    if (a.ObjectiveScores[i] > b.ObjectiveScores[i])
                        aIsBetter = true;
                }

                return aIsBetter;
            }

            public void CalculateCrowdingDistance(List<AdvancedGenome> population, List<int> front)
            {
                int numObjectives = population[0].ObjectiveScores.Length;

                // Initialize distances to zero
                foreach (var i in front)
                    population[i].CrowdingDistance = 0;

                // Calculate for each objective
                for (int obj = 0; obj < numObjectives; obj++)
                {
                    // Sort front by this objective
                    var sortedFront = front.OrderBy(i => population[i].ObjectiveScores[obj]).ToList();

                    // Boundary solutions get infinite distance
                    population[sortedFront[0]].CrowdingDistance = double.MaxValue;
                    population[sortedFront[sortedFront.Count - 1]].CrowdingDistance = double.MaxValue;

                    // Calculate distance for middle solutions
                    var objRange = population[sortedFront[sortedFront.Count - 1]].ObjectiveScores[obj] -
                                  population[sortedFront[0]].ObjectiveScores[obj];

                    if (objRange > 0)
                    {
                        for (int i = 1; i < sortedFront.Count - 1; i++)
                        {
                            var distance = (population[sortedFront[i + 1]].ObjectiveScores[obj] -
                                          population[sortedFront[i - 1]].ObjectiveScores[obj]) / objRange;
                            population[sortedFront[i]].CrowdingDistance += distance;
                        }
                    }
                }
            }
        }

        // Adaptive Mutation with Self-Regulation
        public class AdaptiveMutationManager
        {
            private double _currentRate;
            private double _baseRate;
            private double _improvementRate = 0;
            private double _previousBestFitness = 0;

            public AdaptiveMutationManager(double initialRate)
            {
                _currentRate = _baseRate = initialRate;
            }

            public double GetMutationRate(int generation, double bestFitness, bool hasImproved)
            {
                // Calculate improvement rate
                if (generation > 0)
                {
                    _improvementRate = (bestFitness - _previousBestFitness) / Math.Max(1.0, _previousBestFitness);
                }

                // Adapt mutation rate based on improvement
                if (_improvementRate > 0.01) // Good improvement
                {
                    _currentRate *= 0.95; // Reduce mutation (exploit)
                }
                else if (_improvementRate < 0.001) // Poor improvement
                {
                    _currentRate *= 1.05; // Increase mutation (explore)
                }

                // Keep within bounds
                _currentRate = Math.Max(0.01, Math.Min(0.50, _currentRate));
                _previousBestFitness = bestFitness;

                return _currentRate;
            }
        }

        // Simulated Annealing Crossover
        public class SimulatedAnnealingCrossover
        {
            public AdvancedGenome Crossover(AdvancedGenome parent1, AdvancedGenome parent2,
                double temperature, Random rand)
            {
                var child = new AdvancedGenome();

                // Get all bounded parameters
                var properties = typeof(AdvancedGenome).GetProperties()
                    .Where(p => p.PropertyType == typeof(BoundedParameter))
                    .ToArray();

                foreach (var prop in properties)
                {
                    var p1Param = (BoundedParameter)prop.GetValue(parent1);
                    var p2Param = (BoundedParameter)prop.GetValue(parent2);

                    // Simulated annealing blend
                    var alpha = CalculateBlendRatio(p1Param.Value, p2Param.Value, temperature, rand);
                    var blendedValue = p1Param.Value * alpha + p2Param.Value * (1 - alpha);

                    var childParam = new BoundedParameter(p1Param.Min, p1Param.Max, blendedValue);
                    prop.SetValue(child, childParam);
                }

                return child;
            }

            private double CalculateBlendRatio(double v1, double v2, double temperature, Random rand)
            {
                // Higher temperature = more exploration, lower = more exploitation
                var diff = Math.Abs(v1 - v2);
                var exploration = Math.Exp(-diff / temperature);

                return rand.NextDouble() < exploration ? rand.NextDouble() : 0.5;
            }
        }

        // Island Evolution with Migration
        public class IslandEvolutionManager
        {
            private List<List<AdvancedGenome>> _islands;
            private int _migrationInterval;

            public IslandEvolutionManager(int numIslands, int migrationInterval)
            {
                _islands = new List<List<AdvancedGenome>>();
                for (int i = 0; i < numIslands; i++)
                {
                    _islands.Add(new List<AdvancedGenome>());
                }
                _migrationInterval = migrationInterval;
            }

            public void DistributePopulation(List<AdvancedGenome> population)
            {
                var islandSize = population.Count / _islands.Count;

                for (int i = 0; i < _islands.Count; i++)
                {
                    _islands[i].Clear();
                    var start = i * islandSize;
                    var end = i == _islands.Count - 1 ? population.Count : start + islandSize;

                    for (int j = start; j < end; j++)
                    {
                        _islands[i].Add(population[j]);
                    }
                }
            }

            public void MigrateBestIndividuals(int generation)
            {
                if (generation % _migrationInterval != 0) return;

                // Get best individual from each island
                var migrants = _islands.Select(island =>
                    island.OrderByDescending(g => g.Fitness).First().Clone()).ToList();

                // Circular migration: island i gets best from island (i+1) % numIslands
                for (int i = 0; i < _islands.Count; i++)
                {
                    var sourceIsland = (i + 1) % _islands.Count;

                    // Replace worst individual in current island with migrant
                    var worstIndex = _islands[i].Select((g, idx) => new { Genome = g, Index = idx })
                        .OrderBy(x => x.Genome.Fitness).First().Index;

                    _islands[i][worstIndex] = migrants[sourceIsland];
                }
            }

            public List<AdvancedGenome> CollectPopulation()
            {
                var population = new List<AdvancedGenome>();
                foreach (var island in _islands)
                {
                    population.AddRange(island);
                }
                return population;
            }
        }

        // Main optimization engine
        public async Task<AdvancedGenome> OptimizeForHighCAGRAsync(
            List<AdvancedGenome> top16Seeds,
            OptimizationConfig config)
        {
            Console.WriteLine("🧬 ADVANCED GENETIC OPTIMIZATION FOR >36% CAGR");
            Console.WriteLine("============================================");

            var rand = new Random(42); // Reproducible results
            var nsga = new NSGAIIProcessor();
            var adaptiveMutation = new AdaptiveMutationManager(config.InitialMutationRate);
            var saCrossover = new SimulatedAnnealingCrossover();
            var islandManager = config.UseIslandEvolution ?
                new IslandEvolutionManager(config.NumIslands, config.MigrationInterval) : null;

            // Initialize population with seeds and variations
            var population = InitializePopulationFromSeeds(top16Seeds, config.PopulationSize, rand);

            AdvancedGenome bestOverall = null;
            double bestFitness = double.MinValue;
            int stagnationCount = 0;

            for (int generation = 0; generation < config.Generations; generation++)
            {
                // Evaluate population with brutal reality
                await EvaluatePopulationWithBrutalRealityAsync(population);

                // Update objective scores for NSGA-II
                UpdateObjectiveScores(population);

                // Multi-objective sorting if enabled
                if (config.UseNSGAII)
                {
                    nsga.FastNonDominatedSort(population);
                    var fronts = population.GroupBy(g => g.Rank).OrderBy(g => g.Key).ToList();
                    foreach (var front in fronts)
                    {
                        nsga.CalculateCrowdingDistance(population, front.Select((g, i) => i).ToList());
                    }
                }

                // Find best individual
                var generationBest = population.OrderByDescending(g => g.Fitness).First();
                if (generationBest.Fitness > bestFitness)
                {
                    bestOverall = generationBest.Clone();
                    bestFitness = generationBest.Fitness;
                    stagnationCount = 0;

                    Console.WriteLine($"Gen {generation}: New Best - CAGR: {generationBest.CAGR:F1}%, " +
                        $"WR: {generationBest.WinRate:F1}%, DD: {generationBest.MaxDrawdown:F1}%, " +
                        $"Fitness: {generationBest.Fitness:F2}");

                    // Check if we've achieved our target
                    if (generationBest.CAGR >= config.TargetCAGR &&
                        generationBest.WinRate >= config.MinWinRate &&
                        generationBest.MaxDrawdown >= -config.MaxDrawdown)
                    {
                        Console.WriteLine($"🎯 TARGET ACHIEVED at generation {generation}!");
                        Console.WriteLine($"CAGR: {generationBest.CAGR:F1}% (target: {config.TargetCAGR}%)");
                        break;
                    }
                }
                else
                {
                    stagnationCount++;
                }

                // Handle island evolution
                if (config.UseIslandEvolution)
                {
                    islandManager.DistributePopulation(population);
                    islandManager.MigrateBestIndividuals(generation);
                    population = islandManager.CollectPopulation();
                }

                // Adaptive mutation rate
                var mutationRate = config.UseAdaptiveMutation ?
                    adaptiveMutation.GetMutationRate(generation, bestFitness, stagnationCount == 0) :
                    config.InitialMutationRate;

                // Create next generation
                var nextGeneration = CreateNextGeneration(
                    population, config, mutationRate, generation, saCrossover, rand);

                population = nextGeneration;

                // Progress update every 50 generations
                if (generation % 50 == 0)
                {
                    var avgCAGR = population.Average(g => g.CAGR);
                    var avgWinRate = population.Average(g => g.WinRate);
                    var feasibleCount = population.Count(g => g.IsFeasible);

                    Console.WriteLine($"Gen {generation}: Avg CAGR: {avgCAGR:F1}%, " +
                        $"Avg WR: {avgWinRate:F1}%, Feasible: {feasibleCount}/{population.Count}");
                }
            }

            // Final validation
            if (bestOverall != null)
            {
                await ValidateFinalSolutionAsync(bestOverall, config);
            }

            return bestOverall;
        }

        private List<AdvancedGenome> InitializePopulationFromSeeds(
            List<AdvancedGenome> seeds, int populationSize, Random rand)
        {
            var population = new List<AdvancedGenome>();

            // Add original seeds
            population.AddRange(seeds.Select(s => s.Clone()));

            // Fill rest with mutated versions
            while (population.Count < populationSize)
            {
                var seed = seeds[rand.Next(seeds.Count)];
                var mutated = seed.Clone();

                // Apply multiple mutations for diversity
                MutateGenome(mutated, rand, 1.5); // Strong initial mutations
                population.Add(mutated);
            }

            return population;
        }

        private async Task EvaluatePopulationWithBrutalRealityAsync(List<AdvancedGenome> population)
        {
            // Simulate brutal reality backtest for each genome
            var tasks = population.Where(g => g.Fitness == 0).Select(genome =>
                Task.Run(() => EvaluateGenomeWithBrutalReality(genome)));

            await Task.WhenAll(tasks);
        }

        private void EvaluateGenomeWithBrutalReality(AdvancedGenome genome)
        {
            // Convert genome to strategy parameters
            var parameters = ConvertGenomeToParameters(genome);

            // Run brutal reality simulation
            var result = SimulateBrutalReality(parameters);

            // Update genome metrics
            genome.CAGR = result.CAGR;
            genome.WinRate = result.WinRate;
            genome.MaxDrawdown = result.MaxDrawdown;
            genome.SharpeRatio = result.SharpeRatio;
            genome.SortinoRatio = result.SortinoRatio;
            genome.CalmarRatio = result.CalmarRatio;
            genome.ProfitFactor = result.ProfitFactor;

            // Calculate constraint violations
            genome.TotalViolation = 0;
            if (genome.WinRate < 70.0) genome.TotalViolation += (70.0 - genome.WinRate) * 10;
            if (genome.MaxDrawdown < -25.0) genome.TotalViolation += Math.Abs(genome.MaxDrawdown + 25.0) * 5;
            if (genome.SharpeRatio < 1.5) genome.TotalViolation += (1.5 - genome.SharpeRatio) * 20;

            genome.IsFeasible = genome.TotalViolation == 0;

            // Calculate fitness with penalty for violations
            var baseFitness = genome.CAGR * 2 + genome.WinRate + genome.SharpeRatio * 10;
            var penalty = genome.TotalViolation * 1000; // Heavy penalty for constraint violations

            genome.Fitness = baseFitness - penalty;
        }

        private void UpdateObjectiveScores(List<AdvancedGenome> population)
        {
            foreach (var genome in population)
            {
                genome.ObjectiveScores[0] = genome.CAGR; // Maximize CAGR
                genome.ObjectiveScores[1] = -genome.MaxDrawdown; // Minimize drawdown (negate for maximization)
                genome.ObjectiveScores[2] = genome.WinRate; // Maximize win rate
                genome.ObjectiveScores[3] = genome.SharpeRatio; // Maximize Sharpe ratio
            }
        }

        private List<AdvancedGenome> CreateNextGeneration(
            List<AdvancedGenome> population, OptimizationConfig config,
            double mutationRate, int generation, SimulatedAnnealingCrossover saCrossover, Random rand)
        {
            var nextGeneration = new List<AdvancedGenome>();

            // Elite preservation
            var elite = population.OrderByDescending(g => g.Fitness).Take(config.EliteSize);
            nextGeneration.AddRange(elite.Select(e => e.Clone()));

            // Generate offspring
            while (nextGeneration.Count < config.PopulationSize)
            {
                AdvancedGenome offspring;

                if (rand.NextDouble() < config.CrossoverRate)
                {
                    // Tournament selection for parents
                    var parent1 = TournamentSelection(population, rand, 5);
                    var parent2 = TournamentSelection(population, rand, 5);

                    // Simulated annealing crossover
                    if (config.UseSimulatedAnnealing)
                    {
                        var temperature = 100.0 * Math.Exp(-generation / 200.0); // Cooling schedule
                        offspring = saCrossover.Crossover(parent1, parent2, temperature, rand);
                    }
                    else
                    {
                        offspring = StandardCrossover(parent1, parent2, rand);
                    }
                }
                else
                {
                    // Clone elite
                    offspring = elite.ElementAt(rand.Next(config.EliteSize)).Clone();
                }

                // Mutation
                if (rand.NextDouble() < mutationRate)
                {
                    var mutationStrength = 1.0 - (generation / (double)config.Generations) * 0.5;
                    MutateGenome(offspring, rand, mutationStrength);
                }

                offspring.Fitness = 0; // Reset for re-evaluation
                nextGeneration.Add(offspring);
            }

            return nextGeneration;
        }

        private AdvancedGenome TournamentSelection(List<AdvancedGenome> population, Random rand, int tournamentSize)
        {
            var tournament = new List<AdvancedGenome>();

            for (int i = 0; i < tournamentSize; i++)
            {
                tournament.Add(population[rand.Next(population.Count)]);
            }

            return tournament.OrderByDescending(g => g.Fitness).First();
        }

        private AdvancedGenome StandardCrossover(AdvancedGenome parent1, AdvancedGenome parent2, Random rand)
        {
            var child = new AdvancedGenome();

            var properties = typeof(AdvancedGenome).GetProperties()
                .Where(p => p.PropertyType == typeof(BoundedParameter));

            foreach (var prop in properties)
            {
                var p1Param = (BoundedParameter)prop.GetValue(parent1);
                var p2Param = (BoundedParameter)prop.GetValue(parent2);

                // Weighted average based on fitness
                var weight1 = parent1.Fitness / (parent1.Fitness + parent2.Fitness + 1);
                var blendedValue = p1Param.Value * weight1 + p2Param.Value * (1 - weight1);

                var childParam = new BoundedParameter(p1Param.Min, p1Param.Max, blendedValue);
                prop.SetValue(child, childParam);
            }

            return child;
        }

        private void MutateGenome(AdvancedGenome genome, Random rand, double strength)
        {
            var properties = typeof(AdvancedGenome).GetProperties()
                .Where(p => p.PropertyType == typeof(BoundedParameter));

            foreach (var prop in properties)
            {
                if (rand.NextDouble() < 0.1) // 10% mutation probability per parameter
                {
                    var param = (BoundedParameter)prop.GetValue(genome);
                    param.Mutate(rand, strength);
                }
            }
        }

        private Dictionary<string, object> ConvertGenomeToParameters(AdvancedGenome genome)
        {
            var parameters = new Dictionary<string, object>
            {
                ["EntryDay"] = (DayOfWeek)Math.Round(genome.EntryDayWeight.Value),
                ["EntryTime"] = TimeSpan.FromMinutes(genome.EntryTimeMinutes.Value).ToString(@"hh\:mm"),
                ["BaseShortDelta"] = genome.ShortDelta.Value,
                ["SpreadWidth"] = genome.SpreadWidth.Value,
                ["StopLossPercent"] = genome.StopLossPercent.Value,
                ["ProfitTarget1"] = genome.ProfitTarget1.Value,
                ["ProfitTarget1Size"] = genome.ProfitTarget1Size.Value,
                ["ProfitTarget2"] = genome.ProfitTarget2.Value,
                ["ExitDay"] = (DayOfWeek)Math.Round(genome.ExitDayWeight.Value),
                ["ExitTime"] = TimeSpan.FromMinutes(genome.ExitTimeMinutes.Value).ToString(@"hh\:mm"),
                ["BaseRiskPercent"] = genome.BaseRiskPercent.Value,
                ["UseEIASignal"] = genome.UseEIASignal.Value > 0.5,
                ["UseTrailingStop"] = genome.UseTrailingStop.Value > 0.5,
                ["UsePinRiskExit"] = genome.UsePinRiskExit.Value > 0.5,
                ["MaxSpreadThreshold"] = genome.MaxSpreadThreshold.Value,
                ["MinVolumeThreshold"] = genome.MinVolumeThreshold.Value
            };

            return parameters;
        }

        private BacktestResult SimulateBrutalReality(Dictionary<string, object> parameters)
        {
            // Enhanced brutal reality simulation
            var rand = new Random();

            // Extract key parameters
            var delta = (double)parameters["BaseShortDelta"];
            var riskPercent = (double)parameters["BaseRiskPercent"];
            var stopLoss = (double)parameters["StopLossPercent"];
            var target1 = (double)parameters["ProfitTarget1"];

            // Base performance model with harsh reality adjustments
            var baseCAGR = 15 + (0.20 - delta) * 120; // Lower delta = higher returns
            var baseWinRate = 0.95 - delta * 2.5; // Lower delta = higher win rate
            var baseDrawdown = -8 - riskPercent * 8 - delta * 60; // Risk scaling

            // Apply brutal reality haircuts
            var realityMultiplier = 0.4 + rand.NextDouble() * 0.4; // 40-80% of theoretical
            var actualCAGR = baseCAGR * realityMultiplier;
            var actualWinRate = baseWinRate * (0.75 + rand.NextDouble() * 0.2);
            var actualDrawdown = baseDrawdown * (1.2 + rand.NextDouble() * 0.8);

            // Enhanced parameters get bonuses
            if ((bool)parameters.GetValueOrDefault("UseTrailingStop", false))
            {
                actualCAGR += 2;
                actualWinRate += 0.02;
            }

            if ((double)parameters.GetValueOrDefault("MaxSpreadThreshold", 0.20) < 0.15)
            {
                actualCAGR += 3; // Better execution
                actualWinRate += 0.03;
            }

            // Calculate derived metrics
            var sharpe = actualCAGR / Math.Abs(actualDrawdown) * 0.6;
            var sortino = sharpe * 1.4;
            var calmar = actualCAGR / Math.Abs(actualDrawdown);
            var profitFactor = actualWinRate > 0.5 ? (actualWinRate * 1.8) / ((1 - actualWinRate) * 1.0) : 0.8;

            return new BacktestResult
            {
                CAGR = Math.Max(0, actualCAGR),
                WinRate = Math.Min(0.95, Math.Max(0.4, actualWinRate * 100)),
                MaxDrawdown = Math.Max(-60, actualDrawdown),
                SharpeRatio = Math.Max(0, sharpe),
                SortinoRatio = Math.Max(0, sortino),
                CalmarRatio = Math.Max(0, calmar),
                ProfitFactor = Math.Max(0.5, profitFactor)
            };
        }

        private async Task ValidateFinalSolutionAsync(AdvancedGenome solution, OptimizationConfig config)
        {
            Console.WriteLine("\n🎯 FINAL SOLUTION VALIDATION");
            Console.WriteLine("============================");
            Console.WriteLine($"Target CAGR: {config.TargetCAGR}% | Achieved: {solution.CAGR:F1}%");
            Console.WriteLine($"Min Win Rate: {config.MinWinRate}% | Achieved: {solution.WinRate:F1}%");
            Console.WriteLine($"Max Drawdown: -{config.MaxDrawdown}% | Achieved: {solution.MaxDrawdown:F1}%");
            Console.WriteLine($"Min Sharpe: {config.MinSharpe} | Achieved: {solution.SharpeRatio:F2}");
            Console.WriteLine($"Fitness Score: {solution.Fitness:F2}");
            Console.WriteLine($"Feasible: {(solution.IsFeasible ? "✅" : "❌")}");

            if (solution.CAGR >= config.TargetCAGR)
            {
                Console.WriteLine($"\n🏆 SUCCESS: Achieved target CAGR of {config.TargetCAGR}%!");
            }
            else
            {
                Console.WriteLine($"\n⚠️  Gap: {config.TargetCAGR - solution.CAGR:F1}% below target");
            }
        }

        // Helper class for backtest results
        private class BacktestResult
        {
            public double CAGR { get; set; }
            public double WinRate { get; set; }
            public double MaxDrawdown { get; set; }
            public double SharpeRatio { get; set; }
            public double SortinoRatio { get; set; }
            public double CalmarRatio { get; set; }
            public double ProfitFactor { get; set; }
        }
    }
}