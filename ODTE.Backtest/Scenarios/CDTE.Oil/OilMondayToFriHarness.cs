using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ODTE.Strategy.CDTE.Oil;
using ODTE.Strategy.CDTE.Oil.Risk;
using ODTE.Historical.Providers;
using ODTE.Execution.HistoricalFill;

namespace ODTE.Backtest.Scenarios.CDTE.Oil
{
    public sealed class OilMondayToFriHarness
    {
        private readonly OilCDTEStrategy _strategy;
        private readonly ChainSnapshotProvider _chainProvider;
        private readonly SessionCalendarProvider _calendarProvider;
        private readonly NbboFillEngine _fillEngine;
        private readonly ILogger<OilMondayToFriHarness> _logger;
        private readonly OilCDTEConfig _config;

        public OilMondayToFriHarness(
            OilCDTEStrategy strategy,
            ChainSnapshotProvider chainProvider,
            SessionCalendarProvider calendarProvider,
            NbboFillEngine fillEngine,
            OilCDTEConfig config,
            ILogger<OilMondayToFriHarness> logger)
        {
            _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
            _chainProvider = chainProvider ?? throw new ArgumentNullException(nameof(chainProvider));
            _calendarProvider = calendarProvider ?? throw new ArgumentNullException(nameof(calendarProvider));
            _fillEngine = fillEngine ?? throw new ArgumentNullException(nameof(fillEngine));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<WeeklyBacktestResult> RunWeeklyBacktest(
            string underlying,
            DateTime weekStart,
            BacktestParameters parameters)
        {
            try
            {
                _logger.LogInformation("Starting weekly backtest for {Underlying} week of {WeekStart}", 
                    underlying, weekStart.ToShortDateString());

                var tradingWeek = _calendarProvider.GetTradingWeek(underlying, weekStart);
                var calendar = _calendarProvider.GetCalendar(underlying);

                if (tradingWeek.TradingDays.Length < 3)
                {
                    _logger.LogWarning("Insufficient trading days ({Count}) for week of {WeekStart}, skipping",
                        tradingWeek.TradingDays.Length, weekStart);
                    
                    return new WeeklyBacktestResult
                    {
                        WeekStart = weekStart,
                        Underlying = underlying,
                        Success = false,
                        SkipReason = $"Only {tradingWeek.TradingDays.Length} trading days available",
                        TradingDays = tradingWeek.TradingDays
                    };
                }

                var mondayDecision = GetDecisionTime(tradingWeek.TradingDays[0], _config.MondayDecisionEt);
                var wednesdayDecision = GetDecisionTime(tradingWeek.TradingDays[2], _config.WednesdayDecisionEt);
                var fridayExit = GetExitTime(tradingWeek.TradingDays.Last(), calendar);

                // Get market snapshots for the week
                var snapshots = await _chainProvider.GetSnapshotsForWeek(
                    underlying, mondayDecision, wednesdayDecision, fridayExit, calendar);

                var weeklyResult = new WeeklyBacktestResult
                {
                    WeekStart = weekStart,
                    Underlying = underlying,
                    Success = true,
                    TradingDays = tradingWeek.TradingDays,
                    MondayDecision = mondayDecision,
                    WednesdayDecision = wednesdayDecision,
                    FridayExit = fridayExit,
                    Snapshots = snapshots
                };

                // Phase 1: Monday Entry
                var mondayResult = await ExecuteMondayEntry(snapshots[0], weeklyResult);
                if (!mondayResult.Success)
                {
                    weeklyResult.Success = false;
                    weeklyResult.SkipReason = mondayResult.FailureReason;
                    return weeklyResult;
                }

                weeklyResult.MondayResult = mondayResult;
                weeklyResult.Positions = mondayResult.CreatedPositions.ToList();

                // Phase 2: Wednesday Management
                if (snapshots.Length > 1)
                {
                    var portfolioState = CreatePortfolioState(weeklyResult.Positions, snapshots[1]);
                    var wednesdayResult = await ExecuteWednesdayManagement(snapshots[1], portfolioState, weeklyResult);
                    
                    weeklyResult.WednesdayResult = wednesdayResult;
                    ApplyWednesdayActions(weeklyResult, wednesdayResult);
                }

                // Phase 3: Friday Exit
                if (snapshots.Length > 2)
                {
                    var portfolioState = CreatePortfolioState(weeklyResult.Positions, snapshots[2]);
                    var fridayResult = await ExecuteFridayExit(snapshots[2], portfolioState, weeklyResult);
                    
                    weeklyResult.FridayResult = fridayResult;
                    weeklyResult.FinalPnL = fridayResult.FinalPnL;
                }

                // Calculate performance metrics
                weeklyResult.PerformanceMetrics = CalculatePerformanceMetrics(weeklyResult);

                _logger.LogInformation("Completed weekly backtest for {Underlying}: Final P&L ${FinalPnL:F2}",
                    underlying, weeklyResult.FinalPnL);

                return weeklyResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in weekly backtest for {Underlying} week of {WeekStart}",
                    underlying, weekStart);
                
                return new WeeklyBacktestResult
                {
                    WeekStart = weekStart,
                    Underlying = underlying,
                    Success = false,
                    SkipReason = $"Exception: {ex.Message}"
                };
            }
        }

