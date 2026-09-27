using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace PurePrep.Server.Services;

public interface IImageTicketStore
{
    string Issue(Guid deviceId);
    bool TryRedeem(Guid deviceId, string ticket);
}

/// <summary>
/// One-time permission to generate a recipe photo, handed out by a successful parse. It keeps the
/// "included in the import credit" image endpoint from becoming a free image generator: each ticket
/// works once, for one device, for ten minutes. In memory by design — a restart only loses pending
/// images, which fall back to a placeholder.
/// </summary>
internal sealed class ImageTicketStore(TimeProvider clock) : IImageTicketStore
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<string, (Guid Device, DateTimeOffset ExpiresAt)> _tickets = new();

    public string Issue(Guid deviceId)
    {
        Sweep();
        var ticket = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        _tickets[ticket] = (deviceId, clock.GetUtcNow().Add(Lifetime));
        return ticket;
    }

    public bool TryRedeem(Guid deviceId, string ticket) =>
        !string.IsNullOrWhiteSpace(ticket)
        && _tickets.TryRemove(ticket, out var entry)
        && entry.Device == deviceId
        && clock.GetUtcNow() <= entry.ExpiresAt;

    private void Sweep()
    {
        var now = clock.GetUtcNow();
        foreach (var (key, value) in _tickets)
            if (value.ExpiresAt < now) _tickets.TryRemove(key, out _);
    }
}
