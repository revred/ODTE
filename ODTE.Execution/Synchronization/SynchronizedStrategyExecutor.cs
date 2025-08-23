using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ODTE.Execution.Engine;
using ODTE.Execution.Models;
using ODTE.Contracts.Strategy;
using ODTE.Contracts.Data;
using ODTE.Historical.DistributedStorage;

namespace ODTE.Execution.Synchronization
{
    /// <summary>
    /// Universal synchronized executor for multi-component strategies
    /// Coordinates probe entries, core positions, and hedge adjustments
    /// Can be used for any strategy requiring synchronized execution
    /// </summary>
    public interface ISynchronizedStrategyExecutor
    {
        Task<SynchronizedExecutionPlan> GenerateExecutionPlan(DateTime date, StrategyComponents components);
        Task<ExecutionResult> ExecutePlan(SynchronizedExecutionPlan plan);
        Task<PortfolioState> GetCurrentPortfolioState();
        Task<RiskMetrics> CalculateRiskMetrics();
        Task<bool> ValidateExecutionConstraints(SynchronizedExecutionPlan plan);
    }

    public class SynchronizedStrategyExecutor : ISynchronizedStrategyExecutor
    {
        private readonly RealisticFillEngine _fillEngine;
        private readonly DistributedDatabaseManager _dataManager;
        private readonly IVIXHedgeManager _hedgeManager;
        private readonly SynchronizationConfig _config;
        private readonly Dictionary<string, Position> _activePositions;
        private readonly ExecutionHistory _executionHistory;
        private decimal _currentCapitalUsed;
        private decimal _totalCapitalAvailable;

        public SynchronizedStrategyExecutor(
            RealisticFillEngine fillEngine,
            DistributedDatabaseManager dataManager,
            IVIXHedgeManager hedgeManager,
            SynchronizationConfig config)
        {
            _fillEngine = fillEngine;
            _dataManager = dataManager;
            _hedgeManager = hedgeManager;
            _config = config;
            _activePositions = new Dictionary<string, Position>();
            _executionHistory = new ExecutionHistory();
            _totalCapitalAvailable = config.TotalCapital;
        }

        public async Task<SynchronizedExecutionPlan> GenerateExecutionPlan(
            DateTime date, 
            StrategyComponents components)
        {
            var plan = new SynchronizedExecutionPlan
            {
                ExecutionDate = date,
                DayOfWeek = date.DayOfWeek
            };

            // Step 1: Analyze current portfolio state
            var portfolioState = await GetCurrentPortfolioState();
            plan.CurrentState = portfolioState;

            // Step 2: Check for positions that need to exit
            plan.Exits = await GenerateExitOrders(portfolioState, date);

            // Step 3: Check probe signals for market sentiment
            var probeSentiment = components.ProbeScout?.GetSentiment() ?? ProbeSentiment.Neutral;
            plan.ProbeSentiment = probeSentiment;

            // Step 4: Generate probe entries based on schedule
            if (ShouldEnterProbes(date, probeSentiment))
            {
                plan.ProbeEntries = await GenerateProbeEntries(
                    components.ProbeScout, 
                    date, 
                    GetProbeCountForDay(date.DayOfWeek));
            }

            // Step 5: Generate core entries if conditions are met
            if (ShouldEnterCore(date, probeSentiment, portfolioState))
            {
                plan.CoreEntries = await GenerateCoreEntries(
                    components.CoreEngine, 
                    date, 
                    probeSentiment);
            }

            // Step 6: Calculate hedge requirements
            var totalExposure = CalculateTotalExposure(portfolioState, plan);
            var currentVIX = await GetCurrentVIX(date);
            
            var hedgeRequirement = await _hedgeManager.CalculateHedgeRequirement(
                totalExposure, 
                currentVIX, 
                DetermineMarketConditions(probeSentiment, currentVIX));
            
            plan.HedgeAdjustments = await GenerateHedgeAdjustments(
                hedgeRequirement, 
                portfolioState.ActiveHedges, 
                date);

            // Step 7: Validate capital requirements
            plan.EstimatedCapitalRequired = CalculateCapitalRequired(plan);
            plan.CapitalAvailable = _totalCapitalAvailable - _currentCapitalUsed;
            
            // Step 8: Apply risk limits and position sizing
            await ApplyRiskLimits(plan, portfolioState);

            // Step 9: Prioritize executions if capital is limited
            if (plan.EstimatedCapitalRequired > plan.CapitalAvailable)
            {
                PrioritizeExecutions(plan);
            }

            // Step 10: Calculate projected Greeks
            plan.ProjectedGreeks = await CalculateProjectedGreeks(plan, portfolioState);

            return plan;
        }

