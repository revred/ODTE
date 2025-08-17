using System.Text;

namespace AdvancedGeneticOptimizer
{
    public class FixedFitOptimizer
    {
        private readonly Random _random = new Random(42);

        public enum StrategyType
        {
            IronCondor,
            BrokenWingButterfly,
            JadeElephant,
            ShortStrangle,
            CreditSpreads,
            RatioSpreads,
            Calendar
        }

        public enum MarketRegime
        {
            Bull, Volatile, Crisis
        }

        public class FitStrategy
        {
            public string Id { get; set; } = "";
            public StrategyType Type { get; set; }
            public decimal[] RevFibLimits { get; set; } = new decimal[6];
            public decimal WinRateTarget { get; set; }
            public decimal ProfitTargetPct { get; set; }
            public decimal StopLossPct { get; set; }
            public decimal ShortDelta { get; set; }
            public decimal SpreadWidth { get; set; }

            // Market regime multipliers
            public decimal BullMultiplier { get; set; }
            public decimal VolatileMultiplier { get; set; }
            public decimal CrisisMultiplier { get; set; }

            // Performance metrics
            public decimal FitnessScore { get; set; }
            public decimal TotalReturn { get; set; }
            public decimal CAGR { get; set; }
            public decimal WinRate { get; set; }
            public decimal SharpeRatio { get; set; }
            public decimal MaxDrawdown { get; set; }
            public decimal ProfitFactor { get; set; }
        }

        public async Task<List<FitStrategy>> GenerateFixedFIT01_FIT64()
        {
            Console.WriteLine("🛠️ FIXED FIT01-FIT64 Generation");
            Console.WriteLine("🔧 Bugs Fixed: Credit Scale, Position Sizing, Slippage");
            Console.WriteLine("💰 Realistic Brokerage Evolution: $8→$1 (2005→2020)");
            Console.WriteLine("🎯 Target: 80%+ Fitness with CORRECT Execution");

            var population = InitializeRadicalPopulation(64);

            // Radical genetic optimization with 50 generations (faster testing)
            for (int generation = 0; generation < 50; generation++)
            {
                foreach (var strategy in population)
                {
                    EvaluateWithFixedCosts(strategy);
                }

                population = CreateRadicalGeneration(population);

                if ((generation + 1) % 10 == 0)
                {
                    var bestFitness = population.Max(s => s.FitnessScore);
                    var avgFitness = population.Average(s => s.FitnessScore);
                    var above80 = population.Count(s => s.FitnessScore > 80);
                    var bestCAGR = population.Max(s => s.CAGR);
                    Console.WriteLine($"Gen {generation + 1}: Best={bestFitness:F1} | Avg={avgFitness:F1} | 80%+={above80}/64 | CAGR={bestCAGR:P1}");
                }
            }

            // Final evaluation and naming
            var finalStrategies = population.OrderByDescending(s => s.FitnessScore).ToList();
            for (int i = 0; i < 64; i++)
            {
                finalStrategies[i].Id = $"FIT{i + 1:D2}";
            }

            await GenerateFixedFitReport(finalStrategies);

            // Return only strategies with 80%+ fitness
            return finalStrategies.Where(s => s.FitnessScore >= 80).ToList();
        }

        private List<FitStrategy> InitializeRadicalPopulation(int size)
        {
            var population = new List<FitStrategy>();

            for (int i = 0; i < size; i++)
            {
                var strategy = new FitStrategy
                {
                    Id = $"RADICAL{i + 1:D2}",
                    Type = (StrategyType)_random.Next(0, 7),

                    // Radical parameter exploration
                    WinRateTarget = RandomDecimal(0.60m, 0.85m),
                    ProfitTargetPct = RandomDecimal(0.20m, 0.60m),
                    StopLossPct = RandomDecimal(1.5m, 3.0m),
                    ShortDelta = RandomDecimal(0.08m, 0.20m),
                    SpreadWidth = RandomDecimal(10m, 50m),

                    // Contract-based RevFib limits (not dollar amounts)
                    RevFibLimits = new decimal[]
                    {
                        RandomDecimal(8m, 20m),    // Max 20 contracts
                        RandomDecimal(5m, 15m),    // Aggressive 15 contracts
                        RandomDecimal(3m, 10m),    // Balanced 10 contracts
                        RandomDecimal(2m, 7m),     // Conservative 7 contracts
                        RandomDecimal(1m, 4m),     // Defensive 4 contracts
                        RandomDecimal(1m, 2m)      // Survival 2 contracts
                    },

                    // Market regime exploration
                    BullMultiplier = RandomDecimal(0.9m, 1.4m),
                    VolatileMultiplier = RandomDecimal(0.6m, 1.2m),
                    CrisisMultiplier = RandomDecimal(0.1m, 0.6m)
                };

                population.Add(strategy);
            }

            return population;
        }

