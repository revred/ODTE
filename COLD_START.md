# 🚀  ODTE Cold Start Guide for Claude Code

## 📋 Quick Context Alignment

**ODTE** is a genetic strategy evolution platform for 0DTE options trading, featuring:
- **PM414 Genetic Evolution** (Current Focus) - 100 mutations targeting >29.81% CAGR
- **ODTE.Execution Centralized Engine** - Strategy-agnostic execution for ALL models
- **DistributedDatabaseManager** - 20+ years real options chains (2005-2025)
- **PM212 Baseline** - 29.81% CAGR achieved (statistical model)
- **RevFibNotch Risk Management** - Proportional scaling integrated
- **Zero Synthetic Data** - Authentic market conditions only

## 📁  Critical File Locations

### 🏠 **Root Directory Assets**
- `CLAUDE.md` - Main project instructions and philosophy
- `README.md` - Project overview and quick start
- `COLD_START.md` - This file (quick alignment guide)

### 📚  **Documentation Hub** (`Documentation/`)
- `HISTORICAL_DATA_COMPREHENSIVE_GUIDE.md` - 20+ years data system (669 lines)
- `PROJECT_REORGANIZATION_SUMMARY.md` - Recent consolidation changes
- `REALISTIC_FILL_SIMULATION_DELIVERY_SUMMARY.md` - Execution engine summary
- `REALISTIC_FILL_SIMULATION_IMPLEMENTATION_PLAN.md` - Execution implementation
- `DUAL_STRATEGY_DOCUMENTATION_SUMMARY.md` - PM250/PM212 overview
- `PM250_Complete_20Year_Analysis_Summary.md` - Performance validation
- `ODTE.OPM.250_README.md` - PM250 system documentation

### 🧬 **PM414 Genetic Evolution** (`ODTE.Optimization/AdvancedGeneticOptimizer/`)
- `PM414_GeneticEvolution_MultiAsset.cs` - 100-mutation genetic algorithm
- `PM414_RealDataValidation.cs` - Zero tolerance data validation
- `PM414_Runner.cs` - Evolution execution engine

### ⚙️  **ODTE.Execution Centralized Engine** (`ODTE.Execution/`)
- `Engine/RealisticFillEngine.cs` - Market microstructure-aware fills
- `Models/Order.cs` - Standardized order types for all strategies
- `Interfaces/IFillEngine.cs` - Strategy-agnostic execution interface

### 🗄️ **Distributed Real Data System** (`ODTE.Historical/DistributedStorage/`)
- `DistributedDatabaseManager.cs` - Real options chain access
- `FileManager.cs` - 20+ years data organization
- `Models/OptionsChain.cs` - Authentic options data structures

### 🔧  **Testing & Validation** (`ODTE.Historical.Tests/`)
- `Program.cs` - Console testing interface
- `ValidateStooqData.cs` - Data validation tools
- `InspectDatabase.cs` - Database inspection utilities

## 🎯  Key Data Access Points

### 🗄️ **SQLite Database**
```bash
# Location
C:\code\ODTE\data\ODTE_TimeSeries_5Y.db

# Size: 850 MB
# Records: 2.5M+ across all instruments
# Coverage: Jan 2005 - July 2025 (20+ years)
# Quality Score: 89.5/100 (Excellent)
```

### 📈  **Data Validation Commands**
```bash
cd ODTE.Historical.Tests
dotnet run validate                    # Full validation suite
dotnet run inspect database.db        # Database schema inspection
dotnet run benchmark                   # Performance benchmarks
dotnet run api-demo                    # Clean API demonstration
```

### 🧬 **PM414 Evolution Commands**
```bash
cd ODTE.Optimization/AdvancedGeneticOptimizer
dotnet run                            # Run 100-mutation genetic evolution
dotnet run --validate-data           # Validate real options data only

cd ODTE.Execution  
dotnet test                           # Test centralized execution engine
dotnet run --demo                     # Demo realistic fill simulation
```

## 🏗️  **Project Architecture Quick Map**

```
ODTE/ (Current Architecture)
├── 📚 Documentation/                      # All consolidated documentation
├── 🧬 ODTE.Optimization/AdvancedGeneticOptimizer/  # PM414 genetic evolution (CURRENT FOCUS)
├── ⚙️  ODTE.Execution/                    # CENTRALIZED execution engine (ALL strategies)
├── 🗄️  ODTE.Historical/DistributedStorage/ # Real 20+ years options chains
├── 📊 ODTE.Optimization/PM212_OptionsEnhanced/  # PM212 baseline (29.81% CAGR)
├── 🔧 ODTE.Strategy/                      # Strategy implementation DLL
├── 🎮 Options.Start/                      # Blazor PWA interface
├── 📈 ODTE.Backtest/                      # Backtesting framework
├── 🧪 ODTE.Historical.Tests/              # Data validation & testing
├── 🔍 audit/                              # PM212 institutional compliance
└── 💾 data/                               # Distributed SQLite databases
```

