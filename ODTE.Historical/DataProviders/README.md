# ODTE Multi-Source Data Provider System

A robust, fault-tolerant data fetching system that prevents API blocks by using multiple data sources with automatic failover.

## Overview

This system addresses the problem where Claude's single-source data fetching would get blocked by API providers. Instead, it:

- **Uses multiple data sources** with automatic failover
- **Implements intelligent rate limiting** to prevent hitting API limits
- **Validates data quality** across providers
- **Caches results** to reduce API calls
- **Consolidates data** from multiple sources for accuracy
- **Integrates seamlessly** with ODTE's existing pipeline

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                MultiSourceDataFetcher                      │
│  ┌─────────────────┐ ┌─────────────────┐ ┌──────────────┐  │
│  │  Polygon.io     │ │  Alpha Vantage  │ │ Twelve Data  │  │
│  │  (Priority 1)   │ │  (Priority 2)   │ │ (Priority 3) │  │
│  └─────────────────┘ └─────────────────┘ └──────────────┘  │
│             │                 │                │           │
│             └─────────────────┼────────────────┘           │
│                              │                             │
│  ┌─────────────────┐        │        ┌─────────────────┐   │
│  │   Rate Limiter  │◄───────┼───────►│   Data Cache    │   │
│  └─────────────────┘        │        └─────────────────┘   │
│                              │                             │
│  ┌─────────────────┐        │        ┌─────────────────┐   │
│  │ Health Monitor  │◄───────┼───────►│  Data Validator │   │
│  └─────────────────┘        │        └─────────────────┘   │
│                              ▼                             │
└──────────────────────────────────────────────────────────┘
             │
             ▼
┌─────────────────────────────────────────────────────────────┐
│              ODTE Historical Database                       │
│  ┌─────────────────┐ ┌─────────────────┐ ┌──────────────┐  │
│  │  Parquet Files  │ │  SQLite DB      │ │  Time Series │  │
│  └─────────────────┘ └─────────────────┘ └──────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

## Features

### 1. Multiple Data Providers

- **Polygon.io** (Primary) - High-quality options data
- **Alpha Vantage** (Secondary) - Free tier available
- **Twelve Data** (Tertiary) - Comprehensive market data

### 2. Automatic Failover

- Providers ranked by priority and health
- Automatic switching when primary fails
- Health monitoring and recovery tracking

### 3. Rate Limiting

- Per-provider rate limiting
- Adaptive throttling based on API responses
- Automatic retry with exponential backoff

### 4. Data Quality Validation

- Validates bid/ask spreads
- Checks price reasonableness
- Ensures data completeness
- Cross-provider consistency checks

### 5. Intelligent Caching

- 15-minute default cache duration
- Memory-efficient storage
- Automatic cache cleanup

## Usage

### Setup API Keys

Set environment variables for the providers you want to use:

```bash
# Windows
set POLYGON_API_KEY=your_polygon_key_here
set ALPHA_VANTAGE_API_KEY=your_alpha_vantage_key_here
set TWELVE_DATA_API_KEY=your_twelve_data_key_here

# Linux/Mac
export POLYGON_API_KEY=your_polygon_key_here
export ALPHA_VANTAGE_API_KEY=your_alpha_vantage_key_here
export TWELVE_DATA_API_KEY=your_twelve_data_key_here
```

### Basic Usage

```csharp
using ODTE.Historical.DataProviders;

// Initialize the enhanced fetcher
using var fetcher = new EnhancedHistoricalDataFetcher();

// Fetch data for a date range
var result = await fetcher.FetchAndConsolidateDataAsync(
    "SPY", 
    DateTime.Today.AddDays(-30), 
    DateTime.Today);

if (result.Success)
{
    Console.WriteLine($"Successfully processed {result.TotalDaysProcessed} days");
    Console.WriteLine($"Success rate: {result.SuccessRate:P1}");
}
```

### Advanced Usage

