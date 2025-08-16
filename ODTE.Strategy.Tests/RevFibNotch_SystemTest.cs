using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ODTE.Strategy;

namespace ODTE.Strategy.Tests
{
    /// <summary>
    /// Comprehensive test suite for RevFibNotch Risk Management System
    /// Tests proportional risk adjustment based on P&L magnitude:
    /// - Losses: Immediate rightward movement (more conservative)
    /// - Profits: Leftward movement after sustained performance
    /// - Movement proportional to loss/profit magnitude
    /// </summary>
    [TestClass]
    public class RevFibNotch_SystemTest
    {
        private RevFibNotchManager _manager;
        private DateTime _testDate;

        [TestInitialize]
        public void Setup()
        {
            _manager = new RevFibNotchManager();
            _testDate = new DateTime(2025, 1, 1);
        }

        [TestMethod]
        public void Initial_Setup_Middle_Position()
        {
            // Test: Should start at middle position ($500)
            Assert.AreEqual(500m, _manager.CurrentRFibLimit);
            Assert.AreEqual(2, _manager.CurrentNotchIndex); // Index 2 = $500
            Assert.AreEqual(0, _manager.ConsecutiveProfitDays);
            
            Console.WriteLine($"Initial Setup: RFib={_manager.CurrentRFibLimit:C}, Notch={_manager.CurrentNotchIndex}");
        }

        [TestMethod] 
        public void Mild_Loss_Single_Notch_Right()
        {
            // Test: 10% loss should move 1 notch right (more conservative)
            var lossAmount = 50m; // 10% of $500
            var adjustment = _manager.ProcessDailyPnL(-lossAmount, _testDate);

            Assert.AreEqual(1, adjustment.NotchMovement); // 1 notch right
            Assert.AreEqual(300m, _manager.CurrentRFibLimit); // $500 → $300
            Assert.AreEqual(3, _manager.CurrentNotchIndex);
            Assert.IsTrue(adjustment.Reason.Contains("MILD_LOSS"));

            Console.WriteLine($"Mild Loss: {-lossAmount:C} → RFib: {adjustment.OldRFibLimit:C} → {adjustment.NewRFibLimit:C}");
        }

        [TestMethod]
        public void Significant_Loss_Single_Notch_Right()
        {
            // Test: 25% loss should move 1 notch right
            var lossAmount = 125m; // 25% of $500
            var adjustment = _manager.ProcessDailyPnL(-lossAmount, _testDate);

            Assert.AreEqual(1, adjustment.NotchMovement);
            Assert.AreEqual(300m, _manager.CurrentRFibLimit);
            Assert.IsTrue(adjustment.Reason.Contains("SIGNIFICANT_LOSS"));

            Console.WriteLine($"Significant Loss: {-lossAmount:C} → Movement: {adjustment.NotchMovement} notch");
        }

        [TestMethod]
        public void Major_Loss_Two_Notches_Right()
        {
            // Test: 50% loss should move 2 notches right
            var lossAmount = 250m; // 50% of $500
            var adjustment = _manager.ProcessDailyPnL(-lossAmount, _testDate);

            Assert.AreEqual(2, adjustment.NotchMovement); // 2 notches right
            Assert.AreEqual(200m, _manager.CurrentRFibLimit); // $500 → $200
            Assert.AreEqual(4, _manager.CurrentNotchIndex);
            Assert.IsTrue(adjustment.Reason.Contains("MAJOR_LOSS"));

            Console.WriteLine($"Major Loss: {-lossAmount:C} → RFib: {adjustment.OldRFibLimit:C} → {adjustment.NewRFibLimit:C}");
        }

