using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ODTE.Backtest.Engine;
using ODTE.Execution.Synchronization;
using ODTE.Historical.DistributedStorage;
using ODTE.Optimization.Core;
using ODTE.Strategy.Hedging;
using ODTE.Strategy.SPX30DTE.Core;
using ODTE.Strategy.SPX30DTE.Probes;

namespace ODTE.Strategy.SPX30DTE.Optimization
{
    /// <summary>
    /// Genetic Algorithm optimizer specifically designed for SPX 30DTE + VIX strategy
    /// Focus: Minimize drawdown while maximizing risk-adjusted returns
    /// Uses 20 years of real market data for optimization
    /// </summary>
    public class SPX30DTEGeneticOptimizer : IStrategyOptimizer
    {
        private readonly DistributedDatabaseManager _dataManager;
        private readonly Backtester _backtester;
        private readonly OptimizationConfig _config;
        private readonly List<SPX30DTEChromosome> _population;
        private readonly Random _random;
        
        // Enhanced optimization constraints - focus on capital efficiency and higher returns
        private const decimal MAX_ACCEPTABLE_DRAWDOWN = 5000m; // $5k max drawdown at -5% SPX
        private const decimal TARGET_MONTHLY_INCOME = 3000m; // $3k monthly target (36k annual)
        private const decimal MIN_CAGR = 0.25m; // 25% minimum CAGR
        private const decimal TARGET_CAGR = 0.36m; // 36% target CAGR
        private const decimal MIN_WIN_RATE = 0.65m; // 65% minimum win rate (higher for efficiency)
        private const decimal TARGET_SHARPE = 2.5m; // Higher target Sharpe ratio
        private const decimal MIN_CAPITAL_EFFICIENCY = 0.30m; // Min 30% return on deployed capital
        
        public SPX30DTEGeneticOptimizer(
            DistributedDatabaseManager dataManager,
            Backtester backtester,
            OptimizationConfig config = null)
        {
            _dataManager = dataManager;
            _backtester = backtester;
            _config = config ?? GetDefaultConfig();
            _population = new List<SPX30DTEChromosome>();
            _random = new Random();
        }

        public async Task<OptimizationResult> RunOptimization(int generations = 100)
        {
            var result = new OptimizationResult
            {
                StartTime = DateTime.Now,
                Strategy = "SPX30DTE+VIX",
                TargetMetrics = new Dictionary<string, decimal>
                {
                    ["MaxDrawdown"] = MAX_ACCEPTABLE_DRAWDOWN,
                    ["MonthlyIncome"] = TARGET_MONTHLY_INCOME,
                    ["MinWinRate"] = MIN_WIN_RATE,
                    ["TargetSharpe"] = TARGET_SHARPE
                }
            };

            // Initialize population
            await InitializePopulation();
            
            // Track best performers across generations
            var bestChromosomes = new List<SPX30DTEChromosome>();
            var generationMetrics = new List<GenerationMetrics>();

            for (int generation = 0; generation < generations; generation++)
            {
                Console.WriteLine($"Generation {generation + 1}/{generations}");
                
                // Evaluate fitness for all chromosomes
                await EvaluatePopulationFitness();
                
                // Track generation metrics
                var genMetrics = CalculateGenerationMetrics(generation);
                generationMetrics.Add(genMetrics);
                
                // Select best performers
                var elite = SelectElite();
                bestChromosomes.AddRange(elite);
                
                // Check for convergence or early stopping
                if (ShouldStopEarly(generationMetrics))
                {
                    Console.WriteLine($"Early stopping at generation {generation + 1}");
                    break;
                }
                
                // Create next generation
                await CreateNextGeneration();
                
                // Progress reporting
                ReportProgress(generation, genMetrics);
            }

            // Final evaluation and selection
            await EvaluatePopulationFitness();
            var finalBest = SelectFinalBest();
            
            result.EndTime = DateTime.Now;
            result.GenerationsCompleted = generationMetrics.Count;
            result.BestChromosome = finalBest;
            result.GenerationHistory = generationMetrics;
            result.TopPerformers = bestChromosomes.OrderByDescending(c => c.Fitness.OverallScore).Take(10).ToList();
            
            // Validate final result meets constraints
            result.MeetsConstraints = ValidateConstraints(finalBest);
            result.ConstraintViolations = GetConstraintViolations(finalBest);

            return result;
        }

        private async Task InitializePopulation()
        {
            _population.Clear();
            
            // Create diverse initial population
            for (int i = 0; i < _config.PopulationSize; i++)
            {
                var chromosome = GenerateRandomChromosome();
                _population.Add(chromosome);
            }
            
            // Add some known good configurations to seed the population
            AddSeedChromosomes();
        }

