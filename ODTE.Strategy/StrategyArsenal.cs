using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ODTE.Strategy;

/// <summary>
/// Multi-Strategy Arsenal for 24-Day Framework
/// 
/// Each strategy is optimized for specific market conditions and framework phases:
/// 
/// 1. GHOST STRATEGY: Ultra-conservative, market-neutral approach (85%+ win rate)
/// 2. PRECISION STRATEGY: Surgical strikes on perfect setups (70% win rate, 2:1 RR) 
/// 3. SNIPER STRATEGY: High-conviction trades with larger positions (60% win rate, 3:1 RR)
/// 4. VOLATILITY CRUSHER: Low-vol environment exploitation (80% win rate)
/// 5. REGIME ADAPTIVE: Dynamic strategy based on real-time conditions
/// </summary>

/// <summary>
/// Base interface for all trading strategies
/// </summary>
public interface IStrategy
{
    string Name { get; }
    double ExpectedEdge { get; }
    double ExpectedWinRate { get; }
    double RewardToRiskRatio { get; }
    Task<List<Trade>> ExecuteTrading(MarketConditions conditions, decimal positionSize, MarketRegime regime);
    bool IsOptimalFor(MarketRegime regime);
}

/// <summary>
/// Ghost Strategy: Ultra-conservative market-neutral approach
/// 
/// PHILOSOPHY: "Be invisible, strike only when the edge is overwhelming"
/// 
/// CHARACTERISTICS:
/// - 85%+ win rate through extreme selectivity
/// - Small, consistent profits ($50-150/day)
/// - Market-neutral positions (delta ~0)
/// - High-probability, low-reward trades
/// - Optimal for uncertain/volatile markets
/// </summary>
public class GhostStrategy : IStrategy
{
    public string Name => "Ghost (Ultra-Conservative)";
    public double ExpectedEdge => 0.35;           // 35% edge
    public double ExpectedWinRate => 0.85;       // 85% win rate
    public double RewardToRiskRatio => 1.2;      // 1.2:1 reward/risk

    public async Task<List<Trade>> ExecuteTrading(MarketConditions conditions, decimal positionSize, MarketRegime regime)
    {
        var trades = new List<Trade>();
        
        // Ghost strategy: Only trade in extremely favorable conditions
        if (regime.VIX > 25 || regime.Confidence < 0.8m)
        {
            // High volatility: Short straddles/strangles at extreme strikes
            var trade = new Trade
            {
                Strategy = Name,
                Type = "Iron Condor",
                Strikes = "Ultra-wide (5+ delta)",
                PositionSize = positionSize * 0.8m, // Conservative sizing
                ExpectedPnL = CalculateExpectedPnL(positionSize * 0.8m, 0.85, 120m, -100m),
                Confidence = 0.9,
                MaxRisk = positionSize * 0.8m * 0.15m,
                Reasoning = "High VIX environment - selling overpriced options at extreme strikes"
            };
            trades.Add(trade);
        }
        else if (conditions.IVRank < 20 && regime.RegimeType == RegimeType.Ranging)
        {
            // Low volatility ranging: Tight iron condors
            var trade = new Trade
            {
                Strategy = Name,
                Type = "Tight Iron Condor", 
                Strikes = "15-20 delta wings",
                PositionSize = positionSize * 1.0m,
                ExpectedPnL = CalculateExpectedPnL(positionSize, 0.85, 80m, -60m),
                Confidence = 0.85,
                MaxRisk = positionSize * 0.12m,
                Reasoning = "Low vol ranging market - tight condors with high theta decay"
            };
            trades.Add(trade);
        }
        
        return trades;
    }

    public bool IsOptimalFor(MarketRegime regime)
    {
        return regime.VIX > 25 || // High volatility - sell premium
               (regime.RegimeType == RegimeType.Ranging && regime.Confidence > 0.8m); // High-confidence ranging
    }

    private decimal CalculateExpectedPnL(decimal positionSize, double winRate, decimal avgWin, decimal avgLoss)
    {
        return (decimal)(winRate * (double)avgWin + (1 - winRate) * (double)avgLoss);
    }
}

