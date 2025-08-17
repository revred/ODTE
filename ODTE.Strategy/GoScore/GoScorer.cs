using System.Text.Json;

namespace ODTE.Strategy.GoScore
{
    /// <summary>
    /// GoScore: Core trade selector that computes 0-100 probability score for profitable exits
    /// Integrates: PoE, PoT, Edge, Liquidity, Regime fit, Pin risk, RFib utilization
    /// Decision policy: Score ≥70 = Full, 55-69 = Half, <55 = Skip
    /// 
    /// ** ML/GENETIC ALGORITHM OPTIMIZATION TARGETS **
    /// This framework is designed for continuous ML/GA optimization where algorithms can:
    /// 1. Tune weight parameters (wPoE, wPoT, wEdge, wLiq, wReg, wPin, wRfib)
    /// 2. Adjust decision thresholds (full=70.0, half=55.0)
    /// 3. Optimize regime-specific parameters (VIX thresholds, RFib levels)
    /// 4. Calibrate component calculators (pin alpha, pot delta multiplier)
    /// 
    /// STRATEGY SELECTION IMPACT:
    /// - Higher PoE weight → Favors high probability trades (conservative)
    /// - Higher Edge weight → Favors trades with mathematical advantage
    /// - Negative PoT weight → Penalizes high tail risk scenarios  
    /// - RFib penalty → Prevents position sizing violations
    /// - Regime scoring → Adapts strategy selection to market conditions
    /// </summary>
    public sealed record GoInputs(
        // CORE PROFITABILITY METRICS (ML-tunable via weight optimization)
        double PoE,        // Probability of Expiring profitable [0.0-1.0] - GENETIC TARGET: wPoE weight
        double PoT,        // Probability of Tail event loss [0.0-1.0] - GENETIC TARGET: wPoT weight  
        double Edge,       // Expected profit edge [-1.0 to +1.0] - GENETIC TARGET: wEdge weight

        // EXECUTION QUALITY FACTORS (ML-tunable via scoring algorithms)
        double LiqScore,   // Liquidity quality score [0.0-1.0] - GENETIC TARGET: wLiq weight
        double RegScore,   // Regime fit score [0.0-1.0] - GENETIC TARGET: wReg weight
        double PinScore,   // Pin risk score [0.0-1.0] - GENETIC TARGET: wPin weight

        // RISK MANAGEMENT INTEGRATION (ML-tunable via penalty functions)
        double RfibUtil    // RFib utilization [0.0-1.0] - GENETIC TARGET: wRfib penalty strength
    );

    public sealed class GoPolicy
    {
        public string Version { get; init; } = "1.0";
        public bool UseGoScore { get; init; } = true;
        public Weights Weights { get; init; } = new();
        public Thresholds Thresholds { get; init; } = new();
        public Rfib Rfib { get; init; } = new();
        public RegimeAllow Regime { get; init; } = new();
        public Pin Pin { get; init; } = new();
        public Pot Pot { get; init; } = new();
        public Iv Iv { get; init; } = new();
        public Vix Vix { get; init; } = new();
        public Sizing Sizing { get; init; } = new();
        public Liquidity Liquidity { get; init; } = new();

        public static GoPolicy Load(string path)
        {
            var txt = System.IO.File.ReadAllText(path);
            return JsonSerializer.Deserialize<GoPolicy>(txt, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        }
    }

    /// <summary>
    /// GENETIC ALGORITHM OPTIMIZATION CORE: Weight vector for trade scoring
    /// These are the PRIMARY TARGETS for ML/GA optimization algorithms
    /// 
    /// OPTIMIZATION STRATEGY:
    /// - Use genetic algorithms to evolve weight combinations over 1000+ generations  
    /// - Fitness function: Sharpe ratio + win rate + drawdown minimization
    /// - Crossover: Weighted averaging of successful parameter sets
    /// - Mutation: ±10% random variations with 5% probability
    /// - Selection pressure: Top 20% performers breed for next generation
    /// 
    /// PARAMETER SENSITIVITY (for ML tuning):
    /// - wPoE: High sensitivity - controls conservative vs aggressive trade selection
    /// - wPoT: Critical for tail risk - negative values penalize dangerous scenarios
    /// - wEdge: Mathematical profit bias - should remain positive for profitability
    /// - wRfib: Position sizing safety - large negative values prevent overexposure
    /// </summary>
    public sealed record Weights(
        double wPoE = 1.6,     // Probability of Expiring profitable weight [GENETIC RANGE: 0.1-3.0]
        double wPoT = -1.0,    // Tail risk penalty weight [GENETIC RANGE: -3.0 to -0.1] 
        double wEdge = 0.9,    // Expected edge importance [GENETIC RANGE: 0.1-2.0]
        double wLiq = 0.6,     // Liquidity quality weight [GENETIC RANGE: 0.1-1.5]
        double wReg = 0.8,     // Regime fit weight [GENETIC RANGE: 0.1-1.5] 
        double wPin = 0.3,     // Pin risk weight [GENETIC RANGE: 0.0-1.0]
        double wRfib = -1.2    // RFib violation penalty [GENETIC RANGE: -3.0 to -0.5]
    );

