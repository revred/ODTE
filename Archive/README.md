# 🎯 ODTE - Zero Days to Expiry Options Trading System

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen.svg)]() 
[![.NET Version](https://img.shields.io/badge/.NET-9.0-blue.svg)]()
[![License](https://img.shields.io/badge/license-Proprietary-red.svg)]()
[![Tests](https://img.shields.io/badge/tests-100%25%20passing-brightgreen.svg)]()

> **⚠️ EDUCATIONAL USE ONLY**: This system is designed for educational and research purposes. Options trading involves substantial risk of loss. Always start with paper trading and never risk more than you can afford to lose.

## 📋 Overview

ODTE is a comprehensive **Zero Days to Expiry options trading system** that combines advanced backtesting capabilities with live trading execution. Built with C# and .NET 9.0, it features sophisticated risk management, multiple broker integrations, and defensive trading strategies.

### 🎯 Key Features

- 🔬 **Advanced Backtesting Engine** - Historical data analysis with Black-Scholes pricing
- 🚀 **Live Trading System** - Real-time execution with comprehensive safety controls
- 🛡️ **Multi-Layer Risk Management** - Daily loss limits, position limits, emergency stops
- 📊 **Mock Broker Support** - IBKR and Robinhood simulations for testing
- 📱 **Interactive Dashboard** - Real-time monitoring and control interface
- ⚖️ **Defensive Focus** - Built for risk-managed, educational trading

### 📈 Supported Strategies

- **Iron Condors** - Range-bound market conditions
- **Credit Spreads** - Directional put/call spreads  
- **Market Regime Analysis** - Opening Range, VWAP, ATR-based decisions
- **0DTE Strategies** - Same-day expiration options trading

## 🚀 Quick Start

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Windows/Linux/macOS
- Basic understanding of options trading

### Installation

```bash
# Clone the repository
git clone https://github.com/revred/ODTE.git
cd ODTE

# Build the solution
dotnet build

# Run comprehensive tests
cd ODTE.Trading.Tests
dotnet run
```

### First Run

```bash
# 1. Run backtest engine
cd ODTE.Backtest
dotnet run

# 2. Launch live trading console (paper trading)
cd ../ODTE.LiveTrading.Console
dotnet run
```

## 🏗️ Architecture

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Backtest      │    │   Live Trading   │    │    Brokers      │
│    Engine       │───▶│     Engine       │───▶│  IBKR/RH Mock   │
│                 │    │                  │    │                 │
│ • RegimeScorer  │    │ • Risk Controls  │    │ • Order Mgmt    │
│ • SpreadBuilder │    │ • Position Mon.  │    │ • Market Data   │
│ • Strategy      │    │ • Live Execution │    │ • Account Info  │
└─────────────────┘    └──────────────────┘    └─────────────────┘
```

### 📁 Project Structure

- **`ODTE.Backtest`** - Core backtesting engine with historical analysis
- **`ODTE.LiveTrading`** - Live trading system with broker integrations  
- **`ODTE.LiveTrading.Console`** - Interactive dashboard and controls
- **`ODTE.Trading.Tests`** - Comprehensive test suite
- **`ODTE.Backtest.Tests`** - Unit tests for backtesting components

## 🛡️ Safety Features

### Risk Management
- ✅ **Daily Loss Limits** - Automatic shutdown at configured loss threshold
- ✅ **Position Limits** - Maximum concurrent position enforcement
- ✅ **Emergency Stop** - Immediate position closure capability
- ✅ **Paper Trading** - Safe simulation mode for strategy testing
- ✅ **Order Validation** - Pre-submission risk checks

### Defensive Design
- ✅ **Human Confirmation** - Required for live trading activation
- ✅ **Audit Logging** - Complete decision and order tracking
- ✅ **Circuit Breakers** - Multiple automatic safety triggers
- ✅ **Real-time Monitoring** - Continuous position and account surveillance

## 📊 Usage Examples

### Backtesting

```bash
cd ODTE.Backtest
dotnet run
```

**Sample Output:**
```
🔬 ODTE Backtesting Engine
==========================
Configuration: SPY 0DTE Iron Condors
Period: 2024-01-01 to 2024-12-31
Total trades: 45
Net P&L: $2,150.00
Win rate: 78.9%
Max drawdown: $485.00
```

### Live Trading (Paper Mode)

```bash
cd ODTE.LiveTrading.Console
dotnet run
```

**Interactive Dashboard:**
```
════════════════════════════════════════
       📊 LIVE TRADING DASHBOARD
════════════════════════════════════════

🎯 Engine Status: STOPPED
💰 Account Value: $100,000.00
💵 Available Funds: $80,000.00
📈 Total P&L: $0.00
📋 Active Positions: 0
⏳ Pending Orders: 0
🔗 Broker: Connected

Commands: start, status, positions, orders, help, exit
```

### Running Tests

```bash
cd ODTE.Trading.Tests
dotnet run
```

**Test Results:**
```
🧪 ODTE Live Trading System - Component Tests

📊 Testing IBKR Mock Broker...        ✅ PASS
📱 Testing Robinhood Mock Broker...   ✅ PASS  
🚀 Testing Live Trading Engine...     ✅ PASS
📋 Testing Order Processing...        ✅ PASS
🛡️ Testing Risk Management...         ✅ PASS

Success Rate: 100.0%
🎉 ALL TESTS PASSED!
```

## ⚙️ Configuration

Edit `example_config.json` to customize trading parameters:

```json
{
  "underlying": "SPY",
  "risk": {
    "dailyLossStop": 200,
    "maxConcurrentPerSide": 1
  },
  "stops": {
    "creditMultiple": 2.0,
    "deltaBreach": 0.30
  }
}
```

### Key Parameters

- **`dailyLossStop`** - Maximum daily loss before shutdown
- **`creditMultiple`** - Stop loss as multiple of credit received
- **`deltaBreach`** - Position delta threshold for closure
- **`maxConcurrentPerSide`** - Maximum positions per strategy type

## 🤝 Contributing

We welcome contributions! Please see our [Contributing Guidelines](CONTRIBUTING.md) for details.

### Development Setup

```bash
# Fork and clone the repository
git clone https://github.com/yourusername/ODTE.git

# Create a feature branch
git checkout -b feature/your-feature-name

# Make your changes and test
dotnet build
cd ODTE.Trading.Tests && dotnet run

# Submit a pull request
```

### Reporting Issues

Please use the [GitHub Issues](https://github.com/revred/ODTE/issues) page to report bugs or request features.

## 📚 Documentation

- **[Live Trading Guide](LIVE_TRADING_README.md)** - Comprehensive usage instructions
- **[System Overview](CLAUDE.md)** - Technical architecture and design
- **[Test Report](COMPREHENSIVE_TEST_REPORT.md)** - Validation results
- **[API Documentation](docs/)** - Code reference and examples

## 🔗 Resources

### Educational
- [Options Basics - OIC](https://www.optionseducation.org/)
- [Risk Disclosure - OCC](https://www.theocc.com/about/publications/character-risks.jsp)
- [CBOE Education](https://www.cboe.com/education/)

### Technical
- [Interactive Brokers API](https://interactivebrokers.github.io/tws-api/)
- [Black-Scholes Model](https://en.wikipedia.org/wiki/Black%E2%80%93Scholes_model)
- [Options Greeks](https://www.investopedia.com/trading/using-the-greeks-to-understand-options/)

## ⚖️ Legal & Compliance

### Important Disclaimers

- 📚 **Educational Purpose Only** - This software is for learning and research
- ⚠️ **Substantial Risk** - Options trading can result in significant losses
- 🧪 **No Investment Advice** - System does not provide trading recommendations  
- 👤 **User Responsibility** - All trading decisions are solely your responsibility
- 📋 **No Warranties** - Software provided as-is without performance guarantees

### Regulatory Notes

- Ensure compliance with local financial regulations
- Understand Pattern Day Trader rules if applicable
- Review broker-specific terms and conditions
- Consider tax implications of options trading

## 📝 License

This project is proprietary software owned by revred. See the [LICENSE](LICENSE) file for complete terms and restrictions.

**Commercial Software**: This is not open source. Viewing for educational purposes only.

## 🙏 Acknowledgments

- Options pricing models based on Black-Scholes-Merton framework
- Market data structures inspired by industry standards
- Risk management practices from established trading firms
- Educational resources from CBOE and OIC

## 📞 Support

- **Documentation**: Check the `/docs` folder and `.md` files
- **Issues**: [GitHub Issues](https://github.com/revred/ODTE/issues)
- **Discussions**: [GitHub Discussions](https://github.com/revred/ODTE/discussions)

---

<div align="center">

**⚠️ Risk Warning: Options trading involves substantial risk and is not suitable for all investors. Past performance does not guarantee future results. Always start with paper trading and never invest more than you can afford to lose.**

Made with ❤️ for the options trading community

[⭐ Star this repo](https://github.com/revred/ODTE/stargazers) | [🍴 Fork it](https://github.com/revred/ODTE/fork) | [📝 Report Issues](https://github.com/revred/ODTE/issues)

</div>