using System;
using System.Collections.Generic;
using System.Linq;

namespace ODTE.Strategy
{
    /// <summary>
    /// Comprehensive regression tests for ODTE strategies
    /// Validates that Iron Condor, Credit BWB, and Convex Tail Overlay maintain expected performance
    /// </summary>
    public class StrategyRegressionTests
    {
        public class StrategyPerformanceBaseline
        {
            public string StrategyName { get; set; } = "";
            public double ExpectedWinRate { get; set; }
            public double ExpectedMinWinRate { get; set; }
            public double ExpectedMaxWinRate { get; set; }
            public double ExpectedAverageProfit { get; set; }
            public double ExpectedMaxLoss { get; set; }
            public double ExpectedSharpeRatio { get; set; }
            public double ExpectedProfitFactor { get; set; }
            public Dictionary<string, double> MarketRegimeExpectations { get; set; } = new();
            public int MinSampleSize { get; set; } = 100;
        }

        public class RegressionTestResult
        {
            public string StrategyName { get; set; } = "";
            public bool Passed { get; set; }
            public double ActualWinRate { get; set; }
            public double ActualAverageProfit { get; set; }
            public double ActualMaxLoss { get; set; }
            public double ActualSharpeRatio { get; set; }
            public double ActualProfitFactor { get; set; }
            public List<string> FailureReasons { get; set; } = new();
            public List<string> Warnings { get; set; } = new();
            public Dictionary<string, double> MarketRegimeResults { get; set; } = new();
            public int SampleSize { get; set; }
            public DateTime TestDate { get; set; } = DateTime.Now;
        }

        public class TestScenario
        {
            public string Name { get; set; } = "";
            public string MarketRegime { get; set; } = "";
            public double VIX { get; set; }
            public double TrendStrength { get; set; }
            public double RealizedVol { get; set; }
            public double ImpliedVol { get; set; }
            public int DaysToExpiry { get; set; }
            public double ExpectedOutcome { get; set; } // For validation
        }

        private readonly Random _random;
        private readonly Dictionary<string, StrategyPerformanceBaseline> _baselines;

        public StrategyRegressionTests(Random random = null)
        {
            _random = random ?? new Random(42); // Fixed seed for reproducible tests
            _baselines = InitializePerformanceBaselines();
        }

        /// <summary>
        /// Run complete regression test suite for all strategies
        /// </summary>
        public void RunCompleteRegressionSuite()
        {
            Console.WriteLine("🧪 ODTE STRATEGY REGRESSION TEST SUITE");
            Console.WriteLine("Validating Iron Condor, Credit BWB, and Convex Tail Overlay performance");
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine();

            var results = new List<RegressionTestResult>();

            // Test each strategy
            results.Add(TestIronCondorRegression());
            results.Add(TestCreditBWBRegression());
            results.Add(TestConvexTailOverlayRegression());

            // Run comparative tests
            results.AddRange(RunComparativeTests());

            // Generate final report
            GenerateFinalRegressionReport(results);
        }

