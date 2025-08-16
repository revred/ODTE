using System;
using System.Collections.Generic;
using System.Linq;

namespace ODTE.Strategy.RiskManagement
{
    /// <summary>
    /// Per-Trade Risk Manager with RFib Budget Integration (Tier A-1 Enhancement)
    /// 
    /// IMPROVEMENT GOAL: Prevent tail losses by capping individual trade risk relative to remaining daily budget
    /// 
    /// KEY ENHANCEMENT: Before any trade, reject if MaxLossAtEntry > f × RemainingDailyRFibBudget
    /// Default factor f = 0.40 ensures no single trade can consume more than 40% of remaining budget
    /// 
    /// WHY THIS WORKS UNIVERSALLY:
    /// - Budget-proportional caps are regime-agnostic
    /// - Bound tail losses consistently in calm AND volatile markets  
    /// - Prevent position sizing correlation disasters (like June 2025: 5 contracts = 242% drawdown)
    /// - Mathematical guarantee: cannot exceed daily RFib limit even with worst-case scenarios
    /// </summary>
    public class PerTradeRiskManager
    {
        #region Configuration
        
        /// <summary>
        /// Maximum fraction of remaining daily budget a single trade can risk
        /// Conservative default: 0.40 (40% of remaining budget)
        /// Aggressive option: 0.60 (for higher utilization)
        /// Ultra-safe option: 0.25 (for maximum capital preservation)
        /// </summary>
        public double MaxTradeRiskFraction { get; set; } = 0.40;
        
        /// <summary>
        /// Absolute maximum contracts per trade regardless of budget
        /// Prevents leverage explosion in favorable budget conditions
        /// </summary>
        public int AbsoluteMaxContracts { get; set; } = 3;
        
        /// <summary>
        /// Minimum trade size (contracts) to maintain execution capability
        /// </summary>
        public int MinimumTradeSize { get; set; } = 1;
        
        #endregion
        
        #region State Tracking
        
        private readonly Dictionary<DateTime, DailyRiskState> _dailyStates;
        private readonly ReverseFibonacciRiskManager _rfibManager;
        
        public PerTradeRiskManager(ReverseFibonacciRiskManager rfibManager)
        {
            _rfibManager = rfibManager ?? throw new ArgumentNullException(nameof(rfibManager));
            _dailyStates = new Dictionary<DateTime, DailyRiskState>();
        }
        
        #endregion
        
        #region Public Interface
        
        /// <summary>
        /// Primary validation method: Can this trade be executed given current risk budget?
        /// </summary>
        /// <param name="tradingDay">The trading day for budget tracking</param>
        /// <param name="maxLossAtEntry">Maximum possible loss if trade goes to stop loss</param>
        /// <param name="proposedContracts">Number of contracts proposed for this trade</param>
        /// <returns>Risk validation result with detailed reasoning</returns>
        public TradeRiskValidation ValidateTradeRisk(DateTime tradingDay, decimal maxLossAtEntry, int proposedContracts)
        {
            var dayState = GetOrCreateDailyState(tradingDay);
            var remainingBudget = _rfibManager.GetRemainingDailyBudget(tradingDay);
            
            // Core validation: MaxLoss vs Budget fraction
            var maxAllowedLoss = (decimal)MaxTradeRiskFraction * remainingBudget;
            
            var validation = new TradeRiskValidation
            {
                TradingDay = tradingDay,
                ProposedMaxLoss = maxLossAtEntry,
                ProposedContracts = proposedContracts,
                RemainingBudget = remainingBudget,
                MaxAllowedLoss = maxAllowedLoss,
                BudgetUtilization = (double)(dayState.TotalRiskTaken / remainingBudget),
                IsApproved = true,
                ReasonCodes = new List<string>()
            };
            
            // Validation Gate 1: Budget fraction check
            if (maxLossAtEntry > maxAllowedLoss)
            {
                validation.IsApproved = false;
                validation.ReasonCodes.Add($"BUDGET_FRACTION_EXCEEDED: ${maxLossAtEntry:F2} > ${maxAllowedLoss:F2} ({MaxTradeRiskFraction:P0} of remaining budget)");
            }
            
            // Validation Gate 2: Absolute contract limit
            if (proposedContracts > AbsoluteMaxContracts)
            {
                validation.IsApproved = false;
                validation.ReasonCodes.Add($"ABSOLUTE_CONTRACT_LIMIT: {proposedContracts} contracts > {AbsoluteMaxContracts} max");
            }
            
            // Validation Gate 3: Minimum size requirement
            if (proposedContracts < MinimumTradeSize)
            {
                validation.IsApproved = false;
                validation.ReasonCodes.Add($"BELOW_MINIMUM_SIZE: {proposedContracts} contracts < {MinimumTradeSize} min");
            }
            
            // Validation Gate 4: RFib manager check
            if (!_rfibManager.CanTrade(tradingDay))
            {
                validation.IsApproved = false;
                validation.ReasonCodes.Add("RFIB_TRADING_BLOCKED: Daily or weekly limit reached");
            }
            
            // Success case
            if (validation.IsApproved)
            {
                validation.ReasonCodes.Add($"APPROVED: Risk ${maxLossAtEntry:F2} within {MaxTradeRiskFraction:P0} budget limit");
            }
            
            return validation;
        }
        
