using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ODTE.Strategy.ProcessWindow;

namespace ODTE.Strategy.Tests
{
    [TestClass]
    public class ProcessWindowTests
    {
        private ProcessWindowMonitor _monitor;
        private ProcessWindowValidator _validator;
        private MockTradeExecutor _mockExecutor;
        private ProcessWindowTradeGuard _guard;

        [TestInitialize]
        public void Setup()
        {
            _monitor = new ProcessWindowMonitor();
            _validator = new ProcessWindowValidator(_monitor);
            _mockExecutor = new MockTradeExecutor();
            _guard = new ProcessWindowTradeGuard(_validator, _mockExecutor);
        }

        [TestClass]
        public class IronCondorCreditTests
        {
            private ProcessWindowMonitor _monitor;

            [TestInitialize]
            public void Setup()
            {
                _monitor = new ProcessWindowMonitor();
            }

            [TestMethod]
            public void IronCondorCredit_35Percent_ShouldBeInGreenZone()
            {
                // Arrange: The CORRECT credit percentage that yields 29.81% CAGR
                var creditPct = 0.035m; // 3.5% - the fixed value
                var timestamp = DateTime.UtcNow;

                // Act
                var result = _monitor.CheckParameter("IronCondorCreditPct", creditPct, timestamp, "Test: Correct credit percentage");

                // Assert
                result.Status.Should().Be(ProcessWindowMonitor.WindowStatus.GreenZone);
                result.AlertLevel.Should().Be(ProcessWindowMonitor.AlertLevel.Info);
                result.Message.Should().Contain("within safe bounds");
            }

            [TestMethod]
            public void IronCondorCredit_25Percent_ShouldBeInBlackSwanZone()
            {
                // Arrange: The BUGGY credit percentage that caused 0% returns
                var creditPct = 0.025m; // 2.5% - the bug that destroyed profitability
                var timestamp = DateTime.UtcNow;

                // Act
                var result = _monitor.CheckParameter("IronCondorCreditPct", creditPct, timestamp, "Test: Buggy credit percentage");

                // Assert
                result.Status.Should().Be(ProcessWindowMonitor.WindowStatus.BlackSwan);
                result.AlertLevel.Should().Be(ProcessWindowMonitor.AlertLevel.Emergency);
                result.Message.Should().Contain("CRITICAL VIOLATION");
                result.Message.Should().Contain("SUSPEND TRADING");
            }

            [TestMethod]
            public void IronCondorCredit_32Percent_ShouldBeInYellowZone()
            {
                // Arrange: Credit percentage at warning threshold
                var creditPct = 0.032m; // 3.2% - warning level
                var timestamp = DateTime.UtcNow;

                // Act
                var result = _monitor.CheckParameter("IronCondorCreditPct", creditPct, timestamp, "Test: Warning level credit");

                // Assert
                result.Status.Should().Be(ProcessWindowMonitor.WindowStatus.YellowZone);
                result.AlertLevel.Should().Be(ProcessWindowMonitor.AlertLevel.Warning);
                result.Message.Should().Contain("WARNING");
                result.Message.Should().Contain("approaching limits");
            }

            [TestMethod]
            public void IronCondorCredit_45Percent_ShouldBeInBlackSwanZone()
            {
                // Arrange: Unrealistically high credit percentage
                var creditPct = 0.045m; // 4.5% - too high to be realistic
                var timestamp = DateTime.UtcNow;

                // Act
                var result = _monitor.CheckParameter("IronCondorCreditPct", creditPct, timestamp, "Test: Unrealistic high credit");

                // Assert
                result.Status.Should().Be(ProcessWindowMonitor.WindowStatus.BlackSwan);
                result.AlertLevel.Should().Be(ProcessWindowMonitor.AlertLevel.Emergency);
                result.Message.Should().Contain("CRITICAL VIOLATION");
            }

            [TestMethod]
            public void IronCondorCredit_RealWorldScenario_ShouldValidateCorrectly()
            {
                // Arrange: Real-world Iron Condor scenario
                var positionSize = 500m;  // $500 position
                var vix = 18.5m;          // VIX at 18.5%
                var expectedCredit = 21.70m; // Expected credit
                
                // Calculate credit percentage (should be close to 3.5%)
                var creditPct = expectedCredit / (positionSize * (1.0m + vix / 100m));

                // Act
                var result = _monitor.CheckParameter("IronCondorCreditPct", creditPct, DateTime.UtcNow, 
                    $"Real scenario: ${expectedCredit} credit on ${positionSize} position, VIX={vix}");

                // Assert
                creditPct.Should().BeApproximately(0.035m, 0.003m, "Credit should be approximately 3.5%");
                result.Status.Should().Be(ProcessWindowMonitor.WindowStatus.GreenZone, "Real-world scenario should be safe");
            }
        }

