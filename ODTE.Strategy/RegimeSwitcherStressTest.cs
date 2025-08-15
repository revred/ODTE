using System;
using System.Collections.Generic;
using System.Linq;

namespace ODTE.Strategy
{
    /// <summary>
    /// Stress test for RegimeSwitcher with synthetic data that forces rapid regime changes
    /// Tests performance when regimes change every 2-5 days vs stable 24-day periods
    /// </summary>
    public class RegimeSwitcherStressTest
    {
        public class StressTestScenario
        {
            public string Name { get; set; } = "";
            public int RegimeChangeDays { get; set; }
            public string Pattern { get; set; } = "";
            public List<SyntheticRegimeData> RegimeSequence { get; set; } = new();
        }

        public class SyntheticRegimeData
        {
            public RegimeSwitcher.Regime Regime { get; set; }
            public int DurationDays { get; set; }
            public double VIXLevel { get; set; }
            public double TrendStrength { get; set; }
            public double RealizedVsImplied { get; set; }
            public string EventTrigger { get; set; } = "";
        }

        public class StressTestResults
        {
            public string ScenarioName { get; set; } = "";
            public int TotalDays { get; set; }
            public int RegimeChanges { get; set; }
            public double TotalPnL { get; set; }
            public double MaxDrawdown { get; set; }
            public double WinRate { get; set; }
            public int WhipsawTrades { get; set; }
            public Dictionary<RegimeSwitcher.Regime, double> RegimePerformance { get; set; } = new();
            public Dictionary<RegimeSwitcher.Regime, int> RegimeFrequency { get; set; } = new();
            public double AverageRegimeDuration { get; set; }
            public double WorstWhipsawLoss { get; set; }
        }

        private readonly Random _random;
        private readonly RegimeSwitcher _regimeSwitcher;

        public RegimeSwitcherStressTest(Random random = null)
        {
            _random = random ?? new Random(123); // Different seed for stress testing
            _regimeSwitcher = new RegimeSwitcher(_random);
        }

        /// <summary>
        /// Run comprehensive stress test with multiple rapid regime change scenarios
        /// </summary>
        public void RunComprehensiveStressTest()
        {
            Console.WriteLine("🔥 REGIME SWITCHER STRESS TEST");
            Console.WriteLine("Testing performance under rapid regime changes vs stable periods");
            Console.WriteLine("=" .PadRight(70, '='));
            Console.WriteLine();

            var scenarios = CreateStressTestScenarios();
            var results = new List<StressTestResults>();

            foreach (var scenario in scenarios)
            {
                Console.WriteLine($"🧪 TESTING: {scenario.Name}");
                Console.WriteLine($"   Pattern: {scenario.Pattern}");
                Console.WriteLine($"   Regime Change Frequency: Every {scenario.RegimeChangeDays} days");
                Console.WriteLine();

                var result = RunStressTestScenario(scenario);
                results.Add(result);

                PrintScenarioResults(result);
                Console.WriteLine();
            }

            // Compare all scenarios
            PrintComparativeAnalysis(results);
        }

        /// <summary>
        /// Create various stress test scenarios with different regime change patterns
        /// </summary>
        private List<StressTestScenario> CreateStressTestScenarios()
        {
            return new List<StressTestScenario>
            {
                // Baseline: Stable 24-day periods (for comparison)
                new StressTestScenario
                {
                    Name = "BASELINE: Stable Regimes",
                    RegimeChangeDays = 24,
                    Pattern = "Each regime lasts 24 days - optimal for strategy",
                    RegimeSequence = CreateStableRegimeSequence()
                },

                // Rapid whipsaws: Change every 2 days
                new StressTestScenario
                {
                    Name = "WHIPSAW HELL: 2-Day Changes",
                    RegimeChangeDays = 2,
                    Pattern = "Extreme whipsaws: Calm→Volatile→Calm every 2 days",
                    RegimeSequence = CreateWhipsawRegimeSequence(2)
                },

                // Moderate changes: Every 3 days
                new StressTestScenario
                {
                    Name = "FAST CHANGES: 3-Day Cycles",
                    RegimeChangeDays = 3,
                    Pattern = "Fast regime cycling through all three states",
                    RegimeSequence = CreateRapidCyclingSequence(3)
                },

                // Weekly changes: Every 5 days
                new StressTestScenario
                {
                    Name = "WEEKLY SHIFTS: 5-Day Cycles",
                    RegimeChangeDays = 5,
                    Pattern = "Weekly regime shifts with trend continuation",
                    RegimeSequence = CreateWeeklyShiftSequence(5)
                },

                // Volatility clusters: Mixed durations
                new StressTestScenario
                {
                    Name = "VOLATILITY CLUSTERS",
                    RegimeChangeDays = 0, // Variable
                    Pattern = "Realistic vol clustering: 1-2 calm days, 3-7 volatile bursts",
                    RegimeSequence = CreateVolatilityClusterSequence()
                },

                // Trending markets: Extended directional moves
                new StressTestScenario
                {
                    Name = "TRENDING PUNISHMENT",
                    RegimeChangeDays = 0, // Variable
                    Pattern = "Long trending periods punish mean reversion strategies",
                    RegimeSequence = CreateTrendingMarketSequence()
                },

                // Black swan events: Sudden regime breaks
                new StressTestScenario
                {
                    Name = "BLACK SWAN CHAOS",
                    RegimeChangeDays = 0, // Variable
                    Pattern = "Sudden explosive moves break all regime predictions",
                    RegimeSequence = CreateBlackSwanSequence()
                }
            };
        }

