namespace ODTE.Strategy.ProcessWindow
{
    /// <summary>
    /// Process Window Trade Guard - Prevents catastrophic trading failures
    /// Wraps trade execution with automatic parameter validation
    /// 
    /// CRITICAL: This prevents disasters like the 2.5% vs 3.5% Iron Condor credit bug
    /// that caused 0% returns instead of 29.81% CAGR
    /// </summary>
    public class ProcessWindowTradeGuard
    {
        private readonly ProcessWindowValidator _validator;
        private readonly ITradeExecutor _tradeExecutor;
        private readonly List<string> _suspendedStrategies = new();
        private readonly Dictionary<string, decimal> _positionSizeReductions = new();

        public ProcessWindowTradeGuard(ProcessWindowValidator validator, ITradeExecutor tradeExecutor)
        {
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _tradeExecutor = tradeExecutor ?? throw new ArgumentNullException(nameof(tradeExecutor));
        }

        /// <summary>
        /// Execute trade with automatic process window validation
        /// CRITICAL: This is the main protection against parameter catastrophes
        /// </summary>
        public async Task<GuardedTradeResult> ExecuteTradeWithGuard(TradeRequest request)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                // Step 1: Check if strategy is suspended
                if (_suspendedStrategies.Contains(request.Strategy))
                {
                    return new GuardedTradeResult
                    {
                        Success = false,
                        TradeExecuted = false,
                        ReasonCode = "STRATEGY_SUSPENDED",
                        Message = $"Strategy {request.Strategy} is suspended due to process window violations",
                        Timestamp = startTime
                    };
                }

                // Step 2: Create execution context
                var context = CreateExecutionContext(request);

                // Step 3: Validate process window parameters
                var validation = await _validator.ValidateTradeParameters(context);

                if (!validation.IsValid)
                {
                    // Suspend strategy on critical violations
                    if (validation.SystemStatus.ShouldSuspendTrading)
                    {
                        _suspendedStrategies.Add(request.Strategy);

                        return new GuardedTradeResult
                        {
                            Success = false,
                            TradeExecuted = false,
                            ReasonCode = "PROCESS_WINDOW_VIOLATION",
                            Message = $"Trade blocked - Critical process window violations detected. Strategy suspended.",
                            ValidationResult = validation,
                            Timestamp = startTime
                        };
                    }
                }

                // Step 4: Apply position size reduction if needed
                var adjustedRequest = request;
                if (validation.ShouldReduceSize)
                {
                    adjustedRequest = ApplyPositionSizeReduction(request, validation.SystemStatus);
                }

                // Step 5: Execute the trade
                var tradeResult = await _tradeExecutor.ExecuteTrade(adjustedRequest);

                // Step 6: Post-execution validation
                await ValidatePostExecution(tradeResult, validation);

