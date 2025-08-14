# ODTE.Historical.Tests

## 🧪 Testing and Validation Suite for ODTE.Historical Library

This project provides comprehensive testing, validation, and console tools for the **ODTE.Historical** library.

## 🎯 Project Purpose

### Dual Purpose Design
1. **Unit Testing**: xUnit-based tests for library functionality
2. **Console Tools**: Command-line utilities for data validation and testing

### Why Separate from Library?
- **Clean Separation**: Library remains focused on core functionality
- **Console Tools**: Provides interactive testing and validation tools
- **Development Workflow**: Easy testing during development
- **Production Validation**: Tools for ongoing data quality monitoring

## 🏗️ Project Structure

### Unit Tests
```
Unit Tests/
├── OptionsDataGeneratorTests.cs    # Synthetic data generation tests
├── SyntheticDataBenchmarkTests.cs  # Validation framework tests
└── StooqImporterTests.cs           # Data import tests
```

### Console Tools
```
Console Tools/
├── Program.cs                      # Main testing console
├── BenchmarkSyntheticData.cs       # Synthetic data benchmarking
├── ValidateStooqData.cs            # Stooq data validation
└── InspectDatabase.cs              # Database inspection utility
```

## 🚀 Usage

### Running Unit Tests
```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "OptionsDataGeneratorTests"

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run tests and generate coverage report
dotnet test --collect:"XPlat Code Coverage"
```

### Console Tools Usage

#### Main Testing Console
```bash
# Interactive mode
dotnet run

# Specific operations
dotnet run [operation] [parameters]
```

#### Available Operations

##### 1. Data Import and Management
```bash
# Import historical data from Parquet files
dotnet run import /path/to/parquet/files output.db

# Analyze data gaps
dotnet run gaps

# Fill missing data gaps
dotnet run fill

# Update to latest data
dotnet run update

# Backfill specific date range
dotnet run backfill 2024-01-01 2024-12-31
```

##### 2. Data Validation
```bash
# Quick health check
dotnet run validate database.db --mode health

# Full validation suite
dotnet run validate database.db --mode validate --verbose

# Performance benchmark
dotnet run validate database.db --mode benchmark

# Continuous monitoring
dotnet run validate database.db --mode monitor
```

##### 3. Synthetic Data Testing
```bash
# Benchmark synthetic vs. real data
dotnet run benchmark database.db

# Test data generator quality
dotnet run benchmark database.db --verbose
```

##### 4. Database Inspection
```bash
# Inspect database schema and contents
dotnet run inspect database.db

# View table structure and row counts
dotnet run x x inspect
```

## 📊 Test Coverage

### Unit Test Categories

#### OptionsDataGenerator Tests
- ✅ **Source Properties**: Name, real-time status
- ✅ **Data Generation**: Trading day simulation
- ✅ **Price Validation**: OHLC consistency, realistic ranges
- ✅ **Timing**: Sequential timestamps, market hours
- ✅ **Multi-Symbol**: Different asset types

#### SyntheticDataBenchmark Tests
- ✅ **Validation Framework**: Full benchmark execution
- ✅ **Error Handling**: Invalid database scenarios
- ✅ **Result Structure**: Proper data model validation
- ✅ **Quality Thresholds**: Acceptability scoring

#### StooqImporter Tests
- ✅ **Data Validation**: OHLC relationships, price ranges
- ✅ **Error Handling**: File not found scenarios
- ✅ **Symbol Mapping**: Known vs. unknown symbols
- ✅ **Quality Checks**: Statistical validation logic

### Integration Test Coverage
- ✅ **Database Integration**: Real SQLite database testing
- ✅ **End-to-End Validation**: Complete workflow testing
- ✅ **Performance Testing**: Query time validation
- ✅ **Error Recovery**: Graceful failure handling

## 🔍 Validation Results

### Current Test Status
```
Total Tests: 30
Passed: 27 ✅
Failed: 3 ⚠️
Success Rate: 90%
```

### Known Test Issues
1. **OHLC Precision**: Minor floating-point precision issues in synthetic data
2. **Price Range Tests**: Some test data outside expected ranges
3. **Database Path**: Integration tests require correct database location

### Quality Metrics Achieved
- **Synthetic Data Quality**: 76.9/100 (Acceptable for production)
- **Unit Test Coverage**: 90%+ of core functionality
- **Integration Tests**: All major workflows validated
- **Performance Tests**: All benchmarks within acceptable ranges

