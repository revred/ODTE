using System;
using System.Threading.Tasks;
using ODTE.Historical;

/// <summary>
/// Direct script to consolidate 2015-2016 parquet data to SQLite
/// </summary>
class ConsolidateParquetToSqlite
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🚀 CONSOLIDATING 2015-2016 PARQUET DATA TO SQLITE");
        Console.WriteLine(new string('=', 60));
        
        try
        {
            string sourceDirectory = @"C:\code\ODTE\data\Historical\XSP";
            Console.WriteLine($"Source: {sourceDirectory}");
            
            using var manager = new HistoricalDataManager();
            Console.WriteLine("Initializing HistoricalDataManager...");
            
            await manager.InitializeAsync();
            Console.WriteLine("Manager initialized successfully");
            
            Console.WriteLine("Starting parquet consolidation...");
            var importResult = await manager.ConsolidateFromParquetAsync(sourceDirectory);
            
            if (importResult.Success)
            {
                Console.WriteLine("✅ CONSOLIDATION SUCCESSFUL!");
                
                var stats = await manager.GetStatsAsync();
                Console.WriteLine($"📊 Database Statistics:");
                Console.WriteLine($"   - Total Records: {stats.TotalRecords:N0}");
                Console.WriteLine($"   - Date Range: {stats.StartDate:yyyy-MM-dd} to {stats.EndDate:yyyy-MM-dd}");
                Console.WriteLine($"   - Database Size: {stats.DatabaseSizeMB:N1} MB");
                Console.WriteLine($"   - Compression: {stats.CompressionRatio:N1}x");
            }
            else
            {
                Console.WriteLine($"❌ CONSOLIDATION FAILED: {importResult.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ERROR: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
        
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}