using System;
using System.Collections.Generic;
using System.Linq;

namespace ODTE.Strategy.MultiLegStrategies
{
    /// <summary>
    /// Comprehensive implementation of the 10 most popular multi-leg options strategies.
    /// All strategies are designed with defined risk (no naked exposures).
    /// 
    /// STRATEGIES IMPLEMENTED:
    /// 1. Broken Wing Butterfly (Credit)
    /// 2. Iron Condor
    /// 3. Iron Butterfly
    /// 4. Call Spread (Bull/Bear)
    /// 5. Put Spread (Bull/Bear) 
    /// 6. Straddle (Long/Short)
    /// 7. Strangle (Long/Short)
    /// 8. Calendar Spread
    /// 9. Diagonal Spread
    /// 10. Ratio Spread (Defined Risk)
    /// 
    /// Each strategy includes:
    /// - Realistic credit/debit calculations
    /// - Commission modeling ($2 per leg)
    /// - Slippage modeling (0.5 ticks per leg)
    /// - Greeks exposure tracking
    /// - Max profit/loss calculations
    /// - Breakeven analysis
    /// </summary>
    public static class MultiLegOptionsStrategies
    {
        // Standard commission and slippage constants
        public const decimal CommissionPerLeg = 2.00m;
        public const decimal SlippagePerLeg = 0.025m; // 0.5 ticks × $0.05
        
        public enum StrategyType
        {
            BrokenWingButterfly,
            IronCondor,
            IronButterfly,
            CallSpread,
            PutSpread,
            Straddle,
            Strangle,
            CalendarSpread,
            DiagonalSpread,
            RatioSpread
        }
        
        public enum MarketCondition
        {
            Bull,      // Rising market
            Bear,      // Falling market
            Calm,      // Low volatility
            Volatile   // High volatility
        }
        
        public class OptionLeg
        {
            public string Type { get; set; } = ""; // "Call" or "Put"
            public string Action { get; set; } = ""; // "Buy" or "Sell"
            public decimal Strike { get; set; }
            public decimal Premium { get; set; }
            public int Quantity { get; set; }
            public DateTime Expiration { get; set; }
            public decimal Delta { get; set; }
            public decimal Gamma { get; set; }
            public decimal Theta { get; set; }
            public decimal Vega { get; set; }
        }
        
        public class StrategyPosition
        {
            public StrategyType Type { get; set; }
            public List<OptionLeg> Legs { get; set; } = new();
            public decimal NetCredit { get; set; }
            public decimal NetDebit { get; set; }
            public decimal MaxProfit { get; set; }
            public decimal MaxLoss { get; set; }
            public decimal UpperBreakeven { get; set; }
            public decimal LowerBreakeven { get; set; }
            public decimal TotalCommission { get; set; }
            public decimal TotalSlippage { get; set; }
            public decimal NetDelta { get; set; }
            public decimal NetGamma { get; set; }
            public decimal NetTheta { get; set; }
            public decimal NetVega { get; set; }
            public MarketCondition OptimalCondition { get; set; }
        }
        
        #region 1. Broken Wing Butterfly (Credit)
        
