# 🧬 ODTE.Strategy - Backtest Execution Guide

## 📋 Overview
This guide documents the **mandatory procedure** for executing strategy backtests through the unified ODTE.Backtest system. All strategy models in this project must follow this exact workflow to ensure complete traceability and reproducibility.

## 🚨 Critical Requirements

### 1. Git Commit State Requirements
**BEFORE RUNNING ANY BACKTEST:**

```bash
# 1. Ensure all strategy model changes are committed
git status  # Must show "working tree clean"
git add .   # If any changes exist
git commit -m "Strategy model update: [describe changes]"

# 2. Record current commit hash
git rev-parse HEAD  # Copy this hash for documentation

# 3. Verify no uncommitted changes
git status  # Must show "nothing to commit, working tree clean"
```

**⚠️ NEVER run a backtest with uncommitted changes in strategy files**

### 2. Configuration Management
All strategy configurations must be stored in the centralized location:
```
ODTE.Configurations/
├── Models/
│   ├── SPX30DTE_v1.0_config.yaml
│   ├── PM414_v2.1_config.yaml
│   └── [ModelName]_v[Version]_config.yaml
└── backtest_tracking.md
```

## 🎯 Strategy Model Development Workflow

### Step 1: Strategy Model Implementation
Create or modify strategy models in the appropriate directory:
```
ODTE.Strategy/
├── SPX30DTE/
│   ├── SPX30DTEConfig.cs           # Core configuration
│   ├── SPX30DTEStrategyModel.cs    # Signal generation logic
│   └── README.md                   # Strategy documentation
├── PM414/
└── [YourStrategy]/
```

**Strategy Model Requirements:**
- Must implement `IStrategyModel` interface
- Must be tagged with `[StrategyModelName("ModelName")]` attribute
- Must generate signals only (no execution logic)
- Must include genetic algorithm optimization traceability

### Step 2: Configuration Creation
Create YAML configuration file:
```yaml
model_name: YourStrategy
model_version: v1.0
optimization_parameters:
  genetic_algorithm: [algorithm_type]
  last_optimization: [YYYY-MM-DD]
  optimization_source: ODTE.Strategy/YourStrategy/YourStrategyConfig.cs
  
underlying: SPX  # or relevant underlying
start: 2005-01-01
end: 2025-01-01
mode: comprehensive
# ... other configuration parameters
```

### Step 3: Pre-Backtest Git Workflow
```bash
# Navigate to project root
cd /path/to/ODTE

# Check current status
git status

# Add all strategy changes
git add ODTE.Strategy/
git add ODTE.Configurations/Models/YourStrategy_v1.0_config.yaml

# Commit with descriptive message
git commit -m "Strategy: YourStrategy v1.0 implementation

- Add YourStrategyConfig.cs with genetic algorithm optimization
- Add YourStrategyModel.cs with signal generation logic  
- Add YourStrategy_v1.0_config.yaml configuration
- Ready for backtest execution"

# Record commit hash for traceability
git rev-parse HEAD
# Example output: abc123def456789...
```

### Step 4: Backtest Execution
```bash
# Navigate to backtest project
cd ODTE.Backtest

# Execute backtest with full path to config
dotnet run "../ODTE.Configurations/Models/YourStrategy_v1.0_config.yaml"

# System will automatically:
# 1. Register strategy model from this project
# 2. Load configuration and validate
# 3. Execute unified backtest
# 4. Generate reports
# 5. Update backtest_tracking.md registry
```

### Step 5: Results Documentation
The system automatically creates:
```
ODTE.Configurations/
├── Reports/
│   ├── YourStrategy_[timestamp]_backtest_report.md
│   └── backtest_ledgers/
│       └── YourStrategy_[timestamp]_ledger.db
└── backtest_tracking.md  # Updated with new entry
```

## 📊 Strategy Model Interface Requirements

Every strategy model must implement:

