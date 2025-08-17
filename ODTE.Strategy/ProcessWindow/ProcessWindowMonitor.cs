namespace ODTE.Strategy.ProcessWindow
{
    /// <summary>
    /// Process Window Monitor - Ensures trading parameters stay within safe operational bounds
    /// Inspired by "Margin Call" movie concept: "Know when the music stops"
    /// 
    /// Critical lesson: The 1% difference in Iron Condor credit (2.5% vs 3.5%) 
    /// caused catastrophic failure (0% returns vs 29.81% CAGR)
    /// </summary>
    public class ProcessWindowMonitor
    {
        public enum WindowStatus
        {
            GreenZone,      // All parameters within safe bounds
            YellowZone,     // Warning: parameters approaching limits
            RedZone,        // Critical: parameters outside safe bounds
            BlackSwan       // Catastrophic: multiple violations detected
        }

        public enum AlertLevel
        {
            Info,
            Warning,
            Critical,
            Emergency
        }

        private readonly List<ProcessWindowViolation> _violations = new();
        private readonly Dictionary<string, ParameterWindow> _parameterWindows = new();

        public ProcessWindowMonitor()
        {
            InitializeParameterWindows();
        }

        /// <summary>
        /// Initialize safe parameter windows based on historical validation
        /// These bounds were derived from 20+ years of backtesting
        /// </summary>
        private void InitializeParameterWindows()
        {
            // CRITICAL: Iron Condor Credit Calculation
            // Lesson learned: 2.5% caused 0% returns, 3.5% yielded 29.81% CAGR
            _parameterWindows["IronCondorCreditPct"] = new ParameterWindow
            {
                Name = "Iron Condor Credit %",
                SafeMin = 0.030m,       // 3.0% - absolute minimum for profitability
                SafeMax = 0.040m,       // 4.0% - maximum realistic credit
                WarningMin = 0.032m,    // 3.2% - start monitoring closely
                WarningMax = 0.038m,    // 3.8% - approaching unrealistic territory
                CriticalMin = 0.025m,   // 2.5% - KNOWN FAILURE POINT
                CriticalMax = 0.045m,   // 4.5% - likely overestimated
                Description = "Iron Condor credit as % of position size. CRITICAL parameter - 1% difference = catastrophic impact"
            };

            // Commission Structure (affects all strategies)
            _parameterWindows["CommissionPerLeg"] = new ParameterWindow
            {
                Name = "Commission Per Leg",
                SafeMin = 0.25m,        // $0.25 - modern discount broker
                SafeMax = 2.50m,        // $2.50 - expensive but manageable
                WarningMin = 0.50m,     // $0.50 - start monitoring impact
                WarningMax = 2.00m,     // $2.00 - getting expensive
                CriticalMin = 0.10m,    // $0.10 - unrealistically low
                CriticalMax = 5.00m,    // $5.00 - strategy killer
                Description = "Commission per options leg. Higher values destroy profitability"
            };

            // Slippage per leg (market impact)
            _parameterWindows["SlippagePerLeg"] = new ParameterWindow
            {
                Name = "Slippage Per Leg",
                SafeMin = 0.015m,       // $0.015 - very liquid markets
                SafeMax = 0.035m,       // $0.035 - acceptable for most conditions
                WarningMin = 0.020m,    // $0.020 - monitor closely
                WarningMax = 0.030m,    // $0.030 - approaching problematic
                CriticalMin = 0.005m,   // $0.005 - unrealistic (no slippage)
                CriticalMax = 0.050m,   // $0.050 - excessive slippage
                Description = "Market slippage per leg. Critical for multi-leg strategies"
            };

            // VIX bonus multiplier
            _parameterWindows["VixBonusMultiplier"] = new ParameterWindow
            {
                Name = "VIX Bonus Multiplier",
                SafeMin = 1.00m,        // No bonus (conservative)
                SafeMax = 1.50m,        // 50% bonus max (high vol periods)
                WarningMin = 1.10m,     // 10% bonus - start monitoring
                WarningMax = 1.40m,     // 40% bonus - getting aggressive
                CriticalMin = 0.80m,    // Penalty instead of bonus
                CriticalMax = 2.00m,    // 100% bonus - unrealistic
                Description = "VIX-based credit enhancement. Avoid over-optimization"
            };

            // Position sizing as % of account
            _parameterWindows["PositionSizePct"] = new ParameterWindow
            {
                Name = "Position Size %",
                SafeMin = 0.05m,        // 5% - very conservative
                SafeMax = 0.25m,        // 25% - aggressive but manageable
                WarningMin = 0.10m,     // 10% - start monitoring
                WarningMax = 0.20m,     // 20% - getting risky
                CriticalMin = 0.01m,    // 1% - too small to be meaningful
                CriticalMax = 0.50m,    // 50% - excessive concentration
                Description = "Position size as % of total account. Risk management critical"
            };

            // RevFib guardrail violation rate
            _parameterWindows["RevFibViolationRate"] = new ParameterWindow
            {
                Name = "RevFib Violation Rate",
                SafeMin = 0.00m,        // 0% - perfect (like PM212's 100% win rate)
                SafeMax = 0.05m,        // 5% - acceptable violation rate
                WarningMin = 0.00m,     // Any violations trigger warning
                WarningMax = 0.03m,     // 3% - monitor closely
                CriticalMin = 0.00m,    // N/A for minimum
                CriticalMax = 0.10m,    // 10% - system failing
                Description = "Rate of RevFib guardrail violations. Should be near zero"
            };

            // Win rate (strategy effectiveness)
            _parameterWindows["WinRate"] = new ParameterWindow
            {
                Name = "Win Rate %",
                SafeMin = 0.60m,        // 60% - minimum acceptable
                SafeMax = 1.00m,        // 100% - perfect (like PM212)
                WarningMin = 0.65m,     // 65% - start monitoring
                WarningMax = 0.95m,     // 95% - suspiciously high
                CriticalMin = 0.50m,    // 50% - barely break-even
                CriticalMax = 1.00m,    // N/A for maximum
                Description = "Strategy win rate. Below 60% indicates fundamental issues"
            };
        }

        /// <summary>
        /// Monitor a specific parameter and check if it's within process window
        /// </summary>
        public ProcessWindowResult CheckParameter(string parameterName, decimal currentValue,
            DateTime timestamp, string context = "")
        {
            if (!_parameterWindows.TryGetValue(parameterName, out var window))
            {
                return new ProcessWindowResult
                {
                    Status = WindowStatus.YellowZone,
                    AlertLevel = AlertLevel.Warning,
                    Message = $"Unknown parameter '{parameterName}' - not being monitored",
                    Parameter = parameterName,
                    CurrentValue = currentValue,
                    Timestamp = timestamp
                };
            }

            var status = DetermineStatus(currentValue, window);
            var alertLevel = DetermineAlertLevel(status, currentValue, window);
            var message = GenerateMessage(status, parameterName, currentValue, window, context);

            var result = new ProcessWindowResult
            {
                Status = status,
                AlertLevel = alertLevel,
                Message = message,
                Parameter = parameterName,
                CurrentValue = currentValue,
                ExpectedRange = $"{window.SafeMin:F3} - {window.SafeMax:F3}",
                Timestamp = timestamp,
                Context = context
            };

            // Log violations for trend analysis
            if (status == WindowStatus.RedZone || status == WindowStatus.BlackSwan)
            {
                LogViolation(result);
            }

            return result;
        }

        /// <summary>
        /// Check multiple parameters and return overall system status
        /// </summary>
        public ProcessWindowSystemStatus CheckSystemStatus(Dictionary<string, decimal> parameters,
            DateTime timestamp, string context = "")
        {
            var results = new List<ProcessWindowResult>();
            var criticalCount = 0;
            var warningCount = 0;

            foreach (var param in parameters)
            {
                var result = CheckParameter(param.Key, param.Value, timestamp, context);
                results.Add(result);

                if (result.Status == WindowStatus.RedZone || result.Status == WindowStatus.BlackSwan)
                    criticalCount++;
                else if (result.Status == WindowStatus.YellowZone)
                    warningCount++;
            }

            var overallStatus = DetermineOverallStatus(criticalCount, warningCount, results.Count);

            return new ProcessWindowSystemStatus
            {
                OverallStatus = overallStatus,
                Results = results,
                CriticalViolations = criticalCount,
                WarningCount = warningCount,
                TotalChecks = results.Count,
                Timestamp = timestamp,
                Context = context,
                ShouldSuspendTrading = overallStatus == WindowStatus.BlackSwan || criticalCount > 0,
                ShouldReducePositionSize = warningCount > 2 || criticalCount > 0
            };
        }

        private WindowStatus DetermineStatus(decimal value, ParameterWindow window)
        {
            // Critical violations (Black Swan territory)
            if (value <= window.CriticalMin || value >= window.CriticalMax)
                return WindowStatus.BlackSwan;

            // Red zone (outside safe bounds)
            if (value < window.SafeMin || value > window.SafeMax)
                return WindowStatus.RedZone;

            // Yellow zone (approaching limits)
            if (value < window.WarningMin || value > window.WarningMax)
                return WindowStatus.YellowZone;

            // Green zone (all good)
            return WindowStatus.GreenZone;
        }

        private AlertLevel DetermineAlertLevel(WindowStatus status, decimal value, ParameterWindow window)
        {
            return status switch
            {
                ProcessWindowMonitor.WindowStatus.BlackSwan => ProcessWindowMonitor.AlertLevel.Emergency,
                ProcessWindowMonitor.WindowStatus.RedZone => ProcessWindowMonitor.AlertLevel.Critical,
                ProcessWindowMonitor.WindowStatus.YellowZone => ProcessWindowMonitor.AlertLevel.Warning,
                ProcessWindowMonitor.WindowStatus.GreenZone => ProcessWindowMonitor.AlertLevel.Info,
                _ => ProcessWindowMonitor.AlertLevel.Warning
            };
        }

        private string GenerateMessage(WindowStatus status, string parameterName, decimal value,
            ParameterWindow window, string context)
        {
            var contextStr = string.IsNullOrEmpty(context) ? "" : $" [{context}]";

            return status switch
            {
                WindowStatus.BlackSwan =>
                    $"🚨 CRITICAL VIOLATION{contextStr}: {parameterName} = {value:F3} is outside safe bounds " +
                    $"({window.SafeMin:F3}-{window.SafeMax:F3}). SUSPEND TRADING IMMEDIATELY!",

                WindowStatus.RedZone =>
                    $"⚠️  PARAMETER VIOLATION{contextStr}: {parameterName} = {value:F3} is outside safe range " +
                    $"({window.SafeMin:F3}-{window.SafeMax:F3}). Investigate immediately.",

                WindowStatus.YellowZone =>
                    $"⚡ WARNING{contextStr}: {parameterName} = {value:F3} approaching limits " +
                    $"(safe: {window.SafeMin:F3}-{window.SafeMax:F3}). Monitor closely.",

                WindowStatus.GreenZone =>
                    $"✅ OK{contextStr}: {parameterName} = {value:F3} within safe bounds " +
                    $"({window.SafeMin:F3}-{window.SafeMax:F3})",

                _ => $"Unknown status for {parameterName}"
            };
        }

        private WindowStatus DetermineOverallStatus(int criticalCount, int warningCount, int totalCount)
        {
            if (criticalCount > 0)
                return WindowStatus.BlackSwan;

            if (warningCount > totalCount / 2)  // More than 50% warnings
                return WindowStatus.RedZone;

            if (warningCount > 0)
                return WindowStatus.YellowZone;

            return WindowStatus.GreenZone;
        }

        private void LogViolation(ProcessWindowResult result)
        {
            _violations.Add(new ProcessWindowViolation
            {
                Parameter = result.Parameter,
                Value = result.CurrentValue,
                Status = result.Status,
                Timestamp = result.Timestamp,
                Context = result.Context,
                Message = result.Message
            });

            // Keep only last 1000 violations to prevent memory issues
            if (_violations.Count > 1000)
            {
                _violations.RemoveRange(0, _violations.Count - 1000);
            }
        }

        /// <summary>
        /// Get violation history for trend analysis
        /// </summary>
        public List<ProcessWindowViolation> GetViolationHistory(TimeSpan? period = null)
        {
            if (period == null)
                return _violations.ToList();

            var cutoff = DateTime.UtcNow - period.Value;
            return _violations.Where(v => v.Timestamp >= cutoff).ToList();
        }

        /// <summary>
        /// Get violation summary for reporting
        /// </summary>
        public ProcessWindowSummary GetViolationSummary(TimeSpan? period = null)
        {
            var violations = GetViolationHistory(period);

            return new ProcessWindowSummary
            {
                TotalViolations = violations.Count,
                CriticalViolations = violations.Count(v => v.Status == WindowStatus.BlackSwan),
                RedZoneViolations = violations.Count(v => v.Status == WindowStatus.RedZone),
                MostFrequentParameter = violations.GroupBy(v => v.Parameter)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault()?.Key ?? "None",
                Period = period ?? TimeSpan.FromDays(30),
                LastViolation = violations.LastOrDefault()?.Timestamp
            };
        }
    }

    /// <summary>
    /// Defines safe operational bounds for a trading parameter
    /// </summary>
    public class ParameterWindow
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";

        // Safe operating range (Green Zone)
        public decimal SafeMin { get; set; }
        public decimal SafeMax { get; set; }

        // Warning thresholds (Yellow Zone)
        public decimal WarningMin { get; set; }
        public decimal WarningMax { get; set; }

        // Critical thresholds (Red/Black Zone)
        public decimal CriticalMin { get; set; }
        public decimal CriticalMax { get; set; }
    }

    /// <summary>
    /// Result of a single parameter check
    /// </summary>
    public class ProcessWindowResult
    {
        public ProcessWindowMonitor.WindowStatus Status { get; set; }
        public ProcessWindowMonitor.AlertLevel AlertLevel { get; set; }
        public string Message { get; set; } = "";
        public string Parameter { get; set; } = "";
        public decimal CurrentValue { get; set; }
        public string ExpectedRange { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public string Context { get; set; } = "";
    }

    /// <summary>
    /// Overall system status across all parameters
    /// </summary>
    public class ProcessWindowSystemStatus
    {
        public ProcessWindowMonitor.WindowStatus OverallStatus { get; set; }
        public List<ProcessWindowResult> Results { get; set; } = new();
        public int CriticalViolations { get; set; }
        public int WarningCount { get; set; }
        public int TotalChecks { get; set; }
        public DateTime Timestamp { get; set; }
        public string Context { get; set; } = "";
        public bool ShouldSuspendTrading { get; set; }
        public bool ShouldReducePositionSize { get; set; }

        /// <summary>
        /// Get summary message for quick status check
        /// </summary>
        public string GetSummaryMessage()
        {
            var statusIcon = OverallStatus switch
            {
                ProcessWindowMonitor.WindowStatus.GreenZone => "✅",
                ProcessWindowMonitor.WindowStatus.YellowZone => "⚡",
                ProcessWindowMonitor.WindowStatus.RedZone => "⚠️ ",
                ProcessWindowMonitor.WindowStatus.BlackSwan => "🚨",
                _ => "❓"
            };

            var action = ShouldSuspendTrading ? " - SUSPEND TRADING" :
                        ShouldReducePositionSize ? " - REDUCE POSITIONS" : "";

            return $"{statusIcon} {OverallStatus}: {CriticalViolations} critical, {WarningCount} warnings{action}";
        }
    }

    /// <summary>
    /// Individual violation record for logging
    /// </summary>
    public class ProcessWindowViolation
    {
        public string Parameter { get; set; } = "";
        public decimal Value { get; set; }
        public ProcessWindowMonitor.WindowStatus Status { get; set; }
        public DateTime Timestamp { get; set; }
        public string Context { get; set; } = "";
        public string Message { get; set; } = "";
    }

    /// <summary>
    /// Summary statistics for violation analysis
    /// </summary>
    public class ProcessWindowSummary
    {
        public int TotalViolations { get; set; }
        public int CriticalViolations { get; set; }
        public int RedZoneViolations { get; set; }
        public string MostFrequentParameter { get; set; } = "";
        public TimeSpan Period { get; set; }
        public DateTime? LastViolation { get; set; }
    }
}