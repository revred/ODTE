using FluentAssertions;
using ODTE.Backtest.Config;
using ODTE.Backtest.Core;
using ODTE.Backtest.Engine;
using Xunit;

namespace ODTE.Backtest.Tests.Engine;

public class RiskManagerDebugTest
{
    [Fact]
    public void Debug_DailyLossLimitLogic()
    {
        // Arrange
        var config = new SimConfig
        {
            Risk = new RiskCfg
            {
                DailyLossStop = 500.0,
                PerTradeMaxLossCap = 200.0,
                MaxConcurrentPerSide = 2
            },
            NoNewRiskMinutesToClose = 40
        };
        
        var riskManager = new RiskManager(config);
        var testTime = new DateTime(2024, 2, 1, 11, 0, 0);

        // Act - Simulate losses as in the failing test
        Console.WriteLine($"Initial state:");
        var canAddInitial = riskManager.CanAdd(testTime, Decision.SingleSidePut);
        Console.WriteLine($"CanAdd before losses: {canAddInitial}");

        riskManager.RegisterClose(Decision.SingleSidePut, -300.0);
        Console.WriteLine($"After -300 loss:");
        var canAddAfterFirst = riskManager.CanAdd(testTime, Decision.SingleSidePut);
        Console.WriteLine($"CanAdd after first loss: {canAddAfterFirst}");

        riskManager.RegisterClose(Decision.SingleSideCall, -250.0);
        Console.WriteLine($"After -250 loss (total -550):");
        var canAddAfterSecond = riskManager.CanAdd(testTime, Decision.SingleSidePut);
        Console.WriteLine($"CanAdd after second loss: {canAddAfterSecond}");
        
        Console.WriteLine($"Expected: false (should block), Actual: {canAddAfterSecond}");

        // Assert
        canAddAfterSecond.Should().BeFalse();
    }
}