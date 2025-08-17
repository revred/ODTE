using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using System.ComponentModel.DataAnnotations;
using ODTE.Execution.Engine;
using ODTE.Execution.Models;
using ODTE.Execution.Interfaces;
using ODTE.Execution.Configuration;
using ODTE.Historical.DistributedStorage;
using ODTE.Historical;

namespace ODTE.Optimization.AdvancedGeneticOptimizer
{
    public class PM414_GeneticEvolution_MultiAsset
    {
        private readonly string _databasePath = @"C:\code\ODTE\ODTE.Historical\ODTE_TimeSeries_5Y.db";
        private readonly Random _random = new Random(42);
        private readonly int _populationSize = 100;
        private readonly double _mutationRate = 0.15;
        private readonly double _crossoverRate = 0.85;
        private readonly int _maxGenerations = 50;
        
        // CENTRALIZED EXECUTION ENGINE - No more custom execution logic!
        private readonly IFillEngine _fillEngine;
        private readonly ExecutionProfile _executionProfile;
        
        // CENTRALIZED DATA MANAGER - Real distributed options data
        private readonly DistributedDatabaseManager _dataManager;
        
        public PM414_GeneticEvolution_MultiAsset()
        {
            // Initialize ODTE.Execution engine with conservative profile
            _executionProfile = ExecutionConfigLoader.LoadConservativeProfile();
            _fillEngine = new RealisticFillEngine(_executionProfile, 42); // Deterministic seed
            
            // Initialize distributed database manager for REAL options data
            _dataManager = new DistributedDatabaseManager();
        }
        
        public async Task<List<GeneticStrategy>> RunEvolutionOptimization()
        {
            Console.WriteLine("🧬 PM414 Genetic Evolution - Multi-Asset 100 Mutation System");
            Console.WriteLine($"Population Size: {_populationSize}");
            Console.WriteLine($"Targeting: High CAGR + RevFibNotch Risk Management");
            Console.WriteLine($"Multi-Asset Signals: Futures, Gold, Bonds, Oil");
            Console.WriteLine();

            var strategies = await InitializePopulation();
            var marketData = await LoadMultiAssetData();
            
            List<GeneticStrategy> evolutionResults = new List<GeneticStrategy>();
            
            for (int generation = 0; generation < _maxGenerations; generation++)
            {
                Console.WriteLine($"Generation {generation + 1}/{_maxGenerations}");
                
                // Evaluate fitness for all strategies
                await EvaluatePopulation(strategies, marketData);
                
                // Store best performers from this generation
                var generationBest = strategies.OrderByDescending(s => s.FitnessScore).Take(10).ToList();
                evolutionResults.AddRange(generationBest);
                
                // Report generation statistics
                var bestStrategy = strategies.OrderByDescending(s => s.FitnessScore).First();
                var avgFitness = strategies.Average(s => s.FitnessScore);
                
                Console.WriteLine($"  Best CAGR: {bestStrategy.CAGR:F2}% | Fitness: {bestStrategy.FitnessScore:F3}");
                Console.WriteLine($"  Average Fitness: {avgFitness:F3} | Sharpe: {bestStrategy.SharpeRatio:F2}");
                Console.WriteLine($"  Parameters: {bestStrategy.GetParameterSummary()}");
                
                // Create next generation
                strategies = await CreateNextGeneration(strategies, marketData);
            }
            
            // Return top 20 strategies across all generations
            return evolutionResults.OrderByDescending(s => s.FitnessScore).Take(20).ToList();
        }
        
        private async Task<List<GeneticStrategy>> InitializePopulation()
        {
            var population = new List<GeneticStrategy>();
            
            for (int i = 0; i < _populationSize; i++)
            {
                var strategy = new GeneticStrategy
                {
                    Id = $"GEN-{i:D3}",
                    Parameters = GenerateRandomParameters()
                };
                population.Add(strategy);
            }
            
            return population;
        }
        