    /// <summary>
    /// STRATEGY SELECTION DECISION BOUNDARIES - Key ML optimization targets
    /// These thresholds directly control trade execution frequency and risk
    /// 
    /// ML OPTIMIZATION APPROACH:
    /// - Grid search over threshold combinations (full: 60-80, half: 45-65)
    /// - Optimize for: maximize profit while maintaining <7% loss frequency
    /// - Adaptive thresholds: Higher in volatile markets, lower in calm periods
    /// - Reinforcement learning: Adjust based on recent performance feedback
    /// </summary>
    public sealed record Thresholds(
        double full = 70.0,        // Full position threshold [ML RANGE: 60-80] - Higher = more selective
        double half = 55.0,        // Half position threshold [ML RANGE: 45-65] - Gap controls sizing logic  
        double minLiqScore = 0.5   // Minimum liquidity requirement [ML RANGE: 0.3-0.8] - Market access filter
    );

    /// <summary>
    /// REVERSE FIBONACCI RISK INTEGRATION - ML-tunable position sizing controls
    /// These parameters integrate with the RFib risk management system and can be optimized
    /// for different market volatility regimes and portfolio risk targets
    /// </summary>
    public sealed record Rfib(
        double softStart = 0.8,  // Soft penalty start point [ML RANGE: 0.6-0.9] - When to begin size reduction
        double warn = 0.9,       // Warning threshold [ML RANGE: 0.8-0.95] - Caution zone entry
        double block = 1.0       // Hard block threshold [FIXED] - Absolute safety limit
    );
    /// <summary>
    /// REGIME-BASED STRATEGY SELECTION - Core ML optimization target for market adaptation
    /// Controls which strategies are allowed in different market volatility regimes
    /// 
    /// GENETIC ALGORITHM OPTIMIZATION:
    /// - Boolean flags can be optimized via discrete genetic algorithms
    /// - Fitness evaluation: backtest each regime combination over 20-year dataset
    /// - Strategy: Iron Condor (IC) vs Credit Broken Wing Butterfly (BWB) selection
    /// - Meta-optimization: Learn regime classification thresholds themselves
    /// </summary>
    public sealed record RegimeAllow(
        RegimeAllowed icAllowed,   // Iron Condor permissions per regime [ML TARGET: optimize regime mapping]
        RegimeAllowed bwbAllowed   // BWB permissions per regime [ML TARGET: optimize strategy selection]
    )
    { public RegimeAllow() : this(new(), new()) { } }

    /// <summary>
    /// REGIME-SPECIFIC STRATEGY PERMISSIONS - Direct ML/GA optimization targets
    /// These boolean flags control strategy availability and can be optimized via:
    /// - Binary genetic algorithms (0/1 encoding)
    /// - Reinforcement learning (reward profitable regime-strategy combinations)
    /// - Ensemble methods (combine multiple regime classifiers)
    /// </summary>
    public sealed record RegimeAllowed(
        bool Calm = true,     // Allow strategy in calm markets [ML TARGET: optimize via backtesting]
        bool Mixed = true,    // Allow strategy in mixed volatility [ML TARGET: performance-driven selection]
        bool Convex = false   // Allow strategy in high volatility [ML TARGET: risk-adjusted optimization]
    );

