using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ODTE.Optimization.Core;
using ODTE.Optimization.RiskManagement;

namespace ODTE.Optimization.Reporting
{
    public class VersionedPnLReporter
    {
        private readonly string _reportsBasePath;
        private readonly Dictionary<string, StrategyReport> _strategyReports;
        
        public VersionedPnLReporter(string reportsBasePath = @"C:\code\ODTE\Reports\Optimization")
        {
            _reportsBasePath = reportsBasePath;
            _strategyReports = new Dictionary<string, StrategyReport>();
            Directory.CreateDirectory(_reportsBasePath);
        }
        
        public async Task GenerateReportAsync(
            StrategyVersion strategy,
            PerformanceMetrics performance,
            ReverseFibonacciRiskManager.RiskAnalytics riskAnalytics)
        {
            var report = new StrategyReport
            {
                StrategyName = strategy.StrategyName,
                Version = strategy.Version,
                GeneratedAt = DateTime.Now,
                Performance = performance,
                RiskAnalytics = riskAnalytics,
                Parameters = strategy.Parameters
            };
            
            // Generate various report formats
            await GenerateDetailedReportAsync(report);
            await GenerateSummaryReportAsync(report);
            await GenerateComparisonReportAsync(report);
            await GenerateDailyPnLReportAsync(report);
            await GenerateRiskReportAsync(report);
            
            // Store for comparison
            _strategyReports[strategy.Version] = report;
        }
        
