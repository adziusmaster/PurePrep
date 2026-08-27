using FluentAssertions;
using PurePrep.Server.Services;
using PurePrep.Server.Tests.TestSupport;

namespace PurePrep.Server.Tests.Services;

/// <summary>
/// The free-credit seed cap. Anyone can invent a device GUID, so the only thing standing between a
/// script and unlimited free AI parses is how many *new* devices one origin may seed per window.
/// </summary>
public sealed class FreeCreditPolicyTests : IDisposable
{
    private const int FreeCredits = 10;
    private const int MaxDevicesPerIp = 3;

    private readonly InMemoryDb _db = new();

    private SqliteFreeCreditPolicy CreatePolicy() =>
        new(_db, new CreditOptions
        {
            FreeCredits = FreeCredits,
            MaxNewDevicesPerIpPerDay = MaxDevicesPerIp,
        });

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task ResolveAsync_ForTheFirstDeviceFromAnOrigin_ShouldGrantTheFullAllowance()
    {
        // Arrange
        var policy = CreatePolicy();

        // Act
        var granted = await policy.ResolveAsync(Guid.NewGuid(), "hash-a");

        // Assert
        granted.Should().Be(FreeCredits);
    }

    [Fact]
    public async Task ResolveAsync_WhenAnOriginExceedsItsDailyDeviceCap_ShouldGrantNothing()
    {
        // Arrange
        var policy = CreatePolicy();
        for (var i = 0; i < MaxDevicesPerIp; i++)
            await policy.ResolveAsync(Guid.NewGuid(), "hash-a");

        // Act
        var granted = await policy.ResolveAsync(Guid.NewGuid(), "hash-a");

        // Assert
        granted.Should().Be(0);
    }

    [Fact]
    public async Task ResolveAsync_WhenADifferentOriginIsAtItsCap_ShouldStillGrant()
    {
        // Arrange
        var policy = CreatePolicy();
        for (var i = 0; i < MaxDevicesPerIp + 2; i++)
            await policy.ResolveAsync(Guid.NewGuid(), "hash-a");

        // Act
        var granted = await policy.ResolveAsync(Guid.NewGuid(), "hash-b");

        // Assert
        granted.Should().Be(FreeCredits);
    }

    [Fact]
    public async Task ResolveAsync_WhenTheSameDeviceAsksTwice_ShouldNotConsumeTheCapTwice()
    {
        // Arrange
        var policy = CreatePolicy();
        var device = Guid.NewGuid();
        await policy.ResolveAsync(device, "hash-a");
        await policy.ResolveAsync(device, "hash-a");

        // Act — the cap should still have room for two more distinct devices.
        await policy.ResolveAsync(Guid.NewGuid(), "hash-a");
        var granted = await policy.ResolveAsync(Guid.NewGuid(), "hash-a");

        // Assert
        granted.Should().Be(FreeCredits);
    }

    [Fact]
    public async Task ResolveAsync_WhenEarlierSeedsAreOutsideTheWindow_ShouldGrantAgain()
    {
        // Arrange
        var policy = CreatePolicy();
        await using (var seed = _db.CreateDbContext())
        {
            for (var i = 0; i < MaxDevicesPerIp; i++)
                seed.DeviceSeeds.Add(new PurePrep.Server.Data.DeviceSeed
                {
                    DeviceId = Guid.NewGuid(),
                    IpHash = "hash-a",
                    SeededAt = DateTimeOffset.UtcNow.AddDays(-2),
                });
            await seed.SaveChangesAsync();
        }

        // Act
        var granted = await policy.ResolveAsync(Guid.NewGuid(), "hash-a");

        // Assert
        granted.Should().Be(FreeCredits);
    }

    [Fact]
    public async Task ResolveAsync_WhenTheOriginIsUnknown_ShouldGrantNothing()
    {
        // Arrange — a request with no usable client IP must not be a free bypass of the cap.
        var policy = CreatePolicy();

        // Act
        var granted = await policy.ResolveAsync(Guid.NewGuid(), null);

        // Assert
        granted.Should().Be(0);
    }
}
