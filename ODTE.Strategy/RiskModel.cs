using ODTE.Strategy.Interfaces;

namespace ODTE.Strategy
{
    /// <summary>
    /// Risk calculation implementation for ODTE strategies
    /// Implements MaxPotentialLoss formulas for Iron Condor and Credit BWB
    /// </summary>
    public static class RiskModel
    {
        /// <summary>
        /// Calculate Maximum Potential Loss for Iron Condor strategy
        /// Formula: Max(CallWingWidth - NetCredit, PutWingWidth - NetCredit) * multiplier
        /// </summary>
        /// <param name="netCredit">Net credit received per contract</param>
        /// <param name="putWing">Put wing width (short put - long put)</param>
        /// <param name="callWing">Call wing width (long call - short call)</param>
        /// <param name="multiplier">Contract multiplier (100 for equities)</param>
        /// <returns>Maximum potential loss in dollars</returns>
        public static decimal MaxPotentialLossIC(
            decimal netCredit,
            decimal putWing,
            decimal callWing,
            int multiplier = 100)
        {
            // Call-side loss if price >> short call:
            var callLoss = (callWing - netCredit) * multiplier;

            // Put-side loss if price << short put:
            var putLoss = (putWing - netCredit) * multiplier;

            return Math.Max(callLoss, putLoss);
        }

        /// <summary>
        /// Calculate Maximum Potential Loss for Credit Broken-Wing Butterfly
        /// Formula: ((FarWing - NarrowWing) * multiplier) - (NetCredit * multiplier)
        /// </summary>
        /// <param name="netCredit">Net credit received per contract</param>
        /// <param name="narrowWing">Narrow wing distance (|Body - NearWing|)</param>
        /// <param name="farWing">Far wing distance (|FarWing - Body|)</param>
        /// <param name="multiplier">Contract multiplier (100 for equities)</param>
        /// <returns>Maximum potential loss in dollars (clamped to zero minimum)</returns>
        public static decimal MaxPotentialLossBwb(
            decimal netCredit,
            decimal narrowWing,
            decimal farWing,
            int multiplier = 100)
        {
            var core = ((farWing - narrowWing) * multiplier) - (netCredit * multiplier);

            // If geometry yields negative loss (rare), clamp to zero
            return Math.Max(0m, core);
        }

        /// <summary>
        /// Calculate MaxPotentialLoss for any strategy shape using interface
        /// </summary>
        /// <param name="shape">Strategy geometry definition</param>
        /// <param name="netCredit">Net credit received</param>
        /// <param name="multiplier">Contract multiplier</param>
        /// <returns>Maximum potential loss</returns>
        public static decimal MaxPotentialLoss(IStrategyShape shape, decimal netCredit, int multiplier = 100)
        {
            return shape.Name switch
            {
                "IronCondor" => CalculateIronCondorMPL(shape, netCredit, multiplier),
                "CreditBWB" => CalculateCreditBWBMPL(shape, netCredit, multiplier),
                _ => throw new NotSupportedException($"MaxPotentialLoss calculation not implemented for strategy: {shape.Name}")
            };
        }

        /// <summary>
        /// Calculate margin requirement for strategy shape
        /// </summary>
        public static decimal MarginRequired(IStrategyShape shape)
        {
            // Simplified margin calculation - in practice this would be more complex
            return shape.Name switch
            {
                "IronCondor" => CalculateIronCondorMargin(shape),
                "CreditBWB" => CalculateCreditBWBMargin(shape),
                _ => 0m
            };
        }

        /// <summary>
        /// Generate risk profile for strategy
        /// </summary>
        public static RiskProfile Profile(IStrategyShape shape)
        {
            // Simplified implementation - real version would calculate actual Greeks
            var profile = new RiskProfile();

            // Calculate aggregate Greeks from legs
            foreach (var leg in shape.Legs)
            {
                var multiplier = leg.Action == "Buy" ? leg.Quantity : -leg.Quantity;
                profile.Delta += (decimal)(leg.Delta * multiplier);
                profile.Gamma += (decimal)(leg.Gamma * multiplier);
                profile.Theta += (decimal)(leg.Theta * multiplier);
                profile.Vega += (decimal)(leg.Vega * multiplier);
            }

            return profile;
        }

        private static decimal CalculateIronCondorMPL(IStrategyShape shape, decimal netCredit, int multiplier)
        {
            // Extract wing widths from legs
            var calls = shape.Legs.Where(l => l.OptionType == "Call").OrderBy(l => l.Strike).ToList();
            var puts = shape.Legs.Where(l => l.OptionType == "Put").OrderByDescending(l => l.Strike).ToList();

            if (calls.Count != 2 || puts.Count != 2)
                throw new ArgumentException("Iron Condor must have exactly 2 calls and 2 puts");

            var callWing = (decimal)(calls[1].Strike - calls[0].Strike);
            var putWing = (decimal)(puts[0].Strike - puts[1].Strike);

            return MaxPotentialLossIC(netCredit, putWing, callWing, multiplier);
        }

        private static decimal CalculateCreditBWBMPL(IStrategyShape shape, decimal netCredit, int multiplier)
        {
            // Extract BWB structure from legs
            var legs = shape.Legs.OrderBy(l => l.Strike).ToList();

            if (legs.Count != 3)
                throw new ArgumentException("Credit BWB must have exactly 3 legs");

            // Identify body, near wing, and far wing
            var body = legs[1].Strike; // Middle strike is the body
            var nearWing = Math.Abs(legs[1].Strike - legs[0].Strike);
            var farWing = Math.Abs(legs[2].Strike - legs[1].Strike);

            return MaxPotentialLossBwb(netCredit, (decimal)nearWing, (decimal)farWing, multiplier);
        }

        private static decimal CalculateIronCondorMargin(IStrategyShape shape)
        {
            // Simplified: Use wider wing as margin requirement
            var calls = shape.Legs.Where(l => l.OptionType == "Call").OrderBy(l => l.Strike).ToList();
            var puts = shape.Legs.Where(l => l.OptionType == "Put").OrderByDescending(l => l.Strike).ToList();

            var callWing = calls.Count >= 2 ? (decimal)(calls[1].Strike - calls[0].Strike) : 0;
            var putWing = puts.Count >= 2 ? (decimal)(puts[0].Strike - puts[1].Strike) : 0;

            return Math.Max(callWing, putWing) * 100; // $100 per point
        }

        private static decimal CalculateCreditBWBMargin(IStrategyShape shape)
        {
            // Simplified: Use far wing as margin requirement
            var legs = shape.Legs.OrderBy(l => l.Strike).ToList();
            if (legs.Count < 3) return 0;

            var farWing = (decimal)Math.Abs(legs[2].Strike - legs[1].Strike);
            return farWing * 100; // $100 per point
        }
    }

    /// <summary>
    /// Implementation of IRiskModel interface using static RiskModel methods
    /// </summary>
    public class DefaultRiskModel : IRiskModel
    {
        public decimal MaxPotentialLoss(IStrategyShape shape, decimal netCredit, int multiplier = 100)
        {
            return RiskModel.MaxPotentialLoss(shape, netCredit, multiplier);
        }

        public decimal MarginRequired(IStrategyShape shape)
        {
            return RiskModel.MarginRequired(shape);
        }

        public RiskProfile Profile(IStrategyShape shape)
        {
            return RiskModel.Profile(shape);
        }
    }
}