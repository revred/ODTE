using System;
using System.Collections.Generic;
using System.Linq;

namespace ODTE.Optimization.RiskManagement
{
    public class ReverseFibonacciRiskManager
    {
        private const double BASE_MAX_LOSS = 500.0; // $5 x 100
        private readonly List<double> _fibonacciSeries = new List<double> { 500, 300, 200, 100, 100 }; // $5, $3, $2, $1, $1 x 100
        private int _currentLevel = 0;
        private double _currentDayPnL = 0;
        private double _currentMaxLoss = BASE_MAX_LOSS;
        private readonly List<DailyRiskRecord> _riskHistory = new List<DailyRiskRecord>();
        
        public double CurrentMaxLoss => _currentMaxLoss;
        public int CurrentRiskLevel => _currentLevel;
        public bool IsTradingAllowed => _currentMaxLoss > 0 && Math.Abs(_currentDayPnL) < _currentMaxLoss;
        
        public class DailyRiskRecord
        {
            public DateTime Date { get; set; }
            public double MaxLossAllowed { get; set; }
            public double ActualPnL { get; set; }
            public int RiskLevel { get; set; }
            public bool MaxLossBreached { get; set; }
            public string Action { get; set; }
        }
        
        public void StartNewDay(DateTime date)
        {
            // Save yesterday's record if exists
            if (_riskHistory.Any())
            {
                var lastRecord = _riskHistory.Last();
                lastRecord.ActualPnL = _currentDayPnL;
            }
            
            // Determine today's max loss based on yesterday's performance
            UpdateRiskLevel();
            
            // Reset daily P&L counter
            _currentDayPnL = 0;
            
            // Record today's risk parameters
            _riskHistory.Add(new DailyRiskRecord
            {
                Date = date,
                MaxLossAllowed = _currentMaxLoss,
                RiskLevel = _currentLevel,
                MaxLossBreached = false,
                Action = GetRiskLevelDescription()
            });
        }
        
        private void UpdateRiskLevel()
        {
            if (!_riskHistory.Any())
            {
                // First day - start with base level
                _currentLevel = 0;
                _currentMaxLoss = _fibonacciSeries[0];
                return;
            }
            
            var yesterday = _riskHistory.Last();
            
            if (yesterday.ActualPnL > 0)
            {
                // Made profit - reset to base level
                _currentLevel = 0;
                _currentMaxLoss = _fibonacciSeries[0];
            }
            else if (yesterday.MaxLossBreached)
            {
                // Max loss was breached - move down Fibonacci series
                _currentLevel = Math.Min(_currentLevel + 1, _fibonacciSeries.Count - 1);
                _currentMaxLoss = _fibonacciSeries[_currentLevel];
            }
            else if (yesterday.ActualPnL < 0)
            {
                // Lost money but didn't breach max - stay at same level
                _currentMaxLoss = _fibonacciSeries[_currentLevel];
            }
        }
        
        public bool ValidatePosition(double potentialLoss)
        {
            // Check if taking this position would exceed daily max loss
            double projectedDayLoss = _currentDayPnL - Math.Abs(potentialLoss);
            
            if (Math.Abs(projectedDayLoss) > _currentMaxLoss)
            {
                // Would exceed max loss - reject position
                return false;
            }
            
            return true;
        }
        
        public void UpdateDailyPnL(double pnl)
        {
            _currentDayPnL += pnl;
            
            // Check if we've breached max loss
            if (Math.Abs(_currentDayPnL) >= _currentMaxLoss && _riskHistory.Any())
            {
                _riskHistory.Last().MaxLossBreached = true;
                _riskHistory.Last().ActualPnL = _currentDayPnL;
            }
        }
        
        public bool ShouldStopTrading()
        {
            // Stop trading if:
            // 1. Daily max loss has been reached
            // 2. We're at the lowest Fibonacci level and still losing
            return Math.Abs(_currentDayPnL) >= _currentMaxLoss ||
                   (_currentLevel >= _fibonacciSeries.Count - 1 && _currentDayPnL < 0);
        }
        
        private string GetRiskLevelDescription()
        {
            return _currentLevel switch
            {
                0 => "Normal Trading - Max Loss: $500",
                1 => "Caution Level 1 - Max Loss: $300",
                2 => "Caution Level 2 - Max Loss: $200",
                3 => "Critical Level - Max Loss: $100",
                4 => "Minimum Trading - Max Loss: $100",
                _ => "Trading Suspended"
            };
        }
        
        public RiskAnalytics GetAnalytics()
        {
            return new RiskAnalytics
            {
                TotalDays = _riskHistory.Count,
                DaysAtNormalRisk = _riskHistory.Count(r => r.RiskLevel == 0),
                DaysAtReducedRisk = _riskHistory.Count(r => r.RiskLevel > 0),
                MaxLossBreaches = _riskHistory.Count(r => r.MaxLossBreached),
                TotalPnL = _riskHistory.Sum(r => r.ActualPnL),
                AverageDaily = _riskHistory.Any() ? _riskHistory.Average(r => r.ActualPnL) : 0,
                CurrentStreak = CalculateCurrentStreak(),
                RiskHistory = _riskHistory.ToList()
            };
        }
        
        private int CalculateCurrentStreak()
        {
            if (!_riskHistory.Any()) return 0;
            
            int streak = 0;
            bool isWinning = _riskHistory.Last().ActualPnL > 0;
            
            for (int i = _riskHistory.Count - 1; i >= 0; i--)
            {
                bool dayWasWinning = _riskHistory[i].ActualPnL > 0;
                if (dayWasWinning == isWinning)
                    streak++;
                else
                    break;
            }
            
            return isWinning ? streak : -streak;
        }
        
        public class RiskAnalytics
        {
            public int TotalDays { get; set; }
            public int DaysAtNormalRisk { get; set; }
            public int DaysAtReducedRisk { get; set; }
            public int MaxLossBreaches { get; set; }
            public double TotalPnL { get; set; }
            public double AverageDaily { get; set; }
            public int CurrentStreak { get; set; }
            public List<DailyRiskRecord> RiskHistory { get; set; }
        }
        
        public void SaveToFile(string path)
        {
            var lines = new List<string>
            {
                "Date,MaxLossAllowed,ActualPnL,RiskLevel,MaxLossBreached,Action"
            };
            
            foreach (var record in _riskHistory)
            {
                lines.Add($"{record.Date:yyyy-MM-dd},{record.MaxLossAllowed:F2}," +
                         $"{record.ActualPnL:F2},{record.RiskLevel}," +
                         $"{record.MaxLossBreached},{record.Action}");
            }
            
            System.IO.File.WriteAllLines(path, lines);
        }
    }
}