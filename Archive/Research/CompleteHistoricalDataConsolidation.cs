using System;
using System.Threading.Tasks;
using ODTE.Historical;
using Xunit;
using FluentAssertions;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// Complete Historical Data Consolidation: 2005-Present
    /// Ensures we have ALL historical data to prevent model overfitting to specific periods
    /// </summary>
    public class CompleteHistoricalDataConsolidation
    {
        [Fact]
        public async Task Consolidate_Complete_Historical_Dataset_2005_Present()
        {
            Console.WriteLine("🗄️ CONSOLIDATING COMPLETE HISTORICAL DATASET (2005-PRESENT)");
            Console.WriteLine("=" + new string('=', 80));
            Console.WriteLine("Purpose: Prevent overfitting by ensuring comprehensive market coverage");
            Console.WriteLine("Target: 20+ years of market data for robust strategy validation");
            Console.WriteLine("");
            
            using var manager = new HistoricalDataManager();
            Console.WriteLine("Initializing HistoricalDataManager...");
            
            await manager.InitializeAsync();
            Console.WriteLine("Manager initialized successfully");
            
            // Check current coverage
            var initialStats = await manager.GetStatsAsync();
            Console.WriteLine($"📊 CURRENT DATABASE COVERAGE:");
            Console.WriteLine($"   Records: {initialStats.TotalRecords:N0}");
            Console.WriteLine($"   Date Range: {initialStats.StartDate:yyyy-MM-dd} to {initialStats.EndDate:yyyy-MM-dd}");
            Console.WriteLine($"   Coverage: {(initialStats.EndDate - initialStats.StartDate).TotalDays:F0} days");
            Console.WriteLine($"   Size: {initialStats.DatabaseSizeMB:N1} MB");
            Console.WriteLine("");
            
            // Consolidate ALL available parquet data
            Console.WriteLine("🔄 CONSOLIDATING ALL AVAILABLE PARQUET DATA...");
            string sourceDirectory = @"C:\code\ODTE\data\Historical\XSP";
            
            var consolidationResult = await manager.ConsolidateFromParquetAsync(sourceDirectory);
            
            // Verify comprehensive coverage
            var finalStats = await manager.GetStatsAsync();
            Console.WriteLine($"📈 FINAL DATABASE COVERAGE:");
            Console.WriteLine($"   Records: {finalStats.TotalRecords:N0} (+{finalStats.TotalRecords - initialStats.TotalRecords:N0})");
            Console.WriteLine($"   Date Range: {finalStats.StartDate:yyyy-MM-dd} to {finalStats.EndDate:yyyy-MM-dd}");
            Console.WriteLine($"   Total Coverage: {(finalStats.EndDate - finalStats.StartDate).TotalDays:F0} days");
            Console.WriteLine($"   Size: {finalStats.DatabaseSizeMB:N1} MB (+{finalStats.DatabaseSizeMB - initialStats.DatabaseSizeMB:N1} MB)");
            Console.WriteLine("");
            
            // Validate coverage spans
            var totalYears = (finalStats.EndDate - finalStats.StartDate).TotalDays / 365.25;
            Console.WriteLine($"🎯 COVERAGE VALIDATION:");
            Console.WriteLine($"   Total Years: {totalYears:F1} years");
            Console.WriteLine($"   Start Year: {finalStats.StartDate.Year}");
            Console.WriteLine($"   End Year: {finalStats.EndDate.Year}");
            Console.WriteLine("");
            
            // Market period coverage analysis
            AnalyzeMarketPeriodCoverage(finalStats.StartDate, finalStats.EndDate);
            
            // Assert comprehensive coverage
            consolidationResult.Success.Should().BeTrue("Data consolidation should succeed");
            totalYears.Should().BeGreaterThan(15, "Should have 15+ years of data for robust analysis");
            finalStats.TotalRecords.Should().BeGreaterThan(100000, "Should have substantial record count");
            
            Console.WriteLine("✅ COMPLETE HISTORICAL DATA CONSOLIDATION SUCCESSFUL");
            Console.WriteLine("   PM250 now has comprehensive market data to prevent overfitting!");
        }
        
        private void AnalyzeMarketPeriodCoverage(DateTime start, DateTime end)
        {
            Console.WriteLine("📅 MARKET PERIOD COVERAGE ANALYSIS:");
            
            var periods = new[]
            {
                ("2008 Financial Crisis", new DateTime(2007, 12, 1), new DateTime(2009, 6, 30)),
                ("2010 Flash Crash", new DateTime(2010, 5, 1), new DateTime(2010, 5, 31)),
                ("2015-2016 Volatility", new DateTime(2015, 8, 1), new DateTime(2016, 2, 29)),
                ("2018 Volmageddon", new DateTime(2018, 2, 1), new DateTime(2018, 2, 28)),
                ("2020 COVID Crash", new DateTime(2020, 2, 1), new DateTime(2020, 4, 30)),
                ("2021-2022 Bull/Bear", new DateTime(2021, 1, 1), new DateTime(2022, 12, 31)),
                ("2023-2024 Recovery", new DateTime(2023, 1, 1), new DateTime(2024, 12, 31))
            };
            
            foreach (var (name, periodStart, periodEnd) in periods)
            {
                var covered = start <= periodStart && end >= periodEnd;
                var partial = (start <= periodEnd && end >= periodStart);
                var status = covered ? "✅ FULL" : partial ? "⚠️ PARTIAL" : "❌ MISSING";
                
                Console.WriteLine($"   {status} {name}: {periodStart:yyyy-MM} to {periodEnd:yyyy-MM}");
            }
            Console.WriteLine("");
        }
    }
}