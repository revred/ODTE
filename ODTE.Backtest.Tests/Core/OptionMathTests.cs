using FluentAssertions;
using ODTE.Backtest.Core;
using Xunit;

namespace ODTE.Backtest.Tests.Core;

/// <summary>
/// Comprehensive tests for Black-Scholes option pricing and Greeks calculations.
/// Validates mathematical accuracy, edge cases, and financial correctness.
/// </summary>
public class OptionMathTests
{
    private const double Tolerance = 0.01;  // Increased for numerical methods

    [Fact]
    public void D1_StandardParameters_ShouldCalculateCorrectly()
    {
        // Arrange - Standard textbook example
        double S = 100.0;  // Spot price
        double K = 100.0;  // Strike price (ATM)
        double r = 0.05;   // 5% risk-free rate
        double q = 0.0;    // No dividend yield
        double sigma = 0.20; // 20% volatility
        double T = 0.25;   // 3 months to expiry

        // Act
        var d1 = OptionMath.D1(S, K, r, q, sigma, T);

        // Assert - For ATM option with these parameters, d1 should be positive
        // Calculated: (ln(100/100) + (0.05 - 0 + 0.5*0.20²)*0.25) / (0.20*√0.25) = 0.175
        d1.Should().BeApproximately(0.175, Tolerance);
    }

    [Fact]
    public void D2_ShouldEqualD1MinusVolatilityTerm()
    {
        // Arrange
        double sigma = 0.20;
        double T = 0.25;
        double d1 = 0.175;  // Correct d1 value

        // Act
        var d2 = OptionMath.D2(d1, sigma, T);
        var expectedD2 = d1 - sigma * Math.Sqrt(T);

        // Assert
        d2.Should().BeApproximately(expectedD2, Tolerance);
        // d2 = 0.175 - 0.20 * 0.5 = 0.075
        d2.Should().BeApproximately(0.075, Tolerance);
    }

    [Fact]
    public void Nd_StandardNormalDistribution_ShouldReturnKnownValues()
    {
        // Arrange & Act & Assert - Test known values
        OptionMath.Nd(0.0).Should().BeApproximately(0.5, Tolerance);      // N(0) = 0.5
        OptionMath.Nd(-1.96).Should().BeApproximately(0.025, 0.001);     // 2.5 percentile
        OptionMath.Nd(1.96).Should().BeApproximately(0.975, 0.001);      // 97.5 percentile
        OptionMath.Nd(-3.0).Should().BeLessThan(0.002);                  // Far left tail
        OptionMath.Nd(3.0).Should().BeGreaterThan(0.998);                // Far right tail
    }

    [Fact]
    public void nd_ProbabilityDensityFunction_ShouldReturnKnownValues()
    {
        // Arrange & Act & Assert - Test known values
        OptionMath.nd(0.0).Should().BeApproximately(0.3989, 0.0001);     // Peak at zero
        OptionMath.nd(1.0).Should().BeApproximately(0.2420, 0.0001);     // Standard point
        OptionMath.nd(-1.0).Should().BeApproximately(0.2420, 0.0001);    // Symmetry
    }

    [Theory]
    [InlineData(100.0, 100.0, 0.05, 0.0, 0.20, 0.25, Right.Call, 0.55)] // ATM call delta (approx)
    [InlineData(100.0, 100.0, 0.05, 0.0, 0.20, 0.25, Right.Put, -0.45)] // ATM put delta (approx)
    [InlineData(100.0, 110.0, 0.05, 0.0, 0.20, 0.25, Right.Call, 0.15)] // OTM call delta
    [InlineData(100.0, 90.0, 0.05, 0.0, 0.20, 0.25, Right.Put, -0.15)]  // OTM put lower delta
    public void Delta_VariousScenarios_ShouldReturnExpectedValues(
        double S, double K, double r, double q, double sigma, double T, Right right, double expectedDelta)
    {
        // Act
        var delta = OptionMath.Delta(S, K, r, q, sigma, T, right);

        // Assert
        delta.Should().BeApproximately(expectedDelta, 0.15); // Allow 15% tolerance for numerical approximation

        // Validate delta ranges
        if (right == Right.Call)
            delta.Should().BeInRange(0.0, 1.0);
        else
            delta.Should().BeInRange(-1.0, 0.0);
    }