        public async Task<ExecutionResult> ExecutePlan(SynchronizedExecutionPlan plan)
        {
            var result = new ExecutionResult
            {
                ExecutionDate = plan.ExecutionDate,
                PlannedExecutions = CountPlannedExecutions(plan),
                ExecutionDetails = new List<ExecutionDetail>()
            };

            try
            {
                // Validate constraints before execution
                if (!await ValidateExecutionConstraints(plan))
                {
                    result.Success = false;
                    result.FailureReason = "Execution constraints validation failed";
                    return result;
                }

                // Execute exits first (free up capital)
                if (plan.Exits?.Any() == true)
                {
                    var exitResults = await ExecuteExits(plan.Exits);
                    result.ExecutionDetails.AddRange(exitResults);
                    result.ExitsExecuted = exitResults.Count(e => e.Success);
                }

                // Execute hedge adjustments
                if (plan.HedgeAdjustments?.Any() == true)
                {
                    var hedgeResults = await ExecuteHedgeAdjustments(plan.HedgeAdjustments);
                    result.ExecutionDetails.AddRange(hedgeResults);
                    result.HedgesExecuted = hedgeResults.Count(h => h.Success);
                }

                // Execute probe entries
                if (plan.ProbeEntries?.Any() == true)
                {
                    var probeResults = await ExecuteProbeEntries(plan.ProbeEntries);
                    result.ExecutionDetails.AddRange(probeResults);
                    result.ProbesExecuted = probeResults.Count(p => p.Success);
                }

                // Execute core entries
                if (plan.CoreEntries?.Any() == true)
                {
                    var coreResults = await ExecuteCoreEntries(plan.CoreEntries);
                    result.ExecutionDetails.AddRange(coreResults);
                    result.CoreExecuted = coreResults.Count(c => c.Success);
                }

                // Update capital usage
                await UpdateCapitalUsage();

                // Record execution history
                _executionHistory.RecordExecution(plan, result);

                result.Success = true;
                result.TotalExecuted = result.ExecutionDetails.Count(e => e.Success);
                result.TotalFailed = result.ExecutionDetails.Count(e => !e.Success);
                result.NetCapitalChange = result.ExecutionDetails.Sum(e => e.CapitalImpact);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.FailureReason = $"Execution failed: {ex.Message}";
            }

            return result;
        }

        public async Task<PortfolioState> GetCurrentPortfolioState()
        {
            var state = new PortfolioState
            {
                Timestamp = DateTime.Now,
                ActivePositions = _activePositions.Values.ToList(),
                TotalExposure = _activePositions.Values.Sum(p => p.Risk),
                UnrealizedPnL = _activePositions.Values.Sum(p => p.UnrealizedPnL),
                RealizedPnL = _executionHistory.TotalRealizedPnL
            };

            // Categorize positions
            state.ProbePositions = _activePositions.Values
                .Where(p => p.PositionType == PositionType.Probe)
                .ToList();
            
            state.CorePositions = _activePositions.Values
                .Where(p => p.PositionType == PositionType.Core)
                .ToList();
            
            state.ActiveHedges = _activePositions.Values
                .Where(p => p.PositionType == PositionType.Hedge)
                .ToList();

            // Calculate metrics
            state.ProbeCount = state.ProbePositions.Count;
            state.CoreCount = state.CorePositions.Count;
            state.HedgeCount = state.ActiveHedges.Count;
            
            state.ProbeExposure = state.ProbePositions.Sum(p => p.Risk);
            state.CoreExposure = state.CorePositions.Sum(p => p.Risk);
            state.HedgeCost = state.ActiveHedges.Sum(h => h.Cost);

            return state;
        }

        public async Task<RiskMetrics> CalculateRiskMetrics()
        {
            var metrics = new RiskMetrics();
            var portfolioState = await GetCurrentPortfolioState();
            
            // Portfolio-level metrics
            metrics.TotalExposure = portfolioState.TotalExposure;
            metrics.NetExposure = metrics.TotalExposure - portfolioState.ActiveHedges.Sum(h => h.MaxPayoff);
            metrics.CapitalUtilization = _currentCapitalUsed / _totalCapitalAvailable;
            
            // Calculate Value at Risk (VaR) - simplified
            metrics.VaR95 = CalculateVaR(portfolioState, 0.95m);
            metrics.VaR99 = CalculateVaR(portfolioState, 0.99m);
            
            // Stress test metrics
            metrics.DrawdownAt5PercentMove = await CalculateDrawdown(portfolioState, -0.05m);
            metrics.DrawdownAt7PercentMove = await CalculateDrawdown(portfolioState, -0.07m);
            metrics.DrawdownAt10PercentMove = await CalculateDrawdown(portfolioState, -0.10m);
            
            // Protection level
            var hedgeValue = portfolioState.ActiveHedges.Sum(h => h.MaxPayoff);
            metrics.ProtectionLevel = hedgeValue / Math.Max(1, metrics.TotalExposure);
            
            // Greeks aggregation
            metrics.PortfolioDelta = portfolioState.ActivePositions.Sum(p => p.Greeks?.Delta ?? 0);
            metrics.PortfolioTheta = portfolioState.ActivePositions.Sum(p => p.Greeks?.Theta ?? 0);
            metrics.PortfolioVega = portfolioState.ActivePositions.Sum(p => p.Greeks?.Vega ?? 0);
            
            // Risk limits check
            metrics.WithinRiskLimits = CheckRiskLimits(metrics);
            
            return metrics;
        }

