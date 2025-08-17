namespace ODTE.Strategy.RiskManagement
{
    /// <summary>
    /// Maximum Loss at Entry Calculator (Tier A-1.2 Enhancement)
    /// 
    /// PURPOSE: Accurately calculate worst-case loss before trade execution
    /// KEY IMPROVEMENT: Prevents position sizing disasters by knowing exact risk exposure
    /// 
    /// CRITICAL FOR:
    /// - Budget validation (A1.1): Need precise risk amount for RFib budget check
    /// - Position sizing (A2): Calculate contract caps from loss allowance
    /// - Stop loss management (A4): Adaptive stops based on structure risk
    /// 
    /// CALCULATION METHODS:
    /// - Iron Condor: Max loss = Width - NetCredit + Slippage buffer
    /// - Credit BWB: Max loss = WingWidth - NetCredit + Slippage buffer  
    /// - Generic: Support any spread structure with defined risk
    /// </summary>
    public static class MaxLossCalculator
    {
        #region Configuration Constants

        /// <summary>
        /// Default slippage buffer as percentage of net credit
        /// Conservative: 5% to account for execution slippage
        /// </summary>
        public const double DEFAULT_SLIPPAGE_BUFFER = 0.05;

        /// <summary>
        /// Minimum slippage buffer in absolute terms
        /// Ensures some buffer even for high-credit trades
        /// </summary>
        public const decimal MIN_SLIPPAGE_BUFFER = 2.00m;

        /// <summary>
        /// Commission estimation per contract (round trip)
        /// Typical broker: $1.30 per contract
        /// </summary>
        public const decimal COMMISSION_PER_CONTRACT = 1.30m;

        #endregion

        #region Iron Condor Calculations

        /// <summary>
        /// Calculate maximum loss for Iron Condor strategy
        /// </summary>
        /// <param name="putWidth">Put spread width in points</param>
        /// <param name="callWidth">Call spread width in points</param>
        /// <param name="netCredit">Net credit received</param>
        /// <param name="contracts">Number of contracts</param>
        /// <param name="slippageBuffer">Slippage buffer percentage (default 5%)</param>
        /// <returns>Maximum possible loss including slippage and commissions</returns>
        public static decimal CalculateIronCondorMaxLoss(
            decimal putWidth,
            decimal callWidth,
            decimal netCredit,
            int contracts,
            double slippageBuffer = DEFAULT_SLIPPAGE_BUFFER)
        {
            // Iron Condor max loss = max(putWidth, callWidth) - netCredit
            var maxWidth = Math.Max(putWidth, callWidth);
            var theoreticalMaxLoss = (maxWidth - netCredit) * 100m; // Convert to dollars

            // Add slippage buffer
            var slippageAmount = Math.Max(
                (decimal)slippageBuffer * netCredit * 100m,
                MIN_SLIPPAGE_BUFFER
            );

            // Add commissions
            var commissions = COMMISSION_PER_CONTRACT * contracts;

            // Total max loss per contract
            var maxLossPerContract = theoreticalMaxLoss + slippageAmount + (commissions / contracts);

            return maxLossPerContract * contracts;
        }

        #endregion

        #region Credit Broken Wing Butterfly Calculations

        /// <summary>
        /// Calculate maximum loss for Credit Broken Wing Butterfly
        /// </summary>
        /// <param name="bodyWidth">Width between short strikes</param>
        /// <param name="wingWidth">Width from short to long wings</param>
        /// <param name="netCredit">Net credit received</param>
        /// <param name="contracts">Number of contracts</param>
        /// <param name="slippageBuffer">Slippage buffer percentage</param>
        /// <returns>Maximum possible loss including buffers</returns>
        public static decimal CalculateBWBMaxLoss(
            decimal bodyWidth,
            decimal wingWidth,
            decimal netCredit,
            int contracts,
            double slippageBuffer = DEFAULT_SLIPPAGE_BUFFER)
        {
            // BWB max loss typically occurs at the long wing
            // Max loss = wingWidth - netCredit (if breached to upside)
            var theoreticalMaxLoss = (wingWidth - netCredit) * 100m;

            // Add slippage buffer
            var slippageAmount = Math.Max(
                (decimal)slippageBuffer * netCredit * 100m,
                MIN_SLIPPAGE_BUFFER
            );

            // Add commissions (BWB typically has 4 legs)
            var commissions = COMMISSION_PER_CONTRACT * contracts * 1.5m; // 50% higher for complexity

            var maxLossPerContract = theoreticalMaxLoss + slippageAmount + (commissions / contracts);

            return maxLossPerContract * contracts;
        }

        #endregion

        #region Generic Strategy Calculations

        /// <summary>
        /// Calculate maximum loss for any defined-risk strategy
        /// </summary>
        /// <param name="strategySpec">Strategy specification with risk parameters</param>
        /// <param name="contracts">Number of contracts</param>
        /// <param name="customSlippageBuffer">Custom slippage buffer if needed</param>
        /// <returns>Maximum loss calculation result</returns>
        public static MaxLossResult CalculateGenericMaxLoss(
            StrategySpecification strategySpec,
            int contracts,
            double? customSlippageBuffer = null)
        {
            var slippageBuffer = customSlippageBuffer ?? DEFAULT_SLIPPAGE_BUFFER;

            decimal maxLoss = strategySpec.StrategyType switch
            {
                StrategyType.IronCondor => CalculateIronCondorMaxLoss(
                    strategySpec.PutWidth,
                    strategySpec.CallWidth,
                    strategySpec.NetCredit,
                    contracts,
                    slippageBuffer),

                StrategyType.CreditBWB => CalculateBWBMaxLoss(
                    strategySpec.BodyWidth,
                    strategySpec.WingWidth,
                    strategySpec.NetCredit,
                    contracts,
                    slippageBuffer),

                StrategyType.CreditSpread => CalculateCreditSpreadMaxLoss(
                    strategySpec.Width,
                    strategySpec.NetCredit,
                    contracts,
                    slippageBuffer),

                _ => throw new NotSupportedException($"Strategy type {strategySpec.StrategyType} not supported")
            };

            return new MaxLossResult
            {
                StrategyType = strategySpec.StrategyType,
                Contracts = contracts,
                MaxLossAmount = maxLoss,
                NetCredit = strategySpec.NetCredit,
                SlippageBuffer = slippageBuffer,
                CommissionIncluded = true,
                CalculationTimestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Calculate maximum loss for simple credit spread
        /// </summary>
        private static decimal CalculateCreditSpreadMaxLoss(
            decimal width,
            decimal netCredit,
            int contracts,
            double slippageBuffer)
        {
            var theoreticalMaxLoss = (width - netCredit) * 100m;

            var slippageAmount = Math.Max(
                (decimal)slippageBuffer * netCredit * 100m,
                MIN_SLIPPAGE_BUFFER
            );

            var commissions = COMMISSION_PER_CONTRACT * contracts;
            var maxLossPerContract = theoreticalMaxLoss + slippageAmount + (commissions / contracts);

            return maxLossPerContract * contracts;
        }

        #endregion

        #region Risk Ratio Calculations

        /// <summary>
        /// Calculate risk-to-reward ratio for trade evaluation
        /// </summary>
        /// <param name="maxLoss">Maximum possible loss</param>
        /// <param name="netCredit">Net credit received</param>
        /// <param name="contracts">Number of contracts</param>
        /// <returns>Risk ratio metrics</returns>
        public static RiskRatioMetrics CalculateRiskRatios(decimal maxLoss, decimal netCredit, int contracts)
        {
            var totalCredit = netCredit * 100m * contracts; // Convert to dollars
            var riskToReward = totalCredit > 0 ? (double)(maxLoss / totalCredit) : double.MaxValue;
            var returnOnRisk = maxLoss > 0 ? (double)(totalCredit / maxLoss) : 0;

            return new RiskRatioMetrics
            {
                MaxLoss = maxLoss,
                TotalCredit = totalCredit,
                RiskToRewardRatio = riskToReward,
                ReturnOnRisk = returnOnRisk,
                BreakevenMove = CalculateBreakevenMove(netCredit, maxLoss / contracts / 100m),
                RiskGrade = GetRiskGrade(riskToReward)
            };
        }

        private static decimal CalculateBreakevenMove(decimal netCredit, decimal maxLossPerContract)
        {
            // Approximate breakeven move as percentage
            // For credit strategies, this is typically the credit as % of underlying
            return netCredit; // Simplified - would need underlying price for exact calculation
        }

        private static RiskGrade GetRiskGrade(double riskToReward)
        {
            return riskToReward switch
            {
                <= 2.0 => RiskGrade.Conservative,
                <= 3.0 => RiskGrade.Moderate,
                <= 4.5 => RiskGrade.Aggressive,
                _ => RiskGrade.Speculative
            };
        }

        #endregion
    }

    #region Supporting Types

    /// <summary>
    /// Strategy specification for max loss calculations
    /// </summary>
    public class StrategySpecification
    {
        public StrategyType StrategyType { get; set; }
        public decimal NetCredit { get; set; }
        public decimal Width { get; set; }
        public decimal PutWidth { get; set; }
        public decimal CallWidth { get; set; }
        public decimal BodyWidth { get; set; }
        public decimal WingWidth { get; set; }
    }

    /// <summary>
    /// Strategy types supported
    /// </summary>
    public enum StrategyType
    {
        IronCondor,
        CreditBWB,
        CreditSpread,
        DebitSpread,
        Straddle,
        Strangle
    }

    /// <summary>
    /// Result of maximum loss calculation
    /// </summary>
    public class MaxLossResult
    {
        public StrategyType StrategyType { get; set; }
        public int Contracts { get; set; }
        public decimal MaxLossAmount { get; set; }
        public decimal NetCredit { get; set; }
        public double SlippageBuffer { get; set; }
        public bool CommissionIncluded { get; set; }
        public DateTime CalculationTimestamp { get; set; }

        public string GetSummary()
        {
            return $"{StrategyType}: {Contracts} contracts, Max Loss: ${MaxLossAmount:F2}, " +
                   $"Credit: ${NetCredit * 100 * Contracts:F2}, " +
                   $"Risk/Reward: {(double)(MaxLossAmount / (NetCredit * 100 * Contracts)):F2}";
        }
    }

    /// <summary>
    /// Risk ratio analysis metrics
    /// </summary>
    public class RiskRatioMetrics
    {
        public decimal MaxLoss { get; set; }
        public decimal TotalCredit { get; set; }
        public double RiskToRewardRatio { get; set; }
        public double ReturnOnRisk { get; set; }
        public decimal BreakevenMove { get; set; }
        public RiskGrade RiskGrade { get; set; }

        public string GetAnalysis()
        {
            return $"Risk Grade: {RiskGrade}, R:R Ratio: {RiskToRewardRatio:F2}, " +
                   $"Return on Risk: {ReturnOnRisk:P2}, Max Loss: ${MaxLoss:F2}";
        }
    }

    /// <summary>
    /// Risk classification grades
    /// </summary>
    public enum RiskGrade
    {
        Conservative,  // R:R <= 2.0
        Moderate,      // R:R 2.0-3.0
        Aggressive,    // R:R 3.0-4.5
        Speculative    // R:R > 4.5
    }

    #endregion
}