using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using PurePrep.Server.Tests.TestSupport;

namespace PurePrep.Server.Tests.Endpoints;

public sealed class WaitlistEndpointTests : IDisposable
{
    // A fresh app (and therefore a fresh rate-limit bucket + in-memory database) per test. The
    // waitlist limiter is deliberately tight — 8 requests per 5 minutes per origin — so a shared
    // fixture would let one test's requests trip the limiter for the next. Isolation keeps each
    // test exercising the endpoint's own behaviour rather than the limiter's.
    private readonly PurePrepAppFactory _factory = new();
    private readonly List<WebApplicationFactory<Program>> _derived = new();

    public void Dispose()
    {
        foreach (var derived in _derived)
            derived.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Join_WithAFreshAddress_ShouldReportJoined()
    {
        var client = _factory.CreateClient();

        var res = await client.PostAsJsonAsync("/api/waitlist", new { email = $"cook-{Guid.NewGuid():N}@example.com", consent = true });

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
        await client.PostAsJsonAsync("/api/waitlist", new { email, consent = true });
        var second = await client.PostAsJsonAsync("/api/waitlist", new { email, consent = true });

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

        var res = await client.PostAsJsonAsync("/api/waitlist", new { email, consent = true });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Join_WithoutConsent_ShouldBeRejected()
    {
        // Arrange — no ticked box means no lawful basis to email, so the address must not be stored.
        var client = _factory.CreateClient();

        var res = await client.PostAsJsonAsync("/api/waitlist",
            new { email = $"noconsent-{Guid.NewGuid():N}@example.com", consent = false });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Join_NormalizesCasingAndWhitespace_SoTheSameAddressIsNotStoredTwice()
    {
        var client = _factory.CreateClient();
        var token = Guid.NewGuid().ToString("N");

        var first = await client.PostAsJsonAsync("/api/waitlist", new { email = $"  Mixed-{token}@Example.COM ", consent = true });
        var second = await client.PostAsJsonAsync("/api/waitlist", new { email = $"mixed-{token}@example.com", consent = true });

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.Content.ReadFromJsonAsync<WaitlistDto>())!.Status.Should().Be("joined");
    }

    [Fact]
    public async Task List_WithoutTheAdminSecret_ShouldNotRevealSignups()
    {
        // Arrange — the readout must not be enumerable by an unauthenticated caller. A missing/wrong
        // secret returns 404 (not 401), matching the promo admin routes, so the endpoint's existence
        // is not even confirmed.
        var factory = FactoryWithAdminSecret("top-secret");
        var client = factory.CreateClient();

        var noSecret = await client.GetAsync("/api/admin/waitlist");
        client.DefaultRequestHeaders.Add("X-Admin-Secret", "wrong");
        var wrongSecret = await client.GetAsync("/api/admin/waitlist");

        noSecret.StatusCode.Should().Be(HttpStatusCode.NotFound);
        wrongSecret.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_WithTheAdminSecret_ShouldReturnRegisteredAddressesNewestFirst()
    {
        // Arrange
        var factory = FactoryWithAdminSecret("top-secret");
        var joiner = factory.CreateClient();
        var older = $"older-{Guid.NewGuid():N}@example.com";
        var newer = $"newer-{Guid.NewGuid():N}@example.com";
        await joiner.PostAsJsonAsync("/api/waitlist", new { email = older, consent = true });
        await joiner.PostAsJsonAsync("/api/waitlist", new { email = newer, consent = true });

        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Add("X-Admin-Secret", "top-secret");

        // Act
        var res = await admin.GetAsync("/api/admin/waitlist");

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var entries = await res.Content.ReadFromJsonAsync<List<WaitlistEntryDto>>();
        entries!.Select(e => e.Email).Should().ContainInOrder(newer, older);
        entries.Should().OnlyContain(e => e.Source == "landing");
        // Consent must be captured for GDPR provability, not just accepted client-side.
        entries.Should().OnlyContain(e => e.ConsentedAt != null);
    }

    private WebApplicationFactory<Program> FactoryWithAdminSecret(string secret)
    {
        var configured = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration(config =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["Admin:Secret"] = secret })));
        _derived.Add(configured);
        return configured;
    }

    private sealed record WaitlistDto(string Status);
    private sealed record WaitlistEntryDto(string Email, string Source, DateTimeOffset CreatedAt, DateTimeOffset? ConsentedAt);
}