        public async Task<bool> ValidateExecutionConstraints(SynchronizedExecutionPlan plan)
        {
            // Check capital constraints
            if (plan.EstimatedCapitalRequired > plan.CapitalAvailable)
            {
                return false;
            }

            // Check position limits
            var currentState = plan.CurrentState;
            
            if (currentState.ProbeCount + plan.ProbeEntries?.Count > _config.MaxProbePositions)
            {
                return false;
            }
            
            if (currentState.CoreCount + plan.CoreEntries?.Count > _config.MaxCorePositions)
            {
                return false;
            }
            
            if (currentState.HedgeCount + plan.HedgeAdjustments?.Count(h => h.Action == "ADD") 
                > _config.MaxHedgePositions)
            {
                return false;
            }

            // Check risk limits
            var projectedExposure = currentState.TotalExposure + 
                (plan.ProbeEntries?.Sum(p => p.Risk) ?? 0) +
                (plan.CoreEntries?.Sum(c => c.MaxRisk) ?? 0);
                
            if (projectedExposure > _config.MaxTotalExposure)
            {
                return false;
            }

            // Check Greek limits
            if (Math.Abs(plan.ProjectedGreeks?.NetDelta ?? 0) > _config.MaxDeltaExposure)
            {
                return false;
            }

            // Check drawdown protection
            if (_config.RequireDrawdownProtection)
            {
                var protectionLevel = await CalculateProtectionLevel(plan);
                if (protectionLevel < _config.MinProtectionLevel)
                {
                    return false;
                }
            }

            return true;
        }

        private bool ShouldEnterProbes(DateTime date, ProbeSentiment sentiment)
        {
            // Check if we should enter probes based on schedule and sentiment
            if (_config.ProbeSchedule.ContainsKey(date.DayOfWeek))
            {
                // Skip probes in highly volatile conditions
                if (sentiment == ProbeSentiment.Volatile && _config.SkipProbesInVolatility)
                {
                    return false;
                }
                
                return true;
            }
            
            return false;
        }

        private bool ShouldEnterCore(DateTime date, ProbeSentiment sentiment, PortfolioState state)
        {
            // Core entry conditions
            if (!_config.CoreEntryDays.Contains(date.DayOfWeek))
            {
                return false;
            }

            // Require probe confirmation
            if (_config.RequireProbeConfirmation)
            {
                if (sentiment != ProbeSentiment.Bullish)
                {
                    return false;
                }
                
                // Check recent probe performance
                var recentProbeWins = _executionHistory.GetRecentProbeWinRate(10);
                if (recentProbeWins < _config.MinProbeWinRateForCore)
                {
                    return false;
                }
            }

            // Check position limits
            if (state.CoreCount >= _config.MaxCorePositions)
            {
                return false;
            }

            return true;
        }

        private int GetProbeCountForDay(DayOfWeek day)
        {
            return _config.ProbeSchedule.GetValueOrDefault(day, 0);
        }

        private async Task<List<ExitOrder>> GenerateExitOrders(PortfolioState state, DateTime date)
        {
            var exits = new List<ExitOrder>();

            foreach (var position in state.ActivePositions)
            {
                var shouldExit = false;
                var reason = "";

                // Check DTE-based exits
                if (position.DTE <= position.ForcedExitDTE)
                {
                    shouldExit = true;
                    reason = "DTE_EXPIRY";
                }
                // Check profit targets
                else if (position.UnrealizedPnL / position.Risk >= position.ProfitTarget)
                {
                    shouldExit = true;
                    reason = "PROFIT_TARGET";
                }
                // Check stop losses
                else if (position.UnrealizedPnL < 0 && 
                        Math.Abs(position.UnrealizedPnL) / position.Risk >= position.StopLoss)
                {
                    shouldExit = true;
                    reason = "STOP_LOSS";
                }

                if (shouldExit)
                {
                    exits.Add(new ExitOrder
                    {
                        PositionId = position.PositionId,
                        ExitReason = reason,
                        ExpectedPnL = position.UnrealizedPnL,
                        Position = position
                    });
                }
            }

            return exits;
        }

        private async Task<List<ProbeEntry>> GenerateProbeEntries(
            dynamic probeScout, 
            DateTime date, 
            int count)
        {
            if (probeScout == null || count <= 0) return new List<ProbeEntry>();
            
            return await probeScout.GenerateProbeEntries(date, count);
        }

        private async Task<List<CoreEntry>> GenerateCoreEntries(
            dynamic coreEngine, 
            DateTime date, 
            ProbeSentiment sentiment)
        {
            if (coreEngine == null) return new List<CoreEntry>();
            
            var entries = new List<CoreEntry>();
            
            // Generate single core entry if conditions are met
            var probeSignal = new ProbeSignal { Sentiment = sentiment };
            var spotPrice = await _dataManager.GetUnderlyingPrice("SPX", date);
            
            var coreEntry = await coreEngine.BuildBWB(date, spotPrice, probeSignal);
            if (coreEntry != null)
            {
                entries.Add(new CoreEntry
                {
                    Symbol = coreEntry.Symbol,
                    Structure = "BWB",
                    Strikes = new[] { coreEntry.LongLowerStrike, coreEntry.ShortStrike, coreEntry.LongUpperStrike },
                    Quantities = coreEntry.Quantities,
                    Credit = coreEntry.Credit,
                    MaxRisk = coreEntry.MaxRisk,
                    Expiration = coreEntry.Expiration
                });
            }
            
            return entries;
        }

