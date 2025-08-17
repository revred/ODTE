using FluentAssertions;
using ODTE.Backtest.Config;
using ODTE.Backtest.Core;
using ODTE.Backtest.Engine;
using Xunit;

namespace ODTE.Backtest.Tests.Engine;

/// <summary>
/// Comprehensive tests for ExecutionEngine trade execution modeling.
/// Tests entry execution, exit conditions, slippage modeling, and realistic fill prices.
/// </summary>
public class ExecutionEngineTests
{
    private readonly SimConfig _config;
    private readonly ExecutionEngine _executionEngine;
    private readonly DateTime _testTime = new(2024, 2, 1, 10, 0, 0);
    private readonly DateOnly _testExpiry = DateOnly.FromDateTime(new DateTime(2024, 2, 1));

    public ExecutionEngineTests()
    {
        _config = new SimConfig
        {
            Slippage = new SlippageCfg
            {
                TickValue = 0.05,
                EntryHalfSpreadTicks = 0.5,
                ExitHalfSpreadTicks = 0.5
            },
            Stops = new StopsCfg
            {
                CreditMultiple = 2.2,
                DeltaBreach = 0.33
            }
        };

        _executionEngine = new ExecutionEngine(_config);
    }

    [Fact]
    public void Constructor_ValidConfig_ShouldCreateExecutionEngine()
    {
        // Arrange & Act
        var engine = new ExecutionEngine(_config);

        // Assert
        engine.Should().NotBeNull();
    }

    [Theory]
    [InlineData(1.00)] // $1.00 credit
    [InlineData(0.50)] // $0.50 credit
    [InlineData(0.25)] // $0.25 credit
    [InlineData(0.10)] // $0.10 credit
    public void TryEnter_ValidSpreadOrder_ShouldApplySlippage(double credit)
    {
        // Arrange
        var spreadOrder = CreateTestSpreadOrder(credit);

        // Act
        var result = _executionEngine.TryEnter(spreadOrder);

        // Assert
        result.Should().NotBeNull();
        result!.Order.Should().Be(spreadOrder);
        result.EntryTs.Should().Be(_testTime);

        // Entry price should be credit minus slippage (0.5 ticks * $0.05 = $0.025)
        var expectedEntryPrice = Math.Max(0.05, credit - 0.025);
        result.EntryPrice.Should().BeApproximately(expectedEntryPrice, 0.001);
    }

    [Fact]
    public void TryEnter_VeryLowCredit_ShouldRespectMinimumPrice()
    {
        // Arrange - Credit so low that after slippage it would be negative
        var spreadOrder = CreateTestSpreadOrder(0.02); // $0.02 credit

        // Act
        var result = _executionEngine.TryEnter(spreadOrder);

        // Assert
        result.Should().NotBeNull();
        result!.EntryPrice.Should().Be(0.05); // Minimum tick value
    }

    [Theory]
    [InlineData(0.5, 1.0, 2.0)] // Entry 0.5, spread value 1.0, stop multiple 2.0 -> should exit
    [InlineData(0.5, 2.0, 2.2)] // Entry 0.5, spread value 2.0, stop multiple 2.2 -> should exit (2.0 > 1.1)
    [InlineData(0.5, 1.0, 2.2)] // Entry 0.5, spread value 1.0, stop multiple 2.2 -> should NOT exit (1.0 < 1.1)
    public void ShouldExit_PriceBasedStop_ShouldTriggerCorrectly(double entryPrice, double currentSpreadValue, double stopMultiple)
    {
        // Arrange
        _config.Stops.CreditMultiple = stopMultiple;
        var position = CreateTestPosition(entryPrice);
        var shortStrikeDelta = 0.20; // Below delta breach threshold

        // Act
        var (shouldExit, exitPrice, reason) = _executionEngine.ShouldExit(
            position, currentSpreadValue, shortStrikeDelta, _testTime);

        // Assert
        var stopLevel = entryPrice * stopMultiple;

        if (currentSpreadValue >= stopLevel)
        {
            shouldExit.Should().BeTrue();
            reason.Should().Contain($"Stop credit x{stopMultiple}");
            // Exit price should include slippage
            exitPrice.Should().BeApproximately(currentSpreadValue + 0.025, 0.001);
        }
        else
        {
            shouldExit.Should().BeFalse();
            reason.Should().BeEmpty();
            exitPrice.Should().Be(0);
        }
    }

