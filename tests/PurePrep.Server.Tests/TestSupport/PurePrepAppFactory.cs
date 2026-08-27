using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PurePrep.Ai;
using PurePrep.Server.Data;
using PurePrep.Server.Services;

namespace PurePrep.Server.Tests.TestSupport;

/// <summary>
/// Hosts the real application over an in-memory database, with only the two genuinely external
/// dependencies substituted: Google Play and Gemini. Everything else — routing, rate limiting,
/// the credit ledger, the seed cap — is the production wiring.
/// </summary>
public sealed class PurePrepAppFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public IPlayPurchaseLookup PlayLookup { get; } = Substitute.For<IPlayPurchaseLookup>();
    public IGeminiClient Gemini { get; } = Substitute.For<IGeminiClient>();
    public IPageFetcher PageFetcher { get; } = Substitute.For<IPageFetcher>();

    /// <summary>Server-side log lines captured during a test, so failures can be diagnosed.</summary>
    public List<string> LogLines { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureLogging(logging =>
            logging.AddProvider(new CapturingLoggerProvider(LogLines)));

        builder.ConfigureServices(services =>
        {
            _connection.Open();

            services.RemoveAll(typeof(IDbContextFactory<ServerDbContext>));
            services.AddDbContextFactory<ServerDbContext>(o => o.UseSqlite(_connection));

            // Use the real validator with a substituted Google lookup, so these tests exercise the
            // production validation rules rather than the permissive development validator.
            services.RemoveAll<IPlayValidator>();
            services.RemoveAll<IPlayPurchaseLookup>();
            services.AddSingleton(PlayLookup);
            services.AddScoped<IPlayValidator, AndroidPublisherPlayValidator>();

            services.RemoveAll<IGeminiClient>();
            services.AddSingleton(Gemini);

            services.RemoveAll<IPageFetcher>();
            services.AddSingleton(PageFetcher);

            // TestServer has no transport, so Connection.RemoteIpAddress is null — which the free
            // credit policy correctly treats as "origin unknown, grant nothing". Kestrel always has
            // one, so give the test host an address to make it behave like production.
            services.AddSingleton<IStartupFilter>(new ClientAddressStartupFilter(ClientAddress));
        });
    }

    /// <summary>The address the hosted app sees for every request. Tests may vary it per instance.</summary>
    public System.Net.IPAddress ClientAddress { get; set; } = System.Net.IPAddress.Parse("203.0.113.10");

    private sealed class ClientAddressStartupFilter(System.Net.IPAddress address) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress ??= address;
                await nextMiddleware();
            });
            next(app);
        };
    }

    private sealed class CapturingLoggerProvider(List<string> sink) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(sink);
        public void Dispose() { }

        private sealed class CapturingLogger(List<string> sink) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel)) return;
                lock (sink)
                    sink.Add($"{logLevel}: {formatter(state, exception)} {exception}");
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }
}
