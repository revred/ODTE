using System;

namespace ODTE.Contracts.Data
{
    /// <summary>
    /// Options chain snapshot for a specific expiration date
    /// </summary>
    public class ChainSnapshot
    {
        public DateTime Date { get; set; }
        public DateTime Expiration { get; set; }
        public decimal UnderlyingPrice { get; set; }
        public decimal ImpliedVolatility { get; set; }
        public Dictionary<decimal, OptionsQuote> Calls { get; set; } = new();
        public Dictionary<decimal, OptionsQuote> Puts { get; set; } = new();
    }

    /// <summary>
    /// Individual options quote with Greeks and market data
    /// </summary>
    public class OptionsQuote
    {
        public decimal Strike { get; set; }
        public DateTime Expiration { get; set; }
        public OptionType OptionType { get; set; }
        public decimal Bid { get; set; }
        public decimal Ask { get; set; }
        public decimal Last { get; set; }
        public decimal Mid => (Bid + Ask) / 2;
        public long Volume { get; set; }
        public long OpenInterest { get; set; }
        
        // Greeks
        public decimal Delta { get; set; }
        public decimal Gamma { get; set; }
        public decimal Theta { get; set; }
        public decimal Vega { get; set; }
        public decimal Rho { get; set; }
        public decimal ImpliedVolatility { get; set; }
    }

    /// <summary>
    /// Option type enumeration
    /// </summary>
    public enum OptionType
    {
        Call,
        Put
    }

    /// <summary>
    /// Market conditions and regime information
    /// </summary>
    public class MarketConditions
    {
        public DateTime Date { get; set; }
        public decimal VIX { get; set; }
        public decimal VIX9D { get; set; }
        public decimal SpotPrice { get; set; }
        public decimal VWAP { get; set; }
        public bool IsGammaHour { get; set; }
        public bool IsExpiration { get; set; }
        public decimal TrendStrength { get; set; }
        public MarketRegime Regime { get; set; }
    }

    /// <summary>
    /// Market regime classification
    /// </summary>
    public enum MarketRegime
    {
        Calm,
        Trending,
        Volatile,
        Crisis
    }

    /// <summary>
    /// Unified date range representation for all ODTE projects
    /// Consolidates duplicate DateRange classes from multiple namespaces
    /// </summary>
    public class DateRange
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        
        // Enhanced functionality from ODTE.Historical version
        public int Days => (EndDate - StartDate).Days + 1;
        public bool Contains(DateTime date) => date >= StartDate && date <= EndDate;
        
        // Backward compatibility aliases for ODTE.Historical version
        public DateTime Start { get => StartDate; set => StartDate = value; }
        public DateTime End { get => EndDate; set => EndDate = value; }
        
        // Business logic methods
        public IEnumerable<DateTime> GetTradingDays()
        {
            var current = StartDate;
            while (current <= EndDate)
            {
                // Skip weekends (basic business day logic)
                if (current.DayOfWeek != DayOfWeek.Saturday && current.DayOfWeek != DayOfWeek.Sunday)
                {
                    yield return current;
                }
                current = current.AddDays(1);
            }
        }
        
        public bool IsValid => EndDate >= StartDate;
        
        public override string ToString() => $"{StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd} ({Days} days)";
    }
}