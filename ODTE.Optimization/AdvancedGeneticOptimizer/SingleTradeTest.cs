using System;

namespace AdvancedGeneticOptimizer
{
    public class SingleTradeTest
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("🧪 SINGLE TRADE TEST - VERIFICATION");
            Console.WriteLine("Testing one profitable Iron Condor trade with fixed calculations");
            Console.WriteLine();
            
            // Test parameters
            var spxPrice = 4500m;
            var vixLevel = 18m;
            var contractCount = 5m; // 5 contracts
            var spreadWidth = 50m;
            var shortDelta = 0.15m;
            
            Console.WriteLine($"SPX Price: ${spxPrice}");
            Console.WriteLine($"VIX Level: {vixLevel}");
            Console.WriteLine($"Contract Count: {contractCount}");
            Console.WriteLine($"Spread Width: ${spreadWidth}");
            Console.WriteLine();
            
            // FIXED: Calculate credit per contract
            var baseCreditPct = 0.015m; // 1.5% for Iron Condor
            var notionalPerContract = spxPrice * 100; // $450,000 per contract
            var baseCredit = notionalPerContract * baseCreditPct; // $6,750 per contract
            
            var vixMultiplier = 1.0m + ((vixLevel - 18m) / 100m); // VIX adjustment
            var deltaMultiplier = 1.0m + (shortDelta * 2m); // Delta adjustment
            
            var creditPerContract = baseCredit * vixMultiplier * deltaMultiplier;
            var totalCredit = creditPerContract * contractCount;
            
            Console.WriteLine($"Base Credit %: {baseCreditPct:P2}");
            Console.WriteLine($"Notional per Contract: ${notionalPerContract:F0}");
            Console.WriteLine($"Base Credit per Contract: ${baseCredit:F0}");
            Console.WriteLine($"VIX Multiplier: {vixMultiplier:F2}");
            Console.WriteLine($"Delta Multiplier: {deltaMultiplier:F2}");
            Console.WriteLine($"Credit per Contract: ${creditPerContract:F0}");
            Console.WriteLine($"Total Credit (5 contracts): ${totalCredit:F0}");
            Console.WriteLine();
            
            // Test winning trade
            var profitTargetPct = 0.30m; // 30% profit target
            var winningPnL = totalCredit * profitTargetPct;
            
            // Commission and slippage
            var commission = 4m * contractCount; // $4 per contract (2020+ pricing)
            var slippage = 3m * contractCount; // $3 per contract slippage
            
            var netWinningPnL = winningPnL - commission - slippage;
            
            Console.WriteLine("=== WINNING TRADE ===");
            Console.WriteLine($"Gross P&L (30% of credit): ${winningPnL:F0}");
            Console.WriteLine($"Commission ({contractCount} contracts × $4): ${commission:F0}");
            Console.WriteLine($"Slippage ({contractCount} contracts × $3): ${slippage:F0}");
            Console.WriteLine($"Net P&L: ${netWinningPnL:F0}");
            Console.WriteLine($"Return on Credit: {netWinningPnL / totalCredit:P2}");
            Console.WriteLine();
            
            // Test losing trade
            var stopLossPct = 2.0m; // 2x credit stop loss
            var maxLossPerContract = spreadWidth - creditPerContract;
            var totalMaxLoss = maxLossPerContract * contractCount;
            var stopLoss = totalCredit * stopLossPct;
            var actualLoss = Math.Min(stopLoss, totalMaxLoss);
            
            var netLosingPnL = -actualLoss - commission - slippage;
            
            Console.WriteLine("=== LOSING TRADE ===");
            Console.WriteLine($"Max Loss per Contract: ${maxLossPerContract:F0}");
            Console.WriteLine($"Total Max Loss: ${totalMaxLoss:F0}");
            Console.WriteLine($"Stop Loss (2x credit): ${stopLoss:F0}");
            Console.WriteLine($"Actual Loss: ${actualLoss:F0}");
            Console.WriteLine($"Net P&L (including costs): ${netLosingPnL:F0}");
            Console.WriteLine();
            
            // Calculate win rate needed for profitability
            var winAmount = netWinningPnL;
            var lossAmount = Math.Abs(netLosingPnL);
            var breakEvenWinRate = lossAmount / (winAmount + lossAmount);
            
            Console.WriteLine("=== PROFITABILITY ANALYSIS ===");
            Console.WriteLine($"Win Amount: ${winAmount:F0}");
            Console.WriteLine($"Loss Amount: ${lossAmount:F0}");
            Console.WriteLine($"Break-even Win Rate: {breakEvenWinRate:P1}");
            Console.WriteLine();
            
            // Test 70% win rate scenario
            var testWinRate = 0.70m;
            var expectedPnL = (testWinRate * winAmount) + ((1 - testWinRate) * -lossAmount);
            
            Console.WriteLine($"Expected P&L at {testWinRate:P0} win rate: ${expectedPnL:F0}");
            Console.WriteLine($"This should be POSITIVE for profitable strategy!");
            
            if (expectedPnL > 0)
            {
                Console.WriteLine("✅ TRADE EXECUTION FIXED - PROFITABLE!");
            }
            else
            {
                Console.WriteLine("❌ Still have issues - needs more fixes");
            }
        }
    }
}