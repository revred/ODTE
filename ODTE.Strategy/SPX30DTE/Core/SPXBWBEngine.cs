using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ODTE.Backtest.Core;
using ODTE.Backtest.Data;
using ODTE.Backtest.Strategy;
using ODTE.Execution.Engine;
using ODTE.Execution.Models;
using ODTE.Historical.DistributedStorage;
using ODTE.Historical.Models;
using ODTE.Strategy.MultiLegStrategies;
using ODTE.Strategy.SPX30DTE.Probes;

namespace ODTE.Strategy.SPX30DTE.Core
{
    public interface IBWBEngine
    {
        Task<BWBEntry> BuildBWB(DateTime date, decimal spotPrice, ProbeSignal probeSignal);
        Task<BWBAnalysis> AnalyzeBWBOpportunity(DateTime date);
        Task<List<BWBPosition>> GetActivePositions();
        Task<BWBExitSignal> CheckExitConditions(BWBPosition position, DateTime currentDate);
        PortfolioGreeks CalculateBWBGreeks(BWBPosition position, decimal spotPrice, decimal volatility);
    }

    public class SPXBWBEngine : MultiLegOptionsStrategies, IBWBEngine
    {
        private readonly DistributedDatabaseManager _dataManager;
        private readonly RealisticFillEngine _fillEngine;
        private readonly BWBConfiguration _config;
        private readonly List<BWBPosition> _activePositions;
        private readonly Dictionary<string, BWBPerformance> _performanceHistory;
        
        public SPXBWBEngine(
            DistributedDatabaseManager dataManager,
            RealisticFillEngine fillEngine,
            BWBConfiguration config)
        {
            _dataManager = dataManager;
            _fillEngine = fillEngine;
            _config = config;
            _activePositions = new List<BWBPosition>();
            _performanceHistory = new Dictionary<string, BWBPerformance>();
        }

        public async Task<BWBEntry> BuildBWB(DateTime date, decimal spotPrice, ProbeSignal probeSignal)
        {
            // Check if we should enter based on probe signal
            if (!ShouldEnterBWB(probeSignal))
            {
                return null;
            }

            // Check position limits
            if (_activePositions.Count >= _config.MaxPositions)
            {
                return null;
            }

            // Get SPX options chain
            var spxChain = await _dataManager.GetOptionsChain("SPX", date);
            if (spxChain == null || !spxChain.Any())
            {
                return null;
            }

            // Find optimal expiration (30 DTE target)
            var targetExpiration = FindOptimalExpiration(spxChain, date, _config.TargetDTE);
            if (targetExpiration == null)
            {
                return null;
            }

            // Get options for target expiration
            var expiryChain = spxChain.Where(o => o.Expiration == targetExpiration).ToList();
            
            // Calculate BWB strikes
            var strikes = CalculateBWBStrikes(spotPrice, _config);
            
            // Build BWB structure
            var bwb = await BuildBWBStructure(expiryChain, strikes, date);
            
            // Validate BWB meets requirements
            if (!ValidateBWB(bwb))
            {
                return null;
            }

            // Calculate execution cost with realistic fills
            var executionCost = await CalculateExecutionCost(bwb, expiryChain);
            bwb.Credit = executionCost.NetCredit;
            bwb.MaxRisk = executionCost.MaxRisk;

            return bwb;
        }