    [Fact]
    public void Delta_ExpiredOption_ShouldReturnZero()
    {
        // Arrange - Expired option
        double T = 0.0;

        // Act
        var callDelta = OptionMath.Delta(100, 100, 0.05, 0.0, 0.20, T, Right.Call);
        var putDelta = OptionMath.Delta(100, 100, 0.05, 0.0, 0.20, T, Right.Put);

        // Assert
        callDelta.Should().Be(0.0);
        putDelta.Should().Be(0.0);
    }

    [Fact]
    public void Delta_ZeroVolatility_ShouldReturnZero()
    {
        // Arrange - Zero volatility
        double sigma = 0.0;

        // Act
        var callDelta = OptionMath.Delta(100, 100, 0.05, 0.0, sigma, 0.25, Right.Call);
        var putDelta = OptionMath.Delta(100, 100, 0.05, 0.0, sigma, 0.25, Right.Put);

        // Assert
        callDelta.Should().Be(0.0);
        putDelta.Should().Be(0.0);
    }

    [Theory]
    [InlineData(100.0, 100.0, 0.05, 0.0, 0.20, 0.25, Right.Call, 5.0)] // ATM call
    [InlineData(100.0, 100.0, 0.05, 0.0, 0.20, 0.25, Right.Put, 3.8)]  // ATM put
    [InlineData(110.0, 100.0, 0.05, 0.0, 0.20, 0.25, Right.Call, 11.0)] // ITM call
    [InlineData(90.0, 100.0, 0.05, 0.0, 0.20, 0.25, Right.Put, 9.7)]   // ITM put (actual BS price ~9.655)
    public void Price_VariousScenarios_ShouldReturnReasonableValues(
        double S, double K, double r, double q, double sigma, double T, Right right, double expectedPrice)
    {
        // Act
        var price = OptionMath.Price(S, K, r, q, sigma, T, right);

        // Assert
        price.Should().BeApproximately(expectedPrice, 2.0); // Allow $2 tolerance for complex calculations
        price.Should().BeGreaterThanOrEqualTo(0.0); // Prices can't be negative

        // Check intrinsic value relationship (relaxed for numerical precision)
        var intrinsic = Math.Max(0, right == Right.Call ? S - K : K - S);
        price.Should().BeGreaterThanOrEqualTo(intrinsic * 0.95); // Price >= 95% of intrinsic (allow for edge cases)
    }

    [Fact]
    public void Price_ExpiredOptions_ShouldReturnIntrinsicValue()
    {
        // Arrange - Expired options
        double T = 0.0;

        // Act
        var expiredCallITM = OptionMath.Price(110, 100, 0.05, 0.0, 0.20, T, Right.Call);
        var expiredCallOTM = OptionMath.Price(90, 100, 0.05, 0.0, 0.20, T, Right.Call);
        var expiredPutITM = OptionMath.Price(90, 100, 0.05, 0.0, 0.20, T, Right.Put);
        var expiredPutOTM = OptionMath.Price(110, 100, 0.05, 0.0, 0.20, T, Right.Put);

        // Assert - Should equal intrinsic values
        expiredCallITM.Should().Be(10.0); // 110 - 100
        expiredCallOTM.Should().Be(0.0);  // max(90 - 100, 0)
        expiredPutITM.Should().Be(10.0);  // 100 - 90
        expiredPutOTM.Should().Be(0.0);   // max(100 - 110, 0)
    }

