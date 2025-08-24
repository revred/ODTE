# 🏆 Unified Strategy Model Catalog

## 📋 Purpose
Complete catalog of all strategy models compatible with the unified ODTE backtest execution system. This document provides a comprehensive overview of each model's capabilities, performance targets, and execution requirements.

## ⚡ Quick Execution Reference
All models can be executed with the same unified command:
```bash
cd ODTE.Backtest && dotnet run "../ODTE.Configurations/Models/[ModelName]_v[Version]_config.yaml"
```

## 🎯 Strategy Model Directory

### ✅ **Unified-Compatible Models** (5 Total)

---

#### 1. **SPX30DTE** - Multi-Component Advanced Strategy
- **Model File**: `ODTE.Backtest\Strategy\SPX30DTEStrategyModel.cs`
- **Config File**: `ODTE.Configurations\Models\SPX30DTE_v1.0_config.yaml`
- **Strategy Type**: Multi-layer comprehensive strategy
- **Components**:
  - 🦋 **Broken Wing Butterflies** (SPX 30DTE)
  - 🔍 **Probe System** (XSP 15DTE for confirmation)
  - 🛡️ **VIX Hedging** (50DTE spreads for protection)
  - 📊 **Rev Fib Notch Scaling** (Dynamic position sizing)
- **Genetic Algorithm**: 16-mutation tournament system
- **Expected Performance**: 
  - CAGR: >29.81% (PM212 baseline)
  - Max Drawdown: <25%
  - Win Rate: 60%+
- **Risk Profile**: Moderate with multi-layer protection
- **Execution Status**: ✅ **WORKING**

---

#### 2. **OILY212** - Oil CDTE Genetic Optimized
- **Model File**: `ODTE.Backtest\Strategy\OILY212StrategyModel.cs`
- **Config File**: `ODTE.Configurations\Models\OILY212_v1.0_config.yaml`
- **Strategy Type**: Oil weekly options CDTE
- **Genetic Algorithm**: Advanced multi-objective optimization
- **Performance Targets**:
  - CAGR: **37.8%**
  - Win Rate: **73.4%**
  - Max Drawdown: **-19.2%**
  - Sharpe Ratio: **1.87**
- **Key Parameters**:
  - Entry: Monday + 0.2 offset @ 10:07 AM
  - Short Delta: 0.087 (ultra-conservative)
  - Long Delta: 0.043 (maximum efficiency)
  - Spread Width: 1.31 (liquidity optimized)
- **Underlying**: Oil ETF (USO) / SPX for backtesting
- **Risk Management**: Genetic algorithm optimized levels
- **Execution Status**: ✅ **WORKING**

---

#### 3. **PM250** - High-Frequency Optimal Strategy
- **Model File**: `ODTE.Backtest\Strategy\PM250StrategyModel.cs`
- **Config File**: `ODTE.Configurations\Models\PM250_v1.0_config.yaml`
- **Strategy Type**: High-frequency iron condor
- **Frequency**: Up to 250 trades/week (50 trades/day)
- **Key Features**:
  - 6-minute minimum separation between trades
  - GoScore optimization (75+ threshold)
  - Smart anti-risk pre-screening
  - Rev Fib risk management [1250, 800, 500, 300, 200, 100]
- **Performance Targets**:
  - Win Rate: >90%
  - Expected Edge: 96%
  - Target Profit/Trade: $25
  - Max Daily Drawdown: $75
- **Trading Hours**: 9-11 AM, 1-3 PM (high-volume periods)
- **Risk Profile**: High frequency, moderate individual risk
- **Execution Status**: ✅ **WORKING**

---

#### 4. **PM414** - Multi-Asset Genetic Evolution
- **Model File**: `ODTE.Backtest\Strategy\PM414StrategyModel.cs`
- **Config File**: `ODTE.Configurations\Models\PM414_v1.0_config.yaml`
- **Strategy Type**: Multi-asset genetic evolution
- **Genetic Algorithm**: 100-mutation evolution system
- **Multi-Asset Signals**:
  - Futures: 30% weight (ES correlation)
  - Gold: 20% weight (inverse equity correlation)
  - Bonds: 20% weight (safe haven signals)
  - Oil: 10% weight (commodity correlation)
  - SPX: 20% weight (primary underlying)
