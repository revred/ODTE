# 🗄️ ODTE.Historical - Backtest Data Provider Guide

## 📋 Overview
This guide documents the **data provider role** of ODTE.Historical in the unified backtest execution system. ODTE.Historical serves as the **authoritative data source** for all backtests, providing authentic market data, options chains, and economic calendar information through standardized interfaces.

## 🚨 Critical Data Requirements

### 1. Git Commit State Requirements
**BEFORE ANY BACKTEST EXECUTION:**

```bash
# 1. Ensure all data provider changes are committed
git status  # Must show "working tree clean"
git add ODTE.Historical/   # If any changes exist
git commit -m "Historical data provider update: [describe changes]"

# 2. Record current commit hash
git rev-parse HEAD  # Copy this hash for documentation

# 3. Verify no uncommitted changes in data providers
git status  # Must show "nothing to commit, working tree clean"
```

**⚠️ NEVER run a backtest with uncommitted changes in data provider files**

### 2. Data Integrity Validation
All backtest data must pass integrity checks:
```bash
# Navigate to historical data project
cd ODTE.Historical

# Run data integrity validation
dotnet test --filter "Category=DataIntegrity"

# Validate all data providers are functional
dotnet run --validate-providers

# Check data completeness for backtest period
dotnet run --check-coverage --start 2005-01-01 --end 2025-01-01
```

## 🏗️ Data Provider Architecture

### Unified Data Interface for Backtests
```
┌─────────────────────────────────────────────────────────────────────┐
│                     ODTE.Historical Data Providers                  │
├─────────────────┬─────────────────┬─────────────────┬───────────────┤
│  Market Data    │  Options Data   │   VIX Data      │ Economic Data │
│  Provider       │  Provider       │   Provider      │ Provider      │
│                 │                 │                 │               │
│ ├ SPX bars      │ ├ Options chains│ ├ VIX daily     │ ├ Fed events  │
│ ├ Volume data   │ ├ Greeks calc   │ ├ VIX9D daily   │ ├ Earnings    │
│ ├ OHLC data     │ ├ IV surfaces   │ ├ Term structure│ ├ FOMC dates  │
│ └ Session info  │ └ Expiry cycles │ └ Volatility    │ └ Holiday cal │
└─────────────────┴─────────────────┴─────────────────┴───────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│                 Unified Backtest Data Pipeline                     │
│                                                                     │
│  ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐ │
│  │   Data Cache    │───▶│  Data Validator │───▶│   Data Feeder   │ │
│  │ (Performance)   │    │  (Quality)      │    │ (Backtest API)  │ │
│  └─────────────────┘    └─────────────────┘    └─────────────────┘ │
└─────────────────────────────────────────────────────────────────────┘
```

## 📊 Data Provider Interface Requirements

### IMarketData Interface
Every market data provider must implement:
```csharp
public interface IMarketData
{
    // Unified data access for all strategies
    IEnumerable<MarketDataBar> GetBars(DateTime start, DateTime end);
    MarketDataBar GetCurrentBar(DateTime timestamp);
    
    // Session and timing information
    bool IsMarketOpen(DateTime timestamp);
    DateTime GetNextTradingDay(DateTime date);
    
    // Data quality validation
    DataQualityReport ValidateData(DateTime start, DateTime end);
    bool HasDataForPeriod(DateTime start, DateTime end);
}
```

### IOptionsData Interface
Every options data provider must implement:
```csharp
public interface IOptionsData
{
    // Options chain access for strategy models
    OptionsChain GetOptionsChain(string underlying, DateTime expiry, DateTime timestamp);
    IEnumerable<DateTime> GetExpiryDates(string underlying, DateTime start, DateTime end);
    
    // Greeks and pricing for execution engine
    OptionQuote GetOptionQuote(OptionContract contract, DateTime timestamp);
    Greeks CalculateGreeks(OptionContract contract, DateTime timestamp);
    
    // Data validation and quality
    OptionsDataQualityReport ValidateOptionsData(DateTime start, DateTime end);
}
```

## 🎯 Backtest Data Workflow

### Phase 1: Pre-Backtest Data Validation
```bash
=== BACKTEST DATA VALIDATION ===
Configuration: ../ODTE.Configurations/Models/SPX30DTE_v1.0_config.yaml

🔍 Git State Verification (ODTE.Historical)
   ├── Repository: ODTE
   ├── Commit Hash: abc123def456789012345678901234567890abcd  
   ├── Working Tree: Clean ✅
   └── Data Provider Files: All committed ✅

📊 Data Provider Initialization
   ├── Market Data: CsvMarketData (./Samples/bars_spx_min.csv)
   ├── Options Data: SyntheticOptionsData (Black-Scholes)
   ├── VIX Data: CsvVixData (./Samples/vix_daily.csv)
   ├── Economic Calendar: CsvCalendarData (./Samples/calendar.csv)
   └── Data Period: 2005-01-01 to 2025-01-01 (7305 trading days)

🔍 Data Quality Validation
   ├── Market Data Coverage: 100% (7305/7305 days) ✅
   ├── Options Data Integrity: Validated ✅
   ├── VIX Data Continuity: No gaps detected ✅
   ├── Calendar Data Complete: 5218 trading days ✅
   └── Data Provider Health: All providers operational ✅
```

