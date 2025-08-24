namespace ODTE.Strategy.CDTE.Oil
{
    public static class OilSignals
    {
        public static double CalculateExpectedMove(double atmImpliedVolatility, DateTime timestamp)
        {
            var daysToExpiry = 1.0; // For weekly options
            var yearFraction = daysToExpiry / 365.0;

            // Expected move = spot * IV * sqrt(time)
            var expectedMovePercent = atmImpliedVolatility * Math.Sqrt(yearFraction);

            // For oil, typical spot price around $75
            var spotPrice = 75.0; // This would come from market data in real implementation

            return spotPrice * expectedMovePercent;
        }

        public static bool IsHighVolatilityRegime(double atmImpliedVolatility, double vixLevel)
        {
            return atmImpliedVolatility > 0.30 || vixLevel > 25.0;
        }

        public static double AdjustExpectedMoveForRegime(double baseExpectedMove, double vixLevel, bool hasRecentEvents)
        {
            var adjustment = 1.0;

            // Increase expected move during high VIX
            if (vixLevel > 30)
                adjustment *= 1.2;
            else if (vixLevel > 25)
                adjustment *= 1.1;

            // Increase expected move around events
            if (hasRecentEvents)
                adjustment *= 1.15;

            return baseExpectedMove * adjustment;
        }
    }
}