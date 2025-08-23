using System;
using System.Collections.Generic;
using FluentAssertions;
using ODTE.Contracts.Strategy;
using Xunit;

namespace ODTE.Contracts.Tests.Strategy
{
    public class OptimizationResultTests
    {
        [Fact]
        public void Constructor_DefaultConstructor_ShouldCreateEmptyOptimizationResult()
        {
            // Arrange & Act
            var result = new OptimizationResult();
            
            // Assert
            result.StrategyName.Should().Be(string.Empty);
            result.FinalPnL.Should().Be(0);
            result.FitnessScore.Should().Be(0);
            result.Generation.Should().Be(0);
            result.Parameters.Should().NotBeNull();
            result.Parameters.Should().BeEmpty();
            result.StartDate.Should().Be(default(DateTime));
            result.EndDate.Should().Be(default(DateTime));
            result.OptimalStrategy.Should().BeNull();
            result.FinalReturn.Should().Be(0);
            result.SharpeRatio.Should().Be(0);
            result.MaxDrawdown.Should().Be(0);
            result.TotalTrades.Should().Be(0);
            result.WinRate.Should().Be(0);
        }

        [Fact]
        public void StrategyName_SetValue_ShouldUpdateStrategyName()
        {
            // Arrange
            var result = new OptimizationResult();
            
            // Act
            result.StrategyName = "PM414_Strategy";
            
            // Assert
            result.StrategyName.Should().Be("PM414_Strategy");
        }

        [Fact]
        public void FinalPnL_SetValue_ShouldUpdateFinalPnL()
        {
            // Arrange
            var result = new OptimizationResult();
            
            // Act
            result.FinalPnL = 125000m;
            
            // Assert
            result.FinalPnL.Should().Be(125000m);
        }

        [Fact]
        public void FitnessScore_SetValue_ShouldUpdateFitnessScore()
        {
            // Arrange
            var result = new OptimizationResult();
            
            // Act
            result.FitnessScore = 0.85;
            
            // Assert
            result.FitnessScore.Should().Be(0.85);
        }

        [Fact]
        public void Generation_SetValue_ShouldUpdateGeneration()
        {
            // Arrange
            var result = new OptimizationResult();
            
            // Act
            result.Generation = 42;
            
            // Assert
            result.Generation.Should().Be(42);
        }

        [Fact]
        public void Parameters_SetValue_ShouldUpdateParameters()
        {
            // Arrange
            var result = new OptimizationResult();
            var parameters = new Dictionary<string, object>
            {
                { "short_delta", 0.15 },
                { "width_points", 2 },
                { "stop_multiple", 2.2 }
            };
            
            // Act
            result.Parameters = parameters;
            
            // Assert
            result.Parameters.Should().BeSameAs(parameters);
            result.Parameters.Should().HaveCount(3);
            result.Parameters["short_delta"].Should().Be(0.15);
        }

        [Fact]
        public void StartDate_SetValue_ShouldUpdateStartDate()
        {
            // Arrange
            var result = new OptimizationResult();
            var startDate = new DateTime(2023, 1, 1);
            
            // Act
            result.StartDate = startDate;
            
            // Assert
            result.StartDate.Should().Be(startDate);
        }

        [Fact]
        public void EndDate_SetValue_ShouldUpdateEndDate()
        {
            // Arrange
            var result = new OptimizationResult();
            var endDate = new DateTime(2023, 12, 31);
            
            // Act
            result.EndDate = endDate;
            
            // Assert
            result.EndDate.Should().Be(endDate);
        }

        [Fact]
        public void FinalReturn_SetValue_ShouldUpdateFinalReturn()
        {
            // Arrange
            var result = new OptimizationResult();
            
            // Act
            result.FinalReturn = 0.2981m; // 29.81% return
            
            // Assert
            result.FinalReturn.Should().Be(0.2981m);
        }

