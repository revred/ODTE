using System;
using System.IO;
using System.Text.Json;

namespace ODTE.Strategy.Configuration
{
    /// <summary>
    /// Configuration loader for Reverse Fibonacci Risk Management System
    /// Supports configurable reset thresholds and daily limits
    /// </summary>
    public class RFibConfiguration
    {
        private static RFibConfiguration? _instance;
        private static readonly object _lock = new object();
        
        public decimal ResetProfitThreshold { get; set; } = 16.0m;
        public decimal[] DailyLimits { get; set; } = new decimal[] { 500m, 300m, 200m, 100m };
        public int MaxConsecutiveLossDays { get; set; } = 10;
        public decimal WarningThreshold { get; set; } = 0.90m;
        public bool EnableDynamicScaling { get; set; } = true;
        public string LoggingLevel { get; set; } = "Info";
        
        /// <summary>
        /// Singleton instance with lazy loading
        /// </summary>
        public static RFibConfiguration Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = LoadConfiguration();
                        }
                    }
                }
                return _instance;
            }
        }
        
        /// <summary>
        /// Load configuration from JSON file or use defaults
        /// </summary>
        private static RFibConfiguration LoadConfiguration()
        {
            var config = new RFibConfiguration();
            
            try
            {
                // Try to find config file in multiple locations
                var configPaths = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configuration", "RFibConfig.json"),
                    Path.Combine(Directory.GetCurrentDirectory(), "Configuration", "RFibConfig.json"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ODTE", "RFibConfig.json"),
                    "RFibConfig.json" // Current directory fallback
                };
                
                string? configPath = null;
                foreach (var path in configPaths)
                {
                    if (File.Exists(path))
                    {
                        configPath = path;
                        break;
                    }
                }
                
                if (configPath != null)
                {
                    var jsonContent = File.ReadAllText(configPath);
                    var jsonDoc = JsonDocument.Parse(jsonContent);
                    
                    // Parse ReverseFibonacciRiskManagement section
                    if (jsonDoc.RootElement.TryGetProperty("ReverseFibonacciRiskManagement", out var rFibSection))
                    {
                        if (rFibSection.TryGetProperty("ResetProfitThreshold", out var threshold))
                        {
                            config.ResetProfitThreshold = threshold.GetDecimal();
                        }
                        
                        if (rFibSection.TryGetProperty("DailyLimits", out var limits))
                        {
                            var limitsList = new decimal[4];
                            var i = 0;
                            foreach (var limit in limits.EnumerateArray())
                            {
                                if (i < 4)
                                {
                                    limitsList[i] = limit.GetDecimal();
                                    i++;
                                }
                            }
                            config.DailyLimits = limitsList;
                        }
                    }
                    
                    // Parse AdvancedSettings section
                    if (jsonDoc.RootElement.TryGetProperty("AdvancedSettings", out var advancedSection))
                    {
                        if (advancedSection.TryGetProperty("MaxConsecutiveLossDays", out var maxDays))
                        {
                            config.MaxConsecutiveLossDays = maxDays.GetInt32();
                        }
                        
                        if (advancedSection.TryGetProperty("WarningThreshold", out var warning))
                        {
                            config.WarningThreshold = warning.GetDecimal();
                        }
                        
                        if (advancedSection.TryGetProperty("EnableDynamicScaling", out var scaling))
                        {
                            config.EnableDynamicScaling = scaling.GetBoolean();
                        }
                        
                        if (advancedSection.TryGetProperty("LoggingLevel", out var logging))
                        {
                            config.LoggingLevel = logging.GetString() ?? "Info";
                        }
                    }
                    
                    Console.WriteLine($"✅ RFib Configuration loaded from: {configPath}");
                    Console.WriteLine($"   Reset Threshold: ${config.ResetProfitThreshold}");
                    Console.WriteLine($"   Daily Limits: [{string.Join(", ", config.DailyLimits.Select(l => $"${l}"))}]");
                }
                else
                {
                    Console.WriteLine("⚠️ RFib config file not found, using defaults:");
                    Console.WriteLine($"   Reset Threshold: ${config.ResetProfitThreshold}");
                    Console.WriteLine($"   Daily Limits: [{string.Join(", ", config.DailyLimits.Select(l => $"${l}"))}]");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error loading RFib config: {ex.Message}");
                Console.WriteLine("   Using default values");
            }
            
            return config;
        }
        
        /// <summary>
        /// Reload configuration from file
        /// </summary>
        public static void ReloadConfiguration()
        {
            lock (_lock)
            {
                _instance = LoadConfiguration();
            }
        }
        
        /// <summary>
        /// Get daily limit for given consecutive loss day count
        /// </summary>
        public decimal GetDailyLimit(int consecutiveLossDays)
        {
            var index = Math.Min(consecutiveLossDays, DailyLimits.Length - 1);
            return DailyLimits[index];
        }
        
        /// <summary>
        /// Update reset profit threshold programmatically
        /// </summary>
        public void UpdateResetThreshold(decimal newThreshold)
        {
            if (newThreshold > 0)
            {
                ResetProfitThreshold = newThreshold;
                Console.WriteLine($"🔄 RFib reset threshold updated to: ${newThreshold}");
            }
        }
        
        /// <summary>
        /// Validate configuration values
        /// </summary>
        public bool ValidateConfiguration()
        {
            if (ResetProfitThreshold <= 0)
            {
                Console.WriteLine("❌ Invalid ResetProfitThreshold: must be > 0");
                return false;
            }
            
            if (DailyLimits.Length < 4)
            {
                Console.WriteLine("❌ Invalid DailyLimits: must have at least 4 values");
                return false;
            }
            
            if (DailyLimits.Any(l => l <= 0))
            {
                Console.WriteLine("❌ Invalid DailyLimits: all values must be > 0");
                return false;
            }
            
            return true;
        }
    }
}