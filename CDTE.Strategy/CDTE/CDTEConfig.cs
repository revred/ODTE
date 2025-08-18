using YamlDotNet.Serialization;

namespace CDTE.Strategy.CDTE;

/// <summary>
/// CDTE Weekly Engine Configuration
/// Supports YAML configuration for all strategy parameters per spec
/// </summary>
public class CDTEConfig
{
    [YamlMember(Alias = "monday_decision_et")]
    public TimeOnly MondayDecisionET { get; set; } = new(10, 0, 0);
    
    [YamlMember(Alias = "wednesday_decision_et")]
    public TimeOnly WednesdayDecisionET { get; set; } = new(12, 30, 0);
    
    [YamlMember(Alias = "exit_cutoff_buffer_min")]
    public int ExitCutoffBufferMin { get; set; } = 20;
    
    [YamlMember(Alias = "risk_cap_usd")]
    public decimal RiskCapUsd { get; set; } = 800m;
    
    [YamlMember(Alias = "take_profit_core_pct")]
    public double TakeProfitCorePct { get; set; } = 0.70;
    
    [YamlMember(Alias = "max_drawdown_pct")]
    public double MaxDrawdownPct { get; set; } = 0.50;
    
    [YamlMember(Alias = "neutral_band_pct")]
    public double NeutralBandPct { get; set; } = 0.15;
    
    [YamlMember(Alias = "roll_debit_cap_pct_of_risk")]
    public double RollDebitCapPctOfRisk { get; set; } = 0.25;
    
    [YamlMember(Alias = "delta_targets")]
    public DeltaTargets DeltaTargets { get; set; } = new();
    
    [YamlMember(Alias = "regime_bands_iv")]
    public RegimeBands RegimeBandsIV { get; set; } = new();
    
    [YamlMember(Alias = "fill_policy")]
    public FillPolicy FillPolicy { get; set; } = new();
}

public class DeltaTargets
{
    [YamlMember(Alias = "ic_short_abs")]
    public double IcShortAbs { get; set; } = 0.18;
    
    [YamlMember(Alias = "vert_short_abs")]
    public double VertShortAbs { get; set; } = 0.25;
    
    [YamlMember(Alias = "bwb_body_put")]
    public double BwbBodyPut { get; set; } = -0.30;
    
    [YamlMember(Alias = "bwb_near_put")]
    public double BwbNearPut { get; set; } = -0.15;
}

public class RegimeBands
{
    [YamlMember(Alias = "low")]
    public double Low { get; set; } = 15.0;
    
    [YamlMember(Alias = "high")]
    public double High { get; set; } = 22.0;
}

public class FillPolicy
{
    [YamlMember(Alias = "type")]
    public string Type { get; set; } = "marketable_limit";
    
    [YamlMember(Alias = "window_sec")]
    public int WindowSec { get; set; } = 30;
    
    [YamlMember(Alias = "max_adverse_tick")]
    public int MaxAdverseTick { get; set; } = 1;
    
    [YamlMember(Alias = "aggressiveness_steps")]
    public double[] AggressivenessSteps { get; set; } = { 0.25, 0.40, 0.50 };
}

/// <summary>
/// Market regime classification for strategy selection
/// </summary>
public enum MarketRegime
{
    LowIV,     // Front IV < 15
    MidIV,     // 15-22 IV range  
    HighIV     // > 22 IV or event proximity
}

/// <summary>
/// Strategy structures supported by CDTE engine
/// </summary>
public enum CDTEStructure
{
    BrokenWingButterfly,  // Low IV regime
    IronCondor,           // Mid IV regime
    IronFly,              // High IV regime
    CreditVertical        // Directional lean
}