    /// <summary>
    /// PIN RISK CALCULATION PARAMETERS - ML-tunable for options expiry dynamics
    /// Pin risk occurs when underlying price gravitates toward strike prices near expiry
    /// </summary>
    public sealed record Pin(
        double alphaPoints = 10.0  // Pin influence radius in points [ML RANGE: 5.0-20.0] - Strike clustering effect
    );

    /// <summary>
    /// PROBABILITY OF TAIL (PoT) CALCULATION - Critical ML target for tail risk management  
    /// Controls how the system evaluates extreme loss scenarios in option strategies
    /// </summary>
    public sealed record Pot(
        double deltaMultiplier = 2.0,  // Delta sensitivity amplifier [ML RANGE: 1.0-4.0] - Tail event magnitude
        double max = 1.0               // Maximum PoT value cap [FIXED] - Probability ceiling
    );

    /// <summary>
    /// IMPLIED VOLATILITY RANK THRESHOLDS - Strategy selection filters based on IV environment
    /// These parameters control when strategies are enabled based on volatility percentiles
    /// </summary>
    public sealed record Iv(
        double ivrMinIC = 25.0,   // Min IV rank for Iron Condor [ML RANGE: 10-40] - Credit strategy threshold
        double ivrMinBWB = 0.0    // Min IV rank for BWB [ML RANGE: 0-20] - Always-available vs selective
    );

    /// <summary>
    /// VIX-BASED POSITION SIZING - Critical risk management parameters for ML optimization
    /// These thresholds control position sizing based on market fear/volatility levels
    /// 
    /// OPTIMIZATION APPROACH:
    /// - Backtest different VIX thresholds against historical volatility clusters
    /// - Optimize for: maximum return per unit of VIX risk
    /// - Adaptive sizing: ML models can learn regime-specific VIX sensitivity
    /// </summary>
    public sealed record Vix(
        double halfSize = 30.0,      // VIX level for 50% position sizing [ML RANGE: 25-35] - Caution threshold
        double quarterSize = 40.0    // VIX level for 25% position sizing [ML RANGE: 35-45] - High fear threshold
    );

    /// <summary>
    /// POSITION SIZING CONSTRAINTS - Minimum execution parameters
    /// </summary>
    public sealed record Sizing(
        int contractsMin = 1  // Minimum contracts per trade [FIXED] - Execution minimum
    );

    /// <summary>
    /// LIQUIDITY QUALITY FILTERS - Market microstructure optimization targets
    /// These parameters ensure trades only execute in liquid market conditions
    /// </summary>
    public sealed record Liquidity(
        double maxSpreadMid = 0.25  // Maximum bid-ask spread [ML RANGE: 0.15-0.40] - Transaction cost filter
    );

    public enum StrategyKind { IronCondor, CreditBwb }

    /// <summary>
    /// Strategy specification for GoScore validation
    /// </summary>
    public class StrategySpec
    {
        public StrategyKind Type { get; set; }
        public double CreditTarget { get; set; }
    }
    public enum Regime { Calm, Mixed, Convex }
    public enum Decision { Skip, Half, Full }

    /// <summary>
    /// Mathematical calculators for GoScore components
    /// </summary>
    public static class Calculators
    {
        /// <summary>
        /// Approximation of the error function for normal CDF calculation
        /// </summary>
        private static double Erf(double x)
        {
            // Abramowitz and Stegun approximation
            const double a1 = 0.254829592;
            const double a2 = -0.284496736;
            const double a3 = 1.421413741;
            const double a4 = -1.453152027;
            const double a5 = 1.061405429;
            const double p = 0.3275911;

            int sign = x < 0 ? -1 : 1;
            x = Math.Abs(x);

            double t = 1.0 / (1.0 + p * x);
            double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);

            return sign * y;
        }

        public static double Phi(double x) => 0.5 * (1.0 + Erf(x / Math.Sqrt(2.0)));
        public static double Z(double K, double S, double r, double q, double sigma, double T)
            => (Math.Log(K / S) - (r - q - 0.5 * sigma * sigma) * T) / (sigma * Math.Sqrt(T));

        /// <summary>
        /// IC probability of expiring between short strikes using boundary vols
        /// </summary>
        public static double PoE_IC(double S, double r, double q, double T, double ivPut, double Kp, double ivCall, double Kc)
        {
            var sigma = 0.5 * (ivPut + ivCall);
            if (sigma <= 0 || T <= 0) return 0;
            return Math.Max(0, Phi(Z(Kc, S, r, q, sigma, T)) - Phi(Z(Kp, S, r, q, sigma, T)));
        }

