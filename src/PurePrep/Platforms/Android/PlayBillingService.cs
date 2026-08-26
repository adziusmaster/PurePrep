using Android.BillingClient.Api;
using Microsoft.Maui.ApplicationModel;
using PurePrep.Application;

namespace PurePrep.Platforms.Android;

/// <summary>
/// Real Google Play Billing integration (Play Billing Library, via the
/// <c>Xamarin.Android.Google.BillingClient</c> binding). Sells consumable Smart Credit packs.
///
/// Flow: connect ⇒ query the product ⇒ launch the purchase ⇒ receive the purchase via
/// <see cref="IPurchasesUpdatedListener"/>. The purchase is returned to the caller <b>un-consumed</b>;
/// the ViewModel grants the credits on the backend first and only then calls <see cref="ConsumeAsync"/>,
/// so a failed grant never loses a purchase (Google auto-refunds un-consumed purchases after a few days).
/// </summary>
public sealed class PlayBillingService : IBillingService
{
    public bool IsSupported => true;

    // Product ids must match the consumable in-app products configured in Play Console and the
    // backend's credit-pack map (credits_10/20/50/150). Prices are refreshed from Play at purchase time.
    public IReadOnlyList<CreditPack> Packs { get; } =
    [
        new CreditPack("credits_10", 10, "€0.99"),
        new CreditPack("credits_20", 20, "€1.79"),
        new CreditPack("credits_50", 50, "€3.49"),
        new CreditPack("credits_150", 150, "€7.49"),
    ];

    private readonly SemaphoreSlim _connectGate = new(1, 1);
    private BillingClient? _client;
    private TaskCompletionSource<PurchaseResult?>? _purchaseTcs;

    private async Task<BillingClient> EnsureConnectedAsync()
    {
        if (_client is { IsReady: true } ready)
            return ready;

        await _connectGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_client is { IsReady: true } current)
                return current;

            _client?.EndConnection();

            var pending = PendingPurchasesParams.NewBuilder()
                .EnableOneTimeProducts()
                .Build();

            var client = BillingClient.NewBuilder(global::Android.App.Application.Context!)
                .SetListener(new PurchasesUpdatedListener(OnPurchasesUpdated))
                .EnablePendingPurchases(pending)
                .Build();

            var result = await client.StartConnectionAsync().ConfigureAwait(false);
            if (result.ResponseCode != BillingResponseCode.Ok)
                throw new InvalidOperationException(
                    $"Google Play Billing is unavailable ({result.ResponseCode}): {result.DebugMessage}");

