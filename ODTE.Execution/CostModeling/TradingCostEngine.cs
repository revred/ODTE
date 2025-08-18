using Microsoft.Extensions.Logging;
using ODTE.Execution.Models;

namespace ODTE.Execution.CostModeling;

/// <summary>
/// Trading Cost Engine - Realistic Options Trading Cost Modeling
/// Implements authentic bid-ask spreads, commissions, slippage, and market impact
/// Critical for validating genetic optimization results against real market conditions
/// </summary>
public class TradingCostEngine
{
    private readonly ILogger<TradingCostEngine> _logger;
    private readonly TradingCostConfig _config;

    public TradingCostEngine(ILogger<TradingCostEngine> logger, TradingCostConfig config)
    {
        _logger = logger;
        _config = config;
    }

    /// <summary>
    /// Calculate comprehensive trading costs for an options spread order
    /// Returns detailed cost breakdown for realistic P&L calculation
    /// </summary>
    public async Task<TradingCostResult> CalculateTradingCostsAsync(
        SpreadOrder order, 
        ChainSnapshot marketData, 
        int contractCount,
        MarketConditions conditions)
    {
        var result = new TradingCostResult
        {
            OrderId = order.OrderId,
            ContractCount = contractCount,
            Timestamp = DateTime.UtcNow
        };

        try
        {
            // 1. Calculate bid-ask spread costs
            result.BidAskSpreadCost = CalculateBidAskSpreadCost(order, marketData, contractCount);

            // 2. Calculate commission costs
            result.CommissionCost = CalculateCommissionCost(order, contractCount, conditions);

            // 3. Calculate market impact/slippage
            result.SlippageCost = await CalculateSlippageCostAsync(order, marketData, contractCount, conditions);

            // 4. Calculate assignment/exercise risks
            result.AssignmentRiskCost = CalculateAssignmentRiskCost(order, marketData, contractCount);

            // 5. Calculate liquidity adjustment
            result.LiquidityAdjustment = CalculateLiquidityAdjustment(order, marketData, contractCount);

            // 6. Calculate financing costs (for margin)
            result.FinancingCost = CalculateFinancingCost(order, contractCount, conditions);

            // Total all costs
            result.TotalCost = result.BidAskSpreadCost + result.CommissionCost + result.SlippageCost + 
                              result.AssignmentRiskCost + result.LiquidityAdjustment + result.FinancingCost;

            // Calculate cost as percentage of notional
            var notionalValue = CalculateNotionalValue(order, marketData, contractCount);
            result.CostPercentage = notionalValue > 0 ? result.TotalCost / notionalValue : 0m;

            _logger.LogDebug("Trading costs calculated for {OrderId}: Total ${TotalCost:F2} ({Percentage:P2})",
                order.OrderId, result.TotalCost, result.CostPercentage);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating trading costs for order {OrderId}", order.OrderId);
            throw;
        }
    }

    /// <summary>
    /// Calculate bid-ask spread crossing costs based on real NBBO data
    /// </summary>
    private decimal CalculateBidAskSpreadCost(SpreadOrder order, ChainSnapshot marketData, int contractCount)
    {
        decimal totalSpreadCost = 0m;

        foreach (var leg in order.Legs)
        {
            var optionQuote = GetOptionQuote(marketData, leg.Strike, leg.Expiry, leg.OptionType);
            if (optionQuote == null) continue;

            // Calculate bid-ask spread
            var spread = optionQuote.Ask - optionQuote.Bid;
            
            // Crossing cost depends on order direction and size
            var crossingCost = leg.Direction == OrderDirection.Buy 
                ? spread * _config.BidAskCrossingRate  // Typically pay ask, get closer to mid
                : spread * _config.BidAskCrossingRate; // Typically hit bid, get closer to mid

            // Apply volatility and moneyness adjustments
            var volatilityMultiplier = GetVolatilitySpreadMultiplier(optionQuote.ImpliedVolatility);
            var moneynessMultiplier = GetMoneynessSpreadMultiplier(leg.Strike, marketData.UnderlyingPrice, leg.OptionType);

            var adjustedCost = crossingCost * volatilityMultiplier * moneynessMultiplier;
            totalSpreadCost += adjustedCost * Math.Abs(leg.Quantity) * contractCount;
        }

        return totalSpreadCost;
    }