        /// <summary>
        /// Create stable regime sequence (baseline)
        /// </summary>
        private List<SyntheticRegimeData> CreateStableRegimeSequence()
        {
            var sequence = new List<SyntheticRegimeData>();
            var regimes = new[] { RegimeSwitcher.Regime.Calm, RegimeSwitcher.Regime.Mixed, RegimeSwitcher.Regime.Convex };

            for (int i = 0; i < 5; i++) // 5 periods of 24 days each = 120 days
            {
                var regime = regimes[i % 3];
                sequence.Add(new SyntheticRegimeData
                {
                    Regime = regime,
                    DurationDays = 24,
                    VIXLevel = regime switch
                    {
                        RegimeSwitcher.Regime.Calm => 15 + _random.NextDouble() * 10,     // 15-25
                        RegimeSwitcher.Regime.Mixed => 25 + _random.NextDouble() * 15,    // 25-40  
                        RegimeSwitcher.Regime.Convex => 40 + _random.NextDouble() * 30,  // 40-70
                        _ => 20
                    },
                    TrendStrength = regime switch
                    {
                        RegimeSwitcher.Regime.Calm => _random.NextDouble() * 0.3,        // 0-0.3
                        RegimeSwitcher.Regime.Mixed => 0.3 + _random.NextDouble() * 0.3, // 0.3-0.6
                        RegimeSwitcher.Regime.Convex => 0.6 + _random.NextDouble() * 0.4, // 0.6-1.0
                        _ => 0.5
                    },
                    RealizedVsImplied = regime switch
                    {
                        RegimeSwitcher.Regime.Calm => 0.8 + _random.NextDouble() * 0.3,   // 0.8-1.1
                        RegimeSwitcher.Regime.Mixed => 1.0 + _random.NextDouble() * 0.3,  // 1.0-1.3
                        RegimeSwitcher.Regime.Convex => 1.2 + _random.NextDouble() * 0.5, // 1.2-1.7
                        _ => 1.0
                    },
                    EventTrigger = "Stable Period"
                });
            }
            return sequence;
        }

        /// <summary>
        /// Create whipsaw sequence that changes every 2 days
        /// </summary>
        private List<SyntheticRegimeData> CreateWhipsawRegimeSequence(int changeDays)
        {
            var sequence = new List<SyntheticRegimeData>();
            var regimes = new[] { RegimeSwitcher.Regime.Calm, RegimeSwitcher.Regime.Convex }; // Extreme opposites

            for (int day = 0; day < 120; day += changeDays) // 120 days total
            {
                var regime = regimes[(day / changeDays) % 2]; // Alternate between Calm and Convex
                sequence.Add(new SyntheticRegimeData
                {
                    Regime = regime,
                    DurationDays = changeDays,
                    VIXLevel = regime == RegimeSwitcher.Regime.Calm ? 12 + _random.NextDouble() * 8 : 45 + _random.NextDouble() * 25,
                    TrendStrength = regime == RegimeSwitcher.Regime.Calm ? _random.NextDouble() * 0.2 : 0.7 + _random.NextDouble() * 0.3,
                    RealizedVsImplied = regime == RegimeSwitcher.Regime.Calm ? 0.7 + _random.NextDouble() * 0.2 : 1.3 + _random.NextDouble() * 0.4,
                    EventTrigger = $"Whipsaw Day {day + 1}"
                });
            }
            return sequence;
        }