- **Performance Target**: >29.81% CAGR (beat PM212 baseline)
- **Risk Management**: 
  - Rev Fib levels: [1250, 800, 500, 300, 200, 100]
  - Max risk per trade: $500
  - VIX threshold: 30
- **Innovation**: First multi-asset correlation strategy
- **Execution Status**: ✅ **WORKING**

---

#### 5. **PM212** - Statistical Baseline Model
- **Model File**: `ODTE.Backtest\Strategy\PM212StrategyModel.cs`
- **Config File**: `ODTE.Configurations\Models\PM212_v1.0_config.yaml`
- **Strategy Type**: Conservative iron condor baseline
- **Role**: **PERFORMANCE BENCHMARK** for all other models
- **Established Performance**: **29.81% CAGR** (20-year validated)
- **Configuration**:
  - Target DTE: 45 days
  - Short Put/Call Delta: 0.16 (16 delta)
  - Credit Target: 20%
  - Max Trades/Week: 5 (very conservative)
- **Risk Management**:
  - Rev Fib levels: [2000, 1500, 1000, 800, 500, 300] (higher than others)
  - Conservative trading hours: 10 AM - 2 PM only
  - Entry timing: Mid-month only (days 15-20)
- **Quality Control**: All new models must exceed PM212 performance
- **Execution Status**: ✅ **WORKING**

---

## 📊 **Performance Comparison Matrix**

| Model | CAGR Target | Win Rate | Max Drawdown | Risk Level | Strategy Type | Genetic Algorithm |
|-------|-------------|----------|--------------|------------|---------------|-------------------|
| **OILY212** | **37.8%** | **73.4%** | **-19.2%** | Medium | Oil CDTE | ✅ Advanced Multi-Objective |
| **SPX30DTE** | >29.81% | 60%+ | <25% | Medium | Multi-Component | ✅ 16-Mutation Tournament |
| **PM414** | >29.81% | 65%+ | <25% | Medium | Multi-Asset | ✅ 100-Mutation Evolution |
| **PM250** | 25-40% | >90% | -15% | High-Freq | Iron Condor | ✅ Rev Fib Optimization |
| **PM212** | **29.81%** | 70%+ | <30% | Conservative | Iron Condor Baseline | ❌ Statistical Model |

## 🏗️ **Unified Architecture Benefits**

### **Single Command Execution**
All models execute through the same interface:
```bash
# Execute any model
dotnet run "../ODTE.Configurations/Models/[ModelName]_v[Version]_config.yaml"

# Examples:
dotnet run "../ODTE.Configurations/Models/OILY212_v1.0_config.yaml"
dotnet run "../ODTE.Configurations/Models/PM250_v1.0_config.yaml"
dotnet run "../ODTE.Configurations/Models/PM414_v1.0_config.yaml"
```

### **Centralized Execution Engine**
- **Strategy-Agnostic**: `ODTE.Execution.RealisticFillEngine` handles ALL models
- **No Custom Logic**: Eliminates model-specific execution inconsistencies
- **Consistent Metrics**: All models report identical performance metrics
- **Audit Trail**: Complete git commit traceability for every run

### **Configuration-Driven**
- **YAML-Only Changes**: No code modifications needed to run backtests
- **Parameter Traceability**: All genetic algorithm optimizations documented
- **Version Control**: Complete model versioning and rollback capabilities

## 🔄 **Execution Pipeline**

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   YAML Config   │───▶│ StrategyFactory  │───▶│  IStrategyModel │
│   Parameters    │    │   Dynamic Load   │    │  Implementation │
└─────────────────┘    └──────────────────┘    └─────────────────┘
                                                          │
