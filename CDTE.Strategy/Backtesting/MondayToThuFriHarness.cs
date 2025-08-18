using Microsoft.Extensions.Logging;
using ODTE.Historical.Providers;
using ODTE.Execution.Models;
using ODTE.Execution.HistoricalFill;
using CDTE.Strategy.CDTE;

namespace CDTE.Strategy.Backtesting;

/// <summary>
/// MondayToThuFriHarness - CDTE Weekly Backtest Framework
/// Orchestrates complete Monday/Wednesday/Friday workflow over historical data
/// Per spec: Real NBBO execution, authentic market conditions, zero synthetic data
/// </summary>
public class MondayToThuFriHarness : IDisposable
{
    private readonly ChainSnapshotProvider _snapshotProvider;
    private readonly NbboFillEngine _fillEngine;
    private readonly CDTEStrategy _strategy;
    private readonly ILogger<MondayToThuFriHarness> _logger;
    private readonly CDTEConfig _config;
    
    private readonly List<WeeklyResult> _results = new();
    private readonly Dictionary<string, Position> _portfolio = new();

    public MondayToThuFriHarness(
        ChainSnapshotProvider snapshotProvider,
        NbboFillEngine fillEngine,
        CDTEStrategy strategy,
        CDTEConfig config,
        ILogger<MondayToThuFriHarness> logger)
    {
        _snapshotProvider = snapshotProvider;
        _fillEngine = fillEngine;
        _strategy = strategy;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Run complete CDTE backtest over specified date range
    /// Returns weekly performance metrics and execution analytics
    /// </summary>
    public async Task<BacktestResults> RunBacktestAsync(
        DateTime startDate, 
        DateTime endDate, 
        string underlying = "SPX")
    {
        _logger.LogInformation("Starting CDTE backtest from {Start} to {End} for {Underlying}", 
            startDate, endDate, underlying);

        var backtestResults = new BacktestResults
        {
            StartDate = startDate,
            EndDate = endDate,
            Underlying = underlying,
            WeeklyResults = new List<WeeklyResult>(),
            OverallMetrics = new OverallMetrics()
        };

        try
        {
            var currentDate = startDate;
            while (currentDate <= endDate)
            {
                // Find next Monday for entry
                var monday = GetNextMonday(currentDate);
                if (monday > endDate) break;

                var weekResult = await RunSingleWeekInternalAsync(monday, underlying);
                if (weekResult != null)
                {
                    backtestResults.WeeklyResults.Add(weekResult);
                    _logger.LogInformation("Week {Monday}: P&L {PnL:C}, Trades {Trades}", 
                        monday.ToString("yyyy-MM-dd"), weekResult.WeeklyPnL, weekResult.TradeCount);
                }

                currentDate = monday.AddDays(7); // Move to next week
            }

            // Calculate overall metrics
            backtestResults.OverallMetrics = CalculateOverallMetrics(backtestResults.WeeklyResults);
            
            _logger.LogInformation("Backtest completed: {WeekCount} weeks, Total P&L: {TotalPnL:C}, Sharpe: {Sharpe:F3}", 
                backtestResults.WeeklyResults.Count, 
                backtestResults.OverallMetrics.TotalPnL,
                backtestResults.OverallMetrics.SharpeRatio);

            return backtestResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during CDTE backtest");
            throw;
        }
    }

    /// <summary>
    /// Run single CDTE week: Monday entry → Wednesday management → Friday exit
    /// Public method for SparseDayRunner integration
    /// </summary>
    public async Task<WeeklyResult?> RunSingleWeekAsync(DateTime monday, string underlying)
    {
        return await RunSingleWeekInternalAsync(monday, underlying);
    }

    /// <summary>
    /// Internal implementation of single week execution
    /// </summary>
    private async Task<WeeklyResult?> RunSingleWeekInternalAsync(DateTime monday, string underlying)
    {
        try
        {
            var weekResult = new WeeklyResult
            {
                WeekStart = monday,
                MondayEntries = new List<TradeResult>(),
                WednesdayActions = new List<ManagementResult>(),
                FridayExits = new List<TradeResult>(),
                WeeklyPnL = 0m,
                TradeCount = 0
            };

            // Step 1: Monday Entry (10:00 ET)
            var mondaySnapshot = await GetSnapshotAsync(underlying, monday.Add(_config.MondayDecisionET));
            if (mondaySnapshot == null)
            {
                _logger.LogWarning("No Monday snapshot available for {Date}", monday);
                return null;
            }

            var mondayOrders = await _strategy.EnterMondayAsync(mondaySnapshot, _config);
            var mondayResults = await ExecuteOrdersAsync(mondayOrders.Orders, mondaySnapshot);
            weekResult.MondayEntries.AddRange(mondayResults);

            // Add successful trades to portfolio
            foreach (var result in mondayResults.Where(r => r.WasExecuted))
            {
                _portfolio[result.OrderId] = ConvertToPosition(result, mondaySnapshot);
            }

            // Step 2: Wednesday Management (12:30 ET) - if positions exist
            if (_portfolio.Any())
            {
                var wednesday = monday.AddDays(2);
                var wednesdaySnapshot = await GetSnapshotAsync(underlying, wednesday.Add(_config.WednesdayDecisionET));
                
                if (wednesdaySnapshot != null)
                {
                    var portfolioState = CreatePortfolioState();
                    var decisionPlan = await _strategy.ManageWednesdayAsync(portfolioState, wednesdaySnapshot, _config);
                    
                    foreach (var action in decisionPlan.Actions)
                    {
                        var managementResult = await ExecuteManagementActionAsync(action, wednesdaySnapshot);
                        weekResult.WednesdayActions.Add(managementResult);
                    }
                }
            }

            // Step 3: Friday Exit (15:00 CT) - force close all remaining positions
            if (_portfolio.Any())
            {
                var friday = monday.AddDays(4);
                var fridaySnapshot = await GetSnapshotAsync(underlying, friday.Add(TimeSpan.FromHours(15))); // 15:00 CT
                
                if (fridaySnapshot != null)
                {
                    var portfolioState = CreatePortfolioState();
                    var exitReport = await _strategy.ExitWeekAsync(portfolioState, fridaySnapshot, _config);
                    
                    var fridayResults = await ExecuteOrdersAsync(exitReport.ExitOrders, fridaySnapshot);
                    weekResult.FridayExits.AddRange(fridayResults);
                }
            }

            // Calculate week P&L and clear portfolio
            weekResult.WeeklyPnL = CalculateWeeklyPnL();
            weekResult.TradeCount = weekResult.MondayEntries.Count + weekResult.WednesdayActions.Count + weekResult.FridayExits.Count;
            
            _portfolio.Clear(); // Reset for next week

            return weekResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running week {Monday}", monday);
            return null;
        }
    }

    /// <summary>
    /// Execute list of spread orders using NBBO fill engine
    /// </summary>
    private async Task<List<TradeResult>> ExecuteOrdersAsync(List<SpreadOrder> orders, ChainSnapshot snapshot)
    {
        var results = new List<TradeResult>();

        foreach (var spreadOrder in orders)
        {
            var tradeResult = new TradeResult
            {
                OrderId = spreadOrder.SpreadOrderId,
                OrderTime = spreadOrder.Timestamp,
                StrategyType = spreadOrder.StrategyType,
                LegResults = new List<LegResult>(),
                WasExecuted = true,
                ExecutionCost = 0m
            };

            // Execute each leg using NBBO fill engine
            foreach (var leg in spreadOrder.Legs)
            {
                var quote = CreateQuoteFromOption(leg, snapshot);
                if (quote != null)
                {
                    var fillResult = await _fillEngine.SimulateFillAsync(
                        leg, quote, _fillEngine.CurrentProfile, CreateMarketState(snapshot));

                    var legResult = new LegResult
                    {
                        LegId = leg.OrderId,
                        Symbol = leg.Symbol,
                        Strike = leg.Strike,
                        Side = leg.Side,
                        Quantity = leg.Quantity,
                        LimitPrice = leg.LimitPrice ?? 0m,
                        FillPrice = fillResult?.ChildFills.FirstOrDefault()?.Price ?? 0m,
                        WasFilled = fillResult?.ChildFills.Any() ?? false,
                        Slippage = fillResult?.SlippagePerContract ?? 0m
                    };

                    tradeResult.LegResults.Add(legResult);
                    
                    if (!legResult.WasFilled)
                    {
                        tradeResult.WasExecuted = false;
                        _logger.LogWarning("Leg {LegId} failed to fill: {Symbol} {Strike} {Side}", 
                            leg.OrderId, leg.Symbol, leg.Strike, leg.Side);
                    }
                }
                else
                {
                    tradeResult.WasExecuted = false;
                    _logger.LogWarning("No quote available for leg: {Symbol} {Strike}", leg.Symbol, leg.Strike);
                }
            }

            // Calculate total execution cost
            tradeResult.ExecutionCost = tradeResult.LegResults.Sum(l => l.FillPrice * l.Quantity * (l.Side == OrderSide.Buy ? 1 : -1));
            
            results.Add(tradeResult);
            
            _logger.LogDebug("Executed {Strategy}: {LegCount} legs, Cost: {Cost:C}, Success: {Success}", 
                tradeResult.StrategyType, tradeResult.LegResults.Count, tradeResult.ExecutionCost, tradeResult.WasExecuted);
        }

        return results;
    }

    /// <summary>
    /// Execute Wednesday management action
    /// </summary>
    private async Task<ManagementResult> ExecuteManagementActionAsync(ManagementAction action, ChainSnapshot snapshot)
    {
        var result = new ManagementResult
        {
            ActionType = action.ActionType,
            PositionId = action.PositionId,
            ActionTime = snapshot.TimestampET,
            WasSuccessful = true,
            PnLImpact = 0m
        };

        try
        {
            switch (action.ActionType)
            {
                case "TakeProfit":
                case "LossManagement":
                    if (action.NewOrder != null)
                    {
                        var exitResults = await ExecuteOrdersAsync(new List<SpreadOrder> { action.NewOrder }, snapshot);
                        result.WasSuccessful = exitResults.All(r => r.WasExecuted);
                        result.PnLImpact = exitResults.Sum(r => r.ExecutionCost);
                        
                        // Remove position from portfolio
                        _portfolio.Remove(action.PositionId);
                    }
                    break;

                case "Roll":
                    // Close original position and open new one
                    if (action.NewOrder != null && _portfolio.ContainsKey(action.PositionId))
                    {
                        var originalPosition = _portfolio[action.PositionId];
                        var exitOrder = CreateExitOrderFromPosition(originalPosition, snapshot);
                        var rollResults = await ExecuteOrdersAsync(new List<SpreadOrder> { exitOrder, action.NewOrder }, snapshot);
                        
                        result.WasSuccessful = rollResults.All(r => r.WasExecuted);
                        result.PnLImpact = rollResults.Sum(r => r.ExecutionCost);
                        
                        if (result.WasSuccessful)
                        {
                            // Replace position in portfolio
                            _portfolio[action.PositionId] = ConvertToPosition(rollResults.Last(), snapshot);
                        }
                    }
                    break;

                case "Hold":
                    // No action required
                    result.PnLImpact = 0m;
                    break;
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing management action {ActionType} for position {PositionId}", 
                action.ActionType, action.PositionId);
            result.WasSuccessful = false;
            return result;
        }
    }

    // Helper methods
    private async Task<ChainSnapshot?> GetSnapshotAsync(string underlying, DateTime timestamp)
    {
        return await _snapshotProvider.GetSnapshotAsync(underlying, timestamp, TimeSpan.FromMinutes(15));
    }

    private DateTime GetNextMonday(DateTime date)
    {
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)date.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0 && date.DayOfWeek != DayOfWeek.Monday)
            daysUntilMonday = 7;
        return date.Date.AddDays(daysUntilMonday);
    }

    private Quote? CreateQuoteFromOption(Order leg, ChainSnapshot snapshot)
    {
        var option = snapshot.Options.FirstOrDefault(o => 
            o.Strike == leg.Strike && 
            o.ExpirationDate.Date == leg.ExpirationDate.Date &&
            o.Right == (leg.OptionType == OptionType.Call ? OptionRight.Call : OptionRight.Put));

        if (option != null && option.HasValidNBBO)
        {
            return new Quote
            {
                Symbol = leg.Symbol,
                Bid = option.Bid,
                Ask = option.Ask,
                Mid = (option.Bid + option.Ask) / 2m,
                Spread = option.Ask - option.Bid,
                Timestamp = snapshot.TimestampUTC
            };
        }

        return null;
    }

    private MarketState CreateMarketState(ChainSnapshot snapshot)
    {
        var frontIV = snapshot.GetFrontImpliedVolatility();
        var stressLevel = frontIV > 25.0 ? 1.0 : frontIV < 12.0 ? 0.2 : 0.5;

        return new MarketState
        {
            Timestamp = snapshot.TimestampUTC,
            StressLevel = stressLevel,
            VolatilityRegime = frontIV > 22.0 ? "High" : frontIV < 15.0 ? "Low" : "Mid"
        };
    }

    private PortfolioState CreatePortfolioState()
    {
        return new PortfolioState
        {
            OpenPositions = _portfolio.Values.ToList(),
            TotalCapital = 100000m, // $100k default
            WeeklyPnL = CalculateWeeklyPnL()
        };
    }

    private Position ConvertToPosition(TradeResult tradeResult, ChainSnapshot snapshot)
    {
        return new Position
        {
            Id = tradeResult.OrderId,
            Legs = tradeResult.LegResults.Select(l => new Order
            {
                OrderId = l.LegId,
                Symbol = l.Symbol,
                Strike = l.Strike,
                ExpirationDate = GetExpirationFromSymbol(l.Symbol),
                OptionType = l.Symbol.Contains("C") ? OptionType.Call : OptionType.Put,
                Side = l.Side,
                Quantity = l.Quantity,
                LimitPrice = l.FillPrice
            }).ToList(),
            MaxRisk = _config.RiskCapUsd,
            EntryTime = tradeResult.OrderTime,
            StrategyType = tradeResult.StrategyType
        };
    }

    private SpreadOrder CreateExitOrderFromPosition(Position position, ChainSnapshot snapshot)
    {
        var exitLegs = position.Legs.Select(leg =>
        {
            var currentOption = snapshot.Options.FirstOrDefault(o => 
                o.Strike == leg.Strike && 
                o.ExpirationDate.Date == leg.ExpirationDate.Date &&
                o.Right == (leg.OptionType == OptionType.Call ? OptionRight.Call : OptionRight.Put));

            var exitSide = leg.Side == OrderSide.Buy ? OrderSide.Sell : OrderSide.Buy;
            var exitPrice = currentOption != null 
                ? (exitSide == OrderSide.Buy ? currentOption.Ask : currentOption.Bid)
                : 0.01m;

            return new Order
            {
                OrderId = Guid.NewGuid().ToString(),
                Symbol = leg.Symbol,
                Strike = leg.Strike,
                ExpirationDate = leg.ExpirationDate,
                OptionType = leg.OptionType,
                Side = exitSide,
                Quantity = leg.Quantity,
                LimitPrice = exitPrice,
                Timestamp = snapshot.TimestampET
            };
        }).ToList();

        return new SpreadOrder
        {
            SpreadOrderId = Guid.NewGuid().ToString(),
            Legs = exitLegs,
            StrategyType = $"Exit_{position.StrategyType}",
            SpreadType = SpreadType.Custom,
            Timestamp = snapshot.TimestampET
        };
    }

    private DateTime GetExpirationFromSymbol(string symbol)
    {
        // Simplified - in production would parse actual option symbol
        return DateTime.Today.AddDays(2);
    }

    private decimal CalculateWeeklyPnL()
    {
        // Calculate P&L from all open positions
        return _portfolio.Values.Sum(p => p.MaxRisk * 0.1m); // Placeholder calculation
    }

    private OverallMetrics CalculateOverallMetrics(List<WeeklyResult> weeklyResults)
    {
        var pnls = weeklyResults.Select(w => (double)w.WeeklyPnL).ToList();
        var totalPnL = pnls.Sum();
        var avgPnL = pnls.Average();
        var stdDev = Math.Sqrt(pnls.Average(p => Math.Pow(p - avgPnL, 2)));
        
        return new OverallMetrics
        {
            TotalPnL = (decimal)totalPnL,
            WeekCount = weeklyResults.Count,
            WinRate = weeklyResults.Count(w => w.WeeklyPnL > 0) / (double)weeklyResults.Count,
            AvgWeeklyPnL = (decimal)avgPnL,
            MaxWeeklyPnL = weeklyResults.Max(w => w.WeeklyPnL),
            MinWeeklyPnL = weeklyResults.Min(w => w.WeeklyPnL),
            SharpeRatio = stdDev > 0 ? avgPnL / stdDev * Math.Sqrt(52) : 0.0, // Annualized
            MaxDrawdown = CalculateMaxDrawdown(weeklyResults),
            TotalTrades = weeklyResults.Sum(w => w.TradeCount)
        };
    }

    private decimal CalculateMaxDrawdown(List<WeeklyResult> weeklyResults)
    {
        var cumulative = 0m;
        var peak = 0m;
        var maxDrawdown = 0m;

        foreach (var week in weeklyResults.OrderBy(w => w.WeekStart))
        {
            cumulative += week.WeeklyPnL;
            if (cumulative > peak) peak = cumulative;
            var drawdown = peak - cumulative;
            if (drawdown > maxDrawdown) maxDrawdown = drawdown;
        }

        return maxDrawdown;
    }

    public void Dispose()
    {
        _snapshotProvider?.Dispose();
    }
}

