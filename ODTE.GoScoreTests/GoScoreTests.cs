
using System;
using Xunit;
using ODTE.Strategy.GoScore;

public class GoScoreTests
{
    [Fact]
    public void Sigmoid_BasicMonotonicity()
    {
        Assert.True(Calculators.Sigmoid(-10) < Calculators.Sigmoid(-1));
        Assert.True(Calculators.Sigmoid( 10) > Calculators.Sigmoid( 1));
        Assert.InRange(Calculators.Sigmoid(0), 0.49, 0.51);
    }

    [Fact]
    public void GoScore_WeightsMoveScoreInExpectedDirections()
    {
        var p = GoPolicy.Load(@"C:\code\ODTE\ODTE.GoScoreTests\GoScore.policy.json");
        var scorer = new GoScorer(p);
        var baseInputs = new GoInputs(PoE:0.50, PoT:0.30, Edge:0.00, LiqScore:0.80, RegScore:0.80, PinScore:0.50, RfibUtil:0.50);
        var s1 = scorer.Compute(baseInputs);

        // Better PoE should increase score
        var s2 = scorer.Compute(baseInputs with { PoE = 0.60 });
        Assert.True(s2 > s1);

        // Higher PoT (touch risk) should reduce score
        var s3 = scorer.Compute(baseInputs with { PoT = 0.60 });
        Assert.True(s3 < s1);

        // Higher RFib utilization close to block should reduce score
        var s4 = scorer.Compute(baseInputs with { RfibUtil = 0.95 });
        Assert.True(s4 < s1);
    }

    [Fact]
    public void Decision_Tiers_Work()
    {
        var p = GoPolicy.Load(@"C:\code\ODTE\ODTE.GoScoreTests\GoScore.policy.json");
        var scorer = new GoScorer(p);

        // Force high score
        var full = scorer.Decide(
            new GoInputs( PoE:0.8, PoT:0.10, Edge:0.20, LiqScore:0.95, RegScore:0.9, PinScore:0.8, RfibUtil:0.2 ),
            StrategyKind.CreditBwb, Regime.Calm);
        Assert.Equal(Decision.Full, full);

        // Borderline half
        var half = scorer.Decide(
            new GoInputs( PoE:0.5, PoT:0.35, Edge:0.02, LiqScore:0.60, RegScore:0.6, PinScore:0.5, RfibUtil:0.4 ),
            StrategyKind.CreditBwb, Regime.Mixed);
        Assert.Equal(Decision.Half, half);

        // Skip due to RFib
        var skip1 = scorer.Decide(
            new GoInputs( PoE:0.9, PoT:0.05, Edge:0.3, LiqScore:0.99, RegScore:1.0, PinScore:0.9, RfibUtil:1.05 ),
            StrategyKind.CreditBwb, Regime.Calm);
        Assert.Equal(Decision.Skip, skip1);

        // Skip IC in Convex
        var skip2 = scorer.Decide(
            new GoInputs( PoE:0.7, PoT:0.10, Edge:0.10, LiqScore:0.90, RegScore:0.6, PinScore:0.5, RfibUtil:0.4 ),
            StrategyKind.IronCondor, Regime.Convex);
        Assert.Equal(Decision.Skip, skip2);

        // Skip on liquidity
        var skip3 = scorer.Decide(
            new GoInputs( PoE:0.7, PoT:0.10, Edge:0.10, LiqScore:0.10, RegScore:0.6, PinScore:0.5, RfibUtil:0.2 ),
            StrategyKind.CreditBwb, Regime.Calm);
        Assert.Equal(Decision.Skip, skip3);
    }
}
