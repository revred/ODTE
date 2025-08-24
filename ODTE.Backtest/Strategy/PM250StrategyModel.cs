using ODTE.Backtest.Config;
using ODTE.Backtest.Data;
using ODTE.Contracts.Data;
using ODTE.Contracts.Historical;

namespace ODTE.Backtest.Strategy;

/// <summary>
/// PM250 High-Frequency Strategy Model - Unified Implementation
/// WHY: Implements the Profit Machine 250 high-frequency options trading strategy through unified interface
/// ACHIEVEMENT: Target >90% win rate, 250 trades/week, 6-minute separation, GoScore optimization
/// </summary>
[StrategyModelName("PM250")]
public class PM250StrategyModel : IStrategyModel
{
    public string ModelName { get; } = "PM250";
    public string ModelVersion { get; set; } = "v1.0";

    // PM250 High-Frequency Configuration Parameters
    private const int MAX_TRADES_PER_WEEK = 250;
    private const int MAX_TRADES_PER_DAY = 50; // 250/5 trading days
    private const int MIN_SEPARATION_MINUTES = 6;
    private const double MIN_GOSCORE_THRESHOLD = 75.0; // Higher threshold for quality
    private const decimal TARGET_PROFIT_PER_TRADE = 25.0m; // Slightly lower for volume
    private const decimal MAX_SINGLE_LOSS = 15.0m; // Tighter risk control
    private const decimal MAX_DAILY_DRAWDOWN = 75.0m; // 3x single loss limit

    // Strategy State
    private readonly StrategyConfig _strategyConfig;
    private SimConfig? _config;
    private IMarketData? _marketData;
    private IOptionsData? _optionsData;
    private readonly List<TradeExecution> _recentTrades;
    private readonly Random _random;

    // Risk Management
    private readonly decimal[] _revFibLevels = { 1250m, 800m, 500m, 300m, 200m, 100m };
    private int _currentRevFibIndex = 2; // Start at $500

    public PM250StrategyModel(StrategyConfig strategyConfig)
    {
        _strategyConfig = strategyConfig ?? throw new ArgumentNullException(nameof(strategyConfig));
        _recentTrades = new List<TradeExecution>();
        _random = new Random();
    }

    public async Task InitializeAsync(SimConfig config, IMarketData marketData, IOptionsData optionsData)
    {
        _config = config;
        _marketData = marketData;
        _optionsData = optionsData;
        
        Console.WriteLine($"🎯 Initializing PM250 {ModelVersion}");
        Console.WriteLine($"⚡ Profit Machine 250: High-Frequency Optimal Strategy");
        Console.WriteLine($"📊 Target: 250 trades/week, >90% win rate, 6-minute separation");
        Console.WriteLine($"🎯 GoScore Threshold: {MIN_GOSCORE_THRESHOLD}, Daily Drawdown Limit: ${MAX_DAILY_DRAWDOWN}");
        Console.WriteLine($"💰 Target Profit/Trade: ${TARGET_PROFIT_PER_TRADE}, Max Loss: ${MAX_SINGLE_LOSS}");
        Console.WriteLine($"🛡️ Rev Fib Levels: [{string.Join(", ", _revFibLevels)}], Starting: ${_revFibLevels[_currentRevFibIndex]}");
        
        await Task.CompletedTask;
    }

