using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using ODTE.Strategy;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// API Validation Tests - Ensure all public APIs work correctly with the DLL
    /// </summary>
    public class ApiValidationTests
    {
        private readonly IStrategyEngine _engine;

        public ApiValidationTests()
        {
            _engine = new StrategyEngine();
        }

        [Fact]
        public async Task StrategyEngine_AllPublicMethods_ShouldBeAccessible()
        {
            // Arrange
            var conditions = new MarketConditions
            {
                VIX = 20,
                IVRank = 30,
                TrendScore = 0.2,
                Date = DateTime.Now
            };

            var parameters = new StrategyParameters
            {
                PositionSize = 1000m,
                MaxRisk = 500m
            };

            // Act & Assert - Test that all public APIs are accessible and return valid objects
            
            // 1. Iron Condor execution
            var icResult = await _engine.ExecuteIronCondorAsync(parameters, conditions);
            icResult.Should().NotBeNull();
            icResult.StrategyName.Should().NotBeNullOrEmpty();

            // 2. Credit BWB execution  
            var bwbResult = await _engine.ExecuteCreditBWBAsync(parameters, conditions);
            bwbResult.Should().NotBeNull();
            bwbResult.StrategyName.Should().NotBeNullOrEmpty();

            // 3. Convex Tail Overlay execution
            var tailResult = await _engine.ExecuteConvexTailOverlayAsync(parameters, conditions);
            tailResult.Should().NotBeNull();
            tailResult.StrategyName.Should().NotBeNullOrEmpty();

            // 4. Strategy recommendation
            var recommendation = await _engine.AnalyzeAndRecommendAsync(conditions);
            recommendation.Should().NotBeNull();
            recommendation.RecommendedStrategy.Should().NotBeNullOrEmpty();
            recommendation.MarketRegime.Should().NotBeNullOrEmpty();

            // 5. 24-day regime switching
            var regimeResults = await _engine.Execute24DayRegimeSwitchingAsync(
                DateTime.Now.AddDays(-30), 
                DateTime.Now.AddDays(-6),
                5000m);
            regimeResults.Should().NotBeNull();
            regimeResults.Periods.Should().NotBeEmpty();

            // 6. Performance analysis
            var perfAnalysis = await _engine.AnalyzePerformanceAsync("TestStrategy", new List<StrategyResult> { icResult });
            perfAnalysis.Should().NotBeNull();
            perfAnalysis.StrategyName.Should().Be("TestStrategy");

            // 7. Regression tests
            var regressionResults = await _engine.RunRegressionTestsAsync();
            regressionResults.Should().NotBeNull();
            regressionResults.TotalTests.Should().BeGreaterThan(0);

            // 8. Stress tests
            var stressResults = await _engine.RunStressTestsAsync();
            stressResults.Should().NotBeNull();
            stressResults.Scenarios.Should().NotBeEmpty();
        }

        [Fact]
        public void MarketConditions_AllProperties_ShouldBeAccessible()
        {
            // Arrange & Act
            var conditions = new MarketConditions
            {
                Date = DateTime.Now,
                VIX = 25.5,
                IVRank = 45.2,
                TrendScore = 0.3,
                RealizedVolatility = 0.20,
                ImpliedVolatility = 0.25,
                TermStructureSlope = 0.05,
                DaysToExpiry = 1,
                UnderlyingPrice = 4500,
                MarketRegime = "mixed",
                
                // Legacy properties
                RSI = 55,
                MomentumDivergence = 0.1,
                VIXContango = 3.5
            };

            // Assert - All properties should be settable and gettable
            conditions.Date.Should().BeCloseTo(DateTime.Now, TimeSpan.FromMinutes(1));
            conditions.VIX.Should().Be(25.5);
            conditions.IVRank.Should().Be(45.2);
            conditions.TrendScore.Should().Be(0.3);
            conditions.RealizedVolatility.Should().Be(0.20);
            conditions.ImpliedVolatility.Should().Be(0.25);
            conditions.TermStructureSlope.Should().Be(0.05);
            conditions.DaysToExpiry.Should().Be(1);
            conditions.UnderlyingPrice.Should().Be(4500);
            conditions.MarketRegime.Should().Be("mixed");
            
            // Legacy compatibility
            conditions.RSI.Should().Be(55);
            conditions.MomentumDivergence.Should().Be(0.1);
            conditions.VIXContango.Should().Be(3.5);

            // Test decimal conversion properties
            conditions.RSI_Decimal.Should().Be(55m);
            conditions.IVRank_Decimal.Should().Be(45.2m);
            conditions.VIXContango_Decimal.Should().Be(3.5m);
        }

        [Fact]
        public void StrategyParameters_AllProperties_ShouldBeAccessible()
        {
            // Arrange & Act
            var parameters = new StrategyParameters
            {
                PositionSize = 2000m,
                MaxRisk = 800m,
                DeltaThreshold = 0.12,
                CreditMinimum = 0.30,
                StrikeWidth = 15,
                EnableRiskManagement = false
            };

            parameters.StrategySpecific["CustomParam"] = "TestValue";

            // Assert
            parameters.PositionSize.Should().Be(2000m);
            parameters.MaxRisk.Should().Be(800m);
            parameters.DeltaThreshold.Should().Be(0.12);
            parameters.CreditMinimum.Should().Be(0.30);
            parameters.StrikeWidth.Should().Be(15);
            parameters.EnableRiskManagement.Should().BeFalse();
            parameters.StrategySpecific["CustomParam"].Should().Be("TestValue");
        }

        [Fact]
        public void StrategyResult_AllProperties_ShouldBeAccessible()
        {
            // Arrange & Act
            var result = new StrategyResult
            {
                StrategyName = "Test Strategy",
                ExecutionDate = DateTime.Now,
                PnL = 125.50m,
                MaxRisk = 500m,
                CreditReceived = 2.50m,
                IsWin = true,
                ExitReason = "Profit target",
                WinProbability = 0.75,
                MarketRegime = "calm"
            };

            result.Greeks["Delta"] = 0.05;
            result.Greeks["Gamma"] = 0.02;
            result.Metadata["ExecutionTime"] = "09:30:00";

            var leg = new OptionLeg
            {
                OptionType = "Call",
                Strike = 4500,
                Quantity = -1,
                Action = "Sell",
                Premium = 1.25,
                Delta = 0.15,
                Gamma = 0.03,
                Theta = -0.05,
                Vega = 0.10
            };
            result.Legs.Add(leg);

            // Assert
            result.StrategyName.Should().Be("Test Strategy");
            result.ExecutionDate.Should().BeCloseTo(DateTime.Now, TimeSpan.FromMinutes(1));
            result.PnL.Should().Be(125.50m);
            result.MaxRisk.Should().Be(500m);
            result.CreditReceived.Should().Be(2.50m);
            result.IsWin.Should().BeTrue();
            result.ExitReason.Should().Be("Profit target");
            result.WinProbability.Should().Be(0.75);
            result.MarketRegime.Should().Be("calm");
            
            result.Greeks["Delta"].Should().Be(0.05);
            result.Greeks["Gamma"].Should().Be(0.02);
            result.Metadata["ExecutionTime"].Should().Be("09:30:00");
            
            result.Legs.Should().HaveCount(1);
            result.Legs[0].OptionType.Should().Be("Call");
            result.Legs[0].Strike.Should().Be(4500);
            result.Legs[0].Delta.Should().Be(0.15);
        }

        [Fact]
        public void DllLibrary_CanBeInstantiated_AsInterface()
        {
            // Arrange & Act
            IStrategyEngine engine1 = new StrategyEngine();
            var engine2 = new StrategyEngine();

            // Assert
            engine1.Should().NotBeNull();
            engine2.Should().NotBeNull();
            engine1.Should().BeOfType<StrategyEngine>();
            engine2.Should().BeOfType<StrategyEngine>();
        }

        [Theory]
        [InlineData("calm", 15, 0.1)]
        [InlineData("mixed", 25, 0.4)]
        [InlineData("convex", 45, 0.8)]
        public async Task StrategyEngine_DifferentMarketRegimes_ReturnsAppropriateResults(
            string expectedRegime, double vix, double trendScore)
        {
            // Arrange
            var conditions = new MarketConditions
            {
                VIX = vix,
                TrendScore = trendScore,
                IVRank = vix * 2, // Correlated to VIX
                Date = DateTime.Now
            };

            var parameters = new StrategyParameters { PositionSize = 1000m };

            // Act
            var recommendation = await _engine.AnalyzeAndRecommendAsync(conditions);
            var icResult = await _engine.ExecuteIronCondorAsync(parameters, conditions);
            var bwbResult = await _engine.ExecuteCreditBWBAsync(parameters, conditions);

            // Assert
            recommendation.MarketRegime.Should().Be(expectedRegime);
            recommendation.ConfidenceScore.Should().BeGreaterThan(0);
            
            icResult.MarketRegime.Should().Be(expectedRegime);
            bwbResult.MarketRegime.Should().Be(expectedRegime);
            
            // Results should be consistent with market regime
            if (expectedRegime == "calm")
            {
                recommendation.RecommendedStrategy.Should().Contain("BWB");
            }
            else if (expectedRegime == "convex")
            {
                recommendation.RecommendedStrategy.Should().Contain("Ratio");
            }
        }
    }
}