using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ODTE.Historical.DataCollection;

/// <summary>
/// Advanced data validation engine for detecting gaps, anomalies, and quality issues
/// </summary>
public class DataValidationEngine
{
    private readonly TimeSeriesDatabase _database;
    private readonly ILogger<DataValidationEngine>? _logger;
    
    public DataValidationEngine(TimeSeriesDatabase database, ILogger<DataValidationEngine>? logger = null)
    {
        _database = database;
        _logger = logger;
    }
    
    /// <summary>
    /// Perform comprehensive validation of collected data
    /// </summary>
    public async Task<ValidationReport> ValidateCollectedDataAsync(
        DateTime startDate, 
        DateTime endDate, 
        List<string> symbols)
    {
        var report = new ValidationReport
        {
            ValidationTime = DateTime.UtcNow,
            StartDate = startDate,
            EndDate = endDate,
            SymbolsValidated = symbols
        };
        
        _logger?.LogInformation("🔍 Starting comprehensive data validation...");
        _logger?.LogInformation($"📅 Date range: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
        _logger?.LogInformation($"🎯 Symbols: {symbols.Count}");
        
        foreach (var symbol in symbols)
        {
            _logger?.LogDebug($"   Validating {symbol}...");
            
            var symbolValidation = await ValidateSymbolDataAsync(symbol, startDate, endDate);
            report.SymbolValidations[symbol] = symbolValidation;
            
            // Aggregate statistics
            report.TotalRecords += symbolValidation.TotalRecords;
            report.TotalGaps += symbolValidation.Gaps.Count;
            report.TotalAnomalies += symbolValidation.Anomalies.Count;
        }
        
        // Overall validation
        report.IsValid = report.TotalGaps == 0 && report.TotalAnomalies == 0;
        report.OverallQualityScore = CalculateOverallQualityScore(report);
        
        _logger?.LogInformation($"✅ Validation completed:");
        _logger?.LogInformation($"   Total records: {report.TotalRecords:N0}");
        _logger?.LogInformation($"   Data gaps: {report.TotalGaps}");
        _logger?.LogInformation($"   Anomalies: {report.TotalAnomalies}");
        _logger?.LogInformation($"   Quality score: {report.OverallQualityScore:P1}");
        
        return report;
    }
    
    /// <summary>
    /// Validate data for a specific symbol
    /// </summary>
    public async Task<SymbolValidation> ValidateSymbolDataAsync(
        string symbol, 
        DateTime startDate, 
        DateTime endDate)
    {
        var validation = new SymbolValidation
        {
            Symbol = symbol,
            StartDate = startDate,
            EndDate = endDate
        };
        
        try
        {
            // Get all data for the symbol
            var data = await _database.GetRangeAsync(startDate, endDate, symbol);
            validation.TotalRecords = data.Count;
            
            if (!data.Any())
            {
                validation.Gaps.Add(new DataGap
                {
                    StartDate = startDate,
                    EndDate = endDate,
                    Type = GapType.MissingData,
                    Description = "No data found for entire date range"
                });
                return validation;
            }
            
            // Check for date gaps
            var dateGaps = FindDateGaps(data, startDate, endDate);
            validation.Gaps.AddRange(dateGaps);
            
            // Check for price anomalies
            var priceAnomalies = FindPriceAnomalies(data);
            validation.Anomalies.AddRange(priceAnomalies);
            
            // Check for volume anomalies
            var volumeAnomalies = FindVolumeAnomalies(data);
            validation.Anomalies.AddRange(volumeAnomalies);
            
            // Calculate quality metrics
            validation.QualityScore = CalculateSymbolQualityScore(validation, data);
            validation.DataCompleteness = CalculateDataCompleteness(data, startDate, endDate);
            
            // Statistical summary
            if (data.Any())
            {
                validation.PriceRange = new PriceRange
                {
                    Min = (decimal)data.Min(d => d.Low),
                    Max = (decimal)data.Max(d => d.High),
                    Average = (decimal)data.Average(d => d.Close)
                };
                
                validation.VolumeStats = new VolumeStats
                {
                    Min = data.Min(d => d.Volume),
                    Max = data.Max(d => d.Volume),
                    Average = (long)data.Average(d => d.Volume),
                    MedianDaily = CalculateMedianVolume(data)
                };
            }
            
        }
        catch (Exception ex)
        {
            validation.ValidationError = ex.Message;
            _logger?.LogError(ex, $"Error validating {symbol}");
        }
        
        return validation;
    }
    
    /// <summary>
    /// Find missing trading days in the data
    /// </summary>
    private List<DataGap> FindDateGaps(List<MarketDataBar> data, DateTime startDate, DateTime endDate)
    {
        var gaps = new List<DataGap>();
        
        // Group by date to find missing days
        var dailyData = data.GroupBy(d => d.Timestamp.Date).OrderBy(g => g.Key).ToList();
        
        if (!dailyData.Any())
            return gaps;
        
        var expectedTradingDays = GenerateExpectedTradingDays(startDate, endDate);
        var actualDays = dailyData.Select(g => g.Key).ToHashSet();
        
        var currentGapStart = (DateTime?)null;
        
        foreach (var expectedDay in expectedTradingDays)
        {
            if (!actualDays.Contains(expectedDay))
            {
                // Start of gap
                if (currentGapStart == null)
                {
                    currentGapStart = expectedDay;
                }
            }
            else
            {
                // End of gap
                if (currentGapStart.HasValue)
                {
                    gaps.Add(new DataGap
                    {
                        StartDate = currentGapStart.Value,
                        EndDate = expectedDay.AddDays(-1),
                        Type = GapType.MissingTradingDays,
                        Description = $"Missing trading days from {currentGapStart.Value:yyyy-MM-dd} to {expectedDay.AddDays(-1):yyyy-MM-dd}"
                    });
                    currentGapStart = null;
                }
            }
        }
        
        // Handle gap extending to end
        if (currentGapStart.HasValue)
        {
            gaps.Add(new DataGap
            {
                StartDate = currentGapStart.Value,
                EndDate = endDate,
                Type = GapType.MissingTradingDays,
                Description = $"Missing trading days from {currentGapStart.Value:yyyy-MM-dd} to end"
            });
        }
        
        return gaps;
    }
    
    /// <summary>
    /// Find price anomalies (extreme movements, invalid OHLC, etc.)
    /// </summary>
    private List<DataAnomaly> FindPriceAnomalies(List<MarketDataBar> data)
    {
        var anomalies = new List<DataAnomaly>();
        
        if (data.Count < 2)
            return anomalies;
        
        var sortedData = data.OrderBy(d => d.Timestamp).ToList();
        
        for (int i = 0; i < sortedData.Count; i++)
        {
            var bar = sortedData[i];
            
            // Check OHLC consistency
            if (bar.High < bar.Low)
            {
                anomalies.Add(new DataAnomaly
                {
                    Date = bar.Timestamp.Date,
                    Type = AnomalyType.InvalidOHLC,
                    Description = $"High ({bar.High:F2}) is less than Low ({bar.Low:F2})",
                    Severity = AnomalySeverity.High
                });
            }
            
            if (bar.Open < bar.Low || bar.Open > bar.High)
            {
                anomalies.Add(new DataAnomaly
                {
                    Date = bar.Timestamp.Date,
                    Type = AnomalyType.InvalidOHLC,
                    Description = $"Open ({bar.Open:F2}) is outside High-Low range",
                    Severity = AnomalySeverity.High
                });
            }
            
            if (bar.Close < bar.Low || bar.Close > bar.High)
            {
                anomalies.Add(new DataAnomaly
                {
                    Date = bar.Timestamp.Date,
                    Type = AnomalyType.InvalidOHLC,
                    Description = $"Close ({bar.Close:F2}) is outside High-Low range",
                    Severity = AnomalySeverity.High
                });
            }
            
            // Check for extreme price movements (>20% in one day)
            if (i > 0)
            {
                var prevBar = sortedData[i - 1];
                var priceChange = Math.Abs((bar.Close - prevBar.Close) / prevBar.Close);
                
                if (priceChange > 0.20) // 20% threshold
                {
                    anomalies.Add(new DataAnomaly
                    {
                        Date = bar.Timestamp.Date,
                        Type = AnomalyType.ExtremePriceMovement,
                        Description = $"Large price movement: {priceChange:P1} from previous day",
                        Severity = AnomalySeverity.Medium
                    });
                }
            }
            
            // Check for zero or negative prices
            if (bar.Open <= 0 || bar.High <= 0 || bar.Low <= 0 || bar.Close <= 0)
            {
                anomalies.Add(new DataAnomaly
                {
                    Date = bar.Timestamp.Date,
                    Type = AnomalyType.InvalidPrice,
                    Description = "Zero or negative price detected",
                    Severity = AnomalySeverity.High
                });
            }
            
            // Check for suspiciously round numbers (could indicate synthetic data)
            if (IsRoundNumber(bar.Close) && IsRoundNumber(bar.Open) && IsRoundNumber(bar.High) && IsRoundNumber(bar.Low))
            {
                anomalies.Add(new DataAnomaly
                {
                    Date = bar.Timestamp.Date,
                    Type = AnomalyType.SuspiciousRounding,
                    Description = "All OHLC prices are round numbers",
                    Severity = AnomalySeverity.Low
                });
            }
        }
        
        return anomalies;
    }
    
    /// <summary>
    /// Find volume anomalies
    /// </summary>
    private List<DataAnomaly> FindVolumeAnomalies(List<MarketDataBar> data)
    {
        var anomalies = new List<DataAnomaly>();
        
        if (!data.Any())
            return anomalies;
        
        var volumes = data.Select(d => d.Volume).Where(v => v > 0).ToList();
        
        if (!volumes.Any())
        {
            anomalies.Add(new DataAnomaly
            {
                Date = data.First().Timestamp.Date,
                Type = AnomalyType.ZeroVolume,
                Description = "No volume data available",
                Severity = AnomalySeverity.Medium
            });
            return anomalies;
        }
        
        var medianVolume = CalculateMedian(volumes.Select(v => (double)v));
        var avgVolume = volumes.Average();
        
        foreach (var bar in data)
        {
            // Check for zero volume
            if (bar.Volume == 0)
            {
                anomalies.Add(new DataAnomaly
                {
                    Date = bar.Timestamp.Date,
                    Type = AnomalyType.ZeroVolume,
                    Description = "Zero volume on trading day",
                    Severity = AnomalySeverity.Medium
                });
            }
            
            // Check for extreme volume (>10x median)
            if (bar.Volume > medianVolume * 10)
            {
                anomalies.Add(new DataAnomaly
                {
                    Date = bar.Timestamp.Date,
                    Type = AnomalyType.ExtremeVolume,
                    Description = $"Volume {bar.Volume:N0} is {(bar.Volume / medianVolume):F1}x median",
                    Severity = AnomalySeverity.Low
                });
            }
        }
        
        return anomalies;
    }
    
    /// <summary>
    /// Generate expected trading days (excluding weekends and major holidays)
    /// </summary>
    private List<DateTime> GenerateExpectedTradingDays(DateTime startDate, DateTime endDate)
    {
        var tradingDays = new List<DateTime>();
        var current = startDate.Date;
        
        while (current <= endDate.Date)
        {
            // Skip weekends
            if (current.DayOfWeek != DayOfWeek.Saturday && current.DayOfWeek != DayOfWeek.Sunday)
            {
                // Skip major holidays (basic implementation)
                if (!IsMajorHoliday(current))
                {
                    tradingDays.Add(current);
                }
            }
            
            current = current.AddDays(1);
        }
        
        return tradingDays;
    }
    
    private bool IsMajorHoliday(DateTime date)
    {
        // Basic US market holidays (could be expanded)
        var holidays = new[]
        {
            // New Year's Day
            new DateTime(date.Year, 1, 1),
            // Independence Day
            new DateTime(date.Year, 7, 4),
            // Christmas
            new DateTime(date.Year, 12, 25)
        };
        
        return holidays.Contains(date);
    }
    
    private double CalculateDataCompleteness(List<MarketDataBar> data, DateTime startDate, DateTime endDate)
    {
        var expectedDays = GenerateExpectedTradingDays(startDate, endDate).Count;
        var actualDays = data.GroupBy(d => d.Timestamp.Date).Count();
        
        return expectedDays > 0 ? actualDays / (double)expectedDays : 0;
    }
    
    private double CalculateSymbolQualityScore(SymbolValidation validation, List<MarketDataBar> data)
    {
        var score = 1.0;
        
        // Penalize gaps
        score -= validation.Gaps.Count * 0.1;
        
        // Penalize anomalies based on severity
        foreach (var anomaly in validation.Anomalies)
        {
            score -= anomaly.Severity switch
            {
                AnomalySeverity.High => 0.2,
                AnomalySeverity.Medium => 0.1,
                AnomalySeverity.Low => 0.05,
                _ => 0
            };
        }
        
        // Reward data completeness
        score *= validation.DataCompleteness;
        
        return Math.Max(0, Math.Min(1, score));
    }
    
    private double CalculateOverallQualityScore(ValidationReport report)
    {
        if (!report.SymbolValidations.Any())
            return 0;
        
        return report.SymbolValidations.Values.Average(s => s.QualityScore);
    }
    
    private long CalculateMedianVolume(List<MarketDataBar> data)
    {
        var volumes = data.Select(d => d.Volume).OrderBy(v => v).ToList();
        
        if (!volumes.Any())
            return 0;
        
        int middle = volumes.Count / 2;
        
        if (volumes.Count % 2 == 0)
            return (volumes[middle - 1] + volumes[middle]) / 2;
        
        return volumes[middle];
    }
    
    private double CalculateMedian(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        
        if (!sorted.Any())
            return 0;
        
        int middle = sorted.Count / 2;
        
        if (sorted.Count % 2 == 0)
            return (sorted[middle - 1] + sorted[middle]) / 2;
        
        return sorted[middle];
    }
    
    private bool IsRoundNumber(double value)
    {
        return Math.Abs(value - Math.Round(value)) < 0.01;
    }
}

/// <summary>
/// Comprehensive validation report
/// </summary>
public class ValidationReport
{
    public DateTime ValidationTime { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<string> SymbolsValidated { get; set; } = new();
    
    public bool IsValid { get; set; }
    public double OverallQualityScore { get; set; }
    
    public int TotalRecords { get; set; }
    public int TotalGaps { get; set; }
    public int TotalAnomalies { get; set; }
    
    public Dictionary<string, SymbolValidation> SymbolValidations { get; set; } = new();
}

/// <summary>
/// Validation results for individual symbol
/// </summary>
public class SymbolValidation
{
    public string Symbol { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    
    public int TotalRecords { get; set; }
    public double QualityScore { get; set; }
    public double DataCompleteness { get; set; }
    
    public List<DataGap> Gaps { get; set; } = new();
    public List<DataAnomaly> Anomalies { get; set; } = new();
    
    public PriceRange? PriceRange { get; set; }
    public VolumeStats? VolumeStats { get; set; }
    
    public string? ValidationError { get; set; }
}

/// <summary>
/// Data gap information
/// </summary>
public class DataGap
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public GapType Type { get; set; }
    public string Description { get; set; } = "";
}

/// <summary>
/// Data anomaly information
/// </summary>
public class DataAnomaly
{
    public DateTime Date { get; set; }
    public AnomalyType Type { get; set; }
    public AnomalySeverity Severity { get; set; }
    public string Description { get; set; } = "";
}

/// <summary>
/// Price range statistics
/// </summary>
public class PriceRange
{
    public decimal Min { get; set; }
    public decimal Max { get; set; }
    public decimal Average { get; set; }
}

/// <summary>
/// Volume statistics
/// </summary>
public class VolumeStats
{
    public long Min { get; set; }
    public long Max { get; set; }
    public long Average { get; set; }
    public long MedianDaily { get; set; }
}

/// <summary>
/// Types of data gaps
/// </summary>
public enum GapType
{
    MissingData,
    MissingTradingDays,
    IncompleteDay
}

/// <summary>
/// Types of data anomalies
/// </summary>
public enum AnomalyType
{
    InvalidOHLC,
    ExtremePriceMovement,
    InvalidPrice,
    ZeroVolume,
    ExtremeVolume,
    SuspiciousRounding
}

/// <summary>
/// Severity levels for anomalies
/// </summary>
public enum AnomalySeverity
{
    Low,
    Medium,
    High
}