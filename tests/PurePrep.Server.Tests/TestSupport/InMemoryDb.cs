using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PurePrep.Server.Data;

namespace PurePrep.Server.Tests.TestSupport;

/// <summary>
/// A real SQLite database held in memory for the lifetime of the test. Uses the actual provider
/// rather than the EF in-memory provider so raw SQL, constraints and concurrency behave as they do
/// in production — the credit ledger relies on all three.
/// </summary>
public sealed class InMemoryDb : IDisposable, IDbContextFactory<ServerDbContext>
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ServerDbContext> _options;

    public InMemoryDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ServerDbContext>().UseSqlite(_connection).Options;

        using var db = CreateDbContext();
        db.Database.EnsureCreated();
    }

    public ServerDbContext CreateDbContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}
