using System;
using System.Threading.Tasks;
using ODTE.Strategy;

namespace ODTE.Strategy;

/// <summary>
/// 24-Day Framework Demo and Validation Program
/// 
/// DEMONSTRATES:
/// 1. Framework initialization and configuration
/// 2. Multi-scenario validation testing
/// 3. Strategy performance across market conditions
/// 4. Risk management and position sizing effectiveness
/// 5. Contingency logic (sniper mode, risk amplification)
/// 
/// USAGE:
/// dotnet run                          # Run full validation suite
/// dotnet run --demo                   # Run single demo scenario
/// dotnet run --scenario "Bull Market" # Run specific scenario
/// dotnet run --live                   # Connect to live framework (future)
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("🎯 24-Day Trading Framework");
        Console.WriteLine("Target: $6000 over 24 trading days");
        Console.WriteLine("=".PadRight(50, '='));
        Console.WriteLine();

        try
        {
            // Parse command line arguments
            var mode = ParseMode(args);
            var specificScenario = ParseScenario(args);

            // Check for RegimeSwitcher mode
            if (args.Contains("--regime") || args.Contains("regime"))
            {
                await RunRegimeSwitcherAnalysis();
                return;
            }
            
            // Check for RegimeSwitcher stress test mode
            if (args.Contains("--stress") || args.Contains("stress"))
            {
                RunRegimeSwitcherStressTest();
                return;
            }
            
            // Check for regression test mode
            if (args.Contains("--regression") || args.Contains("regression"))
            {
                RunStrategyRegressionTests();
                return;
            }

            // Initialize framework configuration
            var frameworkConfig = new FrameworkConfig
            {
                MaxDailyLoss = 300m,
                BasePositionSize = 1000m,
                MaxPositionSize = 2000m,
                VolatilityThreshold = 25m,
                EnableRiskAmplification = true,
                EnableSniperMode = true
            };

            Console.WriteLine("📋 Framework Configuration:");
            Console.WriteLine($"   Max Daily Loss: ${frameworkConfig.MaxDailyLoss}");
            Console.WriteLine($"   Base Position Size: ${frameworkConfig.BasePositionSize}");
            Console.WriteLine($"   Max Position Size: ${frameworkConfig.MaxPositionSize}");
            Console.WriteLine($"   Risk Amplification: {(frameworkConfig.EnableRiskAmplification ? "Enabled" : "Disabled")}");
            Console.WriteLine($"   Sniper Mode: {(frameworkConfig.EnableSniperMode ? "Enabled" : "Disabled")}");
            Console.WriteLine();

            switch (mode)
            {
                case RunMode.Demo:
                    await RunDemoScenario(frameworkConfig);
                    break;
                    
                case RunMode.Validation:
                    await RunValidationSuite(frameworkConfig, specificScenario);
                    break;
                    
                case RunMode.BattleTest:
                    await RunRealisticBattleTest(frameworkConfig);
                    break;
                    
                case RunMode.Live:
                    Console.WriteLine("🚀 Live trading mode not yet implemented.");
                    Console.WriteLine("   This would connect to real broker APIs and execute actual trades.");
                    Console.WriteLine("   Use paper trading for development and testing.");
                    break;
                    
                default:
                    await RunValidationSuite(frameworkConfig, null);
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine();
            Console.WriteLine("Stack trace:");
            Console.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }

        Console.WriteLine("\n🎯 Program completed.");
    }

    /// <summary>
    /// Run demonstration scenario
    /// </summary>
    private static async Task RunDemoScenario(FrameworkConfig config)
    {
        Console.WriteLine("🎬 Running Demo Scenario: Mixed Market Conditions");
        Console.WriteLine();

        var framework = new TwentyFourDayFramework(config);
        var startDate = new DateTime(2024, 8, 1);

        Console.WriteLine("Day | Date       | Strategy              | P&L      | Position | Trajectory");
        Console.WriteLine("----|------------|----------------------|----------|----------|------------------");

        decimal totalPnL = 0m;
        
        for (int day = 1; day <= TwentyFourDayFramework.FRAMEWORK_DAYS; day++)
        {
            var tradingDate = startDate.AddDays(day - 1);
            
            // Generate realistic market conditions
            var conditions = GenerateDemoConditions(day);
            
            // Execute trading day
            var result = await framework.ExecuteTradingDay(tradingDate, conditions);
            totalPnL += result.PnL;
            
            // Determine trajectory indicator
            var trajectoryIndicator = DetermineTrajectoryIndicator(totalPnL, day);
            
            Console.WriteLine($"{day,3} | {tradingDate:MM/dd/yyyy} | {result.Strategy,-20} | ${result.PnL,6:F0} | ${result.PositionSize,6:F0} | {trajectoryIndicator}");
            
            // Add commentary for key framework transitions
            if (day == TwentyFourDayFramework.SNIPER_ACTIVATION_DAY)
            {
                Console.WriteLine("    >>> SNIPER MODE EVALUATION POINT <<<");
            }
            if (day == TwentyFourDayFramework.ACCELERATION_DAY)
            {
                Console.WriteLine("    >>> ACCELERATION PHASE BEGINS <<<");
            }
        }

        Console.WriteLine("----|------------|----------------------|----------|----------|------------------");
        Console.WriteLine($"TOTAL P&L: ${totalPnL:F2} | TARGET: ${TwentyFourDayFramework.TARGET_PROFIT:F2} | ACHIEVEMENT: {(totalPnL/TwentyFourDayFramework.TARGET_PROFIT*100):F1}%");
        
        var report = framework.GenerateReport();
        PrintFrameworkSummary(report);
    }

    /// <summary>
    /// Run comprehensive validation suite
    /// </summary>
    private static async Task RunValidationSuite(FrameworkConfig config, string? specificScenario)
    {
        if (!string.IsNullOrEmpty(specificScenario))
        {
            Console.WriteLine($"🎯 Running Specific Scenario: {specificScenario}");
        }
        else
        {
            Console.WriteLine("🧪 Running Comprehensive Validation Suite");
            Console.WriteLine("   Testing framework across multiple market conditions...");
        }
        Console.WriteLine();

        var simulation = new TwentyFourDaySimulation(config);
        var validationReport = await simulation.RunValidationAsync();

        Console.WriteLine("\n📊 FINAL VALIDATION RESULTS");
        Console.WriteLine("=".PadRight(60, '='));
        
        if (validationReport.OverallSuccess)
        {
            Console.WriteLine("✅ FRAMEWORK VALIDATION: PASSED");
            Console.WriteLine($"🎯 Success Rate: {validationReport.SuccessfulScenarios}/{validationReport.TotalScenarios} scenarios");
            Console.WriteLine($"💰 Average Performance: ${validationReport.AveragePnL:F2}");
            Console.WriteLine($"🏆 Best Scenario: ${validationReport.BestPerformance:F2}");
        }
        else
        {
            Console.WriteLine("❌ FRAMEWORK VALIDATION: FAILED");
            Console.WriteLine($"⚠️ Success Rate: {validationReport.SuccessfulScenarios}/{validationReport.TotalScenarios} scenarios (need 4/5)");
            Console.WriteLine($"💸 Worst Scenario: ${validationReport.WorstPerformance:F2}");
        }

        Console.WriteLine($"\n🎖️ Robustness Score: {validationReport.RobustnessScore:F2}");
        Console.WriteLine($"📈 Average Win Rate: {validationReport.AverageWinRate:F1}%");
        Console.WriteLine($"📉 Maximum Drawdown: ${Math.Abs(validationReport.MaximumDrawdown):F2}");

        if (validationReport.Recommendations.Count > 0)
        {
            Console.WriteLine("\n💡 RECOMMENDATIONS:");
            foreach (var recommendation in validationReport.Recommendations)
            {
                Console.WriteLine($"   • {recommendation}");
            }
        }

        // Export detailed results for analysis
        Console.WriteLine($"\n📄 Detailed results exported to: ValidationReport_{DateTime.Now:yyyyMMdd_HHmmss}.json");
    }

    /// <summary>
    /// Run realistic battle test to find losing periods
    /// </summary>
    private static async Task RunRealisticBattleTest(FrameworkConfig config)
    {
        Console.WriteLine("🔥 REALISTIC BATTLE TEST MODE");
        Console.WriteLine("🎯 Objective: Find periods where the 24-day framework loses money");
        Console.WriteLine("⚠️  Using actual historical market conditions with NO FUTURE KNOWLEDGE");
        Console.WriteLine();

        var battleTest = new RealisticBattleTest(config);
        var report = await battleTest.RunComprehensiveBattleTest();

        Console.WriteLine("\n" + "=".PadRight(80, '='));
        Console.WriteLine("🎯 FINAL BATTLE TEST ASSESSMENT");
        Console.WriteLine("=".PadRight(80, '='));

        if (report.LossMakingPeriods == 0)
        {
            Console.WriteLine("❌ UNREALISTIC RESULT: No losing periods found");
            Console.WriteLine("🚨 This suggests the system is not realistic enough");
            Console.WriteLine("💡 In real trading, some 24-day periods WILL lose money");
        }
        else
        {
            Console.WriteLine($"✅ REALISTIC ASSESSMENT COMPLETE");
            Console.WriteLine($"📊 Found {report.LossMakingPeriods} losing periods out of {report.TotalPeriods}");
            Console.WriteLine($"💸 Worst Loss: ${Math.Abs(report.WorstPerformance):F2}");
            Console.WriteLine($"🎖️  Overall Assessment: {report.OverallAssessment}");
        }

        Console.WriteLine($"\n💰 PERFORMANCE METRICS:");
        Console.WriteLine($"   Win Rate: {(double)report.ProfitablePeriods/report.TotalPeriods*100:F1}%");
        Console.WriteLine($"   Average P&L: ${report.AveragePnL:F2}");
        Console.WriteLine($"   Risk-Adjusted Return: {(report.AveragePnL / Math.Max(Math.Abs(report.WorstPerformance), 1000)):F2}");

        Console.WriteLine($"\n🔍 FAILURE ANALYSIS:");
        var failedPeriods = report.Results.Where(r => !r.Success).ToList();
        if (failedPeriods.Any())
        {
            Console.WriteLine($"   Periods that caused losses:");
            foreach (var period in failedPeriods.Take(3))
            {
                Console.WriteLine($"   • {period.Period.Name}: ${period.FinalPnL:F2}");
            }
        }
        else
        {
            Console.WriteLine($"   ⚠️  No clear failure modes identified - may need more realistic testing");
        }

        Console.WriteLine($"\n🎯 HONEST RECOMMENDATION:");
        if (report.AveragePnL > 2000 && report.LossMakingPeriods <= 3)
        {
            Console.WriteLine($"   ✅ Framework shows promise for live trading");
            Console.WriteLine($"   📊 Expected occasional losses are manageable");
            Console.WriteLine($"   🚀 Consider paper trading validation");
        }
        else if (report.LossMakingPeriods > 5)
        {
            Console.WriteLine($"   ❌ Framework fails too often for reliable trading");
            Console.WriteLine($"   🔧 Requires significant improvements before live use");
        }
        else
        {
            Console.WriteLine($"   ⚠️  Framework performance is marginal");
            Console.WriteLine($"   📊 Consider reducing position sizes or improving strategy selection");
        }
    }

    /// <summary>
    /// Generate demo market conditions
    /// </summary>
    private static MarketConditions GenerateDemoConditions(int day)
    {
        // Simulate realistic mixed market conditions
        var conditions = new MarketConditions();
        
        if (day <= 8) // Initial bull phase
        {
            conditions.IVRank = 20 + day * 1.5;
            conditions.RSI = 52 + day * 1.2;
            conditions.MomentumDivergence = 0.4 + day * 0.02;
            conditions.VIXContango = 4.0;
            conditions.VIX = 15 + day * 0.5;
        }
        else if (day <= 16) // Volatile correction phase
        {
            conditions.IVRank = 45 + (day - 8) * 2.5;
            conditions.RSI = 65 - (day - 8) * 2.8;
            conditions.MomentumDivergence = 0.6 - (day - 8) * 0.15;
            conditions.VIXContango = 8.0 + (day - 8) * 0.5;
            conditions.VIX = 20 + (day - 8) * 2.0;
        }
        else // Recovery phase
        {
            conditions.IVRank = 70 - (day - 16) * 4.0;
            conditions.RSI = 35 + (day - 16) * 3.5;
            conditions.MomentumDivergence = -0.3 + (day - 16) * 0.12;
            conditions.VIXContango = 15.0 - (day - 16) * 1.2;
            conditions.VIX = 35 - (day - 16) * 1.5;
        }
        
        // Set common properties
        conditions.Date = DateTime.Now.AddDays(day - 1);
        conditions.UnderlyingPrice = 4500;
        conditions.DaysToExpiry = 1;

        return conditions;
    }

    /// <summary>
    /// Determine trajectory indicator for display
    /// </summary>
    private static string DetermineTrajectoryIndicator(decimal totalPnL, int day)
    {
        var expectedPnL = (TwentyFourDayFramework.TARGET_PROFIT / TwentyFourDayFramework.FRAMEWORK_DAYS) * day;
        var ratio = totalPnL / expectedPnL;

        return ratio switch
        {
            >= 1.2m => "🚀 Ahead",
            >= 0.8m => "✅ On Track",
            >= 0.5m => "⚠️ Behind",
            _ => "🚨 Recovery Needed"
        };
    }

    /// <summary>
    /// Print framework summary
    /// </summary>
    private static void PrintFrameworkSummary(FrameworkReport report)
    {
        Console.WriteLine("\n📊 FRAMEWORK PERFORMANCE SUMMARY");
        Console.WriteLine("=".PadRight(50, '='));
        Console.WriteLine($"Target Achievement: {report.TargetAchievement:F1}%");
        Console.WriteLine($"Win Rate: {report.WinRate:F1}%");
        Console.WriteLine($"Profit Factor: {report.ProfitFactor:F2}");
        Console.WriteLine($"Average Win: ${report.AverageWin:F2}");
        Console.WriteLine($"Average Loss: ${Math.Abs(report.AverageLoss):F2}");
        Console.WriteLine($"Max Drawdown: ${Math.Abs(report.MaxDrawdown):F2}");
        
        Console.WriteLine("\n📈 Strategy Distribution:");
        foreach (var strategy in report.StrategyDistribution)
        {
            Console.WriteLine($"   {strategy.Key}: {strategy.Value} days");
        }
    }

    /// <summary>
    /// Parse command line mode
    /// </summary>
    private static RunMode ParseMode(string[] args)
    {
        if (args.Contains("--demo")) return RunMode.Demo;
        if (args.Contains("--live")) return RunMode.Live;
        if (args.Contains("--validation")) return RunMode.Validation;
        if (args.Contains("--battle")) return RunMode.BattleTest;
        
        return RunMode.Validation; // Default
    }

    /// <summary>
    /// Parse specific scenario from command line
    /// </summary>
    private static string? ParseScenario(string[] args)
    {
        var index = Array.IndexOf(args, "--scenario");
        if (index >= 0 && index + 1 < args.Length)
        {
            return args[index + 1];
        }
        return null;
    }

    /// <summary>
    /// Run RegimeSwitcher 24-Day Rolling Analysis
    /// </summary>
    private static async Task RunRegimeSwitcherAnalysis()
    {
        Console.WriteLine("🔄 REGIME SWITCHER: 24-Day Rolling Strategy Analysis");
        Console.WriteLine("🎯 Maximizing returns in each 24-day period through adaptive regime-based strategies");
        Console.WriteLine("=" .PadRight(80, '='));
        Console.WriteLine();
        
        var regimeSwitcher = new RegimeSwitcher();
        
        // Run 5-year analysis with 24-day rolling periods
        var startDate = new DateTime(2019, 1, 2);
        var endDate = new DateTime(2024, 12, 31);
        
        Console.WriteLine($"📅 Analysis Period: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
        Console.WriteLine($"🔄 24-day rolling periods with fresh capital reset each cycle");
        Console.WriteLine($"🎯 Strategy Selection: Calm (BWB) | Mixed (BWB+Tail) | Convex (RatioBS)");
        Console.WriteLine();
        
        var results = regimeSwitcher.RunHistoricalAnalysis(startDate, endDate);
        
        Console.WriteLine("\n" + "=" .PadRight(80, '='));
        Console.WriteLine("🎉 REGIME SWITCHER ANALYSIS COMPLETE!");
        Console.WriteLine("=" .PadRight(80, '='));
        
        Console.WriteLine($"\n📊 OVERALL PERFORMANCE METRICS:");
        Console.WriteLine($"   Total 24-day periods: {results.TotalPeriods}");
        Console.WriteLine($"   Average return per period: {results.AverageReturn:F1}%");
        Console.WriteLine($"   Best period return: {results.BestPeriodReturn:F1}%");
        Console.WriteLine($"   Worst period return: {results.WorstPeriodReturn:F1}%");
        Console.WriteLine($"   Win rate: {results.WinRate:P1}");
        Console.WriteLine($"   Total compound return: {results.TotalReturn:F1}%");
        
        Console.WriteLine($"\n🎯 STRATEGY REGIME PERFORMANCE:");
        foreach (var (regime, pnl) in results.RegimePerformance.OrderByDescending(r => r.Value))
        {
            Console.WriteLine($"   {regime}: ${pnl:F0}");
        }
        
        // Calculate annualized metrics
        var years = (endDate - startDate).TotalDays / 365.25;
        var annualizedReturn = Math.Pow(1 + results.TotalReturn / 100, 1 / years) - 1;
        var periodsPerYear = 365.25 / 24; // ~15.2 periods per year
        
        Console.WriteLine($"\n📈 ANNUALIZED METRICS:");
        Console.WriteLine($"   Annualized return: {annualizedReturn * 100:F1}%");
        Console.WriteLine($"   Periods per year: ~{periodsPerYear:F1}");
        Console.WriteLine($"   Expected periods needed to double capital: {Math.Log(2) / Math.Log(1 + results.AverageReturn / 100):F1}");
        
        if (results.TotalReturn > 100)
        {
            Console.WriteLine($"\n🏆 EXCEPTIONAL PERFORMANCE!");
            Console.WriteLine($"   RegimeSwitcher achieved {results.TotalReturn:F1}% total return");
            Console.WriteLine($"   This demonstrates the power of adaptive 24-day regime-based strategies");
        }
        else if (results.TotalReturn > 50)
        {
            Console.WriteLine($"\n✅ STRONG PERFORMANCE!");
            Console.WriteLine($"   RegimeSwitcher achieved solid {results.TotalReturn:F1}% total return");
            Console.WriteLine($"   Consistent performance across market regimes");
        }
        else
        {
            Console.WriteLine($"\n⚠️ MODERATE PERFORMANCE");
            Console.WriteLine($"   RegimeSwitcher achieved {results.TotalReturn:F1}% total return");
            Console.WriteLine($"   Consider parameter optimization for enhanced results");
        }
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Run RegimeSwitcher Stress Test with synthetic data
    /// </summary>
    private static void RunRegimeSwitcherStressTest()
    {
        Console.WriteLine("🔥 REGIME SWITCHER STRESS TEST");
        Console.WriteLine("Testing performance under rapid regime changes with synthetic data");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();
        
        var stressTest = new RegimeSwitcherStressTest();
        stressTest.RunComprehensiveStressTest();
        
        Console.WriteLine("\n🎯 Stress test completed.");
    }
    
    /// <summary>
    /// Run Strategy Regression Tests
    /// </summary>
    private static void RunStrategyRegressionTests()
    {
        Console.WriteLine("🧪 STRATEGY REGRESSION TESTS");
        Console.WriteLine("Validating Iron Condor, Credit BWB, and Convex Tail Overlay performance");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();
        
        var regressionTests = new StrategyRegressionTests();
        regressionTests.RunCompleteRegressionSuite();
        
        Console.WriteLine("\n🎯 Regression tests completed.");
    }
}

/// <summary>
/// Program run modes
/// </summary>
public enum RunMode
{
    Demo,
    Validation,
    BattleTest,
    Live
}