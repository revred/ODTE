using Parquet;
using Parquet.Schema;

namespace ODTE.Optimization.Data
{
    public class HistoricalDataFetcher
    {
        private readonly string _baseDataPath;
        private readonly DataGenerator _generator;

        public HistoricalDataFetcher(string baseDataPath = @"C:\code\ODTE\Data\Historical")
        {
            _baseDataPath = baseDataPath;
            _generator = new DataGenerator();
            Directory.CreateDirectory(_baseDataPath);
        }

        public async Task<DataFetchResult> FetchFiveYearDataAsync(string symbol = "XSP")
        {
            var result = new DataFetchResult
            {
                Symbol = symbol,
                StartDate = DateTime.Now.AddYears(-5),
                EndDate = DateTime.Now,
                DataPath = Path.Combine(_baseDataPath, symbol)
            };

            Directory.CreateDirectory(result.DataPath);

            // Generate data for each year
            var tasks = new List<Task<YearlyDataSet>>();
            for (int year = 0; year < 5; year++)
            {
                var targetYear = DateTime.Now.AddYears(-year).Year;
                tasks.Add(GenerateYearDataAsync(symbol, targetYear));
            }

            var yearlyData = await Task.WhenAll(tasks);

            // Organize data by year/month structure
            foreach (var yearData in yearlyData)
            {
                await OrganizeDataAsync(yearData, result.DataPath);
            }

            // Create master index
            await CreateMasterIndexAsync(result);

            result.TotalDays = yearlyData.Sum(y => y.TradingDays);
            result.TotalBars = yearlyData.Sum(y => y.TotalBars);
            result.FormatType = DataFormat.Parquet;

            return result;
        }

        private async Task<YearlyDataSet> GenerateYearDataAsync(string symbol, int year)
        {
            var yearData = new YearlyDataSet
            {
                Year = year,
                Symbol = symbol,
                MonthlyData = new Dictionary<int, MonthlyData>()
            };

            // Generate data for each trading month
            for (int month = 1; month <= 12; month++)
            {
                var monthData = await GenerateMonthDataAsync(symbol, year, month);
                yearData.MonthlyData[month] = monthData;
                yearData.TradingDays += monthData.TradingDays;
                yearData.TotalBars += monthData.Bars.Count;
            }

            return yearData;
        }

        private async Task<MonthlyData> GenerateMonthDataAsync(string symbol, int year, int month)
        {
            var monthData = new MonthlyData
            {
                Year = year,
                Month = month,
                Bars = new List<MarketBar>()
            };

            // Get trading days for the month
            var tradingDays = GetTradingDays(year, month);
            monthData.TradingDays = tradingDays.Count;

            // Generate synthetic market data for each trading day
            foreach (var day in tradingDays)
            {
                var dayBars = GenerateDayBars(symbol, day);
                monthData.Bars.AddRange(dayBars);
            }

            return monthData;
        }

        private List<MarketBar> GenerateDayBars(string symbol, DateTime date)
        {
            var bars = new List<MarketBar>();
            var random = new Random(date.GetHashCode());

            // Generate 390 minute bars (6.5 hours * 60 minutes)
            var basePrice = 490.0 + random.NextDouble() * 20; // XSP around 490-510
            var volatility = 0.001 + random.NextDouble() * 0.002; // 0.1% to 0.3% per minute

            for (int minute = 0; minute < 390; minute++)
            {
                var timestamp = date.Date.AddHours(14).AddMinutes(30).AddMinutes(minute);
                var change = (random.NextDouble() - 0.5) * 2 * volatility * basePrice;
                basePrice += change;

                var bar = new MarketBar
                {
                    Symbol = symbol,
                    Timestamp = timestamp,
                    Open = Math.Round(basePrice - random.NextDouble() * 0.1, 2),
                    High = Math.Round(basePrice + random.NextDouble() * 0.2, 2),
                    Low = Math.Round(basePrice - random.NextDouble() * 0.2, 2),
                    Close = Math.Round(basePrice, 2),
                    Volume = (long)(1000000 + random.NextDouble() * 5000000),
                    VWAP = Math.Round(basePrice + (random.NextDouble() - 0.5) * 0.1, 2)
                };

                bars.Add(bar);
            }

            return bars;
        }

        private List<DateTime> GetTradingDays(int year, int month)
        {
            var tradingDays = new List<DateTime>();
            var date = new DateTime(year, month, 1);
            var lastDay = date.AddMonths(1).AddDays(-1);

            while (date <= lastDay)
            {
                // Skip weekends and major holidays
                if (date.DayOfWeek != DayOfWeek.Saturday &&
                    date.DayOfWeek != DayOfWeek.Sunday &&
                    !IsMarketHoliday(date))
                {
                    tradingDays.Add(date);
                }
                date = date.AddDays(1);
            }

            return tradingDays;
        }