        private async Task<List<HedgeAdjustment>> GenerateHedgeAdjustments(
            HedgeRequirement requirement,
            List<Position> activeHedges,
            DateTime date)
        {
            var adjustments = new List<HedgeAdjustment>();
            
            // Get current VIX
            var currentVIX = await GetCurrentVIX(date);
            
            // Get hedge signal
            var vixHedges = activeHedges.Select(h => new VIXHedge
            {
                HedgeId = h.PositionId,
                EntryVIX = h.EntryPrice,
                DTE = h.DTE
            }).ToList();
            
            var hedgeSignal = await _hedgeManager.GetHedgeAdjustmentSignal(vixHedges, currentVIX);
            
            // Generate adjustments based on signal
            switch (hedgeSignal.Action)
            {
                case HedgeAction.Add:
                    var newHedges = await _hedgeManager.GenerateHedges(requirement, date);
                    foreach (var hedge in newHedges)
                    {
                        adjustments.Add(new HedgeAdjustment
                        {
                            Action = "ADD",
                            Symbol = "VIX",
                            Expiration = hedge.Expiration,
                            LongStrike = hedge.LongStrike,
                            ShortStrike = hedge.ShortStrike,
                            Cost = hedge.Cost,
                            Quantity = 1,
                            Reason = hedgeSignal.Reason
                        });
                    }
                    break;
                    
                case HedgeAction.PartialClose:
                    var hedgesToClose = activeHedges
                        .OrderByDescending(h => h.UnrealizedPnL)
                        .Take(hedgeSignal.Quantity);
                    
                    foreach (var hedge in hedgesToClose)
                    {
                        adjustments.Add(new HedgeAdjustment
                        {
                            Action = "PARTIAL_CLOSE",
                            Symbol = "VIX",
                            Quantity = 1,
                            Reason = hedgeSignal.Reason
                        });
                    }
                    break;
                    
                case HedgeAction.Roll:
                    // Handle rolling expiring hedges
                    foreach (var hedgeId in hedgeSignal.HedgesToRoll)
                    {
                        adjustments.Add(new HedgeAdjustment
                        {
                            Action = "ROLL",
                            Reason = hedgeSignal.Reason
                        });
                    }
                    break;
            }
            
            return adjustments;
        }

        private decimal CalculateTotalExposure(PortfolioState state, SynchronizedExecutionPlan plan)
        {
            var currentExposure = state.TotalExposure;
            var newExposure = (plan.ProbeEntries?.Sum(p => p.Risk) ?? 0) +
                             (plan.CoreEntries?.Sum(c => c.MaxRisk) ?? 0);
            var exitReduction = plan.Exits?.Sum(e => e.Position.Risk) ?? 0;
            
            return currentExposure + newExposure - exitReduction;
        }

        private async Task<decimal> GetCurrentVIX(DateTime date)
        {
            return await _dataManager.GetUnderlyingPrice("VIX", date);
        }

        private MarketConditions DetermineMarketConditions(ProbeSentiment sentiment, decimal vix)
        {
            if (sentiment == ProbeSentiment.Volatile || vix > 30)
                return MarketConditions.Crisis;
            if (vix > 25)
                return MarketConditions.Volatile;
            if (vix < 15 && sentiment == ProbeSentiment.Bullish)
                return MarketConditions.Calm;
            return MarketConditions.Normal;
        }

        private decimal CalculateCapitalRequired(SynchronizedExecutionPlan plan)
        {
            var exitCapital = plan.Exits?.Sum(e => -e.Position.Risk) ?? 0;
            var probeCapital = plan.ProbeEntries?.Sum(p => p.Risk) ?? 0;
            var coreCapital = plan.CoreEntries?.Sum(c => c.MaxRisk) ?? 0;
            var hedgeCapital = plan.HedgeAdjustments?
                .Where(h => h.Action == "ADD")
                .Sum(h => h.Cost) ?? 0;
            
            return probeCapital + coreCapital + hedgeCapital + exitCapital;
        }

        private async Task ApplyRiskLimits(SynchronizedExecutionPlan plan, PortfolioState state)
        {
            // Apply maximum exposure limits
            var totalExposure = CalculateTotalExposure(state, plan);
            if (totalExposure > _config.MaxTotalExposure)
            {
                // Scale down entries proportionally
                var scaleFactor = _config.MaxTotalExposure / totalExposure;
                ScaleDownEntries(plan, scaleFactor);
            }

            // Apply drawdown protection
            if (state.UnrealizedPnL < -_config.DrawdownLimit)
            {
                // Freeze new entries
                plan.ProbeEntries?.Clear();
                plan.CoreEntries?.Clear();
                plan.FreezeReason = "Drawdown limit reached";
            }

            // Apply intraday loss limits
            var todaysPnL = _executionHistory.GetTodaysPnL();
            if (todaysPnL < -_config.DailyLossLimit)
            {
                // Cancel all new entries
                plan.ProbeEntries?.Clear();
                plan.CoreEntries?.Clear();
                plan.FreezeReason = "Daily loss limit reached";
            }
        }

