using System;
using System.Collections.Generic;
using System.Linq;

namespace ODTE.Strategy
{
    /// <summary>
    /// RegimeSwitcher: 24-Day Rolling Strategy Framework
    /// Maximizes returns in each 24-day period through adaptive regime-based strategy selection
    /// After 24 days: rules reset, drawdown reset, allocation reset → fresh program starts
    /// </summary>
    public partial class RegimeSwitcher
    {
        public enum Regime
        {
            Calm,     // Low vol, no strong trend: Credit BWB focus
            Mixed,    // Moderate vol/trend: BWB + Tail Extender
            Convex    // High vol/trend: Ratio Backspread focus
        }

        public class MarketConditions
        {
            public double IVR { get; set; }              // IV Rank
            public double VIX { get; set; }              // VIX level
            public double TermSlope { get; set; }        // IV_0DTE / IV_30D ratio
            public double TrendScore { get; set; }       // SPX 5-min trend score [-1, 1]
            public double RealizedVsImplied { get; set; } // Realized(30m) / Implied(0DTE)
            public DateTime Date { get; set; }
            public string MarketRegime { get; set; } = "";
        }

        public class StrategyParameters
        {
            public string Side { get; set; } = "";           // "Put" or "Call"
            public (int Narrow, int Wide) Wings { get; set; } // Wing widths
            public double CreditMin { get; set; }            // Minimum credit threshold
            public double BudgetPct { get; set; }            // Budget percentage for tail extender
            public double TargetDebit { get; set; }          // Target debit for ratio spreads
            public bool UseTailExtender { get; set; }
            public bool UseOppositeIncome { get; set; }      // Tiny opposite BWB for income
        }

        public class TwentyFourDayPeriod
        {
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public int PeriodNumber { get; set; }
            public double StartingCapital { get; set; } = 5000;
            public double CurrentCapital { get; set; } = 5000;
            public double MaxDrawdown { get; set; } = 0;
            public double Peak { get; set; } = 5000;
            public List<DailyResult> DailyResults { get; set; } = new();
            public Dictionary<Regime, int> RegimeDays { get; set; } = new();
            public Dictionary<Regime, double> RegimePnL { get; set; } = new();
            public bool IsComplete => (EndDate - StartDate).TotalDays >= 24;
        }

        public partial class DailyResult
        {
            public DateTime Date { get; set; }
            public Regime DetectedRegime { get; set; }
            public StrategyParameters StrategyUsed { get; set; } = new();
            public double DailyPnL { get; set; }
            public double CumulativePnL { get; set; }
            public double DrawdownFromPeak { get; set; }
            public MarketConditions Conditions { get; set; } = new();
            public string ExecutionSummary { get; set; } = "";
        }

        public class RegimeSwitcherResults
        {
            public List<TwentyFourDayPeriod> Periods { get; set; } = new();
            public double TotalReturn { get; set; }
            public double AverageReturn { get; set; }
            public double BestPeriodReturn { get; set; }
            public double WorstPeriodReturn { get; set; }
            public double WinRate { get; set; } // % of profitable periods
            public Dictionary<Regime, double> RegimePerformance { get; set; } = new();
            public int TotalPeriods { get; set; }
        }

        private readonly Random _random;

        public RegimeSwitcher(Random random = null)
        {
            _random = random ?? new Random(42);
        }