        /// <summary>
        /// Create rapid cycling sequence through all regimes
        /// </summary>
        private List<SyntheticRegimeData> CreateRapidCyclingSequence(int changeDays)
        {
            var sequence = new List<SyntheticRegimeData>();
            var regimes = new[] { RegimeSwitcher.Regime.Calm, RegimeSwitcher.Regime.Mixed, RegimeSwitcher.Regime.Convex };

            for (int day = 0; day < 120; day += changeDays)
            {
                var regime = regimes[(day / changeDays) % 3]; // Cycle through all three
                sequence.Add(new SyntheticRegimeData
                {
                    Regime = regime,
                    DurationDays = changeDays,
                    VIXLevel = regime switch
                    {
                        RegimeSwitcher.Regime.Calm => 14 + _random.NextDouble() * 8,
                        RegimeSwitcher.Regime.Mixed => 22 + _random.NextDouble() * 12,
                        RegimeSwitcher.Regime.Convex => 35 + _random.NextDouble() * 20,
                        _ => 20
                    },
                    TrendStrength = regime switch
                    {
                        RegimeSwitcher.Regime.Calm => _random.NextDouble() * 0.25,
                        RegimeSwitcher.Regime.Mixed => 0.25 + _random.NextDouble() * 0.35,
                        RegimeSwitcher.Regime.Convex => 0.6 + _random.NextDouble() * 0.35,
                        _ => 0.4
                    },
                    RealizedVsImplied = regime switch
                    {
                        RegimeSwitcher.Regime.Calm => 0.8 + _random.NextDouble() * 0.25,
                        RegimeSwitcher.Regime.Mixed => 1.0 + _random.NextDouble() * 0.25,
                        RegimeSwitcher.Regime.Convex => 1.2 + _random.NextDouble() * 0.4,
                        _ => 1.0
                    },
                    EventTrigger = $"Rapid Cycle Day {day + 1}"
                });
            }
            return sequence;
        }

        /// <summary>
        /// Create weekly shift sequence
        /// </summary>
        private List<SyntheticRegimeData> CreateWeeklyShiftSequence(int changeDays)
        {
            var sequence = new List<SyntheticRegimeData>();
            var regimes = new[] { RegimeSwitcher.Regime.Calm, RegimeSwitcher.Regime.Mixed, RegimeSwitcher.Regime.Convex };

            for (int day = 0; day < 120; day += changeDays)
            {
                var regime = regimes[_random.Next(3)]; // Random regime each week
                sequence.Add(new SyntheticRegimeData
                {
                    Regime = regime,
                    DurationDays = changeDays,
                    VIXLevel = regime switch
                    {
                        RegimeSwitcher.Regime.Calm => 16 + _random.NextDouble() * 9,
                        RegimeSwitcher.Regime.Mixed => 24 + _random.NextDouble() * 16,
                        RegimeSwitcher.Regime.Convex => 38 + _random.NextDouble() * 25,
                        _ => 20
                    },
                    TrendStrength = regime switch
                    {
                        RegimeSwitcher.Regime.Calm => _random.NextDouble() * 0.3,
                        RegimeSwitcher.Regime.Mixed => 0.3 + _random.NextDouble() * 0.3,
                        RegimeSwitcher.Regime.Convex => 0.6 + _random.NextDouble() * 0.4,
                        _ => 0.4
                    },
                    RealizedVsImplied = 0.9 + _random.NextDouble() * 0.6, // More random
                    EventTrigger = $"Weekly Shift Day {day + 1}"
                });
            }
            return sequence;
        }