        /// <summary>
        /// Calculate optimal contract size given risk constraints
        /// </summary>
        /// <param name="tradingDay">Trading day for budget context</param>
        /// <param name="riskPerContract">Risk amount per single contract</param>
        /// <param name="desiredContracts">Ideally desired contract count</param>
        /// <returns>Risk-adjusted contract count</returns>
        public int CalculateRiskAdjustedSize(DateTime tradingDay, decimal riskPerContract, int desiredContracts)
        {
            if (riskPerContract <= 0) return 0;
            
            var remainingBudget = _rfibManager.GetRemainingDailyBudget(tradingDay);
            var maxAllowedLoss = (decimal)MaxTradeRiskFraction * remainingBudget;
            
            // Calculate maximum contracts allowed by budget
            var maxContractsByBudget = (int)Math.Floor(maxAllowedLoss / riskPerContract);
            
            // Apply all constraints
            var constrainedSize = Math.Min(
                Math.Min(desiredContracts, maxContractsByBudget),
                AbsoluteMaxContracts
            );
            
            // Ensure minimum size or zero
            return constrainedSize >= MinimumTradeSize ? constrainedSize : 0;
        }
        
        /// <summary>
        /// Record a trade execution for budget tracking
        /// </summary>
        public void RecordTradeExecution(DateTime tradingDay, decimal actualRiskTaken, int contracts)
        {
            var dayState = GetOrCreateDailyState(tradingDay);
            dayState.TotalRiskTaken += actualRiskTaken;
            dayState.TradeCount++;
            dayState.TotalContracts += contracts;
            
            // Update RFib manager
            _rfibManager.RecordTradeLoss(tradingDay, actualRiskTaken);
        }
        
        /// <summary>
        /// Get current risk statistics for a trading day
        /// </summary>
        public DailyRiskStatistics GetDailyStatistics(DateTime tradingDay)
        {
            var dayState = GetOrCreateDailyState(tradingDay);
            var remainingBudget = _rfibManager.GetRemainingDailyBudget(tradingDay);
            var totalBudget = _rfibManager.GetDailyBudgetLimit(tradingDay);
            
            return new DailyRiskStatistics
            {
                TradingDay = tradingDay,
                TotalBudget = totalBudget,
                RemainingBudget = remainingBudget,
                RiskTaken = dayState.TotalRiskTaken,
                BudgetUtilization = (double)(dayState.TotalRiskTaken / totalBudget),
                TradeCount = dayState.TradeCount,
                TotalContracts = dayState.TotalContracts,
                AverageRiskPerTrade = dayState.TradeCount > 0 ? dayState.TotalRiskTaken / dayState.TradeCount : 0,
                AverageContractsPerTrade = dayState.TradeCount > 0 ? (double)dayState.TotalContracts / dayState.TradeCount : 0
            };
        }
        
        #endregion
        
        #region Internal Implementation
        
        private DailyRiskState GetOrCreateDailyState(DateTime tradingDay)
        {
            var day = tradingDay.Date;
            if (!_dailyStates.ContainsKey(day))
            {
                _dailyStates[day] = new DailyRiskState();
            }
            return _dailyStates[day];
        }
        
        #endregion
        
        #region Supporting Types
        
        private class DailyRiskState
        {
            public decimal TotalRiskTaken { get; set; } = 0;
            public int TradeCount { get; set; } = 0;
            public int TotalContracts { get; set; } = 0;
        }
        
        #endregion
    }
    
    #region Public Data Types
    
    /// <summary>
    /// Result of trade risk validation with detailed reasoning
    /// </summary>
    public class TradeRiskValidation
    {
        public DateTime TradingDay { get; set; }
        public decimal ProposedMaxLoss { get; set; }
        public int ProposedContracts { get; set; }
        public decimal RemainingBudget { get; set; }
        public decimal MaxAllowedLoss { get; set; }
        public double BudgetUtilization { get; set; }
        public bool IsApproved { get; set; }
        public List<string> ReasonCodes { get; set; } = new();
        
        /// <summary>
        /// Human-readable summary of validation result
        /// </summary>
        public string GetSummary()
        {
            var status = IsApproved ? "✅ APPROVED" : "❌ REJECTED";
            var reasons = string.Join("; ", ReasonCodes);
            return $"{status}: {ProposedContracts} contracts, ${ProposedMaxLoss:F2} risk, ${RemainingBudget:F2} budget remaining. {reasons}";
        }
    }
    
    /// <summary>
    /// Daily risk management statistics
    /// </summary>
    public class DailyRiskStatistics
    {
        public DateTime TradingDay { get; set; }
        public decimal TotalBudget { get; set; }
        public decimal RemainingBudget { get; set; }
        public decimal RiskTaken { get; set; }
        public double BudgetUtilization { get; set; }
        public int TradeCount { get; set; }
        public int TotalContracts { get; set; }
        public decimal AverageRiskPerTrade { get; set; }
        public double AverageContractsPerTrade { get; set; }
        
        public string GetSummary()
        {
            return $"Day {TradingDay:MM/dd}: {TradeCount} trades, {TotalContracts} contracts, " +
                   $"${RiskTaken:F2} risk taken ({BudgetUtilization:P1} of ${TotalBudget:F2} budget), " +
                   $"${RemainingBudget:F2} remaining";
        }
    }
    
    #endregion
}