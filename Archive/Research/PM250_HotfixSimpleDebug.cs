using System;
using Xunit;
using ODTE.Strategy.RiskManagement;
using ODTE.Strategy.Models;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// Simple debug to understand why PM250 hotfix test shows 0 trades
    /// </summary>
    public class PM250_HotfixSimpleDebug
    {
        [Fact]
        public void Debug_PM250_SingleOpportunity()
        {
            Console.WriteLine("=== PM250 HOTFIX SINGLE OPPORTUNITY DEBUG ===");
            
            // Setup exactly like PM250 test
            var rfibManager = new ReverseFibonacciRiskManager();
            var perTradeRiskManager = new PerTradeRiskManager(rfibManager);
            var integerPositionSizer = new IntegerPositionSizer(perTradeRiskManager, rfibManager)
            {
                EnableProbeTradeRule = true,
                EnableLowCapBoost = true,
                EnableScaleToFit = true
            };
            var budgetCapValidator = new BudgetCapValidator(perTradeRiskManager, rfibManager);
            var tierAGate = new TierATradeExecutionGate(perTradeRiskManager, budgetCapValidator, integerPositionSizer, rfibManager);
            
            var tradingDay = DateTime.Today;
            
            // Create a simple opportunity like PM250 test creates
            var opportunity = new PM250TradingOpportunity
            {
                Timestamp = tradingDay.AddHours(10),
                StrategyType = StrategyType.IronCondor,
                UnderlyingPrice = 385m,
                NetCredit = 0.22m,
                ProposedContracts = 2,
                Width = 3.75m,
                VIX = 24,
                LiquidityScore = 0.75,
                MarketStress = 0.55,
                GoScore = 75,
                MarketRegime = "Post-COVID Recovery"
            };
            
            Console.WriteLine($"Opportunity: {opportunity.StrategyType}, Credit: ${opportunity.NetCredit}, Width: {opportunity.Width}");
            
            // STEP 1: Test position sizing
            var strategySpec = new StrategySpecification
            {
                StrategyType = opportunity.StrategyType,
                NetCredit = opportunity.NetCredit,
                Width = opportunity.Width,
                PutWidth = opportunity.Width,
                CallWidth = opportunity.Width
            };
            
            var sizingResult = integerPositionSizer.CalculateMaxContracts(tradingDay, strategySpec);
            Console.WriteLine($"Position sizing: {sizingResult.CalculationDetails}");
            Console.WriteLine($"Max contracts: {sizingResult.MaxContracts}");
            Console.WriteLine($"Hotfixes used: Probe={sizingResult.UsedProbeTrade}, Dynamic={sizingResult.UsedDynamicFraction}, Scale={sizingResult.UsedScaleToFit}");
            
            if (sizingResult.MaxContracts > 0)
            {
                // STEP 2: Test Tier A validation
                var adjustedWidth = sizingResult.UsedScaleToFit ? integerPositionSizer.MinWidthPoints : opportunity.Width;
                var tradeCandidate = new TradeCandidate
                {
                    StrategyType = opportunity.StrategyType,
                    Contracts = sizingResult.MaxContracts,
                    NetCredit = opportunity.NetCredit,
                    Width = adjustedWidth,
                    PutWidth = adjustedWidth,
                    CallWidth = adjustedWidth,
                    LiquidityScore = opportunity.LiquidityScore,
                    BidAskSpread = 0.12m,
                    ProposedExecutionTime = opportunity.Timestamp
                };
                
                Console.WriteLine($"Trade candidate: {tradeCandidate.Contracts} contracts, width {tradeCandidate.Width}");
                
                var validation = tierAGate.ValidateTradeExecution(tradeCandidate, tradingDay);
                Console.WriteLine($"Validation result: {validation.GetExecutiveSummary()}");
                
                if (!validation.IsApproved)
                {
                    Console.WriteLine($"REJECTION REASON: {validation.PrimaryRejectReason}");
                    Console.WriteLine($"DETAILED REASON: {validation.DetailedRejectReason}");
                    
                    // Print all validation failures
                    Console.WriteLine("Validation details:");
                    foreach (var result in validation.ValidationResults)
                    {
                        Console.WriteLine($"  {result.ValidatorName}: {(result.Passed ? "PASS" : "FAIL")} - {result.Message}");
                    }
                }
                else
                {
                    Console.WriteLine("✅ TRADE WOULD BE EXECUTED!");
                }
            }
            else
            {
                Console.WriteLine("❌ POSITION SIZING REJECTED - NO CONTRACTS ALLOWED");
                Console.WriteLine($"Rejection details: {sizingResult.CalculationDetails}");
            }
            
            // This should pass if either position sizing allows contracts OR Tier A approves
            Assert.True(sizingResult.MaxContracts > 0, 
                "Debug test should show that position sizing allows contracts");
        }
    }
    
    // Using existing PM250TradingOpportunity class
}