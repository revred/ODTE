using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ODTE.Strategy
{
    /// <summary>
    /// Main interface for ODTE Strategy Library
    /// Provides access to all trading strategies and analysis tools
    /// </summary>
    public interface IStrategyEngine
    {
        /// <summary>
        /// Execute Iron Condor strategy
        /// </summary>
        Task<StrategyResult> ExecuteIronCondorAsync(StrategyParameters parameters, MarketConditions conditions);

        /// <summary>
        /// Execute Credit Broken Wing Butterfly strategy
        /// </summary>
        Task<StrategyResult> ExecuteCreditBWBAsync(StrategyParameters parameters, MarketConditions conditions);

        /// <summary>
        /// Execute Convex Tail Overlay strategy
        /// </summary>
        Task<StrategyResult> ExecuteConvexTailOverlayAsync(StrategyParameters parameters, MarketConditions conditions);

        /// <summary>
        /// Execute 24-day regime switching strategy
        /// </summary>
        Task<RegimeSwitchingResult> Execute24DayRegimeSwitchingAsync(DateTime startDate, DateTime endDate, decimal startingCapital = 5000m);

        /// <summary>
        /// Analyze market conditions and recommend optimal strategy
        /// </summary>
        Task<StrategyRecommendation> AnalyzeAndRecommendAsync(MarketConditions conditions);

        /// <summary>
        /// Run strategy performance analysis
        /// </summary>
        Task<PerformanceAnalysis> AnalyzePerformanceAsync(string strategyName, List<StrategyResult> results);

        /// <summary>
        /// Run regression tests on strategies
        /// </summary>
        Task<RegressionTestResults> RunRegressionTestsAsync();

        /// <summary>
        /// Run stress tests with synthetic data
        /// </summary>
        Task<StressTestResults> RunStressTestsAsync();
    }

    /// <summary>
    /// Market conditions for strategy execution
    /// Unified class supporting both new API and legacy properties
    /// </summary>
    public class MarketConditions
    {
        public DateTime Date { get; set; } = DateTime.Now;
        public double VIX { get; set; }
        public double IVRank { get; set; }
        public double TrendScore { get; set; }
        public double RealizedVolatility { get; set; }
        public double ImpliedVolatility { get; set; }
        public double TermStructureSlope { get; set; }
        public int DaysToExpiry { get; set; }
        public double UnderlyingPrice { get; set; }
        public string MarketRegime { get; set; } = "";
        public Dictionary<string, object> AdditionalData { get; set; } = new();

        // Legacy properties for backward compatibility
        public double RSI { get; set; }
        public double MomentumDivergence { get; set; }
        public double VIXContango { get; set; }

        // Compatibility properties with automatic conversion
        public decimal RSI_Decimal 
        { 
            get => (decimal)RSI; 
            set => RSI = (double)value; 
        }
        
        public decimal IVRank_Decimal 
        { 
            get => (decimal)IVRank; 
            set => IVRank = (double)value; 
        }
        
        public decimal VIXContango_Decimal 
        { 
            get => (decimal)VIXContango; 
            set => VIXContango = (double)value; 
        }
    }

    /// <summary>
    /// Strategy execution parameters
    /// </summary>
    public class StrategyParameters
    {
        public decimal PositionSize { get; set; } = 1000m;
        public decimal MaxRisk { get; set; } = 500m;
        public double DeltaThreshold { get; set; } = 0.15;
        public double CreditMinimum { get; set; } = 0.25;
        public int StrikeWidth { get; set; } = 10;
        public bool EnableRiskManagement { get; set; } = true;
        public Dictionary<string, object> StrategySpecific { get; set; } = new();
    }

    /// <summary>
    /// Result of strategy execution
    /// </summary>
    public class StrategyResult
    {
        public string StrategyName { get; set; } = "";
        public DateTime ExecutionDate { get; set; }
        public decimal PnL { get; set; }
        public decimal MaxRisk { get; set; }
        public decimal CreditReceived { get; set; }
        public bool IsWin { get; set; }
        public string ExitReason { get; set; } = "";
        public double WinProbability { get; set; }
        public Dictionary<string, double> Greeks { get; set; } = new();
        public List<OptionLeg> Legs { get; set; } = new();
        public string MarketRegime { get; set; } = "";
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Option leg details
    /// </summary>
    public class OptionLeg
    {
        public string OptionType { get; set; } = ""; // "Call" or "Put"
        public double Strike { get; set; }
        public int Quantity { get; set; }
        public string Action { get; set; } = ""; // "Buy" or "Sell"
        public double Premium { get; set; }
        public double Delta { get; set; }
        public double Gamma { get; set; }
        public double Theta { get; set; }
        public double Vega { get; set; }
    }

    /// <summary>
    /// 24-day regime switching results
    /// </summary>
    public class RegimeSwitchingResult
    {
        public List<TwentyFourDayPeriod> Periods { get; set; } = new();
        public decimal TotalReturn { get; set; }
        public decimal AverageReturn { get; set; }
        public decimal BestPeriodReturn { get; set; }
        public decimal WorstPeriodReturn { get; set; }
        public double WinRate { get; set; }
        public Dictionary<string, decimal> RegimePerformance { get; set; } = new();
        public int TotalPeriods { get; set; }
        public decimal MaxDrawdown { get; set; }
        public double SharpeRatio { get; set; }
        
        // Additional metrics for comprehensive analysis
        public string StrategyName { get; set; } = "24-Day Regime Switching";
        public decimal FinalCapital { get; set; }
        public decimal ReturnPercentage { get; set; }
        public int WinningPeriods { get; set; }
        public int LosingPeriods { get; set; }
        public int CalmPeriods { get; set; }
        public int MixedPeriods { get; set; }
        public int ConvexPeriods { get; set; }
        public int MaxConsecutiveLosses { get; set; }
        public decimal LargestLoss { get; set; }
        public decimal LargestWin { get; set; }
        public double ProfitFactor { get; set; }
        public int MaxRecoveryDays { get; set; }
        public double BWBWinRate { get; set; }
        public double TailOverlayROI { get; set; }
        public double RegimeAccuracy { get; set; }
        public decimal AnnualizedReturn { get; set; }
        public decimal RiskAdjustedReturn { get; set; }
    }

    /// <summary>
    /// 24-day trading period
    /// </summary>
    public class TwentyFourDayPeriod
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int PeriodNumber { get; set; }
        public decimal StartingCapital { get; set; }
        public decimal EndingCapital { get; set; }
        public decimal PnL { get; set; }
        public decimal MaxDrawdown { get; set; }
        public List<DailyStrategyResult> DailyResults { get; set; } = new();
        public Dictionary<string, int> RegimeDays { get; set; } = new();
        public string DominantRegime { get; set; } = "";
    }

    /// <summary>
    /// Daily strategy execution result
    /// </summary>
    public class DailyStrategyResult
    {
        public DateTime Date { get; set; }
        public string StrategyUsed { get; set; } = "";
        public string RegimeDetected { get; set; } = "";
        public decimal DailyPnL { get; set; }
        public decimal CumulativePnL { get; set; }
        public List<StrategyResult> Trades { get; set; } = new();
        public MarketConditions Conditions { get; set; } = new();
    }

    /// <summary>
    /// Strategy recommendation
    /// </summary>
    public class StrategyRecommendation
    {
        public string RecommendedStrategy { get; set; } = "";
        public double ConfidenceScore { get; set; }
        public string MarketRegime { get; set; } = "";
        public string Reasoning { get; set; } = "";
        public StrategyParameters SuggestedParameters { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, double> AlternativeStrategies { get; set; } = new();
    }

    /// <summary>
    /// Performance analysis results
    /// </summary>
    public class PerformanceAnalysis
    {
        public string StrategyName { get; set; } = "";
        public int TotalTrades { get; set; }
        public double WinRate { get; set; }
        public decimal AverageWin { get; set; }
        public decimal AverageLoss { get; set; }
        public decimal TotalPnL { get; set; }
        public decimal MaxDrawdown { get; set; }
        public double SharpeRatio { get; set; }
        public double ProfitFactor { get; set; }
        public Dictionary<string, double> RegimePerformance { get; set; } = new();
        public List<string> KeyInsights { get; set; } = new();
        public DateTime AnalysisDate { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Regression test results
    /// </summary>
    public class RegressionTestResults
    {
        public bool AllTestsPassed { get; set; }
        public int TestsPassed { get; set; }
        public int TotalTests { get; set; }
        public List<StrategyTestResult> StrategyResults { get; set; } = new();
        public List<string> FailureReasons { get; set; } = new();
        public DateTime TestDate { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Individual strategy test result
    /// </summary>
    public class StrategyTestResult
    {
        public string StrategyName { get; set; } = "";
        public bool Passed { get; set; }
        public double ActualWinRate { get; set; }
        public double ExpectedWinRate { get; set; }
        public double ActualSharpeRatio { get; set; }
        public double ExpectedSharpeRatio { get; set; }
        public List<string> Issues { get; set; } = new();
    }

    /// <summary>
    /// Stress test results
    /// </summary>
    public class StressTestResults
    {
        public List<StressTestScenario> Scenarios { get; set; } = new();
        public string BestPerformingScenario { get; set; } = "";
        public string WorstPerformingScenario { get; set; } = "";
        public decimal AveragePerformance { get; set; }
        public List<string> KeyFindings { get; set; } = new();
        public DateTime TestDate { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Stress test scenario result
    /// </summary>
    public class StressTestScenario
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal TotalPnL { get; set; }
        public double WinRate { get; set; }
        public decimal MaxDrawdown { get; set; }
        public int WhipsawTrades { get; set; }
        public bool Passed { get; set; }
    }
}