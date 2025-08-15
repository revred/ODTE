using System;
using System.Threading.Tasks;

namespace ODTE.Strategy
{
    /// <summary>
    /// Market regime analyzer for strategy selection
    /// </summary>
    public class MarketRegimeAnalyzer
    {
        public async Task<string> ClassifyMarketRegimeAsync(MarketConditions conditions)
        {
            await Task.Delay(1); // Simulate async analysis

            // Primary classification based on VIX and trend
            if (conditions.VIX > 40 || Math.Abs(conditions.TrendScore) >= 0.8)
            {
                return "convex";
            }
            else if (conditions.VIX > 25 || conditions.RealizedVolatility > conditions.ImpliedVolatility * 1.1)
            {
                return "mixed";
            }
            else
            {
                return "calm";
            }
        }

        // Legacy method for backward compatibility
        public string AnalyzeRegime(MarketConditions conditions)
        {
            return ClassifyMarketRegimeAsync(conditions).Result;
        }
    }
}