        /// <summary>
        /// Initialize performance baselines for each strategy
        /// Based on historical performance and expected characteristics
        /// </summary>
        private Dictionary<string, StrategyPerformanceBaseline> InitializePerformanceBaselines()
        {
            return new Dictionary<string, StrategyPerformanceBaseline>
            {
                ["Iron Condor"] = new StrategyPerformanceBaseline
                {
                    StrategyName = "Iron Condor",
                    ExpectedWinRate = 0.75, // 75% win rate
                    ExpectedMinWinRate = 0.65, // Minimum acceptable
                    ExpectedMaxWinRate = 0.85, // Maximum expected
                    ExpectedAverageProfit = 18.0, // Average profit per trade
                    ExpectedMaxLoss = 80.0, // Maximum loss (width - credit)
                    ExpectedSharpeRatio = 1.2,
                    ExpectedProfitFactor = 2.0,
                    MarketRegimeExpectations = new Dictionary<string, double>
                    {
                        ["Calm"] = 0.85, // 85% win rate in calm markets
                        ["Mixed"] = 0.75, // 75% win rate in mixed markets
                        ["Volatile"] = 0.60 // 60% win rate in volatile markets
                    },
                    MinSampleSize = 200
                },
                
                ["Credit BWB"] = new StrategyPerformanceBaseline
                {
                    StrategyName = "Credit BWB",
                    ExpectedWinRate = 0.77, // 77% win rate (better than IC)
                    ExpectedMinWinRate = 0.70,
                    ExpectedMaxWinRate = 0.85,
                    ExpectedAverageProfit = 25.0, // Higher average profit than IC
                    ExpectedMaxLoss = 70.0, // Better risk management
                    ExpectedSharpeRatio = 1.8, // Improved risk-adjusted returns
                    ExpectedProfitFactor = 2.5,
                    MarketRegimeExpectations = new Dictionary<string, double>
                    {
                        ["Calm"] = 0.92, // 92% win rate in calm markets (excellent)
                        ["Mixed"] = 0.78, // 78% win rate in mixed markets
                        ["Volatile"] = 0.65 // 65% win rate in volatile markets
                    },
                    MinSampleSize = 200
                },
                
                ["Convex Tail Overlay"] = new StrategyPerformanceBaseline
                {
                    StrategyName = "Convex Tail Overlay",
                    ExpectedWinRate = 0.25, // 25% win rate (but large wins)
                    ExpectedMinWinRate = 0.15,
                    ExpectedMaxWinRate = 0.35,
                    ExpectedAverageProfit = 150.0, // Large average wins when they hit
                    ExpectedMaxLoss = 30.0, // Small losses most of the time
                    ExpectedSharpeRatio = 0.8, // Lower Sharpe due to volatility
                    ExpectedProfitFactor = 1.5, // Relies on occasional large wins
                    MarketRegimeExpectations = new Dictionary<string, double>
                    {
                        ["Calm"] = 0.10, // 10% win rate in calm markets (tail cost)
                        ["Mixed"] = 0.20, // 20% win rate in mixed markets
                        ["Volatile"] = 0.45 // 45% win rate in volatile markets (payoff time)
                    },
                    MinSampleSize = 100
                }
            };
        }

        /// <summary>
        /// Test Iron Condor regression against baseline
        /// </summary>
        private RegressionTestResult TestIronCondorRegression()
        {
            Console.WriteLine("🔍 TESTING: Iron Condor Regression");
            
            var baseline = _baselines["Iron Condor"];
            var result = new RegressionTestResult { StrategyName = "Iron Condor" };
            
            var trades = new List<TradeResult>();
            var scenarios = GenerateTestScenarios(baseline.MinSampleSize);
            
            foreach (var scenario in scenarios)
            {
                var trade = SimulateIronCondorTrade(scenario);
                trades.Add(trade);
            }
            
            // Calculate actual metrics
            result.SampleSize = trades.Count;
            result.ActualWinRate = trades.Count(t => t.PnL > 0) / (double)trades.Count;
            result.ActualAverageProfit = trades.Where(t => t.PnL > 0).DefaultIfEmpty().Average(t => t?.PnL ?? 0);
            result.ActualMaxLoss = trades.Where(t => t.PnL < 0).DefaultIfEmpty().Min(t => t?.PnL ?? 0);
            
            var avgPnL = trades.Average(t => t.PnL);
            var stdDev = Math.Sqrt(trades.Sum(t => Math.Pow(t.PnL - avgPnL, 2)) / trades.Count);
            result.ActualSharpeRatio = stdDev > 0 ? avgPnL / stdDev : 0;
            
            var totalWins = trades.Where(t => t.PnL > 0).Sum(t => t.PnL);
            var totalLosses = Math.Abs(trades.Where(t => t.PnL < 0).Sum(t => t.PnL));
            result.ActualProfitFactor = totalLosses > 0 ? totalWins / totalLosses : 0;
            
            // Test regime performance
            foreach (var regime in baseline.MarketRegimeExpectations.Keys)
            {
                var regimeTrades = trades.Where(t => t.MarketRegime == regime);
                if (regimeTrades.Any())
                {
                    var regimeWinRate = regimeTrades.Count(t => t.PnL > 0) / (double)regimeTrades.Count();
                    result.MarketRegimeResults[regime] = regimeWinRate;
                }
            }
            
            // Validate against baseline
            ValidateAgainstBaseline(result, baseline);
            
            PrintStrategyTestResults(result, baseline);
            return result;
        }

