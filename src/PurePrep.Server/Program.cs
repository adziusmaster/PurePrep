using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PurePrep.Ai;
using PurePrep.Server.Data;
using PurePrep.Server.Endpoints;
using PurePrep.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<CreditOptions>(builder.Configuration.GetSection(CreditOptions.SectionName));
builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection(GeminiOptions.SectionName));
builder.Services.Configure<PlayOptions>(builder.Configuration.GetSection(PlayOptions.SectionName));

var connectionString = builder.Configuration.GetConnectionString("Db")
    ?? "Data Source=/data/pureprep.server.db";
builder.Services.AddDbContextFactory<ServerDbContext>(o => o.UseSqlite(connectionString));

builder.Services.AddScoped<ICreditStore, SqliteCreditStore>();
builder.Services.AddScoped<IPromoStore, SqlitePromoStore>();
builder.Services.AddScoped<IWaitlistStore, SqliteWaitlistStore>();
builder.Services.AddSingleton<IUrlGuard, UrlGuard>();

// ---- Origin hashing -------------------------------------------------------------------------
// The seed cap needs to recognise a repeat origin without retaining IP addresses. A configured
// salt keeps the cap effective across restarts; without one it still works, but resets on deploy.
var ipSalt = builder.Configuration["Security:IpHashSalt"];
if (string.IsNullOrWhiteSpace(ipSalt))
{
    ipSalt = Guid.NewGuid().ToString("N");
    builder.Logging.AddConsole();
}
builder.Services.AddSingleton<IClientIpHasher>(new ClientIpHasher(ipSalt));

builder.Services.AddScoped<IFreeCreditPolicy>(sp => new SqliteFreeCreditPolicy(
    sp.GetRequiredService<IDbContextFactory<ServerDbContext>>(),
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CreditOptions>>().Value));

// ---- Outbound page fetch (SSRF-guarded) -----------------------------------------------------
// AllowAutoRedirect is off on purpose: GuardedPageFetcher walks the redirect chain itself and
// re-checks every hop, because automatic following is the standard way past an SSRF allow-list.
builder.Services.AddHttpClient<IPageFetcher, GuardedPageFetcher>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.MaxResponseContentBufferSize = 5 * 1024 * 1024; // 5 MB cap
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; PurePrepBot/1.0)");
    client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml");
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

// ---- Gemini ---------------------------------------------------------------------------------
var geminiKey = builder.Configuration[$"{GeminiOptions.SectionName}:ApiKey"];
if (!string.IsNullOrWhiteSpace(geminiKey))
    builder.Services.AddHttpClient<IGeminiClient, GeminiClient>(c =>
        c.BaseAddress = new Uri("https://generativelanguage.googleapis.com/"));
else
    builder.Services.AddSingleton<IGeminiClient, FakeGeminiClient>();

// ---- Purchase validation --------------------------------------------------------------------
// Production refuses to start without real Google credentials. The previous build registered the
// development validator unconditionally, which made any forged purchase token worth real credits.
var playOptions = builder.Configuration.GetSection(PlayOptions.SectionName).Get<PlayOptions>() ?? new PlayOptions();
var playChoice = PlayValidatorSelection.Select(builder.Environment.IsProduction(), playOptions);
if (playChoice == PlayValidatorChoice.GooglePlay)
{
    builder.Services.AddSingleton<IPlayPurchaseLookup, AndroidPublisherPurchaseLookup>();
    builder.Services.AddScoped<IPlayValidator, AndroidPublisherPlayValidator>();
}
else
{
    builder.Services.AddScoped<IPlayValidator, DevPlayValidator>();
}

// ---- Rate limiting --------------------------------------------------------------------------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Parsing costs an outbound fetch plus an LLM call, so it gets the tightest budget.
    options.AddPolicy(RateLimitPolicies.Parse, PerClient(limit: 12, window: TimeSpan.FromMinutes(1)));
    // Promo redemption is the brute-force target: five characters over a 32-symbol alphabet.
    options.AddPolicy(RateLimitPolicies.Promo, PerClient(limit: 5, window: TimeSpan.FromMinutes(5)));
    options.AddPolicy(RateLimitPolicies.Billing, PerClient(limit: 10, window: TimeSpan.FromMinutes(1)));
    options.AddPolicy(RateLimitPolicies.Credits, PerClient(limit: 30, window: TimeSpan.FromMinutes(1)));
    // Waitlist is an unauthenticated public write, so it gets a tight per-origin budget to blunt
    // scripted list-stuffing while leaving ample room for a human who mistypes their address.
    options.AddPolicy(RateLimitPolicies.Waitlist, PerClient(limit: 8, window: TimeSpan.FromMinutes(5)));

    static Func<HttpContext, RateLimitPartition<string>> PerClient(int limit, TimeSpan window) =>
        context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = limit, Window = window, QueueLimit = 0 });
});