        [TestMethod]
        public void Catastrophic_Loss_Three_Notches_Right()
        {
            // Test: 80% loss should move 3 notches right
            var lossAmount = 400m; // 80% of $500
            var adjustment = _manager.ProcessDailyPnL(-lossAmount, _testDate);

            Assert.AreEqual(3, adjustment.NotchMovement); // 3 notches right
            Assert.AreEqual(100m, _manager.CurrentRFibLimit); // $500 → $100 (maximum safety)
            Assert.AreEqual(5, _manager.CurrentNotchIndex);
            Assert.IsTrue(adjustment.Reason.Contains("CATASTROPHIC_LOSS"));

            Console.WriteLine($"Catastrophic Loss: {-lossAmount:C} → RFib: {adjustment.OldRFibLimit:C} → {adjustment.NewRFibLimit:C}");
        }

        [TestMethod]
        public void Single_Profit_Day_No_Movement()
        {
            // Test: Single profit day insufficient for upgrade
            var profitAmount = 50m; // 10% profit
            var adjustment = _manager.ProcessDailyPnL(profitAmount, _testDate);

            Assert.AreEqual(0, adjustment.NotchMovement); // No movement
            Assert.AreEqual(500m, _manager.CurrentRFibLimit); // Remains at $500
            Assert.IsTrue(adjustment.Reason.Contains("PROFIT_INSUFFICIENT"));

            Console.WriteLine($"Single Profit: {profitAmount:C} → No movement: {adjustment.Reason}");
        }

        [TestMethod]
        public void Two_Consecutive_Profit_Days_One_Notch_Left()
        {
            // Test: 2 consecutive profit days should move 1 notch left
            
            // Day 1: Profit (insufficient alone)
            var profit1 = 50m; // 10% of $500
            var adj1 = _manager.ProcessDailyPnL(profit1, _testDate);
            Assert.AreEqual(0, adj1.NotchMovement);

            // Day 2: Profit (triggers upgrade)
            var profit2 = 50m; 
            var adj2 = _manager.ProcessDailyPnL(profit2, _testDate.AddDays(1));
            
            Assert.AreEqual(-1, adj2.NotchMovement); // 1 notch left (more aggressive)
            Assert.AreEqual(800m, _manager.CurrentRFibLimit); // $500 → $800
            Assert.AreEqual(1, _manager.CurrentNotchIndex);
            Assert.IsTrue(adj2.Reason.Contains("SUSTAINED_PROFIT"));

            Console.WriteLine($"Sustained Profit: Day1={profit1:C}, Day2={profit2:C} → RFib: {adj2.OldRFibLimit:C} → {adj2.NewRFibLimit:C}");
        }

        [TestMethod]
        public void Major_Profit_Immediate_Upgrade()
        {
            // Test: Major profit (30%+) should trigger immediate upgrade
            var majorProfit = 150m; // 30% of $500
            var adjustment = _manager.ProcessDailyPnL(majorProfit, _testDate);

            Assert.AreEqual(-1, adjustment.NotchMovement); // 1 notch left immediately
            Assert.AreEqual(800m, _manager.CurrentRFibLimit); // $500 → $800
            Assert.IsTrue(adjustment.Reason.Contains("MAJOR_PROFIT"));

            Console.WriteLine($"Major Profit: {majorProfit:C} → Immediate upgrade to {adjustment.NewRFibLimit:C}");
        }

        [TestMethod]
        public void Loss_Interrupts_Profit_Sequence()
        {
            // Test: Loss interrupts consecutive profit sequence
            
            // Day 1: Profit
            _manager.ProcessDailyPnL(50m, _testDate);
            Assert.AreEqual(1, _manager.ConsecutiveProfitDays);

            // Day 2: Loss (interrupts sequence)
            var adjustment = _manager.ProcessDailyPnL(-25m, _testDate.AddDays(1)); // 5% loss
            Assert.AreEqual(1, adjustment.NotchMovement); // Move right due to loss
            Assert.AreEqual(300m, _manager.CurrentRFibLimit);
            Assert.AreEqual(0, _manager.ConsecutiveProfitDays); // Reset consecutive count

            // Day 3: Profit (starts new sequence)
            _manager.ProcessDailyPnL(30m, _testDate.AddDays(2)); // 10% of new $300 limit
            Assert.AreEqual(1, _manager.ConsecutiveProfitDays);

            Console.WriteLine($"Loss Interruption: Profit sequence reset, now at {_manager.CurrentRFibLimit:C}");
        }