        public async Task<BWBAnalysis> AnalyzeBWBOpportunity(DateTime date)
        {
            var analysis = new BWBAnalysis
            {
                Date = date,
                Opportunities = new List<BWBOpportunity>()
            };

            // Get SPX data
            var spxPrice = await _dataManager.GetUnderlyingPrice("SPX", date);
            var spxChain = await _dataManager.GetOptionsChain("SPX", date);
            
            if (spxChain == null || !spxChain.Any())
            {
                analysis.NoDataReason = "No SPX options data available";
                return analysis;
            }

            // Find all viable expirations
            var viableExpirations = spxChain
                .Where(o => o.DTE >= _config.MinDTE && o.DTE <= _config.MaxDTE)
                .Select(o => o.Expiration)
                .Distinct()
                .OrderBy(e => e)
                .ToList();

            foreach (var expiration in viableExpirations)
            {
                var dte = (expiration - date).Days;
                var expiryChain = spxChain.Where(o => o.Expiration == expiration).ToList();
                
                // Calculate potential BWB
                var strikes = CalculateBWBStrikes(spxPrice, _config);
                var opportunity = await EvaluateBWBOpportunity(expiryChain, strikes, dte, spxPrice);
                
                if (opportunity != null && opportunity.ExpectedValue > 0)
                {
                    analysis.Opportunities.Add(opportunity);
                }
            }

            // Rank opportunities
            analysis.Opportunities = analysis.Opportunities
                .OrderByDescending(o => o.Score)
                .Take(3)
                .ToList();

            // Calculate aggregate metrics
            if (analysis.Opportunities.Any())
            {
                analysis.BestOpportunity = analysis.Opportunities.First();
                analysis.AverageCredit = analysis.Opportunities.Average(o => o.Credit);
                analysis.AverageRisk = analysis.Opportunities.Average(o => o.MaxRisk);
                analysis.RecommendEntry = analysis.Opportunities.First().Score > 70;
            }

            return analysis;
        }

        public async Task<List<BWBPosition>> GetActivePositions()
        {
            // Update current prices and P&L
            foreach (var position in _activePositions)
            {
                await UpdatePositionPricing(position, DateTime.Now);
            }

            return _activePositions.ToList();
        }

        public async Task<BWBExitSignal> CheckExitConditions(BWBPosition position, DateTime currentDate)
        {
            var signal = new BWBExitSignal
            {
                PositionId = position.PositionId,
                CurrentDate = currentDate,
                ShouldExit = false
            };

            // Update current pricing
            await UpdatePositionPricing(position, currentDate);
            
            // Check DTE-based exit
            position.DTE = (position.Expiration - currentDate).Days;
            if (position.DTE <= _config.ForcedExitDTE)
            {
                signal.ShouldExit = true;
                signal.ExitReason = "DTE_EXPIRY";
                signal.ExpectedPnL = position.UnrealizedPnL;
                return signal;
            }

            // Check profit target
            var profitPercent = position.UnrealizedPnL / position.MaxRisk;
            if (profitPercent >= _config.ProfitTarget)
            {
                signal.ShouldExit = true;
                signal.ExitReason = "PROFIT_TARGET";
                signal.ExpectedPnL = position.UnrealizedPnL;
                return signal;
            }

            // Check stop loss
            var lossMultiple = Math.Abs(position.UnrealizedPnL) / position.Credit;
            if (position.UnrealizedPnL < 0 && lossMultiple >= _config.StopLoss)
            {
                signal.ShouldExit = true;
                signal.ExitReason = "STOP_LOSS";
                signal.ExpectedPnL = position.UnrealizedPnL;
                return signal;
            }

            // Check delta threshold breach
            var greeks = CalculateBWBGreeks(position, position.CurrentSpotPrice, position.CurrentIV);
            if (Math.Abs(greeks.NetDelta) > _config.DeltaThreshold)
            {
                signal.ShouldExit = true;
                signal.ExitReason = "DELTA_BREACH";
                signal.ExpectedPnL = position.UnrealizedPnL;
                signal.DeltaAtExit = greeks.NetDelta;
                return signal;
            }

            // Check for early profit opportunity (> 50% in < 50% time)
            var timeElapsed = 1 - (position.DTE / (decimal)position.OriginalDTE);
            if (profitPercent > 0.50m && timeElapsed < 0.50m)
            {
                signal.ConsiderExit = true;
                signal.ExitReason = "EARLY_PROFIT";
                signal.ExpectedPnL = position.UnrealizedPnL;
            }

            return signal;
        }

