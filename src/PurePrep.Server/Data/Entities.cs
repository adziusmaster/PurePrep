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

/// <summary>Lightweight audit trail for observability and abuse detection (no full URLs / PII).</summary>
public sealed class UsageLog
{
    public long Id { get; set; }
    public Guid DeviceId { get; set; }
    public string Host { get; set; } = string.Empty;
    public bool Success { get; set; }
    public DateTimeOffset At { get; set; }
}
