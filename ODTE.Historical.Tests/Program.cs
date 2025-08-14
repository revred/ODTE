using System;
using System.Threading.Tasks;
using ODTE.Historical;
using ODTE.Historical.Validation;

namespace ODTE.Historical.Tests;

/// <summary>
/// ODTE Historical Data Testing and Validation Console
/// Usage: dotnet run [operation] [parameters...]
/// Operations: import, validate, benchmark, inspect, gaps, fill, update, backfill
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            Console.WriteLine("🧬 ODTE Historical Data Testing & Validation");
            Console.WriteLine("═════════════════════════════════════════════");
            Console.WriteLine("Testing and validation console for ODTE.Historical library...");
            Console.WriteLine();

            // Parse command line arguments
            string sourceDir = args.Length > 0 && !string.IsNullOrEmpty(args[0])
                ? args[0] 
                : @"C:\code\ODTE\Data\Historical\XSP";
            
            string outputFile = args.Length > 1 && !string.IsNullOrEmpty(args[1])
                ? args[1] 
                : @"C:\code\ODTE\Data\XSP_Master_5Y_Index.csv";

            Console.WriteLine($"📂 Source Directory: {sourceDir}");
            Console.WriteLine($"💾 Output Index: {outputFile}");
            Console.WriteLine();

            Console.WriteLine("📊 Select operation:");
            Console.WriteLine("   1. Full Import (Import all historical Parquet files)");
            Console.WriteLine("   2. Analyze Gaps (Check for missing trading days)");
            Console.WriteLine("   3. Fill Gaps (Add missing trading days with synthetic data)");
            Console.WriteLine("   4. Update Latest (Add recent trading days to database)");
            Console.WriteLine("   5. Backfill Range (Fill specific date range)");
            Console.WriteLine("   6. Validate Data (Run Stooq data quality validation)");
            Console.WriteLine("   7. Benchmark Synthetic (Test OptionsDataGenerator vs real data)");
            Console.WriteLine();
            
            // Parse operation type
            var operation = args.Length > 2 ? args[2].ToLower() : "1";
            if (args.Any(a => a.ToLower().Contains("gaps"))) operation = "gaps";
            if (args.Any(a => a.ToLower().Contains("fill"))) operation = "fill";
            if (args.Any(a => a.ToLower().Contains("update"))) operation = "update";
            if (args.Any(a => a.ToLower().Contains("backfill"))) operation = "backfill";
            if (args.Any(a => a.ToLower().Contains("validate"))) operation = "validate";
            if (args.Any(a => a.ToLower().Contains("benchmark"))) operation = "benchmark";
            if (args.Any(a => a.ToLower().Contains("inspect"))) operation = "inspect";
            
            if (operation == "1" || operation == "import" || operation == "sqlite")
            {
                // SQLite Time Series Database approach
                using var manager = new HistoricalDataManager();
                await manager.InitializeAsync();
                
                var importResult = await manager.ConsolidateFromParquetAsync(sourceDir);
                if (importResult.Success)
                {
                    var stats = await manager.GetStatsAsync();
                    
                    Console.WriteLine();
                    Console.WriteLine("🎯 SINGLE SOURCE OF TRUTH CREATED:");
                    Console.WriteLine($"   📄 File: ODTE_TimeSeries_5Y.db ({stats.DatabaseSizeMB:N1} MB)");
                    Console.WriteLine($"   📊 Records: {stats.TotalRecords:N0}");
                    Console.WriteLine($"   🗜️ Compression: {stats.CompressionRatio:N1}x");
                    Console.WriteLine($"   ⚡ Fast range queries with SQL indexing");
                    Console.WriteLine($"   📤 Export to CSV/JSON/Parquet on demand");
                    
                    // Demonstrate export capabilities
                    var exportDir = Path.Combine(Path.GetDirectoryName(outputFile) ?? "", "exports");
                    var batchResult = await manager.ExportCommonDatasetsAsync(exportDir);
                    
                    Console.WriteLine();
                    Console.WriteLine("🎯 NEXT STEPS:");
                    Console.WriteLine("   1. Single SQLite file contains entire 5-year dataset");
                    Console.WriteLine("   2. Fast SQL queries for any date range");
                    Console.WriteLine("   3. Export subsets on demand for specific analysis");
                    Console.WriteLine("   4. Optimized for genetic algorithm backtesting");
                    
                    return 0;
                }
                else
                {
                    Console.WriteLine($"❌ SQLite consolidation failed: {importResult.ErrorMessage}");
                    return 1;
                }
            }
            else if (operation == "2" || operation == "gaps")
            {
                // Gap analysis
                using var ingestionEngine = new DataIngestionEngine();
                await ingestionEngine.InitializeAsync();
                
                var gapAnalysis = await ingestionEngine.AnalyzeDataGapsAsync();
                Console.WriteLine();
                Console.WriteLine($"🎯 RECOMMENDATION: {gapAnalysis.Recommendation}");
                
                return gapAnalysis.TotalGaps > 0 ? 1 : 0;
            }
            else if (operation == "3" || operation == "fill")
            {
                // Fill gaps
                using var ingestionEngine = new DataIngestionEngine();
                await ingestionEngine.InitializeAsync();
                
                var fillResult = await ingestionEngine.FillDataGapsAsync();
                
                return fillResult.Success ? 0 : 1;
            }
            else if (operation == "4" || operation == "update")
            {
                // Update to latest
                using var ingestionEngine = new DataIngestionEngine();
                await ingestionEngine.InitializeAsync();
                
                var updateResult = await ingestionEngine.UpdateToLatestAsync();
                
                return updateResult.Success ? 0 : 1;
            }
            else if (operation == "5" || operation == "backfill")
            {
                // Backfill date range
                using var ingestionEngine = new DataIngestionEngine();
                await ingestionEngine.InitializeAsync();
                
                // Parse date range from arguments or use defaults
                var startDate = args.Length > 3 ? DateTime.Parse(args[3]) : DateTime.Now.AddYears(-1);
                var endDate = args.Length > 4 ? DateTime.Parse(args[4]) : DateTime.Now.Date;
                
                var backfillResult = await ingestionEngine.BackfillDateRangeAsync(startDate, endDate);
                
                return backfillResult.Success ? 0 : 1;
            }
            else if (operation == "6" || operation == "validate")
            {
                // Run Stooq data validation
                Console.WriteLine("🔍 Running Stooq data validation...");
                var validationArgs = args.Skip(1).ToArray(); // Pass remaining args
                return await StooqDataValidationTool.RunValidationToolAsync(validationArgs);
            }
            else if (operation == "7" || operation == "benchmark")
            {
                // Run synthetic data benchmark against real market data
                Console.WriteLine("🧬 Running synthetic data benchmark...");
                var benchmarkArgs = args.Skip(1).ToArray(); // Pass remaining args
                return await SyntheticDataBenchmarkTool.RunBenchmarkToolAsync(benchmarkArgs);
            }
            else if (operation == "inspect")
            {
                // Inspect database schema
                Console.WriteLine("🔍 Inspecting database schema...");
                var dbPath = args.Length > 1 ? args[1] : "../data/ODTE_TimeSeries_5Y.db";
                await DatabaseInspector.InspectDatabaseAsync(dbPath);
                return 0;
            }
            else
            {
                Console.WriteLine("❌ Invalid operation. Use: import, gaps, fill, update, backfill, validate, or benchmark");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ FATAL ERROR: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return 1;
        }
    }
}