using System;
using System.Collections.Generic;
using System.Linq;

namespace ODTE.Strategy.CDTE.Oil
{
    public static class OilStrikes
    {
        public static double WingWidth(int dte)
        {
            return dte == 0 ? 0.5 : dte * 2.0;
        }

        public static IronCondor BuildIC(double spot, int dte, double expectedMove, Func<double, double> nearestStrike)
        {
            var wingWidth = WingWidth(dte);
            
            var shortCallStrike = nearestStrike(spot + expectedMove);
            var longCallStrike = nearestStrike(shortCallStrike + wingWidth);
            
            var shortPutStrike = nearestStrike(spot - expectedMove);
            var longPutStrike = nearestStrike(shortPutStrike - wingWidth);

            return new IronCondor(shortCallStrike, longCallStrike, shortPutStrike, longPutStrike);
        }

        public static IronCondor BuildIF(double spot, int dte, Func<double, double> nearestStrike)
        {
            var wingWidth = WingWidth(dte);
            var atmStrike = nearestStrike(spot);
            
            var longCallStrike = nearestStrike(atmStrike + wingWidth);
            var longPutStrike = nearestStrike(atmStrike - wingWidth);

            return new IronCondor(atmStrike, longCallStrike, atmStrike, longPutStrike);
        }

        public static StrikeSelectionResult OptimizeStrikes(
            double spot, 
            int dte, 
            double expectedMove, 
            ChainSnapshot chainData,
            OilCDTEConfig config)
        {
            var wingWidth = WingWidth(dte);
            var targetDelta = config.DeltaTargets.IcShortAbs;
            
            var candidateShortCall = FindStrikeByDelta(
                chainData, 
                OptionRight.Call, 
                targetDelta, 
                spot + expectedMove);
                
            var candidateShortPut = FindStrikeByDelta(
                chainData, 
                OptionRight.Put, 
                targetDelta, 
                spot - expectedMove);

            var longCallStrike = chainData.GetNearestStrike(candidateShortCall + wingWidth);
            var longPutStrike = chainData.GetNearestStrike(candidateShortPut - wingWidth);

            var projectedCredit = EstimateCredit(
                candidateShortCall, longCallStrike, 
                candidateShortPut, longPutStrike, 
                chainData);

            var maxLoss = CalculateMaxLoss(candidateShortCall, longCallStrike, projectedCredit);
            
            if (maxLoss > config.RiskCapUsd)
            {
                return OptimizeForRiskCap(spot, dte, expectedMove, chainData, config);
            }

            var qualityScore = CalculateStrikeQuality(
                candidateShortCall, candidateShortPut, spot, expectedMove, chainData);

            return new StrikeSelectionResult(
                ShortCall: candidateShortCall,
                LongCall: longCallStrike,
                ShortPut: candidateShortPut,
                LongPut: longPutStrike,
                ProjectedCredit: projectedCredit,
                MaxLoss: maxLoss,
                QualityScore: qualityScore,
                IsOptimal: true
            );
        }

        private static StrikeSelectionResult OptimizeForRiskCap(
            double spot, 
            int dte, 
            double expectedMove, 
            ChainSnapshot chainData,
            OilCDTEConfig config)
        {
            var maxRisk = config.RiskCapUsd;
            var wingWidth = WingWidth(dte);
            
            var adjustedWingWidth = wingWidth;
            while (adjustedWingWidth > 0.5)
            {
                var testShortCall = chainData.GetNearestStrike(spot + expectedMove);
                var testLongCall = chainData.GetNearestStrike(testShortCall + adjustedWingWidth);
                var testShortPut = chainData.GetNearestStrike(spot - expectedMove);
                var testLongPut = chainData.GetNearestStrike(testShortPut - adjustedWingWidth);

                var testCredit = EstimateCredit(testShortCall, testLongCall, testShortPut, testLongPut, chainData);
                var testMaxLoss = CalculateMaxLoss(testShortCall, testLongCall, testCredit);

                if (testMaxLoss <= maxRisk)
                {
                    var qualityScore = CalculateStrikeQuality(testShortCall, testShortPut, spot, expectedMove, chainData);
                    
                    return new StrikeSelectionResult(
                        ShortCall: testShortCall,
                        LongCall: testLongCall,
                        ShortPut: testShortPut,
                        LongPut: testLongPut,
                        ProjectedCredit: testCredit,
                        MaxLoss: testMaxLoss,
                        QualityScore: qualityScore,
                        IsOptimal: false
                    );
                }

                adjustedWingWidth -= 0.5;
            }

            throw new InvalidOperationException($"Cannot create position within risk cap of ${maxRisk}");
        }

