namespace ODTE.Strategy.RiskManagement
{
    /// <summary>
    /// Tier A Trade Execution Gate - Pre-Trade Rejection Logic (A1.5 + A2.4)
    /// 
    /// INTEGRATION POINT: Combines all Tier A risk management enhancements into single validation gate
    /// 
    /// VALIDATION PIPELINE:
    /// 1. PerTradeRiskManager - RFib budget integration
    /// 2. MaxLossCalculator - Precise risk calculation  
    /// 3. BudgetCapValidator - f=0.40 factor enforcement
    /// 4. IntegerPositionSizer - Whole contract sizing (A2.4)
    /// 5. LiquidityQualityFilter - Hard floor enforcement
    /// 
    /// PREVENTS:
    /// - Budget fraction violations (June 2025: 242% drawdown)
    /// - Hidden leverage drift from fractional contracts
    /// - Liquidity-based slippage disasters
    /// - Correlation risk from oversized positions
    /// 
    /// DESIGN PHILOSOPHY:
    /// - FAIL FAST: Reject bad trades before execution
    /// - FAIL SAFE: Conservative defaults on any uncertainty
    /// - FAIL TRANSPARENT: Detailed logging of all rejections
    /// </summary>
    public class TierATradeExecutionGate
    {
        #region Dependencies

        private readonly PerTradeRiskManager _perTradeRiskManager;
        private readonly BudgetCapValidator _budgetCapValidator;
        private readonly IntegerPositionSizer _integerPositionSizer;
        private readonly ReverseFibonacciRiskManager _rfibManager;
        private readonly Dictionary<DateTime, List<TradeGateRecord>> _gateHistory;
        private readonly List<ComprehensiveAuditRecord> _auditLog;

        #endregion

        #region Configuration

        /// <summary>
        /// Enable/disable individual validation components for A/B testing
        /// </summary>
        public TierAValidationConfig ValidationConfig { get; set; } = new();

        /// <summary>
        /// Trade execution statistics for monitoring effectiveness
        /// </summary>
        public TradeGateStatistics Statistics { get; private set; } = new();

        #endregion

        #region Constructor

        public TierATradeExecutionGate(
            PerTradeRiskManager perTradeRiskManager,
            BudgetCapValidator budgetCapValidator,
            IntegerPositionSizer integerPositionSizer,
            ReverseFibonacciRiskManager rfibManager)
        {
            _perTradeRiskManager = perTradeRiskManager ?? throw new ArgumentNullException(nameof(perTradeRiskManager));
            _budgetCapValidator = budgetCapValidator ?? throw new ArgumentNullException(nameof(budgetCapValidator));
            _integerPositionSizer = integerPositionSizer ?? throw new ArgumentNullException(nameof(integerPositionSizer));
            _rfibManager = rfibManager ?? throw new ArgumentNullException(nameof(rfibManager));
            _gateHistory = new Dictionary<DateTime, List<TradeGateRecord>>();
            _auditLog = new List<ComprehensiveAuditRecord>();
        }

        #endregion

        #region Primary Trade Validation

