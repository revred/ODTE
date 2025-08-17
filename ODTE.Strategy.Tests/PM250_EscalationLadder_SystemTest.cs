using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using ODTE.Strategy;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// Comprehensive test suite for the PM250 Escalation Ladder System
    /// Validates progressive scaling logic, auto de-escalation, and correlation budget management
    /// Based on ScaleHighWithManagedRisk dual probe & punch specification
    /// </summary>
    public class PM250_EscalationLadder_SystemTest
    {
        private PM250_DualStrategyScalingEngine _engine;
        private MockRiskManager _riskManager;
        private MockProbeDetector _probeDetector;
        private MockCorrelationManager _correlationManager;
        private MockQualityFilter _qualityFilter;
        private MockExitManager _exitManager;

        // Constructor replaces TestInitialize in xUnit
        public void Setup()
        {
            _riskManager = new MockRiskManager();
            _probeDetector = new MockProbeDetector();
            _correlationManager = new MockCorrelationManager();
            _qualityFilter = new MockQualityFilter();
            _exitManager = new MockExitManager();

            _engine = new PM250_DualStrategyScalingEngine(
                _riskManager,
                _probeDetector,
                _correlationManager,
                _qualityFilter,
                _exitManager
            );
        }

        [Fact]
        public void Level0_Baseline_ProbeOnly_Operation()
        {
            // Test: Level 0 should only allow Probe trades with standard allocation
            _engine.ConfigureForPhase(ScalingPhase.Foundation);
            
            // Setup: No positive probe conditions met
            _probeDetector.SetPositiveProbeResult(false);
            _riskManager.SetDailyLimit(1000m);
            _riskManager.SetRemainingBudget(1000m);

            var probeSetup = CreateProbeTradeSetup(width: 1.0m, credit: 0.25m);
            var decision = _engine.ProcessTradeOpportunity(probeSetup);

            // Validate: Should execute as Probe with standard fraction
            Assert.Equal(TradeAction.Execute, decision.Action);
            Assert.Equal(TradeLane.Probe, decision.Lane);
            Assert.Equal(EscalationLevel.Level0, decision.EscalationLevel);
            
            // Position size should be based on 40% probe fraction
            var expectedSize = Math.Floor((0.40m * 1000m) / ((1.0m - 0.25m) * 100m));
            Assert.Equal(expectedSize, decision.PositionSize);

            Console.WriteLine($"Level 0 Probe: Size={decision.PositionSize}, Reason={decision.ReasonCode}");
        }

        [Fact]
        public void Level1_Greenlight1_QualityLane_Activation()
        {
            // Test: Level 1 escalation enables Quality lane with higher allocation
            _engine.ConfigureForPhase(ScalingPhase.Escalation);
            
            // Setup: Positive probe conditions met + sufficient P&L cushion
            _probeDetector.SetPositiveProbeResult(true);
            _probeDetector.SetSessionStats(new SessionStats
            {
                DailyCap = 1500m,
                RealizedDayPnL = 450m, // 30% of daily cap (450/1500 = 0.30)
                ProbeCount = 3,
                ProbeWinRate = 0.70m,
                LiquidityScore = 0.80m,
                InEventBlackout = false
            });
            _riskManager.SetDailyLimit(1500m);
            _riskManager.SetRemainingBudget(1050m); // 1500 - 450 realized

            var qualitySetup = CreateQualityTradeSetup(width: 2.0m, credit: 0.50m);
            _qualityFilter.SetHighQualityResult(true);
            
            var decision = _engine.ProcessTradeOpportunity(qualitySetup);

            // Validate: Should execute as Quality with Level 1 escalation
            Assert.Equal(TradeAction.Execute, decision.Action);
            Assert.Equal(TradeLane.Quality, decision.Lane);
            Assert.Equal(EscalationLevel.Level1, decision.EscalationLevel);
            
            // Position size should use 55% fraction and be constrained by 50% of realized P&L
            var sizingBudget = Math.Min(0.55m * 1050m, 0.50m * 450m); // Min(577.5, 225) = 225
            var expectedSize = Math.Floor(sizingBudget / ((2.0m - 0.50m) * 100m)); // 225 / 150 = 1
            Assert.Equal(expectedSize, decision.PositionSize);

            Console.WriteLine($"Level 1 Quality: Size={decision.PositionSize}, Budget={sizingBudget:C}, Reason={decision.ReasonCode}");
        }

        [Fact]
        public void Level2_Greenlight2_MaxAllocation()
        {
            // Test: Level 2 escalation with maximum capital allocation
            _engine.ConfigureForPhase(ScalingPhase.Quality);
            
            // Setup: High P&L cushion + successful Quality trades
            _probeDetector.SetPositiveProbeResult(true);
            _probeDetector.SetSessionStats(new SessionStats
            {
                DailyCap = 2000m,
                RealizedDayPnL = 1200m, // 60% of daily cap
                ProbeCount = 5,
                ProbeWinRate = 0.75m,
                LiquidityScore = 0.85m,
                Last3PunchPnL = 150m, // Positive Quality trade results
                InEventBlackout = false
            });
            _riskManager.SetDailyLimit(2000m);
            _riskManager.SetRemainingBudget(800m);

            var qualitySetup = CreateQualityTradeSetup(width: 2.5m, credit: 0.60m);
            _qualityFilter.SetHighQualityResult(true);
            
            var decision = _engine.ProcessTradeOpportunity(qualitySetup);

            // Validate: Should execute with Level 2 escalation
            Assert.Equal(TradeAction.Execute, decision.Action);
            Assert.Equal(TradeLane.Quality, decision.Lane);
            Assert.Equal(EscalationLevel.Level2, decision.EscalationLevel);
            
            // Position size should use 65% fraction
            var sizingBudget = Math.Min(0.65m * 800m, 0.50m * 1200m); // Min(520, 600) = 520
            var expectedSize = Math.Floor(sizingBudget / ((2.5m - 0.60m) * 100m)); // 520 / 190 = 2
            Assert.Equal(expectedSize, decision.PositionSize);

            Console.WriteLine($"Level 2 Quality: Size={decision.PositionSize}, Budget={sizingBudget:C}, Reason={decision.ReasonCode}");
        }

        [Fact]
        public void Auto_Deescalation_PnL_Cushion_Loss()
        {
            // Test: Auto de-escalation when P&L cushion is lost
            _engine.ConfigureForPhase(ScalingPhase.Escalation);
            
            // Setup: Start at Level 1, then P&L drops below half of trigger
            _probeDetector.SetPositiveProbeResult(true);
            var sessionStats = new SessionStats
            {
                DailyCap = 1500m,
                RealizedDayPnL = 450m, // Initially at 30% (Level 1 trigger)
                ProbeCount = 3,
                ProbeWinRate = 0.70m,
                LiquidityScore = 0.80m
            };
            _probeDetector.SetSessionStats(sessionStats);
            _riskManager.SetDailyLimit(1500m);
            _riskManager.SetRemainingBudget(1050m);

            // First trade should be Level 1
            var qualitySetup = CreateQualityTradeSetup(width: 2.0m, credit: 0.50m);
            _qualityFilter.SetHighQualityResult(true);
            var decision1 = _engine.ProcessTradeOpportunity(qualitySetup);
            Assert.Equal(EscalationLevel.Level1, decision1.EscalationLevel);

            // Simulate P&L dropping below half of Level 1 trigger (225 < 15% of 1500)
            sessionStats.RealizedDayPnL = 200m; // Below 50% of 450 trigger
            _probeDetector.SetSessionStats(sessionStats);
            _riskManager.SetRemainingBudget(1300m);

            var decision2 = _engine.ProcessTradeOpportunity(qualitySetup);
            
            // Should automatically de-escalate to Level 0
            Assert.Equal(EscalationLevel.Level0, decision2.EscalationLevel);
            Assert.Equal(TradeLane.Probe, decision2.Lane); // Falls back to Probe

            Console.WriteLine($"Auto De-escalation: L1→L0, P&L: 450→200, Decision: {decision2.ReasonCode}");
        }

        [Fact]
        public void Auto_Deescalation_Consecutive_Losses()
        {
            // Test: Auto de-escalation and cooldown after consecutive Quality losses
            _engine.ConfigureForPhase(ScalingPhase.Escalation);
            
            // Setup: Level 1 conditions met but consecutive Quality losses
            _probeDetector.SetPositiveProbeResult(true);
            _probeDetector.SetSessionStats(new SessionStats
            {
                DailyCap = 1500m,
                RealizedDayPnL = 450m,
                ProbeCount = 3,
                ProbeWinRate = 0.70m,
                LiquidityScore = 0.80m,
                ConsecutivePunchLosses = 2, // Triggers auto de-escalation
                InEventBlackout = false
            });
            _riskManager.SetDailyLimit(1500m);
            _riskManager.SetRemainingBudget(1050m);

            var qualitySetup = CreateQualityTradeSetup(width: 2.0m, credit: 0.50m);
            _qualityFilter.SetHighQualityResult(true);
            
            var decision = _engine.ProcessTradeOpportunity(qualitySetup);

            // Should force Level 0 despite positive probe conditions
            Assert.Equal(EscalationLevel.Level0, decision.EscalationLevel);
            Assert.Equal(TradeLane.Probe, decision.Lane);

            Console.WriteLine($"Consecutive Loss De-escalation: Forced L0, Reason={decision.ReasonCode}");
        }

        [Fact]
        public void Correlation_Budget_Enforcement()
        {
            // Test: Correlation budget prevents excessive concurrent exposure
            _engine.ConfigureForPhase(ScalingPhase.Quality);
            
            // Setup: Conditions for Quality trades but high correlation exposure
            _probeDetector.SetPositiveProbeResult(true);
            _probeDetector.SetSessionStats(new SessionStats
            {
                DailyCap = 2000m,
                RealizedDayPnL = 600m,
                ProbeCount = 4,
                ProbeWinRate = 0.80m,
                LiquidityScore = 0.85m
            });
            _riskManager.SetDailyLimit(2000m);
            _riskManager.SetRemainingBudget(1400m);
            
            // Mock correlation manager to indicate budget would be exceeded
            _correlationManager.SetRhoWeightedExposureAfter(1.1m); // Exceeds 1.0 limit
            
            var qualitySetup = CreateQualityTradeSetup(width: 2.0m, credit: 0.50m);
            _qualityFilter.SetHighQualityResult(true);
            
            var decision = _engine.ProcessTradeOpportunity(qualitySetup);

            // Should reject due to correlation budget violation
            Assert.Equal(TradeAction.Reject, decision.Action);
            Assert.True(decision.ReasonCode.Contains("RHO_BUDGET_EXCEEDED") || 
                         decision.ReasonCode.Contains("correlation"));

            Console.WriteLine($"Correlation Budget Block: {decision.ReasonCode}");
        }

        [Fact]
        public void Probe_OneContract_Rule()
        {
            // Test: Probe trades get 1 contract if they fit within absolute budget
            _engine.ConfigureForPhase(ScalingPhase.Foundation);
            
            _probeDetector.SetPositiveProbeResult(false); // No escalation
            _riskManager.SetDailyLimit(500m);
            _riskManager.SetRemainingBudget(100m); // Low remaining budget
            
            // Setup a probe trade that would normally size to 0 but fits in absolute budget
            var probeSetup = CreateProbeTradeSetup(width: 1.0m, credit: 0.20m);
            // Max loss per contract = (1.0 - 0.20) * 100 = 80
            // Normal sizing: floor(40% * 100 / 80) = floor(40/80) = 0
            // But 80 < 100 remaining budget, so probe 1-lot rule applies
            
            var decision = _engine.ProcessTradeOpportunity(probeSetup);

            Assert.Equal(TradeAction.Execute, decision.Action);
            Assert.Equal(TradeLane.Probe, decision.Lane);
            Assert.Equal(1m, decision.PositionSize); // Probe 1-lot rule

            Console.WriteLine($"Probe 1-Lot Rule: Size={decision.PositionSize}, Budget={100:C}, MaxLoss={80:C}");
        }

        [Fact]
        public void Event_Blackout_Rejection()
        {
            // Test: All trades rejected during event blackout periods
            _engine.ConfigureForPhase(ScalingPhase.Escalation);
            
            _probeDetector.SetPositiveProbeResult(true);
            _probeDetector.SetSessionStats(new SessionStats
            {
                DailyCap = 1500m,
                RealizedDayPnL = 450m,
                ProbeCount = 3,
                ProbeWinRate = 0.70m,
                LiquidityScore = 0.80m,
                InEventBlackout = true // Event blackout active
            });
            _riskManager.SetDailyLimit(1500m);
            _riskManager.SetRemainingBudget(1050m);

            var qualitySetup = CreateQualityTradeSetup(width: 2.0m, credit: 0.50m);
            _qualityFilter.SetHighQualityResult(true);
            
            var decision = _engine.ProcessTradeOpportunity(qualitySetup);

            Assert.Equal(TradeAction.Reject, decision.Action);
            Assert.True(decision.ReasonCode.Contains("EVENT_BLACKOUT"));

            Console.WriteLine($"Event Blackout Rejection: {decision.ReasonCode}");
        }

        [Fact]
        public void Quality_Filter_Enforcement()
        {
            // Test: Quality lane requires high-quality setups
            _engine.ConfigureForPhase(ScalingPhase.Quality);
            
            _probeDetector.SetPositiveProbeResult(true);
            _probeDetector.SetSessionStats(new SessionStats
            {
                DailyCap = 2000m,
                RealizedDayPnL = 600m,
                ProbeCount = 4,
                ProbeWinRate = 0.80m,
                LiquidityScore = 0.85m
            });
            _riskManager.SetDailyLimit(2000m);
            _riskManager.SetRemainingBudget(1400m);

            var lowQualitySetup = CreateQualityTradeSetup(width: 2.0m, credit: 0.30m); // Low credit
            _qualityFilter.SetHighQualityResult(false); // Fails quality filter
            
            var decision = _engine.ProcessTradeOpportunity(lowQualitySetup);

            Assert.Equal(TradeAction.Reject, decision.Action);
            Assert.True(decision.ReasonCode.Contains("QUALITY_FAIL") || 
                         decision.ReasonCode.Contains("quality"));

            Console.WriteLine($"Quality Filter Rejection: {decision.ReasonCode}");
        }

        [Fact]
        public void Complete_Escalation_Sequence_Validation()
        {
            // Test: Complete sequence from Level 0 → Level 1 → Level 2 → De-escalation
            _engine.ConfigureForPhase(ScalingPhase.Quality);
            var decisions = new List<ScalingTradeDecision>();

            // Phase 1: Level 0 (No positive probe)
            _probeDetector.SetPositiveProbeResult(false);
            _riskManager.SetDailyLimit(2000m);
            _riskManager.SetRemainingBudget(2000m);
            
            var probe1 = CreateProbeTradeSetup(width: 1.0m, credit: 0.25m);
            decisions.Add(_engine.ProcessTradeOpportunity(probe1));
            Assert.Equal(EscalationLevel.Level0, decisions.Last().EscalationLevel);

            // Phase 2: Level 1 (Positive probe + 30% P&L cushion)
            _probeDetector.SetPositiveProbeResult(true);
            _probeDetector.SetSessionStats(new SessionStats
            {
                DailyCap = 2000m,
                RealizedDayPnL = 600m, // 30% cushion
                ProbeCount = 3,
                ProbeWinRate = 0.75m,
                LiquidityScore = 0.80m
            });
            _riskManager.SetRemainingBudget(1400m);
            _qualityFilter.SetHighQualityResult(true);
            
            var quality1 = CreateQualityTradeSetup(width: 2.0m, credit: 0.50m);
            decisions.Add(_engine.ProcessTradeOpportunity(quality1));
            Assert.Equal(EscalationLevel.Level1, decisions.Last().EscalationLevel);

            // Phase 3: Level 2 (60% P&L cushion + successful Quality trades)
            _probeDetector.SetSessionStats(new SessionStats
            {
                DailyCap = 2000m,
                RealizedDayPnL = 1200m, // 60% cushion
                ProbeCount = 5,
                ProbeWinRate = 0.80m,
                LiquidityScore = 0.85m,
                Last3PunchPnL = 200m // Positive Quality results
            });
            _riskManager.SetRemainingBudget(800m);
            
            var quality2 = CreateQualityTradeSetup(width: 2.5m, credit: 0.65m);
            decisions.Add(_engine.ProcessTradeOpportunity(quality2));
            Assert.Equal(EscalationLevel.Level2, decisions.Last().EscalationLevel);

            // Phase 4: Auto de-escalation (P&L drops)
            _probeDetector.SetSessionStats(new SessionStats
            {
                DailyCap = 2000m,
                RealizedDayPnL = 400m, // Below 50% of Level 2 trigger (600)
                ProbeCount = 5,
                ProbeWinRate = 0.80m,
                LiquidityScore = 0.85m
            });
            _riskManager.SetRemainingBudget(1600m);
            
            var quality3 = CreateQualityTradeSetup(width: 2.0m, credit: 0.50m);
            decisions.Add(_engine.ProcessTradeOpportunity(quality3));
            // Should de-escalate due to P&L loss
            Assert.True(decisions.Last().EscalationLevel < EscalationLevel.Level2);

            Console.WriteLine("Complete Escalation Sequence:");
            for (int i = 0; i < decisions.Count; i++)
            {
                var d = decisions[i];
                Console.WriteLine($"  {i + 1}. Level {(int)d.EscalationLevel}, Lane: {d.Lane}, Size: {d.PositionSize}, Reason: {d.ReasonCode}");
            }
        }

        // Helper Methods

        private TradeSetup CreateProbeTradeSetup(decimal width, decimal credit)
        {
            return new TradeSetup
            {
                Symbol = "SPY",
                Width = width,
                ExpectedCredit = credit,
                EntryTime = DateTime.Now,
                LiquidityScore = 0.75m,
                BidAskSpread = 0.05m,
                IVRank = 0.40m,
                GoScore = 60m
            };
        }

        private TradeSetup CreateQualityTradeSetup(decimal width, decimal credit)
        {
            return new TradeSetup
            {
                Symbol = "SPY",
                Width = width,
                ExpectedCredit = credit,
                EntryTime = DateTime.Now,
                LiquidityScore = 0.85m,
                BidAskSpread = 0.03m,
                IVRank = 0.60m,
                GoScore = 75m
            };
        }
    }

    // Mock Implementations for Testing

    public class MockRiskManager : IReverseFibonacciRiskManager
    {
        private decimal _dailyLimit = 1000m;
        private decimal _remainingBudget = 1000m;

        public void SetDailyLimit(decimal limit) => _dailyLimit = limit;
        public void SetRemainingBudget(decimal budget) => _remainingBudget = budget;

        public decimal GetCurrentDailyLimit() => _dailyLimit;
        public bool WouldViolateWeeklyLimit(decimal additionalRisk) => false;
    }

    public class MockProbeDetector : IPositiveProbeDetector
    {
        private bool _isGreenlit = false;
        private SessionStats _sessionStats = new SessionStats();

        public void SetPositiveProbeResult(bool result) => _isGreenlit = result;
        public void SetSessionStats(SessionStats stats) => _sessionStats = stats;

        public bool IsGreenlit(SessionStats session) => _isGreenlit;
    }

    public class MockCorrelationManager : ICorrelationBudgetManager
    {
        private decimal _rhoExposureAfter = 0.5m;

        public void SetRhoWeightedExposureAfter(decimal exposure) => _rhoExposureAfter = exposure;

        public decimal CalculateCurrentRhoWeightedExposure(List<Position> positions) => 0.3m;
        public decimal CalculateRhoWeightedExposureAfter(List<Position> positions, TradeSetup setup, decimal positionSize) => _rhoExposureAfter;
    }

    public class MockQualityFilter : IQualityEntryFilter
    {
        private bool _isHighQuality = true;

        public void SetHighQualityResult(bool result) => _isHighQuality = result;

        public bool IsHighQuality(TradeSetup setup) => _isHighQuality;
    }

    public class MockExitManager : IDualLaneExitManager
    {
        public void ManageProbeExit(Position position) { }
        public void ManagePunchExit(Position position) { }
    }
}