using FluentAssertions;
using PurePrep.Server.Data;
using PurePrep.Server.Services;
using PurePrep.Server.Tests.TestSupport;

namespace PurePrep.Server.Tests.Services;

/// <summary>
/// The audit trail is kept for abuse detection, not indefinitely. Without a sweep it grows forever
/// and becomes a long-term record of activity the privacy policy says is not retained.
/// </summary>
public sealed class UsageLogRetentionTests : IDisposable
{
    private readonly InMemoryDb _db = new();

    public void Dispose() => _db.Dispose();

    private async Task AddLogAsync(int daysAgo)
    {
        await using var db = _db.CreateDbContext();
        db.UsageLogs.Add(new UsageLog
        {
            DeviceHash = "hash",
            Host = "example.com",
            Success = true,
            At = DateTimeOffset.UtcNow.AddDays(-daysAgo),
        });
        await db.SaveChangesAsync();
    }

    private async Task<int> RemainingAsync()
    {
        await using var db = _db.CreateDbContext();
        return db.UsageLogs.Count();
    }

    [Fact]
    public async Task SweepAsync_ShouldDeleteEntriesOlderThanTheRetentionWindow()
    {
        // Arrange
        await AddLogAsync(daysAgo: 45);
        await AddLogAsync(daysAgo: 31);

        // Act
        var deleted = await UsageLogRetention.SweepAsync(_db, TimeSpan.FromDays(30));

        // Assert
        deleted.Should().Be(2);
        (await RemainingAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SweepAsync_ShouldKeepEntriesInsideTheRetentionWindow()
    {
        // Arrange
        await AddLogAsync(daysAgo: 1);
        await AddLogAsync(daysAgo: 29);

        // Act
        var deleted = await UsageLogRetention.SweepAsync(_db, TimeSpan.FromDays(30));

        // Assert
        deleted.Should().Be(0);
        (await RemainingAsync()).Should().Be(2);
    }

    [Fact]
    public async Task SweepAsync_WhenThereIsNothingToDelete_ShouldReportZero()
    {
        // Arrange & Act
        var deleted = await UsageLogRetention.SweepAsync(_db, TimeSpan.FromDays(30));

        // Assert
        deleted.Should().Be(0);
    }
}
