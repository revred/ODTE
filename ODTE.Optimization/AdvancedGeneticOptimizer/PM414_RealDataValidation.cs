using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using System.Linq;

namespace ODTE.Optimization.AdvancedGeneticOptimizer
{
    /// <summary>
    /// PM414 Real Data Validation Checklist - ZERO TOLERANCE FOR FAKE DATA
    /// This class validates that ALL data sources are real and eliminates any possibility of synthetic/fake data
    /// </summary>
    public class PM414_RealDataValidation
    {
        private readonly string _databasePath;
        private readonly List<ValidationResult> _validationResults = new();
        
        public PM414_RealDataValidation(string databasePath)
        {
            _databasePath = databasePath;
        }
        
        public async Task<bool> ValidateAllRealDataSources()
        {
            Console.WriteLine("🔍 PM414 REAL DATA VALIDATION CHECKLIST");
            Console.WriteLine("=======================================");
            Console.WriteLine("❌ ZERO TOLERANCE FOR FAKE/SYNTHETIC DATA");
            Console.WriteLine();
            
            _validationResults.Clear();
            
            // 1. Validate Database Existence and Structure
            await ValidateDatabaseStructure();
            
            // 2. Validate Real SPY Historical Data
            await ValidateRealSPYData();
            
            // 3. Validate Real VIX Historical Data  
            await ValidateRealVIXData();
            
            // 4. Validate Real Options Chain Data
            await ValidateRealOptionsChainData();
            
            // 5. Validate Multi-Asset Data Sources
            await ValidateMultiAssetData();
            
            // 6. Validate Data Completeness and Quality
            await ValidateDataCompleteness();
            
            // 7. Validate Greeks Calculation Source
            await ValidateGreeksCalculationSource();
            
            // 8. Final Report
            GenerateValidationReport();
            
            return _validationResults.All(r => r.Passed);
        }
        
        private async Task ValidateDatabaseStructure()
        {
            var result = new ValidationResult 
            { 
                CheckName = "1. Database Structure Validation",
                Description = "Verify real database exists with required tables"
            };
            
            try
            {
                if (!File.Exists(_databasePath))
                {
                    result.FailureReason = $"Database file not found: {_databasePath}";
                    result.Passed = false;
                    _validationResults.Add(result);
                    return;
                }
                
                using var connection = new SqliteConnection($"Data Source={_databasePath}");
                await connection.OpenAsync();
                
                // Check required tables exist
                var requiredTables = new[] { "trades", "market_conditions", "options_chains" };
                var existingTables = new List<string>();
                
                var query = "SELECT name FROM sqlite_master WHERE type='table'";
                using var command = new SqliteCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    existingTables.Add(reader.GetString(0));
                }
                
                var missingTables = requiredTables.Where(t => !existingTables.Contains(t)).ToList();
                
                if (missingTables.Any())
                {
                    result.FailureReason = $"Missing required tables: {string.Join(", ", missingTables)}";
                    result.Passed = false;
                }
                else
                {
                    result.Passed = true;
                    result.Details = $"✅ All required tables found: {string.Join(", ", requiredTables)}";
                }
            }
            catch (Exception ex)
            {
                result.FailureReason = $"Database access error: {ex.Message}";
                result.Passed = false;
            }
            
            _validationResults.Add(result);
        }
        
        private async Task ValidateRealSPYData()
        {
            var result = new ValidationResult 
            { 
                CheckName = "2. Real SPY Data Validation",
                Description = "Ensure SPY data is from real market sources, not synthetic"
            };
            
            try
            {
                using var connection = new SqliteConnection($"Data Source={_databasePath}");
                await connection.OpenAsync();
                
                // Check SPY data exists and has realistic values
                var query = @"SELECT COUNT(*), MIN(spx_close), MAX(spx_close), AVG(spx_close) 
                             FROM market_conditions 
                             WHERE spx_close IS NOT NULL AND spx_close > 0";
                
                using var command = new SqliteCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();
                
                if (await reader.ReadAsync())
                {
                    var count = reader.GetInt32(0);
                    var minPrice = reader.GetDouble(1);
                    var maxPrice = reader.GetDouble(2);
                    var avgPrice = reader.GetDouble(3);
                    
                    // Validate realistic SPY price ranges (1990-2025 historical range)
                    if (count == 0)
                    {
                        result.FailureReason = "No SPY data found in database";
                        result.Passed = false;
                    }
                    else if (minPrice < 50 || maxPrice > 600 || avgPrice < 100 || avgPrice > 500)
                    {
                        result.FailureReason = $"SPY prices appear synthetic - Min: {minPrice:F2}, Max: {maxPrice:F2}, Avg: {avgPrice:F2}";
                        result.Passed = false;
                    }
                    else if (count < 5000) // Should have 20+ years of data
                    {
                        result.FailureReason = $"Insufficient SPY data points: {count} (need 5000+ for 20 years)";
                        result.Passed = false;
                    }
                    else
                    {
                        result.Passed = true;
                        result.Details = $"✅ Real SPY data validated - {count} data points, price range {minPrice:F2} to {maxPrice:F2}";
                    }
                }
            }
            catch (Exception ex)
            {
                result.FailureReason = $"SPY data validation error: {ex.Message}";
                result.Passed = false;
            }
            
            _validationResults.Add(result);
        }
        
