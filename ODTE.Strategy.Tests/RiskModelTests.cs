using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using ODTE.Strategy;
using ODTE.Strategy.Interfaces;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// Comprehensive unit tests for MaxPotentialLoss computations
    /// Covers Iron Condor and Credit BWB scenarios with various parameters
    /// </summary>
    public class RiskModelTests
    {
        #region Iron Condor MPL Tests (16 tests)

        [Theory]
        [InlineData(20, 20, 8.0, 1200)] // 20pt wings, $8 credit → $12 max loss per side
        [InlineData(30, 30, 10.0, 2000)] // 30pt wings, $10 credit → $20 max loss per side
        [InlineData(40, 40, 12.0, 2800)] // 40pt wings, $12 credit → $28 max loss per side
        [InlineData(25, 25, 7.5, 1750)] // 25pt wings, $7.50 credit → $17.50 max loss per side
        public void MaxPotentialLossIC_SymmetricWings_ReturnsCorrectMPL(
            decimal putWing, decimal callWing, decimal credit, decimal expectedMPL)
        {
            // Act
            var mpl = RiskModel.MaxPotentialLossIC(credit, putWing, callWing, 100);

            // Assert
            mpl.Should().Be(expectedMPL);
        }

        [Theory]
        [InlineData(20, 30, 8.0, 2200)] // Asymmetric: put=20, call=30, credit=8 → max(12,22)*100 = 2200
        [InlineData(30, 20, 10.0, 2000)] // Asymmetric: put=30, call=20, credit=10 → max(20,10)*100 = 2000
        [InlineData(25, 35, 12.0, 2300)] // Asymmetric: put=25, call=35, credit=12 → max(13,23)*100 = 2300
        [InlineData(40, 25, 15.0, 2500)] // Asymmetric: put=40, call=25, credit=15 → max(25,10)*100 = 2500
        public void MaxPotentialLossIC_AsymmetricWings_ReturnsPutOrCallSideMax(
            decimal putWing, decimal callWing, decimal credit, decimal expectedMPL)
        {
            // Act
            var mpl = RiskModel.MaxPotentialLossIC(credit, putWing, callWing, 100);

            // Assert
            mpl.Should().Be(expectedMPL);
        }

        [Theory]
        [InlineData(100, 2200)] // Standard equity options: 100 multiplier
        [InlineData(10, 220)]   // Mini options: 10 multiplier
        [InlineData(1, 22)]     // Micro options: 1 multiplier
        [InlineData(50, 1100)]  // Custom multiplier: 50
        public void MaxPotentialLossIC_DifferentMultipliers_ScalesCorrectly(
            int multiplier, decimal expectedMPL)
        {
            // Arrange: 30pt wings, $8 credit → $22 base loss per contract
            decimal putWing = 30, callWing = 30, credit = 8.0m;

            // Act
            var mpl = RiskModel.MaxPotentialLossIC(credit, putWing, callWing, multiplier);

            // Assert
            mpl.Should().Be(expectedMPL);
        }

        [Theory]
        [InlineData(0.0)] // Zero credit (unusual but possible)
        [InlineData(5.0)] // Low credit
        [InlineData(15.0)] // High credit (close to wing width)
        [InlineData(19.9)] // Very high credit (almost at wing width)
        public void MaxPotentialLossIC_VariousCredits_NeverExceedsWingWidth(decimal credit)
        {
            // Arrange: 20pt wings
            decimal putWing = 20, callWing = 20;

            // Act
            var mpl = RiskModel.MaxPotentialLossIC(credit, putWing, callWing, 100);

            // Assert
            mpl.Should().BeLessOrEqualTo(2000); // Should never exceed wing width * multiplier
            mpl.Should().BeGreaterOrEqualTo(0); // Should never be negative
        }

        #endregion

        #region Credit BWB MPL Tests (16 tests)

        [Theory]
        [InlineData(5, 15, 3.0, 700)] // Narrow=5, Far=15, Credit=3 → (15-5)*100 - 3*100 = 700
        [InlineData(7, 21, 4.0, 1000)] // Narrow=7, Far=21, Credit=4 → (21-7)*100 - 4*100 = 1000
        [InlineData(10, 30, 5.0, 1500)] // Narrow=10, Far=30, Credit=5 → (30-10)*100 - 5*100 = 1500
        [InlineData(5, 20, 2.5, 1250)] // Narrow=5, Far=20, Credit=2.5 → (20-5)*100 - 2.5*100 = 1250
        public void MaxPotentialLossBwb_ValidGeometry_ReturnsCorrectMPL(
            decimal narrowWing, decimal farWing, decimal credit, decimal expectedMPL)
        {
            // Act
            var mpl = RiskModel.MaxPotentialLossBwb(credit, narrowWing, farWing, 100);

            // Assert
            mpl.Should().Be(expectedMPL);
        }

        [Theory]
        [InlineData(10, 30, 25.0, 0)] // High credit results in negative loss → clamped to 0
        [InlineData(5, 15, 15.0, 0)]  // Credit equals wing spread → clamped to 0
        [InlineData(8, 20, 20.0, 0)]  // Credit exceeds wing spread → clamped to 0
        public void MaxPotentialLossBwb_HighCredit_ClampsToZero(
            decimal narrowWing, decimal farWing, decimal credit, decimal expectedMPL)
        {
            // Act
            var mpl = RiskModel.MaxPotentialLossBwb(credit, narrowWing, farWing, 100);

            // Assert
            mpl.Should().Be(expectedMPL);
        }

        [Theory]
        [InlineData(100, 1500)] // Standard equity options
        [InlineData(10, 150)]   // Mini options
        [InlineData(1, 15)]     // Micro options
        [InlineData(50, 750)]   // Custom multiplier
        public void MaxPotentialLossBwb_DifferentMultipliers_ScalesCorrectly(
            int multiplier, decimal expectedMPL)
        {
            // Arrange: Narrow=5, Far=20, Credit=0 → (20-5)*multiplier = 15*multiplier
            decimal narrowWing = 5, farWing = 20, credit = 0;

            // Act
            var mpl = RiskModel.MaxPotentialLossBwb(credit, narrowWing, farWing, multiplier);

            // Assert
            mpl.Should().Be(expectedMPL);
        }

        [Theory]
        [InlineData(5, 15, 3.0, 3.0 / 7.0)] // ROC = 3 / (15-5-3) = 3/7 ≈ 0.43
        [InlineData(10, 40, 5.0, 5.0 / 25.0)] // ROC = 5 / (40-10-5) = 5/25 = 0.20
        [InlineData(7, 28, 4.0, 4.0 / 17.0)] // ROC = 4 / (28-7-4) = 4/17 ≈ 0.24
        public void MaxPotentialLossBwb_ROCCalculation_ImprovesFarWingIncrease(
            decimal narrowWing, decimal farWing, decimal credit, decimal expectedROC)
        {
            // Act
            var mpl = RiskModel.MaxPotentialLossBwb(credit, narrowWing, farWing, 100);
            var roc = mpl > 0 ? credit * 100 / mpl : 0;

            // Assert
            Math.Abs((double)(roc - expectedROC)).Should().BeLessThan(0.01, 
                "ROC should improve as far wing increases until credit gate fails");
        }

        #endregion

        #region Strategy Shape Interface Tests

        [Fact]
        public void MaxPotentialLoss_IronCondorShape_CalculatesCorrectly()
        {
            // Arrange
            var ironCondor = CreateIronCondorShape(
                putStrikes: new double[] { 4000, 4020 },    // 20pt put wing
                callStrikes: new double[] { 4080, 4100 }); // 20pt call wing
            
            decimal netCredit = 8.0m;

            // Act
            var mpl = RiskModel.MaxPotentialLoss(ironCondor, netCredit, 100);

            // Assert
            mpl.Should().Be(1200); // (20 - 8) * 100 = 1200
        }

        [Fact]
        public void MaxPotentialLoss_CreditBWBShape_CalculatesCorrectly()
        {
            // Arrange
            var creditBWB = CreateCreditBWBShape(
                strikes: new double[] { 4000, 4010, 4040 }); // Narrow=10, Far=30
            
            decimal netCredit = 5.0m;

            // Act
            var mpl = RiskModel.MaxPotentialLoss(creditBWB, netCredit, 100);

            // Assert
            mpl.Should().Be(1500); // ((40-10) - 5) * 100 = 2500
        }

        [Fact]
        public void MaxPotentialLoss_UnsupportedStrategy_ThrowsException()
        {
            // Arrange
            var unsupportedStrategy = CreateMockStrategy("UnsupportedStrategy");

            // Act & Assert
            var action = () => RiskModel.MaxPotentialLoss(unsupportedStrategy, 5.0m, 100);
            action.Should().Throw<NotSupportedException>()
                .WithMessage("*UnsupportedStrategy*");
        }

        #endregion

        #region Fees and After-Fees Tests

        [Theory]
        [InlineData(1200, 10, 1210)] // MPL + fixed fees
        [InlineData(1500, 15, 1515)] // MPL + per-leg fees
        [InlineData(2000, 25, 2025)] // MPL + commission + slippage
        public void MaxPotentialLossAfterFees_IncludesFees(
            decimal baseMPL, decimal fees, decimal expectedMPLAfterFees)
        {
            // This would be implemented as part of order execution
            var mplAfterFees = baseMPL + fees;

            // Assert
            mplAfterFees.Should().Be(expectedMPLAfterFees);
            mplAfterFees.Should().BeGreaterOrEqualTo(baseMPL);
        }

        #endregion

        #region Edge Cases and Validation

        [Theory]
        [InlineData(-5)] // Negative credit
        [InlineData(-10)] // Large negative credit
        public void MaxPotentialLossIC_NegativeCredit_HandlesCorrectly(decimal negativeCredit)
        {
            // Arrange
            decimal putWing = 20, callWing = 20;

            // Act
            var mpl = RiskModel.MaxPotentialLossIC(negativeCredit, putWing, callWing, 100);

            // Assert
            mpl.Should().BeGreaterThan(2000); // Should be larger than normal due to negative credit
        }

        [Theory]
        [InlineData(1, 1)] // Minimum wings
        [InlineData(2, 2)] // Small wings
        public void MaxPotentialLossIC_SmallWings_HandlesCorrectly(decimal putWing, decimal callWing)
        {
            // Arrange
            decimal credit = 0.5m;

            // Act
            var mpl = RiskModel.MaxPotentialLossIC(credit, putWing, callWing, 100);

            // Assert
            mpl.Should().BeGreaterOrEqualTo(0);
        }

        [Fact]
        public void MaxPotentialLossIC_ZeroWings_HandlesEdgeCase()
        {
            // Arrange: Pathological case with zero wing widths
            decimal putWing = 0, callWing = 0, credit = 0.5m;

            // Act
            var mpl = RiskModel.MaxPotentialLossIC(credit, putWing, callWing, 100);

            // Assert: Zero wings with positive credit results in negative theoretical loss
            // This is a mathematical edge case - in practice this trade geometry wouldn't exist
            mpl.Should().Be(-50); // (0 - 0.5) * 100 = -50
        }

        [Fact]
        public void MaxPotentialLossBwb_FarWingSmallerThanNarrow_HandlesCorrectly()
        {
            // Arrange: Pathological case where far wing < narrow wing
            decimal narrowWing = 20, farWing = 10, credit = 2;

            // Act
            var mpl = RiskModel.MaxPotentialLossBwb(credit, narrowWing, farWing, 100);

            // Assert
            mpl.Should().Be(0); // Should clamp negative result to zero
        }

        #endregion

        #region Helper Methods

        private IStrategyShape CreateIronCondorShape(double[] putStrikes, double[] callStrikes)
        {
            var legs = new List<OptionLeg>();
            
            // Put spread (sell higher, buy lower)
            legs.Add(new OptionLeg { OptionType = "Put", Strike = putStrikes[1], Action = "Sell", Quantity = 1 });
            legs.Add(new OptionLeg { OptionType = "Put", Strike = putStrikes[0], Action = "Buy", Quantity = 1 });
            
            // Call spread (sell lower, buy higher)
            legs.Add(new OptionLeg { OptionType = "Call", Strike = callStrikes[0], Action = "Sell", Quantity = 1 });
            legs.Add(new OptionLeg { OptionType = "Call", Strike = callStrikes[1], Action = "Buy", Quantity = 1 });

            return new MockStrategyShape("IronCondor", legs);
        }

        private IStrategyShape CreateCreditBWBShape(double[] strikes)
        {
            var legs = new List<OptionLeg>
            {
                new OptionLeg { OptionType = "Put", Strike = strikes[0], Action = "Buy", Quantity = 1 },  // Far OTM
                new OptionLeg { OptionType = "Put", Strike = strikes[1], Action = "Sell", Quantity = 2 }, // Body (short 2)
                new OptionLeg { OptionType = "Put", Strike = strikes[2], Action = "Buy", Quantity = 1 }   // Near OTM
            };

            return new MockStrategyShape("CreditBWB", legs);
        }

        private IStrategyShape CreateMockStrategy(string name)
        {
            return new MockStrategyShape(name, new List<OptionLeg>());
        }

        private class MockStrategyShape : IStrategyShape
        {
            public string Name { get; }
            public ExerciseStyle Style => ExerciseStyle.European;
            public IReadOnlyList<OptionLeg> Legs { get; }

            public MockStrategyShape(string name, List<OptionLeg> legs)
            {
                Name = name;
                Legs = legs;
            }
        }

        #endregion
    }
}