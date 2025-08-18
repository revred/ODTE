using Microsoft.Extensions.Logging;
using ODTE.Historical.Providers;
using CDTE.Strategy.CDTE;

namespace CDTE.Strategy.Backtesting;

/// <summary>
/// SparseDayRunner - Optimized 20-Year CDTE Coverage
/// Intelligently samples representative weeks across two decades for comprehensive validation
/// Targets: Regime diversity, event coverage, seasonal patterns, market stress scenarios
/// </summary>
public class SparseDayRunner
{
    private readonly ChainSnapshotProvider _snapshotProvider;
    private readonly MondayToThuFriHarness _harness;
    private readonly ILogger<SparseDayRunner> _logger;

    public SparseDayRunner(
        ChainSnapshotProvider snapshotProvider,
        MondayToThuFriHarness harness,
        ILogger<SparseDayRunner> logger)
    {
        _snapshotProvider = snapshotProvider;
        _harness = harness;
        _logger = logger;
    }

    /// <summary>
    /// Execute sparse sampling strategy across 20+ years of historical data
    /// Returns comprehensive validation results with regime/event coverage analysis
    /// </summary>
    public async Task<SparseBacktestResults> RunSparseBacktestAsync(
        DateTime startDate,
        DateTime endDate,
        string underlying = "SPX",
        SamplingStrategy strategy = SamplingStrategy.Comprehensive)
    {
        _logger.LogInformation("Starting sparse CDTE backtest: {Start} to {End}, Strategy: {Strategy}", 
            startDate, endDate, strategy);

        var results = new SparseBacktestResults
        {
            StartDate = startDate,
            EndDate = endDate,
            SamplingStrategy = strategy,
            SampledWeeks = new List<SampledWeek>(),
            RegimeCoverage = new RegimeCoverageAnalysis(),
            EventCoverage = new EventCoverageAnalysis(),
            OverallMetrics = new OverallMetrics()
        };

        try
        {
            // Step 1: Generate sampling plan based on strategy
            var samplingPlan = await GenerateSamplingPlanAsync(startDate, endDate, strategy);
            _logger.LogInformation("Generated sampling plan: {WeekCount} weeks selected from {TotalYears} years", 
                samplingPlan.Count, (endDate - startDate).TotalDays / 365.25);

            // Step 2: Execute sampled weeks with progress tracking
            var progressCounter = 0;
            foreach (var sampledWeek in samplingPlan)
            {
                progressCounter++;
                _logger.LogInformation("Processing week {Progress}/{Total}: {Week} ({Regime}, {Events})", 
                    progressCounter, samplingPlan.Count, 
                    sampledWeek.WeekStart.ToString("yyyy-MM-dd"), 
                    sampledWeek.ExpectedRegime, 
                    string.Join(", ", sampledWeek.EventTags));

                try
                {
                    var weekResult = await _harness.RunSingleWeekAsync(sampledWeek.WeekStart, underlying);
                    if (weekResult != null)
                    {
                        sampledWeek.ActualResult = weekResult;
                        sampledWeek.WasExecuted = true;
                        results.SampledWeeks.Add(sampledWeek);
                    }
                    else
                    {
                        sampledWeek.WasExecuted = false;
                        sampledWeek.FailureReason = "No data available";
                        _logger.LogWarning("Week {Week} failed: No data available", sampledWeek.WeekStart);
                    }
                }
                catch (Exception ex)
                {
                    sampledWeek.WasExecuted = false;
                    sampledWeek.FailureReason = ex.Message;
                    _logger.LogError(ex, "Week {Week} failed with exception", sampledWeek.WeekStart);
                }

                // Throttle execution to avoid overwhelming data provider
                await Task.Delay(100);
            }

            // Step 3: Analyze regime and event coverage
            results.RegimeCoverage = AnalyzeRegimeCoverage(results.SampledWeeks);
            results.EventCoverage = AnalyzeEventCoverage(results.SampledWeeks);

            // Step 4: Calculate comprehensive metrics
            var successfulWeeks = results.SampledWeeks.Where(w => w.WasExecuted && w.ActualResult != null).ToList();
            if (successfulWeeks.Any())
            {
                results.OverallMetrics = CalculateSparseMetrics(successfulWeeks.Select(w => w.ActualResult!).ToList());
            }

            _logger.LogInformation("Sparse backtest completed: {SuccessCount}/{TotalCount} weeks executed, " +
                                 "Coverage: {RegimeCount} regimes, {EventCount} event types",
                successfulWeeks.Count, samplingPlan.Count,
                results.RegimeCoverage.RegimeBreakdown.Count,
                results.EventCoverage.EventBreakdown.Count);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during sparse backtest execution");
            throw;
        }
    }

