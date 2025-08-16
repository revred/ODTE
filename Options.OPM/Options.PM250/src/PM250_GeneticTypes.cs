using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ODTE.Strategy
{
    /// <summary>
    /// PM250 Genetic Algorithm Data Types
    /// 
    /// CHROMOSOME REPRESENTATION FOR PM250 OPTIMIZATION:
    /// - Encodes all optimizable parameters as genes
    /// - Maintains strict type safety and bounds checking
    /// - Supports cloning and mutation operations
    /// - Tracks fitness and performance metrics
    /// </summary>
    
    /// <summary>
    /// PM250 Chromosome - Genetic representation of strategy parameters
    /// </summary>
    public class PM250_Chromosome
    {
        // Core strategy parameters (genes)
        public double GoScoreThreshold { get; set; } = 65.0;
        public decimal ProfitTarget { get; set; } = 2.5m;
        public decimal CreditTarget { get; set; } = 0.08m;
        public double VIXSensitivity { get; set; } = 1.0;
        public double TrendTolerance { get; set; } = 0.7;
        public double RiskMultiplier { get; set; } = 1.0;
        
        // Advanced optimization weights
        public double TimeOfDayWeight { get; set; } = 1.0;
        public double MarketRegimeWeight { get; set; } = 1.0;
        public double VolatilityWeight { get; set; } = 1.0;
        public double MomentumWeight { get; set; } = 1.0;
        
        // Performance tracking
        public double Fitness { get; set; } = 0.0;
        public PerformanceMetrics? PerformanceMetrics { get; set; }
        public string? ErrorMessage { get; set; }
        
        public PM250_Chromosome Clone()
        {
            return new PM250_Chromosome
            {
                GoScoreThreshold = GoScoreThreshold,
                ProfitTarget = ProfitTarget,
                CreditTarget = CreditTarget,
                VIXSensitivity = VIXSensitivity,
                TrendTolerance = TrendTolerance,
                RiskMultiplier = RiskMultiplier,
                TimeOfDayWeight = TimeOfDayWeight,
                MarketRegimeWeight = MarketRegimeWeight,
                VolatilityWeight = VolatilityWeight,
                MomentumWeight = MomentumWeight
            };
        }
        
        public string GetParameterSummary()
        {
            return $"GoScore:{GoScoreThreshold:F1}, Profit:${ProfitTarget:F2}, " +
                   $"Credit:{CreditTarget:P1}, VIX:{VIXSensitivity:F2}, " +
                   $"Trend:{TrendTolerance:F2}, Risk:{RiskMultiplier:F2}";
        }
        
        public bool IsValid()
        {
            return GoScoreThreshold >= 55.0 && GoScoreThreshold <= 80.0 &&
                   ProfitTarget >= 1.5m && ProfitTarget <= 5.0m &&
                   CreditTarget >= 0.06m && CreditTarget <= 0.12m &&
                   VIXSensitivity >= 0.5 && VIXSensitivity <= 2.0 &&
                   TrendTolerance >= 0.3 && TrendTolerance <= 1.2 &&
                   RiskMultiplier >= 0.8 && RiskMultiplier <= 1.5;
        }
    }
    
    /// <summary>
    /// Performance metrics for fitness evaluation
    /// </summary>
    public class PerformanceMetrics
    {
        public int TotalTrades { get; set; }
        public int WinningTrades { get; set; }
        public double WinRate { get; set; }
        public decimal TotalPnL { get; set; }
        public decimal AvgTradeSize { get; set; }
        public decimal MaxDrawdown { get; set; }
        public double SharpeRatio { get; set; }
        public double ProfitFactor { get; set; }
        public double ExecutionRate { get; set; }
        public bool ViolatesRiskMandates { get; set; }
        
        public string GetSummary()
        {
            return $"Trades:{TotalTrades}, WinRate:{WinRate:P1}, " +
                   $"PnL:${TotalPnL:F0}, Drawdown:${MaxDrawdown:F0}, " +
                   $"Sharpe:{SharpeRatio:F2}, ExecRate:{ExecutionRate:P1}";
        }
    }
    
    /// <summary>
    /// Individual trade result for performance calculation
    /// </summary>
    public class TradeResult
    {
        public DateTime ExecutionTime { get; set; }
        public decimal PnL { get; set; }
        public bool IsWin { get; set; }
    }
    
    /// <summary>
    /// Overall optimization result
    /// </summary>
    public class OptimizationResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal MaxDrawdownLimit { get; set; }
        public int GenerationsCompleted { get; set; }
        public int TotalStrategiesTested { get; set; }
        
        public PM250_Chromosome? OptimalChromosome { get; set; }
        public List<PM250_Chromosome>? FinalPopulation { get; set; }
        public OptimizedStrategy? BestStrategy { get; set; }
        
        public TimeSpan Duration => EndTime - StartTime;
        
        public string GetOptimizationSummary()
        {
            if (!Success || OptimalChromosome == null)
                return $"❌ Optimization failed: {ErrorMessage}";
            
            return $"✅ Optimization completed in {Duration.TotalMinutes:F1} minutes\n" +
                   $"🏆 Best fitness: {OptimalChromosome.Fitness:F4}\n" +
                   $"📊 Parameters: {OptimalChromosome.GetParameterSummary()}\n" +
                   $"📈 Performance: {OptimalChromosome.PerformanceMetrics?.GetSummary()}";
        }
    }
    
    /// <summary>
    /// Enhanced strategy performance metrics for v2 genetic optimizer
    /// </summary>
    public class StrategyPerformance
    {
        public decimal AverageTradeProfit { get; set; }
        public int TotalTrades { get; set; }
        public double WinRate { get; set; }
        public decimal TotalProfitLoss { get; set; }
        public double MaxDrawdown { get; set; }
        public double SharpeRatio { get; set; }
        public double CalmarRatio { get; set; }
    }
    
    /// <summary>
    /// Optimization progress tracking
    /// </summary>
    public class OptimizationProgress
    {
        public int Generation { get; set; }
        public double BestFitness { get; set; }
        public decimal BestTradeProfit { get; set; }
        public double BestWinRate { get; set; }
        public double BestDrawdown { get; set; }
        public double BestSharpe { get; set; }
    }
    
    /// <summary>
    /// Optimized strategy result with parameters and performance
    /// </summary>
    public class OptimizedStrategy
    {
        public Dictionary<string, double> Parameters { get; set; } = new();
        public StrategyPerformance Performance { get; set; } = new();
    }
    
    /// <summary>
    /// Backtest result placeholder (will integrate with actual backtest engine)
    /// </summary>
    public class BacktestResult
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<GeneticTrade>? Trades { get; set; }
        public decimal TotalPnL { get; set; }
        public double MaxDrawdown { get; set; }
    }
    
    /// <summary>
    /// Individual trade record for genetic optimization
    /// </summary>
    public class GeneticTrade
    {
        public DateTime ExitTime { get; set; }
        public decimal ProfitLoss { get; set; }
        public bool IsWin => ProfitLoss > 0;
    }
    
    /// <summary>
    /// PM250 Strategy configured with genetic parameters
    /// </summary>
    public class PM250_GeneticStrategy
    {
        private readonly PM250_Chromosome _chromosome;
        private readonly ReverseFibonacciRiskManager _riskManager;
        private readonly List<TradeExecution> _recentTrades;
        private readonly Random _random;
        
        public PM250_GeneticStrategy(PM250_Chromosome chromosome)
        {
            _chromosome = chromosome ?? throw new ArgumentNullException(nameof(chromosome));
            _riskManager = new ReverseFibonacciRiskManager();
            _recentTrades = new List<TradeExecution>();
            _random = new Random();
        }
        
        public async Task<StrategyResult> ExecuteAsync(StrategyParameters parameters, MarketConditions conditions)
        {
            try
            {
                // Apply genetic parameters to strategy logic
                
                // Step 1: Enhanced trade timing validation with genetic weights
                if (!IsValidTradeOpportunity(conditions))
                    return CreateBlockedResult("Invalid trade timing or conditions");
                
                // Step 2: Genetic-optimized GoScore calculation
                var goScore = CalculateGeneticGoScore(conditions);
                if (goScore < _chromosome.GoScoreThreshold)
                    return CreateBlockedResult($"GoScore {goScore:F1} below threshold {_chromosome.GoScoreThreshold:F1}");
                
                // Step 3: Risk assessment with genetic sensitivity
                var riskAssessment = AssessGeneticRisk(conditions);
                if (!riskAssessment.IsAcceptable)
                    return CreateBlockedResult($"Risk assessment failed: {riskAssessment.Reason}");
                
                // Step 4: Genetic position sizing
                var adjustedSize = _riskManager.CalculatePositionSize(
                    parameters.PositionSize * (decimal)_chromosome.RiskMultiplier, 
                    _recentTrades);
                
                // Step 5: Execute trade with genetic parameters
                var result = await ExecuteGeneticTrade(adjustedSize, conditions);
                
                // Step 6: Record for risk management
                if (result.PnL != 0)
                {
                    RecordTradeExecution(result, conditions);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                return CreateErrorResult($"Genetic strategy error: {ex.Message}");
            }
        }
        
        private bool IsValidTradeOpportunity(MarketConditions conditions)
        {
            // Enhanced validation with genetic time-of-day weighting
            var hour = conditions.Date.Hour;
            var timeScore = CalculateTimeScore(hour) * _chromosome.TimeOfDayWeight;
            
            // Base trading hours validation
            var isValidHour = hour >= 9 && hour <= 15;
            
            // Genetic enhancement - allow trading outside normal hours if score is high enough
            if (!isValidHour && timeScore < 1.2)
                return false;
            
            // Check daily and weekly limits (unchanged - risk mandate)
            var todaysTrades = _recentTrades.Count(t => t.ExecutionTime.Date == conditions.Date.Date);
            if (todaysTrades >= 50) return false; // Daily limit
            
            var weekStart = conditions.Date.AddDays(-(int)conditions.Date.DayOfWeek);
            var weekTrades = _recentTrades.Count(t => t.ExecutionTime >= weekStart);
            if (weekTrades >= 250) return false; // Weekly limit
            
            // 6-minute separation (unchanged - risk mandate)
            var lastTrade = _recentTrades.LastOrDefault();
            if (lastTrade != null)
            {
                var timeSinceLastTrade = conditions.Date - lastTrade.ExecutionTime;
                if (timeSinceLastTrade.TotalMinutes < 6)
                    return false;
            }
            
            return true;
        }
        
        private double CalculateGeneticGoScore(MarketConditions conditions)
        {
            var baseScore = 50.0;
            
            // VIX component with genetic sensitivity
            var vixOptimal = conditions.VIX >= 15 && conditions.VIX <= 25;
            var vixScore = vixOptimal ? 85.0 : (conditions.VIX < 15 ? 70.0 : Math.Max(40.0, 90.0 - conditions.VIX));
            baseScore += (vixScore - 50) * 0.3 * _chromosome.VolatilityWeight;
            
            // Market regime with genetic weighting
            var regimeScore = conditions.MarketRegime switch
            {
                "Calm" => 90.0,
                "Mixed" => 75.0,
                "Volatile" => 60.0,
                _ => 50.0
            };
            baseScore += (regimeScore - 50) * 0.25 * _chromosome.MarketRegimeWeight;
            
            // Trend tolerance with genetic parameter
            var trendStability = Math.Max(0, _chromosome.TrendTolerance - Math.Abs(conditions.TrendScore));
            baseScore += trendStability * 20.0 * 0.2;
            
            // Time of day with genetic weighting
            var hour = conditions.Date.Hour;
            var timeScore = CalculateTimeScore(hour);
            baseScore += (timeScore - 50) * 0.15 * _chromosome.TimeOfDayWeight;
            
            // Momentum factor with genetic weighting
            var momentumScore = 50.0 + (_random.NextDouble() - 0.5) * 20.0; // Simplified
            baseScore += (momentumScore - 50) * 0.1 * _chromosome.MomentumWeight;
            
            return Math.Max(0, Math.Min(100, baseScore));
        }
        
        private double CalculateTimeScore(int hour)
        {
            return hour switch
            {
                10 or 14 => 95.0, // Peak hours
                9 or 11 or 13 or 15 => 80.0, // Good hours
                12 => 60.0, // Lunch hour
                _ => 40.0 // Other hours
            };
        }
        
        private RiskAssessment AssessGeneticRisk(MarketConditions conditions)
        {
            var assessment = new RiskAssessment { IsAcceptable = true };
            
            // Enhanced VIX sensitivity with genetic parameter
            var vixLimit = 40.0 + (_chromosome.VIXSensitivity - 1.0) * 15.0; // 25-55 range
            if (conditions.VIX > vixLimit)
            {
                assessment.IsAcceptable = false;
                assessment.Reason = $"VIX {conditions.VIX:F1} exceeds genetic limit {vixLimit:F1}";
                return assessment;
            }
            
            // Trend risk with genetic tolerance
            if (Math.Abs(conditions.TrendScore) > _chromosome.TrendTolerance * 1.5)
            {
                assessment.IsAcceptable = false;
                assessment.Reason = $"Trend {conditions.TrendScore:F2} exceeds genetic tolerance";
                return assessment;
            }
            
            // Market regime filtering
            if (conditions.MarketRegime == "Crisis")
            {
                assessment.IsAcceptable = false;
                assessment.Reason = "Crisis regime detected";
                return assessment;
            }
            
            return assessment;
        }
        
        private async Task<StrategyResult> ExecuteGeneticTrade(decimal adjustedSize, MarketConditions conditions)
        {
            // Calculate credit with genetic parameters
            var creditReceived = _chromosome.CreditTarget * adjustedSize * (decimal)conditions.UnderlyingPrice * 0.01m;
            
            // Apply execution costs
            var executionCost = creditReceived * 0.015m;
            var slippage = creditReceived * 0.008m;
            
            var netPnL = creditReceived - executionCost - slippage;
            
            // Check against genetic profit target
            if (netPnL < _chromosome.ProfitTarget * 0.7m) // Allow 30% variance
            {
                return CreateBlockedResult($"Insufficient profit: ${netPnL:F2} < ${_chromosome.ProfitTarget * 0.7m:F2}");
            }
            
            return new StrategyResult
            {
                PnL = netPnL,
                ExecutionDate = conditions.Date,
                StrategyName = "PM250_Genetic",
                IsWin = netPnL > 0,
                CreditReceived = creditReceived,
                MaxRisk = _chromosome.ProfitTarget * 3m, // Risk is 3x profit target
                Metadata = new Dictionary<string, object>
                {
                    { "ChromosomeId", _chromosome.GetHashCode() },
                    { "GoScoreThreshold", _chromosome.GoScoreThreshold },
                    { "ProfitTarget", _chromosome.ProfitTarget },
                    { "GeneticFitness", _chromosome.Fitness }
                }
            };
        }
        
        private void RecordTradeExecution(StrategyResult result, MarketConditions conditions)
        {
            var execution = new TradeExecution
            {
                ExecutionTime = conditions.Date,
                PnL = result.PnL,
                Success = result.IsWin,
                Strategy = "PM250_Genetic"
            };
            
            _recentTrades.Add(execution);
            
            // Maintain rolling window for performance
            if (_recentTrades.Count > 500)
            {
                _recentTrades.RemoveRange(0, 100);
            }
        }
        
        private StrategyResult CreateBlockedResult(string reason)
        {
            return new StrategyResult
            {
                PnL = 0,
                IsWin = false,
                StrategyName = "PM250_Genetic",
                Metadata = new Dictionary<string, object> { { "BlockReason", reason } }
            };
        }
        
        private StrategyResult CreateErrorResult(string error)
        {
            return new StrategyResult
            {
                PnL = 0,
                IsWin = false,
                StrategyName = "PM250_Genetic",
                Metadata = new Dictionary<string, object> { { "Error", error } }
            };
        }
    }
}