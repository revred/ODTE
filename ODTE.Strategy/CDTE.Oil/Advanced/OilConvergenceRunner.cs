using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ODTE.Strategy.CDTE.Oil.Convergence;

namespace ODTE.Oil.Convergence
{
    /// <summary>
    /// Runner for Oil CDTE Genetic Convergence
    /// Evolves top 16 mutations toward single optimal model
    /// Target: >80% win rate, <15% drawdown, maximized CAGR
    /// </summary>
    public class OilConvergenceRunner
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("🧬 OIL CDTE GENETIC CONVERGENCE ENGINE");
            Console.WriteLine("=====================================");
            Console.WriteLine("Evolving 64 mutations → Single optimal model");
            Console.WriteLine("Target: >80% win rate, <15% drawdown, max CAGR\n");

            // Setup logging
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            var logger = loggerFactory.CreateLogger<OilConvergenceRunner>();

            // Configure convergence parameters
            var config = new OilGeneticConvergence.ConvergenceConfig
            {
                PopulationSize = 100,
                EliteCount = 10,
                MutationRate = 0.15,
                CrossoverRate = 0.85,
                MaxGenerations = 500,
                TargetWinRate = 0.80,
                MaxAcceptableDrawdown = 0.15,
                ConvergenceThreshold = 0.001,
                StagnationLimit = 50
            };

            Console.WriteLine("Configuration:");
            Console.WriteLine($"  Population Size: {config.PopulationSize}");
            Console.WriteLine($"  Elite Count: {config.EliteCount}");
            Console.WriteLine($"  Mutation Rate: {config.MutationRate:P0}");
            Console.WriteLine($"  Crossover Rate: {config.CrossoverRate:P0}");
            Console.WriteLine($"  Max Generations: {config.MaxGenerations}");
            Console.WriteLine($"  Target Win Rate: {config.TargetWinRate:P0}");
            Console.WriteLine($"  Max Drawdown: {config.MaxAcceptableDrawdown:P0}");
            Console.WriteLine();

            // Run convergence
            var convergenceEngine = new OilGeneticConvergence();
            
            logger.LogInformation("Starting genetic convergence...");
            var startTime = DateTime.Now;
            
            var optimalModel = await convergenceEngine.ConvergeAsync(config);
            
            var endTime = DateTime.Now;
            var duration = endTime - startTime;

            // Display results
            Console.WriteLine("\n🎯 CONVERGENCE COMPLETE!");
            Console.WriteLine("========================");
            Console.WriteLine($"Duration: {duration.TotalMinutes:F1} minutes");
            Console.WriteLine($"Generation: {optimalModel.Generation}");
            Console.WriteLine();

            DisplayOptimalModel(optimalModel);
            
            // Validate against targets
            var validationResult = ValidateModel(optimalModel, config);
            DisplayValidation(validationResult);

            // Generate implementation blueprint
            GenerateImplementationBlueprint(optimalModel);