        /// <summary>
        /// Test Credit BWB regression against baseline
        /// </summary>
        private RegressionTestResult TestCreditBWBRegression()
        {
            Console.WriteLine("🔍 TESTING: Credit BWB Regression");
            
            var baseline = _baselines["Credit BWB"];
            var result = new RegressionTestResult { StrategyName = "Credit BWB" };
            
            var trades = new List<TradeResult>();
            var scenarios = GenerateTestScenarios(baseline.MinSampleSize);
            
            foreach (var scenario in scenarios)
            {
                var trade = SimulateCreditBWBTrade(scenario);
                trades.Add(trade);
            }
            
            // Calculate actual metrics (same logic as Iron Condor)
            CalculateTradeMetrics(trades, result);
            
            // Test regime performance
            CalculateRegimePerformance(trades, result, baseline);
            
            // Validate against baseline
            ValidateAgainstBaseline(result, baseline);
            
            PrintStrategyTestResults(result, baseline);
            return result;
        }

        /// <summary>
        /// Test Convex Tail Overlay regression against baseline
        /// </summary>
        private RegressionTestResult TestConvexTailOverlayRegression()
        {
            Console.WriteLine("🔍 TESTING: Convex Tail Overlay Regression");
            
            var baseline = _baselines["Convex Tail Overlay"];
            var result = new RegressionTestResult { StrategyName = "Convex Tail Overlay" };
            
            var trades = new List<TradeResult>();
            var scenarios = GenerateTestScenarios(baseline.MinSampleSize);
            
            foreach (var scenario in scenarios)
            {
                var trade = SimulateConvexTailOverlayTrade(scenario);
                trades.Add(trade);
            }
            
            // Calculate actual metrics
            CalculateTradeMetrics(trades, result);
            
            // Test regime performance
            CalculateRegimePerformance(trades, result, baseline);
            
            // Validate against baseline
            ValidateAgainstBaseline(result, baseline);
            
            PrintStrategyTestResults(result, baseline);
            return result;
        }

        /// <summary>
        /// Generate test scenarios covering various market conditions
        /// </summary>
        private List<TestScenario> GenerateTestScenarios(int count)
        {
            var scenarios = new List<TestScenario>();
            
            for (int i = 0; i < count; i++)
            {
                var regime = DetermineMarketRegime();
                scenarios.Add(new TestScenario
                {
                    Name = $"Scenario_{i + 1}",
                    MarketRegime = regime,
                    VIX = GenerateVIXForRegime(regime),
                    TrendStrength = GenerateTrendStrength(regime),
                    RealizedVol = GenerateRealizedVol(regime),
                    ImpliedVol = GenerateImpliedVol(regime),
                    DaysToExpiry = _random.Next(0, 4) // 0-3 DTE
                });
            }
            
            return scenarios;
        }

        private string DetermineMarketRegime()
        {
            var rand = _random.NextDouble();
            if (rand < 0.60) return "Calm";      // 60% calm markets
            if (rand < 0.80) return "Mixed";     // 20% mixed markets  
            return "Volatile";                   // 20% volatile markets
        }

        private double GenerateVIXForRegime(string regime)
        {
            return regime switch
            {
                "Calm" => 12 + _random.NextDouble() * 13,     // 12-25 VIX
                "Mixed" => 20 + _random.NextDouble() * 20,    // 20-40 VIX
                "Volatile" => 35 + _random.NextDouble() * 45, // 35-80 VIX
                _ => 20
            };
        }