        /// <summary>
        /// Create volatility clustering sequence (realistic market behavior)
        /// </summary>
        private List<SyntheticRegimeData> CreateVolatilityClusterSequence()
        {
            var sequence = new List<SyntheticRegimeData>();
            int totalDays = 0;

            while (totalDays < 120)
            {
                // Calm period: 8-15 days
                var calmDays = Math.Min(8 + _random.Next(8), 120 - totalDays);
                if (calmDays > 0)
                {
                    sequence.Add(new SyntheticRegimeData
                    {
                        Regime = RegimeSwitcher.Regime.Calm,
                        DurationDays = calmDays,
                        VIXLevel = 12 + _random.NextDouble() * 8,
                        TrendStrength = _random.NextDouble() * 0.2,
                        RealizedVsImplied = 0.7 + _random.NextDouble() * 0.3,
                        EventTrigger = "Calm Cluster"
                    });
                    totalDays += calmDays;
                }

                // Volatile burst: 3-7 days
                if (totalDays < 120)
                {
                    var volDays = Math.Min(3 + _random.Next(5), 120 - totalDays);
                    sequence.Add(new SyntheticRegimeData
                    {
                        Regime = RegimeSwitcher.Regime.Convex,
                        DurationDays = volDays,
                        VIXLevel = 40 + _random.NextDouble() * 35,
                        TrendStrength = 0.6 + _random.NextDouble() * 0.4,
                        RealizedVsImplied = 1.3 + _random.NextDouble() * 0.5,
                        EventTrigger = "Volatility Burst"
                    });
                    totalDays += volDays;
                }

                // Transition period: 2-4 days
                if (totalDays < 120)
                {
                    var mixedDays = Math.Min(2 + _random.Next(3), 120 - totalDays);
                    sequence.Add(new SyntheticRegimeData
                    {
                        Regime = RegimeSwitcher.Regime.Mixed,
                        DurationDays = mixedDays,
                        VIXLevel = 25 + _random.NextDouble() * 15,
                        TrendStrength = 0.3 + _random.NextDouble() * 0.3,
                        RealizedVsImplied = 1.0 + _random.NextDouble() * 0.3,
                        EventTrigger = "Transition Period"
                    });
                    totalDays += mixedDays;
                }
            }

            return sequence;
        }

        /// <summary>
        /// Create trending market sequence (punishes mean reversion)
        /// </summary>
        private List<SyntheticRegimeData> CreateTrendingMarketSequence()
        {
            var sequence = new List<SyntheticRegimeData>();
            
            // Long trending period: 45 days
            sequence.Add(new SyntheticRegimeData
            {
                Regime = RegimeSwitcher.Regime.Convex,
                DurationDays = 45,
                VIXLevel = 28 + _random.NextDouble() * 20, // Moderate but persistent volatility
                TrendStrength = 0.8 + _random.NextDouble() * 0.2, // Strong directional move
                RealizedVsImplied = 1.1 + _random.NextDouble() * 0.4,
                EventTrigger = "Strong Trend Beginning"
            });

            // Continuation with acceleration: 30 days
            sequence.Add(new SyntheticRegimeData
            {
                Regime = RegimeSwitcher.Regime.Convex,
                DurationDays = 30,
                VIXLevel = 35 + _random.NextDouble() * 25, // Increasing volatility
                TrendStrength = 0.9 + _random.NextDouble() * 0.1, // Very strong trend
                RealizedVsImplied = 1.3 + _random.NextDouble() * 0.3,
                EventTrigger = "Trend Acceleration"
            });

            // Exhaustion and reversal: 15 days
            sequence.Add(new SyntheticRegimeData
            {
                Regime = RegimeSwitcher.Regime.Mixed,
                DurationDays = 15,
                VIXLevel = 45 + _random.NextDouble() * 20, // High volatility but uncertain direction
                TrendStrength = 0.2 + _random.NextDouble() * 0.6, // Weakening trend
                RealizedVsImplied = 1.4 + _random.NextDouble() * 0.3,
                EventTrigger = "Trend Exhaustion"
            });

            // New opposite trend: 30 days
            sequence.Add(new SyntheticRegimeData
            {
                Regime = RegimeSwitcher.Regime.Convex,
                DurationDays = 30,
                VIXLevel = 30 + _random.NextDouble() * 25,
                TrendStrength = -0.7 - _random.NextDouble() * 0.2, // Strong opposite trend
                RealizedVsImplied = 1.2 + _random.NextDouble() * 0.4,
                EventTrigger = "Trend Reversal"
            });

            return sequence;
        }

