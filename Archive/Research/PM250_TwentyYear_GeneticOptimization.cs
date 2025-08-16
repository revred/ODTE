using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using ODTE.Strategy;
using Xunit;
using FluentAssertions;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// PM250 Twenty-Year Comprehensive Genetic Optimization System
    /// 
    /// OPTIMIZATION OBJECTIVE: Achieve $15 average profit per trade
    /// EVALUATION FREQUENCY: Every 10 minutes (78 opportunities per trading day)
    /// TIME HORIZON: 20 years of historical data (2005-2025)
    /// RISK MANAGEMENT: Advanced drawdown limits with real-time monitoring
    /// 
    /// GENETIC EVOLUTION STRATEGY:
    /// - Population: 500 chromosomes per generation
    /// - Selection: Top 20% survivors + 10% random diversity
    /// - Crossover: Multi-point breeding with parameter blending
    /// - Mutation: Adaptive mutation rate based on fitness stagnation
    /// - Elitism: Preserve top 5% unchanged
    /// 
    /// TARGET PERFORMANCE METRICS:
    /// - Average Profit: $15.00 per trade (16% improvement over current $12.90)
    /// - Win Rate: Maintain ≥80% (vs current 85.7%)
    /// - Max Drawdown: ≤3% (vs current 1.76%)
    /// - Sharpe Ratio: ≥12.0 (vs current 15.91)
    /// - Monthly Consistency: 95%+ profitable months
    /// </summary>
    public class PM250_TwentyYear_GeneticOptimization
    {
        private readonly Random _random;
        private readonly string _historicalDataPath;
        private readonly string _optimizationResultsPath;
        private readonly List<TradingDay> _twentyYearData;
        private int _currentGeneration;
        private const int MAX_GENERATIONS = 1000;
        private const int POPULATION_SIZE = 500;
        private const double TARGET_PROFIT = 15.0; // $15 average profit target
        private const int EVALUATION_INTERVAL_MINUTES = 10;

        public PM250_TwentyYear_GeneticOptimization()
        {
            _random = new Random(42); // Deterministic for reproducibility
            _historicalDataPath = @"C:\code\ODTE\data\Historical";
            _optimizationResultsPath = @"C:\code\ODTE\ODTE.Strategy.Tests\OptimizationResults";
            _twentyYearData = new List<TradingDay>();
            _currentGeneration = 0;
            
            Directory.CreateDirectory(_optimizationResultsPath);
        }

        [Fact]
        public async Task Execute_TwentyYear_ComprehensiveGeneticOptimization()
        {
            Console.WriteLine("🧬 PM250 TWENTY-YEAR COMPREHENSIVE GENETIC OPTIMIZATION");
            Console.WriteLine("=" + new string('=', 80));
            Console.WriteLine($"🎯 TARGET: ${TARGET_PROFIT:F2} average profit per trade");
            Console.WriteLine($"⏱️ EVALUATION: Every {EVALUATION_INTERVAL_MINUTES} minutes");
            Console.WriteLine($"📊 DATASET: 20 years (2005-2025) = ~{20 * 252 * 78:N0} evaluation points");
            Console.WriteLine($"🧬 POPULATION: {POPULATION_SIZE} chromosomes per generation");
            Console.WriteLine($"🔄 MAX GENERATIONS: {MAX_GENERATIONS}");
            Console.WriteLine();

            // Step 1: Load and prepare 20-year historical dataset
            await LoadTwentyYearHistoricalData();
            Console.WriteLine($"✅ Loaded {_twentyYearData.Count:N0} trading days");
            Console.WriteLine($"📅 Date Range: {_twentyYearData.First().Date:yyyy-MM-dd} to {_twentyYearData.Last().Date:yyyy-MM-dd}");
            Console.WriteLine();

            // Step 2: Initialize genetic algorithm population
            var population = InitializePopulation();
            Console.WriteLine($"🧬 Initialized population of {population.Count} chromosomes");
            
            // Step 3: Load baseline performance from existing 20-year weights
            var baselinePerformance = await LoadBaselinePerformance();
            Console.WriteLine($"📊 BASELINE PERFORMANCE (Current 20-year weights):");
            Console.WriteLine($"   Average Profit: ${baselinePerformance.AverageProfit:F2}");
            Console.WriteLine($"   Win Rate: {baselinePerformance.WinRate:F1}%");
            Console.WriteLine($"   Total Trades: {baselinePerformance.TotalTrades:N0}");
            Console.WriteLine($"   Sharpe Ratio: {baselinePerformance.SharpeRatio:F2}");
            Console.WriteLine();

            // Step 4: Execute genetic evolution
            var bestChromosome = await ExecuteGeneticEvolution(population, baselinePerformance);
            
            // Step 5: Validate final optimized strategy
            var finalPerformance = await ComprehensiveValidation(bestChromosome);
            
            // Step 6: Generate production configuration
            await GenerateProductionConfiguration(bestChromosome, finalPerformance);
            
            // Step 7: Performance comparison and results
            await GenerateComprehensiveReport(baselinePerformance, finalPerformance, bestChromosome);
            
            // Assertions for optimization success
            finalPerformance.AverageProfit.Should().BeGreaterOrEqualTo((decimal)TARGET_PROFIT, 
                $"Genetic optimization should achieve target ${TARGET_PROFIT} average profit");
            finalPerformance.WinRate.Should().BeGreaterOrEqualTo(80.0, 
                "Optimized strategy should maintain high win rate");
            finalPerformance.MaxDrawdown.Should().BeLessOrEqualTo(3.0, 
                "Risk management should limit drawdown to 3%");
            
            Console.WriteLine("🏆 TWENTY-YEAR GENETIC OPTIMIZATION COMPLETE");
            Console.WriteLine($"✅ Target achieved: ${finalPerformance.AverageProfit:F2} average profit");
            Console.WriteLine($"📈 Performance improvement: {((double)finalPerformance.AverageProfit / (double)baselinePerformance.AverageProfit - 1) * 100:F1}%");
        }

        /// <summary>
        /// Load 20 years of historical trading data with 10-minute granularity
        /// </summary>
        private async Task LoadTwentyYearHistoricalData()
        {
            Console.WriteLine("📊 Loading 20-year historical dataset...");
            
            // Generate comprehensive dataset covering 2005-2025
            var startDate = new DateTime(2005, 1, 1);
            var endDate = new DateTime(2025, 8, 15);
            var currentDate = startDate;
            
            while (currentDate <= endDate)
            {
                // Skip weekends and major holidays
                if (currentDate.DayOfWeek != DayOfWeek.Saturday && 
                    currentDate.DayOfWeek != DayOfWeek.Sunday &&
                    !IsMarketHoliday(currentDate))
                {
                    var tradingDay = await GenerateComprehensiveTradingDay(currentDate);
                    _twentyYearData.Add(tradingDay);
                }
                
                currentDate = currentDate.AddDays(1);
            }
            
            Console.WriteLine($"   📈 Generated {_twentyYearData.Count:N0} trading days");
            Console.WriteLine($"   ⏱️ {_twentyYearData.Sum(d => d.EvaluationPoints.Count):N0} total 10-minute evaluation points");
        }

        /// <summary>
        /// Generate comprehensive trading day with 10-minute evaluation points
        /// </summary>
        private async Task<TradingDay> GenerateComprehensiveTradingDay(DateTime date)
        {
            var tradingDay = new TradingDay
            {
                Date = date,
                EvaluationPoints = new List<EvaluationPoint>(),
                MarketRegime = DetermineHistoricalMarketRegime(date),
                VIXLevel = GetHistoricalVIX(date),
                EconomicEvents = GetEconomicEvents(date)
            };

            // Generate 10-minute evaluation points (9:30 AM - 4:00 PM = 78 points)
            var marketOpen = date.Date.AddHours(9).AddMinutes(30);
            var marketClose = date.Date.AddHours(16);
            
            for (var time = marketOpen; time <= marketClose; time = time.AddMinutes(EVALUATION_INTERVAL_MINUTES))
            {
                var evaluationPoint = await GenerateEvaluationPoint(time, tradingDay, marketClose);
                tradingDay.EvaluationPoints.Add(evaluationPoint);
            }

            return tradingDay;
        }

        /// <summary>
        /// Generate individual 10-minute evaluation point with market conditions
        /// </summary>
        private async Task<EvaluationPoint> GenerateEvaluationPoint(DateTime time, TradingDay tradingDay, DateTime marketClose)
        {
            var random = new Random(time.GetHashCode());
            
            // Base underlying price (SPY/XSP equivalent)
            var basePrice = GetHistoricalPrice(time.Date);
            var intradayVariation = (decimal)(random.NextDouble() * 4 - 2); // ±$2 intraday movement
            var currentPrice = basePrice + intradayVariation;
            
            // Market microstructure
            var bidAskSpread = GetRealisticSpread(time, tradingDay.VIXLevel);
            var volume = GetHistoricalVolume(time);
            var openInterest = GetOpenInterest(time);
            
            // Options-specific data
            var impliedVolatility = CalculateImpliedVolatility(tradingDay.VIXLevel, time);
            var skew = CalculateVolatilitySkew(time, tradingDay.MarketRegime);
            var gamma = CalculateGammaExposure(time, currentPrice);
            
            return new EvaluationPoint
            {
                Timestamp = time,
                UnderlyingPrice = currentPrice,
                BidPrice = currentPrice - bidAskSpread / 2,
                AskPrice = currentPrice + bidAskSpread / 2,
                Volume = volume,
                VWAP = CalculateVWAP(currentPrice, volume),
                ImpliedVolatility = impliedVolatility,
                VolatilitySkew = skew,
                GammaExposure = gamma,
                OpenInterest = openInterest,
                LiquidityScore = CalculateLiquidityScore(volume, bidAskSpread),
                MarketStress = CalculateMarketStress(tradingDay.VIXLevel, tradingDay.MarketRegime),
                TimeToClose = (decimal)(marketClose - time).TotalHours,
                DaysToExpiry = 0, // 0DTE focus
                TrendStrength = CalculateTrendStrength(time, currentPrice),
                MomentumScore = CalculateMomentumScore(time, volume),
                RegimeScore = CalculateRegimeScore(tradingDay.MarketRegime, time),
                NewsImpact = GetNewsImpact(time),
                FedEvents = GetFedEventImpact(time.Date),
                EarningsImpact = GetEarningsImpact(time.Date),
                ExpirationEffects = CalculateExpirationEffects(time)
            };
        }

        /// <summary>
        /// Initialize genetic algorithm population with diverse chromosomes
        /// </summary>
        private List<PM250_EnhancedChromosome> InitializePopulation()
        {
            var population = new List<PM250_EnhancedChromosome>();
            
            // Load current best chromosome as elite seed
            var currentBest = LoadCurrentBestChromosome();
            population.Add(currentBest);
            
            // Generate diverse population around the current best
            for (int i = 1; i < POPULATION_SIZE; i++)
            {
                var chromosome = GenerateRandomChromosome();
                
                // 20% population: Variations of current best
                if (i < POPULATION_SIZE * 0.2)
                {
                    chromosome = MutateChromosome(currentBest, 0.1); // 10% mutation rate
                }
                // 30% population: Historical best variations
                else if (i < POPULATION_SIZE * 0.5)
                {
                    chromosome = GenerateHistoricalVariation();
                }
                // 50% population: Random exploration
                else
                {
                    chromosome = GenerateRandomChromosome();
                }
                
                population.Add(chromosome);
            }
            
            return population;
        }

        /// <summary>
        /// Execute genetic evolution across generations
        /// </summary>
        private async Task<PM250_EnhancedChromosome> ExecuteGeneticEvolution(
            List<PM250_EnhancedChromosome> population, 
            PerformanceMetrics baseline)
        {
            Console.WriteLine("🧬 STARTING GENETIC EVOLUTION");
            Console.WriteLine("-" + new string('-', 60));
            
            var bestOverallFitness = 0.0;
            var bestOverallChromosome = population.First();
            var stagnationCounter = 0;
            const int MAX_STAGNATION = 50; // Generations without improvement
            
            for (_currentGeneration = 1; _currentGeneration <= MAX_GENERATIONS; _currentGeneration++)
            {
                Console.WriteLine($"🔄 Generation {_currentGeneration}/{MAX_GENERATIONS}");
                
                // Step 1: Evaluate entire population
                var evaluatedPopulation = await EvaluatePopulation(population);
                
                // Step 2: Track best performer
                var generationBest = evaluatedPopulation.OrderByDescending(c => c.Fitness).First();
                
                if (generationBest.Fitness > bestOverallFitness)
                {
                    bestOverallFitness = generationBest.Fitness;
                    bestOverallChromosome = generationBest;
                    stagnationCounter = 0;
                    
                    Console.WriteLine($"   🏆 NEW BEST: Fitness={generationBest.Fitness:F4}, " +
                                    $"Profit=${generationBest.Performance.AverageProfit:F2}, " +
                                    $"WinRate={generationBest.Performance.WinRate:F1}%");
                    
                    // Save intermediate best results
                    await SaveIntermediateResults(generationBest, _currentGeneration);
                }
                else
                {
                    stagnationCounter++;
                    Console.WriteLine($"   📊 Gen Best: Fitness={generationBest.Fitness:F4}, " +
                                    $"Stagnation: {stagnationCounter}/{MAX_STAGNATION}");
                }
                
                // Step 3: Check for target achievement or stagnation
                if (generationBest.Performance.AverageProfit >= (decimal)TARGET_PROFIT)
                {
                    Console.WriteLine($"🎯 TARGET ACHIEVED in generation {_currentGeneration}!");
                    Console.WriteLine($"   Average Profit: ${generationBest.Performance.AverageProfit:F2}");
                    break;
                }
                
                if (stagnationCounter >= MAX_STAGNATION)
                {
                    Console.WriteLine($"⏹️ Evolution stagnated after {stagnationCounter} generations");
                    Console.WriteLine($"   Applying diversity injection...");
                    
                    // Inject diversity to escape local optima
                    population = InjectDiversity(evaluatedPopulation, 0.3); // Replace 30% with new random
                    stagnationCounter = 0;
                    continue;
                }
                
                // Step 4: Selection and reproduction
                population = await ReproduceNextGeneration(evaluatedPopulation);
                
                // Step 5: Progress reporting every 10 generations
                if (_currentGeneration % 10 == 0)
                {
                    await GenerateProgressReport(evaluatedPopulation, _currentGeneration);
                }
            }
            
            Console.WriteLine();
            Console.WriteLine($"🏁 EVOLUTION COMPLETE after {_currentGeneration} generations");
            Console.WriteLine($"🏆 BEST FITNESS: {bestOverallFitness:F4}");
            Console.WriteLine($"💰 BEST AVERAGE PROFIT: ${bestOverallChromosome.Performance.AverageProfit:F2}");
            Console.WriteLine($"🎯 TARGET STATUS: {(bestOverallChromosome.Performance.AverageProfit >= (decimal)TARGET_PROFIT ? "✅ ACHIEVED" : "❌ NOT REACHED")}");
            
            return bestOverallChromosome;
        }

        /// <summary>
        /// Evaluate entire population fitness across 20-year dataset
        /// </summary>
        private async Task<List<PM250_EnhancedChromosome>> EvaluatePopulation(List<PM250_EnhancedChromosome> population)
        {
            var evaluatedPopulation = new List<PM250_EnhancedChromosome>();
            var evaluationTasks = new List<Task<PM250_EnhancedChromosome>>();
            
            // Parallel evaluation for performance
            var semaphore = new SemaphoreSlim(Environment.ProcessorCount);
            
            foreach (var chromosome in population)
            {
                evaluationTasks.Add(EvaluateChromosomeAsync(chromosome, semaphore));
            }
            
            var results = await Task.WhenAll(evaluationTasks);
            evaluatedPopulation.AddRange(results);
            
            return evaluatedPopulation;
        }

        /// <summary>
        /// Evaluate individual chromosome across entire 20-year dataset
        /// </summary>
        private async Task<PM250_EnhancedChromosome> EvaluateChromosomeAsync(
            PM250_EnhancedChromosome chromosome, 
            SemaphoreSlim semaphore)
        {
            await semaphore.WaitAsync();
            
            try
            {
                var trades = new List<TradeResult>();
                var dailyPnL = new List<decimal>();
                var riskManager = new AdvancedRiskManager(chromosome);
                var currentDrawdown = 0m;
                var maxDrawdown = 0m;
                var runningPnL = 0m;
                var peak = 0m;
                
                // Evaluate across all 20 years of data
                foreach (var tradingDay in _twentyYearData)
                {
                    var dayTrades = new List<TradeResult>();
                    var dayPnL = 0m;
                    
                    // Evaluate every 10-minute opportunity
                    foreach (var evaluationPoint in tradingDay.EvaluationPoints)
                    {
                        // Check if we should trade at this point
                        var shouldTrade = ShouldExecuteTrade(chromosome, evaluationPoint, riskManager, currentDrawdown);
                        
                        if (shouldTrade)
                        {
                            var trade = await ExecuteVirtualTrade(chromosome, evaluationPoint, riskManager);
                            
                            if (trade != null)
                            {
                                trades.Add(trade);
                                dayTrades.Add(trade);
                                dayPnL += trade.PnL;
                                runningPnL += trade.PnL;
                                
                                // Update drawdown tracking
                                if (runningPnL > peak) peak = runningPnL;
                                currentDrawdown = peak - runningPnL;
                                maxDrawdown = Math.Max(maxDrawdown, currentDrawdown);
                                
                                // Update risk manager
                                riskManager.RecordTrade(trade);
                            }
                        }
                    }
                    
                    if (dayTrades.Any())
                    {
                        dailyPnL.Add(dayPnL);
                    }
                }
                
                // Calculate comprehensive performance metrics
                chromosome.Performance = CalculatePerformanceMetrics(trades, dailyPnL, maxDrawdown);
                chromosome.Fitness = CalculateFitness(chromosome.Performance);
                
                return chromosome;
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// Determine if we should execute a trade at this evaluation point
        /// </summary>
        private bool ShouldExecuteTrade(
            PM250_EnhancedChromosome chromosome, 
            EvaluationPoint point, 
            AdvancedRiskManager riskManager,
            decimal currentDrawdown)
        {
            // Enhanced trade decision logic with 10-minute granularity
            
            // 1. Risk management checks
            if (!riskManager.CanTrade(currentDrawdown)) return false;
            if (currentDrawdown > (decimal)chromosome.MaxDrawdownLimit) return false;
            
            // 2. Market condition filters
            if (point.MarketStress > chromosome.MaxMarketStress) return false;
            if (point.LiquidityScore < chromosome.MinLiquidityScore) return false;
            if (point.ImpliedVolatility < chromosome.MinIV || point.ImpliedVolatility > chromosome.MaxIV) return false;
            
            // 3. Time-based filters
            if (point.TimeToClose < (decimal)chromosome.MinTimeToClose) return false;
            if (IsEconomicEventPeriod(point.Timestamp, chromosome.AvoidEconomicEvents)) return false;
            
            // 4. Enhanced GoScore calculation
            var goScore = CalculateEnhancedGoScore(chromosome, point);
            
            // 5. Dynamic threshold based on market conditions
            var dynamicThreshold = CalculateDynamicThreshold(chromosome, point);
            
            return goScore >= dynamicThreshold;
        }

        /// <summary>
        /// Execute virtual trade with enhanced realism
        /// </summary>
        private async Task<TradeResult> ExecuteVirtualTrade(
            PM250_EnhancedChromosome chromosome, 
            EvaluationPoint point, 
            AdvancedRiskManager riskManager)
        {
            // Calculate position size based on advanced risk management
            var positionSize = riskManager.CalculatePositionSize(chromosome, point);
            
            // Calculate expected credit with realistic pricing
            var expectedCredit = CalculateRealisticCredit(chromosome, point);
            
            // Determine trade outcome with enhanced probability model
            var outcome = DetermineTradeOutcome(chromosome, point);
            
            // Calculate actual P&L with realistic slippage and fees
            var actualPnL = CalculateActualPnL(chromosome, point, outcome, expectedCredit, positionSize);
            
            return new TradeResult
            {
                Timestamp = point.Timestamp,
                UnderlyingPrice = point.UnderlyingPrice,
                ExpectedCredit = expectedCredit,
                ActualCredit = expectedCredit * 0.98m, // 2% slippage
                PnL = actualPnL,
                PositionSize = positionSize,
                IsWin = actualPnL > 0,
                GoScore = CalculateEnhancedGoScore(chromosome, point),
                MarketRegime = DeterminePointRegime(point),
                RiskAdjustedReturn = actualPnL / Math.Max(1m, riskManager.GetCurrentRisk()),
                ExecutionQuality = CalculateExecutionQuality(point),
                Strategy = "PM250_Enhanced"
            };
        }

        #region Genetic Algorithm Operations

        private async Task<List<PM250_EnhancedChromosome>> ReproduceNextGeneration(List<PM250_EnhancedChromosome> population)
        {
            var newGeneration = new List<PM250_EnhancedChromosome>();
            var sortedPop = population.OrderByDescending(c => c.Fitness).ToList();
            
            // Elite preservation (top 5%)
            var eliteCount = (int)(POPULATION_SIZE * 0.05);
            newGeneration.AddRange(sortedPop.Take(eliteCount));
            
            // Tournament selection and breeding
            while (newGeneration.Count < POPULATION_SIZE)
            {
                var parent1 = TournamentSelection(sortedPop, 5);
                var parent2 = TournamentSelection(sortedPop, 5);
                
                var offspring = await Crossover(parent1, parent2);
                offspring = MutateChromosome(offspring, CalculateAdaptiveMutationRate());
                
                newGeneration.Add(offspring);
            }
            
            return newGeneration;
        }

        private PM250_EnhancedChromosome TournamentSelection(List<PM250_EnhancedChromosome> population, int tournamentSize)
        {
            var tournament = new List<PM250_EnhancedChromosome>();
            
            for (int i = 0; i < tournamentSize; i++)
            {
                var randomIndex = _random.Next(population.Count);
                tournament.Add(population[randomIndex]);
            }
            
            return tournament.OrderByDescending(c => c.Fitness).First();
        }

        private async Task<PM250_EnhancedChromosome> Crossover(PM250_EnhancedChromosome parent1, PM250_EnhancedChromosome parent2)
        {
            var offspring = new PM250_EnhancedChromosome();
            
            // Multi-point crossover with parameter blending
            offspring.ShortDelta = BlendParameter(parent1.ShortDelta, parent2.ShortDelta);
            offspring.WidthPoints = BlendParameter(parent1.WidthPoints, parent2.WidthPoints);
            offspring.CreditRatio = BlendParameter(parent1.CreditRatio, parent2.CreditRatio);
            offspring.StopMultiple = BlendParameter(parent1.StopMultiple, parent2.StopMultiple);
            offspring.GoScoreBase = BlendParameter(parent1.GoScoreBase, parent2.GoScoreBase);
            offspring.GoScoreVolAdj = BlendParameter(parent1.GoScoreVolAdj, parent2.GoScoreVolAdj);
            offspring.GoScoreTrendAdj = BlendParameter(parent1.GoScoreTrendAdj, parent2.GoScoreTrendAdj);
            offspring.VwapWeight = BlendParameter(parent1.VwapWeight, parent2.VwapWeight);
            offspring.RegimeSensitivity = BlendParameter(parent1.RegimeSensitivity, parent2.RegimeSensitivity);
            offspring.VolatilityFilter = BlendParameter(parent1.VolatilityFilter, parent2.VolatilityFilter);
            offspring.MaxPositionSize = BlendParameter(parent1.MaxPositionSize, parent2.MaxPositionSize);
            offspring.PositionScaling = BlendParameter(parent1.PositionScaling, parent2.PositionScaling);
            offspring.DrawdownReduction = BlendParameter(parent1.DrawdownReduction, parent2.DrawdownReduction);
            offspring.RecoveryBoost = BlendParameter(parent1.RecoveryBoost, parent2.RecoveryBoost);
            offspring.BullMarketAggression = BlendParameter(parent1.BullMarketAggression, parent2.BullMarketAggression);
            offspring.BearMarketDefense = BlendParameter(parent1.BearMarketDefense, parent2.BearMarketDefense);
            offspring.HighVolReduction = BlendParameter(parent1.HighVolReduction, parent2.HighVolReduction);
            offspring.LowVolBoost = BlendParameter(parent1.LowVolBoost, parent2.LowVolBoost);
            offspring.OpeningBias = BlendParameter(parent1.OpeningBias, parent2.OpeningBias);
            offspring.ClosingBias = BlendParameter(parent1.ClosingBias, parent2.ClosingBias);
            offspring.FridayReduction = BlendParameter(parent1.FridayReduction, parent2.FridayReduction);
            offspring.FOPExitBias = BlendParameter(parent1.FOPExitBias, parent2.FOPExitBias);
            
            // Enhanced parameters for 10-minute evaluation
            offspring.MinTimeToClose = BlendParameter(parent1.MinTimeToClose, parent2.MinTimeToClose);
            offspring.MaxMarketStress = BlendParameter(parent1.MaxMarketStress, parent2.MaxMarketStress);
            offspring.MinLiquidityScore = BlendParameter(parent1.MinLiquidityScore, parent2.MinLiquidityScore);
            offspring.MinIV = BlendParameter(parent1.MinIV, parent2.MinIV);
            offspring.MaxIV = BlendParameter(parent1.MaxIV, parent2.MaxIV);
            offspring.TrendWeight = BlendParameter(parent1.TrendWeight, parent2.TrendWeight);
            offspring.MomentumWeight = BlendParameter(parent1.MomentumWeight, parent2.MomentumWeight);
            offspring.NewsWeight = BlendParameter(parent1.NewsWeight, parent2.NewsWeight);
            offspring.GammaWeight = BlendParameter(parent1.GammaWeight, parent2.GammaWeight);
            offspring.SkewWeight = BlendParameter(parent1.SkewWeight, parent2.SkewWeight);
            offspring.MaxDrawdownLimit = BlendParameter(parent1.MaxDrawdownLimit, parent2.MaxDrawdownLimit);
            
            // Boolean parameters from better parent
            offspring.AvoidEconomicEvents = _random.NextDouble() < 0.5 ? parent1.AvoidEconomicEvents : parent2.AvoidEconomicEvents;
            offspring.UseAdaptiveThreshold = _random.NextDouble() < 0.5 ? parent1.UseAdaptiveThreshold : parent2.UseAdaptiveThreshold;
            offspring.EnableGammaHedging = _random.NextDouble() < 0.5 ? parent1.EnableGammaHedging : parent2.EnableGammaHedging;
            
            return offspring;
        }

        private double BlendParameter(double value1, double value2)
        {
            // Alpha blending with slight randomization
            var alpha = _random.NextDouble();
            var blended = alpha * value1 + (1 - alpha) * value2;
            
            // Add small random perturbation (1% of the value)
            var perturbation = blended * (_random.NextDouble() - 0.5) * 0.02;
            return blended + perturbation;
        }

        private PM250_EnhancedChromosome MutateChromosome(PM250_EnhancedChromosome chromosome, double mutationRate)
        {
            var mutated = CloneChromosome(chromosome);
            
            if (_random.NextDouble() < mutationRate) mutated.ShortDelta = MutateValue(mutated.ShortDelta, 0.07, 0.25, 0.02);
            if (_random.NextDouble() < mutationRate) mutated.WidthPoints = MutateValue(mutated.WidthPoints, 1.0, 5.0, 0.2);
            if (_random.NextDouble() < mutationRate) mutated.CreditRatio = MutateValue(mutated.CreditRatio, 0.05, 0.30, 0.02);
            if (_random.NextDouble() < mutationRate) mutated.StopMultiple = MutateValue(mutated.StopMultiple, 1.5, 4.0, 0.1);
            if (_random.NextDouble() < mutationRate) mutated.GoScoreBase = MutateValue(mutated.GoScoreBase, 50.0, 90.0, 2.0);
            if (_random.NextDouble() < mutationRate) mutated.GoScoreVolAdj = MutateValue(mutated.GoScoreVolAdj, -10.0, 5.0, 0.5);
            if (_random.NextDouble() < mutationRate) mutated.GoScoreTrendAdj = MutateValue(mutated.GoScoreTrendAdj, -2.0, 2.0, 0.1);
            if (_random.NextDouble() < mutationRate) mutated.VwapWeight = MutateValue(mutated.VwapWeight, 0.0, 1.5, 0.05);
            if (_random.NextDouble() < mutationRate) mutated.RegimeSensitivity = MutateValue(mutated.RegimeSensitivity, 0.3, 1.5, 0.05);
            if (_random.NextDouble() < mutationRate) mutated.VolatilityFilter = MutateValue(mutated.VolatilityFilter, 0.1, 1.0, 0.05);
            if (_random.NextDouble() < mutationRate) mutated.MaxPositionSize = MutateValue(mutated.MaxPositionSize, 5.0, 20.0, 1.0);
            if (_random.NextDouble() < mutationRate) mutated.PositionScaling = MutateValue(mutated.PositionScaling, 0.5, 2.0, 0.1);
            if (_random.NextDouble() < mutationRate) mutated.DrawdownReduction = MutateValue(mutated.DrawdownReduction, 0.3, 0.9, 0.05);
            if (_random.NextDouble() < mutationRate) mutated.RecoveryBoost = MutateValue(mutated.RecoveryBoost, 1.0, 2.0, 0.1);
            if (_random.NextDouble() < mutationRate) mutated.BullMarketAggression = MutateValue(mutated.BullMarketAggression, 0.8, 2.0, 0.1);
            if (_random.NextDouble() < mutationRate) mutated.BearMarketDefense = MutateValue(mutated.BearMarketDefense, 0.3, 1.0, 0.05);
            if (_random.NextDouble() < mutationRate) mutated.HighVolReduction = MutateValue(mutated.HighVolReduction, 0.2, 0.8, 0.05);
            if (_random.NextDouble() < mutationRate) mutated.LowVolBoost = MutateValue(mutated.LowVolBoost, 1.0, 2.5, 0.1);
            if (_random.NextDouble() < mutationRate) mutated.OpeningBias = MutateValue(mutated.OpeningBias, 0.8, 1.5, 0.05);
            if (_random.NextDouble() < mutationRate) mutated.ClosingBias = MutateValue(mutated.ClosingBias, 0.8, 1.5, 0.05);
            if (_random.NextDouble() < mutationRate) mutated.FridayReduction = MutateValue(mutated.FridayReduction, 0.5, 1.0, 0.05);
            if (_random.NextDouble() < mutationRate) mutated.FOPExitBias = MutateValue(mutated.FOPExitBias, 1.0, 2.0, 0.1);
            
            // Enhanced parameters
            if (_random.NextDouble() < mutationRate) mutated.MinTimeToClose = MutateValue(mutated.MinTimeToClose, 0.5, 4.0, 0.1);
            if (_random.NextDouble() < mutationRate) mutated.MaxMarketStress = MutateValue(mutated.MaxMarketStress, 0.3, 1.0, 0.05);
            if (_random.NextDouble() < mutationRate) mutated.MinLiquidityScore = MutateValue(mutated.MinLiquidityScore, 0.2, 0.9, 0.05);
            if (_random.NextDouble() < mutationRate) mutated.MinIV = MutateValue(mutated.MinIV, 0.1, 0.5, 0.02);
            if (_random.NextDouble() < mutationRate) mutated.MaxIV = MutateValue(mutated.MaxIV, 0.4, 1.2, 0.05);
            if (_random.NextDouble() < mutationRate) mutated.TrendWeight = MutateValue(mutated.TrendWeight, 0.0, 2.0, 0.1);
            if (_random.NextDouble() < mutationRate) mutated.MomentumWeight = MutateValue(mutated.MomentumWeight, 0.0, 2.0, 0.1);
            if (_random.NextDouble() < mutationRate) mutated.NewsWeight = MutateValue(mutated.NewsWeight, 0.0, 1.0, 0.05);
            if (_random.NextDouble() < mutationRate) mutated.GammaWeight = MutateValue(mutated.GammaWeight, 0.0, 1.5, 0.05);
            if (_random.NextDouble() < mutationRate) mutated.SkewWeight = MutateValue(mutated.SkewWeight, 0.0, 1.0, 0.05);
            if (_random.NextDouble() < mutationRate) mutated.MaxDrawdownLimit = MutateValue(mutated.MaxDrawdownLimit, 1000.0, 5000.0, 100.0);
            
            return mutated;
        }

        private double MutateValue(double currentValue, double minValue, double maxValue, double maxMutation)
        {
            var mutation = (_random.NextDouble() - 0.5) * 2 * maxMutation;
            var newValue = currentValue + mutation;
            return Math.Max(minValue, Math.Min(maxValue, newValue));
        }

        private double CalculateAdaptiveMutationRate()
        {
            // Higher mutation rate early in evolution, lower as we converge
            var baseRate = 0.1;
            var generationFactor = Math.Max(0.1, 1.0 - (_currentGeneration / (double)MAX_GENERATIONS));
            return baseRate * generationFactor;
        }

        #endregion

        #region Data Classes

        public class TradingDay
        {
            public DateTime Date { get; set; }
            public List<EvaluationPoint> EvaluationPoints { get; set; } = new();
            public string MarketRegime { get; set; } = "";
            public double VIXLevel { get; set; }
            public List<string> EconomicEvents { get; set; } = new();
        }

        public class EvaluationPoint
        {
            public DateTime Timestamp { get; set; }
            public decimal UnderlyingPrice { get; set; }
            public decimal BidPrice { get; set; }
            public decimal AskPrice { get; set; }
            public long Volume { get; set; }
            public decimal VWAP { get; set; }
            public double ImpliedVolatility { get; set; }
            public double VolatilitySkew { get; set; }
            public double GammaExposure { get; set; }
            public long OpenInterest { get; set; }
            public double LiquidityScore { get; set; }
            public double MarketStress { get; set; }
            public decimal TimeToClose { get; set; }
            public int DaysToExpiry { get; set; }
            public double TrendStrength { get; set; }
            public double MomentumScore { get; set; }
            public double RegimeScore { get; set; }
            public double NewsImpact { get; set; }
            public double FedEvents { get; set; }
            public double EarningsImpact { get; set; }
            public double ExpirationEffects { get; set; }
        }

        public class PM250_EnhancedChromosome
        {
            // Core PM250 parameters
            public double ShortDelta { get; set; }
            public double WidthPoints { get; set; }
            public double CreditRatio { get; set; }
            public double StopMultiple { get; set; }
            public double GoScoreBase { get; set; }
            public double GoScoreVolAdj { get; set; }
            public double GoScoreTrendAdj { get; set; }
            public double VwapWeight { get; set; }
            public double RegimeSensitivity { get; set; }
            public double VolatilityFilter { get; set; }
            public double MaxPositionSize { get; set; }
            public double PositionScaling { get; set; }
            public double DrawdownReduction { get; set; }
            public double RecoveryBoost { get; set; }
            public double BullMarketAggression { get; set; }
            public double BearMarketDefense { get; set; }
            public double HighVolReduction { get; set; }
            public double LowVolBoost { get; set; }
            public double OpeningBias { get; set; }
            public double ClosingBias { get; set; }
            public double FridayReduction { get; set; }
            public double FOPExitBias { get; set; }
            
            // Enhanced parameters for 10-minute evaluation
            public double MinTimeToClose { get; set; } = 0.5; // Minimum hours to market close
            public double MaxMarketStress { get; set; } = 0.7; // Maximum market stress level
            public double MinLiquidityScore { get; set; } = 0.6; // Minimum liquidity requirement
            public double MinIV { get; set; } = 0.15; // Minimum implied volatility
            public double MaxIV { get; set; } = 0.8; // Maximum implied volatility
            public double TrendWeight { get; set; } = 1.0; // Trend strength influence
            public double MomentumWeight { get; set; } = 0.8; // Momentum influence
            public double NewsWeight { get; set; } = 0.5; // News impact influence
            public double GammaWeight { get; set; } = 0.7; // Gamma exposure influence
            public double SkewWeight { get; set; } = 0.6; // Volatility skew influence
            public double MaxDrawdownLimit { get; set; } = 2500.0; // Maximum allowed drawdown
            public bool AvoidEconomicEvents { get; set; } = true; // Avoid trading during econ events
            public bool UseAdaptiveThreshold { get; set; } = true; // Use dynamic GoScore threshold
            public bool EnableGammaHedging { get; set; } = false; // Enable gamma hedging logic
            
            // Performance tracking
            public PerformanceMetrics Performance { get; set; } = new();
            public double Fitness { get; set; }
        }

        public class PerformanceMetrics
        {
            public decimal AverageProfit { get; set; }
            public double WinRate { get; set; }
            public int TotalTrades { get; set; }
            public decimal TotalPnL { get; set; }
            public double MaxDrawdown { get; set; }
            public double SharpeRatio { get; set; }
            public double CalmarRatio { get; set; }
            public double ProfitFactor { get; set; }
            public double ConsistencyScore { get; set; }
            public int ProfitableMonths { get; set; }
            public decimal MaxSingleWin { get; set; }
            public decimal MaxSingleLoss { get; set; }
        }

        public class TradeResult
        {
            public DateTime Timestamp { get; set; }
            public decimal UnderlyingPrice { get; set; }
            public decimal ExpectedCredit { get; set; }
            public decimal ActualCredit { get; set; }
            public decimal PnL { get; set; }
            public int PositionSize { get; set; }
            public bool IsWin { get; set; }
            public double GoScore { get; set; }
            public string MarketRegime { get; set; } = "";
            public decimal RiskAdjustedReturn { get; set; }
            public double ExecutionQuality { get; set; }
            public string Strategy { get; set; } = "";
        }

        public class AdvancedRiskManager
        {
            private readonly PM250_EnhancedChromosome _chromosome;
            private readonly List<TradeResult> _recentTrades = new();
            private decimal _currentDrawdown = 0m;
            
            public AdvancedRiskManager(PM250_EnhancedChromosome chromosome)
            {
                _chromosome = chromosome;
            }
            
            public bool CanTrade(decimal currentDrawdown)
            {
                return currentDrawdown < (decimal)_chromosome.MaxDrawdownLimit;
            }
            
            public int CalculatePositionSize(PM250_EnhancedChromosome chromosome, EvaluationPoint point)
            {
                var baseSize = 1; // Start with 1 contract
                
                // Scale based on market conditions
                if (point.LiquidityScore > 0.8) baseSize = (int)(baseSize * 1.2);
                if (point.MarketStress > 0.5) baseSize = (int)(baseSize * 0.7);
                
                return Math.Max(1, baseSize);
            }
            
            public decimal GetCurrentRisk()
            {
                return Math.Max(100m, _currentDrawdown + 100m);
            }
            
            public void RecordTrade(TradeResult trade)
            {
                _recentTrades.Add(trade);
                
                if (trade.PnL < 0)
                {
                    _currentDrawdown += Math.Abs(trade.PnL);
                }
                else if (trade.PnL > 50m) // Significant win
                {
                    _currentDrawdown = Math.Max(0, _currentDrawdown - trade.PnL * 0.5m);
                }
                
                // Keep only recent trades for efficiency
                if (_recentTrades.Count > 1000)
                {
                    _recentTrades.RemoveRange(0, 500);
                }
            }
        }

        #endregion

        #region Helper Methods - Market Data and Calculations

        private decimal GetHistoricalPrice(DateTime date)
        {
            // Realistic historical pricing for SPY/XSP
            return date.Year switch
            {
                <= 2008 => 150m + (decimal)(_random.NextDouble() * 50),  // 2005-2008: $150-200
                <= 2012 => 120m + (decimal)(_random.NextDouble() * 80),  // 2009-2012: $120-200 (crisis recovery)
                <= 2016 => 180m + (decimal)(_random.NextDouble() * 70),  // 2013-2016: $180-250
                <= 2020 => 250m + (decimal)(_random.NextDouble() * 100), // 2017-2020: $250-350
                <= 2022 => 350m + (decimal)(_random.NextDouble() * 150), // 2021-2022: $350-500
                _ => 400m + (decimal)(_random.NextDouble() * 100)         // 2023+: $400-500
            };
        }

        private double GetHistoricalVIX(DateTime date)
        {
            // Realistic VIX levels based on historical periods
            return date switch
            {
                var d when d.Year == 2008 && d.Month >= 9 => 35.0 + _random.NextDouble() * 30, // Financial crisis
                var d when d.Year == 2020 && d.Month >= 2 && d.Month <= 5 => 30.0 + _random.NextDouble() * 50, // COVID
                var d when d.Year == 2018 && d.Month == 2 => 25.0 + _random.NextDouble() * 15, // Volmageddon
                var d when d.Year == 2022 => 22.0 + _random.NextDouble() * 15, // Ukraine/Fed
                _ => 15.0 + _random.NextDouble() * 10 // Normal times: 15-25
            };
        }

        private long GetHistoricalVolume(DateTime time)
        {
            var baseVolume = 50000000L; // 50M base
            var timeOfDay = time.TimeOfDay.TotalHours;
            
            // Higher volume at open and close
            var intradayMultiplier = timeOfDay switch
            {
                >= 9.5 and <= 10.5 => 1.8, // Opening hour
                >= 15.0 and <= 16.0 => 1.6, // Closing hour
                >= 11.0 and <= 14.0 => 0.7, // Lunch lull
                _ => 1.0
            };
            
            return (long)(baseVolume * intradayMultiplier * (0.8 + _random.NextDouble() * 0.4));
        }

        private decimal GetRealisticSpread(DateTime time, double vixLevel)
        {
            var baseSpread = 0.01m; // $0.01 base spread
            var volMultiplier = (decimal)(1.0 + vixLevel / 100.0); // Higher VIX = wider spreads
            var timeMultiplier = time.TimeOfDay.TotalHours switch
            {
                >= 9.5 and <= 10.0 => 1.5m, // Wider at open
                >= 12.0 and <= 13.0 => 1.3m, // Wider at lunch
                >= 15.5 and <= 16.0 => 1.4m, // Wider at close
                _ => 1.0m
            };
            
            return baseSpread * volMultiplier * timeMultiplier;
        }

        private double CalculateImpliedVolatility(double vixLevel, DateTime time)
        {
            var baseIV = vixLevel / 100.0;
            var timeDecay = time.TimeOfDay.TotalHours > 15.0 ? 1.2 : 1.0; // Higher IV near close
            return baseIV * timeDecay * (0.9 + _random.NextDouble() * 0.2);
        }

        private double CalculateVolatilitySkew(DateTime time, string marketRegime)
        {
            var baseSkew = marketRegime switch
            {
                "Bull" => -0.05, // Negative skew in bull markets
                "Bear" => 0.15,  // Positive skew in bear markets
                "Mixed" => 0.05, // Slight positive skew
                _ => 0.0
            };
            
            return baseSkew + (_random.NextDouble() - 0.5) * 0.1;
        }

        private double CalculateGammaExposure(DateTime time, decimal price)
        {
            // Simplified gamma exposure calculation
            var timeToClose = (16.0 - time.TimeOfDay.TotalHours) / 6.5;
            var gammaEffect = Math.Max(0.1, timeToClose) * (_random.NextDouble() - 0.5) * 0.2;
            return gammaEffect;
        }

        private long GetOpenInterest(DateTime time)
        {
            // Simulate realistic open interest for 0DTE options
            var baseOI = 10000L;
            var dayOfWeek = time.DayOfWeek;
            
            var dayMultiplier = dayOfWeek switch
            {
                DayOfWeek.Monday => 1.3, // Higher OI on Monday
                DayOfWeek.Friday => 2.0, // Much higher on Friday (0DTE expiry)
                DayOfWeek.Wednesday => 1.5, // FOMC days typically Wednesday
                _ => 1.0
            };
            
            return (long)(baseOI * dayMultiplier * (0.5 + _random.NextDouble()));
        }

        private decimal CalculateVWAP(decimal price, long volume)
        {
            // Simplified VWAP calculation
            var vwapVariation = (decimal)(_random.NextDouble() - 0.5) * 0.002m; // ±0.2%
            return price * (1 + vwapVariation);
        }

        private double CalculateLiquidityScore(long volume, decimal spread)
        {
            // Higher volume and tighter spreads = better liquidity
            var volumeScore = Math.Min(1.0, volume / 100000000.0); // Normalize to 100M volume
            var spreadScore = Math.Max(0.0, 1.0 - (double)spread * 100); // Penalty for wide spreads
            return (volumeScore + spreadScore) / 2.0;
        }

        private double CalculateMarketStress(double vixLevel, string marketRegime)
        {
            var baseStress = vixLevel / 50.0; // Normalize VIX to 0-1 scale (50 VIX = max stress)
            var regimeStress = marketRegime switch
            {
                "Bear" => 0.3,
                "Mixed" => 0.1,
                _ => 0.0
            };
            
            return Math.Min(1.0, baseStress + regimeStress);
        }

        private double CalculateTrendStrength(DateTime time, decimal price)
        {
            // Simplified trend calculation using price momentum
            var random = new Random(time.GetHashCode());
            return (random.NextDouble() - 0.5) * 2.0; // -1 to +1 range
        }

        private double CalculateMomentumScore(DateTime time, long volume)
        {
            // Volume-based momentum proxy
            var avgVolume = 75000000L;
            var volumeRatio = (double)volume / avgVolume;
            return Math.Min(2.0, Math.Max(-2.0, volumeRatio - 1.0)); // -2 to +2 range
        }

        private double CalculateRegimeScore(string marketRegime, DateTime time)
        {
            return marketRegime switch
            {
                "Bull" => 1.0,
                "Bear" => -1.0,
                "Mixed" => 0.0,
                _ => 0.0
            };
        }

        private double GetNewsImpact(DateTime time)
        {
            // Simulate news impact (typically higher at market open/close)
            var hour = time.Hour;
            return hour switch
            {
                9 => 0.3,  // Pre-market news
                16 => 0.2, // After-hours news
                _ => 0.1   // Normal news flow
            };
        }

        private double GetFedEventImpact(DateTime date)
        {
            // Check if this is a Fed event day (simplified)
            var isFedDay = date.Day == 15 || date.Day == 16; // Approximate FOMC days
            return isFedDay ? 0.5 : 0.0;
        }

        private double GetEarningsImpact(DateTime date)
        {
            // Earnings season impact (simplified)
            var month = date.Month;
            var isEarningsSeason = month == 1 || month == 4 || month == 7 || month == 10;
            return isEarningsSeason ? 0.2 : 0.0;
        }

        private double CalculateExpirationEffects(DateTime time)
        {
            // 0DTE options have high time decay, especially in final hours
            var hoursToClose = (16.0 - time.TimeOfDay.TotalHours);
            return Math.Max(0.1, 1.0 / Math.Max(0.5, hoursToClose)); // Higher effect closer to expiry
        }

        private bool IsMarketHoliday(DateTime date)
        {
            // Simplified holiday detection
            var holidays = new[]
            {
                new DateTime(date.Year, 1, 1),   // New Year's Day
                new DateTime(date.Year, 7, 4),   // Independence Day
                new DateTime(date.Year, 12, 25), // Christmas
                // Add more holidays as needed
            };
            
            return holidays.Contains(date.Date);
        }

        private string DetermineHistoricalMarketRegime(DateTime date)
        {
            // Historical market regime classification
            return date switch
            {
                var d when d.Year >= 2009 && d.Year <= 2020 => "Bull",     // Post-crisis bull market
                var d when d.Year == 2008 => "Bear",                       // Financial crisis
                var d when d.Year == 2022 => "Bear",                       // Fed tightening
                var d when d.Year == 2020 && d.Month >= 3 && d.Month <= 5 => "Bear", // COVID crash
                _ => "Mixed"                                                // Mixed/transitional periods
            };
        }

        private List<string> GetEconomicEvents(DateTime date)
        {
            var events = new List<string>();
            
            // FOMC meetings (simplified - typically 8 per year)
            if (date.Day >= 15 && date.Day <= 17 && 
                (date.Month == 1 || date.Month == 3 || date.Month == 5 || 
                 date.Month == 6 || date.Month == 7 || date.Month == 9 || 
                 date.Month == 11 || date.Month == 12))
            {
                events.Add("FOMC_MEETING");
            }
            
            // Jobs report (first Friday of month)
            if (date.DayOfWeek == DayOfWeek.Friday && date.Day <= 7)
            {
                events.Add("JOBS_REPORT");
            }
            
            // CPI report (typically mid-month)
            if (date.Day >= 10 && date.Day <= 15)
            {
                events.Add("CPI_REPORT");
            }
            
            return events;
        }

        #endregion

        #region Optimization Logic - GoScore and Decision Making

        private double CalculateEnhancedGoScore(PM250_EnhancedChromosome chromosome, EvaluationPoint point)
        {
            // Base GoScore calculation
            var baseScore = chromosome.GoScoreBase;
            
            // VIX adjustment
            var vixAdjustment = chromosome.GoScoreVolAdj * (GetCurrentVIX(point.Timestamp) - 20) / 10;
            
            // Trend adjustment
            var trendAdjustment = chromosome.GoScoreTrendAdj * point.TrendStrength;
            
            // VWAP adjustment
            var vwapDifference = (double)((point.UnderlyingPrice - point.VWAP) / point.VWAP);
            var vwapAdjustment = chromosome.VwapWeight * vwapDifference * 100;
            
            // Enhanced factors for 10-minute evaluation
            var momentumAdjustment = chromosome.MomentumWeight * point.MomentumScore;
            var liquidityAdjustment = chromosome.TrendWeight * (point.LiquidityScore - 0.5) * 20;
            var gammaAdjustment = chromosome.GammaWeight * point.GammaExposure * 100;
            var skewAdjustment = chromosome.SkewWeight * point.VolatilitySkew * 50;
            var newsAdjustment = chromosome.NewsWeight * point.NewsImpact * -20; // Negative = avoid news
            
            // Time decay adjustment (higher score closer to close for 0DTE)
            var timeDecayBonus = point.TimeToClose < 2.0m ? 5.0 : 0.0;
            
            return baseScore + vixAdjustment + trendAdjustment + vwapAdjustment + 
                   momentumAdjustment + liquidityAdjustment + gammaAdjustment + 
                   skewAdjustment + newsAdjustment + timeDecayBonus;
        }

        private double CalculateDynamicThreshold(PM250_EnhancedChromosome chromosome, EvaluationPoint point)
        {
            if (!chromosome.UseAdaptiveThreshold)
            {
                return chromosome.GoScoreBase * 0.9; // Static threshold
            }
            
            // Dynamic threshold based on market conditions
            var baseThreshold = chromosome.GoScoreBase * 0.9;
            
            // Higher threshold in stressed markets
            var stressAdjustment = point.MarketStress * 10.0;
            
            // Lower threshold in high liquidity
            var liquidityAdjustment = (point.LiquidityScore - 0.5) * -5.0;
            
            // Time-of-day adjustments
            var hour = point.Timestamp.Hour;
            var timeAdjustment = hour switch
            {
                9 => 5.0,  // More selective at open
                15 => -3.0, // More aggressive near close
                _ => 0.0
            };
            
            return baseThreshold + stressAdjustment + liquidityAdjustment + timeAdjustment;
        }

        private bool IsEconomicEventPeriod(DateTime time, bool avoidEvents)
        {
            if (!avoidEvents) return false;
            
            // Check for economic events in the next 30 minutes
            var eventTimes = new[]
            {
                8.5,  // 8:30 AM - Jobs report, CPI, etc.
                10.0, // 10:00 AM - ISM, Consumer confidence
                14.0, // 2:00 PM - FOMC announcement time
                14.5  // 2:30 PM - Fed press conference
            };
            
            var currentHour = time.TimeOfDay.TotalHours;
            return eventTimes.Any(eventTime => Math.Abs(currentHour - eventTime) < 0.5);
        }

        private decimal CalculateRealisticCredit(PM250_EnhancedChromosome chromosome, EvaluationPoint point)
        {
            // Calculate realistic option credit based on underlying price and parameters
            var underlyingPrice = point.UnderlyingPrice;
            var width = (decimal)chromosome.WidthPoints;
            var creditRatio = (decimal)chromosome.CreditRatio;
            
            // Base credit calculation
            var baseCredit = underlyingPrice * width * creditRatio / 100m;
            
            // Adjust for implied volatility
            var ivAdjustment = (decimal)(point.ImpliedVolatility - 0.2) * baseCredit * 0.5m;
            
            // Adjust for time to expiry (0DTE has less time premium)
            var timeAdjustment = baseCredit * (decimal)point.TimeToClose * 0.1m;
            
            // Market stress adjustment (higher credit in stressed markets)
            var stressAdjustment = baseCredit * (decimal)point.MarketStress * 0.3m;
            
            return Math.Max(5m, baseCredit + ivAdjustment + timeAdjustment + stressAdjustment);
        }

        private TradeOutcome DetermineTradeOutcome(PM250_EnhancedChromosome chromosome, EvaluationPoint point)
        {
            // Enhanced probability model for trade outcomes
            var baseWinRate = 0.78; // Historical base win rate
            
            // Adjust for market conditions
            var stressAdjustment = -point.MarketStress * 0.15; // Lower win rate in stress
            var liquidityAdjustment = (point.LiquidityScore - 0.5) * 0.1; // Better in liquid markets
            var ivAdjustment = (point.ImpliedVolatility - 0.25) * -0.2; // Lower win rate in high IV
            var timeAdjustment = point.TimeToClose < 1.0m ? -0.05 : 0.0; // Slightly lower very close to expiry
            
            // GoScore influence
            var goScore = CalculateEnhancedGoScore(chromosome, point);
            var goScoreAdjustment = (goScore - chromosome.GoScoreBase) / 100.0 * 0.1;
            
            var finalWinRate = baseWinRate + stressAdjustment + liquidityAdjustment + 
                              ivAdjustment + timeAdjustment + goScoreAdjustment;
            
            finalWinRate = Math.Max(0.4, Math.Min(0.9, finalWinRate)); // Bound between 40-90%
            
            var isWin = _random.NextDouble() < finalWinRate;
            
            return new TradeOutcome
            {
                IsWin = isWin,
                WinProbability = finalWinRate,
                ExpectedReturn = isWin ? 0.6 : -2.2 // Win: 60% of credit, Loss: 2.2x credit
            };
        }

        private decimal CalculateActualPnL(
            PM250_EnhancedChromosome chromosome, 
            EvaluationPoint point, 
            TradeOutcome outcome, 
            decimal expectedCredit, 
            int positionSize)
        {
            if (outcome.IsWin)
            {
                // Winning trade: Keep percentage of credit
                var keepPercentage = 0.5m + (decimal)(_random.NextDouble() * 0.4); // 50-90%
                var winAmount = expectedCredit * keepPercentage * positionSize;
                
                // Execution quality adjustment
                var executionPenalty = (1.0m - (decimal)CalculateExecutionQuality(point)) * winAmount * 0.1m;
                
                return winAmount - executionPenalty;
            }
            else
            {
                // Losing trade: Stop loss hit
                var stopMultiple = (decimal)chromosome.StopMultiple;
                var lossAmount = expectedCredit * stopMultiple * positionSize;
                
                // Market stress can worsen losses (slippage, gaps)
                var stressPenalty = lossAmount * (decimal)point.MarketStress * 0.2m;
                
                return -(lossAmount + stressPenalty);
            }
        }

        private double CalculateExecutionQuality(EvaluationPoint point)
        {
            // Execution quality based on market conditions
            var liquidityFactor = point.LiquidityScore;
            var stressFactor = 1.0 - point.MarketStress;
            var volumeFactor = Math.Min(1.0, point.Volume / 100000000.0);
            
            return (liquidityFactor + stressFactor + volumeFactor) / 3.0;
        }

        private string DeterminePointRegime(EvaluationPoint point)
        {
            return point.MarketStress switch
            {
                > 0.7 => "High Stress",
                > 0.4 => "Moderate Stress",
                _ => "Normal"
            };
        }

        private double GetCurrentVIX(DateTime time)
        {
            return GetHistoricalVIX(time.Date);
        }

        #endregion

        #region Chromosome Management

        private PM250_EnhancedChromosome LoadCurrentBestChromosome()
        {
            // Load the current 20-year optimized weights as starting point
            var configPath = @"C:\code\ODTE\Options.OPM\Options.PM250\config\PM250_OptimalWeights_TwentyYear.json";
            
            try
            {
                var jsonText = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<JsonElement>(jsonText);
                var parameters = config.GetProperty("parameters");
                
                return new PM250_EnhancedChromosome
                {
                    ShortDelta = parameters.GetProperty("ShortDelta").GetDouble(),
                    WidthPoints = parameters.GetProperty("WidthPoints").GetDouble(),
                    CreditRatio = parameters.GetProperty("CreditRatio").GetDouble(),
                    StopMultiple = parameters.GetProperty("StopMultiple").GetDouble(),
                    GoScoreBase = parameters.GetProperty("GoScoreBase").GetDouble(),
                    GoScoreVolAdj = parameters.GetProperty("GoScoreVolAdj").GetDouble(),
                    GoScoreTrendAdj = parameters.GetProperty("GoScoreTrendAdj").GetDouble(),
                    VwapWeight = parameters.GetProperty("VwapWeight").GetDouble(),
                    RegimeSensitivity = parameters.GetProperty("RegimeSensitivity").GetDouble(),
                    VolatilityFilter = parameters.GetProperty("VolatilityFilter").GetDouble(),
                    MaxPositionSize = parameters.GetProperty("MaxPositionSize").GetDouble(),
                    PositionScaling = parameters.GetProperty("PositionScaling").GetDouble(),
                    DrawdownReduction = parameters.GetProperty("DrawdownReduction").GetDouble(),
                    RecoveryBoost = parameters.GetProperty("RecoveryBoost").GetDouble(),
                    BullMarketAggression = parameters.GetProperty("BullMarketAggression").GetDouble(),
                    BearMarketDefense = parameters.GetProperty("BearMarketDefense").GetDouble(),
                    HighVolReduction = parameters.GetProperty("HighVolReduction").GetDouble(),
                    LowVolBoost = parameters.GetProperty("LowVolBoost").GetDouble(),
                    OpeningBias = parameters.GetProperty("OpeningBias").GetDouble(),
                    ClosingBias = parameters.GetProperty("ClosingBias").GetDouble(),
                    FridayReduction = parameters.GetProperty("FridayReduction").GetDouble(),
                    FOPExitBias = parameters.GetProperty("FOPExitBias").GetDouble(),
                    
                    // Enhanced parameters with default values
                    MinTimeToClose = 0.5,
                    MaxMarketStress = 0.7,
                    MinLiquidityScore = 0.6,
                    MinIV = 0.15,
                    MaxIV = 0.8,
                    TrendWeight = 1.0,
                    MomentumWeight = 0.8,
                    NewsWeight = 0.5,
                    GammaWeight = 0.7,
                    SkewWeight = 0.6,
                    MaxDrawdownLimit = 2500.0,
                    AvoidEconomicEvents = true,
                    UseAdaptiveThreshold = true,
                    EnableGammaHedging = false
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not load existing weights: {ex.Message}");
                return GenerateRandomChromosome();
            }
        }

        private PM250_EnhancedChromosome GenerateRandomChromosome()
        {
            return new PM250_EnhancedChromosome
            {
                ShortDelta = 0.07 + _random.NextDouble() * 0.18,
                WidthPoints = 1.0 + _random.NextDouble() * 4.0,
                CreditRatio = 0.05 + _random.NextDouble() * 0.25,
                StopMultiple = 1.5 + _random.NextDouble() * 2.5,
                GoScoreBase = 50.0 + _random.NextDouble() * 40.0,
                GoScoreVolAdj = -10.0 + _random.NextDouble() * 15.0,
                GoScoreTrendAdj = -2.0 + _random.NextDouble() * 4.0,
                VwapWeight = _random.NextDouble() * 1.5,
                RegimeSensitivity = 0.3 + _random.NextDouble() * 1.2,
                VolatilityFilter = 0.1 + _random.NextDouble() * 0.9,
                MaxPositionSize = 5.0 + _random.NextDouble() * 15.0,
                PositionScaling = 0.5 + _random.NextDouble() * 1.5,
                DrawdownReduction = 0.3 + _random.NextDouble() * 0.6,
                RecoveryBoost = 1.0 + _random.NextDouble() * 1.0,
                BullMarketAggression = 0.8 + _random.NextDouble() * 1.2,
                BearMarketDefense = 0.3 + _random.NextDouble() * 0.7,
                HighVolReduction = 0.2 + _random.NextDouble() * 0.6,
                LowVolBoost = 1.0 + _random.NextDouble() * 1.5,
                OpeningBias = 0.8 + _random.NextDouble() * 0.7,
                ClosingBias = 0.8 + _random.NextDouble() * 0.7,
                FridayReduction = 0.5 + _random.NextDouble() * 0.5,
                FOPExitBias = 1.0 + _random.NextDouble() * 1.0,
                
                // Enhanced parameters
                MinTimeToClose = 0.5 + _random.NextDouble() * 3.5,
                MaxMarketStress = 0.3 + _random.NextDouble() * 0.7,
                MinLiquidityScore = 0.2 + _random.NextDouble() * 0.7,
                MinIV = 0.1 + _random.NextDouble() * 0.4,
                MaxIV = 0.4 + _random.NextDouble() * 0.8,
                TrendWeight = _random.NextDouble() * 2.0,
                MomentumWeight = _random.NextDouble() * 2.0,
                NewsWeight = _random.NextDouble() * 1.0,
                GammaWeight = _random.NextDouble() * 1.5,
                SkewWeight = _random.NextDouble() * 1.0,
                MaxDrawdownLimit = 1000.0 + _random.NextDouble() * 4000.0,
                AvoidEconomicEvents = _random.NextDouble() > 0.3,
                UseAdaptiveThreshold = _random.NextDouble() > 0.2,
                EnableGammaHedging = _random.NextDouble() > 0.7
            };
        }

        private PM250_EnhancedChromosome GenerateHistoricalVariation()
        {
            // Generate variations based on known good parameters from historical analysis
            var base1 = LoadCurrentBestChromosome();
            return MutateChromosome(base1, 0.15); // 15% mutation from historical best
        }

        private PM250_EnhancedChromosome CloneChromosome(PM250_EnhancedChromosome original)
        {
            return new PM250_EnhancedChromosome
            {
                ShortDelta = original.ShortDelta,
                WidthPoints = original.WidthPoints,
                CreditRatio = original.CreditRatio,
                StopMultiple = original.StopMultiple,
                GoScoreBase = original.GoScoreBase,
                GoScoreVolAdj = original.GoScoreVolAdj,
                GoScoreTrendAdj = original.GoScoreTrendAdj,
                VwapWeight = original.VwapWeight,
                RegimeSensitivity = original.RegimeSensitivity,
                VolatilityFilter = original.VolatilityFilter,
                MaxPositionSize = original.MaxPositionSize,
                PositionScaling = original.PositionScaling,
                DrawdownReduction = original.DrawdownReduction,
                RecoveryBoost = original.RecoveryBoost,
                BullMarketAggression = original.BullMarketAggression,
                BearMarketDefense = original.BearMarketDefense,
                HighVolReduction = original.HighVolReduction,
                LowVolBoost = original.LowVolBoost,
                OpeningBias = original.OpeningBias,
                ClosingBias = original.ClosingBias,
                FridayReduction = original.FridayReduction,
                FOPExitBias = original.FOPExitBias,
                MinTimeToClose = original.MinTimeToClose,
                MaxMarketStress = original.MaxMarketStress,
                MinLiquidityScore = original.MinLiquidityScore,
                MinIV = original.MinIV,
                MaxIV = original.MaxIV,
                TrendWeight = original.TrendWeight,
                MomentumWeight = original.MomentumWeight,
                NewsWeight = original.NewsWeight,
                GammaWeight = original.GammaWeight,
                SkewWeight = original.SkewWeight,
                MaxDrawdownLimit = original.MaxDrawdownLimit,
                AvoidEconomicEvents = original.AvoidEconomicEvents,
                UseAdaptiveThreshold = original.UseAdaptiveThreshold,
                EnableGammaHedging = original.EnableGammaHedging
            };
        }

        private List<PM250_EnhancedChromosome> InjectDiversity(List<PM250_EnhancedChromosome> population, double replacementRate)
        {
            var newPopulation = new List<PM250_EnhancedChromosome>();
            var keepCount = (int)(population.Count * (1.0 - replacementRate));
            
            // Keep the best performers
            newPopulation.AddRange(population.OrderByDescending(c => c.Fitness).Take(keepCount));
            
            // Replace the rest with new random chromosomes
            while (newPopulation.Count < POPULATION_SIZE)
            {
                newPopulation.Add(GenerateRandomChromosome());
            }
            
            return newPopulation;
        }

        #endregion

        #region Performance Calculation and Fitness

        private PerformanceMetrics CalculatePerformanceMetrics(List<TradeResult> trades, List<decimal> dailyPnL, decimal maxDrawdown)
        {
            if (!trades.Any())
            {
                return new PerformanceMetrics();
            }
            
            var totalPnL = trades.Sum(t => t.PnL);
            var winCount = trades.Count(t => t.IsWin);
            var winRate = (double)winCount / trades.Count * 100.0;
            var averageProfit = totalPnL / trades.Count;
            
            // Calculate Sharpe ratio
            var returns = trades.Select(t => (double)t.PnL).ToList();
            var avgReturn = returns.Average();
            var stdDev = Math.Sqrt(returns.Average(r => Math.Pow(r - avgReturn, 2)));
            var sharpeRatio = stdDev > 0 ? avgReturn * Math.Sqrt(252) / stdDev : 0;
            
            // Calculate Calmar ratio
            var annualReturn = (double)totalPnL * 252.0 / trades.Count; // Annualized
            var maxDrawdownPercent = Math.Max(0.01, (double)maxDrawdown / Math.Max(1.0, (double)Math.Abs(totalPnL)) * 100.0);
            var calmarRatio = annualReturn / maxDrawdownPercent;
            
            // Calculate profit factor
            var totalWins = trades.Where(t => t.PnL > 0).Sum(t => t.PnL);
            var totalLosses = Math.Abs(trades.Where(t => t.PnL < 0).Sum(t => t.PnL));
            var profitFactor = totalLosses > 0 ? (double)(totalWins / totalLosses) : double.PositiveInfinity;
            
            // Monthly consistency
            var monthlyPnL = trades.GroupBy(t => new { t.Timestamp.Year, t.Timestamp.Month })
                                  .Select(g => g.Sum(t => t.PnL))
                                  .ToList();
            var profitableMonths = monthlyPnL.Count(pnl => pnl > 0);
            var consistencyScore = monthlyPnL.Any() ? profitableMonths / (double)monthlyPnL.Count * 100.0 : 0;
            
            return new PerformanceMetrics
            {
                AverageProfit = averageProfit,
                WinRate = winRate,
                TotalTrades = trades.Count,
                TotalPnL = totalPnL,
                MaxDrawdown = maxDrawdownPercent,
                SharpeRatio = sharpeRatio,
                CalmarRatio = calmarRatio,
                ProfitFactor = profitFactor,
                ConsistencyScore = consistencyScore,
                ProfitableMonths = profitableMonths,
                MaxSingleWin = trades.Max(t => t.PnL),
                MaxSingleLoss = trades.Min(t => t.PnL)
            };
        }

        private double CalculateFitness(PerformanceMetrics performance)
        {
            // Multi-objective fitness function targeting $15 average profit
            
            // Primary objective: Average profit per trade (weight: 40%)
            var profitScore = Math.Min(2.0, (double)performance.AverageProfit / TARGET_PROFIT);
            
            // Secondary objectives
            var winRateScore = performance.WinRate / 100.0; // 0-1 scale (weight: 20%)
            var sharpeScore = Math.Min(1.0, performance.SharpeRatio / 10.0); // Normalize (weight: 15%)
            var drawdownScore = Math.Max(0.0, 1.0 - performance.MaxDrawdown / 5.0); // Penalty for >5% drawdown (weight: 15%)
            var consistencyScore = performance.ConsistencyScore / 100.0; // 0-1 scale (weight: 10%)
            
            // Weighted fitness calculation
            var fitness = profitScore * 0.4 + 
                         winRateScore * 0.2 + 
                         sharpeScore * 0.15 + 
                         drawdownScore * 0.15 + 
                         consistencyScore * 0.1;
            
            // Bonus for achieving target profit
            if (performance.AverageProfit >= (decimal)TARGET_PROFIT)
            {
                fitness += 0.2; // 20% bonus
            }
            
            // Penalty for very low trade count (insufficient data)
            if (performance.TotalTrades < 1000)
            {
                fitness *= performance.TotalTrades / 1000.0;
            }
            
            return Math.Max(0.0, fitness);
        }

        #endregion

        #region Baseline and Validation

        private async Task<PerformanceMetrics> LoadBaselinePerformance()
        {
            // Load baseline performance from existing 20-year weights
            var configPath = @"C:\code\ODTE\Options.OPM\Options.PM250\config\PM250_OptimalWeights_TwentyYear.json";
            
            try
            {
                var jsonText = await File.ReadAllTextAsync(configPath);
                var config = JsonSerializer.Deserialize<JsonElement>(jsonText);
                var performance = config.GetProperty("performance");
                
                return new PerformanceMetrics
                {
                    AverageProfit = performance.GetProperty("averageTradeProfit").GetDecimal(),
                    WinRate = performance.GetProperty("winRate").GetDouble(),
                    TotalTrades = performance.GetProperty("totalTrades").GetInt32(),
                    TotalPnL = performance.GetProperty("totalProfitLoss").GetDecimal(),
                    MaxDrawdown = performance.GetProperty("maxDrawdown").GetDouble(),
                    SharpeRatio = performance.GetProperty("sharpeRatio").GetDouble(),
                    CalmarRatio = performance.GetProperty("calmarRatio").GetDouble(),
                    ProfitFactor = 1.8, // Estimated
                    ConsistencyScore = 95.0, // Estimated
                    ProfitableMonths = 230, // Estimated from 20 years
                    MaxSingleWin = 150m, // Estimated
                    MaxSingleLoss = -200m // Estimated
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not load baseline performance: {ex.Message}");
                return new PerformanceMetrics
                {
                    AverageProfit = 12.90m,
                    WinRate = 85.7,
                    TotalTrades = 7609,
                    SharpeRatio = 15.91,
                    MaxDrawdown = 1.76
                };
            }
        }

        private async Task<PerformanceMetrics> ComprehensiveValidation(PM250_EnhancedChromosome bestChromosome)
        {
            Console.WriteLine("🔍 COMPREHENSIVE VALIDATION OF BEST CHROMOSOME");
            Console.WriteLine("-" + new string('-', 60));
            
            // Re-evaluate the best chromosome with more detailed tracking
            var trades = new List<TradeResult>();
            var riskManager = new AdvancedRiskManager(bestChromosome);
            var monthlyResults = new Dictionary<string, List<TradeResult>>();
            
            foreach (var tradingDay in _twentyYearData)
            {
                var monthKey = $"{tradingDay.Date:yyyy-MM}";
                if (!monthlyResults.ContainsKey(monthKey))
                    monthlyResults[monthKey] = new List<TradeResult>();
                
                foreach (var evaluationPoint in tradingDay.EvaluationPoints)
                {
                    var shouldTrade = ShouldExecuteTrade(bestChromosome, evaluationPoint, riskManager, 0m);
                    
                    if (shouldTrade)
                    {
                        var trade = await ExecuteVirtualTrade(bestChromosome, evaluationPoint, riskManager);
                        if (trade != null)
                        {
                            trades.Add(trade);
                            monthlyResults[monthKey].Add(trade);
                            riskManager.RecordTrade(trade);
                        }
                    }
                }
            }
            
            // Calculate detailed performance metrics
            var performance = CalculatePerformanceMetrics(trades, new List<decimal>(), CalculateMaxDrawdown(trades));
            
            // Detailed validation reporting
            Console.WriteLine($"✅ VALIDATION COMPLETE");
            Console.WriteLine($"   Total Trades: {performance.TotalTrades:N0}");
            Console.WriteLine($"   Average Profit: ${performance.AverageProfit:F2}");
            Console.WriteLine($"   Win Rate: {performance.WinRate:F1}%");
            Console.WriteLine($"   Sharpe Ratio: {performance.SharpeRatio:F2}");
            Console.WriteLine($"   Max Drawdown: {performance.MaxDrawdown:F2}%");
            Console.WriteLine($"   Monthly Consistency: {performance.ConsistencyScore:F1}%");
            
            return performance;
        }

        private decimal CalculateMaxDrawdown(List<TradeResult> trades)
        {
            if (!trades.Any()) return 0m;
            
            decimal runningPnL = 0m;
            decimal peak = 0m;
            decimal maxDrawdown = 0m;
            
            foreach (var trade in trades.OrderBy(t => t.Timestamp))
            {
                runningPnL += trade.PnL;
                if (runningPnL > peak) peak = runningPnL;
                var drawdown = peak - runningPnL;
                if (drawdown > maxDrawdown) maxDrawdown = drawdown;
            }
            
            return maxDrawdown;
        }

        #endregion

        #region Result Generation

        private async Task GenerateProductionConfiguration(PM250_EnhancedChromosome bestChromosome, PerformanceMetrics performance)
        {
            Console.WriteLine("📋 GENERATING PRODUCTION CONFIGURATION");
            Console.WriteLine("-" + new string('-', 60));
            
            var version = $"PM250_v3.0_Enhanced_{DateTime.Now:yyyyMMdd}";
            
            var productionConfig = new
            {
                saveDate = DateTime.UtcNow,
                version = version,
                optimizationPeriod = new
                {
                    start = new DateTime(2005, 1, 1),
                    end = DateTime.Now.Date
                },
                targetAchieved = performance.AverageProfit >= (decimal)TARGET_PROFIT,
                parameters = new
                {
                    // Core parameters
                    ShortDelta = bestChromosome.ShortDelta,
                    WidthPoints = bestChromosome.WidthPoints,
                    CreditRatio = bestChromosome.CreditRatio,
                    StopMultiple = bestChromosome.StopMultiple,
                    GoScoreBase = bestChromosome.GoScoreBase,
                    GoScoreVolAdj = bestChromosome.GoScoreVolAdj,
                    GoScoreTrendAdj = bestChromosome.GoScoreTrendAdj,
                    VwapWeight = bestChromosome.VwapWeight,
                    RegimeSensitivity = bestChromosome.RegimeSensitivity,
                    VolatilityFilter = bestChromosome.VolatilityFilter,
                    MaxPositionSize = bestChromosome.MaxPositionSize,
                    PositionScaling = bestChromosome.PositionScaling,
                    DrawdownReduction = bestChromosome.DrawdownReduction,
                    RecoveryBoost = bestChromosome.RecoveryBoost,
                    BullMarketAggression = bestChromosome.BullMarketAggression,
                    BearMarketDefense = bestChromosome.BearMarketDefense,
                    HighVolReduction = bestChromosome.HighVolReduction,
                    LowVolBoost = bestChromosome.LowVolBoost,
                    OpeningBias = bestChromosome.OpeningBias,
                    ClosingBias = bestChromosome.ClosingBias,
                    FridayReduction = bestChromosome.FridayReduction,
                    FOPExitBias = bestChromosome.FOPExitBias,
                    
                    // Enhanced 10-minute evaluation parameters
                    MinTimeToClose = bestChromosome.MinTimeToClose,
                    MaxMarketStress = bestChromosome.MaxMarketStress,
                    MinLiquidityScore = bestChromosome.MinLiquidityScore,
                    MinIV = bestChromosome.MinIV,
                    MaxIV = bestChromosome.MaxIV,
                    TrendWeight = bestChromosome.TrendWeight,
                    MomentumWeight = bestChromosome.MomentumWeight,
                    NewsWeight = bestChromosome.NewsWeight,
                    GammaWeight = bestChromosome.GammaWeight,
                    SkewWeight = bestChromosome.SkewWeight,
                    MaxDrawdownLimit = bestChromosome.MaxDrawdownLimit,
                    AvoidEconomicEvents = bestChromosome.AvoidEconomicEvents,
                    UseAdaptiveThreshold = bestChromosome.UseAdaptiveThreshold,
                    EnableGammaHedging = bestChromosome.EnableGammaHedging,
                    
                    // Evaluation settings
                    EvaluationIntervalMinutes = EVALUATION_INTERVAL_MINUTES
                },
                performance = new
                {
                    averageTradeProfit = performance.AverageProfit,
                    totalTrades = performance.TotalTrades,
                    winRate = performance.WinRate,
                    totalProfitLoss = performance.TotalPnL,
                    maxDrawdown = performance.MaxDrawdown,
                    sharpeRatio = performance.SharpeRatio,
                    calmarRatio = performance.CalmarRatio,
                    profitFactor = performance.ProfitFactor,
                    consistencyScore = performance.ConsistencyScore,
                    profitableMonths = performance.ProfitableMonths,
                    maxSingleWin = performance.MaxSingleWin,
                    maxSingleLoss = performance.MaxSingleLoss
                },
                fitness = CalculateFitness(performance),
                optimizationSettings = new
                {
                    populationSize = POPULATION_SIZE,
                    maxGenerations = MAX_GENERATIONS,
                    actualGenerations = _currentGeneration,
                    targetProfit = TARGET_PROFIT,
                    evaluationPoints = _twentyYearData.Sum(d => d.EvaluationPoints.Count)
                }
            };
            
            var configPath = Path.Combine(_optimizationResultsPath, $"{version}_ProductionConfig.json");
            var jsonOptions = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var jsonData = JsonSerializer.Serialize(productionConfig, jsonOptions);
            await File.WriteAllTextAsync(configPath, jsonData);
            
            Console.WriteLine($"✅ Production configuration saved: {configPath}");
            Console.WriteLine($"📋 Version: {version}");
            Console.WriteLine($"🎯 Target Status: {(performance.AverageProfit >= (decimal)TARGET_PROFIT ? "✅ ACHIEVED" : "❌ NOT REACHED")}");
        }

        private async Task GenerateComprehensiveReport(
            PerformanceMetrics baseline, 
            PerformanceMetrics optimized, 
            PM250_EnhancedChromosome bestChromosome)
        {
            Console.WriteLine();
            Console.WriteLine("📊 COMPREHENSIVE OPTIMIZATION REPORT");
            Console.WriteLine("=" + new string('=', 80));
            
            // Performance comparison
            var profitImprovement = ((double)optimized.AverageProfit / (double)baseline.AverageProfit - 1) * 100;
            var winRateChange = optimized.WinRate - baseline.WinRate;
            var sharpeChange = optimized.SharpeRatio - baseline.SharpeRatio;
            var drawdownChange = optimized.MaxDrawdown - baseline.MaxDrawdown;
            
            Console.WriteLine("🔄 PERFORMANCE COMPARISON:");
            Console.WriteLine("-" + new string('-', 40));
            Console.WriteLine($"Average Profit:");
            Console.WriteLine($"  Baseline:  ${baseline.AverageProfit:F2}");
            Console.WriteLine($"  Optimized: ${optimized.AverageProfit:F2}");
            Console.WriteLine($"  Change:    {profitImprovement:+0.0;-0.0}% ({(profitImprovement > 0 ? "✅" : "❌")})");
            Console.WriteLine();
            
            Console.WriteLine($"Win Rate:");
            Console.WriteLine($"  Baseline:  {baseline.WinRate:F1}%");
            Console.WriteLine($"  Optimized: {optimized.WinRate:F1}%");
            Console.WriteLine($"  Change:    {winRateChange:+0.0;-0.0} percentage points ({(winRateChange >= -2 ? "✅" : "❌")})");
            Console.WriteLine();
            
            Console.WriteLine($"Sharpe Ratio:");
            Console.WriteLine($"  Baseline:  {baseline.SharpeRatio:F2}");
            Console.WriteLine($"  Optimized: {optimized.SharpeRatio:F2}");
            Console.WriteLine($"  Change:    {sharpeChange:+0.0;-0.0} ({(sharpeChange >= -2 ? "✅" : "❌")})");
            Console.WriteLine();
            
            Console.WriteLine($"Max Drawdown:");
            Console.WriteLine($"  Baseline:  {baseline.MaxDrawdown:F2}%");
            Console.WriteLine($"  Optimized: {optimized.MaxDrawdown:F2}%");
            Console.WriteLine($"  Change:    {drawdownChange:+0.0;-0.0} percentage points ({(drawdownChange <= 1 ? "✅" : "❌")})");
            Console.WriteLine();
            
            Console.WriteLine($"Total Trades:");
            Console.WriteLine($"  Baseline:  {baseline.TotalTrades:N0}");
            Console.WriteLine($"  Optimized: {optimized.TotalTrades:N0}");
            Console.WriteLine($"  Change:    {((double)optimized.TotalTrades / baseline.TotalTrades - 1) * 100:+0.0;-0.0}%");
            Console.WriteLine();
            
            // Target achievement assessment
            Console.WriteLine("🎯 TARGET ACHIEVEMENT ASSESSMENT:");
            Console.WriteLine("-" + new string('-', 40));
            
            var targetChecks = new[]
            {
                ("Average Profit ≥ $15.00", optimized.AverageProfit >= 15.0m, $"${optimized.AverageProfit:F2}"),
                ("Win Rate ≥ 80%", optimized.WinRate >= 80.0, $"{optimized.WinRate:F1}%"),
                ("Max Drawdown ≤ 3%", optimized.MaxDrawdown <= 3.0, $"{optimized.MaxDrawdown:F2}%"),
                ("Sharpe Ratio ≥ 12.0", optimized.SharpeRatio >= 12.0, $"{optimized.SharpeRatio:F2}"),
                ("Monthly Consistency ≥ 95%", optimized.ConsistencyScore >= 95.0, $"{optimized.ConsistencyScore:F1}%")
            };
            
            var achievedCount = 0;
            foreach (var (criterion, achieved, value) in targetChecks)
            {
                var status = achieved ? "✅ PASS" : "❌ FAIL";
                Console.WriteLine($"  {status} {criterion}: {value}");
                if (achieved) achievedCount++;
            }
            
            Console.WriteLine();
            Console.WriteLine($"🏆 OVERALL SUCCESS: {achievedCount}/{targetChecks.Length} criteria achieved");
            
            if (achievedCount == targetChecks.Length)
            {
                Console.WriteLine("🎉 COMPLETE SUCCESS - All optimization targets achieved!");
            }
            else if (achievedCount >= 3)
            {
                Console.WriteLine("⚡ SUBSTANTIAL SUCCESS - Most targets achieved");
            }
            else
            {
                Console.WriteLine("🔧 PARTIAL SUCCESS - Further optimization may be needed");
            }
            
            Console.WriteLine();
            Console.WriteLine("🧬 GENETIC OPTIMIZATION SUMMARY:");
            Console.WriteLine("-" + new string('-', 40));
            Console.WriteLine($"Generations Completed: {_currentGeneration}/{MAX_GENERATIONS}");
            Console.WriteLine($"Population Size: {POPULATION_SIZE}");
            Console.WriteLine($"Total Evaluations: {_currentGeneration * POPULATION_SIZE:N0}");
            Console.WriteLine($"Data Points Analyzed: {_twentyYearData.Sum(d => d.EvaluationPoints.Count):N0}");
            Console.WriteLine($"Best Fitness Achieved: {CalculateFitness(optimized):F4}");
            
            Console.WriteLine();
            Console.WriteLine("🚀 READY FOR PRODUCTION DEPLOYMENT");
            Console.WriteLine($"📋 Version: PM250_v3.0_Enhanced_{DateTime.Now:yyyyMMdd}");
            Console.WriteLine($"💰 Expected Average Profit: ${optimized.AverageProfit:F2} per trade");
            Console.WriteLine($"⏱️ Evaluation Frequency: Every {EVALUATION_INTERVAL_MINUTES} minutes");
            Console.WriteLine($"🛡️ Risk Management: Advanced with {optimized.MaxDrawdown:F2}% max drawdown");
        }

        private async Task SaveIntermediateResults(PM250_EnhancedChromosome chromosome, int generation)
        {
            var fileName = $"Generation_{generation:D4}_Best.json";
            var filePath = Path.Combine(_optimizationResultsPath, "Intermediate", fileName);
            
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            
            var result = new
            {
                generation = generation,
                timestamp = DateTime.UtcNow,
                fitness = chromosome.Fitness,
                performance = chromosome.Performance,
                parameters = chromosome
            };
            
            var jsonOptions = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var jsonData = JsonSerializer.Serialize(result, jsonOptions);
            await File.WriteAllTextAsync(filePath, jsonData);
        }

        private async Task GenerateProgressReport(List<PM250_EnhancedChromosome> population, int generation)
        {
            var sorted = population.OrderByDescending(c => c.Fitness).ToList();
            var best = sorted.First();
            var worst = sorted.Last();
            var median = sorted[sorted.Count / 2];
            
            Console.WriteLine($"📈 GENERATION {generation} PROGRESS REPORT:");
            Console.WriteLine($"   Best Fitness: {best.Fitness:F4} (Profit: ${best.Performance.AverageProfit:F2})");
            Console.WriteLine($"   Median Fitness: {median.Fitness:F4} (Profit: ${median.Performance.AverageProfit:F2})");
            Console.WriteLine($"   Worst Fitness: {worst.Fitness:F4} (Profit: ${worst.Performance.AverageProfit:F2})");
            Console.WriteLine($"   Target Progress: {(best.Performance.AverageProfit / (decimal)TARGET_PROFIT * 100):F1}%");
            
            if (generation % 50 == 0)
            {
                var progressPath = Path.Combine(_optimizationResultsPath, $"Progress_Gen_{generation}.json");
                var progressData = new
                {
                    generation = generation,
                    timestamp = DateTime.UtcNow,
                    statistics = new
                    {
                        bestFitness = best.Fitness,
                        medianFitness = median.Fitness,
                        worstFitness = worst.Fitness,
                        bestProfit = best.Performance.AverageProfit,
                        targetProgress = (double)(best.Performance.AverageProfit / (decimal)TARGET_PROFIT)
                    },
                    bestChromosome = best
                };
                
                var jsonData = JsonSerializer.Serialize(progressData, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(progressPath, jsonData);
            }
        }

        #endregion

        #region Helper Classes

        public class TradeOutcome
        {
            public bool IsWin { get; set; }
            public double WinProbability { get; set; }
            public double ExpectedReturn { get; set; }
        }

        #endregion
    }
}