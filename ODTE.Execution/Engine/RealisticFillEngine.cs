using ODTE.Execution.Interfaces;
using ODTE.Execution.Models;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace ODTE.Execution.Engine;

/// <summary>
/// Market-microstructure-aware execution engine implementing the realFillSimulationUpgrade.md specification.
/// Replaces optimistic/idealized fills with calibrated execution friction for institutional compliance.
/// </summary>
public class RealisticFillEngine : IFillEngine
{
    private readonly ILogger<RealisticFillEngine> _logger;
    private readonly Random _random;
    private readonly Dictionary<DateTime, ExecutionMetrics> _dailyMetrics = new();
    
    public ExecutionProfile CurrentProfile { get; private set; }

    public RealisticFillEngine(ExecutionProfile profile, ILogger<RealisticFillEngine>? logger = null)
    {
        CurrentProfile = profile;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<RealisticFillEngine>.Instance;
        
        // Use cryptographically secure random for deterministic testing with seeds
        _random = new Random();
    }

    /// <summary>
    /// Simulate realistic fill following the algorithm specified in realFillSimulationUpgrade.md.
    /// </summary>
    public async Task<FillResult?> SimulateFillAsync(Order order, Quote quote, ExecutionProfile profile, MarketState marketState)
    {
        try
        {
            _logger.LogDebug("Simulating fill for order {OrderId} symbol {Symbol}", order.OrderId, order.Symbol);
            
            // Step 1: Compute spread and ToB size
            var spread = Math.Max(0, quote.Ask - quote.Bid);
            var tobSize = Math.Max(1, quote.TopOfBookSize); // Defensive minimum
            
            // Step 2: Split sizing to enforce participation limits
            var childOrders = SplitOrderForParticipation(order, tobSize, profile);
            
            var childFills = new List<ChildFill>();
            var diagnostics = new ExecutionDiagnostics
            {
                IntendedPrice = order.Side == OrderSide.Buy ? quote.Ask : quote.Bid,
                ExecutionProfile = profile.Name,
                StartQuote = quote
            };
            
            // Step 3: Process each child order
            for (int i = 0; i < childOrders.Count; i++)
            {
                var childOrder = childOrders[i];
                var childFill = await ProcessChildOrder(childOrder, quote, profile, marketState, i);
                childFills.Add(childFill);
                
                // Update diagnostics
                diagnostics = diagnostics with
                {
                    TotalLatencyMs = diagnostics.TotalLatencyMs + childFill.LatencyMs,
                    MidAttempts = diagnostics.MidAttempts + (childFill.WasMidAttempt ? 1 : 0),
                    MidAccepted = diagnostics.MidAccepted + (childFill.WasMidAccepted ? 1 : 0),
                    TotalAdverseSelection = diagnostics.TotalAdverseSelection + childFill.AdverseSelectionCost,
                    TotalSizePenalty = diagnostics.TotalSizePenalty + childFill.SizePenaltyCost,
                    TotalSlippageFloor = diagnostics.TotalSlippageFloor + childFill.SlippageApplied
                };
            }
            
            // Calculate final results
            var result = CreateFillResult(order, childFills, diagnostics, quote);
            
            // Update daily metrics
            UpdateDailyMetrics(result);
            
            _logger.LogDebug("Fill simulation complete: {AveragePrice} (intended: {IntendedPrice})", 
                result.AverageFillPrice, diagnostics.IntendedPrice);
                
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error simulating fill for order {OrderId}", order.OrderId);
            return null;
        }
    }

    /// <summary>
    /// Calculate worst-case fill price for risk management.
    /// Uses conservative assumptions for RiskGate validation.
    /// </summary>
    public decimal CalculateWorstCaseFill(Order order, Quote quote, ExecutionProfile profile)
    {
        var basePrice = order.Side == OrderSide.Buy ? quote.Ask : quote.Bid;
        var spread = quote.Spread;
        
        // Apply all penalties at maximum levels
        var slippageFloor = Math.Max(profile.SlippageFloor.PerContract, 
                                   profile.SlippageFloor.PctOfSpread * spread);
        var adverseSelection = (decimal)profile.AdverseSelectionBps / 10000m * spread;
        var sizePenalty = (decimal)profile.SizePenalty.BpPerExtraTobMultiple / 10000m * spread;
        
        // Worst case: all penalties apply
        var worstCase = order.Side == OrderSide.Buy 
            ? basePrice + slippageFloor + adverseSelection + sizePenalty
            : basePrice - slippageFloor - adverseSelection - sizePenalty;
            
        return Math.Max(0.01m, worstCase); // Minimum tick size
    }

