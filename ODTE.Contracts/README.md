# 🏛️ ODTE.Contracts

**Foundation Project - Shared Interfaces and Data Models**

ODTE.Contracts is the cornerstone of the ODTE platform, providing shared interfaces and data models that eliminate circular dependencies and ensure consistency across all projects.

## 🎯 Purpose

This project serves as the **foundation layer** for the entire ODTE platform by:

- **Eliminating Circular Dependencies**: Provides shared types that all other projects can reference without creating dependency cycles
- **Ensuring Type Consistency**: Single source of truth for core data models used across the platform
- **Interface Standardization**: Defines contracts that all implementations must follow
- **Backwards Compatibility**: Maintains stable API contracts for external integrations

## 📦 Dependencies

**None** - This is the foundation project that all others depend on.

```
ODTE.Contracts (Foundation)
↑ Referenced by all other projects
```

## 🏗️ Architecture

```
ODTE.Contracts/
├── Data/                    # Core data models
│   └── MarketData.cs        # ChainSnapshot, OptionsQuote, MarketConditions, MarketRegime
├── Strategy/                # Strategy interfaces
│   └── Interfaces.cs        # IStrategy, IBacktester, IRiskManager
├── Orders/                  # Order and execution models
│   └── OrderTypes.cs        # Order, OrderLeg, OrderDirection, OptionType
├── Historical/              # Historical data interfaces
│   └── Interfaces.cs        # IMarketDataProvider, IDataValidator
└── Execution/               # Execution engine contracts
    └── Interfaces.cs        # IFillEngine, IExecutionProfile
```

## 📊 Core Data Models

### MarketData.cs
Central data models for options trading:

```csharp
public class ChainSnapshot
{
    public DateTime Date { get; set; }
    public DateTime Expiration { get; set; }
    public decimal UnderlyingPrice { get; set; }
    public decimal ImpliedVolatility { get; set; }
    public Dictionary<decimal, OptionsQuote> Calls { get; set; }
    public Dictionary<decimal, OptionsQuote> Puts { get; set; }
}

public class OptionsQuote
{
    public decimal Strike { get; set; }
    public DateTime Expiration { get; set; }
    public OptionType OptionType { get; set; }
    public decimal Bid { get; set; }
    public decimal Ask { get; set; }
    public decimal Mid => (Bid + Ask) / 2;
    // Greeks: Delta, Gamma, Theta, Vega, Rho, ImpliedVolatility
}

public class MarketConditions
{
    public DateTime Date { get; set; }
    public decimal VIX { get; set; }
    public decimal VIX9D { get; set; }
    public decimal SpotPrice { get; set; }
    public bool IsGammaHour { get; set; }
    public MarketRegime Regime { get; set; }
}

public enum MarketRegime
{
    Calm,      // Low volatility, range-bound
    Trending,  // Directional movement
    Volatile,  // High volatility
    Crisis     // Black swan events
}
```

## 🔌 Key Interfaces

### IStrategy Interface
Standard contract for all trading strategies:

```csharp
public interface IStrategy
{
    string Name { get; }
    Task<List<Order>> GenerateOrdersAsync(MarketConditions conditions, ChainSnapshot snapshot);
    Task<bool> ShouldExitAsync(MarketConditions conditions, PortfolioState portfolio);
    Task<List<Order>> GenerateExitOrdersAsync(PortfolioState portfolio, ChainSnapshot snapshot);
}
```

### IFillEngine Interface
Contract for execution engines:

```csharp
public interface IFillEngine
{
    ExecutionProfile CurrentProfile { get; }
    Task<FillResult?> SimulateFillAsync(Order order, Quote quote, ExecutionProfile profile, MarketState marketState);
    ExecutionDiagnostics GetDiagnostics(DateTime date);
}
```

## 🌊 Projects That Depend On ODTE.Contracts

1. **ODTE.Historical**: Uses `ChainSnapshot`, `OptionsQuote`, `MarketConditions`
2. **ODTE.Execution**: Implements `IFillEngine`, uses `Order` and `Quote` models
3. **ODTE.Backtest**: Uses all data models and implements `IBacktester`
4. **ODTE.Strategy**: Implements `IStrategy`, uses all market data models
5. **ODTE.Optimization**: References all interfaces for strategy optimization

## ✅ Benefits of Shared Contracts

### Before ODTE.Contracts (Circular Dependencies)
```
ODTE.Strategy ←→ ODTE.Backtest
      ↕              ↕
ODTE.Execution ←→ ODTE.Historical
```
**Result**: Build failures, duplicate types, impossible to maintain

### After ODTE.Contracts (Clean Hierarchy)
```
                ODTE.Contracts (Foundation)
                       ↑
        ┌──────────────┼──────────────┐
        │              │              │
   Historical    Execution      Backtest
        └──────────────┼──────────────┘
                       │
                  ODTE.Strategy
                       │
               ODTE.Optimization
```
**Result**: Clean builds, consistent types, maintainable architecture

## 🚀 Usage Examples

### Consuming Projects Reference Contracts
```xml
<ItemGroup>
  <ProjectReference Include="..\ODTE.Contracts\ODTE.Contracts.csproj" />
</ItemGroup>
```

### Using Shared Types
```csharp
using ODTE.Contracts.Data;
using ODTE.Contracts.Strategy;

// Consistent data models across all projects
var conditions = new MarketConditions 
{
    VIX = 18.5m,
    Regime = MarketRegime.Calm
};

// Standard interfaces
IStrategy strategy = new IronCondorStrategy();
var orders = await strategy.GenerateOrdersAsync(conditions, snapshot);
```

### Namespace Aliases for Clarity
```csharp
using ContractsData = ODTE.Contracts.Data;
using ContractsStrategy = ODTE.Contracts.Strategy;

// Avoid namespace conflicts while maintaining clarity
ContractsData.OptionsQuote quote = GetQuote();
ContractsStrategy.IStrategy strategy = GetStrategy();
```

## 🔧 Development Guidelines

### Adding New Contracts
1. **Data Models**: Add to `Data/` folder
2. **Interfaces**: Add to appropriate domain folder
3. **Breaking Changes**: Avoid - use extensions or new interfaces
4. **Documentation**: Update this README when adding new contracts

### Backwards Compatibility
- **Never remove** public properties or methods
- **Never change** method signatures in interfaces
- **Use extensions** for new functionality
- **Version interfaces** if breaking changes are required

### Testing
Since this is a contracts-only project:
- **Unit tests**: Not applicable (no implementation)
- **Integration tests**: Verified by consuming projects
- **Compilation tests**: Ensure all projects build successfully

## 🏆 Success Metrics

The success of ODTE.Contracts is measured by:

✅ **Zero Circular Dependencies**: All projects build independently  
✅ **Consistent Types**: No duplicate data model definitions  
✅ **Clean Interfaces**: Clear contracts for all major components  
✅ **Build Success**: All consuming projects compile without errors  
✅ **Type Safety**: Strong typing across the entire platform  

## 🔄 Version History

| Version | Changes | Impact |
|---------|---------|--------|
| 1.0.0 | Initial contracts extracted from circular dependencies | Foundation established |
| 1.0.1 | Added MarketRegime enum, enhanced OptionsQuote | Strategy improvements |

---

*ODTE.Contracts - The foundation that makes the entire ODTE platform possible.*