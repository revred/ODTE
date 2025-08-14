using System.Data.SQLite;
using System.Globalization;
using Dapper;

namespace ODTE.Historical;

/// <summary>
/// Legacy Stooq importer - enhanced to work with new SQLite schema
/// For new implementations, use EnhancedStooqIntegration instead
/// </summary>
public static class StooqImporter
{
    // Stooq TXT/CSV format: date,open,high,low,close,volume (header may vary)
    public static void ImportDirectory(string rootDir, string sqlitePath)
    {
        using var conn = new SQLiteConnection($"Data Source={sqlitePath}");
        conn.Open();
        using var tx = conn.BeginTransaction();

        foreach (var file in Directory.EnumerateFiles(rootDir, "*.txt", SearchOption.AllDirectories))
        {
            var symbol = Path.GetFileNameWithoutExtension(file).ToUpperInvariant(); // e.g., AAPL.US
            var cleanSymbol = CleanSymbol(symbol);
            
            // Ensure underlying exists in new schema
            EnsureUnderlyingExists(conn, cleanSymbol);
            var underlyingId = GetUnderlyingId(conn, cleanSymbol);

            foreach (var line in File.ReadLines(file).Skip(1)) // skip header
            {
                var parts = line.Split(',');
                if (parts.Length < 6) continue;

                try
                {
                    // Example Stooq date "2025-08-08" or "20250808" depending on file;
                    // handle both:
                    var ds = parts[0].Trim();
                    DateTime dt = ds.Contains('-')
                        ? DateTime.Parse(ds, CultureInfo.InvariantCulture)
                        : DateTime.ParseExact(ds, "yyyyMMdd", CultureInfo.InvariantCulture);

                    var open = double.Parse(parts[1], CultureInfo.InvariantCulture);
                    var high = double.Parse(parts[2], CultureInfo.InvariantCulture);
                    var low = double.Parse(parts[3], CultureInfo.InvariantCulture);
                    var close = double.Parse(parts[4], CultureInfo.InvariantCulture);
                    var volume = long.Parse(parts[5], CultureInfo.InvariantCulture);

                    // Insert into new underlying_quotes table
                    var timestamp = ((DateTimeOffset)dt).ToUnixTimeMilliseconds() * 1000; // Microseconds
                    
                    conn.Execute(@"
                        INSERT OR REPLACE INTO underlying_quotes 
                        (underlying_id, timestamp, open, high, low, close, volume, last, bid, ask)
                        VALUES (@UnderlyingId, @Timestamp, @Open, @High, @Low, @Close, @Volume, @Close, @Bid, @Ask)",
                        new
                        {
                            UnderlyingId = underlyingId,
                            Timestamp = timestamp,
                            Open = open,
                            High = high,
                            Low = low,
                            Close = close,
                            Volume = (int)Math.Min(volume, int.MaxValue),
                            Bid = close - 0.01, // Approximate bid
                            Ask = close + 0.01  // Approximate ask
                        }, tx);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing line in {file}: {line} - {ex.Message}");
                }
            }
        }

        tx.Commit();
        Console.WriteLine($"Stooq import completed for directory: {rootDir}");
    }
    
    /// <summary>
    /// Import single Stooq file with enhanced error handling
    /// </summary>
    public static void ImportFile(string filePath, string sqlitePath)
    {
        using var conn = new SQLiteConnection($"Data Source={sqlitePath}");
        conn.Open();
        
        var symbol = Path.GetFileNameWithoutExtension(filePath).ToUpperInvariant();
        var cleanSymbol = CleanSymbol(symbol);
        
        EnsureUnderlyingExists(conn, cleanSymbol);
        var underlyingId = GetUnderlyingId(conn, cleanSymbol);
        
        var recordCount = 0;
        using var tx = conn.BeginTransaction();
        
        foreach (var line in File.ReadLines(filePath).Skip(1)) // skip header
        {
            var parts = line.Split(',');
            if (parts.Length < 6) continue;

            try
            {
                var ds = parts[0].Trim();
                DateTime dt = ds.Contains('-')
                    ? DateTime.Parse(ds, CultureInfo.InvariantCulture)
                    : DateTime.ParseExact(ds, "yyyyMMdd", CultureInfo.InvariantCulture);

                var open = double.Parse(parts[1], CultureInfo.InvariantCulture);
                var high = double.Parse(parts[2], CultureInfo.InvariantCulture);
                var low = double.Parse(parts[3], CultureInfo.InvariantCulture);
                var close = double.Parse(parts[4], CultureInfo.InvariantCulture);
                var volume = long.Parse(parts[5], CultureInfo.InvariantCulture);

                // Basic validation
                if (open <= 0 || high <= 0 || low <= 0 || close <= 0) continue;
                if (high < Math.Max(open, close) || low > Math.Min(open, close)) continue;

                var timestamp = ((DateTimeOffset)dt).ToUnixTimeMilliseconds() * 1000;
                
                conn.Execute(@"
                    INSERT OR REPLACE INTO underlying_quotes 
                    (underlying_id, timestamp, open, high, low, close, volume, last, bid, ask)
                    VALUES (@UnderlyingId, @Timestamp, @Open, @High, @Low, @Close, @Volume, @Close, @Bid, @Ask)",
                    new
                    {
                        UnderlyingId = underlyingId,
                        Timestamp = timestamp,
                        Open = open,
                        High = high,
                        Low = low,
                        Close = close,
                        Volume = (int)Math.Min(volume, int.MaxValue),
                        Bid = close - 0.01,
                        Ask = close + 0.01
                    }, tx);
                
                recordCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing line: {line} - {ex.Message}");
            }
        }
        
        tx.Commit();
        // Run random validation checks on imported data
        RunRandomValidationChecks(conn, underlyingId, cleanSymbol, recordCount);
        
        Console.WriteLine($"Imported {recordCount} records for {cleanSymbol} from {filePath}");
    }
    
