namespace ODTE.Backtest.Synth;

/// <summary>
/// Represents market regime and event tags for a specific trading day
/// </summary>
public sealed record DayTag(
    DateOnly Date,
    string Archetype,      // calm_range, trend_up, trend_dn, volatile_spike, event_spike_fade
    string VolBucket,      // calm, normal, volatile, extreme
    string EventTags,      // fed, cpi, nfp, opex, fomc, earnings, holiday
    double? VixLevel = null,
    double? OpenToCloseMove = null
);

/// <summary>
/// Manages day classification and tagging for sparse scheduling
/// </summary>
public static class DayTags
{
    /// <summary>
    /// Load day tags from CSV file or generate them from market data
    /// </summary>
    public static Dictionary<DateOnly, DayTag> Load(string pathCsv)
    {
        var dict = new Dictionary<DateOnly, DayTag>();

        if (!File.Exists(pathCsv))
        {
            Console.WriteLine($"Day tags file not found at {pathCsv}. Will generate tags dynamically.");
            return dict;
        }

        try
        {
            var lines = File.ReadAllLines(pathCsv);
            if (lines.Length <= 1)
            {
                Console.WriteLine("Day tags file is empty.");
                return dict;
            }

            // Expected format: date,archetype,vol_bucket,event_tags,vix_level,otc_move
            foreach (var line in lines.Skip(1))
            {
                var parts = line.Split(',');
                if (parts.Length < 4) continue;

                var date = DateOnly.Parse(parts[0]);
                var archetype = parts[1].Trim();
                var volBucket = parts[2].Trim();
                var eventTags = parts[3].Trim();

                double? vix = parts.Length > 4 && double.TryParse(parts[4], out var v) ? v : null;
                double? otcMove = parts.Length > 5 && double.TryParse(parts[5], out var m) ? m : null;

                dict[date] = new DayTag(date, archetype, volBucket, eventTags, vix, otcMove);
            }

            Console.WriteLine($"Loaded {dict.Count} day tags from {pathCsv}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading day tags: {ex.Message}");
        }

        return dict;
    }

    /// <summary>
    /// Generate day tags based on market data and known events
    /// </summary>
    public static Dictionary<DateOnly, DayTag> Generate(
        DateOnly startDate,
        DateOnly endDate,
        Dictionary<DateOnly, double>? vixData = null,
        HashSet<DateOnly>? fedDays = null,
        HashSet<DateOnly>? cpiDays = null,
        HashSet<DateOnly>? nfpDays = null,
        HashSet<DateOnly>? opexDays = null)
    {
        var tags = new Dictionary<DateOnly, DayTag>();

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            // Skip weekends
            var dayOfWeek = date.DayOfWeek;
            if (dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday)
                continue;

            // Determine event tags
            var events = new List<string>();
            if (fedDays?.Contains(date) == true) events.Add("fed");
            if (cpiDays?.Contains(date) == true) events.Add("cpi");
            if (nfpDays?.Contains(date) == true) events.Add("nfp");
            if (opexDays?.Contains(date) == true) events.Add("opex");

            // Determine if it's monthly opex (3rd Friday)
            if (IsMonthlyOpex(date)) events.Add("opex");

            var eventTags = string.Join(",", events);

            // Get VIX level if available
            double? vix = vixData?.GetValueOrDefault(date);

            // Classify volatility bucket based on VIX
            string volBucket = ClassifyVolatility(vix);

            // Determine archetype based on events and volatility
            string archetype = ClassifyArchetype(volBucket, events.Any());

            tags[date] = new DayTag(date, archetype, volBucket, eventTags, vix, null);
        }

        return tags;
    }

    /// <summary>
    /// Save day tags to CSV file for future use
    /// </summary>
    public static void Save(Dictionary<DateOnly, DayTag> tags, string pathCsv)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(pathCsv) ?? ".");

        using var writer = new StreamWriter(pathCsv);
        writer.WriteLine("date,archetype,vol_bucket,event_tags,vix_level,otc_move");

        foreach (var (date, tag) in tags.OrderBy(kvp => kvp.Key))
        {
            writer.WriteLine($"{date:yyyy-MM-dd},{tag.Archetype},{tag.VolBucket},{tag.EventTags}," +
                           $"{tag.VixLevel?.ToString("F2") ?? ""},{tag.OpenToCloseMove?.ToString("F4") ?? ""}");
        }

        Console.WriteLine($"Saved {tags.Count} day tags to {pathCsv}");
    }

    private static bool IsMonthlyOpex(DateOnly date)
    {
        // Third Friday of the month
        if (date.DayOfWeek != DayOfWeek.Friday) return false;

        var firstFriday = new DateOnly(date.Year, date.Month, 1);
        while (firstFriday.DayOfWeek != DayOfWeek.Friday)
            firstFriday = firstFriday.AddDays(1);

        var thirdFriday = firstFriday.AddDays(14);
        return date == thirdFriday;
    }

    private static string ClassifyVolatility(double? vix)
    {
        if (!vix.HasValue) return "normal";

        return vix.Value switch
        {
            < 12 => "calm",
            < 20 => "normal",
            < 30 => "volatile",
            _ => "extreme"
        };
    }

    private static string ClassifyArchetype(string volBucket, bool hasEvents)
    {
        if (hasEvents)
        {
            return volBucket == "volatile" || volBucket == "extreme"
                ? "event_spike_fade"
                : "event_day";
        }

        return volBucket switch
        {
            "calm" => "calm_range",
            "normal" => "trend_neutral",
            "volatile" => "volatile_spike",
            "extreme" => "panic_spike",
            _ => "unknown"
        };
    }

    /// <summary>
    /// Get a human-readable description of the day's characteristics
    /// </summary>
    public static string Describe(DayTag tag)
    {
        var desc = $"{tag.Date:yyyy-MM-dd}: {tag.Archetype} ({tag.VolBucket})";
        if (!string.IsNullOrWhiteSpace(tag.EventTags))
            desc += $" [Events: {tag.EventTags}]";
        if (tag.VixLevel.HasValue)
            desc += $" VIX={tag.VixLevel:F2}";
        return desc;
    }
}