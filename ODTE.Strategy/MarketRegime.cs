namespace ODTE.Strategy
{
    /// <summary>
    /// Market regime classification for 0DTE options trading
    /// Based on 20-year analysis of volatility patterns and strategy performance
    /// </summary>
    public enum MarketRegimeType
    {
        /// <summary>
        /// Calm market: VIX < 15, low realized volatility, stable trends
        /// Strategy focus: Iron Condors, higher position sizing
        /// </summary>
        Calm,

        /// <summary>
        /// Mixed market: VIX 15-25, moderate volatility, uncertain direction
        /// Strategy focus: Balanced approach, medium position sizing
        /// </summary>
        Mixed,

        /// <summary>
        /// Convex market: VIX 25-40, high volatility, strong trends/reversals
        /// Strategy focus: Credit BWB, reduced position sizing, protection strategies
        /// </summary>
        Convex,

        /// <summary>
        /// Crisis market: VIX > 40, extreme volatility, major market disruption
        /// Strategy focus: Tail protection, minimal risk, capital preservation
        /// </summary>
        Crisis
    }
}