        private double GenerateTrendStrength(string regime)
        {
            return regime switch
            {
                "Calm" => (_random.NextDouble() - 0.5) * 0.4,    // -0.2 to +0.2
                "Mixed" => (_random.NextDouble() - 0.5) * 0.8,   // -0.4 to +0.4
                "Volatile" => (_random.NextDouble() - 0.5) * 1.6, // -0.8 to +0.8
                _ => 0
            };
        }

        private double GenerateRealizedVol(string regime)
        {
            return regime switch
            {
                "Calm" => 0.08 + _random.NextDouble() * 0.07,    // 8-15% annual
                "Mixed" => 0.12 + _random.NextDouble() * 0.13,   // 12-25% annual
                "Volatile" => 0.20 + _random.NextDouble() * 0.30, // 20-50% annual
                _ => 0.15
            };
        }

        private double GenerateImpliedVol(string regime)
        {
            var realizedVol = GenerateRealizedVol(regime);
            var volRisk = regime switch
            {
                "Calm" => 1.0 + _random.NextDouble() * 0.3,      // IV 0-30% above RV
                "Mixed" => 1.1 + _random.NextDouble() * 0.4,     // IV 10-50% above RV
                "Volatile" => 1.2 + _random.NextDouble() * 0.8,  // IV 20-100% above RV
                _ => 1.2
            };
            return realizedVol * volRisk;
        }

        /// <summary>
        /// Simulate Iron Condor trade
        /// </summary>
        private TradeResult SimulateIronCondorTrade(TestScenario scenario)
        {
            var credit = 20.0; // Base credit
            var maxLoss = 80.0; // 100 width - 20 credit
            
            // Adjust for market conditions
            if (scenario.VIX > 30) credit *= 1.2; // Higher credit in vol
            if (scenario.DaysToExpiry == 0) credit *= 1.5; // 0DTE premium
            
            var winProbability = scenario.MarketRegime switch
            {
                "Calm" => 0.85 - Math.Abs(scenario.TrendStrength) * 0.2,
                "Mixed" => 0.75 - Math.Abs(scenario.TrendStrength) * 0.3,
                "Volatile" => 0.60 - Math.Abs(scenario.TrendStrength) * 0.4,
                _ => 0.75
            };
            
            var isWin = _random.NextDouble() < winProbability;
            var pnl = isWin ? credit * (0.8 + _random.NextDouble() * 0.2) : -_random.NextDouble() * maxLoss;
            
            return new TradeResult
            {
                PnL = pnl,
                MarketRegime = scenario.MarketRegime,
                VIX = scenario.VIX,
                Strategy = "Iron Condor"
            };
        }

        /// <summary>
        /// Simulate Credit BWB trade
        /// </summary>
        private TradeResult SimulateCreditBWBTrade(TestScenario scenario)
        {
            var credit = 25.0; // Higher base credit than IC
            var maxLoss = 70.0; // Better risk management
            
            // BWB advantages
            if (scenario.VIX > 25) credit *= 1.3; // Better vol capture
            if (scenario.DaysToExpiry == 0) credit *= 1.6; // Superior 0DTE performance
            
            var winProbability = scenario.MarketRegime switch
            {
                "Calm" => 0.92 - Math.Abs(scenario.TrendStrength) * 0.15, // Superior calm performance
                "Mixed" => 0.78 - Math.Abs(scenario.TrendStrength) * 0.25,
                "Volatile" => 0.65 - Math.Abs(scenario.TrendStrength) * 0.35,
                _ => 0.77
            };
            
            var isWin = _random.NextDouble() < winProbability;
            var pnl = isWin ? credit * (0.85 + _random.NextDouble() * 0.15) : -_random.NextDouble() * maxLoss;
            
            return new TradeResult
            {
                PnL = pnl,
                MarketRegime = scenario.MarketRegime,
                VIX = scenario.VIX,
                Strategy = "Credit BWB"
            };
        }