        /// <summary>
        /// Master validation method: Run all Tier A checks on proposed trade
        /// </summary>
        /// <param name="tradeCandidate">Complete trade specification</param>
        /// <param name="tradingDay">Trading day for context</param>
        /// <returns>Comprehensive validation result with detailed reasoning</returns>
        public TierAValidationResult ValidateTradeExecution(TradeCandidate tradeCandidate, DateTime tradingDay)
        {
            var result = new TierAValidationResult
            {
                TradeCandidate = tradeCandidate,
                TradingDay = tradingDay,
                ValidationTimestamp = DateTime.UtcNow,
                IsApproved = true,
                ValidationResults = new List<IndividualValidationResult>()
            };

            try
            {
                // VALIDATION STAGE 1: Calculate maximum loss at entry
                var maxLossResult = CalculateMaxLoss(tradeCandidate);
                result.MaxLossAtEntry = maxLossResult.MaxLossAmount;
                result.ValidationResults.Add(CreateValidationResult("MaxLossCalculation", maxLossResult.IsValid, maxLossResult.Summary));

                if (!maxLossResult.IsValid)
                {
                    result.IsApproved = false;
                    result.PrimaryRejectReason = "MAX_LOSS_CALCULATION_FAILED";
                    return result;
                }

                // VALIDATION STAGE 2: Per-trade risk manager validation
                if (ValidationConfig.EnablePerTradeRiskValidation)
                {
                    var riskValidation = _perTradeRiskManager.ValidateTradeRisk(
                        tradingDay,
                        result.MaxLossAtEntry,
                        tradeCandidate.Contracts);

                    result.ValidationResults.Add(CreateValidationResult(
                        "PerTradeRiskManager",
                        riskValidation.IsApproved,
                        riskValidation.GetSummary()));

                    if (!riskValidation.IsApproved)
                    {
                        result.IsApproved = false;
                        result.PrimaryRejectReason = "PER_TRADE_RISK_EXCEEDED";
                        result.DetailedRejectReason = string.Join("; ", riskValidation.ReasonCodes);
                        return result;
                    }
                }

                // VALIDATION STAGE 3: Budget cap validation (f=0.40 factor)
                if (ValidationConfig.EnableBudgetCapValidation)
                {
                    var budgetValidation = _budgetCapValidator.ValidateBudgetCap(
                        tradingDay,
                        result.MaxLossAtEntry,
                        tradeCandidate.Contracts);

                    result.ValidationResults.Add(CreateValidationResult(
                        "BudgetCapValidator",
                        budgetValidation.IsApproved,
                        budgetValidation.GetSummary()));

                    if (!budgetValidation.IsApproved)
                    {
                        result.IsApproved = false;
                        result.PrimaryRejectReason = "BUDGET_CAP_EXCEEDED";
                        result.DetailedRejectReason = budgetValidation.ReasonMessage;
                        result.SuggestedContractReduction = budgetValidation.SuggestedAction;
                        return result;
                    }
                }

                // VALIDATION STAGE 4: Integer position sizing validation (A2.4)
                if (ValidationConfig.EnableIntegerSizingValidation)
                {
                    var strategySpec = new StrategySpecification
                    {
                        StrategyType = tradeCandidate.StrategyType,
                        NetCredit = tradeCandidate.NetCredit,
                        Width = tradeCandidate.Width,
                        PutWidth = tradeCandidate.PutWidth,
                        CallWidth = tradeCandidate.CallWidth,
                        BodyWidth = tradeCandidate.BodyWidth,
                        WingWidth = tradeCandidate.WingWidth
                    };

                    var integerValidation = _integerPositionSizer.ValidateContractCount(
                        tradingDay,
                        strategySpec,
                        tradeCandidate.Contracts);

                    result.ValidationResults.Add(CreateValidationResult(
                        "IntegerPositionSizer",
                        integerValidation.IsValid,
                        integerValidation.ValidationDetails));

                    if (!integerValidation.IsValid)
                    {
                        result.IsApproved = false;
                        result.PrimaryRejectReason = integerValidation.RejectReason ?? "INTEGER_SIZING_FAILED";
                        result.DetailedRejectReason = integerValidation.ValidationDetails;
                        result.SuggestedContractReduction = $"Max allowed: {integerValidation.MaxAllowedContracts}";
                        return result;
                    }
                }

                // VALIDATION STAGE 5: Liquidity quality validation
                if (ValidationConfig.EnableLiquidityValidation)
                {
                    var liquidityValidation = ValidateLiquidityQuality(tradeCandidate);
                    result.ValidationResults.Add(CreateValidationResult(
                        "LiquidityQuality",
                        liquidityValidation.IsValid,
                        liquidityValidation.Message));

                    if (!liquidityValidation.IsValid)
                    {
                        result.IsApproved = false;
                        result.PrimaryRejectReason = "INSUFFICIENT_LIQUIDITY";
                        result.DetailedRejectReason = liquidityValidation.Message;
                        return result;
                    }
                }

                // VALIDATION STAGE 6: Final sanity checks
                var sanityValidation = ValidateSanityChecks(tradeCandidate, result.MaxLossAtEntry);
                result.ValidationResults.Add(CreateValidationResult(
                    "SanityChecks",
                    sanityValidation.IsValid,
                    sanityValidation.Message));

                if (!sanityValidation.IsValid)
                {
                    result.IsApproved = false;
                    result.PrimaryRejectReason = "SANITY_CHECK_FAILED";
                    result.DetailedRejectReason = sanityValidation.Message;
                    return result;
                }

                // SUCCESS: All validations passed
                result.PrimaryRejectReason = "APPROVED";
                result.DetailedRejectReason = $"All {result.ValidationResults.Count} Tier A validations passed";

                // Update statistics
                Statistics.TotalValidations++;
                Statistics.ApprovedTrades++;

                // H4: Record comprehensive audit trail
                RecordComprehensiveAudit(result);
                RecordGateDecision(result);

                return result;
            }
            catch (Exception ex)
            {
                result.IsApproved = false;
                result.PrimaryRejectReason = "VALIDATION_SYSTEM_ERROR";
                result.DetailedRejectReason = $"Exception during validation: {ex.Message}";
                result.ValidationResults.Add(CreateValidationResult("SystemError", false, ex.Message));

                Statistics.SystemErrors++;
                RecordComprehensiveAudit(result);
                RecordGateDecision(result);

                return result;
            }
        }

