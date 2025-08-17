using System;
using System.Threading.Tasks;

namespace ODTE.Optimization.AdvancedGeneticOptimizer
{
    class PM414_Runner
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("🚀 PM414 GENETIC EVOLUTION SYSTEM");
            Console.WriteLine("Targeting 29%+ CAGR with Multi-Asset Intelligence");
            Console.WriteLine("==============================================");
            Console.WriteLine();

            // STEP 1: MANDATORY REAL DATA VALIDATION - ZERO TOLERANCE FOR FAKE DATA
            var databasePath = @"C:\code\ODTE\ODTE.Historical\ODTE_TimeSeries_5Y.db";
            var validator = new PM414_RealDataValidation(databasePath);
            
            Console.WriteLine("🔍 RUNNING MANDATORY REAL DATA VALIDATION...");
            var validationPassed = await validator.ValidateAllRealDataSources();
            
            if (!validationPassed)
            {
                Console.WriteLine("❌ VALIDATION FAILED - PM414 CANNOT RUN WITH SYNTHETIC DATA");
                Console.WriteLine("🛑 SYSTEM HALTED - FIX ALL DATA VALIDATION ISSUES FIRST");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }
            
            Console.WriteLine("✅ ALL VALIDATIONS PASSED - PROCEEDING WITH 100% REAL DATA");
            Console.WriteLine();

            var optimizer = new PM414_GeneticEvolution_MultiAsset();
            
            try
            {
                var results = await optimizer.RunEvolutionOptimization();
                
                Console.WriteLine();
                Console.WriteLine("🏆 TOP 10 EVOLVED STRATEGIES:");
                Console.WriteLine("============================");
                
                for (int i = 0; i < Math.Min(10, results.Count); i++)
                {
                    var strategy = results[i];
                    Console.WriteLine($"#{i+1}: {strategy.Id}");
                    Console.WriteLine($"   CAGR: {strategy.CAGR:F2}% | Sharpe: {strategy.SharpeRatio:F2}");
                    Console.WriteLine($"   Max DD: {strategy.MaxDrawdown:F2}% | Win Rate: {strategy.WinRate:F2}%");
                    Console.WriteLine($"   Trades: {strategy.TotalTrades:N0} | Fitness: {strategy.FitnessScore:F3}");
                    Console.WriteLine($"   Config: {strategy.GetParameterSummary()}");
                    Console.WriteLine();
                }
                
                Console.WriteLine("🎯 Evolution complete. PM414 strategies ready for validation.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}