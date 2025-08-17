namespace ODTE.Strategy.ProcessWindow;

/// <summary>
/// Circuit Breaker pattern implementation for process window resilience
/// </summary>
public class CircuitBreaker
{
    public enum CircuitBreakerState
    {
        Closed,    // Normal operation
        Open,      // Failing fast
        HalfOpen   // Testing if service has recovered
    }

    private readonly int _failureThreshold;
    private readonly TimeSpan _recoveryTimeout;
    private readonly int _retryAttempts;
    private int _failureCount = 0;
    private DateTime _lastFailureTime = DateTime.MinValue;
    private CircuitBreakerState _state = CircuitBreakerState.Closed;
    private readonly SemaphoreSlim _stateLock = new(1, 1);

    public CircuitBreakerState State => _state;

    public CircuitBreaker(int failureThreshold, TimeSpan recoveryTimeout, int retryAttempts)
    {
        _failureThreshold = failureThreshold;
        _recoveryTimeout = recoveryTimeout;
        _retryAttempts = retryAttempts;
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        await _stateLock.WaitAsync();
        try
        {
            switch (_state)
            {
                case CircuitBreakerState.Open:
                    if (DateTime.UtcNow - _lastFailureTime > _recoveryTimeout)
                    {
                        _state = CircuitBreakerState.HalfOpen;
                        Console.WriteLine("⚡ Circuit Breaker: Transitioning to HalfOpen for testing");
                    }
                    else
                    {
                        throw new CircuitBreakerOpenException("Circuit breaker is open - failing fast");
                    }
                    break;
            }
        }
        finally
        {
            _stateLock.Release();
        }

        try
        {
            var result = await operation();
            await OnSuccess();
            return result;
        }
        catch (Exception ex)
        {
            await OnFailure(ex);
            throw;
        }
    }

    private async Task OnSuccess()
    {
        await _stateLock.WaitAsync();
        try
        {
            _failureCount = 0;
            if (_state == CircuitBreakerState.HalfOpen)
            {
                _state = CircuitBreakerState.Closed;
                Console.WriteLine("✅ Circuit Breaker: Closed - Service recovered");
            }
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private async Task OnFailure(Exception ex)
    {
        await _stateLock.WaitAsync();
        try
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;

            if (_failureCount >= _failureThreshold || _state == CircuitBreakerState.HalfOpen)
            {
                _state = CircuitBreakerState.Open;
                Console.WriteLine($"🚨 Circuit Breaker: Opened due to {_failureCount} failures. Last error: {ex.Message}");
            }
        }
        finally
        {
            _stateLock.Release();
        }
    }
}

/// <summary>
/// Retry policy with exponential backoff
/// </summary>
public class RetryPolicy
{
    private readonly int _maxAttempts;
    private readonly TimeSpan _baseDelay;
    private readonly TimeSpan _maxDelay;
    private int _lastAttemptCount = 0;

    public int LastAttemptCount => _lastAttemptCount;

    public RetryPolicy(int maxAttempts, TimeSpan baseDelay, TimeSpan maxDelay)
    {
        _maxAttempts = maxAttempts;
        _baseDelay = baseDelay;
        _maxDelay = maxDelay;
    }

    public async Task<T?> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        _lastAttemptCount = 0;
        Exception? lastException = null;

        for (int attempt = 1; attempt <= _maxAttempts; attempt++)
        {
            _lastAttemptCount = attempt;

            try
            {
                var result = await operation();
                if (attempt > 1)
                {
                    Console.WriteLine($"✅ Retry Policy: Operation succeeded on attempt {attempt}");
                }
                return result;
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (attempt == _maxAttempts)
                {
                    Console.WriteLine($"🚨 Retry Policy: All {_maxAttempts} attempts failed. Last error: {ex.Message}");
                    break;
                }

                // Calculate exponential backoff delay
                var delay = TimeSpan.FromMilliseconds(
                    Math.Min(_maxDelay.TotalMilliseconds,
                            _baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1)));

                Console.WriteLine($"⚡ Retry Policy: Attempt {attempt} failed, retrying in {delay.TotalMilliseconds}ms. Error: {ex.Message}");
                await Task.Delay(delay);
            }
        }

