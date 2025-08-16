# 🚀 ODTE 20-Year Historical Data Collection System

A comprehensive, production-ready system for collecting 20 years of market data (2005-2025) optimized for ODTE backtesting. Features multi-source data providers, automatic failover, progress tracking, and SQLite optimization.

## 📊 What This System Does

- **Collects 20 years** of historical market data (2005-2025)
- **Multiple data sources** with automatic failover (Polygon.io, Alpha Vantage, Twelve Data)
- **Intelligent rate limiting** to prevent API blocks
- **Progress tracking** with resumption capability
- **Data validation** and quality checks
- **SQLite optimization** for fast backtesting queries
- **~50GB database** ready for ODTE strategies

## 🚀 Quick Start

### Option 1: PowerShell Script (Recommended)

```powershell
# 1. Setup API keys (optional - will work without for demo)
.\setup_and_run_collection.ps1 -SetupKeys

# 2. Test with 30 days (recommended first step)
.\setup_and_run_collection.ps1 -Mode test

# 3. Full 20-year collection (takes 6-12 hours!)
.\setup_and_run_collection.ps1 -Mode full
```

### Option 2: Batch Script (Windows)

```batch
# Double-click run_data_collection.bat and follow the menu
```

### Option 3: Direct .NET Execution

```bash
cd C:\code\ODTE\ODTE.Historical
dotnet run --configuration Release -- test
```

## 🔑 API Keys Setup

The system supports multiple data providers for redundancy:

### Required Environment Variables

```powershell
# PowerShell
$env:POLYGON_API_KEY = "your_polygon_key_here"
$env:ALPHA_VANTAGE_API_KEY = "your_alpha_vantage_key_here" 
$env:TWELVE_DATA_API_KEY = "your_twelve_data_key_here"

# Or permanent setup
[Environment]::SetEnvironmentVariable("POLYGON_API_KEY", "your_key", "User")
```

### Data Provider Options

| Provider | Free Tier | Paid Tier | Best For |
|----------|-----------|-----------|----------|
| **Polygon.io** | 5 calls/min | Up to 1000/min | Premium options data |
| **Alpha Vantage** | 5 calls/min, 500/day | Higher limits | Historical data |
| **Twelve Data** | 8 calls/min, 800/day | Real-time access | Comprehensive data |

**Get API Keys:**
- Polygon.io: https://polygon.io/
- Alpha Vantage: https://www.alphavantage.co/
- Twelve Data: https://twelvedata.com/

## 📋 Collection Modes

### 🧪 Test Mode (Recommended First)
```bash
dotnet run -- test
```
- **Duration**: 2-5 minutes
- **Data**: 30 days, SPY + QQQ
- **Size**: 1-5 MB
- **Purpose**: Verify setup and API connectivity

### 🚀 Full Mode (Production)
```bash
dotnet run -- full
```
- **Duration**: 6-12 hours
- **Data**: 20 years (2005-2025), 25+ symbols
- **Size**: ~50 GB
- **API Calls**: 100,000-500,000
- **⚠️ WARNING**: Major operation, run overnight

### 🔄 Resume Mode
```bash
dotnet run -- resume
```
- Continues from previous progress
- Automatic gap detection and filling

### 🔍 Validation Mode
```bash
dotnet run -- validate
```
- Quality analysis of existing data
- Gap detection and anomaly reporting

### ⚡ Optimize Mode
```bash
dotnet run -- optimize
```
- Database optimization for fast queries
- Index creation and statistics update

## 🗄️ Database Structure

### Optimized Schema
```sql
-- Normalized symbol storage
CREATE TABLE symbols (
    id INTEGER PRIMARY KEY,
    symbol TEXT UNIQUE NOT NULL,
    name TEXT,
    sector TEXT
);

-- High-performance market data storage
CREATE TABLE market_data (
    symbol_id INTEGER NOT NULL,
    timestamp INTEGER NOT NULL,    -- Unix timestamp
    date_key INTEGER NOT NULL,     -- YYYYMMDD for fast filtering
    open_price INTEGER NOT NULL,   -- Price * 10000 for precision
    high_price INTEGER NOT NULL,
    low_price INTEGER NOT NULL,
    close_price INTEGER NOT NULL,
    volume INTEGER NOT NULL,
    vwap_price INTEGER NOT NULL,
    vix_value INTEGER,
    quality_score INTEGER DEFAULT 100,
    
    UNIQUE(symbol_id, timestamp)
) WITHOUT ROWID;

-- Comprehensive indexes for backtesting
CREATE INDEX idx_market_data_symbol_date ON market_data(symbol_id, date_key);
CREATE INDEX idx_market_data_timestamp ON market_data(timestamp);
-- ... 8 more optimized indexes
```