    /// <summary>
    /// Calculate commission costs based on broker fee structure
    /// </summary>
    private decimal CalculateCommissionCost(SpreadOrder order, int contractCount, MarketConditions conditions)
    {
        // Base commission per contract
        var perContractFee = _config.CommissionPerContract;
        
        // Base fee per trade
        var baseTradefee = _config.BaseTradeFee;
        
        // Calculate total contracts
        var totalContracts = order.Legs.Sum(leg => Math.Abs(leg.Quantity)) * contractCount;
        
        // Apply volume discounts
        var volumeDiscount = GetVolumeDiscount(totalContracts);
        
        // Apply complexity premium for multi-leg spreads
        var complexityMultiplier = order.Legs.Count > 2 ? _config.ComplexOrderMultiplier : 1.0m;
        
        var totalCommission = (baseTradeeFee + (perContractFee * totalContracts * complexityMultiplier)) * (1m - volumeDiscount);
        
        return Math.Max(totalCommission, _config.MinimumCommission);
    }

    /// <summary>
    /// Calculate slippage and market impact costs
    /// </summary>
    private async Task<decimal> CalculateSlippageCostAsync(
        SpreadOrder order, 
        ChainSnapshot marketData, 
        int contractCount, 
        MarketConditions conditions)
    {
        decimal totalSlippage = 0m;

        foreach (var leg in order.Legs)
        {
            var optionQuote = GetOptionQuote(marketData, leg.Strike, leg.Expiry, leg.OptionType);
            if (optionQuote == null) continue;

            var legContracts = Math.Abs(leg.Quantity) * contractCount;
            
            // Base slippage from market impact
            var marketImpactSlippage = CalculateMarketImpact(legContracts, optionQuote);
            
            // Timing slippage (time between decision and execution)
            var timingSlippage = CalculateTimingSlippage(optionQuote, conditions);
            
            // Volatility slippage (price movement during execution)
            var volatilitySlippage = CalculateVolatilitySlippage(optionQuote, conditions);
            
            // Liquidity slippage (wide markets, poor liquidity)
            var liquiditySlippage = CalculateLiquiditySlippage(optionQuote, legContracts);

            var totalLegSlippage = marketImpactSlippage + timingSlippage + volatilitySlippage + liquiditySlippage;
            totalSlippage += totalLegSlippage * legContracts;
        }

        return totalSlippage;
    }

    /// <summary>
    /// Calculate assignment risk costs for short option positions
    /// </summary>
    private decimal CalculateAssignmentRiskCost(SpreadOrder order, ChainSnapshot marketData, int contractCount)
    {
        decimal assignmentRisk = 0m;

        foreach (var leg in order.Legs.Where(l => l.Direction == OrderDirection.Sell))
        {
            var optionQuote = GetOptionQuote(marketData, leg.Strike, leg.Expiry, leg.OptionType);
            if (optionQuote == null) continue;

            // Calculate assignment probability
            var assignmentProbability = CalculateAssignmentProbability(leg, optionQuote, marketData);
            
            // Assignment cost (exercise fees, margin changes, etc.)
            var assignmentCost = _config.AssignmentFee + (marketData.UnderlyingPrice * 0.001m); // 0.1% of underlying value
            
            var legAssignmentRisk = assignmentProbability * assignmentCost * Math.Abs(leg.Quantity) * contractCount;
            assignmentRisk += legAssignmentRisk;
        }

        return assignmentRisk;
    }

    /// <summary>
    /// Calculate liquidity adjustment based on open interest and volume
    /// </summary>
    private decimal CalculateLiquidityAdjustment(SpreadOrder order, ChainSnapshot marketData, int contractCount)
    {
        decimal liquidityAdjustment = 0m;

        foreach (var leg in order.Legs)
        {
            var optionQuote = GetOptionQuote(marketData, leg.Strike, leg.Expiry, leg.OptionType);
            if (optionQuote == null) continue;

            var legContracts = Math.Abs(leg.Quantity) * contractCount;
            
            // Liquidity penalty based on position size vs. market depth
            var liquidityRatio = legContracts / Math.Max(1, optionQuote.OpenInterest);
            var liquidityPenalty = liquidityRatio > 0.1m ? (liquidityRatio - 0.1m) * optionQuote.Mid * 0.05m : 0m;
            
            liquidityAdjustment += liquidityPenalty * legContracts;
        }

        return liquidityAdjustment;
    }

