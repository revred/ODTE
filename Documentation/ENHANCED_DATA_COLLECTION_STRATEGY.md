# 📊 Enhanced Historical Data Collection Strategy

## 🎯 Overview

To reduce guessing in synthetic data generation and validate our models, we need to collect and store comprehensive historical options data including Greeks, volume metrics, and market microstructure.

## 📈 Data Points to Collect

### 1. **Core Greeks (Per Strike/Expiry)**
```yaml
Essential Greeks:
  Delta: -1 to 1 (directional exposure)
  Gamma: Rate of delta change (acceleration)
  Theta: Time decay (daily P&L from time)
  Vega: IV sensitivity (P&L from 1% IV move)
  Rho: Interest rate sensitivity

Storage Frequency:
  - Every 15 minutes during RTH
  - Every 5 minutes in final hour (gamma hour)
  - Every 1 minute for 0DTE in final 30 minutes
```

### 2. **Implied Volatility Metrics**
```yaml
IV Data:
  Raw IV: Annualized implied volatility
  IV Rank: Percentile over 30/60/90 days
  IV Percentile: Current vs historical range
  Forward IV: Term structure (1D, 7D, 30D)
  
Smile Metrics:
  ATM IV: At-the-money volatility
  25-Delta RR: Risk reversal (put-call skew)
  25-Delta BF: Butterfly (smile curvature)
  Skew Slope: Linear regression coefficient
  Smile Width: 10-delta to 90-delta spread
```

### 3. **Volume & Liquidity Proxies**
```yaml
Volume Metrics:
  Intraday Volume: 15-minute buckets
  Open Interest: Daily snapshots
  Volume/OI Ratio: Turnover indicator
  Block Trades: Large trade detection (>100 contracts)
  
Liquidity Scores:
  Bid-Ask Spread: In ticks and basis points
  Market Depth: Total size within 5 ticks
  Quote Frequency: Updates per minute
  Execution Quality: Actual fill vs mid
```

### 4. **Market Microstructure**
```yaml
Microstructure Data:
  Exchange Distribution:
    - Quotes per exchange (CBOE, PHLX, ISE, etc.)
    - Top-of-book competition
    - Exchange spread differentials
  
  Order Flow:
    - Trade direction (buy/sell pressure)
    - Order imbalance indicators
    - Time-weighted average spread
    - Effective spread vs quoted spread
  
  Intraday Patterns:
    - Opening auction dynamics
    - Lunch-time liquidity drought
    - Power hour acceleration
    - Closing rotation effects
```

### 5. **Pin Risk & Gamma Exposure (0DTE Specific)**
```yaml
Pin Risk Metrics:
  Max Pain: Strike with maximum OI pain
  Gamma Wall: Strike with maximum gamma
  Delta Hedge Flow: Estimated MM hedging
  Strike Magnetism: Price attraction strength
  
Dealer Positioning:
  Net Gamma Exposure: By strike
  Net Delta Exposure: Aggregate
  Vanna Flow: Delta from IV changes
  Charm Flow: Delta from time decay
```

## 🗄️ Storage Schema

### Parquet File Structure
```
/data/enhanced/
├── greeks/
│   ├── 2024/
│   │   ├── 01/
│   │   │   ├── 20240102_SPY_greeks.parquet
│   │   │   └── 20240102_XSP_greeks.parquet
│   └── ...
├── microstructure/
│   ├── 2024/
│   │   ├── 01/
│   │   │   ├── 20240102_SPY_micro.parquet
│   │   │   └── 20240102_XSP_micro.parquet
├── volume_profiles/
│   ├── SPY_intraday_profile.parquet
│   ├── XSP_intraday_profile.parquet
│   └── QQQ_intraday_profile.parquet
└── chain_stats/
    ├── 2024/
        ├── 01/
            ├── 20240102_chain_snapshots.parquet
```

### Parquet Schema Examples

#### Greeks Table
```python
greeks_schema = {
    'timestamp': 'datetime64[ns, UTC]',
    'underlier': 'string',
    'expiry': 'date32',
    'strike': 'float64',
    'right': 'category',  # 'C' or 'P'
    'delta': 'float32',
    'gamma': 'float32',
    'theta': 'float32',
    'vega': 'float32',
    'rho': 'float32',
    'iv': 'float32',
    'underlying_price': 'float64',
    'data_source': 'category',  # 'vendor', 'calculated'
    'quality_score': 'int8'  # 0-100
}
```