    [Theory]
    [InlineData(0.20)] // Below threshold
    [InlineData(0.32)] // Just below threshold
    [InlineData(0.33)] // At threshold - should trigger
    [InlineData(0.40)] // Above threshold - should trigger
    [InlineData(-0.35)] // Negative delta (put) above threshold
    public void ShouldExit_DeltaBasedStop_ShouldTriggerCorrectly(double shortStrikeDelta)
    {
        // Arrange
        var position = CreateTestPosition(0.50);
        var currentSpreadValue = 0.80; // Below price stop threshold
        var deltaThreshold = _config.Stops.DeltaBreach;

        // Act
        var (shouldExit, exitPrice, reason) = _executionEngine.ShouldExit(
            position, currentSpreadValue, shortStrikeDelta, _testTime);

        // Assert
        if (Math.Abs(shortStrikeDelta) >= deltaThreshold)
        {
            shouldExit.Should().BeTrue();
            reason.Should().Contain($"Delta>{deltaThreshold}");
            exitPrice.Should().BeApproximately(currentSpreadValue + 0.025, 0.001);
        }
        else
        {
            shouldExit.Should().BeFalse();
            reason.Should().BeEmpty();
            exitPrice.Should().Be(0);
        }
    }

    [Fact]
    public void ShouldExit_BothStopsTriggered_ShouldTriggerOnFirst()
    {
        // Arrange - Both price and delta stops would trigger
        var position = CreateTestPosition(0.50);
        var currentSpreadValue = 2.00; // Above price stop (0.50 * 2.2 = 1.1)
        var shortStrikeDelta = 0.40;   // Above delta stop (0.33)

        // Act
        var (shouldExit, exitPrice, reason) = _executionEngine.ShouldExit(
            position, currentSpreadValue, shortStrikeDelta, _testTime);

        // Assert
        shouldExit.Should().BeTrue();
        // Price stop is checked first, so should be the trigger
        reason.Should().Contain("Stop credit");
        exitPrice.Should().BeApproximately(currentSpreadValue + 0.025, 0.001);
    }