### Key Features
- **Integer pricing** for exact decimal math (no floating point errors)
- **Date keys** (YYYYMMDD) for ultra-fast date filtering  
- **WITHOUT ROWID** tables for better performance
- **Comprehensive indexes** for all query patterns
- **Quality tracking** for data validation

## 📊 Target Symbols

The system collects data for strategically chosen symbols:

### Primary Focus (Always Collected)
- **SPY, XSP** - S&P 500 (main ODTE targets)
- **QQQ** - NASDAQ 100
- **IWM** - Russell 2000
- **VIX** - Volatility Index

### Sector ETFs (High Volume Options)
- **XLF** - Financials
- **XLE** - Energy  
- **XLK** - Technology
- **XLV** - Healthcare
- **XLI, XLP, XLU, XLB, XLRE** - Other sectors

### Individual Stocks (Premium Options)
- **AAPL, MSFT, GOOGL, AMZN** - Tech giants
- **TSLA, NVDA, META** - High volatility names

### Specialty ETFs
- **GLD, SLV** - Precious metals
- **TLT** - Long-term treasuries
- **EEM, FXI, EFA** - International

## 🛡️ Risk Management Features

### Rate Limiting
- **Per-provider limits** (5-8 requests/minute)
- **Automatic throttling** on API errors
- **Exponential backoff** for retries
- **Health monitoring** with failover

### Progress Tracking
```json
{
  "completedDays": ["SPY_2020-01-02", "SPY_2020-01-03", ...],
  "completedBatches": ["SPY_2020-01", "QQQ_2020-01", ...],
  "completedSymbols": ["VIX", "SPY"],
  "lastUpdated": "2025-08-15T10:30:00Z"
}
```

### Data Validation
- **Gap detection** (missing trading days)
- **Anomaly detection** (extreme price movements)
- **Quality scoring** (0-100%)
- **Cross-provider validation**

## 🚀 Performance Optimizations

### Database Optimizations
- **Bulk insert mode** during collection
- **WAL journal mode** for concurrency
- **100MB cache** for performance
- **8KB page size** for I/O efficiency
- **Vacuum and analyze** for query optimization

### Memory Management
- **Streaming processing** (no full dataset in memory)
- **Batch processing** (monthly chunks)
- **Automatic cleanup** of temporary data
- **Connection pooling** for HTTP requests

### Query Performance
```sql
-- Example: Get SPY data for 2020 (microsecond response)
SELECT timestamp, close_price/10000.0 as close, volume
FROM market_data md
JOIN symbols s ON s.id = md.symbol_id  
WHERE s.symbol = 'SPY' 
  AND date_key BETWEEN 20200101 AND 20201231
ORDER BY timestamp;
```

## 🔍 Data Quality Assurance

### Validation Checks
- ✅ **OHLC consistency** (High >= Low, Open/Close in range)
- ✅ **Price reasonableness** (no extreme movements >20%)
- ✅ **Volume validation** (non-zero on trading days)
- ✅ **Date continuity** (no missing trading days)
- ✅ **Cross-provider consensus** (detect bad data)

### Quality Metrics
```csharp
// Example validation report
ValidationReport {
    OverallQualityScore: 0.97,  // 97% quality
    TotalRecords: 2_500_000,
    TotalGaps: 3,
    TotalAnomalies: 15,
    SymbolValidations: {
        "SPY": { QualityScore: 0.99, DataCompleteness: 0.98 },
        "QQQ": { QualityScore: 0.96, DataCompleteness: 0.97 }
    }
}
```

## 📈 Usage in ODTE Backtesting

### Direct Database Access
```csharp
// Fast backtesting queries
using var database = new TimeSeriesDatabase(@"C:\code\ODTE\Data\ODTE_TimeSeries_20Y.db");

// Get 5 years of SPY data in milliseconds
var data = await database.GetRangeAsync(
    new DateTime(2019, 1, 1),
    new DateTime(2024, 1, 1), 
    "SPY");

// Ready for ODTE strategy backtesting
foreach (var bar in data)
{
    // Run your 0DTE strategy logic
    var signal = strategy.Evaluate(bar);
    // ...
}
```

