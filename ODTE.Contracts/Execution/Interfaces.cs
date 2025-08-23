using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ODTE.Contracts.Data;
using ODTE.Contracts.Orders;
using ODTE.Contracts.Strategy;

namespace ODTE.Contracts.Execution
{
    /// <summary>
    /// Fill engine interface for trade execution
    /// </summary>
    public interface IFillEngine
    {
        Task<FillResult> ExecuteOrderAsync(Order order, ChainSnapshot snapshot);
        Task<FillResult> ExecuteMultiLegOrderAsync(MultiLegOrder order, ChainSnapshot snapshot);
        decimal EstimateSlippage(Order order, OptionsQuote quote);
    }

    /// <summary>
    /// Realistic fill engine for market microstructure simulation
    /// </summary>
    public interface IRealisticFillEngine : IFillEngine
    {
        void ConfigureTradingCosts(TradingCostSettings costs);
        FillQuality AssessFillQuality(FillResult result);
        Task<List<FillResult>> BatchExecuteAsync(List<Order> orders, ChainSnapshot snapshot);
    }

    /// <summary>
    /// Strategy execution coordinator
    /// </summary>
    public interface IStrategyExecutor
    {
        Task<ExecutionResult> ExecuteStrategyAsync(IStrategy strategy, MarketConditions conditions);
        Task<List<ExecutionResult>> RunBacktestAsync(IStrategy strategy, DateTime start, DateTime end);
        void RegisterRiskManager(IRiskManager riskManager);
    }

    /// <summary>
    /// Trading cost configuration
    /// </summary>
    public class TradingCostSettings
    {
        public decimal CommissionPerContract { get; set; } = 1.25m;
        public decimal RegulatoryFees { get; set; } = 0.045m;
        public decimal SlippageBps { get; set; } = 5.0m;
        public decimal LiquidityPenalty { get; set; } = 0.10m;
    }

    /// <summary>
    /// Fill quality assessment
    /// </summary>
    public enum FillQuality
    {
        Excellent,
        Good, 
        Fair,
        Poor
    }
}