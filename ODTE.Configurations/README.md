# 🗄️ ODTE Configuration Management System

## 📋 Overview
Centralized configuration storage and backtest tracking system ensuring complete reproducibility and traceability for all ODTE strategy models.

## 📂 Directory Structure
```
ODTE.Configurations/
├── Models/                          # Strategy configuration files
│   ├── SPX30DTE_v1.0_config.yaml  # 30-day SPX with probes/VIX hedging
│   ├── PM414_v1.0_config.yaml     # Genetic evolution model
│   └── OILY212_v1.0_config.yaml   # Oil CDTE weekly strategy
├── Reports/                         # Generated backtest reports
│   ├── SPX30DTE_20250824_001_report.md
│   └── backtest_ledgers/           # SQLite trading ledgers
└── backtest_tracking.md            # Master registry of all runs
```

## 🏷️ Configuration Naming Standards

### File Naming Convention
```yaml
Format: {MODEL_NAME}_{VERSION}_config.yaml

Examples:
  SPX30DTE_v1.0_config.yaml  # 30-day SPX strategy, version 1.0
  PM414_v2.1_config.yaml     # PM414 genetic model, version 2.1
  OILY212_v3.0_config.yaml   # Oil CDTE strategy, version 3.0
```

### Version Numbering
- **v1.0**: Initial genetic algorithm optimization
- **v1.x**: Parameter refinements and bug fixes
- **v2.0**: Strategy component additions/changes
- **vX.0**: Major architecture updates

## 📊 Configuration Requirements

Each YAML configuration file must include:

### Model Identification
```yaml
model_name: SPX30DTE
model_version: v1.0
underlying: SPX
mode: SPX30DTE_comprehensive
```

### Optimization Traceability
```yaml
optimization_parameters:
  genetic_algorithm: 16_mutation_tournament
  last_optimization: 2025-08-15
  optimization_source: ODTE.Strategy/SPX30DTE/SPX30DTEConfig.cs
  strategy_components:
    - broken_wing_butterflies
    - probe_system
    - vix_hedging
    - rev_fib_notch_scaling
```

### Period and Data
```yaml
start: 2005-01-01
end: 2025-01-01
underlying: SPX
rth_only: true
timezone: America/New_York
```

### Strategy Parameters
Complete strategy configuration matching genetic algorithm optimization results

## 🎯 Backtest Execution Workflow

### 1. Create Configuration
```bash
# Copy template and customize
cp ODTE.Configurations/Models/template_config.yaml ODTE.Configurations/Models/MyModel_v1.0_config.yaml
```

### 2. Execute Backtest
```bash
cd ODTE.Backtest
dotnet run --config="../ODTE.Configurations/Models/MyModel_v1.0_config.yaml"
```

### 3. Register Results
Update `backtest_tracking.md` with:
- Run ID and timestamp
- Performance metrics (CAGR, max drawdown, win rate)
- Legitimacy validation status
- Ledger location

### 4. Generate Report
```bash
# Generate comprehensive report
dotnet run --report="../ODTE.Configurations/Reports/MyModel_20250824_001_report.md"
```

## ⚠️ Legitimacy Validation

### VALID Status (✅)
- Configuration matches genetic optimization
- Real market data only
- Proper execution costs
- All components functioning
- Performance within expected ranges

### FLAGGED Status (⚠️)
- Performance >2x baseline
- Win rate >95%
- Max drawdown <5%
- Missing execution costs
- Data quality issues

### INVALID Status (❌)
- Compilation errors
- Synthetic data usage
- Code mutations
- Missing components
- Parameter tampering

## 🔧 Usage Examples

### Running SPX30DTE Model
```bash
cd ODTE.Backtest
dotnet run --config="../ODTE.Configurations/Models/SPX30DTE_v1.0_config.yaml"
```

### Checking Registry Status
```bash
# View all registered backtests
cat ODTE.Configurations/backtest_tracking.md
```

### Creating New Model Configuration
```bash
# Start with existing successful config
cp ODTE.Configurations/Models/SPX30DTE_v1.0_config.yaml ODTE.Configurations/Models/NewModel_v1.0_config.yaml
# Edit parameters as needed
```

## 📈 Performance Baselines

### Established Baselines
- **PM212**: 29.81% CAGR (statistical model)
- **SPX30DTE**: Target >29.81% CAGR (genetic optimized)

### Expected Performance Ranges
- **CAGR**: 25-40% for properly optimized strategies
- **Win Rate**: 60-80% for high-probability strategies
- **Max Drawdown**: 10-25% for balanced risk profiles
- **Sharpe Ratio**: 1.5-2.5 for quality strategies

## 🚨 Critical Warnings

### Configuration Integrity
- NEVER modify configuration files after backtest execution
- ALWAYS create new version for parameter changes
- NEVER use synthetic data without explicit flagging
- ALWAYS validate genetic algorithm parameter sources

### Results Validation
- Performance >50% CAGR requires investigation
- Win rates >90% are suspicious
- Missing execution costs invalidate results
- Data gaps or quality issues must be documented

---

**System Purpose**: Ensure complete traceability and reproducibility of all ODTE strategy backtests  
**Maintained By**: ODTE Development Team  
**Created**: 2025-08-24  
**Status**: Production Ready