using FluentAssertions;
using ODTE.Backtest.Config;
using ODTE.Backtest.Core;
using ODTE.Backtest.Engine;
using Xunit;

namespace ODTE.Backtest.Tests.Engine;

/// <summary>
/// Comprehensive tests for RiskManager portfolio-level risk controls.
/// Tests daily loss limits, position limits, time windows, and risk state management.
/// </summary>
public class RiskManagerTests
{
    private readonly SimConfig _config;
    private readonly RiskManager _riskManager;
    private readonly DateTime _testTime = new(2024, 2, 1, 11, 0, 0); // 11:00 AM

    public RiskManagerTests()
    {
        _config = new SimConfig
        {
            Risk = new RiskCfg
            {
                DailyLossStop = 500.0,
                PerTradeMaxLossCap = 200.0,
                MaxConcurrentPerSide = 2
            },
            NoNewRiskMinutesToClose = 40
        };
        
        _riskManager = new RiskManager(_config);
    }

    [Fact]
    public void Constructor_ValidConfig_ShouldCreateRiskManager()
    {
        // Arrange & Act
        var manager = new RiskManager(_config);

        // Assert
        manager.Should().NotBeNull();
    }

    [Theory]
    [InlineData(Decision.SingleSidePut)]
    [InlineData(Decision.SingleSideCall)]
    [InlineData(Decision.Condor)]
    public void CanAdd_InitialState_ShouldAllowAllDecisions(Decision decision)
    {
        // Arrange & Act
        var canAdd = _riskManager.CanAdd(_testTime, decision);

        // Assert
        canAdd.Should().BeTrue();
    }

    [Fact]
    public void CanAdd_NoGo_ShouldAlwaysReturnTrue()
    {
        // Arrange & Act
        var canAdd = _riskManager.CanAdd(_testTime, Decision.NoGo);

        // Assert
        canAdd.Should().BeTrue(); // NoGo should always be allowed (it means don't trade)
    }

    [Fact]
    public void CanAdd_DailyLossLimitExceeded_ShouldBlockNewPositions()
    {
        // Arrange - Simulate daily loss exceeding limit
        _riskManager.RegisterClose(Decision.SingleSidePut, -300.0); // -$300
        _riskManager.RegisterClose(Decision.SingleSideCall, -250.0); // -$250 (total -$550 > $500 limit)

        // Act
        var canAdd = _riskManager.CanAdd(_testTime, Decision.SingleSidePut);

        // Assert
        canAdd.Should().BeFalse();
    }

    [Fact]
    public void CanAdd_DailyLossAtLimit_ShouldBlockNewPositions()
    {
        // Arrange - Simulate daily loss exactly at limit
        _riskManager.RegisterClose(Decision.SingleSidePut, -500.0); // Exactly at $500 limit

        // Act
        var canAdd = _riskManager.CanAdd(_testTime, Decision.SingleSideCall);

        // Assert
        canAdd.Should().BeFalse();
    }

    [Fact]
    public void CanAdd_DailyLossBelowLimit_ShouldAllowNewPositions()
    {
        // Arrange - Simulate daily loss below limit
        _riskManager.RegisterClose(Decision.SingleSidePut, -400.0); // Below $500 limit

        // Act
        var canAdd = _riskManager.CanAdd(_testTime, Decision.SingleSideCall);

        // Assert
        canAdd.Should().BeTrue();
    }

    [Fact]
    public void CanAdd_ProfitableTrades_ShouldNotBlockPositions()
    {
        // Arrange - Simulate profitable trades
        _riskManager.RegisterClose(Decision.SingleSidePut, 100.0);  // +$100
        _riskManager.RegisterClose(Decision.SingleSideCall, 150.0); // +$150

        // Act
        var canAdd = _riskManager.CanAdd(_testTime, Decision.Condor);

        // Assert
        canAdd.Should().BeTrue();
    }

