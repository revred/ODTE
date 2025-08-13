#!/usr/bin/env python3
"""
Feature maker for intraday options trading.
Computes OR(15m), VWAP(30m), ATR(20), momentum indicators.

Usage:
    python feature_maker.py data/spy_1m/2024-01-02.parquet features/2024-01-02.parquet
"""

import argparse
import pandas as pd
import numpy as np
from pathlib import Path

def compute_opening_range(df, window_minutes=15):
    """Compute Opening Range (OR) for the first N minutes."""
    if len(df) < window_minutes:
        return None, None
    
    or_data = df.head(window_minutes)
    or_high = or_data['high'].max()
    or_low = or_data['low'].min()
    
    return or_high, or_low

def compute_vwap(df, window=30):
    """Compute Volume Weighted Average Price over rolling window."""
    typical_price = (df['high'] + df['low'] + df['close']) / 3
    vwap = (typical_price * df['volume']).rolling(window).sum() / df['volume'].rolling(window).sum()
    return vwap

def compute_atr(df, period=20):
    """Compute Average True Range."""
    high_low = df['high'] - df['low']
    high_close_prev = np.abs(df['high'] - df['close'].shift(1))
    low_close_prev = np.abs(df['low'] - df['close'].shift(1))
    
    true_range = np.maximum(high_low, np.maximum(high_close_prev, low_close_prev))
    atr = true_range.rolling(period).mean()
    
    return atr

def compute_momentum(prices, window):
    """Compute price momentum over window."""
    return (prices / prices.shift(window) - 1) * 100

def create_features(input_path, vix_data=None, vix9d_data=None):
    """Create features from SPY 1-minute data."""
    
    # Load SPY data
    df = pd.read_parquet(input_path)
    
    # Ensure timestamp is datetime
    if not pd.api.types.is_datetime64_any_dtype(df['timestamp']):
        df['timestamp'] = pd.to_datetime(df['timestamp'])
    
    # Sort by timestamp
    df = df.sort_values('timestamp').reset_index(drop=True)
    
    # Compute Opening Range (15 minutes)
    or_high, or_low = compute_opening_range(df, 15)
    if or_high is None:
        print("WARNING: Insufficient data for OR calculation")
        or_high = or_low = np.nan
    
    or_range = or_high - or_low if or_high and or_low else np.nan
    
    # Create features dataframe
    features = pd.DataFrame({
        'timestamp': df['timestamp'],
        'close': df['close'],
        'or_high': or_high,
        'or_low': or_low,
        'or_range': or_range,
    })
    
    # VWAP (30-minute rolling)
    features['vwap_30m'] = compute_vwap(df, window=30)
    
    # ATR (20-period)
    features['atr_20'] = compute_atr(df, period=20)
    
    # Momentum indicators
    features['momentum_5m'] = compute_momentum(df['close'], window=5)
    features['momentum_15m'] = compute_momentum(df['close'], window=15)
    
    # Session completion percentage (0.0 - 1.0)
    if len(df) > 0:
        features['session_pct'] = np.arange(len(df)) / len(df)
    else:
        features['session_pct'] = 0.0
    
    # Add VIX data if provided
    if vix_data is not None and len(vix_data) > 0:
        trade_date = df['timestamp'].iloc[0].date()
        vix_row = vix_data[vix_data['date'] == trade_date]
        if len(vix_row) > 0:
            features['vix'] = vix_row['close'].iloc[0]
        else:
            # Forward fill from previous day
            prior_vix = vix_data[vix_data['date'] < trade_date]
            if len(prior_vix) > 0:
                features['vix'] = prior_vix['close'].iloc[-1]
            else:
                features['vix'] = np.nan
    else:
        features['vix'] = np.nan
    
    # Add VIX9D data if provided
    if vix9d_data is not None and len(vix9d_data) > 0:
        trade_date = df['timestamp'].iloc[0].date()
        vix9d_row = vix9d_data[vix9d_data['date'] == trade_date]
        if len(vix9d_row) > 0:
            features['vix9d'] = vix9d_row['close'].iloc[0]
        else:
            # Forward fill from previous day
            prior_vix9d = vix9d_data[vix9d_data['date'] < trade_date]
            if len(prior_vix9d) > 0:
                features['vix9d'] = prior_vix9d['close'].iloc[-1]
            else:
                features['vix9d'] = np.nan
    else:
        features['vix9d'] = np.nan
    
    return features

def main():
    parser = argparse.ArgumentParser(description="Create features from SPY 1-minute data")
    parser.add_argument("input_file", help="Input Parquet file (SPY 1m data)")
    parser.add_argument("output_file", help="Output Parquet file (features)")
    parser.add_argument("--vix", help="VIX daily data file")
    parser.add_argument("--vix9d", help="VIX9D daily data file")
    
    args = parser.parse_args()
    
    # Load VIX data if provided
    vix_data = None
    if args.vix and Path(args.vix).exists():
        vix_data = pd.read_parquet(args.vix)
        vix_data['date'] = pd.to_datetime(vix_data['date']).dt.date
    
    # Load VIX9D data if provided
    vix9d_data = None
    if args.vix9d and Path(args.vix9d).exists():
        vix9d_data = pd.read_parquet(args.vix9d)
        vix9d_data['date'] = pd.to_datetime(vix9d_data['date']).dt.date
    
    # Create features
    try:
        features = create_features(args.input_file, vix_data, vix9d_data)
        
        # Save output
        output_path = Path(args.output_file)
        output_path.parent.mkdir(parents=True, exist_ok=True)
        features.to_parquet(output_path, index=False)
        
        print(f"Created {len(features)} feature records in {output_path}")
        
        # Quick stats
        print(f"  OR Range: {features['or_range'].iloc[0]:.2f}")
        print(f"  ATR (final): {features['atr_20'].iloc[-1]:.2f}")
        print(f"  VIX: {features['vix'].iloc[0]:.1f}")
        
    except Exception as e:
        print(f"Error creating features: {e}")
        exit(1)

if __name__ == "__main__":
    main()