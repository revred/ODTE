using Microsoft.Data.Sqlite;
using ODTE.Contracts.Data;
using ODTE.Execution.Engine;
using ODTE.Historical.DistributedStorage;
using ODTE.Strategy.Hedging;
using ODTE.Strategy.SPX30DTE.Core;
using ODTE.Strategy.SPX30DTE.Probes;
using ODTE.Strategy.SPX30DTE.Risk;

namespace ODTE.Strategy.SPX30DTE.Backtests
{
    public partial class SPX30DTERealisticBacktester
    {
        private readonly DistributedDatabaseManager _dataManager;
        private readonly RealisticFillEngine _fillEngine;
        private readonly VIXHedgeManager _hedgeManager;
        private readonly SPXBWBEngine _coreEngine;
        private readonly XSPProbeScout _probeScout;
        private readonly SPX30DTERevFibNotchManager _riskManager;

        private readonly decimal COMMISSION_PER_CONTRACT = 0.50m;
        private readonly decimal BASE_COMMISSION_PER_TRADE = 1.50m;
        private readonly decimal REGULATORY_FEES_RATE = 0.0000218m; // SEC + FINRA fees
        private readonly decimal EXCHANGE_FEES_PER_CONTRACT = 0.15m;

        public SPX30DTERealisticBacktester(
            DistributedDatabaseManager dataManager,
            RealisticFillEngine fillEngine)
        {
            _dataManager = dataManager ?? throw new ArgumentNullException(nameof(dataManager));
            _fillEngine = fillEngine ?? throw new ArgumentNullException(nameof(fillEngine));
            _hedgeManager = new VIXHedgeManager(dataManager, fillEngine);
            _coreEngine = new SPXBWBEngine(dataManager, fillEngine);
            _probeScout = new XSPProbeScout(dataManager, fillEngine);
            _riskManager = new SPX30DTERevFibNotchManager();
        }

        public async Task<BacktestResults> RunRealisticBacktest(
            SPX30DTEConfig config,
            DateTime startDate,
            DateTime endDate,
            string ledgerPath = null)
        {
            var results = new BacktestResults
            {
                Config = config,
                StartDate = startDate,
                EndDate = endDate,
                Trades = new List<BacktestTrade>(),
                DailyPnL = new Dictionary<DateTime, decimal>(),
                RiskMetrics = new BacktestRiskMetrics()
            };

            decimal portfolioValue = 100000m; // Starting capital
            decimal currentNotchLimit = 3200m; // Starting at balanced level
            var openPositions = new Dictionary<string, OpenPosition>();

            SqliteConnection ledgerConnection = null;
            if (!string.IsNullOrEmpty(ledgerPath))
            {
                ledgerConnection = await CreateLedgerDatabase(ledgerPath);
            }

            try
            {
                var tradingDays = GetTradingDays(startDate, endDate);

                foreach (var date in tradingDays)
                {
                    var dailyPnL = 0m;

                    // Update open positions and calculate daily P&L
                    dailyPnL += await UpdateOpenPositions(openPositions, date);

                    // Check for position exits
                    await CheckPositionExits(openPositions, date, results.Trades, ledgerConnection);

                    // Update risk management based on recent performance
                    currentNotchLimit = UpdateRiskLimit(results.DailyPnL, currentNotchLimit);

                    // Generate new positions based on strategy rules
                    await GenerateNewPositions(config, date, currentNotchLimit, openPositions, results.Trades, ledgerConnection);

                    // Record daily P&L
                    results.DailyPnL[date] = dailyPnL;
                    portfolioValue += dailyPnL;

                    // Update high water mark and drawdown tracking
                    UpdateDrawdownMetrics(results.RiskMetrics, portfolioValue, date);

                    // Log progress every quarter
                    if (date.Day == 1 && date.Month % 3 == 1)
                    {
                        Console.WriteLine($"Backtest Progress: {date:yyyy-MM-dd}, Portfolio: ${portfolioValue:N0}, Drawdown: {results.RiskMetrics.CurrentDrawdown:P2}");
                    }
                }

                // Calculate final performance metrics
                await CalculateFinalMetrics(results, portfolioValue);

                return results;
            }
            finally
            {
                ledgerConnection?.Dispose();
            }
        }