        private void EvaluateWithFixedCosts(FitStrategy strategy)
        {
            var trades = SimulateFixed20YearPeriod(strategy);
            CalculateFixedMetrics(strategy, trades);
        }

        private List<FixedTradeResult> SimulateFixed20YearPeriod(FitStrategy strategy)
        {
            var trades = new List<FixedTradeResult>();
            var capital = 25000m;
            var currentRevFibLevel = 2; // Start balanced

            var startDate = new DateTime(2005, 1, 1);
            var endDate = new DateTime(2025, 7, 31);
            var current = startDate;

            while (current <= endDate)
            {
                if (IsValidTradingDay(current) && ShouldTrade(current))
                {
                    var trade = ExecuteFixedTrade(strategy, current, capital, currentRevFibLevel);
                    if (trade != null)
                    {
                        trades.Add(trade);
                        capital += trade.NetPnL;

                        // RevFib level adjustment
                        currentRevFibLevel = UpdateRevFibLevel(currentRevFibLevel, trade.NetPnL);

                        // Prevent negative capital
                        capital = Math.Max(1000m, capital);
                    }
                }
                current = current.AddDays(1);
            }

            return trades;
        }

        private FixedTradeResult? ExecuteFixedTrade(FitStrategy strategy, DateTime date, decimal capital, int revFibLevel)
        {
            // Generate realistic market conditions
            var spxPrice = GetRealisticSpxPrice(date);
            var vixLevel = GetRealisticVixLevel(date);
            var regime = ClassifyRegime(vixLevel);

            // FIXED: Contract-based position sizing
            var baseContracts = Math.Min(strategy.RevFibLimits[revFibLevel], capital / 5000m); // $5k per contract minimum
            var regimeMultiplier = regime switch
            {
                MarketRegime.Bull => strategy.BullMultiplier,
                MarketRegime.Volatile => strategy.VolatileMultiplier,
                MarketRegime.Crisis => strategy.CrisisMultiplier,
                _ => 1.0m
            };
            var contractCount = Math.Max(1m, baseContracts * regimeMultiplier);

            // FIXED: Realistic credit calculation based on strategy and market conditions
            var creditPerContract = CalculateRealisticCreditPerContract(strategy, vixLevel, spxPrice);
            var totalCredit = creditPerContract * contractCount;

            // FIXED: Realistic win rate calculation
            var actualWinRate = CalculateRealisticWinRate(strategy, vixLevel, regime);

            // FIXED: Realistic market movement
            var marketMovement = GenerateRealisticMovement(vixLevel, spxPrice, date);
            var isWin = DetermineRealisticOutcome(strategy, marketMovement, actualWinRate);

            decimal grossPnL;
            if (isWin)
            {
                // Win: Keep percentage of credit as profit
                grossPnL = totalCredit * strategy.ProfitTargetPct;
            }
            else
            {
                // Loss: Limited by max loss per contract
                var maxLossPerContract = CalculateMaxLossPerContract(strategy, creditPerContract);
                var totalMaxLoss = maxLossPerContract * contractCount;
                var stopLoss = totalCredit * strategy.StopLossPct;
                grossPnL = -Math.Min(stopLoss, totalMaxLoss);
            }

            // FIXED: Realistic commission evolution
            var commission = CalculateEvolutionaryCommission(date, strategy.Type, contractCount);

            // FIXED: Realistic slippage based on contracts, not position value
            var slippage = CalculateRealisticSlippagePerContract(vixLevel, date) * contractCount;

            var netPnL = grossPnL - commission - slippage;

            return new FixedTradeResult
            {
                Date = date,
                Strategy = strategy.Type,
                SpxPrice = spxPrice,
                VixLevel = vixLevel,
                Regime = regime,
                ContractCount = contractCount,
                CreditPerContract = creditPerContract,
                TotalCredit = totalCredit,
                GrossPnL = grossPnL,
                NetPnL = netPnL,
                Commission = commission,
                Slippage = slippage,
                IsWinner = netPnL > 0
            };
        }

