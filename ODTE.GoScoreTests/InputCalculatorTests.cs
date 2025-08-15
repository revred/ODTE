
using System;
using Xunit;
using ODTE.Strategy.GoScore;

public class InputCalculatorTests
{
    [Fact]
    public void PoE_IC_Increases_When_Range_Widens()
    {
        double S=100, r=0.00, q=0.00, T=1.0;
        double iv=0.20;
        var narrow = Calculators.PoE_IC(S,r,q,T,iv,95,iv,105);
        var wide   = Calculators.PoE_IC(S,r,q,T,iv,90,iv,110);
        Assert.True(wide > narrow);
    }

    [Fact]
    public void PoT_FromDelta_Clamped_And_Monotone()
    {
        Assert.Equal(0.0, Calculators.PoT_FromDelta(0));
        Assert.Equal(1.0, Calculators.PoT_FromDelta(1.0)); // 2*1 -> clamp to 1
        Assert.True(Calculators.PoT_FromDelta(0.30) < Calculators.PoT_FromDelta(0.40));
    }

    [Fact]
    public void Sigmoid_Symmetric()
    {
        var a = Calculators.Sigmoid(1.0);
        var b = Calculators.Sigmoid(-1.0);
        Assert.InRange(a + b, 0.99, 1.01);
    }
}
