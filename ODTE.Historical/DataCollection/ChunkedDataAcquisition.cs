using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ODTE.Historical.DataProviders;

namespace ODTE.Historical.DataCollection
{
    /// <summary>
    /// Manages chunked acquisition of authentic market data from multiple sources
    /// Implements the 6-chunk strategy identified in gap analysis
    /// </summary>
    public class ChunkedDataAcquisition : IDisposable
    {
        private readonly MultiSourceDataProvider _dataProvider;
        private readonly TimeSeriesDatabase _database;
        private readonly string _stagingDirectory;
        
        public ChunkedDataAcquisition(string databasePath = @"C:\code\ODTE\Data\ODTE_TimeSeries_5Y.db")
        {
            _dataProvider = new MultiSourceDataProvider(); // Will use demo Alpha Vantage key if Yahoo fails
            _database = new TimeSeriesDatabase(databasePath);
            _stagingDirectory = Path.Combine(Path.GetDirectoryName(databasePath) ?? "", "Staging");
            Directory.CreateDirectory(_stagingDirectory);
        }
        
        /// <summary>
        /// Execute the complete data acquisition plan with all 6 chunks
        /// </summary>
        public async Task<DataAcquisitionResult> ExecuteCompleteAcquisitionAsync(
            IProgress<ChunkProgress>? progress = null)
        {
            Console.WriteLine("🚀 STARTING COMPLETE DATA ACQUISITION - 2005 TO PRESENT");
            Console.WriteLine("=".PadRight(60, '='));
            Console.WriteLine();
            
            await _database.InitializeAsync();
            
            // Define the 6-chunk strategy from gap analysis
            var chunks = new[]
            {
                new AcquisitionChunk
                {
                    Name = "Chunk 1: Recent Data (2022-Present)",
                    StartDate = new DateTime(2022, 1, 1),
                    EndDate = DateTime.Now.Date,
                    Priority = ChunkPriority.High,
                    Symbols = new[] { "XSP", "SPY", "QQQ", "IWM", "^VIX" },
                    ChunkSizeMonths = 3 // Quarterly chunks for recent data
                },
                new AcquisitionChunk
                {
                    Name = "Chunk 2: COVID Era (2020-2021)",
                    StartDate = new DateTime(2020, 1, 1),
                    EndDate = new DateTime(2021, 12, 31),
                    Priority = ChunkPriority.High,
                    Symbols = new[] { "XSP", "SPY", "QQQ", "IWM", "^VIX" },
                    ChunkSizeMonths = 6 // Semi-annual chunks
                },
                new AcquisitionChunk
                {
                    Name = "Chunk 3: Modern Markets (2018-2019)",
                    StartDate = new DateTime(2018, 1, 1),
                    EndDate = new DateTime(2019, 12, 31),
                    Priority = ChunkPriority.Medium,
                    Symbols = new[] { "XSP", "SPY", "QQQ", "IWM", "^VIX" },
                    ChunkSizeMonths = 12 // Annual chunks
                },
                new AcquisitionChunk
                {
                    Name = "Chunk 4: Low Vol Era (2015-2017)",
                    StartDate = new DateTime(2015, 1, 1),
                    EndDate = new DateTime(2017, 12, 31),
                    Priority = ChunkPriority.Medium,
                    Symbols = new[] { "SPY", "QQQ", "IWM", "^VIX" }, // XSP started later
                    ChunkSizeMonths = 12 // Annual chunks
                },
                new AcquisitionChunk
                {
                    Name = "Chunk 5: Post-Crisis (2010-2014)",
                    StartDate = new DateTime(2010, 1, 1),
                    EndDate = new DateTime(2014, 12, 31),
                    Priority = ChunkPriority.Low,
                    Symbols = new[] { "SPY", "QQQ", "IWM", "^VIX" },
                    ChunkSizeMonths = 12 // Annual chunks
                },
                new AcquisitionChunk
                {
                    Name = "Chunk 6: Crisis Era (2005-2009)",
                    StartDate = new DateTime(2005, 1, 1),
                    EndDate = new DateTime(2009, 12, 31),
                    Priority = ChunkPriority.Low,
                    Symbols = new[] { "SPY", "QQQ", "IWM", "^VIX" },
                    ChunkSizeMonths = 12 // Annual chunks
                }
            };
            
            var result = new DataAcquisitionResult
            {
                StartTime = DateTime.UtcNow,
                TotalChunks = chunks.Length
            };
            
            // Process chunks in priority order
            var priorityOrder = chunks
                .OrderByDescending(c => c.Priority)
                .ThenByDescending(c => c.StartDate) // Recent first within same priority
                .ToArray();
            
            for (int i = 0; i < priorityOrder.Length; i++)
            {
                var chunk = priorityOrder[i];
                Console.WriteLine($"📦 Processing {chunk.Name}");
                Console.WriteLine($"   Priority: {chunk.Priority}, Period: {chunk.StartDate:yyyy-MM-dd} to {chunk.EndDate:yyyy-MM-dd}");
                Console.WriteLine($"   Symbols: {string.Join(", ", chunk.Symbols)}");
                Console.WriteLine();
                
                try
                {
                    var chunkResult = await ProcessChunkAsync(chunk, progress);
                    result.ChunkResults.Add(chunkResult);
                    result.TotalRecordsProcessed += chunkResult.RecordsProcessed;
                    result.SuccessfulChunks++;
                    
                    progress?.Report(new ChunkProgress
                    {
                        ChunkName = chunk.Name,
                        ChunkIndex = i + 1,
                        TotalChunks = chunks.Length,
                        OverallProgress = ((double)(i + 1) / chunks.Length) * 100,
                        Status = $"✅ Completed {chunk.Name} - {chunkResult.RecordsProcessed:N0} records"
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Failed to process {chunk.Name}: {ex.Message}");
                    result.FailedChunks++;
                    result.Errors.Add($"{chunk.Name}: {ex.Message}");
                }
                
                Console.WriteLine();
            }
            
            result.EndTime = DateTime.UtcNow;
            result.Success = result.FailedChunks == 0;
            
            // Generate final report
            await GenerateAcquisitionReportAsync(result);
            
            return result;
        }
        
        /// <summary>
        /// Process a single acquisition chunk
        /// </summary>
        private async Task<ChunkResult> ProcessChunkAsync(
            AcquisitionChunk chunk,
            IProgress<ChunkProgress>? progress = null)
        {
            var chunkResult = new ChunkResult
            {
                ChunkName = chunk.Name,
                StartTime = DateTime.UtcNow
            };
            
            var chunkSizeSpan = TimeSpan.FromDays(chunk.ChunkSizeMonths * 30.44); // Average month length
            
            foreach (var symbol in chunk.Symbols)
            {
                Console.WriteLine($"  📈 Processing {symbol}...");
                
                var symbolProgress = new Progress<DataAcquisitionProgress>(p =>
                {
                    progress?.Report(new ChunkProgress
                    {
                        ChunkName = chunk.Name,
                        CurrentSymbol = symbol,
                        Status = p.Status,
                        Progress = p.ProgressPercent
                    });
                });
                
                try
                {
                    var symbolBars = await _dataProvider.GetHistoricalDataChunkedAsync(
                        symbol, 
                        chunk.StartDate, 
                        chunk.EndDate,
                        chunkSizeSpan,
                        symbolProgress);
                    
                    if (symbolBars.Any())
                    {
                        // Save to staging area first (Parquet format)
                        var stagingFile = Path.Combine(_stagingDirectory, $"{symbol}_{chunk.StartDate:yyyyMMdd}_{chunk.EndDate:yyyyMMdd}.csv");
                        await SaveToStagingAsync(symbolBars, stagingFile);
                        
                        // Convert to MarketDataBar format and insert to SQLite
                        var marketDataBars = ConvertToMarketDataBars(symbolBars);
                        await _database.ImportBarsAsync(marketDataBars, symbol);
                        
                        chunkResult.RecordsProcessed += symbolBars.Count;
                        chunkResult.SuccessfulSymbols.Add(symbol);
                        
                        Console.WriteLine($"    ✅ {symbol}: {symbolBars.Count:N0} bars imported");
                    }
                    else
                    {
                        Console.WriteLine($"    ⚠️ {symbol}: No data available for period");
                        chunkResult.FailedSymbols.Add(symbol);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    ❌ {symbol}: {ex.Message}");
                    chunkResult.FailedSymbols.Add(symbol);
                    chunkResult.Errors.Add($"{symbol}: {ex.Message}");
                }
            }
            
            chunkResult.EndTime = DateTime.UtcNow;
            chunkResult.Success = chunkResult.FailedSymbols.Count == 0;
            
            return chunkResult;
        }
        
        /// <summary>
        /// Save raw data to staging area for backup/validation
        /// </summary>
        private async Task SaveToStagingAsync(List<HistoricalDataBar> bars, string filePath)
        {
            var csvLines = new List<string>
            {
                "Date,Open,High,Low,Close,Volume,AdjClose,VWAP"
            };
            
            foreach (var bar in bars)
            {
                csvLines.Add($"{bar.Timestamp:yyyy-MM-dd},{bar.Open},{bar.High},{bar.Low},{bar.Close},{bar.Volume},{bar.AdjustedClose},{bar.VWAP}");
            }
            
            await File.WriteAllLinesAsync(filePath, csvLines);
        }
        
        /// <summary>
        /// Convert HistoricalDataBar to MarketDataBar for database insertion
        /// </summary>
        private List<MarketDataBar> ConvertToMarketDataBars(List<HistoricalDataBar> historicalBars)
        {
            return historicalBars.Select(h => new MarketDataBar
            {
                Timestamp = h.Timestamp,
                Open = h.Open,
                High = h.High,
                Low = h.Low,
                Close = h.Close,
                Volume = h.Volume,
                VWAP = h.VWAP
            }).ToList();
        }
        
        /// <summary>
        /// Generate comprehensive acquisition report
        /// </summary>
        private async Task GenerateAcquisitionReportAsync(DataAcquisitionResult result)
        {
            var reportPath = Path.Combine(_stagingDirectory, $"DataAcquisitionReport_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            var lines = new List<string>
            {
                "🚀 ODTE DATA ACQUISITION COMPLETE REPORT",
                "=".PadRight(50, '='),
                "",
                $"Execution Time: {result.StartTime:yyyy-MM-dd HH:mm:ss} to {result.EndTime:yyyy-MM-dd HH:mm:ss}",
                $"Total Duration: {result.Duration.TotalHours:F1} hours",
                $"Overall Success: {(result.Success ? "✅ SUCCESS" : "❌ PARTIAL")}",
                "",
                "📊 SUMMARY STATISTICS:",
                "-".PadRight(25, '-'),
                $"Total Chunks: {result.TotalChunks}",
                $"Successful Chunks: {result.SuccessfulChunks}",
                $"Failed Chunks: {result.FailedChunks}",
                $"Total Records: {result.TotalRecordsProcessed:N0}",
                "",
                "📦 CHUNK DETAILS:",
                "-".PadRight(20, '-')
            };
            
            foreach (var chunk in result.ChunkResults)
            {
                lines.AddRange(new[]
                {
                    $"• {chunk.ChunkName}:",
                    $"  Status: {(chunk.Success ? "✅ Success" : "❌ Failed")}",
                    $"  Records: {chunk.RecordsProcessed:N0}",
                    $"  Duration: {chunk.Duration.TotalMinutes:F1} minutes",
                    $"  Successful Symbols: {string.Join(", ", chunk.SuccessfulSymbols)}",
                    chunk.FailedSymbols.Any() ? $"  Failed Symbols: {string.Join(", ", chunk.FailedSymbols)}" : "",
                    ""
                });
            }
            
            if (result.Errors.Any())
            {
                lines.AddRange(new[] { "❌ ERRORS:", "-".PadRight(10, '-') });
                lines.AddRange(result.Errors);
            }
            
            await File.WriteAllLinesAsync(reportPath, lines);
            Console.WriteLine($"📋 Report saved to: {reportPath}");
        }
        
        public void Dispose()
        {
            _dataProvider?.Dispose();
            _database?.Dispose();
        }
    }
    
    // Supporting types for chunked acquisition
    public class AcquisitionChunk
    {
        public string Name { get; set; } = "";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public ChunkPriority Priority { get; set; }
        public string[] Symbols { get; set; } = Array.Empty<string>();
        public int ChunkSizeMonths { get; set; } = 12;
    }
    
    public enum ChunkPriority
    {
        Low = 1,
        Medium = 2,
        High = 3
    }
    
    public class ChunkProgress
    {
        public string ChunkName { get; set; } = "";
        public int ChunkIndex { get; set; }
        public int TotalChunks { get; set; }
        public string CurrentSymbol { get; set; } = "";
        public double Progress { get; set; }
        public double OverallProgress { get; set; }
        public string Status { get; set; } = "";
    }
    
    public class DataAcquisitionResult
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        public bool Success { get; set; }
        public int TotalChunks { get; set; }
        public int SuccessfulChunks { get; set; }
        public int FailedChunks { get; set; }
        public int TotalRecordsProcessed { get; set; }
        public List<ChunkResult> ChunkResults { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }
    
    public class ChunkResult
    {
        public string ChunkName { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        public bool Success { get; set; }
        public int RecordsProcessed { get; set; }
        public List<string> SuccessfulSymbols { get; set; } = new();
        public List<string> FailedSymbols { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }
}