using System.Net;

namespace PurePrep.Core.Tests.TestSupport;

/// <summary>
/// Test double for <see cref="HttpMessageHandler"/> that replays a queued script of responses and
/// records the URIs it was asked for, so redirect-following can be asserted without a network.
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _script = new();

    public List<Uri> RequestedUris { get; } = new();

    /// <summary>Snapshot of the identity headers each request went out with, for asserting the honest→browser escalation.</summary>
    public List<SentRequest> SentRequests { get; } = new();

    public sealed record SentRequest(Uri Uri, string? UserAgent, string? From, string? Purpose, string? Info);

    public StubHttpMessageHandler Respond(HttpStatusCode status, string body = "")
    {
        _script.Enqueue(_ => new HttpResponseMessage(status) { Content = new StringContent(body) });
        return this;
    }

    public StubHttpMessageHandler RedirectTo(string location, HttpStatusCode status = HttpStatusCode.Found)
    {
        _script.Enqueue(_ =>
        {
            var response = new HttpResponseMessage(status);
            response.Headers.Location = new Uri(location);
            return response;
        });
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestedUris.Add(request.RequestUri!);
        var ua = request.Headers.UserAgent.ToString();
        SentRequests.Add(new SentRequest(
            request.RequestUri!,
            string.IsNullOrEmpty(ua) ? null : ua,
            request.Headers.From,
            request.Headers.TryGetValues("X-PurePrep-Purpose", out var purpose) ? string.Join(",", purpose) : null,
            request.Headers.TryGetValues("X-PurePrep-Info", out var info) ? string.Join(",", info) : null));

        if (_script.Count == 0)
            throw new InvalidOperationException($"No scripted response for {request.RequestUri}.");
        return Task.FromResult(_script.Dequeue()(request));
    }
}
