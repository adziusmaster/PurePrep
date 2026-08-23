namespace PurePrep.Application;

/// <summary>
/// Talks to the backend's credit endpoints on behalf of the current device. The server is the single
/// source of truth for a device's Smart Credit balance.
/// </summary>
public interface ISmartCreditsClient
{
    /// <summary>Returns the current device's Smart Credit balance (seeding free credits on first use).</summary>
    Task<int> GetBalanceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Redeems a validated Google Play purchase for a credit pack. Returns the new balance.
    /// </summary>
    Task<int> RedeemAsync(string productId, string purchaseToken, CancellationToken cancellationToken = default);
}
