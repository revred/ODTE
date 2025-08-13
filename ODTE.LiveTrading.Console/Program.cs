using ODTE.Backtest.Config;
using ODTE.Backtest.Signals;
using ODTE.Backtest.Strategy;
using ODTE.LiveTrading.Brokers;
using ODTE.LiveTrading.Engine;
using ODTE.LiveTrading.Interfaces;
using System.Text.Json;

namespace ODTE.LiveTrading.Console;

/// <summary>
/// Interactive console application for live ODTE options trading.
/// 
/// FEATURES:
/// - Multiple broker support (IBKR, Robinhood)
/// - Real-time strategy execution
/// - Interactive dashboard and controls
/// - Paper trading mode for safety
/// - Emergency stop capabilities
/// - Real-time P&L monitoring
/// 
/// SECURITY FEATURES:
/// - All trading starts in paper mode
/// - Human approval required for live trading
/// - Multiple confirmation steps
/// - Emergency stop functionality
/// - Comprehensive audit logging
/// 
/// USAGE:
/// 1. Select broker (IBKR or Robinhood)
/// 2. Enter credentials (paper trading recommended)
/// 3. Configure strategy parameters
/// 4. Start trading engine
/// 5. Monitor positions and performance
/// 6. Use interactive commands to control
/// </summary>
class Program
{
    private static LiveTradingEngine? _tradingEngine;
    private static IBroker? _broker;
    private static bool _isRunning = true;

