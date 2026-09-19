using System.Collections.Concurrent;

namespace Cia.Api.Services;

public enum TelegramUpdateDecision
{
    Allowed,
    Duplicate,
    RateLimited
}

public sealed class TelegramAbuseGuard
{
    private const int MaxMessagesPerMinute = 20;
    private static readonly TimeSpan UpdateRetention = TimeSpan.FromHours(24);
    private static readonly TimeSpan RateWindow = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<long, DateTimeOffset> _processedUpdates = new();
    private readonly ConcurrentDictionary<long, Queue<DateTimeOffset>> _chatWindows = new();
    private readonly TimeProvider _timeProvider;
    private long _checks;

    public TelegramAbuseGuard(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public TelegramUpdateDecision Evaluate(long updateId, long chatId)
    {
        var now = _timeProvider.GetUtcNow();
        if (updateId > 0 && !_processedUpdates.TryAdd(updateId, now))
        {
            return TelegramUpdateDecision.Duplicate;
        }

        var window = _chatWindows.GetOrAdd(chatId, _ => new Queue<DateTimeOffset>());
        lock (window)
        {
            while (window.Count > 0 && now - window.Peek() >= RateWindow)
            {
                window.Dequeue();
            }

            if (window.Count >= MaxMessagesPerMinute)
            {
                return TelegramUpdateDecision.RateLimited;
            }

            window.Enqueue(now);
        }

        if (Interlocked.Increment(ref _checks) % 256 == 0)
        {
            Prune(now);
        }

        return TelegramUpdateDecision.Allowed;
    }

    private void Prune(DateTimeOffset now)
    {
        foreach (var item in _processedUpdates)
        {
            if (now - item.Value >= UpdateRetention)
            {
                _processedUpdates.TryRemove(item.Key, out _);
            }
        }

        foreach (var item in _chatWindows)
        {
            lock (item.Value)
            {
                if (item.Value.Count == 0 || now - item.Value.Last() >= RateWindow)
                {
                    _chatWindows.TryRemove(item.Key, out _);
                }
            }
        }
    }
}
