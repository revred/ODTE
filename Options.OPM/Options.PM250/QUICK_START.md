# 🚀 Options.PM250 - Quick Start Guide

## 💡 Discoverable Navigation (No Folder Pointing Required)

The Options.PM250 system is designed for **instant discoverability** using clean relative paths. You can access everything without complex folder navigation.

### 📍 One-Command Access

```bash
# From ODTE root directory
cd Options.OPM/Options.PM250

# Now everything is relative and discoverable:
ls                          # See all folders
cat README.md              # Main documentation
```

### 📊 View Reports
```bash
# All reports in one place
ls reports/
cat reports/PM250_COMPREHENSIVE_ANALYSIS_REPORT.md
cat reports/PM250_GENETIC_OPTIMIZATION_REPORT.md
```

### ⚙️ Access Configuration
```bash
# Production-ready configs
ls config/
cat config/PM250_Production_Config_20250816.yaml
cat config/PM250_OptimalWeights_20250816_Production.json
```

### 🔬 Run Tests
```bash
# Execute test suite
ls tests/
dotnet test tests/
```

### 💻 Build Source
```bash
# Build the strategy
ls src/
dotnet build src/
```

## 🎯 Zero-Knowledge Discovery

**Someone new to the system can discover everything in 30 seconds:**

1. `cd Options.OPM/Options.PM250` → Enter the system
2. `ls` → See all available folders
3. `cat README.md` → Get complete documentation
4. Follow relative paths in README → Access any component

## 📁 Clean Structure Overview

```
Options.OPM/Options.PM250/
├── README.md              ← Start here (complete guide)
├── QUICK_START.md         ← This file (instant discovery)
├── config/                ← All configurations
├── reports/               ← All analysis reports  
├── src/                   ← Source code
├── tests/                 ← Test suites
├── docs/                  ← Additional documentation
└── [logs/data/etc]        ← Runtime folders
```

## ✅ Verification: No Hardcoded Paths

Every reference in the system uses relative paths:
- ✅ `reports/PM250_COMPREHENSIVE_ANALYSIS_REPORT.md` (not `/full/path/...`)
- ✅ `config/PM250_Production_Config_20250816.yaml` (not `C:\code\...`)
- ✅ `src/PM250_OptimizedStrategy.cs` (not absolute paths)

## 🏆 Result

**Zero folder pointing required.** Everything is discoverable through:
1. Simple `cd Options.OPM/Options.PM250` 
2. Standard relative path navigation
3. Self-documenting structure
4. README with complete relative path references

---

**Navigation Test**: Try accessing any file mentioned in README.md - they all work with simple relative paths!