    private static void EnsureUnderlyingExists(SQLiteConnection conn, string symbol)
    {
        conn.Execute(@"
            INSERT OR IGNORE INTO underlyings (symbol, name, multiplier, tick_size)
            VALUES (@Symbol, @Name, @Multiplier, @TickSize)", new
        {
            Symbol = symbol,
            Name = GetSymbolName(symbol),
            Multiplier = GetSymbolMultiplier(symbol),
            TickSize = GetSymbolTickSize(symbol)
        });
    }
    
    private static int GetUnderlyingId(SQLiteConnection conn, string symbol)
    {
        return conn.QuerySingle<int>(
            "SELECT id FROM underlyings WHERE symbol = @Symbol", 
            new { Symbol = symbol });
    }
    
    private static string CleanSymbol(string symbol)
    {
        // Remove Stooq suffixes (.US, .CC, etc.) and clean up
        return symbol.Split('.')[0].Replace("^", "").ToUpperInvariant();
    }
    
    private static string GetSymbolName(string symbol) => symbol switch
    {
        "SPY" => "SPDR S&P 500 ETF",
        "QQQ" => "Invesco QQQ Trust",
        "IWM" => "iShares Russell 2000 ETF",
        "VIX" => "CBOE Volatility Index",
        "SPX" => "S&P 500 Index",
        "NDX" => "Nasdaq 100 Index",
        "RUT" => "Russell 2000 Index",
        _ => $"{symbol} (Stooq Import)"
    };
    
    private static decimal GetSymbolMultiplier(string symbol) => symbol switch
    {
        "SPY" or "QQQ" or "IWM" => 100m, // ETFs
        "VIX" or "SPX" or "NDX" or "RUT" => 1m, // Indices
        _ when symbol.Contains("USD") => 1m, // FX
        _ => 100m // Default to 100
    };
    
    private static decimal GetSymbolTickSize(string symbol) => symbol switch
    {
        _ when symbol.Contains("USD") => 0.0001m, // FX pairs
        "VIX" => 0.01m, // VIX
        _ => 0.01m // Default
    };
    
    /// <summary>
    /// Run random validation checks on imported data to ensure quality
    /// </summary>
    private static void RunRandomValidationChecks(SQLiteConnection conn, int underlyingId, string symbol, int recordCount)
    {
        try
        {
            if (recordCount < 10) return; // Need minimum data for validation
            
            // Random sampling validation (check 5% of records or max 100)
            var sampleSize = Math.Min(100, Math.Max(5, recordCount / 20));
            
            var validationResults = conn.Query<ValidationSample>(@"
                SELECT open, high, low, close, volume, timestamp
                FROM underlying_quotes 
                WHERE underlying_id = @UnderlyingId
                ORDER BY RANDOM()
                LIMIT @SampleSize", new { UnderlyingId = underlyingId, SampleSize = sampleSize });
            
            var issues = new List<string>();
            var validRecords = 0;
            
            foreach (var sample in validationResults)
            {
                var isValid = true;
                
                // OHLC relationship validation
                if (sample.High < Math.Max(sample.Open, sample.Close) ||
                    sample.Low > Math.Min(sample.Open, sample.Close))
                {
                    issues.Add($"Invalid OHLC relationship at {sample.Timestamp}");
                    isValid = false;
                }
                
                // Positive price validation
                if (sample.Open <= 0 || sample.High <= 0 || sample.Low <= 0 || sample.Close <= 0)
                {
                    issues.Add($"Non-positive price at {sample.Timestamp}");
                    isValid = false;
                }
                
                // Reasonable price validation for known symbols
                if (symbol == "SPY" && (sample.Close < 10 || sample.Close > 1000))
                {
                    issues.Add($"Unreasonable SPY price ${sample.Close:F2} at {sample.Timestamp}");
                    isValid = false;
                }
                else if (symbol == "VIX" && (sample.Close < 5 || sample.Close > 200))
                {
                    issues.Add($"Unreasonable VIX level {sample.Close:F2} at {sample.Timestamp}");
                    isValid = false;
                }
                
                // Volume validation (allow zero but not negative)
                if (sample.Volume < 0)
                {
                    issues.Add($"Negative volume {sample.Volume} at {sample.Timestamp}");
                    isValid = false;
                }
                
                if (isValid) validRecords++;
            }
            
            var validityRate = validRecords / (double)validationResults.Count();
            
            if (validityRate < 0.95) // 95% validity threshold
            {
                Console.WriteLine($"⚠️  Data quality warning for {symbol}: {validityRate:P1} validity rate");
                foreach (var issue in issues.Take(3)) // Show first 3 issues
                {
                    Console.WriteLine($"   • {issue}");
                }
            }
            else
            {
                Console.WriteLine($"✅ Data quality check passed for {symbol}: {validityRate:P1} validity ({sampleSize} samples)");
            }
            
            // Performance check: measure query time for recent data
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var recentData = conn.Query(@"
                SELECT COUNT(*), AVG(close) 
                FROM underlying_quotes 
                WHERE underlying_id = @UnderlyingId 
                ORDER BY timestamp DESC 
                LIMIT 100", new { UnderlyingId = underlyingId });
            sw.Stop();
            
            if (sw.ElapsedMilliseconds > 500) // Warn if query takes >500ms
            {
                Console.WriteLine($"⚠️  Performance warning: Query took {sw.ElapsedMilliseconds}ms for {symbol}");
            }
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Validation check failed for {symbol}: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Data structure for validation sampling
    /// </summary>
    private class ValidationSample
    {
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public long Volume { get; set; }
        public long Timestamp { get; set; }
    }
}