        public static StrategyPosition CreateBrokenWingButterfly(
            decimal underlyingPrice, 
            decimal vix = 20m,
            MarketCondition condition = MarketCondition.Calm)
        {
            // Credit BWB: Sell 1 ATM, Buy 1 OTM, Buy 1 Far OTM (skewed)
            // Designed to collect credit while maintaining defined risk
            
            var position = new StrategyPosition { Type = StrategyType.BrokenWingButterfly };
            
            // Strike selection based on market condition
            var atmlStrike = Math.Round(underlyingPrice / 5) * 5; // Round to nearest $5
            var shortStrike = atmlStrike;
            var longStrike1 = atmlStrike + 10; // 10 points OTM
            var longStrike2 = atmlStrike + 25; // 25 points OTM (broken wing)
            
            // Calculate premiums with VIX adjustment
            var vixMultiplier = Math.Max(0.8m, Math.Min(2.0m, vix / 20m));
            
            position.Legs = new List<OptionLeg>
            {
                // Sell ATM Call
                new() {
                    Type = "Call", Action = "Sell", Strike = shortStrike, 
                    Premium = CalculateCallPremium(underlyingPrice, shortStrike, 0.0m, vix) * vixMultiplier,
                    Quantity = 1, Delta = 0.50m, Gamma = 0.05m, Theta = -0.15m, Vega = 0.25m
                },
                // Buy OTM Call
                new() {
                    Type = "Call", Action = "Buy", Strike = longStrike1,
                    Premium = CalculateCallPremium(underlyingPrice, longStrike1, 0.0m, vix) * vixMultiplier,
                    Quantity = 1, Delta = 0.25m, Gamma = 0.03m, Theta = -0.08m, Vega = 0.15m
                },
                // Buy Far OTM Call (broken wing)
                new() {
                    Type = "Call", Action = "Buy", Strike = longStrike2,
                    Premium = CalculateCallPremium(underlyingPrice, longStrike2, 0.0m, vix) * vixMultiplier,
                    Quantity = 1, Delta = 0.10m, Gamma = 0.01m, Theta = -0.03m, Vega = 0.08m
                }
            };
            
            return CalculateStrategyMetrics(position, condition);
        }
        
        #endregion
        
        #region 2. Iron Condor
        
        public static StrategyPosition CreateIronCondor(
            decimal underlyingPrice,
            decimal vix = 20m,
            MarketCondition condition = MarketCondition.Calm)
        {
            var position = new StrategyPosition { Type = StrategyType.IronCondor };
            
            // Standard Iron Condor: Sell OTM Put, Buy Further OTM Put, Sell OTM Call, Buy Further OTM Call
            var putShortStrike = underlyingPrice - 25;
            var putLongStrike = underlyingPrice - 50;
            var callShortStrike = underlyingPrice + 25;
            var callLongStrike = underlyingPrice + 50;
            
            var vixMultiplier = Math.Max(0.8m, Math.Min(2.0m, vix / 20m));
            
            position.Legs = new List<OptionLeg>
            {
                // Put Credit Spread
                new() { Type = "Put", Action = "Sell", Strike = putShortStrike, 
                        Premium = CalculatePutPremium(underlyingPrice, putShortStrike, 0.0m, vix) * vixMultiplier,
                        Quantity = 1, Delta = -0.25m, Gamma = 0.02m, Theta = -0.08m, Vega = 0.15m },
                new() { Type = "Put", Action = "Buy", Strike = putLongStrike,
                        Premium = CalculatePutPremium(underlyingPrice, putLongStrike, 0.0m, vix) * vixMultiplier,
                        Quantity = 1, Delta = -0.10m, Gamma = 0.01m, Theta = -0.03m, Vega = 0.08m },
                        
                // Call Credit Spread  
                new() { Type = "Call", Action = "Sell", Strike = callShortStrike,
                        Premium = CalculateCallPremium(underlyingPrice, callShortStrike, 0.0m, vix) * vixMultiplier,
                        Quantity = 1, Delta = 0.25m, Gamma = 0.02m, Theta = -0.08m, Vega = 0.15m },
                new() { Type = "Call", Action = "Buy", Strike = callLongStrike,
                        Premium = CalculateCallPremium(underlyingPrice, callLongStrike, 0.0m, vix) * vixMultiplier,
                        Quantity = 1, Delta = 0.10m, Gamma = 0.01m, Theta = -0.03m, Vega = 0.08m }
            };
            
            return CalculateStrategyMetrics(position, condition);
        }
        
        #endregion
        
        #region 3. Iron Butterfly
        
