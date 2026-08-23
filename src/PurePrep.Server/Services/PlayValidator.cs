namespace PurePrep.Server.Services;

public sealed record PurchaseValidation(bool Valid, string OrderId, string ProductId);

/// <summary>
/// Validates a Google Play purchase token. The real implementation calls the Google Play Developer
/// API (androidpublisher) with a service account and must also acknowledge/consume the purchase.
/// That requires provisioned service-account credentials, so it is added once those are available.
/// </summary>
public interface IPlayValidator
{
    Task<PurchaseValidation> ValidateAsync(string productId, string purchaseToken, CancellationToken ct = default);
}

/// <summary>
/// Dev validator used until real Google credentials are wired. Treats any non-empty token as valid
/// so the redeem/credit flow can be tested locally. NEVER selected when running in Production.
/// </summary>
public sealed class DevPlayValidator : IPlayValidator
{
    public Task<PurchaseValidation> ValidateAsync(string productId, string purchaseToken, CancellationToken ct = default)
    {
        var valid = !string.IsNullOrWhiteSpace(purchaseToken) && !string.IsNullOrWhiteSpace(productId);
        return Task.FromResult(new PurchaseValidation(valid, $"DEV-{purchaseToken}", productId));
    }
}