    /// <summary>
    /// Generate intelligent sampling plan based on specified strategy
    /// </summary>
    private async Task<List<SampledWeek>> GenerateSamplingPlanAsync(
        DateTime startDate, 
        DateTime endDate, 
        SamplingStrategy strategy)
    {
        var plan = new List<SampledWeek>();

        switch (strategy)
        {
            case SamplingStrategy.Comprehensive:
                plan.AddRange(await GenerateComprehensiveSampling(startDate, endDate));
                break;

            case SamplingStrategy.RegimeFocused:
                plan.AddRange(await GenerateRegimeFocusedSampling(startDate, endDate));
                break;

            case SamplingStrategy.EventDriven:
                plan.AddRange(await GenerateEventDrivenSampling(startDate, endDate));
                break;

            case SamplingStrategy.StressTest:
                plan.AddRange(await GenerateStressTestSampling(startDate, endDate));
                break;

            case SamplingStrategy.Seasonal:
                plan.AddRange(await GenerateSeasonalSampling(startDate, endDate));
                break;

            default:
                plan.AddRange(await GenerateComprehensiveSampling(startDate, endDate));
                break;
        }

        return plan.OrderBy(w => w.WeekStart).ToList();
    }

    /// <summary>
    /// Comprehensive sampling: Representative weeks across all regimes and events
    /// Target: ~200-300 weeks across 20 years (1-1.5% coverage)
    /// </summary>
    private async Task<List<SampledWeek>> GenerateComprehensiveSampling(DateTime startDate, DateTime endDate)
    {
        var plan = new List<SampledWeek>();

        // Define major market periods with expected characteristics
        var majorPeriods = new[]
        {
            new { Start = new DateTime(2000, 1, 1), End = new DateTime(2002, 12, 31), Regime = "Bear", Events = new[] { "DotCom", "911" } },
            new { Start = new DateTime(2003, 1, 1), End = new DateTime(2007, 12, 31), Regime = "Bull", Events = new[] { "Recovery" } },
            new { Start = new DateTime(2008, 1, 1), End = new DateTime(2009, 12, 31), Regime = "Crisis", Events = new[] { "GFC", "Lehman" } },
            new { Start = new DateTime(2010, 1, 1), End = new DateTime(2015, 12, 31), Regime = "Recovery", Events = new[] { "QE", "FlashCrash" } },
            new { Start = new DateTime(2016, 1, 1), End = new DateTime(2019, 12, 31), Regime = "LowVol", Events = new[] { "Trump", "Brexit" } },
            new { Start = new DateTime(2020, 1, 1), End = new DateTime(2022, 12, 31), Regime = "Pandemic", Events = new[] { "COVID", "Meme", "War" } },
            new { Start = new DateTime(2023, 1, 1), End = new DateTime(2025, 12, 31), Regime = "Post", Events = new[] { "AI", "Fed" } }
        };

        foreach (var period in majorPeriods.Where(p => p.End >= startDate && p.Start <= endDate))
        {
            var periodStart = period.Start < startDate ? startDate : period.Start;
            var periodEnd = period.End > endDate ? endDate : period.End;
            
            // Sample 10-15 weeks per major period
            var weeksInPeriod = (int)((periodEnd - periodStart).TotalDays / 7);
            var targetSamples = Math.Min(15, Math.Max(10, weeksInPeriod / 52)); // ~10-15 weeks per year
            
            var periodWeeks = await SampleWeeksFromPeriod(periodStart, periodEnd, targetSamples, period.Regime, period.Events);
            plan.AddRange(periodWeeks);
        }

        // Add specific event weeks
        plan.AddRange(await SampleEventWeeks(startDate, endDate));

        // Add seasonal representatives
        plan.AddRange(await SampleSeasonalWeeks(startDate, endDate));

        return plan.DistinctBy(w => w.WeekStart).ToList();
    }

