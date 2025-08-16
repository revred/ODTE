using System;
using System.Threading.Tasks;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// Console Entry Point for PMxyz Cold Start Testing
    /// 
    /// USAGE EXAMPLES:
    /// PMStrategy_ColdStartConsole.exe PM250 2020
    /// PMStrategy_ColdStartConsole.exe PM300 2021 1,2,3
    /// PMStrategy_ColdStartConsole.exe PMxyz 2020 3,4,5
    /// </summary>
    public class PMStrategy_ColdStartConsole
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("🚀 PMxyz COLD START CONSOLE");
            Console.WriteLine("=" + new string('=', 50));
            Console.WriteLine();

            try
            {
                if (args.Length < 2)
                {
                    ShowUsage();
                    return;
                }

                var strategyName = args[0];
                var year = int.Parse(args[1]);
                int[] months = null;

                if (args.Length > 2)
                {
                    var monthsStr = args[2];
                    months = Array.ConvertAll(monthsStr.Split(','), int.Parse);
                }

                Console.WriteLine($"📋 Strategy: {strategyName}");
                Console.WriteLine($"📅 Year: {year}");
                Console.WriteLine($"📊 Months: {(months != null ? string.Join(",", months) : "ALL")}");
                Console.WriteLine();

                var loader = new PMStrategy_ColdStartLoader();
                var result = await loader.ExecuteComprehensiveTesting(strategyName, year, months);

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
                Console.WriteLine("✅ COLD START EXECUTION COMPLETED SUCCESSFULLY");

            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("❌ COLD START EXECUTION FAILED");
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine();
                ShowUsage();
                Environment.Exit(1);
            }
        }

        private static void ShowUsage()
        {
            Console.WriteLine("USAGE:");
            Console.WriteLine("  PMStrategy_ColdStartConsole.exe <strategy> <year> [months]");
            Console.WriteLine();
            Console.WriteLine("PARAMETERS:");
            Console.WriteLine("  strategy    PMxyz strategy name (PM250, PM300, PM500, PMxyz)");
            Console.WriteLine("  year        Year to test (e.g., 2020, 2021, 2022)");
            Console.WriteLine("  months      Optional: Comma-separated months (e.g., 1,2,3 for Q1)");
            Console.WriteLine("              If omitted, tests entire year");
            Console.WriteLine();
            Console.WriteLine("EXAMPLES:");
            Console.WriteLine("  PM250 2020                  # Full year 2020 comprehensive testing");
            Console.WriteLine("  PM300 2021 1,2,3            # Q1 2021 validation testing");
            Console.WriteLine("  PMxyz 2020 3,4,5            # Q1 2020 COVID period testing");
            Console.WriteLine("  PM500 2018 2                # February 2018 Volmageddon testing");
            Console.WriteLine();
            Console.WriteLine("AVAILABLE STRATEGIES:");
            Console.WriteLine("  PM250                       # Standard PM250 with $16 RFib threshold");
            Console.WriteLine("  PM300                       # Enhanced PM300 with higher capacity");
            Console.WriteLine("  PM500                       # High-capacity PM500 strategy");
            Console.WriteLine("  PMxyz                       # Generic PMxyz with default config");
            Console.WriteLine("  PM250_v2.1                  # Specific PM250 version");
            Console.WriteLine("  PM250_ConfigurableRFib      # PM250 with configurable RFib");
        }
    }
}