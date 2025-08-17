using ODTE.Backtest.Synth;

namespace ODTE.Backtest.Engine;

/// <summary>
/// Intelligent day scheduling for diverse market regime coverage
/// </summary>
public static class SparseScheduler
{
    /// <summary>
    /// Order days using weighted round-robin across market regimes for optimal coverage
    /// </summary>
    public static List<DateOnly> Order(IEnumerable<DateOnly> days, Dictionary<DateOnly, DayTag> tags)
    {
        Console.WriteLine($"📅 Sparse scheduler organizing {days.Count()} days...");

        // Group days by their composite key (archetype|vol_bucket|event_status)
        var buckets = days
            .GroupBy(d => GetBucketKey(tags, d))
            .ToDictionary(
                g => g.Key,
                g => new Queue<DateOnly>(g.OrderBy(x => x))
            );

        // Define weights for different market conditions (higher = more frequent sampling)
        var weights = GetDefaultWeights();

        // Print bucket distribution
        Console.WriteLine("📊 Market regime distribution:");
        foreach (var (key, queue) in buckets.OrderBy(kvp => kvp.Key))
        {
            var weight = GetWeightForKey(weights, key);
            Console.WriteLine($"   {key}: {queue.Count} days (weight: {weight})");
        }

        // Build output list using weighted round-robin
        var outputList = new List<DateOnly>();
        var totalDays = days.Count();

        while (buckets.Values.Any(q => q.Count > 0))
        {
            foreach (var (key, queue) in buckets.ToArray())
            {
                if (queue.Count == 0) continue;

                var weight = GetWeightForKey(weights, key);

                // Take 'weight' number of days from this bucket
                for (int i = 0; i < weight && queue.Count > 0; i++)
                {
                    outputList.Add(queue.Dequeue());
                }
            }
        }

        // Progress indicator
        Console.WriteLine($"✅ Scheduled {outputList.Count} days with diverse market regime coverage");

        return outputList;
    }

    /// <summary>
    /// Order days with custom priority for specific investigation
    /// </summary>
    public static List<DateOnly> OrderWithPriority(
        IEnumerable<DateOnly> days,
        Dictionary<DateOnly, DayTag> tags,
        Func<DayTag, int> priorityFunc)
    {
        var dayList = days.ToList();
        var prioritized = dayList
            .Select(d => new
            {
                Date = d,
                Tag = tags.GetValueOrDefault(d),
                Priority = tags.ContainsKey(d) ? priorityFunc(tags[d]) : 0
            })
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.Date)
            .Select(x => x.Date)
            .ToList();

        Console.WriteLine($"📅 Custom priority scheduling applied to {prioritized.Count} days");
        return prioritized;
    }

    /// <summary>
    /// Create a deterministic but diverse subset of days for quick testing
    /// </summary>
    public static List<DateOnly> CreateTestSubset(
        IEnumerable<DateOnly> days,
        Dictionary<DateOnly, DayTag> tags,
        int targetCount)
    {
        var dayList = days.ToList();
        if (dayList.Count <= targetCount)
            return dayList;

        // Group by bucket and take proportional samples
        var buckets = dayList
            .GroupBy(d => GetBucketKey(tags, d))
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x).ToList());

        var result = new List<DateOnly>();
        var bucketCount = buckets.Count;
        var perBucket = Math.Max(1, targetCount / bucketCount);

        foreach (var (key, bucket) in buckets)
        {
            var takeCount = Math.Min(perBucket, bucket.Count);

            // Take evenly spaced samples from each bucket
            var step = Math.Max(1, bucket.Count / takeCount);
            for (int i = 0; i < bucket.Count && result.Count < targetCount; i += step)
            {
                result.Add(bucket[i]);
            }
        }

        // Fill remainder if needed
        var remaining = targetCount - result.Count;
        if (remaining > 0)
        {
            var unused = dayList.Except(result).OrderBy(d => d).Take(remaining);
            result.AddRange(unused);
        }

        Console.WriteLine($"📅 Created test subset: {result.Count} days from {bucketCount} market regimes");
        return result.OrderBy(d => d).ToList();
    }

    private static string GetBucketKey(Dictionary<DateOnly, DayTag> tags, DateOnly date)
    {
        if (!tags.TryGetValue(date, out var tag))
        {
            // Default classification if no tag exists
            return "unknown|normal|noevent";
        }

        var eventStatus = string.IsNullOrWhiteSpace(tag.EventTags) ? "noevent" : "event";
        return $"{tag.Archetype}|{tag.VolBucket}|{eventStatus}".ToLowerInvariant();
    }

    private static Dictionary<string, int> GetDefaultWeights()
    {
        // Weights determine how many days to take from each bucket per round
        // Higher weights = more frequent sampling of that regime
        return new Dictionary<string, int>
        {
            // Calm markets - highest weight for baseline performance
            {"calm_range|calm|noevent", 4},
            {"trend_neutral|normal|noevent", 3},
            
            // Trending markets
            {"trend_up|normal|noevent", 3},
            {"trend_dn|normal|noevent", 3},
            
            // Volatile conditions - important for risk management
            {"volatile_spike|volatile|noevent", 2},
            {"panic_spike|extreme|noevent", 2},
            
            // Event days - critical for strategy robustness
            {"event_day|*|event", 2},
            {"event_spike_fade|*|event", 2},
            
            // Wildcards for any unmatched patterns
            {"*|volatile|*", 2},
            {"*|extreme|*", 2},
            {"*|*|event", 2},
            
            // Default fallback
            {"*|*|*", 1}
        };
    }

    private static int GetWeightForKey(Dictionary<string, int> weights, string key)
    {
        // Exact match first
        if (weights.TryGetValue(key, out var exactWeight))
            return exactWeight;

        // Wildcard matching
        var keyParts = key.Split('|');
        foreach (var (pattern, weight) in weights)
        {
            var patternParts = pattern.Split('|');
            if (patternParts.Length != 3 || keyParts.Length != 3)
                continue;

            bool matches = true;
            for (int i = 0; i < 3; i++)
            {
                if (patternParts[i] != "*" && patternParts[i] != keyParts[i])
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
                return weight;
        }

        // Default weight if no pattern matches
        return 1;
    }

    /// <summary>
    /// Analyze the distribution of a day schedule
    /// </summary>
    public static void AnalyzeSchedule(List<DateOnly> schedule, Dictionary<DateOnly, DayTag> tags)
    {
        var buckets = schedule
            .GroupBy(d => GetBucketKey(tags, d))
            .OrderBy(g => g.Key)
            .ToList();

        Console.WriteLine("\n📊 Schedule Analysis:");
        Console.WriteLine($"Total days: {schedule.Count}");
        Console.WriteLine($"Date range: {schedule.First():yyyy-MM-dd} to {schedule.Last():yyyy-MM-dd}");
        Console.WriteLine($"Unique market regimes: {buckets.Count}");

        Console.WriteLine("\nRegime distribution:");
        foreach (var bucket in buckets)
        {
            var percentage = (double)bucket.Count() / schedule.Count * 100;
            Console.WriteLine($"  {bucket.Key}: {bucket.Count()} days ({percentage:F1}%)");
        }

        // Check for even distribution across months
        var monthDist = schedule.GroupBy(d => $"{d.Year}-{d.Month:00}").OrderBy(g => g.Key);
        Console.WriteLine("\nMonthly distribution:");
        foreach (var month in monthDist)
        {
            Console.WriteLine($"  {month.Key}: {month.Count()} days");
        }
    }
}