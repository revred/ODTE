using System;
using System.Collections.Generic;

namespace ODTE.Strategy.Interfaces
{
    /// <summary>
    /// Defines the geometric structure and properties of a trading strategy
    /// </summary>
    public interface IStrategyShape
    {
        /// <summary>
        /// Name of the strategy (e.g., "IronCondor", "CreditBWB")
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// Exercise style for pricing (European/American)
        /// </summary>
        ExerciseStyle Style { get; }
        
        /// <summary>
        /// Option legs that compose this strategy
        /// </summary>
        IReadOnlyList<OptionLeg> Legs { get; }
    }

    /// <summary>
    /// Exercise style for option pricing
    /// </summary>
    public enum ExerciseStyle
    {
        European,
        American
    }
}