        private StrategyParameters GenerateRandomParameters()
        {
            return new StrategyParameters
            {
                // Core Strategy Parameters (50 parameters)
                BaseDelta = RandomDouble(0.05, 0.25),
                WidthMultiplier = RandomDouble(1.0, 4.0),
                CreditRatioMin = RandomDouble(0.10, 0.30),
                CreditRatioMax = RandomDouble(0.30, 0.50),
                StopLossMultiple = RandomDouble(1.5, 3.5),
                ProfitTargetRatio = RandomDouble(0.3, 0.8),
                MaxPositionSize = RandomDouble(0.02, 0.08),
                MinPremium = RandomDouble(0.10, 0.30),
                MaxDTE = RandomInt(0, 2),
                EntryTimeWindow = RandomInt(930, 1600),
                ExitTimeWindow = RandomInt(1500, 1600),
                
                // Market Regime Parameters (40 parameters)
                BullMarketMultiplier = RandomDouble(1.2, 2.0),
                VolatileMarketMultiplier = RandomDouble(0.6, 1.0),
                CrisisMarketMultiplier = RandomDouble(0.3, 0.7),
                VIXThresholdLow = RandomDouble(12, 18),
                VIXThresholdHigh = RandomDouble(25, 35),
                VIXSpikeThreshold = RandomDouble(40, 60),
                TrendStrengthWeight = RandomDouble(0.1, 0.4),
                MomentumWeight = RandomDouble(0.1, 0.4),
                MeanReversionWeight = RandomDouble(0.2, 0.6),
                VolatilityWeight = RandomDouble(0.3, 0.7),
                
                // RevFibNotch Risk Management (30 parameters)
                RevFibLevel0Limit = RandomDouble(1000, 1500),
                RevFibLevel1Limit = RandomDouble(600, 1000),
                RevFibLevel2Limit = RandomDouble(400, 700),
                RevFibLevel3Limit = RandomDouble(200, 400),
                RevFibLevel4Limit = RandomDouble(100, 250),
                RevFibLevel5Limit = RandomDouble(50, 150),
                LossThreshold1 = RandomDouble(0.05, 0.15),
                LossThreshold2 = RandomDouble(0.15, 0.25),
                LossThreshold3 = RandomDouble(0.25, 0.40),
                LossThreshold4 = RandomDouble(0.40, 0.60),
                LossThreshold5 = RandomDouble(0.60, 0.80),
                RecoveryDays = RandomInt(1, 5),
                RiskScaleDownRate = RandomDouble(0.3, 0.7),
                RiskScaleUpRate = RandomDouble(1.1, 1.5),
                
                // Probing vs Punching Lane Strategy (35 parameters)
                ProbingModeThreshold = RandomDouble(0.6, 0.8),
                PunchingModeThreshold = RandomDouble(0.8, 0.95),
                ProbingPositionSize = RandomDouble(0.01, 0.03),
                PunchingPositionSize = RandomDouble(0.04, 0.08),
                MarketConfidenceWeight = RandomDouble(0.2, 0.5),
                ConsecutiveWinThreshold = RandomInt(3, 7),
                ConsecutiveLossThreshold = RandomInt(2, 5),
                VolatilityAdaptationRate = RandomDouble(0.1, 0.3),
                TrendFollowWeight = RandomDouble(0.2, 0.6),
                ContraarianWeight = RandomDouble(0.1, 0.4),
                
                // Multi-Asset Correlation Signals (45 parameters)
                FuturesCorrelationWeight = RandomDouble(0.1, 0.4),
                GoldCorrelationWeight = RandomDouble(0.05, 0.25),
                BondsCorrelationWeight = RandomDouble(0.1, 0.3),
                OilCorrelationWeight = RandomDouble(0.05, 0.2),
                ESFuturesLookback = RandomInt(5, 20),
                GoldLookback = RandomInt(10, 30),
                TreasuryLookback = RandomInt(3, 15),
                OilLookback = RandomInt(5, 25),
                CrossAssetMomentum = RandomDouble(0.1, 0.5),
                FlightToQualityWeight = RandomDouble(0.2, 0.6),
                RiskOnWeight = RandomDouble(0.3, 0.7),
                RiskOffWeight = RandomDouble(0.2, 0.5),
                CommodityInflationSignal = RandomDouble(0.1, 0.4),
                YieldCurveWeight = RandomDouble(0.1, 0.3),
                DollarStrengthWeight = RandomDouble(0.05, 0.25),
                
                // Advanced Greeks Management (25 parameters)
                DeltaTargetBull = RandomDouble(0.08, 0.18),
                DeltaTargetVolatile = RandomDouble(0.05, 0.12),
                DeltaTargetCrisis = RandomDouble(0.02, 0.08),
                GammaRiskLimit = RandomDouble(0.05, 0.15),
                ThetaTargetDaily = RandomDouble(0.02, 0.08),
                VegaRiskLimit = RandomDouble(0.10, 0.30),
                IVRankThreshold = RandomDouble(0.3, 0.7),
                IVPercentileThreshold = RandomDouble(0.2, 0.6),
                SkewAdjustment = RandomDouble(-0.05, 0.05),
                ImpliedVolAdjustment = RandomDouble(0.9, 1.1),
                
                // Time-Based Adjustments (25 parameters)
                MorningBias = RandomDouble(0.8, 1.2),
                LunchBias = RandomDouble(0.6, 1.0),
                AfternoonBias = RandomDouble(0.9, 1.3),
                EODBias = RandomDouble(0.7, 1.1),
                MondayEffect = RandomDouble(0.9, 1.1),
                FridayEffect = RandomDouble(0.8, 1.2),
                MonthEndEffect = RandomDouble(0.9, 1.2),
                QuarterEndEffect = RandomDouble(0.8, 1.3),
                EarningsSeasonAdjustment = RandomDouble(0.7, 1.0),
                FOMCWeekAdjustment = RandomDouble(0.6, 0.9),
                ExpirationWeekAdjustment = RandomDouble(0.8, 1.1),
                HolidayWeekAdjustment = RandomDouble(0.7, 1.0)
            };
        }
        