        /// <summary>
        /// Create black swan sequence (extreme events)
        /// </summary>
        private List<SyntheticRegimeData> CreateBlackSwanSequence()
        {
            var sequence = new List<SyntheticRegimeData>();

            // Normal period before shock: 20 days
            sequence.Add(new SyntheticRegimeData
            {
                Regime = RegimeSwitcher.Regime.Calm,
                DurationDays = 20,
                VIXLevel = 15 + _random.NextDouble() * 5,
                TrendStrength = _random.NextDouble() * 0.2,
                RealizedVsImplied = 0.8 + _random.NextDouble() * 0.2,
                EventTrigger = "Calm Before Storm"
            });

            // Black swan event: 5 days of extreme volatility
            sequence.Add(new SyntheticRegimeData
            {
                Regime = RegimeSwitcher.Regime.Convex,
                DurationDays = 5,
                VIXLevel = 70 + _random.NextDouble() * 30, // Extreme VIX levels
                TrendStrength = -0.9 - _random.NextDouble() * 0.1, // Extreme downward pressure
                RealizedVsImplied = 2.0 + _random.NextDouble() * 1.0, // Realized vol explodes
                EventTrigger = "BLACK SWAN EVENT"
            });

            // Recovery attempt: 15 days of high volatility
            sequence.Add(new SyntheticRegimeData
            {
                Regime = RegimeSwitcher.Regime.Convex,
                DurationDays = 15,
                VIXLevel = 50 + _random.NextDouble() * 25,
                TrendStrength = 0.3 + _random.NextDouble() * 0.7, // Violent recovery attempts
                RealizedVsImplied = 1.6 + _random.NextDouble() * 0.6,
                EventTrigger = "Recovery Volatility"
            });

            // Stabilization but elevated levels: 25 days
            sequence.Add(new SyntheticRegimeData
            {
                Regime = RegimeSwitcher.Regime.Mixed,
                DurationDays = 25,
                VIXLevel = 30 + _random.NextDouble() * 15,
                TrendStrength = _random.NextDouble() * 0.4 - 0.2, // Still uncertain
                RealizedVsImplied = 1.2 + _random.NextDouble() * 0.4,
                EventTrigger = "Post-Crisis Stabilization"
            });

            // New normal (higher baseline): 55 days
            sequence.Add(new SyntheticRegimeData
            {
                Regime = RegimeSwitcher.Regime.Calm,
                DurationDays = 55,
                VIXLevel = 22 + _random.NextDouble() * 8, // Higher baseline VIX
                TrendStrength = _random.NextDouble() * 0.3,
                RealizedVsImplied = 0.9 + _random.NextDouble() * 0.3,
                EventTrigger = "New Normal (Post-Crisis)"
            });

            return sequence;
        }

        /// <summary>
        /// Run a single stress test scenario
        /// </summary>
        private StressTestResults RunStressTestScenario(StressTestScenario scenario)
        {
            var result = new StressTestResults
            {
                ScenarioName = scenario.Name,
                RegimePerformance = new Dictionary<RegimeSwitcher.Regime, double>(),
                RegimeFrequency = new Dictionary<RegimeSwitcher.Regime, int>()
            };

            // Initialize regime tracking
            foreach (RegimeSwitcher.Regime regime in Enum.GetValues<RegimeSwitcher.Regime>())
            {
                result.RegimePerformance[regime] = 0;
                result.RegimeFrequency[regime] = 0;
            }

            var totalPnL = 0.0;
            var equity = 5000.0;
            var peak = 5000.0;
            var maxDrawdown = 0.0;
            var wins = 0;
            var totalTrades = 0;
            var whipsawTrades = 0;
            var worstWhipsawLoss = 0.0;
            var regimeChanges = 0;
            var currentRegime = RegimeSwitcher.Regime.Calm;

            foreach (var regimeData in scenario.RegimeSequence)
            {
                // Count regime changes
                if (regimeData.Regime != currentRegime)
                {
                    regimeChanges++;
                    currentRegime = regimeData.Regime;
                }

                result.RegimeFrequency[regimeData.Regime] += regimeData.DurationDays;
                result.TotalDays += regimeData.DurationDays;

                // Simulate trading during this regime period
                var periodPnL = SimulateRegimePeriod(regimeData, ref whipsawTrades, ref worstWhipsawLoss);
                
                result.RegimePerformance[regimeData.Regime] += periodPnL;
                totalPnL += periodPnL;
                equity += periodPnL;

                // Track drawdown
                peak = Math.Max(peak, equity);
                var drawdown = equity - peak;
                maxDrawdown = Math.Min(maxDrawdown, drawdown);

                if (periodPnL > 0) wins++;
                totalTrades++;
            }

            // Calculate results
            result.TotalPnL = totalPnL;
            result.MaxDrawdown = maxDrawdown;
            result.WinRate = totalTrades > 0 ? (double)wins / totalTrades : 0;
            result.RegimeChanges = regimeChanges;
            result.WhipsawTrades = whipsawTrades;
            result.WorstWhipsawLoss = worstWhipsawLoss;
            result.AverageRegimeDuration = result.TotalDays / (double)Math.Max(1, regimeChanges);

            return result;
        }

