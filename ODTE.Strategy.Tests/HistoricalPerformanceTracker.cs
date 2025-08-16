using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// Historical Performance Tracker for PM250 and PMxyz tool versions
    /// 
    /// OBJECTIVES:
    /// - Store comprehensive performance data for each PM250 version and time period
    /// - Enable detailed comparison across different tool versions
    /// - Track performance evolution over time
    /// - Provide analytics for strategy optimization
    /// - Maintain audit trail of all testing results
    /// </summary>
    public class HistoricalPerformanceTracker
    {
        private readonly string _databasePath;
        private readonly string _performanceDbFile;
        private readonly string _comparisonReportsPath;

        public HistoricalPerformanceTracker(string basePath)
        {
            _databasePath = Path.Combine(basePath, "PerformanceDatabase");
            _performanceDbFile = Path.Combine(_databasePath, "PM250_HistoricalPerformance.json");
            _comparisonReportsPath = Path.Combine(_databasePath, "ComparisonReports");
            
            Directory.CreateDirectory(_databasePath);
            Directory.CreateDirectory(_comparisonReportsPath);
        }

        /// <summary>
        /// Record yearly performance results for a specific PM250 version
        /// </summary>
        public async Task RecordYearlyPerformance(PM250_ComprehensiveYearlyTesting.YearlyTestResult yearResult)
        {
            var performanceDb = await LoadPerformanceDatabase();
            
            var performanceRecord = new PerformanceRecord
            {
                Id = Guid.NewGuid().ToString(),
                ToolVersion = yearResult.ToolVersion,
                Year = yearResult.Year,
                TestDate = yearResult.TestExecutionTime,
                
                // Core Performance Metrics
                TotalTrades = yearResult.TotalTrades,
                TotalPnL = yearResult.TotalPnL,
                WinRate = yearResult.WinRate,
                AverageTradeProfit = yearResult.AverageTradeProfit,
                MaxSingleWin = yearResult.MaxSingleWin,
                MaxSingleLoss = yearResult.MaxSingleLoss,
                MaxDrawdown = yearResult.MaxDrawdown,
                SharpeRatio = yearResult.SharpeRatio,
                ProfitFactor = yearResult.ProfitFactor,
                
                // Risk Management Metrics
                RFibResets = yearResult.RFibResets,
                RiskCapacityExhausted = yearResult.RiskCapacityExhausted,
                ResetThreshold = ODTE.Strategy.Configuration.RFibConfiguration.Instance.ResetProfitThreshold,
                
                // Consistency Metrics
                ProfitableMonths = yearResult.ProfitableMonths,
                ConsistencyScore = yearResult.ConsistencyScore,
                
                // Additional Metadata
                ResultsFilePath = yearResult.ResultsFilePath,
                TotalTradeRecords = yearResult.TradeLedger.Count,
                TotalRiskEvents = yearResult.RiskManagementEvents.Count
            };

            performanceDb.Records.Add(performanceRecord);
            performanceDb.LastUpdated = DateTime.UtcNow;
            
            await SavePerformanceDatabase(performanceDb);
            
            Console.WriteLine($"📊 Performance recorded for {yearResult.ToolVersion} - {yearResult.Year}");
            Console.WriteLine($"   Record ID: {performanceRecord.Id}");
            Console.WriteLine($"   Database entries: {performanceDb.Records.Count}");
            
            // Generate comparison reports
            await GenerateVersionComparisonReport(performanceRecord, performanceDb);
            await GenerateTimeSeriesAnalysis(performanceDb);
        }

        /// <summary>
        /// Get historical results for comparison
        /// </summary>
        public async Task<List<PerformanceRecord>> GetHistoricalResults()
        {
            var db = await LoadPerformanceDatabase();
            return db.Records.OrderByDescending(r => r.TestDate).ToList();
        }

        /// <summary>
        /// Get performance results for a specific year
        /// </summary>
        public async Task<List<PerformanceRecord>> GetResultsForYear(int year)
        {
            var db = await LoadPerformanceDatabase();
            return db.Records.Where(r => r.Year == year).OrderByDescending(r => r.TestDate).ToList();
        }

        /// <summary>
        /// Get performance results for a specific tool version
        /// </summary>
        public async Task<List<PerformanceRecord>> GetResultsForVersion(string toolVersion)
        {
            var db = await LoadPerformanceDatabase();
            return db.Records.Where(r => r.ToolVersion == toolVersion).OrderBy(r => r.Year).ToList();
        }

        /// <summary>
        /// Generate comprehensive comparison report between tool versions
        /// </summary>
        public async Task<string> GenerateComprehensiveComparisonReport(string version1, string version2)
        {
            var v1Results = await GetResultsForVersion(version1);
            var v2Results = await GetResultsForVersion(version2);
            
            if (!v1Results.Any() || !v2Results.Any())
            {
                return "Insufficient data for comparison";
            }

            var reportPath = Path.Combine(_comparisonReportsPath, $"Comparison_{SanitizeFileName(version1)}_vs_{SanitizeFileName(version2)}_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            
            var report = new List<string>
            {
                $"PM250 TOOL VERSION COMPARISON REPORT",
                new string('=', 60),
                "",
                $"Version 1: {version1}",
                $"  Test Years: {string.Join(", ", v1Results.Select(r => r.Year))}",
                $"  Total Tests: {v1Results.Count}",
                "",
                $"Version 2: {version2}",
                $"  Test Years: {string.Join(", ", v2Results.Select(r => r.Year))}",
                $"  Total Tests: {v2Results.Count}",
                "",
                "PERFORMANCE COMPARISON:",
                ""
            };

            // Calculate aggregate statistics
            var v1Stats = CalculateAggregateStatistics(v1Results);
            var v2Stats = CalculateAggregateStatistics(v2Results);
            
            report.AddRange(new[]
            {
                $"Average Total P&L:",
                $"  {version1}: ${v1Stats.AvgTotalPnL:N2}",
                $"  {version2}: ${v2Stats.AvgTotalPnL:N2}",
                $"  Improvement: {((v2Stats.AvgTotalPnL - v1Stats.AvgTotalPnL) / Math.Abs(v1Stats.AvgTotalPnL) * 100):+0.0;-0.0}%",
                "",
                $"Average Win Rate:",
                $"  {version1}: {v1Stats.AvgWinRate:F1}%",
                $"  {version2}: {v2Stats.AvgWinRate:F1}%",
                $"  Improvement: {(v2Stats.AvgWinRate - v1Stats.AvgWinRate):+0.0;-0.0} percentage points",
                "",
                $"Average Max Drawdown:",
                $"  {version1}: {v1Stats.AvgMaxDrawdown:F1}%",
                $"  {version2}: {v2Stats.AvgMaxDrawdown:F1}%",
                $"  Improvement: {(v1Stats.AvgMaxDrawdown - v2Stats.AvgMaxDrawdown):+0.0;-0.0} percentage points (lower is better)",
                "",
                $"Average Sharpe Ratio:",
                $"  {version1}: {v1Stats.AvgSharpeRatio:F2}",
                $"  {version2}: {v2Stats.AvgSharpeRatio:F2}",
                $"  Improvement: {(v2Stats.AvgSharpeRatio - v1Stats.AvgSharpeRatio):+0.00;-0.00}",
                "",
                $"Risk Management Comparison:",
                $"  Avg RFib Resets per Year:",
                $"    {version1}: {v1Stats.AvgRFibResets:F1}",
                $"    {version2}: {v2Stats.AvgRFibResets:F1}",
                $"  Avg Risk Capacity Exhausted per Year:",
                $"    {version1}: {v1Stats.AvgRiskExhausted:F1}",
                $"    {version2}: {v2Stats.AvgRiskExhausted:F1}",
                ""
            });

            // Year-by-year comparison for common years
            var commonYears = v1Results.Select(r => r.Year).Intersect(v2Results.Select(r => r.Year)).OrderBy(y => y);
            if (commonYears.Any())
            {
                report.Add("YEAR-BY-YEAR COMPARISON (Common Years):");
                report.Add($"{"Year",-6} {"V1 P&L",-12} {"V2 P&L",-12} {"Improvement",-12} {"V1 WinRate",-10} {"V2 WinRate",-10}");
                report.Add(new string('-', 70));
                
                foreach (var year in commonYears)
                {
                    var v1Year = v1Results.First(r => r.Year == year);
                    var v2Year = v2Results.First(r => r.Year == year);
                    var improvement = ((v2Year.TotalPnL - v1Year.TotalPnL) / Math.Abs(v1Year.TotalPnL) * 100);
                    
                    report.Add($"{year,-6} ${v1Year.TotalPnL,-11:N0} ${v2Year.TotalPnL,-11:N0} {improvement,-11:+0.0;-0.0}% {v1Year.WinRate,-9:F1}% {v2Year.WinRate,-9:F1}%");
                }
                report.Add("");
            }

            // Recommendations
            report.AddRange(GenerateRecommendations(v1Stats, v2Stats, version1, version2));
            
            await File.WriteAllLinesAsync(reportPath, report);
            
            Console.WriteLine($"📈 Comparison report generated: {reportPath}");
            return reportPath;
        }

        /// <summary>
        /// Generate time series analysis showing performance evolution
        /// </summary>
        private async Task GenerateTimeSeriesAnalysis(PerformanceDatabase db)
        {
            var reportPath = Path.Combine(_comparisonReportsPath, $"TimeSeriesAnalysis_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            
            var records = db.Records.OrderBy(r => r.Year).ThenBy(r => r.TestDate).ToList();
            
            var report = new List<string>
            {
                "PM250 TIME SERIES PERFORMANCE ANALYSIS",
                new string('=', 50),
                "",
                $"Analysis Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                $"Total Records: {records.Count}",
                $"Year Range: {records.Min(r => r.Year)} - {records.Max(r => r.Year)}",
                "",
                "PERFORMANCE EVOLUTION:",
                ""
            };

            // Group by year and show evolution
            var yearlyPerformance = records.GroupBy(r => r.Year).Select(g => new
            {
                Year = g.Key,
                BestPnL = g.Max(r => r.TotalPnL),
                AvgPnL = g.Average(r => r.TotalPnL),
                BestWinRate = g.Max(r => r.WinRate),
                AvgWinRate = g.Average(r => r.WinRate),
                TestCount = g.Count(),
                LatestVersion = g.OrderByDescending(r => r.TestDate).First().ToolVersion
            }).OrderBy(x => x.Year);

            report.Add($"{"Year",-6} {"Tests",-6} {"Best P&L",-12} {"Avg P&L",-12} {"Best WR",-10} {"Avg WR",-10} {"Latest Version",-20}");
            report.Add(new string('-', 80));
            
            foreach (var year in yearlyPerformance)
            {
                report.Add($"{year.Year,-6} {year.TestCount,-6} ${year.BestPnL,-11:N0} ${year.AvgPnL,-11:N0} {year.BestWinRate,-9:F1}% {year.AvgWinRate,-9:F1}% {year.LatestVersion,-20}");
            }
            
            report.Add("");
            
            // Trend analysis
            if (yearlyPerformance.Count() > 1)
            {
                var firstYear = yearlyPerformance.First();
                var lastYear = yearlyPerformance.Last();
                var yearSpan = lastYear.Year - firstYear.Year;
                
                if (yearSpan > 0)
                {
                    var pnlTrend = (lastYear.AvgPnL - firstYear.AvgPnL) / yearSpan;
                    var winRateTrend = (lastYear.AvgWinRate - firstYear.AvgWinRate) / yearSpan;
                    
                    report.AddRange(new[]
                    {
                        "TREND ANALYSIS:",
                        $"  Annual P&L Trend: ${pnlTrend:+0;-0}/year",
                        $"  Annual Win Rate Trend: {winRateTrend:+0.0;-0.0} percentage points/year",
                        $"  Overall Improvement: {((lastYear.AvgPnL - firstYear.AvgPnL) / Math.Abs(firstYear.AvgPnL) * 100):+0.0;-0.0}% over {yearSpan} years",
                        ""
                    });
                }
            }

            await File.WriteAllLinesAsync(reportPath, report);
            Console.WriteLine($"📊 Time series analysis generated: {reportPath}");
        }

        /// <summary>
        /// Generate version comparison report for a specific performance record
        /// </summary>
        private async Task GenerateVersionComparisonReport(PerformanceRecord newRecord, PerformanceDatabase db)
        {
            var sameYearRecords = db.Records.Where(r => r.Year == newRecord.Year && r.Id != newRecord.Id).ToList();
            
            if (!sameYearRecords.Any()) return;
            
            var reportPath = Path.Combine(_comparisonReportsPath, $"VersionComparison_{newRecord.Year}_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            
            var report = new List<string>
            {
                $"PM250 VERSION COMPARISON - {newRecord.Year}",
                new string('=', 40),
                "",
                $"New Record: {newRecord.ToolVersion}",
                $"Test Date: {newRecord.TestDate:yyyy-MM-dd HH:mm:ss}",
                "",
                "COMPARISON WITH PREVIOUS VERSIONS FOR SAME YEAR:",
                ""
            };

            var sortedRecords = sameYearRecords.OrderByDescending(r => r.TotalPnL).ToList();
            
            report.Add($"{"Rank",-4} {"Tool Version",-30} {"Total P&L",-12} {"Win Rate",-10} {"Sharpe",-8} {"Test Date",-12}");
            report.Add(new string('-', 80));
            
            var allRecords = sortedRecords.Concat(new[] { newRecord }).OrderByDescending(r => r.TotalPnL).ToList();
            
            for (int i = 0; i < allRecords.Count; i++)
            {
                var record = allRecords[i];
                var marker = record.Id == newRecord.Id ? "***" : "   ";
                report.Add($"{i + 1,-4} {marker}{record.ToolVersion,-27} ${record.TotalPnL,-11:N0} {record.WinRate,-9:F1}% {record.SharpeRatio,-7:F2} {record.TestDate:MM-dd-yyyy}");
            }
            
            await File.WriteAllLinesAsync(reportPath, report);
            Console.WriteLine($"📋 Version comparison generated: {reportPath}");
        }

        #region Helper Methods

        private async Task<PerformanceDatabase> LoadPerformanceDatabase()
        {
            if (File.Exists(_performanceDbFile))
            {
                try
                {
                    var jsonData = await File.ReadAllTextAsync(_performanceDbFile);
                    var db = JsonSerializer.Deserialize<PerformanceDatabase>(jsonData);
                    return db ?? new PerformanceDatabase();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Error loading performance database: {ex.Message}");
                    Console.WriteLine("   Creating new database");
                }
            }
            
            return new PerformanceDatabase();
        }

        private async Task SavePerformanceDatabase(PerformanceDatabase db)
        {
            var jsonOptions = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var jsonData = JsonSerializer.Serialize(db, jsonOptions);
            await File.WriteAllTextAsync(_performanceDbFile, jsonData);
        }

        private AggregateStatistics CalculateAggregateStatistics(List<PerformanceRecord> records)
        {
            if (!records.Any())
                return new AggregateStatistics();

            return new AggregateStatistics
            {
                AvgTotalPnL = records.Average(r => r.TotalPnL),
                AvgWinRate = records.Average(r => r.WinRate),
                AvgMaxDrawdown = records.Average(r => r.MaxDrawdown),
                AvgSharpeRatio = records.Average(r => r.SharpeRatio),
                AvgRFibResets = records.Average(r => r.RFibResets),
                AvgRiskExhausted = records.Average(r => r.RiskCapacityExhausted),
                TotalTests = records.Count
            };
        }

        private List<string> GenerateRecommendations(AggregateStatistics v1Stats, AggregateStatistics v2Stats, string version1, string version2)
        {
            var recommendations = new List<string>
            {
                "RECOMMENDATIONS:",
                ""
            };

            var pnlImprovement = (v2Stats.AvgTotalPnL - v1Stats.AvgTotalPnL) / Math.Abs(v1Stats.AvgTotalPnL) * 100;
            var winRateImprovement = v2Stats.AvgWinRate - v1Stats.AvgWinRate;
            var drawdownImprovement = v1Stats.AvgMaxDrawdown - v2Stats.AvgMaxDrawdown;
            var sharpeImprovement = v2Stats.AvgSharpeRatio - v1Stats.AvgSharpeRatio;

            if (pnlImprovement > 10)
            {
                recommendations.Add($"✅ Significant P&L improvement ({pnlImprovement:F1}%) - {version2} is clearly superior");
            }
            else if (pnlImprovement < -10)
            {
                recommendations.Add($"❌ P&L regression ({pnlImprovement:F1}%) - consider reverting to {version1}");
            }
            else
            {
                recommendations.Add($"➡️ P&L change is moderate ({pnlImprovement:F1}%) - monitor additional metrics");
            }

            if (winRateImprovement > 5)
            {
                recommendations.Add($"✅ Win rate significantly improved (+{winRateImprovement:F1} pp)");
            }
            else if (winRateImprovement < -5)
            {
                recommendations.Add($"⚠️ Win rate declined ({winRateImprovement:F1} pp) - investigate strategy changes");
            }

            if (drawdownImprovement > 2)
            {
                recommendations.Add($"✅ Risk management improved (drawdown reduced by {drawdownImprovement:F1} pp)");
            }
            else if (drawdownImprovement < -2)
            {
                recommendations.Add($"⚠️ Risk management degraded (drawdown increased by {Math.Abs(drawdownImprovement):F1} pp)");
            }

            if (sharpeImprovement > 0.2)
            {
                recommendations.Add($"✅ Risk-adjusted returns improved (Sharpe +{sharpeImprovement:F2})");
            }
            else if (sharpeImprovement < -0.2)
            {
                recommendations.Add($"⚠️ Risk-adjusted returns degraded (Sharpe {sharpeImprovement:F2})");
            }

            // Overall recommendation
            var positiveMetrics = new[] { pnlImprovement > 5, winRateImprovement > 2, drawdownImprovement > 1, sharpeImprovement > 0.1 }.Count(x => x);
            var negativeMetrics = new[] { pnlImprovement < -5, winRateImprovement < -2, drawdownImprovement < -1, sharpeImprovement < -0.1 }.Count(x => x);

            recommendations.Add("");
            if (positiveMetrics >= 3)
            {
                recommendations.Add($"🎯 OVERALL: {version2} shows strong improvement - RECOMMENDED for production");
            }
            else if (negativeMetrics >= 3)
            {
                recommendations.Add($"🚨 OVERALL: {version2} shows concerning regression - NOT RECOMMENDED");
            }
            else
            {
                recommendations.Add($"📊 OVERALL: Mixed results - continue testing or consider hybrid approach");
            }

            return recommendations;
        }

        private string SanitizeFileName(string fileName)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return new string(fileName.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        }

        #endregion

        #region Data Models

        public class PerformanceDatabase
        {
            public List<PerformanceRecord> Records { get; set; } = new();
            public DateTime LastUpdated { get; set; }
            public string Version { get; set; } = "1.0";
        }

        public class PerformanceRecord
        {
            public string Id { get; set; } = "";
            public string ToolVersion { get; set; } = "";
            public int Year { get; set; }
            public DateTime TestDate { get; set; }
            
            // Core Performance Metrics
            public int TotalTrades { get; set; }
            public decimal TotalPnL { get; set; }
            public double WinRate { get; set; }
            public decimal AverageTradeProfit { get; set; }
            public decimal MaxSingleWin { get; set; }
            public decimal MaxSingleLoss { get; set; }
            public double MaxDrawdown { get; set; }
            public double SharpeRatio { get; set; }
            public double ProfitFactor { get; set; }
            
            // Risk Management Metrics
            public int RFibResets { get; set; }
            public int RiskCapacityExhausted { get; set; }
            public decimal ResetThreshold { get; set; }
            
            // Consistency Metrics
            public int ProfitableMonths { get; set; }
            public double ConsistencyScore { get; set; }
            
            // Metadata
            public string ResultsFilePath { get; set; } = "";
            public int TotalTradeRecords { get; set; }
            public int TotalRiskEvents { get; set; }
        }

        public class AggregateStatistics
        {
            public decimal AvgTotalPnL { get; set; }
            public double AvgWinRate { get; set; }
            public double AvgMaxDrawdown { get; set; }
            public double AvgSharpeRatio { get; set; }
            public double AvgRFibResets { get; set; }
            public double AvgRiskExhausted { get; set; }
            public int TotalTests { get; set; }
        }

        #endregion
    }
}