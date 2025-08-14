-- SQLite Schema for Enhanced Options Market Data
-- Single source of truth for all historical and real-time data
-- Optimized for 0DTE options trading with comprehensive Greeks and microstructure

-- Enable foreign keys and performance optimizations
PRAGMA foreign_keys = ON;
PRAGMA journal_mode = WAL;  -- Write-Ahead Logging for better concurrency
PRAGMA synchronous = NORMAL;
PRAGMA cache_size = -64000;  -- 64MB cache
PRAGMA temp_store = MEMORY;

-- ============================================================================
-- CORE TABLES
-- ============================================================================

-- Underlying instruments (SPY, XSP, QQQ, etc.)
CREATE TABLE IF NOT EXISTS underlyings (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    symbol TEXT UNIQUE NOT NULL,
    name TEXT,
    multiplier REAL DEFAULT 100,  -- Contract multiplier
    tick_size REAL DEFAULT 0.01,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_underlying_symbol (symbol)
);

-- Option contracts master table
CREATE TABLE IF NOT EXISTS option_contracts (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    underlying_id INTEGER NOT NULL,
    expiry DATE NOT NULL,
    strike REAL NOT NULL,
    right TEXT CHECK(right IN ('C', 'P')) NOT NULL,
    occ_symbol TEXT UNIQUE,  -- OCC standard symbol
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (underlying_id) REFERENCES underlyings(id),
    UNIQUE(underlying_id, expiry, strike, right),
    INDEX idx_contract_lookup (underlying_id, expiry, strike, right),
    INDEX idx_contract_expiry (expiry),
    INDEX idx_contract_occ (occ_symbol)
);

-- ============================================================================
-- MARKET DATA TABLES
-- ============================================================================

-- NBBO quotes with microsecond timestamps
CREATE TABLE IF NOT EXISTS nbbo_quotes (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    contract_id INTEGER NOT NULL,
    timestamp BIGINT NOT NULL,  -- Microseconds since epoch for precision
    bid REAL NOT NULL,
    bid_size INTEGER NOT NULL,
    ask REAL NOT NULL,
    ask_size INTEGER NOT NULL,
    bid_exchange TEXT,  -- Contributing exchanges
    ask_exchange TEXT,
    conditions TEXT,  -- Special conditions/flags
    sequence_number INTEGER,  -- For order tracking
    FOREIGN KEY (contract_id) REFERENCES option_contracts(id),
    INDEX idx_nbbo_time (contract_id, timestamp DESC),
    INDEX idx_nbbo_lookup (contract_id, timestamp)
) PARTITION BY RANGE (timestamp);  -- Consider partitioning by date

-- Last sale/trades data
CREATE TABLE IF NOT EXISTS trades (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    contract_id INTEGER NOT NULL,
    timestamp BIGINT NOT NULL,
    price REAL NOT NULL,
    size INTEGER NOT NULL,
    exchange TEXT,
    conditions TEXT,
    trade_id TEXT,  -- Exchange trade ID
    is_sweep INTEGER DEFAULT 0,  -- Multi-exchange sweep
    is_block INTEGER DEFAULT 0,  -- Block trade indicator
    FOREIGN KEY (contract_id) REFERENCES option_contracts(id),
    INDEX idx_trades_time (contract_id, timestamp DESC),
    INDEX idx_trades_sweep (contract_id, is_sweep, timestamp)
);

-- ============================================================================
-- GREEKS AND ANALYTICS TABLES
-- ============================================================================

