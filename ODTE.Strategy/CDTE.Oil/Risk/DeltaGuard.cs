using System;
using System.Linq;

namespace ODTE.Strategy.CDTE.Oil.Risk
{
    public static class DeltaGuard
    {
        public static ActionPlan Evaluate(PortfolioState state, ChainSnapshot snapshot, OilRiskGuardrails config)
        {
            var positions = state.GetAllPositions();
            var spot = snapshot.UnderlyingPrice;
            var deltaThreshold = config.DeltaGuardAbs;

            foreach (var position in positions)
            {
                var deltaViolation = CheckPositionDeltaRisk(position, spot, deltaThreshold);
                if (deltaViolation.Action != GuardAction.None)
                    return deltaViolation;
            }

            var portfolioDelta = CalculatePortfolioDelta(state, spot);
            var portfolioDeltaCheck = CheckPortfolioDeltaRisk(portfolioDelta, config);
            if (portfolioDeltaCheck.Action != GuardAction.None)
                return portfolioDeltaCheck;

            return new ActionPlan(GuardAction.None, "Delta guard checks passed", null);
        }

        private static ActionPlan CheckPositionDeltaRisk(Position position, double spot, double deltaThreshold)
        {
            var shortLegs = position.Legs.Where(leg => leg.Quantity < 0).ToArray();
            
            foreach (var shortLeg in shortLegs)
            {
                var delta = CalculateDelta(shortLeg, spot);
                var absDelta = Math.Abs(delta);
                
                if (absDelta > deltaThreshold)
                {
                    var moneyness = CalculateMoneyness(shortLeg, spot);
                    var rollCost = EstimateRollCost(shortLeg, spot, position.TicketRisk);
                    
                    var action = DetermineDeltaAction(absDelta, deltaThreshold, rollCost, position.TicketRisk);
                    
                    return new ActionPlan(
                        action,
                        $"Delta guard triggered: {shortLeg.Right} ${shortLeg.Strike} has |Δ|={absDelta:F3} > {deltaThreshold:F2} (moneyness: {moneyness:F3})",
                        new DeltaViolationPayload(position, shortLeg, delta, moneyness, rollCost, action)
                    );
                }
            }

            return new ActionPlan(GuardAction.None, "Position delta risk acceptable", null);
        }

        private static ActionPlan CheckPortfolioDeltaRisk(double portfolioDelta, OilRiskGuardrails config)
        {
            var absPortfolioDelta = Math.Abs(portfolioDelta);
            var portfolioDeltaLimit = 0.20;

            if (absPortfolioDelta > portfolioDeltaLimit)
            {
                var action = absPortfolioDelta > 0.40 ? GuardAction.ReduceSize : GuardAction.ConvertToDebitVertical;
                
                return new ActionPlan(
                    action,
                    $"Portfolio delta risk: |Δ|={absPortfolioDelta:F3} > {portfolioDeltaLimit:F2}",
                    new PortfolioDeltaRisk(portfolioDelta, portfolioDeltaLimit, absPortfolioDelta)
                );
            }

            return new ActionPlan(GuardAction.None, "Portfolio delta risk acceptable", null);
        }

        private static GuardAction DetermineDeltaAction(double absDelta, double threshold, double rollCost, double ticketRisk)
        {
            var rollCostPercent = ticketRisk > 0 ? (rollCost / ticketRisk) : 1.0;
            var deltaExcess = (absDelta - threshold) / threshold;

            if (deltaExcess > 0.67 || rollCostPercent > 0.25)
            {
                return GuardAction.Close;
            }

            if (deltaExcess > 0.33 && rollCostPercent <= 0.25)
            {
                return GuardAction.RollOutAndAway;
            }

            if (deltaExcess > 0.15)
            {
                return GuardAction.ConvertToDebitVertical;
            }

            return GuardAction.ReduceSize;
        }

