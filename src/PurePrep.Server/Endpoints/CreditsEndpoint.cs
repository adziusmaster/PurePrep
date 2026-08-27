using PurePrep.Server.Services;

namespace PurePrep.Server.Endpoints;

public static class CreditsEndpoint
{
    /// <summary>
    /// Compatibility path for app builds already in the field, which read their balance with a GET
    /// and rely on it seeding the free allowance on first contact. Making it read-only would leave
    /// anyone who installs an older APK on zero credits forever, so it still seeds — but now through
    /// the same per-origin cap and rate limit as everything else, which is what made the original
    /// "unauthenticated GET writes to the database" behaviour abusable.
    ///
    /// New clients call <see cref="Ensure"/>. Once the old builds are gone this can drop the seeding.
    /// </summary>
    public static async Task<IResult> GetBalance(
        Guid deviceId,
        HttpContext http,
        ICreditStore credits,
        IFreeCreditPolicy freeCredits,
        IClientIpHasher ipHasher,
        CancellationToken ct)
    {
        if (deviceId == Guid.Empty)
            return Results.BadRequest(new { error = "A valid deviceId is required." });

        var balance = await EnsureSeededAsync(deviceId, http, credits, freeCredits, ipHasher, ct);
        return Results.Ok(new { balance });
    }

    /// <summary>
    /// Ensures the device exists and seeds its free credits on first contact, subject to the
    /// per-origin cap. Returns the current balance either way.
    /// </summary>
    public static async Task<IResult> Ensure(
        EnsureCreditsRequest request,
        HttpContext http,
        ICreditStore credits,
        IFreeCreditPolicy freeCredits,
        IClientIpHasher ipHasher,
        CancellationToken ct)
    {
        if (request.DeviceId == Guid.Empty)
            return Results.BadRequest(new { error = "A valid deviceId is required." });

        var balance = await EnsureSeededAsync(request.DeviceId, http, credits, freeCredits, ipHasher, ct);
        return Results.Ok(new { balance });
    }

    /// <summary>
    /// Shared seeding path. Lives here so the parse endpoint and the ensure endpoint cannot drift
    /// on how the free allowance is decided.
    /// </summary>
    internal static async Task<int> EnsureSeededAsync(
        Guid deviceId,
        HttpContext http,
        ICreditStore credits,
        IFreeCreditPolicy freeCredits,
        IClientIpHasher ipHasher,
        CancellationToken ct)
    {
        var ipHash = ipHasher.Hash(http.Connection.RemoteIpAddress?.ToString());
        var allowance = await freeCredits.ResolveAsync(deviceId, ipHash, ct);
        return await credits.EnsureDeviceAsync(deviceId, allowance, ct);
    }
}

public sealed record EnsureCreditsRequest(Guid DeviceId);