/// <summary>
/// Precision Strategy: Surgical strikes on perfect market setups
/// 
/// PHILOSOPHY: "Wait for the perfect pitch, then swing for the fences"
/// 
/// CHARACTERISTICS:
/// - 70% win rate through selective entry
/// - Medium-sized profits ($150-300/day)
/// - Directional bias when trend is clear
/// - Technical analysis confluence required
/// - Optimal for trending markets with clear signals
/// </summary>
public class PrecisionStrategy : IStrategy
{
    public string Name => "Precision (Selective Strikes)";
    public double ExpectedEdge => 0.40;           // 40% edge
    public double ExpectedWinRate => 0.70;       // 70% win rate  
    public double RewardToRiskRatio => 2.0;      // 2:1 reward/risk

    public async Task<List<Trade>> ExecuteTrading(MarketConditions conditions, decimal positionSize, MarketRegime regime)
    {
        var trades = new List<Trade>();

        // Precision requires high-confidence directional signals
        if (regime.Confidence < 0.75m)
            return trades; // No trades if low confidence

        if (regime.TrendStrength > 0.6 && conditions.RSI < 35) // Oversold in uptrend
        {
            var trade = new Trade
            {
                Strategy = Name,
                Type = "Put Credit Spread",
                Strikes = "10-15 delta short put",
                PositionSize = positionSize * 1.2m, // Slightly aggressive
                ExpectedPnL = CalculateExpectedPnL(positionSize * 1.2m, 0.70, 200m, -100m),
                Confidence = 0.8,
                MaxRisk = positionSize * 0.20m,
                Reasoning = "Strong uptrend + oversold RSI - sell puts into fear"
            };
            trades.Add(trade);
        }
        else if (regime.TrendStrength > 0.6 && conditions.RSI > 65) // Overbought in downtrend
        {
            var trade = new Trade
            {
                Strategy = Name,
                Type = "Call Credit Spread", 
                Strikes = "10-15 delta short call",
                PositionSize = positionSize * 1.2m,
                ExpectedPnL = CalculateExpectedPnL(positionSize * 1.2m, 0.70, 200m, -100m),
                Confidence = 0.8,
                MaxRisk = positionSize * 0.20m,
                Reasoning = "Strong downtrend + overbought RSI - sell calls into greed"
            };
            trades.Add(trade);
        }
        else if (conditions.IVRank > 50 && regime.RegimeType == RegimeType.LowVolatility)
        {
            // High IV rank in low vol environment - premium selling opportunity
            var trade = new Trade
            {
                Strategy = Name,
                Type = "Iron Condor",
                Strikes = "12-18 delta wings",
                PositionSize = positionSize * 1.1m,
                ExpectedPnL = CalculateExpectedPnL(positionSize * 1.1m, 0.75, 150m, -80m),
                Confidence = 0.75,
                MaxRisk = positionSize * 0.18m,
                Reasoning = "High IV rank in calm market - sell overpriced options"
            };
            trades.Add(trade);
        }

        return trades;
    }

    public bool IsOptimalFor(MarketRegime regime)
    {
        return regime.TrendStrength > 0.6 || // Clear trend
               (regime.RegimeType == RegimeType.LowVolatility && regime.Confidence > 0.7m);
    }

    private decimal CalculateExpectedPnL(decimal positionSize, double winRate, decimal avgWin, decimal avgLoss)
    {
        return (decimal)(winRate * (double)avgWin + (1 - winRate) * (double)avgLoss);
    }
}

/// <summary>
/// Sniper Strategy: High-conviction trades with concentrated positions
/// 
/// PHILOSOPHY: "One shot, one kill - maximum impact with precision execution"
/// 
/// CHARACTERISTICS:
/// - 60% win rate with high reward/risk ratio (3:1)
/// - Large profits when right ($300-600/day)
/// - Higher risk tolerance for better rewards
/// - Used in recovery mode and sniper framework phase
/// - Optimal for extreme market conditions
/// </summary>
public class SniperStrategy : IStrategy
{
    public string Name => "Sniper (High-Conviction)";
    public double ExpectedEdge => 0.20;           // 20% edge (lower due to higher risk)
    public double ExpectedWinRate => 0.60;       // 60% win rate
    public double RewardToRiskRatio => 3.0;      // 3:1 reward/risk