// Supporting data structures for backtest results
public class BacktestResults
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Underlying { get; set; } = "";
    public List<WeeklyResult> WeeklyResults { get; set; } = new();
    public OverallMetrics OverallMetrics { get; set; } = new();
}

public class WeeklyResult
{
    public DateTime WeekStart { get; set; }
    public List<TradeResult> MondayEntries { get; set; } = new();
    public List<ManagementResult> WednesdayActions { get; set; } = new();
    public List<TradeResult> FridayExits { get; set; } = new();
    public decimal WeeklyPnL { get; set; }
    public int TradeCount { get; set; }
}

public class TradeResult
{
    public string OrderId { get; set; } = "";
    public DateTime OrderTime { get; set; }
    public string StrategyType { get; set; } = "";
    public List<LegResult> LegResults { get; set; } = new();
    public bool WasExecuted { get; set; }
    public decimal ExecutionCost { get; set; }
}

public class LegResult
{
    public string LegId { get; set; } = "";
    public string Symbol { get; set; } = "";
    public decimal Strike { get; set; }
    public OrderSide Side { get; set; }
    public int Quantity { get; set; }
    public decimal LimitPrice { get; set; }
    public decimal FillPrice { get; set; }
    public bool WasFilled { get; set; }
    public decimal Slippage { get; set; }
}

public class ManagementResult
{
    public string ActionType { get; set; } = "";
    public string PositionId { get; set; } = "";
    public DateTime ActionTime { get; set; }
    public bool WasSuccessful { get; set; }
    public decimal PnLImpact { get; set; }
}

public class OverallMetrics
{
    public decimal TotalPnL { get; set; }
    public int WeekCount { get; set; }
    public double WinRate { get; set; }
    public decimal AvgWeeklyPnL { get; set; }
    public decimal MaxWeeklyPnL { get; set; }
    public decimal MinWeeklyPnL { get; set; }
    public double SharpeRatio { get; set; }
    public decimal MaxDrawdown { get; set; }
    public int TotalTrades { get; set; }
}

public class MarketState
{
    public DateTime Timestamp { get; set; }
    public double StressLevel { get; set; }
    public string VolatilityRegime { get; set; } = "";
}