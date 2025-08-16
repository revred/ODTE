using System;
using System.Threading.Tasks;
using ODTE.Historical;
using Xunit;
using FluentAssertions;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// Test to consolidate 2015-2016 parquet data into SQLite database
    /// This ensures PM250 can access the historical data properly
    /// </summary>
    public class ConsolidateHistoricalDataTest
    {
        [Fact]
        public async Task Consolidate_2015_2016_Parquet_Data_To_SQLite()
        {
            Console.WriteLine("🚀 CONSOLIDATING 2015-2016 PARQUET DATA TO SQLITE");
            Console.WriteLine("=" + new string('=', 60));
            
            string sourceDirectory = @"C:\code\ODTE\data\Historical\XSP";
            Console.WriteLine($"Source: {sourceDirectory}");
            
            using var manager = new HistoricalDataManager();
            Console.WriteLine("Initializing HistoricalDataManager...");
            
            await manager.InitializeAsync();
            Console.WriteLine("Manager initialized successfully");
            
            Console.WriteLine("Starting parquet consolidation...");
            var importResult = await manager.ConsolidateFromParquetAsync(sourceDirectory);
            
            // Assert success
            importResult.Should().NotBeNull();
            importResult.Success.Should().BeTrue($"Consolidation failed: {importResult.ErrorMessage}");
            
            if (importResult.Success)
            {
                Console.WriteLine("✅ CONSOLIDATION SUCCESSFUL!");
                
                var stats = await manager.GetStatsAsync();
                Console.WriteLine($"📊 Database Statistics:");
                Console.WriteLine($"   - Total Records: {stats.TotalRecords:N0}");
                Console.WriteLine($"   - Date Range: {stats.StartDate:yyyy-MM-dd} to {stats.EndDate:yyyy-MM-dd}");
                Console.WriteLine($"   - Database Size: {stats.DatabaseSizeMB:N1} MB");
                Console.WriteLine($"   - Coverage Days: {(stats.EndDate - stats.StartDate).TotalDays:F0}");
                
                // Verify we have 2015-2016 data
                stats.StartDate.Year.Should().BeLessOrEqualTo(2015, "Should include 2015 data");
                stats.EndDate.Year.Should().BeGreaterOrEqualTo(2015, "Should include data through at least 2015");
                stats.TotalRecords.Should().BeGreaterThan(10000, "Should have substantial historical data");
                
                Console.WriteLine("✅ PM250 HISTORICAL DATA READY FOR TRADING SIMULATION!");
            }
            else
            {
                Console.WriteLine($"❌ CONSOLIDATION FAILED: {importResult.ErrorMessage}");
                throw new Exception($"Data consolidation failed: {importResult.ErrorMessage}");
            }
        }
    }
}