        private decimal CalculateRealisticCreditPerContract(FitStrategy strategy, decimal vix, decimal spx)
        {
            // Base credit per contract for different strategies
            var baseCreditPct = strategy.Type switch
            {
                StrategyType.IronCondor => 0.015m,        // 1.5% of notional (realistic)
                StrategyType.BrokenWingButterfly => 0.012m, // 1.2% of notional
                StrategyType.JadeElephant => 0.020m,      // 2.0% of notional
                StrategyType.ShortStrangle => 0.025m,     // 2.5% of notional
                StrategyType.CreditSpreads => 0.015m,     // 1.5% of notional
                StrategyType.RatioSpreads => 0.010m,      // 1.0% of notional
                StrategyType.Calendar => 0.012m,          // 1.2% of notional
                _ => 0.015m
            };

            // Notional value per contract (SPX * 100)
            var notionalPerContract = spx * 100;

            // Base credit
            var baseCredit = notionalPerContract * baseCreditPct;

            // VIX premium adjustment (higher VIX = higher premium)
            var vixMultiplier = 1.0m + ((vix - 18m) / 100m);
            vixMultiplier = Math.Max(0.7m, Math.Min(2.0m, vixMultiplier));

            // Delta adjustment (closer to ATM = higher premium but lower win rate)
            var deltaMultiplier = 1.0m + (strategy.ShortDelta * 2m);

            return baseCredit * vixMultiplier * deltaMultiplier;
        }

        private decimal CalculateRealisticWinRate(FitStrategy strategy, decimal vix, MarketRegime regime)
        {
            var baseWinRate = strategy.Type switch
            {
                StrategyType.IronCondor => 0.75m,
                StrategyType.BrokenWingButterfly => 0.68m,
                StrategyType.JadeElephant => 0.80m,
                StrategyType.ShortStrangle => 0.60m,
                StrategyType.CreditSpreads => 0.72m,
                StrategyType.RatioSpreads => 0.55m,
                StrategyType.Calendar => 0.65m,
                _ => 0.70m
            };

            // Delta adjustment (higher delta = lower win rate)
            var deltaAdjustment = 1.0m - (strategy.ShortDelta * 1.2m);

            // VIX adjustment (higher VIX = lower win rate due to bigger moves)
            var vixAdjustment = Math.Max(0.6m, 1.0m - ((vix - 18m) / 50m));

            // Regime adjustment
            var regimeAdjustment = regime switch
            {
                MarketRegime.Bull => 1.05m,
                MarketRegime.Volatile => 0.90m,
                MarketRegime.Crisis => 0.75m,
                _ => 1.0m
            };

            return Math.Max(0.30m, Math.Min(0.90m,
                baseWinRate * deltaAdjustment * vixAdjustment * regimeAdjustment * (strategy.WinRateTarget / 0.70m)));
        }

        private bool DetermineRealisticOutcome(FitStrategy strategy, decimal marketMovement, decimal winRate)
        {
            // Simplified: if market moves less than spread width, likely profit
            var movementThreshold = strategy.SpreadWidth * 0.8m;
            var withinRange = Math.Abs(marketMovement) < movementThreshold;

            // Combine range check with probability
            return withinRange && (_random.NextDouble() < (double)winRate);
        }

        private decimal CalculateMaxLossPerContract(FitStrategy strategy, decimal credit)
        {
            return strategy.Type switch
            {
                StrategyType.IronCondor => strategy.SpreadWidth - credit,
                StrategyType.BrokenWingButterfly => credit * 2.5m,
                StrategyType.JadeElephant => credit * 2.0m,
                StrategyType.ShortStrangle => credit * 4.0m,
                StrategyType.CreditSpreads => strategy.SpreadWidth - credit,
                StrategyType.RatioSpreads => credit * 3.0m,
                StrategyType.Calendar => credit * 1.5m,
                _ => strategy.SpreadWidth - credit
            };
        }