        /// <summary>
        /// Run complete 24-day rolling strategy across historical period
        /// </summary>
        public RegimeSwitcherResults RunHistoricalAnalysis(DateTime startDate, DateTime endDate)
        {
            var results = new RegimeSwitcherResults();
            var currentDate = startDate;
            var periodNumber = 1;

            Console.WriteLine("🔄 REGIME SWITCHER: 24-Day Rolling Strategy Analysis");
            Console.WriteLine("=" .PadRight(70, '='));
            Console.WriteLine($"Period: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            Console.WriteLine($"Target: Maximize returns in each 24-day period");
            Console.WriteLine();

            while (currentDate.AddDays(24) <= endDate)
            {
                var period = RunTwentyFourDayPeriod(currentDate, periodNumber);
                results.Periods.Add(period);

                Console.WriteLine($"Period {periodNumber}: {period.StartDate:MM/dd} - {period.EndDate:MM/dd} " +
                                  $"| Return: {((period.CurrentCapital / period.StartingCapital - 1) * 100):F1}% " +
                                  $"| P&L: ${period.CurrentCapital - period.StartingCapital:F0} " +
                                  $"| Max DD: {period.MaxDrawdown:F0}");

                // Move to next 24-day period (fresh start)
                currentDate = currentDate.AddDays(24);
                periodNumber++;
            }

            // Calculate aggregate results
            CalculateAggregateResults(results);
            PrintDetailedResults(results);

            return results;
        }

        /// <summary>
        /// Execute one complete 24-day period with regime-based strategy switching
        /// </summary>
        private TwentyFourDayPeriod RunTwentyFourDayPeriod(DateTime startDate, int periodNumber)
        {
            var period = new TwentyFourDayPeriod
            {
                StartDate = startDate,
                EndDate = startDate.AddDays(24),
                PeriodNumber = periodNumber,
                StartingCapital = 5000, // Reset capital each period
                CurrentCapital = 5000,
                Peak = 5000
            };

            // Initialize regime tracking
            foreach (Regime regime in Enum.GetValues<Regime>())
            {
                period.RegimeDays[regime] = 0;
                period.RegimePnL[regime] = 0;
            }

            var currentDate = startDate;
            for (int day = 0; day < 24; day++)
            {
                // Skip weekends
                while (currentDate.DayOfWeek == DayOfWeek.Saturday || 
                       currentDate.DayOfWeek == DayOfWeek.Sunday)
                {
                    currentDate = currentDate.AddDays(1);
                }

                var dailyResult = ExecuteDailyStrategy(currentDate, period);
                period.DailyResults.Add(dailyResult);

                // Update period metrics
                period.CurrentCapital += dailyResult.DailyPnL;
                period.Peak = Math.Max(period.Peak, period.CurrentCapital);
                period.MaxDrawdown = Math.Min(period.MaxDrawdown, period.CurrentCapital - period.Peak);

                // Track regime usage
                period.RegimeDays[dailyResult.DetectedRegime]++;
                period.RegimePnL[dailyResult.DetectedRegime] += dailyResult.DailyPnL;

                currentDate = currentDate.AddDays(1);
            }

            return period;
        }

        /// <summary>
        /// Execute daily strategy based on detected market regime
        /// </summary>
        private DailyResult ExecuteDailyStrategy(DateTime date, TwentyFourDayPeriod period)
        {
            var result = new DailyResult { Date = date };

            // Generate market conditions for the day
            result.Conditions = GenerateMarketConditions(date);

            // Classify regime using comprehensive criteria
            result.DetectedRegime = ClassifyRegime(result.Conditions);

            // Execute regime-specific strategy
            result.StrategyUsed = GetStrategyParameters(result.DetectedRegime, result.Conditions);

            // Simulate strategy execution and P&L
            result.DailyPnL = SimulateStrategyExecution(result.StrategyUsed, result.Conditions, period);

            // Update cumulative metrics
            var cumulativePnL = period.CurrentCapital + result.DailyPnL - period.StartingCapital;
            result.CumulativePnL = cumulativePnL;
            result.DrawdownFromPeak = Math.Min(0, (period.CurrentCapital + result.DailyPnL) - period.Peak);

            result.ExecutionSummary = GenerateExecutionSummary(result);

            return result;
        }

        /// <summary>
        /// Classify market regime using multi-factor analysis
        /// Implementation of the pseudo-code classification logic
        /// </summary>
        protected virtual Regime ClassifyRegime(MarketConditions conditions)
        {
            // Primary classification based on VIX and trend
            if (conditions.VIX > 40 || Math.Abs(conditions.TrendScore) >= 0.8)
            {
                return Regime.Convex;
            }
            else if (conditions.VIX > 25 || conditions.RealizedVsImplied > 1.1)
            {
                return Regime.Mixed;
            }
            else
            {
                return Regime.Calm;
            }
        }

        /// <summary>
        /// Get strategy parameters based on detected regime
        /// Implementation of the pseudo-code strategy selection
        /// </summary>
        protected virtual StrategyParameters GetStrategyParameters(Regime regime, MarketConditions conditions)
        {
            var parameters = new StrategyParameters();

            switch (regime)
            {
                case Regime.Calm:
                    parameters.Side = DetermineBiasSide(conditions);
                    parameters.Wings = (10, 30);
                    parameters.CreditMin = 0.25;
                    parameters.UseTailExtender = false;
                    parameters.UseOppositeIncome = false;
                    break;

                case Regime.Mixed:
                    parameters.Side = DetermineBiasSide(conditions);
                    parameters.Wings = (10, 40);
                    parameters.CreditMin = 0.30;
                    parameters.BudgetPct = 0.15; // 15% of BWB credit for tail extender
                    parameters.UseTailExtender = true;
                    parameters.UseOppositeIncome = false;
                    break;

                case Regime.Convex:
                    parameters.Side = DetermineRiskSide(conditions);
                    parameters.TargetDebit = 0.25;
                    parameters.UseTailExtender = false;
                    parameters.UseOppositeIncome = _random.NextDouble() < 0.3; // 30% chance for income
                    break;
            }

            return parameters;
        }

        /// <summary>
        /// Simulate strategy execution and return daily P&L
        /// </summary>
        protected virtual double SimulateStrategyExecution(StrategyParameters strategy, MarketConditions conditions, TwentyFourDayPeriod period)
        {
            var pnl = 0.0;

            switch (ClassifyRegime(conditions))
            {
                case Regime.Calm:
                    pnl = SimulateCreditBWB(strategy, conditions, isCalmRegime: true);
                    break;

                case Regime.Mixed:
                    pnl = SimulateCreditBWB(strategy, conditions, isCalmRegime: false);
                    if (strategy.UseTailExtender)
                    {
                        pnl += SimulateTailExtender(strategy, conditions);
                    }
                    break;

                case Regime.Convex:
                    pnl = SimulateRatioBackspread(strategy, conditions);
                    if (strategy.UseOppositeIncome)
                    {
                        pnl += SimulateCreditBWB(strategy, conditions, isCalmRegime: false) * 0.25; // 0.25x size
                    }
                    break;
            }

            // Apply position sizing based on current drawdown in period
            var drawdownAdjustment = CalculateDrawdownAdjustment(period);
            pnl *= drawdownAdjustment;

            return pnl;
        }

        /// <summary>
        /// Simulate Credit BWB execution
        /// </summary>
        private double SimulateCreditBWB(StrategyParameters strategy, MarketConditions conditions, bool isCalmRegime)
        {
            var baseCredit = strategy.Wings.Narrow * strategy.CreditMin;
            var maxLoss = strategy.Wings.Wide - baseCredit;

            // Enhanced performance in calm conditions
            if (isCalmRegime)
            {
                if (_random.NextDouble() < 0.92) // 92% win rate in calm
                {
                    return baseCredit * (0.90 + _random.NextDouble() * 0.10); // 90-100% credit capture
                }
                else
                {
                    return -_random.Next(5, 15); // Small losses
                }
            }
            else
            {
                if (_random.NextDouble() < 0.75) // 75% win rate in mixed/volatile
                {
                    return baseCredit * (0.70 + _random.NextDouble() * 0.20); // 70-90% credit capture
                }
                else
                {
                    return -_random.Next(5, Math.Max(15, (int)maxLoss)); // Variable losses
                }
            }
        }

        /// <summary>
        /// Simulate Tail Extender overlay
        /// </summary>
        private double SimulateTailExtender(StrategyParameters strategy, MarketConditions conditions)
        {
            var tailCost = strategy.BudgetPct * 20; // Cost of tail protection

            // Tail extender pays off in extreme moves
            if (Math.Abs(conditions.TrendScore) > 0.6 || conditions.VIX > 35)
            {
                if (_random.NextDouble() < 0.15) // 15% chance of big payoff
                {
                    return _random.Next(50, 200) - tailCost; // Large convex payoff
                }
            }

            return -tailCost; // Most of the time, lose the tail cost
        }

        /// <summary>
        /// Simulate Ratio Backspread execution
        /// </summary>
        private double SimulateRatioBackspread(StrategyParameters strategy, MarketConditions conditions)
        {
            var netCredit = Math.Max(-strategy.TargetDebit, _random.NextDouble() * 2 - 1); // Small credit or debit

            // Ratio backspreads perform well in volatile/trending conditions
            if (conditions.VIX > 35 || Math.Abs(conditions.TrendScore) > 0.7)
            {
                if (_random.NextDouble() < 0.25) // 25% chance of large convex payoff
                {
                    return netCredit + _random.Next(100, 500); // Unlimited upside simulation
                }
                else if (_random.NextDouble() < 0.6) // 60% small profit
                {
                    return netCredit + _random.Next(10, 50);
                }
                else // 15% max loss
                {
                    return -_random.Next(30, 80);
                }
            }
            else
            {
                return netCredit - _random.Next(5, 25); // Small loss in non-volatile conditions
            }
        }

        /// <summary>
        /// Generate realistic market conditions
        /// </summary>
        private MarketConditions GenerateMarketConditions(DateTime date)
        {
            var conditions = new MarketConditions { Date = date };

            // Simulate realistic market indicators
            conditions.VIX = 15 + _random.NextDouble() * 40; // 15-55 VIX range
            conditions.IVR = _random.NextDouble() * 100; // 0-100 IV Rank
            conditions.TermSlope = 0.8 + _random.NextDouble() * 0.4; // 0.8-1.2 term structure
            conditions.TrendScore = (_random.NextDouble() - 0.5) * 2; // -1 to +1
            conditions.RealizedVsImplied = 0.7 + _random.NextDouble() * 0.8; // 0.7-1.5

            // Add correlation and regime clustering
            if (conditions.VIX > 30)
            {
                conditions.RealizedVsImplied *= 1.2; // Higher real vol in volatile periods
                conditions.TrendScore *= 1.5; // Stronger trends in volatile periods
            }

            return conditions;
        }

        /// <summary>
        /// Determine bias side for BWB
        /// </summary>
        private string DetermineBiasSide(MarketConditions conditions)
        {
            // Use trend score to determine bias
            if (conditions.TrendScore > 0.3)
                return "Call"; // Bullish bias
            else if (conditions.TrendScore < -0.3)
                return "Put"; // Bearish bias
            else
                return _random.NextDouble() < 0.5 ? "Put" : "Call"; // Neutral
        }

        /// <summary>
        /// Determine risk side for convex strategies
        /// </summary>
        private string DetermineRiskSide(MarketConditions conditions)
        {
            // Risk side is where we expect the move
            if (Math.Abs(conditions.TrendScore) > 0.6)
                return conditions.TrendScore > 0 ? "Call" : "Put";
            else
                return _random.NextDouble() < 0.6 ? "Put" : "Call"; // Slight bearish bias
        }

        /// <summary>
        /// Calculate position size adjustment based on period drawdown
        /// </summary>
        private double CalculateDrawdownAdjustment(TwentyFourDayPeriod period)
        {
            var drawdownPercent = Math.Abs(period.MaxDrawdown) / period.Peak;

            if (drawdownPercent > 0.15) // >15% drawdown
                return 0.5; // Half size
            else if (drawdownPercent > 0.10) // >10% drawdown
                return 0.75; // Three-quarter size
            else
                return 1.0; // Full size
        }

        /// <summary>
        /// Generate execution summary string
        /// </summary>
        private string GenerateExecutionSummary(DailyResult result)
        {
            var strategy = result.StrategyUsed;
            var regime = result.DetectedRegime;

            return regime switch
            {
                Regime.Calm => $"Calm: BWB {strategy.Side} {strategy.Wings.Narrow}-{strategy.Wings.Wide}pt, credit≥{strategy.CreditMin:F2}",
                Regime.Mixed => $"Mixed: BWB {strategy.Side} {strategy.Wings.Narrow}-{strategy.Wings.Wide}pt + TailExt {strategy.BudgetPct:P0}",
                Regime.Convex => $"Convex: RatioBS {strategy.Side}, debit≤{strategy.TargetDebit:F2}" + 
                                (strategy.UseOppositeIncome ? " + Income BWB" : ""),
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Calculate aggregate results across all periods
        /// </summary>
        private void CalculateAggregateResults(RegimeSwitcherResults results)
        {
            results.TotalPeriods = results.Periods.Count;
            if (results.TotalPeriods == 0) return;

            var returns = results.Periods.Select(p => (p.CurrentCapital / p.StartingCapital - 1) * 100).ToList();

            results.AverageReturn = returns.Average();
            results.BestPeriodReturn = returns.Max();
            results.WorstPeriodReturn = returns.Min();
            results.WinRate = returns.Count(r => r > 0) / (double)returns.Count;

            // Calculate total compound return
            var compoundMultiplier = 1.0;
            foreach (var period in results.Periods)
            {
                compoundMultiplier *= (period.CurrentCapital / period.StartingCapital);
            }
            results.TotalReturn = (compoundMultiplier - 1) * 100;

            // Aggregate regime performance
            foreach (Regime regime in Enum.GetValues<Regime>())
            {
                var regimePnL = results.Periods.SelectMany(p => p.DailyResults)
                    .Where(d => d.DetectedRegime == regime)
                    .Sum(d => d.DailyPnL);
                results.RegimePerformance[regime] = regimePnL;
            }
        }

        /// <summary>
        /// Print detailed analysis results
        /// </summary>
        private void PrintDetailedResults(RegimeSwitcherResults results)
        {
            Console.WriteLine();
            Console.WriteLine("📊 REGIME SWITCHER ANALYSIS RESULTS");
            Console.WriteLine("=" .PadRight(50, '='));
            Console.WriteLine($"Total Periods: {results.TotalPeriods}");
            Console.WriteLine($"Average Return per Period: {results.AverageReturn:F1}%");
            Console.WriteLine($"Best Period Return: {results.BestPeriodReturn:F1}%");
            Console.WriteLine($"Worst Period Return: {results.WorstPeriodReturn:F1}%");
            Console.WriteLine($"Win Rate: {results.WinRate:P1}");
            Console.WriteLine($"Total Compound Return: {results.TotalReturn:F1}%");

            Console.WriteLine("\n🎯 REGIME PERFORMANCE:");
            foreach (var (regime, pnl) in results.RegimePerformance.OrderByDescending(r => r.Value))
            {
                Console.WriteLine($"  {regime}: ${pnl:F0}");
            }

            Console.WriteLine("\n📈 TOP 5 PERIODS:");
            var topPeriods = results.Periods
                .OrderByDescending(p => (p.CurrentCapital / p.StartingCapital - 1) * 100)
                .Take(5);

            foreach (var period in topPeriods)
            {
                var returnPct = (period.CurrentCapital / period.StartingCapital - 1) * 100;
                var dominantRegime = period.RegimeDays.OrderByDescending(r => r.Value).First().Key;
                Console.WriteLine($"  Period {period.PeriodNumber}: {returnPct:F1}% (Dominant: {dominantRegime})");
            }
        }
    }
}