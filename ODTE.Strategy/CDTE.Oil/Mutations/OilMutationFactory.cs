namespace ODTE.Strategy.CDTE.Oil.Mutations
{
    /// <summary>
    /// OIL01-OIL64 Mutation Factory
    /// Generates 64 radical strategy variants for weekly Oil CDTE trading
    /// Constraint: All activity within Mon-Fri, decisions concentrated on 2 days max
    /// </summary>
    public class OilMutationFactory
    {
        public class OilStrategyVariant
        {
            public string VariantId { get; set; }
            public string Category { get; set; }
            public Dictionary<string, object> Parameters { get; set; }
            public string Description { get; set; }

            // Performance metrics after backtest
            public double? AnnualReturn { get; set; }
            public double? SharpeRatio { get; set; }
            public double? MaxDrawdown { get; set; }
            public double? WinRate { get; set; }
            public int? TotalTrades { get; set; }
        }

        public static List<OilStrategyVariant> GenerateAll64Variants()
        {
            var variants = new List<OilStrategyVariant>();

            // OIL01-OIL16: Entry Timing Mutations
            variants.AddRange(GenerateEntryTimingMutations());

            // OIL17-OIL32: Strike Selection Mutations  
            variants.AddRange(GenerateStrikeSelectionMutations());

            // OIL33-OIL48: Risk Management Mutations
            variants.AddRange(GenerateRiskManagementMutations());

            // OIL49-OIL64: Exit Strategy Mutations
            variants.AddRange(GenerateExitStrategyMutations());

            return variants;
        }

        private static List<OilStrategyVariant> GenerateEntryTimingMutations()
        {
            var variants = new List<OilStrategyVariant>();

            // OIL01-OIL04: Monday Morning Entry Variants
            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL01",
                Category = "EntryTiming",
                Description = "Monday 9:35 AM Aggressive Entry - First 5min candle",
                Parameters = new Dictionary<string, object>
                {
                    ["EntryDay"] = DayOfWeek.Monday,
                    ["EntryTime"] = "09:35",
                    ["EntryStyle"] = "Aggressive",
                    ["RequiredVolume"] = 1000,
                    ["MaxSpread"] = 0.10,
                    ["DeltaTarget"] = 0.15,
                    ["DecisionDays"] = "Mon,Thu"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL02",
                Category = "EntryTiming",
                Description = "Monday 10:00 AM Post-Open Entry - After initial volatility",
                Parameters = new Dictionary<string, object>
                {
                    ["EntryDay"] = DayOfWeek.Monday,
                    ["EntryTime"] = "10:00",
                    ["EntryStyle"] = "Conservative",
                    ["RequiredVolume"] = 2000,
                    ["MaxSpread"] = 0.08,
                    ["DeltaTarget"] = 0.20,
                    ["DecisionDays"] = "Mon,Fri"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL03",
                Category = "EntryTiming",
                Description = "Monday 2:00 PM Afternoon Entry - Post-lunch stability",
                Parameters = new Dictionary<string, object>
                {
                    ["EntryDay"] = DayOfWeek.Monday,
                    ["EntryTime"] = "14:00",
                    ["EntryStyle"] = "Balanced",
                    ["RequiredVolume"] = 1500,
                    ["MaxSpread"] = 0.09,
                    ["DeltaTarget"] = 0.18,
                    ["DecisionDays"] = "Mon,Wed"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL04",
                Category = "EntryTiming",
                Description = "Monday 3:30 PM Late Entry - Power hour momentum",
                Parameters = new Dictionary<string, object>
                {
                    ["EntryDay"] = DayOfWeek.Monday,
                    ["EntryTime"] = "15:30",
                    ["EntryStyle"] = "Momentum",
                    ["RequiredVolume"] = 3000,
                    ["MaxSpread"] = 0.12,
                    ["DeltaTarget"] = 0.25,
                    ["DecisionDays"] = "Mon,Thu"
                }
            });

            // OIL05-OIL08: Tuesday Entry Variants
            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL05",
                Category = "EntryTiming",
                Description = "Tuesday 9:35 AM API Report Day Entry",
                Parameters = new Dictionary<string, object>
                {
                    ["EntryDay"] = DayOfWeek.Tuesday,
                    ["EntryTime"] = "09:35",
                    ["EntryStyle"] = "Volatility",
                    ["RequiredVolume"] = 2500,
                    ["MaxSpread"] = 0.15,
                    ["DeltaTarget"] = 0.12,
                    ["APIAware"] = true,
                    ["DecisionDays"] = "Tue,Fri"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL06",
                Category = "EntryTiming",
                Description = "Tuesday 11:00 AM Post-API Settlement",
                Parameters = new Dictionary<string, object>
                {
                    ["EntryDay"] = DayOfWeek.Tuesday,
                    ["EntryTime"] = "11:00",
                    ["EntryStyle"] = "PostEvent",
                    ["RequiredVolume"] = 2000,
                    ["MaxSpread"] = 0.10,
                    ["DeltaTarget"] = 0.22,
                    ["APIAware"] = true,
                    ["DecisionDays"] = "Tue,Thu"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL07",
                Category = "EntryTiming",
                Description = "Tuesday 1:00 PM Midday Stability Entry",
                Parameters = new Dictionary<string, object>
                {
                    ["EntryDay"] = DayOfWeek.Tuesday,
                    ["EntryTime"] = "13:00",
                    ["EntryStyle"] = "Range",
                    ["RequiredVolume"] = 1800,
                    ["MaxSpread"] = 0.08,
                    ["DeltaTarget"] = 0.19,
                    ["DecisionDays"] = "Tue,Fri"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL08",
                Category = "EntryTiming",
                Description = "Tuesday 3:00 PM Pre-Close Positioning",
                Parameters = new Dictionary<string, object>
                {
                    ["EntryDay"] = DayOfWeek.Tuesday,
                    ["EntryTime"] = "15:00",
                    ["EntryStyle"] = "Closing",
                    ["RequiredVolume"] = 2200,
                    ["MaxSpread"] = 0.11,
                    ["DeltaTarget"] = 0.17,
                    ["DecisionDays"] = "Tue,Thu"
                }
            });

            // OIL09-OIL12: Wednesday Entry Variants (EIA Day)
            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL09",
                Category = "EntryTiming",
                Description = "Wednesday 9:30 AM Pre-EIA Positioning",
                Parameters = new Dictionary<string, object>
                {
                    ["EntryDay"] = DayOfWeek.Wednesday,
                    ["EntryTime"] = "09:30",
                    ["EntryStyle"] = "PreEvent",
                    ["RequiredVolume"] = 3000,
                    ["MaxSpread"] = 0.20,
                    ["DeltaTarget"] = 0.10,
                    ["EIAAware"] = true,
                    ["DecisionDays"] = "Wed,Fri"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL10",
                Category = "EntryTiming",
                Description = "Wednesday 10:35 AM Post-EIA Volatility Capture",
                Parameters = new Dictionary<string, object>
                {
                    ["EntryDay"] = DayOfWeek.Wednesday,
                    ["EntryTime"] = "10:35",
                    ["EntryStyle"] = "VolCapture",
                    ["RequiredVolume"] = 4000,
                    ["MaxSpread"] = 0.25,
                    ["DeltaTarget"] = 0.08,
                    ["EIAAware"] = true,
                    ["DecisionDays"] = "Wed,Thu"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL11",
                Category = "EntryTiming",
                Description = "Wednesday 12:00 PM Post-EIA Settlement",
                Parameters = new Dictionary<string, object>
                {
                    ["EntryDay"] = DayOfWeek.Wednesday,
                    ["EntryTime"] = "12:00",
                    ["EntryStyle"] = "Settlement",
                    ["RequiredVolume"] = 2500,
                    ["MaxSpread"] = 0.12,
                    ["DeltaTarget"] = 0.16,
                    ["EIAAware"] = true,
                    ["DecisionDays"] = "Wed,Fri"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL12",
                Category = "EntryTiming",
                Description = "Wednesday 2:30 PM Late Week Entry",
                Parameters = new Dictionary<string, object>
                {
                    ["EntryDay"] = DayOfWeek.Wednesday,
                    ["EntryTime"] = "14:30",
                    ["EntryStyle"] = "LateWeek",
                    ["RequiredVolume"] = 2000,
                    ["MaxSpread"] = 0.10,
                    ["DeltaTarget"] = 0.20,
                    ["DecisionDays"] = "Wed,Thu"
                }
            });

            // OIL13-OIL16: Multi-Day Entry Variants
            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL13",
                Category = "EntryTiming",
                Description = "Monday+Wednesday Split Entry - Diversified timing",
                Parameters = new Dictionary<string, object>
                {
                    ["EntryDays"] = "Mon,Wed",
                    ["EntryTimes"] = "10:00,10:35",
                    ["EntryStyle"] = "Split",
                    ["SplitRatio"] = "50:50",
                    ["RequiredVolume"] = 1500,
                    ["MaxSpread"] = 0.12,
                    ["DeltaTarget"] = 0.18,
                    ["DecisionDays"] = "Wed,Fri"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL14",
                Category = "EntryTiming",
                Description = "Tuesday+Thursday Ladder Entry - Scale in approach",
                Parameters = new Dictionary<string, object>
                {
                    ["EntryDays"] = "Tue,Thu",
                    ["EntryTimes"] = "11:00,10:00",
                    ["EntryStyle"] = "Ladder",
                    ["SplitRatio"] = "60:40",
                    ["RequiredVolume"] = 2000,
                    ["MaxSpread"] = 0.10,
                    ["DeltaTarget"] = 0.22,
                    ["DecisionDays"] = "Thu,Fri"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL15",
                Category = "EntryTiming",
                Description = "Monday Morning + Friday Adjustment - Start and refine",
                Parameters = new Dictionary<string, object>
                {
                    ["EntryDays"] = "Mon",
                    ["EntryTime"] = "09:35",
                    ["AdjustmentDay"] = "Fri",
                    ["AdjustmentTime"] = "10:00",
                    ["EntryStyle"] = "Adjustable",
                    ["RequiredVolume"] = 2500,
                    ["MaxSpread"] = 0.15,
                    ["DeltaTarget"] = 0.15,
                    ["DecisionDays"] = "Mon,Fri"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL16",
                Category = "EntryTiming",
                Description = "Opportunistic Entry - Any day with signal trigger",
                Parameters = new Dictionary<string, object>
                {
                    ["EntryDays"] = "Any",
                    ["EntryWindow"] = "09:35-11:00",
                    ["EntryStyle"] = "Signal",
                    ["SignalTrigger"] = "ContangoFlip",
                    ["RequiredVolume"] = 3000,
                    ["MaxSpread"] = 0.18,
                    ["DeltaTarget"] = 0.14,
                    ["DecisionDays"] = "Thu,Fri"
                }
            });

            return variants;
        }

        private static List<OilStrategyVariant> GenerateStrikeSelectionMutations()
        {
            var variants = new List<OilStrategyVariant>();

            // OIL17-OIL20: Delta-Based Strike Selection
            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL17",
                Category = "StrikeSelection",
                Description = "Ultra-Low Delta 5-10 - Maximum OTM",
                Parameters = new Dictionary<string, object>
                {
                    ["StrikeMethod"] = "Delta",
                    ["ShortDelta"] = 0.07,
                    ["LongDelta"] = 0.03,
                    ["SpreadWidth"] = 2.0,
                    ["SkewAdjust"] = false,
                    ["EntryDay"] = DayOfWeek.Monday,
                    ["DecisionDays"] = "Mon,Thu"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL18",
                Category = "StrikeSelection",
                Description = "Low Delta 10-15 - Far OTM safety",
                Parameters = new Dictionary<string, object>
                {
                    ["StrikeMethod"] = "Delta",
                    ["ShortDelta"] = 0.12,
                    ["LongDelta"] = 0.06,
                    ["SpreadWidth"] = 1.5,
                    ["SkewAdjust"] = true,
                    ["EntryDay"] = DayOfWeek.Monday,
                    ["DecisionDays"] = "Mon,Fri"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL19",
                Category = "StrikeSelection",
                Description = "Standard Delta 15-20 - Balanced risk/reward",
                Parameters = new Dictionary<string, object>
                {
                    ["StrikeMethod"] = "Delta",
                    ["ShortDelta"] = 0.18,
                    ["LongDelta"] = 0.10,
                    ["SpreadWidth"] = 1.0,
                    ["SkewAdjust"] = true,
                    ["EntryDay"] = DayOfWeek.Tuesday,
                    ["DecisionDays"] = "Tue,Thu"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL20",
                Category = "StrikeSelection",
                Description = "High Delta 20-30 - Aggressive premium",
                Parameters = new Dictionary<string, object>
                {
                    ["StrikeMethod"] = "Delta",
                    ["ShortDelta"] = 0.25,
                    ["LongDelta"] = 0.15,
                    ["SpreadWidth"] = 0.75,
                    ["SkewAdjust"] = false,
                    ["EntryDay"] = DayOfWeek.Wednesday,
                    ["DecisionDays"] = "Wed,Fri"
                }
            });

            // OIL21-OIL24: Width-Based Strike Selection
            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL21",
                Category = "StrikeSelection",
                Description = "Narrow $0.50 Spreads - Minimum risk",
                Parameters = new Dictionary<string, object>
                {
                    ["StrikeMethod"] = "Width",
                    ["SpreadWidth"] = 0.50,
                    ["TargetDelta"] = 0.15,
                    ["MaxContracts"] = 20,
                    ["EntryDay"] = DayOfWeek.Monday,
                    ["DecisionDays"] = "Mon,Wed"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL22",
                Category = "StrikeSelection",
                Description = "Standard $1.00 Spreads - Balanced approach",
                Parameters = new Dictionary<string, object>
                {
                    ["StrikeMethod"] = "Width",
                    ["SpreadWidth"] = 1.00,
                    ["TargetDelta"] = 0.18,
                    ["MaxContracts"] = 15,
                    ["EntryDay"] = DayOfWeek.Monday,
                    ["DecisionDays"] = "Mon,Thu"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL23",
                Category = "StrikeSelection",
                Description = "Wide $2.00 Spreads - Higher premium",
                Parameters = new Dictionary<string, object>
                {
                    ["StrikeMethod"] = "Width",
                    ["SpreadWidth"] = 2.00,
                    ["TargetDelta"] = 0.20,
                    ["MaxContracts"] = 10,
                    ["EntryDay"] = DayOfWeek.Tuesday,
                    ["DecisionDays"] = "Tue,Fri"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL24",
                Category = "StrikeSelection",
                Description = "Ultra-Wide $3.00 Spreads - Maximum premium",
                Parameters = new Dictionary<string, object>
                {
                    ["StrikeMethod"] = "Width",
                    ["SpreadWidth"] = 3.00,
                    ["TargetDelta"] = 0.22,
                    ["MaxContracts"] = 7,
                    ["EntryDay"] = DayOfWeek.Wednesday,
                    ["DecisionDays"] = "Wed,Thu"
                }
            });

            // OIL25-OIL28: Volatility-Based Strike Selection
            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL25",
                Category = "StrikeSelection",
                Description = "IV Rank Based - High IV far strikes",
                Parameters = new Dictionary<string, object>
                {
                    ["StrikeMethod"] = "IVRank",
                    ["HighIVDelta"] = 0.10,
                    ["LowIVDelta"] = 0.25,
                    ["IVThreshold"] = 50,
                    ["SpreadWidth"] = 1.5,
                    ["EntryDay"] = DayOfWeek.Monday,
                    ["DecisionDays"] = "Mon,Fri"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL26",
                Category = "StrikeSelection",
                Description = "Skew-Adjusted Strikes - Follow put/call skew",
                Parameters = new Dictionary<string, object>
                {
                    ["StrikeMethod"] = "Skew",
                    ["BaseDeltas"] = 0.15,
                    ["SkewMultiplier"] = 1.5,
                    ["MaxSkewAdjust"] = 0.05,
                    ["SpreadWidth"] = 1.0,
                    ["EntryDay"] = DayOfWeek.Tuesday,
                    ["DecisionDays"] = "Tue,Thu"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL27",
                Category = "StrikeSelection",
                Description = "Term Structure Strikes - Contango/backwardation aware",
                Parameters = new Dictionary<string, object>
                {
                    ["StrikeMethod"] = "TermStructure",
                    ["ContangoDelta"] = 0.12,
                    ["BackwardationDelta"] = 0.20,
                    ["NeutralDelta"] = 0.16,
                    ["SpreadWidth"] = 1.25,
                    ["EntryDay"] = DayOfWeek.Wednesday,
                    ["DecisionDays"] = "Wed,Fri"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL28",
                Category = "StrikeSelection",
                Description = "Historical Vol Strikes - HV vs IV arbitrage",
                Parameters = new Dictionary<string, object>
                {
                    ["StrikeMethod"] = "HV_IV",
                    ["HVPeriod"] = 20,
                    ["IVPremiumDelta"] = 0.10,
                    ["IVDiscountDelta"] = 0.22,
                    ["SpreadWidth"] = 1.5,
                    ["EntryDay"] = DayOfWeek.Monday,
                    ["DecisionDays"] = "Mon,Thu"
                }
            });

            // OIL29-OIL32: Advanced Strike Selection
            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL29",
                Category = "StrikeSelection",
                Description = "Support/Resistance Strikes - Technical levels",
                Parameters = new Dictionary<string, object>
                {
                    ["StrikeMethod"] = "Technical",
                    ["StrikeAtSupport"] = true,
                    ["StrikeAtResistance"] = true,
                    ["BufferPercent"] = 0.5,
                    ["FallbackDelta"] = 0.15,
                    ["SpreadWidth"] = 1.0,
                    ["EntryDay"] = DayOfWeek.Monday,
                    ["DecisionDays"] = "Mon,Fri"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL30",
                Category = "StrikeSelection",
                Description = "Open Interest Strikes - Follow the crowd",
                Parameters = new Dictionary<string, object>
                {
                    ["StrikeMethod"] = "OpenInterest",
                    ["MinOI"] = 1000,
                    ["PreferHighOI"] = true,
                    ["TargetDelta"] = 0.18,
                    ["SpreadWidth"] = 1.5,
                    ["EntryDay"] = DayOfWeek.Tuesday,
                    ["DecisionDays"] = "Tue,Thu"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL31",
                Category = "StrikeSelection",
                Description = "Pin Risk Aware - Avoid pin strikes on Friday",
                Parameters = new Dictionary<string, object>
                {
                    ["StrikeMethod"] = "PinAware",
                    ["AvoidPinStrikes"] = true,
                    ["PinBuffer"] = 1.0,
                    ["BaseDelta"] = 0.16,
                    ["SpreadWidth"] = 1.25,
                    ["EntryDay"] = DayOfWeek.Wednesday,
                    ["DecisionDays"] = "Wed,Thu"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL32",
                Category = "StrikeSelection",
                Description = "Dynamic Width - Adjust based on volatility",
                Parameters = new Dictionary<string, object>
                {
                    ["StrikeMethod"] = "DynamicWidth",
                    ["BaseWidth"] = 1.0,
                    ["VolMultiplier"] = 0.1,
                    ["MinWidth"] = 0.5,
                    ["MaxWidth"] = 3.0,
                    ["TargetDelta"] = 0.17,
                    ["EntryDay"] = DayOfWeek.Monday,
                    ["DecisionDays"] = "Mon,Fri"
                }
            });

            return variants;
        }

        private static List<OilStrategyVariant> GenerateRiskManagementMutations()
        {
            var variants = new List<OilStrategyVariant>();

            // OIL33-OIL36: Stop Loss Variants
            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL33",
                Category = "RiskManagement",
                Description = "Tight Stop 50% - Quick exit on adverse moves",
                Parameters = new Dictionary<string, object>
                {
                    ["RiskStyle"] = "TightStop",
                    ["StopLossPercent"] = 50,
                    ["StopTrigger"] = "CreditBased",
                    ["TrailingStop"] = false,
                    ["ShortDelta"] = 0.15,
                    ["EntryDay"] = DayOfWeek.Monday,
                    ["DecisionDays"] = "Mon,Wed,Fri"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL34",
                Category = "RiskManagement",
                Description = "Standard Stop 100% - 2x credit stop",
                Parameters = new Dictionary<string, object>
                {
                    ["RiskStyle"] = "StandardStop",
                    ["StopLossPercent"] = 100,
                    ["StopTrigger"] = "CreditBased",
                    ["TrailingStop"] = true,
                    ["TrailStart"] = 50,
                    ["ShortDelta"] = 0.18,
                    ["EntryDay"] = DayOfWeek.Monday,
                    ["DecisionDays"] = "Mon,Thu"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL35",
                Category = "RiskManagement",
                Description = "Wide Stop 200% - Let winners run",
                Parameters = new Dictionary<string, object>
                {
                    ["RiskStyle"] = "WideStop",
                    ["StopLossPercent"] = 200,
                    ["StopTrigger"] = "CreditBased",
                    ["TrailingStop"] = true,
                    ["TrailStart"] = 75,
                    ["ShortDelta"] = 0.12,
                    ["EntryDay"] = DayOfWeek.Tuesday,
                    ["DecisionDays"] = "Tue,Fri"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL36",
                Category = "RiskManagement",
                Description = "No Stop - Defined risk only",
                Parameters = new Dictionary<string, object>
                {
                    ["RiskStyle"] = "NoStop",
                    ["StopLossPercent"] = 999,
                    ["MaxLossManagement"] = "SpreadWidth",
                    ["ShortDelta"] = 0.10,
                    ["SpreadWidth"] = 1.0,
                    ["EntryDay"] = DayOfWeek.Wednesday,
                    ["DecisionDays"] = "Wed,Fri"
                }
            });

            // OIL37-OIL40: Delta Management Variants
            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL37",
                Category = "RiskManagement",
                Description = "Delta Hedge 30 - Maintain delta neutrality",
                Parameters = new Dictionary<string, object>
                {
                    ["RiskStyle"] = "DeltaHedge",
                    ["MaxShortDelta"] = 30,
                    ["RebalanceThreshold"] = 5,
                    ["HedgeInstrument"] = "Futures",
                    ["ShortDelta"] = 0.20,
                    ["EntryDay"] = DayOfWeek.Monday,
                    ["DecisionDays"] = "Daily"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL38",
                Category = "RiskManagement",
                Description = "Delta Roll 25 - Roll when delta exceeds threshold",
                Parameters = new Dictionary<string, object>
                {
                    ["RiskStyle"] = "DeltaRoll",
                    ["RollDeltaTrigger"] = 0.25,
                    ["RollToTargetDelta"] = 0.15,
                    ["MaxRolls"] = 2,
                    ["ShortDelta"] = 0.15,
                    ["EntryDay"] = DayOfWeek.Monday,
                    ["DecisionDays"] = "Mon,Wed,Fri"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL39",
                Category = "RiskManagement",
                Description = "Delta Close 35 - Exit at high delta",
                Parameters = new Dictionary<string, object>
                {
                    ["RiskStyle"] = "DeltaClose",
                    ["CloseDeltaTrigger"] = 0.35,
                    ["PartialClosePercent"] = 50,
                    ["ShortDelta"] = 0.18,
                    ["EntryDay"] = DayOfWeek.Tuesday,
                    ["DecisionDays"] = "Tue,Thu"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL40",
                Category = "RiskManagement",
                Description = "Gamma Brake - Reduce size on gamma spike",
                Parameters = new Dictionary<string, object>
                {
                    ["RiskStyle"] = "GammaBrake",
                    ["MaxGamma"] = 0.05,
                    ["GammaReductionPercent"] = 30,
                    ["ShortDelta"] = 0.16,
                    ["EntryDay"] = DayOfWeek.Wednesday,
                    ["DecisionDays"] = "Wed,Fri"
                }
            });

            // OIL41-OIL44: Profit Management Variants
            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL41",
                Category = "RiskManagement",
                Description = "Quick Profit 25% - Take profits early",
                Parameters = new Dictionary<string, object>
                {
                    ["RiskStyle"] = "QuickProfit",
                    ["ProfitTarget"] = 25,
                    ["PartialProfitPercent"] = 100,
                    ["ShortDelta"] = 0.20,
                    ["EntryDay"] = DayOfWeek.Monday,
                    ["DecisionDays"] = "Mon,Thu"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL42",
                Category = "RiskManagement",
                Description = "Standard Profit 50% - Half max profit target",
                Parameters = new Dictionary<string, object>
                {
                    ["RiskStyle"] = "StandardProfit",
                    ["ProfitTarget"] = 50,
                    ["PartialProfitPercent"] = 50,
                    ["TrailingProfit"] = true,
                    ["ShortDelta"] = 0.18,
                    ["EntryDay"] = DayOfWeek.Monday,
                    ["DecisionDays"] = "Mon,Fri"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL43",
                Category = "RiskManagement",
                Description = "High Profit 75% - Hold for larger gains",
                Parameters = new Dictionary<string, object>
                {
                    ["RiskStyle"] = "HighProfit",
                    ["ProfitTarget"] = 75,
                    ["PartialProfitPercent"] = 0,
                    ["ShortDelta"] = 0.15,
                    ["EntryDay"] = DayOfWeek.Tuesday,
                    ["DecisionDays"] = "Tue,Fri"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL44",
                Category = "RiskManagement",
                Description = "Expiry Hold - No profit target, hold to expiry",
                Parameters = new Dictionary<string, object>
                {
                    ["RiskStyle"] = "ExpiryHold",
                    ["ProfitTarget"] = 999,
                    ["CloseBeforeExpiry"] = false,
                    ["ShortDelta"] = 0.12,
                    ["EntryDay"] = DayOfWeek.Wednesday,
                    ["DecisionDays"] = "Wed"
                }
            });

            // OIL45-OIL48: Advanced Risk Variants
            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL45",
                Category = "RiskManagement",
                Description = "Vega Hedge - Protect against IV changes",
                Parameters = new Dictionary<string, object>
                {
                    ["RiskStyle"] = "VegaHedge",
                    ["MaxVega"] = 50,
                    ["VegaHedgeRatio"] = 0.7,
                    ["ShortDelta"] = 0.17,
                    ["EntryDay"] = DayOfWeek.Monday,
                    ["DecisionDays"] = "Mon,Thu"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL46",
                Category = "RiskManagement",
                Description = "Time Decay Focus - Close at 50% theta capture",
                Parameters = new Dictionary<string, object>
                {
                    ["RiskStyle"] = "ThetaCapture",
                    ["ThetaCaptureTarget"] = 50,
                    ["MinHoldDays"] = 2,
                    ["ShortDelta"] = 0.16,
                    ["EntryDay"] = DayOfWeek.Monday,
                    ["DecisionDays"] = "Wed,Fri"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL47",
                Category = "RiskManagement",
                Description = "Binary Defense - All or nothing approach",
                Parameters = new Dictionary<string, object>
                {
                    ["RiskStyle"] = "Binary",
                    ["DefendThreshold"] = 150,
                    ["DefendAction"] = "DoubleDown",
                    ["MaxDefends"] = 1,
                    ["ShortDelta"] = 0.14,
                    ["EntryDay"] = DayOfWeek.Tuesday,
                    ["DecisionDays"] = "Tue,Thu"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL48",
                Category = "RiskManagement",
                Description = "Kelly Criterion - Optimal position sizing",
                Parameters = new Dictionary<string, object>
                {
                    ["RiskStyle"] = "Kelly",
                    ["KellyFraction"] = 0.25,
                    ["WinRateEstimate"] = 0.70,
                    ["PayoffRatio"] = 0.5,
                    ["ShortDelta"] = 0.18,
                    ["EntryDay"] = DayOfWeek.Wednesday,
                    ["DecisionDays"] = "Wed,Fri"
                }
            });

            return variants;
        }

        private static List<OilStrategyVariant> GenerateExitStrategyMutations()
        {
            var variants = new List<OilStrategyVariant>();

            // OIL49-OIL52: Thursday Exit Variants
            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL49",
                Category = "ExitStrategy",
                Description = "Thursday Morning Exit - Avoid Friday risk",
                Parameters = new Dictionary<string, object>
                {
                    ["ExitDay"] = DayOfWeek.Thursday,
                    ["ExitTime"] = "09:35",
                    ["ExitStyle"] = "Immediate",
                    ["MinProfit"] = 0,
                    ["ShortDelta"] = 0.15,
                    ["EntryDay"] = DayOfWeek.Monday,
                    ["DecisionDays"] = "Mon,Thu"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL50",
                Category = "ExitStrategy",
                Description = "Thursday Noon Exit - Half day hold",
                Parameters = new Dictionary<string, object>
                {
                    ["ExitDay"] = DayOfWeek.Thursday,
                    ["ExitTime"] = "12:00",
                    ["ExitStyle"] = "Scaled",
                    ["ScalePercent"] = 50,
                    ["ShortDelta"] = 0.18,
                    ["EntryDay"] = DayOfWeek.Monday,
                    ["DecisionDays"] = "Mon,Thu"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL51",
                Category = "ExitStrategy",
                Description = "Thursday Close Exit - Full Thursday capture",
                Parameters = new Dictionary<string, object>
                {
                    ["ExitDay"] = DayOfWeek.Thursday,
                    ["ExitTime"] = "15:45",
                    ["ExitStyle"] = "EndOfDay",
                    ["ShortDelta"] = 0.16,
                    ["EntryDay"] = DayOfWeek.Tuesday,
                    ["DecisionDays"] = "Tue,Thu"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL52",
                Category = "ExitStrategy",
                Description = "Thursday Profit-Only Exit - Exit only if profitable",
                Parameters = new Dictionary<string, object>
                {
                    ["ExitDay"] = DayOfWeek.Thursday,
                    ["ExitTime"] = "14:00",
                    ["ExitStyle"] = "Conditional",
                    ["ExitCondition"] = "ProfitOnly",
                    ["MinProfit"] = 10,
                    ["ShortDelta"] = 0.20,
                    ["EntryDay"] = DayOfWeek.Wednesday,
                    ["DecisionDays"] = "Wed,Thu"
                }
            });

            // OIL53-OIL56: Friday Exit Variants
            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL53",
                Category = "ExitStrategy",
                Description = "Friday Morning Exit - Pin risk avoidance",
                Parameters = new Dictionary<string, object>
                {
                    ["ExitDay"] = DayOfWeek.Friday,
                    ["ExitTime"] = "10:00",
                    ["ExitStyle"] = "PinAware",
                    ["PinDistance"] = 1.0,
                    ["ShortDelta"] = 0.14,
                    ["EntryDay"] = DayOfWeek.Monday,
                    ["DecisionDays"] = "Mon,Fri"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL54",
                Category = "ExitStrategy",
                Description = "Friday 2PM Exit - Late exit for max theta",
                Parameters = new Dictionary<string, object>
                {
                    ["ExitDay"] = DayOfWeek.Friday,
                    ["ExitTime"] = "14:00",
                    ["ExitStyle"] = "ThetaMax",
                    ["ShortDelta"] = 0.12,
                    ["EntryDay"] = DayOfWeek.Monday,
                    ["DecisionDays"] = "Mon,Fri"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL55",
                Category = "ExitStrategy",
                Description = "Friday 3:30PM Exit - Near expiry exit",
                Parameters = new Dictionary<string, object>
                {
                    ["ExitDay"] = DayOfWeek.Friday,
                    ["ExitTime"] = "15:30",
                    ["ExitStyle"] = "LateExit",
                    ["EmergencyDelta"] = 0.40,
                    ["ShortDelta"] = 0.10,
                    ["EntryDay"] = DayOfWeek.Tuesday,
                    ["DecisionDays"] = "Tue,Fri"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL56",
                Category = "ExitStrategy",
                Description = "Friday Expiry Hold - Let expire worthless",
                Parameters = new Dictionary<string, object>
                {
                    ["ExitDay"] = DayOfWeek.Friday,
                    ["ExitTime"] = "16:00",
                    ["ExitStyle"] = "Expiry",
                    ["MaxDeltaForExpiry"] = 0.05,
                    ["ShortDelta"] = 0.08,
                    ["EntryDay"] = DayOfWeek.Wednesday,
                    ["DecisionDays"] = "Wed"
                }
            });

            // OIL57-OIL60: Dynamic Exit Variants
            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL57",
                Category = "ExitStrategy",
                Description = "Volatility-Based Exit - Exit on vol spike",
                Parameters = new Dictionary<string, object>
                {
                    ["ExitTrigger"] = "Volatility",
                    ["IVSpikeTrigger"] = 30,
                    ["RVSpikeTrigger"] = 25,
                    ["MaxExitDay"] = DayOfWeek.Friday,
                    ["ShortDelta"] = 0.17,
                    ["EntryDay"] = DayOfWeek.Monday,
                    ["DecisionDays"] = "Daily"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL58",
                Category = "ExitStrategy",
                Description = "Event-Driven Exit - Exit before reports",
                Parameters = new Dictionary<string, object>
                {
                    ["ExitTrigger"] = "Event",
                    ["EventTypes"] = "EIA,API,OPEC",
                    ["PreEventHours"] = 2,
                    ["MaxExitDay"] = DayOfWeek.Friday,
                    ["ShortDelta"] = 0.15,
                    ["EntryDay"] = DayOfWeek.Monday,
                    ["DecisionDays"] = "Event-based"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL59",
                Category = "ExitStrategy",
                Description = "Technical Exit - Exit on support/resistance breach",
                Parameters = new Dictionary<string, object>
                {
                    ["ExitTrigger"] = "Technical",
                    ["TechnicalSignals"] = "Support,Resistance,Trendline",
                    ["ConfirmationBars"] = 2,
                    ["MaxExitDay"] = DayOfWeek.Friday,
                    ["ShortDelta"] = 0.19,
                    ["EntryDay"] = DayOfWeek.Tuesday,
                    ["DecisionDays"] = "Tue,Thu,Fri"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL60",
                Category = "ExitStrategy",
                Description = "Correlation Exit - Exit on correlation breakdown",
                Parameters = new Dictionary<string, object>
                {
                    ["ExitTrigger"] = "Correlation",
                    ["CorrelationAssets"] = "ES,GC,DX",
                    ["CorrelationThreshold"] = 0.7,
                    ["MaxExitDay"] = DayOfWeek.Friday,
                    ["ShortDelta"] = 0.16,
                    ["EntryDay"] = DayOfWeek.Wednesday,
                    ["DecisionDays"] = "Wed,Fri"
                }
            });

            // OIL61-OIL64: Hybrid Exit Variants
            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL61",
                Category = "ExitStrategy",
                Description = "Profit Cascade - Scale out on profit milestones",
                Parameters = new Dictionary<string, object>
                {
                    ["ExitStyle"] = "Cascade",
                    ["ProfitLevels"] = new[] { 20, 40, 60 },
                    ["ExitPercents"] = new[] { 33, 33, 34 },
                    ["FinalExitDay"] = DayOfWeek.Friday,
                    ["ShortDelta"] = 0.18,
                    ["EntryDay"] = DayOfWeek.Monday,
                    ["DecisionDays"] = "Mon,Wed,Fri"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL62",
                Category = "ExitStrategy",
                Description = "Time Decay Optimal - Exit at peak theta/gamma ratio",
                Parameters = new Dictionary<string, object>
                {
                    ["ExitStyle"] = "ThetaGamma",
                    ["OptimalRatio"] = 2.0,
                    ["MinHoldHours"] = 48,
                    ["MaxExitDay"] = DayOfWeek.Friday,
                    ["ExitTime"] = "12:00",
                    ["ShortDelta"] = 0.15,
                    ["EntryDay"] = DayOfWeek.Monday,
                    ["DecisionDays"] = "Wed,Fri"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL63",
                Category = "ExitStrategy",
                Description = "Smart Roll - Roll or exit based on conditions",
                Parameters = new Dictionary<string, object>
                {
                    ["ExitStyle"] = "SmartRoll",
                    ["RollIfDelta"] = 0.30,
                    ["ExitIfLoss"] = 100,
                    ["RollCost"] = 0.10,
                    ["DecisionDay"] = DayOfWeek.Thursday,
                    ["ShortDelta"] = 0.17,
                    ["EntryDay"] = DayOfWeek.Tuesday,
                    ["DecisionDays"] = "Tue,Thu"
                }
            });

            variants.Add(new OilStrategyVariant
            {
                VariantId = "OIL64",
                Category = "ExitStrategy",
                Description = "AI-Guided Exit - ML model determines optimal exit",
                Parameters = new Dictionary<string, object>
                {
                    ["ExitStyle"] = "MLGuided",
                    ["ModelFeatures"] = "Delta,Gamma,Theta,IV,DTE,PnL",
                    ["ConfidenceThreshold"] = 0.75,
                    ["FallbackDay"] = DayOfWeek.Friday,
                    ["FallbackTime"] = "14:00",
                    ["ShortDelta"] = 0.16,
                    ["EntryDay"] = DayOfWeek.Wednesday,
                    ["DecisionDays"] = "Model-driven"
                }
            });

            return variants;
        }
    }
}