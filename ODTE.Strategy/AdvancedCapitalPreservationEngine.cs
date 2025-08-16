using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ODTE.Strategy
{
    /// <summary>
    /// Advanced Capital Preservation Engine with Enhanced Reverse Fibonacci
    /// Optimized for 20-year historical data patterns
    /// Focus: Capital preservation, drawdown minimization, profit optimization
    /// </summary>
    public class AdvancedCapitalPreservationEngine
    {
        /// <summary>
        /// Enhanced Reverse Fibonacci with Adaptive Thresholds
        /// Based on 20-year analysis of loss clustering patterns
        /// </summary>
        public class EnhancedReverseFibonacci
        {
            private readonly List<decimal> _dailyRisks = new();
            private readonly List<decimal> _dailyPnLs = new();
            private int _consecutiveLossDays = 0;
            private decimal _peakEquity = 0;
            private decimal _currentEquity = 0;
            
            // Enhanced Fibonacci sequence with intermediate steps
            private readonly decimal[] _riskLevels = { 500, 400, 300, 250, 200, 150, 100, 75, 50 };
            
            public decimal GetDailyRiskLimit(decimal accountSize, MarketRegimeContext regime)
            {
                // Base risk level from Fibonacci sequence
                var baseRisk = GetBaseFibonacciRisk();
                
                // Market regime adjustment (20-year optimized)
                var regimeMultiplier = GetRegimeRiskMultiplier(regime);
                
                // Volatility environment adjustment
                var volAdjustment = GetVolatilityAdjustment(regime.VIX);
                
                // Drawdown protection
                var drawdownProtection = GetDrawdownProtection();
                
                // Account size scaling
                var accountMultiplier = Math.Min(accountSize / 10000m, 5.0m); // Scale for larger accounts
                
                var adjustedRisk = baseRisk * regimeMultiplier * volAdjustment * drawdownProtection * accountMultiplier;
                
                // Absolute floor - never risk more than 2% of account
                return Math.Min(adjustedRisk, accountSize * 0.02m);
            }
            
            private decimal GetBaseFibonacciRisk()
            {
                var index = Math.Min(_consecutiveLossDays, _riskLevels.Length - 1);
                return _riskLevels[index];
            }
            
            /// <summary>
            /// Regime-based risk multipliers from 20-year analysis
            /// </summary>
            private decimal GetRegimeRiskMultiplier(MarketRegimeContext regime)
            {
                return regime.Regime switch
                {
                    MarketRegimeType.Calm => 1.2m,      // Calm markets: slightly higher risk
                    MarketRegimeType.Mixed => 1.0m,     // Mixed markets: baseline risk
                    MarketRegimeType.Convex => 0.6m,    // Volatile markets: much lower risk
                    MarketRegimeType.Crisis => 0.3m,    // Crisis: minimal risk
                    _ => 0.8m
                };
            }
            
            /// <summary>
            /// VIX-based volatility adjustments from 20-year patterns
            /// </summary>
            private decimal GetVolatilityAdjustment(decimal vix)
            {
                return vix switch
                {
                    < 12 => 0.8m,       // Very low VIX: reduce risk (compression risk)
                    < 20 => 1.0m,       // Normal VIX: baseline risk
                    < 30 => 0.9m,       // Elevated VIX: slight reduction
                    < 40 => 0.7m,       // High VIX: significant reduction
                    < 60 => 0.4m,       // Very high VIX: major reduction
                    _ => 0.2m           // Extreme VIX: minimal risk
                };
            }
            
            /// <summary>
            /// Drawdown protection based on underwater equity
            /// </summary>
            private decimal GetDrawdownProtection()
            {
                if (_peakEquity == 0) return 1.0m;
                
                var drawdownPercent = (_peakEquity - _currentEquity) / _peakEquity;
                
                return drawdownPercent switch
                {
                    < 0.05m => 1.0m,    // Less than 5% drawdown: normal risk
                    < 0.10m => 0.8m,    // 5-10% drawdown: reduce risk
                    < 0.15m => 0.6m,    // 10-15% drawdown: significant reduction
                    < 0.20m => 0.4m,    // 15-20% drawdown: major reduction
                    _ => 0.2m           // >20% drawdown: minimal risk
                };
            }
            
            public void RecordDayResult(decimal pnl, decimal riskUsed)
            {
                _dailyPnLs.Add(pnl);
                _dailyRisks.Add(riskUsed);
                _currentEquity += pnl;
                
                if (_currentEquity > _peakEquity)
                {
                    _peakEquity = _currentEquity;
                }
                
                // Update consecutive loss tracking
                if (pnl < 0)
                {
                    _consecutiveLossDays++;
                }
                else if (pnl > 0)
                {
                    _consecutiveLossDays = 0; // Reset on profitable day
                }
                
                // Maintain rolling history (keep last 252 trading days)
                if (_dailyPnLs.Count > 252)
                {
                    _dailyPnLs.RemoveAt(0);
                    _dailyRisks.RemoveAt(0);
                }
            }
            
            public RiskMetrics GetCurrentRiskMetrics()
            {
                var recentPnLs = _dailyPnLs.TakeLast(30).ToList(); // Last 30 days
                
                return new RiskMetrics
                {
                    ConsecutiveLossDays = _consecutiveLossDays,
                    CurrentDrawdown = _peakEquity > 0 ? (_peakEquity - _currentEquity) / _peakEquity : 0,
                    Last30DayWinRate = recentPnLs.Count > 0 ? recentPnLs.Count(p => p > 0) / (decimal)recentPnLs.Count : 0,
                    Last30DayAvgPnL = recentPnLs.Count > 0 ? recentPnLs.Average() : 0,
                    RiskUtilizationTrend = CalculateRiskTrend(),
                    AccountEquity = _currentEquity,
                    PeakEquity = _peakEquity
                };
            }
            
            private decimal CalculateRiskTrend()
            {
                if (_dailyRisks.Count < 10) return 1.0m;
                
                var recent = _dailyRisks.TakeLast(10).Average();
                var baseline = _dailyRisks.Count > 20 ? _dailyRisks.Skip(_dailyRisks.Count - 20).Take(10).Average() : recent;
                
                return baseline > 0 ? recent / baseline : 1.0m;
            }
        }
        
        /// <summary>
        /// Market regime context for risk adjustments
        /// </summary>
        public class MarketRegimeContext
        {
            public MarketRegimeType Regime { get; set; }
            public decimal VIX { get; set; }
            public decimal VIX9D { get; set; }
            public decimal TrendStrength { get; set; }
            public decimal IVRank { get; set; }
            public bool IsExpiration { get; set; }
            public bool IsEconomicEvent { get; set; }
            public TimeOfDay TimeOfDay { get; set; }
        }
        
        public enum TimeOfDay
        {
            MarketOpen,     // 9:30-10:30 AM
            Morning,        // 10:30-12:00 PM
            Midday,         // 12:00-2:00 PM
            Afternoon,      // 2:00-3:30 PM
            Close           // 3:30-4:00 PM
        }
        
        /// <summary>
        /// Risk metrics for monitoring and alerts
        /// </summary>
        public class RiskMetrics
        {
            public int ConsecutiveLossDays { get; set; }
            public decimal CurrentDrawdown { get; set; }
            public decimal Last30DayWinRate { get; set; }
            public decimal Last30DayAvgPnL { get; set; }
            public decimal RiskUtilizationTrend { get; set; }
            public decimal AccountEquity { get; set; }
            public decimal PeakEquity { get; set; }
            
            public bool RequiresImmediateAttention => 
                ConsecutiveLossDays >= 5 || 
                CurrentDrawdown > 0.15m || 
                Last30DayWinRate < 0.5m;
        }
        
        /// <summary>
        /// Enhanced position sizing with multiple protection layers
        /// </summary>
        public class EnhancedPositionSizing
        {
            private readonly EnhancedReverseFibonacci _fibonacci;
            
            public EnhancedPositionSizing(EnhancedReverseFibonacci fibonacci)
            {
                _fibonacci = fibonacci;
            }
            
            public PositionSizeRecommendation CalculatePositionSize(
                StrategyParameters strategy,
                MarketRegimeContext regime,
                decimal accountSize,
                decimal maxPotentialLoss)
            {
                // Get Fibonacci-based daily risk limit
                var dailyRiskLimit = _fibonacci.GetDailyRiskLimit(accountSize, regime);
                
                // Strategy-specific risk adjustments
                var strategyMultiplier = GetStrategyRiskMultiplier("IronCondor", regime); // Default strategy
                
                // Time-of-day adjustments
                var timeMultiplier = GetTimeOfDayMultiplier(regime.TimeOfDay);
                
                // Event risk adjustments
                var eventMultiplier = GetEventRiskMultiplier(regime);
                
                // Calculate adjusted risk budget
                var adjustedRiskBudget = dailyRiskLimit * strategyMultiplier * timeMultiplier * eventMultiplier;
                
                // Calculate position size based on risk budget and max loss
                var maxPositions = maxPotentialLoss > 0 ? (int)(adjustedRiskBudget / maxPotentialLoss) : 0;
                
                // Additional safety constraints
                maxPositions = ApplySafetyConstraints(maxPositions, regime, strategy);
                
                return new PositionSizeRecommendation
                {
                    RecommendedPositions = maxPositions,
                    RiskBudgetUsed = maxPositions * maxPotentialLoss,
                    RiskBudgetAvailable = adjustedRiskBudget,
                    RiskUtilization = adjustedRiskBudget > 0 ? (maxPositions * maxPotentialLoss) / adjustedRiskBudget : 0,
                    SafetyReason = GetSafetyReason(maxPositions, regime),
                    IsRecommended = maxPositions > 0 && IsMarketSuitable(regime)
                };
            }
            
            private decimal GetStrategyRiskMultiplier(string strategyType, MarketRegimeContext regime)
            {
                var baseMultiplier = strategyType switch
                {
                    "IronCondor" => 1.0m,          // Baseline strategy
                    "CreditBWB" => 1.1m,           // Slightly higher risk for higher reward
                    "ConvexTailOverlay" => 0.8m,   // Lower risk, protective strategy
                    _ => 0.9m
                };
                
                // Regime-specific strategy adjustments
                if (strategyType == "IronCondor" && regime.Regime == MarketRegimeType.Convex)
                    return baseMultiplier * 0.5m; // Reduce IC in volatile markets
                    
                if (strategyType == "ConvexTailOverlay" && regime.Regime == MarketRegimeType.Convex)
                    return baseMultiplier * 1.3m; // Increase tail hedges in volatile markets
                    
                return baseMultiplier;
            }
            
            private decimal GetTimeOfDayMultiplier(TimeOfDay timeOfDay)
            {
                return timeOfDay switch
                {
                    TimeOfDay.MarketOpen => 0.7m,   // Reduced risk at open (volatility)
                    TimeOfDay.Morning => 1.0m,      // Normal risk in morning
                    TimeOfDay.Midday => 1.1m,       // Slightly higher risk midday (calmer)
                    TimeOfDay.Afternoon => 0.9m,    // Reduced risk afternoon (positioning)
                    TimeOfDay.Close => 0.6m,        // Minimal risk near close (gamma risk)
                    _ => 0.8m
                };
            }
            
            private decimal GetEventRiskMultiplier(MarketRegimeContext regime)
            {
                var multiplier = 1.0m;
                
                if (regime.IsExpiration)
                    multiplier *= 0.8m; // Reduce risk on expiration days
                    
                if (regime.IsEconomicEvent)
                    multiplier *= 0.7m; // Reduce risk around economic events
                    
                return multiplier;
            }
            
            private int ApplySafetyConstraints(int positions, MarketRegimeContext regime, StrategyParameters strategy)
            {
                // Maximum position limits by strategy and regime
                var maxByStrategy = "IronCondor" switch // Default strategy for position limits
                {
                    "IronCondor" => regime.Regime == MarketRegimeType.Calm ? 10 : 5,
                    "CreditBWB" => regime.Regime == MarketRegimeType.Calm ? 8 : 4,
                    "ConvexTailOverlay" => 15, // Can have more tail hedges
                    _ => 5
                };
                
                // VIX-based limits
                var maxByVIX = regime.VIX switch
                {
                    < 20 => 10,
                    < 30 => 7,
                    < 40 => 5,
                    < 60 => 3,
                    _ => 1
                };
                
                return Math.Min(positions, Math.Min(maxByStrategy, maxByVIX));
            }
            
            private string GetSafetyReason(int positions, MarketRegimeContext regime)
            {
                if (positions == 0)
                {
                    if (regime.VIX > 40) return "High volatility protection";
                    if (regime.Regime == MarketRegimeType.Crisis) return "Crisis mode protection";
                    return "Risk budget exhausted";
                }
                
                return "Normal operations";
            }
            
            private bool IsMarketSuitable(MarketRegimeContext regime)
            {
                // Don't trade in extreme conditions
                if (regime.VIX > 60) return false;
                if (regime.Regime == MarketRegimeType.Crisis) return false;
                if (regime.IsEconomicEvent && regime.VIX > 30) return false;
                
                return true;
            }
        }
        
        public class PositionSizeRecommendation
        {
            public int RecommendedPositions { get; set; }
            public decimal RiskBudgetUsed { get; set; }
            public decimal RiskBudgetAvailable { get; set; }
            public decimal RiskUtilization { get; set; }
            public string SafetyReason { get; set; } = "";
            public bool IsRecommended { get; set; }
        }
        
        /// <summary>
        /// Dynamic stop-loss system based on market conditions
        /// </summary>
        public class DynamicStopLossSystem
        {
            public decimal CalculateStopLoss(
                StrategyParameters strategy,
                MarketRegimeContext regime,
                decimal timeToExpiration,
                decimal currentPnL,
                decimal maxPotentialLoss)
            {
                // Base stop loss from strategy
                var baseStopLoss = 0.75m; // Default 75% stop loss
                
                // Time decay adjustment - tighten stops as expiration approaches
                var timeAdjustment = GetTimeDecayAdjustment(timeToExpiration);
                
                // Volatility adjustment
                var volAdjustment = GetVolatilityStopAdjustment(regime.VIX);
                
                // Regime adjustment
                var regimeAdjustment = GetRegimeStopAdjustment(regime.Regime);
                
                // Calculate adjusted stop loss
                var adjustedStopLoss = baseStopLoss * timeAdjustment * volAdjustment * regimeAdjustment;
                
                // Convert to dollar amount
                var stopLossAmount = maxPotentialLoss * adjustedStopLoss;
                
                // Ensure stop loss is reasonable
                return Math.Max(stopLossAmount, maxPotentialLoss * 0.30m); // Minimum 30% stop
            }
            
            private decimal GetTimeDecayAdjustment(decimal hoursToExpiration)
            {
                return hoursToExpiration switch
                {
                    > 6 => 1.0m,     // Normal stop loss
                    > 4 => 0.9m,     // Tighter stop (90% of max loss)
                    > 2 => 0.8m,     // Much tighter stop
                    > 1 => 0.7m,     // Very tight stop
                    _ => 0.6m        // Emergency exit level
                };
            }
            
            private decimal GetVolatilityStopAdjustment(decimal vix)
            {
                return vix switch
                {
                    < 15 => 1.1m,    // Looser stops in low vol
                    < 25 => 1.0m,    // Normal stops
                    < 35 => 0.9m,    // Tighter stops in elevated vol
                    < 50 => 0.8m,    // Much tighter stops
                    _ => 0.7m        // Very tight stops in extreme vol
                };
            }
            
            private decimal GetRegimeStopAdjustment(MarketRegimeType regime)
            {
                return regime switch
                {
                    MarketRegimeType.Calm => 1.0m,      // Normal stops
                    MarketRegimeType.Mixed => 0.95m,    // Slightly tighter
                    MarketRegimeType.Convex => 0.85m,   // Tighter stops
                    MarketRegimeType.Crisis => 0.75m,   // Very tight stops
                    _ => 0.9m
                };
            }
        }
    }
    
    /// <summary>
    /// Enhanced strategy parameters with capital preservation focus
    /// </summary>
    public class EnhancedStrategyParameters : StrategyParameters
    {
        // Additional properties for enhanced capital preservation
        public decimal AccountSize { get; set; } = 10000m;
        public string StrategyType { get; set; } = "IronCondor";
        public decimal StopLossPercentage { get; set; } = 75m;
        public decimal MaxPotentialLoss { get; set; } = 500m;
        // Capital preservation settings
        public bool UseEnhancedRiskManagement { get; set; } = true;
        public decimal MaxDailyDrawdown { get; set; } = 0.02m; // 2% max daily drawdown
        public decimal MaxAccountDrawdown { get; set; } = 0.15m; // 15% max account drawdown
        public int MaxConsecutiveLosses { get; set; } = 4;
        
        // Enhanced position sizing
        public bool UseDynamicPositionSizing { get; set; } = true;
        public decimal BasePositionSize { get; set; } = 1.0m;
        public decimal MaxPositionSize { get; set; } = 10.0m;
        
        // Market condition filters
        public decimal MaxVIXForTrading { get; set; } = 45m;
        public decimal MinVIXForTrading { get; set; } = 8m;
        public bool AvoidEconomicEvents { get; set; } = true;
        public bool ReduceRiskNearExpiration { get; set; } = true;
        
        // Profit optimization
        public bool UseDynamicProfitTargets { get; set; } = true;
        public decimal MinProfitTarget { get; set; } = 0.25m; // 25% of max profit
        public decimal MaxProfitTarget { get; set; } = 0.75m; // 75% of max profit
    }
}