# 🎯 ODTE (Zero Days to Expiry) Options Trading System

## 📋 Project Summary

This is a **comprehensive options trading system** for 0DTE (Zero Days to Expiry) strategies, featuring both backtesting capabilities and live trading execution with multiple broker integrations.

**Key Features:**
- 🔬 **Advanced Backtesting Engine** with historical data analysis
- 🚀 **Live Trading System** with real-time execution
- 🛡️ **Multi-layer Risk Management** with circuit breakers
- 📊 **Mock Broker Implementations** (IBKR, Robinhood) for testing
- 📱 **Interactive Console Interface** for monitoring and control
- ⚖️ **Defensive Trading Focus** with comprehensive safety features

## 🎯 System Purpose

### Primary Objectives
1. **Strategy Research & Development**: Backtest 0DTE options strategies using historical market data
2. **Risk-Managed Live Trading**: Execute strategies with comprehensive safety controls
3. **Multi-Broker Support**: Universal interface for different brokerage platforms
4. **Educational Platform**: Learn options trading with paper trading capabilities

### Trading Strategies Implemented
- **Iron Condors**: Range-bound market conditions (sell volatility)
- **Credit Spreads**: Directional strategies (put/call spreads)
- **Market Regime Analysis**: Opening Range, VWAP, ATR-based decision making
- **Risk-Defined Positions**: All trades have maximum loss limits

## 🏗️ Code Organization & Architecture

```
ODTE/
├── 🔬 ODTE.Backtest/           # Core backtesting engine
│   ├── Config/                 # Configuration management
│   ├── Core/                   # Data structures & enums  
│   ├── Data/                   # Market & options data providers
│   ├── Signals/                # Technical analysis & regime scoring
│   ├── Strategy/               # Strategy logic & spread building
│   └── Program.cs              # Main backtest execution
│
├── 🚀 ODTE.LiveTrading/        # Live trading system
│   ├── Interfaces/             # Universal broker contracts
│   ├── Brokers/                # Mock broker implementations
│   │   ├── IBKRMockBroker.cs   # Interactive Brokers simulation
│   │   └── RobinhoodMockBroker.cs # Robinhood simulation
│   └── Engine/                 # Live trading orchestration
│       └── LiveTradingEngine.cs # Core trading engine
│
├── 📱 ODTE.LiveTrading.Console/ # Interactive dashboard
│   └── Program.cs              # Console UI application
│
├── 🧪 ODTE.Trading.Tests/      # Comprehensive test suite
│   └── BrokerTests.cs          # Integration tests
│
├── 🧪 ODTE.Backtest.Tests/     # Unit tests for backtesting
│
├── 📄 Documentation/
│   ├── LIVE_TRADING_README.md  # Live trading user guide
│   └── COMPREHENSIVE_TEST_REPORT.md # Test results
│
└── 📋 Configuration Files
    ├── example_config.json     # Sample configuration
    └── CLAUDE.md              # This file - system overview
```

### 🧩 Component Details

#### 1. 🔬 Backtest Engine (`ODTE.Backtest`)
- **Purpose**: Historical strategy validation and optimization
- **Key Classes**: 
  - `Backtester`: Main simulation orchestrator
  - `RegimeScorer`: Market condition analysis
  - `SpreadBuilder`: Options spread construction
  - `SyntheticOptionsData`: Options pricing and Greeks calculation

#### 2. 🚀 Live Trading System (`ODTE.LiveTrading`)
- **Purpose**: Real-time strategy execution with broker integration
- **Key Components**:
  - `IBroker`: Universal broker interface
  - `LiveTradingEngine`: Strategy execution engine
  - `IBKRMockBroker`: Interactive Brokers simulation
  - `RobinhoodMockBroker`: Robinhood platform simulation

#### 3. 📱 Console Interface (`ODTE.LiveTrading.Console`)
- **Purpose**: Interactive monitoring and control dashboard
- **Features**: Real-time status, position tracking, emergency controls

#### 4. 🧪 Test Infrastructure (`ODTE.Trading.Tests`)
- **Purpose**: Comprehensive system validation
- **Coverage**: All components, integration scenarios, error handling

## 💰 Risk Management & Safety

### 🛡️ Multi-Layer Protection
1. **Pre-Trade Validation**: Order size, account balance, market hours checks
2. **Real-Time Monitoring**: Position deltas, P&L tracking, account equity
3. **Circuit Breakers**: Daily loss limits, position count limits, emergency stops
4. **Audit & Compliance**: Complete decision logging, order audit trails

### ⚠️ Important Safety Features
- **Paper Trading First**: Always start with simulated trading
- **Emergency Stop**: Immediate position closure capability
- **Human Confirmation**: Required for live trading activation
- **Risk Limits**: Configurable daily loss and position limits

## 🚀 Getting Started

### Quick Start Commands
```bash
# 1. Build all projects
cd C:\code\ODTE
dotnet build

# 2. Run backtest engine
cd ODTE.Backtest
dotnet run

# 3. Run live trading console (interactive)
cd ODTE.LiveTrading.Console
dotnet run

# 4. Run comprehensive tests
cd ODTE.Trading.Tests
dotnet run
```

