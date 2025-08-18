using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using ODTE.Historical.Providers;
using ODTE.Historical.DistributedStorage;
using ODTE.Execution.HistoricalFill;
using ODTE.Execution.Models;
using CDTE.Strategy.CDTE;
using CDTE.Strategy.Reporting;

namespace CDTE.Strategy.Backtesting;

/// <summary>
/// CDTE Backtest Runner - 20-Year Historical Analysis
/// Executes comprehensive backtest with CAGR calculation and performance analytics
/// </summary>
public class CDTEBacktestRunner
{
    private readonly ILogger<CDTEBacktestRunner> _logger;
    private readonly ChainSnapshotProvider _snapshotProvider;
    private readonly CDTEStrategy _strategy;
    private readonly MondayToThuFriHarness _harness;
    private readonly SparseDayRunner _sparseRunner;
    private readonly CDTEAuditSystem _auditSystem;
    private readonly CDTEConfig _config;

    public CDTEBacktestRunner(
        ILogger<CDTEBacktestRunner> logger,
        ChainSnapshotProvider snapshotProvider,
        CDTEStrategy strategy,
        MondayToThuFriHarness harness,
        SparseDayRunner sparseRunner,
        CDTEAuditSystem auditSystem,
        CDTEConfig config)
    {
        _logger = logger;
        _snapshotProvider = snapshotProvider;
        _strategy = strategy;
        _harness = harness;
        _sparseRunner = sparseRunner;
        _auditSystem = auditSystem;
        _config = config;
    }

    /// <summary>
    /// Execute 20-year CDTE backtest and calculate comprehensive performance metrics
    /// </summary>
    public async Task<CDTEBacktestResults> RunTwentyYearBacktestAsync(
        decimal initialCapital = 100000m,
        string underlying = "SPX")
    {
        var startDate = new DateTime(2004, 1, 1); // 20 years from 2024
        var endDate = new DateTime(2024, 12, 31);

        _logger.LogInformation("🚀 Starting CDTE 20-Year Backtest: {Start} to {End}", startDate, endDate);
        _logger.LogInformation("📊 Initial Capital: {Capital:C}, Underlying: {Underlying}", initialCapital, underlying);

        var backtestResults = new CDTEBacktestResults
        {
            StartDate = startDate,
            EndDate = endDate,
            InitialCapital = initialCapital,
            Underlying = underlying,
            YearlyResults = new Dictionary<int, YearlyPerformance>(),
            OverallMetrics = new OverallPerformanceMetrics()
        };

        try
        {
            // Run sparse backtest for optimal coverage
            var sparseResults = await _sparseRunner.RunSparseBacktestAsync(
                startDate, endDate, underlying, SamplingStrategy.Comprehensive);

            _logger.LogInformation("✅ Sparse backtest completed: {WeekCount} weeks tested", 
                sparseResults.SampledWeeks.Count(w => w.WasExecuted));

            // Process results and calculate performance metrics
            backtestResults = await ProcessBacktestResults(sparseResults, initialCapital);

            // Generate comprehensive audit report
            var auditReport = await _auditSystem.GenerateAuditReportAsync(sparseResults, DateTime.Now, "20-Year Backtest");

            backtestResults.AuditReportId = auditReport.ReportId;
            backtestResults.ComplianceScore = auditReport.ComplianceValidation.OverallComplianceScore;

            // Calculate final metrics
            await CalculateFinalMetrics(backtestResults);

            _logger.LogInformation("🎉 20-Year Backtest Complete!");
            _logger.LogInformation("📈 Final Capital: {FinalCapital:C}", backtestResults.FinalCapital);
            _logger.LogInformation("📊 CAGR: {CAGR:P2}", backtestResults.OverallMetrics.CAGR);
            _logger.LogInformation("⚡ Sharpe Ratio: {Sharpe:F3}", backtestResults.OverallMetrics.SharpeRatio);

            return backtestResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error during 20-year backtest execution");
            throw;
        }
    }

