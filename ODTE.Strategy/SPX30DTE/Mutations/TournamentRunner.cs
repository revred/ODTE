namespace ODTE.Strategy.SPX30DTE.Mutations
{
    public class TournamentRunner
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("🏆 SPX30DTE Tournament System - 16 Mutations Battle");
            Console.WriteLine("════════════════════════════════════════════════════");
            Console.WriteLine();

            try
            {
                // Initialize data manager and fill engine
                var dataManager = new DistributedDatabaseManager();
                var fillEngine = new RealisticFillEngine();

                var tournament = new SPX30DTEMutationTournament(dataManager, fillEngine);

                Console.WriteLine("🚀 Starting 20-year tournament with 16 mutations...");
                Console.WriteLine($"⏰ Start Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine();

                var results = await tournament.RunTournament(
                    startDate: new DateTime(2005, 1, 1),
                    endDate: new DateTime(2025, 1, 1)
                );

                Console.WriteLine("🎉 Tournament Completed Successfully!");
                Console.WriteLine($"⏰ End Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine();

                DisplayTournamentResults(results);

                Console.WriteLine();
                Console.WriteLine("📊 SQLite ledgers have been generated for top 4 performers:");
                foreach (var result in results.Take(4))
                {
                    Console.WriteLine($"  🗄️  {result.MutationName}_Ledger_{DateTime.Now:yyyyMMdd}.db");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Tournament failed: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   Inner: {ex.InnerException.Message}");
                }
                Console.WriteLine();
                Console.WriteLine("Stack trace:");
                Console.WriteLine(ex.StackTrace);
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        private static void DisplayTournamentResults(List<MutationResult> results)
        {
            Console.WriteLine("🏆 FINAL TOURNAMENT RANKINGS");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
            Console.WriteLine($"{"Rank",-4} {"Mutation",-20} {"CAGR",-8} {"MaxDD",-8} {"WinRate",-8} {"Score",-8}");
            Console.WriteLine("───────────────────────────────────────────────────────────────────────");

            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                var trophy = i switch
                {
                    0 => "🥇",
                    1 => "🥈",
                    2 => "🥉",
                    _ => "  "
                };

                Console.WriteLine($"{trophy} {i + 1,-2} {result.MutationName,-20} " +
                                $"{result.CAGR:P1,-8} {result.MaxDrawdown:P1,-8} " +
                                $"{result.WinRate:P1,-8} {result.OverallScore:F2,-8}");
            }

            Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
            Console.WriteLine();

            // Display top 4 detailed results
            Console.WriteLine("🎯 TOP 4 DETAILED ANALYSIS");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════");

            for (int i = 0; i < Math.Min(4, results.Count); i++)
            {
                var result = results[i];
                Console.WriteLine($"🏅 #{i + 1}: {result.MutationName}");
                Console.WriteLine($"   📈 CAGR: {result.CAGR:P2} | 📉 Max Drawdown: {result.MaxDrawdown:P2}");
                Console.WriteLine($"   🎯 Win Rate: {result.WinRate:P1} | 💰 Final Value: ${result.FinalValue:N0}");
                Console.WriteLine($"   📊 Profit Factor: {result.ProfitFactor:F2} | 📐 Sharpe: {result.SharpeRatio:F2}");
                Console.WriteLine($"   🎲 Total Trades: {result.TotalTrades} | 🔥 Overall Score: {result.OverallScore:F2}");
                Console.WriteLine();
            }
        }
    }
}