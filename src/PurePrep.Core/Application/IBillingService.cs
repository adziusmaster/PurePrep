namespace PurePrep.Application;

/// <summary>A purchasable consumable Smart Credit pack, mapped to a Google Play product id.</summary>
public sealed record CreditPack(string ProductId, int Credits, string DisplayPrice);

/// <summary>The result of a successful Google Play purchase, forwarded to the backend for redemption.</summary>
public sealed record PurchaseResult(string ProductId, string PurchaseToken);

/// <summary>
/// Abstracts the platform in-app billing flow (Google Play Billing on Android). Kept behind an
/// interface so the ViewModel and backend redemption logic stay platform-agnostic and testable.
/// </summary>
public interface IBillingService
{
    /// <summary>True when in-app billing is available on this platform/build.</summary>
    bool IsSupported { get; }

    /// <summary>The credit packs offered for sale.</summary>
    IReadOnlyList<CreditPack> Packs { get; }

    /// <summary>
    /// Launches the platform purchase flow for <paramref name="productId"/>. Returns the purchase on
    /// success, or <c>null</c> if the user cancelled.
    /// </summary>
    Task<PurchaseResult?> BuyAsync(string productId, CancellationToken cancellationToken = default);
}
