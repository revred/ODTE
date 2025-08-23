using System;
using System.Collections.Generic;
using ODTE.Contracts.Data;

namespace ODTE.Contracts.Orders
{
    /// <summary>
    /// Single order leg for options trades
    /// </summary>
    public class OrderLeg
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal Strike { get; set; }
        public DateTime Expiration { get; set; }
        public OptionType OptionType { get; set; }
        public OrderSide Side { get; set; }
        public int Quantity { get; set; }
        public decimal? LimitPrice { get; set; }
        public OrderType OrderType { get; set; }
    }

    /// <summary>
    /// Single options order
    /// </summary>
    public class Order
    {
        public string OrderId { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public OrderLeg Leg { get; set; } = new();
        public TimeInForce TimeInForce { get; set; } = TimeInForce.Day;
        public string Strategy { get; set; } = string.Empty;
    }

    /// <summary>
    /// Multi-leg options order (spreads, straddles, etc.)
    /// </summary>
    public class MultiLegOrder
    {
        public string OrderId { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public List<OrderLeg> Legs { get; set; } = new();
        public decimal? NetDebit { get; set; }
        public decimal? NetCredit { get; set; }
        public TimeInForce TimeInForce { get; set; } = TimeInForce.Day;
        public string Strategy { get; set; } = string.Empty;
    }

    /// <summary>
    /// Order execution result
    /// </summary>
    public class FillResult
    {
        public string OrderId { get; set; } = string.Empty;
        public DateTime FillTime { get; set; }
        public FillStatus Status { get; set; }
        public decimal FilledQuantity { get; set; }
        public decimal AveragePrice { get; set; }
        public decimal Commission { get; set; }
        public decimal Slippage { get; set; }
        public List<ExecutionDetail> Executions { get; set; } = new();
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// Individual execution detail
    /// </summary>
    public class ExecutionDetail
    {
        public DateTime Time { get; set; }
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public string Venue { get; set; } = string.Empty;
    }

    /// <summary>
    /// Strategy execution result
    /// </summary>
    public class ExecutionResult
    {
        public DateTime Date { get; set; }
        public string StrategyName { get; set; } = string.Empty;
        public decimal PnL { get; set; }
        public decimal Commission { get; set; }
        public List<FillResult> Fills { get; set; } = new();
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// Order side enumeration
    /// </summary>
    public enum OrderSide
    {
        Buy,
        Sell
    }

    /// <summary>
    /// Order type enumeration
    /// </summary>
    public enum OrderType
    {
        Market,
        Limit,
        Stop,
        StopLimit
    }

    /// <summary>
    /// Time in force enumeration
    /// </summary>
    public enum TimeInForce
    {
        Day,
        GTC,  // Good Till Canceled
        IOC,  // Immediate or Cancel
        FOK   // Fill or Kill
    }

    /// <summary>
    /// Fill status enumeration
    /// </summary>
    public enum FillStatus
    {
        Pending,
        PartiallyFilled,
        Filled,
        Canceled,
        Rejected,
        Expired
    }
}