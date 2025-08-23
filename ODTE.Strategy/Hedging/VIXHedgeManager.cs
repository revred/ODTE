using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ODTE.Backtest.Core;
using ODTE.Execution.Models;
using ODTE.Historical.DistributedStorage;
using ODTE.Contracts.Data;
using ContractsData = ODTE.Contracts.Data;

namespace ODTE.Strategy.Hedging
{
    /// <summary>
    /// Universal VIX hedge manager that can be used across multiple strategies
    /// Provides volatility protection through VIX call spreads
    /// </summary>
    public interface IVIXHedgeManager
    {
        Task<HedgeRequirement> CalculateHedgeRequirement(decimal portfolioExposure, decimal currentVIX, MarketConditions conditions);
        Task<List<VIXHedge>> GenerateHedges(HedgeRequirement requirement, DateTime date);
        Task<HedgePerformance> EvaluateHedgePerformance(List<VIXHedge> hedges, decimal vixMove);
        Task<VIXHedgeSignal> GetHedgeAdjustmentSignal(List<VIXHedge> activeHedges, decimal currentVIX);
        HedgeConfiguration GetOptimalConfiguration(decimal riskBudget, decimal protectionLevel);
    }

    public class VIXHedgeManager : IVIXHedgeManager
    {
        private readonly DistributedDatabaseManager _dataManager;
        private readonly VIXHedgeConfiguration _defaultConfig;
        private readonly Dictionary<string, VIXHedgeHistory> _hedgeHistory;
        private const decimal VIX_MULTIPLIER = 100m; // VIX contract multiplier

        public VIXHedgeManager(DistributedDatabaseManager dataManager, VIXHedgeConfiguration config = null)
        {
            _dataManager = dataManager;
            _defaultConfig = config ?? GetDefaultConfiguration();
            _hedgeHistory = new Dictionary<string, VIXHedgeHistory>();
        }

        public async Task<HedgeRequirement> CalculateHedgeRequirement(
            decimal portfolioExposure, 
            decimal currentVIX, 
            MarketConditions conditions)
        {
            var requirement = new HedgeRequirement
            {
                PortfolioExposure = portfolioExposure,
                CurrentVIX = currentVIX,
                MarketConditions = conditions
            };

            // Enhanced base hedge count calculation with 5%+ pullback protection
            requirement.BaseHedgeCount = CalculateEnhancedHedgeCount(portfolioExposure, currentVIX);
            
            // Adjust for market conditions (more responsive to moderate stress)
            requirement.ConditionMultiplier = GetEnhancedConditionMultiplier(conditions, currentVIX);
            
            // Enhanced VIX level adjustment (more sensitive to moderate increases)
            requirement.VIXMultiplier = GetEnhancedVIXLevelMultiplier(currentVIX);
            
            // Calculate final hedge count with pullback protection bias
            requirement.RecommendedHedgeCount = (int)Math.Ceiling(
                requirement.BaseHedgeCount * 
                requirement.ConditionMultiplier * 
                requirement.VIXMultiplier);
            
            // Enhanced limits for better protection
            var minHedges = Math.Max(_defaultConfig.MinHedgeCount, CalculateMinHedgesForProtection(portfolioExposure));
            requirement.RecommendedHedgeCount = Math.Min(
                _defaultConfig.MaxHedgeCount + 2, // Allow 2 extra hedges for protection
                Math.Max(minHedges, requirement.RecommendedHedgeCount));
            
            // Enhanced cost budget (allow more for better protection)
            requirement.MaxCostBudget = portfolioExposure * GetDynamicHedgeCostRatio(currentVIX, conditions);
            
            // Determine urgency (more responsive to moderate moves)
            requirement.Urgency = DetermineEnhancedHedgeUrgency(currentVIX, conditions);
            
            // Enhanced protection level targeting 5%+ moves
            requirement.TargetProtectionLevel = CalculateEnhancedProtectionLevel(portfolioExposure, currentVIX);
            
            // NEW: Add specific pullback protection metrics
            requirement.PullbackProtectionLevel = CalculatePullbackProtection(currentVIX);
            requirement.ModerateStressMultiplier = GetModerateStressMultiplier(currentVIX);
            
            return requirement;
        }

