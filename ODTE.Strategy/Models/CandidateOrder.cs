using ODTE.Strategy.Interfaces;

namespace ODTE.Strategy.Models
{
    /// <summary>
    /// Record representing a candidate order with all risk metrics computed
    /// Used at order build-time to ensure MaxPotentialLoss and ROC are first-class
    /// </summary>
    public sealed record CandidateOrder(
        IStrategyShape Shape,
        decimal NetCredit,
        decimal MaxPotentialLoss,
        decimal Roc,                       // NetCredit / MaxPotentialLoss
        decimal RfibUtilization,           // (OpenRisk + MaxPotentialLoss) / DailyCap
        string Reason                      // classifier/gates transcript
    );
}