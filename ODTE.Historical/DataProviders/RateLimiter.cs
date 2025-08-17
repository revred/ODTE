namespace ODTE.Historical.DataProviders;

/// <summary>
/// Thread-safe rate limiter with throttling support
/// </summary>
public class RateLimiter
{
    private readonly SemaphoreSlim _semaphore;
    private readonly Queue<DateTime> _requestTimes;
    private readonly int _maxRequests;
    private readonly TimeSpan _timeWindow;
    private DateTime? _throttledUntil;
    private readonly object _lock = new();

    public RateLimiter(int maxRequestsPerMinute, TimeSpan? timeWindow = null)
    {
        _maxRequests = maxRequestsPerMinute;
        _timeWindow = timeWindow ?? TimeSpan.FromMinutes(1);
        _semaphore = new SemaphoreSlim(1, 1);
        _requestTimes = new Queue<DateTime>();
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            // Check if we're throttled
            lock (_lock)
            {
                if (_throttledUntil.HasValue && DateTime.UtcNow < _throttledUntil.Value)
                {
                    var delay = _throttledUntil.Value - DateTime.UtcNow;
                    Thread.Sleep(delay);
                    _throttledUntil = null;
                }
            }

            // Clean up old request times
            var cutoff = DateTime.UtcNow - _timeWindow;
            while (_requestTimes.Count > 0 && _requestTimes.Peek() < cutoff)
            {
                _requestTimes.Dequeue();
            }

            // Check if we need to wait
            if (_requestTimes.Count >= _maxRequests)
            {
                var oldestRequest = _requestTimes.Peek();
                var waitTime = oldestRequest + _timeWindow - DateTime.UtcNow;

                if (waitTime > TimeSpan.Zero)
                {
                    await Task.Delay(waitTime, cancellationToken);

                    // Clean up again after waiting
                    cutoff = DateTime.UtcNow - _timeWindow;
                    while (_requestTimes.Count > 0 && _requestTimes.Peek() < cutoff)
                    {
                        _requestTimes.Dequeue();
                    }
                }
            }

            // Record this request
            _requestTimes.Enqueue(DateTime.UtcNow);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void SetThrottled(TimeSpan duration)
    {
        lock (_lock)
        {
            _throttledUntil = DateTime.UtcNow + duration;
        }
    }

    public RateLimitStatus GetStatus()
    {
        lock (_lock)
        {
            // Clean up old request times
            var cutoff = DateTime.UtcNow - _timeWindow;
            while (_requestTimes.Count > 0 && _requestTimes.Peek() < cutoff)
            {
                _requestTimes.Dequeue();
            }

            var isThrottled = _throttledUntil.HasValue && DateTime.UtcNow < _throttledUntil.Value;

            return new RateLimitStatus
            {
                RequestsRemaining = Math.Max(0, _maxRequests - _requestTimes.Count),
                RequestsPerMinute = _maxRequests,
                ResetTime = _requestTimes.Count > 0
                    ? _requestTimes.Peek() + _timeWindow
                    : DateTime.UtcNow,
                IsThrottled = isThrottled,
                RetryAfter = isThrottled
                    ? _throttledUntil!.Value - DateTime.UtcNow
                    : null
            };
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _requestTimes.Clear();
            _throttledUntil = null;
        }
    }
}