using System;
using System.Collections.Generic;
using System.Linq;

namespace AdvancedGeneticOptimizer
{
    public class TradeExecutionDebugger
    {
        private readonly Random _random = new Random(42);
        
        public class SimpleIronCondor
        {
            public decimal ShortCallStrike { get; set; }
            public decimal LongCallStrike { get; set; }
            public decimal ShortPutStrike { get; set; }
            public decimal LongPutStrike { get; set; }
            public decimal CreditReceived { get; set; }
            public decimal MaxLoss { get; set; }
            public decimal Commission { get; set; }
            public decimal Slippage { get; set; }
        }
        
        public class TradeResult
        {
            public DateTime Date { get; set; }
            public decimal SpxPrice { get; set; }
            public decimal SpxExpiry { get; set; }
            public decimal CreditReceived { get; set; }
            public decimal GrossPnL { get; set; }
            public decimal Commission { get; set; }
            public decimal Slippage { get; set; }
            public decimal NetPnL { get; set; }
            public bool IsWinner { get; set; }
            public string Outcome { get; set; } = "";
        }
        
        public void DebugIronCondorExecution()
        {
            Console.WriteLine("🔍 IRON CONDOR EXECUTION DEBUGGER");
            Console.WriteLine("🎯 Testing Known Profitable Configuration");
            Console.WriteLine(new string('=', 50));
            
            // Test a known profitable Iron Condor setup
            var spxPrice = 4500m;
            var ironCondor = CreateProfitableIronCondor(spxPrice);
            
            Console.WriteLine("📊 Iron Condor Setup:");
            Console.WriteLine($"SPX Price: ${spxPrice}");
            Console.WriteLine($"Short Call: ${ironCondor.ShortCallStrike} (OTM)");
            Console.WriteLine($"Long Call:  ${ironCondor.LongCallStrike}");
            Console.WriteLine($"Short Put:  ${ironCondor.ShortPutStrike} (OTM)");
            Console.WriteLine($"Long Put:   ${ironCondor.LongPutStrike}");
            Console.WriteLine($"Credit Received: ${ironCondor.CreditReceived}");
            Console.WriteLine($"Max Loss: ${ironCondor.MaxLoss}");
            Console.WriteLine();
            
            // Test various expiry scenarios
            var testScenarios = new[]
            {
                4500m, // At the money (max profit)
                4450m, // Slightly down (profit)
                4550m, // Slightly up (profit) 
                4400m, // Near short put (breakeven)
                4600m, // Near short call (breakeven)
                4350m, // Below short put (loss)
                4650m  // Above short call (loss)
            };
            
            Console.WriteLine("🧪 Testing Expiry Scenarios:");
            Console.WriteLine("Expiry Price | Gross P&L | Commission | Slippage | Net P&L | Win/Loss");
            Console.WriteLine(new string('-', 75));
            
            var totalNetPnL = 0m;
            var winCount = 0;
            
            foreach (var expiryPrice in testScenarios)
            {
                var result = ExecuteIronCondorTrade(ironCondor, spxPrice, expiryPrice);
                
                Console.WriteLine($"{result.SpxExpiry,11:F0} | {result.GrossPnL,8:F0} | {result.Commission,9:F2} | {result.Slippage,7:F2} | {result.NetPnL,6:F0} | {result.Outcome}");
                
                totalNetPnL += result.NetPnL;
                if (result.IsWinner) winCount++;
            }
            
            Console.WriteLine(new string('-', 75));
            Console.WriteLine($"Total Net P&L: ${totalNetPnL:F0}");
            Console.WriteLine($"Win Rate: {winCount}/{testScenarios.Length} ({winCount * 100.0 / testScenarios.Length:F1}%)");
            Console.WriteLine($"Average P&L: ${totalNetPnL / testScenarios.Length:F0}");
            Console.WriteLine();
            
            // Deep dive analysis
            Console.WriteLine("🔬 DEEP DIVE ANALYSIS:");
            AnalyzeIronCondorMath(ironCondor, testScenarios);
            
            // Test the genetic algorithm's version
            Console.WriteLine();
            Console.WriteLine("🧬 GENETIC ALGORITHM COMPARISON:");
            TestGeneticAlgorithmExecution();
        }
        