        public async Task<List<VIXHedge>> GenerateHedges(HedgeRequirement requirement, DateTime date)
        {
            var hedges = new List<VIXHedge>();
            
            // Get VIX options chain
            var vixChain = await _dataManager.GetOptionsChain("VIX", date);
            if (vixChain == null || !vixChain.Any())
            {
                return hedges;
            }

            // Find optimal expiration (45-60 DTE)
            var targetExpirations = vixChain
                .Where(o => o.DTE >= _defaultConfig.MinDTE && o.DTE <= _defaultConfig.MaxDTE)
                .Select(o => o.Expiration)
                .Distinct()
                .OrderBy(e => Math.Abs((e - date).Days - _defaultConfig.TargetDTE))
                .Take(requirement.RecommendedHedgeCount)
                .ToList();

            foreach (var expiration in targetExpirations)
            {
                var hedge = await BuildOptimalHedge(vixChain, expiration, requirement.CurrentVIX, date);
                if (hedge != null && hedge.Cost <= requirement.MaxCostBudget / requirement.RecommendedHedgeCount)
                {
                    hedges.Add(hedge);
                }
            }

            // If we couldn't build enough hedges with optimal parameters, try alternatives
            if (hedges.Count < requirement.RecommendedHedgeCount)
            {
                hedges.AddRange(await BuildAlternativeHedges(
                    vixChain, 
                    requirement, 
                    date, 
                    requirement.RecommendedHedgeCount - hedges.Count));
            }

            return hedges;
        }

        public async Task<HedgePerformance> EvaluateHedgePerformance(List<VIXHedge> hedges, decimal vixMove)
        {
            var performance = new HedgePerformance
            {
                VIXMove = vixMove,
                HedgeCount = hedges.Count
            };

            foreach (var hedge in hedges)
            {
                var payoff = CalculateHedgePayoff(hedge, hedge.LongStrike + vixMove);
                performance.TotalPayoff += payoff;
                performance.IndividualPayoffs.Add(hedge.HedgeId, payoff);
            }

            performance.TotalCost = hedges.Sum(h => h.Cost);
            performance.NetResult = performance.TotalPayoff - performance.TotalCost;
            performance.ReturnOnHedge = performance.TotalCost > 0 
                ? performance.NetResult / performance.TotalCost 
                : 0;

            // Calculate effectiveness metrics
            performance.EffectivenessRatio = CalculateEffectivenessRatio(performance, vixMove);
            performance.CostEfficiency = performance.TotalPayoff / Math.Max(1, performance.TotalCost);

            return performance;
        }

        public async Task<VIXHedgeSignal> GetHedgeAdjustmentSignal(List<VIXHedge> activeHedges, decimal currentVIX)
        {
            var signal = new VIXHedgeSignal
            {
                CurrentVIX = currentVIX,
                ActiveHedgeCount = activeHedges.Count,
                SignalDate = DateTime.Now
            };

            if (!activeHedges.Any())
            {
                signal.Action = HedgeAction.Add;
                signal.Quantity = _defaultConfig.MinHedgeCount;
                signal.Reason = "No active hedges";
                return signal;
            }

            // Check for VIX spike - partial profit taking
            var avgEntryVIX = activeHedges.Average(h => h.EntryVIX);
            var vixIncrease = currentVIX - avgEntryVIX;

            if (vixIncrease >= _defaultConfig.PartialCloseThreshold)
            {
                signal.Action = HedgeAction.PartialClose;
                signal.Quantity = (int)(activeHedges.Count * _defaultConfig.PartialClosePercent);
                signal.Reason = $"VIX spike of {vixIncrease:F2} points";
                signal.ExpectedProfit = await CalculateExpectedProfit(activeHedges, currentVIX, signal.Quantity);
                return signal;
            }

            // Check for expiring hedges
            var expiringHedges = activeHedges.Where(h => h.DTE <= 15).ToList();
            if (expiringHedges.Any())
            {
                signal.Action = HedgeAction.Roll;
                signal.Quantity = expiringHedges.Count;
                signal.Reason = $"{expiringHedges.Count} hedges expiring soon";
                signal.HedgesToRoll = expiringHedges.Select(h => h.HedgeId).ToList();
                return signal;
            }

            // Check if we need more hedges based on VIX level
            if (currentVIX > _defaultConfig.HighVIXThreshold && activeHedges.Count < _defaultConfig.MaxHedgeCount)
            {
                signal.Action = HedgeAction.Add;
                signal.Quantity = Math.Min(2, _defaultConfig.MaxHedgeCount - activeHedges.Count);
                signal.Reason = $"High VIX environment ({currentVIX:F2})";
                return signal;
            }

            // Check if we have too many hedges in low VIX
            if (currentVIX < _defaultConfig.LowVIXThreshold && activeHedges.Count > _defaultConfig.MinHedgeCount)
            {
                signal.Action = HedgeAction.Reduce;
                signal.Quantity = activeHedges.Count - _defaultConfig.MinHedgeCount;
                signal.Reason = $"Low VIX environment ({currentVIX:F2})";
                return signal;
            }

            signal.Action = HedgeAction.Hold;
            signal.Reason = "No adjustment needed";
            return signal;
        }

