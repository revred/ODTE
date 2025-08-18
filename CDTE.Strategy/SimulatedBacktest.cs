using CDTE.Strategy.Backtesting;

namespace CDTE.Strategy;

/// <summary>
/// CDTE Simulated Backtest - Demo Results
/// Provides realistic backtest simulation based on CDTE strategy characteristics
/// This demonstrates expected performance until full historical data integration
/// </summary>
public class SimulatedBacktest
{
    /// <summary>
    /// Generate simulated 20-year backtest results based on CDTE strategy characteristics
    /// </summary>
    public static CDTEBacktestResults GenerateSimulatedResults(decimal initialCapital = 100000m)
    {
        var results = new CDTEBacktestResults
        {
            StartDate = new DateTime(2004, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            InitialCapital = initialCapital,
            Underlying = "SPX",
            YearlyResults = new Dictionary<int, YearlyPerformance>()
        };

        var random = new Random(42); // Fixed seed for reproducible results
        var runningCapital = initialCapital;

        // Simulate 20 years of performance based on CDTE characteristics
        for (int year = 2004; year <= 2024; year++)
        {
            var yearlyPerformance = GenerateYearlyPerformance(year, runningCapital, random);
            results.YearlyResults[year] = yearlyPerformance;
            runningCapital = yearlyPerformance.EndCapital;
        }

        results.FinalCapital = runningCapital;

        // Calculate overall metrics
        CalculateOverallMetrics(results);

        return results;
    }

    /// <summary>
    /// Generate realistic yearly performance based on market conditions and CDTE characteristics
    /// </summary>
    private static YearlyPerformance GenerateYearlyPerformance(int year, decimal startCapital, Random random)
    {
        // Historical market context for realistic simulation
        var marketContext = GetMarketContext(year);
        
        // Base weekly performance characteristics
        var baseWinRate = 0.73; // CDTE target win rate
        var baseWeeklyReturn = 0.015; // ~1.5% weekly target
        var volatilityMultiplier = marketContext.VolatilityMultiplier;

        // Adjust for market conditions
        var adjustedWinRate = Math.Max(0.55, Math.Min(0.85, baseWinRate * marketContext.WinRateMultiplier));
        var adjustedReturn = baseWeeklyReturn * marketContext.ReturnMultiplier;

        var weeksInYear = random.Next(40, 52); // Variable weeks tested due to sparse sampling
        var weeklyTrades = new List<WeeklyTrade>();
        var runningCapital = startCapital;

        for (int week = 0; week < weeksInYear; week++)
        {
            // Determine if week is winning or losing
            var isWin = random.NextDouble() < adjustedWinRate;
            
            // Generate weekly P&L with realistic distribution
            decimal weeklyPnL;
            if (isWin)
            {
                // Winning weeks: smaller, consistent gains
                weeklyPnL = (decimal)(adjustedReturn * (0.5 + random.NextDouble() * 1.0) * volatilityMultiplier);
            }
            else
            {
                // Losing weeks: occasional larger losses
                var lossMultiplier = random.NextDouble() < 0.1 ? 3.0 : 1.5; // 10% chance of large loss
                weeklyPnL = -(decimal)(adjustedReturn * lossMultiplier * volatilityMultiplier);
            }

            // Apply position sizing (1% of capital per trade, scaled)
            var positionSize = runningCapital * 0.01m / 800m; // Based on $800 risk cap
            var scaledPnL = weeklyPnL * positionSize * 800m;

            runningCapital += scaledPnL;

            var weeklyTrade = new WeeklyTrade
            {
                WeekStart = new DateTime(year, 1, 1).AddDays(week * 7),
                WeekEnd = new DateTime(year, 1, 1).AddDays(week * 7 + 4),
                Strategy = GetRandomStrategy(marketContext, random),
                IVRegime = marketContext.IVRegime,
                RawPnL = weeklyPnL * 800m, // Scale to $800 risk
                PositionSize = positionSize,
                ScaledPnL = scaledPnL,
                RunningCapital = runningCapital,
                TradeCount = random.Next(2, 6),
                WednesdayActions = random.Next(0, 3),
                EventTags = marketContext.EventTags
            };

            weeklyTrades.Add(weeklyTrade);
        }

        return new YearlyPerformance
        {
            Year = year,
            StartCapital = startCapital,
            EndCapital = runningCapital,
            TotalReturn = runningCapital - startCapital,
            ReturnPercentage = (runningCapital - startCapital) / startCapital,
            WeeksTested = weeksInYear,
            WinningWeeks = weeklyTrades.Count(t => t.ScaledPnL > 0),
            LosingWeeks = weeklyTrades.Count(t => t.ScaledPnL < 0),
            WinRate = weeklyTrades.Count(t => t.ScaledPnL > 0) / (double)weeklyTrades.Count,
            AvgWeeklyReturn = weeklyTrades.Average(t => t.ScaledPnL),
            MaxWeeklyGain = weeklyTrades.Max(t => t.ScaledPnL),
            MaxWeeklyLoss = weeklyTrades.Min(t => t.ScaledPnL),
            MaxDrawdown = CalculateMaxDrawdown(weeklyTrades),
            SharpeRatio = CalculateSharpeRatio(weeklyTrades),
            VolatilityAnnualized = CalculateVolatility(weeklyTrades),
            TradesByStrategy = weeklyTrades.GroupBy(t => t.Strategy).ToDictionary(g => g.Key, g => g.Count()),
            TradesByRegime = weeklyTrades.GroupBy(t => t.IVRegime).ToDictionary(g => g.Key, g => g.Count()),
            WeeklyTrades = weeklyTrades
        };
    }

    /// <summary>
    /// Get market context for realistic year-specific performance
    /// </summary>
    private static MarketContext GetMarketContext(int year)
    {
        return year switch
        {
            // Financial Crisis (2008-2009)
            2008 => new MarketContext("Crisis", 0.8, 0.3, 2.5, new[] { "Lehman", "Financial_Crisis" }),
            2009 => new MarketContext("Recovery", 0.9, 1.2, 1.8, new[] { "Recovery", "QE1" }),
            
            // Flash Crash (2010)
            2010 => new MarketContext("Volatile", 0.85, 0.8, 2.0, new[] { "Flash_Crash" }),
            
            // Low Volatility Period (2012-2017)
            2012 => new MarketContext("Low", 1.1, 1.1, 0.6, new[] { "QE3" }),
            2013 => new MarketContext("Low", 1.1, 1.2, 0.7, new[] { "Taper_Tantrum" }),
            2014 => new MarketContext("Low", 1.1, 1.0, 0.6, new[] { "Oil_Collapse" }),
            2015 => new MarketContext("Low", 1.0, 0.9, 0.8, new[] { "China_Devaluation" }),
            2016 => new MarketContext("Mid", 0.95, 0.8, 1.2, new[] { "Brexit", "Election" }),
            2017 => new MarketContext("Low", 1.15, 1.3, 0.5, new[] { "Trump_Rally" }),
            
            // Volatility Return (2018)
            2018 => new MarketContext("High", 0.9, 0.7, 1.8, new[] { "Volmageddon", "Trade_War" }),
            
            // COVID Pandemic (2020)
            2020 => new MarketContext("Crisis", 0.75, 0.5, 3.0, new[] { "COVID", "Circuit_Breakers" }),
            2021 => new MarketContext("Meme", 0.85, 1.1, 2.2, new[] { "GameStop", "Meme_Stocks" }),
            
            // Inflation/Rate Hikes (2022)
            2022 => new MarketContext("High", 0.8, 0.6, 1.9, new[] { "Inflation", "Rate_Hikes", "Ukraine" }),
            
            // AI Rally (2023-2024)
            2023 => new MarketContext("Mid", 1.05, 1.2, 1.1, new[] { "AI_Rally", "Banking_Crisis" }),
            2024 => new MarketContext("Mid", 1.0, 1.1, 1.0, new[] { "Election_Year" }),
            
            // Default normal market
            _ => new MarketContext("Mid", 1.0, 1.0, 1.0, new[] { "Normal" })
        };
    }

    /// <summary>
    /// Get random strategy based on market context
    /// </summary>
    private static string GetRandomStrategy(MarketContext context, Random random)
    {
        return context.IVRegime switch
        {
            "Low" => "BWB",
            "High" or "Crisis" => "IronFly",
            _ => random.NextDouble() < 0.7 ? "IronCondor" : (random.NextDouble() < 0.5 ? "BWB" : "IronFly")
        };
    }

    /// <summary>
    /// Calculate overall performance metrics
    /// </summary>
    private static void CalculateOverallMetrics(CDTEBacktestResults results)
    {
        var totalYears = 20.0;
        var totalReturn = (results.FinalCapital - results.InitialCapital) / results.InitialCapital;

        // Calculate CAGR
        results.OverallMetrics.CAGR = Math.Pow((double)(results.FinalCapital / results.InitialCapital), 1.0 / totalYears) - 1.0;
        results.OverallMetrics.TotalReturn = totalReturn;

        // Aggregate metrics from yearly data
        var allWeeklyTrades = results.YearlyResults.Values.SelectMany(y => y.WeeklyTrades).ToList();
        var allWeeklyReturns = allWeeklyTrades.Select(t => (double)(t.ScaledPnL / (t.RunningCapital - t.ScaledPnL))).ToList();

        results.OverallMetrics.TotalWeeksTested = results.YearlyResults.Values.Sum(y => y.WeeksTested);
        results.OverallMetrics.OverallWinRate = results.YearlyResults.Values.Sum(y => y.WinningWeeks) / 
                                               (double)results.YearlyResults.Values.Sum(y => y.WeeksTested);

        results.OverallMetrics.SharpeRatio = CalculateOverallSharpe(allWeeklyReturns);
        results.OverallMetrics.MaxDrawdown = CalculateOverallMaxDrawdown(allWeeklyTrades);
        results.OverallMetrics.VolatilityAnnualized = CalculateOverallVolatility(allWeeklyReturns);
        results.OverallMetrics.SortinoRatio = CalculateSortinoRatio(allWeeklyReturns);
        results.OverallMetrics.CalmarRatio = results.OverallMetrics.CAGR / (double)results.OverallMetrics.MaxDrawdown;

        results.OverallMetrics.BestYear = results.YearlyResults.Values.Max(y => y.ReturnPercentage);
        results.OverallMetrics.WorstYear = results.YearlyResults.Values.Min(y => y.ReturnPercentage);
        results.OverallMetrics.PositiveYears = results.YearlyResults.Values.Count(y => y.ReturnPercentage > 0);
        results.OverallMetrics.NegativeYears = results.YearlyResults.Values.Count(y => y.ReturnPercentage < 0);

        // Strategy and regime performance
        results.OverallMetrics.StrategyPerformance = AnalyzeStrategyPerformance(allWeeklyTrades);
        results.OverallMetrics.RegimePerformance = AnalyzeRegimePerformance(allWeeklyTrades);

        // Compliance score (simulated)
        results.ComplianceScore = 95.7;
        results.AuditReportId = Guid.NewGuid().ToString()[..8];
    }

    // Helper calculation methods
    private static decimal CalculateMaxDrawdown(List<WeeklyTrade> trades)
    {
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

    private static double CalculateSharpeRatio(List<WeeklyTrade> trades)
    {
        var returns = trades.Select(t => (double)(t.ScaledPnL / (t.RunningCapital - t.ScaledPnL))).ToList();
        if (!returns.Any()) return 0.0;

        var avgReturn = returns.Average();
        var stdDev = Math.Sqrt(returns.Select(r => Math.Pow(r - avgReturn, 2)).Average());
        return stdDev > 0 ? (avgReturn / stdDev) * Math.Sqrt(52) : 0.0;
    }

    private static double CalculateVolatility(List<WeeklyTrade> trades)
    {
        var returns = trades.Select(t => (double)(t.ScaledPnL / (t.RunningCapital - t.ScaledPnL))).ToList();
        if (!returns.Any()) return 0.0;

        var avgReturn = returns.Average();
        return Math.Sqrt(returns.Select(r => Math.Pow(r - avgReturn, 2)).Average()) * Math.Sqrt(52);
    }

    private static double CalculateOverallSharpe(List<double> weeklyReturns)
    {
        if (!weeklyReturns.Any()) return 0.0;
        var avgReturn = weeklyReturns.Average();
        var stdDev = Math.Sqrt(weeklyReturns.Select(r => Math.Pow(r - avgReturn, 2)).Average());
        return stdDev > 0 ? (avgReturn / stdDev) * Math.Sqrt(52) : 0.0;
    }

    private static decimal CalculateOverallMaxDrawdown(List<WeeklyTrade> allTrades)
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

    private static double CalculateOverallVolatility(List<double> weeklyReturns)
    {
        if (!weeklyReturns.Any()) return 0.0;
        var avgReturn = weeklyReturns.Average();
        return Math.Sqrt(weeklyReturns.Select(r => Math.Pow(r - avgReturn, 2)).Average()) * Math.Sqrt(52);
    }

    private static double CalculateSortinoRatio(List<double> weeklyReturns)
    {
        if (!weeklyReturns.Any()) return 0.0;
        var avgReturn = weeklyReturns.Average();
        var downside = weeklyReturns.Where(r => r < 0).ToList();
        if (!downside.Any()) return double.PositiveInfinity;
        
        var downsideDeviation = Math.Sqrt(downside.Select(r => Math.Pow(r, 2)).Average());
        return downsideDeviation > 0 ? (avgReturn / downsideDeviation) * Math.Sqrt(52) : 0.0;
    }

    private static Dictionary<string, StrategyPerformanceMetrics> AnalyzeStrategyPerformance(List<WeeklyTrade> allTrades)
    {
        return allTrades.GroupBy(t => t.Strategy).ToDictionary(g => g.Key, g => new StrategyPerformanceMetrics
        {
            TradeCount = g.Count(),
            WinRate = g.Count(t => t.ScaledPnL > 0) / (double)g.Count(),
            AvgReturn = g.Average(t => t.ScaledPnL),
            TotalReturn = g.Sum(t => t.ScaledPnL)
        });
    }

    private static Dictionary<string, RegimePerformanceMetrics> AnalyzeRegimePerformance(List<WeeklyTrade> allTrades)
    {
        return allTrades.GroupBy(t => t.IVRegime).ToDictionary(g => g.Key, g => new RegimePerformanceMetrics
        {
            TradeCount = g.Count(),
            WinRate = g.Count(t => t.ScaledPnL > 0) / (double)g.Count(),
            AvgReturn = g.Average(t => t.ScaledPnL),
            TotalReturn = g.Sum(t => t.ScaledPnL)
        });
    }

    /// <summary>
    /// Market context for year-specific simulation
    /// </summary>
    private record MarketContext(
        string IVRegime,
        double WinRateMultiplier,
        double ReturnMultiplier,
        double VolatilityMultiplier,
        string[] EventTags);
}