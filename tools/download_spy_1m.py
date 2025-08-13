#!/usr/bin/env python3
"""
SPY 1-minute data downloader using Yahoo Finance.
Downloads minute bars for SPY and saves as Parquet files.

Usage:
    python download_spy_1m.py --from 2024-01-01 --to 2024-01-05 --dest data/spy_1m/
"""

import argparse
import pandas as pd
import yfinance as yf
from pathlib import Path
from datetime import datetime, timedelta
import pytz

def download_spy_minute_data(start_date, end_date, dest_dir):
    """Download SPY 1-minute data and save as daily Parquet files."""
    dest_path = Path(dest_dir)
    dest_path.mkdir(parents=True, exist_ok=True)
    
    print(f"Downloading SPY 1m data from {start_date} to {end_date}")
    
    # Download data from Yahoo Finance
    spy = yf.Ticker("SPY")
    
    try:
        # Get 1-minute data - Yahoo limits to ~7 days at a time
        current_date = pd.to_datetime(start_date)
        end_dt = pd.to_datetime(end_date)
        
        while current_date <= end_dt:
            # Download in 5-day chunks to avoid Yahoo limits
            chunk_end = min(current_date + timedelta(days=4), end_dt)
            
            print(f"  Fetching {current_date.date()} to {chunk_end.date()}")
            
            df = spy.history(
                start=current_date,
                end=chunk_end + timedelta(days=1),  # Yahoo end is exclusive
                interval="1m",
                prepost=False,  # Regular trading hours only
                repair=True
            )
            
            if df.empty:
                print(f"  No data for {current_date.date()}")
                current_date += timedelta(days=1)
                continue
            
            # Process each trading day
            df.index = df.index.tz_convert('UTC')  # Convert to UTC
            
            # Group by date and save separate files
            for date, day_data in df.groupby(df.index.date):
                if len(day_data) < 100:  # Skip partial days
                    print(f"  Skipping {date} - only {len(day_data)} minutes")
                    continue
                
                # Clean up data
                day_df = day_data.copy()
                day_df.columns = [col.lower() for col in day_df.columns]
                day_df = day_df[['open', 'high', 'low', 'close', 'volume']].copy()
                day_df['timestamp'] = day_df.index
                day_df = day_df.reset_index(drop=True)
                
                # Filter to regular trading hours (9:30 AM - 4:00 PM ET = 14:30 - 21:00 UTC)
                day_df['hour_utc'] = pd.to_datetime(day_df['timestamp']).dt.hour
                rth_data = day_df[
                    (day_df['hour_utc'] >= 14) & 
                    (day_df['hour_utc'] < 21)
                ].copy()
                
                if len(rth_data) >= 350:  # ~390 minutes in trading day, allow some missing
                    filename = dest_path / f"{date}.parquet"
                    rth_data[['timestamp', 'open', 'high', 'low', 'close', 'volume']].to_parquet(
                        filename, index=False
                    )
                    print(f"  Saved {len(rth_data)} minutes to {filename}")
                else:
                    print(f"  Skipping {date} - only {len(rth_data)} RTH minutes")
            
            current_date = chunk_end + timedelta(days=1)
            
    except Exception as e:
        print(f"Error downloading data: {e}")
        return False
    
    print("SPY download complete")
    return True

def main():
    parser = argparse.ArgumentParser(description="Download SPY 1-minute data")
    parser.add_argument("--from", dest="start_date", required=True, 
                      help="Start date (YYYY-MM-DD)")
    parser.add_argument("--to", dest="end_date", required=True,
                      help="End date (YYYY-MM-DD)")
    parser.add_argument("--dest", required=True,
                      help="Destination directory")
    
    args = parser.parse_args()
    
    success = download_spy_minute_data(args.start_date, args.end_date, args.dest)
    exit(0 if success else 1)

if __name__ == "__main__":
    main()