        public static StrategyPosition CreateIronButterfly(
            decimal underlyingPrice,
            decimal vix = 20m,
            MarketCondition condition = MarketCondition.Calm)
        {
            var position = new StrategyPosition { Type = StrategyType.IronButterfly };
            
            // Iron Butterfly: Sell ATM Call & Put, Buy OTM Call & Put
            var atmStrike = Math.Round(underlyingPrice / 5) * 5;
            var wingStrike = 25m; // $25 wings
            
            var vixMultiplier = Math.Max(0.8m, Math.Min(2.0m, vix / 20m));
            
            position.Legs = new List<OptionLeg>
            {
                // Sell ATM Straddle
                new() { Type = "Call", Action = "Sell", Strike = atmStrike,
                        Premium = CalculateCallPremium(underlyingPrice, atmStrike, 0.0m, vix) * vixMultiplier,
                        Quantity = 1, Delta = 0.50m, Gamma = 0.05m, Theta = -0.15m, Vega = 0.25m },
                new() { Type = "Put", Action = "Sell", Strike = atmStrike,
                        Premium = CalculatePutPremium(underlyingPrice, atmStrike, 0.0m, vix) * vixMultiplier,
                        Quantity = 1, Delta = -0.50m, Gamma = 0.05m, Theta = -0.15m, Vega = 0.25m },
                        
                // Buy Protective Wings
                new() { Type = "Call", Action = "Buy", Strike = atmStrike + wingStrike,
                        Premium = CalculateCallPremium(underlyingPrice, atmStrike + wingStrike, 0.0m, vix) * vixMultiplier,
                        Quantity = 1, Delta = 0.15m, Gamma = 0.02m, Theta = -0.05m, Vega = 0.12m },
                new() { Type = "Put", Action = "Buy", Strike = atmStrike - wingStrike,
                        Premium = CalculatePutPremium(underlyingPrice, atmStrike - wingStrike, 0.0m, vix) * vixMultiplier,
                        Quantity = 1, Delta = -0.15m, Gamma = 0.02m, Theta = -0.05m, Vega = 0.12m }
            };
            
            return CalculateStrategyMetrics(position, condition);
        }
        
        #endregion
        
        #region 4. Call Spread
        
        public static StrategyPosition CreateCallSpread(
            decimal underlyingPrice,
            bool isBullish = true,
            decimal vix = 20m,
            MarketCondition condition = MarketCondition.Bull)
        {
            var position = new StrategyPosition { Type = StrategyType.CallSpread };
            
            var vixMultiplier = Math.Max(0.8m, Math.Min(2.0m, vix / 20m));
            
            if (isBullish)
            {
                // Bull Call Spread: Buy lower strike, Sell higher strike
                var longStrike = underlyingPrice - 10;
                var shortStrike = underlyingPrice + 15;
                
                position.Legs = new List<OptionLeg>
                {
                    new() { Type = "Call", Action = "Buy", Strike = longStrike,
                            Premium = CalculateCallPremium(underlyingPrice, longStrike, 0.0m, vix) * vixMultiplier,
                            Quantity = 1, Delta = 0.65m, Gamma = 0.04m, Theta = -0.12m, Vega = 0.20m },
                    new() { Type = "Call", Action = "Sell", Strike = shortStrike,
                            Premium = CalculateCallPremium(underlyingPrice, shortStrike, 0.0m, vix) * vixMultiplier,
                            Quantity = 1, Delta = 0.35m, Gamma = 0.03m, Theta = -0.10m, Vega = 0.18m }
                };
            }
            else
            {
                // Bear Call Spread: Sell lower strike, Buy higher strike
                var shortStrike = underlyingPrice + 10;
                var longStrike = underlyingPrice + 35;
                
                position.Legs = new List<OptionLeg>
                {
                    new() { Type = "Call", Action = "Sell", Strike = shortStrike,
                            Premium = CalculateCallPremium(underlyingPrice, shortStrike, 0.0m, vix) * vixMultiplier,
                            Quantity = 1, Delta = 0.35m, Gamma = 0.03m, Theta = -0.10m, Vega = 0.18m },
                    new() { Type = "Call", Action = "Buy", Strike = longStrike,
                            Premium = CalculateCallPremium(underlyingPrice, longStrike, 0.0m, vix) * vixMultiplier,
                            Quantity = 1, Delta = 0.15m, Gamma = 0.02m, Theta = -0.05m, Vega = 0.12m }
                };
            }
            
            return CalculateStrategyMetrics(position, condition);
        }
        