            Console.WriteLine("\n✅ Oil CDTE convergence complete!");
            Console.WriteLine("Next step: Implement OIL-OMEGA in paper trading");
        }

        private static void DisplayOptimalModel(OilGeneticConvergence.StrategyGenome model)
        {
            Console.WriteLine("🏆 OPTIMAL MODEL: OIL-OMEGA");
            Console.WriteLine("============================");
            
            Console.WriteLine("\n📊 PERFORMANCE METRICS:");
            Console.WriteLine($"  CAGR: {model.CAGR:F1}%");
            Console.WriteLine($"  Win Rate: {model.WinRate:P1}");
            Console.WriteLine($"  Max Drawdown: {model.MaxDrawdown:F1}%");
            Console.WriteLine($"  Sharpe Ratio: {model.SharpeRatio:F2}");
            Console.WriteLine($"  Profit Factor: {model.ProfitFactor:F2}");
            Console.WriteLine($"  Fitness Score: {model.Fitness:F2}");

            Console.WriteLine("\n⚙️ STRATEGY CONFIGURATION:");
            Console.WriteLine($"  Entry Day: {model.PrimaryEntryDay}");
            Console.WriteLine($"  Entry Time: {model.EntryTime}");
            Console.WriteLine($"  Exit Day: {model.PrimaryExitDay}");
            Console.WriteLine($"  Exit Time: {model.ExitTime}");
            
            Console.WriteLine("\n🎯 STRIKE SELECTION:");
            Console.WriteLine($"  Method: {model.StrikeMethod}");
            Console.WriteLine($"  Base Short Delta: {model.BaseShortDelta:F3}");
            Console.WriteLine($"  Spread Width: {model.SpreadWidth:F1}");
            if (model.StrikeMethod == "IVRank")
            {
                Console.WriteLine($"  High IV Delta: {model.HighIVDelta:F3}");
                Console.WriteLine($"  Low IV Delta: {model.LowIVDelta:F3}");
                Console.WriteLine($"  IV High Threshold: {model.IVHighThreshold:F0}");
            }
            
            Console.WriteLine("\n🛡️ RISK MANAGEMENT:");
            Console.WriteLine($"  Stop Loss: {model.StopLossPercent:F0}%");
            Console.WriteLine($"  Profit Target 1: {model.ProfitTarget1:F0}% ({model.ProfitTarget1Size:F0}% size)");
            Console.WriteLine($"  Profit Target 2: {model.ProfitTarget2:F0}%");
            if (model.UseTrailingStop)
            {
                Console.WriteLine($"  Trailing Stop: Activated at {model.TrailingStopActivation:F0}%");
            }
            if (model.DeltaRollTrigger > 0)
            {
                Console.WriteLine($"  Delta Roll Trigger: {model.DeltaRollTrigger:F3}");
                Console.WriteLine($"  Max Rolls/Week: {model.MaxRollsPerWeek}");
            }
            
            Console.WriteLine("\n💰 POSITION SIZING:");
            Console.WriteLine($"  Base Risk: {model.BaseRiskPercent:F1}%");
            if (model.HighIVSizeReduction > 0)
            {
                Console.WriteLine($"  High IV Reduction: {model.HighIVSizeReduction:F0}%");
            }
            if (model.ConsecutiveLossReduction > 0)
            {
                Console.WriteLine($"  Loss Streak Reduction: {model.ConsecutiveLossReduction:F0}%");
            }
            
            Console.WriteLine("\n🔧 ADVANCED FEATURES:");
            if (model.UseEIASignal)
                Console.WriteLine("  ✓ EIA Signal Awareness");
            if (model.UseAPISignal)
                Console.WriteLine("  ✓ API Signal Awareness");
            if (model.UsePinRiskExit)
                Console.WriteLine($"  ✓ Pin Risk Exit (Buffer: {model.PinRiskBuffer:F1})");
            if (model.UseTimeDecayOptimal)
                Console.WriteLine($"  ✓ Time Decay Optimization (Ratio: {model.ThetaGammaRatioTarget:F1})");
            if (model.UseContangoSignal)
                Console.WriteLine($"  ✓ Contango Signal (Threshold: {model.ContangoThreshold:F1})");
            if (model.UseSkewAdjustment)
                Console.WriteLine($"  ✓ Skew Adjustment (Multiplier: {model.SkewMultiplier:F1})");
        }

        private static ValidationResult ValidateModel(
            OilGeneticConvergence.StrategyGenome model, 
            OilGeneticConvergence.ConvergenceConfig config)
        {
            var result = new ValidationResult();
            
            // Check win rate constraint
            result.WinRateValid = model.WinRate >= config.TargetWinRate;
            result.WinRateScore = model.WinRate ?? 0;
            
            // Check drawdown constraint
            result.DrawdownValid = model.MaxDrawdown >= -config.MaxAcceptableDrawdown;
            result.DrawdownScore = model.MaxDrawdown ?? 0;
            
            // Check CAGR improvement
            result.CAGRValid = model.CAGR >= 35; // Minimum threshold
            result.CAGRScore = model.CAGR ?? 0;
            
            // Check other metrics
            result.SharpeValid = model.SharpeRatio >= 1.5;
            result.SharpeScore = model.SharpeRatio ?? 0;
            
            result.ProfitFactorValid = model.ProfitFactor >= 2.0;
            result.ProfitFactorScore = model.ProfitFactor ?? 0;
            
            // Overall validation
            result.OverallValid = result.WinRateValid && result.DrawdownValid && result.CAGRValid;
            
            return result;
        }

        private static void DisplayValidation(ValidationResult validation)
        {
            Console.WriteLine("\n🔍 VALIDATION RESULTS:");
            Console.WriteLine("=====================");
            
            Console.WriteLine($"Win Rate: {(validation.WinRateValid ? "✅" : "❌")} {validation.WinRateScore:P1} (Target: ≥80%)");
            Console.WriteLine($"Max Drawdown: {(validation.DrawdownValid ? "✅" : "❌")} {validation.DrawdownScore:F1}% (Target: ≥-15%)");
            Console.WriteLine($"CAGR: {(validation.CAGRValid ? "✅" : "❌")} {validation.CAGRScore:F1}% (Target: ≥35%)");
            Console.WriteLine($"Sharpe Ratio: {(validation.SharpeValid ? "✅" : "❌")} {validation.SharpeScore:F2} (Target: ≥1.5)");
            Console.WriteLine($"Profit Factor: {(validation.ProfitFactorValid ? "✅" : "❌")} {validation.ProfitFactorScore:F2} (Target: ≥2.0)");
            
            Console.WriteLine($"\nOVERALL: {(validation.OverallValid ? "✅ PASSED" : "❌ FAILED")}");
            
            if (!validation.OverallValid)
            {
                Console.WriteLine("\n⚠️ Model did not meet all constraints. Consider:");
                if (!validation.WinRateValid)
                    Console.WriteLine("  - Increase profit target frequency");
                if (!validation.DrawdownValid)
                    Console.WriteLine("  - Tighten stop losses");
                if (!validation.CAGRValid)
                    Console.WriteLine("  - Optimize position sizing");
            }
        }

        private static void GenerateImplementationBlueprint(OilGeneticConvergence.StrategyGenome model)
        {
            Console.WriteLine("\n📋 IMPLEMENTATION BLUEPRINT:");
            Console.WriteLine("============================");
            
            Console.WriteLine("\n1. TRADING SCHEDULE:");
            Console.WriteLine($"   Entry Window: {model.PrimaryEntryDay} {model.EntryTime} (5 minutes)");
            Console.WriteLine($"   Exit Window: {model.PrimaryExitDay} {model.ExitTime} (5 minutes)");
            Console.WriteLine("   Total Time Commitment: 10 minutes/week");
            
            Console.WriteLine("\n2. ENTRY CRITERIA:");
            Console.WriteLine($"   - Strike Method: {model.StrikeMethod}");
            Console.WriteLine($"   - Target Delta: {model.BaseShortDelta:F3}");
            Console.WriteLine($"   - Required Volume: 1000+ contracts");
            Console.WriteLine($"   - Max Spread: $0.10");
            if (model.UseEIASignal)
                Console.WriteLine("   - EIA Signal: Check for Wednesday inventory report");
            if (model.UseAPISignal)
                Console.WriteLine("   - API Signal: Monitor Tuesday evening data");
            
            Console.WriteLine("\n3. POSITION MANAGEMENT:");
            Console.WriteLine($"   - Initial Size: {model.BaseRiskPercent:F1}% portfolio risk");
            Console.WriteLine($"   - Stop Loss: {model.StopLossPercent:F0}% of credit");
            Console.WriteLine($"   - Profit Target 1: {model.ProfitTarget1:F0}% (close {model.ProfitTarget1Size:F0}%)");
            Console.WriteLine($"   - Profit Target 2: {model.ProfitTarget2:F0}% (close remainder)");
            
            Console.WriteLine("\n4. EXIT RULES:");
            Console.WriteLine($"   - Primary Exit: {model.PrimaryExitDay} {model.ExitTime}");
            Console.WriteLine("   - Emergency Exit: Friday 10:00 AM if still open");
            if (model.UsePinRiskExit)
                Console.WriteLine($"   - Pin Risk: Exit if within ${model.PinRiskBuffer:F1} of short strike Friday");
            
            Console.WriteLine("\n5. AUTOMATION REQUIREMENTS:");
            Console.WriteLine("   - Real-time options data feed");
            Console.WriteLine("   - Automated order entry/exit");
            Console.WriteLine("   - P&L tracking and alerts");
            Console.WriteLine("   - Risk monitoring dashboard");
            
            Console.WriteLine("\n6. PAPER TRADING CHECKLIST:");
            Console.WriteLine("   ☐ Setup data feed (OPRA or equivalent)");
            Console.WriteLine("   ☐ Configure trading bot with OIL-OMEGA parameters");
            Console.WriteLine("   ☐ Test entry/exit automation");
            Console.WriteLine("   ☐ Validate P&L calculations");
            Console.WriteLine("   ☐ Monitor for 4 weeks minimum");
            Console.WriteLine("   ☐ Compare actual vs. expected performance");
            
            Console.WriteLine("\n7. GO-LIVE CRITERIA:");
            Console.WriteLine("   ☐ 30+ paper trades completed");
            Console.WriteLine("   ☐ Win rate ≥75% in paper trading");
            Console.WriteLine("   ☐ Max drawdown <20% in paper trading");
            Console.WriteLine("   ☐ No system failures or missed signals");
            Console.WriteLine("   ☐ Risk management systems tested");
        }

        private class ValidationResult
        {
            public bool WinRateValid { get; set; }
            public double WinRateScore { get; set; }
            
            public bool DrawdownValid { get; set; }
            public double DrawdownScore { get; set; }
            
            public bool CAGRValid { get; set; }
            public double CAGRScore { get; set; }
            
            public bool SharpeValid { get; set; }
            public double SharpeScore { get; set; }
            
            public bool ProfitFactorValid { get; set; }
            public double ProfitFactorScore { get; set; }
            
            public bool OverallValid { get; set; }
        }
    }
}