using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ODTE.Contracts.Data;
using ODTE.Contracts.Orders;

namespace ODTE.Contracts.Strategy
{
    /// <summary>
    /// Core strategy interface
    /// </summary>
    public interface IStrategy
    {
        string Name { get; }
        Task<List<Order>> GenerateOrdersAsync(MarketConditions conditions, ChainSnapshot snapshot);
        Task<bool> ShouldExitAsync(MarketConditions conditions, PortfolioState portfolio);
        Task<List<Order>> GenerateExitOrdersAsync(PortfolioState portfolio, ChainSnapshot snapshot);
    }

    /// <summary>
    /// VIX hedging manager interface
    /// </summary>
    public interface IVIXHedgeManager
    {
        Task<HedgeRequirement> AssessHedgeNeedAsync(PortfolioState portfolio, MarketConditions conditions);
        Task<List<Order>> GenerateHedgeOrdersAsync(HedgeRequirement requirement, ChainSnapshot vixSnapshot);
        bool IsHedgeActive(PortfolioState portfolio);
    }

    /// <summary>
    /// Risk management interface
    /// </summary>
    public interface IRiskManager
    {
        bool ValidateOrder(Order order, PortfolioState portfolio);
        bool ValidateMultiLegOrder(MultiLegOrder order, PortfolioState portfolio);
        decimal CalculateMaxLoss(Order order, OptionsQuote quote);
        decimal GetPositionSizeLimit(string strategy, decimal accountValue);
    }

    /// <summary>
    /// Strategy optimization interface
    /// </summary>
    public interface IStrategyOptimizer
    {
        Task<OptimizationResult> OptimizeAsync(IStrategy strategy, DateTime start, DateTime end);
        Task<List<StrategyCandidate>> GenerateCandidatesAsync(IStrategy baseStrategy, int populationSize);
        Task<IStrategy> EvolveAsync(List<StrategyCandidate> population, int generations);
    }

    /// <summary>
    /// Portfolio state tracking
    /// </summary>
    public class PortfolioState
    {
        public DateTime Date { get; set; }
        public decimal Cash { get; set; }
        public decimal TotalValue { get; set; }
        public List<Position> Positions { get; set; } = new();
        public decimal DayPnL { get; set; }
        public decimal TotalPnL { get; set; }
        public decimal MaxDrawdown { get; set; }
    }

    /// <summary>
    /// Individual position
    /// </summary>
    public class Position
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal Strike { get; set; }
        public DateTime Expiration { get; set; }
        public OptionType OptionType { get; set; }
        public int Quantity { get; set; }
        public decimal AvgPrice { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal PnL { get; set; }
        public DateTime EntryTime { get; set; }
    }

    /// <summary>
    /// VIX hedge requirement assessment
    /// </summary>
    public class HedgeRequirement
    {
        public bool IsRequired { get; set; }
        public decimal HedgeRatio { get; set; }
        public HedgeType RecommendedType { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Strategy optimization result
    /// </summary>
    public class OptimizationResult
    {
        public IStrategy OptimalStrategy { get; set; } = null!;
        public decimal FinalReturn { get; set; }
        public decimal SharpeRatio { get; set; }
        public decimal MaxDrawdown { get; set; }
        public int TotalTrades { get; set; }
        public decimal WinRate { get; set; }
    }

    /// <summary>
    /// Strategy candidate for genetic algorithms
    /// </summary>
    public class StrategyCandidate
    {
        public IStrategy Strategy { get; set; } = null!;
        public decimal Fitness { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    /// <summary>
    /// Hedge type enumeration
    /// </summary>
    public enum HedgeType
    {
        None,
        VIXCalls,
        VIXPuts,
        SPYPuts,
        Collar
    }
}