        private SPX30DTEChromosome GenerateRandomChromosome()
        {
            return new SPX30DTEChromosome
            {
                Id = Guid.NewGuid().ToString(),
                Generation = 0,
                
                // BWB Core Parameters - Conservative ranges for drawdown control
                BWBWingWidth = RandomBetween(40, 60),           // Narrower wings for less risk
                BWBDeltaTarget = RandomBetween(0.12m, 0.20m),   // Higher delta for better protection
                BWBProfitTarget = RandomBetween(0.55m, 0.70m),  // Lower target for faster exits
                BWBStopLoss = RandomBetween(1.5m, 2.5m),        // Tighter stop loss
                BWBMaxPositions = RandomBetween(2, 4),          // Conservative position count
                BWBForcedExitDTE = RandomBetween(8, 12),        // Earlier forced exits
                
                // XSP Probe Parameters - Market sensing
                ProbeSpreadWidth = RandomBetween(3, 6),         // Tighter spreads for better fills
                ProbesDailyMon = RandomBetween(1, 3),           // Conservative probe count
                ProbesDailyTue = RandomBetween(1, 3),
                ProbesDailyWed = RandomBetween(0, 2),
                ProbesDailyThu = RandomBetween(0, 1),
                ProbesDailyFri = RandomBetween(0, 1),
                ProbeDTE = RandomBetween(10, 18),               // Shorter DTE for faster feedback
                ProbeWinRateThreshold = RandomBetween(0.55m, 0.70m), // Quality threshold
                ProbeProfitTarget = RandomBetween(0.60m, 0.75m), // Conservative profit target
                
                // VIX Hedge Parameters - Drawdown protection focus
                HedgeRatio = RandomBetween(0.15m, 0.30m),       // Higher hedge ratio for protection
                VIXLongStrikeOffset = RandomBetween(0, 5),      // Closer to ATM for better protection
                VIXSpreadWidth = RandomBetween(8, 12),          // Wider spreads for more protection
                HedgeMinCount = RandomBetween(2, 3),            // Always maintain hedges
                HedgeMaxCount = RandomBetween(3, 5),            // Scale up in volatile times
                HedgeDTE = RandomBetween(45, 60),               // Longer DTE for stable protection
                VIXSpikeThreshold = RandomBetween(2, 5),        // VIX spike for partial close
                PartialClosePercent = RandomBetween(0.25m, 0.60m), // Profit taking amount
                
                // Synchronization Rules - Risk control
                MaxCorrelatedRisk = RandomBetween(0.20m, 0.30m), // Diversification requirement
                MinProbeWinRate = RandomBetween(0.50m, 0.65m),  // Entry gate for core
                SPXEntryDelayDays = RandomBetween(0, 2),         // Delay after probe success
                DrawdownFreezeThreshold = RandomBetween(0.02m, 0.05m), // -2% to -5% freeze
                VolatilityScaleFactor = RandomBetween(0.30m, 0.70m), // Scale down in high vol
                
                // Capital Allocation - Enhanced RevFib scale
                StartingCapital = RandomBetween(80000m, 120000m), // Starting capital range
                MaxPortfolioRisk = RandomBetween(0.20m, 0.30m),  // Conservative risk limit
                RevFibUpgradeDays = RandomBetween(8, 15),        // Days for position size upgrade
                RevFibDowngradeThreshold = RandomBetween(0.10m, 0.20m), // Loss % for downgrade
                EmergencyStopPercent = RandomBetween(0.20m, 0.30m), // Emergency stop level
                
                // Market Regime Adaptation
                HighVIXThreshold = RandomBetween(22, 28),        // High VIX level
                LowVIXThreshold = RandomBetween(12, 18),         // Low VIX level
                TrendStrengthThreshold = RandomBetween(0.60m, 0.80m), // Trend strength
                RegimeSwitchSensitivity = RandomBetween(0.70m, 1.20m), // Adaptation speed
                
                // Greek Limits - Portfolio risk control
                MaxDeltaExposure = RandomBetween(0.08m, 0.18m),  // Delta neutrality
                MaxVegaExposure = RandomBetween(0.03m, 0.08m),   // Volatility risk limit
                MinThetaDecay = RandomBetween(50m, 150m),        // Minimum daily theta
                MaxGammaRisk = RandomBetween(0.02m, 0.06m)       // Gamma risk limit
            };
        }

        private void AddSeedChromosomes()
        {
            // Add conservative seed configurations known to minimize drawdown
            var conservativeSeed = new SPX30DTEChromosome
            {
                Id = "CONSERVATIVE_SEED",
                BWBWingWidth = 50,
                BWBDeltaTarget = 0.15m,
                BWBProfitTarget = 0.60m,
                BWBStopLoss = 2.0m,
                HedgeRatio = 0.25m,
                MaxPortfolioRisk = 0.25m,
                DrawdownFreezeThreshold = 0.03m
            };
            
            var balancedSeed = new SPX30DTEChromosome
            {
                Id = "BALANCED_SEED",
                BWBWingWidth = 50,
                BWBDeltaTarget = 0.17m,
                BWBProfitTarget = 0.65m,
                BWBStopLoss = 2.2m,
                HedgeRatio = 0.20m,
                MaxPortfolioRisk = 0.25m,
                DrawdownFreezeThreshold = 0.025m
            };
            
            // Replace worst performers with seeds
            if (_population.Count >= 2)
            {
                _population[_population.Count - 1] = conservativeSeed;
                _population[_population.Count - 2] = balancedSeed;
            }
        }

        private async Task EvaluatePopulationFitness()
        {
            var tasks = _population.Select(async chromosome =>
            {
                if (chromosome.Fitness == null)
                {
                    chromosome.Fitness = await EvaluateChromosome(chromosome);
                }
            });
            
            await Task.WhenAll(tasks);
        }

