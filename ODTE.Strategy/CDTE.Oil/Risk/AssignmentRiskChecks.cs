namespace ODTE.Strategy.CDTE.Oil.Risk
{
    public static class AssignmentRiskChecks
    {
        public static ActionPlan PreTradeGate(PositionPlan plan, ProductCalendar calendar, OilRiskGuardrails config)
        {
            if (plan.Structure == null)
                return new ActionPlan(GuardAction.None, "No structure to validate", null);

            var expiry = plan.Expiry;
            var dte = (expiry - DateTime.Today).Days;

            var shortLegs = plan.Structure.Legs.Where(leg => leg.Quantity < 0).ToArray();
            if (!shortLegs.Any())
                return new ActionPlan(GuardAction.None, "No short legs to check", null);

            if (dte <= 1)
            {
                return new ActionPlan(
                    GuardAction.Close,
                    $"DTE <= 1: Assignment risk too high on {plan.Name}",
                    plan
                );
            }

            if (plan.MaxLoss > config.GammaMaxUsdPer1)
            {
                return new ActionPlan(
                    GuardAction.ReduceSize,
                    $"Max loss ${plan.MaxLoss} exceeds gamma limit ${config.GammaMaxUsdPer1}",
                    plan
                );
            }

            return new ActionPlan(GuardAction.None, "Pre-trade checks passed", null);
        }

        public static ActionPlan PreCloseGate(
            PortfolioState state,
            ChainSnapshot marketSnapshot,
            ProductCalendar calendar,
            OilRiskGuardrails config)
        {
            var positions = state.GetAllPositions();
            var spot = marketSnapshot.UnderlyingPrice;
            var currentTime = marketSnapshot.Timestamp;

            foreach (var position in positions)
            {
                var expiry = GetPositionExpiry(position);
                var dte = (expiry - currentTime.Date).Days;

                var clCheck = CheckCLFuturesAssignment(position, spot, dte, config);
                if (clCheck.Action != GuardAction.None)
                    return clCheck;

                var usoCheck = CheckUSOEquityAssignment(position, spot, dte, config, currentTime);
                if (usoCheck.Action != GuardAction.None)
                    return usoCheck;

                var definedRiskCheck = CheckDefinedRiskIntegrity(position);
                if (definedRiskCheck.Action != GuardAction.None)
                    return definedRiskCheck;
            }

            return new ActionPlan(GuardAction.None, "All assignment checks passed", null);
        }

        private static ActionPlan CheckCLFuturesAssignment(
            Position position,
            double spot,
            int dte,
            OilRiskGuardrails config)
        {
            if (!IsCLPosition(position))
                return new ActionPlan(GuardAction.None, "Not a CL position", null);

            var shortLegs = position.Legs.Where(leg => leg.Quantity < 0).ToArray();

            foreach (var shortLeg in shortLegs)
            {
                var isItm = IsInTheMoney(shortLeg, spot);
                var delta = CalculateDelta(shortLeg, spot);

                if (dte <= 1 && (isItm || Math.Abs(delta) >= config.DeltaItmGuard))
                {
                    return new ActionPlan(
                        GuardAction.Close,
                        $"CL assignment risk: DTE={dte}, ITM={isItm}, |Δ|={Math.Abs(delta):F2} >= {config.DeltaItmGuard}",
                        position
                    );
                }
            }

            return new ActionPlan(GuardAction.None, "CL assignment check passed", null);
        }

        private static ActionPlan CheckUSOEquityAssignment(
            Position position,
            double spot,
            int dte,
            OilRiskGuardrails config,
            DateTime currentTime)
        {
            if (!IsUSOPosition(position))
                return new ActionPlan(GuardAction.None, "Not a USO position", null);

            var exDivDate = GetNextExDividendDate(currentTime);
            var shortCalls = position.Legs.Where(leg => leg.Quantity < 0 && leg.Right == OptionRight.Call).ToArray();

            foreach (var shortCall in shortCalls)
            {
                var isItm = shortCall.Strike < spot;
                var extrinsic = CalculateExtrinsicValue(shortCall, spot);
                var daysToExDiv = exDivDate.HasValue ? (exDivDate.Value - currentTime.Date).Days : int.MaxValue;

                if (daysToExDiv <= 1 && isItm && extrinsic <= config.ExtrinsicMin)
                {
                    return new ActionPlan(
                        GuardAction.Close,
                        $"USO early assignment risk: Ex-div in {daysToExDiv} days, ITM call with extrinsic ${extrinsic:F2} <= ${config.ExtrinsicMin}",
                        position
                    );
                }
            }

            return new ActionPlan(GuardAction.None, "USO assignment check passed", null);
        }

        private static ActionPlan CheckDefinedRiskIntegrity(Position position)
        {
            var callLegs = position.Legs.Where(leg => leg.Right == OptionRight.Call).ToArray();
            var putLegs = position.Legs.Where(leg => leg.Right == OptionRight.Put).ToArray();

            var hasNakedCall = callLegs.Any(leg => leg.Quantity < 0) &&
                               !callLegs.Any(leg => leg.Quantity > 0);

            var hasNakedPut = putLegs.Any(leg => leg.Quantity < 0) &&
                              !putLegs.Any(leg => leg.Quantity > 0);

            if (hasNakedCall || hasNakedPut)
            {
                return new ActionPlan(
                    GuardAction.Close,
                    $"Undefined risk detected: Naked call={hasNakedCall}, Naked put={hasNakedPut}",
                    position
                );
            }

            return new ActionPlan(GuardAction.None, "Defined risk integrity maintained", null);
        }

        private static bool IsCLPosition(Position position) =>
            position.Name.Contains("CL", StringComparison.OrdinalIgnoreCase);

        private static bool IsUSOPosition(Position position) =>
            position.Name.Contains("USO", StringComparison.OrdinalIgnoreCase);

        private static bool IsInTheMoney(OptionLeg leg, double spot) =>
            leg.Right == OptionRight.Call ? leg.Strike < spot : leg.Strike > spot;

        private static double CalculateDelta(OptionLeg leg, double spot)
        {
            var moneyness = leg.Right == OptionRight.Call
                ? spot / leg.Strike
                : leg.Strike / spot;

            return leg.Right == OptionRight.Call
                ? Math.Max(0, Math.Min(1, moneyness - 0.5))
                : Math.Max(0, Math.Min(1, 1.5 - moneyness));
        }

        private static double CalculateExtrinsicValue(OptionLeg leg, double spot)
        {
            var intrinsic = leg.Right == OptionRight.Call
                ? Math.Max(0, spot - leg.Strike)
                : Math.Max(0, leg.Strike - spot);

            var theoreticalValue = intrinsic + 0.05;
            return Math.Max(0, theoreticalValue - intrinsic);
        }

        private static DateTime? GetNextExDividendDate(DateTime currentTime)
        {
            var quarterEndMonths = new[] { 3, 6, 9, 12 };
            var currentMonth = currentTime.Month;

            var nextQuarterMonth = quarterEndMonths.FirstOrDefault(m => m > currentMonth);
            if (nextQuarterMonth == 0)
                nextQuarterMonth = quarterEndMonths[0];

            var exDivYear = nextQuarterMonth > currentMonth ? currentTime.Year : currentTime.Year + 1;
            return new DateTime(exDivYear, nextQuarterMonth, 15);
        }

        private static DateTime GetPositionExpiry(Position position)
        {
            return DateTime.Today.AddDays(7);
        }
    }
}