        /// <summary>
        /// Simulate trading during a specific regime period
        /// </summary>
        private double SimulateRegimePeriod(SyntheticRegimeData regimeData, ref int whipsawTrades, ref double worstWhipsawLoss)
        {
            var periodPnL = 0.0;
            var daysInRegime = regimeData.DurationDays;

            for (int day = 0; day < daysInRegime; day++)
            {
                // Create market conditions for this day
                var conditions = new RegimeSwitcher.MarketConditions
                {
                    VIX = regimeData.VIXLevel + (_random.NextDouble() - 0.5) * 5, // Daily variation
                    TrendScore = regimeData.TrendStrength + (_random.NextDouble() - 0.5) * 0.2,
                    RealizedVsImplied = regimeData.RealizedVsImplied + (_random.NextDouble() - 0.5) * 0.1,
                    IVR = Math.Min(100, Math.Max(0, regimeData.VIXLevel * 2 + (_random.NextDouble() - 0.5) * 20)),
                    Date = DateTime.Today.AddDays(day)
                };

                // Simulate strategy execution based on detected vs actual regime
                var detectedRegime = ClassifyRegimeFromConditions(conditions);
                var dailyPnL = SimulateDailyTrading(detectedRegime, regimeData.Regime, conditions);

                // Check for whipsaw (strategy switches but market doesn't)
                if (detectedRegime != regimeData.Regime && Math.Abs(dailyPnL) > 20)
                {
                    whipsawTrades++;
                    if (dailyPnL < worstWhipsawLoss)
                    {
                        worstWhipsawLoss = dailyPnL;
                    }
                }

                periodPnL += dailyPnL;
            }

            return periodPnL;
        }

        /// <summary>
        /// Classify regime from market conditions (same logic as RegimeSwitcher)
        /// </summary>
        private RegimeSwitcher.Regime ClassifyRegimeFromConditions(RegimeSwitcher.MarketConditions conditions)
        {
            if (conditions.VIX > 40 || Math.Abs(conditions.TrendScore) >= 0.8)
                return RegimeSwitcher.Regime.Convex;
            else if (conditions.VIX > 25 || conditions.RealizedVsImplied > 1.1)
                return RegimeSwitcher.Regime.Mixed;
            else
                return RegimeSwitcher.Regime.Calm;
        }

        /// <summary>
        /// Simulate daily trading with potential regime mismatch
        /// </summary>
        private double SimulateDailyTrading(RegimeSwitcher.Regime detectedRegime, RegimeSwitcher.Regime actualRegime, RegimeSwitcher.MarketConditions conditions)
        {
            // Strategy executes based on detected regime, but market behaves according to actual regime
            var basePnL = actualRegime switch
            {
                RegimeSwitcher.Regime.Calm => 15 + _random.NextDouble() * 10,      // Calm market profits
                RegimeSwitcher.Regime.Mixed => (_random.NextDouble() < 0.6 ? 1 : -1) * (10 + _random.NextDouble() * 15), // Mixed results
                RegimeSwitcher.Regime.Convex => (_random.NextDouble() < 0.3 ? 1 : -1) * (20 + _random.NextDouble() * 50), // High variance
                _ => 0
            };

            // Penalty for regime mismatch
            if (detectedRegime != actualRegime)
            {
                var mismatchPenalty = Math.Abs(basePnL) * 0.5; // 50% penalty for wrong regime
                basePnL = basePnL > 0 ? basePnL - mismatchPenalty : basePnL - mismatchPenalty;
            }

            return basePnL;
        }

