using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PurePrep.Server.Data;

namespace PurePrep.Server.Services;

public enum WaitlistOutcome
{
    Added,
    AlreadyJoined,
    Invalid,
}

public sealed record WaitlistResult(WaitlistOutcome Outcome);

public interface IWaitlistStore
{
    Task<WaitlistResult> JoinAsync(string email, string source, string? ipHash, bool consent, CancellationToken ct = default);

    /// <summary>Every signup, newest first. Admin-only: this is the raw list of registered addresses.</summary>
    Task<IReadOnlyList<WaitlistSignup>> ListAsync(CancellationToken ct = default);
}

public sealed partial class SqliteWaitlistStore(IDbContextFactory<ServerDbContext> factory) : IWaitlistStore
{
    // Deliberately permissive but shape-checking: one @, a dotted domain, no spaces. The goal is to
    // reject obvious junk (and defend the table), not to police every RFC 5321 edge case — the only
    // authority on whether an address works is whether the beta invite mail is delivered.
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailShape();

    private const int MaxEmailLength = 254; // RFC 5321 maximum address length.

    /// <summary>Trimmed, lower-cased address, or null when it is not a plausible email.</summary>
    public static string? Normalize(string? email)
    {
        var trimmed = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxEmailLength || !EmailShape().IsMatch(trimmed))
            return null;
        return trimmed;
    }

    public async Task<WaitlistResult> JoinAsync(string email, string source, string? ipHash, bool consent, CancellationToken ct = default)
    {
        var normalized = Normalize(email);
        if (normalized is null)
            return new WaitlistResult(WaitlistOutcome.Invalid);

        await using var db = await factory.CreateDbContextAsync(ct);

        var now = DateTimeOffset.UtcNow;
        db.WaitlistSignups.Add(new WaitlistSignup
        {
            Email = normalized,
            Source = string.IsNullOrWhiteSpace(source) ? "landing" : source,
            IpHash = ipHash,
            CreatedAt = now,
            // Consent is enforced at the endpoint, so a stored signup always carries the moment it
            // was given — the record GDPR needs to show the email was solicited, not scraped.
            ConsentedAt = consent ? now : null,
        });

        try
        {
            await db.SaveChangesAsync(ct);
            return new WaitlistResult(WaitlistOutcome.Added);
        }
        catch (DbUpdateException)
        {
            // Email is the primary key, so a repeat submission lands here. Treat it as success from
            // the caller's point of view: the address is on the list either way.
            return new WaitlistResult(WaitlistOutcome.AlreadyJoined);
        }
    }

    public async Task<IReadOnlyList<WaitlistSignup>> ListAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.WaitlistSignups
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);
    }
}