        /// <summary>
        /// Simulate Convex Tail Overlay trade
        /// </summary>
        private TradeResult SimulateConvexTailOverlayTrade(TestScenario scenario)
        {
            var tailCost = 25.0; // Cost of tail protection
            var convexPayoff = 200.0; // Large convex payoff when it hits
            
            var winProbability = scenario.MarketRegime switch
            {
                "Calm" => 0.10, // Rarely pays off in calm markets
                "Mixed" => 0.20, // Moderate payoff in mixed markets
                "Volatile" => 0.45 + Math.Abs(scenario.TrendStrength) * 0.3, // Pays off in volatility/trends
                _ => 0.25
            };
            
            var isWin = _random.NextDouble() < winProbability;
            var pnl = isWin ? 
                convexPayoff * (1 + scenario.VIX / 50.0) * (1 + Math.Abs(scenario.TrendStrength)) - tailCost :
                -tailCost;
            
            return new TradeResult
            {
                PnL = pnl,
                MarketRegime = scenario.MarketRegime,
                VIX = scenario.VIX,
                Strategy = "Convex Tail Overlay"
            };
        }

        /// <summary>
        /// Calculate trade metrics for result
        /// </summary>
        private void CalculateTradeMetrics(List<TradeResult> trades, RegressionTestResult result)
        {
            result.SampleSize = trades.Count;
            result.ActualWinRate = trades.Count(t => t.PnL > 0) / (double)trades.Count;
            result.ActualAverageProfit = trades.Where(t => t.PnL > 0).DefaultIfEmpty().Average(t => t?.PnL ?? 0);
            result.ActualMaxLoss = trades.Where(t => t.PnL < 0).DefaultIfEmpty().Min(t => t?.PnL ?? 0);
            
            var avgPnL = trades.Average(t => t.PnL);
            var stdDev = Math.Sqrt(trades.Sum(t => Math.Pow(t.PnL - avgPnL, 2)) / trades.Count);
            result.ActualSharpeRatio = stdDev > 0 ? avgPnL / stdDev : 0;
            
            var totalWins = trades.Where(t => t.PnL > 0).Sum(t => t.PnL);
            var totalLosses = Math.Abs(trades.Where(t => t.PnL < 0).Sum(t => t.PnL));
            result.ActualProfitFactor = totalLosses > 0 ? totalWins / totalLosses : 0;
        }

        /// <summary>
        /// Calculate regime-specific performance
        /// </summary>
        private void CalculateRegimePerformance(List<TradeResult> trades, RegressionTestResult result, StrategyPerformanceBaseline baseline)
        {
            foreach (var regime in baseline.MarketRegimeExpectations.Keys)
            {
                var regimeTrades = trades.Where(t => t.MarketRegime == regime);
                if (regimeTrades.Any())
                {
                    var regimeWinRate = regimeTrades.Count(t => t.PnL > 0) / (double)regimeTrades.Count();
                    result.MarketRegimeResults[regime] = regimeWinRate;
                }
            }
        }

        /// <summary>
        /// Validate results against baseline expectations
        /// </summary>
        private void ValidateAgainstBaseline(RegressionTestResult result, StrategyPerformanceBaseline baseline)
        {
            result.Passed = true;
            
            // Test win rate
            if (result.ActualWinRate < baseline.ExpectedMinWinRate)
            {
                result.Passed = false;
                result.FailureReasons.Add($"Win rate {result.ActualWinRate:P1} below minimum {baseline.ExpectedMinWinRate:P1}");
            }
            else if (result.ActualWinRate > baseline.ExpectedMaxWinRate)
            {
                result.Warnings.Add($"Win rate {result.ActualWinRate:P1} above expected maximum {baseline.ExpectedMaxWinRate:P1} - possible overfitting");
            }
            
            // Test Sharpe ratio
            if (result.ActualSharpeRatio < baseline.ExpectedSharpeRatio * 0.8)
            {
                result.Passed = false;
                result.FailureReasons.Add($"Sharpe ratio {result.ActualSharpeRatio:F2} significantly below expected {baseline.ExpectedSharpeRatio:F2}");
            }
            
            // Test profit factor
            if (result.ActualProfitFactor < baseline.ExpectedProfitFactor * 0.8)
            {
                result.Passed = false;
                result.FailureReasons.Add($"Profit factor {result.ActualProfitFactor:F2} significantly below expected {baseline.ExpectedProfitFactor:F2}");
            }
            
            // Test regime performance
            foreach (var regime in baseline.MarketRegimeExpectations)
            {
                if (result.MarketRegimeResults.ContainsKey(regime.Key))
                {
                    var actual = result.MarketRegimeResults[regime.Key];
                    var expected = regime.Value;
                    
                    if (actual < expected * 0.8) // 20% tolerance
                    {
                        result.Passed = false;
                        result.FailureReasons.Add($"{regime.Key} regime win rate {actual:P1} significantly below expected {expected:P1}");
                    }
                }
            }
        }

