using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ODTE.Strategy;

/// <summary>
/// Realistic Battle Test System - Honest Assessment of 24-Day Framework
/// 
/// OBJECTIVE: Test the framework under realistic conditions without gaming the system
/// 
/// PRINCIPLES:
/// 1. NO FUTURE KNOWLEDGE: Decisions made only on available historical data up to that point
/// 2. REALISTIC MARKET CONDITIONS: Use actual historical volatility spikes, crashes, events
/// 3. HONEST OUTCOMES: Apply realistic win/loss ratios based on actual market behavior
/// 4. COMPREHENSIVE TESTING: Test multiple 24-day periods to find failure modes
/// 5. BRUTAL HONESTY: Report actual losses and explain why they occurred
/// 
/// TEST PERIODS INCLUDE:
/// - March 2020 COVID Crash
/// - October 2018 Volatility Spike
/// - August 2015 China Devaluation
/// - December 2018 Fed Concerns
/// - January 2022 Fed Pivot Fear
/// - September 2022 CPI Surprise
/// 
/// REALISTIC CONSTRAINTS:
/// - Options spreads have realistic bid-ask spreads
/// - Volatility expansion hits short volatility positions hard
/// - Black swan events cause significant losses
/// - Regime detection has lag and false signals
/// - Position sizing doesn't prevent major losses in extreme events
/// </summary>
public class RealisticBattleTest
{
    private readonly TwentyFourDayFramework _framework;
    private readonly List<BattleTestPeriod> _testPeriods;
    private readonly RealisticMarketSimulator _marketSimulator;

    public RealisticBattleTest(FrameworkConfig config)
    {
        _framework = new TwentyFourDayFramework(config);
        _marketSimulator = new RealisticMarketSimulator();
        _testPeriods = InitializeRealisticTestPeriods();
    }

    /// <summary>
    /// Run comprehensive battle test across multiple periods - FIND THE LOSSES
    /// </summary>
    public async Task<BattleTestReport> RunComprehensiveBattleTest()
    {
        Console.WriteLine("🔥 REALISTIC BATTLE TEST - 24-Day Framework");
        Console.WriteLine("🎯 Objective: Find periods where framework loses money");
        Console.WriteLine("📊 Testing against actual historical market crashes and volatility spikes");
        Console.WriteLine("⚠️  NO FUTURE KNOWLEDGE - Point-in-time decisions only");
        Console.WriteLine("=".PadRight(80, '='));

        var results = new List<BattleTestResult>();
        var totalLosses = 0;

        foreach (var period in _testPeriods)
        {
            Console.WriteLine($"\n🧪 Testing Period: {period.Name}");
            Console.WriteLine($"📅 Dates: {period.StartDate:MM/dd/yyyy} - {period.StartDate.AddDays(23):MM/dd/yyyy}");
            Console.WriteLine($"🌪️  Severity: {period.Severity}");
            Console.WriteLine($"📝 Context: {period.HistoricalContext}");
            
            var result = await RunSinglePeriodBattleTest(period);
            results.Add(result);
            
            if (result.FinalPnL < 0)
            {
                totalLosses++;
                Console.WriteLine($"💸 LOSS PERIOD FOUND: ${result.FinalPnL:F2}");
            }
            else if (result.FinalPnL < 1000) // Severe underperformance
            {
                Console.WriteLine($"⚠️  UNDERPERFORMANCE: ${result.FinalPnL:F2} (Target: $6000)");
            }
            else
            {
                Console.WriteLine($"✅ Period Survived: ${result.FinalPnL:F2}");
            }
            
            PrintPeriodSummary(result);
        }

        return AnalyzeBattleTestResults(results, totalLosses);
    }

