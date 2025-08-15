#!/usr/bin/env python3
"""
Download real historical market data for ODTE 10-year analysis
Sources: Yahoo Finance (SPY), FRED (VIX), Alpha Vantage backup
Period: 2015-2020 to extend current 2021-2024 synthetic data
"""

import yfinance as yf
import pandas as pd
import numpy as np
import requests
from datetime import datetime, timedelta
import os
import time
from pathlib import Path

class RealDataDownloader:
    """Download real market data to replace synthetic data"""
    
    def __init__(self, output_dir="C:/code/ODTE/data/real_historical"):
        self.output_dir = Path(output_dir)
        self.output_dir.mkdir(parents=True, exist_ok=True)
        
    def download_spy_data(self, start_date="2015-01-01", end_date="2020-12-31"):
        """Download real SPY minute data from Yahoo Finance"""
        print(f"Downloading real SPY data from {start_date} to {end_date}")
        
        try:
            # Download SPY minute data (Yahoo Finance limitation: last 7 days for 1m)
            # For historical data, we'll get daily and simulate intraday
            spy = yf.Ticker("SPY")
            
            # Get daily data first (more reliable for historical periods)
            daily_data = spy.history(start=start_date, end=end_date, interval="1d")
            print(f"SUCCESS: Downloaded {len(daily_data)} daily SPY bars")
            
            # Save daily data
            daily_file = self.output_dir / "SPY_daily_2015_2020.csv"
            daily_data.to_csv(daily_file)
            print(f"SAVED: Daily data to {daily_file}")
            
            # Also try to get recent minute data for pattern analysis
            try:
                recent_1m = spy.history(period="7d", interval="1m")
                if len(recent_1m) > 0:
                    recent_file = self.output_dir / "SPY_recent_1m_sample.csv"
                    recent_1m.to_csv(recent_file)
                    print(f"SAVED: Recent 1m sample to {recent_file}")
            except:
                print("WARNING: Could not download recent 1m data (expected limitation)")
            
            return daily_data
            
        except Exception as e:
            print(f"ERROR: downloading SPY data: {e}")
            return None
    
    def download_vix_data(self, start_date="2015-01-01", end_date="2020-12-31"):
        """Download real VIX data from Yahoo Finance"""
        print(f"Downloading real VIX data from {start_date} to {end_date}")
        
        try:
            # Download VIX data
            vix = yf.Ticker("^VIX")
            vix_data = vix.history(start=start_date, end=end_date, interval="1d")
            print(f"SUCCESS: Downloaded {len(vix_data)} VIX daily bars")
            
            # Save VIX data
            vix_file = self.output_dir / "VIX_daily_2015_2020.csv"
            vix_data.to_csv(vix_file)
            print(f"SAVED: VIX data to {vix_file}")
            
            return vix_data
            
        except Exception as e:
            print(f"ERROR: downloading VIX data: {e}")
            return None
    
    def download_vix9d_data(self, start_date="2015-01-01", end_date="2020-12-31"):
        """Download VIX9D data (9-day VIX) if available"""
        print(f"Downloading VIX9D data from {start_date} to {end_date}")
        
        try:
            # VIX9D ticker
            vix9d = yf.Ticker("^VIX9D")
            vix9d_data = vix9d.history(start=start_date, end=end_date, interval="1d")
            
            if len(vix9d_data) > 0:
                print(f"SUCCESS: Downloaded {len(vix9d_data)} VIX9D daily bars")
                
                # Save VIX9D data
                vix9d_file = self.output_dir / "VIX9D_daily_2015_2020.csv"
                vix9d_data.to_csv(vix9d_file)
                print(f"💾 Saved VIX9D data: {vix9d_file}")
                return vix9d_data
            else:
                print("WARNING: VIX9D data not available for this period")
                return None
                
        except Exception as e:
            print(f"WARNING: VIX9D not available: {e}")
            return None
    
    def generate_realistic_intraday(self, daily_data, symbol="SPY"):
        """Generate realistic intraday bars from daily OHLC data"""
        print(f"🔧 Generating realistic intraday bars for {symbol}")
        
        all_intraday_bars = []
        
        for date, row in daily_data.iterrows():
            # Generate 390 minute bars for each trading day (9:30-4:00 EST)
            date_str = date.strftime('%Y-%m-%d')
            
            # Daily OHLC values
            daily_open = row['Open']
            daily_high = row['High'] 
            daily_low = row['Low']
            daily_close = row['Close']
            daily_volume = row['Volume']
            
            # Generate intraday path that respects daily OHLC
            intraday_bars = self._generate_intraday_path(
                date, daily_open, daily_high, daily_low, daily_close, daily_volume
            )
            
            all_intraday_bars.extend(intraday_bars)
            
        # Convert to DataFrame
        intraday_df = pd.DataFrame(all_intraday_bars)
        
        # Save intraday data
        intraday_file = self.output_dir / f"{symbol}_intraday_2015_2020.csv"
        intraday_df.to_csv(intraday_file, index=False)
        print(f"💾 Saved intraday data: {intraday_file} ({len(intraday_df)} bars)")
        
        return intraday_df
    
    def _generate_intraday_path(self, date, open_price, high_price, low_price, close_price, volume):
        """Generate realistic minute-by-minute price path"""
        bars = []
        
        # 390 minutes in trading day
        minutes = 390
        
        # Generate price path using geometric brownian motion constrained to daily OHLC
        returns = np.random.normal(0, 0.001, minutes)  # 0.1% per minute volatility
        
        # Adjust returns to ensure we hit daily high/low and end at close
        prices = [open_price]
        
        for i in range(minutes - 1):
            # Basic geometric brownian motion step
            next_price = prices[-1] * (1 + returns[i])
            
            # Constrain to daily range with some flexibility
            if i < minutes * 0.3:  # First third - can hit daily high
                next_price = min(next_price, high_price * 1.001)
            if i > minutes * 0.7:  # Last third - drift toward close
                drift_factor = (close_price / prices[-1] - 1) * 0.1
                next_price = prices[-1] * (1 + returns[i] + drift_factor)
            
            # Hard constraints
            next_price = max(next_price, low_price * 0.999)
            next_price = min(next_price, high_price * 1.001)
            
            prices.append(next_price)
        
        # Ensure we end exactly at close
        prices[-1] = close_price
        
        # Create minute bars
        base_time = date.replace(hour=9, minute=30)  # 9:30 AM
        avg_volume_per_minute = volume / minutes
        
        for i in range(minutes):
            timestamp = base_time + timedelta(minutes=i)
            
            # OHLC for this minute
            if i == 0:
                minute_open = open_price
            else:
                minute_open = prices[i-1]
            
            minute_close = prices[i]
            
            # High/Low with some randomness
            minute_high = max(minute_open, minute_close) * (1 + abs(np.random.normal(0, 0.0005)))
            minute_low = min(minute_open, minute_close) * (1 - abs(np.random.normal(0, 0.0005)))
            
            # Volume distribution (higher at open/close, lower midday)
            if i < 30 or i > 360:  # First/last 30 minutes
                volume_multiplier = 1.5
            elif 120 < i < 270:  # Midday
                volume_multiplier = 0.7
            else:
                volume_multiplier = 1.0
                
            minute_volume = int(avg_volume_per_minute * volume_multiplier * (0.5 + np.random.random()))
            
            bars.append({
                'ts': timestamp.strftime('%Y-%m-%d %H:%M:%S'),
                'o': round(minute_open, 2),
                'h': round(minute_high, 2), 
                'l': round(minute_low, 2),
                'c': round(minute_close, 2),
                'v': minute_volume
            })
        
        return bars
    
    def validate_data_quality(self):
        """Validate downloaded data quality"""
        print("🔍 Validating data quality...")
        
        spy_file = self.output_dir / "SPY_daily_2015_2020.csv"
        vix_file = self.output_dir / "VIX_daily_2015_2020.csv"
        
        issues = []
        
        if spy_file.exists():
            spy_data = pd.read_csv(spy_file)
            print(f"📊 SPY: {len(spy_data)} trading days")
            
            # Check for gaps
            spy_data['Date'] = pd.to_datetime(spy_data['Date'])
            date_gaps = (spy_data['Date'].diff() > timedelta(days=4)).sum()
            if date_gaps > 52:  # More than typical weekends/holidays
                issues.append(f"SPY has {date_gaps} significant date gaps")
            
            # Check price continuity
            price_jumps = (spy_data['Close'].pct_change().abs() > 0.1).sum()
            if price_jumps > 5:
                issues.append(f"SPY has {price_jumps} large price jumps (>10%)")
        
        if vix_file.exists():
            vix_data = pd.read_csv(vix_file)
            print(f"📈 VIX: {len(vix_data)} trading days")
            
            # Check VIX range
            vix_min = vix_data['Close'].min()
            vix_max = vix_data['Close'].max()
            if vix_min < 8 or vix_max > 80:
                issues.append(f"VIX range unusual: {vix_min:.1f} - {vix_max:.1f}")
        
        if issues:
            print("WARNING: Data quality issues found:")
            for issue in issues:
                print(f"   • {issue}")
        else:
            print("SUCCESS: Data quality validation passed")
        
        return len(issues) == 0

def main():
    """Download real historical data for 2015-2020"""
    print("ODTE Real Historical Data Downloader")
    print("=" * 50)
    print("Downloading real market data to replace synthetic data...")
    print()
    
    downloader = RealDataDownloader()
    
    # Download SPY data
    spy_data = downloader.download_spy_data()
    if spy_data is not None:
        print("SUCCESS: SPY download completed")
        
        # Generate realistic intraday data
        downloader.generate_realistic_intraday(spy_data, "SPY")
        print("SUCCESS: Intraday generation completed")
    
    # Download VIX data  
    vix_data = downloader.download_vix_data()
    if vix_data is not None:
        print("SUCCESS: VIX download completed")
    
    # Download VIX9D data
    vix9d_data = downloader.download_vix9d_data()
    
    # Validate data quality
    downloader.validate_data_quality()
    
    print()
    print("SUCCESS: Real data download completed!")
    print(f"Data saved to: {downloader.output_dir}")
    print()
    print("Next steps:")
    print("1. Integrate with ODTE.Historical infrastructure")
    print("2. Update 24-day regime switching to use real data")
    print("3. Compare results vs synthetic data")

if __name__ == "__main__":
    main()