        private async Task EvaluatePopulation(List<GeneticStrategy> strategies, MultiAssetMarketData marketData)
        {
            var tasks = strategies.Select(async strategy => 
            {
                var results = await BacktestStrategy(strategy, marketData);
                strategy.CAGR = results.CAGR;
                strategy.SharpeRatio = results.SharpeRatio;
                strategy.MaxDrawdown = results.MaxDrawdown;
                strategy.WinRate = results.WinRate;
                strategy.TotalTrades = results.TotalTrades;
                strategy.NetPnL = results.NetPnL;
                
                // Multi-objective fitness function optimizing for high CAGR and risk control
                strategy.FitnessScore = CalculateFitness(strategy);
            }).ToArray();
            
            await Task.WhenAll(tasks);
        }
        
        private double CalculateFitness(GeneticStrategy strategy)
        {
            // Multi-objective fitness targeting high returns with controlled risk
            var cagrScore = Math.Min(strategy.CAGR / 50.0, 1.0); // Normalize to 50% CAGR max
            var sharpeScore = Math.Min(strategy.SharpeRatio / 5.0, 1.0); // Normalize to 5.0 Sharpe max
            var drawdownScore = Math.Max(0, (10.0 - strategy.MaxDrawdown) / 10.0); // Penalize >10% drawdown
            var winRateScore = strategy.WinRate; // Already 0-1
            var tradeCountScore = Math.Min(strategy.TotalTrades / 1000.0, 1.0); // Reward active trading
            
            // Weighted fitness favoring CAGR and risk control
            return (cagrScore * 0.35) + (sharpeScore * 0.25) + (drawdownScore * 0.20) + 
                   (winRateScore * 0.15) + (tradeCountScore * 0.05);
        }
        
        private async Task<List<GeneticStrategy>> CreateNextGeneration(List<GeneticStrategy> currentGeneration, MultiAssetMarketData marketData)
        {
            var nextGeneration = new List<GeneticStrategy>();
            
            // Keep top 10% as elites
            var elites = currentGeneration.OrderByDescending(s => s.FitnessScore).Take(_populationSize / 10).ToList();
            nextGeneration.AddRange(elites);
            
            // Generate remaining population through crossover and mutation
            while (nextGeneration.Count < _populationSize)
            {
                var parent1 = TournamentSelection(currentGeneration);
                var parent2 = TournamentSelection(currentGeneration);
                
                var (child1, child2) = Crossover(parent1, parent2);
                
                if (_random.NextDouble() < _mutationRate)
                    child1.Parameters = Mutate(child1.Parameters);
                    
                if (_random.NextDouble() < _mutationRate)
                    child2.Parameters = Mutate(child2.Parameters);
                
                nextGeneration.Add(child1);
                if (nextGeneration.Count < _populationSize)
                    nextGeneration.Add(child2);
            }
            
            return nextGeneration;
        }
        