    public async Task<List<CandidateOrder>> GenerateSignalsAsync(DateTime timestamp, MarketDataBar currentBar, PortfolioState portfolio)
    {
        var signals = new List<CandidateOrder>();
        
        try
        {
            // PM250 High-Frequency Trade Timing Validation
            if (!IsValidTradeOpportunity(timestamp))
                return signals;

            // Smart Anti-Risk Pre-screening
            if (!await PassesSmartAntiRisk(timestamp, currentBar, portfolio))
                return signals;

            // GoScore Quality Optimization
            var goScore = CalculateGoScore(currentBar, timestamp);
            if (goScore < MIN_GOSCORE_THRESHOLD)
                return signals;

            // Generate high-frequency iron condor signal
            var entrySignal = await GenerateHighFrequencyIronCondorSignal(timestamp, currentBar, portfolio, goScore);
            if (entrySignal != null)
            {
                signals.Add(entrySignal);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ PM250 signal generation error: {ex.Message}");
        }
        
        return signals;
    }

    public async Task<List<CandidateOrder>> ManagePositionsAsync(DateTime timestamp, MarketDataBar currentBar, PortfolioState portfolio)
    {
        var managementSignals = new List<CandidateOrder>();
        
        try
        {
            // Manage existing PM250 positions
            foreach (var position in portfolio.OpenPositions.Where(p => p.StrategyType == "PM250_HighFreq"))
            {
                var exitSignal = await CheckPositionExit(position, timestamp, currentBar, portfolio);
                if (exitSignal != null)
                {
                    managementSignals.Add(exitSignal);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ PM250 position management error: {ex.Message}");
        }
        
        return managementSignals;
    }

    private bool IsValidTradeOpportunity(DateTime timestamp)
    {
        // Check daily trade limit
        var todaysTrades = _recentTrades.Count(t => t.ExecutionTime.Date == timestamp.Date);
        if (todaysTrades >= MAX_TRADES_PER_DAY)
            return false;

        // Check weekly trade limit
        var weekStart = timestamp.AddDays(-(int)timestamp.DayOfWeek);
        var weekTrades = _recentTrades.Count(t => t.ExecutionTime >= weekStart);
        if (weekTrades >= MAX_TRADES_PER_WEEK)
            return false;

        // Check minimum separation (6 minutes)
        var lastTrade = _recentTrades.LastOrDefault();
        if (lastTrade != null)
        {
            var timeSinceLastTrade = timestamp - lastTrade.ExecutionTime;
            if (timeSinceLastTrade.TotalMinutes < MIN_SEPARATION_MINUTES)
                return false;
        }

        // Check trading hours (focus on high-volume periods)
        var hour = timestamp.Hour;
        var isValidHour = (hour >= 9 && hour <= 11) ||  // Morning session
                         (hour >= 13 && hour <= 15);     // Afternoon session

        return isValidHour;
    }

    private async Task<bool> PassesSmartAntiRisk(DateTime timestamp, MarketDataBar currentBar, PortfolioState portfolio)
    {
        // 1. Extreme volatility check (simplified for backtest)
        if (currentBar.Volume == 0 || currentBar.High - currentBar.Low > currentBar.Close * 0.05m)
            return false;

        // 2. Daily drawdown protection
        var todaysLoss = _recentTrades
            .Where(t => t.ExecutionTime.Date == timestamp.Date && t.PnL < 0)
            .Sum(t => t.PnL);

        if (Math.Abs(todaysLoss) > MAX_DAILY_DRAWDOWN)
            return false;

        // 3. Consecutive loss pattern analysis
        var recentLosses = _recentTrades.TakeLast(5).Count(t => t.PnL < 0);
        if (recentLosses >= 3)
            return false;

        await Task.CompletedTask;
        return true;
    }

    private double CalculateGoScore(MarketDataBar currentBar, DateTime timestamp)
    {
        // Simplified GoScore calculation for high-frequency trading
        var baseScore = 50.0;

        // Volume contribution (30% weight)
        var volumeScore = currentBar.Volume > 1000000 ? 85.0 : 60.0;
        baseScore += (volumeScore - 50) * 0.3;

        // Time of day contribution (25% weight)
        var hour = timestamp.Hour;
        var timeScore = (hour >= 10 && hour <= 14) ? 85.0 : 65.0;
        baseScore += (timeScore - 50) * 0.25;

        // Price stability contribution (20% weight)
        var priceRange = (double)(currentBar.High - currentBar.Low) / (double)currentBar.Close;
        var stabilityScore = priceRange < 0.01 ? 90.0 : priceRange < 0.02 ? 75.0 : 55.0;
        baseScore += (stabilityScore - 50) * 0.2;

        // Market regime contribution (25% weight) - simplified
        var regimeScore = 75.0; // Assume mixed market for backtest
        baseScore += (regimeScore - 50) * 0.25;

        // Add some realistic variance
        baseScore += (_random.NextDouble() - 0.5) * 10.0;

        return Math.Max(0, Math.Min(100, baseScore));
    }

    private async Task<CandidateOrder?> GenerateHighFrequencyIronCondorSignal(DateTime timestamp, MarketDataBar currentBar, PortfolioState portfolio, double goScore)
    {
        // Calculate current Rev Fib position size
        var currentPositionSize = _revFibLevels[_currentRevFibIndex];
        
        // GoScore enhancement for position sizing
        var goScoreMultiplier = goScore > 85 ? 1.4m :
                               goScore > 80 ? 1.2m :
                               1.0m;

        var adjustedSize = currentPositionSize * goScoreMultiplier;
        
        var orderId = $"PM250_HighFreq_{timestamp:yyyyMMdd_HHmmss}";
        
        var signal = new CandidateOrder
        {
            OrderId = orderId,
            Symbol = "SPY",
            StrategyType = "PM250_HighFreq",
            OrderType = OrderType.Spread,
            MaxRisk = (int)(adjustedSize * 0.8m), // Risk cap based on Rev Fib level
            ExpectedCredit = (int)(TARGET_PROFIT_PER_TRADE * (adjustedSize / 500m)), // Scale with position size
            ExpirationDate = GetNextFridayExpiry(timestamp),
            EntryReason = "PM250_high_frequency_entry",
            Metadata = new Dictionary<string, object>
            {
                {"entry_reason", "PM250_high_frequency_entry"},
                {"go_score", goScore},
                {"position_size", adjustedSize},
                {"rev_fib_level", _currentRevFibIndex},
                {"target_profit", TARGET_PROFIT_PER_TRADE},
                {"max_loss", MAX_SINGLE_LOSS},
                {"trades_today", _recentTrades.Count(t => t.ExecutionTime.Date == timestamp.Date)}
            }
        };

        Console.WriteLine($"⚡ PM250 High-Freq Signal: Iron Condor @ {timestamp:HH:mm} (GoScore: {goScore:F1}, Size: ${adjustedSize}, Risk: ${signal.MaxRisk}$)");
        return signal;
    }

    private async Task<CandidateOrder?> CheckPositionExit(Position position, DateTime timestamp, MarketDataBar currentBar, PortfolioState portfolio)
    {
        var currentPnL = position.UnrealizedPnL;
        var entryCredit = Math.Abs(position.UnrealizedPnL); // Approximate entry credit
        
        // PM250 Exit Rules
        var stopLossThreshold = -MAX_SINGLE_LOSS;
        var profitTargetThreshold = TARGET_PROFIT_PER_TRADE * 0.7m; // Take 70% of target
        
        string exitReason = "";
        
        if (currentPnL <= stopLossThreshold)
        {
            exitReason = $"stop_loss_${MAX_SINGLE_LOSS}";
            UpdateRevFibLevel(false); // Move to more conservative level
        }
        else if (currentPnL >= profitTargetThreshold)
        {
            exitReason = $"profit_target_${profitTargetThreshold:F0}";
            UpdateRevFibLevel(true); // Move to more aggressive level on success
        }
        // Time-based exit for 0DTE (simplified)
        else if (timestamp.Hour >= 15 && (position.ExpirationDate.Date == timestamp.Date))
        {
            exitReason = "time_exit_0dte";
        }
        
        if (!string.IsNullOrEmpty(exitReason))
        {
            Console.WriteLine($"🔚 PM250 Exit: {exitReason} (P&L: ${currentPnL:F2}, Rev Fib: ${_revFibLevels[_currentRevFibIndex]})");
            
            // Record trade execution for future reference
            _recentTrades.Add(new TradeExecution
            {
                ExecutionTime = timestamp,
                PnL = currentPnL,
                Success = currentPnL > 0,
                Strategy = "PM250_HighFreq"
            });
            
            // Maintain rolling window
            if (_recentTrades.Count > 1000)
            {
                _recentTrades.RemoveRange(0, 200);
            }
            
            return new CandidateOrder
            {
                OrderId = $"EXIT_{position.PositionId}",
                Symbol = position.Symbol,
                StrategyType = "EXIT",
                OrderType = OrderType.Market,
                MaxRisk = 0,
                EntryReason = exitReason,
                Metadata = new Dictionary<string, object>
                {
                    {"exit_reason", exitReason},
                    {"original_position_id", position.PositionId},
                    {"pnl", currentPnL},
                    {"rev_fib_level", _currentRevFibIndex}
                }
            };
        }
        
        await Task.CompletedTask;
        return null;
    }

    private void UpdateRevFibLevel(bool wasProfit)
    {
        if (wasProfit)
        {
            // Move to more aggressive level (lower index = higher position size)
            _currentRevFibIndex = Math.Max(0, _currentRevFibIndex - 1);
        }
        else
        {
            // Move to more conservative level (higher index = lower position size)
            _currentRevFibIndex = Math.Min(_revFibLevels.Length - 1, _currentRevFibIndex + 1);
        }
    }

    private DateTime GetNextFridayExpiry(DateTime currentDate)
    {
        var daysUntilFriday = ((int)DayOfWeek.Friday - (int)currentDate.DayOfWeek + 7) % 7;
        if (daysUntilFriday == 0 && currentDate.DayOfWeek == DayOfWeek.Friday)
            daysUntilFriday = 7; // Next Friday if it's already Friday
            
        return currentDate.AddDays(daysUntilFriday);
    }

    public Dictionary<string, object> GetModelParameters()
    {
        return new Dictionary<string, object>
        {
            {"model_name", ModelName},
            {"model_version", ModelVersion},
            {"strategy_type", "high_frequency_iron_condor"},
            {"max_trades_per_week", MAX_TRADES_PER_WEEK},
            {"max_trades_per_day", MAX_TRADES_PER_DAY},
            {"min_separation_minutes", MIN_SEPARATION_MINUTES},
            {"min_goscore_threshold", MIN_GOSCORE_THRESHOLD},
            {"target_profit_per_trade", TARGET_PROFIT_PER_TRADE},
            {"max_single_loss", MAX_SINGLE_LOSS},
            {"max_daily_drawdown", MAX_DAILY_DRAWDOWN},
            {"expected_win_rate", 0.90},
            {"expected_edge", 0.96},
            {"reward_to_risk_ratio", 2.5},
            {"rev_fib_levels", string.Join(",", _revFibLevels)},
            {"current_rev_fib_index", _currentRevFibIndex}
        };
    }

    public void ValidateConfiguration(SimConfig config)
    {
        // PM250 Configuration Validation
        if (config.Underlying != "SPY" && config.Underlying != "SPX")
        {
            throw new ArgumentException($"PM250 model requires SPY or SPX underlying, got: {config.Underlying}");
        }
        
        if ((config.End.ToDateTime(TimeOnly.MinValue) - config.Start.ToDateTime(TimeOnly.MinValue)).Days < 30)
        {
            throw new ArgumentException("PM250 model requires minimum 30 days backtest period for high-frequency validation");
        }
        
        Console.WriteLine("✅ PM250 configuration validation passed");
    }

    public void Dispose()
    {
        // Cleanup resources if needed
    }
}

// Supporting classes for PM250
public class TradeExecution
{
    public DateTime ExecutionTime { get; set; }
    public decimal PnL { get; set; }
    public bool Success { get; set; }
    public string Strategy { get; set; } = "";
}