-- Greeks snapshots (high-frequency for 0DTE)
CREATE TABLE IF NOT EXISTS greeks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    contract_id INTEGER NOT NULL,
    timestamp BIGINT NOT NULL,
    underlying_price REAL NOT NULL,
    
    -- Core Greeks
    delta REAL,  -- -1 to 1
    gamma REAL,  -- Rate of delta change
    theta REAL,  -- Time decay (daily)
    vega REAL,   -- IV sensitivity
    rho REAL,    -- Interest rate sensitivity
    
    -- Extended Greeks
    lambda REAL,  -- Leverage/elasticity
    vanna REAL,   -- Delta sensitivity to IV
    charm REAL,   -- Delta decay
    vomma REAL,   -- Vega sensitivity to IV
    speed REAL,   -- Gamma sensitivity to underlying
    
    -- Implied Volatility metrics
    iv REAL,      -- Implied volatility
    iv_rank REAL, -- IV percentile rank (0-100)
    iv_percentile REAL,
    
    -- Calculation metadata
    model_type TEXT DEFAULT 'BLACK_SCHOLES',  -- BLACK_SCHOLES, BINOMIAL, VENDOR
    risk_free_rate REAL,
    dividend_yield REAL,
    data_source TEXT,  -- VENDOR, CALCULATED, SYNTHETIC
    quality_score INTEGER,  -- 0-100 quality indicator
    
    FOREIGN KEY (contract_id) REFERENCES option_contracts(id),
    INDEX idx_greeks_time (contract_id, timestamp DESC),
    INDEX idx_greeks_lookup (contract_id, timestamp),
    INDEX idx_greeks_underlying (timestamp, underlying_price)
);

-- ============================================================================
-- MICROSTRUCTURE TABLES
-- ============================================================================

-- Market microstructure metrics
CREATE TABLE IF NOT EXISTS microstructure (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    contract_id INTEGER NOT NULL,
    timestamp BIGINT NOT NULL,
    
    -- Spread metrics
    bid_ask_spread REAL,
    spread_bps REAL,  -- Spread in basis points
    effective_spread REAL,  -- Actual execution spread
    realized_spread REAL,  -- Post-trade spread
    
    -- Depth metrics
    bid_depth INTEGER,  -- Total size on bid (multiple levels)
    ask_depth INTEGER,  -- Total size on ask
    bid_levels INTEGER,  -- Number of price levels on bid
    ask_levels INTEGER,
    
    -- Order flow metrics
    order_imbalance REAL,  -- (bid_size - ask_size) / (bid_size + ask_size)
    trade_imbalance REAL,  -- Buy volume - Sell volume
    quote_rate INTEGER,  -- Quotes per second
    message_rate INTEGER,  -- Total messages per second
    
    -- Competition metrics
    exchanges_on_bid INTEGER,
    exchanges_on_ask INTEGER,
    nbbo_changes INTEGER,  -- NBBO changes in period
    
    FOREIGN KEY (contract_id) REFERENCES option_contracts(id),
    INDEX idx_micro_time (contract_id, timestamp DESC)
);

-- ============================================================================
-- VOLUME AND OPEN INTEREST TABLES
-- ============================================================================

-- Volume and open interest snapshots
CREATE TABLE IF NOT EXISTS volume_oi (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    contract_id INTEGER NOT NULL,
    timestamp BIGINT NOT NULL,
    
    -- Volume metrics
    volume INTEGER,
    buy_volume INTEGER,
    sell_volume INTEGER,
    block_volume INTEGER,  -- Large trades
    sweep_volume INTEGER,  -- Multi-exchange sweeps
    
    -- Open Interest
    open_interest INTEGER,
    oi_change INTEGER,  -- Change from previous day
    
    -- Ratios and indicators
    volume_oi_ratio REAL,
    put_call_ratio REAL,  -- For the chain
    
    FOREIGN KEY (contract_id) REFERENCES option_contracts(id),
    INDEX idx_volume_time (contract_id, timestamp DESC),
    INDEX idx_volume_oi (contract_id, open_interest)
);

-- Intraday volume profiles (typical patterns)
CREATE TABLE IF NOT EXISTS volume_profiles (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    underlying_id INTEGER NOT NULL,
    time_of_day TIME NOT NULL,  -- HH:MM:SS
    day_of_week INTEGER,  -- 0=Sunday, 6=Saturday
    
    -- Profile metrics
    relative_volume REAL,  -- % of daily volume
    relative_spread REAL,  -- Spread multiplier vs average
    typical_quote_rate INTEGER,
    participation_rate REAL,  -- Max % of volume we can take
    
    -- Statistics (updated continuously)
    sample_count INTEGER,
    last_updated TIMESTAMP,
    
    FOREIGN KEY (underlying_id) REFERENCES underlyings(id),
    UNIQUE(underlying_id, time_of_day, day_of_week),
    INDEX idx_profile_lookup (underlying_id, time_of_day)
);

