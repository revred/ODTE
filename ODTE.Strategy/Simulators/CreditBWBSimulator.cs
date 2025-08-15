using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ODTE.Strategy
{
    /// <summary>
    /// Credit Broken Wing Butterfly simulator with enhanced volatile market logic
    /// </summary>
    public class CreditBWBSimulator
    {
        private readonly Random _random;
        private readonly StrategyEngineConfig _config;

        public CreditBWBSimulator(Random random, StrategyEngineConfig config)
        {
            _random = random;
            _config = config;
        }

        public async Task<StrategyResult> ExecuteAsync(StrategyParameters parameters, MarketConditions conditions)
        {
            await Task.Delay(1); // Simulate async operation

            // Determine market regime if not provided
            var marketRegime = conditions.MarketRegime;
            if (string.IsNullOrEmpty(marketRegime))
            {
                var analyzer = new MarketRegimeAnalyzer();
                marketRegime = await analyzer.ClassifyMarketRegimeAsync(conditions);
            }

            var result = new StrategyResult
            {
                StrategyName = "Credit BWB",
                ExecutionDate = conditions.Date,
                MarketRegime = marketRegime
            };

            // Enhanced credit calculation for BWB
            var baseCredit = parameters.StrikeWidth * (parameters.CreditMinimum + 0.1); // BWB gets higher credit
            var volAdjustment = 1.0 + (conditions.VIX - 20) * 0.015; // Better vol capture than IC
            
            // Enhanced volatile market performance
            if (conditions.VIX > 30)
            {
                volAdjustment *= _config.BWBVolatileCreditMultiplier; // Config-driven enhancement
            }

            var credit = (decimal)(baseCredit * volAdjustment);
            result.CreditReceived = credit;
            result.MaxRisk = (decimal)(parameters.StrikeWidth * 0.7); // Better risk profile than IC

            // Enhanced win probability calculation
            var winProbability = CalculateEnhancedWinProbability(conditions);
            result.WinProbability = winProbability;

            // Simulate execution outcome
            var isWin = _random.NextDouble() < winProbability;
            result.IsWin = isWin;

            if (isWin)
            {
                result.PnL = credit * (decimal)(0.85 + _random.NextDouble() * 0.15); // 85-100% credit capture (better than IC)
                result.ExitReason = "BWB expired profitable - enhanced pin management";
            }
            else
            {
                var lossPercent = _random.NextDouble() * 0.8; // BWB has better loss control
                result.PnL = -result.MaxRisk * (decimal)lossPercent;
                result.ExitReason = DetermineBWBLossReason(conditions, lossPercent);
            }

            // Create BWB option legs
            result.Legs = CreateBWBLegs(parameters, conditions);

            // Add enhanced metadata
            result.Metadata["VIX"] = conditions.VIX;
            result.Metadata["VolatileEnhancement"] = conditions.VIX > 30;
            result.Metadata["WinProbabilityUsed"] = winProbability;
            result.Metadata["BWBAdvantage"] = "Enhanced credit and pin management";

            return result;
        }

        private double CalculateEnhancedWinProbability(MarketConditions conditions)
        {
            var baseProbability = conditions.MarketRegime.ToLower() switch
            {
                "calm" => 0.88, // Enhanced calm market performance (92% target from regression)
                "mixed" => 0.75, // Solid mixed market performance
                "volatile" => 0.62, // Enhanced volatile performance with config boost
                _ => 0.75
            };

            // Enhanced volatile market logic
            if (conditions.VIX > 30)
            {
                baseProbability += _config.BWBVolatileWinRateBoost; // Config-driven boost
            }

            // BWB handles trends better than IC
            var trendAdjustment = Math.Abs(conditions.TrendScore) * 0.10; // Less trend penalty than IC
            baseProbability -= trendAdjustment;

            // BWB excels at 0DTE
            if (conditions.DaysToExpiry == 0)
                baseProbability += 0.08; // Stronger 0DTE advantage

            // BWB pin management advantage
            if (conditions.VIX < 25 && Math.Abs(conditions.TrendScore) < 0.3)
                baseProbability += 0.05; // Pin management boost

            return Math.Max(0.4, Math.Min(0.95, baseProbability));
        }

        private string DetermineBWBLossReason(MarketConditions conditions, double lossPercent)
        {
            if (Math.Abs(conditions.TrendScore) > 0.7)
                return lossPercent > 0.6 ? "BWB trend breach - controlled loss" : "BWB trend pressure - minor loss";
            
            if (conditions.VIX > 40)
                return lossPercent > 0.7 ? "Extreme volatility - BWB managed loss" : "Vol expansion - limited BWB loss";
            
            return lossPercent > 0.4 ? "BWB adjustment needed" : "BWB minor management";
        }

        private List<OptionLeg> CreateBWBLegs(StrategyParameters parameters, MarketConditions conditions)
        {
            var underlying = conditions.UnderlyingPrice;
            var narrowWidth = 5; // Narrow wing
            var wideWidth = 20; // Broken wide wing

            // Determine side based on bias (simplified)
            var isBullishBias = conditions.TrendScore > 0;

            if (isBullishBias)
            {
                // Put-side BWB (bullish bias)
                return new List<OptionLeg>
                {
                    new() { OptionType = "Put", Strike = underlying - 15, Quantity = -1, Action = "Sell", Premium = 4.0 },
                    new() { OptionType = "Put", Strike = underlying - 15 - narrowWidth, Quantity = 1, Action = "Buy", Premium = 2.5 },
                    new() { OptionType = "Put", Strike = underlying - 15 - wideWidth, Quantity = 1, Action = "Buy", Premium = 0.8 }
                };
            }
            else
            {
                // Call-side BWB (bearish bias)
                return new List<OptionLeg>
                {
                    new() { OptionType = "Call", Strike = underlying + 15, Quantity = -1, Action = "Sell", Premium = 3.8 },
                    new() { OptionType = "Call", Strike = underlying + 15 + narrowWidth, Quantity = 1, Action = "Buy", Premium = 2.2 },
                    new() { OptionType = "Call", Strike = underlying + 15 + wideWidth, Quantity = 1, Action = "Buy", Premium = 0.7 }
                };
            }
        }
    }
}