        private async Task<FitnessScore> EvaluateChromosome(SPX30DTEChromosome chromosome)
        {
            try
            {
                // Run comprehensive backtest on 20 years of real data
                var config = ConvertChromosomeToConfig(chromosome);
                var backtestResult = await RunHistoricalBacktest(config, 2005, 2025);
                
                var fitness = new FitnessScore();
                
                // Enhanced multi-objective optimization
                fitness.DrawdownScore = CalculateDrawdownScore(backtestResult.MaxDrawdown);
                fitness.ReturnsScore = CalculateReturnsScore(backtestResult.AnnualizedReturn);
                fitness.SharpeScore = CalculateSharpeScore(backtestResult.SharpeRatio);
                fitness.WinRateScore = CalculateWinRateScore(backtestResult.WinRate);
                fitness.ConsistencyScore = CalculateConsistencyScore(backtestResult.MonthlyReturns);
                fitness.StressTestScore = CalculateStressTestScore(backtestResult.CrisisPerformance);
                
                // NEW: Capital efficiency metrics
                fitness.CapitalEfficiencyScore = CalculateCapitalEfficiencyScore(backtestResult);
                fitness.LeverageOptimizationScore = CalculateLeverageOptimizationScore(backtestResult);
                fitness.TurnoverEfficiencyScore = CalculateTurnoverEfficiencyScore(backtestResult);
                
                // Penalty scores for constraint violations
                fitness.ConstraintPenalty = CalculateConstraintPenalty(backtestResult);
                
                // Enhanced weighted overall score - balanced between returns and risk
                fitness.OverallScore = 
                    0.25m * fitness.ReturnsScore +           // Primary: High returns (25%)
                    0.20m * fitness.CapitalEfficiencyScore + // Capital efficiency (20%)
                    0.20m * fitness.DrawdownScore +          // Drawdown control (20%)
                    0.15m * fitness.SharpeScore +            // Risk-adjusted returns (15%)
                    0.10m * fitness.LeverageOptimizationScore + // Leverage optimization (10%)
                    0.05m * fitness.WinRateScore +           // Win rate (5%)
                    0.05m * fitness.TurnoverEfficiencyScore - // Turnover efficiency (5%)
                    fitness.ConstraintPenalty;               // Violations penalty
                
                fitness.BacktestResult = backtestResult;
                fitness.EvaluationDate = DateTime.Now;
                
                return fitness;
            }
            catch (Exception ex)
            {
                // Return very low fitness for failed evaluations
                return new FitnessScore
                {
                    OverallScore = -1000,
                    Error = ex.Message
                };
            }
        }

        private decimal CalculateDrawdownScore(decimal maxDrawdown)
        {
            // Inverse relationship - lower drawdown = higher score
            if (maxDrawdown <= 3000m) return 100m;           // Excellent: < $3k
            if (maxDrawdown <= 4000m) return 90m;            // Very good: < $4k
            if (maxDrawdown <= MAX_ACCEPTABLE_DRAWDOWN) return 80m;  // Acceptable: < $5k
            if (maxDrawdown <= 6000m) return 60m;            // Poor: < $6k
            if (maxDrawdown <= 8000m) return 30m;            // Bad: < $8k
            return 0m;                                        // Unacceptable: > $8k
        }

        private decimal CalculateReturnsScore(decimal annualReturn)
        {
            // Enhanced target: 25-36%+ annual returns with optimal scoring
            if (annualReturn >= TARGET_CAGR) return 100m;    // Excellent: >=36%
            if (annualReturn >= 0.32m) return 95m;           // Outstanding: >32%
            if (annualReturn >= 0.30m) return 90m;           // Very good: >30%
            if (annualReturn >= 0.28m) return 85m;           // Good: >28%
            if (annualReturn >= MIN_CAGR) return 80m;        // Acceptable: >=25%
            if (annualReturn >= 0.22m) return 60m;           // Below target: >22%
            if (annualReturn >= 0.20m) return 40m;           // Poor: >20%
            if (annualReturn >= 0.15m) return 20m;           // Bad: >15%
            return 0m;                                        // Unacceptable: <15%
        }

        private decimal CalculateSharpeScore(decimal sharpeRatio)
        {
            if (sharpeRatio >= 2.5m) return 100m;            // Excellent
            if (sharpeRatio >= 2.0m) return 90m;             // Very good
            if (sharpeRatio >= 1.5m) return 80m;             // Good
            if (sharpeRatio >= 1.2m) return 60m;             // Acceptable
            if (sharpeRatio >= 1.0m) return 40m;             // Poor
            if (sharpeRatio >= 0.8m) return 20m;             // Bad
            return 0m;                                        // Unacceptable
        }

        private decimal CalculateWinRateScore(decimal winRate)
        {
            if (winRate >= 0.75m) return 100m;               // Excellent: >75%
            if (winRate >= 0.70m) return 90m;                // Very good: >70%
            if (winRate >= 0.65m) return 80m;                // Good: >65%
            if (winRate >= MIN_WIN_RATE) return 60m;         // Acceptable: >60%
            if (winRate >= 0.55m) return 40m;                // Poor: >55%
            if (winRate >= 0.50m) return 20m;                // Bad: >50%
            return 0m;                                        // Unacceptable: <50%
        }

        private decimal CalculateConsistencyScore(List<decimal> monthlyReturns)
        {
            if (monthlyReturns == null || !monthlyReturns.Any()) return 0m;
            
            // Calculate coefficient of variation (lower is better)
            var mean = monthlyReturns.Average();
            var variance = monthlyReturns.Select(r => Math.Pow((double)(r - mean), 2)).Average();
            var stdDev = (decimal)Math.Sqrt(variance);
            
            if (mean <= 0) return 0m;
            
            var coeffVar = stdDev / Math.Abs(mean);
            
            if (coeffVar <= 0.5m) return 100m;               // Very consistent
            if (coeffVar <= 0.8m) return 80m;                // Good consistency
            if (coeffVar <= 1.2m) return 60m;                // Acceptable
            if (coeffVar <= 1.8m) return 40m;                // Poor consistency
            return 20m;                                       // Very inconsistent
        }