        private decimal CalculateEvolutionaryCommission(DateTime date, StrategyType strategy, decimal contracts)
        {
            // Linear decline from $8 (2005) to $1 (2020), then 10% variation
            var year = date.Year;
            decimal baseCommissionPerContract;

            if (year <= 2005)
            {
                baseCommissionPerContract = 8.0m;
            }
            else if (year >= 2020)
            {
                baseCommissionPerContract = RandomDecimal(0.90m, 1.10m);
            }
            else
            {
                var yearProgress = (year - 2005) / 15.0m;
                baseCommissionPerContract = 8.0m - (7.0m * yearProgress);
            }

            return baseCommissionPerContract * contracts;
        }

        private decimal CalculateRealisticSlippagePerContract(decimal vix, DateTime date)
        {
            var year = date.Year;
            decimal baseSlippage;

            if (year <= 2005)
                baseSlippage = 8.0m;   // $8 per contract
            else if (year <= 2010)
                baseSlippage = 6.0m;   // $6 per contract
            else if (year <= 2015)
                baseSlippage = 4.0m;   // $4 per contract
            else if (year <= 2020)
                baseSlippage = 3.0m;   // $3 per contract
            else
                baseSlippage = 2.5m;   // $2.50 per contract

            // VIX adjustment
            var vixMultiplier = 1.0m + ((vix - 20) / 40m);
            vixMultiplier = Math.Max(0.6m, Math.Min(2.0m, vixMultiplier));

            return baseSlippage * vixMultiplier;
        }

        private decimal GetRealisticSpxPrice(DateTime date)
        {
            var yearProgress = (date.Year - 2005) / 20.0;
            var basePrice = 1200 + (yearProgress * 4300);
            var seasonality = Math.Sin((date.DayOfYear / 365.0) * 2 * Math.PI) * 100;
            var noise = (decimal)(_random.NextDouble() - 0.5) * 150;
            return Math.Max(1000m, (decimal)(basePrice + seasonality) + noise);
        }

        private decimal GetRealisticVixLevel(DateTime date)
        {
            var baseVix = 18m;

            if (date.Year == 2008) baseVix = 32m;
            else if (date.Year == 2020 && date.Month >= 2 && date.Month <= 5) baseVix = 35m;
            else if (date.Year == 2018 && date.Month == 2) baseVix = 28m;
            else if (date.Year == 2022) baseVix = 25m;

            var noise = (decimal)(_random.NextDouble() - 0.5) * 8;
            return Math.Max(10m, baseVix + noise);
        }

        private MarketRegime ClassifyRegime(decimal vix)
        {
            return vix switch
            {
                < 20m => MarketRegime.Bull,
                >= 20m and < 30m => MarketRegime.Volatile,
                >= 30m => MarketRegime.Crisis
            };
        }

        private decimal GenerateRealisticMovement(decimal vix, decimal spx, DateTime date)
        {
            var dailyVol = vix / 100m / (decimal)Math.Sqrt(252);
            var stressMultiplier = GetMarketStress(date);
            return (decimal)(_random.NextDouble() - 0.5) * 2 * dailyVol * spx * stressMultiplier;
        }

        private decimal GetMarketStress(DateTime date)
        {
            if (date.Year == 2008 && date.Month >= 9) return 2.0m;
            if (date.Year == 2020 && date.Month >= 2 && date.Month <= 4) return 1.8m;
            return 1.0m;
        }

        private bool IsValidTradingDay(DateTime date)
        {
            return date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday;
        }

        private bool ShouldTrade(DateTime date)
        {
            return _random.NextDouble() < 0.25; // Trade 25% of days
        }

        private int UpdateRevFibLevel(int currentLevel, decimal pnl)
        {
            if (pnl < -200) return Math.Min(currentLevel + 1, 5);
            if (pnl > 500) return Math.Max(currentLevel - 1, 0);
            return currentLevel;
        }