-- ============================================================================
-- CHAIN-WIDE STATISTICS TABLES
-- ============================================================================

-- Option chain statistics (smile, skew, pin risk)
CREATE TABLE IF NOT EXISTS chain_statistics (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    underlying_id INTEGER NOT NULL,
    expiry DATE NOT NULL,
    timestamp BIGINT NOT NULL,
    
    -- Volatility smile parameters
    atm_strike REAL,
    atm_iv REAL,
    skew_slope REAL,  -- Put-call skew
    smile_convexity REAL,
    risk_reversal_25d REAL,
    butterfly_25d REAL,
    
    -- Chain metrics
    total_strikes INTEGER,
    liquid_strikes INTEGER,  -- Strikes with good volume/OI
    total_volume INTEGER,
    total_open_interest INTEGER,
    put_call_ratio REAL,
    
    -- Strike distribution
    min_strike REAL,
    max_strike REAL,
    strike_interval REAL,
    
    -- Pin risk (critical for 0DTE)
    max_pain_strike REAL,  -- Max option pain point
    gamma_wall_strike REAL,  -- Maximum gamma concentration
    delta_wall_strike REAL,
    zero_gamma_strike REAL,  -- Flip point
    
    -- Dealer positioning estimates
    net_gamma_exposure REAL,
    net_delta_exposure REAL,
    net_vanna_exposure REAL,
    
    FOREIGN KEY (underlying_id) REFERENCES underlyings(id),
    INDEX idx_chain_time (underlying_id, expiry, timestamp DESC),
    INDEX idx_chain_lookup (underlying_id, expiry)
);

-- Gamma profile by strike (for pin risk analysis)
CREATE TABLE IF NOT EXISTS gamma_profile (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    chain_stat_id INTEGER NOT NULL,
    strike REAL NOT NULL,
    net_gamma REAL,
    call_gamma REAL,
    put_gamma REAL,
    gamma_dollars REAL,  -- Gamma * underlying price * multiplier
    
    FOREIGN KEY (chain_stat_id) REFERENCES chain_statistics(id),
    INDEX idx_gamma_strike (chain_stat_id, strike)
);

-- ============================================================================
-- UNDERLYING MARKET DATA
-- ============================================================================

-- Underlying quotes and bars
CREATE TABLE IF NOT EXISTS underlying_quotes (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    underlying_id INTEGER NOT NULL,
    timestamp BIGINT NOT NULL,
    bid REAL,
    ask REAL,
    last REAL,
    volume INTEGER,
    
    -- OHLC for bars
    open REAL,
    high REAL,
    low REAL,
    close REAL,
    vwap REAL,  -- Volume-weighted average price
    
    -- Technical indicators
    rsi REAL,
    atr REAL,
    
    FOREIGN KEY (underlying_id) REFERENCES underlyings(id),
    INDEX idx_underlying_time (underlying_id, timestamp DESC)
);

-- VIX and term structure
CREATE TABLE IF NOT EXISTS vix_data (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp BIGINT NOT NULL,
    vix REAL,
    vix9d REAL,  -- 9-day VIX
    vix3m REAL,  -- 3-month VIX
    vix6m REAL,
    term_structure REAL,  -- VIX9D/VIX ratio
    
    -- VIX futures
    front_month REAL,
    back_month REAL,
    contango REAL,  -- Back - Front
    
    INDEX idx_vix_time (timestamp DESC)
);

-- ============================================================================
-- DATA QUALITY AND VALIDATION TABLES
-- ============================================================================

-- Data quality tracking
CREATE TABLE IF NOT EXISTS data_quality (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    table_name TEXT NOT NULL,
    record_date DATE NOT NULL,
    underlying_id INTEGER,
    
    -- Quality metrics
    completeness_score INTEGER,  -- 0-100
    accuracy_score INTEGER,
    timeliness_score INTEGER,
    consistency_score INTEGER,
    
    -- Issue tracking
    missing_records INTEGER,
    suspicious_values INTEGER,
    validation_errors TEXT,  -- JSON array of errors
    
    -- Metadata
    last_validated TIMESTAMP,
    data_source TEXT,
    
    FOREIGN KEY (underlying_id) REFERENCES underlyings(id),
    UNIQUE(table_name, record_date, underlying_id),
    INDEX idx_quality_date (record_date DESC)
);

