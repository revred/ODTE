using ODTE.Backtest.Config;
using ODTE.Backtest.Engine;
using ODTE.Backtest.Reporting;
using ODTE.Backtest.Data;
using ODTE.Backtest.Signals;
using ODTE.Backtest.Strategy;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ODTE.Backtest;

/// <summary>
/// Main entry point for the 0DTE options backtesting engine.
/// WHY: Composition root that wires all components and orchestrates the backtest execution.
/// 
/// ARCHITECTURE OVERVIEW:
/// This application follows a layered, dependency-injection style architecture:
/// 
/// 1. CONFIGURATION LAYER: YAML → strongly-typed config objects
/// 2. DATA LAYER: CSV files → market data & options quotes
/// 3. SIGNALS LAYER: Market data → regime classification & trade signals
/// 4. STRATEGY LAYER: Signals → concrete spread orders
/// 5. EXECUTION LAYER: Orders → realistic fills with slippage
/// 6. RISK LAYER: Portfolio-level controls & limits
/// 7. REPORTING LAYER: Results → summary & detailed CSV analysis
/// 
/// DEPENDENCY FLOW:
/// Configuration → Data Providers → Signal Generators → Strategy Builder → 
/// Execution Engine → Risk Manager → Backtester → Reporter
/// 
/// EXTENSIBILITY DESIGN:
/// - Interface-based: Easy to swap CSV → database → live feeds
/// - Component isolation: Each layer can be tested independently
/// - Configuration-driven: Strategy parameters tunable without recompilation
/// - Plugin-ready: Add new strategies or data sources easily
/// 
/// COMMAND-LINE USAGE:
/// - Default: dotnet run (uses appsettings.yaml)
/// - Custom config: dotnet run custom-config.yaml
/// - Multiple scenarios: Easy to compare different parameter sets
/// 
/// UPGRADE PATH TO PRODUCTION:
/// 1. Replace CSV data providers with live APIs (ORATS/LiveVol/dxFeed)
/// 2. Add real broker integration (Interactive Brokers TWS API)
/// 3. Implement live position monitoring and management
/// 4. Add real-time risk controls and alerts
/// 5. Integrate with portfolio management systems
/// 
/// REFERENCES FOR FURTHER DEVELOPMENT:
/// - IBKR TWS API: https://interactivebrokers.github.io/tws-api/
/// - Client Portal API: https://www.interactivebrokers.com/campus/ibkr-api-page/cpapi-v1/
/// - Options Data Vendors:
///   * ORATS: https://orats.com/one-minute-data
///   * LiveVol (Cboe): https://datashop.cboe.com/
///   * dxFeed: https://www.livevol.com/stock-options-analysis-data/
/// </summary>
public class Program
{
    /// <summary>
    /// Main application entry point.
    /// Loads configuration, wires dependencies, runs backtest, and generates reports.
    /// 
    /// EXECUTION FLOW:
    /// 1. Configuration: Load and validate YAML settings
    /// 2. Data Layer: Initialize market data and options providers
    /// 3. Engine Layer: Create signal, strategy, execution, and risk components
    /// 4. Orchestration: Run backtester across specified date range
    /// 5. Reporting: Generate summary and detailed trade analysis
    /// 
    /// ERROR HANDLING:
    /// - Configuration errors: Invalid YAML or missing files
    /// - Data errors: Missing CSV files or malformed data
    /// - Runtime errors: Calculation failures or unexpected market conditions
    /// - All errors bubble up with descriptive messages
    /// 
    /// PERFORMANCE CONSIDERATIONS:
    /// - Memory: Large date ranges may require streaming data access
    /// - CPU: Minute-by-minute simulation can be compute-intensive
    /// - I/O: CSV reading optimized with CsvHelper library
    /// - Optimization: Consider parallel processing for parameter sweeps
    /// </summary>
    public static async Task Main(string[] args)
    {
        try
        {
            Console.WriteLine("=== 0DTE Options Backtest Engine ===");
            Console.WriteLine($"Starting at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            
            // === 1. CONFIGURATION LOADING ===
            string cfgPath = args.FirstOrDefault(a => a.EndsWith(".yaml")) ?? "appsettings.yaml";
            Console.WriteLine($"Loading configuration from: {cfgPath}");
            var cfg = LoadConfig(cfgPath);
            
            Console.WriteLine($"Backtest period: {cfg.Start:yyyy-MM-dd} to {cfg.End:yyyy-MM-dd}");
            Console.WriteLine($"Underlying: {cfg.Underlying}, Mode: {cfg.Mode}");

            // Ensure output directory exists
            Directory.CreateDirectory(cfg.Paths.ReportsDir);

            // === 2. DATA LAYER INITIALIZATION ===
            Console.WriteLine("Initializing data providers...");
            
            // Market data: SPX/ES minute bars with RTH filtering
            IMarketData market = new CsvMarketData(cfg.Paths.BarsCsv, cfg.Timezone, cfg.RthOnly);
            
            // Economic calendar: FOMC/CPI/NFP events for risk gating
            IEconCalendar econ = new CsvCalendar(cfg.Paths.CalendarCsv, cfg.Timezone);
            
            // Options data: Synthetic (prototype) or real vendor data (production)
            IOptionsData options = cfg.Mode.Equals("prototype", StringComparison.OrdinalIgnoreCase)
                ? new SyntheticOptionsData(cfg, market, cfg.Paths.VixCsv, cfg.Paths.Vix9dCsv)
                : throw new NotImplementedException("Pro‑grade adapter: plug ORATS/LiveVol/dxFeed here.");

            // === 3. ENGINE LAYER INITIALIZATION ===
            Console.WriteLine("Initializing trading engines...");
            
            var scorer = new RegimeScorer(cfg);     // Market regime classification
            var builder = new SpreadBuilder(cfg);   // Strategy → concrete orders
            var exec = new ExecutionEngine(cfg);    // Realistic fill modeling
            var risk = new RiskManager(cfg);        // Portfolio risk controls
            
            // Master orchestrator
            var backtester = new Backtester(cfg, market, options, econ, scorer, builder, exec, risk);

            // === 4. BACKTEST EXECUTION ===
            Console.WriteLine("Running backtest simulation...");
            var startTime = DateTime.Now;
            
            var report = await backtester.RunAsync();
            
            var duration = DateTime.Now - startTime;
            Console.WriteLine($"Backtest completed in {duration.TotalSeconds:F1} seconds");

            // === 5. REPORTING ===
            Console.WriteLine("Generating reports...");
            Reporter.WriteSummary(cfg, report);
            Reporter.WriteTrades(cfg, report);

            Console.WriteLine($"\n=== BACKTEST COMPLETE ===");
            Console.WriteLine($"Reports saved to: {cfg.Paths.ReportsDir}");
            Console.WriteLine($"Total trades: {report.Trades.Count}");
            Console.WriteLine($"Net P&L: ${report.NetPnL:F2}");
            Console.WriteLine($"Sharpe Ratio: {report.Sharpe:F2}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.WriteLine("Stack trace:");
            Console.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Load and deserialize YAML configuration file.
    /// WHY: YAML provides human-readable, git-friendly configuration format.
    /// 
    /// YAML ADVANTAGES:
    /// - Human readable: Easy to edit and understand
    /// - Comments: Document parameter choices directly in config
    /// - Version control: Git diffs show parameter changes clearly
    /// - Hierarchical: Natural nesting for complex configurations
    /// - Type safe: Deserializes to strongly-typed C# objects
    /// 
    /// NAMING CONVENTION:
    /// Uses underscore_case in YAML → camelCase in C# (YamlDotNet convention)
    /// Example: daily_loss_stop → DailyLossStop
    /// 
    /// ERROR HANDLING:
    /// - File not found: Clear error message with expected path
    /// - Invalid YAML: Syntax errors with line numbers
    /// - Type mismatches: Configuration validation errors
    /// - Missing properties: Ignored (uses C# defaults)
    /// 
    /// ENHANCEMENT OPPORTUNITIES:
    /// - Schema validation: Ensure all required properties present
    /// - Environment substitution: ${ENV_VAR} replacements
    /// - Multiple environments: dev/test/prod configs
    /// - Runtime reload: Watch file for changes during development
    /// </summary>
    static SimConfig LoadConfig(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Configuration file not found: {path}");
            }

            var yaml = File.ReadAllText(path);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()  // Graceful handling of extra properties
                .Build();
                
            return deserializer.Deserialize<SimConfig>(yaml);
        }
        catch (Exception ex) when (!(ex is FileNotFoundException))
        {
            throw new InvalidOperationException($"Failed to load configuration from {path}: {ex.Message}", ex);
        }
    }
}