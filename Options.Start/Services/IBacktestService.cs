// using ODTE.Backtest.Config; // Will be integrated later

namespace Options.Start.Services;

public interface IBacktestService
{
    Task<BacktestResult> RunBacktestAsync(BacktestRequest request);
    Task<List<BacktestResult>> GetBacktestHistoryAsync();
    Task<BacktestResult?> GetBacktestResultAsync(string resultId);
    Task<bool> IsBacktestRunningAsync();
    Task StopBacktestAsync();
}

public class BacktestService : IBacktestService
{
    public async Task<BacktestResult> RunBacktestAsync(BacktestRequest request)
    {
        await Task.Delay(100);
        // Placeholder - would integrate with ODTE.Backtest engine
        throw new NotImplementedException("Integration with ODTE.Backtest pending");
    }

    public async Task<List<BacktestResult>> GetBacktestHistoryAsync()
    {
        await Task.Delay(100);
        return new List<BacktestResult>(); // Placeholder
    }

    public async Task<BacktestResult?> GetBacktestResultAsync(string resultId)
    {
        await Task.Delay(100);
        return null; // Placeholder
    }

    public async Task<bool> IsBacktestRunningAsync()
    {
        await Task.Delay(50);
        return false; // Placeholder
    }

    public async Task StopBacktestAsync()
    {
        await Task.Delay(100);
        // Placeholder
    }
}

public class BacktestRequest
{
    public object Config { get; set; } = new(); // SimConfig placeholder
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string StrategyVersion { get; set; } = string.Empty;
}

public class BacktestResult
{
    public string Id { get; set; } = string.Empty;
    public string StrategyVersion { get; set; } = string.Empty;
    public DateTime RunDate { get; set; }
    public PerformanceMetrics Performance { get; set; } = new();
    public List<Trade> Trades { get; set; } = new();
}

public class Trade
{
    public string Id { get; set; } = string.Empty;
    public DateTime OpenTime { get; set; }
    public DateTime CloseTime { get; set; }
    public string Underlying { get; set; } = string.Empty;
    public string Strategy { get; set; } = string.Empty;
    public double PnL { get; set; }
    public double Delta { get; set; }
    public int DaysToExpiry { get; set; }
}