    [Fact]
    public void CanAdd_MixedTradesWithinLimit_ShouldAllowNewPositions()
    {
        // Arrange - Mix of wins and losses within daily limit
        _riskManager.RegisterClose(Decision.SingleSidePut, -200.0); // -$200
        _riskManager.RegisterClose(Decision.SingleSideCall, 150.0); // +$150
        _riskManager.RegisterClose(Decision.Condor, -100.0);        // -$100
        // Net: -$150, within $500 limit

        // Act
        var canAdd = _riskManager.CanAdd(_testTime, Decision.SingleSidePut);

        // Assert
        canAdd.Should().BeTrue();
    }

    [Theory]
    [InlineData(15, 30, 0)] // 3:30 PM - 30 minutes to close (within 40 min limit)
    [InlineData(15, 25, 0)] // 3:25 PM - 35 minutes to close (within 40 min limit)  
    [InlineData(15, 20, 0)] // 3:20 PM - 40 minutes to close (exactly at limit)
    [InlineData(15, 15, 0)] // 3:15 PM - 45 minutes to close (outside limit)
    public void CanAdd_TimeToCloseWindow_ShouldBlockWhenTooClose(int hour, int minute, int second)
    {
        // Arrange
        var testTime = new DateTime(2024, 2, 1, hour, minute, second);
        var minutesToClose = (16 * 60) - (hour * 60 + minute); // 4:00 PM close

        // Act
        var canAdd = _riskManager.CanAdd(testTime, Decision.SingleSidePut);

        // Assert
        if (minutesToClose < _config.NoNewRiskMinutesToClose)
        {
            canAdd.Should().BeFalse();
        }
        else
        {
            canAdd.Should().BeTrue();
        }
    }

    [Fact]
    public void CanAdd_MaxConcurrentPutPositions_ShouldBlockWhenLimitReached()
    {
        // Arrange - Register maximum concurrent put positions
        _riskManager.RegisterOpen(Decision.SingleSidePut);
        _riskManager.RegisterOpen(Decision.SingleSidePut);
        // Now at limit of 2 concurrent put positions

        // Act
        var canAddPut = _riskManager.CanAdd(_testTime, Decision.SingleSidePut);
        var canAddCall = _riskManager.CanAdd(_testTime, Decision.SingleSideCall); // Calls should still be allowed

        // Assert
        canAddPut.Should().BeFalse();
        canAddCall.Should().BeTrue();
    }

    [Fact]
    public void CanAdd_MaxConcurrentCallPositions_ShouldBlockWhenLimitReached()
    {
        // Arrange - Register maximum concurrent call positions
        _riskManager.RegisterOpen(Decision.SingleSideCall);
        _riskManager.RegisterOpen(Decision.SingleSideCall);
        // Now at limit of 2 concurrent call positions

        // Act
        var canAddCall = _riskManager.CanAdd(_testTime, Decision.SingleSideCall);
        var canAddPut = _riskManager.CanAdd(_testTime, Decision.SingleSidePut); // Puts should still be allowed

        // Assert
        canAddCall.Should().BeFalse();
        canAddPut.Should().BeTrue();
    }

    [Fact]
    public void CanAdd_CondorWithBothSidesAtLimit_ShouldBlockCondor()
    {
        // Arrange - Both put and call sides at limit
        _riskManager.RegisterOpen(Decision.SingleSidePut);
        _riskManager.RegisterOpen(Decision.SingleSidePut);
        _riskManager.RegisterOpen(Decision.SingleSideCall);
        _riskManager.RegisterOpen(Decision.SingleSideCall);

        // Act
        var canAddCondor = _riskManager.CanAdd(_testTime, Decision.Condor);

        // Assert
        canAddCondor.Should().BeFalse(); // Condor needs both put and call sides
    }

    [Fact]
    public void CanAdd_CondorWithOneSideAtLimit_ShouldBlockCondor()
    {
        // Arrange - Only put side at limit
        _riskManager.RegisterOpen(Decision.SingleSidePut);
        _riskManager.RegisterOpen(Decision.SingleSidePut);
        // Call side has room

        // Act
        var canAddCondor = _riskManager.CanAdd(_testTime, Decision.Condor);

        // Assert
        canAddCondor.Should().BeFalse(); // Condor still blocked because put side is full
    }

