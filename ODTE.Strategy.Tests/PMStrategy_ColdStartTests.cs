using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// Comprehensive tests for PMxyz Cold Start Capability
    /// Validates that any PMxyz strategy version can be loaded and executed from cold start
    /// </summary>
    public class PMStrategy_ColdStartTests
    {
        [Theory]
        [InlineData("PM250")]
        [InlineData("PM300")]
        [InlineData("PM500")]
        [InlineData("PMxyz")]
        [InlineData("PM250_v2.1")]
        [InlineData("PM250_ConfigurableRFib")]
        public async Task ColdStart_CanLoad_AllPMStrategyVersions(string strategyName)
        {
            // Arrange
            var loader = new PMStrategy_ColdStartLoader();
            var testYear = 2020;
            var testMonths = new[] { 3, 4, 5 }; // Q1 validation

            Console.WriteLine($"🚀 COLD START TEST: {strategyName}");
            
            // Act
            var result = await loader.ExecuteComprehensiveTesting(strategyName, testYear, testMonths);
            
            // Assert
            result.Should().NotBeNull("Cold start should successfully load and execute");
            result.ToolVersion.Should().Contain(strategyName, "Should identify correct strategy version");
            result.Year.Should().Be(testYear, "Should test correct year");
            result.TotalTrades.Should().BeGreaterThan(0, "Should generate test trades");
            result.TradeLedger.Should().NotBeEmpty("Should create detailed trade ledger");
            result.MonthlyResults.Should().HaveCount(3, "Should process all 3 test months");
            result.ResultsFilePath.Should().NotBeEmpty("Should save results file");
            
            Console.WriteLine($"✅ {strategyName} cold start validation PASSED");
            Console.WriteLine($"   Trades: {result.TotalTrades}");
            Console.WriteLine($"   P&L: ${result.TotalPnL:N2}");
            Console.WriteLine($"   Win Rate: {result.WinRate:F1}%");
        }

        [Fact]
        public async Task ColdStart_PM250_FullYear_ComprehensiveTesting()
        {
            // Arrange
            var loader = new PMStrategy_ColdStartLoader();
            
            Console.WriteLine("🔬 PM250 FULL YEAR COMPREHENSIVE TESTING");
            Console.WriteLine("Testing complete 2020 market year with COVID volatility");
            
            // Act
            var result = await loader.ExecuteComprehensiveTesting("PM250", 2020);
            
            // Assert
            result.Should().NotBeNull();
            result.ToolVersion.Should().Be("PM250_v2.1_ConfigurableRFib");
            result.Year.Should().Be(2020);
            result.TotalTrades.Should().BeGreaterThan(50, "Should have substantial trade count for full year");
            result.MonthlyResults.Should().HaveCount(12, "Should process all 12 months");
            result.RiskManagementEvents.Should().NotBeEmpty("Should detect 2020 market volatility events");
            result.WinRate.Should().BeInRange(60, 80, "Should maintain reasonable win rate");
            
            Console.WriteLine("✅ PM250 FULL YEAR TESTING COMPLETED");
            Console.WriteLine($"   📊 Annual Stats:");
            Console.WriteLine($"      Total Trades: {result.TotalTrades}");
            Console.WriteLine($"      Annual P&L: ${result.TotalPnL:N2}");
            Console.WriteLine($"      Win Rate: {result.WinRate:F1}%");
            Console.WriteLine($"      Max Drawdown: {result.MaxDrawdown:F1}%");
            Console.WriteLine($"      Sharpe Ratio: {result.SharpeRatio:F2}");
            Console.WriteLine($"      Risk Events: {result.RiskManagementEvents.Count}");
        }

        [Fact]
        public async Task ColdStart_PMxyz_CustomConfiguration()
        {
            // Arrange
            var loader = new PMStrategy_ColdStartLoader();
            var customConfig = new PMStrategy_ColdStartLoader.PMStrategyConfig
            {
                StrategyName = "PMxyz_Custom",
                Version = "PMxyz_v1.5_CustomTest",
                Description = "Custom PMxyz configuration for advanced testing",
                StrategyParameters = new PMStrategy_ColdStartLoader.PMStrategyParameters
                {
                    MaxPositionSize = 400,
                    RiskPerTrade = 120m,
                    DeltaTarget = 0.13,
                    WidthPoints = 2,
                    CreditRatio = 0.22m,
                    StopMultiple = 2.8m,
                    EnableRFibRiskManagement = true,
                    RFibResetThreshold = 18.0m
                }
            };
            
            Console.WriteLine("⚙️ PMxyz CUSTOM CONFIGURATION TEST");
            
            // Act
            var result = await loader.ExecuteComprehensiveTesting("PMxyz", 2020, new[] { 7, 8, 9 }, customConfig);
            
            // Assert
            result.Should().NotBeNull();
            result.ToolVersion.Should().Be("PMxyz_v1.5_CustomTest");
            result.TradeLedger.All(t => t.MaxPotentialLoss == 120m).Should().BeTrue("Should use custom risk per trade");
            result.TradeLedger.All(t => t.Strategy == "PMxyz_Custom").Should().BeTrue("Should use custom strategy name");
            
            Console.WriteLine("✅ CUSTOM CONFIGURATION TEST PASSED");
            Console.WriteLine($"   Custom Risk Per Trade: ${customConfig.StrategyParameters.RiskPerTrade}");
            Console.WriteLine($"   Custom RFib Threshold: ${customConfig.StrategyParameters.RFibResetThreshold}");
        }

        [Fact]
        public async Task ColdStart_VersionComparison_PM250vsPM300()
        {
            // Arrange
            var loader = new PMStrategy_ColdStartLoader();
            var testYear = 2021; // Use different year to avoid interference
            var testMonths = new[] { 1, 2, 3 };
            
            Console.WriteLine("⚖️ VERSION COMPARISON: PM250 vs PM300");
            
            // Act - Test both strategies on same data
            var pm250Result = await loader.ExecuteComprehensiveTesting("PM250", testYear, testMonths);
            var pm300Result = await loader.ExecuteComprehensiveTesting("PM300", testYear, testMonths);
            
            // Assert
            pm250Result.Should().NotBeNull();
            pm300Result.Should().NotBeNull();
            
            // Verify different configurations applied
            pm250Result.ToolVersion.Should().Contain("PM250");
            pm300Result.ToolVersion.Should().Contain("PM300");
            
            // PM300 should have higher risk per trade than PM250
            pm300Result.TradeLedger.First().MaxPotentialLoss.Should().BeGreaterThan(
                pm250Result.TradeLedger.First().MaxPotentialLoss, 
                "PM300 should have higher risk per trade than PM250");
            
            Console.WriteLine("✅ VERSION COMPARISON COMPLETED");
            Console.WriteLine($"   PM250 P&L: ${pm250Result.TotalPnL:N2} | Win Rate: {pm250Result.WinRate:F1}%");
            Console.WriteLine($"   PM300 P&L: ${pm300Result.TotalPnL:N2} | Win Rate: {pm300Result.WinRate:F1}%");
            
            var performanceDiff = ((pm300Result.TotalPnL - pm250Result.TotalPnL) / Math.Abs(pm250Result.TotalPnL)) * 100;
            Console.WriteLine($"   Performance Difference: {performanceDiff:+0.0;-0.0}%");
        }

        [Fact]
        public async Task ColdStart_HistoricalMarketEvents_Detection()
        {
            // Arrange
            var loader = new PMStrategy_ColdStartLoader();
            
            Console.WriteLine("🚨 HISTORICAL MARKET EVENTS DETECTION TEST");
            Console.WriteLine("Testing 2020 March COVID crash period");
            
            // Act - Test during COVID crash period
            var result = await loader.ExecuteComprehensiveTesting("PM250", 2020, new[] { 3 });
            
            // Assert
            result.Should().NotBeNull();
            result.RiskManagementEvents.Should().NotBeEmpty("Should detect market volatility events in March 2020");
            
            var highVolEvents = result.RiskManagementEvents.Where(e => 
                e.EventType == "HIGH_VOLATILITY" || e.EventType == "MARKET_CRASH").ToList();
            
            highVolEvents.Should().NotBeEmpty("Should detect high volatility or crash events in March 2020");
            
            Console.WriteLine("✅ MARKET EVENTS DETECTION VALIDATED");
            Console.WriteLine($"   Risk Events Detected: {result.RiskManagementEvents.Count}");
            
            foreach (var evt in result.RiskManagementEvents)
            {
                Console.WriteLine($"      {evt.EventTime:yyyy-MM-dd}: {evt.EventType} - {evt.Description}");
            }
        }

        [Theory]
        [InlineData("PM250", 2018, new[] { 2 })] // Volmageddon period
        [InlineData("PM300", 2008, new[] { 10 })] // Financial crisis
        [InlineData("PM500", 2020, new[] { 3, 4 })] // COVID period
        public async Task ColdStart_HistoricalVolatilityPeriods_Resilience(string strategy, int year, int[] months)
        {
            // Arrange
            var loader = new PMStrategy_ColdStartLoader();
            
            Console.WriteLine($"📈 VOLATILITY RESILIENCE TEST: {strategy} during {year} crisis");
            
            // Act
            var result = await loader.ExecuteComprehensiveTesting(strategy, year, months);
            
            // Assert
            result.Should().NotBeNull();
            result.TotalTrades.Should().BeGreaterThan(0, "Should continue trading during volatile periods");
            result.WinRate.Should().BeGreaterThan(40, "Should maintain reasonable win rate even in crisis");
            result.MaxDrawdown.Should().BeLessThan(50, "Should limit drawdown during crisis periods");
            
            Console.WriteLine($"✅ {strategy} VOLATILITY RESILIENCE VALIDATED");
            Console.WriteLine($"   Crisis Period: {year} - {string.Join(",", months)}");
            Console.WriteLine($"   Maintained Win Rate: {result.WinRate:F1}%");
            Console.WriteLine($"   Max Drawdown: {result.MaxDrawdown:F1}%");
        }
    }
}