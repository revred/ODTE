namespace ODTE.Strategy.ProcessWindow
{
    /// <summary>
    /// Real-time Process Window Validator - Integrates with trading execution
    /// Prevents catastrophic failures like the Iron Condor 2.5% vs 3.5% credit bug
    /// </summary>
    public class ProcessWindowValidator
    {
        private readonly ProcessWindowMonitor _monitor;
        private readonly IProcessWindowLogger _logger;
        private readonly IAlertService _alertService;

        public ProcessWindowValidator(
            ProcessWindowMonitor monitor = null,
            IProcessWindowLogger logger = null,
            IAlertService alertService = null)
        {
            _monitor = monitor ?? new ProcessWindowMonitor();
            _logger = logger ?? new ConsoleProcessWindowLogger();
            _alertService = alertService ?? new ConsoleAlertService();
        }

        /// <summary>
        /// Validate trade execution parameters before placing order
        /// CRITICAL: This prevents the 2.5% vs 3.5% credit catastrophe
        /// </summary>
        public async Task<TradeValidationResult> ValidateTradeParameters(TradeExecutionContext context)
        {
            var parameters = ExtractParametersFromContext(context);
            var systemStatus = _monitor.CheckSystemStatus(parameters, DateTime.UtcNow,
                $"Trade validation for {context.Strategy} - Position: ${context.PositionSize}");

            // Log all results
            await _logger.LogSystemStatus(systemStatus);

            // Send alerts if needed
            if (systemStatus.ShouldSuspendTrading)
            {
                await _alertService.SendEmergencyAlert(
                    "TRADING SUSPENDED - Critical Process Window Violations",
                    systemStatus.GetSummaryMessage(),
                    systemStatus.Results);
            }
            else if (systemStatus.ShouldReducePositionSize)
            {
                await _alertService.SendWarningAlert(
                    "Position Size Reduction Recommended",
                    systemStatus.GetSummaryMessage(),
                    systemStatus.Results);
            }

            return new TradeValidationResult
            {
                IsValid = !systemStatus.ShouldSuspendTrading,
                ShouldReduceSize = systemStatus.ShouldReducePositionSize,
                SystemStatus = systemStatus,
                RecommendedAction = DetermineRecommendedAction(systemStatus),
                ValidationTimestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Extract critical parameters from trade context for validation
        /// </summary>
        private Dictionary<string, decimal> ExtractParametersFromContext(TradeExecutionContext context)
        {
            var parameters = new Dictionary<string, decimal>();

            // Iron Condor credit percentage (THE CRITICAL ONE)
            if (context.Strategy.Contains("IronCondor") || context.Strategy.Contains("IC"))
            {
                var creditPct = context.ExpectedCredit / (context.PositionSize * (1.0m + context.VIX / 100m));
                parameters["IronCondorCreditPct"] = creditPct;
            }

            // Commission structure
            parameters["CommissionPerLeg"] = context.CommissionPerLeg;

            // Slippage estimates
            parameters["SlippagePerLeg"] = context.SlippagePerLeg;

            // VIX bonus multiplier
            if (context.VIX > 0)
            {
                parameters["VixBonusMultiplier"] = 1.0m + (context.VIX / 100m);
            }

            // Position sizing
            if (context.AccountSize > 0)
            {
                parameters["PositionSizePct"] = context.PositionSize / context.AccountSize;
            }

            // Strategy-specific parameters
            AddStrategySpecificParameters(parameters, context);

            return parameters;
        }

        /// <summary>
        /// Add strategy-specific parameter validations
        /// </summary>
        private void AddStrategySpecificParameters(Dictionary<string, decimal> parameters, TradeExecutionContext context)
        {
            switch (context.Strategy.ToLower())
            {
                case var s when s.Contains("brokenWing") || s.Contains("bwb"):
                    // Broken Wing Butterfly specific validations
                    if (context.AdditionalParameters.ContainsKey("BWBCreditPct"))
                    {
                        parameters["BWBCreditPct"] = context.AdditionalParameters["BWBCreditPct"];
                    }
                    break;

                case var s when s.Contains("strangle"):
                    // Short Strangle specific validations
                    if (context.AdditionalParameters.ContainsKey("StrangleCreditPct"))
                    {
                        parameters["StrangleCreditPct"] = context.AdditionalParameters["StrangleCreditPct"];
                    }
                    break;

                case var s when s.Contains("calendar"):
                    // Calendar spread specific validations
                    if (context.AdditionalParameters.ContainsKey("CalendarCreditPct"))
                    {
                        parameters["CalendarCreditPct"] = context.AdditionalParameters["CalendarCreditPct"];
                    }
                    break;
            }
        }

        /// <summary>
        /// Determine recommended action based on system status
        /// </summary>
        private string DetermineRecommendedAction(ProcessWindowSystemStatus systemStatus)
        {
            if (systemStatus.ShouldSuspendTrading)
            {
                return "SUSPEND ALL TRADING - Critical violations detected. Investigate and fix before resuming.";
            }

            if (systemStatus.ShouldReducePositionSize)
            {
                return "REDUCE POSITION SIZE - Warning conditions detected. Use conservative sizing until resolved.";
            }

            if (systemStatus.WarningCount > 0)
            {
                return "MONITOR CLOSELY - Some parameters approaching limits. Proceed with caution.";
            }

            return "PROCEED NORMALLY - All parameters within safe bounds.";
        }

        /// <summary>
        /// Continuous monitoring of live trading parameters
        /// Call this periodically during active trading
        /// </summary>
        public async Task<ProcessWindowSystemStatus> MonitorLiveTradingParameters(
            Dictionary<string, decimal> currentParameters, string context = "Live monitoring")
        {
            var systemStatus = _monitor.CheckSystemStatus(currentParameters, DateTime.UtcNow, context);

            // Log monitoring results
            await _logger.LogSystemStatus(systemStatus);

            // Send alerts for any violations
            foreach (var result in systemStatus.Results)
            {
                if (result.AlertLevel == ProcessWindowMonitor.AlertLevel.Emergency || result.AlertLevel == ProcessWindowMonitor.AlertLevel.Critical)
                {
                    await _alertService.SendCriticalAlert(
                        $"Process Window Violation: {result.Parameter}",
                        result.Message,
                        new List<ProcessWindowResult> { result });
                }
                else if (result.AlertLevel == ProcessWindowMonitor.AlertLevel.Warning)
                {
                    await _alertService.SendWarningAlert(
                        $"Process Window Warning: {result.Parameter}",
                        result.Message,
                        new List<ProcessWindowResult> { result });
                }
            }

            return systemStatus;
        }

        /// <summary>
        /// Validate specific parameter against its process window
        /// </summary>
        public async Task<ProcessWindowResult> ValidateParameter(string parameterName, decimal value, string context = "")
        {
            var result = _monitor.CheckParameter(parameterName, value, DateTime.UtcNow, context);

            await _logger.LogParameterCheck(result);

            if (result.AlertLevel == ProcessWindowMonitor.AlertLevel.Emergency || result.AlertLevel == ProcessWindowMonitor.AlertLevel.Critical)
            {
                await _alertService.SendCriticalAlert(
                    $"Critical Parameter Violation: {parameterName}",
                    result.Message,
                    new List<ProcessWindowResult> { result });
            }

            return result;
        }

        /// <summary>
        /// Check if Iron Condor credit percentage is safe
        /// CRITICAL: Prevents the 2.5% vs 3.5% catastrophe
        /// </summary>
        public async Task<bool> ValidateIronCondorCredit(decimal creditAmount, decimal positionSize, decimal vix, string context = "")
        {
            var creditPct = creditAmount / (positionSize * (1.0m + vix / 100m));
            var result = await ValidateParameter("IronCondorCreditPct", creditPct,
                $"Iron Condor validation: ${creditAmount} credit on ${positionSize} position, VIX={vix}. {context}");

            return result.Status == ProcessWindowMonitor.WindowStatus.GreenZone || result.Status == ProcessWindowMonitor.WindowStatus.YellowZone;
        }

        /// <summary>
        /// Get historical violation summary for reporting
        /// </summary>
        public ProcessWindowSummary GetViolationSummary(TimeSpan? period = null)
        {
            return _monitor.GetViolationSummary(period);
        }

        /// <summary>
        /// Export violation history for analysis
        /// </summary>
        public List<ProcessWindowViolation> GetViolationHistory(TimeSpan? period = null)
        {
            return _monitor.GetViolationHistory(period);
        }
    }

    /// <summary>
    /// Trading execution context for parameter validation
    /// </summary>
    public class TradeExecutionContext
    {
        public string Strategy { get; set; } = "";
        public decimal PositionSize { get; set; }
        public decimal AccountSize { get; set; }
        public decimal ExpectedCredit { get; set; }
        public decimal VIX { get; set; }
        public decimal CommissionPerLeg { get; set; } = 0.65m;  // Default to $0.65
        public decimal SlippagePerLeg { get; set; } = 0.025m;   // Default to $0.025
        public Dictionary<string, decimal> AdditionalParameters { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Result of trade validation
    /// </summary>
    public class TradeValidationResult
    {
        public bool IsValid { get; set; }
        public bool ShouldReduceSize { get; set; }
        public ProcessWindowSystemStatus SystemStatus { get; set; }
        public string RecommendedAction { get; set; } = "";
        public DateTime ValidationTimestamp { get; set; }
    }

    /// <summary>
    /// Interface for process window logging
    /// </summary>
    public interface IProcessWindowLogger
    {
        Task LogSystemStatus(ProcessWindowSystemStatus status);
        Task LogParameterCheck(ProcessWindowResult result);
        Task LogViolation(ProcessWindowViolation violation);
    }

    /// <summary>
    /// Interface for alerting service
    /// </summary>
    public interface IAlertService
    {
        Task SendEmergencyAlert(string title, string message, List<ProcessWindowResult> violations);
        Task SendCriticalAlert(string title, string message, List<ProcessWindowResult> violations);
        Task SendWarningAlert(string title, string message, List<ProcessWindowResult> violations);
    }

    /// <summary>
    /// Console-based logger implementation
    /// </summary>
    public class ConsoleProcessWindowLogger : IProcessWindowLogger
    {
        public async Task LogSystemStatus(ProcessWindowSystemStatus status)
        {
            Console.WriteLine($"[{status.Timestamp:HH:mm:ss}] Process Window Status: {status.GetSummaryMessage()}");

            foreach (var result in status.Results)
            {
                if (result.AlertLevel != ProcessWindowMonitor.AlertLevel.Info)
                {
                    Console.WriteLine($"  └─ {result.Message}");
                }
            }

            await Task.CompletedTask;
        }

        public async Task LogParameterCheck(ProcessWindowResult result)
        {
            if (result.AlertLevel != ProcessWindowMonitor.AlertLevel.Info)
            {
                Console.WriteLine($"[{result.Timestamp:HH:mm:ss}] {result.Message}");
            }
            await Task.CompletedTask;
        }

        public async Task LogViolation(ProcessWindowViolation violation)
        {
            Console.WriteLine($"[{violation.Timestamp:HH:mm:ss}] VIOLATION LOGGED: {violation.Message}");
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Console-based alert service implementation
    /// </summary>
    public class ConsoleAlertService : IAlertService
    {
        public async Task SendEmergencyAlert(string title, string message, List<ProcessWindowResult> violations)
        {
            Console.WriteLine($"\n🚨🚨🚨 EMERGENCY ALERT 🚨🚨🚨");
            Console.WriteLine($"Title: {title}");
            Console.WriteLine($"Message: {message}");
            Console.WriteLine($"Violations: {violations.Count}");
            foreach (var violation in violations)
            {
                Console.WriteLine($"  - {violation.Message}");
            }
            Console.WriteLine("🚨🚨🚨 END EMERGENCY ALERT 🚨🚨🚨\n");
            await Task.CompletedTask;
        }

        public async Task SendCriticalAlert(string title, string message, List<ProcessWindowResult> violations)
        {
            Console.WriteLine($"\n⚠️  CRITICAL ALERT ⚠️ ");
            Console.WriteLine($"Title: {title}");
            Console.WriteLine($"Message: {message}");
            foreach (var violation in violations)
            {
                Console.WriteLine($"  - {violation.Message}");
            }
            Console.WriteLine("⚠️  END CRITICAL ALERT ⚠️ \n");
            await Task.CompletedTask;
        }

        public async Task SendWarningAlert(string title, string message, List<ProcessWindowResult> violations)
        {
            Console.WriteLine($"\n⚡ WARNING ALERT ⚡");
            Console.WriteLine($"Title: {title}");
            Console.WriteLine($"Message: {message}");
            foreach (var violation in violations)
            {
                Console.WriteLine($"  - {violation.Message}");
            }
            Console.WriteLine("⚡ END WARNING ALERT ⚡\n");
            await Task.CompletedTask;
        }
    }
}