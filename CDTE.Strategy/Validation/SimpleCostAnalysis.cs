using System.Text.Json;

namespace CDTE.Strategy.Validation;

/// <summary>
/// Simple Cost Analysis
/// Quick realistic cost validation of genetic optimization results
/// Determines if 58% CAGR is achievable with authentic trading costs
/// </summary>
public class SimpleCostAnalysis
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("💰 CDTE Simple Cost Analysis");
        Console.WriteLine("Realistic Trading Cost Impact on 58% CAGR");
        Console.WriteLine("=========================================");
        Console.WriteLine();

        try
        {
            // Load optimization results
            var optimizedResults = LoadOptimizationResults();
            
            Console.WriteLine("📊 GENETIC OPTIMIZATION RESULTS:");
            Console.WriteLine($"   Achieved CAGR:           {optimizedResults.CAGR:P2}");
            Console.WriteLine($"   Position Size:           {optimizedResults.PositionSize:P2}");
            Console.WriteLine($"   Take Profit:             {optimizedResults.TakeProfit:P1}");
            Console.WriteLine($"   Risk Cap:                ${optimizedResults.RiskCap:F0}");
            Console.WriteLine($"   Strategy Mix:            {optimizedResults.StrategyDescription}");
            Console.WriteLine();

            // Calculate realistic trading costs
            var costAnalysis = CalculateRealisticCosts(optimizedResults);
            
            Console.WriteLine("💸 REALISTIC TRADING COST ANALYSIS:");
            Console.WriteLine($"   Bid-Ask Spread Cost:     {costAnalysis.BidAskImpact:P2} annually");
            Console.WriteLine($"   Commission Cost:         {costAnalysis.CommissionImpact:P2} annually");
            Console.WriteLine($"   Slippage Cost:           {costAnalysis.SlippageImpact:P2} annually");
            Console.WriteLine($"   Assignment Risk:         {costAnalysis.AssignmentImpact:P2} annually");
            Console.WriteLine($"   Liquidity Penalty:       {costAnalysis.LiquidityImpact:P2} annually");
            Console.WriteLine($"   ────────────────────────────────────────");
            Console.WriteLine($"   Total Cost Impact:       {costAnalysis.TotalCostImpact:P2} annually");
            Console.WriteLine();

            // Calculate cost-adjusted performance
            var adjustedCAGR = optimizedResults.CAGR - costAnalysis.TotalCostImpact;
            var costReduction = costAnalysis.TotalCostImpact / optimizedResults.CAGR;
            
            Console.WriteLine("🎯 COST-ADJUSTED PERFORMANCE:");
            Console.WriteLine($"   Original CAGR:           {optimizedResults.CAGR:P2} (genetic algorithm)");
            Console.WriteLine($"   Cost-Adjusted CAGR:      {adjustedCAGR:P2} ⭐");
            Console.WriteLine($"   Performance Reduction:   {costReduction:P1}");
            Console.WriteLine($"   30% Target Achieved:     {(adjustedCAGR >= 0.30 ? "✅ YES" : "❌ NO")}");
            Console.WriteLine();

            // Detailed cost breakdown analysis
            DisplayDetailedCostAnalysis(costAnalysis, optimizedResults);
            
            // Final verdict
            DisplayFinalVerdict(adjustedCAGR, costAnalysis.TotalCostImpact, optimizedResults.CAGR);
            
            // Save results
            await SaveAnalysisResults(optimizedResults, costAnalysis, adjustedCAGR);
            
            Console.WriteLine();
            Console.WriteLine($"💾 Analysis saved to simple_cost_analysis_results.json");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error during cost analysis: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Load optimization results from genetic algorithm
    /// </summary>
    private static OptimizedResults LoadOptimizationResults()
    {
        // Based on actual genetic algorithm results from optimization run
        return new OptimizedResults
        {
            CAGR = 0.5813, // 58.13% from genetic optimization
            PositionSize = 0.0205, // 2.05% position size
            TakeProfit = 0.868, // 86.8% take profit
            RiskCap = 1130, // $1,130 risk cap
            ICWeight = 0.496, // 49.6% Iron Condor
            IFWeight = 0.423, // 42.3% Iron Fly  
            BWBWeight = 0.080, // 8.0% Broken Wing Butterfly
            StrategyDescription = "49.6% IC, 42.3% IF, 8.0% BWB",
            WeeklyTrades = 50, // Estimated weekly trades per year
            AverageContracts = 5 // Estimated contracts per trade
        };
    }

    /// <summary>
    /// Calculate realistic trading costs based on optimized strategy
    /// </summary>
    private static CostAnalysis CalculateRealisticCosts(OptimizedResults results)
    {
        var analysis = new CostAnalysis();
        
        // 1. Bid-Ask Spread Costs
        // SPX weekly options typically have $0.10-0.25 spreads
        // Multi-leg spreads cross multiple bid-ask spreads
        var avgSpreadPerLeg = 0.15m; // $0.15 average spread per option leg
        var legsPerTrade = 4; // Iron Condor/Fly = 4 legs, BWB = 3-4 legs
        var spreadCostPerTrade = avgSpreadPerLeg * legsPerTrade * 0.5m; // Cross 50% of spread
        var annualSpreadCost = spreadCostPerTrade * results.WeeklyTrades * results.AverageContracts;
        var portfolioValue = 100000m; // Base portfolio value
        analysis.BidAskImpact = (double)(annualSpreadCost / portfolioValue);

        // 2. Commission Costs  
        // $0.65 per contract + $0.50 base fee per trade
        var commissionPerContract = 0.65m;
        var baseFeePerTrade = 0.50m;
        var contractsPerTrade = results.AverageContracts * legsPerTrade;
        var commissionPerTrade = (commissionPerContract * contractsPerTrade) + baseFeePerTrade;
        var annualCommissions = commissionPerTrade * results.WeeklyTrades;
        analysis.CommissionImpact = (double)(annualCommissions / portfolioValue);

        // 3. Slippage Costs
        // Market impact increases with position size
        var baseSlippageRate = 0.001; // 0.1% base slippage
        var positionSizeMultiplier = 1.0 + (results.PositionSize - 0.015) * 5; // Higher for larger positions
        var volatilityMultiplier = 1.2; // SPX weekly options have higher slippage
        var slippageRate = baseSlippageRate * positionSizeMultiplier * volatilityMultiplier;
        analysis.SlippageImpact = slippageRate;

        // 4. Assignment Risk
        // Short option positions face early assignment risk
        var shortLegsProportion = 0.5; // 50% of legs are short in spreads
        var assignmentProbability = 0.05; // 5% annual assignment probability
        var assignmentCost = 20m; // $20 assignment fee + impact
        var annualAssignmentCost = (decimal)results.WeeklyTrades * (decimal)results.AverageContracts * (decimal)shortLegsProportion * 
                                  (decimal)assignmentProbability * assignmentCost;
        analysis.AssignmentImpact = (double)(annualAssignmentCost / portfolioValue);

        // 5. Liquidity Impact
        // SPX weeklies have good liquidity, but aggressive position sizing increases impact
        var liquidityPenalty = results.PositionSize > 0.02 ? (results.PositionSize - 0.02) * 10.0 : 0.0; // Penalty for >2%
        analysis.LiquidityImpact = liquidityPenalty;

        // Total cost impact
        analysis.TotalCostImpact = analysis.BidAskImpact + analysis.CommissionImpact + 
                                  analysis.SlippageImpact + analysis.AssignmentImpact + analysis.LiquidityImpact;

        return analysis;
    }

    /// <summary>
    /// Display detailed cost breakdown analysis
    /// </summary>
    private static void DisplayDetailedCostAnalysis(CostAnalysis costAnalysis, OptimizedResults results)
    {
        Console.WriteLine("🔍 DETAILED COST BREAKDOWN ANALYSIS:");
        Console.WriteLine();

        // Rank costs by impact
        var costComponents = new[]
        {
            ("Bid-Ask Spreads", costAnalysis.BidAskImpact),
            ("Slippage & Market Impact", costAnalysis.SlippageImpact), 
            ("Commission Fees", costAnalysis.CommissionImpact),
            ("Liquidity Penalty", costAnalysis.LiquidityImpact),
            ("Assignment Risk", costAnalysis.AssignmentImpact)
        }.OrderByDescending(c => c.Item2).ToArray();

        Console.WriteLine("   📊 COST RANKING (Highest to Lowest Impact):");
        for (int i = 0; i < costComponents.Length; i++)
        {
            var (name, impact) = costComponents[i];
            var percentage = impact / costAnalysis.TotalCostImpact;
            Console.WriteLine($"   {i + 1}. {name,-25}: {impact:P2} ({percentage:P1} of total costs)");
        }
        Console.WriteLine();

        // Cost driver analysis
        Console.WriteLine("   🎯 PRIMARY COST DRIVERS:");
        if (costAnalysis.BidAskImpact > costAnalysis.TotalCostImpact * 0.4)
        {
            Console.WriteLine("   • Bid-ask spreads are the dominant cost (>40% of total)");
            Console.WriteLine("   • Focus on execution timing and order management");
            Console.WriteLine("   • Consider limit orders vs. market orders");
        }

        if (costAnalysis.SlippageImpact > costAnalysis.TotalCostImpact * 0.3)
        {
            Console.WriteLine("   • High slippage due to aggressive position sizing");
            Console.WriteLine("   • 2.05% position size may be too large for consistent execution");
            Console.WriteLine("   • Consider reducing to 1.5% or implementing gradual scaling");
        }

        if (costAnalysis.CommissionImpact > costAnalysis.TotalCostImpact * 0.2)
        {
            Console.WriteLine("   • Commission costs are significant (>20% of total)");
            Console.WriteLine("   • Multi-leg spreads increase per-trade commission costs");
            Console.WriteLine("   • Optimize for commission-efficient brokers");
        }

        Console.WriteLine();

        // Strategy-specific insights
        Console.WriteLine("   💡 STRATEGY-SPECIFIC COST INSIGHTS:");
        Console.WriteLine($"   • {results.StrategyDescription} requires 4-leg execution");
        Console.WriteLine($"   • Iron Condor/Fly strategies have symmetric cost structure");
        Console.WriteLine($"   • {results.TakeProfit:P1} take profit reduces assignment risk");
        Console.WriteLine($"   • Weekly expiration provides theta acceleration but tight execution windows");
        Console.WriteLine();
    }

    /// <summary>
    /// Display final verdict on strategy feasibility
    /// </summary>
    private static void DisplayFinalVerdict(double adjustedCAGR, double totalCostImpact, double originalCAGR)
    {
        Console.WriteLine("⚖️ FINAL VERDICT:");
        Console.WriteLine("================");
        Console.WriteLine();

        if (adjustedCAGR >= 0.30)
        {
            Console.WriteLine("✅ STRATEGY VIABLE WITH REALISTIC COSTS");
            Console.WriteLine($"   🎯 Cost-adjusted CAGR of {adjustedCAGR:P2} exceeds 30% target");
            Console.WriteLine($"   💰 Cost impact of {totalCostImpact:P2} is manageable");
            Console.WriteLine($"   📈 Performance reduction of {(totalCostImpact/originalCAGR):P1} is acceptable");
            Console.WriteLine();
            Console.WriteLine("🚀 RECOMMENDED NEXT STEPS:");
            Console.WriteLine("   1. Proceed to paper trading with live market data");
            Console.WriteLine("   2. Validate execution costs match estimates");
            Console.WriteLine("   3. Start with smaller position sizes (1.5% vs 2.05%)");
            Console.WriteLine("   4. Monitor actual bid-ask spreads during execution");
            Console.WriteLine("   5. Scale gradually to full position sizes");
        }
        else if (adjustedCAGR >= 0.25)
        {
            Console.WriteLine("⚠️ STRATEGY REQUIRES OPTIMIZATION");
            Console.WriteLine($"   📊 Cost-adjusted CAGR of {adjustedCAGR:P2} below 30% target");
            Console.WriteLine($"   💸 Cost impact of {totalCostImpact:P2} reduces viability");
            Console.WriteLine($"   🔧 Performance reduction of {(totalCostImpact/originalCAGR):P1} requires attention");
            Console.WriteLine();
            Console.WriteLine("🔧 RECOMMENDED OPTIMIZATIONS:");
            Console.WriteLine("   1. Reduce position size from 2.05% to 1.5%");
            Console.WriteLine("   2. Optimize execution timing to reduce slippage");
            Console.WriteLine("   3. Consider commission-efficient broker platforms");
            Console.WriteLine("   4. Rerun genetic algorithm with cost constraints");
            Console.WriteLine("   5. Test alternative strategy structures");
        }
        else
        {
            Console.WriteLine("❌ STRATEGY NOT VIABLE WITH REALISTIC COSTS");
            Console.WriteLine($"   📉 Cost-adjusted CAGR of {adjustedCAGR:P2} well below 30% target");
            Console.WriteLine($"   💰 Cost impact of {totalCostImpact:P2} severely degrades performance");
            Console.WriteLine($"   🚨 Performance reduction of {(totalCostImpact/originalCAGR):P1} is unacceptable");
            Console.WriteLine();
            Console.WriteLine("🔄 RECOMMENDED ALTERNATIVES:");
            Console.WriteLine("   1. Complete strategy redesign required");
            Console.WriteLine("   2. Consider longer-term options (monthly vs weekly)");
            Console.WriteLine("   3. Explore different underlying assets (ETFs vs index)");
            Console.WriteLine("   4. Reassess return targets and risk parameters");
            Console.WriteLine("   5. Focus on cost reduction before performance optimization");
        }
    }

    /// <summary>
    /// Save analysis results to file
    /// </summary>
    private static async Task SaveAnalysisResults(OptimizedResults results, CostAnalysis costAnalysis, double adjustedCAGR)
    {
        var analysisResults = new
        {
            AnalysisDate = DateTime.UtcNow,
            OriginalResults = results,
            CostAnalysis = costAnalysis,
            CostAdjustedCAGR = adjustedCAGR,
            TargetAchieved = adjustedCAGR >= 0.30,
            PerformanceReduction = costAnalysis.TotalCostImpact / results.CAGR,
            Verdict = adjustedCAGR >= 0.30 ? "VIABLE" : adjustedCAGR >= 0.25 ? "REQUIRES_OPTIMIZATION" : "NOT_VIABLE"
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(analysisResults, options);
        await File.WriteAllTextAsync("simple_cost_analysis_results.json", json);
    }
}

// Data models for cost analysis
public class OptimizedResults
{
    public double CAGR { get; set; }
    public double PositionSize { get; set; }
    public double TakeProfit { get; set; }
    public double RiskCap { get; set; }
    public double ICWeight { get; set; }
    public double IFWeight { get; set; }
    public double BWBWeight { get; set; }
    public string StrategyDescription { get; set; } = "";
    public int WeeklyTrades { get; set; }
    public int AverageContracts { get; set; }
}

public class CostAnalysis
{
    public double BidAskImpact { get; set; }
    public double CommissionImpact { get; set; }
    public double SlippageImpact { get; set; }
    public double AssignmentImpact { get; set; }
    public double LiquidityImpact { get; set; }
    public double TotalCostImpact { get; set; }
}