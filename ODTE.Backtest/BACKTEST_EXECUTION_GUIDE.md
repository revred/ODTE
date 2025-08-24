# 🎯 ODTE.Backtest - Unified Backtest Engine Guide

## 📋 Overview
This is the **unified backtest execution engine** that handles all strategy models through a centralized, reproducible pipeline. This engine eliminates model-specific backtester code and ensures consistent execution across all strategies.

## 🚨 Mandatory Execution Requirements

### 1. Git State Verification
**BEFORE EVERY BACKTEST EXECUTION:**

```bash
# Navigate to ODTE root directory
cd /path/to/ODTE

# Verify clean working tree
git status
# MUST show: "nothing to commit, working tree clean"

# If changes exist, commit them first
git add .
git commit -m "Pre-backtest: [describe any changes]"

# Record current commit hash
git rev-parse HEAD
# Save this hash - it will be included in all backtest reports
```

### 2. Execution Command Format
```bash
# ALWAYS execute from ODTE.Backtest directory
cd ODTE.Backtest

# Execute with absolute path to configuration
dotnet run "../ODTE.Configurations/Models/[ModelName]_v[Version]_config.yaml"

# Examples:
dotnet run "../ODTE.Configurations/Models/SPX30DTE_v1.0_config.yaml"
dotnet run "../ODTE.Configurations/Models/PM414_v2.1_config.yaml"
dotnet run "../ODTE.Configurations/Models/OILY212_v3.0_config.yaml"
```

## 🏗️ System Architecture

### Unified Execution Pipeline
```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│  Strategy Model │───▶│  ODTE.Backtest  │───▶│ ODTE.Execution  │───▶│   Results &     │
│ (Signal Gen)    │    │   (Orchestrator) │    │ (Fill Engine)   │    │   Reports       │
└─────────────────┘    └─────────────────┘    └─────────────────┘    └─────────────────┘
        │                        │                        │                        │
   Generates signals        Manages portfolio        Realistic fills         Complete audit
   - Entry conditions       - Position tracking       - Slippage modeling      - CAGR calculation
   - Exit conditions        - Risk management         - Commission costs       - Trade ledger
   - Position sizing        - Daily P&L tracking      - Market impact          - Git traceability
```

### Strategy Model Factory
The system automatically scans for strategy models and registers them:
```csharp
// Auto-discovery of strategy models
🔍 Scanning for strategy models...
✅ Registered model: SPX30DTE (SPX30DTEStrategyModel)
✅ Registered model: PM414 (PM414StrategyModel)  
✅ Registered model: OILY212 (OILY212StrategyModel)
📊 Total models registered: 3
```

## 🔧 Configuration-Driven Execution

### YAML Configuration Structure
```yaml
# Model identification (required)
model_name: SPX30DTE
model_version: v1.0

# Genetic algorithm traceability (required)
optimization_parameters:
  genetic_algorithm: 16_mutation_tournament
  last_optimization: 2025-08-15
  optimization_source: ODTE.Strategy/SPX30DTE/SPX30DTEConfig.cs
  tournament_demo: ODTE.Strategy/SPX30DTE/Mutations/SimpleTournamentDemo/SimpleTournamentDemo.cs
  strategy_components:
    - broken_wing_butterflies
    - probe_system
    - vix_hedging
    - rev_fib_notch_scaling

# Backtest parameters (required)
underlying: SPX
start: 2005-01-01
end: 2025-01-01
mode: comprehensive
rth_only: true
timezone: America/New_York

# Execution settings
cadence_seconds: 3600
no_new_risk_minutes_to_close: 60

# Cost modeling
slippage:
  entry_half_spread_ticks: 1.0
  exit_half_spread_ticks: 1.0
  tick_value: 0.05

fees:
  commission_per_contract: 0.65
  exchange_fees_per_contract: 0.25

# Data paths
paths:
  bars_csv: ./Samples/bars_spx_min.csv
  vix_csv: ./Samples/vix_daily.csv
  vix9d_csv: ./Samples/vix9d_daily.csv
  calendar_csv: ./Samples/calendar.csv
  reports_dir: ./Reports
```

## 📊 Execution Workflow