### Phase 2: Data Provider Integration
```csharp
// Market data provides historical bars
var marketData = new CsvMarketData(config.Paths.BarsCsv, config.Timezone, config.RthOnly);

// Options data provides pricing and Greeks
var optionsData = new SyntheticOptionsData(config, marketData, vixPath, vix9dPath);

// Economic calendar provides event information
var calendar = new CsvEconomicCalendar(config.Paths.CalendarCsv);

// Data validation before backtest execution
var dataReport = await ValidateAllDataProviders(config);
if (!dataReport.IsValid)
{
    throw new InvalidOperationException($"Data validation failed: {dataReport.ErrorMessage}");
}
```

### Phase 3: Real-Time Data Feeding
During backtest execution, data providers supply:
```csharp
// Daily market bar for strategy context
var dayBar = marketData.GetBars(currentDate, currentDate).First();

// Options chain for signal generation
var optionsChain = optionsData.GetOptionsChain("SPX", expiry, currentDate);

// VIX levels for volatility context
var vixLevel = vixData.GetVixLevel(currentDate);

// Economic events for risk management
var events = calendar.GetEvents(currentDate);
```

## 🔍 Data Source Configuration

### CSV Data Provider Configuration
```yaml
# Market data paths in strategy configuration
paths:
  bars_csv: ./Samples/bars_spx_min.csv          # OHLC + Volume data
  vix_csv: ./Samples/vix_daily.csv              # VIX volatility index
  vix9d_csv: ./Samples/vix9d_daily.csv          # Short-term VIX
  calendar_csv: ./Samples/calendar.csv          # Economic calendar
  reports_dir: ./Reports                        # Output directory

# Data provider settings
timezone: America/New_York                      # Market timezone
rth_only: true                                 # Regular trading hours only
```

### Database Data Provider Configuration
```yaml
# Alternative database configuration
data_sources:
  market_data:
    provider: DatabaseMarketData
    connection_string: "Server=localhost;Database=MarketData;Integrated Security=true"
    table_name: "SPXBars"
    
  options_data:
    provider: DatabaseOptionsData  
    connection_string: "Server=localhost;Database=OptionsData;Integrated Security=true"
    chains_table: "OptionsChains"
    quotes_table: "OptionQuotes"
```

## 📋 Data Validation Requirements

### Pre-Backtest Validation Checklist
```bash
✅ Data Provider Validation:
   ├── All required CSV files exist and readable
   ├── Data covers complete backtest period (no gaps)
   ├── OHLC data passes sanity checks (High >= Low, etc.)
   ├── Volume data is non-negative
   ├── VIX data is reasonable (typically 5-80 range)
   ├── Calendar data includes all trading days
   └── No corrupted or malformed data records

✅ Options Data Validation:
   ├── Options chains available for all required expiries
   ├── Bid/Ask spreads are reasonable (not wider than 100% of mid)
   ├── Implied volatility calculations are stable
   ├── Greeks calculations are mathematically consistent
   ├── Strike prices align with market conventions
   └── Expiry dates match standard options cycles

✅ Performance Validation:
   ├── Data loading time is acceptable (<30 seconds)
   ├── Memory usage is within bounds (<4GB)
   ├── No data provider exceptions during sample run
   └── Cache performance is optimized
```

### Data Quality Monitoring
```csharp
public class DataQualityReport
{
    public bool IsValid { get; set; }
    public List<string> Warnings { get; set; }
    public List<string> Errors { get; set; }
    
    // Market data quality metrics
    public double DataCompleteness { get; set; }  // 0.0 to 1.0
    public int MissingDays { get; set; }
    public int DataGaps { get; set; }
    
    // Options data quality metrics
    public double AverageSpreadWidth { get; set; }
    public int UnreasonableQuotes { get; set; }
    public double GreeksStability { get; set; }
}
```

## 🚨 Common Data Issues & Solutions

### Data Provider Not Found
```bash
ERROR: Could not load market data provider: CsvMarketData
```
**Solution**:
```bash
# Check data file paths in configuration
cd ODTE.Historical && dotnet build  # Ensure project builds
ls -la ./Samples/                   # Verify data files exist
# Update paths in YAML config if needed
```

### Data Coverage Gaps
```bash
⚠️ Missing market data for date range: 2020-03-16 to 2020-03-17
```
**Solution**:
```bash
# Check for holiday/weekend dates in gap
# Verify data file has complete coverage
# Add missing data or adjust backtest period
```

### Options Data Invalid
```bash
ERROR: Options chain validation failed for SPX 2023-12-15
```
**Solution**:
```bash
# Check options expiry dates align with calendar
# Validate strike price ranges are reasonable
# Ensure bid/ask data is not inverted
```