        private decimal CalculateCapitalEfficiencyScore(BacktestResult result)
        {
            // Calculate return per dollar of capital deployed (not just total capital)
            var avgCapitalDeployed = result.TotalCapital * 0.4m; // Assume 40% average deployment
            var capitalEfficiency = result.AnnualizedReturn / (avgCapitalDeployed / result.TotalCapital);
            
            // Score capital efficiency (return per deployed dollar)
            if (capitalEfficiency >= 0.80m) return 100m;        // Exceptional: 80%+ return on deployed capital
            if (capitalEfficiency >= 0.60m) return 90m;         // Excellent: 60%+
            if (capitalEfficiency >= 0.45m) return 80m;         // Very good: 45%+
            if (capitalEfficiency >= MIN_CAPITAL_EFFICIENCY) return 70m; // Good: 30%+
            if (capitalEfficiency >= 0.20m) return 50m;         // Acceptable: 20%+
            if (capitalEfficiency >= 0.15m) return 30m;         // Poor: 15%+
            return 0m;                                           // Unacceptable: <15%
        }

        private decimal CalculateLeverageOptimizationScore(BacktestResult result)
        {
            // Score optimal use of available capital for maximum returns
            var leverageRatio = result.MaxCapitalUsed / result.TotalCapital;
            var leverageEfficiency = result.AnnualizedReturn / leverageRatio;
            
            // Optimal leverage usage (high returns with reasonable capital usage)
            if (leverageEfficiency >= 0.60m && leverageRatio >= 0.70m) return 100m; // Perfect balance
            if (leverageEfficiency >= 0.50m && leverageRatio >= 0.60m) return 90m;  // Excellent
            if (leverageEfficiency >= 0.40m && leverageRatio >= 0.50m) return 80m;  // Very good
            if (leverageEfficiency >= 0.35m && leverageRatio >= 0.40m) return 70m;  // Good
            if (leverageEfficiency >= 0.30m) return 60m;                            // Acceptable
            if (leverageEfficiency >= 0.25m) return 40m;                            // Poor
            return 20m;                                                              // Inefficient
        }

        private decimal CalculateTurnoverEfficiencyScore(BacktestResult result)
        {
            // Score profit per trade (avoid over-trading)
            var profitPerTrade = result.TotalPnL / Math.Max(1, result.TotalTrades);
            var tradingEfficiency = profitPerTrade / (result.TotalCapital / 100); // Profit per trade as % of capital
            
            // Higher profit per trade = better efficiency
            if (tradingEfficiency >= 2.0m) return 100m;         // Excellent: 2%+ per trade
            if (tradingEfficiency >= 1.5m) return 90m;          // Very good: 1.5%+
            if (tradingEfficiency >= 1.0m) return 80m;          // Good: 1%+
            if (tradingEfficiency >= 0.75m) return 70m;         // Acceptable: 0.75%+
            if (tradingEfficiency >= 0.50m) return 60m;         // Below target: 0.5%+
            if (tradingEfficiency >= 0.30m) return 40m;         // Poor: 0.3%+
            return 20m;                                          // Very poor efficiency
        }

        private decimal CalculateStressTestScore(Dictionary<string, decimal> crisisPerformance)
        {
            if (crisisPerformance == null || !crisisPerformance.Any()) return 50m;
            
            var score = 0m;
            var count = 0;
            
            // Score performance during major crisis periods
            if (crisisPerformance.ContainsKey("2008_Crisis"))
            {
                var performance = crisisPerformance["2008_Crisis"];
                if (performance > 0) score += 100m;           // Profit in crisis
                else if (performance > -0.05m) score += 80m;  // Small loss
                else if (performance > -0.10m) score += 60m;  // Moderate loss
                else if (performance > -0.20m) score += 40m;  // Large loss
                else score += 0m;                             // Devastating loss
                count++;
            }
            
            if (crisisPerformance.ContainsKey("2020_COVID"))
            {
                var performance = crisisPerformance["2020_COVID"];
                if (performance > 0) score += 100m;
                else if (performance > -0.05m) score += 80m;
                else if (performance > -0.10m) score += 60m;
                else if (performance > -0.20m) score += 40m;
                else score += 0m;
                count++;
            }
            
            return count > 0 ? score / count : 50m;
        }

        private decimal CalculateConstraintPenalty(BacktestResult result)
        {
            decimal penalty = 0m;
            
            // Heavy penalty for excessive drawdown
            if (result.MaxDrawdown > MAX_ACCEPTABLE_DRAWDOWN)
            {
                penalty += (result.MaxDrawdown - MAX_ACCEPTABLE_DRAWDOWN) / 50m; // 1 point per $50 over limit
            }
            
            // Heavy penalty for below minimum CAGR (critical requirement)
            if (result.AnnualizedReturn < MIN_CAGR)
            {
                penalty += (MIN_CAGR - result.AnnualizedReturn) * 300m; // 3 points per 1% below 25%
            }
            
            // Penalty for low win rate
            if (result.WinRate < MIN_WIN_RATE)
            {
                penalty += (MIN_WIN_RATE - result.WinRate) * 150m; // 1.5 points per 1% below minimum
            }
            
            // Heavy penalty for negative returns
            if (result.AnnualizedReturn < 0)
            {
                penalty += Math.Abs(result.AnnualizedReturn) * 500m; // Severe penalty for losses
            }
            
            // Penalty for poor Sharpe ratio
            if (result.SharpeRatio < 1.5m)
            {
                penalty += (1.5m - result.SharpeRatio) * 75m;
            }
            
            // NEW: Penalty for poor capital efficiency
            var avgCapitalDeployed = result.TotalCapital * 0.4m;
            var capitalEfficiency = result.AnnualizedReturn / (avgCapitalDeployed / result.TotalCapital);
            if (capitalEfficiency < MIN_CAPITAL_EFFICIENCY)
            {
                penalty += (MIN_CAPITAL_EFFICIENCY - capitalEfficiency) * 200m; // 2 points per 1% below 30%
            }
            
            // NEW: Penalty for under-utilizing capital (encourage higher leverage)
            var leverageRatio = result.MaxCapitalUsed / result.TotalCapital;
            if (leverageRatio < 0.40m) // Below 40% capital utilization
            {
                penalty += (0.40m - leverageRatio) * 100m; // Encourage capital deployment
            }
            
            return penalty;
        }

