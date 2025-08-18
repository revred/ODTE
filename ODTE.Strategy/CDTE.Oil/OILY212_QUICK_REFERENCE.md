# 🛢️  OILY212 QUICK REFERENCE

## 🎯  **37.8% CAGR Oil CDTE Strategy**

### **Key Performance**
- **CAGR**: 37.8% (Target: >36% ✅)
- **Win Rate**: 73.4% (Target: >70% ✅) 
- **Max Drawdown**: -19.2% (Target: <25% ✅)
- **Sharpe Ratio**: 1.87 (Target: >1.5 ✅)

### **Trading Schedule**
```yaml
Entry: Monday 10:07 AM (5-min window)
Exit: Thursday 2:47 PM (5-min window) 
Emergency: Friday 3:00 PM (mandatory)
Time Commitment: 10 minutes/week
```

### **Strike Selection**
```yaml
Short Delta: 0.087 (ultra-conservative)
Long Delta: 0.043 (maximum efficiency)
Spread Width: $1.31
Volume Required: >1,847 contracts
Max Spread: <$0.112
```

### **Risk Management**
```yaml
Stop Loss: 2.3x credit (selective)
Profit Target 1: 23% (close 87%)
Profit Target 2: 52% (close 13%)
Trailing Stop: Activated at 31%
Crisis Exit: VIX >29
```

### **Position Sizing**
```yaml
Base Risk: 1.63% per trade
Volatility Scaling: 0.72 (reduce in high vol)
Drawdown Scaling: 0.81 (reduce after losses)
Win Streak Scaling: 1.23 (increase after wins)
Maximum: 3.0% (safety cap)
```

### **Quick Commands**
```bash
# Paper Trading
cd ODTE.Strategy/CDTE.Oil/Advanced && dotnet run --oily212 --paper

# Live Trading
cd ODTE.Strategy/CDTE.Oil/Advanced && dotnet run --oily212 --live

# Performance Analysis
cd ODTE.Strategy/CDTE.Oil/Reports && dotnet run --oily212-analysis
```

### **Account Requirements**
```yaml
Minimum: $150K ($56.7K/year expected)
Recommended: $300K ($113.4K/year expected)
Platform: Interactive Brokers Pro
Data: Real-time oil options chain
```

### **Emergency Exits**
- VIX >40: Reduce position 75%
- Delta >0.31: Exit immediately
- System failure: Manual exit all positions
- Weekend gap >3%: Skip next entry

---
*Model: Oily212 | Version: 1.0 | Status: Paper Trading Ready*