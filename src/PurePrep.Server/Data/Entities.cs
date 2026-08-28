namespace PurePrep.Server.Data;

/// <summary>Credit balance for one anonymous device (identified by a client-generated GUID).</summary>
public sealed class DeviceCredit
{
    public Guid DeviceId { get; set; }
    public int Balance { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>A redeemed Google Play purchase. Stored to prevent a token being redeemed twice.</summary>
public sealed class ProcessedPurchase
{
    public string PurchaseToken { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public Guid DeviceId { get; set; }
    public int CreditsGranted { get; set; }
    public DateTimeOffset RedeemedAt { get; set; }
}

/// <summary>A promo/redemption code that grants smart credits. One code can be redeemed by many
/// devices, but each device may redeem a given code only once (see <see cref="PromoRedemption"/>).</summary>
public sealed class PromoCode
{
    /// <summary>Normalized (upper-case) code, e.g. "AB3KP".</summary>
    public string Code { get; set; } = string.Empty;
    public int Credits { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>Optional expiry; null means the code never expires.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }
    /// <summary>Set true to permanently disable the code regardless of expiry.</summary>
    public bool Revoked { get; set; }
}

/// <summary>Records that a specific device redeemed a specific code. Composite key enforces the
/// "one redemption per device per code" rule.</summary>
public sealed class PromoRedemption
{
    public string Code { get; set; } = string.Empty;
    public Guid DeviceId { get; set; }
    public int CreditsGranted { get; set; }
    public DateTimeOffset RedeemedAt { get; set; }
}

/// <summary>
/// Lightweight audit trail for observability and abuse detection.
///
/// Deliberately stores a salted <see cref="DeviceHash"/> rather than the device id: host alone is
/// innocuous, but joined to a stable device id it reconstructs which sites a person cooks from.
/// Rows are swept on a retention schedule (see <c>UsageLogRetention</c>).
/// </summary>
public sealed class UsageLog
{
    public long Id { get; set; }
    /// <summary>Salted, non-reversible token for the device. Not the device id.</summary>
    public string DeviceHash { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public bool Success { get; set; }
    public DateTimeOffset At { get; set; }
}

/// <summary>
/// Records that a device was seeded with free credits, and from which origin. The origin is stored
/// as a salted hash, never a raw IP, so the table can enforce the per-origin cap without becoming a
/// log of which addresses used the app.
/// </summary>
public sealed class DeviceSeed
{
    public Guid DeviceId { get; set; }
    /// <summary>Salted hash of the client IP, or null when the origin could not be determined.</summary>
    public string? IpHash { get; set; }
    public DateTimeOffset SeededAt { get; set; }
}

/// <summary>
/// One email address on the open-beta waitlist, captured from the public landing page. The
/// normalized address is the primary key, so a second submission of the same address is a no-op
/// rather than a duplicate row. The salted <see cref="IpHash"/> supports light abuse triage without
/// retaining raw IP addresses, mirroring the rest of the schema.
/// </summary>
public sealed class WaitlistSignup
{
    /// <summary>Trimmed, lower-cased email address. Primary key.</summary>
    public string Email { get; set; } = string.Empty;
    /// <summary>Where the signup came from, e.g. "landing". Free-form, for future segmentation.</summary>
    public string Source { get; set; } = string.Empty;
    /// <summary>Salted, non-reversible hash of the client IP, or null when the origin is unknown.</summary>
    public string? IpHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>
    /// When the person ticked the "email me about testing/launch" consent box. GDPR requires consent
    /// to be demonstrable, so we record the moment it was given rather than a bare boolean.
    /// </summary>
    public DateTimeOffset? ConsentedAt { get; set; }
}