    [Fact]
    public void ShouldExit_NoStopsTriggered_ShouldNotExit()
    {
        // Arrange - Neither stop triggered
        var position = CreateTestPosition(0.50);
        var currentSpreadValue = 1.00; // Below price stop (0.50 * 2.2 = 1.1)
        var shortStrikeDelta = 0.25;   // Below delta stop (0.33)

        // Act
        var (shouldExit, exitPrice, reason) = _executionEngine.ShouldExit(
            position, currentSpreadValue, shortStrikeDelta, _testTime);

        // Assert
        shouldExit.Should().BeFalse();
        exitPrice.Should().Be(0);
        reason.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0.5)] // 0.5 tick slippage
    [InlineData(1.0)] // 1.0 tick slippage  
    [InlineData(0.25)] // 0.25 tick slippage
    public void ShouldExit_ExitSlippage_ShouldBeAppliedCorrectly(double exitHalfSpreadTicks)
    {
        // Arrange
        _config.Slippage.ExitHalfSpreadTicks = exitHalfSpreadTicks;
        var position = CreateTestPosition(0.50);
        var currentSpreadValue = 2.00; // Trigger price stop
        var shortStrikeDelta = 0.20;

        // Act
        var (shouldExit, exitPrice, reason) = _executionEngine.ShouldExit(
            position, currentSpreadValue, shortStrikeDelta, _testTime);

        // Assert
        shouldExit.Should().BeTrue();
        var expectedExitPrice = currentSpreadValue + (exitHalfSpreadTicks * _config.Slippage.TickValue);
        exitPrice.Should().BeApproximately(expectedExitPrice, 0.001);
    }

    [Theory]
    [InlineData(1.5)] // Tighter stop
    [InlineData(3.0)] // Looser stop
    [InlineData(2.2)] // Default stop
    public void ShouldExit_DifferentStopMultiples_ShouldAdjustThreshold(double stopMultiple)
    {
        // Arrange
        _config.Stops.CreditMultiple = stopMultiple;
        var entryPrice = 0.50;
        var position = CreateTestPosition(entryPrice);
        var testSpreadValue = entryPrice * stopMultiple; // Exactly at threshold
        var shortStrikeDelta = 0.20;

        // Act
        var (shouldExit, exitPrice, reason) = _executionEngine.ShouldExit(
            position, testSpreadValue, shortStrikeDelta, _testTime);

        // Assert
        shouldExit.Should().BeTrue();
        reason.Should().Contain($"Stop credit x{stopMultiple}");
    }

    [Theory]
    [InlineData(0.25)] // Tighter delta stop
    [InlineData(0.40)] // Looser delta stop
    [InlineData(0.50)] // Very loose delta stop
    public void ShouldExit_DifferentDeltaThresholds_ShouldAdjustThreshold(double deltaBreach)
    {
        // Arrange
        _config.Stops.DeltaBreach = deltaBreach;
        var position = CreateTestPosition(0.50);
        var currentSpreadValue = 0.80; // Below price stop
        var testDelta = deltaBreach; // Exactly at threshold

        // Act
        var (shouldExit, exitPrice, reason) = _executionEngine.ShouldExit(
            position, currentSpreadValue, testDelta, _testTime);

        // Assert
        shouldExit.Should().BeTrue();
        reason.Should().Contain($"Delta>{deltaBreach}");
    }

    [Fact]
    public void TryEnter_SlippageConfiguration_ShouldRespectConfig()
    {
        // Arrange
        _config.Slippage.EntryHalfSpreadTicks = 1.0; // Higher slippage
        _config.Slippage.TickValue = 0.10; // Larger tick size

        var spreadOrder = CreateTestSpreadOrder(1.00);

        // Act
        var result = _executionEngine.TryEnter(spreadOrder);

        // Assert
        result.Should().NotBeNull();
        // Expected: 1.00 - (1.0 * 0.10) = 0.90
        result!.EntryPrice.Should().BeApproximately(0.90, 0.001);
    }

    [Fact]
    public void ShouldExit_ZeroCurrentSpreadValue_ShouldHandleCorrectly()
    {
        // Arrange
        var position = CreateTestPosition(0.50);
        var currentSpreadValue = 0.0; // Spread has no value
        var shortStrikeDelta = 0.20;

        // Act
        var (shouldExit, exitPrice, reason) = _executionEngine.ShouldExit(
            position, currentSpreadValue, shortStrikeDelta, _testTime);

        // Assert
        shouldExit.Should().BeFalse(); // No stops should trigger with 0 spread value
        exitPrice.Should().Be(0);
        reason.Should().BeEmpty();
    }

    [Fact]
    public void ShouldExit_ExtremeSpreadValue_ShouldTriggerPriceStop()
    {
        // Arrange
        var position = CreateTestPosition(0.10);
        var currentSpreadValue = 100.0; // Extreme spread value
        var shortStrikeDelta = 0.20;

        // Act
        var (shouldExit, exitPrice, reason) = _executionEngine.ShouldExit(
            position, currentSpreadValue, shortStrikeDelta, _testTime);

        // Assert
        shouldExit.Should().BeTrue();
        reason.Should().Contain("Stop credit");
        exitPrice.Should().BeApproximately(100.025, 0.001); // With slippage
    }

    private SpreadOrder CreateTestSpreadOrder(double credit)
    {
        var shortLeg = new SpreadLeg(_testExpiry, 100.0, Right.Put, -1);
        var longLeg = new SpreadLeg(_testExpiry, 99.0, Right.Put, 1);

        return new SpreadOrder(
            _testTime,
            "XSP",
            credit,
            1.0, // width
            credit / 1.0, // credit per width
            Decision.SingleSidePut,
            shortLeg,
            longLeg);
    }

    private OpenPosition CreateTestPosition(double entryPrice)
    {
        var spreadOrder = CreateTestSpreadOrder(1.0); // Credit doesn't matter for position
        return new OpenPosition(spreadOrder, entryPrice, _testTime);
    }
}