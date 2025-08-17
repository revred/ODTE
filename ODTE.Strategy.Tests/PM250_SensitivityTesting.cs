using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// PM250 REVFIBNOTCH SENSITIVITY TESTING
    /// Tests different sensitivity levels to find optimal parameters
    /// Focuses on preventing 2024-2025 style losses while maintaining profitable scaling
    /// </summary>
    public class PM250_SensitivityTesting
    {
        [Fact]
        public void TestRevFibNotchSensitivityLevels()
        {
            Console.WriteLine("🔬 PM250 REVFIBNOTCH SENSITIVITY TESTING");
            Console.WriteLine("=========================================");
            Console.WriteLine($"Test Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine("Objective: Find optimal sensitivity to prevent current losses\n");

            var sensitivityTester = new RevFibNotchSensitivityTester();
            var results = sensitivityTester.RunSensitivityTests();
            
            Console.WriteLine("\n✅ SENSITIVITY TESTING COMPLETE");
            Console.WriteLine($"Optimal Configuration Found: {results.BestConfiguration}");
        }
    }

    public class RevFibNotchSensitivityTester
    {
        private List<TestScenario> _testScenarios;

        public SensitivityTestResults RunSensitivityTests()
        {
            LoadTestScenarios();
            
            var configurations = GenerateTestConfigurations();
            var results = new List<ConfigurationResult>();

            Console.WriteLine("🧪 Testing sensitivity configurations...\n");

            foreach (var config in configurations)
            {
                var result = TestConfiguration(config);
                results.Add(result);
                
                Console.WriteLine($"Config {config.Name}: " +
                    $"Final P&L: ${result.FinalPnL:F2}, " +
                    $"Max DD: {result.MaxDrawdown:P1}, " +
                    $"Protected: {result.LossesPreventedCount}");
            }

            var bestResult = results.OrderByDescending(r => r.OverallScore).First();
            
            Console.WriteLine("\n📊 SENSITIVITY TEST RESULTS:");
            Console.WriteLine("============================");
            PrintDetailedResults(results);
            
            return new SensitivityTestResults
            {
                BestConfiguration = bestResult.Configuration,
                AllResults = results,
                ImprovementVsCurrent = bestResult.OverallScore
            };
        }

        private void LoadTestScenarios()
        {
            // Load the real 2024-2025 problematic periods for testing
            _testScenarios = new List<TestScenario>
            {
                // Current system failures - these are the critical tests
                new()
                {
                    Name = "2024-Q2 Normal Market Losses",
                    Periods = new[]
                    {
                        new TradingPeriod { Date = new DateTime(2024, 4, 1), PnL = -238.13m, WinRate = 0.710m, VIX = 22.1m },
                        new TradingPeriod { Date = new DateTime(2024, 6, 1), PnL = -131.11m, WinRate = 0.706m, VIX = 19.8m },
                        new TradingPeriod { Date = new DateTime(2024, 7, 1), PnL = -144.62m, WinRate = 0.688m, VIX = 18.5m }
                    },
                    ExpectedOutcome = "Should prevent/minimize losses in normal market conditions",
                    CriticalTest = true
                },
                
                new()
                {
                    Name = "2024-Q4 System Breakdown",
                    Periods = new[]
                    {
                        new TradingPeriod { Date = new DateTime(2024, 9, 1), PnL = -222.55m, WinRate = 0.708m, VIX = 20.4m },
                        new TradingPeriod { Date = new DateTime(2024, 10, 1), PnL = -191.10m, WinRate = 0.714m, VIX = 21.7m },
                        new TradingPeriod { Date = new DateTime(2024, 12, 1), PnL = -620.16m, WinRate = 0.586m, VIX = 25.3m }
                    },
                    ExpectedOutcome = "Should detect deteriorating performance and scale down aggressively",
                    CriticalTest = true
                },
                
                new()
                {
                    Name = "2025 Recent Failures",
                    Periods = new[]
                    {
                        new TradingPeriod { Date = new DateTime(2025, 6, 1), PnL = -478.46m, WinRate = 0.522m, VIX = 23.8m },
                        new TradingPeriod { Date = new DateTime(2025, 7, 1), PnL = -348.42m, WinRate = 0.697m, VIX = 21.2m },
                        new TradingPeriod { Date = new DateTime(2025, 8, 1), PnL = -523.94m, WinRate = 0.640m, VIX = 22.9m }
                    },
                    ExpectedOutcome = "Should prevent continued deterioration with immediate protection",
                    CriticalTest = true
                },
                
                // Profitable periods for balance testing
                new()
                {
                    Name = "2024 Profitable Periods",
                    Periods = new[]
                    {
                        new TradingPeriod { Date = new DateTime(2024, 1, 1), PnL = 74.48m, WinRate = 0.741m, VIX = 16.8m },
                        new TradingPeriod { Date = new DateTime(2024, 3, 1), PnL = 1028.02m, WinRate = 0.960m, VIX = 15.2m },
                        new TradingPeriod { Date = new DateTime(2025, 2, 1), PnL = 248.71m, WinRate = 0.840m, VIX = 16.1m }
                    },
                    ExpectedOutcome = "Should scale up appropriately during profitable periods",
                    CriticalTest = false
                }
            };

            Console.WriteLine($"✓ Loaded {_testScenarios.Count} test scenarios");
            Console.WriteLine($"✓ Critical scenarios: {_testScenarios.Count(s => s.CriticalTest)}");
        }

        private List<RevFibNotchConfiguration> GenerateTestConfigurations()
        {
            return new List<RevFibNotchConfiguration>
            {
                // Current system (baseline)
                new()
                {
                    Name = "CURRENT",
                    RevFibLimits = new decimal[] { 1250, 800, 500, 300, 200, 100 },
                    ScalingSensitivity = 1.0m,
                    WinRateThreshold = 0.65m,
                    ConfirmationDays = 2,
                    ProtectiveTrigger = -100m,
                    Description = "Current system parameters"
                },
                
                // More conservative limits
                new()
                {
                    Name = "CONSERVATIVE",
                    RevFibLimits = new decimal[] { 1000, 600, 400, 250, 150, 75 },
                    ScalingSensitivity = 1.0m,
                    WinRateThreshold = 0.65m,
                    ConfirmationDays = 2,
                    ProtectiveTrigger = -100m,
                    Description = "More conservative position limits"
                },
                
                // Higher sensitivity (faster reactions)
                new()
                {
                    Name = "HIGH_SENSITIVITY",
                    RevFibLimits = new decimal[] { 1250, 800, 500, 300, 200, 100 },
                    ScalingSensitivity = 2.0m,
                    WinRateThreshold = 0.65m,
                    ConfirmationDays = 1,
                    ProtectiveTrigger = -50m,
                    Description = "Faster reaction to losses"
                },
                
                // Win rate focused
                new()
                {
                    Name = "WIN_RATE_FOCUSED",
                    RevFibLimits = new decimal[] { 1250, 800, 500, 300, 200, 100 },
                    ScalingSensitivity = 1.0m,
                    WinRateThreshold = 0.70m, // Higher threshold
                    ConfirmationDays = 1,
                    ProtectiveTrigger = -75m,
                    Description = "Scales down when win rate drops below 70%"
                },
                
                // Immediate protection
                new()
                {
                    Name = "IMMEDIATE_PROTECTION",
                    RevFibLimits = new decimal[] { 1250, 800, 500, 300, 200, 100 },
                    ScalingSensitivity = 1.5m,
                    WinRateThreshold = 0.65m,
                    ConfirmationDays = 0, // No confirmation delay
                    ProtectiveTrigger = -25m, // Very sensitive trigger
                    Description = "Immediate scaling on any loss"
                },
                
                // Ultra conservative
                new()
                {
                    Name = "ULTRA_CONSERVATIVE",
                    RevFibLimits = new decimal[] { 800, 500, 300, 200, 100, 50 },
                    ScalingSensitivity = 2.5m,
                    WinRateThreshold = 0.75m,
                    ConfirmationDays = 0,
                    ProtectiveTrigger = -25m,
                    Description = "Maximum protection parameters"
                },
                
                // Balanced approach
                new()
                {
                    Name = "BALANCED_OPTIMAL",
                    RevFibLimits = new decimal[] { 1100, 700, 450, 275, 175, 85 },
                    ScalingSensitivity = 1.5m,
                    WinRateThreshold = 0.68m,
                    ConfirmationDays = 1,
                    ProtectiveTrigger = -60m,
                    Description = "Balanced protection with growth potential"
                }
            };
        }

        private ConfigurationResult TestConfiguration(RevFibNotchConfiguration config)
        {
            var totalPnL = 0m;
            var maxDrawdown = 0m;
            var lossesPreventedCount = 0;
            var currentNotchIndex = 2; // Start at middle position
            var runningCapital = 30000m;
            var scenarioResults = new List<ScenarioResult>();

            foreach (var scenario in _testScenarios)
            {
                var scenarioStartCapital = runningCapital;
                var scenarioStartIndex = currentNotchIndex;
                var scenarioPnL = 0m;
                var consecutiveLosses = 0;

                foreach (var period in scenario.Periods)
                {
                    // Get current position size
                    var positionSize = config.RevFibLimits[currentNotchIndex];
                    
                    // Apply win rate scaling
                    if (period.WinRate < config.WinRateThreshold)
                    {
                        positionSize *= 0.8m; // Reduce size for poor win rate
                    }
                    
                    // Scale P&L by position size (baseline = $500)
                    var scaledPnL = period.PnL * (positionSize / 500m);
                    scenarioPnL += scaledPnL;
                    totalPnL += scaledPnL;
                    runningCapital += scaledPnL;
                    
                    // Check drawdown
                    var drawdownPercent = Math.Abs(Math.Min(0, scaledPnL)) / runningCapital;
                    maxDrawdown = Math.Max(maxDrawdown, drawdownPercent);
                    
                    // Check protective trigger
                    if (scaledPnL <= config.ProtectiveTrigger)
                    {
                        var oldIndex = currentNotchIndex;
                        currentNotchIndex = Math.Min(currentNotchIndex + 2, config.RevFibLimits.Length - 1);
                        if (currentNotchIndex > oldIndex)
                        {
                            lossesPreventedCount++;
                        }
                    }
                    
                    // Normal scaling logic
                    if (scaledPnL < 0)
                    {
                        consecutiveLosses++;
                        if (consecutiveLosses >= config.ConfirmationDays)
                        {
                            var movement = Math.Max(1, (int)(Math.Abs(scaledPnL) / 100 * config.ScalingSensitivity));
                            currentNotchIndex = Math.Min(currentNotchIndex + movement, config.RevFibLimits.Length - 1);
                        }
                    }
                    else
                    {
                        consecutiveLosses = 0;
                        // Scale up on profits
                        if (currentNotchIndex > 0 && scaledPnL > 50)
                        {
                            currentNotchIndex = Math.Max(currentNotchIndex - 1, 0);
                        }
                    }
                }

                scenarioResults.Add(new ScenarioResult
                {
                    ScenarioName = scenario.Name,
                    ScenarioPnL = scenarioPnL,
                    IsCritical = scenario.CriticalTest,
                    StartNotchIndex = scenarioStartIndex,
                    EndNotchIndex = currentNotchIndex,
                    ProtectionTriggered = currentNotchIndex > scenarioStartIndex
                });
            }

            // Calculate overall score
            var criticalScenarioScore = scenarioResults
                .Where(r => r.IsCritical)
                .Average(r => r.ScenarioPnL > -100 ? 100 : Math.Max(0, 100 + r.ScenarioPnL / 10));
            
            var profitabilityScore = totalPnL > 0 ? 50 : Math.Max(0, 50 + totalPnL / 100);
            var drawdownScore = Math.Max(0, 50 - maxDrawdown * 1000);
            var protectionScore = lossesPreventedCount * 20;

            var overallScore = criticalScenarioScore * 0.5 + profitabilityScore * 0.2 + 
                              drawdownScore * 0.2 + protectionScore * 0.1;

            return new ConfigurationResult
            {
                Configuration = config,
                FinalPnL = totalPnL,
                MaxDrawdown = maxDrawdown,
                LossesPreventedCount = lossesPreventedCount,
                OverallScore = overallScore,
                ScenarioResults = scenarioResults,
                CriticalScenarioScore = criticalScenarioScore
            };
        }

        private void PrintDetailedResults(List<ConfigurationResult> results)
        {
            Console.WriteLine("Configuration Ranking (by Overall Score):");
            Console.WriteLine("==========================================");
            
            var sortedResults = results.OrderByDescending(r => r.OverallScore).ToList();
            
            for (int i = 0; i < sortedResults.Count; i++)
            {
                var result = sortedResults[i];
                Console.WriteLine($"\n{i + 1}. {result.Configuration.Name} (Score: {result.OverallScore:F1})");
                Console.WriteLine($"   Description: {result.Configuration.Description}");
                Console.WriteLine($"   Final P&L: ${result.FinalPnL:F2}");
                Console.WriteLine($"   Max Drawdown: {result.MaxDrawdown:P2}");
                Console.WriteLine($"   Losses Prevented: {result.LossesPreventedCount}");
                Console.WriteLine($"   Critical Scenario Performance: {result.CriticalScenarioScore:F1}");
                
                // Show critical scenario details for top 3
                if (i < 3)
                {
                    Console.WriteLine("   Critical Scenario Results:");
                    foreach (var scenario in result.ScenarioResults.Where(s => s.IsCritical))
                    {
                        Console.WriteLine($"     • {scenario.ScenarioName}: ${scenario.ScenarioPnL:F2} " +
                            $"(Protection: {(scenario.ProtectionTriggered ? "YES" : "NO")})");
                    }
                }
            }

            Console.WriteLine("\n📈 KEY INSIGHTS:");
            Console.WriteLine("================");
            
            var best = sortedResults.First();
            var current = results.First(r => r.Configuration.Name == "CURRENT");
            
            Console.WriteLine($"Best configuration improves critical scenario performance by {best.CriticalScenarioScore - current.CriticalScenarioScore:F1} points");
            Console.WriteLine($"Best configuration prevents {best.LossesPreventedCount - current.LossesPreventedCount} additional large losses");
            Console.WriteLine($"Best configuration reduces max drawdown by {(current.MaxDrawdown - best.MaxDrawdown) * 100:F1} percentage points");
        }
    }

    public class TestScenario
    {
        public string Name { get; set; }
        public TradingPeriod[] Periods { get; set; }
        public string ExpectedOutcome { get; set; }
        public bool CriticalTest { get; set; }
    }

    public class TradingPeriod
    {
        public DateTime Date { get; set; }
        public decimal PnL { get; set; }
        public decimal WinRate { get; set; }
        public decimal VIX { get; set; }
    }

    public class RevFibNotchConfiguration
    {
        public string Name { get; set; }
        public decimal[] RevFibLimits { get; set; }
        public decimal ScalingSensitivity { get; set; }
        public decimal WinRateThreshold { get; set; }
        public int ConfirmationDays { get; set; }
        public decimal ProtectiveTrigger { get; set; }
        public string Description { get; set; }
    }

    public class ConfigurationResult
    {
        public RevFibNotchConfiguration Configuration { get; set; }
        public decimal FinalPnL { get; set; }
        public decimal MaxDrawdown { get; set; }
        public int LossesPreventedCount { get; set; }
        public double OverallScore { get; set; }
        public List<ScenarioResult> ScenarioResults { get; set; }
        public double CriticalScenarioScore { get; set; }
    }

    public class ScenarioResult
    {
        public string ScenarioName { get; set; }
        public decimal ScenarioPnL { get; set; }
        public bool IsCritical { get; set; }
        public int StartNotchIndex { get; set; }
        public int EndNotchIndex { get; set; }
        public bool ProtectionTriggered { get; set; }
    }

    public class SensitivityTestResults
    {
        public RevFibNotchConfiguration BestConfiguration { get; set; }
        public List<ConfigurationResult> AllResults { get; set; }
        public double ImprovementVsCurrent { get; set; }
    }
}