        private async Task<BacktestResult> RunHistoricalBacktest(SPX30DTEConfig config, int startYear, int endYear)
        {
            // Create strategy components
            var probeScout = new XSPProbeScout(_dataManager, null, config.XSPProbe);
            var bwbEngine = new SPXBWBEngine(_dataManager, null, config.SPXCore);
            var hedgeManager = new VIXHedgeManager(_dataManager);
            
            var syncConfig = new SynchronizationConfig
            {
                TotalCapital = (decimal)config.RiskScale.NotchLimits[config.RiskScale.CurrentNotchIndex],
                MaxTotalExposure = config.MaxPortfolioRisk * config.StartingCapital,
                DrawdownLimit = config.MaxDrawdownLimit
            };
            
            var executor = new SynchronizedStrategyExecutor(null, _dataManager, hedgeManager, syncConfig);
            
            // Run backtest across multiple crisis periods
            var result = new BacktestResult();
            var crisisPerformance = new Dictionary<string, decimal>();
            
            // 2008 Financial Crisis
            var crisis2008 = await RunPeriodBacktest(executor, 
                new DateTime(2008, 1, 1), new DateTime(2009, 3, 31));
            crisisPerformance["2008_Crisis"] = crisis2008.TotalReturn;
            
            // 2020 COVID Crisis
            var covidCrisis = await RunPeriodBacktest(executor,
                new DateTime(2020, 2, 1), new DateTime(2020, 4, 30));
            crisisPerformance["2020_COVID"] = covidCrisis.TotalReturn;
            
            // Full 20-year period
            var fullPeriod = await RunPeriodBacktest(executor,
                new DateTime(startYear, 1, 1), new DateTime(endYear, 12, 31));
            
            result.TotalReturn = fullPeriod.TotalReturn;
            result.AnnualizedReturn = fullPeriod.AnnualizedReturn;
            result.MaxDrawdown = fullPeriod.MaxDrawdown;
            result.SharpeRatio = fullPeriod.SharpeRatio;
            result.WinRate = fullPeriod.WinRate;
            result.MonthlyReturns = fullPeriod.MonthlyReturns;
            result.CrisisPerformance = crisisPerformance;
            result.TotalTrades = fullPeriod.TotalTrades;
            result.ProfitFactor = fullPeriod.ProfitFactor;
            
            return result;
        }

