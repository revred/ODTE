using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using ODTE.Backtest.Core;

namespace ODTE.Backtest.Data;

public sealed class CsvCalendar : IEconCalendar
{
    private readonly List<EconEvent> _evts;
    
    public CsvCalendar(string path, string timezone)
    {
        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture){ HasHeaderRecord = true });
        
        _evts = csv.GetRecords<Row>()
            .Select(r => new EconEvent(
                DateTime.Parse(r.ts).ToUtc(), 
                r.kind))
            .ToList();
    }
    
    public EconEvent? NextEventAfter(DateTime ts) 
        => _evts.Where(e => e.Ts >= ts)
                .OrderBy(e => e.Ts)
                .FirstOrDefault();

    private sealed class Row 
    { 
        public string ts { get; set; } = ""; 
        public string kind { get; set; } = ""; 
    }
}