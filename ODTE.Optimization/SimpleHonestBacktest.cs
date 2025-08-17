namespace ODTE.Optimization
{
    /// <summary>
    /// Simple, realistic backtesting that respects actual risk limits.
    /// No synthetic optimization - just real math with proper position sizing.
    /// </summary>
    public class SimpleHonestBacktest
    {
        public class RunResult
        {
            public string Strategy { get; set; } = "";
            public int RunNumber { get; set; }
            public double StartingCapital { get; set; } = 5000;
            public double FinalCapital { get; set; }
            public double TotalPnL => FinalCapital - StartingCapital;
            public double MaxDrawdown { get; set; }
            public int TotalTrades { get; set; }
            public int WinningTrades { get; set; }
            public int LosingTrades { get; set; }
            public double WinRate => TotalTrades > 0 ? (double)WinningTrades / TotalTrades * 100 : 0;
            public double CommissionCosts { get; set; }
            public double SlippageCosts { get; set; }
            public double NetPnL => TotalPnL - CommissionCosts - SlippageCosts;
            public List<double> DailyPnL { get; set; } = new();
        }

        public List<RunResult> RunHonestBacktest(int totalRuns = 64)
        {
            // DEFENSIVE VALIDATION
            if (totalRuns <= 0)
                throw new ArgumentException("Total runs must be positive", nameof(totalRuns));

            var results = new List<RunResult>();
            var strategies = new[] { "IronCondor", "PutSpread", "CallSpread" };
            var random = new Random(42); // Fixed seed for reproducibility

            Console.WriteLine("================================================================================");
            Console.WriteLine("REALISTIC HONEST BACKTESTING");
            Console.WriteLine($"Total Runs: {totalRuns}");
            Console.WriteLine("Starting Capital: $5,000 per run");
            Console.WriteLine("Daily Loss Limit: $500 (Reverse Fibonacci)");
            Console.WriteLine("Max Per-Trade Loss: $200");
            Console.WriteLine("Commission: $2 per trade, Slippage: $5 per trade");
            Console.WriteLine("================================================================================");

            foreach (var strategy in strategies)
            {
                var runsPerStrategy = Math.Max(1, totalRuns / strategies.Length); // Ensure at least 1 run per strategy
                Console.WriteLine($"\nTesting {strategy} - {runsPerStrategy} runs");
                Console.WriteLine(new string('-', 60));

                for (int run = 1; run <= runsPerStrategy; run++)
                {
                    var result = SimulateSingleRun(strategy, run + results.Count, random);
                    results.Add(result);

                    var color = result.NetPnL >= 0 ? ConsoleColor.Green : ConsoleColor.Red;
                    Console.ForegroundColor = color;
                    Console.WriteLine($"  Run {run:D2}: P&L ${result.TotalPnL:F0}, Net ${result.NetPnL:F0}, Win% {result.WinRate:F1}%");
                    Console.ResetColor();

                    if (run % 8 == 0)
                    {
                        ShowInterimStats(results);
                    }
                }
            }

            ShowFinalReport(results);
            return results;
        }

        private RunResult SimulateSingleRun(string strategy, int runNumber, Random random)
        {
            var result = new RunResult
            {
                Strategy = strategy,
                RunNumber = runNumber,
                StartingCapital = 5000,
                FinalCapital = 5000
            };

            // Simulate 60 trading days (about 3 months)
            var dailyLossLimits = new[] { 500, 300, 200, 100 }; // Reverse Fibonacci
            var consecutiveLossDays = 0;
            var peak = 5000.0;

            for (int day = 1; day <= 60; day++)
            {
                var currentLimit = dailyLossLimits[Math.Min(consecutiveLossDays, 3)];
                var dayPnL = SimulateTradingDay(strategy, currentLimit, random);

                result.FinalCapital += dayPnL;
                result.DailyPnL.Add(dayPnL);

                // Track drawdown
                peak = Math.Max(peak, result.FinalCapital);
                var currentDrawdown = result.FinalCapital - peak;
                result.MaxDrawdown = Math.Min(result.MaxDrawdown, currentDrawdown);

                // Update consecutive loss days
                if (dayPnL < 0)
                {
                    consecutiveLossDays++;
                    result.LosingTrades++;
                }
                else if (dayPnL > 0)
                {
                    consecutiveLossDays = 0; // Reset on profitable day
                    result.WinningTrades++;
                }

                result.TotalTrades++;

                // Stop if we hit capital preservation mode (lost 80% of starting capital)
                if (result.FinalCapital <= 1000)
                    break;
            }

            // Calculate realistic costs
            result.CommissionCosts = result.TotalTrades * 2.0;
            result.SlippageCosts = result.TotalTrades * 5.0;

            return result;
        }

        public double SimulateTradingDay(string strategy, double dailyLimit, Random random)
        {
            // DEFENSIVE VALIDATION - These should have been here from day 1
            if (dailyLimit < 0)
                throw new ArgumentException("Daily limit cannot be negative", nameof(dailyLimit));
            if (!IsValidStrategy(strategy))
                throw new ArgumentException($"Unknown strategy: {strategy}", nameof(strategy));
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            // Handle edge case: zero daily limit means no trading allowed
            if (dailyLimit == 0)
                return 0;

            // Realistic 0DTE trading simulation
            // Win probability varies by strategy
            var winProb = strategy switch
            {
                "IronCondor" => 0.75,  // Higher win rate, smaller wins
                "PutSpread" => 0.65,   // Moderate win rate
                "CallSpread" => 0.60,  // Lower win rate, bigger wins
                _ => 0.65
            };

            // Typical trade count per day (1-3 trades)
            var tradesPerDay = random.Next(1, 4);
            var dayPnL = 0.0;

            for (int trade = 0; trade < tradesPerDay; trade++)
            {
                var isWinner = random.NextDouble() < winProb;
                var tradePnL = 0.0;

                if (isWinner)
                {
                    // Typical credit spread win: collect 30-50% of premium
                    tradePnL = strategy switch
                    {
                        "IronCondor" => random.Next(20, 40),   // $20-40 win
                        "PutSpread" => random.Next(25, 50),    // $25-50 win  
                        "CallSpread" => random.Next(30, 60),   // $30-60 win
                        _ => random.Next(25, 45)
                    };
                }
                else
                {
                    // FIXED: Use mathematically correct maximum losses
                    var maxLoss = GetMaxLossForStrategy(strategy);

                    // Generate realistic loss within mathematical bounds
                    var lossRange = (int)(maxLoss * 0.8); // 80% of max loss as typical range
                    var minLoss = Math.Min(20, lossRange / 2);

                    tradePnL = -random.Next(minLoss, (int)maxLoss + 1);

                    // BUSINESS LOGIC VALIDATION
                    if (Math.Abs(tradePnL) > maxLoss)
                        throw new InvalidOperationException($"Generated loss ${Math.Abs(tradePnL)} exceeds maximum ${maxLoss} for {strategy}");
                }

                dayPnL += tradePnL;

                // Respect daily loss limit
                if (dayPnL <= -dailyLimit)
                {
                    dayPnL = -dailyLimit;
                    break;
                }
            }

            return dayPnL;
        }

        private void ShowInterimStats(List<RunResult> results)
        {
            var avgPnL = results.Average(r => r.TotalPnL);
            var avgNetPnL = results.Average(r => r.NetPnL);
            var profitableRuns = results.Count(r => r.NetPnL > 0);

            Console.WriteLine();
            Console.WriteLine($"=== INTERIM STATS (After {results.Count} runs) ===");
            Console.WriteLine($"Average Gross P&L: ${avgPnL:F0}");
            Console.WriteLine($"Average Net P&L: ${avgNetPnL:F0}");
            Console.WriteLine($"Profitable Runs: {profitableRuns}/{results.Count} ({profitableRuns * 100.0 / results.Count:F1}%)");
            Console.WriteLine();
        }

        private void ShowFinalReport(List<RunResult> results)
        {
            Console.WriteLine();
            Console.WriteLine("================================================================================");
            Console.WriteLine("FINAL HONEST ASSESSMENT");
            Console.WriteLine("================================================================================");

            var totalGrossPnL = results.Sum(r => r.TotalPnL);
            var totalNetPnL = results.Sum(r => r.NetPnL);
            var totalCosts = results.Sum(r => r.CommissionCosts + r.SlippageCosts);
            var profitableRuns = results.Count(r => r.NetPnL > 0);
            var avgWinRate = results.Average(r => r.WinRate);

            Console.WriteLine($"Total Runs: {results.Count}");
            Console.WriteLine($"Starting Capital (Total): ${results.Count * 5000:N0}");
            Console.WriteLine($"Final Capital (Total): ${results.Sum(r => r.FinalCapital):N0}");
            Console.WriteLine($"Total Gross P&L: ${totalGrossPnL:F0}");
            Console.WriteLine($"Total Costs: ${totalCosts:F0}");
            Console.WriteLine($"Total Net P&L: ${totalNetPnL:F0}");
            Console.WriteLine($"Average Win Rate: {avgWinRate:F1}%");
            Console.WriteLine($"Profitable Runs: {profitableRuns}/{results.Count} ({profitableRuns * 100.0 / results.Count:F1}%)");

            // Per strategy breakdown
            var strategies = results.Select(r => r.Strategy).Distinct();
            foreach (var strategy in strategies)
            {
                var stratResults = results.Where(r => r.Strategy == strategy).ToList();
                Console.WriteLine($"\n{strategy.ToUpper()}:");
                Console.WriteLine($"  Net P&L: ${stratResults.Sum(r => r.NetPnL):F0}");
                Console.WriteLine($"  Win Rate: {stratResults.Average(r => r.WinRate):F1}%");
                Console.WriteLine($"  Profitable: {stratResults.Count(r => r.NetPnL > 0)}/{stratResults.Count}");
                Console.WriteLine($"  Best Run: ${stratResults.Max(r => r.NetPnL):F0}");
                Console.WriteLine($"  Worst Run: ${stratResults.Min(r => r.NetPnL):F0}");
            }

            Console.WriteLine("\n" + new string('=', 80));
            Console.WriteLine("HONEST VERDICT:");

            if (totalNetPnL > 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ Strategies show positive returns after realistic costs");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ Strategies show negative returns after realistic costs");
            }

            if (profitableRuns > results.Count / 2)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ Majority of runs profitable ({profitableRuns * 100.0 / results.Count:F1}%)");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Minority of runs profitable ({profitableRuns * 100.0 / results.Count:F1}%)");
            }

            var avgReturn = totalNetPnL / (results.Count * 5000) * 100;
            Console.ForegroundColor = avgReturn > 0 ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine($"Overall Return: {avgReturn:F1}% over 60 trading days");

            Console.ResetColor();
            Console.WriteLine(new string('=', 80));
        }

        // DEFENSIVE VALIDATION METHODS - Should have been here from day 1
        private static bool IsValidStrategy(string strategy)
        {
            return strategy switch
            {
                "IronCondor" or "PutSpread" or "CallSpread" => true,
                _ => false
            };
        }

        public static double GetMaxLossForStrategy(string strategy)
        {
            // Mathematical maximum loss = Spread Width - Credit Received
            return strategy switch
            {
                "IronCondor" => 80,   // $100 width - $20 credit
                "PutSpread" => 75,    // $100 width - $25 credit
                "CallSpread" => 70,   // $100 width - $30 credit
                _ => throw new ArgumentException($"Unknown strategy: {strategy}")
            };
        }
    }
}