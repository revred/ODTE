# 📏  ODTE Coding Guidelines & Standards

## 🎯  Overview

This document establishes comprehensive coding standards, formatting guidelines, and project organization rules for the ODTE trading platform to ensure consistency, maintainability, and professional presentation across all codebase assets.

## 🎨  Aesthetic & Formatting Standards

### Icon Usage Standards
**Always add double space after Unicode icons/emojis in all markdown content:**

✅  **Correct Usage:**
```markdown
## 🚀  Quick Start
### 📊  Data Analysis  
- 🎯  Target Achievement
- ✅  Completed Task
```

❌  **Incorrect Usage:**
```markdown
## 🚀 Quick Start          # Missing space
### 📊Data Analysis        # No space at all
- 🎯Target Achievement     # No space
```

### Applicable Contexts
- **Headers**: All levels (H1-H6)
- **Bullet points**: Both ordered and unordered lists
- **Inline text**: When icons precede descriptive text
- **Code comments**: When using icons in documentation comments
- **Status indicators**: ✅ ❌ ⚠️ 🔄 usage

### Common Icons & Proper Spacing
```markdown
🎯  Project goals and targets
🚀  Quick start guides and launches  
📊  Data analysis and metrics
🏗️  Architecture and infrastructure
📈  Performance and growth
⚡  Speed and optimization
🔧  Tools and utilities
🛡️  Security and risk management
📋  Documentation and procedures  
🎭  Testing and simulation
🧬  Genetic algorithms and evolution
📁  File and folder organization
💰  Financial results and P&L
🔍  Search and investigation
✅  Success and completion
❌  Errors and failures
⚠️  Warnings and cautions
📚  Knowledge and learning
🔄  Process and workflow
🎮  Interactive tools and interfaces
```

## 📁  Project Organization Standards

### Root Directory Policy
**The root directory MUST remain clean and organized. Random files and folders are prohibited.**

#### ✅  Allowed in Root Directory
```
ODTE/
├── CLAUDE.md                    # Main project instructions
├── README.md                    # Project overview
├── COLD_START.md               # Quick alignment guide
├── LICENSE                     # License file
├── ODTE.sln                    # Solution file
├── Directory.Build.props       # Build configuration
├── Documentation/              # Consolidated documentation
├── [ProjectName]/              # Organized project folders
└── data/                       # Core data assets
```

#### ❌  Prohibited in Root Directory
- Random `.md` files without clear purpose
- Temporary analysis files
- Tool-specific outputs
- Test result files
- Individual script files
- Experimental code files
- Personal notes or scratch files

### Proper File Organization

#### Documentation Files
**All documentation MUST be organized in the `Documentation/` folder:**

```
Documentation/
├── CODING_GUIDELINES.md           # This file
├── LONG_OPTIONS_LOG_SPECIFICATION.md
├── HISTORICAL_DATA_COMPREHENSIVE_GUIDE.md
├── PM250_Complete_20Year_Analysis_Summary.md
└── [Specific topic documentation]
```

#### Tool and Utility Files
**Tools MUST be organized within their respective project folders:**

```
ODTE.ProjectName/
├── Tools/                      # Project-specific tools
├── Scripts/                    # Automation scripts
├── Utilities/                  # Helper utilities
└── Tests/                      # Testing tools
```

#### Analysis and Research Files
**Research files MUST be organized in appropriate folders:**

```
Options.OPM/
├── PM250Tools/                 # PM250 analysis tools
├── PM212Tools/                 # PM212 validation tools
└── Documentation/              # Strategy documentation

Archive/
├── Research/                   # Historical research
├── Reports/                    # Generated reports
└── LegacyCode/                 # Deprecated code
```

## 🔧  Code Standards

### File Naming Conventions

#### C# Files
```csharp
// ✅ Correct naming
PM250_OptimizedStrategy.cs         # Clear, descriptive
HistoricalDataManager.cs           # PascalCase
IOptionsDataProvider.cs            # Interface prefix 'I'

// ❌ Incorrect naming  
strategy.cs                        # Too generic
pm250optimization.cs               # No separators
TestFile123.cs                     # Unclear purpose
```

#### Markdown Files
```markdown
✅ Correct naming:
CODING_GUIDELINES.md               # All caps for standards
PM250_Complete_Analysis.md         # Descriptive with underscores
HISTORICAL_DATA_GUIDE.md          # Clear purpose

❌ Incorrect naming:
notes.md                          # Too generic
temp.md                           # Temporary files
myanalysis.md                     # Personal naming
```

### Documentation Standards

#### Header Structure
**All markdown files MUST follow this header pattern:**

```markdown
# 📊  Document Title - Clear Purpose

## 🎯  Overview
Brief description of the document's purpose and scope.

## 📋  Contents
What the reader will find in this document.
```

#### Code Documentation
**All C# classes MUST include proper XML documentation:**

```csharp
/// <summary>
/// PM250 strategy implementation with genetic optimization
/// and adaptive risk management for profit maximization.
/// </summary>
/// <remarks>
/// This strategy is designed for Bull market conditions with VIX < 19.
/// Uses RevFib scaling for position management.
/// </remarks>
public class PM250OptimizedStrategy
{
    /// <summary>
    /// Executes a trade with the specified parameters.
    /// </summary>
    /// <param name="entryConditions">Market conditions for trade entry</param>
    /// <returns>Trade execution result with P&L details</returns>
    public TradeResult ExecuteTrade(MarketConditions entryConditions)
    {
        // Implementation
    }
}
```

## 🗂️  Folder Structure Standards

### Project-Specific Guidelines

