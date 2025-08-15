using ODTE.Strategy;

namespace ODTE.Strategy.Tests;

/// <summary>
/// 10-Year Regime Switching Analysis using existing API
/// Tests the 24-day rolling strategy framework over a full 10-year period
/// </summary>
public class TenYearRegimeAnalysisTest
{
    [Fact]
    public async Task Execute24DayRegimeSwitching_TenYearPeriod_ShowsComprehensivePerformance()
    {
        // Arrange - Full 10-year test period (2015-2025)
        var strategyEngine = new StrategyEngine();
        var startDate = new DateTime(2015, 1, 1);
        var endDate = new DateTime(2025, 1, 1);
        var startingCapital = 5000m;
        
        Console.WriteLine("🚀 ODTE 24-Day Regime Switching - 10 Year Analysis");
        Console.WriteLine("====================================================");
        Console.WriteLine($"📅 Period: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
        Console.WriteLine($"💰 Starting Capital: ${startingCapital:N0}");
        Console.WriteLine($"📊 Expected 24-Day Periods: ~{(endDate - startDate).TotalDays / 24:N0}");
        Console.WriteLine();

        // Act - Execute 10-year regime switching analysis
        var result = await strategyEngine.Execute24DayRegimeSwitchingAsync(
            startDate, endDate, startingCapital);

        // Assert - Comprehensive results validation
        result.Should().NotBeNull();
        result.TotalPeriods.Should().BeGreaterThan(100); // ~150 periods expected
        result.Periods.Should().NotBeEmpty();

        // Display comprehensive 10-year results
        Console.WriteLine("📊 === 10-YEAR ANALYSIS RESULTS ===");
        Console.WriteLine();
        
        Console.WriteLine($"📈 PERFORMANCE SUMMARY:");
        Console.WriteLine($"   Final Capital: ${result.FinalCapital:N2}");
        Console.WriteLine($"   Total Return: ${result.TotalReturn:N2}");
        Console.WriteLine($"   Return Percentage: {result.ReturnPercentage:N2}%");
        Console.WriteLine($"   Annualized Return: {result.AnnualizedReturn:N2}%");
        Console.WriteLine();
        
        Console.WriteLine($"📊 PERIOD ANALYSIS:");
        Console.WriteLine($"   Total 24-Day Periods: {result.TotalPeriods}");
        Console.WriteLine($"   Winning Periods: {result.WinningPeriods}");
        Console.WriteLine($"   Losing Periods: {result.LosingPeriods}");
        Console.WriteLine($"   Win Rate: {result.WinRate:N2}%");
        Console.WriteLine($"   Average Return per Period: {result.AverageReturn:N2}%");
        Console.WriteLine($"   Best Period: {result.BestPeriodReturn:N2}%");
        Console.WriteLine($"   Worst Period: {result.WorstPeriodReturn:N2}%");
        Console.WriteLine();
        
        Console.WriteLine($"⚠️ RISK METRICS:");
        Console.WriteLine($"   Max Drawdown: {result.MaxDrawdown:N2}%");
        Console.WriteLine($"   Sharpe Ratio: {result.SharpeRatio:N2}");
        Console.WriteLine($"   Largest Loss: ${result.LargestLoss:N2}");
        Console.WriteLine($"   Largest Win: ${result.LargestWin:N2}");
        Console.WriteLine($"   Profit Factor: {result.ProfitFactor:N2}");
        Console.WriteLine();
        
        Console.WriteLine($"🌡️ REGIME DISTRIBUTION:");
        Console.WriteLine($"   Calm Periods: {result.CalmPeriods} ({result.CalmPeriods * 100.0 / result.TotalPeriods:N1}%)");
        Console.WriteLine($"   Mixed Periods: {result.MixedPeriods} ({result.MixedPeriods * 100.0 / result.TotalPeriods:N1}%)");
        Console.WriteLine($"   Convex Periods: {result.ConvexPeriods} ({result.ConvexPeriods * 100.0 / result.TotalPeriods:N1}%)");
        Console.WriteLine();
        
        Console.WriteLine($"💰 REGIME PERFORMANCE:");
        foreach (var regimePerf in result.RegimePerformance.OrderByDescending(x => x.Value))
        {
            Console.WriteLine($"   {regimePerf.Key}: ${regimePerf.Value:N2}");
        }
        Console.WriteLine();
        
        // Annual breakdown analysis
        Console.WriteLine($"📅 ANNUAL PERFORMANCE ESTIMATE:");
        var years = (endDate - startDate).TotalDays / 365.25;
        var annualizedMultiplier = Math.Pow((double)(result.FinalCapital / startingCapital), 1.0 / years);
        
        for (int year = 2015; year <= 2024; year++)
        {
            var yearMultiplier = Math.Pow(annualizedMultiplier, year - 2014);
            var estimatedCapital = (decimal)((double)startingCapital * yearMultiplier);
            var yearReturn = (estimatedCapital - startingCapital) / startingCapital * 100;
            Console.WriteLine($"   {year}: ${estimatedCapital:N0} ({yearReturn:N1}% cumulative)");
        }
        Console.WriteLine();
        
        // Strategy effectiveness analysis  
        Console.WriteLine($"🎯 STRATEGY EFFECTIVENESS:");
        Console.WriteLine($"   BWB Win Rate: {result.BWBWinRate:N1}%");
        Console.WriteLine($"   Tail Overlay ROI: {result.TailOverlayROI:N1}%");
        Console.WriteLine($"   Regime Detection Accuracy: {result.RegimeAccuracy:N1}%");
        Console.WriteLine();
        
        // Performance assessment
        Console.WriteLine($"📝 ASSESSMENT:");
        if (result.ReturnPercentage > 200)
        {
            Console.WriteLine($"   🏆 EXCEPTIONAL: 10-year return of {result.ReturnPercentage:N1}% demonstrates strong strategy");
            Console.WriteLine($"   💎 Regime switching effectively adapts to market conditions");
        }
        else if (result.ReturnPercentage > 100)
        {
            Console.WriteLine($"   ✅ EXCELLENT: 10-year return of {result.ReturnPercentage:N1}% shows solid performance");
            Console.WriteLine($"   📈 Strategy successfully compounds over long periods");
        }
        else if (result.ReturnPercentage > 50)
        {
            Console.WriteLine($"   ✅ GOOD: 10-year return of {result.ReturnPercentage:N1}% is respectable");
            Console.WriteLine($"   🔧 Consider parameter optimization for enhanced returns");
        }
        else if (result.ReturnPercentage > 0)
        {
            Console.WriteLine($"   ⚠️ MODEST: 10-year return of {result.ReturnPercentage:N1}% needs improvement");
            Console.WriteLine($"   🔧 Requires strategy refinement and risk management review");
        }
        else
        {
            Console.WriteLine($"   ❌ POOR: 10-year loss of {Math.Abs(result.ReturnPercentage):N1}% indicates fundamental issues");
            Console.WriteLine($"   🚨 Strategy requires complete overhaul before deployment");
        }
        
        Console.WriteLine();
        Console.WriteLine($"✅ 10-year regime switching analysis complete!");
        
        // Save results to report
        var reportPath = Path.Combine("Reports", $"10Year_Regime_Analysis_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        Directory.CreateDirectory("Reports");
        
        var report = GenerateDetailedReport(result, startDate, endDate, startingCapital);
        await File.WriteAllTextAsync(reportPath, report);
        
        Console.WriteLine($"📄 Detailed report saved: {reportPath}");
        
        // Validation assertions for test framework
        result.TotalPeriods.Should().BeGreaterThan(50, "Should have many 24-day periods over 10 years");
        result.FinalCapital.Should().BeGreaterThan(0, "Should preserve some capital even in worst case");
        result.Periods.Should().HaveCountGreaterThan(50, "Should have executed many trading periods");
        
        // Performance expectations (realistic for 10-year period)
        if (result.ReturnPercentage > 0)
        {
            result.AnnualizedReturn.Should().BeGreaterThan(-50, "Annualized return should not be catastrophic");
        }
    }
    
    private static string GenerateDetailedReport(RegimeSwitchingResult result, DateTime startDate, DateTime endDate, decimal startingCapital)
    {
        var report = $@"
ODTE 24-Day Regime Switching Strategy
10-Year Analysis Report
========================================
Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
Period: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}
Starting Capital: ${startingCapital:N0}

EXECUTIVE SUMMARY
-----------------
Final Capital: ${result.FinalCapital:N2}
Total Return: {result.ReturnPercentage:N2}%
Annualized Return: {result.AnnualizedReturn:N2}%
Sharpe Ratio: {result.SharpeRatio:N2}
Maximum Drawdown: {result.MaxDrawdown:N2}%

PERFORMANCE METRICS
-------------------
Total 24-Day Periods: {result.TotalPeriods}
Winning Periods: {result.WinningPeriods}
Losing Periods: {result.LosingPeriods}
Win Rate: {result.WinRate:N2}%
Average Return per Period: {result.AverageReturn:N2}%
Best Period Return: {result.BestPeriodReturn:N2}%
Worst Period Return: {result.WorstPeriodReturn:N2}%

RISK ANALYSIS
-------------
Largest Win: ${result.LargestWin:N2}
Largest Loss: ${result.LargestLoss:N2}
Profit Factor: {result.ProfitFactor:N2}
Max Consecutive Losses: {result.MaxConsecutiveLosses}
Max Recovery Days: {result.MaxRecoveryDays}

REGIME DISTRIBUTION
-------------------
Calm Markets: {result.CalmPeriods} ({result.CalmPeriods * 100.0 / result.TotalPeriods:N1}%)
Mixed Markets: {result.MixedPeriods} ({result.MixedPeriods * 100.0 / result.TotalPeriods:N1}%)
Convex Markets: {result.ConvexPeriods} ({result.ConvexPeriods * 100.0 / result.TotalPeriods:N1}%)

REGIME PERFORMANCE
------------------";

        foreach (var regimePerf in result.RegimePerformance.OrderByDescending(x => x.Value))
        {
            report += $@"
{regimePerf.Key}: ${regimePerf.Value:N2}";
        }

        report += $@"

STRATEGY EFFECTIVENESS
----------------------
BWB Win Rate: {result.BWBWinRate:N1}%
Tail Overlay ROI: {result.TailOverlayROI:N1}%
Regime Detection Accuracy: {result.RegimeAccuracy:N1}%

CONCLUSION
----------
The 24-day regime switching strategy over {(endDate - startDate).TotalDays / 365.25:N1} years 
achieved a {result.ReturnPercentage:N2}% total return with {result.WinRate:N1}% win rate.

Risk-adjusted return (Sharpe): {result.SharpeRatio:N2}
Maximum drawdown: {result.MaxDrawdown:N2}%

========================================
End of Report
";
        return report;
    }
}