#!/usr/bin/env python3
"""
Download real historical market data for 2005-2015 period
Extends our existing 2015-2020 dataset to provide 20 years total
Sources: Yahoo Finance (SPY), CBOE (VIX)
"""

import yfinance as yf
import pandas as pd
import numpy as np
import requests
from datetime import datetime, timedelta
import os
import time
from pathlib import Path

class Extended2005To2015Downloader:
    """Download real market data for 2005-2015 to extend historical coverage"""
    
    def __init__(self, output_dir="C:/code/ODTE/data/real_historical"):
        self.output_dir = Path(output_dir)
        self.output_dir.mkdir(parents=True, exist_ok=True)
        print(f"Real data will be saved to: {self.output_dir}")
        
    def download_spy_data_2005_2015(self):
        """Download real SPY daily data for 2005-2015"""
        print(">> Downloading real SPY data for 2005-2015...")
        
        try:
            # Create SPY ticker object
            spy = yf.Ticker("SPY")
            
            # Download daily data for 2005-2015
            spy_data = spy.history(
                start="2005-01-01", 
                end="2015-12-31",
                interval="1d",
                auto_adjust=True,
                prepost=True
            )
            
            print(f"Downloaded {len(spy_data)} SPY daily bars")
            
            # Clean and prepare data
            spy_data = spy_data.reset_index()
            spy_data['Date'] = pd.to_datetime(spy_data['Date']).dt.date
            
            # Rename columns to match our existing format
            spy_data = spy_data.rename(columns={
                'Open': 'open',
                'High': 'high', 
                'Low': 'low',
                'Close': 'close',
                'Volume': 'volume'
            })
            
            # Select relevant columns
            spy_data = spy_data[['Date', 'open', 'high', 'low', 'close', 'volume']]
            
            # Remove any rows with NaN values
            spy_data = spy_data.dropna()
            
            # Quality validation
            print(f"Data range: {spy_data['Date'].min()} to {spy_data['Date'].max()}")
            print(f"Price range: ${spy_data['close'].min():.2f} to ${spy_data['close'].max():.2f}")
            
            # Save to CSV
            output_file = self.output_dir / "SPY_daily_2005_2015.csv"
            spy_data.to_csv(output_file, index=False)
            print(f"SUCCESS: SPY data saved to {output_file}")
            
            return spy_data
            
        except Exception as e:
            print(f"ERROR downloading SPY data: {e}")
            return None
    
    def download_vix_data_2005_2015(self):
        """Download real VIX daily data for 2005-2015"""
        print(">> Downloading real VIX data for 2005-2015...")
        
        try:
            # Create VIX ticker object  
            vix = yf.Ticker("^VIX")
            
            # Download daily VIX data
            vix_data = vix.history(
                start="2005-01-01",
                end="2015-12-31", 
                interval="1d",
                auto_adjust=True
            )
            
            print(f"Downloaded {len(vix_data)} VIX daily bars")
            
            # Clean and prepare data
            vix_data = vix_data.reset_index()
            vix_data['Date'] = pd.to_datetime(vix_data['Date']).dt.date
            
            # Rename columns
            vix_data = vix_data.rename(columns={
                'Open': 'open',
                'High': 'high',
                'Low': 'low', 
                'Close': 'close',
                'Volume': 'volume'
            })
            
            # Select relevant columns
            vix_data = vix_data[['Date', 'open', 'high', 'low', 'close', 'volume']]
            
            # Remove NaN values
            vix_data = vix_data.dropna()
            
            # Quality validation
            print(f"VIX range: {vix_data['Date'].min()} to {vix_data['Date'].max()}")
            print(f"VIX levels: {vix_data['close'].min():.2f} to {vix_data['close'].max():.2f}")
            
            # Save to CSV
            output_file = self.output_dir / "VIX_daily_2005_2015.csv"
            vix_data.to_csv(output_file, index=False)
            print(f"SUCCESS: VIX data saved to {output_file}")
            
            return vix_data
            
        except Exception as e:
            print(f"ERROR downloading VIX data: {e}")
            return None
    
    def download_vix9d_data_2005_2015(self):
        """Download VIX9D data for 2005-2015 (available from 2009)"""
        print(">> Downloading VIX9D data for 2009-2015...")
        
        try:
            # VIX9D only available from 2009
            vix9d = yf.Ticker("^VIX9D")
            
            vix9d_data = vix9d.history(
                start="2009-01-01",  # VIX9D started in 2009
                end="2015-12-31",
                interval="1d"
            )
            
            print(f"Downloaded {len(vix9d_data)} VIX9D daily bars")
            
            # Clean data
            vix9d_data = vix9d_data.reset_index()
            vix9d_data['Date'] = pd.to_datetime(vix9d_data['Date']).dt.date
            vix9d_data = vix9d_data.rename(columns={
                'Open': 'open', 'High': 'high', 'Low': 'low', 'Close': 'close', 'Volume': 'volume'
            })
            vix9d_data = vix9d_data[['Date', 'open', 'high', 'low', 'close', 'volume']].dropna()
            
            # Quality validation
            print(f"VIX9D range: {vix9d_data['Date'].min()} to {vix9d_data['Date'].max()}")
            print(f"VIX9D levels: {vix9d_data['close'].min():.2f} to {vix9d_data['close'].max():.2f}")
            
            # Save to CSV
            output_file = self.output_dir / "VIX9D_daily_2009_2015.csv"
            vix9d_data.to_csv(output_file, index=False)
            print(f"SUCCESS: VIX9D data saved to {output_file}")
            
            return vix9d_data
            
        except Exception as e:
            print(f"WARNING: Could not download VIX9D data: {e}")
            print("This is expected as VIX9D has limited history")
            return None
    
    def validate_data_quality(self, spy_data, vix_data):
        """Validate downloaded data quality"""
        print("\n>> Data Quality Validation:")
        
        if spy_data is not None:
            # Check for major market events in the data
            print(f"SPY data covers {len(spy_data)} trading days")
            
            # 2008 Financial Crisis validation
            crisis_data = spy_data[
                (spy_data['Date'] >= datetime(2008, 9, 1).date()) & 
                (spy_data['Date'] <= datetime(2009, 3, 31).date())
            ]
            if len(crisis_data) > 0:
                crisis_low = crisis_data['close'].min()
                print(f"OK 2008 Financial Crisis period captured (SPY low: ${crisis_low:.2f})")
            
            # Flash Crash 2010 validation
            flash_crash_data = spy_data[
                (spy_data['Date'] >= datetime(2010, 5, 1).date()) & 
                (spy_data['Date'] <= datetime(2010, 5, 31).date())
            ]
            if len(flash_crash_data) > 0:
                print(f"OK Flash Crash 2010 period captured")
        
        if vix_data is not None:
            print(f"VIX data covers {len(vix_data)} trading days")
            
            # Check for VIX spikes during crisis
            vix_max = vix_data['close'].max()
            if vix_max > 60:
                print(f"OK Major VIX spike captured (max: {vix_max:.2f})")
            
            # Check for calm periods
            vix_min = vix_data['close'].min()
            print(f"OK VIX range: {vix_min:.2f} to {vix_max:.2f}")
        
        print("OK Data quality validation completed")
    
    def download_all_2005_2015_data(self):
        """Download all market data for 2005-2015"""
        print(">> Starting 2005-2015 Historical Data Download")
        print("=" * 60)
        
        # Download SPY data
        spy_data = self.download_spy_data_2005_2015()
        time.sleep(1)  # Rate limiting
        
        # Download VIX data  
        vix_data = self.download_vix_data_2005_2015()
        time.sleep(1)
        
        # Download VIX9D data (2009+)
        vix9d_data = self.download_vix9d_data_2005_2015()
        
        # Validate data quality
        self.validate_data_quality(spy_data, vix_data)
        
        print("\n>> 2005-2015 Data Download Summary:")
        print(f"SPY: {'SUCCESS' if spy_data is not None else 'FAILED'}")
        print(f"VIX: {'SUCCESS' if vix_data is not None else 'FAILED'}")
        print(f"VIX9D: {'SUCCESS' if vix9d_data is not None else 'LIMITED'}")
        
        if spy_data is not None and vix_data is not None:
            print(f"\n>> SUCCESS: Downloaded {len(spy_data)} SPY + {len(vix_data)} VIX bars")
            print("Ready for 20-year historical analysis!")
            return True
        else:
            print("\n>> FAILED: Could not download required data")
            return False

def main():
    """Main execution function"""
    print("ODTE Historical Data Downloader - 2005-2015 Extension")
    print("====================================================")
    
    downloader = Extended2005To2015Downloader()
    success = downloader.download_all_2005_2015_data()
    
    if success:
        print("\n>> 2005-2015 real data download completed successfully!")
        print("Now we have 20 years of real market data (2005-2025)")
        print("Ready to run enhanced strategy backtesting!")
    else:
        print("\n>> Download failed. Please check internet connection and try again.")

if __name__ == "__main__":
    main()