        private SimpleIronCondor CreateProfitableIronCondor(decimal spxPrice)
        {
            // Create a textbook profitable Iron Condor
            var shortCallStrike = spxPrice + 50;  // 50 points OTM call
            var longCallStrike = spxPrice + 100;  // 50-point spread
            var shortPutStrike = spxPrice - 50;   // 50 points OTM put
            var longPutStrike = spxPrice - 100;   // 50-point spread
            
            // Realistic credit for 50-point spreads
            var creditReceived = 15m; // $1500 for 1 contract
            var maxLoss = (50 * 100) - creditReceived; // Spread width - credit
            
            return new SimpleIronCondor
            {
                ShortCallStrike = shortCallStrike,
                LongCallStrike = longCallStrike,
                ShortPutStrike = shortPutStrike,
                LongPutStrike = longPutStrike,
                CreditReceived = creditReceived,
                MaxLoss = maxLoss,
                Commission = 8m, // $2 per leg
                Slippage = 5m    // Realistic slippage
            };
        }
        
        private TradeResult ExecuteIronCondorTrade(SimpleIronCondor ic, decimal entryPrice, decimal expiryPrice)
        {
            var result = new TradeResult
            {
                Date = DateTime.Today,
                SpxPrice = entryPrice,
                SpxExpiry = expiryPrice,
                CreditReceived = ic.CreditReceived,
                Commission = ic.Commission,
                Slippage = ic.Slippage
            };
            
            // Calculate Iron Condor P&L at expiry
            decimal callSpreadPnL = 0;
            decimal putSpreadPnL = 0;
            
            // Call spread P&L (we sold the lower strike)
            if (expiryPrice > ic.ShortCallStrike)
            {
                // Call spread is in the money - we lose money
                if (expiryPrice >= ic.LongCallStrike)
                {
                    // Max loss on call spread
                    callSpreadPnL = -(ic.LongCallStrike - ic.ShortCallStrike) * 100;
                }
                else
                {
                    // Partial loss on call spread
                    callSpreadPnL = -(expiryPrice - ic.ShortCallStrike) * 100;
                }
            }
            // If expiry <= short call strike, call spread expires worthless (good for us)
            
            // Put spread P&L (we sold the higher strike)
            if (expiryPrice < ic.ShortPutStrike)
            {
                // Put spread is in the money - we lose money
                if (expiryPrice <= ic.LongPutStrike)
                {
                    // Max loss on put spread
                    putSpreadPnL = -(ic.ShortPutStrike - ic.LongPutStrike) * 100;
                }
                else
                {
                    // Partial loss on put spread
                    putSpreadPnL = -(ic.ShortPutStrike - expiryPrice) * 100;
                }
            }
            // If expiry >= short put strike, put spread expires worthless (good for us)
            
            // Total gross P&L = credit received + spread P&L
            result.GrossPnL = ic.CreditReceived * 100 + callSpreadPnL + putSpreadPnL;
            result.NetPnL = result.GrossPnL - result.Commission - result.Slippage;
            result.IsWinner = result.NetPnL > 0;
            
            // Determine outcome
            if (expiryPrice >= ic.ShortPutStrike && expiryPrice <= ic.ShortCallStrike)
            {
                result.Outcome = "MAX PROFIT";
            }
            else if (result.NetPnL > 0)
            {
                result.Outcome = "PROFIT";
            }
            else if (result.NetPnL == 0)
            {
                result.Outcome = "BREAKEVEN";
            }
            else
            {
                result.Outcome = "LOSS";
            }
            
            return result;
        }
        
        private void AnalyzeIronCondorMath(SimpleIronCondor ic, decimal[] scenarios)
        {
            Console.WriteLine("Mathematical Validation:");
            Console.WriteLine($"Credit per contract: ${ic.CreditReceived} = ${ic.CreditReceived * 100} for position");
            Console.WriteLine($"Max profit zone: ${ic.ShortPutStrike} to ${ic.ShortCallStrike}");
            Console.WriteLine($"Profit probability: ~68% (1 std dev move)");
            Console.WriteLine();
            
            var profitScenarios = scenarios.Where(s => s >= ic.ShortPutStrike && s <= ic.ShortCallStrike).Count();
            Console.WriteLine($"Test scenarios in profit zone: {profitScenarios}/{scenarios.Length}");
            
            // Calculate theoretical breakevens
            var upperBreakeven = ic.ShortCallStrike + ic.CreditReceived;
            var lowerBreakeven = ic.ShortPutStrike - ic.CreditReceived;
            Console.WriteLine($"Upper breakeven: ${upperBreakeven}");
            Console.WriteLine($"Lower breakeven: ${lowerBreakeven}");
        }
        