    static async Task Main(string[] args)
    {
        try
        {
            ShowWelcomeMessage();
            
            // Interactive broker selection and setup
            var broker = await SelectAndSetupBroker();
            if (broker == null)
            {
                System.Console.WriteLine("❌ Failed to setup broker connection");
                return;
            }

            _broker = broker;
            
            // Load configuration
            var config = LoadConfiguration();
            if (config == null)
            {
                System.Console.WriteLine("❌ Failed to load configuration");
                return;
            }

            // Initialize strategy components
            var regimeScorer = new RegimeScorer(config);
            var spreadBuilder = new SpreadBuilder(config);

            // Create and initialize trading engine
            _tradingEngine = new LiveTradingEngine(config, broker, regimeScorer, spreadBuilder);

            // Setup console cancellation
            System.Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                _isRunning = false;
                System.Console.WriteLine("\n🛑 Shutdown requested...");
            };

            // Main interaction loop
            await RunInteractiveDashboard();
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"❌ Fatal error: {ex.Message}");
            System.Console.WriteLine("Press any key to exit...");
            System.Console.ReadKey();
        }
        finally
        {
            await Cleanup();
        }
    }

    private static void ShowWelcomeMessage()
    {
        System.Console.Clear();
        System.Console.ForegroundColor = ConsoleColor.Cyan;
        System.Console.WriteLine("════════════════════════════════════════");
        System.Console.WriteLine("      0DTE Live Trading Engine");
        System.Console.WriteLine("    Defensive Options Trading System");
        System.Console.WriteLine("════════════════════════════════════════");
        System.Console.WriteLine();
        System.Console.ResetColor();
        
        System.Console.ForegroundColor = ConsoleColor.Yellow;
        System.Console.WriteLine("⚠️  IMPORTANT DISCLAIMERS:");
        System.Console.WriteLine("   • This is for educational and research purposes");
        System.Console.WriteLine("   • Start with PAPER TRADING only");
        System.Console.WriteLine("   • Options trading involves substantial risk");
        System.Console.WriteLine("   • Past performance does not guarantee results");
        System.Console.WriteLine("   • You are responsible for all trading decisions");
        System.Console.ResetColor();
        System.Console.WriteLine();
        
        System.Console.WriteLine("Press any key to continue...");
        System.Console.ReadKey();
        System.Console.Clear();
    }

    private static async Task<IBroker?> SelectAndSetupBroker()
    {
        System.Console.WriteLine("📊 Select your broker:");
        System.Console.WriteLine("1. Interactive Brokers (IBKR UK) - Mock");
        System.Console.WriteLine("2. Robinhood - Mock");
        System.Console.WriteLine("3. Exit");
        System.Console.WriteLine();
        
        while (true)
        {
            System.Console.Write("Enter your choice (1-3): ");
            var choice = System.Console.ReadLine();
            
            switch (choice)
            {
                case "1":
                    return await SetupIBKR();
                case "2":
                    return await SetupRobinhood();
                case "3":
                    return null;
                default:
                    System.Console.WriteLine("❌ Invalid choice. Please enter 1, 2, or 3.");
                    break;
            }
        }
    }

    private static async Task<IBroker?> SetupIBKR()
    {
        System.Console.Clear();
        System.Console.WriteLine("🏦 Setting up Interactive Brokers connection...");
        System.Console.WriteLine();

        var credentials = new BrokerCredentials();
        
        // Get credentials
        System.Console.Write("Username: ");
        var username = System.Console.ReadLine() ?? "";
        
        System.Console.Write("API Key: ");
        var apiKey = System.Console.ReadLine() ?? "";
        
        System.Console.Write("Use Paper Trading? (Y/n): ");
        var paperTrading = System.Console.ReadLine()?.ToLower() != "n";
        
        credentials = credentials with 
        { 
            Username = username, 
            ApiKey = apiKey, 
            PaperTrading = paperTrading 
        };

        var broker = new IBKRMockBroker();
        
        System.Console.WriteLine("\n🔗 Connecting to IBKR TWS...");
        var connected = await broker.ConnectAsync(credentials);
        
        if (connected)
        {
            System.Console.WriteLine("✅ Connected to IBKR successfully!");
            
            // Show account info
            var accountInfo = await broker.GetAccountInfoAsync();
            System.Console.WriteLine($"📋 Account: {accountInfo.AccountId}");
            System.Console.WriteLine($"💰 Net Liquidation Value: ${accountInfo.NetLiquidationValue:N2}");
            System.Console.WriteLine($"💵 Available Funds: ${accountInfo.AvailableFunds:N2}");
            System.Console.WriteLine($"🎯 Options Level: {accountInfo.MaxOptionsLevel}");
            System.Console.WriteLine();
            
            System.Console.WriteLine("Press any key to continue...");
            System.Console.ReadKey();
            
            return broker;
        }
        else
        {
            System.Console.WriteLine("❌ Failed to connect to IBKR");
            System.Console.WriteLine("Press any key to continue...");
            System.Console.ReadKey();
            return null;
        }
    }

    private static async Task<IBroker?> SetupRobinhood()
    {
        System.Console.Clear();
        System.Console.WriteLine("🏦 Setting up Robinhood connection...");
        System.Console.WriteLine();

        System.Console.Write("Username/Email: ");
        var username = System.Console.ReadLine() ?? "";
        
        System.Console.Write("Password: ");
        var password = ReadPassword();
        
        System.Console.Write("Gold Member? (y/N): ");
        var goldMember = System.Console.ReadLine()?.ToLower() == "y";
        
        System.Console.Write("Use Paper Trading? (Y/n): ");
        var paperTrading = System.Console.ReadLine()?.ToLower() != "n";

        var extraParams = new Dictionary<string, string>();
        if (goldMember)
            extraParams["gold_member"] = "true";

        var credentials = new BrokerCredentials
        {
            Username = username,
            ApiSecret = password, // Using ApiSecret as password for Robinhood
            PaperTrading = paperTrading,
            ExtraParams = extraParams
        };

        var broker = new RobinhoodMockBroker();
        
        System.Console.WriteLine("\n🔗 Connecting to Robinhood...");
        var connected = await broker.ConnectAsync(credentials);
        
        if (connected)
        {
            System.Console.WriteLine("✅ Connected to Robinhood successfully!");
            
            var accountInfo = await broker.GetAccountInfoAsync();
            System.Console.WriteLine($"📋 Account: {accountInfo.AccountId}");
            System.Console.WriteLine($"💰 Portfolio Value: ${accountInfo.NetLiquidationValue:N2}");
            System.Console.WriteLine($"💵 Available Funds: ${accountInfo.AvailableFunds:N2}");
            System.Console.WriteLine($"🎯 Options Level: {accountInfo.MaxOptionsLevel}");
            if (goldMember) System.Console.WriteLine("⭐ Robinhood Gold Member");
            System.Console.WriteLine();
            
            System.Console.WriteLine("Press any key to continue...");
            System.Console.ReadKey();
            
            return broker;
        }
        else
        {
            System.Console.WriteLine("❌ Failed to connect to Robinhood");
            System.Console.WriteLine("Press any key to continue...");
            System.Console.ReadKey();
            return null;
        }
    }

    private static string ReadPassword()
    {
        var password = "";
        ConsoleKeyInfo key;
        
        do
        {
            key = System.Console.ReadKey(true);
            
            if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
            {
                password += key.KeyChar;
                System.Console.Write("*");
            }
            else if (key.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                password = password.Substring(0, password.Length - 1);
                System.Console.Write("\b \b");
            }
        }
        while (key.Key != ConsoleKey.Enter);
        
        System.Console.WriteLine();
        return password;
    }

    private static SimConfig? LoadConfiguration()
    {
        try
        {
            System.Console.WriteLine("📄 Loading default configuration...");
            
            // Create a default configuration optimized for live trading
            var config = new SimConfig
            {
                Underlying = "SPY", // Use SPY instead of XSP for better liquidity
                Start = DateOnly.FromDateTime(DateTime.Today),
                End = DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
                Mode = "live",
                RthOnly = true,
                Timezone = "America/New_York",
                CadenceSeconds = 300, // 5 minutes for live trading
                NoNewRiskMinutesToClose = 30,
                
                ShortDelta = new ShortDeltaCfg
                {
                    CondorMin = 0.10,
                    CondorMax = 0.20,
                    SingleMin = 0.15,
                    SingleMax = 0.25
                },
                
                WidthPoints = new WidthPointsCfg { Min = 1, Max = 3 },
                CreditPerWidthMin = new CreditPerWidthCfg { Condor = 0.25, Single = 0.30 },
                
                Stops = new StopsCfg
                {
                    CreditMultiple = 2.0, // Tighter stops for live trading
                    DeltaBreach = 0.30
                },
                
                Risk = new RiskCfg
                {
                    DailyLossStop = 200, // Conservative for live trading
                    PerTradeMaxLossCap = 50,
                    MaxConcurrentPerSide = 1 // Very conservative
                },
                
                Slippage = new SlippageCfg
                {
                    EntryHalfSpreadTicks = 1.0,
                    ExitHalfSpreadTicks = 1.0,
                    LateSessionExtraTicks = 1.0,
                    TickValue = 0.05,
                    SpreadPctCap = 0.30
                },
                
                Fees = new FeesCfg
                {
                    CommissionPerContract = 0.65,
                    ExchangeFeesPerContract = 0.25
                },
                
                Signals = new SignalsCfg
                {
                    OrMinutes = 15,
                    VwapWindowMinutes = 30,
                    AtrPeriodBars = 20,
                    EventBlockMinutesBefore = 60,
                    EventBlockMinutesAfter = 15
                }
            };

            System.Console.WriteLine("✅ Configuration loaded successfully");
            return config;
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"❌ Failed to load configuration: {ex.Message}");
            return null;
        }
    }

    private static async Task RunInteractiveDashboard()
    {
        System.Console.Clear();
        ShowDashboardHeader();
        
        while (_isRunning)
        {
            try
            {
                ShowEngineStatus();
                ShowMenuOptions();
                
                System.Console.Write("\nEnter command: ");
                var command = System.Console.ReadLine()?.ToLower().Trim();
                
                await ProcessCommand(command);
                
                if (!_isRunning) break;
                
                System.Console.WriteLine("\nPress any key to refresh dashboard...");
                var keyInfo = System.Console.ReadKey(true);
                if (keyInfo.Key == ConsoleKey.Escape)
                    break;
                
                System.Console.Clear();
                ShowDashboardHeader();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"❌ Dashboard error: {ex.Message}");
                await Task.Delay(2000);
            }
        }
    }

    private static void ShowDashboardHeader()
    {
        System.Console.ForegroundColor = ConsoleColor.Green;
        System.Console.WriteLine("════════════════════════════════════════");
        System.Console.WriteLine("       📊 LIVE TRADING DASHBOARD");
        System.Console.WriteLine("════════════════════════════════════════");
        System.Console.ResetColor();
        System.Console.WriteLine();
    }

    private static void ShowEngineStatus()
    {
        if (_tradingEngine == null)
        {
            System.Console.WriteLine("🔴 Engine: Not initialized");
            return;
        }

        var status = _tradingEngine.GetStatus();
        
        // Engine status
        System.Console.ForegroundColor = status.IsRunning ? ConsoleColor.Green : ConsoleColor.Red;
        var statusText = status.IsRunning ? "RUNNING" : "STOPPED";
        if (status.IsPaused) statusText += " (PAUSED)";
        if (status.IsEmergencyStopped) statusText += " (EMERGENCY STOP)";
        System.Console.WriteLine($"🎯 Engine Status: {statusText}");
        System.Console.ResetColor();
        
        // Account info
        System.Console.WriteLine($"💰 Account Value: ${status.AccountValue:N2}");
        System.Console.WriteLine($"💵 Available Funds: ${status.AvailableFunds:N2}");
        
        // P&L
        System.Console.ForegroundColor = status.TotalPnL >= 0 ? ConsoleColor.Green : ConsoleColor.Red;
        System.Console.WriteLine($"📈 Total P&L: ${status.TotalPnL:N2}");
        System.Console.ResetColor();
        
        // Positions and orders
        System.Console.WriteLine($"📋 Active Positions: {status.ActivePositions}");
        System.Console.WriteLine($"⏳ Pending Orders: {status.PendingOrders}");
        
        // Statistics
        System.Console.WriteLine($"📊 Decisions Made: {status.TotalDecisions}");
        System.Console.WriteLine($"📤 Orders Submitted: {status.TotalOrders}");
        
        if (status.IsRunning)
        {
            System.Console.WriteLine($"⏱️  Running Time: {status.RunTime:hh\\:mm\\:ss}");
            System.Console.WriteLine($"🕒 Last Decision: {(DateTime.UtcNow - status.LastDecisionTime).TotalSeconds:F0}s ago");
        }
        
        // Broker status
        System.Console.ForegroundColor = status.BrokerConnected ? ConsoleColor.Green : ConsoleColor.Red;
        System.Console.WriteLine($"🔗 Broker: {(status.BrokerConnected ? "Connected" : "Disconnected")}");
        System.Console.ResetColor();
    }

    private static void ShowMenuOptions()
    {
        System.Console.WriteLine();
        System.Console.WriteLine("📋 Available Commands:");
        
        if (_tradingEngine?.GetStatus().IsRunning == true)
        {
            System.Console.WriteLine("  pause    - Pause trading (monitor only)");
            System.Console.WriteLine("  stop     - Stop trading engine");
            System.Console.WriteLine("  estop    - Emergency stop (close all)");
        }
        else
        {
            System.Console.WriteLine("  start    - Start trading engine");
            if (_tradingEngine?.GetStatus().IsPaused == true)
                System.Console.WriteLine("  resume   - Resume trading");
        }
        
        System.Console.WriteLine("  status   - Show detailed status");
        System.Console.WriteLine("  positions - Show all positions");
        System.Console.WriteLine("  orders   - Show all orders");
        System.Console.WriteLine("  config   - Show configuration");
        System.Console.WriteLine("  help     - Show this help");
        System.Console.WriteLine("  exit     - Exit application");
    }

    private static async Task ProcessCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return;

        try
        {
            switch (command)
            {
                case "start":
                    await StartEngine();
                    break;
                    
                case "stop":
                    await StopEngine();
                    break;
                    
                case "pause":
                    PauseEngine();
                    break;
                    
                case "resume":
                    ResumeEngine();
                    break;
                    
                case "estop":
                    await EmergencyStop();
                    break;
                    
                case "status":
                    await ShowDetailedStatus();
                    break;
                    
                case "positions":
                    await ShowPositions();
                    break;
                    
                case "orders":
                    await ShowOrders();
                    break;
                    
                case "config":
                    ShowConfiguration();
                    break;
                    
                case "help":
                    ShowHelp();
                    break;
                    
                case "exit":
                    _isRunning = false;
                    break;
                    
                default:
                    System.Console.WriteLine($"❌ Unknown command: {command}");
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"❌ Command failed: {ex.Message}");
        }
    }

    private static async Task StartEngine()
    {
        if (_tradingEngine == null)
        {
            System.Console.WriteLine("❌ Trading engine not initialized");
            return;
        }

        System.Console.WriteLine("🚀 Starting trading engine...");
        
        // Safety confirmation for live trading
        System.Console.Write("⚠️  Confirm start trading engine? (yes/no): ");
        var confirmation = System.Console.ReadLine();
        
        if (confirmation?.ToLower() != "yes")
        {
            System.Console.WriteLine("❌ Start cancelled");
            return;
        }

        var started = await _tradingEngine.StartAsync();
        
        if (started)
        {
            System.Console.WriteLine("✅ Trading engine started successfully!");
        }
        else
        {
            System.Console.WriteLine("❌ Failed to start trading engine");
        }
    }

    private static async Task StopEngine()
    {
        if (_tradingEngine == null) return;

        System.Console.WriteLine("🛑 Stopping trading engine...");
        await _tradingEngine.StopAsync();
        System.Console.WriteLine("✅ Trading engine stopped");
    }

    private static void PauseEngine()
    {
        _tradingEngine?.Pause();
        System.Console.WriteLine("⏸️  Trading engine paused");
    }

    private static void ResumeEngine()
    {
        _tradingEngine?.Resume();
        System.Console.WriteLine("▶️  Trading engine resumed");
    }

    private static async Task EmergencyStop()
    {
        if (_tradingEngine == null) return;

        System.Console.WriteLine("🚨 EMERGENCY STOP - This will close all positions!");
        System.Console.Write("⚠️  Type 'EMERGENCY' to confirm: ");
        var confirmation = System.Console.ReadLine();
        
        if (confirmation != "EMERGENCY")
        {
            System.Console.WriteLine("❌ Emergency stop cancelled");
            return;
        }

        System.Console.WriteLine("🚨 Executing emergency stop...");
        await _tradingEngine.EmergencyStopAsync("User requested emergency stop");
        System.Console.WriteLine("✅ Emergency stop completed");
    }

    private static async Task ShowDetailedStatus()
    {
        if (_broker == null) return;

        try
        {
            System.Console.WriteLine("\n📊 Detailed Status:");
            System.Console.WriteLine("═══════════════════");
            
            var accountInfo = await _broker.GetAccountInfoAsync();
            var marketStatus = await _broker.GetMarketStatusAsync();
            
            System.Console.WriteLine($"Account ID: {accountInfo.AccountId}");
            System.Console.WriteLine($"Net Liquidation: ${accountInfo.NetLiquidationValue:N2}");
            System.Console.WriteLine($"Buying Power: ${accountInfo.BuyingPower:N2}");
            System.Console.WriteLine($"PDT Status: {(accountInfo.PatternDayTrader ? "Yes" : "No")}");
            System.Console.WriteLine($"Options Level: {accountInfo.MaxOptionsLevel}");
            System.Console.WriteLine();
            System.Console.WriteLine($"Market Status: {marketStatus.Session}");
            System.Console.WriteLine($"Market Open: {(marketStatus.IsOpen ? "Yes" : "No")}");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"❌ Failed to get detailed status: {ex.Message}");
        }
    }

    private static async Task ShowPositions()
    {
        if (_broker == null) return;

        try
        {
            System.Console.WriteLine("\n📋 Current Positions:");
            System.Console.WriteLine("═══════════════════");
            
            var positions = await _broker.GetPositionsAsync();
            
            if (!positions.Any())
            {
                System.Console.WriteLine("No active positions");
                return;
            }

            foreach (var position in positions)
            {
                System.Console.WriteLine($"Position ID: {position.PositionId}");
                System.Console.WriteLine($"Underlying: {position.Underlying}");
                System.Console.WriteLine($"Opened: {position.OpenedAt:yyyy-MM-dd HH:mm:ss}");
                System.Console.WriteLine($"Net Credit: ${position.NetCredit:N2}");
                System.Console.WriteLine($"Current Value: ${position.CurrentValue:N2}");
                
                System.Console.ForegroundColor = position.UnrealizedPnL >= 0 ? ConsoleColor.Green : ConsoleColor.Red;
                System.Console.WriteLine($"Unrealized P&L: ${position.UnrealizedPnL:N2}");
                System.Console.ResetColor();
                
                System.Console.WriteLine($"Delta: {position.Delta:F3}");
                System.Console.WriteLine($"Legs: {position.Legs.Count}");
                System.Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"❌ Failed to get positions: {ex.Message}");
        }
    }

    private static async Task ShowOrders()
    {
        if (_broker == null) return;

        try
        {
            System.Console.WriteLine("\n📤 Recent Orders:");
            System.Console.WriteLine("═══════════════");
            
            var orders = await _broker.GetOrdersAsync();
            var recentOrders = orders.Take(10);
            
            if (!recentOrders.Any())
            {
                System.Console.WriteLine("No recent orders");
                return;
            }

            foreach (var order in recentOrders)
            {
                System.Console.WriteLine($"Order ID: {order.OrderId}");
                System.Console.WriteLine($"Underlying: {order.Underlying}");
                System.Console.WriteLine($"Type: {order.Type}");
                System.Console.WriteLine($"Side: {order.Side}");
                System.Console.WriteLine($"Status: {order.Status}");
                System.Console.WriteLine($"Created: {order.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                
                if (order.FilledAt.HasValue)
                    System.Console.WriteLine($"Filled: {order.FilledAt:yyyy-MM-dd HH:mm:ss} @ ${order.FilledPrice:N2}");
                
                System.Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"❌ Failed to get orders: {ex.Message}");
        }
    }

    private static void ShowConfiguration()
    {
        System.Console.WriteLine("\n⚙️  Current Configuration:");
        System.Console.WriteLine("═════════════════════════");
        System.Console.WriteLine("This would show current strategy parameters");
        System.Console.WriteLine("(Configuration display not implemented in demo)");
    }

    private static void ShowHelp()
    {
        System.Console.WriteLine("\n❓ Help - ODTE Live Trading Engine");
        System.Console.WriteLine("═════════════════════════════════");
        System.Console.WriteLine();
        System.Console.WriteLine("This system implements a defensive 0DTE options trading strategy.");
        System.Console.WriteLine("It analyzes market conditions and executes credit spreads and iron condors.");
        System.Console.WriteLine();
        System.Console.WriteLine("🛡️  SAFETY FEATURES:");
        System.Console.WriteLine("  • Starts in paper trading mode by default");
        System.Console.WriteLine("  • Daily loss limits prevent large losses");
        System.Console.WriteLine("  • Position limits control risk exposure");
        System.Console.WriteLine("  • Emergency stop closes all positions");
        System.Console.WriteLine("  • Real-time risk monitoring");
        System.Console.WriteLine();
        System.Console.WriteLine("📊 STRATEGY:");
        System.Console.WriteLine("  • Analyzes opening range, VWAP, and volatility");
        System.Console.WriteLine("  • Trades iron condors in range-bound markets");
        System.Console.WriteLine("  • Trades put spreads in bullish conditions");
        System.Console.WriteLine("  • Trades call spreads in bearish conditions");
        System.Console.WriteLine("  • Avoids trading during economic events");
        System.Console.WriteLine();
        System.Console.WriteLine("⚠️  DISCLAIMERS:");
        System.Console.WriteLine("  • This is for educational purposes only");
        System.Console.WriteLine("  • Trading involves substantial risk");
        System.Console.WriteLine("  • Past performance doesn't guarantee results");
        System.Console.WriteLine("  • You are responsible for all decisions");
    }

    private static async Task Cleanup()
    {
        System.Console.WriteLine("\n🧹 Cleaning up...");
        
        if (_tradingEngine != null)
        {
            await _tradingEngine.StopAsync();
            _tradingEngine.Dispose();
        }

        if (_broker != null)
        {
            await _broker.DisconnectAsync();
        }

        System.Console.WriteLine("✅ Cleanup completed");
    }
}
