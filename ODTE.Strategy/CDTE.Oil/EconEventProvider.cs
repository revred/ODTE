namespace ODTE.Strategy.CDTE.Oil
{
    public sealed class EconEventProvider
    {
        private readonly Dictionary<DateTime, EconEvent[]> _eventCache = new();

        public async Task<EconEvent[]> GetEventsInWindow(DateTime start, DateTime end)
        {
            var events = new List<EconEvent>();

            var current = start.Date;
            while (current <= end.Date)
            {
                if (_eventCache.TryGetValue(current, out var dayEvents))
                {
                    events.AddRange(dayEvents);
                }
                else
                {
                    var generatedEvents = GenerateEventsForDate(current);
                    _eventCache[current] = generatedEvents;
                    events.AddRange(generatedEvents);
                }
                current = current.AddDays(1);
            }

            return events.Where(e => e.DateTime >= start && e.DateTime <= end).ToArray();
        }

        public async Task<bool> HasHighImpactEventWithin(DateTime reference, int days)
        {
            var start = reference.AddDays(-days);
            var end = reference.AddDays(days);
            var events = await GetEventsInWindow(start, end);

            return events.Any(e => e.Impact >= EventImpact.High);
        }

        private EconEvent[] GenerateEventsForDate(DateTime date)
        {
            var events = new List<EconEvent>();

            // EIA Crude Oil Inventory (Wednesdays at 10:30 AM ET)
            if (date.DayOfWeek == DayOfWeek.Wednesday)
            {
                events.Add(new EconEvent
                {
                    DateTime = date.Date.AddHours(10).AddMinutes(30),
                    Type = EventType.EIA_Inventory,
                    Impact = EventImpact.High,
                    Description = "EIA Crude Oil Inventory Report"
                });
            }

            // OPEC Meetings (typically first week of month)
            if (date.Day <= 7 && date.DayOfWeek == DayOfWeek.Thursday)
            {
                events.Add(new EconEvent
                {
                    DateTime = date.Date.AddHours(9),
                    Type = EventType.OPEC_Meeting,
                    Impact = EventImpact.Extreme,
                    Description = "OPEC Monthly Meeting"
                });
            }

            // Fed Rate Decisions (8 times per year)
            if (IsFedMeetingDate(date))
            {
                events.Add(new EconEvent
                {
                    DateTime = date.Date.AddHours(14),
                    Type = EventType.Fed_Decision,
                    Impact = EventImpact.High,
                    Description = "Federal Reserve Interest Rate Decision"
                });
            }

            // Geopolitical events (simplified)
            if (date.DayOfWeek == DayOfWeek.Friday && new Random(date.GetHashCode()).NextDouble() < 0.1)
            {
                events.Add(new EconEvent
                {
                    DateTime = date.Date.AddHours(16),
                    Type = EventType.Geopolitical,
                    Impact = EventImpact.Medium,
                    Description = "Geopolitical Development"
                });
            }

            return events.ToArray();
        }

        private bool IsFedMeetingDate(DateTime date)
        {
            // Simplified: Fed meets roughly every 6 weeks
            var fedMeetingMonths = new[] { 1, 3, 5, 6, 7, 9, 11, 12 };
            return fedMeetingMonths.Contains(date.Month) &&
                   date.Day >= 15 && date.Day <= 21 &&
                   date.DayOfWeek == DayOfWeek.Wednesday;
        }
    }

    public sealed class EconEvent
    {
        public DateTime DateTime { get; set; }
        public EventType Type { get; set; }
        public EventImpact Impact { get; set; }
        public string Description { get; set; } = "";
    }

    public enum EventType
    {
        EIA_Inventory,
        OPEC_Meeting,
        Fed_Decision,
        Geopolitical,
        Earnings,
        Economic_Data
    }

    public enum EventImpact
    {
        Low,
        Medium,
        High,
        Extreme
    }
}