#!/usr/bin/env python3
"""
Intraday data validator for SPY 1-minute bars.
Checks for missing minutes, RTH coverage, and data integrity.

Usage:
    python validate_intraday.py data/spy_1m/2024-01-02.parquet
"""

import argparse
import pandas as pd
import numpy as np
from pathlib import Path
from datetime import datetime, time
import pytz

def validate_intraday_file(file_path):
    """Validate a single intraday Parquet file."""
    print(f"Validating {file_path}")
    
    try:
        df = pd.read_parquet(file_path)
    except Exception as e:
        print(f"ERROR: Cannot read file - {e}")
        return False
    
    if df.empty:
        print("ERROR: File is empty")
        return False
    
    # Check required columns
    required_cols = ['timestamp', 'open', 'high', 'low', 'close', 'volume']
    missing_cols = set(required_cols) - set(df.columns)
    if missing_cols:
        print(f"ERROR: Missing columns - {missing_cols}")
        return False
    
    # Convert timestamp if needed
    if not pd.api.types.is_datetime64_any_dtype(df['timestamp']):
        df['timestamp'] = pd.to_datetime(df['timestamp'])
    
    # Ensure UTC timezone
    if df['timestamp'].dt.tz is None:
        df['timestamp'] = df['timestamp'].dt.tz_localize('UTC')
    else:
        df['timestamp'] = df['timestamp'].dt.tz_convert('UTC')
    
    # Check timestamp monotonicity
    if not df['timestamp'].is_monotonic_increasing:
        print("ERROR: Timestamps not monotonic")
        return False
    
    # Check for RTH coverage (9:30-16:00 ET = 14:30-21:00 UTC)
    df['hour_utc'] = df['timestamp'].dt.hour
    df['minute'] = df['timestamp'].dt.minute
    
    # Regular trading hours check
    rth_mask = (df['hour_utc'] >= 14) & (df['hour_utc'] < 21)
    if not rth_mask.any():
        print("ERROR: No regular trading hours data found")
        return False
    
    rth_data = df[rth_mask].copy()
    
    # Expected minutes: 9:30-16:00 ET = 390 minutes
    expected_minutes = 390
    actual_minutes = len(rth_data)
    coverage_pct = (actual_minutes / expected_minutes) * 100
    
    print(f"  RTH Coverage: {actual_minutes}/{expected_minutes} minutes ({coverage_pct:.1f}%)")
    
    if coverage_pct < 85:  # Allow some missing data
        print(f"WARNING: Low RTH coverage ({coverage_pct:.1f}%)")
    
    # Check for gaps > 5 minutes
    time_diffs = rth_data['timestamp'].diff().dt.total_seconds() / 60
    large_gaps = time_diffs[time_diffs > 5]
    if len(large_gaps) > 0:
        print(f"WARNING: {len(large_gaps)} gaps > 5 minutes found")
        for i, gap in large_gaps.head(3).items():
            ts = rth_data.iloc[i]['timestamp']
            print(f"    {gap:.0f}min gap at {ts}")
    
    # Check OHLC validity
    ohlc_issues = 0
    
    # High should be >= Open, Close, Low
    if not (rth_data['high'] >= rth_data[['open', 'close', 'low']].max(axis=1)).all():
        print("ERROR: High < max(O,C,L) in some bars")
        ohlc_issues += 1
    
    # Low should be <= Open, Close, High
    if not (rth_data['low'] <= rth_data[['open', 'close', 'high']].min(axis=1)).all():
        print("ERROR: Low > min(O,C,H) in some bars")
        ohlc_issues += 1
    
    # Volume should be non-negative
    if (rth_data['volume'] < 0).any():
        print("ERROR: Negative volume found")
        ohlc_issues += 1
    
    # Check for extreme price moves (> 5% in 1 minute)
    price_changes = rth_data['close'].pct_change().abs()
    extreme_moves = price_changes[price_changes > 0.05]
    if len(extreme_moves) > 0:
        print(f"WARNING: {len(extreme_moves)} extreme price moves (>5%) found")
        for i, move in extreme_moves.head(3).items():
            ts = rth_data.iloc[i]['timestamp']
            print(f"    {move*100:.1f}% move at {ts}")
    
    # Summary
    if ohlc_issues == 0:
        print(f"  ✓ OHLC data valid")
    else:
        print(f"  ✗ {ohlc_issues} OHLC issues found")
        return False
    
    print(f"  ✓ Data validation passed")
    return True

def main():
    parser = argparse.ArgumentParser(description="Validate intraday data file")
    parser.add_argument("file_path", help="Path to Parquet file to validate")
    
    args = parser.parse_args()
    
    file_path = Path(args.file_path)
    if not file_path.exists():
        print(f"ERROR: File does not exist - {file_path}")
        exit(1)
    
    success = validate_intraday_file(file_path)
    print("OK" if success else "FAILED")
    exit(0 if success else 1)

if __name__ == "__main__":
    main()