        public HedgeConfiguration GetOptimalConfiguration(decimal riskBudget, decimal protectionLevel)
        {
            var config = new HedgeConfiguration
            {
                RiskBudget = riskBudget,
                ProtectionLevel = protectionLevel
            };

            // Calculate optimal strikes based on protection level
            if (protectionLevel >= 0.90m) // High protection
            {
                config.LongStrikeOffset = 0; // ATM
                config.ShortStrikeOffset = 10; // 10 points OTM
                config.Ratio = 2; // Buy 2, Sell 1 for better protection
            }
            else if (protectionLevel >= 0.70m) // Medium protection
            {
                config.LongStrikeOffset = 2; // Slightly OTM
                config.ShortStrikeOffset = 10;
                config.Ratio = 1; // Standard 1:1 spread
            }
            else // Low protection (cost-efficient)
            {
                config.LongStrikeOffset = 5; // Further OTM
                config.ShortStrikeOffset = 15;
                config.Ratio = 1;
            }

            // Calculate hedge count based on budget
            var estimatedCostPerHedge = 50m; // Base estimate
            config.HedgeCount = Math.Max(1, (int)(riskBudget / estimatedCostPerHedge));

            // Adjust DTE based on protection needs
            config.TargetDTE = protectionLevel >= 0.80m ? 60 : 45;

            return config;
        }

        private async Task<VIXHedge> BuildOptimalHedge(
            List<OptionsQuote> chain, 
            DateTime expiration, 
            decimal currentVIX,
            DateTime date)
        {
            var dte = (expiration - date).Days;
            var expiryChain = chain.Where(o => o.Expiration == expiration && o.OptionType == "CALL").ToList();
            
            if (!expiryChain.Any()) return null;

            // Enhanced strike selection for 5%+ pullback protection
            var (longStrike, shortStrike) = CalculateOptimalHedgeStrikes(currentVIX, dte);

            // Get quotes with improved strike matching
            var longCall = FindBestStrikeMatch(expiryChain, longStrike);
            var shortCall = FindBestStrikeMatch(expiryChain, shortStrike);

            if (longCall == null || shortCall == null) return null;

            var hedge = new VIXHedge
            {
                HedgeId = Guid.NewGuid().ToString(),
                EntryDate = date,
                Expiration = expiration,
                LongStrike = longCall.Strike,
                ShortStrike = shortCall.Strike,
                EntryVIX = currentVIX,
                DTE = dte
            };

            // Calculate cost (debit spread) with enhanced pricing
            hedge.Cost = CalculateEnhancedHedgeCost(longCall, shortCall);
            hedge.MaxPayoff = (hedge.ShortStrike - hedge.LongStrike) * VIX_MULTIPLIER;
            
            // Calculate Greeks
            hedge.Vega = (longCall.Vega - shortCall.Vega) * VIX_MULTIPLIER;
            hedge.Theta = (longCall.Theta - shortCall.Theta) * VIX_MULTIPLIER;
            hedge.Delta = (longCall.Delta - shortCall.Delta) * VIX_MULTIPLIER;

            // Enhanced protection metrics for 5%+ moves
            hedge.BreakevenVIX = hedge.LongStrike + (hedge.Cost / VIX_MULTIPLIER);
            hedge.ProtectionStart = hedge.LongStrike;
            hedge.MaxProtectionVIX = hedge.ShortStrike;
            
            // NEW: 5% pullback specific metrics
            hedge.PullbackActivationVIX = Math.Max(hedge.LongStrike, currentVIX * 1.15m); // 15% VIX increase
            hedge.ModerateStressPayoff = CalculateModerateStressPayoff(hedge, currentVIX * 1.25m); // 25% VIX increase

            return hedge;
        }