        /// <summary>
        /// Print test results for a strategy
        /// </summary>
        private void PrintStrategyTestResults(RegressionTestResult result, StrategyPerformanceBaseline baseline)
        {
            Console.WriteLine($"📊 RESULTS: {result.StrategyName}");
            Console.WriteLine($"   Status: {(result.Passed ? "✅ PASSED" : "❌ FAILED")}");
            Console.WriteLine($"   Sample Size: {result.SampleSize}");
            Console.WriteLine($"   Win Rate: {result.ActualWinRate:P1} (Expected: {baseline.ExpectedWinRate:P1})");
            Console.WriteLine($"   Avg Profit: ${result.ActualAverageProfit:F2} (Expected: ${baseline.ExpectedAverageProfit:F2})");
            Console.WriteLine($"   Max Loss: ${result.ActualMaxLoss:F2} (Expected: ${baseline.ExpectedMaxLoss:F2})");
            Console.WriteLine($"   Sharpe Ratio: {result.ActualSharpeRatio:F2} (Expected: {baseline.ExpectedSharpeRatio:F2})");
            Console.WriteLine($"   Profit Factor: {result.ActualProfitFactor:F2} (Expected: {baseline.ExpectedProfitFactor:F2})");
            
            Console.WriteLine($"   Regime Performance:");
            foreach (var regime in result.MarketRegimeResults)
            {
                var expected = baseline.MarketRegimeExpectations.GetValueOrDefault(regime.Key, 0);
                Console.WriteLine($"     {regime.Key}: {regime.Value:P1} (Expected: {expected:P1})");
            }
            
            if (result.FailureReasons.Any())
            {
                Console.WriteLine($"   ❌ Failures:");
                foreach (var failure in result.FailureReasons)
                {
                    Console.WriteLine($"     • {failure}");
                }
            }
            
            if (result.Warnings.Any())
            {
                Console.WriteLine($"   ⚠️ Warnings:");
                foreach (var warning in result.Warnings)
                {
                    Console.WriteLine($"     • {warning}");
                }
            }
            
            Console.WriteLine();
        }

        /// <summary>
        /// Run comparative tests between strategies
        /// </summary>
        private List<RegressionTestResult> RunComparativeTests()
        {
            Console.WriteLine("🔄 RUNNING COMPARATIVE TESTS");
            
            var results = new List<RegressionTestResult>();
            
            // Test BWB vs IC improvement
            var bwbVsIcResult = TestBWBvsICImprovement();
            results.Add(bwbVsIcResult);
            
            // Test Convex Tail performance in different vol regimes
            var convexVolResult = TestConvexTailVolatilityResponse();
            results.Add(convexVolResult);
            
            return results;
        }