        #endregion

        #region Individual Validation Methods

        private MaxLossValidationResult CalculateMaxLoss(TradeCandidate candidate)
        {
            try
            {
                var strategySpec = new StrategySpecification
                {
                    StrategyType = candidate.StrategyType,
                    NetCredit = candidate.NetCredit,
                    Width = candidate.Width,
                    PutWidth = candidate.PutWidth,
                    CallWidth = candidate.CallWidth,
                    BodyWidth = candidate.BodyWidth,
                    WingWidth = candidate.WingWidth
                };

                var maxLossResult = MaxLossCalculator.CalculateGenericMaxLoss(strategySpec, candidate.Contracts);

                return new MaxLossValidationResult
                {
                    IsValid = maxLossResult.MaxLossAmount > 0,
                    MaxLossAmount = maxLossResult.MaxLossAmount,
                    Summary = maxLossResult.GetSummary()
                };
            }
            catch (Exception ex)
            {
                return new MaxLossValidationResult
                {
                    IsValid = false,
                    MaxLossAmount = 0,
                    Summary = $"Max loss calculation failed: {ex.Message}"
                };
            }
        }


        private SimpleValidationResult ValidateLiquidityQuality(TradeCandidate candidate)
        {
            // Simplified liquidity validation - full implementation would integrate with real market data
            if (candidate.LiquidityScore < 0.5)
            {
                return new SimpleValidationResult
                {
                    IsValid = false,
                    Message = $"Liquidity score {candidate.LiquidityScore:F2} below minimum 0.50 threshold"
                };
            }

            if (candidate.BidAskSpread > 0.25m)
            {
                return new SimpleValidationResult
                {
                    IsValid = false,
                    Message = $"Bid-ask spread {candidate.BidAskSpread:P2} exceeds 25% maximum"
                };
            }

            return new SimpleValidationResult
            {
                IsValid = true,
                Message = $"Liquidity validated: Score {candidate.LiquidityScore:F2}, Spread {candidate.BidAskSpread:P2}"
            };
        }