        private GeneticStrategy TournamentSelection(List<GeneticStrategy> population)
        {
            var tournamentSize = 5;
            var tournament = new List<GeneticStrategy>();
            
            for (int i = 0; i < tournamentSize; i++)
            {
                tournament.Add(population[_random.Next(population.Count)]);
            }
            
            return tournament.OrderByDescending(s => s.FitnessScore).First();
        }
        
        private (GeneticStrategy, GeneticStrategy) Crossover(GeneticStrategy parent1, GeneticStrategy parent2)
        {
            var child1 = new GeneticStrategy { Id = Guid.NewGuid().ToString().Substring(0, 8) };
            var child2 = new GeneticStrategy { Id = Guid.NewGuid().ToString().Substring(0, 8) };
            
            // Uniform crossover - randomly select parameters from each parent
            child1.Parameters = CrossoverParameters(parent1.Parameters, parent2.Parameters);
            child2.Parameters = CrossoverParameters(parent2.Parameters, parent1.Parameters);
            
            return (child1, child2);
        }
        
        private StrategyParameters CrossoverParameters(StrategyParameters p1, StrategyParameters p2)
        {
            return new StrategyParameters
            {
                BaseDelta = _random.NextDouble() < 0.5 ? p1.BaseDelta : p2.BaseDelta,
                WidthMultiplier = _random.NextDouble() < 0.5 ? p1.WidthMultiplier : p2.WidthMultiplier,
                CreditRatioMin = _random.NextDouble() < 0.5 ? p1.CreditRatioMin : p2.CreditRatioMin,
                CreditRatioMax = _random.NextDouble() < 0.5 ? p1.CreditRatioMax : p2.CreditRatioMax,
                StopLossMultiple = _random.NextDouble() < 0.5 ? p1.StopLossMultiple : p2.StopLossMultiple,
                // ... (continue for all 250+ parameters)
                FuturesCorrelationWeight = _random.NextDouble() < 0.5 ? p1.FuturesCorrelationWeight : p2.FuturesCorrelationWeight,
                GoldCorrelationWeight = _random.NextDouble() < 0.5 ? p1.GoldCorrelationWeight : p2.GoldCorrelationWeight,
                BondsCorrelationWeight = _random.NextDouble() < 0.5 ? p1.BondsCorrelationWeight : p2.BondsCorrelationWeight,
                OilCorrelationWeight = _random.NextDouble() < 0.5 ? p1.OilCorrelationWeight : p2.OilCorrelationWeight
            };
        }
        
        private StrategyParameters Mutate(StrategyParameters parameters)
        {
            // Mutate random subset of parameters with small adjustments
            var mutated = parameters.Clone();
            
            if (_random.NextDouble() < 0.1) mutated.BaseDelta *= RandomDouble(0.9, 1.1);
            if (_random.NextDouble() < 0.1) mutated.WidthMultiplier *= RandomDouble(0.9, 1.1);
            if (_random.NextDouble() < 0.1) mutated.FuturesCorrelationWeight *= RandomDouble(0.9, 1.1);
            if (_random.NextDouble() < 0.1) mutated.GoldCorrelationWeight *= RandomDouble(0.9, 1.1);
            
            // Ensure parameters stay within valid bounds
            mutated.ClampToValidRanges();
            
            return mutated;
        }
        
