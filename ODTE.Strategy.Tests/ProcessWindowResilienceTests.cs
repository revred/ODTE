using ODTE.Strategy.ProcessWindow;

namespace ODTE.Strategy.Tests;

public class ProcessWindowResilienceTests
{
    private ProcessWindowResilientGuard _resilientGuard;
    private ProcessWindowTradeGuard _baseGuard;
    private ProcessWindowValidator _validator;
    private ProcessWindowMonitor _monitor;
    private ResilienceTestMockTradeExecutor _mockExecutor;
    private InMemoryProcessWindowPersistence _persistence;

    public ProcessWindowResilienceTests()
    {
        _monitor = new ProcessWindowMonitor();
        _validator = new ProcessWindowValidator(_monitor);
        _mockExecutor = new ResilienceTestMockTradeExecutor();
        _baseGuard = new ProcessWindowTradeGuard(_validator, _mockExecutor);
        _persistence = new InMemoryProcessWindowPersistence();
        _resilientGuard = new ProcessWindowResilientGuard(_baseGuard, _persistence);
    }

    public class CircuitBreakerTests
    {
        private CircuitBreaker _circuitBreaker;

        // Constructor replaces TestInitialize in xUnit
        [Fact]
        public void Setup()
        {
            _circuitBreaker = new CircuitBreaker(
                failureThreshold: 3,
                recoveryTimeout: TimeSpan.FromMilliseconds(100),
                retryAttempts: 2
            );
        }

        [Fact]
        public async Task CircuitBreaker_MultipleFailures_ShouldOpenCircuit()
        {
            // Arrange: Create operation that always fails
            var operationCallCount = 0;
            Func<Task<string>> failingOperation = () =>
            {
                operationCallCount++;
                throw new InvalidOperationException($"Failure {operationCallCount}");
            };

            // Act & Assert: First 3 calls should execute, then circuit should open
            for (int i = 1; i <= 3; i++)
            {
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => _circuitBreaker.ExecuteAsync(failingOperation));
            }

            // Circuit should now be open
            _circuitBreaker.State.Should().Be(CircuitBreaker.CircuitBreakerState.Open);

            // Next call should fail fast without executing operation
            var callCountBeforeFastFail = operationCallCount;
            await Assert.ThrowsAsync<CircuitBreakerOpenException>(
                () => _circuitBreaker.ExecuteAsync(failingOperation));

            operationCallCount.Should().Be(callCountBeforeFastFail, "Operation should not execute when circuit is open");
        }

        [Fact]
        public async Task CircuitBreaker_RecoveryAfterTimeout_ShouldTransitionToHalfOpen()
        {
            // Arrange: Open the circuit with failures
            Func<Task<string>> failingOperation = () => throw new InvalidOperationException("Test failure");

            for (int i = 0; i < 3; i++)
            {
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => _circuitBreaker.ExecuteAsync(failingOperation));
            }

            _circuitBreaker.State.Should().Be(CircuitBreaker.CircuitBreakerState.Open);

            // Act: Wait for recovery timeout
            await Task.Delay(150); // Slightly longer than recovery timeout

            // Create a successful operation
            Func<Task<string>> successfulOperation = () => Task.FromResult("Success");

