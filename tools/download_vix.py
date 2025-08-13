#!/usr/bin/env python3
"""
VIX daily data downloader using Yahoo Finance.

Usage:
    python download_vix.py --dest data/vix/
"""

import argparse
import pandas as pd
import yfinance as yf
from pathlib import Path
from datetime import datetime, timedelta

def download_vix_data(dest_dir, start_date=None, end_date=None):
    """Download VIX daily data and save as Parquet."""
    dest_path = Path(dest_dir)
    dest_path.mkdir(parents=True, exist_ok=True)
    
    if start_date is None:
        start_date = "2020-01-01"  # Default to last few years
    if end_date is None:
        end_date = datetime.now().strftime("%Y-%m-%d")
    
    print(f"Downloading VIX data from {start_date} to {end_date}")
    
    try:
        # Download VIX from Yahoo Finance
        vix = yf.Ticker("^VIX")
        df = vix.history(start=start_date, end=end_date, interval="1d")
        
        if df.empty:
            print("No VIX data found")
            return False
        
        # Clean up data
        df = df[['Close']].copy()
        df.columns = ['close']
        df['date'] = df.index.date
        df = df.reset_index(drop=True)
        df = df[['date', 'close']]
        
        # Save to Parquet
        filename = dest_path / "vix_daily.parquet"
        df.to_parquet(filename, index=False)
        print(f"Saved {len(df)} VIX records to {filename}")
        
    except Exception as e:
        print(f"Error downloading VIX data: {e}")
        return False
    
    return True

def main():
    parser = argparse.ArgumentParser(description="Download VIX daily data")
    parser.add_argument("--dest", required=True, help="Destination directory")
    parser.add_argument("--from", dest="start_date", help="Start date (YYYY-MM-DD)")
    parser.add_argument("--to", dest="end_date", help="End date (YYYY-MM-DD)")
    
    args = parser.parse_args()
    
    success = download_vix_data(args.dest, args.start_date, args.end_date)
    exit(0 if success else 1)

if __name__ == "__main__":
    main()