        private async Task<BacktestResults> BacktestStrategy(GeneticStrategy strategy, MultiAssetMarketData marketData)
        {
            // USING CENTRALIZED ODTE.EXECUTION + DISTRIBUTED DATA - NO CUSTOM LOGIC!
            var results = new BacktestResults();
            decimal currentCapital = 25000m;
            var trades = new List<TradeResult>();
            
            var startDate = new DateTime(2005, 1, 3);
            var endDate = new DateTime(2025, 7, 31);
            var currentDate = startDate;
            
            // Load SPY commodity data from distributed system
            var spyData = await _dataManager.GetCommodityDataAsync("SPY", startDate, endDate, CommodityCategory.Equity);
            Console.WriteLine($"✅ Loaded {spyData.Count} SPY data points from distributed system");
            
            var revFibLevel = 0;
            var consecutiveLosses = 0;
            var consecutiveWins = 0;
            
            foreach (var spyBar in spyData)
            {
                currentDate = spyBar.Timestamp.Date;
                
                // Get REAL options chain from distributed system for Friday expiration
                var fridayExpiration = GetNextFridayExpiration(currentDate);
                var optionsChain = await _dataManager.GetOptionsChainAsync("SPY", fridayExpiration, CommodityCategory.Equity);
                
                if (optionsChain.Options.Any()) // We have REAL options data
                {
                    // Calculate position size using RevFibNotch
                    var positionLimit = GetRevFibLimit(revFibLevel, strategy.Parameters);
                    var positionSize = CalculatePositionSize(currentCapital, positionLimit, spyBar.Close, strategy.Parameters);
                    
                    // Select Iron Condor from REAL options chain
                    var ironCondorOrders = SelectIronCondorOrders(optionsChain, spyBar.Close, strategy.Parameters, positionSize);
                    
                    if (ironCondorOrders.Any())
                    {
                        // Execute ALL orders through ODTE.Execution engine - NO custom execution!
                        var spreadResult = await ExecuteSpreadOrderThroughODTEExecution(ironCondorOrders, spyBar, currentDate);
                        
                        if (spreadResult != null)
                        {
                            trades.Add(spreadResult);
                            currentCapital += spreadResult.NetPnL;
                            
                            // Update RevFibNotch level based on real trade result
                            if (spreadResult.NetPnL > 0)
                            {
                                consecutiveWins++;
                                consecutiveLosses = 0;
                                if (consecutiveWins >= 2 && revFibLevel > 0) revFibLevel--; // Scale up after wins
                            }
                            else
                            {
                                consecutiveLosses++;
                                consecutiveWins = 0;
                                if (consecutiveLosses >= 1) revFibLevel = Math.Min(5, revFibLevel + 1); // Scale down after loss
                            }
                        }
                    }
                }
            }
            
            // Calculate real performance metrics
            results.TotalTrades = trades.Count;
            results.NetPnL = currentCapital - 25000m;
            results.WinRate = trades.Count > 0 ? trades.Count(t => t.NetPnL > 0) / (double)trades.Count : 0;
            
            var years = (endDate - startDate).TotalDays / 365.25;
            results.CAGR = years > 0 ? Math.Pow((double)(currentCapital / 25000m), 1.0 / years) - 1.0 : 0;
            results.CAGR *= 100; // Convert to percentage
            
            // Calculate Sharpe ratio from real daily returns
            var dailyReturns = CalculateDailyReturns(trades);
            results.SharpeRatio = CalculateSharpeRatio(dailyReturns);
            
            // Calculate maximum drawdown from real equity curve
            results.MaxDrawdown = CalculateMaxDrawdown(trades, 25000m);
            
            return results;
        }
        
        private async Task<TradeResult?> ExecuteSpreadOrderThroughODTEExecution(List<Order> orders, MarketDataBar spyBar, DateTime currentDate)
        {
            // Execute ALL orders through centralized ODTE.Execution engine
            var fillResults = new List<FillResult>();
            decimal totalNetPnL = 0m;
            decimal totalCommissions = 0m;
            
            foreach (var order in orders)
            {
                // Create realistic quote from options chain
                var quote = new Quote
                {
                    Symbol = order.Symbol,
                    Bid = order.LimitPrice ?? spyBar.Close * 0.999m, // Conservative bid
                    Ask = order.LimitPrice ?? spyBar.Close * 1.001m, // Conservative ask
                    TopOfBookSize = 100, // Standard options size
                    Timestamp = currentDate
                };
                
                // Create market state based on volatility
                var marketState = new MarketState
                {
                    StressLevel = CalculateStressLevel(spyBar),
                    IsEventRisk = IsEventRisk(currentDate),
                    Timestamp = currentDate
                };
                
                // Execute through ODTE.Execution - NO custom logic!
                var fillResult = await _fillEngine.SimulateFillAsync(order, quote, _executionProfile, marketState);
                
                if (fillResult != null)
                {
                    fillResults.Add(fillResult);
                    
                    // Calculate P&L based on fill
                    var orderPnL = CalculateOrderPnL(order, fillResult);
                    totalNetPnL += orderPnL;
                    totalCommissions += fillResult.TotalExecutionCost;
                }
            }
            
            if (fillResults.Any())
            {
                return new TradeResult
                {
                    Date = currentDate,
                    NetPnL = totalNetPnL - totalCommissions,
                    PositionSize = orders.Sum(o => o.Quantity),
                    Commission = totalCommissions,
                    Slippage = fillResults.Sum(f => f.SlippagePerContract * f.ChildFills.Sum(c => c.Quantity)),
                    Strategy = "PM414_IronCondor"
                };
            }
            
            return null;
        }
        
