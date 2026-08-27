using FluentAssertions;
using PurePrep.Server.Services;

namespace PurePrep.Server.Tests.Services;

/// <summary>
/// The seed cap needs to recognise a repeat origin without the server keeping a record of which IP
/// addresses used the app — the privacy policy promises no such profile.
/// </summary>
public sealed class ClientIpHasherTests
{
    private const string Salt = "test-salt";

    [Fact]
    public void Hash_ForTheSameAddress_ShouldBeStable()
    {
        // Arrange
        var hasher = new ClientIpHasher(Salt);

        // Act & Assert
        hasher.Hash("203.0.113.7").Should().Be(hasher.Hash("203.0.113.7"));
    }

    [Fact]
    public void Hash_ForDifferentAddresses_ShouldDiffer()
    {
        // Arrange
        var hasher = new ClientIpHasher(Salt);

        // Act & Assert
        hasher.Hash("203.0.113.7").Should().NotBe(hasher.Hash("203.0.113.8"));
    }

    [Fact]
    public void Hash_UnderADifferentSalt_ShouldDiffer()
    {
        // Arrange — the salt is what stops the table being reversible by hashing the IPv4 space.
        var a = new ClientIpHasher("salt-one");
        var b = new ClientIpHasher("salt-two");

        // Act & Assert
        a.Hash("203.0.113.7").Should().NotBe(b.Hash("203.0.113.7"));
    }

    [Fact]
    public void Hash_ShouldNotContainTheAddressItself()
    {
        // Arrange
        var hasher = new ClientIpHasher(Salt);

        // Act
        var hash = hasher.Hash("203.0.113.7");

        // Assert
        hash.Should().NotContain("203.0.113.7");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Hash_WhenThereIsNoAddress_ShouldReturnNull(string? ip)
    {
        // Arrange
        var hasher = new ClientIpHasher(Salt);

        // Act & Assert
        hasher.Hash(ip).Should().BeNull();
    }
}
