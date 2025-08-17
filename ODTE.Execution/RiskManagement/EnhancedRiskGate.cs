using Microsoft.Extensions.Logging;
using ODTE.Execution.Interfaces;
using ODTE.Execution.Models;

namespace ODTE.Execution.RiskManagement;

/// <summary>
/// Enhanced risk gate that uses realistic fill simulation for worst-case loss calculations.
/// Implements Reverse Fibonacci progression: $500 → $300 → $200 → $100 with green day resets.
/// </summary>
public class EnhancedRiskGate
{
    private readonly IFillEngine _fillEngine;
    private readonly ILogger<EnhancedRiskGate> _logger;
    private readonly Dictionary<DateTime, RiskState> _dailyRiskState = new();

    public EnhancedRiskGate(IFillEngine fillEngine, ILogger<EnhancedRiskGate>? logger = null)
    {
        _fillEngine = fillEngine;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<EnhancedRiskGate>.Instance;
    }

    /// <summary>
    /// Validate order against Reverse Fibonacci daily loss limits using worst-case fill simulation.
    /// </summary>
    public async Task<RiskGateResult> ValidateOrderAsync(Order order, Quote quote, MarketState marketState)
    {
        try
        {
            var today = marketState.Timestamp.Date;
            var riskState = GetOrCreateRiskState(today);

            // Calculate worst-case loss for this order
            var worstCaseFillPrice = _fillEngine.CalculateWorstCaseFill(order, quote, _fillEngine.CurrentProfile);
            var worstCaseLoss = CalculateMaxStructureLoss(order, worstCaseFillPrice, quote);

            // Get current allowed daily loss based on loss streak
            var allowedDailyLoss = GetAllowedDailyLoss(riskState.ConsecutiveLossDays);
            var projectedTotalLoss = riskState.RealizedLossToday + worstCaseLoss;

            var result = new RiskGateResult
            {
                IsApproved = projectedTotalLoss <= allowedDailyLoss,
                OrderId = order.OrderId,
                ValidatedAt = DateTime.UtcNow,
                CurrentDailyLoss = riskState.RealizedLossToday,
                AllowedDailyLoss = allowedDailyLoss,
                ProjectedTotalLoss = projectedTotalLoss,
                WorstCaseOrderLoss = worstCaseLoss,
                ConsecutiveLossDays = riskState.ConsecutiveLossDays,
                RevFibLevel = GetRevFibLevel(riskState.ConsecutiveLossDays)
            };

            if (!result.IsApproved)
            {
                result.RejectionReason = $"Order would breach daily RevFib limit: ${projectedTotalLoss:F2} > ${allowedDailyLoss:F2}";
                _logger.LogWarning("Order {OrderId} rejected: {Reason}", order.OrderId, result.RejectionReason);
            }
            else
            {
                _logger.LogDebug("Order {OrderId} approved: ${Loss:F2} of ${Limit:F2} daily budget",
                    order.OrderId, projectedTotalLoss, allowedDailyLoss);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating order {OrderId}", order.OrderId);
            return new RiskGateResult
            {
                IsApproved = false,
                OrderId = order.OrderId,
                RejectionReason = $"Risk validation error: {ex.Message}",
                ValidatedAt = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Register order execution to update daily risk tracking.
    /// </summary>
    public void RegisterOrderExecution(string orderId, decimal actualLoss, DateTime executionDate)
    {
        var today = executionDate.Date;
        var riskState = GetOrCreateRiskState(today);

        riskState.RealizedLossToday += actualLoss;
        riskState.ExecutedOrders.Add(new ExecutedOrder
        {
            OrderId = orderId,
            Loss = actualLoss,
            ExecutionTime = executionDate
        });

        _logger.LogDebug("Registered order {OrderId} execution: ${Loss:F2}, daily total: ${Total:F2}",
            orderId, actualLoss, riskState.RealizedLossToday);
    }

    /// <summary>
    /// Process end-of-day to update loss streak and reset daily counters.
    /// </summary>
    public void ProcessEndOfDay(DateTime date, decimal totalDayPnL)
    {
        var today = date.Date;
        var riskState = GetOrCreateRiskState(today);

        // Update loss streak based on day's performance
        if (totalDayPnL < 0)
        {
            riskState.ConsecutiveLossDays = Math.Min(3, riskState.ConsecutiveLossDays + 1);
            _logger.LogInformation("Loss day recorded: streak now {Streak} days, next day limit: ${Limit:F2}",
                riskState.ConsecutiveLossDays, GetAllowedDailyLoss(riskState.ConsecutiveLossDays));
        }
        else
        {
            if (riskState.ConsecutiveLossDays > 0)
            {
                _logger.LogInformation("Green day resets loss streak from {OldStreak} to 0", riskState.ConsecutiveLossDays);
            }
            riskState.ConsecutiveLossDays = 0;
        }

        riskState.FinalDayPnL = totalDayPnL;
        riskState.IsFinalized = true;

        // Initialize next day's risk state
        var tomorrow = today.AddDays(1);
        var tomorrowState = new RiskState
        {
            Date = tomorrow,
            ConsecutiveLossDays = riskState.ConsecutiveLossDays
        };
        _dailyRiskState[tomorrow] = tomorrowState;
    }

    /// <summary>
    /// Get current risk state for monitoring and reporting.
    /// </summary>
    public RiskState GetCurrentRiskState(DateTime date)
    {
        return GetOrCreateRiskState(date.Date);
    }

    /// <summary>
    /// Get allowed daily loss based on Reverse Fibonacci progression.
    /// </summary>
    private decimal GetAllowedDailyLoss(int consecutiveLossDays) => consecutiveLossDays switch
    {
        0 => 500m,  // Fresh start or after green day
        1 => 300m,  // First consecutive loss day
        2 => 200m,  // Second consecutive loss day
        _ => 100m   // Third+ consecutive loss day (max protection)
    };

    /// <summary>
    /// Get Reverse Fibonacci level name for reporting.
    /// </summary>
    private string GetRevFibLevel(int consecutiveLossDays) => consecutiveLossDays switch
    {
        0 => "Level 0 ($500 - Fresh Start)",
        1 => "Level 1 ($300 - First Loss)",
        2 => "Level 2 ($200 - Second Loss)",
        _ => "Level 3 ($100 - Maximum Protection)"
    };

    /// <summary>
    /// Calculate maximum potential loss for an order structure given worst-case fill.
    /// </summary>
    private decimal CalculateMaxStructureLoss(Order order, decimal worstCaseFillPrice, Quote currentQuote)
    {
        // For options, max loss depends on strategy type
        // This is a simplified calculation - production should use full Greeks-based analysis

        if (order.StrategyType == "CreditSpread")
        {
            // Credit spread: max loss = width - credit
            var estimatedCredit = Math.Abs(currentQuote.Mid - worstCaseFillPrice) * order.Quantity;
            var estimatedWidth = order.Strike * 0.1m; // Assume 10-point width for XSP
            return Math.Max(0, estimatedWidth * order.Quantity - estimatedCredit);
        }
        else if (order.StrategyType == "IronCondor")
        {
            // Iron condor: max loss = wing width - net credit
            var estimatedCredit = Math.Abs(currentQuote.Mid - worstCaseFillPrice) * order.Quantity;
            var estimatedWingWidth = order.Strike * 0.1m; // Assume 10-point wings
            return Math.Max(0, estimatedWingWidth * order.Quantity - estimatedCredit);
        }
        else
        {
            // Simple position: max loss = notional * max movement
            return order.NotionalValue * 0.1m; // Assume 10% max adverse movement
        }
    }

    /// <summary>
    /// Get or create risk state for a specific date.
    /// </summary>
    private RiskState GetOrCreateRiskState(DateTime date)
    {
        if (!_dailyRiskState.TryGetValue(date, out var state))
        {
            state = new RiskState
            {
                Date = date,
                ConsecutiveLossDays = GetPreviousLossStreak(date)
            };
            _dailyRiskState[date] = state;
        }
        return state;
    }

    /// <summary>
    /// Get loss streak from previous day for initialization.
    /// </summary>
    private int GetPreviousLossStreak(DateTime date)
    {
        var previousDate = date.AddDays(-1);
        if (_dailyRiskState.TryGetValue(previousDate, out var previousState))
        {
            return previousState.ConsecutiveLossDays;
        }
        return 0; // Start fresh if no previous data
    }
}

/// <summary>
/// Daily risk tracking state.
/// </summary>
public class RiskState
{
    public DateTime Date { get; set; }
    public decimal RealizedLossToday { get; set; }
    public int ConsecutiveLossDays { get; set; }
    public List<ExecutedOrder> ExecutedOrders { get; set; } = new();
    public decimal? FinalDayPnL { get; set; }
    public bool IsFinalized { get; set; }
}

/// <summary>
/// Record of executed order for audit trail.
/// </summary>
public class ExecutedOrder
{
    public string OrderId { get; set; } = "";
    public decimal Loss { get; set; }
    public DateTime ExecutionTime { get; set; }
}

/// <summary>
/// Result of risk gate validation.
/// </summary>
public class RiskGateResult
{
    public bool IsApproved { get; set; }
    public string OrderId { get; set; } = "";
    public DateTime ValidatedAt { get; set; }
    public decimal CurrentDailyLoss { get; set; }
    public decimal AllowedDailyLoss { get; set; }
    public decimal ProjectedTotalLoss { get; set; }
    public decimal WorstCaseOrderLoss { get; set; }
    public int ConsecutiveLossDays { get; set; }
    public string RevFibLevel { get; set; } = "";
    public string? RejectionReason { get; set; }
}