    /// <summary>
    /// Sample weeks from specific time period with regime/event targeting
    /// </summary>
    private async Task<List<SampledWeek>> SampleWeeksFromPeriod(
        DateTime periodStart, 
        DateTime periodEnd, 
        int targetCount, 
        string expectedRegime, 
        string[] eventTags)
    {
        var weeks = new List<SampledWeek>();
        var totalWeeks = (int)((periodEnd - periodStart).TotalDays / 7);
        
        if (totalWeeks <= targetCount)
        {
            // Sample every week in short periods
            var current = GetNextMonday(periodStart);
            while (current <= periodEnd)
            {
                weeks.Add(new SampledWeek
                {
                    WeekStart = current,
                    ExpectedRegime = expectedRegime,
                    EventTags = eventTags.ToList(),
                    SamplingReason = "PeriodComplete"
                });
                current = current.AddDays(7);
            }
        }
        else
        {
            // Stratified sampling across period
            var interval = totalWeeks / targetCount;
            for (int i = 0; i < targetCount; i++)
            {
                var weekOffset = i * interval + (interval / 2); // Middle of each segment
                var targetWeek = GetNextMonday(periodStart.AddDays(weekOffset * 7));
                
                if (targetWeek <= periodEnd)
                {
                    weeks.Add(new SampledWeek
                    {
                        WeekStart = targetWeek,
                        ExpectedRegime = expectedRegime,
                        EventTags = eventTags.ToList(),
                        SamplingReason = "StratifiedSample"
                    });
                }
            }
        }

        return weeks;
    }

    /// <summary>
    /// Sample weeks around major market events
    /// </summary>
    private async Task<List<SampledWeek>> SampleEventWeeks(DateTime startDate, DateTime endDate)
    {
        var eventWeeks = new List<SampledWeek>();

        // Define major market events with dates
        var majorEvents = new[]
        {
            new { Date = new DateTime(2001, 9, 11), Name = "911", Regime = "Crisis" },
            new { Date = new DateTime(2008, 9, 15), Name = "Lehman", Regime = "Crisis" },
            new { Date = new DateTime(2010, 5, 6), Name = "FlashCrash", Regime = "Stress" },
            new { Date = new DateTime(2016, 6, 23), Name = "Brexit", Regime = "Event" },
            new { Date = new DateTime(2016, 11, 8), Name = "Trump", Regime = "Event" },
            new { Date = new DateTime(2020, 3, 9), Name = "COVID", Regime = "Crisis" },
            new { Date = new DateTime(2021, 1, 27), Name = "GME", Regime = "Meme" },
            new { Date = new DateTime(2022, 2, 24), Name = "Ukraine", Regime = "War" }
        };

        foreach (var evt in majorEvents.Where(e => e.Date >= startDate && e.Date <= endDate))
        {
            // Sample week containing the event
            var eventMonday = GetMondayOfWeek(evt.Date);
            eventWeeks.Add(new SampledWeek
            {
                WeekStart = eventMonday,
                ExpectedRegime = evt.Regime,
                EventTags = new List<string> { evt.Name },
                SamplingReason = "EventWeek"
            });

            // Sample week after event (recovery behavior)
            var nextMonday = eventMonday.AddDays(7);
            if (nextMonday <= endDate)
            {
                eventWeeks.Add(new SampledWeek
                {
                    WeekStart = nextMonday,
                    ExpectedRegime = evt.Regime,
                    EventTags = new List<string> { $"{evt.Name}_Recovery" },
                    SamplingReason = "EventRecovery"
                });
            }
        }

        return eventWeeks;
    }

