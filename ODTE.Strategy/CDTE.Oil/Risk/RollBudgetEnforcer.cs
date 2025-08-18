using System;
using System.Collections.Generic;
using System.Linq;

namespace ODTE.Strategy.CDTE.Oil.Risk
{
    public static class RollBudgetEnforcer
    {
        public static bool AllowRoll(double proposedDebit, double ticketRisk, OilRiskGuardrails config)
        {
            if (ticketRisk <= 0)
                return false;

            var debitCapPercent = config.RollDebitCapPctOfRisk;
            var maxAllowedDebit = ticketRisk * debitCapPercent;

            return proposedDebit <= maxAllowedDebit;
        }

        public static RollBudgetAnalysis AnalyzeRollProposal(
            Position currentPosition, 
            RollPlan proposedRoll, 
            OilRiskGuardrails config)
        {
            var ticketRisk = currentPosition.TicketRisk;
            var rollDebit = proposedRoll.Debit;
            var debitCapPercent = config.RollDebitCapPctOfRisk;
            var maxAllowedDebit = ticketRisk * debitCapPercent;
            
            var isAllowed = rollDebit <= maxAllowedDebit;
            var utilizationPercent = maxAllowedDebit > 0 ? (rollDebit / maxAllowedDebit) * 100 : 0;
            var excessDebit = Math.Max(0, rollDebit - maxAllowedDebit);

            var recommendation = DetermineRollRecommendation(utilizationPercent, excessDebit, currentPosition);

            return new RollBudgetAnalysis(
                IsAllowed: isAllowed,
                RollDebit: rollDebit,
                MaxAllowedDebit: maxAllowedDebit,
                UtilizationPercent: utilizationPercent,
                ExcessDebit: excessDebit,
                Recommendation: recommendation,
                Reason: GenerateReasonString(isAllowed, utilizationPercent, excessDebit)
            );
        }

        private static RollRecommendation DetermineRollRecommendation(
            double utilizationPercent, 
            double excessDebit, 
            Position position)
        {
            if (excessDebit > 0)
                return RollRecommendation.Reject;

            if (utilizationPercent > 90)
                return RollRecommendation.Close;

            if (utilizationPercent > 75)
                return RollRecommendation.ConvertToDebit;

            if (utilizationPercent > 50)
                return RollRecommendation.AcceptWithCaution;

            return RollRecommendation.Accept;
        }

        private static string GenerateReasonString(bool isAllowed, double utilizationPercent, double excessDebit)
        {
            if (!isAllowed)
                return $"Roll denied: Exceeds budget by ${excessDebit:F2}";

            if (utilizationPercent > 90)
                return $"High budget utilization: {utilizationPercent:F1}% - consider closing";

            if (utilizationPercent > 75)
                return $"Moderate budget utilization: {utilizationPercent:F1}% - consider debit conversion";

            if (utilizationPercent > 50)
                return $"Acceptable utilization: {utilizationPercent:F1}% - proceed with caution";

            return $"Low utilization: {utilizationPercent:F1}% - roll approved";
        }

        public static RollBudgetTracker CreateTracker(double initialBudget, OilRiskGuardrails config)
        {
            return new RollBudgetTracker(initialBudget, config.RollDebitCapPctOfRisk);
        }

        public static bool ValidateMultipleRolls(
            List<(Position position, RollPlan roll)> proposedRolls, 
            OilRiskGuardrails config)
        {
            return proposedRolls.All(rollPair => 
                AllowRoll(rollPair.roll.Debit, rollPair.position.TicketRisk, config));
        }

        public static RollOptimizationResult OptimizeRollBudget(
            Position position, 
            List<RollPlan> rollOptions, 
            OilRiskGuardrails config)
        {
            var ticketRisk = position.TicketRisk;
            var maxBudget = ticketRisk * config.RollDebitCapPctOfRisk;

            var validRolls = rollOptions
                .Where(roll => roll.Debit <= maxBudget)
                .OrderBy(roll => roll.Debit)
                .ToList();

            if (!validRolls.Any())
            {
                return new RollOptimizationResult(
                    HasValidOption: false,
                    RecommendedRoll: null,
                    AlternativeAction: "Close position - no affordable roll options",
                    BudgetUtilization: 0
                );
            }

            var bestRoll = validRolls.First();
            var budgetUtilization = (bestRoll.Debit / maxBudget) * 100;

            return new RollOptimizationResult(
                HasValidOption: true,
                RecommendedRoll: bestRoll,
                AlternativeAction: budgetUtilization > 75 ? "Consider debit conversion" : "Proceed with roll",
                BudgetUtilization: budgetUtilization
            );
        }