        /// <summary>
        /// BWB probability of expiring in profit tent (approximation)
        /// </summary>
        public static double PoE_BWB(double S, double r, double q, double T, double ivShort, double Kp, double Kc, double bodyK, double wingWidth)
        {
            // Approximate tent profit region using combination of inside shorts and near body
            var poInsideShorts = PoE_IC(S, r, q, T, ivShort, Kp, ivShort, Kc);
            var poNearBody = PoE_IC(S, r, q, T, ivShort, bodyK - wingWidth / 2, ivShort, bodyK + wingWidth / 2);
            return 0.5 * poInsideShorts + 0.5 * poNearBody;
        }

        /// <summary>
        /// Probability of Touch from absolute delta (ODTE heuristic)
        /// </summary>
        public static double PoT_FromDelta(double deltaAbs) => Math.Min(1.0, Math.Max(0.0, 2.0 * Math.Abs(deltaAbs)));

        /// <summary>
        /// Logistic sigmoid function for score normalization
        /// </summary>
        public static double Sigmoid(double z) => 1.0 / (1.0 + Math.Exp(-z));

        /// <summary>
        /// Calculate pricing edge: (NetCredit - ModelFair) / MPL
        /// </summary>
        public static double CalculateEdge(double netCredit, double modelFairValue, double maxPotentialLoss)
        {
            if (maxPotentialLoss <= 0) return 0;
            return (netCredit - modelFairValue) / maxPotentialLoss;
        }

        /// <summary>
        /// Calculate liquidity score from bid-ask spread and market quality
        /// </summary>
        public static double CalculateLiqScore(double bid, double ask, double openInterest, bool isHealthyQuote = true)
        {
            if (!isHealthyQuote || bid <= 0 || ask <= bid) return 0;

            var mid = 0.5 * (bid + ask);
            var spread = ask - bid;
            var spreadScore = 1.0 - Math.Min(1.0, spread / mid);

            // Boost for decent open interest (proxy for depth)
            var oiBoost = openInterest >= 100 ? 1.1 : (openInterest >= 50 ? 1.05 : 1.0);

            return Math.Min(1.0, spreadScore * oiBoost);
        }

        /// <summary>
        /// Calculate regime score based on strategy fit to current market conditions
        /// </summary>
        public static double CalculateRegScore(StrategyKind strategy, Regime regime, double ivr, double vix)
        {
            // Base regime fit scores
            var baseScore = (strategy, regime) switch
            {
                (StrategyKind.IronCondor, Regime.Calm) => 0.8,
                (StrategyKind.IronCondor, Regime.Mixed) => 0.6,
                (StrategyKind.IronCondor, Regime.Convex) => 0.0, // Blocked
                (StrategyKind.CreditBwb, Regime.Calm) => 1.0,
                (StrategyKind.CreditBwb, Regime.Mixed) => 0.8,
                (StrategyKind.CreditBwb, Regime.Convex) => 0.6,
                _ => 0.5
            };

            // Adjust for IVR and VIX levels
            var ivrAdjustment = strategy == StrategyKind.IronCondor ?
                Math.Max(0, (ivr - 25) / 75) : // IC needs higher IVR
                Math.Max(0, ivr / 100); // BWB more flexible

            var vixAdjustment = vix > 40 ? 0.8 : (vix > 30 ? 0.9 : 1.0);

            return Math.Min(1.0, baseScore * (0.7 + 0.3 * ivrAdjustment) * vixAdjustment);
        }

        /// <summary>
        /// Calculate pin score based on distance from gamma walls/max pain
        /// </summary>
        public static double CalculatePinScore(double currentPrice, double nearestGammaWall, double alphaPoints = 10.0)
        {
            var distance = Math.Abs(currentPrice - nearestGammaWall);
            return Math.Exp(-distance / alphaPoints);
        }
    }

    /// <summary>
    /// Core GoScore calculator and decision engine
    /// </summary>
    public sealed class GoScorer
    {
        readonly GoPolicy _policy;