#### Microstructure Table
```python
microstructure_schema = {
    'timestamp': 'datetime64[ns, UTC]',
    'option_key': 'string',  # Composite key
    'bid_ask_spread': 'float32',
    'spread_bps': 'float32',
    'bid_depth': 'int32',
    'ask_depth': 'int32',
    'order_imbalance': 'float32',
    'quote_rate': 'int16',  # quotes/second
    'exchanges_on_bid': 'int8',
    'exchanges_on_ask': 'int8',
    'effective_spread': 'float32'
}
```

## 🔄 Collection Sources

### 1. **Real-Time Data Vendors**
```yaml
Primary Sources:
  OPRA Feed: 
    - Direct consolidated feed
    - ~$5000/month for professional
    - Full tick data with all exchanges
  
  Polygon.io:
    - $200-800/month
    - REST + WebSocket APIs
    - Greeks included in options package
  
  Tradier:
    - $10/month brokerage
    - REST API with Greeks
    - Good for snapshots, not tick data
  
  TDA/Schwab API:
    - Free with account
    - Greeks in option chains
    - 15-minute delayed without subscription
```

### 2. **Historical Data Vendors**
```yaml
Historical Sources:
  CBOE DataShop:
    - Official exchange data
    - $500-2000/month
    - Includes Greeks and analytics
  
  OptionMetrics (IvyDB):
    - Academic/institutional
    - Complete Greeks history
    - Expensive but comprehensive
  
  Databento:
    - Pay per GB model
    - OPRA historical data
    - ~$0.50/GB for normalized data
  
  Discount Data:
    - $100-300 one-time
    - End-of-day Greeks
    - Good for validation baseline
```

### 3. **Calculated Greeks (Fallback)**
```python
def calculate_greeks_from_prices(option_quotes, underlying_price, rate=0.05):
    """
    Calculate Greeks when vendor doesn't provide them
    Uses implied volatility from option prices
    """
    for quote in option_quotes:
        # Back out IV from mid price
        iv = implied_volatility(
            price=quote.mid,
            S=underlying_price,
            K=quote.strike,
            T=quote.days_to_expiry/365,
            r=rate,
            right=quote.right
        )
        
        # Calculate Greeks using Black-Scholes
        greeks = black_scholes_greeks(
            S=underlying_price,
            K=quote.strike,
            T=quote.days_to_expiry/365,
            r=rate,
            sigma=iv,
            right=quote.right
        )
        
        yield {
            'option_key': quote.key,
            'timestamp': quote.timestamp,
            'delta': greeks.delta,
            'gamma': greeks.gamma,
            'theta': greeks.theta,
            'vega': greeks.vega,
            'rho': greeks.rho,
            'iv': iv,
            'data_source': 'calculated'
        }
```

## 🔍 Validation Use Cases

### 1. **Synthetic Data Validation**
```python
async def validate_synthetic_generation():
    """
    Compare synthetic vs historical Greeks distributions
    """
    historical = await load_historical_greeks(date, strike_range)
    synthetic = generate_synthetic_options(params)
    
    validations = {
        'delta_distribution': compare_distributions(
            historical.delta, synthetic.delta
        ),
        'iv_smile': validate_smile_shape(
            historical.iv_by_strike, synthetic.iv_by_strike
        ),
        'gamma_profile': validate_gamma_concentration(
            historical.gamma_by_strike, synthetic.gamma_by_strike
        ),
        'theta_decay': validate_time_decay_curve(
            historical.theta_by_dte, synthetic.theta_by_dte
        )
    }
    
    return validations
```

### 2. **Microstructure Realism**
```python
def validate_spread_dynamics(synthetic_quotes, historical_micro):
    """
    Ensure synthetic spreads match historical patterns
    """
    checks = []
    
    # Spread should widen with lower volume
    volume_spread_correlation = correlate(
        historical_micro.volume,
        historical_micro.spread
    )
    
    # Spread should widen near close for 0DTE
    if is_expiry_day:
        final_hour_widening = (
            historical_micro.spread[-60:].mean() / 
            historical_micro.spread[:-60].mean()
        )
        checks.append(('final_hour_spread', final_hour_widening > 1.2))
    
    # Validate quote update frequency
    quote_rate_by_moneyness = historical_micro.groupby('moneyness').quote_rate
    synthetic_rate = synthetic_quotes.quote_update_rate
    
    return checks
```