        public static ActionPlan EvaluateIntraday(PortfolioState openingState, PortfolioState currentState, double spot, OilRiskGuardrails config)
        {
            var openingDelta = CalculatePortfolioDelta(openingState, spot);
            var currentDelta = CalculatePortfolioDelta(currentState, spot);
            var deltaChange = Math.Abs(currentDelta - openingDelta);
            var deltaChangePercent = Math.Abs(openingDelta) > 0.01 ? (deltaChange / Math.Abs(openingDelta)) * 100 : 0;

            if (deltaChangePercent > 100 && Math.Abs(currentDelta) > config.DeltaGuardAbs)
            {
                return new ActionPlan(
                    GuardAction.ReduceSize,
                    $"Intraday delta expansion: {deltaChangePercent:F1}% change, current |Δ|={Math.Abs(currentDelta):F3}",
                    new IntradayDeltaRisk(openingDelta, currentDelta, deltaChange, deltaChangePercent)
                );
            }

            return new ActionPlan(GuardAction.None, "Intraday delta monitoring passed", null);
        }

        public static ActionPlan EvaluateSpotMovement(PortfolioState state, double previousSpot, double currentSpot, OilRiskGuardrails config)
        {
            var spotMove = Math.Abs(currentSpot - previousSpot);
            var spotMovePercent = (spotMove / previousSpot) * 100;

            if (spotMovePercent > 2.0)
            {
                var positions = state.GetAllPositions();
                foreach (var position in positions)
                {
                    var deltaSensitivity = CalculatePositionDeltaSensitivity(position, currentSpot);
                    if (deltaSensitivity > config.DeltaGuardAbs * 0.5)
                    {
                        return new ActionPlan(
                            GuardAction.ReduceSize,
                            $"Spot movement delta risk: {spotMovePercent:F1}% move, position {position.Name} sensitivity {deltaSensitivity:F3}",
                            new SpotMovementDeltaRisk(position, previousSpot, currentSpot, spotMovePercent, deltaSensitivity)
                        );
                    }
                }
            }

            return new ActionPlan(GuardAction.None, "Spot movement delta risk acceptable", null);
        }

        private static double CalculateDelta(OptionLeg leg, double spot)
        {
            var strike = leg.Strike;
            var timeToExpiry = 7.0 / 365.0;
            var volatility = 0.25;
            var riskFreeRate = 0.05;

            var moneyness = spot / strike;
            var d1 = (Math.Log(moneyness) + (riskFreeRate + 0.5 * volatility * volatility) * timeToExpiry) / 
                     (volatility * Math.Sqrt(timeToExpiry));

            var callDelta = NormalCDF(d1);
            var putDelta = callDelta - 1.0;

            return leg.Right == OptionRight.Call ? callDelta : putDelta;
        }

        private static double CalculateMoneyness(OptionLeg leg, double spot)
        {
            return leg.Right == OptionRight.Call ? spot / leg.Strike : leg.Strike / spot;
        }

        private static double EstimateRollCost(OptionLeg leg, double spot, double ticketRisk)
        {
            var intrinsic = leg.Right == OptionRight.Call 
                ? Math.Max(0, spot - leg.Strike)
                : Math.Max(0, leg.Strike - spot);
            
            var timeValue = Math.Max(0.05, ticketRisk * 0.10);
            return intrinsic + timeValue;
        }

        private static double CalculatePortfolioDelta(PortfolioState state, double spot)
        {
            var positions = state.GetAllPositions();
            return positions.Sum(position => 
                position.Legs.Sum(leg => 
                    CalculateDelta(leg, spot) * leg.Quantity));
        }

        private static double CalculatePositionDeltaSensitivity(Position position, double spot)
        {
            return Math.Abs(position.Legs.Sum(leg => 
            {
                var delta = CalculateDelta(leg, spot);
                var gamma = CalculateGamma(leg, spot);
                return (delta + gamma * 1.0) * leg.Quantity;
            }));
        }