    [Fact]
    public void RegisterOpen_SingleSidePut_ShouldIncrementPutCounter()
    {
        // Arrange & Act
        _riskManager.RegisterOpen(Decision.SingleSidePut);

        // Act - Try to add more puts until limit
        var canAdd1 = _riskManager.CanAdd(_testTime, Decision.SingleSidePut);
        _riskManager.RegisterOpen(Decision.SingleSidePut);
        var canAdd2 = _riskManager.CanAdd(_testTime, Decision.SingleSidePut);

        // Assert
        canAdd1.Should().BeTrue();  // Should allow second put
        canAdd2.Should().BeFalse(); // Should block third put (limit is 2)
    }

    [Fact]
    public void RegisterOpen_Condor_ShouldIncrementBothCounters()
    {
        // Arrange & Act
        _riskManager.RegisterOpen(Decision.Condor);

        // Act - Try to add more positions
        var canAddPut = _riskManager.CanAdd(_testTime, Decision.SingleSidePut);
        var canAddCall = _riskManager.CanAdd(_testTime, Decision.SingleSideCall);
        var canAddCondor = _riskManager.CanAdd(_testTime, Decision.Condor);

        // Assert
        canAddPut.Should().BeTrue();   // One put slot still available (condor used 1)
        canAddCall.Should().BeTrue();  // One call slot still available (condor used 1)
        canAddCondor.Should().BeTrue(); // One more condor still possible
    }

    [Fact]
    public void RegisterClose_ShouldDecrementCountersAndUpdatePnL()
    {
        // Arrange - Open positions
        _riskManager.RegisterOpen(Decision.SingleSidePut);
        _riskManager.RegisterOpen(Decision.SingleSideCall);

        // Verify positions are blocking new ones
        _riskManager.RegisterOpen(Decision.SingleSidePut); // Second put
        var shouldBlockPut = _riskManager.CanAdd(_testTime, Decision.SingleSidePut);
        shouldBlockPut.Should().BeFalse();

        // Act - Close one put position
        _riskManager.RegisterClose(Decision.SingleSidePut, -150.0);

        // Assert
        var canAddPutAgain = _riskManager.CanAdd(_testTime, Decision.SingleSidePut);
        canAddPutAgain.Should().BeTrue(); // Should free up put slot

        // Register a large loss to verify P&L tracking
        _riskManager.RegisterClose(Decision.SingleSideCall, -400.0);
        var shouldBlockAfterLoss = _riskManager.CanAdd(_testTime, Decision.SingleSideCall);
        shouldBlockAfterLoss.Should().BeFalse(); // Total loss $550 > $500 limit
    }

    [Fact]
    public void RegisterClose_Condor_ShouldDecrementBothCounters()
    {
        // Arrange - Open two condors to reach limits
        _riskManager.RegisterOpen(Decision.Condor);
        _riskManager.RegisterOpen(Decision.Condor);

        // Verify both sides are at limit
        _riskManager.CanAdd(_testTime, Decision.SingleSidePut).Should().BeFalse();
        _riskManager.CanAdd(_testTime, Decision.SingleSideCall).Should().BeFalse();

        // Act - Close one condor
        _riskManager.RegisterClose(Decision.Condor, 50.0);

        // Assert
        _riskManager.CanAdd(_testTime, Decision.SingleSidePut).Should().BeTrue();
        _riskManager.CanAdd(_testTime, Decision.SingleSideCall).Should().BeTrue();
        _riskManager.CanAdd(_testTime, Decision.Condor).Should().BeTrue();
    }

    [Fact]
    public void DailyReset_NewDay_ShouldResetAllCounters()
    {
        // Arrange - Max out positions and losses on day 1
        var day1 = new DateTime(2024, 2, 1, 11, 0, 0);
        _riskManager.RegisterOpen(Decision.SingleSidePut);
        _riskManager.RegisterOpen(Decision.SingleSidePut);
        _riskManager.RegisterClose(Decision.SingleSidePut, -500.0); // Hit daily loss limit

        // Verify day 1 restrictions
        _riskManager.CanAdd(day1, Decision.SingleSideCall).Should().BeFalse(); // Daily loss limit hit

        // Act - Move to day 2
        var day2 = new DateTime(2024, 2, 2, 11, 0, 0);
        var canAddDay2 = _riskManager.CanAdd(day2, Decision.SingleSidePut);

        // Assert
        canAddDay2.Should().BeTrue(); // Should reset on new day
    }

