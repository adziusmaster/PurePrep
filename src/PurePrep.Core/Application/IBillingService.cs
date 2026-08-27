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

    /// <summary>
    /// The credit packs offered for sale, with placeholder <see cref="CreditPack.DisplayPrice"/> labels.
    /// These are fallbacks only; prefer <see cref="GetPacksAsync"/> for the price actually shown to users.
    /// </summary>
    IReadOnlyList<CreditPack> Packs { get; }

    /// <summary>
    /// Returns the credit packs with prices resolved from the store — Google Play's localized,
    /// tax-inclusive <c>FormattedPrice</c>, i.e. the exact string the user is charged at checkout.
    /// VAT rates differ per country, so this is the only way the displayed price can match checkout
    /// everywhere. Falls back to the corresponding <see cref="Packs"/> label for any pack whose price
    /// can't be fetched (offline, billing unavailable, product not live yet).
    /// </summary>
    Task<IReadOnlyList<CreditPack>> GetPacksAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Launches the platform purchase flow for <paramref name="productId"/>. Returns the purchase on
    /// success, or <c>null</c> if the user cancelled. The purchase is intentionally left un-consumed so
    /// the caller can grant credits on the backend first; call <see cref="ConsumeAsync"/> only after the
    /// server has confirmed the grant.
    /// </summary>
    Task<PurchaseResult?> BuyAsync(string productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Consumes a purchased consumable so it can be bought again, and acknowledges it with Google Play.
    /// Must be called after the backend has granted the credits for the purchase; if it is never called,
    /// Google auto-refunds the purchase after a few days. No-op where billing is unsupported.
    /// </summary>
    Task ConsumeAsync(string purchaseToken, CancellationToken cancellationToken = default);
}