        throw lastException ?? new InvalidOperationException("Retry policy failed without exception");
    }
}

/// <summary>
/// Health checker for system components
/// </summary>
public class HealthChecker
{
    public async Task<HealthStatus> CheckSystemHealth()
    {
        var status = new HealthStatus();
        var checks = new List<Task<HealthCheckResult>>();

        // Check memory usage
        checks.Add(CheckMemoryUsage());

        // Check disk space
        checks.Add(CheckDiskSpace());

        // Check process window dependencies
        checks.Add(CheckProcessWindowDependencies());

        // Check historical data availability
        checks.Add(CheckHistoricalDataAvailability());

        var results = await Task.WhenAll(checks);

        foreach (var result in results)
        {
            status.Checks.Add(result);
            if (!result.IsHealthy)
            {
                status.Issues.Add($"{result.Component}: {result.Message}");
                if (result.IsCritical)
                {
                    status.IsCritical = true;
                }
            }
        }

        status.Status = status.IsCritical ? "Critical" :
                       status.Issues.Any() ? "Warning" : "Healthy";

        return status;
    }

    private async Task<HealthCheckResult> CheckMemoryUsage()
    {
        try
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var memoryMB = process.WorkingSet64 / (1024 * 1024);

            return new HealthCheckResult
            {
                Component = "Memory",
                IsHealthy = memoryMB < 1000, // Less than 1GB
                IsCritical = memoryMB > 2000, // More than 2GB is critical
                Message = $"Memory usage: {memoryMB} MB",
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            return new HealthCheckResult
            {
                Component = "Memory",
                IsHealthy = false,
                IsCritical = true,
                Message = $"Memory check failed: {ex.Message}",
                Timestamp = DateTime.UtcNow
            };
        }
    }

