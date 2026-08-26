using PurePrep.Application;

namespace PurePrep.Services;

/// <summary>
/// Fallback in-app billing service for platforms where Google Play Billing is unavailable
/// (iOS, Mac Catalyst, the web preview). <see cref="IsSupported"/> is <c>false</c> and the paywall
/// explains that Smart Credit packs are purchasable in the published Android app. The real Google Play
/// Billing integration lives in <c>Platforms/Android/PlayBillingService.cs</c>.
///
/// The pack list mirrors the backend's configured products (credits_10/20/50/150). Prices shown here
/// are placeholders; the Android build returns the real localized prices once billing is live.
/// </summary>
public sealed class UnsupportedBillingService : IBillingService
{
    public bool IsSupported => false;

    public IReadOnlyList<CreditPack> Packs { get; } =
    [
        new CreditPack("credits_10", 10, "€0.99"),
        new CreditPack("credits_20", 20, "€1.79"),
        new CreditPack("credits_50", 50, "€3.49"),
        new CreditPack("credits_150", 150, "€7.49"),
    ];

    public Task<PurchaseResult?> BuyAsync(string productId, CancellationToken cancellationToken = default)
        => Task.FromResult<PurchaseResult?>(null);

    public Task ConsumeAsync(string purchaseToken, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