        public GoScorer(GoPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>
        /// Compute GoScore (0-100) from input components
        /// </summary>
        public double Compute(GoInputs inputs)
        {
            var w = _policy.Weights;
            var rfibPenalty = Math.Max(0.0, inputs.RfibUtil - _policy.Rfib.softStart);

            var z = w.wPoE * inputs.PoE +
                   w.wPoT * inputs.PoT +
                   w.wEdge * inputs.Edge +
                   w.wLiq * inputs.LiqScore +
                   w.wReg * inputs.RegScore +
                   w.wPin * inputs.PinScore +
                   w.wRfib * rfibPenalty;

            return 100.0 * Calculators.Sigmoid(z);
        }

        /// <summary>
        /// Make trading decision based on GoScore and policy gates
        /// </summary>
        public Decision Decide(GoInputs inputs, StrategyKind strategy, Regime regime)
        {
            // Hard blocks first
            if (inputs.RfibUtil >= _policy.Rfib.block)
                return Decision.Skip;

            if (regime == Regime.Convex && strategy == StrategyKind.IronCondor)
                return Decision.Skip;

            if (inputs.LiqScore < _policy.Thresholds.minLiqScore)
                return Decision.Skip;

            // Compute score and apply thresholds
            var score = Compute(inputs);

            if (score >= _policy.Thresholds.full)
                return Decision.Full;
            if (score >= _policy.Thresholds.half)
                return Decision.Half;

            return Decision.Skip;
        }

        /// <summary>
        /// Get detailed scoring breakdown for audit/debugging
        /// </summary>
        public GoScoreBreakdown GetBreakdown(GoInputs inputs, StrategyKind strategy, Regime regime)
        {
            var w = _policy.Weights;
            var rfibPenalty = Math.Max(0.0, inputs.RfibUtil - _policy.Rfib.softStart);

            var components = new Dictionary<string, double>
            {
                ["PoE"] = w.wPoE * inputs.PoE,
                ["PoT"] = w.wPoT * inputs.PoT,
                ["Edge"] = w.wEdge * inputs.Edge,
                ["LiqScore"] = w.wLiq * inputs.LiqScore,
                ["RegScore"] = w.wReg * inputs.RegScore,
                ["PinScore"] = w.wPin * inputs.PinScore,
                ["RfibPenalty"] = w.wRfib * rfibPenalty
            };

            var totalZ = components.Values.Sum();
            var finalScore = 100.0 * Calculators.Sigmoid(totalZ);
            var decision = Decide(inputs, strategy, regime);

            return new GoScoreBreakdown(finalScore, decision, components, inputs, strategy, regime);
        }
    }

    /// <summary>
    /// Detailed breakdown of GoScore calculation for auditing
    /// </summary>
    public sealed record GoScoreBreakdown(
        double FinalScore,
        Decision Decision,
        Dictionary<string, double> Components,
        GoInputs Inputs,
        StrategyKind Strategy,
        Regime Regime)
    {
        public string GetAuditSummary()
        {
            var componentSummary = string.Join(", ", Components.Select(kv => $"{kv.Key}:{kv.Value:F2}"));
            return $"GoScore={FinalScore:F1} Decision={Decision} Strategy={Strategy} Regime={Regime} [{componentSummary}]";
        }
    }

    /// <summary>
    /// Ledger record for storing GoScore decisions and audit trail
    /// </summary>
    public sealed record GoScoreLedgerRecord(
        DateTimeOffset Time,
        StrategyKind Strategy,
        Regime Regime,
        double GoScore,
        Decision Decision,
        double PoE, double PoT, double Edge, double LiqScore,
        double RegScore, double PinScore, double RfibUtil,
        string EvidenceJson,
        string ReasonCodes)
    {
        public static GoScoreLedgerRecord FromBreakdown(GoScoreBreakdown breakdown, string evidenceJson = "", string reasonCodes = "")
        {
            var inputs = breakdown.Inputs;
            return new GoScoreLedgerRecord(
                DateTimeOffset.UtcNow,
                breakdown.Strategy,
                breakdown.Regime,
                breakdown.FinalScore,
                breakdown.Decision,
                inputs.PoE, inputs.PoT, inputs.Edge, inputs.LiqScore,
                inputs.RegScore, inputs.PinScore, inputs.RfibUtil,
                evidenceJson,
                reasonCodes
            );
        }
    }
}