        private static double CalculateGamma(OptionLeg leg, double spot)
        {
            var strike = leg.Strike;
            var timeToExpiry = 7.0 / 365.0;
            var volatility = 0.25;
            var riskFreeRate = 0.05;

            var moneyness = spot / strike;
            var d1 = (Math.Log(moneyness) + (riskFreeRate + 0.5 * volatility * volatility) * timeToExpiry) / 
                     (volatility * Math.Sqrt(timeToExpiry));

            var phi = Math.Exp(-0.5 * d1 * d1) / Math.Sqrt(2 * Math.PI);
            return phi / (spot * volatility * Math.Sqrt(timeToExpiry));
        }

        private static double NormalCDF(double x)
        {
            return 0.5 * (1.0 + Erf(x / Math.Sqrt(2.0)));
        }

        private static double Erf(double x)
        {
            const double a1 =  0.254829592;
            const double a2 = -0.284496736;
            const double a3 =  1.421413741;
            const double a4 = -1.453152027;
            const double a5 =  1.061405429;
            const double p  =  0.3275911;

            var sign = x < 0 ? -1 : 1;
            x = Math.Abs(x);

            var t = 1.0 / (1.0 + p * x);
            var y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);

            return sign * y;
        }

        public static ActionPlan EvaluateLegSpecificRisk(Position position, double spot, OilRiskGuardrails config)
        {
            var shortLegs = position.Legs.Where(leg => leg.Quantity < 0).ToArray();
            var longLegs = position.Legs.Where(leg => leg.Quantity > 0).ToArray();

            foreach (var shortLeg in shortLegs)
            {
                var protectingLong = longLegs.FirstOrDefault(ll => 
                    ll.Right == shortLeg.Right && 
                    ((shortLeg.Right == OptionRight.Call && ll.Strike > shortLeg.Strike) ||
                     (shortLeg.Right == OptionRight.Put && ll.Strike < shortLeg.Strike)));

                if (protectingLong == null)
                {
                    return new ActionPlan(
                        GuardAction.Close,
                        $"Naked short leg detected: {shortLeg.Right} ${shortLeg.Strike} without protection",
                        new NakedLegRisk(position, shortLeg)
                    );
                }

                var shortDelta = Math.Abs(CalculateDelta(shortLeg, spot));
                var longDelta = Math.Abs(CalculateDelta(protectingLong, spot));
                var hedgeRatio = longDelta > 0 ? shortDelta / longDelta : double.MaxValue;

                if (hedgeRatio > 3.0 && shortDelta > config.DeltaGuardAbs)
                {
                    return new ActionPlan(
                        GuardAction.RollOutAndAway,
                        $"Poor hedge ratio: Short |Δ|={shortDelta:F3}, Long |Δ|={longDelta:F3}, ratio={hedgeRatio:F1}",
                        new HedgeRatioRisk(position, shortLeg, protectingLong, hedgeRatio)
                    );
                }
            }

            return new ActionPlan(GuardAction.None, "Leg-specific delta risk acceptable", null);
        }
    }

    public sealed record DeltaViolationPayload(
        Position Position,
        OptionLeg ViolatingLeg,
        double Delta,
        double Moneyness,
        double EstimatedRollCost,
        GuardAction RecommendedAction
    );

    public sealed record PortfolioDeltaRisk(
        double CurrentDelta,
        double DeltaLimit,
        double AbsoluteDelta
    );

    public sealed record IntradayDeltaRisk(
        double OpeningDelta,
        double CurrentDelta,
        double DeltaChange,
        double ChangePercentage
    );

    public sealed record SpotMovementDeltaRisk(
        Position Position,
        double PreviousSpot,
        double CurrentSpot,
        double SpotMovePercent,
        double DeltaSensitivity
    );

    public sealed record NakedLegRisk(
        Position Position,
        OptionLeg NakedLeg
    );

    public sealed record HedgeRatioRisk(
        Position Position,
        OptionLeg ShortLeg,
        OptionLeg LongLeg,
        double HedgeRatio
    );
}