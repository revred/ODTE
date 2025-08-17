namespace ODTE.Strategy.ProcessWindow
{
    /// <summary>
    /// Resilient Process Window Guard - Enhanced with fault tolerance patterns
    /// Provides circuit breaker, retry, and graceful degradation capabilities
    /// 
    /// CRITICAL: Ensures the 2.5% vs 3.5% Iron Condor prevention NEVER fails
    /// Even when underlying systems experience failures
    /// </summary>
    public class ProcessWindowResilientGuard
    {
        private readonly ProcessWindowTradeGuard _innerGuard;
        private readonly IProcessWindowPersistence _persistence;
        private readonly CircuitBreaker _circuitBreaker;
        private readonly RetryPolicy _retryPolicy;
        private readonly HealthChecker _healthChecker;
        private readonly FallbackMechanisms _fallback;

        // Resilience state tracking
        private readonly Dictionary<string, DateTime> _lastSuccessfulValidation = new();
        private readonly Dictionary<string, int> _consecutiveFailures = new();
        private volatile bool _emergencyMode = false;

        public ProcessWindowResilientGuard(
            ProcessWindowTradeGuard innerGuard,
            IProcessWindowPersistence persistence = null)
        {
            _innerGuard = innerGuard ?? throw new ArgumentNullException(nameof(innerGuard));
            _persistence = persistence ?? new InMemoryProcessWindowPersistence();
            _circuitBreaker = new CircuitBreaker(
                failureThreshold: 5,      // 5 failures before opening
                recoveryTimeout: TimeSpan.FromMinutes(2),  // 2 min recovery
                retryAttempts: 3          // 3 attempts to close
            );
            _retryPolicy = new RetryPolicy(
                maxAttempts: 3,
                baseDelay: TimeSpan.FromMilliseconds(500),
                maxDelay: TimeSpan.FromSeconds(5)
            );
            _healthChecker = new HealthChecker();
            _fallback = new FallbackMechanisms();
        }

        /// <summary>
        /// Execute trade with maximum resilience protection
        /// CRITICAL: This MUST prevent the Iron Condor bug even under system failures
        /// </summary>
        public async Task<ResilientTradeResult> ExecuteResilientTrade(TradeRequest request)
        {
            var operationId = Guid.NewGuid().ToString("N")[..8];
            var startTime = DateTime.UtcNow;

            try
            {
                // Step 1: Health check before proceeding
                var healthStatus = await _healthChecker.CheckSystemHealth();
                if (healthStatus.IsCritical)
                {
                    return await HandleCriticalHealthFailure(request, healthStatus, operationId);
                }

                // Step 2: Check if we're in emergency mode
                if (_emergencyMode)
                {
                    return await ExecuteEmergencyModeValidation(request, operationId);
                }

                // Step 3: Execute with circuit breaker protection
                return await _circuitBreaker.ExecuteAsync(async () =>
                {
                    // Execute with retry policy
                    return await _retryPolicy.ExecuteAsync(async () =>
                    {
                        var result = await _innerGuard.ExecuteTradeWithGuard(request);

                        // Track success for resilience monitoring
                        await TrackSuccessfulOperation(request.Strategy, operationId);

                        return new ResilientTradeResult(result)
                        {
                            OperationId = operationId,
                            ResilienceInfo = new ResilienceInfo
                            {
                                CircuitBreakerState = _circuitBreaker.State.ToString(),
                                HealthStatus = healthStatus.Status,
                                EmergencyMode = _emergencyMode,
                                RetryAttempts = _retryPolicy.LastAttemptCount,
                                Duration = DateTime.UtcNow - startTime
                            }
                        };
                    });
                });
            }
            catch (CircuitBreakerOpenException)
            {
                // Circuit breaker is open - use fallback validation
                return await ExecuteFallbackValidation(request, operationId, "CircuitBreakerOpen");
            }
            catch (Exception ex)
            {
                // Ultimate fallback - use offline validation rules
                await TrackFailedOperation(request.Strategy, ex, operationId);
                return await ExecuteOfflineValidation(request, operationId, ex);
            }
        }

        /// <summary>
        /// CRITICAL: Iron Condor validation with maximum resilience
        /// This validation MUST work even when primary systems fail
        /// </summary>
        public async Task<ResilientValidationResult> ValidateIronCondorWithResilience(
            decimal positionSize, decimal expectedCredit, decimal vix, string context = "")
        {
            var operationId = Guid.NewGuid().ToString("N")[..8];

            try
            {
                // Primary validation attempt
                var primaryResult = await _innerGuard.ValidateIronCondorBeforeExecution(
                    positionSize, expectedCredit, vix, context);

                // Validate using cached historical data as backup
                var backupResult = await ValidateAgainstHistoricalBaseline(
                    positionSize, expectedCredit, vix, context);

                // Cross-validate results
                if (primaryResult != backupResult)
                {
                    Console.WriteLine($"🚨 VALIDATION MISMATCH DETECTED - Operation: {operationId}");
                    Console.WriteLine($"   Primary Result: {primaryResult}");
                    Console.WriteLine($"   Backup Result: {backupResult}");

                    // Use the more conservative result
                    var conservativeResult = primaryResult && backupResult;
                    await _persistence.LogValidationMismatch(operationId, primaryResult, backupResult, conservativeResult);

                    return new ResilientValidationResult
                    {
                        IsValid = conservativeResult,
                        ValidationMethod = "Conservative (mismatch detected)",
                        PrimaryResult = primaryResult,
                        BackupResult = backupResult,
                        OperationId = operationId
                    };
                }

                return new ResilientValidationResult
                {
                    IsValid = primaryResult,
                    ValidationMethod = "Primary + Backup Confirmed",
                    PrimaryResult = primaryResult,
                    BackupResult = backupResult,
                    OperationId = operationId
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"🚨 PRIMARY VALIDATION FAILED - Falling back to offline rules");

                // CRITICAL: Offline validation using hardcoded safe boundaries
                var offlineResult = ValidateIronCondorOffline(positionSize, expectedCredit, vix);
                await _persistence.LogValidationFailure(operationId, ex, offlineResult);

                return new ResilientValidationResult
                {
                    IsValid = offlineResult,
                    ValidationMethod = "Offline Rules (primary failed)",
                    PrimaryResult = null,
                    BackupResult = null,
                    OperationId = operationId,
                    FallbackReason = ex.Message
                };
            }
        }

        /// <summary>
        /// CRITICAL: Offline Iron Condor validation using hardcoded safe rules
        /// This is the last line of defense against the 2.5% vs 3.5% bug
        /// </summary>
        private bool ValidateIronCondorOffline(decimal positionSize, decimal expectedCredit, decimal vix)
        {
            // Calculate credit percentage
            var creditPct = expectedCredit / (positionSize * (1.0m + vix / 100m));

            // HARDCODED SAFE BOUNDARIES (never change these without extreme caution)
            const decimal ABSOLUTE_MIN_CREDIT = 0.030m;  // 3.0% - below this is catastrophic
            const decimal ABSOLUTE_MAX_CREDIT = 0.045m;  // 4.5% - above this is unrealistic
            const decimal KNOWN_FAILURE_POINT = 0.025m; // 2.5% - the exact failure point we're preventing

            // Multiple safety checks
            var isSafe = creditPct >= ABSOLUTE_MIN_CREDIT &&
                        creditPct <= ABSOLUTE_MAX_CREDIT &&
                        creditPct > KNOWN_FAILURE_POINT;

            Console.WriteLine($"📋 OFFLINE VALIDATION - Iron Condor Credit Check");
            Console.WriteLine($"   Position Size: ${positionSize:F2}");
            Console.WriteLine($"   Expected Credit: ${expectedCredit:F2}");
            Console.WriteLine($"   Credit %: {creditPct:P2}");
            Console.WriteLine($"   VIX: {vix:F1}");
            Console.WriteLine($"   Result: {(isSafe ? "✅ SAFE" : "🚨 BLOCKED")}");

            if (!isSafe)
            {
                Console.WriteLine($"   🚨 BLOCKED REASON: Credit {creditPct:P2} outside safe range [{ABSOLUTE_MIN_CREDIT:P2} - {ABSOLUTE_MAX_CREDIT:P2}]");
                if (creditPct <= KNOWN_FAILURE_POINT)
                {
                    Console.WriteLine($"   🚨 CRITICAL: This matches the 2.5% bug that caused 0% returns!");
                }
            }

            return isSafe;
        }

        /// <summary>
        /// Validate against historical baseline (backup validation)
        /// </summary>
        private async Task<bool> ValidateAgainstHistoricalBaseline(
            decimal positionSize, decimal expectedCredit, decimal vix, string context)
        {
            try
            {
                var historicalData = await _persistence.GetHistoricalValidationData("IronCondor");
                if (historicalData?.CreditPercentiles != null)
                {
                    var creditPct = expectedCredit / (positionSize * (1.0m + vix / 100m));

                    // Use 10th and 90th percentiles as boundaries
                    var isWithinHistoricalBounds = creditPct >= historicalData.CreditPercentiles.P10 &&
                                                 creditPct <= historicalData.CreditPercentiles.P90;

                    return isWithinHistoricalBounds;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Historical baseline validation failed: {ex.Message}");
            }

            // Fallback to conservative validation
            return ValidateIronCondorOffline(positionSize, expectedCredit, vix);
        }

        /// <summary>
        /// Handle critical health failures
        /// </summary>
        private async Task<ResilientTradeResult> HandleCriticalHealthFailure(
            TradeRequest request, HealthStatus healthStatus, string operationId)
        {
            Console.WriteLine($"🚨 CRITICAL HEALTH FAILURE - Operation: {operationId}");
            Console.WriteLine($"   Health Issues: {string.Join(", ", healthStatus.Issues)}");

            // For Iron Condor trades, still validate with offline rules
            if (request.Strategy.Contains("IronCondor", StringComparison.OrdinalIgnoreCase))
            {
                var offlineValidation = ValidateIronCondorOffline(
                    request.PositionSize, request.ExpectedCredit, request.CurrentVIX);

                if (!offlineValidation)
                {
                    return new ResilientTradeResult
                    {
                        Success = false,
                        TradeExecuted = false,
                        ReasonCode = "HEALTH_FAILURE_VALIDATION_BLOCKED",
                        Message = "Trade blocked by offline validation due to critical health failure",
                        OperationId = operationId
                    };
                }
            }

            // Enable emergency mode
            _emergencyMode = true;
            Console.WriteLine("⚠️  EMERGENCY MODE ACTIVATED");

            return new ResilientTradeResult
            {
                Success = false,
                TradeExecuted = false,
                ReasonCode = "CRITICAL_HEALTH_FAILURE",
                Message = $"Trade suspended due to critical health failure: {string.Join(", ", healthStatus.Issues)}",
                OperationId = operationId
            };
        }

        /// <summary>
        /// Execute in emergency mode with minimal dependencies
        /// </summary>
        private async Task<ResilientTradeResult> ExecuteEmergencyModeValidation(
            TradeRequest request, string operationId)
        {
            Console.WriteLine($"⚠️  EMERGENCY MODE VALIDATION - Operation: {operationId}");

            // Only allow trades that pass offline validation
            if (request.Strategy.Contains("IronCondor", StringComparison.OrdinalIgnoreCase))
            {
                var isValid = ValidateIronCondorOffline(
                    request.PositionSize, request.ExpectedCredit, request.CurrentVIX);

                if (!isValid)
                {
                    return new ResilientTradeResult
                    {
                        Success = false,
                        TradeExecuted = false,
                        ReasonCode = "EMERGENCY_MODE_BLOCKED",
                        Message = "Trade blocked by emergency mode offline validation",
                        OperationId = operationId
                    };
                }

                // In emergency mode, reduce position size by 50%
                request.PositionSize *= 0.5m;
                request.ExpectedCredit *= 0.5m;

                Console.WriteLine($"⚠️  EMERGENCY MODE: Position size reduced by 50%");
            }

            return new ResilientTradeResult
            {
                Success = true,
                TradeExecuted = false, // Don't actually execute in emergency mode
                ReasonCode = "EMERGENCY_MODE_QUEUED",
                Message = "Trade validated and queued for execution when systems recover",
                OperationId = operationId
            };
        }

        /// <summary>
        /// Execute fallback validation when circuit breaker is open
        /// </summary>
        private async Task<ResilientTradeResult> ExecuteFallbackValidation(
            TradeRequest request, string operationId, string reason)
        {
            Console.WriteLine($"⚡ FALLBACK VALIDATION - Operation: {operationId}, Reason: {reason}");

            // Use offline validation for critical parameters
            var fallbackValidations = await _fallback.ExecuteFallbackValidations(request);

            if (!fallbackValidations.AllPassed)
            {
                return new ResilientTradeResult
                {
                    Success = false,
                    TradeExecuted = false,
                    ReasonCode = "FALLBACK_VALIDATION_FAILED",
                    Message = $"Trade blocked by fallback validation: {fallbackValidations.FailureReasons}",
                    OperationId = operationId
                };
            }

            // Reduce position size for fallback trades
            var reducedSize = request.PositionSize * 0.75m; // 25% reduction
            Console.WriteLine($"⚡ FALLBACK: Position size reduced to ${reducedSize:F2}");

            return new ResilientTradeResult
            {
                Success = true,
                TradeExecuted = false, // Queue for later execution
                ReasonCode = "FALLBACK_VALIDATION_PASSED",
                Message = "Trade validated via fallback mechanisms, queued for execution",
                OperationId = operationId
            };
        }

        /// <summary>
        /// Ultimate fallback - offline validation only
        /// </summary>
        private async Task<ResilientTradeResult> ExecuteOfflineValidation(
            TradeRequest request, string operationId, Exception originalError)
        {
            Console.WriteLine($"🆘 OFFLINE VALIDATION - Operation: {operationId}");
            Console.WriteLine($"   Original Error: {originalError.Message}");

            // Only validate critical parameters offline
            var isValid = true;
            var blockingReasons = new List<string>();

            // Iron Condor critical validation
            if (request.Strategy.Contains("IronCondor", StringComparison.OrdinalIgnoreCase))
            {
                var creditValid = ValidateIronCondorOffline(
                    request.PositionSize, request.ExpectedCredit, request.CurrentVIX);

                if (!creditValid)
                {
                    isValid = false;
                    blockingReasons.Add("Iron Condor credit percentage failed offline validation");
                }
            }

            // Basic sanity checks
            if (request.PositionSize <= 0)
            {
                isValid = false;
                blockingReasons.Add("Invalid position size");
            }

            if (request.PositionSize > request.AccountSize * 0.5m)
            {
                isValid = false;
                blockingReasons.Add("Position size exceeds 50% of account");
            }

            await _persistence.LogOfflineValidation(operationId, request, isValid, blockingReasons);

            return new ResilientTradeResult
            {
                Success = isValid,
                TradeExecuted = false,
                ReasonCode = isValid ? "OFFLINE_VALIDATION_PASSED" : "OFFLINE_VALIDATION_FAILED",
                Message = isValid ?
                    "Trade validated via offline rules, manual review required" :
                    $"Trade blocked by offline validation: {string.Join(", ", blockingReasons)}",
                OperationId = operationId
            };
        }

        /// <summary>
        /// Track successful operations for resilience monitoring
        /// </summary>
        private async Task TrackSuccessfulOperation(string strategy, string operationId)
        {
            _lastSuccessfulValidation[strategy] = DateTime.UtcNow;
            _consecutiveFailures[strategy] = 0;

            await _persistence.LogSuccessfulOperation(strategy, operationId, DateTime.UtcNow);

            // Check if we can exit emergency mode
            if (_emergencyMode && _consecutiveFailures.Values.All(f => f == 0))
            {
                _emergencyMode = false;
                Console.WriteLine("✅ EMERGENCY MODE DEACTIVATED - All systems operational");
            }
        }

        /// <summary>
        /// Track failed operations for resilience monitoring
        /// </summary>
        private async Task TrackFailedOperation(string strategy, Exception error, string operationId)
        {
            _consecutiveFailures[strategy] = _consecutiveFailures.GetValueOrDefault(strategy, 0) + 1;

            await _persistence.LogFailedOperation(strategy, operationId, error, DateTime.UtcNow);

            // Activate emergency mode on repeated failures
            if (_consecutiveFailures[strategy] >= 3)
            {
                _emergencyMode = true;
                Console.WriteLine($"🚨 EMERGENCY MODE ACTIVATED - {strategy} has {_consecutiveFailures[strategy]} consecutive failures");
            }
        }

        /// <summary>
        /// Get resilience status report
        /// </summary>
        public async Task<ResilienceStatus> GetResilienceStatus()
        {
            var healthStatus = await _healthChecker.CheckSystemHealth();

            return new ResilienceStatus
            {
                EmergencyMode = _emergencyMode,
                CircuitBreakerState = _circuitBreaker.State.ToString(),
                HealthStatus = healthStatus,
                LastSuccessfulValidations = _lastSuccessfulValidation.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                ConsecutiveFailures = _consecutiveFailures.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Resilient trade result with additional resilience information
    /// </summary>
    public class ResilientTradeResult : GuardedTradeResult
    {
        public string OperationId { get; set; } = "";
        public ResilienceInfo ResilienceInfo { get; set; } = new();
        public string FallbackReason { get; set; } = "";

        public ResilientTradeResult() { }

        public ResilientTradeResult(GuardedTradeResult baseResult)
        {
            Success = baseResult.Success;
            TradeExecuted = baseResult.TradeExecuted;
            TradeResult = baseResult.TradeResult;
            ValidationResult = baseResult.ValidationResult;
            PositionSizeAdjusted = baseResult.PositionSizeAdjusted;
            OriginalPositionSize = baseResult.OriginalPositionSize;
            AdjustedPositionSize = baseResult.AdjustedPositionSize;
            ReasonCode = baseResult.ReasonCode;
            Message = baseResult.Message;
            Timestamp = baseResult.Timestamp;
        }
    }

    /// <summary>
    /// Resilient validation result with cross-validation information
    /// </summary>
    public class ResilientValidationResult
    {
        public bool IsValid { get; set; }
        public string ValidationMethod { get; set; } = "";
        public bool? PrimaryResult { get; set; }
        public bool? BackupResult { get; set; }
        public string OperationId { get; set; } = "";
        public string FallbackReason { get; set; } = "";
    }

    /// <summary>
    /// Resilience information for monitoring
    /// </summary>
    public class ResilienceInfo
    {
        public string CircuitBreakerState { get; set; } = "";
        public string HealthStatus { get; set; } = "";
        public bool EmergencyMode { get; set; }
        public int RetryAttempts { get; set; }
        public TimeSpan Duration { get; set; }
    }

    /// <summary>
    /// Overall resilience status
    /// </summary>
    public class ResilienceStatus
    {
        public bool EmergencyMode { get; set; }
        public string CircuitBreakerState { get; set; } = "";
        public HealthStatus HealthStatus { get; set; } = new();
        public Dictionary<string, DateTime> LastSuccessfulValidations { get; set; } = new();
        public Dictionary<string, int> ConsecutiveFailures { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }
}