        [Fact]
        public void SharpeRatio_SetValue_ShouldUpdateSharpeRatio()
        {
            // Arrange
            var result = new OptimizationResult();
            
            // Act
            result.SharpeRatio = 1.85m;
            
            // Assert
            result.SharpeRatio.Should().Be(1.85m);
        }

        [Fact]
        public void MaxDrawdown_SetValue_ShouldUpdateMaxDrawdown()
        {
            // Arrange
            var result = new OptimizationResult();
            
            // Act
            result.MaxDrawdown = -0.15m; // -15% drawdown
            
            // Assert
            result.MaxDrawdown.Should().Be(-0.15m);
        }

        [Fact]
        public void TotalTrades_SetValue_ShouldUpdateTotalTrades()
        {
            // Arrange
            var result = new OptimizationResult();
            
            // Act
            result.TotalTrades = 250;
            
            // Assert
            result.TotalTrades.Should().Be(250);
        }

        [Fact]
        public void WinRate_SetValue_ShouldUpdateWinRate()
        {
            // Arrange
            var result = new OptimizationResult();
            
            // Act
            result.WinRate = 0.734m; // 73.4% win rate
            
            // Assert
            result.WinRate.Should().Be(0.734m);
        }

        [Fact]
        public void IOptimizationResult_Interface_ShouldBeImplementedCorrectly()
        {
            // Arrange
            IOptimizationResult result = new OptimizationResult();
            var parameters = new Dictionary<string, object> { { "test", "value" } };
            
            // Act & Assert
            result.StrategyName = "TestStrategy";
            result.FinalPnL = 100000m;
            result.FitnessScore = 0.9;
            result.Generation = 10;
            result.Parameters = parameters;
            result.StartDate = new DateTime(2023, 1, 1);
            result.EndDate = new DateTime(2023, 12, 31);
            
            result.StrategyName.Should().Be("TestStrategy");
            result.FinalPnL.Should().Be(100000m);
            result.FitnessScore.Should().Be(0.9);
            result.Generation.Should().Be(10);
            result.Parameters.Should().BeSameAs(parameters);
            result.StartDate.Should().Be(new DateTime(2023, 1, 1));
            result.EndDate.Should().Be(new DateTime(2023, 12, 31));
        }

        [Fact]
        public void Parameters_DefaultDictionary_ShouldNotBeNull()
        {
            // Arrange & Act
            var result = new OptimizationResult();
            
            // Assert
            result.Parameters.Should().NotBeNull();
            result.Parameters.Should().BeEmpty();
        }

        [Fact]
        public void Parameters_CanAddAndRetrieveValues()
        {
            // Arrange
            var result = new OptimizationResult();
            
            // Act
            result.Parameters.Add("short_delta", 0.15);
            result.Parameters.Add("width_points", 2);
            result.Parameters.Add("strategy_type", "IronCondor");
            
            // Assert
            result.Parameters.Should().HaveCount(3);
            result.Parameters["short_delta"].Should().Be(0.15);
            result.Parameters["width_points"].Should().Be(2);
            result.Parameters["strategy_type"].Should().Be("IronCondor");
        }

        [Theory]
        [InlineData("", 0, 0.0, 0)]
        [InlineData("PM414", 125000, 0.85, 42)]
        [InlineData("IronCondor_v3", -25000, -0.15, 100)]
        [InlineData("Butterfly_Strategy", 0, 0.0, 1)]
        public void AllCoreProperties_WithVariousValues_ShouldSetCorrectly(string strategyName, double pnl, double fitnessScore, int generation)
        {
            // Arrange
            var result = new OptimizationResult();
            var decimalPnL = (decimal)pnl;
            
            // Act
            result.StrategyName = strategyName;
            result.FinalPnL = decimalPnL;
            result.FitnessScore = fitnessScore;
            result.Generation = generation;
            
            // Assert
            result.StrategyName.Should().Be(strategyName);
            result.FinalPnL.Should().Be(decimalPnL);
            result.FitnessScore.Should().Be(fitnessScore);
            result.Generation.Should().Be(generation);
        }

