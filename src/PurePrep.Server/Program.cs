using Microsoft.EntityFrameworkCore;
using PurePrep.Ai;
using PurePrep.Server.Endpoints;
using PurePrep.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<CreditOptions>(builder.Configuration.GetSection(CreditOptions.SectionName));
builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection(GeminiOptions.SectionName));

var connectionString = builder.Configuration.GetConnectionString("Db")
    ?? "Data Source=/data/pureprep.server.db";
builder.Services.AddDbContextFactory<PurePrep.Server.Data.ServerDbContext>(o => o.UseSqlite(connectionString));

builder.Services.AddScoped<ICreditStore, SqliteCreditStore>();
builder.Services.AddScoped<IPromoStore, SqlitePromoStore>();
builder.Services.AddSingleton<IUrlGuard, UrlGuard>();

// Outbound fetch client with browser-like headers + hard limits (defence in depth for SSRF/DoS).
builder.Services.AddHttpClient("fetch", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.MaxResponseContentBufferSize = 5 * 1024 * 1024; // 5 MB cap
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; PurePrepBot/1.0)");
    client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml");
});

// Gemini: real client when a key is configured, deterministic fake otherwise (local/dev).
var geminiKey = builder.Configuration[$"{GeminiOptions.SectionName}:ApiKey"];
if (!string.IsNullOrWhiteSpace(geminiKey))
    builder.Services.AddHttpClient<IGeminiClient, GeminiClient>(c =>
        c.BaseAddress = new Uri("https://generativelanguage.googleapis.com/"));
else
    builder.Services.AddSingleton<IGeminiClient, FakeGeminiClient>();

// Play validation: real validator in Production (added once service-account creds exist),
// dev validator otherwise so the redeem flow is testable locally.
builder.Services.AddScoped<IPlayValidator, DevPlayValidator>();

var app = builder.Build();

app.MapPost("/api/ai/parse", ParseEndpoint.Parse);
app.MapPost("/api/billing/redeem", BillingEndpoint.Redeem);
app.MapGet("/api/credits/{deviceId:guid}", async (Guid deviceId, ICreditStore credits,
    Microsoft.Extensions.Options.IOptions<CreditOptions> creditOptions, CancellationToken ct) =>
    Results.Ok(new { balance = await credits.EnsureDeviceAsync(deviceId, creditOptions.Value.FreeCredits, ct) }));

app.MapPost("/api/dev/grant", DevEndpoint.Grant).AddEndpointFilter(DevEndpoint.SecretFilter);

app.MapPost("/api/promo/redeem", PromoEndpoint.Redeem);
app.MapPost("/api/admin/promo", PromoEndpoint.Create).AddEndpointFilter(PromoEndpoint.SecretFilter);
app.MapGet("/api/admin/promo", PromoEndpoint.List).AddEndpointFilter(PromoEndpoint.SecretFilter);
app.MapPost("/api/admin/promo/{code}/revoke", PromoEndpoint.Revoke).AddEndpointFilter(PromoEndpoint.SecretFilter);

// Ensure the schema is present. EnsureCreated builds every table for a brand-new database, but it
// does NOT add newly-introduced tables to a database that already exists (no migrations are used).
// So we also create any recently-added tables explicitly and idempotently on startup.
await using (var db = await app.Services
    .GetRequiredService<IDbContextFactory<PurePrep.Server.Data.ServerDbContext>>()
    .CreateDbContextAsync())
{
    await db.Database.EnsureCreatedAsync();
    await db.Database.ExecuteSqlRawAsync(
        """
        CREATE TABLE IF NOT EXISTS "PromoCodes" (
            "Code" TEXT NOT NULL CONSTRAINT "PK_PromoCodes" PRIMARY KEY,
            "Credits" INTEGER NOT NULL,
            "CreatedAt" TEXT NOT NULL,
            "ExpiresAt" TEXT NULL,
            "Revoked" INTEGER NOT NULL
        );
        """);
    await db.Database.ExecuteSqlRawAsync(
        """
        CREATE TABLE IF NOT EXISTS "PromoRedemptions" (
            "Code" TEXT NOT NULL,
            "DeviceId" TEXT NOT NULL,
            "CreditsGranted" INTEGER NOT NULL,
            "RedeemedAt" TEXT NOT NULL,
            CONSTRAINT "PK_PromoRedemptions" PRIMARY KEY ("Code", "DeviceId")
        );
        """);
}

app.Run();