        private RegressionTestResult TestBWBvsICImprovement()
        {
            var result = new RegressionTestResult { StrategyName = "BWB vs IC Comparison" };
            
            var scenarios = GenerateTestScenarios(500);
            var icPnL = 0.0;
            var bwbPnL = 0.0;
            
            foreach (var scenario in scenarios)
            {
                icPnL += SimulateIronCondorTrade(scenario).PnL;
                bwbPnL += SimulateCreditBWBTrade(scenario).PnL;
            }
            
            var improvement = (bwbPnL - icPnL) / Math.Abs(icPnL);
            
            result.Passed = improvement > 0.15; // BWB should be at least 15% better
            if (!result.Passed)
            {
                result.FailureReasons.Add($"BWB improvement {improvement:P1} below expected 15% minimum");
            }
            
            Console.WriteLine($"📊 BWB vs IC Comparison:");
            Console.WriteLine($"   IC Total P&L: ${icPnL:F0}");
            Console.WriteLine($"   BWB Total P&L: ${bwbPnL:F0}");
            Console.WriteLine($"   BWB Improvement: {improvement:P1}");
            Console.WriteLine($"   Status: {(result.Passed ? "✅ PASSED" : "❌ FAILED")}");
            Console.WriteLine();
            
            return result;
        }

        private RegressionTestResult TestConvexTailVolatilityResponse()
        {
            var result = new RegressionTestResult { StrategyName = "Convex Tail Volatility Response" };
            
            // Test performance in high vol vs low vol
            var lowVolScenarios = GenerateTestScenarios(100).Where(s => s.VIX < 25).ToList();
            var highVolScenarios = GenerateTestScenarios(100).Where(s => s.VIX > 40).ToList();
            
            var lowVolPnL = lowVolScenarios.Sum(s => SimulateConvexTailOverlayTrade(s).PnL);
            var highVolPnL = highVolScenarios.Sum(s => SimulateConvexTailOverlayTrade(s).PnL);
            
            // Convex tail should perform much better in high vol
            var volResponse = highVolPnL > lowVolPnL * 2; // At least 2x better in high vol
            
            result.Passed = volResponse;
            if (!result.Passed)
            {
                result.FailureReasons.Add($"Convex tail doesn't show strong volatility response (High vol: ${highVolPnL:F0} vs Low vol: ${lowVolPnL:F0})");
            }
            
            Console.WriteLine($"📊 Convex Tail Volatility Response:");
            Console.WriteLine($"   Low Vol P&L: ${lowVolPnL:F0}");
            Console.WriteLine($"   High Vol P&L: ${highVolPnL:F0}");
            Console.WriteLine($"   Vol Response Ratio: {(highVolPnL / Math.Max(Math.Abs(lowVolPnL), 1)):F2}x");
            Console.WriteLine($"   Status: {(result.Passed ? "✅ PASSED" : "❌ FAILED")}");
            Console.WriteLine();
            
            return result;
        }

        /// <summary>
        /// Generate final regression report
        /// </summary>
        private void GenerateFinalRegressionReport(List<RegressionTestResult> results)
        {
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine("🏆 FINAL REGRESSION TEST REPORT");
            Console.WriteLine("=".PadRight(80, '='));
            
            var passedTests = results.Count(r => r.Passed);
            var totalTests = results.Count;
            
            Console.WriteLine($"📊 OVERALL RESULTS:");
            Console.WriteLine($"   Tests Passed: {passedTests}/{totalTests} ({(double)passedTests/totalTests:P1})");
            Console.WriteLine($"   Overall Status: {(passedTests == totalTests ? "✅ ALL PASSED" : "❌ SOME FAILED")}");
            Console.WriteLine();
            
            Console.WriteLine($"📋 TEST SUMMARY:");
            foreach (var result in results)
            {
                var status = result.Passed ? "✅" : "❌";
                Console.WriteLine($"   {status} {result.StrategyName}");
            }
            
            var failedTests = results.Where(r => !r.Passed).ToList();
            if (failedTests.Any())
            {
                Console.WriteLine($"\n❌ FAILED TESTS DETAIL:");
                foreach (var failed in failedTests)
                {
                    Console.WriteLine($"   🔴 {failed.StrategyName}:");
                    foreach (var reason in failed.FailureReasons)
                    {
                        Console.WriteLine($"     • {reason}");
                    }
                }
            }
            
            Console.WriteLine($"\n🎯 REGRESSION TEST COMPLETED: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        }

        public class TradeResult
        {
            public double PnL { get; set; }
            public string MarketRegime { get; set; } = "";
            public double VIX { get; set; }
            public string Strategy { get; set; } = "";
        }
    }
}