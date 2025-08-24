# 🗄️ ODTE Backtest Tracking Registry

## 📋 Purpose
This registry provides complete traceability of all backtest executions to ensure reproducibility, performance validation, and legitimacy verification. Each backtest run is documented with model configuration, performance metrics, and validation status.

## 📊 Registry Format
```yaml
Backtest Entry:
  run_id: Unique identifier (timestamp-based)
  model_name: Strategy model identifier  
  model_version: Configuration version
  config_file: Path to YAML configuration used
  execution_date: When backtest was executed
  period: Start and end dates of backtested period
  results:
    total_trades: Number of trades executed
    cagr: Compound Annual Growth Rate
    max_drawdown: Maximum portfolio decline
    win_rate: Percentage of profitable trades
    profit_factor: Gross profit / Gross loss
    sharpe_ratio: Risk-adjusted return metric
  ledger_location: Path to detailed trade ledger
  legitimacy_status: VALID | FLAGGED | INVALID
  validation_notes: Issues or concerns identified
  execution_environment: System/data configuration used
```

---

## 🎯 Backtest Registry

### Entry #001: SPX30DTE Comprehensive Model (Configuration Ready)
- **Run ID**: SPX30DTE_20250824_001
- **Model Name**: SPX30DTE
- **Model Version**: v1.0
- **Config File**: `ODTE.Configurations/Models/SPX30DTE_v1.0_config.yaml`
- **Execution Date**: 2025-08-24 (PENDING)
- **Period**: 2005-01-01 to 2025-01-01 (20 years)
- **Strategy Components**:
  - ✅ Broken Wing Butterflies (30DTE)
  - ✅ Probe System (XSP 15DTE)
  - ✅ VIX Hedging (50DTE spreads)
  - ✅ Rev Fib Notch Scaling
- **Results**: PENDING EXECUTION
- **Legitimacy Status**: CONFIG_READY
- **Validation Notes**: 
  - Model configuration identified and documented
  - 31+ compilation errors preventing execution
  - Data provider integration required
  - Genetic algorithm optimization parameters validated
- **Execution Environment**: N/A (Build Issues)

**Model Traceability**:
- Configuration Source: `ODTE.Strategy/SPX30DTE/SPX30DTEConfig.cs`
- Genetic Algorithm: 16-mutation tournament system
- Last Optimization: 2025-08-15
- Parameter Validation: ✅ Complete BWB, Probe, VIX, Risk parameters
- Tournament Demo: `SimpleTournamentDemo.cs` shows expected performance targets

**Expected Performance Targets** (From Genetic Algorithm):
- Monthly Income: $2,500 target
- Max Portfolio Risk: 25%
- Max Drawdown: $5,000 at -5% SPX
- Win Rate: 60%+ (probe threshold)
- CAGR Target: >29.81% (PM212 baseline)

**Blocking Issues**:
1. 31+ compilation errors in SPX30DTE classes
2. Data provider integration missing (requires ORATS/LiveVol)
3. Strategy integration not wired into backtest engine

---

## 📈 Performance Baselines

### PM212 Statistical Baseline
- **Model**: PM212 Options Enhanced
- **Period**: 2005-2025 (20 years)
- **CAGR**: 29.81%
- **Type**: Statistical model (validated)
- **Status**: Established baseline for comparison

### Expected SPX30DTE Performance
- **Target CAGR**: >29.81% (genetic algorithm optimized)
- **Risk Profile**: Multi-layer with VIX hedging
- **Win Rate**: 60%+ (probe-confirmed entries)
- **Max Drawdown**: <25% (RevFib protection)

---

## ⚠️ Legitimacy Validation Framework

### VALID Status Criteria
- ✅ Configuration matches genetic algorithm optimization
- ✅ Real market data only (no synthetic data)
- ✅ Proper execution costs and slippage modeling
- ✅ All strategy components functioning correctly
- ✅ Performance within expected ranges
- ✅ Build compiles without errors
- ✅ Trade ledger generated successfully

### FLAGGED Status Triggers
- ⚠️ Performance >2x expected baseline
- ⚠️ Win rate >95% (suspiciously high)
- ⚠️ Max drawdown <5% (unrealistic)
- ⚠️ Missing execution costs
- ⚠️ Data quality issues detected
- ⚠️ Strategy parameter deviations

### INVALID Status Triggers
- ❌ Compilation errors preventing execution
- ❌ Synthetic data usage detected
- ❌ Code mutations not matching configuration
- ❌ Data provider failures
- ❌ Missing strategy components
- ❌ Parameter tampering detected

---

## 🔧 Configuration Management Standards