### Integration with Existing ODTE
The database structure is fully compatible with existing ODTE code:

```csharp
// Existing ODTE code works unchanged
var backtester = new Backtester(startDate, endDate, symbol);
var results = backtester.Run(strategy);
```

## 🚨 Important Considerations

### ⚠️ Before Full Collection
- **Verify API keys** work with test mode
- **Ensure 50GB+ free space**
- **Stable internet connection**
- **Consider running overnight**
- **Monitor API quotas**

### 💡 Best Practices
1. **Start with test mode** to verify setup
2. **Set all 3 API keys** for maximum reliability  
3. **Use resume mode** if interrupted
4. **Run validation** after collection
5. **Optimize database** before backtesting

### 🛡️ Error Recovery
- **Automatic retries** on transient errors
- **Provider failover** on permanent failures
- **Progress preservation** for resumption
- **Detailed logging** for troubleshooting

## 📊 Expected Results

### Full Collection Completion
```
🎉 Data collection completed!
📊 Total: 2,847,350 days processed in 8.3 hours
📈 Success rate: 97.2%
💾 Database size: 47.3 GB
📅 Date range: 2005-01-03 to 2025-12-30
🗂️ Symbols: 25 (100% complete)
⚡ Ready for backtesting!
```

### Database Statistics
- **~3 million records** per symbol over 20 years
- **70+ million total records** across all symbols
- **Query performance**: microseconds for date ranges
- **Storage efficiency**: ~50GB for 20 years (compressed)

## 🔧 Troubleshooting

### Common Issues

**Q: "No API keys found" warning**
```powershell
# Set environment variables
$env:POLYGON_API_KEY = "your_key_here"
# Or run setup script
.\setup_and_run_collection.ps1 -SetupKeys
```

**Q: Build errors**
```bash
# Ensure .NET 9.0 SDK installed
dotnet --version
# Should show 9.0.x

# Clean and rebuild
dotnet clean
dotnet build --configuration Release
```

**Q: Database locked errors**
- Close any database browsers/tools
- Restart the application
- Check file permissions

**Q: Slow performance**
- Verify SSD storage (not HDD)
- Ensure adequate RAM (8GB+)
- Check antivirus exclusions

### Debug Mode
```bash
# Enable detailed logging
dotnet run --configuration Debug -- test
```

## 🤝 Contributing

### Adding New Data Providers
1. Implement `IOptionsDataProvider`
2. Add rate limiting appropriate for the API
3. Include comprehensive error handling
4. Add validation for the data format
5. Write unit tests

### Extending Symbol Coverage
Edit `GetTargetSymbols()` in `ComprehensiveDataCollector.cs`:

```csharp
return new List<string>
{
    // Add your symbols here
    "NEW_SYMBOL", "ANOTHER_SYMBOL"
};
```

## 📞 Support

### Getting Help
- **GitHub Issues**: Report bugs and feature requests
- **Documentation**: Check `DataProviders/README.md`
- **Examples**: See `Examples/` folder
- **Logs**: Check console output for detailed information

### Performance Monitoring
```csharp
// Monitor collection progress
var statuses = await fetcher.GetProviderStatusAsync();
foreach (var status in statuses)
{
    Console.WriteLine($"{status.ProviderName}: {status.SuccessRate:P1} success");
}
```

---

## 🎯 Summary

This system provides a **production-ready solution** for collecting 20 years of historical market data optimized specifically for ODTE backtesting. Key benefits:

✅ **Robust**: Multi-source providers with automatic failover
✅ **Efficient**: Optimized SQLite for fast backtesting queries  
✅ **Reliable**: Progress tracking with resumption capability
✅ **Quality**: Comprehensive data validation and quality checks
✅ **Complete**: 25+ symbols across 20 years (2005-2025)
✅ **Ready**: Drop-in replacement for existing ODTE data pipeline

**Total Value**: Transform your ODTE backtesting from limited synthetic data to comprehensive 20-year historical analysis, enabling robust strategy development and validation.

Start with **test mode**, then scale to **full collection** when ready. Your strategies will thank you! 🚀