    [Fact]
    public void RegisterClose_DefensiveProgramming_ShouldNotGoNegative()
    {
        // Arrange - Try to close more positions than were opened
        // This simulates a bug where closes aren't matched with opens

        // Act - Close positions without opening them first
        _riskManager.RegisterClose(Decision.SingleSidePut, 100.0);
        _riskManager.RegisterClose(Decision.SingleSideCall, 100.0);

        // Assert - Should not crash and should still allow new positions
        var canAdd = _riskManager.CanAdd(_testTime, Decision.SingleSidePut);
        canAdd.Should().BeTrue();
    }

    [Theory]
    [InlineData(1)]   // Max 1 position per side
    [InlineData(3)]   // Max 3 positions per side
    [InlineData(5)]   // Max 5 positions per side
    public void CanAdd_DifferentConcurrencyLimits_ShouldRespectConfiguration(int maxConcurrent)
    {
        // Arrange
        _config.Risk.MaxConcurrentPerSide = maxConcurrent;

        // Act & Assert - Add positions up to the limit
        for (int i = 0; i < maxConcurrent; i++)
        {
            var canAdd = _riskManager.CanAdd(_testTime, Decision.SingleSidePut);
            canAdd.Should().BeTrue($"Should allow position {i + 1} of {maxConcurrent}");
            _riskManager.RegisterOpen(Decision.SingleSidePut);
        }

        // Should block the next one
        var shouldBlock = _riskManager.CanAdd(_testTime, Decision.SingleSidePut);
        shouldBlock.Should().BeFalse("Should block position beyond limit");
    }

    [Theory]
    [InlineData(100.0)]  // $100 daily loss limit
    [InlineData(1000.0)] // $1000 daily loss limit
    [InlineData(50.0)]   // $50 daily loss limit (very tight)
    public void CanAdd_DifferentDailyLossLimits_ShouldRespectConfiguration(double dailyLossLimit)
    {
        // Arrange
        _config.Risk.DailyLossStop = dailyLossLimit;

        // Act - Register loss exactly at limit
        _riskManager.RegisterClose(Decision.SingleSidePut, -dailyLossLimit);
        var canAddAtLimit = _riskManager.CanAdd(_testTime, Decision.SingleSideCall);

        // Register loss beyond limit
        _riskManager.RegisterClose(Decision.SingleSideCall, -10.0); // $10 beyond limit
        var canAddBeyondLimit = _riskManager.CanAdd(_testTime, Decision.Condor);

        // Assert
        canAddAtLimit.Should().BeFalse("Should block at daily loss limit");
        canAddBeyondLimit.Should().BeFalse("Should block beyond daily loss limit");
    }

    [Fact]
    public void MultipleConstraints_ShouldEnforceAllConstraints()
    {
        // Arrange - Set up scenario with multiple constraints
        var lateTime = new DateTime(2024, 2, 1, 15, 25, 0); // 35 minutes to close (within gamma hour)
        
        _riskManager.RegisterOpen(Decision.SingleSidePut);
        _riskManager.RegisterOpen(Decision.SingleSidePut); // Max put positions
        _riskManager.RegisterClose(Decision.SingleSideCall, -400.0); // Partial daily loss

        // Act & Assert
        var canAddPutLate = _riskManager.CanAdd(lateTime, Decision.SingleSidePut);
        var canAddCallEarly = _riskManager.CanAdd(_testTime, Decision.SingleSideCall);
        var canAddCallLate = _riskManager.CanAdd(lateTime, Decision.SingleSideCall);

        canAddPutLate.Should().BeFalse("Should block - multiple constraints (time + position limit)");
        canAddCallEarly.Should().BeTrue("Should allow call early - only partial loss constraint");
        canAddCallLate.Should().BeFalse("Should block call late - time constraint");
    }
}