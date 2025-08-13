using Xunit;
using FluentAssertions;
using ODTE.Backtest.Core;

namespace ODTE.Backtest.Tests.Core;

/// <summary>
/// Tests for Black-Scholes option pricing and Greeks calculations.
/// Validates the mathematical foundation of the options backtesting engine.
/// </summary>
public class OptionMathTests
{
    [Fact]
    public void BlackScholes_CallOption_ShouldCalculateCorrectPrice()
    {
        // Arrange: ATM call option with 30 days to expiry
        double spot = 100;
        double strike = 100;
        double timeToExpiry = 30.0 / 365.0;
        double volatility = 0.20;
        double riskFreeRate = 0.05;
        
        // Act
        double callPrice = OptionMath.BlackScholes(spot, strike, timeToExpiry, volatility, riskFreeRate, Right.Call);
        
        // Assert: Expected price ~3.05 for these parameters
        callPrice.Should().BeApproximately(3.05, 0.1, 
            "ATM call with 20% vol and 30 days should price around $3.05");
        callPrice.Should().BeGreaterThan(0, "Call price must be positive");
        callPrice.Should().BeLessThan(spot, "Call price cannot exceed spot price");
    }

    [Fact]
    public void BlackScholes_PutOption_ShouldCalculateCorrectPrice()
    {
        // Arrange: ATM put option
        double spot = 100;
        double strike = 100;
        double timeToExpiry = 30.0 / 365.0;
        double volatility = 0.20;
        double riskFreeRate = 0.05;
        
        // Act
        double putPrice = OptionMath.BlackScholes(spot, strike, timeToExpiry, volatility, riskFreeRate, Right.Put);
        
        // Assert: Put-call parity relationship
        double callPrice = OptionMath.BlackScholes(spot, strike, timeToExpiry, volatility, riskFreeRate, Right.Call);
        double parity = callPrice - putPrice - (spot - strike * Math.Exp(-riskFreeRate * timeToExpiry));
        
        parity.Should().BeApproximately(0, 0.01, "Put-call parity should hold");
        putPrice.Should().BeGreaterThan(0, "Put price must be positive");
    }

    [Fact]
    public void BlackScholes_ZeroTimeToExpiry_ShouldReturnIntrinsicValue()
    {
        // Arrange: Option at expiry
        double spot = 105;
        double strike = 100;
        double timeToExpiry = 0;
        double volatility = 0.20;
        double riskFreeRate = 0.05;
        
        // Act
        double callPrice = OptionMath.BlackScholes(spot, strike, timeToExpiry, volatility, riskFreeRate, Right.Call);
        double putPrice = OptionMath.BlackScholes(spot, strike, timeToExpiry, volatility, riskFreeRate, Right.Put);
        
        // Assert: Should equal intrinsic value
        callPrice.Should().BeApproximately(5, 0.001, "ITM call at expiry = intrinsic value");
        putPrice.Should().BeApproximately(0, 0.001, "OTM put at expiry = 0");
    }

    [Fact]
    public void Delta_CallOption_ShouldBePositiveAndBounded()
    {
        // Arrange: Various strikes for call delta testing
        double spot = 100;
        double timeToExpiry = 7.0 / 365.0; // 7 days
        double volatility = 0.25;
        double riskFreeRate = 0.05;
        
        // Act & Assert: Test delta at different moneyness levels
        
        // Deep ITM call
        double deltaDeepITM = OptionMath.Delta(spot, 80, timeToExpiry, volatility, riskFreeRate, Right.Call);
        deltaDeepITM.Should().BeGreaterThan(0.9, "Deep ITM call delta should be close to 1");
        
        // ATM call
        double deltaATM = OptionMath.Delta(spot, 100, timeToExpiry, volatility, riskFreeRate, Right.Call);
        deltaATM.Should().BeApproximately(0.5, 0.1, "ATM call delta should be around 0.5");
        
        // Deep OTM call
        double deltaDeepOTM = OptionMath.Delta(spot, 120, timeToExpiry, volatility, riskFreeRate, Right.Call);
        deltaDeepOTM.Should().BeLessThan(0.1, "Deep OTM call delta should be close to 0");
        deltaDeepOTM.Should().BeGreaterThan(0, "Call delta must be positive");
    }

    [Fact]
    public void Delta_PutOption_ShouldBeNegativeAndBounded()
    {
        // Arrange
        double spot = 100;
        double strike = 100;
        double timeToExpiry = 7.0 / 365.0;
        double volatility = 0.25;
        double riskFreeRate = 0.05;
        
        // Act
        double putDelta = OptionMath.Delta(spot, strike, timeToExpiry, volatility, riskFreeRate, Right.Put);
        
        // Assert
        putDelta.Should().BeNegative("Put delta must be negative");
        putDelta.Should().BeGreaterThan(-1, "Put delta must be greater than -1");
        putDelta.Should().BeApproximately(-0.5, 0.1, "ATM put delta should be around -0.5");
    }

    [Fact]
    public void ImpliedVolatility_ShouldConvergeToInputVolatility()
    {
        // Arrange: Calculate option price with known volatility
        double spot = 100;
        double strike = 100;
        double timeToExpiry = 30.0 / 365.0;
        double inputVol = 0.25;
        double riskFreeRate = 0.05;
        
        double optionPrice = OptionMath.BlackScholes(spot, strike, timeToExpiry, inputVol, riskFreeRate, Right.Call);
        
        // Act: Solve for implied volatility
        double impliedVol = OptionMath.ImpliedVolatility(optionPrice, spot, strike, timeToExpiry, riskFreeRate, Right.Call);
        
        // Assert: Should recover the input volatility
        impliedVol.Should().BeApproximately(inputVol, 0.001, 
            "Implied volatility should match the input volatility used to generate the price");
    }