-- Synthetic data validation results
CREATE TABLE IF NOT EXISTS synthetic_validation (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    validation_date TIMESTAMP NOT NULL,
    synthetic_batch_id TEXT,
    
    -- Validation scores
    greeks_accuracy INTEGER,  -- 0-100
    smile_accuracy INTEGER,
    microstructure_accuracy INTEGER,
    volume_accuracy INTEGER,
    overall_score INTEGER,
    
    -- Detailed results (JSON)
    validation_details TEXT,
    errors TEXT,
    warnings TEXT,
    
    -- Pass/Fail
    is_valid INTEGER DEFAULT 0,
    
    INDEX idx_validation_date (validation_date DESC)
);

-- ============================================================================
-- PERFORMANCE VIEWS
-- ============================================================================

-- Most recent quote for each contract
CREATE VIEW IF NOT EXISTS latest_quotes AS
SELECT 
    oc.*,
    nq.bid,
    nq.ask,
    nq.bid_size,
    nq.ask_size,
    nq.timestamp
FROM option_contracts oc
INNER JOIN (
    SELECT contract_id, MAX(timestamp) as max_ts
    FROM nbbo_quotes
    GROUP BY contract_id
) latest ON oc.id = latest.contract_id
INNER JOIN nbbo_quotes nq ON nq.contract_id = latest.contract_id 
    AND nq.timestamp = latest.max_ts;

-- Current Greeks for all contracts
CREATE VIEW IF NOT EXISTS current_greeks AS
SELECT 
    oc.*,
    g.delta,
    g.gamma,
    g.theta,
    g.vega,
    g.iv,
    g.underlying_price
FROM option_contracts oc
INNER JOIN (
    SELECT contract_id, MAX(timestamp) as max_ts
    FROM greeks
    GROUP BY contract_id
) latest ON oc.id = latest.contract_id
INNER JOIN greeks g ON g.contract_id = latest.contract_id 
    AND g.timestamp = latest.max_ts;

-- 0DTE contracts with enhanced metrics
CREATE VIEW IF NOT EXISTS zero_dte_contracts AS
SELECT 
    oc.*,
    g.delta,
    g.gamma,
    g.theta,
    v.volume,
    v.open_interest,
    m.bid_ask_spread,
    cs.max_pain_strike,
    cs.gamma_wall_strike
FROM option_contracts oc
LEFT JOIN current_greeks g ON oc.id = g.id
LEFT JOIN volume_oi v ON oc.id = v.contract_id
LEFT JOIN microstructure m ON oc.id = m.contract_id
LEFT JOIN chain_statistics cs ON oc.underlying_id = cs.underlying_id 
    AND oc.expiry = cs.expiry
WHERE oc.expiry = DATE('now')
ORDER BY oc.strike;

-- ============================================================================
-- INDEXES FOR PERFORMANCE
-- ============================================================================

-- Composite indexes for common queries
CREATE INDEX idx_nbbo_composite ON nbbo_quotes(contract_id, timestamp DESC, bid, ask);
CREATE INDEX idx_greeks_composite ON greeks(contract_id, timestamp DESC, delta, gamma, iv);
CREATE INDEX idx_volume_composite ON volume_oi(contract_id, timestamp DESC, volume, open_interest);

-- Partial indexes for 0DTE queries
CREATE INDEX idx_0dte_contracts ON option_contracts(expiry, strike) 
    WHERE expiry = DATE('now');
CREATE INDEX idx_liquid_options ON volume_oi(contract_id, volume) 
    WHERE volume > 100 AND open_interest > 500;

-- ============================================================================
-- TRIGGERS FOR DATA INTEGRITY
-- ============================================================================

-- Automatically calculate spread metrics
CREATE TRIGGER calculate_spread_metrics
AFTER INSERT ON nbbo_quotes
BEGIN
    INSERT OR REPLACE INTO microstructure (
        contract_id, 
        timestamp, 
        bid_ask_spread,
        spread_bps
    ) VALUES (
        NEW.contract_id,
        NEW.timestamp,
        NEW.ask - NEW.bid,
        CASE 
            WHEN (NEW.bid + NEW.ask) > 0 
            THEN (NEW.ask - NEW.bid) * 20000 / (NEW.bid + NEW.ask)
            ELSE NULL
        END
    );