        private async Task GenerateNewPositions(
            SPX30DTEConfig config,
            DateTime date,
            decimal notchLimit,
            Dictionary<string, OpenPosition> openPositions,
            List<BacktestTrade> trades,
            SqliteConnection ledgerConnection)
        {
            // Get market data for decision making
            var spxPrice = await GetUnderlyingPrice("SPX", date);
            var vixPrice = await GetUnderlyingPrice("VIX", date);

            // Probe market sentiment with XSP
            var probeSignal = await _probeScout.AnalyzeMarketMood(date);

            // Check if we should enter new SPX BWB position
            if (ShouldEnterCoreBWB(date, openPositions, probeSignal))
            {
                var bwbTrade = await EnterBWBPosition(config, date, spxPrice, probeSignal, notchLimit);
                if (bwbTrade != null)
                {
                    openPositions[bwbTrade.TradeId] = bwbTrade;
                    await RecordTradeEntry(bwbTrade, trades, ledgerConnection);
                }
            }

            // Check VIX hedge requirements
            if (ShouldEnterVIXHedge(date, openPositions, vixPrice, probeSignal))
            {
                var hedgeTrade = await EnterVIXHedgePosition(config, date, vixPrice, notchLimit);
                if (hedgeTrade != null)
                {
                    openPositions[hedgeTrade.TradeId] = hedgeTrade;
                    await RecordTradeEntry(hedgeTrade, trades, ledgerConnection);
                }
            }

            // Enter XSP probe positions if conditions are right
            if (ShouldEnterXSPProbe(date, openPositions, probeSignal))
            {
                var probeTrade = await EnterProbePosition(config, date, notchLimit);
                if (probeTrade != null)
                {
                    openPositions[probeTrade.TradeId] = probeTrade;
                    await RecordTradeEntry(probeTrade, trades, ledgerConnection);
                }
            }
        }

