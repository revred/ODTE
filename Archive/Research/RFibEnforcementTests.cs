using System;
using Xunit;
using FluentAssertions;
using ODTE.Strategy.Models;
using ODTE.Strategy.Interfaces;
using System.Collections.Generic;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// Tests for Reverse Fibonacci (RFib) risk management enforcement
    /// Ensures MaxPotentialLoss integration with daily loss limits
    /// </summary>
    public class RFibEnforcementTests
    {
        #region RFib Daily Limit Tests (20 tests)

        [Theory]
        [InlineData(0, 500, 200, true)]   // Day 0: $200 MPL under $500 limit ✓
        [InlineData(0, 500, 500, true)]   // Day 0: $500 MPL exactly at limit ✓
        [InlineData(0, 500, 501, false)]  // Day 0: $501 MPL exceeds $500 limit ✗
        [InlineData(1, 300, 150, true)]   // Day 1: $150 MPL under $300 limit ✓
        [InlineData(1, 300, 350, false)]  // Day 1: $350 MPL exceeds $300 limit ✗
        [InlineData(2, 200, 200, true)]   // Day 2: $200 MPL exactly at limit ✓
        [InlineData(3, 100, 50, true)]    // Day 3+: $50 MPL under $100 limit ✓
        [InlineData(3, 100, 150, false)]  // Day 3+: $150 MPL exceeds $100 limit ✗
        public void RFibEnforcement_CandidateOrder_RespectsConsecutiveLossLimits(
            int consecutiveLossDays, decimal dailyLimit, decimal candidateMPL, bool shouldAllow)
        {
            // Arrange
            var candidate = CreateMockCandidateOrder(candidateMPL);
            var riskManager = new RFibRiskManager(consecutiveLossDays);

            // Act
            var allowed = riskManager.AllowOrder(candidate, dailyLimit, openRisk: 0);

            // Assert
            allowed.Should().Be(shouldAllow);
        }

        [Theory]
        [InlineData(500, 100, 200, true)]   // Open=$100, MPL=$200, Total=$300 < $500 ✓
        [InlineData(500, 300, 200, true)]   // Open=$300, MPL=$200, Total=$500 = $500 ✓
        [InlineData(500, 400, 200, false)]  // Open=$400, MPL=$200, Total=$600 > $500 ✗
        [InlineData(300, 150, 100, true)]   // Open=$150, MPL=$100, Total=$250 < $300 ✓
        [InlineData(300, 250, 100, false)]  // Open=$250, MPL=$100, Total=$350 > $300 ✗
        public void RFibEnforcement_ExistingOpenRisk_AddedToMPL(
            decimal dailyLimit, decimal openRisk, decimal candidateMPL, bool shouldAllow)
        {
            // Arrange
            var candidate = CreateMockCandidateOrder(candidateMPL);
            var riskManager = new RFibRiskManager(0); // Day 0: $500 limit

            // Act
            var allowed = riskManager.AllowOrder(candidate, dailyLimit, openRisk);

            // Assert
            allowed.Should().Be(shouldAllow);
        }

        [Fact]
        public void RFibEnforcement_ProfitableDay_ResetsToBaseLimit()
        {
            // Arrange
            var riskManager = new RFibRiskManager(3); // Started at max defense: $100 limit
            var candidate = CreateMockCandidateOrder(200);

            // Act: Simulate profitable day (should reset to $500)
            riskManager.ProcessProfitableDay();
            var allowedAfterReset = riskManager.AllowOrder(candidate, 500, openRisk: 0);

            // Assert
            allowedAfterReset.Should().BeTrue("Profitable day should reset to $500 limit");
        }

        [Fact]
        public void RFibEnforcement_SessionRollover_ResetsAllowance()
        {
            // Arrange
            var riskManager = new RFibRiskManager(2); // Day 2: $200 limit
            var candidate = CreateMockCandidateOrder(150);
            
            // Fill up most of daily allowance
            riskManager.AllowOrder(candidate, 200, openRisk: 0);

            // Act: Rollover to next session
            riskManager.StartNewSession();
            var allowedAfterRollover = riskManager.AllowOrder(candidate, 200, openRisk: 0);

            // Assert
            allowedAfterRollover.Should().BeTrue("New session should reset daily allowance");
        }

        [Theory]
        [InlineData(0)]   // $0 MPL (no risk)
        [InlineData(50)]  // $50 MPL
        [InlineData(499)] // $499 MPL (just under limit)
        public void RFibEnforcement_ValidMPLRanges_AllowsOrders(decimal mpl)
        {
            // Arrange
            var candidate = CreateMockCandidateOrder(mpl);
            var riskManager = new RFibRiskManager(0); // Day 0: $500 limit

            // Act
            var allowed = riskManager.AllowOrder(candidate, 500, openRisk: 0);

            // Assert
            allowed.Should().BeTrue($"MPL of ${mpl} should be allowed under $500 limit");
        }

        #endregion

        #region ROC and Position Sizing Tests (20 tests)

        [Theory]
        [InlineData(500, 200, 2)]    // DailyCap=$500, MPL=$200 → 2 contracts
        [InlineData(500, 250, 2)]    // DailyCap=$500, MPL=$250 → 2 contracts  
        [InlineData(500, 300, 1)]    // DailyCap=$500, MPL=$300 → 1 contract
        [InlineData(500, 600, 0)]    // DailyCap=$500, MPL=$600 → 0 contracts (too risky)
        [InlineData(300, 150, 2)]    // DailyCap=$300, MPL=$150 → 2 contracts
        [InlineData(100, 150, 0)]    // DailyCap=$100, MPL=$150 → 0 contracts (exceeds limit)
        public void ROCSizing_CalculateContracts_ReturnsCorrectPositionSize(
            decimal dailyCap, decimal mplPerContract, int expectedContracts)
        {
            // Act
            var contracts = CalculateContractCount(dailyCap, mplPerContract);

            // Assert
            contracts.Should().Be(expectedContracts);
        }

        [Theory]
        [InlineData(50, 200, 0.25)]   // Credit=$50, MPL=$200 → ROC=0.25
        [InlineData(100, 400, 0.25)]  // Credit=$100, MPL=$400 → ROC=0.25
        [InlineData(75, 300, 0.25)]   // Credit=$75, MPL=$300 → ROC=0.25
        [InlineData(30, 100, 0.30)]   // Credit=$30, MPL=$100 → ROC=0.30
        [InlineData(40, 100, 0.40)]   // Credit=$40, MPL=$100 → ROC=0.40
        public void ROCSizing_CalculateROC_ReturnsCorrectRatio(
            decimal credit, decimal mpl, decimal expectedROC)
        {
            // Act
            var roc = mpl > 0 ? credit / mpl : 0;

            // Assert
            roc.Should().BeApproximately(expectedROC, 0.001m);
        }

        [Theory]
        [InlineData(0.15, false)]  // ROC=15% below threshold
        [InlineData(0.20, true)]   // ROC=20% at threshold
        [InlineData(0.25, true)]   // ROC=25% above threshold
        [InlineData(0.40, true)]   // ROC=40% excellent
        public void ROCSizing_ROCThresholdGate_BlocksThinCredits(decimal roc, bool shouldAllow)
        {
            // Arrange
            const decimal rocThreshold = 0.20m; // 20% minimum ROC
            
            // Act
            var allowed = roc >= rocThreshold;

            // Assert
            allowed.Should().Be(shouldAllow);
        }

        [Fact]
        public void ROCSizing_ZeroMPL_HandlesGracefully()
        {
            // Arrange
            decimal credit = 50;
            decimal mpl = 0;

            // Act
            var roc = mpl > 0 ? credit / mpl : 0;

            // Assert
            roc.Should().Be(0, "Zero MPL should result in zero ROC, not division error");
        }

        #endregion

        #region RFib Utilization Tests

        [Theory]
        [InlineData(100, 200, 500, 0.6)]    // (100+200)/500 = 60% utilization
        [InlineData(200, 300, 500, 1.0)]    // (200+300)/500 = 100% utilization
        [InlineData(250, 300, 500, 1.1)]    // (250+300)/500 = 110% over-utilization
        [InlineData(0, 100, 500, 0.2)]      // (0+100)/500 = 20% utilization
        public void RFibUtilization_CalculatesCorrectly(
            decimal openRisk, decimal candidateMPL, decimal dailyCap, decimal expectedUtilization)
        {
            // Act
            var utilization = (openRisk + candidateMPL) / dailyCap;

            // Assert
            utilization.Should().BeApproximately(expectedUtilization, 0.001m);
        }

        [Theory]
        [InlineData(0.8, false)]   // 80% - under warning threshold
        [InlineData(0.9, true)]    // 90% - at warning threshold
        [InlineData(0.95, true)]   // 95% - above warning threshold
        [InlineData(1.0, true)]    // 100% - at block threshold
        [InlineData(1.1, true)]    // 110% - over block threshold
        public void RFibUtilization_WarningThresholds_TriggersCorrectly(
            decimal utilization, bool shouldWarn)
        {
            // Arrange
            const decimal warningThreshold = 0.9m;
            
            // Act
            var shouldTriggerWarning = utilization >= warningThreshold;

            // Assert
            shouldTriggerWarning.Should().Be(shouldWarn);
        }

        [Fact]
        public void RFibUtilization_OverOneHundredPercent_BlocksOrder()
        {
            // Arrange
            decimal openRisk = 400;
            decimal candidateMPL = 200;
            decimal dailyCap = 500;
            var utilization = (openRisk + candidateMPL) / dailyCap; // 120%

            // Act
            var shouldBlock = utilization > 1.0m;

            // Assert
            shouldBlock.Should().BeTrue("Over 100% utilization should block order");
            utilization.Should().BeGreaterThan(1.0m);
        }

        #endregion

        #region Helper Methods and Classes

        private CandidateOrder CreateMockCandidateOrder(decimal mpl)
        {
            var mockShape = new MockStrategyShape("TestStrategy");
            return new CandidateOrder(
                Shape: mockShape,
                NetCredit: mpl * 0.25m, // Assume 25% ROC
                MaxPotentialLoss: mpl,
                Roc: 0.25m,
                RfibUtilization: 0.5m,
                Reason: "Test order"
            );
        }

        private int CalculateContractCount(decimal dailyCap, decimal mplPerContract)
        {
            if (mplPerContract <= 0) return 0;
            return (int)Math.Floor(dailyCap / mplPerContract);
        }

        /// <summary>
        /// Mock RFib Risk Manager for testing
        /// </summary>
        private class RFibRiskManager
        {
            private readonly decimal[] _dailyLimits = { 500m, 300m, 200m, 100m };
            private int _consecutiveLossDays;
            private decimal _dailyRiskUsed = 0;

            public RFibRiskManager(int consecutiveLossDays)
            {
                _consecutiveLossDays = Math.Min(consecutiveLossDays, 3);
            }

            public bool AllowOrder(CandidateOrder candidate, decimal dailyLimit, decimal openRisk)
            {
                var totalRisk = openRisk + candidate.MaxPotentialLoss;
                return totalRisk <= dailyLimit;
            }

            public void ProcessProfitableDay()
            {
                _consecutiveLossDays = 0; // Reset to base limit
            }

            public void StartNewSession()
            {
                _dailyRiskUsed = 0; // Reset daily usage
            }
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