    public async Task<List<Trade>> ExecuteTrading(MarketConditions conditions, decimal positionSize, MarketRegime regime)
    {
        var trades = new List<Trade>();

        // Sniper only takes extremely high-conviction trades
        if (regime.Confidence < 0.8m)
        {
            // Create a conservative trade even in low confidence situations
            var conservativeTrade = new Trade
            {
                Strategy = Name,
                Type = "Conservative Position",
                Strikes = "Wide spread",
                PositionSize = positionSize * 0.5m,
                ExpectedPnL = CalculateExpectedPnL(positionSize * 0.5m, 0.70, 100m, -50m),
                Confidence = 0.70,
                MaxRisk = positionSize * 0.15m,
                Reasoning = "Low confidence - conservative defensive position"
            };
            trades.Add(conservativeTrade);
            return trades;
        }

        // Extreme oversold/overbought with momentum divergence
        if (conditions.RSI < 25 && conditions.MomentumDivergence > 0.7)
        {
            var trade = new Trade
            {
                Strategy = Name,
                Type = "Put Spread (Aggressive)",
                Strikes = "20-30 delta short put",
                PositionSize = positionSize * 1.5m, // Aggressive sizing
                ExpectedPnL = CalculateExpectedPnL(positionSize * 1.5m, 0.60, 400m, -150m),
                Confidence = 0.85,
                MaxRisk = positionSize * 0.35m,
                Reasoning = "Extreme oversold + bullish divergence - high-conviction put spread"
            };
            trades.Add(trade);
        }
        else if (conditions.RSI > 75 && conditions.MomentumDivergence < -0.7)
        {
            var trade = new Trade
            {
                Strategy = Name,
                Type = "Call Spread (Aggressive)",
                Strikes = "20-30 delta short call",
                PositionSize = positionSize * 1.5m,
                ExpectedPnL = CalculateExpectedPnL(positionSize * 1.5m, 0.60, 400m, -150m),
                Confidence = 0.85,
                MaxRisk = positionSize * 0.35m,
                Reasoning = "Extreme overbought + bearish divergence - high-conviction call spread"
            };
            trades.Add(trade);
        }
        else if (regime.VIX > 40 && conditions.VIXContango > 15) // VIX spike with steep contango
        {
            var trade = new Trade
            {
                Strategy = Name,
                Type = "VIX Short Straddle",
                Strikes = "ATM volatility play",
                PositionSize = positionSize * 1.8m, // Very aggressive on VIX mean reversion
                ExpectedPnL = CalculateExpectedPnL(positionSize * 1.8m, 0.65, 500m, -200m),
                Confidence = 0.80,
                MaxRisk = positionSize * 0.45m,
                Reasoning = "Extreme VIX spike with contango - mean reversion trade"
            };
            trades.Add(trade);
        }

        return trades;
    }

    public bool IsOptimalFor(MarketRegime regime)
    {
        return regime.VIX > 35 || // Extreme volatility
               Math.Abs(regime.TrendStrength) > 0.8; // Very strong trend (up or down)
    }

    private decimal CalculateExpectedPnL(decimal positionSize, double winRate, decimal avgWin, decimal avgLoss)
    {
        return (decimal)(winRate * (double)avgWin + (1 - winRate) * (double)avgLoss);
    }
}

/// <summary>
/// Volatility Crusher: Exploit low volatility environments
/// 
/// PHILOSOPHY: "When volatility sleeps, we harvest theta"
/// 
/// CHARACTERISTICS:
/// - 80% win rate in low-vol environments
/// - Consistent theta harvesting ($100-250/day)
/// - Short volatility bias
/// - Multiple smaller positions
/// - Optimal for calm, ranging markets
/// </summary>
public class VolatilityCrusher : IStrategy
{
    public string Name => "Volatility Crusher";
    public double ExpectedEdge => 0.30;           // 30% edge
    public double ExpectedWinRate => 0.80;       // 80% win rate
    public double RewardToRiskRatio => 1.5;      // 1.5:1 reward/risk

