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

/// <summary>Lightweight audit trail for observability and abuse detection (no full URLs / PII).</summary>
public sealed class UsageLog
{
    public long Id { get; set; }
    public Guid DeviceId { get; set; }
    public string Host { get; set; } = string.Empty;
    public bool Success { get; set; }
    public DateTimeOffset At { get; set; }
}
