using PurePrep.Server.Services;

namespace PurePrep.Server.Endpoints;

/// <summary>
/// Dev-only endpoint to grant credits without real billing, for local/preview testing.
/// Protected by a shared secret header and disabled unless a secret is configured.
/// </summary>
public static class DevEndpoint
{
    public const string SecretHeader = "X-Dev-Secret";

    public static async Task<IResult> Grant(GrantRequest request, ICreditStore credits, CancellationToken ct)
    {
        if (request.DeviceId == Guid.Empty || request.Amount <= 0)
            return Results.BadRequest(new { error = "deviceId and a positive amount are required." });

        var balance = await credits.GrantAsync(request.DeviceId, request.Amount, ct);
        return Results.Ok(new { balance });
    }

    public static async ValueTask<object?> SecretFilter(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var configured = context.HttpContext.RequestServices
            .GetRequiredService<IConfiguration>()["Dev:Secret"];
        var provided = context.HttpContext.Request.Headers[SecretHeader].ToString();

        if (string.IsNullOrEmpty(configured) || !string.Equals(configured, provided, StringComparison.Ordinal))
            return Results.NotFound();

        return await next(context);
    }
}
