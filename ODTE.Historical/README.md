# ODTE.Historical

## 📊 Historical Data Management Library

**ODTE.Historical** is a comprehensive .NET library for managing historical market data, synthetic data generation, and data quality validation within the ODTE dual-strategy trading platform. Supports both PM250 (profit maximization) and PM212 (capital preservation) strategies with institutional-grade data quality.

## 🎯 Core Features

### 📈 **Historical Data Management**
- **SQLite Single Source of Truth**: Centralized market data storage
- **Multi-Source Integration**: Stooq free data, OPRA feeds, custom sources
- **Time Series Optimization**: Efficient querying and indexing
- **Data Consolidation**: Parquet → SQLite conversion pipeline

### 🧬 **Synthetic Data Generation**
- **OptionsDataGenerator**: Research-based synthetic options data
- **Market Regime Modeling**: Calm, stressed, and crisis scenarios
- **Volatility Surface**: Realistic smile dynamics and term structure
- **Jump-Diffusion**: Tail event and crash simulation

### 🔍 **Data Quality Validation**
- **Statistical Validation**: Random sampling with 95%+ confidence
- **Performance Monitoring**: Real-time query benchmarking
- **Cross-Validation**: Synthetic vs. historical data comparison
- **Alert System**: Automated quality degradation detection

### 🗃️ **Data Sources**
- **Stooq Integration**: Free historical data with validation
- **Market Microstructure**: Bid-ask spreads and volume modeling for realistic fills
- **Options Greeks**: Delta, Gamma, Theta, Vega calculations
- **VIX Term Structure**: Forward-looking volatility modeling
- **Execution Data**: NBBO compliance, latency metrics, slippage parameters
- **Audit Trail**: Institutional compliance data for PM212 strategy validation

## 🏗️ Architecture

### Library Structure
```
ODTE.Historical/
├── Core/
│   ├── OptionsDataGenerator.cs        # Synthetic data generation
│   ├── HistoricalDataManager.cs       # Data management
│   ├── TimeSeriesDatabase.cs          # SQLite operations
│   └── DataIngestionEngine.cs         # Import pipeline
├── Validation/
│   ├── SyntheticDataBenchmark.cs      # Quality validation
│   ├── StooqDataValidator.cs          # Stooq data validation
│   └── OptionsDataQualityValidator.cs # Options-specific validation
├── Monitoring/
│   └── StooqPerformanceMonitor.cs     # Performance tracking
└── Sources/
    ├── StooqMarketDataSource.cs       # Stooq integration
    └── EnhancedMarketDataSource.cs    # Advanced data sources
```

### Testing Structure
```
ODTE.Historical.Tests/
├── Unit Tests/
│   ├── OptionsDataGeneratorTests.cs   # Synthetic data tests
│   ├── SyntheticDataBenchmarkTests.cs # Validation tests
│   └── StooqImporterTests.cs          # Import tests
└── Console Tools/
    ├── Program.cs                     # Main testing console
    ├── BenchmarkSyntheticData.cs      # Benchmark tool
    ├── ValidateStooqData.cs           # Validation tool
    └── InspectDatabase.cs             # Database inspector
```

## 🚀 Quick Start

### Installation
```bash
# Add library reference
dotnet add reference ../ODTE.Historical/ODTE.Historical.csproj

# Or install NuGet package
dotnet add package ODTE.Historical
```

### Basic Usage

#### Historical Data Access
```csharp
using ODTE.Historical;

// Initialize data manager
using var manager = new HistoricalDataManager();
await manager.InitializeAsync();

// Query market data
var data = await manager.GetMarketDataAsync("SPY", 
    DateTime.Today.AddDays(-30), 
    DateTime.Today);
```

#### Synthetic Data Generation
```csharp
using ODTE.Historical;

// Generate synthetic options data
var generator = new OptionsDataGenerator();
var syntheticData = await generator.GenerateTradingDayAsync(
    DateTime.Today, "SPY");

Console.WriteLine($"Generated {syntheticData.Count} data points");
```

#### Data Quality Validation
```csharp
using ODTE.Historical.Validation;
using Microsoft.Extensions.Logging;

// Validate data quality
var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<SyntheticDataBenchmark>();
var benchmark = new SyntheticDataBenchmark("market_data.db", logger);
var result = await benchmark.RunBenchmarkAsync();

Console.WriteLine($"Data quality score: {result.OverallScore}/100");
```

## 🧪 Testing and Validation

### Running Tests
```bash
# Run all unit tests
cd ODTE.Historical.Tests
dotnet test

# Run validation console
dotnet run validate database.db

# Run synthetic data benchmark
dotnet run benchmark database.db

# Inspect database schema
dotnet run inspect database.db
```