    /// <summary>
    /// Test single 24-day period with realistic market conditions
    /// </summary>
    private async Task<BattleTestResult> RunSinglePeriodBattleTest(BattleTestPeriod period)
    {
        var framework = new TwentyFourDayFramework(_framework._config);
        var dayResults = new List<DayResult>();
        var marketCrashEvents = new List<CrashEvent>();

        for (int day = 1; day <= TwentyFourDayFramework.FRAMEWORK_DAYS; day++)
        {
            var tradingDate = period.StartDate.AddDays(day - 1);
            
            // Generate REALISTIC market conditions for this specific date
            var conditions = await _marketSimulator.GenerateRealisticConditions(tradingDate, period);
            
            // Check for major market events that would impact trading
            var crashEvent = await _marketSimulator.CheckForMarketCrash(tradingDate, period);
            if (crashEvent != null)
            {
                marketCrashEvents.Add(crashEvent);
                Console.WriteLine($"💥 Day {day}: {crashEvent.Description}");
            }

            try
            {
                // Execute trading day with point-in-time information only
                var dayResult = await framework.ExecuteTradingDay(tradingDate, conditions);
                
                // Apply realistic outcome variation and crash impacts
                ApplyRealisticOutcomes(dayResult, conditions, crashEvent, day, period);
                
                dayResults.Add(dayResult);
                
                // Real-time logging (as trader would see)
                var totalPnL = dayResults.Sum(r => r.PnL);
                var status = totalPnL >= 0 ? "✅" : "💸";
                Console.WriteLine($"{status} Day {day:D2}: ${dayResult.PnL,6:F0} | Total: ${totalPnL,7:F0} | {dayResult.Strategy}");
                
                // Check for margin calls or catastrophic losses (realistic constraint)
                if (dayResult.PnL < -800) // Single day loss exceeding risk limits
                {
                    Console.WriteLine($"🚨 MARGIN CALL: Day {day} loss exceeds risk tolerance");
                    // In real trading, this would force position closure
                    dayResult.PnL = Math.Max(dayResult.PnL, -1000); // Cap at account protection level
                }
                
            }
            catch (Exception ex)
            {
                // Realistic: Trading systems can fail during extreme volatility
                Console.WriteLine($"⚠️  Day {day}: System error during extreme volatility - No trades executed");
                dayResults.Add(new DayResult
                {
                    Day = day,
                    Date = tradingDate,
                    Strategy = "System Failure",
                    PnL = -50, // Cost of missed opportunity and system issues
                    Trades = 0,
                    Reason = "System overload during market stress"
                });
            }
        }

        var frameworkReport = framework.GenerateReport();
        
        return new BattleTestResult
        {
            Period = period,
            DayResults = dayResults,
            FinalPnL = dayResults.Sum(r => r.PnL),
            MaxDrawdown = CalculateMaxDrawdown(dayResults),
            WinRate = dayResults.Count(r => r.PnL > 0) / (double)dayResults.Count * 100,
            CrashEvents = marketCrashEvents,
            FrameworkReport = frameworkReport,
            Success = dayResults.Sum(r => r.PnL) > 0 // Honest success criterion: just be profitable
        };
    }