    /// <summary>
    /// Sample representative weeks for seasonal patterns
    /// </summary>
    private async Task<List<SampledWeek>> SampleSeasonalWeeks(DateTime startDate, DateTime endDate)
    {
        var seasonalWeeks = new List<SampledWeek>();

        // Target: 2-3 weeks per year for key seasonal periods
        var currentYear = startDate.Year;
        var endYear = endDate.Year;

        for (int year = currentYear; year <= endYear; year++)
        {
            // January Effect (first 2 weeks)
            var jan1 = new DateTime(year, 1, 1);
            if (jan1 >= startDate && jan1 <= endDate)
            {
                var jan1Monday = GetNextMonday(jan1);
                seasonalWeeks.Add(new SampledWeek
                {
                    WeekStart = jan1Monday,
                    ExpectedRegime = "Seasonal",
                    EventTags = new List<string> { "January_Effect" },
                    SamplingReason = "Seasonal"
                });
            }

            // Quad Witching (March, June, September, December 3rd Fridays)
            var quadMonths = new[] { 3, 6, 9, 12 };
            foreach (var month in quadMonths)
            {
                var quadFriday = GetThirdFriday(year, month);
                if (quadFriday >= startDate && quadFriday <= endDate)
                {
                    var quadMonday = GetMondayOfWeek(quadFriday);
                    seasonalWeeks.Add(new SampledWeek
                    {
                        WeekStart = quadMonday,
                        ExpectedRegime = "QuadWitch",
                        EventTags = new List<string> { "Quad_Witching", $"Q{(month - 1) / 3 + 1}" },
                        SamplingReason = "Seasonal"
                    });
                }
            }

            // Santa Rally (last week of December)
            var dec25 = new DateTime(year, 12, 25);
            if (dec25 >= startDate && dec25 <= endDate)
            {
                var santaMonday = GetMondayOfWeek(dec25);
                seasonalWeeks.Add(new SampledWeek
                {
                    WeekStart = santaMonday,
                    ExpectedRegime = "Holiday",
                    EventTags = new List<string> { "Santa_Rally" },
                    SamplingReason = "Seasonal"
                });
            }
        }

        return seasonalWeeks;
    }

    /// <summary>
    /// Regime-focused sampling for IV environment validation
    /// </summary>
    private async Task<List<SampledWeek>> GenerateRegimeFocusedSampling(DateTime startDate, DateTime endDate)
    {
        // Implementation would analyze VIX history to find representative weeks for each IV regime
        // For now, return subset of comprehensive sampling focused on regime diversity
        var comprehensive = await GenerateComprehensiveSampling(startDate, endDate);
        return comprehensive.GroupBy(w => w.ExpectedRegime).SelectMany(g => g.Take(20)).ToList();
    }

    /// <summary>
    /// Event-driven sampling for stress testing
    /// </summary>
    private async Task<List<SampledWeek>> GenerateEventDrivenSampling(DateTime startDate, DateTime endDate)
    {
        var eventWeeks = await SampleEventWeeks(startDate, endDate);
        
        // Add high-stress weeks (top 5% VIX moves)
        // This would require VIX history analysis in full implementation
        
        return eventWeeks;
    }

    /// <summary>
    /// Stress test sampling - worst-case scenarios only
    /// </summary>
    private async Task<List<SampledWeek>> GenerateStressTestSampling(DateTime startDate, DateTime endDate)
    {
        // Sample only the worst weeks in market history
        var stressWeeks = new List<SampledWeek>();

        var knownStressWeeks = new[]
        {
            new DateTime(2008, 10, 6),  // Week of Oct 2008 crash
            new DateTime(2020, 3, 9),   // COVID crash week
            new DateTime(2001, 9, 17),  // 9/11 recovery week
            new DateTime(2010, 5, 3),   // Flash crash week
            new DateTime(1987, 10, 19), // Black Monday (if in range)
            new DateTime(2018, 2, 5),   // Volmageddon
            new DateTime(2021, 1, 25)   // GameStop week
        };

        foreach (var stressDate in knownStressWeeks.Where(d => d >= startDate && d <= endDate))
        {
            var monday = GetMondayOfWeek(stressDate);
            stressWeeks.Add(new SampledWeek
            {
                WeekStart = monday,
                ExpectedRegime = "Stress",
                EventTags = new List<string> { "HistoricalStress" },
                SamplingReason = "StressTest"
            });
        }

        return stressWeeks;
    }

    /// <summary>
    /// Seasonal sampling for calendar effects
    /// </summary>
    private async Task<List<SampledWeek>> GenerateSeasonalSampling(DateTime startDate, DateTime endDate)
    {
        return await SampleSeasonalWeeks(startDate, endDate);
    }

