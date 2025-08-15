using System;
using Xunit;
using ODTE.Strategy.GoScore;

public class GoScoreDiagnostics
{
    [Fact]
    public void Diagnose_Current_GoScore_Values()
    {
        var p = GoPolicy.Load(@"C:\code\ODTE\ODTE.GoScoreTests\GoScore.policy.json");
        var scorer = new GoScorer(p);

        // Test cases from failing tests
        var inputs1 = new GoInputs(0.50,0.50,0,0.60,0.60,0.5,0.3);
        var breakdown1 = scorer.GetBreakdown(inputs1, StrategyKind.CreditBwb, Regime.Mixed);
        
        var inputs2 = new GoInputs(0.62,0.25,0.05,0.75,0.70,0.6,0.3);
        var breakdown2 = scorer.GetBreakdown(inputs2, StrategyKind.CreditBwb, Regime.Mixed);
        
        var inputs3 = new GoInputs(0.6, 0.25, 0.05, 0.80, 0.7, 0.6, 0.3);
        var breakdown3 = scorer.GetBreakdown(inputs3, StrategyKind.CreditBwb, Regime.Mixed);

        Console.WriteLine("GoScore Diagnostics:");
        Console.WriteLine($"Test case 1 (expect Skip <55): {breakdown1.GetAuditSummary()}");
        Console.WriteLine($"Test case 2 (expect Half 55-70): {breakdown2.GetAuditSummary()}");
        Console.WriteLine($"Test case 3 (from other test): {breakdown3.GetAuditSummary()}");
        
        Console.WriteLine($"Policy thresholds: Full={p.Thresholds.full}, Half={p.Thresholds.half}");
        
        // Should fail to make visible
        Assert.True(breakdown1.FinalScore < 55, $"Expected score <55, got {breakdown1.FinalScore}");
    }
}