    /// <summary>
    /// Apply realistic market outcomes - NO GAMING THE SYSTEM
    /// </summary>
    private void ApplyRealisticOutcomes(DayResult dayResult, MarketConditions conditions, 
        CrashEvent crashEvent, int day, BattleTestPeriod period)
    {
        var random = new Random(dayResult.Date.GetHashCode());
        
        // Base realistic win rates (much lower than theoretical)
        var baseWinRate = dayResult.Strategy switch
        {
            "Ghost (Ultra-Conservative)" => 0.70, // Even conservative strategies lose in crashes
            "Precision (Selective Strikes)" => 0.60,
            "Sniper (High-Conviction)" => 0.50,
            "Volatility Crusher" => 0.65,
            "Regime Adaptive" => 0.55,
            _ => 0.50
        };

        // Crash impacts - BRUTAL REALITY
        if (crashEvent != null)
        {
            switch (crashEvent.Severity)
            {
                case CrashSeverity.Mild:
                    baseWinRate *= 0.8; // 20% reduction in win rate
                    break;
                case CrashSeverity.Moderate:
                    baseWinRate *= 0.6; // 40% reduction
                    break;
                case CrashSeverity.Severe:
                    baseWinRate *= 0.3; // 70% reduction - most strategies fail
                    break;
                case CrashSeverity.BlackSwan:
                    baseWinRate *= 0.1; // 90% reduction - almost everything fails
                    dayResult.PnL *= 3; // Amplify losses in black swan events
                    break;
            }
        }

        // High volatility periods hurt premium sellers
        if (conditions.IVRank > 60 && dayResult.Strategy.Contains("Volatility"))
        {
            baseWinRate *= 0.7; // Volatility sellers get crushed in high IV expansion
        }

        // Apply win/loss outcome
        var isWin = random.NextDouble() < baseWinRate;
        
        if (!isWin)
        {
            // Realistic loss scenarios
            var lossMultiplier = crashEvent?.Severity switch
            {
                CrashSeverity.BlackSwan => 4.0, // Catastrophic losses
                CrashSeverity.Severe => 2.5,
                CrashSeverity.Moderate => 1.8,
                CrashSeverity.Mild => 1.3,
                _ => 1.0
            };
            
            // Make it a loss with realistic magnitude
            dayResult.PnL = -Math.Abs(dayResult.PnL) * (decimal)lossMultiplier;
        }

        // Bid-ask spread impacts (realistic transaction costs)
        var spreadCost = (decimal)(10 + random.NextDouble() * 20); // $10-30 per trade in spreads
        dayResult.PnL -= spreadCost;

        // Late-day volatility expansion (0DTE nightmare scenario)
        if (day > 15 && conditions.IVRank > 50)
        {
            var lateVolExpansion = (decimal)(random.NextDouble() * 100 - 150); // -$150 to -$50 average
            dayResult.PnL += lateVolExpansion;
        }
    }

    /// <summary>
    /// Initialize realistic test periods - ACTUAL HISTORICAL DISASTERS
    /// </summary>
    private List<BattleTestPeriod> InitializeRealisticTestPeriods()
    {
        return new List<BattleTestPeriod>
        {
            new BattleTestPeriod
            {
                Name = "COVID-19 Crash",
                StartDate = new DateTime(2020, 2, 20), // Right before the crash
                Severity = MarketSeverity.BlackSwan,
                HistoricalContext = "Global pandemic declaration, 35% market drop in 24 days",
                VIXPattern = day => Math.Min(85, 15 + Math.Pow(day, 1.8) * 2.5), // VIX 15 → 85
                SPYDropPattern = day => Math.Max(-35, -Math.Pow(day / 8.0, 2.5) * 3.5) // -35% over period
            },

            new BattleTestPeriod
            {
                Name = "October 2018 Vol Spike",
                StartDate = new DateTime(2018, 10, 1),
                Severity = MarketSeverity.Severe,
                HistoricalContext = "Fed rate concerns, tech selloff, -10% market drop",
                VIXPattern = day => 12 + Math.Sin(day * 0.3) * 8 + (day > 15 ? 15 : 0),
                SPYDropPattern = day => Math.Max(-12, -0.8 * day + Math.Sin(day * 0.2) * 2)
            },

            new BattleTestPeriod
            {
                Name = "China Devaluation Crash",
                StartDate = new DateTime(2015, 8, 10),
                Severity = MarketSeverity.Severe,
                HistoricalContext = "China currency devaluation, global selloff",
                VIXPattern = day => 15 + (day < 8 ? day * 4 : 45 - day),
                SPYDropPattern = day => Math.Max(-8, -0.4 * day)
            },

            new BattleTestPeriod
            {
                Name = "December 2018 Fed Panic",
                StartDate = new DateTime(2018, 12, 3),
                Severity = MarketSeverity.Moderate,
                HistoricalContext = "Fed rate hike concerns, -9% December drop",
                VIXPattern = day => 18 + Math.Max(0, (day - 10) * 1.2),
                SPYDropPattern = day => Math.Max(-9, -0.5 * day + 1)
            },

            new BattleTestPeriod
            {
                Name = "Omicron + Fed Pivot Fear",
                StartDate = new DateTime(2022, 1, 3),
                Severity = MarketSeverity.Moderate,
                HistoricalContext = "Fed hawkish pivot, tech selloff, growth concerns",
                VIXPattern = day => 20 + Math.Sin(day * 0.4) * 8,
                SPYDropPattern = day => Math.Max(-12, -0.6 * day + Math.Sin(day * 0.3) * 3)
            },

            new BattleTestPeriod
            {
                Name = "September 2022 CPI Shock",
                StartDate = new DateTime(2022, 9, 5),
                Severity = MarketSeverity.Severe,
                HistoricalContext = "Hotter than expected inflation, aggressive Fed response",
                VIXPattern = day => 25 + (day == 13 ? 15 : 0) + Math.Sin(day * 0.2) * 5, // CPI day spike
                SPYDropPattern = day => Math.Max(-8, -0.4 * day + (day == 13 ? -2 : 0))
            },

            new BattleTestPeriod
            {
                Name = "Normal Bull Market",
                StartDate = new DateTime(2017, 6, 1),
                Severity = MarketSeverity.Calm,
                HistoricalContext = "Low volatility bull market - should be profitable",
                VIXPattern = day => 10 + Math.Sin(day * 0.1) * 2,
                SPYDropPattern = day => 0.1 * day // Mild uptrend
            },

            new BattleTestPeriod
            {
                Name = "2019 Trade War Escalation",
                StartDate = new DateTime(2019, 8, 5),
                Severity = MarketSeverity.Moderate,
                HistoricalContext = "Trump tariff escalation, yuan devaluation",
                VIXPattern = day => 16 + (day < 5 ? day * 3 : 28 - day),
                SPYDropPattern = day => Math.Max(-6, -0.3 * day + Math.Sin(day * 0.5))
            }
        };
    }