        public PortfolioGreeks CalculateBWBGreeks(BWBPosition position, decimal spotPrice, decimal volatility)
        {
            var greeks = new PortfolioGreeks();
            var timeToExpiry = position.DTE / 365.0m;
            var riskFreeRate = 0.05m; // Current risk-free rate
            
            // Calculate Greeks for each leg
            // Long Lower Put
            var lowerLongGreeks = OptionMath.CalculateGreeks(
                spotPrice, position.LongLowerStrike, timeToExpiry, 
                volatility, riskFreeRate, OptionType.Put);
            
            // Short Middle Puts (2x)
            var shortGreeks = OptionMath.CalculateGreeks(
                spotPrice, position.ShortStrike, timeToExpiry, 
                volatility, riskFreeRate, OptionType.Put);
            
            // Long Upper Put
            var upperLongGreeks = OptionMath.CalculateGreeks(
                spotPrice, position.LongUpperStrike, timeToExpiry, 
                volatility, riskFreeRate, OptionType.Put);
            
            // Aggregate Greeks (remember short strike has -2 quantity)
            greeks.NetDelta = lowerLongGreeks.Delta - 2 * shortGreeks.Delta + upperLongGreeks.Delta;
            greeks.NetGamma = lowerLongGreeks.Gamma - 2 * shortGreeks.Gamma + upperLongGreeks.Gamma;
            greeks.NetTheta = lowerLongGreeks.Theta - 2 * shortGreeks.Theta + upperLongGreeks.Theta;
            greeks.NetVega = lowerLongGreeks.Vega - 2 * shortGreeks.Vega + upperLongGreeks.Vega;
            greeks.NetRho = lowerLongGreeks.Rho - 2 * shortGreeks.Rho + upperLongGreeks.Rho;
            
            // Calculate risk metrics
            greeks.DeltaAdjustedExposure = greeks.NetDelta * spotPrice * 100; // SPX multiplier
            greeks.GammaRisk = greeks.NetGamma * spotPrice * spotPrice * 0.01m * 100; // 1% move
            greeks.DailyThetaDecay = greeks.NetTheta;
            greeks.VegaExposure = greeks.NetVega * 0.01m; // Per 1% vol change
            
            // Store component Greeks
            greeks.ComponentGreeks["LongLower"] = lowerLongGreeks.Delta;
            greeks.ComponentGreeks["Short"] = -2 * shortGreeks.Delta;
            greeks.ComponentGreeks["LongUpper"] = upperLongGreeks.Delta;
            
            return greeks;
        }

        private bool ShouldEnterBWB(ProbeSignal probeSignal)
        {
            // Check if probe confirmation is required
            if (_config.RequireProbeConfirmation)
            {
                if (probeSignal == null || probeSignal.Sentiment != ProbeSentiment.Bullish)
                {
                    return false;
                }
                
                // Need sufficient probe wins
                if (probeSignal.RecentWinRate < _config.MinProbeWins / 10m)
                {
                    return false;
                }
            }
            
            return true;
        }

        private DateTime? FindOptimalExpiration(List<OptionsQuote> chain, DateTime currentDate, int targetDTE)
        {
            var expirations = chain
                .Where(o => o.DTE >= _config.MinDTE && o.DTE <= _config.MaxDTE)
                .Select(o => o.Expiration)
                .Distinct()
                .OrderBy(e => Math.Abs((e - currentDate).Days - targetDTE))
                .ToList();
                
            return expirations.FirstOrDefault();
        }

        private BWBStrikes CalculateBWBStrikes(decimal spotPrice, BWBConfiguration config)
        {
            return new BWBStrikes
            {
                LongLowerStrike = Math.Round(spotPrice - config.LongLowerOffset / 5) * 5, // Round to $5
                ShortStrike = Math.Round(spotPrice - config.ShortStrikeOffset / 5) * 5,
                LongUpperStrike = Math.Round(spotPrice - config.LongUpperOffset / 5) * 5
            };
        }

