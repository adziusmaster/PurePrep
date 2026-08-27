using FluentAssertions;
using PurePrep.Ai;

namespace PurePrep.Core.Tests.Ai;

/// <summary>
/// Characterization tests for the SSRF allow/deny policy. Only IP-literal hosts are used so the
/// suite never touches DNS and stays deterministic offline.
/// </summary>
public sealed class UrlGuardTests
{
    private readonly UrlGuard _guard = new();

    [Theory]
    [InlineData("http://8.8.8.8/recipe")]
    [InlineData("https://1.1.1.1/recipe")]
    [InlineData("https://[2606:4700:4700::1111]/recipe")]
    public async Task IsPublicHttpAsync_WhenAddressIsPublic_ShouldAllow(string url)
    {
        // Arrange & Act
        var allowed = await _guard.IsPublicHttpAsync(new Uri(url));

        // Assert
        allowed.Should().BeTrue();
    }

    [Theory]
    [InlineData("http://127.0.0.1/x", "loopback")]
    [InlineData("http://10.1.2.3/x", "private 10/8")]
    [InlineData("http://172.16.0.1/x", "private 172.16/12")]
    [InlineData("http://192.168.1.1/x", "private 192.168/16")]
    [InlineData("http://169.254.169.254/latest/meta-data", "cloud metadata")]
    [InlineData("http://100.64.0.1/x", "CGNAT")]
    [InlineData("http://0.0.0.0/x", "unspecified")]
    [InlineData("http://[::1]/x", "IPv6 loopback")]
    [InlineData("http://[fd00::1]/x", "IPv6 unique-local")]
    public async Task IsPublicHttpAsync_WhenAddressIsNotPublic_ShouldDeny(string url, string reason)
    {
        // Arrange & Act
        var allowed = await _guard.IsPublicHttpAsync(new Uri(url));

        // Assert
        allowed.Should().BeFalse(because: reason);
    }

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://8.8.8.8/x")]
    [InlineData("gopher://8.8.8.8/x")]
    public async Task IsPublicHttpAsync_WhenSchemeIsNotHttp_ShouldDeny(string url)
    {
        // Arrange & Act
        var allowed = await _guard.IsPublicHttpAsync(new Uri(url));

        // Assert
        allowed.Should().BeFalse();
    }

    [Fact]
    public async Task IsPublicHttpAsync_WhenHostDoesNotResolve_ShouldDeny()
    {
        // Arrange
        var url = new Uri("https://this-host-does-not-exist.invalid/recipe");

        // Act
        var allowed = await _guard.IsPublicHttpAsync(url);

        // Assert
        allowed.Should().BeFalse();
    }
}