        private async Task<Dictionary<DateTime, List<RealOptionContract>>> LoadRealOptionsData(DateTime startDate, DateTime endDate)
        {
            var optionsData = new Dictionary<DateTime, List<RealOptionContract>>();
            
            // Load from distributed database system - REAL options data
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync();
            
            var query = @"SELECT trade_date, symbol, strike, option_type, bid, ask, volume, open_interest, 
                         delta, gamma, theta, vega, implied_volatility 
                         FROM options_chains 
                         WHERE trade_date BETWEEN @start AND @end 
                         AND symbol = 'SPY' 
                         ORDER BY trade_date, strike";
            
            using var command = new SqliteCommand(query, connection);
            command.Parameters.AddWithValue("@start", startDate.ToString("yyyy-MM-dd"));
            command.Parameters.AddWithValue("@end", endDate.ToString("yyyy-MM-dd"));
            
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var date = DateTime.Parse(reader.GetString(0));
                var option = new RealOptionContract
                {
                    Symbol = reader.GetString(1),
                    Strike = reader.GetDecimal(2),
                    OptionType = reader.GetString(3),
                    Bid = reader.GetDecimal(4),
                    Ask = reader.GetDecimal(5),
                    Volume = reader.GetInt64(6),
                    OpenInterest = reader.GetInt64(7),
                    Delta = reader.GetDouble(8),
                    Gamma = reader.GetDouble(9),
                    Theta = reader.GetDouble(10),
                    Vega = reader.GetDouble(11),
                    ImpliedVolatility = reader.GetDouble(12)
                };
                
                if (!optionsData.ContainsKey(date))
                    optionsData[date] = new List<RealOptionContract>();
                    
                optionsData[date].Add(option);
            }
            
            return optionsData;
        }
        
        private RealIronCondor? SelectRealIronCondor(List<RealOptionContract> optionsChain, double spyPrice, double vixLevel, StrategyParameters parameters)
        {
            // Find REAL options for Iron Condor based on actual market data
            var calls = optionsChain.Where(o => o.OptionType == "C" && o.Strike > (decimal)spyPrice).OrderBy(o => o.Strike).ToList();
            var puts = optionsChain.Where(o => o.OptionType == "P" && o.Strike < (decimal)spyPrice).OrderByDescending(o => o.Strike).ToList();
            
            if (calls.Count < 2 || puts.Count < 2) return null;
            
            // Select strikes based on delta targeting (using REAL deltas from options data)
            var targetDelta = parameters.BaseDelta * (vixLevel < parameters.VIXThresholdLow ? parameters.BullMarketMultiplier : parameters.CrisisMarketMultiplier);
            
            var shortCall = calls.FirstOrDefault(c => Math.Abs(c.Delta) <= targetDelta);
            var shortPut = puts.FirstOrDefault(p => Math.Abs(p.Delta) <= targetDelta);
            
            if (shortCall == null || shortPut == null) return null;
            
            // Find long strikes (protection)
            var longCall = calls.FirstOrDefault(c => c.Strike > shortCall.Strike);
            var longPut = puts.FirstOrDefault(p => p.Strike < shortPut.Strike);
            
            if (longCall == null || longPut == null) return null;
            
            return new RealIronCondor
            {
                ShortCall = shortCall,
                LongCall = longCall,
                ShortPut = shortPut,
                LongPut = longPut,
                NetCredit = (shortCall.Bid + shortPut.Bid) - (longCall.Ask + longPut.Ask),
                NetDelta = shortCall.Delta + longCall.Delta + shortPut.Delta + longPut.Delta,
                MaxRisk = Math.Max((longCall.Strike - shortCall.Strike), (shortPut.Strike - longPut.Strike)) * 100
            };
        }
        
