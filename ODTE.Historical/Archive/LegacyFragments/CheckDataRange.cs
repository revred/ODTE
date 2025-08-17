using ODTE.Historical;

class CheckDataRange
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("📊 Checking available data range in SQLite database...");

        using var dataManager = new HistoricalDataManager();
        await dataManager.InitializeAsync();

        var stats = await dataManager.GetStatsAsync();
        Console.WriteLine($"Start Date: {stats.StartDate:yyyy-MM-dd}");
        Console.WriteLine($"End Date: {stats.EndDate:yyyy-MM-dd}");
        Console.WriteLine($"Total Records: {stats.TotalRecords:N0}");
        Console.WriteLine($"Database Size: {stats.DatabaseSizeMB:F1} MB");

        // Check specifically for 2021 data
        var symbols = await dataManager.GetAvailableSymbolsAsync();
        Console.WriteLine($"Available symbols: {string.Join(", ", symbols)}");

        // Try to get some sample data from 2021
        try
        {
            var jan2021Data = await dataManager.GetMarketDataAsync("XSP", new DateTime(2021, 1, 1), new DateTime(2021, 1, 31));
            Console.WriteLine($"January 2021 data: {jan2021Data?.Count ?? 0} records");

            var feb2021Data = await dataManager.GetMarketDataAsync("XSP", new DateTime(2021, 2, 1), new DateTime(2021, 2, 28));
            Console.WriteLine($"February 2021 data: {feb2021Data?.Count ?? 0} records");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting 2021 data: {ex.Message}");
        }
    }
}