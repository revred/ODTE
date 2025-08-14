using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ODTE.Syntricks;

/// <summary>
/// Represents a single market tick with OHLCV + derived indicators
/// </summary>
public record SpotTick
{
    public DateTime Timestamp { get; init; }
    public double Open { get; init; }
    public double High { get; init; }
    public double Low { get; init; }
    public double Close { get; init; }
    public long Volume { get; init; }
    public double Vwap { get; init; }
    public double Atr { get; init; }
    public double SessionPct { get; init; }  // 0.0 - 1.0 session completion
}

/// <summary>
/// Interface for streaming market data in real-time or replay mode
/// </summary>
public interface IMarketStream : IAsyncEnumerable<SpotTick>
{
    /// <summary>
    /// Start the market stream
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Stop the market stream
    /// </summary>
    Task StopAsync();
    
    /// <summary>
    /// Current replay speed multiplier (1.0 = real-time, 5.0 = 5x speed)
    /// </summary>
    double ReplaySpeed { get; set; }
    
    /// <summary>
    /// Check if stream is currently active
    /// </summary>
    bool IsActive { get; }
}

/// <summary>
/// Historical data replay stream that can accelerate playback
/// </summary>
public class HistoricalMarketStream : IMarketStream
{
    private readonly List<SpotTick> _ticks;
    private readonly TimeSpan _baseInterval;
    private bool _isActive;
    private CancellationTokenSource? _cancellationTokenSource;

    public double ReplaySpeed { get; set; } = 1.0;
    public bool IsActive => _isActive;

    public HistoricalMarketStream(IEnumerable<SpotTick> historicalTicks, TimeSpan baseInterval)
    {
        _ticks = historicalTicks.ToList();
        _baseInterval = baseInterval;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _isActive = true;
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    }

    public async Task StopAsync()
    {
        _isActive = false;
        _cancellationTokenSource?.Cancel();
    }

    public async IAsyncEnumerator<SpotTick> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        if (!_isActive)
            yield break;

        var combined = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, 
            _cancellationTokenSource?.Token ?? CancellationToken.None);

        try
        {
            foreach (var tick in _ticks)
            {
                if (combined.Token.IsCancellationRequested)
                    yield break;

                yield return tick;

                // Delay based on replay speed
                if (ReplaySpeed > 0)
                {
                    var delay = TimeSpan.FromMilliseconds(_baseInterval.TotalMilliseconds / ReplaySpeed);
                    if (delay.TotalMilliseconds > 1) // Don't delay for very fast replay
                    {
                        await Task.Delay(delay, combined.Token);
                    }
                }
            }
        }
        finally
        {
            _isActive = false;
        }
    }
}

/// <summary>
/// Block bootstrap stream that stitches historical segments to create synthetic days
/// </summary>
public class BootstrapMarketStream : IMarketStream
{
    private readonly IBlockBootstrap _bootstrap;
    private readonly string _archetype;
    private readonly Random _random;
    private bool _isActive;

    public double ReplaySpeed { get; set; } = 1.0;
    public bool IsActive => _isActive;

    public BootstrapMarketStream(IBlockBootstrap bootstrap, string archetype, int seed = 42)
    {
        _bootstrap = bootstrap;
        _archetype = archetype;
        _random = new Random(seed);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _isActive = true;
    }

    public async Task StopAsync()
    {
        _isActive = false;
    }

    public async IAsyncEnumerator<SpotTick> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        if (!_isActive)
            yield break;

        // Generate synthetic day using block bootstrap
        var syntheticTicks = await _bootstrap.GenerateDayAsync(_archetype, _random);
        
        var baseInterval = TimeSpan.FromMinutes(1); // Assume 1-minute bars
        
        foreach (var tick in syntheticTicks)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;

            yield return tick;

            // Delay based on replay speed
            if (ReplaySpeed > 0)
            {
                var delay = TimeSpan.FromMilliseconds(baseInterval.TotalMilliseconds / ReplaySpeed);
                if (delay.TotalMilliseconds > 1)
                {
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }

        _isActive = false;
    }
}

/// <summary>
/// Interface for block bootstrap data generation
/// </summary>
public interface IBlockBootstrap
{
    /// <summary>
    /// Generate a synthetic trading day using historical blocks matching the archetype
    /// </summary>
    Task<IEnumerable<SpotTick>> GenerateDayAsync(string archetype, Random random);
}