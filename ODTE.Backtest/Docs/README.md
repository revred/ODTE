# ODTE Backtest Engine - Dual-Strategy Framework

A component-based backtesting engine for 0DTE/1DTE options strategies on SPX/XSP, built with .NET 9. Supports the ODTE dual-strategy framework with PM250 (profit maximization) and PM212 (capital preservation) strategies.

## Overview

This engine simulates short premium strategies (iron condors, credit spreads) on daily-expiry index options with:
- **Dual-Strategy Support**: PM250 (profit focus) and PM212 (preservation focus) strategies
- **Realistic Execution**: Integration with ODTE.Execution for institutional-grade fill simulation
- **Track B (Prototype)**: Synthetic options from SPX/ES + VIX/VIX9D proxies
- **Track A (Pro-grade)**: Extensible adapters for ORATS/LiveVol/dxFeed data
- Component-based architecture following ConvertStar patterns
- YAML configuration for strategy parameters
- CSV output for trade analysis with audit compliance

## Quick Start

```bash
# Build the project
dotnet build

# Run with default settings
dotnet run

# Run with custom config
dotnet run custom-config.yaml
```

## Architecture

### Core Components
- **Config**: Strongly-typed YAML configuration
- **Core**: Domain types, option math, utilities
- **Data**: Market data interfaces and CSV adapters
- **Signals**: Regime scoring (OR/VWAP/ATR) with dual-strategy detection
- **Strategy**: PM250/PM212 spread construction logic
- **Engine**: Execution with ODTE.Execution integration, risk management, backtesting
- **Reporting**: Trade summaries and CSV exports with audit trail

### Data Flow
1. Market data feeds into regime scorer
2. Scorer determines PM250 vs PM212 strategy selection
3. Strategy builder constructs risk-defined spreads
4. ODTE.Execution engine simulates realistic fills with market microstructure
5. Risk manager enforces RevFib guardrails and portfolio limits
6. Backtester orchestrates the simulation with audit compliance tracking

## Configuration

Key parameters in `appsettings.yaml`:

```yaml
underlying: XSP            # Mini-SPX (1/10th size)
mode: prototype           # prototype | pro
cadence_seconds: 900      # Decision frequency

short_delta:
  condor_min: 0.07       # Delta bands for strikes
  single_min: 0.10

stops:
  credit_multiple: 2.2   # Exit at 2.2x credit
  delta_breach: 0.33     # Exit if delta > 33

risk:
  daily_loss_stop: 500   # Daily hard stop
  per_trade_max_loss_cap: 200
```

## Sample Data Format

### bars_spx_min.csv
```csv
ts,o,h,l,c,v
2024-02-01 09:30:00,4950.25,4952.50,4949.75,4951.00,125000
```

### vix_daily.csv
```csv
date,vix
2024-02-01,14.25
```

### calendar.csv
```csv
ts,kind
2024-02-01 08:30:00,NFP
2024-02-14 14:00:00,FOMC
```

## Integration with ODTE Platform

### Dual-Strategy Framework Integration

```csharp
// Initialize dual-strategy backtester
public class DualStrategyBacktester : BacktestEngine
{
    private readonly IFillEngine _fillEngine;
    private readonly PM250Strategy _pm250Strategy;
    private readonly PM212Strategy _pm212Strategy;
    
    public DualStrategyBacktester(IFillEngine fillEngine)
    {
        _fillEngine = fillEngine;
        _pm250Strategy = new PM250Strategy(fillEngine);
        _pm212Strategy = new PM212Strategy(fillEngine);
    }
    
    public async Task<BacktestResult> RunDualStrategyBacktestAsync(
        DateTime startDate, DateTime endDate)
    {
        var results = new List<TradeResult>();
        
        foreach (var tradingDay in GetTradingDays(startDate, endDate))
        {
            var marketConditions = GetMarketConditions(tradingDay);
            var strategy = SelectStrategy(marketConditions); // PM250 or PM212
            
            var signals = strategy.GenerateSignals(tradingDay);
            var trades = await ExecuteTradesAsync(signals, tradingDay);
            results.AddRange(trades);
        }
        
        return new BacktestResult(results);
    }
}
```

### Realistic Execution Integration

```csharp
// Backtest with realistic fills
public class RealisticBacktestEngine : BacktestEngine
{
    private readonly IFillEngine _fillEngine;
    
    protected override async Task<FillResult> ExecuteOrderAsync(Order order, Quote quote)
    {
        var marketState = GetCurrentMarketState();
        var profile = GetExecutionProfile(); // Conservative/Base/Optimistic
        
        return await _fillEngine.SimulateFillAsync(order, quote, profile, marketState);
    }
}
```

### Adding Real Options Data

Implement `IOptionsData` for your vendor:

```csharp
public class OratsOptionsData : IOptionsData
{
    public IEnumerable<OptionQuote> GetQuotesAt(DateTime ts)
    {
        // Query ORATS API for option chain
        // Return quotes with actual Greeks and microstructure data
    }
}
```

### Custom Regime Detection

Extend `RegimeScorer` for PM250/PM212 selection:

```csharp
public class DualStrategyScorer : RegimeScorer
{
    public override StrategyType SelectStrategy(MarketConditions conditions)
    {
        var vixLevel = conditions.VIX;
        var trendStrength = CalculateTrendStrength(conditions);
        
        // PM212 for crisis/volatile periods
        if (vixLevel > 21 || conditions.IsEventRisk)
            return StrategyType.PM212;
            
        // PM250 for optimal conditions
        if (vixLevel < 19 && trendStrength < 0.3)
            return StrategyType.PM250;
            
        // Default to preservation in uncertain conditions
        return StrategyType.PM212;
    }
}
```

## Performance Metrics

The reporter outputs:
- Win rate, average win/loss (per strategy: PM250/PM212)
- Sharpe ratio (annualized) with dual-strategy comparison
- Maximum drawdown and RevFib guardrail effectiveness
- Trade distribution by type and strategy
- Execution quality metrics (NBBO compliance, slippage)
- Detailed CSV with all trades and audit compliance data

## Risk Warnings

- This is a backtesting framework, not live trading software
- Past performance doesn't guarantee future results
- Always validate with out-of-sample data
- Consider transaction costs and market impact
- 0DTE options carry significant gamma risk

## References

- [SPX/SPXW Specifications](https://www.cboe.com/tradable_products/sp_500/spx_options/specifications/)
- [XSP Mini-SPX Options](https://www.cboe.com/tradable_products/sp_500/mini_spx_options/)
- [Black-Scholes Model](https://en.wikipedia.org/wiki/Black%E2%80%93Scholes_model)
- [ORATS Data](https://orats.com/one-minute-data)
- [Cboe DataShop](https://datashop.cboe.com/)

## License

This is educational software for research purposes only.