        private (decimal longStrike, decimal shortStrike) CalculateOptimalHedgeStrikes(decimal currentVIX, int dte)
        {
            // Enhanced strike selection optimized for 5%+ pullback protection
            decimal longStrikeOffset;
            decimal spreadWidth;
            
            // Adjust strikes based on current VIX and DTE for optimal 5% pullback protection
            if (currentVIX < 18)
            {
                // Low VIX: Position for moderate increase (prepare for 5% pullback)
                longStrikeOffset = 1m; // Closer to ATM for early activation
                spreadWidth = 8m;      // Narrower spread for lower cost
            }
            else if (currentVIX < 25)
            {
                // Moderate VIX: Optimal range for 5% pullback protection
                longStrikeOffset = 2m; // Slightly OTM
                spreadWidth = 10m;     // Standard spread
            }
            else
            {
                // High VIX: Wider protection for larger moves
                longStrikeOffset = 3m; // Further OTM
                spreadWidth = 12m;     // Wider spread for more protection
            }
            
            // Adjust for DTE
            if (dte < 30)
            {
                longStrikeOffset -= 1m; // Closer strikes for shorter time
                spreadWidth -= 2m;      // Narrower spreads
            }
            else if (dte > 60)
            {
                longStrikeOffset += 1m; // Further strikes for longer time
                spreadWidth += 2m;      // Wider spreads
            }
            
            var longStrike = Math.Round(currentVIX + longStrikeOffset);
            var shortStrike = longStrike + spreadWidth;
            
            return (longStrike, shortStrike);
        }

        private OptionsQuote FindBestStrikeMatch(List<OptionsQuote> chain, decimal targetStrike)
        {
            // Find the best available strike match with liquidity consideration
            var candidates = chain
                .Where(o => o.Volume > 0 || o.OpenInterest > 0) // Ensure liquidity
                .OrderBy(o => Math.Abs(o.Strike - targetStrike))
                .Take(3) // Consider top 3 closest strikes
                .ToList();
            
            if (!candidates.Any())
            {
                // Fallback to closest strike regardless of liquidity
                return chain.OrderBy(o => Math.Abs(o.Strike - targetStrike)).FirstOrDefault();
            }
            
            // Prefer strikes with better liquidity
            return candidates.OrderByDescending(o => o.Volume + o.OpenInterest).First();
        }

        private decimal CalculateEnhancedHedgeCost(OptionsQuote longCall, OptionsQuote shortCall)
        {
            // Enhanced cost calculation with bid-ask spread consideration
            var longCost = longCall.Ask;
            var shortCredit = shortCall.Bid;
            
            // Adjust for wider spreads in less liquid options
            var longSpread = longCall.Ask - longCall.Bid;
            var shortSpread = shortCall.Ask - shortCall.Bid;
            
            if (longSpread > longCall.Mid * 0.10m) // Wide spread (>10% of mid)
            {
                longCost = longCall.Mid + longSpread * 0.25m; // Use mid + 25% of spread
            }
            
            if (shortSpread > shortCall.Mid * 0.10m)
            {
                shortCredit = shortCall.Mid - shortSpread * 0.25m; // Use mid - 25% of spread
            }
            
            return (longCost - shortCredit) * VIX_MULTIPLIER;
        }

