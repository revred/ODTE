# 🚀 ODTE Authentic Market Data Acquisition System

## 📋 Overview

This system downloads **20 years of authentic market data** (2005-present) from Yahoo Finance and converts it to an optimized SQLite database for the ODTE genetic algorithm trading system.

## 🎯 Key Features

- ✅ **Authentic Data**: Real market data from Yahoo Finance (no synthetic/simulated data)
- ✅ **Comprehensive Coverage**: 2005-present with major ETFs (XSP, SPY, QQQ, IWM) + VIX
- ✅ **Chunked Processing**: 6 manageable chunks with priority-based execution
- ✅ **Rate Limited**: Respectful API usage with 60 requests/minute limit  
- ✅ **Resumable**: Can restart from any chunk if interrupted
- ✅ **Validated**: Data quality checks and gap detection included
- ✅ **Optimized Storage**: CSV → Parquet → SQLite with compression

## 📊 Data Coverage Analysis

### Current Database Status
- **Existing Data**: 2015-01-02 to 2021-02-08 (20,000 records, 1.0 MB)
- **Missing Data**: 2005-2014 (10 years) + 2021-present (4+ years)
- **Total Gap**: ~5,300 days (~1.4M records estimated)

### 6-Chunk Acquisition Strategy

| **Chunk** | **Period** | **Priority** | **Est. Records** | **Est. Size** | **Why Important** |
|-----------|------------|--------------|------------------|---------------|-------------------|
| **Chunk 1** | 2022-Present | **HIGH** | 355K | 22 MB | Recent market conditions for current trading |
| **Chunk 2** | 2020-2021 | **HIGH** | 197K | 12 MB | COVID era - critical for volatility testing |
| **Chunk 3** | 2018-2019 | MEDIUM | 196K | 12 MB | Modern market structure, low volatility |
| **Chunk 4** | 2015-2017 | MEDIUM | 295K | 18 MB | Complete XSP coverage, post-crisis stability |
| **Chunk 5** | 2010-2014 | LOW | 491K | 30 MB | Post-2008 recovery period |
| **Chunk 6** | 2005-2009 | LOW | 491K | 30 MB | Financial crisis data - stress testing |

**Total Estimated**: ~2M records, ~124 MB additional data

## 🔧 Technical Implementation

### Architecture
```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Yahoo Finance │───▶│   Rate Limiter   │───▶│  CSV Download   │
│       API       │    │ (60 req/minute)  │    │   (Raw Data)    │
└─────────────────┘    └──────────────────┘    └─────────────────┘
                                                         │
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│ SQLite Database │◀───│  Data Converter  │◀───│ Parquet Staging │
│  (Final Store)  │    │ (Quality Checks) │    │  (Validation)   │
└─────────────────┘    └──────────────────┘    └─────────────────┘
```

### Data Processing Pipeline
1. **Download**: Yahoo Finance historical data API calls
2. **Parse**: CSV parsing with error handling and validation
3. **Stage**: Save to Parquet files for backup/validation  
4. **Convert**: Transform to MarketDataBar format
5. **Insert**: Batch SQLite insertion with transactions
6. **Validate**: Data quality checks and gap detection

### Rate Limiting & Respectful Usage
- **60 requests per minute** maximum to Yahoo Finance
- **1 second delay** between chunk downloads
- **Exponential backoff** on API errors
- **User-Agent identification** for responsible usage
- **No API key required** - uses free Yahoo Finance endpoint

## 🚀 Quick Start

### Option 1: Automated Setup (Recommended)
```powershell
# Run comprehensive setup and acquisition
.\setup_and_run_acquisition.ps1
```

### Option 2: Manual Execution
```bash
# Run data gap analysis first
cd ODTE.Strategy.Tests
dotnet test --filter "DataGapAnalysisTest"

# Test the pipeline with sample data
dotnet test --filter "Execute_Small_Sample_Data_Acquisition"

# Run full acquisition (2-4 hours)
dotnet test --filter "Execute_Complete_Data_Acquisition_2005_To_Present"
```

### Option 3: Batch File (Windows)
```batch
# Simple batch execution
run_data_acquisition.bat
```

## 📈 Data Sources & Coverage

### Primary Data Source: Yahoo Finance
- **URL**: `https://query1.finance.yahoo.com/v7/finance/download/`
- **Cost**: FREE (no API key required)
- **Rate Limit**: Self-imposed 60 requests/minute
- **Data Quality**: High (actual exchange data)
- **Historical Coverage**: 1970s-present for major symbols

### Symbol Coverage
| **Symbol** | **Description** | **Available From** | **PM250 Usage** |
|------------|-----------------|-------------------|-----------------|
| **XSP** | SPDR S&P 500 Mini ETF | ~2011 | Primary 0DTE options strategy |
| **SPY** | SPDR S&P 500 ETF | 1993 | Backup/validation data |
| **QQQ** | Invesco QQQ ETF | 1999 | Tech sector representation |
| **IWM** | iShares Russell 2000 ETF | 2000 | Small cap representation |
| **^VIX** | CBOE Volatility Index | 1990 | Volatility regime detection |

## ⚡ Performance Optimization

### Processing Speed
- **Parallel Downloads**: Multiple symbols processed concurrently
- **SQLite WAL Mode**: Write-ahead logging for faster inserts
- **Batch Transactions**: 1000+ records per transaction
- **Memory Mapping**: Efficient file I/O operations
- **Progress Tracking**: Real-time ETA and completion estimates