        [TestClass]
        public class SystemStatusTests
        {
            private ProcessWindowMonitor _monitor;

            [TestInitialize]
            public void Setup()
            {
                _monitor = new ProcessWindowMonitor();
            }

            [TestMethod]
            public void SystemStatus_AllParametersGreen_ShouldBeGreenZone()
            {
                // Arrange: All parameters in safe ranges
                var parameters = new Dictionary<string, decimal>
                {
                    ["IronCondorCreditPct"] = 0.035m,
                    ["CommissionPerLeg"] = 0.65m,
                    ["SlippagePerLeg"] = 0.025m,
                    ["VixBonusMultiplier"] = 1.20m,
                    ["WinRate"] = 0.75m
                };

                // Act
                var systemStatus = _monitor.CheckSystemStatus(parameters, DateTime.UtcNow, "All green test");

                // Assert
                systemStatus.OverallStatus.Should().Be(ProcessWindowMonitor.WindowStatus.GreenZone);
                systemStatus.CriticalViolations.Should().Be(0);
                systemStatus.WarningCount.Should().Be(0);
                systemStatus.ShouldSuspendTrading.Should().BeFalse();
                systemStatus.ShouldReducePositionSize.Should().BeFalse();
            }

            [TestMethod]
            public void SystemStatus_OneParameterCritical_ShouldBeBlackSwan()
            {
                // Arrange: One parameter in critical range (the Iron Condor bug)
                var parameters = new Dictionary<string, decimal>
                {
                    ["IronCondorCreditPct"] = 0.025m, // CRITICAL BUG
                    ["CommissionPerLeg"] = 0.65m,     // Good
                    ["SlippagePerLeg"] = 0.025m,      // Good
                    ["WinRate"] = 0.75m               // Good
                };

                // Act
                var systemStatus = _monitor.CheckSystemStatus(parameters, DateTime.UtcNow, "Critical violation test");

                // Assert
                systemStatus.OverallStatus.Should().Be(ProcessWindowMonitor.WindowStatus.BlackSwan);
                systemStatus.CriticalViolations.Should().Be(1);
                systemStatus.ShouldSuspendTrading.Should().BeTrue();
                systemStatus.GetSummaryMessage().Should().Contain("SUSPEND TRADING");
            }

            [TestMethod]
            public void SystemStatus_MultipleWarnings_ShouldBeRedZone()
            {
                // Arrange: Multiple parameters in warning ranges
                var parameters = new Dictionary<string, decimal>
                {
                    ["IronCondorCreditPct"] = 0.032m,  // Warning
                    ["CommissionPerLeg"] = 1.90m,      // Warning  
                    ["SlippagePerLeg"] = 0.030m,       // Warning
                    ["WinRate"] = 0.67m                // Warning
                };

                // Act
                var systemStatus = _monitor.CheckSystemStatus(parameters, DateTime.UtcNow, "Multiple warnings test");

                // Assert
                systemStatus.OverallStatus.Should().Be(ProcessWindowMonitor.WindowStatus.RedZone);
                systemStatus.WarningCount.Should().BeGreaterThan(2);
                systemStatus.ShouldReducePositionSize.Should().BeTrue();
            }
        }

        [TestClass]
        public class TradeValidationTests
        {
            private ProcessWindowValidator _validator;
            private ProcessWindowMonitor _monitor;

            [TestInitialize]
            public void Setup()
            {
                _monitor = new ProcessWindowMonitor();
                _validator = new ProcessWindowValidator(_monitor);
            }

            [TestMethod]
            public async Task ValidateTradeParameters_SafeIronCondor_ShouldAllowTrade()
            {
                // Arrange: Safe Iron Condor parameters
                var context = new TradeExecutionContext
                {
                    Strategy = "IronCondor",
                    PositionSize = 500m,
                    AccountSize = 10000m,
                    ExpectedCredit = 18.50m,  // This gives ~3.5% credit
                    VIX = 15.0m,
                    CommissionPerLeg = 0.65m,
                    SlippagePerLeg = 0.025m
                };

                // Act
                var result = await _validator.ValidateTradeParameters(context);

                // Assert
                result.IsValid.Should().BeTrue();
                result.ShouldReduceSize.Should().BeFalse();
                result.SystemStatus.ShouldSuspendTrading.Should().BeFalse();
            }