        private decimal CalculateModerateStressPayoff(VIXHedge hedge, decimal stressVIX)
        {
            // Calculate expected payoff during moderate stress (5% pullback scenario)
            if (stressVIX <= hedge.LongStrike)
                return -hedge.Cost; // Hedge expires worthless
            
            if (stressVIX >= hedge.ShortStrike)
                return hedge.MaxPayoff - hedge.Cost; // Maximum profit
            
            // Linear interpolation for partial profit
            var intrinsicValue = (stressVIX - hedge.LongStrike) * VIX_MULTIPLIER;
            return intrinsicValue - hedge.Cost;
        }

        private async Task<List<VIXHedge>> BuildAlternativeHedges(
            List<OptionsQuote> chain,
            HedgeRequirement requirement,
            DateTime date,
            int count)
        {
            var alternativeHedges = new List<VIXHedge>();
            
            // Try different strike combinations
            var strikeOffsets = new[] { 0, 2, 5, 7 }; // ATM to OTM
            var widths = new[] { 10, 8, 12, 15 }; // Different spread widths

            foreach (var offset in strikeOffsets)
            {
                foreach (var width in widths)
                {
                    if (alternativeHedges.Count >= count) break;

                    var config = new VIXHedgeConfiguration
                    {
                        LongStrikeOffset = offset,
                        SpreadWidth = width,
                        MinDTE = 30,
                        MaxDTE = 90
                    };

                    var hedge = await BuildHedgeWithConfig(chain, requirement.CurrentVIX, date, config);
                    if (hedge != null && hedge.Cost <= requirement.MaxCostBudget / requirement.RecommendedHedgeCount)
                    {
                        alternativeHedges.Add(hedge);
                    }
                }
            }

            return alternativeHedges.Take(count).ToList();
        }

        private async Task<VIXHedge> BuildHedgeWithConfig(
            List<OptionsQuote> chain,
            decimal currentVIX,
            DateTime date,
            VIXHedgeConfiguration config)
        {
            var validExpirations = chain
                .Where(o => o.DTE >= config.MinDTE && o.DTE <= config.MaxDTE)
                .Select(o => o.Expiration)
                .Distinct()
                .OrderBy(e => e)
                .FirstOrDefault();

            if (validExpirations == default) return null;

            var expiryChain = chain.Where(o => o.Expiration == validExpirations && o.OptionType == "CALL").ToList();
            
            var longStrike = Math.Round(currentVIX + config.LongStrikeOffset);
            var shortStrike = longStrike + config.SpreadWidth;

            var longCall = expiryChain.OrderBy(o => Math.Abs(o.Strike - longStrike)).FirstOrDefault();
            var shortCall = expiryChain.OrderBy(o => Math.Abs(o.Strike - shortStrike)).FirstOrDefault();

            if (longCall == null || shortCall == null) return null;

            return new VIXHedge
            {
                HedgeId = Guid.NewGuid().ToString(),
                EntryDate = date,
                Expiration = validExpirations,
                LongStrike = longCall.Strike,
                ShortStrike = shortCall.Strike,
                EntryVIX = currentVIX,
                Cost = (longCall.Ask - shortCall.Bid) * VIX_MULTIPLIER,
                MaxPayoff = config.SpreadWidth * VIX_MULTIPLIER,
                DTE = (validExpirations - date).Days
            };
        }

        private int CalculateEnhancedHedgeCount(decimal exposure, decimal currentVIX)
        {
            // Enhanced calculation targeting 5%+ pullback protection
            var baseCount = Math.Max(2, (int)(exposure / 4000m)); // More hedges per dollar (was 5000)
            
            // Add extra hedges based on VIX level for early protection
            if (currentVIX >= 20) baseCount += 1; // Add hedge when VIX above 20
            if (currentVIX >= 25) baseCount += 1; // Add another when above 25
            if (exposure > 15000m) baseCount += 1; // Extra hedge for larger portfolios
            
            return baseCount;
        }

        private int CalculateBaseHedgeCount(decimal exposure)
        {
            // Legacy method for backward compatibility
            return Math.Max(2, (int)(exposure / 5000m));
        }