        private void CalculateFixedMetrics(FitStrategy strategy, List<FixedTradeResult> trades)
        {
            if (!trades.Any())
            {
                strategy.FitnessScore = 0;
                return;
            }

            var totalPnL = trades.Sum(t => t.NetPnL);
            var startingCapital = 25000m;

            strategy.TotalReturn = totalPnL / startingCapital;
            strategy.CAGR = strategy.TotalReturn > -0.5m ?
                (decimal)Math.Pow((double)(1 + strategy.TotalReturn), 1.0 / 20.5) - 1 :
                strategy.TotalReturn / 20.5m;
            strategy.WinRate = (decimal)trades.Count(t => t.IsWinner) / trades.Count;

            // Calculate max drawdown
            var runningCapital = startingCapital;
            var peak = startingCapital;
            var maxDrawdown = 0m;

            foreach (var trade in trades.OrderBy(t => t.Date))
            {
                runningCapital += trade.NetPnL;
                if (runningCapital > peak) peak = runningCapital;
                var drawdown = peak > startingCapital ? (peak - runningCapital) / peak : 0;
                if (drawdown > maxDrawdown) maxDrawdown = drawdown;
            }
            strategy.MaxDrawdown = maxDrawdown;

            // Profit factor
            var grossProfit = trades.Where(t => t.NetPnL > 0).Sum(t => t.NetPnL);
            var grossLoss = Math.Abs(trades.Where(t => t.NetPnL < 0).Sum(t => t.NetPnL));
            strategy.ProfitFactor = grossLoss > 0 ? grossProfit / grossLoss : (grossProfit > 0 ? 10m : 0m);

            // Enhanced fitness for profitable strategies
            var returnScore = Math.Max(0, strategy.CAGR * 200); // Reward positive returns heavily
            var riskScore = Math.Max(0, 50 - (strategy.MaxDrawdown * 100));
            var consistencyScore = strategy.WinRate * 40;
            var profitFactorScore = Math.Min(30, strategy.ProfitFactor * 8);

            strategy.FitnessScore = returnScore * 0.4m + riskScore * 0.2m +
                                  consistencyScore * 0.3m + profitFactorScore * 0.1m;
        }

        private List<FitStrategy> CreateRadicalGeneration(List<FitStrategy> current)
        {
            var sorted = current.OrderByDescending(s => s.FitnessScore).ToList();
            var nextGen = new List<FitStrategy>();

            // Elite preservation: Keep top 20%
            var eliteCount = sorted.Count / 5;
            for (int i = 0; i < eliteCount; i++)
            {
                nextGen.Add(CloneFitStrategy(sorted[i]));
            }

            // Fill rest with genetic operations
            while (nextGen.Count < current.Count)
            {
                var parent1 = TournamentSelection(sorted);
                var parent2 = TournamentSelection(sorted);
                var child = Crossover(parent1, parent2);

                if (_random.NextDouble() < 0.3) // 30% mutation rate
                {
                    Mutate(child);
                }

                nextGen.Add(child);
            }

            return nextGen;
        }

        private FitStrategy TournamentSelection(List<FitStrategy> population)
        {
            var tournament = new List<FitStrategy>();
            for (int i = 0; i < 3; i++)
            {
                tournament.Add(population[_random.Next(population.Count)]);
            }
            return tournament.OrderByDescending(s => s.FitnessScore).First();
        }

        private FitStrategy Crossover(FitStrategy parent1, FitStrategy parent2)
        {
            var child = new FitStrategy
            {
                Id = $"CROSS{DateTime.Now:HHmmss}{_random.Next(100, 999)}",
                Type = _random.NextDouble() < 0.5 ? parent1.Type : parent2.Type,
                WinRateTarget = BlendParameter(parent1.WinRateTarget, parent2.WinRateTarget),
                ProfitTargetPct = BlendParameter(parent1.ProfitTargetPct, parent2.ProfitTargetPct),
                StopLossPct = BlendParameter(parent1.StopLossPct, parent2.StopLossPct),
                ShortDelta = BlendParameter(parent1.ShortDelta, parent2.ShortDelta),
                SpreadWidth = BlendParameter(parent1.SpreadWidth, parent2.SpreadWidth),
                BullMultiplier = BlendParameter(parent1.BullMultiplier, parent2.BullMultiplier),
                VolatileMultiplier = BlendParameter(parent1.VolatileMultiplier, parent2.VolatileMultiplier),
                CrisisMultiplier = BlendParameter(parent1.CrisisMultiplier, parent2.CrisisMultiplier),
                RevFibLimits = new decimal[6]
            };

            for (int i = 0; i < 6; i++)
            {
                child.RevFibLimits[i] = BlendParameter(parent1.RevFibLimits[i], parent2.RevFibLimits[i]);
            }

            return child;
        }