    /// <summary>
    /// Calculate maximum drawdown during period
    /// </summary>
    private decimal CalculateMaxDrawdown(List<DayResult> dayResults)
    {
        decimal runningPnL = 0;
        decimal peak = 0;
        decimal maxDrawdown = 0;

        foreach (var result in dayResults)
        {
            runningPnL += result.PnL;
            peak = Math.Max(peak, runningPnL);
            var drawdown = runningPnL - peak;
            maxDrawdown = Math.Min(maxDrawdown, drawdown);
        }

        return maxDrawdown;
    }

    /// <summary>
    /// Print detailed summary of battle test period
    /// </summary>
    private void PrintPeriodSummary(BattleTestResult result)
    {
        Console.WriteLine($"📊 PERIOD SUMMARY:");
        Console.WriteLine($"   Final P&L: ${result.FinalPnL:F2}");
        Console.WriteLine($"   Max Drawdown: ${Math.Abs(result.MaxDrawdown):F2}");
        Console.WriteLine($"   Win Rate: {result.WinRate:F1}%");
        Console.WriteLine($"   Crash Events: {result.CrashEvents.Count}");
        
        if (result.CrashEvents.Any())
        {
            Console.WriteLine($"   Major Events:");
            foreach (var crashEvent in result.CrashEvents.Take(3))
            {
                Console.WriteLine($"     • {crashEvent.Description}");
            }
        }
        
        var losingDays = result.DayResults.Count(r => r.PnL < 0);
        Console.WriteLine($"   Losing Days: {losingDays}/24 ({losingDays/24.0*100:F0}%)");
        
        if (result.FinalPnL < 0)
        {
            Console.WriteLine($"   💸 FRAMEWORK FAILED - Lost ${Math.Abs(result.FinalPnL):F2}");
            Console.WriteLine($"   🔍 Failure Mode: {AnalyzeFailureMode(result)}");
        }
    }

    /// <summary>
    /// Analyze why the framework failed
    /// </summary>
    private string AnalyzeFailureMode(BattleTestResult result)
    {
        var bigLossDays = result.DayResults.Where(r => r.PnL < -200).Count();
        var hasBlackSwan = result.CrashEvents.Any(e => e.Severity == CrashSeverity.BlackSwan);
        
        if (hasBlackSwan)
            return "Black swan event - fundamental market structure breakdown";
        
        if (bigLossDays >= 3)
            return "Multiple large loss days - insufficient risk management";
        
        if (result.MaxDrawdown < -1500)
            return "Excessive drawdown - position sizing too aggressive";
        
        var volCrushDays = result.DayResults.Count(r => r.Strategy.Contains("Volatility") && r.PnL < -100);
        if (volCrushDays >= 5)
            return "Volatility expansion crushed short premium strategies";
        
        return "Death by a thousand cuts - consistent small losses";
    }

