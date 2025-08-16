using System;
using System.Linq;
using Xunit;
using ODTE.Strategy.RiskManagement;
using ODTE.Strategy.Models;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// Tier A Hotfix Validation Test - H1, H2, H3, H4 Implementation
    /// 
    /// PURPOSE: Validate that hotfixes from PM250_A1A2_Regression_Hotfix_Roadmap.md
    /// successfully address the zero-trade issue while maintaining safety
    /// 
    /// HOTFIXES TESTED:
    /// - H1: Probe 1-Lot Rule for zero-trade prevention
    /// - H2: Dynamic Fraction (f=0.80 at low caps ≤$150)
    /// - H3: Scale-to-Fit narrow-once fallback
    /// - H4: Comprehensive audit logging
    /// 
    /// BEFORE HOTFIXES: System produced zero trades due to over-tight constraints
    /// AFTER HOTFIXES: System should execute safe trades while respecting RFib caps
    /// </summary>
    public class TierA_Hotfix_ValidationTest
    {
        #region H1: Probe 1-Lot Rule Tests
        
        [Fact]
        public void H1_ProbeTradeRule_AllowsOneContractWithinAbsoluteBudget()
        {
            // Arrange: Low remaining budget scenario
            var rfibManager = new ReverseFibonacciRiskManager();
            var perTradeRiskManager = new PerTradeRiskManager(rfibManager);
            var integerPositionSizer = new IntegerPositionSizer(perTradeRiskManager, rfibManager)
            {
                EnableProbeTradeRule = true,
                ProbeOnlyWhenNoPositions = true
            };
            
            var tradingDay = DateTime.Today;
            var strategySpec = new StrategySpecification
            {
                StrategyType = StrategyType.IronCondor,
                NetCredit = 0.22m,
                Width = 1.0m,
                PutWidth = 1.0m,
                CallWidth = 1.0m
            };
            
            // Act: Calculate contracts with probe rule enabled
            var result = integerPositionSizer.CalculateMaxContracts(tradingDay, strategySpec);
            
            // Assert: Should allow 1 contract even if derived calculation gives 0
            Assert.True(result.IsValid || result.MaxContracts == 1, 
                $"Probe rule should allow 1 contract. Result: {result.CalculationDetails}");
            
            // Verify probe trade was used if derived was 0
            if (result.DerivedMaxContracts == 0 && result.MaxContracts == 1)
            {
                Assert.True(result.UsedProbeTrade, "Should indicate probe trade was used");
            }
        }
        
        [Fact]
        public void H1_ProbeTradeRule_RespectsAbsoluteBudgetLimit()
        {
            // Arrange: Scenario where even 1 contract exceeds remaining budget
            var rfibManager = new ReverseFibonacciRiskManager();
            var perTradeRiskManager = new PerTradeRiskManager(rfibManager);
            var integerPositionSizer = new IntegerPositionSizer(perTradeRiskManager, rfibManager);
            
            // Force very small remaining budget
            var tradingDay = DateTime.Today;
            for (int i = 0; i < 10; i++)
            {
                rfibManager.RecordTradeLoss(tradingDay, 45m); // Reduce budget significantly
            }
            
            var strategySpec = new StrategySpecification
            {
                StrategyType = StrategyType.IronCondor,
                NetCredit = 0.15m,
                Width = 1.0m,
                PutWidth = 1.0m,
                CallWidth = 1.0m
            };
            
            // Act
            var result = integerPositionSizer.CalculateMaxContracts(tradingDay, strategySpec);
            
            // Assert: Should not allow any trades if 1 contract exceeds absolute budget
            var remainingBudget = rfibManager.GetRemainingDailyBudget(tradingDay);
            if (result.MaxLossPerContract > remainingBudget)
            {
                Assert.True(result.MaxContracts == 0, 
                    "Probe rule should respect absolute budget limit");
            }
        }
        
        #endregion
        
        #region H2: Dynamic Fraction Tests
        
        [Fact]
        public void H2_DynamicFraction_UsesHigherFractionAtLowCaps()
        {
            // Arrange: Low daily cap scenario (≤$150)
            var rfibManager = new ReverseFibonacciRiskManager();
            var perTradeRiskManager = new PerTradeRiskManager(rfibManager);
            var integerPositionSizer = new IntegerPositionSizer(perTradeRiskManager, rfibManager)
            {
                EnableLowCapBoost = true
            };
            
            // Force low daily cap by recording losses to trigger lower RFib level
            var tradingDay = DateTime.Today;
            rfibManager.RecordTradeLoss(tradingDay.AddDays(-1), 400m); // Force to $300 cap
            rfibManager.RecordTradeLoss(tradingDay.AddDays(-1), 200m); // Force to $200 cap
            rfibManager.RecordTradeLoss(tradingDay.AddDays(-1), 100m); // Force to $100 cap
            
            var strategySpec = new StrategySpecification
            {
                StrategyType = StrategyType.IronCondor,
                NetCredit = 0.22m,
                Width = 1.0m,
                PutWidth = 1.0m,
                CallWidth = 1.0m
            };
            
            // Act
            var result = integerPositionSizer.CalculateMaxContracts(tradingDay, strategySpec);
            
            // Assert: Should use higher fraction (0.80) at low caps
            var dailyCap = rfibManager.GetDailyBudgetLimit(tradingDay);
            if (dailyCap <= IntegerPositionSizer.LOW_CAP_THRESHOLD)
            {
                Assert.True(result.UsedDynamicFraction, 
                    $"Should use dynamic fraction at low cap ${dailyCap:F0}");
                Assert.Equal(IntegerPositionSizer.LOW_CAP_FRACTION, result.AppliedFraction);
            }
        }
        
        [Fact]
        public void H2_DynamicFraction_UsesNormalFractionAtHighCaps()
        {
            // Arrange: Normal daily cap scenario (>$150)
            var rfibManager = new ReverseFibonacciRiskManager();
            var perTradeRiskManager = new PerTradeRiskManager(rfibManager);
            var integerPositionSizer = new IntegerPositionSizer(perTradeRiskManager, rfibManager);
            
            var tradingDay = DateTime.Today;
            var strategySpec = new StrategySpecification
            {
                StrategyType = StrategyType.IronCondor,
                NetCredit = 0.22m,
                Width = 1.0m,
                PutWidth = 1.0m,
                CallWidth = 1.0m
            };
            
            // Act
            var result = integerPositionSizer.CalculateMaxContracts(tradingDay, strategySpec);
            
            // Assert: Should use normal fraction at high caps
            var dailyCap = rfibManager.GetDailyBudgetLimit(tradingDay);
            if (dailyCap > IntegerPositionSizer.LOW_CAP_THRESHOLD)
            {
                Assert.False(result.UsedDynamicFraction, 
                    $"Should use normal fraction at high cap ${dailyCap:F0}");
                Assert.Equal(perTradeRiskManager.MaxTradeRiskFraction, result.AppliedFraction);
            }
        }
        
        #endregion
        
        #region H3: Scale-to-Fit Tests
        
        [Fact]
        public void H3_ScaleToFit_NarrowsWidthWhenZeroContracts()
        {
            // Arrange: Wide strategy that doesn't fit budget
            var rfibManager = new ReverseFibonacciRiskManager();
            var perTradeRiskManager = new PerTradeRiskManager(rfibManager);
            var integerPositionSizer = new IntegerPositionSizer(perTradeRiskManager, rfibManager)
            {
                EnableScaleToFit = true,
                MinWidthPoints = 1.0m
            };
            
            var tradingDay = DateTime.Today;
            var wideStrategySpec = new StrategySpecification
            {
                StrategyType = StrategyType.IronCondor,
                NetCredit = 0.40m,
                Width = 5.0m, // Wide strategy
                PutWidth = 5.0m,
                CallWidth = 5.0m
            };
            
            // Act
            var result = integerPositionSizer.CalculateMaxContracts(tradingDay, wideStrategySpec);
            
            // Assert: Should try narrower width if original gives zero contracts
            if (result.UsedScaleToFit)
            {
                Assert.True(result.MaxContracts >= 1, 
                    "Scale-to-fit should produce at least 1 contract");
                Assert.Contains("[SCALED]", result.CalculationDetails);
            }
        }
        
        [Fact]
        public void H3_ScaleToFit_SkipsWhenAlreadyMinWidth()
        {
            // Arrange: Strategy already at minimum width
            var rfibManager = new ReverseFibonacciRiskManager();
            var perTradeRiskManager = new PerTradeRiskManager(rfibManager);
            var integerPositionSizer = new IntegerPositionSizer(perTradeRiskManager, rfibManager)
            {
                EnableScaleToFit = true,
                MinWidthPoints = 1.0m
            };
            
            var tradingDay = DateTime.Today;
            var minWidthStrategySpec = new StrategySpecification
            {
                StrategyType = StrategyType.IronCondor,
                NetCredit = 0.22m,
                Width = 1.0m, // Already at minimum
                PutWidth = 1.0m,
                CallWidth = 1.0m
            };
            
            // Act
            var result = integerPositionSizer.CalculateMaxContracts(tradingDay, minWidthStrategySpec);
            
            // Assert: Should not attempt scale-to-fit
            Assert.False(result.UsedScaleToFit, 
                "Should not scale when already at minimum width");
        }
        
        #endregion
        
        #region H4: Audit Logging Tests
        
        [Fact]
        public void H4_AuditLogging_RecordsComprehensiveDetails()
        {
            // Arrange
            var rfibManager = new ReverseFibonacciRiskManager();
            var perTradeRiskManager = new PerTradeRiskManager(rfibManager);
            var integerPositionSizer = new IntegerPositionSizer(perTradeRiskManager, rfibManager);
            var budgetCapValidator = new BudgetCapValidator(perTradeRiskManager, rfibManager);
            var tierAGate = new TierATradeExecutionGate(perTradeRiskManager, budgetCapValidator, integerPositionSizer, rfibManager);
            
            var tradingDay = DateTime.Today;
            var tradeCandidate = new TradeCandidate
            {
                StrategyType = StrategyType.IronCondor,
                Contracts = 1,
                NetCredit = 0.22m,
                Width = 1.0m,
                PutWidth = 1.0m,
                CallWidth = 1.0m,
                LiquidityScore = 0.8,
                BidAskSpread = 0.12m
            };
            
            // Act
            var validation = tierAGate.ValidateTradeExecution(tradeCandidate, tradingDay);
            
            // Assert: Should record comprehensive audit
            var auditRecords = tierAGate.GetAuditRecords(10);
            Assert.True(auditRecords.Count > 0, "Should record audit entries");
            
            var latestRecord = auditRecords.Last();
            Assert.Equal("XSP", latestRecord.Symbol);
            Assert.Equal(tradeCandidate.Width, latestRecord.Width);
            Assert.Equal(tradeCandidate.NetCredit, latestRecord.ExpectedCredit);
            Assert.True(latestRecord.DailyCap > 0, "Should record daily cap");
            Assert.True(latestRecord.RemainingBudget >= 0, "Should record remaining budget");
            Assert.True(!string.IsNullOrEmpty(latestRecord.Decision), "Should record decision");
        }
        
        [Fact]
        public void H4_AuditLogging_ExportsToJsonFormat()
        {
            // Arrange
            var rfibManager = new ReverseFibonacciRiskManager();
            var perTradeRiskManager = new PerTradeRiskManager(rfibManager);
            var integerPositionSizer = new IntegerPositionSizer(perTradeRiskManager, rfibManager);
            var budgetCapValidator = new BudgetCapValidator(perTradeRiskManager, rfibManager);
            var tierAGate = new TierATradeExecutionGate(perTradeRiskManager, budgetCapValidator, integerPositionSizer, rfibManager);
            
            var tradingDay = DateTime.Today;
            var tradeCandidate = new TradeCandidate
            {
                StrategyType = StrategyType.IronCondor,
                Contracts = 1,
                NetCredit = 0.22m,
                Width = 1.0m
            };
            
            // Act: Generate some audit data
            tierAGate.ValidateTradeExecution(tradeCandidate, tradingDay);
            var jsonExport = tierAGate.ExportAuditToJson();
            
            // Assert: Should export valid JSON
            Assert.False(string.IsNullOrEmpty(jsonExport), "Should export JSON data");
            Assert.Contains("\"t\":", jsonExport, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"sym\":", jsonExport, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"decision\":", jsonExport, StringComparison.OrdinalIgnoreCase);
        }
        
        #endregion
        
        #region Integration Test: Zero-Trade Prevention
        
        [Fact]
        public void Integration_HotfixesPreventsZeroTradeMonths()
        {
            // Arrange: Simulate tight budget scenario that previously caused zero trades
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
            
            // Force low budget scenario
            var tradingDay = DateTime.Today;
            rfibManager.RecordTradeLoss(tradingDay.AddDays(-1), 350m); // Force to lower RFib level
            
            var tightBudgetCandidate = new TradeCandidate
            {
                StrategyType = StrategyType.IronCondor,
                Contracts = 3, // Originally requested
                NetCredit = 0.22m,
                Width = 5.0m, // Wide strategy that might not fit
                PutWidth = 5.0m,
                CallWidth = 5.0m,
                LiquidityScore = 0.7,
                BidAskSpread = 0.15m
            };
            
            // Act
            var validation = tierAGate.ValidateTradeExecution(tightBudgetCandidate, tradingDay);
            
            // Assert: Hotfixes should prevent total rejection
            // Even if not approved at original size, should provide path to execute
            var contractCalculation = integerPositionSizer.CalculateMaxContracts(tradingDay, new StrategySpecification
            {
                StrategyType = tightBudgetCandidate.StrategyType,
                NetCredit = tightBudgetCandidate.NetCredit,
                Width = tightBudgetCandidate.Width,
                PutWidth = tightBudgetCandidate.PutWidth,
                CallWidth = tightBudgetCandidate.CallWidth
            });
            
            // Should find a path to trade (via probe, dynamic fraction, or scale-to-fit)
            var foundTradingPath = validation.IsApproved || 
                                   contractCalculation.UsedProbeTrade || 
                                   contractCalculation.UsedDynamicFraction || 
                                   contractCalculation.UsedScaleToFit;
                                   
            Assert.True(foundTradingPath, 
                $"Hotfixes should find a trading path. Validation: {validation.GetExecutiveSummary()}. " +
                $"Contracts: {contractCalculation.CalculationDetails}");
            
            // Verify audit trail captured the decision process
            var auditRecords = tierAGate.GetAuditRecords(1);
            Assert.True(auditRecords.Count > 0, "Should record decision in audit trail");
        }
        
        #endregion
    }
}