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
}