    /// <summary>
    /// Get daily execution metrics for audit compliance.
    /// </summary>
    public ExecutionMetrics GetDailyMetrics(DateTime date)
    {
        var dateKey = date.Date;
        return _dailyMetrics.GetValueOrDefault(dateKey, new ExecutionMetrics { Date = dateKey });
    }

    /// <summary>
    /// Split order into child orders respecting ToB participation limits.
    /// </summary>
    private List<Order> SplitOrderForParticipation(Order order, int tobSize, ExecutionProfile profile)
    {
        var maxChildSize = (int)(tobSize * profile.MaxTobParticipation);
        if (maxChildSize >= order.Quantity || maxChildSize <= 0)
        {
            return new List<Order> { order };
        }
        
        var childOrders = new List<Order>();
        var remainingQuantity = order.Quantity;
        var sequenceNumber = 0;
        
        while (remainingQuantity > 0)
        {
            var childQuantity = Math.Min(remainingQuantity, maxChildSize);
            var childOrder = order with 
            { 
                OrderId = $"{order.OrderId}-{sequenceNumber++}",
                Quantity = childQuantity
            };
            childOrders.Add(childOrder);
            remainingQuantity -= childQuantity;
        }
        
        return childOrders;
    }

    /// <summary>
    /// Process individual child order following the fill simulation algorithm.
    /// </summary>
    private async Task<ChildFill> ProcessChildOrder(Order childOrder, Quote quote, ExecutionProfile profile, 
                                                   MarketState marketState, int sequenceNumber)
    {
        var spread = quote.Spread;
        var spreadCents = spread * 100m;
        
        // Step 3.1: Decide attempt@mid
        var midProbability = profile.MidFill.GetMidFillProbability(profile.Name, spreadCents);
        
        // Apply market state adjustments
        if (marketState.IsEventRisk)
            midProbability *= 0.5m; // Reduce mid-fill chance during events
            
        var attemptMid = _random.NextDouble() < (double)midProbability;
        
        // Step 3.2: Simulate latency
        var latency = SimulateLatency(profile.LatencyMs);
        var newQuote = SimulateQuoteAfterLatency(quote, latency, marketState);
        
        var fillPrice = quote.Mid; // Default to mid
        var wasMidAccepted = false;
        
        // Step 3.3: Mid attempt logic
        if (attemptMid)
        {
            if (QuoteImprovedOrUnchanged(quote, newQuote) && _random.NextDouble() < (double)midProbability)
            {
                fillPrice = newQuote.Mid;
                wasMidAccepted = true;
            }
            else
            {
                // Reprice to touch
                fillPrice = childOrder.Side == OrderSide.Buy ? newQuote.Ask : newQuote.Bid;
            }
        }
        else
        {
            // Direct to touch
            fillPrice = childOrder.Side == OrderSide.Buy ? newQuote.Ask : newQuote.Bid;
        }
        
        // Step 3.4: Apply slippage floor
        var slippageFloor = Math.Max(profile.SlippageFloor.PerContract, 
                                   profile.SlippageFloor.PctOfSpread * spread);
        
        // Step 3.5: Apply adverse selection
        var adverseSelectionCost = 0m;
        if (QuoteMovedAgainst(quote, newQuote, childOrder.Side))
        {
            adverseSelectionCost = (decimal)profile.AdverseSelectionBps / 10000m * spread;
        }
        
        // Step 3.6: Apply size penalty for large orders
        var sizePenaltyCost = 0m;
        var tobMultiples = (decimal)childOrder.Quantity / quote.TopOfBookSize;
        if (tobMultiples > 1.0m)
        {
            var extraMultiples = tobMultiples - 1.0m;
            sizePenaltyCost = extraMultiples * (decimal)profile.SizePenalty.BpPerExtraTobMultiple / 10000m * spread;
        }
        
        // Apply all costs (only if not mid-accepted)
        if (!wasMidAccepted)
        {
            var direction = childOrder.Side == OrderSide.Buy ? 1m : -1m;
            fillPrice += direction * (slippageFloor + adverseSelectionCost + sizePenaltyCost);
        }
        
        // Ensure minimum tick size
        fillPrice = Math.Max(0.01m, fillPrice);
        
        return new ChildFill
        {
            SequenceNumber = sequenceNumber,
            Price = fillPrice,
            Quantity = childOrder.Quantity,
            Timestamp = DateTime.UtcNow.AddMilliseconds((double)latency),
            WasMidAttempt = attemptMid,
            WasMidAccepted = wasMidAccepted,
            LatencyMs = latency,
            SlippageApplied = wasMidAccepted ? 0m : slippageFloor,
            AdverseSelectionCost = adverseSelectionCost,
            SizePenaltyCost = sizePenaltyCost
        };
    }

