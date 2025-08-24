namespace ODTE.Strategy.CDTE.Oil.Risk
{
    public static class GammaBrake
    {
        public static ActionPlan Evaluate(GreeksAggregate greeks, OilRiskGuardrails config)
        {
            var portfolioGammaUsdPer1 = Math.Abs(greeks.Gamma * 100);
            var gammaLimit = config.GammaMaxUsdPer1;

            if (portfolioGammaUsdPer1 <= gammaLimit)
            {
                return new ActionPlan(
                    GuardAction.None,
                    $"Gamma exposure acceptable: ${portfolioGammaUsdPer1:F0} <= ${gammaLimit:F0}",
                    null
                );
            }

            var excessGamma = portfolioGammaUsdPer1 - gammaLimit;
            var excessPercentage = (excessGamma / gammaLimit) * 100;

            var action = DetermineGammaAction(excessPercentage);
            var payload = new GammaBrakePayload(
                CurrentGamma: portfolioGammaUsdPer1,
                GammaLimit: gammaLimit,
                ExcessGamma: excessGamma,
                ExcessPercentage: excessPercentage,
                RecommendedAction: action
            );

            return new ActionPlan(
                action,
                $"Gamma brake triggered: ${portfolioGammaUsdPer1:F0} exceeds limit ${gammaLimit:F0} by ${excessGamma:F0} ({excessPercentage:F1}%)",
                payload
            );
        }

        private static GuardAction DetermineGammaAction(double excessPercentage)
        {
            return excessPercentage switch
            {
                > 100 => GuardAction.Close,
                > 50 => GuardAction.ReduceSize,
                > 25 => GuardAction.ConvertToDebitVertical,
                _ => GuardAction.ReduceSize
            };
        }

        public static ActionPlan EvaluatePositionSpecific(PortfolioState state, OilRiskGuardrails config)
        {
            var positions = state.GetAllPositions();
            var totalGammaExposure = 0.0;

            foreach (var position in positions)
            {
                var positionGamma = CalculatePositionGamma(position);
                totalGammaExposure += Math.Abs(positionGamma);

                var positionGammaUsd = Math.Abs(positionGamma * 100);
                if (positionGammaUsd > config.GammaMaxUsdPer1 * 0.5)
                {
                    return new ActionPlan(
                        GuardAction.ReduceSize,
                        $"Individual position gamma risk: {position.Name} has ${positionGammaUsd:F0} exposure",
                        new PositionGammaRisk(position, positionGammaUsd)
                    );
                }
            }

            var portfolioGammaUsd = totalGammaExposure * 100;
            if (portfolioGammaUsd > config.GammaMaxUsdPer1)
            {
                var riskiestPosition = FindRiskiestGammaPosition(positions);
                return new ActionPlan(
                    GuardAction.ReduceSize,
                    $"Portfolio gamma limit exceeded: ${portfolioGammaUsd:F0} > ${config.GammaMaxUsdPer1:F0}, target {riskiestPosition?.Name}",
                    riskiestPosition
                );
            }

            return new ActionPlan(GuardAction.None, "Position-specific gamma analysis passed", null);
        }

        public static ActionPlan EvaluateIntraday(GreeksAggregate currentGreeks, GreeksAggregate openingGreeks, OilRiskGuardrails config)
        {
            var currentGammaUsd = Math.Abs(currentGreeks.Gamma * 100);
            var openingGammaUsd = Math.Abs(openingGreeks.Gamma * 100);
            var gammaChange = currentGammaUsd - openingGammaUsd;
            var gammaChangePercent = openingGammaUsd > 0 ? (gammaChange / openingGammaUsd) * 100 : 0;

            if (Math.Abs(gammaChangePercent) > 50 && currentGammaUsd > config.GammaMaxUsdPer1 * 0.8)
            {
                return new ActionPlan(
                    GuardAction.ReduceSize,
                    $"Intraday gamma expansion: {gammaChangePercent:F1}% increase to ${currentGammaUsd:F0}",
                    new IntradayGammaRisk(currentGammaUsd, openingGammaUsd, gammaChangePercent)
                );
            }

            if (currentGammaUsd > config.GammaMaxUsdPer1)
            {
                return new ActionPlan(
                    GuardAction.ReduceSize,
                    $"Intraday gamma limit breach: ${currentGammaUsd:F0} > ${config.GammaMaxUsdPer1:F0}",
                    new IntradayGammaRisk(currentGammaUsd, openingGammaUsd, gammaChangePercent)
                );
            }

            return new ActionPlan(GuardAction.None, "Intraday gamma monitoring passed", null);
        }

        public static double CalculatePositionGamma(Position position)
        {
            return position.Legs.Sum(leg =>
            {
                var legGamma = CalculateLegGamma(leg);
                return legGamma * leg.Quantity;
            });
        }

        private static double CalculateLegGamma(OptionLeg leg)
        {
            var timeToExpiry = 7.0 / 365.0;
            var volatility = 0.25;
            var riskFreeRate = 0.05;

            var spot = 75.0;
            var strike = leg.Strike;
            var moneyness = spot / strike;

            var d1 = (Math.Log(moneyness) + (riskFreeRate + 0.5 * volatility * volatility) * timeToExpiry) /
                     (volatility * Math.Sqrt(timeToExpiry));

            var phi = Math.Exp(-0.5 * d1 * d1) / Math.Sqrt(2 * Math.PI);
            var gamma = phi / (spot * volatility * Math.Sqrt(timeToExpiry));

            return gamma;
        }

        private static Position? FindRiskiestGammaPosition(Position[] positions)
        {
            return positions
                .OrderByDescending(p => Math.Abs(CalculatePositionGamma(p)))
                .FirstOrDefault();
        }

        public static ActionPlan EvaluateGammaConcentration(PortfolioState state, double spot, OilRiskGuardrails config)
        {
            var positions = state.GetAllPositions();
            var strikeGammaMap = new Dictionary<double, double>();

            foreach (var position in positions)
            {
                foreach (var leg in position.Legs.Where(l => l.Quantity != 0))
                {
                    var legGamma = CalculateLegGamma(leg) * Math.Abs(leg.Quantity);
                    strikeGammaMap[leg.Strike] = strikeGammaMap.GetValueOrDefault(leg.Strike, 0) + legGamma;
                }
            }

            var maxStrikeGamma = strikeGammaMap.Values.DefaultIfEmpty(0).Max();
            var maxStrikeGammaUsd = maxStrikeGamma * 100;

            if (maxStrikeGammaUsd > config.GammaMaxUsdPer1 * 0.4)
            {
                var concentratedStrike = strikeGammaMap.First(kvp => Math.Abs(kvp.Value - maxStrikeGamma) < 0.001).Key;
                var distanceFromSpot = Math.Abs(spot - concentratedStrike);

                return new ActionPlan(
                    GuardAction.ConvertToDebitVertical,
                    $"Gamma concentration risk: ${maxStrikeGammaUsd:F0} at ${concentratedStrike:F2} strike (${distanceFromSpot:F2} from spot)",
                    new GammaConcentrationRisk(concentratedStrike, maxStrikeGammaUsd, distanceFromSpot)
                );
            }

            return new ActionPlan(GuardAction.None, "Gamma concentration acceptable", null);
        }
    }

    public sealed record GammaBrakePayload(
        double CurrentGamma,
        double GammaLimit,
        double ExcessGamma,
        double ExcessPercentage,
        GuardAction RecommendedAction
    );

    public sealed record PositionGammaRisk(
        Position Position,
        double GammaExposureUsd
    );

    public sealed record IntradayGammaRisk(
        double CurrentGammaUsd,
        double OpeningGammaUsd,
        double ChangePercentage
    );

    public sealed record GammaConcentrationRisk(
        double ConcentratedStrike,
        double GammaExposureUsd,
        double DistanceFromSpot
    );
}