using System.Net.Http.Json;
using PurePrep.Application;

namespace PurePrep.Infrastructure;

/// <summary>
/// HTTP client for the backend credit endpoints. Reads the device's balance and redeems purchased
/// credit packs. Uses <see cref="IDeviceIdentity"/> so the device id never leaks into the ViewModel.
/// </summary>
public sealed class HttpSmartCreditsClient(HttpClient http, IDeviceIdentity identity) : ISmartCreditsClient
{
    public async Task<int> GetBalanceAsync(CancellationToken cancellationToken = default)
    {
        var deviceId = await identity.GetDeviceIdAsync(cancellationToken);
        var dto = await http.GetFromJsonAsync<BalanceResponse>($"api/credits/{deviceId}", cancellationToken);
        return dto?.Balance ?? 0;
    }

    public async Task<int> RedeemAsync(string productId, string purchaseToken, CancellationToken cancellationToken = default)
    {
        var deviceId = await identity.GetDeviceIdAsync(cancellationToken);

        using var response = await http.PostAsJsonAsync(
            "api/billing/redeem",
            new { deviceId, productId, purchaseToken },
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<RedeemResponse>(cancellationToken);
        return dto?.Balance ?? 0;
    }

    private sealed record BalanceResponse(int Balance);

    private sealed record RedeemResponse(int CreditsGranted, int Balance);
}
