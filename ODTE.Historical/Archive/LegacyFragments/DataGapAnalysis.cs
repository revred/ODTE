using ODTE.Historical;

namespace ODTE.DataAnalysis
{
    /// <summary>
    /// Analyzes current SQLite database to identify data gaps for 2005-present acquisition
    /// </summary>
    public class DataGapAnalysis
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("🔍 ODTE DATA GAP ANALYSIS - 2005 to Present");
            Console.WriteLine("=".PadRight(60, '='));
            Console.WriteLine();

            using var dataManager = new HistoricalDataManager();
            await dataManager.InitializeAsync();

            // Get current database stats
            var stats = await dataManager.GetStatsAsync();
            Console.WriteLine("📊 CURRENT DATABASE STATUS:");
            Console.WriteLine("-".PadRight(30, '-'));
            Console.WriteLine($"Start Date: {stats.StartDate:yyyy-MM-dd}");
            Console.WriteLine($"End Date: {stats.EndDate:yyyy-MM-dd}");
            Console.WriteLine($"Total Records: {stats.TotalRecords:N0}");
            Console.WriteLine($"Database Size: {stats.DatabaseSizeMB:F1} MB");
            Console.WriteLine($"Compression Ratio: {stats.CompressionRatio:F1}x");
            Console.WriteLine();

            // Define target range (2005 to present)
            var targetStart = new DateTime(2005, 1, 1);
            var targetEnd = DateTime.Now.Date;
            var totalYears = (targetEnd - targetStart).TotalDays / 365.25;

            Console.WriteLine("🎯 TARGET DATA RANGE:");
            Console.WriteLine("-".PadRight(25, '-'));
            Console.WriteLine($"Target Start: {targetStart:yyyy-MM-dd}");
            Console.WriteLine($"Target End: {targetEnd:yyyy-MM-dd}");
            Console.WriteLine($"Total Years: {totalYears:F1} years");
            Console.WriteLine($"Estimated Trading Days: {totalYears * 252:F0} days");
            Console.WriteLine();

            // Identify gaps
            Console.WriteLine("🕳️ DATA GAPS IDENTIFIED:");
            Console.WriteLine("-".PadRight(30, '-'));

            var gaps = new List<(DateTime Start, DateTime End, string Description)>();

            // Gap 1: Pre-current data (2005 to current start)
            if (stats.StartDate > targetStart)
            {
                var gapYears = (stats.StartDate - targetStart).TotalDays / 365.25;
                gaps.Add((targetStart, stats.StartDate.AddDays(-1),
                    $"Pre-2015 Historical Data ({gapYears:F1} years)"));
            }

            // Gap 2: Post-current data (current end to present)
            if (stats.EndDate < targetEnd)
            {
                var gapDays = (targetEnd - stats.EndDate).TotalDays;
                gaps.Add((stats.EndDate.AddDays(1), targetEnd,
                    $"Recent Data ({gapDays:F0} days missing)"));
            }

            // Display gaps
            if (gaps.Count > 0)
            {
                for (int i = 0; i < gaps.Count; i++)
                {
                    var gap = gaps[i];
                    var gapDays = (gap.End - gap.Start).TotalDays + 1;
                    var gapTradingDays = gapDays * 252 / 365; // Approximate

                    Console.WriteLine($"Gap {i + 1}: {gap.Description}");
                    Console.WriteLine($"   Period: {gap.Start:yyyy-MM-dd} to {gap.End:yyyy-MM-dd}");
                    Console.WriteLine($"   Duration: {gapDays:F0} days ({gapTradingDays:F0} trading days)");
                    Console.WriteLine($"   Priority: {(gap.End > DateTime.Now.AddYears(-2) ? "HIGH" : "MEDIUM")}");
                    Console.WriteLine();
                }
            }
            else
            {
                Console.WriteLine("✅ No gaps found - database covers target range");
            }

            // Calculate data acquisition requirements
            Console.WriteLine("📈 DATA ACQUISITION REQUIREMENTS:");
            Console.WriteLine("-".PadRight(40, '-'));

            var totalGapDays = 0.0;
            foreach (var gap in gaps)
            {
                totalGapDays += (gap.End - gap.Start).TotalDays + 1;
            }

            var estimatedTradingDays = totalGapDays * 252 / 365;
            var estimatedRecords = estimatedTradingDays * 390; // ~390 bars per trading day
            var estimatedSizeMB = estimatedRecords * 64 / (1024 * 1024); // ~64 bytes per record

            Console.WriteLine($"Total Missing Days: {totalGapDays:F0}");
            Console.WriteLine($"Estimated Trading Days: {estimatedTradingDays:F0}");
            Console.WriteLine($"Estimated Records: {estimatedRecords:N0}");
            Console.WriteLine($"Estimated Size: {estimatedSizeMB:F0} MB");
            Console.WriteLine();

            // Available symbols analysis
            var symbols = await dataManager.GetAvailableSymbolsAsync();
            Console.WriteLine("📋 AVAILABLE SYMBOLS:");
            Console.WriteLine("-".PadRight(25, '-'));
            foreach (var symbol in symbols)
            {
                Console.WriteLine($"   ✅ {symbol}");
            }
            Console.WriteLine();

            // Recommendations
            Console.WriteLine("💡 ACQUISITION STRATEGY RECOMMENDATIONS:");
            Console.WriteLine("-".PadRight(45, '-'));
            Console.WriteLine("1. 🎯 Focus on XSP/SPX options data (primary strategy requirement)");
            Console.WriteLine("2. 📅 Prioritize recent data (2022-2024) for immediate use");
            Console.WriteLine("3. 🏗️ Acquire in yearly chunks to manage memory/processing");
            Console.WriteLine("4. 🔄 Use incremental updates for daily/weekly recent data");
            Console.WriteLine("5. ✅ Validate each chunk before SQLite insertion");
            Console.WriteLine("6. 💾 Maintain Parquet backups during conversion process");
            Console.WriteLine();

            Console.WriteLine("📊 CHUNK STRATEGY RECOMMENDATION:");
            Console.WriteLine("-".PadRight(35, '-'));
            Console.WriteLine("Chunk 1: 2022-2024 (High Priority - Recent Data)");
            Console.WriteLine("Chunk 2: 2018-2021 (Medium Priority - COVID Era)");
            Console.WriteLine("Chunk 3: 2015-2017 (Medium Priority - Modern Markets)");
            Console.WriteLine("Chunk 4: 2010-2014 (Low Priority - Post-Crisis)");
            Console.WriteLine("Chunk 5: 2005-2009 (Low Priority - Crisis Era)");
            Console.WriteLine();

            Console.WriteLine($"🎯 NEXT STEPS:");
            Console.WriteLine("-".PadRight(15, '-'));
            Console.WriteLine("1. Research authentic data sources (Yahoo, Alpha Vantage, Polygon, etc.)");
            Console.WriteLine("2. Set up data acquisition pipeline with error handling");
            Console.WriteLine("3. Design chunked processing with progress tracking");
            Console.WriteLine("4. Implement SQLite batch insertion with validation");
            Console.WriteLine("5. Create data quality checks and gap detection");
        }
    }
}