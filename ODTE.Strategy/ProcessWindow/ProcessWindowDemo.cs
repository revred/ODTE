namespace ODTE.Strategy.ProcessWindow
{
    /// <summary>
    /// Process Window Demonstration Program
    /// Shows how to prevent catastrophic trading failures like the Iron Condor 2.5% vs 3.5% bug
    /// 
    /// KEY LESSON: The 1% difference in credit calculation (2.5% vs 3.5%) caused:
    /// - 0% returns vs 29.81% CAGR
    /// - Complete strategy failure vs highly profitable system
    /// </summary>
    public class ProcessWindowDemo
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("🎯 PROCESS WINDOW MONITORING SYSTEM DEMONSTRATION");
            Console.WriteLine("=".PadRight(60, '='));
            Console.WriteLine();
            Console.WriteLine("📚 CRITICAL LESSON FROM PM212 AUDIT:");
            Console.WriteLine("   Iron Condor credit: 2.5% = 0% returns (CATASTROPHIC FAILURE)");
            Console.WriteLine("   Iron Condor credit: 3.5% = 29.81% CAGR (HIGHLY PROFITABLE)");
            Console.WriteLine("   Difference: Just 1% parameter change = Complete success/failure");
            Console.WriteLine();

            var demo = new ProcessWindowDemo();

            await demo.DemonstrateParameterValidation();
            await demo.DemonstrateTradeExecution();
            await demo.DemonstrateRealTimeMonitoring();
            await demo.DemonstrateCrisisScenarios();

            Console.WriteLine("🎯 DEMONSTRATION COMPLETE");
            Console.WriteLine("The Process Window system prevents catastrophic parameter drift!");
        }

        private async Task DemonstrateParameterValidation()
        {
            Console.WriteLine("🔍 DEMONSTRATION 1: PARAMETER VALIDATION");
            Console.WriteLine("-".PadRight(50, '-'));

            var monitor = new ProcessWindowMonitor();

            // Test the critical Iron Condor credit parameter
            Console.WriteLine("\n📊 Testing Iron Condor Credit Percentages:");

            // The CORRECT parameter (from PM212 fix)
            var correctResult = monitor.CheckParameter("IronCondorCreditPct", 0.035m, DateTime.UtcNow, "Correct value (29.81% CAGR)");
            Console.WriteLine($"✅ {correctResult.Message}");

            // The BUGGY parameter (original problem)
            var buggyResult = monitor.CheckParameter("IronCondorCreditPct", 0.025m, DateTime.UtcNow, "Buggy value (0% returns)");
            Console.WriteLine($"🚨 {buggyResult.Message}");

            // Warning level
            var warningResult = monitor.CheckParameter("IronCondorCreditPct", 0.032m, DateTime.UtcNow, "Warning level");
            Console.WriteLine($"⚡ {warningResult.Message}");

            // Unrealistic high
            var unrealisticResult = monitor.CheckParameter("IronCondorCreditPct", 0.050m, DateTime.UtcNow, "Unrealistic high");
            Console.WriteLine($"🚨 {unrealisticResult.Message}");

            Console.WriteLine("\n💡 KEY INSIGHT: The system immediately flags the 2.5% bug that caused 0% returns!");
        }

        private async Task DemonstrateTradeExecution()
        {
            Console.WriteLine("\n\n🛡️  DEMONSTRATION 2: PROTECTED TRADE EXECUTION");
            Console.WriteLine("-".PadRight(50, '-'));

            var monitor = new ProcessWindowMonitor();
            var validator = new ProcessWindowValidator(monitor);
            var mockExecutor = new DemoTradeExecutor();
            var guard = new ProcessWindowTradeGuard(validator, mockExecutor);

            Console.WriteLine("\n📈 Attempting to execute Iron Condor trades...");

            // Scenario 1: Safe trade (should execute)
            Console.WriteLine("\n1️⃣  SAFE TRADE (3.5% credit - the correct value):");
            var safeRequest = new TradeRequest
            {
                Strategy = "IronCondor",
                PositionSize = 500m,
                AccountSize = 10000m,
                ExpectedCredit = 18.50m,  // ~3.5% credit
                CurrentVIX = 15.0m
            };

            var safeResult = await guard.ExecuteTradeWithGuard(safeRequest);
            Console.WriteLine($"   Result: {(safeResult.Success ? "✅ EXECUTED" : "❌ BLOCKED")}");
            Console.WriteLine($"   Reason: {safeResult.Message}");

            // Scenario 2: Dangerous trade (should be blocked)
            Console.WriteLine("\n2️⃣  DANGEROUS TRADE (2.5% credit - the bug!):");
            var dangerousRequest = new TradeRequest
            {
                Strategy = "IronCondor",
                PositionSize = 500m,
                AccountSize = 10000m,
                ExpectedCredit = 13.50m,  // ~2.5% credit (THE BUG!)
                CurrentVIX = 15.0m
            };

            var dangerousResult = await guard.ExecuteTradeWithGuard(dangerousRequest);
            Console.WriteLine($"   Result: {(dangerousResult.Success ? "✅ EXECUTED" : "❌ BLOCKED")}");
            Console.WriteLine($"   Reason: {dangerousResult.Message}");

            // Scenario 3: Warning conditions (should reduce position size)
            Console.WriteLine("\n3️⃣  WARNING CONDITIONS (multiple yellow flags):");
            var warningRequest = new TradeRequest
            {
                Strategy = "IronCondor",
                PositionSize = 500m,
                AccountSize = 10000m,
                ExpectedCredit = 16.50m,  // ~3.2% credit (warning level)
                CurrentVIX = 15.0m,
                CommissionPerLeg = 1.90m  // High commission (warning)
            };

            var warningResult = await guard.ExecuteTradeWithGuard(warningRequest);
            Console.WriteLine($"   Result: {(warningResult.Success ? "✅ EXECUTED" : "❌ BLOCKED")}");
            Console.WriteLine($"   Position Adjusted: {(warningResult.PositionSizeAdjusted ? "YES" : "NO")}");
            if (warningResult.PositionSizeAdjusted)
            {
                Console.WriteLine($"   Original Size: ${warningResult.OriginalPositionSize:F2}");
                Console.WriteLine($"   Reduced Size: ${warningResult.AdjustedPositionSize:F2}");
            }

            Console.WriteLine("\n💡 KEY INSIGHT: The system prevents the 2.5% bug and adjusts risk when needed!");
        }

        private async Task DemonstrateRealTimeMonitoring()
        {
            Console.WriteLine("\n\n📊 DEMONSTRATION 3: REAL-TIME PARAMETER MONITORING");
            Console.WriteLine("-".PadRight(50, '-'));

            var monitor = new ProcessWindowMonitor();
            var validator = new ProcessWindowValidator(monitor);

            Console.WriteLine("\n🔄 Simulating live trading parameter monitoring...");

            // Simulate monitoring session with parameter drift
            var scenarios = new[]
            {
                new { Time = "09:30", Parameters = new Dictionary<string, decimal> { ["IronCondorCreditPct"] = 0.035m, ["CommissionPerLeg"] = 0.65m, ["WinRate"] = 0.75m }, Description = "Market open - all good" },
                new { Time = "10:15", Parameters = new Dictionary<string, decimal> { ["IronCondorCreditPct"] = 0.035m, ["CommissionPerLeg"] = 0.65m, ["WinRate"] = 0.68m }, Description = "Win rate declining slightly" },
                new { Time = "11:00", Parameters = new Dictionary<string, decimal> { ["IronCondorCreditPct"] = 0.032m, ["CommissionPerLeg"] = 0.65m, ["WinRate"] = 0.65m }, Description = "Credit percentage dropping (warning)" },
                new { Time = "11:30", Parameters = new Dictionary<string, decimal> { ["IronCondorCreditPct"] = 0.028m, ["CommissionPerLeg"] = 0.65m, ["WinRate"] = 0.60m }, Description = "Approaching danger zone" },
                new { Time = "12:00", Parameters = new Dictionary<string, decimal> { ["IronCondorCreditPct"] = 0.025m, ["CommissionPerLeg"] = 0.65m, ["WinRate"] = 0.55m }, Description = "CRITICAL: Hit the 2.5% bug level!" }
            };

            foreach (var scenario in scenarios)
            {
                Console.WriteLine($"\n⏰ {scenario.Time} - {scenario.Description}");

                var systemStatus = await validator.MonitorLiveTradingParameters(scenario.Parameters, $"Live monitoring at {scenario.Time}");

                Console.WriteLine($"   Status: {systemStatus.GetSummaryMessage()}");

                if (systemStatus.ShouldSuspendTrading)
                {
                    Console.WriteLine("   🚨 ACTION: TRADING SUSPENDED!");
                    break;
                }
                else if (systemStatus.ShouldReducePositionSize)
                {
                    Console.WriteLine("   ⚡ ACTION: POSITION SIZE REDUCED");
                }
            }

            Console.WriteLine("\n💡 KEY INSIGHT: Real-time monitoring catches parameter drift before catastrophe!");
        }

        private async Task DemonstrateCrisisScenarios()
        {
            Console.WriteLine("\n\n🌪️  DEMONSTRATION 4: CRISIS SCENARIO DETECTION");
            Console.WriteLine("-".PadRight(50, '-'));

            var monitor = new ProcessWindowMonitor();

            Console.WriteLine("\n🎭 Testing various crisis scenarios...");

            var crisisScenarios = new[]
            {
                new { Name = "Commission Explosion", Parameters = new Dictionary<string, decimal> { ["CommissionPerLeg"] = 5.50m }, Expected = "Should block trades" },
                new { Name = "Extreme Slippage", Parameters = new Dictionary<string, decimal> { ["SlippagePerLeg"] = 0.080m }, Expected = "Should block trades" },
                new { Name = "VIX Bonus Overload", Parameters = new Dictionary<string, decimal> { ["VixBonusMultiplier"] = 2.50m }, Expected = "Should block trades" },
                new { Name = "Win Rate Collapse", Parameters = new Dictionary<string, decimal> { ["WinRate"] = 0.45m }, Expected = "Should block trades" },
                new { Name = "Position Size Explosion", Parameters = new Dictionary<string, decimal> { ["PositionSizePct"] = 0.75m }, Expected = "Should block trades" },
                new { Name = "The Original Bug", Parameters = new Dictionary<string, decimal> { ["IronCondorCreditPct"] = 0.025m }, Expected = "Should block trades" }
            };

            foreach (var crisis in crisisScenarios)
            {
                Console.WriteLine($"\n💥 {crisis.Name}:");

                var systemStatus = monitor.CheckSystemStatus(crisis.Parameters, DateTime.UtcNow, crisis.Name);

                Console.WriteLine($"   Status: {systemStatus.GetSummaryMessage()}");
                Console.WriteLine($"   Expected: {crisis.Expected}");

                if (systemStatus.ShouldSuspendTrading)
                {
                    Console.WriteLine("   ✅ CORRECTLY BLOCKED!");
                }
                else
                {
                    Console.WriteLine("   ❌ WARNING: Should have been blocked!");
                }
            }

            Console.WriteLine("\n💡 KEY INSIGHT: The system catches all types of parameter failures, not just the Iron Condor bug!");
        }
    }

    /// <summary>
    /// Demo trade executor that simulates trade execution
    /// </summary>
    public class DemoTradeExecutor : ITradeExecutor
    {
        public async Task<TradeResult> ExecuteTrade(TradeRequest request)
        {
            // Simulate execution time
            await Task.Delay(100);

            // Simulate successful execution
            return new TradeResult
            {
                Success = true,
                ActualCredit = request.ExpectedCredit,
                ActualCommission = request.CommissionPerLeg * 4, // 4 legs for Iron Condor
                ActualSlippage = request.SlippagePerLeg * 4,
                ExecutionTime = DateTime.UtcNow
            };
        }
    }
}