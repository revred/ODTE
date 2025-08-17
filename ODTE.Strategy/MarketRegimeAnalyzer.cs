namespace ODTE.Strategy
{
    /// <summary>
    /// Market regime analyzer for strategy selection
    /// 
    /// ** CORE MACHINE LEARNING / GENETIC ALGORITHM OPTIMIZATION TARGET **
    /// 
    /// This is the PRIMARY STRATEGY SELECTION ALGORITHM that determines which trading
    /// strategies are allowed based on current market conditions. Every parameter here
    /// directly controls trading performance and risk management.
    /// 
    /// GENETIC ALGORITHM OPTIMIZATION TARGETS:
    /// 1. VIX thresholds: [40, 25] → optimize ranges [35-45, 20-30]  
    /// 2. Trend thresholds: [0.8] → optimize range [0.6-1.0]
    /// 3. Volatility ratios: [1.1] → optimize range [1.05-1.3]
    /// 
    /// MACHINE LEARNING ENHANCEMENT OPPORTUNITIES:
    /// - Replace hard thresholds with learned decision boundaries (SVM, Random Forest)
    /// - Add feature engineering: volatility persistence, correlation, term structure
    /// - Implement ensemble methods combining multiple regime classifiers
    /// - Time-series models (LSTM) for regime transition prediction
    /// - Reinforcement learning for adaptive threshold adjustment
    /// 
    /// STRATEGY SELECTION IMPACT:
    /// - "convex" → BWB only, position size limits, high ROC requirements
    /// - "mixed" → BWB + IC allowed, moderate position sizing  
    /// - "calm" → All strategies, standard position sizing
    /// 
    /// OPTIMIZATION FITNESS FUNCTION:
    /// - Maximize: 20-year backtest Sharpe ratio + win rate
    /// - Minimize: maximum drawdown + regime misclassification rate
    /// - Constraint: maintain <7% loss frequency across all regimes
    /// </summary>
    public class MarketRegimeAnalyzer
    {
        public async Task<string> ClassifyMarketRegimeAsync(MarketConditions conditions)
        {
            await Task.Delay(1); // Simulate async analysis

            // GENETIC ALGORITHM TARGET: Core regime classification thresholds
            // These values control which strategies are allowed and position sizing

            // CONVEX REGIME: High volatility or strong momentum
            // ML OPTIMIZATION: VIX_HIGH=40, TREND_HIGH=0.8
            if (conditions.VIX > 40 ||                              // VIX_HIGH_THRESHOLD [GENETIC RANGE: 35-45]
                Math.Abs(conditions.TrendScore) >= 0.8)             // TREND_HIGH_THRESHOLD [GENETIC RANGE: 0.6-1.0]
            {
                return "convex";  // STRATEGY IMPACT: BWB only, 25% sizing, 30% ROC required
            }
            // MIXED REGIME: Elevated volatility or vol/trend signals
            // ML OPTIMIZATION: VIX_MED=25, VOL_RATIO=1.1 
            else if (conditions.VIX > 25 ||                         // VIX_MEDIUM_THRESHOLD [GENETIC RANGE: 20-30]
                     conditions.RealizedVolatility > conditions.ImpliedVolatility * 1.1) // VOL_RATIO [GENETIC RANGE: 1.05-1.3]
            {
                return "mixed";   // STRATEGY IMPACT: BWB + IC allowed, 50% sizing
            }
            // CALM REGIME: Low volatility, range-bound (DEFAULT)
            else
            {
                return "calm";    // STRATEGY IMPACT: All strategies, standard sizing
            }
        }

        // Legacy method for backward compatibility
        public string AnalyzeRegime(MarketConditions conditions)
        {
            return ClassifyMarketRegimeAsync(conditions).Result;
        }
    }
}