## 🚀  **Quick Start Workflows**

### 🧬 **PM414 Genetic Evolution Discovery**
```bash
# Run PM414 genetic evolution with real data validation
cd ODTE.Optimization/AdvancedGeneticOptimizer && dotnet run

# Validate real options chain data (zero tolerance for synthetic)
cd ODTE.Optimization/AdvancedGeneticOptimizer && dotnet run --validate-data

# Test centralized execution engine
cd ODTE.Execution && dotnet test
```

### ⚙️  **Centralized Execution Discovery**  
```bash
# Demo realistic fill simulation (strategy-agnostic)
cd ODTE.Execution && dotnet run --demo

# Validate PM212 baseline performance (29.81% CAGR)
cd ODTE.Optimization/PM212_OptionsEnhanced && dotnet run

# Test distributed options chain access
cd ODTE.Historical.Tests && dotnet run api-demo
```

### 📊  **Historical Data Access**
```bash
# Demo clean data acquisition APIs
cd ODTE.Historical.Tests && dotnet run api-demo

# Test all data providers
cd ODTE.Historical.Tests && dotnet run providers

# Test multi-instrument support
cd ODTE.Historical.Tests && dotnet run instruments
```

## 📖 **Documentation Categories**

### 🧬 **Current Strategy Documentation**
- **PM414 Genetic Evolution**: 100-mutation algorithm targeting >29.81% CAGR
- **PM212 Baseline**: 29.81% CAGR achieved (statistical model validation)
- **RevFibNotch**: Proportional risk management ([1250,800,500,300,200,100])
- **Multi-Asset Signals**: Futures, gold, bonds, oil correlation integration
- **Probing vs Punching**: Adaptive lane strategy framework

### ⚙️  **Centralized Execution Documentation**
- **ODTE.Execution**: Strategy-agnostic engine for ALL models
- **RealisticFillEngine**: Market microstructure-aware execution
- **Order Management**: Standardized order types and fill simulation
- **Zero Custom Logic**: Eliminates model-specific execution inconsistencies

### 🗄️ **Distributed Data Documentation**
- **20+ Years Coverage**: Jan 2005 - July 2025 real options chains
- **DistributedDatabaseManager**: Authentic market data access
- **Zero Synthetic Data**: Real bid/ask/volume/Greeks only
- **Crisis Coverage**: 2008 Financial, 2020 COVID, 2022 Bear Market

## ⚡  **Emergency Commands**

### 🔍  **Quick Health Check**
```bash
# Database health
cd ODTE.Historical.Tests && dotnet run validate --mode health

# Strategy performance
cd ODTE.Strategy.Tests && dotnet test --filter "PM250_SystematicLossAnalysis"

# Data provider status
cd ODTE.Historical.Tests && dotnet run providers
```

### 📊  **Quick Data Access**
```bash
# Get recent market data
cd ODTE.Historical.Tests && dotnet run api-demo

# Validate specific symbol
cd ODTE.Historical.Tests && dotnet run validate SPY

# Performance benchmark
cd ODTE.Historical.Tests && dotnet run benchmark
```

## 📈  **Current Status (August 2025)**

### ✅  **Completed Systems (August 2025)**
- **PM414 Genetic Evolution**: 100-mutation system with 250+ parameters
- **ODTE.Execution**: Centralized, strategy-agnostic execution engine
- **DistributedDatabaseManager**: 20+ years real options chain access
- **PM212 Baseline**: 29.81% CAGR validation (statistical model)
- **RevFibNotch**: Integrated proportional risk management
- **Real Data Validation**: Zero tolerance for synthetic data

### 🔄  **Current Focus (August 2025)**
- **PM414 Optimization**: Target >29.81% CAGR with real options execution
- **Architecture Consolidation**: Eliminate all model-specific execution logic
- **Performance Validation**: PM414 vs PM212 baseline comparison
- **Multi-Asset Integration**: Futures/gold/bonds/oil correlation signals

## 🎯  **Key Performance Metrics**

- **PM212 Baseline**: 29.81% CAGR, 100% win rate (statistical model)
- **PM414 Target**: >29.81% CAGR with real options execution  
- **Data Coverage**: 20+ years real options chains (2005-2025)
- **Execution Engine**: Strategy-agnostic, market microstructure-aware
- **Architecture**: Centralized execution, distributed real data
- **Validation**: Zero tolerance for synthetic data

---

**Last Updated**: August 17, 2025  
**Version**: 2.0 - PM414 Genetic Evolution + Centralized Execution Architecture  
**Purpose**: Instant Claude Code alignment with current ODTE system