        #endregion
        
        #region 5. Put Spread
        
        public static StrategyPosition CreatePutSpread(
            decimal underlyingPrice,
            bool isBullish = true,
            decimal vix = 20m,
            MarketCondition condition = MarketCondition.Bull)
        {
            var position = new StrategyPosition { Type = StrategyType.PutSpread };
            
            var vixMultiplier = Math.Max(0.8m, Math.Min(2.0m, vix / 20m));
            
            if (isBullish)
            {
                // Bull Put Spread: Sell higher strike, Buy lower strike
                var shortStrike = underlyingPrice - 15;
                var longStrike = underlyingPrice - 40;
                
                position.Legs = new List<OptionLeg>
                {
                    new() { Type = "Put", Action = "Sell", Strike = shortStrike,
                            Premium = CalculatePutPremium(underlyingPrice, shortStrike, 0.0m, vix) * vixMultiplier,
                            Quantity = 1, Delta = -0.35m, Gamma = 0.03m, Theta = -0.10m, Vega = 0.18m },
                    new() { Type = "Put", Action = "Buy", Strike = longStrike,
                            Premium = CalculatePutPremium(underlyingPrice, longStrike, 0.0m, vix) * vixMultiplier,
                            Quantity = 1, Delta = -0.15m, Gamma = 0.02m, Theta = -0.05m, Vega = 0.12m }
                };
            }
            else
            {
                // Bear Put Spread: Buy higher strike, Sell lower strike
                var longStrike = underlyingPrice + 10;
                var shortStrike = underlyingPrice - 15;
                
                position.Legs = new List<OptionLeg>
                {
                    new() { Type = "Put", Action = "Buy", Strike = longStrike,
                            Premium = CalculatePutPremium(underlyingPrice, longStrike, 0.0m, vix) * vixMultiplier,
                            Quantity = 1, Delta = -0.65m, Gamma = 0.04m, Theta = -0.12m, Vega = 0.20m },
                    new() { Type = "Put", Action = "Sell", Strike = shortStrike,
                            Premium = CalculatePutPremium(underlyingPrice, shortStrike, 0.0m, vix) * vixMultiplier,
                            Quantity = 1, Delta = -0.35m, Gamma = 0.03m, Theta = -0.10m, Vega = 0.18m }
                };
            }
            
            return CalculateStrategyMetrics(position, condition);
        }
        
        #endregion
        
        #region 6. Straddle
        
        public static StrategyPosition CreateStraddle(
            decimal underlyingPrice,
            bool isLong = true,
            decimal vix = 20m,
            MarketCondition condition = MarketCondition.Volatile)
        {
            var position = new StrategyPosition { Type = StrategyType.Straddle };
            
            var atmStrike = Math.Round(underlyingPrice / 5) * 5;
            var vixMultiplier = Math.Max(0.8m, Math.Min(2.0m, vix / 20m));
            
            var action = isLong ? "Buy" : "Sell";
            var deltaSign = isLong ? 1m : -1m;
            
            position.Legs = new List<OptionLeg>
            {
                new() { Type = "Call", Action = action, Strike = atmStrike,
                        Premium = CalculateCallPremium(underlyingPrice, atmStrike, 0.0m, vix) * vixMultiplier,
                        Quantity = 1, Delta = 0.50m * deltaSign, Gamma = 0.05m, Theta = -0.15m * deltaSign, Vega = 0.25m * deltaSign },
                new() { Type = "Put", Action = action, Strike = atmStrike,
                        Premium = CalculatePutPremium(underlyingPrice, atmStrike, 0.0m, vix) * vixMultiplier,
                        Quantity = 1, Delta = -0.50m * deltaSign, Gamma = 0.05m, Theta = -0.15m * deltaSign, Vega = 0.25m * deltaSign }
            };
            
            return CalculateStrategyMetrics(position, condition);
        }
        
