# 📊 ODTE Strategy Versioning System

## 🎯 Purpose: Traceable Strategy Evolution
**Create a comprehensive version control system for all trading strategies, enabling systematic comparison, rollback capabilities, and performance attribution across strategy iterations.**

## 📋 Version Naming Convention

### Standard Format:
```
{StrategyName}_v{Major}.{Minor}.{Patch}_{YYYYMMDD}
```

### Examples:
```yaml
PM250_v2.1.3_20250816        # PM250 strategy, version 2.1.3, saved Aug 16, 2025
IronCondor_v1.0.0_20250815   # Initial Iron Condor release
BWB_v3.2.1_20250820          # Broken Wing Butterfly, 3rd major version
TailOverlay_v1.5.0_20250825  # Tail Overlay strategy enhancement
```

### Version Number Semantics:
```yaml
Major (X.0.0):
  - Fundamental strategy logic changes
  - New entry/exit rule paradigms  
  - Complete risk management overhauls
  - Breaking changes to parameter structure
  
Minor (0.X.0):
  - Parameter optimization updates
  - New filter additions
  - Risk management enhancements
  - Performance improvements
  
Patch (0.0.X):
  - Bug fixes and corrections
  - Minor parameter tweaks
  - Documentation updates
  - Code refactoring without logic changes
```

---

## 🗂️ Storage Structure

### Directory Organization:
```
ODTE/
├── Strategies/
│   ├── PM250/
│   │   ├── versions/
│   │   │   ├── v2.1.3_20250816/
│   │   │   │   ├── parameters.json
│   │   │   │   ├── performance.json
│   │   │   │   ├── backtest_results.csv
│   │   │   │   ├── optimization_log.md
│   │   │   │   ├── stress_test_results.json
│   │   │   │   ├── code_snapshot/
│   │   │   │   │   ├── PM250_OptimizedStrategy.cs
│   │   │   │   │   ├── PM250_GeneticOptimizer_v2.cs
│   │   │   │   │   └── configuration.json
│   │   │   │   └── deployment_notes.md
│   │   │   ├── v2.1.2_20250815/
│   │   │   ├── v2.1.1_20250814/
│   │   │   └── v2.1.0_20250810/
│   │   ├── current_production.json -> v2.1.3_20250816
│   │   ├── comparison_reports/
│   │   │   ├── v2.1.3_vs_v2.1.2.md
│   │   │   ├── v2.1.2_vs_v2.1.1.md
│   │   │   └── monthly_performance_comparison.csv
│   │   └── README.md
│   ├── IronCondor/
│   │   ├── versions/
│   │   ├── current_production.json
│   │   └── comparison_reports/
│   └── BWB/
│       ├── versions/
│       ├── current_production.json
│       └── comparison_reports/
```

---

## 📄 Version Tracking Elements

### 1. Core Parameters (`parameters.json`)
```json
{
  "version": "PM250_v2.1.3_20250816",
  "strategy_name": "PM250_OptimizedStrategy",
  "creation_date": "2025-08-16T14:30:00Z",
  "created_by": "genetic_optimizer_v2",
  "optimization_period": {
    "start_date": "2020-01-01",
    "end_date": "2025-08-15"
  },
  "core_parameters": {
    "short_delta": 0.152,
    "width_points": 2.3,
    "credit_ratio": 0.086,
    "stop_multiple": 2.15,
    "go_score_base": 67.5,
    "go_score_vol_adj": 8.2,
    "go_score_trend_adj": -3.1
  },
  "risk_parameters": {
    "max_position_size": 15.0,
    "position_scaling": 1.25,
    "drawdown_reduction": 0.62,
    "recovery_boost": 1.35
  },
  "fibonacci_levels": {
    "fib_level_1": 500.0,
    "fib_level_2": 300.0,
    "fib_level_3": 200.0,
    "fib_level_4": 100.0,
    "fib_reset_profit": 150.0
  },
  "market_adaptation": {
    "bull_market_aggression": 1.25,
    "bear_market_defense": 0.75,
    "high_vol_reduction": 0.55,
    "low_vol_boost": 1.45
  },
  "parameter_confidence": {
    "short_delta": 0.92,
    "width_points": 0.87,
    "credit_ratio": 0.94,
    "optimization_quality_score": 0.89
  }
}
```

