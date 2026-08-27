using Google.Apis.AndroidPublisher.v3;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Microsoft.Extensions.Options;

namespace PurePrep.Server.Services;

/// <summary>The fields of a Google Play product purchase this app makes decisions on.</summary>
public sealed record PlayPurchase(int PurchaseState, int ConsumptionState, string? OrderId);

/// <summary>
/// Looks a purchase token up with Google. Split from the validator so the rules about which
/// purchase states earn credits can be tested without the Google SDK or a network.
/// </summary>
public interface IPlayPurchaseLookup
{
    /// <summary>The purchase Google has on record, or <c>null</c> when it knows nothing about the token.</summary>
    Task<PlayPurchase?> GetProductPurchaseAsync(string productId, string purchaseToken, CancellationToken ct = default);
}

/// <summary>
/// Validates a purchase against the Google Play Developer API. Credits are granted only for a
/// purchase Google confirms is <b>Purchased</b>, <b>not yet consumed</b>, and carries an order id
/// (which <c>ProcessedPurchase</c>'s unique index then uses for replay protection).
/// </summary>
public sealed class AndroidPublisherPlayValidator(
    IPlayPurchaseLookup lookup,
    ILogger<AndroidPublisherPlayValidator>? logger = null) : IPlayValidator
{
    private const int Purchased = 0;
    private const int NotConsumed = 0;

    public async Task<PurchaseValidation> ValidateAsync(string productId, string purchaseToken, CancellationToken ct = default)
    {
        PlayPurchase? purchase;
        try
        {
            purchase = await lookup.GetProductPurchaseAsync(productId, purchaseToken, ct);
        }
        catch (Exception ex)
        {
            // Fail closed. An outage must never become a way to mint credits, and the caller
            // turns this into "could not be validated" rather than a 500.
            logger?.LogError(ex, "Play validation failed for product {ProductId}.", productId);
            return Invalid(productId);
        }

        if (purchase is null)
            return Invalid(productId);

        var acceptable = purchase.PurchaseState == Purchased
            && purchase.ConsumptionState == NotConsumed
            && !string.IsNullOrWhiteSpace(purchase.OrderId);

        return acceptable
            ? new PurchaseValidation(true, purchase.OrderId!, productId)
            : Invalid(productId);
    }

    private static PurchaseValidation Invalid(string productId) => new(false, string.Empty, productId);
}

/// <summary>Thin adapter over the Google Play Developer API (androidpublisher v3).</summary>
public sealed class AndroidPublisherPurchaseLookup : IPlayPurchaseLookup, IDisposable
{
    private readonly AndroidPublisherService _service;
    private readonly string _packageName;

    public AndroidPublisherPurchaseLookup(IOptions<PlayOptions> options)
    {
        var settings = options.Value;
        _packageName = settings.PackageName;

        var credential = GoogleCredential
            .FromFile(settings.ServiceAccountJsonPath!)
            .CreateScoped(AndroidPublisherService.Scope.Androidpublisher);

        _service = new AndroidPublisherService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "PurePrep",
        });
    }

    public async Task<PlayPurchase?> GetProductPurchaseAsync(string productId, string purchaseToken, CancellationToken ct = default)
    {
        try
        {
            var purchase = await _service.Purchases.Products
                .Get(_packageName, productId, purchaseToken)
                .ExecuteAsync(ct);

            return new PlayPurchase(
                purchase.PurchaseState ?? -1,
                purchase.ConsumptionState ?? -1,
                purchase.OrderId);
        }
        catch (Google.GoogleApiException ex) when (
            ex.HttpStatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.BadRequest)
        {
            // Google knows this app but not this token. 404 means no such purchase; 400 ("Invalid
            // Value") means the token is not even well-formed. Both mean the same thing here — the
            // purchase is not real — and neither is a server fault, so they are not logged as one.
            // Anything else (401/403 permissions, 5xx, network) propagates and IS logged.
            return null;
        }
    }

    public void Dispose() => _service.Dispose();
}