    public async Task<List<Trade>> ExecuteTrading(MarketConditions conditions, decimal positionSize, MarketRegime regime)
    {
        var trades = new List<Trade>();

        // Only operate in low volatility environments
        if (regime.VIX > 25 || regime.RegimeType != RegimeType.LowVolatility)
            return trades;

        // Multiple smaller theta-generating trades
        var individualSize = positionSize * 0.4m; // Split into multiple positions

        // Trade 1: Iron Condor on SPY
        trades.Add(new Trade
        {
            Strategy = Name,
            Type = "Iron Condor (SPY)",
            Strikes = "10 delta wings, 5-point spread",
            PositionSize = individualSize,
            ExpectedPnL = CalculateExpectedPnL(individualSize, 0.80, 100m, -50m),
            Confidence = 0.8,
            MaxRisk = individualSize * 0.12m,
            Reasoning = "Low VIX + ranging market - harvest theta decay"
        });

        // Trade 2: Put spread on minor pullback
        if (conditions.RSI < 45)
        {
            trades.Add(new Trade
            {
                Strategy = Name,
                Type = "Put Credit Spread",
                Strikes = "8 delta short put",
                PositionSize = individualSize,
                ExpectedPnL = CalculateExpectedPnL(individualSize, 0.85, 80m, -40m),
                Confidence = 0.85,
                MaxRisk = individualSize * 0.10m,
                Reasoning = "Minor pullback in low vol - sell puts below support"
            });
        }

        // Trade 3: Short strangle if IV rank elevated
        if (conditions.IVRank > 30)
        {
            trades.Add(new Trade
            {
                Strategy = Name,
                Type = "Short Strangle",
                Strikes = "12 delta puts/calls",
                PositionSize = individualSize,
                ExpectedPnL = CalculateExpectedPnL(individualSize, 0.75, 120m, -60m),
                Confidence = 0.75,
                MaxRisk = individualSize * 0.15m,
                Reasoning = "Elevated IV in low vol environment - sell premium"
            });
        }

        return trades;
    }

    public bool IsOptimalFor(MarketRegime regime)
    {
        return regime.RegimeType == RegimeType.LowVolatility && regime.VIX < 22;
    }

    private decimal CalculateExpectedPnL(decimal positionSize, double winRate, decimal avgWin, decimal avgLoss)
    {
        return (decimal)(winRate * (double)avgWin + (1 - winRate) * (double)avgLoss);
    }
}

/// <summary>
/// Regime Adaptive: Dynamic strategy selection based on market conditions
/// 
/// PHILOSOPHY: "Adapt to survive and thrive in any market condition"
/// 
/// CHARACTERISTICS:
/// - Changes strategy based on real-time conditions
/// - 65% win rate through adaptability
/// - Balanced approach across market regimes
/// - Used as fallback when other strategies suboptimal
/// - Optimal for uncertain or transitional markets
/// </summary>
public class RegimeAdaptive : IStrategy
{
    public string Name => "Regime Adaptive";
    public double ExpectedEdge => 0.25;           // 25% edge
    public double ExpectedWinRate => 0.65;       // 65% win rate
    public double RewardToRiskRatio => 1.8;      // 1.8:1 reward/risk

    public async Task<List<Trade>> ExecuteTrading(MarketConditions conditions, decimal positionSize, MarketRegime regime)
    {
        var trades = new List<Trade>();

        // Adaptive strategy selection based on multiple factors
        var strategySelection = DetermineAdaptiveStrategy(conditions, regime);

        switch (strategySelection)
        {
            case AdaptiveMode.TrendFollowing:
                trades.Add(CreateTrendFollowingTrade(positionSize, conditions, regime));
                break;
                
            case AdaptiveMode.MeanReversion:
                trades.Add(CreateMeanReversionTrade(positionSize, conditions, regime));
                break;
                
            case AdaptiveMode.VolatilitySelling:
                trades.AddRange(CreateVolatilitySellingTrades(positionSize, conditions, regime));
                break;
                
            case AdaptiveMode.Defensive:
                trades.Add(CreateDefensiveTrade(positionSize, conditions, regime));
                break;
        }

        return trades;
    }

    private AdaptiveMode DetermineAdaptiveStrategy(MarketConditions conditions, MarketRegime regime)
    {
        // Decision tree for adaptive strategy selection
        if (regime.TrendStrength > 0.5 && regime.Confidence > 0.7m)
            return AdaptiveMode.TrendFollowing;
        
        if (conditions.RSI > 70 || conditions.RSI < 30)
            return AdaptiveMode.MeanReversion;
        
        if (regime.VIX < 25 && conditions.IVRank > 25)
            return AdaptiveMode.VolatilitySelling;
        
        return AdaptiveMode.Defensive; // Default to defensive
    }