    /// <summary>
    /// Process sparse backtest results into detailed performance analysis
    /// </summary>
    private async Task<CDTEBacktestResults> ProcessBacktestResults(
        SparseBacktestResults sparseResults, 
        decimal initialCapital)
    {
        var results = new CDTEBacktestResults
        {
            StartDate = sparseResults.StartDate,
            EndDate = sparseResults.EndDate,
            InitialCapital = initialCapital,
            YearlyResults = new Dictionary<int, YearlyPerformance>()
        };

        var runningCapital = initialCapital;
        var yearlyData = new Dictionary<int, List<WeeklyTrade>>();

        // Process each successful week
        foreach (var sampledWeek in sparseResults.SampledWeeks.Where(w => w.WasExecuted && w.ActualResult != null))
        {
            var year = sampledWeek.WeekStart.Year;
            var weeklyPnL = sampledWeek.ActualResult!.WeeklyPnL;

            // Calculate position size based on current capital and risk management
            var positionSize = CalculatePositionSize(runningCapital, weeklyPnL);
            var scaledPnL = weeklyPnL * positionSize;

            runningCapital += scaledPnL;

            var weeklyTrade = new WeeklyTrade
            {
                WeekStart = sampledWeek.WeekStart,
                WeekEnd = sampledWeek.WeekStart.AddDays(4),
                Strategy = DetermineStrategyType(sampledWeek.ExpectedRegime),
                IVRegime = sampledWeek.ExpectedRegime,
                RawPnL = weeklyPnL,
                PositionSize = positionSize,
                ScaledPnL = scaledPnL,
                RunningCapital = runningCapital,
                TradeCount = sampledWeek.ActualResult.TradeCount,
                WednesdayActions = sampledWeek.ActualResult.WednesdayActions.Count,
                EventTags = sampledWeek.EventTags
            };

            if (!yearlyData.ContainsKey(year))
                yearlyData[year] = new List<WeeklyTrade>();
            
            yearlyData[year].Add(weeklyTrade);
        }

        // Calculate yearly performance metrics
        foreach (var yearGroup in yearlyData)
        {
            var year = yearGroup.Key;
            var yearTrades = yearGroup.Value.OrderBy(t => t.WeekStart).ToList();

            if (!yearTrades.Any()) continue;

            var startCapital = year == sparseResults.StartDate.Year 
                ? initialCapital 
                : yearTrades.First().RunningCapital - yearTrades.First().ScaledPnL;
            var endCapital = yearTrades.Last().RunningCapital;

            var yearlyPerformance = new YearlyPerformance
            {
                Year = year,
                StartCapital = startCapital,
                EndCapital = endCapital,
                TotalReturn = endCapital - startCapital,
                ReturnPercentage = (endCapital - startCapital) / startCapital,
                WeeksTested = yearTrades.Count,
                WinningWeeks = yearTrades.Count(t => t.ScaledPnL > 0),
                LosingWeeks = yearTrades.Count(t => t.ScaledPnL < 0),
                WinRate = yearTrades.Count(t => t.ScaledPnL > 0) / (double)yearTrades.Count,
                AvgWeeklyReturn = yearTrades.Average(t => t.ScaledPnL),
                MaxWeeklyGain = yearTrades.Max(t => t.ScaledPnL),
                MaxWeeklyLoss = yearTrades.Min(t => t.ScaledPnL),
                MaxDrawdown = CalculateYearlyMaxDrawdown(yearTrades),
                SharpeRatio = CalculateYearlySharpe(yearTrades),
                VolatilityAnnualized = CalculateYearlyVolatility(yearTrades),
                TradesByStrategy = yearTrades.GroupBy(t => t.Strategy).ToDictionary(g => g.Key, g => g.Count()),
                TradesByRegime = yearTrades.GroupBy(t => t.IVRegime).ToDictionary(g => g.Key, g => g.Count()),
                WeeklyTrades = yearTrades
            };

            results.YearlyResults[year] = yearlyPerformance;
        }

        results.FinalCapital = runningCapital;
        return results;
    }