        private SimpleValidationResult ValidateSanityChecks(TradeCandidate candidate, decimal maxLoss)
        {
            // Sanity check 1: Net credit should be positive for credit strategies
            if (candidate.NetCredit <= 0)
            {
                return new SimpleValidationResult
                {
                    IsValid = false,
                    Message = $"Invalid net credit: ${candidate.NetCredit:F2}. Credit strategies require positive credit."
                };
            }

            // Sanity check 2: Max loss should be reasonable relative to credit
            var riskRewardRatio = maxLoss / (candidate.NetCredit * 100m * candidate.Contracts);
            if (riskRewardRatio > 10m) // Risk > 10x reward is suspicious
            {
                return new SimpleValidationResult
                {
                    IsValid = false,
                    Message = $"Excessive risk/reward ratio: {riskRewardRatio:F1}x. Max loss ${maxLoss:F2} vs credit ${candidate.NetCredit * 100m * candidate.Contracts:F2}"
                };
            }

            // Sanity check 3: Position size should be reasonable
            if (candidate.Contracts > 10)
            {
                return new SimpleValidationResult
                {
                    IsValid = false,
                    Message = $"Excessive position size: {candidate.Contracts} contracts exceeds 10 contract safety limit"
                };
            }

            return new SimpleValidationResult
            {
                IsValid = true,
                Message = $"Sanity checks passed: R/R {riskRewardRatio:F1}x, {candidate.Contracts} contracts"
            };
        }

        #endregion

        #region Helper Methods

        private IndividualValidationResult CreateValidationResult(string validator, bool passed, string message)
        {
            return new IndividualValidationResult
            {
                ValidatorName = validator,
                Passed = passed,
                Message = message,
                Timestamp = DateTime.UtcNow
            };
        }

        private void RecordGateDecision(TierAValidationResult result)
        {
            var day = result.TradingDay.Date;
            if (!_gateHistory.ContainsKey(day))
            {
                _gateHistory[day] = new List<TradeGateRecord>();
            }

            _gateHistory[day].Add(new TradeGateRecord
            {
                Timestamp = result.ValidationTimestamp,
                IsApproved = result.IsApproved,
                RejectReason = result.PrimaryRejectReason,
                MaxLossAtEntry = result.MaxLossAtEntry,
                ProposedContracts = result.TradeCandidate.Contracts,
                ValidationCount = result.ValidationResults.Count
            });

            if (!result.IsApproved)
            {
                Statistics.RejectedTrades++;
            }
        }

        #endregion

        #region Analytics & Reporting

        /// <summary>
        /// Get daily gate statistics for performance monitoring
        /// </summary>
        public DailyGateStatistics GetDailyStatistics(DateTime tradingDay)
        {
            var day = tradingDay.Date;
            var records = _gateHistory.ContainsKey(day) ? _gateHistory[day] : new List<TradeGateRecord>();

            return new DailyGateStatistics
            {
                TradingDay = day,
                TotalValidations = records.Count,
                ApprovedTrades = records.Count(r => r.IsApproved),
                RejectedTrades = records.Count(r => !r.IsApproved),
                ApprovalRate = records.Count > 0 ? (double)records.Count(r => r.IsApproved) / records.Count : 0,
                TotalRiskRequested = records.Sum(r => r.MaxLossAtEntry),
                TotalRiskApproved = records.Where(r => r.IsApproved).Sum(r => r.MaxLossAtEntry),
                TopRejectReasons = records.Where(r => !r.IsApproved)
                                        .GroupBy(r => r.RejectReason)
                                        .OrderByDescending(g => g.Count())
                                        .Take(3)
                                        .ToDictionary(g => g.Key, g => g.Count())
            };
        }

        /// <summary>
        /// H4: Record comprehensive audit trail for all trade decisions
        /// </summary>
        private void RecordComprehensiveAudit(TierAValidationResult result)
        {
            var remainingBudget = _rfibManager.GetRemainingDailyBudget(result.TradingDay);
            var dailyCap = _rfibManager.GetDailyBudgetLimit(result.TradingDay);

            var auditRecord = new ComprehensiveAuditRecord
            {
                Timestamp = result.ValidationTimestamp,
                Symbol = "XSP", // Default underlying
                Side = GetTradeDescription(result.TradeCandidate.StrategyType),
                Width = result.TradeCandidate.Width,
                ExpectedCredit = result.TradeCandidate.NetCredit,
                MaxLossPerContract = result.MaxLossAtEntry / result.TradeCandidate.Contracts,
                DailyCap = dailyCap,
                RemainingBudget = remainingBudget,
                PerTradeFraction = _perTradeRiskManager.MaxTradeRiskFraction,
                PerTradeCap = remainingBudget * (decimal)_perTradeRiskManager.MaxTradeRiskFraction,
                DerivedContracts = result.TradeCandidate.Contracts,
                HardCap = IntegerPositionSizer.HARD_CAP_CONTRACTS,
                Decision = result.IsApproved ? "ACCEPT" : "REJECT",
                ReasonCode = result.PrimaryRejectReason,
                DetailedReason = result.DetailedRejectReason,
                ValidationCount = result.ValidationResults.Count,
                PassedValidations = result.ValidationResults.Count(v => v.Passed)
            };

            _auditLog.Add(auditRecord);

            // Keep audit log size manageable (last 1000 entries)
            if (_auditLog.Count > 1000)
            {
                _auditLog.RemoveRange(0, _auditLog.Count - 1000);
            }
        }

