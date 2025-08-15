using System;
using System.Collections.Generic;
using System.Linq;
using ODTE.Strategy;
using ODTE.Strategy.GoScore;
using Xunit;
using FluentAssertions;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// Intraday GoScore Optimization: Head-to-head with baseline on 64 random trading days
    /// Tests trade opportunities every 10-30 minutes with realistic intraday market conditions
    /// Last trade must be at least 1 hour before market close (3:00 PM ET)
    /// 
    /// OPTIMIZATION STRATEGY:
    /// 1. Generate 64 random trading days from 2023
    /// 2. Create intraday opportunities every 10-30 minutes (9:40 AM - 3:00 PM)
    /// 3. Run baseline vs GoScore head-to-head on identical opportunities
    /// 4. Optimize GoScore parameters based on actual performance gaps
    /// 5. Test parameter sensitivity and regime adaptation
    /// </summary>
    public class IntradayGoScoreOptimization
    {
        private readonly GoPolicy _initialPolicy;
        private readonly GoScorer _goScorer;
        private readonly Random _random = new Random(12345); // Fixed seed for reproducibility

        public IntradayGoScoreOptimization()
        {
            _initialPolicy = GoPolicy.Load(@"C:\code\ODTE\ODTE.GoScoreTests\GoScore.policy.json");
            _goScorer = new GoScorer(_initialPolicy);
        }

        [Fact]
        public void OptimizeGoScore_IntradayHeadToHead_64RandomDays()
        {
            Console.WriteLine("===============================================================================");
            Console.WriteLine("INTRADAY GOSCORE OPTIMIZATION: HEAD-TO-HEAD VS BASELINE");
            Console.WriteLine("===============================================================================");
            Console.WriteLine("Testing 64 random trading days with opportunities every 10-30 minutes");
            Console.WriteLine("Last trade opportunity: 1 hour before close (3:00 PM ET)");
            Console.WriteLine();

            // Generate 64 random trading days from 2023
            var tradingDays = GenerateRandomTradingDays(64);
            Console.WriteLine($"📅 Selected {tradingDays.Count} random trading days from 2023");
            Console.WriteLine($"    First day: {tradingDays.Min():yyyy-MM-dd}");
            Console.WriteLine($"    Last day: {tradingDays.Max():yyyy-MM-dd}");
            Console.WriteLine();

            // Run baseline vs GoScore comparison
            var baselineResults = new List<IntradayTradeResult>();
            var goScoreResults = new List<IntradayTradeResult>();
            
            var totalOpportunities = 0;

            foreach (var tradingDay in tradingDays)
            {
                Console.WriteLine($"🔄 Processing {tradingDay:yyyy-MM-dd}...");
                
                // Generate intraday opportunities (every 10-30 minutes from 9:40 AM to 3:00 PM)
                var opportunities = GenerateIntradayOpportunities(tradingDay);
                totalOpportunities += opportunities.Count;

                foreach (var opportunity in opportunities)
                {
                    // Run baseline strategy
                    var baselineResult = ExecuteBaselineStrategy(opportunity);
                    baselineResults.Add(baselineResult);

                    // Run GoScore strategy on identical opportunity
                    var goScoreResult = ExecuteGoScoreStrategy(opportunity);
                    goScoreResults.Add(goScoreResult);
                }
            }

            Console.WriteLine($"📊 Processed {totalOpportunities} total intraday opportunities");
            Console.WriteLine();

            // Analyze results and optimize parameters
            AnalyzeAndOptimizeParameters(baselineResults, goScoreResults, tradingDays);
        }

        private List<DateTime> GenerateRandomTradingDays(int count)
        {
            var allTradingDays = new List<DateTime>();
            var start = new DateTime(2023, 1, 1);
            var end = new DateTime(2023, 12, 31);
            var current = start;

            // Generate all trading days in 2023
            while (current <= end)
            {
                if (current.DayOfWeek != DayOfWeek.Saturday && current.DayOfWeek != DayOfWeek.Sunday)
                {
                    allTradingDays.Add(current);
                }
                current = current.AddDays(1);
            }

            // Select 64 random days
            var selectedDays = new List<DateTime>();
            var availableDays = new List<DateTime>(allTradingDays);

            for (int i = 0; i < count && availableDays.Count > 0; i++)
            {
                var randomIndex = _random.Next(availableDays.Count);
                selectedDays.Add(availableDays[randomIndex]);
                availableDays.RemoveAt(randomIndex);
            }

            return selectedDays.OrderBy(d => d).ToList();
        }

        private List<IntradayOpportunity> GenerateIntradayOpportunities(DateTime tradingDay)
        {
            var opportunities = new List<IntradayOpportunity>();
            
            // Market hours: 9:30 AM - 4:00 PM ET
            // First opportunity: 9:40 AM (10 minutes after open)
            // Last opportunity: 3:00 PM (1 hour before close)
            var startTime = tradingDay.Date.AddHours(9).AddMinutes(40); // 9:40 AM
            var endTime = tradingDay.Date.AddHours(15); // 3:00 PM
            
            var currentTime = startTime;
            var opportunityId = 1;

            while (currentTime <= endTime)
            {
                // Generate market conditions for this time
                var conditions = GenerateIntradayMarketConditions(tradingDay, currentTime);
                
                opportunities.Add(new IntradayOpportunity
                {
                    Id = opportunityId++,
                    TradingDay = tradingDay,
                    OpportunityTime = currentTime,
                    MarketConditions = conditions,
                    TimeToClose = endTime.AddHours(1) - currentTime // Time until 4:00 PM close
                });

                // Next opportunity: 10-30 minutes later (random within range)
                var minutesToNext = _random.Next(10, 31);
                currentTime = currentTime.AddMinutes(minutesToNext);
            }

            return opportunities;
        }

        private IntradayMarketConditions GenerateIntradayMarketConditions(DateTime tradingDay, DateTime currentTime)
        {
            var dayOfYear = tradingDay.DayOfYear;
            var hourFactor = (currentTime.Hour - 9) + (currentTime.Minute / 60.0); // Hours since 9 AM
            
            // Base VIX with intraday pattern (higher at open/close, lower mid-day)
            var baseVix = 15 + Math.Sin(dayOfYear * 0.1) * 10 + _random.NextDouble() * 15;
            var intradayVixMultiplier = 1.0 + 0.3 * Math.Sin(hourFactor * Math.PI / 6.5); // U-shape pattern
            var vix = baseVix * intradayVixMultiplier;

            // Volume profile (higher at open/close)
            var volumeProfile = 1.0 + 0.5 * (Math.Exp(-Math.Pow(hourFactor - 0.5, 2) / 2) + 
                                            Math.Exp(-Math.Pow(hourFactor - 6, 2) / 2));

            // Trend persistence (stronger in middle of day)
            var trendPersistence = Math.Min(1.0, hourFactor / 3.0 * (6.5 - hourFactor) / 3.5);

            return new IntradayMarketConditions
            {
                DateTime = currentTime,
                VIX = Math.Max(8, Math.Min(80, vix)),
                IVRank = Math.Max(0, Math.Min(1, (_random.NextDouble() + Math.Sin(dayOfYear * 0.05)) / 2)),
                TrendScore = Math.Sin(dayOfYear * 0.02 + hourFactor * 0.5) * 0.8 * trendPersistence,
                VolumeProfile = volumeProfile,
                SpreadQuality = Math.Max(0.3, 1.0 - (vix - 15) / 50.0), // Spreads widen with VIX
                OpeningRange = hourFactor < 1.0, // First hour after open
                ClosingHour = hourFactor > 5.5,  // Last 1.5 hours
                RealizedVolatility = vix * (0.8 + _random.NextDouble() * 0.4) * intradayVixMultiplier,
                ImpliedVolatility = vix,
                TermSlope = 0.9 + _random.NextDouble() * 0.3,
                DeltaGamma = _random.NextDouble() * 0.1 + 0.05, // Higher gamma risk intraday
                PinRisk = _random.NextDouble() * (hourFactor > 5.0 ? 0.8 : 0.3) // Higher pin risk near close
            };
        }

        private IntradayTradeResult ExecuteBaselineStrategy(IntradayOpportunity opportunity)
        {
            var conditions = opportunity.MarketConditions;
            
            // Baseline: 91% win rate with slight intraday adjustments
            var baseWinRate = 0.91;
            
            // Adjust for intraday patterns
            if (conditions.OpeningRange) baseWinRate -= 0.05; // Riskier in opening hour
            if (conditions.ClosingHour) baseWinRate -= 0.08;  // Riskier near close
            if (conditions.VIX > 30) baseWinRate -= 0.10;     // High vol adjustment
            if (conditions.VIX < 15) baseWinRate += 0.02;     // Low vol bonus
            
            baseWinRate = Math.Max(0.5, Math.Min(0.95, baseWinRate)); // Clamp to realistic range

            var isWin = _random.NextDouble() < baseWinRate;
            var positionSize = 1.0; // Full position always

            double pnl;
            if (isWin)
            {
                // Intraday wins: smaller but more frequent
                pnl = 15 + _random.NextDouble() * 25; // $15-40 profit
            }
            else
            {
                // Intraday losses: smaller max loss due to time decay
                var timeDecayFactor = Math.Min(1.0, opportunity.TimeToClose.TotalHours / 6.5);
                pnl = -(60 + _random.NextDouble() * 120) * timeDecayFactor; // $60-180 loss, scaled by time
            }

            return new IntradayTradeResult
            {
                Opportunity = opportunity,
                Strategy = "Baseline",
                Decision = IntradayDecision.Trade,
                PositionSize = positionSize,
                PnL = pnl,
                WasWin = isWin,
                ActualWinRate = baseWinRate
            };
        }

        private IntradayTradeResult ExecuteGoScoreStrategy(IntradayOpportunity opportunity)
        {
            var conditions = opportunity.MarketConditions;
            
            // Determine regime and strategy
            var regime = ClassifyIntradayRegime(conditions);
            var strategy = GetStrategyForRegime(regime);
            
            // Calculate GoScore inputs with intraday adjustments
            var goInputs = CalculateIntradayGoScoreInputs(conditions, strategy, opportunity.TimeToClose);
            
            // Get GoScore decision
            var breakdown = _goScorer.GetBreakdown(goInputs, strategy.Type, MapToGoScoreRegime(regime));
            
            double pnl = 0;
            bool wasWin = false;
            double positionSize = 0;

            // Execute based on GoScore decision
            switch (breakdown.Decision)
            {
                case Decision.Full:
                    positionSize = 1.0;
                    var fullResult = SimulateIntradayTrade(conditions, opportunity.TimeToClose, 1.0);
                    pnl = fullResult.PnL;
                    wasWin = fullResult.WasWin;
                    break;
                    
                case Decision.Half:
                    positionSize = 0.5;
                    var halfResult = SimulateIntradayTrade(conditions, opportunity.TimeToClose, 0.5);
                    pnl = halfResult.PnL;
                    wasWin = halfResult.WasWin;
                    break;
                    
                case Decision.Skip:
                    positionSize = 0;
                    pnl = 0;
                    wasWin = false; // No trade = no win
                    break;
            }

            return new IntradayTradeResult
            {
                Opportunity = opportunity,
                Strategy = "GoScore",
                Decision = MapToIntradayDecision(breakdown.Decision),
                PositionSize = positionSize,
                PnL = pnl,
                WasWin = wasWin,
                GoScore = breakdown.FinalScore,
                GoScoreBreakdown = breakdown
            };
        }

        private IntradayDecision MapToIntradayDecision(Decision goDecision)
        {
            return goDecision switch
            {
                Decision.Full => IntradayDecision.Trade,
                Decision.Half => IntradayDecision.HalfSize,
                Decision.Skip => IntradayDecision.Skip,
                _ => IntradayDecision.Skip
            };
        }

        private (double PnL, bool WasWin) SimulateIntradayTrade(IntradayMarketConditions conditions, TimeSpan timeToClose, double positionSize)
        {
            // Intraday win rate model with market condition adjustments
            var baseWinRate = 0.70; // Lower than daily baseline due to intraday noise
            
            // Market condition adjustments
            if (conditions.VIX > 30) baseWinRate -= 0.15;
            if (conditions.VIX < 15) baseWinRate += 0.05;
            if (Math.Abs(conditions.TrendScore) > 0.5) baseWinRate -= 0.10;
            if (conditions.IVRank > 0.7) baseWinRate += 0.08;
            if (conditions.OpeningRange) baseWinRate -= 0.05;
            if (conditions.ClosingHour) baseWinRate -= 0.12;
            if (conditions.SpreadQuality < 0.5) baseWinRate -= 0.08;
            if (conditions.PinRisk > 0.5) baseWinRate -= 0.06;

            baseWinRate = Math.Max(0.3, Math.Min(0.9, baseWinRate));

            var isWin = _random.NextDouble() < baseWinRate;

            double basePnL;
            if (isWin)
            {
                // Intraday profit: Time decay benefit
                var timeDecayBonus = 1.0 + (6.5 - timeToClose.TotalHours) / 10.0;
                basePnL = (12 + _random.NextDouble() * 23) * timeDecayBonus; // $12-35, boosted by time decay
            }
            else
            {
                // Intraday loss: Less severe due to time constraints
                var timeDecayProtection = Math.Min(1.0, timeToClose.TotalHours / 6.5);
                basePnL = -(50 + _random.NextDouble() * 100) * timeDecayProtection; // $50-150 loss, protected by time
            }

            return (basePnL * positionSize, isWin);
        }

        private string ClassifyIntradayRegime(IntradayMarketConditions conditions)
        {
            if (conditions.VIX > 35 || Math.Abs(conditions.TrendScore) > 0.7 || conditions.PinRisk > 0.6)
                return "Convex";
            else if (conditions.VIX > 22 || conditions.OpeningRange || conditions.ClosingHour)
                return "Mixed";
            else
                return "Calm";
        }

        private StrategySpec GetStrategyForRegime(string regime)
        {
            return regime switch
            {
                "Calm" => new StrategySpec { Type = StrategyKind.CreditBwb, CreditTarget = 0.25 },
                "Mixed" => new StrategySpec { Type = StrategyKind.CreditBwb, CreditTarget = 0.30 },
                "Convex" => new StrategySpec { Type = StrategyKind.IronCondor, CreditTarget = 0.20 },
                _ => new StrategySpec { Type = StrategyKind.CreditBwb, CreditTarget = 0.25 }
            };
        }

        private GoInputs CalculateIntradayGoScoreInputs(IntradayMarketConditions conditions, StrategySpec strategy, TimeSpan timeToClose)
        {
            // Time decay factor: More aggressive as we get closer to close
            var timeDecayFactor = Math.Min(1.0, (6.5 - timeToClose.TotalHours) / 6.5);
            
            // Base PoE with time decay boost
            var basePoE = 0.60 + conditions.IVRank * 0.25 - Math.Abs(conditions.TrendScore) * 0.20;
            var poE = Math.Max(0.1, Math.Min(0.95, basePoE + timeDecayFactor * 0.15));

            // PoT increases with time to expiry and volatility
            var poT = Math.Min(0.8, conditions.VIX / 80.0 + Math.Abs(conditions.TrendScore) * 0.4 + 
                     (timeToClose.TotalHours / 6.5) * 0.2);

            // Edge calculation with spread impact
            var edge = (conditions.IVRank - 0.5) * 0.25 + (strategy.CreditTarget - 0.2) * 0.6 - 
                      (1.0 - conditions.SpreadQuality) * 0.15;

            // Liquidity score from spread quality and volume
            var liqScore = Math.Min(1.0, conditions.SpreadQuality * conditions.VolumeProfile / 1.5);

            // Regime score - penalize difficult intraday periods
            var regScore = 0.8;
            if (conditions.OpeningRange) regScore -= 0.2;
            if (conditions.ClosingHour) regScore -= 0.3;
            if (conditions.VIX > 30) regScore -= 0.2;

            // Pin score - higher risk near close
            var pinScore = conditions.PinRisk < 0.3 ? 0.8 : 
                          conditions.PinRisk < 0.6 ? 0.6 : 0.3;

            // RFib utilization - assume moderate usage
            var rfibUtil = strategy.CreditTarget * 0.7;

            return new GoInputs(
                PoE: poE,
                PoT: poT,
                Edge: edge,
                LiqScore: liqScore,
                RegScore: regScore,
                PinScore: pinScore,
                RfibUtil: rfibUtil
            );
        }

        private ODTE.Strategy.GoScore.Regime MapToGoScoreRegime(string regime)
        {
            return regime switch
            {
                "Calm" => ODTE.Strategy.GoScore.Regime.Calm,
                "Mixed" => ODTE.Strategy.GoScore.Regime.Mixed,
                "Convex" => ODTE.Strategy.GoScore.Regime.Convex,
                _ => ODTE.Strategy.GoScore.Regime.Calm
            };
        }

        private void AnalyzeAndOptimizeParameters(List<IntradayTradeResult> baselineResults, 
                                                List<IntradayTradeResult> goScoreResults, 
                                                List<DateTime> tradingDays)
        {
            Console.WriteLine("🎯 INTRADAY PERFORMANCE ANALYSIS");
            Console.WriteLine("═════════════════════════════════");
            Console.WriteLine();

            // Calculate baseline performance
            var baselinePnL = baselineResults.Sum(r => r.PnL);
            var baselineWins = baselineResults.Count(r => r.WasWin);
            var baselineWinRate = (double)baselineWins / baselineResults.Count;
            var baselineTrades = baselineResults.Count;
            var baselineDrawdown = CalculateMaxDrawdown(baselineResults.Select(r => r.PnL).ToList());

            Console.WriteLine($"📊 BASELINE PERFORMANCE (Trade Every Opportunity):");
            Console.WriteLine($"   Total P&L: ${baselinePnL:F0}");
            Console.WriteLine($"   Win Rate: {baselineWinRate:P1}");
            Console.WriteLine($"   Total Trades: {baselineTrades}");
            Console.WriteLine($"   Max Drawdown: ${baselineDrawdown:F0}");
            Console.WriteLine($"   Avg P&L per Trade: ${baselinePnL / baselineTrades:F1}");
            Console.WriteLine();

            // Calculate GoScore performance
            var goScorePnL = goScoreResults.Sum(r => r.PnL);
            var goScoreWins = goScoreResults.Count(r => r.WasWin);
            var goScoreExecutedTrades = goScoreResults.Count(r => r.Decision != IntradayDecision.Skip);
            var goScoreWinRate = goScoreExecutedTrades > 0 ? (double)goScoreWins / goScoreExecutedTrades : 0;
            var goScoreSkipped = goScoreResults.Count(r => r.Decision == IntradayDecision.Skip);
            var goScoreSelectivity = (double)goScoreSkipped / goScoreResults.Count;
            var goScoreDrawdown = CalculateMaxDrawdown(goScoreResults.Select(r => r.PnL).ToList());

            Console.WriteLine($"🧠 GOSCORE PERFORMANCE (Intelligent Selection):");
            Console.WriteLine($"   Total P&L: ${goScorePnL:F0}");
            Console.WriteLine($"   Win Rate: {goScoreWinRate:P1} (of executed trades)");
            Console.WriteLine($"   Executed Trades: {goScoreExecutedTrades}");
            Console.WriteLine($"   Skipped Opportunities: {goScoreSkipped} ({goScoreSelectivity:P1} selectivity)");
            Console.WriteLine($"   Max Drawdown: ${goScoreDrawdown:F0}");
            if (goScoreExecutedTrades > 0)
                Console.WriteLine($"   Avg P&L per Trade: ${goScorePnL / goScoreExecutedTrades:F1}");
            Console.WriteLine();

            // Performance gap analysis
            var pnlGap = goScorePnL - baselinePnL;
            var drawdownImprovement = Math.Abs(goScoreDrawdown) - Math.Abs(baselineDrawdown);

            Console.WriteLine($"⚔️ HEAD-TO-HEAD COMPARISON:");
            Console.WriteLine($"   P&L Difference: ${pnlGap:+0;-0;0} ({(pnlGap / Math.Abs(baselinePnL) * 100):+0.0;-0.0;0.0}%)");
            Console.WriteLine($"   Drawdown Change: ${-drawdownImprovement:+0;-0;0} ({(-drawdownImprovement / Math.Abs(baselineDrawdown) * 100):+0.0;-0.0;0.0}%)");
            Console.WriteLine($"   Win Rate Gap: {(goScoreWinRate - baselineWinRate) * 100:+0.0;-0.0;0.0}%");
            Console.WriteLine($"   Trade Reduction: {baselineTrades - goScoreExecutedTrades} trades ({goScoreSelectivity:P1} skipped)");
            Console.WriteLine();

            // Analyze GoScore decision quality
            AnalyzeGoScoreDecisionQuality(baselineResults, goScoreResults);
            
            // Optimize parameters based on findings
            OptimizeGoScoreParameters(baselineResults, goScoreResults);
        }

        private void AnalyzeGoScoreDecisionQuality(List<IntradayTradeResult> baselineResults, 
                                                  List<IntradayTradeResult> goScoreResults)
        {
            Console.WriteLine("🔍 GOSCORE DECISION QUALITY ANALYSIS:");
            Console.WriteLine("═══════════════════════════════════");

            var decisions = baselineResults.Zip(goScoreResults, (baseline, goScore) => new
            {
                Baseline = baseline,
                GoScore = goScore,
                OpportunityProfit = baseline.PnL, // What we would have made
                ActualResult = goScore.PnL,       // What we actually made
                GoScoreValue = goScore.GoScore ?? 0,
                WasMissedProfit = goScore.Decision == IntradayDecision.Skip && baseline.WasWin,
                WasAvoidedLoss = goScore.Decision == IntradayDecision.Skip && !baseline.WasWin,
                WasBadExecute = goScore.Decision != IntradayDecision.Skip && !baseline.WasWin
            }).ToList();

            var missedProfits = decisions.Where(d => d.WasMissedProfit).ToList();
            var avoidedLosses = decisions.Where(d => d.WasAvoidedLoss).ToList();
            var badExecutes = decisions.Where(d => d.WasBadExecute).ToList();

            Console.WriteLine($"   ❌ MISSED PROFITS: {missedProfits.Count}");
            Console.WriteLine($"      Lost opportunity: ${missedProfits.Sum(d => d.OpportunityProfit):F0}");
            if (missedProfits.Any())
                Console.WriteLine($"      Avg GoScore: {missedProfits.Average(d => d.GoScoreValue):F1}");

            Console.WriteLine($"   ✅ AVOIDED LOSSES: {avoidedLosses.Count}");
            Console.WriteLine($"      Losses avoided: ${Math.Abs(avoidedLosses.Sum(d => d.OpportunityProfit)):F0}");
            if (avoidedLosses.Any())
                Console.WriteLine($"      Avg GoScore: {avoidedLosses.Average(d => d.GoScoreValue):F1}");

            Console.WriteLine($"   💣 BAD EXECUTIONS: {badExecutes.Count}");
            Console.WriteLine($"      Should have skipped: ${Math.Abs(badExecutes.Sum(d => d.ActualResult)):F0}");
            if (badExecutes.Any())
                Console.WriteLine($"      Avg GoScore: {badExecutes.Average(d => d.GoScoreValue):F1}");

            var netBenefit = avoidedLosses.Sum(d => Math.Abs(d.OpportunityProfit)) - missedProfits.Sum(d => d.OpportunityProfit);
            Console.WriteLine($"   🎯 NET BENEFIT: ${netBenefit:+0;-0;0}");
            Console.WriteLine();
        }

        private void OptimizeGoScoreParameters(List<IntradayTradeResult> baselineResults, 
                                              List<IntradayTradeResult> goScoreResults)
        {
            Console.WriteLine("⚙️ GOSCORE PARAMETER OPTIMIZATION RECOMMENDATIONS:");
            Console.WriteLine("═════════════════════════════════════════════════");

            var decisions = baselineResults.Zip(goScoreResults, (baseline, goScore) => new
            {
                Baseline = baseline,
                GoScore = goScore,
                OpportunityProfit = baseline.PnL,
                GoScoreValue = goScore.GoScore ?? 0
            }).ToList();

            // Analyze threshold optimization
            var profitableOpportunities = decisions.Where(d => d.OpportunityProfit > 0).ToList();
            var unprofitableOpportunities = decisions.Where(d => d.OpportunityProfit <= 0).ToList();

            if (profitableOpportunities.Any() && unprofitableOpportunities.Any())
            {
                var avgScoreProfitable = profitableOpportunities.Average(d => d.GoScoreValue);
                var avgScoreUnprofitable = unprofitableOpportunities.Average(d => d.GoScoreValue);
                
                Console.WriteLine($"📊 SCORE ANALYSIS:");
                Console.WriteLine($"   Profitable opportunities avg score: {avgScoreProfitable:F1}");
                Console.WriteLine($"   Unprofitable opportunities avg score: {avgScoreUnprofitable:F1}");
                Console.WriteLine($"   Score separation: {avgScoreProfitable - avgScoreUnprofitable:F1}");
                
                // Recommend optimal threshold
                var optimalThreshold = (avgScoreProfitable + avgScoreUnprofitable) / 2;
                Console.WriteLine($"   💡 RECOMMENDED THRESHOLD: {optimalThreshold:F0} (currently using 52)");
            }

            // Analyze selectivity optimization
            var currentSelectivity = (double)goScoreResults.Count(r => r.Decision == IntradayDecision.Skip) / goScoreResults.Count;
            var targetSelectivity = 0.25; // Target 25% selectivity for intraday

            Console.WriteLine();
            Console.WriteLine($"🎯 SELECTIVITY OPTIMIZATION:");
            Console.WriteLine($"   Current selectivity: {currentSelectivity:P1}");
            Console.WriteLine($"   Target selectivity: {targetSelectivity:P1}");
            
            if (currentSelectivity < targetSelectivity)
            {
                Console.WriteLine($"   💡 RECOMMENDATION: Increase thresholds to skip more trades");
                Console.WriteLine($"      Suggested Half threshold: {_initialPolicy.Thresholds.half + 5:F0}");
                Console.WriteLine($"      Suggested Full threshold: {_initialPolicy.Thresholds.full + 5:F0}");
            }
            else if (currentSelectivity > targetSelectivity)
            {
                Console.WriteLine($"   💡 RECOMMENDATION: Lower thresholds to capture more opportunities");
                Console.WriteLine($"      Suggested Half threshold: {_initialPolicy.Thresholds.half - 3:F0}");
                Console.WriteLine($"      Suggested Full threshold: {_initialPolicy.Thresholds.full - 3:F0}");
            }

            Console.WriteLine();
            Console.WriteLine("🔧 PARAMETER TUNING PRIORITIES:");
            Console.WriteLine("   1. Threshold optimization for intraday selectivity");
            Console.WriteLine("   2. Time decay weight adjustment (more aggressive near close)");
            Console.WriteLine("   3. Regime-specific threshold adaptation");
            Console.WriteLine("   4. Liquidity penalty calibration for intraday spreads");
            Console.WriteLine();
            Console.WriteLine("===============================================================================");
            Console.WriteLine("INTRADAY OPTIMIZATION COMPLETE - READY FOR PARAMETER IMPLEMENTATION");
            Console.WriteLine("===============================================================================");
        }

        private double CalculateMaxDrawdown(List<double> pnls)
        {
            if (!pnls.Any()) return 0;

            var peak = 0.0;
            var maxDrawdown = 0.0;
            var cumulative = 0.0;

            foreach (var pnl in pnls)
            {
                cumulative += pnl;
                peak = Math.Max(peak, cumulative);
                var drawdown = cumulative - peak;
                maxDrawdown = Math.Min(maxDrawdown, drawdown);
            }

            return maxDrawdown;
        }
    }

    // Supporting classes for intraday optimization
    public class IntradayOpportunity
    {
        public int Id { get; set; }
        public DateTime TradingDay { get; set; }
        public DateTime OpportunityTime { get; set; }
        public IntradayMarketConditions MarketConditions { get; set; } = new();
        public TimeSpan TimeToClose { get; set; }
    }

    public class IntradayMarketConditions
    {
        public DateTime DateTime { get; set; }
        public double VIX { get; set; }
        public double IVRank { get; set; }
        public double TrendScore { get; set; }
        public double VolumeProfile { get; set; }
        public double SpreadQuality { get; set; }
        public bool OpeningRange { get; set; }
        public bool ClosingHour { get; set; }
        public double RealizedVolatility { get; set; }
        public double ImpliedVolatility { get; set; }
        public double TermSlope { get; set; }
        public double DeltaGamma { get; set; }
        public double PinRisk { get; set; }
    }

    public class IntradayTradeResult
    {
        public IntradayOpportunity Opportunity { get; set; } = new();
        public string Strategy { get; set; } = "";
        public IntradayDecision Decision { get; set; }
        public double PositionSize { get; set; }
        public double PnL { get; set; }
        public bool WasWin { get; set; }
        public double? GoScore { get; set; }
        public GoScoreBreakdown? GoScoreBreakdown { get; set; }
        public double ActualWinRate { get; set; }
    }

    public enum IntradayDecision
    {
        Skip,
        HalfSize,
        Trade
    }
}