        [Theory]
        [InlineData(-1000000)]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(1000000)]
        public void FinalPnL_WithVariousValues_ShouldAcceptAllRanges(double inputPnL)
        {
            // Arrange
            var result = new OptimizationResult();
            var decimalPnL = (decimal)inputPnL;
            
            // Act
            result.FinalPnL = decimalPnL;
            
            // Assert
            result.FinalPnL.Should().Be(decimalPnL);
        }

        [Theory]
        [InlineData(-5.0)]
        [InlineData(-1.0)]
        [InlineData(0.0)]
        [InlineData(0.5)]
        [InlineData(1.0)]
        [InlineData(5.0)]
        public void FitnessScore_WithVariousValues_ShouldAcceptAllRanges(double inputScore)
        {
            // Arrange
            var result = new OptimizationResult();
            
            // Act
            result.FitnessScore = inputScore;
            
            // Assert
            result.FitnessScore.Should().Be(inputScore);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(100)]
        [InlineData(int.MaxValue)]
        public void Generation_WithVariousValues_ShouldAcceptAllRanges(int inputGeneration)
        {
            // Arrange
            var result = new OptimizationResult();
            
            // Act
            result.Generation = inputGeneration;
            
            // Assert
            result.Generation.Should().Be(inputGeneration);
        }

        [Fact]
        public void CompleteOptimizationResult_AllPropertiesSet_ShouldMaintainAllValues()
        {
            // Arrange
            var result = new OptimizationResult();
            var parameters = new Dictionary<string, object>
            {
                { "short_delta", 0.15 },
                { "width_points", 2 },
                { "stop_multiple", 2.2 }
            };
            var startDate = new DateTime(2023, 1, 1);
            var endDate = new DateTime(2023, 12, 31);
            
            // Act
            result.StrategyName = "PM414_Optimized";
            result.FinalPnL = 125000m;
            result.FitnessScore = 0.85;
            result.Generation = 42;
            result.Parameters = parameters;
            result.StartDate = startDate;
            result.EndDate = endDate;
            result.FinalReturn = 0.2981m;
            result.SharpeRatio = 1.85m;
            result.MaxDrawdown = -0.15m;
            result.TotalTrades = 250;
            result.WinRate = 0.734m;
            
            // Assert
            result.StrategyName.Should().Be("PM414_Optimized");
            result.FinalPnL.Should().Be(125000m);
            result.FitnessScore.Should().Be(0.85);
            result.Generation.Should().Be(42);
            result.Parameters.Should().HaveCount(3);
            result.Parameters["short_delta"].Should().Be(0.15);
            result.StartDate.Should().Be(startDate);
            result.EndDate.Should().Be(endDate);
            result.FinalReturn.Should().Be(0.2981m);
            result.SharpeRatio.Should().Be(1.85m);
            result.MaxDrawdown.Should().Be(-0.15m);
            result.TotalTrades.Should().Be(250);
            result.WinRate.Should().Be(0.734m);
        }

        [Fact]
        public void Parameters_WithComplexObjectTypes_ShouldStoreCorrectly()
        {
            // Arrange
            var result = new OptimizationResult();
            var complexParameters = new Dictionary<string, object>
            {
                { "int_value", 42 },
                { "double_value", 3.14159 },
                { "string_value", "test_string" },
                { "bool_value", true },
                { "decimal_value", 123.45m },
                { "date_value", new DateTime(2023, 1, 1) }
            };
            
            // Act
            result.Parameters = complexParameters;
            
            // Assert
            result.Parameters.Should().HaveCount(6);
            result.Parameters["int_value"].Should().Be(42);
            result.Parameters["double_value"].Should().Be(3.14159);
            result.Parameters["string_value"].Should().Be("test_string");
            result.Parameters["bool_value"].Should().Be(true);
            result.Parameters["decimal_value"].Should().Be(123.45m);
            result.Parameters["date_value"].Should().Be(new DateTime(2023, 1, 1));
        }
    }
}