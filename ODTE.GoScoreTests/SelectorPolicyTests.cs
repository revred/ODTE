
using System;
using System.Text.Json;
using Xunit;
using ODTE.Strategy.GoScore;

public class SelectorPolicyTests
{
    [Fact]
    public void Policy_Loads_From_Json()
    {
        var p = GoPolicy.Load(@"C:\code\ODTE\ODTE.GoScoreTests\GoScore.policy.json");
        Assert.True(p.UseGoScore);
        Assert.True(p.Regime.icAllowed.Calm);
        Assert.False(p.Regime.icAllowed.Convex);
    }

    [Fact]
    public void Decision_Respects_Policy_Thresholds()
    {
        var p = GoPolicy.Load(@"C:\code\ODTE\ODTE.GoScoreTests\GoScore.policy.json");
        var scorer = new GoScorer(p);

        // Construct score just below half threshold via inputs
        var d1 = scorer.Decide(new GoInputs(0.50,0.50,0,0.60,0.60,0.5,0.3), StrategyKind.CreditBwb, Regime.Mixed);
        Assert.Equal(Decision.Skip, d1);

        // Above half but below full
        var d2 = scorer.Decide(new GoInputs(0.62,0.25,0.05,0.75,0.70,0.6,0.3), StrategyKind.CreditBwb, Regime.Mixed);
        Assert.Equal(Decision.Half, d2);
    }
}