        private async Task<BWBEntry> BuildBWBStructure(List<OptionsQuote> chain, BWBStrikes strikes, DateTime date)
        {
            var bwb = new BWBEntry
            {
                Symbol = "SPX",
                Expiration = chain.First().Expiration,
                LongLowerStrike = strikes.LongLowerStrike,
                ShortStrike = strikes.ShortStrike,
                LongUpperStrike = strikes.LongUpperStrike,
                Quantities = new[] { 1, -2, 1 }
            };
            
            // Get option quotes
            var lowerLong = chain.FirstOrDefault(o => 
                o.Strike == strikes.LongLowerStrike && o.OptionType == "PUT");
            var shortOpt = chain.FirstOrDefault(o => 
                o.Strike == strikes.ShortStrike && o.OptionType == "PUT");
            var upperLong = chain.FirstOrDefault(o => 
                o.Strike == strikes.LongUpperStrike && o.OptionType == "PUT");
            
            if (lowerLong != null && shortOpt != null && upperLong != null)
            {
                // Calculate theoretical credit (before execution costs)
                bwb.Credit = -lowerLong.Ask + 2 * shortOpt.Bid - upperLong.Ask;
                
                // Calculate max risk
                var wingWidth = strikes.ShortStrike - strikes.LongUpperStrike;
                bwb.MaxRisk = (wingWidth * 100) - bwb.Credit; // SPX multiplier
            }
            
            return bwb;
        }

        private bool ValidateBWB(BWBEntry bwb)
        {
            if (bwb == null) return false;
            
            // Check credit meets minimum
            if (bwb.Credit < _config.MinCredit)
                return false;
                
            // Check risk is within limits
            if (bwb.MaxRisk > _config.MaxRisk)
                return false;
                
            // Check strikes are properly ordered
            if (bwb.LongUpperStrike >= bwb.ShortStrike || 
                bwb.ShortStrike >= bwb.LongLowerStrike)
                return false;
                
            return true;
        }

        private async Task<BWBExecutionCost> CalculateExecutionCost(BWBEntry bwb, List<OptionsQuote> chain)
        {
            var cost = new BWBExecutionCost();
            
            // Get realistic fills for each leg
            var lowerLong = chain.FirstOrDefault(o => 
                o.Strike == bwb.LongLowerStrike && o.OptionType == "PUT");
            var shortOpt = chain.FirstOrDefault(o => 
                o.Strike == bwb.ShortStrike && o.OptionType == "PUT");
            var upperLong = chain.FirstOrDefault(o => 
                o.Strike == bwb.LongUpperStrike && o.OptionType == "PUT");
            
            if (lowerLong != null && shortOpt != null && upperLong != null)
            {
                // Buy orders fill at ask + slippage
                var lowerLongFill = await _fillEngine.GetRealisticFill(
                    new Order { Side = "BUY", Quantity = 1, LimitPrice = lowerLong.Ask },
                    new Quote { Bid = lowerLong.Bid, Ask = lowerLong.Ask },
                    MarketState.Normal);
                    
                // Sell orders fill at bid - slippage
                var shortFill = await _fillEngine.GetRealisticFill(
                    new Order { Side = "SELL", Quantity = 2, LimitPrice = shortOpt.Bid },
                    new Quote { Bid = shortOpt.Bid, Ask = shortOpt.Ask },
                    MarketState.Normal);
                    
                var upperLongFill = await _fillEngine.GetRealisticFill(
                    new Order { Side = "BUY", Quantity = 1, LimitPrice = upperLong.Ask },
                    new Quote { Bid = upperLong.Bid, Ask = upperLong.Ask },
                    MarketState.Normal);
                
                // Calculate net credit after realistic fills
                cost.NetCredit = -lowerLongFill.FillPrice + 
                                 2 * shortFill.FillPrice - 
                                 upperLongFill.FillPrice;
                                 
                cost.Slippage = (lowerLongFill.Slippage + 
                                shortFill.Slippage + 
                                upperLongFill.Slippage) * 100; // SPX multiplier
                                
                cost.Commission = 3 * 1.0m; // $1 per contract typical
                
                var wingWidth = bwb.ShortStrike - bwb.LongUpperStrike;
                cost.MaxRisk = (wingWidth * 100) - cost.NetCredit;
            }
            
            return cost;
        }