        private decimal GetEnhancedConditionMultiplier(MarketConditions conditions, decimal currentVIX)
        {
            var baseMultiplier = conditions switch
            {
                MarketConditions.Calm => 0.8m,      // Increased from 0.5m for better protection
                MarketConditions.Normal => 1.2m,    // Increased from 1.0m
                MarketConditions.Volatile => 1.8m,  // Increased from 1.5m
                MarketConditions.Crisis => 2.5m,    // Increased from 2.0m
                _ => 1.2m
            };
            
            // Additional multiplier for VIX in "worry zone" (18-25 range)
            if (currentVIX >= 18 && currentVIX <= 25)
            {
                baseMultiplier += 0.3m; // Extra protection in moderate stress
            }
            
            return baseMultiplier;
        }

        private decimal GetEnhancedVIXLevelMultiplier(decimal vix)
        {
            // More responsive to moderate VIX increases (better 5% pullback protection)
            if (vix < 15) return 0.9m;   // Increased from 0.75m
            if (vix < 18) return 1.1m;   // New tier for early warning
            if (vix < 22) return 1.4m;   // Enhanced moderate protection
            if (vix < 28) return 1.8m;   // Better high vol protection
            if (vix < 35) return 2.2m;   // Crisis protection
            return 2.8m;                 // Extreme crisis
        }

        private decimal GetConditionMultiplier(MarketConditions conditions)
        {
            return conditions switch
            {
                MarketConditions.Calm => 0.5m,
                MarketConditions.Normal => 1.0m,
                MarketConditions.Volatile => 1.5m,
                MarketConditions.Crisis => 2.0m,
                _ => 1.0m
            };
        }

        private decimal GetVIXLevelMultiplier(decimal vix)
        {
            if (vix < 15) return 0.75m;
            if (vix < 20) return 1.0m;
            if (vix < 25) return 1.25m;
            if (vix < 30) return 1.5m;
            return 2.0m;
        }

        private HedgeUrgency DetermineHedgeUrgency(decimal vix, MarketConditions conditions)
        {
            if (vix > 30 || conditions == MarketConditions.Crisis)
                return HedgeUrgency.Critical;
            if (vix > 25 || conditions == MarketConditions.Volatile)
                return HedgeUrgency.High;
            if (vix > 20)
                return HedgeUrgency.Medium;
            return HedgeUrgency.Low;
        }

        private int CalculateMinHedgesForProtection(decimal exposure)
        {
            // Ensure minimum hedges based on exposure for 5%+ protection
            if (exposure > 20000m) return 4;
            if (exposure > 15000m) return 3;
            if (exposure > 10000m) return 3;
            return 2;
        }

        private decimal GetDynamicHedgeCostRatio(decimal vix, MarketConditions conditions)
        {
            // Allow higher hedge budget for better protection
            var baseCostRatio = 0.025m; // Increased from 0.02m
            
            // Increase budget when we need more protection
            if (vix >= 20) baseCostRatio += 0.01m;
            if (vix >= 25) baseCostRatio += 0.01m;
            if (conditions == MarketConditions.Volatile) baseCostRatio += 0.005m;
            if (conditions == MarketConditions.Crisis) baseCostRatio += 0.015m;
            
            return Math.Min(0.05m, baseCostRatio); // Cap at 5% of exposure
        }

        private HedgeUrgency DetermineEnhancedHedgeUrgency(decimal vix, MarketConditions conditions)
        {
            // More responsive urgency for moderate stress (5%+ pullback preparation)
            if (vix > 30 || conditions == MarketConditions.Crisis)
                return HedgeUrgency.Critical;
            if (vix > 25 || conditions == MarketConditions.Volatile)
                return HedgeUrgency.High;
            if (vix > 20) // Lowered threshold for earlier action
                return HedgeUrgency.High;
            if (vix > 18) // New tier for early warning
                return HedgeUrgency.Medium;
            return HedgeUrgency.Low;
        }

        private decimal CalculateEnhancedProtectionLevel(decimal exposure, decimal vix)
        {
            // Enhanced protection targeting 5%+ moves specifically
            var exposureFactor = Math.Min(1.0m, exposure / 30000m); // More responsive to smaller exposures
            var vixFactor = Math.Min(1.0m, vix / 30m); // More responsive to moderate VIX
            var pullbackFactor = GetPullbackProtectionFactor(vix);
            
            return (exposureFactor + vixFactor + pullbackFactor) / 3;
        }