    /// <summary>
    /// Calculate final comprehensive performance metrics
    /// </summary>
    private async Task CalculateFinalMetrics(CDTEBacktestResults results)
    {
        var totalYears = (results.EndDate - results.StartDate).TotalDays / 365.25;
        var totalReturn = (results.FinalCapital - results.InitialCapital) / results.InitialCapital;

        // Calculate CAGR
        results.OverallMetrics.CAGR = Math.Pow((double)(results.FinalCapital / results.InitialCapital), 1.0 / totalYears) - 1.0;

        // Calculate overall metrics from yearly data
        var allWeeklyReturns = results.YearlyResults.Values
            .SelectMany(y => y.WeeklyTrades)
            .Select(t => (double)(t.ScaledPnL / (t.RunningCapital - t.ScaledPnL)))
            .ToList();

        results.OverallMetrics.TotalReturn = totalReturn;
        results.OverallMetrics.TotalWeeksTested = results.YearlyResults.Values.Sum(y => y.WeeksTested);
        results.OverallMetrics.OverallWinRate = results.YearlyResults.Values.Sum(y => y.WinningWeeks) / 
                                               (double)results.YearlyResults.Values.Sum(y => y.WeeksTested);

        results.OverallMetrics.SharpeRatio = CalculateOverallSharpe(allWeeklyReturns);
        results.OverallMetrics.MaxDrawdown = CalculateOverallMaxDrawdown(results.YearlyResults.Values.SelectMany(y => y.WeeklyTrades));
        results.OverallMetrics.VolatilityAnnualized = CalculateOverallVolatility(allWeeklyReturns);
        results.OverallMetrics.SortinoRatio = CalculateSortinoRatio(allWeeklyReturns);
        results.OverallMetrics.CalmarRatio = results.OverallMetrics.CAGR / (double)results.OverallMetrics.MaxDrawdown;

        // Calculate additional metrics
        results.OverallMetrics.BestYear = results.YearlyResults.Values.Max(y => y.ReturnPercentage);
        results.OverallMetrics.WorstYear = results.YearlyResults.Values.Min(y => y.ReturnPercentage);
        results.OverallMetrics.PositiveYears = results.YearlyResults.Values.Count(y => y.ReturnPercentage > 0);
        results.OverallMetrics.NegativeYears = results.YearlyResults.Values.Count(y => y.ReturnPercentage < 0);

        // Strategy and regime analysis
        results.OverallMetrics.StrategyPerformance = AnalyzeStrategyPerformance(results.YearlyResults.Values.SelectMany(y => y.WeeklyTrades));
        results.OverallMetrics.RegimePerformance = AnalyzeRegimePerformance(results.YearlyResults.Values.SelectMany(y => y.WeeklyTrades));
    }

    /// <summary>
    /// Calculate position size based on capital and RevFibNotch risk management
    /// </summary>
    private decimal CalculatePositionSize(decimal currentCapital, decimal expectedPnL)
    {
        // Use 1% of capital per trade with RevFibNotch scaling
        var basePositionSize = Math.Max(1m, currentCapital * 0.01m / Math.Abs(_config.RiskCapUsd));
        
        // Apply RevFibNotch scaling based on recent performance
        // This is simplified - in production would use actual RevFibNotch logic
        return Math.Min(basePositionSize, 5m); // Cap at 5x base size
    }

    /// <summary>
    /// Create a configured CDTE backtest runner instance
    /// </summary>
    public static async Task<CDTEBacktestRunner> CreateAsync(string dataPath = @"C:\code\ODTE\data")
    {
        // Setup logging
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        var serviceProvider = services.BuildServiceProvider();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        // Create configuration
        var config = new CDTEConfig
        {
            RiskCapUsd = 800m,
            MondayDecisionET = TimeSpan.FromHours(10),
            WednesdayDecisionET = TimeSpan.FromHours(12.5),
            DeltaTargets = new DeltaTargets
            {
                IcShortAbs = 0.18,
                BwbBodyPut = -0.30,
                BwbNearPut = -0.15,
                VertShortAbs = 0.20
            },
            RegimeBandsIV = new RegimeBands { Low = 15.0, High = 22.0 },
            TakeProfitCorePct = 0.70,
            NeutralBandPct = 0.15,
            MaxDrawdownPct = 0.50,
            RollDebitCapPctOfRisk = 0.25
        };

        // Create components
        var dataManager = new DistributedDatabaseManager(dataPath);
        var snapshotProvider = new ChainSnapshotProvider(dataManager, loggerFactory.CreateLogger<ChainSnapshotProvider>());
        
        var executionProfile = new ExecutionProfile { Name = "CDTE_Historical" };
        var fillEngine = new NbboFillEngine(executionProfile, loggerFactory.CreateLogger<NbboFillEngine>());
        
        var rollRules = new CDTERollRules(loggerFactory.CreateLogger<CDTERollRules>());
        var strategy = new CDTEStrategy(config, loggerFactory.CreateLogger<CDTEStrategy>(), rollRules);
        
        var harness = new MondayToThuFriHarness(snapshotProvider, fillEngine, strategy, config, loggerFactory.CreateLogger<MondayToThuFriHarness>());
        var sparseRunner = new SparseDayRunner(snapshotProvider, harness, loggerFactory.CreateLogger<SparseDayRunner>());
        var auditSystem = new CDTEAuditSystem(loggerFactory.CreateLogger<CDTEAuditSystem>());

        var runner = new CDTEBacktestRunner(
            loggerFactory.CreateLogger<CDTEBacktestRunner>(),
            snapshotProvider, strategy, harness, sparseRunner, auditSystem, config);

        return runner;
    }

