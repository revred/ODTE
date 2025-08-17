# 🚀  ODTE Cold Start Guide for Claude Code

## 📋 Quick Context Alignment

**ODTE** is a genetic strategy evolution platform for 0DTE options trading, featuring:
- **PM250** (Profit Maximization) + **PM212** (Capital Preservation) dual strategies
- **20+ years historical data** (Jan 2005 - July 2025) in SQLite
- **RevFibNotch risk management** with proportional scaling
- **Multi-source data fetching** with automatic failover
- **Genetic optimization** with 64 elite configurations (GAP01-GAP64)

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

### 🧬 **Strategy System** (`Options.OPM/Options.PM250/`)
- `README.md` - PM250 strategy documentation
- `QUICK_START.md` - PM250 quick access guide

### 📊  **Historical Data System** (`ODTE.Historical/`)
- `README.md` - Library documentation (284 lines)
- `SqliteMarketDataSchema.sql` - Complete database schema
- `StooqDataValidator.cs` - Data quality validation
- `DataProviders/README.md` - Multi-source fetching system

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

### 🧬 **Strategy Testing Commands**
```bash
cd Options.OPM/Options.PM250/tests
dotnet test                           # PM250 test suite

cd ODTE.Strategy.Tests
dotnet test --filter "PM250*"        # PM250 validation tests
dotnet test --filter "RevFibNotch*"  # Risk management tests
```

## 🏗️  **Project Architecture Quick Map**

```
ODTE/
├── 📚 Documentation/           # All consolidated documentation
├── 🧬 Options.OPM/            # PM250/PM212 strategy tools
├── 📊 ODTE.Historical/        # Historical data management
├── 🔧 ODTE.Strategy/          # Strategy implementation
├── ⚡ ODTE.Execution/         # Realistic fill simulation
├── 🧪 ODTE.Optimization/      # Genetic algorithms
├── 🎮 Options.Start/          # Blazor PWA interface
├── 📈 ODTE.Backtest/          # Backtesting engine
├── 🎭 ODTE.Syntricks/         # Synthetic stress testing
├── 🔍 audit/                  # PM212 institutional compliance
└── 💾 data/                   # Historical database + staging
```

## 🚀  **Quick Start Workflows**

### 🔍  **Data System Discovery**
```bash
# Understand data capabilities
cd ODTE.Historical.Tests && dotnet run api-demo

# Validate data quality
cd ODTE.Historical.Tests && dotnet run validate

# Inspect database schema
cd ODTE.Historical.Tests && dotnet run inspect
```

### 🧬 **Strategy System Discovery**
```bash
# PM250 strategy overview
cat Options.OPM/Options.PM250/README.md

# Run comprehensive tests
cd ODTE.Strategy.Tests && dotnet test

# View performance analysis
cat Documentation/PM250_Complete_20Year_Analysis_Summary.md
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

### 🎯  **Strategy Documentation**
- **PM250 System**: Profit maximization with genetic optimization
- **PM212 System**: Capital preservation with institutional audit compliance
- **RevFibNotch**: Proportional risk management ([1250,800,500,300,200,100])
- **Genetic Algorithms**: GAP01-GAP64 elite configurations

### 📊  **Data Documentation**
- **20+ Years Coverage**: Jan 2005 - July 2025 comprehensive dataset
- **Multi-Source Fetching**: Stooq, Polygon.io, Alpha Vantage, Twelve Data
- **SQLite Schema**: Complete table documentation with relationships
- **Quality Validation**: 89.5/100 excellence rating with monitoring

### 🔧  **Implementation Documentation**
- **Realistic Fill Simulation**: Market microstructure modeling
- **Project Reorganization**: Recent consolidation changes
- **Testing Framework**: Comprehensive validation procedures
- **Integration Examples**: PM250/PM212 usage patterns

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

### ✅  **Completed Systems**
- **Historical Data**: 20+ years, 2.5M+ records, 89.5% quality
- **PM250 Strategy**: Genetic optimization, 64 elite configurations
- **PM212 Strategy**: Institutional audit compliance
- **RevFibNotch**: Proportional risk management complete
- **Realistic Execution**: Market microstructure simulation
- **Documentation**: Consolidated structure with cold start

### 🔄  **Active Development**
- **Paper Trading**: Broker integration ready
- **Real-time Monitoring**: Strategy performance tracking
- **ML Enhancement**: Loss pattern recognition
- **Strategy Evolution**: Continuous optimization loop

## 🎯  **Key Performance Metrics**

- **Data Quality Score**: 89.5/100 (Excellent)
- **Query Performance**: <100ms for most operations
- **Test Coverage**: 91.3% pass rate (46 tests)
- **Historical Coverage**: 20+ years (7,570+ trading days)
- **Strategy Performance**: Validated across multiple market regimes

---

**Last Updated**: August 17, 2025  
**Version**: 1.0  
**Purpose**: Instant Claude Code alignment with ODTE project structure