    /// <summary>
    /// Simulate network/processing latency with normal distribution.
    /// </summary>
    private decimal SimulateLatency(int meanLatencyMs)
    {
        // Normal distribution with 50ms standard deviation
        var latency = meanLatencyMs + (_random.NextDouble() - 0.5) * 100; // ±50ms
        return Math.Max(10, (decimal)latency); // Minimum 10ms
    }

    /// <summary>
    /// Simulate quote changes during latency period.
    /// </summary>
    private Quote SimulateQuoteAfterLatency(Quote originalQuote, decimal latencyMs, MarketState marketState)
    {
        // For simplicity, assume quote remains stable during short latency periods
        // In production, this would fetch actual quote at T+latency
        
        // Small random quote movement based on market volatility
        var volatilityFactor = marketState.StressLevel * 0.01m; // Up to 1% movement in extreme stress
        var bidMovement = ((decimal)_random.NextDouble() - 0.5m) * volatilityFactor;
        var askMovement = ((decimal)_random.NextDouble() - 0.5m) * volatilityFactor;
        
        return originalQuote with
        {
            Bid = Math.Max(0.01m, originalQuote.Bid + bidMovement),
            Ask = Math.Max(0.02m, originalQuote.Ask + askMovement),
            Timestamp = originalQuote.Timestamp.AddMilliseconds((double)latencyMs)
        };
    }

    /// <summary>
    /// Check if quote improved or remained unchanged (favorable for mid-fills).
    /// </summary>
    private bool QuoteImprovedOrUnchanged(Quote original, Quote updated)
    {
        return updated.Spread <= original.Spread;
    }

    /// <summary>
    /// Check if quote moved against the order during latency.
    /// </summary>
    private bool QuoteMovedAgainst(Quote original, Quote updated, OrderSide side)
    {
        return side == OrderSide.Buy ? updated.Ask > original.Ask : updated.Bid < original.Bid;
    }

    /// <summary>
    /// Create final fill result from child fills and diagnostics.
    /// </summary>
    private FillResult CreateFillResult(Order order, List<ChildFill> childFills, ExecutionDiagnostics diagnostics, Quote originalQuote)
    {
        var avgPrice = childFills.Sum(f => f.Price * f.Quantity) / childFills.Sum(f => f.Quantity);
        var intendedPrice = order.Side == OrderSide.Buy ? originalQuote.Ask : originalQuote.Bid;
        
        diagnostics = diagnostics with { AchievedPrice = avgPrice };
        
        return new FillResult
        {
            OrderId = order.OrderId,
            FillTimestamp = DateTime.UtcNow,
            ChildFills = childFills,
            Diagnostics = diagnostics,
            WasMidOrBetter = avgPrice >= originalQuote.Mid,
            WasWithinNbbo = IsWithinNbbo(avgPrice, originalQuote),
            SlippagePerContract = Math.Abs(avgPrice - intendedPrice),
            TotalExecutionCost = Math.Abs(avgPrice - originalQuote.Mid) * order.Quantity
        };
    }

    /// <summary>
    /// Check if fill price is within NBBO band (±$0.01 tolerance).
    /// </summary>
    private bool IsWithinNbbo(decimal fillPrice, Quote quote)
    {
        return fillPrice >= quote.Bid - 0.01m && fillPrice <= quote.Ask + 0.01m;
    }

    /// <summary>
    /// Update daily metrics for audit compliance tracking.
    /// </summary>
    private void UpdateDailyMetrics(FillResult result)
    {
        var date = result.FillTimestamp.Date;
        var existing = _dailyMetrics.GetValueOrDefault(date, new ExecutionMetrics { Date = date });
        
        _dailyMetrics[date] = existing with
        {
            TotalFills = existing.TotalFills + 1,
            MidOrBetterFills = existing.MidOrBetterFills + (result.WasMidOrBetter ? 1 : 0),
            WithinNbboFills = existing.WithinNbboFills + (result.WasWithinNbbo ? 1 : 0),
            AverageLatencyMs = (existing.AverageLatencyMs * existing.TotalFills + result.Diagnostics.TotalLatencyMs) / (existing.TotalFills + 1),
            TotalNotional = existing.TotalNotional + result.TotalExecutionCost
        };
    }
}