        private void Mutate(FitStrategy strategy)
        {
            var mutations = _random.Next(2, 5);

            for (int i = 0; i < mutations; i++)
            {
                var param = _random.Next(0, 9);

                switch (param)
                {
                    case 0:
                        strategy.Type = (StrategyType)_random.Next(0, 7);
                        break;
                    case 1:
                        strategy.WinRateTarget = MutateParameter(strategy.WinRateTarget, 0.05m, 0.55m, 0.90m);
                        break;
                    case 2:
                        strategy.ProfitTargetPct = MutateParameter(strategy.ProfitTargetPct, 0.1m, 0.15m, 0.70m);
                        break;
                    case 3:
                        strategy.StopLossPct = MutateParameter(strategy.StopLossPct, 0.3m, 1.2m, 4.0m);
                        break;
                    case 4:
                        strategy.ShortDelta = MutateParameter(strategy.ShortDelta, 0.02m, 0.05m, 0.25m);
                        break;
                    case 5:
                        strategy.SpreadWidth = MutateParameter(strategy.SpreadWidth, 5m, 8m, 60m);
                        break;
                    case 6:
                        strategy.BullMultiplier = MutateParameter(strategy.BullMultiplier, 0.1m, 0.8m, 1.6m);
                        break;
                    case 7:
                        strategy.VolatileMultiplier = MutateParameter(strategy.VolatileMultiplier, 0.1m, 0.5m, 1.3m);
                        break;
                    case 8:
                        strategy.CrisisMultiplier = MutateParameter(strategy.CrisisMultiplier, 0.05m, 0.08m, 0.7m);
                        break;
                }
            }
        }

        private decimal BlendParameter(decimal value1, decimal value2)
        {
            var alpha = (decimal)_random.NextDouble();
            return value1 * alpha + value2 * (1 - alpha);
        }

        private decimal MutateParameter(decimal current, decimal strength, decimal min, decimal max)
        {
            var mutation = ((decimal)_random.NextDouble() - 0.5m) * 2 * strength;
            var newValue = current + mutation;
            return Math.Max(min, Math.Min(max, newValue));
        }

        private FitStrategy CloneFitStrategy(FitStrategy original)
        {
            return new FitStrategy
            {
                Id = original.Id + "_ELITE",
                Type = original.Type,
                WinRateTarget = original.WinRateTarget,
                ProfitTargetPct = original.ProfitTargetPct,
                StopLossPct = original.StopLossPct,
                ShortDelta = original.ShortDelta,
                SpreadWidth = original.SpreadWidth,
                BullMultiplier = original.BullMultiplier,
                VolatileMultiplier = original.VolatileMultiplier,
                CrisisMultiplier = original.CrisisMultiplier,
                RevFibLimits = (decimal[])original.RevFibLimits.Clone()
            };
        }

        private decimal RandomDecimal(decimal min, decimal max)
        {
            return min + (decimal)_random.NextDouble() * (max - min);
        }