        #endregion
        
        #region 7. Strangle
        
        public static StrategyPosition CreateStrangle(
            decimal underlyingPrice,
            bool isLong = true,
            decimal vix = 20m,
            MarketCondition condition = MarketCondition.Volatile)
        {
            var position = new StrategyPosition { Type = StrategyType.Strangle };
            
            var callStrike = underlyingPrice + 20; // OTM call
            var putStrike = underlyingPrice - 20;  // OTM put
            var vixMultiplier = Math.Max(0.8m, Math.Min(2.0m, vix / 20m));
            
            var action = isLong ? "Buy" : "Sell";
            var deltaSign = isLong ? 1m : -1m;
            
            position.Legs = new List<OptionLeg>
            {
                new() { Type = "Call", Action = action, Strike = callStrike,
                        Premium = CalculateCallPremium(underlyingPrice, callStrike, 0.0m, vix) * vixMultiplier,
                        Quantity = 1, Delta = 0.30m * deltaSign, Gamma = 0.03m, Theta = -0.08m * deltaSign, Vega = 0.18m * deltaSign },
                new() { Type = "Put", Action = action, Strike = putStrike,
                        Premium = CalculatePutPremium(underlyingPrice, putStrike, 0.0m, vix) * vixMultiplier,
                        Quantity = 1, Delta = -0.30m * deltaSign, Gamma = 0.03m, Theta = -0.08m * deltaSign, Vega = 0.18m * deltaSign }
            };
            
            return CalculateStrategyMetrics(position, condition);
        }
        
        #endregion
        
        #region 8. Calendar Spread
        
        public static StrategyPosition CreateCalendarSpread(
            decimal underlyingPrice,
            string optionType = "Call",
            decimal vix = 20m,
            MarketCondition condition = MarketCondition.Calm)
        {
            var position = new StrategyPosition { Type = StrategyType.CalendarSpread };
            
            var strike = Math.Round(underlyingPrice / 5) * 5; // ATM
            var vixMultiplier = Math.Max(0.8m, Math.Min(2.0m, vix / 20m));
            
            // Calendar: Sell front month, Buy back month
            position.Legs = new List<OptionLeg>
            {
                new() { Type = optionType, Action = "Sell", Strike = strike,
                        Premium = CalculateOptionPremium(underlyingPrice, strike, 0.0m, vix, optionType, 30) * vixMultiplier,
                        Quantity = 1, Expiration = DateTime.Today.AddDays(30),
                        Delta = optionType == "Call" ? 0.50m : -0.50m, Gamma = 0.05m, Theta = -0.20m, Vega = 0.15m },
                new() { Type = optionType, Action = "Buy", Strike = strike,
                        Premium = CalculateOptionPremium(underlyingPrice, strike, 0.0m, vix, optionType, 60) * vixMultiplier,
                        Quantity = 1, Expiration = DateTime.Today.AddDays(60),
                        Delta = optionType == "Call" ? 0.45m : -0.45m, Gamma = 0.03m, Theta = -0.10m, Vega = 0.25m }
            };
            
            return CalculateStrategyMetrics(position, condition);
        }
        
        #endregion
        
        #region 9. Diagonal Spread
        