        private void PrioritizeExecutions(SynchronizedExecutionPlan plan)
        {
            // Priority order: Exits > Hedges > Probes > Core
            var availableCapital = plan.CapitalAvailable;
            
            // Always execute exits (they free up capital)
            // Exits are already in the plan
            
            // Prioritize hedges
            if (plan.HedgeAdjustments != null)
            {
                var hedgeCost = plan.HedgeAdjustments
                    .Where(h => h.Action == "ADD")
                    .Sum(h => h.Cost);
                    
                if (hedgeCost > availableCapital)
                {
                    // Reduce hedge count
                    var affordableHedges = new List<HedgeAdjustment>();
                    var runningCost = 0m;
                    
                    foreach (var hedge in plan.HedgeAdjustments.Where(h => h.Action == "ADD"))
                    {
                        if (runningCost + hedge.Cost <= availableCapital)
                        {
                            affordableHedges.Add(hedge);
                            runningCost += hedge.Cost;
                        }
                    }
                    
                    plan.HedgeAdjustments = affordableHedges;
                }
                
                availableCapital -= hedgeCost;
            }
            
            // Then probes
            if (plan.ProbeEntries != null && availableCapital > 0)
            {
                var probeCost = plan.ProbeEntries.Sum(p => p.Risk);
                if (probeCost > availableCapital)
                {
                    // Reduce probe count
                    var affordableProbes = new List<ProbeEntry>();
                    var runningCost = 0m;
                    
                    foreach (var probe in plan.ProbeEntries.OrderBy(p => p.Risk))
                    {
                        if (runningCost + probe.Risk <= availableCapital)
                        {
                            affordableProbes.Add(probe);
                            runningCost += probe.Risk;
                        }
                    }
                    
                    plan.ProbeEntries = affordableProbes;
                    availableCapital -= runningCost;
                }
                else
                {
                    availableCapital -= probeCost;
                }
            }
            
            // Finally core entries
            if (plan.CoreEntries != null && availableCapital > 0)
            {
                var coreCost = plan.CoreEntries.Sum(c => c.MaxRisk);
                if (coreCost > availableCapital)
                {
                    // Skip core entries if insufficient capital
                    plan.CoreEntries.Clear();
                    plan.SkippedCoreReason = "Insufficient capital after higher priority executions";
                }
            }
        }

        private async Task<PortfolioGreeks> CalculateProjectedGreeks(
            SynchronizedExecutionPlan plan, 
            PortfolioState state)
        {
            var greeks = new PortfolioGreeks();
            
            // Start with current Greeks
            foreach (var position in state.ActivePositions)
            {
                if (position.Greeks != null)
                {
                    greeks.NetDelta += position.Greeks.Delta;
                    greeks.NetGamma += position.Greeks.Gamma;
                    greeks.NetTheta += position.Greeks.Theta;
                    greeks.NetVega += position.Greeks.Vega;
                }
            }
            
            // Subtract Greeks from positions to be closed
            if (plan.Exits != null)
            {
                foreach (var exit in plan.Exits)
                {
                    if (exit.Position.Greeks != null)
                    {
                        greeks.NetDelta -= exit.Position.Greeks.Delta;
                        greeks.NetGamma -= exit.Position.Greeks.Gamma;
                        greeks.NetTheta -= exit.Position.Greeks.Theta;
                        greeks.NetVega -= exit.Position.Greeks.Vega;
                    }
                }
            }
            
            // Add Greeks from new positions (estimated)
            // This would need actual Greek calculations based on the specific options
            
            return greeks;
        }

        private void ScaleDownEntries(SynchronizedExecutionPlan plan, decimal scaleFactor)
        {
            // Scale probe entries
            if (plan.ProbeEntries != null)
            {
                var targetProbeCount = (int)(plan.ProbeEntries.Count * scaleFactor);
                plan.ProbeEntries = plan.ProbeEntries.Take(targetProbeCount).ToList();
            }
            
            // Scale core entries
            if (plan.CoreEntries != null)
            {
                var targetCoreCount = (int)(plan.CoreEntries.Count * scaleFactor);
                plan.CoreEntries = plan.CoreEntries.Take(targetCoreCount).ToList();
            }
        }

        private int CountPlannedExecutions(SynchronizedExecutionPlan plan)
        {
            return (plan.Exits?.Count ?? 0) +
                   (plan.ProbeEntries?.Count ?? 0) +
                   (plan.CoreEntries?.Count ?? 0) +
                   (plan.HedgeAdjustments?.Count ?? 0);
        }

        private async Task<List<ExecutionDetail>> ExecuteExits(List<ExitOrder> exits)
        {
            var details = new List<ExecutionDetail>();
            
            foreach (var exit in exits)
            {
                try
                {
                    // Execute the exit through fill engine
                    var fillResult = await _fillEngine.GetRealisticFill(
                        new Order
                        {
                            OrderId = Guid.NewGuid().ToString(),
                            Symbol = exit.Position.Symbol,
                            Side = exit.Position.Side == "BUY" ? "SELL" : "BUY",
                            Quantity = exit.Position.Quantity,
                            OrderType = "MARKET"
                        },
                        new Quote { Bid = exit.Position.CurrentBid, Ask = exit.Position.CurrentAsk },
                        MarketState.Normal);
                    
                    details.Add(new ExecutionDetail
                    {
                        ExecutionId = Guid.NewGuid().ToString(),
                        PositionId = exit.PositionId,
                        ExecutionType = "EXIT",
                        Success = fillResult.Filled,
                        FillPrice = fillResult.FillPrice,
                        Slippage = fillResult.Slippage,
                        CapitalImpact = -exit.Position.Risk + exit.ExpectedPnL,
                        Timestamp = DateTime.Now
                    });
                    
                    // Remove from active positions
                    if (fillResult.Filled)
                    {
                        _activePositions.Remove(exit.PositionId);
                    }
                }
                catch (Exception ex)
                {
                    details.Add(new ExecutionDetail
                    {
                        ExecutionId = Guid.NewGuid().ToString(),
                        PositionId = exit.PositionId,
                        ExecutionType = "EXIT",
                        Success = false,
                        Error = ex.Message,
                        Timestamp = DateTime.Now
                    });
                }
            }
            
            return details;
        }

