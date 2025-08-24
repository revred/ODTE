namespace ODTE.Strategy.CDTE.Oil.Convergence
{
    /// <summary>
    /// Genetic Convergence Engine for Oil CDTE Strategy
    /// Combines top 16 mutations to evolve toward single optimal model
    /// Target: >80% win rate, controlled drawdown, maximized CAGR
    /// </summary>
    public class OilGeneticConvergence
    {
        // Configuration for convergence
        public class ConvergenceConfig
        {
            public int PopulationSize { get; set; } = 100;
            public int EliteCount { get; set; } = 10;
            public double MutationRate { get; set; } = 0.15;
            public double CrossoverRate { get; set; } = 0.85;
            public int MaxGenerations { get; set; } = 500;
            public double TargetWinRate { get; set; } = 0.80;
            public double MaxAcceptableDrawdown { get; set; } = 0.15;
            public double ConvergenceThreshold { get; set; } = 0.001;
            public int StagnationLimit { get; set; } = 50;
        }

        // Strategy genome representing all parameters
        public class StrategyGenome
        {
            // Entry Timing Genes
            public DayOfWeek PrimaryEntryDay { get; set; }
            public string EntryTime { get; set; }
            public bool UseEIASignal { get; set; }
            public bool UseAPISignal { get; set; }
            public double EntryVolThreshold { get; set; }

            // Strike Selection Genes
            public string StrikeMethod { get; set; } // Delta, IVRank, Skew, Dynamic
            public double BaseShortDelta { get; set; }
            public double IVHighThreshold { get; set; }
            public double IVLowThreshold { get; set; }
            public double HighIVDelta { get; set; }
            public double LowIVDelta { get; set; }
            public double SpreadWidth { get; set; }
            public bool UseSkewAdjustment { get; set; }
            public double SkewMultiplier { get; set; }

            // Risk Management Genes
            public double StopLossPercent { get; set; }
            public double ProfitTarget1 { get; set; }
            public double ProfitTarget1Size { get; set; }
            public double ProfitTarget2 { get; set; }
            public double DeltaRollTrigger { get; set; }
            public int MaxRollsPerWeek { get; set; }
            public bool UseTrailingStop { get; set; }
            public double TrailingStopActivation { get; set; }

            // Exit Strategy Genes
            public DayOfWeek PrimaryExitDay { get; set; }
            public string ExitTime { get; set; }
            public bool UsePinRiskExit { get; set; }
            public double PinRiskBuffer { get; set; }
            public bool UseTimeDecayOptimal { get; set; }
            public double ThetaGammaRatioTarget { get; set; }

            // Position Sizing Genes
            public double BaseRiskPercent { get; set; }
            public double HighIVSizeReduction { get; set; }
            public double ConsecutiveLossReduction { get; set; }
            public int ConsecutiveLossThreshold { get; set; }

            // Advanced Genes
            public bool UseContangoSignal { get; set; }
            public double ContangoThreshold { get; set; }
            public bool UseCorrelationFilter { get; set; }
            public double CorrelationThreshold { get; set; }
            public string[] CorrelationAssets { get; set; }

            // Performance Metrics (calculated)
            public double? Fitness { get; set; }
            public double? CAGR { get; set; }
            public double? WinRate { get; set; }
            public double? MaxDrawdown { get; set; }
            public double? SharpeRatio { get; set; }
            public double? ProfitFactor { get; set; }
            public int? Generation { get; set; }

            public StrategyGenome Clone()
            {
                return (StrategyGenome)this.MemberwiseClone();
            }
        }

        // Top 16 performers from initial mutations
        private List<StrategyGenome> GetTop16Seeds()
        {
            var seeds = new List<StrategyGenome>();

            // OIL09 - Wednesday Pre-EIA (Best performer)
            seeds.Add(new StrategyGenome
            {
                PrimaryEntryDay = DayOfWeek.Wednesday,
                EntryTime = "09:30",
                UseEIASignal = true,
                BaseShortDelta = 0.10,
                StrikeMethod = "Delta",
                StopLossPercent = 200,
                ProfitTarget1 = 25,
                ProfitTarget1Size = 50,
                ProfitTarget2 = 50,
                PrimaryExitDay = DayOfWeek.Friday,
                ExitTime = "14:00",
                BaseRiskPercent = 2.0,
                CAGR = 42.3,
                WinRate = 71,
                MaxDrawdown = -18.5
            });

            // OIL41 - Quick Profit 25%
            seeds.Add(new StrategyGenome
            {
                PrimaryEntryDay = DayOfWeek.Monday,
                EntryTime = "10:00",
                BaseShortDelta = 0.20,
                StrikeMethod = "Delta",
                StopLossPercent = 100,
                ProfitTarget1 = 25,
                ProfitTarget1Size = 100,
                PrimaryExitDay = DayOfWeek.Thursday,
                ExitTime = "14:00",
                BaseRiskPercent = 2.0,
                CAGR = 38.7,
                WinRate = 82,
                MaxDrawdown = -12.3
            });

            // OIL25 - IV Rank Based
            seeds.Add(new StrategyGenome
            {
                PrimaryEntryDay = DayOfWeek.Monday,
                EntryTime = "10:00",
                StrikeMethod = "IVRank",
                IVHighThreshold = 50,
                HighIVDelta = 0.10,
                LowIVDelta = 0.25,
                StopLossPercent = 150,
                ProfitTarget1 = 30,
                ProfitTarget1Size = 50,
                ProfitTarget2 = 60,
                PrimaryExitDay = DayOfWeek.Friday,
                ExitTime = "10:00",
                BaseRiskPercent = 2.0,
                CAGR = 36.4,
                WinRate = 68,
                MaxDrawdown = -21.2
            });

            // OIL62 - Time Decay Optimal
            seeds.Add(new StrategyGenome
            {
                PrimaryEntryDay = DayOfWeek.Monday,
                EntryTime = "09:35",
                BaseShortDelta = 0.15,
                UseTimeDecayOptimal = true,
                ThetaGammaRatioTarget = 2.0,
                StopLossPercent = 150,
                ProfitTarget1 = 35,
                ProfitTarget1Size = 50,
                ProfitTarget2 = 70,
                PrimaryExitDay = DayOfWeek.Friday,
                ExitTime = "12:00",
                BaseRiskPercent = 1.5,
                CAGR = 35.2,
                WinRate = 73,
                MaxDrawdown = -16.8
            });

            // OIL17 - Ultra-Low Delta
            seeds.Add(new StrategyGenome
            {
                PrimaryEntryDay = DayOfWeek.Monday,
                EntryTime = "10:00",
                BaseShortDelta = 0.07,
                SpreadWidth = 2.0,
                StopLossPercent = 300,
                ProfitTarget1 = 20,
                ProfitTarget1Size = 100,
                PrimaryExitDay = DayOfWeek.Thursday,
                ExitTime = "15:00",
                BaseRiskPercent = 2.5,
                CAGR = 32.8,
                WinRate = 85,
                MaxDrawdown = -14.2
            });

            // OIL34 - Standard Stop with Trailing
            seeds.Add(new StrategyGenome
            {
                PrimaryEntryDay = DayOfWeek.Monday,
                EntryTime = "10:00",
                BaseShortDelta = 0.18,
                StopLossPercent = 100,
                UseTrailingStop = true,
                TrailingStopActivation = 50,
                ProfitTarget1 = 40,
                ProfitTarget1Size = 50,
                ProfitTarget2 = 80,
                PrimaryExitDay = DayOfWeek.Thursday,
                ExitTime = "14:00",
                BaseRiskPercent = 2.0,
                CAGR = 34.5,
                WinRate = 76,
                MaxDrawdown = -15.7
            });

            // OIL27 - Term Structure Aware
            seeds.Add(new StrategyGenome
            {
                PrimaryEntryDay = DayOfWeek.Wednesday,
                EntryTime = "10:00",
                StrikeMethod = "Dynamic",
                UseContangoSignal = true,
                ContangoThreshold = 0.5,
                BaseShortDelta = 0.16,
                StopLossPercent = 125,
                ProfitTarget1 = 30,
                ProfitTarget1Size = 60,
                ProfitTarget2 = 55,
                PrimaryExitDay = DayOfWeek.Friday,
                ExitTime = "10:00",
                BaseRiskPercent = 1.8,
                CAGR = 33.9,
                WinRate = 74,
                MaxDrawdown = -17.3
            });

            // OIL05 - Tuesday API Aware
            seeds.Add(new StrategyGenome
            {
                PrimaryEntryDay = DayOfWeek.Tuesday,
                EntryTime = "09:35",
                UseAPISignal = true,
                BaseShortDelta = 0.12,
                EntryVolThreshold = 2500,
                StopLossPercent = 175,
                ProfitTarget1 = 28,
                ProfitTarget1Size = 70,
                ProfitTarget2 = 45,
                PrimaryExitDay = DayOfWeek.Friday,
                ExitTime = "11:00",
                BaseRiskPercent = 1.7,
                CAGR = 31.6,
                WinRate = 77,
                MaxDrawdown = -19.1
            });

            // OIL49 - Thursday Morning Exit
            seeds.Add(new StrategyGenome
            {
                PrimaryEntryDay = DayOfWeek.Monday,
                EntryTime = "09:35",
                BaseShortDelta = 0.15,
                StopLossPercent = 150,
                ProfitTarget1 = 22,
                ProfitTarget1Size = 100,
                PrimaryExitDay = DayOfWeek.Thursday,
                ExitTime = "09:35",
                BaseRiskPercent = 2.2,
                CAGR = 30.8,
                WinRate = 79,
                MaxDrawdown = -13.9
            });

            // OIL38 - Delta Roll Strategy
            seeds.Add(new StrategyGenome
            {
                PrimaryEntryDay = DayOfWeek.Monday,
                EntryTime = "10:00",
                BaseShortDelta = 0.15,
                DeltaRollTrigger = 0.25,
                MaxRollsPerWeek = 2,
                StopLossPercent = 200,
                ProfitTarget1 = 35,
                ProfitTarget1Size = 50,
                ProfitTarget2 = 65,
                PrimaryExitDay = DayOfWeek.Friday,
                ExitTime = "10:00",
                BaseRiskPercent = 1.8,
                CAGR = 32.4,
                WinRate = 72,
                MaxDrawdown = -18.6
            });

            // OIL13 - Monday+Wednesday Split
            seeds.Add(new StrategyGenome
            {
                PrimaryEntryDay = DayOfWeek.Monday,
                EntryTime = "10:00",
                UseEIASignal = true,
                BaseShortDelta = 0.18,
                StopLossPercent = 120,
                ProfitTarget1 = 25,
                ProfitTarget1Size = 50,
                ProfitTarget2 = 50,
                PrimaryExitDay = DayOfWeek.Friday,
                ExitTime = "13:00",
                BaseRiskPercent = 1.0,
                CAGR = 29.7,
                WinRate = 81,
                MaxDrawdown = -11.8
            });

            // OIL26 - Skew Adjusted
            seeds.Add(new StrategyGenome
            {
                PrimaryEntryDay = DayOfWeek.Tuesday,
                EntryTime = "10:00",
                StrikeMethod = "Skew",
                BaseShortDelta = 0.15,
                UseSkewAdjustment = true,
                SkewMultiplier = 1.5,
                StopLossPercent = 140,
                ProfitTarget1 = 32,
                ProfitTarget1Size = 60,
                ProfitTarget2 = 58,
                PrimaryExitDay = DayOfWeek.Thursday,
                ExitTime = "14:00",
                BaseRiskPercent = 1.9,
                CAGR = 31.2,
                WinRate = 75,
                MaxDrawdown = -16.4
            });

            // OIL42 - Standard Profit 50%
            seeds.Add(new StrategyGenome
            {
                PrimaryEntryDay = DayOfWeek.Monday,
                EntryTime = "10:00",
                BaseShortDelta = 0.18,
                StopLossPercent = 125,
                ProfitTarget1 = 25,
                ProfitTarget1Size = 50,
                ProfitTarget2 = 50,
                UseTrailingStop = true,
                TrailingStopActivation = 40,
                PrimaryExitDay = DayOfWeek.Friday,
                ExitTime = "10:00",
                BaseRiskPercent = 2.0,
                CAGR = 30.5,
                WinRate = 78,
                MaxDrawdown = -14.6
            });

            // OIL53 - Friday Pin Risk Aware
            seeds.Add(new StrategyGenome
            {
                PrimaryEntryDay = DayOfWeek.Monday,
                EntryTime = "10:00",
                BaseShortDelta = 0.14,
                StopLossPercent = 160,
                ProfitTarget1 = 27,
                ProfitTarget1Size = 75,
                ProfitTarget2 = 48,
                PrimaryExitDay = DayOfWeek.Friday,
                ExitTime = "10:00",
                UsePinRiskExit = true,
                PinRiskBuffer = 1.0,
                BaseRiskPercent = 1.8,
                CAGR = 29.4,
                WinRate = 80,
                MaxDrawdown = -12.7
            });

            // OIL30 - Open Interest Based
            seeds.Add(new StrategyGenome
            {
                PrimaryEntryDay = DayOfWeek.Tuesday,
                EntryTime = "10:00",
                StrikeMethod = "OpenInterest",
                BaseShortDelta = 0.18,
                StopLossPercent = 135,
                ProfitTarget1 = 28,
                ProfitTarget1Size = 65,
                ProfitTarget2 = 52,
                PrimaryExitDay = DayOfWeek.Thursday,
                ExitTime = "14:00",
                BaseRiskPercent = 1.7,
                CAGR = 28.9,
                WinRate = 76,
                MaxDrawdown = -15.3
            });

            // OIL61 - Profit Cascade
            seeds.Add(new StrategyGenome
            {
                PrimaryEntryDay = DayOfWeek.Monday,
                EntryTime = "10:00",
                BaseShortDelta = 0.18,
                StopLossPercent = 145,
                ProfitTarget1 = 20,
                ProfitTarget1Size = 33,
                ProfitTarget2 = 40,
                PrimaryExitDay = DayOfWeek.Friday,
                ExitTime = "12:00",
                BaseRiskPercent = 1.6,
                CAGR = 30.1,
                WinRate = 83,
                MaxDrawdown = -13.1
            });

            return seeds;
        }

        // Fitness function prioritizing CAGR with constraints
        private double CalculateFitness(StrategyGenome genome, BacktestResult result)
        {
            // Hard constraints - return negative fitness if violated
            if (result.WinRate < 0.80)
                return -1000 + result.WinRate * 100; // Penalize but allow evolution

            if (result.MaxDrawdown < -0.15)
                return -500 - result.MaxDrawdown * 100; // Penalize high drawdown

            // Primary objective: Maximize CAGR
            double fitness = result.CAGR * 100;

            // Bonus for exceeding win rate target
            if (result.WinRate > 0.80)
                fitness += (result.WinRate - 0.80) * 500;

            // Bonus for lower drawdown
            if (result.MaxDrawdown > -0.15)
                fitness += (0.15 + result.MaxDrawdown) * 200;

            // Secondary objectives
            fitness += result.SharpeRatio * 10;
            fitness += result.ProfitFactor * 5;
            fitness += result.ConsistencyScore * 20;

            // Penalize complexity (fewer parameters is better)
            int activeParameters = CountActiveParameters(genome);
            fitness -= activeParameters * 0.5;

            return fitness;
        }

        // Crossover operator - combine two parent strategies
        private StrategyGenome Crossover(StrategyGenome parent1, StrategyGenome parent2, Random rand)
        {
            var child = new StrategyGenome();

            // Entry timing genes (tend to inherit from better performer)
            double bias = parent1.Fitness > parent2.Fitness ? 0.7 : 0.3;
            child.PrimaryEntryDay = rand.NextDouble() < bias ? parent1.PrimaryEntryDay : parent2.PrimaryEntryDay;
            child.EntryTime = rand.NextDouble() < bias ? parent1.EntryTime : parent2.EntryTime;
            child.UseEIASignal = rand.NextDouble() < 0.5 ? parent1.UseEIASignal : parent2.UseEIASignal;
            child.UseAPISignal = rand.NextDouble() < 0.5 ? parent1.UseAPISignal : parent2.UseAPISignal;

            // Strike selection genes (blend approaches)
            if (parent1.StrikeMethod == parent2.StrikeMethod)
            {
                child.StrikeMethod = parent1.StrikeMethod;
            }
            else
            {
                // If different methods, pick the one with better win rate
                child.StrikeMethod = parent1.WinRate > parent2.WinRate ?
                    parent1.StrikeMethod : parent2.StrikeMethod;
            }

            // Numeric parameters - use weighted average based on fitness
            double weight1 = parent1.Fitness ?? 0;
            double weight2 = parent2.Fitness ?? 0;
            double totalWeight = weight1 + weight2;
            if (totalWeight > 0)
            {
                weight1 /= totalWeight;
                weight2 /= totalWeight;
            }
            else
            {
                weight1 = weight2 = 0.5;
            }

            child.BaseShortDelta = parent1.BaseShortDelta * weight1 + parent2.BaseShortDelta * weight2;
            child.StopLossPercent = parent1.StopLossPercent * weight1 + parent2.StopLossPercent * weight2;
            child.ProfitTarget1 = parent1.ProfitTarget1 * weight1 + parent2.ProfitTarget1 * weight2;
            child.ProfitTarget1Size = parent1.ProfitTarget1Size * weight1 + parent2.ProfitTarget1Size * weight2;
            child.ProfitTarget2 = parent1.ProfitTarget2 * weight1 + parent2.ProfitTarget2 * weight2;

            // Risk management genes
            child.DeltaRollTrigger = parent1.DeltaRollTrigger * weight1 + parent2.DeltaRollTrigger * weight2;
            child.MaxRollsPerWeek = rand.NextDouble() < 0.5 ? parent1.MaxRollsPerWeek : parent2.MaxRollsPerWeek;
            child.UseTrailingStop = (parent1.UseTrailingStop && parent2.UseTrailingStop) ||
                                   (rand.NextDouble() < 0.3); // Bias toward trailing stops

            // Exit strategy genes
            child.PrimaryExitDay = rand.NextDouble() < bias ? parent1.PrimaryExitDay : parent2.PrimaryExitDay;
            child.ExitTime = rand.NextDouble() < bias ? parent1.ExitTime : parent2.ExitTime;
            child.UsePinRiskExit = parent1.UsePinRiskExit || parent2.UsePinRiskExit; // Inherit safety features

            // Position sizing genes
            child.BaseRiskPercent = Math.Min(parent1.BaseRiskPercent, parent2.BaseRiskPercent); // Conservative
            child.HighIVSizeReduction = Math.Max(parent1.HighIVSizeReduction, parent2.HighIVSizeReduction);

            return child;
        }

        // Mutation operator - introduce random changes
        private void Mutate(StrategyGenome genome, Random rand, double mutationStrength = 1.0)
        {
            // Entry timing mutations
            if (rand.NextDouble() < 0.1 * mutationStrength)
            {
                var days = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday };
                genome.PrimaryEntryDay = days[rand.Next(days.Length)];
            }

            if (rand.NextDouble() < 0.1 * mutationStrength)
            {
                var times = new[] { "09:30", "09:35", "10:00", "10:30", "11:00" };
                genome.EntryTime = times[rand.Next(times.Length)];
            }

            // Delta mutations (small adjustments)
            if (rand.NextDouble() < 0.15 * mutationStrength)
            {
                genome.BaseShortDelta += (rand.NextDouble() - 0.5) * 0.03;
                genome.BaseShortDelta = Math.Max(0.05, Math.Min(0.30, genome.BaseShortDelta));
            }

            // Stop loss mutations
            if (rand.NextDouble() < 0.12 * mutationStrength)
            {
                genome.StopLossPercent += (rand.NextDouble() - 0.5) * 25;
                genome.StopLossPercent = Math.Max(50, Math.Min(300, genome.StopLossPercent));
            }

            // Profit target mutations
            if (rand.NextDouble() < 0.15 * mutationStrength)
            {
                genome.ProfitTarget1 += (rand.NextDouble() - 0.5) * 5;
                genome.ProfitTarget1 = Math.Max(15, Math.Min(40, genome.ProfitTarget1));
            }

            if (rand.NextDouble() < 0.12 * mutationStrength)
            {
                genome.ProfitTarget1Size += (rand.NextDouble() - 0.5) * 20;
                genome.ProfitTarget1Size = Math.Max(25, Math.Min(100, genome.ProfitTarget1Size));
            }

            // Exit timing mutations
            if (rand.NextDouble() < 0.1 * mutationStrength)
            {
                var exitDays = new[] { DayOfWeek.Thursday, DayOfWeek.Friday };
                genome.PrimaryExitDay = exitDays[rand.Next(exitDays.Length)];
            }

            if (rand.NextDouble() < 0.1 * mutationStrength)
            {
                var exitTimes = new[] { "09:35", "10:00", "11:00", "12:00", "14:00", "15:00", "15:30" };
                genome.ExitTime = exitTimes[rand.Next(exitTimes.Length)];
            }

            // Advanced feature mutations (rare)
            if (rand.NextDouble() < 0.05 * mutationStrength)
            {
                genome.UseEIASignal = !genome.UseEIASignal;
            }

            if (rand.NextDouble() < 0.05 * mutationStrength)
            {
                genome.UseTrailingStop = !genome.UseTrailingStop;
                if (genome.UseTrailingStop)
                {
                    genome.TrailingStopActivation = 30 + rand.Next(40);
                }
            }

            if (rand.NextDouble() < 0.05 * mutationStrength)
            {
                genome.UsePinRiskExit = !genome.UsePinRiskExit;
                if (genome.UsePinRiskExit)
                {
                    genome.PinRiskBuffer = 0.5 + rand.NextDouble() * 1.5;
                }
            }
        }

        // Main convergence algorithm
        public async Task<StrategyGenome> ConvergeAsync(ConvergenceConfig config)
        {
            var rand = new Random(42); // Deterministic seed for reproducibility
            var population = InitializePopulation(config.PopulationSize, rand);

            StrategyGenome bestEver = null;
            double bestFitness = double.MinValue;
            int stagnationCount = 0;

            for (int generation = 0; generation < config.MaxGenerations; generation++)
            {
                // Evaluate fitness for entire population
                await EvaluatePopulationAsync(population);

                // Sort by fitness
                population = population.OrderByDescending(g => g.Fitness ?? double.MinValue).ToList();

                // Track best
                if (population[0].Fitness > bestFitness)
                {
                    bestEver = population[0].Clone();
                    bestFitness = population[0].Fitness.Value;
                    stagnationCount = 0;

                    LogProgress(generation, bestEver);
                }
                else
                {
                    stagnationCount++;
                }

                // Check convergence criteria
                if (CheckConvergence(population, config))
                {
                    Console.WriteLine($"Converged at generation {generation}!");
                    break;
                }

                // Check stagnation
                if (stagnationCount >= config.StagnationLimit)
                {
                    Console.WriteLine($"Stagnation detected at generation {generation}. Injecting diversity...");
                    InjectDiversity(population, rand, config.PopulationSize / 4);
                    stagnationCount = 0;
                }

                // Create next generation
                var nextGeneration = new List<StrategyGenome>();

                // Elitism - keep best performers
                for (int i = 0; i < config.EliteCount; i++)
                {
                    nextGeneration.Add(population[i].Clone());
                }

                // Generate offspring
                while (nextGeneration.Count < config.PopulationSize)
                {
                    StrategyGenome offspring;

                    if (rand.NextDouble() < config.CrossoverRate)
                    {
                        // Tournament selection for parents
                        var parent1 = TournamentSelect(population, rand, 3);
                        var parent2 = TournamentSelect(population, rand, 3);
                        offspring = Crossover(parent1, parent2, rand);
                    }
                    else
                    {
                        // Clone a random elite
                        offspring = population[rand.Next(config.EliteCount)].Clone();
                    }

                    // Apply mutation
                    if (rand.NextDouble() < config.MutationRate)
                    {
                        double mutationStrength = 1.0 - (generation / (double)config.MaxGenerations) * 0.5;
                        Mutate(offspring, rand, mutationStrength);
                    }

                    offspring.Generation = generation + 1;
                    nextGeneration.Add(offspring);
                }

                population = nextGeneration;
            }

            return bestEver;
        }

        // Initialize population with top 16 seeds and random variations
        private List<StrategyGenome> InitializePopulation(int size, Random rand)
        {
            var population = new List<StrategyGenome>();
            var seeds = GetTop16Seeds();

            // Add all seeds
            population.AddRange(seeds);

            // Fill rest with mutated versions of seeds
            while (population.Count < size)
            {
                var seed = seeds[rand.Next(seeds.Count)];
                var variant = seed.Clone();

                // Apply multiple mutations for diversity
                for (int i = 0; i < rand.Next(1, 5); i++)
                {
                    Mutate(variant, rand, 1.5); // Stronger initial mutations
                }

                variant.Generation = 0;
                population.Add(variant);
            }

            return population;
        }

        // Evaluate fitness for entire population
        private async Task EvaluatePopulationAsync(List<StrategyGenome> population)
        {
            // In production, this would run actual backtests
            // For now, simulate with heuristic evaluation

            foreach (var genome in population.Where(g => !g.Fitness.HasValue))
            {
                var result = SimulateBacktest(genome);
                genome.Fitness = CalculateFitness(genome, result);
                genome.CAGR = result.CAGR;
                genome.WinRate = result.WinRate;
                genome.MaxDrawdown = result.MaxDrawdown;
                genome.SharpeRatio = result.SharpeRatio;
                genome.ProfitFactor = result.ProfitFactor;
            }

            await Task.CompletedTask;
        }

        // Simulate backtest (simplified heuristic model)
        private BacktestResult SimulateBacktest(StrategyGenome genome)
        {
            // Base performance from delta
            double baseWinRate = 0.95 - genome.BaseShortDelta * 2;
            double baseCAGR = 20 + (0.20 - genome.BaseShortDelta) * 100;

            // Adjust for profit targets
            if (genome.ProfitTarget1 < 30)
            {
                baseWinRate += 0.05;
                baseCAGR -= 5;
            }
            else if (genome.ProfitTarget1 > 40)
            {
                baseWinRate -= 0.05;
                baseCAGR += 3;
            }

            // Adjust for stop loss
            if (genome.StopLossPercent < 100)
            {
                baseWinRate -= 0.08;
                baseCAGR -= 8;
            }
            else if (genome.StopLossPercent > 200)
            {
                baseWinRate += 0.03;
                baseCAGR += 2;
            }

            // Wednesday EIA bonus
            if (genome.PrimaryEntryDay == DayOfWeek.Wednesday && genome.UseEIASignal)
            {
                baseCAGR += 8;
                baseWinRate -= 0.02; // Slightly more risk
            }

            // Thursday exit bonus (avoid Friday gamma)
            if (genome.PrimaryExitDay == DayOfWeek.Thursday)
            {
                baseWinRate += 0.04;
                baseCAGR -= 2;
            }

            // Trailing stop bonus
            if (genome.UseTrailingStop)
            {
                baseWinRate += 0.02;
                baseCAGR += 1;
            }

            // Calculate drawdown based on risk parameters
            double baseDrawdown = -5 - genome.BaseShortDelta * 50;
            baseDrawdown *= (genome.StopLossPercent / 100);
            baseDrawdown = Math.Max(baseDrawdown, -30);

            // Calculate other metrics
            double sharpe = baseCAGR / Math.Abs(baseDrawdown) * 0.5;
            double profitFactor = baseWinRate > 0.5 ?
                (baseWinRate * 1.5) / ((1 - baseWinRate) * 1.0) : 0.8;

            return new BacktestResult
            {
                CAGR = Math.Max(0, baseCAGR + (rand.NextDouble() - 0.5) * 5),
                WinRate = Math.Min(0.95, Math.Max(0.5, baseWinRate + (rand.NextDouble() - 0.5) * 0.05)),
                MaxDrawdown = baseDrawdown + (rand.NextDouble() - 0.5) * 3,
                SharpeRatio = Math.Max(0, sharpe + (rand.NextDouble() - 0.5) * 0.3),
                ProfitFactor = Math.Max(0.5, profitFactor + (rand.NextDouble() - 0.5) * 0.2),
                ConsistencyScore = 0.7 + (rand.NextDouble() - 0.5) * 0.2
            };
        }

        private Random rand = new Random();

        // Tournament selection
        private StrategyGenome TournamentSelect(List<StrategyGenome> population, Random rand, int tournamentSize)
        {
            StrategyGenome best = null;

            for (int i = 0; i < tournamentSize; i++)
            {
                var candidate = population[rand.Next(population.Count)];
                if (best == null || candidate.Fitness > best.Fitness)
                {
                    best = candidate;
                }
            }

            return best;
        }

        // Check convergence criteria
        private bool CheckConvergence(List<StrategyGenome> population, ConvergenceConfig config)
        {
            // Check if best meets all criteria
            var best = population[0];
            if (best.WinRate >= config.TargetWinRate &&
                best.MaxDrawdown >= -config.MaxAcceptableDrawdown &&
                best.CAGR > 35) // Minimum CAGR target
            {
                // Check population convergence (low variance)
                var top10 = population.Take(10).ToList();
                var avgFitness = top10.Average(g => g.Fitness ?? 0);
                var variance = top10.Select(g => Math.Pow((g.Fitness ?? 0) - avgFitness, 2)).Average();
                var stdDev = Math.Sqrt(variance);

                if (stdDev / avgFitness < config.ConvergenceThreshold)
                {
                    return true;
                }
            }

            return false;
        }

        // Inject diversity when stagnant
        private void InjectDiversity(List<StrategyGenome> population, Random rand, int count)
        {
            var seeds = GetTop16Seeds();

            for (int i = population.Count - count; i < population.Count; i++)
            {
                if (rand.NextDouble() < 0.5)
                {
                    // Replace with heavily mutated seed
                    var seed = seeds[rand.Next(seeds.Count)];
                    population[i] = seed.Clone();
                    for (int j = 0; j < 5; j++)
                    {
                        Mutate(population[i], rand, 2.0); // Strong mutations
                    }
                }
                else
                {
                    // Crossover between distant individuals
                    var parent1 = population[rand.Next(10)]; // Top performer
                    var parent2 = population[population.Count - rand.Next(10) - 1]; // Bottom performer
                    population[i] = Crossover(parent1, parent2, rand);
                    Mutate(population[i], rand, 1.5);
                }

                population[i].Fitness = null; // Force re-evaluation
            }
        }

        // Count active parameters for complexity penalty
        private int CountActiveParameters(StrategyGenome genome)
        {
            int count = 5; // Base parameters always active

            if (genome.UseEIASignal) count++;
            if (genome.UseAPISignal) count++;
            if (genome.UseSkewAdjustment) count += 2;
            if (genome.UseTrailingStop) count += 2;
            if (genome.UsePinRiskExit) count += 2;
            if (genome.UseTimeDecayOptimal) count += 2;
            if (genome.UseContangoSignal) count += 2;
            if (genome.UseCorrelationFilter) count += 3;
            if (genome.MaxRollsPerWeek > 0) count += 2;

            return count;
        }

        // Log progress
        private void LogProgress(int generation, StrategyGenome best)
        {
            Console.WriteLine($"\n=== Generation {generation} - New Best ===");
            Console.WriteLine($"Fitness: {best.Fitness:F2}");
            Console.WriteLine($"CAGR: {best.CAGR:F1}%");
            Console.WriteLine($"Win Rate: {best.WinRate:P1}");
            Console.WriteLine($"Max Drawdown: {best.MaxDrawdown:F1}%");
            Console.WriteLine($"Sharpe Ratio: {best.SharpeRatio:F2}");
            Console.WriteLine($"Entry: {best.PrimaryEntryDay} {best.EntryTime}");
            Console.WriteLine($"Exit: {best.PrimaryExitDay} {best.ExitTime}");
            Console.WriteLine($"Delta: {best.BaseShortDelta:F3}");
            Console.WriteLine($"Stop: {best.StopLossPercent:F0}%");
            Console.WriteLine($"Target1: {best.ProfitTarget1:F0}% ({best.ProfitTarget1Size:F0}% size)");
        }

        // Helper classes
        private class BacktestResult
        {
            public double CAGR { get; set; }
            public double WinRate { get; set; }
            public double MaxDrawdown { get; set; }
            public double SharpeRatio { get; set; }
            public double ProfitFactor { get; set; }
            public double ConsistencyScore { get; set; }
        }
    }
}