### Phase 1: Pre-Execution Validation
```
=== UNIFIED STRATEGY MODEL BACKTEST ===
Configuration: ../ODTE.Configurations/Models/SPX30DTE_v1.0_config.yaml

🔍 Git State Verification
   ├── Repository: ODTE
   ├── Commit Hash: abc123def456789012345678901234567890abcd  
   ├── Working Tree: Clean ✅
   └── Strategy Files: All committed ✅

🔍 Strategy Model Factory Initialization
   ├── Scanning assemblies for IStrategyModel implementations
   ├── Registering discovered models
   └── Total models registered: [count]

📋 Configuration Loading & Validation
   ├── YAML parsing and deserialization
   ├── Model parameter validation
   ├── Data path verification
   └── Period validation (minimum 30 days)
```

### Phase 2: Data & Engine Initialization
```
🔍 Initializing Components
   ├── Market Data Provider (CSV/Database)
   ├── Options Data Provider (Synthetic/Real)
   ├── Economic Calendar Provider
   ├── Execution Engine (RealisticFillEngine)
   ├── Risk Manager (Portfolio controls)
   └── Strategy Model (Signal generator)

🎯 Strategy Model: [ModelName] v[Version]
   ├── Genetic Algorithm: [algorithm_type]
   ├── Last Optimization: [date]
   ├── Components: [component_list]
   └── Initial Parameters: [key_parameters]
```

### Phase 3: Backtest Execution
```
🚀 Running Strategy Model Backtest
   ├── Trading Days: [count] ([start] to [end])
   ├── Starting Portfolio: $100,000
   └── Processing daily signals...

📊 Progress Updates (every 50 days)
   └── Progress: [XX.X]% ([processed]/[total] days) - Portfolio: $[current_value]

Daily Signal Processing:
   ├── Generate entry signals from strategy model
   ├── Manage existing positions (exits/adjustments)
   ├── Execute signals through ODTE.Execution engine
   ├── Apply realistic fills, slippage, and costs
   ├── Update portfolio state and P&L tracking
   └── Record daily results
```

### Phase 4: Results Generation
```
✅ Strategy Model Backtest Completed
   ├── Duration: [XX.X] minutes
   ├── Final Portfolio Value: $[final_value]
   ├── Total Return: [XX.XX]%
   └── Annualized CAGR: [XX.XX]%

📝 Automatic Report Generation
   ├── Performance Report: Reports/[ModelName]_[timestamp]_backtest_report.md
   ├── Trade Ledger: Reports/backtest_ledgers/[ModelName]_[timestamp]_ledger.db  
   └── Registry Update: ../ODTE.Configurations/backtest_tracking.md
```

## 📋 Generated Reports

### Performance Report Structure
```markdown
# [ModelName] v[Version] Backtest Report

## Git Traceability
- **Repository**: ODTE
- **Commit Hash**: [full_commit_hash]
- **Commit Date**: [commit_timestamp]
- **Working Tree**: Clean ✅
- **Configuration**: [config_file_path]

## Model Configuration  
- **Model Name**: [ModelName]
- **Version**: [Version]
- **Genetic Algorithm**: [algorithm_type]
- **Last Optimization**: [optimization_date]
- **Strategy Components**: [component_list]

## Performance Summary
- **Period**: [start_date] to [end_date] ([total_days] trading days)
- **Final Portfolio Value**: $[final_value]
- **Total Return**: [total_return]%
- **Annualized CAGR**: [cagr]%
- **Max Drawdown**: [max_drawdown]%
- **Sharpe Ratio**: [sharpe_ratio]
- **Total Trades**: [trade_count]

## Execution Environment
- **Engine**: ODTE.Backtest Unified Strategy Model System
- **Execution**: ODTE.Execution.RealisticFillEngine
- **Data**: [data_sources]
- **Legitimacy**: VALID ✅
```