        private async Task<List<ExecutionDetail>> ExecuteProbeEntries(List<ProbeEntry> probes)
        {
            var details = new List<ExecutionDetail>();
            
            foreach (var probe in probes)
            {
                try
                {
                    // Execute probe entry
                    var position = new Position
                    {
                        PositionId = Guid.NewGuid().ToString(),
                        Symbol = probe.Symbol,
                        PositionType = PositionType.Probe,
                        EntryDate = DateTime.Now,
                        Expiration = probe.Expiration,
                        Risk = probe.Risk,
                        Credit = probe.Credit,
                        Quantity = probe.Quantity,
                        Side = "SELL", // Selling spreads
                        ProfitTarget = 0.65m,
                        StopLoss = 2.0m,
                        ForcedExitDTE = 5
                    };
                    
                    _activePositions[position.PositionId] = position;
                    
                    details.Add(new ExecutionDetail
                    {
                        ExecutionId = Guid.NewGuid().ToString(),
                        PositionId = position.PositionId,
                        ExecutionType = "PROBE_ENTRY",
                        Success = true,
                        FillPrice = probe.Credit,
                        CapitalImpact = probe.Risk,
                        Timestamp = DateTime.Now
                    });
                }
                catch (Exception ex)
                {
                    details.Add(new ExecutionDetail
                    {
                        ExecutionId = Guid.NewGuid().ToString(),
                        ExecutionType = "PROBE_ENTRY",
                        Success = false,
                        Error = ex.Message,
                        Timestamp = DateTime.Now
                    });
                }
            }
            
            return details;
        }

        private async Task<List<ExecutionDetail>> ExecuteCoreEntries(List<CoreEntry> cores)
        {
            var details = new List<ExecutionDetail>();
            
            foreach (var core in cores)
            {
                try
                {
                    var position = new Position
                    {
                        PositionId = Guid.NewGuid().ToString(),
                        Symbol = core.Symbol,
                        PositionType = PositionType.Core,
                        EntryDate = DateTime.Now,
                        Expiration = core.Expiration,
                        Risk = core.MaxRisk,
                        Credit = core.Credit,
                        Quantity = 1,
                        Side = "COMPLEX", // BWB structure
                        ProfitTarget = 0.65m,
                        StopLoss = 2.0m,
                        ForcedExitDTE = 10
                    };
                    
                    _activePositions[position.PositionId] = position;
                    
                    details.Add(new ExecutionDetail
                    {
                        ExecutionId = Guid.NewGuid().ToString(),
                        PositionId = position.PositionId,
                        ExecutionType = "CORE_ENTRY",
                        Success = true,
                        FillPrice = core.Credit,
                        CapitalImpact = core.MaxRisk,
                        Timestamp = DateTime.Now
                    });
                }
                catch (Exception ex)
                {
                    details.Add(new ExecutionDetail
                    {
                        ExecutionId = Guid.NewGuid().ToString(),
                        ExecutionType = "CORE_ENTRY",
                        Success = false,
                        Error = ex.Message,
                        Timestamp = DateTime.Now
                    });
                }
            }
            
            return details;
        }

        private async Task<List<ExecutionDetail>> ExecuteHedgeAdjustments(List<HedgeAdjustment> adjustments)
        {
            var details = new List<ExecutionDetail>();
            
            foreach (var adjustment in adjustments)
            {
                try
                {
                    if (adjustment.Action == "ADD")
                    {
                        var position = new Position
                        {
                            PositionId = Guid.NewGuid().ToString(),
                            Symbol = adjustment.Symbol,
                            PositionType = PositionType.Hedge,
                            EntryDate = DateTime.Now,
                            Expiration = adjustment.Expiration,
                            Risk = 0, // Hedges are protective
                            Cost = adjustment.Cost,
                            MaxPayoff = (adjustment.ShortStrike - adjustment.LongStrike) * 100,
                            Quantity = adjustment.Quantity,
                            Side = "BUY" // Buying protection
                        };
                        
                        _activePositions[position.PositionId] = position;
                        
                        details.Add(new ExecutionDetail
                        {
                            ExecutionId = Guid.NewGuid().ToString(),
                            PositionId = position.PositionId,
                            ExecutionType = "HEDGE_ADD",
                            Success = true,
                            FillPrice = adjustment.Cost,
                            CapitalImpact = adjustment.Cost,
                            Timestamp = DateTime.Now
                        });
                    }
                }
                catch (Exception ex)
                {
                    details.Add(new ExecutionDetail
                    {
                        ExecutionId = Guid.NewGuid().ToString(),
                        ExecutionType = $"HEDGE_{adjustment.Action}",
                        Success = false,
                        Error = ex.Message,
                        Timestamp = DateTime.Now
                    });
                }
            }
            
            return details;
        }

        private async Task UpdateCapitalUsage()
        {
            _currentCapitalUsed = _activePositions.Values.Sum(p => 
                p.PositionType == PositionType.Hedge ? p.Cost : p.Risk);
        }

        private decimal CalculateVaR(PortfolioState state, decimal confidenceLevel)
        {
            // Simplified VaR calculation
            // In production, this would use historical data and proper statistical methods
            var portfolioStdDev = state.TotalExposure * 0.02m; // Assume 2% daily volatility
            var zScore = GetZScore(confidenceLevel);
            return portfolioStdDev * zScore;
        }