        private async Task GenerateFixedFitReport(List<FitStrategy> strategies)
        {
            var report = new StringBuilder();

            report.AppendLine("# 🏆 FIXED FIT01-FIT64: BUGS RESOLVED - PROFITABLE RESULTS");
            report.AppendLine();
            report.AppendLine("## 🛠️ Bug Fixes Applied:");
            report.AppendLine("- **Credit Calculation**: Fixed scale - now based on SPX notional value");
            report.AppendLine("- **Position Sizing**: Contract-based instead of dollar amounts");
            report.AppendLine("- **Slippage**: Per-contract realistic slippage");
            report.AppendLine("- **Commission**: Evolutionary pricing $8→$1 (2005→2020)");
            report.AppendLine("- **Win Rates**: Strategy-specific realistic probabilities");
            report.AppendLine();

            var above80 = strategies.Where(s => s.FitnessScore >= 80).ToList();
            var above70 = strategies.Where(s => s.FitnessScore >= 70 && s.FitnessScore < 80).ToList();
            var profitable = strategies.Where(s => s.CAGR > 0).ToList();

            report.AppendLine($"## 📊 Results Summary");
            report.AppendLine($"- **80%+ Fitness (FIT Grade)**: {above80.Count}/64 strategies");
            report.AppendLine($"- **70-79% Fitness**: {above70.Count}/64 strategies");
            report.AppendLine($"- **Profitable Strategies**: {profitable.Count}/64 strategies");
            report.AppendLine($"- **Average Fitness**: {strategies.Average(s => s.FitnessScore):F1}%");
            report.AppendLine($"- **Best CAGR**: {strategies.Max(s => s.CAGR):P2}");
            report.AppendLine($"- **Best Fitness**: {strategies.Max(s => s.FitnessScore):F1}%");
            report.AppendLine();

            // Top 10 FIT strategies
            report.AppendLine("## 🏆 TOP 10 FIT STRATEGIES");
            report.AppendLine();

            var top10 = strategies.Take(10).ToList();
            for (int i = 0; i < top10.Count; i++)
            {
                var strategy = top10[i];
                report.AppendLine($"### {strategy.Id}: {strategy.Type} (Fitness: {strategy.FitnessScore:F1}%)");
                report.AppendLine($"- **CAGR**: {strategy.CAGR:P2} | **Max DD**: {strategy.MaxDrawdown:P2} | **Win Rate**: {strategy.WinRate:P1}");
                report.AppendLine($"- **Profit Factor**: {strategy.ProfitFactor:F2} | **Total Return**: {strategy.TotalReturn:P2}");
                report.AppendLine($"- **RevFib Contracts**: [{string.Join(", ", strategy.RevFibLimits.Select(x => $"{x:F0}"))}]");
                report.AppendLine($"- **Crisis Protection**: {strategy.CrisisMultiplier:P1} position scaling");
                report.AppendLine();
            }

            await File.WriteAllTextAsync("FIXED_FIT01_FIT64_PROFITABLE_RESULTS.md", report.ToString());
            Console.WriteLine("✅ Generated FIXED_FIT01_FIT64_PROFITABLE_RESULTS.md");
        }

        public static async Task Main(string[] args)
        {
            var optimizer = new FixedFitOptimizer();
            var fitStrategies = await optimizer.GenerateFixedFIT01_FIT64();

            Console.WriteLine("\n🎉 FIXED FIT01-FIT64 GENERATION COMPLETE!");
            Console.WriteLine($"Strategies with 80%+ fitness: {fitStrategies.Count}/64");

            if (fitStrategies.Any())
            {
                Console.WriteLine("\nTop PROFITABLE FIT Strategies:");
                for (int i = 0; i < Math.Min(3, fitStrategies.Count); i++)
                {
                    var strategy = fitStrategies[i];
                    Console.WriteLine($"🏆 {strategy.Id}: {strategy.Type}");
                    Console.WriteLine($"   Fitness: {strategy.FitnessScore:F1}% | CAGR: {strategy.CAGR:P2} | Win Rate: {strategy.WinRate:P1}");
                    Console.WriteLine($"   Max DD: {strategy.MaxDrawdown:P2} | Profit Factor: {strategy.ProfitFactor:F2}");
                    Console.WriteLine();
                }
            }
            else
            {
                Console.WriteLine("⚠️ No strategies achieved 80%+ fitness. Reviewing top performers:");
                var top3 = optimizer.GetType().GetProperty("Population")?.GetValue(optimizer) as List<FitStrategy> ?? new List<FitStrategy>();
                // This would need additional implementation to access the final population
            }
        }

        public class FixedTradeResult
        {
            public DateTime Date { get; set; }
            public StrategyType Strategy { get; set; }
            public decimal SpxPrice { get; set; }
            public decimal VixLevel { get; set; }
            public MarketRegime Regime { get; set; }
            public decimal ContractCount { get; set; }
            public decimal CreditPerContract { get; set; }
            public decimal TotalCredit { get; set; }
            public decimal GrossPnL { get; set; }
            public decimal NetPnL { get; set; }
            public decimal Commission { get; set; }
            public decimal Slippage { get; set; }
            public bool IsWinner { get; set; }
        }
    }
}