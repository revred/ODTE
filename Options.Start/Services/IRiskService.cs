namespace Options.Start.Services;

public interface IRiskService
{
    Task<int> GetCurrentRiskLevelAsync();
    Task<RiskStatus> GetRiskStatusAsync();
    Task<List<RiskEvent>> GetRiskHistoryAsync();
    Task<bool> UpdateRiskLevelAsync(double dailyPnL);
    Task<RiskLimits> GetCurrentLimitsAsync();
}

public class RiskService : IRiskService
{
    private readonly List<double> _fibonacciSeries = new() { 500, 300, 200, 100 };
    private int _currentLevel = 0; // Start at $500
    
    public async Task<int> GetCurrentRiskLevelAsync()
    {
        await Task.Delay(50);
        return _currentLevel;
    }

    public async Task<RiskStatus> GetRiskStatusAsync()
    {
        await Task.Delay(100);
        var currentLimit = _fibonacciSeries[_currentLevel];
        
        return new RiskStatus
        {
            CurrentLevel = _currentLevel,
            CurrentLimit = currentLimit,
            DailyPnL = -75.50, // Placeholder
            DailyLossUsed = 75.50,
            DailyLossRemaining = currentLimit - 75.50,
            MaxDrawdownToday = -125.75,
            ConsecutiveLossDays = _currentLevel,
            LastRiskUpdate = DateTime.UtcNow.AddHours(-2)
        };
    }

    public async Task<List<RiskEvent>> GetRiskHistoryAsync()
    {
        await Task.Delay(100);
        return new List<RiskEvent>
        {
            new RiskEvent
            {
                Timestamp = DateTime.UtcNow.AddDays(-1),
                EventType = "RISK_LEVEL_CHANGE",
                PreviousLevel = 0,
                NewLevel = 1,
                Reason = "Daily loss limit breached: -$525.75",
                DailyPnL = -525.75
            },
            new RiskEvent
            {
                Timestamp = DateTime.UtcNow.AddDays(-2),
                EventType = "RISK_LEVEL_CHANGE", 
                PreviousLevel = 1,
                NewLevel = 0,
                Reason = "Profitable day: +$125.50",
                DailyPnL = 125.50
            }
        };
    }

    public async Task<bool> UpdateRiskLevelAsync(double dailyPnL)
    {
        await Task.Delay(100);
        
        if (dailyPnL > 0)
        {
            // Reset to level 0 on profitable day
            _currentLevel = 0;
            return true;
        }
        else if (Math.Abs(dailyPnL) >= _fibonacciSeries[_currentLevel])
        {
            // Move to next risk level on loss limit breach
            _currentLevel = Math.Min(_currentLevel + 1, _fibonacciSeries.Count - 1);
            return true;
        }
        
        return false;
    }

    public async Task<RiskLimits> GetCurrentLimitsAsync()
    {
        await Task.Delay(50);
        return new RiskLimits
        {
            DailyLossLimit = _fibonacciSeries[_currentLevel],
            MaxPositions = 3,
            MaxPositionSize = 1,
            PositionSizePercent = 0.02, // 2% of account
            MaxDrawdownPercent = 0.05   // 5% max drawdown
        };
    }
}

public class RiskStatus
{
    public int CurrentLevel { get; set; }
    public double CurrentLimit { get; set; }
    public double DailyPnL { get; set; }
    public double DailyLossUsed { get; set; }
    public double DailyLossRemaining { get; set; }
    public double MaxDrawdownToday { get; set; }
    public int ConsecutiveLossDays { get; set; }
    public DateTime LastRiskUpdate { get; set; }
}

public class RiskEvent
{
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int PreviousLevel { get; set; }
    public int NewLevel { get; set; }
    public string Reason { get; set; } = string.Empty;
    public double DailyPnL { get; set; }
}

public class RiskLimits
{
    public double DailyLossLimit { get; set; }
    public int MaxPositions { get; set; }
    public int MaxPositionSize { get; set; }
    public double PositionSizePercent { get; set; }
    public double MaxDrawdownPercent { get; set; }
}