### Performance Issues
```bash
⚠️ Data loading took 180 seconds (expected <30s)
```
**Solution**:
```bash
# Enable data caching in configuration
# Consider database provider for large datasets
# Optimize CSV file formats (remove unnecessary columns)
```

## 🔧 Debug Mode Data Analysis

### Data Provider Testing
```bash
# Test individual data providers
cd ODTE.Historical.Tests

# Run market data provider tests
dotnet test --filter "Category=MarketData"

# Run options data provider tests  
dotnet test --filter "Category=OptionsData"

# Run data integration tests
dotnet test --filter "Category=Integration"

# Test data with specific date range
dotnet run --test-data --start 2023-01-01 --end 2023-12-31
```

### Data Quality Analysis
```bash
# Generate data quality report
dotnet run --data-quality-report --period 2020-01-01..2020-12-31

# Analyze data gaps and issues
dotnet run --analyze-gaps --source ./Samples/bars_spx_min.csv

# Validate options data consistency
dotnet run --validate-options --underlying SPX --period 2023
```

## 📚 Integration with Other Components

### ODTE.Backtest Integration
```csharp
// ODTE.Backtest loads data providers based on configuration
var config = LoadConfiguration(configPath);
var marketData = DataProviderFactory.CreateMarketData(config);
var optionsData = DataProviderFactory.CreateOptionsData(config, marketData);

// Data providers feed the backtest execution engine
foreach (var day in tradingDays)
{
    var marketBar = marketData.GetBars(day, day).First();
    var optionsChain = optionsData.GetOptionsChain(underlying, expiry, day);
    
    // Strategy generates signals using data context
    var signals = await strategyModel.GenerateSignalsAsync(day, marketBar, portfolio);
}
```

### ODTE.Execution Integration
```csharp
// Execution engine uses options data for realistic fills
var quote = optionsData.GetOptionQuote(contract, timestamp);
var fillPrice = executionEngine.CalculateFillPrice(quote, orderType, slippageCfg);

// Greeks calculations for position risk
var greeks = optionsData.CalculateGreeks(contract, timestamp);
var positionDelta = portfolio.CalculatePositionDelta(greeks);
```

### ODTE.Strategy Integration  
```csharp
// Strategy models receive data context for signal generation
public async Task<List<CandidateOrder>> GenerateSignalsAsync(
    DateTime timestamp, MarketDataBar currentBar, PortfolioState portfolio)
{
    // Market data provides context
    var trend = AnalyzeMarketTrend(currentBar, historicalBars);
    
    // VIX data provides volatility context
    var vixLevel = GetVixLevel(timestamp);
    
    // Generate signals based on data context
    return await GenerateDataDrivenSignals(trend, vixLevel, portfolio);
}
```

---

## 📝 Data Provider Traceability

### Required Git Information in Reports
Every backtest report must include:
```markdown
## Data Provider Traceability
- **Repository**: ODTE
- **Commit Hash**: abc123def456789012345678901234567890abcd
- **Commit Date**: 2025-08-24 18:30:00 UTC
- **Data Provider Files**:
  - `ODTE.Historical/Providers/CsvMarketData.cs`
  - `ODTE.Historical/Providers/SyntheticOptionsData.cs`
  - `ODTE.Historical/Providers/CsvVixData.cs`
  - `ODTE.Historical/Providers/CsvEconomicCalendar.cs`
- **Data Sources**: 
  - Market Data: `./Samples/bars_spx_min.csv` (7305 trading days)
  - VIX Data: `./Samples/vix_daily.csv` (5218 days)
  - Options: Synthetic Black-Scholes with real VIX surface
- **Data Quality**: 100% coverage, 0 gaps, validated ✅
```

### Data Provider Versioning
```yaml
# Data provider version tracking
data_providers:
  market_data_version: v2.1
  options_data_version: v1.3
  vix_data_version: v1.0
  calendar_version: v1.1
  last_validation: 2025-08-24
  git_commit: abc123def456789012345678901234567890abcd
```

---

## 🚨 Critical Requirements Summary

### ✅ ALWAYS Do This:
1. **Commit all data provider changes** before backtest execution
2. **Validate data coverage** for complete backtest period
3. **Test data providers** independently before integration
4. **Document data sources** in backtest reports
5. **Record git commit hash** for data provider traceability

### ❌ NEVER Do This:
1. **Run backtest with uncommitted data provider changes**
2. **Skip data validation** steps
3. **Use corrupted or incomplete data** without validation
4. **Modify data during backtest** execution
5. **Ignore data quality warnings**

---

**Version**: 1.0  
**Last Updated**: 2025-08-24  
**Git Commit**: [TO BE FILLED BY NEXT COMMIT]  
**Execution Command**: Data providers are automatically initialized by ODTE.Backtest  
**Status**: ✅ Production Ready - Unified Data Provider System