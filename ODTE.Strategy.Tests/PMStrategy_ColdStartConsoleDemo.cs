using System;
using System.Threading.Tasks;
using Xunit;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// Demonstration of console entry point functionality
    /// </summary>
    public class PMStrategy_ColdStartConsoleDemo
    {
        [Fact]
        public async Task Console_PM250_SingleMonth_Demo()
        {
            // Arrange - Simulate console arguments
            var args = new[] { "PM250", "2020", "3" };
            
            Console.WriteLine("🎯 COLD START CONSOLE DEMO");
            Console.WriteLine("=" + new string('=', 50));
            Console.WriteLine($"Simulating: PMStrategy_ColdStartConsole.exe {string.Join(" ", args)}");
            Console.WriteLine();

            // Act - Execute the same logic as console application
            var strategyName = args[0];
            var year = int.Parse(args[1]);
            var months = new[] { int.Parse(args[2]) };

            Console.WriteLine($"📋 Strategy: {strategyName}");
            Console.WriteLine($"📅 Year: {year}");
            Console.WriteLine($"📊 Months: {string.Join(",", months)}");
            Console.WriteLine();

            var loader = new PMStrategy_ColdStartLoader();
            var result = await loader.ExecuteComprehensiveTesting(strategyName, year, months);

            // Assert & Display Results
            Console.WriteLine();
            Console.WriteLine("🎯 EXECUTION SUMMARY");
            Console.WriteLine("-" + new string('-', 30));
            Console.WriteLine($"Strategy Version: {result.ToolVersion}");
            Console.WriteLine($"Test Period: {result.Year}");
            Console.WriteLine($"Total Trades: {result.TotalTrades}");
            Console.WriteLine($"Total P&L: ${result.TotalPnL:N2}");
            Console.WriteLine($"Win Rate: {result.WinRate:F1}%");
            Console.WriteLine($"Average Trade: ${result.AverageTradeProfit:N2}");
            Console.WriteLine($"Max Drawdown: {result.MaxDrawdown:F1}%");
            Console.WriteLine($"Sharpe Ratio: {result.SharpeRatio:F2}");
            Console.WriteLine($"Risk Events: {result.RiskManagementEvents.Count}");
            Console.WriteLine($"Results File: {result.ResultsFilePath}");
            Console.WriteLine();
            Console.WriteLine("✅ CONSOLE DEMO COMPLETED SUCCESSFULLY");

            // Verify console functionality works as expected
            Assert.NotNull(result);
            Assert.Contains("PM250", result.ToolVersion);
            Assert.Equal(2020, result.Year);
            Assert.True(result.TotalTrades > 0);
            Assert.NotEmpty(result.ResultsFilePath);
        }

        [Theory]
        [InlineData("PM250", "2020")]
        [InlineData("PM300", "2021")]
        [InlineData("PMxyz", "2020")]
        public async Task Console_MultipleStrategies_YearlyDemo(string strategy, string year)
        {
            // Arrange - Simulate full year console execution
            var args = new[] { strategy, year };
            
            Console.WriteLine($"🔬 FULL YEAR CONSOLE DEMO: {strategy} {year}");
            Console.WriteLine("-" + new string('-', 40));

            // Act - Execute full year testing as console would
            var loader = new PMStrategy_ColdStartLoader();
            var result = await loader.ExecuteComprehensiveTesting(strategy, int.Parse(year));

            // Assert & Display Summary
            Console.WriteLine($"✅ {strategy} {year} COMPLETED");
            Console.WriteLine($"   Trades: {result.TotalTrades} | P&L: ${result.TotalPnL:N2} | Win Rate: {result.WinRate:F1}%");
            Console.WriteLine($"   Results: {result.ResultsFilePath}");
            Console.WriteLine();

            // Verify console functionality
            Assert.NotNull(result);
            Assert.Contains(strategy, result.ToolVersion);
            Assert.Equal(int.Parse(year), result.Year);
            Assert.Equal(12, result.MonthlyResults.Count); // Full year = 12 months
            Assert.True(result.TotalTrades > 0);
        }

        [Fact]
        public async Task Console_CustomConfiguration_Demo()
        {
            Console.WriteLine("⚙️ CUSTOM CONFIGURATION CONSOLE DEMO");
            Console.WriteLine("-" + new string('-', 40));

            // Demonstrate custom configuration usage (as console would with config file)
            var customConfig = new PMStrategy_ColdStartLoader.PMStrategyConfig
            {
                StrategyName = "PM250_Custom",
                Version = "PM250_v2.1_CustomDemo",
                Description = "Custom PM250 for console demo",
                StrategyParameters = new PMStrategy_ColdStartLoader.PMStrategyParameters
                {
                    MaxPositionSize = 300,
                    RiskPerTrade = 90m,
                    DeltaTarget = 0.12,
                    WidthPoints = 2,
                    CreditRatio = 0.25m,
                    StopMultiple = 2.5m,
                    EnableRFibRiskManagement = true,
                    RFibResetThreshold = 20.0m
                }
            };

            var loader = new PMStrategy_ColdStartLoader();
            var result = await loader.ExecuteComprehensiveTesting("PM250", 2020, new[] { 7, 8 }, customConfig);

            Console.WriteLine($"✅ CUSTOM CONFIG DEMO COMPLETED");
            Console.WriteLine($"   Version: {result.ToolVersion}");
            Console.WriteLine($"   Custom Risk: ${customConfig.StrategyParameters.RiskPerTrade}");
            Console.WriteLine($"   Custom RFib: ${customConfig.StrategyParameters.RFibResetThreshold}");
            Console.WriteLine();

            Assert.Equal("PM250_v2.1_CustomDemo", result.ToolVersion);
            Assert.True(result.TradeLedger.All(t => t.MaxPotentialLoss == 90m));
        }
    }
}