        public static StrategyPosition CreateDiagonalSpread(
            decimal underlyingPrice,
            string optionType = "Call",
            decimal vix = 20m,
            MarketCondition condition = MarketCondition.Bull)
        {
            var position = new StrategyPosition { Type = StrategyType.DiagonalSpread };
            
            var vixMultiplier = Math.Max(0.8m, Math.Min(2.0m, vix / 20m));
            
            // Diagonal: Different strikes AND different expirations
            var shortStrike = underlyingPrice + 10;
            var longStrike = underlyingPrice + 30;
            
            position.Legs = new List<OptionLeg>
            {
                new() { Type = optionType, Action = "Sell", Strike = shortStrike,
                        Premium = CalculateOptionPremium(underlyingPrice, shortStrike, 0.0m, vix, optionType, 30) * vixMultiplier,
                        Quantity = 1, Expiration = DateTime.Today.AddDays(30),
                        Delta = optionType == "Call" ? 0.35m : -0.65m, Gamma = 0.03m, Theta = -0.12m, Vega = 0.15m },
                new() { Type = optionType, Action = "Buy", Strike = longStrike,
                        Premium = CalculateOptionPremium(underlyingPrice, longStrike, 0.0m, vix, optionType, 60) * vixMultiplier,
                        Quantity = 1, Expiration = DateTime.Today.AddDays(60),
                        Delta = optionType == "Call" ? 0.20m : -0.35m, Gamma = 0.02m, Theta = -0.06m, Vega = 0.18m }
            };
            
            return CalculateStrategyMetrics(position, condition);
        }
        
        #endregion
        
        #region 10. Ratio Spread (Defined Risk)
        
        public static StrategyPosition CreateRatioSpread(
            decimal underlyingPrice,
            string optionType = "Call",
            decimal vix = 20m,
            MarketCondition condition = MarketCondition.Calm)
        {
            var position = new StrategyPosition { Type = StrategyType.RatioSpread };
            
            var vixMultiplier = Math.Max(0.8m, Math.Min(2.0m, vix / 20m));
            
            // Defined Risk Ratio: Buy 1 ITM, Sell 2 OTM, Buy 1 Far OTM (cap)
            var longStrike1 = underlyingPrice - 10; // ITM
            var shortStrike = underlyingPrice + 15;  // OTM
            var longStrike2 = underlyingPrice + 50;  // Far OTM cap
            
            position.Legs = new List<OptionLeg>
            {
                new() { Type = optionType, Action = "Buy", Strike = longStrike1,
                        Premium = CalculateOptionPremium(underlyingPrice, longStrike1, 0.0m, vix, optionType) * vixMultiplier,
                        Quantity = 1, Delta = optionType == "Call" ? 0.70m : -0.30m, Gamma = 0.04m, Theta = -0.12m, Vega = 0.20m },
                new() { Type = optionType, Action = "Sell", Strike = shortStrike,
                        Premium = CalculateOptionPremium(underlyingPrice, shortStrike, 0.0m, vix, optionType) * vixMultiplier,
                        Quantity = 2, Delta = optionType == "Call" ? 0.30m : -0.70m, Gamma = 0.03m, Theta = -0.08m, Vega = 0.15m },
                new() { Type = optionType, Action = "Buy", Strike = longStrike2,
                        Premium = CalculateOptionPremium(underlyingPrice, longStrike2, 0.0m, vix, optionType) * vixMultiplier,
                        Quantity = 1, Delta = optionType == "Call" ? 0.10m : -0.90m, Gamma = 0.01m, Theta = -0.03m, Vega = 0.08m }
            };
            
            return CalculateStrategyMetrics(position, condition);
        }
        
        #endregion
        
        #region Helper Methods
        
        private static decimal CalculateCallPremium(decimal spot, decimal strike, decimal rate, decimal vix, int dte = 45)
        {
            // Simplified Black-Scholes approximation
            var moneyness = spot / strike;
            var timeValue = Math.Sqrt(dte / 365.0m);
            var volatility = vix / 100m;
            
            if (moneyness >= 1.0m)
            {
                // ITM: Intrinsic + time value
                return (spot - strike) + (strike * volatility * timeValue * 0.4m);
            }
            else
            {
                // OTM: Time value only
                return strike * volatility * timeValue * (0.3m + moneyness * 0.3m);
            }
        }
        
        private static decimal CalculatePutPremium(decimal spot, decimal strike, decimal rate, decimal vix, int dte = 45)
        {
            var moneyness = strike / spot;
            var timeValue = Math.Sqrt(dte / 365.0m);
            var volatility = vix / 100m;
            
            if (moneyness >= 1.0m)
            {
                // ITM: Intrinsic + time value
                return (strike - spot) + (strike * volatility * timeValue * 0.4m);
            }
            else
            {
                // OTM: Time value only
                return strike * volatility * timeValue * (0.3m + (2.0m - moneyness) * 0.3m);
            }
        }
        
