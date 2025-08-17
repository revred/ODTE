namespace ODTE.Strategy.RiskManagement
{
    /// <summary>
    /// Tier A-2: Integer Position Sizing Component
    /// 
    /// PURPOSE: Enforces integer contract validation with smart cap calculation
    /// 
    /// CORE FUNCTIONALITY:
    /// 1. Calculates maximum contracts from loss allowance per trade
    /// 2. Enforces hard cap: min(derivedCap, HardCap) 
    /// 3. Validates integer constraints before execution
    /// 4. Provides defensive position sizing for uncertain conditions
    /// 
    /// INTEGRATION:
    /// - Works with PerTradeRiskManager for loss allowance
    /// - Integrates with BudgetCapValidator for f=0.40 factor
    /// - Feeds into TierATradeExecutionGate validation
    /// 
    /// RISK MITIGATION:
    /// - Prevents fractional contract execution (impossible to execute)
    /// - Caps position size to manageable levels
    /// - Provides consistency across all strategy types
    /// - Maintains mathematical guarantees from Tier A-1
    /// </summary>
    public class IntegerPositionSizer
    {
        #region Configuration

        /// <summary>Hard cap on maximum contracts per trade (defensive limit)</summary>
        public const int HARD_CAP_CONTRACTS = 8;

        /// <summary>Minimum contract size (always 1)</summary>
        public const int MIN_CONTRACTS = 1;

        /// <summary>Safety buffer for position sizing calculations (5%)</summary>
        public const decimal SAFETY_BUFFER = 0.05m;

        /// <summary>Low cap threshold for dynamic fraction boost</summary>
        public const decimal LOW_CAP_THRESHOLD = 150m;

        /// <summary>Higher fraction for low cap scenarios</summary>
        public const double LOW_CAP_FRACTION = 0.80;

        /// <summary>Enable probe 1-lot rule when no positions open</summary>
        public bool EnableProbeTradeRule { get; set; } = true;

        /// <summary>Only probe when no positions are open</summary>
        public bool ProbeOnlyWhenNoPositions { get; set; } = true;

        /// <summary>Enable dynamic fraction boost at low caps</summary>
        public bool EnableLowCapBoost { get; set; } = true;

        /// <summary>Enable scale-to-fit narrow-once fallback</summary>
        public bool EnableScaleToFit { get; set; } = true;

        /// <summary>Minimum width for scale-to-fit fallback</summary>
        public decimal MinWidthPoints { get; set; } = 1.0m;

        #endregion

        #region Dependencies

        private readonly PerTradeRiskManager _perTradeRiskManager;
        private readonly ReverseFibonacciRiskManager _rfibManager;
        private readonly Dictionary<DateTime, int> _openPositionTracker;

        #endregion

        #region Constructor

        public IntegerPositionSizer(
            PerTradeRiskManager perTradeRiskManager,
            ReverseFibonacciRiskManager rfibManager)
        {
            _perTradeRiskManager = perTradeRiskManager ?? throw new ArgumentNullException(nameof(perTradeRiskManager));
            _rfibManager = rfibManager ?? throw new ArgumentNullException(nameof(rfibManager));
            _openPositionTracker = new Dictionary<DateTime, int>();
        }

        #endregion

        #region Core Position Sizing Logic

        /// <summary>
        /// Calculate maximum integer contracts from loss allowance
        /// 
        /// ALGORITHM:
        /// 1. Get remaining daily budget from RFib manager
        /// 2. Apply Tier A-1 budget cap factor (40%)
        /// 3. Calculate max loss per contract for strategy
        /// 4. Derive maximum contracts: floor(allowance / maxLossPerContract)
        /// 5. Apply hard cap: min(derived, HARD_CAP_CONTRACTS)
        /// 6. Apply safety buffer for execution uncertainty
        /// </summary>
        public IntegerPositionResult CalculateMaxContracts(
            DateTime tradingDay,
            StrategySpecification strategySpec)
        {
            try
            {
                // Step 1: Get loss allowance from Tier A-1 system with dynamic fraction
                var remainingBudget = _rfibManager.GetRemainingDailyBudget(tradingDay);
                var dailyCap = _rfibManager.GetDailyBudgetLimit(tradingDay);

                // H2: Dynamic fraction f based on daily cap size
                var fraction = EnableLowCapBoost && dailyCap <= LOW_CAP_THRESHOLD
                    ? LOW_CAP_FRACTION
                    : _perTradeRiskManager.MaxTradeRiskFraction;

                var maxLossAllowance = remainingBudget * (decimal)fraction;

                if (maxLossAllowance <= 0)
                {
                    return new IntegerPositionResult
                    {
                        MaxContracts = 0,
                        IsValid = false,
                        RejectReason = "INSUFFICIENT_BUDGET",
                        RemainingBudget = remainingBudget,
                        MaxLossAllowance = maxLossAllowance,
                        CalculationDetails = "No budget remaining for new positions"
                    };
                }

                // Step 2: Calculate max loss per contract for this strategy
                var maxLossPerContract = MaxLossCalculator.CalculateGenericMaxLoss(strategySpec, 1).MaxLossAmount;

                if (maxLossPerContract <= 0)
                {
                    return new IntegerPositionResult
                    {
                        MaxContracts = 0,
                        IsValid = false,
                        RejectReason = "INVALID_STRATEGY_PARAMS",
                        RemainingBudget = remainingBudget,
                        MaxLossAllowance = maxLossAllowance,
                        CalculationDetails = $"Invalid max loss calculation: ${maxLossPerContract:F2}"
                    };
                }

                // Step 3: Derive maximum contracts (integer floor)
                var derivedMaxContracts = (int)Math.Floor((double)(maxLossAllowance / maxLossPerContract));

                // Step 4: Apply hard cap
                var cappedContracts = Math.Min(derivedMaxContracts, HARD_CAP_CONTRACTS);

                // Step 5: Apply safety buffer (reduce by 5% and floor)
                var bufferAdjustedContracts = (int)Math.Floor(cappedContracts * (double)(1.0m - SAFETY_BUFFER));

                // Step 6: Ensure minimum is 1 (if any trading is allowed)
                var finalContracts = Math.Max(bufferAdjustedContracts,
                    cappedContracts > 0 ? MIN_CONTRACTS : 0);

                // H3: Scale-to-Fit - try narrower width once if zero contracts
                var usedScaleToFit = false;
                if (finalContracts < MIN_CONTRACTS && EnableScaleToFit && strategySpec.Width > MinWidthPoints)
                {
                    // Try with minimum width
                    var narrowStrategySpec = CreateNarrowedStrategy(strategySpec);
                    var narrowMaxLossPerContract = MaxLossCalculator.CalculateGenericMaxLoss(narrowStrategySpec, 1).MaxLossAmount;

                    if (narrowMaxLossPerContract > 0 && narrowMaxLossPerContract < maxLossPerContract)
                    {
                        // Recalculate with narrower strategy
                        var narrowDerivedMaxContracts = (int)Math.Floor((double)(maxLossAllowance / narrowMaxLossPerContract));
                        var narrowCappedContracts = Math.Min(narrowDerivedMaxContracts, HARD_CAP_CONTRACTS);
                        var narrowBufferAdjustedContracts = (int)Math.Floor(narrowCappedContracts * (double)(1.0m - SAFETY_BUFFER));
                        var narrowFinalContracts = Math.Max(narrowBufferAdjustedContracts, narrowCappedContracts > 0 ? MIN_CONTRACTS : 0);

                        if (narrowFinalContracts >= MIN_CONTRACTS)
                        {
                            finalContracts = narrowFinalContracts;
                            maxLossPerContract = narrowMaxLossPerContract;
                            derivedMaxContracts = narrowDerivedMaxContracts;
                            cappedContracts = narrowCappedContracts;
                            bufferAdjustedContracts = narrowBufferAdjustedContracts;
                            usedScaleToFit = true;
                        }
                    }
                }

                // H1: Probe 1-Lot Rule - if still zero contracts but 1 fits in absolute budget
                if (finalContracts < MIN_CONTRACTS && EnableProbeTradeRule)
                {
                    var hasOpenPositions = GetOpenPositionCount(tradingDay) > 0;
                    var canProbe = !ProbeOnlyWhenNoPositions || !hasOpenPositions;

                    if (canProbe && maxLossPerContract <= remainingBudget)
                    {
                        finalContracts = MIN_CONTRACTS;
                    }
                }

                var isValid = finalContracts >= MIN_CONTRACTS;

                return new IntegerPositionResult
                {
                    MaxContracts = finalContracts,
                    IsValid = isValid,
                    RejectReason = isValid ? null : "INSUFFICIENT_SIZE_AFTER_CAPS",
                    RemainingBudget = remainingBudget,
                    MaxLossAllowance = maxLossAllowance,
                    MaxLossPerContract = maxLossPerContract,
                    DerivedMaxContracts = derivedMaxContracts,
                    HardCapApplied = derivedMaxContracts > HARD_CAP_CONTRACTS,
                    SafetyBufferApplied = bufferAdjustedContracts < cappedContracts,
                    UsedProbeTrade = EnableProbeTradeRule && finalContracts == MIN_CONTRACTS && derivedMaxContracts == 0 && !usedScaleToFit,
                    UsedDynamicFraction = fraction != _perTradeRiskManager.MaxTradeRiskFraction,
                    UsedScaleToFit = usedScaleToFit,
                    AppliedFraction = fraction,
                    CalculationDetails = $"Budget: ${remainingBudget:F2} → Fraction: {fraction:P0} → Allowance: ${maxLossAllowance:F2} → " +
                                       $"PerContract: ${maxLossPerContract:F2} → Derived: {derivedMaxContracts} → " +
                                       $"Capped: {cappedContracts} → Final: {finalContracts}" +
                                       (usedScaleToFit ? " [SCALED]" : "") +
                                       (finalContracts == MIN_CONTRACTS && derivedMaxContracts == 0 && !usedScaleToFit ? " [PROBE]" : "")
                };
            }
            catch (Exception ex)
            {
                return new IntegerPositionResult
                {
                    MaxContracts = 0,
                    IsValid = false,
                    RejectReason = "CALCULATION_ERROR",
                    CalculationDetails = $"Exception in position sizing: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Validate specific contract count against calculated limits
        /// </summary>
        public IntegerPositionValidation ValidateContractCount(
            DateTime tradingDay,
            StrategySpecification strategySpec,
            int proposedContracts)
        {
            var maxContractsResult = CalculateMaxContracts(tradingDay, strategySpec);

            if (!maxContractsResult.IsValid)
            {
                return new IntegerPositionValidation
                {
                    IsValid = false,
                    ValidatedContracts = 0,
                    RejectReason = maxContractsResult.RejectReason,
                    ValidationDetails = $"Max contracts calculation failed: {maxContractsResult.CalculationDetails}"
                };
            }

            if (proposedContracts <= 0)
            {
                return new IntegerPositionValidation
                {
                    IsValid = false,
                    ValidatedContracts = 0,
                    RejectReason = "INVALID_PROPOSED_COUNT",
                    ValidationDetails = $"Proposed contracts {proposedContracts} must be ≥ 1"
                };
            }

            if (proposedContracts > maxContractsResult.MaxContracts)
            {
                return new IntegerPositionValidation
                {
                    IsValid = false,
                    ValidatedContracts = maxContractsResult.MaxContracts,
                    RejectReason = "EXCEEDS_MAX_CONTRACTS",
                    ValidationDetails = $"Proposed {proposedContracts} exceeds max {maxContractsResult.MaxContracts}"
                };
            }

            // Valid - proposed contracts are within limits
            return new IntegerPositionValidation
            {
                IsValid = true,
                ValidatedContracts = proposedContracts,
                MaxAllowedContracts = maxContractsResult.MaxContracts,
                ValidationDetails = $"Validated {proposedContracts} contracts (max: {maxContractsResult.MaxContracts})"
            };
        }

        /// <summary>
        /// Get recommended contract size based on strategy and market conditions
        /// </summary>
        public IntegerPositionRecommendation GetRecommendedContracts(
            DateTime tradingDay,
            StrategySpecification strategySpec,
            MarketConditions marketConditions)
        {
            var maxContractsResult = CalculateMaxContracts(tradingDay, strategySpec);

            if (!maxContractsResult.IsValid)
            {
                return new IntegerPositionRecommendation
                {
                    RecommendedContracts = 0,
                    MaxContracts = 0,
                    RecommendationReason = maxContractsResult.RejectReason ?? "UNKNOWN_ERROR"
                };
            }

            // Calculate recommended size based on market conditions
            var maxContracts = maxContractsResult.MaxContracts;
            var recommendedContracts = CalculateMarketAdjustedSize(maxContracts, marketConditions);

            return new IntegerPositionRecommendation
            {
                RecommendedContracts = recommendedContracts,
                MaxContracts = maxContracts,
                RecommendationReason = GetRecommendationReason(recommendedContracts, maxContracts, marketConditions)
            };
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Adjust position size based on market conditions
        /// </summary>
        private int CalculateMarketAdjustedSize(int maxContracts, MarketConditions marketConditions)
        {
            if (maxContracts <= 0) return 0;

            // Conservative adjustment factors based on VIX as market stress proxy
            var marketStress = marketConditions.VIX / 30.0; // Normalize VIX to stress factor
            var adjustmentFactor = marketStress switch
            {
                <= 0.3 => 1.0,      // Low stress: full size
                <= 0.5 => 0.8,      // Medium stress: 80% of max
                <= 0.7 => 0.6,      // High stress: 60% of max
                _ => 0.4             // Extreme stress: 40% of max
            };

            // Apply volatility adjustment
            if (marketConditions.ImpliedVolatility > 0.30) // High IV
            {
                adjustmentFactor *= 0.9; // Further reduction
            }

            // Calculate adjusted size and ensure minimum of 1
            var adjustedSize = (int)Math.Floor(maxContracts * adjustmentFactor);
            return Math.Max(MIN_CONTRACTS, adjustedSize);
        }

        /// <summary>
        /// Generate explanation for recommendation
        /// </summary>
        private string GetRecommendationReason(int recommended, int max, MarketConditions marketConditions)
        {
            if (recommended == max)
            {
                return "FULL_SIZE_RECOMMENDED";
            }
            else if (recommended == MIN_CONTRACTS)
            {
                return "MINIMUM_SIZE_DUE_TO_STRESS";
            }
            else
            {
                var reductionPct = (1.0 - (double)recommended / max) * 100;
                return $"REDUCED_BY_{reductionPct:F0}PCT_FOR_MARKET_CONDITIONS";
            }
        }

        /// <summary>
        /// Create a narrowed version of strategy specification for scale-to-fit
        /// </summary>
        private StrategySpecification CreateNarrowedStrategy(StrategySpecification original)
        {
            var narrowed = new StrategySpecification
            {
                StrategyType = original.StrategyType,
                Width = MinWidthPoints,
                NetCredit = original.NetCredit * (MinWidthPoints / original.Width) // Proportionally adjust credit
            };

            // Adjust type-specific parameters
            switch (original.StrategyType)
            {
                case StrategyType.IronCondor:
                    narrowed.PutWidth = MinWidthPoints;
                    narrowed.CallWidth = MinWidthPoints;
                    break;
                case StrategyType.CreditBWB:
                    narrowed.BodyWidth = MinWidthPoints * 0.7m;
                    narrowed.WingWidth = MinWidthPoints * 0.3m;
                    break;
                case StrategyType.CreditSpread:
                    // Width already set
                    break;
            }

            return narrowed;
        }

        #endregion

        #region Position Tracking

        /// <summary>
        /// Record that a position was opened
        /// </summary>
        public void RecordPositionOpened(DateTime tradingDay)
        {
            var day = tradingDay.Date;
            if (!_openPositionTracker.ContainsKey(day))
                _openPositionTracker[day] = 0;
            _openPositionTracker[day]++;
        }

        /// <summary>
        /// Record that a position was closed
        /// </summary>
        public void RecordPositionClosed(DateTime tradingDay)
        {
            var day = tradingDay.Date;
            if (_openPositionTracker.ContainsKey(day) && _openPositionTracker[day] > 0)
                _openPositionTracker[day]--;
        }

        /// <summary>
        /// Get current count of open positions for a trading day
        /// </summary>
        private int GetOpenPositionCount(DateTime tradingDay)
        {
            var day = tradingDay.Date;
            return _openPositionTracker.ContainsKey(day) ? _openPositionTracker[day] : 0;
        }

        #endregion

        #region Status Methods

        /// <summary>
        /// Get current status of position sizing constraints
        /// </summary>
        public IntegerPositionSizerStatus GetCurrentStatus(DateTime tradingDay)
        {
            var remainingBudget = _rfibManager.GetRemainingDailyBudget(tradingDay);
            var maxLossAllowance = remainingBudget * (decimal)_perTradeRiskManager.MaxTradeRiskFraction;

            return new IntegerPositionSizerStatus
            {
                TradingDay = tradingDay,
                RemainingBudget = remainingBudget,
                MaxLossAllowance = maxLossAllowance,
                HardCapContracts = HARD_CAP_CONTRACTS,
                MinContracts = MIN_CONTRACTS,
                SafetyBufferPercentage = SAFETY_BUFFER * 100m,
                CanTrade = maxLossAllowance > 0
            };
        }

        #endregion
    }

    #region Supporting Data Types

    /// <summary>Result of maximum contract calculation</summary>
    public class IntegerPositionResult
    {
        public int MaxContracts { get; set; }
        public bool IsValid { get; set; }
        public string? RejectReason { get; set; }
        public decimal RemainingBudget { get; set; }
        public decimal MaxLossAllowance { get; set; }
        public decimal MaxLossPerContract { get; set; }
        public int DerivedMaxContracts { get; set; }
        public bool HardCapApplied { get; set; }
        public bool SafetyBufferApplied { get; set; }
        public bool UsedProbeTrade { get; set; }
        public bool UsedDynamicFraction { get; set; }
        public bool UsedScaleToFit { get; set; }
        public double AppliedFraction { get; set; }
        public string CalculationDetails { get; set; } = "";
    }

    /// <summary>Result of contract count validation</summary>
    public class IntegerPositionValidation
    {
        public bool IsValid { get; set; }
        public int ValidatedContracts { get; set; }
        public int MaxAllowedContracts { get; set; }
        public string? RejectReason { get; set; }
        public string ValidationDetails { get; set; } = "";
    }

    /// <summary>Recommended position size with reasoning</summary>
    public class IntegerPositionRecommendation
    {
        public int RecommendedContracts { get; set; }
        public int MaxContracts { get; set; }
        public string RecommendationReason { get; set; } = "";
    }

    /// <summary>Current status of position sizer</summary>
    public class IntegerPositionSizerStatus
    {
        public DateTime TradingDay { get; set; }
        public decimal RemainingBudget { get; set; }
        public decimal MaxLossAllowance { get; set; }
        public int HardCapContracts { get; set; }
        public int MinContracts { get; set; }
        public decimal SafetyBufferPercentage { get; set; }
        public bool CanTrade { get; set; }
    }

    #endregion
}