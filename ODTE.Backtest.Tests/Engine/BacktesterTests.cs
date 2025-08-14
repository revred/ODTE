using FluentAssertions;
using Moq;
using ODTE.Backtest.Config;
using ODTE.Backtest.Core;
using ODTE.Backtest.Data;
using ODTE.Backtest.Engine;
using ODTE.Backtest.Signals;
using ODTE.Backtest.Strategy;
using Xunit;

namespace ODTE.Backtest.Tests.Engine;

/// <summary>
/// Comprehensive tests for Backtester orchestration and end-to-end trading simulation.
/// Tests position lifecycle, trade execution, reporting, and component integration.
/// </summary>
public class BacktesterTests
{
    private readonly Mock<IMarketData> _mockMarketData;
    private readonly Mock<IOptionsData> _mockOptionsData;
    private readonly Mock<IEconCalendar> _mockCalendar;
    private readonly Mock<RegimeScorer> _mockScorer;
    private readonly Mock<SpreadBuilder> _mockBuilder;
    private readonly Mock<ExecutionEngine> _mockExecution;
    private readonly Mock<RiskManager> _mockRisk;
    private readonly SimConfig _config;
    private readonly Backtester _backtester;

    private readonly DateTime _startTime = new(2024, 2, 1, 9, 30, 0);
    private readonly DateTime _endTime = new(2024, 2, 1, 16, 0, 0);
    private readonly DateOnly _testExpiry = DateOnly.FromDateTime(new DateTime(2024, 2, 1));

    public BacktesterTests()
    {
        _mockMarketData = new Mock<IMarketData>();
        _mockOptionsData = new Mock<IOptionsData>();
        _mockCalendar = new Mock<IEconCalendar>();
        
        _config = new SimConfig
        {
            Start = DateOnly.FromDateTime(_startTime),
            End = DateOnly.FromDateTime(_endTime),
            CadenceSeconds = 900, // 15 minutes
            Fees = new FeesCfg
            {
                CommissionPerContract = 0.65,
                ExchangeFeesPerContract = 0.25
            }
        };

        _mockScorer = new Mock<RegimeScorer>(_config);
        _mockBuilder = new Mock<SpreadBuilder>(_config);
        _mockExecution = new Mock<ExecutionEngine>(_config);
        _mockRisk = new Mock<RiskManager>(_config);

        _backtester = new Backtester(
            _config,
            _mockMarketData.Object,
            _mockOptionsData.Object,
            _mockCalendar.Object,
            _mockScorer.Object,
            _mockBuilder.Object,
            _mockExecution.Object,
            _mockRisk.Object);
    }

    [Fact]
    public void Constructor_ValidDependencies_ShouldCreateBacktester()
    {
        // Arrange & Act
        var backtester = new Backtester(
            _config,
            _mockMarketData.Object,
            _mockOptionsData.Object,
            _mockCalendar.Object,
            _mockScorer.Object,
            _mockBuilder.Object,
            _mockExecution.Object,
            _mockRisk.Object);

        // Assert
        backtester.Should().NotBeNull();
    }