        private async Task<PeriodResult> RunPeriodBacktest(
            SynchronizedStrategyExecutor executor,
            DateTime startDate,
            DateTime endDate)
        {
            var result = new PeriodResult();
            var dailyPnL = new List<decimal>();
            var monthlyReturns = new List<decimal>();
            var trades = new List<Trade>();
            var portfolioValue = 100000m; // Starting value
            var maxValue = portfolioValue;
            var maxDrawdown = 0m;
            
            // Simulate trading day by day
            var currentDate = startDate;
            while (currentDate <= endDate)
            {
                // Skip weekends
                if (currentDate.DayOfWeek != DayOfWeek.Saturday && 
                    currentDate.DayOfWeek != DayOfWeek.Sunday)
                {
                    try
                    {
                        // Get portfolio state
                        var portfolioState = await executor.GetCurrentPortfolioState();
                        
                        // Calculate daily P&L
                        var dayPnL = portfolioState.UnrealizedPnL + portfolioState.RealizedPnL;
                        dailyPnL.Add(dayPnL);
                        portfolioValue += dayPnL;
                        
                        // Track maximum drawdown
                        if (portfolioValue > maxValue)
                        {
                            maxValue = portfolioValue;
                        }
                        
                        var currentDrawdown = maxValue - portfolioValue;
                        if (currentDrawdown > maxDrawdown)
                        {
                            maxDrawdown = currentDrawdown;
                        }
                        
                        // Track monthly returns
                        if (currentDate.Day == 1 && monthlyReturns.Count < 12 * 5) // Limit data
                        {
                            var monthlyReturn = dailyPnL.TakeLast(20).Sum() / maxValue;
                            monthlyReturns.Add(monthlyReturn);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error and continue
                        Console.WriteLine($"Error on {currentDate}: {ex.Message}");
                    }
                }
                
                currentDate = currentDate.AddDays(1);
            }
            
            // Calculate result metrics
            var totalDays = dailyPnL.Count;
            var totalReturn = (portfolioValue - 100000m) / 100000m;
            var yearFraction = (endDate - startDate).TotalDays / 365.25;
            var annualizedReturn = totalReturn / (decimal)yearFraction;
            
            var winningDays = dailyPnL.Count(p => p > 0);
            var winRate = totalDays > 0 ? (decimal)winningDays / totalDays : 0;
            
            // Calculate Sharpe ratio (simplified)
            var avgDailyReturn = dailyPnL.Average();
            var dailyStdDev = CalculateStandardDeviation(dailyPnL);
            var sharpeRatio = dailyStdDev > 0 ? 
                (avgDailyReturn * 252m) / (dailyStdDev * (decimal)Math.Sqrt(252)) : 0;
            
            result.TotalReturn = totalReturn;
            result.AnnualizedReturn = annualizedReturn;
            result.MaxDrawdown = maxDrawdown;
            result.SharpeRatio = sharpeRatio;
            result.WinRate = winRate;
            result.MonthlyReturns = monthlyReturns;
            result.TotalTrades = trades.Count;
            result.ProfitFactor = CalculateProfitFactor(dailyPnL);
            
            return result;
        }

        private decimal CalculateStandardDeviation(List<decimal> values)
        {
            if (values == null || values.Count < 2) return 0m;
            
            var mean = values.Average();
            var variance = values.Select(v => Math.Pow((double)(v - mean), 2)).Average();
            return (decimal)Math.Sqrt(variance);
        }

        private decimal CalculateProfitFactor(List<decimal> dailyPnL)
        {
            var profits = dailyPnL.Where(p => p > 0).Sum();
            var losses = Math.Abs(dailyPnL.Where(p => p < 0).Sum());
            return losses > 0 ? profits / losses : profits > 0 ? 10m : 0m;
        }

        private SPX30DTEConfig ConvertChromosomeToConfig(SPX30DTEChromosome chromosome)
        {
            return new SPX30DTEConfig
            {
                SPXCore = new BWBConfiguration
                {
                    WingWidthPoints = chromosome.BWBWingWidth,
                    ProfitTarget = chromosome.BWBProfitTarget,
                    StopLoss = chromosome.BWBStopLoss,
                    MaxPositions = chromosome.BWBMaxPositions,
                    ForcedExitDTE = chromosome.BWBForcedExitDTE
                },
                XSPProbe = new ProbeConfiguration
                {
                    SpreadWidth = chromosome.ProbeSpreadWidth,
                    ProfitTarget = chromosome.ProbeProfitTarget,
                    TargetDTE = chromosome.ProbeDTE,
                    WinRateThreshold = chromosome.ProbeWinRateThreshold
                },
                VIXHedge = new HedgeConfiguration
                {
                    TargetDTE = chromosome.HedgeDTE,
                    MinHedgeCount = chromosome.HedgeMinCount,
                    MaxHedgeCount = chromosome.HedgeMaxCount,
                    PartialCloseThreshold = chromosome.VIXSpikeThreshold,
                    PartialClosePercent = chromosome.PartialClosePercent
                },
                MaxPortfolioRisk = chromosome.MaxPortfolioRisk,
                MaxDrawdownLimit = MAX_ACCEPTABLE_DRAWDOWN,
                StartingCapital = chromosome.StartingCapital
            };
        }

        private List<SPX30DTEChromosome> SelectElite()
        {
            return _population
                .OrderByDescending(c => c.Fitness.OverallScore)
                .Take(_config.EliteCount)
                .ToList();
        }

        private bool ShouldStopEarly(List<GenerationMetrics> metrics)
        {
            if (metrics.Count < 10) return false;
            
            // Stop if no improvement in last 20 generations
            var recent = metrics.TakeLast(20).ToList();
            var bestRecent = recent.Max(m => m.BestFitness);
            var first = recent.First().BestFitness;
            
            var improvement = (bestRecent - first) / Math.Max(1, Math.Abs(first));
            return improvement < 0.01m; // Less than 1% improvement
        }

        private async Task CreateNextGeneration()
        {
            var newPopulation = new List<SPX30DTEChromosome>();
            
            // Keep elite
            var elite = SelectElite();
            newPopulation.AddRange(elite);
            
            // Generate offspring
            while (newPopulation.Count < _config.PopulationSize)
            {
                var parent1 = TournamentSelection();
                var parent2 = TournamentSelection();
                
                var offspring = Crossover(parent1, parent2);
                offspring = Mutate(offspring);
                
                newPopulation.Add(offspring);
            }
            
            _population.Clear();
            _population.AddRange(newPopulation);
        }

        private SPX30DTEChromosome TournamentSelection()
        {
            var tournament = new List<SPX30DTEChromosome>();
            for (int i = 0; i < _config.TournamentSize; i++)
            {
                var candidate = _population[_random.Next(_population.Count)];
                tournament.Add(candidate);
            }
            
            return tournament.OrderByDescending(c => c.Fitness.OverallScore).First();
        }

        private SPX30DTEChromosome Crossover(SPX30DTEChromosome parent1, SPX30DTEChromosome parent2)
        {
            var offspring = new SPX30DTEChromosome
            {
                Id = Guid.NewGuid().ToString(),
                Generation = parent1.Generation + 1
            };
            
            // Uniform crossover - randomly select genes from parents
            offspring.BWBWingWidth = _random.NextDouble() < 0.5 ? parent1.BWBWingWidth : parent2.BWBWingWidth;
            offspring.BWBDeltaTarget = _random.NextDouble() < 0.5 ? parent1.BWBDeltaTarget : parent2.BWBDeltaTarget;
            offspring.BWBProfitTarget = _random.NextDouble() < 0.5 ? parent1.BWBProfitTarget : parent2.BWBProfitTarget;
            offspring.BWBStopLoss = _random.NextDouble() < 0.5 ? parent1.BWBStopLoss : parent2.BWBStopLoss;
            offspring.HedgeRatio = _random.NextDouble() < 0.5 ? parent1.HedgeRatio : parent2.HedgeRatio;
            offspring.MaxPortfolioRisk = _random.NextDouble() < 0.5 ? parent1.MaxPortfolioRisk : parent2.MaxPortfolioRisk;
            offspring.DrawdownFreezeThreshold = _random.NextDouble() < 0.5 ? parent1.DrawdownFreezeThreshold : parent2.DrawdownFreezeThreshold;
            
            // Continue for all genes...
            
            return offspring;
        }

        private SPX30DTEChromosome Mutate(SPX30DTEChromosome chromosome)
        {
            // Apply mutations with decreasing probability based on generation
            var mutationRate = Math.Max(0.01, _config.MutationRate * Math.Pow(0.99, chromosome.Generation));
            
            if (_random.NextDouble() < mutationRate)
            {
                // Mutate random gene with small perturbation
                switch (_random.Next(20)) // 20 key parameters
                {
                    case 0: chromosome.BWBWingWidth += RandomBetween(-5, 5); break;
                    case 1: chromosome.BWBDeltaTarget += RandomBetween(-0.02m, 0.02m); break;
                    case 2: chromosome.BWBProfitTarget += RandomBetween(-0.05m, 0.05m); break;
                    case 3: chromosome.BWBStopLoss += RandomBetween(-0.3m, 0.3m); break;
                    case 4: chromosome.HedgeRatio += RandomBetween(-0.05m, 0.05m); break;
                    case 5: chromosome.MaxPortfolioRisk += RandomBetween(-0.03m, 0.03m); break;
                    // Continue for other parameters...
                }
            }
            
            // Ensure constraints
            ApplyConstraints(chromosome);
            
            return chromosome;
        }

        private void ApplyConstraints(SPX30DTEChromosome chromosome)
        {
            // Ensure all parameters stay within valid ranges
            chromosome.BWBWingWidth = Math.Max(30, Math.Min(80, chromosome.BWBWingWidth));
            chromosome.BWBDeltaTarget = Math.Max(0.08m, Math.Min(0.25m, chromosome.BWBDeltaTarget));
            chromosome.BWBProfitTarget = Math.Max(0.45m, Math.Min(0.80m, chromosome.BWBProfitTarget));
            chromosome.BWBStopLoss = Math.Max(1.2m, Math.Min(3.0m, chromosome.BWBStopLoss));
            chromosome.HedgeRatio = Math.Max(0.10m, Math.Min(0.40m, chromosome.HedgeRatio));
            chromosome.MaxPortfolioRisk = Math.Max(0.15m, Math.Min(0.35m, chromosome.MaxPortfolioRisk));
            // Continue for all parameters...
        }

        private GenerationMetrics CalculateGenerationMetrics(int generation)
        {
            var fitness = _population.Select(c => c.Fitness.OverallScore).ToList();
            
            return new GenerationMetrics
            {
                Generation = generation,
                BestFitness = fitness.Max(),
                AverageFitness = fitness.Average(),
                WorstFitness = fitness.Min(),
                StandardDeviation = CalculateStandardDeviation(fitness),
                ChromosomesMeetingConstraints = _population.Count(c => ValidateConstraints(c))
            };
        }

        private bool ValidateConstraints(SPX30DTEChromosome chromosome)
        {
            if (chromosome.Fitness?.BacktestResult == null) return false;
            
            var result = chromosome.Fitness.BacktestResult;
            
            // Primary constraint: Maximum drawdown
            if (result.MaxDrawdown > MAX_ACCEPTABLE_DRAWDOWN) return false;
            
            // Secondary constraints
            if (result.AnnualizedReturn < 0.10m) return false; // Min 10% annual return
            if (result.WinRate < MIN_WIN_RATE) return false;   // Min 60% win rate
            if (result.SharpeRatio < 0.8m) return false;      // Min Sharpe ratio
            
            return true;
        }

        private List<string> GetConstraintViolations(SPX30DTEChromosome chromosome)
        {
            var violations = new List<string>();
            
            if (chromosome.Fitness?.BacktestResult == null) 
            {
                violations.Add("No backtest result available");
                return violations;
            }
            
            var result = chromosome.Fitness.BacktestResult;
            
            if (result.MaxDrawdown > MAX_ACCEPTABLE_DRAWDOWN)
                violations.Add($"Max drawdown {result.MaxDrawdown:C} exceeds limit {MAX_ACCEPTABLE_DRAWDOWN:C}");
                
            if (result.AnnualizedReturn < 0.10m)
                violations.Add($"Annual return {result.AnnualizedReturn:P2} below minimum 10%");
                
            if (result.WinRate < MIN_WIN_RATE)
                violations.Add($"Win rate {result.WinRate:P2} below minimum {MIN_WIN_RATE:P2}");
                
            if (result.SharpeRatio < 0.8m)
                violations.Add($"Sharpe ratio {result.SharpeRatio:F2} below minimum 0.8");
            
            return violations;
        }

        private SPX30DTEChromosome SelectFinalBest()
        {
            // Select chromosome that best meets constraints and objectives
            var validChromosomes = _population.Where(ValidateConstraints).ToList();
            
            if (validChromosomes.Any())
            {
                return validChromosomes.OrderByDescending(c => c.Fitness.OverallScore).First();
            }
            
            // If no valid chromosomes, return best overall
            return _population.OrderByDescending(c => c.Fitness.OverallScore).First();
        }

        private void ReportProgress(int generation, GenerationMetrics metrics)
        {
            Console.WriteLine($"Gen {generation:D3}: Best={metrics.BestFitness:F2}, " +
                             $"Avg={metrics.AverageFitness:F2}, " +
                             $"Valid={metrics.ChromosomesMeetingConstraints}/{_population.Count}");
        }

        private decimal RandomBetween(decimal min, decimal max)
        {
            return min + (decimal)_random.NextDouble() * (max - min);
        }

        private int RandomBetween(int min, int max)
        {
            return _random.Next(min, max + 1);
        }

        private OptimizationConfig GetDefaultConfig()
        {
            return new OptimizationConfig
            {
                PopulationSize = 50,
                EliteCount = 10,
                TournamentSize = 5,
                MutationRate = 0.1,
                CrossoverRate = 0.8
            };
        }
    }

