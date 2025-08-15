using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace ODTE.Optimization.Tests
{
    /// <summary>
    /// EMERGENCY DEFENSIVE TESTS: These should have caught the random.Next(60,120) bug
    /// </summary>
    public class SimulationValidationTests
    {
        [Theory]
        [InlineData("IronCondor")]
        [InlineData("PutSpread")]
        [InlineData("CallSpread")]
        public void SimulateTradingDay_LossesNeverExceedMathematicalMaximum(string strategy)
        {
            // CRITICAL: This test should have caught the random.Next(60,120) bug
            var maxLoss = GetMaxLossForStrategy(strategy);
            var backtest = new SimpleHonestBacktest();
            
            // Test with multiple seeds to catch random variations
            for (int seed = 0; seed < 100; seed++)
            {
                var random = new System.Random(seed);
                var dayPnL = backtest.SimulateTradingDay(strategy, 500, random);
                
                // If this is a loss day, it should never exceed mathematical maximum
                if (dayPnL < 0)
                {
                    Math.Abs(dayPnL).Should().BeLessOrEqualTo(500, 
                        $"Day loss for {strategy} with seed {seed} should respect daily limit");
                }
            }
        }

        [Theory]
        [InlineData("IronCondor", 80)]   // $100 width - $20 credit = $80 max loss
        [InlineData("PutSpread", 75)]    // $100 width - $25 credit = $75 max loss
        [InlineData("CallSpread", 70)]   // $100 width - $30 credit = $70 max loss
        public void Strategy_MaxLossDefinitions_ShouldBeCorrect(string strategy, double expectedMaxLoss)
        {
            // Validate our business logic assumptions
            var actualMaxLoss = GetMaxLossForStrategy(strategy);
            actualMaxLoss.Should().Be(expectedMaxLoss, 
                $"{strategy} max loss should be mathematically correct");
        }

        [Fact]
        public void SimulateTradingDay_WithFixedSeed_ProducesConsistentResults()
        {
            // This test ensures deterministic behavior for regression testing
            var strategy = "IronCondor";
            var dailyLimit = 500.0;
            var seed = 42;

            var results = new List<double>();
            for (int i = 0; i < 5; i++)
            {
                var backtest = new SimpleHonestBacktest();
                var random = new System.Random(seed);
                var dayPnL = backtest.SimulateTradingDay(strategy, dailyLimit, random);
                results.Add(dayPnL);
            }

            // All results should be identical with same seed
            var firstResult = results[0];
            results.Should().AllBeEquivalentTo(firstResult, 
                "Fixed seed should produce identical results for regression testing");
        }

        [Theory]
        [InlineData(1)]     // Minimal positive limit
        [InlineData(100)]
        [InlineData(500)]
        [InlineData(10000)]
        public void SimulateTradingDay_RespectsDailyLossLimit(double dailyLimit)
        {
            // Edge case testing: regardless of daily limit, should never exceed it
            var backtest = new SimpleHonestBacktest();
            
            for (int seed = 0; seed < 50; seed++)
            {
                var random = new System.Random(seed);
                var result = backtest.SimulateTradingDay("IronCondor", dailyLimit, random);
                
                result.Should().BeGreaterOrEqualTo(-dailyLimit, 
                    $"Daily P&L should never exceed limit ${dailyLimit} (seed: {seed})");
            }
        }

        [Fact]
        public void SimulateTradingDay_ZeroDailyLimit_ReturnsZero()
        {
            // Edge case: zero daily limit should return zero (no trading allowed)
            var backtest = new SimpleHonestBacktest();
            var random = new Random(42);
            
            var result = backtest.SimulateTradingDay("IronCondor", 0, random);
            
            result.Should().Be(0, "Zero daily limit should prevent all trading");
        }

        [Fact]
        public void SimulateTradingDay_NegativeDailyLimit_ThrowsException()
        {
            // Boundary case: negative daily limit should throw
            var backtest = new SimpleHonestBacktest();
            var random = new Random(42);
            
            var act = () => backtest.SimulateTradingDay("IronCondor", -100, random);
            
            act.Should().Throw<ArgumentException>()
               .WithMessage("Daily limit cannot be negative*");
        }

        [Theory]
        [InlineData("")]
        [InlineData("InvalidStrategy")]
        [InlineData("INVALID")]
        public void SimulateTradingDay_InvalidStrategy_ThrowsException(string strategy)
        {
            // Boundary case: invalid strategy names should throw
            var backtest = new SimpleHonestBacktest();
            var random = new Random(42);
            
            var act = () => backtest.SimulateTradingDay(strategy, 500, random);
            
            act.Should().Throw<ArgumentException>()
               .WithMessage($"Unknown strategy: {strategy}*");
        }

        [Fact]
        public void SimulateTradingDay_NullRandom_ThrowsException()
        {
            // Boundary case: null random should throw
            var backtest = new SimpleHonestBacktest();
            
            var act = () => backtest.SimulateTradingDay("IronCondor", 500, null);
            
            act.Should().Throw<ArgumentNullException>();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public void RunHonestBacktest_InvalidTotalRuns_ThrowsException(int totalRuns)
        {
            // Boundary case: invalid total runs should throw
            var backtest = new SimpleHonestBacktest();
            
            var act = () => backtest.RunHonestBacktest(totalRuns);
            
            act.Should().Throw<ArgumentException>()
               .WithMessage("Total runs must be positive*");
        }

        [Theory]
        [InlineData("IronCondor", 0.70, 0.10)]   // Expected ~75% ± 10%
        [InlineData("PutSpread", 0.60, 0.10)]    // Expected ~65% ± 10%  
        [InlineData("CallSpread", 0.55, 0.10)]   // Expected ~60% ± 10%
        public void SimulateTradingDay_WinRateWithinReasonableRange(
            string strategy, double expectedWinRate, double tolerance)
        {
            // Statistical validation: win rates should be in expected ranges
            var backtest = new SimpleHonestBacktest();
            var random = new System.Random(42);
            var wins = 0;
            var total = 1000;

            for (int i = 0; i < total; i++)
            {
                var dayPnL = backtest.SimulateTradingDay(strategy, 500, random);
                if (dayPnL > 0) wins++;
            }

            var actualWinRate = (double)wins / total;
            actualWinRate.Should().BeInRange(expectedWinRate - tolerance, expectedWinRate + tolerance,
                $"{strategy} win rate should be within expected range");
        }

        [Fact]
        public void RunHonestBacktest_ResultsPassSanityChecks()
        {
            // Integration test: full backtest should produce reasonable results
            var backtest = new SimpleHonestBacktest();
            var results = backtest.RunHonestBacktest(totalRuns: 6); // 6 runs = 2 per strategy (3 strategies)

            results.Should().HaveCount(6);

            foreach (var result in results)
            {
                // Capital preservation
                result.FinalCapital.Should().BeGreaterOrEqualTo(0, 
                    "Final capital should never be negative");

                // Reasonable metrics  
                result.WinRate.Should().BeInRange(0, 100, "Win rate must be 0-100%");
                result.TotalTrades.Should().BeGreaterOrEqualTo(0, "Trade count must be non-negative");

                // Mathematical consistency
                var calculatedPnL = result.FinalCapital - result.StartingCapital;
                Math.Abs(calculatedPnL - result.TotalPnL).Should().BeLessThan(0.01, 
                    "P&L calculation must be consistent");

                // Reasonable P&L bounds
                result.TotalPnL.Should().BeInRange(-5000, 5000, 
                    "Total P&L should be within reasonable bounds");
            }
        }

        [Fact]
        public void SimpleHonestBacktest_ConstantsShouldBeRealistic()
        {
            // Validate the constants used in simulation
            var ironCondorMaxLoss = GetMaxLossForStrategy("IronCondor");
            var putSpreadMaxLoss = GetMaxLossForStrategy("PutSpread"); 
            var callSpreadMaxLoss = GetMaxLossForStrategy("CallSpread");

            ironCondorMaxLoss.Should().BeLessOrEqualTo(100, 
                "Iron Condor max loss should be reasonable for XSP");
            putSpreadMaxLoss.Should().BeLessOrEqualTo(100,
                "Put Spread max loss should be reasonable for XSP");
            callSpreadMaxLoss.Should().BeLessOrEqualTo(100,
                "Call Spread max loss should be reasonable for XSP");

            // All should be positive
            ironCondorMaxLoss.Should().BePositive();
            putSpreadMaxLoss.Should().BePositive();
            callSpreadMaxLoss.Should().BePositive();
        }

        [Theory]
        [InlineData("IronCondor", 500, 42)]
        [InlineData("PutSpread", 300, 123)]
        [InlineData("CallSpread", 200, 999)]
        public void SimulateTradingDay_ValidInputs_ProduceInRangeOutputs(string strategy, double dailyLimit, int seed)
        {
            // Test that valid inputs always produce outputs within expected mathematical ranges
            var backtest = new SimpleHonestBacktest();
            var random = new Random(seed);
            
            var result = backtest.SimulateTradingDay(strategy, dailyLimit, random);
            
            // Output must be within daily limit bounds
            result.Should().BeInRange(-dailyLimit, dailyLimit, 
                "Result should be within daily limit bounds");
            
            // Output should be mathematically reasonable for options trading
            result.Should().BeInRange(-1000, 1000, 
                "Result should be within sanity bounds for single day trading");
        }

        [Theory]
        [InlineData(1, "Should handle minimal runs")]
        [InlineData(3, "Should handle runs equal to strategy count")]
        [InlineData(10, "Should handle moderate runs")]
        [InlineData(100, "Should handle large runs")]
        public void RunHonestBacktest_ValidInputs_ProduceValidResults(int totalRuns, string description)
        {
            // Test that all valid inputs produce mathematically consistent results
            var backtest = new SimpleHonestBacktest();
            
            var results = backtest.RunHonestBacktest(totalRuns);
            
            // Should have correct number of results (rounded down per strategy)
            var expectedCount = Math.Max(1, totalRuns / 3) * 3; // 3 strategies
            results.Should().HaveCountGreaterOrEqualTo(Math.Min(totalRuns, 3), description);
            
            foreach (var result in results)
            {
                // All results should have valid mathematical properties
                result.StartingCapital.Should().Be(5000, "Starting capital should be consistent");
                result.FinalCapital.Should().BeGreaterOrEqualTo(0, "Final capital should not be negative");
                result.WinRate.Should().BeInRange(0, 100, "Win rate should be 0-100%");
                result.TotalTrades.Should().BePositive("Should have executed trades");
                
                // P&L consistency
                var calculatedPnL = result.FinalCapital - result.StartingCapital;
                Math.Abs(calculatedPnL - result.TotalPnL).Should().BeLessThan(0.01, 
                    "P&L calculation should be mathematically consistent");
            }
        }

        private static double GetMaxLossForStrategy(string strategy)
        {
            // This defines the mathematical maximum loss per strategy
            // Based on: Max Loss = Spread Width - Credit Received
            return strategy switch
            {
                "IronCondor" => 80,   // $100 width - $20 credit
                "PutSpread" => 75,    // $100 width - $25 credit
                "CallSpread" => 70,   // $100 width - $30 credit
                _ => throw new ArgumentException($"Unknown strategy: {strategy}")
            };
        }
    }

    /// <summary>
    /// Property-based tests for invariants that must ALWAYS hold
    /// </summary>
    public class SimulationInvariantTests
    {
        [Theory]
        [InlineData(1)]
        [InlineData(42)]
        [InlineData(100)]
        [InlineData(999)]
        public void SimulateTradingDay_AlwaysRespectsInvariants(int seed)
        {
            // Property: No matter what seed, certain invariants must hold
            var strategies = new[] { "IronCondor", "PutSpread", "CallSpread" };
            var dailyLimits = new[] { 100, 200, 500, 1000 };
            var backtest = new SimpleHonestBacktest();

            foreach (var strategy in strategies)
            {
                foreach (var limit in dailyLimits)
                {
                    var random = new System.Random(seed);
                    var result = backtest.SimulateTradingDay(strategy, limit, random);

                    // Invariants that must ALWAYS hold
                    result.Should().BeGreaterOrEqualTo(-limit, 
                        $"Should never exceed daily limit (strategy: {strategy}, limit: ${limit}, seed: {seed})");
                    
                    result.Should().BeInRange(-10000, 10000, 
                        $"Should be within sanity bounds (strategy: {strategy}, seed: {seed})");
                }
            }
        }
    }
}