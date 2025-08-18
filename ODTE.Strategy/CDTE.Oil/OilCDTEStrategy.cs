using System;
using System.Threading.Tasks;

namespace ODTE.Strategy.CDTE.Oil
{
    public sealed class OilCDTEStrategy : IStrategy
    {
        private readonly OilCDTEConfig _config;
        private readonly AssignmentRiskChecks _assignmentChecks;
        private readonly PinRiskMonitor _pinMonitor;
        private readonly GammaBrake _gammaBrake;
        private readonly DeltaGuard _deltaGuard;
        private readonly RollBudgetEnforcer _rollBudget;
        private readonly ExitWindowEnforcer _exitWindow;

        public OilCDTEStrategy(OilCDTEConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _assignmentChecks = new AssignmentRiskChecks();
            _pinMonitor = new PinRiskMonitor();
            _gammaBrake = new GammaBrake();
            _deltaGuard = new DeltaGuard();
            _rollBudget = new RollBudgetEnforcer();
            _exitWindow = new ExitWindowEnforcer();
        }

        public async Task<PlannedOrders> EnterMondayAsync(ChainSnapshot oil10Et, OilCDTEConfig cfg)
        {
            try
            {
                var spot = oil10Et.UnderlyingPrice;
                var atmIv = oil10Et.GetAtmImpliedVolatility();
                var expectedMove = OilSignals.CalculateExpectedMove(atmIv, oil10Et.Timestamp);

                var thursdayExpiry = GetThursdayExpiry(oil10Et.Timestamp);
                var fridayExpiry = GetFridayExpiry(oil10Et.Timestamp);

                var thursdayDte = (thursdayExpiry - oil10Et.Timestamp.Date).Days;
                var fridayDte = (fridayExpiry - oil10Et.Timestamp.Date).Days;

                var coreIC = OilStrikes.BuildIC(spot, thursdayDte, expectedMove, oil10Et.GetNearestStrike);
                var carryIC = _config.IsHighIv(atmIv) 
                    ? OilStrikes.BuildIF(spot, fridayDte, oil10Et.GetNearestStrike)
                    : OilStrikes.BuildIC(spot, fridayDte, expectedMove, oil10Et.GetNearestStrike);

                var corePlan = new PositionPlan("CORE_THU", coreIC, thursdayExpiry);
                var carryPlan = new PositionPlan("CARRY_FRI", carryIC, fridayExpiry);

                var coreRiskCheck = _assignmentChecks.PreTradeGate(corePlan, oil10Et.Calendar, cfg.Risk);
                var carryRiskCheck = _assignmentChecks.PreTradeGate(carryPlan, oil10Et.Calendar, cfg.Risk);

                if (coreRiskCheck.Action != GuardAction.None)
                    corePlan = ApplyRiskAction(corePlan, coreRiskCheck);

                if (carryRiskCheck.Action != GuardAction.None)
                    carryPlan = ApplyRiskAction(carryPlan, carryRiskCheck);

                ValidateRiskCaps(corePlan, carryPlan, cfg);

                return new PlannedOrders(new[] { corePlan, carryPlan });
            }
            catch (Exception ex)
            {
                throw new OilCDTEException($"Monday entry failed: {ex.Message}", ex);
            }
        }

        public async Task<DecisionPlan> ManageWednesdayAsync(PortfolioState state, ChainSnapshot oil1230Et, OilCDTEConfig cfg)
        {
            try
            {
                var guards = new Func<ActionPlan>[]
                {
                    () => _gammaBrake.Evaluate(state.Greeks, cfg.Risk),
                    () => _deltaGuard.Evaluate(state, oil1230Et, cfg.Risk),
                    () => _exitWindow.Check(oil1230Et.Calendar, oil1230Et.Timestamp, cfg.Risk),
                    () => _pinMonitor.Check(state, oil1230Et.UnderlyingPrice, cfg.Risk),
                    () => _assignmentChecks.PreCloseGate(state, oil1230Et, oil1230Et.Calendar, cfg.Risk)
                };

                foreach (var guard in guards)
                {
                    var plan = guard();
                    if (plan.Action != GuardAction.None)
                        return new DecisionPlan(plan.Action, plan.Reason, plan.Payload);
                }

                var corePosition = state.GetPosition("CORE_THU");
                if (corePosition != null)
                {
                    var profitPct = corePosition.GetProfitPercentage();
                    
                    if (profitPct >= cfg.TakeProfitCorePct)
                    {
                        return new DecisionPlan(GuardAction.Close, "Take profit: Core >= 70%", corePosition);
                    }
                    
                    if (Math.Abs(profitPct) < cfg.NeutralBandPct)
                    {
                        var rollPlan = CreateRollPlan(corePosition, oil1230Et, cfg);
                        if (_rollBudget.AllowRoll(rollPlan.Debit, corePosition.TicketRisk, cfg.Risk))
                        {
                            return new DecisionPlan(GuardAction.RollOutAndAway, "Neutral roll Core->Fri", rollPlan);
                        }
                    }
                    
                    if (profitPct <= -cfg.MaxDrawdownPct)
                    {
                        return new DecisionPlan(GuardAction.Close, "Stop loss: Core >= 50% loss", corePosition);
                    }
                }

                return new DecisionPlan(GuardAction.None, "Hold positions", null);
            }
            catch (Exception ex)
            {
                throw new OilCDTEException($"Wednesday management failed: {ex.Message}", ex);
            }
        }