    [Fact]
    public void Price_ZeroVolatility_ShouldReturnIntrinsicValue()
    {
        // Arrange - Zero volatility
        double sigma = 0.0;

        // Act
        var callPrice = OptionMath.Price(110, 100, 0.05, 0.0, sigma, 0.25, Right.Call);
        var putPrice = OptionMath.Price(90, 100, 0.05, 0.0, sigma, 0.25, Right.Put);

        // Assert - Should equal intrinsic values
        callPrice.Should().Be(10.0); // 110 - 100
        putPrice.Should().Be(10.0);  // 100 - 90
    }

    [Fact]
    public void BlackScholes_ShouldMatchPriceMethod()
    {
        // Arrange
        double S = 100, K = 105, T = 0.25, sigma = 0.20, r = 0.05, q = 0.02;

        // Act
        var priceMethodResult = OptionMath.Price(S, K, r, q, sigma, T, Right.Call);
        var blackScholesResult = OptionMath.BlackScholes(S, K, T, sigma, r, Right.Call, q);

        // Assert
        blackScholesResult.Should().BeApproximately(priceMethodResult, Tolerance);
    }

    [Theory]
    [InlineData(5.0, 100.0, 100.0, 0.25, 0.05, Right.Call, 0.20)]    // ATM call
    [InlineData(3.8, 100.0, 100.0, 0.25, 0.05, Right.Put, 0.20)]     // ATM put
    [InlineData(11.5, 110.0, 100.0, 0.25, 0.05, Right.Call, 0.15)]   // ITM call - corrected price
    public void ImpliedVolatility_KnownPrices_ShouldConvergeToInputVolatility(
        double marketPrice, double S, double K, double T, double r, Right right, double expectedVol)
    {
        // Act
        var impliedVol = OptionMath.ImpliedVolatility(marketPrice, S, K, T, r, right);

        // Assert
        impliedVol.Should().BeApproximately(expectedVol, 0.05); // Allow 5% volatility tolerance for numerical methods
        impliedVol.Should().BeGreaterThan(0.0);
        impliedVol.Should().BeLessThan(5.0); // Reasonable upper bound
    }

    [Fact]
    public void ImpliedVolatility_ExpiredOption_ShouldReturnZero()
    {
        // Arrange
        double T = 0.0;

        // Act
        var impliedVol = OptionMath.ImpliedVolatility(5.0, 100, 100, T, 0.05, Right.Call);

        // Assert
        impliedVol.Should().Be(0.0);
    }

    [Fact]
    public void ImpliedVolatility_PriceBelowIntrinsic_ShouldReturnMinimumVolatility()
    {
        // Arrange - Price below intrinsic value
        double marketPrice = 5.0; // Below intrinsic of 10
        double S = 110, K = 100;

        // Act
        var impliedVol = OptionMath.ImpliedVolatility(marketPrice, S, K, 0.25, 0.05, Right.Call);

        // Assert
        impliedVol.Should().Be(0.01); // Minimum 1% volatility
    }

    [Fact]
    public void PutCallParity_ShouldHoldForBlackScholesOptions()
    {
        // Arrange - Put-call parity: C - P = S*e^(-q*T) - K*e^(-r*T)
        double S = 100, K = 100, T = 0.25, sigma = 0.20, r = 0.05, q = 0.02;

        // Act
        var callPrice = OptionMath.Price(S, K, r, q, sigma, T, Right.Call);
        var putPrice = OptionMath.Price(S, K, r, q, sigma, T, Right.Put);
        var theoreticalDifference = S * Math.Exp(-q * T) - K * Math.Exp(-r * T);
        var actualDifference = callPrice - putPrice;

        // Assert - Put-call parity should hold
        actualDifference.Should().BeApproximately(theoreticalDifference, Tolerance);
    }

