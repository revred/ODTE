using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ODTE.Strategy
{
    /// <summary>
    /// Convex Tail Overlay simulator - converts vol/trend risk into convex payoffs
    /// </summary>
    public class ConvexTailOverlaySimulator
    {
        private readonly Random _random;
        private readonly StrategyEngineConfig _config;

        public ConvexTailOverlaySimulator(Random random, StrategyEngineConfig config)
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
                StrategyName = "Convex Tail Overlay",
                ExecutionDate = conditions.Date,
                MarketRegime = marketRegime
            };

            // Determine overlay type based on conditions
            var overlayType = DetermineOverlayType(conditions);
            var tailCost = CalculateTailCost(parameters, conditions);
            
            result.MaxRisk = tailCost;
            result.CreditReceived = 0; // Convex overlay typically costs money upfront

            // Calculate win probability (low frequency, high magnitude)
            var winProbability = CalculateConvexWinProbability(conditions, overlayType);
            result.WinProbability = winProbability;

            // Simulate execution outcome
            var isWin = _random.NextDouble() < winProbability;
            result.IsWin = isWin;

            if (isWin)
            {
                var convexPayoff = CalculateConvexPayoff(conditions, overlayType);
                result.PnL = convexPayoff - tailCost;
                result.ExitReason = $"Convex {overlayType} payoff - vol/trend conversion successful";
            }
            else
            {
                result.PnL = -tailCost;
                result.ExitReason = $"Tail cost - {overlayType} did not activate";
            }

            // Create convex legs
            result.Legs = CreateConvexLegs(parameters, conditions, overlayType);

            // Add convex-specific metadata
            result.Metadata["OverlayType"] = overlayType;
            result.Metadata["TailCost"] = (double)tailCost;
            result.Metadata["VolatilityLevel"] = conditions.VIX;
            result.Metadata["TrendStrength"] = Math.Abs(conditions.TrendScore);
            result.Metadata["ConvexActivated"] = isWin;

            return result;
        }

        private string DetermineOverlayType(MarketConditions conditions)
        {
            // Tail Extender: For moderate vol/trend risk
            if (conditions.VIX >= 25 || Math.Abs(conditions.TrendScore) >= 0.6)
            {
                return "Tail Extender";
            }
            
            // Ratio Backspread: For high vol/trend conditions
            if (conditions.VIX >= 35 || Math.Abs(conditions.TrendScore) >= 0.8)
            {
                return "Ratio Backspread";
            }

            // Default to Tail Extender for mixed conditions
            return "Tail Extender";
        }

        private decimal CalculateTailCost(StrategyParameters parameters, MarketConditions conditions)
        {
            var baseCost = parameters.PositionSize * 0.03m; // 3% of position size

            // Adjust cost based on volatility
            var volAdjustment = 1.0 + (conditions.VIX - 20) * 0.02; // 2% per VIX point above 20
            
            return baseCost * (decimal)Math.Max(0.5, volAdjustment);
        }

        private double CalculateConvexWinProbability(MarketConditions conditions, string overlayType)
        {
            var baseProbability = conditions.MarketRegime.ToLower() switch
            {
                "calm" => 0.08, // Very low in calm markets (just tail cost)
                "mixed" => 0.18, // Moderate in mixed markets
                "volatile" => 0.35, // Much higher in volatile markets
                _ => 0.15
            };

            // Overlay type adjustments
            if (overlayType == "Ratio Backspread")
            {
                baseProbability += 0.10; // Higher probability for ratio spreads in vol
            }

            // Trend strength boost (convex strategies love strong moves)
            if (Math.Abs(conditions.TrendScore) > 0.7)
            {
                baseProbability += 0.15;
            }

            // VIX boost
            if (conditions.VIX > 40)
            {
                baseProbability += 0.20;
            }

            return Math.Max(0.05, Math.Min(0.6, baseProbability));
        }

        private decimal CalculateConvexPayoff(MarketConditions conditions, string overlayType)
        {
            var basePayoff = overlayType switch
            {
                "Tail Extender" => 150m + (decimal)(_random.NextDouble() * 100), // 150-250 payoff
                "Ratio Backspread" => 300m + (decimal)(_random.NextDouble() * 200), // 300-500 payoff
                _ => 100m
            };

            // Scale by volatility and trend
            var volMultiplier = 1.0 + (conditions.VIX - 20) / 50.0; // Higher vol = higher payoffs
            var trendMultiplier = 1.0 + Math.Abs(conditions.TrendScore); // Strong trends = higher payoffs

            return basePayoff * (decimal)(volMultiplier * trendMultiplier);
        }

        private List<OptionLeg> CreateConvexLegs(StrategyParameters parameters, MarketConditions conditions, string overlayType)
        {
            var underlying = conditions.UnderlyingPrice;
            
            return overlayType switch
            {
                "Tail Extender" => CreateTailExtenderLegs(underlying, conditions),
                "Ratio Backspread" => CreateRatioBackspreadLegs(underlying, conditions),
                _ => new List<OptionLeg>()
            };
        }

        private List<OptionLeg> CreateTailExtenderLegs(double underlying, MarketConditions conditions)
        {
            // Simple tail extender: Long options beyond the BWB wings
            var isCallSide = conditions.TrendScore > 0;

            if (isCallSide)
            {
                return new List<OptionLeg>
                {
                    new() { OptionType = "Call", Strike = underlying + 50, Quantity = 1, Action = "Buy", Premium = 1.2 },
                    new() { OptionType = "Call", Strike = underlying + 70, Quantity = 1, Action = "Buy", Premium = 0.6 }
                };
            }
            else
            {
                return new List<OptionLeg>
                {
                    new() { OptionType = "Put", Strike = underlying - 50, Quantity = 1, Action = "Buy", Premium = 1.1 },
                    new() { OptionType = "Put", Strike = underlying - 70, Quantity = 1, Action = "Buy", Premium = 0.5 }
                };
            }
        }

        private List<OptionLeg> CreateRatioBackspreadLegs(double underlying, MarketConditions conditions)
        {
            // Ratio backspread: Sell 1, Buy 2 for unlimited upside
            var isCallSide = conditions.TrendScore >= 0; // Slight bullish bias for ratio spreads

            if (isCallSide)
            {
                return new List<OptionLeg>
                {
                    new() { OptionType = "Call", Strike = underlying + 25, Quantity = -1, Action = "Sell", Premium = 3.5 },
                    new() { OptionType = "Call", Strike = underlying + 40, Quantity = 2, Action = "Buy", Premium = 1.5 }
                };
            }
            else
            {
                return new List<OptionLeg>
                {
                    new() { OptionType = "Put", Strike = underlying - 25, Quantity = -1, Action = "Sell", Premium = 3.2 },
                    new() { OptionType = "Put", Strike = underlying - 40, Quantity = 2, Action = "Buy", Premium = 1.4 }
                };
            }
        }
    }
}