```csharp
// Get provider status
var statuses = await fetcher.GetProviderStatusAsync();
foreach (var status in statuses)
{
    Console.WriteLine($"{status.ProviderName}: {status.SuccessRate:P1} success rate, " +
                     $"{status.RequestsRemaining} requests remaining");
}

// Validate data quality
var qualityReport = await fetcher.ValidateDataQualityAsync("SPY", DateTime.Today.AddDays(-1));
if (qualityReport.IsValid)
{
    Console.WriteLine($"Data from {qualityReport.Sources.Count} sources");
    Console.WriteLine($"Average bid-ask spread: {qualityReport.QualityMetrics?.AverageBidAskSpread:P2}");
}
```

## Provider Configuration

### Polygon.io
- **Best for**: High-quality options chains, real-time data
- **Free tier**: 5 requests/minute
- **Paid tier**: Up to 1000 requests/minute
- **Signup**: https://polygon.io/

### Alpha Vantage
- **Best for**: Historical data, basic options
- **Free tier**: 5 requests/minute, 500/day
- **Paid tier**: Higher limits available
- **Signup**: https://www.alphavantage.co/

### Twelve Data
- **Best for**: Comprehensive market data with Greeks
- **Free tier**: 8 requests/minute, 800/day
- **Paid tier**: Real-time data available
- **Signup**: https://twelvedata.com/

## Error Handling

The system gracefully handles:

- **API rate limits** - Automatic throttling and retry
- **Network timeouts** - Failover to next provider
- **Invalid data** - Quality validation and filtering
- **Provider downtime** - Health monitoring and recovery

## Integration with ODTE

The system integrates seamlessly with ODTE's existing pipeline:

1. **Fetches data** from multiple sources
2. **Validates quality** using ODTE's standards
3. **Saves to parquet** format for backtesting
4. **Updates SQLite** database for quick access
5. **Maintains compatibility** with existing code

## Performance Optimizations

- **Parallel fetching** when possible
- **Intelligent caching** to reduce API calls  
- **Batch processing** for efficiency
- **Memory management** for large datasets
- **Connection pooling** for HTTP requests

## Monitoring

### Provider Health
```csharp
var statuses = await fetcher.GetProviderStatusAsync();
foreach (var status in statuses)
{
    if (!status.IsHealthy)
    {
        logger.LogWarning($"Provider {status.ProviderName} is unhealthy: " +
                         $"{status.ConsecutiveFailures} consecutive failures");
    }
}
```

### Data Quality
```csharp
var report = await fetcher.ValidateDataQualityAsync("SPY", date);
if (report.QualityMetrics?.AverageBidAskSpread > 0.1) // 10% spread
{
    logger.LogWarning($"Wide spreads detected: {report.QualityMetrics.AverageBidAskSpread:P2}");
}
```

## Testing

Run the comprehensive test suite:

```bash
cd ODTE.Historical.Tests
dotnet test --filter "DataProviderTests"
```

Tests cover:
- Rate limiting behavior
- Cache functionality
- Provider failover
- Data validation
- Error handling

## Contributing

When adding new providers:

1. Implement `IOptionsDataProvider`
2. Add rate limiting appropriate for the API
3. Include comprehensive error handling
4. Add validation for the data format
5. Write unit tests
6. Update this documentation

## Troubleshooting

### Common Issues

**Q: "No available data providers" error**
A: Set environment variables for at least one provider's API key

**Q: Data fetch is slow**
A: Check provider rate limits, consider upgrading to paid tiers

**Q: Inconsistent data across providers**
A: Use consolidated data fetching for consensus pricing

**Q: High memory usage**
A: Adjust cache duration or implement disk-based caching

### Debug Mode

Enable detailed logging:

```csharp
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder => 
    builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

var fetcher = new EnhancedHistoricalDataFetcher(
    logger: loggerFactory.CreateLogger<EnhancedHistoricalDataFetcher>());
```

This will show detailed information about provider selection, rate limiting, caching, and data validation.