            [TestMethod]
            public async Task ValidateTradeParameters_BuggyIronCondor_ShouldBlockTrade()
            {
                // Arrange: Iron Condor with the 2.5% bug
                var context = new TradeExecutionContext
                {
                    Strategy = "IronCondor",
                    PositionSize = 500m,
                    AccountSize = 10000m,
                    ExpectedCredit = 13.50m,  // This gives ~2.5% credit (the bug!)
                    VIX = 15.0m,
                    CommissionPerLeg = 0.65m,
                    SlippagePerLeg = 0.025m
                };

                // Act
                var result = await _validator.ValidateTradeParameters(context);

                // Assert
                result.IsValid.Should().BeFalse();
                result.SystemStatus.ShouldSuspendTrading.Should().BeTrue();
                result.SystemStatus.CriticalViolations.Should().BeGreaterThan(0);
            }

            [TestMethod]
            public async Task ValidateIronCondorCredit_CorrectCredit_ShouldReturnTrue()
            {
                // Arrange: The correct 3.5% credit that yields 29.81% CAGR
                var positionSize = 500m;
                var vix = 18.0m;
                var expectedCredit = positionSize * 0.035m * (1.0m + vix / 100m); // Correct formula

                // Act
                var isValid = await _validator.ValidateIronCondorCredit(expectedCredit, positionSize, vix, "Correct credit test");

                // Assert
                isValid.Should().BeTrue();
            }

            [TestMethod]
            public async Task ValidateIronCondorCredit_BuggyCredit_ShouldReturnFalse()
            {
                // Arrange: The buggy 2.5% credit that caused 0% returns
                var positionSize = 500m;
                var vix = 18.0m;
                var buggyCredit = positionSize * 0.025m * (1.0m + vix / 100m); // Buggy formula

                // Act
                var isValid = await _validator.ValidateIronCondorCredit(buggyCredit, positionSize, vix, "Buggy credit test");

                // Assert
                isValid.Should().BeFalse();
            }
        }

        [TestClass]
        public class TradeGuardTests
        {
            private ProcessWindowTradeGuard _guard;
            private MockTradeExecutor _mockExecutor;
            private ProcessWindowValidator _validator;

            [TestInitialize]
            public void Setup()
            {
                var monitor = new ProcessWindowMonitor();
                _validator = new ProcessWindowValidator(monitor);
                _mockExecutor = new MockTradeExecutor();
                _guard = new ProcessWindowTradeGuard(_validator, _mockExecutor);
            }

            [TestMethod]
            public async Task ExecuteTradeWithGuard_SafeParameters_ShouldExecuteTrade()
            {
                // Arrange: Safe trade request
                var request = new TradeRequest
                {
                    Strategy = "IronCondor",
                    PositionSize = 500m,
                    AccountSize = 10000m,
                    ExpectedCredit = 18.50m,  // Safe 3.5% credit
                    CurrentVIX = 15.0m
                };

                _mockExecutor.SetupSuccessfulTrade(18.50m);

                // Act
                var result = await _guard.ExecuteTradeWithGuard(request);

                // Assert
                result.Success.Should().BeTrue();
                result.TradeExecuted.Should().BeTrue();
                result.PositionSizeAdjusted.Should().BeFalse();
                result.ReasonCode.Should().Be("SUCCESS");
            }

            [TestMethod]
            public async Task ExecuteTradeWithGuard_CriticalViolation_ShouldBlockTrade()
            {
                // Arrange: Trade request with critical violation
                var request = new TradeRequest
                {
                    Strategy = "IronCondor",
                    PositionSize = 500m,
                    AccountSize = 10000m,
                    ExpectedCredit = 13.50m,  // Buggy 2.5% credit
                    CurrentVIX = 15.0m
                };

                // Act
                var result = await _guard.ExecuteTradeWithGuard(request);

                // Assert
                result.Success.Should().BeFalse();
                result.TradeExecuted.Should().BeFalse();
                result.ReasonCode.Should().Be("PROCESS_WINDOW_VIOLATION");
                result.Message.Should().Contain("suspended");
            }