        private async Task<BWBOpportunity> EvaluateBWBOpportunity(
            List<OptionsQuote> chain, 
            BWBStrikes strikes, 
            int dte,
            decimal spotPrice)
        {
            var opp = new BWBOpportunity
            {
                Expiration = chain.First().Expiration,
                DTE = dte,
                Strikes = strikes
            };
            
            // Get option quotes
            var lowerLong = chain.FirstOrDefault(o => 
                o.Strike == strikes.LongLowerStrike && o.OptionType == "PUT");
            var shortOpt = chain.FirstOrDefault(o => 
                o.Strike == strikes.ShortStrike && o.OptionType == "PUT");
            var upperLong = chain.FirstOrDefault(o => 
                o.Strike == strikes.LongUpperStrike && o.OptionType == "PUT");
            
            if (lowerLong == null || shortOpt == null || upperLong == null)
                return null;
            
            // Calculate credit and risk
            opp.Credit = -lowerLong.Ask + 2 * shortOpt.Bid - upperLong.Ask;
            var wingWidth = strikes.ShortStrike - strikes.LongUpperStrike;
            opp.MaxRisk = (wingWidth * 100) - opp.Credit;
            
            // Calculate probability of profit (simplified)
            var probITM = 1 - Math.Abs(shortOpt.Delta); // Probability of expiring OTM
            opp.ProbabilityOfProfit = probITM;
            
            // Calculate expected value
            var winAmount = opp.Credit * 0.65m; // Target 65% profit
            var lossAmount = opp.MaxRisk;
            opp.ExpectedValue = (probITM * winAmount) - ((1 - probITM) * lossAmount);
            
            // Calculate Greeks
            var greeks = new PortfolioGreeks
            {
                NetDelta = lowerLong.Delta - 2 * shortOpt.Delta + upperLong.Delta,
                NetTheta = lowerLong.Theta - 2 * shortOpt.Theta + upperLong.Theta,
                NetVega = lowerLong.Vega - 2 * shortOpt.Vega + upperLong.Vega
            };
            opp.Greeks = greeks;
            
            // Score the opportunity (0-100)
            opp.Score = CalculateOpportunityScore(opp);
            
            return opp;
        }

        private decimal CalculateOpportunityScore(BWBOpportunity opp)
        {
            var score = 50m; // Base score
            
            // Credit quality (higher is better)
            if (opp.Credit > _config.TargetCredit)
                score += 15;
            else if (opp.Credit > _config.MinCredit)
                score += 5;
            else
                score -= 10;
            
            // Risk/reward ratio
            var riskReward = opp.Credit / opp.MaxRisk;
            score += riskReward * 50; // Up to 25 points for 0.5 R/R
            
            // Probability of profit
            score += (opp.ProbabilityOfProfit - 0.5m) * 40; // +/- 20 points
            
            // Expected value
            if (opp.ExpectedValue > 0)
                score += Math.Min(15, opp.ExpectedValue / 10);
            
            // Greeks assessment
            if (opp.Greeks.NetTheta > 0)
                score += 5;
            if (Math.Abs(opp.Greeks.NetDelta) < 0.1m)
                score += 5;
            
            return Math.Min(100, Math.Max(0, score));
        }