### 2. Performance Metrics (`performance.json`)
```json
{
  "version": "PM250_v2.1.3_20250816",
  "backtest_performance": {
    "period": "2020-01-01 to 2025-08-15",
    "total_trades": 1247,
    "win_rate": 73.2,
    "average_trade_profit": 16.85,
    "total_pnl": 21008.95,
    "max_drawdown": 8.4,
    "sharpe_ratio": 1.87,
    "calmar_ratio": 2.23,
    "profit_factor": 2.14,
    "largest_win": 89.50,
    "largest_loss": -187.30,
    "avg_win": 28.45,
    "avg_loss": -13.30,
    "win_loss_ratio": 2.14,
    "recovery_time_days": 12.3
  },
  "regime_performance": {
    "bull_market": {
      "win_rate": 76.8,
      "avg_profit": 18.20,
      "max_drawdown": 6.2
    },
    "bear_market": {
      "win_rate": 68.5,
      "avg_profit": 14.90,
      "max_drawdown": 11.1
    },
    "sideways_market": {
      "win_rate": 75.3,
      "avg_profit": 17.35,
      "max_drawdown": 7.8
    },
    "high_volatility": {
      "win_rate": 69.2,
      "avg_profit": 15.60,
      "max_drawdown": 9.9
    }
  },
  "forward_test_performance": {
    "period": "2025-07-01 to 2025-08-15",
    "total_trades": 48,
    "win_rate": 72.9,
    "average_trade_profit": 17.12,
    "total_pnl": 822.76,
    "max_drawdown": 4.8,
    "sharpe_ratio": 1.94,
    "backtest_deviation": -1.4
  },
  "stress_test_results": {
    "march_2020_crash": {
      "survived": true,
      "max_drawdown": 15.2,
      "recovery_days": 18
    },
    "flash_crash_simulation": {
      "survived": true,
      "max_loss_single_day": -287.50,
      "fibonacci_level_reached": 3
    },
    "volatility_explosion": {
      "survived": true,
      "performance_degradation": "12.3%",
      "adaptive_response": "activated"
    }
  }
}
```

### 3. Technical Metadata (`deployment_notes.md`)
```markdown
# PM250_v2.1.3_20250816 Deployment Notes

## Version Summary
- **Previous Version**: PM250_v2.1.2_20250815
- **Key Changes**: Enhanced GoScore volatility adjustment, improved bear market defense
- **Optimization Method**: Genetic Algorithm (200 population, 75 generations)
- **Test Environment**: ODTE.Strategy.Tests with 5-year backtest validation

## Code Changes
- Modified `PM250_GeneticOptimizer_v2.cs` line 66: GoScore volatility adjustment range
- Updated `PM250_OptimizedStrategy.cs` bear market defense logic
- Enhanced Fibonacci risk management triggers

## Performance Improvements
- Average trade profit: $16.43 → $16.85 (+2.6%)
- Win rate: 72.8% → 73.2% (+0.4pp)
- Max drawdown: 9.1% → 8.4% (-0.7pp)
- Sharpe ratio: 1.82 → 1.87 (+2.7%)

## Validation Results
- ✅ Genetic optimization fitness: 2.847 (previous: 2.832)
- ✅ All stress tests passed
- ✅ Parameter stability confirmed
- ✅ Forward test deviation: -1.4% (acceptable)

## Deployment Checklist
- [x] Code committed to repository (hash: a7b9c2d)
- [x] Configuration files updated
- [x] Test suite passed (46/46 tests)
- [x] Performance benchmarks met
- [x] Risk limits validated
- [x] Documentation updated

## Risk Assessment
- **Risk Level**: LOW - Minor parameter adjustments only
- **Rollback Plan**: Revert to v2.1.2 if performance degrades >5% over 30 days
- **Monitoring**: Enhanced monitoring for first 14 days post-deployment

## Next Iteration Roadmap
- Investigate machine learning enhancements for regime detection
- Test expanded parameter ranges for market adaptation
- Consider integration with new volatility prediction models
```

---

## 🔄 Version Management Operations

### 1. Creating New Version
```bash
# Navigate to strategy directory
cd ODTE/Strategies/PM250

# Create new version directory
mkdir versions/v2.1.4_$(date +%Y%m%d)

# Copy optimized parameters
cp current_optimization_results.json versions/v2.1.4_20250817/parameters.json

# Run comprehensive testing
dotnet test ../../ODTE.Strategy.Tests

# Generate performance report
dotnet run --project ../../ODTE.Backtest -- generate-report PM250_v2.1.4

# Archive code snapshot
cp -r ../../ODTE.Strategy/PM250* versions/v2.1.4_20250817/code_snapshot/

# Update production pointer
echo "v2.1.4_20250817" > current_production.json
```

### 2. Version Comparison
```csharp
// Automated version comparison tool
public class VersionComparator
{
    public async Task<ComparisonReport> CompareVersionsAsync(string version1, string version2)
    {
        var v1Data = await LoadVersionData(version1);
        var v2Data = await LoadVersionData(version2);
        
        return new ComparisonReport
        {
            ParameterDifferences = CompareParameters(v1Data.Parameters, v2Data.Parameters),
            PerformanceDelta = ComparePerformance(v1Data.Performance, v2Data.Performance),
            RiskProfileChange = CompareRiskMetrics(v1Data.Risk, v2Data.Risk),
            Recommendation = GenerateRecommendation(v1Data, v2Data)
        };
    }
}
```

### 3. Rollback Procedures
```yaml
Rollback_Process:
  Triggers:
    - Performance degradation >10% for 7+ days
    - Risk limit violations
    - System instability
    - Critical bug discovery
    
  Steps:
    1. Assess_Situation:
       - Quantify performance impact
       - Identify root cause
       - Estimate recovery time
       
    2. Select_Rollback_Target:
       - Last known good version
       - Verify target version stability
       - Check production compatibility
       
    3. Execute_Rollback:
       - Update production pointer
       - Restart trading systems
       - Verify correct deployment
       
    4. Monitor_Recovery:
       - Track performance restoration
       - Validate risk metrics
       - Document lessons learned
```