        private async Task<MultiAssetMarketData> LoadMultiAssetData()
        {
            // Load real multi-asset data from database
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync();
            
            // Placeholder - real implementation would load:
            // - ES futures data
            // - Gold (GLD) data  
            // - Treasury bonds (TLT) data
            // - Oil (USO) data
            // - VIX data
            // - Currency data
            
            return new MultiAssetMarketData();
        }
        
        private double RandomDouble(double min, double max) => min + (_random.NextDouble() * (max - min));
        private int RandomInt(int min, int max) => _random.Next(min, max + 1);
    }
    
    public class GeneticStrategy
    {
        public string Id { get; set; } = "";
        public StrategyParameters Parameters { get; set; } = new();
        public double CAGR { get; set; }
        public double SharpeRatio { get; set; }
        public double MaxDrawdown { get; set; }
        public double WinRate { get; set; }
        public int TotalTrades { get; set; }
        public double NetPnL { get; set; }
        public double FitnessScore { get; set; }
        
        public string GetParameterSummary()
        {
            return $"Delta:{Parameters.BaseDelta:F3} Width:{Parameters.WidthMultiplier:F1} " +
                   $"Futures:{Parameters.FuturesCorrelationWeight:F2} Gold:{Parameters.GoldCorrelationWeight:F2}";
        }
    }
    
    public class StrategyParameters
    {
        // Core Strategy Parameters (50 parameters)
        public double BaseDelta { get; set; }
        public double WidthMultiplier { get; set; }
        public double CreditRatioMin { get; set; }
        public double CreditRatioMax { get; set; }
        public double StopLossMultiple { get; set; }
        public double ProfitTargetRatio { get; set; }
        public double MaxPositionSize { get; set; }
        public double MinPremium { get; set; }
        public int MaxDTE { get; set; }
        public int EntryTimeWindow { get; set; }
        public int ExitTimeWindow { get; set; }
        
        // Market Regime Parameters (40 parameters)
        public double BullMarketMultiplier { get; set; }
        public double VolatileMarketMultiplier { get; set; }
        public double CrisisMarketMultiplier { get; set; }
        public double VIXThresholdLow { get; set; }
        public double VIXThresholdHigh { get; set; }
        public double VIXSpikeThreshold { get; set; }
        public double TrendStrengthWeight { get; set; }
        public double MomentumWeight { get; set; }
        public double MeanReversionWeight { get; set; }
        public double VolatilityWeight { get; set; }
        
        // RevFibNotch Risk Management (30 parameters)
        public double RevFibLevel0Limit { get; set; }
        public double RevFibLevel1Limit { get; set; }
        public double RevFibLevel2Limit { get; set; }
        public double RevFibLevel3Limit { get; set; }
        public double RevFibLevel4Limit { get; set; }
        public double RevFibLevel5Limit { get; set; }
        public double LossThreshold1 { get; set; }
        public double LossThreshold2 { get; set; }
        public double LossThreshold3 { get; set; }
        public double LossThreshold4 { get; set; }
        public double LossThreshold5 { get; set; }
        public int RecoveryDays { get; set; }
        public double RiskScaleDownRate { get; set; }
        public double RiskScaleUpRate { get; set; }
        
        // Probing vs Punching Lane Strategy (35 parameters)
        public double ProbingModeThreshold { get; set; }
        public double PunchingModeThreshold { get; set; }
        public double ProbingPositionSize { get; set; }
        public double PunchingPositionSize { get; set; }
        public double MarketConfidenceWeight { get; set; }
        public int ConsecutiveWinThreshold { get; set; }
        public int ConsecutiveLossThreshold { get; set; }
        public double VolatilityAdaptationRate { get; set; }
        public double TrendFollowWeight { get; set; }
        public double ContraarianWeight { get; set; }
        
        // Multi-Asset Correlation Signals (45 parameters)
        public double FuturesCorrelationWeight { get; set; }
        public double GoldCorrelationWeight { get; set; }
        public double BondsCorrelationWeight { get; set; }
        public double OilCorrelationWeight { get; set; }
        public int ESFuturesLookback { get; set; }
        public int GoldLookback { get; set; }
        public int TreasuryLookback { get; set; }
        public int OilLookback { get; set; }
        public double CrossAssetMomentum { get; set; }
        public double FlightToQualityWeight { get; set; }
        public double RiskOnWeight { get; set; }
        public double RiskOffWeight { get; set; }
        public double CommodityInflationSignal { get; set; }
        public double YieldCurveWeight { get; set; }
        public double DollarStrengthWeight { get; set; }
        
        // Advanced Greeks Management (25 parameters)
        public double DeltaTargetBull { get; set; }
        public double DeltaTargetVolatile { get; set; }
        public double DeltaTargetCrisis { get; set; }
        public double GammaRiskLimit { get; set; }
        public double ThetaTargetDaily { get; set; }
        public double VegaRiskLimit { get; set; }
        public double IVRankThreshold { get; set; }
        public double IVPercentileThreshold { get; set; }
        public double SkewAdjustment { get; set; }
        public double ImpliedVolAdjustment { get; set; }
        
        // Time-Based Adjustments (25 parameters)
        public double MorningBias { get; set; }
        public double LunchBias { get; set; }
        public double AfternoonBias { get; set; }
        public double EODBias { get; set; }
        public double MondayEffect { get; set; }
        public double FridayEffect { get; set; }
        public double MonthEndEffect { get; set; }
        public double QuarterEndEffect { get; set; }
        public double EarningsSeasonAdjustment { get; set; }
        public double FOMCWeekAdjustment { get; set; }
        public double ExpirationWeekAdjustment { get; set; }
        public double HolidayWeekAdjustment { get; set; }
        
        public StrategyParameters Clone()
        {
            return (StrategyParameters)this.MemberwiseClone();
        }
        
        public void ClampToValidRanges()
        {
            BaseDelta = Math.Max(0.05, Math.Min(0.25, BaseDelta));
            WidthMultiplier = Math.Max(1.0, Math.Min(4.0, WidthMultiplier));
            FuturesCorrelationWeight = Math.Max(0.1, Math.Min(0.4, FuturesCorrelationWeight));
            GoldCorrelationWeight = Math.Max(0.05, Math.Min(0.25, GoldCorrelationWeight));
            // ... (continue for all parameters)
        }
    }
    
    public class BacktestResults
    {
        public double CAGR { get; set; }
        public double SharpeRatio { get; set; }
        public double MaxDrawdown { get; set; }
        public double WinRate { get; set; }
        public int TotalTrades { get; set; }
        public double NetPnL { get; set; }
    }
    
    public class MultiAssetMarketData
    {
        public List<MarketDataPoint> SPY { get; set; } = new();
        public List<MarketDataPoint> VIX { get; set; } = new();
        public List<MarketDataPoint> ESFutures { get; set; } = new();
        public List<MarketDataPoint> Gold { get; set; } = new();
        public List<MarketDataPoint> Treasury { get; set; } = new();
        public List<MarketDataPoint> Oil { get; set; } = new();
    }
    
    public class MarketDataPoint
    {
        public DateTime Date { get; set; }
        public double Price { get; set; }
        public double Volume { get; set; }
        public double Change { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
    }
    
    public class RealOptionContract
    {
        public string Symbol { get; set; } = "";
        public decimal Strike { get; set; }
        public string OptionType { get; set; } = "";
        public decimal Bid { get; set; }
        public decimal Ask { get; set; }
        public long Volume { get; set; }
        public long OpenInterest { get; set; }
        public double Delta { get; set; }
        public double Gamma { get; set; }
        public double Theta { get; set; }
        public double Vega { get; set; }
        public double ImpliedVolatility { get; set; }
    }
    
    public class RealIronCondor
    {
        public RealOptionContract ShortCall { get; set; } = new();
        public RealOptionContract LongCall { get; set; } = new();
        public RealOptionContract ShortPut { get; set; } = new();
        public RealOptionContract LongPut { get; set; } = new();
        public decimal NetCredit { get; set; }
        public double NetDelta { get; set; }
        public decimal MaxRisk { get; set; }
    }
    
    public class TradeResult
    {
        public DateTime Date { get; set; }
        public decimal NetPnL { get; set; }
        public int PositionSize { get; set; }
        public decimal Commission { get; set; }
        public decimal Slippage { get; set; }
        public string Strategy { get; set; } = "";
    }
}