    // Analysis methods
    private RegimeCoverageAnalysis AnalyzeRegimeCoverage(List<SampledWeek> sampledWeeks)
    {
        var regimeBreakdown = sampledWeeks
            .Where(w => w.WasExecuted)
            .GroupBy(w => w.ExpectedRegime)
            .ToDictionary(g => g.Key, g => new RegimeStats
            {
                WeekCount = g.Count(),
                AvgPnL = g.Where(w => w.ActualResult != null).Average(w => w.ActualResult!.WeeklyPnL),
                WinRate = g.Where(w => w.ActualResult != null).Count(w => w.ActualResult!.WeeklyPnL > 0) / 
                         (double)g.Count(w => w.ActualResult != null)
            });

        return new RegimeCoverageAnalysis
        {
            RegimeBreakdown = regimeBreakdown,
            TotalRegimesCovered = regimeBreakdown.Count,
            RegimeDistribution = regimeBreakdown.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.WeekCount)
        };
    }

    private EventCoverageAnalysis AnalyzeEventCoverage(List<SampledWeek> sampledWeeks)
    {
        var eventBreakdown = sampledWeeks
            .Where(w => w.WasExecuted)
            .SelectMany(w => w.EventTags.Select(tag => new { Tag = tag, Week = w }))
            .GroupBy(x => x.Tag)
            .ToDictionary(g => g.Key, g => new EventStats
            {
                WeekCount = g.Count(),
                AvgPnL = g.Where(x => x.Week.ActualResult != null).Average(x => x.Week.ActualResult!.WeeklyPnL),
                WinRate = g.Where(x => x.Week.ActualResult != null).Count(x => x.Week.ActualResult!.WeeklyPnL > 0) /
                         (double)g.Count(x => x.Week.ActualResult != null)
            });

        return new EventCoverageAnalysis
        {
            EventBreakdown = eventBreakdown,
            TotalEventTypesCovered = eventBreakdown.Count,
            EventDistribution = eventBreakdown.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.WeekCount)
        };
    }

    private OverallMetrics CalculateSparseMetrics(List<WeeklyResult> weeklyResults)
    {
        // Reuse the calculation from MondayToThuFriHarness
        return new OverallMetrics(); // Placeholder
    }

    // Helper methods
    private DateTime GetNextMonday(DateTime date)
    {
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)date.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0 && date.DayOfWeek != DayOfWeek.Monday)
            daysUntilMonday = 7;
        return date.Date.AddDays(daysUntilMonday);
    }

    private DateTime GetMondayOfWeek(DateTime date)
    {
        var daysFromMonday = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.Date.AddDays(-daysFromMonday);
    }

    private DateTime GetThirdFriday(int year, int month)
    {
        var firstDay = new DateTime(year, month, 1);
        var firstFriday = firstDay.AddDays(((int)DayOfWeek.Friday - (int)firstDay.DayOfWeek + 7) % 7);
        return firstFriday.AddDays(14); // Third Friday
    }
}

// Enums and supporting classes for sparse sampling
public enum SamplingStrategy
{
    Comprehensive,
    RegimeFocused,
    EventDriven,
    StressTest,
    Seasonal
}

public class SparseBacktestResults
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public SamplingStrategy SamplingStrategy { get; set; }
    public List<SampledWeek> SampledWeeks { get; set; } = new();
    public RegimeCoverageAnalysis RegimeCoverage { get; set; } = new();
    public EventCoverageAnalysis EventCoverage { get; set; } = new();
    public OverallMetrics OverallMetrics { get; set; } = new();
}

public class SampledWeek
{
    public DateTime WeekStart { get; set; }
    public string ExpectedRegime { get; set; } = "";
    public List<string> EventTags { get; set; } = new();
    public string SamplingReason { get; set; } = "";
    public bool WasExecuted { get; set; }
    public string FailureReason { get; set; } = "";
    public WeeklyResult? ActualResult { get; set; }
}

public class RegimeCoverageAnalysis
{
    public Dictionary<string, RegimeStats> RegimeBreakdown { get; set; } = new();
    public int TotalRegimesCovered { get; set; }
    public Dictionary<string, int> RegimeDistribution { get; set; } = new();
}

public class EventCoverageAnalysis
{
    public Dictionary<string, EventStats> EventBreakdown { get; set; } = new();
    public int TotalEventTypesCovered { get; set; }
    public Dictionary<string, int> EventDistribution { get; set; } = new();
}

public class RegimeStats
{
    public int WeekCount { get; set; }
    public decimal AvgPnL { get; set; }
    public double WinRate { get; set; }
}

public class EventStats
{
    public int WeekCount { get; set; }
    public decimal AvgPnL { get; set; }
    public double WinRate { get; set; }
}