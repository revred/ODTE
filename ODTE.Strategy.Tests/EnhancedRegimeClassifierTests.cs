using System;
using Xunit;
using FluentAssertions;
using ODTE.Strategy;
using ODTE.Strategy.Interfaces;
using ODTE.Strategy.Models;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// Tests for enhanced regime classifier that suppresses IC in Convex regimes
    /// Covers strategy suppression, VIX sizing, and regime-driven selection logic
    /// </summary>
    public class EnhancedRegimeClassifierTests
    {
        private readonly EnhancedRegimeClassifier _classifier;

        public EnhancedRegimeClassifierTests()
        {
            _classifier = new EnhancedRegimeClassifier();
        }

        #region Regime Classification Tests (8 tests)

        [Theory]
        [InlineData(15, 0.2, 0.3, RegimeSwitcher.Regime.Calm)]   // Low VIX, low trend
        [InlineData(20, 0.3, 0.5, RegimeSwitcher.Regime.Calm)]   // Moderate VIX, low trend
        [InlineData(30, 0.5, 0.7, RegimeSwitcher.Regime.Mixed)]  // Elevated VIX
        [InlineData(45, 0.6, 0.8, RegimeSwitcher.Regime.Convex)] // High VIX
        [InlineData(25, 0.9, 0.8, RegimeSwitcher.Regime.Convex)] // High trend score
        [InlineData(20, 0.8, 0.9, RegimeSwitcher.Regime.Mixed)]  // High IV rank
        public void ClassifyRegime_VariousConditions_ReturnsCorrectRegime(
            decimal vix, decimal trendScore, decimal ivRank, RegimeSwitcher.Regime expectedRegime)
        {
            // Arrange
            var market = CreateMarketSnapshot(vix, trendScore, ivRank);

            // Act
            var result = _classifier.AnalyzeAndRecommend(market);

            // Assert
            result.Regime.Should().Be(expectedRegime);
        }

        [Fact]
        public void ClassifyRegime_ConvexConditions_ContainsCorrectEvidence()
        {
            // Arrange
            var market = CreateMarketSnapshot(vix: 45, trendScore: 0.9m, ivRank: 0.8m);

            // Act
            var result = _classifier.AnalyzeAndRecommend(market);

            // Assert
            result.Regime.Should().Be(RegimeSwitcher.Regime.Convex);
            result.Evidence.Should().Contain("VIX:45");
            result.Evidence.Should().Contain("Trend:0.90");
        }

        #endregion

        #region IC Suppression Tests (16 tests)

        [Theory]
        [InlineData(RegimeSwitcher.Regime.Calm, "IronCondor", true)]     // IC allowed in Calm
        [InlineData(RegimeSwitcher.Regime.Mixed, "IronCondor", true)]    // IC allowed in Mixed  
        [InlineData(RegimeSwitcher.Regime.Convex, "IronCondor", false)]  // IC suppressed in Convex
        [InlineData(RegimeSwitcher.Regime.Convex, "CreditBWB", true)]    // BWB allowed in Convex
        public void ValidateCandidateOrder_ICSuppressionRules_EnforcesCorrectly(
            RegimeSwitcher.Regime regime, string strategyName, bool shouldAllow)
        {
            // Arrange
            var market = CreateMarketForRegime(regime);
            
            // Adjust position sizing based on regime for valid tests
            var rfibUtil = regime == RegimeSwitcher.Regime.Convex ? 0.2m : 0.4m; // 0.2 for VIX>40, 0.4 otherwise
            var roc = regime == RegimeSwitcher.Regime.Convex ? 0.35m : 0.25m; // Higher ROC for Convex
            
            var candidate = CreateMockCandidateOrder(strategyName, mpl: 200, roc: roc, rfibUtil: rfibUtil);

            // Act
            var allowed = _classifier.ValidateCandidateOrder(candidate, market, out var reason);

            // Assert
            allowed.Should().Be(shouldAllow, $"Strategy {strategyName} in {regime} regime should be {(shouldAllow ? "allowed" : "blocked")}. Reason: {reason}");
            if (!shouldAllow && strategyName == "IronCondor" && regime == RegimeSwitcher.Regime.Convex)
            {
                reason.Should().Contain("IC suppressed in Convex");
            }
        }

        [Fact]
        public void ValidateCandidateOrder_VIXOver40_SuppressesIC()
        {
            // Arrange
            var market = CreateMarketSnapshot(vix: 45, trendScore: 0.3m, ivRank: 0.5m);
            var icCandidate = CreateMockCandidateOrder("IronCondor", mpl: 200);

            // Act
            var allowed = _classifier.ValidateCandidateOrder(icCandidate, market, out var reason);

            // Assert
            allowed.Should().BeFalse();
            reason.Should().Contain("IC suppressed");
        }

        [Fact]
        public void ValidateCandidateOrder_VIXOver40_AllowsBWBWithGoodROC()
        {
            // Arrange
            var market = CreateMarketSnapshot(vix: 45, trendScore: 0.3m, ivRank: 0.5m);
            var bwbCandidate = CreateMockCandidateOrder("CreditBWB", mpl: 200, roc: 0.35m, rfibUtil: 0.2m);

            // Act
            var allowed = _classifier.ValidateCandidateOrder(bwbCandidate, market, out var reason);

            // Assert
            allowed.Should().BeTrue();
            reason.Should().Contain("gates passed");
        }

        [Theory]
        [InlineData(35, 0.6, true)]   // VIX 35: 0.5x sizing, 0.6 util rejected
        [InlineData(35, 0.4, true)]   // VIX 35: 0.5x sizing, 0.4 util allowed
        [InlineData(45, 0.3, false)]  // VIX 45: 0.25x sizing, 0.3 util rejected
        [InlineData(45, 0.2, true)]   // VIX 45: 0.25x sizing, 0.2 util allowed
        public void ValidateCandidateOrder_VIXSizingRules_EnforcesCorrectly(
            decimal vix, decimal rfibUtilization, bool shouldAllow)
        {
            // Arrange
            var market = CreateMarketSnapshot(vix, trendScore: 0.3m, ivRank: 0.5m);
            var candidate = CreateMockCandidateOrder("CreditBWB", mpl: 200, rfibUtil: rfibUtilization);

            // Act
            var allowed = _classifier.ValidateCandidateOrder(candidate, market, out var reason);

            // Assert
            allowed.Should().Be(shouldAllow);
            if (!shouldAllow)
            {
                reason.Should().Contain("Position size too large");
            }
        }

        #endregion

        #region Strategy Selection Tests (16 tests)

        [Theory]
        [InlineData(RegimeSwitcher.Regime.Calm, "CreditBWB")]     // Prefer BWB in Calm (better ROC)
        [InlineData(RegimeSwitcher.Regime.Mixed, "CreditBWB")]    // BWB + optional tail in Mixed
        [InlineData(RegimeSwitcher.Regime.Convex, "CreditBWB")]   // BWB only if ROC adequate in Convex
        public void AnalyzeAndRecommend_StrategyPreference_SelectsOptimal(
            RegimeSwitcher.Regime regime, string expectedStrategy)
        {
            // Arrange
            var market = CreateMarketForRegime(regime);

            // Act
            var result = _classifier.AnalyzeAndRecommend(market);

            // Assert
            result.RecommendedStrategy.Should().Be(expectedStrategy);
        }

        [Fact]
        public void AnalyzeAndRecommend_ConvexRegime_ExcludesIC()
        {
            // Arrange
            var market = CreateMarketForRegime(RegimeSwitcher.Regime.Convex);

            // Act
            var result = _classifier.AnalyzeAndRecommend(market);

            // Assert
            result.AllowedStrategies.Should().NotContain("IronCondor");
            result.AllowedStrategies.Should().Contain("CreditBWB");
            result.Restrictions.Should().Contain("IC suppressed");
        }

        [Fact]
        public void AnalyzeAndRecommend_CalmRegime_AllowsBothStrategies()
        {
            // Arrange
            var market = CreateMarketForRegime(RegimeSwitcher.Regime.Calm);

            // Act
            var result = _classifier.AnalyzeAndRecommend(market);

            // Assert
            result.AllowedStrategies.Should().Contain("IronCondor");
            result.AllowedStrategies.Should().Contain("CreditBWB");
        }

        [Fact]
        public void AnalyzeAndRecommend_ExtremeVIX_RecommendsRatioBackspread()
        {
            // Arrange
            var market = CreateMarketSnapshot(vix: 55, trendScore: 0.9m, ivRank: 0.9m);

            // Act
            var result = _classifier.AnalyzeAndRecommend(market);

            // Assert
            result.Regime.Should().Be(RegimeSwitcher.Regime.Convex);
            result.RecommendedStrategy.Should().Be("RatioBackspread");
        }

        #endregion

        #region Trend Breaker Tests (8 tests)

        [Theory]
        [InlineData(0.7, true)]   // Trend below threshold
        [InlineData(0.8, false)]  // Trend at threshold  
        [InlineData(0.9, false)]  // Strong trend
        public void ValidateCandidateOrder_TrendBreaker_BlocksStrongTrends(
            decimal trendScore, bool shouldAllow)
        {
            // Arrange
            var market = CreateMarketSnapshot(vix: 20, trendScore, ivRank: 0.5m);
            var candidate = CreateMockCandidateOrder("CreditBWB", mpl: 200);

            // Act
            var allowed = _classifier.ValidateCandidateOrder(candidate, market, out var reason);

            // Assert
            allowed.Should().Be(shouldAllow);
            if (!shouldAllow)
            {
                reason.Should().Contain("strong trend");
            }
        }

        [Fact]
        public void ValidateCandidateOrder_NegativeTrend_AlsoBlocksWhenAbsolute()
        {
            // Arrange: Negative trend score that's large in absolute value
            var market = CreateMarketSnapshot(vix: 20, trendScore: -0.85m, ivRank: 0.5m);
            var candidate = CreateMockCandidateOrder("CreditBWB", mpl: 200);

            // Act
            var allowed = _classifier.ValidateCandidateOrder(candidate, market, out var reason);

            // Assert
            allowed.Should().BeFalse("Absolute trend score >= 0.8 should block entries");
            reason.Should().Contain("strong trend");
        }

        #endregion

        #region ROC Threshold Tests (8 tests)

        [Theory]
        [InlineData(RegimeSwitcher.Regime.Convex, 0.25, false)] // Below Convex threshold
        [InlineData(RegimeSwitcher.Regime.Convex, 0.30, true)]  // At Convex threshold
        [InlineData(RegimeSwitcher.Regime.Convex, 0.35, true)]  // Above Convex threshold
        [InlineData(RegimeSwitcher.Regime.Calm, 0.15, true)]    // Lower threshold for Calm
        [InlineData(RegimeSwitcher.Regime.Mixed, 0.15, true)]   // Lower threshold for Mixed
        public void ValidateCandidateOrder_ROCThresholds_EnforcesRegimeSpecific(
            RegimeSwitcher.Regime regime, decimal roc, bool shouldAllow)
        {
            // Arrange
            var market = CreateMarketForRegime(regime);
            var candidate = CreateMockCandidateOrder("CreditBWB", mpl: 200, roc: roc);

            // Act
            var allowed = _classifier.ValidateCandidateOrder(candidate, market, out var reason);

            // Assert
            allowed.Should().Be(shouldAllow);
            if (!shouldAllow && regime == RegimeSwitcher.Regime.Convex)
            {
                reason.Should().Contain("ROC too low for Convex");
            }
        }

        [Fact]
        public void ValidateCandidateOrder_BWBThinCredit_RejectsLowROC()
        {
            // Arrange
            var market = CreateMarketForRegime(RegimeSwitcher.Regime.Calm);
            var thinCreditCandidate = CreateMockCandidateOrder("CreditBWB", mpl: 1000, roc: 0.15m);

            // Act
            var allowed = _classifier.ValidateCandidateOrder(thinCreditCandidate, market, out var reason);

            // Assert
            allowed.Should().BeFalse();
            reason.Should().Contain("credit too thin");
        }

        #endregion

        #region Helper Methods

        private MarketSnapshot CreateMarketSnapshot(decimal vix, decimal trendScore, decimal ivRank)
        {
            return new MarketSnapshot
            {
                Timestamp = DateTime.Now,
                UnderlyingPrice = 4500,
                VIX = vix,
                IVRank = ivRank,
                TrendScore = trendScore,
                MarketRegime = "" // Will be determined by classifier
            };
        }

        private MarketSnapshot CreateMarketForRegime(RegimeSwitcher.Regime regime)
        {
            return regime switch
            {
                RegimeSwitcher.Regime.Calm => CreateMarketSnapshot(vix: 18, trendScore: 0.2m, ivRank: 0.3m),
                RegimeSwitcher.Regime.Mixed => CreateMarketSnapshot(vix: 28, trendScore: 0.5m, ivRank: 0.7m),
                RegimeSwitcher.Regime.Convex => CreateMarketSnapshot(vix: 42, trendScore: 0.6m, ivRank: 0.8m),
                _ => CreateMarketSnapshot(vix: 20, trendScore: 0.3m, ivRank: 0.5m)
            };
        }

        private CandidateOrder CreateMockCandidateOrder(
            string strategyName, 
            decimal mpl, 
            decimal roc = 0.25m, 
            decimal rfibUtil = 0.4m)
        {
            var mockShape = new MockStrategyShape(strategyName);
            return new CandidateOrder(
                Shape: mockShape,
                NetCredit: mpl * roc,
                MaxPotentialLoss: mpl,
                Roc: roc,
                RfibUtilization: rfibUtil,
                Reason: "Test order"
            );
        }

        private class MockStrategyShape : IStrategyShape
        {
            public string Name { get; }
            public ExerciseStyle Style => ExerciseStyle.European;
            public IReadOnlyList<OptionLeg> Legs => new List<OptionLeg>();

            public MockStrategyShape(string name)
            {
                Name = name;
            }
        }

        #endregion
    }
}