        public async Task<BatchBacktestResult> RunBatchBacktest(
            string underlying,
            DateTime startDate,
            DateTime endDate,
            BacktestParameters parameters)
        {
            var results = new List<WeeklyBacktestResult>();
            var currentWeek = GetMondayOfWeek(startDate);
            var endWeek = GetMondayOfWeek(endDate);

            _logger.LogInformation("Starting batch backtest for {Underlying} from {StartDate} to {EndDate}",
                underlying, startDate.ToShortDateString(), endDate.ToShortDateString());

            while (currentWeek <= endWeek)
            {
                var weeklyResult = await RunWeeklyBacktest(underlying, currentWeek, parameters);
                results.Add(weeklyResult);

                if (weeklyResult.Success)
                {
                    _logger.LogDebug("Week {WeekStart}: P&L ${PnL:F2}",
                        currentWeek.ToShortDateString(), weeklyResult.FinalPnL);
                }

                currentWeek = currentWeek.AddDays(7);

                // Progress reporting every 10 weeks
                if (results.Count % 10 == 0)
                {
                    var successRate = results.Count(r => r.Success) / (double)results.Count * 100;
                    var avgPnL = results.Where(r => r.Success).Average(r => r.FinalPnL);
                    
                    _logger.LogInformation("Progress: {WeekCount} weeks, {SuccessRate:F1}% success, avg P&L ${AvgPnL:F2}",
                        results.Count, successRate, avgPnL);
                }
            }

            var batchResult = new BatchBacktestResult
            {
                Underlying = underlying,
                StartDate = startDate,
                EndDate = endDate,
                Parameters = parameters,
                WeeklyResults = results.ToArray(),
                OverallMetrics = CalculateOverallMetrics(results),
                ExecutionTime = DateTime.UtcNow
            };

            _logger.LogInformation("Batch backtest completed: {TotalWeeks} weeks, {SuccessfulWeeks} successful",
                results.Count, results.Count(r => r.Success));

            return batchResult;
        }

