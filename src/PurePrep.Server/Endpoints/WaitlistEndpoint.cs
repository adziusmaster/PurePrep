using PurePrep.Server.Services;

namespace PurePrep.Server.Endpoints;

/// <summary>
/// Public endpoint behind the landing-page waitlist form. It captures a single email address for
/// the open beta. It is intentionally minimal: no account, no confirmation loop, just a de-duplicated
/// address on a list. Abuse is contained by the rate limiter and by storing only a salted IP hash.
/// </summary>
public static class WaitlistEndpoint
{
    public static async Task<IResult> Join(
        WaitlistRequest request,
        HttpContext http,
        IWaitlistStore waitlist,
        IClientIpHasher ipHasher,
        CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Email))
            return Results.BadRequest(new { error = "An email address is required." });

        var ipHash = ipHasher.Hash(http.Connection.RemoteIpAddress?.ToString());
        var result = await waitlist.JoinAsync(request.Email, request.Source ?? "landing", ipHash, ct);

        return result.Outcome switch
        {
            // A fresh signup and a repeat both mean "you're on the list"; the client shows one message
            // either way, and not distinguishing them avoids leaking whether an address is enrolled.
            WaitlistOutcome.Added => Results.Ok(new WaitlistResponse("joined")),
            WaitlistOutcome.AlreadyJoined => Results.Ok(new WaitlistResponse("joined")),
            _ => Results.BadRequest(new { error = "That doesn't look like a valid email address." }),
        };
    }

    /// <summary>
    /// Admin-only: returns every waitlist signup, newest first. Gated by the same shared-secret filter
    /// as the promo admin endpoints, so an unauthenticated caller cannot enumerate registered addresses.
    /// The salted IP hash is deliberately not returned — it exists only for abuse containment.
    /// </summary>
    public static async Task<IResult> List(IWaitlistStore waitlist, CancellationToken ct)
    {
        var signups = await waitlist.ListAsync(ct);
        return Results.Ok(signups.Select(s => new WaitlistEntryResponse(s.Email, s.Source, s.CreatedAt)));
    }
}
