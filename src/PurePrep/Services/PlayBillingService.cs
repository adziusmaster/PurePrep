using PurePrep.Application;

namespace PurePrep.Services;

/// <summary>
/// Placeholder in-app billing service. The real Google Play Billing integration (native Billing
/// Library / a MAUI billing plugin) is added in the Play-signed build once a Play Console app and
/// consumable products exist. Until then <see cref="IsSupported"/> is <c>false</c> and the paywall
/// explains that Smart Credit packs are purchasable in the published app.
///
/// The pack list mirrors the backend's configured products (credits_10/20/50/150). Prices shown here
/// are placeholders; the store returns the real localized prices once billing is live.
/// </summary>
public sealed class PlayBillingService : IBillingService
{
    public bool IsSupported => false;

    public IReadOnlyList<CreditPack> Packs { get; } =
    [
        new CreditPack("credits_10", 10, "€1.99"),
        new CreditPack("credits_20", 20, "€3.49"),
        new CreditPack("credits_50", 50, "€6.99"),
        new CreditPack("credits_150", 150, "€14.99"),
    ];

    public Task<PurchaseResult?> BuyAsync(string productId, CancellationToken cancellationToken = default)
        => Task.FromResult<PurchaseResult?>(null);
}
