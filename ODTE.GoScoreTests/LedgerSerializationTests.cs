
using System;
using System.Text.Json;
using Xunit;
using ODTE.Strategy.GoScore;

public class LedgerSerializationTests
{
    [Fact]
    public void LedgerRecord_Serializes_With_GoScore_Fields()
    {
        var rec = new GoScoreLedgerRecord(
            Time: DateTimeOffset.UtcNow,
            Strategy: StrategyKind.CreditBwb,
            Regime: Regime.Calm,
            GoScore: 72.3,
            Decision: Decision.Full,
            PoE: 0.65, PoT: 0.25, Edge: 0.08, LiqScore: 0.9, RegScore: 0.8, PinScore: 0.6, RfibUtil: 0.4,
            EvidenceJson: "{\"ivr\":35,\"vix\":22}",
            ReasonCodes: "HIGH_POE,GOOD_LIQUIDITY"
        );
        var json = JsonSerializer.Serialize(rec);
        Assert.Contains("GoScore", json);
        Assert.Contains("EvidenceJson", json);
    }
}