### 3. **Pin Risk Validation (0DTE)**
```python
def validate_pin_dynamics(synthetic_chain, historical_stats):
    """
    Validate gamma concentration and pin behavior
    """
    # Historical max pain accuracy
    historical_pins = []
    for expiry_date in historical_dates:
        max_pain = calculate_max_pain(historical_stats[expiry_date])
        actual_close = get_underlying_close(expiry_date)
        pin_distance = abs(actual_close - max_pain) / actual_close
        historical_pins.append(pin_distance)
    
    # Synthetic should show similar pin magnetism
    synthetic_gamma_wall = find_gamma_wall(synthetic_chain)
    synthetic_max_pain = calculate_max_pain(synthetic_chain)
    
    return {
        'historical_pin_accuracy': np.percentile(historical_pins, 50),
        'synthetic_gamma_concentration': synthetic_gamma_wall.concentration,
        'strike_spacing_realistic': validate_strike_distribution(synthetic_chain)
    }
```

## 📐 Implementation Priority

### Phase 1: Core Greeks Collection (Week 1-2)
- [ ] Set up Polygon.io or Tradier API connection
- [ ] Implement Greeks snapshot collector (every 15 min)
- [ ] Store in Parquet format with proper schema
- [ ] Build Greeks calculation fallback using Black-Scholes

### Phase 2: Volume & Microstructure (Week 3-4)
- [ ] Collect intraday volume profiles
- [ ] Track bid-ask spreads throughout the day
- [ ] Build liquidity scoring system
- [ ] Store Open Interest snapshots

### Phase 3: Chain Statistics (Week 5-6)
- [ ] Implement smile parameter extraction
- [ ] Calculate max pain and gamma walls
- [ ] Build pin risk indicators for 0DTE
- [ ] Create chain-wide validation metrics

### Phase 4: Validation Framework (Week 7-8)
- [ ] Build comparison tools for synthetic vs historical
- [ ] Create automated validation reports
- [ ] Implement quality scoring system
- [ ] Set up alerts for anomaly detection

## 💰 Cost-Benefit Analysis

### Estimated Costs
```yaml
One-Time:
  Historical Data (1 year): $500-1500
  Development Time: 160 hours
  Infrastructure Setup: $200

Monthly Recurring:
  Polygon.io Options: $200
  Storage (S3/Azure): $50
  Compute (validation): $100
  Total: ~$350/month
```

### Expected Benefits
```yaml
Quantifiable:
  - 30% reduction in synthetic data errors
  - 50% improvement in spread modeling accuracy
  - 90% confidence in Greeks validation
  - 2x faster strategy development cycle

Strategic:
  - Better risk management from accurate Greeks
  - More realistic backtesting with proper microstructure
  - Ability to detect regime changes via Greeks patterns
  - Foundation for ML-based strategy enhancement
```

## 🚀 Quick Start Commands

```bash
# Start collecting Greeks from Polygon
python collect_greeks.py --source polygon --symbol SPY --interval 15m

# Validate synthetic data against historical
python validate_synthetic.py --date 2024-01-15 --symbol XSP

# Generate microstructure report
python analyze_microstructure.py --symbol SPY --date-range 2024-01

# Export enhanced data for backtesting
python export_enhanced.py --format parquet --output ./data/enhanced/
```

## 📚 References

1. **Greeks Calculation**: Hull, J. (2018). "Options, Futures, and Other Derivatives"
2. **Microstructure**: Harris, L. (2003). "Trading and Exchanges"
3. **Pin Risk**: Ni, S. et al. (2005). "Stock Price Clustering on Option Expiration Dates"
4. **Data Vendors Comparison**: [r/algotrading vendor guide](https://www.reddit.com/r/algotrading/wiki/data)

---

**Version**: 1.0  
**Created**: August 2025  
**Status**: Ready for Implementation