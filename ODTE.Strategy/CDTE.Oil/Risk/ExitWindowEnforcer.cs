namespace ODTE.Strategy.CDTE.Oil.Risk
{
    public static class ExitWindowEnforcer
    {
        public static ActionPlan Check(ProductCalendar calendar, DateTimeOffset nowEt, OilRiskGuardrails config)
        {
            var exitBufferMinutes = config.ExitBufferMin;
            var sessionClose = calendar.GetSessionClose(nowEt.Date);
            var forceExitTime = sessionClose.AddMinutes(-exitBufferMinutes);

            if (nowEt >= forceExitTime)
            {
                var minutesToClose = (sessionClose - nowEt).TotalMinutes;
                return new ActionPlan(
                    GuardAction.Close,
                    $"Force exit window: {minutesToClose:F0} minutes to close (buffer: {exitBufferMinutes} min)",
                    new ExitWindowPayload(sessionClose, forceExitTime, nowEt, minutesToClose)
                );
            }

            var warningTime = forceExitTime.AddMinutes(-15);
            if (nowEt >= warningTime)
            {
                var minutesToForceExit = (forceExitTime - nowEt).TotalMinutes;
                return new ActionPlan(
                    GuardAction.None,
                    $"Exit warning: {minutesToForceExit:F0} minutes to force exit",
                    new ExitWarningPayload(forceExitTime, nowEt, minutesToForceExit)
                );
            }

            return new ActionPlan(GuardAction.None, "Exit window check passed", null);
        }

        public static ExitWindowAnalysis AnalyzeExitTiming(
            ProductCalendar calendar,
            DateTimeOffset currentTime,
            List<Position> positions,
            OilRiskGuardrails config)
        {
            var exitBufferMinutes = config.ExitBufferMin;
            var sessionClose = calendar.GetSessionClose(currentTime.Date);
            var forceExitTime = sessionClose.AddMinutes(-exitBufferMinutes);
            var minutesToForceExit = (forceExitTime - currentTime).TotalMinutes;

            var positionAnalysis = positions.Select(position =>
                AnalyzePositionExitUrgency(position, currentTime, forceExitTime, sessionClose))
                .ToList();

            var urgentPositions = positionAnalysis.Where(pa => pa.UrgencyLevel >= ExitUrgencyLevel.High).ToList();
            var overallUrgency = DetermineOverallUrgency(minutesToForceExit, urgentPositions.Count, positions.Count);

            return new ExitWindowAnalysis(
                CurrentTime: currentTime,
                SessionClose: sessionClose,
                ForceExitTime: forceExitTime,
                MinutesToForceExit: minutesToForceExit,
                OverallUrgency: overallUrgency,
                PositionAnalysis: positionAnalysis,
                RecommendedAction: DetermineRecommendedAction(overallUrgency, urgentPositions)
            );
        }

        private static PositionExitAnalysis AnalyzePositionExitUrgency(
            Position position,
            DateTimeOffset currentTime,
            DateTimeOffset forceExitTime,
            DateTimeOffset sessionClose)
        {
            var expiry = GetPositionExpiry(position);
            var dte = (expiry - currentTime.Date).Days;
            var minutesToForceExit = (forceExitTime - currentTime).TotalMinutes;
            var minutesToExpiry = (expiry - currentTime).TotalMinutes;

            var urgencyLevel = DeterminePositionUrgency(dte, minutesToForceExit, minutesToExpiry);
            var exitComplexity = CalculateExitComplexity(position);
            var estimatedExitTime = EstimateExitTime(position, exitComplexity);

            return new PositionExitAnalysis(
                Position: position,
                DTE: dte,
                MinutesToForceExit: minutesToForceExit,
                MinutesToExpiry: minutesToExpiry,
                UrgencyLevel: urgencyLevel,
                ExitComplexity: exitComplexity,
                EstimatedExitTime: estimatedExitTime,
                CanExitSafely: minutesToForceExit > estimatedExitTime
            );
        }

        private static ExitUrgencyLevel DeterminePositionUrgency(int dte, double minutesToForceExit, double minutesToExpiry)
        {
            if (dte == 0 && minutesToExpiry < 60)
                return ExitUrgencyLevel.Critical;

            if (minutesToForceExit < 15)
                return ExitUrgencyLevel.Critical;

            if (minutesToForceExit < 30 || (dte == 0 && minutesToExpiry < 120))
                return ExitUrgencyLevel.High;

            if (minutesToForceExit < 60)
                return ExitUrgencyLevel.Medium;

            return ExitUrgencyLevel.Low;
        }

        private static ExitComplexity CalculateExitComplexity(Position position)
        {
            var legCount = position.Legs.Length;
            var hasShortLegs = position.Legs.Any(leg => leg.Quantity < 0);
            var hasMultipleExpirations = position.Legs.Select(leg => GetLegExpiry(leg)).Distinct().Count() > 1;

            if (legCount >= 4 && hasShortLegs && hasMultipleExpirations)
                return ExitComplexity.Complex;

            if (legCount >= 3 && hasShortLegs)
                return ExitComplexity.Moderate;

            if (legCount <= 2)
                return ExitComplexity.Simple;

            return ExitComplexity.Moderate;
        }

        private static double EstimateExitTime(Position position, ExitComplexity complexity)
        {
            return complexity switch
            {
                ExitComplexity.Simple => 2.0,
                ExitComplexity.Moderate => 5.0,
                ExitComplexity.Complex => 10.0,
                _ => 5.0
            };
        }

        private static ExitUrgencyLevel DetermineOverallUrgency(double minutesToForceExit, int urgentPositionCount, int totalPositions)
        {
            if (minutesToForceExit < 15 || urgentPositionCount == totalPositions)
                return ExitUrgencyLevel.Critical;

            if (minutesToForceExit < 30 || urgentPositionCount > totalPositions * 0.5)
                return ExitUrgencyLevel.High;

            if (minutesToForceExit < 60)
                return ExitUrgencyLevel.Medium;

            return ExitUrgencyLevel.Low;
        }

        private static string DetermineRecommendedAction(ExitUrgencyLevel urgency, List<PositionExitAnalysis> urgentPositions)
        {
            return urgency switch
            {
                ExitUrgencyLevel.Critical => "Immediately close all positions - market orders if necessary",
                ExitUrgencyLevel.High => $"Close {urgentPositions.Count} urgent positions now, others soon",
                ExitUrgencyLevel.Medium => "Begin orderly exit process for all positions",
                ExitUrgencyLevel.Low => "Monitor exit windows, no immediate action required",
                _ => "Continue normal monitoring"
            };
        }

        public static ActionPlan CheckSpecialExitConditions(
            ProductCalendar calendar,
            DateTimeOffset currentTime,
            OilRiskGuardrails config)
        {
            if (calendar.IsEarlyClose(currentTime.Date))
            {
                var earlyCloseTime = calendar.GetEarlyCloseTime(currentTime.Date);
                var adjustedForceExit = earlyCloseTime.AddMinutes(-config.ExitBufferMin);

                if (currentTime >= adjustedForceExit)
                {
                    return new ActionPlan(
                        GuardAction.Close,
                        $"Early close day: Force exit for {earlyCloseTime:HH:mm} close",
                        new EarlyClosePayload(earlyCloseTime, adjustedForceExit, currentTime)
                    );
                }
            }

            if (IsHolidayEve(calendar, currentTime.Date))
            {
                var extendedBuffer = config.ExitBufferMin + 30;
                var sessionClose = calendar.GetSessionClose(currentTime.Date);
                var holidayForceExit = sessionClose.AddMinutes(-extendedBuffer);

                if (currentTime >= holidayForceExit)
                {
                    return new ActionPlan(
                        GuardAction.Close,
                        $"Holiday eve: Extended exit buffer ({extendedBuffer} min)",
                        new HolidayEvePayload(sessionClose, holidayForceExit, currentTime, extendedBuffer)
                    );
                }
            }

            return new ActionPlan(GuardAction.None, "No special exit conditions", null);
        }

        private static bool IsHolidayEve(ProductCalendar calendar, DateTime date)
        {
            var nextTradingDay = calendar.GetNextTradingDay(date);
            return (nextTradingDay - date).Days > 1;
        }

        private static DateTime GetPositionExpiry(Position position)
        {
            return DateTime.Today.AddDays(7);
        }

        private static DateTime GetLegExpiry(OptionLeg leg)
        {
            return DateTime.Today.AddDays(7);
        }

        public static ExitExecutionPlan CreateExecutionPlan(
            List<Position> positions,
            ExitWindowAnalysis analysis,
            OilRiskGuardrails config)
        {
            var sortedPositions = positions
                .OrderByDescending(p => analysis.PositionAnalysis
                    .First(pa => pa.Position == p).UrgencyLevel)
                .ThenBy(p => analysis.PositionAnalysis
                    .First(pa => pa.Position == p).EstimatedExitTime)
                .ToList();

            var executionSteps = new List<ExitExecutionStep>();
            var currentTime = analysis.CurrentTime;

            foreach (var position in sortedPositions)
            {
                var positionAnalysis = analysis.PositionAnalysis.First(pa => pa.Position == position);
                var orderType = DetermineOrderType(positionAnalysis.UrgencyLevel);

                executionSteps.Add(new ExitExecutionStep(
                    Position: position,
                    ExecutionTime: currentTime,
                    OrderType: orderType,
                    Priority: (int)positionAnalysis.UrgencyLevel,
                    EstimatedDuration: positionAnalysis.EstimatedExitTime
                ));

                currentTime = currentTime.AddMinutes(positionAnalysis.EstimatedExitTime);
            }

            return new ExitExecutionPlan(
                TotalEstimatedTime: executionSteps.Sum(step => step.EstimatedDuration),
                ExecutionSteps: executionSteps,
                IsViable: currentTime <= analysis.ForceExitTime
            );
        }

        private static OrderType DetermineOrderType(ExitUrgencyLevel urgency)
        {
            return urgency switch
            {
                ExitUrgencyLevel.Critical => OrderType.MarketClose,
                ExitUrgencyLevel.High => OrderType.MarketableLimit,
                _ => OrderType.LimitOrder
            };
        }
    }

    public enum ExitUrgencyLevel
    {
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }

    public enum ExitComplexity
    {
        Simple,
        Moderate,
        Complex
    }

    public sealed record ExitWindowPayload(
        DateTimeOffset SessionClose,
        DateTimeOffset ForceExitTime,
        DateTimeOffset CurrentTime,
        double MinutesToClose
    );

    public sealed record ExitWarningPayload(
        DateTimeOffset ForceExitTime,
        DateTimeOffset CurrentTime,
        double MinutesToForceExit
    );

    public sealed record EarlyClosePayload(
        DateTimeOffset EarlyCloseTime,
        DateTimeOffset AdjustedForceExit,
        DateTimeOffset CurrentTime
    );

    public sealed record HolidayEvePayload(
        DateTimeOffset SessionClose,
        DateTimeOffset HolidayForceExit,
        DateTimeOffset CurrentTime,
        int ExtendedBufferMinutes
    );

    public sealed record ExitWindowAnalysis(
        DateTimeOffset CurrentTime,
        DateTimeOffset SessionClose,
        DateTimeOffset ForceExitTime,
        double MinutesToForceExit,
        ExitUrgencyLevel OverallUrgency,
        List<PositionExitAnalysis> PositionAnalysis,
        string RecommendedAction
    );

    public sealed record PositionExitAnalysis(
        Position Position,
        int DTE,
        double MinutesToForceExit,
        double MinutesToExpiry,
        ExitUrgencyLevel UrgencyLevel,
        ExitComplexity ExitComplexity,
        double EstimatedExitTime,
        bool CanExitSafely
    );

    public sealed record ExitExecutionPlan(
        double TotalEstimatedTime,
        List<ExitExecutionStep> ExecutionSteps,
        bool IsViable
    );

    public sealed record ExitExecutionStep(
        Position Position,
        DateTimeOffset ExecutionTime,
        OrderType OrderType,
        int Priority,
        double EstimatedDuration
    );
}