---

## 📈 Performance Tracking & Comparison

### Version Performance Dashboard
```yaml
Dashboard_Metrics:
  Current_vs_Previous:
    - Win rate comparison
    - Average profit delta
    - Risk metric changes
    - Sharpe ratio evolution
    
  Historical_Trends:
    - Performance over time
    - Parameter drift analysis
    - Optimization effectiveness
    - Degradation patterns
    
  Cross_Strategy_Comparison:
    - Relative performance ranking
    - Risk-adjusted returns
    - Portfolio correlation impact
    - Resource utilization
```

### Automated Alerts
```json
{
  "performance_alerts": {
    "win_rate_degradation": {
      "threshold": "5% below version baseline",
      "window": "30 days",
      "action": "investigate_and_report"
    },
    "profit_decline": {
      "threshold": "$2 below average",
      "window": "50 trades",
      "action": "parameter_review"
    },
    "risk_increase": {
      "threshold": "2% above max drawdown",
      "window": "immediate",
      "action": "emergency_review"
    }
  },
  "technical_alerts": {
    "parameter_drift": {
      "threshold": "10% from optimal",
      "window": "weekly_check",
      "action": "reoptimization_candidate"
    },
    "correlation_increase": {
      "threshold": "0.8 with other strategies",
      "window": "monthly_check",
      "action": "diversification_review"
    }
  }
}
```

---

## 🎯 Integration with ODTE Systems

### Strategy Library Integration
```csharp
// IStrategyEngine enhancement for version management
public interface IVersionedStrategyEngine : IStrategyEngine
{
    Task<List<StrategyVersion>> GetAvailableVersionsAsync(string strategyName);
    Task<StrategyVersion> GetCurrentProductionVersionAsync(string strategyName);
    Task<ComparisonReport> CompareVersionsAsync(string strategyName, string version1, string version2);
    Task<bool> DeployVersionAsync(string strategyName, string version);
    Task<bool> RollbackVersionAsync(string strategyName, string targetVersion);
    Task<VersionHistory> GetVersionHistoryAsync(string strategyName);
}
```

### Genetic Optimization Integration
```csharp
// Enhanced genetic optimizer with automatic versioning
public class VersionedGeneticOptimizer : PM250_GeneticOptimizer_v2
{
    public async Task<OptimizationResult> OptimizeAndVersionAsync(
        string baseVersion, 
        string targetVersion,
        OptimizationConfig config)
    {
        // Load base version parameters
        var baseParams = await LoadVersionParameters(baseVersion);
        
        // Run genetic optimization
        var result = await OptimizeAsync(baseParams, config);
        
        // Create new version if improvement found
        if (result.ImprovementFound)
        {
            await CreateNewVersion(targetVersion, result);
            await GenerateComparisonReport(baseVersion, targetVersion);
        }
        
        return result;
    }
}
```

---

## 🏆 Success Metrics

### Version Quality Indicators
```yaml
Quality_Metrics:
  Performance_Improvement:
    - Win rate increase ≥1%
    - Average profit increase ≥$1
    - Risk-adjusted return improvement ≥5%
    - Max drawdown reduction preferred
    
  Stability_Measures:
    - Parameter sensitivity <10%
    - Out-of-sample performance within 15% of in-sample
    - Stress test survival rate 100%
    - Multi-regime performance consistency
    
  Operational_Excellence:
    - Zero deployment issues
    - Complete documentation
    - Automated testing passed
    - Rollback plan validated
```

### Portfolio-Level Version Management
```yaml
Portfolio_Version_Coordination:
  Deployment_Rules:
    - Maximum 1 strategy version change per week
    - No simultaneous deployments across correlated strategies
    - Minimum 48-hour monitoring period before next change
    
  Risk_Management:
    - Total portfolio risk budget allocation
    - Version correlation analysis
    - Emergency rollback procedures
    - Performance attribution tracking
```

---

## 📚 Documentation Standards

### Required Documentation per Version
1. **Version Summary**: Key changes and improvements
2. **Parameter Documentation**: Complete parameter set with descriptions
3. **Performance Report**: Comprehensive backtest and forward test results
4. **Optimization Log**: Detailed genetic algorithm process and results
5. **Stress Test Results**: Survival analysis in extreme scenarios
6. **Deployment Notes**: Technical implementation details
7. **Comparison Report**: Analysis versus previous version
8. **Risk Assessment**: Updated risk profile and limits

### Documentation Automation
```bash
# Automated documentation generation
./generate_version_docs.sh PM250 v2.1.4_20250817

# Outputs:
# - parameters.json (auto-generated)
# - performance.json (from backtesting)
# - deployment_notes.md (template + results)
# - comparison_report.md (vs previous version)
# - stress_test_results.json (from testing suite)
```

---

**This versioning system ensures complete traceability of strategy evolution, enabling systematic improvement while maintaining the ability to quickly rollback to stable versions when needed.**