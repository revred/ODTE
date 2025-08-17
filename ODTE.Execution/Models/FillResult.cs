namespace ODTE.Execution.Models;

/// <summary>
/// Result of fill simulation with complete execution details and audit trail.
/// </summary>
public record FillResult
{
    public string OrderId { get; init; } = "";
    public DateTime FillTimestamp { get; init; }
    public List<ChildFill> ChildFills { get; init; } = new();
    public ExecutionDiagnostics Diagnostics { get; init; } = new();

    /// <summary>
    /// Average fill price across all child fills
    /// </summary>
    public decimal AverageFillPrice => ChildFills.Count > 0
        ? ChildFills.Sum(f => f.Price * f.Quantity) / ChildFills.Sum(f => f.Quantity)
        : 0m;

    /// <summary>
    /// Total quantity filled
    /// </summary>
    public int TotalQuantity => ChildFills.Sum(f => f.Quantity);

    /// <summary>
    /// Whether fill achieved mid-price or better
    /// </summary>
    public bool WasMidOrBetter { get; init; }

    /// <summary>
    /// Whether fill was within NBBO band (±$0.01)
    /// </summary>
    public bool WasWithinNbbo { get; init; }

    /// <summary>
    /// Slippage from intended price in dollars per contract
    /// </summary>
    public decimal SlippagePerContract { get; init; }

    /// <summary>
    /// Total execution cost including slippage and fees
    /// </summary>
    public decimal TotalExecutionCost { get; init; }
}

/// <summary>
/// Individual child fill from order splitting for participation limits
/// </summary>
public record ChildFill
{
    public int SequenceNumber { get; init; }
    public decimal Price { get; init; }
    public int Quantity { get; init; }
    public DateTime Timestamp { get; init; }
    public bool WasMidAttempt { get; init; }
    public bool WasMidAccepted { get; init; }
    public decimal LatencyMs { get; init; }
    public decimal SlippageApplied { get; init; }
    public decimal AdverseSelectionCost { get; init; }
    public decimal SizePenaltyCost { get; init; }
}

/// <summary>
/// Execution diagnostics for audit and analysis
/// </summary>
public record ExecutionDiagnostics
{
    public decimal IntendedPrice { get; init; }
    public decimal AchievedPrice { get; init; }
    public decimal TotalLatencyMs { get; init; }
    public int MidAttempts { get; init; }
    public int MidAccepted { get; init; }
    public decimal TotalAdverseSelection { get; init; }
    public decimal TotalSizePenalty { get; init; }
    public decimal TotalSlippageFloor { get; init; }
    public string ExecutionProfile { get; init; } = "";
    public Quote StartQuote { get; init; } = new();
    public Quote? EndQuote { get; init; }
    public List<string> Warnings { get; init; } = new();

    /// <summary>
    /// Calculate execution quality score (0-100)
    /// </summary>
    public decimal ExecutionQualityScore
    {
        get
        {
            var score = 100m;

            // Penalty for excessive slippage
            var slippageRatio = Math.Abs(AchievedPrice - IntendedPrice) / IntendedPrice;
            score -= slippageRatio * 1000m; // 10 points per 1% slippage

            // Penalty for high latency
            if (TotalLatencyMs > 500)
                score -= (TotalLatencyMs - 500) / 100; // 1 point per 100ms over 500ms

            // Bonus for mid-fills
            if (MidAttempts > 0)
                score += (decimal)MidAccepted / MidAttempts * 10; // Up to 10 bonus points

            return Math.Max(0, Math.Min(100, score));
        }
    }
}

/// <summary>
/// Daily execution metrics for audit compliance
/// </summary>
public record ExecutionMetrics
{
    public DateTime Date { get; init; }
    public int TotalFills { get; init; }
    public int MidOrBetterFills { get; init; }
    public int WithinNbboFills { get; init; }
    public decimal AverageLatencyMs { get; init; }
    public decimal AverageSlippageBps { get; init; }
    public decimal TotalNotional { get; init; }

    /// <summary>
    /// Percentage of fills at mid-price or better (target < 60%)
    /// </summary>
    public decimal MidRate => TotalFills > 0 ? (decimal)MidOrBetterFills / TotalFills * 100m : 0m;

    /// <summary>
    /// Percentage of fills within NBBO band (target ≥ 98%)
    /// </summary>
    public decimal NbboComplianceRate => TotalFills > 0 ? (decimal)WithinNbboFills / TotalFills * 100m : 0m;

    /// <summary>
    /// Check if metrics meet institutional acceptance criteria
    /// </summary>
    public bool MeetsAuditCriteria => MidRate < 60m && NbboComplianceRate >= 98m;
}

/// <summary>
/// Slippage sensitivity analysis result
/// </summary>
public record SlippageAnalysis
{
    public decimal OriginalProfitFactor { get; init; }
    public decimal ProfitFactorWith5c { get; init; }
    public decimal ProfitFactorWith10c { get; init; }
    public bool Passes5cThreshold => ProfitFactorWith5c >= 1.30m;
    public bool Passes10cThreshold => ProfitFactorWith10c >= 1.15m;
    public bool PassesSlippageTest => Passes5cThreshold && Passes10cThreshold;
}