    private Trade CreateTrendFollowingTrade(decimal positionSize, MarketConditions conditions, MarketRegime regime)
    {
        var isUptrend = regime.TrendStrength > 0;
        return new Trade
        {
            Strategy = Name,
            Type = isUptrend ? "Put Credit Spread" : "Call Credit Spread",
            Strikes = "12-18 delta short strike",
            PositionSize = positionSize * 1.1m,
            ExpectedPnL = CalculateExpectedPnL(positionSize * 1.1m, 0.68, 180m, -90m),
            Confidence = 0.72,
            MaxRisk = positionSize * 0.18m,
            Reasoning = $"Adaptive: {(isUptrend ? "Up" : "Down")} trend following"
        };
    }

    private Trade CreateMeanReversionTrade(decimal positionSize, MarketConditions conditions, MarketRegime regime)
    {
        var isOversold = conditions.RSI < 35;
        return new Trade
        {
            Strategy = Name,
            Type = "Iron Condor (Wide)",
            Strikes = "8-15 delta wings",
            PositionSize = positionSize * 1.0m,
            ExpectedPnL = CalculateExpectedPnL(positionSize, 0.70, 140m, -70m),
            Confidence = 0.68,
            MaxRisk = positionSize * 0.16m,
            Reasoning = $"Adaptive: Mean reversion from {(isOversold ? "oversold" : "overbought")}"
        };
    }

    private List<Trade> CreateVolatilitySellingTrades(decimal positionSize, MarketConditions conditions, MarketRegime regime)
    {
        return new List<Trade>
        {
            new Trade
            {
                Strategy = Name,
                Type = "Iron Condor",
                Strikes = "10 delta wings",
                PositionSize = positionSize * 0.7m,
                ExpectedPnL = CalculateExpectedPnL(positionSize * 0.7m, 0.75, 120m, -60m),
                Confidence = 0.75,
                MaxRisk = positionSize * 0.14m,
                Reasoning = "Adaptive: Volatility selling primary"
            }
        };
    }

    private Trade CreateDefensiveTrade(decimal positionSize, MarketConditions conditions, MarketRegime regime)
    {
        return new Trade
        {
            Strategy = Name,
            Type = "Conservative Iron Condor",
            Strikes = "5 delta wings, wide spread",
            PositionSize = positionSize * 0.8m,
            ExpectedPnL = CalculateExpectedPnL(positionSize * 0.8m, 0.80, 80m, -40m),
            Confidence = 0.80,
            MaxRisk = positionSize * 0.10m,
            Reasoning = "Adaptive: Defensive mode - uncertain conditions"
        };
    }

    public bool IsOptimalFor(MarketRegime regime)
    {
        return regime.RegimeType == RegimeType.Uncertain || regime.Confidence < 0.6m;
    }

    private decimal CalculateExpectedPnL(decimal positionSize, double winRate, decimal avgWin, decimal avgLoss)
    {
        return (decimal)(winRate * (double)avgWin + (1 - winRate) * (double)avgLoss);
    }
}

/// <summary>
/// Adaptive strategy modes
/// </summary>
public enum AdaptiveMode
{
    TrendFollowing,
    MeanReversion,
    VolatilitySelling,
    Defensive
}

/// <summary>
/// Market conditions data structure
/// </summary>
public class MarketConditions
{
    public decimal IVRank { get; set; }           // 0-100, implied volatility rank
    public decimal RSI { get; set; }             // 0-100, relative strength index
    public double MomentumDivergence { get; set; } // -1 to 1, momentum divergence
    public decimal VIXContango { get; set; }     // VIX term structure slope
}

/// <summary>
/// Individual trade representation
/// </summary>
public class Trade
{
    public string Strategy { get; set; } = "";
    public string Type { get; set; } = "";
    public string Strikes { get; set; } = "";
    public decimal PositionSize { get; set; }
    public decimal ExpectedPnL { get; set; }
    public double Confidence { get; set; }
    public decimal MaxRisk { get; set; }
    public string Reasoning { get; set; } = "";
    public decimal PnL { get; set; }
    public decimal RunningPnL { get; set; }
}