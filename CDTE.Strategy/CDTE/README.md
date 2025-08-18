# 🗓️ CDTE - Couple Days To Expiry Weekly Strategy

## 📋 Overview
CDTE (Couple Days To Expiry) is a weekly options strategy engine that operates on **real historical NBBO data** with authentic market conditions. The system implements a sophisticated **Monday/Wednesday/Friday workflow** for SPX/XSP weekly options.

## 🎯 Strategy Framework

### Weekly Cycle
```
Monday 10:00 ET  → Enter Core (Thu) + Carry (Fri) positions
Wednesday 12:30 ET → Manage/Roll based on P&L and regime
Thu/Fri by 15:00 CT → Force exit all positions
```

### Market Regime Classification
- **Low IV (<15)**: Broken Wing Butterfly (BWB) on put side
- **Mid IV (15-22)**: Iron Condor bracketing 1-day IV move  
- **High IV (>22)**: Iron Fly with wings for faster theta

## 🏗️ Key Components

### CDTEStrategy.cs
Main strategy engine implementing:
- `EnterMondayAsync()` - Create Core and Carry positions
- `ManageWednesdayAsync()` - P&L assessment and roll decisions
- `ExitWeekAsync()` - Force close by Friday cutoff

### CDTEConfig.cs
YAML-configurable parameters:
```yaml
risk_cap_usd: 800
take_profit_core_pct: 0.70
max_drawdown_pct: 0.50
delta_targets:
  ic_short_abs: 0.18
  bwb_body_put: -0.30
```

### Strike Selection Logic
1. **Primary**: Delta-targeted using recorded Greeks at decision time
2. **Fallback**: Expected Move from ATM IV if Greeks missing
3. **No Synthesis**: All strikes must exist in historical chain

## 🔄 Wednesday Management Rules

### Decision Tree
1. **Take Profit**: Core ≥70% max profit → Close Core, keep Carry
2. **Neutral**: |P&L| < 15% risk → Roll Core to Friday  
3. **Loss**: Drawdown ≥50% → Close both, re-enter cheaper Carry

### Roll Constraints
- Maintain defined risk structure
- Net debit ≤ 25% of original risk cap
- Use current delta targets at 12:30 ET

## ⚙️ Execution Framework

### Real NBBO Fills
- **No synthetic slippage** - uses recorded bid/ask from dataset
- **Marketable-limit orders** against historical book
- **30-second fill window** with adverse tick protection
- **Multi-leg synchronization** required for spreads

### Risk Management  
- **Per-ticket max loss**: ≤ $800
- **Position sizing**: Base (win) vs Half (loss) from previous week
- **Gamma brake**: Reduce exposure if |Gamma| exceeds threshold Thursday

## 📊 Supported Structures

### Broken Wing Butterfly (Low IV)
```
Short 2x Body (Δ -0.30)
Long 1x Near Wing (Δ -0.15)  
Long 1x Far Wing (risk cap)
```

### Iron Condor (Mid IV)
```
Short Call (Δ +0.18)
Long Call Wing (risk cap)
Short Put (Δ -0.18)
Long Put Wing (risk cap)
```

### Iron Fly (High IV)
```
Short ATM Call + Put
Long Wings (risk cap)
```

## 🧪 Testing Framework

### Golden Week Tests
- Frozen dataset slice for reproducible results
- Exact leg selection and P&L validation
- Fill/no-fill scenarios with real NBBO

### Coverage Requirements
- Low/Mid/High IV regime weeks
- Event proximity (FOMC, CPI, NFP)
- Gap scenarios and chain sparsity
- Holiday/half-day handling

## 📈 Performance Metrics

### Weekly KPIs
- **Win Rate**: % of profitable weeks
- **Risk-Adjusted Return**: Weekly P&L per $ at risk  
- **Max Weekly Drawdown**: Worst single week
- **Fill Rate**: % of orders executed vs missed
- **Regime Coverage**: Performance across IV environments

### Validation Targets
- **Sharpe/Sortino** on weekly return series
- **Consistent profitability** across market regimes  
- **Realistic execution** with authentic slippage costs

## 🗄️ Data Dependencies

### Required Tables
- **UnderlyingPrices**: SPX/XSP spot with NBBO
- **OptionChains**: 20+ years with bid/ask/Greeks
- **SessionCalendar**: Trading hours and holidays
- **EconEvents**: FOMC/CPI/NFP timing and severity

### Access Patterns
- **Chain snapshots** at decision times (10:00 ET, 12:30 ET)
- **Delta-sorted views** for fast strike picking
- **NBBO queries** for realistic fill simulation
- **No look-ahead bias** - only data ≤ decision timestamp

## 🔧 Configuration

### Default Settings
```yaml
cdte:
  monday_decision_et: "10:00:00"
  wednesday_decision_et: "12:30:00"
  risk_cap_usd: 800
  
  delta_targets:
    ic_short_abs: 0.18
    bwb_body_put: -0.30
    
  fill_policy:
    window_sec: 30
    max_adverse_tick: 1
```

## 🚀 Usage

### Basic Implementation
```csharp
var config = LoadCDTEConfig("cdte-config.yml");
var strategy = new CDTEStrategy(config, logger);

// Monday entry
var snapshot = await chainProvider.GetSnapshotAsync("SPX", mondayTime);
var orders = await strategy.EnterMondayAsync(snapshot, config);

// Wednesday management  
var wednesdaySnapshot = await chainProvider.GetSnapshotAsync("SPX", wednesdayTime);
var decisions = await strategy.ManageWednesdayAsync(portfolio, wednesdaySnapshot, config);

// Friday exit
var exitSnapshot = await chainProvider.GetSnapshotAsync("SPX", fridayTime);
var exits = await strategy.ExitWeekAsync(portfolio, exitSnapshot, config);
```

## 📋 Implementation Status

### ✅ Completed
- [x] Core strategy framework and configuration
- [x] Market regime classification
- [x] Strike selection with delta targeting
- [x] Monday entry workflow  
- [x] Basic structure creation (BWB, IC, IF)

### 🔄 In Progress  
- [ ] Wednesday management decision tree
- [ ] Real NBBO fill engine integration
- [ ] Chain snapshot provider
- [ ] Historical backtest harness

### 📅 Planned
- [ ] UI dashboard and heatmap
- [ ] Audit trail and reporting
- [ ] Sparse day runner for 20-year coverage
- [ ] Performance analytics and KPI tracking

---

**Last Updated**: August 17, 2025  
**Version**: 1.0 - Initial CDTE Strategy Implementation