        private decimal CalculatePullbackProtection(decimal vix)
        {
            // Specific protection level for 5%+ pullbacks
            if (vix >= 25) return 0.90m; // Very high protection
            if (vix >= 22) return 0.80m; // High protection
            if (vix >= 19) return 0.70m; // Good protection
            if (vix >= 16) return 0.60m; // Moderate protection
            return 0.50m; // Base protection
        }

        private decimal GetModerateStressMultiplier(decimal vix)
        {
            // Multiplier specifically for moderate stress scenarios
            if (vix >= 20 && vix <= 30) return 1.5m; // Sweet spot for 5% pullbacks
            if (vix >= 18 && vix <= 32) return 1.3m; // Extended moderate range
            return 1.0m;
        }

        private decimal GetPullbackProtectionFactor(decimal vix)
        {
            // Factor that increases protection as we approach pullback territory
            if (vix >= 20) return 1.0m; // Maximum protection factor
            if (vix >= 18) return 0.8m;
            if (vix >= 16) return 0.6m;
            if (vix >= 14) return 0.4m;
            return 0.2m; // Minimum protection factor
        }

        private decimal CalculateProtectionLevel(decimal exposure, decimal vix)
        {
            // Legacy method for backward compatibility
            var exposureFactor = Math.Min(1.0m, exposure / 50000m);
            var vixFactor = Math.Min(1.0m, vix / 40m);
            return (exposureFactor + vixFactor) / 2;
        }

        private decimal CalculateHedgePayoff(VIXHedge hedge, decimal finalVIX)
        {
            if (finalVIX <= hedge.LongStrike)
                return -hedge.Cost; // Hedge expires worthless
            
            if (finalVIX >= hedge.ShortStrike)
                return hedge.MaxPayoff - hedge.Cost; // Maximum profit
            
            // Partial profit
            var intrinsicValue = (finalVIX - hedge.LongStrike) * VIX_MULTIPLIER;
            return intrinsicValue - hedge.Cost;
        }

        private decimal CalculateEffectivenessRatio(HedgePerformance performance, decimal vixMove)
        {
            if (vixMove <= 0) return 0;
            
            // Ratio of hedge payoff to VIX move
            var expectedPayoff = vixMove * 100 * performance.HedgeCount; // Simplified expectation
            return performance.TotalPayoff / Math.Max(1, expectedPayoff);
        }

        private async Task<decimal> CalculateExpectedProfit(List<VIXHedge> hedges, decimal currentVIX, int quantity)
        {
            decimal totalProfit = 0;
            var hedgesToClose = hedges.OrderByDescending(h => h.EntryVIX).Take(quantity);
            
            foreach (var hedge in hedgesToClose)
            {
                var payoff = CalculateHedgePayoff(hedge, currentVIX);
                totalProfit += Math.Max(0, payoff);
            }
            
            return totalProfit;
        }

        private VIXHedgeConfiguration GetDefaultConfiguration()
        {
            return new VIXHedgeConfiguration
            {
                TargetDTE = 50,
                MinDTE = 45,
                MaxDTE = 60,
                LongStrikeOffset = 2,
                SpreadWidth = 10,
                MinHedgeCount = 2,
                MaxHedgeCount = 6,
                HedgeCostRatio = 0.02m,
                PartialCloseThreshold = 3,
                PartialClosePercent = 0.5m,
                HighVIXThreshold = 25,
                LowVIXThreshold = 15
            };
        }
    }

    // Supporting classes
    public class VIXHedgeConfiguration
    {
        public int TargetDTE { get; set; } = 50;
        public int MinDTE { get; set; } = 45;
        public int MaxDTE { get; set; } = 60;
        public decimal LongStrikeOffset { get; set; } = 2;
        public decimal SpreadWidth { get; set; } = 10;
        public int MinHedgeCount { get; set; } = 2;
        public int MaxHedgeCount { get; set; } = 6;
        public decimal HedgeCostRatio { get; set; } = 0.02m;
        public decimal PartialCloseThreshold { get; set; } = 3;
        public decimal PartialClosePercent { get; set; } = 0.5m;
        public decimal HighVIXThreshold { get; set; } = 25;
        public decimal LowVIXThreshold { get; set; } = 15;
    }