builder.Services.AddHostedService<UsageLogRetentionService>();

var app = builder.Build();

// Caddy terminates TLS and proxies to this container. Without this the rate limiter and the seed
// cap would see the proxy's address for every request and behave as one global bucket.
var forwarded = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    ForwardLimit = 1,
};
forwarded.KnownNetworks.Clear();
forwarded.KnownProxies.Clear();
// The reverse proxy shares this container's Docker network; its address is not fixed, so private
// ranges are trusted for the forwarded header. The container is not otherwise publicly reachable.
forwarded.KnownNetworks.Add(new IPNetwork(System.Net.IPAddress.Parse("10.0.0.0"), 8));
forwarded.KnownNetworks.Add(new IPNetwork(System.Net.IPAddress.Parse("172.16.0.0"), 12));
forwarded.KnownNetworks.Add(new IPNetwork(System.Net.IPAddress.Parse("192.168.0.0"), 16));
app.UseForwardedHeaders(forwarded);

// The public landing page and its privacy policy are static files served straight from wwwroot on
// the marketing hostname (pureprep.lechdigital.nl), which Caddy routes to this same container. Doing
// so keeps the waitlist form same-origin with /api/waitlist, so no CORS handling is required.
app.UseDefaultFiles();
app.UseStaticFiles();
// Friendly extensionless URL for the privacy policy (…/privacy rather than …/privacy.html).
app.MapGet("/privacy", () => Results.File(
    Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "privacy.html"), "text/html; charset=utf-8"));

app.UseRateLimiter();

app.MapPost("/api/ai/parse", ParseEndpoint.Parse).RequireRateLimiting(RateLimitPolicies.Parse);
app.MapPost("/api/billing/redeem", BillingEndpoint.Redeem).RequireRateLimiting(RateLimitPolicies.Billing);

// Read-only balance. Kept for app builds already in the field; new clients call /ensure.
app.MapGet("/api/credits/{deviceId:guid}", CreditsEndpoint.GetBalance)
    .RequireRateLimiting(RateLimitPolicies.Credits);

// Seeds free credits on first contact, subject to the per-origin cap.
app.MapPost("/api/credits/ensure", CreditsEndpoint.Ensure)
    .RequireRateLimiting(RateLimitPolicies.Credits);

app.MapPost("/api/dev/grant", DevEndpoint.Grant).AddEndpointFilter(DevEndpoint.SecretFilter);

app.MapPost("/api/waitlist", WaitlistEndpoint.Join).RequireRateLimiting(RateLimitPolicies.Waitlist);
// Admin-only readout of registered addresses, behind the same shared-secret gate as the promo admin routes.
app.MapGet("/api/admin/waitlist", WaitlistEndpoint.List).AddEndpointFilter(PromoEndpoint.SecretFilter);

app.MapPost("/api/promo/redeem", PromoEndpoint.Redeem).RequireRateLimiting(RateLimitPolicies.Promo);app.MapPost("/api/admin/promo", PromoEndpoint.Create).AddEndpointFilter(PromoEndpoint.SecretFilter);
app.MapGet("/api/admin/promo", PromoEndpoint.List).AddEndpointFilter(PromoEndpoint.SecretFilter);
app.MapPost("/api/admin/promo/{code}/revoke", PromoEndpoint.Revoke).AddEndpointFilter(PromoEndpoint.SecretFilter);

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Build the Play client now rather than on the first purchase. A key file that exists but cannot
// be parsed would otherwise pass the startup check and then turn every purchase into a 500 — the
// same "looks fine, is broken" failure mode this whole change exists to remove.
if (playChoice == PlayValidatorChoice.GooglePlay)
{
    try
    {
        _ = app.Services.GetRequiredService<IPlayPurchaseLookup>();
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException(
            $"The Google Play service-account key at '{playOptions.ServiceAccountJsonPath}' could not be " +
            "loaded. Check it is the JSON key file downloaded from the Cloud console and is readable.", ex);
    }
}

await SchemaInitializer.InitializeAsync(app.Services.GetRequiredService<IDbContextFactory<ServerDbContext>>());

app.Run();

/// <summary>Named rate-limit policies, so endpoint registration cannot drift from the definitions.</summary>
public static class RateLimitPolicies
{
    public const string Parse = "parse";
    public const string Promo = "promo";
    public const string Billing = "billing";
    public const string Credits = "credits";
    public const string Waitlist = "waitlist";
}

/// <summary>Exposed so the integration tests can host the real application.</summary>
public partial class Program;