        public async Task<ExitReport> ExitWeekAsync(PortfolioState state, ChainSnapshot exitWindow, OilCDTEConfig cfg)
        {
            try
            {
                var exitCheck = _exitWindow.Check(exitWindow.Calendar, exitWindow.Timestamp, cfg.Risk);
                if (exitCheck.Action == GuardAction.Close)
                {
                    var allPositions = state.GetAllPositions();
                    var exitOrders = CreateExitOrders(allPositions, exitWindow);
                    
                    return new ExitReport(
                        success: true,
                        reason: "Force exit before session close",
                        orders: exitOrders,
                        finalPnL: state.GetTotalPnL()
                    );
                }

                var optionalLotto = CreateOptional0DTE(exitWindow, cfg);
                if (optionalLotto != null)
                {
                    return new ExitReport(
                        success: true,
                        reason: "0DTE lotto position added",
                        orders: new[] { optionalLotto },
                        finalPnL: state.GetTotalPnL()
                    );
                }

                return new ExitReport(
                    success: true,
                    reason: "Week completed normally",
                    orders: Array.Empty<PlannedOrder>(),
                    finalPnL: state.GetTotalPnL()
                );
            }
            catch (Exception ex)
            {
                throw new OilCDTEException($"Weekly exit failed: {ex.Message}", ex);
            }
        }

        private PositionPlan ApplyRiskAction(PositionPlan original, ActionPlan riskAction)
        {
            return riskAction.Action switch
            {
                GuardAction.ReduceSize => original.WithReducedSize(0.5),
                GuardAction.Close => original.WithClosed(),
                GuardAction.ConvertToDebitVertical => original.WithConvertedToDebit(),
                _ => original
            };
        }

        private void ValidateRiskCaps(PositionPlan core, PositionPlan carry, OilCDTEConfig cfg)
        {
            var totalRisk = core.MaxLoss + carry.MaxLoss;
            if (totalRisk > cfg.RiskCapUsd)
                throw new RiskCapExceededException($"Total risk ${totalRisk} exceeds cap ${cfg.RiskCapUsd}");
        }

        private RollPlan CreateRollPlan(Position corePosition, ChainSnapshot snapshot, OilCDTEConfig cfg)
        {
            var newExpiry = GetFridayExpiry(snapshot.Timestamp);
            var newDte = (newExpiry - snapshot.Timestamp.Date).Days;
            var newEM = OilSignals.CalculateExpectedMove(snapshot.GetAtmImpliedVolatility(), snapshot.Timestamp);
            
            var newIC = OilStrikes.BuildIC(snapshot.UnderlyingPrice, newDte, newEM, snapshot.GetNearestStrike);
            var rollDebit = CalculateRollDebit(corePosition, newIC, snapshot);
            
            return new RollPlan(newIC, newExpiry, rollDebit);
        }

        private PlannedOrder[] CreateExitOrders(Position[] positions, ChainSnapshot snapshot)
        {
            return positions.Select(pos => new PlannedOrder(
                OrderType.MarketClose,
                pos.Legs,
                snapshot.Timestamp,
                $"Force exit: {pos.Name}"
            )).ToArray();
        }

        private PlannedOrder? CreateOptional0DTE(ChainSnapshot snapshot, OilCDTEConfig cfg)
        {
            if (!snapshot.HasZeroDteOptions()) return null;

            var lottoIC = OilStrikes.BuildIC(
                snapshot.UnderlyingPrice, 
                0, 
                OilSignals.CalculateExpectedMove(snapshot.GetAtmImpliedVolatility(), snapshot.Timestamp),
                snapshot.GetNearestStrike
            );

            return new PlannedOrder(
                OrderType.MarketableLimit,
                lottoIC.Legs,
                snapshot.Timestamp,
                "0DTE lotto"
            ) { SizeMultiplier = 0.5 };
        }

        private DateTime GetThursdayExpiry(DateTime fromDate) => 
            GetNextWeekday(fromDate, DayOfWeek.Thursday);

        private DateTime GetFridayExpiry(DateTime fromDate) => 
            GetNextWeekday(fromDate, DayOfWeek.Friday);

        private DateTime GetNextWeekday(DateTime from, DayOfWeek target)
        {
            var daysUntilTarget = ((int)target - (int)from.DayOfWeek + 7) % 7;
            return daysUntilTarget == 0 ? from.AddDays(7) : from.AddDays(daysUntilTarget);
        }

        private double CalculateRollDebit(Position existing, IronCondor newIC, ChainSnapshot snapshot)
        {
            return 0.0;
        }
    }

    public interface IStrategy
    {
        Task<PlannedOrders> EnterMondayAsync(ChainSnapshot oil10Et, OilCDTEConfig cfg);
        Task<DecisionPlan> ManageWednesdayAsync(PortfolioState state, ChainSnapshot oil1230Et, OilCDTEConfig cfg);
        Task<ExitReport> ExitWeekAsync(PortfolioState state, ChainSnapshot exitWindow, OilCDTEConfig cfg);
    }

    public class OilCDTEException : Exception
    {
        public OilCDTEException(string message) : base(message) { }
        public OilCDTEException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class RiskCapExceededException : OilCDTEException
    {
        public RiskCapExceededException(string message) : base(message) { }
    }
}