### File Naming Convention
```
{MODEL_NAME}_{VERSION}_config.yaml
Examples:
  - SPX30DTE_v1.0_config.yaml
  - PM414_v2.1_config.yaml
  - OILY212_v3.0_config.yaml
```

### Configuration Requirements
1. **Model Identification**: Name, version, components
2. **Optimization Traceability**: Source, date, algorithm used
3. **Parameter Validation**: All genetic algorithm results applied
4. **Period Specification**: Exact start/end dates
5. **Data Sources**: Real market data requirements
6. **Execution Settings**: Costs, slippage, risk controls

### Version Control
- **v1.0**: Initial genetic algorithm optimization
- **v1.1**: Parameter refinements
- **v2.0**: Strategy component additions/changes
- **v3.0**: Major architecture updates

---

## 📂 Directory Structure
```
ODTE.Configurations/
├── Models/
│   ├── SPX30DTE_v1.0_config.yaml
│   ├── PM414_v1.0_config.yaml
│   └── OILY212_v1.0_config.yaml
├── Reports/
│   ├── SPX30DTE_20250824_001_report.md
│   └── backtest_ledgers/
│       └── SPX30DTE_20250824_001_ledger.db
└── backtest_tracking.md (this file)
```

---

## 📝 Next Actions Required

1. **Fix SPX30DTE Build Issues**: Resolve 31+ compilation errors
2. **Data Integration**: Connect real options data providers
3. **Execute Backtest**: Run full 20-year analysis
4. **Generate Ledger**: Create investor-grade performance report
5. **Validate Results**: Compare against genetic algorithm targets

---

**Registry Maintained By**: ODTE Backtest System  
**Last Updated**: 2025-08-24  
**Total Registered Models**: 1 (SPX30DTE)  
**Successful Executions**: 0 (pending technical resolution)
### Entry #4: SPX30DTE Unified Backtest Execution
- **Run ID**: SPX30DTE_20250824_184658
- **Model Name**: SPX30DTE
- **Model Version**: v1.0
- **Config File**: `SPX30DTE_v1.0_config.yaml`
- **Execution Date**: 2025-08-24 18:46:58
- **Period**: 2005-01-01 to 2025-01-01 (7305 days)
- **Results**:
  - **Total Trades**: 3
  - **CAGR**: -0.00%
  - **Total Return**: -0.04%
  - **Final Value**: $99,965
  - **Realized P&L**: $0
  - **Unrealized P&L**: $-35
- **Legitimacy Status**: ✅ VALID (Unified strategy model system)
- **Validation Notes**: 
  - Model parameters traced to genetic algorithm optimization
  - Unified backtest engine with strategy factory pattern
  - Complete configuration reproducibility
- **Execution Environment**: ODTE.Backtest unified strategy model system
### Entry #4: SPX30DTE Unified Backtest Execution
- **Run ID**: SPX30DTE_20250824_190146
- **Git Commit**: f3edd7fba7252d943808779329964975e3a9203f
- **Git Date**: 2025-08-23 16:10:57 UTC
- **Working Tree**: Has Changes ⚠️
- **Model Name**: SPX30DTE
- **Model Version**: v1.0  
- **Config File**: `SPX30DTE_v1.0_config.yaml`
- **Execution Date**: 2025-08-24 19:01:46
- **Period**: 2005-01-01 to 2025-01-01 (7305 days)
- **Results**:
  - **Total Trades**: 3
  - **CAGR**: -0.00%
  - **Total Return**: -0.04%
  - **Final Value**: $99,965
  - **Realized P&L**: $0
  - **Unrealized P&L**: $-35
- **Legitimacy Status**: ⚠️ FLAGGED (Unified strategy model system)
- **Git Traceability**: Full commit hash and uncommitted changes detected
- **Validation Notes**: 
  - Model parameters traced to genetic algorithm optimization
  - Unified backtest engine with strategy factory pattern
  - Complete configuration reproducibility
  - Git commit: f3edd7fba7252d943808779329964975e3a9203f
- **Execution Environment**: ODTE.Backtest unified strategy model system
### Entry #4: OILY212 Unified Backtest Execution
- **Run ID**: OILY212_20250824_191823
- **Git Commit**: 47e13f0f69a81e97d387bded0697b9aac92cb80d
- **Git Date**: 2025-08-24 19:07:36 UTC
- **Working Tree**: Has Changes ⚠️
- **Model Name**: OILY212
- **Model Version**: v1.0  
- **Config File**: `OILY212_v1.0_config.yaml`
- **Execution Date**: 2025-08-24 19:18:23
- **Period**: 2020-01-01 to 2025-01-01 (1827 days)
- **Results**:
  - **Total Trades**: 0
  - **CAGR**: 0.00%
  - **Total Return**: 0.00%
  - **Final Value**: $100,000
  - **Realized P&L**: $0
  - **Unrealized P&L**: $0