┌─────────────────┐    ┌──────────────────┐              ▼
│ Performance     │◀───│ ODTE.Execution   │    ┌─────────────────┐
│ Report + Git    │    │ RealisticFill    │◀───│ Signal          │
│ Traceability    │    │ Engine           │    │ Generation      │
└─────────────────┘    └──────────────────┘    └─────────────────┘
```

## ⚠️ **Models Requiring Unification**

### **Medium Priority** (Not Yet Unified):
1. **CDTE Weekly Engine** - `CDTE.Strategy\CDTE\CDTEStrategy.cs`
   - Status: Uses old IStrategy interface
   - Action: Needs migration to IStrategyModel

2. **OilCDTE Original** - `ODTE.Strategy\CDTE.Oil\OilCDTEStrategy.cs`
   - Status: Pre-genetic optimization version
   - Note: OILY212 is the evolved version

### **Research/Legacy Models** (Archive Status):
- PM250 Variants (20+ genetic optimizations in Archive/)
- Black Swan Strategy (Crisis handling)
- Volatility Expansion Strategy

## 🎯 **Quality Standards**

### **VALID Model Criteria**
- ✅ Implements `IStrategyModel` interface
- ✅ Has corresponding YAML configuration
- ✅ Successfully executes through unified system
- ✅ Generates signals only (no execution logic)
- ✅ Includes genetic algorithm traceability
- ✅ Complete parameter documentation

### **Performance Validation**
- ✅ Must run without compilation errors
- ✅ Must generate expected signal patterns
- ✅ Must respect risk management rules
- ✅ Must produce auditable results
- ✅ Must include git commit traceability

## 🚀 **Usage Examples**

### **Run Complete Model Suite**
```bash
# Test all models sequentially
models=("SPX30DTE" "OILY212" "PM250" "PM414" "PM212")
for model in "${models[@]}"; do
  echo "Testing $model..."
  dotnet run "../ODTE.Configurations/Models/${model}_v1.0_config.yaml"
done
```

### **Performance Comparison Run**
```bash
# Run baseline vs advanced models
echo "=== PM212 Baseline ==="
dotnet run "../ODTE.Configurations/Models/PM212_v1.0_config.yaml"

echo "=== OILY212 Advanced ==="
dotnet run "../ODTE.Configurations/Models/OILY212_v1.0_config.yaml"

echo "=== PM414 Multi-Asset ==="
dotnet run "../ODTE.Configurations/Models/PM414_v1.0_config.yaml"
```

## 📈 **Model Selection Guide**

### **Choose SPX30DTE** when:
- Need multi-component strategy
- Want probe system confirmation
- Require VIX hedging protection
- Long-term conservative growth focus

### **Choose OILY212** when:
- Want highest CAGR target (37.8%)
- Oil/commodity correlation exposure desired
- Advanced genetic optimization proven performance
- Accept moderate drawdown for higher returns

### **Choose PM250** when:
- High-frequency trading capability needed
- Want maximum trade volume (250/week)
- Short-term profit generation focus
- Can monitor positions actively

### **Choose PM414** when:
- Multi-asset correlation signals desired
- Want cutting-edge genetic evolution
- Need futures/gold/bonds diversification
- Long-term systematic approach preferred

### **Choose PM212** when:
- Need established performance baseline
- Want conservative, proven approach
- Require statistical validation reference
- Building comparative performance analysis

---

## 🏆 **Success Metrics Achieved**

### **Unified System Validation**
- ✅ **5 Models** successfully unified and tested
- ✅ **Single Command** execution for all models  
- ✅ **Centralized Engine** (ODTE.Execution) handles all fills
- ✅ **Configuration-Driven** approach (YAML-only changes)
- ✅ **Git Traceability** for complete reproducibility
- ✅ **Automatic Reporting** with performance documentation
- ✅ **Registry Tracking** for all backtest executions

### **Architectural Victory**
The unified system proves the core principle: **"If it can't run with just a YAML file change, the model isn't properly unified."**

All 5 models now meet this gold standard, demonstrating complete separation of:
- **Strategy Logic** (signal generation) 
- **Execution Engine** (fill simulation)
- **Configuration Management** (YAML parameters)
- **Performance Reporting** (automated documentation)

---

**Catalog Version**: 1.0  
**Last Updated**: 2025-08-24  
**Total Unified Models**: 5  
**Total Tested Successfully**: 5  
**System Status**: 🏆 **UNIFIED ARCHITECTURE COMPLETE**