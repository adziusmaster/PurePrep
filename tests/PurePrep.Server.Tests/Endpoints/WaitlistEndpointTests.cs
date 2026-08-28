using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PurePrep.Server.Tests.TestSupport;

namespace PurePrep.Server.Tests.Endpoints;

public sealed class WaitlistEndpointTests : IDisposable
{
    // A fresh app (and therefore a fresh rate-limit bucket + in-memory database) per test. The
    // waitlist limiter is deliberately tight — 8 requests per 5 minutes per origin — so a shared
    // fixture would let one test's requests trip the limiter for the next. Isolation keeps each
    // test exercising the endpoint's own behaviour rather than the limiter's.
    private readonly PurePrepAppFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Join_WithAFreshAddress_ShouldReportJoined()
    {
        var client = _factory.CreateClient();

        var res = await client.PostAsJsonAsync("/api/waitlist", new { email = $"cook-{Guid.NewGuid():N}@example.com" });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<WaitlistDto>();
        body!.Status.Should().Be("joined");
    }

    [Fact]
    public async Task Join_WithTheSameAddressTwice_ShouldStayJoinedAndNotError()
    {
        // Arrange — the email is the primary key, so a repeat submission must be a no-op success
        // rather than a 500 from a duplicate-key violation.
        var client = _factory.CreateClient();
        var email = $"repeat-{Guid.NewGuid():N}@example.com";

        // Act
        await client.PostAsJsonAsync("/api/waitlist", new { email });
        var second = await client.PostAsJsonAsync("/api/waitlist", new { email });

        // Assert
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.Content.ReadFromJsonAsync<WaitlistDto>())!.Status.Should().Be("joined");
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@domain")]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Join_WithAnInvalidAddress_ShouldReturnBadRequest(string email)
    {
        var client = _factory.CreateClient();

        var res = await client.PostAsJsonAsync("/api/waitlist", new { email });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Join_NormalizesCasingAndWhitespace_SoTheSameAddressIsNotStoredTwice()
    {
        var client = _factory.CreateClient();
        var token = Guid.NewGuid().ToString("N");

        var first = await client.PostAsJsonAsync("/api/waitlist", new { email = $"  Mixed-{token}@Example.COM " });
        var second = await client.PostAsJsonAsync("/api/waitlist", new { email = $"mixed-{token}@example.com" });

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.Content.ReadFromJsonAsync<WaitlistDto>())!.Status.Should().Be("joined");
    }

    private sealed record WaitlistDto(string Status);
}
