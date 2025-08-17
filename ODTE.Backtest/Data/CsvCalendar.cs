using CsvHelper;
using CsvHelper.Configuration;
using ODTE.Backtest.Core;
using System.Globalization;

namespace ODTE.Backtest.Data;

public sealed class CsvCalendar : IEconCalendar
{
    private readonly List<EconEvent> _evts;

    public CsvCalendar(string path, string timezone)
    {
        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true });

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

    public List<EconEvent> GetEvents(DateOnly startDate, DateOnly endDate)
    {
        var start = startDate.ToDateTime(TimeOnly.MinValue);
        var end = endDate.ToDateTime(TimeOnly.MaxValue);

        return _evts.Where(e => e.Ts >= start && e.Ts <= end)
                    .OrderBy(e => e.Ts)
                    .ToList();
    }

    private sealed class Row
    {
        public string ts { get; set; } = "";
        public string kind { get; set; } = "";
    }
}