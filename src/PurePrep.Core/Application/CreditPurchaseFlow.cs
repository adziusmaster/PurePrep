namespace PurePrep.Application;

/// <summary>
/// Shared "buy a pack" flow: launch the platform purchase, grant the credits on the backend
/// <b>first</b>, then consume the purchase. Reused by the library paywall and the Settings
/// buy-credits popup so the grant-first ordering (which prevents a failed grant from losing a
/// purchase) lives in exactly one place.
/// </summary>
public static class CreditPurchaseFlow
{
    /// <summary>
    /// Buys <paramref name="productId"/> and returns the new credit balance, or <c>null</c> if the
    /// user cancelled. Throws if billing or the backend grant fails.
    /// </summary>
    public static async Task<int?> PurchaseAsync(
        IBillingService billing,
        ISmartCreditsClient credits,
        string productId,
        CancellationToken cancellationToken = default)
    {
        var purchase = await billing.BuyAsync(productId, cancellationToken);
        if (purchase is null)
            return null; // user cancelled

        var balance = await credits.RedeemAsync(purchase.ProductId, purchase.PurchaseToken, cancellationToken);

        // Credits are granted server-side: only now consume the purchase so Google marks it
        // fulfilled (and re-purchasable). A failed consume is non-fatal — reconciled on next buy.
        try
        {
            await billing.ConsumeAsync(purchase.PurchaseToken, cancellationToken);
        }
        catch
        {
            // Ignore: already redeemed on the backend; consume retries on the next purchase.
        }

        return balance;
    }
}