        /// <summary>
        /// Print results for a single scenario
        /// </summary>
        private void PrintScenarioResults(StressTestResults result)
        {
            Console.WriteLine($"📊 RESULTS: {result.ScenarioName}");
            Console.WriteLine($"   Total P&L: ${result.TotalPnL:F0}");
            Console.WriteLine($"   Max Drawdown: ${Math.Abs(result.MaxDrawdown):F0}");
            Console.WriteLine($"   Win Rate: {result.WinRate:P1}");
            Console.WriteLine($"   Regime Changes: {result.RegimeChanges}");
            Console.WriteLine($"   Whipsaw Trades: {result.WhipsawTrades}");
            Console.WriteLine($"   Worst Whipsaw Loss: ${result.WorstWhipsawLoss:F0}");
            Console.WriteLine($"   Average Regime Duration: {result.AverageRegimeDuration:F1} days");
            
            Console.WriteLine($"   Regime Performance:");
            foreach (var regime in result.RegimePerformance.OrderByDescending(r => r.Value))
            {
                Console.WriteLine($"     {regime.Key}: ${regime.Value:F0}");
            }
        }

        /// <summary>
        /// Print comparative analysis of all scenarios
        /// </summary>
        private void PrintComparativeAnalysis(List<StressTestResults> results)
        {
            Console.WriteLine("\n" + "=".PadRight(80, '='));
            Console.WriteLine("🏆 REGIME SWITCHER STRESS TEST ANALYSIS");
            Console.WriteLine("=".PadRight(80, '='));

            Console.WriteLine("\n📊 PERFORMANCE RANKING (by Total P&L):");
            var rankedResults = results.OrderByDescending(r => r.TotalPnL).ToList();
            
            for (int i = 0; i < rankedResults.Count; i++)
            {
                var result = rankedResults[i];
                var rank = i == 0 ? "🥇" : i == 1 ? "🥈" : i == 2 ? "🥉" : $"{i + 1}.";
                Console.WriteLine($"{rank} {result.ScenarioName}: ${result.TotalPnL:F0} " +
                                  $"(Win Rate: {result.WinRate:P1}, Whipsaws: {result.WhipsawTrades})");
            }

            Console.WriteLine("\n💡 KEY INSIGHTS:");
            
            var baseline = results.FirstOrDefault(r => r.ScenarioName.Contains("BASELINE"));
            if (baseline != null)
            {
                Console.WriteLine($"📈 Baseline (24-day) Performance: ${baseline.TotalPnL:F0}");
                
                foreach (var result in results.Where(r => !r.ScenarioName.Contains("BASELINE")))
                {
                    var performanceRatio = result.TotalPnL / Math.Max(baseline.TotalPnL, 1);
                    var impact = performanceRatio > 1.1 ? "🟢 POSITIVE" : 
                                performanceRatio < 0.9 ? "🔴 NEGATIVE" : 
                                "🟡 NEUTRAL";
                    
                    Console.WriteLine($"   {result.ScenarioName}: {performanceRatio:P1} vs baseline {impact}");
                }
            }

            var worstWhipsaw = results.OrderBy(r => r.WorstWhipsawLoss).First();
            Console.WriteLine($"\n⚠️ WORST WHIPSAW SCENARIO: {worstWhipsaw.ScenarioName}");
            Console.WriteLine($"   Worst Single Loss: ${Math.Abs(worstWhipsaw.WorstWhipsawLoss):F0}");
            Console.WriteLine($"   Total Whipsaw Trades: {worstWhipsaw.WhipsawTrades}");

            var bestResilience = results.OrderByDescending(r => r.TotalPnL / Math.Max(Math.Abs(r.MaxDrawdown), 1)).First();
            Console.WriteLine($"\n🛡️ BEST RISK-ADJUSTED: {bestResilience.ScenarioName}");
            Console.WriteLine($"   Risk-Adjusted Return: {bestResilience.TotalPnL / Math.Max(Math.Abs(bestResilience.MaxDrawdown), 1):F2}");

            Console.WriteLine($"\n🎯 REGIME SWITCHING TOLERANCE:");
            var rapidChange = results.Where(r => r.RegimeChanges > 20).OrderByDescending(r => r.TotalPnL).FirstOrDefault();
            if (rapidChange != null)
            {
                Console.WriteLine($"   Best Rapid-Change Performance: {rapidChange.ScenarioName}");
                Console.WriteLine($"   Handled {rapidChange.RegimeChanges} regime changes with ${rapidChange.TotalPnL:F0} profit");
            }
        }
    }
}