    /// <summary>
    /// Calculate financing costs for margin requirements
    /// </summary>
    private decimal CalculateFinancingCost(SpreadOrder order, int contractCount, MarketConditions conditions)
    {
        // For defined risk spreads, financing cost is minimal
        // For undefined risk, calculate based on margin requirement and interest rates
        
        var marginRequired = CalculateMarginRequirement(order, contractCount);
        var dailyFinancingRate = _config.InterestRate / 365m;
        var averageHoldingPeriod = 2.5m; // Average holding period in days
        
        return marginRequired * dailyFinancingRate * averageHoldingPeriod;
    }

    // Helper methods for cost calculations
    private decimal GetVolatilitySpreadMultiplier(decimal impliedVolatility)
    {
        // Higher volatility = wider spreads
        return 1m + (impliedVolatility - 0.20m) * 0.5m; // Base 20% IV, 50% spread increase per 100% IV increase
    }

    private decimal GetMoneynessSpreadMultiplier(decimal strike, decimal underlyingPrice, OptionType optionType)
    {
        var moneyness = optionType == OptionType.Call 
            ? underlyingPrice / strike 
            : strike / underlyingPrice;

        // ATM options have tightest spreads, OTM options have wider spreads
        if (moneyness >= 0.95m && moneyness <= 1.05m) return 1.0m; // ATM
        if (moneyness >= 0.90m && moneyness <= 1.10m) return 1.2m; // Near the money
        if (moneyness >= 0.80m && moneyness <= 1.20m) return 1.5m; // OTM
        return 2.0m; // Deep OTM
    }

    private decimal GetVolumeDiscount(decimal totalContracts)
    {
        // Volume discount tiers
        if (totalContracts >= 1000) return 0.15m; // 15% discount for 1000+ contracts
        if (totalContracts >= 500) return 0.10m;  // 10% discount for 500+ contracts
        if (totalContracts >= 100) return 0.05m;  // 5% discount for 100+ contracts
        return 0m; // No discount
    }

    private decimal CalculateMarketImpact(decimal contractCount, OptionQuote quote)
    {
        // Market impact increases non-linearly with position size
        var impactMultiplier = Math.Pow((double)(contractCount / 10m), 0.6); // Square root scaling
        var baseImpact = (quote.Ask - quote.Bid) * 0.1m; // 10% of spread as base impact
        return (decimal)impactMultiplier * baseImpact;
    }

    private decimal CalculateTimingSlippage(OptionQuote quote, MarketConditions conditions)
    {
        // Slippage due to time between decision and execution
        var baseSlippage = quote.Mid * 0.001m; // 0.1% base slippage
        var volatilityMultiplier = 1m + conditions.RealizedVolatility; // Higher vol = more slippage
        return baseSlippage * volatilityMultiplier;
    }

    private decimal CalculateVolatilitySlippage(OptionQuote quote, MarketConditions conditions)
    {
        // Additional slippage during high volatility periods
        if (conditions.VIX > 30m) return quote.Mid * 0.002m; // 0.2% extra slippage in high vol
        if (conditions.VIX > 20m) return quote.Mid * 0.001m; // 0.1% extra slippage in medium vol
        return 0m;
    }

    private decimal CalculateLiquiditySlippage(OptionQuote quote, decimal contractCount)
    {
        // Slippage due to poor liquidity
        var spreadRatio = (quote.Ask - quote.Bid) / quote.Mid;
        if (spreadRatio > 0.10m) return quote.Mid * 0.005m * contractCount / 10m; // Wide spread penalty
        return 0m;
    }

    private decimal CalculateAssignmentProbability(OrderLeg leg, OptionQuote quote, ChainSnapshot marketData)
    {
        // Simplified assignment probability calculation
        if (leg.OptionType == OptionType.Call)
        {
            var intrinsicValue = Math.Max(0, marketData.UnderlyingPrice - leg.Strike);
            return intrinsicValue > 0 ? 0.1m : 0.01m; // 10% if ITM, 1% if OTM
        }
        else
        {
            var intrinsicValue = Math.Max(0, leg.Strike - marketData.UnderlyingPrice);
            return intrinsicValue > 0 ? 0.1m : 0.01m; // 10% if ITM, 1% if OTM
        }
    }

