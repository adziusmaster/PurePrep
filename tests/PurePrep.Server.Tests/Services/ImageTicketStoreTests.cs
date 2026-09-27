using FluentAssertions;
using PurePrep.Server.Services;

namespace PurePrep.Server.Tests.Services;

public sealed class ImageTicketStoreTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UnixEpoch;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public void TryRedeem_WhenTicketValid_ShouldSucceedOnlyOnce()
    {
        // Arrange
        var store = new ImageTicketStore(new Clock());
        var device = Guid.NewGuid();
        var ticket = store.Issue(device);

        // Act
        var first = store.TryRedeem(device, ticket);
        var second = store.TryRedeem(device, ticket);

        // Assert
        first.Should().BeTrue();
        second.Should().BeFalse();
    }

    [Fact]
    public void TryRedeem_WhenDifferentDevice_ShouldFail()
    {
        // Arrange
        var store = new ImageTicketStore(new Clock());
        var ticket = store.Issue(Guid.NewGuid());

        // Act
        var redeemed = store.TryRedeem(Guid.NewGuid(), ticket);

        // Assert
        redeemed.Should().BeFalse();
    }

    [Fact]
    public void TryRedeem_WhenOlderThanTenMinutes_ShouldFail()
    {
        // Arrange
        var clock = new Clock();
        var store = new ImageTicketStore(clock);
        var device = Guid.NewGuid();
        var ticket = store.Issue(device);
        clock.Now = clock.Now.AddMinutes(10).AddSeconds(1);

        // Act
        var redeemed = store.TryRedeem(device, ticket);

        // Assert
        redeemed.Should().BeFalse();
    }
}