        private static double FindStrikeByDelta(
            ChainSnapshot chainData, 
            OptionRight right, 
            double targetDelta, 
            double preferredStrike)
        {
            var nearestToPreferred = chainData.GetNearestStrike(preferredStrike);
            var candidateStrikes = GetStrikesAroundPrice(chainData, nearestToPreferred, 5);

            var bestStrike = candidateStrikes
                .Select(strike => new
                {
                    Strike = strike,
                    Delta = EstimateDelta(strike, chainData.UnderlyingPrice, right),
                    Distance = Math.Abs(strike - preferredStrike)
                })
                .OrderBy(x => Math.Abs(Math.Abs(x.Delta) - targetDelta))
                .ThenBy(x => x.Distance)
                .First();

            return bestStrike.Strike;
        }

        private static double[] GetStrikesAroundPrice(ChainSnapshot chainData, double centerStrike, int count)
        {
            var strikes = new List<double>();
            var increment = 0.5;
            
            for (int i = -count; i <= count; i++)
            {
                strikes.Add(centerStrike + (i * increment));
            }

            return strikes.Where(s => s > 0).ToArray();
        }

        private static double EstimateDelta(double strike, double spot, OptionRight right)
        {
            var moneyness = spot / strike;
            var roughDelta = right == OptionRight.Call 
                ? Math.Max(0, Math.Min(1, (moneyness - 0.95) * 5))
                : Math.Max(0, Math.Min(1, (1.05 - moneyness) * 5));
                
            return right == OptionRight.Call ? roughDelta : -roughDelta;
        }

        private static double EstimateCredit(
            double shortCall, double longCall, 
            double shortPut, double longPut, 
            ChainSnapshot chainData)
        {
            var callSpreadCredit = EstimateSpreadCredit(shortCall, longCall, OptionRight.Call, chainData);
            var putSpreadCredit = EstimateSpreadCredit(shortPut, longPut, OptionRight.Put, chainData);
            
            return callSpreadCredit + putSpreadCredit;
        }

        private static double EstimateSpreadCredit(
            double shortStrike, 
            double longStrike, 
            OptionRight right, 
            ChainSnapshot chainData)
        {
            var shortPrice = EstimateOptionPrice(shortStrike, chainData.UnderlyingPrice, right);
            var longPrice = EstimateOptionPrice(longStrike, chainData.UnderlyingPrice, right);
            
            return Math.Max(0, shortPrice - longPrice);
        }

        private static double EstimateOptionPrice(double strike, double spot, OptionRight right)
        {
            var intrinsic = right == OptionRight.Call 
                ? Math.Max(0, spot - strike)
                : Math.Max(0, strike - spot);
                
            var timeValue = Math.Max(0.05, Math.Min(2.0, Math.Abs(spot - strike) * 0.02));
            
            return intrinsic + timeValue;
        }

        private static double CalculateMaxLoss(double shortStrike, double longStrike, double credit)
        {
            var spreadWidth = Math.Abs(longStrike - shortStrike);
            return Math.Max(0, spreadWidth - credit) * 100;
        }

        private static double CalculateStrikeQuality(
            double shortCall, 
            double shortPut, 
            double spot, 
            double expectedMove, 
            ChainSnapshot chainData)
        {
            var callDistance = Math.Abs(shortCall - (spot + expectedMove));
            var putDistance = Math.Abs(shortPut - (spot - expectedMove));
            var symmetry = 1.0 - Math.Abs(callDistance - putDistance) / Math.Max(callDistance, putDistance);
            
            var callLiquidity = EstimateLiquidity(shortCall, chainData);
            var putLiquidity = EstimateLiquidity(shortPut, chainData);
            var avgLiquidity = (callLiquidity + putLiquidity) / 2.0;
            
            var probabilityOfProfit = EstimateProbabilityOfProfit(shortCall, shortPut, spot, expectedMove);
            
            return (symmetry * 0.3) + (avgLiquidity * 0.3) + (probabilityOfProfit * 0.4);
        }

