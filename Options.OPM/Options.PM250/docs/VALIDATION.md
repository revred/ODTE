# 🔍 PM250 Validation Processes & Constraints Documentation

## Overview

This document details the comprehensive validation framework, data quality requirements, and operational constraints that govern the PM250 Trading System. All trades must pass multiple validation layers before execution.

## Table of Contents
1. [Input Data Validation](#input-data-validation)
2. [Trading Constraints](#trading-constraints)
3. [Risk Validation Rules](#risk-validation-rules)
4. [Performance Validation](#performance-validation)
5. [System Health Checks](#system-health-checks)
6. [Audit Trail Requirements](#audit-trail-requirements)

## Input Data Validation

### Market Data Requirements
```yaml
Required Data Points:
  underlying_price:
    type: decimal
    range: [0.01, 10000.00]
    validation: must_be_within_2%_of_previous
    
  bid_ask_spread:
    type: decimal
    max_spread: 0.50  # Maximum $0.50 spread
    validation: reject_if_crossed
    
  implied_volatility:
    type: decimal
    range: [5.0, 100.0]
    validation: compare_to_historical_range
    
  option_greeks:
    delta:
      range: [-1.0, 1.0]
      precision: 0.001
    gamma:
      range: [0.0, 1.0]
      precision: 0.0001
    theta:
      range: [-10.0, 0.0]
      precision: 0.01
    vega:
      range: [0.0, 50.0]
      precision: 0.01
      
  volume:
    type: integer
    minimum: 100  # Minimum contracts traded
    validation: rolling_average_check
    
  open_interest:
    type: integer
    minimum: 500  # Minimum open interest
    validation: liquidity_threshold
```

### Data Quality Checks
```python
class DataValidator:
    def validate_market_data(self, data):
        checks = []
        
        # 1. Timestamp validation
        if not self.is_market_hours(data.timestamp):
            checks.append("FAIL: Outside market hours")
            
        # 2. Price continuity
        if abs(data.price - self.last_price) / self.last_price > 0.02:
            checks.append("WARN: Price jump > 2%")
            
        # 3. Spread validation
        spread_pct = (data.ask - data.bid) / data.mid * 100
        if spread_pct > 2.0:
            checks.append("FAIL: Spread > 2%")
            
        # 4. Volume validation
        if data.volume < self.min_volume_threshold:
            checks.append("WARN: Low volume")
            
        # 5. Greeks consistency
        if not self.validate_greeks(data.greeks):
            checks.append("FAIL: Invalid Greeks")
            
        # 6. IV validation
        if data.iv < 5 or data.iv > 100:
            checks.append("FAIL: IV out of range")
            
        return all("FAIL" not in check for check in checks)
```

### Missing Data Handling
```yaml
Strategy: Conservative
Rules:
  - No trading if > 5% data missing
  - Forward-fill for up to 3 minutes
  - Use mid-point for missing bid/ask
  - Skip entry if Greeks unavailable
  - Halt on critical data loss
  
Recovery:
  - Attempt reconnection every 30 seconds
  - Switch to backup data feed after 2 minutes
  - Alert operations team immediately
  - Log all data gaps for audit
```

## Trading Constraints

### Position Limits
```python
POSITION_CONSTRAINTS = {
    # Per-trade limits
    'max_contracts_per_trade': 25,
    'max_notional_per_trade': 50000,  # $50K
    'max_risk_per_trade': 500,        # $500
    
    # Daily limits
    'max_trades_per_day': 50,
    'max_contracts_per_day': 500,
    'max_notional_per_day': 500000,   # $500K
    
    # Weekly limits
    'max_trades_per_week': 250,
    'max_loss_per_week': 2500,        # $2,500
    
    # Concentration limits
    'max_positions_same_strike': 5,
    'max_positions_same_expiry': 10,
    'max_delta_exposure': 100,
}
```

### Time Constraints
```yaml
Trading Windows:
  pre_market:
    allowed: false
    reason: "Insufficient liquidity"
    
  market_open:
    start: "09:30:00"
    end: "09:45:00"
    restrictions: "Reduced size (50%)"
    
  regular_hours:
    start: "09:45:00"
    end: "15:00:00"
    restrictions: "None"
    
  power_hour:
    start: "15:00:00"
    end: "15:45:00"
    restrictions: "No new positions after 15:30"
    
  market_close:
    start: "15:45:00"
    end: "16:00:00"
    restrictions: "Exit only"
    
Minimum Separation:
  between_trades: 6 minutes
  same_strike: 15 minutes
  after_loss: 10 minutes
  after_big_win: 5 minutes
```

### Market Condition Constraints
```python
def validate_market_conditions(conditions):
    constraints_violated = []
    
    # VIX constraints
    if conditions.vix > 50:
        constraints_violated.append("VIX > 50: No trading")
    elif conditions.vix > 35:
        constraints_violated.append("VIX > 35: Reduce size 50%")
        
    # Trend constraints
    if abs(conditions.trend_score) > 2.0:
        constraints_violated.append("Extreme trend: Skip entry")
        
    # Volume constraints
    if conditions.volume < conditions.avg_volume * 0.5:
        constraints_violated.append("Low volume: Reduce size 30%")
        
    # Event constraints
    if conditions.is_fomc_day:
        constraints_violated.append("FOMC: No trading 2hrs before/after")
    if conditions.is_expiration_day:
        constraints_violated.append("Expiry: Exit by 14:00")
        
    return constraints_violated
```

## Risk Validation Rules

### Pre-Trade Risk Checks
```python
class PreTradeRiskValidator:
    def validate(self, trade_request):
        validations = {
            'daily_limit': self.check_daily_limit(),
            'position_size': self.check_position_size(trade_request),
            'correlation': self.check_correlation_risk(),
            'margin': self.check_margin_requirements(),
            'fibonacci': self.check_fibonacci_level(),
            'consecutive_losses': self.check_loss_streak(),
            'drawdown': self.check_current_drawdown(),
            'exposure': self.check_total_exposure(),
        }
        
        # All must pass
        return all(validations.values())
    
    def check_daily_limit(self):
        current_loss = self.get_daily_pnl()
        fibonacci_limit = self.get_current_fibonacci_limit()
        return current_loss > -fibonacci_limit
    
    def check_position_size(self, request):
        max_size = self.calculate_max_position_size()
        return request.contracts <= max_size
    
    def check_correlation_risk(self):
        correlation = self.calculate_portfolio_correlation()
        return correlation < 0.7  # Max 70% correlation
```

### Reverse Fibonacci Validation
```yaml
Level Determination:
  consecutive_loss_days: 0
  daily_limit: $500
  
  consecutive_loss_days: 1
  daily_limit: $300
  validation: "Previous day loss > $0"
  
  consecutive_loss_days: 2
  daily_limit: $200
  validation: "Two consecutive loss days"
  
  consecutive_loss_days: 3+
  daily_limit: $100
  validation: "Three or more loss days"
  
Reset Conditions:
  trigger: "Any profitable day > $150"
  action: "Reset to Level 1 ($500)"
  validation: "Verified P&L > $150"
```

### Stop Loss Validation
```python
def validate_stop_loss(position, current_price):
    """Ensure stop loss is properly set and maintained"""
    
    # Calculate maximum loss
    max_loss = position.credit_received * 2.35
    current_loss = position.calculate_loss(current_price)
    
    # Validation rules
    validations = []
    
    # 1. Stop must be set
    if position.stop_price is None:
        validations.append("FAIL: No stop loss set")
    
    # 2. Stop must be at correct level
    expected_stop = position.entry_price + (position.credit * 2.35)
    if abs(position.stop_price - expected_stop) > 0.05:
        validations.append("FAIL: Stop at wrong level")
    
    # 3. Current loss check
    if current_loss > max_loss:
        validations.append("FAIL: Loss exceeds stop")
        
    # 4. Time stop (end of day)
    if current_time >= "15:45:00":
        validations.append("WARN: Approaching time stop")
        
    return validations
```

## Performance Validation

### Real-Time Performance Monitoring
```yaml
Metrics Tracked:
  win_rate:
    calculation: "wins / total_trades"
    threshold: 0.70
    action_if_below: "Reduce position size 25%"
    
  average_win:
    calculation: "sum(winning_trades) / count(wins)"
    threshold: $20.00
    action_if_below: "Review entry criteria"
    
  average_loss:
    calculation: "sum(losing_trades) / count(losses)"
    threshold: -$25.00
    action_if_above: "Tighten stop losses"
    
  profit_factor:
    calculation: "gross_profit / gross_loss"
    threshold: 1.5
    action_if_below: "Pause trading for review"
    
  sharpe_ratio:
    calculation: "avg_return / std_dev * sqrt(252)"
    threshold: 1.0
    action_if_below: "Reduce risk exposure"
```

### Deviation Detection
```python
class PerformanceValidator:
    def __init__(self):
        self.expected_metrics = {
            'win_rate': 0.732,
            'avg_trade_pnl': 16.85,
            'daily_trades': 12.5,
            'max_drawdown': 0.086,
        }
        self.tolerance = 0.15  # 15% deviation allowed
    
    def validate_performance(self, actual_metrics):
        deviations = {}
        
        for metric, expected in self.expected_metrics.items():
            actual = actual_metrics.get(metric)
            deviation = abs(actual - expected) / expected
            
            if deviation > self.tolerance:
                deviations[metric] = {
                    'expected': expected,
                    'actual': actual,
                    'deviation': deviation,
                    'action': self.get_remediation(metric, deviation)
                }
        
        return deviations
    
    def get_remediation(self, metric, deviation):
        actions = {
            'win_rate': "Review GoScore threshold",
            'avg_trade_pnl': "Check execution quality",
            'daily_trades': "Adjust entry frequency",
            'max_drawdown': "Tighten risk controls",
        }
        return actions.get(metric, "Manual review required")
```

## System Health Checks

### Infrastructure Validation
```yaml
Connection Checks:
  broker_api:
    interval: 30 seconds
    timeout: 5 seconds
    failover: backup_broker
    
  data_feed:
    interval: 10 seconds
    timeout: 2 seconds
    failover: secondary_feed
    
  database:
    interval: 60 seconds
    timeout: 3 seconds
    failover: read_replica
    
Resource Monitoring:
  cpu_usage:
    threshold: 80%
    action: "Scale up instance"
    
  memory_usage:
    threshold: 75%
    action: "Restart workers"
    
  disk_space:
    threshold: 90%
    action: "Archive old logs"
    
  network_latency:
    threshold: 100ms
    action: "Switch to closer server"
```

### Order Execution Validation
```python
def validate_execution(order, fill):
    """Validate that fills match expectations"""
    
    validations = []
    
    # Price validation
    expected_price = order.limit_price
    actual_price = fill.price
    slippage = abs(actual_price - expected_price) / expected_price
    
    if slippage > 0.01:  # 1% slippage
        validations.append(f"High slippage: {slippage:.2%}")
    
    # Size validation
    if fill.quantity != order.quantity:
        validations.append(f"Partial fill: {fill.quantity}/{order.quantity}")
    
    # Time validation
    execution_time = fill.timestamp - order.timestamp
    if execution_time.total_seconds() > 1.0:
        validations.append(f"Slow execution: {execution_time.total_seconds()}s")
    
    # Cost validation
    commission = fill.commission + fill.fees
    if commission > order.quantity * 1.00:  # $1 per contract
        validations.append(f"High costs: ${commission:.2f}")
    
    return validations
```

## Audit Trail Requirements

### Trade Logging Requirements
```json
{
  "trade_id": "PM250_20250816_001234",
  "timestamp": "2025-08-16T10:30:15.123Z",
  "market_conditions": {
    "vix": 18.5,
    "spy_price": 445.50,
    "trend_score": 0.35,
    "market_regime": "Calm",
    "volume": 65000000
  },
  "entry_validation": {
    "goscore": 72.5,
    "goscore_threshold": 67.5,
    "risk_level": 1,
    "daily_limit": 500,
    "current_daily_pnl": -125.00,
    "position_size": 10,
    "all_checks_passed": true
  },
  "execution": {
    "strategy": "iron_condor",
    "strikes": [440, 442, 448, 450],
    "contracts": 10,
    "credit_received": 95.00,
    "max_risk": 205.00,
    "stop_loss": 223.25,
    "expected_commission": 10.00
  },
  "validation_results": {
    "pre_trade": "PASS",
    "risk_check": "PASS",
    "position_size": "PASS",
    "margin_check": "PASS",
    "correlation": "PASS"
  },
  "exit": {
    "timestamp": "2025-08-16T10:48:32.456Z",
    "reason": "profit_target",
    "exit_price": 47.50,
    "pnl": 47.50,
    "actual_commission": 10.40,
    "net_pnl": 37.10
  }
}
```

### Compliance Reporting
```yaml
Daily Reports:
  - Total trades executed
  - Win/loss ratio
  - P&L summary
  - Risk metrics
  - Constraint violations
  - System health status

Weekly Reports:
  - Performance vs targets
  - Risk exposure analysis
  - Parameter effectiveness
  - Market regime analysis
  - Optimization recommendations

Monthly Reports:
  - Full performance attribution
  - Risk-adjusted returns
  - Parameter stability analysis
  - Backtest comparison
  - Regulatory compliance

Required Retention:
  - Trade logs: 7 years
  - Performance reports: 5 years
  - System logs: 1 year
  - Tick data: 90 days
```

## Validation Failure Protocols

### Immediate Actions
```yaml
Critical Failures:
  data_loss:
    action: "Halt all trading"
    notification: "Immediate alert to ops team"
    recovery: "Switch to backup systems"
    
  risk_breach:
    action: "Close all positions"
    notification: "Risk manager + ops team"
    recovery: "Manual review required"
    
  system_failure:
    action: "Graceful shutdown"
    notification: "All stakeholders"
    recovery: "Failover to DR site"

Warning Conditions:
  performance_deviation:
    action: "Reduce position size 50%"
    notification: "Daily report"
    recovery: "Auto-adjust parameters"
    
  high_slippage:
    action: "Widen limit orders"
    notification: "End of day summary"
    recovery: "Review execution algo"
```

### Validation Dashboard Requirements
```yaml
Real-Time Displays:
  - Current positions and P&L
  - Fibonacci risk level status
  - GoScore distribution
  - Win rate (rolling 20 trades)
  - System health indicators
  - Constraint violation count
  
Alerts Required:
  - Daily loss > 50% of limit
  - 3 consecutive losses
  - Win rate < 65%
  - System latency > 200ms
  - Data feed interruption
  - Unusual market conditions
```

---

**Document Version**: 1.0.0  
**Last Updated**: August 16, 2025  
**Review Frequency**: Monthly  
**Owner**: Risk Management Team