        private async Task GenerateDetailedReportAsync(StrategyReport report)
        {
            var strategyDir = Path.Combine(_reportsBasePath, report.StrategyName, report.Version);
            Directory.CreateDirectory(strategyDir);
            
            var detailedPath = Path.Combine(strategyDir, "detailed_report.txt");
            
            var sb = new StringBuilder();
            sb.AppendLine("=" .PadRight(80, '='));
            sb.AppendLine($"STRATEGY PERFORMANCE REPORT - {report.StrategyName} v{report.Version}");
            sb.AppendLine("=" .PadRight(80, '='));
            sb.AppendLine($"Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            
            sb.AppendLine("PERFORMANCE METRICS");
            sb.AppendLine("-" .PadRight(40, '-'));
            sb.AppendLine($"Total P&L:           ${report.Performance.TotalPnL:N2}");
            sb.AppendLine($"Max Drawdown:        ${report.Performance.MaxDrawdown:N2}");
            sb.AppendLine($"Win Rate:            {report.Performance.WinRate:P2}");
            sb.AppendLine($"Sharpe Ratio:        {report.Performance.SharpeRatio:F3}");
            sb.AppendLine($"Calmar Ratio:        {report.Performance.CalmarRatio:F3}");
            sb.AppendLine($"Profit Factor:       {report.Performance.ProfitFactor:F2}");
            sb.AppendLine($"Expected Value:      ${report.Performance.ExpectedValue:N2}");
            sb.AppendLine();
            
            sb.AppendLine("TRADING STATISTICS");
            sb.AppendLine("-" .PadRight(40, '-'));
            sb.AppendLine($"Total Trades:        {report.Performance.TotalTrades}");
            sb.AppendLine($"Winning Days:        {report.Performance.WinningDays}");
            sb.AppendLine($"Losing Days:         {report.Performance.LosingDays}");
            sb.AppendLine($"Average Daily P&L:   ${report.Performance.AverageDailyPnL:N2}");
            sb.AppendLine($"Std Deviation:       ${report.Performance.StandardDeviation:N2}");
            sb.AppendLine();
            
            sb.AppendLine("RISK MANAGEMENT (Reverse Fibonacci)");
            sb.AppendLine("-" .PadRight(40, '-'));
            if (report.RiskAnalytics != null)
            {
                sb.AppendLine($"Total Days:          {report.RiskAnalytics.TotalDays}");
                sb.AppendLine($"Normal Risk Days:    {report.RiskAnalytics.DaysAtNormalRisk}");
                sb.AppendLine($"Reduced Risk Days:   {report.RiskAnalytics.DaysAtReducedRisk}");
                sb.AppendLine($"Max Loss Breaches:   {report.RiskAnalytics.MaxLossBreaches}");
                sb.AppendLine($"Current Streak:      {report.RiskAnalytics.CurrentStreak}");
                sb.AppendLine($"Average Daily:       ${report.RiskAnalytics.AverageDaily:N2}");
            }
            sb.AppendLine();
            
            sb.AppendLine("STRATEGY PARAMETERS");
            sb.AppendLine("-" .PadRight(40, '-'));
            sb.AppendLine($"Opening Range:       {report.Parameters.OpeningRangeMinutes} minutes");
            sb.AppendLine($"Min IV Rank:         {report.Parameters.MinIVRank}%");
            sb.AppendLine($"Max Delta:           {report.Parameters.MaxDelta:F2}");
            sb.AppendLine($"Min Premium:         ${report.Parameters.MinPremium:F2}");
            sb.AppendLine($"Strike Offset:       {report.Parameters.StrikeOffset}");
            sb.AppendLine($"Stop Loss:           {report.Parameters.StopLossPercent}%");
            sb.AppendLine($"Profit Target:       {report.Parameters.ProfitTargetPercent}%");
            sb.AppendLine($"Delta Exit:          {report.Parameters.DeltaExitThreshold:F2}");
            sb.AppendLine($"Max Positions/Side:  {report.Parameters.MaxPositionsPerSide}");
            sb.AppendLine($"Allocation/Trade:    ${report.Parameters.AllocationPerTrade:N0}");
            sb.AppendLine($"Entry Window:        {report.Parameters.EntryStartTime:hh\\:mm} - {report.Parameters.EntryEndTime:hh\\:mm}");
            sb.AppendLine($"Force Close Time:    {report.Parameters.ForceCloseTime:hh\\:mm}");
            sb.AppendLine($"Use VWAP Filter:     {report.Parameters.UseVWAPFilter}");
            sb.AppendLine($"Use ATR Filter:      {report.Parameters.UseATRFilter}");
            if (report.Parameters.UseATRFilter)
            {
                sb.AppendLine($"  ATR Range:         {report.Parameters.MinATR:F1} - {report.Parameters.MaxATR:F1}");
            }
            
            await File.WriteAllTextAsync(detailedPath, sb.ToString());
        }
        
        private async Task GenerateSummaryReportAsync(StrategyReport report)
        {
            var summaryDir = Path.Combine(_reportsBasePath, "Summaries");
            Directory.CreateDirectory(summaryDir);
            
            var summaryPath = Path.Combine(summaryDir, $"{report.StrategyName}_v{report.Version}_summary.json");
            
            var summary = new
            {
                Strategy = report.StrategyName,
                Version = report.Version,
                Date = report.GeneratedAt,
                Performance = new
                {
                    TotalPnL = report.Performance.TotalPnL,
                    MaxDrawdown = report.Performance.MaxDrawdown,
                    WinRate = report.Performance.WinRate,
                    SharpeRatio = report.Performance.SharpeRatio,
                    CalmarRatio = report.Performance.CalmarRatio,
                    TotalTrades = report.Performance.TotalTrades
                },
                RiskLevel = DetermineRiskLevel(report.Performance),
                Recommendation = GenerateRecommendation(report.Performance)
            };
            
            var json = JsonSerializer.Serialize(summary, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            await File.WriteAllTextAsync(summaryPath, json);
        }
        
        private async Task GenerateComparisonReportAsync(StrategyReport currentReport)
        {
            if (_strategyReports.Count < 2) return;
            
            var comparisonDir = Path.Combine(_reportsBasePath, "Comparisons");
            Directory.CreateDirectory(comparisonDir);
            
            var comparisonPath = Path.Combine(comparisonDir, 
                $"comparison_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            
            var sb = new StringBuilder();
            sb.AppendLine("Version,Date,TotalPnL,MaxDD,WinRate,Sharpe,Calmar,Trades,WinDays,LoseDays");
            
            foreach (var report in _strategyReports.Values.OrderBy(r => r.GeneratedAt))
            {
                sb.AppendLine($"{report.Version}," +
                    $"{report.GeneratedAt:yyyy-MM-dd}," +
                    $"{report.Performance.TotalPnL:F2}," +
                    $"{report.Performance.MaxDrawdown:F2}," +
                    $"{report.Performance.WinRate:F4}," +
                    $"{report.Performance.SharpeRatio:F3}," +
                    $"{report.Performance.CalmarRatio:F3}," +
                    $"{report.Performance.TotalTrades}," +
                    $"{report.Performance.WinningDays}," +
                    $"{report.Performance.LosingDays}");
            }
            
            await File.WriteAllTextAsync(comparisonPath, sb.ToString());
            
            // Also generate improvement analysis
            await GenerateImprovementAnalysisAsync(currentReport);
        }
        
        private async Task GenerateImprovementAnalysisAsync(StrategyReport currentReport)
        {
            if (_strategyReports.Count < 2) return;
            
            var previousReports = _strategyReports.Values
                .Where(r => r.GeneratedAt < currentReport.GeneratedAt)
                .OrderByDescending(r => r.GeneratedAt)
                .ToList();
            
            if (!previousReports.Any()) return;
            
            var previous = previousReports.First();
            var improvementPath = Path.Combine(_reportsBasePath, currentReport.StrategyName, 
                currentReport.Version, "improvement_analysis.txt");
            
            var sb = new StringBuilder();
            sb.AppendLine("IMPROVEMENT ANALYSIS");
            sb.AppendLine("=" .PadRight(50, '='));
            sb.AppendLine($"Current Version: {currentReport.Version}");
            sb.AppendLine($"Previous Version: {previous.Version}");
            sb.AppendLine();
            
            sb.AppendLine("PERFORMANCE CHANGES");
            sb.AppendLine("-" .PadRight(50, '-'));
            
            var pnlChange = currentReport.Performance.TotalPnL - previous.Performance.TotalPnL;
            var winRateChange = currentReport.Performance.WinRate - previous.Performance.WinRate;
            var sharpeChange = currentReport.Performance.SharpeRatio - previous.Performance.SharpeRatio;
            
            sb.AppendLine($"P&L Change:        {FormatChange(pnlChange, "$")}");
            sb.AppendLine($"Win Rate Change:   {FormatChange(winRateChange * 100, "%")}");
            sb.AppendLine($"Sharpe Change:     {FormatChange(sharpeChange)}");
            sb.AppendLine($"Trades Change:     {currentReport.Performance.TotalTrades - previous.Performance.TotalTrades}");
            sb.AppendLine();
            
            sb.AppendLine("PARAMETER CHANGES");
            sb.AppendLine("-" .PadRight(50, '-'));
            
            if (Math.Abs(currentReport.Parameters.MaxDelta - previous.Parameters.MaxDelta) > 0.01)
                sb.AppendLine($"Max Delta: {previous.Parameters.MaxDelta:F2} -> {currentReport.Parameters.MaxDelta:F2}");
            
            if (Math.Abs(currentReport.Parameters.StopLossPercent - previous.Parameters.StopLossPercent) > 1)
                sb.AppendLine($"Stop Loss: {previous.Parameters.StopLossPercent}% -> {currentReport.Parameters.StopLossPercent}%");
            
            if (currentReport.Parameters.UseATRFilter != previous.Parameters.UseATRFilter)
                sb.AppendLine($"ATR Filter: {previous.Parameters.UseATRFilter} -> {currentReport.Parameters.UseATRFilter}");
            
            sb.AppendLine();
            sb.AppendLine("RECOMMENDATION");
            sb.AppendLine("-" .PadRight(50, '-'));
            
            if (pnlChange > 0 && winRateChange > 0 && sharpeChange > 0)
            {
                sb.AppendLine("STRONG IMPROVEMENT - Consider deploying this version");
            }
            else if (pnlChange > 0 && sharpeChange > 0)
            {
                sb.AppendLine("MODERATE IMPROVEMENT - Further testing recommended");
            }
            else if (pnlChange < 0)
            {
                sb.AppendLine("REGRESSION DETECTED - Review parameter changes");
            }
            else
            {
                sb.AppendLine("MIXED RESULTS - Continue optimization");
            }
            
            await File.WriteAllTextAsync(improvementPath, sb.ToString());
        }
        
        private string FormatChange(double change, string suffix = "")
        {
            var sign = change >= 0 ? "+" : "";
            return $"{sign}{change:F2}{suffix}";
        }
        
        private async Task GenerateDailyPnLReportAsync(StrategyReport report)
        {
            if (report.Performance.DailyPnL == null || !report.Performance.DailyPnL.Any())
                return;
            
            var pnlPath = Path.Combine(_reportsBasePath, report.StrategyName, 
                report.Version, "daily_pnl.csv");
            
            var sb = new StringBuilder();
            sb.AppendLine("Date,PnL,CumulativePnL,DrawdownFromPeak");
            
            double cumulative = 0;
            double peak = 0;
            
            foreach (var (date, pnl) in report.Performance.DailyPnL.OrderBy(kvp => kvp.Key))
            {
                cumulative += pnl;
                peak = Math.Max(peak, cumulative);
                double drawdown = peak > 0 ? (cumulative - peak) / peak * 100 : 0;
                
                sb.AppendLine($"{date:yyyy-MM-dd},{pnl:F2},{cumulative:F2},{drawdown:F2}");
            }
            
            await File.WriteAllTextAsync(pnlPath, sb.ToString());
        }
        
        private async Task GenerateRiskReportAsync(StrategyReport report)
        {
            if (report.RiskAnalytics == null) return;
            
            var riskPath = Path.Combine(_reportsBasePath, report.StrategyName, 
                report.Version, "risk_management.csv");
            
            var sb = new StringBuilder();
            sb.AppendLine("Date,MaxLossAllowed,ActualPnL,RiskLevel,Breached,Action");
            
            foreach (var record in report.RiskAnalytics.RiskHistory)
            {
                sb.AppendLine($"{record.Date:yyyy-MM-dd}," +
                    $"{record.MaxLossAllowed:F2}," +
                    $"{record.ActualPnL:F2}," +
                    $"{record.RiskLevel}," +
                    $"{record.MaxLossBreached}," +
                    $"{record.Action}");
            }
            
            await File.WriteAllTextAsync(riskPath, sb.ToString());
        }
        
        private string DetermineRiskLevel(PerformanceMetrics performance)
        {
            if (performance.MaxDrawdown < -1000) return "HIGH RISK";
            if (performance.MaxDrawdown < -500) return "MODERATE RISK";
            if (performance.MaxDrawdown < -200) return "LOW RISK";
            return "MINIMAL RISK";
        }
        
        private string GenerateRecommendation(PerformanceMetrics performance)
        {
            if (performance.SharpeRatio > 2 && performance.WinRate > 0.6)
                return "HIGHLY RECOMMENDED - Excellent risk-adjusted returns";
            
            if (performance.SharpeRatio > 1 && performance.WinRate > 0.5)
                return "RECOMMENDED - Good performance metrics";
            
            if (performance.TotalPnL > 0)
                return "VIABLE - Positive returns but room for improvement";
            
            return "NOT RECOMMENDED - Requires further optimization";
        }
        
        public async Task GenerateMasterReportAsync(string outputPath = null)
        {
            outputPath ??= Path.Combine(_reportsBasePath, $"master_report_{DateTime.Now:yyyyMMdd_HHmmss}.html");
            
            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html><head>");
            html.AppendLine("<title>ODTE Strategy Optimization Report</title>");
            html.AppendLine("<style>");
            html.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
            html.AppendLine("table { border-collapse: collapse; width: 100%; margin: 20px 0; }");
            html.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
            html.AppendLine("th { background-color: #4CAF50; color: white; }");
            html.AppendLine("tr:nth-child(even) { background-color: #f2f2f2; }");
            html.AppendLine(".positive { color: green; font-weight: bold; }");
            html.AppendLine(".negative { color: red; font-weight: bold; }");
            html.AppendLine("</style>");
            html.AppendLine("</head><body>");
            
            html.AppendLine("<h1>ODTE Strategy Optimization Report</h1>");
            html.AppendLine($"<p>Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");
            
            html.AppendLine("<h2>Strategy Performance Comparison</h2>");
            html.AppendLine("<table>");
            html.AppendLine("<tr><th>Version</th><th>Total P&L</th><th>Max DD</th><th>Win Rate</th>" +
                          "<th>Sharpe</th><th>Calmar</th><th>Trades</th><th>Risk Level</th></tr>");
            
            foreach (var report in _strategyReports.Values.OrderByDescending(r => r.Performance.TotalPnL))
            {
                var pnlClass = report.Performance.TotalPnL > 0 ? "positive" : "negative";
                html.AppendLine($"<tr>");
                html.AppendLine($"<td>{report.Version}</td>");
                html.AppendLine($"<td class='{pnlClass}'>${report.Performance.TotalPnL:N2}</td>");
                html.AppendLine($"<td>${report.Performance.MaxDrawdown:N2}</td>");
                html.AppendLine($"<td>{report.Performance.WinRate:P1}</td>");
                html.AppendLine($"<td>{report.Performance.SharpeRatio:F2}</td>");
                html.AppendLine($"<td>{report.Performance.CalmarRatio:F2}</td>");
                html.AppendLine($"<td>{report.Performance.TotalTrades}</td>");
                html.AppendLine($"<td>{DetermineRiskLevel(report.Performance)}</td>");
                html.AppendLine($"</tr>");
            }
            
            html.AppendLine("</table>");
            
            // Best performing strategy
            var best = _strategyReports.Values.OrderByDescending(r => r.Performance.SharpeRatio).FirstOrDefault();
            if (best != null)
            {
                html.AppendLine("<h2>Best Performing Strategy</h2>");
                html.AppendLine($"<p><strong>Version:</strong> {best.Version}</p>");
                html.AppendLine($"<p><strong>Sharpe Ratio:</strong> {best.Performance.SharpeRatio:F3}</p>");
                html.AppendLine($"<p><strong>Total P&L:</strong> ${best.Performance.TotalPnL:N2}</p>");
                html.AppendLine($"<p><strong>Recommendation:</strong> {GenerateRecommendation(best.Performance)}</p>");
            }
            
            html.AppendLine("</body></html>");
            
            await File.WriteAllTextAsync(outputPath, html.ToString());
        }
    }
    
    public class StrategyReport
    {
        public string StrategyName { get; set; }
        public string Version { get; set; }
        public DateTime GeneratedAt { get; set; }
        public PerformanceMetrics Performance { get; set; }
        public ReverseFibonacciRiskManager.RiskAnalytics RiskAnalytics { get; set; }
        public StrategyParameters Parameters { get; set; }
    }
}