        private decimal GetZScore(decimal confidenceLevel)
        {
            return confidenceLevel switch
            {
                0.95m => 1.645m,
                0.99m => 2.326m,
                _ => 1.645m
            };
        }

        private async Task<decimal> CalculateDrawdown(PortfolioState state, decimal marketMove)
        {
            // Calculate expected portfolio loss at given market move
            var deltaExposure = state.ActivePositions.Sum(p => (p.Greeks?.Delta ?? 0) * p.Risk);
            var gammaExposure = state.ActivePositions.Sum(p => (p.Greeks?.Gamma ?? 0) * p.Risk);
            
            // First-order approximation
            var deltaLoss = deltaExposure * marketMove;
            var gammaLoss = 0.5m * gammaExposure * marketMove * marketMove;
            
            // Account for hedge protection
            var hedgeProtection = 0m;
            if (marketMove < -0.05m) // Hedges kick in on significant moves
            {
                hedgeProtection = state.ActiveHedges.Sum(h => h.MaxPayoff) * Math.Min(1, Math.Abs(marketMove) / 0.10m);
            }
            
            return deltaLoss + gammaLoss - hedgeProtection;
        }

        private bool CheckRiskLimits(RiskMetrics metrics)
        {
            if (metrics.CapitalUtilization > 0.95m) return false;
            if (Math.Abs(metrics.PortfolioDelta) > _config.MaxDeltaExposure) return false;
            if (metrics.DrawdownAt5PercentMove < -_config.MaxDrawdownAt5Percent) return false;
            return true;
        }

        private async Task<decimal> CalculateProtectionLevel(SynchronizedExecutionPlan plan)
        {
            var totalExposure = CalculateTotalExposure(plan.CurrentState, plan);
            var hedgeProtection = plan.CurrentState.ActiveHedges.Sum(h => h.MaxPayoff) +
                                 plan.HedgeAdjustments?
                                     .Where(h => h.Action == "ADD")
                                     .Sum(h => (h.ShortStrike - h.LongStrike) * 100) ?? 0;
            
            return hedgeProtection / Math.Max(1, totalExposure);
        }
    }

    // Supporting classes for Synchronized Executor
    public class SynchronizedExecutionPlan
    {
        public DateTime ExecutionDate { get; set; }
        public DayOfWeek DayOfWeek { get; set; }
        public PortfolioState CurrentState { get; set; }
        public ProbeSentiment ProbeSentiment { get; set; }
        public List<ExitOrder> Exits { get; set; }
        public List<ProbeEntry> ProbeEntries { get; set; }
        public List<CoreEntry> CoreEntries { get; set; }
        public List<HedgeAdjustment> HedgeAdjustments { get; set; }
        public decimal EstimatedCapitalRequired { get; set; }
        public decimal CapitalAvailable { get; set; }
        public PortfolioGreeks ProjectedGreeks { get; set; }
        public string FreezeReason { get; set; }
        public string SkippedCoreReason { get; set; }
    }

    public class StrategyComponents
    {
        public dynamic ProbeScout { get; set; }
        public dynamic CoreEngine { get; set; }
        public IVIXHedgeManager HedgeManager { get; set; }
    }

    public class SynchronizationConfig
    {
        public decimal TotalCapital { get; set; } = 100000m;
        public int MaxProbePositions { get; set; } = 20;
        public int MaxCorePositions { get; set; } = 4;
        public int MaxHedgePositions { get; set; } = 4;
        public decimal MaxTotalExposure { get; set; } = 25000m;
        public decimal MaxDeltaExposure { get; set; } = 0.15m;
        public decimal DrawdownLimit { get; set; } = 5000m;
        public decimal DailyLossLimit { get; set; } = 2000m;
        public decimal MaxDrawdownAt5Percent { get; set; } = 5000m;
        public bool RequireProbeConfirmation { get; set; } = true;
        public decimal MinProbeWinRateForCore { get; set; } = 0.60m;
        public bool SkipProbesInVolatility { get; set; } = true;
        public bool RequireDrawdownProtection { get; set; } = true;
        public decimal MinProtectionLevel { get; set; } = 0.50m;
        
        public Dictionary<DayOfWeek, int> ProbeSchedule { get; set; } = new()
        {
            { DayOfWeek.Monday, 2 },
            { DayOfWeek.Tuesday, 2 },
            { DayOfWeek.Wednesday, 1 },
            { DayOfWeek.Thursday, 0 },
            { DayOfWeek.Friday, 0 }
        };
        
        public List<DayOfWeek> CoreEntryDays { get; set; } = new()
        {
            DayOfWeek.Wednesday
        };
    }

    public class ExecutionResult
    {
        public DateTime ExecutionDate { get; set; }
        public bool Success { get; set; }
        public string FailureReason { get; set; }
        public int PlannedExecutions { get; set; }
        public int TotalExecuted { get; set; }
        public int TotalFailed { get; set; }
        public int ExitsExecuted { get; set; }
        public int ProbesExecuted { get; set; }
        public int CoreExecuted { get; set; }
        public int HedgesExecuted { get; set; }
        public decimal NetCapitalChange { get; set; }
        public List<ExecutionDetail> ExecutionDetails { get; set; }
    }

