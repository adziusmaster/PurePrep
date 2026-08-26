using System.Net;
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

    public async Task<PromoRedeemResult> RedeemCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
        var deviceId = await identity.GetDeviceIdAsync(cancellationToken);

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync(
                "api/promo/redeem",
                new { deviceId, code = normalized },
                cancellationToken);
        }
        catch (HttpRequestException)
        {
            return new PromoRedeemResult(PromoRedeemOutcome.NetworkError, 0, 0);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new PromoRedeemResult(PromoRedeemOutcome.NetworkError, 0, 0);
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                var dto = await response.Content.ReadFromJsonAsync<RedeemResponse>(cancellationToken);
                return new PromoRedeemResult(PromoRedeemOutcome.Success, dto?.CreditsGranted ?? 0, dto?.Balance ?? 0);
            }

            var error = await ReadErrorAsync(response, cancellationToken);
            var outcome = response.StatusCode switch
            {
                HttpStatusCode.NotFound => PromoRedeemOutcome.InvalidCode,
                HttpStatusCode.Conflict => PromoRedeemOutcome.AlreadyRedeemed,
                HttpStatusCode.BadRequest when error == "revoked_code" => PromoRedeemOutcome.Revoked,
                HttpStatusCode.BadRequest when error == "expired_code" => PromoRedeemOutcome.Expired,
                HttpStatusCode.BadRequest => PromoRedeemOutcome.InvalidCode,
                _ => PromoRedeemOutcome.NetworkError,
            };
            return new PromoRedeemResult(outcome, 0, 0);
        }
    }

    private static async Task<string?> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var dto = await response.Content.ReadFromJsonAsync<ErrorResponse>(ct);
            return dto?.Error;
        }
        catch
        {
            return null;
        }
    }

    private sealed record BalanceResponse(int Balance);

    private sealed record RedeemResponse(int CreditsGranted, int Balance);

    private sealed record ErrorResponse(string? Error);
}