                return new GuardedTradeResult
                {
                    Success = tradeResult.Success,
                    TradeExecuted = true,
                    TradeResult = tradeResult,
                    ValidationResult = validation,
                    PositionSizeAdjusted = adjustedRequest.PositionSize != request.PositionSize,
                    OriginalPositionSize = request.PositionSize,
                    AdjustedPositionSize = adjustedRequest.PositionSize,
                    ReasonCode = tradeResult.Success ? "SUCCESS" : tradeResult.ErrorCode,
                    Message = tradeResult.Success ? "Trade executed successfully with process window validation" : tradeResult.ErrorMessage,
                    Timestamp = startTime
                };
            }
            catch (Exception ex)
            {
                return new GuardedTradeResult
                {
                    Success = false,
                    TradeExecuted = false,
                    ReasonCode = "GUARD_ERROR",
                    Message = $"Process window guard error: {ex.Message}",
                    Timestamp = startTime
                };
            }
        }

        /// <summary>
        /// Special validation for Iron Condor trades (the critical one)
        /// </summary>
        public async Task<bool> ValidateIronCondorBeforeExecution(decimal positionSize, decimal expectedCredit, decimal vix, string context = "")
        {
            // This is THE critical check that prevents the 2.5% vs 3.5% disaster
            var isValid = await _validator.ValidateIronCondorCredit(expectedCredit, positionSize, vix, context);

            if (!isValid)
            {
                Console.WriteLine($"🚨 IRON CONDOR EXECUTION BLOCKED: Credit validation failed");
                Console.WriteLine($"   Position: ${positionSize:F2}");
                Console.WriteLine($"   Expected Credit: ${expectedCredit:F2}");
                Console.WriteLine($"   Credit %: {(expectedCredit / positionSize):P2}");
                Console.WriteLine($"   VIX: {vix:F1}");
                Console.WriteLine($"   Context: {context}");
            }

            return isValid;
        }

        /// <summary>
        /// Monitor live positions for parameter drift
        /// </summary>
        public async Task<ProcessWindowSystemStatus> MonitorLivePositions(List<LivePosition> positions)
        {
            var allParameters = new Dictionary<string, decimal>();
            var context = $"Live monitoring of {positions.Count} positions";

            foreach (var position in positions)
            {
                var positionParams = ExtractParametersFromPosition(position);

                // Merge parameters (using latest values for duplicates)
                foreach (var param in positionParams)
                {
                    allParameters[param.Key] = param.Value;
                }
            }

            var systemStatus = await _validator.MonitorLiveTradingParameters(allParameters, context);

            // Handle any critical violations
            if (systemStatus.ShouldSuspendTrading)
            {
                await HandleCriticalViolations(systemStatus, positions);
            }

            return systemStatus;
        }

        /// <summary>
        /// Create execution context from trade request
        /// </summary>
        private TradeExecutionContext CreateExecutionContext(TradeRequest request)
        {
            return new TradeExecutionContext
            {
                Strategy = request.Strategy,
                PositionSize = request.PositionSize,
                AccountSize = request.AccountSize,
                ExpectedCredit = request.ExpectedCredit,
                VIX = request.CurrentVIX,
                CommissionPerLeg = request.CommissionPerLeg,
                SlippagePerLeg = request.SlippagePerLeg,
                AdditionalParameters = request.AdditionalParameters ?? new Dictionary<string, decimal>(),
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Apply position size reduction based on process window warnings
        /// </summary>
        private TradeRequest ApplyPositionSizeReduction(TradeRequest original, ProcessWindowSystemStatus status)
        {
            var reductionFactor = CalculateReductionFactor(status);
            var adjustedSize = original.PositionSize * reductionFactor;

            // Store reduction for tracking
            _positionSizeReductions[original.Strategy] = reductionFactor;

            Console.WriteLine($"⚡ POSITION SIZE REDUCED: {original.Strategy}");
            Console.WriteLine($"   Original: ${original.PositionSize:F2}");
            Console.WriteLine($"   Reduced: ${adjustedSize:F2} ({reductionFactor:P0})");

            return new TradeRequest
            {
                Strategy = original.Strategy,
                PositionSize = adjustedSize,
                AccountSize = original.AccountSize,
                ExpectedCredit = original.ExpectedCredit * reductionFactor, // Scale credit proportionally
                CurrentVIX = original.CurrentVIX,
                CommissionPerLeg = original.CommissionPerLeg,
                SlippagePerLeg = original.SlippagePerLeg,
                AdditionalParameters = original.AdditionalParameters
            };
        }

        /// <summary>
        /// Calculate position size reduction factor based on violations
        /// </summary>
        private decimal CalculateReductionFactor(ProcessWindowSystemStatus status)
        {
            // Aggressive reduction for critical violations
            if (status.CriticalViolations > 0)
                return 0.25m; // 75% reduction

            // Moderate reduction for multiple warnings
            if (status.WarningCount >= 3)
                return 0.50m; // 50% reduction

            // Conservative reduction for some warnings
            if (status.WarningCount >= 2)
                return 0.75m; // 25% reduction

            // Minimal reduction for single warnings
            return 0.90m; // 10% reduction
        }

        /// <summary>
        /// Extract parameters from live position for monitoring
        /// </summary>
        private Dictionary<string, decimal> ExtractParametersFromPosition(LivePosition position)
        {
            var parameters = new Dictionary<string, decimal>();

            // Current P&L as indicator of parameter health
            if (position.AccountSize > 0)
            {
                parameters["PositionSizePct"] = position.PositionSize / position.AccountSize;
            }

            // Strategy-specific parameter extraction
            if (position.Strategy.Contains("IronCondor"))
            {
                // Reverse-calculate credit percentage from realized P&L
                if (position.RealizedCredit > 0 && position.PositionSize > 0)
                {
                    var impliedCreditPct = position.RealizedCredit / position.PositionSize;
                    parameters["IronCondorCreditPct"] = impliedCreditPct;
                }
            }

            // Add position-specific parameters
            foreach (var param in position.AdditionalParameters)
            {
                parameters[param.Key] = param.Value;
            }

            return parameters;
        }

        /// <summary>
        /// Handle critical violations that require immediate action
        /// </summary>
        private async Task HandleCriticalViolations(ProcessWindowSystemStatus status, List<LivePosition> positions)
        {
            Console.WriteLine("🚨 CRITICAL VIOLATIONS DETECTED - TAKING PROTECTIVE ACTION");

            // Suspend all strategies with violations
            foreach (var result in status.Results)
            {
                if (result.Status == ProcessWindowMonitor.WindowStatus.BlackSwan || result.Status == ProcessWindowMonitor.WindowStatus.RedZone)
                {
                    var affectedStrategies = positions
                        .Where(p => p.Strategy.Contains(result.Parameter.Replace("Pct", "").Replace("Credit", "")))
                        .Select(p => p.Strategy)
                        .Distinct();

                    foreach (var strategy in affectedStrategies)
                    {
                        if (!_suspendedStrategies.Contains(strategy))
                        {
                            _suspendedStrategies.Add(strategy);
                            Console.WriteLine($"   └─ SUSPENDED: {strategy}");
                        }
                    }
                }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Validate parameters after trade execution
        /// </summary>
        private async Task ValidatePostExecution(TradeResult tradeResult, TradeValidationResult preValidation)
        {
            if (!tradeResult.Success)
                return;

            // Check if actual execution matched expectations
            var actualCredit = tradeResult.ActualCredit;
            var expectedCredit = preValidation.SystemStatus.Results
                .FirstOrDefault(r => r.Parameter.Contains("Credit"))?.CurrentValue ?? 0;

            if (expectedCredit > 0)
            {
                var creditDifference = Math.Abs(actualCredit - expectedCredit);
                var creditDifferencePct = creditDifference / expectedCredit;

                if (creditDifferencePct > 0.10m) // More than 10% difference
                {
                    Console.WriteLine($"⚠️  POST-EXECUTION WARNING: Credit mismatch");
                    Console.WriteLine($"   Expected: ${expectedCredit:F2}");
                    Console.WriteLine($"   Actual: ${actualCredit:F2}");
                    Console.WriteLine($"   Difference: {creditDifferencePct:P1}");
                }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Resume suspended strategy after manual review
        /// </summary>
        public void ResumeStrategy(string strategy, string reason)
        {
            if (_suspendedStrategies.Remove(strategy))
            {
                Console.WriteLine($"✅ STRATEGY RESUMED: {strategy} - Reason: {reason}");
            }
        }

        /// <summary>
        /// Get list of currently suspended strategies
        /// </summary>
        public List<string> GetSuspendedStrategies()
        {
            return _suspendedStrategies.ToList();
        }

        /// <summary>
        /// Get position size reduction history
        /// </summary>
        public Dictionary<string, decimal> GetPositionSizeReductions()
        {
            return _positionSizeReductions.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
    }

    /// <summary>
    /// Trade request structure
    /// </summary>
    public class TradeRequest
    {
        public string Strategy { get; set; } = "";
        public decimal PositionSize { get; set; }
        public decimal AccountSize { get; set; }
        public decimal ExpectedCredit { get; set; }
        public decimal CurrentVIX { get; set; }
        public decimal CommissionPerLeg { get; set; } = 0.65m;
        public decimal SlippagePerLeg { get; set; } = 0.025m;
        public Dictionary<string, decimal> AdditionalParameters { get; set; } = new();
    }

    /// <summary>
    /// Result of guarded trade execution
    /// </summary>
    public class GuardedTradeResult
    {
        public bool Success { get; set; }
        public bool TradeExecuted { get; set; }
        public TradeResult TradeResult { get; set; }
        public TradeValidationResult ValidationResult { get; set; }
        public bool PositionSizeAdjusted { get; set; }
        public decimal OriginalPositionSize { get; set; }
        public decimal AdjustedPositionSize { get; set; }
        public string ReasonCode { get; set; } = "";
        public string Message { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Live position for monitoring
    /// </summary>
    public class LivePosition
    {
        public string Strategy { get; set; } = "";
        public decimal PositionSize { get; set; }
        public decimal AccountSize { get; set; }
        public decimal RealizedCredit { get; set; }
        public decimal CurrentPnL { get; set; }
        public Dictionary<string, decimal> AdditionalParameters { get; set; } = new();
        public DateTime OpenTime { get; set; }
    }

    /// <summary>
    /// Trade execution result
    /// </summary>
    public class TradeResult
    {
        public bool Success { get; set; }
        public decimal ActualCredit { get; set; }
        public decimal ActualCommission { get; set; }
        public decimal ActualSlippage { get; set; }
        public string ErrorCode { get; set; } = "";
        public string ErrorMessage { get; set; } = "";
        public DateTime ExecutionTime { get; set; }
    }

    /// <summary>
    /// Interface for trade execution
    /// </summary>
    public interface ITradeExecutor
    {
        Task<TradeResult> ExecuteTrade(TradeRequest request);
    }
}