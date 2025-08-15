#!/usr/bin/env python3
"""
Simple VIX data downloader without Unicode characters
"""

import yfinance as yf
import pandas as pd
from pathlib import Path

def download_vix_data():
    """Download VIX data for 2015-2020"""
    output_dir = Path("C:/code/ODTE/data/real_historical")
    output_dir.mkdir(parents=True, exist_ok=True)
    
    print("Downloading VIX data from 2015-01-01 to 2020-12-31")
    
    try:
        # Download VIX data
        vix = yf.Ticker("^VIX")
        vix_data = vix.history(start="2015-01-01", end="2020-12-31", interval="1d")
        print(f"Downloaded {len(vix_data)} VIX daily bars")
        
        # Save VIX data
        vix_file = output_dir / "VIX_daily_2015_2020.csv"
        vix_data.to_csv(vix_file)
        print(f"Saved VIX data to {vix_file}")
        
        # Download VIX9D data
        print("Downloading VIX9D data")
        vix9d = yf.Ticker("^VIX9D")
        vix9d_data = vix9d.history(start="2015-01-01", end="2020-12-31", interval="1d")
        
        if len(vix9d_data) > 0:
            vix9d_file = output_dir / "VIX9D_daily_2015_2020.csv"
            vix9d_data.to_csv(vix9d_file)
            print(f"Saved VIX9D data to {vix9d_file}")
        else:
            print("VIX9D data not available for this period")
        
        return True
        
    except Exception as e:
        print(f"Error downloading VIX data: {e}")
        return False

if __name__ == "__main__":
    download_vix_data()