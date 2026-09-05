using System.Collections.Concurrent;

namespace PurePrep.Ai;

/// <summary>
/// Remembers which hosts have refused our honest <c>PurePrepBot</c> so a repeat import of the same
/// site skips straight to the browser fallback instead of paying a guaranteed-to-fail first fetch.
/// </summary>
public interface IFetchHostMemory
{
    /// <summary>True if <paramref name="host"/> recently walled the honest bot (within the TTL).</summary>
    bool IsBlocked(string host);

    /// <summary>Record that <paramref name="host"/> refused the honest bot.</summary>
    void MarkBlocked(string host);
}

/// <summary>
/// Process-local, thread-safe implementation with a sliding TTL. A cache miss simply means one
/// extra honest attempt — never a correctness problem — so an in-memory store is entirely adequate
/// and needs no external dependency.
/// </summary>
public sealed class InMemoryFetchHostMemory(TimeSpan ttl, Func<DateTimeOffset>? clock = null) : IFetchHostMemory
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _blockedUntil = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<DateTimeOffset> _now = clock ?? (() => DateTimeOffset.UtcNow);

    public InMemoryFetchHostMemory() : this(TimeSpan.FromHours(6)) { }

    public bool IsBlocked(string host)
    {
        if (string.IsNullOrEmpty(host) || !_blockedUntil.TryGetValue(host, out var until))
            return false;

        if (_now() < until)
            return true;

        _blockedUntil.TryRemove(host, out _);
        return false;
    }

    public void MarkBlocked(string host)
    {
        if (!string.IsNullOrEmpty(host))
            _blockedUntil[host] = _now() + ttl;
    }
}