        private bool IsMarketHoliday(DateTime date)
        {
            // Simplified holiday check - add more as needed
            var holidays = new[]
            {
                new DateTime(date.Year, 1, 1),   // New Year's
                new DateTime(date.Year, 7, 4),   // July 4th
                new DateTime(date.Year, 12, 25), // Christmas
            };

            return holidays.Contains(date.Date);
        }

        private async Task OrganizeDataAsync(YearlyDataSet yearData, string basePath)
        {
            var yearPath = Path.Combine(basePath, yearData.Year.ToString());
            Directory.CreateDirectory(yearPath);

            foreach (var (month, monthData) in yearData.MonthlyData)
            {
                var monthPath = Path.Combine(yearPath, month.ToString("D2"));
                Directory.CreateDirectory(monthPath);

                // Group bars by day and save as Parquet
                var dayGroups = monthData.Bars.GroupBy(b => b.Timestamp.Date);

                foreach (var dayGroup in dayGroups)
                {
                    var fileName = $"{dayGroup.Key:yyyyMMdd}.parquet";
                    var filePath = Path.Combine(monthPath, fileName);

                    await SaveBarsToParquetAsync(dayGroup.ToList(), filePath);
                }
            }
        }

        private async Task SaveBarsToParquetAsync(List<MarketBar> bars, string filePath)
        {
            // Create Parquet schema using DataField
            var dataFields = new List<DataField>
            {
                new DataField("timestamp", typeof(DateTime)),
                new DataField("symbol", typeof(string)),
                new DataField("open", typeof(double)),
                new DataField("high", typeof(double)),
                new DataField("low", typeof(double)),
                new DataField("close", typeof(double)),
                new DataField("volume", typeof(long)),
                new DataField("vwap", typeof(double))
            };

            var schema = new ParquetSchema(dataFields.Cast<Field>().ToList());

            // Prepare data arrays
            var timestamps = bars.Select(b => b.Timestamp).ToArray();
            var symbols = bars.Select(b => b.Symbol).ToArray();
            var opens = bars.Select(b => b.Open).ToArray();
            var highs = bars.Select(b => b.High).ToArray();
            var lows = bars.Select(b => b.Low).ToArray();
            var closes = bars.Select(b => b.Close).ToArray();
            var volumes = bars.Select(b => b.Volume).ToArray();
            var vwaps = bars.Select(b => b.VWAP).ToArray();

            // Write to Parquet file
            using var file = File.Create(filePath);
            using var writer = await ParquetWriter.CreateAsync(schema, file);

            using var groupWriter = writer.CreateRowGroup();
            await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(dataFields[0], timestamps));
            await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(dataFields[1], symbols));
            await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(dataFields[2], opens));
            await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(dataFields[3], highs));
            await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(dataFields[4], lows));
            await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(dataFields[5], closes));
            await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(dataFields[6], volumes));
            await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(dataFields[7], vwaps));
        }

        private async Task CreateMasterIndexAsync(DataFetchResult result)
        {
            var indexPath = Path.Combine(result.DataPath, "master_index.json");

            var index = new
            {
                Symbol = result.Symbol,
                StartDate = result.StartDate,
                EndDate = result.EndDate,
                TotalDays = result.TotalDays,
                TotalBars = result.TotalBars,
                Format = result.FormatType.ToString(),
                Created = DateTime.Now,
                Structure = "Year/Month/Day.parquet"
            };

            var json = System.Text.Json.JsonSerializer.Serialize(index, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(indexPath, json);
        }
    }

    public class DataFetchResult
    {
        public string Symbol { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string DataPath { get; set; }
        public int TotalDays { get; set; }
        public long TotalBars { get; set; }
        public DataFormat FormatType { get; set; }
    }

    public class YearlyDataSet
    {
        public int Year { get; set; }
        public string Symbol { get; set; }
        public Dictionary<int, MonthlyData> MonthlyData { get; set; }
        public int TradingDays { get; set; }
        public long TotalBars { get; set; }
    }

    public class MonthlyData
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public List<MarketBar> Bars { get; set; }
        public int TradingDays { get; set; }
    }

    public class MarketBar
    {
        public string Symbol { get; set; }
        public DateTime Timestamp { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public long Volume { get; set; }
        public double VWAP { get; set; }
    }

    public class DataGenerator
    {
        // Placeholder for actual data generation logic
    }

    public enum DataFormat
    {
        CSV,
        Parquet,
        Binary
    }
}