```csharp
[StrategyModelName("YourStrategy")]
public class YourStrategyModel : IStrategyModel
{
    public string ModelName { get; } = "YourStrategy";
    public string ModelVersion { get; } = "v1.0";
    
    // Signal generation (what to trade)
    public async Task<List<CandidateOrder>> GenerateSignalsAsync(
        DateTime timestamp, MarketDataBar currentBar, PortfolioState portfolio);
    
    // Position management (when to exit)
    public async Task<List<CandidateOrder>> ManagePositionsAsync(
        DateTime timestamp, MarketDataBar currentBar, PortfolioState portfolio);
    
    // Model parameters for traceability
    public Dictionary<string, object> GetModelParameters();
    
    // Configuration validation
    public void ValidateConfiguration(SimConfig config);
    
    // Initialization and cleanup
    public async Task InitializeAsync(SimConfig config, IMarketData marketData, IOptionsData optionsData);
    public void Dispose();
}
```

## 🔍 Git Commit Traceability Example

Each backtest report includes git commit information:

```markdown
# YourStrategy v1.0 Backtest Report

## Git Traceability
- **Repository**: ODTE
- **Commit Hash**: abc123def456789012345678901234567890abcd
- **Commit Date**: 2025-08-24 18:30:00 UTC
- **Commit Message**: Strategy: YourStrategy v1.0 implementation
- **Strategy Files**:
  - `ODTE.Strategy/YourStrategy/YourStrategyConfig.cs`
  - `ODTE.Strategy/YourStrategy/YourStrategyModel.cs`
- **Configuration**: `ODTE.Configurations/Models/YourStrategy_v1.0_config.yaml`

## Model Configuration
- **Model Name**: YourStrategy
- **Version**: v1.0
- **Genetic Algorithm**: [algorithm_type]
- **Last Optimization**: 2025-08-15
```

## 🚨 Common Mistakes to Avoid

### ❌ NEVER Do This:
1. **Run backtest with uncommitted changes**
2. **Modify strategy files without committing**
3. **Create model-specific backtest runners**
4. **Run backtest from wrong directory**
5. **Modify configuration files after backtest**

### ✅ ALWAYS Do This:
1. **Commit all changes before backtest**
2. **Use centralized configuration system**
3. **Run from ODTE.Backtest directory**
4. **Document commit hash in results**
5. **Follow unified execution pipeline**

## 🔧 Troubleshooting

### Strategy Not Registered
```bash
# Error: "Unknown strategy model: 'YourStrategy'"
# Solution: Check strategy model attribute
[StrategyModelName("YourStrategy")]  # Must match config model_name
```

### Configuration Validation Failed
```bash
# Error: Configuration validation errors
# Solution: Check required fields in YAML config
model_name: YourStrategy      # Required
model_version: v1.0           # Required
underlying: SPX               # Required
start: 2005-01-01            # Required
end: 2025-01-01              # Required
```

### Build Errors
```bash
# Build strategy project first
cd ODTE.Strategy
dotnet build

# Then build backtest project
cd ../ODTE.Backtest  
dotnet build
```

## 📈 Expected Output

Successful backtest execution will show:
```
=== UNIFIED STRATEGY MODEL BACKTEST ===
🔍 Scanning for strategy models...
✅ Registered model: YourStrategy (YourStrategyModel)
📋 Loading strategy configuration: YourStrategy_v1.0_config.yaml
🎯 Model: YourStrategy v1.0
✅ Strategy model loaded: YourStrategy v1.0
🚀 Running strategy model backtest...
📊 Progress: 100.0% (5218/5218 days) - Portfolio: $[final_value]
✅ Strategy model backtest completed
📝 Strategy model report saved: Reports/YourStrategy_[timestamp]_backtest_report.md
📋 Backtest registry updated: backtest_tracking.md
```

## 📚 Related Documentation
- `/ODTE.Backtest/BACKTEST_EXECUTION_GUIDE.md` - Backtest engine details
- `/ODTE.Execution/EXECUTION_ENGINE_GUIDE.md` - Execution engine documentation
- `/ODTE.Historical/DATA_REQUIREMENTS_GUIDE.md` - Data requirements
- `/ODTE.Configurations/README.md` - Configuration system overview

---

**Version**: 1.0  
**Last Updated**: 2025-08-24  
**Git Commit**: [TO BE FILLED BY NEXT COMMIT]  
**Status**: ✅ Production Ready - Follow This Procedure Exactly