using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ODTE.Strategy;

/// <summary>
/// Black Swan Strategy - Extreme Market Stress Trading
/// 
/// ACTIVATION CONDITIONS:
/// - VIX > 80 (extreme fear)
/// - Market crashes, circuit breakers
/// - Complete regime confidence breakdown
/// - Flash crashes, gap openings >3%
/// 
/// CORE STRATEGY:
/// - Long volatility positions (VIX calls, put spreads)
/// - Volatility expansion plays (straddles, strangles)
/// - Mean reversion at extreme oversold levels
/// - Tail hedge activation for portfolio protection
/// 
/// HISTORICAL PERFORMANCE TARGET:
/// - COVID-19: Turn -$1,446 loss into profit
/// - China Devaluation: Turn -$1,523 loss into manageable loss
/// - Target: 70%+ win rate during crisis periods
/// </summary>
public class BlackSwanStrategy : IStrategy
{
    public string Name => "Black Swan Crisis Response";
    public string Description => "Extreme market stress trading for crisis periods";
    private double ExpectedEdge => 0.65; // 65% edge during extreme stress
    public double ExpectedWinRate => 0.60; // 60% win rate in crisis
    public double RewardToRiskRatio => 2.5; // 2.5:1 reward/risk
    
    private readonly CrisisDetector _crisisDetector;
    
    public decimal Execute(MarketConditions conditions, decimal positionSize)
    {
        // Implementation for IStrategy interface
        return 0m;
    }
    
    public bool ShouldExecute(MarketConditions conditions)
    {
        return conditions.VIX > 80;
    }
    
    public void UpdatePerformance(decimal result)
    {
        // Update performance tracking
    }
    
    public BlackSwanStrategy()
    {
        _crisisDetector = new CrisisDetector();
    }
    
    /// <summary>
    /// Execute Black Swan strategy during extreme market stress
    /// </summary>
    public async Task<List<Trade>> ExecuteTrading(MarketConditions conditions, decimal positionSize, MarketRegime regime)
    {
        var trades = new List<Trade>();
        var crisisMode = _crisisDetector.DetectCrisisMode(conditions, regime);
        
        // Only activate in crisis or black swan conditions
        if (crisisMode < CrisisMode.Crisis)
        {
            return trades; // No trades in normal conditions
        }
        
        Console.WriteLine($"🚨 BLACK SWAN STRATEGY ACTIVATED - Crisis Mode: {crisisMode}");
        
        // Strategy 1: Volatility Expansion Trade (Primary)
        if (regime.VIX > 50 && conditions.IVRank < 90) // VIX spiking but more room to go
        {
            var volExpansionTrade = await CreateVolatilityExpansionTrade(positionSize, conditions, regime);
            trades.Add(volExpansionTrade);
            Console.WriteLine($"   📈 Volatility Expansion: ${volExpansionTrade.ExpectedPnL:F0}");
        }
        
        // Strategy 2: Extreme Oversold Bounce (Secondary)
        if (conditions.RSI < 25 && conditions.MomentumDivergence < -0.5)
        {
            var bounceTradePosition = positionSize * 0.6m; // Smaller allocation
            var bounceTradeRed = await CreateOversoldBounceTrade(bounceTradePosition, conditions, regime);
            trades.Add(bounceTradeRed);
            Console.WriteLine($"   ⚡ Oversold Bounce: ${bounceTradeRed.ExpectedPnL:F0}");
        }
        
        // Strategy 3: VIX Term Structure Arbitrage
        if (conditions.VIXContango < -3 && regime.VIX > 40) // Deep backwardation
        {
            var termStructureTrade = await CreateVIXTermStructureTrade(positionSize * 0.4m, conditions, regime);
            trades.Add(termStructureTrade);
            Console.WriteLine($"   📊 VIX Term Structure: ${termStructureTrade.ExpectedPnL:F0}");
        }
        
        // Strategy 4: Tail Hedge Activation (Emergency)
        if (crisisMode == CrisisMode.BlackSwan)
        {
            var tailHedge = await CreateTailHedgeTrade(positionSize * 0.3m, conditions, regime);
            trades.Add(tailHedge);
            Console.WriteLine($"   🛡️ Tail Hedge: ${tailHedge.ExpectedPnL:F0}");
        }
        
        return trades;
    }
    
    /// <summary>
    /// Check if Black Swan strategy is optimal for current regime
    /// </summary>
    public bool IsOptimalFor(MarketRegime regime)
    {
        return regime.VIX > 50 || regime.Confidence < 0.3m;
    }
    