        private async Task<OpenPosition> EnterBWBPosition(
            SPX30DTEConfig config,
            DateTime date,
            decimal spxPrice,
            ProbeSignal probeSignal,
            decimal notchLimit)
        {
            try
            {
                // Get 30DTE expiry
                var expiryDate = GetNearestExpiry(date, 30);
                var spxChain = await _dataManager.GetOptionsChain("SPX", date, expiryDate);

                if (spxChain?.Options == null || !spxChain.Options.Any())
                    return null;

                // Calculate BWB strikes based on market sentiment
                var strikes = CalculateBWBStrikes(spxPrice, probeSignal, config);

                // Build the BWB structure
                var longPut = GetClosestOption(spxChain, strikes.LongStrike, OptionType.Put);
                var shortPut1 = GetClosestOption(spxChain, strikes.ShortStrike1, OptionType.Put);
                var shortPut2 = GetClosestOption(spxChain, strikes.ShortStrike2, OptionType.Put);

                if (longPut == null || shortPut1 == null || shortPut2 == null)
                    return null;

                // Calculate position size based on notch limit
                var targetCredit = Math.Min(notchLimit * 0.15m, 2000m); // 15% of notch limit, max $2000
                var contracts = Math.Max(1, (int)(targetCredit / CalculateExpectedCredit(longPut, shortPut1, shortPut2)));

                // Execute realistic fills
                var longFill = await _fillEngine.SimulateFill(new OrderRequest
                {
                    Symbol = longPut.Symbol,
                    Quantity = contracts,
                    OrderType = OrderType.Market,
                    Side = OrderSide.Buy,
                    Timestamp = date
                });

                var shortFill1 = await _fillEngine.SimulateFill(new OrderRequest
                {
                    Symbol = shortPut1.Symbol,
                    Quantity = contracts,
                    OrderType = OrderType.Market,
                    Side = OrderSide.Sell,
                    Timestamp = date
                });

                var shortFill2 = await _fillEngine.SimulateFill(new OrderRequest
                {
                    Symbol = shortPut2.Symbol,
                    Quantity = contracts,
                    OrderType = OrderType.Market,
                    Side = OrderSide.Sell,
                    Timestamp = date
                });

                // Calculate net credit received
                var netCredit = (-longFill.FillPrice + shortFill1.FillPrice + shortFill2.FillPrice) * contracts * 100;
                var totalCommissions = CalculateCommissions(3, contracts); // 3-leg trade
                var netCreditAfterCosts = netCredit - totalCommissions;

                return new OpenPosition
                {
                    TradeId = $"SPX_BWB_{date:yyyyMMdd}_{Guid.NewGuid().ToString("N")[..8]}",
                    Symbol = "SPX",
                    TradeType = "BWB",
                    EntryDate = date,
                    ExpiryDate = expiryDate,
                    Contracts = contracts,
                    EntryCredit = netCreditAfterCosts,
                    MaxLoss = CalculateMaxLoss(strikes, contracts),
                    Legs = new List<PositionLeg>
                    {
                        new() { Symbol = longPut.Symbol, Quantity = contracts, Side = "Buy", EntryPrice = longFill.FillPrice },
                        new() { Symbol = shortPut1.Symbol, Quantity = contracts, Side = "Sell", EntryPrice = shortFill1.FillPrice },
                        new() { Symbol = shortPut2.Symbol, Quantity = contracts, Side = "Sell", EntryPrice = shortFill2.FillPrice }
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error entering BWB position on {date}: {ex.Message}");
                return null;
            }
        }

        private async Task<OpenPosition> EnterVIXHedgePosition(
            SPX30DTEConfig config,
            DateTime date,
            decimal vixPrice,
            decimal notchLimit)
        {
            try
            {
                var expiryDate = GetNearestExpiry(date, 30);
                var vixChain = await _dataManager.GetOptionsChain("VIX", date, expiryDate);

                if (vixChain?.Options == null || !vixChain.Options.Any())
                    return null;

                // Calculate hedge strikes - closer to ATM for early activation
                var longStrike = Math.Ceiling(vixPrice) + (vixPrice < 18 ? 1m : 2m);
                var shortStrike = longStrike + (vixPrice < 18 ? 8m : 12m);

                var longCall = GetClosestOption(vixChain, longStrike, OptionType.Call);
                var shortCall = GetClosestOption(vixChain, shortStrike, OptionType.Call);

                if (longCall == null || shortCall == null)
                    return null;

                // Position size based on portfolio protection needs (2-3% of notch limit)
                var hedgeCost = Math.Min(notchLimit * 0.025m, 500m);
                var expectedDebit = (longCall.Mid - shortCall.Mid) * 100;
                var contracts = Math.Max(1, (int)(hedgeCost / expectedDebit));

                var longFill = await _fillEngine.SimulateFill(new OrderRequest
                {
                    Symbol = longCall.Symbol,
                    Quantity = contracts,
                    OrderType = OrderType.Market,
                    Side = OrderSide.Buy,
                    Timestamp = date
                });

                var shortFill = await _fillEngine.SimulateFill(new OrderRequest
                {
                    Symbol = shortCall.Symbol,
                    Quantity = contracts,
                    OrderType = OrderType.Market,
                    Side = OrderSide.Sell,
                    Timestamp = date
                });

                var netDebit = (longFill.FillPrice - shortFill.FillPrice) * contracts * 100;
                var totalCommissions = CalculateCommissions(2, contracts);
                var totalCost = netDebit + totalCommissions;

                return new OpenPosition
                {
                    TradeId = $"VIX_HEDGE_{date:yyyyMMdd}_{Guid.NewGuid().ToString("N")[..8]}",
                    Symbol = "VIX",
                    TradeType = "HEDGE",
                    EntryDate = date,
                    ExpiryDate = expiryDate,
                    Contracts = contracts,
                    EntryCredit = -totalCost, // Negative because it's a debit
                    MaxLoss = totalCost,
                    Legs = new List<PositionLeg>
                    {
                        new() { Symbol = longCall.Symbol, Quantity = contracts, Side = "Buy", EntryPrice = longFill.FillPrice },
                        new() { Symbol = shortCall.Symbol, Quantity = contracts, Side = "Sell", EntryPrice = shortFill.FillPrice }
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error entering VIX hedge position on {date}: {ex.Message}");
                return null;
            }
        }

        private decimal CalculateCommissions(int legs, int contracts)
        {
            var baseCommission = BASE_COMMISSION_PER_TRADE;
            var contractCommissions = legs * contracts * COMMISSION_PER_CONTRACT;
            var exchangeFees = legs * contracts * EXCHANGE_FEES_PER_CONTRACT;
            var regulatoryFees = legs * contracts * 100 * REGULATORY_FEES_RATE; // Notional value based

            return baseCommission + contractCommissions + exchangeFees + regulatoryFees;
        }

        private bool ShouldEnterCoreBWB(DateTime date, Dictionary<string, OpenPosition> openPositions, ProbeSignal probeSignal)
        {
            // Don't enter if we already have 2+ SPX positions
            var spxPositions = openPositions.Values.Count(p => p.Symbol == "SPX");
            if (spxPositions >= 2) return false;

            // Enter on Mondays or Wednesdays typically
            if (date.DayOfWeek != DayOfWeek.Monday && date.DayOfWeek != DayOfWeek.Wednesday) return false;

            // Avoid entering during high volatility unless probe signals opportunity
            if (probeSignal.IVRank > 70 && probeSignal.Sentiment != ProbeSentiment.Bullish) return false;

            return true;
        }

        private bool ShouldEnterVIXHedge(DateTime date, Dictionary<string, OpenPosition> openPositions, decimal vixPrice, ProbeSignal probeSignal)
        {
            var vixPositions = openPositions.Values.Count(p => p.Symbol == "VIX");
            if (vixPositions >= 1) return false; // Only one VIX hedge at a time

            // Enter hedge when VIX is low or market showing stress signs
            return vixPrice < 20 || probeSignal.Sentiment == ProbeSentiment.Bearish;
        }

        private async Task<SqliteConnection> CreateLedgerDatabase(string ledgerPath)
        {
            var connection = new SqliteConnection($"Data Source={ledgerPath}");
            await connection.OpenAsync();

            var createTables = @"
                CREATE TABLE IF NOT EXISTS trades (
                    trade_id TEXT PRIMARY KEY,
                    symbol TEXT NOT NULL,
                    trade_type TEXT NOT NULL,
                    entry_date TEXT NOT NULL,
                    exit_date TEXT,
                    contracts INTEGER NOT NULL,
                    entry_credit REAL NOT NULL,
                    exit_value REAL,
                    realized_pnl REAL,
                    max_loss REAL,
                    commissions REAL NOT NULL,
                    days_held INTEGER,
                    exit_reason TEXT
                );
                
                CREATE TABLE IF NOT EXISTS daily_pnl (
                    date TEXT PRIMARY KEY,
                    realized_pnl REAL NOT NULL,
                    unrealized_pnl REAL NOT NULL,
                    total_pnl REAL NOT NULL,
                    portfolio_value REAL NOT NULL,
                    open_positions INTEGER NOT NULL
                );
                
                CREATE TABLE IF NOT EXISTS position_legs (
                    trade_id TEXT NOT NULL,
                    leg_number INTEGER NOT NULL,
                    symbol TEXT NOT NULL,
                    quantity INTEGER NOT NULL,
                    side TEXT NOT NULL,
                    entry_price REAL NOT NULL,
                    exit_price REAL,
                    PRIMARY KEY (trade_id, leg_number),
                    FOREIGN KEY (trade_id) REFERENCES trades(trade_id)
                );";

            using var command = new SqliteCommand(createTables, connection);
            await command.ExecuteNonQueryAsync();

            return connection;
        }

        private async Task RecordTradeEntry(OpenPosition position, List<BacktestTrade> trades, SqliteConnection ledgerConnection)
        {
            var trade = new BacktestTrade
            {
                TradeId = position.TradeId,
                Symbol = position.Symbol,
                TradeType = position.TradeType,
                EntryDate = position.EntryDate,
                ExpiryDate = position.ExpiryDate,
                Contracts = position.Contracts,
                EntryCredit = position.EntryCredit,
                MaxLoss = position.MaxLoss,
                IsOpen = true
            };

            trades.Add(trade);

            if (ledgerConnection != null)
            {
                await InsertTradeToLedger(position, ledgerConnection);
            }
        }

        private DateTime GetNearestExpiry(DateTime date, int targetDTE)
        {
            // Simple approximation - in reality would query options chain
            var daysToAdd = targetDTE;
            var expiry = date.AddDays(daysToAdd);

            // Ensure it's a Friday (typical expiry)
            while (expiry.DayOfWeek != DayOfWeek.Friday)
            {
                expiry = expiry.AddDays(1);
            }

            return expiry;
        }
    }

    public class BacktestResults
    {
        public SPX30DTEConfig Config { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<BacktestTrade> Trades { get; set; }
        public Dictionary<DateTime, decimal> DailyPnL { get; set; }
        public BacktestRiskMetrics RiskMetrics { get; set; }
        public decimal FinalPortfolioValue { get; set; }
        public decimal CAGR { get; set; }
        public decimal SharpeRatio { get; set; }
        public decimal MaxDrawdown { get; set; }
        public decimal WinRate { get; set; }
        public decimal ProfitFactor { get; set; }
    }

    public class BacktestTrade
    {
        public string TradeId { get; set; }
        public string Symbol { get; set; }
        public string TradeType { get; set; }
        public DateTime EntryDate { get; set; }
        public DateTime? ExitDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public int Contracts { get; set; }
        public decimal EntryCredit { get; set; }
        public decimal ExitValue { get; set; }
        public decimal RealizedPnL { get; set; }
        public decimal MaxLoss { get; set; }
        public decimal Commissions { get; set; }
        public int? DaysHeld { get; set; }
        public string ExitReason { get; set; }
        public bool IsOpen { get; set; }
    }

    public class OpenPosition
    {
        public string TradeId { get; set; }
        public string Symbol { get; set; }
        public string TradeType { get; set; }
        public DateTime EntryDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public int Contracts { get; set; }
        public decimal EntryCredit { get; set; }
        public decimal MaxLoss { get; set; }
        public decimal CurrentValue { get; set; }
        public List<PositionLeg> Legs { get; set; } = new();
    }

    public class PositionLeg
    {
        public string Symbol { get; set; }
        public int Quantity { get; set; }
        public string Side { get; set; }
        public decimal EntryPrice { get; set; }
        public decimal CurrentPrice { get; set; }
    }

    public class BacktestRiskMetrics
    {
        public decimal HighWaterMark { get; set; }
        public decimal CurrentDrawdown { get; set; }
        public decimal MaxDrawdown { get; set; }
        public DateTime MaxDrawdownDate { get; set; }
        public int DrawdownDays { get; set; }
        public decimal VaR95 { get; set; }
        public decimal CVaR95 { get; set; }
    }

    // Helper methods implementation
    public partial class SPX30DTERealisticBacktester
    {
        private async Task<decimal> UpdateOpenPositions(Dictionary<string, OpenPosition> openPositions, DateTime date)
        {
            decimal totalPnL = 0m;

            foreach (var position in openPositions.Values)
            {
                var currentValue = await CalculatePositionValue(position, date);
                position.CurrentValue = currentValue;
                totalPnL += currentValue - position.EntryCredit;
            }

            return totalPnL;
        }

        private async Task<decimal> CalculatePositionValue(OpenPosition position, DateTime date)
        {
            try
            {
                var chain = await _dataManager.GetOptionsChain(position.Symbol, date);
                if (chain?.Options == null) return 0m;

                decimal totalValue = 0m;
                foreach (var leg in position.Legs)
                {
                    var option = chain.Options.FirstOrDefault(o => o.Symbol == leg.Symbol);
                    if (option != null)
                    {
                        var currentPrice = option.Mid;
                        var legValue = leg.Side == "Buy" ? currentPrice * leg.Quantity * 100 : -currentPrice * leg.Quantity * 100;
                        totalValue += legValue;
                    }
                }

                return totalValue;
            }
            catch
            {
                return 0m;
            }
        }

        private async Task CheckPositionExits(
            Dictionary<string, OpenPosition> openPositions,
            DateTime date,
            List<BacktestTrade> trades,
            SqliteConnection ledgerConnection)
        {
            var positionsToClose = new List<string>();

            foreach (var position in openPositions.Values)
            {
                var shouldExit = await ShouldExitPosition(position, date);
                if (shouldExit.exit)
                {
                    var exitValue = await ExecutePositionExit(position, date);
                    var trade = trades.First(t => t.TradeId == position.TradeId);

                    trade.ExitDate = date;
                    trade.ExitValue = exitValue;
                    trade.RealizedPnL = exitValue - position.EntryCredit;
                    trade.DaysHeld = (date - position.EntryDate).Days;
                    trade.ExitReason = shouldExit.reason;
                    trade.IsOpen = false;

                    if (ledgerConnection != null)
                    {
                        await UpdateTradeInLedger(trade, ledgerConnection);
                    }

                    positionsToClose.Add(position.TradeId);
                }
            }

            foreach (var tradeId in positionsToClose)
            {
                openPositions.Remove(tradeId);
            }
        }

        private async Task<(bool exit, string reason)> ShouldExitPosition(OpenPosition position, DateTime date)
        {
            // Exit at expiry
            if (date >= position.ExpiryDate)
                return (true, "Expiry");

            // Exit if position is very profitable (50%+ of max credit)
            var profitTarget = position.EntryCredit * 0.5m;
            if (position.CurrentValue <= profitTarget && position.EntryCredit > 0)
                return (true, "Profit Target");

            // Exit if position is losing significantly (varies by trade type)
            var lossThreshold = position.TradeType switch
            {
                "BWB" => position.MaxLoss * 0.4m, // Exit at 40% of max loss
                "HEDGE" => position.MaxLoss * 0.8m, // Hold hedges longer
                "PROBE" => position.MaxLoss * 0.3m, // Exit probes quickly
                _ => position.MaxLoss * 0.5m
            };

            var currentLoss = position.EntryCredit - position.CurrentValue;
            if (currentLoss > lossThreshold)
                return (true, "Stop Loss");

            return (false, null);
        }

        private async Task<decimal> ExecutePositionExit(OpenPosition position, DateTime date)
        {
            decimal totalExitValue = 0m;

            foreach (var leg in position.Legs)
            {
                var exitOrder = new OrderRequest
                {
                    Symbol = leg.Symbol,
                    Quantity = leg.Quantity,
                    OrderType = OrderType.Market,
                    Side = leg.Side == "Buy" ? OrderSide.Sell : OrderSide.Buy,
                    Timestamp = date
                };

                var fill = await _fillEngine.SimulateFill(exitOrder);
                totalExitValue += fill.FillPrice * leg.Quantity * 100 * (leg.Side == "Buy" ? 1 : -1);
            }

            // Subtract exit commissions
            var exitCommissions = CalculateCommissions(position.Legs.Count, position.Contracts);
            totalExitValue -= exitCommissions;

            return totalExitValue;
        }

        private decimal UpdateRiskLimit(Dictionary<DateTime, decimal> dailyPnL, decimal currentLimit)
        {
            if (dailyPnL.Count < 5) return currentLimit; // Need some history

            var recentPnL = dailyPnL.TakeLast(5).Sum(kvp => kvp.Value);

            // Move up/down RevFibNotch scale based on recent performance
            var limits = new[] { 400m, 800m, 1200m, 2000m, 3200m, 5000m, 8000m };
            var currentIndex = Array.IndexOf(limits, currentLimit);

            if (recentPnL > 1000m && currentIndex < limits.Length - 1)
                return limits[currentIndex + 1]; // Move up
            else if (recentPnL < -2000m && currentIndex > 0)
                return limits[currentIndex - 1]; // Move down

            return currentLimit;
        }

        private void UpdateDrawdownMetrics(BacktestRiskMetrics metrics, decimal portfolioValue, DateTime date)
        {
            if (portfolioValue > metrics.HighWaterMark)
            {
                metrics.HighWaterMark = portfolioValue;
                metrics.CurrentDrawdown = 0m;
            }
            else
            {
                metrics.CurrentDrawdown = (metrics.HighWaterMark - portfolioValue) / metrics.HighWaterMark;
                if (metrics.CurrentDrawdown > metrics.MaxDrawdown)
                {
                    metrics.MaxDrawdown = metrics.CurrentDrawdown;
                    metrics.MaxDrawdownDate = date;
                }
            }
        }

        private async Task CalculateFinalMetrics(BacktestResults results, decimal finalValue)
        {
            results.FinalPortfolioValue = finalValue;

            var years = (decimal)(results.EndDate - results.StartDate).TotalDays / 365.25m;
            results.CAGR = (decimal)Math.Pow((double)(finalValue / 100000m), (double)(1m / years)) - 1m;

            var winningTrades = results.Trades.Count(t => !t.IsOpen && t.RealizedPnL > 0);
            results.WinRate = results.Trades.Count(t => !t.IsOpen) > 0 ?
                (decimal)winningTrades / results.Trades.Count(t => !t.IsOpen) : 0m;

            var grossProfit = results.Trades.Where(t => !t.IsOpen && t.RealizedPnL > 0).Sum(t => t.RealizedPnL);
            var grossLoss = Math.Abs(results.Trades.Where(t => !t.IsOpen && t.RealizedPnL < 0).Sum(t => t.RealizedPnL));
            results.ProfitFactor = grossLoss > 0 ? grossProfit / grossLoss : 0m;

            // Calculate Sharpe Ratio
            if (results.DailyPnL.Any())
            {
                var dailyReturns = results.DailyPnL.Values.ToArray();
                var avgReturn = dailyReturns.Average();
                var stdDev = (decimal)Math.Sqrt(dailyReturns.Select(r => Math.Pow((double)(r - avgReturn), 2)).Average());
                results.SharpeRatio = stdDev > 0 ? avgReturn / stdDev * (decimal)Math.Sqrt(252) : 0m;
            }

            results.MaxDrawdown = results.RiskMetrics.MaxDrawdown;
        }

        private List<DateTime> GetTradingDays(DateTime startDate, DateTime endDate)
        {
            var tradingDays = new List<DateTime>();
            var current = startDate;

            while (current <= endDate)
            {
                if (current.DayOfWeek != DayOfWeek.Saturday && current.DayOfWeek != DayOfWeek.Sunday)
                {
                    tradingDays.Add(current);
                }
                current = current.AddDays(1);
            }

            return tradingDays;
        }

        private async Task<decimal> GetUnderlyingPrice(string symbol, DateTime date)
        {
            try
            {
                var chain = await _dataManager.GetOptionsChain(symbol, date);
                return chain?.UnderlyingPrice ?? 0m;
            }
            catch
            {
                return 0m;
            }
        }

        private (decimal LongStrike, decimal ShortStrike1, decimal ShortStrike2) CalculateBWBStrikes(
            decimal spxPrice, ProbeSignal probeSignal, SPX30DTEConfig config)
        {
            var putSkew = probeSignal.Sentiment == ProbeSentiment.Bearish ? 1.1m : 0.9m;

            var longStrike = Math.Round(spxPrice * 0.90m / 5m) * 5m; // 10% OTM, round to $5
            var shortStrike1 = Math.Round(spxPrice * 0.95m / 5m) * 5m; // 5% OTM
            var shortStrike2 = Math.Round(spxPrice * 0.92m / 5m) * 5m; // 8% OTM for broken wing

            return (longStrike, shortStrike1, shortStrike2);
        }

        private OptionData GetClosestOption(OptionsChain chain, decimal targetStrike, OptionType optionType)
        {
            return chain.Options?
                .Where(o => o.Type == optionType)
                .OrderBy(o => Math.Abs(o.Strike - targetStrike))
                .FirstOrDefault();
        }

        private decimal CalculateExpectedCredit(OptionData longPut, OptionData shortPut1, OptionData shortPut2)
        {
            return (-longPut.Mid + shortPut1.Mid + shortPut2.Mid) * 100;
        }

        private decimal CalculateMaxLoss((decimal LongStrike, decimal ShortStrike1, decimal ShortStrike2) strikes, int contracts)
        {
            var maxLossPerContract = Math.Max(strikes.ShortStrike1 - strikes.LongStrike, strikes.ShortStrike2 - strikes.LongStrike);
            return maxLossPerContract * contracts * 100; // Convert to dollars
        }

        private bool ShouldEnterXSPProbe(DateTime date, Dictionary<string, OpenPosition> openPositions, ProbeSignal probeSignal)
        {
            var probePositions = openPositions.Values.Count(p => p.Symbol == "XSP");
            if (probePositions >= 1) return false;

            // Enter probes when IV rank is attractive
            return probeSignal.IVRank > 30 && probeSignal.IVRank < 80;
        }

        private async Task<OpenPosition> EnterProbePosition(SPX30DTEConfig config, DateTime date, decimal notchLimit)
        {
            try
            {
                var expiryDate = GetNearestExpiry(date, 7); // Weekly probe
                var xspChain = await _dataManager.GetOptionsChain("XSP", date, expiryDate);

                if (xspChain?.Options == null || !xspChain.Options.Any())
                    return null;

                var xspPrice = xspChain.UnderlyingPrice;
                var shortStrike = Math.Round(xspPrice * 0.95m); // 5% OTM
                var longStrike = shortStrike - 5m; // $5 wide spread

                var shortPut = GetClosestOption(xspChain, shortStrike, OptionType.Put);
                var longPut = GetClosestOption(xspChain, longStrike, OptionType.Put);

                if (shortPut == null || longPut == null) return null;

                var targetCredit = Math.Min(notchLimit * 0.05m, 200m); // 5% of notch limit, max $200
                var expectedCredit = (shortPut.Mid - longPut.Mid) * 100;
                var contracts = Math.Max(1, (int)(targetCredit / expectedCredit));

                var shortFill = await _fillEngine.SimulateFill(new OrderRequest
                {
                    Symbol = shortPut.Symbol,
                    Quantity = contracts,
                    OrderType = OrderType.Market,
                    Side = OrderSide.Sell,
                    Timestamp = date
                });

                var longFill = await _fillEngine.SimulateFill(new OrderRequest
                {
                    Symbol = longPut.Symbol,
                    Quantity = contracts,
                    OrderType = OrderType.Market,
                    Side = OrderSide.Buy,
                    Timestamp = date
                });

                var netCredit = (shortFill.FillPrice - longFill.FillPrice) * contracts * 100;
                var commissions = CalculateCommissions(2, contracts);
                var netCreditAfterCosts = netCredit - commissions;

                return new OpenPosition
                {
                    TradeId = $"XSP_PROBE_{date:yyyyMMdd}_{Guid.NewGuid().ToString("N")[..8]}",
                    Symbol = "XSP",
                    TradeType = "PROBE",
                    EntryDate = date,
                    ExpiryDate = expiryDate,
                    Contracts = contracts,
                    EntryCredit = netCreditAfterCosts,
                    MaxLoss = (shortStrike - longStrike) * contracts * 100,
                    Legs = new List<PositionLeg>
                    {
                        new() { Symbol = shortPut.Symbol, Quantity = contracts, Side = "Sell", EntryPrice = shortFill.FillPrice },
                        new() { Symbol = longPut.Symbol, Quantity = contracts, Side = "Buy", EntryPrice = longFill.FillPrice }
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error entering XSP probe position on {date}: {ex.Message}");
                return null;
            }
        }

        private async Task InsertTradeToLedger(OpenPosition position, SqliteConnection connection)
        {
            var insertTrade = @"
                INSERT INTO trades (trade_id, symbol, trade_type, entry_date, contracts, entry_credit, max_loss, commissions)
                VALUES (@tradeId, @symbol, @tradeType, @entryDate, @contracts, @entryCredit, @maxLoss, @commissions)";

            using var command = new SqliteCommand(insertTrade, connection);
            command.Parameters.AddWithValue("@tradeId", position.TradeId);
            command.Parameters.AddWithValue("@symbol", position.Symbol);
            command.Parameters.AddWithValue("@tradeType", position.TradeType);
            command.Parameters.AddWithValue("@entryDate", position.EntryDate.ToString("yyyy-MM-dd"));
            command.Parameters.AddWithValue("@contracts", position.Contracts);
            command.Parameters.AddWithValue("@entryCredit", position.EntryCredit);
            command.Parameters.AddWithValue("@maxLoss", position.MaxLoss);
            command.Parameters.AddWithValue("@commissions", CalculateCommissions(position.Legs.Count, position.Contracts));

            await command.ExecuteNonQueryAsync();

            // Insert position legs
            for (int i = 0; i < position.Legs.Count; i++)
            {
                var leg = position.Legs[i];
                var insertLeg = @"
                    INSERT INTO position_legs (trade_id, leg_number, symbol, quantity, side, entry_price)
                    VALUES (@tradeId, @legNumber, @symbol, @quantity, @side, @entryPrice)";

                using var legCommand = new SqliteCommand(insertLeg, connection);
                legCommand.Parameters.AddWithValue("@tradeId", position.TradeId);
                legCommand.Parameters.AddWithValue("@legNumber", i);
                legCommand.Parameters.AddWithValue("@symbol", leg.Symbol);
                legCommand.Parameters.AddWithValue("@quantity", leg.Quantity);
                legCommand.Parameters.AddWithValue("@side", leg.Side);
                legCommand.Parameters.AddWithValue("@entryPrice", leg.EntryPrice);

                await legCommand.ExecuteNonQueryAsync();
            }
        }

        private async Task UpdateTradeInLedger(BacktestTrade trade, SqliteConnection connection)
        {
            var updateTrade = @"
                UPDATE trades 
                SET exit_date = @exitDate, exit_value = @exitValue, realized_pnl = @realizedPnl, 
                    days_held = @daysHeld, exit_reason = @exitReason
                WHERE trade_id = @tradeId";

            using var command = new SqliteCommand(updateTrade, connection);
            command.Parameters.AddWithValue("@tradeId", trade.TradeId);
            command.Parameters.AddWithValue("@exitDate", trade.ExitDate?.ToString("yyyy-MM-dd"));
            command.Parameters.AddWithValue("@exitValue", trade.ExitValue);
            command.Parameters.AddWithValue("@realizedPnL", trade.RealizedPnL);
            command.Parameters.AddWithValue("@daysHeld", trade.DaysHeld);
            command.Parameters.AddWithValue("@exitReason", trade.ExitReason);

            await command.ExecuteNonQueryAsync();
        }
    }
}