    private decimal CalculateMarginRequirement(SpreadOrder order, int contractCount)
    {
        // Simplified margin calculation for spreads
        // Real implementation would use broker-specific margin calculations
        var maxRisk = order.Legs.Where(l => l.Direction == OrderDirection.Sell)
                                .Sum(l => l.Strike * Math.Abs(l.Quantity)) * contractCount * 0.20m; // 20% of notional
        return Math.Max(maxRisk, 2000m * contractCount); // Minimum $2000 per spread
    }

    private decimal CalculateNotionalValue(SpreadOrder order, ChainSnapshot marketData, int contractCount)
    {
        return order.Legs.Sum(leg =>
        {
            var optionQuote = GetOptionQuote(marketData, leg.Strike, leg.Expiry, leg.OptionType);
            return optionQuote?.Mid * Math.Abs(leg.Quantity) * contractCount ?? 0m;
        });
    }

    private OptionQuote? GetOptionQuote(ChainSnapshot marketData, decimal strike, DateTime expiry, OptionType optionType)
    {
        // Simplified option quote lookup - in production would use sophisticated matching
        return marketData.Options.FirstOrDefault(o => 
            o.Strike == strike && 
            o.Expiry.Date == expiry.Date && 
            o.OptionType == optionType);
    }
}

/// <summary>
/// Trading cost configuration parameters
/// </summary>
public class TradingCostConfig
{
    // Commission structure
    public decimal CommissionPerContract { get; set; } = 0.65m; // $0.65 per contract
    public decimal BaseTradeFee { get; set; } = 0.50m; // $0.50 base fee per trade
    public decimal MinimumCommission { get; set; } = 1.00m; // $1.00 minimum
    public decimal ComplexOrderMultiplier { get; set; } = 1.25m; // 25% premium for multi-leg orders

    // Bid-ask spread crossing
    public decimal BidAskCrossingRate { get; set; } = 0.5m; // Cross 50% of spread on average

    // Assignment and exercise
    public decimal AssignmentFee { get; set; } = 19.95m; // $19.95 assignment fee

    // Financing
    public decimal InterestRate { get; set; } = 0.05m; // 5% annual interest rate

    // Slippage factors
    public decimal BaseSlippageRate { get; set; } = 0.001m; // 0.1% base slippage
    public decimal VolatilitySlippageMultiplier { get; set; } = 0.5m; // 50% increase per 100% vol
}

/// <summary>
/// Comprehensive trading cost result
/// </summary>
public class TradingCostResult
{
    public string OrderId { get; set; } = "";
    public int ContractCount { get; set; }
    public DateTime Timestamp { get; set; }

    // Detailed cost breakdown
    public decimal BidAskSpreadCost { get; set; }
    public decimal CommissionCost { get; set; }
    public decimal SlippageCost { get; set; }
    public decimal AssignmentRiskCost { get; set; }
    public decimal LiquidityAdjustment { get; set; }
    public decimal FinancingCost { get; set; }

    // Total cost metrics
    public decimal TotalCost { get; set; }
    public decimal CostPercentage { get; set; } // As percentage of notional value

    // Cost efficiency metrics
    public decimal CostPerContract => ContractCount > 0 ? TotalCost / ContractCount : 0m;
    public decimal EffectiveCostRatio => CostPercentage; // For comparison with expected returns
}

/// <summary>
/// Market conditions for cost calculation context
/// </summary>
public class MarketConditions
{
    public decimal VIX { get; set; }
    public decimal RealizedVolatility { get; set; }
    public decimal CorrelationToSPY { get; set; }
    public TimeSpan TimeToClose { get; set; }
    public bool IsEconomicEvent { get; set; }
    public decimal VolumeMultiplier { get; set; } = 1.0m; // Relative to average volume
}

/// <summary>
/// Option quote with market data
/// </summary>
public class OptionQuote
{
    public decimal Strike { get; set; }
    public DateTime Expiry { get; set; }
    public OptionType OptionType { get; set; }
    public decimal Bid { get; set; }
    public decimal Ask { get; set; }
    public decimal Mid => (Bid + Ask) / 2;
    public decimal ImpliedVolatility { get; set; }
    public int OpenInterest { get; set; }
    public int Volume { get; set; }
    public decimal Delta { get; set; }
    public decimal Gamma { get; set; }
    public decimal Theta { get; set; }
    public decimal Vega { get; set; }
}