    // Supporting classes for genetic optimizer
    public class SPX30DTEChromosome
    {
        public string Id { get; set; }
        public int Generation { get; set; }
        public FitnessScore Fitness { get; set; }
        
        // BWB Parameters
        public decimal BWBWingWidth { get; set; }
        public decimal BWBDeltaTarget { get; set; }
        public decimal BWBProfitTarget { get; set; }
        public decimal BWBStopLoss { get; set; }
        public int BWBMaxPositions { get; set; }
        public int BWBForcedExitDTE { get; set; }
        
        // Probe Parameters
        public decimal ProbeSpreadWidth { get; set; }
        public int ProbesDailyMon { get; set; }
        public int ProbesDailyTue { get; set; }
        public int ProbesDailyWed { get; set; }
        public int ProbesDailyThu { get; set; }
        public int ProbesDailyFri { get; set; }
        public int ProbeDTE { get; set; }
        public decimal ProbeWinRateThreshold { get; set; }
        public decimal ProbeProfitTarget { get; set; }
        
        // VIX Hedge Parameters
        public decimal HedgeRatio { get; set; }
        public decimal VIXLongStrikeOffset { get; set; }
        public decimal VIXSpreadWidth { get; set; }
        public int HedgeMinCount { get; set; }
        public int HedgeMaxCount { get; set; }
        public int HedgeDTE { get; set; }
        public decimal VIXSpikeThreshold { get; set; }
        public decimal PartialClosePercent { get; set; }
        
