using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ODTE.Historical
{
    /// <summary>
    /// Evaluates and ranks data providers for 20-year historical options data
    /// Focus: Dense, traceable, production-quality datasets
    /// </summary>
    public class DataProviderEvaluator
    {
        public class DataProviderProfile
        {
            public string Name { get; set; } = "";
            public string Type { get; set; } = ""; // Commercial, Free, Academic
            public decimal Cost { get; set; }
            public int HistoryYears { get; set; }
            public bool HasOptionsData { get; set; }
            public bool HasIntraday { get; set; }
            public string DataQuality { get; set; } = ""; // Excellent, Good, Fair, Poor
            public string ApiAccess { get; set; } = ""; // REST, WebSocket, FTP, Download
            public List<string> Symbols { get; set; } = new();
            public string Notes { get; set; } = "";
            public int OverallScore { get; set; }
        }

        /// <summary>
        /// Production-grade data providers for 20-year historical options data
        /// </summary>
        public static List<DataProviderProfile> GetEvaluatedProviders()
        {
            return new List<DataProviderProfile>
            {
                // TIER 1: Premium Commercial (Institutional Grade)
                new DataProviderProfile
                {
                    Name = "OPRA (Options Price Reporting Authority)",
                    Type = "Official Exchange Data",
                    Cost = 50000, // High cost but most authoritative
                    HistoryYears = 25,
                    HasOptionsData = true,
                    HasIntraday = true,
                    DataQuality = "Excellent",
                    ApiAccess = "Market Data Vendors",
                    Symbols = new() { "All US Options" },
                    Notes = "Gold standard - official exchange data, requires market data vendor",
                    OverallScore = 95
                },

                new DataProviderProfile
                {
                    Name = "Bloomberg Terminal",
                    Type = "Commercial",
                    Cost = 24000, // $2K/month
                    HistoryYears = 20,
                    HasOptionsData = true,
                    HasIntraday = true,
                    DataQuality = "Excellent",
                    ApiAccess = "Bloomberg API",
                    Symbols = new() { "SPY", "SPX", "XSP", "VIX", "Major indices" },
                    Notes = "Institutional standard, historical depth, API access",
                    OverallScore = 90
                },

                new DataProviderProfile
                {
                    Name = "Refinitiv (Thomson Reuters)",
                    Type = "Commercial",
                    Cost = 30000, // Variable pricing
                    HistoryYears = 20,
                    HasOptionsData = true,
                    HasIntraday = true,
                    DataQuality = "Excellent",
                    ApiAccess = "Eikon API",
                    Symbols = new() { "Global Options", "SPY", "SPX" },
                    Notes = "Global coverage, institutional quality",
                    OverallScore = 88
                },

                // TIER 2: Professional Commercial
                new DataProviderProfile
                {
                    Name = "CBOE DataShop",
                    Type = "Exchange Direct",
                    Cost = 5000, // Per dataset
                    HistoryYears = 20,
                    HasOptionsData = true,
                    HasIntraday = true,
                    DataQuality = "Excellent",
                    ApiAccess = "Direct Download",
                    Symbols = new() { "SPX", "VIX", "CBOE Products" },
                    Notes = "Direct from CBOE, authoritative for SPX/VIX, bulk historical data",
                    OverallScore = 85
                },

                new DataProviderProfile
                {
                    Name = "QuantConnect Data Library",
                    Type = "Commercial/Academic",
                    Cost = 2000, // Research license
                    HistoryYears = 20,
                    HasOptionsData = true,
                    HasIntraday = true,
                    DataQuality = "Good",
                    ApiAccess = "REST API + Python/C#",
                    Symbols = new() { "SPY", "QQQ", "US Equities", "Options" },
                    Notes = "Algo trading focused, clean APIs, research-friendly",
                    OverallScore = 82
                },

                new DataProviderProfile
                {
                    Name = "Polygon.io Historical",
                    Type = "Commercial",
                    Cost = 1200, // Per year professional
                    HistoryYears = 15,
                    HasOptionsData = true,
                    HasIntraday = true,
                    DataQuality = "Good",
                    ApiAccess = "REST API",
                    Symbols = new() { "All US Options", "Stocks" },
                    Notes = "Developer-friendly, reasonable cost, good coverage",
                    OverallScore = 78
                },

                new DataProviderProfile
                {
                    Name = "Alpha Query",
                    Type = "Commercial",
                    Cost = 2500, // Historical data package
                    HistoryYears = 20,
                    HasOptionsData = true,
                    HasIntraday = true,
                    DataQuality = "Good",
                    ApiAccess = "Direct Download + API",
                    Symbols = new() { "SPY", "QQQ", "IWM", "US Options" },
                    Notes = "Historical focus, bulk downloads, research oriented",
                    OverallScore = 75
                },

                // TIER 3: Budget/Academic Options
                new DataProviderProfile
                {
                    Name = "Yahoo Finance (Extended)",
                    Type = "Free/Limited",
                    Cost = 0,
                    HistoryYears = 10,
                    HasOptionsData = false, // Limited options data
                    HasIntraday = false,
                    DataQuality = "Fair",
                    ApiAccess = "REST API",
                    Symbols = new() { "SPY", "Major ETFs", "Stocks" },
                    Notes = "Free but limited historical depth, no comprehensive options",
                    OverallScore = 45
                },

                new DataProviderProfile
                {
                    Name = "FRED Economic Data",
                    Type = "Free Government",
                    Cost = 0,
                    HistoryYears = 30,
                    HasOptionsData = false,
                    HasIntraday = false,
                    DataQuality = "Excellent",
                    ApiAccess = "REST API",
                    Symbols = new() { "VIX", "Economic Indicators" },
                    Notes = "Great for VIX and economic data, no options",
                    OverallScore = 60
                },

                new DataProviderProfile
                {
                    Name = "Quandl (Nasdaq Data Link)",
                    Type = "Commercial/Free Tier",
                    Cost = 1000, // Per dataset
                    HistoryYears = 15,
                    HasOptionsData = true,
                    HasIntraday = false,
                    DataQuality = "Good",
                    ApiAccess = "REST API",
                    Symbols = new() { "Various", "Some Options" },
                    Notes = "Mixed quality, some good datasets, limited options coverage",
                    OverallScore = 65
                }
            };
        }

        /// <summary>
        /// Get recommended data acquisition strategy based on budget and requirements
        /// </summary>
        public static DataAcquisitionStrategy GetRecommendedStrategy(decimal budget, bool needsRealTime = false)
        {
            var providers = GetEvaluatedProviders();
            var strategy = new DataAcquisitionStrategy();

            if (budget >= 20000) // High Budget - Institutional
            {
                strategy.PrimaryProvider = providers.First(p => p.Name == "CBOE DataShop");
                strategy.SecondaryProvider = providers.First(p => p.Name == "Polygon.io Historical");
                strategy.VixProvider = providers.First(p => p.Name == "FRED Economic Data");
                strategy.TotalCost = 6200;
                strategy.DataQuality = "Excellent";
                strategy.Strategy = "Premium: CBOE direct + Polygon backup + Free VIX";
            }
            else if (budget >= 5000) // Medium Budget - Professional
            {
                strategy.PrimaryProvider = providers.First(p => p.Name == "QuantConnect Data Library");
                strategy.SecondaryProvider = providers.First(p => p.Name == "Polygon.io Historical");
                strategy.VixProvider = providers.First(p => p.Name == "FRED Economic Data");
                strategy.TotalCost = 3200;
                strategy.DataQuality = "Good";
                strategy.Strategy = "Professional: QuantConnect + Polygon + Free VIX";
            }
            else if (budget >= 1000) // Budget - Startup
            {
                strategy.PrimaryProvider = providers.First(p => p.Name == "Polygon.io Historical");
                strategy.SecondaryProvider = providers.First(p => p.Name == "Alpha Query");
                strategy.VixProvider = providers.First(p => p.Name == "FRED Economic Data");
                strategy.TotalCost = 3700;
                strategy.DataQuality = "Good";
                strategy.Strategy = "Budget: Polygon primary + Alpha Query backup";
            }
            else // Low Budget - Academic/Research
            {
                strategy.PrimaryProvider = providers.First(p => p.Name == "Quandl (Nasdaq Data Link)");
                strategy.SecondaryProvider = providers.First(p => p.Name == "Yahoo Finance (Extended)");
                strategy.VixProvider = providers.First(p => p.Name == "FRED Economic Data");
                strategy.TotalCost = 1000;
                strategy.DataQuality = "Fair";
                strategy.Strategy = "Academic: Mixed sources, limited coverage";
            }

            return strategy;
        }

        public class DataAcquisitionStrategy
        {
            public DataProviderProfile? PrimaryProvider { get; set; }
            public DataProviderProfile? SecondaryProvider { get; set; }
            public DataProviderProfile? VixProvider { get; set; }
            public decimal TotalCost { get; set; }
            public string DataQuality { get; set; } = "";
            public string Strategy { get; set; } = "";
            
            public List<string> GetImplementationSteps()
            {
                return new List<string>
                {
                    $"1. Subscribe to {PrimaryProvider?.Name} ({PrimaryProvider?.Cost:C})",
                    $"2. Set up {SecondaryProvider?.Name} as backup ({SecondaryProvider?.Cost:C})",
                    $"3. Configure {VixProvider?.Name} for VIX data (Free)",
                    "4. Implement data quality validation pipeline",
                    "5. Set up automated data ingestion",
                    "6. Create data monitoring and alerting",
                    "7. Establish data retention and backup policies"
                };
            }
        }

        /// <summary>
        /// Specific recommendation for ODTE project requirements
        /// </summary>
        public static DataAcquisitionStrategy GetODTERecommendation()
        {
            // For ODTE: Need 20 years of dense options data, focus on SPY/SPX/XSP
            var providers = GetEvaluatedProviders();
            
            return new DataAcquisitionStrategy
            {
                PrimaryProvider = providers.First(p => p.Name == "CBOE DataShop"),
                SecondaryProvider = providers.First(p => p.Name == "Polygon.io Historical"), 
                VixProvider = providers.First(p => p.Name == "FRED Economic Data"),
                TotalCost = 6200,
                DataQuality = "Excellent",
                Strategy = "ODTE-Optimized: CBOE (SPX direct) + Polygon (SPY backup) + FRED (VIX/economic)"
            };
        }
    }
}