    // Helper calculation methods
    private string DetermineStrategyType(string regime) => regime switch
    {
        "Low" or "LowVol" => "BWB",
        "High" or "Crisis" or "Stress" => "IronFly", 
        _ => "IronCondor"
    };

    private decimal CalculateYearlyMaxDrawdown(List<WeeklyTrade> trades)
    {
        var peak = trades.First().RunningCapital - trades.First().ScaledPnL;
        var maxDrawdown = 0m;

        foreach (var trade in trades)
        {
            if (trade.RunningCapital > peak) peak = trade.RunningCapital;
            var drawdown = (peak - trade.RunningCapital) / peak;
            if (drawdown > maxDrawdown) maxDrawdown = drawdown;
        }

        return maxDrawdown;
    }

    private double CalculateYearlySharpe(List<WeeklyTrade> trades)
    {
        var returns = trades.Select(t => (double)(t.ScaledPnL / (t.RunningCapital - t.ScaledPnL))).ToList();
        if (!returns.Any()) return 0.0;

        var avgReturn = returns.Average();
        var stdDev = Math.Sqrt(returns.Select(r => Math.Pow(r - avgReturn, 2)).Average());
        return stdDev > 0 ? (avgReturn / stdDev) * Math.Sqrt(52) : 0.0; // Annualized weekly returns
    }

    private double CalculateYearlyVolatility(List<WeeklyTrade> trades)
    {
        var returns = trades.Select(t => (double)(t.ScaledPnL / (t.RunningCapital - t.ScaledPnL))).ToList();
        if (!returns.Any()) return 0.0;

        var avgReturn = returns.Average();
        return Math.Sqrt(returns.Select(r => Math.Pow(r - avgReturn, 2)).Average()) * Math.Sqrt(52);
    }

    private double CalculateOverallSharpe(List<double> weeklyReturns)
    {
        if (!weeklyReturns.Any()) return 0.0;
        var avgReturn = weeklyReturns.Average();
        var stdDev = Math.Sqrt(weeklyReturns.Select(r => Math.Pow(r - avgReturn, 2)).Average());
        return stdDev > 0 ? (avgReturn / stdDev) * Math.Sqrt(52) : 0.0;
    }

    private decimal CalculateOverallMaxDrawdown(IEnumerable<WeeklyTrade> allTrades)
    {
        var trades = allTrades.OrderBy(t => t.WeekStart).ToList();
        if (!trades.Any()) return 0m;

        var peak = trades.First().RunningCapital - trades.First().ScaledPnL;
        var maxDrawdown = 0m;

        foreach (var trade in trades)
        {
            if (trade.RunningCapital > peak) peak = trade.RunningCapital;
            var drawdown = (peak - trade.RunningCapital) / peak;
            if (drawdown > maxDrawdown) maxDrawdown = drawdown;
        }

        return maxDrawdown;
    }

    private double CalculateOverallVolatility(List<double> weeklyReturns)
    {
        if (!weeklyReturns.Any()) return 0.0;
        var avgReturn = weeklyReturns.Average();
        return Math.Sqrt(weeklyReturns.Select(r => Math.Pow(r - avgReturn, 2)).Average()) * Math.Sqrt(52);
    }

    private double CalculateSortinoRatio(List<double> weeklyReturns)
    {
        if (!weeklyReturns.Any()) return 0.0;
        var avgReturn = weeklyReturns.Average();
        var downside = weeklyReturns.Where(r => r < 0).ToList();
        if (!downside.Any()) return double.PositiveInfinity;
        
        var downsideDeviation = Math.Sqrt(downside.Select(r => Math.Pow(r, 2)).Average());
        return downsideDeviation > 0 ? (avgReturn / downsideDeviation) * Math.Sqrt(52) : 0.0;
    }

