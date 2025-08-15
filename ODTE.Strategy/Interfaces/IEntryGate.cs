namespace ODTE.Strategy.Interfaces
{
    /// <summary>
    /// Entry gate for validating trade entry conditions
    /// </summary>
    public interface IEntryGate
    {
        /// <summary>
        /// Determine if a trade should be allowed based on market conditions and strategy inputs
        /// </summary>
        /// <param name="mkt">Current market snapshot</param>
        /// <param name="inp">Strategy input parameters</param>
        /// <param name="reason">Output reason for allow/deny decision</param>
        /// <returns>True if trade should be allowed, false otherwise</returns>
        bool Allow(MarketSnapshot mkt, StrategyInputs inp, out string reason);
    }

    /// <summary>
    /// Market snapshot for entry gate evaluation
    /// </summary>
    public class MarketSnapshot
    {
        public DateTime Timestamp { get; set; }
        public decimal UnderlyingPrice { get; set; }
        public decimal VIX { get; set; }
        public decimal IVRank { get; set; }
        public decimal TrendScore { get; set; }
        public string MarketRegime { get; set; } = "";
    }

    /// <summary>
    /// Strategy inputs for entry gate evaluation
    /// </summary>
    public class StrategyInputs
    {
        public string StrategyName { get; set; } = "";
        public decimal PositionSize { get; set; }
        public decimal MaxRisk { get; set; }
        public decimal DeltaTarget { get; set; }
        public decimal CreditTarget { get; set; }
    }
}