END;

-- Update data quality scores
CREATE TRIGGER update_quality_scores
AFTER INSERT ON greeks
BEGIN
    INSERT OR REPLACE INTO data_quality (
        table_name,
        record_date,
        underlying_id,
        completeness_score,
        last_validated
    ) 
    SELECT 
        'greeks',
        DATE(NEW.timestamp / 1000000, 'unixepoch'),
        oc.underlying_id,
        CASE 
            WHEN NEW.delta IS NOT NULL 
                AND NEW.gamma IS NOT NULL 
                AND NEW.theta IS NOT NULL 
                AND NEW.vega IS NOT NULL 
                AND NEW.iv IS NOT NULL
            THEN 100
            ELSE 50
        END,
        CURRENT_TIMESTAMP
    FROM option_contracts oc
    WHERE oc.id = NEW.contract_id;
END;

-- ============================================================================
-- STORED PROCEDURES (Using CTEs for complex queries)
-- ============================================================================

-- Get complete option chain with all metrics
CREATE VIEW option_chain_snapshot AS
WITH latest_data AS (
    SELECT 
        contract_id,
        MAX(timestamp) as max_ts
    FROM nbbo_quotes
    WHERE timestamp > (strftime('%s', 'now') - 300) * 1000000  -- Last 5 minutes
    GROUP BY contract_id
)
SELECT 
    u.symbol as underlying,
    oc.expiry,
    oc.strike,
    oc.right,
    nq.bid,
    nq.ask,
    (nq.bid + nq.ask) / 2 as mid,
    g.delta,
    g.gamma,
    g.theta,
    g.vega,
    g.iv,
    v.volume,
    v.open_interest,
    m.bid_ask_spread,
    m.spread_bps
FROM option_contracts oc
JOIN underlyings u ON oc.underlying_id = u.id
LEFT JOIN latest_data ld ON oc.id = ld.contract_id
LEFT JOIN nbbo_quotes nq ON nq.contract_id = ld.contract_id AND nq.timestamp = ld.max_ts
LEFT JOIN greeks g ON g.contract_id = oc.id 
    AND g.timestamp = (SELECT MAX(timestamp) FROM greeks WHERE contract_id = oc.id)
LEFT JOIN volume_oi v ON v.contract_id = oc.id 
    AND v.timestamp = (SELECT MAX(timestamp) FROM volume_oi WHERE contract_id = oc.id)
LEFT JOIN microstructure m ON m.contract_id = oc.id 
    AND m.timestamp = (SELECT MAX(timestamp) FROM microstructure WHERE contract_id = oc.id);

-- ============================================================================
-- MAINTENANCE PROCEDURES
-- ============================================================================

-- Archive old tick data to separate tables
CREATE TABLE IF NOT EXISTS nbbo_quotes_archive AS SELECT * FROM nbbo_quotes WHERE 0;
CREATE TABLE IF NOT EXISTS trades_archive AS SELECT * FROM trades WHERE 0;

-- Procedure to archive old data (run daily)
-- DELETE FROM nbbo_quotes WHERE timestamp < (strftime('%s', 'now', '-7 days') * 1000000);
-- INSERT INTO nbbo_quotes_archive SELECT * FROM nbbo_quotes WHERE timestamp < (strftime('%s', 'now', '-7 days') * 1000000);

-- ============================================================================
-- SAMPLE DATA INSERTION
-- ============================================================================

-- Insert sample underlyings
INSERT OR IGNORE INTO underlyings (symbol, name, multiplier, tick_size) VALUES
    ('SPY', 'SPDR S&P 500 ETF', 100, 0.01),
    ('XSP', 'Mini-SPX Index Options', 100, 0.01),
    ('QQQ', 'Invesco QQQ Trust', 100, 0.01),
    ('IWM', 'iShares Russell 2000 ETF', 100, 0.01);

-- Sample option contract
-- INSERT INTO option_contracts (underlying_id, expiry, strike, right, occ_symbol) 
-- SELECT id, '2024-08-14', 445.0, 'C', 'SPY240814C00445000'
-- FROM underlyings WHERE symbol = 'SPY';