        private async Task ValidateRealVIXData()
        {
            var result = new ValidationResult 
            { 
                CheckName = "3. Real VIX Data Validation",
                Description = "Ensure VIX data is from real CBOE sources, not calculated"
            };
            
            try
            {
                using var connection = new SqliteConnection($"Data Source={_databasePath}");
                await connection.OpenAsync();
                
                var query = @"SELECT COUNT(*), MIN(vix_close), MAX(vix_close), AVG(vix_close) 
                             FROM market_conditions 
                             WHERE vix_close IS NOT NULL AND vix_close > 0";
                
                using var command = new SqliteCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();
                
                if (await reader.ReadAsync())
                {
                    var count = reader.GetInt32(0);
                    var minVIX = reader.GetDouble(1);
                    var maxVIX = reader.GetDouble(2);
                    var avgVIX = reader.GetDouble(3);
                    
                    // Validate realistic VIX ranges (historical: 9-89)
                    if (count == 0)
                    {
                        result.FailureReason = "No VIX data found in database";
                        result.Passed = false;
                    }
                    else if (minVIX < 8 || maxVIX > 100 || avgVIX < 15 || avgVIX > 35)
                    {
                        result.FailureReason = $"VIX values appear synthetic - Min: {minVIX:F2}, Max: {maxVIX:F2}, Avg: {avgVIX:F2}";
                        result.Passed = false;
                    }
                    else
                    {
                        result.Passed = true;
                        result.Details = $"✅ Real VIX data validated - {count} data points, range {minVIX:F2} to {maxVIX:F2}";
                    }
                }
            }
            catch (Exception ex)
            {
                result.FailureReason = $"VIX data validation error: {ex.Message}";
                result.Passed = false;
            }
            
            _validationResults.Add(result);
        }
        
        private async Task ValidateRealOptionsChainData()
        {
            var result = new ValidationResult 
            { 
                CheckName = "4. Real Options Chain Data Validation",
                Description = "Verify options data comes from real market sources with actual bid/ask/volume"
            };
            
            try
            {
                using var connection = new SqliteConnection($"Data Source={_databasePath}");
                await connection.OpenAsync();
                
                // Check if options_chains table exists and has real data
                var query = @"SELECT COUNT(*), 
                             COUNT(DISTINCT trade_date) as unique_dates,
                             MIN(volume) as min_vol, 
                             MAX(volume) as max_vol,
                             AVG(CASE WHEN volume > 0 THEN volume END) as avg_vol,
                             COUNT(CASE WHEN bid > 0 AND ask > 0 THEN 1 END) as valid_quotes
                             FROM options_chains 
                             WHERE symbol = 'SPY'";
                
                using var command = new SqliteCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();
                
                if (await reader.ReadAsync())
                {
                    var totalRecords = reader.GetInt32(0);
                    var uniqueDates = reader.GetInt32(1);
                    var validQuotes = reader.GetInt32(5);
                    
                    if (totalRecords == 0)
                    {
                        result.FailureReason = "No options chain data found - system using synthetic options pricing";
                        result.Passed = false;
                    }
                    else if (uniqueDates < 1000) // Need many trading days
                    {
                        result.FailureReason = $"Insufficient options data coverage: {uniqueDates} days (need 1000+)";
                        result.Passed = false;
                    }
                    else if (validQuotes < totalRecords * 0.8) // At least 80% should have valid bid/ask
                    {
                        result.FailureReason = $"Too many invalid option quotes: {validQuotes}/{totalRecords} valid";
                        result.Passed = false;
                    }
                    else
                    {
                        result.Passed = true;
                        result.Details = $"✅ Real options data validated - {totalRecords} contracts across {uniqueDates} days";
                    }
                }
            }
            catch (Exception ex)
            {
                // If table doesn't exist, we're definitely using synthetic data
                result.FailureReason = $"Options chain validation failed - likely using synthetic data: {ex.Message}";
                result.Passed = false;
            }
            
            _validationResults.Add(result);
        }
        