        private string GetTradeDescription(StrategyType strategyType)
        {
            return strategyType switch
            {
                StrategyType.IronCondor => "iron_condor",
                StrategyType.CreditBWB => "credit_bwb",
                StrategyType.CreditSpread => "credit_spread",
                _ => strategyType.ToString().ToLowerInvariant()
            };
        }

        /// <summary>
        /// Get recent audit records for analysis
        /// </summary>
        public List<ComprehensiveAuditRecord> GetAuditRecords(int maxRecords = 100)
        {
            return _auditLog.TakeLast(maxRecords).ToList();
        }

        /// <summary>
        /// Export audit records as JSON for external analysis
        /// </summary>
        public string ExportAuditToJson(DateTime? fromDate = null)
        {
            var records = fromDate.HasValue
                ? _auditLog.Where(r => r.Timestamp >= fromDate.Value).ToList()
                : _auditLog;

            return System.Text.Json.JsonSerializer.Serialize(records, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        #endregion
    }

    #region Supporting Data Types

    /// <summary>
    /// Configuration for Tier A validation components
    /// </summary>
    public class TierAValidationConfig
    {
        public bool EnablePerTradeRiskValidation { get; set; } = true;
        public bool EnableBudgetCapValidation { get; set; } = true;
        public bool EnableIntegerSizingValidation { get; set; } = true;
        public bool EnableLiquidityValidation { get; set; } = true;
        public bool EnableSanityChecks { get; set; } = true;
    }

    /// <summary>
    /// Overall statistics for trade gate performance
    /// </summary>
    public class TradeGateStatistics
    {
        public int TotalValidations { get; set; }
        public int ApprovedTrades { get; set; }
        public int RejectedTrades { get; set; }
        public int SystemErrors { get; set; }

        public double ApprovalRate => TotalValidations > 0 ? (double)ApprovedTrades / TotalValidations : 0;
        public double ErrorRate => TotalValidations > 0 ? (double)SystemErrors / TotalValidations : 0;
    }

    /// <summary>
    /// Complete validation result from Tier A gate
    /// </summary>
    public class TierAValidationResult
    {
        public TradeCandidate TradeCandidate { get; set; } = new();
        public DateTime TradingDay { get; set; }
        public DateTime ValidationTimestamp { get; set; }
        public bool IsApproved { get; set; }
        public decimal MaxLossAtEntry { get; set; }
        public string PrimaryRejectReason { get; set; } = "";
        public string DetailedRejectReason { get; set; } = "";
        public string SuggestedContractReduction { get; set; } = "";
        public List<IndividualValidationResult> ValidationResults { get; set; } = new();

        public string GetExecutiveSummary()
        {
            var status = IsApproved ? "✅ APPROVED" : "❌ REJECTED";
            var passedCount = ValidationResults.Count(v => v.Passed);
            var totalCount = ValidationResults.Count;

            return $"{status}: {TradeCandidate.Contracts} contracts, ${MaxLossAtEntry:F2} max loss. " +
                   $"Validations: {passedCount}/{totalCount} passed. {PrimaryRejectReason}";
        }
    }

    /// <summary>
    /// Individual validation component result
    /// </summary>
    public class IndividualValidationResult
    {
        public string ValidatorName { get; set; } = "";
        public bool Passed { get; set; }
        public string Message { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Simple validation result for basic checks
    /// </summary>
    public class SimpleValidationResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = "";
    }

    /// <summary>
    /// Max loss calculation validation result
    /// </summary>
    public class MaxLossValidationResult
    {
        public bool IsValid { get; set; }
        public decimal MaxLossAmount { get; set; }
        public string Summary { get; set; } = "";
    }

    /// <summary>
    /// Internal record for gate decision history
    /// </summary>
    internal class TradeGateRecord
    {
        public DateTime Timestamp { get; set; }
        public bool IsApproved { get; set; }
        public string RejectReason { get; set; } = "";
        public decimal MaxLossAtEntry { get; set; }
        public int ProposedContracts { get; set; }
        public int ValidationCount { get; set; }
    }

    /// <summary>
    /// Daily gate performance statistics
    /// </summary>
    public class DailyGateStatistics
    {
        public DateTime TradingDay { get; set; }
        public int TotalValidations { get; set; }
        public int ApprovedTrades { get; set; }
        public int RejectedTrades { get; set; }
        public double ApprovalRate { get; set; }
        public decimal TotalRiskRequested { get; set; }
        public decimal TotalRiskApproved { get; set; }
        public Dictionary<string, int> TopRejectReasons { get; set; } = new();

        public string GetSummary()
        {
            var topReason = TopRejectReasons.FirstOrDefault();
            return $"Day {TradingDay:MM/dd}: {ApprovedTrades}/{TotalValidations} approved ({ApprovalRate:P1}). " +
                   $"Top reject reason: {topReason.Key} ({topReason.Value} occurrences)";
        }
    }

    /// <summary>
    /// Trade candidate for validation
    /// </summary>
    public class TradeCandidate
    {
        public StrategyType StrategyType { get; set; }
        public int Contracts { get; set; }
        public decimal NetCredit { get; set; }
        public decimal Width { get; set; }
        public decimal PutWidth { get; set; }
        public decimal CallWidth { get; set; }
        public decimal BodyWidth { get; set; }
        public decimal WingWidth { get; set; }
        public double LiquidityScore { get; set; }
        public decimal BidAskSpread { get; set; }
        public DateTime ProposedExecutionTime { get; set; }
    }

    /// <summary>
    /// H4: Comprehensive audit record following roadmap specification
    /// </summary>
    public class ComprehensiveAuditRecord
    {
        public DateTime Timestamp { get; set; }
        public string Symbol { get; set; } = "";
        public string Side { get; set; } = "";
        public decimal Width { get; set; }
        public decimal ExpectedCredit { get; set; }
        public decimal MaxLossPerContract { get; set; }
        public decimal DailyCap { get; set; }
        public decimal RemainingBudget { get; set; }
        public double PerTradeFraction { get; set; }
        public decimal PerTradeCap { get; set; }
        public int DerivedContracts { get; set; }
        public int HardCap { get; set; }
        public string Decision { get; set; } = "";
        public string ReasonCode { get; set; } = "";
        public string DetailedReason { get; set; } = "";
        public int ValidationCount { get; set; }
        public int PassedValidations { get; set; }

        /// <summary>
        /// Convert to JSON format as specified in roadmap
        /// </summary>
        public string ToJsonFormat()
        {
            return $@"{{
  ""t"": ""{Timestamp:yyyy-MM-ddTHH:mm:ssZ}"",
  ""sym"": ""{Symbol}"",
  ""side"": ""{Side}"",
  ""width"": {Width:F2},
  ""expectedCredit"": {ExpectedCredit:F2},
  ""maxLossPerContract"": {MaxLossPerContract:F0},
  ""dailyCap"": {DailyCap:F0},
  ""remainingBudget"": {RemainingBudget:F0},
  ""perTradeFraction"": {PerTradeFraction:F2},
  ""perTradeCap"": {PerTradeCap:F0},
  ""derivedContracts"": {DerivedContracts},
  ""hardCap"": {HardCap},
  ""decision"": ""{Decision}"",
  ""reasonCode"": ""{ReasonCode}""
}}";
        }
    }

    #endregion
}