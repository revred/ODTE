# ODTE Backtest Engine

A component-based backtesting engine for 0DTE/1DTE options strategies on SPX/XSP, built with .NET 9.

## Overview

This engine simulates short premium strategies (iron condors, credit spreads) on daily-expiry index options with:
- **Track B (Prototype)**: Synthetic options from SPX/ES + VIX/VIX9D proxies
- **Track A (Pro-grade)**: Extensible adapters for ORATS/LiveVol/dxFeed data
- Component-based architecture following ConvertStar patterns
- YAML configuration for strategy parameters
- CSV output for trade analysis

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
- **Signals**: Regime scoring (OR/VWAP/ATR)
- **Strategy**: Spread construction logic
- **Engine**: Execution, risk management, backtesting
- **Reporting**: Trade summaries and CSV exports

### Data Flow
1. Market data feeds into regime scorer
2. Scorer outputs Go/No-Go decisions
3. Builder constructs risk-defined spreads
4. Execution engine models fills with slippage
5. Risk manager enforces portfolio limits
6. Backtester orchestrates the simulation

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

## Extending for Production

### Adding Real Options Data

Implement `IOptionsData` for your vendor:

```csharp
public class OratsOptionsData : IOptionsData
{
    public IEnumerable<OptionQuote> GetQuotesAt(DateTime ts)
    {
        // Query ORATS API for option chain
        // Return quotes with actual Greeks
    }
}
```

### Custom Signals

Extend `RegimeScorer` with additional indicators:

```csharp
public class CustomScorer : RegimeScorer
{
    public override (int score, ...) Score(...)
    {
        var baseScore = base.Score(...);
        // Add custom signals
        return AdjustScore(baseScore);
    }
}
```

## Performance Metrics

The reporter outputs:
- Win rate, average win/loss
- Sharpe ratio (annualized)
- Maximum drawdown
- Trade distribution by type
- Detailed CSV with all trades

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