#### Strategy Projects
```
ODTE.Strategy/
├── Strategies/                 # Core strategy implementations
├── RiskManagement/            # Risk management classes  
├── Configuration/             # Strategy configuration
├── Interfaces/                # Strategy contracts
└── Tests/                     # Strategy-specific tests
```

#### Data Projects  
```
ODTE.Historical/
├── DataProviders/             # Multi-source data providers
├── Validation/                # Data quality validation
├── Examples/                  # Usage examples
├── Archive/                   # Historical data storage
└── Tests/                     # Data validation tests
```

#### Analysis Projects
```
Options.OPM/
├── PM250Tools/                # PM250 analysis and optimization
├── PM212Tools/                # PM212 validation and audit  
├── Documentation/             # Strategy-specific documentation
└── SharedUtilities/           # Common analysis tools
```

### Testing Organization
```
[Project].Tests/
├── Unit/                      # Unit tests
├── Integration/               # Integration tests  
├── Performance/               # Performance benchmarks
├── Compliance/                # Audit compliance tests
└── TestData/                  # Test data assets
```

## 📊  Performance & Quality Standards

### Code Quality Requirements

#### Automated Testing
- **Minimum 80% code coverage** for all production classes
- **All public methods MUST have unit tests**  
- **Integration tests required** for external dependencies
- **Performance benchmarks** for time-critical operations

#### Performance Standards
```csharp
// ✅ Example: Properly optimized data access
public async Task<IEnumerable<TradeResult>> GetTradeHistoryAsync(
    DateTime startDate, 
    DateTime endDate)
{
    // Use connection pooling and prepared statements
    using var connection = _connectionPool.GetConnection();
    var query = _preparedQueries.GetTradeHistoryQuery();
    
    return await connection.QueryAsync<TradeResult>(
        query, 
        new { StartDate = startDate, EndDate = endDate });
}
```

#### Memory Management
- **Dispose patterns** for all IDisposable resources
- **Async/await patterns** for I/O operations  
- **Connection pooling** for database operations
- **Memory profiling** for large dataset operations

### Documentation Quality
- **All public APIs documented** with XML comments
- **Usage examples provided** for complex functionality
- **Performance characteristics documented** for critical methods
- **Thread safety explicitly documented** where applicable

## 🔐  Security & Compliance Standards

### Data Protection
```csharp
// ✅ Correct: Never log sensitive data
_logger.LogInformation("Trade executed: ID={TradeId}, Symbol={Symbol}", 
    trade.Id, trade.Symbol);

// ❌ Incorrect: Exposing sensitive information  
_logger.LogDebug("Trade details: {TradeData}", JsonSerializer.Serialize(trade));
```

### API Key Management
```csharp
// ✅ Correct: Environment variable usage
var apiKey = Environment.GetEnvironmentVariable("POLYGON_API_KEY");
if (string.IsNullOrEmpty(apiKey))
    throw new InvalidOperationException("API key not configured");

// ❌ Incorrect: Hardcoded secrets
private const string API_KEY = "your_secret_key_here";
```

### Audit Trail Requirements
- **All financial calculations MUST be logged**
- **Strategy parameter changes MUST be tracked**  
- **Data source information MUST be preserved**
- **Git commit IDs MUST be embedded** in trading records

## 🚀  Development Workflow

### Version Control Standards

#### Commit Message Format
```bash
✅ Correct commit messages:
🎯 Add PM250 genetic optimization framework
📊 Update historical data validation with NBBO compliance  
🛡️ Implement RevFib risk management guardrails
🔧 Fix execution quality scoring algorithm

❌ Incorrect commit messages:
"fixed stuff"
"update"
"WIP"
"temp commit"
```

#### Branch Naming
```bash
✅ Correct branch names:
feature/pm250-genetic-optimization
bugfix/historical-data-validation
hotfix/revfib-calculation-error
improvement/execution-performance

❌ Incorrect branch names:
"my-branch"
"test"
"temp"
"branch123"
```

### Code Review Requirements
- **All code MUST be reviewed** before merging to main
- **Performance impact MUST be assessed** for critical paths
- **Security implications MUST be evaluated** for data handling
- **Documentation MUST be updated** for public API changes

## 📋  Enforcement & Compliance

### Automated Checks
The following will be enforced through automated tooling:

1. **Icon spacing validation** in markdown files
2. **File organization compliance** checking  
3. **Code coverage requirements** for all merges
4. **Documentation coverage** for public APIs
5. **Performance regression detection** in CI/CD

### Manual Review Items
1. **Strategic alignment** with ODTE goals
2. **Institutional compliance** for trading code  
3. **Documentation clarity** and completeness
4. **User experience** for developer tools

### Violation Consequences
- **Build failures** for guideline violations
- **PR rejection** for non-compliant code
- **Refactoring requirements** for existing violations
- **Documentation updates** for guideline changes

## 🎯  Summary Checklist

Before committing any code or documentation:

- [ ] ✅  Icons have proper double-space formatting
- [ ] 📁  Files are organized in appropriate folders  
- [ ] 📋  Documentation follows header standards
- [ ] 🔧  Code includes proper XML documentation
- [ ] 🧪  Tests cover all public functionality
- [ ] 🔐  No sensitive data in logs or comments
- [ ] 📊  Performance impact has been considered
- [ ] 🎯  Changes align with ODTE strategic goals

---

**Version**: 1.0.0  
**Last Updated**: August 17, 2025  
**Status**: ✅  ENFORCED - Mandatory for all contributions  
**Review Cycle**: Quarterly updates with team feedback