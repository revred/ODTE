using System.ComponentModel.DataAnnotations;

namespace ODTE.Strategy.CDTE.Oil
{
    public sealed class OilCDTEConfig
    {
        [Required]
        public TimeOnly MondayDecisionEt { get; set; } = new(10, 0, 0);

        [Required]
        public TimeOnly WednesdayDecisionEt { get; set; } = new(12, 30, 0);

        [Required]
        [Range(30, 120)]
        public int ExitCutoffBufferMin { get; set; } = 45;

        [Required]
        [Range(100, 2000)]
        public double RiskCapUsd { get; set; } = 800;

        [Required]
        [Range(1, 20)]
        public double WeeklyCapPct { get; set; } = 6;

        [Required]
        [Range(0.5, 0.9)]
        public double TakeProfitCorePct { get; set; } = 0.70;

        [Required]
        [Range(0.3, 0.7)]
        public double MaxDrawdownPct { get; set; } = 0.50;

        [Required]
        [Range(0.1, 0.3)]
        public double NeutralBandPct { get; set; } = 0.15;

        [Required]
        [Range(0.15, 0.35)]
        public double RollDebitCapPctOfRisk { get; set; } = 0.25;

        [Required]
        [Range(20, 50)]
        public double IvHighThresholdPct { get; set; } = 30;

        [Required]
        public WingRuleConfig WidthRule { get; set; } = new();

        [Required]
        public DeltaTargetsConfig DeltaTargets { get; set; } = new();

        [Required]
        public FillPolicyConfig FillPolicy { get; set; } = new();

        [Required]
        public OilRiskGuardrails Risk { get; set; } = new();

        public bool IsHighIv(double impliedVolatility) =>
            impliedVolatility > (IvHighThresholdPct / 100.0);
    }

    public sealed class WingRuleConfig
    {
        [Required]
        [Range(1.0, 5.0)]
        public double PerDayUsd { get; set; } = 2.0;

        [Required]
        [Range(0.25, 1.0)]
        public double ZeroDteUsd { get; set; } = 0.5;
    }

    public sealed class DeltaTargetsConfig
    {
        [Required]
        [Range(0.10, 0.25)]
        public double IcShortAbs { get; set; } = 0.18;

        [Required]
        [Range(0.15, 0.35)]
        public double VertShortAbs { get; set; } = 0.25;
    }

    public sealed class FillPolicyConfig
    {
        [Required]
        public string Type { get; set; } = "marketable_limit";

        [Required]
        [Range(15, 60)]
        public int WindowSec { get; set; } = 30;

        [Required]
        [Range(1, 3)]
        public int MaxAdverseTick { get; set; } = 1;

        [Required]
        public double[] AggressivenessSteps { get; set; } = new[] { 0.25, 0.40, 0.50 };
    }

    public sealed class OilRiskGuardrails
    {
        [Required]
        [Range(0.05, 0.25)]
        public double PinBandUsd { get; set; } = 0.10;

        [Required]
        [Range(0.25, 0.40)]
        public double DeltaGuardAbs { get; set; } = 0.30;

        [Required]
        [Range(1000, 5000)]
        public double GammaMaxUsdPer1 { get; set; } = 2500;

        [Required]
        [Range(0.15, 0.35)]
        public double RollDebitCapPctOfRisk { get; set; } = 0.25;

        [Required]
        [Range(30, 120)]
        public int ExitBufferMin { get; set; } = 45;

        [Required]
        [Range(0.25, 0.40)]
        public double DeltaItmGuard { get; set; } = 0.30;

        [Required]
        [Range(0.01, 0.05)]
        public double ExtrinsicMin { get; set; } = 0.02;

        [Required]
        public EventGuardConfig EventGuard { get; set; } = new();
    }

    public sealed class EventGuardConfig
    {
        [Required]
        public bool Enable { get; set; } = true;

        [Required]
        [Range(1, 5)]
        public int EiaOpecWithinTMinusDays { get; set; } = 2;

        [Required]
        public bool PreferIronFly { get; set; } = true;

        [Required]
        public bool EarlyTakeProfit { get; set; } = true;
    }

    public enum GuardAction
    {
        None,
        Close,
        RollOutAndAway,
        ConvertToDebitVertical,
        ReduceSize
    }

    public sealed record ActionPlan(
        GuardAction Action,
        string Reason,
        object? Payload = null
    );

    public sealed record PlannedOrders(
        PositionPlan[] Plans
    );

    public sealed record DecisionPlan(
        GuardAction Action,
        string Reason,
        object? Payload
    );

    public sealed record ExitReport(
        bool Success,
        string Reason,
        PlannedOrder[] Orders,
        double FinalPnL
    );

    public sealed record PositionPlan(
        string Name,
        IronCondor Structure,
        DateTime Expiry
    )
    {
        public double MaxLoss { get; init; }

        public PositionPlan WithReducedSize(double factor) =>
            this with { MaxLoss = MaxLoss * factor };

        public PositionPlan WithClosed() =>
            this with { MaxLoss = 0 };

        public PositionPlan WithConvertedToDebit() =>
            this with { Structure = Structure.ToDebitVertical() };
    }

    public sealed record RollPlan(
        IronCondor NewStructure,
        DateTime NewExpiry,
        double Debit
    );

    public sealed record PlannedOrder(
        OrderType Type,
        OptionLeg[] Legs,
        DateTime Timestamp,
        string Description
    )
    {
        public double SizeMultiplier { get; init; } = 1.0;
    }

    public enum OrderType
    {
        MarketableLimit,
        MarketClose,
        LimitOrder
    }

    public sealed record IronCondor(
        double ShortCall,
        double LongCall,
        double ShortPut,
        double LongPut
    )
    {
        public OptionLeg[] Legs => new[]
        {
            new OptionLeg(ShortCall, OptionRight.Call, -1),
            new OptionLeg(LongCall, OptionRight.Call, 1),
            new OptionLeg(ShortPut, OptionRight.Put, -1),
            new OptionLeg(LongPut, OptionRight.Put, 1)
        };

        public IronCondor ToDebitVertical() =>
            new(ShortCall, LongCall, 0, 0);
    }

    public sealed record OptionLeg(
        double Strike,
        OptionRight Right,
        int Quantity
    );

    public enum OptionRight
    {
        Call,
        Put
    }

    public class ChainSnapshot
    {
        public DateTime Timestamp { get; set; }
        public double UnderlyingPrice { get; set; }
        public ProductCalendar Calendar { get; set; } = new();

        public double GetAtmImpliedVolatility() => 0.25;
        public Func<double, double> GetNearestStrike => strike => Math.Round(strike * 2) / 2;
        public bool HasZeroDteOptions() => true;
    }

    public class PortfolioState
    {
        public GreeksAggregate Greeks { get; set; } = new();
        public Position[] GetAllPositions() => Array.Empty<Position>();
        public Position? GetPosition(string name) => null;
        public double GetTotalPnL() => 0;
    }

    public class GreeksAggregate
    {
        public double Gamma { get; set; }
        public double Delta { get; set; }
    }

    public class Position
    {
        public string Name { get; set; } = "";
        public OptionLeg[] Legs { get; set; } = Array.Empty<OptionLeg>();
        public double TicketRisk { get; set; }
        public double GetProfitPercentage() => 0;
    }

    public class ProductCalendar
    {
        public DateTime GetSessionClose(DateTime date) => date.Date.AddHours(16);
        public bool IsEarlyClose(DateTime date) => false;
    }
}