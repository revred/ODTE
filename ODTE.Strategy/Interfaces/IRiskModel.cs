namespace ODTE.Strategy.Interfaces
{
    /// <summary>
    /// Risk calculation interface for strategy evaluation
    /// </summary>
    public interface IRiskModel
    {
        /// <summary>
        /// Calculate maximum potential loss for a strategy shape
        /// </summary>
        /// <param name="shape">Strategy geometry definition</param>
        /// <param name="netCredit">Net credit received from opening the position</param>
        /// <param name="multiplier">Contract multiplier (100 for equities, 10 for minis, 1 for micros)</param>
        /// <returns>Maximum potential loss in dollars</returns>
        decimal MaxPotentialLoss(IStrategyShape shape, decimal netCredit, int multiplier = 100);

        /// <summary>
        /// Calculate margin requirement for a strategy shape
        /// </summary>
        /// <param name="shape">Strategy geometry definition</param>
        /// <returns>Required margin in dollars</returns>
        decimal MarginRequired(IStrategyShape shape);

        /// <summary>
        /// Generate risk profile for strategy
        /// </summary>
        /// <param name="shape">Strategy geometry definition</param>
        /// <returns>Risk profile with Greeks and stress points</returns>
        RiskProfile Profile(IStrategyShape shape);
    }

    /// <summary>
    /// Risk profile containing Greeks and stress analysis
    /// </summary>
    public class RiskProfile
    {
        public decimal Delta { get; set; }
        public decimal Gamma { get; set; }
        public decimal Theta { get; set; }
        public decimal Vega { get; set; }
        public decimal[] StressPoints { get; set; } = Array.Empty<decimal>();
        public decimal[] StressPnL { get; set; } = Array.Empty<decimal>();
    }
}