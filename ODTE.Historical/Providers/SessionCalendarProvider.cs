using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ODTE.Historical.Providers
{
    public sealed class SessionCalendarProvider
    {
        private readonly ILogger<SessionCalendarProvider> _logger;
        private readonly Dictionary<string, ProductCalendar> _calendars;
        private readonly Dictionary<DateTime, MarketSession> _sessionCache;
        private readonly Dictionary<DateTime, bool> _holidayCache;

        public SessionCalendarProvider(ILogger<SessionCalendarProvider> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _calendars = new Dictionary<string, ProductCalendar>();
            _sessionCache = new Dictionary<DateTime, MarketSession>();
            _holidayCache = new Dictionary<DateTime, bool>();
            
            InitializeProductCalendars();
        }

        public ProductCalendar GetCalendar(string product)
        {
            if (_calendars.TryGetValue(product.ToUpper(), out var calendar))
            {
                return calendar;
            }

            // _logger?.LogWarning($"No calendar found for product {product}, using default");
            return _calendars["DEFAULT"];
        }

        public MarketSession GetSession(string product, DateTime date)
        {
            var calendar = GetCalendar(product);
            var dateKey = date.Date;

            if (_sessionCache.TryGetValue(dateKey, out var cachedSession))
            {
                return cachedSession;
            }

            var session = BuildMarketSession(calendar, dateKey);
            _sessionCache[dateKey] = session;

            _logger.LogDebug("Built market session for {Product} on {Date}: {Open} - {Close}",
                product, dateKey.ToShortDateString(), session.RegularOpen, session.RegularClose);

            return session;
        }

        public bool IsMarketOpen(string product, DateTime timestamp)
        {
            var session = GetSession(product, timestamp.Date);
            
            if (session.IsHoliday)
                return false;

            var timeOfDay = timestamp.TimeOfDay;
            return timeOfDay >= session.RegularOpen.TimeOfDay && 
                   timeOfDay <= session.RegularClose.TimeOfDay;
        }

        public bool IsEarlyClose(string product, DateTime date)
        {
            var session = GetSession(product, date);
            return session.IsEarlyClose;
        }

        public DateTime GetSessionClose(string product, DateTime date)
        {
            var session = GetSession(product, date);
            return session.IsEarlyClose ? session.EarlyClose : session.RegularClose;
        }

        public DateTime GetNextTradingDay(string product, DateTime date)
        {
            var currentDate = date.Date.AddDays(1);
            
            while (currentDate <= date.AddDays(10)) // Safety limit
            {
                var session = GetSession(product, currentDate);
                if (!session.IsHoliday && !IsWeekend(currentDate))
                {
                    return currentDate;
                }
                currentDate = currentDate.AddDays(1);
            }

            throw new InvalidOperationException($"Could not find next trading day after {date} for {product}");
        }

        public DateTime GetPreviousTradingDay(string product, DateTime date)
        {
            var currentDate = date.Date.AddDays(-1);
            
            while (currentDate >= date.AddDays(-10)) // Safety limit
            {
                var session = GetSession(product, currentDate);
                if (!session.IsHoliday && !IsWeekend(currentDate))
                {
                    return currentDate;
                }
                currentDate = currentDate.AddDays(-1);
            }

            throw new InvalidOperationException($"Could not find previous trading day before {date} for {product}");
        }

        public TradingWeek GetTradingWeek(string product, DateTime weekOf)
        {
            var monday = GetMondayOfWeek(weekOf);
            var tradingDays = new List<DateTime>();

            for (int i = 0; i < 5; i++) // Monday through Friday
            {
                var day = monday.AddDays(i);
                var session = GetSession(product, day);
                
                if (!session.IsHoliday)
                {
                    tradingDays.Add(day);
                }
            }

            return new TradingWeek
            {
                WeekOf = monday,
                Product = product,
                TradingDays = tradingDays.ToArray(),
                HasEarlyClose = tradingDays.Any(day => IsEarlyClose(product, day)),
                HasHoliday = tradingDays.Count < 5
            };
        }

        public TimeWindow GetDecisionWindow(string product, DateTime date, TimeOnly decisionTime, int bufferMinutes = 5)
        {
            var session = GetSession(product, date);
            
            if (session.IsHoliday)
            {
                throw new InvalidOperationException($"Cannot create decision window on holiday: {date:yyyy-MM-dd}");
            }

            var decisionDateTime = date.Date.Add(decisionTime.ToTimeSpan());
            var windowStart = decisionDateTime.AddMinutes(-bufferMinutes);
            var windowEnd = decisionDateTime.AddMinutes(bufferMinutes);

            // Ensure window is within trading hours
            var sessionStart = session.RegularOpen;
            var sessionEnd = session.IsEarlyClose ? session.EarlyClose : session.RegularClose;

            if (windowStart < sessionStart)
            {
                // _logger?.LogWarning($"Decision window start {windowStart} before session open {sessionStart}, adjusting");
                windowStart = sessionStart;
            }

            if (windowEnd > sessionEnd)
            {
                // _logger?.LogWarning($"Decision window end {windowEnd} after session close {sessionEnd}, adjusting");
                windowEnd = sessionEnd;
            }

            return new TimeWindow
            {
                Start = windowStart,
                End = windowEnd,
                DecisionTime = decisionDateTime,
                IsValid = windowStart < windowEnd,
                Duration = windowEnd - windowStart
            };
        }

        public ExpirationSchedule GetExpirationSchedule(string product, DateTime fromDate, int weeksAhead = 8)
        {
            var expirations = new List<ExpirationDate>();
            var currentDate = fromDate.Date;

            for (int week = 0; week < weeksAhead; week++)
            {
                var weekStart = currentDate.AddDays(week * 7);
                var tradingWeek = GetTradingWeek(product, weekStart);

                // Add standard weekly expirations (typically Monday, Wednesday, Friday)
                foreach (var day in tradingWeek.TradingDays)
                {
                    if (IsWeeklyExpirationDay(day.DayOfWeek))
                    {
                        var session = GetSession(product, day);
                        var expirationTime = GetExpirationTime(product, day);

                        expirations.Add(new ExpirationDate
                        {
                            Date = day,
                            ExpirationTime = expirationTime,
                            IsWeekly = true,
                            IsMonthly = IsMonthlyExpiration(day),
                            IsQuarterly = IsQuarterlyExpiration(day),
                            ProductType = GetProductType(product),
                            TradingSession = session
                        });
                    }
                }
            }

            return new ExpirationSchedule
            {
                Product = product,
                FromDate = fromDate,
                ToDate = fromDate.AddDays(weeksAhead * 7),
                Expirations = expirations.OrderBy(e => e.Date).ToArray()
            };
        }

        public DateTime GetExpirationTime(string product, DateTime expirationDate)
        {
            var calendar = GetCalendar(product);
            var session = GetSession(product, expirationDate);

            return calendar.ProductType switch
            {
                ProductType.FuturesOptions => expirationDate.Date.AddHours(14).AddMinutes(30), // 2:30 PM ET for CL
                ProductType.EquityOptions => expirationDate.Date.AddHours(16), // 4:00 PM ET for USO
                ProductType.IndexOptions => expirationDate.Date.AddHours(16).AddMinutes(15), // 4:15 PM ET
                _ => session.RegularClose
            };
        }

        public bool IsExpirationDay(string product, DateTime date)
        {
            var schedule = GetExpirationSchedule(product, date, 1);
            return schedule.Expirations.Any(exp => exp.Date.Date == date.Date);
        }

        private void InitializeProductCalendars()
        {
            // CL (Crude Oil Futures Options)
            _calendars["CL"] = new ProductCalendar
            {
                Product = "CL",
                ProductType = ProductType.FuturesOptions,
                Exchange = "NYMEX",
                TimeZone = "America/New_York",
                RegularOpen = new TimeOnly(9, 0, 0),
                RegularClose = new TimeOnly(14, 30, 0), // 2:30 PM for futures options
                PreMarketOpen = new TimeOnly(8, 0, 0),
                PostMarketClose = new TimeOnly(17, 0, 0)
            };

            // USO (United States Oil ETF)
            _calendars["USO"] = new ProductCalendar
            {
                Product = "USO",
                ProductType = ProductType.EquityOptions,
                Exchange = "ARCA",
                TimeZone = "America/New_York",
                RegularOpen = new TimeOnly(9, 30, 0),
                RegularClose = new TimeOnly(16, 0, 0), // 4:00 PM for equity options
                PreMarketOpen = new TimeOnly(4, 0, 0),
                PostMarketClose = new TimeOnly(20, 0, 0)
            };

            // Default calendar
            _calendars["DEFAULT"] = new ProductCalendar
            {
                Product = "DEFAULT",
                ProductType = ProductType.EquityOptions,
                Exchange = "NYSE",
                TimeZone = "America/New_York",
                RegularOpen = new TimeOnly(9, 30, 0),
                RegularClose = new TimeOnly(16, 0, 0),
                PreMarketOpen = new TimeOnly(4, 0, 0),
                PostMarketClose = new TimeOnly(20, 0, 0)
            };

            _logger.LogInformation("Initialized calendars for {Count} products", _calendars.Count);
        }

        private MarketSession BuildMarketSession(ProductCalendar calendar, DateTime date)
        {
            var isHoliday = IsHoliday(date);
            var isEarlyClose = IsEarlyCloseDay(date);
            
            var regularOpen = date.Date.Add(calendar.RegularOpen.ToTimeSpan());
            var regularClose = date.Date.Add(calendar.RegularClose.ToTimeSpan());
            var earlyClose = isEarlyClose ? date.Date.AddHours(13) : regularClose; // 1:00 PM early close

            return new MarketSession
            {
                Date = date,
                Product = calendar.Product,
                RegularOpen = regularOpen,
                RegularClose = regularClose,
                EarlyClose = earlyClose,
                IsHoliday = isHoliday,
                IsEarlyClose = isEarlyClose,
                IsWeekend = IsWeekend(date),
                Calendar = calendar
            };
        }

        private bool IsHoliday(DateTime date)
        {
            var dateKey = date.Date;
            
            if (_holidayCache.TryGetValue(dateKey, out var cached))
            {
                return cached;
            }

            var isHoliday = CheckHoliday(date);
            _holidayCache[dateKey] = isHoliday;
            
            return isHoliday;
        }

        private bool CheckHoliday(DateTime date)
        {
            // Major US market holidays
            var year = date.Year;
            
            // New Year's Day
            if (IsObservedHoliday(new DateTime(year, 1, 1), date))
                return true;
                
            // Martin Luther King Jr. Day (3rd Monday in January)
            if (date == GetNthWeekdayOfMonth(year, 1, DayOfWeek.Monday, 3))
                return true;
                
            // Presidents Day (3rd Monday in February)
            if (date == GetNthWeekdayOfMonth(year, 2, DayOfWeek.Monday, 3))
                return true;
                
            // Good Friday (Friday before Easter)
            var easter = GetEasterSunday(year);
            if (date == easter.AddDays(-2))
                return true;
                
            // Memorial Day (Last Monday in May)
            if (date == GetLastWeekdayOfMonth(year, 5, DayOfWeek.Monday))
                return true;
                
            // Juneteenth (June 19th, observed if on weekend)
            if (IsObservedHoliday(new DateTime(year, 6, 19), date))
                return true;
                
            // Independence Day (July 4th, observed if on weekend)
            if (IsObservedHoliday(new DateTime(year, 7, 4), date))
                return true;
                
            // Labor Day (1st Monday in September)
            if (date == GetNthWeekdayOfMonth(year, 9, DayOfWeek.Monday, 1))
                return true;
                
            // Thanksgiving (4th Thursday in November)
            if (date == GetNthWeekdayOfMonth(year, 11, DayOfWeek.Thursday, 4))
                return true;
                
            // Christmas (December 25th, observed if on weekend)
            if (IsObservedHoliday(new DateTime(year, 12, 25), date))
                return true;

            return false;
        }

        private bool IsEarlyCloseDay(DateTime date)
        {
            var year = date.Year;
            
            // Day after Thanksgiving
            var thanksgiving = GetNthWeekdayOfMonth(year, 11, DayOfWeek.Thursday, 4);
            if (date == thanksgiving.AddDays(1))
                return true;
                
            // Christmas Eve (if on weekday)
            var christmasEve = new DateTime(year, 12, 24);
            if (date == christmasEve && !IsWeekend(christmasEve))
                return true;
                
            // July 3rd (if July 4th is on Monday)
            var july4th = new DateTime(year, 7, 4);
            if (july4th.DayOfWeek == DayOfWeek.Monday && date == july4th.AddDays(-1))
                return true;

            return false;
        }

        private bool IsWeekend(DateTime date) => 
            date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

        private bool IsWeeklyExpirationDay(DayOfWeek dayOfWeek) =>
            dayOfWeek is DayOfWeek.Monday or DayOfWeek.Wednesday or DayOfWeek.Friday;

        private bool IsMonthlyExpiration(DateTime date) =>
            date.DayOfWeek == DayOfWeek.Friday && date.Day >= 15 && date.Day <= 21;

        private bool IsQuarterlyExpiration(DateTime date) =>
            IsMonthlyExpiration(date) && date.Month % 3 == 0;

        private ProductType GetProductType(string product) =>
            product.ToUpper() switch
            {
                "CL" => ProductType.FuturesOptions,
                "USO" => ProductType.EquityOptions,
                _ => ProductType.EquityOptions
            };

        private DateTime GetMondayOfWeek(DateTime date)
        {
            var daysFromMonday = (int)date.DayOfWeek - (int)DayOfWeek.Monday;
            if (daysFromMonday < 0) daysFromMonday += 7;
            return date.Date.AddDays(-daysFromMonday);
        }

        private bool IsObservedHoliday(DateTime actualHoliday, DateTime testDate)
        {
            if (actualHoliday.Date == testDate.Date)
                return true;
                
            // If holiday falls on Saturday, observed on Friday
            if (actualHoliday.DayOfWeek == DayOfWeek.Saturday && testDate == actualHoliday.AddDays(-1))
                return true;
                
            // If holiday falls on Sunday, observed on Monday
            if (actualHoliday.DayOfWeek == DayOfWeek.Sunday && testDate == actualHoliday.AddDays(1))
                return true;
                
            return false;
        }

        private DateTime GetNthWeekdayOfMonth(int year, int month, DayOfWeek dayOfWeek, int occurrence)
        {
            var firstDay = new DateTime(year, month, 1);
            var firstOccurrence = firstDay.AddDays(((int)dayOfWeek - (int)firstDay.DayOfWeek + 7) % 7);
            return firstOccurrence.AddDays((occurrence - 1) * 7);
        }

        private DateTime GetLastWeekdayOfMonth(int year, int month, DayOfWeek dayOfWeek)
        {
            var lastDay = new DateTime(year, month, DateTime.DaysInMonth(year, month));
            var daysBack = ((int)lastDay.DayOfWeek - (int)dayOfWeek + 7) % 7;
            return lastDay.AddDays(-daysBack);
        }

        private DateTime GetEasterSunday(int year)
        {
            // Gregorian calendar Easter calculation
            var a = year % 19;
            var b = year / 100;
            var c = year % 100;
            var d = b / 4;
            var e = b % 4;
            var f = (b + 8) / 25;
            var g = (b - f + 1) / 3;
            var h = (19 * a + b - d - g + 15) % 30;
            var i = c / 4;
            var k = c % 4;
            var l = (32 + 2 * e + 2 * i - h - k) % 7;
            var m = (a + 11 * h + 22 * l) / 451;
            var month = (h + l - 7 * m + 114) / 31;
            var day = ((h + l - 7 * m + 114) % 31) + 1;
            
            return new DateTime(year, month, day);
        }
    }

    public sealed class ProductCalendar
    {
        public string Product { get; set; } = "";
        public ProductType ProductType { get; set; }
        public string Exchange { get; set; } = "";
        public string TimeZone { get; set; } = "";
        public TimeOnly RegularOpen { get; set; }
        public TimeOnly RegularClose { get; set; }
        public TimeOnly PreMarketOpen { get; set; }
        public TimeOnly PostMarketClose { get; set; }
    }

    public sealed class MarketSession
    {
        public DateTime Date { get; set; }
        public string Product { get; set; } = "";
        public DateTime RegularOpen { get; set; }
        public DateTime RegularClose { get; set; }
        public DateTime EarlyClose { get; set; }
        public bool IsHoliday { get; set; }
        public bool IsEarlyClose { get; set; }
        public bool IsWeekend { get; set; }
        public ProductCalendar Calendar { get; set; } = new();
    }

    public sealed class TradingWeek
    {
        public DateTime WeekOf { get; set; }
        public string Product { get; set; } = "";
        public DateTime[] TradingDays { get; set; } = Array.Empty<DateTime>();
        public bool HasEarlyClose { get; set; }
        public bool HasHoliday { get; set; }
    }

    public sealed class TimeWindow
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public DateTime DecisionTime { get; set; }
        public bool IsValid { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public sealed class ExpirationSchedule
    {
        public string Product { get; set; } = "";
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public ExpirationDate[] Expirations { get; set; } = Array.Empty<ExpirationDate>();
    }

    public sealed class ExpirationDate
    {
        public DateTime Date { get; set; }
        public DateTime ExpirationTime { get; set; }
        public bool IsWeekly { get; set; }
        public bool IsMonthly { get; set; }
        public bool IsQuarterly { get; set; }
        public ProductType ProductType { get; set; }
        public MarketSession TradingSession { get; set; } = new();
    }

    public enum ProductType
    {
        EquityOptions,
        FuturesOptions,
        IndexOptions
    }
}