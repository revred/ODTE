namespace ODTE.Optimization
{
    /// <summary>
    /// Convex Tail Overlay system for converting worst vol/trend buckets into home runs
    /// Activates during high risk periods to add bounded risk, unbounded profit potential
    /// Target: Transform 1x to ~3x total returns over 5 years
    /// </summary>
    public class ConvexTailOverlay
    {
        public class TailOverlayConditions
        {
            public double VIX { get; set; }
            public double RealizedVol5Day { get; set; }
            public double ImpliedVol { get; set; }
            public double TrendScore { get; set; } // |T| where T in [-1, 1]
            public double MarketBreadth { get; set; } // % one-sided
            public bool PostEventExpansion { get; set; }
            public string MarketRegime { get; set; } = "";
        }

        public class TailExtenderStructure
        {
            public string Type { get; set; } = ""; // "TailExtender" or "RatioBackspread"
            public string RiskSide { get; set; } = ""; // "Put" or "Call" 
            public double BaseCreditUsed { get; set; } // 10-25% of BWB credit
            public double ExtenderStrike { get; set; } // Deep OTM beyond far wing
            public int ExtenderContracts { get; set; } // 1-2 extra longs
            public double NetDebit { get; set; } // ≤$0.50 target
            public double MaxLoss { get; set; } // Bounded (unchanged or slightly higher)
            public bool HasUnboundedProfit { get; set; } = true;
        }

        public class RatioBackspreadStructure
        {
            public string ThreatSide { get; set; } = ""; // Direction of expected move
            public double ShortStrike { get; set; } // ~25Δ
            public double LongStrike { get; set; } // ~10-12Δ  
            public int ShortContracts { get; set; } = 1;
            public int LongContracts { get; set; } = 2;
            public double NetCredit { get; set; } // Target small credit or ≤$0.25 debit
            public double MaxLoss { get; set; } // Bounded between strikes
            public bool HasUnlimitedConvexity { get; set; } = true;
        }

        public class ConvexOverlayResult
        {
            public bool OverlayActivated { get; set; }
            public string OverlayType { get; set; } = ""; // "None", "TailExtender", "RatioBackspread"
            public double BasePnL { get; set; } // BWB/IC P&L
            public double OverlayPnL { get; set; } // Tail overlay P&L
            public double TotalPnL { get; set; } // Combined P&L
            public string ActivationReason { get; set; } = "";
            public TailExtenderStructure? TailExtender { get; set; }
            public RatioBackspreadStructure? RatioBackspread { get; set; }
        }

        private readonly Random _random;

        public ConvexTailOverlay(Random random)
        {
            _random = random;
        }

        /// <summary>
        /// Evaluate if convex tail overlay should be activated based on market conditions
        /// </summary>
        public bool ShouldActivateOverlay(TailOverlayConditions conditions)
        {
            // 2a) Tail Extender Conditions: VIX ≥ 25 or 5-day realized > implied or trend score |T| ≥ 0.6
            if (conditions.VIX >= 25 ||
                conditions.RealizedVol5Day > conditions.ImpliedVol ||
                Math.Abs(conditions.TrendScore) >= 0.6)
            {
                return true;
            }

            // 2b) Ratio Backspread Conditions: VIX ≥ 30, breadth > 70% one-sided, or post-event expansion
            if (conditions.VIX >= 30 ||
                conditions.MarketBreadth > 0.70 ||
                conditions.PostEventExpansion)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determine which overlay type to use based on market conditions
        /// </summary>
        public string DetermineOverlayType(TailOverlayConditions conditions)
        {
            // Ratio Backspread for extreme conditions
            if (conditions.VIX >= 30 || conditions.MarketBreadth > 0.70 || conditions.PostEventExpansion)
            {
                return "RatioBackspread";
            }

            // Tail Extender for moderate high-risk conditions
            if (conditions.VIX >= 25 ||
                conditions.RealizedVol5Day > conditions.ImpliedVol ||
                Math.Abs(conditions.TrendScore) >= 0.6)
            {
                return "TailExtender";
            }

            return "None";
        }

        /// <summary>
        /// Apply convex tail overlay to existing BWB/IC position
        /// </summary>
        public ConvexOverlayResult ApplyConvexOverlay(
            double basePnL,
            CreditBWBEngine.BWBStructure baseStructure,
            TailOverlayConditions conditions,
            double marketMove) // Simulated market movement for testing
        {
            var result = new ConvexOverlayResult
            {
                BasePnL = basePnL,
                OverlayPnL = 0,
                TotalPnL = basePnL
            };

            if (!ShouldActivateOverlay(conditions))
            {
                result.OverlayType = "None";
                return result;
            }

            result.OverlayActivated = true;
            result.OverlayType = DetermineOverlayType(conditions);

            switch (result.OverlayType)
            {
                case "TailExtender":
                    result = ApplyTailExtender(result, baseStructure, conditions, marketMove);
                    break;

                case "RatioBackspread":
                    result = ApplyRatioBackspread(result, conditions, marketMove);
                    break;
            }

            result.TotalPnL = result.BasePnL + result.OverlayPnL;
            return result;
        }

        /// <summary>
        /// Apply Tail Extender: BWB + 1-2 deep OTM longs beyond far wing
        /// Finance with 10-25% of credit, keep net credit or ≤$0.50 debit
        /// </summary>
        private ConvexOverlayResult ApplyTailExtender(
            ConvexOverlayResult result,
            CreditBWBEngine.BWBStructure baseStructure,
            TailOverlayConditions conditions,
            double marketMove)
        {
            var tailExtender = new TailExtenderStructure();

            // Determine risk side based on trend/vol
            tailExtender.RiskSide = DetermineRiskSide(conditions);
            tailExtender.Type = "TailExtender";

            // Use 10-25% of BWB credit to finance tail
            var creditAvailable = baseStructure.NetCredit * 0.175; // Mid-point 17.5%
            tailExtender.BaseCreditUsed = creditAvailable;

            // Add 1-2 deep OTM longs beyond existing wing
            tailExtender.ExtenderContracts = _random.NextDouble() < 0.7 ? 1 : 2; // 70% chance of 1 contract

            // Calculate extender strike (deep OTM beyond far wing)
            var strikeSpacing = 25; // Typical SPX spacing
            if (tailExtender.RiskSide == "Put")
            {
                tailExtender.ExtenderStrike = baseStructure.LongStrike1 - (strikeSpacing * 2); // 2 strikes deeper
            }
            else
            {
                tailExtender.ExtenderStrike = baseStructure.LongStrike1 + (strikeSpacing * 2);
            }

            // Net debit should be ≤$0.50 (cost of longs minus credit used)
            var longCost = CalculateDeepOTMCost(tailExtender.ExtenderStrike, conditions.VIX, tailExtender.ExtenderContracts);
            tailExtender.NetDebit = Math.Max(0, longCost - creditAvailable);

            // Max loss is bounded (unchanged or slightly higher)
            tailExtender.MaxLoss = baseStructure.MaxLoss + tailExtender.NetDebit;

            // Simulate tail extender P&L
            result.OverlayPnL = SimulateTailExtenderPnL(tailExtender, conditions, marketMove);
            result.TailExtender = tailExtender;
            result.ActivationReason = $"Tail Extender: VIX={conditions.VIX:F1}, Trend={conditions.TrendScore:F2}";

            return result;
        }

        /// <summary>
        /// Apply Ratio Backspread: Sell 1 @ ~25Δ, Buy 2 @ ~10-12Δ on threat side
        /// Target ≤$0.25 debit or small credit
        /// </summary>
        private ConvexOverlayResult ApplyRatioBackspread(
            ConvexOverlayResult result,
            TailOverlayConditions conditions,
            double marketMove)
        {
            var ratioBackspread = new RatioBackspreadStructure();

            // Determine threat side
            ratioBackspread.ThreatSide = DetermineThreatSide(conditions);

            // Set up ratio structure: Sell 1 @ ~25Δ, Buy 2 @ ~10-12Δ
            ratioBackspread.ShortContracts = 1;
            ratioBackspread.LongContracts = 2;

            // Simulate strikes based on deltas
            var spotPrice = 4800; // Approximate XSP level
            if (ratioBackspread.ThreatSide == "Put")
            {
                ratioBackspread.ShortStrike = spotPrice - 100; // ~25Δ put
                ratioBackspread.LongStrike = spotPrice - 200;  // ~10-12Δ put
            }
            else
            {
                ratioBackspread.ShortStrike = spotPrice + 100; // ~25Δ call
                ratioBackspread.LongStrike = spotPrice + 200;  // ~10-12Δ call
            }

            // Calculate net credit/debit (target ≤$0.25 debit or small credit)
            ratioBackspread.NetCredit = CalculateRatioBackspreadCredit(ratioBackspread, conditions.VIX);

            // Max loss is bounded between strikes
            var strikeWidth = Math.Abs(ratioBackspread.LongStrike - ratioBackspread.ShortStrike);
            ratioBackspread.MaxLoss = strikeWidth - ratioBackspread.NetCredit;

            // Simulate ratio backspread P&L
            result.OverlayPnL = SimulateRatioBackspreadPnL(ratioBackspread, conditions, marketMove);
            result.RatioBackspread = ratioBackspread;
            result.ActivationReason = $"Ratio Backspread: VIX={conditions.VIX:F1}, Breadth={conditions.MarketBreadth:P0}";

            return result;
        }

        /// <summary>
        /// Determine risk side based on market conditions
        /// </summary>
        private string DetermineRiskSide(TailOverlayConditions conditions)
        {
            // If trending down, put risk is higher
            if (conditions.TrendScore < -0.3)
                return "Put";

            // If trending up, call risk is higher  
            if (conditions.TrendScore > 0.3)
                return "Call";

            // For high vol without trend, use volatility regime
            return conditions.MarketRegime == "Volatile" && _random.NextDouble() < 0.6 ? "Put" : "Call";
        }

        /// <summary>
        /// Determine threat side for ratio backspread
        /// </summary>
        private string DetermineThreatSide(TailOverlayConditions conditions)
        {
            // Similar logic but more aggressive for extreme conditions
            if (conditions.TrendScore < -0.5 || conditions.MarketBreadth > 0.70)
                return "Put"; // Downside threat

            if (conditions.TrendScore > 0.5)
                return "Call"; // Upside threat

            // For extreme vol, slight bearish bias
            return _random.NextDouble() < 0.65 ? "Put" : "Call";
        }

        /// <summary>
        /// Calculate cost of deep OTM options for tail extender
        /// </summary>
        private double CalculateDeepOTMCost(double strike, double vix, int contracts)
        {
            // Simplified deep OTM pricing - very low cost but some premium for vol
            var baseCost = 0.50; // Base $0.50 per contract for deep OTM
            var volMultiplier = 1.0 + (vix - 20) * 0.02; // Higher vol = higher cost

            return baseCost * volMultiplier * contracts;
        }

        /// <summary>
        /// Calculate net credit for ratio backspread
        /// </summary>
        private double CalculateRatioBackspreadCredit(RatioBackspreadStructure structure, double vix)
        {
            // Sell 1 at 25Δ, Buy 2 at 10-12Δ
            // Typically small credit or small debit
            var shortCredit = 3.0 + vix * 0.1; // Higher VIX = higher short premium
            var longCost = (1.0 + vix * 0.05) * 2; // Cost of 2 long contracts

            var netCredit = shortCredit - longCost;

            // Ensure it's within target range (≤$0.25 debit or small credit)
            return Math.Max(-0.25, Math.Min(1.0, netCredit));
        }

        /// <summary>
        /// Simulate tail extender P&L based on market movement
        /// Key: Bounded loss but unbounded profit beyond the tail
        /// </summary>
        private double SimulateTailExtenderPnL(
            TailExtenderStructure extender,
            TailOverlayConditions conditions,
            double marketMove)
        {
            var spotPrice = 4800; // Base price
            var finalPrice = spotPrice * (1 + marketMove);

            // Tail extender P&L calculation
            var pnl = 0.0;

            if (extender.RiskSide == "Put" && finalPrice < extender.ExtenderStrike)
            {
                // Deep ITM puts - unbounded profit potential
                var intrinsicValue = Math.Max(0, extender.ExtenderStrike - finalPrice);
                pnl = intrinsicValue * extender.ExtenderContracts - extender.NetDebit;

                // Cap simulation at reasonable level (10x move)
                pnl = Math.Min(pnl, extender.ExtenderStrike * 0.1 * extender.ExtenderContracts);
            }
            else if (extender.RiskSide == "Call" && finalPrice > extender.ExtenderStrike)
            {
                // Deep ITM calls - unbounded profit potential  
                var intrinsicValue = Math.Max(0, finalPrice - extender.ExtenderStrike);
                pnl = intrinsicValue * extender.ExtenderContracts - extender.NetDebit;

                // Cap simulation at reasonable level
                pnl = Math.Min(pnl, finalPrice * 0.1 * extender.ExtenderContracts);
            }
            else
            {
                // Options expire worthless - lose the net debit
                pnl = -extender.NetDebit;
            }

            return pnl;
        }

        /// <summary>
        /// Simulate ratio backspread P&L
        /// Key: Bounded max loss, unlimited convex payoff beyond long strike
        /// </summary>
        private double SimulateRatioBackspreadPnL(
            RatioBackspreadStructure backspread,
            TailOverlayConditions conditions,
            double marketMove)
        {
            var spotPrice = 4800;
            var finalPrice = spotPrice * (1 + marketMove);

            var pnl = backspread.NetCredit; // Start with net credit

            if (backspread.ThreatSide == "Put")
            {
                // Short 1 put @ 25Δ, Long 2 puts @ 10-12Δ
                var shortPutPnL = -Math.Max(0, backspread.ShortStrike - finalPrice);
                var longPutPnL = 2 * Math.Max(0, backspread.LongStrike - finalPrice);

                pnl += shortPutPnL + longPutPnL;
            }
            else
            {
                // Short 1 call @ 25Δ, Long 2 calls @ 10-12Δ  
                var shortCallPnL = -Math.Max(0, finalPrice - backspread.ShortStrike);
                var longCallPnL = 2 * Math.Max(0, finalPrice - backspread.LongStrike);

                pnl += shortCallPnL + longCallPnL;
            }

            // Apply max loss constraint
            pnl = Math.Max(-backspread.MaxLoss, pnl);

            return pnl;
        }

        /// <summary>
        /// Generate market conditions for overlay testing
        /// </summary>
        public static TailOverlayConditions GenerateMarketConditions(string regime, Random random)
        {
            var conditions = new TailOverlayConditions { MarketRegime = regime };

            switch (regime)
            {
                case "Volatile":
                    conditions.VIX = 25 + random.NextDouble() * 30; // 25-55
                    conditions.RealizedVol5Day = conditions.VIX * (0.9 + random.NextDouble() * 0.3); // Often > implied
                    conditions.ImpliedVol = conditions.VIX * 0.95;
                    conditions.TrendScore = (random.NextDouble() - 0.5) * 1.6; // -0.8 to +0.8
                    conditions.MarketBreadth = 0.3 + random.NextDouble() * 0.5; // 30-80%
                    conditions.PostEventExpansion = random.NextDouble() < 0.3; // 30% chance
                    break;

                case "Trending":
                    conditions.VIX = 18 + random.NextDouble() * 20; // 18-38
                    conditions.RealizedVol5Day = conditions.VIX * 0.8;
                    conditions.ImpliedVol = conditions.VIX;
                    conditions.TrendScore = (random.NextDouble() < 0.5 ? -1 : 1) * (0.4 + random.NextDouble() * 0.6); // Strong trends
                    conditions.MarketBreadth = 0.5 + random.NextDouble() * 0.3; // 50-80%
                    conditions.PostEventExpansion = false;
                    break;

                case "Calm":
                default:
                    conditions.VIX = 12 + random.NextDouble() * 15; // 12-27
                    conditions.RealizedVol5Day = conditions.VIX * 0.9;
                    conditions.ImpliedVol = conditions.VIX * 1.05;
                    conditions.TrendScore = (random.NextDouble() - 0.5) * 0.8; // -0.4 to +0.4
                    conditions.MarketBreadth = 0.4 + random.NextDouble() * 0.3; // 40-70%
                    conditions.PostEventExpansion = false;
                    break;
            }

            return conditions;
        }
    }
}