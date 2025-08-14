#!/usr/bin/env python3
"""
Generate realistic full-day market data in Parquet format for 0DTE strategy testing.
Creates minute-by-minute bars from 14:30 to 21:00 EST (SPX hours).
Includes CSV export functionality for debugging/compatibility.
"""

import pandas as pd
import numpy as np
import pyarrow as pa
import pyarrow.parquet as pq
from datetime import datetime, timedelta
import os

# Configuration
daily_vol = 0.015  # 1.5% daily volatility (typical for SPX)

class MarketDataGenerator:
    """Professional-grade market data generator for 0DTE options backtesting"""
    
    def __init__(self, symbol="SPX", start_price=4951.00):
        self.symbol = symbol
        self.start_price = start_price
        
    def generate_multi_day_data(self, dates):
        """Generate data for multiple trading days"""
        all_bars = []
        
        for i, (year, month, day) in enumerate(dates):
            start_time = datetime(year, month, day, 14, 30)  # 2:30 PM EST
            end_time = datetime(year, month, day, 21, 0)     # 9:00 PM EST
            
            # Each day starts near previous close with realistic gap
            if all_bars:
                prev_close = all_bars[-1]['close']
                start_price = prev_close + np.random.normal(0, prev_close * 0.002)  # Small overnight gap
            else:
                start_price = self.start_price
                
            # Generate bars for this day
            daily_bars = self._generate_day_bars(start_time, end_time, start_price)
            all_bars.extend(daily_bars)
            
            print(f"SUCCESS: Generated {len(daily_bars)} bars for {year}-{month:02d}-{day:02d}")
        
        return all_bars
    
    def _generate_day_bars(self, start_time, end_time, start_price):
        """Generate realistic bars for a single trading day"""
        total_minutes = int((end_time - start_time).total_seconds() / 60)  # 390 minutes
        minute_vol = daily_vol / np.sqrt(390)  # Scale daily vol to minute vol
        
        bars = []
        current_time = start_time
        current_price = start_price
        
        # Realistic intraday patterns
        minutes_from_start = np.arange(total_minutes)
        
        # U-shaped volume pattern (high at open/close, lower midday)
        volume_base = 125000
        volume_pattern = volume_base * (1 + 0.6 * np.cos(2 * np.pi * minutes_from_start / total_minutes))
        
        # Declining volatility pattern (gamma decay for 0DTE)
        vol_pattern = minute_vol * (1.8 - 1.2 * minutes_from_start / total_minutes)
        
        # Trend component (slight drift)
        trend_strength = np.random.uniform(-0.0002, 0.0002)  # Random daily trend
        
        for i in range(total_minutes):
            vol = vol_pattern[i]
            
            # Price evolution with mean reversion and trend
            random_component = np.random.normal(0, vol * current_price)
            trend_component = trend_strength * current_price
            
            # Mean reversion (prevents prices from drifting too far)
            if i > 60:  # After first hour
                mean_reversion = -0.0001 * (current_price - start_price)
                price_change = random_component + trend_component + mean_reversion * current_price
            else:
                price_change = random_component + trend_component
                
            # Calculate OHLC with proper relationships
            open_price = current_price
            close_price = current_price + price_change
            
            # Generate realistic high/low with volatility-based spread
            intrabar_range = abs(price_change) + np.random.exponential(vol * current_price * 0.3)
            high_price = max(open_price, close_price) + np.random.uniform(0, intrabar_range * 0.6)
            low_price = min(open_price, close_price) - np.random.uniform(0, intrabar_range * 0.6)
            
            # Ensure OHLC constraints
            high_price = max(high_price, open_price, close_price)
            low_price = min(low_price, open_price, close_price)
            
            # Volume with microstructure noise
            volume = int(volume_pattern[i] * np.random.lognormal(0, 0.2))  # Lognormal for realistic distribution
            volume = max(50000, volume)  # Minimum volume floor
            
            bars.append({
                'timestamp': current_time,
                'symbol': self.symbol,
                'open': round(open_price, 2),
                'high': round(high_price, 2),
                'low': round(low_price, 2),
                'close': round(close_price, 2),
                'volume': volume,
                'bar_type': '1min',
                'session': 'RTH'  # Regular Trading Hours
            })
            
            current_price = close_price
            current_time += timedelta(minutes=1)
        
        return bars