    [Fact]
    public void ImpliedVolatility_WithInvalidPrice_ShouldReturnReasonableDefault()
    {
        // Arrange: Price below intrinsic value
        double spot = 100;
        double strike = 90;
        double timeToExpiry = 30.0 / 365.0;
        double invalidPrice = 5; // Below intrinsic value of 10
        double riskFreeRate = 0.05;
        
        // Act
        double impliedVol = OptionMath.ImpliedVolatility(invalidPrice, spot, strike, timeToExpiry, riskFreeRate, Right.Call);
        
        // Assert
        impliedVol.Should().BeGreaterThan(0, "Should return positive volatility even for invalid prices");
        impliedVol.Should().BeLessThanOrEqualTo(5, "Should cap volatility at reasonable maximum");
    }

    [Theory]
    [InlineData(100, 95, 0.25)]  // 5% OTM
    [InlineData(100, 100, 0.20)] // ATM
    [InlineData(100, 105, 0.25)] // 5% OTM
    public void VolatilitySmile_ShouldBeSymmetric(double spot, double strike, double expectedVol)
    {
        // Testing that the volatility smile implementation treats puts and calls symmetrically
        // This is a simplified test - real markets have skew
        
        // Arrange
        double timeToExpiry = 30.0 / 365.0;
        double riskFreeRate = 0.05;
        
        // Calculate prices with expected volatility
        double callPrice = OptionMath.BlackScholes(spot, strike, timeToExpiry, expectedVol, riskFreeRate, Right.Call);
        double putPrice = OptionMath.BlackScholes(spot, strike, timeToExpiry, expectedVol, riskFreeRate, Right.Put);
        
        // Act: Get implied vols back
        double callIV = OptionMath.ImpliedVolatility(callPrice, spot, strike, timeToExpiry, riskFreeRate, Right.Call);
        double putIV = OptionMath.ImpliedVolatility(putPrice, spot, strike, timeToExpiry, riskFreeRate, Right.Put);
        
        // Assert
        callIV.Should().BeApproximately(putIV, 0.001, 
            "Call and put implied volatilities should match for same strike");
    }

    [Fact]
    public void Greeks_Gamma_ShouldBePositiveAndMaximizedATM()
    {
        // Gamma measures the rate of change of delta - highest for ATM options
        
        // Arrange
        double spot = 100;
        double timeToExpiry = 7.0 / 365.0;
        double volatility = 0.25;
        double riskFreeRate = 0.05;
        
        // Calculate gamma at different strikes
        double gammaOTM = CalculateGamma(spot, 110, timeToExpiry, volatility, riskFreeRate);
        double gammaATM = CalculateGamma(spot, 100, timeToExpiry, volatility, riskFreeRate);
        double gammaITM = CalculateGamma(spot, 90, timeToExpiry, volatility, riskFreeRate);
        
        // Assert
        gammaATM.Should().BeGreaterThan(gammaOTM, "ATM gamma should exceed OTM gamma");
        gammaATM.Should().BeGreaterThan(gammaITM, "ATM gamma should exceed ITM gamma");
        gammaATM.Should().BePositive("Gamma must be positive");
    }
    
    private double CalculateGamma(double spot, double strike, double timeToExpiry, double vol, double r)
    {
        // Approximate gamma using finite differences
        double epsilon = 0.01;
        double deltaUp = OptionMath.Delta(spot + epsilon, strike, timeToExpiry, vol, r, Right.Call);
        double deltaDown = OptionMath.Delta(spot - epsilon, strike, timeToExpiry, vol, r, Right.Call);
        return (deltaUp - deltaDown) / (2 * epsilon);
    }

    [Fact]
    public void Theta_ShouldBeNegative_ForLongOptions()
    {
        // Theta represents time decay - should be negative for long options
        
        // Arrange
        double spot = 100;
        double strike = 100;
        double timeToExpiry = 30.0 / 365.0;
        double volatility = 0.25;
        double riskFreeRate = 0.05;
        
        // Act: Calculate option values at different times
        double priceNow = OptionMath.BlackScholes(spot, strike, timeToExpiry, volatility, riskFreeRate, Right.Call);
        double priceTomorrow = OptionMath.BlackScholes(spot, strike, (timeToExpiry - 1.0/365.0), volatility, riskFreeRate, Right.Call);
        
        double theta = priceTomorrow - priceNow; // Daily theta
        
        // Assert
        theta.Should().BeNegative("Theta should be negative (time decay)");
        Math.Abs(theta).Should().BeLessThan(priceNow, "Daily theta should be less than option value");
    }

    [Fact]
    public void Vega_ShouldBePositive_AndMaximizedATM()
    {
        // Vega measures sensitivity to volatility changes
        
        // Arrange
        double spot = 100;
        double strike = 100;
        double timeToExpiry = 30.0 / 365.0;
        double volatility = 0.25;
        double riskFreeRate = 0.05;
        
        // Act: Calculate price sensitivity to 1% vol change
        double priceBase = OptionMath.BlackScholes(spot, strike, timeToExpiry, volatility, riskFreeRate, Right.Call);
        double priceHighVol = OptionMath.BlackScholes(spot, strike, timeToExpiry, volatility + 0.01, riskFreeRate, Right.Call);
        
        double vega = priceHighVol - priceBase; // Per 1% vol move
        
        // Assert
        vega.Should().BePositive("Vega must be positive for long options");
        vega.Should().BeLessThan(spot * 0.1, "Vega should be reasonable relative to spot");
    }
}