        [TestMethod]
        public void Boundary_Conditions_Maximum_Safety()
        {
            // Test: Cannot move beyond maximum safety position
            _manager.ResetToNotch(5); // Start at $100 (maximum safety)
            
            var catastrophicLoss = 80m; // 80% of $100
            var adjustment = _manager.ProcessDailyPnL(-catastrophicLoss, _testDate);

            Assert.AreEqual(0, adjustment.NotchMovement); // Cannot move further right
            Assert.AreEqual(100m, _manager.CurrentRFibLimit); // Remains at $100
            Assert.AreEqual(5, _manager.CurrentNotchIndex);

            Console.WriteLine($"Boundary Test: Already at max safety, cannot move further");
        }

        [TestMethod]
        public void Boundary_Conditions_Maximum_Aggression()
        {
            // Test: Cannot move beyond maximum aggressive position
            _manager.ResetToNotch(0); // Start at $1250 (maximum aggression)
            
            var majorProfit = 375m; // 30% of $1250
            var adjustment = _manager.ProcessDailyPnL(majorProfit, _testDate);

            Assert.AreEqual(0, adjustment.NotchMovement); // Cannot move further left
            Assert.AreEqual(1250m, _manager.CurrentRFibLimit); // Remains at $1250
            Assert.AreEqual(0, _manager.CurrentNotchIndex);

            Console.WriteLine($"Boundary Test: Already at max aggression, cannot move further");
        }

        [TestMethod]
        public void Complete_RFib_Journey_Up_And_Down()
        {
            // Test: Complete journey through all RFib levels
            var results = new List<NotchRFibAdjustment>();
            var currentDate = _testDate;

            // Journey to maximum safety (series of losses)
            Console.WriteLine("=== JOURNEY TO MAXIMUM SAFETY ===");
            
            // Loss 1: $500 → $300
            results.Add(_manager.ProcessDailyPnL(-50m, currentDate++));
            
            // Loss 2: $300 → $200 
            results.Add(_manager.ProcessDailyPnL(-75m, currentDate++)); // 25% of $300
            
            // Loss 3: $200 → $100
            results.Add(_manager.ProcessDailyPnL(-50m, currentDate++)); // 25% of $200

            Assert.AreEqual(100m, _manager.CurrentRFibLimit);
            
            // Journey back to aggressive (series of profits)
            Console.WriteLine("\n=== JOURNEY BACK TO AGGRESSION ===");
            
            // Two consecutive profits: $100 → $200
            results.Add(_manager.ProcessDailyPnL(10m, currentDate++)); // Day 1 profit
            results.Add(_manager.ProcessDailyPnL(10m, currentDate++)); // Day 2 profit (triggers upgrade)
            Assert.AreEqual(200m, _manager.CurrentRFibLimit);
            
            // Two consecutive profits: $200 → $300
            results.Add(_manager.ProcessDailyPnL(20m, currentDate++)); // Day 1 profit
            results.Add(_manager.ProcessDailyPnL(20m, currentDate++)); // Day 2 profit (triggers upgrade)
            Assert.AreEqual(300m, _manager.CurrentRFibLimit);

            // Major profit: $300 → $500 (immediate)
            results.Add(_manager.ProcessDailyPnL(90m, currentDate++)); // 30% major profit
            Assert.AreEqual(500m, _manager.CurrentRFibLimit);

            // Print journey summary
            Console.WriteLine("\nJOURNEY SUMMARY:");
            foreach (var result in results.Where(r => r.NotchMovement != 0))
            {
                Console.WriteLine($"  {result.Date:MM-dd}: {result.DailyPnL:C} → {result.OldRFibLimit:C} → {result.NewRFibLimit:C} ({result.Reason})");
            }
        }