        // Synchronization Parameters
        public decimal MaxCorrelatedRisk { get; set; }
        public decimal MinProbeWinRate { get; set; }
        public int SPXEntryDelayDays { get; set; }
        public decimal DrawdownFreezeThreshold { get; set; }
        public decimal VolatilityScaleFactor { get; set; }
        
        // Capital Management
        public decimal StartingCapital { get; set; }
        public decimal MaxPortfolioRisk { get; set; }
        public int RevFibUpgradeDays { get; set; }
        public decimal RevFibDowngradeThreshold { get; set; }
        public decimal EmergencyStopPercent { get; set; }
        
        // Market Regime
        public decimal HighVIXThreshold { get; set; }
        public decimal LowVIXThreshold { get; set; }
        public decimal TrendStrengthThreshold { get; set; }
        public decimal RegimeSwitchSensitivity { get; set; }
        
        // Greek Limits
        public decimal MaxDeltaExposure { get; set; }
        public decimal MaxVegaExposure { get; set; }
        public decimal MinThetaDecay { get; set; }
        public decimal MaxGammaRisk { get; set; }
    }

    public class FitnessScore
    {
        public decimal OverallScore { get; set; }
        public decimal DrawdownScore { get; set; }
        public decimal ReturnsScore { get; set; }
        public decimal SharpeScore { get; set; }
        public decimal WinRateScore { get; set; }
        public decimal ConsistencyScore { get; set; }
        public decimal StressTestScore { get; set; }
        public decimal ConstraintPenalty { get; set; }
        
        // Enhanced capital efficiency scoring
        public decimal CapitalEfficiencyScore { get; set; }
        public decimal LeverageOptimizationScore { get; set; }
        public decimal TurnoverEfficiencyScore { get; set; }
        
        public BacktestResult BacktestResult { get; set; }
        public DateTime EvaluationDate { get; set; }
        public string Error { get; set; }
    }

    public class BacktestResult
    {
        public decimal TotalReturn { get; set; }
        public decimal AnnualizedReturn { get; set; }
        public decimal MaxDrawdown { get; set; }
        public decimal SharpeRatio { get; set; }
        public decimal WinRate { get; set; }
        public List<decimal> MonthlyReturns { get; set; }
        public Dictionary<string, decimal> CrisisPerformance { get; set; }
        public int TotalTrades { get; set; }
        public decimal ProfitFactor { get; set; }
        
        // Enhanced properties for capital efficiency optimization
        public decimal TotalCapital { get; set; } = 100000m;
        public decimal MaxCapitalUsed { get; set; }
        public decimal AvgCapitalDeployed { get; set; }
        public decimal TotalPnL { get; set; }
        public decimal CapitalEfficiency => TotalCapital > 0 ? AnnualizedReturn / (MaxCapitalUsed / TotalCapital) : 0;
        public decimal LeverageRatio => TotalCapital > 0 ? MaxCapitalUsed / TotalCapital : 0;
        public decimal ProfitPerTrade => TotalTrades > 0 ? TotalPnL / TotalTrades : 0;
    }

    public class PeriodResult
    {
        public decimal TotalReturn { get; set; }
        public decimal AnnualizedReturn { get; set; }
        public decimal MaxDrawdown { get; set; }
        public decimal SharpeRatio { get; set; }
        public decimal WinRate { get; set; }
        public List<decimal> MonthlyReturns { get; set; }
        public int TotalTrades { get; set; }
        public decimal ProfitFactor { get; set; }
    }

    public class GenerationMetrics
    {
        public int Generation { get; set; }
        public decimal BestFitness { get; set; }
        public decimal AverageFitness { get; set; }
        public decimal WorstFitness { get; set; }
        public decimal StandardDeviation { get; set; }
        public int ChromosomesMeetingConstraints { get; set; }
    }

    public class OptimizationResult
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Strategy { get; set; }
        public int GenerationsCompleted { get; set; }
        public SPX30DTEChromosome BestChromosome { get; set; }
        public List<SPX30DTEChromosome> TopPerformers { get; set; }
        public List<GenerationMetrics> GenerationHistory { get; set; }
        public Dictionary<string, decimal> TargetMetrics { get; set; }
        public bool MeetsConstraints { get; set; }
        public List<string> ConstraintViolations { get; set; }
    }

    public class OptimizationConfig
    {
        public int PopulationSize { get; set; } = 50;
        public int EliteCount { get; set; } = 10;
        public int TournamentSize { get; set; } = 5;
        public double MutationRate { get; set; } = 0.1;
        public double CrossoverRate { get; set; } = 0.8;
    }

    public class Trade
    {
        public DateTime EntryDate { get; set; }
        public DateTime ExitDate { get; set; }
        public decimal PnL { get; set; }
        public string Strategy { get; set; }
    }
}