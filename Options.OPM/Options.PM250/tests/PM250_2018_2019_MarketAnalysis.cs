using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ODTE.Historical;
using ODTE.Strategy;
using Xunit;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// Market Analysis for 2018-2019 Period for PM250 Optimization
    /// 
    /// CRITICAL ANALYSIS FOR GENETIC OPTIMIZATION:
    /// - 2018: February volatility spike (VIX >50), October correction
    /// - 2019: Trade war volatility, Fed policy changes
    /// - Focus: Risk management during high-stress periods
    /// - Target: $2500 max drawdown with Reverse Fibonacci
    /// </summary>
    public class PM250_2018_2019_MarketAnalysis
    {
        [Fact]
        public async Task Analyze_2018_2019_Market_Conditions()
        {
            Console.WriteLine("📊 PM250 MARKET ANALYSIS: 2018-2019 OPTIMIZATION PERIOD");
            Console.WriteLine("=".PadRight(70, '='));
            
            var dataManager = new HistoricalDataManager();
            await dataManager.InitializeAsync();
            
            var stats = await dataManager.GetStatsAsync();
            Console.WriteLine($"💾 Database Coverage: {stats.StartDate:yyyy-MM-dd} to {stats.EndDate:yyyy-MM-dd}");
            Console.WriteLine($"📈 Total Records: {stats.TotalRecords:N0}");
            Console.WriteLine();
            
            // Define optimization period
            var startDate = new DateTime(2018, 1, 1);
            var endDate = new DateTime(2019, 12, 31);
            
            Console.WriteLine($"🎯 Target Optimization Period: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            
            // Check data availability for 2018-2019
            var hasData = stats.StartDate <= startDate && stats.EndDate >= endDate;
            Console.WriteLine($"📊 Data Available: {(hasData ? "✅ COMPLETE" : "⚠️ PARTIAL")}");
            
            if (!hasData)
            {
                Console.WriteLine($"   Available: {stats.StartDate:yyyy-MM-dd} to {stats.EndDate:yyyy-MM-dd}");
                Console.WriteLine($"   Will use available data overlap for analysis");
                
                // Adjust to available data
                startDate = new DateTime(Math.Max(startDate.Ticks, stats.StartDate.Ticks));
                endDate = new DateTime(Math.Min(endDate.Ticks, stats.EndDate.Ticks));
                Console.WriteLine($"   Adjusted Period: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            }
            
            Console.WriteLine();
            Console.WriteLine("🔍 MARKET CONDITION ANALYSIS:");
            Console.WriteLine("-".PadRight(50, '-'));
            
            // Sample key periods for analysis
            var keyPeriods = new List<(DateTime Date, string Event)>
            {
                (new DateTime(2018, 2, 5), "VIX Spike (Volmageddon)"),
                (new DateTime(2018, 10, 10), "October Correction"),
                (new DateTime(2019, 5, 15), "Trade War Escalation"),
                (new DateTime(2019, 8, 14), "Yield Curve Inversion"),
                (new DateTime(2019, 12, 15), "Phase One Trade Deal")
            };
            
            var marketConditions = new List<MarketAnalysisPoint>();
            
            foreach (var (date, eventDescription) in keyPeriods)
            {
                if (date >= startDate && date <= endDate)
                {
                    try
                    {
                        var marketData = await dataManager.GetMarketDataAsync("XSP", date.Date, date.Date.AddDays(1));
                        
                        if (marketData?.Any() == true)
                        {
                            var data = marketData.First();
                            var volatility = EstimateVolatility(data);
                            var trend = CalculateTrend(data);
                            var regime = ClassifyRegime(volatility);
                            
                            var analysis = new MarketAnalysisPoint
                            {
                                Date = date,
                                Event = eventDescription,
                                Price = data.Close,
                                EstimatedVIX = volatility,
                                TrendScore = trend,
                                MarketRegime = regime,
                                Volume = data.Volume
                            };
                            
                            marketConditions.Add(analysis);
                            
                            Console.WriteLine($"📅 {date:yyyy-MM-dd} ({eventDescription}):");
                            Console.WriteLine($"   Price: ${data.Close:F2}, Vol: {volatility:F1}, Trend: {trend:F2}, Regime: {regime}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ {date:yyyy-MM-dd}: Data not available - {ex.Message}");
                    }
                }
            }
            
            Console.WriteLine();
            Console.WriteLine("🎯 GENETIC ALGORITHM OPTIMIZATION TARGETS:");
            Console.WriteLine("-".PadRight(50, '-'));
            Console.WriteLine("✅ Primary Objective: Maximize profit with strict risk controls");
            Console.WriteLine("✅ Risk Mandate: $2,500 maximum drawdown (HARD CONSTRAINT)");
            Console.WriteLine("✅ Capital Management: Reverse Fibonacci curtailment");
            Console.WriteLine("✅ Market Adaptability: Handle 2018 vol spike + 2019 uncertainty");
            Console.WriteLine("✅ Preserve High Win Rate: Target >75% while optimizing returns");
            
            Console.WriteLine();
            Console.WriteLine("🧬 PARAMETER SPACE FOR GENETIC OPTIMIZATION:");
            Console.WriteLine("-".PadRight(50, '-'));
            
            var parameterSpace = new
            {
                GoScoreThresholds = new { Min = 55.0, Max = 80.0, Current = 65.0 },
                ProfitTargets = new { Min = 1.5m, Max = 5.0m, Current = 2.5m },
                CreditTargets = new { Min = 0.06m, Max = 0.12m, Current = 0.08m },
                VIXSensitivity = new { Min = 0.5, Max = 2.0, Current = 1.0 },
                TrendTolerance = new { Min = 0.3, Max = 1.2, Current = 0.7 },
                RiskMultipliers = new { Min = 0.8, Max = 1.5, Current = 1.0 }
            };
            
            Console.WriteLine($"🎯 GoScore Threshold: {parameterSpace.GoScoreThresholds.Min}-{parameterSpace.GoScoreThresholds.Max} (current: {parameterSpace.GoScoreThresholds.Current})");
            Console.WriteLine($"💰 Profit Target: ${parameterSpace.ProfitTargets.Min}-${parameterSpace.ProfitTargets.Max} (current: ${parameterSpace.ProfitTargets.Current})");
            Console.WriteLine($"📊 Credit Target: {parameterSpace.CreditTargets.Min:P1}-{parameterSpace.CreditTargets.Max:P1} (current: {parameterSpace.CreditTargets.Current:P1})");
            Console.WriteLine($"📈 VIX Sensitivity: {parameterSpace.VIXSensitivity.Min}-{parameterSpace.VIXSensitivity.Max} (current: {parameterSpace.VIXSensitivity.Current})");
            Console.WriteLine($"📉 Trend Tolerance: {parameterSpace.TrendTolerance.Min}-{parameterSpace.TrendTolerance.Max} (current: {parameterSpace.TrendTolerance.Current})");
            
            Console.WriteLine();
            Console.WriteLine("⚡ OPTIMIZATION CONSTRAINTS:");
            Console.WriteLine("-".PadRight(30, '-'));
            Console.WriteLine("🛡️ MAX DRAWDOWN: $2,500 (ABSOLUTE LIMIT)");
            Console.WriteLine("📊 REVERSE FIBONACCI: Enabled (capital curtailment)");
            Console.WriteLine("🎯 WIN RATE: Maintain >75% (no dilution)");
            Console.WriteLine("⚡ EXECUTION RATE: Target 15-25% (quality over quantity)");
            Console.WriteLine("🔒 RISK MANDATE: NO COMPROMISE (fitness penalty for violations)");
            
            Console.WriteLine();
            Console.WriteLine("🚀 READY FOR GENETIC ALGORITHM OPTIMIZATION");
        }
        
        private double EstimateVolatility(MarketDataBar data)
        {
            // Estimate VIX based on price range
            var range = (data.High - data.Low) / data.Close;
            var baseVIX = 15.0 + (range * 200.0);
            return Math.Max(10.0, Math.Min(60.0, baseVIX));
        }
        
        private double CalculateTrend(MarketDataBar data)
        {
            // Simple trend indicator
            var mid = (data.High + data.Low) / 2.0;
            return Math.Max(-1.0, Math.Min(1.0, (data.Close - mid) / mid * 10.0));
        }
        
        private string ClassifyRegime(double vix)
        {
            return vix switch
            {
                > 35 => "Crisis",
                > 25 => "Volatile", 
                > 20 => "Mixed",
                _ => "Calm"
            };
        }
    }
    
    public class MarketAnalysisPoint
    {
        public DateTime Date { get; set; }
        public string Event { get; set; } = "";
        public double Price { get; set; }
        public double EstimatedVIX { get; set; }
        public double TrendScore { get; set; }
        public string MarketRegime { get; set; } = "";
        public long Volume { get; set; }
    }
}