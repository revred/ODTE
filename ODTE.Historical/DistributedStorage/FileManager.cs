namespace ODTE.Historical.DistributedStorage;

/// <summary>
/// Fast file path resolution for distributed SQLite storage
/// Handles commodities and options data with optimized naming conventions
/// </summary>
public class FileManager
{
    private readonly string _baseDataPath;
    
    public FileManager(string baseDataPath = @"C:\code\ODTE\data")
    {
        _baseDataPath = baseDataPath;
    }

    /// <summary>
    /// Get path for commodity underlying data (monthly files)
    /// Format: data/commodities/{category}/{yyyy}/{MM}/{SYMBOL}_{yyyyMM}.db
    /// </summary>
    public string GetCommodityPath(string symbol, DateTime date, CommodityCategory category = CommodityCategory.Oil)
    {
        var categoryPath = GetCategoryPath(category);
        return Path.Combine(_baseDataPath, "commodities", categoryPath, 
            $"{date:yyyy}", $"{date:MM}", $"{symbol}_{date:yyyyMM}.db");
    }

    /// <summary>
    /// Get path for options chain data (per expiration)
    /// Format: data/options/{category}/{SYMBOL}/{yyyy}/{MM}/{SYMBOL}_OPT_{yyyyMMdd}.db
    /// </summary>
    public string GetOptionsPath(string symbol, DateTime expirationDate, CommodityCategory category = CommodityCategory.Oil)
    {
        var categoryPath = GetCategoryPath(category);
        return Path.Combine(_baseDataPath, "options", categoryPath, symbol.ToUpper(),
            $"{expirationDate:yyyy}", $"{expirationDate:MM}", 
            $"{symbol.ToUpper()}_OPT_{expirationDate:yyyyMMdd}.db");
    }

    /// <summary>
    /// Get all commodity files for a symbol over a date range
    /// </summary>
    public List<string> GetCommodityFilesInRange(string symbol, DateTime startDate, DateTime endDate, CommodityCategory category = CommodityCategory.Oil)
    {
        var files = new List<string>();
        var current = new DateTime(startDate.Year, startDate.Month, 1);
        var end = new DateTime(endDate.Year, endDate.Month, 1);

        while (current <= end)
        {
            var filePath = GetCommodityPath(symbol, current, category);
            if (File.Exists(filePath))
            {
                files.Add(filePath);
            }
            current = current.AddMonths(1);
        }

        return files;
    }

    /// <summary>
    /// Get all options files for a symbol within date range
    /// </summary>
    public List<string> GetOptionsFilesInRange(string symbol, DateTime startDate, DateTime endDate, CommodityCategory category = CommodityCategory.Oil)
    {
        var files = new List<string>();
        var categoryPath = GetCategoryPath(category);
        var symbolDir = Path.Combine(_baseDataPath, "options", categoryPath, symbol.ToUpper());

        if (!Directory.Exists(symbolDir))
            return files;

        // Search through year/month directories
        var current = new DateTime(startDate.Year, startDate.Month, 1);
        var end = new DateTime(endDate.Year, endDate.Month, 1);

        while (current <= end)
        {
            var monthDir = Path.Combine(symbolDir, $"{current:yyyy}", $"{current:MM}");
            if (Directory.Exists(monthDir))
            {
                var pattern = $"{symbol.ToUpper()}_OPT_*.db";
                var monthFiles = Directory.GetFiles(monthDir, pattern)
                    .Where(f => IsFileInDateRange(f, startDate, endDate))
                    .ToList();
                files.AddRange(monthFiles);
            }
            current = current.AddMonths(1);
        }

        return files.OrderBy(f => f).ToList();
    }