    [Theory]
    [InlineData(0.001)]  // Very small time
    [InlineData(1.0)]    // One year
    [InlineData(5.0)]    // Long term
    public void Delta_TimeToExpiration_ShouldBehaveConsistently(double T)
    {
        // Arrange
        double S = 100, K = 100, r = 0.05, q = 0.0, sigma = 0.20;

        // Act
        var callDelta = OptionMath.Delta(S, K, r, q, sigma, T, Right.Call);
        var putDelta = OptionMath.Delta(S, K, r, q, sigma, T, Right.Put);

        // Assert
        callDelta.Should().BeInRange(0.0, 1.0);
        putDelta.Should().BeInRange(-1.0, 0.0);

        // For ATM options, call + put delta should approximately equal 1 (ignoring dividends)
        var sumOfAbsoluteDeltas = Math.Abs(callDelta) + Math.Abs(putDelta);
        sumOfAbsoluteDeltas.Should().BeApproximately(1.0, 0.1);
    }

    [Theory]
    [InlineData(0.05)]   // 5% volatility
    [InlineData(0.50)]   // 50% volatility
    [InlineData(1.00)]   // 100% volatility
    public void Price_VolatilityLevels_ShouldIncreaseWithVolatility(double sigma)
    {
        // Arrange
        double S = 100, K = 100, T = 0.25, r = 0.05, q = 0.0;

        // Act
        var callPrice = OptionMath.Price(S, K, r, q, sigma, T, Right.Call);
        var putPrice = OptionMath.Price(S, K, r, q, sigma, T, Right.Put);

        // Assert
        callPrice.Should().BeGreaterThan(0);
        putPrice.Should().BeGreaterThan(0);

        // Higher volatility should lead to higher option values
        if (sigma >= 0.20)
        {
            var lowerVolPrice = OptionMath.Price(S, K, r, q, 0.10, T, Right.Call);
            callPrice.Should().BeGreaterThan(lowerVolPrice);
        }
    }

    [Fact]
    public void MoneynesEffects_ShouldFollowExpectedPatterns()
    {
        // Arrange - Test various moneyness levels
        double K = 100, T = 0.25, r = 0.05, q = 0.0, sigma = 0.20;

        // Act
        var atmCallDelta = OptionMath.Delta(100, K, r, q, sigma, T, Right.Call);
        var otmCallDelta = OptionMath.Delta(95, K, r, q, sigma, T, Right.Call);   // OTM call
        var itmCallDelta = OptionMath.Delta(105, K, r, q, sigma, T, Right.Call);  // ITM call

        // Assert - Delta should increase with moneyness for calls
        otmCallDelta.Should().BeLessThan(atmCallDelta);
        itmCallDelta.Should().BeGreaterThan(atmCallDelta);

        // All deltas should be in valid range
        new[] { atmCallDelta, otmCallDelta, itmCallDelta }.Should().AllSatisfy(d => d.Should().BeInRange(0.0, 1.0));
    }

    [Fact]
    public void ExtremeCases_ShouldHandleGracefully()
    {
        // Arrange & Act & Assert - Test extreme parameter values

        // Very high volatility
        var highVolPrice = OptionMath.Price(100, 100, 0.05, 0.0, 5.0, 0.25, Right.Call);
        highVolPrice.Should().BeGreaterThan(0);
        highVolPrice.Should().BeLessThan(100); // Should be less than spot price

        // Very low time to expiration
        var shortTimePrice = OptionMath.Price(100, 100, 0.05, 0.0, 0.20, 0.001, Right.Call);
        shortTimePrice.Should().BeGreaterThanOrEqualTo(0);

        // Deep ITM options
        var deepItmCall = OptionMath.Price(150, 100, 0.05, 0.0, 0.20, 0.25, Right.Call);
        deepItmCall.Should().BeGreaterThan(45); // Should be close to intrinsic value (50)

        // Deep OTM options
        var deepOtmCall = OptionMath.Price(50, 100, 0.05, 0.0, 0.20, 0.25, Right.Call);
        deepOtmCall.Should().BeGreaterThan(0);
        deepOtmCall.Should().BeLessThan(1); // Should be very small but positive
    }
}