### Console Operations
```bash
# Available operations
dotnet run [operation] [parameters]

Operations:
  import      - Import historical data from Parquet files
  validate    - Run Stooq data quality validation
  benchmark   - Test synthetic data vs. real data
  inspect     - Inspect database schema and contents
  gaps        - Analyze missing trading days
  fill        - Fill data gaps with synthetic data
  update      - Update to latest trading data
  backfill    - Fill specific date range
```

## 📊 Data Quality Standards

### Validation Thresholds
- **Overall Quality Score**: ≥75% for production use
- **Statistical Similarity**: Mean (≥95%), Volatility (≥80%), Skewness (≥70%)
- **Performance**: Basic queries <100ms, Analytical queries <500ms
- **Data Integrity**: Random sampling 95%+ validity rate

### Quality Metrics
- **Excellent (85%+)**: Production ready, exceeds standards
- **Good (75-84%)**: Acceptable for production with monitoring
- **Needs Improvement (60-74%)**: Requires calibration adjustments
- **Poor (<60%)**: Not suitable for strategy validation

## 🔧 Configuration

### Database Configuration
```json
{
  "ConnectionString": "Data Source=market_data.db",
  "ValidationSampleSize": 100,
  "PerformanceThresholds": {
    "BasicQueryMs": 100,
    "AnalyticalQueryMs": 500,
    "RandomAccessMs": 100
  }
}
```

### Synthetic Data Parameters
```json
{
  "VolatilitySurface": {
    "BaseVolatility": 0.20,
    "VolOfVol": 0.15,
    "MeanReversion": 0.8
  },
  "JumpDiffusion": {
    "JumpIntensity": 0.1,
    "JumpMagnitude": 0.02
  },
  "MarketRegimes": {
    "VolatilityThreshold": 0.25,
    "TrendThreshold": 0.15
  }
}
```

## 📈 Performance Benchmarks

### Library Performance (Tested)
- **OptionsDataGenerator**: 76.9/100 quality score ✅
- **Data Import**: 10,000+ records in <2 seconds
- **Query Performance**: <50ms average response time
- **Memory Usage**: <100MB for 1M+ data points
- **Validation**: Statistical confidence with 5% sampling

### Synthetic Data Quality
- **Mean Matching**: 99.9% accuracy
- **Volatility Replication**: 92.4% accuracy  
- **Distribution Properties**: 78-86% matching
- **Market Regime Simulation**: 72-88% accuracy

## 🤝 Integration with ODTE Platform

### Used By
- **ODTE.Strategy**: PM250/PM212 dual-strategy data requirements
- **ODTE.Execution**: Market microstructure data for realistic fill simulation
- **ODTE.Optimization**: Historical data for backtesting both strategies
- **ODTE.Syntricks**: Baseline data for stress testing
- **Options.Start**: Real-time data dashboard
- **audit/**: PM212 institutional audit compliance validation

### Dependencies
- **ODTE.Backtest**: Options math and pricing models
- **ODTE.Execution**: Integration with realistic execution modeling
- **Microsoft.Data.Sqlite**: Database operations
- **Dapper**: Object-relational mapping
- **Microsoft.Extensions.Logging**: Logging framework

## 📖 API Reference

### Core Classes
- `HistoricalDataManager`: Main data management interface
- `OptionsDataGenerator`: Synthetic data generation
- `TimeSeriesDatabase`: SQLite operations
- `DataIngestionEngine`: Data import pipeline

### Validation Classes
- `SyntheticDataBenchmark`: Quality validation framework
- `StooqDataValidator`: Stooq-specific validation
- `StooqPerformanceMonitor`: Real-time monitoring

### Data Sources
- `StooqImporter`: Free data source integration
- `EnhancedMarketDataSource`: Advanced data sources

## 🔍 Troubleshooting

### Common Issues
1. **Database not found**: Ensure correct path to SQLite file
2. **Schema mismatch**: Run `dotnet run inspect` to verify structure
3. **Quality degradation**: Check validation reports and recalibrate
4. **Performance issues**: Monitor query times and optimize indexes

### Debug Commands
```bash
# Check database health
dotnet run validate --mode health

# Monitor performance
dotnet run validate --mode monitor

# Run full benchmark
dotnet run validate --mode benchmark
```

## 📄 License

Part of the ODTE trading platform. See main project license.

## 🚀 Version History

- **v1.0.0**: Initial library release with full feature set
  - Historical data management
  - Synthetic data generation (76.9% quality score)
  - Comprehensive validation framework
  - Console testing tools
  - Production-ready quality standards