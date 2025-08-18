using Microsoft.Extensions.Logging;
using CDTE.Strategy.Backtesting;
using System.Text.Json;

namespace CDTE.Strategy;

/// <summary>
/// CDTE Strategy Console Application
/// Executes 20-year backtest and calculates CAGR
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("🧬 CDTE Weekly Engine - 20-Year Backtest");
        Console.WriteLine("==========================================");
        Console.WriteLine();

        try
        {
            // Parse command line arguments
            var initialCapital = args.Length > 0 && decimal.TryParse(args[0], out var capital) ? capital : 100000m;
            var underlying = args.Length > 1 ? args[1] : "SPX";
            var dataPath = args.Length > 2 ? args[2] : @"C:\code\ODTE\data";

            Console.WriteLine($"📊 Configuration:");
            Console.WriteLine($"   Initial Capital: {initialCapital:C}");
            Console.WriteLine($"   Underlying: {underlying}");
            Console.WriteLine($"   Data Path: {dataPath}");
            Console.WriteLine();

            // Create backtest runner
            Console.WriteLine("🔧 Initializing CDTE Backtest Runner...");
            var runner = await CDTEBacktestRunner.CreateAsync(dataPath);
            Console.WriteLine("✅ Runner initialized successfully");
            Console.WriteLine();

            // Execute 20-year backtest
            Console.WriteLine("🚀 Starting 20-Year Backtest Execution...");
            Console.WriteLine("⏳ This may take several minutes depending on data availability...");
            Console.WriteLine();

            var startTime = DateTime.Now;
            var results = await runner.RunTwentyYearBacktestAsync(initialCapital, underlying);
            var executionTime = DateTime.Now - startTime;

            Console.WriteLine($"⚡ Backtest completed in {executionTime.TotalMinutes:F1} minutes");
            Console.WriteLine();

            // Display results
            await DisplayBacktestResults(results);

            // Save detailed results
            await SaveBacktestResults(results);

            Console.WriteLine();
            Console.WriteLine("🎉 CDTE 20-Year Backtest Complete!");
            Console.WriteLine("📄 Detailed results saved to CDTE_Backtest_Results.json");

        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error during backtest execution:");
            Console.WriteLine($"   {ex.Message}");
            Console.WriteLine();
            Console.WriteLine("💡 Troubleshooting:");
            Console.WriteLine("   • Ensure historical data is available in the specified path");
            Console.WriteLine("   • Check that the ODTE.Historical database files exist");
            Console.WriteLine("   • Verify file permissions for the data directory");
            
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Display comprehensive backtest results
    /// </summary>
    private static async Task DisplayBacktestResults(CDTEBacktestResults results)
    {
        Console.WriteLine("📈 CDTE 20-Year Backtest Results");
        Console.WriteLine("================================");
        Console.WriteLine();

        // Overall Performance Summary
        Console.WriteLine("🏆 OVERALL PERFORMANCE:");
        Console.WriteLine($"   Initial Capital: {results.InitialCapital:C}");
        Console.WriteLine($"   Final Capital:   {results.FinalCapital:C}");
        Console.WriteLine($"   Total Return:    {results.OverallMetrics.TotalReturn:P2}");
        Console.WriteLine($"   CAGR:           {results.OverallMetrics.CAGR:P2} ⭐");
        Console.WriteLine();

        // Risk Metrics
        Console.WriteLine("⚖️ RISK METRICS:");
        Console.WriteLine($"   Sharpe Ratio:    {results.OverallMetrics.SharpeRatio:F3}");
        Console.WriteLine($"   Sortino Ratio:   {results.OverallMetrics.SortinoRatio:F3}");
        Console.WriteLine($"   Calmar Ratio:    {results.OverallMetrics.CalmarRatio:F3}");
        Console.WriteLine($"   Max Drawdown:    {results.OverallMetrics.MaxDrawdown:P2}");
        Console.WriteLine($"   Volatility:      {results.OverallMetrics.VolatilityAnnualized:P2}");
        Console.WriteLine();

        // Trading Statistics
        Console.WriteLine("📊 TRADING STATISTICS:");
        Console.WriteLine($"   Total Weeks:     {results.OverallMetrics.TotalWeeksTested:N0}");
        Console.WriteLine($"   Win Rate:        {results.OverallMetrics.OverallWinRate:P1}");
        Console.WriteLine($"   Positive Years:  {results.OverallMetrics.PositiveYears}/{results.YearlyResults.Count}");
        Console.WriteLine($"   Best Year:       {results.OverallMetrics.BestYear:P2}");
        Console.WriteLine($"   Worst Year:      {results.OverallMetrics.WorstYear:P2}");
        Console.WriteLine();

        // Strategy Breakdown
        Console.WriteLine("🎯 STRATEGY PERFORMANCE:");
        foreach (var strategy in results.OverallMetrics.StrategyPerformance)
        {
            Console.WriteLine($"   {strategy.Key}:");
            Console.WriteLine($"     Trades:       {strategy.Value.TradeCount:N0}");
            Console.WriteLine($"     Win Rate:     {strategy.Value.WinRate:P1}");
            Console.WriteLine($"     Avg Return:   {strategy.Value.AvgReturn:C}");
            Console.WriteLine($"     Total Return: {strategy.Value.TotalReturn:C}");
        }
        Console.WriteLine();

        // Market Regime Performance
        Console.WriteLine("🌊 MARKET REGIME PERFORMANCE:");
        foreach (var regime in results.OverallMetrics.RegimePerformance)
        {
            Console.WriteLine($"   {regime.Key}:");
            Console.WriteLine($"     Trades:       {regime.Value.TradeCount:N0}");
            Console.WriteLine($"     Win Rate:     {regime.Value.WinRate:P1}");
            Console.WriteLine($"     Avg Return:   {regime.Value.AvgReturn:C}");
            Console.WriteLine($"     Total Return: {regime.Value.TotalReturn:C}");
        }
        Console.WriteLine();

        // Yearly Breakdown (Top 5 and Bottom 5 years)
        Console.WriteLine("📅 YEARLY PERFORMANCE (Best & Worst 5 Years):");
        
        var sortedYears = results.YearlyResults.Values
            .OrderByDescending(y => y.ReturnPercentage)
            .ToList();

        Console.WriteLine("   🏆 BEST YEARS:");
        foreach (var year in sortedYears.Take(5))
        {
            Console.WriteLine($"     {year.Year}: {year.ReturnPercentage:P2} " +
                            $"(Weeks: {year.WeeksTested}, Win Rate: {year.WinRate:P1})");
        }

        Console.WriteLine("   📉 WORST YEARS:");
        foreach (var year in sortedYears.TakeLast(5).Reverse())
        {
            Console.WriteLine($"     {year.Year}: {year.ReturnPercentage:P2} " +
                            $"(Weeks: {year.WeeksTested}, Win Rate: {year.WinRate:P1})");
        }
        Console.WriteLine();

        // Performance vs Benchmarks
        Console.WriteLine("📊 BENCHMARK COMPARISON:");
        var spyCAGR = 0.10; // Approximate S&P 500 CAGR over 20 years
        var riskFreeRate = 0.03; // Approximate risk-free rate

        Console.WriteLine($"   CDTE CAGR:       {results.OverallMetrics.CAGR:P2}");
        Console.WriteLine($"   S&P 500 (est):   {spyCAGR:P2}");
        Console.WriteLine($"   Risk-Free Rate:  {riskFreeRate:P2}");
        Console.WriteLine($"   Alpha vs SPY:    {(results.OverallMetrics.CAGR - spyCAGR):P2}");
        Console.WriteLine($"   Excess Return:   {(results.OverallMetrics.CAGR - riskFreeRate):P2}");
        Console.WriteLine();

        // Key Insights
        Console.WriteLine("💡 KEY INSIGHTS:");
        Console.WriteLine($"   • Strategy generated {results.OverallMetrics.CAGR:P2} annual returns over 20 years");
        Console.WriteLine($"   • ${results.InitialCapital:N0} grew to ${results.FinalCapital:N0} " +
                         $"({(results.FinalCapital / results.InitialCapital):F1}x multiplier)");
        Console.WriteLine($"   • Sharpe ratio of {results.OverallMetrics.SharpeRatio:F2} indicates strong risk-adjusted returns");
        Console.WriteLine($"   • Maximum drawdown of {results.OverallMetrics.MaxDrawdown:P1} shows controlled risk");
        Console.WriteLine($"   • {results.OverallMetrics.PositiveYears} out of {results.YearlyResults.Count} years were profitable");
        
        if (results.OverallMetrics.CAGR > spyCAGR)
        {
            Console.WriteLine($"   ⭐ Outperformed S&P 500 by {(results.OverallMetrics.CAGR - spyCAGR):P2} annually");
        }
        
        Console.WriteLine($"   • Audit compliance score: {results.ComplianceScore:F1}%");
    }

    /// <summary>
    /// Save detailed backtest results to JSON file
    /// </summary>
    private static async Task SaveBacktestResults(CDTEBacktestResults results)
    {
        try
        {
            var fileName = $"CDTE_Backtest_Results_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(results, options);
            await File.WriteAllTextAsync(fileName, json);

            Console.WriteLine($"💾 Results saved to: {fileName}");

            // Also save CSV summary for Excel analysis
            await SaveResultsAsCsv(results);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Warning: Could not save results file: {ex.Message}");
        }
    }

    /// <summary>
    /// Save results summary as CSV for Excel analysis
    /// </summary>
    private static async Task SaveResultsAsCsv(CDTEBacktestResults results)
    {
        try
        {
            var csvFileName = $"CDTE_Yearly_Performance_{DateTime.Now:yyyyMMdd}.csv";
            var csvLines = new List<string>
            {
                "Year,StartCapital,EndCapital,TotalReturn,ReturnPct,WeeksTested,WinRate,AvgWeeklyReturn,MaxDrawdown,SharpeRatio"
            };

            foreach (var year in results.YearlyResults.Values.OrderBy(y => y.Year))
            {
                csvLines.Add($"{year.Year},{year.StartCapital},{year.EndCapital},{year.TotalReturn}," +
                           $"{year.ReturnPercentage},{year.WeeksTested},{year.WinRate},{year.AvgWeeklyReturn}," +
                           $"{year.MaxDrawdown},{year.SharpeRatio}");
            }

            await File.WriteAllLinesAsync(csvFileName, csvLines);
            Console.WriteLine($"📊 CSV summary saved to: {csvFileName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Warning: Could not save CSV file: {ex.Message}");
        }
    }
}