    [Fact]
    public async Task RunAsync_NoMarketData_ShouldReturnEmptyReport()
    {
        // Arrange
        _mockMarketData.Setup(x => x.GetBars(_config.Start, _config.End))
            .Returns(new List<Bar>());

        // Act
        var report = await _backtester.RunAsync();

        // Assert
        report.Should().NotBeNull();
        report.Trades.Should().BeEmpty();
        report.NetPnL.Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_SimpleScenario_ShouldProcessBasicWorkflow()
    {
        // Arrange
        var bars = CreateTestBars();
        SetupBasicMarketData(bars);

        // Act
        var report = await _backtester.RunAsync();

        // Assert
        report.Should().NotBeNull();
        // Verify basic workflow was executed
        _mockScorer.Verify(x => x.Score(It.IsAny<DateTime>(), It.IsAny<IMarketData>(), It.IsAny<IEconCalendar>()), 
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task RunAsync_ProfitableTradeScenario_ShouldRecordProfit()
    {
        // Arrange
        var bars = CreateTestBars();
        SetupProfitableTradeScenario(bars);

        // Act
        var report = await _backtester.RunAsync();

        // Assert
        report.Should().NotBeNull();
        report.Trades.Should().HaveCountGreaterThan(0);
        
        var trade = report.Trades.First();
        trade.PnL.Should().BeGreaterThan(0); // Should be profitable
        trade.Fees.Should().BeGreaterThan(0); // Should include fees
    }

    [Fact]
    public async Task RunAsync_StopLossTriggered_ShouldExitWithLoss()
    {
        // Arrange
        var bars = CreateTestBars();
        SetupStopLossScenario(bars);

        // Act
        var report = await _backtester.RunAsync();

        // Assert
        report.Should().NotBeNull();
        report.Trades.Should().HaveCountGreaterThan(0);
        
        var trade = report.Trades.First();
        trade.PnL.Should().BeLessThan(0); // Should be a loss
        trade.ExitReason.Should().Contain("Stop"); // Should exit via stop loss
    }

    [Fact]
    public async Task RunAsync_RiskBlocked_ShouldNotExecuteTrade()
    {
        // Arrange
        var bars = CreateTestBars();
        SetupRiskBlockedScenario(bars);

        // Act
        var report = await _backtester.RunAsync();

        // Assert
        report.Should().NotBeNull();
        report.Trades.Should().BeEmpty(); // No trades should execute due to risk block
        _mockRisk.Verify(x => x.CanAdd(It.IsAny<DateTime>(), It.IsAny<Decision>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RunAsync_NoGoDecision_ShouldNotExecuteTrade()
    {
        // Arrange
        var bars = CreateTestBars();
        SetupNoGoScenario(bars);

        // Act
        var report = await _backtester.RunAsync();

        // Assert
        report.Should().NotBeNull();
        report.Trades.Should().BeEmpty(); // No trades should execute for NoGo decisions
    }

    [Fact]
    public async Task RunAsync_OrderBuildFails_ShouldNotExecuteTrade()
    {
        // Arrange
        var bars = CreateTestBars();
        SetupOrderBuildFailureScenario(bars);

        // Act
        var report = await _backtester.RunAsync();

        // Assert
        report.Should().NotBeNull();
        report.Trades.Should().BeEmpty(); // No trades should execute when order build fails
        _mockBuilder.Verify(x => x.TryBuild(It.IsAny<DateTime>(), It.IsAny<Decision>(), 
            It.IsAny<IMarketData>(), It.IsAny<IOptionsData>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RunAsync_ExecutionFails_ShouldNotRecordTrade()
    {
        // Arrange
        var bars = CreateTestBars();
        SetupExecutionFailureScenario(bars);

        // Act
        var report = await _backtester.RunAsync();

        // Assert
        report.Should().NotBeNull();
        report.Trades.Should().BeEmpty(); // No trades should execute when execution fails
        _mockExecution.Verify(x => x.TryEnter(It.IsAny<SpreadOrder>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RunAsync_PMSettlement_ShouldCloseRemainingPositions()
    {
        // Arrange
        var bars = CreateTestBarsWithPMClose();
        SetupPMSettlementScenario(bars);

        // Act
        var report = await _backtester.RunAsync();

        // Assert
        report.Should().NotBeNull();
        report.Trades.Should().HaveCountGreaterThan(0);
        
        var pmTrade = report.Trades.FirstOrDefault(t => t.ExitReason.Contains("PM cash settlement"));
        pmTrade.Should().NotBeNull(); // Should have PM settlement trade
    }

    [Fact]
    public async Task RunAsync_MultiplePositions_ShouldTrackAllPositions()
    {
        // Arrange
        var bars = CreateTestBars();
        SetupMultiplePositionsScenario(bars);

        // Act
        var report = await _backtester.RunAsync();

        // Assert
        report.Should().NotBeNull();
        report.Trades.Should().HaveCount(2); // Should have multiple trades
        
        // Verify risk manager was called for each position
        _mockRisk.Verify(x => x.RegisterOpen(It.IsAny<Decision>()), Times.Exactly(2));
        _mockRisk.Verify(x => x.RegisterClose(It.IsAny<Decision>(), It.IsAny<double>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RunAsync_CadenceRespected_ShouldOnlyDecideAtIntervals()
    {
        // Arrange
        var bars = CreateFrequentBars(); // Every 5 minutes
        _config.CadenceSeconds = 900; // 15 minute cadence
        SetupBasicMarketData(bars);

        // Act
        var report = await _backtester.RunAsync();

        // Assert
        // Should only call scorer at 15-minute intervals, not every bar
        var expectedCalls = bars.Count / 3; // Every 3rd bar (5min bars, 15min cadence)
        _mockScorer.Verify(x => x.Score(It.IsAny<DateTime>(), It.IsAny<IMarketData>(), It.IsAny<IEconCalendar>()), 
            Times.AtMost(expectedCalls + 1)); // +1 for first call
    }

    [Theory]
    [InlineData(true, true, false)] // Calm range -> Condor
    [InlineData(false, true, false)] // Trend up -> Call spread  
    [InlineData(false, false, true)] // Trend down -> Put spread
    public async Task RunAsync_DifferentRegimes_ShouldChooseCorrectStrategy(bool calm, bool trendUp, bool trendDown)
    {
        // Arrange
        var bars = CreateTestBars();
        SetupSpecificRegimeScenario(bars, calm, trendUp, trendDown);

        // Act
        var report = await _backtester.RunAsync();

        // Assert
        if (calm)
        {
            _mockBuilder.Verify(x => x.TryBuild(It.IsAny<DateTime>(), Decision.Condor, 
                It.IsAny<IMarketData>(), It.IsAny<IOptionsData>()), Times.AtLeastOnce);
        }
        else if (trendUp)
        {
            _mockBuilder.Verify(x => x.TryBuild(It.IsAny<DateTime>(), Decision.SingleSideCall, 
                It.IsAny<IMarketData>(), It.IsAny<IOptionsData>()), Times.AtLeastOnce);
        }
        else if (trendDown)
        {
            _mockBuilder.Verify(x => x.TryBuild(It.IsAny<DateTime>(), Decision.SingleSidePut, 
                It.IsAny<IMarketData>(), It.IsAny<IOptionsData>()), Times.AtLeastOnce);
        }
    }

    [Fact]
    public async Task RunAsync_CalculateMetrics_ShouldComputePerformanceStats()
    {
        // Arrange
        var bars = CreateTestBars();
        SetupMultipleProfitableTradesScenario(bars);

        // Act
        var report = await _backtester.RunAsync();

        // Assert
        report.Should().NotBeNull();
        report.Trades.Should().HaveCountGreaterThan(1);
        
        // Check computed metrics
        report.NetPnL.Should().BeGreaterThan(0);
        report.WinCount.Should().BeGreaterThan(0);
        report.WinRate.Should().BeGreaterThan(0);
        report.Sharpe.Should().NotBe(0); // Should calculate Sharpe ratio
        report.MaxDrawdown.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task RunAsync_OnlyRegularTradingHours_ShouldFilterBars()
    {
        // Arrange
        var allHoursBars = CreateAllHoursBars(); // Include pre-market and after-hours
        _mockMarketData.Setup(x => x.GetBars(_config.Start, _config.End)).Returns(allHoursBars);
        SetupBasicRegimeScoring();

        // Act
        var report = await _backtester.RunAsync();

        // Assert - Should only process bars within regular trading hours (9:30 AM - 4:00 PM ET)
        _mockScorer.Verify(x => x.Score(
            It.Is<DateTime>(dt => dt.TimeOfDay >= new TimeSpan(14, 30, 0) && dt.TimeOfDay <= new TimeSpan(21, 0, 0)),
            It.IsAny<IMarketData>(), It.IsAny<IEconCalendar>()), Times.AtLeastOnce);
    }

    private List<Bar> CreateTestBars()
    {
        var bars = new List<Bar>();
        for (int i = 0; i < 12; i++) // 3 hours of 15-minute bars
        {
            var time = _startTime.AddMinutes(i * 15);
            bars.Add(new Bar(time, 100.0, 100.5, 99.5, 100.0 + i * 0.1, 10000));
        }
        return bars;
    }

    private List<Bar> CreateTestBarsWithPMClose()
    {
        var bars = CreateTestBars();
        // Add PM close bar
        bars.Add(new Bar(new DateTime(2024, 2, 1, 21, 0, 0), 101.0, 101.0, 101.0, 101.0, 5000));
        return bars;
    }

    private List<Bar> CreateFrequentBars()
    {
        var bars = new List<Bar>();
        for (int i = 0; i < 36; i++) // 3 hours of 5-minute bars
        {
            var time = _startTime.AddMinutes(i * 5);
            bars.Add(new Bar(time, 100.0, 100.5, 99.5, 100.0, 10000));
        }
        return bars;
    }

    private List<Bar> CreateAllHoursBars()
    {
        var bars = new List<Bar>();
        var startTime = new DateTime(2024, 2, 1, 4, 0, 0); // Pre-market start
        
        for (int i = 0; i < 64; i++) // 4 AM to 8 PM (16 hours of 15-minute bars)
        {
            var time = startTime.AddMinutes(i * 15);
            bars.Add(new Bar(time, 100.0, 100.5, 99.5, 100.0, 10000));
        }
        return bars;
    }

    private void SetupBasicMarketData(List<Bar> bars)
    {
        _mockMarketData.Setup(x => x.GetBars(_config.Start, _config.End)).Returns(bars);
        _mockOptionsData.Setup(x => x.TodayExpiry(It.IsAny<DateTime>())).Returns(_testExpiry);
        _mockOptionsData.Setup(x => x.GetQuotesAt(It.IsAny<DateTime>())).Returns(new List<OptionQuote>());
        SetupBasicRegimeScoring();
    }

    private void SetupBasicRegimeScoring()
    {
        _mockScorer.Setup(x => x.Score(It.IsAny<DateTime>(), It.IsAny<IMarketData>(), It.IsAny<IEconCalendar>()))
            .Returns((-2, false, false, false)); // NoGo scenario by default
    }

    private void SetupProfitableTradeScenario(List<Bar> bars)
    {
        SetupBasicMarketData(bars);
        
        // Setup profitable trade flow
        _mockScorer.Setup(x => x.Score(It.IsAny<DateTime>(), It.IsAny<IMarketData>(), It.IsAny<IEconCalendar>()))
            .Returns((3, true, false, false)); // Favorable score for condor
        
        _mockRisk.Setup(x => x.CanAdd(It.IsAny<DateTime>(), It.IsAny<Decision>())).Returns(true);
        
        var testOrder = CreateTestSpreadOrder();
        _mockBuilder.Setup(x => x.TryBuild(It.IsAny<DateTime>(), It.IsAny<Decision>(), 
            It.IsAny<IMarketData>(), It.IsAny<IOptionsData>())).Returns(testOrder);
        
        var testPosition = CreateTestPosition();
        _mockExecution.Setup(x => x.TryEnter(It.IsAny<SpreadOrder>())).Returns(testPosition);
        
        // Setup profitable exit
        _mockExecution.Setup(x => x.ShouldExit(It.IsAny<OpenPosition>(), It.IsAny<double>(), 
            It.IsAny<double>(), It.IsAny<DateTime>()))
            .Returns((true, 0.10, "Target profit")); // Exit at lower price = profit for credit spread
        
        // Add quotes for position tracking
        var quotes = new List<OptionQuote>
        {
            new(_startTime, _testExpiry, 100.0, Right.Put, 0.80, 0.90, 0.85, -0.15, 0.20),
            new(_startTime, _testExpiry, 99.0, Right.Put, 0.20, 0.30, 0.25, -0.12, 0.20)
        };
        _mockOptionsData.Setup(x => x.GetQuotesAt(It.IsAny<DateTime>())).Returns(quotes);
    }

    private void SetupStopLossScenario(List<Bar> bars)
    {
        SetupBasicMarketData(bars);
        
        _mockScorer.Setup(x => x.Score(It.IsAny<DateTime>(), It.IsAny<IMarketData>(), It.IsAny<IEconCalendar>()))
            .Returns((3, true, false, false));
        
        _mockRisk.Setup(x => x.CanAdd(It.IsAny<DateTime>(), It.IsAny<Decision>())).Returns(true);
        
        var testOrder = CreateTestSpreadOrder();
        _mockBuilder.Setup(x => x.TryBuild(It.IsAny<DateTime>(), It.IsAny<Decision>(), 
            It.IsAny<IMarketData>(), It.IsAny<IOptionsData>())).Returns(testOrder);
        
        var testPosition = CreateTestPosition();
        _mockExecution.Setup(x => x.TryEnter(It.IsAny<SpreadOrder>())).Returns(testPosition);
        
        // Setup stop loss exit
        _mockExecution.Setup(x => x.ShouldExit(It.IsAny<OpenPosition>(), It.IsAny<double>(), 
            It.IsAny<double>(), It.IsAny<DateTime>()))
            .Returns((true, 1.50, "Stop credit x2.2"));
        
        // Add quotes
        var quotes = new List<OptionQuote>
        {
            new(_startTime, _testExpiry, 100.0, Right.Put, 0.80, 0.90, 0.85, -0.15, 0.20),
            new(_startTime, _testExpiry, 99.0, Right.Put, 0.20, 0.30, 0.25, -0.12, 0.20)
        };
        _mockOptionsData.Setup(x => x.GetQuotesAt(It.IsAny<DateTime>())).Returns(quotes);
    }

    private void SetupRiskBlockedScenario(List<Bar> bars)
    {
        SetupBasicMarketData(bars);
        
        _mockScorer.Setup(x => x.Score(It.IsAny<DateTime>(), It.IsAny<IMarketData>(), It.IsAny<IEconCalendar>()))
            .Returns((3, true, false, false)); // Favorable score
        
        _mockRisk.Setup(x => x.CanAdd(It.IsAny<DateTime>(), It.IsAny<Decision>())).Returns(false); // Risk blocks
    }

    private void SetupNoGoScenario(List<Bar> bars)
    {
        SetupBasicMarketData(bars);
        
        _mockScorer.Setup(x => x.Score(It.IsAny<DateTime>(), It.IsAny<IMarketData>(), It.IsAny<IEconCalendar>()))
            .Returns((-2, false, false, false)); // NoGo score
    }

    private void SetupOrderBuildFailureScenario(List<Bar> bars)
    {
        SetupBasicMarketData(bars);
        
        _mockScorer.Setup(x => x.Score(It.IsAny<DateTime>(), It.IsAny<IMarketData>(), It.IsAny<IEconCalendar>()))
            .Returns((3, true, false, false));
        
        _mockRisk.Setup(x => x.CanAdd(It.IsAny<DateTime>(), It.IsAny<Decision>())).Returns(true);
        
        _mockBuilder.Setup(x => x.TryBuild(It.IsAny<DateTime>(), It.IsAny<Decision>(), 
            It.IsAny<IMarketData>(), It.IsAny<IOptionsData>())).Returns((SpreadOrder?)null); // Build fails
    }

    private void SetupExecutionFailureScenario(List<Bar> bars)
    {
        SetupBasicMarketData(bars);
        
        _mockScorer.Setup(x => x.Score(It.IsAny<DateTime>(), It.IsAny<IMarketData>(), It.IsAny<IEconCalendar>()))
            .Returns((3, true, false, false));
        
        _mockRisk.Setup(x => x.CanAdd(It.IsAny<DateTime>(), It.IsAny<Decision>())).Returns(true);
        
        var testOrder = CreateTestSpreadOrder();
        _mockBuilder.Setup(x => x.TryBuild(It.IsAny<DateTime>(), It.IsAny<Decision>(), 
            It.IsAny<IMarketData>(), It.IsAny<IOptionsData>())).Returns(testOrder);
        
        _mockExecution.Setup(x => x.TryEnter(It.IsAny<SpreadOrder>())).Returns((OpenPosition?)null); // Execution fails
    }

    private void SetupPMSettlementScenario(List<Bar> bars)
    {
        SetupBasicMarketData(bars);
        
        _mockScorer.Setup(x => x.Score(It.IsAny<DateTime>(), It.IsAny<IMarketData>(), It.IsAny<IEconCalendar>()))
            .Returns((3, true, false, false));
        
        _mockRisk.Setup(x => x.CanAdd(It.IsAny<DateTime>(), It.IsAny<Decision>())).Returns(true);
        
        var testOrder = CreateTestSpreadOrder();
        _mockBuilder.Setup(x => x.TryBuild(It.IsAny<DateTime>(), It.IsAny<Decision>(), 
            It.IsAny<IMarketData>(), It.IsAny<IOptionsData>())).Returns(testOrder);
        
        var testPosition = CreateTestPosition();
        _mockExecution.Setup(x => x.TryEnter(It.IsAny<SpreadOrder>())).Returns(testPosition);
        
        // Setup no exit before PM settlement
        _mockExecution.Setup(x => x.ShouldExit(It.IsAny<OpenPosition>(), It.IsAny<double>(), 
            It.IsAny<double>(), It.IsAny<DateTime>()))
            .Returns((false, 0, ""));
        
        // Add quotes
        var quotes = new List<OptionQuote>
        {
            new(_startTime, _testExpiry, 100.0, Right.Put, 0.80, 0.90, 0.85, -0.15, 0.20),
            new(_startTime, _testExpiry, 99.0, Right.Put, 0.20, 0.30, 0.25, -0.12, 0.20)
        };
        _mockOptionsData.Setup(x => x.GetQuotesAt(It.IsAny<DateTime>())).Returns(quotes);
    }

    private void SetupMultiplePositionsScenario(List<Bar> bars)
    {
        SetupBasicMarketData(bars);
        
        var callCount = 0;
        _mockScorer.Setup(x => x.Score(It.IsAny<DateTime>(), It.IsAny<IMarketData>(), It.IsAny<IEconCalendar>()))
            .Returns(() => 
            {
                callCount++;
                return callCount <= 2 ? (3, true, false, false) : (-2, false, false, false); // Two trade signals
            });
        
        _mockRisk.Setup(x => x.CanAdd(It.IsAny<DateTime>(), It.IsAny<Decision>())).Returns(true);
        
        var testOrder = CreateTestSpreadOrder();
        _mockBuilder.Setup(x => x.TryBuild(It.IsAny<DateTime>(), It.IsAny<Decision>(), 
            It.IsAny<IMarketData>(), It.IsAny<IOptionsData>())).Returns(testOrder);
        
        var testPosition = CreateTestPosition();
        _mockExecution.Setup(x => x.TryEnter(It.IsAny<SpreadOrder>())).Returns(testPosition);
        
        // Setup exit after a few bars
        var exitCallCount = 0;
        _mockExecution.Setup(x => x.ShouldExit(It.IsAny<OpenPosition>(), It.IsAny<double>(), 
            It.IsAny<double>(), It.IsAny<DateTime>()))
            .Returns(() =>
            {
                exitCallCount++;
                return exitCallCount > 2 ? (true, 0.20, "Profit target") : (false, 0, "");
            });
        
        // Add quotes
        var quotes = new List<OptionQuote>
        {
            new(_startTime, _testExpiry, 100.0, Right.Put, 0.80, 0.90, 0.85, -0.15, 0.20),
            new(_startTime, _testExpiry, 99.0, Right.Put, 0.20, 0.30, 0.25, -0.12, 0.20)
        };
        _mockOptionsData.Setup(x => x.GetQuotesAt(It.IsAny<DateTime>())).Returns(quotes);
    }

    private void SetupSpecificRegimeScenario(List<Bar> bars, bool calm, bool trendUp, bool trendDown)
    {
        SetupBasicMarketData(bars);
        
        _mockScorer.Setup(x => x.Score(It.IsAny<DateTime>(), It.IsAny<IMarketData>(), It.IsAny<IEconCalendar>()))
            .Returns((3, calm, trendUp, trendDown)); // High score with specific regime
        
        _mockRisk.Setup(x => x.CanAdd(It.IsAny<DateTime>(), It.IsAny<Decision>())).Returns(true);
        
        var testOrder = CreateTestSpreadOrder();
        _mockBuilder.Setup(x => x.TryBuild(It.IsAny<DateTime>(), It.IsAny<Decision>(), 
            It.IsAny<IMarketData>(), It.IsAny<IOptionsData>())).Returns(testOrder);
        
        var testPosition = CreateTestPosition();
        _mockExecution.Setup(x => x.TryEnter(It.IsAny<SpreadOrder>())).Returns(testPosition);
        
        _mockExecution.Setup(x => x.ShouldExit(It.IsAny<OpenPosition>(), It.IsAny<double>(), 
            It.IsAny<double>(), It.IsAny<DateTime>()))
            .Returns((true, 0.15, "Quick exit"));
        
        // Add quotes
        var quotes = new List<OptionQuote>
        {
            new(_startTime, _testExpiry, 100.0, Right.Put, 0.80, 0.90, 0.85, -0.15, 0.20),
            new(_startTime, _testExpiry, 99.0, Right.Put, 0.20, 0.30, 0.25, -0.12, 0.20)
        };
        _mockOptionsData.Setup(x => x.GetQuotesAt(It.IsAny<DateTime>())).Returns(quotes);
    }

    private void SetupMultipleProfitableTradesScenario(List<Bar> bars)
    {
        SetupBasicMarketData(bars);
        
        var tradeCount = 0;
        _mockScorer.Setup(x => x.Score(It.IsAny<DateTime>(), It.IsAny<IMarketData>(), It.IsAny<IEconCalendar>()))
            .Returns(() => 
            {
                tradeCount++;
                return tradeCount <= 3 ? (3, true, false, false) : (-2, false, false, false); // Three trade signals
            });
        
        _mockRisk.Setup(x => x.CanAdd(It.IsAny<DateTime>(), It.IsAny<Decision>())).Returns(true);
        
        var testOrder = CreateTestSpreadOrder();
        _mockBuilder.Setup(x => x.TryBuild(It.IsAny<DateTime>(), It.IsAny<Decision>(), 
            It.IsAny<IMarketData>(), It.IsAny<IOptionsData>())).Returns(testOrder);
        
        var testPosition = CreateTestPosition();
        _mockExecution.Setup(x => x.TryEnter(It.IsAny<SpreadOrder>())).Returns(testPosition);
        
        // Mix of profitable and losing exits
        var exitCount = 0;
        _mockExecution.Setup(x => x.ShouldExit(It.IsAny<OpenPosition>(), It.IsAny<double>(), 
            It.IsAny<double>(), It.IsAny<DateTime>()))
            .Returns(() =>
            {
                exitCount++;
                return exitCount % 2 == 1 
                    ? (true, 0.10, "Profit") // Profitable exit
                    : (true, 1.20, "Stop loss"); // Loss exit
            });
        
        // Add quotes
        var quotes = new List<OptionQuote>
        {
            new(_startTime, _testExpiry, 100.0, Right.Put, 0.80, 0.90, 0.85, -0.15, 0.20),
            new(_startTime, _testExpiry, 99.0, Right.Put, 0.20, 0.30, 0.25, -0.12, 0.20)
        };
        _mockOptionsData.Setup(x => x.GetQuotesAt(It.IsAny<DateTime>())).Returns(quotes);
    }

    private SpreadOrder CreateTestSpreadOrder()
    {
        var shortLeg = new SpreadLeg(_testExpiry, 100.0, Right.Put, -1);
        var longLeg = new SpreadLeg(_testExpiry, 99.0, Right.Put, 1);
        
        return new SpreadOrder(
            _startTime,
            "XSP",
            0.50, // credit
            1.0,  // width
            0.50, // credit per width
            Decision.Condor,
            shortLeg,
            longLeg);
    }

    private OpenPosition CreateTestPosition()
    {
        var order = CreateTestSpreadOrder();
        return new OpenPosition(order, 0.45, _startTime); // Entry price after slippage
    }
}