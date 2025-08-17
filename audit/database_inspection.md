=== TABLES ===
table: audit_trail
table: market_conditions
table: option_legs
table: performance_metrics
table: portfolio_snapshots
table: risk_management_log
table: sqlite_sequence
table: trades

=== TABLE SCHEMAS ===

--- audit_trail ---
  audit_id (INTEGER)  
  table_name (TEXT) NOT NULL 
  operation (TEXT) NOT NULL 
  record_id (INTEGER) NOT NULL 
  old_values (TEXT)  
  new_values (TEXT)  
  timestamp (TEXT)  DEFAULT CURRENT_TIMESTAMP
  system_notes (TEXT)  
  Row count: 2
  Sample data (first 3 rows):
    audit_id=1 table_name=trades operation=BULK_INSERT record_id=0 old_values=NULL new_values=All trades imported timestamp=2025-08-17 01:51:37 system_notes=PM212 trading ledger generation 
    audit_id=2 table_name=option_legs operation=BULK_INSERT record_id=0 old_values=NULL new_values=All option legs imported timestamp=2025-08-17 01:51:37 system_notes=Complete option chain data 

--- market_conditions ---
  condition_id (INTEGER)  
  month (TEXT) NOT NULL 
  spx_open (REAL) NOT NULL 
  spx_close (REAL) NOT NULL 
  spx_high (REAL) NOT NULL 
  spx_low (REAL) NOT NULL 
  spx_return (REAL) NOT NULL 
  vix_open (REAL) NOT NULL 
  vix_close (REAL) NOT NULL 
  vix_high (REAL) NOT NULL 
  vix_low (REAL) NOT NULL 
  market_regime (TEXT) NOT NULL 
  volatility_rank (REAL) NOT NULL 
  economic_events (TEXT)  
  market_description (TEXT)  
  Row count: 0

--- option_legs ---
  leg_id (INTEGER)  
  trade_id (INTEGER) NOT NULL 
  leg_number (INTEGER) NOT NULL 
  option_symbol (TEXT) NOT NULL 
  expiration_date (TEXT) NOT NULL 
  strike_price (REAL) NOT NULL 
  option_type (TEXT) NOT NULL 
  action (TEXT) NOT NULL 
  quantity (INTEGER) NOT NULL 
  entry_premium (REAL) NOT NULL 
  exit_premium (REAL) NOT NULL 
  entry_delta (REAL) NOT NULL 
  entry_gamma (REAL) NOT NULL 
  entry_theta (REAL) NOT NULL 
  entry_vega (REAL) NOT NULL 
  entry_implied_vol (REAL) NOT NULL 
  entry_bid (REAL) NOT NULL 
  entry_ask (REAL) NOT NULL 
  entry_mid_price (REAL) NOT NULL 
  exit_bid (REAL) NOT NULL 
  exit_ask (REAL) NOT NULL 
  exit_mid_price (REAL) NOT NULL 
  leg_pnl (REAL) NOT NULL 
  settlement_type (TEXT)  DEFAULT 'EUROPEAN'
  assignment_risk (INTEGER)  DEFAULT 0
  Row count: 2920
  Sample data (first 3 rows):
    leg_id=1 trade_id=1 leg_number=1 option_symbol=SPX050103P00001110 expiration_date=2005-01-03 strike_price=1110 option_type=PUT action=SELL quantity=1 entry_premium=0.1 exit_premium=0.01 entry_delta=-0.12 entry_gamma=0.02 entry_theta=-15 entry_vega=0.8 entry_implied_vol=0.128 entry_bid=0.098 entry_ask=0.102 entry_mid_price=0.1 exit_bid=0.0098 exit_ask=0.0102 exit_mid_price=0.01 leg_pnl=0.09 settlement_type=EUROPEAN assignment_risk=0 
    leg_id=2 trade_id=1 leg_number=2 option_symbol=SPX050103P00001100 expiration_date=2005-01-03 strike_price=1100 option_type=PUT action=BUY quantity=1 entry_premium=0.1 exit_premium=0.01 entry_delta=-0.05 entry_gamma=0.01 entry_theta=-8 entry_vega=0.4 entry_implied_vol=0.128 entry_bid=0.098 entry_ask=0.102 entry_mid_price=0.1 exit_bid=0.0098 exit_ask=0.0102 exit_mid_price=0.01 leg_pnl=-0.09 settlement_type=EUROPEAN assignment_risk=0 
    leg_id=3 trade_id=1 leg_number=3 option_symbol=SPX050103C00001305 expiration_date=2005-01-03 strike_price=1305 option_type=CALL action=SELL quantity=1 entry_premium=0.1 exit_premium=0.01 entry_delta=0.12 entry_gamma=0.02 entry_theta=-15 entry_vega=0.8 entry_implied_vol=0.128 entry_bid=0.098 entry_ask=0.102 entry_mid_price=0.1 exit_bid=0.0098 exit_ask=0.0102 exit_mid_price=0.01 leg_pnl=0.09 settlement_type=EUROPEAN assignment_risk=0 

--- performance_metrics ---
  metric_id (INTEGER)  
  period_start (TEXT) NOT NULL 
  period_end (TEXT) NOT NULL 
  total_trades (INTEGER) NOT NULL 
  winning_trades (INTEGER) NOT NULL 
  losing_trades (INTEGER) NOT NULL 
  win_rate (REAL) NOT NULL 
  total_pnl (REAL) NOT NULL 
  avg_win (REAL) NOT NULL 
  avg_loss (REAL) NOT NULL 
  profit_factor (REAL) NOT NULL 
  sharpe_ratio (REAL) NOT NULL 
  max_drawdown (REAL) NOT NULL 
  max_drawdown_duration (INTEGER) NOT NULL 
  volatility (REAL) NOT NULL 
  beta_to_market (REAL) NOT NULL 
  alpha (REAL) NOT NULL 
  total_commissions (REAL) NOT NULL 
  Row count: 0