    public class HedgeRequirement
    {
        public decimal PortfolioExposure { get; set; }
        public decimal CurrentVIX { get; set; }
        public MarketConditions MarketConditions { get; set; }
        public int BaseHedgeCount { get; set; }
        public decimal ConditionMultiplier { get; set; }
        public decimal VIXMultiplier { get; set; }
        public int RecommendedHedgeCount { get; set; }
        public decimal MaxCostBudget { get; set; }
        public HedgeUrgency Urgency { get; set; }
        public decimal TargetProtectionLevel { get; set; }
        
        // Enhanced properties for 5%+ pullback protection
        public decimal PullbackProtectionLevel { get; set; }
        public decimal ModerateStressMultiplier { get; set; }
        public bool RequiresEnhancedProtection => PullbackProtectionLevel >= 0.70m;
        public bool IsInModerateStressZone => CurrentVIX >= 18 && CurrentVIX <= 30;
    }

    public class VIXHedge
    {
        public string HedgeId { get; set; }
        public DateTime EntryDate { get; set; }
        public DateTime Expiration { get; set; }
        public decimal LongStrike { get; set; }
        public decimal ShortStrike { get; set; }
        public decimal Cost { get; set; }
        public decimal MaxPayoff { get; set; }
        public decimal EntryVIX { get; set; }
        public int DTE { get; set; }
        public decimal Vega { get; set; }
        public decimal Theta { get; set; }
        public decimal Delta { get; set; }
        public decimal BreakevenVIX { get; set; }
        public decimal ProtectionStart { get; set; }
        public decimal MaxProtectionVIX { get; set; }
        
        // Enhanced properties for 5%+ pullback protection
        public decimal PullbackActivationVIX { get; set; }
        public decimal ModerateStressPayoff { get; set; }
        public bool IsEffectiveFor5PercentPullback => PullbackActivationVIX <= EntryVIX * 1.20m;
        public decimal CostEfficiencyRatio => MaxPayoff > 0 ? Cost / MaxPayoff : 1m;
    }

    public class HedgePerformance
    {
        public decimal VIXMove { get; set; }
        public int HedgeCount { get; set; }
        public decimal TotalCost { get; set; }
        public decimal TotalPayoff { get; set; }
        public decimal NetResult { get; set; }
        public decimal ReturnOnHedge { get; set; }
        public decimal EffectivenessRatio { get; set; }
        public decimal CostEfficiency { get; set; }
        public Dictionary<string, decimal> IndividualPayoffs { get; set; } = new();
    }

    public class VIXHedgeSignal
    {
        public DateTime SignalDate { get; set; }
        public decimal CurrentVIX { get; set; }
        public int ActiveHedgeCount { get; set; }
        public HedgeAction Action { get; set; }
        public int Quantity { get; set; }
        public string Reason { get; set; }
        public decimal ExpectedProfit { get; set; }
        public List<string> HedgesToRoll { get; set; } = new();
    }

    public class HedgeConfiguration
    {
        public decimal RiskBudget { get; set; }
        public decimal ProtectionLevel { get; set; }
        public decimal LongStrikeOffset { get; set; }
        public decimal ShortStrikeOffset { get; set; }
        public int Ratio { get; set; }
        public int HedgeCount { get; set; }
        public int TargetDTE { get; set; }
    }

    public class VIXHedgeHistory
    {
        public string HedgeId { get; set; }
        public DateTime EntryDate { get; set; }
        public DateTime ExitDate { get; set; }
        public decimal EntryVIX { get; set; }
        public decimal ExitVIX { get; set; }
        public decimal RealizedPnL { get; set; }
        public string ExitReason { get; set; }
    }

    public enum MarketConditions
    {
        Calm,
        Normal,
        Volatile,
        Crisis
    }

    public enum HedgeAction
    {
        Hold,
        Add,
        Reduce,
        PartialClose,
        Roll
    }

    public enum HedgeUrgency
    {
        Low,
        Medium,
        High,
        Critical
    }
}