### Registry Entry Structure
```markdown
### Entry #[N]: [ModelName] Unified Backtest Execution
- **Run ID**: [ModelName]_[timestamp]
- **Git Commit**: [commit_hash]
- **Model Name**: [ModelName]
- **Model Version**: [Version]  
- **Config File**: `[config_filename]`
- **Execution Date**: [execution_timestamp]
- **Period**: [start] to [end] ([total_days] days)
- **Results**:
  - **Total Trades**: [trade_count]
  - **CAGR**: [cagr]%
  - **Total Return**: [total_return]%
  - **Final Value**: $[final_value]
  - **Max Drawdown**: [max_drawdown]%
- **Legitimacy Status**: ✅ VALID (Unified strategy model system)
- **Git Traceability**: Full commit hash and clean working tree verified
- **Execution Environment**: ODTE.Backtest unified strategy model system
```

## 🔍 Quality Assurance Features

### Automatic Validation
- **Git State**: Ensures clean working tree before execution
- **Configuration**: Validates all required YAML fields
- **Model Registration**: Verifies strategy model implements IStrategyModel
- **Data Integrity**: Validates market data availability
- **Parameter Consistency**: Ensures genetic algorithm traceability

### Legitimacy Detection
The system flags results as INVALID if:
- ❌ Uncommitted changes in strategy files
- ❌ Missing genetic algorithm traceability  
- ❌ Performance metrics outside realistic bounds
- ❌ Configuration tampering detected
- ❌ Data quality issues identified

### Reproducibility Guarantees
Every backtest can be exactly reproduced using:
- **Git Commit Hash**: Exact code state used
- **Configuration File**: Exact parameters used  
- **Data Snapshot**: Exact market data used
- **Execution Engine**: Consistent fill modeling

## 🚨 Error Handling & Troubleshooting

### Common Issues & Solutions

#### 1. Strategy Model Not Found
```
ERROR: Unknown strategy model: 'YourStrategy'. Available models: [list]
```
**Solution**: 
- Verify `[StrategyModelName("YourStrategy")]` attribute on strategy class
- Ensure model_name in YAML matches attribute exactly
- Check strategy project builds successfully

#### 2. Git State Validation Failed
```  
ERROR: Working tree not clean. Commit changes before running backtest.
```
**Solution**:
```bash
git status               # Check what changed
git add .               # Stage changes  
git commit -m "..."     # Commit changes
# Then retry backtest
```

#### 3. Configuration Validation Failed
```
ERROR: SPX30DTE model requires SPX underlying, got: [other]
```
**Solution**: Check strategy-specific requirements in YAML config

#### 4. Data Provider Issues
```
⚠️ No market data for [date], skipping
```
**Solution**: Verify data files exist in paths specified in configuration

### Debug Mode Execution
```bash
# Enable verbose logging
dotnet run [config.yaml] --verbosity detailed

# Run with specific date range for testing
dotnet run [config.yaml] --range 2023-01-01..2023-12-31
```

## 📚 Integration with Other Components

### ODTE.Execution Integration
```csharp
// Strategy models generate signals
var signals = await strategyModel.GenerateSignalsAsync(timestamp, marketBar, portfolio);

// ODTE.Execution handles realistic fills
foreach (var signal in signals)
{
    var fillResult = await executionEngine.ExecuteSignalAsync(signal);
    portfolio.UpdateFromFill(fillResult);
}
```

### ODTE.Historical Integration  
```csharp
// Market data provides historical bars
var marketData = new CsvMarketData(config.Paths.BarsCsv, config.Timezone, config.RthOnly);
var dayBars = marketData.GetBars(day, day).ToList();

// Options data provides pricing
var optionsData = new SyntheticOptionsData(config, marketData, vixPath, vix9dPath);
```

### ODTE.Strategy Integration
```csharp
// Strategy factory auto-discovers models
StrategyModelFactory.Initialize();
var strategyModel = await StrategyModelFactory.CreateFromConfigAsync(configPath);

// Models focus only on signal generation
var entrySignals = await strategyModel.GenerateSignalsAsync(timestamp, marketBar, portfolio);
var exitSignals = await strategyModel.ManagePositionsAsync(timestamp, marketBar, portfolio);
```

---

**Version**: 1.0  
**Last Updated**: 2025-08-24  
**Git Commit**: [TO BE FILLED BY NEXT COMMIT]  
**Execution Command**: `cd ODTE.Backtest && dotnet run "../ODTE.Configurations/Models/[Model]_v[Version]_config.yaml"`  
**Status**: ✅ Production Ready - This Is The Only Way To Run Backtests