using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ODTE.Strategy.GoScore
{
    /// <summary>
    /// GoScore: Core trade selector that computes 0-100 probability score for profitable exits
    /// Integrates: PoE, PoT, Edge, Liquidity, Regime fit, Pin risk, RFib utilization
    /// Decision policy: Score ≥70 = Full, 55-69 = Half, <55 = Skip
    /// </summary>
    public sealed record GoInputs(
        double PoE, double PoT, double Edge, double LiqScore,
        double RegScore, double PinScore, double RfibUtil);

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
            return JsonSerializer.Deserialize<GoPolicy>(txt, new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
        }
    }

    public sealed record Weights(double wPoE=1.6, double wPoT=-1.0, double wEdge=0.9, double wLiq=0.6, double wReg=0.8, double wPin=0.3, double wRfib=-1.2);
    public sealed record Thresholds(double full=70.0, double half=55.0, double minLiqScore=0.5);
    public sealed record Rfib(double softStart=0.8, double warn=0.9, double block=1.0);
    public sealed record RegimeAllow(RegimeAllowed icAllowed, RegimeAllowed bwbAllowed) { public RegimeAllow():this(new(),new()){} }
    public sealed record RegimeAllowed(bool Calm=true,bool Mixed=true,bool Convex=false);
    public sealed record Pin(double alphaPoints=10.0);
    public sealed record Pot(double deltaMultiplier=2.0, double max=1.0);
    public sealed record Iv(double ivrMinIC=25.0, double ivrMinBWB=0.0);
    public sealed record Vix(double halfSize=30.0, double quarterSize=40.0);
    public sealed record Sizing(int contractsMin=1);
    public sealed record Liquidity(double maxSpreadMid=0.25);

    public enum StrategyKind { IronCondor, CreditBwb }
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
            const double a1 =  0.254829592;
            const double a2 = -0.284496736;
            const double a3 =  1.421413741;
            const double a4 = -1.453152027;
            const double a5 =  1.061405429;
            const double p  =  0.3275911;

            int sign = x < 0 ? -1 : 1;
            x = Math.Abs(x);

            double t = 1.0 / (1.0 + p * x);
            double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);

            return sign * y;
        }

        public static double Phi(double x) => 0.5 * (1.0 + Erf(x / Math.Sqrt(2.0)));
        public static double Z(double K, double S, double r, double q, double sigma, double T)
            => (Math.Log(K/S) - (r - q - 0.5*sigma*sigma)*T) / (sigma*Math.Sqrt(T));

        /// <summary>
        /// IC probability of expiring between short strikes using boundary vols
        /// </summary>
        public static double PoE_IC(double S, double r, double q, double T, double ivPut, double Kp, double ivCall, double Kc)
        {
            var sigma = 0.5*(ivPut + ivCall);
            if (sigma <= 0 || T <= 0) return 0;
            return Math.Max(0, Phi(Z(Kc,S,r,q,sigma,T)) - Phi(Z(Kp,S,r,q,sigma,T)));
        }

        /// <summary>
        /// BWB probability of expiring in profit tent (approximation)
        /// </summary>
        public static double PoE_BWB(double S, double r, double q, double T, double ivShort, double Kp, double Kc, double bodyK, double wingWidth)
        {
            // Approximate tent profit region using combination of inside shorts and near body
            var poInsideShorts = PoE_IC(S, r, q, T, ivShort, Kp, ivShort, Kc);
            var poNearBody = PoE_IC(S, r, q, T, ivShort, bodyK - wingWidth/2, ivShort, bodyK + wingWidth/2);
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