            _client = client;
            return client;
        }
        finally
        {
            _connectGate.Release();
        }
    }

    public async Task<PurchaseResult?> BuyAsync(string productId, CancellationToken cancellationToken = default)
    {
        var client = await EnsureConnectedAsync().ConfigureAwait(false);

        // Reuse an earlier purchase that was never consumed (e.g. a prior grant failed): buying again
        // would be rejected by Google Play with ITEM_ALREADY_OWNED.
        var owned = await FindOwnedPurchaseAsync(client, productId).ConfigureAwait(false);
        if (owned is not null)
            return owned;

        var details = await GetProductDetailsAsync(client, productId).ConfigureAwait(false);

        // New one-time-product model: launching requires the offer token from the product's
        // one-time purchase offer. (The legacy tokenless path only works for old-style SKUs.)
        var offerToken = details.OneTimePurchaseOfferDetailsList?.FirstOrDefault()?.OfferToken;

        var productParamsBuilder = BillingFlowParams.ProductDetailsParams.NewBuilder()
            .SetProductDetails(details);
        if (!string.IsNullOrEmpty(offerToken))
            productParamsBuilder.SetOfferToken(offerToken);

        var flowParams = BillingFlowParams.NewBuilder()
            .SetProductDetailsParamsList(new List<BillingFlowParams.ProductDetailsParams>
            {
                productParamsBuilder.Build(),
            })
            .Build();

        var tcs = new TaskCompletionSource<PurchaseResult?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _purchaseTcs = tcs;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var activity = Platform.CurrentActivity
                ?? throw new InvalidOperationException("No foreground activity is available to launch the purchase.");

            var launch = client.LaunchBillingFlow(activity, flowParams);
            if (launch.ResponseCode != BillingResponseCode.Ok)
            {
                _purchaseTcs = null;
                tcs.TrySetException(new InvalidOperationException(
                    $"Could not start the purchase ({launch.ResponseCode}): {launch.DebugMessage}"));
            }
        }).ConfigureAwait(false);

        using (cancellationToken.Register(() => tcs.TrySetResult(null)))
            return await tcs.Task.ConfigureAwait(false);
    }

    public async Task ConsumeAsync(string purchaseToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(purchaseToken))
            return;

        var client = await EnsureConnectedAsync().ConfigureAwait(false);
        var consumeParams = ConsumeParams.NewBuilder()
            .SetPurchaseToken(purchaseToken)
            .Build();

        await client.ConsumeAsync(consumeParams).ConfigureAwait(false);
    }

    private static async Task<ProductDetails> GetProductDetailsAsync(BillingClient client, string productId)
    {
        var product = QueryProductDetailsParams.Product.NewBuilder()
            .SetProductId(productId)
            .SetProductType(BillingClient.ProductType.Inapp)
            .Build();

        var query = QueryProductDetailsParams.NewBuilder()
            .SetProductList(new List<QueryProductDetailsParams.Product> { product })
            .Build();

        var result = await client.QueryProductDetailsAsync(query).ConfigureAwait(false);

        // The binding exposes both the (Java) ProductDetailsList and a synthesized ProductDetails
        // list; read whichever is populated.
        var fetched = result.ProductDetailsList?.FirstOrDefault()
                      ?? result.ProductDetails?.FirstOrDefault();
        if (fetched is not null)
            return fetched;

        // Nothing fetched: surface exactly why so the on-device message is actionable instead of a
        // generic "not available" (e.g. billing response code + Play's per-product status code).
        var reasons = new List<string>();
        if (result.Result is { } r)
            reasons.Add($"response {r.ResponseCode}: {r.DebugMessage}");
        if (result.UnfetchedProductList is { Count: > 0 } unfetched)
            reasons.Add("unfetched " + string.Join(", ",
                unfetched.Select(u => $"{u.ProductId} (status {u.StatusCodeValue})")));

        var detail = reasons.Count > 0 ? " — " + string.Join("; ", reasons) : string.Empty;
        throw new InvalidOperationException(
            $"Product '{productId}' is not available in Google Play yet{detail}. " +
            "It can take a little while after creating a product, and the app must be installed from a Play track.");
    }

    private static async Task<PurchaseResult?> FindOwnedPurchaseAsync(BillingClient client, string productId)
    {
        var query = QueryPurchasesParams.NewBuilder()
            .SetProductType(BillingClient.ProductType.Inapp)
            .Build();

        var result = await client.QueryPurchasesAsync(query).ConfigureAwait(false);
        var purchase = result.Purchases?.FirstOrDefault(p =>
            p.PurchaseState == PurchaseState.Purchased &&
            p.Products is not null && p.Products.Contains(productId));

        return purchase is null ? null : new PurchaseResult(productId, purchase.PurchaseToken!);
    }

    private void OnPurchasesUpdated(BillingResult result, IList<Purchase>? purchases)
    {
        var tcs = _purchaseTcs;
        if (tcs is null)
            return;
        _purchaseTcs = null;

        var code = result.ResponseCode;
        if (code == BillingResponseCode.UserCancelled)
        {
            tcs.TrySetResult(null);
            return;
        }

        if (code != BillingResponseCode.Ok || purchases is null)
        {
            tcs.TrySetException(new InvalidOperationException(
                $"The purchase did not complete ({code}): {result.DebugMessage}"));
            return;
        }

        var purchase = purchases.FirstOrDefault(p => p.PurchaseState == PurchaseState.Purchased);
        if (purchase is null)
        {
            // Purchase is still PENDING (e.g. cash / slow card). Nothing to grant yet.
            tcs.TrySetResult(null);
            return;
        }

        var productId = purchase.Products?.FirstOrDefault() ?? string.Empty;
        tcs.TrySetResult(new PurchaseResult(productId, purchase.PurchaseToken!));
    }

    private sealed class PurchasesUpdatedListener : Java.Lang.Object, IPurchasesUpdatedListener
    {
        private readonly Action<BillingResult, IList<Purchase>?> _callback;

        public PurchasesUpdatedListener(Action<BillingResult, IList<Purchase>?> callback) => _callback = callback;

        public void OnPurchasesUpdated(BillingResult billingResult, IList<Purchase>? purchases) =>
            _callback(billingResult, purchases);
    }
}