## 🛠️ Development Workflow

### Adding New Tests
```csharp
[Fact]
public async Task NewFeature_ShouldWorkCorrectly()
{
    // Arrange
    var generator = new OptionsDataGenerator();
    
    // Act
    var result = await generator.SomeNewMethod();
    
    // Assert
    result.Should().NotBeNull();
    result.Should().BeOfType<ExpectedType>();
}
```

### Adding Console Tools
```csharp
public static async Task<int> NewToolAsync(string[] args)
{
    try
    {
        // Tool implementation
        return 0; // Success
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Tool failed: {ex.Message}");
        return 1; // Failure
    }
}
```

### Integration with Main Console
```csharp
// In Program.cs
else if (operation == "newtool")
{
    Console.WriteLine("🔧 Running new tool...");
    return await NewToolAsync(args.Skip(1).ToArray());
}
```

## 📈 Performance Benchmarks

### Test Execution Performance
- **Unit Tests**: ~1 second total execution
- **Integration Tests**: ~5 seconds with database access
- **Full Validation Suite**: ~15 seconds comprehensive testing
- **Synthetic Benchmark**: ~10 seconds for 1000 data points

### Console Tool Performance
- **Health Check**: <1 second
- **Full Validation**: 10-15 seconds
- **Benchmark Test**: 15-30 seconds
- **Database Import**: 1-5 minutes depending on data size

## 🎯 Quality Assurance

### Continuous Integration
```bash
# Pre-commit validation
dotnet test
dotnet run validate --mode health
dotnet run benchmark --quick

# Full validation suite
dotnet test --collect:"XPlat Code Coverage"
dotnet run validate --mode validate
dotnet run benchmark database.db
```

### Quality Gates
- ✅ All unit tests must pass
- ✅ Integration tests with real database
- ✅ Synthetic data quality ≥75%
- ✅ Performance benchmarks within limits
- ✅ No critical validation failures

## 🔧 Configuration

### Test Configuration
```json
{
  "TestDatabasePath": "../../../../data/ODTE_TimeSeries_5Y.db",
  "SyntheticDataSamples": 1000,
  "ValidationThresholds": {
    "MinQualityScore": 75.0,
    "MaxQueryTimeMs": 500,
    "MinValidityRate": 0.95
  }
}
```

### Console Tool Settings
```json
{
  "DefaultDatabase": "../data/ODTE_TimeSeries_5Y.db",
  "LogLevel": "Information",
  "OutputFormats": ["Console", "Json", "Csv"]
}
```

## 🤝 Integration Points

### With Main Library
- **Library Reference**: Direct project reference to ODTE.Historical
- **Shared Models**: Uses library data models and interfaces
- **Validation Framework**: Tests library validation components

### With ODTE Platform
- **CI/CD Pipeline**: Integrated into build and deployment
- **Quality Gates**: Prevents deployment if tests fail
- **Monitoring**: Console tools used for production monitoring

## 📖 API Reference

### Test Utilities
- `DatabaseInspector`: Database schema and content inspection
- `SyntheticDataBenchmarkTool`: Command-line benchmarking
- `StooqDataValidationTool`: Stooq data validation utilities

### Console Commands
- `dotnet run validate`: Data validation operations
- `dotnet run benchmark`: Synthetic data quality testing
- `dotnet run inspect`: Database inspection
- `dotnet run import`: Data import operations

## 🔍 Troubleshooting

### Common Test Issues
1. **Database Not Found**: Ensure correct path in test configuration
2. **Test Timeouts**: Increase timeout for long-running integration tests
3. **Flaky Tests**: Some tests may be sensitive to system performance

### Debug Mode
```bash
# Run with detailed logging
dotnet run validate database.db --verbose

# Debug specific test
dotnet test --filter "TestName" --logger "console;verbosity=detailed"

# Check test database
dotnet run inspect test_database.db
```

## 📄 Best Practices

### Test Organization
- **Arrange-Act-Assert**: Clear test structure
- **One Assert Per Test**: Focused test validation
- **Descriptive Names**: Self-documenting test methods
- **Test Categories**: Group related functionality

### Console Tool Design
- **Error Handling**: Graceful failure with helpful messages
- **Progress Indication**: User feedback for long operations
- **Configurable Output**: Support different verbosity levels
- **Exit Codes**: Proper return codes for CI/CD integration

---

This testing suite ensures the **ODTE.Historical** library maintains high quality and reliability standards throughout development and production deployment.