--- portfolio_snapshots ---
  snapshot_id (INTEGER)  
  month (TEXT) NOT NULL 
  starting_capital (REAL) NOT NULL 
  ending_capital (REAL) NOT NULL 
  monthly_return (REAL) NOT NULL 
  monthly_return_pct (REAL) NOT NULL 
  cumulative_return (REAL) NOT NULL 
  cumulative_return_pct (REAL) NOT NULL 
  trades_count (INTEGER) NOT NULL 
  win_rate (REAL) NOT NULL 
  avg_trade_return (REAL) NOT NULL 
  max_single_loss (REAL) NOT NULL 
  rev_fib_level_used (INTEGER) NOT NULL 
  total_commissions (REAL) NOT NULL 
  sharpe_ratio (REAL) NOT NULL 
  Row count: 0

--- risk_management_log ---
  log_id (INTEGER)  
  trade_id (INTEGER) NOT NULL 
  timestamp (TEXT) NOT NULL 
  risk_event (TEXT) NOT NULL 
  action_taken (TEXT) NOT NULL 
  pre_action_value (REAL)  
  post_action_value (REAL)  
  notes (TEXT)  
  Row count: 0

--- sqlite_sequence ---
  name ()  
  seq ()  
  Row count: 3
  Sample data (first 3 rows):
    name=trades seq=730 
    name=option_legs seq=2920 
    name=audit_trail seq=2 

--- trades ---
  trade_id (INTEGER)  
  month (TEXT) NOT NULL 
  entry_date (TEXT) NOT NULL 
  exit_date (TEXT) NOT NULL 
  strategy (TEXT) NOT NULL 
  underlying_symbol (TEXT) NOT NULL 
  underlying_entry_price (REAL) NOT NULL 
  underlying_exit_price (REAL) NOT NULL 
  vix_entry (REAL) NOT NULL 
  vix_exit (REAL) NOT NULL 
  market_regime (TEXT) NOT NULL 
  total_credit (REAL) NOT NULL 
  total_debit (REAL) NOT NULL 
  net_premium (REAL) NOT NULL 
  max_risk (REAL) NOT NULL 
  max_profit (REAL) NOT NULL 
  actual_pnl (REAL) NOT NULL 
  percent_return (REAL) NOT NULL 
  exit_reason (TEXT) NOT NULL 
  days_to_expiration (INTEGER) NOT NULL 
  rev_fib_limit (REAL) NOT NULL 
  rev_fib_level (INTEGER) NOT NULL 
  position_size (REAL) NOT NULL 
  risk_management (TEXT) NOT NULL 
  was_profit (INTEGER) NOT NULL 
  commissions_paid (REAL) NOT NULL 
  notes (TEXT)  
  created_timestamp (TEXT)  DEFAULT CURRENT_TIMESTAMP
  Row count: 730
  Sample data (first 3 rows):
    trade_id=1 month=2005-01 entry_date=2005-01-03 00:00:00 exit_date=2005-01-03 00:00:00 strategy=Iron Condor 0DTE underlying_symbol=SPX underlying_entry_price=1207.1310890375119 underlying_exit_price=1207.1310890375119 vix_entry=12.8 vix_exit=12.8 market_regime=Bull total_credit=0.2 total_debit=0.2 net_premium=0 max_risk=10 max_profit=0 actual_pnl=-8 percent_return=-0.008 exit_reason=Profit Target (59% of max profit) days_to_expiration=0 rev_fib_limit=1200 rev_fib_level=0 position_size=1000 risk_management=RevFib Level 0: Max Risk $1200 was_profit=1 commissions_paid=8 notes=PM212 Strategy | Post dot-com recovery, institutional confidence building created_timestamp=2025-08-17 01:51:37 
    trade_id=2 month=2005-01 entry_date=2005-01-04 00:00:00 exit_date=2005-01-04 00:00:00 strategy=Iron Condor 0DTE underlying_symbol=SPX underlying_entry_price=1202.9179758882642 underlying_exit_price=1202.9179758882642 vix_entry=12.8 vix_exit=12.8 market_regime=Bull total_credit=0.2 total_debit=0.2 net_premium=0 max_risk=10 max_profit=0 actual_pnl=-8 percent_return=-0.008 exit_reason=Profit Target (46% of max profit) days_to_expiration=0 rev_fib_limit=1200 rev_fib_level=0 position_size=1000 risk_management=RevFib Level 0: Max Risk $1200 was_profit=1 commissions_paid=8 notes=PM212 Strategy | Post dot-com recovery, institutional confidence building created_timestamp=2025-08-17 01:51:37 
    trade_id=3 month=2005-01 entry_date=2005-01-06 00:00:00 exit_date=2005-01-06 00:00:00 strategy=Iron Condor 0DTE underlying_symbol=SPX underlying_entry_price=1210.7686461380226 underlying_exit_price=1210.7686461380226 vix_entry=12.8 vix_exit=12.8 market_regime=Bull total_credit=0.2 total_debit=0.2 net_premium=0 max_risk=10 max_profit=0 actual_pnl=-8 percent_return=-0.008 exit_reason=Profit Target (54% of max profit) days_to_expiration=0 rev_fib_limit=1200 rev_fib_level=0 position_size=1000 risk_management=RevFib Level 0: Max Risk $1200 was_profit=1 commissions_paid=8 notes=PM212 Strategy | Post dot-com recovery, institutional confidence building created_timestamp=2025-08-17 01:51:37 