            // Assert: Should execute and transition to closed
            var result = await _circuitBreaker.ExecuteAsync(successfulOperation);
            result.Should().Be("Success");
            _circuitBreaker.State.Should().Be(CircuitBreaker.CircuitBreakerState.Closed);
        }

        [Fact]
        public async Task CircuitBreaker_SuccessfulOperations_ShouldResetFailureCount()
        {
            // Arrange: Partially fail (but not enough to open circuit)
            Func<Task<string>> failingOperation = () => throw new InvalidOperationException("Test failure");
            Func<Task<string>> successfulOperation = () => Task.FromResult("Success");

            // Act: 2 failures (below threshold of 3)
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _circuitBreaker.ExecuteAsync(failingOperation));
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _circuitBreaker.ExecuteAsync(failingOperation));

            // 1 success should reset failure count
            var result = await _circuitBreaker.ExecuteAsync(successfulOperation);
            result.Should().Be("Success");

            // Circuit should still be closed
            _circuitBreaker.State.Should().Be(CircuitBreaker.CircuitBreakerState.Closed);

            // Should take 3 more failures to open (not just 1 more)
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _circuitBreaker.ExecuteAsync(failingOperation));
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _circuitBreaker.ExecuteAsync(failingOperation));

            // Circuit should still be closed after 2 failures
            _circuitBreaker.State.Should().Be(CircuitBreaker.CircuitBreakerState.Closed);
        }
    }

    public class RetryPolicyTests
    {
        [Fact]
        public async Task RetryPolicy_TransientFailure_ShouldEventuallySucceed()
        {
            // Arrange
            var retryPolicy = new RetryPolicy(
                maxAttempts: 3,
                baseDelay: TimeSpan.FromMilliseconds(10),
                maxDelay: TimeSpan.FromMilliseconds(100)
            );

            var attemptCount = 0;
            Func<Task<string>> operationThatSucceedsOnThirdAttempt = () =>
            {
                attemptCount++;
                if (attemptCount < 3)
                    throw new InvalidOperationException($"Transient failure {attemptCount}");
                return Task.FromResult("Success");
            };

            // Act
            var result = await retryPolicy.ExecuteAsync(operationThatSucceedsOnThirdAttempt);

            // Assert
            result.Should().Be("Success");
            attemptCount.Should().Be(3);
            retryPolicy.LastAttemptCount.Should().Be(3);
        }

        [Fact]
        public async Task RetryPolicy_PersistentFailure_ShouldExhaustRetries()
        {
            // Arrange
            var retryPolicy = new RetryPolicy(
                maxAttempts: 3,
                baseDelay: TimeSpan.FromMilliseconds(10),
                maxDelay: TimeSpan.FromMilliseconds(100)
            );

            var attemptCount = 0;
            Func<Task<string>> alwaysFailingOperation = () =>
            {
                attemptCount++;
                throw new InvalidOperationException($"Persistent failure {attemptCount}");
            };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => retryPolicy.ExecuteAsync(alwaysFailingOperation));

            attemptCount.Should().Be(3);
            retryPolicy.LastAttemptCount.Should().Be(3);
        }

        [Fact]
        public async Task RetryPolicy_ExponentialBackoff_ShouldIncreaseDelay()
        {
            // Arrange
            var retryPolicy = new RetryPolicy(
                maxAttempts: 3,
                baseDelay: TimeSpan.FromMilliseconds(50),
                maxDelay: TimeSpan.FromMilliseconds(500)
            );

            var attemptTimes = new List<DateTime>();
            Func<Task<string>> timingOperation = () =>
            {
                attemptTimes.Add(DateTime.UtcNow);
                throw new InvalidOperationException("Timing test");
            };

            // Act
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => retryPolicy.ExecuteAsync(timingOperation));

            // Assert: Should have 3 attempts with increasing delays
            attemptTimes.Count.Should().Be(3);

            // Check delays between attempts (allowing for some variance)
            var delay1 = attemptTimes[1] - attemptTimes[0];
            var delay2 = attemptTimes[2] - attemptTimes[1];

            delay1.TotalMilliseconds.Should().BeGreaterOrEqualTo(40); // ~50ms with some tolerance
            delay2.TotalMilliseconds.Should().BeGreaterThan(delay1.TotalMilliseconds); // Exponential increase
        }
    }

    public class HealthCheckerTests
    {
        private HealthChecker _healthChecker;

        // Constructor replaces TestInitialize in xUnit
        [Fact]
        public void Setup()
        {
            _healthChecker = new HealthChecker();
        }

        [Fact]
        public async Task CheckSystemHealth_NormalConditions_ShouldReturnHealthy()
        {
            // Act
            var healthStatus = await _healthChecker.CheckSystemHealth();

            // Assert
            healthStatus.Should().NotBeNull();
            healthStatus.Checks.Should().NotBeEmpty();
            healthStatus.Status.Should().BeOneOf("Healthy", "Warning"); // May have warnings in test environment

            // Should include all expected health checks
            var componentNames = healthStatus.Checks.Select(c => c.Component).ToList();
            componentNames.Should().Contain(new[] { "Memory", "DiskSpace", "ProcessWindow", "HistoricalData" });
        }

        [Fact]
        public async Task CheckSystemHealth_ProcessWindowCheck_ShouldValidateBasicFunctionality()
        {
            // Act
            var healthStatus = await _healthChecker.CheckSystemHealth();

            // Assert
            var processWindowCheck = healthStatus.Checks.FirstOrDefault(c => c.Component == "ProcessWindow");
            processWindowCheck.Should().NotBeNull();
            processWindowCheck?.IsHealthy.Should().BeTrue();
            processWindowCheck?.Message.Should().Contain("operational");
        }
    }

    public class FallbackMechanismsTests
    {
        private FallbackMechanisms _fallback;

        // Constructor replaces TestInitialize in xUnit
        [Fact]
        public void Setup()
        {
            _fallback = new FallbackMechanisms();
        }

        [Fact]
        public async Task ExecuteFallbackValidations_ValidIronCondorRequest_ShouldPass()
        {
            // Arrange: Valid Iron Condor request
            var request = new TradeRequest
            {
                Strategy = "IronCondor",
                PositionSize = 500m,
                AccountSize = 10000m,
                ExpectedCredit = 18.50m,  // ~3.5% credit (safe)
                CurrentVIX = 15.0m
            };

            // Act
            var result = await _fallback.ExecuteFallbackValidations(request);

            // Assert
            result.AllPassed.Should().BeTrue();
            result.FailureReasons.Should().BeEmpty();
            result.Validations.Should().ContainKeys("BasicSanity", "PositionSize", "IronCondorCredit");
            result.Validations.Values.Should().AllSatisfy(v => v.Should().BeTrue());
        }

        [Fact]
        public async Task ExecuteFallbackValidations_BuggyIronCondorCredit_ShouldFail()
        {
            // Arrange: Iron Condor with the 2.5% bug
            var request = new TradeRequest
            {
                Strategy = "IronCondor",
                PositionSize = 500m,
                AccountSize = 10000m,
                ExpectedCredit = 13.50m,  // ~2.5% credit (the bug!)
                CurrentVIX = 15.0m
            };

            // Act
            var result = await _fallback.ExecuteFallbackValidations(request);

            // Assert
            result.AllPassed.Should().BeFalse();
            result.FailureReasons.Should().NotBeEmpty();
            result.FailureReasons.Should().Contain(r => r.Contains("2.5% bug"));
        }

        [Fact]
        public async Task ExecuteFallbackValidations_ExcessivePositionSize_ShouldFail()
        {
            // Arrange: Request with excessive position size
            var request = new TradeRequest
            {
                Strategy = "IronCondor",
                PositionSize = 6000m,    // 60% of account
                AccountSize = 10000m,
                ExpectedCredit = 210m,
                CurrentVIX = 15.0m
            };

            // Act
            var result = await _fallback.ExecuteFallbackValidations(request);

            // Assert
            result.AllPassed.Should().BeFalse();
            result.FailureReasons.Should().Contain(r => r.Contains("exceeds 50%"));
        }
    }

    public class ResilientGuardIntegrationTests
    {
        private ProcessWindowResilientGuard _resilientGuard;
        private ResilienceTestMockTradeExecutor _mockExecutor;

        // Constructor replaces TestInitialize in xUnit
        [Fact]
        public void Setup()
        {
            var monitor = new ProcessWindowMonitor();
            var validator = new ProcessWindowValidator(monitor);
            _mockExecutor = new ResilienceTestMockTradeExecutor();
            var baseGuard = new ProcessWindowTradeGuard(validator, _mockExecutor);
            var persistence = new InMemoryProcessWindowPersistence();
            _resilientGuard = new ProcessWindowResilientGuard(baseGuard, persistence);
        }

        [Fact]
        public async Task ExecuteResilientTrade_ValidIronCondor_ShouldSucceed()
        {
            // Arrange: Valid Iron Condor trade
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
            var result = await _resilientGuard.ExecuteResilientTrade(request);

            // Assert
            result.Success.Should().BeTrue();
            result.TradeExecuted.Should().BeTrue();
            result.OperationId.Should().NotBeNullOrEmpty();
            result.ResilienceInfo.Should().NotBeNull();
            result.ResilienceInfo.CircuitBreakerState.Should().Be("Closed");
            result.ResilienceInfo.EmergencyMode.Should().BeFalse();
        }

        [Fact]
        public async Task ExecuteResilientTrade_BuggyIronCondor_ShouldBlock()
        {
            // Arrange: Iron Condor with the 2.5% bug
            var request = new TradeRequest
            {
                Strategy = "IronCondor",
                PositionSize = 500m,
                AccountSize = 10000m,
                ExpectedCredit = 13.50m,  // Buggy 2.5% credit
                CurrentVIX = 15.0m
            };

            // Act
            var result = await _resilientGuard.ExecuteResilientTrade(request);

            // Assert
            result.Success.Should().BeFalse();
            result.TradeExecuted.Should().BeFalse();
            result.ReasonCode.Should().Contain("VIOLATION");
            result.OperationId.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task ValidateIronCondorWithResilience_PrimaryAndBackupAgree_ShouldReturnConsistentResult()
        {
            // Arrange: Valid Iron Condor parameters
            var positionSize = 500m;
            var vix = 18.0m;
            var expectedCredit = 20.65m; // ~3.5% credit

            // Act
            var result = await _resilientGuard.ValidateIronCondorWithResilience(
                positionSize, expectedCredit, vix, "Resilience test");

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeTrue();
            result.ValidationMethod.Should().Contain("Primary + Backup");
            result.PrimaryResult.Should().BeTrue();
            result.BackupResult.Should().BeTrue();
            result.OperationId.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task ValidateIronCondorWithResilience_BuggyCredit_ShouldBlockWithAllMethods()
        {
            // Arrange: Iron Condor with the 2.5% bug
            var positionSize = 500m;
            var vix = 18.0m;
            var buggyCredit = 14.75m; // ~2.5% credit (the bug!)

            // Act
            var result = await _resilientGuard.ValidateIronCondorWithResilience(
                positionSize, buggyCredit, vix, "Bug detection test");

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.OperationId.Should().NotBeNullOrEmpty();

            // Should be blocked by either primary, backup, or offline validation
            if (result.ValidationMethod.Contains("Primary"))
            {
                result.PrimaryResult.Should().BeFalse();
            }
            else if (result.ValidationMethod.Contains("Offline"))
            {
                result.ValidationMethod.Should().Contain("primary failed");
            }
        }

        [Fact]
        public async Task GetResilienceStatus_AfterOperations_ShouldProvideCompleteStatus()
        {
            // Arrange: Execute some operations first
            var validRequest = new TradeRequest
            {
                Strategy = "IronCondor",
                PositionSize = 500m,
                AccountSize = 10000m,
                ExpectedCredit = 18.50m,
                CurrentVIX = 15.0m
            };

            _mockExecutor.SetupSuccessfulTrade(18.50m);
            await _resilientGuard.ExecuteResilientTrade(validRequest);

            // Act
            var status = await _resilientGuard.GetResilienceStatus();

            // Assert
            status.Should().NotBeNull();
            status.EmergencyMode.Should().BeFalse();
            status.CircuitBreakerState.Should().Be("Closed");
            status.HealthStatus.Should().NotBeNull();
            status.LastSuccessfulValidations.Should().ContainKey("IronCondor");
            status.ConsecutiveFailures.Should().ContainKey("IronCondor");
            status.ConsecutiveFailures["IronCondor"].Should().Be(0);
        }
    }

    public class OfflineValidationTests
    {
        [Fact]
        public void ValidateIronCondorOffline_CorrectCredit_ShouldReturnTrue()
        {
            // Arrange
            var positionSize = 500m;
            var vix = 18.0m;
            var correctCredit = positionSize * 0.035m * (1.0m + vix / 100m); // 3.5% credit

            // Act
            var resilientGuard = new ProcessWindowResilientGuard(null, new InMemoryProcessWindowPersistence());
            var result = TestHelper.CallPrivateMethod<bool>(resilientGuard, "ValidateIronCondorOffline",
                positionSize, correctCredit, vix);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void ValidateIronCondorOffline_BuggyCredit_ShouldReturnFalse()
        {
            // Arrange: The exact 2.5% credit that caused the bug
            var positionSize = 500m;
            var vix = 18.0m;
            var buggyCredit = positionSize * 0.025m * (1.0m + vix / 100m); // 2.5% credit (the bug!)

            // Act
            var resilientGuard = new ProcessWindowResilientGuard(null, new InMemoryProcessWindowPersistence());
            var result = TestHelper.CallPrivateMethod<bool>(resilientGuard, "ValidateIronCondorOffline",
                positionSize, buggyCredit, vix);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void ValidateIronCondorOffline_EdgeCases_ShouldHandleCorrectly()
        {
            var resilientGuard = new ProcessWindowResilientGuard(null, new InMemoryProcessWindowPersistence());

            // Test minimum safe boundary
            var minSafeCredit = 500m * 0.030m * 1.18m; // 3.0% with VIX 18%
            var minResult = TestHelper.CallPrivateMethod<bool>(resilientGuard, "ValidateIronCondorOffline",
                500m, minSafeCredit, 18.0m);
            minResult.Should().BeTrue();

            // Test maximum safe boundary
            var maxSafeCredit = 500m * 0.045m * 1.18m; // 4.5% with VIX 18%
            var maxResult = TestHelper.CallPrivateMethod<bool>(resilientGuard, "ValidateIronCondorOffline",
                500m, maxSafeCredit, 18.0m);
            maxResult.Should().BeTrue();

            // Test just below minimum (should fail)
            var belowMinCredit = 500m * 0.029m * 1.18m; // 2.9% with VIX 18%
            var belowMinResult = TestHelper.CallPrivateMethod<bool>(resilientGuard, "ValidateIronCondorOffline",
                500m, belowMinCredit, 18.0m);
            belowMinResult.Should().BeFalse();

            // Test just above maximum (should fail)
            var aboveMaxCredit = 500m * 0.046m * 1.18m; // 4.6% with VIX 18%
            var aboveMaxResult = TestHelper.CallPrivateMethod<bool>(resilientGuard, "ValidateIronCondorOffline",
                500m, aboveMaxCredit, 18.0m);
            aboveMaxResult.Should().BeFalse();
        }
    }

    public class EmergencyModeTests
    {
        [Fact]
        public async Task ExecuteResilientTrade_MultipleFailures_ShouldActivateEmergencyMode()
        {
            // Arrange: Setup guard with failing executor
            var monitor = new ProcessWindowMonitor();
            var validator = new ProcessWindowValidator(monitor);
            var failingExecutor = new ResilienceTestMockTradeExecutor();
            failingExecutor.SetupFailedTrade("SYSTEM_ERROR", "Simulated system failure");

            var baseGuard = new ProcessWindowTradeGuard(validator, failingExecutor);
            var persistence = new InMemoryProcessWindowPersistence();
            var resilientGuard = new ProcessWindowResilientGuard(baseGuard, persistence);

            var request = new TradeRequest
            {
                Strategy = "IronCondor",
                PositionSize = 500m,
                AccountSize = 10000m,
                ExpectedCredit = 18.50m,
                CurrentVIX = 15.0m
            };

            // Act: Cause multiple failures to trigger emergency mode
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    await resilientGuard.ExecuteResilientTrade(request);
                }
                catch
                {
                    // Expected failures
                }
            }

            // Check if emergency mode was activated
            var status = await resilientGuard.GetResilienceStatus();

            // Assert
            status.EmergencyMode.Should().BeTrue();
            status.ConsecutiveFailures["IronCondor"].Should().BeGreaterOrEqualTo(3);
        }
    }

    /// <summary>
    /// Helper class for testing private methods (used sparingly and carefully)
    /// </summary>
    public static class TestHelper
    {
        public static T CallPrivateMethod<T>(object obj, string methodName, params object[] parameters)
        {
            var method = obj.GetType().GetMethod(methodName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (T)method.Invoke(obj, parameters);
        }
    }
}

/// <summary>
/// Mock trade executor for resilience testing
/// </summary>
public class ResilienceTestMockTradeExecutor : ITradeExecutor
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
