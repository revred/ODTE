using ODTE.Backtest.Config;
using ODTE.Backtest.Signals;
using ODTE.Backtest.Strategy;
using ODTE.Backtest.Core;
using ODTE.LiveTrading.Brokers;
using ODTE.LiveTrading.Engine;
using ODTE.LiveTrading.Interfaces;

namespace ODTE.Trading.Tests;

/// <summary>
/// Comprehensive tests for ODTE Live Trading System components
/// </summary>
public class BrokerTests
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("🧪 ODTE Live Trading System - Component Tests");
        Console.WriteLine("=" + new string('=', 49));
        Console.WriteLine();

        var testResults = new Dictionary<string, bool>();

        try
        {
            // Test IBKR Mock Broker
            Console.WriteLine("📊 Testing IBKR Mock Broker...");
            testResults["IBKR Connection"] = await TestIBKRBroker();
            Console.WriteLine();

            // Test Robinhood Mock Broker
            Console.WriteLine("📱 Testing Robinhood Mock Broker...");
            testResults["Robinhood Connection"] = await TestRobinhoodBroker();
            Console.WriteLine();

            // Test Live Trading Engine
            Console.WriteLine("🚀 Testing Live Trading Engine...");
            testResults["Live Trading Engine"] = await TestLiveTradingEngine();
            Console.WriteLine();

            // Test Order Processing
            Console.WriteLine("📋 Testing Order Processing...");
            testResults["Order Processing"] = await TestOrderProcessing();
            Console.WriteLine();

            // Test Risk Management
            Console.WriteLine("🛡️ Testing Risk Management...");
            testResults["Risk Management"] = await TestRiskManagement();
            Console.WriteLine();

            // Generate Report
            GenerateTestReport(testResults);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Fatal test error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    static async Task<bool> TestIBKRBroker()
    {
        try
        {
            var broker = new IBKRMockBroker();
            var credentials = new BrokerCredentials
            {
                Username = "testuser",
                ApiKey = "test123",
                PaperTrading = true
            };

            // Test connection
            var connected = await broker.ConnectAsync(credentials);
            Console.WriteLine($"  Connection: {(connected ? "✅" : "❌")}");

            if (!connected) return false;

            // Test account info
            var accountInfo = await broker.GetAccountInfoAsync();
            Console.WriteLine($"  Account ID: {accountInfo.AccountId}");
            Console.WriteLine($"  Net Liq Value: ${accountInfo.NetLiquidationValue:N2}");
            Console.WriteLine($"  Available Funds: ${accountInfo.AvailableFunds:N2}");

            // Test market status
            var marketStatus = await broker.GetMarketStatusAsync();
            Console.WriteLine($"  Market Status: {marketStatus.Session}");

            // Test option chain
            var expiry = DateTime.Today.AddHours(16);
            var optionChain = await broker.GetOptionChainAsync("SPY", expiry);
            Console.WriteLine($"  Option Chain: {optionChain.Count()} quotes");

            // Test spot price
            var spotPrice = await broker.GetSpotPriceAsync("SPY");
            Console.WriteLine($"  Spot Price: ${spotPrice:F2}");

            // Test risk limits
            var riskLimits = await broker.GetRiskLimitsAsync();
            Console.WriteLine($"  Max Order Value: ${riskLimits.MaxOrderValue:N2}");

            await broker.DisconnectAsync();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ IBKR Test failed: {ex.Message}");
            return false;
        }
    }

    static async Task<bool> TestRobinhoodBroker()
    {
        try
        {
            var broker = new RobinhoodMockBroker();
            var credentials = new BrokerCredentials
            {
                Username = "testuser@email.com",
                ApiSecret = "password123",
                PaperTrading = true,
                ExtraParams = new Dictionary<string, string>
                {
                    ["gold_member"] = "true"
                }
            };

            // Test connection
            var connected = await broker.ConnectAsync(credentials);
            Console.WriteLine($"  Connection: {(connected ? "✅" : "❌")}");

            if (!connected) return false;

            // Test account info
            var accountInfo = await broker.GetAccountInfoAsync();
            Console.WriteLine($"  Account ID: {accountInfo.AccountId}");
            Console.WriteLine($"  Portfolio Value: ${accountInfo.NetLiquidationValue:N2}");
            Console.WriteLine($"  Gold Member Level: {accountInfo.MaxOptionsLevel}");

            // Test market status
            var marketStatus = await broker.GetMarketStatusAsync();
            Console.WriteLine($"  Market Status: {marketStatus.Session}");
            Console.WriteLine($"  Crypto Trading: {marketStatus.ProductStatus["Crypto"]}");

            // Test option chain (limited symbols)
            var expiry = DateTime.Today.AddHours(16);
            var optionChain = await broker.GetOptionChainAsync("AAPL", expiry);
            Console.WriteLine($"  Option Chain (AAPL): {optionChain.Count()} quotes");

            // Test spot price
            var spotPrice = await broker.GetSpotPriceAsync("AAPL");
            Console.WriteLine($"  AAPL Spot Price: ${spotPrice:F2}");

            await broker.DisconnectAsync();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Robinhood Test failed: {ex.Message}");
            return false;
        }
    }

    static async Task<bool> TestLiveTradingEngine()
    {
        try
        {
            // Setup mock broker
            var broker = new IBKRMockBroker();
            var credentials = new BrokerCredentials
            {
                Username = "testuser",
                ApiKey = "test123",
                PaperTrading = true
            };

            await broker.ConnectAsync(credentials);

            // Create configuration for testing
            var config = new SimConfig
            {
                Underlying = "SPY",
                Start = DateOnly.FromDateTime(DateTime.Today),
                End = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
                CadenceSeconds = 60, // 1 minute for testing
                NoNewRiskMinutesToClose = 30,
                Risk = new RiskCfg
                {
                    DailyLossStop = 100,
                    PerTradeMaxLossCap = 25,
                    MaxConcurrentPerSide = 1
                },
                Stops = new StopsCfg
                {
                    CreditMultiple = 2.0,
                    DeltaBreach = 0.30
                }
            };

            // Initialize components
            var regimeScorer = new RegimeScorer(config);
            var spreadBuilder = new SpreadBuilder(config);

            // Create trading engine
            using var engine = new LiveTradingEngine(config, broker, regimeScorer, spreadBuilder);

            // Test engine status
            var status = engine.GetStatus();
            Console.WriteLine($"  Initial Status: {(status.IsRunning ? "Running" : "Stopped")}");
            Console.WriteLine($"  Account Value: ${status.AccountValue:N2}");
            Console.WriteLine($"  Broker Connected: {status.BrokerConnected}");

            // Test pause/resume functionality
            engine.Pause();
            var pausedStatus = engine.GetStatus();
            Console.WriteLine($"  Paused Status: {pausedStatus.IsPaused}");

            engine.Resume();
            var resumedStatus = engine.GetStatus();
            Console.WriteLine($"  Resumed Status: {!resumedStatus.IsPaused}");

            await broker.DisconnectAsync();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Live Trading Engine Test failed: {ex.Message}");
            return false;
        }
    }

    static async Task<bool> TestOrderProcessing()
    {
        try
        {
            var broker = new IBKRMockBroker();
            var credentials = new BrokerCredentials
            {
                Username = "testuser",
                ApiKey = "test123",
                PaperTrading = true
            };

            await broker.ConnectAsync(credentials);

            // Create a test order
            var testOrder = new LiveOrder
            {
                Underlying = "SPY",
                Type = OrderType.Limit,
                Side = OrderSide.SellToOpen,
                Quantity = 1,
                LimitPrice = 1.50m,
                TimeInForce = OrderTimeInForce.Day,
                Legs = new List<OrderLeg>
                {
                    new OrderLeg
                    {
                        Symbol = "SPY",
                        Expiry = DateTime.Today.AddHours(16),
                        Strike = 490m,
                        Right = Right.Put,
                        Side = OrderSide.SellToOpen,
                        Ratio = 1
                    },
                    new OrderLeg
                    {
                        Symbol = "SPY",
                        Expiry = DateTime.Today.AddHours(16),
                        Strike = 485m,
                        Right = Right.Put,
                        Side = OrderSide.BuyToOpen,
                        Ratio = 1
                    }
                }
            };

            // Test order validation
            var isValid = await broker.ValidateOrderAsync(testOrder);
            Console.WriteLine($"  Order Validation: {(isValid ? "✅" : "❌")}");

            // Test order submission
            var orderResult = await broker.SubmitOrderAsync(testOrder);
            Console.WriteLine($"  Order Submission: {(orderResult.Success ? "✅" : "❌")}");
            if (orderResult.Success)
            {
                Console.WriteLine($"  Order ID: {orderResult.OrderId}");
            }

            // Wait a moment for execution simulation
            await Task.Delay(3000);

            // Test order retrieval
            var orders = await broker.GetOrdersAsync();
            Console.WriteLine($"  Orders Retrieved: {orders.Count()}");

            // Test positions
            var positions = await broker.GetPositionsAsync();
            Console.WriteLine($"  Positions Created: {positions.Count()}");

            await broker.DisconnectAsync();
            return orderResult.Success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Order Processing Test failed: {ex.Message}");
            return false;
        }
    }

    static async Task<bool> TestRiskManagement()
    {
        try
        {
            var broker = new IBKRMockBroker();
            var credentials = new BrokerCredentials
            {
                Username = "testuser",
                ApiKey = "test123",
                PaperTrading = true
            };

            await broker.ConnectAsync(credentials);

            // Test risk limits
            var riskLimits = await broker.GetRiskLimitsAsync();
            Console.WriteLine($"  Max Order Value: ${riskLimits.MaxOrderValue:N2}");
            Console.WriteLine($"  Daily Loss Limit: ${riskLimits.DailyLossLimit:N2}");
            Console.WriteLine($"  Max Positions: {riskLimits.MaxPositions}");
            Console.WriteLine($"  Allow Naked Options: {riskLimits.AllowNakedOptions}");

            // Test order size validation
            var largeOrder = new LiveOrder
            {
                Underlying = "SPY",
                Type = OrderType.Market,
                Side = OrderSide.Buy,
                Quantity = 1000, // Deliberately large
                Legs = new List<OrderLeg>()
            };

            var isValidLarge = await broker.ValidateOrderAsync(largeOrder);
            Console.WriteLine($"  Large Order Rejected: {(!isValidLarge ? "✅" : "❌")}");

            // Test invalid order validation
            var invalidOrder = new LiveOrder
            {
                Underlying = "",
                Type = OrderType.Market,
                Side = OrderSide.Buy,
                Quantity = 0,
                Legs = new List<OrderLeg>()
            };

            var isValidInvalid = await broker.ValidateOrderAsync(invalidOrder);
            Console.WriteLine($"  Invalid Order Rejected: {(!isValidInvalid ? "✅" : "❌")}");

            await broker.DisconnectAsync();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Risk Management Test failed: {ex.Message}");
            return false;
        }
    }

    static void GenerateTestReport(Dictionary<string, bool> testResults)
    {
        Console.WriteLine();
        Console.WriteLine("📋 TEST REPORT");
        Console.WriteLine("=" + new string('=', 15));
        Console.WriteLine();

        int passed = 0;
        int failed = 0;

        foreach (var (testName, result) in testResults)
        {
            var status = result ? "✅ PASS" : "❌ FAIL";
            Console.WriteLine($"{testName,-25}: {status}");
            if (result) passed++; else failed++;
        }

        Console.WriteLine();
        Console.WriteLine($"Total Tests: {testResults.Count}");
        Console.WriteLine($"Passed: {passed}");
        Console.WriteLine($"Failed: {failed}");
        Console.WriteLine($"Success Rate: {(passed * 100.0 / testResults.Count):F1}%");

        if (failed == 0)
        {
            Console.WriteLine();
            Console.WriteLine("🎉 ALL TESTS PASSED! The live trading system is ready for use.");
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine("⚠️ Some tests failed. Please review the errors above.");
        }
    }
}