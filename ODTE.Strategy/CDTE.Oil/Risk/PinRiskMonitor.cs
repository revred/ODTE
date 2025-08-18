using System;
using System.Linq;

namespace ODTE.Strategy.CDTE.Oil.Risk
{
    public static class PinRiskMonitor
    {
        public static ActionPlan Check(PortfolioState state, double spot, OilRiskGuardrails config)
        {
            var positions = state.GetAllPositions();
            var pinBand = config.PinBandUsd;

            foreach (var position in positions)
            {
                var expiry = GetPositionExpiry(position);
                var dte = (expiry - DateTime.Today).Days;

                if (dte > 1)
                    continue;

                var pinRiskCheck = CheckPositionPinRisk(position, spot, pinBand);
                if (pinRiskCheck.Action != GuardAction.None)
                    return pinRiskCheck;
            }

            return new ActionPlan(GuardAction.None, "No pin risk detected", null);
        }

        private static ActionPlan CheckPositionPinRisk(Position position, double spot, double pinBand)
        {
            var shortLegs = position.Legs.Where(leg => leg.Quantity < 0).ToArray();
            
            foreach (var shortLeg in shortLegs)
            {
                var distanceFromStrike = Math.Abs(spot - shortLeg.Strike);
                
                if (distanceFromStrike <= pinBand)
                {
                    var riskLevel = CalculatePinRiskLevel(distanceFromStrike, pinBand);
                    var action = DeterminePinAction(riskLevel, shortLeg);
                    
                    return new ActionPlan(
                        action,
                        $"Pin risk detected: Spot ${spot:F2} within ${distanceFromStrike:F2} of {shortLeg.Right} ${shortLeg.Strike} strike (band: ${pinBand:F2})",
                        new PinRiskPayload(position, shortLeg, distanceFromStrike, riskLevel)
                    );
                }
            }

            return new ActionPlan(GuardAction.None, "No pin risk for position", null);
        }

        private static PinRiskLevel CalculatePinRiskLevel(double distance, double pinBand)
        {
            var proximityRatio = distance / pinBand;
            
            return proximityRatio switch
            {
                <= 0.25 => PinRiskLevel.Extreme,
                <= 0.50 => PinRiskLevel.High,
                <= 0.75 => PinRiskLevel.Moderate,
                _ => PinRiskLevel.Low
            };
        }

        private static GuardAction DeterminePinAction(PinRiskLevel riskLevel, OptionLeg shortLeg)
        {
            return riskLevel switch
            {
                PinRiskLevel.Extreme => GuardAction.Close,
                PinRiskLevel.High => GuardAction.Close,
                PinRiskLevel.Moderate => GuardAction.ConvertToDebitVertical,
                PinRiskLevel.Low => GuardAction.None,
                _ => GuardAction.None
            };
        }

        private static DateTime GetPositionExpiry(Position position)
        {
            return DateTime.Today.AddDays(7);
        }

        public static ActionPlan CheckMultiLegPinRisk(PortfolioState state, double spot, OilRiskGuardrails config)
        {
            var positions = state.GetAllPositions();
            var pinBand = config.PinBandUsd;

            foreach (var position in positions)
            {
                var expiry = GetPositionExpiry(position);
                var dte = (expiry - DateTime.Today).Days;

                if (dte > 0)
                    continue;

                var icPinRisk = CheckIronCondorPinRisk(position, spot, pinBand);
                if (icPinRisk.Action != GuardAction.None)
                    return icPinRisk;

                var ifPinRisk = CheckIronFlyPinRisk(position, spot, pinBand);
                if (ifPinRisk.Action != GuardAction.None)
                    return ifPinRisk;
            }

            return new ActionPlan(GuardAction.None, "No multi-leg pin risk detected", null);
        }