        private void TestGeneticAlgorithmExecution()
        {
            // Simulate what the genetic algorithm is doing
            var strategy = new TestStrategy
            {
                Type = "IronCondor",
                SpreadWidth = 50m,
                ShortDelta = 0.15m,
                WinRateTarget = 0.80m,
                ProfitTargetPct = 0.30m,
                StopLossPct = 2.0m
            };
            
            var spxPrice = 4500m;
            var vix = 18m;
            var positionSize = 1000m; // $1000 position
            
            Console.WriteLine("Genetic Algorithm Test:");
            Console.WriteLine($"Position Size: ${positionSize}");
            Console.WriteLine($"Spread Width: ${strategy.SpreadWidth}");
            
            // This is likely where the bug is - let's trace the execution
            var creditReceived = CalculateGeneticCredit(positionSize, vix);
            Console.WriteLine($"Credit Calculated: ${creditReceived}");
            
            var winRate = CalculateGeneticWinRate(strategy);
            Console.WriteLine($"Win Rate: {winRate:P1}");
            
            var marketMovement = GenerateGeneticMovement(vix, spxPrice);
            Console.WriteLine($"Market Movement: ${marketMovement}");
            
            var withinProfitZone = Math.Abs(marketMovement) < (strategy.SpreadWidth * 0.75m);
            Console.WriteLine($"Within Profit Zone: {withinProfitZone}");
            
            var isWin = withinProfitZone && (_random.NextDouble() < (double)winRate);
            Console.WriteLine($"Trade Outcome: {(isWin ? "WIN" : "LOSS")}");
            
            decimal grossPnL;
            if (isWin)
            {
                grossPnL = creditReceived * strategy.ProfitTargetPct;
            }
            else
            {
                var maxLoss = strategy.SpreadWidth * 100 - creditReceived;
                grossPnL = -Math.Min(creditReceived * strategy.StopLossPct, maxLoss);
            }
            
            Console.WriteLine($"Gross P&L: ${grossPnL}");
            
            var commission = 8m; // 4 legs * $2
            var slippage = positionSize * 0.03m;
            var netPnL = grossPnL - commission - slippage;
            
            Console.WriteLine($"Commission: ${commission}");
            Console.WriteLine($"Slippage: ${slippage}");
            Console.WriteLine($"Net P&L: ${netPnL}");
            
            // FOUND THE BUG! Let's analyze the credit calculation
            Console.WriteLine();
            Console.WriteLine("🚨 BUG ANALYSIS:");
            Console.WriteLine($"Credit as % of position: {creditReceived / positionSize:P2}");
            Console.WriteLine($"This seems way too low for an Iron Condor!");
            Console.WriteLine($"Real Iron Condor should get ~1.5-3% credit");
        }
        
        private decimal CalculateGeneticCredit(decimal positionSize, decimal vix)
        {
            var baseCreditPct = 0.025m; // This might be the bug - too low
            var vixBonus = 1.0m + (vix / 100m);
            return positionSize * baseCreditPct * vixBonus;
        }
        
        private decimal CalculateGeneticWinRate(TestStrategy strategy)
        {
            var baseWinRate = 0.85m; // Iron Condor base
            var deltaAdjustment = 1.0m - (strategy.ShortDelta * 1.5m);
            return baseWinRate * deltaAdjustment * (strategy.WinRateTarget / 0.75m);
        }
        
        private decimal GenerateGeneticMovement(decimal vix, decimal spx)
        {
            var dailyVol = vix / 100m / (decimal)Math.Sqrt(252);
            return (decimal)(_random.NextDouble() - 0.5) * 2 * dailyVol * spx;
        }
        
        public static void Main(string[] args)
        {
            var debugger = new TradeExecutionDebugger();
            debugger.DebugIronCondorExecution();
        }
        
        public class TestStrategy
        {
            public string Type { get; set; } = "";
            public decimal SpreadWidth { get; set; }
            public decimal ShortDelta { get; set; }
            public decimal WinRateTarget { get; set; }
            public decimal ProfitTargetPct { get; set; }
            public decimal StopLossPct { get; set; }
        }
    }
}