        private async Task ValidateMultiAssetData()
        {
            var result = new ValidationResult 
            { 
                CheckName = "5. Multi-Asset Data Validation",
                Description = "Validate futures, gold, bonds, oil data from real sources"
            };
            
            // For now, mark as requires implementation
            result.FailureReason = "Multi-asset data validation not yet implemented - need to add ES, GLD, TLT, USO data sources";
            result.Passed = false;
            
            _validationResults.Add(result);
        }
        
        private async Task ValidateDataCompleteness()
        {
            var result = new ValidationResult 
            { 
                CheckName = "6. Data Completeness Validation",
                Description = "Ensure data coverage is complete with no major gaps"
            };
            
            try
            {
                using var connection = new SqliteConnection($"Data Source={_databasePath}");
                await connection.OpenAsync();
                
                // Check for data gaps in market_conditions
                var query = @"SELECT 
                             MIN(month) as start_date,
                             MAX(month) as end_date,
                             COUNT(*) as total_records,
                             COUNT(CASE WHEN spx_close IS NULL THEN 1 END) as missing_spy,
                             COUNT(CASE WHEN vix_close IS NULL THEN 1 END) as missing_vix
                             FROM market_conditions";
                
                using var command = new SqliteCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();
                
                if (await reader.ReadAsync())
                {
                    var startDate = reader.GetString(0);
                    var endDate = reader.GetString(1);
                    var totalRecords = reader.GetInt32(2);
                    var missingSPY = reader.GetInt32(3);
                    var missingVIX = reader.GetInt32(4);
                    
                    var missingDataPercent = (missingSPY + missingVIX) / (double)(totalRecords * 2) * 100;
                    
                    if (missingDataPercent > 5) // More than 5% missing data
                    {
                        result.FailureReason = $"Too much missing data: {missingDataPercent:F1}% (SPY: {missingSPY}, VIX: {missingVIX})";
                        result.Passed = false;
                    }
                    else
                    {
                        result.Passed = true;
                        result.Details = $"✅ Data completeness validated - {startDate} to {endDate}, {missingDataPercent:F1}% missing";
                    }
                }
            }
            catch (Exception ex)
            {
                result.FailureReason = $"Data completeness check failed: {ex.Message}";
                result.Passed = false;
            }
            
            _validationResults.Add(result);
        }
        
        private async Task ValidateGreeksCalculationSource()
        {
            var result = new ValidationResult 
            { 
                CheckName = "7. Greeks Calculation Source Validation",
                Description = "Ensure Greeks come from real market data, not Black-Scholes approximations"
            };
            
            // Check if we have real Greeks data or if we're calculating them
            result.FailureReason = "Greeks validation requires checking if delta/gamma/theta come from real market data vs synthetic calculation";
            result.Passed = false;
            
            _validationResults.Add(result);
        }
        
        private void GenerateValidationReport()
        {
            Console.WriteLine();
            Console.WriteLine("📋 PM414 REAL DATA VALIDATION REPORT");
            Console.WriteLine("===================================");
            
            var passedChecks = _validationResults.Count(r => r.Passed);
            var totalChecks = _validationResults.Count;
            
            Console.WriteLine($"Overall Status: {passedChecks}/{totalChecks} checks passed");
            Console.WriteLine();
            
            foreach (var result in _validationResults)
            {
                var status = result.Passed ? "✅ PASS" : "❌ FAIL";
                Console.WriteLine($"{status} {result.CheckName}");
                Console.WriteLine($"     {result.Description}");
                
                if (result.Passed && !string.IsNullOrEmpty(result.Details))
                {
                    Console.WriteLine($"     {result.Details}");
                }
                
                if (!result.Passed)
                {
                    Console.WriteLine($"     ⚠️ FAILURE: {result.FailureReason}");
                }
                
                Console.WriteLine();
            }
            
            if (passedChecks == totalChecks)
            {
                Console.WriteLine("🎉 ALL VALIDATIONS PASSED - PM414 USING 100% REAL DATA");
            }
            else
            {
                Console.WriteLine("🚨 VALIDATION FAILURES DETECTED - SYNTHETIC DATA STILL PRESENT");
                Console.WriteLine("❌ PM414 CANNOT RUN UNTIL ALL REAL DATA SOURCES ARE VALIDATED");
            }
        }
    }
    
    public class ValidationResult
    {
        public string CheckName { get; set; } = "";
        public string Description { get; set; } = "";
        public bool Passed { get; set; }
        public string FailureReason { get; set; } = "";
        public string Details { get; set; } = "";
    }
}