        private static double EstimateLiquidity(double strike, ChainSnapshot chainData)
        {
            return 0.8;
        }

        private static double EstimateProbabilityOfProfit(double shortCall, double shortPut, double spot, double expectedMove)
        {
            var upperBound = shortCall;
            var lowerBound = shortPut;
            var profitRange = upperBound - lowerBound;
            var expectedRange = expectedMove * 2;
            
            return Math.Min(1.0, profitRange / Math.Max(expectedRange, profitRange));
        }

        public static StrikeAdjustmentResult AdjustForLiquidity(
            IronCondor originalCondor, 
            ChainSnapshot chainData,
            double minLiquidityScore = 0.5)
        {
            var adjustments = new List<StrikeAdjustment>();
            
            var shortCallLiquidity = EstimateLiquidity(originalCondor.ShortCall, chainData);
            if (shortCallLiquidity < minLiquidityScore)
            {
                var betterStrike = FindMoreLiquidStrike(originalCondor.ShortCall, chainData, OptionRight.Call);
                adjustments.Add(new StrikeAdjustment(
                    OriginalStrike: originalCondor.ShortCall,
                    AdjustedStrike: betterStrike,
                    Reason: $"Improved call liquidity: {shortCallLiquidity:F2} -> {EstimateLiquidity(betterStrike, chainData):F2}",
                    LegType: "Short Call"
                ));
            }

            var shortPutLiquidity = EstimateLiquidity(originalCondor.ShortPut, chainData);
            if (shortPutLiquidity < minLiquidityScore)
            {
                var betterStrike = FindMoreLiquidStrike(originalCondor.ShortPut, chainData, OptionRight.Put);
                adjustments.Add(new StrikeAdjustment(
                    OriginalStrike: originalCondor.ShortPut,
                    AdjustedStrike: betterStrike,
                    Reason: $"Improved put liquidity: {shortPutLiquidity:F2} -> {EstimateLiquidity(betterStrike, chainData):F2}",
                    LegType: "Short Put"
                ));
            }

            if (!adjustments.Any())
            {
                return new StrikeAdjustmentResult(
                    AdjustedCondor: originalCondor,
                    WasAdjusted: false,
                    Adjustments: adjustments
                );
            }

            var adjustedCondor = ApplyAdjustments(originalCondor, adjustments, chainData);
            
            return new StrikeAdjustmentResult(
                AdjustedCondor: adjustedCondor,
                WasAdjusted: true,
                Adjustments: adjustments
            );
        }

        private static double FindMoreLiquidStrike(double originalStrike, ChainSnapshot chainData, OptionRight right)
        {
            var candidates = GetStrikesAroundPrice(chainData, originalStrike, 3);
            
            return candidates
                .OrderByDescending(strike => EstimateLiquidity(strike, chainData))
                .ThenBy(strike => Math.Abs(strike - originalStrike))
                .First();
        }

        private static IronCondor ApplyAdjustments(
            IronCondor original, 
            List<StrikeAdjustment> adjustments, 
            ChainSnapshot chainData)
        {
            var shortCall = original.ShortCall;
            var longCall = original.LongCall;
            var shortPut = original.ShortPut;
            var longPut = original.LongPut;

            foreach (var adjustment in adjustments)
            {
                switch (adjustment.LegType)
                {
                    case "Short Call":
                        shortCall = adjustment.AdjustedStrike;
                        longCall = chainData.GetNearestStrike(shortCall + (original.LongCall - original.ShortCall));
                        break;
                    case "Short Put":
                        shortPut = adjustment.AdjustedStrike;
                        longPut = chainData.GetNearestStrike(shortPut - (original.ShortPut - original.LongPut));
                        break;
                }
            }

            return new IronCondor(shortCall, longCall, shortPut, longPut);
        }
    }

    public sealed record StrikeSelectionResult(
        double ShortCall,
        double LongCall,
        double ShortPut,
        double LongPut,
        double ProjectedCredit,
        double MaxLoss,
        double QualityScore,
        bool IsOptimal
    );

    public sealed record StrikeAdjustmentResult(
        IronCondor AdjustedCondor,
        bool WasAdjusted,
        List<StrikeAdjustment> Adjustments
    );

    public sealed record StrikeAdjustment(
        double OriginalStrike,
        double AdjustedStrike,
        string Reason,
        string LegType
    );
}