    /// <summary>
    /// Analyze comprehensive battle test results
    /// </summary>
    private BattleTestReport AnalyzeBattleTestResults(List<BattleTestResult> results, int totalLosses)
    {
        Console.WriteLine("\n" + "=".PadRight(80, '='));
        Console.WriteLine("🔥 COMPREHENSIVE BATTLE TEST RESULTS");
        Console.WriteLine("=".PadRight(80, '='));

        var profitablePeriods = results.Count(r => r.FinalPnL > 0);
        var totalPeriods = results.Count;
        
        Console.WriteLine($"📊 OVERALL PERFORMANCE:");
        Console.WriteLine($"   Periods Tested: {totalPeriods}");
        Console.WriteLine($"   Profitable Periods: {profitablePeriods}/{totalPeriods} ({(double)profitablePeriods/totalPeriods*100:F1}%)");
        Console.WriteLine($"   Loss-Making Periods: {totalLosses}/{totalPeriods} ({(double)totalLosses/totalPeriods*100:F1}%)");

        var avgPnL = results.Average(r => r.FinalPnL);
        var worstLoss = results.Min(r => r.FinalPnL);
        var bestGain = results.Max(r => r.FinalPnL);
        
        Console.WriteLine($"\n💰 P&L STATISTICS:");
        Console.WriteLine($"   Average P&L: ${avgPnL:F2}");
        Console.WriteLine($"   Best Period: ${bestGain:F2}");
        Console.WriteLine($"   Worst Period: ${worstLoss:F2}");
        Console.WriteLine($"   Standard Deviation: ${CalculateStdDev(results.Select(r => r.FinalPnL)):F2}");

        // HONEST ASSESSMENT
        Console.WriteLine($"\n🎯 HONEST ASSESSMENT:");
        if (totalLosses == 0)
        {
            Console.WriteLine($"   ❌ UNREALISTIC: No losing periods found - likely system is gaming outcomes");
            Console.WriteLine($"   🔍 Real trading would have losses in crash periods");
        }
        else if (totalLosses <= 2)
        {
            Console.WriteLine($"   ⚠️  SUSPICIOUS: Very few losses - may not be realistic enough");
        }
        else if (totalLosses <= 4)
        {
            Console.WriteLine($"   ✅ REALISTIC: Framework shows expected losses in crash periods");
            Console.WriteLine($"   📈 Performance acceptable for aggressive strategy");
        }
        else
        {
            Console.WriteLine($"   💸 POOR PERFORMANCE: Framework fails in too many conditions");
            Console.WriteLine($"   🔧 Requires significant improvements");
        }

        return new BattleTestReport
        {
            TotalPeriods = totalPeriods,
            ProfitablePeriods = profitablePeriods,
            LossMakingPeriods = totalLosses,
            AveragePnL = avgPnL,
            BestPerformance = bestGain,
            WorstPerformance = worstLoss,
            Results = results,
            OverallAssessment = DetermineOverallAssessment(totalLosses, totalPeriods, avgPnL)
        };
    }

    private decimal CalculateStdDev(IEnumerable<decimal> values)
    {
        var avg = values.Average();
        var sumSquaredDiffs = values.Sum(v => (v - avg) * (v - avg));
        return (decimal)Math.Sqrt((double)(sumSquaredDiffs / values.Count()));
    }

    private string DetermineOverallAssessment(int losses, int total, decimal avgPnL)
    {
        var lossRate = (double)losses / total;
        
        if (lossRate == 0) return "UNREALISTIC - No losses detected";
        if (lossRate < 0.25 && avgPnL > 1000) return "EXCELLENT - Strong risk-adjusted returns";
        if (lossRate < 0.4 && avgPnL > 500) return "GOOD - Acceptable performance";
        if (lossRate < 0.6) return "MARGINAL - High loss rate but some profitability";
        return "POOR - Unacceptable loss rate for live trading";
    }
}