        public static WeeklyRollBudgetSummary GenerateWeeklySummary(
            List<RollTransaction> weeklyRolls, 
            double totalWeeklyRisk,
            OilRiskGuardrails config)
        {
            var totalRollDebits = weeklyRolls.Sum(roll => roll.ActualDebit);
            var averageRollCost = weeklyRolls.Any() ? weeklyRolls.Average(roll => roll.ActualDebit) : 0;
            var maxSingleRoll = weeklyRolls.Any() ? weeklyRolls.Max(roll => roll.ActualDebit) : 0;
            
            var weeklyBudgetCap = totalWeeklyRisk * config.RollDebitCapPctOfRisk;
            var weeklyUtilization = weeklyBudgetCap > 0 ? (totalRollDebits / weeklyBudgetCap) * 100 : 0;

            var rollSuccessRate = weeklyRolls.Any() 
                ? (weeklyRolls.Count(roll => roll.WasSuccessful) / (double)weeklyRolls.Count) * 100
                : 0;

            return new WeeklyRollBudgetSummary(
                TotalRollDebits: totalRollDebits,
                AverageRollCost: averageRollCost,
                MaxSingleRoll: maxSingleRoll,
                WeeklyBudgetCap: weeklyBudgetCap,
                WeeklyUtilization: weeklyUtilization,
                RollCount: weeklyRolls.Count,
                RollSuccessRate: rollSuccessRate,
                Recommendation: GenerateWeeklyRecommendation(weeklyUtilization, rollSuccessRate)
            );
        }

        private static string GenerateWeeklyRecommendation(double utilization, double successRate)
        {
            if (utilization > 80 && successRate < 70)
                return "High cost, low success - review roll criteria";

            if (utilization > 60)
                return "Moderate roll budget usage - monitor closely";

            if (successRate > 80 && utilization < 40)
                return "Efficient roll management - continue current approach";

            return "Normal roll budget management";
        }
    }

    public enum RollRecommendation
    {
        Accept,
        AcceptWithCaution,
        ConvertToDebit,
        Close,
        Reject
    }

    public sealed record RollBudgetAnalysis(
        bool IsAllowed,
        double RollDebit,
        double MaxAllowedDebit,
        double UtilizationPercent,
        double ExcessDebit,
        RollRecommendation Recommendation,
        string Reason
    );

    public sealed record RollOptimizationResult(
        bool HasValidOption,
        RollPlan? RecommendedRoll,
        string AlternativeAction,
        double BudgetUtilization
    );

    public sealed record WeeklyRollBudgetSummary(
        double TotalRollDebits,
        double AverageRollCost,
        double MaxSingleRoll,
        double WeeklyBudgetCap,
        double WeeklyUtilization,
        int RollCount,
        double RollSuccessRate,
        string Recommendation
    );

    public sealed record RollTransaction(
        DateTime Timestamp,
        string PositionName,
        double ActualDebit,
        double BudgetAllowance,
        bool WasSuccessful,
        string Outcome
    );

    public class RollBudgetTracker
    {
        private readonly double _initialBudget;
        private readonly double _budgetCapPercent;
        private readonly List<RollTransaction> _transactions;
        private double _remainingBudget;

        public RollBudgetTracker(double initialBudget, double budgetCapPercent)
        {
            _initialBudget = initialBudget;
            _budgetCapPercent = budgetCapPercent;
            _transactions = new List<RollTransaction>();
            _remainingBudget = initialBudget * budgetCapPercent;
        }

        public bool CanAffordRoll(double rollDebit)
        {
            return rollDebit <= _remainingBudget;
        }

        public bool ExecuteRoll(RollTransaction transaction)
        {
            if (!CanAffordRoll(transaction.ActualDebit))
                return false;

            _remainingBudget -= transaction.ActualDebit;
            _transactions.Add(transaction);
            return true;
        }

        public double GetRemainingBudget() => _remainingBudget;

        public double GetUtilizationPercent()
        {
            var totalBudget = _initialBudget * _budgetCapPercent;
            var usedBudget = totalBudget - _remainingBudget;
            return totalBudget > 0 ? (usedBudget / totalBudget) * 100 : 0;
        }

        public List<RollTransaction> GetTransactionHistory() => _transactions.ToList();
    }
}