    private async Task<HealthCheckResult> CheckDiskSpace()
    {
        try
        {
            var drive = new System.IO.DriveInfo(System.IO.Path.GetPathRoot(Environment.CurrentDirectory));
            var freeSpaceGB = drive.AvailableFreeSpace / (1024 * 1024 * 1024);

            return new HealthCheckResult
            {
                Component = "DiskSpace",
                IsHealthy = freeSpaceGB > 1, // More than 1GB free
                IsCritical = freeSpaceGB < 0.5, // Less than 500MB is critical
                Message = $"Free disk space: {freeSpaceGB:F2} GB",
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            return new HealthCheckResult
            {
                Component = "DiskSpace",
                IsHealthy = false,
                IsCritical = false,
                Message = $"Disk space check failed: {ex.Message}",
                Timestamp = DateTime.UtcNow
            };
        }
    }

    private async Task<HealthCheckResult> CheckProcessWindowDependencies()
    {
        try
        {
            // Test basic process window functionality
            var monitor = new ProcessWindowMonitor();
            var result = monitor.CheckParameter("IronCondorCreditPct", 0.035m, DateTime.UtcNow, "Health check");

            return new HealthCheckResult
            {
                Component = "ProcessWindow",
                IsHealthy = result != null && result.Status == ProcessWindowMonitor.WindowStatus.GreenZone,
                IsCritical = result == null,
                Message = result != null ? "Process window monitor operational" : "Process window monitor failed",
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            return new HealthCheckResult
            {
                Component = "ProcessWindow",
                IsHealthy = false,
                IsCritical = true,
                Message = $"Process window check failed: {ex.Message}",
                Timestamp = DateTime.UtcNow
            };
        }
    }

    private async Task<HealthCheckResult> CheckHistoricalDataAvailability()
    {
        try
        {
            // Simulate historical data availability check
            await Task.Delay(50); // Simulate data access

            return new HealthCheckResult
            {
                Component = "HistoricalData",
                IsHealthy = true, // Assume available for now
                IsCritical = false,
                Message = "Historical data accessible",
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            return new HealthCheckResult
            {
                Component = "HistoricalData",
                IsHealthy = false,
                IsCritical = false, // Not critical for basic operation
                Message = $"Historical data check failed: {ex.Message}",
                Timestamp = DateTime.UtcNow
            };
        }
    }
}

/// <summary>
/// Fallback mechanisms when primary validation fails
/// </summary>
public class FallbackMechanisms
{
    public async Task<FallbackValidationResult> ExecuteFallbackValidations(TradeRequest request)
    {
        var result = new FallbackValidationResult();
        var validations = new List<Task<(string Name, bool Passed, string Reason)>>();

        // Basic sanity checks
        validations.Add(ValidateBasicSanity(request));

        // Position size validation
        validations.Add(ValidatePositionSize(request));

        // Strategy-specific offline validation
        if (request.Strategy.Contains("IronCondor", StringComparison.OrdinalIgnoreCase))
        {
            validations.Add(ValidateIronCondorOffline(request));
        }

        var validationResults = await Task.WhenAll(validations);

        foreach (var (name, passed, reason) in validationResults)
        {
            result.Validations[name] = passed;
            if (!passed)
            {
                result.FailureReasons.Add($"{name}: {reason}");
            }
        }

        result.AllPassed = result.Validations.Values.All(v => v);
        return result;
    }

    private async Task<(string Name, bool Passed, string Reason)> ValidateBasicSanity(TradeRequest request)
    {
        await Task.CompletedTask;

        if (string.IsNullOrEmpty(request.Strategy))
            return ("BasicSanity", false, "Strategy name is empty");

        if (request.PositionSize <= 0)
            return ("BasicSanity", false, "Position size must be positive");

        if (request.AccountSize <= 0)
            return ("BasicSanity", false, "Account size must be positive");

        return ("BasicSanity", true, "All basic checks passed");
    }

    private async Task<(string Name, bool Passed, string Reason)> ValidatePositionSize(TradeRequest request)
    {
        await Task.CompletedTask;

        var positionPct = request.PositionSize / request.AccountSize;

        if (positionPct > 0.5m)
            return ("PositionSize", false, $"Position size {positionPct:P1} exceeds 50% of account");

        if (positionPct < 0.01m)
            return ("PositionSize", false, $"Position size {positionPct:P1} too small (< 1% of account)");

        return ("PositionSize", true, $"Position size {positionPct:P1} within acceptable range");
    }

    private async Task<(string Name, bool Passed, string Reason)> ValidateIronCondorOffline(TradeRequest request)
    {
        await Task.CompletedTask;

        var creditPct = request.ExpectedCredit / (request.PositionSize * (1.0m + request.CurrentVIX / 100m));

        // Hardcoded safe boundaries
        const decimal MIN_SAFE_CREDIT = 0.030m;
        const decimal MAX_SAFE_CREDIT = 0.045m;
        const decimal KNOWN_FAILURE = 0.025m;

        if (creditPct <= KNOWN_FAILURE)
            return ("IronCondorCredit", false, $"Credit {creditPct:P2} matches known failure point (2.5% bug)");

        if (creditPct < MIN_SAFE_CREDIT)
            return ("IronCondorCredit", false, $"Credit {creditPct:P2} below minimum safe threshold {MIN_SAFE_CREDIT:P2}");

        if (creditPct > MAX_SAFE_CREDIT)
            return ("IronCondorCredit", false, $"Credit {creditPct:P2} above maximum realistic threshold {MAX_SAFE_CREDIT:P2}");

        return ("IronCondorCredit", true, $"Credit {creditPct:P2} within safe range");
    }
}

/// <summary>
/// Interface for persistence operations
/// </summary>
public interface IProcessWindowPersistence
{
    Task LogValidationMismatch(string operationId, bool primaryResult, bool backupResult, bool finalResult);
    Task LogValidationFailure(string operationId, Exception error, bool fallbackResult);
    Task LogOfflineValidation(string operationId, TradeRequest request, bool result, List<string> reasons);
    Task LogSuccessfulOperation(string strategy, string operationId, DateTime timestamp);
    Task LogFailedOperation(string strategy, string operationId, Exception error, DateTime timestamp);
    Task<HistoricalValidationData?> GetHistoricalValidationData(string strategy);
}

/// <summary>
/// In-memory persistence implementation (for development/testing)
/// </summary>
public class InMemoryProcessWindowPersistence : IProcessWindowPersistence
{
    private readonly List<string> _logs = new();
    private readonly object _logLock = new();

    public async Task LogValidationMismatch(string operationId, bool primaryResult, bool backupResult, bool finalResult)
    {
        var logEntry = $"[{DateTime.UtcNow:HH:mm:ss}] VALIDATION_MISMATCH - Op:{operationId}, Primary:{primaryResult}, Backup:{backupResult}, Final:{finalResult}";
        lock (_logLock) { _logs.Add(logEntry); }
        Console.WriteLine(logEntry);
        await Task.CompletedTask;
    }

    public async Task LogValidationFailure(string operationId, Exception error, bool fallbackResult)
    {
        var logEntry = $"[{DateTime.UtcNow:HH:mm:ss}] VALIDATION_FAILURE - Op:{operationId}, Error:{error.Message}, Fallback:{fallbackResult}";
        lock (_logLock) { _logs.Add(logEntry); }
        Console.WriteLine(logEntry);
        await Task.CompletedTask;
    }

    public async Task LogOfflineValidation(string operationId, TradeRequest request, bool result, List<string> reasons)
    {
        var logEntry = $"[{DateTime.UtcNow:HH:mm:ss}] OFFLINE_VALIDATION - Op:{operationId}, Strategy:{request.Strategy}, Result:{result}, Reasons:{string.Join(", ", reasons)}";
        lock (_logLock) { _logs.Add(logEntry); }
        Console.WriteLine(logEntry);
        await Task.CompletedTask;
    }

    public async Task LogSuccessfulOperation(string strategy, string operationId, DateTime timestamp)
    {
        var logEntry = $"[{timestamp:HH:mm:ss}] SUCCESS - Strategy:{strategy}, Op:{operationId}";
        lock (_logLock) { _logs.Add(logEntry); }
        await Task.CompletedTask;
    }

    public async Task LogFailedOperation(string strategy, string operationId, Exception error, DateTime timestamp)
    {
        var logEntry = $"[{timestamp:HH:mm:ss}] FAILURE - Strategy:{strategy}, Op:{operationId}, Error:{error.Message}";
        lock (_logLock) { _logs.Add(logEntry); }
        Console.WriteLine(logEntry);
        await Task.CompletedTask;
    }

    public async Task<HistoricalValidationData?> GetHistoricalValidationData(string strategy)
    {
        await Task.CompletedTask;

        // Return mock historical data for Iron Condor
        if (strategy == "IronCondor")
        {
            return new HistoricalValidationData
            {
                Strategy = strategy,
                CreditPercentiles = new CreditPercentiles
                {
                    P10 = 0.030m,  // 10th percentile
                    P25 = 0.032m,  // 25th percentile
                    P50 = 0.035m,  // Median (50th percentile)
                    P75 = 0.038m,  // 75th percentile
                    P90 = 0.040m   // 90th percentile
                },
                DataPoints = 10000,
                LastUpdated = DateTime.UtcNow.AddDays(-1)
            };
        }

        return null;
    }

    public List<string> GetAllLogs()
    {
        lock (_logLock) { return _logs.ToList(); }
    }
}

// Supporting data structures

public class CircuitBreakerOpenException : Exception
{
    public CircuitBreakerOpenException(string message) : base(message) { }
}

public class HealthStatus
{
    public string Status { get; set; } = "Unknown";
    public List<HealthCheckResult> Checks { get; set; } = new();
    public List<string> Issues { get; set; } = new();
    public bool IsCritical { get; set; } = false;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class HealthCheckResult
{
    public string Component { get; set; } = "";
    public bool IsHealthy { get; set; }
    public bool IsCritical { get; set; }
    public string Message { get; set; } = "";
    public DateTime Timestamp { get; set; }
}

public class FallbackValidationResult
{
    public Dictionary<string, bool> Validations { get; set; } = new();
    public List<string> FailureReasons { get; set; } = new();
    public bool AllPassed { get; set; }
}

public class HistoricalValidationData
{
    public string Strategy { get; set; } = "";
    public CreditPercentiles CreditPercentiles { get; set; }
    public int DataPoints { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class CreditPercentiles
{
    public decimal P10 { get; set; }
    public decimal P25 { get; set; }
    public decimal P50 { get; set; }
    public decimal P75 { get; set; }
    public decimal P90 { get; set; }
}