    private Dictionary<string, StrategyPerformanceMetrics> AnalyzeStrategyPerformance(IEnumerable<WeeklyTrade> allTrades)
    {
        return allTrades.GroupBy(t => t.Strategy).ToDictionary(g => g.Key, g => new StrategyPerformanceMetrics
        {
            TradeCount = g.Count(),
            WinRate = g.Count(t => t.ScaledPnL > 0) / (double)g.Count(),
            AvgReturn = g.Average(t => t.ScaledPnL),
            TotalReturn = g.Sum(t => t.ScaledPnL)
        });
    }

    private Dictionary<string, RegimePerformanceMetrics> AnalyzeRegimePerformance(IEnumerable<WeeklyTrade> allTrades)
    {
        return allTrades.GroupBy(t => t.IVRegime).ToDictionary(g => g.Key, g => new RegimePerformanceMetrics
        {
            TradeCount = g.Count(),
            WinRate = g.Count(t => t.ScaledPnL > 0) / (double)g.Count(),
            AvgReturn = g.Average(t => t.ScaledPnL),
            TotalReturn = g.Sum(t => t.ScaledPnL)
        });
    }
}

// Data models for backtest results
public class CDTEBacktestResults
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal InitialCapital { get; set; }
    public decimal FinalCapital { get; set; }
    public string Underlying { get; set; } = "";
    public Dictionary<int, YearlyPerformance> YearlyResults { get; set; } = new();
    public OverallPerformanceMetrics OverallMetrics { get; set; } = new();
    public string AuditReportId { get; set; } = "";
    public double ComplianceScore { get; set; }
}

public class YearlyPerformance
{
    public int Year { get; set; }
    public decimal StartCapital { get; set; }
    public decimal EndCapital { get; set; }
    public decimal TotalReturn { get; set; }
    public decimal ReturnPercentage { get; set; }
    public int WeeksTested { get; set; }
    public int WinningWeeks { get; set; }
    public int LosingWeeks { get; set; }
    public double WinRate { get; set; }
    public decimal AvgWeeklyReturn { get; set; }
    public decimal MaxWeeklyGain { get; set; }
    public decimal MaxWeeklyLoss { get; set; }
    public decimal MaxDrawdown { get; set; }
    public double SharpeRatio { get; set; }
    public double VolatilityAnnualized { get; set; }
    public Dictionary<string, int> TradesByStrategy { get; set; } = new();
    public Dictionary<string, int> TradesByRegime { get; set; } = new();
    public List<WeeklyTrade> WeeklyTrades { get; set; } = new();
}

public class OverallPerformanceMetrics
{
    public double CAGR { get; set; }
    public decimal TotalReturn { get; set; }
    public int TotalWeeksTested { get; set; }
    public double OverallWinRate { get; set; }
    public double SharpeRatio { get; set; }
    public decimal MaxDrawdown { get; set; }
    public double VolatilityAnnualized { get; set; }
    public double SortinoRatio { get; set; }
    public double CalmarRatio { get; set; }
    public decimal BestYear { get; set; }
    public decimal WorstYear { get; set; }
    public int PositiveYears { get; set; }
    public int NegativeYears { get; set; }
    public Dictionary<string, StrategyPerformanceMetrics> StrategyPerformance { get; set; } = new();
    public Dictionary<string, RegimePerformanceMetrics> RegimePerformance { get; set; } = new();
}

public class WeeklyTrade
{
    public DateTime WeekStart { get; set; }
    public DateTime WeekEnd { get; set; }
    public string Strategy { get; set; } = "";
    public string IVRegime { get; set; } = "";
    public decimal RawPnL { get; set; }
    public decimal PositionSize { get; set; }
    public decimal ScaledPnL { get; set; }
    public decimal RunningCapital { get; set; }
    public int TradeCount { get; set; }
    public int WednesdayActions { get; set; }
    public List<string> EventTags { get; set; } = new();
}

public class StrategyPerformanceMetrics
{
    public int TradeCount { get; set; }
    public double WinRate { get; set; }
    public decimal AvgReturn { get; set; }
    public decimal TotalReturn { get; set; }
}

public class RegimePerformanceMetrics
{
    public int TradeCount { get; set; }
    public double WinRate { get; set; }
    public decimal AvgReturn { get; set; }
    public decimal TotalReturn { get; set; }
}