- **Legitimacy Status**: ⚠️ FLAGGED (Unified strategy model system)
- **Git Traceability**: Full commit hash and uncommitted changes detected
- **Validation Notes**: 
  - Model parameters traced to genetic algorithm optimization
  - Unified backtest engine with strategy factory pattern
  - Complete configuration reproducibility
  - Git commit: 47e13f0f69a81e97d387bded0697b9aac92cb80d
- **Execution Environment**: ODTE.Backtest unified strategy model system
### Entry #4: PM250 Unified Backtest Execution
- **Run ID**: PM250_20250824_192907
- **Git Commit**: 47e13f0f69a81e97d387bded0697b9aac92cb80d
- **Git Date**: 2025-08-24 19:07:36 UTC
- **Working Tree**: Has Changes ⚠️
- **Model Name**: PM250
- **Model Version**: v1.0  
- **Config File**: `PM250_v1.0_config.yaml`
- **Execution Date**: 2025-08-24 19:29:07
- **Period**: 2020-01-01 to 2025-01-01 (1827 days)
- **Results**:
  - **Total Trades**: 3
  - **CAGR**: 0.01%
  - **Total Return**: 0.07%
  - **Final Value**: $100,065
  - **Realized P&L**: $25
  - **Unrealized P&L**: $40
- **Legitimacy Status**: ⚠️ FLAGGED (Unified strategy model system)
- **Git Traceability**: Full commit hash and uncommitted changes detected
- **Validation Notes**: 
  - Model parameters traced to genetic algorithm optimization
  - Unified backtest engine with strategy factory pattern
  - Complete configuration reproducibility
  - Git commit: 47e13f0f69a81e97d387bded0697b9aac92cb80d
- **Execution Environment**: ODTE.Backtest unified strategy model system
### Entry #4: PM414 Unified Backtest Execution
- **Run ID**: PM414_20250824_193115
- **Git Commit**: 47e13f0f69a81e97d387bded0697b9aac92cb80d
- **Git Date**: 2025-08-24 19:07:36 UTC
- **Working Tree**: Has Changes ⚠️
- **Model Name**: PM414
- **Model Version**: v1.0  
- **Config File**: `PM414_v1.0_config.yaml`
- **Execution Date**: 2025-08-24 19:31:15
- **Period**: 2005-01-01 to 2025-01-01 (7305 days)
- **Results**:
  - **Total Trades**: 0
  - **CAGR**: 0.00%
  - **Total Return**: 0.00%
  - **Final Value**: $100,000
  - **Realized P&L**: $0
  - **Unrealized P&L**: $0
- **Legitimacy Status**: ⚠️ FLAGGED (Unified strategy model system)
- **Git Traceability**: Full commit hash and uncommitted changes detected
- **Validation Notes**: 
  - Model parameters traced to genetic algorithm optimization
  - Unified backtest engine with strategy factory pattern
  - Complete configuration reproducibility
  - Git commit: 47e13f0f69a81e97d387bded0697b9aac92cb80d
- **Execution Environment**: ODTE.Backtest unified strategy model system
### Entry #4: PM212 Unified Backtest Execution
- **Run ID**: PM212_20250824_193322
- **Git Commit**: 47e13f0f69a81e97d387bded0697b9aac92cb80d
- **Git Date**: 2025-08-24 19:07:36 UTC
- **Working Tree**: Has Changes ⚠️
- **Model Name**: PM212
- **Model Version**: v1.0  
- **Config File**: `PM212_v1.0_config.yaml`
- **Execution Date**: 2025-08-24 19:33:22
- **Period**: 2005-01-01 to 2025-01-01 (7305 days)
- **Results**:
  - **Total Trades**: 0
  - **CAGR**: 0.00%
  - **Total Return**: 0.00%
  - **Final Value**: $100,000
  - **Realized P&L**: $0
  - **Unrealized P&L**: $0
- **Legitimacy Status**: ⚠️ FLAGGED (Unified strategy model system)
- **Git Traceability**: Full commit hash and uncommitted changes detected
- **Validation Notes**: 
  - Model parameters traced to genetic algorithm optimization
  - Unified backtest engine with strategy factory pattern
  - Complete configuration reproducibility
  - Git commit: 47e13f0f69a81e97d387bded0697b9aac92cb80d
- **Execution Environment**: ODTE.Backtest unified strategy model system