        private async Task UpdatePositionPricing(BWBPosition position, DateTime currentDate)
        {
            // Get current SPX price
            position.CurrentSpotPrice = await _dataManager.GetUnderlyingPrice("SPX", currentDate);
            
            // Get current options prices
            var chain = await _dataManager.GetOptionsChain("SPX", currentDate);
            if (chain == null || !chain.Any()) return;
            
            var currentChain = chain.Where(o => o.Expiration == position.Expiration).ToList();
            
            var lowerLong = currentChain.FirstOrDefault(o => 
                o.Strike == position.LongLowerStrike && o.OptionType == "PUT");
            var shortOpt = currentChain.FirstOrDefault(o => 
                o.Strike == position.ShortStrike && o.OptionType == "PUT");
            var upperLong = currentChain.FirstOrDefault(o => 
                o.Strike == position.LongUpperStrike && o.OptionType == "PUT");
            
            if (lowerLong != null && shortOpt != null && upperLong != null)
            {
                // Calculate current value (closing cost)
                var currentValue = lowerLong.Bid - 2 * shortOpt.Ask + upperLong.Bid;
                
                // P&L = Credit received - Current closing cost
                position.UnrealizedPnL = position.Credit + currentValue;
                
                // Update IV for Greeks calculation
                position.CurrentIV = (lowerLong.ImpliedVolatility + 
                                     shortOpt.ImpliedVolatility + 
                                     upperLong.ImpliedVolatility) / 3;
            }
        }
    }

    public class BWBStrikes
    {
        public decimal LongLowerStrike { get; set; }
        public decimal ShortStrike { get; set; }
        public decimal LongUpperStrike { get; set; }
    }

    public class BWBPosition
    {
        public string PositionId { get; set; }
        public DateTime EntryDate { get; set; }
        public DateTime Expiration { get; set; }
        public decimal LongLowerStrike { get; set; }
        public decimal ShortStrike { get; set; }
        public decimal LongUpperStrike { get; set; }
        public decimal Credit { get; set; }
        public decimal MaxRisk { get; set; }
        public decimal CurrentSpotPrice { get; set; }
        public decimal EntrySpotPrice { get; set; }
        public decimal UnrealizedPnL { get; set; }
        public int DTE { get; set; }
        public int OriginalDTE { get; set; }
        public decimal CurrentIV { get; set; }
        public string Status { get; set; }
    }

    public class BWBAnalysis
    {
        public DateTime Date { get; set; }
        public List<BWBOpportunity> Opportunities { get; set; }
        public BWBOpportunity BestOpportunity { get; set; }
        public decimal AverageCredit { get; set; }
        public decimal AverageRisk { get; set; }
        public bool RecommendEntry { get; set; }
        public string NoDataReason { get; set; }
    }

    public class BWBOpportunity
    {
        public DateTime Expiration { get; set; }
        public int DTE { get; set; }
        public BWBStrikes Strikes { get; set; }
        public decimal Credit { get; set; }
        public decimal MaxRisk { get; set; }
        public decimal ProbabilityOfProfit { get; set; }
        public decimal ExpectedValue { get; set; }
        public PortfolioGreeks Greeks { get; set; }
        public decimal Score { get; set; }
    }

    public class BWBExitSignal
    {
        public string PositionId { get; set; }
        public DateTime CurrentDate { get; set; }
        public bool ShouldExit { get; set; }
        public bool ConsiderExit { get; set; }
        public string ExitReason { get; set; }
        public decimal ExpectedPnL { get; set; }
        public decimal DeltaAtExit { get; set; }
    }

    public class BWBExecutionCost
    {
        public decimal NetCredit { get; set; }
        public decimal MaxRisk { get; set; }
        public decimal Slippage { get; set; }
        public decimal Commission { get; set; }
    }

    public class BWBPerformance
    {
        public string PositionId { get; set; }
        public DateTime EntryDate { get; set; }
        public DateTime ExitDate { get; set; }
        public decimal RealizedPnL { get; set; }
        public decimal ReturnPercent { get; set; }
        public int HoldingDays { get; set; }
        public string ExitReason { get; set; }
    }
}