    /// <summary>
    /// Create volatility expansion trade - primary crisis strategy
    /// </summary>
    private async Task<Trade> CreateVolatilityExpansionTrade(decimal positionSize, MarketConditions conditions, MarketRegime regime)
    {
        // Long straddle or strangle to capture volatility expansion
        var expectedMove = (double)regime.VIX * 0.6; // Expected daily move percentage
        var probabilityOfProfit = Math.Min(0.85, 0.4 + ((double)regime.VIX - 30) / 100.0); // Higher VIX = higher POP
        
        // Calculate expected P&L based on volatility expansion
        var maxProfit = positionSize * 2.5m; // Volatility expansion can be very profitable
        var maxLoss = positionSize * 0.8m; // Limited downside with proper structure
        
        var expectedPnL = (decimal)(probabilityOfProfit * (double)maxProfit - (1 - probabilityOfProfit) * (double)maxLoss);
        
        return new Trade
        {
            Strategy = "Volatility Expansion Straddle",
            Type = "Long Straddle",
            Strikes = "ATM",
            PositionSize = positionSize,
            ExpectedPnL = expectedPnL,
            MaxRisk = maxLoss,
            Confidence = probabilityOfProfit,
            Reasoning = $"VIX spike to {regime.VIX:F1}, expanding volatility play"
        };
    }
    
    /// <summary>
    /// Create oversold bounce trade - mean reversion in extreme conditions
    /// </summary>
    private async Task<Trade> CreateOversoldBounceTrade(decimal positionSize, MarketConditions conditions, MarketRegime regime)
    {
        // Put credit spread or call debit spread for bounce
        var oversoldSeverity = (30 - (double)conditions.RSI) / 10.0; // More oversold = higher edge
        var probabilityOfProfit = Math.Min(0.75, 0.55 + oversoldSeverity * 0.15);
        
        var maxProfit = positionSize * 1.8m;
        var maxLoss = positionSize * 0.6m;
        
        var expectedPnL = (decimal)(probabilityOfProfit * (double)maxProfit - (1 - probabilityOfProfit) * (double)maxLoss);
        
        return new Trade
        {
            Strategy = "Oversold Bounce",
            Type = "Put Credit Spread",
            Strikes = "OTM Put Spread",
            PositionSize = positionSize,
            ExpectedPnL = expectedPnL,
            MaxRisk = maxLoss,
            Confidence = probabilityOfProfit,
            Reasoning = $"RSI {conditions.RSI:F1} extreme oversold with divergence"
        };
    }
    
    /// <summary>
    /// Create VIX term structure arbitrage trade
    /// </summary>
    private async Task<Trade> CreateVIXTermStructureTrade(decimal positionSize, MarketConditions conditions, MarketRegime regime)
    {
        // Capture term structure normalization
        var backwardationSeverity = Math.Abs((double)conditions.VIXContango) / 5.0; // Deeper backwardation = higher edge
        var probabilityOfProfit = Math.Min(0.70, 0.50 + backwardationSeverity * 0.15);
        
        var maxProfit = positionSize * 1.5m;
        var maxLoss = positionSize * 0.7m;
        
        var expectedPnL = (decimal)(probabilityOfProfit * (double)maxProfit - (1 - probabilityOfProfit) * (double)maxLoss);
        
        return new Trade
        {
            Strategy = "Term Structure Arbitrage",
            Type = "VIX Calendar",
            Strikes = "ATM Calendar",
            PositionSize = positionSize,
            ExpectedPnL = expectedPnL,
            MaxRisk = maxLoss,
            Confidence = probabilityOfProfit,
            Reasoning = $"VIX contango {conditions.VIXContango:F1} deep backwardation"
        };
    }
    
    /// <summary>
    /// Create tail hedge trade for extreme protection
    /// </summary>
    private async Task<Trade> CreateTailHedgeTrade(decimal positionSize, MarketConditions conditions, MarketRegime regime)
    {
        // Long puts or put spreads for downside protection
        var crisisSeverity = ((double)regime.VIX - 50) / 30.0; // VIX 50-80+ range
        var probabilityOfProfit = Math.Min(0.60, 0.35 + crisisSeverity * 0.20);
        
        var maxProfit = positionSize * 3.0m; // Tail events can be very profitable
        var maxLoss = positionSize * 1.0m; // Accept higher risk for tail protection
        
        var expectedPnL = (decimal)(probabilityOfProfit * (double)maxProfit - (1 - probabilityOfProfit) * (double)maxLoss);
        
        return new Trade
        {
            Strategy = "Tail Hedge Protection",
            Type = "Long Puts",
            Strikes = "OTM Puts",
            PositionSize = positionSize,
            ExpectedPnL = expectedPnL,
            MaxRisk = maxLoss,
            Confidence = probabilityOfProfit,
            Reasoning = $"Black Swan VIX {regime.VIX:F1} - emergency tail hedge"
        };
    }
}

// Trade class defined in StrategyArsenal.cs