using System;
using System.IO;
using System.Linq;
using Xunit;
using FluentAssertions;
using ODTE.Strategy;
using ODTE.Strategy.Configuration;
using ODTE.Strategy.Models;
using ODTE.Strategy.Interfaces;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// Comprehensive tests for configurable RFib reset threshold
    /// Validates $16 default threshold and configuration system
    /// </summary>
    public class RFib_ConfigurableThreshold_Tests
    {
        [Fact]
        public void RFibConfiguration_DefaultValues_AreCorrect()
        {
            // Act
            var config = RFibConfiguration.Instance;
            
            // Assert
            config.ResetProfitThreshold.Should().Be(16.0m, "Default reset threshold should be $16");
            config.DailyLimits.Should().HaveCount(4, "Should have 4 daily limits");
            config.DailyLimits[0].Should().Be(500m, "Day 0 limit should be $500");
            config.DailyLimits[1].Should().Be(300m, "Day 1 limit should be $300");
            config.DailyLimits[2].Should().Be(200m, "Day 2 limit should be $200");
            config.DailyLimits[3].Should().Be(100m, "Day 3+ limit should be $100");
        }

        [Fact]
        public void RFibRiskManager_UsesConfigurable_ResetThreshold()
        {
            // Arrange
            var manager = new RFibRiskManager();
            
            // Simulate a loss day to reduce limit
            manager.StartNewTradingDay(DateTime.Today);
            manager.RecordExecution(CreateMockStrategyResult(mpl: 100m, pnl: -50m));
            manager.StartNewTradingDay(DateTime.Today.AddDays(1));
            
            // Verify we're at reduced limit
            manager.GetStatus().DailyLimit.Should().Be(300m, "Should be at Day 1 limit after loss");
            
            // Act: Record a profit day of exactly $16 (should trigger reset)
            manager.RecordExecution(CreateMockStrategyResult(mpl: 50m, pnl: 16.01m));
            manager.StartNewTradingDay(DateTime.Today.AddDays(2));
            
            // Assert: Should reset to base limit
            manager.GetStatus().DailyLimit.Should().Be(500m, "Should reset to base limit with $16+ profit");
            manager.GetStatus().ConsecutiveLossDays.Should().Be(0, "Consecutive loss days should reset");
        }

        [Fact]
        public void RFibRiskManager_DoesNotReset_BelowThreshold()
        {
            // Arrange
            var manager = new RFibRiskManager();
            
            // Simulate consecutive loss days
            manager.StartNewTradingDay(DateTime.Today);
            manager.RecordExecution(CreateMockStrategyResult(mpl: 100m, pnl: -50m));
            manager.StartNewTradingDay(DateTime.Today.AddDays(1));
            manager.RecordExecution(CreateMockStrategyResult(mpl: 100m, pnl: -30m));
            manager.StartNewTradingDay(DateTime.Today.AddDays(2));
            
            // Verify we're at reduced limit (Day 2)
            manager.GetStatus().DailyLimit.Should().Be(200m, "Should be at Day 2 limit");
            
            // Act: Record profit below $16 threshold
            manager.RecordExecution(CreateMockStrategyResult(mpl: 50m, pnl: 15.99m));
            manager.StartNewTradingDay(DateTime.Today.AddDays(3));
            
            // Assert: Should NOT reset
            manager.GetStatus().DailyLimit.Should().Be(200m, "Should remain at reduced limit");
            manager.GetStatus().ConsecutiveLossDays.Should().Be(2, "Should maintain consecutive loss count");
        }

        [Fact]
        public void RFibConfiguration_CanBeUpdated_Programmatically()
        {
            // Arrange
            var config = RFibConfiguration.Instance;
            var originalThreshold = config.ResetProfitThreshold;
            
            // Act
            config.UpdateResetThreshold(25.0m);
            
            // Assert
            config.ResetProfitThreshold.Should().Be(25.0m, "Should update to new threshold");
            
            // Cleanup
            config.UpdateResetThreshold(originalThreshold);
        }

        [Fact]
        public void ReverseFibonacciRiskManager_UsesConfigurable_Threshold()
        {
            // Arrange
            var manager = new ReverseFibonacciRiskManager();
            var trades = new List<TradeExecution>();
            
            // Create loss trades to trigger curtailment
            for (int i = 0; i < 3; i++)
            {
                trades.Add(new TradeExecution
                {
                    ExecutionTime = DateTime.Today.AddDays(-i),
                    PnL = -20m,
                    Success = false,
                    Strategy = "TestStrategy"
                });
            }
            
            // Verify position is curtailed
            var curtailedSize = manager.CalculatePositionSize(10m, trades);
            curtailedSize.Should().BeLessThan(10m, "Position should be curtailed after losses");
            
            // Act: Add a profitable day of exactly $16.50
            trades.Add(new TradeExecution
            {
                ExecutionTime = DateTime.Today,
                PnL = 16.50m,
                Success = true,
                Strategy = "TestStrategy"
            });
            
            var resetSize = manager.CalculatePositionSize(10m, trades);
            
            // Assert: Should reset to full position
            resetSize.Should().Be(10m, "Position should reset to full size with $16+ profit");
        }

        [Fact]
        public void ValidateConfiguration_RejectsInvalidValues()
        {
            // Arrange
            var config = new RFibConfiguration();
            
            // Test invalid reset threshold
            config.ResetProfitThreshold = -5.0m;
            config.ValidateConfiguration().Should().BeFalse("Should reject negative reset threshold");
            
            // Test invalid daily limits
            config.ResetProfitThreshold = 16.0m;
            config.DailyLimits = new decimal[] { 100m, 200m }; // Too few values
            config.ValidateConfiguration().Should().BeFalse("Should reject insufficient daily limits");
            
            // Test valid configuration
            config.DailyLimits = new decimal[] { 500m, 300m, 200m, 100m };
            config.ValidateConfiguration().Should().BeTrue("Should accept valid configuration");
        }

        [Fact]
        public void RealData_Validation_With16DollarThreshold()
        {
            // Arrange
            var manager = new RFibRiskManager();
            var testDate = new DateTime(2020, 7, 15); // Real market date
            
            Console.WriteLine("🔧 TESTING $16 RESET THRESHOLD WITH REALISTIC SCENARIO");
            Console.WriteLine("=" + new string('=', 55));
            
            // Simulate realistic trading sequence
            manager.StartNewTradingDay(testDate);
            
            // Day 1: Small loss
            manager.RecordExecution(CreateMockStrategyResult(mpl: 80m, pnl: -12.50m));
            manager.StartNewTradingDay(testDate.AddDays(1));
            var day1Status = manager.GetStatus();
            Console.WriteLine($"Day 1: Loss -$12.50, New Limit: ${day1Status.DailyLimit}");
            
            // Day 2: Another small loss  
            manager.RecordExecution(CreateMockStrategyResult(mpl: 75m, pnl: -8.75m));
            manager.StartNewTradingDay(testDate.AddDays(2));
            var day2Status = manager.GetStatus();
            Console.WriteLine($"Day 2: Loss -$8.75, New Limit: ${day2Status.DailyLimit}");
            
            // Day 3: Moderate win above $16 threshold
            manager.RecordExecution(CreateMockStrategyResult(mpl: 60m, pnl: 18.25m));
            manager.StartNewTradingDay(testDate.AddDays(3));
            var day3Status = manager.GetStatus();
            Console.WriteLine($"Day 3: Win +$18.25 (>${RFibConfiguration.Instance.ResetProfitThreshold}), New Limit: ${day3Status.DailyLimit}");
            
            // Assert
            day1Status.DailyLimit.Should().Be(300m, "Should reduce after first loss");
            day2Status.DailyLimit.Should().Be(200m, "Should reduce further after second loss");
            day3Status.DailyLimit.Should().Be(500m, "Should reset after $16+ profit");
            day3Status.ConsecutiveLossDays.Should().Be(0, "Consecutive losses should reset");
            
            Console.WriteLine($"✅ $16 threshold working correctly: Reset triggered on ${18.25m} profit");
        }

        [Fact]
        public void Multiple_SmallWins_DontReset_Unless_Above_Threshold()
        {
            // Arrange
            var manager = new RFibRiskManager();
            var testDate = DateTime.Today;
            
            // Create loss sequence
            manager.StartNewTradingDay(testDate);
            manager.RecordExecution(CreateMockStrategyResult(mpl: 100m, pnl: -25m));
            manager.StartNewTradingDay(testDate.AddDays(1));
            manager.RecordExecution(CreateMockStrategyResult(mpl: 80m, pnl: -15m));
            manager.StartNewTradingDay(testDate.AddDays(2));
            
            // Verify we're at reduced limit
            manager.GetStatus().DailyLimit.Should().Be(200m);
            
            // Act: Multiple small wins that don't individually exceed $16
            for (int i = 0; i < 5; i++)
            {
                manager.RecordExecution(CreateMockStrategyResult(mpl: 50m, pnl: 8.50m)); // $8.50 each
                if (i < 4) // Don't advance day on last iteration
                {
                    manager.StartNewTradingDay(testDate.AddDays(3 + i));
                    manager.GetStatus().DailyLimit.Should().Be(200m, $"Should stay at reduced limit after win #{i + 1}");
                }
            }
            
            // Now add one more win to push day total over $16
            manager.RecordExecution(CreateMockStrategyResult(mpl: 50m, pnl: 10.00m)); // Total day: $52.50
            manager.StartNewTradingDay(testDate.AddDays(8));
            
            // Assert: Should reset because daily total exceeded $16
            manager.GetStatus().DailyLimit.Should().Be(500m, "Should reset when daily total exceeds $16");
        }

        #region Helper Methods

        private StrategyResult CreateMockStrategyResult(decimal mpl, decimal pnl, string exitReason = "Entry")
        {
            return new StrategyResult
            {
                StrategyName = "TestStrategy",
                ExecutionDate = DateTime.Now,
                PnL = pnl,
                MaxPotentialLoss = mpl,
                ExitReason = exitReason,
                CreditReceived = mpl * 0.25m,
                Roc = 0.25m
            };
        }

        #endregion
    }
}