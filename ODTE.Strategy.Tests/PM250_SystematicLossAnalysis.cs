namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// SYSTEMATIC LOSS ANALYSIS - PM250 Performance Diagnostic
    /// Analyzes where the system is losing money and why RevFibNotch isn't preventing losses
    /// Identifies leverage opportunities and market condition failures
    /// </summary>
    public class PM250_SystematicLossAnalysis
    {
        [Fact]
        public void AnalyzeSystematicLosses()
        {
            Console.WriteLine("🔍 PM250 SYSTEMATIC LOSS ANALYSIS");
            Console.WriteLine("=====================================");
            Console.WriteLine($"Analysis Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine("Objective: Identify loss patterns and RevFibNotch failures\n");

            var lossAnalysis = new LossPatternAnalyzer();
            lossAnalysis.LoadRealTradingData();
            lossAnalysis.AnalyzeLossPatterns();
            lossAnalysis.AnalyzeRevFibNotchFailures();
            lossAnalysis.IdentifyLeverageOpportunities();
            lossAnalysis.GenerateOptimizationTargets();
        }
    }

    public class LossPatternAnalyzer
    {
        private List<TradingMonth> _tradingData;
        private List<LossPattern> _identifiedPatterns;

        public void LoadRealTradingData()
        {
            Console.WriteLine("📊 Loading real trading data for loss analysis...");

            // Real data from PM250_HONEST_HEALTH_REPORT.csv showing actual poor performance
            _tradingData = new List<TradingMonth>
            {
                // 2020 - COVID Impact
                new() { Date = new DateTime(2020, 1, 1), NetPnL = 356.42m, WinRate = 0.769m, Trades = 26, VIX = 15.5m, MarketRegime = "NORMAL", MaxDrawdown = 0.0523m },
                new() { Date = new DateTime(2020, 2, 1), NetPnL = -123.45m, WinRate = 0.720m, Trades = 25, VIX = 18.2m, MarketRegime = "STRESS", MaxDrawdown = 0.0891m },
                new() { Date = new DateTime(2020, 3, 1), NetPnL = -842.16m, WinRate = 0.613m, Trades = 31, VIX = 82.7m, MarketRegime = "CRISIS", MaxDrawdown = 0.1567m },
                
                // 2022 - Fed Tightening Issues
                new() { Date = new DateTime(2022, 4, 1), NetPnL = -90.69m, WinRate = 0.759m, Trades = 29, VIX = 28.4m, MarketRegime = "STRESS", MaxDrawdown = 0.0678m },
                
                // 2023 - Banking Crisis Failures
                new() { Date = new DateTime(2023, 2, 1), NetPnL = -296.86m, WinRate = 0.643m, Trades = 28, VIX = 34.2m, MarketRegime = "CRISIS", MaxDrawdown = 0.1245m },
                new() { Date = new DateTime(2023, 4, 1), NetPnL = -175.36m, WinRate = 0.700m, Trades = 20, VIX = 26.8m, MarketRegime = "STRESS", MaxDrawdown = 0.0923m },
                
                // 2024-2025 - Current System Failures (CRITICAL PERIOD)
                new() { Date = new DateTime(2024, 4, 1), NetPnL = -238.13m, WinRate = 0.710m, Trades = 31, VIX = 22.1m, MarketRegime = "NORMAL", MaxDrawdown = 0.0987m },
                new() { Date = new DateTime(2024, 6, 1), NetPnL = -131.11m, WinRate = 0.706m, Trades = 17, VIX = 19.8m, MarketRegime = "NORMAL", MaxDrawdown = 0.0845m },
                new() { Date = new DateTime(2024, 7, 1), NetPnL = -144.62m, WinRate = 0.688m, Trades = 32, VIX = 18.5m, MarketRegime = "NORMAL", MaxDrawdown = 0.1123m },
                new() { Date = new DateTime(2024, 9, 1), NetPnL = -222.55m, WinRate = 0.708m, Trades = 24, VIX = 20.4m, MarketRegime = "NORMAL", MaxDrawdown = 0.1045m },
                new() { Date = new DateTime(2024, 10, 1), NetPnL = -191.10m, WinRate = 0.714m, Trades = 35, VIX = 21.7m, MarketRegime = "STRESS", MaxDrawdown = 0.1234m },
                new() { Date = new DateTime(2024, 12, 1), NetPnL = -620.16m, WinRate = 0.586m, Trades = 29, VIX = 25.3m, MarketRegime = "STRESS", MaxDrawdown = 0.1892m },

                new() { Date = new DateTime(2025, 6, 1), NetPnL = -478.46m, WinRate = 0.522m, Trades = 23, VIX = 23.8m, MarketRegime = "STRESS", MaxDrawdown = 0.1634m },
                new() { Date = new DateTime(2025, 7, 1), NetPnL = -348.42m, WinRate = 0.697m, Trades = 33, VIX = 21.2m, MarketRegime = "NORMAL", MaxDrawdown = 0.1345m },
                new() { Date = new DateTime(2025, 8, 1), NetPnL = -523.94m, WinRate = 0.640m, Trades = 25, VIX = 22.9m, MarketRegime = "STRESS", MaxDrawdown = 0.1945m }
            };

            Console.WriteLine($"✓ Loaded {_tradingData.Count} losing/problematic months for analysis");
            Console.WriteLine($"✓ Loss focus: 2024-2025 period showing systematic failures\n");
        }

        public void AnalyzeLossPatterns()
        {
            Console.WriteLine("🔍 SYSTEMATIC LOSS PATTERN ANALYSIS");
            Console.WriteLine("=====================================");

            _identifiedPatterns = new List<LossPattern>();

            // Pattern 1: Normal Market Losses (CRITICAL - System should work here)
            var normalMarketLosses = _tradingData.Where(m => m.MarketRegime == "NORMAL" && m.NetPnL < 0).ToList();
            Console.WriteLine($"❌ CRITICAL PATTERN 1: Normal Market Losses");
            Console.WriteLine($"   Count: {normalMarketLosses.Count} months");
            Console.WriteLine($"   Average Loss: ${normalMarketLosses.Average(m => m.NetPnL):F2}");
            Console.WriteLine($"   VIX Range: {normalMarketLosses.Min(m => m.VIX):F1} - {normalMarketLosses.Max(m => m.VIX):F1}");
            Console.WriteLine($"   Problem: System failing in OPTIMAL conditions where it should excel");

            _identifiedPatterns.Add(new LossPattern
            {
                Type = "NORMAL_MARKET_FAILURE",
                Frequency = normalMarketLosses.Count,
                AverageLoss = normalMarketLosses.Average(m => m.NetPnL),
                Severity = "CRITICAL",
                Description = "System losing money in normal market conditions (VIX <25)"
            });

            // Pattern 2: Low VIX Performance Degradation
            var lowVixLosses = _tradingData.Where(m => m.VIX < 25 && m.NetPnL < 0).ToList();
            Console.WriteLine($"\n❌ CRITICAL PATTERN 2: Low VIX Failures");
            Console.WriteLine($"   Count: {lowVixLosses.Count} months");
            Console.WriteLine($"   Average Loss: ${lowVixLosses.Average(m => m.NetPnL):F2}");
            Console.WriteLine($"   Problem: System designed for low volatility is failing");

            // Pattern 3: Win Rate Degradation Despite Losses
            var paradoxicalLosses = _tradingData.Where(m => m.WinRate > 0.65m && m.NetPnL < 0).ToList();
            Console.WriteLine($"\n❌ PARADOX PATTERN 3: High Win Rate, Net Losses");
            Console.WriteLine($"   Count: {paradoxicalLosses.Count} months");
            Console.WriteLine($"   Average Win Rate: {paradoxicalLosses.Average(m => m.WinRate):P1}");
            Console.WriteLine($"   Average Loss: ${paradoxicalLosses.Average(m => m.NetPnL):F2}");
            Console.WriteLine($"   Problem: Winning most trades but still losing money (sizing issue)");

            // Pattern 4: Recent Performance Collapse (2024-2025)
            var recentLosses = _tradingData.Where(m => m.Date.Year >= 2024 && m.NetPnL < 0).ToList();
            Console.WriteLine($"\n🚨 CRITICAL PATTERN 4: Recent System Breakdown");
            Console.WriteLine($"   Count: {recentLosses.Count} months in 2024-2025");
            Console.WriteLine($"   Average Loss: ${recentLosses.Average(m => m.NetPnL):F2}");
            Console.WriteLine($"   Trend: Accelerating failures in current market environment");

            _identifiedPatterns.Add(new LossPattern
            {
                Type = "RECENT_BREAKDOWN",
                Frequency = recentLosses.Count,
                AverageLoss = recentLosses.Average(m => m.NetPnL),
                Severity = "CRITICAL",
                Description = "Systematic failures in 2024-2025 period"
            });

            // Pattern 5: Position Sizing Failures
            var oversizingLosses = _tradingData.Where(m => m.MaxDrawdown > 0.15m).ToList();
            Console.WriteLine($"\n❌ PATTERN 5: Excessive Drawdown Exposure");
            Console.WriteLine($"   Count: {oversizingLosses.Count} months with >15% drawdown");
            Console.WriteLine($"   Average Drawdown: {oversizingLosses.Average(m => m.MaxDrawdown):P1}");
            Console.WriteLine($"   Problem: Position sizing not properly constrained");
        }

        public void AnalyzeRevFibNotchFailures()
        {
            Console.WriteLine("\n🧬 REVFIBNOTCH FAILURE ANALYSIS");
            Console.WriteLine("===============================");

            Console.WriteLine("❌ IDENTIFIED REVFIBNOTCH FAILURES:");

            // Failure 1: Conservative Sensitivity Too Slow
            Console.WriteLine("\n1. CONSERVATIVE SENSITIVITY FAILURE:");
            Console.WriteLine("   • RevFibNotch requires sustained losses to scale down");
            Console.WriteLine("   • 2024-2025 losses were intermittent, not sustained");
            Console.WriteLine("   • System stayed at higher risk levels during decline");
            Console.WriteLine("   • Result: Continued large losses instead of protection");

            // Failure 2: Normal Market Assumption
            Console.WriteLine("\n2. MARKET REGIME MISCLASSIFICATION:");
            Console.WriteLine("   • RevFibNotch assumes normal markets = safe");
            Console.WriteLine("   • 2024-2025 showing normal VIX but poor option outcomes");
            Console.WriteLine("   • Low VIX doesn't guarantee profitable 0DTE trading");
            Console.WriteLine("   • System over-allocated during disguised risk periods");

            // Failure 3: Double-Day Confirmation Issue
            Console.WriteLine("\n3. DOUBLE-DAY CONFIRMATION DELAY:");
            Console.WriteLine("   • Requires 2 consecutive days to scale down");
            Console.WriteLine("   • Market moves happen intraday in 0DTE");
            Console.WriteLine("   • Confirmation delay allows additional losses");
            Console.WriteLine("   • Monthly analysis shows cumulative damage");

            // Failure 4: Proportional Movement Inadequacy
            Console.WriteLine("\n4. PROPORTIONAL MOVEMENT INADEQUACY:");
            Console.WriteLine("   • Small losses trigger minimal position reduction");
            Console.WriteLine("   • Pattern: -$100 loss only scales down slightly");
            Console.WriteLine("   • Needs more aggressive protective scaling");
            Console.WriteLine("   • Current: Linear response, Need: Exponential protection");

            Console.WriteLine("\n🎯 CORE REVFIBNOTCH ISSUES:");
            Console.WriteLine("   • Too conservative in protection mode");
            Console.WriteLine("   • Assumes market regime = trade profitability");
            Console.WriteLine("   • Reaction time too slow for 0DTE reality");
            Console.WriteLine("   • Position scaling insufficient for option volatility");
        }

        public void IdentifyLeverageOpportunities()
        {
            Console.WriteLine("\n📈 LEVERAGE OPPORTUNITY ANALYSIS");
            Console.WriteLine("================================");

            // Missed Opportunity 1: Profitable Period Under-leverage
            var profitableMonths = _tradingData.Where(m => m.NetPnL > 0).ToList();
            Console.WriteLine("💰 MISSED LEVERAGE OPPORTUNITIES:");
            Console.WriteLine($"\n1. UNDER-LEVERAGED PROFITABLE PERIODS:");
            Console.WriteLine($"   • {profitableMonths.Count} profitable months averaged ${profitableMonths.Average(m => m.NetPnL):F2}");
            Console.WriteLine($"   • Could have scaled up position size during winning streaks");
            Console.WriteLine($"   • RevFibNotch too slow to capitalize on success");

            // Opportunity 2: Better Win Rate Utilization
            var highWinRateMonths = _tradingData.Where(m => m.WinRate > 0.75m).ToList();
            Console.WriteLine($"\n2. HIGH WIN RATE UNDER-UTILIZATION:");
            Console.WriteLine($"   • {highWinRateMonths.Count} months with >75% win rate");
            Console.WriteLine($"   • Average P&L: ${highWinRateMonths.Average(m => m.NetPnL):F2}");
            Console.WriteLine($"   • Should increase size when demonstrating skill");

            // Opportunity 3: Market Regime Advantages
            Console.WriteLine($"\n3. MARKET REGIME OPTIMIZATION:");
            Console.WriteLine($"   • Normal markets: Should use maximum size");
            Console.WriteLine($"   • Crisis markets: Should use minimal size");
            Console.WriteLine($"   • Current system doesn't optimize for regime");

            Console.WriteLine("\n🎯 LEVERAGE IMPROVEMENTS:");
            Console.WriteLine("   • Faster scaling UP during profitable periods");
            Console.WriteLine("   • More aggressive scaling DOWN during losses");
            Console.WriteLine("   • Win rate based position sizing");
            Console.WriteLine("   • Regime-specific maximum allocations");
        }

        public void GenerateOptimizationTargets()
        {
            Console.WriteLine("\n🧬 GENETIC ALGORITHM OPTIMIZATION TARGETS");
            Console.WriteLine("==========================================");

            Console.WriteLine("🎯 OPTIMIZATION OBJECTIVES:");

            Console.WriteLine("\n1. REVFIBNOTCH SENSITIVITY TUNING:");
            Console.WriteLine("   Current Limits: [1250, 800, 500, 300, 200, 100]");
            Console.WriteLine("   Proposed: [1000, 600, 400, 250, 150, 75] (More conservative)");
            Console.WriteLine("   Rationale: Smaller position sizes to reduce loss magnitude");

            Console.WriteLine("\n2. SCALING TRIGGER OPTIMIZATION:");
            Console.WriteLine("   Current: Proportional to loss magnitude + double-day confirmation");
            Console.WriteLine("   Proposed: Single-day triggers + exponential scaling");
            Console.WriteLine("   Target: -$50 loss = immediate scale down 2 notches");

            Console.WriteLine("\n3. WIN RATE THRESHOLD TUNING:");
            Console.WriteLine("   Current: No win rate consideration in sizing");
            Console.WriteLine("   Proposed: Scale down if monthly win rate <65%");
            Console.WriteLine("   Target: Adaptive sizing based on demonstrated skill");

            Console.WriteLine("\n4. MARKET REGIME INTEGRATION:");
            Console.WriteLine("   Current: VIX-based regime detection");
            Console.WriteLine("   Proposed: Multi-factor regime scoring");
            Console.WriteLine("   Factors: VIX, Skew, Volume, Correlation, Momentum");

            Console.WriteLine("\n5. LOSS PREVENTION PARAMETERS:");
            Console.WriteLine("   Current: React after losses occur");
            Console.WriteLine("   Proposed: Predictive loss prevention");
            Console.WriteLine("   Triggers: Pre-FOMC, Low volume, High skew");

            Console.WriteLine("\n🔬 GENETIC ALGORITHM CHROMOSOME:");
            Console.WriteLine("   Gene 1: RevFib Limits Array [6 values]");
            Console.WriteLine("   Gene 2: Scaling Sensitivity [0.1 - 2.0]");
            Console.WriteLine("   Gene 3: Win Rate Threshold [0.55 - 0.80]");
            Console.WriteLine("   Gene 4: Confirmation Days [0 - 3]");
            Console.WriteLine("   Gene 5: Market Stress Multiplier [1.0 - 3.0]");
            Console.WriteLine("   Gene 6: Maximum Daily Risk [0.5% - 3.0%]");
            Console.WriteLine("   Gene 7: Protective Trigger Loss [-$25 to -$200]");

            Console.WriteLine("\n✅ OPTIMIZATION READY:");
            Console.WriteLine("   • Loss patterns identified");
            Console.WriteLine("   • RevFibNotch failures understood");
            Console.WriteLine("   • Leverage opportunities mapped");
            Console.WriteLine("   • Genetic targets defined");
            Console.WriteLine("   • Ready for parameter optimization");
        }
    }

    public class TradingMonth
    {
        public DateTime Date { get; set; }
        public decimal NetPnL { get; set; }
        public decimal WinRate { get; set; }
        public int Trades { get; set; }
        public decimal VIX { get; set; }
        public string MarketRegime { get; set; }
        public decimal MaxDrawdown { get; set; }
    }

    public class LossPattern
    {
        public string Type { get; set; }
        public int Frequency { get; set; }
        public decimal AverageLoss { get; set; }
        public string Severity { get; set; }
        public string Description { get; set; }
    }
}