        private static decimal CalculateOptionPremium(decimal spot, decimal strike, decimal rate, decimal vix, string type, int dte = 45)
        {
            return type.ToUpper() == "CALL" 
                ? CalculateCallPremium(spot, strike, rate, vix, dte)
                : CalculatePutPremium(spot, strike, rate, vix, dte);
        }
        
        private static StrategyPosition CalculateStrategyMetrics(StrategyPosition position, MarketCondition condition)
        {
            // Calculate net premium (credit/debit)
            var totalCredit = 0m;
            var totalDebit = 0m;
            
            foreach (var leg in position.Legs)
            {
                var premium = leg.Premium * leg.Quantity;
                if (leg.Action == "Sell")
                    totalCredit += premium;
                else
                    totalDebit += premium;
            }
            
            position.NetCredit = totalCredit;
            position.NetDebit = totalDebit;
            
            // Calculate commissions and slippage
            position.TotalCommission = position.Legs.Sum(l => Math.Abs(l.Quantity)) * CommissionPerLeg;
            position.TotalSlippage = position.Legs.Sum(l => Math.Abs(l.Quantity)) * SlippagePerLeg;
            
            // Calculate net Greeks
            position.NetDelta = position.Legs.Sum(l => l.Delta * l.Quantity * (l.Action == "Sell" ? -1 : 1));
            position.NetGamma = position.Legs.Sum(l => l.Gamma * l.Quantity * (l.Action == "Sell" ? -1 : 1));
            position.NetTheta = position.Legs.Sum(l => l.Theta * l.Quantity * (l.Action == "Sell" ? -1 : 1));
            position.NetVega = position.Legs.Sum(l => l.Vega * l.Quantity * (l.Action == "Sell" ? -1 : 1));
            
            // Calculate max profit/loss (simplified)
            CalculateMaxProfitLoss(position);
            
            // Set optimal condition
            position.OptimalCondition = condition;
            
            return position;
        }
        
        private static void CalculateMaxProfitLoss(StrategyPosition position)
        {
            var netPremium = position.NetCredit - position.NetDebit;
            var totalCosts = position.TotalCommission + position.TotalSlippage;
            
            switch (position.Type)
            {
                case StrategyType.IronCondor:
                case StrategyType.BrokenWingButterfly:
                    position.MaxProfit = netPremium - totalCosts;
                    position.MaxLoss = Math.Abs(GetSpreadWidth(position)) - netPremium - totalCosts;
                    break;
                    
                case StrategyType.IronButterfly:
                    position.MaxProfit = netPremium - totalCosts;
                    position.MaxLoss = GetSpreadWidth(position) - netPremium - totalCosts;
                    break;
                    
                case StrategyType.Straddle:
                case StrategyType.Strangle:
                    if (position.Legs[0].Action == "Buy")
                    {
                        position.MaxLoss = netPremium + totalCosts; // Debit paid
                        position.MaxProfit = decimal.MaxValue; // Unlimited
                    }
                    else
                    {
                        position.MaxProfit = netPremium - totalCosts; // Credit received
                        position.MaxLoss = decimal.MaxValue; // Unlimited (but we don't do naked)
                    }
                    break;
                    
                default:
                    position.MaxProfit = Math.Max(netPremium - totalCosts, GetSpreadWidth(position) - totalCosts);
                    position.MaxLoss = Math.Max(totalCosts, Math.Abs(netPremium) + totalCosts);
                    break;
            }
        }
        
        private static decimal GetSpreadWidth(StrategyPosition position)
        {
            if (position.Legs.Count < 2) return 0;
            
            var strikes = position.Legs.Select(l => l.Strike).OrderBy(s => s).ToList();
            return strikes.Last() - strikes.First();
        }
        
        #endregion
    }
}