            [TestMethod]
            public async Task ExecuteTradeWithGuard_WarningConditions_ShouldReducePositionSize()
            {
                // Arrange: Trade request with warning conditions
                var request = new TradeRequest
                {
                    Strategy = "IronCondor",
                    PositionSize = 500m,
                    AccountSize = 10000m,
                    ExpectedCredit = 16.50m,  // Warning level credit (3.2%)
                    CurrentVIX = 15.0m,
                    CommissionPerLeg = 1.90m  // Warning level commission
                };

                _mockExecutor.SetupSuccessfulTrade(14.85m); // Reduced credit for reduced position

                // Act
                var result = await _guard.ExecuteTradeWithGuard(request);

                // Assert
                result.Success.Should().BeTrue();
                result.TradeExecuted.Should().BeTrue();
                result.PositionSizeAdjusted.Should().BeTrue();
                result.AdjustedPositionSize.Should().BeLessThan(result.OriginalPositionSize);
            }

            [TestMethod]
            public async Task ValidateIronCondorBeforeExecution_SafeCredit_ShouldReturnTrue()
            {
                // Arrange
                var positionSize = 500m;
                var vix = 18.0m;
                var safeCredit = 20.65m; // Approximately 3.5%

                // Act
                var isValid = await _guard.ValidateIronCondorBeforeExecution(positionSize, safeCredit, vix, "Pre-execution validation");

                // Assert
                isValid.Should().BeTrue();
            }

            [TestMethod]
            public async Task ValidateIronCondorBeforeExecution_BuggyCredit_ShouldReturnFalse()
            {
                // Arrange
                var positionSize = 500m;
                var vix = 18.0m;
                var buggyCredit = 14.75m; // Approximately 2.5% (the bug!)

                // Act
                var isValid = await _guard.ValidateIronCondorBeforeExecution(positionSize, buggyCredit, vix, "Bug detection test");

                // Assert
                isValid.Should().BeFalse();
            }
        }

        [TestClass]
        public class ViolationLoggingTests
        {
            private ProcessWindowMonitor _monitor;

            [TestInitialize]
            public void Setup()
            {
                _monitor = new ProcessWindowMonitor();
            }

            [TestMethod]
            public void ViolationHistory_MultipleViolations_ShouldTrackCorrectly()
            {
                // Arrange & Act: Generate multiple violations
                _monitor.CheckParameter("IronCondorCreditPct", 0.025m, DateTime.UtcNow.AddMinutes(-5), "First violation");
                _monitor.CheckParameter("CommissionPerLeg", 6.00m, DateTime.UtcNow.AddMinutes(-3), "Second violation");
                _monitor.CheckParameter("IronCondorCreditPct", 0.020m, DateTime.UtcNow.AddMinutes(-1), "Third violation");

                var history = _monitor.GetViolationHistory();
                var summary = _monitor.GetViolationSummary();

                // Assert
                history.Count.Should().Be(3);
                summary.TotalViolations.Should().Be(3);
                summary.CriticalViolations.Should().Be(3); // All should be critical
                summary.MostFrequentParameter.Should().Be("IronCondorCreditPct");
            }

            [TestMethod]
            public void ViolationHistory_TimePeriodFilter_ShouldFilterCorrectly()
            {
                // Arrange: Generate violations across time
                _monitor.CheckParameter("IronCondorCreditPct", 0.025m, DateTime.UtcNow.AddHours(-2), "Old violation");
                _monitor.CheckParameter("IronCondorCreditPct", 0.025m, DateTime.UtcNow.AddMinutes(-5), "Recent violation");

                // Act: Get violations from last hour only
                var recentHistory = _monitor.GetViolationHistory(TimeSpan.FromHours(1));

                // Assert
                recentHistory.Count.Should().Be(1);
                recentHistory[0].Context.Should().Contain("Recent violation");
            }
        }

        /// <summary>
        /// Mock trade executor for testing
        /// </summary>
        public class MockTradeExecutor : ITradeExecutor
        {
            private TradeResult _nextResult;

            public void SetupSuccessfulTrade(decimal actualCredit)
            {
                _nextResult = new TradeResult
                {
                    Success = true,
                    ActualCredit = actualCredit,
                    ActualCommission = 2.60m, // 4 legs × $0.65
                    ActualSlippage = 0.10m,   // 4 legs × $0.025
                    ExecutionTime = DateTime.UtcNow
                };
            }

            public void SetupFailedTrade(string errorCode, string errorMessage)
            {
                _nextResult = new TradeResult
                {
                    Success = false,
                    ErrorCode = errorCode,
                    ErrorMessage = errorMessage,
                    ExecutionTime = DateTime.UtcNow
                };
            }

            public async Task<TradeResult> ExecuteTrade(TradeRequest request)
            {
                await Task.Delay(1); // Simulate async execution
                return _nextResult ?? new TradeResult { Success = true, ActualCredit = request.ExpectedCredit };
            }
        }
    }
}