    public class ExecutionDetail
    {
        public string ExecutionId { get; set; }
        public string PositionId { get; set; }
        public string ExecutionType { get; set; }
        public bool Success { get; set; }
        public decimal FillPrice { get; set; }
        public decimal Slippage { get; set; }
        public decimal CapitalImpact { get; set; }
        public string Error { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class Position
    {
        public string PositionId { get; set; }
        public string Symbol { get; set; }
        public PositionType PositionType { get; set; }
        public DateTime EntryDate { get; set; }
        public DateTime Expiration { get; set; }
        public decimal EntryPrice { get; set; }
        public decimal CurrentBid { get; set; }
        public decimal CurrentAsk { get; set; }
        public decimal Risk { get; set; }
        public decimal Credit { get; set; }
        public decimal Cost { get; set; }
        public decimal MaxPayoff { get; set; }
        public decimal UnrealizedPnL { get; set; }
        public int Quantity { get; set; }
        public string Side { get; set; }
        public int DTE { get; set; }
        public int ForcedExitDTE { get; set; }
        public decimal ProfitTarget { get; set; }
        public decimal StopLoss { get; set; }
        public Greeks Greeks { get; set; }
    }

    public class Greeks
    {
        public decimal Delta { get; set; }
        public decimal Gamma { get; set; }
        public decimal Theta { get; set; }
        public decimal Vega { get; set; }
        public decimal Rho { get; set; }
    }

    public class ExitOrder
    {
        public string PositionId { get; set; }
        public string ExitReason { get; set; }
        public decimal ExpectedPnL { get; set; }
        public Position Position { get; set; }
    }

    public class CoreEntry
    {
        public string Symbol { get; set; }
        public string Structure { get; set; }
        public decimal[] Strikes { get; set; }
        public int[] Quantities { get; set; }
        public decimal Credit { get; set; }
        public decimal MaxRisk { get; set; }
        public DateTime Expiration { get; set; }
    }

    public class ProbeEntry
    {
        public string Symbol { get; set; }
        public DateTime Expiration { get; set; }
        public decimal ShortStrike { get; set; }
        public decimal LongStrike { get; set; }
        public decimal Credit { get; set; }
        public decimal Risk { get; set; }
        public int Quantity { get; set; }
    }

    public class HedgeAdjustment
    {
        public string Action { get; set; }
        public string Symbol { get; set; }
        public DateTime Expiration { get; set; }
        public decimal LongStrike { get; set; }
        public decimal ShortStrike { get; set; }
        public decimal Cost { get; set; }
        public int Quantity { get; set; }
        public string Reason { get; set; }
    }

    public class PortfolioState
    {
        public DateTime Timestamp { get; set; }
        public List<Position> ActivePositions { get; set; }
        public List<Position> ProbePositions { get; set; }
        public List<Position> CorePositions { get; set; }
        public List<Position> ActiveHedges { get; set; }
        public int ProbeCount { get; set; }
        public int CoreCount { get; set; }
        public int HedgeCount { get; set; }
        public decimal TotalExposure { get; set; }
        public decimal ProbeExposure { get; set; }
        public decimal CoreExposure { get; set; }
        public decimal HedgeCost { get; set; }
        public decimal UnrealizedPnL { get; set; }
        public decimal RealizedPnL { get; set; }
    }

    public class RiskMetrics
    {
        public decimal TotalExposure { get; set; }
        public decimal NetExposure { get; set; }
        public decimal CapitalUtilization { get; set; }
        public decimal VaR95 { get; set; }
        public decimal VaR99 { get; set; }
        public decimal DrawdownAt5PercentMove { get; set; }
        public decimal DrawdownAt7PercentMove { get; set; }
        public decimal DrawdownAt10PercentMove { get; set; }
        public decimal ProtectionLevel { get; set; }
        public decimal PortfolioDelta { get; set; }
        public decimal PortfolioTheta { get; set; }
        public decimal PortfolioVega { get; set; }
        public bool WithinRiskLimits { get; set; }
    }

    public class PortfolioGreeks
    {
        public decimal NetDelta { get; set; }
        public decimal NetGamma { get; set; }
        public decimal NetTheta { get; set; }
        public decimal NetVega { get; set; }
        public decimal NetRho { get; set; }
        public decimal DeltaAdjustedExposure { get; set; }
        public decimal GammaRisk { get; set; }
        public decimal DailyThetaDecay { get; set; }
        public decimal VegaExposure { get; set; }
        public Dictionary<string, decimal> ComponentGreeks { get; set; } = new();
    }

    public class ExecutionHistory
    {
        private readonly List<ExecutionResult> _history = new();
        public decimal TotalRealizedPnL { get; private set; }

        public void RecordExecution(SynchronizedExecutionPlan plan, ExecutionResult result)
        {
            _history.Add(result);
        }

        public decimal GetRecentProbeWinRate(int days)
        {
            // Implementation would track probe performance
            return 0.65m; // Placeholder
        }

        public decimal GetTodaysPnL()
        {
            // Implementation would calculate today's P&L
            return 0m; // Placeholder
        }
    }

    public class ProbeSignal
    {
        public ProbeSentiment Sentiment { get; set; }
    }

    public enum ProbeSentiment
    {
        Bullish,
        Neutral,
        Bearish,
        Volatile,
        Insufficient
    }

    public enum PositionType
    {
        Probe,
        Core,
        Hedge
    }
}