        private static ActionPlan CheckIronCondorPinRisk(Position position, double spot, double pinBand)
        {
            if (!IsIronCondor(position))
                return new ActionPlan(GuardAction.None, "Not an iron condor", null);

            var shortLegs = position.Legs.Where(leg => leg.Quantity < 0).ToArray();
            var shortCall = shortLegs.FirstOrDefault(leg => leg.Right == OptionRight.Call);
            var shortPut = shortLegs.FirstOrDefault(leg => leg.Right == OptionRight.Put);

            if (shortCall == null || shortPut == null)
                return new ActionPlan(GuardAction.None, "Invalid iron condor structure", null);

            var callDistance = Math.Abs(spot - shortCall.Strike);
            var putDistance = Math.Abs(spot - shortPut.Strike);
            var minDistance = Math.Min(callDistance, putDistance);

            if (minDistance <= pinBand)
            {
                var threatenedSide = callDistance < putDistance ? "call" : "put";
                var maxGammaExposure = CalculateMaxGammaExposure(position, spot);

                return new ActionPlan(
                    GuardAction.Close,
                    $"Iron condor pin risk: {threatenedSide} side within ${minDistance:F2} of strike, max gamma exposure ${maxGammaExposure:F0}",
                    new IronCondorPinRisk(position, threatenedSide, minDistance, maxGammaExposure)
                );
            }

            return new ActionPlan(GuardAction.None, "Iron condor pin risk acceptable", null);
        }

        private static ActionPlan CheckIronFlyPinRisk(Position position, double spot, double pinBand)
        {
            if (!IsIronFly(position))
                return new ActionPlan(GuardAction.None, "Not an iron fly", null);

            var shortLegs = position.Legs.Where(leg => leg.Quantity < 0).ToArray();
            var atmStrike = shortLegs.FirstOrDefault()?.Strike ?? 0;
            var distanceFromAtm = Math.Abs(spot - atmStrike);

            if (distanceFromAtm <= pinBand)
            {
                var maxGammaExposure = CalculateMaxGammaExposure(position, spot);

                return new ActionPlan(
                    GuardAction.Close,
                    $"Iron fly pin risk: Spot within ${distanceFromAtm:F2} of ATM strike ${atmStrike:F2}, max gamma exposure ${maxGammaExposure:F0}",
                    new IronFlyPinRisk(position, atmStrike, distanceFromAtm, maxGammaExposure)
                );
            }

            return new ActionPlan(GuardAction.None, "Iron fly pin risk acceptable", null);
        }

        private static bool IsIronCondor(Position position)
        {
            var callLegs = position.Legs.Where(leg => leg.Right == OptionRight.Call).Count();
            var putLegs = position.Legs.Where(leg => leg.Right == OptionRight.Put).Count();
            return callLegs == 2 && putLegs == 2;
        }

        private static bool IsIronFly(Position position)
        {
            var shortLegs = position.Legs.Where(leg => leg.Quantity < 0).ToArray();
            if (shortLegs.Length != 2) return false;

            var strikes = shortLegs.Select(leg => leg.Strike).Distinct().ToArray();
            return strikes.Length == 1;
        }

        private static double CalculateMaxGammaExposure(Position position, double spot)
        {
            var shortLegs = position.Legs.Where(leg => leg.Quantity < 0);
            var totalGamma = shortLegs.Sum(leg => CalculateGamma(leg, spot) * Math.Abs(leg.Quantity));
            return totalGamma * 100;
        }

        private static double CalculateGamma(OptionLeg leg, double spot)
        {
            var moneyness = spot / leg.Strike;
            var timeToExpiry = 1.0 / 365.0;
            var volatility = 0.25;

            var atTheMoneyFactor = Math.Exp(-Math.Pow(Math.Log(moneyness), 2) / (2 * volatility * volatility * timeToExpiry));
            return atTheMoneyFactor / (spot * volatility * Math.Sqrt(2 * Math.PI * timeToExpiry));
        }
    }

    public enum PinRiskLevel
    {
        Low,
        Moderate,
        High,
        Extreme
    }

    public sealed record PinRiskPayload(
        Position Position,
        OptionLeg ThreatenedLeg,
        double DistanceFromStrike,
        PinRiskLevel RiskLevel
    );

    public sealed record IronCondorPinRisk(
        Position Position,
        string ThreatenedSide,
        double MinDistance,
        double MaxGammaExposure
    );

    public sealed record IronFlyPinRisk(
        Position Position,
        double AtmStrike,
        double DistanceFromAtm,
        double MaxGammaExposure
    );
}