        [TestMethod]
        public void RFib_Limits_Array_Integrity()
        {
            // Test: Verify RFib limits array matches specification
            var limits = _manager.AllRFibLimits;
            var expected = new decimal[] { 1250m, 800m, 500m, 300m, 200m, 100m };

            CollectionAssert.AreEqual(expected, limits);
            Assert.AreEqual(6, limits.Length);

            Console.WriteLine($"RFib Limits: [{string.Join(", ", limits.Select(l => l.ToString("C0")))}]");
        }

        [TestMethod]
        public void Status_Reporting_Accuracy()
        {
            // Test: Status reporting provides accurate information
            
            // Create some history
            _manager.ProcessDailyPnL(50m, _testDate);
            _manager.ProcessDailyPnL(30m, _testDate.AddDays(1)); // Triggers upgrade
            _manager.ProcessDailyPnL(-20m, _testDate.AddDays(2)); // Small loss
            
            var status = _manager.GetStatus();
            
            Assert.AreEqual(800m, status.CurrentLimit); // After upgrade then small loss back to $500
            Assert.AreEqual(1, status.CurrentNotchIndex); // Should be at index 1 ($800)
            Assert.AreEqual("2/6", status.NotchPosition);
            Assert.AreEqual(0, status.ConsecutiveProfitDays); // Reset by loss
            Assert.AreEqual(6, status.AllLimits.Length);
            Assert.IsTrue(status.RecentHistory.Count > 0);

            Console.WriteLine($"Status: {status.CurrentLimit:C} at position {status.NotchPosition}");
            Console.WriteLine($"Consecutive Profit Days: {status.ConsecutiveProfitDays}");
            Console.WriteLine($"Recent Drawdown: {status.RecentDrawdown:C}");
        }

        [TestMethod]
        public void Proportional_Movement_Validation()
        {
            // Test: Validate proportional movement for different loss magnitudes
            var testCases = new[]
            {
                new { Loss = 25m, ExpectedMovement = 0, Description = "5% loss - no movement" },
                new { Loss = 50m, ExpectedMovement = 1, Description = "10% loss - 1 notch" },
                new { Loss = 125m, ExpectedMovement = 1, Description = "25% loss - 1 notch" },
                new { Loss = 250m, ExpectedMovement = 2, Description = "50% loss - 2 notches" },
                new { Loss = 400m, ExpectedMovement = 3, Description = "80% loss - 3 notches" }
            };

            foreach (var testCase in testCases)
            {
                // Reset to $500 for each test
                _manager.ResetToNotch(2, "TEST_RESET");
                
                var adjustment = _manager.ProcessDailyPnL(-testCase.Loss, _testDate);
                
                Assert.AreEqual(testCase.ExpectedMovement, adjustment.NotchMovement, 
                    $"Failed for {testCase.Description}");
                
                Console.WriteLine($"✓ {testCase.Description}: {-testCase.Loss:C} → {adjustment.NotchMovement} notches");
            }
        }

        [TestMethod]
        public void Configuration_Customization()
        {
            // Test: Custom configuration affects behavior
            var customConfig = new RevFibNotchConfiguration
            {
                RequiredConsecutiveProfitDays = 3, // Require 3 days instead of 2
                MildProfitThreshold = 0.15m, // 15% instead of 10%
                MajorProfitThreshold = 0.40m // 40% instead of 30%
            };

            var customManager = new RevFibNotchManager(customConfig);
            
            // Test: 2 consecutive profits should NOT trigger upgrade (needs 3)
            customManager.ProcessDailyPnL(75m, _testDate); // 15% profit
            customManager.ProcessDailyPnL(75m, _testDate.AddDays(1)); // 15% profit
            Assert.AreEqual(500m, customManager.CurrentRFibLimit); // No movement yet
            
            // Test: 3rd consecutive profit SHOULD trigger upgrade
            var adj = customManager.ProcessDailyPnL(75m, _testDate.AddDays(2)); // 15% profit
            Assert.AreEqual(-1, adj.NotchMovement); // Now triggers upgrade
            Assert.AreEqual(800m, customManager.CurrentRFibLimit);

            Console.WriteLine($"Custom Config Test: Required 3 consecutive days, upgrade at day 3");
        }
    }
}