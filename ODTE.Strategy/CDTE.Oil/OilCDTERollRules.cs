using ODTE.Strategy.CDTE.Oil.Risk;

namespace ODTE.Strategy.CDTE.Oil
{
    public static class OilCDTERollRules
    {
        public static RollDecision EvaluateRollOpportunity(
            Position currentPosition,
            ChainSnapshot wednesdaySnapshot,
            OilCDTEConfig config)
        {
            var profitPercent = currentPosition.GetProfitPercentage();
            var spot = wednesdaySnapshot.UnderlyingPrice;
            var atmIv = wednesdaySnapshot.GetAtmImpliedVolatility();

            // Take profit if >= 70%
            if (profitPercent >= config.TakeProfitCorePct)
            {
                return new RollDecision
                {
                    Action = RollAction.TakeProfit,
                    Reason = $"Profit target reached: {profitPercent:P1}",
                    NewPosition = null
                };
            }

            // Stop loss if >= 50%
            if (profitPercent <= -config.MaxDrawdownPct)
            {
                return new RollDecision
                {
                    Action = RollAction.StopLoss,
                    Reason = $"Stop loss triggered: {profitPercent:P1}",
                    NewPosition = null
                };
            }

            // Neutral zone: consider rolling
            if (Math.Abs(profitPercent) < config.NeutralBandPct)
            {
                var newExpectedMove = OilSignals.CalculateExpectedMove(atmIv, wednesdaySnapshot.Timestamp);
                var fridayExpiry = GetFridayExpiry(wednesdaySnapshot.Timestamp);
                var newDte = (fridayExpiry - wednesdaySnapshot.Timestamp.Date).Days;

                var newIC = OilStrikes.BuildIC(spot, newDte, newExpectedMove, wednesdaySnapshot.GetNearestStrike);
                var rollDebit = EstimateRollDebit(currentPosition, newIC, wednesdaySnapshot);

                if (RollBudgetEnforcer.AllowRoll(rollDebit, currentPosition.TicketRisk, config.Risk))
                {
                    return new RollDecision
                    {
                        Action = RollAction.Roll,
                        Reason = $"Neutral roll: P&L {profitPercent:P1}, debit ${rollDebit:F2}",
                        NewPosition = new RollPlan(newIC, fridayExpiry, rollDebit)
                    };
                }
                else
                {
                    return new RollDecision
                    {
                        Action = RollAction.Close,
                        Reason = $"Roll debit ${rollDebit:F2} exceeds budget",
                        NewPosition = null
                    };
                }
            }

            return new RollDecision
            {
                Action = RollAction.Hold,
                Reason = "Position within acceptable range",
                NewPosition = null
            };
        }

        private static double EstimateRollDebit(Position existing, IronCondor newIC, ChainSnapshot snapshot)
        {
            // Simplified roll cost estimation
            var currentValue = EstimatePositionValue(existing, snapshot);
            var newValue = EstimatePositionValue(newIC, snapshot);

            return Math.Max(0, newValue - currentValue);
        }

        private static double EstimatePositionValue(Position position, ChainSnapshot snapshot)
        {
            // Simplified position valuation
            return 1.0; // Placeholder
        }

        private static double EstimatePositionValue(IronCondor ic, ChainSnapshot snapshot)
        {
            // Simplified IC valuation
            return 2.0; // Placeholder
        }

        private static DateTime GetFridayExpiry(DateTime from)
        {
            var friday = from.Date;
            while (friday.DayOfWeek != DayOfWeek.Friday)
            {
                friday = friday.AddDays(1);
            }
            return friday;
        }
    }

    public sealed class RollDecision
    {
        public RollAction Action { get; set; }
        public string Reason { get; set; } = "";
        public RollPlan? NewPosition { get; set; }
    }

    public enum RollAction
    {
        Hold,
        Roll,
        TakeProfit,
        StopLoss,
        Close
    }
}