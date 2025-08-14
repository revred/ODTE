#!/usr/bin/env python3
"""
Generate realistic full-day market data for 0DTE strategy testing.
Creates minute-by-minute bars from 14:30 to 21:00 EST (SPX hours).
"""

import pandas as pd
import numpy as np
from datetime import datetime, timedelta

# Configuration
start_time = datetime(2024, 2, 1, 14, 30)  # 2:30 PM EST
end_time = datetime(2024, 2, 1, 21, 0)     # 9:00 PM EST
start_price = 4951.00
daily_vol = 0.015  # 1.5% daily volatility (typical for SPX)

def generate_realistic_bars():
    """Generate realistic minute bars with SPX characteristics"""
    
    # Calculate parameters
    total_minutes = int((end_time - start_time).total_seconds() / 60)
    minute_vol = daily_vol / np.sqrt(390)  # Scale daily vol to minute vol
    
    bars = []
    current_time = start_time
    current_price = start_price
    
    # Generate intraday pattern (U-shaped volume, declining volatility)
    minutes_from_start = np.arange(total_minutes)
    
    # Volume pattern: high at open/close, lower midday
    volume_pattern = 125000 * (1 + 0.5 * np.cos(2 * np.pi * minutes_from_start / total_minutes))
    
    # Volatility pattern: higher at open, declining through day
    vol_pattern = minute_vol * (1.5 - 0.7 * minutes_from_start / total_minutes)
    
    for i in range(total_minutes):
        # Generate realistic OHLC with proper relationships
        vol = vol_pattern[i]
        
        # Random walk with mean reversion
        price_change = np.random.normal(0, vol * current_price)
        if i > 60:  # Add some mean reversion after first hour
            drift = -0.0001 * (current_price - start_price)
            price_change += drift * current_price
            
        # Calculate OHLC
        open_price = current_price
        close_price = current_price + price_change
        
        # Generate high/low with realistic spread
        spread = abs(price_change) + np.random.exponential(vol * current_price * 0.5)
        high_price = max(open_price, close_price) + np.random.uniform(0, spread)
        low_price = min(open_price, close_price) - np.random.uniform(0, spread)
        
        # Ensure price constraints
        high_price = max(high_price, open_price, close_price)
        low_price = min(low_price, open_price, close_price)
        
        # Volume with some randomness
        volume = int(volume_pattern[i] * np.random.uniform(0.8, 1.2))
        
        bars.append({
            'ts': current_time.strftime('%Y-%m-%d %H:%M:%S'),
            'o': round(open_price, 2),
            'h': round(high_price, 2),
            'l': round(low_price, 2),
            'c': round(close_price, 2),
            'v': volume
        })
        
        current_price = close_price
        current_time += timedelta(minutes=1)
    
    return bars

def generate_multi_day_data():
    """Generate data for multiple days"""
    all_bars = []
    
    # Generate for the 3 days in our test range
    dates = [
        (2024, 2, 1),
        (2024, 2, 2), 
        (2024, 2, 5)  # Note: 2/3 and 2/4 were weekend
    ]
    
    for year, month, day in dates:
        start_time = datetime(year, month, day, 14, 30)
        end_time = datetime(year, month, day, 21, 0)
        
        # Each day starts near previous close with gap
        if all_bars:
            prev_close = all_bars[-1]['c']
            start_price = prev_close + np.random.normal(0, prev_close * 0.002)  # Small gap
        else:
            start_price = 4951.00
            
        # Generate bars for this day
        daily_bars = generate_day_bars(start_time, end_time, start_price)
        all_bars.extend(daily_bars)
    
    return all_bars

def generate_day_bars(start_time, end_time, start_price):
    """Generate bars for a single day"""
    total_minutes = int((end_time - start_time).total_seconds() / 60)
    minute_vol = daily_vol / np.sqrt(390)
    
    bars = []
    current_time = start_time
    current_price = start_price
    
    # Intraday patterns
    minutes_from_start = np.arange(total_minutes)
    volume_pattern = 125000 * (1 + 0.5 * np.cos(2 * np.pi * minutes_from_start / total_minutes))
    vol_pattern = minute_vol * (1.5 - 0.7 * minutes_from_start / total_minutes)
    
    for i in range(total_minutes):
        vol = vol_pattern[i]
        
        # Price evolution
        price_change = np.random.normal(0, vol * current_price)
        if i > 60:
            drift = -0.0001 * (current_price - start_price)
            price_change += drift * current_price
            
        open_price = current_price
        close_price = current_price + price_change
        
        spread = abs(price_change) + np.random.exponential(vol * current_price * 0.5)
        high_price = max(open_price, close_price) + np.random.uniform(0, spread)
        low_price = min(open_price, close_price) - np.random.uniform(0, spread)
        
        high_price = max(high_price, open_price, close_price)
        low_price = min(low_price, open_price, close_price)
        
        volume = int(volume_pattern[i] * np.random.uniform(0.8, 1.2))
        
        bars.append({
            'ts': current_time.strftime('%Y-%m-%d %H:%M:%S'),
            'o': round(open_price, 2),
            'h': round(high_price, 2),
            'l': round(low_price, 2),
            'c': round(close_price, 2),
            'v': volume
        })
        
        current_price = close_price
        current_time += timedelta(minutes=1)
    
    return bars

if __name__ == "__main__":
    print("🎯 Generating full-day market data for 0DTE strategy...")
    
    # Generate multi-day data
    bars = generate_multi_day_data()
    
    # Create DataFrame and save
    df = pd.DataFrame(bars)
    output_path = "../Samples/bars_spx_min.csv"
    df.to_csv(output_path, index=False)
    
    print(f"✅ Generated {len(bars)} minute bars across {len(set([b['ts'][:10] for b in bars]))} days")
    print(f"📁 Saved to: {output_path}")
    print(f"🕐 Time range: {bars[0]['ts']} to {bars[-1]['ts']}")
    print(f"💰 Price range: ${bars[0]['o']:.2f} to ${bars[-1]['c']:.2f}")
    
    # Show sample of decision points (every 15 minutes)
    decision_times = [bar for i, bar in enumerate(bars) if i % 15 == 0]
    print(f"\n📊 Sample decision points (every 15 min):")
    for i, bar in enumerate(decision_times[:10]):
        print(f"  {bar['ts']} | Price: ${bar['c']:.2f} | Volume: {bar['v']:,}")
    print(f"  ... and {len(decision_times)-10} more decision points")