def save_to_parquet(bars, output_path):
    """Save market data to Parquet format with optimal schema"""
    df = pd.DataFrame(bars)
    
    # Optimize data types for storage efficiency
    df['timestamp'] = pd.to_datetime(df['timestamp'])
    df['symbol'] = df['symbol'].astype('category')  # String compression
    df['bar_type'] = df['bar_type'].astype('category')
    df['session'] = df['session'].astype('category')
    
    # Price columns as float32 (sufficient precision for SPX)
    for col in ['open', 'high', 'low', 'close']:
        df[col] = df[col].astype('float32')
    
    df['volume'] = df['volume'].astype('uint32')  # Volume never negative
    
    # Set timestamp as index for time series operations
    df = df.set_index('timestamp').sort_index()
    
    # Save with compression and metadata
    table = pa.Table.from_pandas(df)
    
    # Add metadata for future reference
    metadata = {
        b'generator': b'ODTE Market Data Generator v1.0',
        b'created_at': datetime.now().isoformat().encode(),
        b'symbol': bars[0]['symbol'].encode(),
        b'frequency': b'1min',
        b'timezone': b'US/Eastern',
        b'total_bars': str(len(bars)).encode(),
        b'date_range': f"{bars[0]['timestamp'].date()} to {bars[-1]['timestamp'].date()}".encode()
    }
    
    existing_meta = table.schema.metadata or {}
    combined_meta = {**existing_meta, **metadata}
    table = table.replace_schema_metadata(combined_meta)
    
    # Write with optimal settings for time series
    pq.write_table(
        table, 
        output_path,
        compression='snappy',  # Good balance of speed/compression
        row_group_size=10000,  # ~1 week of minute data per row group
        use_dictionary=True    # Efficient for categorical data
    )
    
    return df

def export_parquet_to_csv(parquet_path, csv_path):
    """
    Export Parquet data to CSV format for debugging/compatibility.
    Maintains compatibility with existing CsvMarketData.cs reader.
    """
    print(f"INFO: Reading Parquet file: {parquet_path}")
    
    # Read Parquet file
    df = pd.read_parquet(parquet_path)
    
    # Reset index to get timestamp as column
    df = df.reset_index()
    
    # Transform to match existing CSV schema (ts,o,h,l,c,v)
    csv_df = pd.DataFrame({
        'ts': df['timestamp'].dt.strftime('%Y-%m-%d %H:%M:%S'),
        'o': df['open'].round(2),
        'h': df['high'].round(2), 
        'l': df['low'].round(2),
        'c': df['close'].round(2),
        'v': df['volume'].astype(int)
    })
    
    # Save to CSV
    csv_df.to_csv(csv_path, index=False)
    
    print(f"SUCCESS: Exported {len(csv_df)} bars to CSV: {csv_path}")
    
    # Print sample for verification
    print(f"\nSAMPLE: First 5 bars:")
    print(csv_df.head())
    
    return csv_df

def analyze_data_quality(df):
    """Analyze generated data for quality metrics"""
    print(f"\nANALYSIS: Data Quality Report:")
    print(f"   Total bars: {len(df):,}")
    print(f"   Date range: {df.index.min().date()} to {df.index.max().date()}")
    trading_days = len(set(df.index.date))
    print(f"   Trading days: {trading_days}")
    print(f"   Avg daily bars: {len(df) / trading_days:.0f}")
    
    # Price statistics
    price_change = (df['close'].iloc[-1] - df['close'].iloc[0]) / df['close'].iloc[0]
    daily_returns = df['close'].resample('D').last().pct_change().dropna()
    
    print(f"\nPRICE: Statistics:")
    print(f"   Price range: ${df['low'].min():.2f} - ${df['high'].max():.2f}")
    print(f"   Total return: {price_change:.2%}")
    print(f"   Daily volatility: {daily_returns.std():.2%}")
    print(f"   Avg daily volume: {df['volume'].mean():,.0f}")
    
    # Decision points (every 15 minutes)
    decision_points = df.iloc[::15]  # Every 15th bar = 15 minutes
    print(f"\nTRADING: 0DTE Decision Points:")
    print(f"   Total decision points: {len(decision_points)}")
    print(f"   Avg per day: {len(decision_points) / trading_days:.0f}")

if __name__ == "__main__":
    print("TARGET: Generating professional-grade market data for 0DTE strategy...")
    
    # Initialize generator
    generator = MarketDataGenerator(symbol="SPX", start_price=4951.00)
    
    # Generate for test date range (matching existing config)
    test_dates = [
        (2024, 2, 1),   # Thursday
        (2024, 2, 2),   # Friday  
        (2024, 2, 5)    # Monday (skipping weekend)
    ]
    
    # Generate market data
    bars = generator.generate_multi_day_data(test_dates)
    
    # Save to Parquet (primary format)
    parquet_path = "../Samples/bars_spx_min.parquet"
    df = save_to_parquet(bars, parquet_path)
    
    print(f"SUCCESS: Saved {len(bars)} bars to Parquet: {parquet_path}")
    
    # Export to CSV for compatibility
    csv_path = "../Samples/bars_spx_min.csv"
    export_parquet_to_csv(parquet_path, csv_path)
    
    # Quality analysis
    analyze_data_quality(df)
    
    print(f"\nREADY: 0DTE strategy testing enabled!")
    print(f"   PARQUET: Use for production: {parquet_path}")
    print(f"   CSV: Use for debugging: {csv_path}")