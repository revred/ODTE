namespace ODTE.Synth;

/// <summary>
/// Microstructure patterns for realistic intraday behavior
/// Implements U-shaped volume, lunch lull, late-session effects
/// </summary>
public class Microstructure
{
    private readonly MicrostructureConfig _config;
    private readonly Random _random;

    public Microstructure(MicrostructureConfig config, int seed = 42)
    {
        _config = config;
        _random = new Random(seed);
    }

    /// <summary>
    /// Apply microstructure effects to a stream of ticks
    /// </summary>
    public IEnumerable<SpotTick> ApplyEffects(IEnumerable<SpotTick> baseTicks)
    {
        var ticks = baseTicks.ToList();
        if (ticks.Count == 0) yield break;

        var sessionStart = ticks.First().Timestamp;
        var sessionEnd = ticks.Last().Timestamp;
        var sessionDuration = sessionEnd - sessionStart;

        for (int i = 0; i < ticks.Count; i++)
        {
            var tick = ticks[i];
            var sessionPct = (double)i / ticks.Count;
            var currentTime = tick.Timestamp;

            // Apply volume shaping
            var volumeMultiplier = GetVolumeMultiplier(sessionPct, currentTime);
            var adjustedVolume = (long)(tick.Volume * volumeMultiplier);

            // Apply spread widening effects
            var spreadMultiplier = GetSpreadMultiplier(sessionPct, currentTime);
            
            // Adjust bid-ask spread implicitly through price noise
            var priceNoise = GetPriceNoise(sessionPct, currentTime) * spreadMultiplier;
            
            yield return tick with
            {
                Volume = Math.Max(adjustedVolume, 1000), // Minimum volume floor
                Close = tick.Close + priceNoise,
                High = Math.Max(tick.High, tick.Close + priceNoise),
                Low = Math.Min(tick.Low, tick.Close + priceNoise),
                SessionPct = sessionPct
            };
        }
    }

    /// <summary>
    /// Get volume multiplier based on session time (U-shaped pattern)
    /// </summary>
    private double GetVolumeMultiplier(double sessionPct, DateTime currentTime)
    {
        if (!_config.UShapedVolume)
            return 1.0;

        // U-shaped volume: higher at open/close, lower midday
        var baseMultiplier = Math.Pow(4 * sessionPct * (1 - sessionPct), 0.5) + 0.3;

        // Lunch lull effect
        if (_config.LunchLull && IsLunchTime(currentTime))
        {
            baseMultiplier *= 0.6; // 40% reduction during lunch
        }

        // Add some randomness
        var noise = 1.0 + (_random.NextDouble() - 0.5) * 0.3; // ±15% noise
        
        return Math.Max(baseMultiplier * noise, 0.2); // Minimum 20% of base volume
    }

    /// <summary>
    /// Get spread widening multiplier
    /// </summary>
    private double GetSpreadMultiplier(double sessionPct, DateTime currentTime)
    {
        var multiplier = 1.0;

        // Late session widening (after 3:30 PM ET = 20:30 UTC)
        if (_config.LateSessionWidening && sessionPct > 0.85)
        {
            var lateSessionFactor = (sessionPct - 0.85) / 0.15; // 0-1 over last 15%
            multiplier *= 1.0 + lateSessionFactor * 1.5; // Up to 2.5x wider at close
        }

        // Lunch time slight widening
        if (_config.LunchLull && IsLunchTime(currentTime))
        {
            multiplier *= 1.2; // 20% wider during lunch
        }

        return multiplier;
    }

    /// <summary>
    /// Get price noise for spread simulation
    /// </summary>
    private double GetPriceNoise(double sessionPct, DateTime currentTime)
    {
        // Base noise: 1-2 cents
        var baseNoise = 0.01 + _random.NextDouble() * 0.01;
        
        // Random direction
        var direction = _random.NextDouble() > 0.5 ? 1 : -1;
        
        return baseNoise * direction;
    }

    /// <summary>
    /// Check if current time is during lunch hours
    /// </summary>
    private bool IsLunchTime(DateTime currentTime)
    {
        if (!_config.LunchLull)
            return false;

        var lunchStart = ParseTimeToUtcHour(_config.LunchStart);
        var lunchEnd = ParseTimeToUtcHour(_config.LunchEnd);
        
        var currentHour = currentTime.Hour + currentTime.Minute / 60.0;
        
        return currentHour >= lunchStart && currentHour < lunchEnd;
    }

    /// <summary>
    /// Parse time string (HH:mm) to UTC hour decimal
    /// </summary>
    private double ParseTimeToUtcHour(string timeStr)
    {
        if (TimeSpan.TryParse(timeStr, out var time))
        {
            return time.TotalHours;
        }
        return 17.0; // Default to 12 PM ET = 17:00 UTC
    }
}

/// <summary>
/// Extension methods for applying microstructure to market streams
/// </summary>
public static class MicrostructureExtensions
{
    /// <summary>
    /// Apply microstructure effects to a market stream
    /// </summary>
    public static async IAsyncEnumerable<SpotTick> WithMicrostructure(
        this IAsyncEnumerable<SpotTick> stream, 
        Microstructure microstructure)
    {
        var buffer = new List<SpotTick>();
        
        await foreach (var tick in stream)
        {
            buffer.Add(tick);
            
            // Process in small batches to maintain streaming behavior
            if (buffer.Count >= 10)
            {
                var processed = microstructure.ApplyEffects(buffer);
                foreach (var processedTick in processed)
                {
                    yield return processedTick;
                }
                buffer.Clear();
            }
        }

        // Process remaining ticks
        if (buffer.Count > 0)
        {
            var processed = microstructure.ApplyEffects(buffer);
            foreach (var processedTick in processed)
            {
                yield return processedTick;
            }
        }
    }
}