        private async Task<MondayEntryResult> ExecuteMondayEntry(ChainSnapshot snapshot, WeeklyBacktestResult weekContext)
        {
            try
            {
                var plannedOrders = await _strategy.EnterMondayAsync(snapshot, _config);
                var createdPositions = new List<BacktestPosition>();
                var fillResults = new List<FillResult>();

                foreach (var plan in plannedOrders.Plans)
                {
                    var fillResult = await SimulateFill(plan, snapshot);
                    fillResults.Add(fillResult);

                    if (fillResult.Success)
                    {
                        var position = CreateBacktestPosition(plan, fillResult, snapshot.Timestamp);
                        createdPositions.Add(position);
                    }
                }

                var allFilled = fillResults.All(f => f.Success);
                
                return new MondayEntryResult
                {
                    Success = allFilled,
                    FailureReason = allFilled ? null : "One or more orders failed to fill",
                    PlannedOrders = plannedOrders.Plans,
                    FillResults = fillResults.ToArray(),
                    CreatedPositions = createdPositions.ToArray(),
                    TotalRiskCapital = createdPositions.Sum(p => p.MaxLoss),
                    EntrySnapshot = snapshot
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Monday entry execution");
                
                return new MondayEntryResult
                {
                    Success = false,
                    FailureReason = $"Monday entry exception: {ex.Message}",
                    EntrySnapshot = snapshot
                };
            }
        }

        private async Task<WednesdayManagementResult> ExecuteWednesdayManagement(
            ChainSnapshot snapshot, 
            PortfolioState portfolioState, 
            WeeklyBacktestResult weekContext)
        {
            try
            {
                var decisionPlan = await _strategy.ManageWednesdayAsync(portfolioState, snapshot, _config);
                var managementActions = new List<ManagementAction>();
                var newPositions = new List<BacktestPosition>();

                if (decisionPlan.Action != GuardAction.None)
                {
                    var action = await ExecuteManagementAction(decisionPlan, snapshot, portfolioState);
                    managementActions.Add(action);

                    if (action.ResultingPositions != null)
                    {
                        newPositions.AddRange(action.ResultingPositions);
                    }
                }

                return new WednesdayManagementResult
                {
                    Success = true,
                    DecisionPlan = decisionPlan,
                    ManagementActions = managementActions.ToArray(),
                    NewPositions = newPositions.ToArray(),
                    ManagementSnapshot = snapshot,
                    PortfolioStateAfter = CreatePortfolioState(newPositions, snapshot)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Wednesday management execution");
                
                return new WednesdayManagementResult
                {
                    Success = false,
                    FailureReason = $"Wednesday management exception: {ex.Message}",
                    ManagementSnapshot = snapshot
                };
            }
        }

        private async Task<FridayExitResult> ExecuteFridayExit(
            ChainSnapshot snapshot, 
            PortfolioState portfolioState, 
            WeeklyBacktestResult weekContext)
        {
            try
            {
                var exitReport = await _strategy.ExitWeekAsync(portfolioState, snapshot, _config);
                var exitActions = new List<ExitAction>();
                var finalPnL = 0.0;

                foreach (var position in weekContext.Positions)
                {
                    var exitAction = await ExecutePositionExit(position, snapshot);
                    exitActions.Add(exitAction);
                    finalPnL += exitAction.RealizedPnL;
                }

                return new FridayExitResult
                {
                    Success = true,
                    ExitReport = exitReport,
                    ExitActions = exitActions.ToArray(),
                    FinalPnL = finalPnL,
                    ExitSnapshot = snapshot
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Friday exit execution");
                
                return new FridayExitResult
                {
                    Success = false,
                    FailureReason = $"Friday exit exception: {ex.Message}",
                    ExitSnapshot = snapshot
                };
            }
        }

        private async Task<FillResult> SimulateFill(PositionPlan plan, ChainSnapshot snapshot)
        {
            // Create synthetic NBBO data for the fill simulation
            var nbboProvider = new MockNbboProvider(snapshot);
            
            var order = new MarketableOrder(
                symbol: plan.Structure.ToString(),
                side: OrderSide.Sell, // Selling credit spreads
                quantity: 1,
                type: OrderType.MarketableLimit
            );

            return await _fillEngine.AttemptFill(order, nbboProvider, snapshot.Timestamp);
        }

        private BacktestPosition CreateBacktestPosition(PositionPlan plan, FillResult fillResult, DateTime timestamp)
        {
            return new BacktestPosition
            {
                Name = plan.Name,
                Structure = plan.Structure,
                Expiry = plan.Expiry,
                EntryTime = timestamp,
                EntryPrice = fillResult.FillPrice ?? 0,
                MaxLoss = plan.MaxLoss,
                Legs = plan.Structure.Legs,
                Status = PositionStatus.Open,
                CurrentPnL = 0
            };
        }

        private PortfolioState CreatePortfolioState(List<BacktestPosition> positions, ChainSnapshot snapshot)
        {
            var portfolioPositions = positions.Select(p => new Position
            {
                Name = p.Name,
                Legs = p.Legs,
                TicketRisk = p.MaxLoss
            }).ToArray();

            return new PortfolioState
            {
                Greeks = new GreeksAggregate
                {
                    Gamma = positions.Sum(p => CalculatePositionGamma(p, snapshot)),
                    Delta = positions.Sum(p => CalculatePositionDelta(p, snapshot))
                }
            };
        }

        private double CalculatePositionGamma(BacktestPosition position, ChainSnapshot snapshot)
        {
            // Simplified gamma calculation
            return position.Legs.Sum(leg => 0.01 * leg.Quantity); // Placeholder
        }

        private double CalculatePositionDelta(BacktestPosition position, ChainSnapshot snapshot)
        {
            // Simplified delta calculation
            return position.Legs.Sum(leg => 0.05 * leg.Quantity); // Placeholder
        }

        private async Task<ManagementAction> ExecuteManagementAction(
            DecisionPlan decisionPlan, 
            ChainSnapshot snapshot, 
            PortfolioState portfolioState)
        {
            return new ManagementAction
            {
                Action = decisionPlan.Action,
                Reason = decisionPlan.Reason,
                Timestamp = snapshot.Timestamp,
                Success = true,
                ResultingPositions = Array.Empty<BacktestPosition>()
            };
        }

        private async Task<ExitAction> ExecutePositionExit(BacktestPosition position, ChainSnapshot snapshot)
        {
            var exitPrice = EstimateExitPrice(position, snapshot);
            var realizedPnL = CalculateRealizedPnL(position, exitPrice);

            return new ExitAction
            {
                Position = position,
                ExitTime = snapshot.Timestamp,
                ExitPrice = exitPrice,
                RealizedPnL = realizedPnL,
                Success = true
            };
        }

        private double EstimateExitPrice(BacktestPosition position, ChainSnapshot snapshot)
        {
            // Simplified exit price estimation
            return position.EntryPrice * 0.5; // Assume 50% profit capture
        }

        private double CalculateRealizedPnL(BacktestPosition position, double exitPrice)
        {
            return (exitPrice - position.EntryPrice) * 100; // Per contract
        }

        private void ApplyWednesdayActions(WeeklyBacktestResult weeklyResult, WednesdayManagementResult wednesdayResult)
        {
            // Apply management actions to the weekly result
            if (wednesdayResult.NewPositions?.Any() == true)
            {
                weeklyResult.Positions.AddRange(wednesdayResult.NewPositions);
            }
        }

        private PerformanceMetrics CalculatePerformanceMetrics(WeeklyBacktestResult result)
        {
            return new PerformanceMetrics
            {
                WeeklyReturn = result.FinalPnL,
                MaxDrawdown = Math.Min(0, result.FinalPnL),
                WinRate = result.FinalPnL > 0 ? 1.0 : 0.0,
                SharpeRatio = 0.0, // Would need more data
                MaxRisk = result.Positions.Sum(p => p.MaxLoss)
            };
        }

        private OverallMetrics CalculateOverallMetrics(List<WeeklyBacktestResult> results)
        {
            var successfulResults = results.Where(r => r.Success).ToArray();
            
            if (!successfulResults.Any())
            {
                return new OverallMetrics();
            }

            var totalPnL = successfulResults.Sum(r => r.FinalPnL);
            var winCount = successfulResults.Count(r => r.FinalPnL > 0);
            var winRate = (double)winCount / successfulResults.Length;

            return new OverallMetrics
            {
                TotalWeeks = results.Count,
                SuccessfulWeeks = successfulResults.Length,
                TotalPnL = totalPnL,
                AverageWeeklyPnL = totalPnL / successfulResults.Length,
                WinRate = winRate,
                LossRate = 1.0 - winRate,
                MaxWeeklyGain = successfulResults.Max(r => r.FinalPnL),
                MaxWeeklyLoss = successfulResults.Min(r => r.FinalPnL),
                SharpeRatio = CalculateSharpeRatio(successfulResults.Select(r => r.FinalPnL))
            };
        }

        private double CalculateSharpeRatio(IEnumerable<double> returns)
        {
            var returnsArray = returns.ToArray();
            if (returnsArray.Length < 2) return 0;

            var mean = returnsArray.Average();
            var variance = returnsArray.Sum(x => Math.Pow(x - mean, 2)) / returnsArray.Length;
            var stdDev = Math.Sqrt(variance);

            return stdDev > 0 ? mean / stdDev : 0;
        }

        private DateTime GetDecisionTime(DateTime tradingDay, TimeOnly decisionTime)
        {
            return tradingDay.Date.Add(decisionTime.ToTimeSpan());
        }

        private DateTime GetExitTime(DateTime tradingDay, ProductCalendar calendar)
        {
            var sessionClose = _calendarProvider.GetSessionClose(calendar.Product, tradingDay);
            return sessionClose.AddMinutes(-_config.Risk.ExitBufferMin);
        }

        private DateTime GetMondayOfWeek(DateTime date)
        {
            var daysFromMonday = (int)date.DayOfWeek - (int)DayOfWeek.Monday;
            if (daysFromMonday < 0) daysFromMonday += 7;
            return date.Date.AddDays(-daysFromMonday);
        }
    }

    // Result classes and supporting types would go here...
    public class WeeklyBacktestResult
    {
        public DateTime WeekStart { get; set; }
        public string Underlying { get; set; } = "";
        public bool Success { get; set; }
        public string? SkipReason { get; set; }
        public DateTime[] TradingDays { get; set; } = Array.Empty<DateTime>();
        public DateTime MondayDecision { get; set; }
        public DateTime WednesdayDecision { get; set; }
        public DateTime FridayExit { get; set; }
        public ChainSnapshot[] Snapshots { get; set; } = Array.Empty<ChainSnapshot>();
        public MondayEntryResult? MondayResult { get; set; }
        public WednesdayManagementResult? WednesdayResult { get; set; }
        public FridayExitResult? FridayResult { get; set; }
        public List<BacktestPosition> Positions { get; set; } = new();
        public double FinalPnL { get; set; }
        public PerformanceMetrics? PerformanceMetrics { get; set; }
    }

    public class BatchBacktestResult
    {
        public string Underlying { get; set; } = "";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public BacktestParameters Parameters { get; set; } = new();
        public WeeklyBacktestResult[] WeeklyResults { get; set; } = Array.Empty<WeeklyBacktestResult>();
        public OverallMetrics OverallMetrics { get; set; } = new();
        public DateTime ExecutionTime { get; set; }
    }

    public class BacktestParameters
    {
        public string Underlying { get; set; } = "";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double InitialCapital { get; set; } = 100000;
        public bool UseRealFills { get; set; } = true;
    }

    public class MondayEntryResult
    {
        public bool Success { get; set; }
        public string? FailureReason { get; set; }
        public PositionPlan[] PlannedOrders { get; set; } = Array.Empty<PositionPlan>();
        public FillResult[] FillResults { get; set; } = Array.Empty<FillResult>();
        public BacktestPosition[] CreatedPositions { get; set; } = Array.Empty<BacktestPosition>();
        public double TotalRiskCapital { get; set; }
        public ChainSnapshot? EntrySnapshot { get; set; }
    }

    public class WednesdayManagementResult
    {
        public bool Success { get; set; }
        public string? FailureReason { get; set; }
        public DecisionPlan? DecisionPlan { get; set; }
        public ManagementAction[] ManagementActions { get; set; } = Array.Empty<ManagementAction>();
        public BacktestPosition[] NewPositions { get; set; } = Array.Empty<BacktestPosition>();
        public ChainSnapshot? ManagementSnapshot { get; set; }
        public PortfolioState? PortfolioStateAfter { get; set; }
    }

    public class FridayExitResult
    {
        public bool Success { get; set; }
        public string? FailureReason { get; set; }
        public ExitReport? ExitReport { get; set; }
        public ExitAction[] ExitActions { get; set; } = Array.Empty<ExitAction>();
        public double FinalPnL { get; set; }
        public ChainSnapshot? ExitSnapshot { get; set; }
    }

    public class BacktestPosition
    {
        public string Name { get; set; } = "";
        public IronCondor Structure { get; set; } = new(0, 0, 0, 0);
        public DateTime Expiry { get; set; }
        public DateTime EntryTime { get; set; }
        public double EntryPrice { get; set; }
        public double MaxLoss { get; set; }
        public OptionLeg[] Legs { get; set; } = Array.Empty<OptionLeg>();
        public PositionStatus Status { get; set; }
        public double CurrentPnL { get; set; }
    }

    public class ManagementAction
    {
        public GuardAction Action { get; set; }
        public string Reason { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public bool Success { get; set; }
        public BacktestPosition[]? ResultingPositions { get; set; }
    }

    public class ExitAction
    {
        public BacktestPosition Position { get; set; } = new();
        public DateTime ExitTime { get; set; }
        public double ExitPrice { get; set; }
        public double RealizedPnL { get; set; }
        public bool Success { get; set; }
    }

    public class PerformanceMetrics
    {
        public double WeeklyReturn { get; set; }
        public double MaxDrawdown { get; set; }
        public double WinRate { get; set; }
        public double SharpeRatio { get; set; }
        public double MaxRisk { get; set; }
    }

    public class OverallMetrics
    {
        public int TotalWeeks { get; set; }
        public int SuccessfulWeeks { get; set; }
        public double TotalPnL { get; set; }
        public double AverageWeeklyPnL { get; set; }
        public double WinRate { get; set; }
        public double LossRate { get; set; }
        public double MaxWeeklyGain { get; set; }
        public double MaxWeeklyLoss { get; set; }
        public double SharpeRatio { get; set; }
    }

    public enum PositionStatus
    {
        Open,
        Closed,
        Rolled,
        Assigned
    }

    public class MockNbboProvider : HistoricalNbboProvider
    {
        private readonly ChainSnapshot _snapshot;

        public MockNbboProvider(ChainSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public Task<IEnumerable<NbboSnapshot>> GetNbboHistory(string symbol, DateTime start, DateTime end)
        {
            var nbboSnapshots = new List<NbboSnapshot>
            {
                new NbboSnapshot(
                    Timestamp: start,
                    Symbol: symbol,
                    BidPrice: _snapshot.UnderlyingPrice * 0.98,
                    AskPrice: _snapshot.UnderlyingPrice * 1.02,
                    BidSize: 100,
                    AskSize: 100,
                    LastPrice: _snapshot.UnderlyingPrice
                )
            };

            return Task.FromResult<IEnumerable<NbboSnapshot>>(nbboSnapshots);
        }
    }
}