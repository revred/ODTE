# 📊 ODTE Historical Data Access - Quick Start

## 🎯 What is ODTE.Historical?
**ODTE.Historical** is a production-ready, clean API for acquiring market data from multiple sources with cold start capability and zero hardcoded paths.

## ⚡ Quick Access Commands
```bash
# Navigate to Historical Data system
cd ODTE.Historical.Tests

# Run interactive data acquisition demo
dotnet run api-demo

# Test all data providers
dotnet run providers

# Access any market instrument
dotnet run instruments

# Validate data quality
dotnet run validate SPY
```

## 🏗️ What Makes It Special
✅ **Cold Start Ready** - Works immediately without setup  
✅ **Multi-Instrument** - Stocks, ETFs, commodities, forex, bonds  
✅ **Clean API** - No hardcoded paths or complex configuration  
✅ **Quality Tested** - 21/21 tests passing (100% success rate)  
✅ **Production Ready** - Used by PM250 and other trading systems  

## 🌍 Supported Instruments
```
📈 US Equities (4):     SPY ✅, QQQ ✅, IWM ✅, DIA ✅
🥇 Commodities (4):     GLD ✅, SLV ✅, USO ✅, UNG ✅  
💰 Bonds (4):           TLT ✅, IEF ✅, SHY ✅, LQD ✅
📊 Volatility (3):      VIX ✅, UVXY ✅, VXX ✅
🌍 International (4):   EFA ✅, EEM ✅, FXI ✅, EWJ ✅
💱 Forex (3):           FXE ✅, FXY ✅, UUP ✅

TOTAL TESTED: 22+ INSTRUMENTS ACROSS 6 CATEGORIES
```

## 🧪 Simple Usage Example
```csharp
// Works from cold start - no setup required
using ODTE.Historical;

using var dataManager = new HistoricalDataManager(); // No hardcoded paths
await dataManager.InitializeAsync(); // Self-configuring

// Get data for ANY instrument/commodity
var spyData = await dataManager.GetMarketDataAsync("SPY", startDate, endDate);
var goldData = await dataManager.GetMarketDataAsync("GLD", startDate, endDate);
var vixData = await dataManager.GetMarketDataAsync("VIX", startDate, endDate);
```

## 📍 System Location
**ODTE.Historical** is located at: `ODTE.Historical/`

**Key Components:**
- `ODTE.Historical/` - Main library with clean API
- `ODTE.Historical.Tests/` - Interactive testing console
- `ODTE.Historical.Tests/Program.cs` - Entry point for demos

## 🚀 Data Providers
- **Primary**: Stooq (free, reliable)
- **Backup**: Yahoo Finance
- **Failover**: Automatic provider switching
- **Rate Limited**: Respectful API usage

## 📊 Performance Metrics
```
⚡ Test Suite Performance:
   Full test suite: <520ms
   Cold start: <200ms  
   Multi-instrument (5 symbols): <300ms
   Concurrent operations (10 symbols): <500ms
   
🎯 Quality Metrics:
   Test pass rate: 100% (21/21)
   API coverage: 100%
   Cold start success: ✅
   Cross-platform: ✅
```

## 🔗 Integration Examples

### PM250 Trading System
```csharp
// PM250 uses ODTE.Historical for market data
var dataManager = new HistoricalDataManager();
await dataManager.InitializeAsync();
var marketData = await dataManager.GetMarketDataAsync("SPY", start, end);
```

### Any Trading System
```csharp
// Generic usage for portfolio data
var symbols = new[] { "SPY", "QQQ", "GLD", "TLT", "VIX" };
var portfolioData = new Dictionary<string, List<MarketDataBar>>();

foreach (var symbol in symbols)
{
    portfolioData[symbol] = await dataManager.GetMarketDataAsync(symbol, start, end);
}
```

## 📚 Complete Documentation
👉 **[ODTE.Historical README](ODTE.Historical/README.md)** - Complete technical documentation  
👉 **[Clean API Summary](ODTE.Historical/ODTE_HISTORICAL_CLEAN_API_SUMMARY.md)** - Detailed API overview  
👉 **[Interactive Tests](ODTE.Historical.Tests/README.md)** - Testing and validation guide  

## 🛠️ Available Operations
```bash
# From ODTE.Historical.Tests/
dotnet run api-demo          # Demonstrate clean APIs
dotnet run providers         # Test data providers  
dotnet run instruments       # Test multi-instrument support
dotnet run cold-start        # Test cold start capability
dotnet run stress            # Stress test concurrent operations
dotnet run --help            # Show detailed help
```

## ⚠️ Migration Notes
**Legacy fragments archived**: All old data acquisition scripts, documentation fragments, and CSV/Parquet code have been moved to `ODTE.Historical/Archive/LegacyFragments/` to maintain clean root directory.

**Current system**: Direct provider → SQLite with validation and quality checks.

## 🏆 Summary
ODTE.Historical provides **instant access to market data** with:
- ✅ **Zero setup** required
- ✅ **22+ instruments** supported  
- ✅ **Production quality** (100% test pass rate)
- ✅ **Cold start discovery** ready
- ✅ **Clean API** with no hardcoded paths

**Get started**: `cd ODTE.Historical.Tests && dotnet run api-demo`

---
*This file provides cold start discovery for ODTE Historical Data Access from the root directory*