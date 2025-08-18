using Microsoft.Extensions.Logging;
using ODTE.Execution.Models;
using ODTE.Historical.Providers;

namespace CDTE.Strategy.CDTE;

/// <summary>
/// CDTE Roll Rules - Wednesday Management Decision Tree
/// Per spec: Take Profit / Neutral Roll / Loss Management with defined risk
/// </summary>
public class CDTERollRules
{
    private readonly ILogger<CDTERollRules> _logger;
    
    public CDTERollRules(ILogger<CDTERollRules> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Determine Wednesday management action based on position P&L and market conditions
    /// Decision tree: Take Profit (70%+) → Neutral Roll (±15%) → Loss Management (50%+)
    /// </summary>
    public async Task<ManagementAction?> DetermineWednesdayAction(
        Position position, 
        decimal currentPnL, 
        double pnlPct, 
        ChainSnapshot wednesdaySnapshot, 
        CDTEConfig config)
    {
        try
        {
            _logger.LogInformation("Evaluating Wednesday action for position {Id}: P&L {PnL:C} ({PnLPct:P})", 
                position.Id, currentPnL, pnlPct);

            // Decision Tree Path 1: Take Profit (Core position only)
            if (position.StrategyType.Contains("Core") && pnlPct >= config.TakeProfitCorePct)
            {
                _logger.LogInformation("Take profit triggered for Core position {Id}: {PnLPct:P} >= {Threshold:P}", 
                    position.Id, pnlPct, config.TakeProfitCorePct);
                
                return await CreateTakeProfitAction(position, wednesdaySnapshot, config);
            }

            // Decision Tree Path 2: Neutral Band - Consider Rolling
            if (Math.Abs(pnlPct) < config.NeutralBandPct)
            {
                _logger.LogInformation("Neutral band detected for position {Id}: |{PnLPct:P}| < {Threshold:P}", 
                    position.Id, pnlPct, config.NeutralBandPct);
                
                return await EvaluateNeutralRoll(position, wednesdaySnapshot, config);
            }

            // Decision Tree Path 3: Loss Management
            if (pnlPct <= -config.MaxDrawdownPct)
            {
                _logger.LogInformation("Loss management triggered for position {Id}: {PnLPct:P} <= -{Threshold:P}", 
                    position.Id, pnlPct, config.MaxDrawdownPct);
                
                return await CreateLossManagementAction(position, wednesdaySnapshot, config);
            }

            // No action required - hold position
            _logger.LogInformation("No action required for position {Id}: P&L {PnLPct:P} within acceptable range", 
                position.Id, pnlPct);
            
            return new ManagementAction
            {
                ActionType = "Hold",
                PositionId = position.Id,
                Reason = $"P&L {pnlPct:P} within acceptable range"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error determining Wednesday action for position {Id}", position.Id);
            return null;
        }
    }

    /// <summary>
    /// Create take profit action - close Core position, keep Carry
    /// </summary>
    private async Task<ManagementAction> CreateTakeProfitAction(
        Position position, 
        ChainSnapshot snapshot, 
        CDTEConfig config)
    {
        try
        {
            var exitOrder = CreateExitOrder(position, snapshot, "TakeProfit");
            
            return new ManagementAction
            {
                ActionType = "TakeProfit",
                PositionId = position.Id,
                NewOrder = exitOrder,
                Reason = $"Core position profit target achieved: ≥{config.TakeProfitCorePct:P}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating take profit action for position {Id}", position.Id);
            throw;
        }
    }

    /// <summary>
    /// Evaluate neutral roll opportunity - maintain defined risk
    /// Per spec: Roll Core to Friday using current delta targets
    /// </summary>
    private async Task<ManagementAction?> EvaluateNeutralRoll(
        Position position, 
        ChainSnapshot snapshot, 
        CDTEConfig config)
    {
        try
        {
            // Only roll Core positions, not Carry
            if (!position.StrategyType.Contains("Core"))
            {
                return new ManagementAction
                {
                    ActionType = "Hold",
                    PositionId = position.Id,
                    Reason = "Carry position - no roll required"
                };
            }

            // Get Friday expiration for roll target
            var fridayExpiry = GetNextFridayExpiry(snapshot.TimestampET);
            
            // Calculate current position value for roll economics
            var currentValue = CalculatePositionValue(position, snapshot);
            
            // Create potential roll order using current delta targets
            var rollOrder = await CreateRollOrder(position, snapshot, fridayExpiry, config);
            
            if (rollOrder == null)
            {
                _logger.LogWarning("Could not create roll order for position {Id}", position.Id);
                return null;
            }

            // Evaluate roll economics - must meet debit cap constraint
            var rollDebit = CalculateRollDebit(currentValue, rollOrder, snapshot);
            var maxAllowedDebit = config.RollDebitCapPctOfRisk * (double)Math.Abs(position.MaxRisk);

            if (rollDebit > (decimal)maxAllowedDebit)
            {
                _logger.LogInformation("Roll debit too high for position {Id}: {Debit:C} > {MaxDebit:C}", 
                    position.Id, rollDebit, (decimal)maxAllowedDebit);
                
                return new ManagementAction
                {
                    ActionType = "Hold",
                    PositionId = position.Id,
                    Reason = $"Roll debit {rollDebit:C} exceeds cap {(decimal)maxAllowedDebit:C}"
                };
            }

            return new ManagementAction
            {
                ActionType = "Roll",
                PositionId = position.Id,
                NewOrder = rollOrder,
                Reason = $"Neutral roll to Friday expiry - debit {rollDebit:C} within cap"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating neutral roll for position {Id}", position.Id);
            return null;
        }
    }

    /// <summary>
    /// Create loss management action - close both Core and Carry, re-enter cheaper Carry
    /// </summary>
    private async Task<ManagementAction> CreateLossManagementAction(
        Position position, 
        ChainSnapshot snapshot, 
        CDTEConfig config)
    {
        try
        {
            // First, create exit order for current position
            var exitOrder = CreateExitOrder(position, snapshot, "LossManagement");
            
            // Then create new, cheaper Carry position for Friday
            var fridayExpiry = GetNextFridayExpiry(snapshot.TimestampET);
            var newCarryOrder = await CreateCheaperCarryPosition(snapshot, fridayExpiry, config);
            
            return new ManagementAction
            {
                ActionType = "LossManagement",
                PositionId = position.Id,
                NewOrder = exitOrder, // Primary action is to exit
                Reason = $"Loss management triggered: drawdown ≥{config.MaxDrawdownPct:P}"
                // Note: New carry position would be handled as separate entry
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating loss management action for position {Id}", position.Id);
            throw;
        }
    }

    /// <summary>
    /// Create exit order to close position
    /// </summary>
    private SpreadOrder CreateExitOrder(Position position, ChainSnapshot snapshot, string reason)
    {
        var exitLegs = new List<Order>();
        
        foreach (var leg in position.Legs)
        {
            // Find corresponding option in current snapshot
            var currentOption = FindOptionInSnapshot(leg, snapshot);
            if (currentOption != null)
            {
                // Reverse the original order side to close
                var exitSide = leg.Side == OrderSide.Buy ? OrderSide.Sell : OrderSide.Buy;
                var exitPrice = exitSide == OrderSide.Buy ? currentOption.Ask : currentOption.Bid;
                
                var exitLeg = new Order
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
                
                exitLegs.Add(exitLeg);
            }
        }
        
        return new SpreadOrder
        {
            SpreadOrderId = Guid.NewGuid().ToString(),
            Legs = exitLegs,
            StrategyType = $"Exit_{position.StrategyType}",
            SpreadType = SpreadType.Custom,
            Timestamp = snapshot.TimestampET
        };
    }

    /// <summary>
    /// Create roll order to Friday expiry using current delta targets
    /// </summary>
    private async Task<SpreadOrder?> CreateRollOrder(
        Position position, 
        ChainSnapshot snapshot, 
        DateTime fridayExpiry, 
        CDTEConfig config)
    {
        try
        {
            // Determine original strategy type to maintain structure
            var originalStrategy = DetermineOriginalStrategy(position);
            
            return originalStrategy switch
            {
                CDTEStructure.IronCondor => await CreateRollIronCondor(snapshot, fridayExpiry, config),
                CDTEStructure.BrokenWingButterfly => await CreateRollBWB(snapshot, fridayExpiry, config),
                CDTEStructure.IronFly => await CreateRollIronFly(snapshot, fridayExpiry, config),
                _ => null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating roll order for position {Id}", position.Id);
            return null;
        }
    }

    /// <summary>
    /// Create cheaper carry position after loss management
    /// Uses smaller widths and further strikes to limit weekly bleed
    /// </summary>
    private async Task<SpreadOrder?> CreateCheaperCarryPosition(
        ChainSnapshot snapshot, 
        DateTime fridayExpiry, 
        CDTEConfig config)
    {
        try
        {
            // Create conservative position with reduced risk
            var adjustedConfig = CreateConservativeConfig(config);
            
            // Use Iron Condor with wider wings and smaller position size
            return await CreateRollIronCondor(snapshot, fridayExpiry, adjustedConfig);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating cheaper carry position");
            return null;
        }
    }

    /// <summary>
    /// Create roll Iron Condor using current delta targets
    /// </summary>
    private async Task<SpreadOrder?> CreateRollIronCondor(
        ChainSnapshot snapshot, 
        DateTime expiry, 
        CDTEConfig config)
    {
        try
        {
            var fridayOptions = snapshot.GetOptionsForExpiry(expiry).ToList();
            var calls = fridayOptions.Where(o => o.Right == OptionRight.Call).ToList();
            var puts = fridayOptions.Where(o => o.Right == OptionRight.Put).ToList();
            
            // Use current delta targets from config
            var shortCall = PickByDelta(calls, config.DeltaTargets.IcShortAbs);
            var shortPut = PickByDelta(puts, -config.DeltaTargets.IcShortAbs);
            
            if (shortCall == null || shortPut == null)
            {
                _logger.LogWarning("Could not find strikes for IC roll at delta {Delta}", config.DeltaTargets.IcShortAbs);
                return null;
            }
            
            // Calculate wings to meet risk cap
            var longCall = CalculateWingStrike(shortCall, config.RiskCapUsd, true);
            var longPut = CalculateWingStrike(shortPut, config.RiskCapUsd, false);
            
            if (longCall == null || longPut == null)
            {
                return null;
            }
            
            return new SpreadOrder
            {
                SpreadOrderId = Guid.NewGuid().ToString(),
                Legs = new List<Order>
                {
                    CreateOptionOrder(shortCall, OrderSide.Sell, 1),
                    CreateOptionOrder(longCall, OrderSide.Buy, 1),
                    CreateOptionOrder(shortPut, OrderSide.Sell, 1),
                    CreateOptionOrder(longPut, OrderSide.Buy, 1)
                },
                StrategyType = "IronCondor_Roll",
                SpreadType = SpreadType.IronCondor,
                MaxRisk = config.RiskCapUsd,
                Timestamp = snapshot.TimestampET
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating roll Iron Condor");
            return null;
        }
    }

    // Helper methods
    private OptionContract? PickByDelta(List<OptionContract> chain, double targetDelta)
    {
        return chain.OrderBy(o => Math.Abs(o.Delta - targetDelta)).FirstOrDefault();
    }
    
    private Order CreateOptionOrder(OptionContract contract, OrderSide side, int quantity)
    {
        return new Order
        {
            OrderId = Guid.NewGuid().ToString(),
            Symbol = contract.Symbol,
            Strike = contract.Strike,
            ExpirationDate = contract.ExpirationDate,
            OptionType = contract.Right == OptionRight.Call ? OptionType.Call : OptionType.Put,
            Side = side,
            Quantity = quantity,
            LimitPrice = side == OrderSide.Buy ? contract.Ask : contract.Bid,
            Timestamp = DateTime.UtcNow
        };
    }
    
    private DateTime GetNextFridayExpiry(DateTime referenceDate)
    {
        var daysUntilFriday = ((int)DayOfWeek.Friday - (int)referenceDate.DayOfWeek + 7) % 7;
        if (daysUntilFriday == 0 && referenceDate.DayOfWeek != DayOfWeek.Friday)
            daysUntilFriday = 7;
        return referenceDate.Date.AddDays(daysUntilFriday);
    }
    
    private decimal CalculatePositionValue(Position position, ChainSnapshot snapshot)
    {
        decimal totalValue = 0m;
        foreach (var leg in position.Legs)
        {
            var currentOption = FindOptionInSnapshot(leg, snapshot);
            if (currentOption != null)
            {
                var legValue = leg.Side == OrderSide.Buy ? currentOption.Bid : -currentOption.Ask;
                totalValue += legValue * leg.Quantity;
            }
        }
        return totalValue;
    }
    
    private OptionContract? FindOptionInSnapshot(Order leg, ChainSnapshot snapshot)
    {
        return snapshot.Options.FirstOrDefault(o => 
            o.Strike == leg.Strike && 
            o.ExpirationDate.Date == leg.ExpirationDate.Date &&
            o.Right == (leg.OptionType == OptionType.Call ? OptionRight.Call : OptionRight.Put));
    }
    
    private decimal CalculateRollDebit(decimal currentValue, SpreadOrder rollOrder, ChainSnapshot snapshot)
    {
        // Calculate net debit/credit for the roll
        decimal rollValue = 0m;
        foreach (var leg in rollOrder.Legs)
        {
            var option = FindOptionInSnapshot(leg, snapshot);
            if (option != null)
            {
                var legValue = leg.Side == OrderSide.Buy ? -option.Ask : option.Bid;
                rollValue += legValue * leg.Quantity;
            }
        }
        
        return Math.Max(0, currentValue - rollValue); // Net debit
    }
    
    private CDTEStructure DetermineOriginalStrategy(Position position)
    {
        if (position.StrategyType.Contains("BWB") || position.StrategyType.Contains("Butterfly"))
            return CDTEStructure.BrokenWingButterfly;
        if (position.StrategyType.Contains("IronCondor"))
            return CDTEStructure.IronCondor;
        if (position.StrategyType.Contains("IronFly"))
            return CDTEStructure.IronFly;
        
        return CDTEStructure.IronCondor; // Default
    }
    
    private CDTEConfig CreateConservativeConfig(CDTEConfig original)
    {
        // Create more conservative configuration for loss management
        return new CDTEConfig
        {
            RiskCapUsd = original.RiskCapUsd * 0.5m, // Half the risk
            DeltaTargets = new DeltaTargets
            {
                IcShortAbs = original.DeltaTargets.IcShortAbs * 0.8, // Further OTM
                BwbBodyPut = original.DeltaTargets.BwbBodyPut * 0.8,
                BwbNearPut = original.DeltaTargets.BwbNearPut * 0.8,
                VertShortAbs = original.DeltaTargets.VertShortAbs * 0.8
            }
        };
    }
    
    // Placeholder implementations for missing methods
    private async Task<SpreadOrder?> CreateRollBWB(ChainSnapshot snapshot, DateTime expiry, CDTEConfig config) => null;
    private async Task<SpreadOrder?> CreateRollIronFly(ChainSnapshot snapshot, DateTime expiry, CDTEConfig config) => null;
    private OptionContract? CalculateWingStrike(OptionContract shortStrike, decimal riskCap, bool isCall) => null;
}