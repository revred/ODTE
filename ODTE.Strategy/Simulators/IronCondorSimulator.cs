using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ODTE.Strategy
{
    /// <summary>
    /// Iron Condor strategy simulator with calibrated expectations
    /// </summary>
    public class IronCondorSimulator
    {
        private readonly Random _random;
        private readonly StrategyEngineConfig _config;

        public IronCondorSimulator(Random random, StrategyEngineConfig config)
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
                StrategyName = "Iron Condor",
                ExecutionDate = conditions.Date,
                MarketRegime = marketRegime
            };

            // Calculate credit based on market conditions
            var baseCredit = parameters.StrikeWidth * parameters.CreditMinimum;
            var volAdjustment = 1.0 + (conditions.VIX - 20) * 0.01; // 1% per VIX point above 20
            var credit = (decimal)(baseCredit * volAdjustment);

            result.CreditReceived = credit;
            result.MaxRisk = (decimal)(parameters.StrikeWidth * (1 - parameters.CreditMinimum));

            // Calculate win probability based on market regime and conditions
            var winProbability = CalculateWinProbability(conditions);
            result.WinProbability = winProbability;

            // Simulate execution outcome
            var isWin = _random.NextDouble() < winProbability;
            result.IsWin = isWin;

            if (isWin)
            {
                result.PnL = credit * (decimal)(0.8 + _random.NextDouble() * 0.2); // 80-100% credit capture
                result.ExitReason = "Expired profitable";
            }
            else
            {
                var lossPercent = _random.NextDouble();
                result.PnL = -result.MaxRisk * (decimal)lossPercent;
                result.ExitReason = DetermineLossReason(conditions, lossPercent);
            }

            // Create option legs
            result.Legs = CreateIronCondorLegs(parameters, conditions);

            // Add metadata
            result.Metadata["VIX"] = conditions.VIX;
            result.Metadata["DTE"] = conditions.DaysToExpiry;
            result.Metadata["WinProbabilityUsed"] = winProbability;

            return result;
        }

        private double CalculateWinProbability(MarketConditions conditions)
        {
            var baseProbability = conditions.MarketRegime.ToLower() switch
            {
                "calm" => 0.78, // Calibrated expectation for calm markets
                "mixed" => 0.68, // Calibrated expectation for mixed markets
                "volatile" => 0.55, // Calibrated expectation for volatile markets
                _ => 0.70
            };

            // Adjust for trend strength
            var trendAdjustment = Math.Abs(conditions.TrendScore) * 0.15; // Strong trends hurt IC
            baseProbability -= trendAdjustment;

            // Adjust for DTE
            if (conditions.DaysToExpiry == 0)
                baseProbability += 0.05; // Slight boost for 0DTE pin effect

            return Math.Max(0.3, Math.Min(0.9, baseProbability));
        }

        private string DetermineLossReason(MarketConditions conditions, double lossPercent)
        {
            if (Math.Abs(conditions.TrendScore) > 0.6)
                return lossPercent > 0.7 ? "Strong trend breach - max loss" : "Trend breach - partial loss";
            
            if (conditions.VIX > 35)
                return lossPercent > 0.8 ? "Volatility explosion - max loss" : "Volatility breach - partial loss";
            
            return lossPercent > 0.5 ? "Pin failure - moderate loss" : "Minor breach - small loss";
        }

        private List<OptionLeg> CreateIronCondorLegs(StrategyParameters parameters, MarketConditions conditions)
        {
            var underlying = conditions.UnderlyingPrice;
            var strikeWidth = parameters.StrikeWidth;

            return new List<OptionLeg>
            {
                // Call spread (short call closer to money)
                new() { OptionType = "Call", Strike = underlying + 20, Quantity = -1, Action = "Sell", Premium = 3.5 },
                new() { OptionType = "Call", Strike = underlying + 20 + strikeWidth, Quantity = 1, Action = "Buy", Premium = 1.5 },
                
                // Put spread (short put closer to money)
                new() { OptionType = "Put", Strike = underlying - 20, Quantity = -1, Action = "Sell", Premium = 3.2 },
                new() { OptionType = "Put", Strike = underlying - 20 - strikeWidth, Quantity = 1, Action = "Buy", Premium = 1.3 }
            };
        }
    }
}