    /// <summary>
    /// Ensure directory structure exists for a file path
    /// </summary>
    public void EnsureDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    /// <summary>
    /// Get available symbols for a category and date range
    /// </summary>
    public List<string> GetAvailableSymbols(CommodityCategory category, DateTime startDate, DateTime endDate)
    {
        var symbols = new HashSet<string>();
        var categoryPath = GetCategoryPath(category);
        var commoditiesDir = Path.Combine(_baseDataPath, "commodities", categoryPath);

        if (!Directory.Exists(commoditiesDir))
            return new List<string>();

        // Search through year/month directories for commodity files
        var current = new DateTime(startDate.Year, startDate.Month, 1);
        var end = new DateTime(endDate.Year, endDate.Month, 1);

        while (current <= end)
        {
            var monthDir = Path.Combine(commoditiesDir, $"{current:yyyy}", $"{current:MM}");
            if (Directory.Exists(monthDir))
            {
                var files = Directory.GetFiles(monthDir, "*.db");
                foreach (var file in files)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    if (fileName.Contains('_'))
                    {
                        var symbol = fileName.Split('_')[0];
                        symbols.Add(symbol);
                    }
                }
            }
            current = current.AddMonths(1);
        }

        return symbols.OrderBy(s => s).ToList();
    }

    /// <summary>
    /// Get storage statistics for a symbol
    /// </summary>
    public FileStorageStats GetStorageStats(string symbol, CommodityCategory category = CommodityCategory.Oil)
    {
        var stats = new FileStorageStats { Symbol = symbol, Category = category };
        
        // Get commodity files
        var commoditiesDir = Path.Combine(_baseDataPath, "commodities", GetCategoryPath(category));
        if (Directory.Exists(commoditiesDir))
        {
            var commodityFiles = Directory.GetFiles(commoditiesDir, $"{symbol}_*.db", SearchOption.AllDirectories);
            stats.CommodityFiles = commodityFiles.Length;
            stats.CommodityStorageBytes = commodityFiles.Sum(f => new FileInfo(f).Length);
        }

        // Get options files
        var optionsDir = Path.Combine(_baseDataPath, "options", GetCategoryPath(category), symbol.ToUpper());
        if (Directory.Exists(optionsDir))
        {
            var optionsFiles = Directory.GetFiles(optionsDir, $"{symbol.ToUpper()}_OPT_*.db", SearchOption.AllDirectories);
            stats.OptionsFiles = optionsFiles.Length;
            stats.OptionsStorageBytes = optionsFiles.Sum(f => new FileInfo(f).Length);
        }

        return stats;
    }

    private string GetCategoryPath(CommodityCategory category)
    {
        return category switch
        {
            CommodityCategory.Oil => "oil",
            CommodityCategory.Metals => "metals", 
            CommodityCategory.Agriculture => "agriculture",
            CommodityCategory.Energy => "energy",
            _ => "other"
        };
    }

    private bool IsFileInDateRange(string filePath, DateTime startDate, DateTime endDate)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        
        // Extract date from filename like USO_OPT_20240119
        if (fileName.Contains("_OPT_") && fileName.Length >= 16)
        {
            var dateStr = fileName.Substring(fileName.Length - 8); // Last 8 characters
            if (DateTime.TryParseExact(dateStr, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var fileDate))
            {
                return fileDate >= startDate && fileDate <= endDate;
            }
        }

        return false;
    }
}

/// <summary>
/// Commodity categories for organized storage
/// </summary>
public enum CommodityCategory
{
    Oil,
    Metals,
    Agriculture,
    Energy
}

/// <summary>
/// Storage statistics for a symbol
/// </summary>
public class FileStorageStats
{
    public string Symbol { get; set; } = "";
    public CommodityCategory Category { get; set; }
    public int CommodityFiles { get; set; }
    public int OptionsFiles { get; set; }
    public long CommodityStorageBytes { get; set; }
    public long OptionsStorageBytes { get; set; }
    
    public long TotalStorageBytes => CommodityStorageBytes + OptionsStorageBytes;
    public double TotalStorageMB => TotalStorageBytes / 1024.0 / 1024.0;
    public int TotalFiles => CommodityFiles + OptionsFiles;
}