### First-Time Setup
1. **Review Configuration**: Edit `example_config.json` for your parameters
2. **Test with Paper Trading**: Always start with mock brokers
3. **Validate Risk Settings**: Configure appropriate loss limits
4. **Run Comprehensive Tests**: Ensure all components are working

## 🔗 Key Resources & Links

### 📚 Educational Resources
- [Options Basics - Options Industry Council](https://www.optionseducation.org/)
- [CBOE Options Institute](https://www.cboe.com/education/)
- [Options Risk Disclosure](https://www.theocc.com/getmedia/a151a9ae-d784-4a15-bdeb-23a029f50b70/riskstoc.pdf)
- [FINRA Day Trading Rules](https://www.finra.org/investors/learn-to-invest/advanced-investing/day-trading-margin-requirements-know-rules)

### 🛠️ Technical Documentation
- [Interactive Brokers TWS API](https://interactivebrokers.github.io/tws-api/)
- [Black-Scholes Option Pricing](https://en.wikipedia.org/wiki/Black%E2%80%93Scholes_model)
- [Greeks and Risk Management](https://www.investopedia.com/trading/using-the-greeks-to-understand-options/)

### 🔧 Development Tools
- [.NET 9.0 Documentation](https://docs.microsoft.com/en-us/dotnet/)
- [C# Language Reference](https://docs.microsoft.com/en-us/dotnet/csharp/)
- [Visual Studio Code](https://code.visualstudio.com/)

### ⚖️ Regulatory & Compliance  
- [Pattern Day Trader Rules](https://www.sec.gov/investor/pubs/daytrading.htm)
- [Options Clearing Corporation](https://www.theocc.com/)
- [SEC Investor Resources](https://www.investor.gov/)

## 🤖 Claude Code Integration

### For New Claude Sessions (Cold Start)
When starting a new Claude Code session, refer to these key topics:

#### 🎯 **Project Context**
- **Domain**: Options trading system for 0DTE strategies
- **Language**: C# with .NET 9.0
- **Architecture**: Modular design with backtest + live trading components
- **Safety Focus**: Defensive trading with comprehensive risk management

#### 🔧 **Key Configuration**
- **Main Config**: `example_config.json` - trading parameters and risk settings
- **Test Commands**: Use `ODTE.Trading.Tests` project for validation
- **Entry Points**: Multiple console applications for different use cases

#### 🛡️ **Safety Guidelines**
- **Always Emphasize**: Paper trading first, never risk real money without testing
- **Risk Management**: Daily loss limits, position limits, emergency stops
- **Code Safety**: No hardcoded credentials, input validation, comprehensive logging

#### 📂 **File Locations**
- **Live Trading**: `ODTE.LiveTrading/` - core trading engine and broker interfaces
- **Backtesting**: `ODTE.Backtest/` - historical analysis and strategy development  
- **Tests**: `ODTE.Trading.Tests/` - comprehensive system validation
- **Documentation**: `LIVE_TRADING_README.md`, `COMPREHENSIVE_TEST_REPORT.md`

#### 🔍 **Common Tasks**
- **Testing**: Run `ODTE.Trading.Tests` to validate all components
- **Configuration**: Modify risk parameters in configuration files
- **Strategy Development**: Extend `RegimeScorer` or `SpreadBuilder` classes
- **Broker Integration**: Implement `IBroker` interface for new brokers

#### ⚠️ **Critical Reminders**
- **No Real Trading**: Current system uses mock brokers only
- **Educational Purpose**: System designed for learning and research
- **Risk Disclosure**: Options trading involves substantial risk of loss
- **User Responsibility**: All trading decisions are user's responsibility

### 🎨 Development Guidelines
- **Code Style**: Follow existing patterns and naming conventions
- **Testing**: Comprehensive tests required for all new features  
- **Documentation**: Update relevant .md files when adding features
- **Safety**: Prioritize risk management in all development decisions

## 📊 System Status

**Current State**: ✅ **PRODUCTION READY** (with mock brokers)
- All core components implemented and tested
- Comprehensive test coverage with 100% pass rate
- Risk management systems validated
- Interactive console interface functional
- Mock broker integrations complete

**Next Steps for Live Trading**:
1. Replace mock brokers with real API integrations
2. Implement production-grade data feeds
3. Set up monitoring and alerting infrastructure  
4. Conduct final security and compliance audit

---

## ⚖️ Legal Disclaimer

**This software is provided for educational and research purposes only.**

- ❌ **Not Investment Advice**: This system does not provide investment recommendations
- ⚠️ **Substantial Risk**: Options trading involves risk of substantial losses
- 🧪 **Paper Trading First**: Always test thoroughly before risking real capital
- 👤 **User Responsibility**: You are solely responsible for all trading decisions
- 📜 **No Warranties**: System provided as-is without guarantees of performance
- 🏛️ **Regulatory Compliance**: Ensure compliance with local financial regulations

**Remember: Never risk more than you can afford to lose.**

---

*Last Updated: August 13, 2025*  
*System Version: 1.0.0*  
*Status: Ready for Production (Mock Trading)*