### Storage Efficiency  
- **Compression**: ~10x reduction from raw CSV to SQLite
- **Indexing**: Optimized timestamp/symbol queries
- **Data Types**: Efficient integer storage for prices (×10000)
- **Normalization**: Symbol lookup tables to reduce redundancy

## 🛡️ Data Quality & Validation

### Automatic Quality Checks
- ✅ **Date Validation**: Ensure chronological order
- ✅ **Price Validation**: OHLC relationships (High ≥ Open, etc.)
- ✅ **Volume Validation**: Non-negative volume checks  
- ✅ **Gap Detection**: Identify missing trading days
- ✅ **Duplicate Prevention**: Primary key constraints
- ✅ **Range Validation**: Reasonable price/volume ranges

### Manual Validation Options
```csharp
// Check data integrity
var stats = await dataManager.GetStatsAsync();
Console.WriteLine($"Records: {stats.TotalRecords:N0}");
Console.WriteLine($"Date Range: {stats.StartDate} to {stats.EndDate}");

// Validate specific periods
var sample = await dataManager.GetMarketDataAsync("XSP", 
    new DateTime(2020, 3, 1), new DateTime(2020, 3, 31));
Console.WriteLine($"March 2020 (COVID crash): {sample.Count} records");
```

## 🎯 Integration with PM250 Genetic Algorithm

### Enhanced Testing Capabilities
With 20 years of data, the PM250 genetic algorithm can now:

- ✅ **Test across major market events**: 2008 Crisis, 2020 COVID, 2022 Rate Hikes
- ✅ **Validate regime switching**: Bull/Bear/Volatile market adaptability  
- ✅ **Stress test parameters**: Extreme volatility periods (VIX 80+)
- ✅ **Long-term robustness**: 20-year parameter stability validation
- ✅ **Seasonal analysis**: January effect, FOMC cycles, triple witching
- ✅ **Volatility regime testing**: VIX 10-80 range coverage

### Updated Test Commands
```bash
# Test genetic algorithm across full 20-year period
dotnet test --filter "PM250_TwentyYear_GeneticTest"

# Validate performance in specific crisis periods
dotnet test --filter "PM250_FinancialCrisis_2008_Test"
dotnet test --filter "PM250_COVID_Crash_2020_Test"
dotnet test --filter "PM250_Rate_Hiking_2022_Test"
```

## 📊 Expected Results

### Database Growth
- **Current**: 1.0 MB SQLite database
- **After Acquisition**: ~125 MB SQLite database  
- **Compression Ratio**: ~10x vs raw CSV data
- **Query Performance**: <100ms for any yearly range

### Genetic Algorithm Benefits
- **Parameter Robustness**: 20-year validation vs 6-year current
- **Crisis Testing**: Validate performance in 2008, 2020 crashes
- **Regime Adaptation**: Test bull/bear/volatile market switching
- **Production Confidence**: Extensive historical validation

## 🚨 Important Warnings

### Yahoo Finance Terms of Service
- ✅ **Rate Limited**: We respect their servers with 60 req/min limit
- ✅ **Educational Use**: Data used for research/educational purposes  
- ✅ **No Resale**: Data not redistributed or sold
- ✅ **Attribution**: Yahoo Finance credited as data source

### Technical Considerations
- ⚠️ **Internet Required**: Stable connection for 2-4 hours
- ⚠️ **Disk Space**: 3 GB temporary space during processing
- ⚠️ **Processing Time**: 2-4 hours depending on connection speed
- ⚠️ **Interruption Handling**: Can resume but may need cleanup

### Data Limitations
- 📅 **Weekend Gaps**: No trading data for weekends/holidays (expected)
- 📊 **Corporate Actions**: May not adjust for all splits/dividends  
- 🕐 **Timezone**: Data in market timezone (EST/EDT)
- 📈 **Intraday**: Daily OHLC data only (no minute/tick data)

## 🤝 Support & Troubleshooting

### Common Issues

**"Connection timeout"**
- Solution: Check internet connection, retry with smaller chunks
- The system will automatically retry failed requests

**"Rate limit exceeded"**  
- Solution: System automatically handles this with delays
- If persistent, increase delay in YahooFinanceProvider.cs

**"Insufficient disk space"**
- Solution: Free up at least 5 GB before starting
- Clean staging directory: `rm -rf Data/Staging/*`

**"Database locked"**
- Solution: Close any other applications using the SQLite file
- Restart the acquisition process

### Getting Help
1. Check the detailed acquisition report in `Data/Staging/`
2. Review console output for specific error messages  
3. Test with small sample first: `Execute_Small_Sample_Data_Acquisition`
4. Validate your internet connection and Yahoo Finance availability

## 📈 Future Enhancements

### Planned Improvements
- 🔄 **Delta Updates**: Daily incremental updates for recent data
- 📊 **Options Data**: Integration with options chain historical data
- 🌐 **Multiple Sources**: Fallback to Alpha Vantage, Quandl if Yahoo fails
- ⚡ **Parallel Processing**: Multi-threaded downloads for faster acquisition
- 📱 **Progress UI**: Real-time web dashboard for acquisition monitoring

---

## 🎉 Ready to Begin?

Run the setup script to start acquiring 20 years of authentic market data:

```powershell
.\setup_and_run_acquisition.ps1
```

This will transform your ODTE system with comprehensive historical data for robust genetic algorithm testing and production-ready strategy validation! 🚀